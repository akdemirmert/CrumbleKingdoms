using UnityEngine;

// Thor: Mjolnir. A heavy spinning hammer. Its first contact calls Thor's lightning down on the spot and the
// hammer smashes what it hit; a block it destroys does not stop it (it keeps 70% of its speed, twice at
// most). Shortly after the impact, or if it is lost, it flies back along an arc into Thor's hand.
public class MjolnirHammer : Projectile {
    const float Mass = 7f;
    const float SpinSpeed = 900f;          // deg/s of the visual tumble in flight
    const float BreakThroughKeep = 0.7f;   // share of the pre-impact velocity kept after smashing a block
    const int MaxBreakThroughs = 2;
    const float ReturnDelay = 0.7f;        // seconds after the first impact...
    const float SlowSpeed = 1.5f;          // ...or sooner once it has (almost) stopped
    const float ReturnTime = 1.1f;
    const float FadeTime = 0.5f;

    enum Phase { Flying, Smashing, Returning, Fading }

    Phase phase;
    Transform spin;
    BoxCollider box;
    Light glow;
    GameObject trail, sparks;
    Vector3 lastVelocity;     // velocity before the current physics step (collision callbacks only see the result)
    bool smashing = true;     // still breaking through: the next thing it hits takes full damage
    int breakThroughs;
    float impactAge, phaseTime, returnSpin, arcTimer;
    Vector3 returnFrom, returnControl;
    bool underwater;

    bool InFlight => phase == Phase.Flying || phase == Phase.Smashing;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        SFX.Play(SoundId.LightningCrack, origin, 0.5f, 1.3f);
        VFX.Sparks(origin, GodKit.ThorBlue, 12);
        WeaponFactory.Spawn<MjolnirHammer>("Mjolnir", owner, def, origin, velocity);
    }

    protected override void OnLaunch(Vector3 velocity) {
        spin = ModelKit.Root("Spin", transform).transform;
        Model(spin).transform.localPosition = new Vector3(-0.12f, 0f, 0f);   // tumble around the heavy head
        box = gameObject.AddComponent<BoxCollider>();
        box.size = new Vector3(0.6f, 0.6f, 0.45f);
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;   // only the model spins
        lastVelocity = velocity;
        trail = VFX.Trail(transform, GodKit.ThorBlue, 0.45f, 0.22f);
        sparks = VFX.Emitter(transform, GodKit.ThorBlue * 2f, 0.12f, 30f);
        glow = GodKit.Glow(transform, GodKit.ThorBlue, 4f, 6f);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished || !InFlight) return;
        lastVelocity = rb.linearVelocity;
        float sinceImpact = age - impactAge;
        if (phase == Phase.Smashing && (sinceImpact >= ReturnDelay || (sinceImpact > 0.25f && lastVelocity.magnitude < SlowSpeed)))
            BeginReturn();
    }

    // Fell into the sea, left the arena or flew too long: Mjolnir always comes back.
    protected override void OnLost() { BeginReturn(); }

    void OnCollisionEnter(Collision c) {
        if (!InFlight) return;
        Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        if (phase == Phase.Flying) {              // first contact: Thor answers
            phase = Phase.Smashing;
            impactAge = age;
            RaiseImpact(p);
            ThorLightning.Strike(p, def, owner);
        }
        IDamageable target = c.collider.GetComponentInParent<IDamageable>();
        if (target == null || !smashing) return;   // the ground does not use up the smash
        smashing = false;
        if (target.IsAlive) target.ApplyDamage(new DamageInfo(def.damage, DamageType.Impact, p, owner, lastVelocity * 0.6f));
        if (target is Block block && !block.IsAlive && breakThroughs < MaxBreakThroughs) {
            // The block is gone: carry on through the rubble with most of the speed it had before the hit.
            breakThroughs++;
            smashing = true;
            Physics.IgnoreCollision(box, c.collider);   // the broken block's collider lives until the end of the frame
            rb.linearVelocity = lastVelocity * BreakThroughKeep;
            SFX.Play(SoundId.HammerHit, p, 0.6f, 0.85f);
            BattleCamera.Shake(0.25f);
        }
    }

    void Update() {
        float dt = Time.deltaTime;
        switch (phase) {
            case Phase.Flying: spin.Rotate(0f, 0f, -travelDir * SpinSpeed * dt); break;
            case Phase.Smashing: spin.Rotate(0f, 0f, -travelDir * SpinSpeed * 0.3f * dt); break;
            case Phase.Returning: UpdateReturn(dt); break;
            case Phase.Fading: UpdateFade(dt); break;
        }
        Crackle(dt);
        CheckWater();
    }

    // ------------------------------------------------------------------ return to Thor
    void BeginReturn() {
        if (!InFlight) return;
        // No more physics: the hammer follows a curve we drive ourselves.
        rb.linearVelocity = Vector3.zero;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;   // the only mode kinematic bodies support
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        box.enabled = false;
        cameraFollows = false;                   // the camera stays on the destruction
        phaseTime = 0f;
        if (owner == null || !owner.CommanderAlive) { BeginFade(); return; }

        phase = Phase.Returning;
        returnFrom = transform.position;
        Vector3 hand = owner.commander.LaunchPoint;
        float lift = Mathf.Clamp(Vector3.Distance(returnFrom, hand) * 0.3f, 3f, 14f);
        returnControl = (returnFrom + hand) * 0.5f + Vector3.up * lift;
        returnControl.y = Mathf.Max(returnControl.y, Mathf.Max(returnFrom.y, hand.y) + 2f);   // arc over the fortresses
        returnSpin = (hand.x >= returnFrom.x ? -1f : 1f) * SpinSpeed * 1.4f;
        glow.intensity = 6f;
        VFX.Sparks(returnFrom, GodKit.ThorBlue, 16);
        SFX.Play(SoundId.LightningCrack, returnFrom, 0.6f, 1.15f);
    }

    void UpdateReturn(float dt) {
        if (owner == null || !owner.CommanderAlive) { BeginFade(); return; }
        phaseTime += dt;
        float k = Mathf.Clamp01(phaseTime / ReturnTime);
        Vector3 hand = owner.commander.LaunchPoint;   // re-read: Thor may have moved
        transform.position = GodKit.Bezier(returnFrom, returnControl, hand, Mathf.SmoothStep(0f, 1f, k));
        spin.Rotate(0f, 0f, returnSpin * dt);
        if (k >= 1f) Arrive(hand);
    }

    void Arrive(Vector3 hand) {
        VFX.Sparks(hand, GodKit.ThorBlue, 24);
        VFX.LightFlash(hand, GodKit.ThorBlue, 6f, 6f, 0.3f);
        SFX.Play(SoundId.HammerReturn, hand);
        if (owner.commander.rig != null) owner.commander.rig.Flash(GodKit.ThorBlue, 0.3f);
        End();
    }

    // Thor has fallen: with no hand to return to, the hammer fades away where it is.
    void BeginFade() {
        phase = Phase.Fading;
        phaseTime = 0f;
        VFX.MagicBurst(transform.position, GodKit.ThorBlue, 0.6f);
        SFX.Play(SoundId.LightningCrack, transform.position, 0.4f, 1.4f);
    }

    void UpdateFade(float dt) {
        phaseTime += dt;
        float k = Mathf.Clamp01(1f - phaseTime / FadeTime);
        spin.localScale = Vector3.one * k;
        spin.Rotate(0f, 0f, 720f * dt);
        glow.intensity = 4f * k;
        if (k <= 0f) End();
    }

    void End() {
        GodKit.Release(trail);
        GodKit.Release(sparks);
        Finish(true);
    }

    // ------------------------------------------------------------------ flourishes
    // Little electric arcs jumping off the hammer, faster on the way back.
    void Crackle(float dt) {
        if (phase == Phase.Fading) return;
        arcTimer -= dt;
        if (arcTimer > 0f) return;
        arcTimer = phase == Phase.Returning ? 0.05f : 0.12f;
        Vector3 p = transform.position;
        Vector3 dir = Random.insideUnitCircle.normalized;
        VFX.Bolt(p, p + dir * Random.Range(0.5f, 1f), GodKit.ThorBlue, 0.05f, 0.08f);
    }

    // Splash when it plunges into the sea, and again when it bursts out on the way home.
    void CheckWater() {
        bool under = transform.position.y < World.WaterY;
        if (under == underwater) return;
        underwater = under;
        Vector3 s = new Vector3(transform.position.x, World.WaterY, 0f);
        VFX.Splash(s, 1.3f);
        SFX.Play(SoundId.Splash, s, 0.8f);
    }

    // ------------------------------------------------------------------ model
    // Dark steel head with glowing blue runes (long axis vertical), a short leather-wrapped handle trailing
    // behind (-X), a pommel and a wrist strap.
    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Mjolnir", parent);
        Transform r = root.transform;
        Material steel = Mats.Lit(new Color(0.36f, 0.38f, 0.43f), 0.55f, 0.55f);
        Material trim = Mats.Lit(new Color(0.62f, 0.65f, 0.7f), 0.7f, 0.6f);
        Material rune = Mats.Emissive(new Color(0.35f, 0.55f, 0.85f), new Color(0.7f, 1.4f, 3.6f));
        Material leather = Mats.Lit(new Color(0.4f, 0.24f, 0.13f), 0.25f);
        Material strap = Mats.Lit(new Color(0.24f, 0.14f, 0.08f), 0.2f);

        Vector3 h = new Vector3(0.2f, 0f, 0f);     // head centre
        ModelKit.Part(r, PrimitiveType.Cube, h, new Vector3(0.36f, 0.5f, 0.36f), steel);
        for (int s = -1; s <= 1; s += 2) {
            ModelKit.Part(r, PrimitiveType.Cube, h + new Vector3(0f, s * 0.275f, 0f), new Vector3(0.3f, 0.05f, 0.3f), steel);   // chamfer
            ModelKit.Part(r, PrimitiveType.Cube, h + new Vector3(0f, s * 0.31f, 0f), new Vector3(0.24f, 0.02f, 0.24f), trim);   // striking face
            for (int z = -1; z <= 1; z += 2)
                ModelKit.Part(r, PrimitiveType.Cube, h + new Vector3(s * 0.18f, 0f, z * 0.18f), new Vector3(0.025f, 0.5f, 0.025f), trim);   // edge trim
        }
        for (int s = -1; s <= 1; s += 2) {          // runes on both broad faces
            float z = s * 0.182f;
            ModelKit.Part(r, PrimitiveType.Cube, h + new Vector3(0f, 0.2f, z), new Vector3(0.3f, 0.016f, 0.008f), rune);
            ModelKit.Part(r, PrimitiveType.Cube, h + new Vector3(0f, -0.2f, z), new Vector3(0.3f, 0.016f, 0.008f), rune);
            // Thurisaz, Thor's rune: a stem with a thorn
            ModelKit.Part(r, PrimitiveType.Cube, h + new Vector3(-0.05f, 0f, z), new Vector3(0.02f, 0.28f, 0.008f), rune);
            ModelKit.Part(r, PrimitiveType.Cube, h + new Vector3(-0.005f, 0.045f, z), new Vector3(0.018f, 0.13f, 0.008f), rune, new Vector3(0f, 0f, 45f));
            ModelKit.Part(r, PrimitiveType.Cube, h + new Vector3(-0.005f, -0.045f, z), new Vector3(0.018f, 0.13f, 0.008f), rune, new Vector3(0f, 0f, -45f));
        }

        GodKit.Rod(r, new Vector3(0f, 0f, 0f), new Vector3(0.04f, 0f, 0f), 0.13f, steel);        // collar
        GodKit.Rod(r, new Vector3(-0.4f, 0f, 0f), new Vector3(0.02f, 0f, 0f), 0.085f, leather);   // grip
        for (int i = 0; i < 5; i++)                                                                 // leather wraps
            ModelKit.Part(r, PrimitiveType.Cylinder, new Vector3(-0.05f - i * 0.07f, 0f, 0f), new Vector3(0.095f, 0.008f, 0.095f), strap, new Vector3(0f, 0f, 70f));
        GodKit.Rod(r, new Vector3(-0.46f, 0f, 0f), new Vector3(-0.39f, 0f, 0f), 0.12f, steel);   // pommel
        ModelKit.Part(r, PrimitiveType.Sphere, new Vector3(-0.47f, 0f, 0f), Vector3.one * 0.1f, trim);
        Vector3 loop = new Vector3(-0.54f, -0.05f, 0f);                                             // wrist strap
        for (int i = 0; i < 9; i++) {
            float a = i * 40f;
            Vector3 p = loop + new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad), 0f) * 0.08f;
            ModelKit.Part(r, PrimitiveType.Cube, p, new Vector3(0.06f, 0.018f, 0.018f), strap, new Vector3(0f, 0f, a + 90f));
        }
        return root;
    }
}

// Thor's answer to the hammer's first impact: a bolt from the sky onto the spot and an electric blast.
static class ThorLightning {
    const float SkyHeight = 28f;
    const float BoltZ = -1f;       // just in front of the fortresses (blocks span z = ±0.8), so towers never hide it

    public static void Strike(Vector3 p, WeaponDef def, Fortress owner) {
        TimeFX.SlowMo(0.3f, 0.08f);   // a short hit-stop sells the weight (a commander's death slow-mo overrides it)
        Color c = GodKit.ThorBlue;
        Vector3 ground = new Vector3(p.x, p.y, BoltZ);
        Vector3 sky = new Vector3(p.x + Random.Range(-3f, 3f), p.y + SkyHeight, BoltZ);
        VFX.Bolt(sky, ground, c, 0.6f, 0.4f);
        for (int i = 0; i < 3; i++) {   // forks branching off the main bolt
            Vector3 from = Vector3.Lerp(sky, ground, Random.Range(0.25f, 0.75f));
            VFX.Bolt(from, from + new Vector3(Random.Range(-5f, 5f), Random.Range(-7f, -3f), 0f), c, 0.2f, 0.25f);
        }
        Explosion.Create(p, def.radius, def.damage * 0.6f, def.force, owner, DamageType.Lightning, BlastStyle.Lightning);
        VFX.Sparks(p, c, 30);
        VFX.LightFlash(p + Vector3.up * 3f, c, 16f, 30f, 0.4f);
        VFX.ScreenFlash(new Color(0.8f, 0.9f, 1f, 0.45f), 0.2f);
        SFX.Play(SoundId.Thunder, p);
        SFX.Play(SoundId.HammerHit, p);
        BattleCamera.Shake(0.7f);
    }
}

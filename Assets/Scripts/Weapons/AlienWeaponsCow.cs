using UnityEngine;

// The Alien's Flying Cow: a heavy (5 kg), bouncy cow that tumbles head over heels. It moos when it is thrown
// and again when it first lands. Every hard bounce on an enemy block or commander deals def.damage scaled by
// the impact speed; a block it destroys does not stop it (it keeps most of its speed, twice at most). Its part
// of the turn ends SettleTime after the first impact or once it lies still; then a small saucer comes and beams
// it back up (AlienCowPickup). A cow that lands in the sea just sinks.
public class AlienCow : Projectile {
    const float Mass = 5f;
    const float MinHitSpeed = 3f, FullDamageSpeed = 22f, MaxDamageScale = 1.4f;
    const float BreakThroughKeep = 0.6f;
    const int MaxBreakThroughs = 2;
    const float SettleTime = 2.5f, SlowSpeed = 1.2f;
    const float HitCooldown = 0.25f;         // one bounce on one target counts once

    static PhysicsMaterial hide;

    BoxCollider box;
    Vector3 lastVelocity;                    // before this physics step (collision callbacks only see the result)
    float firstImpactAge = -1f, lastHitTime = -1f, lastThud = -1f;
    Collider lastHit;
    int breakThroughs;

    // Rubbery: a cow bounces off walls and the ground (Maximum: the bounciest of the two surfaces wins).
    static PhysicsMaterial Hide => hide != null ? hide : hide = new PhysicsMaterial("Cow") {
        dynamicFriction = 0.45f, staticFriction = 0.5f, bounciness = 0.55f,
        frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Maximum,
    };

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        AlienSounds.Play(AlienSounds.Moo, origin, 0.9f);
        VFX.FloatingText(origin + Vector3.up * 0.8f, "MOO!", Color.white, 0.9f);
        WeaponFactory.Spawn<AlienCow>("Flying Cow", owner, def, origin, velocity);
    }

    protected override void OnLaunch(Vector3 velocity) {
        Transform model = AlienKit.Cow(transform).transform;
        if (travelDir < 0) model.localRotation = Quaternion.Euler(0f, 180f, 0f);   // head first
        box = gameObject.AddComponent<BoxCollider>();
        box.size = new Vector3(1.25f, 0.85f, 0.58f);                 // tail to snout, hooves to horns
        box.center = new Vector3(0.1f * travelDir, -0.03f, 0f);
        box.sharedMaterial = Hide;
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        rb.angularDamping = 0.3f;
        rb.angularVelocity = new Vector3(0f, 0f, -travelDir * Random.Range(2.5f, 5f));   // tumbling head over heels
        lastVelocity = velocity;
        VFX.Trail(transform, new Color(1f, 1f, 1f, 0.35f), 0.4f, 0.18f);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished) return;
        if (transform.position.y < World.WaterY) { Sink(); return; }
        lastVelocity = rb.linearVelocity;
        if (firstImpactAge < 0f) return;
        float since = age - firstImpactAge;
        if (since > SettleTime || (since > 0.5f && lastVelocity.sqrMagnitude < SlowSpeed * SlowSpeed)) Settle();
    }

    void OnCollisionEnter(Collision c) {
        if (finished) return;
        Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        float speed = c.relativeVelocity.magnitude;
        if (firstImpactAge < 0f) {
            firstImpactAge = age;
            RaiseImpact(p);
            AlienSounds.Play(AlienSounds.Moo, p, 1f, 1.3f);       // a surprised one
            VFX.FloatingText(p + Vector3.up * 1.4f, "MOO!", Color.white, 1.2f);
        }
        if (speed < MinHitSpeed) return;
        float k = Mathf.Clamp01(speed / FullDamageSpeed);
        Vector3 dir = lastVelocity.sqrMagnitude > 0.01f ? lastVelocity.normalized : Vector3.down;
        IDamageable target = c.collider.GetComponentInParent<IDamageable>();
        bool fresh = c.collider != lastHit || Time.time > lastHitTime + HitCooldown;
        if (target != null && target.IsAlive && fresh && !IsOwnTeam(c.collider)) {
            lastHit = c.collider;
            lastHitTime = Time.time;
            float damage = def.damage * Mathf.Min(speed / FullDamageSpeed, MaxDamageScale);
            target.ApplyDamage(new DamageInfo(damage, DamageType.Impact, p, owner, dir * speed * 0.5f));
            if (target is Block block) {
                SFX.PlayImpact(block.material, p, k);
                if (!block.IsAlive) BreakThrough(c.collider);
            } else {
                SFX.Play(SoundId.Bounce, p, 0.5f + 0.5f * k, 0.75f);
            }
        } else if (Time.time > lastThud + 0.12f) {
            lastThud = Time.time;
            SFX.Play(SoundId.Bounce, p, 0.35f + 0.5f * k, 0.7f);
        }
        VFX.Dust(p, 0.4f + 0.7f * k);
        BattleCamera.Shake(0.05f + 0.2f * k);
    }

    // The block gave way: the cow carries on through the rubble with most of the speed it had.
    void BreakThrough(Collider broken) {
        if (breakThroughs >= MaxBreakThroughs) return;
        breakThroughs++;
        Physics.IgnoreCollision(box, broken);    // the broken block's collider lives until the end of the frame
        rb.linearVelocity = lastVelocity * BreakThroughKeep;
        lastVelocity = rb.linearVelocity;
    }

    void Settle() {
        Finish(false);
        AlienSounds.Play(AlienSounds.Moo, transform.position, 0.55f, 1.45f);
        AlienCowPickup.Begin(this);
    }

    void Sink() {
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);
        VFX.Splash(p, 1.3f);
        SFX.Play(SoundId.Splash, p, 0.9f);
        AlienSounds.Play(AlienSounds.Moo, p, 0.6f, 0.7f);     // glug
        if (!impacted) RaiseImpact(p);
        Finish(true, 0.8f);
    }

    protected override void OnLost() { Finish(true); }
}

// When the cow's part of the turn is over, a small saucer comes for it: it drops in above the cow, beams it up
// and zips off. Purely for show (the cow no longer takes part in the battle).
public class AlienCowPickup : MonoBehaviour {
    const float Height = 4.2f, Size = 0.55f;
    const float ArriveTime = 0.5f, LiftTime = 1.1f, LeaveTime = 0.6f;

    Transform saucer, hull, cow;
    AlienBeam beam;
    Vector3 hover, cowFrom, cowScale, away;
    float t;
    int stage;                               // 0 arriving, 1 lifting the cow, 2 leaving

    public static void Begin(AlienCow c) {
        foreach (Collider col in c.GetComponentsInChildren<Collider>()) col.enabled = false;
        Rigidbody body = c.rb;
        if (body != null) {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;   // the only mode kinematic bodies support
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
        }
        AlienCowPickup p = new GameObject("Cow Pickup").AddComponent<AlienCowPickup>();
        p.cow = c.transform;
        p.cowScale = c.transform.localScale;
        p.hover = c.transform.position + Vector3.up * Height;
        p.away = new Vector3(Random.value < 0.5f ? -1f : 1f, 1.6f, 0f).normalized;
        p.saucer = AlienKit.Saucer(p.transform, true, false, out p.hull, out _).transform;
        p.saucer.position = p.hover + Vector3.up * 6f;
        p.saucer.localScale = Vector3.one * 0.001f;
        AlienSounds.Play(AlienSounds.Warble, p.hover, 0.55f, 1.3f);
    }

    void Update() {
        float dt = Time.deltaTime;
        t += dt;
        hull.Rotate(0f, 320f * dt, 0f, Space.Self);
        if (stage == 0) {                                        // drops in from above, growing
            float k = Mathf.Clamp01(t / ArriveTime);
            saucer.position = Vector3.Lerp(hover + Vector3.up * 6f, hover, Mathf.SmoothStep(0f, 1f, k));
            saucer.localScale = Vector3.one * Mathf.Max(0.001f, Size * k);
            if (k < 1f) return;
            if (cow == null) { Next(); return; }
            beam = AlienBeam.Create(transform, hover + Vector3.down * 0.1f, Height - 0.2f, 0.18f, 0.75f);
            cowFrom = cow.position;
            AlienSounds.Play(AlienSounds.BeamOn, hover, 0.6f, 1.3f);
            Next();
        } else if (stage == 1) {                                 // the cow rises, spinning, and shrinks into it
            if (cow == null) { Next(); return; }
            float k = Mathf.Clamp01(t / LiftTime);
            cow.position = Vector3.Lerp(cowFrom, hover, k * k);
            cow.Rotate(0f, 0f, 120f * dt, Space.World);
            cow.localScale = cowScale * Mathf.Max(0.001f, 1f - Mathf.SmoothStep(0.7f, 1f, k));
            if (k < 1f) return;
            Destroy(cow.gameObject);
            if (beam != null) beam.Close();
            AlienSounds.Play(AlienSounds.Abduct, hover, 0.8f, 1.2f);
            AlienSounds.Play(AlienSounds.Zoom, hover, 0.6f, 1.2f);
            Next();
        } else {                                                 // and off it goes
            float k = Mathf.Clamp01(t / LeaveTime);
            saucer.position = hover + away * (k * k * 18f);
            saucer.localScale = Vector3.one * Mathf.Max(0.001f, Size * (1f - k));
            if (k >= 1f) Destroy(gameObject);
        }
    }

    void Next() {
        stage++;
        t = 0f;
    }
}

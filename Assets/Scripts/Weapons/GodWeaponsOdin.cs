using UnityEngine;

// Odin: Gungnir, the spear that never misses. A piercing spear whose budget (def.pierce = 99) nothing can
// use up. Once past the top of its arc it locks onto the enemy commander its throw is heading for and steers
// in. Odin's ravens Huginn and Muninn circle it in flight. Where it finally stops, a holy burst.
public class GungnirSpear : PiercingProjectile {
    const float HomingTurnRate = 110f;                         // degrees per second
    const float HolyRadius = 2.2f, HolyDamage = 30f, HolyForce = 8f;
    const float IconScale = 0.6f;                              // menu/icon model ~1 m; the thrown spear is 1.7 m
    const float RavenOrbitSpeed = 420f;                        // degrees per second around the shaft

    static readonly float[] BandX = { -0.6f, -0.2f, 0.18f };
    static readonly float[] RuneX = { -0.5f, -0.43f, -0.36f, -0.29f, -0.08f, -0.01f, 0.06f };
    static readonly float[] HeadX = { 0f, 0.06f, 0.16f, 0.3f, 0.42f };   // leaf-shaped head profile
    static readonly float[] HeadW = { 0.03f, 0.078f, 0.085f, 0.05f, 0f };

    Commander target;
    Transform mark;          // follows the target's chest: the homing aims there, not at its feet
    bool lockedOn;
    Transform ravenOrbit;
    float orbitAngle;
    GameObject trail, sparkle;
    Light glow;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        SFX.Play(SoundId.SpearThrow, origin);
        SFX.Play(SoundId.MagicCast, origin, 0.45f, 0.8f);
        VFX.MagicBurst(origin, GodKit.Gold, 0.6f);
        WeaponFactory.Spawn<GungnirSpear>("Gungnir", owner, def, origin, velocity, s => {
            s.sweepRadius = 0.15f;
            s.slowPerCost = 0.05f;
            s.stopSpeed = 3f;
            s.embedDepth = 0.35f;
            s.commanderDamage = def.damage;    // it always reaches its commander, so no extra commander bonus
            s.homingTurnRate = HomingTurnRate;
            s.homingAfterApex = true;
        });
    }

    public static GameObject Model(Transform parent) { return Build(parent, IconScale); }

    protected override void OnLaunch(Vector3 velocity) {
        base.OnLaunch(velocity);
        Build(transform, 1f);
        trail = VFX.Trail(transform, GodKit.Gold, 0.22f, 0.4f);
        trail.transform.localPosition = new Vector3(-0.8f, 0f, 0f);     // streams from the butt of the spear
        sparkle = VFX.Emitter(transform, GodKit.Gold * 1.5f, 0.08f, 25f);
        sparkle.transform.localPosition = new Vector3(0.6f, 0f, 0f);    // around the golden head
        glow = GodKit.Glow(transform, GodKit.Gold, 3f, 5f);
        ravenOrbit = ModelKit.Root("Ravens", transform).transform;
        OdinRaven.Create(ravenOrbit, new Vector3(-0.2f, 0.65f, 0f), 0f);     // Huginn
        OdinRaven.Create(ravenOrbit, new Vector3(-0.45f, -0.65f, 0f), 1.7f); // Muninn
        Stopped += OnStopped;
    }

    protected override void FixedUpdate() {
        if (!finished && !IsStuck) {
            if (!lockedOn && manualVelocity.y < 0f) LockOn();          // past the apex
            if (target != null && mark != null) mark.position = target.AimPoint;
        }
        base.FixedUpdate();
    }

    void Update() {
        if (ravenOrbit == null) return;
        orbitAngle = Mathf.Repeat(orbitAngle + RavenOrbitSpeed * Time.deltaTime, 360f);
        ravenOrbit.localRotation = Quaternion.Euler(orbitAngle, 0f, 0f);   // circle around the flight axis
    }

    void LockOn() {
        lockedOn = true;
        target = TargetAhead();
        if (target == null) return;
        mark = new GameObject("Gungnir mark").transform;
        mark.position = target.AimPoint;
        homingTarget = mark;
        VFX.Ring(target.AimPoint, 1.2f, GodKit.Gold, 0.6f, 0.2f);
        VFX.MagicBurst(transform.position, GodKit.Gold, 0.5f);
        SFX.Play(SoundId.MagicCast, transform.position, 0.6f, 1.4f);
    }

    // The living enemy commander nearest to where the throw is heading (its unguided arc), so in a 1vN battle
    // the spear honours the fortress it was thrown at.
    Commander TargetAhead() {
        if (BattleManager.I == null || owner == null) return null;
        Commander best = null;
        float bestMiss = float.MaxValue;
        float wind = BattleManager.Wind * windFactor;
        foreach (Fortress f in BattleManager.I.EnemiesOf(owner)) {
            if (!f.CommanderAlive) continue;
            Vector3 aim = f.commander.AimPoint;
            float y = Ballistics.HeightAtX(transform.position, manualVelocity, gravityScale, wind, aim.x);
            float miss = float.IsNaN(y) ? 1000f + Vector3.Distance(transform.position, aim) : Mathf.Abs(y - aim.y);
            if (miss < bestMiss) { bestMiss = miss; best = f.commander; }
        }
        return best;
    }

    void OnStopped(PiercingProjectile self, Vector3 point, Collider hit) {
        // Odin's light bursts out where the spear stops.
        Explosion.Create(point, HolyRadius, HolyDamage, HolyForce, owner, DamageType.Pierce, BlastStyle.Holy);
        VFX.MagicBurst(point, GodKit.Gold, 1f);
        VFX.LightFlash(point, GodKit.Gold, 10f, 10f, 0.5f);
        SFX.Play(SoundId.MagicCast, point, 0.7f, 0.7f);
        BattleCamera.Shake(0.35f);
        GodKit.Release(trail);
        GodKit.Release(sparkle);
        if (glow != null) Destroy(glow.gameObject);
        if (mark != null) Destroy(mark.gameObject);
        ReleaseRavens();
        GodKit.DropIfFloating(this, hit);
    }

    void ReleaseRavens() {
        if (ravenOrbit == null) return;
        foreach (OdinRaven raven in ravenOrbit.GetComponentsInChildren<OdinRaven>()) raven.FlyAway(transform.right);
        Destroy(ravenOrbit.gameObject);
        ravenOrbit = null;
    }

    protected override void OnDestroy() {
        base.OnDestroy();
        if (mark != null) Destroy(mark.gameObject);
    }

    // ------------------------------------------------------------------ model
    // Dark ash shaft with gold bands and glowing golden runes, a long leaf-shaped golden head. Centred, +X forward.
    static GameObject Build(Transform parent, float scale) {
        GameObject root = ModelKit.Root("Gungnir", parent);
        Transform r = ModelKit.Root("Spear", root.transform).transform;
        r.localScale = Vector3.one * scale;
        Material ash = Mats.Lit(new Color(0.27f, 0.2f, 0.14f), 0.35f);
        Material gold = GodKit.GoldMetal;
        Material head =Mats.Emissive(new Color(1f, 0.84f, 0.45f), new Color(0.55f, 0.38f, 0.08f), 0.85f);
        Material rune = Mats.Emissive(GodKit.Gold, new Color(4f, 2.6f, 0.6f));

        GodKit.Rod(r, new Vector3(-0.8f, 0f, 0f), new Vector3(0.4f, 0f, 0f), 0.064f, ash);              // shaft
        foreach (float x in BandX) GodKit.Rod(r, new Vector3(x - 0.02f, 0f, 0f), new Vector3(x + 0.02f, 0f, 0f), 0.084f, gold);
        GodKit.Rod(r, new Vector3(-0.85f, 0f, 0f), new Vector3(-0.78f, 0f, 0f), 0.08f, gold);           // butt cap
        ModelKit.MeshPart(r, MeshKit.Cone(8, 0.05f, 0.1f), new Vector3(0.36f, 0f, 0f), Vector3.one, gold, new Vector3(0f, 0f, -90f), "Socket");
        ModelKit.MeshPart(r, GodKit.Blade("gungnirHead", HeadX, HeadW, 0.024f), new Vector3(0.44f, 0f, 0f), Vector3.one, head, default, "Head");
        ModelKit.Part(r, PrimitiveType.Cube, new Vector3(0.62f, 0f, 0f), new Vector3(0.26f, 0.012f, 0.06f), rune);   // glowing fuller
        for (int i = 0; i < RuneX.Length; i++) {     // runes carved into the camera-facing side of the shaft
            Vector3 c = new Vector3(RuneX[i], 0f, -0.033f);
            ModelKit.Part(r, PrimitiveType.Cube, c, new Vector3(0.007f, 0.044f, 0.006f), rune);
            ModelKit.Part(r, PrimitiveType.Cube, c + new Vector3(0.012f, 0.008f * (i % 3 - 1), 0f), new Vector3(0.006f, 0.028f, 0.006f), rune,
                          new Vector3(0f, 0f, i % 2 == 0 ? 40f : -40f));
        }
        return root;
    }
}

// Huginn or Muninn: a small raven circling Gungnir in flight (as a child of the spear's orbit pivot). When the
// spear stops, it flies off and is gone.
public class OdinRaven : MonoBehaviour {
    const float LeaveTime = 1.6f;

    Transform wingA, wingB;
    float flap, leftAt = -1f;
    Vector3 velocity;

    public static OdinRaven Create(Transform parent, Vector3 localPos, float flapPhase) {
        GameObject go = ModelKit.Root("Raven", parent);
        go.transform.localPosition = localPos;
        Transform t = go.transform;
        Material black = Mats.Lit(new Color(0.06f, 0.06f, 0.08f), 0.6f);
        Material beak = Mats.Lit(new Color(0.18f, 0.17f, 0.15f), 0.4f);
        ModelKit.Part(t, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.24f, 0.09f, 0.1f), black);                  // body
        ModelKit.Part(t, PrimitiveType.Sphere, new Vector3(0.12f, 0.03f, 0f), Vector3.one * 0.085f, black);            // head
        ModelKit.MeshPart(t, MeshKit.Cone(5, 0.018f, 0.06f), new Vector3(0.155f, 0.025f, 0f), Vector3.one, beak, new Vector3(0f, 0f, -90f), "Beak");
        ModelKit.Part(t, PrimitiveType.Cube, new Vector3(-0.15f, 0f, 0f), new Vector3(0.1f, 0.012f, 0.08f), black, new Vector3(0f, 0f, 8f));   // tail
        OdinRaven raven = go.AddComponent<OdinRaven>();
        raven.wingA = Wing(t, 1f, black);
        raven.wingB = Wing(t, -1f, black);
        raven.flap = flapPhase;
        return raven;
    }

    // A wing hinged at the shoulder, reaching out sideways (along Z).
    static Transform Wing(Transform body, float side, Material mat) {
        Transform pivot = ModelKit.Root("Wing", body).transform;
        pivot.localPosition = new Vector3(0.01f, 0.03f, 0f);
        ModelKit.Part(pivot, PrimitiveType.Cube, new Vector3(-0.02f, 0f, side * 0.12f), new Vector3(0.12f, 0.01f, 0.22f), mat);
        return pivot;
    }

    public void FlyAway(Vector3 forward) {
        transform.SetParent(null, true);
        velocity = new Vector3(forward.x * 3f, 2.5f, 0f);
        transform.rotation = Quaternion.Euler(0f, velocity.x < 0f ? 180f : 0f, 25f);   // beak first, climbing
        leftAt = Time.time;
    }

    void Update() {
        float dt = Time.deltaTime;
        flap += dt * 20f;
        float a = Mathf.Sin(flap) * 50f;
        wingA.localRotation = Quaternion.Euler(a, 0f, 0f);
        wingB.localRotation = Quaternion.Euler(-a, 0f, 0f);
        if (leftAt < 0f) return;
        velocity += Vector3.up * 5f * dt;
        transform.position += velocity * dt;
        if (Time.time - leftAt > LeaveTime) Destroy(gameObject);
    }
}

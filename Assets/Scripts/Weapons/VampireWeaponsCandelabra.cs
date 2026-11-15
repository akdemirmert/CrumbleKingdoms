using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Burning Candelabra: a golden three-armed candelabra with lit candles, spinning end over end so its flames draw
// fiery spirals and drip wax. On its first contact it breaks apart: the three flames burst one after another in
// a fan of fire blasts (each a full Explosion with def.radius / def.damage / def.force, Fire damage) that set the
// enemy's wood around them alight, while its arms fly off still burning. In the sea it is simply doused.
public class VampireCandelabra : Projectile {
    const float Mass = 3f, Radius = 0.36f;
    const float SpinSpeed = 640f;          // deg/s
    const float CentreHeight = 0.48f;      // the model spins about this point of its stem
    const float FanAngle = 50f;            // the side blasts burst this far either side of the centre one...
    const float FanReach = 1.4f;           // ...and this far from the impact
    const float Stagger = 0.08f;           // seconds between the blasts
    const float IgniteSeconds = 5f;
    static readonly Collider[] near = new Collider[64];

    Transform spin;
    Transform[] flames;
    ParticleSystem wax;
    Light glow;
    AudioSource whirl;
    Vector3 lastVelocity;                  // before the current physics step (the collision callback sees the bounce)
    bool burst;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        SFX.Play(SoundId.FireIgnite, origin, 0.5f, 1.3f);
        VFX.Burst(origin, 14, 3f, 0.12f, 0.6f, VampireKit.Flame, -0.2f, true, TexKit.SoftCircle);
        WeaponFactory.Spawn<VampireCandelabra>("Burning Candelabra", owner, def, origin, velocity);
    }

    // Upright, as on a table (menus and icons).
    public static GameObject Model(Transform parent) { return Build(parent, out _); }

    protected override void OnLaunch(Vector3 velocity) {
        spin = ModelKit.Root("Spin", transform).transform;
        Build(spin, out flames).transform.localPosition = new Vector3(0f, -CentreHeight, 0f);
        gameObject.AddComponent<SphereCollider>().radius = Radius;
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;   // only the model spins
        lastVelocity = velocity;
        foreach (Transform f in flames) VFX.Emitter(f, VampireKit.Flame, 0.22f, 40f);   // spirals of fire
        wax = VampireKit.Drips(transform, VampireKit.Wax, 14f, 0.07f);
        glow = VampireKit.Glow(transform, new Color(1f, 0.62f, 0.25f), 3.5f, 6f);
        whirl = VampireSounds.Loop(VampireSounds.Whirl, transform, 0.4f);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished || burst) return;
        if (transform.position.y < World.WaterY) { Douse(); return; }
        lastVelocity = rb.linearVelocity;
    }

    void Update() {
        if (burst || finished) return;
        spin.Rotate(0f, 0f, -travelDir * SpinSpeed * Time.deltaTime);
        glow.intensity = 3.5f * (0.8f + 0.35f * Mathf.PerlinNoise(Time.time * 9f, 0.5f));
    }

    void OnCollisionEnter(Collision c) {
        if (burst || finished) return;
        Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        p.z = 0f;
        Vector3 normal = transform.position - p;   // from the surface towards the candelabra
        normal.z = 0f;
        normal = normal.sqrMagnitude > 1e-4f ? normal.normalized : -lastVelocity.normalized;
        StartCoroutine(Burst(p, normal));
    }

    // Three fire blasts in a fan pointing between the flight direction and straight into the surface.
    IEnumerator Burst(Vector3 p, Vector3 normal) {
        burst = true;
        RaiseImpact(p);
        rb.linearVelocity = Vector3.zero;
        rb.detectCollisions = false;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeAll;
        Vector3 into = (lastVelocity.normalized - normal).normalized;
        if (into.sqrMagnitude < 0.5f) into = -normal;
        BreakApart(p, normal);
        Vector3 origin = p + normal * 0.2f;
        Vector3[] blasts = {
            origin + into * 0.4f,
            origin + Quaternion.Euler(0f, 0f, FanAngle) * into * FanReach,
            origin + Quaternion.Euler(0f, 0f, -FanAngle) * into * FanReach,
        };
        for (int i = 0; i < blasts.Length; i++) {
            if (i > 0) yield return new WaitForSeconds(Stagger);
            Vector3 q = new Vector3(blasts[i].x, blasts[i].y, 0f);
            Explosion.Create(q, def.radius, def.damage, def.force, owner, DamageType.Fire, BlastStyle.Fire);
            SetAblaze(q);
            VFX.Burst(q, 20, 6f, 0.15f, 0.9f, VampireKit.Flame, 0.5f, true, TexKit.SoftCircle);
        }
        yield return new WaitForSeconds(0.1f);
        Finish(true);
    }

    // The enemy's wood (and powder kegs) within a blast's reach catches fire; it spreads by itself (Block.SpreadFire).
    void SetAblaze(Vector3 p) {
        int n = Physics.OverlapSphereNonAlloc(p, def.radius, near, Layers.BlockMask, QueryTriggerInteraction.Ignore);
        List<Block> wood = new List<Block>();
        for (int i = 0; i < n; i++) {
            Block b = near[i] != null ? near[i].GetComponent<Block>() : null;
            if (b != null && b.IsAlive && b.def.flammable && !IsOwnTeam(b)) wood.Add(b);
        }
        foreach (Block b in wood) b.Ignite(IgniteSeconds, owner);
    }

    // The candelabra itself is gone: a golden clang, splashes of wax, and its three arms thrown back off the surface.
    void BreakApart(Vector3 p, Vector3 normal) {
        spin.gameObject.SetActive(false);   // the flame emitters see their candles vanish and fade out
        StopFlight();
        VampireSounds.Play(VampireSounds.Clang, p, 0.8f);
        VFX.Sparks(p, VampireKit.Gold, 18);
        VFX.Burst(p, 26, 5f, 0.08f, 0.9f, VampireKit.Wax, 1.2f, false, TexKit.SoftCircle);
        for (int i = 0; i < 3; i++) BrokenArm(p, normal);
        BattleCamera.Shake(0.3f);
    }

    // A broken arm with its candle still burning: a little piece of real debris (it bounces), gone in a few seconds.
    static void BrokenArm(Vector3 p, Vector3 normal) {
        Vector3 v = normal * Random.Range(3f, 6f) + new Vector3(Random.Range(-3f, 3f), Random.Range(2f, 5f), 0f);
        Vector3 at = p + normal * 0.35f + (Vector3)(Random.insideUnitCircle * 0.25f);
        Debris arm = Debris.Spawn(at, Random.rotation, new Vector3(0.34f, 0.045f, 0.045f), VampireKit.GoldMetal, 3f,
                                  v, Random.insideUnitSphere * 14f, 2.2f);
        Transform t = arm.transform;
        ModelKit.MeshPart(t, WorldMeshes.Frustum(8, 0.075f, 0.04f, 0.04f), new Vector3(0.17f, 0f, 0f), Vector3.one, VampireKit.GoldMetal, default, "Cup");
        ModelKit.MeshPart(t, WorldMeshes.Frustum(8, 0.034f, 0.032f, 0.14f), new Vector3(0.17f, 0.03f, 0f), Vector3.one, VampireKit.WaxMat, default, "Candle");
        GameObject flame = ModelKit.MeshPart(t, WorldMeshes.Ball(0.04f), new Vector3(0.17f, 0.22f, 0f), new Vector3(1f, 1.8f, 1f), VampireKit.FlameMat, default, "Flame");
        ModelKit.Shadows(flame, false, false);
        VFX.Emitter(flame.transform, VampireKit.Flame, 0.14f, 25f);
    }

    void Douse() {
        Vector3 p = transform.position;
        if (!impacted) RaiseImpact(p);
        VFX.Splash(new Vector3(p.x, World.WaterY, 0f), 0.9f);
        VFX.Dust(new Vector3(p.x, World.WaterY + 0.3f, 0f), 0.9f, new Color(0.92f, 0.93f, 0.95f, 0.55f));   // steam
        SFX.Play(SoundId.Splash, p, 0.7f);
        StopFlight();
        Finish(true);
    }

    protected override void OnLost() {
        StopFlight();
        Finish(true);
    }

    void StopFlight() {
        VampireKit.Release(wax, 1f);
        wax = null;
        if (glow != null) Destroy(glow.gameObject);
        SFX.Stop(whirl);
        whirl = null;
    }

    protected override void OnDestroy() {
        base.OnDestroy();
        SFX.Stop(whirl);
    }

    // ------------------------------------------------------------------ model
    // A golden candelabra about 1 m tall: a round foot, a stem with two knops, two curved arms and three cups with
    // ivory candles (the middle one tallest) and glowing flames. `flames` = the three flame parts.
    static GameObject Build(Transform parent, out Transform[] flames) {
        GameObject root = ModelKit.Root("Candelabra", parent);
        Transform r = root.transform;
        Material gold = VampireKit.GoldMetal, wax = VampireKit.WaxMat;
        ModelKit.MeshPart(r, WorldMeshes.Frustum(10, 0.17f, 0.05f, 0.07f), Vector3.zero, Vector3.one, gold, default, "Foot");
        ModelKit.MeshPart(r, WorldMeshes.Frustum(8, 0.028f, 0.028f, 0.5f), new Vector3(0f, 0.06f, 0f), Vector3.one, gold, default, "Stem");
        ModelKit.MeshPart(r, WorldMeshes.Ball(0.05f), new Vector3(0f, 0.2f, 0f), Vector3.one, gold, default, "Knop");
        ModelKit.MeshPart(r, WorldMeshes.Ball(0.045f), new Vector3(0f, 0.43f, 0f), Vector3.one, gold, default, "Knop");
        ModelKit.MeshPart(r, ArmsMesh(), Vector3.zero, Vector3.one, gold, default, "Arms");

        Vector3[] cups = { new Vector3(-0.34f, 0.6f, 0f), new Vector3(0f, 0.56f, 0f), new Vector3(0.34f, 0.6f, 0f) };
        float[] heights = { 0.2f, 0.27f, 0.2f };
        flames = new Transform[cups.Length];
        for (int i = 0; i < cups.Length; i++) {
            Vector3 c = cups[i];
            float h = heights[i];
            ModelKit.MeshPart(r, WorldMeshes.Frustum(10, 0.04f, 0.075f, 0.045f), c, Vector3.one, gold, default, "Cup");
            ModelKit.MeshPart(r, WorldMeshes.Frustum(10, 0.036f, 0.034f, h), c + Vector3.up * 0.04f, Vector3.one, wax, default, "Candle");
            ModelKit.MeshPart(r, WorldMeshes.Ball(0.017f), c + new Vector3(0.034f, 0.04f + h * 0.7f, -0.01f), new Vector3(1f, 1.7f, 1f), wax, default, "Drip");
            GameObject flame = ModelKit.MeshPart(r, WorldMeshes.Ball(0.042f), c + Vector3.up * (0.09f + h), new Vector3(1f, 1.8f, 1f),
                                                 VampireKit.FlameMat, default, "Flame");
            ModelKit.Shadows(flame, false, false);
            flames[i] = flame.transform;
        }
        return root;
    }

    // The two curved arms: from the upper knop out and up to the side cups.
    static Mesh ArmsMesh() {
        return VampireKit.Cached("candelabraArms", () => {
            List<Vector3> v = new List<Vector3>();
            List<int> t = new List<int>();
            const int N = 10;
            for (int s = -1; s <= 1; s += 2) {
                Vector3 a = new Vector3(s * 0.02f, 0.43f, 0f), bend = new Vector3(s * 0.38f, 0.36f, 0f), b = new Vector3(s * 0.34f, 0.6f, 0f);
                Vector3[] path = new Vector3[N];
                float[] radii = new float[N];
                for (int i = 0; i < N; i++) {
                    path[i] = VampireKit.Bezier(a, bend, b, i / (N - 1f));
                    radii[i] = 0.02f;
                }
                VampireKit.AddTube(v, t, path, radii, 8);
            }
            return VampireKit.ToMesh("Candelabra Arms", v, t);
        });
    }
}

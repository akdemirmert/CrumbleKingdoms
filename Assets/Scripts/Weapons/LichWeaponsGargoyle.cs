using UnityEngine;

// Gargoyle Dive: the Lich throws a crouching stone gargoyle, tumbling like any statue.
//  * Asleep (no click): it lands like a heavy stone (StoneShare of def.damage on what it hits) and crumbles.
//  * On a click (or the bot's hint) it wakes: its eyes light up green, its wings snap open for a moment and fold
//    back, and it dives straight down at DiveSpeed with extra gravity. In the dive it smashes through up to
//    MaxBreakThroughs blocks while its pierce budget lasts (def.pierce, like a piercing weapon: wood costs 1, stone
//    1.6) or when the block gives way: each takes def.damage and it keeps BreakThroughKeep of its speed. Where it
//    finally stops it bursts in a small blast (def.radius, BlastShare of def.damage). A commander in its way takes
//    def.damage and stops it. In the sea it just sinks.
public class GargoyleDive : Projectile {
    const float Mass = 4f, Radius = 0.38f;
    const float StoneShare = 0.6f, BlastShare = 0.5f;
    const float DiveSpeed = 28f, DiveGravity = 2.2f, KeepSideways = 0.08f;
    const int MaxBreakThroughs = 2;
    const float BreakThroughKeep = 0.7f;
    const float SmashSpeed = 8f;                     // slower than this it stops on what it hits
    const float SpreadTime = 0.14f;                  // wings open, then fold for the dive

    Transform model, wingL, wingR;
    Renderer[] eyes;
    Vector3 lastVelocity;
    SphereCollider body;
    float tumble, wakeAge = -1f, pierceLeft;
    int breakThroughs;
    bool diving;

    public override bool CanActivate => base.CanActivate && !diving;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        SFX.Play(SoundId.LaunchHeavy, origin, 0.6f, 0.9f);
        WeaponFactory.Spawn<GargoyleDive>("Gargoyle", owner, def, origin, velocity);
    }

    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Gargoyle", parent);
        Build(root.transform, out _, out _, out Renderer[] e);
        foreach (Renderer r in e) r.sharedMaterial = LichKit.SoulMat;   // the icon shows it awake
        return root;
    }

    protected override void OnLaunch(Vector3 velocity) {
        maxLifetime = 10f;
        model = Build(transform, out wingL, out wingR, out eyes).transform;
        body = gameObject.AddComponent<SphereCollider>();
        body.radius = Radius;
        pierceLeft = def.pierce;
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;   // only the model turns
        lastVelocity = velocity;
        LichSounds.WarmUp();
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished) return;
        if (diving) {
            // Extra gravity in the dive, and nearly nothing sideways: it drops on the spot it was woken over.
            rb.AddForce(Vector3.down * World.Gravity * (DiveGravity - 1f), ForceMode.Acceleration);
            Vector3 v = rb.linearVelocity;
            v.x = Mathf.MoveTowards(v.x, 0f, 30f * Time.fixedDeltaTime);
            rb.linearVelocity = v;
        }
        lastVelocity = rb.linearVelocity;
        if (transform.position.y < World.WaterY) Sink();
    }

    void Update() {
        if (finished) return;
        float dt = Time.deltaTime;
        if (!diving) {
            tumble += -travelDir * 260f * dt;               // an asleep statue tumbles end over end
            model.localRotation = Quaternion.Euler(0f, 0f, tumble);
            return;
        }
        float k = (age - wakeAge) / SpreadTime;             // wings: snap open, then fold tight for the dive
        float open = k < 1f ? Mathf.Sin(k * Mathf.PI) : 0f;
        SetWings(Mathf.Lerp(15f, 95f, open));
        Vector3 v = Velocity;
        float aim = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;   // head (+x) first along the dive
        model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(0f, 0f, aim), 1f - Mathf.Exp(-dt * 18f));
    }

    // Click: it wakes and dives.
    protected override void OnActivate() {
        diving = true;
        wakeAge = age;
        foreach (Renderer r in eyes) r.sharedMaterial = LichKit.SoulMat;
        LichKit.Glow(transform, new Vector3(0f, 0.2f, -0.7f), 2.5f, 4f);
        VFX.Trail(transform, new Color(0.55f, 1f, 0.4f, 0.5f), 0.3f, 0.25f);
        Vector3 v = rb.linearVelocity;
        rb.linearVelocity = new Vector3(v.x * KeepSideways, -Mathf.Max(DiveSpeed, Mathf.Abs(v.y)), 0f);
        LichSounds.Play(LichSounds.Wake, transform.position, 0.85f);
        LichKit.SoulBurst(transform.position, 0.6f, false);
        VFX.Dust(transform.position, 0.4f, new Color(0.62f, 0.64f, 0.6f, 0.5f));   // stone dust off its wings
        BattleCamera.Shake(0.1f);
    }

    void OnCollisionEnter(Collision c) {
        if (finished) return;
        SoldierWeapons.FirstContact(c, transform.position, out Vector3 point, out _);
        SoldierWeapons.LimitShove(c);
        point.z = 0f;
        if (!impacted) RaiseImpact(point);
        Block block = c.collider.GetComponentInParent<Block>();
        IDamageable target = block != null ? block : c.collider.GetComponentInParent<IDamageable>();
        bool enemy = target != null && target.IsAlive && !IsOwnTeam(c.collider);
        Vector3 dir = lastVelocity.sqrMagnitude > 0.01f ? lastVelocity.normalized : Vector3.down;

        if (!diving) {                                        // asleep: a heavy stone, then rubble
            if (enemy) Hit(c.collider, def.damage * StoneShare, DamageType.Impact, point, owner, dir * Mass * lastVelocity.magnitude * 0.5f);
            SFX.PlayImpact(BlockMaterial.Stone, point, 0.9f);
            Crumble(point, false);
            return;
        }
        if (enemy) {
            Hit(c.collider, def.damage, DamageType.Impact, point, owner, dir * Mass * lastVelocity.magnitude * 0.5f);
            VFX.Chips(point, BlockMaterial.Stone, 8, -dir);
            SFX.PlayImpact(block != null ? block.material : BlockMaterial.Stone, point, 1f);
            BattleCamera.Shake(0.2f);
            // On through the roof: the block gave way, or the dive still has the weight to smash through it.
            bool through = block != null && (!block.IsAlive || pierceLeft >= block.def.pierceCost);
            if (through && breakThroughs < MaxBreakThroughs && lastVelocity.magnitude > SmashSpeed) {
                breakThroughs++;
                if (block.IsAlive) pierceLeft -= block.def.pierceCost;
                if (c.collider.enabled) Physics.IgnoreCollision(body, c.collider);   // a broken block's collider is already off
                rb.linearVelocity = lastVelocity * BreakThroughKeep;
                lastVelocity = rb.linearVelocity;
                return;
            }
        }
        Crumble(point, true);
    }

    // It stops: rubble and stone dust; a woken gargoyle bursts in a blast of soulfire as well.
    void Crumble(Vector3 point, bool blast) {
        if (finished) return;
        if (blast) {
            Explosion.Create(point, def.radius, def.damage * BlastShare, def.force, owner, DamageType.Explosion, BlastStyle.Small);
            LichKit.SoulBurst(point, 0.9f);
        }
        VFX.Shatter(transform.position, Vector3.one * 0.7f, BlockMaterial.Stone);
        SFX.Play(SoundId.BreakStone, point, 0.8f, 0.9f);
        Finish(true);
    }

    void Sink() {
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);
        if (!impacted) RaiseImpact(p);
        VFX.Splash(p, 1f);
        SFX.Play(SoundId.Splash, p, 0.8f);
        Finish(true);
    }

    protected override void OnLost() { Finish(true); }

    void SetWings(float deg) {
        if (wingL != null) wingL.localRotation = Quaternion.Euler(-deg, 0f, 0f);
        if (wingR != null) wingR.localRotation = Quaternion.Euler(deg, 0f, 0f);
    }

    // ------------------------------------------------------------------ model
    // A crouching gargoyle about 0.9 m tall, facing +x: a squat body, a horned head with pointed ears and a fanged
    // snout, folded bat wings along its back (pivots turn them out to the sides), clawed feet and a curled tail.
    // Weathered grey-green stone with dark sockets; `eyes` light up when it wakes.
    static GameObject Build(Transform parent, out Transform wingL, out Transform wingR, out Renderer[] eyes) {
        GameObject root = ModelKit.Root("Gargoyle", parent);
        Transform r = root.transform;
        Material stone = LichKit.StoneMat, dark = LichKit.StoneDarkMat, socket = LichKit.SocketMat;
        // Body, haunches and chest.
        LichKit.Part(r, WorldMeshes.Ball(0.27f), new Vector3(-0.05f, -0.05f, 0f), new Vector3(1.2f, 1f, 0.95f), stone);
        LichKit.Part(r, WorldMeshes.Ball(0.2f), new Vector3(0.13f, 0.08f, 0f), new Vector3(1f, 1.05f, 0.9f), stone);
        for (int s = -1; s <= 1; s += 2) {
            LichKit.Part(r, WorldMeshes.Ball(0.13f), new Vector3(-0.14f, -0.2f, s * 0.15f), Vector3.one, dark);          // haunches
            LichKit.Rod(r, new Vector3(-0.06f, -0.27f, s * 0.15f), new Vector3(0.06f, -0.36f, s * 0.16f), 0.06f, 0.045f, stone);   // feet
            LichKit.Rod(r, new Vector3(0.18f, -0.02f, s * 0.12f), new Vector3(0.24f, -0.33f, s * 0.13f), 0.05f, 0.04f, stone);    // arms
            for (int k = -1; k <= 1; k++)                                                                                // claws
                LichKit.Part(r, MeshKit.Cone(4, 0.016f, 0.07f), new Vector3(0.27f, -0.35f, s * 0.13f + k * 0.025f), Vector3.one, dark, new Vector3(0f, 0f, -90f));
        }
        // Head: a horned skull-like face with a short snout and fangs.
        Transform head = LichKit.Pivot("Head", r, new Vector3(0.26f, 0.24f, 0f));
        LichKit.Part(head, WorldMeshes.Ball(0.15f), Vector3.zero, new Vector3(1.05f, 0.95f, 0.95f), stone);
        LichKit.Part(head, WorldMeshes.Ball(0.085f), new Vector3(0.13f, -0.05f, 0f), new Vector3(1.3f, 0.8f, 0.9f), stone);   // snout
        eyes = new Renderer[2];
        for (int s = -1; s <= 1; s += 2) {
            int i = s < 0 ? 0 : 1;
            eyes[i] = LichKit.Part(head, WorldMeshes.Ball(0.03f), new Vector3(0.1f, 0.04f, s * 0.07f), Vector3.one, socket).GetComponent<Renderer>();
            LichKit.Part(head, MeshKit.Cone(5, 0.04f, 0.22f), new Vector3(-0.04f, 0.1f, s * 0.07f), Vector3.one, dark, new Vector3(s * 12f, 0f, 58f));    // horns, swept back
            LichKit.Part(head, MeshKit.Cone(4, 0.035f, 0.1f), new Vector3(0f, 0.06f, s * 0.13f), Vector3.one, stone, new Vector3(s * -60f, 0f, 20f));     // ears
            LichKit.Part(head, MeshKit.Cone(4, 0.012f, 0.05f), new Vector3(0.2f, -0.09f, s * 0.03f), Vector3.one, LichKit.BoneMat, new Vector3(180f, 0f, 0f));   // fangs
        }
        // Wings: folded along the back, a spar and two fingers each, with a membrane of dark stone.
        wingL = Wing(r, -1, stone, dark);
        wingR = Wing(r, 1, stone, dark);
        // Tail curling under it.
        LichKit.Rod(r, new Vector3(-0.3f, -0.12f, 0f), new Vector3(-0.45f, -0.25f, 0f), 0.05f, 0.035f, stone);
        LichKit.Rod(r, new Vector3(-0.45f, -0.25f, 0f), new Vector3(-0.38f, -0.38f, 0f), 0.035f, 0.02f, stone);
        LichKit.Part(r, MeshKit.Cone(3, 0.04f, 0.08f), new Vector3(-0.37f, -0.39f, 0f), Vector3.one, dark, new Vector3(0f, 0f, -160f));
        ModelKit.Shadows(root, true);
        return root;
    }

    // A wing hinged at the shoulder: at rotation 0 it lies folded along the back (pointing up and back); the pivot's
    // x rotation swings it out towards the camera (-z) or away (+z).
    static Transform Wing(Transform parent, int side, Material spar, Material membrane) {
        Transform w = LichKit.Pivot(side < 0 ? "Wing Near" : "Wing Far", parent, new Vector3(0f, 0.12f, side * 0.12f));
        Vector3 tip = new Vector3(-0.42f, 0.4f, 0f), mid = new Vector3(-0.5f, 0.12f, 0f);
        LichKit.Rod(w, Vector3.zero, tip, 0.04f, 0.018f, spar);
        LichKit.Rod(w, tip * 0.55f, mid, 0.025f, 0.012f, spar);
        LichKit.Part(w, PrimitiveType.Cube, new Vector3(-0.3f, 0.2f, 0f), new Vector3(0.42f, 0.36f, 0.025f), membrane, new Vector3(0f, 0f, -18f));
        return w;
    }
}

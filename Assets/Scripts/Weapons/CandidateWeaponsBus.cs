using UnityEngine;

// Campaign Bus: the Candidate throws his campaign coach dead straight along the aim (WeaponDefs gives it no gravity and no
// wind, so the aim line and the bots shoot straight). Horn blaring, engine roaring, wheels spinning and smoke behind it, it
// flies until it touches something that is not his own side's and blows up there (Explosion), flying apart: its wheels,
// panels and loudspeaker as debris and a burst of ballot papers. His votes (CandidateVotes, read once at the throw) make it
// grow step by step: a bigger bus (scale = Radius), a wider blast (def.radius x Radius) that hits harder (def.damage, +28 %
// a tier: faster than his other skills, so it is mild with no votes and as hard as ever at full votes); it plows through the first block it can afford (pierce cost <= 1.6 x Radius: anything but metal), smashing it,
// before it blows up on the next thing; from tier 1 it sheds ballot papers in flight (more every tier); from tier 3 it
// wears a gold ram and plows through two blocks; from tier 4 gold glints follow it.
public static class CandidateBus {
    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        CandidateSounds.Play(CandidateSounds.BusHorn, origin, 0.9f);
        CandidateSounds.Play(CandidateSounds.BusEngine, origin, 0.8f);
        VFX.Dust(origin, 0.8f, new Color(0.55f, 0.55f, 0.58f, 0.5f));   // a puff of exhaust as it pulls away
        CandidateBusRide.Drive(owner, def, origin, velocity, CandidateVotes.Tier(owner), CandidateVotes.Damage(owner),
                               CandidateVotes.Radius(owner));
    }

    // Weapon icon: the bus side-on (front = +X).
    public static GameObject Model(Transform parent) { return CandidateBusModel.Build(parent, 0, out _); }
}

// The thrown bus. No rigidbody and no collider: it moves itself every physics step along its straight line, sweeping its
// box over the step first, so it never curves and never tunnels. It passes its own side's blocks, commanders and creatures as
// if they were not there. It plows through the first block it can afford (its pierce cost within PlowBudget x Radius),
// smashing it (from PlowTier on the first two), and blows up on the next solid thing it touches. Into the sea
// it only splashes; off into the sky, out of the arena or after MaxFlight it is gone quietly. Past every enemy fortress
// ahead without touching anything (PassMargin beyond the last one) its part of the turn is over at once: the camera lets
// it go and it drives on out of sight for FlyAwayTime.
public class CandidateBusRide : Projectile {
    public const int PlowTier = 3;
    const int PaperTier = 1, GlintTier = 4;
    const float MaxFlight = 6f, CeilingY = 80f;           // lost: flying on past everything, or off into the sky
    const float PassMargin = 3f, FlyAwayTime = 1.5f;       // m past the last enemy fortress; s it drives on unwatched
    const float GrowTime = 0.1f;                           // it grows in out of the throw (from 40 %)
    const float WheelSpin = 900f;                          // deg/s
    const float PlowBudget = 1.6f, PlowDamage = 40f;       // x Radius, x Damage
    const float BlastPerTier = 0.28f;                      // the blast's growth a vote tier (CandidateVotes.DamagePerTier is 0.15)
    const float PlowPush = 3f, PlowSlow = 0.85f;           // impulse on the plowed block (x sqrt mass); speed kept
    const float PaperRate = 7f;                            // ballot papers shed per second, per tier
    static readonly Vector3 BoxCentre = new Vector3(0f, -0.07f, 0f), BoxHalf = new Vector3(0.85f, 0.31f, 0.27f);
    static readonly Color DustTint = new Color(0.72f, 0.67f, 0.6f, 0.6f);
    static readonly RaycastHit[] castHits = new RaycastHit[24];
    static FxLayer paperLayer;

    int tier, plowsLeft;
    float damageBoost = 1f, radiusBoost = 1f, plowBudget, paperDue, spin;
    float passX = float.NaN;   // past this x (in the travel direction) it has missed every enemy fortress ahead
    bool grown, flyingAway;
    readonly System.Collections.Generic.HashSet<Collider> plowedThrough = new System.Collections.Generic.HashSet<Collider>();
    Quaternion facing = Quaternion.identity;
    Vector3 halfExtents;
    Transform model;
    Transform[] wheels;

    // The boosts are the votes' (CandidateVotes.Damage and Radius), read at the throw.
    public static void Drive(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity, int tier, float damageBoost,
                             float radiusBoost) {
        WeaponFactory.Spawn<CandidateBusRide>("Campaign Bus", owner, def, origin, velocity, b => {
            b.tier = tier;
            b.damageBoost = damageBoost;
            b.radiusBoost = radiusBoost;
        });
    }

    protected override void OnLaunch(Vector3 velocity) {
        manualVelocity = velocity;
        maxLifetime = MaxFlight;
        facing = FlightRotation(velocity.sqrMagnitude > 1e-4f ? velocity.normalized : new Vector3(travelDir, 0f, 0f));
        transform.rotation = facing;    // fixed for the whole flight: the line is straight
        model = CandidateBusModel.Build(transform, tier, out wheels).transform;
        model.localScale = Vector3.one * radiusBoost * 0.4f;
        halfExtents = BoxHalf * radiusBoost;
        plowBudget = PlowBudget * radiusBoost;
        plowsLeft = tier >= PlowTier ? 2 : 1;
        passX = PassLine();

        GameObject smoke = VFX.Emitter(transform, new Color(0.42f, 0.42f, 0.45f, 0.55f), 0.3f * radiusBoost, 14f);
        smoke.transform.localPosition = new Vector3(-0.95f, -0.19f, 0f) * radiusBoost;   // out of the exhaust
        GameObject streak = VFX.Trail(transform, CandidateKit.TealHdr * 0.45f, 0.4f * radiusBoost, 0.16f);
        streak.transform.localPosition = new Vector3(-0.8f, 0.05f, 0f) * radiusBoost;
        if (tier >= GlintTier) VFX.Emitter(transform, new Color(2.2f, 1.75f, 0.7f, 1f), 0.12f * radiusBoost, 24f);
    }

    // +X along the flight with the roof up: a bus going left is turned round (about Y), not flipped upside down.
    static Quaternion FlightRotation(Vector3 dir) {
        if (dir.x >= 0f) return Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        return Quaternion.Euler(0f, 180f, Mathf.Atan2(dir.y, -dir.x) * Mathf.Rad2Deg);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished) return;
        Vector3 p = transform.position;
        if (p.y < World.WaterY) { Splash(p.x); return; }
        if (p.y > CeilingY && manualVelocity.y > 0f) { OnLost(); return; }
        if (!float.IsNaN(passX) && (p.x - passX) * travelDir > 0f) { FlyAway(); return; }
        float dt = Time.fixedDeltaTime;
        Vector3 step = manualVelocity * dt;
        float len = step.magnitude;
        if (len > 1e-5f && Sweep(p, step / len, len)) return;
        transform.position = p + step;
        ShedPapers(dt);
    }

    // The x a little beyond the last enemy fortress ahead of the throw (NaN: none ahead, e.g. a throw backwards).
    float PassLine() {
        BattleManager bm = BattleManager.I;
        if (bm == null || owner == null) return float.NaN;
        float line = float.NaN, x = transform.position.x;
        foreach (Fortress f in bm.EnemiesOf(owner)) {
            Bounds b = f.GetBounds();
            if ((b.center.x - x) * travelDir <= 0f) continue;   // behind the throw
            float edge = travelDir > 0 ? b.max.x : b.min.x;
            if (float.IsNaN(line) || (edge - line) * travelDir > 0f) line = edge;
        }
        return float.IsNaN(line) ? line : line + travelDir * PassMargin;
    }

    // It missed them all: the turn goes on (the camera lets it go) while it drives off out of sight.
    void FlyAway() {
        flyingAway = true;
        cameraFollows = false;
        Finish(false);
        Destroy(gameObject, FlyAwayTime);
    }

    void Update() {
        if (flyingAway) transform.position += manualVelocity * Time.deltaTime;
        if (model == null) return;
        if (!grown) {
            float k = Mathf.Clamp01(age / GrowTime);
            model.localScale = Vector3.one * radiusBoost * Mathf.Lerp(0.4f, 1f, k);
            grown = k >= 1f;
        }
        spin = Mathf.Repeat(spin + WheelSpin * Time.deltaTime, 360f);
        foreach (Transform w in wheels) w.localRotation = Quaternion.Euler(0f, 0f, -spin);   // rolling forward
    }

    // Sweeps the bus's box over this step: it plows through what it may and blows up on the rest. True = the ride is over.
    bool Sweep(Vector3 p, Vector3 dir, float len) {
        Vector3 centre = p + facing * (BoxCentre * radiusBoost);
        int n = Physics.BoxCastNonAlloc(centre, halfExtents, dir, castHits, facing, len, Layers.SolidMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(castHits, 0, n, Nearest.Instance);
        for (int i = 0; i < n; i++) {
            RaycastHit h = castHits[i];
            Collider col = h.collider;
            if (col == null || plowedThrough.Contains(col) || IsOwnTeam(col)) continue;
            Block b = col.GetComponentInParent<Block>();
            if (b != null && !b.IsAlive) continue;
            // Something already inside the box at the start of the step (it fell onto the bus): a cast gives no point for it.
            bool inside = h.distance <= 0f && h.point == Vector3.zero;
            Vector3 point = inside ? Touch(col, centre + facing * new Vector3(halfExtents.x, 0f, 0f)) : h.point;
            if (b != null && plowsLeft > 0 && b.def.pierceCost <= plowBudget) {
                Plow(b, col, point, dir);
                continue;
            }
            transform.position = p + dir * h.distance;
            if (point.y < World.WaterY) Splash(point.x);   // the island's foot under the waterline
            else Blow(point);
            return true;
        }
        return false;
    }

    // Where the bus touches a collider it already overlaps (closest points only work on these shapes).
    static Vector3 Touch(Collider col, Vector3 from) {
        bool closest = col is BoxCollider || col is SphereCollider || col is CapsuleCollider || (col is MeshCollider m && m.convex);
        return closest ? col.ClosestPoint(from) : from;
    }

    // It rams through the block, smashing it, shoving it along and losing a little speed.
    void Plow(Block b, Collider col, Vector3 point, Vector3 dir) {
        plowsLeft--;
        plowedThrough.Add(col);
        Rigidbody body = b.rb;
        if (body != null && !body.isKinematic)
            body.AddForceAtPosition(dir * PlowPush * Mathf.Sqrt(body.mass), point, ForceMode.Impulse);
        SoundId thud = b.material switch {
            BlockMaterial.Stone => SoundId.ImpactStone,
            BlockMaterial.Metal => SoundId.ImpactMetal,
            BlockMaterial.Glass => SoundId.ImpactGlass,
            BlockMaterial.Ice => SoundId.ImpactGlass,
            _ => SoundId.ImpactWood,
        };
        Hit(b, PlowDamage * damageBoost, DamageType.Crush, point, owner, dir * 8f);
        RaiseImpact(point);
        manualVelocity *= PlowSlow;
        VFX.Sparks(point, CandidateKit.Gold, 18);
        VFX.Dust(point, 0.9f * radiusBoost, DustTint);
        SFX.Play(thud, point, 1f, 0.75f);
        BattleCamera.Shake(0.22f);
    }

    // The blast where it touched, the bus flying apart and a burst of ballot papers; the turn goes on without it.
    void Blow(Vector3 point) {
        RaiseImpact(point);
        float blast = def.radius * radiusBoost;
        Explosion.Create(point, blast, def.damage * (1f + BlastPerTier * tier), def.force, owner, DamageType.Explosion);
        BattleCamera.Shake(0.15f + 0.04f * tier);
        VFX.Ring(point, blast * 1.15f, CandidateKit.Teal, 0.45f);
        VFX.Sparks(point, CandidateKit.Gold, 20);
        CandidateBusModel.BreakUp(transform.position, facing, manualVelocity, radiusBoost);
        Ballots(point, 16 + 8 * tier);
        Finish(true);
    }

    // Into the sea: a big splash and nothing else.
    void Splash(float x) {
        Vector3 p = new Vector3(x, World.WaterY, 0f);
        if (!impacted) RaiseImpact(p);
        VFX.Splash(p, 1.5f * radiusBoost);
        SFX.Play(SoundId.Splash, p, 0.85f);
        Finish(true);
    }

    // From tier 1: ballot papers blow off the roof as it flies, more with every tier.
    void ShedPapers(float dt) {
        if (tier < PaperTier) return;
        paperDue += PaperRate * tier * dt;
        for (; paperDue >= 1f; paperDue -= 1f) {
            Vector3 at = transform.position + facing * (new Vector3(Random.Range(-0.8f, 0.2f), 0.36f, 0f) * radiusBoost);
            Vector3 v = manualVelocity * 0.3f + new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(1.5f, 3.5f), Random.Range(-0.6f, 0.6f));
            Papers.EmitChunk(CandidateKit.Front(at), v, new Vector3(0.13f, 0.09f, 0.008f) * radiusBoost, Random.Range(1.4f, 2.2f));
        }
    }

    static void Ballots(Vector3 at, int count) {
        Vector3 c = CandidateKit.Front(at);
        for (int i = 0; i < count; i++) {
            Vector3 v = new Vector3(Random.Range(-6f, 6f), Random.Range(5f, 12f), Random.Range(-1.5f, 0.5f));
            Papers.EmitChunk(c + (Vector3)(Random.insideUnitCircle * 0.5f), v, new Vector3(0.17f, 0.12f, 0.008f), Random.Range(2.2f, 3.4f));
        }
    }

    // Paper slips that tumble and flutter down slowly: particles of a layer of their own, so no objects.
    static FxLayer Papers {
        get {
            if (paperLayer != null) return paperLayer;
            LayerLook look = FxLooks.Chunk(CandidateKit.PaperMat);
            look.gravity = 0.2f;
            look.drag = 2.4f;
            look.maxParticles = 300;
            paperLayer = FxLayer.Create("Ballot papers", look, null);
            return paperLayer;
        }
    }

    class Nearest : System.Collections.Generic.IComparer<RaycastHit> {
        public static readonly Nearest Instance = new Nearest();
        public int Compare(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); }
    }
}

// The coach, about 1.7 m long, built from boxes and prisms: a deep teal lower half and a white upper half split by a gold
// pinstripe, a dark window strip with white pillars, a windscreen wrapping round the front, headlights glowing at the front
// corners, tail lights, bumpers, a loudspeaker horn on the roof and, between the wheels, a bright teal banner edged in gold
// with his mark on it (a white tick in a circle, built from geometry). Both sides are dressed alike: the camera sees one side
// of a bus going right and the other of one going left. Visual only (no colliders). Front = +X, wheels down (-Y).
static class CandidateBusModel {
    public const float WheelRadius = 0.165f, HubRadius = 0.095f;
    static readonly float[] Pillars = { -0.47f, -0.22f, 0.03f, 0.28f };
    static Mesh mark;

    static Material BodyMat => Mats.Lit(Color.Lerp(CandidateKit.Teal, CandidateKit.Navy, 0.35f), 0.5f);
    static Material WhiteMat => Mats.Lit(new Color(0.94f, 0.95f, 0.96f), 0.55f);
    static Material GlassMat => Mats.Lit(new Color(0.07f, 0.1f, 0.15f), 0.92f);
    static Material TrimMat => Mats.Lit(new Color(0.17f, 0.18f, 0.2f), 0.4f, 0.3f);
    static Material TyreMat => Mats.Lit(new Color(0.07f, 0.07f, 0.08f), 0.12f);
    static Material HubMat => Mats.Lit(new Color(0.7f, 0.72f, 0.76f), 0.7f, 0.7f);
    static Material SpeakerMat => Mats.Lit(new Color(0.86f, 0.87f, 0.9f), 0.6f, 0.3f);
    static Material HeadlightMat => Mats.Emissive(new Color(1f, 0.96f, 0.8f), new Color(3.2f, 2.9f, 2f));
    static Material TaillightMat => Mats.Emissive(new Color(0.85f, 0.12f, 0.1f), new Color(2.6f, 0.25f, 0.15f));

    // wheels: the two axles, which the ride turns about their local Z.
    public static GameObject Build(Transform parent, int tier, out Transform[] wheels) {
        GameObject root = ModelKit.Root("Campaign Bus", parent);
        Transform t = root.transform;
        Material white = WhiteMat, glass = GlassMat, gold = CandidateKit.GoldMat, dark = TrimMat;
        // the body: deep teal below, white above, a gold pinstripe between them and a white roof on top
        Box(t, new Vector3(0f, -0.09f, 0f), new Vector3(1.7f, 0.22f, 0.56f), BodyMat);
        Box(t, new Vector3(0f, 0.02f, 0f), new Vector3(1.705f, 0.022f, 0.566f), gold);
        Box(t, new Vector3(0f, 0.165f, 0f), new Vector3(1.7f, 0.27f, 0.56f), white);
        Box(t, new Vector3(-0.01f, 0.315f, 0f), new Vector3(1.64f, 0.04f, 0.5f), white);
        // the window strip with its pillars; the windscreen wraps round the front
        Box(t, new Vector3(-0.1f, 0.175f, 0f), new Vector3(1.22f, 0.15f, 0.57f), glass);
        foreach (float x in Pillars) Box(t, new Vector3(x, 0.175f, 0f), new Vector3(0.035f, 0.15f, 0.576f), white);
        Box(t, new Vector3(0.745f, 0.165f, 0f), new Vector3(0.22f, 0.21f, 0.57f), glass);
        // lights on the corners (so they show from the side), bumpers and the exhaust pipe
        for (int side = -1; side <= 1; side += 2) {
            Box(t, new Vector3(0.83f, -0.11f, 0.262f * side), new Vector3(0.055f, 0.07f, 0.06f), HeadlightMat);
            Box(t, new Vector3(-0.83f, -0.11f, 0.262f * side), new Vector3(0.055f, 0.07f, 0.06f), TaillightMat);
        }
        Box(t, new Vector3(0.858f, -0.18f, 0f), new Vector3(0.04f, 0.05f, 0.58f), dark);
        Box(t, new Vector3(-0.858f, -0.18f, 0f), new Vector3(0.04f, 0.05f, 0.58f), dark);
        ModelKit.MeshPart(t, WorldMeshes.Frustum(6, 0.022f, 0.022f, 0.09f), new Vector3(-0.85f, -0.19f, 0f), Vector3.one, dark,
                          new Vector3(0f, 0f, 90f), "Exhaust");
        // the banner between the wheels: bright teal edged in gold, his mark on each side
        Box(t, new Vector3(0f, -0.1f, 0f), new Vector3(0.62f, 0.15f, 0.572f), CandidateKit.TealMat);
        Box(t, new Vector3(0f, -0.022f, 0f), new Vector3(0.64f, 0.016f, 0.576f), gold);
        Box(t, new Vector3(0f, -0.178f, 0f), new Vector3(0.64f, 0.016f, 0.576f), gold);
        ModelKit.MeshPart(t, Mark, new Vector3(0f, -0.1f, -0.29f), Vector3.one * 0.065f, CandidateKit.PaperMat, Vector3.zero, "Mark");
        ModelKit.MeshPart(t, Mark, new Vector3(0f, -0.1f, 0.29f), Vector3.one * 0.065f, CandidateKit.PaperMat, new Vector3(0f, 180f, 0f), "Mark");
        // the loudspeaker on its post on the roof, calling ahead
        Box(t, new Vector3(0.4f, 0.375f, 0f), new Vector3(0.035f, 0.08f, 0.035f), dark);
        Loudspeaker(t, new Vector3(0.3f, 0.44f, 0f));
        // enough votes: a gold ram on the nose (it plows through two blocks)
        if (tier >= CandidateBusRide.PlowTier)
            ModelKit.MeshPart(t, MeshKit.Wedge(new Vector3(0.24f, 0.17f, 0.5f)), new Vector3(0.875f, -0.13f, 0f), Vector3.one, gold,
                              new Vector3(0f, 0f, -90f), "Ram");
        wheels = new[] { Wheel(t, new Vector3(0.56f, -0.24f, 0f), 0.6f), Wheel(t, new Vector3(-0.56f, -0.24f, 0f), 0.6f) };
        return root;
    }

    static void Box(Transform parent, Vector3 at, Vector3 size, Material mat) {
        ModelKit.Part(parent, PrimitiveType.Cube, at, size, mat);
    }

    // A loudspeaker horn pointing +X, its narrow end at `at`, a gold rim round its mouth.
    static void Loudspeaker(Transform parent, Vector3 at) {
        ModelKit.MeshPart(parent, WorldMeshes.Frustum(10, 0.035f, 0.105f, 0.22f), at, Vector3.one, SpeakerMat, new Vector3(0f, 0f, -90f), "Loudspeaker");
        ModelKit.MeshPart(parent, WorldMeshes.Frustum(10, 0.112f, 0.112f, 0.02f), at + new Vector3(0.21f, 0f, 0f), Vector3.one,
                          CandidateKit.GoldMat, new Vector3(0f, 0f, -90f), "Rim");
    }

    // A wheel (or an axle: depth = the width it spans) that turns about its local Z: a dark tyre, on each face a grey
    // hubcap with a bar across it (so the turning shows) and a gold nut.
    static Transform Wheel(Transform parent, Vector3 at, float depth) {
        Transform w = ModelKit.Root("Wheel", parent).transform;
        w.localPosition = at;
        ModelKit.MeshPart(w, WorldMeshes.Frustum(14, WheelRadius, WheelRadius, depth), new Vector3(0f, 0f, -0.5f * depth), Vector3.one,
                          TyreMat, new Vector3(90f, 0f, 0f), "Tyre");
        for (int side = -1; side <= 1; side += 2) {
            float z = side * (0.5f * depth + 0.002f);
            Vector3 face = side < 0 ? Vector3.zero : new Vector3(0f, 180f, 0f);
            ModelKit.MeshPart(w, WorldMeshes.Disc(12, HubRadius), new Vector3(0f, 0f, z), Vector3.one, HubMat, face, "Hubcap");
            ModelKit.Part(w, PrimitiveType.Cube, new Vector3(0f, 0f, z + side * 0.004f), new Vector3(1.8f * HubRadius, 0.026f, 0.005f), TrimMat);
            ModelKit.MeshPart(w, WorldMeshes.Disc(8, 0.028f), new Vector3(0f, 0f, z + side * 0.008f), Vector3.one, CandidateKit.GoldMat, face, "Nut");
        }
        return w;
    }

    // His mark: a tick in a circle of unit radius, flat in XY and facing -Z.
    static Mesh Mark {
        get {
            if (mark != null) return mark;
            MeshBuilder b = new MeshBuilder();
            Vector2 uv = new Vector2(0.5f, 0.5f);
            const int Segments = 28;
            for (int i = 0; i < Segments; i++) {
                float a0 = i * Mathf.PI * 2f / Segments, a1 = (i + 1) * Mathf.PI * 2f / Segments;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f), d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                b.Quad(b.Vertex(d0 * 0.8f, uv), b.Vertex(d1 * 0.8f, uv), b.Vertex(d1, uv), b.Vertex(d0, uv));   // the ring
            }
            Stroke(b, new Vector2(-0.44f, 0.02f), new Vector2(-0.12f, -0.32f), 0.115f);   // the tick: short stroke down...
            Stroke(b, new Vector2(-0.12f, -0.32f), new Vector2(0.46f, 0.38f), 0.115f);    // ...long stroke up
            mark = b.ToMesh("Candidate mark");
            return mark;
        }
    }

    // A straight stroke of the tick, its ends run on a little so the two strokes close where they meet.
    static void Stroke(MeshBuilder b, Vector2 from, Vector2 to, float halfWidth) {
        Vector2 d = (to - from).normalized, n = new Vector2(-d.y, d.x) * halfWidth;
        from -= d * halfWidth * 0.7f;
        to += d * halfWidth * 0.7f;
        Vector2 uv = new Vector2(0.5f, 0.5f);
        b.Quad(b.Vertex(from - n, uv), b.Vertex(from + n, uv), b.Vertex(to + n, uv), b.Vertex(to - n, uv));
    }

    // The bus flies apart where it blew up: its four wheels, panels of its body and the loudspeaker, real debris for a few
    // seconds (Debris shrinks them away). The wheels and the horn keep only their debris box as a collider and show their own
    // shape instead. They keep a little of the bus's speed.
    public static void BreakUp(Vector3 at, Quaternion facing, Vector3 velocity, float scale) {
        Vector3 carry = velocity * 0.2f;
        for (int i = 0; i < 4; i++) {
            Vector3 local = new Vector3(i < 2 ? 0.56f : -0.56f, -0.24f, i % 2 == 0 ? -0.24f : 0.24f) * scale;
            Debris d = Debris.Spawn(at + facing * local, facing * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)),
                                    new Vector3(2f * WheelRadius, 2f * WheelRadius, 0.12f) * scale, TyreMat, 1.4f,
                                    carry + Fling(), Random.insideUnitSphere * 25f, Random.Range(2.2f, 3.2f));
            d.GetComponent<MeshRenderer>().enabled = false;
            Wheel(d.transform, Vector3.zero, 0.12f).localScale = Vector3.one * scale;
        }
        Panel(at, facing, carry, scale, new Vector3(-0.45f, -0.09f, -0.28f), new Vector3(0.6f, 0.22f, 0.04f), BodyMat);
        Panel(at, facing, carry, scale, new Vector3(0.4f, -0.09f, 0.28f), new Vector3(0.5f, 0.22f, 0.04f), BodyMat);
        Panel(at, facing, carry, scale, new Vector3(-0.3f, 0.165f, -0.28f), new Vector3(0.55f, 0.27f, 0.04f), WhiteMat);
        Panel(at, facing, carry, scale, new Vector3(0.1f, 0.315f, 0f), new Vector3(0.7f, 0.04f, 0.5f), WhiteMat);
        Panel(at, facing, carry, scale, new Vector3(0.74f, 0.165f, 0f), new Vector3(0.05f, 0.21f, 0.45f), GlassMat);
        Panel(at, facing, carry, scale, new Vector3(0f, -0.1f, -0.29f), new Vector3(0.62f, 0.15f, 0.03f), CandidateKit.TealMat);
        Debris horn = Debris.Spawn(at + facing * (new Vector3(0.4f, 0.44f, 0f) * scale), Random.rotation, new Vector3(0.24f, 0.21f, 0.21f) * scale,
                                   SpeakerMat, 1f, carry + Fling(), Random.insideUnitSphere * 15f, Random.Range(2f, 3f));
        horn.GetComponent<MeshRenderer>().enabled = false;
        Transform shape = ModelKit.Root("Loudspeaker", horn.transform).transform;
        shape.localScale = Vector3.one * scale;
        Loudspeaker(shape, new Vector3(-0.12f, 0f, 0f));
    }

    static void Panel(Vector3 at, Quaternion facing, Vector3 carry, float scale, Vector3 local, Vector3 size, Material mat) {
        Quaternion tilt = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(-25f, 25f), Random.Range(-40f, 40f));
        Debris.Spawn(at + facing * (local * scale), facing * tilt, size * scale, mat, 1.2f, carry + Fling(),
                     Random.insideUnitSphere * 14f, Random.Range(2f, 3.2f));
    }

    // Thrown up and out by the blast.
    static Vector3 Fling() { return new Vector3(Random.Range(-6f, 6f), Random.Range(4f, 10f), Random.Range(-2f, 0.5f)); }
}

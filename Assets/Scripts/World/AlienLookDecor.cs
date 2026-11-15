using System.Collections.Generic;
using UnityEngine;

// The Alien fortress decorations (registered by AlienLook; FortressDecor has already planted the team flag and
// banners). On the anchors the generator left in the plan:
//   turret roof  the flag's tower gets a small flying saucer docked on legs (rim lights circling, the flag moves up
//                onto its dome); other turrets (a citadel's wings) get a turning satellite dish
//   shield       a glowing hexagonal beacon on the wall
//   torch        a hovering, turning hex crystal lamp on a bracket
//   base         a big satellite dish on a tripod in front of the walls
// plus antenna masts with blinking tips on the highest free tops (the battlements). Everything is parented to a
// block (so it falls with it) and has no collider; at night at most MaxLights point lights per fortress.
public static class AlienDecor {
    const int MaxLights = 3;
    // AlienKit.Saucer in model units; at this scale its feet land 0.39 m out, on top of a 0.8 x 1 m turret.
    const float SaucerScale = 1.3f, SaucerLegs = 0.37f, SaucerDome = 0.375f;

    static readonly Color Green = new Color(0.35f, 1f, 0.6f), Tip = new Color(0.75f, 1f, 0.35f);
    static Mesh dishMesh;

    static Material GlowMat => Mats.Emissive(Green * 0.6f, Green * 2.6f);
    static Material TipMat => Mats.Emissive(Tip * 0.7f, Tip * 4f);
    static Material DishMat => Mats.Lit(new Color(0.9f, 0.92f, 0.95f), 0.6f, 0.2f);

    // One decoration pass: which way the enemy is and the lights left.
    class Job {
        public int facing;
        public bool night;
        public int lights;

        public bool TakeLight() {
            if (!night || lights >= MaxLights) return false;
            lights++;
            return true;
        }
    }

    public static void Apply(Fortress f) {
        FortressPlan plan = f.blueprint as FortressPlan;
        if (plan == null || f.blocks.Count != plan.blocks.Count) return;   // anchors index the original block order
        Job job = new Job { facing = f.Facing >= 0 ? 1 : -1, night = ArenaBuilder.IsNight };
        HashSet<int> flagged = new HashSet<int>(), taken = new HashSet<int>();
        foreach (DecorAnchor a in plan.decor) if (a.kind == DecorKind.Flag) flagged.Add(a.block);
        foreach (DecorAnchor a in plan.decor) {
            Block host = a.block >= 0 && a.block < f.blocks.Count ? f.blocks[a.block] : null;
            if (host == null) continue;
            Transform t = host.transform;
            switch (a.kind) {
                case DecorKind.TurretRoof:
                    taken.Add(a.block);
                    if (flagged.Contains(a.block)) DockedSaucer(job, t, a.local);
                    else Dish(t, a.local, job.facing, 1f);
                    break;
                case DecorKind.Shield: HexBeacon(t, a.local); break;
                case DecorKind.Torch: HexLamp(job, t, a.local); break;
                case DecorKind.Base: GroundDish(job, host, a.local, a.span); break;
            }
        }
        Antennae(f, taken, job.facing);
    }

    // ------------------------------------------------------------------ on the towers
    // A small saucer docked on its legs; the team flag (already on this turret) moves up onto the dome.
    static void DockedSaucer(Job job, Transform host, Vector3 at) {
        Transform root = Root("Docked Saucer", host, at);
        Transform saucer = AlienKit.Saucer(root, false, true, out Transform hull, out _).transform;
        saucer.localPosition = Vector3.up * (SaucerLegs * SaucerScale);
        saucer.localScale = Vector3.one * SaucerScale;
        hull.gameObject.AddComponent<DecorMotion>().spin = new Vector3(0f, 35f, 0f);   // its rim lights circle
        if (job.TakeLight()) PointLight(root, new Vector3(0f, 0.2f, -1f), Green, 1.8f, 6f);
        Transform flag = host.Find("Flag");
        if (flag != null) flag.localPosition = at + Vector3.up * ((SaucerLegs + SaucerDome) * SaucerScale - 0.02f);
    }

    // A satellite dish on a mast, turning slowly to scan the sky, tilted up towards the enemy, with a blinking feed.
    static void Dish(Transform host, Vector3 at, int facing, float size) {
        Transform root = Root("Dish", host, at);
        Part(root, WorldMeshes.Frustum(6, 0.07f * size, 0.04f * size, 0.5f * size), Vector3.zero, AlienKit.DarkMetal);
        Transform head = Root("Head", root, Vector3.up * (0.5f * size));
        head.gameObject.AddComponent<DecorMotion>().spin = new Vector3(0f, 22f, 0f);
        Transform bowl = Root("Bowl", head, Vector3.zero);
        bowl.localRotation = Quaternion.Euler(-20f, 0f, -facing * 40f);
        Part(bowl, DishMesh, Vector3.zero, DishMat, default, Vector3.one * size);
        Part(bowl, WorldMeshes.Frustum(6, 0.07f * size, 0.05f * size, 0.06f * size), Vector3.zero, AlienKit.DarkMetal);
        AlienKit.Rod(bowl, Vector3.up * (0.05f * size), Vector3.up * (0.4f * size), 0.025f * size, AlienKit.DarkMetal);
        AlienBlink.Add(Part(bowl, WorldMeshes.Ball(0.045f * size), Vector3.up * (0.42f * size), TipMat), 1.3f, Random.value, true);
    }

    // ------------------------------------------------------------------ on the walls
    // A hexagonal beacon: a silver frame, a dark face and a glowing core that slowly pulses.
    static void HexBeacon(Transform host, Vector3 at) {
        Transform root = Root("Hex Beacon", host, at);
        Vector3 pointy = new Vector3(0f, 0f, 30f);
        Part(root, WorldMeshes.Disc(6, 0.27f), new Vector3(0f, 0f, -0.012f), AlienKit.SilverMetal, pointy);
        Part(root, WorldMeshes.Disc(6, 0.22f), new Vector3(0f, 0f, -0.02f), AlienKit.DarkMetal, pointy);
        for (int k = 0; k < 3; k++)                                       // three glowing spokes
            Part(root, PrimitiveType.Cube, new Vector3(0f, 0f, -0.026f), new Vector3(0.36f, 0.018f, 0.006f), GlowMat, new Vector3(0f, 0f, 30f + k * 60f));
        GameObject core = Part(root, WorldMeshes.Disc(6, 0.1f), new Vector3(0f, 0f, -0.032f), GlowMat, pointy);
        AlienBlink.Add(core, 2.2f, Random.value, false, 0.8f, 1.2f);
    }

    // A dark bracket with a silver cup and a hex crystal hovering and turning above it; a light at night.
    static void HexLamp(Job job, Transform host, Vector3 at) {
        Transform root = Root("Hex Lamp", host, at);
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0f, -0.1f), new Vector3(0.08f, 0.08f, 0.22f), AlienKit.DarkMetal);
        Part(root, WorldMeshes.Frustum(6, 0.05f, 0.1f, 0.05f), new Vector3(0f, -0.03f, -0.24f), AlienKit.SilverMetal);
        Transform crystal = Root("Crystal", root, new Vector3(0f, 0.2f, -0.24f));
        Material glow = GlowMat;
        Part(crystal, WorldMeshes.Frustum(6, 0.07f, 0.07f, 0.14f), new Vector3(0f, -0.07f, 0f), glow);
        Part(crystal, WorldMeshes.Frustum(6, 0.07f, 0f, 0.08f), new Vector3(0f, 0.07f, 0f), glow);
        Part(crystal, WorldMeshes.Frustum(6, 0.07f, 0f, 0.08f), new Vector3(0f, -0.07f, 0f), glow, new Vector3(180f, 0f, 0f));
        DecorMotion motion = crystal.gameObject.AddComponent<DecorMotion>();
        motion.bob = 0.03f;
        motion.spin = new Vector3(0f, 50f, 0f);
        if (job.TakeLight()) PointLight(crystal, new Vector3(0f, 0f, -0.3f), Green, 1.8f, 6f);
    }

    // ------------------------------------------------------------------ in front of the walls
    // A big dish on a tripod standing on the ground beyond the front pillar's outer face and what stands before it
    // (`clear` metres), far enough out that the turning bowl (1 m in radius) never sweeps through the wall.
    static void GroundDish(Job job, Block pillar, Vector3 at, float clear) {
        float face = pillar.size.x * 0.5f + clear;
        Transform root = Root("Ground Dish", pillar.transform, at + new Vector3(job.facing * (face + 1.15f), 0f, -0.6f));
        for (int i = 0; i < 3; i++) {
            float a = (90f + i * 120f) * Mathf.Deg2Rad;
            AlienKit.Rod(root, Vector3.up * 0.75f, new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f), 0.05f, AlienKit.DarkMetal);
        }
        Dish(root, Vector3.zero, job.facing, 2.2f);
    }

    // ------------------------------------------------------------------ on the tops
    // Antenna masts on up to two of the highest free tops (nothing stands on them), front first, at least 1.5 m
    // apart and not right over the commander's chamber.
    static void Antennae(Fortress f, HashSet<int> taken, int facing) {
        List<int> tops = new List<int>();
        float highest = float.MinValue;
        for (int i = 0; i < f.blocks.Count; i++) {
            if (f.blocks[i] == null || taken.Contains(i) || !Free(f.blocks, i)) continue;
            tops.Add(i);
            highest = Mathf.Max(highest, Top(f.blocks[i]));
        }
        tops.RemoveAll(i => Top(f.blocks[i]) < highest - 3.5f);
        tops.Sort((a, b) => (f.blocks[b].transform.localPosition.x * facing).CompareTo(f.blocks[a].transform.localPosition.x * facing));
        float room = f.blueprint.commanderSpot.x, lastX = float.NaN;
        int placed = 0;
        foreach (int i in tops) {
            Block b = f.blocks[i];
            float x = b.transform.localPosition.x;
            if (Mathf.Abs(x - room) < 1.2f || (!float.IsNaN(lastX) && Mathf.Abs(x - lastX) < 1.5f)) continue;
            Antenna(b.transform, new Vector3(0f, b.size.y * 0.5f, 0f), 1.25f + 0.35f * placed, placed);
            lastX = x;
            if (++placed >= 2) break;
        }
    }

    static float Top(Block b) { return b.transform.localPosition.y + b.size.y * 0.5f; }

    // Nothing rests on block i (the blocks still stand where the plan put them).
    static bool Free(List<Block> blocks, int i) {
        Block a = blocks[i];
        Vector3 pa = a.transform.localPosition;
        float top = Top(a);
        for (int k = 0; k < blocks.Count; k++) {
            Block b = blocks[k];
            if (k == i || b == null) continue;
            Vector3 pb = b.transform.localPosition;
            float bottom = pb.y - b.size.y * 0.5f;
            float overlap = Mathf.Min(pa.x + a.size.x * 0.5f, pb.x + b.size.x * 0.5f) - Mathf.Max(pa.x - a.size.x * 0.5f, pb.x - b.size.x * 0.5f);
            if (overlap > 0.05f && bottom > top - 0.1f && bottom < top + 0.3f) return false;
        }
        return true;
    }

    // A thin dark mast with two silver cross bars and a blinking green tip.
    static void Antenna(Transform host, Vector3 at, float height, int index) {
        Transform root = Root("Antenna", host, at);
        Part(root, WorldMeshes.Frustum(5, 0.035f, 0.015f, height), Vector3.zero, AlienKit.DarkMetal);
        for (int k = 0; k < 2; k++)
            Part(root, PrimitiveType.Cube, Vector3.up * (height * (0.5f + 0.22f * k)), new Vector3(0.32f - 0.1f * k, 0.025f, 0.025f), AlienKit.SilverMetal);
        AlienBlink.Add(Part(root, WorldMeshes.Ball(0.06f), Vector3.up * (height + 0.03f), TipMat), 1.3f, index * 0.5f, true);
    }

    // ------------------------------------------------------------------ helpers
    // A shallow bowl (a paraboloid opening upwards, 0.9 m across), with its own vertices on each side so both the
    // inside and the outside are drawn and lit.
    static Mesh DishMesh {
        get {
            if (dishMesh != null) return dishMesh;
            const int sides = 16, rings = 4;
            const float radius = 0.45f, depth = 0.16f;
            MeshBuilder b = new MeshBuilder();
            for (int side = 0; side < 2; side++) {
                bool inside = side == 0;
                int centre = b.Vertex(Vector3.zero, new Vector2(0.5f, 0.5f));
                int[,] ring = new int[rings, sides];
                for (int k = 0; k < rings; k++) {
                    float r = radius * (k + 1) / rings, y = depth * (r / radius) * (r / radius);
                    for (int s = 0; s < sides; s++) {
                        float a = s * Mathf.PI * 2f / sides;
                        Vector3 p = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                        ring[k, s] = b.Vertex(p, new Vector2(p.x + 0.5f, p.z + 0.5f));
                    }
                }
                for (int s = 0; s < sides; s++) {
                    int s1 = (s + 1) % sides;
                    // (centre, s, s + 1) faces down (the outside); reversed it faces up into the bowl
                    if (inside) b.Triangle(centre, ring[0, s1], ring[0, s]);
                    else b.Triangle(centre, ring[0, s], ring[0, s1]);
                    for (int k = 0; k + 1 < rings; k++) {
                        int p0 = ring[k, s], p1 = ring[k + 1, s], p2 = ring[k + 1, s1], p3 = ring[k, s1];
                        if (inside) { b.Triangle(p0, p2, p1); b.Triangle(p0, p3, p2); }
                        else { b.Triangle(p0, p1, p2); b.Triangle(p0, p2, p3); }
                    }
                }
            }
            dishMesh = b.ToMesh("Dish");
            return dishMesh;
        }
    }

    static void PointLight(Transform parent, Vector3 at, Color color, float intensity, float range) {
        GameObject go = ModelKit.Root("Decor Light", parent);
        go.transform.localPosition = at;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
    }

    static Transform Root(string name, Transform parent, Vector3 at) {
        Transform t = ModelKit.Root(name, parent).transform;
        t.localPosition = at;
        return t;
    }

    static GameObject Part(Transform parent, Mesh mesh, Vector3 at, Material m, Vector3 euler = default, Vector3? scale = null) {
        return ModelKit.MeshPart(parent, mesh, at, scale ?? Vector3.one, m, euler);
    }

    static GameObject Part(Transform parent, PrimitiveType type, Vector3 at, Vector3 scale, Material m, Vector3 euler = default) {
        return ModelKit.Part(parent, type, at, scale, m, euler);
    }
}

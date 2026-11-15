using System.Collections.Generic;
using UnityEngine;

// Dalí's fortress decorations, registered through ClassLooks (DaliLook). FortressDecor has already put up the team
// flag and banners; this adds, parented to blocks (so they topple and fall with them) and without colliders:
//   * a giant egg on the tower top and small eggs along the battlements (the eggs on his theatre-museum),
//   * soft watches draped over the walls where other classes hang their shields,
//   * a crutch propping up the front wall and a red lips sofa behind the back wall,
//   * gilded sconces with soft candles, lit at night; at night the eggs glow too (at most MaxLights lights).
public static class DaliDecor {
    const int MaxLights = 3, MaxSmallEggs = 5;
    const float BigEggRadius = 0.42f, BigEggHeight = 1.15f, SmallEggRadius = 0.2f, SmallEggHeight = 0.54f;
    const float WatchRadius = 0.34f;
    static readonly Color EggGlow = new Color(1f, 0.72f, 0.4f);
    static readonly Color CandleWax = new Color(0.96f, 0.93f, 0.84f), PadLeather = new Color(0.45f, 0.13f, 0.12f);
    static readonly Color CrutchWood = new Color(0.55f, 0.37f, 0.2f);
    static readonly Color FlameHot = new Color(1f, 0.85f, 0.45f), FlameCool = new Color(1f, 0.5f, 0.15f);

    // One decoration pass: which way the fortress faces, day or night, and the lights it has left.
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
        if (plan == null || f.blocks.Count != plan.blocks.Count) return;   // the anchors index the original blocks
        Job job = new Job { facing = f.Facing >= 0 ? 1 : -1, night = ArenaBuilder.IsNight };
        HashSet<int> crowned = new HashSet<int>();
        foreach (DecorAnchor a in plan.decor) {
            Block host = a.block >= 0 && a.block < f.blocks.Count ? f.blocks[a.block] : null;
            if (host == null) continue;
            switch (a.kind) {
                case DecorKind.TurretRoof:
                    Egg(job, host.transform, a.local, BigEggRadius, BigEggHeight, true);
                    crowned.Add(a.block);
                    break;
                case DecorKind.Shield: DrapedWatch(host); break;
                case DecorKind.Torch: Sconce(job, host.transform, a.local); break;
                case DecorKind.Base: Crutch(host, a.local, a.span, job.facing); break;
            }
        }
        SmallEggs(job, f.blocks, plan.height, crowned);
        Sofa(f.blocks, job.facing);
        DaliArt.Prewarm();
    }

    // ------------------------------------------------------------------ eggs
    // An egg standing on a block's top (a gilded ring holds the big one). The flag pole of the tower comes out of
    // the big egg's crown. At night the shells glow like lanterns.
    static void Egg(Job job, Transform host, Vector3 at, float radius, float height, bool big) {
        Transform root = Root("Egg", host, at);
        Material shell = job.night ? Mats.Emissive(DaliArt.Cream, EggGlow * (big ? 0.9f : 0.6f), 0.55f) : Mats.Lit(DaliArt.Cream, 0.55f);
        if (big) Part(root, WorldMeshes.Frustum(16, radius * 0.72f, radius * 0.88f, 0.08f), Vector3.zero, DaliArt.GoldMetal);
        Part(root, DaliArt.Egg(radius, height), new Vector3(0f, big ? 0.03f : 0f, 0f), shell);
        if (big && job.TakeLight()) PointLight(root, new Vector3(0f, height * 0.6f, -1f), EggGlow, 2.2f, 7f, false);
    }

    // Small eggs along the roof line: on the narrow blocks high up that nothing stands on and nothing overhangs
    // (the battlements and steps), evenly picked.
    static void SmallEggs(Job job, List<Block> blocks, float height, HashSet<int> skip) {
        List<Block> spots = new List<Block>();
        for (int i = 0; i < blocks.Count; i++) {
            Block b = blocks[i];
            if (b == null || skip.Contains(i) || b.size.x > 0.65f || b.size.y > 0.8f) continue;
            if (Top(b) < height * 0.45f || Overhung(blocks, b)) continue;
            spots.Add(b);
        }
        int n = Mathf.Min(MaxSmallEggs, spots.Count);
        for (int k = 0; k < n; k++) {
            Block b = spots[k * spots.Count / n];
            Egg(job, b.transform, new Vector3(0f, b.size.y * 0.5f, 0f), SmallEggRadius, SmallEggHeight, false);
        }
    }

    // True when another block lies anywhere above b's top within its width (decorations are placed before the
    // fortress wakes up, so the blocks still stand where the blueprint put them).
    static bool Overhung(List<Block> blocks, Block b) {
        float x0 = b.transform.localPosition.x - b.size.x * 0.5f, x1 = x0 + b.size.x, top = Top(b);
        foreach (Block o in blocks) {
            if (o == null || o == b) continue;
            float ox0 = o.transform.localPosition.x - o.size.x * 0.5f, ox1 = ox0 + o.size.x;
            float bottom = o.transform.localPosition.y - o.size.y * 0.5f;
            if (bottom > top - 0.05f && ox0 < x1 - 0.02f && ox1 > x0 + 0.02f) return true;
        }
        return false;
    }

    static float Top(Block b) { return b.transform.localPosition.y + b.size.y * 0.5f; }

    // ------------------------------------------------------------------ on the walls
    // A soft watch draped over the top front edge of a pillar, just under its lintel, melting down the wall.
    static void DrapedWatch(Block host) {
        Vector3 edge = new Vector3(0f, host.size.y * 0.5f, -host.size.z * 0.5f);
        GameObject watch = Part(host.transform, DaliArt.DrapedWatch(WatchRadius), edge, Mats.LitTex(DaliArt.WatchFace(true), Color.white, 0.5f));
        watch.name = "Soft Watch";
    }

    // A gilded sconce holding a soft candle, wax running down it; it burns at night.
    static void Sconce(Job job, Transform host, Vector3 at) {
        Transform root = Root("Sconce", host, at);
        Material wax = Mats.Lit(CandleWax, 0.35f);
        ModelKit.Part(root, PrimitiveType.Cube, new Vector3(0f, 0f, -0.1f), new Vector3(0.06f, 0.06f, 0.22f), DaliArt.GoldMetal);
        Part(root, WorldMeshes.Frustum(8, 0.06f, 0.12f, 0.05f), new Vector3(0f, -0.02f, -0.24f), DaliArt.GoldMetal);
        Part(root, WorldMeshes.Frustum(8, 0.045f, 0.04f, 0.32f), new Vector3(0f, 0.03f, -0.24f), wax);
        Part(root, WorldMeshes.Ball(0.03f), new Vector3(0.035f, 0.24f, -0.27f), wax, default, new Vector3(0.8f, 2.6f, 0.8f));
        if (!job.night) return;
        FlameEmitter.Create(root, new Vector3(0f, 0.4f, -0.24f), 0.2f, FlameHot, FlameCool);
        if (job.TakeLight()) PointLight(root, new Vector3(0f, 0.5f, -0.6f), FlameCool, 1.8f, 6f, true);
    }

    // ------------------------------------------------------------------ on the ground
    // A crutch propping up the front wall: its foot on the ground beyond the wall (past a plinth, `clear`), its padded
    // top pressed against the pillar's outer face, below the first lintel.
    static void Crutch(Block pillar, Vector3 at, float clear, int facing) {
        float face = pillar.size.x * 0.5f;
        float rest = Mathf.Clamp(pillar.size.y * 0.82f, 1.2f, 2f);
        Vector3 foot = at + new Vector3(facing * (face + clear + 0.55f), 0f, -0.45f);
        Vector3 top = at + new Vector3(facing * (face + 0.05f), rest, -0.45f);
        Vector3 d = top - foot;
        Transform root = Root("Crutch", pillar.transform, foot);
        root.localRotation = Quaternion.FromToRotation(Vector3.up, d);
        root.localScale = Vector3.one * (d.magnitude / DaliArt.CrutchLength);
        Part(root, DaliArt.CrutchWood, Vector3.zero, Mats.Lit(CrutchWood, 0.3f));
        Part(root, WorldMeshes.Ball(0.5f), new Vector3(0f, 2.05f, 0f), Mats.Lit(PadLeather, 0.4f), default, new Vector3(0.34f, 0.1f, 0.12f));
        Part(root, WorldMeshes.Frustum(6, 0.05f, 0.045f, 0.06f), Vector3.zero, Mats.Lit(DaliArt.Ink, 0.3f));   // rubber foot
    }

    // The red lips sofa on the ground behind the back wall (the side away from the enemy).
    static void Sofa(List<Block> blocks, int facing) {
        Block back = null;
        float backU = float.MaxValue;
        foreach (Block b in blocks) {
            if (b == null) continue;
            Vector3 p = b.transform.localPosition;
            if (p.y - b.size.y * 0.5f > 0.1f) continue;                // standing on the ground
            float u = p.x * facing - b.size.x * 0.5f;                    // its back face, in metres towards the enemy
            if (u < backU) { backU = u; back = b; }
        }
        if (back == null) return;
        Vector3 at = new Vector3(-facing * (back.size.x * 0.5f + 1.05f), -back.size.y * 0.5f, -0.25f);
        Part(back.transform, DaliArt.Lips(), at, Mats.Lit(DaliArt.LipsRed, 0.6f), new Vector3(0f, facing * 10f, 0f)).name = "Lips Sofa";
    }

    // ------------------------------------------------------------------ helpers
    static void PointLight(Transform parent, Vector3 at, Color color, float intensity, float range, bool flicker) {
        GameObject go = ModelKit.Root("Decor Light", parent);
        go.transform.localPosition = at;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        if (flicker) go.AddComponent<FlameFlicker>();
    }

    static Transform Root(string name, Transform parent, Vector3 at) {
        Transform t = ModelKit.Root(name, parent).transform;
        t.localPosition = at;
        return t;
    }

    static GameObject Part(Transform parent, Mesh mesh, Vector3 at, Material m, Vector3 euler = default, Vector3 scale = default) {
        return ModelKit.MeshPart(parent, mesh, at, scale == default ? Vector3.one : scale, m, euler);
    }
}

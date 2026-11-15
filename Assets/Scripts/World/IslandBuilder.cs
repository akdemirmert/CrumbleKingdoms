using System.Collections.Generic;
using UnityEngine;

// A point on the outline of an island (its top edge or its waterline): position, outward direction and
// distance along the outline (for texture coordinates).
public struct EdgePoint {
    public Vector3 position, outward;
    public float distance;
}

// Islands: one BoxCollider each (gameplay needs a top that is exactly flat at topY), dressed with
// collider-free terrain: faceted rock faces with strata, a turf lip hanging over the edges, a textured top,
// boulders at the waterline and on the faces, rubble at terrace steps and themed props.
public static class IslandBuilder {
    public const float Front = -5f, Back = 9f, Bottom = -30f;
    const float LipOut = 0.08f;   // the turf lip stands this far proud of the rock

    // ------------------------------------------------------------------ gameplay
    public static GameObject Collider(Transform root, IslandSpec i) {
        GameObject go = new GameObject("Island " + i.xMin.ToString("0") + " to " + i.xMax.ToString("0")) { layer = Layers.Ground };
        go.transform.SetParent(root, false);
        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.center = new Vector3((i.xMin + i.xMax) * 0.5f, (i.topY + Bottom) * 0.5f, (Front + Back) * 0.5f);
        bc.size = new Vector3(i.xMax - i.xMin, i.topY - Bottom, Back - Front);
        bc.sharedMaterial = PhysMats.Ground;
        return go;
    }

    // ------------------------------------------------------------------ scenery
    // Builds the visible terrain as children of the island object (which sits at the world origin).
    public static void Dress(GameObject island, ArenaPalette p, List<IslandSpec> all, int index, List<float> fortressXs) {
        IslandSpec isl = all[index];
        Transform t = island.transform;
        System.Random rng = new System.Random(Mathf.RoundToInt(isl.xMin * 17f + isl.topY * 101f) + (int)p.theme * 7);
        Material rock = WorldTextures.Tiled(WorldTextures.Cliff(p), WorldTextures.CliffTile, 0.12f);
        float top = isl.topY;
        float leftBase = SideBase(all, index, true), rightBase = SideBase(all, index, false);
        bool leftOpen = !float.IsNaN(leftBase), rightOpen = !float.IsNaN(rightBase);
        int seed = rng.Next(1000);

        // front face, then the exposed parts of the sides (the whole height at sea, the step above a lower terrace)
        int na = Mathf.Max(2, Mathf.CeilToInt((isl.xMax - isl.xMin) / 1.6f)), nb = Mathf.Max(2, Mathf.CeilToInt((top - Bottom) / 1.6f));
        Face(t, rock, "Cliff front", na, nb, Vector3.back, 0.6f, seed,
             (u, v) => new Vector3(Mathf.Lerp(isl.xMin, isl.xMax, u), Mathf.Lerp(Bottom, top - 0.03f, v), Front));
        if (leftOpen) {
            int sb = Mathf.Max(2, Mathf.CeilToInt((top - leftBase) / 1.6f));
            Face(t, rock, "Cliff left", 9, sb, Vector3.left, leftBase > Bottom ? 0.25f : 0.35f, seed + 1,
                 (u, v) => new Vector3(isl.xMin, Mathf.Lerp(leftBase, top - 0.03f, v), Mathf.Lerp(Back, Front, u)));
        }
        if (rightOpen) {
            int sb = Mathf.Max(2, Mathf.CeilToInt((top - rightBase) / 1.6f));
            Face(t, rock, "Cliff right", 9, sb, Vector3.right, rightBase > Bottom ? 0.25f : 0.35f, seed + 2,
                 (u, v) => new Vector3(isl.xMax, Mathf.Lerp(rightBase, top - 0.03f, v), Mathf.Lerp(Front, Back, u)));
        }

        TopCap(t, p, isl, leftOpen, rightOpen);
        Lip(t, p, isl, leftOpen ? top - leftBase : 0f, rightOpen ? top - rightBase : 0f, seed);
        Boulders(t, rock, all, index, leftOpen ? leftBase : float.NaN, rightOpen ? rightBase : float.NaN, rng);
        PropKit.Scatter(t, p, isl, fortressXs, rng);
    }

    // A faceted rock face over a grid. corner(u, v) is the flat point (u across to the viewer's right, v up,
    // both 0..1); the face then bulges outwards with noise, staying flat along its edges and under the lip
    // so it meets neighbouring faces and islands without cracks.
    static void Face(Transform parent, Material m, string name, int na, int nb, Vector3 outward, float strength, int seed,
                     System.Func<float, float, Vector3> corner) {
        Vector3[,] p = new Vector3[na + 1, nb + 1];
        Vector3 across = (corner(1f, 0f) - corner(0f, 0f)) / na, up = (corner(0f, 1f) - corner(0f, 0f)) / nb;
        for (int a = 0; a <= na; a++)
            for (int b = 0; b <= nb; b++) {
                Vector3 q = corner(a / (float)na, b / (float)nb);
                float w = Mathf.Clamp01(Mathf.Min(a, na - a) / 1.5f) * Mathf.Clamp01((nb - b) / 1.2f) * Mathf.Clamp01(b / 1f);
                float n = Mathf.PerlinNoise(q.x * 0.21f + q.z * 0.21f + seed, q.y * 0.27f) * 0.7f
                          + Mathf.PerlinNoise(q.x * 0.63f + q.z * 0.63f, q.y * 0.6f + seed) * 0.3f;
                bool interior = a > 0 && a < na && b > 0 && b < nb;
                if (interior) {   // break the grid up a little
                    q += across * (Mathf.PerlinNoise(a * 1.7f + seed, b * 1.3f) - 0.5f) * 0.6f;
                    q += up * (Mathf.PerlinNoise(b * 1.9f, a * 1.1f + seed) - 0.5f) * 0.6f;
                }
                p[a, b] = q + outward * (strength * n * w);
            }
        MeshBuilder mb = new MeshBuilder();
        System.Func<Vector3, Vector2> uv = v => new Vector2(v.x + v.z, v.y);   // metres, like MeshKit.Rock
        for (int a = 0; a < na; a++)
            for (int b = 0; b < nb; b++)
                mb.FacetQuad(p[a, b], p[a, b + 1], p[a + 1, b + 1], p[a + 1, b], uv);
        ModelKit.MeshPart(parent, mb.ToMesh(name), Vector3.zero, Vector3.one, m, default, name);
    }

    // The walkable top, flush with the collider at topY (UVs in metres on x/z).
    static void TopCap(Transform parent, ArenaPalette p, IslandSpec i, bool leftOpen, bool rightOpen) {
        float x0 = i.xMin - (leftOpen ? LipOut : 0f), x1 = i.xMax + (rightOpen ? LipOut : 0f), z0 = Front - LipOut, y = i.topY;
        MeshBuilder b = new MeshBuilder();
        b.Quad(b.Vertex(new Vector3(x0, y, z0), new Vector2(x0, z0)), b.Vertex(new Vector3(x0, y, Back), new Vector2(x0, Back)),
               b.Vertex(new Vector3(x1, y, Back), new Vector2(x1, Back)), b.Vertex(new Vector3(x1, y, z0), new Vector2(x1, z0)));
        Material m = WorldTextures.Tiled(WorldTextures.Top(p), WorldTextures.TopTile, p.theme == ArenaTheme.Asgard ? 0.45f : 0.1f);
        ModelKit.MeshPart(parent, b.ToMesh("Top"), Vector3.zero, Vector3.one, m, default, "Top");
    }

    // The turf (sand, snow, ash, marble) layer hanging over the top edges with a ragged lower edge.
    // leftDrop/rightDrop: exposed height of each side (0 = the side is hidden by a higher neighbour).
    static void Lip(Transform parent, ArenaPalette p, IslandSpec i, float leftDrop, float rightDrop, int seed) {
        List<EdgePoint> path = EdgePath(i, leftDrop > 0f, rightDrop > 0f, i.topY + 0.01f, LipOut, 0.5f);
        MeshBuilder b = new MeshBuilder();
        int prevTop = -1, prevBottom = -1;
        foreach (EdgePoint e in path) {
            float s = e.distance;
            float depth = 0.45f + 0.35f * Mathf.PerlinNoise(s * 0.45f + seed, 0.5f);
            if (Mathf.PerlinNoise(s * 2.1f, seed + 0.5f) > 0.72f) depth += 0.3f;              // the odd longer drip
            float limit = e.outward.x < -0.1f ? leftDrop : e.outward.x > 0.1f ? rightDrop : 99f;
            depth = Mathf.Min(depth, limit * 0.8f);
            // v is pre-multiplied by the tile size: the material's tiling then maps the lip to exactly 0..1
            int topV = b.Vertex(e.position, new Vector2(s, WorldTextures.LipTile));
            int bottomV = b.Vertex(e.position + Vector3.down * depth, new Vector2(s, 0f));
            if (prevTop >= 0) b.Quad(prevBottom, prevTop, topV, bottomV);
            prevTop = topV;
            prevBottom = bottomV;
        }
        Material m = WorldTextures.Tiled(WorldTextures.Lip(p), WorldTextures.LipTile, 0.1f);
        ModelKit.MeshPart(parent, b.ToMesh("Lip"), Vector3.zero, Vector3.one, m, default, "Lip");
    }

    // Boulders half-sunk at the waterline, chunks jutting from the cliff, rubble at the foot of steps.
    static void Boulders(Transform parent, Material m, List<IslandSpec> all, int index, float leftBase, float rightBase, System.Random rng) {
        IslandSpec i = all[index];
        float width = i.xMax - i.xMin;
        int n = Mathf.Max(3, Mathf.RoundToInt(width / 3.5f));
        for (int k = 0; k < n; k++) {
            float x = Mathf.Lerp(i.xMin + 0.8f, i.xMax - 0.8f, (k + Rand(rng, 0.1f, 0.9f)) / n);
            Rock(parent, m, new Vector3(x, World.WaterY + Rand(rng, -0.9f, 0.2f), Front - Rand(rng, 0.1f, 0.9f)), Rand(rng, 0.8f, 2f), rng);
        }
        foreach (bool left in new[] { true, false }) {
            float baseY = left ? leftBase : rightBase;
            if (float.IsNaN(baseY)) continue;
            float x = left ? i.xMin : i.xMax, side = left ? -1f : 1f;
            if (baseY <= Bottom) {   // open sea on this side
                for (int k = 0; k < 2; k++)
                    Rock(parent, m, new Vector3(x + side * Rand(rng, 0.1f, 0.8f), World.WaterY + Rand(rng, -0.8f, 0.1f), Rand(rng, Front + 1f, Back - 1f)),
                         Rand(rng, 0.8f, 1.8f), rng);
            } else {                 // a terrace step: rubble on the lower island, clear of the gameplay plane
                for (int k = 0; k < 3; k++) {
                    float z = k == 1 ? Rand(rng, Front + 0.6f, -1.8f) : Rand(rng, 1.8f, Back - 1f);
                    Rock(parent, m, new Vector3(x + side * Rand(rng, 0.3f, 1.3f), baseY, z), Rand(rng, 0.35f, 0.7f), rng);
                }
            }
        }
        float faceHeight = i.topY - World.WaterY;
        if (faceHeight > 3.5f) {
            int chunks = Mathf.Clamp(Mathf.RoundToInt(width * faceHeight / 45f), 1, 6);
            for (int k = 0; k < chunks; k++)
                Rock(parent, m, new Vector3(Rand(rng, i.xMin + 1.5f, i.xMax - 1.5f), Rand(rng, World.WaterY + 1.2f, i.topY - 1.8f), Front + 0.25f),
                     Rand(rng, 0.7f, 1.4f), rng);
        }
    }

    public static GameObject Rock(Transform parent, Material m, Vector3 pos, float radius, System.Random rng) {
        Mesh mesh = MeshKit.Rock(1f, 1 + rng.Next(6), 0.3f, 1);   // six shared shapes, scaled and turned
        Vector3 scale = new Vector3(Rand(rng, 1f, 1.5f), Rand(rng, 0.6f, 0.95f), Rand(rng, 0.8f, 1.2f)) * radius;
        return ModelKit.MeshPart(parent, mesh, pos, scale, m, new Vector3(Rand(rng, -15f, 15f), Rand(rng, 0f, 360f), Rand(rng, -15f, 15f)), "Rock");
    }

    public static float Rand(System.Random rng, float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

    // ------------------------------------------------------------------ outline helpers
    // Lowest exposed height of a side face: the sea floor, or the top of a lower neighbour; NaN when a
    // neighbour at least as high covers the whole side.
    static float SideBase(List<IslandSpec> all, int index, bool left) {
        IslandSpec me = all[index];
        float baseY = Bottom;
        for (int j = 0; j < all.Count; j++) {
            if (j == index || !Covers(all[j], me, left)) continue;
            if (all[j].topY >= me.topY - 0.01f) return float.NaN;
            baseY = Mathf.Max(baseY, all[j].topY);
        }
        return baseY;
    }

    // True when no neighbour covers this side of the island at height y (the rock meets air or sea there).
    public static bool SideOpen(List<IslandSpec> all, int index, bool left, float y) {
        for (int j = 0; j < all.Count; j++)
            if (j != index && Covers(all[j], all[index], left) && all[j].topY >= y - 0.01f) return false;
        return true;
    }

    // Does island o touch or overlap `me` on that side?
    static bool Covers(IslandSpec o, IslandSpec me, bool left) {
        return left ? o.xMin < me.xMin - 0.01f && o.xMax > me.xMin - 0.05f
                    : o.xMax > me.xMax + 0.01f && o.xMin < me.xMax + 0.05f;
    }

    // Outline of the island's open sides and front at height y, pushed `outset` out from the rock and sampled
    // every `step` metres: left side back to front, front left to right, right side front to back. That runs
    // counter-clockwise seen from above, so strips built from it face outwards with quads
    // (bottom_k, top_k, top_k+1, bottom_k+1) and upwards with quads (outer_k, inner_k, inner_k+1, outer_k+1).
    public static List<EdgePoint> EdgePath(IslandSpec i, bool left, bool right, float y, float outset, float step) {
        List<EdgePoint> pts = new List<EdgePoint>();
        float x0 = i.xMin - (left ? outset : 0f), x1 = i.xMax + (right ? outset : 0f), zf = Front - outset;
        if (left) Segment(pts, new Vector3(x0, y, Back), new Vector3(x0, y, zf), Vector3.left, step);
        Segment(pts, new Vector3(x0, y, zf), new Vector3(x1, y, zf), Vector3.back, step);
        if (right) Segment(pts, new Vector3(x1, y, zf), new Vector3(x1, y, Back), Vector3.right, step);
        return pts;
    }

    static void Segment(List<EdgePoint> pts, Vector3 a, Vector3 b, Vector3 outward, float step) {
        float length = Vector3.Distance(a, b);
        int n = Mathf.Max(1, Mathf.CeilToInt(length / step));
        int first = 0;
        float start = 0f;
        if (pts.Count > 0) {   // the corner is shared with the previous segment: point it diagonally outwards
            EdgePoint corner = pts[pts.Count - 1];
            corner.outward = (corner.outward + outward).normalized;
            pts[pts.Count - 1] = corner;
            start = corner.distance;
            first = 1;
        }
        for (int k = first; k <= n; k++)
            pts.Add(new EdgePoint { position = Vector3.Lerp(a, b, k / (float)n), outward = outward, distance = start + length * k / n });
    }
}

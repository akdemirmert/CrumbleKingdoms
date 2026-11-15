using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Themed props on the island tops: leafy trees, pines, bushes and flowers (Meadow), saguaros, barrel cacti and
// dry shrubs (Desert), snowy pines, snowy rocks and ice crystals (Frost), charred trees and glowing ember rocks
// (Volcano), runestones, golden trees and marble columns (Asgard).
// Visual only: no colliders, never inside a fortress footprint, tall props only behind the gameplay plane.
// All props of an island are merged into one mesh per material, so an island costs a handful of renderers.
public static class PropKit {
    const float FortressClearance = 7f;   // half the widest fortress (~11 m) plus room for a canopy
    const float EdgeMargin = 1.5f;        // keep clear of the cliff edges and terrace steps

    static readonly Color Bark = new Color(0.36f, 0.25f, 0.16f), Leaf = new Color(0.23f, 0.46f, 0.17f), LeafLight = new Color(0.37f, 0.61f, 0.22f);
    static readonly Color Needles = new Color(0.13f, 0.32f, 0.21f), Snow = new Color(0.94f, 0.97f, 1f), Ice = new Color(0.62f, 0.84f, 1f);
    static readonly Color Cactus = new Color(0.33f, 0.53f, 0.27f), DryWood = new Color(0.55f, 0.42f, 0.27f), Blossom = new Color(1f, 0.5f, 0.62f);
    static readonly Color Charcoal = new Color(0.12f, 0.1f, 0.1f), Obsidian = new Color(0.08f, 0.07f, 0.1f);
    static readonly Color RuneStone = new Color(0.6f, 0.58f, 0.68f), Marble = new Color(0.94f, 0.92f, 0.89f);
    static readonly Color Gold = new Color(1f, 0.8f, 0.34f), Amber = new Color(0.98f, 0.62f, 0.26f);
    static readonly Color[] Flowers = { new Color(1f, 0.92f, 0.4f), new Color(1f, 1f, 1f), new Color(1f, 0.62f, 0.78f) };

    // ------------------------------------------------------------------ placement
    public static void Scatter(Transform parent, ArenaPalette p, IslandSpec isl, List<float> fortressXs, System.Random rng) {
        Batch batch = new Batch();
        for (float x = isl.xMin + EdgeMargin + R(rng, 0f, 1.2f); x < isl.xMax - EdgeMargin; x += R(rng, 1.8f, 3.4f)) {
            if (NearFortress(x, fortressXs)) continue;
            bool tall = rng.NextDouble() < 0.45;
            // tall props stand behind the gameplay plane; small ones also dot the front edge
            float z = tall || rng.NextDouble() < 0.5 ? R(rng, 2.4f, 7.6f) : R(rng, -4.3f, -2.4f);
            Vector3 pos = new Vector3(x, isl.topY, z);
            float size = R(rng, 0.85f, 1.2f);
            if (tall) Tall(batch, p, pos, size, rng);
            else Small(batch, p, Place(pos, R(rng, 0f, 360f), size), rng);
        }
        batch.Build(parent);
    }

    static bool NearFortress(float x, List<float> fortressXs) {
        foreach (float fx in fortressXs)
            if (Mathf.Abs(x - fx) < FortressClearance) return true;
        return false;
    }

    static Matrix4x4 Place(Vector3 pos, float yaw, float size) {
        return Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one * size);
    }

    // ------------------------------------------------------------------ tall props
    static void Tall(Batch b, ArenaPalette p, Vector3 pos, float size, System.Random rng) {
        Matrix4x4 at = Place(pos, R(rng, 0f, 360f), size);
        switch (p.theme) {
            case ArenaTheme.Desert: Saguaro(b, at, rng); break;
            case ArenaTheme.Frost: Pine(b, at, rng, true); break;
            case ArenaTheme.Volcano: DeadTree(b, at, rng); break;
            case ArenaTheme.Asgard:
                double pick = rng.NextDouble();
                if (pick < 0.4) Runestone(b, Place(pos, R(rng, -20f, 20f), size), rng);   // the runes face the camera
                else if (pick < 0.75) LeafyTree(b, at, rng, Marble, Gold, Amber);
                else Column(b, at, rng);
                break;
            default:
                if (rng.NextDouble() < 0.65) LeafyTree(b, at, rng, Bark, Leaf, LeafLight);
                else Pine(b, at, rng, false);
                break;
        }
    }

    // A trunk under a cluster of low-poly leaf balls.
    static void LeafyTree(Batch b, Matrix4x4 at, System.Random rng, Color bark, Color leaf, Color light) {
        float h = R(rng, 2f, 2.8f);
        b.Add(Trunk, Lit(bark), at, Vector3.zero, new Vector3(0.2f, h, 0.2f));
        b.Add(Ball, Lit(leaf), at, new Vector3(0f, h + 0.5f, 0f), new Vector3(1.5f, 1.25f, 1.5f), Spin(rng));
        b.Add(Ball, Lit(light), at, new Vector3(0.75f, h + 0.15f, 0.25f), Vector3.one * R(rng, 0.85f, 1.05f), Spin(rng));
        b.Add(Ball, Lit(leaf), at, new Vector3(-0.7f, h + 0.05f, -0.2f), Vector3.one * R(rng, 0.8f, 1f), Spin(rng));
        b.Add(Ball, Lit(light), at, new Vector3(0.1f, h + 1.25f, -0.15f), Vector3.one * R(rng, 0.7f, 0.9f), Spin(rng));
    }

    // Three tiers of needles; snowy pines get a white cone on each tier.
    static void Pine(Batch b, Matrix4x4 at, System.Random rng, bool snowy) {
        b.Add(Trunk, Lit(Bark), at, Vector3.zero, new Vector3(0.16f, 0.9f, 0.16f));
        float y = 0.6f, r = R(rng, 1.15f, 1.4f), h = 1.5f;
        for (int k = 0; k < 3; k++) {
            Vector3 turn = new Vector3(0f, R(rng, 0f, 60f), 0f);
            b.Add(Cone, Lit(Needles), at, new Vector3(0f, y, 0f), new Vector3(r, h, r), turn);
            if (snowy) b.Add(Cone, Lit(Snow), at, new Vector3(0f, y + h * 0.45f, 0f), new Vector3(r * 0.66f, h * 0.58f, r * 0.66f), turn);
            y += h * 0.55f;
            r *= 0.76f;
            h *= 0.86f;
        }
    }

    // A saguaro: a ribbed column with one or two bent arms and the odd pink blossom.
    static void Saguaro(Batch b, Matrix4x4 at, System.Random rng) {
        Material green = Lit(Cactus);
        float h = R(rng, 2.3f, 3.2f);
        b.Add(Column8, green, at, Vector3.zero, new Vector3(0.3f, h, 0.3f));
        b.Add(Dome, green, at, new Vector3(0f, h, 0f), new Vector3(0.28f, 0.3f, 0.28f));
        int arms = 1 + rng.Next(2);
        for (int i = 0; i < arms; i++) {
            float side = i == 0 ? 1f : -1f, y = R(rng, 0.9f, h * 0.6f), up = R(rng, 0.6f, 1.1f);
            b.Add(Column8, green, at, new Vector3(0f, y, 0f), new Vector3(0.17f, 0.5f, 0.17f), new Vector3(0f, 0f, -90f * side));
            b.Add(Column8, green, at, new Vector3(0.48f * side, y - 0.12f, 0f), new Vector3(0.17f, up, 0.17f));
            b.Add(Dome, green, at, new Vector3(0.48f * side, y - 0.12f + up, 0f), new Vector3(0.16f, 0.18f, 0.16f));
        }
        if (rng.NextDouble() < 0.5) b.Add(Ball, Lit(Blossom), at, new Vector3(0f, h + 0.28f, 0f), Vector3.one * 0.08f);
    }

    // A charred, leafless tree with crooked branches.
    static void DeadTree(Batch b, Matrix4x4 at, System.Random rng) {
        Material wood = Lit(Charcoal);
        float h = R(rng, 2.2f, 3f);
        b.Add(Trunk, wood, at, Vector3.zero, new Vector3(0.22f, h, 0.22f), new Vector3(0f, 0f, R(rng, -6f, 6f)));
        int n = 3 + rng.Next(2);
        for (int i = 0; i < n; i++) {
            float y = h * R(rng, 0.45f, 0.9f), length = R(rng, 0.8f, 1.3f);
            b.Add(Branch, wood, at, new Vector3(0f, y, 0f), new Vector3(0.07f, length, 0.07f),
                  new Vector3(0f, i * 360f / n + R(rng, -25f, 25f), R(rng, 35f, 62f)));   // lean out, then turn round the trunk
        }
    }

    // A standing stone with glowing golden runes carved into its face.
    static void Runestone(Batch b, Matrix4x4 at, System.Random rng) {
        float h = R(rng, 1.7f, 2.3f);
        Matrix4x4 slab = at * Matrix4x4.TRS(new Vector3(0f, h * 0.5f - 0.08f, 0f), Quaternion.Euler(R(rng, -4f, 4f), 0f, R(rng, -5f, 5f)), Vector3.one);
        b.Add(Cube, Lit(RuneStone), slab, Vector3.zero, new Vector3(0.85f, h, 0.32f));
        Material glow = Mats.Emissive(Gold * 0.6f, new Color(2.6f, 1.7f, 0.5f), 0.4f);
        for (int j = 0; j < 3; j++) {   // three glyphs: a stem with a slanted twig
            float y = h * (0.26f - 0.24f * j), x = R(rng, -0.12f, 0.12f);
            b.Add(Cube, glow, slab, new Vector3(x, y, -0.165f), new Vector3(0.05f, 0.34f, 0.03f));
            b.Add(Cube, glow, slab, new Vector3(x + 0.07f, y + R(rng, -0.08f, 0.08f), -0.165f), new Vector3(0.045f, 0.2f, 0.03f),
                  new Vector3(0f, 0f, rng.NextDouble() < 0.5 ? 40f : -40f));
        }
    }

    // A marble column (whole or broken off) on a plinth, with a gold band.
    static void Column(Batch b, Matrix4x4 at, System.Random rng) {
        float h = R(rng, 1.4f, 2.8f);
        b.Add(Cube, Lit(Marble), at, new Vector3(0f, 0.12f, 0f), new Vector3(0.9f, 0.24f, 0.9f));
        b.Add(Column8, Lit(Marble), at, new Vector3(0f, 0.24f, 0f), new Vector3(0.32f, h, 0.32f));
        b.Add(Column8, Mats.Lit(Gold, 0.6f, 0.8f), at, new Vector3(0f, 0.24f + h * 0.82f, 0f), new Vector3(0.36f, 0.1f, 0.36f));
    }

    // ------------------------------------------------------------------ small props
    static void Small(Batch b, ArenaPalette p, Matrix4x4 at, System.Random rng) {
        double pick = rng.NextDouble();
        if (pick < 0.35) {
            Boulder(b, at, rng, p.theme == ArenaTheme.Volcano ? Obsidian : p.rockLight);
            return;
        }
        switch (p.theme) {
            case ArenaTheme.Desert:
                if (pick < 0.7) BarrelCactus(b, at); else DryBush(b, at, rng);
                break;
            case ArenaTheme.Frost:
                if (pick < 0.7) SnowyRock(b, at, rng, p.rock); else IceCrystals(b, at, rng);
                break;
            case ArenaTheme.Volcano: EmberRock(b, at, rng); break;
            case ArenaTheme.Asgard: Blossoms(b, at, rng, Gold, 0.09f); break;
            default:
                if (pick < 0.65) Bush(b, at, rng); else Tuft(b, at, rng);
                break;
        }
    }

    // Half-sunk rock (the unit rock is centred on the ground).
    static void Boulder(Batch b, Matrix4x4 at, System.Random rng, Color c) {
        b.Add(RockMesh(rng), Mats.Lit(c, c == Obsidian ? 0.7f : 0.12f), at, Vector3.zero, new Vector3(1.2f, 0.75f, 1f) * R(rng, 0.35f, 0.7f), Spin(rng));
    }

    static void Bush(Batch b, Matrix4x4 at, System.Random rng) {
        int n = 2 + rng.Next(2);
        for (int i = 0; i < n; i++)
            b.Add(Ball, Lit(i == 1 ? LeafLight : Leaf), at, new Vector3(R(rng, -0.35f, 0.35f), 0.28f, R(rng, -0.25f, 0.25f)),
                  Vector3.one * R(rng, 0.42f, 0.62f), Spin(rng));
    }

    // Blades of grass with a few flowers.
    static void Tuft(Batch b, Matrix4x4 at, System.Random rng) {
        for (int i = 0; i < 5; i++)
            b.Add(Blade, Lit(LeafLight), at, new Vector3(R(rng, -0.2f, 0.2f), 0f, R(rng, -0.2f, 0.2f)), new Vector3(0.06f, R(rng, 0.35f, 0.55f), 0.06f),
                  new Vector3(R(rng, -22f, 22f), R(rng, 0f, 360f), R(rng, -22f, 22f)));
        Blossoms(b, at, rng, Flowers[rng.Next(Flowers.Length)], 0.07f);
    }

    static void Blossoms(Batch b, Matrix4x4 at, System.Random rng, Color c, float radius) {
        int n = 2 + rng.Next(3);
        for (int i = 0; i < n; i++)
            b.Add(Ball, Lit(c), at, new Vector3(R(rng, -0.45f, 0.45f), R(rng, 0.05f, 0.3f), R(rng, -0.35f, 0.35f)), Vector3.one * radius);
    }

    static void BarrelCactus(Batch b, Matrix4x4 at) {
        b.Add(Ball, Lit(Cactus), at, new Vector3(0f, 0.25f, 0f), new Vector3(0.35f, 0.42f, 0.35f));
        b.Add(Ball, Lit(Blossom), at, new Vector3(0f, 0.66f, 0f), Vector3.one * 0.07f);
    }

    // Bare twigs fanning out of the sand.
    static void DryBush(Batch b, Matrix4x4 at, System.Random rng) {
        for (int i = 0; i < 6; i++)
            b.Add(Branch, Lit(DryWood), at, Vector3.zero, new Vector3(0.035f, R(rng, 0.45f, 0.75f), 0.035f),
                  new Vector3(0f, i * 60f + R(rng, -20f, 20f), R(rng, 25f, 55f)));
    }

    static void SnowyRock(Batch b, Matrix4x4 at, System.Random rng, Color rock) {
        float k = R(rng, 0.4f, 0.75f);
        b.Add(RockMesh(rng), Lit(rock), at, Vector3.zero, new Vector3(1.2f, 0.75f, 1f) * k, new Vector3(0f, R(rng, 0f, 360f), 0f));
        b.Add(Dome, Lit(Snow), at, new Vector3(0f, 0.35f * k, 0f), new Vector3(0.95f * k, 0.45f * k, 0.8f * k));
    }

    static void IceCrystals(Batch b, Matrix4x4 at, System.Random rng) {
        Material ice = Mats.Lit(Ice, 0.85f, 0.1f);
        for (int i = 0; i < 3; i++) {
            float r = R(rng, 0.14f, 0.22f);
            b.Add(Crystal, ice, at, new Vector3(R(rng, -0.25f, 0.25f), -0.05f, R(rng, -0.2f, 0.2f)), new Vector3(r, R(rng, 0.7f, 1.3f), r),
                  new Vector3(R(rng, -25f, 25f), R(rng, 0f, 360f), R(rng, -25f, 25f)));
        }
    }

    // An obsidian rock with embers glowing in its cracks.
    static void EmberRock(Batch b, Matrix4x4 at, System.Random rng) {
        float k = R(rng, 0.4f, 0.7f);
        b.Add(RockMesh(rng), Mats.Lit(Obsidian, 0.7f), at, Vector3.zero, new Vector3(1.2f, 0.75f, 1f) * k, Spin(rng));
        Material ember = Mats.Emissive(new Color(0.4f, 0.1f, 0.03f), new Color(3f, 0.9f, 0.18f), 0.3f);
        int n = 1 + rng.Next(2);
        for (int i = 0; i < n; i++)
            b.Add(Ball, ember, at, new Vector3(R(rng, -0.4f, 0.4f) * k, 0.45f * k, -0.55f * k), Vector3.one * R(rng, 0.12f, 0.2f));
    }

    // ------------------------------------------------------------------ helpers
    static Mesh Trunk => WorldMeshes.Frustum(6, 1f, 0.6f, 1f);
    static Mesh Branch => WorldMeshes.Frustum(4, 1f, 0.3f, 1f);
    static Mesh Column8 => WorldMeshes.Frustum(8, 1f, 0.92f, 1f);
    static Mesh Ball => WorldMeshes.Ball(1f);
    static Mesh Dome => WorldMeshes.Dome(8, 2, 1f);
    static Mesh Cone => MeshKit.Cone(7, 1f, 1f);
    static Mesh Blade => MeshKit.Cone(3, 1f, 1f);
    static Mesh Crystal => MeshKit.Cone(5, 1f, 1f);
    static Mesh Cube => MeshKit.Box(Vector3.one);
    static Mesh RockMesh(System.Random rng) { return MeshKit.Rock(1f, 1 + rng.Next(6), 0.3f, 1); }   // six shared shapes

    static Material Lit(Color c) { return Mats.Lit(c, 0.15f); }

    static Vector3 Spin(System.Random rng) { return new Vector3(R(rng, -20f, 20f), R(rng, 0f, 360f), R(rng, -20f, 20f)); }

    static float R(System.Random rng, float a, float b) { return IslandBuilder.Rand(rng, a, b); }

    // Prop parts collected per material, then merged into one mesh per material.
    class Batch {
        readonly Dictionary<Material, List<CombineInstance>> parts = new Dictionary<Material, List<CombineInstance>>();

        // A mesh placed at pos / euler / scale in the prop's own space (at).
        public void Add(Mesh mesh, Material m, Matrix4x4 at, Vector3 pos, Vector3 scale, Vector3 euler = default) {
            if (!parts.TryGetValue(m, out List<CombineInstance> list)) parts[m] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = mesh, transform = at * Matrix4x4.TRS(pos, Quaternion.Euler(euler), scale) });
        }

        public void Build(Transform parent) {
            foreach (KeyValuePair<Material, List<CombineInstance>> kv in parts) {
                int verts = 0;
                foreach (CombineInstance c in kv.Value) verts += c.mesh.vertexCount;
                Mesh mesh = new Mesh { name = "Props" };
                if (verts > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                ModelKit.MeshPart(parent, mesh, Vector3.zero, Vector3.one, kv.Key, default, "Props");
            }
        }
    }
}

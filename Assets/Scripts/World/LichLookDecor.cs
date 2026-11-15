using System.Collections.Generic;
using UnityEngine;

// The Lich's fortress decorations (visual only, parented to blocks so they fall with them, no colliders): black
// obsidian spires on the roof turrets with a skull set in their front (the team flag FortressDecor planted on the
// turret is moved up to the spire's tip), skull-and-crossbones emblems on the walls, iron braziers burning with green
// soulfire, a little graveyard of tombstones and bones in front of the walls, and bone spikes on the merlons. At night
// up to MaxLights green lights. FortressDecor has already placed the team flag and banners.
public static class LichDecor {
    const int MaxLights = 3, MaxSpikes = 8;
    const float SpireHeight = 1.7f, FlagSink = 0.3f;
    static readonly Color Obsidian = new Color(0.07f, 0.08f, 0.08f), Grave = new Color(0.42f, 0.43f, 0.45f);
    static readonly Color Flame = new Color(0.55f, 1f, 0.4f, 0.9f);

    public static void Apply(Fortress f) {
        FortressPlan plan = f.blueprint as FortressPlan;
        int facing = f.Facing >= 0 ? 1 : -1, lights = 0;
        bool night = ArenaBuilder.IsNight;
        if (plan != null && f.blocks.Count == plan.blocks.Count) {   // anchors need the original block order
            foreach (DecorAnchor a in plan.decor) {
                Block host = a.block >= 0 && a.block < f.blocks.Count ? f.blocks[a.block] : null;
                if (host == null) continue;
                switch (a.kind) {
                    case DecorKind.TurretRoof: Spire(host, a.local); break;
                    case DecorKind.Shield: Emblem(host.transform, a.local); break;
                    case DecorKind.Torch: Brazier(host.transform, a.local, night && lights < MaxLights, ref lights); break;
                    case DecorKind.Base: Graveyard(host, a.local, a.span, facing); break;
                }
            }
        }
        Spikes(f.blocks);
        LichSounds.WarmUp();   // a Lich is in this battle: get his sounds ready
    }

    // ------------------------------------------------------------------ spires
    // A four-sided obsidian spire on the turret with a skull in its front face; the team flag flies from its tip, or
    // a soulfire orb burns there when the turret has no flag.
    static void Spire(Block turret, Vector3 at) {
        Transform host = turret.transform;
        Vector3 size = turret.size;
        Transform root = Root("Obsidian Spire", host, at);
        float r = 0.5f * Mathf.Min(size.x, size.z) * 1.2f;
        Material stone = Mats.Lit(Obsidian, 0.55f, 0.2f);
        Part(root, WorldMeshes.Frustum(4, r, 0f, SpireHeight), Vector3.zero, stone, new Vector3(0f, 45f, 0f));
        Part(root, WorldMeshes.Frustum(4, r + 0.05f, r + 0.03f, 0.08f), Vector3.zero, LichKit.BoneMat, new Vector3(0f, 45f, 0f));
        Skull(root, new Vector3(0f, 0.38f, -r * 0.62f), 0.13f);
        Transform flag = host.Find("Flag");   // planted by FortressDecor on the turret's top
        if (flag != null) flag.localPosition = at + Vector3.up * (SpireHeight - FlagSink);
        else {
            Transform orb = Root("Soul Orb", root, Vector3.up * (SpireHeight + 0.08f));
            Part(orb, WorldMeshes.Ball(0.09f), Vector3.zero, LichKit.SoulMat);
            VFX.Emitter(orb, Flame, 0.14f, 10f);
        }
    }

    // ------------------------------------------------------------------ wall emblems
    // A bone skull over two crossed bones, its sockets glowing, on a face that looks at the camera.
    static void Emblem(Transform host, Vector3 at) {
        Transform root = Root("Skull Emblem", host, at + new Vector3(0f, 0.1f, -0.02f));
        for (int s = -1; s <= 1; s += 2) {
            Vector3 a = new Vector3(-0.2f * s, -0.2f, 0f), b = new Vector3(0.2f * s, 0.2f, 0f);
            LichKit.Rod(root, a, b, 0.03f, 0.03f, LichKit.BoneMat);
            Part(root, WorldMeshes.Ball(0.04f), a, LichKit.BoneMat);
            Part(root, WorldMeshes.Ball(0.04f), b, LichKit.BoneMat);
        }
        Skull(root, new Vector3(0f, 0.02f, -0.04f), 0.13f);
    }

    // A skull of radius r facing the camera (-z), with glowing sockets.
    static void Skull(Transform parent, Vector3 at, float r) {
        Transform s = Root("Skull", parent, at);
        Part(s, WorldMeshes.Ball(r), Vector3.zero, LichKit.BoneMat, default, new Vector3(1f, 1.05f, 0.8f));
        Part(s, WorldMeshes.Ball(r * 0.6f), new Vector3(0f, -r * 0.6f, -r * 0.15f), LichKit.BoneMat, default, new Vector3(1f, 0.7f, 0.9f));
        for (int k = -1; k <= 1; k += 2) {
            Part(s, WorldMeshes.Ball(r * 0.28f), new Vector3(k * r * 0.38f, r * 0.05f, -r * 0.72f), LichKit.SocketMat);
            Part(s, WorldMeshes.Ball(r * 0.12f), new Vector3(k * r * 0.38f, r * 0.05f, -r * 0.85f), LichKit.SoulMat);
        }
    }

    // ------------------------------------------------------------------ braziers
    // An iron bracket and bowl on the wall with green soulfire burning in it (and a green light at night).
    static void Brazier(Transform host, Vector3 at, bool light, ref int lights) {
        Transform root = Root("Soul Brazier", host, at + new Vector3(0f, 0.1f, -0.02f));
        Material iron = Mats.Lit(new Color(0.08f, 0.08f, 0.09f), 0.5f, 0.5f);
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0f, -0.08f), iron, default, new Vector3(0.05f, 0.05f, 0.16f));
        Part(root, WorldMeshes.Frustum(8, 0.07f, 0.15f, 0.12f), new Vector3(0f, 0.02f, -0.18f), iron);
        Transform fire = Root("Soulfire", root, new Vector3(0f, 0.16f, -0.18f));
        Part(fire, WorldMeshes.Ball(0.07f), Vector3.zero, LichKit.SoulMat);
        VFX.Emitter(fire, Flame, 0.2f, 16f);
        if (light) {
            lights++;
            LichKit.Glow(fire, new Vector3(0f, 0.1f, -0.4f), 1.6f, 5f);
        }
    }

    // ------------------------------------------------------------------ graveyard
    // Two leaning tombstones and a skull in the grass in front of the wall, on the side away from it.
    static void Graveyard(Block pillar, Vector3 at, float clear, int facing) {
        Transform root = Root("Graveyard", pillar.transform, at + new Vector3(facing * (pillar.size.x * 0.5f + clear + 0.35f), 0f, -0.55f));
        root.localScale = new Vector3(facing, 1f, 1f);   // it runs away from the wall
        Material grave = Mats.Lit(Grave, 0.12f);
        Tomb(root, new Vector3(0.05f, 0f, 0f), new Vector3(0f, 6f, -8f), 1f, grave);
        Tomb(root, new Vector3(0.62f, 0f, 0.35f), new Vector3(0f, -10f, 6f), 0.8f, grave);
        Skull(root, new Vector3(0.32f, 0.07f, -0.2f), 0.08f);
        LichKit.Rod(root, new Vector3(0.4f, 0.03f, -0.1f), new Vector3(0.7f, 0.03f, -0.25f), 0.022f, 0.02f, LichKit.BoneMat);
    }

    static void Tomb(Transform parent, Vector3 at, Vector3 euler, float scale, Material m) {
        Transform t = Root("Tombstone", parent, at);
        t.localRotation = Quaternion.Euler(euler);
        t.localScale = Vector3.one * scale;
        Part(t, PrimitiveType.Cube, new Vector3(0f, 0.22f, 0f), m, default, new Vector3(0.34f, 0.44f, 0.09f));
        Part(t, WorldMeshes.Dome(10, 2, 0.17f), new Vector3(0f, 0.44f, 0f), m, default, new Vector3(1f, 0.7f, 0.26f));
        Part(t, PrimitiveType.Cube, new Vector3(0f, 0.3f, -0.047f), LichKit.StoneDarkMat, default, new Vector3(0.03f, 0.2f, 0.01f));   // a carved cross
        Part(t, PrimitiveType.Cube, new Vector3(0f, 0.34f, -0.047f), LichKit.StoneDarkMat, default, new Vector3(0.13f, 0.03f, 0.01f));
    }

    // ------------------------------------------------------------------ merlons
    // A bone spike on top of the small blocks that have nothing above them (the merlons and the turret tops).
    static void Spikes(List<Block> blocks) {
        int n = 0;
        foreach (Block b in blocks) {
            if (n >= MaxSpikes) return;
            if (b == null || b.size.x > 0.8f || b.size.y > 0.8f) continue;
            if (b.material == BlockMaterial.Glass || b.material == BlockMaterial.Ice || b.material == BlockMaterial.Explosive) continue;
            if (!OpenAbove(b, blocks) || b.transform.Find("Obsidian Spire") != null) continue;
            Transform root = Root("Bone Spike", b.transform, new Vector3(0f, b.size.y * 0.5f, -0.2f));
            Part(root, MeshKit.Cone(6, 0.07f, 0.42f), Vector3.zero, LichKit.BoneMat, new Vector3(0f, 0f, (n % 2 == 0 ? 1f : -1f) * 6f));
            Part(root, WorldMeshes.Ball(0.06f), Vector3.zero, LichKit.BoneDarkMat);
            n++;
        }
    }

    // Blocks still stand where they were built, axis-aligned: nothing above = no block whose bottom is at or above
    // this one's top and overlaps it across.
    static bool OpenAbove(Block b, List<Block> blocks) {
        Vector3 p = b.transform.position;
        float top = p.y + b.size.y * 0.5f, x0 = p.x - b.size.x * 0.5f, x1 = p.x + b.size.x * 0.5f;
        foreach (Block o in blocks) {
            if (o == null || o == b) continue;
            Vector3 q = o.transform.position;
            if (q.y - o.size.y * 0.5f < top - 0.05f) continue;
            if (q.x + o.size.x * 0.5f > x0 + 0.05f && q.x - o.size.x * 0.5f < x1 - 0.05f) return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ helpers
    static Transform Root(string name, Transform parent, Vector3 at) {
        Transform t = ModelKit.Root(name, parent).transform;
        t.localPosition = at;
        return t;
    }

    static GameObject Part(Transform parent, Mesh mesh, Vector3 at, Material m, Vector3 euler = default, Vector3 scale = default) {
        return ModelKit.MeshPart(parent, mesh, at, scale == default ? Vector3.one : scale, m, euler);
    }

    static GameObject Part(Transform parent, PrimitiveType type, Vector3 at, Material m, Vector3 euler = default, Vector3 scale = default) {
        return ModelKit.Part(parent, type, at, scale == default ? Vector3.one : scale, m, euler);
    }
}

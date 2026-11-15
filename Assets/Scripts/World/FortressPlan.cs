using System.Collections.Generic;
using UnityEngine;

// Visual details the generator asks for. FortressDecor builds each one in the style of the fortress's class:
// a turret roof is a soldier's battlement cap, a wizard's spire with a floating crystal or a god's golden dome;
// a shield is a soldier's shield, a wizard's rune circle or a god's sun disc; the base of the front wall gets a
// palisade, a crystal cluster or a runestone. Only flags and banners show the team colour.
public enum DecorKind { Flag, TurretRoof, Banner, Shield, Torch, Brazier, Pediment, Base }

public struct DecorAnchor {
    public DecorKind kind;
    public int block;        // index in blueprint.blocks (Fortress.blocks has the same order when Decorate runs)
    public Vector3 local;    // offset from the block centre
    public float span;       // Pediment: the width it covers; Base: free ground before the decor starts (m)

    public DecorAnchor(DecorKind kind, int block, Vector3 local, float span = 0f) {
        this.kind = kind; this.block = block; this.local = local; this.span = span;
    }
}

// One storey of a post-and-lintel frame: a row of pillars and the lintels lying on them. Positions along the
// fortress are in u = metres towards the enemy (FortressPlan.X turns them into x).
public class Storey {
    public float[] us;          // pillar centres, ascending u (index 0 = the back, away from the enemy)
    public float[] widths;      // pillar widths
    public int[] pillars;       // block index of each pillar
    public readonly List<int> lintels = new List<int>();
    public float floorY;        // what the storey stands on: the ground or the lintel tops below
    public float pillarTop;
    public float top;           // top of the lintels = floor of the next storey

    public int Bays => us.Length - 1;
    public int FrontIndex => us.Length - 1;
    public float Back => us[0] - widths[0] * 0.5f;                                   // outer faces
    public float Front => us[FrontIndex] + widths[FrontIndex] * 0.5f;
    public float BayBack(int i) => us[i] + widths[i] * 0.5f;                         // the free floor of a bay
    public float BayFront(int i) => us[i + 1] - widths[i + 1] * 0.5f;
    public float BayCenter(int i) => (BayBack(i) + BayFront(i)) * 0.5f;
    public float BayWidth(int i) => BayFront(i) - BayBack(i) - 2f * FortressPlan.Joint;   // room for a filler
    public float PillarHeight => pillarTop - floorY - FortressPlan.Gap;
}

// A fortress blueprint under construction. The layouts live in FortressGenerator; this class holds the rules
// that make a fresh fortress stand still when its rigidbodies wake up, and the commander's chamber:
//  * post and lintel only: pillars stand on the ground or on lintels, lintels and slabs rest on pillars;
//  * a lintel spans pillar centre to pillar centre (outer ends flush with the end pillar's face), so the
//    pillars of the next storey always land on lintels, at worst half on each of two;
//  * every block starts Gap above its support and Joint away from its neighbours, so nothing overlaps;
//  * the commander stands in a chamber: thick walls on both sides plus one more wall towards the enemy, a
//    floor below and a roof of two layers above, all in the strongest material of the tier. Shots leave
//    LaunchClearance above that roof (projectiles pass through their own team's blocks).
public class FortressPlan : FortressBlueprint {
    public const float Gap = 0.02f;
    public const float Joint = 0.03f;
    public const float Wall = 0.85f;              // chamber walls
    public const float Buttress = 0.7f;           // the extra wall on the enemy side
    public const float RoomSpan = 2.4f;           // wall centre to wall centre: 1.55 m of floor
    public const float RoomHeight = 2.05f;        // chamber pillars: 2.09 m of headroom (a wizard's hat is ~2 m)
    public const float LaunchClearance = 0.9f;
    public const float TurretWidth = 0.8f, TurretDepth = 1f;
    public const float PedestalWidth = 0.6f, PedestalHeight = 0.45f;
    const float FrontFace = -World.BlockDepth * 0.5f;

    public readonly FortressSetup setup;
    public readonly HeroClass hero;
    public readonly int front;                    // +1 when the enemy is on the +x side
    public readonly List<DecorAnchor> decor = new List<DecorAnchor>();
    public float roomBack, roomFront;             // the chamber's free floor (u)
    readonly System.Random rng;
    int kegsLeft;

    public FortressPlan(FortressSetup s) {
        setup = s;
        hero = s.heroClass;
        front = s.facing >= 0 ? 1 : -1;
        rng = new System.Random(s.seed * 7919 + (int)s.style * 131 + s.tier * 17);
        kegsLeft = Mathf.Max(0, s.kegs);
    }

    public float RoomCenter => (roomBack + roomFront) * 0.5f;

    // Turret on the roof: the base of a wizard's spire, the drum of a god's dome, a square tower otherwise.
    public float TurretHeight => hero == HeroClass.Wizard ? 1.3f : hero == HeroClass.God ? 0.9f : 1.05f;

    // ------------------------------------------------------------------ randomness (deterministic per seed)
    public float Range(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    public bool Chance(float p) { return rng.NextDouble() < p; }

    // Layouts are written in "u" (metres towards the enemy); this converts to fortress-local x.
    public float X(float u) { return u * front; }

    // ------------------------------------------------------------------ blocks
    // A block whose bottom face is at `bottom` (callers pass support top + Gap). depth <= 0: World.BlockDepth.
    public int Block(float u, float bottom, float w, float h, BlockMaterial m, float depth = 0f) {
        blocks.Add(new BlockSpec(new Vector3(X(u), bottom + h * 0.5f, 0f), new Vector3(w, h, depth), m));
        return blocks.Count - 1;
    }

    public void SetMat(int block, BlockMaterial m) {
        BlockSpec b = blocks[block];
        b.mat = m;
        blocks[block] = b;
    }

    public void Harden(int block) { SetMat(block, StrongMat); }

    // The span a block covers along u.
    public void URange(int block, out float u0, out float u1) {
        BlockSpec b = blocks[block];
        float a = (b.center.x - b.size.x * 0.5f) * front, c = (b.center.x + b.size.x * 0.5f) * front;
        u0 = Mathf.Min(a, c);
        u1 = Mathf.Max(a, c);
    }

    public float Top(int block) { return blocks[block].center.y + blocks[block].size.y * 0.5f; }

    bool Covers(int block, float u0, float u1) {
        URange(block, out float a, out float b);
        return a < u1 && b > u0;
    }

    // Pillars at us (ascending u) standing on floorY, then lintels on top. groups = bays per lintel from the back
    // to the front: {2, 1} is a long lintel over the first two bays and a short one over the last. Long lintels
    // tie the frame together; changing the grouping between storeys interlocks them like brickwork.
    public Storey AddStorey(float floorY, float[] us, float[] widths, float pillarH, float lintelH, int[] groups,
                            int level, int levels) {
        Storey s = new Storey { us = us, widths = widths, pillars = new int[us.Length], floorY = floorY };
        BlockMaterial pm = PillarMat(level, levels);
        for (int i = 0; i < us.Length; i++) s.pillars[i] = Block(us[i], floorY + Gap, widths[i], pillarH, pm);
        s.pillarTop = floorY + Gap + pillarH;

        float y = s.pillarTop + Gap;
        int a = 0, last = s.FrontIndex, g = 0;
        while (a < last) {
            int bays = g < groups.Length ? Mathf.Max(1, groups[g]) : last - a;
            int b = Mathf.Min(a + bays, last);
            float back = a == 0 ? s.Back : us[a] + Joint * 0.5f;
            float fore = b == last ? s.Front : us[b] - Joint * 0.5f;
            s.lintels.Add(Block((back + fore) * 0.5f, y, fore - back, lintelH, LintelMat(level, g)));
            a = b;
            g++;
        }
        s.top = y + lintelH;
        return s;
    }

    public Storey AddStorey(float floorY, float[] us, float width, float pillarH, float lintelH, int[] groups,
                            int level, int levels) {
        float[] widths = new float[us.Length];
        for (int i = 0; i < widths.Length; i++) widths[i] = width;
        return AddStorey(floorY, us, widths, pillarH, lintelH, groups, level, levels);
    }

    // ------------------------------------------------------------------ the commander's chamber
    // Bay `bay` of storey s becomes the chamber: its two pillars are the walls, the lintels of `below` the floor
    // and the storey's own lintels the first layer of the roof (the generator lays the second with Course).
    public void Chamber(Storey s, Storey below, int bay) {
        roomBack = s.BayBack(bay);
        roomFront = s.BayFront(bay);
        commanderSpot = new Vector3(X(RoomCenter), s.floorY, 0f);
        Harden(s.pillars[bay]);
        Harden(s.pillars[bay + 1]);
        foreach (int l in s.lintels) if (Covers(l, roomBack, roomFront)) Harden(l);
        foreach (int l in below.lintels) if (Covers(l, roomBack, roomFront)) Harden(l);
    }

    // The roof's second layer: slabs on the lintel tops (y) from u0 to u1, cut at `cuts` (chosen away from the
    // lintel joints below, so the two layers interlock). Returns the slabs, back to front.
    public List<int> Course(float y, float u0, float u1, float h, params float[] cuts) {
        List<int> slabs = new List<int>();
        float a = u0;
        for (int i = 0; i <= cuts.Length; i++) {
            float b = i < cuts.Length ? cuts[i] - Joint * 0.5f : u1;
            slabs.Add(Block((a + b) * 0.5f, y + Gap, b - a, h, StrongMat));
            if (i < cuts.Length) a = cuts[i] + Joint * 0.5f;
        }
        return slabs;
    }

    // A small battlement / step block standing on the lintels at height y.
    public int Merlon(float u, float y, float w, float h) { return Block(u, y + Gap, w, h, TrimMat()); }

    // A low block standing on the ground against an outer pillar: a stepped plinth that widens the base.
    public int Plinth(float faceU, int side, float w, float h) {
        return Block(faceU + side * (Joint + w * 0.5f), Gap, w, h, TrimMat());
    }

    // ------------------------------------------------------------------ bay fillers
    // Ground floor: powder kegs first (they are the fun part), then crates or ice blocks. Each stands against one
    // of the bay's pillars, a joint away, so it rests snugly against it.
    public void FillGroundBays(Storey s) {
        foreach (int bay in Shuffled(s.Bays)) {
            float w = s.BayWidth(bay), y = s.floorY + Gap;
            int side = Chance(0.5f) ? -1 : 1;
            if (kegsLeft > 0 && w >= 0.9f) {
                Block(Against(s, bay, side, 0.9f), y, 0.9f, 0.9f, BlockMaterial.Explosive, 0.9f);
                kegsLeft--;
            } else if (setup.ice && w >= 0.8f && Chance(0.4f)) {
                Block(Against(s, bay, side, 0.8f), y, 0.8f, 0.8f, BlockMaterial.Ice, 0.8f);
            } else if (w >= 0.75f && Chance(0.55f)) {
                Crates(Against(s, bay, side, 0.75f), y, s.PillarHeight, -side);
            }
        }
    }

    // The centre of a filler w wide standing against the back (side < 0) or front pillar of a bay.
    static float Against(Storey s, int bay, int side, float w) {
        return side < 0 ? s.BayBack(bay) + Joint + w * 0.5f : s.BayFront(bay) - Joint - w * 0.5f;
    }

    // Upper floors: glass windows when the level asks for them, the odd crate otherwise. The chamber's bay
    // (skipBay) stays empty: nothing may stand in the commander's room.
    public void FillUpperBays(Storey s, float glassChance = 0.6f, int skipBay = -1) {
        for (int bay = 0; bay < s.Bays; bay++) {
            float w = s.BayWidth(bay), u = s.BayCenter(bay), y = s.floorY + Gap;
            if (bay == skipBay || w < 0.5f) continue;
            if (setup.glass && Chance(glassChance)) {
                // A pane standing on the floor and stopping short of the lintel above, so it carries no load.
                Block(u, y, Mathf.Min(w, 1.6f), s.PillarHeight - 0.06f, BlockMaterial.Glass, 0.3f);
            } else if (w >= 0.75f && Chance(0.15f)) {
                Crates(u, y, s.PillarHeight, Chance(0.5f) ? -1 : 1);
            }
        }
    }

    // A crate, sometimes with a smaller one on top, nudged towards `lean` (away from a pillar it stands against).
    void Crates(float u, float y, float room, int lean) {
        Block(u, y, 0.75f, 0.75f, BlockMaterial.Wood, 0.75f);
        if (room > 1.4f && Chance(0.4f))
            Block(u + lean * Range(0.03f, 0.08f), y + 0.75f + Gap, 0.5f, 0.5f, BlockMaterial.Wood, 0.5f);
    }

    List<int> Shuffled(int n) {
        List<int> list = new List<int>();
        for (int i = 0; i < n; i++) list.Insert(rng.Next(list.Count + 1), i);
        return list;
    }

    // ------------------------------------------------------------------ decorations
    // On the top face of a block (raise > 0 lifts it).
    public void OnTop(DecorKind kind, int block, float raise = 0f) {
        decor.Add(new DecorAnchor(kind, block, new Vector3(0f, blocks[block].size.y * 0.5f + raise, 0f)));
    }

    // On the camera-facing side of a block, `height` above its centre.
    public void OnFront(DecorKind kind, int block, float height) {
        decor.Add(new DecorAnchor(kind, block, new Vector3(0f, height, FrontFace)));
    }

    // A banner (hangs from near the top), shield (middle) or wall light (lower) on pillar i of a storey.
    public void OnPillar(DecorKind kind, Storey s, int i) {
        float h = s.PillarHeight;
        float y = kind == DecorKind.Banner ? h * 0.5f - 0.1f : kind == DecorKind.Shield ? h * 0.1f : h * 0.15f;
        OnFront(kind, s.pillars[i], y);
    }

    // A gable roof lying on top of `block`, centred between u0 and u1.
    public void Pediment(int block, float u0, float u1) {
        float dx = X((u0 + u1) * 0.5f) - blocks[block].center.x;
        decor.Add(new DecorAnchor(DecorKind.Pediment, block, new Vector3(dx, blocks[block].size.y * 0.5f, 0f), u1 - u0));
    }

    // The foot of the front ground pillar; the decor (palisade, crystals, runestone) stands on the ground in front
    // of it, `clear` metres beyond its face (past a plinth).
    public void AtBase(int pillar, float clear = 0f) {
        decor.Add(new DecorAnchor(DecorKind.Base, pillar, new Vector3(0f, -blocks[pillar].size.y * 0.5f, 0f), clear));
    }

    // ------------------------------------------------------------------ materials by tier
    // Tier 0 is all timber; higher tiers move to stone and finally to a metal-shod base.
    public BlockMaterial PillarMat(int level, int levels) {
        switch (setup.tier) {
            case 0: return BlockMaterial.Wood;
            case 1: return level == 0 ? BlockMaterial.Stone : BlockMaterial.Wood;
            case 2: return levels > 2 && level == levels - 1 ? BlockMaterial.Wood : BlockMaterial.Stone;
            default: return level == 0 ? BlockMaterial.Metal : BlockMaterial.Stone;
        }
    }

    public BlockMaterial LintelMat(int level, int index) {
        if (setup.ice && level % 2 == 1 && index % 2 == 0) return BlockMaterial.Ice;
        switch (setup.tier) {
            case 0:
            case 1: return BlockMaterial.Wood;
            case 2: return Chance(0.5f) ? BlockMaterial.Stone : BlockMaterial.Wood;
            default: return Chance(0.3f) ? BlockMaterial.Metal : BlockMaterial.Stone;
        }
    }

    public BlockMaterial TrimMat() {
        if (setup.tier == 0) return BlockMaterial.Wood;
        return setup.tier >= 3 && Chance(0.3f) ? BlockMaterial.Metal : BlockMaterial.Stone;
    }

    // The chamber's walls, floor and roof: the strongest material the tier has.
    public BlockMaterial StrongMat => setup.tier <= 0 ? BlockMaterial.Wood : setup.tier >= 3 ? BlockMaterial.Metal : BlockMaterial.Stone;

    // ------------------------------------------------------------------ finish
    // Centres the footprint on x = 0 (the fortress root stands at FortressSetup.x), fills width/height and sets
    // the launch height: shots start LaunchClearance above the highest block over the chamber's floor.
    public FortressBlueprint Finish() {
        float minX = float.MaxValue, maxX = float.MinValue, maxY = 0f;
        foreach (BlockSpec b in blocks) {
            minX = Mathf.Min(minX, b.center.x - b.size.x * 0.5f);
            maxX = Mathf.Max(maxX, b.center.x + b.size.x * 0.5f);
            maxY = Mathf.Max(maxY, b.center.y + b.size.y * 0.5f);
        }
        if (blocks.Count == 0) { minX = maxX = 0f; }
        float shift = -(minX + maxX) * 0.5f;
        for (int i = 0; i < blocks.Count; i++) {
            BlockSpec b = blocks[i];
            b.center.x += shift;
            blocks[i] = b;
        }
        commanderSpot.x += shift;
        width = maxX - minX;
        height = maxY;

        float roof = commanderSpot.y, half = (roomFront - roomBack) * 0.5f;
        foreach (BlockSpec b in blocks) {
            bool over = Mathf.Abs(b.center.x - commanderSpot.x) < b.size.x * 0.5f + half - 0.01f;
            if (over && b.center.y > commanderSpot.y) roof = Mathf.Max(roof, b.center.y + b.size.y * 0.5f);
        }
        launchHeight = roof + LaunchClearance - commanderSpot.y;
        return this;
    }
}

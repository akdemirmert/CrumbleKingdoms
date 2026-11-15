using System.Collections.Generic;
using UnityEngine;

// Turns a FortressSetup into a block layout (fortress-local: origin = centre of the base, x across, y up).
// Every style is a post-and-lintel frame built with FortressPlan's rules, so a fresh fortress stands still until
// something hits it. The commander stands in a chamber on the top storey: thick walls on both sides, one more
// wall towards the enemy, a floor below and a two-layer roof above. On that roof the class puts its crown: a
// soldier's battlements and square tower, a wizard's spire, a god's golden dome, braziers and pediment (any
// other class gets the soldier's layout and dresses it itself through ClassLooks).
// Layouts are written in u = metres towards the enemy (p.X converts to x), so asymmetric styles face the right
// way for both sides.
public static class FortressGenerator {
    public const int MaxBlocks = 75;
    const float Gap = FortressPlan.Gap, Joint = FortressPlan.Joint;
    const float Wall = FortressPlan.Wall, Buttress = FortressPlan.Buttress;
    const float CourseHeight = 0.45f, RoofLintel = 0.5f;

    public static FortressBlueprint Generate(FortressSetup s) {
        FortressPlan p = new FortressPlan(s);
        switch (s.style) {
            case FortressStyle.Outpost: Outpost(p); break;
            case FortressStyle.Watchtower: Watchtower(p); break;
            case FortressStyle.Bastion: Bastion(p); break;
            case FortressStyle.Ziggurat: Ziggurat(p); break;
            case FortressStyle.Citadel: Citadel(p); break;
            default: Keep(p); break;
        }
        return p.Finish();
    }

    // Flags, banners, emblems, roofs, braziers and lights on the built fortress (visual only).
    public static void Decorate(Fortress f) { FortressDecor.Apply(f); }

    // ------------------------------------------------------------------ styles
    // A small blockhouse: the chamber fills the upper storey and an outer wall shields its front, one roof
    // course over both.
    static void Outpost(FortressPlan p) {
        float[] us = { -1.2f, 1.2f, p.Range(2.9f, 3.05f) };
        float[] widths = { Wall, Wall, 0.75f };
        Storey s0 = p.AddStorey(0f, us, widths, p.Range(1.65f, 1.75f), RoofLintel, new[] { 2 }, 0, 2);
        p.FillGroundBays(s0);
        Storey s1 = p.AddStorey(s0.top, us, widths, FortressPlan.RoomHeight, RoofLintel, new[] { 1, 1 }, 1, 2);
        p.Chamber(s1, s0, 0);
        p.Harden(s1.pillars[2]);
        p.FillUpperBays(s1, 0.7f, 0);
        Roof(p, s1);
        p.Plinth(s0.Back, -1, 0.55f, 1.1f);
        p.Plinth(s0.Front, 1, 0.55f, 1.1f);

        p.OnPillar(DecorKind.Banner, s1, 2);
        p.OnPillar(DecorKind.Shield, s0, 1);
        p.OnPillar(DecorKind.Torch, s0, 0);
        p.OnPillar(DecorKind.Torch, s0, 2);
        p.AtBase(s0.pillars[2], 0.55f + Joint);
    }

    // Tall and narrow: two wide storeys on three thick pillars, two-pillar storeys above and the chamber on top
    // with a buttress against its front wall. Every storey is as wide as the chamber (4 m) on a 4.55 m base.
    static void Watchtower(FortressPlan p) {
        int levels = p.setup.tier >= 3 || p.Chance(0.5f) ? 5 : 4;   // the chamber storey included
        float[] wide = { -1.85f, 0f, 1.85f }, narrow = { -1.615f, 1.615f };
        Storey s0 = p.AddStorey(0f, wide, 0.85f, 1.6f, RoofLintel, new[] { 2 }, 0, levels);
        p.FillGroundBays(s0);
        p.OnPillar(DecorKind.Torch, s0, 0);
        p.OnPillar(DecorKind.Torch, s0, 2);
        p.AtBase(s0.pillars[2]);
        Storey s = p.AddStorey(s0.top, wide, 0.75f, 1.5f, 0.45f, new[] { 1, 1 }, 1, levels);
        p.FillUpperBays(s);
        p.OnPillar(DecorKind.Shield, s, 1);
        for (int level = 2; level < levels - 1; level++) {
            s = p.AddStorey(s.top, narrow, 0.75f, 1.5f, level == levels - 2 ? RoofLintel : 0.45f, new[] { 1 }, level, levels);
            p.FillUpperBays(s);
            if (level == 2) p.OnPillar(DecorKind.Banner, s, 1);
        }
        Storey top = ChamberStorey(p, s, s.Back + Wall * 0.5f, levels - 1, levels);
        Roof(p, top);
    }

    // Three bays by three storeys. Lintel groupings alternate between storeys (interlocking). The chamber is
    // the middle bay of the top storey, so the outer pillars shield it on both sides.
    static void Keep(FortressPlan p) {
        float pw = p.setup.tier >= 2 ? 0.8f : 0.75f;
        float[] us = { -3.3f, -1.2f, 1.2f, 3.3f };
        int[] g0 = p.Chance(0.5f) ? new[] { 2, 1 } : new[] { 1, 2 };
        Storey s0 = p.AddStorey(0f, us, 0.9f, 1.75f, 0.45f, g0, 0, 3);
        p.FillGroundBays(s0);
        Storey s1 = p.AddStorey(s0.top, us, pw, 1.75f, RoofLintel, new[] { g0[1], g0[0] }, 1, 3);
        p.FillUpperBays(s1);
        Storey s2 = p.AddStorey(s1.top, us, new[] { pw, Wall, Wall, pw }, FortressPlan.RoomHeight, RoofLintel,
                                new[] { 1, 1, 1 }, 2, 3);
        p.Chamber(s2, s1, 1);
        p.Harden(s2.pillars[3]);
        p.FillUpperBays(s2, 0.5f, 1);
        float joint = (us[2] + us[3]) * 0.5f;   // course joints over the middle of the side lintels
        Roof(p, s2, -joint, joint);
        p.Plinth(s0.Back, -1, 0.5f, 1.1f);
        p.Plinth(s0.Front, 1, 0.5f, 1.1f);

        p.OnPillar(DecorKind.Banner, s1, 1);
        p.OnPillar(DecorKind.Banner, s1, 2);
        p.OnPillar(DecorKind.Shield, s2, 3);
        p.OnPillar(DecorKind.Shield, s0, 1);
        p.OnPillar(DecorKind.Torch, s0, 0);
        p.OnPillar(DecorKind.Torch, s0, 3);
        p.AtBase(s0.pillars[3], 0.5f + Joint);
    }

    // A two-storey wall on the enemy side shields a taller tower at the back, where the commander's chamber
    // is the top storey. The tower's front is doubled (wall + buttress) all the way down; the gap between
    // tower and wall lets the wall fall without dragging the tower down.
    static void Bastion(FortressPlan p) {
        float back = -4f, wall = back + FortressPlan.RoomSpan, buttress = wall + (Wall + Buttress) * 0.5f + Joint;
        float[] tower = { back, wall, buttress }, tw = { Wall, Wall, Buttress };
        Storey t0 = p.AddStorey(0f, tower, tw, 1.7f, 0.45f, new[] { 1, 1 }, 0, 3);
        p.FillGroundBays(t0);
        Storey t1 = p.AddStorey(t0.top, tower, tw, 1.7f, RoofLintel, new[] { 2 }, 1, 3);
        p.FillUpperBays(t1, 0.4f);
        Roof(p, ChamberStorey(p, t1, back, 2, 3));
        p.Plinth(t0.Back, -1, 0.5f, 1.1f);
        p.OnPillar(DecorKind.Torch, t0, 0);
        p.OnPillar(DecorKind.Shield, t1, 0);

        float[] front = { 0.45f, 2.2f, 3.95f };
        Storey w0 = p.AddStorey(0f, front, 0.75f, 1.7f, 0.45f, new[] { 2 }, 0, 2);
        p.FillGroundBays(w0);
        Storey w1 = p.AddStorey(w0.top, front, 0.7f, 1.7f, 0.45f, new[] { 1, 1 }, 1, 2);
        p.FillUpperBays(w1, 0.25f);
        Parapet(p, w1.top, w1.Back, w1.Front);
        p.OnPillar(DecorKind.Banner, w1, 0);
        p.OnPillar(DecorKind.Banner, w1, 2);
        p.OnPillar(DecorKind.Shield, w0, 1);
        p.OnPillar(DecorKind.Torch, w0, 2);
        p.AtBase(w0.pillars[2]);
    }

    // A stepped pyramid: every storey is one bay narrower than the one below, step stones sit on the lintel
    // ends the next storey leaves bare, and the shrine on top is the commander's chamber.
    static void Ziggurat(FortressPlan p) {
        float[][] rows = {
            new[] { -4f, -2f, 0f, 2f, 4f },
            new[] { -3f, -1f, 1f, 3f },
            new[] { -2f, 0f, 2f },
        };
        float[] widths = { 0.8f, 0.7f, 0.7f };
        int[][] groups = { new[] { 2, 2 }, new[] { 1, 1, 1 }, new[] { 2 } };
        Storey s = null;
        for (int level = 0; level < rows.Length; level++) {
            Storey below = s;
            float lintel = level == rows.Length - 1 ? RoofLintel : 0.45f;
            s = p.AddStorey(below != null ? below.top : 0f, rows[level], widths[level], 1.3f, lintel, groups[level], level, 4);
            if (level == 0) {
                p.FillGroundBays(s);
                p.OnPillar(DecorKind.Banner, s, 1);
                p.OnPillar(DecorKind.Banner, s, 3);
                p.AtBase(s.pillars[4]);
            } else {
                p.FillUpperBays(s, 0.5f);
            }
            if (below != null) Steps(p, below, s, level == 2);
        }
        p.OnPillar(DecorKind.Shield, s, 1);
        Storey top = ChamberStorey(p, s, -0.2f - FortressPlan.RoomSpan * 0.5f, 3, 4);
        Steps(p, s, top, false);
        Roof(p, top);
    }

    // Big, five bays and mixed materials: two full storeys with roofed turrets on the wings, a four-pillar third
    // storey and a central tower on top whose storey is the commander's chamber.
    static void Citadel(FortressPlan p) {
        // five bays below (the middle one 2.4 m: the chamber walls stand over its pillars), three above
        float[] us6 = { -5.05f, -3.125f, -1.2f, 1.2f, 3.125f, 5.05f }, us4 = { -3.125f, -1.2f, 1.2f, 3.125f };
        // Pairs of lintel groupings whose joints never line up between the two storeys.
        int[][] g = p.Chance(0.5f) ? new[] { new[] { 2, 1, 2 }, new[] { 1, 3, 1 } } : new[] { new[] { 1, 2, 2 }, new[] { 2, 2, 1 } };
        Storey s0 = p.AddStorey(0f, us6, 0.8f, 1.75f, 0.45f, g[0], 0, 4);
        p.FillGroundBays(s0);
        Storey s1 = p.AddStorey(s0.top, us6, 0.7f, 1.75f, 0.45f, g[1], 1, 4);
        p.FillUpperBays(s1, 0.5f);
        Storey s2 = p.AddStorey(s1.top, us4, 0.75f, 1.75f, RoofLintel, new[] { 1, 1, 1 }, 2, 4);
        p.FillUpperBays(s2, 0.5f);
        // wing turrets on the bare lintel ends of the second storey, battlements between them and the third
        for (int side = -1; side <= 1; side += 2) {
            float end = side < 0 ? s1.Back : s1.Front, inner = side < 0 ? s2.Back : s2.Front;
            Turret(p, s1.top, end - side * FortressPlan.TurretWidth * 0.5f);
            float a = end - side * (FortressPlan.TurretWidth + Joint), b = inner + side * Joint;
            Parapet(p, s1.top, Mathf.Min(a, b), Mathf.Max(a, b));
        }
        Storey s3 = ChamberStorey(p, s2, us4[1], 3, 4);
        Roof(p, s3);
        Parapet(p, s2.top, s2.Back, s3.Back - Joint);
        Parapet(p, s2.top, s3.Front + Joint, s2.Front);

        p.OnPillar(DecorKind.Banner, s1, 1);
        p.OnPillar(DecorKind.Banner, s1, 4);
        p.OnPillar(DecorKind.Shield, s2, 0);
        p.OnPillar(DecorKind.Shield, s2, 3);
        p.OnPillar(DecorKind.Torch, s0, 0);
        p.OnPillar(DecorKind.Torch, s0, 5);
        p.AtBase(s0.pillars[5]);
    }

    // ------------------------------------------------------------------ the chamber and the crown
    // A storey that is only the chamber: back wall at u = back, the enemy-side wall RoomSpan in front of it and a
    // buttress against that wall, all standing on the storey `below`.
    static Storey ChamberStorey(FortressPlan p, Storey below, float back, int level, int levels) {
        float wall = back + FortressPlan.RoomSpan, buttress = wall + (Wall + Buttress) * 0.5f + Joint;
        Storey s = p.AddStorey(below.top, new[] { back, wall, buttress }, new[] { Wall, Wall, Buttress },
                               FortressPlan.RoomHeight, RoofLintel, new[] { 1, 1 }, level, levels);
        p.Chamber(s, below, 0);
        p.Harden(s.pillars[2]);
        foreach (int l in s.lintels) p.Harden(l);
        return s;
    }

    // The roof over the chamber storey: a course of slabs on its lintels (the second layer), then the crown.
    static void Roof(FortressPlan p, Storey s, params float[] cuts) {
        List<int> slabs = p.Course(s.top, s.Back, s.Front, CourseHeight, cuts);
        Crown(p, slabs, s.top + Gap + CourseHeight, s.Back, s.Front);
    }

    // The class's crown on the roof course (top at y) from u0 (back end) to u1 (front end), with a turret at the
    // back. Nothing stands over the chamber's room itself, so shots leave right above the course.
    static void Crown(FortressPlan p, List<int> slabs, float y, float u0, float u1) {
        int turret = Turret(p, y, u0 + FortressPlan.TurretWidth * 0.5f);
        float free = u0 + FortressPlan.TurretWidth + Joint;
        switch (p.hero) {
            case HeroClass.Wizard:
                p.OnTop(DecorKind.Flag, p.Merlon(u1 - 0.25f, y, 0.5f, 0.6f));
                break;
            case HeroClass.God:
                p.OnTop(DecorKind.Flag, turret);
                float pedestal = u1 - FortressPlan.PedestalWidth - Joint;
                Pedestal(p, y, u1 - FortressPlan.PedestalWidth * 0.5f);
                p.Pediment(SlabUnder(p, slabs, (free + pedestal) * 0.5f), free, pedestal);
                break;
            default:
                p.OnTop(DecorKind.Flag, turret);
                Battlements(p, y, free, u1, p.roomBack - 0.1f, p.roomFront + 0.1f);
                break;
        }
    }

    // A roof turret: the soldier's square tower, the wizard's spire base, the god's dome drum.
    static int Turret(FortressPlan p, float y, float u) {
        int t = p.Block(u, y + Gap, FortressPlan.TurretWidth, p.TurretHeight, p.TrimMat(), FortressPlan.TurretDepth);
        p.OnTop(DecorKind.TurretRoof, t);
        return t;
    }

    // A low block carrying a brazier of holy fire (gods).
    static int Pedestal(FortressPlan p, float y, float u) {
        int b = p.Merlon(u, y, FortressPlan.PedestalWidth, FortressPlan.PedestalHeight);
        p.OnTop(DecorKind.Brazier, b);
        return b;
    }

    // Merlons 0.5 m wide every metre from the front end back to u0, leaving [skip0, skip1] open.
    static void Battlements(FortressPlan p, float y, float u0, float u1, float skip0, float skip1) {
        for (float u = u1 - 0.25f; u - 0.25f >= u0 - 0.001f; u -= 1f) {
            if (u + 0.25f > skip0 && u - 0.25f < skip1) continue;
            p.Merlon(u, y, 0.5f, 0.55f);
        }
    }

    // The top of a plain wall from u0 to u1 in the class's style: crenellations, tall merlons at the ends, or
    // pedestals with braziers at the ends (a short wall gets one, in the middle).
    static void Parapet(FortressPlan p, float y, float u0, float u1) {
        float len = u1 - u0;
        if (len < 0.5f) return;
        switch (p.hero) {
            case HeroClass.God:
                if (len < FortressPlan.PedestalWidth) return;
                float half = FortressPlan.PedestalWidth * 0.5f;
                if (len < 2f) { Pedestal(p, y, (u0 + u1) * 0.5f); break; }
                Pedestal(p, y, u0 + half);
                Pedestal(p, y, u1 - half);
                break;
            case HeroClass.Wizard:
                if (len < 1.7f) { p.Merlon((u0 + u1) * 0.5f, y, 0.5f, 0.6f); break; }
                p.Merlon(u0 + 0.25f, y, 0.5f, 0.6f);
                p.Merlon(u1 - 0.25f, y, 0.5f, 0.6f);
                break;
            default:
                Battlements(p, y, u0, u1, float.MaxValue, float.MinValue);
                break;
        }
    }

    // Step stones on the parts of the lower storey's lintels that the upper storey leaves bare.
    static void Steps(FortressPlan p, Storey below, Storey upper, bool braziers) {
        float back = upper.Back - Joint - below.Back, front = below.Front - upper.Front - Joint;
        if (back >= 0.45f) {
            float w = Mathf.Min(0.7f, back);
            int b = p.Merlon(below.Back + w * 0.5f, below.top, w, 0.4f);
            if (braziers) p.OnTop(DecorKind.Brazier, b);
        }
        if (front >= 0.45f) {
            float w = Mathf.Min(0.7f, front);
            int b = p.Merlon(below.Front - w * 0.5f, below.top, w, 0.4f);
            if (braziers) p.OnTop(DecorKind.Brazier, b);
        }
    }

    static int SlabUnder(FortressPlan p, List<int> slabs, float u) {
        foreach (int s in slabs) {
            p.URange(s, out float a, out float b);
            if (u >= a && u <= b) return s;
        }
        return slabs[0];
    }

    // ------------------------------------------------------------------ validation
    // Checks a blueprint against the building rules (used by tests and handy in the editor): sizes, no overlaps,
    // every block resting on the ground or on blocks right below it with its centre over the support, and the
    // commander's chamber. Returns one line per problem.
    public static List<string> Validate(FortressBlueprint bp) {
        List<string> errors = new List<string>();
        int n = bp.blocks.Count;
        if (n > MaxBlocks) errors.Add(n + " blocks (max " + MaxBlocks + ")");
        for (int i = 0; i < n; i++) {
            BlockSpec a = bp.blocks[i];
            if (a.size.x < 0.35f - 1e-4f || a.size.y < 0.35f - 1e-4f) errors.Add("block " + i + " is thinner than 0.35 m");
            bool touches = false;
            for (int j = 0; j < n; j++) {
                if (j == i) continue;
                if (j > i && Overlap(a, bp.blocks[j]) > -0.005f) errors.Add("blocks " + i + " and " + j + " overlap or touch");
                if (Touching(a, bp.blocks[j])) touches = true;
            }
            if (!touches) errors.Add("block " + i + " (" + a.mat + ") touches no other block (loose piece)");
            string support = CheckSupport(bp, i);
            if (support != null) errors.Add("block " + i + " (" + a.mat + " " + a.size.x.ToString("0.00") + "x" + a.size.y.ToString("0.00") + "): " + support);
        }
        CheckChamber(bp, errors);
        return errors;
    }

    // Two blocks whose faces are within 0.06 m and share more than 0.2 m (stacked or side by side).
    public static bool Touching(BlockSpec a, BlockSpec b) {
        float ox = (a.size.x + b.size.x) * 0.5f - Mathf.Abs(a.center.x - b.center.x);
        float oy = (a.size.y + b.size.y) * 0.5f - Mathf.Abs(a.center.y - b.center.y);
        return (Mathf.Abs(oy) < 0.06f && ox > 0.2f) || (Mathf.Abs(ox) < 0.06f && oy > 0.2f);
    }

    // The chamber around the commander's feet c: nothing inside x c±0.45, y c..c+1.9; a solid floor; at mid-height
    // a wall (>= 0.7 m, not glass or ice) on each side with >= 1.4 m of room between them and a second wall
    // beyond the one towards the enemy; two roof layers over the room; shots leaving above the roof.
    static void CheckChamber(FortressBlueprint bp, List<string> errors) {
        Vector3 c = bp.commanderSpot;
        int front = bp is FortressPlan plan ? plan.front : 1;
        float mid = c.y + 0.95f, roofTop = c.y, lowestRoof = float.MaxValue;
        bool floor = false;
        int nearBack = -1, nearFront = -1, roofLeft = 0, roofCentre = 0, roofRight = 0;
        float backFace = float.MinValue, frontFace = float.MaxValue;   // inner faces, in u
        for (int i = 0; i < bp.blocks.Count; i++) {
            BlockSpec b = bp.blocks[i];
            float left = b.center.x - b.size.x * 0.5f, right = b.center.x + b.size.x * 0.5f;
            float bottom = b.center.y - b.size.y * 0.5f, top = b.center.y + b.size.y * 0.5f;
            if (Mathf.Abs(top - c.y) < 0.005f && left <= c.x && right >= c.x && Solid(b.mat)) floor = true;
            if (left < c.x + 0.45f && right > c.x - 0.45f && bottom < c.y + 1.9f && top > c.y + 0.005f)
                errors.Add("block " + i + " (" + b.mat + ") stands in the chamber");
            if (bottom >= c.y + 1.9f) {
                if (left <= c.x - 0.45f && right >= c.x - 0.45f && Solid(b.mat)) roofLeft++;
                if (left <= c.x && right >= c.x && Solid(b.mat)) roofCentre++;
                if (left <= c.x + 0.45f && right >= c.x + 0.45f && Solid(b.mat)) roofRight++;
                if (left < c.x + 0.45f && right > c.x - 0.45f) {
                    roofTop = Mathf.Max(roofTop, top);
                    lowestRoof = Mathf.Min(lowestRoof, bottom);
                }
            }
            if (bottom < mid && top > mid) {
                // in u (towards the enemy) the block spans [u0, u1]; the commander is at uc
                float u0 = Mathf.Min(left * front, right * front), u1 = Mathf.Max(left * front, right * front), uc = c.x * front;
                if (u1 <= uc && u1 > backFace) { backFace = u1; nearBack = i; }
                if (u0 >= uc && u0 < frontFace) { frontFace = u0; nearFront = i; }
            }
        }
        if (!floor) errors.Add("nothing solid to stand on at the commander spot");
        if (nearBack < 0 || nearFront < 0) { errors.Add("the chamber is open at mid-height"); return; }
        if (frontFace - backFace < 1.4f - 1e-3f) errors.Add("chamber only " + (frontFace - backFace).ToString("0.00") + " m wide");
        foreach (int w in new[] { nearBack, nearFront }) {
            BlockSpec b = bp.blocks[w];
            if (b.size.x < 0.7f - 1e-3f || !Solid(b.mat)) errors.Add("chamber wall " + w + " is " + b.mat + " " + b.size.x.ToString("0.00") + " m");
        }
        bool second = false;   // another wall right in front of the enemy-side one
        BlockSpec fw = bp.blocks[nearFront];
        float fwFar = Mathf.Max((fw.center.x - fw.size.x * 0.5f) * front, (fw.center.x + fw.size.x * 0.5f) * front);
        foreach (BlockSpec b in bp.blocks) {
            float bottom = b.center.y - b.size.y * 0.5f, top = b.center.y + b.size.y * 0.5f;
            float u0 = Mathf.Min((b.center.x - b.size.x * 0.5f) * front, (b.center.x + b.size.x * 0.5f) * front);
            if (bottom < mid && top > mid && u0 >= fwFar - 1e-3f && u0 < fwFar + 2.5f && b.size.x >= 0.6f && Solid(b.mat)) second = true;
        }
        if (!second) errors.Add("no second wall towards the enemy");
        if (Mathf.Min(roofLeft, Mathf.Min(roofCentre, roofRight)) < 2) errors.Add("the roof over the chamber has fewer than two layers");
        if (lowestRoof - c.y < 1.95f - 1e-3f) errors.Add("chamber only " + (lowestRoof - c.y).ToString("0.00") + " m high");
        if (c.y + bp.launchHeight < roofTop + 0.85f) errors.Add("shots would start inside the roof");
    }

    static bool Solid(BlockMaterial m) { return m == BlockMaterial.Wood || m == BlockMaterial.Stone || m == BlockMaterial.Metal; }

    // Penetration depth of two blocks in the x-y plane (all blocks are centred on z = 0); negative = clearance.
    static float Overlap(BlockSpec a, BlockSpec b) {
        float ox = (a.size.x + b.size.x) * 0.5f - Mathf.Abs(a.center.x - b.center.x);
        float oy = (a.size.y + b.size.y) * 0.5f - Mathf.Abs(a.center.y - b.center.y);
        return Mathf.Min(ox, oy);
    }

    static string CheckSupport(FortressBlueprint bp, int i) {
        BlockSpec a = bp.blocks[i];
        float bottom = a.center.y - a.size.y * 0.5f, left = a.center.x - a.size.x * 0.5f, right = a.center.x + a.size.x * 0.5f;
        if (bottom < -1e-4f) return "below the ground";
        if (bottom < FortressPlan.Gap + 0.005f) return null;   // stands on the island
        float lo = float.MaxValue, hi = float.MinValue, carried = 0f;
        foreach (BlockSpec b in bp.blocks) {
            float top = b.center.y + b.size.y * 0.5f;
            if (Mathf.Abs(bottom - FortressPlan.Gap - top) > 0.004f) continue;
            float l = Mathf.Max(left, b.center.x - b.size.x * 0.5f), r = Mathf.Min(right, b.center.x + b.size.x * 0.5f);
            if (r - l <= 0.01f) continue;
            lo = Mathf.Min(lo, l);
            hi = Mathf.Max(hi, r);
            carried += r - l;
        }
        if (carried <= 0f) return "floats (nothing right below it)";
        if (a.center.x < lo + 0.05f || a.center.x > hi - 0.05f) return "centre of mass is not over its supports";
        if (left < lo - 0.05f || right > hi + 0.05f) return "overhangs its supports";
        if (carried < Mathf.Min(0.25f, a.size.x * 0.5f)) return "rests on too little";
        return null;
    }
}

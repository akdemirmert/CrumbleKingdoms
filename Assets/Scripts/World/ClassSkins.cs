using System.Collections.Generic;
using UnityEngine;

// Class "skins" for fortress blocks: the same physics, a different look per class.
//   Soldier: grey granite ashlar, dark riveted iron with a crimson band, iron-strapped oak, leaded glass.
//   Wizard:  dark slate-blue bricks carved with glowing runes, purple-stained wood with silver nails, bronze
//            plates with glowing sigils, violet stained glass.
//   God:     white marble with glowing gold seams and veins, pale ash with gold trims, polished gold plates,
//            amber glass.
// Textures are 256 x 256 and tile once per metre (MeshKit.Box gives faces UVs in metres). All three damage
// stages of a material are made together, the first time a block asks for it (so nothing is generated in the
// middle of a battle), and cached: blocks share one material per class, material and stage. Stage 1 adds
// cracks, stage 2 more cracks and grime; glowing runes and seams fade as the block breaks. Ice and powder
// kegs keep the neutral look (null), and so does any class other than these three (their looks live elsewhere).
public static class ClassSkins {
    const int S = 256;                                      // texels per metre
    static readonly float[] GlowByStage = { 1f, 0.65f, 0.35f };
    static readonly Dictionary<int, Material[]> sets = new Dictionary<int, Material[]>();

    public static Material Block(BlockMaterial m, int stage, HeroClass skin) {
        if (skin != HeroClass.Soldier && skin != HeroClass.Wizard && skin != HeroClass.God) return null;
        if (m == BlockMaterial.Ice || m == BlockMaterial.Explosive || GameAssets.I == null) return null;
        int key = (int)skin * 8 + (int)m;
        if (!sets.TryGetValue(key, out Material[] set) || set[0] == null) {
            set = Build(m, skin);
            sets[key] = set;
        }
        return set[Mathf.Clamp(stage, 0, 2)];
    }

    // What a painter produces: the albedo (alpha = opacity for glass), an optional glow mask, the surface.
    class Look {
        public readonly Color[] albedo = new Color[S * S];
        public Color[] glow;
        public Color glowColor = Color.white;   // HDR multiplier of the mask: above 1 blooms
        public float smooth = 0.2f, metal;
        public bool glass;
    }

    static Material[] Build(BlockMaterial m, HeroClass skin) {
        Look look = Paint(m, skin);
        string name = skin + " " + m;
        Texture2D glow = look.glow != null ? ToTexture(look.glow, name + " glow") : null;
        Material[] set = new Material[3];
        for (int stage = 0; stage < 3; stage++) {
            Color[] px = stage == 0 ? look.albedo : Damaged(look.albedo, stage, look.glass, (int)skin * 31 + (int)m * 7);
            Texture2D tex = ToTexture(px, name + " " + stage);
            if (look.glass) set[stage] = Mats.Transparent(Color.white, look.smooth, tex);
            else if (glow != null) set[stage] = Glowing(tex, glow, look.glowColor * GlowByStage[stage], look.smooth, look.metal, name + " " + stage);
            else set[stage] = Mats.LitTex(tex, Color.white, look.smooth, look.metal);
        }
        return set;
    }

    static Look Paint(BlockMaterial m, HeroClass skin) {
        switch (skin) {
            case HeroClass.Wizard:
                return m == BlockMaterial.Stone ? RuneSlate() : m == BlockMaterial.Wood ? PlumWood()
                     : m == BlockMaterial.Metal ? SigilBronze() : StainedGlass();
            case HeroClass.God:
                return m == BlockMaterial.Stone ? GoldMarble() : m == BlockMaterial.Wood ? AshWood()
                     : m == BlockMaterial.Metal ? GoldPlate() : AmberGlass();
            default:
                return m == BlockMaterial.Stone ? Granite() : m == BlockMaterial.Wood ? StrappedOak()
                     : m == BlockMaterial.Metal ? RivetedIron() : LeadedGlass();
        }
    }

    // A copy of the emissive base material (its _EMISSION keyword is on) with an emission mask.
    static Material Glowing(Texture2D albedo, Texture2D mask, Color emission, float smooth, float metal, string name) {
        Material m = new Material(GameAssets.I.litEmissive) { name = name };
        m.SetTexture("_BaseMap", albedo);
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_EmissionMap", mask);
        m.SetColor("_EmissionColor", emission);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        return m;
    }

    // ================================================================== soldier
    // Grey granite in three courses of different heights: speckled stone, pale mortar, bevelled edges.
    static Look Granite() {
        Look l = new Look { smooth = 0.16f };
        float[] heights = { 0.36f, 0.32f, 0.32f }, offsets = { 0f, 0.17f, 0.36f };
        int[] counts = { 2, 3, 2 };
        Color granite = new Color(0.6f, 0.6f, 0.62f), mortar = new Color(0.42f, 0.41f, 0.4f);
        Each((u, v, x, y, i) => {
            float e = Courses(u, v, heights, counts, offsets, out int id, out _, out float fy);
            Color c = granite * (0.82f + 0.3f * Hash(id, 1, 3)) * Mathf.Lerp(0.88f, 1.1f, Noise(u, v, 32, 5, 2));
            c *= Mathf.Lerp(0.9f, 1.06f, Noise(u, v, 3, 11, 3));
            float speck = Hash(x, y, 9);
            if (speck > 0.965f) c = Color.Lerp(c, new Color(0.18f, 0.18f, 0.2f), 0.7f);
            else if (speck < 0.03f) c = Color.Lerp(c, new Color(0.9f, 0.9f, 0.88f), 0.6f);
            else if (speck > 0.5f && speck < 0.508f) c = Color.Lerp(c, new Color(0.72f, 0.56f, 0.52f), 0.6f);
            if (e < 0.008f) c = mortar * Mathf.Lerp(0.85f, 1.05f, Noise(u, v, 16, 7, 2));
            else if (e < 0.026f) c *= Mathf.Lerp(fy > 0.5f ? 1.1f : 0.74f, 1f, (e - 0.008f) / 0.018f);   // lit top edges, shaded bottoms
            l.albedo[i] = Opaque(c);
        });
        return l;
    }

    // Dark iron plates (2 x 2 per metre) with raised rims, rivets, rust streaks and a crimson painted band.
    static Look RivetedIron() {
        Look l = new Look { smooth = 0.42f, metal = 0.5f };
        Color iron = new Color(0.27f, 0.28f, 0.31f), crimson = new Color(0.58f, 0.07f, 0.09f);
        Each((u, v, x, y, i) => {
            float e = Plates(u, v, 2, out int id, out float lx, out float ly);
            float brush = Noise(u, v * 32f, 2, 13, 2);
            Color c = iron * (0.88f + 0.18f * Hash(id, 2, 4)) * Mathf.Lerp(0.88f, 1.08f, brush);
            if (ly > 0.3f && ly < 0.37f && Noise(u, v, 16, 17, 2) < 0.66f) c = crimson * Mathf.Lerp(0.85f, 1.1f, brush);
            float rust = Noise(u, v, 6, 21, 3) - 0.6f + (0.5f - ly) * 0.25f;
            if (rust > 0f) c = Color.Lerp(c, new Color(0.42f, 0.22f, 0.12f), Mathf.Clamp01(rust * 2.5f));
            if (e < 0.006f) c = iron * 0.35f;
            else if (e < 0.016f) c *= 1.22f;
            c = Rivet(c, lx, ly, 0.5f, 0.04f, 0.013f, new Color(0.55f, 0.56f, 0.6f), true);
            l.albedo[i] = Opaque(c);
        });
        return l;
    }

    // Oak planks (five per metre) held by two vertical iron straps with rivets.
    static Look StrappedOak() {
        Look l = new Look { smooth = 0.14f };
        Color light = new Color(0.63f, 0.44f, 0.25f), dark = new Color(0.4f, 0.26f, 0.14f), iron = new Color(0.2f, 0.2f, 0.22f);
        Each((u, v, x, y, i) => {
            int plank = Mathf.FloorToInt(v * 5f);
            float py = v * 5f - plank, shift = Hash(plank, 9, 2);
            float grain = Noise(Frac(u + shift), v * 20f, 4, 31 + plank, 3);
            float rings = 0.5f + 0.5f * Mathf.Sin((grain * 9f + py * 1.5f) * Mathf.PI);
            Color c = Color.Lerp(dark, light, 0.3f + 0.5f * rings) * (0.88f + 0.22f * Hash(plank, 3, 5));
            float edge = Mathf.Min(py, 1f - py) * 0.2f;
            if (edge < 0.005f) c = dark * 0.4f;
            else if (edge < 0.011f) c *= 0.85f;
            float strap = Mathf.Min(Mathf.Abs(u - 0.215f), Mathf.Abs(u - 0.715f));
            if (strap < 0.035f) {
                c = iron * Mathf.Lerp(0.85f, 1.1f, Noise(u, v, 16, 33, 2));
                if (strap > 0.029f) c *= 1.35f;
                float dx = strap, dy = (py - 0.5f) * 0.2f;
                if (dx * dx + dy * dy < 0.012f * 0.012f) c = new Color(0.55f, 0.55f, 0.58f) * (dy > 0f ? 1.15f : 0.8f);
            }
            l.albedo[i] = Opaque(c);
        });
        return l;
    }

    // Small diamond quarries of pale glass in a lead lattice.
    static Look LeadedGlass() {
        Look l = new Look { smooth = 0.92f, glass = true };
        Each((u, v, x, y, i) => {
            float a = (u + v) * 4f, b = (u - v) * 4f + 8f;
            int da = Mathf.FloorToInt(a), db = Mathf.FloorToInt(b);
            float lead = Mathf.Min(Mathf.Min(a - da, 1f - (a - da)), Mathf.Min(b - db, 1f - (b - db))) * 0.177f;   // metres
            float tint = Hash(da & 3, db & 3, 41);   // da, db grow by 4 per tile: & 3 keeps the tile seamless
            Color c = new Color(0.8f + 0.08f * tint, 0.88f, 0.8f + 0.1f * (1f - tint), 0.3f + 0.08f * tint);
            c = Color.Lerp(c, new Color(1f, 1f, 1f, 0.7f), Streak(u, v) * 0.6f);
            if (lead < 0.006f || Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) < 0.012f) c = new Color(0.22f, 0.22f, 0.25f, 1f);
            l.albedo[i] = c;
        });
        return l;
    }

    // ================================================================== wizard
    // Dark slate-blue bricks; about a third of them carry a carved rune that glows violet or cyan.
    static Look RuneSlate() {
        Look l = new Look { smooth = 0.3f, glow = new Color[S * S], glowColor = new Color(2.6f, 2.6f, 2.6f) };
        Color slate = new Color(0.19f, 0.21f, 0.31f), mortar = new Color(0.06f, 0.06f, 0.09f);
        Each((u, v, x, y, i) => {
            float e = Bond(u, v, 4, 2, out int id, out float fx, out float fy);
            float h = Hash(id, 5, 61);
            Color c = slate * (0.78f + 0.4f * Hash(id, 1, 62));
            c = Color.Lerp(c, h < 0.5f ? new Color(0.25f, 0.18f, 0.34f) : new Color(0.15f, 0.22f, 0.3f), 0.35f);
            c *= Mathf.Lerp(0.85f, 1.12f, Noise(u, v * 8f, 4, 63, 3)) * Mathf.Lerp(0.9f, 1.08f, Noise(u, v, 32, 64, 2));
            Color glow = Color.black;
            if (e < 0.007f) c = mortar;
            else {
                if (e < 0.02f) c *= Mathf.Lerp(0.7f, 1f, (e - 0.007f) / 0.013f);
                if (Hash(id, 9, 65) < 0.45f) {
                    float d = Rune(id, (fx - 0.5f) * 0.5f, (fy - 0.5f) * 0.25f);
                    Color hue = Hash(id, 4, 66) < 0.5f ? new Color(0.72f, 0.32f, 1f) : new Color(0.3f, 0.86f, 1f);
                    float k = Mathf.Exp(-(d * d) / (0.014f * 0.014f));
                    glow = hue * k;
                    if (d < 0.0075f) c = Color.Lerp(new Color(0.05f, 0.04f, 0.08f), hue, 0.55f);
                    else c = Color.Lerp(c, hue * 0.5f, k * 0.35f);
                }
            }
            l.albedo[i] = Opaque(c);
            l.glow[i] = Opaque(glow);
        });
        return l;
    }

    // Vertical planks stained deep plum, with silver nails at both ends.
    static Look PlumWood() {
        Look l = new Look { smooth = 0.24f };
        Color dark = new Color(0.16f, 0.07f, 0.15f), light = new Color(0.34f, 0.18f, 0.31f), silver = new Color(0.82f, 0.84f, 0.9f);
        Each((u, v, x, y, i) => {
            int plank = Mathf.FloorToInt(u * 4f);
            float px = u * 4f - plank;
            float grain = Noise(u * 16f, Frac(v + Hash(plank, 1, 71)), 4, 72 + plank, 3);
            float rings = 0.5f + 0.5f * Mathf.Sin((grain * 10f + px * 1.5f) * Mathf.PI);
            Color c = Color.Lerp(dark, light, 0.25f + 0.55f * rings) * (0.85f + 0.25f * Hash(plank, 2, 73));
            float edge = Mathf.Min(px, 1f - px) * 0.25f;
            if (edge < 0.005f) c = dark * 0.4f;
            else if (edge < 0.012f) c *= 0.8f;
            for (int k = 0; k < 4; k++) {
                float dx = (px - (k % 2 == 0 ? 0.28f : 0.72f)) * 0.25f, dy = v - (k < 2 ? 0.07f : 0.93f);
                float r2 = dx * dx + dy * dy;
                if (r2 < 0.011f * 0.011f) c = silver * (dy > 0f ? 1.08f : 0.75f);
                else if (r2 < 0.016f * 0.016f) c *= 0.7f;
            }
            l.albedo[i] = Opaque(c);
        });
        return l;
    }

    // Bronze plates with verdigris at the seams; each plate carries a glowing cyan or violet sigil.
    static Look SigilBronze() {
        Look l = new Look { smooth = 0.5f, metal = 0.55f, glow = new Color[S * S], glowColor = new Color(1.8f, 1.8f, 1.8f) };
        Color bronze = new Color(0.62f, 0.41f, 0.2f), verdigris = new Color(0.27f, 0.52f, 0.45f);
        Each((u, v, x, y, i) => {
            float e = Plates(u, v, 2, out int id, out float lx, out float ly);
            Color c = bronze * (0.9f + 0.16f * Hash(id, 3, 81)) * Mathf.Lerp(0.88f, 1.08f, Noise(u, v * 32f, 2, 82, 2));
            float patina = Noise(u, v, 8, 83, 3) - 0.55f + Mathf.Clamp01(1f - e / 0.06f) * 0.3f;
            if (patina > 0f) c = Color.Lerp(c, verdigris, Mathf.Clamp01(patina * 2f));
            if (e < 0.006f) c = bronze * 0.3f;
            else if (e < 0.015f) c *= 1.2f;
            c = Rivet(c, lx, ly, 0.5f, 0.045f, 0.014f, new Color(0.85f, 0.62f, 0.32f), false);
            float d = Sigil(id, lx - 0.25f, ly - 0.25f);
            Color hue = Hash(id, 7, 84) < 0.6f ? new Color(0.3f, 0.9f, 1f) : new Color(0.6f, 0.32f, 1f);
            float k = Mathf.Exp(-(d * d) / (0.009f * 0.009f));
            if (d < 0.0065f) c = Color.Lerp(new Color(0.12f, 0.08f, 0.05f), hue, 0.5f);
            l.albedo[i] = Opaque(c);
            l.glow[i] = Opaque(hue * k);
        });
        return l;
    }

    // Stained glass: irregular violet, indigo, magenta and cyan cells in a dark lead net.
    static Look StainedGlass() {
        Look l = new Look { smooth = 0.9f, glass = true };
        Color[] palette = {
            new Color(0.6f, 0.25f, 0.95f), new Color(0.32f, 0.26f, 0.9f), new Color(0.85f, 0.3f, 0.78f),
            new Color(0.48f, 0.2f, 0.75f), new Color(0.3f, 0.78f, 0.95f), new Color(0.95f, 0.76f, 0.32f),
        };
        Each((u, v, x, y, i) => {
            float border = Cells(u, v, 4, 91, out int cell, out float d1);
            Color c = palette[Mathf.Min(palette.Length - 1, (int)(Hash(cell, 1, 92) * (Hash(cell, 2, 93) < 0.12f ? 6f : 5f)))];
            c *= Mathf.Lerp(1.15f, 0.8f, Mathf.Clamp01(d1 * 1.6f));
            c.a = 0.58f;
            c = Color.Lerp(c, new Color(1f, 1f, 1f, 0.75f), Streak(u, v) * 0.45f);
            if (border < 0.035f) c = new Color(0.1f, 0.08f, 0.12f, 1f);
            l.albedo[i] = c;
        });
        return l;
    }

    // ================================================================== god
    // White marble slabs joined by gold seams, with grey veins and a few gold veins; the gold glows softly.
    static Look GoldMarble() {
        Look l = new Look { smooth = 0.62f, glow = new Color[S * S], glowColor = new Color(1.25f, 1.25f, 1.25f) };
        Color marble = new Color(0.94f, 0.93f, 0.9f), gold = new Color(1f, 0.77f, 0.3f);
        Each((u, v, x, y, i) => {
            float e = Bond(u, v, 2, 1, out int id, out _, out _);
            Color c = marble * (0.95f + 0.07f * Hash(id, 1, 101));
            c = Color.Lerp(c, Hash(id, 2, 102) < 0.5f ? new Color(0.95f, 0.92f, 0.86f) : new Color(0.9f, 0.92f, 0.95f), 0.4f);
            float warp = Noise(u, v, 4, 103, 3) * 2f;
            float vein = Mathf.Abs(Noise(u + warp * 0.25f, v, 3, 104 + id % 3, 5) * 2f - 1f);
            if (vein < 0.045f) c = Color.Lerp(c, new Color(0.66f, 0.66f, 0.7f), (0.045f - vein) / 0.045f * 0.8f);
            float goldVein = Mathf.Abs(Noise(u, v + warp * 0.2f, 2, 107, 4) * 2f - 1f);
            float glow = 0f;
            if (goldVein < 0.014f) { c = gold * 0.95f; glow = 0.55f; }
            if (e < 0.006f) { c = gold; glow = 1f; }
            else if (e < 0.013f) c *= 0.93f;
            l.albedo[i] = Opaque(c);
            l.glow[i] = Opaque(gold * glow);
        });
        return l;
    }

    // Pale ash planks with a gold trim along the edges of every metre and gold studs.
    static Look AshWood() {
        Look l = new Look { smooth = 0.3f };
        Color light = new Color(0.9f, 0.83f, 0.67f), dark = new Color(0.74f, 0.64f, 0.47f), gold = new Color(1f, 0.78f, 0.32f);
        Each((u, v, x, y, i) => {
            int plank = Mathf.FloorToInt(v * 4f);
            float py = v * 4f - plank;
            float grain = Noise(Frac(u + Hash(plank, 1, 111)), v * 64f, 4, 112, 3);
            Color c = Color.Lerp(dark, light, 0.35f + 0.6f * grain) * (0.94f + 0.1f * Hash(plank, 2, 113));
            float edge = Mathf.Min(py, 1f - py) * 0.25f;
            if (edge < 0.004f) c = dark * 0.62f;
            float trim = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            if (trim < 0.028f) c = gold * (trim < 0.006f || trim > 0.022f ? 0.78f : 1.05f);
            float sx = Mathf.Min(u, 1f - u) - 0.06f, sy = (py - 0.5f) * 0.25f;
            if (sx * sx + sy * sy < 0.012f * 0.012f) c = gold * (sy > 0f ? 1.1f : 0.8f);
            l.albedo[i] = Opaque(c);
        });
        return l;
    }

    // Polished gold plates with an engraved border and a sun disc; a faint glow keeps them bright.
    static Look GoldPlate() {
        Look l = new Look { smooth = 0.72f, metal = 0.7f, glow = new Color[S * S], glowColor = new Color(0.45f, 0.45f, 0.45f) };
        Color gold = new Color(1f, 0.79f, 0.36f);
        Each((u, v, x, y, i) => {
            float e = Plates(u, v, 2, out int id, out float lx, out float ly);
            Color c = gold * Mathf.Lerp(0.85f, 1.08f, Noise(u, v, 4, 121, 3)) * (0.95f + 0.08f * Hash(id, 1, 122));
            float r = Mathf.Sqrt((lx - 0.25f) * (lx - 0.25f) + (ly - 0.25f) * (ly - 0.25f));
            if (e < 0.006f) c = gold * 0.42f;
            else if (Mathf.Abs(e - 0.035f) < 0.004f || Mathf.Abs(r - 0.09f) < 0.005f || r < 0.035f) c *= 0.66f;
            else if (e < 0.014f) c *= 1.12f;
            l.albedo[i] = Opaque(c);
            l.glow[i] = Opaque(c);
        });
        return l;
    }

    // Amber panes in thin gold frames, with a faint sunburst.
    static Look AmberGlass() {
        Look l = new Look { smooth = 0.92f, glass = true };
        Each((u, v, x, y, i) => {
            float e = Plates(u, v, 2, out _, out float lx, out float ly);
            float ang = Mathf.Atan2(ly - 0.25f, lx - 0.25f);
            float ray = Mathf.Clamp01(Mathf.Cos(ang * 12f) * 0.5f + 0.1f);
            Color c = new Color(1f, 0.7f + 0.08f * ray, 0.25f + 0.1f * ray, 0.44f + 0.08f * ray);
            c = Color.Lerp(c, new Color(1f, 0.95f, 0.8f, 0.75f), Streak(u, v) * 0.5f);
            if (e < 0.01f) c = new Color(0.96f, 0.76f, 0.3f, 1f) * (e < 0.004f ? 0.75f : 1f);
            l.albedo[i] = c;
        });
        return l;
    }

    // ================================================================== patterns
    delegate void PixelFn(float u, float v, int x, int y, int i);

    static void Each(PixelFn fn) {
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                fn((x + 0.5f) / S, (y + 0.5f) / S, x, y, y * S + x);
    }

    // Running bond: rows x cols bricks per metre (rows even, so the tile repeats), odd rows shifted half a brick.
    // Returns the distance (m) to the brick's nearest edge; fx, fy = position inside the brick (0..1).
    static float Bond(float u, float v, int rows, int cols, out int id, out float fx, out float fy) {
        float rv = v * rows;
        int row = Mathf.FloorToInt(rv);
        fy = rv - row;
        float cu = u * cols + (row % 2) * 0.5f;
        int col = Mathf.FloorToInt(cu);
        fx = cu - col;
        col %= cols;
        id = row * 31 + col;
        return Mathf.Min(Mathf.Min(fx, 1f - fx) / cols, Mathf.Min(fy, 1f - fy) / rows);
    }

    // Coursed masonry: course k is heights[k] tall (they sum to 1) and holds counts[k] stones shifted by offsets[k].
    static float Courses(float u, float v, float[] heights, int[] counts, float[] offsets, out int id, out float fx, out float fy) {
        int k = 0;
        float y0 = 0f;
        while (k < heights.Length - 1 && v >= y0 + heights[k]) { y0 += heights[k]; k++; }
        fy = Mathf.Clamp01((v - y0) / heights[k]);
        float cu = (u + offsets[k]) * counts[k];
        int col = Mathf.FloorToInt(cu);
        fx = cu - col;
        col %= counts[k];
        id = k * 17 + col;
        return Mathf.Min(Mathf.Min(fx, 1f - fx) / counts[k], Mathf.Min(fy, 1f - fy) * heights[k]);
    }

    // A grid of n x n square plates per metre: distance (m) to the plate edge and the position inside it (m).
    static float Plates(float u, float v, int n, out int id, out float lx, out float ly) {
        int cx = Mathf.FloorToInt(u * n), cy = Mathf.FloorToInt(v * n);
        float size = 1f / n;
        lx = u - cx * size;
        ly = v - cy * size;
        id = cy * 7 + cx;
        return Mathf.Min(Mathf.Min(lx, size - lx), Mathf.Min(ly, size - ly));
    }

    // Tileable Voronoi cells (n x n jittered sites per metre): distance between the two nearest sites' bisector
    // (border) in cell units, the nearest cell's id and the distance to its site.
    static float Cells(float u, float v, int n, int seed, out int cell, out float d1) {
        float px = u * n, py = v * n, best = float.MaxValue, second = float.MaxValue;
        int bx = Mathf.FloorToInt(px), by = Mathf.FloorToInt(py);
        cell = 0;
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++) {
                int cx = bx + ox, cy = by + oy, wx = ((cx % n) + n) % n, wy = ((cy % n) + n) % n;
                float sx = cx + 0.15f + 0.7f * Hash(wx, wy, seed), sy = cy + 0.15f + 0.7f * Hash(wx, wy, seed + 1);
                float d = (sx - px) * (sx - px) + (sy - py) * (sy - py);
                if (d < best) { second = best; best = d; cell = wy * n + wx; }
                else if (d < second) second = d;
            }
        d1 = Mathf.Sqrt(best);
        return (Mathf.Sqrt(second) - d1) * 0.5f;
    }

    // An Elder-Futhark-like rune centred on (px, py) (m): a stave with up to three twigs (from, to). Returns the
    // distance to the nearest stroke.
    const float RuneW = 0.055f, RuneH = 0.085f;
    static readonly Vector4[] Twigs = {
        new Vector4(0f, RuneH, RuneW, RuneH * 0.35f), new Vector4(0f, RuneH * 0.3f, RuneW, -RuneH * 0.3f),
        new Vector4(0f, RuneH, -RuneW, RuneH * 0.35f), new Vector4(-RuneW, RuneH, RuneW, -RuneH),
        new Vector4(0f, 0f, RuneW, RuneH), new Vector4(0f, 0f, -RuneW, -RuneH),
        new Vector4(-RuneW, 0f, RuneW, 0f), new Vector4(0f, -RuneH * 0.3f, -RuneW, -RuneH),
    };

    internal static float Rune(int id, float px, float py) {
        float d = Segment(px, py, 0f, -RuneH, 0f, RuneH);
        int bits = (int)(Hash(id, 3, 67) * 255f);   // no twigs at all is a rune too (Isa)
        int drawn = 0;
        for (int k = 0; k < Twigs.Length && drawn < 3; k++) {
            if ((bits & (1 << k)) == 0) continue;
            d = Mathf.Min(d, Segment(px, py, Twigs[k].x, Twigs[k].y, Twigs[k].z, Twigs[k].w));
            drawn++;
        }
        return d;
    }

    // A sigil centred on (px, py) (m): a ring around a turned triangle with a dot in the middle.
    static float Sigil(int id, float px, float py) {
        float r = Mathf.Sqrt(px * px + py * py);
        float d = Mathf.Min(Mathf.Abs(r - 0.12f), Mathf.Abs(r - 0.018f));
        float turn = Hash(id, 5, 85) * Mathf.PI * 2f;
        for (int k = 0; k < 3; k++) {
            float a0 = turn + k * Mathf.PI * 2f / 3f, a1 = a0 + Mathf.PI * 2f / 3f;
            d = Mathf.Min(d, Segment(px, py, Mathf.Cos(a0) * 0.095f, Mathf.Sin(a0) * 0.095f, Mathf.Cos(a1) * 0.095f, Mathf.Sin(a1) * 0.095f));
        }
        return d;
    }

    internal static float Segment(float px, float py, float ax, float ay, float bx, float by) {
        float dx = bx - ax, dy = by - ay;
        float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
        float ex = px - ax - t * dx, ey = py - ay - t * dy;
        return Mathf.Sqrt(ex * ex + ey * ey);
    }

    // A domed rivet head at the corners of a plate (and the edge middles when `many`), lit from above.
    static Color Rivet(Color c, float lx, float ly, float size, float inset, float radius, Color head, bool many) {
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++) {
                if (!many && (i == 1 || j == 1)) continue;
                float rx = i == 0 ? inset : i == 1 ? size * 0.5f : size - inset, ry = j == 0 ? inset : j == 1 ? size * 0.5f : size - inset;
                float dx = lx - rx, dy = ly - ry, r2 = dx * dx + dy * dy;
                if (r2 < radius * radius) return head * Mathf.Lerp(1.25f, 0.65f, Mathf.Clamp01(0.5f - dy / radius * 0.5f));
                if (r2 < radius * radius * 1.9f && dy < 0f) c *= 0.7f;   // its shadow
            }
        return c;
    }

    // A soft diagonal glint for glass.
    static float Streak(float u, float v) {
        float s = u + v;
        return Mathf.Clamp01(1f - Mathf.Abs(s - 0.7f) * 12f) + Mathf.Clamp01(1f - Mathf.Abs(s - 0.86f) * 30f);
    }

    // Tileable noise: (u, v) scaled by integer multiples of `cells` keep the tile seamless.
    static float Noise(float u, float v, int cells, int seed, int octaves) { return TexKit.Fbm(u * cells, v * cells, cells, seed, octaves); }

    static float Hash(int x, int y, int seed) { return TexKit.Hash(x, y, seed); }

    static float Frac(float x) { return x - Mathf.Floor(x); }

    static Color Opaque(Color c) { c.a = 1f; return c; }

    // ================================================================== damage and upload
    // Cracks wandering across the tile (more and wider at stage 2), plus grime at stage 2; glass cracks whiten.
    static Color[] Damaged(Color[] src, int stage, bool glass, int seed) {
        Color[] px = (Color[])src.Clone();
        System.Random rng = new System.Random(seed * 13 + stage * 101);
        int lines = stage == 1 ? 4 : 10;
        for (int l = 0; l < lines; l++) {
            float x = (float)rng.NextDouble() * S, y = (float)rng.NextDouble() * S;
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            int len = 50 + rng.Next(stage == 1 ? 70 : 140);
            for (int i = 0; i < len; i++) {
                ang += ((float)rng.NextDouble() - 0.5f) * 0.9f;
                if (rng.NextDouble() < 0.04) ang += (rng.NextDouble() < 0.5 ? 1f : -1f) * 0.9f;   // kink
                x += Mathf.Cos(ang) * 1.5f;
                y += Mathf.Sin(ang) * 1.5f;
                int w = stage == 2 && i < len / 3 ? 2 : 1;
                for (int oy = -w; oy <= w; oy++)
                    for (int ox = -w; ox <= w; ox++) {
                        int k = (((int)y + oy) % S + S) % S * S + (((int)x + ox) % S + S) % S;
                        Color c = px[k];
                        if (glass) c = Color.Lerp(c, new Color(1f, 1f, 1f, 0.95f), 0.75f);
                        else { float a = c.a; c *= ox == 0 && oy == 0 ? 0.25f : 0.6f; c.a = a; }
                        px[k] = c;
                    }
            }
        }
        if (stage == 2 && !glass)
            Each((u, v, x, y, i) => {
                Color c = px[i] * Mathf.Lerp(0.66f, 0.92f, Noise(u, v, 3, seed, 2));
                c.a = 1f;
                px[i] = c;
            });
        return px;
    }

    static Texture2D ToTexture(Color[] px, string name) {
        Texture2D t = new Texture2D(S, S, TextureFormat.RGBA32, true) {
            name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4,
        };
        t.SetPixels(px);
        t.Apply(true, true);   // mipmaps; the pixels stay on the GPU only
        return t;
    }
}

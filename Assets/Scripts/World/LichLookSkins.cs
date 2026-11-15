using UnityEngine;

// Block skins of the Lich's fortress (the same physics, a necropolis look):
//   Stone: black-green basalt bricks; some carry a carved rune glowing soul green, and green light seeps through a
//          few of the mortar joints.
//   Wood:  grey, weathered grave planks studded with bone-white nails.
//   Metal: black tarnished iron plates, each with a riveted ring around a glowing green "eye".
//   Glass: sickly green stained glass in a heavy black lead net.
// Like ClassSkins: 256 x 256 textures that tile once per metre, all three damage stages made together the first
// time a material is asked for, one cached material per material and stage; the glow fades as a block cracks.
// Ice and powder kegs keep the neutral look (null).
public static class LichSkins {
    const int S = 256;                                      // texels per metre
    static readonly float[] GlowByStage = { 1f, 0.6f, 0.3f };
    static readonly Material[][] sets = new Material[6][];
    static readonly Color SoulGlow = new Color(0.45f, 1f, 0.32f);

    public static Material Block(BlockMaterial m, int stage) {
        if (m == BlockMaterial.Ice || m == BlockMaterial.Explosive || GameAssets.I == null) return null;
        Material[] set = sets[(int)m];
        if (set == null || set[0] == null) {
            set = Build(m);
            sets[(int)m] = set;
        }
        return set[Mathf.Clamp(stage, 0, 2)];
    }

    class Look {
        public readonly Color[] albedo = new Color[S * S];
        public Color[] glow;
        public Color glowColor = Color.white;   // HDR multiplier of the mask: above 1 blooms
        public float smooth = 0.2f, metal;
        public bool glass;
    }

    static Material[] Build(BlockMaterial m) {
        Look look = m == BlockMaterial.Stone ? Basalt() : m == BlockMaterial.Wood ? GraveWood()
                  : m == BlockMaterial.Metal ? EyeIron() : SoulGlass();
        string name = "Lich " + m;
        Texture2D glow = look.glow != null ? ToTexture(look.glow, name + " glow") : null;
        Material[] set = new Material[3];
        for (int stage = 0; stage < 3; stage++) {
            Color[] px = stage == 0 ? look.albedo : Damaged(look.albedo, stage, look.glass, 70 + (int)m * 7);
            Texture2D tex = ToTexture(px, name + " " + stage);
            if (look.glass) set[stage] = Mats.Transparent(Color.white, look.smooth, tex);
            else if (glow != null) set[stage] = Glowing(tex, glow, look.glowColor * GlowByStage[stage], look.smooth, look.metal, name + " " + stage);
            else set[stage] = Mats.LitTex(tex, Color.white, look.smooth, look.metal);
        }
        return set;
    }

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

    // ------------------------------------------------------------------ painters
    // Running-bond basalt bricks (4 rows x 2 per metre) with bevelled edges; one in eight carries a thin glowing rune,
    // and soul light seeps out of a few patches of the mortar.
    static Look Basalt() {
        Look l = new Look { smooth = 0.24f, glow = new Color[S * S], glowColor = new Color(1.8f, 1.8f, 1.8f) };
        Color black = new Color(0.11f, 0.13f, 0.12f), moss = new Color(0.2f, 0.25f, 0.2f), mortar = new Color(0.035f, 0.04f, 0.035f);
        Each((u, v, x, y, i) => {
            float e = Bond(u, v, 4, 2, out int id, out float fx, out float fy);
            Color c = Color.Lerp(black, moss, Hash(id, 3, 401) * 0.8f) * (0.85f + 0.3f * Hash(id, 5, 402));
            c *= Mathf.Lerp(0.82f, 1.12f, Noise(u, v, 16, 403, 3)) * Mathf.Lerp(0.9f, 1.06f, Noise(u, v, 4, 404, 2));
            Color glow = Color.black;
            if (e < 0.008f) {
                c = mortar;
                float seep = Mathf.Clamp01((Noise(u, v, 6, 405, 3) - 0.68f) * 7f);
                if (seep > 0f) { c = Color.Lerp(mortar, SoulGlow * 0.5f, seep); glow = SoulGlow * seep; }
            } else {
                if (e < 0.022f) c *= Mathf.Lerp(fy > 0.5f ? 1.15f : 0.7f, 1f, (e - 0.008f) / 0.014f);   // lit tops, shaded bottoms
                if (Hash(id, 9, 406) < 0.125f) {
                    float d = ClassSkins.Rune(id + 77, (fx - 0.5f) * 0.5f, (fy - 0.5f) * 0.25f);
                    float k = Mathf.Exp(-(d * d) / (0.008f * 0.008f));
                    if (d < 0.005f) c = Color.Lerp(new Color(0.03f, 0.05f, 0.03f), SoulGlow, 0.5f);
                    glow = SoulGlow * k;
                }
            }
            l.albedo[i] = Opaque(c);
            l.glow[i] = Opaque(glow);
        });
        return l;
    }

    // Horizontal grey planks (five per metre), weathered and split, with two bone-white nails at each end.
    static Look GraveWood() {
        Look l = new Look { smooth = 0.12f };
        Color dark = new Color(0.17f, 0.17f, 0.16f), light = new Color(0.4f, 0.39f, 0.36f), bone = new Color(0.86f, 0.83f, 0.74f);
        Each((u, v, x, y, i) => {
            int plank = Mathf.FloorToInt(v * 5f);
            float py = v * 5f - plank, shift = Hash(plank, 9, 411);
            float grain = Noise(Frac(u + shift), v * 20f, 4, 412 + plank, 3);
            float rings = 0.5f + 0.5f * Mathf.Sin((grain * 9f + py * 1.5f) * Mathf.PI);
            Color c = Color.Lerp(dark, light, 0.2f + 0.55f * rings) * (0.85f + 0.25f * Hash(plank, 3, 413));
            float split = Mathf.Abs(py - (0.3f + 0.4f * Hash(plank, 5, 414)));
            if (split < 0.03f && Noise(u, v, 8, 415 + plank, 2) > 0.55f) c *= 0.35f;                 // a long crack
            float edge = Mathf.Min(py, 1f - py) * 0.2f;
            if (edge < 0.005f) c = dark * 0.35f;
            else if (edge < 0.011f) c *= 0.8f;
            for (int k = 0; k < 4; k++) {
                float dx = u - (k % 2 == 0 ? 0.08f : 0.92f), dy = (py - (k < 2 ? 0.3f : 0.7f)) * 0.2f;
                float r2 = dx * dx + dy * dy;
                if (r2 < 0.012f * 0.012f) c = bone * (dy > 0f ? 1.05f : 0.75f);
                else if (r2 < 0.017f * 0.017f) c *= 0.6f;
            }
            l.albedo[i] = Opaque(c);
        });
        return l;
    }

    // Black iron plates (2 x 2 per metre) with raised rims; each carries a ring of rivets around a glowing green eye.
    static Look EyeIron() {
        Look l = new Look { smooth = 0.45f, metal = 0.55f, glow = new Color[S * S], glowColor = new Color(2f, 2f, 2f) };
        Color iron = new Color(0.17f, 0.18f, 0.18f), tarnish = new Color(0.22f, 0.27f, 0.22f);
        Each((u, v, x, y, i) => {
            float e = Plates(u, v, 2, out int id, out float lx, out float ly);
            float brush = Noise(u, v * 32f, 2, 421, 2);
            Color c = iron * (0.88f + 0.2f * Hash(id, 2, 422)) * Mathf.Lerp(0.88f, 1.08f, brush);
            float stain = Noise(u, v, 6, 423, 3) - 0.55f;
            if (stain > 0f) c = Color.Lerp(c, tarnish, Mathf.Clamp01(stain * 2.5f));
            if (e < 0.006f) c = iron * 0.35f;
            else if (e < 0.016f) c *= 1.25f;
            float dx = lx - 0.25f, dy = ly - 0.25f, r = Mathf.Sqrt(dx * dx + dy * dy);
            Color glow = Color.black;
            float ring = Mathf.Abs(r - 0.15f);
            if (ring < 0.012f) c *= 1.3f;                                                            // a raised ring
            for (int k = 0; k < 8; k++) {                                                            // rivets on it
                float a = k * Mathf.PI / 4f, rx = Mathf.Cos(a) * 0.15f - dx, ry = Mathf.Sin(a) * 0.15f - dy;
                if (rx * rx + ry * ry < 0.013f * 0.013f) c = new Color(0.4f, 0.42f, 0.4f) * (ry < 0f ? 1.2f : 0.7f);
            }
            float eye = Mathf.Sqrt(dx * dx * 0.6f + dy * dy * 2.2f);                                 // an almond eye
            if (eye < 0.06f) {
                float k = 1f - eye / 0.06f;
                c = Color.Lerp(new Color(0.05f, 0.08f, 0.05f), SoulGlow, k * 0.6f);
                glow = SoulGlow * k;
            }
            l.albedo[i] = Opaque(c);
            l.glow[i] = Opaque(glow);
        });
        return l;
    }

    // Stained glass of sickly greens and a few murky golds in a heavy black lead net.
    static Look SoulGlass() {
        Look l = new Look { smooth = 0.9f, glass = true };
        Color[] palette = {
            new Color(0.4f, 0.85f, 0.3f), new Color(0.25f, 0.6f, 0.3f), new Color(0.55f, 0.95f, 0.45f),
            new Color(0.2f, 0.45f, 0.25f), new Color(0.75f, 0.7f, 0.3f),
        };
        Each((u, v, x, y, i) => {
            float border = Cells(u, v, 4, 431, out int cell, out float d1);
            Color c = palette[Mathf.Min(palette.Length - 1, (int)(Hash(cell, 1, 432) * palette.Length))];
            c *= Mathf.Lerp(1.15f, 0.8f, Mathf.Clamp01(d1 * 1.6f));
            c.a = 0.6f;
            float s = u + v;
            float streak = Mathf.Clamp01(1f - Mathf.Abs(s - 0.7f) * 12f);
            c = Color.Lerp(c, new Color(1f, 1f, 1f, 0.75f), streak * 0.4f);
            if (border < 0.04f) c = new Color(0.05f, 0.06f, 0.05f, 1f);
            l.albedo[i] = c;
        });
        return l;
    }

    // ------------------------------------------------------------------ patterns
    delegate void PixelFn(float u, float v, int x, int y, int i);

    static void Each(PixelFn fn) {
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                fn((x + 0.5f) / S, (y + 0.5f) / S, x, y, y * S + x);
    }

    // Running bond: rows x cols bricks per metre, odd rows shifted half a brick. Distance (m) to the brick's nearest
    // edge; fx, fy = position inside the brick (0..1).
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

    // n x n square plates per metre: distance (m) to the plate edge and the position inside it (m).
    static float Plates(float u, float v, int n, out int id, out float lx, out float ly) {
        int cx = Mathf.FloorToInt(u * n), cy = Mathf.FloorToInt(v * n);
        float size = 1f / n;
        lx = u - cx * size;
        ly = v - cy * size;
        id = cy * 7 + cx;
        return Mathf.Min(Mathf.Min(lx, size - lx), Mathf.Min(ly, size - ly));
    }

    // Tileable Voronoi cells: half the distance between the two nearest sites (border), the nearest cell and its distance.
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

    static float Noise(float u, float v, int cells, int seed, int octaves) { return TexKit.Fbm(u * cells, v * cells, cells, seed, octaves); }

    static float Hash(int x, int y, int seed) { return TexKit.Hash(x, y, seed); }

    static float Frac(float x) { return x - Mathf.Floor(x); }

    static Color Opaque(Color c) { c.a = 1f; return c; }

    // ------------------------------------------------------------------ damage and upload
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

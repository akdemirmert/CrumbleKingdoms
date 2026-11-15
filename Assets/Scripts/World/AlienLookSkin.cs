using UnityEngine;

// The Alien fortress skin (registered by AlienLook): the same physics, alien materials.
//   Stone: silver alloy in small hexagonal panels, some outlined by glowing green seams, some with a glowing port.
//   Wood:  faceted crystal (teal, mint and violet facets with softly glowing edges).
//   Metal: dark gunmetal plates with hex bolts and glowing green circuit traces.
//   Glass: glowing green glass in a hexagonal alloy frame (unlit, so it shines at night too).
// Like ClassSkins: 256 x 256 textures that tile once per metre (MeshKit.Box gives faces UVs in metres), all three
// damage stages built together the first time a block asks, cached (one material per material and stage), cracks
// at stages 1 and 2 while the glow fades. Ice and powder kegs keep the neutral look (null).
public static class AlienSkin {
    const int S = 256;                                      // texels per metre
    static readonly float[] GlowByStage = { 1f, 0.6f, 0.3f };
    static readonly Color Green = new Color(0.3f, 1f, 0.55f);
    static readonly Color GlassTint = new Color(1.35f, 1.35f, 1.35f, 1f);   // HDR: the panes bloom
    static readonly Material[][] sets = new Material[6][];

    public static Material Block(BlockMaterial m, int stage) {
        if (m == BlockMaterial.Ice || m == BlockMaterial.Explosive || GameAssets.I == null) return null;
        Material[] set = sets[(int)m];
        if (set == null || set[0] == null) sets[(int)m] = set = Build(m);
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

    static Material[] Build(BlockMaterial m) {
        Look look = m == BlockMaterial.Stone ? HexAlloy() : m == BlockMaterial.Wood ? Crystal()
                  : m == BlockMaterial.Metal ? Gunmetal() : GreenGlass();
        string name = "Alien " + m;
        Texture2D glow = look.glow != null ? ToTexture(look.glow, name + " glow") : null;
        Material[] set = new Material[3];
        for (int stage = 0; stage < 3; stage++) {
            Color[] px = stage == 0 ? look.albedo : Damaged(look.albedo, stage, look.glass, 400 + (int)m * 7);
            Texture2D tex = ToTexture(px, name + " " + stage);
            if (look.glass) set[stage] = Mats.UnlitTransparent(GlassTint, tex);
            else if (glow != null) set[stage] = Glowing(tex, glow, look.glowColor * GlowByStage[stage], look.smooth, look.metal, name + " " + stage);
            else set[stage] = Mats.LitTex(tex, Color.white, look.smooth, look.metal);
        }
        return set;
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

    // ================================================================== painters
    // Brushed silver hex panels with bevelled edges lit from above. About a third are outlined by a glowing green
    // seam; some carry a round port with a glowing core.
    static Look HexAlloy() {
        Look l = new Look { smooth = 0.55f, metal = 0.35f, glow = new Color[S * S], glowColor = new Color(2.4f, 2.4f, 2.4f) };
        Color alloy = new Color(0.68f, 0.71f, 0.75f), groove = new Color(0.14f, 0.16f, 0.18f);
        Each((u, v, x, y, i) => {
            float e = Hex(u, v, out int id, out float lx, out float ly);
            Color c = alloy * (0.88f + 0.16f * Hash(id, 1, 301));
            c *= Mathf.Lerp(0.92f, 1.06f, Noise(u, v * 24f, 2, 302, 2));                  // brushed streaks
            c = Color.Lerp(c, new Color(0.6f, 0.7f, 0.74f), 0.3f * Hash(id, 2, 303));      // some panels cooler
            bool seam = Hash(id, 3, 304) < 0.35f;
            Color glow = Color.black;
            if (e < 0.006f) c = groove;
            else if (e < 0.02f) c *= ly > 0f ? 1.15f : 0.72f;
            if (seam) glow = Green * Mathf.Exp(-(e * e) / (0.008f * 0.008f));
            if (Hash(id, 4, 305) < 0.25f) {
                float r = Mathf.Sqrt(lx * lx + ly * ly);
                if (r < 0.028f) { c = Color.Lerp(groove, Green, 0.6f); glow = Green; }
                else if (r < 0.042f) c *= 0.6f;
            }
            l.albedo[i] = Opaque(c);
            l.glow[i] = Opaque(glow);
        });
        return l;
    }

    // Crystal: Voronoi facets, each a flat face tilted its own way (a brightness gradient across it), in teal,
    // mint, cyan and violet, with bright glowing edges and a glint on the facets that catch the light.
    static Look Crystal() {
        Look l = new Look { smooth = 0.8f, glow = new Color[S * S], glowColor = new Color(1.3f, 1.3f, 1.3f) };
        Color[] hues = {
            new Color(0.38f, 0.9f, 0.82f), new Color(0.45f, 0.95f, 0.6f), new Color(0.35f, 0.72f, 0.95f), new Color(0.62f, 0.48f, 0.95f),
        };
        Each((u, v, x, y, i) => {
            float border = Cells(u, v, 3, 311, out int cell, out float rx, out float ry);
            Color hue = hues[Mathf.Min(hues.Length - 1, (int)(Hash(cell, 1, 312) * hues.Length))];
            float gx = (Hash(cell, 2, 313) - 0.5f) * 1.1f, gy = (Hash(cell, 3, 314) - 0.5f) * 1.1f;
            float shade = Mathf.Clamp(0.8f + (Hash(cell, 4, 315) - 0.5f) * 0.35f + gx * rx + gy * ry, 0.45f, 1.25f);
            Color c = hue * shade * 0.8f;
            c = Color.Lerp(c, Color.white, Mathf.Clamp01((shade - 1f) * 3f) * 0.45f);
            Color glow = Color.black;
            if (border < 0.008f) {
                float k = 1f - border / 0.008f;
                c = Color.Lerp(c, Color.Lerp(hue, Color.white, 0.6f), k);
                glow = hue * k;
            }
            l.albedo[i] = Opaque(c);
            l.glow[i] = Opaque(glow);
        });
        return l;
    }

    // Dark gunmetal plates (2 x 2 per metre) with bevels, a hexagonal bolt in each corner and a glowing green
    // circuit trace per plate: in from the left edge, a bend, down to the bottom edge, with a via at the bend.
    static Look Gunmetal() {
        Look l = new Look { smooth = 0.5f, metal = 0.45f, glow = new Color[S * S], glowColor = new Color(2.2f, 2.2f, 2.2f) };
        Color gun = new Color(0.21f, 0.23f, 0.27f);
        Each((u, v, x, y, i) => {
            float e = Plates(u, v, 2, out int id, out float lx, out float ly);
            Color c = gun * (0.85f + 0.25f * Hash(id, 1, 321)) * Mathf.Lerp(0.9f, 1.08f, Noise(u, v * 16f, 2, 322, 2));
            if (e < 0.006f) c = gun * 0.35f;
            else if (e < 0.016f) c *= ly > 0.25f ? 1.3f : 0.75f;
            for (int k = 0; k < 4; k++) {
                float bx = (k % 2 == 0 ? 0.045f : 0.455f) - lx, by = (k < 2 ? 0.045f : 0.455f) - ly;
                if (HexDistance(bx, by) < 0.016f) c = new Color(0.42f, 0.44f, 0.5f) * (by < 0f ? 1.2f : 0.85f);
            }
            float ty = 0.12f + 0.26f * Hash(id, 2, 323), tx = 0.12f + 0.26f * Hash(id, 3, 324);
            float trace = Mathf.Min(Segment(lx, ly, 0.02f, ty, tx, ty), Segment(lx, ly, tx, ty, tx, 0.02f));
            float via = Mathf.Sqrt((lx - tx) * (lx - tx) + (ly - ty) * (ly - ty));
            Color glow = Color.black;
            if (trace < 0.005f || via < 0.014f) {
                c = Color.Lerp(c, Green, 0.65f);
                glow = Green;
            } else if (trace < 0.012f) {
                glow = Green * 0.3f;
            }
            l.albedo[i] = Opaque(c);
            l.glow[i] = Opaque(glow);
        });
        return l;
    }

    // Glowing green panes, brighter in their middles, with a soft streak of reflection, in a hexagonal frame.
    static Look GreenGlass() {
        Look l = new Look { glass = true };
        Each((u, v, x, y, i) => {
            float e = Hex(u, v, out int id, out float lx, out float ly);
            float r = Mathf.Sqrt(lx * lx + ly * ly);
            Color c = new Color(0.22f, 0.9f, 0.5f, 0.34f + 0.1f * Hash(id, 1, 331));
            c = Color.Lerp(c, new Color(0.6f, 1f, 0.8f, 0.55f), Mathf.Clamp01(1f - r / 0.15f) * 0.4f);
            c = Color.Lerp(c, new Color(1f, 1f, 1f, 0.7f), Streak(u, v) * 0.45f);
            if (e < 0.008f || Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) < 0.01f) c = new Color(0.1f, 0.12f, 0.13f, 1f);
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

    // Hexagonal panels, four across and two row pairs per metre: the cells around the centres of two offset
    // rectangular lattices are hexagons (a little taller than regular, so the pattern tiles once per metre).
    // Returns the distance (m) to the panel's edge, the panel id and the position relative to its centre (m).
    static float Hex(float u, float v, out int id, out float lx, out float ly) {
        const float w = 0.25f, h = 0.5f;
        float best = float.MaxValue, second = float.MaxValue;
        id = 0;
        lx = ly = 0f;
        for (int lattice = 0; lattice < 2; lattice++) {
            float ox = lattice * w * 0.5f, oy = lattice * h * 0.5f;
            int ci = Mathf.FloorToInt((u - ox) / w), cj = Mathf.FloorToInt((v - oy) / h);
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++) {
                    float dx = u - ((ci + di) * w + ox), dy = v - ((cj + dj) * h + oy);
                    float d = dx * dx + dy * dy;
                    if (d < best) {
                        second = best;
                        best = d;
                        lx = dx;
                        ly = dy;
                        id = (Mod(cj + dj, 2) * 4 + Mod(ci + di, 4)) * 2 + lattice;   // wrapped: the tile repeats
                    } else if (d < second) {
                        second = d;
                    }
                }
        }
        return (Mathf.Sqrt(second) - Mathf.Sqrt(best)) * 0.5f;
    }

    // Tileable Voronoi cells (n x n jittered sites per metre): distance (m) to the border with the next cell, the
    // cell's id and the position relative to its site (cell units).
    static float Cells(float u, float v, int n, int seed, out int cell, out float rx, out float ry) {
        float px = u * n, py = v * n, best = float.MaxValue, second = float.MaxValue;
        int bx = Mathf.FloorToInt(px), by = Mathf.FloorToInt(py);
        cell = 0;
        rx = ry = 0f;
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++) {
                int cx = bx + ox, cy = by + oy, wx = Mod(cx, n), wy = Mod(cy, n);
                float sx = cx + 0.15f + 0.7f * Hash(wx, wy, seed), sy = cy + 0.15f + 0.7f * Hash(wx, wy, seed + 1);
                float d = (sx - px) * (sx - px) + (sy - py) * (sy - py);
                if (d < best) {
                    second = best;
                    best = d;
                    cell = wy * n + wx;
                    rx = px - sx;
                    ry = py - sy;
                } else if (d < second) {
                    second = d;
                }
            }
        return (Mathf.Sqrt(second) - Mathf.Sqrt(best)) * 0.5f / n;
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

    // "Radius" of a flat-topped hexagon through (x, y): points with HexDistance < r lie inside a hexagon of radius r.
    static float HexDistance(float x, float y) {
        x = Mathf.Abs(x);
        y = Mathf.Abs(y);
        return Mathf.Max(y * 1.1547f, x + y * 0.57735f);
    }

    static float Segment(float px, float py, float ax, float ay, float bx, float by) {
        float dx = bx - ax, dy = by - ay;
        float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / Mathf.Max(1e-6f, dx * dx + dy * dy));
        float ex = px - ax - t * dx, ey = py - ay - t * dy;
        return Mathf.Sqrt(ex * ex + ey * ey);
    }

    // A soft diagonal glint for glass.
    static float Streak(float u, float v) {
        float s = u + v;
        return Mathf.Clamp01(1f - Mathf.Abs(s - 0.7f) * 12f) + Mathf.Clamp01(1f - Mathf.Abs(s - 0.86f) * 30f);
    }

    // Tileable noise: (u, v) scaled by integer multiples of `cells` keep the tile seamless.
    static float Noise(float u, float v, int cells, int seed, int octaves) { return TexKit.Fbm(u * cells, v * cells, cells, seed, octaves); }

    static float Hash(int x, int y, int seed) { return TexKit.Hash(x, y, seed); }

    static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

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
                        int k = Mod((int)y + oy, S) * S + Mod((int)x + ox, S);
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

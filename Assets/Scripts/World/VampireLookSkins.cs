using UnityEngine;

// Block skins of the Vampire's fortress (the same physics, a gothic look):
//   Stone: black and plum-grey bricks; blood seeps glowing red through a few of the mortar joints.
//   Wood:  ebony planks bound by black iron straps with rivets.
//   Metal: black wrought-iron plates with raised quatrefoil tracery around a glowing red jewel.
//   Glass: blood-red stained glass, a pointed lancet window in every metre.
// Like ClassSkins: 256 x 256 textures that tile once per metre, all three damage stages made together the first
// time a material is asked for, one cached material per material and stage; the glow fades as a block cracks.
// Ice and powder kegs keep the neutral look (null).
public static class VampireSkins {
    const int S = 256;                                      // texels per metre
    static readonly float[] GlowByStage = { 1f, 0.65f, 0.35f };
    static readonly Material[][] sets = new Material[6][];

    public static Material Block(BlockMaterial m, int stage) {
        if (m == BlockMaterial.Ice || m == BlockMaterial.Explosive || GameAssets.I == null) return null;
        Material[] set = sets[(int)m];
        if (set == null || set[0] == null) {
            set = Build(m);
            sets[(int)m] = set;
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

    static Material[] Build(BlockMaterial m) {
        Look look = m == BlockMaterial.Stone ? GothicStone() : m == BlockMaterial.Wood ? Ebony()
                  : m == BlockMaterial.Metal ? WroughtIron() : BloodGlass();
        string name = "Vampire " + m;
        Texture2D glow = look.glow != null ? ToTexture(look.glow, name + " glow") : null;
        Material[] set = new Material[3];
        for (int stage = 0; stage < 3; stage++) {
            Color[] px = stage == 0 ? look.albedo : Damaged(look.albedo, stage, look.glass, 40 + (int)m * 7);
            Texture2D tex = ToTexture(px, name + " " + stage);
            if (look.glass) set[stage] = Mats.Transparent(Color.white, look.smooth, tex);
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

    // ------------------------------------------------------------------ painters
    // Running-bond bricks (4 rows x 2 per metre) in black and plum-grey with bevelled edges (lit tops, shaded
    // bottoms) and pits; patches of the mortar glow with seeping blood.
    static Look GothicStone() {
        Look l = new Look { smooth = 0.22f, glow = new Color[S * S], glowColor = new Color(2.2f, 2.2f, 2.2f) };
        Color black = new Color(0.12f, 0.1f, 0.13f), plum = new Color(0.25f, 0.19f, 0.26f), mortar = new Color(0.04f, 0.035f, 0.045f);
        Color blood = new Color(0.85f, 0.06f, 0.1f);
        Each((u, v, x, y, i) => {
            float e = Bond(u, v, 4, 2, out int id, out float fy);
            Color c = Color.Lerp(black, plum, Hash(id, 3, 201) * 0.85f) * (0.85f + 0.3f * Hash(id, 5, 202));
            c *= Mathf.Lerp(0.82f, 1.12f, Noise(u, v, 16, 203, 3)) * Mathf.Lerp(0.9f, 1.06f, Noise(u, v, 4, 204, 2));
            Color glow = Color.black;
            if (e < 0.008f) {
                c = mortar;
                float seep = Mathf.Clamp01((Noise(u, v, 6, 205, 3) - 0.62f) * 8f);
                if (seep > 0f) {
                    c = Color.Lerp(mortar, blood * 0.6f, seep);
                    glow = blood * seep;
                }
            } else if (e < 0.022f) {
                c *= Mathf.Lerp(fy > 0.5f ? 1.18f : 0.68f, 1f, (e - 0.008f) / 0.014f);
            }
            if (Hash(x, y, 206) > 0.992f) c *= 0.6f;
            l.albedo[i] = Opaque(c);
            l.glow[i] = Opaque(glow);
        });
        return l;
    }

    // Ebony planks (five per metre) with a faint warm grain, dark gaps, and two black iron straps with rivets.
    static Look Ebony() {
        Look l = new Look { smooth = 0.32f };
        Color dark = new Color(0.07f, 0.05f, 0.05f), light = new Color(0.21f, 0.14f, 0.11f);
        Color iron = new Color(0.06f, 0.06f, 0.07f), shine = new Color(0.34f, 0.33f, 0.37f);
        Each((u, v, x, y, i) => {
            int plank = Mathf.FloorToInt(v * 5f);
            float py = v * 5f - plank, shift = Hash(plank, 9, 211);
            float grain = Noise(Frac(u + shift), v * 20f, 4, 212 + plank, 3);
            float rings = 0.5f + 0.5f * Mathf.Sin((grain * 9f + py * 1.5f) * Mathf.PI);
            Color c = Color.Lerp(dark, light, 0.2f + 0.55f * rings) * (0.85f + 0.3f * Hash(plank, 3, 213));
            float edge = Mathf.Min(py, 1f - py) * 0.2f;
            if (edge < 0.005f) c = dark * 0.35f;
            else if (edge < 0.011f) c *= 0.8f;
            float strap = Mathf.Min(Mathf.Abs(u - 0.25f), Mathf.Abs(u - 0.75f));
            if (strap < 0.032f) {
                c = iron * Mathf.Lerp(0.85f, 1.15f, Noise(u, v, 16, 214, 2));
                if (strap > 0.026f) c = Color.Lerp(c, shine, 0.5f);   // the strap's edges catch the light
                float dy = (py - 0.5f) * 0.2f;
                if (strap * strap + dy * dy < 0.011f * 0.011f) c = shine * (dy > 0f ? 1.2f : 0.7f);
            }
            l.albedo[i] = Opaque(c);
        });
        return l;
    }

    // Black wrought-iron plates (2 x 2 per metre) with raised rims, a raised inner frame and quatrefoil tracery
    // catching the light, rivets in the corners, and a glowing red jewel in each quatrefoil.
    static Look WroughtIron() {
        Look l = new Look { smooth = 0.45f, metal = 0.45f, glow = new Color[S * S], glowColor = new Color(2.6f, 2.6f, 2.6f) };
        Color iron = new Color(0.1f, 0.1f, 0.115f), shine = new Color(0.38f, 0.37f, 0.42f), jewel = new Color(0.9f, 0.05f, 0.12f);
        Each((u, v, x, y, i) => {
            float e = Plates(u, v, 2, out int id, out float lx, out float ly);
            Color c = iron * (0.85f + 0.25f * Hash(id, 1, 221)) * Mathf.Lerp(0.85f, 1.1f, Noise(u, v, 16, 222, 3));
            float cx = lx - 0.25f, cy = ly - 0.25f, r = Mathf.Sqrt(cx * cx + cy * cy);
            float q = Mathf.Abs(Quatrefoil(cx, cy));
            Color glow = Color.black;
            if (q < 0.007f || Mathf.Abs(e - 0.04f) < 0.005f) c = shine * (cy > 0f ? 1.1f : 0.85f);
            else if (q < 0.012f) c *= 0.5f;
            if (r < 0.03f) {
                float k = 1f - r / 0.03f;
                c = Color.Lerp(jewel * 0.6f, Color.Lerp(jewel, Color.white, 0.3f), k);
                glow = jewel * (0.5f + 0.5f * k);
            }
            if (e < 0.006f) c = iron * 0.3f;
            else if (e < 0.016f) c = Color.Lerp(c, shine, 0.35f);
            float rx = Mathf.Min(lx, 0.5f - lx) - 0.022f, ry = Mathf.Min(ly, 0.5f - ly) - 0.022f;   // rivets in the corners
            if (rx * rx + ry * ry < 0.011f * 0.011f) c = shine * (ly > 0.25f ? 1.15f : 0.75f);
            l.albedo[i] = Opaque(c);
            l.glow[i] = Opaque(glow);
        });
        return l;
    }

    // Blood-red, crimson and dark purple cells in a black lead net, with a pointed lancet (arch, mullion, sill
    // and a round light in its head) leaded into every metre; the glass around the lancet is darker.
    static Look BloodGlass() {
        Look l = new Look { smooth = 0.9f, glass = true };
        Color[] palette = {
            new Color(0.62f, 0.02f, 0.08f), new Color(0.82f, 0.07f, 0.13f), new Color(0.45f, 0.02f, 0.12f),
            new Color(0.3f, 0.03f, 0.16f), new Color(0.95f, 0.35f, 0.12f),
        };
        Each((u, v, x, y, i) => {
            float border = Cells(u, v, 4, 231, out int cell, out float d1);
            int pick = Hash(cell, 2, 232) < 0.08f ? 4 : (int)(Hash(cell, 1, 233) * 3.999f);   // the odd amber cell
            Color c = palette[pick] * Mathf.Lerp(1.15f, 0.8f, Mathf.Clamp01(d1 * 1.6f));
            bool inside = Lancet(u, v, out float lead);
            if (!inside) c *= 0.45f;
            c.a = inside ? 0.62f : 0.8f;
            c = Color.Lerp(c, new Color(1f, 0.8f, 0.8f, 0.75f), Streak(u, v) * 0.35f);
            if (border < 0.03f || lead < 0.012f || Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) < 0.012f)
                c = new Color(0.05f, 0.03f, 0.04f, 1f);
            l.albedo[i] = c;
        });
        return l;
    }

    // ------------------------------------------------------------------ patterns
    // A lancet 0.6 m wide: its sides rise from a sill at 0.06 to the springing line at 0.45, where two arcs of
    // radius 0.6 meet in a point at ~0.97; a mullion runs up the middle into a round light. Returns whether (u, v)
    // is inside the arch and the distance to its nearest lead line.
    static bool Lancet(float u, float v, out float lead) {
        const float L = 0.2f, R = 0.8f, Sill = 0.06f, Spring = 0.45f, Radius = 0.6f;
        float dl = Mathf.Sqrt((u - L) * (u - L) + (v - Spring) * (v - Spring));   // from the left springing point
        float dr = Mathf.Sqrt((u - R) * (u - R) + (v - Spring) * (v - Spring));
        bool inside = v > Sill && u > L && u < R && (v < Spring || (dl < Radius && dr < Radius));
        lead = Mathf.Abs(v - Sill) + (u < L || u > R ? 1f : 0f);
        if (v > Sill && v < Spring) lead = Mathf.Min(lead, Mathf.Min(Mathf.Abs(u - L), Mathf.Abs(u - R)));
        if (v >= Spring && u >= 0.5f) lead = Mathf.Min(lead, Mathf.Abs(dl - Radius));   // the right arc is centred on the left point
        if (v >= Spring && u < 0.5f) lead = Mathf.Min(lead, Mathf.Abs(dr - Radius));
        if (v > Sill && v < 0.68f) lead = Mathf.Min(lead, Mathf.Abs(u - 0.5f));
        float ro = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.78f) * (v - 0.78f));
        lead = Mathf.Min(lead, Mathf.Abs(ro - 0.09f));
        return inside;
    }

    // Signed distance to a quatrefoil (four overlapping circles around the centre), in metres.
    static float Quatrefoil(float x, float y) {
        const float Offset = 0.07f, Radius = 0.075f;
        float d = float.MaxValue;
        for (int k = 0; k < 4; k++) {
            float a = k * Mathf.PI * 0.5f;
            float dx = x - Mathf.Cos(a) * Offset, dy = y - Mathf.Sin(a) * Offset;
            d = Mathf.Min(d, Mathf.Sqrt(dx * dx + dy * dy) - Radius);
        }
        return d;
    }

    delegate void PixelFn(float u, float v, int x, int y, int i);

    static void Each(PixelFn fn) {
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                fn((x + 0.5f) / S, (y + 0.5f) / S, x, y, y * S + x);
    }

    // Running bond: rows x cols bricks per metre (rows even, so the tile repeats), odd rows shifted half a brick.
    // Returns the distance (m) to the brick's nearest edge; fy = height inside the brick (0..1).
    static float Bond(float u, float v, int rows, int cols, out int id, out float fy) {
        float rv = v * rows;
        int row = Mathf.FloorToInt(rv);
        fy = rv - row;
        float cu = u * cols + (row % 2) * 0.5f;
        int col = Mathf.FloorToInt(cu);
        float fx = cu - col;
        col %= cols;
        id = row * 31 + col;
        return Mathf.Min(Mathf.Min(fx, 1f - fx) / cols, Mathf.Min(fy, 1f - fy) / rows);
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

    // Tileable Voronoi cells (n x n jittered sites per metre): half the gap between the two nearest sites (the
    // border) in cell units, the nearest cell's id and the distance to its site.
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
                if (rng.NextDouble() < 0.04) ang += (rng.NextDouble() < 0.5 ? 1f : -1f) * 0.9f;   // kink
                x += Mathf.Cos(ang) * 1.5f;
                y += Mathf.Sin(ang) * 1.5f;
                int w = stage == 2 && i < len / 3 ? 2 : 1;
                for (int oy = -w; oy <= w; oy++)
                    for (int ox = -w; ox <= w; ox++) {
                        int k = (((int)y + oy) % S + S) % S * S + (((int)x + ox) % S + S) % S;
                        Color c = px[k];
                        if (glass) c = Color.Lerp(c, new Color(1f, 0.9f, 0.9f, 0.95f), 0.75f);
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

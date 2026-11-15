using UnityEngine;

// Dalí's fortress skin (same physics, his look), registered through ClassLooks (DaliLook):
//   Stone: warm sand-coloured stucco with soft runs of ochre glaze melting down it.
//   Metal: glazed tiles, ochre and sky-blue, each painted with a cream flower, in white grout.
//   Wood:  dark walnut panels in raised gilded frames with a gold baroque scroll in each; the gold glows softly.
//   Glass: sky-blue painted glass with soft white clouds in a thin gold frame.
// Textures are 256 x 256 and tile once per metre (MeshKit.Box gives faces UVs in metres). All three damage stages
// of a material are built together the first time a block asks for it and cached, so blocks share one material
// per material and stage. Stage 1 adds cracks, stage 2 more cracks and grime; the gold's glow fades as the wood
// breaks. Ice and powder kegs keep the neutral look (null).
public static class DaliSkins {
    const int S = 256;                                       // texels per metre
    static readonly float[] GlowByStage = { 1f, 0.65f, 0.35f };
    static readonly Material[][] sets = new Material[4][];   // Wood, Stone, Metal, Glass (BlockMaterial order)

    public static Material Block(BlockMaterial m, int stage) {
        if (m == BlockMaterial.Ice || m == BlockMaterial.Explosive || GameAssets.I == null) return null;
        int k = (int)m;
        if (sets[k] == null || sets[k][0] == null) sets[k] = Build(m);
        return sets[k][Mathf.Clamp(stage, 0, 2)];
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
        Look look = m == BlockMaterial.Stone ? Stucco() : m == BlockMaterial.Metal ? Tiles() : m == BlockMaterial.Wood ? GildedWood() : SkyGlass();
        string name = "Dali " + m;
        Texture2D glow = look.glow != null ? ToTexture(look.glow, name + " glow") : null;
        Material[] set = new Material[3];
        for (int stage = 0; stage < 3; stage++) {
            Color[] px = stage == 0 ? look.albedo : Damaged(look.albedo, stage, look.glass, 900 + (int)m * 7);
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

    // ================================================================== stone: melting stucco
    struct Run { public float u, top, length, width; }

    // Warm sand stucco smoothed by hand (broad trowel smears, fine grain, little pits), with soft runs of ochre
    // glaze melting down it, each ending in a bead.
    static Look Stucco() {
        Look l = new Look { smooth = 0.24f };
        Color sand = new Color(0.9f, 0.76f, 0.55f), glaze = new Color(0.84f, 0.56f, 0.28f);
        Run[] runs = Runs(11, 811);
        Each((u, v, x, y, i) => {
            float smear = Noise(u + Noise(u, v, 2, 812, 2) * 0.25f, v, 4, 813, 3);
            Color c = sand * Mathf.Lerp(0.88f, 1.06f, smear) * Mathf.Lerp(0.95f, 1.04f, Noise(u, v, 32, 814, 2));
            if (Hash(x, y, 815) > 0.992f) c *= 0.84f;
            float cover = Glaze(runs, u, v, out float light);
            if (cover > 0f) c = Color.Lerp(c, glaze * light, cover * 0.85f);
            l.albedo[i] = Opaque(c);
        });
        return l;
    }

    static Run[] Runs(int n, int seed) {
        Run[] runs = new Run[n];
        for (int k = 0; k < n; k++) {
            float length = 0.12f + 0.3f * Hash(k, 1, seed);
            runs[k] = new Run {
                u = Hash(k, 2, seed), length = length,
                top = length + 0.06f + (0.92f - length - 0.06f) * Hash(k, 3, seed),   // the whole run stays inside the tile
                width = 0.012f + 0.016f * Hash(k, 4, seed),
            };
        }
        return runs;
    }

    // How much of (u, v) a glaze run covers (soft edged) and its shading: lit from the left, a glint down the middle.
    static float Glaze(Run[] runs, float u, float v, out float light) {
        float best = 0f;
        light = 1f;
        foreach (Run r in runs) {
            float dx = u - r.u;
            dx -= Mathf.Round(dx);                                  // the tile repeats sideways
            if (Mathf.Abs(dx) > 0.06f) continue;
            float along = (r.top - v) / r.length;                   // 0 at the top, 1 at the bead
            if (along < -0.2f || along > 1.25f) continue;
            float w = r.width * Mathf.Lerp(1.1f, 0.6f, Mathf.Clamp01(along));
            float body = along < 0f ? Mathf.Sqrt(dx * dx + (v - r.top) * (v - r.top)) - w : Mathf.Abs(dx) - w;
            float by = v - (r.top - r.length);
            float bead = Mathf.Sqrt(dx * dx + by * by) - r.width * 1.25f;
            float d = Mathf.Min(along <= 1f ? body : float.MaxValue, bead);
            float cover = Mathf.Clamp01(0.5f - d / 0.005f);
            if (cover <= best) continue;
            best = cover;
            float across = Mathf.Clamp(dx / (w + 1e-4f), -1f, 1f);
            light = 1.1f - 0.14f * (across + 1f) + 0.12f * Mathf.Exp(-(across + 0.35f) * (across + 0.35f) * 18f);
        }
        return best;
    }

    // ================================================================== metal: painted tiles
    // Glazed tiles, four by four per metre, ochre and sky-blue in a chequer, each painted with a cream four-petal
    // flower and corner rings that join into circles across the tiles; cobalt outlines, white grout.
    static Look Tiles() {
        Look l = new Look { smooth = 0.75f, metal = 0.05f };
        Color ochre = new Color(0.88f, 0.6f, 0.17f), blue = new Color(0.42f, 0.69f, 0.92f);
        Color cream = new Color(0.98f, 0.94f, 0.83f), cobalt = new Color(0.17f, 0.25f, 0.52f), grout = new Color(0.93f, 0.9f, 0.84f);
        Each((u, v, x, y, i) => {
            float e = Plates(u, v, 4, out int id, out float lx, out float ly);
            bool warm = (((int)(u * 4f) + (int)(v * 4f)) & 1) == 0;
            Color c = (warm ? ochre : blue) * (0.94f + 0.1f * Hash(id, 1, 821));
            float px = lx * 4f - 0.5f, py = ly * 4f - 0.5f;                     // -0.5..0.5 across the tile
            float r = Mathf.Sqrt(px * px + py * py), a = Mathf.Atan2(py, px);
            float petal = r - (0.13f + 0.11f * Mathf.Abs(Mathf.Cos(2f * a)));
            float cx = 0.5f - Mathf.Abs(px), cy = 0.5f - Mathf.Abs(py);
            float corner = Mathf.Sqrt(cx * cx + cy * cy) - 0.2f;
            if (petal < 0f) c = Color.Lerp(cream, warm ? blue : ochre, Mathf.Clamp01(1f - r / 0.07f));
            if (Mathf.Abs(corner) < 0.035f) c = cream;
            if (Mathf.Abs(petal) < 0.014f || Mathf.Abs(Mathf.Abs(corner) - 0.035f) < 0.01f) c = cobalt;
            c *= Mathf.Lerp(0.93f, 1.07f, Mathf.Clamp01(0.5f + py * 0.6f - px * 0.3f));     // the glaze catches the light
            if (e < 0.005f) c = grout;
            else if (e < 0.011f) c *= 0.88f;
            l.albedo[i] = Opaque(c);
        });
        return l;
    }

    // ================================================================== wood: gilded panels
    // Dark walnut panels (two by two per metre) in raised gilded frames, each with a gold baroque ornament: a stem,
    // a lozenge and four curls. The gold glows softly.
    static Look GildedWood() {
        Look l = new Look { smooth = 0.42f, glow = new Color[S * S], glowColor = new Color(0.6f, 0.6f, 0.6f) };
        Color dark = new Color(0.2f, 0.11f, 0.06f), light = new Color(0.4f, 0.24f, 0.12f), gold = new Color(1f, 0.78f, 0.32f);
        Each((u, v, x, y, i) => {
            float e = Plates(u, v, 2, out int id, out float lx, out float ly);
            float grain = TexKit.Fbm(u * 32f, v * 4f, 4, 831, 3);
            Color c = Color.Lerp(dark, light, 0.25f + 0.55f * grain) * (0.9f + 0.15f * Hash(id, 1, 832));
            float glow = 0f;
            if (e < 0.008f) c = dark * 0.45f;                                     // the gap between panels
            else if (e < 0.042f) { c = gold * Mathf.Lerp(1.15f, 0.72f, (e - 0.008f) / 0.034f); glow = 1f; }   // bevelled gilded frame
            else if (e < 0.05f) c *= 0.55f;                                       // its shadow inside
            else {
                float d = Ornament(Mathf.Abs(lx - 0.25f), ly - 0.25f);
                if (d < 0.0055f) { c = gold * (ly > 0.25f ? 1.05f : 0.9f); glow = 0.85f; }
                else if (d < 0.009f) c *= 0.7f;                                   // the carving's shadow
            }
            l.albedo[i] = Opaque(c);
            l.glow[i] = Opaque(gold * glow);
        });
        return l;
    }

    static readonly Vector2[] Curl = BuildCurl();

    // A baroque curl: a spiral winding inwards one and a half turns.
    static Vector2[] BuildCurl() {
        Vector2[] p = new Vector2[22];
        for (int k = 0; k < p.Length; k++) {
            float t = k / (p.Length - 1f) * Mathf.PI * 3f;
            float r = 0.075f * Mathf.Exp(-0.28f * t);
            p[k] = new Vector2(0.085f + r * Mathf.Cos(t + 2.2f), 0.02f + r * Mathf.Sin(t + 2.2f));
        }
        return p;
    }

    // Distance (m) to the ornament's lines; x >= 0 (it is mirrored), (0, 0) = the panel centre.
    static float Ornament(float x, float y) {
        float d = Mathf.Abs(x + Mathf.Abs(y) - 0.035f);                         // lozenge
        d = Mathf.Min(d, Segment(x, y, 0f, -0.15f, 0f, -0.035f));                // stem
        d = Mathf.Min(d, Segment(x, y, 0f, 0.035f, 0f, 0.13f));
        float ay = Mathf.Abs(y);                                                  // curls above and below
        for (int k = 0; k + 1 < Curl.Length; k++)
            d = Mathf.Min(d, Segment(x, ay, Curl[k].x, Curl[k].y, Curl[k + 1].x, Curl[k + 1].y));
        return d;
    }

    // ================================================================== glass: painted sky
    // Sky-blue painted glass with soft white clouds and a thin gold frame.
    static Look SkyGlass() {
        Look l = new Look { smooth = 0.9f, glass = true };
        Color sky = new Color(0.45f, 0.72f, 0.95f, 0.5f), cloud = new Color(1f, 1f, 1f, 0.85f);
        Each((u, v, x, y, i) => {
            float puff = Mathf.Clamp01((Noise(u, v, 3, 841, 4) - 0.52f) * 5f);
            Color c = Color.Lerp(sky, cloud, puff * 0.9f);
            c = Color.Lerp(c, new Color(1f, 1f, 1f, 0.75f), Streak(u, v) * 0.45f);
            float e = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            if (e < 0.005f) c = new Color(0.74f, 0.57f, 0.22f, 1f);
            else if (e < 0.013f) c = new Color(0.98f, 0.76f, 0.3f, 1f);
            l.albedo[i] = c;
        });
        return l;
    }

    // ================================================================== helpers
    delegate void PixelFn(float u, float v, int x, int y, int i);

    static void Each(PixelFn fn) {
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                fn((x + 0.5f) / S, (y + 0.5f) / S, x, y, y * S + x);
    }

    // A grid of n x n square plates per metre: distance (m) to the plate's edge and the position inside it (m).
    static float Plates(float u, float v, int n, out int id, out float lx, out float ly) {
        int cx = Mathf.FloorToInt(u * n), cy = Mathf.FloorToInt(v * n);
        float size = 1f / n;
        lx = u - cx * size;
        ly = v - cy * size;
        id = cy * 7 + cx;
        return Mathf.Min(Mathf.Min(lx, size - lx), Mathf.Min(ly, size - ly));
    }

    static float Segment(float px, float py, float ax, float ay, float bx, float by) { return DaliArt.Segment(px, py, ax, ay, bx, by); }

    // A soft diagonal glint for glass.
    static float Streak(float u, float v) {
        float s = u + v;
        return Mathf.Clamp01(1f - Mathf.Abs(s - 0.7f) * 12f) + Mathf.Clamp01(1f - Mathf.Abs(s - 0.86f) * 30f);
    }

    // Tileable noise: (u, v) scaled by integer multiples of `cells` keep the tile seamless.
    static float Noise(float u, float v, int cells, int seed, int octaves) { return TexKit.Fbm(u * cells, v * cells, cells, seed, octaves); }

    static float Hash(int x, int y, int seed) { return TexKit.Hash(x, y, seed); }

    static Color Opaque(Color c) { c.a = 1f; return c; }

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
                if (rng.NextDouble() < 0.04) ang += (rng.NextDouble() < 0.5 ? 1f : -1f) * 0.9f;   // a kink
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

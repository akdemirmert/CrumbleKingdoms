using UnityEngine;

// Block skins of the Gambler's fortress (the same physics, a casino look):
//   Stone: green baize in gold-railed panels, like a card table; each panel has a gold card suit printed inside a thin
//          gold betting line (spades, hearts, diamonds, clubs).
//   Wood:  polished dark mahogany planks, a thin gold inlay along the middle of each, studded with small gold diamonds.
//   Metal: brass plates with raised rims and an engraved diamond riveted at its points and sides, a neon-pink diamond
//          glowing in the middle.
//   Glass: harlequin diamonds of neon-pink and ruby glass in a gold lead net.
// The God already has white marble with gold veins and Dali gilded walnut panels, so the Gambler's stone is the card
// table and his gold stays in thin lines. Like ClassSkins: 256 x 256 textures that tile once per metre, all three
// damage stages made together the first time a material is asked for, one cached material per material and stage; the
// glow fades as a block cracks. Ice and powder kegs keep the neutral look (null).
public static class GamblerSkins {
    const int S = 256;                                      // texels per metre
    static readonly float[] GlowByStage = { 1f, 0.6f, 0.3f };
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
        Look look = m == BlockMaterial.Stone ? Baize() : m == BlockMaterial.Wood ? Mahogany()
                  : m == BlockMaterial.Metal ? Brass() : Harlequin();
        string name = "Gambler " + m;
        Texture2D glow = look.glow != null ? ToTexture(look.glow, name + " glow") : null;
        Material[] set = new Material[3];
        for (int stage = 0; stage < 3; stage++) {
            Color[] px = stage == 0 ? look.albedo : Damaged(look.albedo, stage, look.glass, 90 + (int)m * 7);
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
    // Green baize in panels (2 x 2 per metre) framed by rounded gold rails: fine fibres and a little mottling, a shadow
    // on the felt along the rails, and in each panel a gold suit printed inside a thin gold betting line.
    static Look Baize() {
        Look l = new Look { smooth = 0.06f };
        Color print = new Color(0.86f, 0.7f, 0.3f);
        Each((u, v, x, y, i) => {
            float e = Plates(u, v, 2, out int id, out float lx, out float ly);
            int suit = Mathf.FloorToInt(u * 2f) + 2 * Mathf.FloorToInt(v * 2f);   // the four suits, one per panel
            Color c = GamblerKit.Felt * (0.92f + 0.14f * Hash(id, 1, 501)) * Mathf.Lerp(0.86f, 1.08f, Hash(x, y, 502));
            c *= Mathf.Lerp(0.88f, 1.06f, Noise(u, v, 8, 503, 3));
            if (Mathf.Abs(e - 0.075f) < 0.0045f || InSuit(suit, lx - 0.25f, ly - 0.25f, 0.14f)) c = Color.Lerp(c, print, 0.8f);
            if (e < 0.006f) c = GamblerKit.Gold * 0.3f;                                                          // between rails
            else if (e < 0.026f) c = GamblerKit.Gold * (0.7f + 0.5f * Mathf.Sin((e - 0.006f) / 0.02f * Mathf.PI));   // a rounded rail
            else if (e < 0.036f) c *= 0.6f;                                                                     // its shadow
            l.albedo[i] = Opaque(c);
        });
        return l;
    }

    // Polished dark mahogany planks (four per metre) with fine grain and dark joints; a thin gold inlay runs along the
    // middle of each, studded every half metre with a small gold diamond (staggered from plank to plank).
    static Look Mahogany() {
        Look l = new Look { smooth = 0.5f };
        Color dark = new Color(0.16f, 0.045f, 0.03f), light = new Color(0.42f, 0.14f, 0.07f), gold = GamblerKit.Gold * 0.92f;
        Each((u, v, x, y, i) => {
            int plank = Mathf.FloorToInt(v * 4f);
            float py = v * 4f - plank, shift = Hash(plank, 9, 511);
            float grain = Noise(Frac(u + shift), v * 16f, 4, 512 + plank, 3);
            float rings = 0.5f + 0.5f * Mathf.Sin((grain * 10f + py * 1.5f) * Mathf.PI);
            Color c = Color.Lerp(dark, light, 0.25f + 0.5f * rings) * (0.86f + 0.24f * Hash(plank, 3, 513));
            float joint = Mathf.Min(py, 1f - py) * 0.25f;                                          // metres to the joint
            if (joint < 0.004f) c = dark * 0.35f;
            else if (joint < 0.01f) c *= 0.82f;
            float dx = (Frac(u * 2f + (plank % 2) * 0.5f) - 0.5f) * 0.5f, dy = (py - 0.5f) * 0.25f;   // metres from a stud
            float stud = Mathf.Abs(dx) / 0.04f + Mathf.Abs(dy) / 0.055f;
            if (stud < 1f) c = gold * (dy > 0f ? 1.12f : 0.85f);                                     // lit from above
            else if (stud < 1.2f) c = dark * 0.5f;                                                   // its dark outline
            else if (Mathf.Abs(dy) < 0.0045f) c = gold * (dy > 0f ? 1.05f : 0.82f);                  // the inlay line
            l.albedo[i] = Opaque(c);
        });
        return l;
    }

    // Rivets on the engraved diamond of a brass plate: its four points and the middles of its sides (m from the centre).
    static readonly Vector2[] Rivets = {
        new Vector2(0.16f, 0f), new Vector2(-0.16f, 0f), new Vector2(0f, 0.16f), new Vector2(0f, -0.16f),
        new Vector2(0.08f, 0.08f), new Vector2(-0.08f, 0.08f), new Vector2(0.08f, -0.08f), new Vector2(-0.08f, -0.08f),
    };

    // Brass plates (2 x 2 per metre) with raised rims and a little tarnish; on each an engraved diamond with domed
    // rivets on it, and a neon-pink diamond glowing at its heart.
    static Look Brass() {
        Look l = new Look { smooth = 0.58f, metal = 0.6f, glow = new Color[S * S], glowColor = new Color(2.4f, 2.4f, 2.4f) };
        Color brass = new Color(0.8f, 0.6f, 0.27f), tarnish = new Color(0.42f, 0.34f, 0.17f), neon = GamblerKit.Neon;
        Each((u, v, x, y, i) => {
            float e = Plates(u, v, 2, out int id, out float lx, out float ly);
            Color c = brass * (0.9f + 0.15f * Hash(id, 1, 521)) * Mathf.Lerp(0.88f, 1.08f, Noise(u, v * 32f, 2, 522, 2));
            float stain = Noise(u, v, 6, 523, 3) - 0.56f;
            if (stain > 0f) c = Color.Lerp(c, tarnish, Mathf.Clamp01(stain * 2.2f));
            if (e < 0.006f) c = brass * 0.3f;
            else if (e < 0.016f) c *= 1.22f;
            float dx = lx - 0.25f, dy = ly - 0.25f;
            if (Mathf.Abs(Mathf.Abs(dx) + Mathf.Abs(dy) - 0.16f) < 0.005f) c *= 0.55f;              // the engraved diamond
            foreach (Vector2 r in Rivets) {
                float rx = dx - r.x, ry = dy - r.y, r2 = rx * rx + ry * ry;
                if (r2 < 0.012f * 0.012f) c = brass * (ry > 0f ? 1.3f : 0.72f);                       // lit from above
                else if (r2 < 0.017f * 0.017f && ry < 0f) c *= 0.65f;                                 // its shadow
            }
            Color glow = Color.black;
            float gem = Mathf.Abs(dx) / 0.045f + Mathf.Abs(dy) / 0.065f;
            if (gem < 1f) {
                float k = 1f - gem;
                c = Color.Lerp(neon * 0.55f, Color.Lerp(neon, Color.white, 0.4f), k);
                glow = neon * (0.55f + 0.45f * k);
            }
            l.albedo[i] = Opaque(c);
            l.glow[i] = Opaque(glow);
        });
        return l;
    }

    // Harlequin glass: diamonds a quarter metre wide and half a metre tall, neon pink and ruby in turn, brighter in the
    // middle, in a gold lead net with a gold frame at the edge of every metre, and a soft glint.
    static Look Harlequin() {
        Look l = new Look { smooth = 0.9f, glass = true };
        Color pink = new Color(1f, 0.36f, 0.66f, 0.48f), ruby = new Color(0.72f, 0.04f, 0.18f, 0.58f);
        Color lead = new Color(0.95f, 0.72f, 0.28f, 1f);
        Each((u, v, x, y, i) => {
            float a = u * 4f + v * 2f, b = u * 4f - v * 2f + 8f;   // whole steps per metre: the tile stays seamless
            int da = Mathf.FloorToInt(a), db = Mathf.FloorToInt(b);
            float net = Mathf.Min(Mathf.Min(a - da, 1f - (a - da)), Mathf.Min(b - db, 1f - (b - db))) / 4.472f;   // metres to the lead
            Color c = (da + db) % 2 == 0 ? pink : ruby;
            float k = Mathf.Lerp(0.82f, 1.12f, Mathf.Clamp01(net * 16f)) * Mathf.Lerp(0.9f, 1.08f, Noise(u, v, 4, 531, 2));
            c = new Color(c.r * k, c.g * k, c.b * k, c.a);
            c = Color.Lerp(c, new Color(1f, 0.9f, 0.95f, 0.75f), Streak(u, v) * 0.45f);
            if (net < 0.006f || Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) < 0.012f) c = lead;
            l.albedo[i] = c;
        });
        return l;
    }

    // ------------------------------------------------------------------ patterns
    // Whether (x, y) (m, from the symbol's centre) lies inside a card suit `s` tall, in CardSuit order: 0 spades,
    // 1 hearts, 2 diamonds, 3 clubs. Lobes are circles and points triangles, as in GamblerKit.Suit. (Also paints the
    // pennants of GamblerDecor.)
    internal static bool InSuit(int suit, float x, float y, float s) {
        x = Mathf.Abs(x) / s;   // every suit is the same left and right
        y /= s;
        switch (suit) {
            case 1:   // two lobes over a point
                return Disc(x, y, 0.21f, 0.15f, 0.27f) || (y < 0.15f && y > -0.5f && x < (y + 0.5f) * 0.74f);
            case 2:
                return x / 0.36f + Mathf.Abs(y) / 0.5f < 1f;
            case 3:   // three lobes on a stem
                return Disc(x, y, 0f, 0.22f, 0.2f) || Disc(x, y, 0.22f, -0.06f, 0.2f) || Disc(x, y, 0f, 0f, 0.12f) || Stem(x, y);
            default:  // a point over two lobes, on a stem
                return Disc(x, y, 0.21f, -0.08f, 0.25f) || (y > -0.08f && y < 0.5f && x < (0.5f - y) * 0.8f) || Stem(x, y);
        }
    }

    static bool Disc(float x, float y, float cx, float cy, float r) { return (x - cx) * (x - cx) + (y - cy) * (y - cy) < r * r; }

    static bool Stem(float x, float y) { return y > -0.5f && y < -0.1f && x < 0.04f + (-0.1f - y) * 0.3f; }   // flares at the foot

    delegate void PixelFn(float u, float v, int x, int y, int i);

    static void Each(PixelFn fn) {
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                fn((x + 0.5f) / S, (y + 0.5f) / S, x, y, y * S + x);
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
                        if (glass) c = Color.Lerp(c, new Color(1f, 0.92f, 0.96f, 0.95f), 0.75f);
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

using System.Collections.Generic;
using UnityEngine;

// Procedural textures (generated once, cached). Block textures cover 1 x 1 metre and tile, because
// MeshKit.Box gives faces UVs in metres.
public static class TexKit {
    static Texture2D softCircle, smoke, spark, ring, star, chip, noise, white;
    static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

    public static Texture2D SoftCircle => softCircle ??= Make(64, 64, false, TextureWrapMode.Clamp, (u, v) => {
        float r = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f);
        float a = 1f - r; return new Color(1f, 1f, 1f, a * a);
    });

    public static Texture2D Smoke => smoke ??= Make(128, 128, false, TextureWrapMode.Clamp, (u, v) => {
        float r = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f);
        float n = Fbm(u * 4f, v * 4f, 4, 17, 4);
        float a = Mathf.Clamp01((1f - r) * 1.6f - 0.25f) * Mathf.Lerp(0.55f, 1f, n);
        return new Color(1f, 1f, 1f, a * a);
    });

    public static Texture2D Spark => spark ??= Make(64, 32, false, TextureWrapMode.Clamp, (u, v) => {
        float dx = Mathf.Abs(u - 0.5f) * 2f, dy = Mathf.Abs(v - 0.5f) * 2f;
        float a = Mathf.Clamp01(1f - dx) * Mathf.Clamp01(1f - dy * dy * 1.2f);
        return new Color(1f, 1f, 1f, a * a);
    });

    public static Texture2D Ring => ring ??= Make(128, 128, false, TextureWrapMode.Clamp, (u, v) => {
        float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
        float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.85f) / 0.12f);
        return new Color(1f, 1f, 1f, a * a);
    });

    public static Texture2D Star => star ??= Make(64, 64, false, TextureWrapMode.Clamp, (u, v) => {
        float x = Mathf.Abs(u - 0.5f) * 2f, y = Mathf.Abs(v - 0.5f) * 2f;
        float cross = Mathf.Clamp01(1f - x * 7f) * Mathf.Clamp01(1f - y) + Mathf.Clamp01(1f - y * 7f) * Mathf.Clamp01(1f - x);
        float core = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) * 2.2f);
        float a = Mathf.Clamp01(cross + core * core);
        return new Color(1f, 1f, 1f, a);
    });

    public static Texture2D Chip => chip ??= Make(32, 32, false, TextureWrapMode.Clamp, (u, v) => {
        float e = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
        return new Color(1f, 1f, 1f, Mathf.Clamp01(e * 10f));
    });

    public static Texture2D Noise => noise ??= Make(256, 256, true, TextureWrapMode.Repeat, (u, v) => {
        float n = Fbm(u * 8f, v * 8f, 8, 3, 4);
        return new Color(n, n, n, 1f);
    });

    public static Texture2D White => white ??= Make(4, 4, false, TextureWrapMode.Clamp, (u, v) => Color.white);

    // Vertical gradient (bottom -> top) through the given colours, evenly spaced.
    public static Texture2D Gradient(params Color[] colors) {
        string key = "grad";
        foreach (Color c in colors) key += ColorUtility.ToHtmlStringRGBA(c);
        if (cache.TryGetValue(key, out Texture2D t) && t != null) return t;
        t = Make(4, 256, false, TextureWrapMode.Clamp, (u, v) => {
            float f = v * (colors.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), colors.Length - 2);
            return Color.Lerp(colors[i], colors[i + 1], Mathf.SmoothStep(0f, 1f, f - i));
        });
        cache[key] = t;
        return t;
    }

    // ------------------------------------------------------------------ UI helpers
    public static Texture2D RoundedRect(int size, int radius) {
        string key = "rr" + size + "_" + radius;
        if (cache.TryGetValue(key, out Texture2D t) && t != null) return t;
        t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = key };
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) {
                float dx = Mathf.Max(0f, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                float dy = Mathf.Max(0f, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.5f));
            }
        t.SetPixels(px);
        t.Apply();
        cache[key] = t;
        return t;
    }

    public static Sprite ToSprite(Texture2D t, int border = 0) {
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    // ------------------------------------------------------------------ block textures
    // stage 0 = intact, 1 = cracked, 2 = badly cracked
    public static Texture2D Block(BlockMaterial m, int stage) {
        string key = "blk" + (int)m + "_" + stage;
        if (cache.TryGetValue(key, out Texture2D t) && t != null) return t;
        const int S = 256;
        Color[] px = new Color[S * S];
        switch (m) {
            case BlockMaterial.Wood: Wood(px, S, WeaponDefs.Hex("B07A44"), WeaponDefs.Hex("7A4F27")); break;
            case BlockMaterial.Stone: Stone(px, S); break;
            case BlockMaterial.Metal: Metal(px, S); break;
            case BlockMaterial.Glass: Glass(px, S); break;
            case BlockMaterial.Ice: Ice(px, S); break;
            case BlockMaterial.Explosive: Keg(px, S); break;
        }
        if (stage > 0) Cracks(px, S, stage, 11 + (int)m * 7, m == BlockMaterial.Glass || m == BlockMaterial.Ice);
        t = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 2, name = key };
        t.SetPixels(px);
        t.Apply(true);
        cache[key] = t;
        return t;
    }

    static void Wood(Color[] px, int S, Color light, Color dark) {
        int planks = 4, ph = S / planks;
        for (int y = 0; y < S; y++) {
            int p = y / ph;
            float py = (y % ph) / (float)ph;
            float tone = 0.85f + 0.3f * Hash(p, 3, 5);
            int shift = (int)(Hash(p, 9, 2) * S);
            for (int x = 0; x < S; x++) {
                float xs = ((x + shift) % S) / (float)S;
                float grain = Fbm(xs * 2f, py * 0.9f + p * 3.1f, 2, 4 + p, 3);
                float rings = 0.5f + 0.5f * Mathf.Sin((grain * 9f + py * 2f) * Mathf.PI);
                Color c = Color.Lerp(dark, light, 0.35f + 0.45f * rings) * tone;
                float edge = Mathf.Min(py, 1f - py) * ph;
                if (edge < 2.2f) c = Color.Lerp(c, dark * 0.45f, 0.75f);          // gap between planks
                else if (edge < 4f) c *= 0.88f;
                // nails near both ends of the board
                float nx = ((x + shift) % (S / 2)) / (float)(S / 2);
                if (Mathf.Abs(nx - 0.08f) < 0.016f && Mathf.Abs(py - 0.5f) < 0.07f) c = WeaponDefs.Hex("3A3532");
                c.a = 1f;
                px[y * S + x] = c;
            }
        }
    }

    static void Stone(Color[] px, int S) {
        int rows = 4, rh = S / rows, bw = S / 2;
        Color mortar = WeaponDefs.Hex("6E6A63");
        for (int y = 0; y < S; y++) {
            int r = y / rh;
            float ry = (y % rh);
            int off = (r % 2) * (bw / 2);
            for (int x = 0; x < S; x++) {
                int bx = (x + off) / bw;
                float rx = (x + off) % bw;
                float tone = 0.78f + 0.3f * Hash(bx + r * 13, r, 7);
                Color stone = WeaponDefs.Hex("A8A49B") * tone;
                float n = Fbm(x / (float)S * 6f, y / (float)S * 6f, 6, 9, 3);
                Color c = Color.Lerp(stone * 0.85f, stone * 1.08f, n);
                float e = Mathf.Min(Mathf.Min(rx, bw - rx), Mathf.Min(ry, rh - ry));
                if (e < 3f) c = mortar * (0.85f + 0.2f * n);
                else if (e < 7f) c *= Mathf.Lerp(0.82f, 1f, (e - 3f) / 4f);     // bevel
                if (Hash(x, y, 31) > 0.985f) c *= 0.8f;                            // speckles
                c.a = 1f;
                px[y * S + x] = c;
            }
        }
    }

    static void Metal(Color[] px, int S) {
        int cell = S / 2;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++) {
                int cx = x / cell, cy = y / cell;
                float lx = x % cell, ly = y % cell;
                float tone = 0.9f + 0.15f * Hash(cx, cy, 4);
                float brush = Fbm(x / (float)S * 1.5f, y / (float)S * 30f, 2, 5, 2);
                Color c = WeaponDefs.Hex("7D8A96") * tone * Mathf.Lerp(0.9f, 1.07f, brush);
                float e = Mathf.Min(Mathf.Min(lx, cell - lx), Mathf.Min(ly, cell - ly));
                if (e < 3f) c *= 0.55f;
                else if (e < 6f) c *= 1.12f;
                Vector2 q = new Vector2(Mathf.Min(lx, cell - lx), Mathf.Min(ly, cell - ly));
                float rd = Vector2.Distance(q, new Vector2(14f, 14f));
                if (rd < 6f) c = Color.Lerp(WeaponDefs.Hex("C9D2DA"), WeaponDefs.Hex("4C565F"), rd / 6f);   // rivets
                c.a = 1f;
                px[y * S + x] = c;
            }
    }

    static void Glass(Color[] px, int S) {
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++) {
                float u = x / (float)S, v = y / (float)S;
                float e = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                Color c = new Color(0.72f, 0.9f, 0.98f, 0.32f);
                float streak = Mathf.Clamp01(1f - Mathf.Abs((u + v) - 0.75f) * 14f) + Mathf.Clamp01(1f - Mathf.Abs((u + v) - 0.95f) * 30f);
                c = Color.Lerp(c, new Color(1f, 1f, 1f, 0.75f), streak * 0.7f);
                if (e < 0.035f) c = new Color(0.32f, 0.42f, 0.5f, 1f);           // lead frame
                px[y * S + x] = c;
            }
    }

    static void Ice(Color[] px, int S) {
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++) {
                float u = x / (float)S, v = y / (float)S;
                float n = Fbm(u * 5f, v * 5f, 5, 21, 4);
                float vein = Mathf.Abs(Fbm(u * 3f, v * 3f, 3, 8, 3) - 0.5f);
                Color c = Color.Lerp(new Color(0.78f, 0.92f, 1f, 0.72f), new Color(0.95f, 0.99f, 1f, 0.85f), n);
                if (vein < 0.02f) c = new Color(1f, 1f, 1f, 0.95f);
                px[y * S + x] = c;
            }
    }

    static void Keg(Color[] px, int S) {
        Wood(px, S, WeaponDefs.Hex("A0402E"), WeaponDefs.Hex("5E2018"));
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++) {
                float u = x / (float)S, v = y / (float)S;
                Color c = px[y * S + x];
                if (Mathf.Abs(v - 0.18f) < 0.05f || Mathf.Abs(v - 0.82f) < 0.05f) c = WeaponDefs.Hex("2B2B2B");   // iron hoops
                float d = Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f);                                          // warning diamond
                if (d < 0.22f) c = WeaponDefs.Hex("F5C518");
                if (d < 0.22f && d > 0.19f) c = WeaponDefs.Hex("1E1E1E");
                if (d < 0.19f && Mathf.Abs(u - 0.5f) < 0.022f && v > 0.44f && v < 0.6f) c = WeaponDefs.Hex("1E1E1E"); // "!"
                if (d < 0.19f && Mathf.Abs(u - 0.5f) < 0.022f && Mathf.Abs(v - 0.39f) < 0.022f) c = WeaponDefs.Hex("1E1E1E");
                c.a = 1f;
                px[y * S + x] = c;
            }
    }

    static void Cracks(Color[] px, int S, int stage, int seed, bool clear) {
        System.Random rng = new System.Random(seed + stage * 101);
        int lines = stage == 1 ? 3 : 8;
        for (int l = 0; l < lines; l++) {
            float x = (float)rng.NextDouble() * S, y = (float)rng.NextDouble() * S;
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            int len = 40 + rng.Next(stage == 1 ? 60 : 120);
            for (int i = 0; i < len; i++) {
                ang += ((float)rng.NextDouble() - 0.5f) * 0.9f;
                x += Mathf.Cos(ang) * 1.5f; y += Mathf.Sin(ang) * 1.5f;
                int w = stage == 2 && i < len / 3 ? 2 : 1;
                for (int oy = -w; oy <= w; oy++)
                    for (int ox = -w; ox <= w; ox++) {
                        int px0 = ((int)x + ox + S * 4) % S, py0 = ((int)y + oy + S * 4) % S;
                        Color c = px[py0 * S + px0];
                        float k = (ox == 0 && oy == 0) ? 0.25f : 0.6f;
                        if (clear) { c = Color.Lerp(c, new Color(1f, 1f, 1f, 0.95f), 0.8f); }
                        else { float a = c.a; c *= k; c.a = a; }
                        px[py0 * S + px0] = c;
                    }
                if (rng.NextDouble() < 0.04) ang += (rng.NextDouble() < 0.5 ? 1f : -1f) * 0.9f; // branch kink
            }
        }
        if (stage == 2 && !clear)   // soot / grime
            for (int i = 0; i < px.Length; i++) {
                float n = Fbm((i % S) / (float)S * 3f, (i / S) / (float)S * 3f, 3, seed, 2);
                Color c = px[i]; float a = c.a; c *= Mathf.Lerp(0.72f, 0.95f, n); c.a = a; px[i] = c;
            }
    }

    // ------------------------------------------------------------------ noise
    public static float Hash(int x, int y, int seed) {
        unchecked {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)0x7fffffff;
        }
    }

    // Tileable value noise: x, y in cells; period = cells per tile.
    public static float ValueNoise(float x, float y, int period, int seed) {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        int xa = Mod(x0, period), xb = Mod(x0 + 1, period), ya = Mod(y0, period), yb = Mod(y0 + 1, period);
        float a = Mathf.Lerp(Hash(xa, ya, seed), Hash(xb, ya, seed), fx);
        float b = Mathf.Lerp(Hash(xa, yb, seed), Hash(xb, yb, seed), fx);
        return Mathf.Lerp(a, b, fy);
    }

    public static float Fbm(float x, float y, int period, int seed, int octaves) {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int o = 0; o < octaves; o++) {
            sum += ValueNoise(x, y, period, seed + o * 17) * amp;
            norm += amp; amp *= 0.5f; x *= 2f; y *= 2f; period *= 2;
        }
        return sum / norm;
    }

    static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

    delegate Color PixelFn(float u, float v);

    static Texture2D Make(int w, int h, bool mips, TextureWrapMode wrap, PixelFn fn) {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, mips) { wrapMode = wrap, filterMode = FilterMode.Bilinear };
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = fn((x + 0.5f) / w, (y + 0.5f) / h);
        t.SetPixels(px);
        t.Apply(mips);
        return t;
    }
}

using System;
using UnityEngine;

// Small, fast value noise for the procedural planet / nebula textures. Self-contained on purpose:
// the planets do not depend on TexKit's signatures.
// Textures are equirectangular: u wraps around the planet (0..1 = one full turn), v is the latitude.
public static class SpaceNoise {
    static float Hash(int x, int y, int seed) {
        unchecked {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / 16777216f;
        }
    }

    public static float Hash01(int x, int y, int seed) { return Hash(x, y, seed); }

    static int Wrap(int v, int period) {
        int m = v % period;
        return m < 0 ? m + period : m;
    }

    // Smooth value noise that repeats every `periodX` cells along x.
    static float Value(float x, float y, int periodX, int seed) {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        int xa = Wrap(x0, periodX), xb = Wrap(x0 + 1, periodX);
        float a = Hash(xa, y0, seed), b = Hash(xb, y0, seed);
        float c = Hash(xa, y0 + 1, seed), d = Hash(xb, y0 + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    // Fractal noise, 0..1 (mostly 0.25..0.75). `scale` = feature cells around the planet at the first octave.
    public static float Fbm(float u, float v, int scale, int octaves, int seed) {
        float sum = 0f, amp = 0.5f, norm = 0f;
        int cells = Mathf.Max(2, scale);
        for (int o = 0; o < octaves; o++) {
            sum += amp * Value(u * cells, v * cells * 0.5f, cells, seed + o * 31);
            norm += amp;
            amp *= 0.5f;
            cells *= 2;
        }
        return sum / norm;
    }

    // Hermite step: 0 at a, 1 at b (works for a > b as well, which gives a falling edge).
    public static float Smooth(float a, float b, float x) {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    public static Texture2D Make(string name, int w, int h, Func<float, float, Color> fn, bool repeatU = true) {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, true) {
            name = name, filterMode = FilterMode.Bilinear, anisoLevel = 4,
            wrapModeU = repeatU ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
            wrapModeV = TextureWrapMode.Clamp,
        };
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = fn((x + 0.5f) / w, (y + 0.5f) / h);
        t.SetPixels32(px);
        t.Apply(true);
        return t;
    }
}

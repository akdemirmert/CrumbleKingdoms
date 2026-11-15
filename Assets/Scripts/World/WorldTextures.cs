using System;
using System.Collections.Generic;
using UnityEngine;

// Procedural textures for the world, generated once (per theme where colours differ) and cached.
// Terrain textures tile seamlessly (TexKit noise is periodic); meshes carry UVs in metres and the
// material sets how many metres one tile covers (Tiled).
public static class WorldTextures {
    public const float CliffTile = 10f, TopTile = 4f, LipTile = 4f, LavaTile = 12f;
    public const float SkyDistance = 700f, SkyHeight = 900f;   // the sky gradient quad (see SkyBuilder)

    static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, Material> tiled = new Dictionary<string, Material>();

    static readonly Color[] RainbowBands = {
        new Color(0.55f, 0.3f, 0.95f), new Color(0.25f, 0.45f, 1f), new Color(0.3f, 0.9f, 0.45f),
        new Color(1f, 0.92f, 0.3f), new Color(1f, 0.6f, 0.2f), new Color(1f, 0.25f, 0.25f),
    };

    // ------------------------------------------------------------------ terrain
    // Cliff rock: wavy strata of different tones, darker seams, veins, fine grain.
    public static Texture2D Cliff(ArenaPalette p) {
        int seed = 11 + (int)p.theme * 7;
        return Make(Key("cliff", p), 256, 256, true, true, (u, v) => {
            float warp = TexKit.Fbm(u * 3f, v * 3f, 3, seed, 3) - 0.5f;
            float band = v * 5f + warp * 0.9f + 5f;                    // five strata per tile; +5 keeps it positive
            int index = Mathf.FloorToInt(band);
            float frac = band - index;
            float tone = TexKit.Hash(index % 5, 3, seed);
            Color c = Color.Lerp(Color.Lerp(p.rockDark, p.rockLight, 0.2f + 0.6f * tone), p.rock, 0.35f);
            c *= Mathf.Lerp(0.82f, 1.12f, TexKit.Fbm(u * 4f, v * 4f, 4, seed + 1, 4));
            float seam = Mathf.Min(frac, 1f - frac);
            if (seam < 0.05f) c = Color.Lerp(c, p.rockDark * 0.7f, 0.6f);
            else if (frac > 0.88f) c = p.theme == ArenaTheme.Frost ? Color.Lerp(c, Color.white, 0.55f) : c * 1.08f;   // ledge tops
            float crack = Mathf.Abs(TexKit.Fbm(u * 6f, v * 6f, 6, seed + 2, 3) * 2f - 1f);
            if (crack < 0.035f) c = Color.Lerp(c, p.vein, 0.75f);
            c *= Mathf.Lerp(0.9f, 1.06f, TexKit.Fbm(u * 24f, v * 24f, 24, seed + 3, 2));
            c.a = 1f;
            return c;
        });
    }

    // The walkable top: grass with flowers, rippled sand, sparkling snow, ash with embers, or marble slabs.
    public static Texture2D Top(ArenaPalette p) {
        int seed = 31 + (int)p.theme * 5;
        return Make(Key("top", p), 256, 256, true, true, (u, v) => {
            float n = TexKit.Fbm(u * 6f, v * 6f, 6, seed, 4);
            Color c = Color.Lerp(p.top, p.topDetail, n) * Mathf.Lerp(0.88f, 1.08f, TexKit.Fbm(u * 32f, v * 32f, 32, seed + 1, 2));
            float speck = TexKit.Hash((int)(u * 256f), (int)(v * 256f), seed + 9);
            switch (p.theme) {
                case ArenaTheme.Meadow:
                    if (speck > 0.996f) c = TexKit.Hash((int)(u * 256f), 7, seed) > 0.5f ? new Color(1f, 0.95f, 0.5f) : new Color(1f, 0.75f, 0.85f);
                    else if (speck < 0.05f) c *= 0.8f;
                    break;
                case ArenaTheme.Desert:
                    c *= 0.93f + 0.07f * Mathf.Sin((u * 10f + n * 1.5f) * Mathf.PI * 2f);
                    break;
                case ArenaTheme.Frost:
                    c = Color.Lerp(c, new Color(0.7f, 0.8f, 0.95f), (1f - n) * 0.2f);
                    if (speck > 0.993f) c = Color.white;
                    break;
                case ArenaTheme.Volcano:
                    if (speck > 0.995f) c = new Color(1f, 0.45f, 0.1f);
                    break;
                case ArenaTheme.Asgard:
                    float vein = Mathf.Abs(TexKit.Fbm(u * 3f, v * 3f, 3, seed + 4, 4) * 2f - 1f);
                    if (vein < 0.02f) c = Color.Lerp(c, p.vein, 0.8f);
                    float slab = Mathf.Min(Mathf.Abs(u * 2f - Mathf.Round(u * 2f)), Mathf.Abs(v * 2f - Mathf.Round(v * 2f)));
                    if (slab < 0.012f) c *= 0.82f;   // joints between marble slabs
                    break;
            }
            c.a = 1f;
            return c;
        });
    }

    // The turf edge hanging over the cliff: v = 1 top (the top layer, fringed downwards), soil below.
    public static Texture2D Lip(ArenaPalette p) {
        int seed = 51 + (int)p.theme * 3;
        return Make(Key("lip", p), 256, 64, true, false, (u, v) => {
            float spike = TexKit.Hash((int)(u * 128f), 0, seed);
            float boundary = 0.66f - 0.14f * TexKit.Fbm(u * 8f, 0.5f, 8, seed + 1, 3) - 0.12f * spike * spike;
            float n = TexKit.Fbm(u * 8f, v * 2f, 8, seed + 2, 3);
            Color c;
            if (v > boundary) {
                c = Color.Lerp(p.top, p.topDetail, n) * Mathf.Lerp(0.85f, 1.05f, (v - boundary) / (1f - boundary));
            } else {
                bool goldTrim = p.theme == ArenaTheme.Asgard;
                c = goldTrim ? (v > boundary - 0.14f ? p.soil : p.rockLight) : p.soil;
                c *= Mathf.Lerp(0.8f, 1.1f, n);
                if (!goldTrim && TexKit.Hash((int)(u * 64f), (int)(v * 16f), seed + 5) > 0.93f) c = Color.Lerp(c, p.rockLight, 0.6f);
                c *= Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(v / 0.25f));   // shaded underside
            }
            c.a = 1f;
            return c;
        });
    }

    // ------------------------------------------------------------------ water and lava
    // u tiles along x; v runs from the near edge of the sea (0) to the far edge (1), where it dissolves.
    public static Texture2D Sea(ArenaPalette p) {
        return Make(Key("sea", p), 64, 256, true, false, (u, v) => {
            Color c = Color.Lerp(p.seaNear, p.seaFar, Smooth(0.15f, 0.8f, v));
            float streak = TexKit.Fbm(u * 4f, v * 48f, 4, 71, 3);
            c = Color.Lerp(c, Color.Lerp(c, Color.white, 0.3f), Mathf.Clamp01((streak - 0.55f) * 2.2f) * (1f - v));
            float a = p.seaAlpha * Mathf.Lerp(1f, 1.08f, v);
            a *= 1f - Smooth(0.8f, 1f, v);
            c.a = Mathf.Clamp01(a);
            return c;
        });
    }

    // Surf along the cliffs: u tiles, v = 1 against the rock (dense) fading to 0 at the outer edge.
    public static Texture2D Foam => Make("foam", 128, 32, true, false, (u, v) => {
        float patch = TexKit.Fbm(u * 8f, v * 2f, 8, 81, 4);
        float bubble = TexKit.Hash((int)(u * 128f), (int)(v * 32f), 83) > 0.86f ? 0.6f : 0f;
        float a = Mathf.Clamp01(v * 1.25f - 0.25f + (patch - 0.5f) * 1.2f) + bubble * v;
        a *= Smooth(0f, 0.3f, v);
        return new Color(1f, 1f, 1f, Mathf.Clamp01(a) * 0.9f);
    });

    // Lava: a dark crust broken by glowing cracks. Crust = albedo, Glow = emission map.
    public static Texture2D LavaCrust => Make("lavaCrust", 256, 256, true, true, (u, v) => {
        float n = TexKit.Fbm(u * 8f, v * 8f, 8, 93, 3);
        Color crust = Color.Lerp(new Color(0.09f, 0.06f, 0.06f), new Color(0.24f, 0.14f, 0.1f), n);
        Color c = Color.Lerp(crust, new Color(1f, 0.6f, 0.2f), LavaCrack(u, v));
        c.a = 1f;
        return c;
    });

    public static Texture2D LavaGlow => Make("lavaGlow", 256, 256, true, true, (u, v) => {
        float g = Mathf.Clamp01(LavaCrack(u, v) + 0.1f * TexKit.Fbm(u * 5f, v * 5f, 5, 97, 3));
        return new Color(g, g * 0.85f, g * 0.6f, 1f);
    });

    static float LavaCrack(float u, float v) {
        float big = Mathf.Abs(TexKit.Fbm(u * 4f, v * 4f, 4, 91, 4) * 2f - 1f);    // ridged noise: ~0 along the cracks
        float small = Mathf.Abs(TexKit.Fbm(u * 9f, v * 9f, 9, 95, 3) * 2f - 1f);
        return Mathf.Clamp01(1f - Mathf.Min(big / 0.09f, small / 0.05f + 0.3f));
    }

    // ------------------------------------------------------------------ sky
    // Vertical gradient of the sky quad: each row gets the colour of its elevation angle as seen from the camera.
    public static Texture2D Sky(ArenaPalette p) {
        return Make(Key("sky", p), 4, 512, false, false,
                    (u, v) => SkyAt(p, Mathf.Atan2((v - 0.5f) * SkyHeight, SkyDistance) * Mathf.Rad2Deg));
    }

    // Sky colour at an elevation angle in degrees: below-horizon band, horizon, then up to the zenith.
    public static Color SkyAt(ArenaPalette p, float e) {
        Color mid = Color.Lerp(p.horizon, p.zenith, 0.5f);
        Color c;
        if (e < 0f) c = Color.Lerp(p.horizon, p.belowHorizon, Mathf.SmoothStep(0f, 1f, -e / 2f));
        else if (e < 7f) c = Color.Lerp(p.horizon, mid, Mathf.SmoothStep(0f, 1f, e / 7f));
        else c = Color.Lerp(mid, p.zenith, Mathf.SmoothStep(0f, 1f, (e - 7f) / 20f));
        c.a = 1f;
        return c;
    }

    // Soft band (alpha) laid over the horizon so the far sea melts into the sky.
    public static Texture2D Haze => Make("haze", 4, 128, false, false, (u, v) => {
        float d = (v - 0.43f) / 0.22f;
        return new Color(1f, 1f, 1f, Mathf.Exp(-d * d) * 0.85f);
    });

    // A cumulus cloud: overlapping puffs with noisy edges on a flat, slightly darker base (2:1 texture).
    public static Texture2D Cloud => Make("cloud", 128, 64, false, false, (u, v) => {
        float x = u * 2f;
        float d = Mathf.Max(Mathf.Max(Puff(x, v, 0.6f, 0.38f, 0.3f), Puff(x, v, 1f, 0.5f, 0.36f)),
                            Mathf.Max(Puff(x, v, 1.4f, 0.4f, 0.28f), Mathf.Max(Puff(x, v, 0.85f, 0.3f, 0.25f), Puff(x, v, 1.2f, 0.32f, 0.24f))));
        float n = TexKit.Fbm(u * 6f, v * 3f, 6, 101, 4);
        float a = Mathf.Clamp01((d + (n - 0.5f) * 0.5f) * 2.2f - 0.4f) * Smooth(0.05f, 0.25f, v);
        float shade = Mathf.Lerp(0.78f, 1f, Mathf.Clamp01(v * 1.6f));
        return new Color(shade, shade, shade, a);
    });

    static float Puff(float x, float y, float cx, float cy, float r) {
        float dx = x - cx, dy = y - cy;
        return Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / r);
    }

    public static Texture2D Moon => Make("moon", 128, 128, false, false, (u, v) => {
        float dx = u - 0.5f, dy = v - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) / 0.47f;
        float maria = TexKit.Fbm(u * 4f, v * 4f, 4, 111, 4);
        float g = Mathf.Lerp(0.98f, 0.72f, Mathf.Clamp01((maria - 0.48f) * 4f));
        g *= Crater(u, v, 0.36f, 0.6f, 0.07f) * Crater(u, v, 0.62f, 0.36f, 0.05f) * Crater(u, v, 0.58f, 0.68f, 0.04f);
        g *= Mathf.Lerp(1f, 0.82f, r * r);
        return new Color(g, g, g * 0.97f, Mathf.Clamp01((1f - r) * 40f));
    });

    static float Crater(float u, float v, float cx, float cy, float r) {
        float d = Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy)) / r;
        return d > 1.15f ? 1f : d > 0.85f ? 1.08f : 0.8f;
    }

    // Across a rainbow band (u: violet inside .. red outside), soft at both edges.
    public static Texture2D Rainbow => Make("rainbow", 64, 4, false, false, (u, v) => {
        float f = u * (RainbowBands.Length - 1);
        int i = Mathf.Min((int)f, RainbowBands.Length - 2);
        Color c = Color.Lerp(RainbowBands[i], RainbowBands[i + 1], f - i);
        c.a = Mathf.Sin(u * Mathf.PI);
        return c;
    });

    // Aurora curtain: bright lower edge fading upwards, broken into vertical rays (u tiles).
    public static Texture2D Aurora => Make("aurora", 64, 128, true, false, (u, v) => {
        float rays = TexKit.Fbm(u * 16f, 0.3f, 16, 121, 3);
        float body = Mathf.Exp(-v * 3.2f) * Smooth(0f, 0.06f, v);
        return new Color(1f, 1f, 1f, Mathf.Clamp01(body * (0.45f + 0.9f * rays)));
    });

    // ------------------------------------------------------------------ cloth
    // Flags and banners: white field (tinted by the team colour), dark border, a diamond-and-sun emblem.
    public static Texture2D Emblem => Make("emblem", 128, 128, false, false, (u, v) => {
        float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
        float g = edge < 0.07f ? 0.45f : edge < 0.095f ? 1f : 0.86f;
        float dx = Mathf.Abs(u - 0.5f), dy = Mathf.Abs(v - 0.52f);
        if (dx + dy * 0.8f < 0.21f) g = 0.42f;
        if (dx * dx + dy * dy < 0.0049f) g = 1f;
        return new Color(g, g, g, 1f);
    });

    // ------------------------------------------------------------------ helpers
    // A lit material whose texture covers `metres` x `metres` (for meshes with UVs in metres).
    public static Material Tiled(Texture2D tex, float metres, float smooth) {
        string key = tex.name + "|" + metres.ToString("0.00") + "|" + smooth.ToString("0.00");
        if (tiled.TryGetValue(key, out Material m) && m != null) return m;
        m = new Material(Mats.LitTex(tex, Color.white, smooth)) { name = key };
        m.SetTextureScale("_BaseMap", Vector2.one / metres);
        tiled[key] = m;
        return m;
    }

    // Hermite step from 0 at edge0 to 1 at edge1 (Mathf.SmoothStep interpolates between two values instead).
    public static float Smooth(float edge0, float edge1, float x) {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    static string Key(string name, ArenaPalette p) { return name + "_" + p.theme + (p.night ? "_night" : "_day"); }

    static Texture2D Make(string key, int w, int h, bool repeatU, bool repeatV, Func<float, float, Color> fn) {
        if (cache.TryGetValue(key, out Texture2D t) && t != null) return t;
        t = new Texture2D(w, h, TextureFormat.RGBA32, true) {
            name = key, filterMode = FilterMode.Bilinear, anisoLevel = 4,
            wrapModeU = repeatU ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
            wrapModeV = repeatV ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
        };
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = fn((x + 0.5f) / w, (y + 0.5f) / h);
        t.SetPixels(px);
        t.Apply(true);
        cache[key] = t;
        return t;
    }
}

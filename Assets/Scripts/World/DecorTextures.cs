using System.Collections.Generic;
using UnityEngine;

// Procedural textures for the class decorations (generated once, cached): fish-scale roof tiles for a wizard's
// spires, a glowing rune circle for a wizard's walls and a band of runes for a god's runestones. The glowing
// ones are white with alpha; their materials tint them with HDR colours so they bloom.
public static class DecorTextures {
    public const float RuneBandWidth = 0.3f, RuneBandHeight = 1f;   // metres the rune band texture is made for
    static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

    // Overlapping fish-scale tiles (6 rows and 7 scales per metre) for meshes with UVs in metres (WorldMeshes.Frustum).
    public static Material RoofTiles(Color tile, Color shadow) {
        string key = "roof" + ColorUtility.ToHtmlStringRGB(tile) + ColorUtility.ToHtmlStringRGB(shadow);
        Texture2D t = Make(key, 128, 128, true, (u, v) => {
            const int rows = 6, cols = 7;
            float rv = v * rows;
            int row = Mathf.FloorToInt(rv);
            float fy = rv - row, cu = u * cols + (row % 2) * 0.5f;
            float dx = cu - Mathf.Floor(cu) - 0.5f;
            float arc = 0.55f - Mathf.Sqrt(Mathf.Max(0f, 0.25f - dx * dx)) * 0.9f;   // the scale's round lower edge
            float tone = 0.88f + 0.22f * TexKit.Hash(Mathf.FloorToInt(cu) % cols, row, 151);
            Color c = fy < arc ? shadow : tile * Mathf.Lerp(0.72f, 1.1f, Mathf.Clamp01((fy - arc) / 0.5f)) * tone;
            if (Mathf.Abs(fy - arc) < 0.05f) c = shadow * 0.8f;
            c.a = 1f;
            return c;
        });
        return Mats.LitTex(t, Color.white, 0.4f);
    }

    // A magic circle: two rings with eight runes between them and a hexagram inside (alpha; UV 0..1).
    public static Texture2D RuneCircle => Make("runeCircle", 128, 128, false, (u, v) => {
        float x = u - 0.5f, y = v - 0.5f, r = Mathf.Sqrt(x * x + y * y);
        float d = Mathf.Min(Mathf.Abs(r - 0.46f), Mathf.Abs(r - 0.36f));
        for (int k = 0; k < 6; k++) {
            float a0 = k * Mathf.PI / 3f, a1 = a0 + Mathf.PI * 2f / 3f;
            d = Mathf.Min(d, ClassSkins.Segment(x, y, Mathf.Cos(a0) * 0.33f, Mathf.Sin(a0) * 0.33f, Mathf.Cos(a1) * 0.33f, Mathf.Sin(a1) * 0.33f));
        }
        if (r > 0.365f && r < 0.455f) {
            float a = Mathf.Atan2(y, x) / (Mathf.PI * 2f) * 8f + 8f;
            int slot = Mathf.FloorToInt(a) % 8;
            float along = (a - Mathf.Floor(a) - 0.5f) * (Mathf.PI * 2f * 0.41f / 8f);   // UV units along the ring
            d = Mathf.Min(d, ClassSkins.Rune(slot * 13 + 5, along * 2f, (r - 0.41f) * 2f) * 0.5f);   // glyph unit = 0.5 UV
        }
        float alpha = Mathf.Clamp01(1f - d / 0.012f) + Mathf.Exp(-d * d / 0.0009f) * 0.35f;
        return new Color(1f, 1f, 1f, Mathf.Clamp01(alpha) * Mathf.Clamp01((0.5f - r) * 40f));
    });

    // Four runes stacked in a band RuneBandWidth x RuneBandHeight metres (alpha), for the face of a runestone.
    public static Texture2D RuneBand => Make("runeBand", 32, 128, false, (u, v) => {
        const float scale = 1.1f;   // metres per glyph unit
        int slot = Mathf.Min(3, Mathf.FloorToInt(v * 4f));
        float px = (u - 0.5f) * RuneBandWidth / scale, py = (v * 4f - slot - 0.5f) * RuneBandHeight * 0.25f / scale;
        float d = ClassSkins.Rune(slot * 29 + 3, px, py) * scale;
        float alpha = Mathf.Clamp01(1f - d / 0.012f) + Mathf.Exp(-d * d / 0.0006f) * 0.3f;
        return new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
    });

    static Texture2D Make(string key, int w, int h, bool repeat, System.Func<float, float, Color> fn) {
        if (textures.TryGetValue(key, out Texture2D t) && t != null) return t;
        t = new Texture2D(w, h, TextureFormat.RGBA32, true) {
            name = key, wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 2,
        };
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = fn((x + 0.5f) / w, (y + 0.5f) / h);
        t.SetPixels(px);
        t.Apply(true, true);
        textures[key] = t;
        return t;
    }
}

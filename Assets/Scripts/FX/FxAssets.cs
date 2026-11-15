using System.Collections.Generic;
using UnityEngine;

// Textures and material variants used only by the effects.
// Particle and line vertex colours are clamped to 0..1, so the HDR brightness that makes bloom kick in
// has to come from the material: these are copies of the Mats particle materials with a brighter base colour.
public static class FxAssets {
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly Dictionary<string, Material> variants = new Dictionary<string, Material>();
    static readonly Material[] chunkMats = new Material[6];
    static Material charMat;
    static Texture2D glow, beam;

    // Hot centre with a soft halo: flashes, glows, embers.
    public static Texture2D Glow {
        get {
            if (glow == null) glow = Radial(64, r => { float k = 1f - r; return Mathf.Clamp01(0.7f * k * k * k * k + 0.3f * k * k); });
            return glow;
        }
    }

    // Soft across its width (v), constant along its length (u): ribbons for LineRenderer and TrailRenderer.
    public static Texture2D Beam {
        get {
            if (beam != null) return beam;
            const int H = 64;
            beam = new Texture2D(4, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FxBeam" };
            Color[] px = new Color[4 * H];
            for (int y = 0; y < H; y++) {
                float d = Mathf.Abs((y + 0.5f) / H - 0.5f) * 2f;          // 0 at the centre line, 1 at the edge
                float a = Mathf.Clamp01(0.75f * Mathf.Pow(1f - d, 3f) + 0.25f * (1f - d));
                for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color(1f, 1f, 1f, a);
            }
            beam.SetPixels(px);
            beam.Apply(false);
            return beam;
        }
    }

    // Additive particles and lines; intensity > 1 makes them bloom.
    public static Material Additive(Texture2D tex, float intensity = 1f) {
        return Variant("add", Mats.ParticleAdditive(tex), tex, intensity);
    }

    // Alpha-blended particles; intensity > 1 gives fire that covers the sky and still glows.
    public static Material Alpha(Texture2D tex, float intensity = 1f) {
        return Variant("alpha", Mats.ParticleAlpha(tex), tex, intensity);
    }

    // Lit material for flying chunks of a block material (glass and ice stay see-through).
    public static Material Chunk(BlockMaterial m) {
        Material mat = chunkMats[(int)m];
        if (mat != null) return mat;
        Color c = BlockMats.Get(m).color;
        switch (m) {
            case BlockMaterial.Glass: mat = Mats.Transparent(new Color(c.r, c.g, c.b, 0.55f), 0.95f); break;
            case BlockMaterial.Ice: mat = Mats.Transparent(new Color(c.r, c.g, c.b, 0.7f), 0.9f); break;
            case BlockMaterial.Metal: mat = Mats.Lit(c, 0.6f, 0.7f); break;
            default: mat = Mats.Lit(c, 0.15f); break;
        }
        chunkMats[(int)m] = mat;
        return mat;
    }

    // Charred bits thrown by explosions.
    public static Material Char {
        get {
            if (charMat == null) charMat = Mats.Lit(new Color(0.13f, 0.11f, 0.1f), 0.1f);
            return charMat;
        }
    }

    // Splits an HDR colour (components above 1) into a displayable colour and a brightness factor.
    public static Color SplitHdr(Color c, out float intensity) {
        float m = c.maxColorComponent;
        if (m <= 1f) { intensity = 1f; return c; }
        intensity = m;
        return new Color(c.r / m, c.g / m, c.b / m, Mathf.Clamp01(c.a));
    }

    static Material Variant(string kind, Material source, Texture2D tex, float intensity) {
        intensity = Mathf.Round(Mathf.Clamp(intensity, 0.25f, 8f) * 4f) / 4f;   // quarter steps keep the cache small
        if (Mathf.Approximately(intensity, 1f)) return source;
        string key = kind + (tex != null ? tex.GetEntityId().ToString() : "0") + "_" + intensity.ToString("0.00");
        if (variants.TryGetValue(key, out Material m) && m != null) return m;
        m = new Material(source) { name = "Fx " + key };
        m.SetColor(BaseColorId, new Color(intensity, intensity, intensity, 1f));
        variants[key] = m;
        return m;
    }

    static Texture2D Radial(int size, System.Func<float, float> alpha) {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FxGlow" };
        Color[] px = new Color[size * size];
        Vector2 c = new Vector2(0.5f, 0.5f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) {
                float r = Mathf.Clamp01(Vector2.Distance(new Vector2((x + 0.5f) / size, (y + 0.5f) / size), c) * 2f);
                px[y * size + x] = new Color(1f, 1f, 1f, alpha(r));
            }
        t.SetPixels(px);
        t.Apply(false);
        return t;
    }
}

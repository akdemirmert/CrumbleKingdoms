using System.Collections.Generic;
using UnityEngine;

// Runtime material cache. Every material is a copy of a base material from GameAssets, so the shader
// and its keywords are guaranteed to be in the build (never use Shader.Find at runtime).
public static class Mats {
    static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
    static readonly int Metallic = Shader.PropertyToID("_Metallic");
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    // Floats (not hex) so HDR colours above 1 get their own cache entry.
    static string Key(string kind, Color c, float a = 0f, float b = 0f, Object tex = null, Color e = default) {
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0}|{1:0.###},{2:0.###},{3:0.###},{4:0.###}|{5:0.##}|{6:0.##}|{7}|{8:0.##},{9:0.##},{10:0.##}",
            kind, c.r, c.g, c.b, c.a, a, b, tex != null ? tex.GetInstanceID() : 0, e.r, e.g, e.b);
    }

    static Material Make(string key, Material source, System.Action<Material> setup) {
        if (cache.TryGetValue(key, out Material m) && m != null) return m;
        m = new Material(source) { name = key };
        setup(m);
        cache[key] = m;
        return m;
    }

    // Opaque lit surface.
    public static Material Lit(Color c, float smooth = 0.25f, float metal = 0f) {
        return Make(Key("lit", c, smooth, metal), GameAssets.I.litOpaque, m => {
            m.SetColor(BaseColor, c); m.SetFloat(Smoothness, smooth); m.SetFloat(Metallic, metal);
        });
    }

    // Opaque lit surface with a texture (world-unit UVs from MeshKit.Box tile it once per metre).
    public static Material LitTex(Texture2D tex, Color tint, float smooth = 0.2f, float metal = 0f) {
        return Make(Key("littex", tint, smooth, metal, tex), GameAssets.I.litOpaque, m => {
            m.SetTexture(BaseMap, tex); m.SetColor(BaseColor, tint); m.SetFloat(Smoothness, smooth); m.SetFloat(Metallic, metal);
        });
    }

    // Lit surface that glows (emission is HDR: values above 1 bloom).
    public static Material Emissive(Color baseColor, Color emission, float smooth = 0.4f) {
        return Make(Key("emis", baseColor, smooth, 0f, null, emission), GameAssets.I.litEmissive, m => {
            m.SetColor(BaseColor, baseColor); m.SetFloat(Smoothness, smooth); m.SetColor(EmissionColor, emission);
        });
    }

    // Lit, alpha-blended (glass, ice, water, illusions). Use the colour's alpha.
    public static Material Transparent(Color c, float smooth = 0.8f, Texture2D tex = null) {
        return Make(Key("trans", c, smooth, 0f, tex), GameAssets.I.litTransparent, m => {
            m.SetColor(BaseColor, c); m.SetFloat(Smoothness, smooth);
            if (tex != null) m.SetTexture(BaseMap, tex);
        });
    }

    public static Material Unlit(Color c, Texture2D tex = null) {
        return Make(Key("unlit", c, 0f, 0f, tex), GameAssets.I.unlitOpaque, m => {
            m.SetColor(BaseColor, c);
            if (tex != null) m.SetTexture(BaseMap, tex);
        });
    }

    public static Material UnlitTransparent(Color c, Texture2D tex = null) {
        return Make(Key("unlitT", c, 0f, 0f, tex), GameAssets.I.unlitTransparent, m => {
            m.SetColor(BaseColor, c);
            if (tex != null) m.SetTexture(BaseMap, tex);
        });
    }

    // Additive particles / lines (glows, sparks, bolts). Colour comes from the particle/line vertex colour.
    public static Material ParticleAdditive(Texture2D tex = null) {
        if (tex == null) tex = TexKit.SoftCircle;
        return Make(Key("padd", Color.white, 0f, 0f, tex), GameAssets.I.particleAdditive, m => m.SetTexture(BaseMap, tex));
    }

    // Alpha-blended particles (smoke, dust, debris chips drawn as billboards).
    public static Material ParticleAlpha(Texture2D tex = null) {
        if (tex == null) tex = TexKit.Smoke;
        return Make(Key("palpha", Color.white, 0f, 0f, tex), GameAssets.I.particleAlpha, m => m.SetTexture(BaseMap, tex));
    }
}

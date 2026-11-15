using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Sun, ambient light, fog, sky reflections and post-processing for an arena palette.
public static class ArenaLighting {
    public static void Apply(ArenaPalette p) {
        Sun(p);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = p.ambientSky;
        RenderSettings.ambientEquatorColor = p.ambientEquator;
        RenderSettings.ambientGroundColor = p.ambientGround;

        // Exponential-squared fog: the battlefield (30-110 m away) stays crisp while the distance melts into the haze.
        // The distant layers carry their own aerial tint too (ArenaPalette), so the look holds even where a player
        // build has stripped the fog shader variants.
        RenderSettings.fog = p.fogDensity > 0f;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = p.fogDensity;
        RenderSettings.fogColor = p.haze;
        SkyReflection(p);

        Camera cam = Camera.main;
        if (cam != null) {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = p.horizon;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 1200f);   // the sky sits 700 m away
        }
        Post(p);
    }

    // ------------------------------------------------------------------ sun
    static void Sun(ArenaPalette p) {
        Light sun = FindSun();
        if (sun == null) return;
        sun.color = p.light;
        sun.intensity = p.lightIntensity;
        sun.transform.rotation = Quaternion.Euler(p.lightEuler);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = p.shadowStrength;
        RenderSettings.sun = sun;
    }

    // The scene's directional light: RenderSettings.sun, else the first directional light that lights the world
    // (the icon renderer's temporary lights only light the icon stage).
    static Light FindSun() {
        if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional) return RenderSettings.sun;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional && (l.cullingMask & Layers.GroundMask) != 0) return l;
        return null;
    }

    // ------------------------------------------------------------------ reflections
    // A small cubemap of the sky gradient as the default reflection, so water, glass and metal reflect this sky
    // (the scenes have no skybox to take reflections from).
    static void SkyReflection(ArenaPalette p) {
        const int size = 16;
        Cubemap cube = new Cubemap(size, TextureFormat.RGBA32, true) { name = "Sky reflection" };
        Color[] px = new Color[size * size];
        for (int face = 0; face < 6; face++) {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++) {
                    Vector3 dir = CubeDirection((CubemapFace)face, (x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                    px[y * size + x] = WorldTextures.SkyAt(p, Mathf.Asin(dir.normalized.y) * Mathf.Rad2Deg);
                }
            cube.SetPixels(px, (CubemapFace)face);
        }
        cube.Apply(true);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = cube;
        RenderSettings.reflectionIntensity = p.reflections;
    }

    // Direction through texel (s, t) of a cubemap face; s runs right and t down the face, both -1..1
    // (cubemap faces are stored top row first).
    static Vector3 CubeDirection(CubemapFace face, float s, float t) {
        switch (face) {
            case CubemapFace.PositiveX: return new Vector3(1f, -t, -s);
            case CubemapFace.NegativeX: return new Vector3(-1f, -t, s);
            case CubemapFace.PositiveY: return new Vector3(s, 1f, t);
            case CubemapFace.NegativeY: return new Vector3(s, -1f, -t);
            case CubemapFace.PositiveZ: return new Vector3(s, -t, 1f);
            default: return new Vector3(-s, -t, -1f);
        }
    }

    // ------------------------------------------------------------------ post-processing
    // The global Volume shares the profile asset: give it a private copy (components included) before changing
    // anything, so GameAssets.I.postProfile itself is never modified.
    static void Post(ArenaPalette p) {
        Volume volume = FindGlobalVolume();
        VolumeProfile source = GameAssets.I != null ? GameAssets.I.postProfile : null;
        if (volume == null || source == null) return;
        VolumeProfile profile = Object.Instantiate(source);
        for (int i = 0; i < profile.components.Count; i++) profile.components[i] = Object.Instantiate(profile.components[i]);
        volume.profile = profile;

        if (profile.TryGet(out Bloom bloom)) {
            bloom.intensity.Override(p.bloom);
            bloom.threshold.Override(p.bloomThreshold);
            bloom.tint.Override(p.bloomTint);
        }
        if (profile.TryGet(out ColorAdjustments grading)) {
            grading.postExposure.Override(p.exposure);
            grading.contrast.Override(p.contrast);
            grading.saturation.Override(p.saturation);
            grading.colorFilter.Override(p.colorFilter);
        }
        if (profile.TryGet(out Vignette vignette)) vignette.intensity.Override(p.vignette);
        if (!profile.TryGet(out WhiteBalance balance)) balance = profile.Add<WhiteBalance>();
        balance.temperature.Override(p.temperature);
    }

    static Volume FindGlobalVolume() {
        foreach (Volume v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            if (v.isGlobal) return v;
        return null;
    }
}

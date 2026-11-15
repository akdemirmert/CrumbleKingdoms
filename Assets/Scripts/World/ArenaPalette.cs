using UnityEngine;

// Every colour and light value of an arena, per theme and time of day. The builders only read this, so a
// theme's look is tuned in one place.
public class ArenaPalette {
    public ArenaTheme theme;
    public bool night;

    // sky (gradient by elevation) and atmosphere
    public Color zenith, horizon, belowHorizon, haze;
    public float fogDensity;
    public bool stars;

    // sun or moon disc: direction as azimuth (+ = right of the view) and elevation in degrees
    public bool moon;
    public Color sunColor;
    public float sunAzimuth, sunElevation, sunSize, sunGlow;

    // the scene's directional light and the ambient trilight
    public Color light;
    public float lightIntensity, shadowStrength = 0.8f;
    public Vector3 lightEuler;
    public Color ambientSky, ambientEquator, ambientGround;
    public float reflections = 0.8f;

    // terrain: top layer, the soil lip under it, and the cliff rock with its strata
    public Color top, topDetail, soil, rock, rockDark, rockLight, vein;

    // sea (or lava)
    public bool lava;
    public Color seaNear, seaFar, foam = Color.white;
    public float seaAlpha = 0.86f;

    // distant layers: near hills, mid mountains, far mountains (top colour; the foot fades into haze)
    public Color hillNear, hillMid, hillFar;
    public Color peakCap;            // tint of the highest peaks (snow); alpha 0 = no caps
    public Color cloud;
    public int clouds;

    // post-processing
    public float bloom = 0.65f, bloomThreshold = 1f, exposure, contrast = 12f, saturation = 14f, temperature, vignette = 0.22f;
    public Color bloomTint = Color.white, colorFilter = Color.white;

    public static ArenaPalette For(ArenaTheme theme, bool night) {
        ArenaPalette p = Day(theme);
        p.theme = theme;
        if (night) Night(p);
        return p;
    }

    static Color C(float r, float g, float b, float a = 1f) { return new Color(r, g, b, a); }

    static ArenaPalette Day(ArenaTheme theme) {
        switch (theme) {
            case ArenaTheme.Desert:
                return new ArenaPalette {
                    zenith = C(0.3f, 0.52f, 0.84f), horizon = C(0.99f, 0.86f, 0.66f), belowHorizon = C(0.93f, 0.8f, 0.62f), haze = C(0.97f, 0.84f, 0.66f),
                    fogDensity = 0.0007f,
                    sunColor = C(1f, 0.93f, 0.74f), sunAzimuth = 22f, sunElevation = 12f, sunSize = 1.9f, sunGlow = 0.8f,
                    light = C(1f, 0.9f, 0.72f), lightIntensity = 1.45f, lightEuler = new Vector3(48f, -26f, 0f),
                    ambientSky = C(0.74f, 0.72f, 0.7f), ambientEquator = C(0.8f, 0.68f, 0.52f), ambientGround = C(0.5f, 0.38f, 0.26f),
                    top = C(0.9f, 0.76f, 0.5f), topDetail = C(0.97f, 0.86f, 0.62f), soil = C(0.8f, 0.56f, 0.33f),
                    rock = C(0.8f, 0.54f, 0.35f), rockDark = C(0.6f, 0.36f, 0.22f), rockLight = C(0.94f, 0.74f, 0.5f), vein = C(0.55f, 0.3f, 0.18f),
                    seaNear = C(0.06f, 0.44f, 0.52f), seaFar = C(0.42f, 0.74f, 0.76f),
                    hillNear = C(0.9f, 0.7f, 0.44f), hillMid = C(0.84f, 0.58f, 0.42f), hillFar = C(0.9f, 0.76f, 0.68f), peakCap = C(0f, 0f, 0f, 0f),
                    cloud = C(1f, 0.97f, 0.92f), clouds = 5,
                    bloom = 0.7f, exposure = 0.08f, contrast = 14f, saturation = 10f, temperature = 14f, vignette = 0.24f, bloomTint = C(1f, 0.92f, 0.8f),
                };
            case ArenaTheme.Frost:
                return new ArenaPalette {
                    zenith = C(0.4f, 0.58f, 0.82f), horizon = C(0.87f, 0.92f, 0.97f), belowHorizon = C(0.78f, 0.86f, 0.92f), haze = C(0.85f, 0.9f, 0.96f),
                    fogDensity = 0.0009f,
                    sunColor = C(1f, 0.98f, 0.94f), sunAzimuth = -20f, sunElevation = 8f, sunSize = 1.4f, sunGlow = 0.5f,
                    light = C(0.92f, 0.95f, 1f), lightIntensity = 1.15f, lightEuler = new Vector3(32f, 35f, 0f),
                    ambientSky = C(0.7f, 0.8f, 0.95f), ambientEquator = C(0.72f, 0.78f, 0.86f), ambientGround = C(0.55f, 0.6f, 0.68f),
                    top = C(0.93f, 0.96f, 1f), topDetail = C(0.8f, 0.88f, 0.98f), soil = C(0.42f, 0.45f, 0.52f),
                    rock = C(0.48f, 0.52f, 0.58f), rockDark = C(0.3f, 0.33f, 0.4f), rockLight = C(0.68f, 0.74f, 0.82f), vein = C(0.88f, 0.94f, 1f),
                    seaNear = C(0.07f, 0.28f, 0.4f), seaFar = C(0.45f, 0.62f, 0.72f),
                    hillNear = C(0.76f, 0.83f, 0.9f), hillMid = C(0.62f, 0.7f, 0.84f), hillFar = C(0.76f, 0.82f, 0.92f), peakCap = C(1f, 1f, 1f, 1f),
                    cloud = C(0.95f, 0.97f, 1f), clouds = 10,
                    bloom = 0.6f, contrast = 10f, saturation = 4f, temperature = -16f, vignette = 0.22f, bloomTint = C(0.85f, 0.93f, 1f),
                };
            case ArenaTheme.Volcano:
                return new ArenaPalette {
                    zenith = C(0.2f, 0.12f, 0.13f), horizon = C(0.86f, 0.42f, 0.22f), belowHorizon = C(0.5f, 0.2f, 0.12f), haze = C(0.62f, 0.3f, 0.2f),
                    fogDensity = 0.0012f,
                    sunColor = C(1f, 0.55f, 0.3f), sunAzimuth = 18f, sunElevation = 9f, sunSize = 2.3f, sunGlow = 0.6f,
                    light = C(1f, 0.64f, 0.44f), lightIntensity = 1.05f, lightEuler = new Vector3(38f, -22f, 0f),
                    ambientSky = C(0.44f, 0.32f, 0.3f), ambientEquator = C(0.5f, 0.3f, 0.22f), ambientGround = C(0.7f, 0.26f, 0.1f),
                    top = C(0.2f, 0.18f, 0.18f), topDetail = C(0.33f, 0.28f, 0.26f), soil = C(0.15f, 0.12f, 0.12f),
                    rock = C(0.25f, 0.22f, 0.22f), rockDark = C(0.12f, 0.1f, 0.1f), rockLight = C(0.38f, 0.32f, 0.3f), vein = C(0.75f, 0.22f, 0.06f),
                    lava = true, seaNear = C(1f, 0.42f, 0.08f), seaFar = C(0.55f, 0.14f, 0.04f), foam = C(1f, 0.7f, 0.25f),
                    hillNear = C(0.17f, 0.11f, 0.11f), hillMid = C(0.3f, 0.17f, 0.15f), hillFar = C(0.5f, 0.26f, 0.2f), peakCap = C(0f, 0f, 0f, 0f),
                    cloud = C(0.36f, 0.26f, 0.25f), clouds = 9,
                    bloom = 1f, bloomThreshold = 0.95f, contrast = 18f, saturation = 12f, temperature = 10f, vignette = 0.3f, bloomTint = C(1f, 0.72f, 0.5f),
                };
            case ArenaTheme.Asgard:
                return new ArenaPalette {
                    zenith = C(0.34f, 0.28f, 0.7f), horizon = C(1f, 0.83f, 0.72f), belowHorizon = C(0.92f, 0.76f, 0.74f), haze = C(0.97f, 0.81f, 0.77f),
                    fogDensity = 0.0005f,
                    sunColor = C(1f, 0.92f, 0.7f), sunAzimuth = -25f, sunElevation = 12f, sunSize = 2f, sunGlow = 0.85f,
                    light = C(1f, 0.92f, 0.8f), lightIntensity = 1.35f, lightEuler = new Vector3(44f, 30f, 0f),
                    ambientSky = C(0.7f, 0.66f, 0.9f), ambientEquator = C(0.82f, 0.72f, 0.7f), ambientGround = C(0.45f, 0.38f, 0.42f),
                    top = C(0.93f, 0.91f, 0.87f), topDetail = C(0.96f, 0.82f, 0.46f), soil = C(0.92f, 0.74f, 0.36f),
                    rock = C(0.74f, 0.71f, 0.78f), rockDark = C(0.5f, 0.46f, 0.57f), rockLight = C(0.9f, 0.88f, 0.93f), vein = C(0.98f, 0.8f, 0.4f),
                    seaNear = C(0.15f, 0.36f, 0.6f), seaFar = C(0.56f, 0.62f, 0.86f),
                    hillNear = C(0.58f, 0.5f, 0.72f), hillMid = C(0.72f, 0.6f, 0.8f), hillFar = C(0.86f, 0.75f, 0.86f), peakCap = C(1f, 0.93f, 0.8f, 1f),
                    cloud = C(1f, 0.9f, 0.88f), clouds = 12,
                    bloom = 0.95f, exposure = 0.05f, contrast = 10f, saturation = 18f, temperature = 4f, vignette = 0.2f, bloomTint = C(1f, 0.9f, 0.75f),
                };
            default: // Meadow
                return new ArenaPalette {
                    zenith = C(0.27f, 0.51f, 0.86f), horizon = C(0.78f, 0.88f, 0.97f), belowHorizon = C(0.7f, 0.82f, 0.9f), haze = C(0.75f, 0.86f, 0.95f),
                    fogDensity = 0.0004f,
                    sunColor = C(1f, 0.96f, 0.85f), sunAzimuth = -28f, sunElevation = 11f, sunSize = 1.6f, sunGlow = 0.6f,
                    light = C(1f, 0.95f, 0.86f), lightIntensity = 1.3f, lightEuler = new Vector3(42f, 28f, 0f),
                    ambientSky = C(0.62f, 0.72f, 0.88f), ambientEquator = C(0.64f, 0.67f, 0.6f), ambientGround = C(0.32f, 0.33f, 0.25f),
                    top = C(0.36f, 0.62f, 0.24f), topDetail = C(0.52f, 0.77f, 0.3f), soil = C(0.42f, 0.28f, 0.16f),
                    rock = C(0.53f, 0.49f, 0.43f), rockDark = C(0.36f, 0.32f, 0.28f), rockLight = C(0.68f, 0.62f, 0.54f), vein = C(0.3f, 0.27f, 0.24f),
                    seaNear = C(0.11f, 0.42f, 0.56f), seaFar = C(0.42f, 0.67f, 0.8f),
                    hillNear = C(0.33f, 0.56f, 0.3f), hillMid = C(0.42f, 0.58f, 0.56f), hillFar = C(0.62f, 0.73f, 0.84f), peakCap = C(0.96f, 0.98f, 1f, 1f),
                    cloud = C(1f, 1f, 1f), clouds = 14,
                };
        }
    }

    // Night keeps the terrain colours (the dim light does the rest) and swaps sky, light and atmosphere.
    static void Night(ArenaPalette p) {
        p.night = true;
        p.stars = true;
        p.moon = true;
        p.zenith = C(0.02f, 0.03f, 0.08f);
        p.horizon = C(0.12f, 0.16f, 0.28f);
        p.belowHorizon = C(0.07f, 0.09f, 0.17f);
        p.haze = C(0.1f, 0.13f, 0.22f);
        p.sunColor = C(0.88f, 0.92f, 1f);
        p.sunAzimuth = 24f;
        p.sunElevation = 15f;
        p.sunSize = 1.3f;
        p.sunGlow = 0.45f;
        p.light = C(0.56f, 0.66f, 1f);
        p.lightIntensity = 0.55f;
        p.lightEuler = new Vector3(40f, -30f, 0f);
        p.shadowStrength = 0.6f;
        p.ambientSky = C(0.13f, 0.16f, 0.3f);
        p.ambientEquator = C(0.1f, 0.12f, 0.2f);
        p.ambientGround = C(0.06f, 0.06f, 0.08f);
        p.reflections = 0.5f;
        if (!p.lava) {
            p.seaNear = C(0.04f, 0.11f, 0.19f);
            p.seaFar = C(0.1f, 0.16f, 0.28f);
        }
        p.hillNear = C(0.05f, 0.07f, 0.1f);
        p.hillMid = C(0.08f, 0.1f, 0.17f);
        p.hillFar = C(0.13f, 0.16f, 0.26f);
        p.peakCap = p.peakCap.a > 0f ? C(0.4f, 0.45f, 0.6f, 1f) : p.peakCap;
        p.cloud = C(0.2f, 0.24f, 0.34f);
        p.clouds = Mathf.Max(3, p.clouds / 2);
        p.bloom = 1.1f;
        p.exposure = -0.15f;
        p.contrast = 14f;
        p.saturation = 0f;
        p.temperature = -10f;
        p.vignette = 0.32f;
        p.bloomTint = Color.white;

        switch (p.theme) {
            case ArenaTheme.Volcano:   // the lava keeps the night red
                p.zenith = C(0.05f, 0.02f, 0.03f);
                p.horizon = C(0.45f, 0.12f, 0.06f);
                p.belowHorizon = C(0.25f, 0.07f, 0.04f);
                p.haze = C(0.3f, 0.09f, 0.05f);
                p.sunColor = C(1f, 0.62f, 0.45f);
                p.ambientGround = C(0.6f, 0.2f, 0.08f);
                p.hillNear = C(0.08f, 0.04f, 0.04f);
                p.hillMid = C(0.14f, 0.06f, 0.05f);
                p.hillFar = C(0.24f, 0.08f, 0.06f);
                p.cloud = C(0.22f, 0.1f, 0.08f);
                p.temperature = 6f;
                p.saturation = 10f;
                p.bloomTint = C(1f, 0.72f, 0.5f);
                break;
            case ArenaTheme.Asgard:    // Ragnarok: a purple-red night under a blood moon
                p.zenith = C(0.06f, 0.03f, 0.12f);
                p.horizon = C(0.32f, 0.12f, 0.24f);
                p.belowHorizon = C(0.16f, 0.07f, 0.14f);
                p.haze = C(0.25f, 0.1f, 0.18f);
                p.sunColor = C(1f, 0.42f, 0.33f);
                p.sunSize = 1.9f;
                p.sunGlow = 0.7f;
                p.ambientSky = C(0.2f, 0.14f, 0.3f);
                p.hillFar = C(0.2f, 0.1f, 0.2f);
                p.saturation = 12f;
                p.temperature = 0f;
                break;
            case ArenaTheme.Desert:
                p.horizon = C(0.18f, 0.16f, 0.27f);
                break;
        }
    }

    // Distant colours drift towards the haze (aerial perspective); k = 0 near .. 1 far.
    public Color Aerial(Color c, float k) { return Color.Lerp(c, haze, Mathf.Clamp01(k)); }
}

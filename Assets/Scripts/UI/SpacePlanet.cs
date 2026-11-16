using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// One stylized planet of the menu's galaxy: a textured sphere, a drifting cloud shell, a glowing atmosphere
// rim and, depending on the world, a rainbow ring (Asgard) or a moon (night worlds).
// Everything is generated from the world's ArenaPalette, so a planet always matches the arena you fight on.
// Textures are built lazily (EnsureBuilt), one planet at a time, so the menu never stalls.
public class SpacePlanet : MonoBehaviour {
    public const float Radius = 12f;
    const int TexW = 768, TexH = 384;          // surface
    const int CloudW = 1024, CloudH = 512;     // clouds: the sharpest layer, so the biggest texture

    public ArenaTheme theme;
    public bool night;
    public bool Built { get; private set; }

    // Planets the player has not discovered yet (locked campaign levels) are shown as dark silhouettes.
    public bool Seen {
        get => seen;
        set { if (seen != value) { seen = value; dirty = true; } }
    }

    ArenaPalette pal;
    Transform tilt, body, clouds, glow, moonPivot;
    Material bodyMat, cloudMat, glowMat;
    Color bodyTint, cloudColor, glowColor;
    float emission;
    bool hasEmission, seen = true, dirty = true;
    float seenBlend = 1f;

    static Texture2D glowTex, ringTex, moonTex;
    static readonly Color[] Rainbow = {
        new Color(0.55f, 0.3f, 0.95f), new Color(0.25f, 0.45f, 1f), new Color(0.3f, 0.9f, 0.45f),
        new Color(1f, 0.92f, 0.3f), new Color(1f, 0.6f, 0.2f), new Color(1f, 0.25f, 0.25f),
    };

    public static SpacePlanet Create(Transform parent, ArenaTheme theme, bool night, Vector3 position) {
        GameObject go = new GameObject("Planet " + theme + (night ? " (night)" : ""));
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        SpacePlanet p = go.AddComponent<SpacePlanet>();
        p.theme = theme;
        p.night = night;
        return p;
    }

    // ------------------------------------------------------------------ colours shared with the transitions
    // Glow of the atmosphere seen from space.
    public static Color AtmoColor(ArenaTheme t, bool night) {
        Color c;
        switch (t) {
            case ArenaTheme.Desert: c = new Color(1f, 0.68f, 0.36f); break;
            case ArenaTheme.Frost: c = new Color(0.66f, 0.88f, 1f); break;
            case ArenaTheme.Volcano: return new Color(1f, 0.36f, 0.14f);
            case ArenaTheme.Asgard: return night ? new Color(1f, 0.3f, 0.42f) : new Color(0.86f, 0.62f, 1f);
            default: c = new Color(0.45f, 0.78f, 1f); break;
        }
        return night ? Color.Lerp(c, new Color(0.3f, 0.4f, 0.95f), 0.25f) : c;
    }

    // The colour the screen floods with while diving into this world (it is also the colour the battle fades in from).
    public static Color FloodColor(ArenaTheme t, bool night) {
        ArenaPalette p = ArenaPalette.For(t, night);
        Color c = night ? Color.Lerp(p.horizon, AtmoColor(t, night), 0.35f) : p.horizon;
        c.a = 1f;
        return c;
    }

    // ------------------------------------------------------------------ building
    IEnumerator job;

    // Builds the whole planet right now (used when the camera needs it immediately).
    public void EnsureBuilt() {
        if (Built) return;
        if (job == null) job = BuildRoutine();
        while (job.MoveNext()) { }
    }

    // One slice (a few milliseconds) of the build per call; false = finished. SpaceBackdrop builds the planets that are
    // not on screen this way, so the menu never stutters.
    public bool BuildStep() {
        if (Built) return false;
        if (job == null) job = BuildRoutine();
        return job.MoveNext();
    }

    IEnumerator BuildRoutine() {
        pal = ArenaPalette.For(theme, night);
        int seed = 5 + (int)theme * 13 + (night ? 7 : 0);

        // surface + emission, 16 rows per slice
        Color32[] sp = new Color32[TexW * TexH], ep = new Color32[TexW * TexH];
        bool anyEmit = false;
        for (int y = 0; y < TexH; y++) {
            for (int x = 0; x < TexW; x++) {
                Shade((x + 0.5f) / TexW, (y + 0.5f) / TexH, x, y, seed, out Color sc, out Color se);
                sp[y * TexW + x] = sc;
                ep[y * TexW + x] = se;
                if (se.r + se.g + se.b > 0.02f) anyEmit = true;
            }
            if ((y & 15) == 15) yield return null;
        }
        Texture2D surface = NewTexture("PlanetSurface " + theme, sp);
        Texture2D emit = anyEmit ? NewTexture("PlanetEmission " + theme, ep) : null;

        // clouds: fine detail, crisp edges (fewer clouds = higher `lo`)
        float lo = theme == ArenaTheme.Desert ? 0.68f : theme == ArenaTheme.Frost ? 0.6f : theme == ArenaTheme.Asgard ? 0.62f : 0.58f;
        Color32[] cp = new Color32[CloudW * CloudH];
        for (int y = 0; y < CloudH; y++) {
            float v = (y + 0.5f) / CloudH;
            float vv = theme == ArenaTheme.Desert ? 0.5f + (v - 0.5f) * 0.35f : v;   // long dusty streaks
            for (int x = 0; x < CloudW; x++) {
                float u = (x + 0.5f) / CloudW;
                float n = SpaceNoise.Fbm(u, vv, 6, 6, seed + 50);
                float wisp = SpaceNoise.Fbm(u, vv, 24, 3, seed + 57);
                float a = SpaceNoise.Smooth(lo, lo + 0.08f, n + (wisp - 0.5f) * 0.10f);
                cp[y * CloudW + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a * 0.9f) * 255f));
            }
            if ((y & 15) == 15) yield return null;
        }
        Texture2D cloudTex = new Texture2D(CloudW, CloudH, TextureFormat.RGBA32, true) {
            name = "PlanetClouds " + theme, filterMode = FilterMode.Bilinear, anisoLevel = 8,
            wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp,
        };
        cloudTex.SetPixels32(cp);
        cloudTex.Apply(true);
        yield return null;

        tilt = new GameObject("Tilt").transform;
        tilt.SetParent(transform, false);
        tilt.localRotation = Quaternion.Euler(0f, 0f, -14f);

        bodyTint = night ? new Color(0.72f, 0.78f, 1f) : Color.white;
        bodyMat = new Material(Mats.LitTex(surface, Color.white, 0.12f)) { name = "Planet body" };
        bodyMat.SetColor("_BaseColor", bodyTint);
        hasEmission = emit != null;
        if (hasEmission) {
            emission = theme == ArenaTheme.Volcano ? (night ? 1.7f : 1.3f) : 1.8f;
            bodyMat.EnableKeyword("_EMISSION");
            bodyMat.SetTexture("_EmissionMap", emit);
            bodyMat.SetColor("_EmissionColor", Color.white * emission);
            bodyMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        body = Sphere("Body", tilt, 1f, bodyMat);

        cloudColor = Color.Lerp(pal.cloud, Color.white, night ? 0.25f : 0f);
        cloudColor.a = night ? 0.8f : 0.92f;
        cloudMat = new Material(Mats.Transparent(Color.white, 0.1f)) { name = "Planet clouds" };
        cloudMat.SetTexture("_BaseMap", cloudTex);
        cloudMat.SetColor("_BaseColor", cloudColor);
        clouds = Sphere("Clouds", tilt, 1.028f, cloudMat);

        // atmosphere: an additive quad that always faces the camera (see LateUpdate)
        glowColor = AtmoColor(theme, night) * (night ? 0.85f : 1f);
        glowColor.a = 1f;
        glowMat = new Material(Mats.ParticleAdditive(GlowTex)) { name = "Planet glow" };
        glowMat.SetColor("_BaseColor", glowColor);
        glow = Quad("Glow", transform, Radius * 2f * 1.7f, glowMat);

        if (theme == ArenaTheme.Asgard) {
            Material ringMat = new Material(Mats.ParticleAlpha(RingTex)) { name = "Planet ring" };
            Transform ring = Quad("Ring", transform, Radius * 2f * 3.4f, ringMat);
            ring.localRotation = Quaternion.Euler(76f, 0f, -16f);
        }
        if (night) {
            moonPivot = new GameObject("Moon orbit").transform;
            moonPivot.SetParent(transform, false);
            moonPivot.localRotation = Quaternion.Euler(14f, 0f, 8f);
            Material moonMat = new Material(Mats.LitTex(MoonTex, Color.white, 0.05f)) { name = "Planet moon" };
            Color moonTint = theme == ArenaTheme.Asgard ? new Color(1f, 0.4f, 0.34f)
                           : theme == ArenaTheme.Volcano ? new Color(1f, 0.78f, 0.68f) : new Color(0.9f, 0.94f, 1f);
            moonMat.SetColor("_BaseColor", moonTint);
            Sphere("Moon", moonPivot, 0.24f, moonMat).localPosition = new Vector3(Radius * 2.35f, 0f, 0f);
        }
        Built = true;
        dirty = true;
    }

    // Unity's built-in sphere has only 24 segments (a jagged silhouette next to the camera): this one has 128 x 64.
    static Mesh sphereMesh;
    public static Mesh SphereMesh {
        get {
            if (sphereMesh != null) return sphereMesh;
            const int Lon = 128, Lat = 64;
            Vector3[] v = new Vector3[(Lon + 1) * (Lat + 1)], n = new Vector3[v.Length];
            Vector2[] uv = new Vector2[v.Length];
            int[] tri = new int[Lon * Lat * 6];
            for (int j = 0; j <= Lat; j++) {
                float phi = j / (float)Lat * Mathf.PI;
                for (int i = 0; i <= Lon; i++) {
                    float th = i / (float)Lon * Mathf.PI * 2f;
                    Vector3 p = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), -Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                    int k = j * (Lon + 1) + i;
                    n[k] = p;
                    v[k] = p * 0.5f;
                    uv[k] = new Vector2(i / (float)Lon, j / (float)Lat);
                }
            }
            int t = 0;
            for (int j = 0; j < Lat; j++) {
                for (int i = 0; i < Lon; i++) {
                    int a = j * (Lon + 1) + i, b = a + 1, c = a + Lon + 1, d = c + 1;
                    tri[t++] = a; tri[t++] = c; tri[t++] = d;
                    tri[t++] = a; tri[t++] = d; tri[t++] = b;
                }
            }
            sphereMesh = new Mesh { name = "PlanetSphere", vertices = v, normals = n, uv = uv, triangles = tri };
            sphereMesh.RecalculateBounds();
            return sphereMesh;
        }
    }

    static Transform Sphere(string name, Transform parent, float sizeMul, Material mat) {
        GameObject s = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        s.GetComponent<MeshFilter>().sharedMesh = SphereMesh;
        s.transform.SetParent(parent, false);
        s.transform.localScale = Vector3.one * (Radius * 2f * sizeMul);
        MeshRenderer r = s.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return s.transform;
    }

    static Transform Quad(string name, Transform parent, float size, Material mat) {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.localScale = new Vector3(size, size, 1f);
        MeshRenderer r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return q.transform;
    }

    // ------------------------------------------------------------------ per frame
    void Update() {
        if (!Built) return;
        float dt = Time.unscaledDeltaTime;
        body.Rotate(0f, 3.2f * dt, 0f, Space.Self);
        clouds.Rotate(0f, 4.6f * dt, 0f, Space.Self);
        if (moonPivot != null) moonPivot.Rotate(0f, 11f * dt, 0f, Space.Self);

        float target = seen ? 1f : 0f;
        if (!Mathf.Approximately(seenBlend, target)) {
            seenBlend = Mathf.MoveTowards(seenBlend, target, dt * 2.5f);
            dirty = true;
        }
        if (dirty) {
            dirty = false;
            Apply();
        }
    }

    void LateUpdate() {
        if (!Built) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        // the atmosphere quad sits just in front of the planet and looks at the camera: its inner part brightens the
        // limb, its outer part is the halo
        Vector3 toCam = (cam.transform.position - transform.position).normalized;
        glow.position = transform.position + toCam * (Radius * 1.02f);
        glow.rotation = cam.transform.rotation;
    }

    // Silhouette blend: undiscovered worlds are dark with a faint halo.
    void Apply() {
        float k = Mathf.SmoothStep(0f, 1f, seenBlend);
        bodyMat.SetColor("_BaseColor", Color.Lerp(new Color(0.035f, 0.04f, 0.06f), bodyTint, k));
        if (hasEmission) bodyMat.SetColor("_EmissionColor", Color.white * (emission * k));
        Color c = cloudColor;
        c.a *= Mathf.Lerp(0.08f, 1f, k);
        cloudMat.SetColor("_BaseColor", c);
        glowMat.SetColor("_BaseColor", glowColor * Mathf.Lerp(0.25f, 1f, k));
    }

    // ------------------------------------------------------------------ textures
    static Texture2D NewTexture(string name, Color32[] px) {
        Texture2D t = new Texture2D(TexW, TexH, TextureFormat.RGBA32, true) {
            name = name, filterMode = FilterMode.Bilinear, anisoLevel = 4,
            wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp,
        };
        t.SetPixels32(px);
        t.Apply(true);
        return t;
    }

    // Colour (and emission: lava, city lights) of the surface at longitude u, latitude v.
    void Shade(float u, float v, int px, int py, int seed, out Color c, out Color e) {
        float lat = Mathf.Abs(v - 0.5f) * 2f;                      // 0 at the equator, 1 at the poles
        float h = SpaceNoise.Fbm(u, v, 4, 5, seed);                // continents
        float detail = SpaceNoise.Fbm(u, v, 16, 3, seed + 3);
        bool land = true;
        e = Color.black;
        Color ice = new Color(0.95f, 0.97f, 1f);

        switch (theme) {
            case ArenaTheme.Frost: {
                c = Color.Lerp(pal.top, pal.topDetail, detail);
                land = h >= 0.44f;
                if (!land) c = Color.Lerp(new Color(0.1f, 0.3f, 0.55f), new Color(0.38f, 0.64f, 0.88f), h / 0.44f);
                float crack = Mathf.Abs(SpaceNoise.Fbm(u, v, 6, 3, seed + 9) * 2f - 1f);
                if (crack < 0.04f) c = Color.Lerp(c, new Color(0.35f, 0.6f, 0.92f), 0.8f);
                c = Color.Lerp(c, ice, SpaceNoise.Smooth(0.7f, 0.85f, lat));
                break;
            }
            case ArenaTheme.Desert: {
                float warp = SpaceNoise.Fbm(u, v, 3, 3, seed + 1);
                float band = Mathf.Sin((v * 9f + warp * 3.5f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                c = Color.Lerp(pal.soil, pal.top, band);
                c = Color.Lerp(c, pal.topDetail, detail * 0.6f);
                if (h > 0.62f) c = Color.Lerp(c, pal.rock, SpaceNoise.Smooth(0.62f, 0.72f, h));   // dark highlands
                land = h >= 0.3f;
                if (!land) c = Color.Lerp(c, new Color(0.2f, 0.72f, 0.72f), SpaceNoise.Smooth(0.3f, 0.24f, h));   // oases and seas
                c = Color.Lerp(c, new Color(1f, 0.95f, 0.85f), SpaceNoise.Smooth(0.9f, 0.97f, lat) * 0.6f);
                break;
            }
            case ArenaTheme.Volcano: {
                c = Color.Lerp(pal.rockDark, pal.rock, detail);
                float ridge = 1f - Mathf.Abs(SpaceNoise.Fbm(u, v, 5, 4, seed + 2) * 2f - 1f);   // ~1 along the cracks
                float crackLava = SpaceNoise.Smooth(0.93f, 0.985f, ridge);
                float lake = SpaceNoise.Smooth(0.42f, 0.34f, h);
                float lava = Mathf.Max(crackLava, lake);
                Color hot = Color.Lerp(new Color(1f, 0.35f, 0.05f), new Color(1f, 0.85f, 0.35f), crackLava * 0.7f);
                c = Color.Lerp(c, hot, lava);
                e = hot * lava;
                e.a = 1f;
                land = lava < 0.5f;
                break;
            }
            case ArenaTheme.Asgard: {
                land = h >= 0.5f;
                if (!land) {
                    float depth = Mathf.Clamp01((0.5f - h) / 0.22f);
                    c = Color.Lerp(Color.Lerp(pal.seaFar, new Color(0.55f, 0.5f, 0.9f), 0.5f),
                                   Color.Lerp(pal.seaNear, new Color(0.12f, 0.1f, 0.35f), 0.6f), depth);
                } else {
                    c = Color.Lerp(pal.top, new Color(0.98f, 0.97f, 0.99f), detail);
                    c = Color.Lerp(c, pal.topDetail, SpaceNoise.Smooth(0.58f, 0.72f, h) * 0.8f);   // gold terraces
                    float vein = Mathf.Abs(SpaceNoise.Fbm(u, v, 7, 3, seed + 4) * 2f - 1f);
                    if (vein < 0.035f) c = Color.Lerp(c, pal.vein, 0.85f);
                }
                c = Color.Lerp(c, new Color(1f, 0.95f, 1f), SpaceNoise.Smooth(0.85f, 0.94f, lat));
                break;
            }
            default: {   // Meadow
                land = h >= 0.5f;
                if (!land) {
                    float depth = Mathf.Clamp01((0.5f - h) / 0.2f);
                    c = Color.Lerp(Color.Lerp(pal.seaFar, new Color(0.35f, 0.8f, 0.85f), 0.4f),
                                   Color.Lerp(pal.seaNear, new Color(0.05f, 0.2f, 0.45f), 0.5f), depth);
                } else {
                    float t = Mathf.Clamp01((h - 0.5f) / 0.22f);
                    c = Color.Lerp(pal.top, pal.topDetail, detail);
                    if (h < 0.525f) c = Color.Lerp(c, new Color(0.9f, 0.82f, 0.55f), 0.8f);             // beaches
                    if (t > 0.65f) c = Color.Lerp(c, pal.rockLight, (t - 0.65f) * 2.5f);                // mountains
                    if (t > 0.9f) c = Color.Lerp(c, Color.white, 0.8f);                                 // snow caps
                }
                c = Color.Lerp(c, ice, SpaceNoise.Smooth(0.82f, 0.9f, lat + (detail - 0.5f) * 0.12f));
                break;
            }
        }
        c.a = 1f;

        // night worlds glow with the lights of their inhabitants
        if (night && land && lat < 0.8f && theme != ArenaTheme.Volcano) {
            float cluster = SpaceNoise.Fbm(u, v, 12, 2, seed + 21);
            float dot = SpaceNoise.Hash01(px / 2, py / 2, seed + 5);
            Color lights = LightColor();
            if (cluster > 0.52f && dot > 0.82f) e = lights * (0.8f + 0.2f * dot);
            else e = lights * (SpaceNoise.Smooth(0.55f, 0.7f, cluster) * 0.06f);
            e.a = 1f;
        }
    }

    Color LightColor() {
        switch (theme) {
            case ArenaTheme.Desert: return new Color(1f, 0.6f, 0.25f);
            case ArenaTheme.Frost: return new Color(0.6f, 0.9f, 1f);
            case ArenaTheme.Asgard: return new Color(1f, 0.3f, 0.35f);   // runes of Ragnarok
            default: return new Color(1f, 0.82f, 0.45f);
        }
    }

    // Halo of the atmosphere. The quad is 1.7x the planet's diameter, so the limb sits at r = 1 / 1.7.
    static Texture2D GlowTex {
        get {
            if (glowTex == null) glowTex = SpaceNoise.Make("PlanetGlow", 256, 256, (u, v) => {
                float dx = u - 0.5f, dy = v - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                const float edge = 1f / 1.7f;
                float a = r < edge ? 0.5f * SpaceNoise.Smooth(edge * 0.6f, edge, r)
                                   : 0.9f * Mathf.Exp(-Mathf.Pow((r - edge) / 0.09f, 1.5f));
                a *= 1f - SpaceNoise.Smooth(0.8f, 1f, r);
                return new Color(1f, 1f, 1f, a);
            }, false);
            return glowTex;
        }
    }

    // Rainbow bands between 1.45 and 2.3 planet radii (the quad is 3.4 radii wide on each side of the centre).
    static Texture2D RingTex {
        get {
            if (ringTex == null) ringTex = SpaceNoise.Make("PlanetRing", 256, 256, (u, v) => {
                float dx = u - 0.5f, dy = v - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float t = (r - 0.426f) / (0.676f - 0.426f);
                if (t < 0f || t > 1f) return new Color(1f, 1f, 1f, 0f);
                float f = t * (Rainbow.Length - 1);
                int i = Mathf.Min((int)f, Rainbow.Length - 2);
                Color col = Color.Lerp(Rainbow[i], Rainbow[i + 1], f - i);
                float stripe = 0.5f + 0.5f * Mathf.Sin(t * 40f);
                float a = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.7f) * (0.6f + 0.4f * stripe);
                if (t > 0.62f && t < 0.68f) a *= 0.2f;   // a gap in the ring
                col.a = a * 0.85f;
                return col;
            }, false);
            return ringTex;
        }
    }

    static Texture2D MoonTex {
        get {
            if (moonTex == null) moonTex = SpaceNoise.Make("PlanetMoon", 256, 128, (u, v) => {
                float n = SpaceNoise.Fbm(u, v, 5, 4, 901);
                float g = Mathf.Lerp(0.55f, 0.95f, n);
                return new Color(g, g, g * 0.98f, 1f);
            });
            return moonTex;
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;

// The sky and the distant world behind the battlefield.
// - "At infinity", on a SkyAnchor that follows the camera (so they never show an edge or a parallax error):
//   the gradient sky, the sun or the moon with its glow, stars, aurora curtains and the falling snow.
// - Fixed in the world, so they slide past with real parallax: three ridges of distant land fading into the
//   haze, a wall of haze where the sea meets the sky, drifting clouds and each theme's landmark: Bifrost and
//   floating rocks over Asgard, a smoking volcano and drifting embers, icebergs, desert dunes and mesas.
// Nothing here has a collider or casts a shadow. Depth layering, for a camera that can pull back to z = -110:
// land and clouds stay within ~625 m of it (far ridge crest z <= 512), the sun, stars and aurora sit between
// 630 and 700 m (behind every mountain), and the opaque sky quad closes everything at WorldTextures.SkyDistance.
public static class SkyBuilder {
    const float SunDistance = 670f, StarDistance = 690f, AuroraDistance = 668f;   // AuroraRibbon sways +-25 m in depth
    const float HazeZ = 420f;          // in front of the far ridge and the last 100 m of the sea (SeaBuilder.FarBack)
    const float HazePeak = 0.43f;      // v of the densest row of WorldTextures.Haze
    const int HazeQueue = 2985;        // after the water (2980), so the haze veils the far sea
    const float RidgeHalfWidth = 1300f;

    enum Shape { Hills, Mountains, Dunes, Mesas }

    static Texture2D sunDisc;

    public static void Build(Transform root, ArenaPalette p) {
        System.Random rng = new System.Random(4242 + (int)p.theme * 97 + (p.night ? 13 : 0));
        Transform sky = ModelKit.Root("Sky", root).transform;

        Transform anchor = Anchor(sky);
        GradientSky(anchor, p);
        SunOrMoon(anchor, p);
        if (p.stars) Stars(anchor, rng);
        if (p.theme == ArenaTheme.Asgard || (p.theme == ArenaTheme.Frost && p.night)) Aurora(anchor, p, rng);

        Ridges(sky, p);
        HazeWall(sky, p);
        Clouds(sky, p, rng);
        switch (p.theme) {
            case ArenaTheme.Asgard:
                Bifrost(sky, p);
                FloatingRocks(sky, p, rng);
                break;
            case ArenaTheme.Volcano:
                Volcano(sky, p);
                DriftingEmbers(sky);
                break;
            case ArenaTheme.Frost:
                Icebergs(sky, rng);
                Snowfall(anchor, p);
                break;
        }
    }

    // ------------------------------------------------------------------ at infinity (camera-anchored)
    static Transform Anchor(Transform parent) {
        GameObject go = ModelKit.Root("Sky anchor", parent);
        go.AddComponent<SkyAnchor>();
        Camera cam = Camera.main;
        if (cam != null) go.transform.position = cam.transform.position;   // SkyAnchor keeps it there from now on
        return go.transform;
    }

    // A huge quad with the elevation gradient (WorldTextures.Sky is laid out for exactly this quad).
    static void GradientSky(Transform anchor, ArenaPalette p) {
        float d = WorldTextures.SkyDistance;
        Part(anchor, WorldWideQuad, new Vector3(0f, 0f, d), new Vector3(d * 3f, WorldTextures.SkyHeight, 1f),
             Mats.Unlit(Color.white, WorldTextures.Sky(p)), "Sky gradient");
    }

    // The sun (HDR, so it blooms) or the moon, each with a tight glow and a wide one.
    // ArenaPalette.sunSize is the disc's angular diameter in degrees.
    static void SunOrMoon(Transform anchor, ArenaPalette p) {
        Vector3 dir = Direction(p.sunAzimuth, p.sunElevation);
        Vector3 face = Quaternion.LookRotation(dir).eulerAngles;   // the quad's front (-z) looks back at the camera
        float size = 2f * SunDistance * Mathf.Tan(p.sunSize * 0.5f * Mathf.Deg2Rad);
        Part(anchor, WorldWideQuad, dir * (SunDistance + 8f), Vector3.one * size * 16f, Glow(Hdr(p.sunColor, 0.32f * p.sunGlow)), "Sky glow", face);
        Part(anchor, WorldWideQuad, dir * (SunDistance + 4f), Vector3.one * size * 5f, Glow(Hdr(p.sunColor, 1.1f * p.sunGlow)), "Sun glow", face);
        Material disc = new Material(Mats.UnlitTransparent(Color.white, p.moon ? WorldTextures.Moon : SunDisc));
        disc.SetColor("_BaseColor", Hdr(p.sunColor, p.moon ? 1.25f : 3f));
        Part(anchor, WorldWideQuad, dir * SunDistance, Vector3.one * size, disc, p.moon ? "Moon" : "Sun", face);
    }

    // Many faint stars in one static mesh, a few bright twinkling ones in another (StarTwinkle).
    static void Stars(Transform anchor, System.Random rng) {
        StarField(anchor, "Stars", 300, 1.4f, 3.2f, 0.35f, 0.9f, TexKit.SoftCircle, rng);
        GameObject bright = StarField(anchor, "Bright stars", 36, 5f, 9f, 0.8f, 1.6f, TexKit.Star, rng);
        bright.AddComponent<StarTwinkle>().Init(bright.GetComponent<MeshFilter>().sharedMesh);
    }

    static GameObject StarField(Transform anchor, string name, int count, float minSize, float maxSize, float minBright, float maxBright,
                                Texture2D tex, System.Random rng) {
        MeshBuilder b = new MeshBuilder();
        for (int i = 0; i < count; i++) {
            // only the band from the horizon to ~16 degrees is ever in view; fewer stars low down, in the haze
            Vector3 dir = Direction(R(rng, -58f, 58f), Mathf.Lerp(2.5f, 19f, Mathf.Sqrt(R(rng, 0f, 1f))));
            float k = R(rng, minBright, maxBright);
            double tint = rng.NextDouble();
            Color c = tint < 0.2 ? new Color(1f, 0.86f, 0.72f) : tint < 0.45 ? new Color(0.75f, 0.85f, 1f) : Color.white;
            Billboard(b, dir * StarDistance, dir, R(rng, minSize, maxSize) * 0.5f, new Color(c.r * k, c.g * k, c.b * k, Mathf.Clamp01(k)));
        }
        return Part(anchor, b.ToMesh(name), Vector3.zero, Vector3.one, Mats.ParticleAdditive(tex), name);
    }

    // Swaying curtains of light (AuroraRibbon animates them); faint by day over Asgard, strong at night.
    static void Aurora(Transform anchor, ArenaPalette p, System.Random rng) {
        float strength = p.night ? 1f : 0.3f;
        Curtain(anchor, "Aurora", AuroraDistance, 5f, 95f, new Color(0.35f, 1.5f, 0.75f), new Color(0.4f, 0.75f, 1.5f), strength, rng);
        Curtain(anchor, "Aurora (high)", AuroraDistance - 6f, 8.5f, 70f, new Color(0.5f, 1.3f, 1f), new Color(1.1f, 0.5f, 1.3f), strength * 0.7f, rng);
    }

    static void Curtain(Transform anchor, string name, float distance, float baseElevation, float height, Color a, Color c, float strength,
                        System.Random rng) {
        const float halfWidth = 700f, step = 20f, endFade = 180f;
        float seed = rng.Next(1000) * 0.37f, baseY = distance * Mathf.Tan(baseElevation * Mathf.Deg2Rad);
        MeshBuilder b = new MeshBuilder();
        int prevBottom = -1, prevTop = -1;
        for (float x = -halfWidth; x <= halfWidth + 0.1f; x += step) {
            float n = Mathf.PerlinNoise(x * 0.004f + seed, 0.5f);
            float y = baseY + 18f * (n - 0.5f), h = height * (0.6f + 0.6f * Mathf.PerlinNoise(x * 0.007f, seed + 0.3f));
            Color col = Color.Lerp(a, c, Mathf.PerlinNoise(x * 0.003f, seed + 0.7f));
            col.a = strength * (0.55f + 0.45f * n) * WorldTextures.Smooth(0f, endFade, halfWidth - Mathf.Abs(x));
            int bottom = b.Vertex(new Vector3(x, y, distance), new Vector2(x / 140f, 0f), col);
            int top = b.Vertex(new Vector3(x, y + h, distance), new Vector2(x / 140f, 1f), col);
            if (prevBottom >= 0) b.Quad(prevBottom, prevTop, top, bottom);
            prevBottom = bottom;
            prevTop = top;
        }
        Mesh mesh = b.ToMesh(name);
        GameObject go = Part(anchor, mesh, Vector3.zero, Vector3.one, Mats.ParticleAdditive(WorldTextures.Aurora), name);
        go.AddComponent<AuroraRibbon>().Init(mesh);
    }

    // Snow falling around the camera wherever it looks: emitted in a box in front of the anchor, simulated in the
    // world so the flakes do not slide along when the camera pans.
    static void Snowfall(Transform anchor, ArenaPalette p) {
        ParticleSystem ps = Particles(anchor, "Snowfall", new Vector3(0f, 14f, 68f), Quaternion.identity, Mats.ParticleAlpha(TexKit.SoftCircle));
        ParticleSystem.MainModule main = ps.main;
        main.duration = 18f;   // prewarm runs one duration: as long as the longest life, so it starts full
        main.startLifetime = new ParticleSystem.MinMaxCurve(13f, 18f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.2f);
        main.startColor = p.night ? new Color(0.62f, 0.68f, 0.85f, 0.85f) : new Color(1f, 1f, 1f, 0.9f);
        main.maxParticles = 2200;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 130f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(210f, 70f, 120f);
        Velocity(ps, new Vector2(0.3f, 1.2f), new Vector2(-2.8f, -2f));
        Flutter(ps, 0.7f, 0.25f);
        ps.Play();
    }

    // ------------------------------------------------------------------ distant land
    // Three ridges, near to far, each one colour fading into the haze at its foot (aerial perspective).
    static void Ridges(Transform parent, ArenaPalette p) {
        Shape near = p.theme == ArenaTheme.Desert ? Shape.Dunes : p.theme == ArenaTheme.Volcano ? Shape.Mountains : Shape.Hills;
        Shape far = p.theme == ArenaTheme.Desert ? Shape.Mesas : Shape.Mountains;
        Ridge(parent, p, "Near hills", near, 160f, 26f, 9f, p.hillNear, 0.25f, false, 11);
        Ridge(parent, p, "Mountains", far, 300f, 70f, 14f, p.hillMid, 0.45f, p.theme == ArenaTheme.Frost, 23);
        Ridge(parent, p, "Far mountains", far, 430f, 125f, 20f, p.hillFar, 0.7f, true, 37);
    }

    // A faceted strip of land from x = -RidgeHalfWidth to +RidgeHalfWidth: a foot below the waterline at z, a
    // shoulder and a crest further back. Only the slopes facing the camera are built.
    static void Ridge(Transform parent, ArenaPalette p, string name, Shape shape, float z, float maxHeight, float step, Color top,
                      float footHaze, bool caps, int seed) {
        Vector3 toLight = ToLight(p);
        float depth = maxHeight * 0.6f + 10f, foot = World.WaterY - 3f;
        MeshBuilder b = new MeshBuilder();
        Vector3 lastBase = Vector3.zero, lastShoulder = Vector3.zero, lastCrest = Vector3.zero;
        int columns = Mathf.FloorToInt(2f * RidgeHalfWidth / step);
        for (int i = 0; i <= columns; i++) {
            float x = -RidgeHalfWidth + i * step;
            float h = Profile(shape, x, seed) * maxHeight;
            float j = Mathf.PerlinNoise(x * 0.05f, seed * 0.37f) - 0.5f;   // -0.5..0.5: breaks up the regular grid
            Vector3 basePoint = new Vector3(x, foot, z);
            Vector3 crest = new Vector3(x + j * step * 0.4f, World.WaterY + h, z + depth * (0.75f + 0.3f * j));
            Vector3 shoulder = Vector3.Lerp(basePoint, crest, 0.5f) + new Vector3(0f, j * h * 0.15f, -depth * 0.12f);
            if (i > 0) {
                ShadedQuad(b, lastBase, lastShoulder, shoulder, basePoint, toLight, World.WaterY, maxHeight);
                ShadedQuad(b, lastShoulder, lastCrest, crest, shoulder, toLight, World.WaterY, maxHeight);
            }
            lastBase = basePoint;
            lastShoulder = shoulder;
            lastCrest = crest;
        }
        Part(parent, b.ToMesh(name), Vector3.zero, Vector3.one, RidgeMaterial(p, top, footHaze, caps), name);
    }

    // Height profile 0..1 along x.
    static float Profile(Shape shape, float x, int seed) {
        float s = seed * 13.7f;
        switch (shape) {
            case Shape.Hills:
                return 0.25f + 0.75f * (0.65f * Mathf.PerlinNoise(x * 0.006f + s, 0.3f) + 0.35f * Mathf.PerlinNoise(x * 0.019f + s, 1.7f));
            case Shape.Dunes: {
                float w = x * 0.021f + 9f * Mathf.PerlinNoise(x * 0.004f + s, 0.6f);
                float dune = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(w + 0.45f * Mathf.Sin(w)), 1.5f);   // long windward side, steep lee
                return 0.2f + 0.8f * dune * (0.55f + 0.45f * Mathf.PerlinNoise(x * 0.009f + s, 2.1f));
            }
            case Shape.Mesas: {
                float n = 0.7f * Mathf.PerlinNoise(x * 0.005f + s, 0.9f) + 0.3f * Mathf.PerlinNoise(x * 0.02f + s, 3.3f);
                float table = WorldTextures.Smooth(0.42f, 0.5f, n);                                 // sheer cliffs up to flat tops
                return Mathf.Lerp(0.15f + 0.5f * n, 0.62f + 0.12f * Mathf.PerlinNoise(x * 0.03f + s, 4.4f), table);
            }
            default: {   // mountains: ridged noise gives sharp peaks, a slow envelope gathers them into massifs
                float r = 0f, amp = 0.6f, f = 0.004f;
                for (int o = 0; o < 3; o++) {
                    float n = 1f - Mathf.Abs(2f * Mathf.PerlinNoise(x * f + s + o * 5.1f, o * 1.3f) - 1f);
                    r += n * n * amp;
                    amp *= 0.45f;
                    f *= 2.3f;
                }
                return Mathf.Clamp01(r / 0.95f) * (0.45f + 0.55f * Mathf.PerlinNoise(x * 0.0015f + s, 7.7f));
            }
        }
    }

    // Unlit colour by height (v: the foot melts into the haze, snow on the highest peaks) and by a light baked
    // into u per facet (ShadedFacet), so the far land keeps the palette's colours yet still looks faceted.
    static Material RidgeMaterial(ArenaPalette p, Color top, float footHaze, bool caps) {
        const int W = 16, H = 64;
        Color foot = p.Aerial(top, footHaze);
        bool snow = caps && p.peakCap.a > 0f;
        Texture2D t = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "Ridge", wrapMode = TextureWrapMode.Clamp };
        Color[] px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++) {
                float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                Color c = Color.Lerp(foot, top, WorldTextures.Smooth(0f, 0.6f, v));
                if (snow) c = Color.Lerp(c, p.peakCap, WorldTextures.Smooth(0.7f, 0.78f, v));
                c *= Mathf.Lerp(0.66f, 1.1f, u);
                c.a = 1f;
                px[y * W + x] = c;
            }
        t.SetPixels(px);
        t.Apply(false);
        Material m = new Material(Mats.Unlit(Color.white));
        m.SetTexture("_BaseMap", t);
        return m;
    }

    static void ShadedQuad(MeshBuilder b, Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Vector3 toLight, float baseY, float maxHeight) {
        ShadedFacet(b, bl, tl, tr, toLight, baseY, maxHeight);
        ShadedFacet(b, bl, tr, br, toLight, baseY, maxHeight);
    }

    // A flat facet: u = how much it faces the sun, v = height above baseY as a fraction of maxHeight.
    static void ShadedFacet(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 toLight, float baseY, float maxHeight) {
        Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0).normalized;   // front faces wind clockwise (see MeshBuilder)
        float u = Mathf.InverseLerp(0.15f, 0.95f, Vector3.Dot(normal, toLight));
        b.Facet(p0, p1, p2, q => new Vector2(u, (q.y - baseY) / maxHeight));
    }

    // A wall of haze standing just before the sea's far edge: it hides the seam between sea and sky and veils
    // the foot of the far mountains. The desert gets a taller, warmer band.
    static void HazeWall(Transform parent, ArenaPalette p) {
        float height = p.theme == ArenaTheme.Desert ? 95f : 64f, peakY = World.WaterY + 2f;
        Material m = new Material(Mats.UnlitTransparent(p.haze, WorldTextures.Haze)) { renderQueue = HazeQueue };
        Part(parent, WorldWideQuad, new Vector3(0f, peakY + (0.5f - HazePeak) * height, HazeZ), new Vector3(3400f, height, 1f), m, "Horizon haze");
    }

    // Billboard clouds over the distant land (in front of the far mountains); CloudDrift moves them with the wind.
    static void Clouds(Transform parent, ArenaPalette p, System.Random rng) {
        GameObject layer = ModelKit.Root("Clouds", parent);
        CloudDrift drift = layer.AddComponent<CloudDrift>();
        Material m = Mats.UnlitTransparent(p.cloud, WorldTextures.Cloud);
        for (int i = 0; i < p.clouds; i++) {
            float width = R(rng, 70f, 170f), x = Mathf.Lerp(-drift.halfWidth, drift.halfWidth, (i + R(rng, 0.1f, 0.9f)) / p.clouds);
            Part(layer.transform, WorldWideQuad, new Vector3(x, R(rng, 45f, 125f), R(rng, 230f, 410f)),
                 new Vector3(width, width * R(rng, 0.42f, 0.55f), 1f), m, "Cloud");
        }
    }

    // ------------------------------------------------------------------ theme landmarks
    // Bifrost: a glowing rainbow bridge arching out of the sea behind the mountains, fading out at both feet.
    static void Bifrost(Transform parent, ArenaPalette p) {
        const float radius = 240f, width = 16f, z = 400f;
        const int steps = 72;
        Vector3 centre = new Vector3(-50f, -150f, z);
        float footAngle = Mathf.Asin((World.WaterY - centre.y) / radius);   // where the arc meets the sea
        MeshBuilder b = new MeshBuilder();
        int prevIn = -1, prevOut = -1;
        for (int k = 0; k <= steps; k++) {
            float t = k / (float)steps;
            float a = Mathf.Lerp(Mathf.PI - footAngle, footAngle, t);   // left foot to right foot
            Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Color fade = new Color(1f, 1f, 1f, WorldTextures.Smooth(0f, 0.16f, Mathf.Min(t, 1f - t)));
            int inner = b.Vertex(centre + dir * radius, new Vector2(0f, 0.5f), fade);           // violet inside
            int outer = b.Vertex(centre + dir * (radius + width), new Vector2(1f, 0.5f), fade);  // red outside
            if (prevIn >= 0) b.Quad(prevIn, prevOut, outer, inner);
            prevIn = inner;
            prevOut = outer;
        }
        Material m = new Material(Mats.ParticleAlpha(WorldTextures.Rainbow));
        m.SetColor("_BaseColor", new Color(1.3f, 1.3f, 1.3f, p.night ? 0.8f : 0.65f));
        Part(parent, b.ToMesh("Bifrost"), Vector3.zero, Vector3.one, m, "Bifrost");
    }

    // Islets floating in the sky to both sides of the battle: an upturned rocky cone with a marble top (Bobber).
    static void FloatingRocks(Transform parent, ArenaPalette p, System.Random rng) {
        Material rock = Mats.Lit(p.rock, 0.15f), top = Mats.Lit(p.top, 0.35f);
        float[] xs = { -230f, -130f, 115f, 200f, 290f };
        foreach (float x in xs) {
            float s = R(rng, 5f, 11f);
            Transform isle = ModelKit.Root("Floating rock", parent).transform;
            isle.SetPositionAndRotation(new Vector3(x + R(rng, -25f, 25f), R(rng, 32f, 72f), R(rng, 130f, 240f)), Quaternion.Euler(0f, R(rng, 0f, 360f), 0f));
            Bobber bob = isle.gameObject.AddComponent<Bobber>();
            bob.height = R(rng, 0.8f, 1.6f);
            bob.period = R(rng, 6f, 10f);
            Part(isle, WorldMeshes.Frustum(7, 0.12f, 1f, 1f), new Vector3(0f, -s * 1.5f, 0f), new Vector3(s, s * 1.5f, s), rock, "Rock");
            Part(isle, WorldMeshes.Dome(7, 1, 1f), Vector3.zero, new Vector3(s * 1.02f, s * 0.16f, s * 1.02f), top, "Top");
            Part(isle, MeshKit.Rock(1f, 1 + rng.Next(6), 0.3f, 1), new Vector3(s * 0.35f, -s * 0.9f, s * 0.1f), Vector3.one * (s * 0.42f), rock, "Rock");
        }
    }

    // A smoking volcano on the distant left: a faceted cone shaded like the ridges, a glowing crater rim and lava
    // rivers, a soft glow over the crater, a smoke plume and a fountain of embers.
    static void Volcano(Transform parent, ArenaPalette p) {
        const float height = 120f, footRadius = 112f, craterRadius = 20f;
        const int sides = 18;
        float[] levels = { 0f, 0.3f, 0.58f, 0.8f, 0.94f, 1f };
        Vector3 foot = new Vector3(-190f, World.WaterY - 3f, 390f), crater = foot + Vector3.up * height;
        Transform root = ModelKit.Root("Volcano", parent).transform;

        Vector3[,] ring = new Vector3[levels.Length, sides];
        for (int k = 0; k < levels.Length; k++)
            for (int i = 0; i < sides; i++) {
                float a = i * Mathf.PI * 2f / sides;
                float r = ConeRadius(levels[k], footRadius, craterRadius) * (1f + 0.06f * (TexKit.Hash(i, k, 77) - 0.5f));
                ring[k, i] = foot + new Vector3(Mathf.Cos(a) * r, levels[k] * height, Mathf.Sin(a) * r);
            }
        MeshBuilder b = new MeshBuilder();
        Vector3 toLight = ToLight(p);
        for (int k = 0; k < levels.Length - 1; k++)
            for (int i = 0; i < sides; i++) {
                int j = (i + 1) % sides;
                ShadedQuad(b, ring[k, i], ring[k + 1, i], ring[k + 1, j], ring[k, j], toLight, foot.y, height);
            }
        Part(root, b.ToMesh("Volcano"), Vector3.zero, Vector3.one, RidgeMaterial(p, Color.Lerp(p.hillMid, p.rockDark, 0.4f), 0.45f, false), "Volcano");

        Material lava = Mats.Emissive(new Color(0.35f, 0.08f, 0.02f), new Color(3.2f, 1f, 0.18f), 0.3f);
        Part(root, WorldMeshes.Frustum(sides, craterRadius + 1.6f, craterRadius * 0.9f, 4f), crater + Vector3.down * 3.2f, Vector3.one, lava, "Crater rim");
        Part(root, LavaRivers(foot, height, levels, footRadius, craterRadius), Vector3.zero, Vector3.one, lava, "Lava rivers");
        Part(root, WorldWideQuad, crater + new Vector3(0f, 8f, -30f), new Vector3(120f, 90f, 1f),
             Glow(Hdr(new Color(1f, 0.38f, 0.1f), p.night ? 1.1f : 0.6f)), "Crater glow");
        SmokePlume(root, crater + Vector3.up * 2f, p);
        CraterEmbers(root, crater);
    }

    // Concave flanks narrowing to the crater; t = 0 at the foot .. 1 at the top.
    static float ConeRadius(float t, float footRadius, float craterRadius) {
        return craterRadius + (footRadius - craterRadius) * Mathf.Pow(1f - t, 1.7f);
    }

    // Glowing strips running down the camera side of the cone from the crater, just proud of the rock (the cone's
    // flat facets lie on straight lines between its rings, and its vertices are jittered by up to 3 %).
    static Mesh LavaRivers(Vector3 foot, float height, float[] levels, float footRadius, float craterRadius) {
        MeshBuilder b = new MeshBuilder();
        float[] angles = { 252f, 276f, 297f };   // around the cone; 270 faces the camera (-z)
        float[] lengths = { 0.62f, 0.8f, 0.48f };
        for (int r = 0; r < angles.Length; r++) {
            int prevL = -1, prevR = -1;
            for (int k = 0; k <= 12; k++) {
                float t = k / 12f, level = 1f - t * lengths[r];
                float a = (angles[r] + 7f * Mathf.Sin(t * 7f + r)) * Mathf.Deg2Rad;
                float radius = FacetRadius(level, levels, footRadius, craterRadius) * 1.035f + 0.6f;
                Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), side = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                Vector3 c = foot + outward * radius + Vector3.up * (level * height);
                float w = Mathf.Lerp(3.5f, 1.2f, t);
                int left = b.Vertex(c - side * w, new Vector2(0f, t)), right = b.Vertex(c + side * w, new Vector2(1f, t));
                if (prevL >= 0) b.Quad(left, prevL, prevR, right);   // seen from outside: lower edge, then upper edge
                prevL = left;
                prevR = right;
            }
        }
        return b.ToMesh("Lava rivers");
    }

    // Radius of the faceted cone surface at a level (linear between its rings).
    static float FacetRadius(float t, float[] levels, float footRadius, float craterRadius) {
        for (int k = 0; k < levels.Length - 1; k++)
            if (t <= levels[k + 1])
                return Mathf.Lerp(ConeRadius(levels[k], footRadius, craterRadius), ConeRadius(levels[k + 1], footRadius, craterRadius),
                                  Mathf.InverseLerp(levels[k], levels[k + 1], t));
        return craterRadius;
    }

    static void SmokePlume(Transform parent, Vector3 at, ArenaPalette p) {
        ParticleSystem ps = Particles(parent, "Smoke plume", at, Quaternion.Euler(-90f, 0f, 0f), Mats.ParticleAlpha(TexKit.Smoke));
        ParticleSystem.MainModule main = ps.main;
        main.duration = 26f;   // prewarm runs one duration: the whole plume is there from the start
        main.startLifetime = new ParticleSystem.MinMaxCurve(18f, 26f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(28f, 50f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
        main.startColor = p.night ? new Color(0.2f, 0.13f, 0.12f, 0.85f) : new Color(0.32f, 0.27f, 0.26f, 0.8f);
        main.maxParticles = 70;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 3f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = 10f;
        Velocity(ps, new Vector2(2f, 5f), Vector2.zero);   // the plume leans away with the high wind
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 2.4f));
        // lit orange by the lava at the bottom, greying as it rises
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.55f, 0.3f), 0f), new GradientColorKey(Color.white, 0.3f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.08f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(g);
        ps.Play();
    }

    static void CraterEmbers(Transform parent, Vector3 at) {
        ParticleSystem ps = Particles(parent, "Crater embers", at, Quaternion.Euler(-90f, 0f, 0f), EmberMaterial());
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(18f, 32f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 3.4f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.12f), new Color(1f, 0.75f, 0.3f));
        main.gravityModifier = 0.55f;
        main.maxParticles = 70;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 9f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 22f;
        shape.radius = 6f;
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = SeaBuilder.Fade(Color.white, new Color(1f, 0.35f, 0.2f), 1f);
        ps.Play();
    }

    // Embers rising from the lava sea and drifting through the battlefield.
    static void DriftingEmbers(Transform parent) {
        ParticleSystem ps = Particles(parent, "Drifting embers", new Vector3(5f, 7f, 28f), Quaternion.identity, EmberMaterial());
        ParticleSystem.MainModule main = ps.main;
        main.duration = 10f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.5f, 0.15f), new Color(1f, 0.78f, 0.35f));
        main.maxParticles = 160;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 18f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(260f, 26f, 86f);
        Velocity(ps, new Vector2(0.1f, 0.6f), new Vector2(0.4f, 1.3f));
        Flutter(ps, 0.5f, 0.35f);
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = SeaBuilder.Fade(Color.white, new Color(1f, 0.5f, 0.3f), 1f);
        ps.Play();
    }

    // Icebergs bobbing in the sea well behind the battle (Bobber).
    static void Icebergs(Transform parent, System.Random rng) {
        Material ice = Mats.Lit(new Color(0.86f, 0.94f, 1f), 0.6f);
        float[] xs = { -190f, -105f, 95f, 165f, 245f };   // beside the battle, not right behind the fortresses
        foreach (float x in xs) {
            float s = R(rng, 5f, 13f);
            Transform berg = ModelKit.Root("Iceberg", parent).transform;
            berg.position = new Vector3(x + R(rng, -20f, 20f), World.WaterY, R(rng, 80f, 230f));
            Bobber bob = berg.gameObject.AddComponent<Bobber>();
            bob.height = 0.3f;
            bob.period = R(rng, 7f, 11f);
            bob.tilt = 1f;
            Part(berg, MeshKit.Rock(1f, 1 + rng.Next(6), 0.38f, 1), new Vector3(0f, s * 0.12f, 0f), new Vector3(s * 1.6f, s * 0.85f, s * 1.2f), ice, "Ice");
            Part(berg, MeshKit.Rock(1f, 1 + rng.Next(6), 0.38f, 1), new Vector3(s * 0.9f, s * 0.05f, s * 0.3f), new Vector3(s * 0.7f, s * 0.5f, s * 0.6f), ice, "Ice");
        }
    }

    // ------------------------------------------------------------------ helpers
    static Mesh WorldWideQuad => WorldMeshes.Quad();   // unit quad facing -z (the camera)

    static GameObject Part(Transform parent, Mesh mesh, Vector3 pos, Vector3 scale, Material m, string name, Vector3 euler = default) {
        GameObject go = ModelKit.MeshPart(parent, mesh, pos, scale, m, euler, name);
        ModelKit.Shadows(go, false, false);
        return go;
    }

    // Colour scaled to an HDR intensity with alpha kept at 1 (Color * float would scale the alpha too).
    static Color Hdr(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, 1f); }

    // Additive soft glow with an HDR colour (a material copy: Mats keys colours as clamped hex).
    static Material Glow(Color c) {
        Material m = new Material(Mats.ParticleAdditive(TexKit.SoftCircle));
        m.SetColor("_BaseColor", c);
        return m;
    }

    // Additive embers; the HDR tint makes them bloom (particle colours themselves are 8-bit).
    static Material EmberMaterial() {
        Material m = new Material(Mats.ParticleAdditive(TexKit.SoftCircle));
        m.SetColor("_BaseColor", new Color(2.6f, 2.6f, 2.6f, 1f));
        return m;
    }

    // A crisp disc with a slightly darker limb.
    static Texture2D SunDisc {
        get {
            if (sunDisc != null) return sunDisc;
            const int S = 64;
            sunDisc = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = "sunDisc", wrapMode = TextureWrapMode.Clamp };
            Color[] px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++) {
                    float dx = (x + 0.5f) / S - 0.5f, dy = (y + 0.5f) / S - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) / 0.47f;
                    float limb = Mathf.Lerp(1f, 0.85f, r * r);
                    px[y * S + x] = new Color(limb, limb, limb, Mathf.Clamp01((1f - r) * 24f));
                }
            sunDisc.SetPixels(px);
            sunDisc.Apply(true);
            return sunDisc;
        }
    }

    // A looping, prewarmed particle system simulated in world space (configure, then Play).
    static ParticleSystem Particles(Transform parent, string name, Vector3 localPos, Quaternion localRot, Material m) {
        GameObject go = ModelKit.Root(name, parent);
        go.transform.SetLocalPositionAndRotation(localPos, localRot);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    // Constant world-space drift, random per particle between the given (min, max) for x and y.
    static void Velocity(ParticleSystem ps, Vector2 x, Vector2 y) {
        ParticleSystem.VelocityOverLifetimeModule v = ps.velocityOverLifetime;
        v.enabled = true;
        v.space = ParticleSystemSimulationSpace.World;
        v.x = new ParticleSystem.MinMaxCurve(x.x, x.y);
        v.y = new ParticleSystem.MinMaxCurve(y.x, y.y);
        v.z = new ParticleSystem.MinMaxCurve(0f, 0f);   // all three axes must use the same curve mode
    }

    static void Flutter(ParticleSystem ps, float strength, float frequency) {
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = strength;
        noise.frequency = frequency;
        noise.scrollSpeed = 0.2f;
        noise.quality = ParticleSystemNoiseQuality.Low;
    }

    // A camera-facing quad of half-size `half` centred at c on the anchor's sky dome (dir = unit direction to c).
    static void Billboard(MeshBuilder b, Vector3 c, Vector3 dir, float half, Color col) {
        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized * half, up = Vector3.Cross(dir, right).normalized * half;
        b.Quad(b.Vertex(c - right - up, new Vector2(0f, 0f), col), b.Vertex(c - right + up, new Vector2(0f, 1f), col),
               b.Vertex(c + right + up, new Vector2(1f, 1f), col), b.Vertex(c + right - up, new Vector2(1f, 0f), col));
    }

    // Unit direction from azimuth (degrees, + = right of the view) and elevation (degrees above the horizon).
    static Vector3 Direction(float azimuth, float elevation) {
        float a = azimuth * Mathf.Deg2Rad, e = elevation * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
    }

    static Vector3 ToLight(ArenaPalette p) { return -(Quaternion.Euler(p.lightEuler) * Vector3.forward); }

    static float R(System.Random rng, float a, float b) { return IslandBuilder.Rand(rng, a, b); }
}

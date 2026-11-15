using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// The Alien's Space Warp (used on yourself: one click, the aim means nothing). It pulls the battle into space
// until the start of the caster's second turn after the cast: gravity drops to GravityScale for everyone and
// every enemy fortress throws with SpeedScale of its power ("Space" on its card). The sky turns to space.
// One effect per enemy fortress (each shows its own status chip); gravity and the sky follow "is any warp
// still on", so warps of two different aliens never undo each other. Cancel (time rewind, end of the battle)
// takes back exactly this warp's part.
public class AlienSpaceWarp : TurnEffect {
    public const float GravityScale = 0.45f, SpeedScale = 0.75f;
    const int Turns = 2;                    // ends at the start of the owner's second turn after the cast
    const float CastTime = 1.6f;            // the turn waits while the sky turns to space

    int ownerTurns;
    bool ended;

    public override string StatusText => ended ? null : "Space " + Mathf.Max(1, Turns - ownerTurns);
    public override int RewindState { get => ownerTurns; set => ownerTurns = value; }
    public override Color StatusColor => new Color(0.66f, 0.58f, 1f);

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        if (BattleManager.I == null || owner == null) return;
        List<Fortress> enemies = BattleManager.I.EnemiesOf(owner);
        if (enemies.Count == 0) return;      // nobody left to slow down (and no effect that would end the warp)
        foreach (Fortress enemy in enemies) {
            AlienSpaceWarp w = new GameObject("Space Warp (" + enemy.DisplayName + ")").AddComponent<AlienSpaceWarp>();
            w.Init(owner, enemy);
            enemy.launchSpeedScale = SpeedScale;
        }
        World.SetGravityScale(GravityScale);
        AlienSpaceSky.Show(true);

        GameObject cast = new GameObject("Space Warp cast");
        cast.AddComponent<TurnActor>();
        Destroy(cast, CastTime);
        VFX.MagicBurst(origin, AlienKit.Warp, 1.4f);
        VFX.Ring(origin, 9f, AlienKit.Warp, 0.7f, 0.5f);
        VFX.Ring(origin, 5f, new Color(0.6f, 1f, 0.8f), 0.5f, 0.3f);
        VFX.LightFlash(origin, AlienKit.Warp, 8f, 20f, 0.6f);
        VFX.ScreenFlash(new Color(0.55f, 0.45f, 1f, 0.45f), 0.45f);
        AlienSounds.Play(AlienSounds.Warp, null, 0.95f);
        BattleCamera.Shake(0.35f);
    }

    public override IEnumerator OnTurnStart(Fortress shooter) {
        if (ended) yield break;
        if (shooter == owner) ownerTurns++;
        if (ownerTurns < Turns && owner != null && !owner.defeated) yield break;
        if (End()) {                         // the last warp: back to the ground, with a moment to see it happen
            AlienSounds.Play(AlienSounds.WarpOut, null, 0.9f);
            VFX.ScreenFlash(new Color(0.8f, 0.75f, 1f, 0.35f), 0.35f);
            yield return new WaitForSeconds(0.7f);
        }
        if (this != null) Destroy(gameObject);
    }

    public override void Cancel() {
        End();
        base.Cancel();
    }

    // Takes this warp's part back. Returns true when no warp is left: gravity and the sky are back to normal.
    bool End() {
        if (ended) return false;
        ended = true;
        bool anyLeft = false, stillSlowed = false;
        foreach (TurnEffect e in TurnEffect.All) {
            if (!(e is AlienSpaceWarp w) || w.ended) continue;
            anyLeft = true;
            if (w.target == target) stillSlowed = true;
        }
        if (target != null && !stillSlowed) target.launchSpeedScale = 1f;
        if (anyLeft) return false;
        World.SetGravityScale(1f);
        AlienSpaceSky.Show(false);
        return true;
    }
}

// The sky during a space warp: a dark, starry backdrop with a nebula, a ringed planet and a small moon far behind
// the battle, plus motes drifting over the battlefield. The backdrop follows the camera like the normal sky, at
// a distance between the far land and clouds (in front) and the sun, stars and sky gradient (behind), and draws
// before the sea and the horizon haze so they still veil its foot. It writes depth, so the sun and the normal
// stars behind it disappear. It fades in and out; while it shows, the haze (fog) thins out: no air in space.
public class AlienSpaceSky : MonoBehaviour {
    const float Distance = 634f;            // land and clouds stay within ~625 m of the camera, the sun is at 670
    const float FadeIn = 1.4f, FadeOut = 1.1f;
    const int Queue = 2950;                 // before the water (2980) and the haze (2985)
    const float Bottom = -3f, Top = 21f;    // elevation range of the backdrop (degrees)
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

    static AlienSpaceSky instance;
    static Texture2D backdropTexture, planetTexture, moonTexture;
    static Mesh starMesh, brightStarMesh;

    Transform anchor, motesRoot;
    ParticleSystem motes;
    Material backdrop, stars, brightStars, planet, halo, moon;
    float level, target, shown = -1f, baseFog;
    bool closing;                            // faded out: destroyed at the end of this frame

    public static void Show(bool on) {
        if (instance == null || instance.closing) {
            if (!on) return;
            instance = new GameObject("Space Sky").AddComponent<AlienSpaceSky>();
            instance.Build();
        }
        instance.target = on ? 1f : 0f;
    }

    void Build() {
        baseFog = RenderSettings.fogDensity;
        anchor = ModelKit.Root("Anchor", transform).transform;
        float d = Distance, yLo = d * Mathf.Tan(Bottom * Mathf.Deg2Rad), yHi = d * Mathf.Tan(Top * Mathf.Deg2Rad);
        backdrop = Copy(Mats.UnlitTransparent(Color.white, Backdrop), Queue);
        Part(WorldMeshes.Quad(), new Vector3(0f, (yLo + yHi) * 0.5f, d), new Vector3(d * 2.4f, yHi - yLo, 1f), Quaternion.identity, backdrop, "Space");
        stars = Copy(Mats.ParticleAdditive(TexKit.SoftCircle), Queue + 1);
        brightStars = Copy(Mats.ParticleAdditive(TexKit.Star), Queue + 1);
        if (starMesh == null) starMesh = StarField(380, 1.3f, 3f, 0.35f, 1f, d - 2f, 11);
        if (brightStarMesh == null) brightStarMesh = StarField(40, 4f, 8f, 0.8f, 1f, d - 2f, 12);
        Part(starMesh, Vector3.zero, Vector3.one, Quaternion.identity, stars, "Stars");
        Part(brightStarMesh, Vector3.zero, Vector3.one, Quaternion.identity, brightStars, "Bright stars");
        halo = Copy(Mats.ParticleAdditive(FxAssets.Glow), Queue + 2);
        planet = Copy(Mats.UnlitTransparent(Color.white, Planet), Queue + 2);
        moon = Copy(Mats.UnlitTransparent(Color.white, Moon), Queue + 2);
        Billboard(-20f, 9f, d - 7f, new Vector2(100f, 100f), halo, "Planet glow");
        Billboard(-20f, 9f, d - 8f, new Vector2(190f, 95f), planet, "Planet");
        Billboard(23f, 13.5f, d - 8f, new Vector2(26f, 26f), moon, "Moon");
        ModelKit.Shadows(gameObject, false, false);
        motes = Motes();
        LateUpdate();
        Apply(0f);
    }

    void Update() {
        level = Mathf.MoveTowards(level, target, Time.deltaTime / (target > level ? FadeIn : FadeOut));
        if (level != shown) Apply(level);
        if (level <= 0f && target <= 0f && !closing) {
            RenderSettings.fogDensity = baseFog;
            closing = true;
            Destroy(gameObject);
        }
    }

    // Like the normal sky: always at the same place in view (no parallax), whatever the camera does.
    void LateUpdate() {
        Camera cam = Camera.main;
        if (cam == null) return;
        Transform c = cam.transform;
        Vector3 flat = c.forward;
        flat.y = 0f;
        anchor.SetPositionAndRotation(c.position, flat.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(flat) : Quaternion.identity);
        motesRoot.position = new Vector3(c.position.x, c.position.y, 0f);
    }

    void Apply(float k) {
        shown = k;
        float e = k * k * (3f - 2f * k);
        backdrop.SetColor(BaseColorId, new Color(1f, 1f, 1f, e));
        backdrop.SetFloat(ZWriteId, k > 0.2f ? 1f : 0f);       // hides the sun and the normal stars behind it
        stars.SetColor(BaseColorId, new Color(1.2f, 1.2f, 1.2f, e));
        brightStars.SetColor(BaseColorId, new Color(2.2f, 2.2f, 2.2f, e));
        halo.SetColor(BaseColorId, new Color(0.55f, 0.4f, 1f, 0.35f * e));
        planet.SetColor(BaseColorId, new Color(1f, 1f, 1f, e));
        moon.SetColor(BaseColorId, new Color(1f, 1f, 1f, e));
        ParticleSystem.EmissionModule em = motes.emission;
        em.rateOverTime = 40f * e;
        RenderSettings.fogDensity = baseFog * (1f - e);
    }

    void OnDestroy() {
        if (instance == this) instance = null;
        foreach (Material m in new[] { backdrop, stars, brightStars, halo, planet, moon }) if (m != null) Destroy(m);
    }

    // ------------------------------------------------------------------ building
    static Material Copy(Material source, int queue) { return new Material(source) { renderQueue = queue }; }

    void Part(Mesh mesh, Vector3 pos, Vector3 scale, Quaternion rot, Material m, string name) {
        ModelKit.MeshPart(anchor, mesh, pos, scale, m, rot.eulerAngles, name);
    }

    // A camera-facing quad in the direction (azimuth, elevation), `dist` away.
    void Billboard(float azimuth, float elevation, float dist, Vector2 size, Material m, string name) {
        Vector3 dir = Direction(azimuth, elevation);
        Part(WorldMeshes.Quad(), dir * dist, new Vector3(size.x, size.y, 1f), Quaternion.LookRotation(dir), m, name);
    }

    static Vector3 Direction(float azimuth, float elevation) {
        float a = azimuth * Mathf.Deg2Rad, e = elevation * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
    }

    // Stars as camera-facing quads in one mesh, denser higher up (the low sky is hazy).
    static Mesh StarField(int count, float minSize, float maxSize, float minBright, float maxBright, float dist, int seed) {
        System.Random rng = new System.Random(seed);
        MeshBuilder b = new MeshBuilder();
        for (int i = 0; i < count; i++) {
            Vector3 dir = Direction(Range(rng, -55f, 55f), Mathf.Lerp(2f, 20f, Mathf.Sqrt((float)rng.NextDouble())));
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized, up = Vector3.Cross(dir, right);
            float half = Range(rng, minSize, maxSize) * 0.5f, k = Range(rng, minBright, maxBright);
            double tint = rng.NextDouble();
            Color c = tint < 0.2 ? new Color(1f, 0.85f, 0.7f) : tint < 0.5 ? new Color(0.72f, 0.85f, 1f) : Color.white;
            c = new Color(c.r * k, c.g * k, c.b * k, 1f);
            Vector3 p = dir * dist;
            b.Quad(b.Vertex(p - right * half - up * half, new Vector2(0f, 0f), c), b.Vertex(p - right * half + up * half, new Vector2(0f, 1f), c),
                   b.Vertex(p + right * half + up * half, new Vector2(1f, 1f), c), b.Vertex(p + right * half - up * half, new Vector2(1f, 0f), c));
        }
        return b.ToMesh("Space stars");
    }

    static float Range(System.Random rng, float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

    // Slow glowing motes drifting over the battlefield (world space, around the camera's view of the battle).
    ParticleSystem Motes() {
        motesRoot = ModelKit.Root("Motes", transform).transform;
        ParticleSystem ps = motesRoot.gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.95f, 1f), new Color(0.85f, 0.75f, 1f));
        main.gravityModifier = -0.015f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 260;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 0f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(110f, 55f, 16f);
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = 0.35f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.1f;
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.25f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = FxAssets.Additive(FxAssets.Glow, 2f);
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sortingOrder = FxLayers.OrderGlow;
        ps.Play();
        return ps;
    }

    // ------------------------------------------------------------------ textures (made once)
    // Deep blue-black space over a horizon glow, clear at the horizon (the haze and the land show there) and opaque
    // from ~8 degrees up, with a slanted band of violet and teal nebula clouds. v runs from Bottom to Top elevation.
    static Texture2D Backdrop {
        get {
            if (backdropTexture != null) return backdropTexture;
            const int W = 512, H = 128;
            backdropTexture = Paint(W, H, "Space backdrop", (u, v) => {
                float elev = Mathf.Lerp(Bottom, Top, v);
                Color c = Color.Lerp(new Color(0.07f, 0.1f, 0.21f), new Color(0.015f, 0.01f, 0.045f), Mathf.SmoothStep(0f, 1f, elev / 18f));
                float band = Mathf.Exp(-Sq((v - 0.6f - (u - 0.5f) * 0.45f) / 0.17f));
                float n = Fbm(u * 7f, v * 1.8f, 1.3f);
                float cloud = Mathf.Clamp01((n - 0.42f) * 2.4f) * band;
                Color neb = Color.Lerp(new Color(0.55f, 0.15f, 0.65f), new Color(0.08f, 0.5f, 0.62f), Mathf.PerlinNoise(u * 3f + 7.1f, v * 2f + 3.3f));
                c += neb * cloud * 0.6f;
                c.a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 8f, elev));
                return c;
            });
            return backdropTexture;
        }
    }

    // A banded violet gas giant lit from the upper right, with a thin atmosphere rim and a tilted ring
    // (its far half behind the planet, its near half in front). 2:1, so the ring fits.
    static Texture2D Planet {
        get {
            if (planetTexture != null) return planetTexture;
            const int W = 256, H = 128;
            Vector3 light = new Vector3(0.65f, 0.45f, -0.6f).normalized;
            float tilt = -14f * Mathf.Deg2Rad, cos = Mathf.Cos(tilt), sin = Mathf.Sin(tilt);
            planetTexture = Paint(W, H, "Space planet", (u, v) => {
                float x = u - 0.5f, y = (v - 0.5f) * 0.5f;                // units of the texture's width
                const float R = 0.2f;
                float rx = x * cos + y * sin, ry = -x * sin + y * cos;    // in the ring's frame
                float q = Mathf.Sqrt(rx * rx + Sq(ry / 0.26f));
                Color ring = Ring(q);
                bool front = ry < 0f;
                Color c = front ? Color.clear : ring;
                float d = Mathf.Sqrt(x * x + y * y) / R;
                if (d < 1f) {
                    Vector3 n = new Vector3(x / R, y / R, -Mathf.Sqrt(1f - d * d));
                    float lit = Mathf.Clamp01(Vector3.Dot(n, light));
                    float lat = y / R + 0.12f * Mathf.PerlinNoise(u * 14f, v * 5f);
                    float bands = 0.5f + 0.5f * Mathf.Sin(lat * 11f + Mathf.PerlinNoise(u * 6f, v * 9f) * 2f);
                    Color body = Color.Lerp(new Color(0.42f, 0.28f, 0.72f), new Color(0.86f, 0.7f, 0.95f), bands);
                    body = Color.Lerp(body, new Color(0.98f, 0.72f, 0.6f), Mathf.Clamp01(Mathf.Sin(lat * 3.3f + 1f)) * 0.3f);
                    body *= 0.12f + 0.95f * lit;
                    body += new Color(0.45f, 0.65f, 1f) * Mathf.Pow(d, 6f) * 0.55f * (0.3f + lit);   // atmosphere rim
                    body.a = Mathf.Clamp01((1f - d) * R * W * 0.7f);       // a soft, anti-aliased edge
                    c = Over(body, c);
                }
                if (front) c = Over(ring, c);
                return c;
            });
            return planetTexture;
        }
    }

    // The ring's colour at ring radius q (texture widths): pale bands with a dark gap.
    static Color Ring(float q) {
        if (q < 0.29f || q > 0.47f) return Color.clear;
        float t = (q - 0.29f) / 0.18f;
        float a = (0.35f + 0.45f * Mathf.PerlinNoise(t * 22f, 0.5f)) * Mathf.Clamp01(Mathf.Min(t, 1f - t) * 12f);
        if (t > 0.58f && t < 0.66f) a *= 0.15f;                      // the gap
        return new Color(0.85f, 0.8f, 0.95f, a);
    }

    // A small grey-violet moon with craters, lit from the upper right.
    static Texture2D Moon {
        get {
            if (moonTexture != null) return moonTexture;
            const int S = 64;
            Vector3 light = new Vector3(0.65f, 0.45f, -0.6f).normalized;
            moonTexture = Paint(S, S, "Space moon", (u, v) => {
                float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f, d = Mathf.Sqrt(x * x + y * y);
                if (d >= 1f) return Color.clear;
                Vector3 n = new Vector3(x, y, -Mathf.Sqrt(1f - d * d));
                float crater = Mathf.Clamp01((Mathf.PerlinNoise(u * 9f + 2f, v * 9f + 5f) - 0.55f) * 4f);
                Color c = Color.Lerp(new Color(0.72f, 0.7f, 0.8f), new Color(0.45f, 0.43f, 0.55f), crater);
                c *= 0.1f + 0.95f * Mathf.Clamp01(Vector3.Dot(n, light));
                c.a = Mathf.Clamp01((1f - d) * S * 0.5f);
                return c;
            });
            return moonTexture;
        }
    }

    static Texture2D Paint(int w, int h, string name, System.Func<float, float, Color> fn) {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = fn((x + 0.5f) / w, (y + 0.5f) / h);
        t.SetPixels(px);
        t.Apply(false, true);
        return t;
    }

    // `top` drawn over `under` (straight alpha).
    static Color Over(Color top, Color under) {
        float a = top.a + under.a * (1f - top.a);
        if (a <= 0f) return Color.clear;
        Color c = (top * top.a + under * under.a * (1f - top.a)) / a;
        c.a = a;
        return c;
    }

    static float Fbm(float x, float y, float lacunarity) {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int o = 0; o < 4; o++) {
            sum += Mathf.PerlinNoise(x + o * 17.3f, y + o * 9.1f) * amp;
            norm += amp;
            amp *= 0.5f;
            x *= lacunarity * 1.6f;
            y *= lacunarity * 1.6f;
        }
        return sum / norm;
    }

    static float Sq(float x) { return x * x; }
}

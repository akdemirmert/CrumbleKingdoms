using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public enum SpaceView { Wide, Title, Level, Duel }

// The galaxy behind the main menu (replaces MenuBackdrop): twinkling stars, nebulae, drifting dust, shooting
// stars and ten stylized planets, one per arena (5 themes x day/night). The camera flies between them as the
// menu changes page or the player hovers a level. When a battle starts, Dive() flies into that level's planet,
// floods the screen with its atmosphere colour and hands over to SceneFader.
public class SpaceBackdrop : MonoBehaviour {
    public static SpaceBackdrop Current { get; private set; }
    public static bool Diving { get; private set; }

    public const float BaseFov = 40f;
    const float DiveSeconds = 1.7f;

    // Level order first, then the two duel-only worlds.
    public static readonly (ArenaTheme theme, bool night)[] Worlds = {
        (ArenaTheme.Meadow, false), (ArenaTheme.Frost, false), (ArenaTheme.Desert, false), (ArenaTheme.Frost, true),
        (ArenaTheme.Volcano, true), (ArenaTheme.Volcano, false), (ArenaTheme.Asgard, false), (ArenaTheme.Asgard, true),
        (ArenaTheme.Meadow, true), (ArenaTheme.Desert, true),
    };

    public static int WorldIndex(ArenaTheme theme, bool night) {
        for (int i = 0; i < Worlds.Length; i++)
            if (Worlds[i].theme == theme && Worlds[i].night == night) return i;
        return 0;
    }

    static Vector3 WorldPos(int i) { return new Vector3(i * 70f - 315f, 20f * Mathf.Sin(i * 1.1f), 35f * Mathf.Cos(i * 0.8f)); }

    public CanvasGroup uiGroup;   // the menu canvas: fades out while diving

    Camera cam;
    Light sun;
    SpacePlanet[] planets;
    Transform starAnchor, nebulaAnchor;

    // where the camera wants to be: a point, a distance, a screen offset of that point and the viewing angle
    int viewWorld;
    Vector3 viewFocus;
    float viewDist, viewYaw, viewPitch;
    Vector2 viewNdc;
    Quaternion viewRot = Quaternion.identity;
    Color accentTarget = new Color(0.56f, 0.71f, 1f);

    Vector3 camPos, camVel;
    bool placed;
    float time, introK = 1f, fovKick, nextShootingStar;

    // dive
    bool diveLoading;
    float diveT;
    int diveWorld;
    Vector3 diveStartPos;
    Quaternion diveStartRot;
    Color diveColor;

    public static SpaceBackdrop Create(Transform parent) {
        GameObject go = new GameObject("SpaceBackdrop");
        go.transform.SetParent(parent, false);
        return go.AddComponent<SpaceBackdrop>();
    }

    // ------------------------------------------------------------------ views (called by the menu)
    int HeroWorld() {
        LevelDef L = LevelDefs.Levels[Mathf.Clamp(SaveData.UnlockedCount - 1, 0, LevelDefs.Count - 1)];
        return WorldIndex(L.theme, L.night);
    }

    // Big planet low on the left; the title and buttons stay clear on the right.
    public void ShowTitle() { SetPlanetView(HeroWorld(), new Vector2(-0.55f, -0.3f), 0.62f, true); }

    // Small planet far to the side, for pages that cover most of the screen.
    public void ShowWide() { SetPlanetView(HeroWorld(), new Vector2(0.55f, 0.3f), 0.3f, true); }

    // The level page keeps the planet in the open band between the info strip and the level cards.
    public void ShowLevel(int level) {
        LevelDef L = LevelDefs.Levels[Mathf.Clamp(level, 0, LevelDefs.Count - 1)];
        SetPlanetView(WorldIndex(L.theme, L.night), new Vector2(0f, 0.29f), 0.28f, true);
    }

    // Duel: the planet of the chosen arena rises behind the page, its upper half visible under the heading.
    public void ShowWorld(ArenaTheme theme, bool night, SpaceView view) {
        SetPlanetView(WorldIndex(theme, night), new Vector2(0f, 0.55f), 0.75f, false);
    }

    // Choose your class: drifting through a violet nebula.
    public void ShowNebula() { SetFreeView(SpaceScenery.NebulaPos, 95f, Vector2.zero, -10f, 3f, new Color(0.78f, 0.56f, 1f)); }

    // How to play: the observatory, a ringed gas giant rising below, asteroids drifting past.
    public void ShowObservatory() { SetFreeView(SpaceScenery.ObservatoryPos, 52f, new Vector2(0.1f, -1.75f), 0f, -4f, new Color(1f, 0.75f, 0.45f)); }

    // Settings: the station on the right, the galaxy turning behind it.
    public void ShowStation() { SetFreeView(SpaceScenery.StationPos, 120f, new Vector2(0.62f, -0.1f), 12f, -4f, new Color(0.5f, 0.9f, 1f)); }

    void SetPlanetView(int world, Vector2 ndc, float frac, bool campaignRules) {
        float tan = Mathf.Tan(BaseFov * 0.5f * Mathf.Deg2Rad);
        viewWorld = world;
        viewNdc = ndc;
        viewFocus = planets[world].transform.position;
        viewDist = SpacePlanet.Radius / (tan * frac);
        viewYaw = viewPitch = 0f;
        planets[world].EnsureBuilt();
        for (int i = 0; i < planets.Length; i++) planets[i].Seen = !campaignRules || CampaignSeen(i);
        accentTarget = Color.Lerp(SpacePlanet.AtmoColor(Worlds[world].theme, Worlds[world].night), Color.white, 0.15f);
    }

    void SetFreeView(Vector3 focus, float dist, Vector2 ndc, float yaw, float pitch, Color accent) {
        viewFocus = focus;
        viewDist = dist;
        viewNdc = ndc;
        viewYaw = yaw;
        viewPitch = pitch;
        accentTarget = accent;
    }

    // A planet is "discovered" once any unlocked campaign level is fought on it.
    static bool CampaignSeen(int world) {
        for (int l = 0; l < LevelDefs.Count; l++) {
            LevelDef L = LevelDefs.Levels[l];
            if (WorldIndex(L.theme, L.night) == world && SaveData.IsUnlocked(l)) return true;
        }
        return false;
    }

    // Short hyperspace hop when a menu page changes.
    public void WarpPulse() {
        fovKick = 12f;
        WarpOverlay.Pulse(0.75f, 0.45f);
    }

    // ------------------------------------------------------------------ dive into a planet (battle start)
    public void Dive(ArenaTheme theme, bool night) {
        if (Diving) return;
        Diving = true;
        diveLoading = false;
        diveT = 0f;
        diveWorld = WorldIndex(theme, night);
        planets[diveWorld].EnsureBuilt();
        planets[diveWorld].Seen = true;
        diveColor = SpacePlanet.FloodColor(theme, night);
        diveStartPos = cam.transform.position;
        diveStartRot = cam.transform.rotation;
        Tooltip.Hide();
        WarpOverlay.Block(true);
        SFX.Play(SoundId.LaunchHeavy, null, 0.8f, 0.55f);
    }

    void UpdateDive(float dt) {
        diveT += dt / DiveSeconds;
        float t = Mathf.Clamp01(diveT);
        float e = Mathf.Pow(t, 2.2f);                                   // slow start, then it plunges
        Vector3 p = planets[diveWorld].transform.position;
        Vector3 endPos = p + (diveStartPos - p).normalized * (SpacePlanet.Radius * 1.12f);
        Quaternion look = Quaternion.LookRotation(p - endPos) * Quaternion.Euler(0f, 0f, 14f * t);
        cam.transform.SetPositionAndRotation(Vector3.Lerp(diveStartPos, endPos, e),
                                             Quaternion.Slerp(diveStartRot, look, Mathf.SmoothStep(0f, 1f, t)));
        cam.fieldOfView = Mathf.Lerp(BaseFov, 78f, e);

        WarpOverlay.SetBase(Mathf.Clamp01(t * 1.6f));
        WarpOverlay.SetFlood(diveColor, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, t)));
        if (uiGroup != null) uiGroup.alpha = 1f - Mathf.Clamp01(t * 4f);

        // the fade-out of SceneFader reaches full colour just as the dive ends; the battle then fades in from the same colour
        if (!diveLoading && t >= 0.86f) {
            diveLoading = true;
            SceneFader.Load(GameSession.BattleScene, diveColor, 0.25f, 1.0f);
        }
    }

    // ------------------------------------------------------------------ setup
    void Awake() {
        Current = this;
        Diving = false;
        cam = Camera.main;
        if (cam == null) {
            cam = new GameObject("Space Camera", typeof(Camera)).GetComponent<Camera>();
            cam.tag = "MainCamera";
        }
        if (cam.TryGetComponent(out BattleCamera battleCam)) battleCam.enabled = false;

        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.008f, 0.01f, 0.03f);
        cam.nearClipPlane = 0.5f;
        cam.farClipPlane = 6000f;
        cam.fieldOfView = BaseFov;

        SetupLighting();
        planets = new SpacePlanet[Worlds.Length];
        for (int i = 0; i < Worlds.Length; i++) planets[i] = SpacePlanet.Create(transform, Worlds[i].theme, Worlds[i].night, WorldPos(i));
        BuildStars();
        BuildNebulae();
        BuildDust();

        ShowTitle();
        nextShootingStar = Random.Range(2f, 4f);
        StartCoroutine(BuildRest());
    }

    void OnDestroy() {
        if (Current == this) Current = null;
        Diving = false;
    }

    // The other planets are textured one per frame, so the menu opens without a stall.
    IEnumerator BuildRest() {
        for (int i = 0; i < planets.Length; i++)
            while (planets[i].BuildStep()) yield return null;   // a few milliseconds per frame
        yield return null;
        SpaceScenery.BuildNebula(transform);
        yield return null;
        SpaceScenery.BuildObservatory(transform);
        yield return null;
        SpaceScenery.BuildStation(transform);
    }

    void SetupLighting() {
        foreach (Light l in FindObjectsByType<Light>())
            if (l.type == LightType.Directional) l.enabled = false;   // leftovers of the old diorama
        GameObject go = new GameObject("Space Sun");
        go.transform.SetParent(transform, false);
        sun = go.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.95f, 0.85f);
        sun.intensity = 1.7f;
        sun.shadows = LightShadows.None;
        go.transform.rotation = Quaternion.Euler(24f, 36f, 0f);   // light travels away from the viewer: lit from the front-left
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.1f, 0.11f, 0.2f);
        RenderSettings.fog = false;
        RenderSettings.skybox = null;
    }

    // ------------------------------------------------------------------ stars, nebulae, dust
    static Vector3 RandomDir(System.Random rng, float minZ) {
        Vector3 d;
        do {
            d = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f);
        } while (d.sqrMagnitude < 0.05f || d.sqrMagnitude > 1f);
        d.Normalize();
        if (d.z < minZ) d.z = Mathf.Lerp(minZ, 1f, (float)rng.NextDouble());
        return d.normalized;
    }

    void BuildStars() {
        starAnchor = new GameObject("Stars").transform;
        starAnchor.SetParent(transform, false);
        System.Random rng = new System.Random(77);
        StarField("Stars", 1100, 3.5f, 8f, 0.35f, 1f, TexKit.SoftCircle, rng, false);
        StarField("Bright stars", 70, 14f, 28f, 0.8f, 1.5f, TexKit.Star, rng, true);
    }

    void StarField(string name, int count, float minSize, float maxSize, float minBright, float maxBright, Texture2D tex,
                   System.Random rng, bool twinkle) {
        const float Dist = 2400f;
        Vector3[] v = new Vector3[count * 4];
        Vector2[] uv = new Vector2[count * 4];
        Color[] col = new Color[count * 4];
        int[] tri = new int[count * 6];
        for (int i = 0; i < count; i++) {
            Vector3 dir = RandomDir(rng, -1f);
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            if (right.sqrMagnitude < 1e-4f) right = Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(dir, right);
            float size = Mathf.Lerp(minSize, maxSize, (float)rng.NextDouble()) * 0.5f * (Dist / 1500f);
            float k = Mathf.Lerp(minBright, maxBright, (float)rng.NextDouble());
            double tint = rng.NextDouble();
            Color c = tint < 0.2 ? new Color(1f, 0.86f, 0.72f) : tint < 0.45 ? new Color(0.75f, 0.85f, 1f) : Color.white;
            c = new Color(c.r * k, c.g * k, c.b * k, Mathf.Clamp01(k));
            Vector3 centre = dir * Dist;
            int b = i * 4;
            v[b] = centre - right * size - up * size;
            v[b + 1] = centre - right * size + up * size;
            v[b + 2] = centre + right * size + up * size;
            v[b + 3] = centre + right * size - up * size;
            uv[b] = new Vector2(0f, 0f);
            uv[b + 1] = new Vector2(0f, 1f);
            uv[b + 2] = new Vector2(1f, 1f);
            uv[b + 3] = new Vector2(1f, 0f);
            col[b] = col[b + 1] = col[b + 2] = col[b + 3] = c;
            int t = i * 6;
            tri[t] = b; tri[t + 1] = b + 1; tri[t + 2] = b + 2;
            tri[t + 3] = b; tri[t + 4] = b + 2; tri[t + 5] = b + 3;
        }
        Mesh m = new Mesh { name = name, vertices = v, uv = uv, colors = col, triangles = tri };
        m.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
        GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(starAnchor, false);
        go.GetComponent<MeshFilter>().sharedMesh = m;
        MeshRenderer r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = Mats.ParticleAdditive(tex);
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (twinkle) go.AddComponent<StarTwinkle>().Init(m);   // WorldMotion's twinkle works on any 4-vertex star mesh
    }

    void BuildNebulae() {
        nebulaAnchor = new GameObject("Nebulae").transform;
        nebulaAnchor.SetParent(transform, false);
        System.Random rng = new System.Random(311);
        Color[] tints = {
            new Color(0.45f, 0.2f, 0.9f), new Color(0.15f, 0.45f, 1f), new Color(0.9f, 0.25f, 0.6f),
            new Color(0.1f, 0.8f, 0.8f), new Color(0.6f, 0.3f, 1f),
        };
        for (int i = 0; i < tints.Length; i++) {
            int seed = 400 + i * 17;
            Texture2D tex = SpaceNoise.Make("Nebula " + i, 256, 256, (u, v) => {
                float dx = u - 0.5f, dy = v - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float mask = Mathf.Pow(Mathf.Clamp01(1f - r), 1.5f);
                float n = SpaceNoise.Fbm(u, v, 4, 5, seed);
                float core = SpaceNoise.Fbm(u, v, 8, 3, seed + 9);
                float a = SpaceNoise.Smooth(0.38f, 0.75f, n) * mask * (0.6f + 0.4f * core);
                return new Color(1f, 1f, 1f, a * 0.9f);
            }, false);
            Material m = new Material(Mats.ParticleAdditive(tex)) { name = "Nebula " + i };
            m.SetColor("_BaseColor", tints[i] * 0.55f);

            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Nebula " + i;
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(nebulaAnchor, false);
            Vector3 dir = new Vector3((float)rng.NextDouble() * 2f - 1f, ((float)rng.NextDouble() * 2f - 1f) * 0.6f, 0.3f + (float)rng.NextDouble() * 0.7f).normalized;
            q.transform.position = dir * 2600f;
            q.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 360f);
            float size = 2600f + (float)rng.NextDouble() * 1000f;
            q.transform.localScale = new Vector3(size, size, 1f);
            MeshRenderer r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    // Slow dust around the camera: it stays where it was emitted, so flying through it shows how fast you move.
    void BuildDust() {
        GameObject go = new GameObject("Dust");
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 60f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.duration = 16f;
        main.loop = true;
        main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 16f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.35f);
        main.startColor = new Color(0.72f, 0.82f, 1f, 0.5f);
        main.maxParticles = 700;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 45f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(260f, 150f, 200f);
        ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Mats.ParticleAdditive(TexKit.SoftCircle);
        ps.Play();
    }

    void SpawnShootingStar() {
        Transform c = cam.transform;
        Vector3 start = c.position + c.forward * 380f + c.right * Random.Range(-260f, 260f) + c.up * Random.Range(40f, 200f);
        Vector3 dir = (c.right * (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.6f, 1f) - c.up * Random.Range(0.2f, 0.6f)).normalized;
        GameObject go = new GameObject("Shooting star");
        go.transform.SetParent(transform, false);
        go.transform.position = start;
        TrailRenderer tr = go.AddComponent<TrailRenderer>();
        tr.time = 0.6f;
        tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        tr.widthMultiplier = 2.4f;
        tr.sharedMaterial = FxAssets.Additive(FxAssets.Beam, 2.2f);
        tr.alignment = LineAlignment.View;
        tr.textureMode = LineTextureMode.Stretch;
        tr.startColor = new Color(0.95f, 0.97f, 1f, 1f);
        tr.endColor = new Color(0.5f, 0.6f, 1f, 0f);
        tr.shadowCastingMode = ShadowCastingMode.Off;
        tr.receiveShadows = false;
        go.AddComponent<ShootingStar>().velocity = dir * Random.Range(380f, 520f);
    }

    // ------------------------------------------------------------------ camera
    void LateUpdate() {
        TimeFX.Tick();   // there is no BattleManager here, so a stray slow-motion would otherwise stick
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        float dt = Time.unscaledDeltaTime;
        time += dt;
        UIKit.LiveAccent = Color.Lerp(UIKit.LiveAccent, accentTarget, 1f - Mathf.Exp(-3f * dt));   // buttons follow the scenery

        if (Diving) UpdateDive(dt);
        else UpdateCruise(dt);

        starAnchor.position = cam.transform.position;
        nebulaAnchor.position = cam.transform.position;
        nebulaAnchor.Rotate(0f, 0.35f * dt, 0f, Space.World);

        nextShootingStar -= dt;
        if (nextShootingStar <= 0f && !Diving) {
            SpawnShootingStar();
            nextShootingStar = Random.Range(5f, 11f);
        }
    }

    void UpdateCruise(float dt) {
        float tan = Mathf.Tan(BaseFov * 0.5f * Mathf.Deg2Rad);
        float h = viewDist * tan;
        float aspect = Mathf.Max(1.2f, cam.aspect);
        Quaternion vr = Quaternion.Euler(viewPitch, viewYaw, 0f);
        Vector3 target = viewFocus + vr * new Vector3(-viewNdc.x * h * aspect, -viewNdc.y * h, -viewDist);
        if (!placed) { camPos = target; viewRot = vr; placed = true; }
        camPos = Vector3.SmoothDamp(camPos, target, ref camVel, 1.2f, Mathf.Infinity, dt);
        viewRot = Quaternion.Slerp(viewRot, vr, 1f - Mathf.Exp(-3f * dt));

        // arriving from hyperspace: starts pulled back with a wide lens and settles
        introK = Mathf.MoveTowards(introK, 0f, dt / 1.8f);
        float ik = introK * introK * (3f - 2f * introK);
        fovKick = Mathf.Lerp(fovKick, 0f, 1f - Mathf.Exp(-6f * dt));

        Quaternion sway = Quaternion.Euler(Mathf.Sin(time * 0.11f) * 0.8f, Mathf.Sin(time * 0.07f) * 1.4f, Mathf.Sin(time * 0.05f) * 0.6f);
        Vector3 bob = new Vector3(Mathf.Sin(time * 0.13f) * 0.8f, Mathf.Sin(time * 0.17f) * 0.6f, 0f);
        cam.transform.SetPositionAndRotation(camPos + bob + Vector3.back * (160f * ik), viewRot * sway);
        cam.fieldOfView = BaseFov + fovKick + 26f * ik;

        // streaks follow the flight: sideways hops are gentle, flying towards a place stronger
        float level = new Vector2(camVel.x, camVel.y).magnitude / 420f + Mathf.Abs(camVel.z) / 150f + ik * 0.8f;
        WarpOverlay.SetBase(level);
    }
}

// A star that crosses the far sky and leaves a short trail.
public class ShootingStar : MonoBehaviour {
    public Vector3 velocity;
    const float Life = 0.9f;
    float t;
    TrailRenderer trail;

    void Awake() { trail = GetComponent<TrailRenderer>(); }

    void Update() {
        t += Time.unscaledDeltaTime;
        if (t < Life) {
            transform.position += velocity * Time.unscaledDeltaTime;
        } else {
            if (trail != null) trail.emitting = false;
            if (t > Life + 0.8f) Destroy(gameObject);
        }
    }
}

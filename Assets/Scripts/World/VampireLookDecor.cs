using System.Collections.Generic;
using UnityEngine;

// The Vampire's fortress decorations (visual only, parented to blocks so they fall with them, no colliders):
// steep black slate spires with corner pinnacles and a glowing lancet window on the roof turrets (the team flag
// FortressDecor planted on the turret is moved up to the spire's tip), pointed slate caps with iron spikes on
// the merlons, glowing blood-red rose windows, iron sconces with three burning candles, a spiked iron railing
// and a tombstone in front of the walls, and a few bats circling the main spire. At night up to MaxLights
// candle lights. FortressDecor has already placed the team flag and banners.
public static class VampireDecor {
    const int MaxLights = 3;
    const float SpireHeight = 2.2f;
    const float FlagSink = 0.3f;           // the flag pole's foot hides in the spire's tip

    static readonly Color SlateTile = new Color(0.2f, 0.16f, 0.22f), SlateShadow = new Color(0.05f, 0.04f, 0.06f);
    static readonly Color Tomb = new Color(0.36f, 0.34f, 0.38f);
    static Texture2D roseTexture;

    static Material Slate => DecorTextures.RoofTiles(SlateTile, SlateShadow);
    static Material Iron => VampireKit.IronMat;

    public static void Apply(Fortress f) {
        FortressPlan plan = f.blueprint as FortressPlan;
        int facing = f.Facing >= 0 ? 1 : -1, lights = 0;
        bool night = ArenaBuilder.IsNight;
        Block main = null;          // the turret with the flag: the bats circle its spire
        Transform mainWindow = null;
        if (plan != null && f.blocks.Count == plan.blocks.Count) {   // anchors need the original block order
            HashSet<int> flagged = new HashSet<int>();
            foreach (DecorAnchor a in plan.decor) if (a.kind == DecorKind.Flag) flagged.Add(a.block);
            foreach (DecorAnchor a in plan.decor) {
                Block host = a.block >= 0 && a.block < f.blocks.Count ? f.blocks[a.block] : null;
                if (host == null) continue;
                switch (a.kind) {
                    case DecorKind.TurretRoof:
                        Transform window = Spire(host, a.local);
                        if (main == null || flagged.Contains(a.block)) { main = host; mainWindow = window; }
                        break;
                    case DecorKind.Shield: RoseWindow(host.transform, a.local); break;
                    case DecorKind.Torch: Sconce(host.transform, a.local, night && lights < MaxLights, ref lights); break;
                    case DecorKind.Base: Railing(host, a.local, a.span, facing); break;
                }
            }
        }
        if (night && lights < MaxLights && mainWindow != null) Lamp(mainWindow, new Vector3(0f, 0.2f, -0.35f), VampireKit.BloodGlow, 1.6f, 5f, false);
        Caps(f.blocks);
        if (main != null) VampireDecorBats.Create(f, main);
        VampireSounds.WarmUp();   // a vampire is in this battle: get his sounds ready
    }

    // ------------------------------------------------------------------ the spires
    // A steep octagonal spire of dark slate on the turret, an iron eaves band, a slim pinnacle at each corner of the
    // turret and a glowing lancet window on its front. The team flag flies from the tip; a turret without a flag
    // gets an iron finial instead. Returns the window.
    static Transform Spire(Block turret, Vector3 at) {
        Transform host = turret.transform;
        Vector3 size = turret.size;
        Transform root = Root("Gothic Spire", host, at);
        float r = 0.5f * Mathf.Min(size.x, size.z) * 1.15f;
        Vector3 octagon = new Vector3(0f, 22.5f, 0f);   // a flat face to the camera
        Part(root, WorldMeshes.Frustum(8, r, 0f, SpireHeight), Vector3.zero, Slate, octagon);
        Part(root, WorldMeshes.Frustum(8, r + 0.05f, r + 0.03f, 0.1f), Vector3.zero, Iron, octagon);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Part(root, Pinnacle(), new Vector3(sx * (size.x * 0.5f - 0.1f), 0f, sz * (size.z * 0.5f - 0.1f)), Slate);
        Transform flag = host.Find("Flag");   // planted by FortressDecor on the turret's top
        if (flag != null) flag.localPosition = at + Vector3.up * (SpireHeight - FlagSink);
        else Part(root, Finial(), Vector3.up * (SpireHeight - 0.05f), Iron);
        return Window(host, new Vector3(0f, -size.y * 0.2f, -size.z * 0.5f), 0.22f, 0.32f);
    }

    // A slim four-sided pinnacle on a small square base.
    static Mesh Pinnacle() {
        return VampireKit.Cached("vampirePinnacle", () => VampireKit.Merge("Pinnacle",
            new VampireKit.Piece(WorldMeshes.Frustum(4, 0.1f, 0f, 0.75f), Vector3.zero, new Vector3(0f, 45f, 0f), Vector3.one),
            new VampireKit.Piece(WorldMeshes.Frustum(4, 0.12f, 0.11f, 0.06f), Vector3.zero, new Vector3(0f, 45f, 0f), Vector3.one)));
    }

    // An iron spike with a ball, crowning a spire.
    static Mesh Finial() {
        return VampireKit.Cached("vampireFinial", () => VampireKit.Merge("Finial",
            new VampireKit.Piece(WorldMeshes.Frustum(6, 0.035f, 0f, 0.55f), Vector3.zero, Vector3.zero, Vector3.one),
            new VampireKit.Piece(WorldMeshes.Ball(0.06f), Vector3.up * 0.12f, Vector3.zero, Vector3.one)));
    }

    // A pointed (lancet) window glowing blood red in a black iron frame, on a face that looks at the camera.
    // `at` = the centre of its sill; w = width, h = height of the straight sides.
    static Transform Window(Transform host, Vector3 at, float w, float h) {
        Transform root = Root("Lancet Window", host, at + new Vector3(0f, 0f, -0.012f));
        Part(root, LancetMesh(w + 0.07f, h + 0.035f), new Vector3(0f, -0.035f, 0.004f), Iron);
        GameObject glass = Part(root, LancetMesh(w, h), Vector3.zero, VampireKit.GlowRed);
        ModelKit.Shadows(glass, false, false);
        return root;
    }

    // A flat lancet facing -z: straight sides h high, then two arcs of radius w meeting in a point (a gothic arch).
    static Mesh LancetMesh(float w, float h) {
        return VampireKit.Cached("lancet" + w.ToString("0.000") + "_" + h.ToString("0.000"), () => {
            List<Vector3> outline = new List<Vector3>();
            float half = w * 0.5f;
            for (int k = 0; k <= 6; k++) {   // the right arc (centred on the left springing point), top to bottom
                float a = Mathf.Lerp(60f, 0f, k / 6f) * Mathf.Deg2Rad;
                outline.Add(new Vector3(-half + Mathf.Cos(a) * w, h + Mathf.Sin(a) * w, 0f));
            }
            outline.Add(new Vector3(half, 0f, 0f));
            outline.Add(new Vector3(-half, 0f, 0f));
            for (int k = 0; k < 6; k++) {    // the left arc, bottom to (not including) the top
                float a = Mathf.Lerp(180f, 120f, k / 6f) * Mathf.Deg2Rad;
                outline.Add(new Vector3(half + Mathf.Cos(a) * w, h + Mathf.Sin(a) * w, 0f));
            }
            // The outline runs clockwise as the camera sees it, so a fan from inside faces -z.
            MeshBuilder b = new MeshBuilder();
            int c = b.Vertex(new Vector3(0f, h * 0.7f, 0f), new Vector2(0.5f, 0.5f));
            int first = b.Vertex(outline[0], Vector2.zero), prev = first;
            for (int i = 1; i < outline.Count; i++) {
                int v = b.Vertex(outline[i], Vector2.zero);
                b.Triangle(c, prev, v);
                prev = v;
            }
            b.Triangle(c, prev, first);
            return b.ToMesh("Lancet");
        });
    }

    // ------------------------------------------------------------------ walls
    // A round rose window of blood-red glass in black tracery that glows day and night (a "shield" anchor).
    static void RoseWindow(Transform host, Vector3 at) {
        Transform root = Root("Rose Window", host, at + new Vector3(0f, 0f, -0.02f));
        GameObject glass = Part(root, WorldMeshes.Quad(), Vector3.zero, Mats.UnlitTransparent(new Color(2.2f, 2.2f, 2.2f, 1f), RoseTexture),
                                default, Vector3.one * 0.66f);
        ModelKit.Shadows(glass, false, false);
    }

    // An iron wall sconce with three candles of different heights, burning day and night (flickering flames);
    // at night it also lights the wall while the fortress has lights left.
    static void Sconce(Transform host, Vector3 at, bool light, ref int lights) {
        Transform root = Root("Candle Sconce", host, at);
        Part(root, SconceMesh(), Vector3.zero, Iron);
        Part(root, CandlesMesh(), Vector3.zero, VampireKit.WaxMat);
        Transform flicker = Root("Flames", root, new Vector3(0f, 0.27f, -0.26f));   // scaled from the candle tops
        GameObject flames = Part(flicker, FlamesMesh(), new Vector3(0f, -0.27f, 0.26f), VampireKit.FlameMat);
        ModelKit.Shadows(flames, false, false);
        VampireFlicker f = flicker.gameObject.AddComponent<VampireFlicker>();
        if (light) {
            lights++;
            f.lamp = Lamp(root, new Vector3(0f, 0.45f, -0.55f), new Color(1f, 0.6f, 0.28f), 1.8f, 6f, true);
        }
    }

    // (Merged parts are built from runtime meshes only: their vertices can always be read.)
    static Mesh SconceMesh() {
        return VampireKit.Cached("vampireSconce", () => VampireKit.Merge("Sconce",
            new VampireKit.Piece(MeshKit.Box(new Vector3(0.12f, 0.22f, 0.03f)), new Vector3(0f, 0f, -0.015f), Vector3.zero, Vector3.one),    // wall plate
            new VampireKit.Piece(MeshKit.Box(new Vector3(0.035f, 0.035f, 0.22f)), new Vector3(0f, -0.02f, -0.13f), Vector3.zero, Vector3.one), // arm
            new VampireKit.Piece(WorldMeshes.Frustum(8, 0.12f, 0.15f, 0.03f), new Vector3(0f, 0f, -0.26f), Vector3.zero, Vector3.one),   // tray
            new VampireKit.Piece(WorldMeshes.Frustum(4, 0.025f, 0f, 0.12f), new Vector3(0f, 0.11f, -0.015f), Vector3.zero, Vector3.one)));   // spike
    }

    static readonly float[] CandleX = { -0.075f, 0f, 0.075f }, CandleH = { 0.17f, 0.24f, 0.14f };

    static Mesh CandlesMesh() {
        return VampireKit.Cached("vampireCandles", () => {
            VampireKit.Piece[] candles = new VampireKit.Piece[CandleX.Length];
            for (int i = 0; i < candles.Length; i++)
                candles[i] = new VampireKit.Piece(WorldMeshes.Frustum(8, 0.026f, 0.024f, CandleH[i]), new Vector3(CandleX[i], 0.03f, -0.26f), Vector3.zero, Vector3.one);
            return VampireKit.Merge("Candles", candles);
        });
    }

    static Mesh FlamesMesh() {
        return VampireKit.Cached("vampireFlames", () => {
            VampireKit.Piece[] flames = new VampireKit.Piece[CandleX.Length];
            for (int i = 0; i < flames.Length; i++)
                flames[i] = new VampireKit.Piece(WorldMeshes.Ball(0.026f), new Vector3(CandleX[i], 0.075f + CandleH[i], -0.26f), Vector3.zero, new Vector3(1f, 1.9f, 1f));
            return VampireKit.Merge("Candle Flames", flames);
        });
    }

    // ------------------------------------------------------------------ in front of the walls
    // A spiked wrought-iron railing running from the front wall towards the enemy, with a leaning tombstone behind it.
    // `clear` = how far beyond the pillar's outer face the ground is free (a plinth may stand there).
    static void Railing(Block pillar, Vector3 at, float clear, int facing) {
        Transform root = Root("Iron Railing", pillar.transform, at + new Vector3(facing * (pillar.size.x * 0.5f + clear + 0.15f), 0f, -0.5f));
        root.localScale = new Vector3(facing, 1f, 1f);   // it runs away from the wall
        Part(root, RailingMesh(), Vector3.zero, Iron);
        Part(root, TombstoneMesh(), new Vector3(0.95f, 0f, 0.42f), Mats.Lit(Tomb, 0.2f), new Vector3(0f, 8f, -7f));
    }

    static Mesh RailingMesh() {
        return VampireKit.Cached("vampireRailing", () => {
            Mesh rail = MeshKit.Box(new Vector3(1.64f, 0.035f, 0.035f));
            List<VampireKit.Piece> parts = new List<VampireKit.Piece> {
                new VampireKit.Piece(rail, new Vector3(0.8f, 0.16f, 0f), Vector3.zero, Vector3.one),
                new VampireKit.Piece(rail, new Vector3(0.8f, 0.68f, 0f), Vector3.zero, Vector3.one),
            };
            for (int i = 0; i < 9; i++) {
                float x = i * 0.2f;
                float h = i % 2 == 0 ? 0.86f : 0.78f;
                parts.Add(new VampireKit.Piece(WorldMeshes.Frustum(4, 0.018f, 0.018f, h), new Vector3(x, 0f, 0f), new Vector3(0f, 45f, 0f), Vector3.one));
                parts.Add(new VampireKit.Piece(MeshKit.Cone(4, 0.045f, 0.13f), new Vector3(x, h, 0f), Vector3.zero, Vector3.one));
            }
            return VampireKit.Merge("Iron Railing", parts.ToArray());
        });
    }

    static Mesh TombstoneMesh() {
        return VampireKit.Cached("vampireTomb", () => VampireKit.Merge("Tombstone",
            new VampireKit.Piece(MeshKit.Box(new Vector3(0.38f, 0.5f, 0.1f)), new Vector3(0f, 0.25f, 0f), Vector3.zero, Vector3.one),
            new VampireKit.Piece(WorldMeshes.Dome(10, 2, 0.19f), new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(1f, 0.7f, 0.26f))));
    }

    // ------------------------------------------------------------------ the merlons
    // A pointed slate cap with an iron spike on every small block that nothing rests on (the merlons on the roofs,
    // not crates standing inside a bay).
    static void Caps(List<Block> blocks) {
        foreach (Block b in blocks) {
            if (b == null || b.size.x > 0.75f || b.size.y > 0.7f) continue;
            if (b.material == BlockMaterial.Glass || b.material == BlockMaterial.Ice || b.material == BlockMaterial.Explosive) continue;
            if (!OpenAbove(b, blocks)) continue;
            Transform root = Root("Pointed Cap", b.transform, new Vector3(0f, b.size.y * 0.5f, 0f));
            GameObject cap = Part(root, CapMesh(b.size.x, b.size.z), Vector3.zero, Slate);
            cap.GetComponent<MeshRenderer>().sharedMaterials = new[] { Slate, Iron };
        }
    }

    // Blocks still stand where they were built, axis-aligned: nothing above = no block whose bottom is at or above
    // this one's top and overlaps it across.
    static bool OpenAbove(Block b, List<Block> blocks) {
        Vector3 p = b.transform.position;
        float top = p.y + b.size.y * 0.5f, x0 = p.x - b.size.x * 0.5f, x1 = p.x + b.size.x * 0.5f;
        foreach (Block o in blocks) {
            if (o == null || o == b) continue;
            Vector3 q = o.transform.position;
            if (q.y - o.size.y * 0.5f < top - 0.05f) continue;
            if (q.x + o.size.x * 0.5f > x0 + 0.05f && q.x - o.size.x * 0.5f < x1 - 0.05f) return false;
        }
        return true;
    }

    static Mesh CapMesh(float width, float depth) {
        float h = Mathf.Min(0.6f, width * 1.1f);
        return VampireKit.Cached("vampireCap" + width.ToString("0.00") + "_" + depth.ToString("0.00"), () => VampireKit.Merge("Pointed Cap",
            new VampireKit.Piece(WorldMeshes.Pyramid(width * 0.5f + 0.03f, depth * 0.5f + 0.03f, h), Vector3.zero, Vector3.zero, Vector3.one),
            new VampireKit.Piece(WorldMeshes.Frustum(4, 0.03f, 0f, 0.3f), Vector3.up * (h - 0.06f), Vector3.zero, Vector3.one, 1)));
    }

    // ------------------------------------------------------------------ helpers
    static Light Lamp(Transform parent, Vector3 at, Color color, float intensity, float range, bool candle) {
        GameObject go = ModelKit.Root(candle ? "Candle Light" : "Window Light", parent);
        go.transform.localPosition = at;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    // A rose window: eight petals of red and purple glass, each with a round light, around a glowing centre, in
    // black iron tracery (spokes, two rings, a thick rim). Distances are in units of the window's radius; the
    // alpha makes it round.
    static Texture2D RoseTexture {
        get {
            if (roseTexture != null) return roseTexture;
            const int N = 128;
            const float Sector = Mathf.PI / 4f;
            Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "Rose Window", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            Color[] px = new Color[N * N];
            Color lead = new Color(0.03f, 0.02f, 0.03f, 1f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++) {
                    float u = (x + 0.5f) / N - 0.5f, v = (y + 0.5f) / N - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v) * 2f, a = Mathf.Atan2(v, u) + Mathf.PI;
                    int petal = Mathf.FloorToInt(a / Sector) & 7;
                    float local = Mathf.Repeat(a, Sector) - Sector * 0.5f;          // angle from the middle of this petal
                    float spoke = r * Mathf.Sin(Sector * 0.5f - Mathf.Abs(local));   // distance to the nearest spoke
                    float ring = Mathf.Min(Mathf.Abs(r - 0.55f), Mathf.Abs(r - 0.24f));
                    float bx = r * Mathf.Cos(local) - 0.72f, by = r * Mathf.Sin(local);
                    float bulb = Mathf.Abs(Mathf.Sqrt(bx * bx + by * by) - 0.1f);    // the petal's round light
                    Color c = petal % 2 == 0 ? new Color(0.85f, 0.05f, 0.1f) : new Color(0.45f, 0.03f, 0.22f);
                    c *= Mathf.Lerp(1.1f, 0.6f, r);
                    if (r < 0.18f) c = new Color(1f, 0.3f, 0.32f);
                    if ((spoke < 0.035f && r > 0.2f) || ring < 0.025f || bulb < 0.018f || r > 0.88f) c = lead;
                    c.a = Mathf.Clamp01((1f - r) * 40f);
                    px[y * N + x] = c;
                }
            t.SetPixels(px);
            t.Apply(true);
            roseTexture = t;
            return t;
        }
    }

    static Transform Root(string name, Transform parent, Vector3 at) {
        Transform t = ModelKit.Root(name, parent).transform;
        t.localPosition = at;
        return t;
    }

    static GameObject Part(Transform parent, Mesh mesh, Vector3 at, Material m, Vector3 euler = default, Vector3 scale = default) {
        return ModelKit.MeshPart(parent, mesh, at, scale == default ? Vector3.one : scale, m, euler);
    }
}

// Candle flames that flicker (stretch and shrink), and the light they cast, if any.
public class VampireFlicker : MonoBehaviour {
    public Light lamp;
    float seed, intensity;
    Vector3 baseScale;

    void Start() {
        seed = Random.value * 50f;
        baseScale = transform.localScale;
        if (lamp != null) intensity = lamp.intensity;
    }

    void Update() {
        float n = Mathf.PerlinNoise(seed, Time.time * 8f);
        transform.localScale = new Vector3(baseScale.x, baseScale.y * (0.82f + 0.36f * n), baseScale.z);
        if (lamp != null) lamp.intensity = intensity * (0.75f + 0.45f * n);
    }
}

// A few bats circling the spire of the main turret, passing in front of and behind it. When the turret is
// destroyed or the fortress falls they scatter.
public class VampireDecorBats : MonoBehaviour {
    const int Count = 4;

    Fortress fortress;
    Block host;
    Transform[] bats;
    float[] angle, speed, radius, height;

    public static void Create(Fortress f, Block host) {
        VampireDecorBats d = ModelKit.Root("Circling Bats", f.transform).AddComponent<VampireDecorBats>();
        d.fortress = f;
        d.host = host;
        d.bats = new Transform[Count];
        d.angle = new float[Count];
        d.speed = new float[Count];
        d.radius = new float[Count];
        d.height = new float[Count];
        for (int i = 0; i < Count; i++) {
            d.bats[i] = VampireKit.Bat(d.transform, Random.Range(0.32f, 0.42f)).transform;
            d.angle[i] = i * Mathf.PI * 2f / Count + Random.Range(-0.4f, 0.4f);
            d.speed[i] = Random.Range(1.1f, 1.7f) * (i % 2 == 0 ? 1f : -1f);
            d.radius[i] = Random.Range(1.1f, 1.8f);
            d.height[i] = host.size.y * 0.5f + Random.Range(0.9f, 2.4f);
        }
        d.Circle(0f);
    }

    void Update() {
        if (host == null || !host.IsAlive || fortress == null || fortress.defeated) { Scatter(); return; }
        Circle(Time.deltaTime);
    }

    void Circle(float dt) {
        Vector3 c = host.transform.position;
        for (int i = 0; i < bats.Length; i++) {
            angle[i] += speed[i] * dt;
            float a = angle[i];
            bats[i].position = c + new Vector3(Mathf.Cos(a) * radius[i], height[i] + Mathf.Sin(a * 2f + i) * 0.3f, Mathf.Sin(a) * radius[i] * 0.7f);
            bats[i].rotation = Quaternion.Euler(0f, 0f, -Mathf.Sin(a) * 25f * Mathf.Sign(speed[i]));   // bank into the turn
        }
    }

    void Scatter() {
        foreach (Transform b in bats)
            if (b != null) VampireFlyAway.Launch(b, new Vector3(Random.Range(-4f, 4f), Random.Range(3f, 6f), Random.Range(-1f, 1f)), 1.6f);
        Destroy(gameObject);
    }
}

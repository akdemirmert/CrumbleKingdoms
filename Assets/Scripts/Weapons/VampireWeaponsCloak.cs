using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Blood Cloak (self-cast). The cloak leaves the vampire's shoulders, flies up above his chamber spinning, spreads
// its bat wings and drops over the chamber (VampireCloakFlight). Then the draped cloak (VampireBloodCloak, a
// TurnEffect) guards him: it takes every hit meant for the commander ("BLOCKED"), until the end of the first
// enemy turn in which it blocked something or until two of its owner's turns have passed, and then tears apart
// into a flock of bats.
public class VampireCloakFlight : Projectile {
    const float RiseTime = 0.5f, DrapeTime = 0.65f;
    const float RiseHeight = 5.5f;         // above the cloak's resting place
    const float StartScale = 0.16f;

    Commander commander;
    VampireCloakModel cloak;
    Vector3 start;
    float t;

    public override bool CanActivate => false;

    // The aim means nothing: the cloak always goes over the caster's own chamber.
    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        VampireSounds.Play(VampireSounds.CloakFlap, origin, 0.9f);
        VampireBloodCloak worn = VampireBloodCloak.On(owner);
        if (worn != null) worn.Cancel();   // a fresh cloak replaces one still worn
        WeaponFactory.Spawn<VampireCloakFlight>("Blood Cloak", owner, def, origin, Vector3.zero);
    }

    // A small, static copy of the cloak (menus and icons).
    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Blood Cloak", parent);
        VampireCloakModel model = VampireCloakModel.Create(root.transform, false);
        model.transform.localScale = Vector3.one * 0.25f;
        model.transform.localPosition = new Vector3(0f, -0.48f, 0f);
        return root;
    }

    protected override void OnLaunch(Vector3 velocity) {
        maxLifetime = 5f;
        commander = owner != null ? owner.commander : null;
        start = transform.position;
        cloak = VampireCloakModel.Create(transform, true);
        cloak.transform.localScale = Vector3.one * StartScale;
        for (int i = 0; i < 4; i++)   // a few bats burst from the vampire's shoulders with it
            VampireKit.FreeBat(start, 0.35f, new Vector3(Random.Range(-3f, 3f), Random.Range(2f, 5f), Random.Range(-1f, 0f)), 0.9f);
        VFX.MagicBurst(start, VampireKit.Crimson, 0.7f);
    }

    void Update() {
        if (finished) return;
        if (commander == null || !commander.IsAlive) { Fizzle(); return; }
        t += Time.deltaTime;
        Vector3 rest = VampireBloodCloak.Rest(commander);
        Vector3 high = rest + Vector3.up * RiseHeight;
        Transform model = cloak.transform;
        if (t < RiseTime) {
            float k = t / RiseTime;
            transform.position = Vector3.Lerp(start, high, 1f - (1f - k) * (1f - k));   // shoots up, slowing
            model.localRotation = Quaternion.Euler(0f, k * 540f, 0f);
            return;
        }
        float d = Mathf.Clamp01((t - RiseTime) / DrapeTime);
        transform.position = Vector3.Lerp(high, rest, d * d * (3f - 2f * d));   // falls open over the chamber
        model.localScale = Vector3.one * Mathf.Lerp(StartScale, 1f, Mathf.Sqrt(d));
        model.localRotation = Quaternion.Euler(0f, 540f + d * 180f, 0f);        // ends square to the camera
        if (d >= 1f) Arrive();
    }

    // The cloak settles: the TurnEffect takes the model over and the flight is done.
    void Arrive() {
        cloak.transform.localRotation = Quaternion.identity;
        cloak.transform.localScale = Vector3.one;
        VampireBloodCloak.Create(owner, cloak);
        cloak = null;
        Vector3 c = commander.AimPoint;
        VampireSounds.Play(VampireSounds.Drape, c, 0.9f);
        VFX.Ring(c + Vector3.up * 1f, 3f, VampireKit.BloodGlow, 0.5f, 0.3f);
        VFX.LightFlash(c, VampireKit.BloodGlow, 7f, 9f, 0.4f);
        BattleCamera.Shake(0.2f);
        Finish(true);
    }

    // Nobody left to wear it: it bursts in the air.
    void Fizzle() {
        if (cloak != null) cloak.Burst(8);
        Finish(true);
    }

    protected override void OnLost() { Fizzle(); }
}

// The cloak draped over the vampire's chamber. While it is there it absorbs every hit meant for the commander
// (Commander.shield). It tears apart at the start of the turn after the first enemy turn in which it blocked
// something, or when its owner's second turn after the cast begins (so it lasts through two enemy turns at most).
public class VampireBloodCloak : TurnEffect {
    const int Turns = 2;
    const float FeedbackEvery = 0.3f;      // "BLOCKED" text and sound at most this often (a bat swarm bites ten times)
    const float SwellTime = 0.3f;

    Commander commander;
    VampireCloakModel model;
    System.Func<DamageInfo, bool> shieldFn;
    int ownerTurns;
    int spentTurn = -1;                    // the enemy turn in which it first blocked something
    bool tearing;
    float lastFeedback = -10f;

    public override string StatusText => "Cloak " + Mathf.Max(1, Turns - ownerTurns);
    public override Color StatusColor => VampireKit.Crimson;

    // The cloak `f` is wearing, if any.
    public static VampireBloodCloak On(Fortress f) {
        foreach (TurnEffect e in All)
            if (e is VampireBloodCloak cloak && cloak.owner == f && !cloak.tearing) return cloak;
        return null;
    }

    // Where the cloak hangs: around the commander, its hem a little below his feet.
    public static Vector3 Rest(Commander c) {
        Vector3 p = c.transform.position;
        return new Vector3(p.x, p.y - 0.35f, 0f);
    }

    public static VampireBloodCloak Create(Fortress owner, VampireCloakModel model) {
        GameObject go = new GameObject("Blood Cloak");
        go.transform.position = Rest(owner.commander);
        VampireBloodCloak cloak = go.AddComponent<VampireBloodCloak>();
        cloak.Init(owner, owner);
        cloak.commander = owner.commander;
        cloak.model = model;
        model.transform.SetParent(go.transform, true);
        cloak.shieldFn = cloak.Absorb;
        cloak.commander.shield = cloak.shieldFn;
        return cloak;
    }

    void Update() {
        if (tearing) return;
        if (commander == null || !commander.IsAlive) {   // drowned or crushed by something no shield can stop
            StartCoroutine(Tear(false));
            return;
        }
        transform.position = Vector3.Lerp(transform.position, Rest(commander), 1f - Mathf.Exp(-8f * Time.deltaTime));
    }

    // The shield: every hit is taken by the cloak. One taken in an enemy's turn uses the cloak up.
    bool Absorb(DamageInfo d) {
        if (tearing) return false;
        BattleManager bm = BattleManager.I;
        if (spentTurn < 0 && bm != null && bm.Current != null && owner != null && bm.Current.team != owner.team) spentTurn = bm.Turn;
        if (Time.time >= lastFeedback + FeedbackEvery) {
            lastFeedback = Time.time;
            VFX.FloatingText(commander.transform.position + Vector3.up * 2.7f, "BLOCKED", new Color(1f, 0.45f, 0.5f), 1.1f);
            VampireSounds.Play(VampireSounds.CloakBlock, commander.AimPoint, 0.8f);
        }
        VFX.Sparks(d.point == Vector3.zero ? commander.AimPoint : d.point, VampireKit.BloodGlow, 8);
        if (model != null) model.Flash();
        return true;
    }

    // Runs at the start of every turn. A block in an enemy turn uses the cloak up once that turn is over (a hit at
    // the start of the turn, like the painted sun's, still leaves it for the enemy's shot).
    public override IEnumerator OnTurnStart(Fortress shooter) {
        if (tearing) yield break;
        if (shooter == owner) ownerTurns++;
        int turn = BattleManager.I != null ? BattleManager.I.Turn : int.MaxValue;
        bool used = spentTurn >= 0 && turn > spentTurn;
        if (used || ownerTurns >= Turns) yield return Tear(true);
    }

    // Time rewound past the cast, or the battle is over.
    public override void Cancel() {
        RemoveShield();
        base.Cancel();
    }

    void OnDestroy() { RemoveShield(); }

    void RemoveShield() {
        if (commander != null && commander.shield == shieldFn) commander.shield = null;
    }

    // The cloak swells for a moment, then rips into bats and shreds of red cloth. `pause` lets the next shooter
    // see it go before he aims.
    IEnumerator Tear(bool pause) {
        tearing = true;
        RemoveShield();
        for (float t = 0f; t < SwellTime && model != null; t += Time.deltaTime) {
            model.Swell(t / SwellTime);
            yield return null;
        }
        if (model != null) model.Burst(12);
        Destroy(gameObject);
        if (pause) yield return new WaitForSeconds(0.4f);
    }
}

// The cloak's model: a bell of blood-red bat-wing membrane stretched between dark ribs that end in glowing claws,
// with a glowing hem and a jewel clasp on top. Its hem at a rib is at the origin. A live cloak flutters (the
// membrane ripples between the ribs, most at the hem), glows, flashes when it blocks a hit and bursts into bats.
// Icons get a static, opaque copy.
public class VampireCloakModel : MonoBehaviour {
    public const float Width = 2.55f, Depth = 1.3f, Height = 3.9f;   // half width (x), half depth (z), height
    const int Ribs = 10, Cols = 4, Rings = 10;                       // gores, columns per gore, rings top to hem
    const float Sag = 0.08f;               // the membrane sags in between the ribs...
    const float Scallop = 0.55f;           // ...and its hem rises between them (bat-wing scallops)
    const float Flutter = 0.11f;           // metres of ripple at the hem
    const float TopH = 0.035f;             // the first ring, just below the clasp
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    static Mesh staticDome, ribsMesh, clawsMesh;
    static Texture2D membraneTexture;

    Mesh dome;
    Vector3[] rest, verts, rippleDir;
    float[] rippleWeight, ripplePhase;
    int[] hemIndex;
    Vector3[] hemPoints;
    Material membrane;
    LineRenderer hem;
    Light glow;
    float flash, swell;

    public static VampireCloakModel Create(Transform parent, bool live) {
        GameObject root = ModelKit.Root("Cloak", parent);
        VampireCloakModel m = root.AddComponent<VampireCloakModel>();
        m.enabled = live;
        Transform r = root.transform;
        if (live) m.BuildLive();
        else ModelKit.MeshPart(r, StaticDome(), Vector3.zero, Vector3.one, Mats.Lit(new Color(0.55f, 0.04f, 0.1f), 0.5f), default, "Membrane");
        ModelKit.MeshPart(r, Ribs3D(), Vector3.zero, Vector3.one, Mats.Lit(new Color(0.12f, 0.06f, 0.08f), 0.4f), default, "Ribs");
        GameObject claws = ModelKit.MeshPart(r, Claws(), Vector3.zero, Vector3.one, VampireKit.GlowRed, default, "Claws");
        ModelKit.MeshPart(r, WorldMeshes.Frustum(12, 0.22f, 0.15f, 0.16f), new Vector3(0f, Height - 0.08f, 0f), Vector3.one, VampireKit.GoldMetal, default, "Clasp");
        GameObject gem = ModelKit.MeshPart(r, WorldMeshes.Ball(0.13f), new Vector3(0f, Height + 0.12f, 0f), Vector3.one, VampireKit.GlowRed, default, "Jewel");
        ModelKit.Shadows(claws, false);
        ModelKit.Shadows(gem, false);
        return m;
    }

    // Per-cloak mesh, material, hem line and light (they animate).
    void BuildLive() {
        List<Vector3> v = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> t = new List<int>();
        List<float> weight = new List<float>(), phase = new List<float>();
        List<Vector3> dir = new List<Vector3>();
        Shape(v, uv, t, weight, phase, dir);
        rest = v.ToArray();
        verts = v.ToArray();
        rippleWeight = weight.ToArray();
        ripplePhase = phase.ToArray();
        rippleDir = dir.ToArray();
        dome = new Mesh { name = "Cloak" };
        dome.MarkDynamic();
        dome.SetVertices(v);
        dome.SetUVs(0, uv);
        dome.SetTriangles(t, 0);
        dome.RecalculateNormals();
        membrane = new Material(Mats.Transparent(Color.white, 0.55f, MembraneTexture));   // own copy: it flashes and fades
        ModelKit.MeshPart(transform, dome, Vector3.zero, Vector3.one, membrane, default, "Membrane");

        hemIndex = HemRing();
        hemPoints = new Vector3[hemIndex.Length];
        hem = new GameObject("Hem").AddComponent<LineRenderer>();
        hem.transform.SetParent(transform, false);
        hem.useWorldSpace = true;
        hem.loop = true;
        hem.positionCount = hemIndex.Length;
        hem.sharedMaterial = FxAssets.Additive(FxAssets.Beam, 2.2f);
        hem.startColor = hem.endColor = new Color(1f, 0.15f, 0.25f, 0.9f);
        hem.alignment = LineAlignment.View;
        hem.shadowCastingMode = ShadowCastingMode.Off;
        hem.receiveShadows = false;

        glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = new Vector3(0f, 1.5f, -1.8f);   // in front of the chamber, inside the bell's light
        glow.type = LightType.Point;
        glow.color = VampireKit.BloodGlow;
        glow.shadows = LightShadows.None;
        Animate();
    }

    void Update() { Animate(); }

    public void Flash() { flash = 1f; }

    // 0..1 just before it tears: it puffs up and fades.
    public void Swell(float k) {
        swell = Mathf.Clamp01(k);
        transform.localScale = Vector3.one * (1f + 0.15f * swell);
    }

    // Rips into `bats` bats flying off in all directions and shreds of red cloth.
    public void Burst(int bats) {
        Vector3 centre = transform.TransformPoint(new Vector3(0f, Height * 0.45f, 0f));
        float scale = transform.lossyScale.y;
        for (int i = 0; i < bats; i++) {
            Vector3 p = transform.TransformPoint(Point(Random.Range(0, Ribs), Random.value, Random.Range(0.25f, 1f)));
            Vector3 v = (p - centre).normalized * Random.Range(4f, 8f) + Vector3.up * Random.Range(1f, 3f);
            v.z *= 0.3f;
            VampireKit.FreeBat(p, Random.Range(0.4f, 0.6f) * Mathf.Max(0.5f, scale), v, Random.Range(1f, 1.6f));
        }
        VFX.Burst(centre, 50, 7f * scale, 0.35f * scale, 1.3f, new Color(0.6f, 0.04f, 0.1f, 0.95f), 0.9f, false, TexKit.Chip);
        VFX.Burst(centre, 30, 3f * scale, 1.2f * scale, 1f, VampireKit.Smoke, -0.1f, false, TexKit.Smoke);
        VFX.Ring(centre, 3.5f * scale, VampireKit.BloodGlow, 0.5f, 0.3f);
        VFX.LightFlash(centre, VampireKit.BloodGlow, 9f, 12f, 0.45f);
        VampireSounds.Play(VampireSounds.CloakTear, centre, 0.9f);
        BattleCamera.Shake(0.25f);
        gameObject.SetActive(false);   // the owner destroys it (or it goes with its parent)
    }

    // Ripples, the glowing hem and the pulsing light; a flash fades out quickly.
    void Animate() {
        float time = Time.time;
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 2.5f);
        float ripple = Flutter * (1f + 2f * flash);
        for (int i = 0; i < rest.Length; i++)
            verts[i] = rest[i] + rippleDir[i] * (ripple * rippleWeight[i] * Mathf.Sin(time * 3.4f + ripplePhase[i]));
        dome.vertices = verts;
        dome.RecalculateNormals();

        float scale = transform.lossyScale.x;
        for (int i = 0; i < hemIndex.Length; i++) hemPoints[i] = transform.TransformPoint(verts[hemIndex[i]]);
        hem.SetPositions(hemPoints);
        hem.widthMultiplier = 0.09f * scale * (1f + flash);

        float fade = 1f - 0.7f * swell;
        Color tint = Color.Lerp(Color.white, new Color(2.2f, 1.2f, 1.2f, 1f), flash);
        tint.a = fade;
        membrane.SetColor(BaseColorId, tint);
        glow.range = 7f * scale;
        glow.intensity = (2.2f * (0.8f + 0.2f * Mathf.Sin(time * 2.3f)) + 7f * flash) * fade;
    }

    void OnDestroy() {
        if (dome != null) Destroy(dome);
        if (membrane != null) Destroy(membrane);
    }

    // ------------------------------------------------------------------ shape
    // A point of the bell: gore g, position f across it (0 and 1 = on its ribs), h from the top (0) to the hem (1).
    // A bulging profile (steep sides, rounded top) covers the chamber; the membrane sags between the ribs and its
    // hem rises there into scallops.
    static Vector3 Point(int g, float f, float h) {
        float phi = (g + f) / Ribs * Mathf.PI * 2f;
        float between = Mathf.Sin(Mathf.PI * f);
        float radial = Mathf.Pow(Mathf.Sin(h * Mathf.PI * 0.5f), 0.75f) * (1f - Sag * between * (0.3f + 0.7f * h));
        float y = Height * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(h * Mathf.PI * 0.5f)), 0.55f);
        y += Scallop * between * Mathf.Clamp01((h - 0.8f) / 0.2f);
        return new Vector3(Mathf.Cos(phi) * Width * radial, y, Mathf.Sin(phi) * Depth * radial);
    }

    static float RingH(int r) { return Mathf.Lerp(TopH, 1f, r / (Rings - 1f)); }

    static int Index(int g, int c, int r) { return (g * (Cols + 1) + c) * Rings + r; }

    // The membrane: every gore has its own columns (so its texture runs rib to rib), a fan closes the top.
    static void Shape(List<Vector3> v, List<Vector2> uv, List<int> t, List<float> weight, List<float> phase, List<Vector3> dir) {
        for (int g = 0; g < Ribs; g++)
            for (int c = 0; c <= Cols; c++) {
                float f = c / (float)Cols, phi = (g + f) / Ribs * Mathf.PI * 2f;
                for (int r = 0; r < Rings; r++) {
                    float h = RingH(r);
                    v.Add(Point(g, f, h));
                    uv.Add(new Vector2(f, h));
                    weight.Add(Mathf.Sin(Mathf.PI * f) * h * h);           // still on the ribs and at the top
                    phase.Add(phi * 3f + h * 5f);
                    dir.Add(new Vector3(Mathf.Cos(phi), 0.2f, Mathf.Sin(phi)).normalized);
                }
            }
        int apex = v.Count;
        for (int g = 0; g < Ribs; g++) {
            v.Add(new Vector3(0f, Height, 0f));
            uv.Add(new Vector2(0.5f, 0f));
            weight.Add(0f);
            phase.Add(0f);
            dir.Add(Vector3.up);
        }
        for (int g = 0; g < Ribs; g++)
            for (int c = 0; c < Cols; c++) {
                for (int r = 0; r + 1 < Rings; r++) {
                    int a = Index(g, c, r), b = Index(g, c + 1, r);
                    Outward(v, t, a, b, a + 1);
                    Outward(v, t, b, b + 1, a + 1);
                }
                Outward(v, t, apex + g, Index(g, c, 0), Index(g, c + 1, 0));
            }
    }

    // Adds triangle a-b-c facing out of the bell.
    static void Outward(List<Vector3> v, List<int> t, int a, int b, int c) {
        Vector3 m = (v[a] + v[b] + v[c]) / 3f;
        Vector3 outward = new Vector3(m.x / Width, m.y / Height * 0.8f, m.z / Depth);
        if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), outward) < 0f) { int s = b; b = c; c = s; }
        t.Add(a);
        t.Add(b);
        t.Add(c);
    }

    // The hem vertices once round (the last ring, without the duplicated column where two gores meet).
    static int[] HemRing() {
        int[] ring = new int[Ribs * Cols];
        for (int g = 0; g < Ribs; g++)
            for (int c = 0; c < Cols; c++) ring[g * Cols + c] = Index(g, c, Rings - 1);
        return ring;
    }

    static Mesh StaticDome() {
        if (staticDome != null) return staticDome;
        List<Vector3> v = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> t = new List<int>();
        Shape(v, uv, t, new List<float>(), new List<float>(), new List<Vector3>());
        staticDome = VampireKit.ToMesh("Cloak (static)", v, t);
        return staticDome;
    }

    // The ribs: a tapering tube along every gore boundary, from the clasp to the hem.
    static Mesh Ribs3D() {
        if (ribsMesh != null) return ribsMesh;
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        for (int g = 0; g < Ribs; g++) {
            Vector3[] path = new Vector3[Rings];
            float[] radii = new float[Rings];
            for (int r = 0; r < Rings; r++) {
                path[r] = Point(g, 0f, RingH(r)) * 1.01f;
                radii[r] = Mathf.Lerp(0.06f, 0.04f, r / (Rings - 1f));
            }
            VampireKit.AddTube(v, t, path, radii, 6);
        }
        ribsMesh = VampireKit.ToMesh("Cloak Ribs", v, t);
        return ribsMesh;
    }

    // A glowing claw at the end of every rib, pointing on along the rib, down and out.
    static Mesh Claws() {
        if (clawsMesh != null) return clawsMesh;
        VampireKit.Piece[] claws = new VampireKit.Piece[Ribs];
        Mesh cone = MeshKit.Cone(5, 0.055f, 0.3f);
        for (int g = 0; g < Ribs; g++) {
            Vector3 end = Point(g, 0f, 1f), before = Point(g, 0f, RingH(Rings - 2));
            Quaternion along = Quaternion.FromToRotation(Vector3.up, (end - before).normalized);
            claws[g] = new VampireKit.Piece(cone, end * 1.01f, along, Vector3.one);
        }
        clawsMesh = VampireKit.Merge("Cloak Claws", claws);
        return clawsMesh;
    }

    // One gore of membrane (U = rib to rib, V = top to hem): dark crimson at the top deepening to blood red, veins
    // fanning down from both ribs, darker edges along the ribs and a bright rim at the hem; more see-through below.
    static Texture2D MembraneTexture {
        get {
            if (membraneTexture != null) return membraneTexture;
            const int W = 64, H = 128;
            Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, true) {
                name = "Cloak Membrane", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            Color[] px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++) {
                    float u = (x + 0.5f) / W, v = (y + 0.5f) / H;   // v = h: 0 at the top of the bell, 1 at the hem
                    Color c = Color.Lerp(new Color(0.34f, 0.02f, 0.07f), new Color(0.66f, 0.05f, 0.12f), v);
                    float edge = Mathf.Min(u, 1f - u);
                    if (edge < 0.05f) c *= 0.55f;
                    for (int k = 0; k < 4; k++) {
                        float v0 = 0.12f + 0.22f * k;
                        float d = Mathf.Min(Mathf.Abs(v - (v0 + 0.5f * u)), Mathf.Abs(v - (v0 + 0.5f * (1f - u))));
                        if (d < 0.012f) c = Color.Lerp(c, new Color(0.16f, 0f, 0.03f), 0.7f);
                    }
                    if (v > 0.93f) c = new Color(0.9f, 0.1f, 0.16f);
                    c.a = Mathf.Lerp(0.9f, 0.68f, v);
                    px[y * W + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(true);
            membraneTexture = tex;
            return tex;
        }
    }
}

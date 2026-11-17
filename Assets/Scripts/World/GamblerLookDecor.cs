using System.Collections.Generic;
using UnityEngine;

// The Gambler's fortress decorations, a casino front (visual only, parented to blocks so they fall with them, no
// colliders; FortressDecor has already planted the team flag and banners). On the anchors the generator left:
//   turret roof  a round neon sign: a big neon-pink card suit on a black board in a gold rim, ringed by marquee bulbs.
//                The flag's turret (the tallest) gets the full-size sign and the team flag moves up onto its rim; other
//                turrets (a citadel's wings) get a smaller one crowned by a gold ball.
//   shield       a giant playing card pinned to the wall, a little askew (A, K, Q, J in turn)
//   torch        a gold sconce holding a glowing globe
//   base         two giant dice showing a lucky seven and three stacks of poker chips on the ground before the wall
// plus a row of marquee bulbs on a gold band along the front of each roof and a card pennant (the four suits in turn)
// on the merlons. GamblerDecorLights makes the bulbs chase and the neon stutter now and then, and puts them out while
// the fortress is defeated. At night at most MaxLights point lights per fortress.
public static class GamblerDecor {
    const int MaxLights = 3, MaxRows = 4, MaxPennants = 6, RingBulbs = 15;   // a ring of 15: the three-step chase closes
    const float SignRadius = 0.5f, SignRim = 0.05f, SignStand = 0.22f, SignDepth = 0.1f, WingSign = 0.65f;
    const float BulbRadius = 0.042f, BulbSpacing = 0.24f;
    const float CardHeight = 0.62f;
    const float ChipRadius = 0.2f, ChipHeight = 1f / 16f;   // the chip texture paints 16 chips to the metre
    const float PoleHeight = 0.62f, PennantLength = 0.42f, PennantHeight = 0.26f;

    static readonly PlayingCard[] WallCards = {
        new PlayingCard(CardRank.Ace, CardSuit.Spades), new PlayingCard(CardRank.King, CardSuit.Hearts),
        new PlayingCard(CardRank.Queen, CardSuit.Diamonds), new PlayingCard(CardRank.Jack, CardSuit.Clubs),
    };
    static readonly Color ChipRed = new Color(0.78f, 0.08f, 0.1f), ChipBlack = new Color(0.09f, 0.09f, 0.11f);
    static readonly Color ChipPurple = new Color(0.42f, 0.14f, 0.6f);
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    static readonly Dictionary<Color, Texture2D> chipTextures = new Dictionary<Color, Texture2D>();
    static readonly Texture2D[] pennantTextures = new Texture2D[4];
    static Mesh pennantMesh;

    static Material BoardMat => Mats.Lit(new Color(0.05f, 0.03f, 0.06f), 0.55f);
    internal static Material BulbOff => Mats.Emissive(new Color(0.45f, 0.34f, 0.16f), new Color(0.3f, 0.2f, 0.05f));
    internal static Material NeonOff => Mats.Emissive(new Color(0.3f, 0.1f, 0.18f), new Color(0.35f, 0.08f, 0.2f));

    // One decoration pass: which way the enemy is, the lights left, and the bulbs and neon GamblerDecorLights will run
    // (the bulbs in three groups, every third bulb in each, so they can chase).
    class Job {
        public int facing;
        public bool night;
        public int lights;
        public readonly List<Renderer>[] bulbs = { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
        public readonly List<Renderer> neon = new List<Renderer>();

        public bool TakeLight() {
            if (!night || lights >= MaxLights) return false;
            lights++;
            return true;
        }

        public void AddBulbs(int group, GameObject part) {
            ModelKit.Shadows(part, false, false);
            bulbs[group].Add(part.GetComponent<Renderer>());
        }
    }

    public static void Apply(Fortress f) {
        GamblerSounds.WarmUp();   // a Gambler is in this battle: get his sounds ready
        FortressPlan plan = f.blueprint as FortressPlan;
        Job job = new Job { facing = f.Facing >= 0 ? 1 : -1, night = ArenaBuilder.IsNight };
        if (plan != null && f.blocks.Count == plan.blocks.Count) {   // anchors need the original block order
            HashSet<int> flagged = new HashSet<int>();
            foreach (DecorAnchor a in plan.decor) if (a.kind == DecorKind.Flag) flagged.Add(a.block);
            int cards = 0;
            foreach (DecorAnchor a in plan.decor) {
                Block host = a.block >= 0 && a.block < f.blocks.Count ? f.blocks[a.block] : null;
                if (host == null) continue;
                switch (a.kind) {
                    case DecorKind.TurretRoof: NeonSign(job, host.transform, a.local, flagged.Contains(a.block)); break;
                    case DecorKind.Shield: WallCard(host.transform, a.local, cards++); break;
                    case DecorKind.Torch: Sconce(job, host.transform, a.local); break;
                    case DecorKind.Base: DiceAndChips(host, a.local, a.span, job.facing); break;
                }
            }
        }
        Marquees(job, f.blocks);
        Pennants(f.blocks, job.facing);
        if (job.neon.Count > 0 || job.bulbs[0].Count > 0) GamblerDecorLights.Create(f, job.bulbs, job.neon);
    }

    // ------------------------------------------------------------------ the neon signs
    // A round sign on a short gold stand: a black board in a gold rim with a big neon suit on it, ringed by marquee
    // bulbs. The main one (on the flag's turret) also lights the roof at night, and the team flag FortressDecor planted
    // on the turret moves up onto its rim; a smaller wing sign is crowned by a gold ball instead.
    static void NeonSign(Job job, Transform host, Vector3 at, bool main) {
        Transform root = Root("Neon Sign", host, at);
        float scale = main ? 1f : WingSign;
        root.localScale = Vector3.one * scale;
        float cy = SignStand + SignRadius + SignRim, front = -SignDepth * 0.5f;
        Part(root, WorldMeshes.Frustum(6, 0.11f, 0.07f, SignStand + 0.05f), Vector3.zero, GamblerKit.GoldMat);
        Part(root, WorldMeshes.Frustum(24, SignRadius + SignRim, SignRadius + SignRim, SignDepth), new Vector3(0f, cy, -front),
             GamblerKit.GoldMat, new Vector3(-90f, 0f, 0f));   // the rim: a gold drum from z = +depth/2 to -depth/2
        Part(root, WorldMeshes.Disc(24, SignRadius), new Vector3(0f, cy, front - 0.004f), BoardMat);
        GameObject suit = GamblerKit.Suit(root, (CardSuit)Random.Range(0, 4), new Vector3(0f, cy - 0.02f, front - 0.05f), 0.56f, GamblerKit.NeonMat);
        ModelKit.Shadows(suit, false, false);
        job.neon.AddRange(suit.GetComponentsInChildren<Renderer>());
        for (int g = 0; g < 3; g++) job.AddBulbs(g, Part(root, RingMesh(g), new Vector3(0f, cy, front - 0.03f), GamblerKit.GoldGlowMat));
        float top = cy + SignRadius + SignRim;
        if (main && job.TakeLight()) PointLight(root, new Vector3(0f, cy, -1f), GamblerKit.Neon, 2f, 6f);
        Transform flag = main ? host.Find("Flag") : null;   // planted by FortressDecor on the turret's top
        if (flag != null) flag.localPosition = at + Vector3.up * ((top - 0.02f) * scale);
        else Part(root, WorldMeshes.Ball(0.08f), Vector3.up * (top + 0.05f), GamblerKit.GoldMat);
    }

    // Every third bulb (group g) of the sign's ring, as one mesh.
    static Mesh RingMesh(int group) {
        return Merged("gamblerRing" + group, () => {
            List<CombineInstance> parts = new List<CombineInstance>();
            for (int i = group; i < RingBulbs; i += 3) {
                float a = i * Mathf.PI * 2f / RingBulbs;
                parts.Add(Bulb(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (SignRadius - 0.065f)));
            }
            return parts.ToArray();
        });
    }

    // ------------------------------------------------------------------ on the walls
    // A giant playing card on the wall, a little askew, held by a gold pin.
    static void WallCard(Transform host, Vector3 at, int index) {
        Transform root = Root("Wall Card", host, at + new Vector3(0f, 0f, -0.012f));
        root.localRotation = Quaternion.Euler(0f, 0f, index % 2 == 0 ? 6f : -5f);
        GamblerKit.Card(root, WallCards[index % WallCards.Length], CardHeight);
        Part(root, WorldMeshes.Ball(0.035f), new Vector3(0f, CardHeight * 0.43f, -0.02f), GamblerKit.GoldMat);
    }

    // A gold sconce (wall plate, arm, cup) holding a glowing globe; at night it lights the wall while the fortress has
    // lights left.
    static void Sconce(Job job, Transform host, Vector3 at) {
        Transform root = Root("Globe Sconce", host, at);
        Material gold = GamblerKit.GoldMat;
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0f, -0.015f), gold, default, new Vector3(0.12f, 0.2f, 0.03f));
        Part(root, PrimitiveType.Cube, new Vector3(0f, -0.03f, -0.13f), gold, default, new Vector3(0.04f, 0.04f, 0.22f));
        Part(root, WorldMeshes.Frustum(8, 0.05f, 0.08f, 0.07f), new Vector3(0f, -0.04f, -0.25f), gold);
        ModelKit.Shadows(Part(root, WorldMeshes.Ball(0.1f), new Vector3(0f, 0.12f, -0.25f), GamblerKit.GoldGlowMat), false, false);
        if (job.TakeLight()) PointLight(root, new Vector3(0f, 0.25f, -0.6f), GamblerKit.Gold, 1.8f, 6f);
    }

    // ------------------------------------------------------------------ before the walls
    // Two giant dice showing a lucky seven and three stacks of poker chips on the ground beyond the front pillar's outer
    // face and what stands before it (`clear` metres), running towards the enemy.
    static void DiceAndChips(Block pillar, Vector3 at, float clear, int facing) {
        Transform root = Root("Dice and Chips", pillar.transform, at + new Vector3(facing * (pillar.size.x * 0.5f + clear), 0f, -0.5f));
        GiantDie(root, new Vector3(facing * 0.45f, 0f, 0.05f), 0.6f, 4, facing * 16f);
        GiantDie(root, new Vector3(facing * 1.15f, 0f, -0.3f), 0.42f, 3, -facing * 24f);
        Chips(root, new Vector3(facing * 1.74f, 0f, 0.15f), 9, ChipRed);
        Chips(root, new Vector3(facing * 2.16f, 0f, 0.2f), 6, ChipBlack);
        Chips(root, new Vector3(facing * 1.95f, 0f, -0.25f), 4, ChipPurple);
    }

    // A die `size` on a side sitting on the ground, `value` towards the camera, turned `yaw` degrees. The pips on the
    // faces turned away from the camera or lying on the ground are left out: nobody sees them.
    static void GiantDie(Transform parent, Vector3 at, float size, int value, float yaw) {
        Transform seat = Root("Giant Die", parent, at + Vector3.up * (size * 0.5f));
        Transform die = GamblerKit.Die(seat, size);
        die.localRotation = Quaternion.Euler(0f, yaw, 0f) * GamblerKit.DieFacing(value);
        foreach (Transform pip in die) {
            if (pip.name != "Pip") continue;
            Vector3 face = die.localRotation * FaceOf(pip.localPosition);
            if (face.z > 0.2f || face.y < -0.5f) Object.Destroy(pip.gameObject);
        }
    }

    // The axis of the die face a pip sits on (its largest coordinate).
    static Vector3 FaceOf(Vector3 p) {
        float x = Mathf.Abs(p.x), y = Mathf.Abs(p.y), z = Mathf.Abs(p.z);
        if (x >= y && x >= z) return new Vector3(Mathf.Sign(p.x), 0f, 0f);
        return y >= z ? new Vector3(0f, Mathf.Sign(p.y), 0f) : new Vector3(0f, 0f, Mathf.Sign(p.z));
    }

    // A stack of `count` poker chips: one cylinder whose side is painted with the chips' edges, its texture seam turned
    // away from the camera.
    static void Chips(Transform parent, Vector3 at, int count, Color color) {
        Part(parent, WorldMeshes.Frustum(16, ChipRadius, ChipRadius, count * ChipHeight), at, ChipMat(color),
             new Vector3(0f, -90f + Random.Range(-40f, 40f), 0f));
    }

    // Chip edges, 16 chips and 8 white edge spots to the metre (the cylinder's UVs are in metres): each chip turned its
    // own way, a dark gap between chips.
    static Material ChipMat(Color body) {
        if (!chipTextures.TryGetValue(body, out Texture2D t) || t == null) {
            t = Paint("Poker Chips", 128, TextureWrapMode.Repeat, (u, v) => {
                int chip = Mathf.FloorToInt(v * 16f);
                float fy = v * 16f - chip, fx = Frac(u * 8f + TexKit.Hash(chip, 1, 541));
                Color c = body * (0.9f + 0.15f * TexKit.Hash(chip, 2, 542));
                if (fx < 0.36f && fy > 0.2f && fy < 0.8f) c = GamblerKit.Paper;
                if (fy < 0.1f || fy > 0.9f) c *= 0.5f;
                c.a = 1f;
                return c;
            });
            chipTextures[body] = t;
        }
        return Mats.LitTex(t, Color.white, 0.35f);
    }

    // ------------------------------------------------------------------ the roofs
    // A row of marquee bulbs on a gold band just under the top edge of the front of each roof: the long blocks less than
    // half covered by other long blocks above them (the top course, the ends of a lower storey), highest first.
    static void Marquees(Job job, List<Block> blocks) {
        List<Block> roofs = blocks.FindAll(b => b != null && Long(b) && Roof(b, blocks));
        roofs.Sort((a, b) => Top(b).CompareTo(Top(a)));
        for (int i = 0; i < roofs.Count && i < MaxRows; i++) {
            Block b = roofs[i];
            int n = Mathf.FloorToInt((b.size.x - 0.2f) / BulbSpacing) + 1;
            Transform row = Root("Marquee", b.transform, new Vector3(0f, b.size.y * 0.5f - 0.09f, -b.size.z * 0.5f));
            Part(row, PrimitiveType.Cube, new Vector3(0f, 0f, -0.01f), GamblerKit.GoldMat, default, new Vector3((n - 1) * BulbSpacing + 0.14f, 0.12f, 0.02f));
            for (int g = 0; g < 3; g++) job.AddBulbs(g, Part(row, RowMesh(n, g), new Vector3(0f, 0f, -0.035f), GamblerKit.GoldGlowMat));
        }
    }

    // Every third bulb (group g) of a centred row of n, as one mesh.
    static Mesh RowMesh(int n, int group) {
        return Merged("gamblerRow" + n + "_" + group, () => {
            List<CombineInstance> parts = new List<CombineInstance>();
            for (int i = group; i < n; i += 3) parts.Add(Bulb(new Vector3((i - (n - 1) * 0.5f) * BulbSpacing, 0f, 0f)));
            return parts.ToArray();
        });
    }

    static bool Long(Block b) { return b.size.x >= 1.2f && b.size.x >= b.size.y * 2f; }

    static float Top(Block b) { return b.transform.position.y + b.size.y * 0.5f; }

    // Less than half of b lies under other long blocks (merlons and turrets may stand on it); the blocks still stand
    // where they were built, axis-aligned.
    static bool Roof(Block b, List<Block> blocks) {
        Vector3 p = b.transform.position;
        float top = Top(b), x0 = p.x - b.size.x * 0.5f, x1 = p.x + b.size.x * 0.5f, covered = 0f;
        foreach (Block o in blocks) {
            if (o == null || o == b || !Long(o)) continue;
            Vector3 q = o.transform.position;
            if (q.y - o.size.y * 0.5f < top - 0.05f) continue;
            covered += Mathf.Max(0f, Mathf.Min(x1, q.x + o.size.x * 0.5f) - Mathf.Max(x0, q.x - o.size.x * 0.5f));
        }
        return covered < b.size.x * 0.5f;
    }

    // ------------------------------------------------------------------ the merlons
    // A card pennant on a gold pole on the small blocks that nothing rests on (the merlons), at most MaxPennants: white
    // with a red or black suit, the four suits in turn, flying away from the enemy.
    static void Pennants(List<Block> blocks, int facing) {
        int n = 0;
        foreach (Block b in blocks) {
            if (n >= MaxPennants) return;
            if (b == null || b.size.x > 0.75f || b.size.y > 0.7f) continue;
            if (b.material == BlockMaterial.Glass || b.material == BlockMaterial.Ice || b.material == BlockMaterial.Explosive) continue;
            if (!OpenAbove(b, blocks)) continue;
            Transform root = Root("Card Pennant", b.transform, new Vector3(0f, b.size.y * 0.5f, 0f));
            Part(root, WorldMeshes.Frustum(6, 0.022f, 0.015f, PoleHeight), Vector3.zero, GamblerKit.GoldMat);
            Part(root, PennantMesh, new Vector3(0f, PoleHeight - 0.02f, -0.03f), PennantMat(n % 4), default, new Vector3(-facing, 1f, 1f));
            n++;
        }
    }

    // Nothing above: no block whose bottom is at or above this one's top and overlaps it across.
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

    // A triangular pennant, its hoist on the origin (the pole), pointing +x and facing -z; UVs 0..1 across it.
    static Mesh PennantMesh {
        get {
            if (pennantMesh != null) return pennantMesh;
            MeshBuilder b = new MeshBuilder();
            int top = b.Vertex(Vector3.zero, new Vector2(0f, 1f));
            int tip = b.Vertex(new Vector3(PennantLength, -PennantHeight * 0.5f, 0f), new Vector2(1f, 0.5f));
            int bottom = b.Vertex(new Vector3(0f, -PennantHeight, 0f), new Vector2(0f, 0f));
            b.Triangle(bottom, top, tip);   // clockwise as the camera sees it
            pennantMesh = b.ToMesh("Pennant");
            return pennantMesh;
        }
    }

    // White, with the suit in red or black near the pole, where the pennant is widest.
    static Material PennantMat(int suit) {
        if (pennantTextures[suit] == null) {
            Color ink = suit == (int)CardSuit.Hearts || suit == (int)CardSuit.Diamonds ? GamblerKit.CardRed : GamblerKit.CardBlack;
            pennantTextures[suit] = Paint("Pennant " + (CardSuit)suit, 64, TextureWrapMode.Clamp, (u, v) =>
                GamblerSkins.InSuit(suit, (u - 0.3f) * PennantLength, (v - 0.5f) * PennantHeight, PennantHeight * 0.5f) ? ink : GamblerKit.Paper);
        }
        return Mats.LitTex(pennantTextures[suit], Color.white, 0.2f);
    }

    // ------------------------------------------------------------------ helpers
    // Bulbs combined into one cached mesh (one renderer instead of many).
    static Mesh Merged(string key, System.Func<CombineInstance[]> pieces) {
        if (meshes.TryGetValue(key, out Mesh m) && m != null) return m;
        m = new Mesh { name = key };
        m.CombineMeshes(pieces());
        meshes[key] = m;
        return m;
    }

    static CombineInstance Bulb(Vector3 at) {
        return new CombineInstance { mesh = WorldMeshes.Ball(BulbRadius), transform = Matrix4x4.Translate(at) };
    }

    static Texture2D Paint(string name, int n, TextureWrapMode wrap, System.Func<float, float, Color> fn) {
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = name, wrapMode = wrap, filterMode = FilterMode.Trilinear };
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                px[y * n + x] = fn((x + 0.5f) / n, (y + 0.5f) / n);
        t.SetPixels(px);
        t.Apply(true, true);
        return t;
    }

    static float Frac(float x) { return x - Mathf.Floor(x); }

    static void PointLight(Transform parent, Vector3 at, Color color, float intensity, float range) {
        GameObject go = ModelKit.Root("Decor Light", parent);
        go.transform.localPosition = at;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
    }

    static Transform Root(string name, Transform parent, Vector3 at) {
        Transform t = ModelKit.Root(name, parent).transform;
        t.localPosition = at;
        return t;
    }

    static GameObject Part(Transform parent, Mesh mesh, Vector3 at, Material m, Vector3 euler = default, Vector3 scale = default) {
        return ModelKit.MeshPart(parent, mesh, at, scale == default ? Vector3.one : scale, m, euler);
    }

    static GameObject Part(Transform parent, PrimitiveType type, Vector3 at, Material m, Vector3 euler = default, Vector3 scale = default) {
        return ModelKit.Part(parent, type, at, scale == default ? Vector3.one : scale, m, euler);
    }
}

// The casino lights of one Gambler fortress: the marquee bulbs chase (one bulb in three is dim and the dim ones step
// along), the neon stutters off and on now and then, and everything goes dark while the fortress is defeated. It only
// swaps shared materials, and only when something changes (a few times a second).
public class GamblerDecorLights : MonoBehaviour {
    const float Step = 0.2f;       // seconds per step of the chase
    const float Blink = 0.06f;     // a stutter: off, on, off, on, off, this long each

    Fortress fortress;
    List<Renderer>[] bulbs;
    List<Renderer> neon;
    Material bulbOn, bulbOff, neonOn, neonOff;
    int step = -1;
    bool neonLit = true;
    float stutterAt;

    public static void Create(Fortress f, List<Renderer>[] bulbs, List<Renderer> neon) {
        GamblerDecorLights d = ModelKit.Root("Casino Lights", f.transform).AddComponent<GamblerDecorLights>();
        d.fortress = f;
        d.bulbs = bulbs;
        d.neon = neon;
        d.bulbOn = GamblerKit.GoldGlowMat;
        d.bulbOff = GamblerDecor.BulbOff;
        d.neonOn = GamblerKit.NeonMat;
        d.neonOff = GamblerDecor.NeonOff;
        d.stutterAt = Time.time + Random.Range(2f, 8f);
    }

    void Update() {
        bool dark = fortress == null || fortress.defeated;
        int s = dark ? 3 : Mathf.FloorToInt(Time.time / Step) % 3;   // 3: all out
        if (s != step) {
            step = s;
            for (int g = 0; g < bulbs.Length; g++) Set(bulbs[g], dark || g == s ? bulbOff : bulbOn);
        }
        float k = Time.time - stutterAt;
        if (k >= Blink * 5f) {
            stutterAt = Time.time + Random.Range(4f, 11f);
            k = -1f;
        }
        bool lit = !dark && (k < 0f || (int)(k / Blink) % 2 == 1);
        if (lit != neonLit) {
            neonLit = lit;
            Set(neon, lit ? neonOn : neonOff);
        }
    }

    static void Set(List<Renderer> list, Material m) {
        foreach (Renderer r in list) if (r != null) r.sharedMaterial = m;
    }
}

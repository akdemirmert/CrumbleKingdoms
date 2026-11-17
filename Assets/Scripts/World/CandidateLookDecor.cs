using System.Collections.Generic;
using UnityEngine;

// The Candidate's fortress decorations, a campaign headquarters (visual only, parented to blocks so they fall with
// them, no colliders; FortressDecor has already planted the team flag and banners). On the anchors the generator left:
//   turret roof    a loudspeaker horn on each front corner of every turret. The tallest also carries a billboard with
//                  his big tick in a circle and two lamps on its top edge (and a light at night); the team flag moves
//                  up onto it and a bunch of balloons is tied to its back corner. The others get a short mast.
//   shield, torch  a campaign poster pasted on the wall: a smiling round head and his tick in a circle, no text
//   base           a low stage with a lectern and two microphones on the ground before the wall
// plus bunting of teal, white and gold pennants: swags hung from rosettes along the front of the roofs, and strings from
// pole to pole over the merlons; the back-most pole gets a bunch of balloons too when it stands behind the tower (with
// no tower and no poles the bunch is tied to the back of the top roof). The bunting, poles, horns, balloon strings and
// microphones are cached meshes painted from one small palette texture, so a fortress gets a few dozen renderers;
// CandidateDecorMotion moves the balloons and takes down a string whose far pole has been knocked away.
public static class CandidateDecor {
    const int MaxRows = 3, MaxPoles = 6;
    const float PoleHeight = 0.62f, StringAt = PoleHeight - 0.05f;   // bunting poles; their strings hang just under the ball
    const float MaxSpan = 1.3f, MaxRise = 0.35f;                     // how far apart two poles strung together may stand
    const float PennantWidth = 0.1f, PennantDrop = 0.13f, PennantPitch = 0.15f, TwineWidth = 0.012f;
    const float BoardWidth = 1.32f, BoardHeight = 0.7f, BoardRise = 0.5f;
    const float PosterWidth = 0.48f, PosterHeight = 0.66f, PanelSize = 0.42f;

    // The colours of the palette texture the merged meshes are painted from, one texel each.
    enum Tone { Teal, White, Gold, Twine, Navy, Steel, Black }
    const int PaletteSize = 8;
    static readonly Tone[] PennantTones = { Tone.Teal, Tone.White, Tone.Gold };

    // A bunch of balloons: teal, white and gold, fanning out (degrees from upright, positive to the left) on strings
    // this long (m).
    static readonly float[] BalloonAngles = { -15f, 2f, 18f }, BalloonLengths = { 0.82f, 1.02f, 0.9f };

    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    static readonly Texture2D[] posterTextures = new Texture2D[2];
    static Texture2D palette, boardTexture, panelTexture;

    // One decoration pass: which way the enemy is, whether it is night, where the billboard went and what will move.
    class Job {
        public int facing;
        public bool night;
        public float towerX = float.NaN;   // world x of the billboard's tower (NaN: there is none)
        public readonly List<CandidateDecorMotion.Bunch> bunches = new List<CandidateDecorMotion.Bunch>();
        public readonly List<CandidateDecorMotion.Span> spans = new List<CandidateDecorMotion.Span>();
    }

    public static void Apply(Fortress f) {
        CandidateSounds.WarmUp();    // a Candidate is in this battle: get his sounds ready
        CandidateVotes.Attach(f);    // and start counting his votes (his passive)
        CandidateCrowd.Create(f);    // the supporters who gather beside his fortress as the votes come in
        FortressPlan plan = f.blueprint as FortressPlan;
        Job job = new Job { facing = f.Facing >= 0 ? 1 : -1, night = ArenaBuilder.IsNight };
        if (plan != null && f.blocks.Count == plan.blocks.Count) {   // anchors need the original block order
            int tower = Tallest(f, plan);
            int posters = 0;
            foreach (DecorAnchor a in plan.decor) {
                Block host = a.block >= 0 && a.block < f.blocks.Count ? f.blocks[a.block] : null;
                if (host == null) continue;
                switch (a.kind) {
                    case DecorKind.TurretRoof: Tower(job, host, a.local, a.block == tower); break;
                    case DecorKind.Shield:
                    case DecorKind.Torch: Poster(host.transform, a.local, posters++); break;
                    case DecorKind.Base: Podium(host, a.local, a.span, job.facing); break;
                }
            }
        }
        Block roof = Garlands(f.blocks);
        PoleBunting(job, f.blocks);
        if (job.bunches.Count == 0 && roof != null)
            Balloons(job, roof.transform, new Vector3(-job.facing * (roof.size.x * 0.5f - 0.15f), roof.size.y * 0.5f, 0f));
        if (job.bunches.Count > 0 || job.spans.Count > 0) CandidateDecorMotion.Create(f, job.bunches, job.spans);
    }

    // The block of the highest turret roof (the billboard goes there), or -1.
    static int Tallest(Fortress f, FortressPlan plan) {
        int best = -1;
        float top = float.MinValue;
        foreach (DecorAnchor a in plan.decor) {
            if (a.kind != DecorKind.TurretRoof || a.block < 0 || a.block >= f.blocks.Count || f.blocks[a.block] == null) continue;
            float y = f.blocks[a.block].transform.TransformPoint(a.local).y;
            if (y > top + 0.01f) {
                top = y;
                best = a.block;
            }
        }
        return best;
    }

    // ------------------------------------------------------------------ the towers
    // A loudspeaker horn on each front corner of a turret's top, pointing out, a little down and a little towards the
    // camera; the tallest turret also carries the billboard, the others a short mast with a gold ball.
    static void Tower(Job job, Block turret, Vector3 at, bool main) {
        for (int s = -1; s <= 1; s += 2)
            Part(turret.transform, HornMesh, at + new Vector3(s * (turret.size.x * 0.5f - 0.08f), 0.07f, -turret.size.z * 0.5f + 0.16f),
                 PaletteMat, new Vector3(0f, s * 25f, -s * 105f));
        if (main) Billboard(job, turret, at);
        else Part(turret.transform, PoleMesh, at, PaletteMat);
    }

    // Two navy posts, a gold-framed board with his big tick in a circle, and two lamps on its top edge that glow (and at
    // night light the board). The team flag FortressDecor planted on the turret moves up onto the board, and a bunch of
    // balloons is tied to the board's back corner.
    static void Billboard(Job job, Block turret, Vector3 at) {
        Transform host = turret.transform, root = Root("Billboard", host, at);
        float top = BoardRise + BoardHeight, mid = BoardRise + BoardHeight * 0.5f;
        for (int s = -1; s <= 1; s += 2)
            Part(root, PrimitiveType.Cube, new Vector3(s * 0.3f, top * 0.5f, 0.06f), CandidateKit.NavyMat, default, new Vector3(0.07f, top, 0.07f));
        Part(root, PrimitiveType.Cube, new Vector3(0f, mid, 0f), CandidateKit.GoldMat, default, new Vector3(BoardWidth, BoardHeight, 0.05f));
        Part(root, WorldMeshes.Quad(), new Vector3(0f, mid, -0.03f), BoardMat(), default, new Vector3(BoardWidth - 0.08f, BoardHeight - 0.08f, 1f));
        Material lamp = Mats.Emissive(new Color(1f, 0.95f, 0.85f), new Color(2.4f, 2.2f, 1.8f));
        for (int s = -1; s <= 1; s += 2)
            ModelKit.Shadows(Part(root, PrimitiveType.Cube, new Vector3(s * 0.36f, top + 0.03f, -0.09f), lamp, new Vector3(20f, 0f, 0f),
                                  new Vector3(0.16f, 0.05f, 0.1f)), false, false);
        if (job.night) PointLight(root, new Vector3(0f, mid, -1.1f), new Color(1f, 0.93f, 0.78f), 1.6f, 5f);
        Transform flag = host.Find("Flag");   // planted by FortressDecor on the turret's top
        if (flag != null) flag.localPosition = at + Vector3.up * top;
        job.towerX = root.position.x;
        Balloons(job, root, new Vector3(-job.facing * (BoardWidth * 0.5f - 0.05f), top, 0.05f));
    }

    // ------------------------------------------------------------------ on the walls
    // A campaign poster pasted on the wall, a little askew: teal with a navy figure and navy with a gold one in turn.
    static void Poster(Transform host, Vector3 at, int index) {
        ModelKit.Shadows(Part(host, WorldMeshes.Quad(), at + new Vector3(0f, 0f, -0.012f), PosterMat(index % 2),
                              new Vector3(0f, 0f, index % 2 == 0 ? 2.5f : -2f), new Vector3(PosterWidth, PosterHeight, 1f)), false);
    }

    // ------------------------------------------------------------------ before the walls
    // A low navy stage with a gold edge on the ground beyond the front pillar's outer face and what stands before it
    // (`clear` metres), turned a little towards the enemy; on it a lectern with his mark on its front and two
    // microphones bending back to where he would stand.
    static void Podium(Block pillar, Vector3 at, float clear, int facing) {
        Transform root = Root("Podium", pillar.transform, at + new Vector3(facing * (pillar.size.x * 0.5f + clear + 0.95f), 0f, -0.6f));
        root.localRotation = Quaternion.Euler(0f, -facing * 18f, 0f);
        Material navy = CandidateKit.NavyMat;
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.07f, 0f), navy, default, new Vector3(1.5f, 0.14f, 1f));
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.115f, -0.505f), CandidateKit.GoldMat, default, new Vector3(1.5f, 0.03f, 0.012f));
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.64f, 0.1f), navy, default, new Vector3(0.56f, 1f, 0.42f));
        Part(root, PrimitiveType.Cube, new Vector3(0f, 1.165f, 0.1f), CandidateKit.TealMat, default, new Vector3(0.66f, 0.05f, 0.5f));
        Part(root, WorldMeshes.Quad(), new Vector3(0f, 0.72f, -0.115f), PanelMat(), default, new Vector3(PanelSize, PanelSize, 1f));
        Part(root, MicsMesh, new Vector3(0f, 1.19f, -0.05f), PaletteMat);
    }

    // ------------------------------------------------------------------ the roofs
    // Swags of bunting on the front of each roof (the long blocks less than half covered by other long blocks above
    // them: the top course, the ends of a lower storey), at most MaxRows, highest first. Returns the highest roof.
    static Block Garlands(List<Block> blocks) {
        List<Block> roofs = blocks.FindAll(b => b != null && Long(b) && Roof(b, blocks));
        roofs.Sort((a, b) => Top(b).CompareTo(Top(a)));
        for (int i = 0; i < roofs.Count && i < MaxRows; i++) {
            Block b = roofs[i];
            ModelKit.Shadows(Part(b.transform, GarlandMesh(b.size.x), new Vector3(0f, b.size.y * 0.5f - 0.08f, -b.size.z * 0.5f - 0.012f),
                                  PaletteMat), false);
        }
        return roofs.Count > 0 ? roofs[0] : null;
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
    // A slim navy pole with a gold ball on each merlon (the small blocks nothing rests on; at most MaxPoles, the highest
    // first) and a string of bunting from each pole to the next along when it stands close by at about the same height.
    // The back-most pole gets a bunch of balloons as well when it stands behind the billboard's tower, or there is none.
    static void PoleBunting(Job job, List<Block> blocks) {
        List<Block> merlons = blocks.FindAll(b => b != null && Merlon(b) && OpenAbove(b, blocks));
        merlons.Sort((a, b) => Top(b).CompareTo(Top(a)));
        if (merlons.Count > MaxPoles) merlons.RemoveRange(MaxPoles, merlons.Count - MaxPoles);
        merlons.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        List<Transform> poles = new List<Transform>();
        foreach (Block m in merlons) poles.Add(Part(m.transform, PoleMesh, new Vector3(0f, m.size.y * 0.5f, 0f), PaletteMat).transform);
        for (int i = 0; i + 1 < poles.Count; i++) {
            Vector3 d = poles[i + 1].position - poles[i].position;
            if (d.x <= MaxSpan && Mathf.Abs(d.y) <= MaxRise) Span(job, poles[i], poles[i + 1], d);
        }
        if (poles.Count == 0) return;
        Transform back = job.facing > 0 ? poles[0] : poles[poles.Count - 1];
        if (float.IsNaN(job.towerX) || (back.position.x - job.towerX) * job.facing < -0.6f) Balloons(job, back, Vector3.up * StringAt);
    }

    static bool Merlon(Block b) {
        return b.size.x <= 0.75f && b.size.y <= 0.7f
            && b.material != BlockMaterial.Glass && b.material != BlockMaterial.Ice && b.material != BlockMaterial.Explosive;
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

    // A string of bunting from pole a's top to pole b's (d metres along and up), hung on pole a; CandidateDecorMotion
    // hides it while pole b is not where it was.
    static void Span(Job job, Transform a, Transform b, Vector3 d) {
        int dx = Mathf.RoundToInt(d.x * 100f), dy = Mathf.RoundToInt(d.y * 100f);
        Vector3 p0 = Vector3.up * StringAt, p1 = p0 + new Vector3(dx / 100f, dy / 100f, 0f);
        Mesh mesh = Cached("candidateSpan" + dx + "_" + dy, mb => {
            int colour = 0;
            Bunting(mb, p0, p1, 0.06f + 0.06f * p1.x, ref colour);
        });
        GameObject span = Part(a, mesh, Vector3.zero, PaletteMat);
        job.spans.Add(new CandidateDecorMotion.Span { rend = span.GetComponent<Renderer>(), far = b, farPoint = p0, end = p1 });
    }

    // ------------------------------------------------------------------ balloons
    // A bunch of three balloons (teal, white, gold) on thin strings tied at `at` on `host`, fanning out and leaning a
    // little away from the enemy; CandidateDecorMotion keeps the bunch upright, sways it round the tie and bobs the
    // balloons along their strings.
    static void Balloons(Job job, Transform host, Vector3 at) {
        Transform root = Root("Balloons", host, at);
        float lean = job.facing * 8f;
        root.rotation = Quaternion.Euler(0f, 0f, lean);
        Part(root, BalloonStringsMesh, Vector3.zero, PaletteMat);
        int n = BalloonAngles.Length;
        Transform[] heads = new Transform[n];
        Vector3[] home = new Vector3[n];
        for (int k = 0; k < n; k++) {
            home[k] = BalloonKnot(k);
            Color colour = k == 0 ? CandidateKit.Teal : k == 1 ? CandidateKit.Paper : CandidateKit.Gold;
            heads[k] = Part(root, BalloonMesh, home[k], Mats.Lit(colour, 0.82f), new Vector3(0f, 0f, BalloonAngles[k])).transform;
        }
        job.bunches.Add(new CandidateDecorMotion.Bunch { root = root, heads = heads, home = home, lean = lean, phase = root.position.x * 1.3f });
    }

    // Where balloon k's knot is, from the bunch's tie: up its string, leaning BalloonAngles[k] degrees.
    static Vector3 BalloonKnot(int k) {
        float a = BalloonAngles[k] * Mathf.Deg2Rad;
        return new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f) * BalloonLengths[k];
    }

    // ------------------------------------------------------------------ meshes
    // Swags of bunting for the front of a roof `width` long, hung from rosettes 12 cm in from its ends and about every
    // 90 cm between; the pennants run teal, white and gold all along.
    static Mesh GarlandMesh(float width) {
        int cm = Mathf.RoundToInt(width * 100f);
        return Cached("candidateGarland" + cm, b => {
            float w = cm / 100f - 0.24f;
            int n = Mathf.Max(1, Mathf.RoundToInt(w / 0.9f));
            float step = w / n, x0 = -w * 0.5f;
            int colour = 0;
            for (int i = 0; i < n; i++)
                Bunting(b, new Vector3(x0 + i * step, 0f, 0f), new Vector3(x0 + (i + 1) * step, 0f, 0f), 0.08f + 0.04f * step, ref colour);
            for (int i = 0; i <= n; i++) Rosette(b, new Vector3(x0 + i * step, 0f, -0.006f));
        });
    }

    // A slim navy pole with a gold ball on top (the bunting poles, the masts of the lower towers).
    static Mesh PoleMesh => Cached("candidatePole", b => {
        Lathe(b, Vector3.zero, Quaternion.identity, 6, new[] { 0f, 0.022f, 0.016f, 0f }, new[] { 0f, 0f, PoleHeight, PoleHeight }, Tone.Navy);
        Ball(b, Vector3.up * (PoleHeight + 0.035f), 0.045f, Tone.Gold);
    });

    // A loudspeaker horn along +y: a navy driver behind the throat and a pale steel bell flaring to an open mouth.
    static Mesh HornMesh => Cached("candidateHorn", b => {
        Lathe(b, Vector3.zero, Quaternion.identity, 8, new[] { 0f, 0.06f, 0.06f, 0f }, new[] { -0.1f, -0.1f, 0.01f, 0.01f }, Tone.Navy);
        Lathe(b, Vector3.zero, Quaternion.identity, 12,
              new[] { 0f, 0.032f, 0.04f, 0.065f, 0.105f, 0.15f, 0.15f, 0.134f, 0.075f, 0.034f, 0f },
              new[] { 0f, 0f, 0.06f, 0.17f, 0.265f, 0.325f, 0.332f, 0.332f, 0.25f, 0.13f, 0.11f }, Tone.Steel);
    });

    // A balloon along +y from its knot: a small bead and neck, then the body, roundest above the middle.
    static Mesh BalloonMesh => Cached("candidateBalloon", b =>
        Lathe(b, Vector3.zero, Quaternion.identity, 12,
              new[] { 0f, 0.02f, 0.012f, 0.045f, 0.095f, 0.13f, 0.148f, 0.146f, 0.128f, 0.09f, 0.045f, 0f },
              new[] { 0f, 0.01f, 0.035f, 0.065f, 0.105f, 0.16f, 0.225f, 0.285f, 0.335f, 0.375f, 0.398f, 0.405f }, Tone.White));

    // The strings of a bunch, from its tie up into each balloon's knot (a little past it, so a bobbing balloon never
    // leaves its string).
    static Mesh BalloonStringsMesh => Cached("candidateBalloonStrings", b => {
        Vector2 uv = Uv(Tone.Twine);
        for (int k = 0; k < BalloonAngles.Length; k++) {
            Vector3 end = BalloonKnot(k) * ((BalloonLengths[k] + 0.04f) / BalloonLengths[k]);
            Vector3 dir = end.normalized, side = new Vector3(dir.y, -dir.x, 0f) * 0.004f;   // half the width, to the right
            b.Quad(b.Vertex(-side, uv), b.Vertex(end - side, uv), b.Vertex(end + side, uv), b.Vertex(side, uv));
        }
    });

    // Two goose-neck microphones on gold bases, bending back to where the speaker stands (+z) and a little apart.
    static Mesh MicsMesh => Cached("candidateMics", b => {
        for (int s = -1; s <= 1; s += 2) {
            Vector3 foot = new Vector3(s * 0.1f, 0f, 0f), neck = new Vector3(s * 0.05f, 0.26f, 0.15f);
            Quaternion q = Quaternion.FromToRotation(Vector3.up, neck);
            float len = neck.magnitude;
            Lathe(b, foot, Quaternion.identity, 8, new[] { 0f, 0.03f, 0.03f, 0f }, new[] { 0f, 0f, 0.012f, 0.012f }, Tone.Gold);
            Lathe(b, foot, q, 5, new[] { 0f, 0.009f, 0.007f, 0f }, new[] { 0f, 0f, len, len }, Tone.Black);
            Lathe(b, foot + neck, q, 8, new[] { 0f, 0.016f, 0.024f, 0.024f, 0.016f, 0f },
                  new[] { -0.01f, -0.006f, 0.008f, 0.04f, 0.054f, 0.058f }, Tone.Black);
        }
    });

    // A string of pennants from p0 to p1 (p0 the left end as the camera sees it, both at one z), sagging `sag` in the
    // middle; the pennants hang straight down from it, just in front, teal, white and gold in turn from `colour` on, and
    // keep 6 cm clear of either end.
    static void Bunting(MeshBuilder b, Vector3 p0, Vector3 p1, float sag, ref int colour) {
        float len = Vector3.Distance(p0, p1);
        int n = Mathf.Max(4, Mathf.CeilToInt(len / 0.05f));
        Vector2 twine = Uv(Tone.Twine);
        Vector3 h = Vector3.up * (TwineWidth * 0.5f);
        for (int i = 0; i < n; i++) {
            Vector3 a = Sag(p0, p1, sag, (float)i / n), c = Sag(p0, p1, sag, (float)(i + 1) / n);
            b.Quad(b.Vertex(a - h, twine), b.Vertex(a + h, twine), b.Vertex(c + h, twine), b.Vertex(c - h, twine));
        }
        float room = len - 0.12f - PennantWidth;
        if (room < 0f) return;
        int count = Mathf.FloorToInt(room / PennantPitch) + 1;
        float first = (len - (count - 1) * PennantPitch) * 0.5f, dt = PennantWidth * 0.5f / len;
        Vector3 front = Vector3.back * 0.003f;
        for (int j = 0; j < count; j++) {
            float t = (first + j * PennantPitch) / len;
            Vector2 uv = Uv(PennantTones[colour++ % PennantTones.Length]);
            b.Triangle(b.Vertex(Sag(p0, p1, sag, t - dt) + front, uv), b.Vertex(Sag(p0, p1, sag, t + dt) + front, uv),
                       b.Vertex(Sag(p0, p1, sag, t) + front + Vector3.down * PennantDrop, uv));   // clockwise as the camera sees it
        }
    }

    static Vector3 Sag(Vector3 p0, Vector3 p1, float sag, float t) { return Vector3.Lerp(p0, p1, t) + Vector3.down * (sag * 4f * t * (1f - t)); }

    // A rosette of gold, teal and white rings with two teal tails, facing the camera, centred on `at`.
    static void Rosette(MeshBuilder b, Vector3 at) {
        Vector2 teal = Uv(Tone.Teal);
        Vector3 z = at + Vector3.forward * 0.001f;   // the tails, just behind the rings
        for (int s = -1; s <= 1; s += 2) {
            float t0 = s * 0.008f, t1 = s * 0.033f, b0 = s * 0.033f, b1 = s * 0.058f;   // their top and bottom edges, across
            b.Quad(b.Vertex(z + new Vector3(Mathf.Min(b0, b1), -0.12f, 0f), teal), b.Vertex(z + new Vector3(Mathf.Min(t0, t1), -0.02f, 0f), teal),
                   b.Vertex(z + new Vector3(Mathf.Max(t0, t1), -0.02f, 0f), teal), b.Vertex(z + new Vector3(Mathf.Max(b0, b1), -0.12f, 0f), teal));
        }
        Fan(b, at, 0.062f, Tone.Gold);
        Fan(b, at + Vector3.back * 0.002f, 0.048f, Tone.Teal);
        Fan(b, at + Vector3.back * 0.004f, 0.03f, Tone.White);
        Fan(b, at + Vector3.back * 0.006f, 0.014f, Tone.Teal);
    }

    // A flat disc facing the camera.
    static void Fan(MeshBuilder b, Vector3 c, float r, Tone tone) {
        const int Sides = 12;
        Vector2 uv = Uv(tone);
        int centre = b.Vertex(c, uv), first = -1, prev = -1;
        for (int i = 0; i < Sides; i++) {
            float a = i * Mathf.PI * 2f / Sides;
            int v = b.Vertex(c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r, uv);
            if (prev >= 0) b.Triangle(centre, v, prev);
            else first = v;
            prev = v;
        }
        b.Triangle(centre, first, prev);
    }

    // A ball of radius r centred on `at`.
    static void Ball(MeshBuilder b, Vector3 at, float r, Tone tone) {
        const int Rings = 6;
        float[] rs = new float[Rings + 1], ys = new float[Rings + 1];
        for (int k = 0; k <= Rings; k++) {
            float a = k * Mathf.PI / Rings;
            rs[k] = Mathf.Sin(a) * r;
            ys[k] = -Mathf.Cos(a) * r;
        }
        Lathe(b, at, Quaternion.identity, 10, rs, ys, tone);
    }

    // One closed piece of a surface of revolution round +y (turned by `rot`, moved to `at`): rings of radius rs[k] at
    // height ys[k] from the first to the last, a radius of 0 closing an end, all one palette colour. The rings share
    // their vertices, so it shades smoothly; a profile that turns back down (an open mouth) faces inwards there.
    static void Lathe(MeshBuilder b, Vector3 at, Quaternion rot, int sides, float[] rs, float[] ys, Tone tone) {
        Vector2 uv = Uv(tone);
        int[] prev = null;
        for (int k = 0; k < rs.Length; k++) {
            int[] ring;
            if (rs[k] < 0.0005f) ring = new[] { b.Vertex(at + rot * new Vector3(0f, ys[k], 0f), uv) };
            else {
                ring = new int[sides];
                for (int s = 0; s < sides; s++) {
                    float a = s * Mathf.PI * 2f / sides;
                    ring[s] = b.Vertex(at + rot * new Vector3(Mathf.Cos(a) * rs[k], ys[k], Mathf.Sin(a) * rs[k]), uv);
                }
            }
            if (prev != null && (prev.Length > 1 || ring.Length > 1)) {
                for (int s = 0; s < sides; s++) {
                    int s1 = (s + 1) % sides;
                    if (prev.Length == 1) b.Triangle(prev[0], ring[s], ring[s1]);
                    else if (ring.Length == 1) b.Triangle(prev[s], ring[0], prev[s1]);
                    else b.Quad(prev[s], ring[s], ring[s1], prev[s1]);
                }
            }
            prev = ring;
        }
    }

    static Mesh Cached(string key, System.Action<MeshBuilder> build) {
        if (meshes.TryGetValue(key, out Mesh m) && m != null) return m;
        MeshBuilder b = new MeshBuilder();
        build(b);
        m = b.ToMesh(key);
        meshes[key] = m;
        return m;
    }

    // ------------------------------------------------------------------ materials and textures
    // The palette: one texel a Tone, sampled at its centre without filtering, so a merged mesh is flat-coloured part by part.
    static Material PaletteMat {
        get {
            if (palette == null) {
                palette = new Texture2D(PaletteSize, 1, TextureFormat.RGBA32, false) {
                    name = "Candidate Palette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                };
                palette.SetPixels(new[] {
                    CandidateKit.Teal, CandidateKit.Paper, CandidateKit.Gold, new Color(0.16f, 0.17f, 0.2f), CandidateKit.Navy,
                    new Color(0.85f, 0.86f, 0.88f), new Color(0.09f, 0.09f, 0.1f), Color.white,
                });
                palette.Apply(false, true);
            }
            return Mats.LitTex(palette, Color.white, 0.3f);
        }
    }

    static Vector2 Uv(Tone t) { return new Vector2(((int)t + 0.5f) / PaletteSize, 0.5f); }

    static Material PosterMat(int variant) {
        if (posterTextures[variant] == null)
            posterTextures[variant] = Paint("Campaign Poster " + variant, 128, 176, (u, v) => PosterPixel(variant, u, v));
        return Mats.LitTex(posterTextures[variant], Color.white, 0.25f);
    }

    static Material BoardMat() {
        if (boardTexture == null) boardTexture = Paint("Campaign Billboard", 256, 128, BoardPixel);
        return Mats.LitTex(boardTexture, Color.white, 0.3f);
    }

    static Material PanelMat() {
        if (panelTexture == null) panelTexture = Paint("Lectern Panel", 64, 64, PanelPixel);
        return Mats.LitTex(panelTexture, Color.white, 0.3f);
    }

    // A campaign poster (0.48 x 0.66 m; u, v across it): in a white border, a portrait panel (a round head smiling over
    // its shoulders, a soft light behind it) over a band with his tick in a circle between two gold stars, a thin gold
    // rule between them. Variant 0 is teal with a navy figure, variant 1 navy with a gold one. No text.
    static Color PosterPixel(int variant, float u, float v) {
        float x = (u - 0.5f) * PosterWidth, y = v * PosterHeight, texel = PosterWidth / 128f;
        Color paper = CandidateKit.Paper, gold = CandidateKit.Gold;
        Color panel = variant == 0 ? CandidateKit.Teal : CandidateKit.Navy, band = variant == 0 ? CandidateKit.Navy : CandidateKit.Teal;
        Color figure = variant == 0 ? CandidateKit.Navy : gold, features = variant == 0 ? paper : CandidateKit.Navy;
        Color c;
        if (Mathf.Min(PosterWidth * 0.5f - Mathf.Abs(x), Mathf.Min(y, PosterHeight - y)) < 0.018f) c = paper;
        else if (y < 0.2f) {   // the band: his mark between two stars
            const float R = 0.066f;
            float mx = x / R, my = (y - 0.11f) / R;
            c = Color.Lerp(band, gold, CandidateSkins.Cover(CandidateSkins.MarkRing(mx, my), texel / R));
            c = Color.Lerp(c, paper, CandidateSkins.Cover(CandidateSkins.MarkTick(mx, my), texel / R));
            if (CandidateSkins.InStar(Mathf.Abs(x) - 0.15f, y - 0.11f, 0.03f)) c = gold;
        } else if (y < 0.208f) c = gold;
        else {   // the portrait: head, neck and shoulders in one silhouette, the eyes and the smile cut out of the head
            c = Color.Lerp(panel, paper, Mathf.Clamp01(1f - Dist(x, y - 0.45f) / 0.2f) * 0.18f);
            float head = Dist(x, y - 0.452f) - 0.088f;
            float body = Mathf.Min(Ellipse(x, y - 0.208f, 0.17f, 0.13f), Box(x, y - 0.34f, 0.034f, 0.05f));
            c = Color.Lerp(c, figure, CandidateSkins.Cover(Mathf.Min(head, body), texel));
            float eyes = Dist(Mathf.Abs(x) - 0.03f, y - 0.472f) - 0.0095f;
            float smile = y - 0.466f < -0.022f ? Mathf.Abs(Dist(x, y - 0.466f) - 0.05f) - 0.0075f : 1f;
            if (head < 0f) c = Color.Lerp(c, features, CandidateSkins.Cover(Mathf.Min(eyes, smile), texel));
        }
        c.a = 1f;
        return c;
    }

    // The billboard's face (1.24 x 0.62 m): navy brightening towards teal at the top, a teal band along the bottom under
    // a gold rule, and his big tick in a circle (on a teal disc in a gold ring) between two pairs of gold stars.
    static Color BoardPixel(float u, float v) {
        const float R = 0.2f;
        float x = (u - 0.5f) * 1.24f, y = (v - 0.5f) * 0.62f, t = 1.24f / 256f / R;
        Color c = Color.Lerp(CandidateKit.Navy, Color.Lerp(CandidateKit.Navy, CandidateKit.Teal, 0.35f), v);
        if (y < -0.2f) c = CandidateKit.Teal * 0.9f;
        else if (y < -0.188f) c = CandidateKit.Gold;
        float mx = x / R, my = (y - 0.04f) / R;
        c = Color.Lerp(c, CandidateKit.Teal, CandidateSkins.Cover(Dist(mx, my) - 0.78f, t));
        c = Color.Lerp(c, CandidateKit.Gold, CandidateSkins.Cover(CandidateSkins.MarkRing(mx, my), t));
        c = Color.Lerp(c, CandidateKit.Paper, CandidateSkins.Cover(CandidateSkins.MarkTick(mx, my), t));
        float sx = Mathf.Abs(x);
        if (CandidateSkins.InStar(sx - 0.33f, y - 0.04f, 0.06f) || CandidateSkins.InStar(sx - 0.47f, y - 0.04f, 0.04f)) c = CandidateKit.Gold;
        c.a = 1f;
        return c;
    }

    // The lectern's front panel (PanelSize square): teal in a gold border, his mark in the middle.
    static Color PanelPixel(float u, float v) {
        const float R = 0.14f;
        float x = (u - 0.5f) * PanelSize, y = (v - 0.5f) * PanelSize, t = PanelSize / 64f / R;
        Color c = PanelSize * 0.5f - Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) < 0.014f ? CandidateKit.Gold : CandidateKit.Teal;
        c = Color.Lerp(c, CandidateKit.Gold, CandidateSkins.Cover(CandidateSkins.MarkRing(x / R, y / R), t));
        c = Color.Lerp(c, CandidateKit.Paper, CandidateSkins.Cover(CandidateSkins.MarkTick(x / R, y / R), t));
        c.a = 1f;
        return c;
    }

    // Signed distances (negative inside) for the poster's figure.
    static float Dist(float x, float y) { return Mathf.Sqrt(x * x + y * y); }

    static float Ellipse(float x, float y, float rx, float ry) { return (Dist(x / rx, y / ry) - 1f) * Mathf.Min(rx, ry); }

    static float Box(float x, float y, float hx, float hy) { return Mathf.Max(Mathf.Abs(x) - hx, Mathf.Abs(y) - hy); }

    static Texture2D Paint(string name, int w, int h, System.Func<float, float, Color> fn) {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, true) {
            name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 2,
        };
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = fn((x + 0.5f) / w, (y + 0.5f) / h);
        t.SetPixels(px);
        t.Apply(true, true);
        return t;
    }

    // ------------------------------------------------------------------ helpers
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

// The moving parts of one Candidate fortress's decorations, in one small update: every bunch of balloons stays upright
// whatever its block does, sways a few degrees round its tie and bobs its balloons along their strings; a string of
// bunting between two merlons hides while its far pole is not where it was (knocked away or broken) and shows again if
// a time rewind puts that pole back. It only moves transforms and flips renderers, nothing is made or destroyed.
public class CandidateDecorMotion : MonoBehaviour {
    const float Sway = 4f, SwaySpeed = 0.75f;   // degrees either way; radians per second
    const float Bob = 0.03f, BobSpeed = 1.4f;   // metres along the string; radians per second
    const float Slack = 0.12f;                  // how far (m) the far pole may move before its string comes down

    internal class Bunch {
        public Transform root;                  // at the tie
        public Transform[] heads;               // the balloons
        public Vector3[] home;                  // their knots at rest, from the tie
        public float lean, phase;               // degrees (positive: to the left); radians
    }

    internal class Span {
        public Renderer rend;                   // the string, hung on the near pole
        public Transform far;                   // the far pole
        public Vector3 farPoint, end;           // where the string meets it: on that pole, and seen from the near one
    }

    List<Bunch> bunches;
    List<Span> spans;

    internal static void Create(Fortress f, List<Bunch> bunches, List<Span> spans) {
        CandidateDecorMotion m = ModelKit.Root("Campaign Decor Motion", f.transform).AddComponent<CandidateDecorMotion>();
        m.bunches = bunches;
        m.spans = spans;
    }

    void Update() {
        float t = Time.time;
        foreach (Bunch b in bunches) {
            if (b.root == null) continue;   // its block broke
            b.root.rotation = Quaternion.Euler(0f, 0f, b.lean + Mathf.Sin(t * SwaySpeed + b.phase) * Sway);
            for (int i = 0; i < b.heads.Length; i++) {
                if (b.heads[i] == null) continue;
                float bob = Mathf.Sin(t * BobSpeed + b.phase + i * 2.1f) * Bob;
                b.heads[i].localPosition = b.home[i] + b.home[i].normalized * bob;
            }
        }
        foreach (Span s in spans) {
            if (s.rend == null) continue;   // its block broke
            bool held = s.far != null
                     && (s.rend.transform.InverseTransformPoint(s.far.TransformPoint(s.farPoint)) - s.end).sqrMagnitude < Slack * Slack;
            if (s.rend.enabled != held) s.rend.enabled = held;
        }
    }
}

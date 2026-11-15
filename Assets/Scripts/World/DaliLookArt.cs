using System.Collections.Generic;
using UnityEngine;

// Shapes, textures and materials shared by Dalí's commander, fortress and weapons: the waxed mustache, eggs,
// soft (melting) watches, a crutch, the lips sofa, the painted sun and black feathers. Everything is built once
// and cached; a watch that melts in front of the player keeps a mesh of its own (SoftWatch, below).
public static class DaliArt {
    public static readonly Color Gold = new Color(1f, 0.8f, 0.36f);
    public static readonly Color Ink = new Color(0.09f, 0.07f, 0.06f);       // waxed mustache, slick hair, painted lines
    public static readonly Color Cream = new Color(0.97f, 0.93f, 0.83f);     // eggs, watch faces
    public static readonly Color LipsRed = new Color(0.86f, 0.1f, 0.2f);
    public static readonly Color Paint = new Color(1f, 0.79f, 0.24f);        // the sun's golden paint
    public const float CrutchLength = 2.1f;                                   // foot to the top of the pad

    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

    // Polished gold, only half metallic (the arenas have no reflection probes: full metal would render dark).
    public static Material GoldMetal => Mats.Lit(Gold, 0.7f, 0.5f);
    public static Material Wax => Mats.Lit(Ink, 0.85f);                   // mustache and hair are waxed to a shine

    // Dalí is in this battle: build the textures his weapons need now, while the battle loads, not in mid-throw.
    public static void Prewarm() {
        if (GameAssets.I == null) return;
        _ = SunDisc;
        _ = SunRay;
        _ = WatchFace(false);
        _ = Feather;
    }

    // ================================================================== meshes
    // One half of the waxed mustache: from the middle of the upper lip (origin) out along +x, dipping a little,
    // then sweeping up into a long thin point (about 0.25 m out and 0.25 m up). The other half is this mirrored.
    public static Mesh MustacheHalf() {
        if (meshes.TryGetValue("mustache", out Mesh cached) && cached != null) return cached;
        Vector3[] ctrl = {
            new Vector3(0f, 0f, 0f), new Vector3(0.05f, -0.018f, 0.004f), new Vector3(0.11f, -0.03f, 0.012f),
            new Vector3(0.17f, -0.02f, 0.026f), new Vector3(0.215f, 0.025f, 0.04f), new Vector3(0.24f, 0.1f, 0.05f),
            new Vector3(0.245f, 0.18f, 0.056f), new Vector3(0.232f, 0.25f, 0.06f),
        };
        const int per = 4;
        int n = (ctrl.Length - 1) * per + 1;
        Vector3[] path = new Vector3[n];
        float[] radii = new float[n];
        for (int i = 0; i < n; i++) {
            int seg = Mathf.Min(i / per, ctrl.Length - 2);
            float t = (i - seg * per) / (float)per;
            path[i] = CatmullRom(ctrl[Mathf.Max(seg - 1, 0)], ctrl[seg], ctrl[seg + 1], ctrl[Mathf.Min(seg + 2, ctrl.Length - 1)], t);
            radii[i] = Mathf.Lerp(0.034f, 0.002f, Mathf.Pow(i / (n - 1f), 0.75f));   // thick at the lip, a needle point
        }
        Mesh m = Tube("Mustache", path, radii, 8);
        meshes["mustache"] = m;
        return m;
    }

    // A smooth egg standing on its broad end, base at the origin.
    public static Mesh Egg(float radius, float height) {
        string key = "egg" + radius.ToString("0.000") + "_" + height.ToString("0.000");
        if (meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
        const int rings = 14, sides = 18;
        MeshBuilder b = new MeshBuilder();
        int[,] idx = new int[rings + 1, sides + 1];
        for (int i = 0; i <= rings; i++) {
            float th = Mathf.PI * i / rings;                                     // 0 at the bottom pole
            float y = height * 0.5f * (1f - Mathf.Cos(th));
            float r = radius * Mathf.Sin(th) * (1f + 0.14f * Mathf.Cos(th));   // fuller below the middle
            for (int k = 0; k <= sides; k++) {
                float a = Mathf.PI * 0.5f + k * Mathf.PI * 2f / sides;           // the seam is at the back (+z)
                idx[i, k] = b.Vertex(new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a)), new Vector2(k / (float)sides, i / (float)rings));
            }
        }
        for (int i = 0; i < rings; i++)
            for (int k = 0; k < sides; k++)
                b.Quad(idx[i, k], idx[i + 1, k], idx[i + 1, k + 1], idx[i, k + 1]);
        Mesh m = b.ToMesh("Egg");
        Vector3[] normals = m.normals;                                            // clean poles
        for (int k = 0; k <= sides; k++) { normals[idx[0, k]] = Vector3.down; normals[idx[rings, k]] = Vector3.up; }
        m.normals = normals;
        meshes[key] = m;
        return m;
    }

    // A soft watch draped over the front edge of a block (SoftWatch.Drape): hands at ten past ten, ants on it.
    public static Mesh DrapedWatch(float radius) {
        string key = "drape" + radius.ToString("0.000");
        if (meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
        Mesh m = SoftWatch.BuildFace(radius, p => SoftWatch.Drape(p.x, p.y, radius, radius * 0.5f, 0.8f, 1.7f));   // the hands hang below the fold
        meshes[key] = m;
        return m;
    }

    // A wooden crutch, the prop that holds up the soft things in his paintings: one leg splitting into two rails
    // with a hand grip between them (the padded rest on top is a separate part). Foot at the origin, along +y.
    public static Mesh CrutchWood {
        get {
            if (meshes.TryGetValue("crutch", out Mesh cached) && cached != null) return cached;
            List<CombineInstance> parts = new List<CombineInstance>();
            Rod(parts, Vector3.zero, new Vector3(0f, 0.95f, 0f), 0.042f, 0.034f);
            for (int s = -1; s <= 1; s += 2) {
                Rod(parts, new Vector3(0f, 0.9f, 0f), new Vector3(s * 0.11f, 1.45f, 0f), 0.026f, 0.024f);
                Rod(parts, new Vector3(s * 0.11f, 1.44f, 0f), new Vector3(s * 0.12f, 2.03f, 0f), 0.024f, 0.024f);
            }
            Rod(parts, new Vector3(-0.12f, 1.4f, 0f), new Vector3(0.12f, 1.4f, 0f), 0.022f, 0.022f);
            Mesh m = new Mesh { name = "Crutch" };
            m.CombineMeshes(parts.ToArray(), true, true);
            m.RecalculateNormals();
            m.RecalculateBounds();
            meshes["crutch"] = m;
            return m;
        }
    }

    // The lips sofa (after his sofa shaped like a film star's lips): the upper lip is the backrest, the fuller lower
    // lip in front the seat, both pillowy. About 1.5 m wide and 0.85 m tall, base at the origin, facing -z.
    public static Mesh Lips() {
        if (meshes.TryGetValue("lips", out Mesh cached) && cached != null) return cached;
        MeshBuilder b = new MeshBuilder();
        LipBand(b, LipLine, UpperEdge, -0.06f, 0.1f, 0.4f);     // upper lip: the backrest
        LipBand(b, LowerEdge, LipLine, -0.32f, 0.15f, 0.27f);   // lower lip: the seat, its top running back to the backrest
        Mesh m = b.ToMesh("Lips");
        meshes["lips"] = m;
        return m;
    }

    const float LipsHalfWidth = 0.75f, LipsLift = 0.44f;

    static float UpperEdge(float x) {
        float a = Mathf.Abs(x);
        return 0.38f * (1f - Mathf.Pow(a / LipsHalfWidth, 2.2f)) + 0.05f * Bell((a - 0.2f) / 0.12f) - 0.08f * Bell(x / 0.08f);   // cupid's bow
    }

    static float LipLine(float x) { float a = Mathf.Abs(x) / LipsHalfWidth; return -0.03f * (1f - a * a) + 0.03f * Bell(x / 0.1f); }

    static float LowerEdge(float x) { return -0.42f * (1f - Mathf.Pow(Mathf.Abs(x) / LipsHalfWidth, 1.8f)); }

    static float Bell(float t) { return Mathf.Exp(-t * t); }

    // A lip: a strip between two curves across x that bulges towards the camera, plus its top surface running back.
    static void LipBand(MeshBuilder b, System.Func<float, float> lower, System.Func<float, float> upper, float front, float bulge, float depth) {
        const int N = 24, M = 5;
        int[,] grid = new int[N + 1, M + 1];
        Vector3[] top = new Vector3[N + 1];
        for (int i = 0; i <= N; i++) {
            float x = Mathf.Lerp(-LipsHalfWidth, LipsHalfWidth, i / (float)N);
            float plump = 1f - Mathf.Pow(Mathf.Abs(x) / LipsHalfWidth, 4f);       // flatter towards the corners
            for (int j = 0; j <= M; j++) {
                float t = j / (float)M;
                Vector3 p = new Vector3(x, Mathf.Lerp(lower(x), upper(x), t) + LipsLift, front - bulge * Mathf.Sin(Mathf.PI * t) * plump);
                grid[i, j] = b.Vertex(p, new Vector2(i / (float)N, t));
                if (j == M) top[i] = p;
            }
        }
        for (int i = 0; i < N; i++)
            for (int j = 0; j < M; j++)
                b.Quad(grid[i, j], grid[i, j + 1], grid[i + 1, j + 1], grid[i + 1, j]);
        Vector3 back = Vector3.forward * depth;
        for (int i = 0; i < N; i++)
            b.Quad(b.Vertex(top[i], Vector2.zero), b.Vertex(top[i] + back, Vector2.up), b.Vertex(top[i + 1] + back, Vector2.one), b.Vertex(top[i + 1], Vector2.right));
    }

    // A smooth tube along a path with a radius per point (both ends open; callers hide them).
    public static Mesh Tube(string name, Vector3[] path, float[] radii, int sides) {
        int n = path.Length, ring = sides + 1;
        Vector3[] verts = new Vector3[n * ring];
        Vector3[] normals = new Vector3[n * ring];
        Vector2[] uv = new Vector2[n * ring];
        Vector3 normal = Vector3.zero;
        for (int i = 0; i < n; i++) {
            Vector3 tangent = (path[Mathf.Min(i + 1, n - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
            // Carry the previous ring's frame along the path (parallel transport), so the tube never twists.
            normal = i == 0 ? Vector3.Cross(tangent, Vector3.forward) : Vector3.ProjectOnPlane(normal, tangent);
            if (normal.sqrMagnitude < 1e-8f) normal = Vector3.Cross(tangent, Vector3.up);
            normal.Normalize();
            Vector3 binormal = Vector3.Cross(tangent, normal);
            for (int k = 0; k < ring; k++) {
                float a = k * Mathf.PI * 2f / sides;
                Vector3 dir = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                verts[i * ring + k] = path[i] + dir * radii[i];
                normals[i * ring + k] = dir;
                uv[i * ring + k] = new Vector2(k / (float)sides, i / (n - 1f));
            }
        }
        int[] tris = new int[(n - 1) * sides * 6];
        int q = 0;
        for (int i = 0; i + 1 < n; i++)
            for (int k = 0; k < sides; k++) {
                int a = i * ring + k, b = a + 1, c = a + ring, d = c + 1;   // quad between two rings, wound outwards
                tris[q++] = a; tris[q++] = b; tris[q++] = c;
                tris[q++] = b; tris[q++] = d; tris[q++] = c;
            }
        Mesh m = new Mesh { name = name };
        m.vertices = verts;
        m.normals = normals;
        m.uv = uv;
        m.triangles = tris;
        m.RecalculateBounds();
        return m;
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t) {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
    }

    // A tapered rod from a to b (a 6-sided frustum) for combined meshes.
    static void Rod(List<CombineInstance> parts, Vector3 a, Vector3 b, float r0, float r1) {
        Vector3 d = b - a;
        parts.Add(new CombineInstance {
            mesh = WorldMeshes.Frustum(6, r0, r1, 1f),
            transform = Matrix4x4.TRS(a, Quaternion.FromToRotation(Vector3.up, d), new Vector3(1f, d.magnitude, 1f)),
        });
    }

    // ================================================================== textures and materials
    // A watch face: cream enamel aged towards the rim, a gold bezel and hour marks; with `hands`, the hands at ten
    // past ten and a few ants (they swarm the watches in his paintings). UV 0..1 across the disc.
    public static Texture2D WatchFace(bool hands) => MakeTexture(hands ? "watchHands" : "watch", 256, 256, (u, v) => {
        float x = u * 2f - 1f, y = v * 2f - 1f, r = Mathf.Sqrt(x * x + y * y);
        if (r > 0.86f) return Opaque(Gold * (0.7f + 0.5f * Mathf.Sin(Mathf.Clamp01((r - 0.86f) / 0.14f) * Mathf.PI)));   // rounded bezel
        if (Mathf.Abs(r - 0.8f) < 0.012f) return Opaque(Ink);                                                            // chapter ring
        float hour = Mathf.Atan2(x, y) / (Mathf.PI * 2f) * 12f;                  // 0 at twelve, clockwise
        int nearest = Mathf.RoundToInt(hour);
        float off = Mathf.Abs(hour - nearest) * (Mathf.PI * 2f / 12f) * r;      // distance to that hour's mark
        bool major = ((nearest % 3) + 3) % 3 == 0;
        if (r > (major ? 0.6f : 0.67f) && r < 0.78f && off < (major ? 0.035f : 0.018f)) return Opaque(Ink);
        if (hands) {
            if (Segment(x, y, 0f, 0f, -0.36f, 0.21f) < 0.03f || Segment(x, y, 0f, 0f, 0.5f, 0.29f) < 0.02f || r < 0.05f) return Opaque(Ink);
            if (Ant(x, y)) return Opaque(new Color(0.16f, 0.05f, 0.03f));
        }
        return Opaque(Color.Lerp(Cream, new Color(0.9f, 0.8f, 0.6f), r * r * 0.7f));
    });

    static readonly Vector3[] Ants = {   // x, y, heading (radians) on the face
        new Vector3(0.42f, -0.35f, 2.2f), new Vector3(0.2f, -0.52f, 2.9f), new Vector3(-0.15f, -0.6f, 3.4f),
        new Vector3(-0.5f, -0.3f, 4.2f), new Vector3(0.05f, 0.1f, 1.2f),
    };

    static bool Ant(float x, float y) {
        foreach (Vector3 a in Ants) {
            float dx = x - a.x, dy = y - a.y;
            if (dx * dx + dy * dy > 0.01f) continue;
            float c = Mathf.Cos(a.z), s = Mathf.Sin(a.z);
            float lx = dx * c + dy * s, ly = -dx * s + dy * c;                    // along / across the ant
            if (Blob(lx - 0.035f, ly, 0.014f, 0.012f) || Blob(lx, ly, 0.013f, 0.009f) || Blob(lx + 0.04f, ly, 0.022f, 0.015f)) return true;
            for (int k = -1; k <= 1; k++)                                          // six legs
                if (Segment(lx, Mathf.Abs(ly), k * 0.012f, 0f, k * 0.02f + 0.004f, 0.03f) < 0.004f) return true;
        }
        return false;
    }

    static bool Blob(float x, float y, float a, float b) { return (x / a) * (x / a) + (y / b) * (y / b) < 1f; }

    // A small feather (white with alpha: particles tint it black): a quill with a curved, ragged vane.
    public static Texture2D Feather => MakeTexture("feather", 32, 64, (u, v) => {
        float dx = u - (0.5f + 0.12f * (v - 0.5f) * (v - 0.5f));                 // the quill bends a little
        float vane = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01((v - 0.12f) / 0.88f)), 0.6f) * 0.42f;
        float barbs = (0.5f + 0.5f * Mathf.Sin(v * 46f + (dx > 0f ? 1.3f : 0f))) * 0.08f;
        float a = Mathf.Clamp01((vane - barbs - Mathf.Abs(dx)) * 25f) * 0.95f;
        if (Mathf.Abs(dx) < 0.035f && v < 0.97f) a = 1f;
        return new Color(1f, 1f, 1f, a);
    });

    // The painted sun's disc and rays. The disc carries thick yellow strokes running round, deepening to orange at
    // the rim, rosy cheeks and a calm surreal face in brown paint (sideways eyes, arched brows, a long nose, a
    // little smile and of course a curled mustache). The glow mask leaves the lines dark so the face reads through
    // the bloom.
    public static Material SunDisc => Glowing("sunDisc", SunTexture(false), SunTexture(true), new Color(1.5f, 1.05f, 0.35f), 0.15f);
    public static Material SunRay => Glowing("sunRay", RayStroke, RayStroke, new Color(1.8f, 1.15f, 0.35f), 0.15f);

    static Texture2D SunTexture(bool mask) => MakeTexture(mask ? "sunMask" : "sunDisc", 256, 256, (u, v) => {
        float x = u * 2f - 1f, y = v * 2f - 1f, r = Mathf.Sqrt(x * x + y * y);
        float turn = Mathf.Atan2(y, x) / (Mathf.PI * 2f) + 0.5f;
        Color paint = r < 0.5f ? Color.Lerp(new Color(1f, 0.95f, 0.62f), new Color(1f, 0.8f, 0.28f), r / 0.5f)
                               : Color.Lerp(new Color(1f, 0.8f, 0.28f), new Color(0.98f, 0.52f, 0.12f), Mathf.Clamp01((r - 0.5f) / 0.5f));
        paint *= Mathf.Lerp(0.86f, 1.1f, TexKit.Fbm(turn * 16f, r * 5f, 16, 611, 2));   // strokes running round
        float white = 0f, ink = 0f;
        if (r < 0.95f) {
            for (int s = -1; s <= 1; s += 2) {
                float cx = x - s * 0.5f, cy = y + 0.12f;                          // rosy cheek
                paint = Color.Lerp(paint, new Color(1f, 0.45f, 0.3f), 0.35f * Mathf.Clamp01(1f - Mathf.Sqrt(cx * cx + cy * cy) / 0.2f));
                float ex = x - s * 0.33f, ey = y - 0.2f;
                float e = Mathf.Sqrt((ex / 0.17f) * (ex / 0.17f) + (ey / 0.08f) * (ey / 0.08f));   // 1 on the almond's outline
                if (e < 1f) {
                    white = 1f;
                    float px = ex - 0.07f;                                         // both eyes glance the same way
                    if (px * px + ey * ey < 0.055f * 0.055f) ink = 1f;
                }
                ink = Mathf.Max(ink, Stroke(Mathf.Abs(e - 1f) * 0.08f, ey > 0f ? 0.016f : 0.008f));   // a heavier upper lid
                ink = Mathf.Max(ink, Stroke(Poly(ex * s, ey, Brow), 0.011f));
            }
            ink = Mathf.Max(ink, Stroke(Poly(x, y, Nose), 0.012f));
            ink = Mathf.Max(ink, Stroke(Poly(x, y, Mouth), 0.012f));
            ink = Mathf.Max(ink, Mustache(Mathf.Abs(x), y));
        }
        Color c = Color.Lerp(paint, new Color(0.99f, 0.96f, 0.88f), white);
        c = Color.Lerp(c, new Color(0.32f, 0.12f, 0.04f), ink);
        if (mask) c = paint * (1f - ink) * (1f - 0.5f * white);
        return Opaque(c);
    });

    static readonly Vector2[] Brow = { new Vector2(-0.17f, 0.11f), new Vector2(-0.06f, 0.2f), new Vector2(0.07f, 0.19f), new Vector2(0.17f, 0.12f) };
    static readonly Vector2[] Nose = { new Vector2(0.03f, 0.17f), new Vector2(0.07f, 0f), new Vector2(0.02f, -0.06f), new Vector2(-0.04f, -0.04f) };
    static readonly Vector2[] Mouth = { new Vector2(-0.12f, -0.29f), new Vector2(0f, -0.33f), new Vector2(0.12f, -0.29f) };
    static readonly Vector2[] MustachePath = {
        new Vector2(0f, -0.12f), new Vector2(0.1f, -0.15f), new Vector2(0.2f, -0.14f), new Vector2(0.29f, -0.09f),
        new Vector2(0.34f, 0f), new Vector2(0.35f, 0.09f), new Vector2(0.32f, 0.15f),
    };

    // The painted mustache: thick at the lip, tapering to fine upturned points.
    static float Mustache(float x, float y) {
        float best = 0f;
        for (int k = 0; k + 1 < MustachePath.Length; k++) {
            Vector2 a = MustachePath[k], b = MustachePath[k + 1];
            float w = Mathf.Lerp(0.04f, 0.006f, (k + 0.5f) / (MustachePath.Length - 1f));
            best = Mathf.Max(best, Stroke(Segment(x, y, a.x, a.y, b.x, b.y), w));
        }
        return best;
    }

    static float Poly(float x, float y, Vector2[] pts) {
        float d = float.MaxValue;
        for (int k = 0; k + 1 < pts.Length; k++) d = Mathf.Min(d, Segment(x, y, pts[k].x, pts[k].y, pts[k + 1].x, pts[k + 1].y));
        return d;
    }

    static float Stroke(float distance, float width) { return Mathf.Clamp01((width - distance) / 0.006f + 0.5f); }

    // One brush stroke for the sun's rays: streaks of yellow and orange along its length (v).
    static Texture2D RayStroke => MakeTexture("rayStroke", 32, 128, (u, v) => {
        float streak = TexKit.Fbm(u * 6f, v * 2f, 6, 621, 3);
        Color c = Color.Lerp(new Color(1f, 0.58f, 0.12f), new Color(1f, 0.9f, 0.45f), Mathf.Clamp01(streak * 1.4f - 0.15f));
        return Opaque(c * (0.9f + 0.12f * Mathf.Sin(u * 38f + streak * 5f)));   // bristle lines
    });

    // A copy of the emissive base material with an albedo and a glow mask (HDR emission blooms), cached by key.
    public static Material Glowing(string key, Texture2D albedo, Texture2D mask, Color emission, float smooth) {
        if (materials.TryGetValue(key, out Material m) && m != null) return m;
        m = new Material(GameAssets.I.litEmissive) { name = "Dali " + key };
        m.SetTexture("_BaseMap", albedo);
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_EmissionMap", mask);
        m.SetColor("_EmissionColor", emission);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", 0f);
        materials[key] = m;
        return m;
    }

    static Texture2D MakeTexture(string key, int w, int h, System.Func<float, float, Color> fn) {
        if (textures.TryGetValue(key, out Texture2D t) && t != null) return t;
        t = new Texture2D(w, h, TextureFormat.RGBA32, true) {
            name = "Dali " + key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 2,
        };
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = fn((x + 0.5f) / w, (y + 0.5f) / h);
        t.SetPixels(px);
        t.Apply(true, true);
        textures[key] = t;
        return t;
    }

    public static float Segment(float px, float py, float ax, float ay, float bx, float by) {
        float dx = bx - ax, dy = by - ay;
        float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy + 1e-9f));
        float ex = px - ax - t * dx, ey = py - ay - t * dy;
        return Mathf.Sqrt(ex * ex + ey * ey);
    }

    static Color Opaque(Color c) { c.a = 1f; return c; }
}

// A soft pocket watch, after the famous melting watches: a gold-cased disc whose lower part sags, narrows and runs
// into drips. melt = 0 is a flat watch, 1 a watch melted to about twice its height. An animated watch (weapons)
// owns its mesh and rebuilds it whenever `melt` changes; a draped one (fortress decor) is built once (DaliArt).
public class SoftWatch : MonoBehaviour {
    const int Rings = 5, Segments = 28;
    const float Thickness = 0.1f;               // of the case, as a share of the radius

    public float melt;                          // 0..1
    public float handSpeed;                     // degrees per second of the minute hand; positive = clockwise
    public float phase;                         // shifts the drips along the hem

    float radius, minute = -60f, hour = 60f;    // ten past ten
    float built = -1f;
    Mesh mesh;
    Vector3[] flat, verts;
    Transform hands, minuteHand, hourHand;

    public static SoftWatch Create(Transform parent, float radius, float melt, bool withHands) {
        GameObject go = ModelKit.Root("Soft Watch", parent);
        if (parent != null) go.layer = parent.gameObject.layer;
        SoftWatch w = go.AddComponent<SoftWatch>();
        w.radius = radius;
        w.melt = melt;
        w.phase = Random.Range(0f, 6f);
        w.flat = Layout(radius, true, out Vector2[] uv);
        w.verts = new Vector3[w.flat.Length];
        w.mesh = new Mesh { name = "Soft Watch" };
        w.mesh.MarkDynamic();
        w.mesh.vertices = w.flat;
        w.mesh.uv = uv;
        w.mesh.subMeshCount = 2;
        List<int> face = new List<int>(), gold = new List<int>();
        Disc(face, 0, false);
        CaseTriangles(gold);
        w.mesh.SetTriangles(face, 0);
        w.mesh.SetTriangles(gold, 1);
        go.AddComponent<MeshFilter>().sharedMesh = w.mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { Mats.LitTex(DaliArt.WatchFace(false), Color.white, 0.55f), DaliArt.GoldMetal };

        float t = radius * Thickness;                // the crown and its bow on top (the top never melts)
        ModelKit.MeshPart(go.transform, WorldMeshes.Frustum(8, radius * 0.1f, radius * 0.1f, radius * 0.16f), new Vector3(0f, radius * 0.97f, t * 0.5f),
                          Vector3.one, DaliArt.GoldMetal, default, "Crown");
        ModelKit.MeshPart(go.transform, WorldMeshes.Ball(radius * 0.12f), new Vector3(0f, radius * 1.24f, t * 0.5f), new Vector3(1.4f, 1.3f, 0.45f),
                          DaliArt.GoldMetal, default, "Bow");
        if (withHands) w.AddHands();
        w.Rebuild();
        return w;
    }

    // Free melting: the lower part sags, narrows and runs into uneven drips; the sides curl back a little.
    public static Vector3 Melt(float x, float y, float z, float r, float melt, float phase) {
        float below = Mathf.Clamp01((0.4f - y / r) / 1.4f);                 // 0 above the upper third, 1 at the bottom
        float b2 = below * below;
        float sag = melt * b2 * 0.95f * r;
        float drips = melt * b2 * below * (0.55f + 0.45f * Mathf.Sin(x / r * 6.5f + phase)) * 0.45f * r;
        float curl = melt * 0.18f * r * (x / r) * (x / r) * (0.3f + below);
        return new Vector3(x * (1f - 0.25f * melt * below), y - sag - drips, z + curl);
    }

    // Draped over an edge running along x through the origin (the block lies below and behind it: -y, +z). The
    // disc folds at flat height `fold`: above it the watch lies on the block's top, below it hangs down the front
    // face and melts.
    public static Vector3 Drape(float x, float y, float r, float fold, float melt, float phase) {
        const float Skin = 0.012f;                                           // distance from the stone
        float s = y - fold;
        if (s > 0f) {
            float wrap = Mathf.PI * 0.5f * Skin;
            if (s < wrap) { float a = s / Skin; return new Vector3(x, Skin * Mathf.Sin(a), -Skin * Mathf.Cos(a)); }
            return new Vector3(x, Skin, s - wrap);
        }
        float hang = r + fold, k = Mathf.Clamp01(-s / hang);
        float sag = melt * k * k * 0.6f * hang;
        float drips = melt * k * k * k * (0.55f + 0.45f * Mathf.Sin(x / r * 6.5f + phase)) * 0.4f * r;
        return new Vector3(x * (1f - 0.2f * melt * k), s - sag - drips, -Skin - melt * 0.06f * r * k);
    }

    // The face alone, deformed once (draped decor watches).
    public static Mesh BuildFace(float radius, System.Func<Vector3, Vector3> deform) {
        Vector3[] p = Layout(radius, false, out Vector2[] uv);
        for (int i = 0; i < p.Length; i++) p[i] = deform(p[i]);
        List<int> tris = new List<int>();
        Disc(tris, 0, false);
        Mesh m = new Mesh { name = "Draped Watch" };
        m.vertices = p;
        m.uv = uv;
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    void AddHands() {
        Material ink = Mats.Lit(DaliArt.Ink, 0.4f);
        hands = ModelKit.Root("Hands", transform).transform;
        minuteHand = Hand(hands, ink, radius * 0.66f, radius * 0.045f);
        hourHand = Hand(hands, ink, radius * 0.44f, radius * 0.065f);
        ModelKit.MeshPart(hands, WorldMeshes.Ball(radius * 0.06f), new Vector3(0f, 0f, -0.002f), new Vector3(1f, 1f, 0.4f), DaliArt.GoldMetal, default, "Pin");
        SetHands();
    }

    static Transform Hand(Transform parent, Material m, float length, float width) {
        Transform pivot = ModelKit.Root("Hand", parent).transform;
        ModelKit.MeshPart(pivot, WorldMeshes.Quad(), new Vector3(0f, length * 0.42f, 0f), new Vector3(width, length, 1f), m);
        return pivot;
    }

    void Update() {
        if (handSpeed == 0f || minuteHand == null) return;
        minute = Mathf.Repeat(minute - handSpeed * Time.deltaTime, 360f);
        hour = Mathf.Repeat(hour - handSpeed / 12f * Time.deltaTime, 360f);
        SetHands();
    }

    void LateUpdate() { if (melt != built) Rebuild(); }

    void OnDestroy() { if (mesh != null) Destroy(mesh); }

    void SetHands() {
        minuteHand.localRotation = Quaternion.Euler(0f, 0f, minute);
        hourHand.localRotation = Quaternion.Euler(0f, 0f, hour);
    }

    void Rebuild() {
        for (int i = 0; i < flat.Length; i++) verts[i] = Melt(flat[i].x, flat[i].y, flat[i].z, radius, melt, phase);
        mesh.vertices = verts;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (hands != null) hands.localPosition = Melt(0f, 0f, 0f, radius, melt, phase) + new Vector3(0f, 0f, -0.012f * radius - 0.003f);
        built = melt;
    }

    // ------------------------------------------------------------------ layout
    // Face (centre + rings), back (the same at z = thickness) and the rim (the outer ring again, front and back).
    static Vector3[] Layout(float r, bool withCase, out Vector2[] uv) {
        int face = 1 + Rings * Segments;
        int count = withCase ? face * 2 + Segments * 2 : face;
        Vector3[] p = new Vector3[count];
        uv = new Vector2[count];
        for (int layer = 0; layer < (withCase ? 2 : 1); layer++) {
            int o = layer * face;
            float z = layer * r * Thickness;
            p[o] = new Vector3(0f, 0f, z);
            uv[o] = new Vector2(0.5f, 0.5f);
            for (int ring = 1; ring <= Rings; ring++)
                for (int s = 0; s < Segments; s++) {
                    float a = s * Mathf.PI * 2f / Segments, rr = r * ring / Rings;
                    int i = o + Ring(ring, s);
                    p[i] = new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, z);
                    uv[i] = new Vector2(0.5f + p[i].x / (2f * r), 0.5f + p[i].y / (2f * r));
                }
        }
        if (withCase)
            for (int s = 0; s < Segments; s++) {
                int front = face * 2 + s, back = face * 2 + Segments + s;
                p[front] = p[Ring(Rings, s)];
                p[back] = p[face + Ring(Rings, s)];
                uv[front] = uv[Ring(Rings, s)];
                uv[back] = uv[front];
            }
        return p;
    }

    static int Ring(int ring, int s) { return 1 + (ring - 1) * Segments + (s % Segments); }

    // A disc facing -z (or +z for the back): a fan round the centre, then quads between the rings.
    static void Disc(List<int> tris, int o, bool back) {
        for (int s = 0; s < Segments; s++) {
            Tri(tris, o, o + Ring(1, s + 1), o + Ring(1, s), back);
            for (int ring = 1; ring < Rings; ring++) {
                int a = o + Ring(ring, s), b = o + Ring(ring, s + 1), c = o + Ring(ring + 1, s), d = o + Ring(ring + 1, s + 1);
                Tri(tris, a, d, c, back);
                Tri(tris, a, b, d, back);
            }
        }
    }

    // The gold case: the back disc and the rim joining it to the face.
    static void CaseTriangles(List<int> tris) {
        int face = 1 + Rings * Segments;
        Disc(tris, face, true);
        int f = face * 2, k = f + Segments;
        for (int s = 0; s < Segments; s++) {
            int o0 = f + s, o1 = f + (s + 1) % Segments, q0 = k + s, q1 = k + (s + 1) % Segments;
            tris.Add(o0); tris.Add(o1); tris.Add(q0);
            tris.Add(o1); tris.Add(q1); tris.Add(q0);
        }
    }

    static void Tri(List<int> t, int a, int b, int c, bool flip) {
        t.Add(a);
        if (flip) { t.Add(c); t.Add(b); } else { t.Add(b); t.Add(c); }
    }
}

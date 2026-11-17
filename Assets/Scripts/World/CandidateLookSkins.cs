using UnityEngine;

// Block skins of the Candidate's fortress (the same physics, a campaign headquarters):
//   Stone: pale civic limestone in courses of tall and short blocks, tight joints, every block framed by a border line
//          carved just inside its edges.
//   Wood:  boards painted navy, the grain faint under the paint, a thin teal pinstripe along each.
//   Metal: brushed steel plates with a row of domed rivets round their edges and a small star embossed in the middle,
//          its steel touched with gold.
//   Glass: blue-tinted office glass in slim steel frames, a faint frosted tick in a circle on every pane.
// The God already has white marble and the Gambler gold-railed baize, so the Candidate's stone stays plain limestone and
// his colours (navy, teal, gold) stay in the paint, the pinstripes and the stars. Like the other class skins: 256 x 256
// textures that tile once per metre, all three damage stages made together the first time a material is asked for, one
// cached material per material and stage. Ice and powder kegs keep the neutral look (null).
// His mark (a tick in a circle) and the star are drawn here from geometry; CandidateDecor paints with them too.
public static class CandidateSkins {
    const int S = 256;                   // texels per metre
    const float Texel = 1f / S;          // metres per texel
    const float GlassMark = 0.07f;       // radius of the frosted mark on a pane (m)
    static readonly Material[][] sets = new Material[6][];

    public static Material Block(BlockMaterial m, int stage) {
        if (m == BlockMaterial.Ice || m == BlockMaterial.Explosive || GameAssets.I == null) return null;
        Material[] set = sets[(int)m];
        if (set == null || set[0] == null) {
            set = Build(m);
            sets[(int)m] = set;
        }
        return set[Mathf.Clamp(stage, 0, 2)];
    }

    // What a painter produces: the albedo (alpha = opacity for glass) and the surface.
    class Look {
        public readonly Color[] albedo = new Color[S * S];
        public float smooth = 0.2f, metal;
        public bool glass;
    }

    static Material[] Build(BlockMaterial m) {
        Look look = m == BlockMaterial.Stone ? Limestone() : m == BlockMaterial.Wood ? NavyBoards()
                  : m == BlockMaterial.Metal ? BrushedSteel() : OfficeGlass();
        string name = "Candidate " + m;
        Material[] set = new Material[3];
        for (int stage = 0; stage < 3; stage++) {
            Color[] px = stage == 0 ? look.albedo : Damaged(look.albedo, stage, look.glass, 70 + (int)m * 11);
            Texture2D tex = ToTexture(px, name + " " + stage);
            set[stage] = look.glass ? Mats.Transparent(Color.white, look.smooth, tex) : Mats.LitTex(tex, Color.white, look.smooth, look.metal);
        }
        return set;
    }

    // ------------------------------------------------------------------ painters
    // Pale limestone in alternating courses, a tall one (0.3 m) and a short one (0.2 m) twice a metre, of blocks half a
    // metre long (the short courses offset by a quarter), with fine pores and a little weathering at their feet; tight
    // joints, and a border line carved 3.5 cm inside every block's edges (a dark groove, the panel's edge lit inside it).
    static Look Limestone() {
        Look l = new Look { smooth = 0.14f };
        Color stone = new Color(0.86f, 0.82f, 0.72f), joint = new Color(0.58f, 0.54f, 0.47f);
        Each((u, v, x, y, i) => {
            int row = v < 0.3f ? 0 : v < 0.5f ? 1 : v < 0.8f ? 2 : 3;
            float bed = row == 0 ? 0f : row == 1 ? 0.3f : row == 2 ? 0.5f : 0.8f, h = row % 2 == 0 ? 0.3f : 0.2f;
            float ly = v - bed;                                                    // metres above the course's bed joint
            float su = Frac(u + (row % 2) * 0.25f) * 2f;
            int col = Mathf.FloorToInt(su);
            float lx = (su - col) * 0.5f;                                          // metres along the block
            float e = Mathf.Min(Mathf.Min(lx, 0.5f - lx), Mathf.Min(ly, h - ly));  // metres to the nearest joint
            Color c = stone * (0.94f + 0.1f * Hash(row * 2 + col, 1, 601)) * Mathf.Lerp(0.9f, 1.05f, Noise(u, v, 8, 602, 3));
            c *= Mathf.Lerp(0.93f, 1f, Mathf.Clamp01(ly / h * 2.5f));
            if (Hash(x, y, 603) > 0.986f) c *= 0.78f;                             // pores
            float g = e - 0.035f;                                                  // the carved border line
            if (Mathf.Abs(g) < 0.0035f) c *= 0.62f;
            else if (g > 0f && g < 0.0075f) c *= 1.07f;
            if (e < 0.0045f) c = joint * Mathf.Lerp(0.9f, 1.08f, Noise(u, v, 16, 604, 2));
            else if (e < 0.009f) c *= 0.86f;                                       // the arris
            l.albedo[i] = Opaque(c);
        });
        return l;
    }

    // Boards a quarter metre wide painted navy (satin), the grain faint under the paint and a butt joint somewhere along
    // each metre of every board; a thin teal pinstripe runs 4.5 cm under the top of each board.
    static Look NavyBoards() {
        Look l = new Look { smooth = 0.36f };
        Color paint = new Color(0.15f, 0.2f, 0.4f), teal = CandidateKit.Teal;
        Each((u, v, x, y, i) => {
            int board = Mathf.FloorToInt(v * 4f);
            float py = (v * 4f - board) * 0.25f;                                   // metres above the board's lower edge
            float grain = Noise(Frac(u + Hash(board, 9, 611)), v * 16f, 4, 612 + board, 3);
            float rings = 0.5f + 0.5f * Mathf.Sin((grain * 10f + py * 6f) * Mathf.PI);
            Color c = paint * (0.93f + 0.1f * Hash(board, 3, 613)) * Mathf.Lerp(0.92f, 1.06f, rings);
            c *= Mathf.Lerp(0.95f, 1.04f, Noise(u, v, 16, 614, 2));                 // brush marks
            c = Color.Lerp(c, teal, Cover(Mathf.Abs(0.205f - py) - 0.0028f, Texel) * 0.9f);
            float joint = Mathf.Min(py, 0.25f - py);
            float butt = Mathf.Abs(Frac(u - Hash(board, 5, 615) + 0.5f) - 0.5f);   // metres to the butt joint
            if (joint < 0.004f || butt < 0.003f) c = paint * 0.28f;
            else if (joint < 0.009f) c *= py > 0.125f ? 1.1f : 0.78f;              // the top edge catches the light
            l.albedo[i] = Opaque(c);
        });
        return l;
    }

    // Brushed steel plates (2 x 2 per metre) with fine horizontal brushing and a soft band of sheen, dark seams with
    // rolled edges, five domed rivets along every edge, and a small star embossed in the middle of each, its steel
    // touched with gold (its upper edges lit, its lower edges and its shadow on the plate dark).
    static Look BrushedSteel() {
        Look l = new Look { smooth = 0.64f, metal = 0.7f };
        Color steel = new Color(0.68f, 0.71f, 0.74f), star = Color.Lerp(steel, CandidateKit.Gold, 0.35f);
        const float StarSize = 0.065f, Lip = 0.006f;   // the star's outer radius and the width of its lit and dark edges (m)
        Each((u, v, x, y, i) => {
            float e = Plates(u, v, 2, out int id, out float lx, out float ly);
            float brush = Mathf.Lerp(0.84f, 1.1f, Noise(u, v * 40f, 2, 622, 3));
            Color c = steel * (0.93f + 0.1f * Hash(id, 1, 621)) * brush;
            c *= 1f + 0.07f * Mathf.Sin(ly * 2f * Mathf.PI);
            if (e < 0.005f) c = steel * 0.3f;
            else if (e < 0.011f) c *= 1.16f;
            NearestRivet(lx, ly, out float rx, out float ry);
            float r2 = rx * rx + ry * ry;
            if (r2 < 0.0085f * 0.0085f) c = steel * (ry > 0f ? 1.3f : 0.72f);     // lit from above
            else if (r2 < 0.012f * 0.012f && ry < 0f) c *= 0.66f;                  // its shadow
            float dx = lx - 0.25f, dy = ly - 0.25f;
            bool near = Mathf.Abs(dx) < StarSize + 0.01f && Mathf.Abs(dy) < StarSize + 0.01f;   // only test the star near it
            if (near && InStar(dx, dy, StarSize)) {
                c = star * brush;
                if (!InStar(dx - Lip, dy + Lip, StarSize)) c *= 1.35f;           // light from the upper left
                else if (!InStar(dx + Lip, dy - Lip, StarSize)) c *= 0.66f;
            } else if (near && InStar(dx - 0.004f, dy + 0.008f, StarSize)) c *= 0.72f;   // its shadow, down and to the right
            l.albedo[i] = Opaque(c);
        });
        return l;
    }

    // Blue-tinted office glass in panes half a metre wide and a metre tall, slim steel frames, the sky caught in the top
    // of each pane and two soft glints, and a faint frosted tick in a circle in the middle of every pane.
    static Look OfficeGlass() {
        Look l = new Look { smooth = 0.93f, glass = true };
        Color tint = new Color(0.4f, 0.6f, 0.85f, 0.42f), frost = new Color(0.9f, 0.95f, 1f, 0.62f);
        Color frame = new Color(0.6f, 0.63f, 0.68f, 1f), gasket = new Color(0.2f, 0.24f, 0.3f, 0.85f);
        Each((u, v, x, y, i) => {
            float px = Frac(u * 2f) * 0.5f;                                        // metres across the pane
            float e = Mathf.Min(Mathf.Min(px, 0.5f - px), Mathf.Min(v, 1f - v));   // metres to the frame
            float k = Mathf.Lerp(0.82f, 1.14f, v);
            Color c = new Color(tint.r * k, tint.g * k, tint.b * k, tint.a);
            c = Color.Lerp(c, new Color(0.88f, 0.95f, 1f, 0.66f), Streak(px * 2f, v) * 0.4f);
            float mx = (px - 0.25f) / GlassMark, my = (v - 0.5f) / GlassMark, t = Texel / GlassMark;
            c = Color.Lerp(c, frost, Mathf.Max(Cover(MarkRing(mx, my), t), Cover(MarkTick(mx, my), t)) * 0.45f);
            if (e < 0.008f) c = frame * Mathf.Lerp(0.9f, 1.1f, Noise(u, v, 8, 631, 2));
            else if (e < 0.012f) c = Color.Lerp(c, gasket, 0.55f);
            l.albedo[i] = c;
        });
        return l;
    }

    // ------------------------------------------------------------------ his mark and the star
    // His mark, a tick in a circle, as signed distances (negative inside) in units of its radius, for (x, y) from its
    // centre: the ring, and the tick inside it (a short arm down to the corner and a long one up to the right).
    internal static float MarkRing(float x, float y) { return Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 0.84f) - 0.08f; }

    internal static float MarkTick(float x, float y) {
        return Mathf.Min(Segment(x, y, -0.42f, 0.02f, -0.12f, -0.3f), Segment(x, y, -0.12f, -0.3f, 0.46f, 0.36f)) - 0.11f;
    }

    // How much of a texel `texel` long a shape covers at signed distance d (same units): soft over one texel.
    internal static float Cover(float d, float texel) { return Mathf.Clamp01(0.5f - d / texel); }

    // Whether (x, y) lies inside a five-pointed star of outer radius r with a point up.
    internal static bool InStar(float x, float y, float r) {
        const float Seg = Mathf.PI / 5f;                                   // from a point to the next notch
        float a = Mathf.Repeat(Mathf.Atan2(x, y), 2f * Seg);              // the angle from up, folded onto one point
        if (a > Seg) a = 2f * Seg - a;
        float rho = Mathf.Sqrt(x * x + y * y);
        float px = rho * Mathf.Cos(a), py = rho * Mathf.Sin(a);           // the same point with its star point along +x
        float ex = 0.382f * r * Mathf.Cos(Seg) - r, ey = 0.382f * r * Mathf.Sin(Seg);   // the edge from the point to the notch
        return ex * py - ey * (px - r) > 0f;
    }

    static float Segment(float x, float y, float ax, float ay, float bx, float by) {
        float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
        float t = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy));
        float ex = px - dx * t, ey = py - dy * t;
        return Mathf.Sqrt(ex * ex + ey * ey);
    }

    // ------------------------------------------------------------------ patterns
    // The offset (m) from the nearest rivet on a half-metre plate: rows of five, 3 cm in from each edge, corners shared.
    static void NearestRivet(float lx, float ly, out float rx, out float ry) {
        const float In = 0.03f, Step = 0.11f;
        float ax = lx - (In + Mathf.Clamp(Mathf.Round((lx - In) / Step), 0f, 4f) * Step);
        float ay = ly - (ly < 0.25f ? In : 0.5f - In);
        float bx = lx - (lx < 0.25f ? In : 0.5f - In);
        float by = ly - (In + Mathf.Clamp(Mathf.Round((ly - In) / Step), 0f, 4f) * Step);
        bool row = ax * ax + ay * ay <= bx * bx + by * by;
        rx = row ? ax : bx;
        ry = row ? ay : by;
    }

    delegate void PixelFn(float u, float v, int x, int y, int i);

    static void Each(PixelFn fn) {
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                fn((x + 0.5f) / S, (y + 0.5f) / S, x, y, y * S + x);
    }

    // A grid of n x n square plates per metre: distance (m) to the plate edge and the position inside it (m).
    static float Plates(float u, float v, int n, out int id, out float lx, out float ly) {
        int cx = Mathf.FloorToInt(u * n), cy = Mathf.FloorToInt(v * n);
        float size = 1f / n;
        lx = u - cx * size;
        ly = v - cy * size;
        id = cy * 7 + cx;
        return Mathf.Min(Mathf.Min(lx, size - lx), Mathf.Min(ly, size - ly));
    }

    // A soft diagonal glint for glass.
    static float Streak(float u, float v) {
        float s = u + v;
        return Mathf.Clamp01(1f - Mathf.Abs(s - 0.7f) * 12f) + Mathf.Clamp01(1f - Mathf.Abs(s - 0.86f) * 30f);
    }

    // Tileable noise: (u, v) scaled by integer multiples of `cells` keep the tile seamless.
    static float Noise(float u, float v, int cells, int seed, int octaves) { return TexKit.Fbm(u * cells, v * cells, cells, seed, octaves); }

    static float Hash(int x, int y, int seed) { return TexKit.Hash(x, y, seed); }

    static float Frac(float x) { return x - Mathf.Floor(x); }

    static Color Opaque(Color c) { c.a = 1f; return c; }

    // ------------------------------------------------------------------ damage and upload
    // Cracks wandering across the tile (more and wider at stage 2), plus grime at stage 2; glass cracks frost white.
    static Color[] Damaged(Color[] src, int stage, bool glass, int seed) {
        Color[] px = (Color[])src.Clone();
        System.Random rng = new System.Random(seed * 13 + stage * 101);
        int lines = stage == 1 ? 4 : 10;
        for (int l = 0; l < lines; l++) {
            float x = (float)rng.NextDouble() * S, y = (float)rng.NextDouble() * S;
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            int len = 50 + rng.Next(stage == 1 ? 70 : 140);
            for (int i = 0; i < len; i++) {
                ang += ((float)rng.NextDouble() - 0.5f) * 0.9f;
                if (rng.NextDouble() < 0.04) ang += (rng.NextDouble() < 0.5 ? 1f : -1f) * 0.9f;   // kink
                x += Mathf.Cos(ang) * 1.5f;
                y += Mathf.Sin(ang) * 1.5f;
                int w = stage == 2 && i < len / 3 ? 2 : 1;
                for (int oy = -w; oy <= w; oy++)
                    for (int ox = -w; ox <= w; ox++) {
                        int k = (((int)y + oy) % S + S) % S * S + (((int)x + ox) % S + S) % S;
                        Color c = px[k];
                        if (glass) c = Color.Lerp(c, new Color(0.88f, 0.94f, 1f, 0.92f), 0.75f);
                        else { float a = c.a; c *= ox == 0 && oy == 0 ? 0.25f : 0.6f; c.a = a; }
                        px[k] = c;
                    }
            }
        }
        if (stage == 2 && !glass)
            Each((u, v, x, y, i) => {
                Color c = px[i] * Mathf.Lerp(0.66f, 0.92f, Noise(u, v, 3, seed, 2));
                c.a = 1f;
                px[i] = c;
            });
        return px;
    }

    static Texture2D ToTexture(Color[] px, string name) {
        Texture2D t = new Texture2D(S, S, TextureFormat.RGBA32, true) {
            name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4,
        };
        t.SetPixels(px);
        t.Apply(true, true);   // mipmaps; the pixels stay on the GPU only
        return t;
    }
}

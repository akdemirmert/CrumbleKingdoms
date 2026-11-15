using System;
using System.Collections.Generic;
using UnityEngine;

// Collects vertices and triangles for a procedural mesh.
// Winding rule (Unity): a triangle is front-facing when its corners run clockwise as seen from the front,
// so quads are given as bottom-left, top-left, top-right, bottom-right "as the viewer sees them".
public class MeshBuilder {
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Vector2> uvs = new List<Vector2>();
    readonly List<Color> colors = new List<Color>();
    readonly List<int> tris = new List<int>();

    public int Vertex(Vector3 p, Vector2 uv) { return Vertex(p, uv, Color.white); }

    public int Vertex(Vector3 p, Vector2 uv, Color c) {
        verts.Add(p);
        uvs.Add(uv);
        colors.Add(c);
        return verts.Count - 1;
    }

    public void Triangle(int a, int b, int c) { tris.Add(a); tris.Add(b); tris.Add(c); }

    public void Quad(int bl, int tl, int tr, int br) { Triangle(bl, tl, tr); Triangle(bl, tr, br); }

    // A facet with its own three vertices, so it keeps its own normal (flat, low-poly shading).
    public void Facet(Vector3 a, Vector3 b, Vector3 c, Func<Vector3, Vector2> uv) {
        Triangle(Vertex(a, uv(a)), Vertex(b, uv(b)), Vertex(c, uv(c)));
    }

    public void FacetQuad(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Func<Vector3, Vector2> uv) {
        Facet(bl, tl, tr, uv);
        Facet(bl, tr, br, uv);
    }

    public Mesh ToMesh(string name) {
        Mesh m = new Mesh { name = name };
        if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(verts);
        m.SetUVs(0, uvs);
        m.SetColors(colors);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}

// Cached low-poly shapes used by the world, the fortress decorations and the commanders.
// UVs are in metres (textures are tiled through the material), like MeshKit.
public static class WorldMeshes {
    static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

    static bool Cached(string key, out Mesh m) { return cache.TryGetValue(key, out m) && m != null; }

    static Mesh Store(string key, Mesh m) { cache[key] = m; return m; }

    static string F(float v) { return v.ToString("0.000"); }

    // Tapered prism along +Y with its base centred on the origin: limbs, torsos, trunks, hats, poles.
    public static Mesh Frustum(int sides, float r0, float r1, float height) {
        string key = "frustum" + sides + "_" + F(r0) + "_" + F(r1) + "_" + F(height);
        if (Cached(key, out Mesh cached)) return cached;
        MeshBuilder b = new MeshBuilder();
        Vector3 top = Vector3.up * height;
        float circ = Mathf.PI * (r0 + r1);
        for (int i = 0; i < sides; i++) {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            Vector3 p0 = d0 * r0, p1 = d1 * r0, q0 = d0 * r1 + top, q1 = d1 * r1 + top;
            float u0 = circ * i / sides, u1 = circ * (i + 1) / sides;
            int bl = b.Vertex(p0, new Vector2(u0, 0f)), tl = b.Vertex(q0, new Vector2(u0, height));
            int tr = b.Vertex(q1, new Vector2(u1, height)), br = b.Vertex(p1, new Vector2(u1, 0f));
            b.Quad(bl, tl, tr, br);
            if (r1 > 0.001f) b.Facet(top, q1, q0, p => new Vector2(p.x, p.z));
            if (r0 > 0.001f) b.Facet(Vector3.zero, p0, p1, p => new Vector2(p.x, p.z));
        }
        return Store(key, b.ToMesh("Frustum"));
    }

    // Low-poly half sphere (y >= 0): helmets, domes, cactus tops.
    public static Mesh Dome(int sides, int rings, float radius) {
        string key = "dome" + sides + "_" + rings + "_" + F(radius);
        if (Cached(key, out Mesh cached)) return cached;
        MeshBuilder b = new MeshBuilder();
        Func<Vector3, Vector2> uv = p => new Vector2(p.x + p.z, p.y);
        for (int r = 0; r < rings; r++) {
            float f0 = r * Mathf.PI * 0.5f / rings, f1 = (r + 1) * Mathf.PI * 0.5f / rings;
            for (int s = 0; s < sides; s++) {
                float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
                Vector3 bl = Sphere(f0, a0) * radius, br = Sphere(f0, a1) * radius;
                Vector3 tl = Sphere(f1, a0) * radius, tr = Sphere(f1, a1) * radius;
                if (r == rings - 1) b.Facet(bl, tl, br, uv);   // the top ring closes on the pole
                else b.FacetQuad(bl, tl, tr, br, uv);
            }
        }
        return Store(key, b.ToMesh("Dome"));
    }

    static Vector3 Sphere(float lat, float lon) {
        return new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
    }

    // Flat disc in the XY plane facing -Z (the camera). UVs 0..1 across the disc.
    public static Mesh Disc(int sides, float radius) {
        string key = "disc" + sides + "_" + F(radius);
        if (Cached(key, out Mesh cached)) return cached;
        MeshBuilder b = new MeshBuilder();
        int c = b.Vertex(Vector3.zero, new Vector2(0.5f, 0.5f));
        int first = -1, prev = -1;
        for (int i = 0; i < sides; i++) {
            float a = i * Mathf.PI * 2f / sides;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            int v = b.Vertex(d * radius, d * 0.5f + new Vector2(0.5f, 0.5f));
            if (prev >= 0) b.Triangle(c, v, prev);
            else first = v;
            prev = v;
        }
        b.Triangle(c, first, prev);
        return Store(key, b.ToMesh("Disc"));
    }

    // Quad in the XY plane facing -Z, centred, UVs 0..1, vertex colour white.
    public static Mesh Quad() {
        if (Cached("quad", out Mesh cached)) return cached;
        MeshBuilder b = new MeshBuilder();
        b.Quad(b.Vertex(new Vector3(-0.5f, -0.5f, 0f), new Vector2(0f, 0f)), b.Vertex(new Vector3(-0.5f, 0.5f, 0f), new Vector2(0f, 1f)),
               b.Vertex(new Vector3(0.5f, 0.5f, 0f), new Vector2(1f, 1f)), b.Vertex(new Vector3(0.5f, -0.5f, 0f), new Vector2(1f, 0f)));
        return Store("quad", b.ToMesh("Quad"));
    }

    // Five-pointed star in the XY plane facing -Z (hat decorations, emblems).
    public static Mesh Star(float outer, float inner) {
        string key = "star" + F(outer) + "_" + F(inner);
        if (Cached(key, out Mesh cached)) return cached;
        MeshBuilder b = new MeshBuilder();
        int c = b.Vertex(Vector3.zero, new Vector2(0.5f, 0.5f));
        int[] ring = new int[10];
        for (int i = 0; i < 10; i++) {
            float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
            float r = i % 2 == 0 ? outer : inner;
            ring[i] = b.Vertex(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f), new Vector2(0.5f, 0.5f));
        }
        for (int i = 0; i < 10; i++) b.Triangle(c, ring[(i + 1) % 10], ring[i]);
        return Store(key, b.ToMesh("Star"));
    }

    // Four-sided roof over a hx-by-hz footprint, apex at `height`. Base centred on the origin.
    public static Mesh Pyramid(float hx, float hz, float height) {
        string key = "pyramid" + F(hx) + "_" + F(hz) + "_" + F(height);
        if (Cached(key, out Mesh cached)) return cached;
        MeshBuilder b = new MeshBuilder();
        Func<Vector3, Vector2> uv = p => new Vector2(p.x + p.z, p.y);
        Vector3 apex = new Vector3(0f, height, 0f);
        Vector3 fl = new Vector3(-hx, 0f, -hz), fr = new Vector3(hx, 0f, -hz), bl = new Vector3(-hx, 0f, hz), br = new Vector3(hx, 0f, hz);
        b.Facet(fl, apex, fr, uv);   // front (-z)
        b.Facet(fr, apex, br, uv);   // right (+x)
        b.Facet(br, apex, bl, uv);   // back (+z)
        b.Facet(bl, apex, fl, uv);   // left (-x)
        return Store(key, b.ToMesh("Pyramid"));
    }

    // Hanging banner with a swallow-tail notch, top edge on the origin, facing -Z. UVs 0..1.
    public static Mesh Banner(float width, float height, float notch) {
        string key = "banner" + F(width) + "_" + F(height) + "_" + F(notch);
        if (Cached(key, out Mesh cached)) return cached;
        MeshBuilder b = new MeshBuilder();
        float hw = width * 0.5f;
        Func<float, float, int> v = (x, y) => b.Vertex(new Vector3(x, y, 0f), new Vector2(x / width + 0.5f, 1f + y / height));
        int tl = v(-hw, 0f), tc = v(0f, 0f), tr = v(hw, 0f);
        int bl = v(-hw, -height), n = v(0f, -height + notch), br = v(hw, -height);
        b.Quad(bl, tl, tc, n);
        b.Quad(n, tc, tr, br);
        return Store(key, b.ToMesh("Banner"));
    }

    // Low-poly sphere (an icosahedron subdivided once), flat shaded.
    public static Mesh Ball(float radius) { return MeshKit.Rock(radius, 0, 0f, 1); }
}

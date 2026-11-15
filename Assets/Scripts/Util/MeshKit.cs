using System.Collections.Generic;
using UnityEngine;

// Procedural meshes. Box faces get UVs in metres so textures tile at the same scale on every block size.
public static class MeshKit {
    static readonly Dictionary<long, Mesh> boxes = new Dictionary<long, Mesh>();
    static readonly Dictionary<string, Mesh> misc = new Dictionary<string, Mesh>();

    public static Mesh Box(Vector3 size) {
        int qx = Mathf.RoundToInt(size.x * 100f), qy = Mathf.RoundToInt(size.y * 100f), qz = Mathf.RoundToInt(size.z * 100f);
        long key = ((long)qx << 40) | ((long)qy << 20) | (long)qz;
        if (boxes.TryGetValue(key, out Mesh m) && m != null) return m;
        m = BuildBox(size);
        boxes[key] = m;
        return m;
    }

    static Mesh BuildBox(Vector3 s) {
        Vector3 h = s * 0.5f;
        Vector3[] v = new Vector3[24];
        Vector3[] n = new Vector3[24];
        Vector2[] uv = new Vector2[24];
        int[] tri = new int[36];
        // each face: normal, right axis, up axis, half sizes
        Vector3[] normals = { Vector3.back, Vector3.forward, Vector3.left, Vector3.right, Vector3.up, Vector3.down };
        Vector3[] rights = { Vector3.right, Vector3.left, Vector3.back, Vector3.forward, Vector3.right, Vector3.right };
        Vector3[] ups = { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.forward, Vector3.back };
        for (int f = 0; f < 6; f++) {
            Vector3 nn = normals[f], r = rights[f], u = ups[f];
            float hn = Mathf.Abs(Vector3.Dot(h, nn)), hr = Mathf.Abs(Vector3.Dot(h, r)), hu = Mathf.Abs(Vector3.Dot(h, u));
            Vector3 c = nn * hn;
            Vector3[] corners = { c - r * hr - u * hu, c - r * hr + u * hu, c + r * hr + u * hu, c + r * hr - u * hu };
            for (int k = 0; k < 4; k++) {
                int i = f * 4 + k;
                v[i] = corners[k];
                n[i] = nn;
                // metres along the face, offset so textures line up across neighbouring blocks loosely
                uv[i] = new Vector2(Vector3.Dot(corners[k], r) + hr, Vector3.Dot(corners[k], u) + hu);
            }
            int b = f * 4;
            tri[f * 6 + 0] = b; tri[f * 6 + 1] = b + 1; tri[f * 6 + 2] = b + 2;
            tri[f * 6 + 3] = b; tri[f * 6 + 4] = b + 2; tri[f * 6 + 5] = b + 3;
        }
        Mesh m = new Mesh { name = "Box" };
        m.vertices = v; m.normals = n; m.uv = uv; m.triangles = tri;
        m.RecalculateTangents();
        m.RecalculateBounds();
        return m;
    }

    // Low-poly rock: subdivided icosahedron with radial noise, flat shaded.
    public static Mesh Rock(float radius, int seed, float roughness = 0.22f, int subdiv = 1) {
        string key = "rock" + radius.ToString("0.00") + "_" + seed + "_" + roughness.ToString("0.00") + "_" + subdiv;
        if (misc.TryGetValue(key, out Mesh cached) && cached != null) return cached;
        List<Vector3> verts; List<int> tris;
        Icosphere(subdiv, out verts, out tris);
        for (int i = 0; i < verts.Count; i++) {
            Vector3 p = verts[i];
            float k = 1f + (TexKit.Hash(Mathf.RoundToInt(p.x * 97f), Mathf.RoundToInt(p.y * 97f + p.z * 31f), seed) - 0.5f) * 2f * roughness;
            verts[i] = p * radius * k;
        }
        Mesh m = FlatShaded(verts, tris, key);
        misc[key] = m;
        return m;
    }

    // Cone along +Y with its base centred at the origin.
    public static Mesh Cone(int sides, float radius, float height) {
        string key = "cone" + sides + "_" + radius.ToString("0.00") + "_" + height.ToString("0.00");
        if (misc.TryGetValue(key, out Mesh cached) && cached != null) return cached;
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        Vector3 tip = Vector3.up * height;
        for (int i = 0; i < sides; i++) {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
            Vector3 p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
            int b = verts.Count;
            verts.Add(p0); verts.Add(tip); verts.Add(p1);
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            b = verts.Count;
            verts.Add(p1); verts.Add(Vector3.zero); verts.Add(p0);
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
        }
        Mesh m = FlatShaded(verts, tris, key);
        misc[key] = m;
        return m;
    }

    // Triangular prism (roof wedge): base width x, height y, depth z, base centred at origin.
    public static Mesh Wedge(Vector3 size) {
        string key = "wedge" + size.ToString("F2");
        if (misc.TryGetValue(key, out Mesh cached) && cached != null) return cached;
        float hx = size.x / 2f, hz = size.z / 2f, y = size.y;
        Vector3 a = new Vector3(-hx, 0, -hz), b = new Vector3(hx, 0, -hz), c = new Vector3(0, y, -hz);
        Vector3 a2 = new Vector3(-hx, 0, hz), b2 = new Vector3(hx, 0, hz), c2 = new Vector3(0, y, hz);
        List<Vector3> v = new List<Vector3> { a, c, b, b2, c2, a2, a, a2, c2, a, c2, c, b, c, c2, b, c2, b2, a, b, b2, a, b2, a2 };
        List<int> t = new List<int>();
        for (int i = 0; i < v.Count; i++) t.Add(i);
        Mesh m = FlatShaded(v, t, key);
        misc[key] = m;
        return m;
    }

    public static Mesh FlatShaded(List<Vector3> verts, List<int> tris, string name) {
        Vector3[] v = new Vector3[tris.Count];
        Vector2[] uv = new Vector2[tris.Count];
        int[] t = new int[tris.Count];
        for (int i = 0; i < tris.Count; i++) {
            v[i] = verts[tris[i]];
            uv[i] = new Vector2(v[i].x + v[i].z, v[i].y);
            t[i] = i;
        }
        Mesh m = new Mesh { name = name };
        m.vertices = v; m.uv = uv; m.triangles = t;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    public static void Icosphere(int subdiv, out List<Vector3> verts, out List<int> tris) {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        verts = new List<Vector3> {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
        tris = new List<int> {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
        };
        for (int s = 0; s < subdiv; s++) {
            Dictionary<long, int> mid = new Dictionary<long, int>();
            List<int> nt = new List<int>();
            for (int i = 0; i < tris.Count; i += 3) {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                int ab = Mid(verts, mid, a, b), bc = Mid(verts, mid, b, c), ca = Mid(verts, mid, c, a);
                nt.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            tris = nt;
        }
    }

    static int Mid(List<Vector3> verts, Dictionary<long, int> cache, int a, int b) {
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        if (cache.TryGetValue(key, out int i)) return i;
        verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
        cache[key] = verts.Count - 1;
        return verts.Count - 1;
    }
}

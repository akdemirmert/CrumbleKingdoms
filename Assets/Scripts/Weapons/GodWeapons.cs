using System.Collections.Generic;
using UnityEngine;

// God class weapons: the Norse pantheon. Each god's weapon lives in its own file:
//   GodWeaponsLoki.cs      Loki: Trickster Daggers (one real dagger and three illusions)
//   GodWeaponsThor.cs      Thor: Mjolnir (lightning on impact, smashes through, flies back to Thor)
//   GodWeaponsHeimdall.cs  Heimdall: Gjallarhorn (rainbow shockwave that hurls blocks away)
//   GodWeaponsOdin.cs      Odin: Gungnir (homing spear that pierces everything)
static class GodWeapons {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() {
        WeaponFactory.Register(WeaponId.LokiDaggers, LokiDagger.Fire, LokiDagger.Model);
        WeaponFactory.Register(WeaponId.Mjolnir, MjolnirHammer.Fire, MjolnirHammer.Model);
        WeaponFactory.Register(WeaponId.Gjallarhorn, HeimdallHorn.Fire, HeimdallHorn.Model);
        WeaponFactory.Register(WeaponId.Gungnir, GungnirSpear.Fire, GungnirSpear.Model);
    }
}

// Colours, flight effects and small mesh builders shared by the god weapons.
static class GodKit {
    public static readonly Color LokiGreen = new Color(0.35f, 0.9f, 0.55f);
    public static readonly Color ThorBlue = new Color(0.55f, 0.78f, 1f);
    public static readonly Color Gold = new Color(1f, 0.78f, 0.3f);
    public static readonly Color[] Rainbow = {      // Bifröst, red to violet
        new Color(1f, 0.25f, 0.22f), new Color(1f, 0.6f, 0.15f), new Color(1f, 0.92f, 0.25f),
        new Color(0.3f, 0.92f, 0.38f), new Color(0.25f, 0.62f, 1f), new Color(0.62f, 0.32f, 1f),
    };

    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

    // Polished gold for guards, bands and fittings. Only half metallic: the arenas have no reflection probes,
    // and a fully metallic surface would render dark.
    public static Material GoldMetal => Mats.Lit(Gold, 0.75f, 0.5f);

    // ------------------------------------------------------------------ flight effects
    // A point light that travels with a projectile, a little towards the camera so it lights the visible side.
    public static Light Glow(Transform parent, Color color, float intensity, float range) {
        GameObject go = new GameObject("Glow");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.8f);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        return light;
    }

    // Detaches a trail or emitter so it fades out on its own instead of vanishing with its projectile
    // (or lingering on a dagger that is stuck in a wall).
    public static void Release(GameObject fx) {
        if (fx == null) return;
        fx.transform.SetParent(null, true);
        float life = 1f;
        foreach (TrailRenderer tr in fx.GetComponentsInChildren<TrailRenderer>()) {
            tr.emitting = false;
            life = Mathf.Max(life, tr.time);
        }
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Object.Destroy(fx, life + 0.1f);
    }

    // A dagger or spear whose host broke (or died) from the very hit it stopped in would hang in mid-air:
    // let it fall like loose debris instead.
    public static void DropIfFloating(Projectile p, Collider hit) {
        if (p.transform.parent != null || p.GetComponent<Rigidbody>() != null) return;   // in a live host, or already falling
        if (hit != null && hit.gameObject.layer == Layers.Ground) return;                  // stuck in the ground
        p.gameObject.AddComponent<Embedded>().Release(p.transform.right * 3f);
    }

    public static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t) {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    // ------------------------------------------------------------------ model parts
    // A cylinder (or a square bar) from a to b in the parent's space. thickness = diameter.
    public static GameObject Rod(Transform parent, Vector3 a, Vector3 b, float thickness, Material mat,
                                 PrimitiveType type = PrimitiveType.Cylinder) {
        Vector3 d = b - a;
        float length = type == PrimitiveType.Cylinder ? d.magnitude * 0.5f : d.magnitude;   // Unity's cylinder is 2 m tall
        return ModelKit.Part(parent, type, (a + b) * 0.5f, new Vector3(thickness, length, thickness), mat,
                             Quaternion.FromToRotation(Vector3.up, d).eulerAngles);
    }

    // Double-edged blade along +X with a diamond cross-section (sharp edges, a ridge down the middle).
    // xs: stations from the base to the tip; ws: half width at each station (0 at the tip).
    public static Mesh Blade(string key, float[] xs, float[] ws, float thickness) {
        if (meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        float wMax = Mathf.Max(ws);
        for (int i = 0; i + 1 < xs.Length; i++)
            for (int y = -1; y <= 1; y += 2)            // upper / lower edge
                for (int z = -1; z <= 1; z += 2) {      // front / back face
                    Vector3 e0 = new Vector3(xs[i], y * ws[i], 0f), e1 = new Vector3(xs[i + 1], y * ws[i + 1], 0f);
                    Vector3 r0 = new Vector3(xs[i], 0f, z * thickness * ws[i] / wMax);
                    Vector3 r1 = new Vector3(xs[i + 1], 0f, z * thickness * ws[i + 1] / wMax);
                    Vector3 outward = new Vector3(0f, y, z);
                    Tri(v, t, e0, e1, r1, outward);
                    Tri(v, t, e0, r1, r0, outward);
                }
        float h = thickness * ws[0] / wMax;              // flat cap at the base, facing back (-X)
        Vector3 top = new Vector3(xs[0], ws[0], 0f), bottom = new Vector3(xs[0], -ws[0], 0f);
        Tri(v, t, top, new Vector3(xs[0], 0f, -h), bottom, Vector3.left);
        Tri(v, t, top, bottom, new Vector3(xs[0], 0f, h), Vector3.left);
        Mesh m = MeshKit.FlatShaded(v, t, key);
        meshes[key] = m;
        return m;
    }

    // Smooth round tube along a path in the XY plane with a radius per path point (both ends open).
    public static Mesh Tube(string key, Vector3[] path, float[] radii, int sides) {
        if (meshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
        int n = path.Length, ring = sides + 1;
        Vector3[] verts = new Vector3[n * ring];
        Vector3[] normals = new Vector3[n * ring];
        Vector2[] uv = new Vector2[n * ring];
        for (int i = 0; i < n; i++) {
            Vector3 tangent = (path[Mathf.Min(i + 1, n - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
            Vector3 side = new Vector3(-tangent.y, tangent.x, 0f);    // in-plane normal; each ring spans it and Z
            for (int k = 0; k < ring; k++) {
                float angle = k * Mathf.PI * 2f / sides;
                Vector3 dir = side * Mathf.Cos(angle) + Vector3.forward * Mathf.Sin(angle);
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
        Mesh m = new Mesh { name = key };
        m.vertices = verts;
        m.normals = normals;
        m.uv = uv;
        m.triangles = tris;
        m.RecalculateBounds();
        meshes[key] = m;
        return m;
    }

    // Adds a triangle wound so that it faces `outward` (Unity front faces are clockwise). Skips degenerate ones.
    static void Tri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 outward) {
        Vector3 n = Vector3.Cross(b - a, c - a);
        if (n.sqrMagnitude < 1e-12f) return;
        if (Vector3.Dot(n, outward) < 0f) { Vector3 s = b; b = c; c = s; }
        t.Add(v.Count); v.Add(a);
        t.Add(v.Count); v.Add(b);
        t.Add(v.Count); v.Add(c);
    }
}

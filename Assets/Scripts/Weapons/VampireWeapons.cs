using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// The Vampire's four weapons. This file registers them with WeaponFactory and holds what they and the
// Vampire's look share (VampireKit: colours, materials, the bat model, mesh and effect helpers).
// Each weapon lives in its own file:
//   VampireWeaponsBats.cs        Bat Swarm: a flapping cloud that releases ten diving, biting bats
//   VampireWeaponsFang.cs        Vampire Fang: a piercing fang that drains a bitten commander's blood
//   VampireWeaponsCandelabra.cs  Burning Candelabra: three fire blasts in a fan that set wood alight
//   VampireWeaponsCloak.cs       Blood Cloak: a bat-wing cloak over the chamber that blocks every hit
//   VampireWeaponsSounds.cs      the class's own synthesised sounds
static class VampireWeapons {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() {
        WeaponFactory.Register(WeaponId.BatSwarm, VampireBatSwarm.Fire, VampireBatSwarm.Model);
        WeaponFactory.Register(WeaponId.VampireFang, VampireFang.Fire, VampireFang.Model);
        WeaponFactory.Register(WeaponId.Candelabra, VampireCandelabra.Fire, VampireCandelabra.Model);
        WeaponFactory.Register(WeaponId.BloodCloak, VampireCloakFlight.Fire, VampireCloakFlight.Model);
    }
}

// Colours, materials, the bat and small mesh/effect builders shared by the Vampire's weapons and decorations.
static class VampireKit {
    public static readonly Color Crimson = WeaponDefs.Hex("B3203D");              // the class colour
    public static readonly Color BloodRed = new Color(0.5f, 0.02f, 0.06f);         // dark blood
    public static readonly Color BloodGlow = new Color(1f, 0.14f, 0.22f);           // glowing red: eyes, sparks, trails
    public static readonly Color Ivory = new Color(0.95f, 0.91f, 0.8f);
    public static readonly Color Gold = new Color(1f, 0.78f, 0.32f);
    public static readonly Color Wax = new Color(0.96f, 0.92f, 0.82f);
    public static readonly Color Flame = new Color(1f, 0.58f, 0.18f);
    public static readonly Color Smoke = new Color(0.1f, 0.03f, 0.07f, 0.65f);    // the swarm's dark haze
    static readonly Color Fur = new Color(0.09f, 0.07f, 0.09f);
    static readonly Color Membrane = new Color(0.17f, 0.08f, 0.12f);

    static readonly RaycastHit[] sweepBuffer = new RaycastHit[16];
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

    public static Material FurMat => Mats.Lit(Fur, 0.3f);
    public static Material MembraneMat => Mats.Lit(Membrane, 0.35f);
    public static Material EyeMat => Mats.Emissive(new Color(0.9f, 0.1f, 0.12f), new Color(4f, 0.3f, 0.35f));
    public static Material GoldMetal => Mats.Lit(Gold, 0.72f, 0.5f);   // half metallic: the arenas have no reflection probes
    public static Material WaxMat => Mats.Lit(Wax, 0.45f);
    public static Material FlameMat => Mats.Emissive(new Color(1f, 0.85f, 0.45f), new Color(5f, 2.6f, 0.8f));
    public static Material IronMat => Mats.Lit(new Color(0.07f, 0.07f, 0.08f), 0.5f, 0.55f);
    public static Material GlowRed => Mats.Emissive(new Color(0.6f, 0.03f, 0.07f), new Color(3.4f, 0.25f, 0.32f));

    // ------------------------------------------------------------------ the bat
    // A bat seen from the front (it faces the camera, -Z): body, head, pointed ears and two glowing red eyes in one
    // mesh, and two scalloped wings hinged at the shoulders that flap in the screen plane (VampireBatRig).
    // About one metre from wing tip to wing tip at size 1.
    public static VampireBatRig Bat(Transform parent, float size) {
        GameObject root = ModelKit.Root("Bat", parent);
        root.transform.localScale = Vector3.one * size;
        GameObject body = ModelKit.MeshPart(root.transform, BatBody(), Vector3.zero, Vector3.one, FurMat, default, "Body");
        body.GetComponent<MeshRenderer>().sharedMaterials = new[] { FurMat, EyeMat };
        VampireBatRig rig = root.AddComponent<VampireBatRig>();
        rig.wingR = BatWing(root.transform, 1f);
        rig.wingL = BatWing(root.transform, -1f);
        return rig;
    }

    // A bat on its own in the world that flies off with `velocity` and is gone after `life` seconds.
    public static VampireBatRig FreeBat(Vector3 position, float size, Vector3 velocity, float life) {
        VampireBatRig bat = Bat(null, size);
        bat.transform.position = position;
        VampireFlyAway.Launch(bat.transform, velocity, life);
        return bat;
    }

    static Transform BatWing(Transform root, float side) {
        Transform pivot = ModelKit.Root("Wing", root).transform;
        pivot.localPosition = new Vector3(side * 0.05f, 0.05f, 0.01f);
        pivot.localScale = new Vector3(side, 1f, 1f);   // the left wing is the right one mirrored
        ModelKit.MeshPart(pivot, WingMesh(), Vector3.zero, Vector3.one, MembraneMat, default, "Membrane");
        return pivot;
    }

    static Mesh BatBody() {
        return Cached("batBody", () => Merge("Bat Body",
            new Piece(WorldMeshes.Ball(0.1f), new Vector3(0f, -0.01f, 0f), Vector3.zero, new Vector3(1f, 1.4f, 0.85f)),
            new Piece(WorldMeshes.Ball(0.075f), new Vector3(0f, 0.13f, -0.01f), Vector3.zero, Vector3.one),
            new Piece(MeshKit.Cone(4, 0.032f, 0.09f), new Vector3(0.04f, 0.18f, 0f), new Vector3(0f, 0f, -16f), Vector3.one),
            new Piece(MeshKit.Cone(4, 0.032f, 0.09f), new Vector3(-0.04f, 0.18f, 0f), new Vector3(0f, 0f, 16f), Vector3.one),
            new Piece(WorldMeshes.Ball(0.017f), new Vector3(0.028f, 0.14f, -0.074f), Vector3.zero, Vector3.one, 1),
            new Piece(WorldMeshes.Ball(0.017f), new Vector3(-0.028f, 0.14f, -0.074f), Vector3.zero, Vector3.one, 1)));
    }

    // The right wing (+X from the shoulder): a leading edge arching up to the wrist and out to the tip, and a
    // trailing edge scalloped between the finger tips. The outline runs clockwise as the camera sees it, so a fan
    // from a point inside faces -Z; every triangle is doubled for the back side.
    static Mesh WingMesh() {
        return Cached("batWing", () => {
            List<Vector2> outline = new List<Vector2> {
                new Vector2(0f, 0.02f), new Vector2(0.08f, 0.1f), new Vector2(0.18f, 0.15f),
                new Vector2(0.3f, 0.16f), new Vector2(0.42f, 0.11f), new Vector2(0.52f, 0.03f),
            };
            Vector2 wrist = new Vector2(0.3f, 0.16f);
            Vector2[] tips = { new Vector2(0.52f, 0.03f), new Vector2(0.43f, -0.09f), new Vector2(0.28f, -0.13f), new Vector2(0.03f, -0.1f) };
            for (int k = 0; k + 1 < tips.Length; k++) {
                Vector2 a = tips[k], b = tips[k + 1];
                Vector2 bulge = (wrist - (a + b) * 0.5f).normalized * Vector2.Distance(a, b) * 0.35f;   // scallops curve towards the wrist
                for (int s = 1; s <= 4; s++) outline.Add(Vector2.Lerp(a, b, s / 4f) + bulge * Mathf.Sin(Mathf.PI * s / 4f));
            }
            List<Vector3> v = new List<Vector3>();
            List<int> t = new List<int>();
            Vector3 c = new Vector3(0.26f, 0.03f, 0f);
            for (int i = 0; i < outline.Count; i++) {
                Vector3 p = outline[i], q = outline[(i + 1) % outline.Count];
                Facet(v, t, c, p, q);   // front
                Facet(v, t, c, q, p);   // back
            }
            return ToMesh("Bat Wing", v, t);
        });
    }

    static void Facet(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c) {
        t.Add(v.Count); v.Add(a);
        t.Add(v.Count); v.Add(b);
        t.Add(v.Count); v.Add(c);
    }

    // ------------------------------------------------------------------ flight helpers
    // A point light that travels with a projectile, a little towards the camera so it lights the visible side.
    public static Light Glow(Transform parent, Color color, float intensity, float range) {
        GameObject go = new GameObject("Glow");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.8f);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    // Drops falling off a moving object (wax from candles, blood from the fang). World space, so they stay behind.
    public static ParticleSystem Drips(Transform parent, Color color, float rate, float size) {
        GameObject go = new GameObject("Drips");
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // it starts playing on creation
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        main.startColor = color;
        main.gravityModifier = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(fade);
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Mats.ParticleAlpha(TexKit.SoftCircle);
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return ps;
    }

    // Lets a particle effect outlive its projectile: detach it, stop emitting, remove it once faded.
    // Call before the projectile is destroyed (the hierarchy cannot change during destruction).
    public static void Release(Component fx, float life) {
        if (fx == null) return;
        fx.transform.SetParent(null, true);
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Object.Destroy(fx.gameObject, life);
    }

    // The nearest solid thing a sphere moving by `step` would touch, ignoring the projectile's own team.
    public static bool Sweep(Projectile self, Vector3 from, float radius, Vector3 step, out RaycastHit hit) {
        hit = default;
        float len = step.magnitude;
        if (len < 1e-5f) return false;
        int n = Physics.SphereCastNonAlloc(from, radius, step / len, sweepBuffer, len, Layers.SolidMask, QueryTriggerInteraction.Ignore);
        bool found = false;
        float best = float.MaxValue;
        for (int i = 0; i < n; i++) {
            RaycastHit h = sweepBuffer[i];
            if (h.collider == null || h.distance >= best || self.IsOwnTeam(h.collider)) continue;
            if (h.distance <= 0f && h.point == Vector3.zero) h.point = from;   // it started inside the collider
            best = h.distance;
            hit = h;
            found = true;
        }
        return found;
    }

    public static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t) {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    // ------------------------------------------------------------------ mesh helpers
    // One part of a merged mesh: a mesh placed with a transform, drawn with material slot `submesh`.
    public readonly struct Piece {
        public readonly Mesh mesh;
        public readonly Matrix4x4 matrix;
        public readonly int submesh;

        public Piece(Mesh mesh, Vector3 position, Vector3 euler, Vector3 scale, int submesh = 0)
            : this(mesh, position, Quaternion.Euler(euler), scale, submesh) { }

        public Piece(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, int submesh = 0) {
            this.mesh = mesh;
            matrix = Matrix4x4.TRS(position, rotation, scale);
            this.submesh = submesh;
        }
    }

    // Several static parts as one mesh (one renderer instead of many; submeshes for different materials).
    public static Mesh Merge(string name, params Piece[] pieces) {
        List<Vector3> v = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        int subs = 1;
        foreach (Piece p in pieces) subs = Mathf.Max(subs, p.submesh + 1);
        List<int>[] tris = new List<int>[subs];
        for (int s = 0; s < subs; s++) tris[s] = new List<int>();
        foreach (Piece p in pieces) {
            Vector3[] pv = p.mesh.vertices;
            Vector2[] puv = p.mesh.uv;
            int[] pt = p.mesh.triangles;
            int b = v.Count;
            for (int i = 0; i < pv.Length; i++) {
                v.Add(p.matrix.MultiplyPoint3x4(pv[i]));
                uv.Add(i < puv.Length ? puv[i] : Vector2.zero);
            }
            bool flip = p.matrix.determinant < 0f;   // a mirrored part would turn inside out
            for (int i = 0; i + 2 < pt.Length; i += 3) {
                tris[p.submesh].Add(b + pt[i]);
                tris[p.submesh].Add(b + pt[flip ? i + 2 : i + 1]);
                tris[p.submesh].Add(b + pt[flip ? i + 1 : i + 2]);
            }
        }
        Mesh m = new Mesh { name = name };
        m.SetVertices(v);
        m.SetUVs(0, uv);
        m.subMeshCount = subs;
        for (int s = 0; s < subs; s++) m.SetTriangles(tris[s], s);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // A round tube along a 3D path (radius per point) appended to v/t, its triangles facing outwards. The ring
    // frame is carried along the path (parallel transport) so the tube never twists.
    public static void AddTube(List<Vector3> v, List<int> t, Vector3[] path, float[] radii, int sides) {
        int n = path.Length, start = v.Count;
        Vector3 normal = Vector3.zero;
        for (int i = 0; i < n; i++) {
            Vector3 tangent = (path[Mathf.Min(i + 1, n - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
            Vector3 carried = normal - Vector3.Dot(normal, tangent) * tangent;
            if (i == 0 || carried.sqrMagnitude < 1e-6f)
                carried = Vector3.Cross(tangent, Mathf.Abs(tangent.y) < 0.9f ? Vector3.up : Vector3.right);
            normal = carried.normalized;
            Vector3 binormal = Vector3.Cross(tangent, normal);
            for (int k = 0; k < sides; k++) {
                float a = k * Mathf.PI * 2f / sides;
                v.Add(path[i] + (normal * Mathf.Cos(a) + binormal * Mathf.Sin(a)) * radii[i]);
            }
        }
        for (int i = 0; i + 1 < n; i++) {
            Vector3 centre = (path[i] + path[i + 1]) * 0.5f;
            for (int k = 0; k < sides; k++) {
                int a = start + i * sides + k, b = start + i * sides + (k + 1) % sides;
                OutwardTri(v, t, a, b, a + sides, centre);
                OutwardTri(v, t, b, b + sides, a + sides, centre);
            }
        }
    }

    // Adds triangle a-b-c wound so that it faces away from `centre` (Unity front faces are clockwise).
    public static void OutwardTri(List<Vector3> v, List<int> t, int a, int b, int c, Vector3 centre) {
        Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (Vector3.Dot(n, (v[a] + v[b] + v[c]) / 3f - centre) < 0f) { int s = b; b = c; c = s; }
        t.Add(a);
        t.Add(b);
        t.Add(c);
    }

    public static Mesh ToMesh(string name, List<Vector3> v, List<int> t) {
        Mesh m = new Mesh { name = name };
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // Builds a mesh once and keeps it (meshes are shared by every model that uses them).
    public static Mesh Cached(string key, System.Func<Mesh> build) {
        if (meshes.TryGetValue(key, out Mesh m) && m != null) return m;
        m = build();
        meshes[key] = m;
        return m;
    }
}

// Flaps a bat's wings up and down in the screen plane.
public class VampireBatRig : MonoBehaviour {
    public Transform wingL, wingR;
    public float flapRate = 38f;       // radians per second: about six wing beats a second
    public float amplitude = 50f;      // degrees either side of `lift`
    public float lift = 8f;
    float phase;

    void Awake() { phase = Random.value * Mathf.PI * 2f; }

    void Update() {
        phase += flapRate * Time.deltaTime;
        Pose(lift + amplitude * Mathf.Sin(phase));
    }

    // Both wings at `degrees` above the horizontal (negative = below).
    public void Pose(float degrees) {
        if (wingR != null) wingR.localRotation = Quaternion.Euler(0f, 0f, degrees);
        if (wingL != null) wingL.localRotation = Quaternion.Euler(0f, 0f, -degrees);
    }
}

// A bat that flies off, climbing, and shrinks away: bats leaving a torn cloak or a fallen tower.
public class VampireFlyAway : MonoBehaviour {
    Vector3 velocity, baseScale;
    float life, age;

    public static void Launch(Transform bat, Vector3 velocity, float life) {
        bat.SetParent(null, true);
        VampireFlyAway f = bat.gameObject.AddComponent<VampireFlyAway>();
        f.velocity = velocity;
        f.life = Mathf.Max(0.2f, life);
        f.baseScale = bat.localScale;
        VampireBatRig rig = bat.GetComponent<VampireBatRig>();
        if (rig != null) rig.flapRate *= 1.4f;   // panicked
    }

    void Update() {
        float dt = Time.deltaTime;
        age += dt;
        velocity += Vector3.up * 3f * dt;
        transform.position += velocity * dt;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(-velocity.x * 6f, -40f, 40f));   // lean into the flight
        transform.localScale = baseScale * Mathf.Clamp01((life - age) / 0.4f);
        if (age >= life) Destroy(gameObject);
    }
}

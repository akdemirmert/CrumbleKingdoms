using UnityEngine;
using UnityEngine.Rendering;

// Places in the galaxy that exist only for one menu page, so each page feels like somewhere of its own:
//   Nebula region (Choose your class)  - drifting violet / magenta gas clouds with sparkles
//   Observatory (How to Play)          - a banded gas giant with a dusty ring, a moon and a slow asteroid belt
//   Station (Settings)                 - a small space station with a spinning ring, a nodding dish, solar wings
//                                        and blinking lights, in front of a slowly turning spiral galaxy
// Everything is generated from primitives and procedural textures (no imported models).
// SpaceBackdrop builds them one per frame and flies the camera to them (SpaceBackdrop.ShowNebula etc).
public static class SpaceScenery {
    public static readonly Vector3 NebulaPos = new Vector3(-120f, 420f, 500f);
    public static readonly Vector3 ObservatoryPos = new Vector3(-300f, -450f, 400f);
    public static readonly Vector3 StationPos = new Vector3(300f, -450f, 400f);

    // ------------------------------------------------------------------ helpers
    static Transform Prim(PrimitiveType type, string name, Transform parent, Material mat, Vector3 localPos, Vector3 scale) {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        MeshRenderer r = g.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return g.transform;
    }

    static Transform Root(Transform parent, string name, Vector3 position) {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = position;
        return t;
    }

    static Transform Billboard(Transform parent, string name, Material mat, Vector3 localPos, float size) {
        Transform q = Prim(PrimitiveType.Quad, name, parent, mat, localPos, new Vector3(size, size, 1f));
        q.gameObject.AddComponent<SpaceBillboard>();
        return q;
    }

    static float Rand(System.Random rng) { return (float)(rng.NextDouble() * 2.0 - 1.0); }

    static Texture2D NebulaTex(int seed) {
        return SpaceNoise.Make("Nebula " + seed, 256, 256, (u, v) => {
            float dx = u - 0.5f, dy = v - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float mask = Mathf.Pow(Mathf.Clamp01(1f - r), 1.4f);
            float n = SpaceNoise.Fbm(u, v, 4, 5, seed);
            float core = SpaceNoise.Fbm(u, v, 8, 3, seed + 9);
            float a = SpaceNoise.Smooth(0.36f, 0.74f, n) * mask * (0.6f + 0.4f * core);
            return new Color(1f, 1f, 1f, a * 0.9f);
        }, false);
    }

    // ------------------------------------------------------------------ nebula region (class page)
    public static void BuildNebula(Transform parent) {
        Transform root = Root(parent, "Nebula region", NebulaPos);
        System.Random rng = new System.Random(901);
        Color[] tints = {
            new Color(0.55f, 0.25f, 1f), new Color(0.95f, 0.3f, 0.7f), new Color(0.25f, 0.5f, 1f),
            new Color(0.2f, 0.85f, 0.85f), new Color(0.7f, 0.4f, 1f),
        };
        Texture2D[] texs = { NebulaTex(950), NebulaTex(963), NebulaTex(976) };
        for (int i = 0; i < 12; i++) {
            Material m = new Material(Mats.ParticleAdditive(texs[i % 3])) { name = "Nebula cloud" };
            m.SetColor("_BaseColor", tints[i % tints.Length] * Mathf.Lerp(0.5f, 0.9f, (float)rng.NextDouble()));
            Billboard(root, "Cloud " + i, m, new Vector3(Rand(rng) * 90f, Rand(rng) * 45f, Rand(rng) * 70f), 90f + (float)rng.NextDouble() * 110f);
        }

        // sparkles hanging in the gas
        GameObject go = new GameObject("Sparkles");
        go.transform.SetParent(root, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.duration = 6f;
        main.loop = true;
        main.prewarm = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        main.startColor = new Color(0.92f, 0.88f, 1f, 0.9f);
        main.maxParticles = 400;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 55f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(200f, 110f, 160f);
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mats.ParticleAdditive(TexKit.Star);
        ps.Play();
    }

    // ------------------------------------------------------------------ observatory (how to play)
    public static void BuildObservatory(Transform parent) {
        Transform root = Root(parent, "Observatory", ObservatoryPos);
        const float R = 22f;

        // banded gas giant with a great storm
        Texture2D bands = SpaceNoise.Make("GasGiant", 512, 256, (u, v) => {
            float warp = SpaceNoise.Fbm(u, v, 3, 3, 71) - 0.5f;
            float band = Mathf.Sin((v * 14f + warp * 3.5f) * Mathf.PI) * 0.5f + 0.5f;
            float detail = SpaceNoise.Fbm(u, v, 14, 3, 72);
            Color col = Color.Lerp(new Color(0.95f, 0.72f, 0.45f), new Color(0.98f, 0.9f, 0.75f), band);
            col = Color.Lerp(col, new Color(0.72f, 0.42f, 0.3f), SpaceNoise.Smooth(0.55f, 0.75f, detail) * 0.6f);
            float dx = (u - 0.3f) * 2f, dy = (v - 0.38f) * 4f;
            float storm = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) * 3f);
            col = Color.Lerp(col, new Color(0.85f, 0.3f, 0.25f), storm * 0.8f);
            col.a = 1f;
            return col;
        });
        Material giantMat = new Material(Mats.LitTex(bands, Color.white, 0.1f)) { name = "Gas giant" };
        Transform giant = Prim(PrimitiveType.Sphere, "Gas giant", root, giantMat, Vector3.zero, Vector3.one * (R * 2f));
        giant.GetComponent<MeshFilter>().sharedMesh = SpacePlanet.SphereMesh;
        giant.gameObject.AddComponent<SpaceSpinner>().axisSpeed = new Vector3(0f, 2.2f, 0f);

        // dusty ring: stripes with a gap, soft edges (quad half-size 70 -> ring from 30 to 66)
        Texture2D ringTex = SpaceNoise.Make("GiantRing", 256, 256, (u, v) => {
            float dx = u - 0.5f, dy = v - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float t = (r - 0.43f) / (0.94f - 0.43f);
            if (t < 0f || t > 1f) return new Color(1f, 1f, 1f, 0f);
            float stripes = SpaceNoise.Fbm(r, 0.3f, 8, 4, 90);
            float a = Mathf.Sin(t * Mathf.PI) * Mathf.Lerp(0.35f, 1f, stripes);
            if (t > 0.58f && t < 0.66f) a *= 0.12f;   // a gap in the ring
            Color c = Color.Lerp(new Color(0.78f, 0.66f, 0.5f), new Color(0.98f, 0.9f, 0.76f), stripes);
            c.a = a * 0.9f;
            return c;
        }, false);
        Material ringMat = new Material(Mats.ParticleAlpha(ringTex)) { name = "Giant ring" };
        Transform ring = Prim(PrimitiveType.Quad, "Ring", root, ringMat, Vector3.zero, new Vector3(140f, 140f, 1f));
        ring.localRotation = Quaternion.Euler(74f, 0f, -10f);

        // a moon on its own orbit
        Transform moonPivot = new GameObject("Moon orbit").transform;
        moonPivot.SetParent(root, false);
        moonPivot.localRotation = Quaternion.Euler(10f, 0f, 6f);
        moonPivot.gameObject.AddComponent<SpaceSpinner>().axisSpeed = new Vector3(0f, 6f, 0f);
        Prim(PrimitiveType.Sphere, "Moon", moonPivot, Mats.Lit(new Color(0.8f, 0.78f, 0.74f), 0.1f), new Vector3(52f, 0f, 0f), Vector3.one * 7f);

        // slow asteroid belt in front of the giant: the foreground that makes the camera feel close
        Transform belt = new GameObject("Asteroid belt").transform;
        belt.SetParent(root, false);
        belt.gameObject.AddComponent<SpaceSpinner>().axisSpeed = new Vector3(0f, 1.1f, 0f);
        Material[] rocks = {
            Mats.Lit(new Color(0.42f, 0.37f, 0.33f), 0.1f), Mats.Lit(new Color(0.55f, 0.48f, 0.42f), 0.1f), Mats.Lit(new Color(0.3f, 0.28f, 0.28f), 0.1f),
        };
        System.Random rng = new System.Random(77);
        for (int i = 0; i < 90; i++) {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float rad = Mathf.Lerp(80f, 115f, (float)rng.NextDouble());
            Vector3 pos = new Vector3(Mathf.Cos(a) * rad, Rand(rng) * 8f, Mathf.Sin(a) * rad);
            float s = Mathf.Lerp(0.6f, 2.8f, Mathf.Pow((float)rng.NextDouble(), 2f));
            Transform rock = Prim(PrimitiveType.Sphere, "Rock", belt, rocks[i % rocks.Length], pos,
                                  new Vector3(s, s * Mathf.Lerp(0.6f, 1f, (float)rng.NextDouble()), s * Mathf.Lerp(0.7f, 1.2f, (float)rng.NextDouble())));
            rock.rotation = Random.rotation;
            rock.gameObject.AddComponent<SpaceSpinner>().axisSpeed = new Vector3(Rand(rng), Rand(rng), Rand(rng)) * 14f;
        }
    }

    // ------------------------------------------------------------------ station (settings)
    public static void BuildStation(Transform parent) {
        Transform root = Root(parent, "Station", StationPos);
        Material hull = Mats.Lit(new Color(0.78f, 0.82f, 0.9f), 0.6f, 0.7f);
        Material dark = Mats.Lit(new Color(0.25f, 0.28f, 0.35f), 0.5f, 0.6f);
        Texture2D solar = SpaceNoise.Make("StationSolar", 128, 64, (u, v) => {
            float gx = Mathf.Abs(Mathf.Repeat(u * 8f, 1f) - 0.5f), gy = Mathf.Abs(Mathf.Repeat(v * 4f, 1f) - 0.5f);
            bool line = gx > 0.46f || gy > 0.46f;
            Color c = Color.Lerp(new Color(0.05f, 0.12f, 0.34f), new Color(0.14f, 0.3f, 0.62f), v);
            return line ? new Color(0.55f, 0.65f, 0.85f, 1f) : c;
        }, false);
        Material panel = new Material(Mats.LitTex(solar, Color.white, 0.8f)) { name = "Solar panel" };
        Material lightMat = Mats.Emissive(new Color(0.05f, 0.1f, 0.12f), new Color(0.3f, 1.4f, 1.6f), 0.3f);

        Transform body = new GameObject("Body").transform;
        body.SetParent(root, false);
        body.localRotation = Quaternion.Euler(18f, 0f, -12f);

        Prim(PrimitiveType.Cylinder, "Hub", body, hull, Vector3.zero, new Vector3(3.4f, 4f, 3.4f));      // 8 m tall, axis = Y
        Prim(PrimitiveType.Sphere, "Hub cap", body, dark, new Vector3(0f, 4f, 0f), Vector3.one * 3.4f);

        // spinning habitat ring with four spokes
        Transform ring = new GameObject("Ring").transform;
        ring.SetParent(body, false);
        ring.gameObject.AddComponent<SpaceSpinner>().axisSpeed = new Vector3(0f, 14f, 0f);
        const int Segments = 20;
        for (int i = 0; i < Segments; i++) {
            float a = i * Mathf.PI * 2f / Segments;
            Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            Transform seg = Prim(PrimitiveType.Cube, "Segment", ring, i % 5 == 0 ? dark : hull, radial * 9f, new Vector3(1.4f, 1.2f, 3.1f));
            seg.localRotation = Quaternion.LookRotation(tangent);
        }
        for (int i = 0; i < 4; i++) {
            float a = i * Mathf.PI * 0.5f;
            Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Transform spoke = Prim(PrimitiveType.Cube, "Spoke", ring, dark, radial * 4.5f, new Vector3(0.5f, 0.5f, 9f));
            spoke.localRotation = Quaternion.LookRotation(radial);
        }
        for (int i = 0; i < 4; i++) {
            float a = (i + 0.5f) * Mathf.PI * 0.5f;
            Material m = new Material(lightMat) { name = "Station light" };
            Transform light = Prim(PrimitiveType.Sphere, "Light", ring, m, new Vector3(Mathf.Cos(a), 0.9f, Mathf.Sin(a)) * 9f, Vector3.one * 0.7f);
            SpaceBlinker b = light.gameObject.AddComponent<SpaceBlinker>();
            b.mat = m;
            b.color = new Color(0.3f, 1.4f, 1.6f);
            b.speed = 2.2f + i * 0.5f;
            b.phase = i * 1.3f;
        }

        // two solar wings
        Prim(PrimitiveType.Cube, "Wing L", body, panel, new Vector3(-12f, 5.2f, 0f), new Vector3(14f, 0.15f, 5f));
        Prim(PrimitiveType.Cube, "Wing R", body, panel, new Vector3(12f, 5.2f, 0f), new Vector3(14f, 0.15f, 5f));
        Prim(PrimitiveType.Cube, "Wing arm", body, dark, new Vector3(0f, 5.2f, 0f), new Vector3(8f, 0.3f, 0.3f));

        // dish: turns around and nods
        Transform dishYaw = new GameObject("Dish yaw").transform;
        dishYaw.SetParent(body, false);
        dishYaw.localPosition = new Vector3(0f, -4.4f, 0f);
        dishYaw.gameObject.AddComponent<SpaceSpinner>().axisSpeed = new Vector3(0f, 25f, 0f);
        Prim(PrimitiveType.Cylinder, "Stem", dishYaw, dark, new Vector3(0f, -1.2f, 0f), new Vector3(0.4f, 1.2f, 0.4f));
        Transform dishTilt = new GameObject("Dish tilt").transform;
        dishTilt.SetParent(dishYaw, false);
        dishTilt.localPosition = new Vector3(0f, -2.6f, 0f);
        dishTilt.gameObject.AddComponent<SpaceNod>();
        Prim(PrimitiveType.Sphere, "Dish", dishTilt, hull, new Vector3(0f, -0.9f, 0f), new Vector3(5.2f, 1.2f, 5.2f));
        Prim(PrimitiveType.Cylinder, "Feed", dishTilt, dark, new Vector3(0f, -2.2f, 0f), new Vector3(0.15f, 0.9f, 0.15f));

        // a spiral galaxy turning slowly far behind the station
        Texture2D galaxy = SpaceNoise.Make("Galaxy", 512, 512, (u, v) => {
            float dx = u - 0.5f, dy = v - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) * 2f, th = Mathf.Atan2(dy, dx);
            float arm = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(2f * (th - r * 5.5f)), 2.2f);
            float dust = 0.7f + 0.6f * SpaceNoise.Fbm(u, v, 12, 4, 60);
            float inten = (Mathf.Exp(-r * 3.2f) * 1.2f + arm * Mathf.Exp(-r * 2.4f) * 0.9f) * dust;
            Color c = Color.Lerp(new Color(1f, 0.85f, 0.6f), new Color(0.6f, 0.75f, 1f), SpaceNoise.Smooth(0.05f, 0.5f, r));
            c.a = Mathf.Clamp01(inten) * (1f - SpaceNoise.Smooth(0.85f, 1f, r));
            return c;
        }, false);
        Material galaxyMat = new Material(Mats.ParticleAdditive(galaxy)) { name = "Galaxy" };
        galaxyMat.SetColor("_BaseColor", new Color(0.9f, 0.9f, 1f, 1f));
        Transform gal = Prim(PrimitiveType.Quad, "Galaxy", parent, galaxyMat, Vector3.zero, new Vector3(1300f, 1300f, 1f));
        gal.position = StationPos + new Vector3(-150f, 120f, 1100f);
        gal.localRotation = Quaternion.Euler(0f, 0f, 20f);
        gal.gameObject.AddComponent<SpaceSpinner>().axisSpeed = new Vector3(0f, 0f, 1.5f);
    }
}

// Faces the camera (nebula clouds).
public class SpaceBillboard : MonoBehaviour {
    void LateUpdate() {
        Camera c = Camera.main;
        if (c == null) return;
        Vector3 d = transform.position - c.transform.position;
        if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d, c.transform.up);
    }
}

// Constant rotation (planets, belts, rocks, station ring, galaxy). Unscaled time: menus ignore slow motion.
public class SpaceSpinner : MonoBehaviour {
    public Vector3 axisSpeed;   // degrees per second around each local axis
    void Update() { transform.Rotate(axisSpeed * Time.unscaledDeltaTime, Space.Self); }
}

// The station's dish nods up and down.
public class SpaceNod : MonoBehaviour {
    void Update() { transform.localRotation = Quaternion.Euler(Mathf.Sin(Time.unscaledTime * 0.6f) * 22f, 0f, 0f); }
}

// Pulsing emissive light.
public class SpaceBlinker : MonoBehaviour {
    public Material mat;
    public Color color;
    public float speed = 2f, phase;
    void Update() {
        if (mat == null) return;
        float k = Mathf.SmoothStep(0.08f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed + phase));
        mat.SetColor("_EmissionColor", color * k);
    }
}

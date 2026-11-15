using UnityEngine;

// The Alien's arsenal: strange tech from the stars. This file registers the four weapons and holds what they
// share (AlienKit): colours, small helpers and the models (visual only, +X = forward, about a metre across;
// they are also rendered as the weapon icons). Each weapon lives in its own file:
//   AlienWeaponsUfo.cs     Flying Saucer: hovers over the target and beams blocks (and exposed commanders) up
//   AlienWeaponsRayGun.cs  Ray Gun: a straight laser bolt that burns through up to five blocks
//   AlienWeaponsCow.cs     Flying Cow: a heavy, bouncy cow that a small saucer collects afterwards
//   AlienWeaponsWarp.cs    Space Warp: two rounds in space (low gravity, slowed enemy throws, a starry sky)
//   AlienWeaponsSound.cs   the class's own synthesised sounds (warble, beam hum, pew, moo, warp)
static class AlienWeapons {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() {
        WeaponFactory.Register(WeaponId.Ufo, AlienSaucer.Fire, AlienKit.UfoModel);
        WeaponFactory.Register(WeaponId.RayGun, AlienRayBolt.Fire, AlienKit.RayGunModel);
        WeaponFactory.Register(WeaponId.Cow, AlienCow.Fire, AlienKit.CowModel);
        WeaponFactory.Register(WeaponId.SpaceWarp, AlienSpaceWarp.Fire, AlienKit.WarpModel);
    }
}

// Colours, helpers and models shared by the alien weapons (and by the alien fortress decor).
static class AlienKit {
    public static readonly Color Green = WeaponDefs.Hex("39D98A");      // the class colour
    public static readonly Color Beam = new Color(0.45f, 1f, 0.65f);    // tractor beams, saucer lights
    public static readonly Color Laser = WeaponDefs.Hex("39FF88");
    public static readonly Color Warp = new Color(0.55f, 0.45f, 1f);
    public static readonly Color Amber = new Color(1f, 0.82f, 0.35f);
    static readonly Color Silver = new Color(0.8f, 0.83f, 0.88f), Gunmetal = new Color(0.27f, 0.29f, 0.34f);
    static readonly Color AlienSkin = new Color(0.42f, 0.88f, 0.55f), Ink = new Color(0.02f, 0.02f, 0.03f);

    // Only partly metallic: the arenas have no reflection probes, and fully metallic surfaces render dark.
    public static Material SilverMetal => Mats.Lit(Silver, 0.72f, 0.35f);
    public static Material DarkMetal => Mats.Lit(Gunmetal, 0.55f, 0.4f);
    public static Material Glass => Mats.Transparent(new Color(0.62f, 0.95f, 1f, 0.3f), 0.95f);
    public static Material Glowing(Color c, float hdr) { return Mats.Emissive(c * 0.7f, c * hdr); }

    // A point light travelling with a projectile, a little towards the camera so it lights the visible side.
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

    // A cylinder from a to b in the parent's space (thickness = diameter).
    public static GameObject Rod(Transform parent, Vector3 a, Vector3 b, float thickness, Material mat) {
        Vector3 d = b - a;
        return ModelKit.Part(parent, PrimitiveType.Cylinder, (a + b) * 0.5f, new Vector3(thickness, d.magnitude * 0.5f, thickness), mat,
                             Quaternion.FromToRotation(Vector3.up, d).eulerAngles);   // Unity's cylinder is 2 m tall
    }

    static GameObject Part(Transform parent, Mesh mesh, Vector3 pos, Material mat, Vector3 euler = default, Vector3? scale = null) {
        return ModelKit.MeshPart(parent, mesh, pos, scale ?? Vector3.one, mat, euler);
    }

    // ------------------------------------------------------------------ the flying saucer
    public static GameObject UfoModel(Transform parent) { return Saucer(parent, true, false, out _, out _); }

    // A silver disc with a ring of green and amber lights, a glass dome (with a tiny pilot) and a glowing tractor
    // emitter underneath; landing legs for a saucer docked on a tower. `hull` carries the rim lights and may spin
    // around the vertical axis. Origin = the middle of the rim; 1.25 m across, the legs' feet 0.3 m out from the
    // middle and 0.37 m below.
    public static GameObject Saucer(Transform parent, bool pilot, bool legs, out Transform hull, out Transform emitter) {
        GameObject root = ModelKit.Root("Saucer", parent);
        Transform r = root.transform;
        Material silver = SilverMetal;
        hull = ModelKit.Root("Hull", r).transform;
        Part(hull, WorldMeshes.Frustum(18, 0.24f, 0.56f, 0.12f), new Vector3(0f, -0.12f, 0f), silver);   // belly
        Part(hull, WorldMeshes.Frustum(24, 0.61f, 0.61f, 0.04f), new Vector3(0f, -0.02f, 0f), DarkMetal);  // rim band
        Part(hull, WorldMeshes.Frustum(18, 0.56f, 0.3f, 0.09f), new Vector3(0f, 0.02f, 0f), silver);      // upper deck
        Material green = Glowing(Beam, 3f), amber = Glowing(Amber, 3f);
        for (int i = 0; i < 10; i++) {
            float a = i * Mathf.PI * 0.2f;
            Part(hull, WorldMeshes.Ball(0.04f), new Vector3(Mathf.Cos(a) * 0.62f, 0f, Mathf.Sin(a) * 0.62f), i % 2 == 0 ? green : amber);
        }
        emitter = Part(r, WorldMeshes.Frustum(12, 0.18f, 0.18f, 0.03f), new Vector3(0f, -0.15f, 0f), Glowing(Beam, 2.6f)).transform;
        if (pilot) Pilot(r);
        ModelKit.Shadows(Part(r, WorldMeshes.Dome(14, 4, 0.27f), new Vector3(0f, 0.105f, 0f), Glass), false);
        if (legs) Legs(r);
        return root;
    }

    // A tiny green pilot with big black eyes and two antennae, under the dome.
    static void Pilot(Transform r) {
        Material skin = Mats.Lit(AlienSkin, 0.45f), eye = Mats.Lit(Ink, 0.95f), tip = Glowing(Beam, 4f);
        Part(r, WorldMeshes.Frustum(8, 0.09f, 0.06f, 0.07f), new Vector3(0f, 0.105f, 0f), SilverMetal);
        Part(r, WorldMeshes.Ball(0.085f), new Vector3(0f, 0.235f, 0f), skin, default, new Vector3(1f, 1.1f, 1f));
        for (int s = -1; s <= 1; s += 2) {
            Part(r, WorldMeshes.Ball(0.028f), new Vector3(s * 0.035f, 0.24f, -0.07f), eye, new Vector3(0f, 0f, s * 22f), new Vector3(1.4f, 0.8f, 0.5f));
            Rod(r, new Vector3(s * 0.025f, 0.3f, 0f), new Vector3(s * 0.06f, 0.34f, 0f), 0.012f, skin);
            Part(r, WorldMeshes.Ball(0.016f), new Vector3(s * 0.06f, 0.345f, 0f), tip);
        }
    }

    static void Legs(Transform r) {
        for (int i = 0; i < 3; i++) {
            float a = (90f + i * 120f) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 foot = dir * 0.3f + Vector3.down * 0.35f;
            Rod(r, dir * 0.2f + Vector3.down * 0.1f, foot, 0.035f, DarkMetal);
            Part(r, WorldMeshes.Frustum(8, 0.07f, 0.06f, 0.02f), foot + Vector3.down * 0.02f, DarkMetal);
        }
    }

    // ------------------------------------------------------------------ the ray gun (icon; the bolt is built by AlienRayBolt)
    // A retro ray gun: a silver bulb with red fins, glowing coils along the barrel and a flared, glowing muzzle.
    public static GameObject RayGunModel(Transform parent) {
        GameObject root = ModelKit.Root("Ray Gun", parent);
        Transform r = root.transform;
        Material silver = SilverMetal, glow = Glowing(Laser, 3.5f);
        Material fin = Mats.Lit(new Color(0.9f, 0.3f, 0.22f), 0.45f);
        Vector3 alongX = new Vector3(0f, 0f, 90f);
        ModelKit.Part(r, PrimitiveType.Sphere, new Vector3(-0.06f, 0.06f, 0f), new Vector3(0.36f, 0.2f, 0.2f), silver);    // bulb
        ModelKit.Part(r, PrimitiveType.Cylinder, new Vector3(0.2f, 0.06f, 0f), new Vector3(0.06f, 0.16f, 0.06f), silver, alongX);
        for (int i = 0; i < 3; i++) {
            float d = 0.11f - i * 0.012f;
            ModelKit.Part(r, PrimitiveType.Cylinder, new Vector3(0.12f + i * 0.075f, 0.06f, 0f), new Vector3(d, 0.012f, d), glow, alongX);
        }
        Part(r, MeshKit.Cone(10, 0.075f, 0.1f), new Vector3(0.42f, 0.06f, 0f), silver, new Vector3(0f, 0f, 90f));   // flared muzzle
        ModelKit.Part(r, PrimitiveType.Sphere, new Vector3(0.43f, 0.06f, 0f), Vector3.one * 0.07f, glow);
        ModelKit.Part(r, PrimitiveType.Cube, new Vector3(-0.21f, 0.16f, 0f), new Vector3(0.14f, 0.1f, 0.02f), fin, new Vector3(0f, 0f, 25f));
        ModelKit.Part(r, PrimitiveType.Cube, new Vector3(-0.22f, -0.02f, 0f), new Vector3(0.12f, 0.08f, 0.02f), fin, new Vector3(0f, 0f, -25f));
        ModelKit.Part(r, PrimitiveType.Cube, new Vector3(-0.1f, -0.1f, 0f), new Vector3(0.08f, 0.2f, 0.09f), DarkMetal, new Vector3(0f, 0f, -18f));
        ModelKit.Part(r, PrimitiveType.Cylinder, new Vector3(-0.04f, 0.17f, 0f), new Vector3(0.06f, 0.04f, 0.06f), glow);   // power cell
        return root;
    }

    // ------------------------------------------------------------------ the cow
    public static GameObject CowModel(Transform parent) { return Cow(parent); }

    // A low-poly cow, head along +X, about a metre long: white with black patches on both flanks, pink snout and
    // udder, cream horns, dark hooves, a tail with a tuft and a little golden bell.
    public static GameObject Cow(Transform parent) {
        GameObject root = ModelKit.Root("Cow", parent);
        Transform r = root.transform;
        Material white = Mats.Lit(new Color(0.96f, 0.95f, 0.92f), 0.3f), black = Mats.Lit(new Color(0.08f, 0.08f, 0.09f), 0.25f);
        Material pink = Mats.Lit(new Color(1f, 0.66f, 0.72f), 0.35f), horn = Mats.Lit(new Color(0.93f, 0.88f, 0.74f), 0.4f);
        Material hoof = Mats.Lit(new Color(0.2f, 0.18f, 0.17f), 0.3f), gold = Mats.Lit(new Color(1f, 0.78f, 0.3f), 0.7f, 0.5f);

        Vector3 body = new Vector3(0.465f, 0.3f, 0.285f);                               // semi-axes of the body
        Part(r, WorldMeshes.Ball(0.3f), Vector3.zero, white, default, new Vector3(1.55f, 1f, 0.95f));
        for (int s = -1; s <= 1; s += 2) {                                               // patches on both flanks
            Patch(r, black, body, Vector3.zero, -0.14f, 0.07f, s, 0.13f, 20f);
            Patch(r, black, body, Vector3.zero, 0.17f, -0.06f, s, 0.1f, -30f);
            Patch(r, black, body, Vector3.zero, -0.3f, -0.1f, s, 0.08f, 60f);
        }
        Part(r, WorldMeshes.Ball(0.1f), new Vector3(0.02f, 0.27f, 0f), black, new Vector3(0f, 0f, 10f), new Vector3(1.4f, 0.4f, 1.3f));   // on the back

        // head with a patch round one eye, snout, nostrils, eyes, horns and ears
        Vector3 head = new Vector3(0.5f, 0.12f, 0f), headAxes = new Vector3(0.2125f, 0.17f, 0.153f);
        Part(r, WorldMeshes.Ball(0.17f), head, white, default, new Vector3(1.25f, 1f, 0.9f));
        Patch(r, black, headAxes, head, 0.07f, 0.06f, -1, 0.06f, 0f);
        Part(r, WorldMeshes.Ball(0.12f), new Vector3(0.66f, 0.06f, 0f), pink, default, new Vector3(0.9f, 0.75f, 1.05f));
        for (int s = -1; s <= 1; s += 2) {
            Part(r, WorldMeshes.Ball(0.018f), new Vector3(0.765f, 0.075f, s * 0.04f), black);
            Part(r, WorldMeshes.Ball(0.034f), new Vector3(0.58f, 0.19f, s * 0.122f), black);
            Part(r, WorldMeshes.Ball(0.011f), new Vector3(0.6f, 0.205f, s * 0.15f), Mats.Lit(Color.white, 0.9f));
            Part(r, MeshKit.Cone(6, 0.035f, 0.14f), new Vector3(0.46f, 0.26f, s * 0.09f), horn, new Vector3(s * 35f, 0f, 15f));
            Part(r, WorldMeshes.Ball(0.07f), new Vector3(0.43f, 0.2f, s * 0.18f), white, new Vector3(s * -20f, 0f, 0f), new Vector3(0.5f, 0.35f, 1.2f));
        }
        Part(r, WorldMeshes.Frustum(8, 0.045f, 0.02f, 0.06f), new Vector3(0.38f, -0.2f, 0f), gold);   // bell
        Part(r, WorldMeshes.Ball(0.016f), new Vector3(0.38f, -0.205f, 0f), gold);

        for (int i = 0; i < 4; i++) {                                                    // legs and hooves
            Vector3 foot = new Vector3(i < 2 ? 0.24f : -0.24f, -0.42f, i % 2 == 0 ? 0.15f : -0.15f);
            Part(r, WorldMeshes.Frustum(6, 0.055f, 0.065f, 0.24f), foot, white);
            Part(r, WorldMeshes.Frustum(6, 0.065f, 0.06f, 0.06f), foot + Vector3.down * 0.05f, hoof);
        }
        Part(r, WorldMeshes.Ball(0.09f), new Vector3(-0.1f, -0.24f, 0f), pink, default, new Vector3(1.1f, 0.8f, 1f));   // udder
        for (int i = 0; i < 4; i++)
            Part(r, WorldMeshes.Frustum(5, 0.012f, 0.018f, 0.05f), new Vector3(i < 2 ? -0.06f : -0.14f, -0.36f, i % 2 == 0 ? 0.04f : -0.04f), pink);
        Rod(r, new Vector3(-0.44f, 0.12f, 0f), new Vector3(-0.56f, -0.14f, 0f), 0.025f, white);                     // tail
        Part(r, WorldMeshes.Ball(0.045f), new Vector3(-0.565f, -0.17f, 0f), black, default, new Vector3(0.8f, 1.3f, 0.8f));
        return root;
    }

    // A flattened black patch lying on an ellipsoid (semi-axes `axes`, centred on `centre`) at (x, y) on its
    // -z (side -1) or +z (side +1) face, turned `roll` degrees.
    static void Patch(Transform r, Material m, Vector3 axes, Vector3 centre, float x, float y, int side, float size, float roll) {
        float k = 1f - (x * x) / (axes.x * axes.x) - (y * y) / (axes.y * axes.y);
        float z = side * axes.z * Mathf.Sqrt(Mathf.Max(0.05f, k));
        Vector3 normal = new Vector3(x / (axes.x * axes.x), y / (axes.y * axes.y), z / (axes.z * axes.z)).normalized;
        Quaternion rot = Quaternion.LookRotation(normal) * Quaternion.Euler(0f, 0f, roll);
        Part(r, WorldMeshes.Ball(size), centre + new Vector3(x, y, z) - normal * size * 0.1f, m, rot.eulerAngles, new Vector3(1.4f, 1f, 0.28f));
    }

    // ------------------------------------------------------------------ the space warp (icon)
    // A violet ringed planet with a glowing ring and three little stars around it.
    public static GameObject WarpModel(Transform parent) {
        GameObject root = ModelKit.Root("Space Warp", parent);
        Transform r = root.transform;
        ModelKit.Part(r, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.52f, Mats.Lit(new Color(0.42f, 0.33f, 0.88f), 0.5f));
        ModelKit.Part(r, PrimitiveType.Sphere, new Vector3(0f, 0.09f, 0f), new Vector3(0.528f, 0.08f, 0.528f), Mats.Lit(new Color(0.72f, 0.6f, 1f), 0.5f));
        ModelKit.Part(r, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.98f, 0.008f, 0.98f), Glowing(new Color(0.78f, 0.7f, 1f), 1.8f),
                      new Vector3(-18f, 0f, -16f));
        Material star = Glowing(new Color(1f, 0.95f, 0.75f), 3f);
        Vector3[] stars = { new Vector3(-0.42f, 0.36f, -0.1f), new Vector3(0.44f, 0.3f, -0.1f), new Vector3(0.3f, -0.4f, -0.1f) };
        for (int i = 0; i < stars.Length; i++)
            Part(r, WorldMeshes.Star(0.07f - i * 0.012f, 0.03f - i * 0.005f), stars[i], star);
        return root;
    }
}

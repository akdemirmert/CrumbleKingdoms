using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// The sea at World.WaterY (or a lava sea): an animated near grid around the battlefield, flat far skirts
// that dissolve into the horizon haze, and surf (or glowing embers) along every cliff that meets it.
public static class SeaBuilder {
    public const float NearHalfX = 130f, NearBack = 90f, Front = -170f, FarBack = 520f, FarHalfX = 900f;
    public const float FoamTile = 6f;
    const int WaterQueue = 2980, FoamQueue = 2990;   // before other transparents: splashes and glass draw over the water

    public static void Build(Transform root, ArenaPalette p, List<IslandSpec> islands) {
        Transform sea = ModelKit.Root(p.lava ? "Lava Sea" : "Sea", root).transform;
        Material mat = p.lava ? LavaMaterial(p) : WaterMaterial(p);
        System.Func<float, float, Vector2> uv = p.lava
            ? (System.Func<float, float, Vector2>)((x, z) => new Vector2(x, z))                               // metres, tiled by the material
            : (x, z) => new Vector2(x / 60f, (z - Front) / (FarBack - Front));                                // v: near edge 0 .. horizon 1

        GameObject near = NearSea(sea, mat, uv, out float[] weight);
        SeaWaves waves = near.AddComponent<SeaWaves>();
        waves.Init(near.GetComponent<MeshFilter>().sharedMesh, weight, p.lava ? 0.12f : 0.32f, p.lava ? 0.35f : 1f, p.lava ? mat : null);
        Skirt(sea, mat, uv, -FarHalfX, FarHalfX, NearBack, FarBack);
        Skirt(sea, mat, uv, -FarHalfX, -NearHalfX, Front, NearBack);
        Skirt(sea, mat, uv, NearHalfX, FarHalfX, Front, NearBack);

        for (int i = 0; i < islands.Count; i++) Shore(sea, p, islands, i, waves);
    }

    // ------------------------------------------------------------------ materials
    static Material WaterMaterial(ArenaPalette p) {
        Material m = new Material(Mats.Transparent(Color.white, 0.92f, WorldTextures.Sea(p))) { name = "Sea " + p.theme };
        m.renderQueue = WaterQueue;
        return m;
    }

    // Opaque lava: dark crust albedo with glowing cracks in the emission map (HDR, so the cracks bloom).
    static Material LavaMaterial(ArenaPalette p) {
        Material m = new Material(Mats.Emissive(Color.white, p.seaNear * 2.6f, 0.3f)) { name = "Lava" };
        m.SetTexture("_BaseMap", WorldTextures.LavaCrust);
        m.SetTexture("_EmissionMap", WorldTextures.LavaGlow);
        m.SetTextureScale("_BaseMap", Vector2.one / WorldTextures.LavaTile);
        return m;
    }

    // ------------------------------------------------------------------ surfaces
    // Grid dense in the middle (2 m) and coarser towards its edges; weight = wave strength per vertex.
    static GameObject NearSea(Transform parent, Material mat, System.Func<float, float, Vector2> uv, out float[] weight) {
        List<float> xs = Axis(-NearHalfX, NearHalfX, -70f, 70f), zs = Axis(Front, NearBack, -26f, 40f);
        MeshBuilder b = new MeshBuilder();
        int cols = xs.Count;
        weight = new float[cols * zs.Count];
        for (int j = 0; j < zs.Count; j++)
            for (int i = 0; i < cols; i++) {
                float x = xs[i], z = zs[j];
                float edge = Mathf.Min(Mathf.Min(x - -NearHalfX, NearHalfX - x), Mathf.Min(z - Front, NearBack - z));
                weight[b.Vertex(new Vector3(x, World.WaterY, z), uv(x, z))] = Mathf.SmoothStep(0f, 1f, edge / 30f);
            }
        // seen from above: x to the right, z up the screen
        for (int j = 0; j < zs.Count - 1; j++)
            for (int i = 0; i < cols - 1; i++) {
                int bl = j * cols + i;
                b.Quad(bl, bl + cols, bl + cols + 1, bl + 1);
            }
        GameObject go = ModelKit.MeshPart(parent, b.ToMesh("Near sea"), Vector3.zero, Vector3.one, mat, default, "Near sea");
        NoShadows(go);
        return go;
    }

    static void Skirt(Transform parent, Material mat, System.Func<float, float, Vector2> uv, float x0, float x1, float z0, float z1) {
        MeshBuilder b = new MeshBuilder();
        float y = World.WaterY;
        b.Quad(b.Vertex(new Vector3(x0, y, z0), uv(x0, z0)), b.Vertex(new Vector3(x0, y, z1), uv(x0, z1)),
               b.Vertex(new Vector3(x1, y, z1), uv(x1, z1)), b.Vertex(new Vector3(x1, y, z0), uv(x1, z0)));
        NoShadows(ModelKit.MeshPart(parent, b.ToMesh("Far sea"), Vector3.zero, Vector3.one, mat, default, "Far sea"));
    }

    // Sample positions: `step` apart inside [denseMin, denseMax], growing by 20 % per step outside it.
    static List<float> Axis(float min, float max, float denseMin, float denseMax, float step = 2f) {
        List<float> v = new List<float>();
        for (float a = denseMin; a <= denseMax + 1e-3f; a += step) v.Add(a);
        for (float s = step, a = denseMax; a < max;) { s *= 1.2f; a = Mathf.Min(max, a + s); v.Add(a); }
        for (float s = step, a = denseMin; a > min;) { s *= 1.2f; a = Mathf.Max(min, a - s); v.Insert(0, a); }
        return v;
    }

    // ------------------------------------------------------------------ shores
    // Foam (or a glowing rim of lava) hugging the waterline of every cliff face that meets the open sea,
    // plus a gentle surf of foam puffs (or rising embers) along the front of the island.
    static void Shore(Transform parent, ArenaPalette p, List<IslandSpec> all, int index, SeaWaves waves) {
        IslandSpec isl = all[index];
        bool left = IslandBuilder.SideOpen(all, index, true, World.WaterY), right = IslandBuilder.SideOpen(all, index, false, World.WaterY);
        List<EdgePoint> path = IslandBuilder.EdgePath(isl, left, right, World.WaterY + 0.06f, 0.5f, 0.8f);
        System.Random rng = new System.Random(Mathf.RoundToInt(isl.xMin * 31f) + 7);
        MeshBuilder b = new MeshBuilder();
        Color tint = p.lava ? new Color(1f, 0.55f, 0.15f, 1f) : Color.white;
        int prevInner = -1, prevOuter = -1;
        foreach (EdgePoint e in path) {
            float width = 1.2f + 0.9f * (float)rng.NextDouble();
            int inner = b.Vertex(e.position, new Vector2(e.distance, 1f), tint);
            int outer = b.Vertex(e.position + e.outward * width, new Vector2(e.distance, 0f), tint);
            if (prevInner >= 0) b.Quad(prevOuter, prevInner, inner, outer);   // faces up (see IslandBuilder.EdgePath)
            prevInner = inner;
            prevOuter = outer;
        }
        Material m = new Material(p.lava ? Mats.ParticleAdditive(WorldTextures.Foam) : Mats.UnlitTransparent(new Color(1f, 1f, 1f, 0.85f), WorldTextures.Foam));
        m.SetTextureScale("_BaseMap", new Vector2(1f / FoamTile, 1f));
        if (!p.lava) m.renderQueue = FoamQueue;
        GameObject foam = ModelKit.MeshPart(parent, b.ToMesh("Foam"), Vector3.zero, Vector3.one, m, default, "Foam");
        NoShadows(foam);
        waves.AddFoam(foam.GetComponent<Renderer>());
        Surf(parent, p, isl);
    }

    static void Surf(Transform parent, ArenaPalette p, IslandSpec isl) {
        float width = isl.xMax - isl.xMin;
        GameObject go = ModelKit.Root("Surf", parent);
        go.transform.position = new Vector3((isl.xMin + isl.xMax) * 0.5f, World.WaterY + 0.1f, IslandBuilder.Front - 0.9f);
        go.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1f, -0.5f));   // emit up and away from the rock
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.duration = 2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width, 0.3f, 0.2f);
        ParticleSystem.EmissionModule em = ps.emission;
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.shadowCastingMode = ShadowCastingMode.Off;
        if (p.lava) {
            // embers rising from where the lava licks the rock
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.3f), new Color(1f, 0.4f, 0.1f));
            main.gravityModifier = -0.05f;
            em.rateOverTime = width * 0.7f;
            col.color = Fade(new Color(1f, 0.6f, 0.2f), new Color(0.8f, 0.15f, 0.05f), 1f);
            r.sharedMaterial = Mats.ParticleAdditive(TexKit.SoftCircle);
        } else {
            // puffs of foam thrown up by the swell and falling back
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 1f);
            main.startColor = new Color(1f, 1f, 1f, 0.55f);
            main.gravityModifier = 0.25f;
            em.rateOverTime = width * 0.5f;
            col.color = Fade(Color.white, Color.white, 0.6f);
            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.3f));
            r.sharedMaterial = Mats.ParticleAlpha(TexKit.SoftCircle);
        }
        ps.Play();
    }

    // Colour over lifetime: fade in quickly, fade out to nothing.
    public static ParticleSystem.MinMaxGradient Fade(Color from, Color to, float alpha) {
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.15f), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }

    static void NoShadows(GameObject go) { ModelKit.Shadows(go, false, true); }
}

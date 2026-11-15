using UnityEngine;

// The shared particle layers every effect draws with (see FxLayer). Each layer is built the first time
// it is used in a scene; a scene change destroys them and the null check rebuilds them.
public static class FxLayers {
    // Draw order among transparent objects: smoke under fire under glows under flashes.
    public const int OrderSmoke = 1, OrderFire = 2, OrderGlow = 3, OrderFlash = 4;

    static Transform root;
    static FxLayer flash, glow, fire, smoke, dust, spark, ember, star, ray, droplet, shock, foam, charChunks;
    static readonly FxLayer[] chunks = new FxLayer[6];

    public static FxLayer Flash => flash != null ? flash : (flash = Build("Flash", FxLooks.Flash()));
    public static FxLayer Glow => glow != null ? glow : (glow = Build("Glow", FxLooks.Glow()));
    public static FxLayer Fire => fire != null ? fire : (fire = Build("Fire", FxLooks.Fire()));
    public static FxLayer Smoke => smoke != null ? smoke : (smoke = Build("Smoke", FxLooks.Smoke()));
    public static FxLayer Dust => dust != null ? dust : (dust = Build("Dust", FxLooks.Dust()));
    public static FxLayer Spark => spark != null ? spark : (spark = Build("Sparks", FxLooks.Spark()));
    public static FxLayer Ember => ember != null ? ember : (ember = Build("Embers", FxLooks.Ember()));
    public static FxLayer Star => star != null ? star : (star = Build("Stars", FxLooks.Star()));
    public static FxLayer Ray => ray != null ? ray : (ray = Build("Rays", FxLooks.Ray()));
    public static FxLayer Droplet => droplet != null ? droplet : (droplet = Build("Droplets", FxLooks.Droplet()));
    public static FxLayer Shock => shock != null ? shock : (shock = Build("Shock rings", FxLooks.Shock()));
    public static FxLayer Foam => foam != null ? foam : (foam = Build("Foam", FxLooks.Foam()));
    public static FxLayer CharChunks => charChunks != null ? charChunks : (charChunks = Build("Char chunks", FxLooks.Chunk(FxAssets.Char)));

    // Tumbling pieces of one block material.
    public static FxLayer Chunks(BlockMaterial m) {
        int i = (int)m;
        return chunks[i] != null ? chunks[i] : (chunks[i] = Build(m + " chunks", FxLooks.Chunk(FxAssets.Chunk(m))));
    }

    static FxLayer Build(string name, LayerLook look) {
        if (root == null) root = new GameObject("[FX]").transform;
        return FxLayer.Create(name, look, root);
    }
}

// The look of every layer: material, motion and how size and colour change over a particle's life.
public static class FxLooks {
    public const float GlowDrag = 2.5f, DropletGravity = 1f;

    public static LayerLook Flash() => new LayerLook {
        material = FxAssets.Additive(FxAssets.Glow, 3f), order = FxLayers.OrderFlash,
        size = Curve(0f, 0.7f, 1f, 1.1f), color = Fade(0f, 1f, 0.25f, 0.65f, 1f, 0f), maxScreenSize = 3f, maxParticles = 80,
    };

    public static LayerLook Glow() => new LayerLook {
        material = FxAssets.Additive(FxAssets.Glow, 2f), order = FxLayers.OrderGlow, drag = GlowDrag,
        size = Curve(0f, 0.45f, 0.25f, 1f, 1f, 1.15f), color = Fade(0f, 0.6f, 0.1f, 1f, 1f, 0f), maxScreenSize = 2f, maxParticles = 300,
    };

    // Alpha-blended but HDR-bright puffs that cool from white-hot through orange and red to dark smoke.
    public static LayerLook Fire() => new LayerLook {
        material = FxAssets.Alpha(TexKit.Smoke, 2.4f), order = FxLayers.OrderFire, gravity = -0.35f, drag = 3f, youngestInFront = true,
        size = Curve(0f, 0.35f, 0.2f, 0.85f, 1f, 1.25f),
        color = Ramp(new[] { new Color(1f, 0.96f, 0.85f), new Color(1f, 0.8f, 0.4f), new Color(1f, 0.48f, 0.14f),
                             new Color(0.55f, 0.17f, 0.07f), new Color(0.2f, 0.12f, 0.1f), new Color(0.14f, 0.12f, 0.11f) },
                     new[] { 0f, 0.12f, 0.32f, 0.55f, 0.8f, 1f }, 0f, 1f, 0.6f, 0.85f, 1f, 0f),
        maxScreenSize = 1.5f, maxParticles = 400,
    };

    public static LayerLook Smoke() => new LayerLook {
        material = FxAssets.Alpha(TexKit.Smoke), order = FxLayers.OrderSmoke, gravity = -0.06f, drag = 1.6f, youngestInFront = true,
        size = Curve(0f, 0.4f, 0.3f, 0.85f, 1f, 1.3f), color = Fade(0f, 0f, 0.12f, 1f, 0.55f, 0.75f, 1f, 0f),
        maxScreenSize = 2f, maxParticles = 400,
    };

    // Settling puffs: block dust, water mist.
    public static LayerLook Dust() => new LayerLook {
        material = FxAssets.Alpha(TexKit.Smoke), order = FxLayers.OrderSmoke, gravity = 0.02f, drag = 2.6f, youngestInFront = true,
        size = Curve(0f, 0.5f, 1f, 1.35f), color = Fade(0f, 0f, 0.07f, 1f, 1f, 0f), maxScreenSize = 2f, maxParticles = 400,
    };

    // Streaks stretched along their velocity.
    public static LayerLook Spark() => new LayerLook {
        material = FxAssets.Additive(TexKit.Spark, 3f), mode = ParticleSystemRenderMode.Stretch, velocityScale = 0.045f, lengthScale = 1.5f,
        order = FxLayers.OrderGlow, gravity = 0.9f, drag = 1.3f, size = Curve(0f, 1f, 1f, 0.15f),
        color = Ramp(new[] { Color.white, new Color(1f, 0.7f, 0.5f) }, new[] { 0f, 1f }, 0f, 1f, 0.7f, 0.9f, 1f, 0f), maxParticles = 500,
    };

    // Floating glowing motes that drift on turbulence.
    public static LayerLook Ember() => new LayerLook {
        material = FxAssets.Additive(FxAssets.Glow, 2.5f), order = FxLayers.OrderGlow, gravity = -0.12f, drag = 0.9f, noise = 0.9f,
        size = Curve(0f, 1f, 1f, 0.2f), color = Fade(0f, 0f, 0.08f, 1f, 0.7f, 0.8f, 1f, 0f), maxParticles = 300,
    };

    // Twinkling four-point stars (magic, holy light, glass glints).
    public static LayerLook Star() => new LayerLook {
        material = FxAssets.Additive(TexKit.Star, 2.5f), order = FxLayers.OrderGlow, gravity = 0.05f, drag = 2.2f,
        size = Curve(0f, 0f, 0.15f, 1f, 0.6f, 0.75f, 1f, 0f), maxParticles = 400,
    };

    // Long light rays: the stretch length equals the speed (velocityScale 1), so speed sets the length.
    public static LayerLook Ray() => new LayerLook {
        material = FxAssets.Additive(TexKit.Spark, 2.2f), mode = ParticleSystemRenderMode.Stretch, velocityScale = 1f, lengthScale = 1f,
        order = FxLayers.OrderGlow, size = Curve(0f, 0.6f, 0.2f, 1f, 1f, 0.8f), color = Fade(0f, 0f, 0.12f, 1f, 1f, 0f),
        maxScreenSize = 3f, maxParticles = 100,
    };

    public static LayerLook Droplet() => new LayerLook {
        material = FxAssets.Alpha(TexKit.SoftCircle, 1.25f), mode = ParticleSystemRenderMode.Stretch, velocityScale = 0.05f, lengthScale = 1.2f,
        order = FxLayers.OrderFire, gravity = DropletGravity, drag = 0.1f, size = Curve(0f, 1f, 1f, 0.7f), color = Fade(0f, 1f, 0.85f, 0.9f, 1f, 0f),
        maxParticles = 400,
    };

    // A single expanding ring that faces the camera.
    public static LayerLook Shock() => new LayerLook {
        material = FxAssets.Additive(TexKit.Ring, 2f), order = FxLayers.OrderGlow,
        size = Curve(0f, 0.15f, 0.3f, 0.75f, 1f, 1f), color = Fade(0f, 1f, 1f, 0f), maxScreenSize = 3f, maxParticles = 40,
    };

    // A ring lying flat on the water.
    public static LayerLook Foam() => new LayerLook {
        material = FxAssets.Alpha(TexKit.Ring, 1.3f), mode = ParticleSystemRenderMode.HorizontalBillboard, order = FxLayers.OrderFire,
        size = Curve(0f, 0.2f, 1f, 1f), color = Fade(0f, 0.9f, 1f, 0f), maxScreenSize = 3f, maxParticles = 40,
    };

    // Lit cube pieces that fall with full gravity and shrink away at the end.
    public static LayerLook Chunk(Material mat) => new LayerLook {
        material = mat, mode = ParticleSystemRenderMode.Mesh, mesh = ModelKit.Primitive(PrimitiveType.Cube), order = FxLayers.OrderFire,
        gravity = 1f, drag = 0.3f, chunks = true, size = Curve(0f, 1f, 0.75f, 1f, 1f, 0f), maxParticles = 200,
    };

    // VFX.Emitter: glowing motes left behind a moving object.
    public static LayerLook TrailGlow(float intensity) => new LayerLook {
        material = FxAssets.Additive(FxAssets.Glow, 1.6f * intensity), order = FxLayers.OrderGlow, gravity = -0.05f, drag = 1f,
        size = Curve(0f, 1f, 1f, 0.1f), color = Fade(0f, 0.85f, 1f, 0f), maxParticles = 200,
    };

    // ------------------------------------------------------------------ helpers
    // Piecewise-linear curve from (time, value) pairs.
    public static AnimationCurve Curve(params float[] tv) {
        int n = tv.Length / 2;
        Keyframe[] keys = new Keyframe[n];
        for (int i = 0; i < n; i++) keys[i] = new Keyframe(tv[2 * i], tv[2 * i + 1]);
        for (int i = 0; i < n; i++) {
            float slopeIn = i > 0 ? Slope(keys[i - 1], keys[i]) : (n > 1 ? Slope(keys[0], keys[1]) : 0f);
            float slopeOut = i < n - 1 ? Slope(keys[i], keys[i + 1]) : slopeIn;
            keys[i].inTangent = slopeIn;
            keys[i].outTangent = slopeOut;
        }
        return new AnimationCurve(keys);
    }

    // White, with alpha keys from (time, alpha) pairs.
    public static Gradient Fade(params float[] ta) {
        return Ramp(new[] { Color.white, Color.white }, new[] { 0f, 1f }, ta);
    }

    // Colour keys at the given times plus alpha keys from (time, alpha) pairs (at most 8 of each).
    public static Gradient Ramp(Color[] colors, float[] times, params float[] ta) {
        GradientColorKey[] ck = new GradientColorKey[colors.Length];
        for (int i = 0; i < colors.Length; i++) ck[i] = new GradientColorKey(colors[i], times[i]);
        GradientAlphaKey[] ak = new GradientAlphaKey[ta.Length / 2];
        for (int i = 0; i < ak.Length; i++) ak[i] = new GradientAlphaKey(ta[2 * i + 1], ta[2 * i]);
        Gradient g = new Gradient();
        g.SetKeys(ck, ak);
        return g;
    }

    static float Slope(Keyframe a, Keyframe b) {
        float dt = b.time - a.time;
        return dt > 1e-5f ? (b.value - a.value) / dt : 0f;
    }
}

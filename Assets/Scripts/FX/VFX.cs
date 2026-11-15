using TMPro;
using UnityEngine;

// Visual effects API used by gameplay code. Every effect cleans itself up.
// Particles are emitted into a few shared layers (FxLayers), so even a big explosion creates no new
// objects; lights, rings, bolts, text and attached emitters are small self-destroying objects.
public static class VFX {
    // Blocks span z = ±0.8 around the battle plane; effects are drawn just in front so blocks never hide them.
    public const float FrontZ = -(World.BlockDepth * 0.5f + 0.15f);

    // ================================================================== explosions
    // Everything scales with the radius; the particle total stays under ~250 even for the biggest blasts.
    public static void Explosion(Vector3 pos, float radius, BlastStyle style) {
        if (!Valid(pos)) return;
        Vector3 c = Front(pos);
        float s = Mathf.Clamp(radius, 0.4f, 9f);
        switch (style) {
            case BlastStyle.Small: FireBlast(c, s, true); break;
            case BlastStyle.Keg: KegBlast(c, s); break;
            case BlastStyle.Magic: MagicBlast(c, s); break;
            case BlastStyle.Void: VoidBlast(c, s); break;
            case BlastStyle.Holy: HolyBlast(c, s); break;
            case BlastStyle.Lightning: LightningBlast(c, s); break;
            default: FireBlast(c, s, false); break;
        }
    }

    // White-hot flash, a fireball that cools into rolling smoke, sparks, embers, charred chunks, a shock ring.
    static void FireBlast(Vector3 c, float s, bool small) {
        float k = small ? 0.6f : 1f;
        FxLayers.Flash.Emit(c, Vector3.zero, s * (small ? 2.6f : 3.4f), small ? 0.11f : 0.15f, new Color(1f, 0.93f, 0.78f));
        FxLayers.Glow.Emit(c, Vector3.zero, s * 2.2f, small ? 0.3f : 0.45f, new Color(1f, 0.6f, 0.25f));
        Fireball(c, s, Count((8f + 5f * s) * k, 40), 1f);
        SmokeCloud(c, s, Count((5f + 3f * s) * k, 26), new Color(0.24f, 0.22f, 0.21f, small ? 0.55f : 0.75f), small ? 0.8f : 1.2f);
        SparkSpray(c, Count((10f + 6f * s) * k, 48), 9f + s * 1.5f, new Color(1f, 0.78f, 0.42f));
        Embers(c, s, Count((4f + 2f * s) * k, 18));
        if (!small) ChunkSpray(FxLayers.CharChunks, c, Count(3f + 1.5f * s, 12), 6f + s, 0.2f);
        FxLayers.Shock.Emit(c, Vector3.zero, s * (small ? 2.2f : 2.8f), small ? 0.24f : 0.32f, new Color(1f, 0.85f, 0.65f, 0.55f));
        LightFlash(c, new Color(1f, 0.62f, 0.3f), (small ? 4f : 6f) + s * 1.5f, s * 5f + 4f, small ? 0.35f : 0.5f);
    }

    // Powder keg: a bigger fire blast, flying staves and a few late puffs as the powder burns through.
    static void KegBlast(Vector3 c, float s) {
        s *= 1.2f;
        FireBlast(c, s, false);
        ChunkSpray(FxLayers.Chunks(BlockMaterial.Explosive), c, 10, 11f, 0.16f);
        Embers(c, s, 8);
        for (int i = 0; i < 3; i++) {
            Vector3 p = c + RandDir() * s * Random.Range(0.4f, 0.8f);
            float size = s * 0.45f;
            Runner.Delay(Random.Range(0.06f, 0.22f), () => Fireball(p, size, 7, 0.8f));
        }
    }

    // Violet flash, glowing puffs, star sparkles and a crisp ring.
    static void MagicBlast(Vector3 c, float s) {
        Color violet = new Color(0.72f, 0.45f, 1f), pink = new Color(1f, 0.5f, 0.92f);
        FxLayers.Flash.Emit(c, Vector3.zero, s * 3f, 0.16f, new Color(0.92f, 0.8f, 1f));
        GlowPuffs(c, s, Count(8f + 3f * s, 30), violet, pink);
        Stars(c, s, Count(12f + 5f * s, 40), Color.Lerp(violet, Color.white, 0.45f), new Color(0.6f, 0.85f, 1f));
        SparkSpray(c, Count(6f + 3f * s, 24), 10f, Color.Lerp(violet, Color.white, 0.3f));
        SmokeCloud(c, s * 0.8f, Count(3f + s, 10), new Color(0.22f, 0.1f, 0.32f, 0.55f), 0.6f);
        FxLayers.Shock.Emit(c, Vector3.zero, s * 2.6f, 0.3f, new Color(0.8f, 0.6f, 1f, 0.6f));
        Ring(c, s * 1.3f, violet, 0.4f, 0.22f + s * 0.03f);
        LightFlash(c, violet, 5f + s * 1.5f, s * 5f + 3f, 0.45f);
    }

    // Space folds in (motes and dark smoke rush to the centre), then bursts out in a ring.
    static void VoidBlast(Vector3 c, float s) {
        const float Implode = 0.24f;
        Color violet = new Color(0.55f, 0.25f, 1f), magenta = new Color(0.9f, 0.3f, 1f);
        int n = Count(16f + 2f * s, 34);
        for (int i = 0; i < n; i++) {
            Vector3 d = RandDir(0.2f);
            float dist = s * Random.Range(0.7f, 1.15f);
            float speed = ReachSpeed(dist, Implode, FxLooks.GlowDrag);
            FxLayers.Glow.Emit(c + d * dist, -d * speed, s * Random.Range(0.12f, 0.22f), Implode, Random.value < 0.5f ? violet : magenta);
        }
        FxLayers.Smoke.Emit(c, Vector3.zero, s * 1.1f, Implode + 0.35f, new Color(0.07f, 0.02f, 0.1f, 0.85f));
        Runner.Delay(Implode, () => VoidBurst(c, s, violet, magenta));
    }

    static void VoidBurst(Vector3 c, float s, Color violet, Color magenta) {
        FxLayers.Flash.Emit(c, Vector3.zero, s * 2.2f, 0.18f, new Color(0.85f, 0.7f, 1f));
        int n = Count(20f + 2f * s, 40);
        for (int i = 0; i < n; i++) {                                  // a ring of motes in the screen plane
            float a = (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / n;
            Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), -0.1f);
            FxLayers.Glow.Emit(c, d * s * Random.Range(2.5f, 4.5f), s * Random.Range(0.18f, 0.32f), Random.Range(0.4f, 0.7f),
                               Random.value < 0.5f ? violet : magenta);
        }
        Stars(c, s, Count(8f + 2f * s, 24), Color.Lerp(violet, Color.white, 0.5f), magenta);
        SparkSpray(c, Count(10f + 2f * s, 30), 9f + s, Color.Lerp(violet, Color.white, 0.3f));
        SmokeCloud(c, s * 0.7f, Count(4f + s, 12), new Color(0.1f, 0.03f, 0.15f, 0.65f), 0.3f);
        FxLayers.Shock.Emit(c, Vector3.zero, s * 2.4f, 0.35f, new Color(0.7f, 0.4f, 1f, 0.7f));
        Ring(c, s * 1.1f, magenta, 0.45f, 0.3f);
        LightFlash(c, violet, 6f + s, s * 4f + 4f, 0.5f);
    }

    // Golden flash with long light rays, sparkles and a warm ring.
    static void HolyBlast(Vector3 c, float s) {
        Color gold = new Color(1f, 0.85f, 0.4f), warm = new Color(1f, 0.96f, 0.82f);
        FxLayers.Flash.Emit(c, Vector3.zero, s * 2.4f, 0.2f, warm);
        int rays = Count(10f + s, 20);
        for (int i = 0; i < rays; i++) {
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            float len = s * Random.Range(0.7f, 1.3f);
            FxLayers.Ray.Emit(c + d * len * 0.5f, d * len, Random.Range(0.25f, 0.5f) * (0.7f + s * 0.06f), Random.Range(0.35f, 0.6f),
                              Color.Lerp(gold, warm, Random.value));
        }
        Stars(c, s, Count(16f + 2f * s, 36), gold, warm);
        GlowPuffs(c, s * 0.8f, Count(6f + s, 16), warm, gold);
        FxLayers.Shock.Emit(c, Vector3.zero, s * 2.6f, 0.4f, new Color(1f, 0.9f, 0.6f, 0.6f));
        Ring(c, s * 1.2f, gold, 0.5f, 0.3f);
        LightFlash(c, new Color(1f, 0.9f, 0.65f), 6f + s, s * 4f + 4f, 0.6f);
    }

    // Blue-white flash, a storm of sparks and short arcs crackling out of the centre.
    static void LightningBlast(Vector3 c, float s) {
        Color blue = new Color(0.55f, 0.82f, 1f), white = new Color(0.9f, 0.96f, 1f);
        FxLayers.Flash.Emit(c, Vector3.zero, s * 2.8f, 0.14f, white);
        FxLayers.Glow.Emit(c, Vector3.zero, s * 1.8f, 0.35f, blue);
        SparkSpray(c, Count(14f + 5f * s, 45), 14f, Color.Lerp(blue, white, 0.5f));
        int arcs = Mathf.Clamp(3 + (int)(s / 2f), 3, 6);
        for (int i = 0; i < arcs; i++)
            MakeBolt(c, c + RandDir(0.15f) * s * Random.Range(0.6f, 1.1f), blue, 0.1f + s * 0.02f, Random.Range(0.12f, 0.25f));
        GlowPuffs(c, s * 0.6f, Count(4f + s, 12), blue, white);
        SmokeCloud(c, s * 0.5f, Count(2f + s * 0.5f, 6), new Color(0.6f, 0.65f, 0.75f, 0.35f), 0.8f);
        FxLayers.Shock.Emit(c, Vector3.zero, s * 2.4f, 0.25f, new Color(0.6f, 0.85f, 1f, 0.6f));
        LightFlash(c, new Color(0.6f, 0.8f, 1f), 8f + 2f * s, s * 5f + 4f, 0.35f);
    }

    // ================================================================== blocks and debris
    public static void Dust(Vector3 pos, float size, Color? tint = null) {
        if (!Valid(pos)) return;
        float s = Mathf.Clamp(size, 0.2f, 6f);
        Puffs(Front(pos), s, Count(4f + s * 3f, 16), tint ?? new Color(0.74f, 0.68f, 0.58f, 0.6f));
    }

    // Small pieces knocked off a block, thrown mostly along `dir`.
    public static void Chips(Vector3 pos, BlockMaterial mat, int count, Vector3 dir) {
        if (!Valid(pos) || count <= 0) return;
        Vector3 p = Front(pos);
        Vector3 d = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.up;
        int n = Mathf.Clamp(count, 1, 18);
        FxLayer layer = FxLayers.Chunks(mat);
        for (int i = 0; i < n; i++) {
            Vector3 v = (d + RandDir(0.3f) * 0.7f + Vector3.up * 0.35f) * Random.Range(3f, 7.5f);
            layer.EmitChunk(p, v, ChipSize(mat), Random.Range(0.6f, 1.1f));
        }
        switch (mat) {
            case BlockMaterial.Metal: SparkSpray(p, Mathf.Clamp(n, 4, 14), 9f, new Color(1f, 0.75f, 0.4f)); break;
            case BlockMaterial.Glass:
            case BlockMaterial.Ice: Glints(p, 0.5f, Mathf.Max(2, n / 2)); break;
            default: Puffs(p, 0.45f + n * 0.03f, 3, DustColor(mat)); break;
        }
    }

    // A block broke: chunks from all over its face, a dust cloud (stone, wood), sparks (metal),
    // glittering shards (glass, ice).
    public static void Shatter(Vector3 pos, Vector3 size, BlockMaterial mat) {
        if (!Valid(pos)) return;
        Vector3 p = Front(pos);
        float extent = Mathf.Clamp(Mathf.Max(size.x, size.y), 0.3f, 6f);
        bool brittle = mat == BlockMaterial.Glass || mat == BlockMaterial.Ice;
        FxLayer layer = FxLayers.Chunks(mat);
        int n = Count(6f + extent * 3f, 18);
        for (int i = 0; i < n; i++) {
            Vector3 at = p + new Vector3(Random.Range(-0.5f, 0.5f) * size.x, Random.Range(-0.5f, 0.5f) * size.y, 0f);
            Vector3 v = new Vector3(Random.Range(-1f, 1f), Random.Range(0.2f, 1.2f), Random.Range(-0.4f, 0f)) * Random.Range(2f, 6f);
            layer.EmitChunk(at, v, ChipSize(mat) * (brittle ? 1.3f : 1.5f), Random.Range(0.8f, 1.5f));
        }
        if (brittle) {
            Glints(p, extent, Count(6f + extent * 3f, 16));
            if (mat == BlockMaterial.Ice) Puffs(p, extent * 0.6f, Count(3f + extent, 8), new Color(0.92f, 0.97f, 1f, 0.45f));
        } else {
            Puffs(p, extent * 0.75f, Count(4f + extent * 3f, 14), DustColor(mat));
            if (mat == BlockMaterial.Metal) SparkSpray(p, 12, 10f, new Color(1f, 0.7f, 0.35f));
        }
    }

    // Something hit the sea: a column of droplets, mist and a foam ring on the water.
    public static void Splash(Vector3 pos, float size) {
        if (!Valid(pos)) return;
        float s = Mathf.Clamp(size, 0.25f, 5f), r = Mathf.Sqrt(s);
        Vector3 p = new Vector3(pos.x, World.WaterY + 0.05f, pos.z);
        Color water = new Color(0.82f, 0.92f, 1f, 0.9f);
        float g = World.Gravity * FxLooks.DropletGravity;
        int n = Count(10f + s * 7f, 40);
        for (int i = 0; i < n; i++) {
            float vy = Random.Range(3.5f, 8.5f) * r;
            Vector3 v = new Vector3(Random.Range(-1.6f, 1.6f) * r, vy, Random.Range(-0.8f, 0.3f));
            float life = Mathf.Clamp(2f * vy / g, 0.25f, 1.8f);           // dies as it falls back onto the surface
            FxLayers.Droplet.Emit(p + Vector3.right * Random.Range(-0.3f, 0.3f) * s, v, Random.Range(0.07f, 0.16f) * r, life, water);
        }
        int mist = Count(3f + 2f * s, 10);
        for (int i = 0; i < mist; i++) {
            Vector3 v = new Vector3(Random.Range(-1f, 1f), Random.Range(0.5f, 2f), 0f) * r;
            FxLayers.Dust.Emit(p + Vector3.up * Random.Range(0f, 0.6f) * r, v, s * Random.Range(0.5f, 0.9f), Random.Range(0.7f, 1.2f),
                               new Color(0.9f, 0.95f, 1f, 0.5f), Random.Range(-40f, 40f));
        }
        FxLayers.Foam.Emit(p, Vector3.zero, s * 3.2f, 0.9f + 0.1f * s, new Color(0.95f, 0.98f, 1f, 0.8f));
    }

    // ================================================================== small bursts
    public static void Sparks(Vector3 pos, Color color, int count = 16) {
        if (!Valid(pos) || count <= 0) return;
        Vector3 p = Front(pos);
        Color hot = Color.Lerp(FxAssets.SplitHdr(color, out _), Color.white, 0.35f);
        SparkSpray(p, Mathf.Clamp(count, 1, 40), 11f, hot);
        FxLayers.Flash.Emit(p, Vector3.zero, 0.5f + Mathf.Min(count, 40) * 0.03f, 0.08f, hot);
    }

    public static void MagicBurst(Vector3 pos, Color color, float size = 1f) {
        if (!Valid(pos)) return;
        Vector3 p = Front(pos);
        float s = Mathf.Clamp(size, 0.2f, 6f);
        Color c = FxAssets.SplitHdr(color, out _), light = Color.Lerp(c, Color.white, 0.45f);
        FxLayers.Flash.Emit(p, Vector3.zero, s * 2.2f, 0.16f, light);
        GlowPuffs(p, s, Count(5f + 4f * s, 20), c, light);
        Stars(p, s, Count(8f + 7f * s, 30), light, c);
        FxLayers.Shock.Emit(p, Vector3.zero, s * 2.4f, 0.3f, new Color(c.r, c.g, c.b, 0.6f));
    }

    // A commander's soul leaves: a column of light, rising wisps and sparkles in the team colour.
    public static void SoulRise(Vector3 pos, Color color) {
        if (!Valid(pos)) return;
        Vector3 p = Front(pos);
        Color c = FxAssets.SplitHdr(color, out _), pale = Color.Lerp(c, Color.white, 0.45f);
        FxLayers.Glow.Emit(p, Vector3.zero, 2.4f, 0.7f, c);
        FxLayers.Ray.Emit(p + Vector3.up * 2.2f, Vector3.up * 4.4f, 0.7f, 0.9f, new Color(pale.r, pale.g, pale.b, 0.7f));
        for (int i = 0; i < 14; i++)
            FxLayers.Ember.Emit(p + RandDir(0.3f) * 0.45f, Vector3.up * Random.Range(1.2f, 2.8f) + RandDir(0.3f) * 0.5f,
                                Random.Range(0.18f, 0.42f), Random.Range(1.3f, 2.2f), Vary(pale, 0.15f));
        for (int i = 0; i < 10; i++)
            FxLayers.Star.Emit(p + RandDir(0.3f) * 0.3f, Vector3.up * Random.Range(1f, 3f) + RandDir(0.3f) * 1.2f,
                               Random.Range(0.15f, 0.3f), Random.Range(0.8f, 1.5f), Color.Lerp(pale, Color.white, 0.4f), Random.Range(-90f, 90f));
        FxLayers.Shock.Emit(p, Vector3.zero, 2.6f, 0.5f, new Color(c.r, c.g, c.b, 0.5f));
    }

    // ================================================================== attached, looping effects
    // Looping flames, embers and thin smoke on a burning object. The caller destroys the returned object;
    // the flames then die down over a moment instead of vanishing (they also do if the parent is destroyed).
    public static GameObject Fire(Transform parent, Vector3 localPos, float size) {
        GameObject handle = new GameObject("Fire");
        if (parent != null) handle.transform.SetParent(parent, false);
        handle.transform.localPosition = localPos;

        float s = Mathf.Clamp(size, 0.3f, 5f), f = Mathf.Min(s, 2.4f);
        float z = -Mathf.Min(World.BlockDepth * 0.5f + 0.1f, s * 0.6f);  // just in front of the burning object
        GameObject fx = new GameObject("Fire FX");
        fx.transform.position = handle.transform.position;
        Vector3 area = new Vector3(s * 0.7f, s * 0.45f, 0.1f);

        ParticleSystem flames = FxLayer.AddLoop(Child(fx, "Flames"), FxLooks.Fire(), 8f + 7f * f, Color.white);
        FxLayer.SetRanges(flames, 0.45f, 0.8f, f * 0.3f, f * 0.55f, 0.2f, 0.8f);
        Area(flames, area, new Vector3(0f, 0f, z));

        LayerLook smokeLook = FxLooks.Smoke();
        smokeLook.gravity = -0.18f;                                    // hot smoke rises faster than blast smoke
        ParticleSystem smoke = FxLayer.AddLoop(Child(fx, "Smoke"), smokeLook, 2f + 1.5f * f, new Color(0.2f, 0.18f, 0.17f, 0.5f));
        FxLayer.SetRanges(smoke, 1.4f, 2.2f, f * 0.5f, f * 0.9f, 0.3f, 0.8f);
        Area(smoke, area * 0.8f, new Vector3(0f, s * 0.3f, z + 0.05f));

        ParticleSystem embers = FxLayer.AddLoop(Child(fx, "Embers"), FxLooks.Ember(), 2f + 1.5f * f, new Color(1f, 0.6f, 0.2f));
        FxLayer.SetRanges(embers, 1f, 1.8f, 0.06f, 0.12f, 0.5f, 1.5f);
        Area(embers, area, new Vector3(0f, s * 0.1f, z));

        flames.Play();
        smoke.Play();
        embers.Play();
        FlickerLight.Add(fx.transform, new Vector3(0f, s * 0.2f, -1.5f), new Color(1f, 0.55f, 0.2f), 1.5f + 0.5f * f, s * 3f + 2f);
        FxFollower.Track(fx, handle.transform, 2.4f);
        return handle;
    }

    // Ribbon trail behind a moving object. It outlives the object and fades out.
    public static GameObject Trail(Transform parent, Color color, float width, float time = 0.3f) {
        GameObject go = new GameObject("Trail");
        if (parent != null) go.transform.SetParent(parent, false);
        Color c = FxAssets.SplitHdr(color, out float hdr);
        TrailRenderer tr = go.AddComponent<TrailRenderer>();
        tr.sharedMaterial = FxAssets.Additive(FxAssets.Beam, 1.3f * hdr);
        tr.time = Mathf.Max(0.02f, time);
        tr.widthMultiplier = width;
        tr.widthCurve = FxLooks.Curve(0f, 1f, 0.2f, 0.85f, 1f, 0f);
        Color mid = c, end = c;
        mid.a = c.a * 0.6f;
        end.a = 0f;
        tr.colorGradient = FxLooks.Ramp(new[] { c, mid, end }, new[] { 0f, 0.4f, 1f }, 0f, c.a, 0.4f, mid.a, 1f, 0f);
        tr.minVertexDistance = Mathf.Clamp(width * 0.5f, 0.05f, 0.3f);
        tr.numCapVertices = 3;
        tr.alignment = LineAlignment.View;
        tr.textureMode = LineTextureMode.Stretch;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;
        tr.sortingOrder = FxLayers.OrderGlow;
        if (parent != null) FxFollower.Track(go, parent, tr.time + 0.05f);
        return go;
    }

    // Looping particle emitter that follows `parent` (smoke trails, embers, sparkles). Bright colours glow
    // (additive); dark colours are drawn as smoke. It keeps emitting per metre too, so fast objects
    // leave no gaps, and it fades out after the parent is gone.
    public static GameObject Emitter(Transform parent, Color color, float size, float rate) {
        GameObject go = new GameObject("Emitter");
        if (parent != null) go.transform.SetParent(parent, false);
        float s = Mathf.Max(0.02f, size);
        Color c = FxAssets.SplitHdr(color, out float hdr);
        bool smoky = color.maxColorComponent < 0.55f;
        float life = smoky ? 1.1f : 0.6f;
        ParticleSystem ps = FxLayer.AddLoop(go, smoky ? FxLooks.Smoke() : FxLooks.TrailGlow(hdr), Mathf.Max(0f, rate), c);
        FxLayer.SetRanges(ps, life * 0.7f, life, s * 0.7f, s, 0.2f, 0.6f);
        var emission = ps.emission;
        emission.rateOverDistance = Mathf.Clamp(0.8f / s, 0.5f, 8f);
        var shape = ps.shape;
        shape.radius = s * 0.25f;
        ps.Play();
        if (parent != null) FxFollower.Track(go, parent, life + 0.1f);
        return go;
    }

    // ================================================================== lights, rings, text, screen
    public static void LightFlash(Vector3 pos, Color color, float intensity, float range, float duration) {
        if (!Valid(pos)) return;
        GameObject go = new GameObject("Flash");
        go.transform.position = pos + Vector3.back * 1.5f;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        go.AddComponent<FadeLight>().duration = Mathf.Max(0.02f, duration);
    }

    // Expanding ring in the XY plane facing the camera.
    public static void Ring(Vector3 pos, float radius, Color color, float duration, float width = 0.35f) {
        if (!Valid(pos) || radius <= 0f) return;
        new GameObject("Ring").AddComponent<RingWave>().Init(Front(pos), radius, color, duration, width);
    }

    // World-space text that pops in, rises and fades (damage numbers).
    public static void FloatingText(Vector3 pos, string text, Color color, float scale = 1f) {
        GameAssets assets = GameAssets.I;
        if (assets == null || assets.fontBold == null || string.IsNullOrEmpty(text) || !Valid(pos)) return;
        GameObject go = new GameObject("FloatingText");
        go.transform.position = new Vector3(pos.x, pos.y, Mathf.Min(pos.z, -2.5f));   // in front of the battle plane
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.font = assets.fontBold;
        if (assets.fontBoldOutline != null) t.fontSharedMaterial = assets.fontBoldOutline;
        t.text = text;
        t.color = color;
        t.fontSize = 11f;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.sortingOrder = 20;
        t.rectTransform.sizeDelta = new Vector2(12f, 3f);
        go.AddComponent<FloatUp>().Init(Mathf.Max(0.1f, scale));
    }

    public static void ScreenFlash(Color color, float duration) {
        if (duration <= 0f || color.a <= 0f) return;
        ScreenFlashOverlay.Show(color, duration);
    }

    public static LightningBolt Bolt(Vector3 from, Vector3 to, Color color, float width, float duration) {
        LightningBolt bolt = MakeBolt(from, to, color, width, duration);
        if (Valid(to)) {                                                // a bright spot where it strikes
            Color hot = Color.Lerp(FxAssets.SplitHdr(color, out _), Color.white, 0.5f);
            FxLayers.Flash.Emit(Front(to), Vector3.zero, Mathf.Clamp(width * 8f, 0.6f, 4f), 0.14f, hot);
        }
        return bolt;
    }

    // ================================================================== generic burst
    // A one-shot particle burst of its own (callers may tweak the returned system). Destroys itself.
    public static ParticleSystem Burst(Vector3 pos, int count, float speed, float size, float life, Color color,
                                       float gravity, bool additive, Texture2D tex = null) {
        GameObject go = new GameObject("Burst");
        go.transform.position = pos;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        count = Mathf.Clamp(count, 1, 200);
        life = Mathf.Max(0.05f, life);
        Color c = FxAssets.SplitHdr(color, out float hdr);
        var main = ps.main;
        main.loop = false;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = c;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.maxParticles = count;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.05f, size * 0.3f);
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, FxLooks.Curve(0f, 1f, 1f, 0.2f));
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FxLooks.Fade(0f, 1f, 0.6f, 0.85f, 1f, 0f));
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 1000f;
        limit.drag = 0.8f;
        limit.multiplyDragByParticleSize = false;
        limit.multiplyDragByParticleVelocity = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = additive ? FxAssets.Additive(tex, hdr) : FxAssets.Alpha(tex, hdr);
        r.sortingOrder = additive ? FxLayers.OrderGlow : FxLayers.OrderSmoke;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return ps;
    }

    // ================================================================== building blocks
    // Billowing puffs that cool from white-hot to dark smoke (the Fire layer's colour ramp).
    static void Fireball(Vector3 c, float s, int n, float speed) {
        float life = Mathf.Lerp(0.85f, 1.3f, s / 8f);
        for (int i = 0; i < n; i++) {
            Vector3 d = RandDir();
            Vector3 v = d * s * Random.Range(2f, 4f) * speed + Vector3.up * s * Random.Range(0.3f, 1f);
            FxLayers.Fire.Emit(c + d * s * Random.Range(0f, 0.35f), v, s * Random.Range(0.55f, 0.95f), Random.Range(0.5f, 0.9f) * life,
                               Color.white, Random.Range(-90f, 90f));
        }
    }

    static void SmokeCloud(Vector3 c, float s, int n, Color tint, float rise) {
        for (int i = 0; i < n; i++) {
            Vector3 d = RandDir();
            Vector3 v = d * s * Random.Range(0.5f, 1.4f) + Vector3.up * rise * Random.Range(0.8f, 2.2f);
            FxLayers.Smoke.Emit(c + d * s * Random.Range(0.1f, 0.5f), v, s * Random.Range(0.8f, 1.3f), Random.Range(1.6f, 2.8f),
                                Vary(tint, 0.15f), Random.Range(-30f, 30f));
        }
    }

    static void SparkSpray(Vector3 c, int n, float speed, Color color) {
        for (int i = 0; i < n; i++) {
            Vector3 v = RandDir(0.3f) * speed * Random.Range(0.45f, 1f) + Vector3.up * speed * 0.15f;
            FxLayers.Spark.Emit(c, v, Random.Range(0.05f, 0.11f), Random.Range(0.35f, 0.9f), color);
        }
    }

    static void Embers(Vector3 c, float s, int n) {
        for (int i = 0; i < n; i++) {
            Vector3 d = RandDir();
            FxLayers.Ember.Emit(c + d * s * Random.Range(0.1f, 0.6f), d * Random.Range(1f, 4f) + Vector3.up * Random.Range(1f, 3f),
                                Random.Range(0.07f, 0.15f), Random.Range(1.1f, 2.1f), new Color(1f, Random.Range(0.45f, 0.7f), 0.18f));
        }
    }

    static void GlowPuffs(Vector3 c, float s, int n, Color a, Color b) {
        for (int i = 0; i < n; i++) {
            Vector3 d = RandDir();
            FxLayers.Glow.Emit(c + d * s * 0.2f, d * s * Random.Range(1.5f, 3.5f), s * Random.Range(0.4f, 0.8f), Random.Range(0.4f, 0.75f),
                               Color.Lerp(a, b, Random.value));
        }
    }

    static void Stars(Vector3 c, float s, int n, Color a, Color b) {
        for (int i = 0; i < n; i++) {
            Vector3 d = RandDir(0.4f);
            FxLayers.Star.Emit(c + d * s * 0.15f, d * Random.Range(2.5f, 8f) * (0.7f + s * 0.1f), Random.Range(0.18f, 0.4f) * (0.8f + s * 0.08f),
                               Random.Range(0.5f, 1f), Color.Lerp(a, b, Random.value), Random.Range(-180f, 180f));
        }
    }

    // Glittering glass / ice sparkles.
    static void Glints(Vector3 c, float extent, int n) {
        for (int i = 0; i < n; i++) {
            Vector3 at = c + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0f) * extent;
            FxLayers.Star.Emit(at, RandDir(0.3f) * Random.Range(0.5f, 2.5f) + Vector3.up * 0.8f, Random.Range(0.12f, 0.28f),
                               Random.Range(0.4f, 0.9f), new Color(0.85f, 0.95f, 1f), Random.Range(-120f, 120f));
        }
    }

    static void Puffs(Vector3 c, float s, int n, Color tint) {
        for (int i = 0; i < n; i++) {
            Vector3 d = RandDir(0.3f);
            FxLayers.Dust.Emit(c + d * s * Random.Range(0f, 0.35f), d * s * Random.Range(0.5f, 1.3f) + Vector3.up * Random.Range(0.1f, 0.6f),
                               s * Random.Range(0.55f, 0.95f), Random.Range(0.9f, 1.6f), Vary(tint, 0.1f), Random.Range(-40f, 40f));
        }
    }

    static void ChunkSpray(FxLayer layer, Vector3 c, int n, float speed, float size) {
        for (int i = 0; i < n; i++) {
            Vector3 v = (RandDir(0.3f) + Vector3.up * 0.6f).normalized * speed * Random.Range(0.5f, 1f);
            Vector3 sz = new Vector3(Random.Range(0.6f, 1.3f), Random.Range(0.6f, 1.3f), Random.Range(0.6f, 1.3f)) * size;
            layer.EmitChunk(c + Random.insideUnitSphere * size, v, sz, Random.Range(1f, 1.8f));
        }
    }

    static LightningBolt MakeBolt(Vector3 from, Vector3 to, Color color, float width, float duration) {
        LightningBolt bolt = new GameObject("Bolt").AddComponent<LightningBolt>();
        bolt.Init(from, to, color, width, duration);
        return bolt;
    }

    // Shape of a chip: splinters for wood, chunks for stone, flakes for metal, thin shards for glass and ice.
    static Vector3 ChipSize(BlockMaterial m) {
        switch (m) {
            case BlockMaterial.Stone: return new Vector3(Random.Range(0.08f, 0.18f), Random.Range(0.07f, 0.16f), Random.Range(0.08f, 0.17f));
            case BlockMaterial.Metal: return new Vector3(Random.Range(0.06f, 0.14f), Random.Range(0.02f, 0.04f), Random.Range(0.06f, 0.14f));
            case BlockMaterial.Glass:
            case BlockMaterial.Ice: return new Vector3(Random.Range(0.08f, 0.18f), Random.Range(0.06f, 0.14f), 0.02f);
            default: return new Vector3(Random.Range(0.04f, 0.07f), Random.Range(0.04f, 0.07f), Random.Range(0.14f, 0.3f));
        }
    }

    // A lighter, greyer version of the material colour for its dust.
    static Color DustColor(BlockMaterial m) {
        Color c = Color.Lerp(BlockMats.Get(m).color, new Color(0.8f, 0.78f, 0.74f), 0.5f);
        c.a = 0.55f;
        return c;
    }

    // Random direction biased into the screen plane and towards the camera (never into the blocks).
    static Vector3 RandDir(float zScale = 0.35f) {
        Vector3 d = Random.onUnitSphere;
        d.z = -Mathf.Abs(d.z) * zScale;
        return d;
    }

    static Vector3 Front(Vector3 p) { return new Vector3(p.x, p.y, Mathf.Min(p.z, FrontZ)); }

    static Color Vary(Color c, float amount) {
        float k = Random.Range(1f - amount, 1f + amount);
        return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
    }

    static int Count(float n, int max) { return Mathf.Clamp(Mathf.RoundToInt(n), 1, max); }

    // Start speed that covers `dist` in `time` against linear drag: x(t) = v0 / drag * (1 - e^(-drag t)).
    static float ReachSpeed(float dist, float time, float drag) {
        return drag > 0f ? dist * drag / (1f - Mathf.Exp(-drag * time)) : dist / time;
    }

    static GameObject Child(GameObject parent, string name) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    // Box-shaped emission area at a local offset.
    static void Area(ParticleSystem ps, Vector3 box, Vector3 offset) {
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = box;
        shape.position = offset;
    }

    static bool Valid(Vector3 p) {
        return !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z)) && Mathf.Abs(p.x) < 1e4f && Mathf.Abs(p.y) < 1e4f && Mathf.Abs(p.z) < 1e4f;
    }
}

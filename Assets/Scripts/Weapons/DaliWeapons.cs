using UnityEngine;
using UnityEngine.Rendering;

// Dalí's weapons: surrealist art thrown at the enemy. Each weapon lives in its own file:
//   DaliWeaponsMustache.cs  Mustache Birds: the halves of his mustache fly off as two birds and burst on contact
//   DaliWeaponsAnteater.cs  Pet Anteater: burrows to the enemy and eats one block from below on each of his turns
//   DaliWeaponsClocks.cs    Melting Clocks: time melts and the battle rewinds to before the enemy's last move
//   DaliWeaponsSun.cs       Painted Sun: a sun painted over the enemy burns their commander every turn
// Shared effects are below, his synthesised sounds in DaliWeaponsSounds.cs, the shapes and textures (mustache,
// soft watches, sun) in World/DaliLookArt.cs.
static class DaliWeapons {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() {
        WeaponFactory.Register(WeaponId.MustacheBirds, MustacheBird.Fire, MustacheBird.Model);
        WeaponFactory.Register(WeaponId.Anteater, AnteaterBall.Fire, AnteaterBall.Model);
        WeaponFactory.Register(WeaponId.MeltingClocks, MeltingClock.Fire, MeltingClock.Model);
        WeaponFactory.Register(WeaponId.PaintedSun, PaintBall.Fire, PaintBall.Model);
    }

    // The enemy fortress (not defeated; with blocks left unless needBlocks is false) whose middle is nearest to x,
    // or null. The anteater needs blocks to eat; the painted sun only burns the commander.
    public static Fortress NearestEnemy(Fortress owner, float x, bool needBlocks = true) {
        if (BattleManager.I == null || owner == null) return null;
        Fortress best = null;
        float bestDistance = float.MaxValue;
        foreach (Fortress f in BattleManager.I.EnemiesOf(owner)) {
            if (f == null || (needBlocks && f.blocks.Count == 0) || (!needBlocks && !f.CommanderAlive)) continue;
            float d = Mathf.Abs(f.transform.position.x - x);
            if (d < bestDistance) { bestDistance = d; best = f; }
        }
        return best;
    }

    // Stops a projectile's body where it is: no more physics, it is moved by hand from now on.
    public static void Freeze(Projectile p) {
        Rigidbody rb = p.rb;
        if (rb != null && !rb.isKinematic) {
            rb.linearVelocity = Vector3.zero;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;   // the only mode kinematic bodies support
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
        }
        foreach (Collider c in p.GetComponentsInChildren<Collider>()) c.enabled = false;
    }
}

// Small effects shared by Dalí's weapons. Everything here cleans itself up.
static class DaliFx {
    public static readonly Color Earth = new Color(0.47f, 0.37f, 0.27f, 0.75f);
    static Mesh splatMesh;

    // Effects are drawn just in front of the blocks (see VFX.FrontZ).
    public static Vector3 Front(Vector3 p) { return new Vector3(p.x, p.y, Mathf.Min(p.z, VFX.FrontZ)); }

    // A point light riding along with a projectile, a little towards the camera so it lights the side we see.
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

    // Detaches a trail or emitter so it fades out on its own instead of vanishing with its projectile.
    public static void Release(GameObject fx) {
        if (fx == null) return;
        fx.transform.SetParent(null, true);
        float life = 1f;
        foreach (TrailRenderer tr in fx.GetComponentsInChildren<TrailRenderer>()) {
            tr.emitting = false;
            life = Mathf.Max(life, tr.time);
        }
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>()) {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            life = Mathf.Max(life, ps.main.startLifetime.constantMax);
        }
        Object.Destroy(fx, life + 0.1f);
    }

    // Black feathers bursting out and fluttering down.
    public static void Feathers(Vector3 pos, int count, float speed) {
        GameObject go = new GameObject("Feathers");
        go.transform.position = Front(pos);
        ParticleSystem ps = FeatherSystem(go, Mathf.Clamp(count, 1, 40));
        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.duration = 0.1f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed);
        main.stopAction = ParticleSystemStopAction.Destroy;
        ParticleSystem.EmissionModule em = ps.emission;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, 40)) });
        ps.Play();
    }

    // A few feathers shed while flying: a looping emitter that follows `parent` (Release it when done).
    public static GameObject FeatherTrail(Transform parent, float rate) {
        GameObject go = new GameObject("Feather Trail");
        go.transform.SetParent(parent, false);
        ParticleSystem ps = FeatherSystem(go, 20);
        ParticleSystem.MainModule main = ps.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = rate;
        ps.Play();
        return go;
    }

    // Feathers: small dark quills that spin, drift on the air (noise) and sink slowly.
    static ParticleSystem FeatherSystem(GameObject go, int max) {
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.26f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.07f, 0.06f, 0.06f), new Color(0.24f, 0.21f, 0.2f));
        main.gravityModifier = 0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 0f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;
        ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.limit = 1000f;
        drag.drag = 2.5f;                                   // air brakes them quickly
        drag.multiplyDragByParticleSize = false;
        drag.multiplyDragByParticleVelocity = false;
        ParticleSystem.RotationOverLifetimeModule spin = ps.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        ParticleSystem.NoiseModule flutter = ps.noise;
        flutter.enabled = true;
        flutter.strength = 0.7f;
        flutter.frequency = 0.6f;
        flutter.scrollSpeed = 0.4f;
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Mats.ParticleAlpha(DaliArt.Feather);
        r.sortingOrder = FxLayers.OrderSmoke;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    // Paint dripping off a flying paint ball: a looping emitter that follows `parent` (Release it when done).
    public static GameObject PaintDrips(Transform parent, Color color) {
        GameObject go = new GameObject("Paint Drips");
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
        main.startColor = color;
        main.gravityModifier = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 16f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Mats.ParticleAlpha(TexKit.SoftCircle);
        r.sortingOrder = FxLayers.OrderSmoke;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return go;
    }

    // A splat of paint: droplets flying off and a glossy blot left on whatever was hit (it moves with that block,
    // and shrinks away after a while).
    public static void Splat(Vector3 pos, Color color, Transform stuckTo) {
        VFX.Burst(Front(pos), 26, 6f, 0.18f, 0.9f, color, 1f, false, TexKit.SoftCircle);
        GameObject blot = ModelKit.MeshPart(null, SplatMesh(), Vector3.zero, Vector3.one * Random.Range(0.9f, 1.25f), Mats.Lit(color, 0.85f),
                                            new Vector3(0f, 0f, Random.Range(0f, 360f)), "Paint Splat");
        blot.transform.position = Front(pos) + Vector3.back * 0.02f;
        if (stuckTo != null) blot.transform.SetParent(stuckTo, true);
        ModelKit.Shadows(blot, false, false);
        DaliFader.Begin(blot, 2.5f, 0.6f);
    }

    // Earth thrown up by the anteater: dust and a few clods.
    public static void Dirt(Vector3 pos, float size) {
        VFX.Dust(pos, size, Earth);
        VFX.Burst(Front(pos), Mathf.RoundToInt(8f + size * 6f), 3.5f + size * 2f, 0.12f, 0.8f, new Color(0.3f, 0.22f, 0.15f), 1f, false, TexKit.Chip);
    }

    // An irregular blot facing the camera, about 1 m across: a ragged disc with a few longer splashes.
    static Mesh SplatMesh() {
        if (splatMesh != null) return splatMesh;
        MeshBuilder b = new MeshBuilder();
        const int n = 24;
        int c = b.Vertex(Vector3.zero, new Vector2(0.5f, 0.5f));
        int first = -1, prev = -1;
        for (int i = 0; i < n; i++) {
            float a = i * Mathf.PI * 2f / n;
            float r = 0.36f * (0.75f + 0.35f * TexKit.Hash(i, 1, 951)) * (i % 5 == 0 ? 1.45f : 1f);
            int v = b.Vertex(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f), new Vector2(0.5f, 0.5f));
            if (prev >= 0) b.Triangle(c, v, prev);
            else first = v;
            prev = v;
        }
        b.Triangle(c, first, prev);
        splatMesh = b.ToMesh("Paint Splat");
        return splatMesh;
    }
}

// Shrinks an object away after `delay` seconds, then destroys it (paint blots, a cancelled sun's last glow).
public class DaliFader : MonoBehaviour {
    float delay, duration, t;
    Vector3 scale;

    public static void Begin(GameObject go, float delay, float duration) {
        if (go == null) return;
        DaliFader f = go.GetComponent<DaliFader>();
        if (f == null) f = go.AddComponent<DaliFader>();
        f.delay = delay;
        f.duration = Mathf.Max(0.05f, duration);
        f.t = 0f;
        f.scale = go.transform.localScale;
    }

    void Update() {
        t += Time.deltaTime;
        if (t < delay) return;
        float k = Mathf.Clamp01((t - delay) / duration);
        transform.localScale = scale * (1f - Mathf.SmoothStep(0f, 1f, k));
        if (k >= 1f) Destroy(gameObject);
    }
}

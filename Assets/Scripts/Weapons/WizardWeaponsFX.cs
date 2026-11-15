using UnityEngine;
using UnityEngine.Rendering;

// Particle, light and motion helpers for the Wizard's spells. Every effect is configured in code and
// cleans itself up: looping emitters die with their parent or fade out through WizardFX.Release.
static class WizardFX {
    // Point lights sit a little towards the camera so they light the faces we actually see.
    public static readonly Vector3 LightOffset = new Vector3(0f, 0f, -1.2f);

    // A stopped particle system under `parent` (null = world) with the shared defaults.
    // Callers tune the modules they need, then call Play().
    public static ParticleSystem Create(Transform parent, string name, Texture2D tex, bool additive, int maxParticles, bool worldSpace = true) {
        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.maxParticles = maxParticles;
        main.startSpeed = 0f;
        main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // scaling a parent (portal, black hole) scales the effect
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 0f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = additive ? Mats.ParticleAdditive(tex) : Mats.ParticleAlpha(tex);
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    // Colour `from` -> `to` over the particle's life, fading out at the end.
    public static ParticleSystem.MinMaxGradient Fade(Color from, Color to) {
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                  new[] { new GradientAlphaKey(from.a, 0f), new GradientAlphaKey(to.a, 0.55f), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }

    // Fades in quickly and out slowly (smoke, glows, swirls).
    public static ParticleSystem.MinMaxGradient FadeInOut() {
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }

    static ParticleSystem.MinMaxCurve Curve(float from, float to) {
        return new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
    }

    // Flames licking off a moving object: the hot colour cools down while the flame shrinks and rises.
    public static ParticleSystem Flames(Transform parent, float size, float rate, Color hot, Color cool) {
        ParticleSystem ps = Create(parent, "Flames", TexKit.SoftCircle, true, 220);
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.15f;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = rate;
        em.rateOverDistance = 3f;          // keeps the trail continuous at high speed
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.radius = size * 0.3f;
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Fade(hot, cool);
        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = Curve(1f, 0.25f);
        ps.Play();
        return ps;
    }

    // Soft smoke left behind a moving object; it billows (grows) as it fades.
    public static ParticleSystem Smoke(Transform parent, float size, float rate, Color color) {
        ParticleSystem ps = Create(parent, "Smoke", TexKit.Smoke, false, 160);
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = color;
        main.gravityModifier = -0.04f;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = rate;
        em.rateOverDistance = 1.2f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.radius = size * 0.3f;
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeInOut();
        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = Curve(0.6f, 2.2f);
        ParticleSystem.RotationOverLifetimeModule rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = 0.6f;
        ps.Play();
        return ps;
    }

    // Tiny glowing sparks thrown off and drifting down.
    public static ParticleSystem Embers(Transform parent, float rate, Color color, float speed) {
        ParticleSystem ps = Create(parent, "Embers", TexKit.SoftCircle, true, 120);
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        main.startColor = color;
        main.gravityModifier = 0.25f;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = rate;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.radius = 0.25f;
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = 0.8f;
        noise.frequency = 1.5f;
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Fade(Color.white, new Color(1f, 1f, 1f, 0.8f));
        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = Curve(1f, 0.3f);
        ps.Play();
        return ps;
    }

    // A soft shimmering glow that stays centred on its parent (local space).
    public static ParticleSystem Halo(Transform parent, Color color, float size) {
        ParticleSystem ps = Create(parent, "Halo", TexKit.SoftCircle, true, 16, false);
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = 0.3f;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.8f, size);
        main.startColor = color;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 30f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.radius = 0.02f;
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeInOut();
        ps.Play();
        return ps;
    }

    // Particles born on a ring (a circle in the parent's XY plane) that orbit its centre and drift
    // inwards. Local space, so tilting or scaling the parent tilts or scales the whole swirl.
    public static ParticleSystem SwirlRing(Transform parent, string name, Texture2D tex, bool additive, float radius, float rate,
                                           Color color, float orbit, float inward) {
        ParticleSystem ps = Create(parent, name, tex, additive, 400, false);
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.38f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = color;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = rate;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0.2f;
        ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.orbitalZ = orbit;          // around the ring's own axis
        vel.radial = -inward;          // negative = towards the centre
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = FadeInOut();
        ps.Play();
        return ps;
    }

    // One-shot burst (impact sparks, puffs) that fades out and destroys itself.
    public static void Burst(Vector3 pos, int count, float speed, float size, float life, Color color, float gravity, Texture2D tex) {
        ParticleSystem ps = Create(null, "SpellBurst", tex, true, Mathf.Max(1, count));
        ps.transform.position = pos;
        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.duration = 0.1f;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = color;
        main.gravityModifier = gravity;
        ParticleSystem.EmissionModule em = ps.emission;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Fade(Color.white, new Color(1f, 1f, 1f, 0.7f));
        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = Curve(1f, 0.2f);
        ps.Play();
    }

    // Lets the effects of a vanishing projectile linger: detaches them, stops emitters and trails, fades
    // the lights, and destroys the lot once the last particles are gone.
    public static void Release(Transform fx, float life) {
        if (fx == null) return;
        fx.SetParent(null, true);
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>()) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        foreach (TrailRenderer tr in fx.GetComponentsInChildren<TrailRenderer>()) tr.emitting = false;
        foreach (WizardLight l in fx.GetComponentsInChildren<WizardLight>()) l.FadeOut(0.3f);
        Object.Destroy(fx.gameObject, life);
    }
}

// The point light of a spell: flickers while the spell lives and fades out when told to.
public class WizardLight : MonoBehaviour {
    public float intensity;        // before flicker and fade (spells may animate it)
    float flicker, seed, fade = 1f, fadeSpeed;
    Light lamp;

    public static WizardLight Add(Transform parent, Vector3 localPos, Color color, float intensity, float range, float flicker) {
        GameObject go = new GameObject("Light");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        WizardLight w = go.AddComponent<WizardLight>();
        w.lamp = l;
        w.intensity = intensity;
        w.flicker = flicker;
        w.seed = Random.value * 100f;
        return w;
    }

    public void FadeOut(float seconds) { fadeSpeed = 1f / Mathf.Max(0.01f, seconds); }

    void Update() {
        if (fadeSpeed > 0f) fade = Mathf.Max(0f, fade - fadeSpeed * Time.deltaTime);
        float noise = 1f + (Mathf.PerlinNoise(seed, Time.time * 13f) - 0.5f) * 2f * flicker;
        lamp.intensity = Mathf.Max(0f, intensity * noise * fade);
    }
}

// Spins a visual part (tumbling meteors, the rocks circling the meteor orb).
public class WizardSpin : MonoBehaviour {
    public Vector3 degreesPerSecond;

    void Update() { transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self); }
}

// Little arcs crackling over the lightning orb: a few line renderers re-shaped many times a second.
public class WizardArcs : MonoBehaviour {
    const int Points = 5;
    const float Interval = 0.04f;
    LineRenderer[] lines;
    float next;

    public static void Add(Transform parent, int count, Color color) {
        WizardArcs arcs = new GameObject("Arcs").AddComponent<WizardArcs>();
        arcs.transform.SetParent(parent, false);
        arcs.lines = new LineRenderer[count];
        Color tip = color;
        tip.a = 0.25f;
        for (int i = 0; i < count; i++) {
            LineRenderer lr = new GameObject("Arc").AddComponent<LineRenderer>();
            lr.transform.SetParent(arcs.transform, false);
            lr.useWorldSpace = false;
            lr.positionCount = Points;
            lr.sharedMaterial = Mats.ParticleAdditive(TexKit.SoftCircle);
            lr.startColor = Color.white;
            lr.endColor = tip;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            arcs.lines[i] = lr;
        }
    }

    void Update() {
        if (lines == null || Time.time < next) return;
        next = Time.time + Interval;
        foreach (LineRenderer lr in lines) {
            Vector3 dir = Random.onUnitSphere;
            dir.z *= 0.3f;                          // mostly in the plane we look at
            dir.Normalize();
            float reach = Random.Range(0.45f, 0.8f);
            for (int i = 0; i < Points; i++) {
                Vector3 p = dir * Mathf.Lerp(0.15f, reach, i / (float)(Points - 1));
                if (i > 0) p += Random.insideUnitSphere * 0.08f;
                lr.SetPosition(i, p);
            }
            lr.widthMultiplier = Random.Range(0.025f, 0.065f);
        }
    }
}

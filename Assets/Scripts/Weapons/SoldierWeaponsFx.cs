using UnityEngine;

// Weapon-specific effects for the Soldier's arsenal, configured in code: sparks for the burning fuse, a point
// light for the bomb's blinker, and helpers for splashes and for letting a trail fade out after its projectile
// is gone.
static class SoldierFx {
    public static readonly Color GroundDust = new Color(0.62f, 0.55f, 0.45f, 0.6f);

    // Dust colour for a hit on a block material.
    public static Color DustOf(BlockMaterial m) {
        Color c = BlockMats.Get(m).color;
        c.a = 0.6f;
        return c;
    }

    // Fizzing streaks of fire thrown off a burning fuse.
    public static ParticleSystem FuseSparks(Transform parent) {
        ParticleSystem ps = NewSystem("Fuse sparks", parent, Mats.ParticleAdditive(TexKit.SoftCircle), 60);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.08f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.55f, 0.15f));
        main.gravityModifier = 0.5f;
        var emission = ps.emission;
        emission.rateOverTime = 50f;
        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.04f;
        r.lengthScale = 1f;
        FadeOut(ps, 0.3f);
        ps.Play();
        return ps;
    }

    public static Light PointLight(Transform parent, Color color, float range) {
        GameObject go = new GameObject("Light");
        go.transform.SetParent(parent, false);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = 0f;
        l.shadows = LightShadows.None;
        return l;
    }

    // Lets a trail or emitter outlive its projectile: unparent it, stop emitting, remove it once faded.
    // Call before the projectile is destroyed (the hierarchy cannot change during destruction).
    public static void Release(GameObject fx, float fadeTime) {
        if (fx == null) return;
        fx.transform.SetParent(null, true);
        TrailRenderer tr = fx.GetComponent<TrailRenderer>();
        if (tr != null) tr.emitting = false;
        ParticleSystem ps = fx.GetComponent<ParticleSystem>();
        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Object.Destroy(fx, fadeTime);
    }

    public static void Splash(Vector3 pos, float size) {
        VFX.Splash(new Vector3(pos.x, World.WaterY, 0f), size);
        SFX.Play(SoundId.Splash, pos, Mathf.Clamp(0.4f + size * 0.3f, 0.4f, 1f));
    }

    // ------------------------------------------------------------------ helpers
    static ParticleSystem NewSystem(string name, Transform parent, Material mat, int maxParticles) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // it starts playing on creation
        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = maxParticles;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        return ps;
    }

    // Particles fade to transparent and scale from 1 to sizeAtEnd over their life.
    static void FadeOut(ParticleSystem ps, float sizeAtEnd) {
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = g;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, sizeAtEnd));
    }
}

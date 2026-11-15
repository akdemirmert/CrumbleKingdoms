using UnityEngine;
using UnityEngine.Rendering;

// How a particle layer looks and moves. Used by the shared layers and by looping emitters.
public class LayerLook {
    public Material material;
    public ParticleSystemRenderMode mode = ParticleSystemRenderMode.Billboard;
    public Mesh mesh;                            // Mesh mode only
    public int order;                            // sorting among transparent objects (higher = drawn later)
    public float gravity;                        // multiplier of Physics.gravity (negative = rises)
    public float drag;                           // linear drag in 1/s (0 = none)
    public AnimationCurve size;                  // size multiplier over the particle's life (null = constant)
    public Gradient color;                       // colour multiplier over life (null = constant)
    public float velocityScale, lengthScale = 1f; // stretched billboards: length = speed * velocityScale + size * lengthScale
    public float noise;                          // turbulence strength (0 = off)
    public bool youngestInFront;                 // alpha layers: fresh puffs cover old ones
    public bool chunks;                          // 3D size and rotation (tumbling mesh pieces)
    public float maxScreenSize = 1f;             // largest particle as a fraction of the screen
    public int maxParticles = 400;
}

// A shared world-space particle system for one look (fire, smoke, sparks...). Effects call Emit with
// per-particle values, so even a large explosion creates no new objects, and the particle cap of each
// layer bounds the cost. Layers live in the scene and are rebuilt on first use after a scene change.
public class FxLayer : MonoBehaviour {
    ParticleSystem ps;

    public static FxLayer Create(string name, LayerLook look, Transform parent) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // modules can only be changed while stopped
        Configure(ps, look);
        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startSpeed = 0f;
        var emission = ps.emission;
        emission.rateOverTime = 0f;                                     // particles only come from Emit
        var shape = ps.shape;
        shape.enabled = false;
        ps.Play();
        FxLayer layer = go.AddComponent<FxLayer>();
        layer.ps = ps;
        return layer;
    }

    // One billboard particle. rotation/spin in degrees (ignored by stretched billboards).
    public void Emit(Vector3 pos, Vector3 vel, float size, float life, Color color, float spin = 0f) {
        var p = new ParticleSystem.EmitParams {
            position = pos, velocity = vel, startSize = size, startLifetime = life, startColor = color,
            rotation = Random.Range(0f, 360f), angularVelocity = spin,
        };
        ps.Emit(p, 1);
    }

    // One tumbling mesh chunk with a non-uniform size.
    public void EmitChunk(Vector3 pos, Vector3 vel, Vector3 size, float life) {
        var p = new ParticleSystem.EmitParams {
            position = pos, velocity = vel, startSize3D = size, startLifetime = life, startColor = Color.white,
            rotation3D = Random.insideUnitSphere * 180f, angularVelocity3D = Random.insideUnitSphere * 600f,
        };
        ps.Emit(p, 1);
    }

    // Adds a looping emitter of its own (not shared) to `go`: effects attached to moving objects.
    // The caller sets the ranges/shape and then calls Play().
    public static ParticleSystem AddLoop(GameObject go, LayerLook look, float rate, Color color) {
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Configure(ps, look);
        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startColor = color;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;
        shape.randomDirectionAmount = 1f;
        if (look.mode == ParticleSystemRenderMode.Billboard) {
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);         // radians per second
        }
        return ps;
    }

    public static void SetRanges(ParticleSystem ps, float lifeMin, float lifeMax, float sizeMin, float sizeMax, float speedMin, float speedMax) {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
    }

    // Renderer and over-lifetime modules from a look.
    public static void Configure(ParticleSystem ps, LayerLook look) {
        var main = ps.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;   // never resume stale particles after being off-screen
        main.gravityModifier = look.gravity;
        main.maxParticles = look.maxParticles;
        main.startSize3D = look.chunks;
        main.startRotation3D = look.chunks;

        if (look.drag > 0f) {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 1000f;                                       // never reached: only the drag is wanted
            limit.drag = look.drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
        }
        if (look.size != null) {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, look.size);
        }
        if (look.color != null) {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(look.color);
        }
        if (look.noise > 0f) {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = look.noise;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.4f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
        }

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = look.mode;
        if (look.mode == ParticleSystemRenderMode.Mesh) {
            r.mesh = look.mesh;
            r.alignment = ParticleSystemRenderSpace.World;
            r.enableGPUInstancing = false;                             // the lit chunk shader has no particle instancing
        }
        if (look.mode == ParticleSystemRenderMode.Stretch) {
            r.velocityScale = look.velocityScale;
            r.lengthScale = look.lengthScale;
        }
        r.sharedMaterial = look.material;
        r.sortingOrder = look.order;
        r.sortMode = look.youngestInFront ? ParticleSystemSortMode.YoungestInFront : ParticleSystemSortMode.None;
        r.maxParticleSize = look.maxScreenSize;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }
}

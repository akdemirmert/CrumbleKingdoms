using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Painted Sun: Dalí throws a wobbling ball of golden paint. Where it splats, a painted sun rises over the nearest
// enemy fortress and the whole arena lights up in warm sunshine (PaintedSunEffect, SunlitArena). At the start of
// each of that fortress's next four turns the sun flares: a shaft of sunlight burns its commander for 9-13
// (sunlight ignores walls) and sets one of its wooden blocks alight. Then the light fades back.
public class PaintBall : Projectile {
    const float Radius = 0.26f, Mass = 1f;

    Transform blob;
    GameObject trail, drips;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        DaliSounds.Play(DaliSound.Splat, origin, 0.35f, 1.5f);
        WeaponFactory.Spawn<PaintBall>("Paint Ball", owner, def, origin, velocity);
    }

    // Weapon icon: the painted sun, with the brush that painted it.
    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Painted Sun", parent);
        Transform r = root.transform;
        ModelKit.MeshPart(r, PaintedSunEffect.IconRays(), Vector3.zero, Vector3.one, DaliArt.SunRay, default, "Rays");
        ModelKit.MeshPart(r, WorldMeshes.Disc(32, 0.3f), new Vector3(0f, 0f, -0.01f), Vector3.one, DaliArt.SunDisc, default, "Disc");
        Transform brush = PaintedSunEffect.Brush(r, DaliArt.Paint).transform;
        brush.localPosition = new Vector3(0.15f, -0.05f, -0.08f);
        brush.localRotation = Quaternion.Euler(0f, 0f, -135f);
        brush.localScale = Vector3.one * 0.35f;
        return root;
    }

    protected override void OnLaunch(Vector3 velocity) {
        blob = ModelKit.Root("Blob", transform).transform;
        ModelKit.MeshPart(blob, WorldMeshes.Ball(Radius), Vector3.zero, Vector3.one, Mats.Lit(DaliArt.Paint, 0.9f), default, "Paint");
        ModelKit.MeshPart(blob, WorldMeshes.Ball(Radius * 0.5f), new Vector3(-0.1f, 0.08f, -0.12f), Vector3.one, Mats.Lit(new Color(1f, 0.52f, 0.14f), 0.9f), default, "Swirl");
        gameObject.AddComponent<SphereCollider>().radius = Radius;
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;   // the blob wobbles, the body does not turn
        trail = VFX.Trail(transform, new Color(1f, 0.72f, 0.2f, 0.8f), 0.32f, 0.3f);
        drips = DaliFx.PaintDrips(transform, DaliArt.Paint);
        DaliFx.Glow(transform, new Color(1f, 0.8f, 0.4f), 1.2f, 3f);
    }

    // Squash and stretch along the flight, with a jelly wobble.
    void Update() {
        if (finished) return;
        Vector3 v = Velocity;
        if (v.sqrMagnitude > 0.01f) blob.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
        float stretch = Mathf.Clamp(v.magnitude / 25f, 0f, 0.35f), wobble = 0.07f * Mathf.Sin(Time.time * 18f);
        blob.localScale = new Vector3(1f + stretch + wobble, 1f - stretch * 0.5f - wobble, 1f - stretch * 0.5f);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished || transform.position.y > World.WaterY) return;
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);
        VFX.Splash(p, 0.9f);
        Splat(p, null);                                                  // paint on water counts too
    }

    void OnCollisionEnter(Collision c) {
        if (finished) return;
        Block block = c.collider.GetComponentInParent<Block>();
        Splat(c.contactCount > 0 ? c.GetContact(0).point : transform.position, block != null ? block.transform : null);
    }

    protected override void OnLost() { End(); }

    // A splat (the blot sticks to the block it hit), then the sun goes up over the nearest enemy.
    void Splat(Vector3 p, Transform stuckTo) {
        p.z = 0f;
        RaiseImpact(p);
        DaliFx.Splat(p, DaliArt.Paint, stuckTo);
        DaliSounds.Play(DaliSound.Splat, p, 0.9f);
        BattleCamera.Shake(0.12f);
        Fortress target = DaliWeapons.NearestEnemy(owner, p.x, false);
        if (target != null) PaintedSunEffect.Paint(owner, target, def);
        End();
    }

    void End() {
        DaliFx.Release(trail);
        DaliFx.Release(drips);
        trail = drips = null;
        Finish(true);
    }
}

// The painted sun over an enemy fortress (see PaintBall). It shows as sunshine over the whole arena (SunlitArena)
// while it lasts; at the start of each of the target's turns it flares and burns, four times, then it is gone.
// A time rewind or the end of the battle (Cancel) ends it at once.
public class PaintedSunEffect : TurnEffect {
    const int Flares = 4;
    const int Rays = 14, RaySteps = 4;                // the weapon icon's rays
    const float BeamZ = -1.2f;                        // shafts of sunlight are drawn in front of the walls
    const float IgniteSeconds = 5f;
    static readonly Color Gold = WeaponDefs.Hex("FFC93C");
    static Mesh iconRays;

    WeaponDef def;
    int flaresLeft = Flares;
    bool ended;

    public bool Shining => !ended;
    public override int RewindState { get => flaresLeft; set => flaresLeft = value; }
    public override string StatusText => ended ? null : "Sun " + flaresLeft;
    public override Color StatusColor => Gold;

    public static void Paint(Fortress owner, Fortress target, WeaponDef def) {
        PaintedSunEffect s = new GameObject("Painted Sun").AddComponent<PaintedSunEffect>();
        s.Init(owner, target);
        s.def = def;
        s.StartCoroutine(s.Rise());
    }

    // The sun comes out: a warm flash and the arena brightens. The turn waits until it has.
    IEnumerator Rise() {
        TurnActor actor = gameObject.AddComponent<TurnActor>();
        SunlitArena.Ensure();
        DaliSounds.Play(DaliSound.Shimmer, target.GetBounds().center, 0.9f);
        VFX.ScreenFlash(new Color(1f, 0.86f, 0.5f, 0.3f), 0.5f);
        yield return new WaitForSeconds(SunlitArena.FadeIn);
        if (actor != null) Destroy(actor);
    }

    // ------------------------------------------------------------------ every turn
    public override IEnumerator OnTurnStart(Fortress shooter) {
        if (ended) yield break;
        if (target == null || target.defeated || flaresLeft <= 0) {
            End();
            yield break;
        }
        if (shooter != target) yield break;
        flaresLeft--;
        SunlitArena.Flare();
        DaliSounds.Play(DaliSound.SunFlare, target.GetBounds().center, 0.9f);
        VFX.ScreenFlash(new Color(1f, 0.85f, 0.45f, 0.22f), 0.35f);
        yield return new WaitForSeconds(0.3f);
        if (this == null) yield break;
        Burn();
        yield return new WaitForSeconds(0.8f);                           // let it be seen before the target aims
        if (this != null && flaresLeft <= 0) End();
    }

    // A shaft of sunlight on the commander (straight through the roof: sunlight ignores walls) and another on one
    // random flammable block, which catches fire.
    void Burn() {
        Commander c = target.commander;
        if (c != null && c.IsAlive) {
            float typical = def != null ? def.damage : 11f;
            float amount = Mathf.Round(Random.Range(typical - 2f, typical + 2f));
            Vector3 at = c.AimPoint;
            DaliSunBeam.Create(SkyAbove(at), new Vector3(at.x, at.y, BeamZ), 0.7f, 0.6f);
            Destroy(VFX.Fire(c.transform, Vector3.up * 0.9f, 0.8f), 1.4f);
            SFX.Play(SoundId.FireIgnite, at, 0.7f);
            c.ApplyDamage(new DamageInfo(amount, DamageType.Fire, at, owner));
        }
        Block wood = RandomFlammable();
        if (wood != null) {
            Vector3 p = wood.transform.position;
            DaliSunBeam.Create(SkyAbove(p), new Vector3(p.x, p.y, BeamZ), 0.4f, 0.5f);
            wood.Ignite(IgniteSeconds, owner);
        }
    }

    // Where a shaft of sunlight onto `p` starts: high up, slanting in from the side the arena's sun shines from.
    static Vector3 SkyAbove(Vector3 p) {
        Light sun = RenderSettings.sun;
        float side = sun != null && sun.transform.forward.x > 0f ? -1f : 1f;
        return new Vector3(p.x + side * 6f, p.y + 16f, BeamZ);
    }

    Block RandomFlammable() {
        List<Block> wood = new List<Block>();
        foreach (Block b in target.blocks) if (b != null && b.IsAlive && b.def.flammable && !b.IsBurning) wood.Add(b);
        return wood.Count > 0 ? wood[Random.Range(0, wood.Count)] : null;
    }

    // Its work done (or its fortress fallen): the sunshine fades back (SunlitArena follows Shining).
    void End() {
        ended = true;
        Destroy(gameObject);
    }

    public override void Cancel() {
        ended = true;
        base.Cancel();
    }

    // Each ray is a curved, tapering brush stroke (a strip of RaySteps quads) from just inside the rim; long and
    // short rays alternate and sway a little, like flames. reveal: how much of each ray is painted (0..1).
    static void FillRays(Vector3[] v, float radius, float length, float[] reveal, float time, float seed) {
        for (int k = 0; k < Rays; k++) {
            float a = (k + 0.5f) * Mathf.PI * 2f / Rays;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), side = new Vector2(dir.y, -dir.x);
            bool longRay = k % 2 == 0;
            float len = length * (longRay ? 1f : 0.62f) * reveal[k];
            float width = radius * (longRay ? 0.2f : 0.15f) * Mathf.Min(1f, reveal[k] * 4f);
            float bend = (longRay ? 0.22f : -0.22f) * len * Mathf.Sin(time * 1.4f + k * 0.7f + seed);
            for (int i = 0; i <= RaySteps; i++) {
                float t = i / (float)RaySteps;
                Vector2 c = dir * (radius * 0.9f + len * t) + side * (Mathf.Sin(t * Mathf.PI) * bend);
                float w = width * Mathf.Pow(1f - t, 0.8f);
                int o = (k * (RaySteps + 1) + i) * 2;
                v[o] = new Vector3(c.x - side.x * w, c.y - side.y * w, 0f);
                v[o + 1] = new Vector3(c.x + side.x * w, c.y + side.y * w, 0f);
            }
        }
    }

    static int[] RayTriangles() {
        int[] t = new int[Rays * RaySteps * 6];
        int q = 0;
        for (int k = 0; k < Rays; k++)
            for (int i = 0; i < RaySteps; i++) {
                int o = (k * (RaySteps + 1) + i) * 2;                 // left, right of step i; step i + 1 follows (facing -z)
                t[q++] = o; t[q++] = o + 2; t[q++] = o + 3;
                t[q++] = o; t[q++] = o + 3; t[q++] = o + 1;
            }
        return t;
    }

    static Vector2[] RayUVs() {
        Vector2[] uv = new Vector2[Rays * (RaySteps + 1) * 2];
        for (int k = 0; k < Rays; k++)
            for (int i = 0; i <= RaySteps; i++) {
                int o = (k * (RaySteps + 1) + i) * 2;
                uv[o] = new Vector2(0f, i / (float)RaySteps);
                uv[o + 1] = new Vector2(1f, i / (float)RaySteps);
            }
        return uv;
    }

    // A small finished sun's rays, for the weapon icon.
    public static Mesh IconRays() {
        if (iconRays != null) return iconRays;
        float[] full = new float[Rays];
        for (int i = 0; i < Rays; i++) full[i] = 1f;
        Vector3[] v = new Vector3[Rays * (RaySteps + 1) * 2];
        FillRays(v, 0.3f, 0.26f, full, 0f, 0.6f);
        iconRays = new Mesh { name = "Sun Rays" };
        iconRays.vertices = v;
        iconRays.uv = RayUVs();
        iconRays.triangles = RayTriangles();
        iconRays.RecalculateNormals();
        iconRays.RecalculateBounds();
        return iconRays;
    }

    // A big floating paintbrush: loaded bristles at the origin, a gold ferrule and a red lacquered handle up +y.
    public static GameObject Brush(Transform parent, Color paint) {
        GameObject root = ModelKit.Root("Paintbrush", parent);
        Transform r = root.transform;
        ModelKit.MeshPart(r, WorldMeshes.Frustum(8, 0.02f, 0.11f, 0.32f), Vector3.zero, Vector3.one, Mats.Lit(paint, 0.7f), default, "Bristles");
        ModelKit.MeshPart(r, WorldMeshes.Frustum(8, 0.11f, 0.08f, 0.16f), new Vector3(0f, 0.32f, 0f), Vector3.one, DaliArt.GoldMetal, default, "Ferrule");
        ModelKit.MeshPart(r, WorldMeshes.Frustum(8, 0.06f, 0.035f, 1.3f), new Vector3(0f, 0.48f, 0f), Vector3.one, Mats.Lit(new Color(0.5f, 0.12f, 0.08f), 0.75f), default, "Handle");
        ModelKit.Shadows(root, false, false);
        return root;
    }
}

// A shaft of painted sunlight from the sun to what it burns; it narrows and fades.
public class DaliSunBeam : MonoBehaviour {
    LineRenderer line;
    float width, life, t;

    public static void Create(Vector3 from, Vector3 to, float width, float life) {
        GameObject go = new GameObject("Sun Beam");
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.sharedMaterial = FxAssets.Additive(FxAssets.Beam, 2f);
        lr.widthMultiplier = width;
        lr.startColor = new Color(1f, 0.92f, 0.6f, 1f);
        lr.endColor = new Color(1f, 0.6f, 0.2f, 1f);
        lr.numCapVertices = 4;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sortingOrder = FxLayers.OrderGlow;
        DaliSunBeam beam = go.AddComponent<DaliSunBeam>();
        beam.line = lr;
        beam.width = width;
        beam.life = Mathf.Max(0.05f, life);
        VFX.Sparks(to, new Color(1f, 0.8f, 0.35f), 14);
    }

    void Update() {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / life);
        line.widthMultiplier = width * (1f - k * k);
        if (k >= 1f) Destroy(gameObject);
    }
}

// While any painted sun shines, the whole arena is bathed in warm sunshine: the key light grows stronger and
// warmer, the ambient light brighter and the exposure a little higher (night turns to day). It eases in and out,
// pulses when a sun flares, and puts the exact original lighting back once no sun is left.
public class SunlitArena : MonoBehaviour {
    public const float FadeIn = 0.9f;
    const float FadeOut = 1.2f;
    const float Exposure = 0.22f;
    static readonly Color SunColor = new Color(1f, 0.93f, 0.78f), Tint = new Color(1f, 0.95f, 0.84f);   // warm sunshine
    static readonly Color Sky = new Color(0.95f, 0.88f, 0.74f), Equator = new Color(0.9f, 0.78f, 0.6f), Ground = new Color(0.55f, 0.44f, 0.3f);
    static SunlitArena instance;

    Light sun;
    ColorAdjustments grading;
    Color baseColor, baseSky, baseEquator, baseGround;
    Color baseFilter;
    float baseIntensity, baseExposure, level, flare;

    public static void Ensure() {
        if (instance == null) instance = new GameObject("Sunlit Arena").AddComponent<SunlitArena>();
    }

    public static void Flare() {
        if (instance != null) instance.flare = 1f;
    }

    void Awake() {
        sun = RenderSettings.sun;
        if (sun != null) {
            baseColor = sun.color;
            baseIntensity = sun.intensity;
        }
        baseSky = RenderSettings.ambientSkyColor;
        baseEquator = RenderSettings.ambientEquatorColor;
        baseGround = RenderSettings.ambientGroundColor;
        foreach (Volume v in FindObjectsByType<Volume>())
            if (v.isGlobal && v.profile != null && v.profile.TryGet(out grading)) break;
        if (grading != null) {
            baseExposure = grading.postExposure.value;
            baseFilter = grading.colorFilter.value;
        }
    }

    void Update() {
        bool shining = false;
        foreach (TurnEffect e in TurnEffect.All)
            if (e is PaintedSunEffect s && s.Shining) { shining = true; break; }
        level = Mathf.MoveTowards(level, shining ? 1f : 0f, Time.deltaTime / (shining ? FadeIn : FadeOut));
        flare = Mathf.MoveTowards(flare, 0f, Time.deltaTime * 1.5f);
        if (!shining && level <= 0f) {
            Apply(0f, 0f);
            Destroy(gameObject);
            return;
        }
        Apply(Mathf.SmoothStep(0f, 1f, level), flare);
    }

    // k = 0: the arena's own lighting, exactly; k = 1: full sunshine. f: a flare's extra pulse.
    void Apply(float k, float f) {
        if (sun != null) {
            sun.color = Color.Lerp(baseColor, SunColor, k);
            sun.intensity = Mathf.Lerp(baseIntensity, Mathf.Max(baseIntensity * 1.35f, 1.75f), k) * (1f + 0.2f * f * k);
        }
        RenderSettings.ambientSkyColor = Color.Lerp(baseSky, Sky, 0.9f * k);
        RenderSettings.ambientEquatorColor = Color.Lerp(baseEquator, Equator, 0.9f * k);
        RenderSettings.ambientGroundColor = Color.Lerp(baseGround, Ground, 0.9f * k);
        if (grading != null) {
            grading.postExposure.value = baseExposure + Exposure * k + 0.15f * f * k;
            grading.colorFilter.value = Color.Lerp(baseFilter, baseFilter * Tint, k);
        }
    }

    void OnDestroy() {
        if (instance == this) instance = null;
    }
}

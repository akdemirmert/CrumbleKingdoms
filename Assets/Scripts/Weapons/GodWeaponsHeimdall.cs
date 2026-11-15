using UnityEngine;
using UnityEngine.Rendering;

// Heimdall: Gjallarhorn. The thrown horn trails the rainbow bridge Bifröst. When it touches anything, or when
// the player clicks, it sounds: a shockwave that hurls every body around it away (the "pure physics" weapon)
// and shatters glass and ice.
public class HeimdallHorn : Projectile {
    const float Mass = 2.5f;
    const float Upward = 0.45f;            // lifts what the wave pushes, so towers tip over instead of sliding
    const float BrittleMultiplier = 4f;    // glass and ice ring with the horn and shatter

    // Centre line of the horn: a crescent from the mouthpiece (back, -X) to the bell (front, +X).
    static readonly Vector3 MouthEnd = new Vector3(-0.46f, 0.1f, 0f);
    static readonly Vector3 Bend = new Vector3(-0.02f, -0.26f, 0f);
    static readonly Vector3 BellEnd = new Vector3(0.44f, 0.15f, 0f);
    static readonly float[] BandStations = { 0.12f, 0.42f, 0.72f };

    Transform visual;
    GameObject trail, sparkle;
    bool sounded;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        SFX.Play(SoundId.MagicCast, origin, 0.55f, 0.9f);
        VFX.MagicBurst(origin, GodKit.Gold, 0.5f);
        WeaponFactory.Spawn<HeimdallHorn>("Gjallarhorn", owner, def, origin, velocity);
    }

    protected override void OnLaunch(Vector3 velocity) {
        visual = Model(transform).transform;
        gameObject.AddComponent<SphereCollider>().radius = 0.3f;
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;   // only the model turns
        trail = RainbowTrail(transform);
        sparkle = VFX.Emitter(transform, GodKit.Gold * 1.5f, 0.1f, 20f);
        GodKit.Glow(transform, GodKit.Gold, 3f, 5f);
        FaceFlight(velocity);
    }

    void Update() { if (!sounded) FaceFlight(Velocity); }

    // Bell first along the flight, kept upright whichever way it flies.
    void FaceFlight(Vector3 v) {
        if (v.sqrMagnitude < 0.01f) return;
        float pitch = Mathf.Atan2(v.y, Mathf.Abs(v.x)) * Mathf.Rad2Deg;
        visual.rotation = Quaternion.Euler(0f, v.x < 0f ? 180f : 0f, pitch);
    }

    protected override void OnActivate() { Sound(transform.position); }

    void OnCollisionEnter(Collision c) {
        Sound(c.contactCount > 0 ? c.GetContact(0).point : transform.position);
    }

    // The horn sounds once, from whichever comes first: a contact or the click.
    void Sound(Vector3 p) {
        if (sounded || finished) return;
        sounded = true;
        RaiseImpact(p);
        Shockwave(p);
        Effects(p);
        GodKit.Release(trail);
        GodKit.Release(sparkle);
        Finish(true);
    }

    // Pushes every body in range away (heavier ones move less) and hurts them with falloff.
    void Shockwave(Vector3 p) {
        Explosion.Push(p, def.radius, def.force, Upward, out var hits);
        foreach (var h in hits) {
            if (h.target == null || !h.target.IsAlive) continue;
            Block b = h.target as Block;
            bool brittle = b != null && (b.material == BlockMaterial.Glass || b.material == BlockMaterial.Ice);
            Vector3 dir = h.point - p;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.up;
            float damage = def.damage * h.falloff * (brittle ? BrittleMultiplier : 1f);
            h.target.ApplyDamage(new DamageInfo(damage, DamageType.Explosion, h.point, owner, dir * def.force * h.falloff));
        }
    }

    // Rainbow rings rippling out one after another, dust kicked up along the ground, a golden flash.
    void Effects(Vector3 p) {
        float radius = def.radius;
        for (int i = 0; i < GodKit.Rainbow.Length; i++) {
            Color c = GodKit.Rainbow[i];
            float ringRadius = radius * (0.5f + 0.1f * i);
            Runner.Delay(i * 0.06f, () => VFX.Ring(p, ringRadius, c, 0.55f, 0.5f));
        }
        for (int i = -4; i <= 4; i++) {
            Vector3 from = new Vector3(p.x + i * radius / 4f, p.y + 2f, 0f);
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, radius, Layers.GroundMask | Layers.BlockMask, QueryTriggerInteraction.Ignore))
                continue;
            Vector3 at = hit.point;
            float size = 1.6f - Mathf.Abs(i) * 0.15f;
            Runner.Delay(Mathf.Abs(i) * 0.05f, () => VFX.Dust(at, size));
        }
        VFX.MagicBurst(p, GodKit.Gold, 1.3f);
        VFX.LightFlash(p, GodKit.Gold, 14f, radius * 3f, 0.5f);
        VFX.ScreenFlash(new Color(1f, 0.9f, 0.55f, 0.35f), 0.25f);
        SFX.Play(SoundId.Horn, p, 1f, 0.85f);
        BattleCamera.Shake(0.85f);
    }

    // The Bifröst behind the horn: a ribbon striped red to violet across its width, fading along its length.
    static GameObject RainbowTrail(Transform parent) {
        GameObject go = new GameObject("Bifrost");
        go.transform.SetParent(parent, false);
        TrailRenderer tr = go.AddComponent<TrailRenderer>();
        Color[] rainbow = GodKit.Rainbow;
        Color[] stripes = new Color[rainbow.Length + 2];
        rainbow.CopyTo(stripes, 1);
        stripes[0] = Clear(rainbow[0]);
        stripes[stripes.Length - 1] = Clear(rainbow[rainbow.Length - 1]);
        tr.sharedMaterial = Mats.ParticleAlpha(TexKit.Gradient(stripes));
        tr.time = 0.7f;
        tr.minVertexDistance = 0.1f;
        tr.widthMultiplier = 0.6f;
        tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.5f);
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = fade;
        tr.shadowCastingMode = ShadowCastingMode.Off;
        tr.receiveShadows = false;
        return go;
    }

    static Color Clear(Color c) { return new Color(c.r, c.g, c.b, 0f); }

    // ------------------------------------------------------------------ model
    static Vector3 HornPoint(float s) { return GodKit.Bezier(MouthEnd, Bend, BellEnd, s); }
    static Vector3 HornTangent(float s) { return ((1f - s) * (Bend - MouthEnd) + s * (BellEnd - Bend)).normalized; }
    static float HornRadius(float s) { return 0.024f + 0.05f * s + 0.085f * s * s * s * s; }   // flares into the bell

    // A curved ivory horn with gold bands, a gold mouthpiece and golden light glowing in the bell.
    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Gjallarhorn", parent);
        Transform r = root.transform;
        Material ivory = Mats.Lit(new Color(0.94f, 0.88f, 0.74f), 0.45f);
        Material gold = GodKit.GoldMetal;
        Material glow = Mats.Emissive(GodKit.Gold, GodKit.Gold * 2.5f);

        const int N = 12;
        Vector3[] path = new Vector3[N];
        float[] radii = new float[N];
        for (int i = 0; i < N; i++) {
            float s = i / (N - 1f);
            path[i] = HornPoint(s);
            radii[i] = HornRadius(s);
        }
        ModelKit.MeshPart(r, GodKit.Tube("gjallarhorn", path, radii, 14), Vector3.zero, Vector3.one, ivory, default, "Horn");

        foreach (float s in BandStations) {
            Vector3 c = HornPoint(s), t = HornTangent(s);
            GodKit.Rod(r, c - t * 0.018f, c + t * 0.018f, HornRadius(s) * 2.35f, gold);
        }
        Vector3 t0 = HornTangent(0f);
        GodKit.Rod(r, MouthEnd - t0 * 0.08f, MouthEnd + t0 * 0.01f, 0.06f, gold);                   // mouthpiece
        Vector3 bell = HornPoint(1f), tb = HornTangent(1f);
        GodKit.Rod(r, bell - tb * 0.045f, bell, HornRadius(1f) * 2.15f, gold);                       // rim of the bell
        ModelKit.MeshPart(r, MeshKit.Cone(14, HornRadius(1f) * 0.95f, 0.12f), bell + tb * 0.003f, Vector3.one, glow,
                          Quaternion.FromToRotation(Vector3.up, -tb).eulerAngles, "Bell glow");     // its base faces out of the bell
        return root;
    }
}

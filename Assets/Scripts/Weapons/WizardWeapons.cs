using UnityEngine;

// The Wizard's four spells: Fireball, Lightning Orb, Meteor Orb and Void Orb.
// This file registers them with WeaponFactory, builds their models (also used for the weapon icons) and
// holds what they share: the orb flight (WizardOrb) and a few gameplay helpers. Each spell lives in its
// own WizardWeapons<Spell>.cs file; WizardWeaponsFX.cs has the particle, light and motion helpers.
static class WizardWeapons {
    public static readonly Color StormColor = new Color(0.55f, 0.85f, 1f);
    public static readonly Color EmberColor = new Color(1f, 0.62f, 0.22f);
    public static readonly Color VoidColor = new Color(0.62f, 0.32f, 1f);

    static readonly Collider[] near = new Collider[64];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() {
        WeaponFactory.Register(WeaponId.Fireball, (o, d, p, v) => WeaponFactory.Spawn<FireballOrb>("Fireball", o, d, p, v), FireballModel);
        WeaponFactory.Register(WeaponId.Lightning, (o, d, p, v) => WeaponFactory.Spawn<LightningOrb>("Lightning Orb", o, d, p, v), LightningModel);
        WeaponFactory.Register(WeaponId.MeteorOrb, (o, d, p, v) => WeaponFactory.Spawn<MeteorOrb>("Meteor Orb", o, d, p, v), MeteorOrbModel);
        WeaponFactory.Register(WeaponId.VoidOrb, (o, d, p, v) => WeaponFactory.Spawn<VoidOrb>("Void Orb", o, d, p, v), VoidOrbModel);
    }

    // ------------------------------------------------------------------ gameplay helpers
    public static bool IsFriend(Fortress a, Fortress b) { return a != null && b != null && a.team == b.team; }

    // Where bolts and sparks should land on a block or commander.
    public static Vector3 PointOf(IDamageable d) {
        if (d is Commander cm) return cm.AimPoint;
        return ((Component)d).transform.position;
    }

    // Sets flammable blocks of the other teams around p on fire (each with the given chance) and,
    // if asked, sets their commanders burning too. Fire then spreads by itself (Block.SpreadFire).
    public static void SetAblaze(Vector3 p, float reach, Fortress source, float chance, float seconds, bool burnCommanders) {
        int n = Physics.OverlapSphereNonAlloc(p, reach, near, Layers.BlockMask | Layers.CommanderMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++) {
            IDamageable d = near[i] != null ? near[i].GetComponentInParent<IDamageable>() : null;
            if (d == null || !d.IsAlive || IsFriend(d.Owner, source) || Random.value > chance) continue;
            if (d is Block b && b.def.flammable) b.Ignite(seconds, source);
            else if (burnCommanders && d is Commander cm && Explosion.Cover(p, cm.AimPoint) > 0.99f) WizardBurn.Apply(cm, source);   // walls shield him
        }
    }

    // ------------------------------------------------------------------ models (+X = forward, ~1 m across)
    public static void FireCore(Transform parent) {
        GameObject core = ModelKit.Part(parent, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.56f,
            Mats.Emissive(new Color(1f, 0.8f, 0.4f), new Color(6f, 2.9f, 0.7f)));
        ModelKit.Shadows(core, false, false);
    }

    static GameObject FireballModel(Transform parent) {
        GameObject root = ModelKit.Root("Fireball", parent);
        FireCore(root.transform);
        // three flame tongues streaming backwards (cones point along +Y, turned to -X)
        Material flame = Mats.Emissive(new Color(1f, 0.42f, 0.08f), new Color(4f, 1.1f, 0.15f));
        Mesh cone = MeshKit.Cone(7, 0.2f, 0.62f);
        ModelKit.MeshPart(root.transform, cone, new Vector3(-0.08f, 0.1f, 0.06f), Vector3.one, flame, new Vector3(0f, 0f, 76f), "Flame");
        ModelKit.MeshPart(root.transform, cone, new Vector3(-0.08f, -0.1f, 0.06f), Vector3.one * 0.85f, flame, new Vector3(0f, 0f, 104f), "Flame");
        ModelKit.MeshPart(root.transform, cone, new Vector3(-0.05f, 0f, 0.1f), Vector3.one * 1.3f, flame, new Vector3(0f, 0f, 90f), "Flame");
        ModelKit.Shadows(root, false, false);
        return root;
    }

    public static void LightningCore(Transform parent) {
        GameObject core = ModelKit.Part(parent, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.5f,
            Mats.Emissive(new Color(0.65f, 0.9f, 1f), new Color(2.2f, 4.6f, 6.5f)));
        ModelKit.Shadows(core, false, false);
    }

    static GameObject LightningModel(Transform parent) {
        GameObject root = ModelKit.Root("Lightning Orb", parent);
        LightningCore(root.transform);
        // a zig-zag bolt across the front of the orb
        Material bolt = Mats.Emissive(new Color(0.92f, 0.97f, 1f), new Color(3.5f, 5f, 6f));
        Vector3 a = new Vector3(0.16f, 0.52f, -0.3f), b = new Vector3(-0.1f, 0.05f, -0.3f);
        Vector3 c = new Vector3(0.14f, 0.05f, -0.3f), e = new Vector3(-0.12f, -0.52f, -0.3f);
        Bar(root.transform, a, b, 0.09f, bolt);
        Bar(root.transform, b, c, 0.09f, bolt);
        Bar(root.transform, c, e, 0.09f, bolt);
        ModelKit.Shadows(root, false, false);
        return root;
    }

    // A dark rock with glowing magma seams: two noisy rocks with different seeds overlap.
    public static GameObject MeteorRock(Transform parent, float size, int seed) {
        GameObject root = ModelKit.Root("Meteor Rock", parent);
        ModelKit.MeshPart(root.transform, MeshKit.Rock(0.42f, 30 + seed, 0.28f), Vector3.zero, Vector3.one * size,
            Mats.Lit(new Color(0.2f, 0.15f, 0.13f), 0.12f), default, "Crust");
        ModelKit.MeshPart(root.transform, MeshKit.Rock(0.4f, 60 + seed, 0.3f), Vector3.zero, Vector3.one * size,
            Mats.Emissive(new Color(0.9f, 0.35f, 0.08f), new Color(4.2f, 1.2f, 0.18f)), default, "Magma");
        return root;
    }

    // A glowing core circled by three little meteors. Returns the transform the meteors orbit on.
    public static Transform MeteorOrbCore(Transform parent) {
        GameObject core = ModelKit.Part(parent, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.44f,
            Mats.Emissive(new Color(1f, 0.55f, 0.18f), new Color(5.5f, 2f, 0.45f)));
        ModelKit.Shadows(core, false, false);
        Transform orbit = ModelKit.Root("Orbit", parent).transform;
        for (int i = 0; i < 3; i++) {
            float a = (90f + i * 120f) * Mathf.Deg2Rad;
            GameObject rock = MeteorRock(orbit, 0.42f, i);
            rock.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.42f, Mathf.Sin(a) * 0.42f, -0.05f);
        }
        return orbit;
    }

    static GameObject MeteorOrbModel(Transform parent) {
        GameObject root = ModelKit.Root("Meteor Orb", parent);
        MeteorOrbCore(root.transform);
        return root;
    }

    // A black core with a glowing purple rim (a bigger glowing sphere just behind it) and an accretion disk.
    public static GameObject VoidOrbModel(Transform parent) {
        GameObject root = ModelKit.Root("Void Orb", parent);
        ModelKit.Part(root.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.52f, Mats.Unlit(new Color(0.015f, 0f, 0.03f)));
        ModelKit.Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 0f, 0.14f), Vector3.one * 0.62f,
            Mats.Emissive(new Color(0.4f, 0.15f, 0.7f), new Color(2.4f, 0.8f, 5f)));
        ModelKit.Part(root.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.05f, 0.012f, 1.05f),
            Mats.Emissive(new Color(0.75f, 0.45f, 1f), new Color(3f, 1.3f, 5f)), new Vector3(-16f, 0f, -14f));
        ModelKit.Shadows(root, false, false);
        return root;
    }

    // A thin glowing bar from a to b (model space), used for the lightning glyph.
    static void Bar(Transform parent, Vector3 a, Vector3 b, float width, Material mat) {
        Vector3 d = b - a;
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        ModelKit.Part(parent, PrimitiveType.Cube, (a + b) * 0.5f, new Vector3(d.magnitude + width, width, width), mat, new Vector3(0f, 0f, angle));
    }
}

// Shared flight of the four orbs: a glowing sphere on a rigidbody that casts its spell on the first
// contact or, for the activatable ones, on a mid-air click. Trail, emitters and light live under `fx`
// so they can linger and fade out after the orb itself is gone.
public abstract class WizardOrb : Projectile {
    const float Radius = 0.3f, Mass = 1.2f;

    protected Transform fx;
    bool cast, splashed;

    public override bool CanActivate => base.CanActivate && !cast;

    protected override void OnLaunch(Vector3 velocity) {
        fx = new GameObject("FX").transform;
        fx.SetParent(transform, false);
        BuildVisuals();
        gameObject.AddComponent<SphereCollider>().radius = Radius;
        AddBody(Mass, velocity);
        SFX.Play(SoundId.MagicCast, transform.position, 0.8f);
    }

    // Model under the orb; emitters, light and trail under `fx`.
    protected abstract void BuildVisuals();

    // The spell itself. `hit` is null when the orb was activated in mid-air (point = the orb's position).
    // `normal` points from the surface that was hit towards the orb.
    protected abstract void Cast(Vector3 point, Vector3 normal, Collider hit);

    protected virtual void OnCollisionEnter(Collision c) {
        if (cast || finished) return;
        Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        p.z = 0f;
        Vector3 n = transform.position - p;
        n = n.sqrMagnitude > 1e-6f ? n.normalized : Vector3.up;
        Trigger(p, n, c.collider);
    }

    protected override void OnActivate() { Trigger(transform.position, Vector3.up, null); }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished || splashed || transform.position.y > World.WaterY) return;
        splashed = true;                   // a miss: splash where it enters the sea, then it is lost
        VFX.Splash(new Vector3(transform.position.x, World.WaterY, 0f), 0.8f);
        SFX.Play(SoundId.Splash, transform.position, 0.6f);
    }

    protected override void OnLost() { Vanish(); }

    void Trigger(Vector3 point, Vector3 normal, Collider hit) {
        if (cast || finished) return;
        cast = true;
        Cast(point, normal, hit);
        Vanish();
    }

    void Vanish() {
        if (rb != null) rb.detectCollisions = false;     // it lingers until the end of the frame: no more contacts
        WizardFX.Release(fx, 1.6f);
        fx = null;
        Finish(true);
    }
}

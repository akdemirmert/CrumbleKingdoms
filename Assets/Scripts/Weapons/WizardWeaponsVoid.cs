using System.Collections.Generic;
using UnityEngine;

// Void Orb: a black orb with a purple rim and accretion disk. On contact or a mid-air click it tears
// open a black hole (see BlackHole).
public class VoidOrb : WizardOrb {
    protected override void BuildVisuals() {
        WizardWeapons.VoidOrbModel(transform);
        WizardFX.Halo(fx, new Color(0.6f, 0.3f, 1f, 0.22f), 1.4f);
        WizardFX.Smoke(fx, 0.5f, 18f, new Color(0.18f, 0.06f, 0.3f, 0.5f));
        WizardFX.Embers(fx, 16f, WizardWeapons.VoidColor, 1f);
        WizardLight.Add(fx, WizardFX.LightOffset, WizardWeapons.VoidColor, 3f, 6f, 0.2f);
        VFX.Trail(fx, new Color(0.62f, 0.32f, 1f, 0.8f), 0.45f, 0.3f);
    }

    protected override void Cast(Vector3 point, Vector3 normal, Collider hit) {
        Vector3 center = hit != null ? point + normal * 0.5f : transform.position;   // just outside what it hit
        center.z = 0f;
        RaiseImpact(center);
        BlackHole.Open(owner, def, center);
    }
}

// The Void Orb's black hole. For a few seconds it pulls every loose body within def.radius towards its
// centre (harder closer in, with a slight swirl and some drag so things clump instead of slingshotting),
// grinds the blocks that reach its core, hurts commanders in range and swallows debris. Then it
// collapses and bursts. It is a Projectile only so the camera frames it and the turn waits for it.
public class BlackHole : Projectile {
    const float Life = 2.6f, GrowTime = 0.45f, CollapseTime = 0.3f;
    const float PullAccel = 28f, SwirlAccel = 7f, CoreDrag = 2.5f, CommanderPull = 0.6f, CollapsePull = 1.8f;
    const float CoreRadius = 1.6f, SwallowRadius = 1.1f, MaxSize = 1.5f;
    const float BlockDps = 30f, CommanderDps = 8f, TickEvery = 0.25f;
    const float BurstRadius = 4.5f, GlowIntensity = 5f;
    static readonly Collider[] buffer = new Collider[256];

    readonly HashSet<Rigidbody> pulled = new HashSet<Rigidbody>();
    readonly List<(IDamageable target, float amount, Vector3 point)> victims = new List<(IDamageable, float, Vector3)>();
    Transform horizon, rim, disk;
    WizardLight glow;
    AudioSource hum;
    float time, tick;
    int ticks;
    bool burst;

    public override bool CanActivate => false;

    public static void Open(Fortress owner, WeaponDef def, Vector3 center) {
        WeaponFactory.Spawn<BlackHole>("Black Hole", owner, def, center, Vector3.zero);
    }

    protected override void OnLaunch(Vector3 velocity) {
        maxLifetime = Life + 3f;        // safety net; the hole normally bursts at Life
        BuildVisuals();
        hum = SFX.PlayLoop(SoundId.VoidHum, transform, 0.9f);
        VFX.MagicBurst(transform.position, WizardWeapons.VoidColor, 1.3f);
        VFX.Ring(transform.position, 3f, WizardWeapons.VoidColor, 0.3f, 0.3f);
        BattleCamera.Shake(0.25f);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished) return;
        float dt = Time.fixedDeltaTime;
        time += dt;
        float strength = Mathf.Clamp01(time / GrowTime) * (time > Life - CollapseTime ? CollapsePull : 1f);
        Pull(strength);
        tick += dt;
        if (tick >= TickEvery) { tick -= TickEvery; Grind(); }
        if (time >= Life) Burst();
    }

    protected override void OnLost() { Burst(); }

    protected override void OnDestroy() {
        SFX.Stop(hum);
        base.OnDestroy();
    }

    // Acceleration towards the centre, scaled by (1 - d/r), plus a swirl tangent and a drag near the core.
    void Pull(float strength) {
        Vector3 c = transform.position;
        float r = def.radius;
        int n = Physics.OverlapSphereNonAlloc(c, r, buffer, Layers.BodiesMask, QueryTriggerInteraction.Ignore);
        pulled.Clear();
        for (int i = 0; i < n; i++) {
            Collider col = buffer[i];
            Rigidbody body = col != null ? col.attachedRigidbody : null;
            if (body == null || body.isKinematic || !pulled.Add(body)) continue;
            Vector3 to = c - body.worldCenterOfMass;
            float d = to.magnitude;
            if (d > r) continue;
            if (col.gameObject.layer == Layers.Debris && d < SwallowRadius) { Swallow(body); continue; }
            float k = 1f - d / r;                               // 0 at the edge, 1 at the centre
            Vector3 dir = d > 0.01f ? to / d : Vector3.zero;
            Vector3 swirl = new Vector3(-dir.y, dir.x, 0f);     // tangent in the battle plane
            Vector3 a = ((dir * PullAccel + swirl * SwirlAccel) * k - body.linearVelocity * (CoreDrag * k * k)) * strength;
            if (col.gameObject.layer == Layers.Commander) a *= CommanderPull;
            body.AddForce(a, ForceMode.Acceleration);
        }
    }

    // Debris that reaches the core spirals in and vanishes.
    void Swallow(Rigidbody body) {
        Debris debris = body.GetComponent<Debris>();
        if (debris != null) Destroy(debris);                // its own lifetime/shrink logic would fight ours
        body.gameObject.AddComponent<VoidSwallow>().Init(transform);
    }

    // Damage tick: blocks inside the core are crushed; commanders in range are hurt, less through walls
    // (Explosion.Cover), every other tick so their damage numbers stay readable. Targets are copied out first.
    void Grind() {
        ticks++;
        Vector3 c = transform.position;
        int n = Physics.OverlapSphereNonAlloc(c, def.radius, buffer, Layers.BlockMask | Layers.CommanderMask, QueryTriggerInteraction.Ignore);
        victims.Clear();
        for (int i = 0; i < n; i++) {
            if (buffer[i] == null) continue;
            IDamageable d = buffer[i].GetComponentInParent<IDamageable>();
            if (d == null || !d.IsAlive || WizardWeapons.IsFriend(d.Owner, owner)) continue;
            Vector3 p = buffer[i].ClosestPoint(c);
            if (d is Block && (p - c).sqrMagnitude <= CoreRadius * CoreRadius) victims.Add((d, BlockDps * TickEvery, p));
            else if (d is Commander cm && ticks % 2 == 0) victims.Add((d, CommanderDps * TickEvery * 2f * Explosion.Cover(c, cm.AimPoint), p));
        }
        foreach (var v in victims) v.target.ApplyDamage(new DamageInfo(v.amount, DamageType.Magic, v.point, owner, (c - v.point) * 0.5f));
        BattleCamera.Shake(0.08f);      // a constant low rumble
    }

    void Burst() {
        if (burst) return;
        burst = true;
        Vector3 c = transform.position;
        SFX.Stop(hum);
        hum = null;
        Explosion.Create(c, BurstRadius, def.damage, def.force, owner, DamageType.Magic, BlastStyle.Void);
        VFX.Ring(c, 6.5f, WizardWeapons.VoidColor, 0.45f, 0.5f);
        VFX.MagicBurst(c, WizardWeapons.VoidColor, 1.6f);
        VFX.LightFlash(c, WizardWeapons.VoidColor, 10f, 24f, 0.4f);
        VFX.ScreenFlash(new Color(0.6f, 0.35f, 1f, 0.35f), 0.22f);
        SFX.Play(SoundId.VoidBurst, c, 1f);
        BattleCamera.Shake(0.7f);
        horizon.gameObject.SetActive(false);
        rim.gameObject.SetActive(false);
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>()) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        glow.FadeOut(0.3f);
        Finish(true, 1.3f);             // let the swirl fade before the object goes
    }

    // The event horizon grows, pulses and swells slowly, then implodes in the last moments before the burst.
    void Update() {
        if (burst) return;
        float grow = Mathf.SmoothStep(0f, 1f, time / GrowTime);
        float collapse = Mathf.Clamp01((Life - time) / CollapseTime);     // 1, then 1 -> 0 at the very end
        float pulse = 1f + Mathf.Sin(Time.time * 17f) * 0.05f;
        float size = MaxSize * grow * Mathf.Lerp(0.75f, 1f, time / Life) * pulse * Mathf.Sqrt(collapse);
        horizon.localScale = Vector3.one * Mathf.Max(0.001f, size);
        rim.localScale = Vector3.one * Mathf.Max(0.001f, size * 1.22f);
        disk.localScale = Vector3.one * Mathf.Max(0.001f, Mathf.Lerp(0.3f, 1f, grow) * Mathf.Lerp(0.4f, 1f, collapse));
        glow.intensity = GlowIntensity * grow * (1f + (1f - collapse) * 2f);
    }

    void BuildVisuals() {
        horizon = ModelKit.Part(transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.001f, Mats.Unlit(new Color(0.01f, 0f, 0.02f))).transform;
        rim = ModelKit.Part(transform, PrimitiveType.Sphere, new Vector3(0f, 0f, 0.35f), Vector3.one * 0.001f,
                            Mats.Emissive(new Color(0.4f, 0.15f, 0.7f), new Color(3f, 1f, 6f))).transform;
        ModelKit.Shadows(gameObject, false, false);

        // accretion disk: a tilted ring of particles spiralling inwards, with dark dust further out
        disk = new GameObject("Disk").transform;
        disk.SetParent(transform, false);
        disk.localRotation = Quaternion.Euler(70f, 0f, -12f);
        WizardFX.SwirlRing(disk, "Accretion", TexKit.SoftCircle, true, 3.2f, 160f, new Color(0.8f, 0.55f, 1f), 3.8f, 2.6f);
        ParticleSystem dust = WizardFX.SwirlRing(disk, "Dust", TexKit.Smoke, false, 4.6f, 18f, new Color(0.16f, 0.05f, 0.28f, 0.55f), 1.6f, 3f);
        ParticleSystem.MainModule dustMain = dust.main;
        dustMain.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
        Infall(def.radius * 0.85f, 10f);
        glow = WizardLight.Add(transform, WizardFX.LightOffset, WizardWeapons.VoidColor, 0f, 14f, 0.15f);
    }

    // Thin streaks rushing in from the edge of the pull radius (a circle emitting with negative speed).
    void Infall(float radius, float speed) {
        ParticleSystem ps = WizardFX.Create(transform, "Infall", TexKit.SoftCircle, true, 120, false);
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = Mathf.Max(0.1f, (radius - 0.6f) / speed);    // gone just before the centre
        main.startSpeed = -speed;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        main.startColor = new Color(0.85f, 0.7f, 1f, 0.8f);
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 50f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0f;
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = WizardFX.FadeInOut();
        ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.06f;
        r.lengthScale = 2f;
        ps.Play();
    }
}

// A piece of debris falling into the black hole: it spirals to the centre and shrinks to nothing.
public class VoidSwallow : MonoBehaviour {
    const float Duration = 0.4f;
    Transform hole;
    Vector3 start, startScale;
    float t;

    public void Init(Transform hole) {
        this.hole = hole;
        start = transform.position;
        startScale = transform.localScale;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null) {
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;   // kinematic bodies cannot use continuous modes
            body.isKinematic = true;
        }
    }

    void Update() {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / Duration);
        if (hole != null) transform.position = Vector3.Lerp(start, hole.position, k * k);
        transform.localScale = startScale * Mathf.Max(0.001f, 1f - k);
        transform.Rotate(0f, 0f, 540f * Time.deltaTime, Space.World);
        if (k >= 1f) Destroy(gameObject);
    }
}

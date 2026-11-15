using UnityEngine;

// The Soldier's fuse bomb (FuseBombProjectile): a bouncing iron bomb with a burning fuse. It blows up when the
// fuse runs out or on a click (air burst), and fizzles out if it falls into the sea.

public class FuseBombProjectile : Projectile {
    const float FuseTime = 3.2f;
    const float Radius = 0.3f;
    const float Mass = 2f;
    static readonly Color BlinkColor = new Color(1f, 0.25f, 0.15f);

    Transform fuse, ember;
    Renderer band;
    Material bandLit, bandDark;
    ParticleSystem sparks;
    Light blinkLight;
    AudioSource fuseLoop;
    float blinkPhase, lastBounce;
    bool blinkOn;

    protected override void OnLaunch(Vector3 velocity) {
        Transform model = SoldierWeapons.BuildBomb(transform, out fuse, out ember, out band).transform;
        bandLit = SoldierWeapons.BandMaterial(true);
        bandDark = SoldierWeapons.BandMaterial(false);
        SphereCollider c = gameObject.AddComponent<SphereCollider>();
        c.radius = Radius;
        c.sharedMaterial = PhysMats.Bouncy;
        AddBody(Mass, velocity);
        rb.angularDamping = 0.8f;
        rb.angularVelocity = new Vector3(0f, 0f, -travelDir * 5f);
        sparks = SoldierFx.FuseSparks(model);
        blinkLight = SoldierFx.PointLight(transform, BlinkColor, 4f);
        fuseLoop = SFX.PlayLoop(SoundId.Fuse, transform, 0.6f);
        SFX.Play(SoundId.FireIgnite, transform.position, 0.35f, 1.6f);   // the fuse catches
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished) return;
        if (transform.position.y < World.WaterY) Fizzle();
        else if (age >= FuseTime) Detonate();
    }

    void Update() {
        if (finished) return;
        float k = Mathf.Clamp01(age / FuseTime);   // 0 = just lit, 1 = boom
        // The fuse burns down towards the bomb; the ember and its sparks ride on the tip.
        fuse.localScale = new Vector3(1f, Mathf.Max(0.05f, 1f - k), 1f);
        ember.localPosition = SoldierWeapons.FuseTip(fuse, 1f - k);
        sparks.transform.localPosition = ember.localPosition;
        // The warning light blinks faster as the fuse runs out (2 Hz -> 14 Hz).
        blinkPhase += Time.deltaTime * Mathf.Lerp(2f, 14f, k * k);
        bool on = blinkPhase % 1f < 0.5f;
        blinkLight.intensity = on ? 2.5f + 3f * k : 0f;
        if (on != blinkOn) {
            blinkOn = on;
            band.sharedMaterial = on ? bandLit : bandDark;
        }
    }

    protected override void OnActivate() { Detonate(); }   // click: air burst

    void OnCollisionEnter(Collision c) {
        if (finished) return;
        SoldierWeapons.FirstContact(c, transform.position, out Vector3 point, out _);
        SoldierWeapons.LimitShove(c);
        if (!impacted) RaiseImpact(point);
        float speed = c.relativeVelocity.magnitude;
        if (speed < 2.5f || Time.time < lastBounce + 0.12f) return;
        lastBounce = Time.time;
        SFX.Play(SoundId.Bounce, point, Mathf.Clamp(speed / 15f, 0.25f, 1f), 0.9f);
        VFX.Dust(point, 0.3f + speed * 0.02f, SoldierFx.GroundDust);
    }

    void Detonate() {
        if (finished) return;
        Vector3 pos = transform.position;
        if (!impacted) RaiseImpact(pos);
        StopFuse();
        Explosion.Create(pos, def.radius, def.damage, def.force, owner, DamageType.Explosion, BlastStyle.Fire);
        VFX.Sparks(pos, new Color(1f, 0.7f, 0.3f), 28);   // shrapnel
        Finish(true);
    }

    // Fell into the sea: the fuse goes out in a puff of steam, no blast.
    void Fizzle() {
        Vector3 pos = transform.position;
        if (!impacted) RaiseImpact(pos);
        StopFuse();
        SoldierFx.Splash(pos, 0.8f);
        VFX.Dust(new Vector3(pos.x, World.WaterY + 0.3f, 0f), 0.8f, new Color(0.92f, 0.93f, 0.95f, 0.55f));
        Finish(true);
    }

    void StopFuse() {
        SFX.Stop(fuseLoop);
        fuseLoop = null;
    }

    protected override void OnDestroy() {
        base.OnDestroy();
        StopFuse();
    }
}

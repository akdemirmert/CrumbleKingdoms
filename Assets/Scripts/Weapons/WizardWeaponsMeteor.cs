using System.Collections;
using UnityEngine;

// Meteor Orb: a glowing orb circled by little burning rocks. On contact or a mid-air click it opens a
// portal in the sky above the spot (the hit point, or the first surface below the orb) and the portal
// rains seven meteors around that spot.
public class MeteorOrb : WizardOrb {
    protected override void BuildVisuals() {
        Transform orbit = WizardWeapons.MeteorOrbCore(transform);
        orbit.gameObject.AddComponent<WizardSpin>().degreesPerSecond = new Vector3(0f, 0f, 260f);
        WizardFX.Halo(fx, new Color(1f, 0.55f, 0.18f, 0.2f), 1.4f);
        WizardFX.Flames(fx, 0.4f, 40f, new Color(1f, 0.8f, 0.4f), new Color(0.85f, 0.2f, 0.05f, 0.5f));
        WizardFX.Embers(fx, 30f, WizardWeapons.EmberColor, 1.4f);
        WizardLight.Add(fx, WizardFX.LightOffset, new Color(1f, 0.6f, 0.25f), 3.5f, 6.5f, 0.3f);
        VFX.Trail(fx, new Color(1f, 0.6f, 0.2f, 0.75f), 0.4f, 0.25f);
    }

    protected override void Cast(Vector3 point, Vector3 normal, Collider hit) {
        Vector3 spot = hit != null ? point : GroundBelow(transform.position);
        RaiseImpact(spot);
        WizardFX.Burst(transform.position, 30, 6f, 0.25f, 0.7f, WizardWeapons.EmberColor, 0.2f, TexKit.Star);
        MeteorShower.Open(owner, def, spot);
    }

    // The first solid surface under p that is not our own (the sea if there is none).
    Vector3 GroundBelow(Vector3 p) {
        RaycastHit[] below = Physics.RaycastAll(p, Vector3.down, Mathf.Max(0.1f, p.y - World.WaterY), Layers.SolidMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(below, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in below)
            if (!IsOwnTeam(h.collider)) return new Vector3(p.x, h.point.y, 0f);
        return new Vector3(p.x, World.WaterY, 0f);
    }
}

// The sky portal: a tilted, swirling ring of embers high above the spot that drops seven meteors over
// ~2.2 s. It is a Projectile only so the camera frames the spot and the turn waits for it; after the
// last meteor it lets the meteors in flight hold the turn and closes.
public class MeteorShower : Projectile {
    const int Meteors = 7;
    const float PortalHeight = 15f, AnchorHeight = 5f;     // above the spot; the camera follows the anchor
    const float OpenTime = 0.4f, RainTime = 2.2f, CloseTime = 0.8f;
    const float PortalSpread = 2.5f, GroundSpread = 3.5f, MinSpeed = 20f, MaxSpeed = 26f;
    const float PortalLight = 6f;

    Vector3 spot;
    Transform portal;
    WizardLight glow;

    public override bool CanActivate => false;

    public static void Open(Fortress owner, WeaponDef def, Vector3 spot) {
        WeaponFactory.Spawn<MeteorShower>("Meteor Shower", owner, def, spot + Vector3.up * AnchorHeight, Vector3.zero, s => s.spot = spot);
    }

    protected override void OnLaunch(Vector3 velocity) {
        maxLifetime = OpenTime + RainTime + 4f;    // safety net; the shower normally ends itself
        BuildPortal();
        StartCoroutine(Rain());
    }

    protected override void OnLost() { Finish(true); }

    IEnumerator Rain() {
        SFX.Play(SoundId.Portal, portal.position, 1f);
        BattleCamera.Shake(0.15f);
        for (float t = 0f; t < OpenTime; t += Time.deltaTime) {
            SetOpen(Mathf.SmoothStep(0f, 1f, t / OpenTime));
            yield return null;
        }
        SetOpen(1f);
        for (int i = 0; i < Meteors; i++) {
            if (i > 0) yield return new WaitForSeconds(RainTime / (Meteors - 1) * Random.Range(0.7f, 1.3f));
            DropMeteor();
        }
        Finish(false);                            // the meteors in flight hold the turn from here
        for (float t = 0f; t < CloseTime; t += Time.deltaTime) {
            SetOpen(1f - Mathf.SmoothStep(0f, 1f, t / CloseTime));
            yield return null;
        }
        Destroy(gameObject);
    }

    void SetOpen(float k) {
        portal.localScale = Vector3.one * Mathf.Max(0.001f, k);   // never exactly zero (degenerate bounds)
        glow.intensity = PortalLight * k;
    }

    // Each meteor flies straight from a point of the portal to a point around the spot.
    void DropMeteor() {
        Vector3 from = portal.position + new Vector3(Random.Range(-PortalSpread, PortalSpread), 0f, 0f);
        Vector3 to = spot + new Vector3(Random.Range(-GroundSpread, GroundSpread), 0f, 0f);
        Vector3 velocity = (to - from).normalized * Random.Range(MinSpeed, MaxSpeed);
        WeaponFactory.Spawn<FallingMeteor>("Meteor", owner, def, from, velocity);
        WizardFX.Burst(from, 14, 4f, 0.35f, 0.4f, new Color(1f, 0.75f, 0.35f), 0f, TexKit.SoftCircle);
    }

    // A ring of swirling embers around a dark, glowing eye, seen a little from below; embers drip from it.
    void BuildPortal() {
        portal = new GameObject("Portal").transform;
        portal.SetParent(transform, false);
        portal.localPosition = Vector3.up * (PortalHeight - AnchorHeight);
        Transform disc = new GameObject("Disc").transform;
        disc.SetParent(portal, false);
        disc.localRotation = Quaternion.Euler(62f, 0f, 0f);

        WizardFX.SwirlRing(disc, "Ring", TexKit.SoftCircle, true, 2.7f, 170f, new Color(1f, 0.72f, 0.3f), 3.2f, 0.3f);
        ParticleSystem vortex = WizardFX.SwirlRing(disc, "Vortex", TexKit.Smoke, true, 1.9f, 26f, new Color(1f, 0.4f, 0.1f, 0.35f), 1.6f, 0.9f);
        ParticleSystem eye = WizardFX.SwirlRing(disc, "Eye", TexKit.Smoke, false, 1.3f, 12f, new Color(0.12f, 0.02f, 0.05f, 0.8f), 0.8f, 0.3f);
        foreach (ParticleSystem ps in new[] { vortex, eye }) {
            ParticleSystem.MainModule main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.radiusThickness = 1f;          // fill the disc, not just its rim
        }
        WizardFX.Embers(portal, 30f, WizardWeapons.EmberColor, 1f);
        glow = WizardLight.Add(portal, WizardFX.LightOffset, new Color(1f, 0.55f, 0.25f), 0f, 24f, 0.25f);
        SetOpen(0f);
    }
}

// One burning rock of the shower: flies in a straight line (no gravity, no wind) and blows up on
// contact, sometimes setting wood on fire.
public class FallingMeteor : Projectile {
    const float IgniteChance = 0.4f, IgniteSeconds = 4f;
    Transform fx;
    bool splashed;

    public override bool CanActivate => false;

    protected override void OnLaunch(Vector3 velocity) {
        gravityScale = 1f;      // with the body's own gravity off, this means no gravity at all
        windFactor = 0f;
        float size = Random.Range(0.8f, 1.2f);
        GameObject rock = WizardWeapons.MeteorRock(transform, size, Random.Range(0, 4));
        rock.AddComponent<WizardSpin>().degreesPerSecond = Random.onUnitSphere * Random.Range(150f, 320f);

        fx = new GameObject("FX").transform;
        fx.SetParent(transform, false);
        WizardFX.Flames(fx, 0.8f * size, 70f, new Color(1f, 0.85f, 0.45f), new Color(0.9f, 0.2f, 0.04f, 0.6f));
        WizardFX.Smoke(fx, 0.8f * size, 24f, new Color(0.14f, 0.11f, 0.1f, 0.55f));
        WizardFX.Embers(fx, 20f, WizardWeapons.EmberColor, 2f);
        WizardLight.Add(fx, WizardFX.LightOffset, new Color(1f, 0.55f, 0.2f), 3f, 6f, 0.3f);
        VFX.Trail(fx, new Color(1f, 0.5f, 0.15f, 0.8f), 0.7f * size, 0.3f);

        gameObject.AddComponent<SphereCollider>().radius = 0.38f * size;
        AddBody(3f * size, velocity, false);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;   // the model spins instead
        SFX.Play(SoundId.MeteorFall, transform.position, 0.7f, Random.Range(0.9f, 1.1f));
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished || splashed || transform.position.y > World.WaterY) return;
        splashed = true;        // missed everything: a splash where it hits the sea
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);
        VFX.Splash(p, 1.2f);
        SFX.Play(SoundId.Splash, p, 0.8f);
    }

    void OnCollisionEnter(Collision c) {
        if (finished) return;
        Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        p.z = 0f;
        RaiseImpact(p);
        Explosion.Create(p, def.radius, def.damage, def.force, owner, DamageType.Explosion, BlastStyle.Fire);
        WizardWeapons.SetAblaze(p, def.radius, owner, IgniteChance, IgniteSeconds, false);
        WizardFX.Burst(p, 24, 7f, 0.14f, 0.9f, WizardWeapons.EmberColor, 0.6f, TexKit.SoftCircle);
        End();
    }

    protected override void OnLost() { End(); }

    void End() {
        WizardFX.Release(fx, 1.5f);
        fx = null;
        Finish(true);
    }
}

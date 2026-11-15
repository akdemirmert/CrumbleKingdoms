using System;
using System.Collections;
using UnityEngine;

// Visual parts of a commander (built by CommanderModel). Models face the camera; for facing -1 the
// visual root is mirrored (scale x = -1), so arm angles are always "towards the enemy".
public class CommanderRig : MonoBehaviour {
    public Transform body, head, armPivot, hand, prop;
    public Renderer[] renderers;
    public float armRest = -25f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    MaterialPropertyBlock mpb;
    Coroutine flashing;

    public void SetArm(float deg) {
        if (armPivot != null) armPivot.localRotation = Quaternion.Euler(0f, 0f, deg);
    }

    public void Flash(Color c, float seconds) {
        if (renderers == null || renderers.Length == 0 || !isActiveAndEnabled) return;
        if (flashing != null) StopCoroutine(flashing);
        flashing = StartCoroutine(FlashRoutine(c, seconds));
    }

    IEnumerator FlashRoutine(Color c, float seconds) {
        mpb ??= new MaterialPropertyBlock();
        float t = 0f;
        while (t < seconds) {
            t += Time.deltaTime;
            float k = 1f - t / seconds;
            foreach (Renderer r in renderers) {
                if (r == null) continue;
                Color baseC = r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId) ? r.sharedMaterial.GetColor(BaseColorId) : Color.white;
                mpb.SetColor(BaseColorId, Color.Lerp(baseC, c, k));
                r.SetPropertyBlock(mpb);
            }
            yield return null;
        }
        foreach (Renderer r in renderers) if (r != null) r.SetPropertyBlock(null);
        flashing = null;
    }

    public void SetDead() {
        if (head != null) head.localRotation = Quaternion.Euler(0f, 0f, 25f);
        SetArm(-70f);
    }

    public void SetAlive() {
        if (head != null) head.localRotation = Quaternion.identity;
        SetArm(armRest);
        SetMustache(true);
    }

    // Dalí throws his mustache; other models have none (null).
    public GameObject mustache;

    public void SetMustache(bool visible) {
        if (mustache != null) mustache.SetActive(visible);
    }
}

public class Commander : MonoBehaviour, IDamageable {
    public Fortress fortress;
    public float maxHP, hp;
    public bool dead;
    public string deathCause;
    public Rigidbody rb;
    public CapsuleCollider col;
    public CommanderRig rig;

    public event Action<Commander> Died;

    public float launchHeight = 2.05f;   // set from the blueprint: shots leave above the chamber roof

    // A shield (e.g. the Vampire's blood cloak) gets every hit first; returning true means it absorbed it.
    public Func<DamageInfo, bool> shield;

    public Fortress Owner => fortress;
    public bool IsAlive => this != null && !dead;
    public Vector3 AimPoint => transform.position + Vector3.up * 0.85f;
    public Vector3 LaunchPoint => transform.position + new Vector3(0.45f * fortress.Facing, launchHeight, 0f);

    float spawnTime, hurtSfxTime;
    bool splashed;
    Coroutine throwing;

    public static Commander Create(Fortress f, Vector3 feet) {
        GameObject go = new GameObject("Commander — " + f.setup.name) { layer = Layers.Commander };
        go.transform.position = feet + Vector3.up * 0.02f;
        CapsuleCollider col = go.AddComponent<CapsuleCollider>();
        col.radius = 0.32f;
        col.height = 1.6f;
        col.center = new Vector3(0f, 0.8f, 0f);
        col.sharedMaterial = PhysMats.Commander;
        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.mass = 4f;   // as heavy as a boulder, so one hit rarely throws a commander off its tower
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.linearDamping = 0.1f;

        Commander c = go.AddComponent<Commander>();
        c.fortress = f;
        c.rb = rb;
        c.col = col;
        c.maxHP = c.hp = f.setup.commanderHP;
        c.spawnTime = Time.time;
        c.rig = CommanderModel.Build(f.setup.heroClass, f.setup.teamColor, f.Facing, go.transform);
        if (c.rig != null) {
            ModelKit.SetLayer(c.rig.gameObject, Layers.Commander);
            c.rig.SetArm(c.rig.armRest);
        }
        rb.Sleep();
        return c;
    }

    public void ApplyDamage(DamageInfo d) {
        if (!IsAlive || d.amount <= 0f) return;
        if ((d.type == DamageType.Impact || d.type == DamageType.Crush) && Time.time < spawnTime + 1.5f) return;
        if (shield != null && shield(d)) return;
        float amount = Mathf.Min(d.amount, hp);
        hp -= amount;
        if (d.source != null && d.source != fortress) d.source.damageDealt += amount;
        if (amount >= 1f)
            VFX.FloatingText(transform.position + Vector3.up * 2.4f, "-" + Mathf.RoundToInt(amount), new Color(1f, 0.38f, 0.32f),
                             Mathf.Clamp(0.8f + amount / 60f, 0.8f, 1.7f));
        if (rig != null) rig.Flash(new Color(1f, 0.25f, 0.25f), 0.3f);
        if (Time.time > hurtSfxTime + 0.25f) { hurtSfxTime = Time.time; SFX.Play(SoundId.Hurt, transform.position, 0.8f); }
        if (d.impulse.sqrMagnitude > 0.01f && !rb.isKinematic)
            rb.AddForce(Vector3.ClampMagnitude(d.impulse, 12f) * 0.25f, ForceMode.Impulse);
        if (hp <= 0f) Die(CauseFor(d.type));
    }

    void OnCollisionEnter(Collision c) {
        if (!IsAlive || Time.time < spawnTime + 1.5f) return;
        if (c.rigidbody != null && c.rigidbody.GetComponent<Projectile>() != null) {
            // Projectiles deal their own damage; a direct hit may knock the commander back, not out to sea.
            rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, 5f);
            return;
        }
        float dv = c.impulse.magnitude / rb.mass;
        if (dv <= 4.5f) return;
        bool fromDebris = c.gameObject.layer == Layers.Debris;
        Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        // Falling rubble hurts, but one bad landing should not decide the battle on its own.
        float dmg = Mathf.Min((dv - 4.5f) * (fromDebris ? 1.5f : 4f), 45f);
        ApplyDamage(new DamageInfo(dmg, DamageType.Crush, p, null));
    }

    void FixedUpdate() {
        if (dead) return;
        if (transform.position.y < World.WaterY - 0.4f) {
            if (!splashed) {
                splashed = true;
                VFX.Splash(new Vector3(transform.position.x, World.WaterY, 0f), 1.2f);
                SFX.Play(SoundId.Splash, transform.position);
            }
            Die("Swept away by the sea");
        }
    }

    public void Die(string cause) {
        if (dead) return;
        dead = true;
        hp = 0f;
        deathCause = cause;
        if (throwing != null) StopCoroutine(throwing);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        rb.AddTorque(new Vector3(0f, 0f, -fortress.Facing * 4f), ForceMode.Impulse);
        if (rig != null) rig.SetDead();
        VFX.SoulRise(transform.position + Vector3.up, fortress.setup.teamColor);
        SFX.Play(SoundId.Death, transform.position);
        Embedded.ReleaseAll(transform, rb.linearVelocity);
        TimeFX.SlowMo(0.35f, 0.9f);
        Died?.Invoke(this);
    }

    // Life drained back (Vampire fang). Returns how much was actually healed.
    public float Heal(float amount) {
        if (!IsAlive || amount <= 0f) return 0f;
        float healed = Mathf.Min(amount, maxHP - hp);
        if (healed <= 0f) return 0f;
        hp += healed;
        VFX.FloatingText(transform.position + Vector3.up * 2.4f, "+" + Mathf.RoundToInt(healed), new Color(0.45f, 1f, 0.5f), 1.1f);
        if (rig != null) rig.Flash(new Color(0.4f, 1f, 0.5f), 0.35f);
        return healed;
    }

    // Time rewind: put the commander back where and how he was (alive again if he died since).
    public void Restore(Vector3 position, float health) {
        hp = Mathf.Clamp(health, 1f, maxHP);
        if (dead) {
            dead = false;
            deathCause = null;
            splashed = false;
            if (rig != null) rig.SetAlive();
            foreach (CommanderIdle idle in GetComponentsInChildren<CommanderIdle>(true)) idle.enabled = true;   // it stops itself on death
        }
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = position;
        transform.SetPositionAndRotation(position, Quaternion.identity);
        spawnTime = Time.time;   // no crush damage while the restored fortress settles
        rb.Sleep();
    }

    // ------------------------------------------------------------------ animation
    public void SetAim(float angleDeg) {
        if (rig != null && IsAlive && throwing == null) rig.SetArm(Mathf.Clamp(angleDeg, -30f, 95f) + 90f);
    }

    public void ResetPose() {
        if (rig != null && IsAlive && throwing == null) rig.SetArm(rig.armRest);
    }

    public void PlayThrow() {
        if (rig == null || !IsAlive) return;
        if (throwing != null) StopCoroutine(throwing);
        throwing = StartCoroutine(ThrowRoutine());
    }

    // Arm angle 0 = hanging down, 90 = pointing at the enemy, 180 = straight up, 225 = up and back.
    IEnumerator ThrowRoutine() {
        float[] keys = { 215f, 235f, 40f, rig.armRest };
        float[] times = { 0.08f, 0.1f, 0.12f, 0.3f };
        float from = rig.armPivot != null ? rig.armPivot.localEulerAngles.z : 0f;
        for (int k = 0; k < keys.Length; k++) {
            float t = 0f;
            while (t < times[k]) {
                t += Time.deltaTime;
                rig.SetArm(Mathf.Lerp(from, keys[k], Mathf.SmoothStep(0f, 1f, t / times[k])));
                yield return null;
            }
            from = keys[k];
        }
        rig.SetArm(rig.armRest);
        throwing = null;
    }

    static string CauseFor(DamageType t) {
        switch (t) {
            case DamageType.Fire: return "Burned to ashes";
            case DamageType.Lightning: return "Struck by lightning";
            case DamageType.Crush: return "Crushed by the rubble";
            case DamageType.Pierce: return "Pierced through";
            case DamageType.Magic: return "Swallowed by the void";
            case DamageType.Explosion: return "Blown sky-high";
            default: return "The commander has fallen";
        }
    }
}

using UnityEngine;

// Fireball: a burning orb that explodes on contact, sets the wood around the blast ablaze (the fire then
// spreads by itself, see Block.SpreadFire) and leaves enemy commanders caught in it burning for a while.
public class FireballOrb : WizardOrb {
    const float IgniteReach = 1.15f;    // x blast radius
    const float IgniteSeconds = 5f;

    protected override void BuildVisuals() {
        WizardWeapons.FireCore(transform);
        WizardFX.Halo(fx, new Color(1f, 0.55f, 0.15f, 0.22f), 1.4f);
        WizardFX.Flames(fx, 0.6f, 70f, new Color(1f, 0.85f, 0.45f), new Color(0.9f, 0.18f, 0.03f, 0.6f));
        WizardFX.Smoke(fx, 0.55f, 16f, new Color(0.16f, 0.12f, 0.1f, 0.45f));
        WizardFX.Embers(fx, 24f, WizardWeapons.EmberColor, 1.8f);
        WizardLight.Add(fx, WizardFX.LightOffset, new Color(1f, 0.55f, 0.2f), 4f, 7f, 0.35f);
        VFX.Trail(fx, new Color(1f, 0.5f, 0.12f, 0.85f), 0.5f, 0.22f);
    }

    protected override void Cast(Vector3 point, Vector3 normal, Collider hit) {
        Vector3 p = point + normal * 0.15f;
        RaiseImpact(p);
        Explosion.Create(p, def.radius, def.damage, def.force, owner, DamageType.Fire, BlastStyle.Fire);
        WizardWeapons.SetAblaze(p, def.radius * IgniteReach, owner, 1f, IgniteSeconds, true);
        WizardFX.Burst(p, 46, 9f, 0.16f, 1.1f, WizardWeapons.EmberColor, 0.5f, TexKit.SoftCircle);
        SFX.Play(SoundId.FireIgnite, p, 1f);
    }
}

// A commander caught in a fireball burns for a few seconds: flames on the body and small fire damage
// ticks. It holds the turn (TurnActor) until the fire is out, so a death by burning counts this turn.
public class WizardBurn : MonoBehaviour {
    const float Duration = 3f, TickEvery = 0.6f, TickDamage = 3f;

    Commander target;
    Fortress source;
    float timeLeft, tick;

    public static void Apply(Commander cm, Fortress source) {
        if (cm == null || !cm.IsAlive) return;
        WizardBurn burn = cm.GetComponentInChildren<WizardBurn>();
        if (burn == null) {
            GameObject go = new GameObject("Burning");
            go.transform.SetParent(cm.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            go.AddComponent<TurnActor>();
            burn = go.AddComponent<WizardBurn>();
            burn.target = cm;
            VFX.Fire(go.transform, Vector3.zero, 1.1f);
            SFX.Play(SoundId.FireIgnite, cm.transform.position, 0.8f);
        }
        burn.source = source;
        burn.timeLeft = Duration;      // a second fireball refreshes the burn
    }

    void Update() {
        if (target == null || !target.IsAlive) { Destroy(gameObject); return; }
        tick += Time.deltaTime;
        timeLeft -= Time.deltaTime;
        if (tick >= TickEvery) {
            tick -= TickEvery;
            target.ApplyDamage(new DamageInfo(TickDamage, DamageType.Fire, target.AimPoint, source));
        }
        if (timeLeft <= 0f) Destroy(gameObject);
    }
}

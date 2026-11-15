using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A deliberately simple bot:
//   1. pick the enemy to shoot at (players first, then the nearest),
//   2. pick a weapon (the biggest ready one half of the time, otherwise any ready one),
//   3. work out the perfect throw at that enemy's commander (Ballistics.Solve),
//   4. spoil it with random error: large on early levels, small on late ones (AIProfile), and a little
//      smaller with every shot the bot fires in the same battle, so it "finds its range".
public class BotShotController : MonoBehaviour {
    readonly Dictionary<Fortress, int> shotsFired = new Dictionary<Fortress, int>();
    AimVisuals visuals;

    void Awake() { visuals = gameObject.AddComponent<AimVisuals>(); }

    public IEnumerator TakeTurn(Fortress f, Action<ShotCommand> done) {
        AIProfile ai = f.setup.ai ?? AIProfile.FromSkill(0.7f);
        yield return new WaitForSeconds(ai.thinkTime);

        Fortress target = ChooseTarget(f);
        if (target == null) { done(null); yield break; }
        int slot = ChooseSlot(f);
        WeaponDef def = f.Weapon(slot);

        // The perfect throw (allowing for part of the wind).
        Vector3 aim = target.CommanderAlive ? target.commander.AimPoint : target.TopPoint;
        Vector3 origin = f.LaunchOrigin;
        bool high = def.arc == ArcPreference.High || (def.arc == ArcPreference.Any && UnityEngine.Random.value < 0.5f);
        Ballistics.Solve(origin, aim, def, BattleManager.Wind * ai.windSense, high, out Vector3 perfect, f.launchSpeedScale);

        // Human-like error that shrinks by 8 % per shot already fired (down to 60 %).
        shotsFired.TryGetValue(f, out int n);
        shotsFired[f] = n + 1;
        float practice = Mathf.Max(0.6f, 1f - 0.08f * n);
        float angle = Ballistics.AngleOf(perfect, f.Facing) + Gauss() * ai.aimError * practice;
        float speed = Mathf.Clamp(perfect.magnitude * (1f + Gauss() * ai.powerError * practice), f.MinSpeed(def), f.MaxSpeed(def));

        // Mid-air powers (lightning, bomb...) fire above the target, with the same kind of error.
        float activateX = def.CanActivate ? aim.x - f.Facing * ActivationLead(def.id) + Gauss() * ai.aimError * 0.3f * practice : float.NaN;

        if (!def.selfCast) yield return ShowAim(f, def, origin, angle, speed);   // nothing to aim for a self-cast
        done(new ShotCommand {
            slot = slot, velocity = Ballistics.FromAngle(angle, speed, f.Facing), fromBot = true, target = aim, targetFortress = target,
            activateX = activateX,
        });
    }

    // Swing the aim line into place over ~1 s so the player can read what the bot is doing.
    IEnumerator ShowAim(Fortress f, WeaponDef def, Vector3 origin, float angle, float speed) {
        float t = 0f, duration = 0.9f;
        float power01 = Mathf.InverseLerp(f.MinSpeed(def), f.MaxSpeed(def), speed);
        while (t < duration) {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            float a = Mathf.Lerp(30f, angle, k);
            if (!GameSession.HideBotAim)
                visuals.Show(origin, Ballistics.FromAngle(a, Mathf.Lerp(f.MinSpeed(def), speed, k), f.Facing), def.gravityScale, def.color, power01 * k);
            if (f.CommanderAlive) f.commander.SetAim(a);
            yield return null;
        }
        yield return new WaitForSeconds(0.15f);
        visuals.Hide();
    }

    static Fortress ChooseTarget(Fortress f) {
        Fortress best = null;
        float bestScore = float.MaxValue;
        foreach (Fortress e in BattleManager.I.EnemiesOf(f)) {
            float score = Mathf.Abs(e.transform.position.x - f.transform.position.x) - (e.IsHuman ? 1000f : 0f);
            if (score < bestScore) { bestScore = score; best = e; }
        }
        return best;
    }

    static int ChooseSlot(Fortress f) {
        if (GameSession.ForcedSlot >= 0 && f.IsReady(GameSession.ForcedSlot)) return GameSession.ForcedSlot;
        List<int> ready = new List<int>();
        for (int i = 0; i < 4; i++) if (f.IsReady(i)) ready.Add(i);
        if (ready.Count == 0) return 0;
        return UnityEngine.Random.value < 0.5f ? ready[ready.Count - 1] : ready[UnityEngine.Random.Range(0, ready.Count)];
    }

    // How far before the target (along the flight) a bot triggers an activatable weapon.
    static float ActivationLead(WeaponId id) {
        switch (id) {
            case WeaponId.MeteorOrb: return 1f;
            case WeaponId.VoidOrb: return 1.2f;
            case WeaponId.Gjallarhorn: return 0.8f;
            default: return 0f;
        }
    }

    static float Gauss() {
        float u1 = 1f - UnityEngine.Random.value, u2 = UnityEngine.Random.value;
        return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin(2f * Mathf.PI * u2);
    }
}

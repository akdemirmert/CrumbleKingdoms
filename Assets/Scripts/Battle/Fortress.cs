using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// What FortressGenerator produces: block layout in fortress-local space (origin = centre of the base).
public class FortressBlueprint {
    public readonly List<BlockSpec> blocks = new List<BlockSpec>();
    public Vector3 commanderSpot;   // feet position of the commander (local), inside its protected chamber
    public float launchHeight = 2.05f; // shots start this far above the commander's feet (above the chamber roof)
    public float width, height;
}

public class Team {
    public int id;
    public readonly List<Fortress> fortresses = new List<Fortress>();
    int rotation;

    public bool IsAlive { get { foreach (Fortress f in fortresses) if (!f.defeated) return true; return false; } }
    public bool HasHuman { get { foreach (Fortress f in fortresses) if (f.IsHuman) return true; return false; } }

    // Alive fortresses of a team shoot in rotation.
    public Fortress NextShooter() {
        for (int i = 0; i < fortresses.Count; i++) {
            int k = (rotation + i) % fortresses.Count;
            if (!fortresses[k].defeated) { rotation = k + 1; return fortresses[k]; }
        }
        return null;
    }
}

public class Fortress : MonoBehaviour {
    public FortressSetup setup;
    public int index;
    public Team team;
    public FortressBlueprint blueprint;
    public Commander commander;
    public readonly List<Block> blocks = new List<Block>();
    public float initialHP;

    public readonly int[] cooldowns = new int[4];
    public int selectedSlot;
    public bool defeated;
    public string defeatReason;

    // stats
    public int shotsFired;
    public float damageDealt;
    public int blocksLost;

    // remembered aim (keyboard aiming continues from here)
    public float lastAimAngle = 40f, lastAimPower = 0.6f;

    // Multiplies this fortress's launch speeds (the Alien's space warp slows its enemies down).
    public float launchSpeedScale = 1f;
    public float MinSpeed(WeaponDef d) { return d.minSpeed * launchSpeedScale; }
    public float MaxSpeed(WeaponDef d) { return d.maxSpeed * launchSpeedScale; }

    float cachedHP;
    bool dirty = true;

    public HeroClass HeroClass => setup.heroClass;
    public int Facing => setup.facing;
    public string DisplayName => setup.name;
    public Color TeamColor => setup.teamColor;
    public bool IsHuman => setup.controller == ControllerType.Human && !GameSession.AutoPlay;
    public bool CommanderAlive => commander != null && commander.IsAlive;

    public static Fortress Build(FortressSetup s, int index) {
        GameObject root = new GameObject("Fortress " + index + " — " + s.name);
        root.transform.position = new Vector3(s.x, s.groundY, 0f);
        Fortress f = root.AddComponent<Fortress>();
        f.setup = s;
        f.index = index;
        f.blueprint = FortressGenerator.Generate(s);
        foreach (BlockSpec spec in f.blueprint.blocks) {
            Block b = Block.Create(spec, f, root.transform);
            f.blocks.Add(b);
            f.initialHP += b.maxHP;
        }
        f.commander = Commander.Create(f, root.transform.TransformPoint(f.blueprint.commanderSpot));
        f.commander.launchHeight = f.blueprint.launchHeight;
        FortressGenerator.Decorate(f);
        return f;
    }

    // ------------------------------------------------------------------ health
    public float CurrentHP {
        get {
            if (dirty) {
                cachedHP = 0f;
                foreach (Block b in blocks) if (b != null && b.IsAlive) cachedHP += b.hp;
                dirty = false;
            }
            return cachedHP;
        }
    }

    public float Integrity => initialHP > 0f ? Mathf.Clamp01(CurrentHP / initialHP) : 0f;

    public void MarkDirty() { dirty = true; }

    public void OnBlockGone(Block b) {
        blocks.Remove(b);
        blocksLost++;
        dirty = true;
        WakeUp();
    }

    // Blocks spawn asleep a hair apart, so a sleeping block never notices that what held it up is gone (it
    // would hang in the air): when one leaves, the rest of the fortress wakes, settles and sleeps again.
    void WakeUp() {
        foreach (Block x in blocks)
            if (x != null && x.rb != null && !x.rb.isKinematic && x.rb.IsSleeping()) x.rb.WakeUp();
        if (CommanderAlive && commander.rb.IsSleeping()) commander.rb.WakeUp();
    }

    // ------------------------------------------------------------------ weapons
    public WeaponDef Weapon(int slot) { return WeaponDefs.ForClass(setup.heroClass, slot); }
    public WeaponDef SelectedWeapon => Weapon(selectedSlot);
    public bool IsReady(int slot) { return cooldowns[slot] <= 0; }

    public void TickCooldowns() {
        for (int i = 0; i < 4; i++) if (cooldowns[i] > 0) cooldowns[i]--;
        if (!IsReady(selectedSlot)) selectedSlot = 0;
    }

    // Cooldown N = the weapon skips the owner's next N turns.
    public void Consume(int slot) {
        int cd = Weapon(slot).cooldown;
        cooldowns[slot] = cd > 0 ? cd + 1 : 0;
        shotsFired++;
    }

    // ------------------------------------------------------------------ geometry
    public Vector3 LaunchOrigin => CommanderAlive ? commander.LaunchPoint : TopPoint + Vector3.up;

    public Vector3 TopPoint {
        get {
            Bounds b = GetBounds();
            return new Vector3(b.center.x, b.max.y, 0f);
        }
    }

    public Bounds GetBounds() {
        Bounds b = new Bounds(transform.position + Vector3.up, Vector3.one * 2f);
        foreach (Block bl in blocks) if (bl != null && bl.IsAlive) b.Encapsulate(bl.rend.bounds);
        if (commander != null) b.Encapsulate(commander.transform.position + Vector3.up * 2f);
        return b;
    }

    // ------------------------------------------------------------------ defeat
    // Only the commander's death decides: a ruined fortress just leaves him exposed.
    public bool ShouldBeDefeated(out string reason) {
        reason = null;
        if (defeated || CommanderAlive) return false;
        reason = commander != null && commander.deathCause != null ? commander.deathCause : "The commander has fallen";
        return true;
    }

    // Marks the fortress defeated and crumbles what is left of it.
    public IEnumerator Collapse(string reason) {
        defeated = true;
        defeatReason = reason;
        if (CommanderAlive) commander.Die(reason);
        List<Block> left = new List<Block>(blocks);
        left.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
        float step = left.Count > 0 ? Mathf.Min(0.08f, 1.4f / left.Count) : 0f;
        int crumbleEvery = left.Count > 30 ? 2 : 1;
        for (int i = 0; i < left.Count; i++) {
            Block b = left[i];
            if (b == null || !b.IsAlive) continue;
            if (i % crumbleEvery == 0 || b.def.brittle) b.Crumble();
            else if (b.rb != null) b.rb.AddForce(new Vector3(Random.Range(-1f, 1f), 1.5f, 0f), ForceMode.VelocityChange);
            if (step > 0f) yield return new WaitForSeconds(step);
        }
        BattleCamera.Shake(0.5f);
        yield return new WaitForSeconds(0.6f);
    }
}

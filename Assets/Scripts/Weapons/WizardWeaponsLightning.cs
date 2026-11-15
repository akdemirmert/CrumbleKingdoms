using System.Collections.Generic;
using UnityEngine;

// Lightning Orb: a crackling orb. On contact or a mid-air click it calls a bolt from the sky straight
// down its own column (see LightningStrike).
public class LightningOrb : WizardOrb {
    protected override void BuildVisuals() {
        WizardWeapons.LightningCore(transform);
        WizardArcs.Add(transform, 3, WizardWeapons.StormColor);
        WizardFX.Halo(fx, new Color(0.5f, 0.85f, 1f, 0.2f), 1.3f);
        WizardFX.Embers(fx, 18f, WizardWeapons.StormColor, 4f);
        WizardLight.Add(fx, WizardFX.LightOffset, WizardWeapons.StormColor, 3.5f, 6.5f, 0.6f);
        VFX.Trail(fx, new Color(0.55f, 0.85f, 1f, 0.8f), 0.35f, 0.28f);
    }

    protected override void Cast(Vector3 point, Vector3 normal, Collider hit) {
        Vector3 end = LightningStrike.Strike(transform.position, def.damage, def.radius, def.force, owner);
        RaiseImpact(end);
    }
}

// A lightning strike from the sky down one column; usable by any weapon (e.g. Thor's hammer).
// The bolt pierces the first three blocks or commanders it meets (100 %, 60 %, 35 % of the damage;
// commanders take half of that share), stops at the ground or the sea, gives a small push where it
// ends and may set wood it hit on fire. Then it chains to up to three metal blocks or enemy commanders
// around the end point (35 % damage each). The source's own team is never hurt by the bolt itself.
public static class LightningStrike {
    public const float SkyY = 40f;
    const float SweepRadius = 0.6f, CommanderShare = 0.5f, ChainShare = 0.35f;
    const float IgniteChance = 0.5f, IgniteSeconds = 4f, PushRadius = 1.8f;
    const int MaxHits = 3, MaxChains = 3;
    static readonly float[] hitShare = { 1f, 0.6f, 0.35f };
    static readonly RaycastHit[] hits = new RaycastHit[32];
    static readonly Collider[] near = new Collider[64];
    static readonly IComparer<RaycastHit> byDistance = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

    // Strikes the column at at.x. Returns where the main bolt ended (ground, sea or the last thing it pierced).
    public static Vector3 Strike(Vector3 at, float damage, float chainRadius, float push, Fortress source) {
        Vector3 top = new Vector3(at.x, Mathf.Max(SkyY, at.y + 6f), 0f);
        List<IDamageable> struck = new List<IDamageable>(MaxHits);
        List<Vector3> points = new List<Vector3>(MaxHits);
        Vector3 end = FindTargets(top, source, struck, points, out bool ground);

        for (int i = 0; i < struck.Count; i++) {
            float share = hitShare[i] * (struck[i] is Commander ? CommanderShare : 1f);
            Zap(struck[i], points[i], damage * share, source, true);
        }
        Explosion.Push(end, PushRadius, push, 0.4f, out _);
        Chain(end, damage * ChainShare, chainRadius, source, struck);
        Visuals(top, end, ground, struck.Count);
        return end;
    }

    // Sweeps a sphere down the column, collecting up to MaxHits targets (copied out before any damage).
    static Vector3 FindTargets(Vector3 top, Fortress source, List<IDamageable> struck, List<Vector3> points, out bool ground) {
        ground = false;
        int n = Physics.SphereCastNonAlloc(top, SweepRadius, Vector3.down, hits, top.y - World.WaterY, Layers.SolidMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, 0, n, byDistance);
        for (int i = 0; i < n; i++) {
            Collider c = hits[i].collider;
            if (c == null) continue;
            Vector3 p = hits[i].distance > 0f ? hits[i].point : top;
            p.z = 0f;
            if (c.gameObject.layer == Layers.Ground) { ground = true; return new Vector3(top.x, p.y, 0f); }
            IDamageable d = c.GetComponentInParent<IDamageable>();
            if (d == null || !d.IsAlive || WizardWeapons.IsFriend(d.Owner, source) || struck.Contains(d)) continue;
            struck.Add(d);
            points.Add(p);
            if (struck.Count == MaxHits) return new Vector3(top.x, p.y, 0f);
        }
        return new Vector3(top.x, World.WaterY, 0f);    // nothing solid below: the bolt hits the sea
    }

    static void Zap(IDamageable d, Vector3 p, float amount, Fortress source, bool mayIgnite) {
        if (d == null || !d.IsAlive) return;
        d.ApplyDamage(new DamageInfo(amount, DamageType.Lightning, p, source, Vector3.down * 5f));
        VFX.Sparks(p, WizardWeapons.StormColor, 12);
        if (mayIgnite && d is Block b && b.IsAlive && b.def.flammable && Random.value < IgniteChance) b.Ignite(IgniteSeconds, source);
    }

    // Secondary bolts from the end point to the nearest conductors: metal blocks and enemy commanders.
    static void Chain(Vector3 from, float amount, float radius, Fortress source, List<IDamageable> struck) {
        int n = Physics.OverlapSphereNonAlloc(from, radius, near, Layers.BlockMask | Layers.CommanderMask, QueryTriggerInteraction.Ignore);
        List<(IDamageable target, Vector3 point, float dist)> conductors = new List<(IDamageable, Vector3, float)>();
        HashSet<IDamageable> seen = new HashSet<IDamageable>(struck);
        for (int i = 0; i < n; i++) {
            IDamageable d = near[i] != null ? near[i].GetComponentInParent<IDamageable>() : null;
            if (d == null || !d.IsAlive || WizardWeapons.IsFriend(d.Owner, source) || !seen.Add(d)) continue;
            bool conducts = d is Block b ? b.def.conductive : d is Commander;
            if (!conducts) continue;
            Vector3 p = WizardWeapons.PointOf(d);
            conductors.Add((d, p, (p - from).sqrMagnitude));
        }
        conductors.Sort((a, b) => a.dist.CompareTo(b.dist));
        for (int i = 0; i < conductors.Count && i < MaxChains; i++) {
            VFX.Bolt(from + Vector3.up * 0.3f, conductors[i].point, WizardWeapons.StormColor, 0.22f, 0.3f);
            Zap(conductors[i].target, conductors[i].point, amount, source, false);
        }
    }

    // A thick glowing bolt with a white-hot core, a few side branches, a flash, a crack and the thunder.
    static void Visuals(Vector3 top, Vector3 end, bool ground, int pierced) {
        Color c = WizardWeapons.StormColor;
        VFX.Bolt(top, end, c, 0.8f, 0.4f);
        VFX.Bolt(top, end, new Color(0.92f, 0.97f, 1f), 0.28f, 0.3f);
        for (int i = 0; i < 4; i++) {
            float y = Mathf.Lerp(end.y + 2f, Mathf.Min(top.y, end.y + 18f), Random.value);
            Vector3 a = new Vector3(end.x + Random.Range(-0.4f, 0.4f), y, 0f);
            Vector3 b = a + new Vector3(Random.Range(1.5f, 4f) * (Random.value < 0.5f ? -1f : 1f), -Random.Range(1.5f, 4.5f), 0f);
            VFX.Bolt(a, b, c, 0.14f, 0.22f);
        }
        WizardFX.Burst(end, 40, 11f, 0.14f, 0.6f, c, 0.8f, TexKit.SoftCircle);
        VFX.Sparks(end, c, 30);
        VFX.Ring(end, 2.6f, c, 0.28f, 0.3f);
        if (ground) VFX.Dust(end, 1.4f);
        else if (pierced < MaxHits) VFX.Splash(end, 1.6f);   // found no ground: it ended in the sea
        VFX.LightFlash(end + Vector3.up * 2f, c, 14f, 32f, 0.35f);
        VFX.ScreenFlash(new Color(0.85f, 0.95f, 1f, 0.55f), 0.18f);
        BattleCamera.Shake(0.6f);
        SFX.Play(SoundId.LightningCrack, end, 1f);
        Runner.Delay(0.4f, () => SFX.Play(SoundId.Thunder, end, 1f));
    }
}

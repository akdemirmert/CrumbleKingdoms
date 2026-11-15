using System;
using System.Collections.Generic;
using UnityEngine;

// Shared piercing flight (Loki's daggers, Gungnir). Moves itself every physics step and sweeps a
// sphere along the step, so fast thin projectiles never tunnel. Each block it passes eats part of its
// budget (BlockMatDef.pierceCost) and slows it down; when the budget or speed runs out it sticks
// (Embedded) in the last block, a commander, or the ground.
public class PiercingProjectile : Projectile {
    public float sweepRadius = 0.12f;
    public float budget;                 // 0 = use def.pierce
    public float damage;                 // per block at full speed; 0 = use def.damage
    public float commanderDamage = -1f;  // < 0 = damage * 1.3
    public float slowPerCost = 0.14f;    // fraction of speed lost per unit of pierce cost
    public float stopSpeed = 6f;
    public float embedDepth = 0.3f;
    public float blockPush = 1.2f;       // impulse along the flight given to pierced blocks
    public bool passThroughCommanders;
    public Transform homingTarget;
    public float homingTurnRate;         // degrees per second
    public bool homingAfterApex = true;

    public Action<PiercingProjectile, Block, Vector3> BlockPierced;
    public Action<PiercingProjectile, Vector3, Collider> Stopped;   // collider may be null

    public bool IsStuck => stuck;

    bool stuck;
    readonly HashSet<Collider> hitSet = new HashSet<Collider>();
    readonly RaycastHit[] hits = new RaycastHit[32];
    static readonly RaycastHit[] hitBuffer = new RaycastHit[32];

    protected override void OnLaunch(Vector3 velocity) {
        manualVelocity = velocity;
        if (damage <= 0f) damage = def.damage;
        if (budget <= 0f) budget = def.pierce;
        FaceVelocity(velocity);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished || stuck) return;
        float dt = Time.fixedDeltaTime;
        Vector3 v = manualVelocity + new Vector3(BattleManager.Wind * windFactor, -World.Gravity * gravityScale, 0f) * dt;
        if (homingTarget != null && homingTurnRate > 0f && (!homingAfterApex || v.y < 0f)) {
            Vector3 to = homingTarget.position - transform.position;
            to.z = 0f;
            if (to.sqrMagnitude > 0.01f)
                v = Vector3.RotateTowards(v, to.normalized * v.magnitude, homingTurnRate * Mathf.Deg2Rad * dt, 0f);
        }

        Vector3 p = transform.position;
        Vector3 step = v * dt;
        float len = step.magnitude;
        if (len > 1e-5f) {
            int n = Physics.SphereCastNonAlloc(p, sweepRadius, step / len, hitBuffer, len, Layers.SolidMask, QueryTriggerInteraction.Ignore);
            Array.Copy(hitBuffer, hits, n);                  // own copy: callbacks below may run other queries
            Array.Sort(hits, 0, n, ByDistance.Instance);
            for (int i = 0; i < n; i++) {
                RaycastHit h = hits[i];
                Collider col = h.collider;
                if (col == null || hitSet.Contains(col) || IsOwnTeam(col)) continue;
                hitSet.Add(col);
                Vector3 hp = (h.distance <= 0f && h.point == Vector3.zero) ? p : h.point;
                Vector3 dir = v.normalized;

                if (col.gameObject.layer == Layers.Ground) { StopAt(hp, col, null); return; }

                Commander cm = col.GetComponentInParent<Commander>();
                if (cm != null) {
                    float cd = commanderDamage >= 0f ? commanderDamage : damage * 1.3f;
                    Hit(cm, cd * SpeedFactor(v), DamageType.Pierce, hp, owner, dir * 6f);
                    RaiseImpact(hp);
                    VFX.Sparks(hp, new Color(1f, 0.4f, 0.3f), 10);
                    if (!passThroughCommanders) { StopAt(hp, col, cm.IsAlive ? cm.transform : null); return; }
                    continue;
                }

                Block b = col.GetComponentInParent<Block>();
                if (b == null) {                      // something else that can be hurt (a summoned creature)
                    IDamageable other = col.GetComponentInParent<IDamageable>();
                    if (other != null && other.IsAlive) {
                        Hit(col, damage * SpeedFactor(v), DamageType.Pierce, hp, owner, dir * 4f);
                        RaiseImpact(hp);
                        VFX.Sparks(hp, def.color, 8);
                        budget -= 0.5f;
                    }
                    continue;
                }
                if (!b.IsAlive) continue;
                float cost = b.def.pierceCost;
                if (b.rb != null && !b.rb.isKinematic)
                    b.rb.AddForceAtPosition(dir * blockPush * Mathf.Sqrt(b.rb.mass) * SpeedFactor(v), hp, ForceMode.Impulse);
                Hit(b, damage * SpeedFactor(v), DamageType.Pierce, hp, owner, dir * 4f);
                BlockPierced?.Invoke(this, b, hp);
                RaiseImpact(hp);
                SFX.Play(SoundId.Pierce, hp, 0.55f);
                budget -= cost;
                v *= Mathf.Max(0f, 1f - slowPerCost * cost);
                if (budget <= 0f || v.magnitude < stopSpeed) {
                    StopAt(hp + dir * embedDepth, col, b.IsAlive ? b.transform : null);
                    return;
                }
            }
        }
        manualVelocity = v;
        transform.position = p + step;
        FaceVelocity(v);
    }

    float SpeedFactor(Vector3 v) { return Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(v.magnitude / Mathf.Max(1f, def.maxSpeed))); }

    void StopAt(Vector3 point, Collider col, Transform host) {
        stuck = true;
        Vector3 lastVelocity = manualVelocity;
        manualVelocity = Vector3.zero;
        point.z = Mathf.Clamp(point.z, -0.5f, 0.5f);
        transform.position = point;
        if (host != null) {
            transform.SetParent(host, true);
            gameObject.AddComponent<Embedded>();
        }
        RaiseImpact(point);
        SFX.Play(SoundId.Stick, point, 0.7f);
        Stopped?.Invoke(this, point, col);
        Finish(false);
        if (host != null) return;
        bool inGround = col != null && col.gameObject.layer == Layers.Ground;
        if (inGround) Destroy(gameObject, 8f);
        // The block broke or the commander died under it: nothing to stick in, so it drops
        // (unless the weapon's Stopped callback already released it).
        else if (GetComponent<Rigidbody>() == null) gameObject.AddComponent<Embedded>().Release(lastVelocity * 0.2f);
    }

    class ByDistance : IComparer<RaycastHit> {
        public static readonly ByDistance Instance = new ByDistance();
        public int Compare(RaycastHit a, RaycastHit b) { return a.distance.CompareTo(b.distance); }
    }
}

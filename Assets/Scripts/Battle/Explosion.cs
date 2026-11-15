using System.Collections.Generic;
using UnityEngine;

public enum BlastStyle { Fire, Small, Magic, Void, Holy, Lightning, Keg }

// Radial blasts: damage with linear falloff plus an outward impulse on every body in range.
// Walls protect commanders: every block between the blast and a commander cuts what reaches him.
public static class Explosion {
    const float CoverFactor = 0.4f;   // share of the blast that gets through one block
    static readonly Collider[] buffer = new Collider[512];
    static readonly RaycastHit[] coverHits = new RaycastHit[16];

    // force: velocity change (m/s) given to a 1 kg body at the centre; heavier bodies move less (mass^0.6).
    public static void Create(Vector3 pos, float radius, float damage, float force, Fortress source,
                              DamageType type = DamageType.Explosion, BlastStyle style = BlastStyle.Fire,
                              float upward = 0.35f, bool effects = true) {
        Push(pos, radius, force, upward, out List<(IDamageable target, float falloff, Vector3 point)> hits);
        foreach (var h in hits) {
            if (h.target == null || !h.target.IsAlive) continue;
            Vector3 dir = h.point - pos;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.up;
            h.target.ApplyDamage(new DamageInfo(damage * h.falloff, type, h.point, source, dir * force * h.falloff));
        }
        if (!effects) return;
        VFX.Explosion(pos, radius, style);
        SFX.Play(radius >= 3f ? SoundId.ExplosionBig : SoundId.ExplosionSmall, pos, Mathf.Clamp(radius / 3.5f, 0.5f, 1f));
        BattleCamera.Shake(Mathf.Clamp(radius * 0.11f, 0.12f, 0.75f));
    }

    // Pushes every body in range and returns what can be damaged (falloff 1 at the centre, 0 at the edge).
    // Safe against re-entry: results are copied out of the shared buffer before anything is changed.
    public static void Push(Vector3 pos, float radius, float force, float upward,
                            out List<(IDamageable target, float falloff, Vector3 point)> hits) {
        hits = new List<(IDamageable, float, Vector3)>();
        int n = Physics.OverlapSphereNonAlloc(pos, radius, buffer, Layers.BodiesMask, QueryTriggerInteraction.Ignore);
        List<Collider> cols = new List<Collider>(n);
        for (int i = 0; i < n; i++) cols.Add(buffer[i]);

        HashSet<Rigidbody> seen = new HashSet<Rigidbody>();
        foreach (Collider c in cols) {
            if (c == null) continue;
            Rigidbody rb = c.attachedRigidbody;
            if (rb == null || !seen.Add(rb)) continue;
            Vector3 closest = c.ClosestPoint(pos);
            float d = Vector3.Distance(closest, pos);
            float falloff = Mathf.Clamp01(1f - d / radius);
            if (c.gameObject.layer == Layers.Commander) falloff *= Cover(pos, rb.worldCenterOfMass);
            if (falloff <= 0f) continue;
            if (!rb.isKinematic && force > 0f) {
                Vector3 dir = rb.worldCenterOfMass - pos;
                dir.z *= 0.3f;
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.up;
                dir = (dir + Vector3.up * upward).normalized;
                float k = c.gameObject.layer == Layers.Commander ? 0.6f : 1f;
                rb.AddForceAtPosition(dir * force * falloff * k * Mathf.Pow(rb.mass, 0.4f), closest, ForceMode.Impulse);
            }
            IDamageable dmg = rb.GetComponent<IDamageable>();
            if (dmg != null) hits.Add((dmg, falloff, closest));
        }
    }

    // 1 with nothing in the way, CoverFactor^n with n intact blocks (max 3) between the blast and the target.
    public static float Cover(Vector3 from, Vector3 to) {
        Vector3 ray = to - from;
        float dist = ray.magnitude;
        if (dist < 0.05f) return 1f;
        int n = Physics.RaycastNonAlloc(from, ray / dist, coverHits, dist, Layers.BlockMask, QueryTriggerInteraction.Ignore);
        int blocks = 0;
        for (int i = 0; i < n && blocks < 3; i++) {
            Block b = coverHits[i].collider != null ? coverHits[i].collider.GetComponent<Block>() : null;
            if (b != null && b.IsAlive) blocks++;
        }
        return Mathf.Pow(CoverFactor, blocks);
    }
}

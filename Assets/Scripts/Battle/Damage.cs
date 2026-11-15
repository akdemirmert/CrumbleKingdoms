using System.Collections.Generic;
using UnityEngine;

public struct DamageInfo {
    public float amount;
    public DamageType type;
    public Vector3 point;
    public Vector3 impulse;     // direction * strength of the hit (used to throw debris)
    public Fortress source;     // who caused it (can be null)

    public DamageInfo(float amount, DamageType type, Vector3 point, Fortress source, Vector3 impulse = default) {
        this.amount = amount; this.type = type; this.point = point; this.source = source; this.impulse = impulse;
    }
}

// Blocks and commanders.
public interface IDamageable {
    Fortress Owner { get; }
    bool IsAlive { get; }
    void ApplyDamage(DamageInfo d);
}

// Anything that must finish before the turn can end (projectiles, meteor showers, black holes...).
// Add the component while busy; remove it or destroy the GameObject when done.
public class TurnActor : MonoBehaviour {
    public static readonly HashSet<TurnActor> All = new HashSet<TurnActor>();
    public static int Count => All.Count;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public static void ClearAll() {
        foreach (TurnActor a in new List<TurnActor>(All)) if (a != null) Destroy(a);
        All.Clear();
    }
}

// A solid body of a fortress that is neither one of its blocks nor its commander: a summoned creature such as the
// Lich's bone dragon. Projectiles of its own team pass through it as through their own blocks
// (Projectile.IgnoreOwnTeam), and it never pushes its own fortress's blocks around.
public class TeamBody : MonoBehaviour {
    public static readonly List<TeamBody> All = new List<TeamBody>();
    public Fortress owner;
    public Collider body;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public static TeamBody Add(GameObject go, Fortress owner, Collider body) {
        TeamBody t = go.AddComponent<TeamBody>();
        t.owner = owner;
        t.body = body;
        t.IgnoreOwn();
        return t;
    }

    // No contact with its own side's blocks and commanders (again after a time rewind has rebuilt blocks).
    public void IgnoreOwn() {
        if (owner == null || owner.team == null || body == null) return;
        foreach (Fortress f in owner.team.fortresses) {
            foreach (Block b in f.blocks) if (b != null && b.box != null) Physics.IgnoreCollision(body, b.box);
            if (f.commander != null && f.commander.col != null) Physics.IgnoreCollision(body, f.commander.col);
        }
    }
}

// A projectile (or anything) stuck inside a block or commander. Released when its host breaks.
public class Embedded : MonoBehaviour {
    public float lifetimeAfterRelease = 3f;

    public void Release(Vector3 velocity) {
        if (this == null) return;
        transform.SetParent(null, true);
        ModelKit.SetLayer(gameObject, Layers.Debris);
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        BoxCollider bc = gameObject.AddComponent<BoxCollider>();
        bc.size = Vector3.one * 0.25f;
        Rigidbody rb = gameObject.GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.mass = 0.3f;
        rb.linearVelocity = velocity + Random.insideUnitSphere * 2f;
        rb.angularVelocity = Random.insideUnitSphere * 8f;
        Destroy(gameObject, lifetimeAfterRelease);
        Destroy(this);
    }

    public static void ReleaseAll(Transform host, Vector3 velocity) {
        foreach (Embedded e in host.GetComponentsInChildren<Embedded>()) e.Release(velocity);
    }
}

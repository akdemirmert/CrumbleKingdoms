using System;
using System.Collections.Generic;
using UnityEngine;

// Base class for everything a weapon launches. Handles: turn bookkeeping (TurnActor), extra gravity and
// wind for dynamic rigidbodies, ignoring the owner's own team, mid-air activation (player click or bot
// hint), out-of-bounds/lifetime, and the impact event the camera and the bot listen to.
public abstract class Projectile : MonoBehaviour {
    public static readonly List<Projectile> Active = new List<Projectile>();
    public static event Action<Projectile, Vector3> AnyImpact;

    [System.NonSerialized] public WeaponDef def;
    public Fortress owner;
    public Rigidbody rb;                 // null for kinematic/manual projectiles
    public float gravityScale = 1f;
    public float windFactor = 1f;
    public float maxLifetime = 14f;
    public bool cameraFollows = true;    // the camera tracks projectiles that have this set
    public float age;
    public bool activated, finished, impacted;

    // Bot hint: activate when the projectile crosses this x (travel direction aware). NaN = never.
    public float botActivateX = float.NaN;
    protected int travelDir = 1;

    TurnActor actor;

    public virtual bool CanActivate => def != null && def.CanActivate && !activated && !finished;
    public Vector3 Velocity => rb != null ? rb.linearVelocity : manualVelocity;
    protected Vector3 manualVelocity;

    // Called by WeaponFactory right after the component is added.
    public void Launch(Fortress owner, WeaponDef def, Vector3 position, Vector3 velocity) {
        this.owner = owner;
        this.def = def;
        gravityScale = def.gravityScale;
        windFactor = def.windFactor;
        travelDir = velocity.x >= 0f ? 1 : -1;
        transform.position = position;
        actor = gameObject.AddComponent<TurnActor>();
        Active.Add(this);
        OnLaunch(velocity);
        IgnoreOwnTeam();
    }

    // Subclasses build their model/colliders/rigidbody here and set the initial velocity.
    protected abstract void OnLaunch(Vector3 velocity);

    // Mid-air click (player) or bot hint.
    protected virtual void OnActivate() { }

    // Fell out of the world or lived too long. Default: end quietly.
    protected virtual void OnLost() { Finish(true); }

    public void TryActivate() {
        if (!CanActivate) return;
        activated = true;
        OnActivate();
    }

    protected virtual void FixedUpdate() {
        if (finished) return;
        age += Time.fixedDeltaTime;
        if (rb != null && !rb.isKinematic) {
            Vector3 extra = new Vector3(BattleManager.Wind * windFactor, -World.Gravity * (gravityScale - 1f), 0f);
            rb.AddForce(extra, ForceMode.Acceleration);
        }
        if (!float.IsNaN(botActivateX) && CanActivate && (transform.position.x - botActivateX) * travelDir >= 0f) TryActivate();
        Vector3 p = transform.position;
        if (p.y < World.KillY || Mathf.Abs(p.x) > World.ArenaHalfWidth || p.y > 400f || age > maxLifetime) OnLost();
    }

    // End this projectile's part of the turn. destroyObject = remove it (after delay) as well.
    public void Finish(bool destroyObject, float delay = 0f) {
        if (!finished) {
            finished = true;
            Active.Remove(this);
            if (actor != null) Destroy(actor);
        }
        if (destroyObject) Destroy(gameObject, delay);
    }

    protected void RaiseImpact(Vector3 point) {
        impacted = true;
        AnyImpact?.Invoke(this, point);
    }

    protected virtual void OnDestroy() { Active.Remove(this); }

    // ------------------------------------------------------------------ helpers for subclasses
    public bool IsOwnTeam(Component c) {
        if (owner == null || c == null) return false;
        Block b = c.GetComponentInParent<Block>();
        if (b != null) return b.fortress != null && b.fortress.team == owner.team;
        Commander cm = c.GetComponentInParent<Commander>();
        if (cm != null) return cm.fortress != null && cm.fortress.team == owner.team;
        return false;
    }

    protected void IgnoreOwnTeam() {
        if (owner == null || owner.team == null) return;
        Collider[] mine = GetComponentsInChildren<Collider>();
        if (mine.Length == 0) return;
        foreach (Fortress f in owner.team.fortresses) {
            foreach (Block b in f.blocks) if (b != null && b.box != null) foreach (Collider m in mine) Physics.IgnoreCollision(m, b.box);
            if (f.commander != null && f.commander.col != null) foreach (Collider m in mine) Physics.IgnoreCollision(m, f.commander.col);
        }
    }

    // Adds a dynamic rigidbody set up for projectiles (continuous collision, interpolation, own gravity handled).
    protected Rigidbody AddBody(float mass, Vector3 velocity, bool useGravity = true) {
        rb = gameObject.GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.useGravity = useGravity;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.constraints = RigidbodyConstraints.FreezePositionZ;
        rb.linearVelocity = velocity;
        return rb;
    }

    // Points the model's +X axis along the flight direction (side view).
    protected void FaceVelocity(Vector3 v) {
        if (v.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
    }

    // Damage helper for direct hits.
    protected static void Hit(Component target, float amount, DamageType type, Vector3 point, Fortress source, Vector3 impulse) {
        if (target == null) return;
        IDamageable d = target.GetComponentInParent<IDamageable>();
        if (d != null && d.IsAlive) d.ApplyDamage(new DamageInfo(amount, type, point, source, impulse));
    }
}

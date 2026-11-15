using System;
using System.Collections.Generic;
using UnityEngine;

// What a controller (player or bot) decided to shoot.
public class ShotCommand {
    public int slot;
    public Vector3 velocity;
    public bool fromBot;
    public Vector3 target;                 // bots: the point they aimed at
    public Fortress targetFortress;
    public float activateX = float.NaN;    // bots: mid-air activation x (see Projectile.botActivateX)
}

public delegate void WeaponFireFn(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity);
public delegate GameObject WeaponModelFn(Transform parent);

// Weapons register themselves (see the [RuntimeInitializeOnLoadMethod] in each weapon file), so this
// file never needs to know the concrete projectile classes. Unregistered weapons use a simple fallback.
public static class WeaponFactory {
    static readonly Dictionary<WeaponId, WeaponFireFn> fireFns = new Dictionary<WeaponId, WeaponFireFn>();
    static readonly Dictionary<WeaponId, WeaponModelFn> modelFns = new Dictionary<WeaponId, WeaponModelFn>();

    // The shot being fired right now (null outside Fire). Projectiles read the bot hint from it.
    public static ShotCommand CurrentShot { get; private set; }

    public static void Register(WeaponId id, WeaponFireFn fire, WeaponModelFn model) {
        fireFns[id] = fire;
        modelFns[id] = model;
    }

    public static bool IsRegistered(WeaponId id) { return fireFns.ContainsKey(id); }

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity, ShotCommand cmd) {
        CurrentShot = cmd;
        try {
            if (fireFns.TryGetValue(def.id, out WeaponFireFn fn)) fn(owner, def, origin, velocity);
            else Spawn<FallbackProjectile>("Fallback " + def.name, owner, def, origin, velocity);
        } finally {
            CurrentShot = null;
        }
    }

    // Visual-only model (no colliders) for menus and icons. Model axis: +X = forward.
    public static GameObject CreateModel(WeaponId id, Transform parent) {
        if (modelFns.TryGetValue(id, out WeaponModelFn fn) && fn != null) return fn(parent);
        GameObject root = ModelKit.Root(id.ToString(), parent);
        ModelKit.Part(root.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.6f, Mats.Lit(WeaponDefs.Get(id).color, 0.4f));
        return root;
    }

    // Creates a projectile GameObject, lets `setup` configure the component, then launches it.
    public static T Spawn<T>(string name, Fortress owner, WeaponDef def, Vector3 position, Vector3 velocity, Action<T> setup = null) where T : Projectile {
        GameObject go = new GameObject(name) { layer = Layers.Projectile };
        go.transform.position = position;
        T p = go.AddComponent<T>();
        setup?.Invoke(p);
        p.Launch(owner, def, position, velocity);
        if (CurrentShot != null && def.CanActivate) p.botActivateX = CurrentShot.activateX;
        return p;
    }
}

// Used only if a weapon's script is missing: a ball that explodes on contact.
public class FallbackProjectile : Projectile {
    protected override void OnLaunch(Vector3 velocity) {
        WeaponFactory.CreateModel(def.id, transform);
        SphereCollider c = gameObject.AddComponent<SphereCollider>();
        c.radius = 0.35f;
        AddBody(2f, velocity);
    }

    void OnCollisionEnter(Collision c) {
        if (finished) return;
        Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        RaiseImpact(p);
        Explosion.Create(p, Mathf.Max(1.8f, def.radius), Mathf.Max(30f, def.damage), Mathf.Max(6f, def.force), owner);
        Finish(true);
    }

    protected override void OnLost() { Finish(true); }
}

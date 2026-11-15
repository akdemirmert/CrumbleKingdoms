using UnityEngine;

// The Soldier's arsenal: grappling hook, fuse bomb, kamikaze drone and airstrike.
// This file registers the four weapons and builds the fuse bomb's model (visual only, +X = forward, ~1 m).
// Behaviours: SoldierWeaponsHook.cs, SoldierWeaponsBombs.cs, SoldierWeaponsDrone.cs, SoldierWeaponsAirstrike.cs;
// effects: SoldierWeaponsFx.cs.
static class SoldierWeapons {
    public const float BombFuseLength = 0.2f;     // full length of the fuse cord
    public const float MaxCommanderShove = 5f;    // m/s, see LimitShove

    static readonly Color Brass = WeaponDefs.Hex("B8904E"), Iron = WeaponDefs.Hex("3A3F47"), Twine = WeaponDefs.Hex("D6BE8A");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() {
        WeaponFactory.Register(WeaponId.GrapplingHook, GrapplingHook.Fire, GrapplingHook.Model);
        WeaponFactory.Register(WeaponId.Bomb, FireBomb, BombModel);
        WeaponFactory.Register(WeaponId.Drone, SoldierDrone.Fire, SoldierDrone.Model);
        WeaponFactory.Register(WeaponId.Airstrike, AirstrikeFlare.Fire, AirstrikeFlare.Model);
    }

    // ------------------------------------------------------------------ fire
    static void FireBomb(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        WeaponFactory.Spawn<FuseBombProjectile>("Fuse bomb", owner, def, origin, velocity);
    }

    // ------------------------------------------------------------------ models
    public static GameObject BombModel(Transform parent) { return BuildBomb(parent, out _, out _, out _); }

    // The fuse bomb, returning the parts the projectile animates: the fuse pivot (scaled down as it
    // burns), the glowing ember at its tip and the warning band that blinks.
    public static GameObject BuildBomb(Transform parent, out Transform fuse, out Transform ember, out Renderer band) {
        GameObject root = ModelKit.Root("Fuse bomb", parent);
        Transform t = root.transform;
        ModelKit.Part(t, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.6f, Mats.Lit(Iron, 0.6f, 0.35f));
        band = ModelKit.Part(t, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.615f, 0.022f, 0.615f), BandMaterial(false)).GetComponent<Renderer>();
        ModelKit.Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f), new Vector3(0.17f, 0.045f, 0.17f), Mats.Lit(Brass, 0.5f, 0.6f));
        fuse = ModelKit.Root("Fuse", t).transform;
        fuse.localPosition = new Vector3(0f, 0.34f, 0f);
        fuse.localRotation = Quaternion.Euler(0f, 0f, 28f);   // leans back
        ModelKit.Part(fuse, PrimitiveType.Cylinder, new Vector3(0f, BombFuseLength * 0.5f, 0f), new Vector3(0.028f, BombFuseLength * 0.5f, 0.028f),
                      Mats.Lit(Twine, 0.1f));
        ember = ModelKit.Part(t, PrimitiveType.Sphere, FuseTip(fuse, 1f), Vector3.one * 0.06f,
                              Mats.Emissive(WeaponDefs.Hex("FFC060"), new Color(6f, 2.6f, 0.7f))).transform;
        return root;
    }

    // Model-space point at `left` (0..1) of the fuse's length.
    public static Vector3 FuseTip(Transform fuse, float left) {
        return fuse.localPosition + fuse.localRotation * Vector3.up * (BombFuseLength * left);
    }

    public static Material BandMaterial(bool lit) {
        return lit ? Mats.Emissive(WeaponDefs.Hex("FF4030"), new Color(5f, 0.6f, 0.3f))
                   : Mats.Emissive(WeaponDefs.Hex("5E1712"), new Color(0.25f, 0.02f, 0.01f));
    }

    // ------------------------------------------------------------------ shared helpers
    // First contact of a collision; falls back to `fallback` if Unity reports no contact points.
    // The normal points out of the surface that was hit, towards the projectile.
    public static void FirstContact(Collision c, Vector3 fallback, out Vector3 point, out Vector3 normal) {
        if (c.contactCount > 0) {
            ContactPoint cp = c.GetContact(0);
            point = cp.point;
            normal = cp.normal;
        } else {
            point = fallback;
            normal = Vector3.up;
        }
    }

    // Our projectiles outweigh a commander (2 kg): a direct hit would launch him off the tower into the
    // sea. Capping the shove keeps hits hurting through damage and blasts instead of an instant ring-out.
    public static void LimitShove(Collision c) {
        Commander cm = c.collider != null ? c.collider.GetComponentInParent<Commander>() : null;
        if (cm == null || cm.rb == null || cm.rb.isKinematic) return;
        cm.rb.linearVelocity = Vector3.ClampMagnitude(cm.rb.linearVelocity, MaxCommanderShove);
    }
}

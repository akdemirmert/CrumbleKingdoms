using System.Collections.Generic;
using UnityEngine;

// Bat Swarm. A dark cloud of flapping bats flies along the aimed arc (VampireBatSwarm). It releases its bats on
// a click (or the bot's hint), when it touches anything, or as soon as it comes within TriggerDistance of an
// enemy fortress. Each bat (VampireBat) picks a nearby enemy block - now and then (two bats at most) the enemy
// commander -
// scatters for a moment, then dives at it with light homing, bites (def.damage, x1.5 on a commander) and
// bursts into a puff. Every bat is a Projectile, so the turn waits for the last one; bats that find nothing
// to bite flutter off and vanish.
public class VampireBatSwarm : Projectile {
    const int BatCount = 10;
    const float BatSize = 0.5f;
    const float SweepRadius = 0.45f;
    const float TriggerDistance = 6f;      // releases this close to an enemy fortress
    const float TargetReach = 16f;         // the bats only go for blocks this close to the release point
    const float CommanderChance = 0.3f;    // share of the bats that go for an enemy commander in reach...
    const int MaxCommanderBats = 2;        // ...but no more than this many per swarm (they bite through his walls)
    static readonly Collider[] near = new Collider[256];

    Transform cloud;
    VampireBatRig[] bats;
    Vector4[] orbits;                       // per bat: radius x, radius y, angular speed, phase
    Light glow;
    AudioSource flutter;
    readonly List<Bounds> enemies = new List<Bounds>();
    bool released;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        VampireSounds.Play(VampireSounds.Screech, origin, 0.6f, 1.15f);
        VFX.Burst(origin, 16, 2.5f, 0.7f, 0.7f, VampireKit.Smoke, -0.1f, false, TexKit.Smoke);
        WeaponFactory.Spawn<VampireBatSwarm>("Bat Swarm", owner, def, origin, velocity);
    }

    // Three bats in a tight group (menus and icons).
    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Bat Swarm", parent);
        Pose(VampireKit.Bat(root.transform, 0.95f), new Vector3(0.05f, 0.12f, -0.05f), 22f, -8f);
        Pose(VampireKit.Bat(root.transform, 0.62f), new Vector3(-0.34f, -0.14f, 0.05f), -12f, 12f);
        Pose(VampireKit.Bat(root.transform, 0.55f), new Vector3(0.36f, -0.24f, 0.08f), 38f, -18f);
        return root;
    }

    static void Pose(VampireBatRig bat, Vector3 at, float wings, float roll) {
        bat.transform.localPosition = at;
        bat.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
        bat.Pose(wings);
    }

    protected override void OnLaunch(Vector3 velocity) {
        manualVelocity = velocity;
        maxLifetime = 9f;
        cloud = ModelKit.Root("Cloud", transform).transform;
        bats = new VampireBatRig[BatCount];
        orbits = new Vector4[BatCount];
        for (int i = 0; i < BatCount; i++) {
            bats[i] = VampireKit.Bat(cloud, BatSize * Random.Range(0.8f, 1.15f));
            bats[i].flapRate = Random.Range(34f, 46f);
            float turn = Random.Range(3f, 5.5f) * (Random.value < 0.5f ? -1f : 1f);
            orbits[i] = new Vector4(Random.Range(0.5f, 1.4f), Random.Range(0.35f, 0.9f), turn, Random.Range(0f, Mathf.PI * 2f));
        }
        UpdateCloud();
        VFX.Emitter(transform, VampireKit.Smoke, 1.1f, 22f);                 // the dark haze around the swarm
        VFX.Emitter(transform, new Color(1f, 0.15f, 0.22f), 0.09f, 18f);    // red glints, like eyes in the dark
        glow = VampireKit.Glow(transform, VampireKit.BloodGlow, 2.2f, 5f);
        flutter = VampireSounds.Loop(VampireSounds.Flutter, transform, 0.45f);
        if (BattleManager.I != null && owner != null)
            foreach (Fortress f in BattleManager.I.EnemiesOf(owner)) enemies.Add(f.GetBounds());
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();   // may activate (bot hint) or lose the swarm, both of which release it
        if (finished) return;
        float dt = Time.fixedDeltaTime;
        Vector3 p = transform.position;
        Vector3 v = manualVelocity + new Vector3(BattleManager.Wind * windFactor, -World.Gravity * gravityScale, 0f) * dt;
        Vector3 step = v * dt;
        if (VampireKit.Sweep(this, p, SweepRadius, step, out RaycastHit hit)) {   // about to touch something
            Release(hit.point - step.normalized * 0.5f);
            return;
        }
        manualVelocity = v;
        transform.position = p + step;
        if (NearEnemy(transform.position) || transform.position.y < World.WaterY + 0.4f) Release(transform.position);
    }

    void Update() { if (!released) UpdateCloud(); }

    protected override void OnActivate() { Release(transform.position); }

    protected override void OnLost() { Release(transform.position); }

    protected override void OnDestroy() {
        base.OnDestroy();
        SFX.Stop(flutter);
    }

    bool NearEnemy(Vector3 p) {
        foreach (Bounds b in enemies) if (b.SqrDistance(p) < TriggerDistance * TriggerDistance) return true;
        return false;
    }

    // Each bat circles the centre on its own tilted ellipse, leaning into the turn.
    void UpdateCloud() {
        float t = Time.time;
        for (int i = 0; i < bats.Length; i++) {
            Vector4 o = orbits[i];
            float a = o.w + o.z * t;
            Transform b = bats[i].transform;
            b.localPosition = new Vector3(Mathf.Cos(a) * o.x, Mathf.Sin(a) * o.y + Mathf.Sin(a * 2.3f + i) * 0.15f, Mathf.Sin(a + i) * 0.6f);
            b.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Cos(a) * o.z * 5f);
        }
    }

    // ------------------------------------------------------------------ release
    // The cloud bursts: every bat becomes a projectile of its own, starting where it was in the cloud.
    void Release(Vector3 at) {
        if (released) return;
        released = true;
        at.z = 0f;
        List<Block> blocks = new List<Block>();
        List<Commander> commanders = new List<Commander>();
        FindTargets(at, blocks, commanders);
        int onCommander = 0;
        for (int i = 0; i < bats.Length; i++) {
            Transform b = bats[i].transform;
            Vector3 from = new Vector3(b.position.x, b.position.y, 0f);
            Vector3 outward = from - at;
            if (outward.sqrMagnitude < 0.01f) outward = Random.insideUnitCircle;
            Vector3 velocity = manualVelocity * 0.35f + outward.normalized * Random.Range(4f, 7f);
            Component target = PickTarget(blocks, commanders, at, onCommander < MaxCommanderBats);
            if (target is Commander) onCommander++;
            float depth = b.position.z, size = b.localScale.x;
            WeaponFactory.Spawn<VampireBat>("Bat", owner, def, from, velocity, bat => {
                bat.target = target;
                bat.depth = depth;
                bat.size = size;
            });
        }
        ReleaseEffects(at);
        SFX.Stop(flutter);
        flutter = null;
        if (glow != null) Destroy(glow.gameObject);
        Destroy(cloud.gameObject);   // the bats fly on as projectiles
        Finish(true);
    }

    // Enemy blocks within TargetReach of the release point and enemy commanders a little further.
    void FindTargets(Vector3 at, List<Block> blocks, List<Commander> commanders) {
        int n = Physics.OverlapSphereNonAlloc(at, TargetReach, near, Layers.BlockMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++) {
            Block b = near[i] != null ? near[i].GetComponent<Block>() : null;
            if (b != null && b.IsAlive && !IsOwnTeam(b)) blocks.Add(b);
        }
        if (BattleManager.I == null || owner == null) return;
        foreach (Fortress f in BattleManager.I.EnemiesOf(owner))
            if (f.CommanderAlive && Vector3.Distance(f.commander.AimPoint, at) < TargetReach + 4f) commanders.Add(f.commander);
    }

    // A commander now and then (while the swarm may still send one), otherwise the nearest of three random blocks
    // (bats go for what is close); null when neither is in reach.
    static Component PickTarget(List<Block> blocks, List<Commander> commanders, Vector3 at, bool commanderAllowed) {
        if (commanderAllowed && commanders.Count > 0 && Random.value < CommanderChance)
            return commanders[Random.Range(0, commanders.Count)];
        Block best = null;
        float bestDistance = float.MaxValue;
        for (int k = 0; k < 3 && blocks.Count > 0; k++) {
            Block b = blocks[Random.Range(0, blocks.Count)];
            float d = (b.transform.position - at).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = b; }
        }
        return best;
    }

    static void ReleaseEffects(Vector3 at) {
        VFX.Burst(at, 28, 4f, 0.9f, 0.8f, VampireKit.Smoke, -0.05f, false, TexKit.Smoke);
        VFX.Sparks(at, VampireKit.BloodGlow, 14);
        VFX.Ring(at, 2.2f, VampireKit.BloodGlow, 0.45f, 0.25f);
        VFX.LightFlash(at, VampireKit.BloodGlow, 6f, 8f, 0.35f);
        VampireSounds.Play(VampireSounds.Screech, at, 0.8f);
        Runner.Delay(0.08f, () => VampireSounds.Play(VampireSounds.Screech, at, 0.6f, 1.25f));
        Runner.Delay(0.17f, () => VampireSounds.Play(VampireSounds.Screech, at, 0.55f, 0.85f));
        BattleCamera.Shake(0.15f);
    }
}

// One bat of the swarm. It flies itself (no rigidbody): a short scatter, then a homing dive at its target; a
// sphere sweep each step finds what it bites. A bat going for a commander bites its way through the blocks in
// front of him (CommanderPierce of wall, like a piercing weapon's budget). Without a target it flutters upwards
// and vanishes.
public class VampireBat : Projectile {
    const float Radius = 0.16f;
    const float ScatterTime = 0.32f;       // drifting out of the cloud before the dive
    const float DiveSpeed = 17f, Accel = 26f, TurnRate = 300f;   // m/s, m/s², degrees per second
    const float CommanderBonus = 1.5f;
    const float CommanderPierce = 3.2f;    // two stone blocks or three wooden ones (BlockMatDef.pierceCost)
    const float FreeFlightTime = 1.2f;     // a bat with nothing to bite is gone after this
    const float RetargetReach = 3.5f;
    static readonly Collider[] near = new Collider[64];
    static float lastBiteSound;

    public Component target;               // a Block or a Commander (null: nothing in reach)
    public float depth, size = 0.5f;       // where it flew in the cloud (z, visual only) and its size

    Transform model;
    bool hasTarget;
    Vector3 aim;
    float roll, wobble, freeSince = -1f, pierceLeft;
    readonly HashSet<Collider> bitten = new HashSet<Collider>();   // blocks it has bitten through

    public override bool CanActivate => false;

    protected override void OnLaunch(Vector3 velocity) {
        manualVelocity = velocity;
        maxLifetime = 5f;
        pierceLeft = target is Commander ? CommanderPierce : 0f;
        model = ModelKit.Root("Model", transform).transform;
        model.localPosition = new Vector3(0f, 0f, Mathf.Clamp(depth, -0.6f, 0.6f));
        VampireBatRig rig = VampireKit.Bat(model, size);
        rig.flapRate = 50f;
        VFX.Trail(model, new Color(0.85f, 0.08f, 0.15f, 0.55f), 0.1f, 0.22f);
        wobble = Random.Range(0f, Mathf.PI * 2f);
        hasTarget = target != null;
        aim = transform.position + velocity;
        roll = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f;
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished) return;
        float dt = Time.fixedDeltaTime;
        Vector3 p = transform.position;
        Vector3 v = Steer(manualVelocity, p, dt);
        Vector3 step = v * dt;
        if (VampireKit.Sweep(this, p, Radius, step, out RaycastHit hit, bitten) && !BiteThrough(hit, v)) { Bite(hit, v); return; }
        manualVelocity = v;
        transform.position = p + step;
        if (transform.position.y < World.WaterY) { Splash(); return; }
        if (!hasTarget && age > ScatterTime) {
            if (freeSince < 0f) freeSince = age;
            else if (age - freeSince > FreeFlightTime) Vanish();
        }
    }

    // Scatter (slowing down), then turn towards the target at a limited rate while speeding up to the dive speed.
    // A sideways flutter makes the flight erratic, calming down near the target so the bite lands.
    Vector3 Steer(Vector3 v, Vector3 p, float dt) {
        if (age < ScatterTime) return v * (1f - 2.2f * dt);
        if (hasTarget) aim = TargetPoint();
        if (!hasTarget) return v + Vector3.up * 5f * dt;
        Vector3 to = aim - p;
        to.z = 0f;
        Vector3 dir = v.sqrMagnitude > 0.01f ? v.normalized : to.normalized;
        if (to.sqrMagnitude > 0.01f) dir = Vector3.RotateTowards(dir, to.normalized, TurnRate * Mathf.Deg2Rad * dt, 0f);
        float speed = Mathf.MoveTowards(v.magnitude, DiveSpeed, Accel * dt);
        Vector3 side = new Vector3(-dir.y, dir.x, 0f);
        float flutter = Mathf.Sin(age * 16f + wobble) * 1.5f * Mathf.Clamp01(to.magnitude / 4f);
        return dir * speed + side * flutter;
    }

    // The target's centre (a commander's chest). If the target is gone, the nearest enemy block around where it
    // was; if there is none either, the bat has nothing left to bite.
    Vector3 TargetPoint() {
        if (target is Commander cm && cm != null && cm.IsAlive) return cm.AimPoint;
        if (target is Block b && b != null && b.IsAlive) return b.transform.position;
        target = Retarget(aim);
        hasTarget = target != null;
        return hasTarget ? target.transform.position : aim;
    }

    Block Retarget(Vector3 around) {
        int n = Physics.OverlapSphereNonAlloc(around, RetargetReach, near, Layers.BlockMask, QueryTriggerInteraction.Ignore);
        Block best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < n; i++) {
            Block b = near[i] != null ? near[i].GetComponent<Block>() : null;
            if (b == null || !b.IsAlive || IsOwnTeam(b)) continue;
            float d = (b.transform.position - around).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = b; }
        }
        return best;
    }

    // On its way to a living commander a bat bites through a block in front of him while it has budget left for it,
    // and flies on.
    bool BiteThrough(RaycastHit hit, Vector3 v) {
        if (!(target is Commander cm) || cm == null || !cm.IsAlive) return false;
        Block b = hit.collider.GetComponentInParent<Block>();
        if (b == null || !b.IsAlive || pierceLeft < b.def.pierceCost) return false;
        pierceLeft -= b.def.pierceCost;
        bitten.Add(hit.collider);
        Vector3 point = new Vector3(hit.point.x, hit.point.y, 0f);
        Hit(b, def.damage, DamageType.Pierce, point, owner, v.normalized * 2f);
        VFX.Sparks(point, VampireKit.BloodGlow, 4);
        return true;
    }

    // Whatever it flew into takes the bite (the ground only gets the puff); a commander bitten without biting through
    // a wall first takes CommanderBonus.
    void Bite(RaycastHit hit, Vector3 v) {
        Vector3 point = new Vector3(hit.point.x, hit.point.y, 0f);
        Collider col = hit.collider;
        if (col.gameObject.layer != Layers.Ground) {
            bool commander = col.GetComponentInParent<Commander>() != null;
            Hit(col, def.damage * (commander && bitten.Count == 0 ? CommanderBonus : 1f), DamageType.Pierce, point, owner, v.normalized * 2f);
        }
        RaiseImpact(point);
        if (Time.time > lastBiteSound + 0.06f) {   // ten bites at once would drown everything else
            lastBiteSound = Time.time;
            VampireSounds.Play(VampireSounds.Bite, point, 0.7f, Random.Range(0.9f, 1.2f));
        }
        Puff(point);
        VFX.Sparks(point, VampireKit.BloodGlow, 6);
        Finish(true);
    }

    void Splash() {
        Vector3 p = transform.position;
        VFX.Splash(new Vector3(p.x, World.WaterY, 0f), 0.3f);
        Finish(true);
    }

    void Vanish() {
        Puff(transform.position);
        Finish(true);
    }

    protected override void OnLost() { Vanish(); }

    static void Puff(Vector3 p) {
        VFX.Burst(p, 12, 2.2f, 0.45f, 0.55f, VampireKit.Smoke, -0.15f, false, TexKit.Smoke);
    }

    // Upright while it drifts, head first (along the flight) once it dives.
    void Update() {
        if (finished || model == null) return;
        Vector3 v = manualVelocity;
        if (v.sqrMagnitude < 0.1f) return;
        float headFirst = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg - 90f;
        float want = Mathf.LerpAngle(0f, headFirst, Mathf.Clamp01(v.magnitude / DiveSpeed));
        roll = Mathf.LerpAngle(roll, want, 10f * Time.deltaTime);
        model.localRotation = Quaternion.Euler(0f, 0f, roll);
    }
}

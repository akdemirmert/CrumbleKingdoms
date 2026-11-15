using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Skeleton Legion: the Lich throws a cracked tombstone. Where it comes down it plants itself and three skeletons
// claw their way out of the ground:
//  * on the enemy's island (or against his fortress) they run to the nearest wall on foot, climb it (one to the
//    bottom course, one half way up, one to the top) and each tears a block out of the wall (def.damage and a
//    yank outwards), then falls apart. A skeleton that finds the commander's room open at its height leaps in
//    and strikes him instead (CommanderDamage);
//  * anywhere they cannot walk to an enemy (their own side, an island of nobody, across the sea) they rise,
//    rattle and crumble: a miss. In the sea the tombstone just sinks.
// A block falling on a skeleton or a blast next to it turns it to bones, so a wall torn out above can bury the
// skeleton below. The skeletons are projectiles of their own (they hold the turn and the camera follows them).
public class Tombstone : Projectile {
    const float Mass = 3f;
    static readonly Vector3 Size = new Vector3(0.5f, 0.75f, 0.18f);

    Transform model;
    float tumble;
    bool landed;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        SFX.Play(SoundId.LaunchHeavy, origin, 0.55f, 1.05f);
        WeaponFactory.Spawn<Tombstone>("Tombstone", owner, def, origin, velocity);
    }

    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Tombstone", parent);
        Build(root.transform).transform.localPosition = new Vector3(0f, -0.32f, 0f);
        return root;
    }

    // Bots throw it just in front of the wall that faces them (on the ground, so the skeletons have a short run).
    public static Vector3 BotAim(Fortress shooter, Fortress target) {
        Bounds b = target.GetBounds();
        bool fromLeft = shooter.transform.position.x < b.center.x;
        float wall = fromLeft ? b.min.x : b.max.x;
        float x = wall + (fromLeft ? -2.2f : 2.2f);
        if (LichKit.GroundAt(x, out float y) && y > World.WaterY + 0.5f) return new Vector3(x, y + 0.3f, 0f);
        return new Vector3(wall + (fromLeft ? 0.3f : -0.3f), target.transform.position.y + 1f, 0f);   // no shore in front: hit the wall low
    }

    protected override void OnLaunch(Vector3 velocity) {
        maxLifetime = 10f;
        model = Build(transform).transform;
        gameObject.AddComponent<BoxCollider>().size = Size;
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;   // only the model tumbles
        VFX.Emitter(transform, new Color(0.55f, 1f, 0.4f, 0.8f), 0.12f, 14f);                         // a wisp of soul
        LichSounds.WarmUp();
    }

    void Update() {
        if (landed || finished) return;
        tumble += -travelDir * 380f * Time.deltaTime;
        model.localRotation = Quaternion.Euler(0f, 0f, tumble);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished || landed || transform.position.y > World.WaterY) return;
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);   // into the sea: no grave to rise from
        RaiseImpact(p);
        VFX.Splash(p, 0.9f);
        SFX.Play(SoundId.Splash, p, 0.8f);
        Finish(true);
    }

    void OnCollisionEnter(Collision c) {
        if (finished || landed) return;
        SoldierWeapons.FirstContact(c, transform.position, out Vector3 point, out _);
        SoldierWeapons.LimitShove(c);
        Block hit = c.collider.GetComponentInParent<Block>();
        Land(new Vector3(point.x, point.y, 0f), hit != null && hit.IsAlive ? hit.fortress : null);
    }

    protected override void OnLost() { Finish(true); }

    // Down it comes. Against a fortress it drops to the foot of that wall; on open ground it stands where it fell.
    void Land(Vector3 point, Fortress against) {
        landed = true;
        RaiseImpact(point);
        LichKit.Freeze(this);
        cameraFollows = false;
        maxLifetime = age + 20f;
        SFX.PlayImpact(BlockMaterial.Stone, point, 0.8f);
        Fortress target = against != null && against.team != owner.team ? against : LichKit.NearestEnemy(owner, point.x);
        float x = point.x;
        if (against != null && target == against) {
            Bounds b = against.GetBounds();
            bool leftSide = point.x < b.center.x;
            x = leftSide ? b.min.x - 0.6f : b.max.x + 0.6f;
        }
        bool ground = LichKit.GroundAt(x, out float y);
        if (!ground) {                                        // nothing to stand on below (a wall over the sea)
            VFX.Dust(point, 0.6f, new Color(0.6f, 0.6f, 0.58f, 0.5f));
            Finish(true);
            return;
        }
        Vector3 grave = new Vector3(x, y, 0f);
        transform.position = grave + Vector3.up * (Size.y * 0.5f - 0.12f);   // planted, sunk in a little
        model.localRotation = Quaternion.Euler(0f, 0f, -travelDir * 6f);
        StartCoroutine(Rise(grave, target != null && Reachable(grave.x, target) ? target : null));
    }

    // The earth splits and three skeletons claw their way up (a little apart, away from the wall they will climb).
    IEnumerator Rise(Vector3 grave, Fortress target) {
        LichSounds.Play(LichSounds.Rise, grave, 0.9f);
        VFX.Dust(grave, 1.2f, new Color(0.45f, 0.36f, 0.27f, 0.75f));
        LichKit.SoulBurst(grave + Vector3.up * 0.2f, 0.8f);
        BattleCamera.Shake(0.15f);
        int towards = target != null ? (target.GetBounds().center.x > grave.x ? 1 : -1) : travelDir;
        for (int i = 0; i < 3; i++) {
            Vector3 at = grave + Vector3.right * (-towards * (0.55f + 0.5f * i));
            if (!LichKit.GroundAt(at.x, out float gy) || Mathf.Abs(gy - grave.y) > 1f) at.x = grave.x;
            else at.y = gy;
            int level = 2 - i;                                // the first one out goes highest
            WeaponFactory.Spawn<SkeletonWarrior>("Skeleton", owner, def, at, Vector3.zero, s => s.Setup(target, level, towards));
            yield return new WaitForSeconds(0.22f);
        }
        Finish(true, 6f);
        StartCoroutine(SinkAway());
    }

    // The empty grave settles back into the ground.
    IEnumerator SinkAway() {
        yield return new WaitForSeconds(4f);
        Vector3 from = transform.position;
        for (float t = 0f; t < 1.5f; t += Time.deltaTime) {
            transform.position = from + Vector3.down * (t / 1.5f) * Size.y;
            yield return null;
        }
    }

    // Can a skeleton walk from x to the fortress without crossing the sea?
    static bool Reachable(float x, Fortress target) {
        Bounds b = target.GetBounds();
        float to = x < b.center.x ? b.min.x : b.max.x;
        if (x >= b.min.x && x <= b.max.x) return true;
        float step = 0.5f * Mathf.Sign(to - x);
        for (float sx = x; (to - sx) * step > 0f; sx += step)
            if (!LichKit.GroundAt(sx, out float y) || y < World.WaterY + 0.3f) return false;
        return true;
    }

    // ------------------------------------------------------------------ model
    // A weathered grey tombstone with a rounded top, a carved skull that glows faintly green, a crack and moss at
    // its foot. Its centre is the origin.
    static GameObject Build(Transform parent) {
        GameObject root = ModelKit.Root("Tombstone", parent);
        Transform r = root.transform;
        Material stone = Mats.Lit(new Color(0.5f, 0.51f, 0.53f), 0.1f), dark = LichKit.StoneDarkMat;
        LichKit.Part(r, PrimitiveType.Cube, new Vector3(0f, -0.07f, 0f), new Vector3(Size.x, Size.y - 0.25f, Size.z), stone);
        LichKit.Part(r, PrimitiveType.Cylinder, new Vector3(0f, Size.y * 0.5f - 0.25f, 0f), new Vector3(Size.x, Size.z * 0.5f, Size.x), stone, new Vector3(90f, 0f, 0f));
        float face = -Size.z * 0.5f - 0.004f;
        Material glow = Mats.Emissive(new Color(0.3f, 0.6f, 0.25f), new Color(0.6f, 1.6f, 0.45f));
        LichKit.Part(r, WorldMeshes.Ball(0.09f), new Vector3(0f, 0.12f, face), new Vector3(1f, 1f, 0.15f), glow);            // skull
        for (int s = -1; s <= 1; s += 2)
            LichKit.Part(r, WorldMeshes.Ball(0.025f), new Vector3(s * 0.035f, 0.13f, face - 0.012f), new Vector3(1f, 1.1f, 0.3f), LichKit.SocketMat);
        LichKit.Part(r, PrimitiveType.Cube, new Vector3(0f, 0.04f, face), new Vector3(0.06f, 0.02f, 0.01f), glow);         // jaw
        LichKit.Part(r, PrimitiveType.Cube, new Vector3(-0.1f, -0.12f, face), new Vector3(0.014f, 0.22f, 0.01f), dark, new Vector3(0f, 0f, 24f));   // crack
        Material moss = Mats.Lit(new Color(0.26f, 0.36f, 0.18f), 0.05f);
        for (int k = 0; k < 4; k++)
            LichKit.Part(r, WorldMeshes.Ball(0.07f), new Vector3(-0.2f + k * 0.13f, -Size.y * 0.5f + 0.06f, face + 0.03f), new Vector3(1.2f, 0.6f, 0.5f), moss);
        ModelKit.Shadows(root, true);
        return root;
    }
}

// One skeleton of the legion. It is moved by hand (no physics body): it runs along the ground in front of the walls,
// climbs straight up the wall face, strikes, and falls apart. level 0 = the bottom course, 1 = half way up, 2 = the
// top (the commander's room).
public class SkeletonWarrior : Projectile {
    const float Z = -1.15f;                      // just in front of the walls (blocks span z = ±0.8)
    const float RunSpeed = 3.6f, ClimbSpeed = 2.4f, RiseTime = 0.55f;
    const float Reach = 0.4f;                    // it stands this far from the wall face it strikes
    const float StrikeHeight = 0.85f;            // its sword arm, above its feet
    const float CommanderDamage = 30f;
    const float TearTime = 0.4f, TearSpeed = 5f, TearAccel = 40f;   // the wrench that pulls its block out of the wall
    const int Swings = 3;
    const float SwingTime = 0.26f;
    const float FirstSwingShare = 0.2f;          // of def.damage for each swing before the last (the last deals the rest)
    const float CrushSpeed = 2.4f;               // a block moving this fast that touches it buries it

    enum Phase { Rising, Running, Climbing, Striking, Done }

    Fortress target;
    int level, towards;
    Phase phase;
    Vector3 pos;
    float phaseTime, groundY;
    Block food;
    bool goForCommander, risen;
    Transform model;
    Transform[] legs, arms;
    static readonly Collider[] near = new Collider[16];

    public void Setup(Fortress target, int level, int towards) {
        this.target = target;
        this.level = level;
        this.towards = towards;
    }

    protected override void OnLaunch(Vector3 velocity) {
        maxLifetime = 16f;
        pos = new Vector3(transform.position.x, transform.position.y, Z);
        groundY = pos.y;
        model = Build(transform, out legs, out arms).transform;
        model.localScale = new Vector3(towards >= 0 ? 1f : -1f, 1f, 1f);
        transform.position = pos + Vector3.down * 1.3f;    // still under the ground
        Explosion.Blasted += OnBlast;
    }

    protected override void OnDestroy() {
        base.OnDestroy();
        Explosion.Blasted -= OnBlast;
    }

    protected override void OnLost() { Crumble(); }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished || phase == Phase.Rising) return;
        // Buried by a falling block?
        Vector3 chest = new Vector3(pos.x, pos.y + 0.75f, 0f);
        int n = Physics.OverlapSphereNonAlloc(chest, 0.32f, near, Layers.BlockMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++) {
            Rigidbody body = near[i] != null ? near[i].attachedRigidbody : null;
            if (body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude > CrushSpeed * CrushSpeed) { Crumble(); return; }
        }
    }

    void OnBlast(Vector3 at, float radius) {
        if (finished || phase == Phase.Rising) return;
        Vector3 chest = new Vector3(pos.x, pos.y + 0.75f, 0f);
        if ((chest - new Vector3(at.x, at.y, 0f)).sqrMagnitude < (radius + 0.3f) * (radius + 0.3f)) Crumble();
    }

    void Update() {
        if (finished) return;
        float dt = Time.deltaTime;
        phaseTime += dt;
        switch (phase) {
            case Phase.Rising: Rise(); break;
            case Phase.Running: Run(dt); break;
            case Phase.Climbing: Climb(dt); break;
        }
        transform.position = pos;
    }

    // Out of the ground, then off it goes (or, with nobody in reach, it rattles and crumbles).
    void Rise() {
        float k = Mathf.Clamp01(phaseTime / RiseTime);
        pos.y = groundY - 1.3f * (1f - k * (2f - k));
        Pose(0f, 0f, 150f * (1f - k), 150f * (1f - k));   // clawing out with both arms up
        if (k < 1f) return;
        pos.y = groundY;
        if (!risen) {
            risen = true;
            LichSounds.Play(LichSounds.Rattle, pos, 0.5f, Random.Range(0.9f, 1.15f));
        }
        if (target == null || target.defeated) {
            if (phaseTime > RiseTime + 0.5f) Crumble();
            return;
        }
        Enter(Phase.Running);
    }

    // Along the ground to the foot of its wall (up any cliff on the way).
    void Run(float dt) {
        if (!PickTarget(out float faceX, out float strikeY)) { Crumble(); return; }
        float goalX = faceX - towards * Reach;
        float groundHere = LichKit.GroundAt(pos.x, out float gy) ? gy : World.WaterY;
        if (groundHere < World.WaterY + 0.2f) { Crumble(); return; }   // it walked off into the sea
        if (pos.y < groundHere - 0.05f) { pos.y = Mathf.MoveTowards(pos.y, groundHere, ClimbSpeed * dt); ClimbPose(); return; }
        pos.y = groundHere;
        pos.x = Mathf.MoveTowards(pos.x, goalX, RunSpeed * dt);
        float t = Time.time * 11f;
        Pose(28f * Mathf.Sin(t), -28f * Mathf.Sin(t), -20f * Mathf.Sin(t) + 20f, 20f * Mathf.Sin(t) + 20f);
        if (Mathf.Abs(pos.x - goalX) < 0.02f) Enter(strikeY - StrikeHeight > pos.y + 0.15f ? Phase.Climbing : Phase.Striking);
        if (phase == Phase.Striking) StartCoroutine(Strike());
    }

    // Straight up the wall face to the height of its block.
    void Climb(float dt) {
        if (!PickTarget(out float faceX, out float strikeY)) { Crumble(); return; }
        pos.x = Mathf.MoveTowards(pos.x, faceX - towards * Reach, RunSpeed * dt);
        pos.y = Mathf.MoveTowards(pos.y, strikeY - StrikeHeight, ClimbSpeed * dt);
        ClimbPose();
        if (Mathf.Abs(pos.y - (strikeY - StrikeHeight)) < 0.02f) {
            Enter(Phase.Striking);
            StartCoroutine(Strike());
        }
    }

    // Three swings of the rusty sword; the last one tears the block out (or cuts down the commander).
    IEnumerator Strike() {
        for (int s = 0; s < Swings; s++) {
            for (float t = 0f; t < SwingTime; t += Time.deltaTime) {
                float k = t / SwingTime;
                float sword = Mathf.Lerp(200f, 55f, k * k);
                Pose(-8f, 12f, sword, 160f + 10f * Mathf.Sin(k * Mathf.PI));
                if (phase != Phase.Striking || finished) yield break;
                yield return null;
            }
            Vector3 tip = new Vector3(pos.x + towards * 0.55f, pos.y + StrikeHeight, Z);
            if (s < Swings - 1) {
                VFX.Sparks(LichKit.Front(tip), new Color(1f, 0.85f, 0.6f), 4);
                SFX.Play(SoundId.ImpactStone, tip, 0.3f, Random.Range(1.2f, 1.4f));
                if (!goForCommander && food != null && food.IsAlive)   // the first swings hack at it, the last one tears it out
                    food.ApplyDamage(new DamageInfo(def.damage * FirstSwingShare, DamageType.Impact, food.transform.position, owner));
            }
        }
        if (goForCommander && target != null && target.CommanderAlive) {
            Commander cm = target.commander;
            cm.ApplyDamage(new DamageInfo(CommanderDamage, DamageType.Pierce, cm.AimPoint, owner, Vector3.right * towards * 2f));
            VFX.Sparks(LichKit.Front(cm.AimPoint), new Color(1f, 0.4f, 0.3f), 10);
            SFX.Play(SoundId.Pierce, cm.AimPoint, 0.6f);
        } else if (food != null && food.IsAlive) {
            Vector3 p = new Vector3(food.transform.position.x - towards * food.size.x * 0.4f, pos.y + StrikeHeight, 0f);
            food.ApplyDamage(new DamageInfo(def.damage * (1f - FirstSwingShare * (Swings - 1)), DamageType.Impact, p, owner, Vector3.left * towards * 4f));
            if (food != null && food.IsAlive && food.rb != null && !food.rb.isKinematic) {
                WakeFortress(target);
                Runner.Run(TearOut(food, towards));           // it outlives the skeleton, which falls apart now
            }
            VFX.Chips(p, food != null ? food.material : BlockMaterial.Stone, 8, Vector3.left * towards);
            SFX.PlayImpact(food != null ? food.material : BlockMaterial.Stone, p, 0.8f);
            BattleCamera.Shake(0.08f);
        }
        yield return new WaitForSeconds(0.25f);
        Crumble();
    }

    // Its block at its height on the side it came from: the outermost one there (re-chosen as the wall changes).
    // The top skeleton goes for the commander when nothing stands between the outside and him.
    bool PickTarget(out float faceX, out float strikeY) {
        faceX = strikeY = 0f;
        if (target == null || target.defeated) return false;
        float ground = target.transform.position.y, top = float.MinValue, outer = float.MaxValue;
        List<Block> alive = new List<Block>();
        foreach (Block b in target.blocks) {
            if (b == null || !b.IsAlive || b.box == null) continue;
            Bounds bb = b.box.bounds;
            if (bb.max.y < ground - 0.5f) continue;          // fallen far below: not part of the wall any more
            alive.Add(b);
            top = Mathf.Max(top, bb.max.y);
            outer = Mathf.Min(outer, towards > 0 ? bb.min.x : -bb.max.x);
        }
        if (alive.Count == 0) return false;
        float wantY = level == 0 ? ground + 0.45f : level == 1 ? Mathf.Lerp(ground, top, 0.5f) : top - 0.5f;
        if (level == 2 && target.CommanderAlive) wantY = target.commander.AimPoint.y;

        goForCommander = false;
        if (target.CommanderAlive && (level == 2 || Mathf.Abs(target.commander.AimPoint.y - wantY) < 1.2f)) {
            Commander cm = target.commander;
            Vector3 outside = new Vector3((towards > 0 ? outer : -outer) - towards * 0.8f, cm.AimPoint.y, 0f);
            if (Explosion.Cover(outside, cm.AimPoint) > 0.99f) {
                goForCommander = true;
                food = null;
                faceX = cm.transform.position.x - towards * 0.15f;
                strikeY = cm.AimPoint.y;
                return true;
            }
        }
        Block best = null;
        float bestScore = float.MaxValue;
        foreach (Block b in alive) {
            Bounds bb = b.box.bounds;
            float dy = Mathf.Abs(Mathf.Clamp(wantY, bb.min.y, bb.max.y) - wantY);
            float depth = (towards > 0 ? bb.min.x : -bb.max.x) - outer;   // how far it sits back from the outer face
            float score = dy * 4f + depth;
            if (b == food) score -= 0.3f;                    // keep to the chosen block while it is still sensible
            if (score < bestScore) { bestScore = score; best = b; }
        }
        food = best;
        Bounds fb = best.box.bounds;
        faceX = towards > 0 ? fb.min.x : fb.max.x;
        strikeY = Mathf.Clamp(wantY, fb.min.y + 0.15f, Mathf.Max(fb.min.y + 0.15f, fb.max.y - 0.1f));
        return true;
    }

    void Enter(Phase p) {
        phase = p;
        phaseTime = 0f;
    }

    // Falls apart into a heap of bones.
    void Crumble() {
        if (finished) return;
        phase = Phase.Done;
        Vector3 at = pos + Vector3.up * 0.6f;
        LichKit.BoneScatter(at, 0.8f);
        LichSounds.Play(LichSounds.Rattle, at, 0.45f, Random.Range(0.8f, 1f));
        if (!impacted) RaiseImpact(at);
        Finish(true);
    }

    // The block is wrenched out of the wall towards the skeleton's side: for TearTime its velocity is steered outwards
    // (and a little up), changing by at most TearAccel per second, so a block pinned under a heavy load may still
    // hold while a loose one comes right out (like the grappling hook's pull).
    static IEnumerator TearOut(Block b, int towards) {
        Vector3 dir = new Vector3(-towards, 0.3f, 0f).normalized;
        for (float t = 0f; t < TearTime; t += Time.fixedDeltaTime) {
            if (b == null || !b.IsAlive || b.rb == null || b.rb.isKinematic) yield break;
            b.rb.WakeUp();
            Vector3 v = Vector3.MoveTowards(b.rb.linearVelocity, dir * TearSpeed, TearAccel * Time.fixedDeltaTime);
            v.z = 0f;
            b.rb.linearVelocity = v;
            yield return new WaitForFixedUpdate();
        }
    }

    // Blocks sleep a hair apart (see Fortress.WakeUp): wake them, or the wall could hang where a block was torn out.
    static void WakeFortress(Fortress f) {
        if (f == null) return;
        foreach (Block x in f.blocks)
            if (x != null && x.rb != null && !x.rb.isKinematic && x.rb.IsSleeping()) x.rb.WakeUp();
        if (f.CommanderAlive && f.commander.rb != null && f.commander.rb.IsSleeping()) f.commander.rb.WakeUp();
    }

    // ------------------------------------------------------------------ animation
    // Angles in degrees: legs and arms hang down at 0, positive swings them forward (towards `towards`).
    void Pose(float legNear, float legFar, float armNear, float armFar) {
        legs[0].localRotation = Quaternion.Euler(0f, 0f, legNear);
        legs[1].localRotation = Quaternion.Euler(0f, 0f, legFar);
        arms[0].localRotation = Quaternion.Euler(0f, 0f, armNear);
        arms[1].localRotation = Quaternion.Euler(0f, 0f, armFar);
    }

    // Hand over hand up the wall.
    void ClimbPose() {
        float t = Time.time * 9f;
        Pose(25f + 20f * Mathf.Sin(t), 25f - 20f * Mathf.Sin(t), 150f + 25f * Mathf.Sin(t), 150f - 25f * Mathf.Sin(t));
    }

    // ------------------------------------------------------------------ model
    // A skeleton about 1.3 m tall facing +x, feet at the origin: a skull with glowing green pinpoint eyes, a spine,
    // a ribcage, a pelvis, bony limbs on pivots (legs[0], arms[0] = the near side) and a rusty sword in the near hand.
    static GameObject Build(Transform parent, out Transform[] legs, out Transform[] arms) {
        GameObject root = ModelKit.Root("Skeleton", parent);
        Transform r = root.transform;
        Material bone = LichKit.BoneMat, dark = LichKit.BoneDarkMat;
        legs = new Transform[2];
        arms = new Transform[2];
        LichKit.Part(r, WorldMeshes.Ball(0.08f), new Vector3(0f, 0.58f, 0f), new Vector3(1.2f, 0.7f, 1.4f), dark);       // pelvis
        LichKit.Rod(r, new Vector3(0f, 0.58f, 0f), new Vector3(0.02f, 1.02f, 0f), 0.025f, 0.022f, dark, 5);                 // spine
        for (int k = 0; k < 3; k++) {                                                                                       // ribs
            float y = 0.74f + k * 0.075f, w = 0.11f - Mathf.Abs(k - 1) * 0.015f;
            LichKit.Part(r, WorldMeshes.Frustum(8, w, w * 0.92f, 0.035f), new Vector3(0.03f, y, 0f), new Vector3(1f, 1f, 1.2f), bone);
        }
        Transform skull = LichKit.Pivot("Skull", r, new Vector3(0.03f, 1.13f, 0f));
        LichKit.Part(skull, WorldMeshes.Ball(0.11f), Vector3.zero, new Vector3(1f, 1.05f, 0.95f), bone);
        LichKit.Part(skull, WorldMeshes.Ball(0.06f), new Vector3(0.05f, -0.08f, 0f), new Vector3(1f, 0.6f, 1.2f), bone);   // jaw
        for (int s = -1; s <= 1; s += 2) {
            LichKit.Part(skull, WorldMeshes.Ball(0.032f), new Vector3(0.08f, 0.01f, s * 0.045f), Vector3.one, LichKit.SocketMat);
            LichKit.Part(skull, WorldMeshes.Ball(0.012f), new Vector3(0.1f, 0.01f, s * 0.045f), Vector3.one, LichKit.SoulMat);
        }
        for (int i = 0; i < 2; i++) {
            float z = i == 0 ? -0.09f : 0.09f;
            legs[i] = LichKit.Pivot(i == 0 ? "Leg Near" : "Leg Far", r, new Vector3(0f, 0.56f, z));
            LichKit.Rod(legs[i], Vector3.zero, new Vector3(0.02f, -0.28f, 0f), 0.026f, 0.02f, bone, 5);
            LichKit.Rod(legs[i], new Vector3(0.02f, -0.28f, 0f), new Vector3(0f, -0.54f, 0f), 0.02f, 0.016f, bone, 5);
            LichKit.Part(legs[i], PrimitiveType.Cube, new Vector3(0.04f, -0.55f, 0f), new Vector3(0.12f, 0.03f, 0.05f), dark);   // foot
            arms[i] = LichKit.Pivot(i == 0 ? "Arm Near" : "Arm Far", r, new Vector3(0.02f, 0.97f, i == 0 ? -0.14f : 0.14f));
            LichKit.Rod(arms[i], Vector3.zero, new Vector3(0f, -0.23f, 0f), 0.022f, 0.018f, bone, 5);
            LichKit.Rod(arms[i], new Vector3(0f, -0.23f, 0f), new Vector3(0.02f, -0.43f, 0f), 0.017f, 0.014f, bone, 5);
            LichKit.Part(arms[i], WorldMeshes.Ball(0.03f), new Vector3(0.02f, -0.45f, 0f), Vector3.one, dark);
        }
        // The sword hangs from the near hand: a pitted, rusty blade with a dark cross-guard.
        Transform sword = LichKit.Pivot("Sword", arms[0], new Vector3(0.02f, -0.46f, -0.01f));
        LichKit.Part(sword, PrimitiveType.Cube, new Vector3(0f, -0.28f, 0f), new Vector3(0.05f, 0.5f, 0.012f), LichKit.RustMat);
        LichKit.Part(sword, PrimitiveType.Cube, new Vector3(0f, -0.02f, 0f), new Vector3(0.16f, 0.025f, 0.03f), LichKit.StoneDarkMat);
        ModelKit.Shadows(root, true);
        return root;
    }
}

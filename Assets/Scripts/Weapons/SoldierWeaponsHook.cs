using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// The Soldier's grappling hook (GrapplingHook): a three-pronged steel hook thrown on a rope. The rope runs from the
// soldier's throwing point to the hook's ring; while slack it trails along the throw and sags, when the hook bites
// it snaps taut and thrums.
//  * First contact with an enemy block: def.damage (Impact), and the hook latches on where it bit and rides on the
//    block. For up to BlockPullTime the block is woken and its velocity is steered towards the thrower, with an
//    upward bias so it lifts out of the wall, aiming at def.force m/s but changing by at most BlockAccel per second
//    (YankBoost times that for the first YankTime, as the rope jerks taut; a heavy load on top can still hold a
//    block back, a loose one flies). It is torn out of the wall, knocks into its neighbours and is flung out over the
//    gap between the islands. The hauling stops early once the block would come down past ReachShare of the way to
//    the thrower, so a block is never thrown onto our own fortress; the rope then goes slack and the hook rides the
//    flying block until MinRide. A block that breaks under the hit lets the hook fly on (MaxBreakThroughs at most).
//  * First contact with an enemy commander: def.damage, and a weaker, shorter pull (CommanderSpeed for
//    CommanderPullTime) that drags him against the wall of his room, or out of it if that wall is gone.
//  * Then, or at once after hitting the ground, the sea, our own side or nothing at all, or if what it holds breaks,
//    the hook is reeled back to the thrower along the slack rope, and the weapon finishes.
public class GrapplingHook : Projectile {
    // flight
    const float Mass = 1.5f, Radius = 0.2f, ModelScale = 1.1f;
    const float RollSpeed = 540f, ReelRollSpeed = 1080f;     // deg/s the hook spins around its shank
    const int MaxBreakThroughs = 3;
    const float BreakThroughKeep = 0.75f;                     // share of its speed kept after smashing a block
    // the bite and the pull
    const float Bite = 0.18f, BiteTime = 0.06f;               // how deep and how fast the crown sinks in
    const float BlockPullTime = 0.9f, BlockUpBias = 0.42f, BlockAccel = 75f;
    const float CommanderSpeed = 6f, CommanderPullTime = 0.5f, CommanderUpBias = 0.15f, CommanderAccel = 40f;
    const float YankTime = 0.2f, YankBoost = 2f;              // the rope jerks taut: twice the pull for the first moment
    const float ReachShare = 0.5f;                            // a pulled body comes down at most this share of the way to us
    const float MinRide = 0.3f;                               // the hook stays on what it pulled at least this long
    const float SpinPerMetre = 5f, MaxSpin = 3.5f, SpinAccel = 20f;
    const float TearSpeed = 3f;                               // m/s towards the thrower: the block has come loose
    const float WakeEvery = 0.25f;
    const int DustPuffs = 4;
    // reeling in
    const float MinReelTime = 0.25f, MaxReelTime = 0.5f;
    const float HardStop = 4f;                                // seconds past maxLifetime before the hook simply ends
    // rope
    const int RopePoints = 20;
    const float RopeWidth = 0.07f, OutlineWidth = 0.12f, OutlineDepth = 0.03f;
    const float PathStep = 0.6f;
    const int MaxPath = 200;
    const float TautTime = 0.08f, SlackTime = 0.15f;
    const float TwangAmp = 0.35f, TwangDecay = 0.15f, StrainAmp = 0.03f, TwangHz = 11f;
    // model
    const float ArmRadius = 0.27f, ArmFrom = 4f, ArmTo = 86f;  // each prong: a quarter circle from the crown back to the ring

    static readonly Color Steel = new Color(0.6f, 0.62f, 0.66f), Blackened = new Color(0.26f, 0.27f, 0.29f);
    static readonly Color Sharpened = new Color(0.86f, 0.88f, 0.9f), Brass = WeaponDefs.Hex("B8904E");
    static readonly Color Olive = WeaponDefs.Hex("5A5F33"), Accent = WeaponDefs.Hex("FF5A1F");
    static readonly Color RopeColor = new Color(0.62f, 0.56f, 0.38f), RopeDark = new Color(0.13f, 0.12f, 0.08f);
    static readonly Color SparkColor = new Color(1f, 0.72f, 0.4f);
    static readonly Vector3 RingKnot = new Vector3(-0.83f, 0f, 0f);   // where the rope is tied (model space)

    enum Phase { Flying, Pulling, Reeling }

    Phase phase;
    Transform model;
    SphereCollider tip;
    LineRenderer rope, ropeOutline;
    readonly Vector3[] ropeFront = new Vector3[RopePoints], ropeBack = new Vector3[RopePoints], bulge = new Vector3[RopePoints];
    readonly List<Vector3> path = new List<Vector3>();        // where the ring has been in flight (path[0]: the throw)
    Vector3 lastVelocity, hand;
    float roll, faceAngle, tension, twang, phaseTime, bulgeAt;
    int breakThroughs;
    bool underwater;

    // what the hook holds
    Block hookedBlock;
    Commander hookedCommander;
    Rigidbody held;
    Transform host;
    Vector3 anchorLocal, biteFrom;
    float biteStart, pullSpeed, pullDuration, pullAccel, upBias, reachX, reachSign, wakeTimer, nextDust;
    int dustLeft;
    bool steering, tornOut, swings;
    AudioSource creak;

    // reeling in
    Vector3 reelFrom;
    float reelTime;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        WeaponFactory.Spawn<GrapplingHook>("Grappling hook", owner, def, origin, velocity);
    }

    protected override void OnLaunch(Vector3 velocity) {
        maxLifetime = 9f;                    // safety net: a throw comes down long before
        hand = transform.position;
        model = BuildHook(transform, false).transform;
        model.localScale = Vector3.one * ModelScale;
        tip = gameObject.AddComponent<SphereCollider>();   // the crown leads
        tip.radius = Radius;
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;   // only the model turns
        lastVelocity = velocity;
        faceAngle = Angle(velocity);
        path.Add(transform.position);
        ropeOutline = RopeLine("Rope outline", RopeDark, OutlineWidth);
        rope = RopeLine("Rope", RopeColor, RopeWidth);
        HookSounds.Warm();
        Pose();
        DrawRope();
    }

    protected override void OnDestroy() {
        base.OnDestroy();
        StopCreak();
    }

    // Out of the arena or in the air too long: reel it in. The later phases end by themselves; past the hard stop
    // the hook simply ends.
    protected override void OnLost() {
        if (phase == Phase.Flying) BeginReel();
        else if (age > maxLifetime + HardStop) Finish(true);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished) return;
        if (phase == Phase.Flying) {
            lastVelocity = rb.linearVelocity;    // before this step's contacts change it
            RecordPath();
            if (transform.position.y < World.WaterY) Splashdown();
        } else if (phase == Phase.Pulling) Pull(Time.fixedDeltaTime);
    }

    void Update() {
        if (finished) return;
        float dt = Time.deltaTime;
        if (phase == Phase.Flying) {
            roll += RollSpeed * dt;
            Vector3 v = Velocity;
            if (v.sqrMagnitude > 0.01f) faceAngle = Angle(v);
        } else if (phase == Phase.Pulling) {
            FollowHost();
            TurnTowards(transform.position - Hand(), dt);   // it pivots on its prongs: the ring points along the rope
        } else {
            Reel(dt);
            if (finished) return;
        }
        Pose();
    }

    void LateUpdate() {
        if (finished) return;
        float dt = Time.deltaTime;
        float goal = phase == Phase.Pulling && steering ? 1f : 0f;
        tension = Mathf.MoveTowards(tension, goal, dt / (goal > tension ? TautTime : SlackTime));
        twang *= Mathf.Exp(-dt / TwangDecay);
        DrawRope();
    }

    // ------------------------------------------------------------------ contact
    void OnCollisionEnter(Collision c) {
        if (finished || phase != Phase.Flying) return;
        SoldierWeapons.FirstContact(c, transform.position, out Vector3 point, out _);
        SoldierWeapons.LimitShove(c);
        point.z = 0f;
        if (!impacted) RaiseImpact(point);
        Vector3 dir = lastVelocity.sqrMagnitude > 0.01f ? lastVelocity.normalized : Vector3.right * travelDir;
        Block block = c.collider.GetComponentInParent<Block>();
        Commander cm = block == null ? c.collider.GetComponentInParent<Commander>() : null;
        if (block != null && block.IsAlive && !IsOwnTeam(block)) HitBlock(block, point, dir);
        else if (cm != null && cm.IsAlive && !IsOwnTeam(cm)) HitCommander(cm, point, dir);
        else Glance(point);
    }

    void HitBlock(Block b, Vector3 point, Vector3 dir) {
        Hit(b, def.damage, DamageType.Impact, point, owner, dir * (Mass * lastVelocity.magnitude));
        VFX.Sparks(point, SparkColor, 12);
        if (!b.IsAlive) {                    // smashed through (its collider is already off): fly on
            if (breakThroughs++ < MaxBreakThroughs) {
                rb.linearVelocity = lastVelocity * BreakThroughKeep;
                lastVelocity = rb.linearVelocity;
                SFX.Play(SoundId.ImpactMetal, point, 0.4f, 1.25f);
            } else BeginReel();
            return;
        }
        Vector3 anchor = b.box != null ? b.box.ClosestPoint(point + dir * Bite) : point;
        Latch(b.transform, b.rb, anchor, def.force, BlockPullTime, BlockAccel, BlockUpBias, true);
        hookedBlock = b;
        WakeFortress(b.fortress);
        SFX.Play(SoundId.ImpactMetal, point, 0.6f, 0.9f);
        SFX.PlayImpact(b.material, point, 0.7f);
        creak = HookSounds.Loop(HookSounds.Creak, transform, 0.5f);
        TimeFX.SlowMo(0.5f, 0.05f);          // a short hit-stop sells the bite
        BattleCamera.Shake(0.12f);
    }

    void HitCommander(Commander cm, Vector3 point, Vector3 dir) {
        float before = cm.hp;
        Hit(cm, def.damage, DamageType.Impact, point, owner, Vector3.zero);
        VFX.Sparks(point, SparkColor, 8);
        SFX.Play(SoundId.ImpactMetal, point, 0.4f, 1.15f);
        // A shield (the blood cloak) took the hit, he fell, or he cannot be moved: the hook glances off.
        if (cm.hp >= before || !cm.IsAlive || cm.rb == null || cm.rb.isKinematic) { BeginReel(); return; }
        Vector3 anchor = cm.col != null ? cm.col.ClosestPoint(point + dir * 0.1f) : point;
        Latch(cm.transform, cm.rb, anchor, CommanderSpeed, CommanderPullTime, CommanderAccel, CommanderUpBias, false);
        hookedCommander = cm;
        creak = HookSounds.Loop(HookSounds.Creak, transform, 0.4f, 1.2f);
        BattleCamera.Shake(0.1f);
    }

    // The ground, our own side or anything else: a clink, a puff of dust, and it is reeled in.
    void Glance(Vector3 point) {
        SFX.Play(SoundId.ImpactMetal, point, 0.35f, 1.3f);
        VFX.Dust(point, 0.4f, SoldierFx.GroundDust);
        BeginReel();
    }

    void Splashdown() {
        Vector3 p = transform.position;
        if (!impacted) RaiseImpact(new Vector3(p.x, World.WaterY, 0f));
        underwater = true;
        SoldierFx.Splash(p, 0.6f);
        BeginReel();
    }

    // ------------------------------------------------------------------ the pull
    // The crown bites into `anchor` on the host and the pull begins: from now on the hook rides on the host.
    void Latch(Transform on, Rigidbody body, Vector3 anchor, float speed, float duration, float accel, float bias, bool turns) {
        anchor.z = 0f;
        StopBody();
        phase = Phase.Pulling;
        phaseTime = 0f;
        host = on;
        held = body;
        anchorLocal = on.InverseTransformPoint(anchor);
        biteFrom = transform.position;
        biteStart = Time.time;
        pullSpeed = speed;
        pullDuration = duration;
        pullAccel = accel;
        upBias = bias;
        swings = turns;
        steering = true;
        tornOut = false;
        float handX = Hand().x;
        reachX = Mathf.Lerp(anchor.x, handX, ReachShare);
        reachSign = handX >= anchor.x ? 1f : -1f;
        wakeTimer = WakeEvery;
        twang = TwangAmp;
        CaptureBulge();                      // from here on the rope runs straight from the hand (DrawRope)
        path.Clear();
        HookSounds.Play(HookSounds.Twang, anchor, 0.75f);
    }

    // The pull ends when its time is up or the target is gone; once the hauling has stopped (Steer), the hook rides
    // on until MinRide and is then reeled in.
    void Pull(float dt) {
        phaseTime += dt;
        if (!HoldsLiveTarget() || held.isKinematic || phaseTime >= pullDuration) { BeginReel(); return; }
        if (steering) Steer(dt);
        if (!steering && phaseTime >= MinRide) { BeginReel(); return; }
        Shed(held.worldCenterOfMass);
        wakeTimer -= dt;
        if (wakeTimer <= 0f && hookedBlock != null) {
            wakeTimer = WakeEvery;
            WakeFortress(hookedBlock.fortress);
        }
    }

    // Steers the held body towards the thrower with a limited velocity change per step, until letting go now
    // already throws it as far as it may go.
    void Steer(float dt) {
        Vector3 p = held.worldCenterOfMass;
        Vector3 to = Hand() - p;
        to.z = 0f;
        if (to.sqrMagnitude < 1f) { Slacken(); return; }
        Vector3 dir = (to.normalized + Vector3.up * upBias).normalized;
        Vector3 v = held.linearVelocity;
        if (!tornOut && Vector3.Dot(v, dir) > TearSpeed) TearOut(p, dir);   // moving our way (the hit itself shoves it the other way)
        float accel = phaseTime < YankTime ? pullAccel * YankBoost : pullAccel;
        Vector3 want = Vector3.MoveTowards(v, dir * pullSpeed, accel * dt);
        want.z = 0f;
        if (LandsPastReach(p, want)) {       // any faster and it would fly too far: let it fly as it is
            if (!tornOut) TearOut(p, dir);
            Slacken();
            return;
        }
        held.WakeUp();
        held.linearVelocity = want;
        if (swings) Swing(p, dir, dt);
    }

    // The thrower stops hauling: the rope goes slack while the hook still rides on what it pulled.
    void Slacken() {
        steering = false;
        StopCreak();
    }

    bool HoldsLiveTarget() {
        if (held == null || host == null) return false;
        if (hookedBlock != null) return hookedBlock.IsAlive;
        return hookedCommander != null && hookedCommander.IsAlive;
    }

    // Where the body would come down (at sea level) if the rope let go with velocity v: past the reach limit?
    bool LandsPastReach(Vector3 p, Vector3 v) {
        float g = World.Gravity, drop = p.y - World.WaterY;
        if (g <= 0f || drop <= 0f) return false;
        float t = (v.y + Mathf.Sqrt(v.y * v.y + 2f * g * drop)) / g;
        return (p.x + v.x * t - reachX) * reachSign > 0f;
    }

    // The rope pulls at the bite, not at the centre: the block turns until the bite faces the thrower.
    void Swing(Vector3 centre, Vector3 dir, float dt) {
        if (host == null) return;
        Vector3 r = host.TransformPoint(anchorLocal) - centre;
        float torque = r.x * dir.y - r.y * dir.x;          // z of r x dir
        Vector3 w = held.angularVelocity;
        w.z = Mathf.MoveTowards(w.z, Mathf.Clamp(torque * SpinPerMetre, -MaxSpin, MaxSpin), SpinAccel * dt);
        held.angularVelocity = w;
    }

    // The moment it comes loose: a burst of dust and chips, the knock of the block and a jolt.
    void TearOut(Vector3 p, Vector3 dir) {
        tornOut = true;
        dustLeft = DustPuffs;
        nextDust = phaseTime + 0.09f;
        BattleCamera.Shake(hookedBlock != null ? 0.2f : 0.1f);
        if (hookedBlock == null) return;
        BlockMaterial m = hookedBlock.material;
        VFX.Dust(p, Mathf.Max(hookedBlock.size.x, hookedBlock.size.y) * 0.8f, SoldierFx.DustOf(m));
        VFX.Chips(p, m, 10, dir);
        SFX.PlayImpact(m, p, 1f);
    }

    // A few puffs of dust shed by the block as it flies out.
    void Shed(Vector3 p) {
        if (dustLeft <= 0 || phaseTime < nextDust || hookedBlock == null) return;
        dustLeft--;
        nextDust = phaseTime + 0.09f;
        VFX.Dust(p, 0.45f, SoldierFx.DustOf(hookedBlock.material));
    }

    // Blocks sleep a hair apart (see Fortress.WakeUp): wake them, or a wall could hang where a block was pulled out.
    static void WakeFortress(Fortress f) {
        if (f == null) return;
        foreach (Block x in f.blocks)
            if (x != null && x.rb != null && !x.rb.isKinematic && x.rb.IsSleeping()) x.rb.WakeUp();
        if (f.CommanderAlive && f.commander.rb != null && f.commander.rb.IsSleeping()) f.commander.rb.WakeUp();
    }

    // Rides on what it holds, sinking in over BiteTime after the bite.
    void FollowHost() {
        if (host == null) return;
        float k = Mathf.Clamp01((Time.time - biteStart) / BiteTime);
        transform.position = Vector3.Lerp(biteFrom, host.TransformPoint(anchorLocal), k * (2f - k));
    }

    // ------------------------------------------------------------------ reeling in
    // The rope goes slack and the hook is reeled back to the thrower along it. From here on nothing collides.
    void BeginReel() {
        if (phase == Phase.Reeling) return;
        if (hookedBlock != null) WakeFortress(hookedBlock.fortress);   // nothing may hang where the block was
        phase = Phase.Reeling;
        phaseTime = 0f;
        hookedBlock = null;
        hookedCommander = null;
        held = null;
        host = null;
        StopBody();
        Slacken();
        cameraFollows = false;               // the camera stays on what the hook tore loose
        CaptureBulge();
        path.Clear();
        reelFrom = transform.position;
        reelTime = Mathf.Clamp(MinReelTime + Vector3.Distance(reelFrom, Hand()) * 0.004f, MinReelTime, MaxReelTime);
        HookSounds.Play(HookSounds.Zip, reelFrom, 0.7f);
    }

    // Slowly at first while the slack is taken up, then fast; it dips a little, like the rope it runs along.
    void Reel(float dt) {
        phaseTime += dt;
        float k = Mathf.Clamp01(phaseTime / reelTime);
        Vector3 to = Hand();
        float span = Vector3.Distance(reelFrom, to);
        Vector3 dip = (reelFrom + to) * 0.5f + Vector3.down * Mathf.Min(span * 0.06f, 2.5f);
        Vector3 p = Bezier(reelFrom, dip, to, k * k);
        transform.position = p;
        roll += ReelRollSpeed * dt;
        TurnTowards(p - to, dt);
        CheckWater(p);
        if (k < 1f) return;
        SFX.Play(SoundId.ImpactMetal, to, 0.3f, 1.7f);   // back in the soldier's hand: a light clink
        Finish(true);
    }

    // Splashes where the hook dives into the sea or is pulled out of it.
    void CheckWater(Vector3 p) {
        bool under = p.y < World.WaterY;
        if (under == underwater) return;
        underwater = under;
        Vector3 s = new Vector3(p.x, World.WaterY, 0f);
        VFX.Splash(s, 0.5f);
        SFX.Play(SoundId.Splash, s, 0.4f, 1.3f);
    }

    // No more physics for the hook itself: it rides on what it holds, then flies a path we drive.
    void StopBody() {
        if (rb == null || rb.isKinematic) return;
        rb.linearVelocity = Vector3.zero;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;   // the only mode kinematic bodies support
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        tip.enabled = false;
    }

    void StopCreak() {
        SFX.Stop(creak);
        creak = null;
    }

    // ------------------------------------------------------------------ the hook and its rope
    // The thrower's launch point (above his chamber roof); if he has fallen, where it last was.
    Vector3 Hand() {
        if (owner != null && owner.CommanderAlive) hand = owner.LaunchOrigin;
        return hand;
    }

    void TurnTowards(Vector3 dir, float dt) {
        if (dir.sqrMagnitude > 0.04f) faceAngle = Mathf.LerpAngle(faceAngle, Angle(dir), 1f - Mathf.Exp(-dt * 20f));
    }

    void Pose() {
        model.rotation = Quaternion.Euler(0f, 0f, faceAngle) * Quaternion.Euler(roll, 0f, 0f);
    }

    void RecordPath() {
        Vector3 ring = model.TransformPoint(RingKnot);
        if ((ring - path[path.Count - 1]).sqrMagnitude < PathStep * PathStep) return;
        path.Add(ring);
        if (path.Count <= MaxPath) return;
        int w = 1;                           // too long: keep every other point (and both ends)
        for (int i = 1; i < path.Count; i++)
            if (i % 2 == 0 || i == path.Count - 1) path[w++] = path[i];
        path.RemoveRange(w, path.Count - w);
    }

    // The rope's last drawn shape, kept as an offset from the straight line, so a change of shape (the bite, the
    // reel) eases in over SlackTime instead of jumping.
    void CaptureBulge() {
        Vector3 a = ropeFront[0], b = ropeFront[RopePoints - 1];
        for (int i = 0; i < RopePoints; i++) bulge[i] = ropeFront[i] - Vector3.Lerp(a, b, i / (RopePoints - 1f));
        bulgeAt = Time.time;
    }

    // Slack: along the flight path in the air, straight from the hand once the hook has bitten, sagging in the
    // middle. Taut: a straight line that thrums after the bite and trembles under the strain. Drawn twice, a dark
    // outline behind a khaki core, so it reads against a bright sky and a night sky alike.
    void DrawRope() {
        Vector3 a = Hand(), b = model.TransformPoint(RingKnot);
        float sag;
        if (phase == Phase.Flying) {
            sag = Mathf.Min(AlongPath(a, b) * 0.04f, 1.6f) * Mathf.Clamp01(age / 0.8f);
        } else {
            float fade = Mathf.Clamp01((Time.time - bulgeAt) / SlackTime);
            for (int i = 0; i < RopePoints; i++) ropeFront[i] = Vector3.Lerp(a, b, i / (RopePoints - 1f)) + bulge[i] * (1f - fade);
            sag = Mathf.Min(Vector3.Distance(a, b) * 0.06f, 2.5f) * fade;
        }
        Vector3 d = b - a;
        Vector3 across = new Vector3(-d.y, d.x, 0f).normalized;
        float shake = (twang + (steering ? StrainAmp : 0f)) * Mathf.Sin(Time.time * TwangHz * Mathf.PI * 2f);
        for (int i = 0; i < RopePoints; i++) {
            float t = i / (RopePoints - 1f);
            Vector3 slack = ropeFront[i] + Vector3.down * (sag * 4f * t * (1f - t));
            Vector3 taut = Vector3.Lerp(a, b, t) + across * (shake * Mathf.Sin(Mathf.PI * t));
            Vector3 p = Vector3.Lerp(slack, taut, tension);
            ropeFront[i] = p;
            ropeBack[i] = p + Vector3.forward * OutlineDepth;
        }
        rope.SetPositions(ropeFront);
        ropeOutline.SetPositions(ropeBack);
    }

    // Fills ropeFront with points evenly spaced along hand -> the ring's flight path -> ring; returns the length.
    float AlongPath(Vector3 a, Vector3 b) {
        int n = path.Count + 1;
        float total = 0f;
        for (int i = 1; i < n; i++) total += Vector3.Distance(PathPoint(i - 1, a, b), PathPoint(i, a, b));
        int seg = 1;
        float walked = 0f, segLength = Vector3.Distance(PathPoint(0, a, b), PathPoint(1, a, b));
        for (int k = 0; k < RopePoints; k++) {
            float want = total * k / (RopePoints - 1f);
            while (seg < n - 1 && walked + segLength < want) {
                walked += segLength;
                seg++;
                segLength = Vector3.Distance(PathPoint(seg - 1, a, b), PathPoint(seg, a, b));
            }
            float f = segLength > 1e-5f ? Mathf.Clamp01((want - walked) / segLength) : 1f;
            ropeFront[k] = Vector3.Lerp(PathPoint(seg - 1, a, b), PathPoint(seg, a, b), f);
        }
        return total;
    }

    // Point i of the rope's polyline: the hand, then the recorded path, then the ring.
    Vector3 PathPoint(int i, Vector3 a, Vector3 b) { return i == 0 ? a : i < path.Count ? path[i] : b; }

    LineRenderer RopeLine(string name, Color color, float width) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = RopePoints;
        lr.widthMultiplier = width;
        lr.sharedMaterial = Mats.Unlit(color);
        lr.numCapVertices = 2;
        lr.numCornerVertices = 1;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    static float Angle(Vector3 v) { return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg; }

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t) {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    // ------------------------------------------------------------------ model
    public static GameObject Model(Transform parent) { return BuildHook(parent, true); }

    // Steel grappling hook along +X with its crown at the origin: a sharp nose, three dark steel prongs curling back
    // towards the ring with bright sharpened points, a steel shank with an orange paint band and an olive grip
    // sleeve, and a brass ring at the back with the rope's knot. The icon also shows the end of the rope.
    static GameObject BuildHook(Transform parent, bool icon) {
        GameObject root = ModelKit.Root("Grappling hook", parent);
        Transform r = root.transform;
        Material steel = Mats.Lit(Steel, 0.6f, 0.45f), dark = Mats.Lit(Blackened, 0.5f, 0.4f);
        Material bright = Mats.Lit(Sharpened, 0.8f, 0.5f), rope = Mats.Lit(RopeColor, 0.1f);
        Rod(r, new Vector3(-0.66f, 0f, 0f), new Vector3(-0.02f, 0f, 0f), 0.034f, 0.04f, steel, 8);                  // shank
        Rod(r, new Vector3(-0.64f, 0f, 0f), new Vector3(-0.45f, 0f, 0f), 0.047f, 0.047f, Mats.Lit(Olive, 0.15f), 8);  // grip
        Rod(r, new Vector3(-0.37f, 0f, 0f), new Vector3(-0.31f, 0f, 0f), 0.046f, 0.046f, Mats.Lit(Accent, 0.4f), 8);  // band
        ModelKit.MeshPart(r, WorldMeshes.Ball(0.08f), Vector3.zero, Vector3.one, dark, default, "Crown");
        Rod(r, new Vector3(0.04f, 0f, 0f), new Vector3(0.22f, 0f, 0f), 0.052f, 0f, bright, 6);                       // nose
        for (int k = 0; k < 3; k++) Prong(r, k * 120f, dark, bright);
        Ring(r, new Vector3(-0.745f, 0f, 0f), 0.085f, 0.02f, Mats.Lit(Brass, 0.6f, 0.45f));
        ModelKit.MeshPart(r, WorldMeshes.Ball(0.04f), RingKnot, Vector3.one, rope, default, "Knot");
        if (icon) RopeEnd(r, rope);
        return root;
    }

    // One prong, turned `roll` degrees around the shank: it leaves the crown sideways, curls back towards the ring
    // and ends in a sharpened point.
    static void Prong(Transform parent, float roll, Material arm, Material point) {
        const int Segments = 5;
        Quaternion q = Quaternion.Euler(roll, 0f, 0f);
        Vector3 prev = q * ArmPoint(0f);
        for (int i = 1; i <= Segments; i++) {
            float t0 = (i - 1f) / Segments, t1 = i / (float)Segments;
            Vector3 p = q * ArmPoint(t1);
            Rod(parent, prev, p, Mathf.Lerp(0.034f, 0.024f, t0), Mathf.Lerp(0.034f, 0.024f, t1), arm);
            prev = p;
        }
        Rod(parent, prev, prev + q * ArmTangent(1f) * 0.13f, 0.025f, 0f, point, 5);
    }

    static Vector3 ArmPoint(float t) {
        float a = Mathf.Lerp(ArmFrom, ArmTo, t) * Mathf.Deg2Rad;
        return new Vector3(ArmRadius * (Mathf.Cos(a) - 1f), ArmRadius * Mathf.Sin(a), 0f);
    }

    static Vector3 ArmTangent(float t) {
        float a = Mathf.Lerp(ArmFrom, ArmTo, t) * Mathf.Deg2Rad;
        return new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f);
    }

    // A ring in the XY plane (it faces the side-view camera), made of short overlapping bars.
    static void Ring(Transform parent, Vector3 centre, float radius, float bar, Material mat) {
        const int Segments = 12;
        for (int i = 0; i < Segments; i++) {
            float a0 = (i - 0.1f) * Mathf.PI * 2f / Segments, a1 = (i + 1.1f) * Mathf.PI * 2f / Segments;
            Rod(parent, centre + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * radius,
                centre + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * radius, bar, bar, mat);
        }
    }

    // The end of the rope hanging from the knot in a loose curl (weapon icon only: in battle the rope is a line).
    static void RopeEnd(Transform parent, Material mat) {
        Vector3[] pts = {
            RingKnot, new Vector3(-0.97f, -0.06f, 0f), new Vector3(-1.07f, -0.18f, 0f),
            new Vector3(-1.1f, -0.33f, 0f), new Vector3(-1.05f, -0.46f, 0f),
        };
        for (int i = 1; i < pts.Length; i++) {
            Rod(parent, pts[i - 1], pts[i], 0.03f, 0.03f, mat);
            ModelKit.MeshPart(parent, WorldMeshes.Ball(0.03f), pts[i], Vector3.one, mat, default, "Rope bend");
        }
    }

    // A tapered bar from a to b (radius r0 at a, r1 at b; r1 = 0 makes a point) in the parent's space.
    static void Rod(Transform parent, Vector3 a, Vector3 b, float r0, float r1, Material mat, int sides = 6) {
        Vector3 d = b - a;
        ModelKit.MeshPart(parent, WorldMeshes.Frustum(sides, r0, r1, d.magnitude), a, Vector3.one, mat,
                          Quaternion.FromToRotation(Vector3.up, d).eulerAngles, "Rod");
    }
}

// The hook's own sounds, synthesised like the rest of the game's (Synth; see SoundBank for the house recipes) the
// first time each is needed, then kept. One-shots play through the shared pool (SFX.PlayClip); the creak is a loop
// that follows the hook and stops with SFX.Stop.
static class HookSounds {
    static AudioClip twang, creak, zip;

    public static AudioClip Twang => twang != null ? twang : twang = Make("Hook twang", TwangData());
    public static AudioClip Creak => creak != null ? creak : creak = Make("Hook creak", CreakData());
    public static AudioClip Zip => zip != null ? zip : zip = Make("Hook zip", ZipData());

    // Builds the clips at the throw, so the bite does not wait for them.
    public static void Warm() {
        _ = Twang;
        _ = Creak;
        _ = Zip;
    }

    public static void Play(AudioClip clip, Vector3 pos, float volume, float pitch = 1f) { SFX.PlayClip(clip, pos, volume, pitch); }

    public static AudioSource Loop(AudioClip clip, Transform follow, float volume, float pitch = 1f) {
        if (clip == null) return null;
        GameObject go = new GameObject("[Loop] " + clip.name);
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.pitch = pitch;
        go.AddComponent<LoopVoice>().Init(follow, volume);
        source.Play();
        return source;
    }

    static AudioClip Make(string name, float[] data) {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Synth.Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // The rope snapping taut: a deep plucked thrum, the slap of the fibres and a low thump.
    static float[] TwangData() => new Synth(0.7f, 301)
        .Pluck(0f, 82f, 0.9f, 0.5f, 0.6f)
        .Pluck(0.004f, 124f, 0.4f, 0.35f, 0.45f)
        .Noise(0f, 0.05f, 0.7f, Env.Hit(0.0005f, 0.012f), Filter.Band(1400f, 1.3f))
        .Tone(0f, 0.25f, new Sweep(120f, 55f), 0.6f, Env.Hit(0.002f, 0.06f))
        .Done(0.85f);

    // A rope under strain (looped): a creaking stick-slip groan that judders and wavers, over crackling fibres.
    static float[] CreakData() => new Synth(0.8f, 302)
        .Tone(0f, 0.8f, new Sweep(150f, 205f, 140f, 0.5f), 0.5f, Env.Flat, Waveform.Saw, 9f, 0.05f, Filter.Band(new Sweep(620f, 900f, 640f, 0.5f), 3f))
        .Tone(0f, 0.8f, 72f, 0.3f, Env.Flat, Waveform.Triangle, 6f, 0.04f)
        .Crackle(0f, 0.8f, 40, 0.35f, 900f, 2600f, 1f, 0.0025f)
        .Noise(0f, 0.8f, 0.12f, Env.Flat, Filter.Band(1800f, 2f))
        .Tremolo(14f, 0.5f)
        .DoneLoop(0.12f, 0.8f);

    // Reeling in: a whizz rising in pitch over the ratchet of the reel, clicking faster and faster.
    static float[] ZipData() {
        Synth s = new Synth(0.5f, 303);
        s.Noise(0f, 0.45f, 0.7f, Env.Swell(0.75f), Filter.Band(new Sweep(450f, 3200f), 2.2f));
        s.Tone(0f, 0.42f, new Sweep(260f, 820f), 0.18f, Env.Swell(0.7f), Waveform.Triangle);
        float t = 0.02f, gap = 0.045f;
        while (t < 0.42f) {
            s.Noise(t, 0.012f, 0.45f, Env.Hit(0.0003f, 0.0025f), Filter.Band(2800f, 2.5f));
            t += gap;
            gap = Mathf.Max(0.011f, gap * 0.88f);
        }
        return s.Done(0.85f);
    }
}

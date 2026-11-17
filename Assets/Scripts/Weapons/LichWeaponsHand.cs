using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Bone Hand: the Lich throws a bone charm, a small clenched skeletal fist with green fire between its fingers.
// Where it lands - or straight under it on a click or at the bot's hint - the ground splits and a giant skeletal
// hand (BoneHand) bursts up out of it, in front of whatever stands there, fingers spread, until it reaches over the
// top. Then it slams down on that top (a blast of def.radius / def.damage that drives everything downwards), closes
// its fingers and sinks back into the ground for GripTime, dragging down every block, commander and piece of rubble
// within GripHalfWidth of it (extra downward pull, strongest at its middle, and crushing damage to the blocks in its
// grip), so walls buckle and towers fold into the ground. Over the sea it rises from the waves and drags things in.
public class BoneCharm : Projectile {
    const float Radius = 0.3f, Mass = 2f;
    const float CharmScale = 0.16f;

    Transform model;
    float tumble;
    bool raised;

    public override bool CanActivate => base.CanActivate && !raised;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        SFX.Play(SoundId.MagicCast, origin, 0.55f, 0.75f);
        WeaponFactory.Spawn<BoneCharm>("Bone Charm", owner, def, origin, velocity);
    }

    // Weapon icon: the hand itself, fingers spread, without its forearm.
    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Bone Hand", parent);
        BoneHandModel hand = BoneHandModel.Create(root.transform, 0.3f, false);
        hand.transform.localPosition = new Vector3(0f, -0.38f, 0f);
        hand.SetCurl(0.15f);
        return root;
    }

    protected override void OnLaunch(Vector3 velocity) {
        maxLifetime = 10f;
        BoneHandModel fist = BoneHandModel.Create(transform, CharmScale, true);
        fist.SetCurl(0.95f);                                       // clenched
        fist.transform.localPosition = new Vector3(0f, -0.15f, 0f);
        model = fist.transform;
        gameObject.AddComponent<SphereCollider>().radius = Radius;
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;   // only the model tumbles
        VFX.Emitter(transform, new Color(0.55f, 1f, 0.4f, 0.9f), 0.15f, 18f);
        LichSounds.WarmUp();
    }

    void Update() {
        if (finished || raised) return;
        tumble += -travelDir * 330f * Time.deltaTime;
        model.localRotation = Quaternion.Euler(0f, 0f, tumble);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (!finished && !raised && transform.position.y < World.WaterY + 0.2f) Raise(transform.position);
    }

    protected override void OnActivate() { Raise(transform.position); }

    void OnCollisionEnter(Collision c) {
        if (finished || raised) return;
        SoldierWeapons.FirstContact(c, transform.position, out Vector3 point, out _);
        SoldierWeapons.LimitShove(c);
        Raise(point);
    }

    protected override void OnLost() {
        if (!raised) Raise(transform.position);
        else Finish(true);
    }

    void Raise(Vector3 at) {
        if (raised || finished) return;
        raised = true;
        activated = true;
        at.z = 0f;
        if (!impacted) RaiseImpact(at);
        LichKit.Freeze(this);
        LichKit.SoulBurst(at, 0.6f, false);
        BoneHand.Summon(owner, def, at.x);
        Finish(true);
    }
}

// The giant hand: it rises, slams, grips and sinks back, holding the turn while it works.
public class BoneHand : MonoBehaviour {
    const float HandScale = 2f;                  // about 5 m from wrist to fingertip, a 2.6 m palm
    const float Z = -1.5f;                       // just in front of the walls (blocks span z = ±0.8)
    const float RiseTime = 0.55f, HoldTime = 0.18f, SlamTime = 0.16f, SlamDrop = 1.4f;
    const float GripTime = 1.3f, SinkTime = 0.45f;
    const float Overreach = 2.2f;                // the fingertips rise this far above the top before the slam
    const float GripHalfWidth = 2.8f;            // it drags down what lies within this distance of its middle (x)
    const float GripAccel = 30f;                 // m/s² downwards at its middle, fading to 30 % at the edge
    const float InwardPull = 2.5f;               // m/s² per metre towards its middle: the walls fold in on themselves
    const float BlockDps = 90f;                  // crushing damage to the blocks in its grip (at its middle): they give way
    const float CommanderDps = 30f;              // to a commander in its grip, less through walls (Explosion.Cover)
    const float TickEvery = 0.25f;

    static readonly Collider[] buffer = new Collider[256];

    Fortress owner;
    WeaponDef def;
    float x, ground, top, tick;
    bool gripping;
    BoneHandModel model;
    readonly HashSet<Rigidbody> pulled = new HashSet<Rigidbody>();

    public static void Summon(Fortress owner, WeaponDef def, float x) {
        BoneHand h = new GameObject("Bone Hand").AddComponent<BoneHand>();
        h.owner = owner;
        h.def = def;
        h.x = x;
        h.StartCoroutine(h.Run());
    }

    IEnumerator Run() {
        TurnActor actor = gameObject.AddComponent<TurnActor>();
        if (!LichKit.GroundAt(x, out ground)) ground = World.WaterY;
        top = Mathf.Max(ground + 1.5f, TopOfZone());
        model = BoneHandModel.Create(transform, HandScale, true);
        float reach = model.Reach;
        float riseTo = Mathf.Max(top + Overreach - reach, ground + 0.6f);   // the wrist, once risen
        float below = ground - reach - 1f;                                  // fully under the ground
        SetWrist(below);
        model.SetCurl(0f);
        if (BattleCamera.I != null) {
            Bounds view = new Bounds(new Vector3(x, ground, 0f), new Vector3(14f, 2f, 1f));
            view.Encapsulate(new Vector3(x, top + Overreach + 1.5f, 0f));
            BattleCamera.I.Frame(view, 3f);
        }

        // The ground splits and the hand bursts out, fingers spread.
        Vector3 hole = new Vector3(x, ground, 0f);
        LichSounds.Play(LichSounds.Rise, hole, 1f, 0.8f);
        VFX.Dust(hole, 2f, ground > World.WaterY + 0.1f ? new Color(0.45f, 0.36f, 0.27f, 0.8f) : new Color(0.9f, 0.93f, 0.95f, 0.6f));
        if (ground <= World.WaterY + 0.1f) VFX.Splash(hole, 2f);
        LichKit.SoulBurst(hole + Vector3.up * 0.3f, 1.4f);
        BattleCamera.Shake(0.35f);
        for (float t = 0f; t < RiseTime; t += Time.deltaTime) {
            float k = t / RiseTime;
            SetWrist(Mathf.Lerp(below, riseTo, 1f - (1f - k) * (1f - k)));
            model.SetCurl(0.35f * (1f - k));                        // the fingers unfurl as it comes up
            yield return null;
        }
        SetWrist(riseTo);
        LichSounds.Play(LichSounds.Rattle, transform.position, 0.8f, 0.7f);
        yield return new WaitForSeconds(HoldTime);

        // The slam: down onto the top, fingers half closing.
        for (float t = 0f; t < SlamTime; t += Time.deltaTime) {
            float k = t / SlamTime;
            SetWrist(riseTo - SlamDrop * k * k);
            model.SetCurl(0.5f * k);
            yield return null;
        }
        float wrist = riseTo - SlamDrop;
        SetWrist(wrist);
        Slam();

        // The grip: the fingers close and it sinks back into the ground, dragging everything with it.
        LichSounds.Play(LichSounds.Grind, transform.position, 0.95f);
        gripping = true;
        for (float t = 0f; t < GripTime; t += Time.deltaTime) {
            float k = t / GripTime;
            model.SetCurl(Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(k * 4f)));
            SetWrist(Mathf.Lerp(wrist, ground - reach * 0.6f, k * k));
            if (Random.value < Time.deltaTime * 8f)
                VFX.Dust(new Vector3(x + Random.Range(-GripHalfWidth, GripHalfWidth), ground + 0.1f, 0f), 0.8f, new Color(0.45f, 0.36f, 0.27f, 0.6f));
            yield return null;
        }
        gripping = false;
        float from = transform.position.y;
        for (float t = 0f; t < SinkTime; t += Time.deltaTime) {
            SetWrist(Mathf.Lerp(from, below, t / SinkTime));
            yield return null;
        }
        VFX.Dust(hole, 1.4f, new Color(0.45f, 0.36f, 0.27f, 0.7f));
        if (actor != null) Destroy(actor);
        Destroy(gameObject);
    }

    // The highest surface (a block's top, or the ground) within its reach across.
    float TopOfZone() {
        float best = ground;
        for (int i = 0; i <= 8; i++) {
            float sx = x + Mathf.Lerp(-GripHalfWidth * 0.7f, GripHalfWidth * 0.7f, i / 8f);
            if (Physics.Raycast(new Vector3(sx, ground + 60f, 0f), Vector3.down, out RaycastHit hit, 80f,
                                Layers.BlockMask | Layers.GroundMask, QueryTriggerInteraction.Ignore))
                best = Mathf.Max(best, hit.point.y);
        }
        return best;
    }

    void SetWrist(float y) { transform.position = new Vector3(x, y, Z); }

    // The palm comes down on the top: a blast that drives everything downwards (and out), dust, a green flare.
    void Slam() {
        Vector3 at = new Vector3(x, top, 0f);
        WakeZone();
        Explosion.Create(at, def.radius, def.damage, def.force, owner, DamageType.Crush, BlastStyle.Small, -1.3f, false);
        VFX.Dust(at, 1.8f, new Color(0.6f, 0.58f, 0.52f, 0.7f));
        VFX.Ring(at, def.radius + 0.6f, LichKit.Soul, 0.35f, 0.3f);
        LichKit.SoulBurst(at, 1.2f);
        SFX.Play(SoundId.HammerHit, at, 1f, 0.75f);
        SFX.Play(SoundId.ExplosionBig, at, 0.6f, 0.7f);
        BattleCamera.Shake(0.55f);
        TimeFX.SlowMo(0.5f, 0.08f);                  // a short hit-stop sells the blow
    }

    void FixedUpdate() {
        if (!gripping) return;
        Pull();
        tick += Time.fixedDeltaTime;
        if (tick >= TickEvery) {
            tick -= TickEvery;
            Crush();
        }
    }

    // Everything of the enemy's in its grip is pulled down (strongest at its middle) and in towards its middle.
    void Pull() {
        Vector3 c = new Vector3(x, (ground + top) * 0.5f, 0f);
        Vector3 half = new Vector3(GripHalfWidth, (top - ground) * 0.5f + 1.5f, 3f);
        int n = Physics.OverlapBoxNonAlloc(c, half, buffer, Quaternion.identity, Layers.BodiesMask, QueryTriggerInteraction.Ignore);
        pulled.Clear();
        for (int i = 0; i < n; i++) {
            Rigidbody body = buffer[i] != null ? buffer[i].attachedRigidbody : null;
            if (body == null || body.isKinematic || OwnSide(buffer[i]) || !pulled.Add(body)) continue;
            Vector3 p = body.worldCenterOfMass;
            float k = Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(Mathf.Abs(p.x - x) / GripHalfWidth));
            Vector3 a = Vector3.down * GripAccel * k + Vector3.right * ((x - p.x) * InwardPull);
            if (buffer[i].gameObject.layer == Layers.Commander) a *= 0.6f;
            body.WakeUp();
            body.AddForce(a, ForceMode.Acceleration);
        }
    }

    // Crushing damage to the enemy's blocks in its grip and, through what is left of the walls, to a commander there.
    void Crush() {
        Vector3 c = new Vector3(x, (ground + top) * 0.5f, 0f);
        Vector3 half = new Vector3(GripHalfWidth, (top - ground) * 0.5f + 1f, 2f);
        int n = Physics.OverlapBoxNonAlloc(c, half, buffer, Quaternion.identity, Layers.BlockMask | Layers.CommanderMask, QueryTriggerInteraction.Ignore);
        List<(IDamageable target, float amount, Vector3 point)> victims = new List<(IDamageable, float, Vector3)>();
        for (int i = 0; i < n; i++) {
            if (buffer[i] == null) continue;
            IDamageable d = buffer[i].GetComponentInParent<IDamageable>();
            if (d == null || !d.IsAlive || OwnSide(buffer[i])) continue;
            Vector3 p = buffer[i].bounds.center;
            float k = Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(Mathf.Abs(p.x - x) / GripHalfWidth));
            if (d is Block) victims.Add((d, BlockDps * TickEvery * k, p));
            else if (d is Commander cm) victims.Add((d, CommanderDps * TickEvery * k * Explosion.Cover(new Vector3(x, top, 0f), cm.AimPoint), p));
        }
        foreach (var v in victims) if (v.target.IsAlive) v.target.ApplyDamage(new DamageInfo(v.amount, DamageType.Crush, v.point, owner, Vector3.down * 2f));
    }

    // Its master's own blocks, commander and creatures (the grip spares them).
    bool OwnSide(Collider c) {
        if (owner == null || owner.team == null) return false;
        IDamageable d = c.GetComponentInParent<IDamageable>();
        return d != null && d.Owner != null && d.Owner.team == owner.team;
    }

    // Blocks sleep a hair apart (see Fortress.WakeUp): wake every fortress in reach so nothing hangs in the air.
    void WakeZone() {
        if (BattleManager.I == null) return;
        foreach (Fortress f in BattleManager.I.fortresses) {
            if (f == null) continue;
            Bounds b = f.GetBounds();
            if (b.max.x < x - GripHalfWidth - 1f || b.min.x > x + GripHalfWidth + 1f) continue;
            foreach (Block blk in f.blocks)
                if (blk != null && blk.rb != null && !blk.rb.isKinematic && blk.rb.IsSleeping()) blk.rb.WakeUp();
            if (f.CommanderAlive && f.commander.rb.IsSleeping()) f.commander.rb.WakeUp();
        }
    }
}

// The skeletal hand's model, built upright with the wrist at the origin and the fingers up (+y), the back of the hand
// to the camera (-z): a forearm of two long bones, a cluster of wrist bones, a palm of four bones fanning out to the
// knuckles, four three-jointed fingers and a two-jointed thumb ending in dark claws, and a green soul gem on the back
// of the hand. SetCurl closes the fingers away from the camera (into whatever the hand grips).
public class BoneHandModel : MonoBehaviour {
    // Unscaled sizes (metres at scale 1).
    static readonly float[] KnuckleX = { -0.48f, -0.16f, 0.16f, 0.48f };
    static readonly float[] Spread = { 9f, 3f, -3f, -10f };        // degrees each finger leans out from the middle
    static readonly float[] FingerLength = { 0.85f, 1.1f, 1f, 0.8f };
    static readonly float[] Phalanx = { 0.55f, 0.42f, 0.32f };     // share of the middle finger's length per joint
    static readonly float[] CurlAngle = { 55f, 70f, 60f };         // degrees per joint when fully closed
    const float PalmHeight = 1f;

    readonly List<Transform> joints = new List<Transform>();
    readonly List<int> jointIndex = new List<int>();
    readonly List<Quaternion> rest = new List<Quaternion>();
    float scale = 1f;

    // Wrist to the tip of the longest finger.
    public float Reach => (PalmHeight + FingerLength[1] * (Phalanx[0] + Phalanx[1] + Phalanx[2]) + 0.1f) * scale;

    public static BoneHandModel Create(Transform parent, float scale, bool forearm) {
        GameObject root = ModelKit.Root("Bone Hand Model", parent);
        root.transform.localScale = Vector3.one * scale;
        BoneHandModel m = root.AddComponent<BoneHandModel>();
        m.scale = scale;
        m.Build(root.transform, forearm);
        return m;
    }

    // 0 = open, 1 = a closed claw.
    public void SetCurl(float k) {
        k = Mathf.Clamp01(k);
        for (int i = 0; i < joints.Count; i++)
            joints[i].localRotation = rest[i] * Quaternion.Euler(CurlAngle[jointIndex[i]] * k, 0f, 0f);
    }

    void Build(Transform r, bool forearm) {
        Material bone = LichKit.BoneMat, dark = LichKit.BoneDarkMat;
        if (forearm) {                                                                             // radius and ulna
            LichKit.Rod(r, new Vector3(-0.2f, -0.05f, 0f), new Vector3(-0.26f, -3.3f, 0.05f), 0.15f, 0.12f, bone, 7);
            LichKit.Rod(r, new Vector3(0.2f, -0.05f, 0f), new Vector3(0.16f, -3.3f, -0.05f), 0.12f, 0.15f, bone, 7);
            LichKit.Part(r, WorldMeshes.Ball(0.24f), new Vector3(-0.05f, -3.35f, 0f), new Vector3(1.4f, 1f, 1f), dark);   // elbow
        }
        for (int k = 0; k < 6; k++)                                                                // wrist bones
            LichKit.Part(r, WorldMeshes.Ball(0.12f), new Vector3(-0.27f + k * 0.11f, (k % 2) * 0.09f, 0f), Vector3.one, k % 2 == 0 ? bone : dark);
        for (int f = 0; f < 4; f++) {                                                              // palm bones and fingers
            Vector3 knuckle = new Vector3(KnuckleX[f], PalmHeight, 0f);
            LichKit.Rod(r, new Vector3(KnuckleX[f] * 0.35f, 0.08f, 0f), knuckle, 0.095f, 0.08f, bone);
            Finger(r, knuckle, Spread[f], FingerLength[f], 3, bone, dark);
        }
        Finger(r, new Vector3(-0.42f, 0.28f, 0f), 42f, 0.85f, 2, bone, dark);                      // thumb, leaning out
        Transform gem = LichKit.Pivot("Soul Gem", r, new Vector3(0f, 0.55f, -0.12f));
        LichKit.Part(gem, WorldMeshes.Ball(0.13f), Vector3.zero, new Vector3(1f, 1.2f, 0.6f), LichKit.SoulMat);
        LichKit.Part(r, WorldMeshes.Ball(0.17f), new Vector3(0f, 0.55f, -0.06f), new Vector3(1.1f, 1.3f, 0.5f), dark);   // its setting
        ModelKit.Shadows(r.gameObject, true);
    }

    // A finger from its knuckle: `joints` phalanges on pivots (each curls about x), knuckle balls, a claw at the tip.
    void Finger(Transform parent, Vector3 knuckle, float spread, float length, int count, Material bone, Material dark) {
        Transform p = LichKit.Pivot("Finger", parent, knuckle, new Vector3(0f, 0f, spread));
        int first = 3 - count;
        for (int j = first; j < 3; j++) {
            joints.Add(p);
            jointIndex.Add(j);
            rest.Add(p.localRotation);
            float len = Phalanx[j] * length;
            LichKit.Part(p, WorldMeshes.Ball(0.105f - j * 0.014f), Vector3.zero, Vector3.one, dark);
            LichKit.Rod(p, Vector3.zero, Vector3.up * len, 0.088f - j * 0.012f, 0.074f - j * 0.012f, bone);
            Transform next = LichKit.Pivot("Joint", p, Vector3.up * len);
            if (j == 2) LichKit.Part(next, MeshKit.Cone(5, 0.05f, 0.2f), Vector3.zero, Vector3.one, dark, new Vector3(18f, 0f, 0f));   // claw
            p = next;
        }
    }
}

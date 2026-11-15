using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Bone Dragon: the Lich throws a horned dragon skull with green fire in its sockets. Where it lands (or on a click,
// or at the bot's hint) the bone dragon assembles out of the air (BoneDragon), breathes a stream of soulfire down on
// that spot, and flies home to perch on the back of the Lich's fortress. At the start of each of the Lich's next
// two turns it flies out again, sweeps over the nearest enemy fortress breathing soulfire along its top (blasts of
// def.radius / def.damage that set wood alight), and comes back. After its last pass it falls apart.
// The dragon is a creature on the battlefield: while it perches, enemy blasts, shots and bolts can hurt it (MaxHP);
// at 0 it collapses into bones. A new summon replaces a dragon still alive. It shows as "Dragon N" on the card.
public class DragonSkull : Projectile {
    const float Radius = 0.32f, Mass = 2f;

    Transform model;
    float spin;
    bool raised;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        SFX.Play(SoundId.MagicCast, origin, 0.6f, 0.8f);
        LichKit.SoulBurst(origin, 0.5f, false);
        WeaponFactory.Spawn<DragonSkull>("Dragon Skull", owner, def, origin, velocity);
    }

    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Bone Dragon", parent);
        BoneDragonModel.Skull(root.transform).transform.localPosition = new Vector3(-0.15f, 0f, 0f);
        return root;
    }

    public override bool CanActivate => base.CanActivate && !raised;

    protected override void OnLaunch(Vector3 velocity) {
        maxLifetime = 10f;
        model = BoneDragonModel.Skull(transform).transform;
        gameObject.AddComponent<SphereCollider>().radius = Radius;
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        VFX.Emitter(transform, new Color(0.55f, 1f, 0.4f, 0.9f), 0.18f, 26f);
        LichSounds.WarmUp();
    }

    void Update() {
        if (finished || raised) return;
        spin += -travelDir * 300f * Time.deltaTime;
        model.localRotation = Quaternion.Euler(0f, 0f, spin);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (!finished && !raised && transform.position.y < World.WaterY + 0.3f) Raise(new Vector3(transform.position.x, World.WaterY, 0f));
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
        BoneDragon.Summon(owner, def, at);
        Finish(true);
    }
}

// The bone dragon itself: a TurnEffect that is also a body on the battlefield (it can be hurt while it perches).
public class BoneDragon : TurnEffect, IDamageable {
    public const float MaxHP = 60f;
    const int Passes = 2;                        // turn-start passes after the first strike
    const float FlySpeed = 15f, SweepSpeed = 8f, Altitude = 4.2f;
    const float BlastEvery = 0.9f;               // metres between soulfire blasts on a sweep
    const float MouthLead = 1.8f;                // its breath lands this far ahead of its body, so it flies that far behind the line it burns
    const float HitDamage = 25f;                 // a shot that strikes it (blasts and bolts hurt it by their own damage)
    const float IgniteSeconds = 3.5f;

    WeaponDef def;
    BoneDragonModel model;
    SphereCollider hull;
    TeamBody team;
    float hp = MaxHP;
    int passesLeft = Passes;
    bool dying, busy;
    readonly HashSet<Projectile> struck = new HashSet<Projectile>();
    float lastFeedback = -10f;

    public Fortress Owner => owner;
    public bool IsAlive => this != null && !dying && hp > 0f;
    public override string StatusText => dying ? null : "Dragon " + Mathf.Max(1, passesLeft);   // the last pass still shows 1
    public override Color StatusColor => LichKit.Soul;
    public override int RewindState {
        get => passesLeft * 1000 + Mathf.RoundToInt(hp);
        set { passesLeft = value / 1000; hp = Mathf.Max(1f, value % 1000); }
    }

    // The dragon of this fortress that is still alive, if any.
    public static BoneDragon Of(Fortress f) {
        foreach (TurnEffect e in All) if (e is BoneDragon d && d.owner == f && d.IsAlive) return d;
        return null;
    }

    public static void Summon(Fortress owner, WeaponDef def, Vector3 at) {
        BoneDragon old = Of(owner);
        if (old != null) old.Collapse(false);
        BoneDragon d = new GameObject("Bone Dragon").AddComponent<BoneDragon>();
        d.Init(owner, null);
        d.def = def;
        d.transform.position = new Vector3(at.x, Mathf.Max(at.y, World.WaterY) + 1f, 0f);
        d.model = BoneDragonModel.Create(d.transform);
        Rigidbody body = d.gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        d.gameObject.layer = Layers.Block;             // blasts, bolts and shots find it like a block
        d.hull = d.gameObject.AddComponent<SphereCollider>();
        d.hull.radius = 0.8f;
        d.hull.center = new Vector3(0f, 0.1f, 0f);
        d.hull.isTrigger = true;                       // solid only while it perches (in flight it must not shove walls)
        d.team = TeamBody.Add(d.gameObject, owner, d.hull);
        d.StartCoroutine(d.FirstStrike(at));
    }

    // ------------------------------------------------------------------ the summon
    // Bones whirl together out of green fire, it roars, breathes down on the spot and flies home.
    IEnumerator FirstStrike(Vector3 at) {
        TurnActor actor = gameObject.AddComponent<TurnActor>();
        busy = true;
        int dir = owner != null && at.x < owner.transform.position.x ? 1 : -1;   // it sweeps the spot on its way home
        if (owner != null && Mathf.Abs(at.x - owner.transform.position.x) < 0.5f) dir = owner.Facing;
        model.Face(dir);
        if (BattleCamera.I != null) {                  // the skull has finished: the camera watches the dragon instead
            Bounds view = new Bounds(at, new Vector3(12f, 4f, 1f));
            view.Encapsulate(at + Vector3.up * (Altitude + 3f));
            BattleCamera.I.Frame(view, 3f);
        }
        LichSounds.Play(LichSounds.Rise, at, 0.7f);
        LichKit.SoulBurst(at + Vector3.up, 1.3f);
        VFX.Ring(at + Vector3.up, 3f, LichKit.Soul, 0.5f, 0.3f);
        for (float t = 0f; t < 0.6f; t += Time.deltaTime) {
            model.Assemble(t / 0.6f);
            model.Flap(t, 1f);
            yield return null;
        }
        model.Assemble(1f);
        LichSounds.Play(LichSounds.Roar, transform.position, 0.9f);
        BattleCamera.Shake(0.25f);
        float surface = World.WaterY;               // the highest top under its whole flight (body MouthLead behind a +-2.4 m breath line)
        for (float sx = at.x - 4.2f; sx <= at.x + 4.2f; sx += 0.5f) surface = Mathf.Max(surface, Surface(sx, 60f));
        Vector3 high = new Vector3(at.x - dir * 2.4f, Mathf.Max(surface, World.WaterY) + Altitude, 0f);
        yield return FlyTo(high - Vector3.right * dir * MouthLead, FlySpeed * 0.7f);
        yield return Sweep(high.x, at.x + dir * 2.4f, high.y);
        FrameFlightHome();
        yield return Home();
        busy = false;
        if (actor != null) Destroy(actor);
    }

    // ------------------------------------------------------------------ every turn
    public override IEnumerator OnTurnStart(Fortress shooter) {
        if (!IsAlive) yield break;
        if (owner == null || owner.defeated) { Collapse(true); yield break; }
        if (shooter != owner || busy) yield break;
        Fortress target = LichKit.NearestEnemy(owner, transform.position.x);
        if (target == null) yield break;
        busy = true;
        passesLeft--;
        Bounds b = target.GetBounds();
        int dir = b.center.x > transform.position.x ? 1 : -1;
        float y = b.max.y + Altitude;
        if (BattleCamera.I != null) {
            Bounds view = b;
            view.Encapsulate(new Vector3(b.center.x, y + 2f, 0f));
            BattleCamera.I.Frame(view, 4f);
        }
        LichSounds.Play(LichSounds.Roar, transform.position, 0.8f, Random.Range(0.92f, 1.05f));
        float near = (dir > 0 ? b.min.x : b.max.x) - dir * 0.5f, far = (dir > 0 ? b.max.x : b.min.x) + dir * 0.5f;
        yield return FlyTo(new Vector3(near - dir * MouthLead, y, 0f), FlySpeed);
        if (!IsAlive) yield break;
        yield return Sweep(near, far, y);
        if (!IsAlive) yield break;
        FrameFlightHome();
        yield return Home();
        busy = false;
        if (passesLeft <= 0) {
            yield return new WaitForSeconds(0.2f);
            Collapse(true);
            yield return new WaitForSeconds(0.6f);
        }
        if (BattleCamera.I != null && shooter != null) BattleCamera.I.FrameForAim(shooter);
    }

    // Level flight along y, breathing soulfire on whatever lies below every BlastEvery metres from x0 to x1 (its body
    // flies MouthLead behind the line its breath burns).
    IEnumerator Sweep(float x0, float x1, float y) {
        int dir = x1 >= x0 ? 1 : -1;
        model.Face(dir);
        model.Jaw(1f);
        LichSounds.Play(LichSounds.Breath, transform.position, 0.85f);
        float x = x0, nextBlast = x0 + dir * 0.4f;
        transform.position = new Vector3(x0 - dir * MouthLead, y, 0f);
        while ((x1 - x) * dir > 0f) {
            float dt = Time.deltaTime;
            x = dir > 0 ? Mathf.Min(x + SweepSpeed * dt, x1) : Mathf.Max(x - SweepSpeed * dt, x1);
            transform.position = new Vector3(x - dir * MouthLead, y + Mathf.Sin(Time.time * 6f) * 0.08f, 0f);
            model.Flap(Time.time, 1.2f);
            Stream(dt);
            if ((x - nextBlast) * dir >= 0f) {
                nextBlast += dir * BlastEvery;
                Blast(x, y);
            }
            if (!IsAlive) { model.Jaw(0f); yield break; }
            yield return null;
        }
        model.Jaw(0f);
    }

    // One gout of soulfire where the breath meets something below: a blast (full damage on stone as well as wood; like
    // any blast it hurts whatever it reaches) that also sets the enemy's wood alight.
    void Blast(float x, float fromY) {
        Vector3 top = new Vector3(x, fromY, 0f);
        Vector3 at = Physics.Raycast(top, Vector3.down, out RaycastHit hit, fromY - World.WaterY + 1f, Layers.SolidMask, QueryTriggerInteraction.Ignore)
            ? hit.point : new Vector3(x, World.WaterY, 0f);
        at.z = 0f;
        Explosion.Create(at, def.radius, def.damage, def.force, owner, DamageType.Explosion, BlastStyle.Small, 0.3f, false);
        LichKit.SoulBurst(at, 1f);
        SFX.Play(SoundId.ExplosionSmall, at, 0.45f, 0.85f);
        BattleCamera.Shake(0.1f);
        Collider[] near = Physics.OverlapSphere(at, def.radius, Layers.BlockMask, QueryTriggerInteraction.Ignore);
        foreach (Collider c in near) {
            Block b = c.GetComponent<Block>();
            if (b != null && b.IsAlive && b.def.flammable && b.fortress != null && owner != null && b.fortress.team != owner.team)
                b.Ignite(IgniteSeconds, owner);
        }
    }

    // The breath itself: green flames pouring down and forward from the open jaws.
    void Stream(float dt) {
        Vector3 mouth = LichKit.Front(model.Mouth.position);
        int n = Mathf.Max(1, Mathf.RoundToInt(dt * 90f));
        for (int i = 0; i < n; i++) {
            Vector3 v = new Vector3(model.Facing * Random.Range(1.5f, 3.5f), Random.Range(-9f, -6f), 0f);
            FxLayers.Fire.Emit(mouth, v, Random.Range(0.45f, 0.9f), Random.Range(0.35f, 0.55f), LichKit.Soul);
        }
    }

    // The camera widens to keep the dragon in view on its way back to its perch (the turn waits for it).
    void FrameFlightHome() {
        if (BattleCamera.I == null || owner == null) return;
        Bounds view = owner.GetBounds();
        view.Encapsulate(transform.position + Vector3.up * 2f);
        BattleCamera.I.Frame(view, 3f);
    }

    // Back to its perch on the Lich's fortress.
    IEnumerator Home() {
        if (owner == null) yield break;
        Vector3 perch = Perch();
        yield return FlyTo(perch, FlySpeed);
        model.Face(owner.Facing);
        if (!IsAlive) yield break;
        if (team != null) team.IgnoreOwn();            // its fortress may have blocks it never met (a time rewind)
        hull.isTrigger = false;
    }

    // Along a gentle arc to `to`, wings beating.
    IEnumerator FlyTo(Vector3 to, float speed) {
        hull.isTrigger = true;
        Vector3 from = transform.position;
        float dist = Vector3.Distance(from, to);
        float time = Mathf.Max(0.3f, dist / speed);
        Vector3 mid = (from + to) * 0.5f + Vector3.up * Mathf.Min(dist * 0.18f, 4f);
        model.Face(to.x >= from.x ? 1 : -1);
        for (float t = 0f; t < time; t += Time.deltaTime) {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            float u = 1f - k;
            transform.position = u * u * from + 2f * u * k * mid + k * k * to;
            model.Flap(Time.time, 1f);
            if (!IsAlive) yield break;
            yield return null;
        }
        transform.position = to;
    }

    // On the back of its master's fortress: the highest point between the back wall and a little behind the commander
    // (clear of his throws), wings half folded.
    Vector3 Perch() {
        Bounds b = owner.GetBounds();
        int f = owner.Facing >= 0 ? 1 : -1;
        float back = (f > 0 ? b.min.x : b.max.x) + f * 0.6f;
        float front = owner.CommanderAlive ? owner.commander.transform.position.x - f * 2.2f : b.center.x;
        if ((front - back) * f < 0f) front = back;
        float bestX = back, bestY = float.MinValue;
        for (int i = 0; i <= 6; i++) {
            float x = Mathf.Lerp(back, front, i / 6f);
            float y = Surface(x, b.max.y + 3f);
            if (y > bestY + 0.05f) { bestY = y; bestX = x; }
        }
        return new Vector3(bestX, Mathf.Max(bestY, owner.transform.position.y) + 0.75f, 0f);
    }

    static float Surface(float x, float fromY) {
        return Physics.Raycast(new Vector3(x, fromY, 0f), Vector3.down, out RaycastHit hit, fromY - World.WaterY + 1f,
                               Layers.BlockMask | Layers.GroundMask, QueryTriggerInteraction.Ignore) ? hit.point.y : World.WaterY;
    }

    void Update() {
        if (dying || busy || model == null) return;
        model.Flap(Time.time * 0.35f, 0.35f);          // perched: slow wing beats, a soft bob
        model.Jaw(0f);
    }

    // ------------------------------------------------------------------ getting hurt
    public void ApplyDamage(DamageInfo d) {
        if (!IsAlive || d.amount <= 0f) return;
        if (d.source != null && owner != null && d.source.team == owner.team) return;   // its master's magic spares it
        float amount = d.type == DamageType.Fire ? d.amount * 0.5f : d.amount;           // old bones burn badly
        hp -= amount;
        if (d.source != null) d.source.damageDealt += Mathf.Min(amount, hp + amount);
        if (Time.time > lastFeedback + 0.25f) {
            lastFeedback = Time.time;
            VFX.FloatingText(transform.position + Vector3.up * 1.6f, "-" + Mathf.RoundToInt(amount), LichKit.Bone, 0.9f);
            SFX.Play(SoundId.ImpactStone, transform.position, 0.5f, 1.3f);
        }
        VFX.Sparks(LichKit.Front(transform.position), LichKit.Bone, 8);
        model.Flash();
        if (hp <= 0f) Collapse(true);
    }

    // An enemy shot that strikes it hurts it once, whichever of the two collision callbacks runs first (a shot that
    // finished in this very physics step still counts; one lying about from earlier does not).
    void OnCollisionEnter(Collision c) {
        if (!IsAlive || c.rigidbody == null) return;
        Projectile p = c.rigidbody.GetComponent<Projectile>();
        if (p != null && (!p.finished || p.finishedAt >= Time.fixedTime) && struck.Add(p) && p.owner != null && owner != null && p.owner.team != owner.team)
            ApplyDamage(new DamageInfo(HitDamage, DamageType.Impact, transform.position, p.owner));
    }

    // It falls apart: the bones rain down, the green fire in its chest goes out.
    public void Collapse(bool show) {
        if (dying) return;
        dying = true;
        if (hull != null) hull.isTrigger = true;
        StopAllCoroutines();
        if (show) StartCoroutine(Fall());
        else Destroy(gameObject);
    }

    IEnumerator Fall() {
        TurnActor actor = gameObject.AddComponent<TurnActor>();
        LichSounds.Play(LichSounds.Roar, transform.position, 0.7f, 1.35f);
        LichSounds.Play(LichSounds.Rattle, transform.position, 0.8f, 0.8f);
        LichKit.SoulBurst(transform.position, 1.2f);
        Vector3 from = transform.position;
        for (float t = 0f; t < 0.7f; t += Time.deltaTime) {
            float k = t / 0.7f;
            model.Assemble(1f - k);
            transform.position = from + Vector3.down * (k * k * 2.5f);
            yield return null;
        }
        LichKit.BoneScatter(transform.position, 1.6f);
        if (actor != null) Destroy(actor);
        Destroy(gameObject);
    }
}

// The bone dragon's model, facing +x, about 3.2 m from snout to tail, its chest at the origin: a horned skull with
// glowing eyes and an opening jaw on a curved neck of vertebrae, a ribcage with a soulfire heart, four bony legs, a
// long tail with a spade tip, and two wings of bone spars and tattered membrane on shoulder pivots.
public class BoneDragonModel : MonoBehaviour {
    Transform flip, wingNear, wingFar, jaw, mouth;
    Renderer[] renderers;
    MaterialPropertyBlock mpb;
    float flash, jawOpen, jawTarget;
    int facing = 1;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static Mesh membrane;

    public Transform Mouth => mouth;
    public int Facing => facing;

    public static BoneDragonModel Create(Transform parent) {
        GameObject root = ModelKit.Root("Dragon Model", parent);
        BoneDragonModel m = root.AddComponent<BoneDragonModel>();
        m.flip = LichKit.Pivot("Flip", root.transform, Vector3.zero);
        m.Build(m.flip);
        m.renderers = root.GetComponentsInChildren<Renderer>(true);
        m.Assemble(0f);
        return m;
    }

    public void Face(int dir) {
        facing = dir >= 0 ? 1 : -1;
        flip.localScale = new Vector3(facing, 1f, 1f);
    }

    // 0 = nothing there yet, 1 = whole (bones whirl in from a little bigger, as green fire).
    public void Assemble(float k) {
        k = Mathf.Clamp01(k);
        transform.localScale = Vector3.one * Mathf.Max(0.001f, k < 1f ? Mathf.SmoothStep(0f, 1f, k) : 1f);
    }

    // Wing beat: t = time, strength 0..1 (perched: slow and small).
    public void Flap(float t, float strength) {
        float a = Mathf.Lerp(30f, 25f + 55f * (0.5f + 0.5f * Mathf.Sin(t * 7f)), strength);
        if (wingNear != null) wingNear.localRotation = Quaternion.Euler(-a, 0f, 0f);
        if (wingFar != null) wingFar.localRotation = Quaternion.Euler(a, 0f, 0f);
    }

    public void Jaw(float open) { jawTarget = open; }

    public void Flash() { flash = 1f; }

    void Update() {
        float dt = Time.deltaTime;
        jawOpen = Mathf.MoveTowards(jawOpen, jawTarget, dt * 6f);
        if (jaw != null) jaw.localRotation = Quaternion.Euler(0f, 0f, -38f * jawOpen);
        if (flash > 0f) {
            flash = Mathf.Max(0f, flash - dt * 4f);
            mpb ??= new MaterialPropertyBlock();
            foreach (Renderer r in renderers) {
                if (r == null) continue;
                if (flash <= 0f) { r.SetPropertyBlock(null); continue; }
                Color baseC = r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId) ? r.sharedMaterial.GetColor(BaseColorId) : Color.white;
                mpb.SetColor(BaseColorId, Color.Lerp(baseC, new Color(1f, 0.35f, 0.3f), flash));
                r.SetPropertyBlock(mpb);
            }
        }
    }

    void Build(Transform r) {
        Material bone = LichKit.BoneMat, dark = LichKit.BoneDarkMat;
        // Spine from the chest back to the hips, ribs around it and a soulfire heart inside.
        for (int k = 0; k <= 6; k++) {
            float x = -k * 0.16f;
            LichKit.Part(r, WorldMeshes.Ball(0.07f), new Vector3(x, 0.12f - k * 0.01f, 0f), Vector3.one, dark);
        }
        for (int k = 0; k < 4; k++) {
            float x = -0.12f - k * 0.17f, rad = Mathf.Lerp(0.3f, 0.2f, k / 3f);
            LichKit.Part(r, WorldMeshes.Frustum(10, rad, rad * 0.94f, 0.05f), new Vector3(x, -0.05f, 0f), new Vector3(1f, 1f, 0.8f), bone, new Vector3(0f, 0f, 90f));
        }
        LichKit.Part(r, WorldMeshes.Ball(0.14f), new Vector3(-0.3f, -0.04f, 0f), Vector3.one, LichKit.SoulMat);
        LichKit.Glow(r, new Vector3(-0.3f, 0f, -0.8f), 1.1f, 3f);
        // Neck curving up to the head.
        Vector3 prev = new Vector3(0.12f, 0.12f, 0f);
        for (int k = 1; k <= 5; k++) {
            float f = k / 5f;
            Vector3 p = new Vector3(0.12f + 0.62f * f, 0.12f + 0.5f * Mathf.Sin(f * Mathf.PI * 0.55f), 0f);
            LichKit.Part(r, WorldMeshes.Ball(Mathf.Lerp(0.075f, 0.055f, f)), p, Vector3.one, dark);
            prev = p;
        }
        Transform head = LichKit.Pivot("Head", r, prev + new Vector3(0.1f, 0.03f, 0f), new Vector3(0f, 0f, -12f));
        Skull(head, out jaw, out mouth);
        // Legs: thigh and shin bones with three claws, hanging back as it flies.
        for (int i = 0; i < 4; i++) {
            float x = i < 2 ? 0.02f : -0.86f, z = i % 2 == 0 ? -0.2f : 0.2f;
            Vector3 hip = new Vector3(x, -0.12f, z), knee = hip + new Vector3(-0.2f, -0.3f, 0f), foot = knee + new Vector3(0.12f, -0.28f, 0f);
            LichKit.Rod(r, hip, knee, 0.045f, 0.035f, bone);
            LichKit.Rod(r, knee, foot, 0.035f, 0.025f, bone);
            for (int c = -1; c <= 1; c++)
                LichKit.Part(r, MeshKit.Cone(4, 0.018f, 0.1f), foot + new Vector3(0.02f, 0f, c * 0.035f), Vector3.one, dark, new Vector3(0f, 0f, -100f));
        }
        // Tail: tapering vertebrae and a spade.
        Vector3 t0 = new Vector3(-1.1f, 0.04f, 0f);
        for (int k = 0; k < 8; k++) {
            float f = k / 7f;
            Vector3 p = t0 + new Vector3(-0.17f * k, -0.04f * k - 0.12f * Mathf.Sin(f * Mathf.PI), 0f);
            LichKit.Part(r, WorldMeshes.Ball(Mathf.Lerp(0.065f, 0.028f, f)), p, Vector3.one, k % 2 == 0 ? bone : dark);
            if (k == 7) LichKit.Part(r, MeshKit.Cone(3, 0.09f, 0.22f), p, new Vector3(1f, 1f, 0.3f), dark, new Vector3(0f, 0f, 100f));
        }
        // Wings.
        wingNear = Wing(r, -1, bone);
        wingFar = Wing(r, 1, bone);
        ModelKit.Shadows(r.gameObject, true);
    }

    // A wing on a shoulder pivot: an arm bone up and back to the wrist, three finger spars fanning back from it, and
    // a torn membrane between them. At rotation 0 it stands straight up (seen whole from the side); the pivot's x
    // rotation swings the near wing towards the camera and the far one away.
    Transform Wing(Transform parent, int side, Material bone) {
        Transform w = LichKit.Pivot(side < 0 ? "Wing Near" : "Wing Far", parent, new Vector3(0.02f, 0.22f, side * 0.24f));
        Vector3 wrist = new Vector3(-0.15f, 0.95f, 0f);
        LichKit.Rod(w, Vector3.zero, wrist * 0.5f, 0.05f, 0.04f, bone);
        LichKit.Rod(w, wrist * 0.5f, wrist, 0.04f, 0.03f, bone);
        Vector3[] tips = { new Vector3(-0.75f, 1.25f, 0f), new Vector3(-1.25f, 0.75f, 0f), new Vector3(-1.1f, 0.1f, 0f) };
        foreach (Vector3 tip in tips) {
            LichKit.Rod(w, wrist, tip, 0.025f, 0.008f, bone);
            LichKit.Part(w, MeshKit.Cone(4, 0.02f, 0.08f), tip, Vector3.one, LichKit.BoneDarkMat, Quaternion.FromToRotation(Vector3.up, tip - wrist).eulerAngles);
        }
        GameObject skin = LichKit.Part(w, Membrane(wrist, tips), Vector3.zero, Vector3.one, Mats.Transparent(new Color(0.16f, 0.32f, 0.22f, 0.55f), 0.3f));
        ModelKit.Shadows(skin, false);
        return w;
    }

    // The membrane: triangles from the shoulder and wrist to the finger tips, the trailing edges torn into ragged
    // notches. Both sides are drawn.
    static Mesh Membrane(Vector3 wrist, Vector3[] tips) {
        if (membrane != null) return membrane;
        MeshBuilder b = new MeshBuilder();
        System.Func<Vector3, Vector2> uv = p => new Vector2(p.x, p.y);
        void Tri(Vector3 a, Vector3 c, Vector3 d) { b.Facet(a, c, d, uv); b.Facet(a, d, c, uv); }
        Vector3 root = Vector3.zero, body = new Vector3(-0.85f, -0.05f, 0f);
        for (int i = 0; i < tips.Length - 1; i++) {
            Vector3 a = tips[i], c = tips[i + 1];
            Vector3 notch = Vector3.Lerp(a, c, 0.5f) + (wrist - Vector3.Lerp(a, c, 0.5f)) * 0.35f;   // a torn bite out of the edge
            Tri(wrist, a, notch);
            Tri(wrist, notch, c);
        }
        Vector3 last = tips[tips.Length - 1];
        Vector3 rag = Vector3.Lerp(last, body, 0.5f) + new Vector3(0.1f, 0.25f, 0f);
        Tri(wrist, last, rag);
        Tri(wrist, rag, body);
        Tri(root, wrist, body);
        membrane = b.ToMesh("Dragon wing membrane");
        return membrane;
    }

    // The skull alone (also the thrown projectile and the weapon icon): long and horned, green fire in the sockets,
    // a row of fangs. Faces +x.
    public static GameObject Skull(Transform parent) { return Skull(parent, out _, out _); }

    static GameObject Skull(Transform parent, out Transform jaw, out Transform mouth) {
        GameObject root = ModelKit.Root("Dragon Skull", parent);
        Transform r = root.transform;
        Material bone = LichKit.BoneMat, dark = LichKit.BoneDarkMat;
        LichKit.Part(r, WorldMeshes.Ball(0.16f), Vector3.zero, new Vector3(1.25f, 0.85f, 0.85f), bone);
        LichKit.Part(r, WorldMeshes.Ball(0.1f), new Vector3(0.24f, -0.03f, 0f), new Vector3(1.9f, 0.7f, 0.8f), bone);       // snout
        for (int s = -1; s <= 1; s += 2) {
            LichKit.Part(r, WorldMeshes.Ball(0.05f), new Vector3(0.07f, 0.05f, s * 0.1f), Vector3.one, LichKit.SocketMat);
            LichKit.Part(r, WorldMeshes.Ball(0.028f), new Vector3(0.09f, 0.05f, s * 0.115f), Vector3.one, LichKit.SoulMat);
            LichKit.Part(r, MeshKit.Cone(5, 0.045f, 0.34f), new Vector3(-0.08f, 0.1f, s * 0.07f), Vector3.one, dark, new Vector3(s * 10f, 0f, 62f));   // horns
            LichKit.Part(r, MeshKit.Cone(4, 0.03f, 0.18f), new Vector3(-0.16f, 0f, s * 0.08f), Vector3.one, dark, new Vector3(s * 14f, 0f, 95f));   // cheek spikes
        }
        for (int k = 0; k < 4; k++)                                                                                           // upper fangs
            LichKit.Part(r, MeshKit.Cone(4, 0.014f, 0.06f), new Vector3(0.18f + k * 0.06f, -0.09f, 0f), Vector3.one, bone, new Vector3(180f, 0f, 0f));
        jaw = LichKit.Pivot("Jaw", r, new Vector3(-0.05f, -0.08f, 0f));
        LichKit.Rod(jaw, Vector3.zero, new Vector3(0.42f, -0.04f, 0f), 0.05f, 0.025f, bone);
        for (int k = 0; k < 3; k++)
            LichKit.Part(jaw, MeshKit.Cone(4, 0.012f, 0.05f), new Vector3(0.22f + k * 0.07f, -0.01f, 0f), Vector3.one, bone);
        mouth = LichKit.Pivot("Mouth", r, new Vector3(0.4f, -0.1f, 0f));
        ModelKit.Shadows(root, true);
        return root;
    }
}

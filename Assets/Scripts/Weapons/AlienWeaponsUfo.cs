using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// The Alien's Flying Saucer. It flies the arc like any throw. On a click, a bot's hint, its first contact, or
// once it passes over an enemy fortress, it brakes and hovers HoverHeight above that spot and switches on its
// tractor beam for BeamTime seconds:
//  * debris and a commander with nothing over his head are made weightless, lifted and drawn to the middle;
//    debris that reaches the saucer is swallowed;
//  * enemy blocks inside are made light (BlockLift of gravity cancelled) but not lifted, or the whole middle of
//    a fortress would float off, and they take def.damage per second;
//  * one after another the highest enemy block in the cone is pulled out and up into the saucer (abducted,
//    MaxAbductions at most). A commander is lifted and dropped, never taken.
// Then it zooms off. It stays a Projectile (and so holds the turn) until it has left.
public class AlienSaucer : Projectile {
    const float Mass = 1.5f;
    const float HoverHeight = 6.5f;          // above the surface it stops over
    const float FocusDrop = 3f;              // the camera follows a point this far under the saucer: the beam
    const float GlideSpeed = 12f, MinGlide = 0.4f, MaxGlide = 1.4f;
    const float BeamTime = 3.2f, Warmup = 0.35f;
    const float TopRadius = 0.6f, MaxLength = 20f;   // the cone widens from TopRadius to def.radius at its foot
    const float BlockLift = 0.85f;           // share of gravity cancelled for the blocks in the cone
    const float DebrisLift = 13f;            // m/s² upwards for debris, on top of cancelling gravity
    const float Centering = 3f, Drag = 1.5f;
    const float CommanderHang = 2.4f;        // a lifted commander hangs this far under the saucer
    const float GrabEvery = 0.2f, GrabSpeed = 13f, GrabTimeout = 1.6f, Reach = 1.1f;
    const int MaxAbductions = 5;
    const float TickEvery = 0.25f;
    const float LeaveTime = 0.9f;

    enum Phase { Flying, Gliding, Beaming, Leaving }

    static readonly Collider[] buffer = new Collider[256];
    static readonly RaycastHit[] rayHits = new RaycastHit[32];

    readonly HashSet<Rigidbody> pulled = new HashSet<Rigidbody>();
    readonly List<Block> targets = new List<Block>();
    List<Fortress> enemies;
    Phase phase;
    Transform saucer, hull, emitter;
    CapsuleCollider body;
    AlienBeam beam;
    AudioSource hum, beamHum;
    Vector3 glideFrom, hoverPoint, beamTop, leaveVelocity;
    float glideTime, phaseTime, prevX, bottomY, beamLength, tick, nextGrab, grabTime;
    int abductions;
    Block grabbed;
    Commander lifted;

    public override bool CanActivate => base.CanActivate && phase == Phase.Flying;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        AlienSounds.Play(AlienSounds.Warble, origin, 0.8f);
        VFX.MagicBurst(origin, AlienKit.Beam, 0.5f);
        WeaponFactory.Spawn<AlienSaucer>("Flying Saucer", owner, def, origin, velocity);
    }

    protected override void OnLaunch(Vector3 velocity) {
        maxLifetime = 24f;                   // safety net: flight, hover and exit normally take far less
        saucer = AlienKit.Saucer(transform, true, false, out hull, out emitter).transform;
        body = gameObject.AddComponent<CapsuleCollider>();
        body.direction = 0;                  // a flat disc: a capsule lying along x
        body.radius = 0.22f;
        body.height = 1.15f;
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;   // only the model tilts
        VFX.Trail(saucer, new Color(0.5f, 1f, 0.7f, 0.45f), 0.35f, 0.3f);
        VFX.Emitter(saucer, AlienKit.Beam * 1.6f, 0.12f, 18f).transform.localPosition = new Vector3(0f, -0.16f, 0f);
        AlienKit.Glow(saucer, AlienKit.Beam, 2.5f, 5f);
        hum = AlienSounds.Loop(AlienSounds.Hum, saucer, 0.5f);
        prevX = transform.position.x;
        enemies = BattleManager.I != null && owner != null ? BattleManager.I.EnemiesOf(owner) : new List<Fortress>();
    }

    protected override void OnDestroy() {
        base.OnDestroy();
        SFX.Stop(hum);
        SFX.Stop(beamHum);
    }

    // ------------------------------------------------------------------ flight and stopping
    protected override void OnActivate() { StopOver(transform.position.x); }

    void OnCollisionEnter(Collision c) {
        if (phase != Phase.Flying || finished) return;
        Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        RaiseImpact(p);
        VFX.Sparks(p, AlienKit.Beam, 10);
        SFX.Play(SoundId.ImpactMetal, p, 0.5f, 1.3f);
        StopOver(p.x);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished) return;
        if (phase == Phase.Flying) CheckStop();
        else if (phase == Phase.Beaming) Beam(Time.fixedDeltaTime);
    }

    // Skimming the sea, or crossing the middle of an enemy fortress: stop there.
    void CheckStop() {
        Vector3 p = transform.position;
        if (p.y < World.WaterY + 0.5f) { StopOver(p.x); return; }
        if (p.x != prevX)
            foreach (Fortress f in enemies) {
                if (f == null || f.defeated) continue;
                float mid = f.CommanderAlive ? f.commander.transform.position.x : f.transform.position.x;
                if ((prevX - mid) * (p.x - mid) <= 0f) { StopOver(mid); return; }
            }
        prevX = p.x;
    }

    // From here on the saucer flies itself: no more physics, no more contacts.
    void StopOver(float x) {
        if (phase != Phase.Flying) return;
        phase = Phase.Gliding;
        phaseTime = 0f;
        rb.linearVelocity = Vector3.zero;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;   // the only mode kinematic bodies support
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        body.enabled = false;
        float surface = SurfaceBelow(x, transform.position.y);
        bottomY = GroundBelow(x);
        hoverPoint = new Vector3(x, surface + HoverHeight, 0f);
        glideFrom = transform.position;
        glideTime = Mathf.Clamp(Vector3.Distance(glideFrom, hoverPoint) / GlideSpeed, MinGlide, MaxGlide);
        if (!impacted) RaiseImpact(new Vector3(x, surface, 0f));
        AlienSounds.Play(AlienSounds.Warble, transform.position, 0.6f, 0.75f);
    }

    // The top of whatever stands under x (a fortress, a commander, the ground), else the sea. Our own blocks count
    // too, so a saucer stopped early never parks inside our own walls (its beam ignores them).
    static float SurfaceBelow(float x, float fromY) {
        Vector3 from = new Vector3(x, Mathf.Max(fromY, 0f) + 40f, 0f);
        int n = Physics.RaycastNonAlloc(from, Vector3.down, rayHits, from.y - World.WaterY + 1f, Layers.SolidMask, QueryTriggerInteraction.Ignore);
        float top = World.WaterY;
        for (int i = 0; i < n; i++) top = Mathf.Max(top, rayHits[i].point.y);
        return top;
    }

    // Where the beam ends: the island under x, else the sea.
    static float GroundBelow(float x) {
        return Physics.Raycast(new Vector3(x, 150f, 0f), Vector3.down, out RaycastHit hit, 300f, Layers.GroundMask, QueryTriggerInteraction.Ignore)
            ? hit.point.y : World.WaterY;
    }

    void Update() {
        float dt = Time.deltaTime;
        hull.Rotate(0f, (phase == Phase.Beaming ? 300f : 150f) * dt, 0f, Space.Self);   // the rim lights circle
        switch (phase) {
            case Phase.Flying: Bank(Velocity); break;
            case Phase.Gliding: Glide(dt); break;
            case Phase.Beaming: Hover(); break;
            case Phase.Leaving: Leave(dt); break;
        }
    }

    // The disc tilts with its flight path.
    void Bank(Vector3 v) {
        if (v.sqrMagnitude < 0.01f) return;
        float pitch = Mathf.Atan2(v.y, Mathf.Abs(v.x)) * Mathf.Rad2Deg;
        saucer.localRotation = Quaternion.Euler(0f, 0f, travelDir * Mathf.Clamp(pitch * 0.4f, -22f, 22f));
    }

    // Glides to the hover point and levels out. The root (followed by the camera) ends FocusDrop under the saucer.
    void Glide(float dt) {
        phaseTime += dt;
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phaseTime / glideTime));
        transform.position = Vector3.Lerp(glideFrom, hoverPoint + Vector3.down * FocusDrop, k);
        saucer.localPosition = Vector3.up * (FocusDrop * k);
        saucer.localRotation = Quaternion.Slerp(saucer.localRotation, Quaternion.identity, dt * 6f);
        if (k >= 1f) StartBeam();
    }

    void Hover() {
        float t = Time.time;
        saucer.localPosition = Vector3.up * (FocusDrop + Mathf.Sin(t * 2.3f) * 0.08f);
        saucer.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.7f) * 3f, 0f, Mathf.Sin(t * 2.1f) * 4f);
        emitter.localScale = Vector3.one * (1.5f + 0.2f * Mathf.Sin(t * 14f));
    }

    // ------------------------------------------------------------------ the beam
    void StartBeam() {
        phase = Phase.Beaming;
        phaseTime = 0f;
        beamTop = hoverPoint + Vector3.down * 0.15f;
        beamLength = Mathf.Clamp(beamTop.y - bottomY, 2f, MaxLength);
        beam = AlienBeam.Create(transform, beamTop, beamLength, TopRadius, def.radius);
        beamHum = AlienSounds.Loop(AlienSounds.BeamHum, saucer, 0.6f);
        AlienSounds.Play(AlienSounds.BeamOn, beamTop, 0.9f);
        BattleCamera.Shake(0.15f);
    }

    void Beam(float dt) {
        phaseTime += dt;
        float strength = Mathf.Clamp01(phaseTime / Warmup);
        Pull(strength);
        tick += dt;
        if (tick >= TickEvery) { tick -= TickEvery; Burn(); }
        Abduct(dt);
        if (phaseTime >= BeamTime) StopBeam();
    }

    // Radius of the cone `depth` metres under its top (outside the beam: negative).
    float RadiusAt(float depth) {
        if (depth < -0.3f || depth > beamLength) return -1f;
        return Mathf.Lerp(TopRadius, def.radius, Mathf.Clamp01(depth / beamLength));
    }

    bool InBeam(Vector3 p) {
        float r = RadiusAt(beamTop.y - p.y);
        float dx = p.x - beamTop.x;
        return r > 0f && dx * dx + p.z * p.z <= r * r;
    }

    int Overlap(int mask) {
        return Physics.OverlapCapsuleNonAlloc(beamTop, beamTop + Vector3.down * beamLength, def.radius, buffer, mask, QueryTriggerInteraction.Ignore);
    }

    // Debris in the cone floats up towards the middle (and is swallowed at the saucer), the enemy blocks still
    // standing in it are made light, an exposed commander is lifted.
    void Pull(float strength) {
        int n = Overlap(Layers.BodiesMask);
        pulled.Clear();
        for (int i = 0; i < n; i++) {
            Collider col = buffer[i];
            Rigidbody b = col != null ? col.attachedRigidbody : null;
            if (b == null || b.isKinematic || !pulled.Add(b)) continue;
            Vector3 p = b.worldCenterOfMass;
            if (!InBeam(p) || IsOwnTeam(col)) continue;
            int layer = col.gameObject.layer;
            if (layer == Layers.Commander) LiftCommander(col.GetComponentInParent<Commander>(), b, p, strength);
            else if (layer == Layers.Debris && (p - beamTop).sqrMagnitude < Reach * Reach) AlienAbduction.Take(b.gameObject, saucer);
            else if (layer == Layers.Debris) Float(b, p, strength);
            else if (grabbed == null || b != grabbed.rb) b.AddForce(Vector3.up * (World.Gravity * BlockLift * strength), ForceMode.Acceleration);
        }
    }

    // Weightless plus DebrisLift, drawn towards the beam's axis, with a little drag so things drift rather than fly.
    void Float(Rigidbody b, Vector3 p, float strength) {
        Vector3 v = b.linearVelocity;
        Vector3 toAxis = new Vector3(beamTop.x - p.x, 0f, -p.z);
        Vector3 a = Vector3.up * (World.Gravity + DebrisLift) + toAxis * Centering - v * Drag;
        b.AddForce(a * strength, ForceMode.Acceleration);
    }

    // A commander is lifted only with nothing over his head (Explosion.Cover counts the blocks in between),
    // up to CommanderHang under the saucer, where he dangles until the beam lets go.
    void LiftCommander(Commander cm, Rigidbody b, Vector3 p, float strength) {
        if (cm == null || !cm.IsAlive || Explosion.Cover(beamTop, cm.AimPoint) < 0.99f) return;
        Vector3 v = b.linearVelocity;
        float ay = World.Gravity + Mathf.Clamp((beamTop.y - CommanderHang - p.y) * 5f, -8f, 9f) - v.y * 2.5f;
        float ax = (beamTop.x - p.x) * 3f - v.x * 2f;
        b.AddForce(new Vector3(ax, ay, 0f) * strength, ForceMode.Acceleration);
        if (lifted == cm) return;
        lifted = cm;
        VFX.FloatingText(cm.transform.position + Vector3.up * 2.6f, "!", Color.white, 1.3f);
        AlienSounds.Play(AlienSounds.Abduct, p, 0.7f, 0.7f);
    }

    // Damage tick on the enemy blocks in the cone (targets are copied out first: blocks may break on the way),
    // with a few green sparks crackling over them.
    void Burn() {
        int n = Overlap(Layers.BlockMask);
        targets.Clear();
        for (int i = 0; i < n; i++) {
            Block b = buffer[i] != null ? buffer[i].GetComponent<Block>() : null;
            if (b != null && b.IsAlive && b != grabbed && !IsOwnTeam(b) && InBeam(b.transform.position)) targets.Add(b);
        }
        for (int i = 0; i < targets.Count && i < 3; i++)
            VFX.Sparks(targets[Random.Range(0, targets.Count)].transform.position, AlienKit.Beam, 4);
        foreach (Block b in targets)
            if (b.IsAlive) Hit(b, def.damage * TickEvery, DamageType.Magic, b.transform.position, owner, Vector3.up * 2f);
    }

    // One block at a time: the highest enemy block that fits in the beam is pulled out and up to the saucer,
    // where it is taken (or beamed away on the spot if it got stuck on the way).
    void Abduct(float dt) {
        if (grabbed != null && !grabbed.IsAlive) grabbed = null;     // broke or fell apart on the way
        if (grabbed == null) {
            if (abductions >= MaxAbductions || phaseTime < nextGrab || phaseTime > BeamTime - 0.5f) return;
            grabbed = HighestBlock();
            if (grabbed != null) TearOut(grabbed);
            return;
        }
        grabTime += dt;
        Rigidbody b = grabbed.rb;
        Vector3 to = beamTop - b.worldCenterOfMass;
        float d = to.magnitude;
        if (d < Reach + grabbed.size.y * 0.5f || grabTime > GrabTimeout) { TakeBlock(grabbed); return; }
        Vector3 want = to / d * Mathf.Min(GrabSpeed, d * 2.5f);
        b.AddForce((want - b.linearVelocity) * 5f + Vector3.up * World.Gravity, ForceMode.Acceleration);
        b.AddTorque(new Vector3(0f, 0f, (travelDir * 1.5f - b.angularVelocity.z) * 3f), ForceMode.Acceleration);   // slow tumble
    }

    // The highest free block in the beam: one with another block lying on it would carry that one (a roof slab)
    // up too. Blocks too long for the beam are taken only when nothing else is free.
    Block HighestBlock() {
        Block best = null, wide = null;
        float bestTop = float.MinValue, wideTop = float.MinValue;
        int n = Overlap(Layers.BlockMask);
        for (int i = 0; i < n; i++) {
            Block b = buffer[i] != null ? buffer[i].GetComponent<Block>() : null;
            if (b == null || !b.IsAlive || b.rb == null || b.rb.isKinematic || IsOwnTeam(b)) continue;
            Vector3 c = b.rb.worldCenterOfMass;
            Bounds bounds = b.rend.bounds;
            if (!InBeam(c) || Covered(b, bounds)) continue;
            float top = bounds.max.y;
            if (bounds.size.x > 2f * RadiusAt(beamTop.y - c.y) + 0.6f) {   // too long for the beam
                if (top > wideTop) { wideTop = top; wide = b; }
                continue;
            }
            if (top > bestTop) { bestTop = top; best = b; }
        }
        return best != null ? best : wide;
    }

    static bool Covered(Block b, Bounds bounds) {
        Vector3 centre = new Vector3(bounds.center.x, bounds.max.y + 0.08f, 0f);
        Vector3 half = new Vector3(bounds.extents.x * 0.9f, 0.06f, 0.4f);
        int n = Physics.OverlapBoxNonAlloc(centre, half, coverBuffer, Quaternion.identity, Layers.BlockMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++) if (coverBuffer[i] != null && coverBuffer[i] != b.box) return true;
        return false;
    }

    static readonly Collider[] coverBuffer = new Collider[8];

    // The beam takes hold of it: a jolt, dust and sparks, and from the next physics step it rises.
    void TearOut(Block b) {
        b.rb.WakeUp();
        grabTime = 0f;
        Vector3 p = b.transform.position;
        VFX.Dust(p, Mathf.Min(b.size.x, b.size.y) * 0.6f, BlockMats.Get(b.material).color);
        VFX.Sparks(p, AlienKit.Beam, 8);
        SFX.PlayImpact(b.material, p, 0.6f);
    }

    void TakeBlock(Block b) {
        abductions++;
        grabbed = null;
        nextGrab = phaseTime + GrabEvery;
        Vector3 p = b.transform.position;
        AlienAbduction.Ghost(b, saucer);
        VFX.MagicBurst(p, AlienKit.Beam, 0.7f);
        VFX.LightFlash(p, AlienKit.Beam, 4f, 6f, 0.25f);
        AlienSounds.Play(AlienSounds.Abduct, p, 0.9f, 1f + abductions * 0.1f);
        BattleCamera.Shake(0.12f);
        b.Vanish();
    }

    // ------------------------------------------------------------------ leaving
    void StopBeam() {
        phase = Phase.Leaving;
        phaseTime = 0f;
        grabbed = null;                      // a block on its way up drops
        if (beam != null) beam.Close();
        SFX.Stop(beamHum);
        beamHum = null;
        cameraFollows = false;               // the camera stays on what the beam left behind
        leaveVelocity = new Vector3(0f, 3f, 0f);
        AlienSounds.Play(AlienSounds.Zoom, saucer.position, 0.9f);
    }

    // Zooms up and away, faster and faster, shrinking into the sky.
    void Leave(float dt) {
        phaseTime += dt;
        leaveVelocity += new Vector3(travelDir * 22f, 34f, 0f) * dt;
        transform.position += leaveVelocity * dt;
        saucer.localRotation = Quaternion.Slerp(saucer.localRotation, Quaternion.Euler(0f, 0f, -travelDir * 18f), dt * 5f);
        saucer.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, phaseTime / LeaveTime);
        if (phaseTime >= LeaveTime) Finish(true);
    }

    // Off the map in flight, or (safety net) taking too long over the target.
    protected override void OnLost() {
        if (phase == Phase.Flying || phase == Phase.Leaving) Finish(true);
        else StopBeam();
    }
}

// The look of a tractor beam: a translucent green cone with a brighter core and bands drifting upwards, motes
// rising into the saucer, a glowing ring where it meets the ground and a green light. It shoots down when
// created; Close narrows it to nothing and removes it.
public class AlienBeam : MonoBehaviour {
    const float OpenTime = 0.3f, CloseTime = 0.25f;
    const float FrontZ = -0.7f;              // drawn a little in front of the blocks it passes through
    const float MoteSpeed = 4f;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    static readonly Color OuterColor = new Color(0.3f, 1.1f, 0.55f, 0.5f), CoreColor = new Color(0.75f, 1.6f, 1f, 0.75f);
    static readonly Color RingColor = new Color(0.4f, 1.6f, 0.8f, 0.8f);
    static readonly Dictionary<long, Mesh> cones = new Dictionary<long, Mesh>();
    static Texture2D bands;

    Transform cone, ring;
    Renderer outer, core;
    ParticleSystem motes;
    Light lamp;
    MaterialPropertyBlock mpb;
    Vector3 ringSize;
    float length, reach, width = 1f, scroll;
    bool closing;

    // top: where the beam leaves the saucer; it ends `length` below, `bottomRadius` wide.
    public static AlienBeam Create(Transform parent, Vector3 top, float length, float topRadius, float bottomRadius) {
        GameObject go = new GameObject("Tractor Beam");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(top.x, top.y, FrontZ);
        AlienBeam beam = go.AddComponent<AlienBeam>();
        beam.Build(length, topRadius, bottomRadius);
        return beam;
    }

    public void Close() {
        if (closing) return;
        closing = true;
        transform.SetParent(null, true);     // stays where it is while its saucer leaves
        motes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void Build(float length, float topRadius, float bottomRadius) {
        this.length = length;
        mpb = new MaterialPropertyBlock();
        Material glow = Mats.ParticleAdditive(Bands);
        Mesh mesh = Cone(topRadius, bottomRadius);
        cone = ModelKit.Root("Cone", transform).transform;
        cone.localScale = new Vector3(1f, 0.001f, 1f);
        outer = ModelKit.MeshPart(cone, mesh, Vector3.zero, Vector3.one, glow, default, "Beam").GetComponent<Renderer>();
        core = ModelKit.MeshPart(cone, mesh, Vector3.zero, new Vector3(0.4f, 1f, 0.4f), glow, default, "Core").GetComponent<Renderer>();

        ringSize = new Vector3(bottomRadius * 2.6f, bottomRadius * 0.8f, 1f);
        GameObject r = ModelKit.MeshPart(transform, WorldMeshes.Quad(), new Vector3(0f, -length, -0.3f), Vector3.one * 0.001f,
                                         Mats.ParticleAdditive(TexKit.Ring), default, "Ring");
        ring = r.transform;
        mpb.SetColor(BaseColorId, RingColor);
        r.GetComponent<Renderer>().SetPropertyBlock(mpb);

        motes = Motes(length, bottomRadius);
        GameObject light = ModelKit.Root("Beam Light", transform);
        light.transform.localPosition = new Vector3(0f, -length * 0.55f, -1.2f);
        lamp = light.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = AlienKit.Beam;
        lamp.range = length + 4f;
        lamp.intensity = 0f;
        lamp.shadows = LightShadows.None;
        ModelKit.Shadows(gameObject, false, false);
    }

    void Update() {
        float dt = Time.deltaTime;
        if (closing) width = Mathf.MoveTowards(width, 0f, dt / CloseTime);
        else reach = Mathf.MoveTowards(reach, 1f, dt / OpenTime);
        cone.localScale = new Vector3(Mathf.Max(0.001f, width), Mathf.Max(0.001f, length * reach), Mathf.Max(0.001f, width));
        scroll += dt * 1.8f;                 // the bands drift up into the saucer
        float flicker = 0.88f + 0.12f * Mathf.Sin(Time.time * 31f) * Mathf.Sin(Time.time * 7.3f);
        mpb.Clear();
        mpb.SetVector(BaseMapStId, new Vector4(1f, Mathf.Max(1f, length / 1.3f), 0f, scroll));
        mpb.SetColor(BaseColorId, OuterColor * flicker);
        outer.SetPropertyBlock(mpb);
        mpb.SetColor(BaseColorId, CoreColor * flicker);
        core.SetPropertyBlock(mpb);
        float landed = reach >= 1f ? width : 0f;
        ring.localScale = ringSize * Mathf.Max(0.001f, landed * (1f + 0.12f * Mathf.Sin(Time.time * 9f)));
        lamp.intensity = 3.2f * reach * width * flicker;
        if (closing && width <= 0f) Destroy(gameObject);
    }

    // Motes born on a circle at the foot that rise along straight lines into the top of the beam (a radial
    // velocity towards a centre moved up to the apex), slowly circling the axis.
    ParticleSystem Motes(float length, float radius) {
        GameObject go = ModelKit.Root("Motes", transform);
        go.transform.localPosition = new Vector3(0f, -length, 0f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.playOnAwake = false;
        main.startLifetime = Mathf.Sqrt(length * length + radius * radius * 0.7f) / MoteSpeed * 0.92f;   // gone just short of the apex
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.17f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 1f, 0.7f), new Color(0.9f, 1f, 0.95f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 160;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = Mathf.Clamp(length * 6f, 15f, 70f);
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.85f;
        shape.rotation = new Vector3(90f, 0f, 0f);          // a horizontal circle
        ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.radial = -MoteSpeed;
        vel.orbitalY = 1.2f;
        vel.orbitalOffsetY = length;
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = FxAssets.Additive(FxAssets.Glow, 2f);
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sortingOrder = FxLayers.OrderGlow;
        ps.Play();
        return ps;
    }

    // An open cone along -Y (unit length; the beam scales it), seen from both sides. Vertex alpha is highest at
    // the silhouette edges and near the top, so the flat side view reads as a volume of light.
    static Mesh Cone(float rTop, float rBottom) {
        long key = ((long)Mathf.RoundToInt(rTop * 100f) << 20) | (long)Mathf.RoundToInt(rBottom * 100f);
        if (cones.TryGetValue(key, out Mesh cached) && cached != null) return cached;
        const int sides = 28, rings = 8;
        MeshBuilder b = new MeshBuilder();
        int[,] outside = new int[rings + 1, sides + 1], inside = new int[rings + 1, sides + 1];
        for (int k = 0; k <= rings; k++) {
            float t = k / (float)rings;                                   // 0 at the saucer, 1 at the foot
            float r = Mathf.Lerp(rTop, rBottom, t);
            float fade = Mathf.Lerp(1f, 0.35f, t) * Mathf.Clamp01((1f - t) / 0.15f);
            for (int s = 0; s <= sides; s++) {
                float a = s * Mathf.PI * 2f / sides, cos = Mathf.Cos(a);
                float edge = 0.18f + 0.82f * Mathf.Pow(Mathf.Abs(cos), 3f);
                Color c = new Color(1f, 1f, 1f, edge * fade);
                Vector3 p = new Vector3(cos * r, -t, Mathf.Sin(a) * r);
                Vector2 uv = new Vector2(s / (float)sides, t);
                outside[k, s] = b.Vertex(p, uv, c);
                inside[k, s] = b.Vertex(p, uv, c);
            }
        }
        for (int k = 0; k < rings; k++)
            for (int s = 0; s < sides; s++) {
                // seen from outside, a growing angle runs to the right and ring k is above ring k + 1
                b.Quad(outside[k + 1, s], outside[k, s], outside[k, s + 1], outside[k + 1, s + 1]);
                b.Quad(inside[k + 1, s + 1], inside[k, s + 1], inside[k, s], inside[k + 1, s]);
            }
        Mesh m = b.ToMesh("Tractor beam");
        cones[key] = m;
        return m;
    }

    // Soft horizontal bands (alpha) that repeat along the beam.
    static Texture2D Bands {
        get {
            if (bands != null) return bands;
            const int H = 64;
            bands = new Texture2D(4, H, TextureFormat.RGBA32, false) { name = "Beam bands", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            Color[] px = new Color[4 * H];
            for (int y = 0; y < H; y++) {
                float s = 0.5f + 0.5f * Mathf.Cos(y / (float)H * Mathf.PI * 2f);
                for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color(1f, 1f, 1f, 0.5f + 0.5f * s * s * s);
            }
            bands.SetPixels(px);
            bands.Apply(false);
            return bands;
        }
    }
}

// Something taken up by a saucer: it flies into the saucer's belly, spinning and shrinking, and is gone.
// A block leaves a look-alike copy behind for this (the block itself vanishes at once); debris goes itself.
public class AlienAbduction : MonoBehaviour {
    const float Duration = 0.45f;
    Transform target;
    Vector3 start, end, startScale;
    float t;

    public static void Ghost(Block b, Transform target) {
        MeshFilter mf = b.GetComponent<MeshFilter>();
        if (mf == null || b.rend == null) return;
        GameObject go = new GameObject("Abducted " + b.def.name);
        go.transform.SetPositionAndRotation(b.transform.position, b.transform.rotation);
        go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = b.rend.sharedMaterial;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        go.AddComponent<AlienAbduction>().Init(target);
    }

    public static void Take(GameObject go, Transform target) {
        Debris debris = go.GetComponent<Debris>();
        if (debris != null) Destroy(debris);                     // its own shrink-and-die would fight this
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        Rigidbody body = go.GetComponent<Rigidbody>();
        if (body != null) {
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;   // kinematic bodies cannot use continuous modes
            body.isKinematic = true;
        }
        go.AddComponent<AlienAbduction>().Init(target);
    }

    void Init(Transform target) {
        this.target = target;
        start = transform.position;
        startScale = transform.localScale;
        end = target != null ? target.position : start + Vector3.up;
    }

    void Update() {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / Duration);
        if (target != null) end = target.position + Vector3.down * 0.1f;
        transform.position = Vector3.Lerp(start, end, k * k);
        transform.localScale = startScale * Mathf.Max(0.001f, 1f - k * k);
        transform.Rotate(0f, 0f, 420f * Time.deltaTime, Space.World);
        if (k >= 1f) Destroy(gameObject);
    }
}

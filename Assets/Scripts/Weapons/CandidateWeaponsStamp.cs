using System.Collections;
using UnityEngine;

// Ballot Stamp: the Candidate throws a small rubber voting stamp (CandidateStampToss) along his aim. It tumbles end over
// end and does no damage itself: on its first solid contact with the other side (a block, a commander, the ground) it
// turns its face onto the spot with a splat of ink and stays there as the mark. High over that spot a giant copy of it
// (CandidateStampGiant) appears, hangs a moment and slams straight down, cutting through what stands in its way. Where it
// stops it bursts into an ink blast and prints his mark (a tick in a ring, CandidateStampImprint) on the fortress, then
// lifts off and shrinks away. His votes (CandidateVotes, read once at Fire) make the giant stamp bigger, its cut deeper,
// its blast wider and harder, and throw more ink and confetti about; from GlowTier on its ink glows.
public static class CandidateStamp {
    // The stamp's proportions, as shares of its height (face to knob top).
    public const float BaseRadius = 0.445f, FaceRadius = 0.4f;
    public static readonly Color InkDrop = new Color(0.42f, 0.2f, 0.78f, 0.95f);    // CandidateKit.Ink as ink drops
    public static readonly Color InkMist = new Color(0.34f, 0.18f, 0.58f, 0.5f);
    public static readonly Color InkPrint = new Color(0.42f, 0.2f, 0.78f, 0.85f);
    static readonly Color Wood = new Color(0.55f, 0.36f, 0.2f), WoodLight = new Color(0.68f, 0.47f, 0.28f);
    static Mesh mark;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        CandidateStampToss.Throw(owner, def, origin, velocity, CandidateVotes.Tier(owner), CandidateVotes.Damage(owner),
                                 CandidateVotes.Radius(owner));
    }

    // Weapon icon: the small stamp leaning over towards +X (forward), so its inked face shows under it.
    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Ballot Stamp", parent);
        Build(root.transform, 0.5f).localRotation = Quaternion.Euler(0f, 0f, -60f);
        return root;
    }

    // A rubber stamp `height` tall: a round wooden knob on a short neck, a round teal base with a gold band and the violet
    // inked rubber underneath, which carries his mark. It stands along +Y with its face at local y = 0. glow: the ink glows.
    public static Transform Build(Transform parent, float height, bool glow = false) {
        Transform t = ModelKit.Root("Stamp", parent).transform;
        float h = height;
        ModelKit.Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.03f * h, 0f), new Vector3(0.8f, 0.03f, 0.8f) * h,
                      glow ? CandidateKit.InkGlowMat : CandidateKit.InkMat);
        ModelKit.MeshPart(t, Mark, new Vector3(0f, -0.004f * h, 0f), Vector3.one * (0.3f * h), CandidateKit.PaperMat,
                          new Vector3(-90f, 0f, 0f), "Mark");   // on the rubber, facing down
        ModelKit.Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.14f * h, 0f), new Vector3(0.89f, 0.08f, 0.89f) * h, CandidateKit.TealMat);
        ModelKit.Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.2025f * h, 0f), new Vector3(0.904f, 0.0125f, 0.904f) * h, CandidateKit.GoldMat);
        ModelKit.Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.37f * h, 0f), new Vector3(0.2f, 0.15f, 0.2f) * h, Mats.Lit(Wood, 0.35f));
        ModelKit.Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.525f * h, 0f), new Vector3(0.3f, 0.025f, 0.3f) * h, CandidateKit.NavyMat);
        ModelKit.Part(t, PrimitiveType.Sphere, new Vector3(0f, 0.74f * h, 0f), new Vector3(0.5f, 0.46f, 0.5f) * h, Mats.Lit(WoodLight, 0.45f));
        return t;
    }

    // His mark, a tick in a ring, as one flat mesh in the XY plane facing -Z (the camera), outer radius 1. The tick's two
    // strokes meet in a mitred corner, so nothing overlaps (a see-through print has no darker spots).
    public static Mesh Mark {
        get {
            if (mark != null) return mark;
            const int Sides = 40;
            const float Inner = 0.8f, Half = 0.1f;   // the ring's inner radius; half the tick's stroke width
            MeshBuilder b = new MeshBuilder();
            for (int i = 0; i < Sides; i++) {
                float a0 = i * Mathf.PI * 2f / Sides, a1 = (i + 1) * Mathf.PI * 2f / Sides;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f), d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                b.Quad(V(b, d0 * Inner), V(b, d1 * Inner), V(b, d1), V(b, d0));
            }
            Vector3 a = new Vector3(-0.44f, 0.04f, 0f), k = new Vector3(-0.12f, -0.34f, 0f), c = new Vector3(0.48f, 0.4f, 0f);
            Vector3 n1 = Side(k - a), n2 = Side(c - k), m = (n1 + n2).normalized;
            m *= Half / Vector3.Dot(m, n1);
            b.Quad(V(b, a - n1 * Half), V(b, a + n1 * Half), V(b, k + m), V(b, k - m));
            b.Quad(V(b, k - m), V(b, k + m), V(b, c + n2 * Half), V(b, c - n2 * Half));
            return mark = b.ToMesh("Candidate mark");
        }
    }

    static int V(MeshBuilder b, Vector3 p) { return b.Vertex(p, new Vector2(p.x * 0.5f + 0.5f, p.y * 0.5f + 0.5f)); }

    // The left-hand side of a stroke running along d (seen from the camera).
    static Vector3 Side(Vector3 d) { d.Normalize(); return new Vector3(-d.y, d.x, 0f); }

    // A few drops of ink flung up off a hit.
    public static void Spatter(Vector3 at, int count, float speed) {
        Vector3 c = CandidateKit.Front(at);
        for (int i = 0; i < count; i++)
            FxLayers.Droplet.Emit(c, new Vector3(Random.Range(-1f, 1f), Random.Range(0.3f, 1f), Random.Range(-0.3f, 0f)) * speed,
                                  Random.Range(0.07f, 0.14f), Random.Range(0.4f, 0.7f), InkDrop);
    }

    // The giant stamp's ink blast: a violet flash and shock ring, ink squirted out low to both sides, a mist of ink and,
    // the more votes he has, the more confetti.
    public static void InkBlast(Vector3 at, float blast, int tier) {
        Vector3 c = CandidateKit.Front(at);
        FxLayers.Flash.Emit(c, Vector3.zero, blast * 1.3f, 0.14f, new Color(0.88f, 0.78f, 1f));
        FxLayers.Shock.Emit(c, Vector3.zero, blast * 2.4f, 0.38f, new Color(0.62f, 0.4f, 1f, 0.7f));
        int drops = 16 + 5 * tier;
        for (int i = 0; i < drops; i++) {
            float side = i % 2 == 0 ? 1f : -1f;
            Vector3 v = new Vector3(side * Random.Range(2f, 7f), Random.Range(1.5f, 6f), Random.Range(-1.2f, 0f)) * (0.6f + 0.15f * blast);
            FxLayers.Droplet.Emit(c + new Vector3(side * Random.Range(0.2f, 0.5f) * blast, Random.Range(0f, 0.3f), 0f), v,
                                  Random.Range(0.1f, 0.22f), Random.Range(0.5f, 0.9f), InkDrop);
        }
        for (int i = 0; i < 10; i++) {
            float side = i % 2 == 0 ? 1f : -1f;
            Vector3 v = new Vector3(side * Random.Range(0.5f, 1.5f) * blast, Random.Range(0.3f, 1.5f), Random.Range(-0.5f, 0f));
            FxLayers.Dust.Emit(c + new Vector3(side * Random.Range(0f, 0.5f) * blast, Random.Range(-0.2f, 0.4f), 0f), v,
                               Random.Range(0.35f, 0.6f) * blast, Random.Range(0.7f, 1.2f), InkMist, Random.Range(-40f, 40f));
        }
        for (int i = 0; i < 6 * tier; i++)
            FxLayers.Star.Emit(c + Vector3.up * 0.5f, new Vector3(Random.Range(-5f, 5f), Random.Range(3f, 9f), Random.Range(-1.5f, 0f)),
                               Random.Range(0.18f, 0.32f), Random.Range(1f, 1.6f), i % 2 == 0 ? CandidateKit.Gold : CandidateKit.Teal,
                               Random.Range(-90f, 90f));
        VFX.LightFlash(c, new Color(0.7f, 0.45f, 1f), 5f + 2f * blast, blast * 4f + 3f, 0.4f);
    }
}

// The thrown stamp: a small rubber stamp that tumbles end over end along the throw and does no damage. On its first
// contact with the other side (its own side's blocks and commanders never touch it) it stops dead, turns its face onto
// what it hit with a splat of ink and squashes onto it. It holds the turn until the giant stamp is on its way, then stays
// as the mark until the giant comes down on it (or stops short of it) and is gone. Into the sea it only splashes.
public class CandidateStampToss : Projectile {
    const float Height = 0.4f;                      // the model, face to knob
    const float BodyRadius = 0.17f, Mass = 0.15f;
    const float TumbleSpeed = 720f;                 // deg/s, end over end
    const float SettleTime = 0.08f;                 // landed: it turns onto its face...
    const float SquashTime = 0.18f, Squash = 0.25f; // ...and squashes onto it
    const float DropDelay = 0.3f;                   // landed -> the giant stamp appears
    const float MarkTimeout = 3f, FadeTime = 0.25f; // safety net: the giant never came down on it; then it shrinks away
    static readonly Color Glint = new Color(0.75f, 0.55f, 1f, 0.6f);

    int tier;
    float damageBoost, reach, spin;
    bool landed;
    Transform flip, model;
    TurnActor hold;

    // tier, damageBoost, reach: his votes (CandidateVotes.Tier, Damage, Radius), read at Fire.
    public static void Throw(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity, int tier, float damageBoost, float reach) {
        WeaponFactory.Spawn<CandidateStampToss>("Ballot stamp", owner, def, origin, velocity, s => {
            s.tier = tier;
            s.damageBoost = damageBoost;
            s.reach = reach;
        });
    }

    protected override void OnLaunch(Vector3 velocity) {
        hold = GetComponent<TurnActor>();
        flip = ModelKit.Root("Tumble", transform).transform;
        model = CandidateStamp.Build(flip, Height);
        model.localPosition = Vector3.down * (Height * 0.5f);   // it tumbles about its middle
        gameObject.AddComponent<SphereCollider>().radius = BodyRadius;
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;   // only the model tumbles
        VFX.Trail(transform, Glint, 0.14f, 0.2f);
    }

    protected override void FixedUpdate() {
        if (!finished && hold == null) { Finish(true); return; }   // the turn went on without it (a time rewind, the turn timeout)
        base.FixedUpdate();
        if (!finished && !landed && transform.position.y < World.WaterY) Sink();
    }

    void Update() {
        if (finished || landed) return;
        spin = Mathf.Repeat(spin - TumbleSpeed * travelDir * Time.deltaTime, 360f);   // forwards, whichever way it flies
        flip.localRotation = Quaternion.Euler(0f, 0f, spin);
    }

    void OnCollisionEnter(Collision c) {
        if (finished || landed || IsOwnTeam(c.collider)) return;
        bool touched = c.contactCount > 0;
        Land(touched ? c.GetContact(0).point : transform.position, touched ? c.GetContact(0).normal : Vector3.up);
    }

    // It stops dead where it touched, out of physics from now on, its face on the surface.
    void Land(Vector3 point, Vector3 normal) {
        if (point.y < World.WaterY) { Sink(); return; }   // it struck the island's foot under the waterline
        landed = true;
        LichKit.Freeze(this);
        point.z = 0f;
        normal.z = 0f;
        if (Vector3.Dot(normal, transform.position - point) < 0f) normal = -normal;   // out of what it hit
        normal = normal.sqrMagnitude > 1e-4f ? normal.normalized : Vector3.up;
        transform.position = point + normal * (Height * 0.5f);
        maxLifetime = age + 6f;   // safety net: the giant is on its way well within a second
        RaiseImpact(point);
        CandidateStamp.Spatter(point, 9, 3.5f);
        FxLayers.Flash.Emit(CandidateKit.Front(point), Vector3.zero, 0.9f, 0.1f, new Color(0.88f, 0.78f, 1f));
        VFX.Ring(point, 1.1f, new Color(0.62f, 0.4f, 1f, 0.8f), 0.4f, 0.16f);
        CandidateSounds.Play(CandidateSounds.StampThump, point, 0.45f, 1.8f);   // a small thunk
        CandidateStampGiant.FrameDrop(point);
        StartCoroutine(MarkSpot(point, Mathf.Atan2(-normal.x, normal.y) * Mathf.Rad2Deg));
    }

    // The tumble runs out on its face (angle: the turn that puts its face against the surface), it squashes onto it and
    // springs back; then the giant stamp comes for the spot. The little one stays until the giant comes down on it.
    IEnumerator MarkSpot(Vector3 spot, float angle) {
        float from = spin, to = spin + Mathf.DeltaAngle(spin, angle);
        for (float t = 0f; t < DropDelay; t += Time.deltaTime) {
            float k = Mathf.Clamp01(t / SettleTime);
            flip.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(from, to, 1f - (1f - k) * (1f - k)));
            float s = t < SquashTime ? Squash * Mathf.Sin(t / SquashTime * Mathf.PI) : 0f;
            model.localScale = new Vector3(1f + s * 0.6f, 1f - s, 1f + s * 0.6f);
            yield return null;
        }
        flip.localRotation = Quaternion.Euler(0f, 0f, to);
        model.localScale = Vector3.one;
        if (finished) yield break;

        CandidateStampGiant giant = CandidateStampGiant.Drop(owner, def, spot, tier, damageBoost, reach);
        Finish(false);   // the giant stamp holds the turn from here
        float top = transform.position.y + Height * 0.5f;
        for (float t = 0f; t < MarkTimeout && giant != null && !giant.finished && giant.FaceY > top; t += Time.deltaTime)
            yield return null;
        if (giant != null && giant.FaceY <= top) {   // crushed under it
            CandidateStamp.Spatter(transform.position, 5, 3f);
            Destroy(gameObject);
            yield break;
        }
        for (float t = 0f; t < FadeTime; t += Time.deltaTime) {   // the giant stopped short of it, or never came
            model.localScale = Vector3.one * Mathf.Max(0.001f, 1f - t / FadeTime);
            yield return null;
        }
        Destroy(gameObject);
    }

    // Into the sea before it touched anything: a little splash and the turn ends. No giant stamp.
    void Sink() {
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);
        if (!impacted) RaiseImpact(p);
        VFX.Splash(p, 0.5f);
        SFX.Play(SoundId.Splash, p, 0.4f);
        Finish(true);
    }
}

// The giant stamp: a copy of the little one BaseHeight tall (GrowPerTier taller per vote tier) that grows in high over
// the mark, hangs a moment and slams straight down at a steady speed (no gravity, no wind), cutting through what stands
// in its way (PiercingProjectile: every block eats part of its budget and slows it down). Where it stops (the ground,
// its budget spent, or a commander) it bursts into an ink blast and prints his mark on the fortress
// (CandidateStampImprint), stays pressed down a moment, lifts off and shrinks away. In the sea it only splashes.
public class CandidateStampGiant : PiercingProjectile {
    const float BaseHeight = 3f, GrowPerTier = 0.08f;
    const float DropHeight = 20f, Speed = 40f;      // its face starts this high over the mark
    const float GrowTime = 0.15f, Hover = 0.45f;    // it grows in and hangs until Hover before it falls
    const float SweepShare = 0.7f;                  // the sweep radius: share of its base radius
    const float CommanderHit = 35f, BlastDamage = 30f;
    const float Press = 0.12f;                      // how deep it stamps into a block it stops in
    const float SquashTime = 0.14f, Squash = 0.12f;
    const float HoldTime = 0.35f, LiftTime = 0.4f, LiftHeight = 1f, ShrinkTime = 0.35f;
    const float ImprintLife = 4f;
    const float FrameWidth = 16f;
    const int GlowTier = 3;                         // from this vote tier on its ink glows
    static readonly Color TrailTint = new Color(0.72f, 0.55f, 1f, 0.4f);

    int tier;
    float height, faceOffset, damageBoost, reach, columnX, stuckFor;
    bool falling, slammed;
    Transform pivot, model;
    GameObject trail;
    TurnActor hold;
    Vector3 rest;

    // The height of its inked face (the projectile is the centre of its sweep sphere, the face is at its bottom).
    public float FaceY => transform.position.y - faceOffset;

    public static CandidateStampGiant Drop(Fortress owner, WeaponDef def, Vector3 spot, int tier, float damageBoost, float reach) {
        float h = BaseHeight * (1f + GrowPerTier * tier);
        float sweep = CandidateStamp.BaseRadius * h * SweepShare;
        Vector3 at = new Vector3(spot.x, spot.y + DropHeight + sweep, 0f);
        return WeaponFactory.Spawn<CandidateStampGiant>("Giant stamp", owner, def, at, Vector3.zero, g => {
            g.tier = tier;
            g.height = h;
            g.faceOffset = sweep;
            g.columnX = spot.x;
            g.damageBoost = damageBoost;
            g.reach = reach;
            g.sweepRadius = sweep;
            g.budget = def.pierce * reach;
            g.damage = def.damage * damageBoost;
            g.commanderDamage = CommanderHit * damageBoost;
            g.embedDepth = Press;
        });
    }

    // The camera takes in the sky the giant stamp will come from and the spot it will hit.
    public static void FrameDrop(Vector3 spot) {
        if (BattleCamera.I == null) return;
        BattleCamera.I.Frame(new Bounds(spot + Vector3.up * (DropHeight * 0.5f + 2f), new Vector3(FrameWidth, DropHeight + 8f, 0f)), 2f);
    }

    protected override void OnLaunch(Vector3 velocity) {
        base.OnLaunch(velocity);
        hold = GetComponent<TurnActor>();
        gravityScale = 0f;              // it keeps its own steady speed: it hangs, then falls at Speed
        windFactor = 0f;
        maxLifetime = Hover + 4f;       // safety net: the fall takes well under a second
        pivot = ModelKit.Root("Giant stamp", transform).transform;
        model = CandidateStamp.Build(pivot, height, tier >= GlowTier);
        model.localPosition = Vector3.down * faceOffset;
        pivot.localScale = Vector3.one * 0.001f;
        trail = VFX.Trail(transform, TrailTint, CandidateStamp.BaseRadius * height * 1.2f, 0.16f);
        Stopped += OnStopped;
        BlockPierced += OnPierced;
        VFX.MagicBurst(transform.position + Vector3.up * (height * 0.48f - faceOffset), CandidateKit.Ink, 1.6f);
    }

    protected override void FixedUpdate() {
        if (!finished && hold == null) { Finish(true); return; }   // the turn went on without it (a time rewind, the turn timeout)
        if (!falling && !finished && age >= Hover) {
            falling = true;
            manualVelocity = Vector3.down * Speed;
            CandidateSounds.Play(CandidateSounds.StampWhoosh, transform.position, 0.9f);
        }
        base.FixedUpdate();
        if (!finished && FaceY < World.WaterY) Splash();
    }

    void Update() {
        if (pivot == null) return;
        pivot.rotation = Quaternion.identity;   // upright, however the flight turns the projectile
        if (!slammed) {
            float k = Mathf.Clamp01(age / GrowTime) - 1f;
            pivot.localScale = Vector3.one * Mathf.Max(0.001f, 1f + 2.7f * k * k * k + 1.7f * k * k);   // grows in, overshooting a little
            return;
        }
        stuckFor += Time.deltaTime;
        float s = stuckFor < SquashTime ? Squash * Mathf.Sin(stuckFor / SquashTime * Mathf.PI) : 0f;   // the slam squashes it
        model.localScale = new Vector3(1f + s * 0.6f, 1f - s, 1f + s * 0.6f);
        float lift = Mathf.Clamp01((stuckFor - HoldTime) / LiftTime);
        float shrink = Mathf.Clamp01((stuckFor - HoldTime - LiftTime) / ShrinkTime);
        transform.position = rest + Vector3.up * (LiftHeight * (1f - (1f - lift) * (1f - lift)));
        pivot.localScale = Vector3.one * Mathf.Max(0.001f, 1f - Mathf.SmoothStep(0f, 1f, shrink));
        if (shrink >= 1f) Destroy(gameObject);
    }

    // Crashing through a block: ink spatters off it and the camera feels it (the block throws its own chips).
    void OnPierced(PiercingProjectile self, Block block, Vector3 point) {
        CandidateStamp.Spatter(point, 5, 4f);
        BattleCamera.Shake(0.08f);
    }

    // Stopped (right after this the projectile finishes, which ends its part of the turn): the ink blast, his mark, the thump.
    void OnStopped(PiercingProjectile self, Vector3 point, Collider hit) {
        // It stays where it stopped on its own: a block or commander breaking under it must neither take it along nor drop
        // it as debris (PiercingProjectile leaves alone a projectile that has a body of its own).
        transform.SetParent(null, true);
        Embedded holder = GetComponent<Embedded>();
        if (holder != null) Destroy(holder);
        gameObject.AddComponent<Rigidbody>().isKinematic = true;
        TrailRenderer streak = trail != null ? trail.GetComponent<TrailRenderer>() : null;
        if (streak != null) streak.emitting = false;
        if (point.y < World.WaterY) { Splash(); return; }

        slammed = true;
        rest = new Vector3(columnX, point.y + faceOffset, 0f);   // straight under where it fell, its face on what stopped it
        transform.position = rest;
        Vector3 at = new Vector3(columnX, point.y, 0f);
        float blast = def.radius * reach;
        Explosion.Create(at, blast, BlastDamage * damageBoost, def.force, owner, DamageType.Explosion, effects: false);
        CandidateStamp.InkBlast(at, blast, tier);
        CandidateStampImprint.Print(at, CandidateStamp.FaceRadius * height, ImprintLife);
        CandidateSounds.Play(CandidateSounds.StampThump, at, 1f);
        BattleCamera.Shake(0.45f);
    }

    // Nothing stood under it all the way down to the sea.
    void Splash() {
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);
        VFX.Splash(p, 2.5f);
        SFX.Play(SoundId.Splash, p, 1f);
        Finish(true);
    }
}

// His mark (a tick in a ring) printed in violet ink where the giant stamp came down: flat, just in front of the blocks and
// facing the camera, turned a little as a hand stamp would be. It slams in a little big, stays a few seconds and fades.
public class CandidateStampImprint : MonoBehaviour {
    const float PopTime = 0.12f, Pop = 0.18f, FadeTime = 0.8f;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    Renderer ink;
    MaterialPropertyBlock mpb;
    float t, life, radius;

    // radius: the ring's outer radius (m); life: seconds until it has faded away.
    public static void Print(Vector3 at, float radius, float life) {
        GameObject go = new GameObject("Ballot mark");
        go.transform.SetPositionAndRotation(CandidateKit.Front(at) + Vector3.back * 0.02f, Quaternion.Euler(0f, 0f, Random.Range(-12f, 12f)));
        CandidateStampImprint p = go.AddComponent<CandidateStampImprint>();
        p.ink = ModelKit.MeshPart(go.transform, CandidateStamp.Mark, Vector3.zero, Vector3.one, Mats.UnlitTransparent(Color.white),
                                  default, "Ink").GetComponent<Renderer>();
        ModelKit.Shadows(go, false, false);
        p.mpb = new MaterialPropertyBlock();
        p.radius = radius;
        p.life = Mathf.Max(life, PopTime + FadeTime);
        p.Update();
    }

    void Update() {
        t += Time.deltaTime;
        float k = 1f - Mathf.Clamp01(t / PopTime);
        transform.localScale = Vector3.one * (radius * (1f + Pop * k * k));
        Color c = CandidateStamp.InkPrint;
        c.a *= Mathf.Clamp01((life - t) / FadeTime);
        mpb.SetColor(BaseColorId, c);
        ink.SetPropertyBlock(mpb);
        if (t >= life) Destroy(gameObject);
    }
}

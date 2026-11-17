using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// Rally Shout (self-cast; the aim means nothing). A megaphone pops up at the Candidate's launch point and he shouts at the
// nearest enemy fortress (nearest in x): a sound wave, a fan of concentric orange and teal arcs that grows taller as it
// rolls, crosses to that fortress's front face (CandidateShoutWave) and shoves it there (Explosion.Push: force, never
// damage; blocks knocked into each other may still crack). Then it bounces, rolls back to him as an echo and closes round
// his commander as a shield (CandidateShield). With nobody to shout at it goes out 25 m and comes back. His votes
// (CandidateVotes) widen and harden the shove and make the megaphone, the wave and the shield bigger and brighter; from
// tier 3 the wave has a fourth arc and the shield lasts a turn longer, at tier 5 it takes two enemy turns of hits.
public static class CandidateShout {
    internal static readonly Color Orange = WeaponDefs.Hex("FF9F43");   // the weapon colour: the wave's other colour
    internal static readonly Color OrangeHdr = new Color(3.4f, 1.8f, 0.5f);
    internal const float MouthX = 0.262f;                              // the megaphone's mouth on its axis (model units)

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        WeaponFactory.Spawn<CandidateShoutWave>("Rally Shout", owner, def, origin, Vector3.zero);
    }

    // The weapon-bar icon: the megaphone with three sound arcs coming out of it.
    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Rally Shout", parent);
        Megaphone(root.transform);
        Material orange = Mats.Emissive(Orange, OrangeHdr);
        for (int i = 0; i < 3; i++) IconArc(root.transform, 0.24f + 0.17f * i, i == 1 ? CandidateKit.TealGlowMat : orange);
        return root;
    }

    // A megaphone along +X, about 0.8 m long: navy mouthpiece and grip, a teal bell, a gold rim round its dark mouth.
    internal static void Megaphone(Transform parent) {
        Vector3 along = new Vector3(0f, 0f, -90f);   // the frustums are built along +Y
        ModelKit.MeshPart(parent, WorldMeshes.Frustum(10, 0.055f, 0.085f, 0.14f), new Vector3(-0.52f, 0f, 0f), Vector3.one, CandidateKit.NavyMat, along, "Mouthpiece");
        ModelKit.MeshPart(parent, WorldMeshes.Frustum(18, 0.085f, 0.33f, 0.6f), new Vector3(-0.38f, 0f, 0f), Vector3.one, CandidateKit.TealMat, along, "Bell");
        ModelKit.MeshPart(parent, WorldMeshes.Frustum(18, 0.35f, 0.35f, 0.06f), new Vector3(0.19f, 0f, 0f), Vector3.one, CandidateKit.GoldMat, along, "Rim");
        ModelKit.MeshPart(parent, WorldMeshes.Frustum(18, 0.29f, 0.29f, 0.012f), new Vector3(0.25f, 0f, 0f), Vector3.one, CandidateKit.NavyMat, along, "Mouth");
        ModelKit.MeshPart(parent, MeshKit.Box(new Vector3(0.08f, 0.26f, 0.09f)), new Vector3(-0.24f, -0.2f, 0f), Vector3.one, CandidateKit.NavyMat, new Vector3(0f, 0f, 12f), "Grip");
    }

    // One sound arc of the icon round the mouth, bulging forward: a chain of short glowing bars.
    static void IconArc(Transform parent, float radius, Material m) {
        const int Bars = 8;
        const float Half = 46f;   // degrees either side of the axis
        Mesh bar = MeshKit.Box(new Vector3(0.055f, 2f * Half * Mathf.Deg2Rad * radius / Bars * 1.15f, 0.07f));   // overlapping a little
        for (int i = 0; i < Bars; i++) {
            float a = Mathf.Lerp(-Half, Half, (i + 0.5f) / Bars);
            Vector3 p = new Vector3(MouthX + radius * Mathf.Cos(a * Mathf.Deg2Rad), radius * Mathf.Sin(a * Mathf.Deg2Rad), 0f);
            ModelKit.MeshPart(parent, bar, p, Vector3.one, m, new Vector3(0f, 0f, a), "Arc");
        }
    }

    // 0 -> 1 with a little overshoot (things popping up).
    internal static float EaseOutBack(float x) {
        const float C1 = 1.70158f, C3 = C1 + 1f;
        float u = x - 1f;
        return 1f + C3 * u * u * u + C1 * u * u;
    }
}

// The shout's sound wave, animated by hand (no physics, no colliders). Its transform is the wave front, so the camera
// rides along: from the megaphone's mouth to the enemy's face, a shove there, a short bounce, then back to its owner's
// commander, where it becomes his shield. The arcs are LineRenderers drawn just in front of the blocks.
public class CandidateShoutWave : Projectile {
    const float Speed = 22f, EchoSpeed = 25f;     // m/s out and back...
    const float MaxLeg = 2.4f;                    // ...faster when a leg would take longer than this (s)
    const float NoTargetReach = 25f;              // nobody to shout at: out this far and back (m)
    const float ShoutAt = 0.22f;                  // the megaphone pops up first (s)
    const float BounceTime = 0.2f;
    const float MegaphoneSize = 1.35f;
    const float StartSize = 0.55f, EndSize = 2.4f, EchoSize = 1.4f;   // the leading arc's radius (m): at the mouth, the face, home
    const float HalfAngle = 52f * Mathf.Deg2Rad;  // each arc spans this either side of the travel direction
    const float Gap = 0.22f;                      // arc spacing, as a share of the leading arc's radius
    const int Segments = 16;
    const float Inset = 1f, Drop = 0.6f, Upward = 0.3f;   // the shove's centre: in front of the face, a little below the wave
    const float ArriveAt = 1.1f;                  // the echo closes into the shield this close to the commander (m)
    const float WaveZ = VFX.FrontZ - 0.2f;        // drawn in front of the blocks
    static readonly RaycastHit[] rayHits = new RaycastHit[64];

    enum Phase { Shout, Out, Bounce, Back }

    Commander commander;
    Fortress target;                              // null: nobody to shout at
    int tier;
    float scale, pushRadius, pushForce;
    Vector3 hitPoint, pushCentre, dir;
    Phase phase;
    float t, phaseStart, legLength, speed, size, legStartSize, progress, sigma = 1f, trailDue;
    LineRenderer[] arcs;
    Color[] arcColors;
    Vector3[] points;
    Gradient gradient;
    GradientColorKey[] colorKeys;
    GradientAlphaKey[] alphaKeys;

    public override bool CanActivate => false;

    protected override void OnLaunch(Vector3 velocity) {
        commander = owner != null ? owner.commander : null;
        if (commander == null || !commander.IsAlive) { Finish(true); return; }
        tier = CandidateVotes.Tier(owner);
        scale = CandidateVotes.Radius(owner);
        pushForce = def.force * CandidateVotes.Damage(owner);

        Vector3 launch = transform.position;
        launch.z = 0f;
        target = NearestEnemy(owner, launch.x);
        float dx = target != null ? Mathf.Sign(target.transform.position.x - launch.x) : (owner.Facing >= 0 ? 1f : -1f);
        hitPoint = target != null ? FaceOf(target, launch, dx) : launch + Vector3.right * (dx * NoTargetReach);
        hitPoint.x = Mathf.Clamp(hitPoint.x, 3f - World.ArenaHalfWidth, World.ArenaHalfWidth - 3f);
        dir = hitPoint - launch;
        dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.right * dx;
        pushCentre = new Vector3(hitPoint.x - dx * Inset * scale, hitPoint.y - Drop, 0f);
        pushRadius = Mathf.Min(def.radius * scale, ClearOfOwnSide(pushCentre));

        float k = MegaphoneSize * scale;
        CandidateShoutMegaphone.Create(launch, dir, k, ShoutAt);
        transform.position = launch + dir * (CandidateShout.MouthX * k);
        legLength = Mathf.Max(0.1f, Vector3.Distance(transform.position, hitPoint));
        speed = Mathf.Max(Speed, legLength / MaxLeg);
        maxLifetime = ShoutAt + legLength / speed + BounceTime + MaxLeg + 2.5f;
        legStartSize = size = StartSize * scale;
        BuildArcs();
    }

    static Fortress NearestEnemy(Fortress owner, float x) {
        BattleManager bm = BattleManager.I;
        if (bm == null) return null;
        Fortress best = null;
        float bestDistance = float.MaxValue;
        foreach (Fortress f in bm.EnemiesOf(owner)) {
            if (f == null) continue;
            float d = Mathf.Abs(f.transform.position.x - x);
            if (d < bestDistance) { bestDistance = d; best = f; }
        }
        return best;
    }

    // Where the wave meets `f`: the nearest of its blocks along three level rays round the wave's height (kept within
    // the fortress's height and above the sea), else the near side of its bounds.
    static Vector3 FaceOf(Fortress f, Vector3 from, float dx) {
        Bounds b = f.GetBounds();
        float y = Mathf.Max(World.WaterY + 1.5f, Mathf.Clamp(from.y, b.min.y + 1f, Mathf.Max(b.min.y + 1f, b.max.y - 1f)));
        float side = dx > 0f ? b.min.x : b.max.x;
        float reach = Mathf.Abs(side - from.x) + b.size.x;
        float nearest = float.MaxValue;
        for (int k = -1; k <= 1; k++) {
            int n = Physics.RaycastNonAlloc(new Vector3(from.x, y + 0.9f * k, 0f), Vector3.right * dx, rayHits, reach,
                                            Layers.BlockMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) {
                Block block = rayHits[i].collider != null ? rayHits[i].collider.GetComponent<Block>() : null;
                if (block != null && block.fortress == f && block.IsAlive) nearest = Mathf.Min(nearest, rayHits[i].distance);
            }
        }
        return new Vector3(nearest < float.MaxValue ? from.x + dx * nearest : side, y, 0f);
    }

    // How far the shove may reach without touching a fortress of his own side.
    float ClearOfOwnSide(Vector3 centre) {
        float clear = Mathf.Sqrt(owner.GetBounds().SqrDistance(centre));
        if (owner.team != null)
            foreach (Fortress f in owner.team.fortresses)
                if (f != null) clear = Mathf.Min(clear, Mathf.Sqrt(f.GetBounds().SqrDistance(centre)));
        return clear - 0.5f;
    }

    void BuildArcs() {
        int n = tier >= 3 ? 4 : 3;
        arcs = new LineRenderer[n];
        arcColors = new Color[n];
        points = new Vector3[Segments + 1];
        gradient = new Gradient();
        colorKeys = new GradientColorKey[2];
        alphaKeys = new GradientAlphaKey[4];
        Material m = FxAssets.Additive(FxAssets.Beam, 1.8f + 0.3f * tier);
        for (int i = 0; i < n; i++) {
            LineRenderer lr = new GameObject("Arc " + i).AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.useWorldSpace = true;
            lr.positionCount = Segments + 1;
            lr.sharedMaterial = m;
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCapVertices = 2;
            lr.widthCurve = FxLooks.Curve(0f, 0.25f, 0.5f, 1f, 1f, 0.25f);
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sortingOrder = FxLayers.OrderGlow;
            lr.enabled = false;
            arcs[i] = lr;
            arcColors[i] = i % 2 == 0 ? CandidateShout.Orange : CandidateKit.Teal;
        }
    }

    void Update() {
        if (finished) return;
        if (commander == null || !commander.IsAlive || transform.position.y < World.WaterY) { Fizzle(); return; }
        t += Time.deltaTime;
        switch (phase) {
            case Phase.Shout: if (t >= ShoutAt) Shout(); break;
            case Phase.Out: Out(); break;
            case Phase.Bounce: Bouncing(); break;
            default: Back(); break;
        }
        if (finished || phase == Phase.Shout) return;
        float u = phase == Phase.Bounce ? Mathf.Clamp01((t - phaseStart) / BounceTime) : 0f;
        float alpha = phase == Phase.Out ? Mathf.Clamp01((t - ShoutAt) / 0.08f) : phase == Phase.Bounce ? 1f - 0.15f * u : 0.85f;
        float echo = phase == Phase.Out ? 0f : phase == Phase.Bounce ? 0.4f * u : 0.4f + 0.6f * progress;   // the echo turns teal
        DrawArcs(alpha, echo);
    }

    // He shouts: the wave leaves the megaphone in a spray of glints.
    void Shout() {
        phase = Phase.Out;
        foreach (LineRenderer lr in arcs) lr.enabled = true;
        Vector3 mouth = CandidateKit.Front(transform.position);
        CandidateSounds.Play(CandidateSounds.Shout, mouth, 1f);
        FxLayers.Shock.Emit(mouth, Vector3.zero, 2.2f * scale, 0.25f, UIKit.Alpha(CandidateShout.Orange, 0.8f));
        for (int i = 0; i < 10 + 3 * tier; i++) {
            Vector3 v = dir * Random.Range(3f, 7f) + (Vector3)(Random.insideUnitCircle * 2.5f);
            Color c = i % 3 == 0 ? CandidateKit.Gold : i % 3 == 1 ? CandidateShout.Orange : UIKit.Lighten(CandidateKit.Teal, 0.3f);
            FxLayers.Star.Emit(mouth, v, Random.Range(0.14f, 0.26f), Random.Range(0.35f, 0.6f), c, Random.Range(-120f, 120f));
        }
        BattleCamera.Shake(0.08f + 0.02f * tier);
    }

    // Rolling out to the face, growing taller.
    void Out() {
        Vector3 p = Vector3.MoveTowards(transform.position, hitPoint, speed * Time.deltaTime);
        transform.position = p;
        manualVelocity = dir * speed;
        progress = 1f - Vector3.Distance(p, hitPoint) / legLength;
        size = Mathf.Lerp(legStartSize, EndSize * scale, progress);
        if ((p - hitPoint).sqrMagnitude < 1e-6f) Bounce();
    }

    // The wave meets the face (or rings out in the empty air): it shoves the fortress, then turns over (Bouncing).
    void Bounce() {
        phase = Phase.Bounce;
        phaseStart = t;
        legStartSize = size;
        manualVelocity = Vector3.zero;
        Vector3 face = CandidateKit.Front(hitPoint);
        CandidateSounds.Play(CandidateSounds.Echo, face, 0.9f);
        if (target == null) {
            VFX.Ring(face, 1.8f * scale, CandidateKit.TealHdr, 0.4f, 0.3f);
            return;
        }
        RaiseImpact(hitPoint);
        if (pushRadius >= 1f) Explosion.Push(pushCentre, pushRadius, pushForce, Upward, out _);   // force only: no damage
        float dx = Mathf.Sign(hitPoint.x - pushCentre.x);
        VFX.Ring(face, Mathf.Max(1.5f, pushRadius * 0.5f), CandidateShout.OrangeHdr, 0.45f, 0.45f);
        FxLayers.Shock.Emit(face, Vector3.zero, 4f * scale, 0.35f, UIKit.Alpha(CandidateKit.Teal, 0.7f));
        for (int i = 0; i < 6; i++) VFX.Dust(face + new Vector3(dx * 0.3f, Random.Range(-1.6f, 1.6f) * scale, 0f), 0.8f * scale);
        VFX.LightFlash(face, CandidateKit.Teal, 6f, 9f * scale, 0.35f);
        BattleCamera.Shake(0.2f + 0.03f * tier);
    }

    // The arcs flatten against the face and turn over (sigma 1 -> -1), then the echo heads home.
    void Bouncing() {
        float u = Mathf.Clamp01((t - phaseStart) / BounceTime);
        sigma = Mathf.Cos(Mathf.PI * u);
        size = legStartSize * (1f + 0.15f * Mathf.Sin(Mathf.PI * u) - 0.15f * u);
        if (u < 1f) return;
        phase = Phase.Back;
        sigma = 1f;
        legStartSize = size;
        Vector3 to = Home() - transform.position;
        legLength = Mathf.Max(0.1f, to.magnitude);
        speed = Mathf.Max(EchoSpeed, legLength / MaxLeg);
        dir = to.sqrMagnitude > 1e-4f ? to.normalized : -dir;
        progress = 0f;
    }

    // The echo rolls home to the commander (wherever he is now), shrinking to the shield's size.
    void Back() {
        Vector3 to = Home() - transform.position;
        float dist = to.magnitude, step = speed * Time.deltaTime;
        if (dist <= ArriveAt * scale + step) { Arrive(); return; }
        dir = to / dist;
        transform.position += dir * step;
        manualVelocity = dir * speed;
        progress = Mathf.Clamp01(1f - (dist - step) / legLength);
        size = Mathf.Lerp(legStartSize, EchoSize * scale, progress);
    }

    Vector3 Home() {
        Vector3 c = commander.AimPoint;
        c.z = 0f;
        return c;
    }

    // The echo closes round the commander: the shield (the turn has waited for it).
    void Arrive() {
        CandidateShield.Create(owner, tier);
        Vector3 c = commander.AimPoint;
        VFX.Ring(c, 2.2f * scale, CandidateKit.TealHdr, 0.4f, 0.3f);
        VFX.MagicBurst(c, CandidateKit.Teal, 0.7f * scale);
        Finish(true);
    }

    // Nobody left to come home to: the wave breaks up where it is.
    void Fizzle() {
        if (finished) return;
        if (phase != Phase.Shout) VFX.MagicBurst(transform.position, CandidateKit.Teal, 0.6f);
        Finish(true);
    }

    protected override void OnLost() {
        if (phase == Phase.Back && commander != null && commander.IsAlive) Arrive();
        else Fizzle();
    }

    // The arcs round the wave front: concentric, the leading one biggest, the others following it at Gap steps; sigma
    // turns them over at the bounce. A ripple runs along them; on the way back they fade to teal and thin out.
    void DrawArcs(float alpha, float echo) {
        Vector3 side = new Vector3(-dir.y, dir.x, 0f);
        float time = Time.time;
        for (int i = 0; i < arcs.Length; i++) {
            float r = size * (1f - Gap * i);
            Vector3 apex = transform.position - dir * (size * Gap * i * sigma);
            for (int s = 0; s <= Segments; s++) {
                float phi = Mathf.Lerp(-HalfAngle, HalfAngle, s / (float)Segments);
                float rr = r * (1f + 0.05f * Mathf.Sin(phi * 7f + time * 18f + i * 1.7f));
                Vector3 q = apex + dir * ((rr * Mathf.Cos(phi) - r) * sigma) + side * (rr * Mathf.Sin(phi));
                points[s] = new Vector3(q.x, q.y, WaveZ);
            }
            LineRenderer lr = arcs[i];
            lr.SetPositions(points);
            lr.widthMultiplier = (0.12f + 0.06f * size) * (1f - 0.15f * i) * (1f - 0.3f * echo) * (1f + 0.15f * Mathf.Sin(time * 25f + i));
            Color c = Color.Lerp(arcColors[i], CandidateKit.Teal, echo);
            float a = alpha * (1f - 0.18f * i);
            colorKeys[0] = new GradientColorKey(c, 0f);
            colorKeys[1] = new GradientColorKey(c, 1f);
            alphaKeys[0] = new GradientAlphaKey(0f, 0f);
            alphaKeys[1] = new GradientAlphaKey(a, 0.22f);
            alphaKeys[2] = new GradientAlphaKey(a, 0.78f);
            alphaKeys[3] = new GradientAlphaKey(0f, 1f);
            gradient.SetKeys(colorKeys, alphaKeys);
            lr.colorGradient = gradient;
            if (i == 0) Trail();
        }
    }

    // Glowing motes shed behind the leading arc (its points are in `points` now); gold glints too at the top tier.
    void Trail() {
        trailDue += Time.deltaTime * (30f + 6f * tier);
        for (; trailDue >= 1f; trailDue -= 1f) {
            Vector3 p = points[Random.Range(0, points.Length)];
            Vector3 v = -dir * Random.Range(1.5f, 3.5f) + (Vector3)(Random.insideUnitCircle * 0.8f);
            FxLayers.Glow.Emit(p, v, Random.Range(0.25f, 0.45f) * size, Random.Range(0.25f, 0.4f), UIKit.Alpha(CandidateKit.Teal, 0.35f));
            if (tier >= CandidateVotes.MaxTier && Random.value < 0.35f)
                FxLayers.Star.Emit(p, v * 0.6f + Vector3.up * 0.5f, Random.Range(0.12f, 0.22f), Random.Range(0.4f, 0.7f), CandidateKit.Gold,
                                   Random.Range(-120f, 120f));
        }
    }
}

// The megaphone over the Candidate's chamber (at his launch point, in front of the blocks): it pops up pointing at the
// enemy, kicks back as he shouts, then shrinks away once the wave is off. Visual only; it removes itself.
public class CandidateShoutMegaphone : MonoBehaviour {
    const float PopTime = 0.2f, KickTime = 0.3f, HoldTime = 0.55f, ShrinkTime = 0.25f;

    Transform body;
    float t, shoutAt;

    public static void Create(Vector3 at, Vector3 dir, float size, float shoutAt) {
        GameObject go = new GameObject("Rally Megaphone");
        go.transform.position = new Vector3(at.x, at.y, VFX.FrontZ - 0.45f);
        float tilt = Mathf.Atan2(dir.y, Mathf.Abs(dir.x)) * Mathf.Rad2Deg;
        go.transform.rotation = Quaternion.Euler(0f, dir.x < 0f ? 180f : 0f, tilt);   // +X along dir, the grip below
        go.transform.localScale = Vector3.one * size;
        CandidateShoutMegaphone m = go.AddComponent<CandidateShoutMegaphone>();
        m.body = ModelKit.Root("Body", go.transform).transform;
        CandidateShout.Megaphone(m.body);
        ModelKit.Shadows(go, false);
        m.shoutAt = shoutAt;
        m.Pose();
    }

    void Update() {
        t += Time.deltaTime;
        if (t >= shoutAt + HoldTime + ShrinkTime) { Destroy(gameObject); return; }
        Pose();
    }

    // Pops up (overshooting a little), kicks back along its axis with a flared bell as he shouts, shrinks away at the end.
    void Pose() {
        float s = CandidateShout.EaseOutBack(Mathf.Clamp01(t / PopTime));
        float kick = Mathf.Clamp01((t - shoutAt) / KickTime);
        float e = Mathf.Sin(kick * Mathf.PI) * (1f - kick);
        float shrink = Mathf.Clamp01((t - shoutAt - HoldTime) / ShrinkTime);
        s *= 1f - shrink * shrink;
        body.localScale = new Vector3(s * (1f - 0.3f * e), s * (1f + 0.35f * e), s * (1f + 0.35f * e));
        body.localPosition = new Vector3(-0.15f * e, 0f, 0f);
    }
}

// The echo's shield round the Candidate's commander: a translucent teal bubble with a pulsing glowing rim (a gold one too
// from vote tier 3), bigger and brighter with more votes. While it is up it takes every hit meant for him
// (Commander.shield). It breaks at the start of the turn after the first enemy turn in which it blocked something (at
// tier 5: after the second such turn), or when its owner's second turn after the cast begins (from tier 3: his third).
public class CandidateShield : TurnEffect {
    const int Turns = 2, LongTier = 3, DoubleTier = 5;
    const float FeedbackEvery = 0.3f;             // "BLOCKED" text and sound at most this often
    const float FormTime = 0.35f, SwellTime = 0.18f;
    const float HalfW = 1.25f, HalfH = 1.35f, HalfD = 1.25f;   // the bubble's half sizes at tier 0 (m)
    const float BubbleZ = -0.5f;                  // its centre a little in front of the commander, so it shows over the blocks
    const int RimPoints = 48;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static Mesh sphere;

    Commander commander;
    System.Func<DamageInfo, bool> shieldFn;
    int tier, turns, hits;                        // owner turns it lasts; enemy turns of hits it takes
    int ownerTurns, usedHits;
    int blockTurn = -1;                           // an enemy turn in which it blocked something, not counted yet
    bool breaking;
    float born, size, flash, swell, lastFeedback = -10f;
    Material skin;
    LineRenderer rim, goldRim;
    Light glow;

    public override string StatusText => breaking ? null : "Shield " + Mathf.Max(1, turns - ownerTurns);
    public override Color StatusColor => CandidateKit.Teal;
    public override int RewindState {
        get => ownerTurns + 16 * usedHits;
        set { ownerTurns = value % 16; usedHits = value / 16; blockTurn = -1; }
    }

    // The shield `f` is wearing, if any.
    public static CandidateShield On(Fortress f) {
        if (f == null) return null;
        foreach (TurnEffect e in All)
            if (e is CandidateShield s && s.owner == f && !s.breaking) return s;
        return null;
    }

    // Closes a shield round `owner`'s commander (alive); it replaces one he still wears.
    public static CandidateShield Create(Fortress owner, int tier) {
        CandidateShield old = On(owner);
        if (old != null) old.Cancel();
        GameObject go = new GameObject("Rally Shield");
        CandidateShield s = go.AddComponent<CandidateShield>();
        s.Init(owner, owner);
        s.commander = owner.commander;
        s.tier = tier;
        s.turns = tier >= LongTier ? Turns + 1 : Turns;
        s.hits = tier >= DoubleTier ? 2 : 1;
        s.shieldFn = s.Absorb;
        s.commander.shield = s.shieldFn;
        s.Build();
        CandidateSounds.Play(CandidateSounds.ShieldUp, s.commander.AimPoint, 0.9f);
        return s;
    }

    void Build() {
        size = 1f + 0.07f * tier;
        born = Time.time;
        transform.position = Centre();
        skin = new Material(Mats.Transparent(UIKit.Alpha(CandidateKit.Teal, 0.2f), 0.92f));   // its own copy: it pulses
        GameObject bubble = ModelKit.MeshPart(transform, Sphere, Vector3.zero, new Vector3(HalfW, HalfH, HalfD) * size, skin, default, "Bubble");
        ModelKit.Shadows(bubble, false, false);
        float z = VFX.FrontZ - BubbleZ;           // the rims lie just in front of the blocks
        rim = Rim("Rim", 1f, z, 1.6f + 0.3f * tier);
        if (tier >= LongTier) goldRim = Rim("Gold Rim", 1.08f, z - 0.02f, 1.3f + 0.2f * tier);
        glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = new Vector3(0f, 0.3f, -1.8f);
        glow.type = LightType.Point;
        glow.color = CandidateKit.Teal;
        glow.shadows = LightShadows.None;
        Animate();
    }

    // The bubble's outline as the camera sees it (`k` times its size), a glowing loop.
    LineRenderer Rim(string name, float k, float z, float intensity) {
        LineRenderer lr = new GameObject(name).AddComponent<LineRenderer>();
        lr.transform.SetParent(transform, false);
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = RimPoints;
        for (int i = 0; i < RimPoints; i++) {
            float a = i * Mathf.PI * 2f / RimPoints;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * HalfW * size * k, Mathf.Sin(a) * HalfH * size * k, z));
        }
        lr.sharedMaterial = FxAssets.Additive(FxAssets.Beam, intensity);
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sortingOrder = FxLayers.OrderGlow;
        return lr;
    }

    Vector3 Centre() { return commander.transform.position + new Vector3(0f, 0.82f, BubbleZ); }

    void Update() {
        if (breaking) return;
        if (commander == null || !commander.IsAlive) {   // drowned, or crushed by something no shield stops
            StartCoroutine(Break(false, false));
            return;
        }
        transform.position = Vector3.Lerp(transform.position, Centre(), 1f - Mathf.Exp(-10f * Time.deltaTime));
        Animate();
    }

    // It forms with a pop, breathes, its rim and light pulse; a block flashes it; it puffs up and fades as it breaks.
    void Animate() {
        float now = Time.time;
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 3f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(now * 3.2f);
        float strength = usedHits > 0 ? 0.65f : 1f;   // a tier-5 shield that has taken one turn of hits
        float fade = 1f - 0.8f * swell;
        float form = CandidateShout.EaseOutBack(Mathf.Clamp01((now - born) / FormTime));
        transform.localScale = Vector3.one * (Mathf.LerpUnclamped(0.3f, 1f, form) * (1f + 0.025f * Mathf.Sin(now * 2.1f)) * (1f + 0.1f * flash + 0.3f * swell));
        Color c = CandidateKit.Teal;
        c.a = ((0.16f + 0.02f * tier) * strength + 0.05f * pulse + 0.25f * flash) * fade;
        skin.SetColor(BaseColorId, c);
        rim.widthMultiplier = (0.06f + 0.04f * pulse) * size * (1f + 1.5f * flash);
        rim.startColor = rim.endColor = UIKit.Alpha(CandidateKit.Teal, Mathf.Clamp01((0.55f + 0.45f * pulse) * strength + flash) * fade);
        if (goldRim != null) {
            goldRim.widthMultiplier = 0.035f * size * (1f + flash);
            goldRim.startColor = goldRim.endColor = UIKit.Alpha(CandidateKit.Gold, (0.35f + 0.5f * (1f - pulse)) * strength * fade);
        }
        glow.range = 5f * size;
        glow.intensity = ((1.4f + 0.35f * tier) * (0.75f + 0.25f * pulse) * strength + 6f * flash) * fade;
    }

    // The shield: every hit is taken by the bubble. One taken in an enemy's turn uses that turn up (see OnTurnStart).
    bool Absorb(DamageInfo d) {
        if (breaking) return false;
        BattleManager bm = BattleManager.I;
        if (blockTurn < 0 && bm != null && bm.Current != null && owner != null && bm.Current.team != owner.team) blockTurn = bm.Turn;
        flash = 1f;
        if (Time.time >= lastFeedback + FeedbackEvery) {
            lastFeedback = Time.time;
            VFX.FloatingText(commander.transform.position + Vector3.up * 2.9f, "BLOCKED", UIKit.Lighten(CandidateKit.Teal, 0.45f), 1.1f);
            CandidateSounds.Play(CandidateSounds.ShieldUp, commander.AimPoint, 0.6f, 1.5f);
        }
        VFX.Sparks(d.point == Vector3.zero ? commander.AimPoint : d.point, CandidateKit.TealHdr, 8);
        return true;
    }

    // Runs at the start of every turn. An enemy turn in which it blocked something is counted once that turn is over (a
    // hit at the start of a turn still leaves it up for that turn's shot).
    public override IEnumerator OnTurnStart(Fortress shooter) {
        if (breaking) yield break;
        if (shooter == owner) ownerTurns++;
        int turn = BattleManager.I != null ? BattleManager.I.Turn : int.MaxValue;
        if (blockTurn >= 0 && turn > blockTurn) {
            usedHits++;
            blockTurn = -1;
            if (usedHits < hits) Crack();
        }
        if (usedHits >= hits || ownerTurns >= turns) yield return Break(true, usedHits >= hits);
    }

    // A tier-5 shield has taken its first turn of hits: it flickers and dims, but holds.
    void Crack() {
        flash = 1f;
        VFX.Sparks(transform.position, CandidateKit.TealHdr, 14);
        CandidateSounds.Play(CandidateSounds.ShieldPop, transform.position, 0.5f, 1.35f);
    }

    // Time rewound past the cast, the battle is over, or a new shout replaces it.
    public override void Cancel() {
        breaking = true;
        RemoveShield();
        base.Cancel();
    }

    void OnDestroy() {
        RemoveShield();
        if (skin != null) Destroy(skin);
    }

    void RemoveShield() {
        if (commander != null && shieldFn != null && commander.shield == shieldFn) commander.shield = null;
    }

    // The bubble puffs up for a moment and pops into glints (`hit`: broken by hits, louder than running out). `pause`
    // lets the next shooter see it go before he aims.
    IEnumerator Break(bool pause, bool hit) {
        breaking = true;
        RemoveShield();
        for (float s = 0f; s < SwellTime; s += Time.deltaTime) {
            if (this == null) yield break;
            swell = s / SwellTime;
            Animate();
            yield return null;
        }
        if (this == null) yield break;
        Pop(hit);
        Destroy(gameObject);
        if (pause) yield return new WaitForSeconds(0.4f);
    }

    void Pop(bool hit) {
        Vector3 c = CandidateKit.Front(transform.position);
        float r = HalfH * size;
        VFX.Ring(c, r * 1.8f, CandidateKit.TealHdr, 0.45f, 0.35f);
        VFX.MagicBurst(c, CandidateKit.Teal, 0.8f * size);
        for (int i = 0; i < 14 + 2 * tier; i++) {
            float a = Random.value * Mathf.PI * 2f;
            Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            FxLayers.Star.Emit(c + d * r, d * Random.Range(2.5f, 5.5f), Random.Range(0.14f, 0.26f), Random.Range(0.4f, 0.75f),
                               Color.Lerp(CandidateKit.Teal, Color.white, Random.Range(0.2f, 0.6f)), Random.Range(-120f, 120f));
        }
        VFX.LightFlash(c, CandidateKit.Teal, hit ? 7f : 4f, 8f * size, 0.35f);
        CandidateSounds.Play(CandidateSounds.ShieldPop, c, hit ? 0.95f : 0.65f);
        BattleCamera.Shake(hit ? 0.18f : 0.08f);
    }

    // A smooth unit sphere (its normals are its points), stretched into the bubble.
    static Mesh Sphere {
        get {
            if (sphere != null) return sphere;
            const int Lon = 28, Lat = 16;
            MeshBuilder b = new MeshBuilder();
            for (int j = 0; j <= Lat; j++) {
                float lat = Mathf.PI * (j / (float)Lat - 0.5f);
                for (int i = 0; i <= Lon; i++) {
                    float lon = Mathf.PI * 2f * i / Lon;
                    b.Vertex(new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon)), new Vector2(i / (float)Lon, j / (float)Lat));
                }
            }
            for (int j = 0; j < Lat; j++)
                for (int i = 0; i < Lon; i++) {
                    int a = j * (Lon + 1) + i, above = a + Lon + 1;
                    b.Quad(a, above, above + 1, a + 1);   // bottom-left, top-left, top-right, bottom-right seen from outside
                }
            sphere = b.ToMesh("Rally Shield Bubble");
            sphere.normals = sphere.vertices;
            return sphere;
        }
    }
}

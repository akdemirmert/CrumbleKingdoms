using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The faces of the Deck of Fate that are more than a thrown card: the King, the Ace and the Joker. GamblerDeck.Fire has
// announced the card and calls these with the Deck's aimed velocity (gravity 0.6, wind 0.3). Every damage, blast radius
// and pierce budget here is multiplied by the Gambler's lucky streak (GamblerLuck.Damage, GamblerLuck.Radius), read once
// when the card is thrown; the King's healing is not.
//  * King, "the King's sword" (GamblerKing, GamblerKingSword): he heals himself and throws a sword, point first, along
//    the aim. It pierces like a knife (budget 6: about four stone blocks; 35 a block, 50 to a commander) with sparks at
//    every block, and stays stuck where it stops for a while before it fades away.
//  * Ace, "the Ace falls" (GamblerAce, GamblerAceCard, GamblerAceBlade, GamblerAceRemains): a small ace spins along the
//    aim. 22 m above the first thing of the other side it strikes (the ace finds its mark: above the commander of the
//    enemy fortress it struck, or of one within SnapRange of where it struck the ground) a giant copy of it appears, hangs a moment with a gleam running along its
//    lower edge, then drops straight down edge first like a guillotine blade: a narrow sweep that cuts through every
//    block (40 a block, 35 to a commander) down to the commander or the ground and bursts there (radius 3, 30). The card is left standing in the rubble, sinks
//    a little and turns edge-on out of sight. An ace lost at sea calls nothing.
//  * Joker, "the Joker juggles" (GamblerJoker, GamblerJokerJester, GamblerJokerPin): a jester pops up in confetti by
//    the throw point, tosses six bowling pins underhand along the aim, one every 0.22 s with a little scatter, bows and
//    vanishes in confetti. Each pin tumbles end over end and bursts (radius 2.4, 32) on the first thing it touches; one
//    that falls into the sea only splashes.
// The turn waits for every part: the sword; the small ace and then the giant one until its blast; the jester and every pin.

// ====================================================================== King
public static class GamblerKing {
    const float HealAmount = 20f;                                   // not boosted by luck
    const float Budget = 6f, BlockDamage = 35f, CommanderDamage = 50f;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity, PlayingCard card) {
        float damage = GamblerLuck.Damage(owner), reach = GamblerLuck.Radius(owner);
        if (owner != null && owner.CommanderAlive) {
            owner.commander.Heal(HealAmount);
            GamblerKit.Sparkle(owner.commander.transform.position + Vector3.up * 1.1f, 0.9f);
        }
        GamblerSounds.Play(GamblerSounds.SwordRing, origin, 0.9f);
        WeaponFactory.Spawn<GamblerKingSword>("King's sword", owner, def, origin, velocity, s => {
            s.budget = Budget * reach;
            s.damage = BlockDamage * damage;
            s.commanderDamage = CommanderDamage * damage;
            s.sweepRadius = 0.14f;
            s.embedDepth = 0.45f;          // the point sinks in; most of the blade stands out of the wall
        });
    }
}

// The King's sword in flight (FaceVelocity keeps it point first). Its point is at the origin, so the sweep touches what
// the point touches. Stuck, it stays StuckTime and then shrinks away; if what it stuck in breaks first,
// PiercingProjectile lets it drop as debris.
public class GamblerKingSword : PiercingProjectile {
    const float StuckTime = 6f, FadeTime = 0.4f;
    static readonly Color SparkColor = new Color(1f, 0.86f, 0.55f);

    Transform model;
    GameObject trail;

    protected override void OnLaunch(Vector3 velocity) {
        base.OnLaunch(velocity);
        model = Build(transform).transform;
        trail = VFX.Trail(transform, new Color(1f, 0.93f, 0.72f, 0.55f), 0.14f, 0.22f);
        trail.transform.localPosition = new Vector3(-0.6f, 0f, 0f);   // streams from the middle of the blade
        BlockPierced += (self, block, point) => VFX.Sparks(point, SparkColor, 12);
        Stopped += OnStopped;
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (!finished && transform.position.y < World.WaterY) Splash();
    }

    void OnStopped(PiercingProjectile self, Vector3 point, Collider hit) {
        VFX.Sparks(point, SparkColor, 18);
        if (trail != null) Destroy(trail, 0.3f);
        trail = null;
        StartCoroutine(FadeLater());
    }

    IEnumerator FadeLater() {
        yield return new WaitForSeconds(StuckTime);
        for (float t = 0f; t < FadeTime; t += Time.deltaTime) {
            model.localScale = Vector3.one * (1f - t / FadeTime);
            yield return null;
        }
        Destroy(gameObject);
    }

    // Into the sea: a splash, nothing more.
    void Splash() {
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);
        if (!impacted) RaiseImpact(p);
        VFX.Splash(p, 0.6f);
        SFX.Play(SoundId.Splash, p, 0.6f);
        Finish(true);
    }

    // A knightly sword about 1.5 m long along +X, its point just ahead of the origin: a steel blade 1.1 m long with a
    // darker fuller and a pointed tip, a gold cross-guard with round ends and a ruby, a red grip and a gold pommel.
    static GameObject Build(Transform parent) {
        GameObject root = ModelKit.Root("King's sword", parent);
        Transform r = root.transform;
        // Only half metallic: the arenas have no reflection probes, and a fully metallic surface would render dark.
        Material steel = Mats.Lit(new Color(0.84f, 0.87f, 0.92f), 0.85f, 0.5f);
        Material fuller = Mats.Lit(new Color(0.58f, 0.62f, 0.68f), 0.75f, 0.45f);
        Material gold = GamblerKit.GoldMat, grip = Mats.Lit(new Color(0.6f, 0.07f, 0.1f), 0.3f);
        ModelKit.Part(r, PrimitiveType.Cube, new Vector3(-0.59f, 0f, 0f), new Vector3(0.92f, 0.12f, 0.025f), steel);
        ModelKit.MeshPart(r, MeshKit.Cone(4, 0.06f, 0.18f), new Vector3(-0.13f, 0f, 0f), new Vector3(1f, 1f, 0.21f), steel,
                          new Vector3(0f, 0f, -90f), "Tip");
        ModelKit.Part(r, PrimitiveType.Cube, new Vector3(-0.62f, 0f, 0f), new Vector3(0.7f, 0.025f, 0.03f), fuller);
        ModelKit.Part(r, PrimitiveType.Cube, new Vector3(-1.08f, 0f, 0f), new Vector3(0.06f, 0.4f, 0.07f), gold);
        ModelKit.Part(r, PrimitiveType.Sphere, new Vector3(-1.08f, 0.2f, 0f), Vector3.one * 0.08f, gold);
        ModelKit.Part(r, PrimitiveType.Sphere, new Vector3(-1.08f, -0.2f, 0f), Vector3.one * 0.08f, gold);
        ModelKit.Part(r, PrimitiveType.Sphere, new Vector3(-1.08f, 0f, -0.04f), Vector3.one * 0.05f,
                      Mats.Emissive(new Color(0.8f, 0.05f, 0.1f), new Color(1.6f, 0.08f, 0.18f)));   // a ruby in the guard
        ModelKit.Part(r, PrimitiveType.Cylinder, new Vector3(-1.24f, 0f, 0f), new Vector3(0.05f, 0.13f, 0.05f), grip, new Vector3(0f, 0f, 90f));
        ModelKit.Part(r, PrimitiveType.Sphere, new Vector3(-1.41f, 0f, 0f), Vector3.one * 0.1f, gold);
        return root;
    }
}

// ====================================================================== Ace
public static class GamblerAce {
    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity, PlayingCard card) {
        float damage = GamblerLuck.Damage(owner), reach = GamblerLuck.Radius(owner);
        GamblerSounds.Play(GamblerSounds.CardFlick, origin, 0.85f);
        WeaponFactory.Spawn<GamblerAceCard>(card.Name, owner, def, origin, velocity, a => {
            a.card = card;
            a.damageBoost = damage;
            a.radiusBoost = reach;
        });
    }
}

// The small ace: a card spinning in the picture plane along the aimed arc. It is moved by hand and swept like a piercing
// projectile, so it marks exactly the point it touches and pushes nothing. The first solid thing of the other side it
// touches calls the giant ace (GamblerAceBlade) above that point.
public class GamblerAceCard : Projectile {
    const float Height = 0.35f, Radius = 0.12f, Spin = 900f;   // m, m, deg/s
    static readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    public PlayingCard card;
    public float damageBoost = 1f, radiusBoost = 1f;

    Transform spinner;

    protected override void OnLaunch(Vector3 velocity) {
        manualVelocity = velocity;
        maxLifetime = 10f;
        spinner = ModelKit.Root("Spin", transform).transform;
        GamblerKit.Card(spinner, card, Height);
        VFX.Trail(transform, new Color(1f, 0.85f, 0.45f, 0.7f), 0.12f, 0.25f);
    }

    void Update() {
        if (!finished) spinner.Rotate(0f, 0f, -travelDir * Spin * Time.deltaTime, Space.Self);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished) return;
        float dt = Time.fixedDeltaTime;
        Vector3 v = manualVelocity + new Vector3(BattleManager.Wind * windFactor, -World.Gravity * gravityScale, 0f) * dt;
        Vector3 p = transform.position;
        if (FirstHit(p, v * dt, out Vector3 point, out Collider hit)) { Strike(point, hit); return; }
        manualVelocity = v;
        transform.position = p + v * dt;
        if (transform.position.y < World.WaterY) Sink();
    }

    // The nearest solid thing along this step that is not of our own side.
    bool FirstHit(Vector3 p, Vector3 step, out Vector3 point, out Collider hit) {
        point = p;
        hit = null;
        float len = step.magnitude;
        if (len < 1e-5f) return false;
        int n = Physics.SphereCastNonAlloc(p, Radius, step / len, hitBuffer, len, Layers.SolidMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++) {
            RaycastHit h = hitBuffer[i];
            if (h.collider == null || h.distance >= best || IsOwnTeam(h.collider)) continue;
            best = h.distance;
            hit = h.collider;
            point = h.distance <= 0f && h.point == Vector3.zero ? p : h.point;   // overlapping at the start of the step
        }
        return best < float.MaxValue;
    }

    void Strike(Vector3 point, Collider hit) {
        point.z = 0f;
        if (point.y < World.WaterY) { Sink(); return; }   // the foot of a cliff under the waterline
        RaiseImpact(point);
        GamblerAceBlade.Summon(owner, def, card, point, FortressOf(hit), damageBoost, radiusBoost);
        GamblerKit.Sparkle(point, 0.5f);
        VFX.Ring(point, 1.3f, GamblerKit.Gold, 0.35f);
        Finish(true);
    }

    // The fortress a block or commander belongs to (null for the ground).
    static Fortress FortressOf(Collider c) {
        if (c == null) return null;
        Block b = c.GetComponentInParent<Block>();
        if (b != null) return b.fortress;
        Commander cm = c.GetComponentInParent<Commander>();
        return cm != null ? cm.fortress : null;
    }

    // Lost at sea: a splash, and no giant ace.
    void Sink() {
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);
        if (!impacted) RaiseImpact(p);
        VFX.Splash(p, 0.35f);
        SFX.Play(SoundId.Splash, p, 0.4f, 1.4f);
        Finish(true);
    }
}

// The giant ace. It hangs still for HangTime (it pops in and a gleam runs along its lower edge) while the camera frames
// the drop, then falls straight down at DropSpeed with no gravity or wind of its own, so it neither curves nor speeds
// up. The projectile is turned to face straight down (as FaceVelocity turns it in the fall) and the card is kept upright
// on a holder inside it, its lower edge Lead below the sweep's centre. Where it stops it bursts; the card is handed to
// GamblerAceRemains and the projectile goes, its part of the turn done.
public class GamblerAceBlade : PiercingProjectile {
    const float DropHeight = 22f, DropSpeed = 45f;
    const float CardHeight = 3.2f, Lead = 0.5f;
    const float PopTime = 0.12f, HangTime = 0.4f;
    const float Budget = 99f, BlockDamage = 40f, CommanderDamage = 35f;   // a guillotine: it cuts through whatever is above him
    const float BlastRadius = 3f, BlastDamage = 30f, BlastForce = 12f;
    const float SnapRange = 3.5f;   // m: an enemy commander this close (in x) to the strike draws the drop onto himself

    public PlayingCard card;
    public Vector3 target;
    public float blastRadius, blastDamage;

    Transform holder;
    GameObject[] streaks;
    float shown;
    bool dropping;

    Vector3 CardMiddle => transform.position + Vector3.up * (CardHeight * 0.5f - Lead);

    public static void Summon(Fortress owner, WeaponDef def, PlayingCard card, Vector3 target, Fortress struck, float damageBoost, float radiusBoost) {
        target = Mark(owner, target, struck);
        Vector3 top = new Vector3(target.x, target.y + DropHeight, 0f);
        WeaponFactory.Spawn<GamblerAceBlade>("Giant " + card.Name, owner, def, top, Vector3.zero, b => {
            b.card = card;
            b.target = target;
            b.sweepRadius = 0.35f;         // it cuts with its edge: about one block per layer, so it goes deep
            b.slowPerCost = 0.04f;         // and the blocks barely slow it
            b.embedDepth = 0.5f;
            b.budget = Budget * radiusBoost;
            b.damage = BlockDamage * damageBoost;
            b.commanderDamage = CommanderDamage * damageBoost;
            b.blastRadius = BlastRadius * radiusBoost;
            b.blastDamage = BlastDamage * damageBoost;
            b.cameraFollows = false;       // the camera frames the whole drop instead
        });
    }

    // The ace finds its mark: it drops onto the commander of the enemy fortress it struck, or onto a living enemy
    // commander within SnapRange (in x) of where it struck the ground.
    static Vector3 Mark(Fortress owner, Vector3 strike, Fortress struck) {
        if (BattleManager.I == null || owner == null) return strike;
        if (struck != null && struck.team != owner.team && struck.CommanderAlive)
            return new Vector3(struck.commander.transform.position.x, Mathf.Max(strike.y, struck.commander.AimPoint.y), 0f);
        Vector3 mark = strike;
        float best = SnapRange;
        foreach (Fortress f in BattleManager.I.EnemiesOf(owner)) {
            if (!f.CommanderAlive) continue;
            float x = f.commander.transform.position.x, d = Mathf.Abs(x - strike.x);
            if (d < best) { best = d; mark = new Vector3(x, Mathf.Max(strike.y, f.commander.AimPoint.y), 0f); }
        }
        return mark;
    }

    protected override void OnLaunch(Vector3 velocity) {
        base.OnLaunch(velocity);
        gravityScale = 0f;
        windFactor = 0f;
        maxLifetime = 6f;
        transform.rotation = Quaternion.Euler(0f, 0f, -90f);
        holder = ModelKit.Root("Upright", transform).transform;
        holder.localRotation = Quaternion.Euler(0f, 0f, 90f);
        holder.localScale = Vector3.one * 0.01f;
        GamblerKit.Card(holder, card, CardHeight).transform.localPosition = new Vector3(0f, CardHeight * 0.5f - Lead, 0f);
        Stopped += OnStopped;
        GamblerKit.Sparkle(CardMiddle, 1.3f);
        GamblerSounds.Play(GamblerSounds.CardFlip, CardMiddle, 0.9f);
        if (BattleCamera.I != null) {
            Bounds view = new Bounds(target, new Vector3(16f, 6f, 0f));   // some of what it will cut down into, too
            view.Encapsulate(new Vector3(target.x, transform.position.y + CardHeight - Lead + 1f, 0f));
            BattleCamera.I.Frame(view, 3f);
        }
    }

    void Update() {
        if (finished || holder == null) return;
        shown += Time.deltaTime;
        float k = Mathf.Clamp01(shown / PopTime);
        holder.localScale = Vector3.one * Mathf.Max(0.01f, 1f - Mathf.Pow(1f - k, 3f) + Mathf.Sin(k * Mathf.PI) * 0.15f);
        if (!dropping && shown >= PopTime) Gleam((shown - PopTime) / (HangTime - PopTime));
    }

    // A glint running along the lower edge from corner to corner while it hangs: the blade is sharp.
    void Gleam(float f) {
        float halfWidth = CardHeight * GamblerKit.CardAspect * 0.5f;
        Vector3 at = transform.position + new Vector3(Mathf.Lerp(-halfWidth, halfWidth, Mathf.Clamp01(f)), -Lead, 0f);
        FxLayers.Star.Emit(GamblerKit.Front(at), Vector3.zero, 0.7f, 0.25f, Color.white, 90f);
    }

    protected override void FixedUpdate() {
        if (!finished && !dropping && age >= HangTime) Drop();
        base.FixedUpdate();
        if (!finished && transform.position.y < World.WaterY) Sink();
    }

    void Drop() {
        dropping = true;
        manualVelocity = Vector3.down * DropSpeed;
        GamblerSounds.Play(GamblerSounds.SwordRing, transform.position, 1f, 0.75f);
        // Speed lines from its top corners.
        float halfWidth = CardHeight * GamblerKit.CardAspect * 0.5f;
        streaks = new GameObject[2];
        for (int i = 0; i < streaks.Length; i++) {
            streaks[i] = VFX.Trail(holder, new Color(1f, 0.95f, 0.8f, 0.5f), 0.2f, 0.2f);
            streaks[i].transform.localPosition = new Vector3(i == 0 ? -halfWidth : halfWidth, CardHeight - Lead, 0f);
        }
    }

    void OnStopped(PiercingProjectile self, Vector3 point, Collider hit) {
        point.z = 0f;
        LeaveCard();
        transform.SetParent(null, true);   // out of the block it stuck in, so the blast does not release this husk as debris
        if (point.y < World.WaterY) VFX.Splash(point, 2.5f);
        else {
            Explosion.Create(point, blastRadius, blastDamage, BlastForce, owner, DamageType.Explosion, BlastStyle.Fire);
            GamblerKit.Sparkle(point, 1.2f, false);
        }
        Destroy(gameObject);
    }

    // Fell into the sea: a great splash, no blast.
    void Sink() {
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);
        if (!impacted) RaiseImpact(p);
        VFX.Splash(p, 3f);
        SFX.Play(SoundId.Splash, p, 1f, 0.7f);
        LeaveCard();
        Finish(true);
    }

    void LeaveCard() {
        if (holder != null) GamblerAceRemains.Leave(holder);
        holder = null;
        if (streaks != null)
            foreach (GameObject s in streaks) if (s != null) Destroy(s, 0.25f);
        streaks = null;
    }
}

// What is left of the giant ace: the card stands where it struck, sinking slowly, then turns edge-on (a card seen from
// the side is gone) and is destroyed. Visual only; it does not hold the turn.
public class GamblerAceRemains : MonoBehaviour {
    const float StandTime = 0.7f, TurnTime = 0.45f, SinkSpeed = 0.5f;

    float t;

    public static void Leave(Transform card) {
        card.SetParent(null, true);
        card.gameObject.AddComponent<GamblerAceRemains>();
    }

    void Update() {
        t += Time.deltaTime;
        transform.position += Vector3.down * SinkSpeed * Time.deltaTime;
        float k = Mathf.Clamp01((t - StandTime) / TurnTime);
        transform.rotation = Quaternion.Euler(0f, 90f * k * k, 0f);
        if (k >= 1f) Destroy(gameObject);
    }
}

// ====================================================================== Joker
public static class GamblerJoker {
    const float BlastRadius = 2.4f, BlastDamage = 32f;

    public static readonly Color Purple = new Color(0.46f, 0.18f, 0.66f);
    public static readonly Color[] Colours = {
        GamblerKit.CardRed, Purple, GamblerKit.Gold, new Color(0.2f, 0.6f, 1f), new Color(0.3f, 0.85f, 0.4f), Color.white,
    };

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity, PlayingCard card) {
        float damage = GamblerLuck.Damage(owner), reach = GamblerLuck.Radius(owner);
        WeaponFactory.Spawn<GamblerJokerJester>("Joker", owner, def, origin, velocity, j => {
            j.pinRadius = BlastRadius * reach;
            j.pinDamage = BlastDamage * damage;
        });
    }

    // A white bowling pin with two red stripes round its neck, 0.45 m tall along +Y and centred on its middle.
    public static GameObject Pin(Transform parent) {
        GameObject root = ModelKit.Root("Bowling pin", parent);
        Transform r = root.transform;
        Material white = Mats.Lit(new Color(0.97f, 0.96f, 0.93f), 0.65f), red = GamblerKit.RedInk;
        Mesh ball = WorldMeshes.Ball(0.5f);   // 1 m across: its scale is the diameter
        ModelKit.MeshPart(r, WorldMeshes.Frustum(10, 0.05f, 0.075f, 0.08f), new Vector3(0f, -0.225f, 0f), Vector3.one, white);   // foot
        ModelKit.MeshPart(r, ball, new Vector3(0f, -0.08f, 0f), new Vector3(0.17f, 0.24f, 0.17f), white);                        // belly
        ModelKit.MeshPart(r, WorldMeshes.Frustum(10, 0.055f, 0.03f, 0.14f), new Vector3(0f, 0.02f, 0f), Vector3.one, white);     // neck
        ModelKit.MeshPart(r, ball, new Vector3(0f, 0.18f, 0f), Vector3.one * 0.09f, white);                                      // head
        ModelKit.MeshPart(r, WorldMeshes.Frustum(10, 0.051f, 0.048f, 0.018f), new Vector3(0f, 0.055f, 0f), Vector3.one, red);
        ModelKit.MeshPart(r, WorldMeshes.Frustum(10, 0.045f, 0.042f, 0.018f), new Vector3(0f, 0.09f, 0f), Vector3.one, red);
        return root;
    }

    // A puff of paper confetti in every colour, with a white pop.
    public static void Confetti(Vector3 pos, float size) {
        Vector3 at = GamblerKit.Front(pos);
        int n = Mathf.Clamp(Mathf.RoundToInt(7f * size), 3, 14);
        foreach (Color c in Colours) VFX.Burst(at, n, 6f * size, 0.14f, 1.4f, c, 0.3f, false, TexKit.Chip);
        FxLayers.Flash.Emit(at, Vector3.zero, 1.8f * size, 0.12f, Color.white);
    }
}

// The jester. A Projectile only so the turn waits for him and the camera can look at him while he gets ready: no body,
// no collider, and he never moves. His figure stands a little behind and below the throw point, in front of the walls,
// facing the camera, mirrored when he throws to the left; the pins leave from the throw point itself, so they fly the
// aimed arc. He pops up, tosses Pins pins underhand, one every ThrowEvery seconds, bows and vanishes. If his Gambler
// falls meanwhile he throws no more.
public class GamblerJokerJester : Projectile {
    const int Pins = 6;
    const float ThrowEvery = 0.22f, AngleJitter = 2f, SpeedJitter = 0.04f;   // s, degrees, share of the speed
    const float PopTime = 0.28f, BowTime = 0.5f;
    const float Z = -1.05f;                                                   // in front of the walls (blocks span z = ±0.8)
    static readonly Vector2 Feet = new Vector2(-0.4f, -0.42f);               // from the throw point; x is towards the throw
    // Arm angles in degrees: 0 = hanging down, 90 = pointing the way he throws, 180 = straight up. The throw is an
    // underarm swing from Back through Release (the hand is then at the throw point) up to Follow.
    const float Hold = 25f, Back = -55f, Release = 65f, Follow = 115f;

    public float pinRadius, pinDamage;

    readonly List<GamblerJokerPin> troupe = new List<GamblerJokerPin>();
    Vector3 aim, stand;
    Transform figure, upper, throwArm, freeArm;
    GameObject handPin;
    float clock;
    bool bowing, gone;

    bool GamblerStands => owner == null || owner.CommanderAlive;   // no throws from the grave

    protected override void OnLaunch(Vector3 velocity) {
        aim = velocity;
        maxLifetime = 6f;                  // safety net: the act takes about 2.2 s
        figure = Build(transform, out upper, out throwArm, out freeArm, out handPin);
        stand = new Vector3(Feet.x * travelDir, Feet.y, Z);
        figure.localPosition = stand;
        SetSize(0.01f);
        handPin.SetActive(false);
        SetArm(throwArm, Hold);
        StartCoroutine(Act());
    }

    void Update() {
        if (gone) return;
        clock += Time.deltaTime;
        if (!bowing) SetArm(freeArm, 150f + 18f * Mathf.Sin(clock * 9f));   // the other hand waves, ready to catch
    }

    IEnumerator Act() {
        Vector3 middle = figure.position + Vector3.up * 0.6f;
        GamblerSounds.Play(GamblerSounds.Honk, middle, 0.9f);
        GamblerJoker.Confetti(middle, 1f);
        for (float t = 0f; t < PopTime; t += Time.deltaTime) {
            float k = t / PopTime;
            SetSize(1f - Mathf.Pow(1f - k, 3f) + Mathf.Sin(k * Mathf.PI) * 0.25f);   // springs up, a little too big
            figure.localPosition = stand + Vector3.up * 0.25f * Mathf.Sin(k * Mathf.PI);
            yield return null;
        }
        SetSize(1f);
        figure.localPosition = stand;

        float arm = Hold;
        for (int i = 0; i < Pins && GamblerStands; i++) {
            handPin.SetActive(true);
            yield return Swing(arm, Back, ThrowEvery * 0.55f);
            yield return Swing(Back, Release, ThrowEvery * 0.3f);
            handPin.SetActive(false);
            Throw();
            yield return Swing(Release, Follow, ThrowEvery * 0.15f);
            arm = Follow;
        }

        // The bow: he bends towards the audience, one hand across his belly, the other swept out.
        bowing = true;
        float free = freeArm.localEulerAngles.z;
        for (float t = 0f; t < BowTime; t += Time.deltaTime) {
            float k = Mathf.Sin(Mathf.PI * t / BowTime);
            upper.localRotation = Quaternion.Euler(-40f * k, 0f, 0f);
            SetArm(throwArm, Mathf.Lerp(arm, -30f, k));
            SetArm(freeArm, Mathf.LerpAngle(free, -120f, k));
            yield return null;
        }
        Vanish();
    }

    IEnumerator Swing(float from, float to, float time) {
        for (float t = 0f; t < time; t += Time.deltaTime) {
            SetArm(throwArm, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / time)));
            yield return null;
        }
        SetArm(throwArm, to);
    }

    // One pin along the aim, scattered a little. The camera follows the oldest pin still flying.
    void Throw() {
        Vector3 v = Quaternion.Euler(0f, 0f, Random.Range(-AngleJitter, AngleJitter)) * aim * Random.Range(1f - SpeedJitter, 1f + SpeedJitter);
        GamblerJokerPin pin = WeaponFactory.Spawn<GamblerJokerPin>("Joker's pin", owner, def, transform.position, v, p => {
            p.blastRadius = pinRadius;
            p.blastDamage = pinDamage;
            p.troupe = troupe;
            p.cameraFollows = false;
        });
        troupe.Add(pin);
        cameraFollows = false;
        GamblerJokerPin.PassCamera(troupe);
        SFX.Play(SoundId.KnifeThrow, transform.position, 0.35f, Random.Range(1.15f, 1.35f));
    }

    void Vanish() {
        if (gone) return;
        gone = true;
        Vector3 middle = figure.position + Vector3.up * 0.6f;
        GamblerJoker.Confetti(middle, 1.2f);
        SFX.Play(SoundId.Illusion, middle, 0.5f, 1.3f);
        Finish(true);
    }

    protected override void OnLost() { Vanish(); }

    void SetSize(float s) {
        s = Mathf.Max(0.01f, s);
        figure.localScale = new Vector3(travelDir * s, s, s);
    }

    static void SetArm(Transform arm, float degrees) { arm.localRotation = Quaternion.Euler(0f, 0f, degrees); }

    // ------------------------------------------------------------------ model
    // The jester, about 1.2 m tall with his feet at the origin, facing the camera, +X the way he throws: harlequin legs in
    // pointed shoes with bells, a red and purple doublet with gold diamonds, a white ruff, a white face with a red nose,
    // pink cheeks and a red smile, and a two-pointed red and purple hat with gold bells. `upper` (from the hips up) bends
    // for the bow; the arm pivots sit at the shoulders; a pin waits in the throwing hand.
    static Transform Build(Transform parent, out Transform upper, out Transform throwArm, out Transform freeArm, out GameObject handPin) {
        Material red = Mats.Lit(GamblerKit.CardRed, 0.35f), purple = Mats.Lit(GamblerJoker.Purple, 0.35f), gold = GamblerKit.GoldMat;
        Material white = GamblerKit.PaperMat, black = GamblerKit.BlackInk, face = Mats.Lit(new Color(0.99f, 0.97f, 0.95f), 0.3f);
        Material nose = Mats.Lit(new Color(0.95f, 0.08f, 0.1f), 0.75f), cheek = Mats.Lit(new Color(1f, 0.55f, 0.6f), 0.3f);
        Mesh ball = WorldMeshes.Ball(0.5f);   // 1 m across: its scale is the diameter
        Transform root = ModelKit.Root("Joker", parent).transform;

        // Legs (the front one purple, the back one red) in shoes that curl outwards, a bell on each toe.
        for (int s = -1; s <= 1; s += 2) {
            ModelKit.MeshPart(root, WorldMeshes.Frustum(6, 0.05f, 0.045f, 0.36f), new Vector3(s * 0.07f, 0.03f, 0f), Vector3.one, s > 0 ? purple : red);
            ModelKit.MeshPart(root, MeshKit.Cone(5, 0.05f, 0.16f), new Vector3(s * 0.07f, 0.035f, 0f), new Vector3(1f, 1f, 0.8f),
                              s > 0 ? red : purple, new Vector3(0f, 0f, -s * 80f));
            ModelKit.MeshPart(root, ball, new Vector3(s * 0.228f, 0.063f, 0f), Vector3.one * 0.045f, gold);
        }

        upper = Pivot("Upper", root, new Vector3(0f, 0.38f, 0f));
        // The doublet in two halves, gold diamonds down the front.
        ModelKit.Part(upper, PrimitiveType.Cube, new Vector3(-0.065f, 0.18f, 0f), new Vector3(0.13f, 0.36f, 0.2f), red);
        ModelKit.Part(upper, PrimitiveType.Cube, new Vector3(0.065f, 0.18f, 0f), new Vector3(0.13f, 0.36f, 0.2f), purple);
        for (int k = 0; k < 3; k++)
            ModelKit.Part(upper, PrimitiveType.Cube, new Vector3(0f, 0.08f + 0.1f * k, -0.103f), new Vector3(0.05f, 0.05f, 0.012f), gold, new Vector3(0f, 0f, 45f));
        // The ruff: a white disc frilled with little balls.
        ModelKit.MeshPart(upper, ball, new Vector3(0f, 0.37f, 0f), new Vector3(0.34f, 0.07f, 0.26f), white);
        for (int k = 0; k < 8; k++) {
            float a = k * Mathf.PI / 4f;
            ModelKit.MeshPart(upper, ball, new Vector3(Mathf.Cos(a) * 0.15f, 0.37f, Mathf.Sin(a) * 0.11f), Vector3.one * 0.08f, white);
        }
        // The face.
        ModelKit.MeshPart(upper, ball, new Vector3(0f, 0.52f, 0f), Vector3.one * 0.24f, face);
        ModelKit.MeshPart(upper, ball, new Vector3(0f, 0.515f, -0.12f), Vector3.one * 0.07f, nose);
        ModelKit.Part(upper, PrimitiveType.Cube, new Vector3(0f, 0.465f, -0.108f), new Vector3(0.04f, 0.012f, 0.01f), nose);   // the smile
        for (int s = -1; s <= 1; s += 2) {
            ModelKit.MeshPart(upper, ball, new Vector3(s * 0.045f, 0.55f, -0.1f), Vector3.one * 0.032f, black);
            ModelKit.MeshPart(upper, ball, new Vector3(s * 0.075f, 0.5f, -0.085f), Vector3.one * 0.045f, cheek);
            ModelKit.Part(upper, PrimitiveType.Cube, new Vector3(s * 0.035f, 0.475f, -0.106f), new Vector3(0.035f, 0.012f, 0.01f), nose, new Vector3(0f, 0f, s * 30f));
        }
        // The hat: a gold band and two floppy points, red and purple, with a gold bell on each.
        ModelKit.MeshPart(upper, WorldMeshes.Frustum(10, 0.125f, 0.12f, 0.05f), new Vector3(0f, 0.58f, 0f), Vector3.one, gold);
        for (int s = -1; s <= 1; s += 2) {
            Material m = s < 0 ? red : purple;
            ModelKit.MeshPart(upper, MeshKit.Cone(6, 0.07f, 0.28f), new Vector3(s * 0.05f, 0.62f, 0f), Vector3.one, m, new Vector3(0f, 0f, -s * 55f));
            ModelKit.MeshPart(upper, MeshKit.Cone(5, 0.035f, 0.13f), new Vector3(s * 0.279f, 0.781f, 0f), Vector3.one, m, new Vector3(0f, 0f, -s * 125f));
            ModelKit.MeshPart(upper, ball, new Vector3(s * 0.386f, 0.706f, 0f), Vector3.one * 0.06f, gold);
        }
        // Arms: the throwing one on the front side.
        throwArm = Arm(upper, 1, red, white, out Transform hand);
        freeArm = Arm(upper, -1, purple, white, out _);
        handPin = GamblerJoker.Pin(hand);
        handPin.transform.localPosition = new Vector3(0f, -0.1f, 0f);   // held by the neck, the belly beyond the hand
        return root;
    }

    // An arm hanging from its shoulder pivot: a sleeve and a white glove; `hand` is where a held pin goes.
    static Transform Arm(Transform upper, int side, Material sleeve, Material glove, out Transform hand) {
        Transform pivot = Pivot(side > 0 ? "Throwing arm" : "Free arm", upper, new Vector3(side * 0.16f, 0.33f, 0f));
        ModelKit.MeshPart(pivot, WorldMeshes.Frustum(6, 0.045f, 0.035f, 0.3f), Vector3.zero, Vector3.one, sleeve, new Vector3(0f, 0f, 180f));
        ModelKit.MeshPart(pivot, WorldMeshes.Ball(0.5f), new Vector3(0f, -0.32f, 0f), Vector3.one * 0.1f, glove);
        hand = Pivot("Hand", pivot, new Vector3(0f, -0.33f, 0f));
        return pivot;
    }

    static Transform Pivot(string name, Transform parent, Vector3 localPos) {
        Transform t = ModelKit.Root(name, parent).transform;
        t.localPosition = localPos;
        return t;
    }
}

// One of the Joker's pins: a rigid body that tumbles end over end (only the model turns) along its arc and bursts on the
// first thing it touches; in the sea it only splashes.
public class GamblerJokerPin : Projectile {
    const float Radius = 0.15f, Mass = 1f, Tumble = 760f, BlastForce = 9f;   // m, kg, deg/s, m/s

    public float blastRadius = 2f, blastDamage = 26f;
    public List<GamblerJokerPin> troupe;     // every pin of this act (outlives the jester)

    Transform model;
    float spin;

    // The camera follows one pin of an act at a time: the oldest still flying.
    public static void PassCamera(List<GamblerJokerPin> troupe) {
        if (troupe == null) return;
        bool given = false;
        foreach (GamblerJokerPin p in troupe) {
            if (p == null || p.finished) continue;
            p.cameraFollows = !given;
            given = true;
        }
    }

    protected override void OnLaunch(Vector3 velocity) {
        maxLifetime = 10f;
        model = GamblerJoker.Pin(transform).transform;
        spin = Random.Range(0f, 360f);
        model.localRotation = Quaternion.Euler(0f, 0f, spin);
        gameObject.AddComponent<SphereCollider>().radius = Radius;
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        VFX.Trail(transform, new Color(1f, 1f, 1f, 0.4f), 0.1f, 0.18f);
    }

    void Update() {
        if (finished) return;
        spin += -travelDir * Tumble * Time.deltaTime;
        model.localRotation = Quaternion.Euler(0f, 0f, spin);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (!finished && transform.position.y < World.WaterY) Sink();
    }

    void OnCollisionEnter(Collision c) {
        if (finished) return;
        SoldierWeapons.FirstContact(c, transform.position, out Vector3 point, out _);
        SoldierWeapons.LimitShove(c);
        point.z = 0f;
        if (point.y < World.WaterY) { Sink(); return; }
        RaiseImpact(point);
        Explosion.Create(point, blastRadius, blastDamage, BlastForce, owner, DamageType.Explosion, BlastStyle.Small);
        Vector3 at = GamblerKit.Front(point);
        for (int i = 0; i < 10; i++)                                   // a twinkle of confetti colours in the blast
            FxLayers.Star.Emit(at + (Vector3)(Random.insideUnitCircle * 0.5f), new Vector3(Random.Range(-4f, 4f), Random.Range(1f, 6f), 0f),
                               Random.Range(0.25f, 0.4f), Random.Range(0.5f, 0.9f), GamblerJoker.Colours[i % GamblerJoker.Colours.Length]);
        End();
    }

    void Sink() {
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);
        if (!impacted) RaiseImpact(p);
        VFX.Splash(p, 0.5f);
        SFX.Play(SoundId.Splash, p, 0.45f, 1.3f);
        End();
    }

    protected override void OnLost() { End(); }

    void End() {
        Finish(true);
        PassCamera(troupe);
    }
}

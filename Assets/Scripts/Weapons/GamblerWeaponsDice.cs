using System.Collections;
using UnityEngine;

// Loaded Dice (self-cast; the aim means nothing). Two dice pop out of the Gambler's hand, fly up over his chamber
// tumbling, come down above his commander, bounce once and settle showing the roll (decided when he casts). Both 3 or
// more: "LUCKY!" and a lucky streak (GamblerLuck) for his other three weapons; anything else: no luck. The dice linger a
// moment, then shrink away. Animated by hand, no physics: they come to rest a little above the launch point and just in
// front of the blocks, where nothing can be in their way.
public class GamblerDice : Projectile {
    const float Size = 0.55f;                          // edge of a die (m)
    const float PopTime = 0.18f;                       // growing out of the hand
    const float TossTime = 1.05f, SecondLater = 0.07f; // hand to touchdown; the second die lands a moment later
    const float ArcMin = 2.3f, ArcMax = 2.8f;          // how high the toss rises over the line from hand to touchdown
    const float BounceTime = 0.3f, BounceHeight = 0.3f, BounceDrift = 0.12f;
    const float Spread = 0.38f;                        // they rest this far either side of the commander...
    const float RestHeight = 0.1f;                     // ...their bottoms this far above the launch point
    const float Stagger = 0.7f;                        // the left die flies and rests nearer the camera, so the two never meet
    const float VerdictDelay = 0.1f, LingerTime = 1f, ShrinkTime = 0.35f;
    static readonly Color NoLuckColor = new Color(0.55f, 0.62f, 0.75f);   // a dull grey-blue

    // One die: from the hand to its touchdown on an arc, tumbling, then one bounce out to where it rests, rolling over
    // to show its value.
    class Die {
        public Transform body;
        public Vector3 start, touch, rest;
        public float landAt, arc;
        public Vector3 axis;
        public float spin;                             // degrees per second about axis
        public Quaternion tumble0, touchRotation, final;
    }

    int valueA, valueB;                                // the left die and the right one
    Commander commander;
    Die[] dice;
    Vector3 table;                                     // between the two dice at rest
    float t, verdictAt, shrinkAt;
    bool landed, judged, fell;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        int a = Random.Range(1, 7), b = Random.Range(1, 7);
        GamblerSounds.Play(GamblerSounds.DiceRattle, origin, 0.9f);
        WeaponFactory.Spawn<GamblerDice>("Loaded Dice", owner, def, origin, Vector3.zero, d => { d.valueA = a; d.valueB = b; });
    }

    // The rule: both dice show 3 or more.
    public static bool Lucky(int a, int b) { return a >= 3 && b >= 3; }

    // The weapon-bar icon: a 3 and a 5 side by side, tipped back a little so three faces of each show.
    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Loaded Dice", parent);
        Transform three = GamblerKit.Die(root.transform, 0.5f);
        three.localPosition = new Vector3(-0.31f, -0.03f, 0f);
        three.localRotation = Quaternion.Euler(-20f, 8f, 3f) * GamblerKit.DieFacing(3);
        Transform five = GamblerKit.Die(root.transform, 0.5f);
        five.localPosition = new Vector3(0.31f, 0.03f, 0.05f);
        five.localRotation = Quaternion.Euler(-16f, -6f, -4f) * GamblerKit.DieFacing(5);
        return root;
    }

    protected override void OnLaunch(Vector3 velocity) {
        maxLifetime = 6f;
        commander = owner != null ? owner.commander : null;
        Vector3 hand = transform.position;
        float x = commander != null ? commander.transform.position.x : hand.x;
        float y = hand.y + RestHeight + Size * 0.5f;
        float z = -(World.BlockDepth * 0.5f + Size * 0.5f + 0.05f) - Stagger * 0.5f;   // both in front of the blocks
        table = new Vector3(x, y, z);
        transform.position = table + Vector3.up * 1.2f;               // the camera watches the whole toss from here
        dice = new[] { NewDie(hand, -1f, valueA, TossTime), NewDie(hand, 1f, valueB, TossTime + SecondLater) };
        verdictAt = TossTime + SecondLater + BounceTime + VerdictDelay;
        shrinkAt = verdictAt + LingerTime;
        foreach (Die d in dice) Pose(d);
    }

    // side -1 = the left die, +1 = the right one.
    Die NewDie(Vector3 hand, float side, int value, float landAt) {
        Vector3 rest = table + new Vector3(side * Spread, 0f, side * Stagger * 0.5f);
        Die d = new Die {
            body = GamblerKit.Die(transform, Size),
            start = hand + new Vector3(side * 0.15f, 0f, side * Stagger * 0.5f),
            touch = rest - new Vector3(side * BounceDrift, 0f, 0f),
            rest = rest,
            landAt = landAt,
            arc = Random.Range(ArcMin, ArcMax),
            axis = Random.onUnitSphere,
            spin = Random.Range(540f, 860f),
            tumble0 = Random.rotation,
            final = Quaternion.Euler(0f, Random.Range(-16f, 16f), 0f) * GamblerKit.DieFacing(value),   // a little askew, as dice lie
        };
        d.touchRotation = Quaternion.AngleAxis(d.spin * landAt, d.axis) * d.tumble0;
        return d;
    }

    void Update() {
        if (finished) return;
        t += Time.deltaTime;
        if (!fell && (commander == null || !commander.IsAlive)) {   // he fell: no verdict, the dice just go
            fell = true;
            shrinkAt = Mathf.Min(shrinkAt, t);
        }
        if (!landed && t >= TossTime) {
            landed = true;
            if (!fell) GamblerSounds.Play(GamblerSounds.DiceLand, table, 0.9f);
        }
        if (!judged && !fell && t >= verdictAt) Verdict();
        foreach (Die d in dice) Pose(d);
        if (t >= shrinkAt + ShrinkTime) Vanish();
    }

    // Where a die is and how it is turned at time t.
    void Pose(Die d) {
        Vector3 p;
        Quaternion r;
        if (t < d.landAt) {
            float k = t / d.landAt;
            p = Vector3.Lerp(d.start, d.touch, k) + Vector3.up * (4f * d.arc * k * (1f - k));
            r = Quaternion.AngleAxis(d.spin * t, d.axis) * d.tumble0;
        } else {
            float k = Mathf.Clamp01((t - d.landAt) / BounceTime);
            p = Vector3.Lerp(d.touch, d.rest, k) + Vector3.up * (4f * BounceHeight * k * (1f - k));
            r = Quaternion.Slerp(d.touchRotation, d.final, 1f - (1f - k) * (1f - k));
        }
        float pop = Mathf.Clamp01(t / PopTime);
        float s = Mathf.Lerp(0.3f, 1f, pop * pop * (3f - 2f * pop));
        if (t > shrinkAt) {                                // a little swell, then gone with a twist
            float k = Mathf.Clamp01((t - shrinkAt) / ShrinkTime);
            s *= (1f - k * k) * (1f + 0.6f * k);
            r = Quaternion.AngleAxis(150f * k * k, Vector3.up) * r;
        }
        d.body.SetPositionAndRotation(p, r);
        d.body.localScale = Vector3.one * s;
    }

    void Verdict() {
        judged = true;
        Vector3 above = table + Vector3.up * 1.3f;
        string roll = valueA + " + " + valueB;
        if (Lucky(valueA, valueB)) {
            GamblerKit.Announce(above, "LUCKY! " + roll, GamblerKit.Gold);
            GamblerKit.Sparkle(table, 1f);
            GamblerSounds.Play(GamblerSounds.Lucky, table, 1f);
            GamblerLuck.Grant(owner);
        } else {
            GamblerKit.Announce(above, "No luck: " + roll, NoLuckColor);
            GamblerSounds.Play(GamblerSounds.NoLuck, table, 0.9f);
        }
    }

    // A puff of glitter where each die was, and the cast is over.
    void Vanish() {
        foreach (Die d in dice) {
            Vector3 at = GamblerKit.Front(d.body.position);
            for (int i = 0; i < 5; i++)
                FxLayers.Star.Emit(at, (Vector3)(Random.insideUnitCircle * 2.2f) + Vector3.up * 0.6f, Random.Range(0.14f, 0.24f),
                                   Random.Range(0.35f, 0.6f), Color.Lerp(GamblerKit.Ivory, GamblerKit.Gold, Random.value));
        }
        Finish(true);
    }

    protected override void OnLost() { Vanish(); }
}

// A lucky streak (the dice came up 3 or more twice): the Gambler's other three weapons hit harder (Damage) and wider
// (Radius: blast radii and pierce budgets) on his next Turns turns. Rolling lucky again starts the streak over; it never
// stacks. Shown as "Lucky N" on his fortress card; while it runs gold stars drift up around his commander, and it ends
// in a little gold burst.
public class GamblerLuck : TurnEffect {
    public const float DamageBoost = 1.5f, RadiusBoost = 1.3f;
    public const int Turns = 3;
    const float TwinkleEvery = 0.4f;

    int ownerTurns;   // owner turns begun since the roll (the roll's own turn is 0)
    bool over;
    float twinkleIn;

    public override string StatusText => over ? null : "Lucky " + Mathf.Clamp(Turns + 1 - Mathf.Max(1, ownerTurns), 1, Turns);
    public override Color StatusColor => GamblerKit.Gold;
    public override int RewindState { get => ownerTurns; set => ownerTurns = value; }

    // The streak `f` is on, if any.
    public static GamblerLuck Of(Fortress f) {
        if (f == null) return null;
        foreach (TurnEffect e in All)
            if (e is GamblerLuck l && l.owner == f && !l.over) return l;
        return null;
    }

    public static float Damage(Fortress f) { return Of(f) != null ? DamageBoost : 1f; }
    public static float Radius(Fortress f) { return Of(f) != null ? RadiusBoost : 1f; }

    // Starts a streak for `f`, or starts its running one over.
    public static GamblerLuck Grant(Fortress f) {
        GamblerLuck l = Of(f);
        if (l != null) { l.ownerTurns = 0; return l; }
        GameObject go = new GameObject("Lucky Streak");
        l = go.AddComponent<GamblerLuck>();
        l.Init(f, f);
        return l;
    }

    // A few gold stars drift up around the lucky commander (none while he is down).
    void Update() {
        if (over || owner == null || !owner.CommanderAlive) return;
        twinkleIn -= Time.deltaTime;
        if (twinkleIn > 0f) return;
        twinkleIn = TwinkleEvery;
        Vector3 c = owner.commander.AimPoint;
        int n = Random.Range(2, 4);
        for (int i = 0; i < n; i++) {
            Vector3 at = GamblerKit.Front(c + new Vector3(Random.Range(-0.75f, 0.75f), Random.Range(-0.7f, 0.8f), 0f));
            FxLayers.Star.Emit(at, new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(1.4f, 2.4f), 0f), Random.Range(0.2f, 0.32f),
                               Random.Range(0.9f, 1.3f), GamblerKit.Gold, Random.Range(-90f, 90f));
        }
    }

    public override IEnumerator OnTurnStart(Fortress shooter) {
        if (shooter == owner && !over) {
            ownerTurns++;
            if (ownerTurns > Turns) {
                if (owner.CommanderAlive) GamblerKit.Sparkle(owner.commander.AimPoint, 0.6f);   // the luck runs out
                End();
            }
        }
        yield break;
    }

    void End() {
        over = true;
        Destroy(gameObject);
    }

    public override void Cancel() { End(); }
}

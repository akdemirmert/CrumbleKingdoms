using System.Collections;
using UnityEngine;

// Deck of Fate: the Gambler throws the card he holds (a human picked it face down in the HUD before aiming, a bot draws
// it as it throws) and what flies depends on the card. Everybody sees which card it is: it pops up big and face up over
// his head, under its name and what it does.
//   2-10            GamblerRazorCards: that many razor cards in a tight fan; each cuts through a wall like a fang
//   Jack            GamblerJack: a jack-in-the-box; where it lands it cranks its tune, pops its jester and blows up
//   Queen           GamblerQueen: the Queen's heart; past the top of its arc it seeks the enemy commander through walls
//   King, Ace, Joker  GamblerWeaponsFaces.cs
// A lucky streak from the dice (GamblerLuck) is read once, at the throw, and multiplies the damage and the reach.
public static class GamblerDeck {
    const float RevealAbove = 2.9f;   // the revealed card's centre over the thrower's feet: 1.4 m tall, it clears his head
    const float TextAbove = 5.1f;     // its name and effect float over the card (two lines of big text)
    const float IconCard = 0.9f;      // card height in the weapon icon

    // Throws the card the Gambler holds (a human picked it face down before aiming; a bot draws now).
    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        PlayingCard card = GamblerHand.Of(owner).TakeCard();
        Announce(owner, card, origin);
        switch (card.rank) {
            case CardRank.Jack: GamblerJack.Fire(owner, def, origin, velocity, card); break;
            case CardRank.Queen: GamblerQueen.Fire(owner, def, origin, velocity, card); break;
            case CardRank.King: GamblerKing.Fire(owner, def, origin, velocity, card); break;
            case CardRank.Ace: GamblerAce.Fire(owner, def, origin, velocity, card); break;
            case CardRank.Joker: GamblerJoker.Fire(owner, def, origin, velocity, card); break;
            default: GamblerRazorCards.Fire(owner, def, origin, velocity, card); break;
        }
    }

    // Shows everybody which card it is: the card itself over the thrower's head, its name and what it does above that.
    static void Announce(Fortress owner, PlayingCard card, Vector3 origin) {
        Vector3 feet = owner != null && owner.CommanderAlive ? owner.commander.transform.position : origin;
        GamblerDeckReveal.Show(card, feet + Vector3.up * RevealAbove);
        GamblerKit.Announce(feet + Vector3.up * TextAbove, card.Name.ToUpperInvariant() + "\n" + card.Effect,
                            card.IsRed ? GamblerKit.CardRed : Color.white, 1.1f);
    }

    // Weapon icon: a hand of three cards fanned out from where they are held.
    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Deck of Fate", parent);
        Transform hand = ModelKit.Root("Hand", root.transform).transform;
        hand.localRotation = Quaternion.Euler(0f, 0f, -30f);   // the icon stage rolls every model 38 degrees: this keeps the hand upright
        PlayingCard[] cards = {
            new PlayingCard(CardRank.Seven, CardSuit.Clubs), new PlayingCard(CardRank.King, CardSuit.Hearts), new PlayingCard(CardRank.Ace, CardSuit.Spades),
        };
        for (int i = 0; i < cards.Length; i++) {
            Transform fan = ModelKit.Root("Fan", hand).transform;
            fan.localPosition = new Vector3(0f, 0f, -0.03f * i);           // each card lies on the one before it
            fan.localRotation = Quaternion.Euler(0f, 0f, (1 - i) * 20f);
            GamblerKit.Card(fan, cards[i], IconCard).transform.localPosition = new Vector3(0f, IconCard * 0.42f, 0f);
        }
        return root;
    }
}

// The card just drawn, shown to everybody over the thrower's head: it pops up turning from its back to its face, holds
// for a second and shrinks away. Only a show: it does not hold up the turn.
public class GamblerDeckReveal : MonoBehaviour {
    const float Height = 1.4f;
    const float FlipTime = 0.3f, HoldTime = 1f, ShrinkTime = 0.3f;
    const float Rise = 0.35f;
    const float Z = -2.2f;            // in front of the walls and the commander

    Vector3 start;
    float t;

    public static void Show(PlayingCard card, Vector3 at) {
        at.z = Z;
        GameObject go = ModelKit.Root("Card Reveal");
        go.transform.position = at;
        GamblerKit.Card(go.transform, card, Height);
        ModelKit.Shadows(go, false, false);
        GamblerDeckReveal reveal = go.AddComponent<GamblerDeckReveal>();
        reveal.start = at;
        reveal.Apply();
        GamblerSounds.Play(GamblerSounds.CardFlip, at, 0.9f);
        GamblerKit.Sparkle(at, 0.45f, false);
    }

    // Unscaled time, like the floating text above it: a slow-motion moment does not stretch the show.
    void LateUpdate() {
        t += Time.unscaledDeltaTime;
        if (t >= FlipTime + HoldTime + ShrinkTime) { Destroy(gameObject); return; }
        Apply();
    }

    void Apply() {
        float k = Mathf.Clamp01(t / FlipTime);
        float scale = k < 1f ? BackOut(k) : 1f - Mathf.SmoothStep(0f, 1f, (t - FlipTime - HoldTime) / ShrinkTime);
        float flip = 180f * (1f - Mathf.SmoothStep(0f, 1f, k));        // from its red back to its face
        float u = t / (FlipTime + HoldTime + ShrinkTime);
        float rise = Rise * (1f - (1f - u) * (1f - u));
        float sway = Mathf.Sin(t * 3f) * 4f;
        Camera cam = Camera.main;
        Quaternion face = cam != null ? cam.transform.rotation : Quaternion.identity;
        transform.SetPositionAndRotation(start + Vector3.up * rise, face * Quaternion.Euler(0f, flip, sway));
        transform.localScale = Vector3.one * Mathf.Max(0.001f, scale);
    }

    // Ease-out with a little overshoot: 0, up to about 1.1, back to 1.
    static float BackOut(float k) {
        const float c = 1.70158f;
        float m = k - 1f;
        return 1f + (c + 1f) * m * m * m + c * m * m;
    }
}

// ====================================================================== 2-10: razor cards
// Number cards: card.Count razor cards thrown at once in a tight fan around the aim (ten span about +-3 degrees, fewer
// cards less). Each one pierces (GamblerRazorCard) like the vampire's fang: up to five wooden blocks, three stone, one iron.
public static class GamblerRazorCards {
    const float FanStep = 0.6f;            // degrees between neighbouring cards
    const float SpeedJitter = 0.03f;
    const float CommanderDamage = 6f;
    const float SweepRadius = 0.1f;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity, PlayingCard card) {
        float damage = def.damage * GamblerLuck.Damage(owner);
        float commanderDamage = CommanderDamage * GamblerLuck.Damage(owner);
        float budget = def.pierce * GamblerLuck.Radius(owner);
        int n = Mathf.Max(1, card.Count);
        GamblerSounds.Play(GamblerSounds.CardFlick, origin, 0.9f);
        for (int i = 0; i < n; i++) {
            float angle = (i - (n - 1) * 0.5f) * FanStep;
            Vector3 v = Quaternion.Euler(0f, 0f, angle) * velocity * Random.Range(1f - SpeedJitter, 1f + SpeedJitter);
            WeaponFactory.Spawn<GamblerRazorCard>("Razor Card", owner, def, origin, v, c => {
                c.card = card;
                c.budget = budget;
                c.damage = damage;
                c.commanderDamage = commanderDamage;
                c.sweepRadius = SweepRadius;
            });
        }
    }
}

// One razor card: a small face-up card spinning in its own plane like a blade. It cuts through what its budget allows,
// sticks edge first in what stops it, and fades out a few seconds later.
public class GamblerRazorCard : PiercingProjectile {
    const float Height = 0.32f;
    const float SpinSpeed = 1500f;             // degrees per second
    const float FadeDelay = 3f, FadeTime = 0.4f;

    public PlayingCard card;

    Transform model;
    GameObject trail;
    float spin, stuckFor;

    protected override void OnLaunch(Vector3 velocity) {
        base.OnLaunch(velocity);
        embedDepth = 0f;                       // the model is set back on stopping instead (see OnStopped)
        model = GamblerKit.Card(transform, card, Height).transform;
        spin = Random.Range(0f, 360f);
        trail = VFX.Trail(transform, new Color(1f, 0.9f, 0.95f, 0.45f), 0.07f, 0.1f);
        Stopped += OnStopped;
    }

    void Update() {
        if (model == null) return;
        if (!IsStuck) {
            spin -= travelDir * SpinSpeed * Time.deltaTime;
            model.localRotation = Quaternion.Euler(0f, 0f, spin);
            return;
        }
        stuckFor += Time.deltaTime;
        float k = (stuckFor - FadeDelay) / FadeTime;
        if (k <= 0f) return;
        model.localScale = Vector3.one * Mathf.Max(0f, 1f - k);
        if (k >= 1f) Destroy(gameObject);
    }

    // It stops spinning and sticks top edge first along its flight, about a third of it in whatever stopped it.
    void OnStopped(PiercingProjectile self, Vector3 point, Collider hit) {
        GodKit.Release(trail);
        model.localRotation = Quaternion.Euler(0f, 0f, -90f);
        model.localPosition = new Vector3(-Height * 0.2f, 0f, 0f);
    }
}

// ====================================================================== Jack: the jack-in-the-box
// Jack: a jack-in-the-box flies along the aim. Where it first touches something of the enemy's (a block, a commander) or
// the ground, it stops dead, rights itself and its crank turns out a little tune; then the lid flies open, the jester
// springs out on his coil and the whole box goes off: one blast, even if the block under it has broken by then. In the
// sea it just sinks.
public static class GamblerJack {
    const float BlastRadius = 3.6f, BlastDamage = 85f, BlastForce = 12f;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity, PlayingCard card) {
        float radius = BlastRadius * GamblerLuck.Radius(owner), damage = BlastDamage * GamblerLuck.Damage(owner);
        WeaponFactory.Spawn<GamblerJackBox>("Jack-in-the-box", owner, def, origin, velocity, b => {
            b.card = card;
            b.blastRadius = radius;
            b.blastDamage = damage;
            b.blastForce = BlastForce;
        });
    }
}

public class GamblerJackBox : Projectile {
    const float Size = 0.6f, Edge = 0.06f, Mass = 2f;
    const float Spin = 3f;                                       // rad/s: a lazy tumble in flight
    const float SettleTime = 0.15f, CrankTime = 1f, PopTime = 0.45f;
    const float CrankTurns = 3f;
    const float LidOpen = 115f;                                  // degrees the lid swings back
    const float SpringBase = 0.15f, SpringLength = 0.75f, SpringSquashed = 0.08f;
    const int SpringZigs = 9;

    static readonly Color[] ConfettiColors = {
        GamblerKit.CardRed, GamblerKit.Gold, GamblerKit.Neon, Color.white, new Color(0.25f, 0.55f, 1f), new Color(0.3f, 0.85f, 0.4f),
    };

    public PlayingCard card;
    public float blastRadius, blastDamage, blastForce;

    Transform model, lid, crank, spring, jester;
    bool planted;

    protected override void OnLaunch(Vector3 velocity) {
        Build();
        gameObject.AddComponent<BoxCollider>().size = Vector3.one * Size;
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        rb.angularVelocity = new Vector3(0f, 0f, -travelDir * Spin);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (!finished && !planted && transform.position.y < World.WaterY) Sink();
    }

    void OnCollisionEnter(Collision c) {
        if (finished || planted || IsOwnTeam(c.collider)) return;
        SoldierWeapons.FirstContact(c, transform.position, out Vector3 point, out _);
        SoldierWeapons.LimitShove(c);
        if (transform.position.y < World.WaterY) Sink();       // the sea bed
        else Plant(point);
    }

    // Lost on the way (out of the arena): no show. Its time ran out mid-show: it goes off where it is.
    protected override void OnLost() {
        if (planted) Blow();
        else Finish(true);
    }

    void Plant(Vector3 contact) {
        planted = true;
        RaiseImpact(contact);
        LichKit.Freeze(this);
        maxLifetime = age + SettleTime + CrankTime + PopTime + 1f;   // whatever happens, the show ends
        StartCoroutine(Show());
    }

    IEnumerator Show() {
        Vector3 at = transform.position;
        Quaternion tilt = transform.rotation;
        for (float t = 0f; t < SettleTime; t += Time.deltaTime) {    // it rights itself, lid up
            transform.rotation = Quaternion.Slerp(tilt, Quaternion.identity, t / SettleTime);
            yield return null;
        }
        transform.rotation = Quaternion.identity;

        GamblerSounds.Play(GamblerSounds.Crank, at, 0.9f);
        for (float t = 0f; t < CrankTime; t += Time.deltaTime) {
            float k = t / CrankTime;
            crank.localRotation = Quaternion.Euler(-360f * CrankTurns * k, 0f, 0f);
            float shake = Mathf.Sin(t * 45f) * 0.05f * k;               // it trembles more and more
            model.localScale = new Vector3(1f + shake, 1f - shake, 1f);
            yield return null;
        }
        model.localScale = Vector3.one;

        GamblerSounds.Play(GamblerSounds.Boing, at, 1f);
        GamblerKit.Sparkle(at + Vector3.up * 0.6f, 0.5f, false);
        spring.gameObject.SetActive(true);
        jester.gameObject.SetActive(true);
        for (float t = 0f; t < PopTime; t += Time.deltaTime) {
            float k = t / PopTime;
            lid.localRotation = Quaternion.Euler(0f, 0f, LidOpen * Mathf.Clamp01(k * 5f));
            // Out it shoots, overshoots and wobbles on the coil.
            SetSpring(1f - (1f - SpringSquashed) * Mathf.Cos(k * Mathf.PI * 3.5f) * Mathf.Exp(-k * 4f));
            jester.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 22f) * 14f * (1f - k));
            yield return null;
        }
        Blow();
    }

    void Blow() {
        if (finished) return;
        Vector3 pos = transform.position;
        Explosion.Create(pos, blastRadius, blastDamage, blastForce, owner, DamageType.Explosion, BlastStyle.Fire);
        Vector3 at = GamblerKit.Front(pos + Vector3.up * 0.4f);
        foreach (Color c in ConfettiColors) VFX.Burst(at, 9, 9f, 0.16f, 1.4f, c, 0.5f, false, TexKit.White);
        Finish(true);
    }

    void Sink() {
        Vector3 pos = transform.position;
        if (!impacted) RaiseImpact(pos);
        VFX.Splash(new Vector3(pos.x, World.WaterY, 0f), 0.8f);
        SFX.Play(SoundId.Splash, pos, 0.6f);
        Finish(true);
    }

    // The jester's coil is squashed to `extension` of its length (1 = fully out); his head rides on its top.
    void SetSpring(float extension) {
        spring.localScale = new Vector3(1f, extension, 1f);
        jester.localPosition = new Vector3(0f, SpringBase + SpringLength * extension, 0f);
    }

    // ------------------------------------------------------------------ model
    // A red toy box with gold edges and the card's suit on a medallion in front, a crank on its right side and a lid
    // hinged on its left; inside, the coil and the jester (hidden until the lid pops). Centred, sitting upright.
    void Build() {
        model = ModelKit.Root("Jack-in-the-box", transform).transform;
        float h = Size * 0.5f;
        ModelKit.Part(model, PrimitiveType.Cube, Vector3.zero, Vector3.one * Size, GamblerKit.RedInk);
        for (int axis = 0; axis < 3; axis++)
            for (int i = 0; i < 4; i++) {                                   // the twelve gold edges
                Vector3 p = Vector3.zero, s = Vector3.one * Edge;
                p[(axis + 1) % 3] = (i & 1) == 0 ? -h : h;
                p[(axis + 2) % 3] = (i & 2) == 0 ? -h : h;
                s[axis] = Size + Edge;
                ModelKit.Part(model, PrimitiveType.Cube, p, s, GamblerKit.GoldMat);
            }
        ModelKit.Part(model, PrimitiveType.Cube, new Vector3(0f, h + 0.002f, 0f), new Vector3(Size - Edge * 2f, 0.004f, Size - Edge * 2f), GamblerKit.BlackInk);   // the opening
        ModelKit.Part(model, PrimitiveType.Cylinder, new Vector3(0f, 0f, -h - 0.002f), new Vector3(0.42f, 0.008f, 0.42f), GamblerKit.GoldMat, new Vector3(90f, 0f, 0f));
        ModelKit.Part(model, PrimitiveType.Cylinder, new Vector3(0f, 0f, -h - 0.008f), new Vector3(0.36f, 0.008f, 0.36f), GamblerKit.PaperMat, new Vector3(90f, 0f, 0f));
        GamblerKit.Suit(model, card.suit, new Vector3(0f, 0f, -h - 0.035f), 0.24f, GamblerKit.InkMat(card));

        lid = ModelKit.Root("Lid", model).transform;
        lid.localPosition = new Vector3(-h - Edge * 0.5f, h + Edge * 0.5f, 0f);
        ModelKit.Part(lid, PrimitiveType.Cube, new Vector3(h + Edge * 0.5f, 0.025f, 0f), new Vector3(Size + Edge, 0.05f, Size + Edge), GamblerKit.GoldMat);
        ModelKit.Part(lid, PrimitiveType.Cube, new Vector3(h + Edge * 0.5f, 0.03f, 0f), new Vector3(Size - Edge, 0.052f, Size - Edge), GamblerKit.RedInk);

        ModelKit.Part(model, PrimitiveType.Cylinder, new Vector3(h + 0.04f, 0f, 0f), Vector3.one * 0.045f, GamblerKit.GoldMat, new Vector3(0f, 0f, 90f));   // axle
        crank = ModelKit.Root("Crank", model).transform;
        crank.localPosition = new Vector3(h + 0.08f, 0f, 0f);
        ModelKit.Part(crank, PrimitiveType.Cube, new Vector3(0.015f, -0.07f, 0f), new Vector3(0.03f, 0.17f, 0.04f), GamblerKit.GoldMat);
        ModelKit.Part(crank, PrimitiveType.Cylinder, new Vector3(0.05f, -0.14f, 0f), new Vector3(0.05f, 0.035f, 0.05f), GamblerKit.IvoryMat, new Vector3(0f, 0f, 90f));

        // The coil, as a side view draws one: a zigzag of steel wire.
        spring = ModelKit.Root("Spring", model).transform;
        spring.localPosition = new Vector3(0f, SpringBase, 0f);
        Material steel = Mats.Lit(new Color(0.72f, 0.74f, 0.78f), 0.75f, 0.85f);
        for (int i = 0; i < SpringZigs; i++) {
            float side = i % 2 == 0 ? -0.1f : 0.1f;
            Rod(spring, new Vector3(side, SpringLength * i / SpringZigs, 0f), new Vector3(-side, SpringLength * (i + 1) / SpringZigs, 0f), 0.03f, steel);
        }

        // The jester: a white face with a red nose and grin on a gold ruff, a two-pointed cap with a bell on each point.
        jester = ModelKit.Root("Jester", model).transform;
        ModelKit.Part(jester, PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0f), new Vector3(0.38f, 0.09f, 0.32f), GamblerKit.GoldMat);
        ModelKit.Part(jester, PrimitiveType.Sphere, new Vector3(0f, 0.18f, 0f), Vector3.one * 0.32f, GamblerKit.PaperMat);
        ModelKit.Part(jester, PrimitiveType.Sphere, new Vector3(-0.06f, 0.22f, -0.14f), Vector3.one * 0.045f, GamblerKit.BlackInk);
        ModelKit.Part(jester, PrimitiveType.Sphere, new Vector3(0.06f, 0.22f, -0.14f), Vector3.one * 0.045f, GamblerKit.BlackInk);
        ModelKit.Part(jester, PrimitiveType.Sphere, new Vector3(0f, 0.17f, -0.16f), Vector3.one * 0.075f, GamblerKit.RedInk);
        ModelKit.Part(jester, PrimitiveType.Sphere, new Vector3(0f, 0.1f, -0.13f), new Vector3(0.14f, 0.035f, 0.04f), GamblerKit.RedInk);
        Mesh point = MeshKit.Cone(6, 0.09f, 0.3f);
        for (int s = -1; s <= 1; s += 2) {
            Vector3 root = new Vector3(s * 0.07f, 0.29f, 0f);
            Vector3 lean = new Vector3(0f, 0f, -s * 40f);
            ModelKit.MeshPart(jester, point, root, Vector3.one, s < 0 ? GamblerKit.RedInk : GamblerKit.BlackInk, lean, "Cap");
            ModelKit.Part(jester, PrimitiveType.Sphere, root + Quaternion.Euler(lean) * new Vector3(0f, 0.3f, 0f), Vector3.one * 0.07f, GamblerKit.GoldMat);
        }
        ModelKit.Part(jester, PrimitiveType.Cylinder, new Vector3(0f, 0.29f, 0f), new Vector3(0.3f, 0.025f, 0.3f), GamblerKit.GoldMat);

        SetSpring(SpringSquashed);
        spring.gameObject.SetActive(false);
        jester.gameObject.SetActive(false);
    }

    static void Rod(Transform parent, Vector3 a, Vector3 b, float thickness, Material m) {
        Vector3 d = b - a;
        ModelKit.Part(parent, PrimitiveType.Cylinder, (a + b) * 0.5f, new Vector3(thickness, d.magnitude * 0.5f, thickness), m,
                      Quaternion.FromToRotation(Vector3.up, d).eulerAngles);   // Unity's cylinder is 2 m tall
    }
}

// ====================================================================== Queen: the Queen's heart
// Queen: the Queen's heart. A glowing red heart flies along the aim; past the top of its arc it picks the enemy commander
// its throw is heading for and seeks him (GamblerQueenHeart), passing through every wall and nicking each block on the
// way, until it strikes him and bursts into little hearts. With no living enemy commander it simply flies on.
public static class GamblerQueen {
    const float BlockDamage = 6f, CommanderDamage = 30f, Budget = 99f;
    const float TurnRate = 110f;                                 // degrees per second

    public static readonly Color Pink = new Color(1f, 0.45f, 0.65f);
    public static Material HeartMat => Mats.Emissive(new Color(0.95f, 0.12f, 0.24f), new Color(3.4f, 0.35f, 0.7f));

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity, PlayingCard card) {
        float boost = GamblerLuck.Damage(owner), budget = Budget * GamblerLuck.Radius(owner);
        SFX.Play(SoundId.MagicCast, origin, 0.45f, 1.5f);
        WeaponFactory.Spawn<GamblerQueenHeart>("Queen's Heart", owner, def, origin, velocity, h => {
            h.budget = budget;
            h.damage = BlockDamage * boost;
            h.commanderDamage = CommanderDamage * boost;
            h.homingTurnRate = TurnRate;
            h.homingAfterApex = true;
            h.passThroughCommanders = false;
            h.sweepRadius = 0.22f;
            h.slowPerCost = 0.04f;                               // walls barely slow it
            h.stopSpeed = 3f;
        });
    }
}

public class GamblerQueenHeart : PiercingProjectile {
    const float Size = 0.6f;
    const float SparkleEvery = 0.03f;                            // seconds between trail sparkles

    Commander target;
    Transform mark;          // follows the target's chest: the homing aims there, not at his feet
    Transform heart;
    bool lockedOn;
    float sparkle;

    protected override void OnLaunch(Vector3 velocity) {
        base.OnLaunch(velocity);
        heart = ModelKit.Root("Heart", transform).transform;
        heart.localRotation = Quaternion.Euler(0f, 0f, 90f);    // its point leads
        GamblerKit.Suit(heart, CardSuit.Hearts, Vector3.zero, Size, GamblerQueen.HeartMat);
        VFX.Trail(transform, new Color(1.6f, 0.3f, 0.55f, 0.8f), 0.22f, 0.35f);
        GodKit.Glow(transform, GamblerQueen.Pink, 2.5f, 4f);
        Stopped += OnStopped;
    }

    protected override void FixedUpdate() {
        if (!finished && !IsStuck) {
            if (!lockedOn && manualVelocity.y < 0f) LockOn();          // past the top of its arc
            if (mark != null) {
                if (target != null && target.IsAlive) mark.position = target.AimPoint;
                else DropTarget();                                    // he fell to something else: it flies on
            }
        }
        base.FixedUpdate();
    }

    // It beats, and sheds pink and red sparkles behind it.
    void Update() {
        if (finished || heart == null) return;
        float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(age * 6f)), 8f);
        heart.localScale = Vector3.one * (1f + 0.15f * beat);
        sparkle += Time.deltaTime;
        while (sparkle >= SparkleEvery) {
            sparkle -= SparkleEvery;
            Vector3 p = GamblerKit.Front(transform.position + (Vector3)(Random.insideUnitCircle * 0.2f));
            FxLayers.Star.Emit(p, -Velocity * 0.05f + (Vector3)(Random.insideUnitCircle * 0.8f), Random.Range(0.1f, 0.2f),
                               Random.Range(0.35f, 0.6f), Color.Lerp(GamblerQueen.Pink, GamblerKit.CardRed, Random.value), Random.Range(-180f, 180f));
        }
    }

    void LockOn() {
        lockedOn = true;
        target = TargetAhead();
        if (target == null) return;                                   // no living enemy commander: it flies on
        mark = new GameObject("Queen's Heart mark").transform;
        mark.position = target.AimPoint;
        homingTarget = mark;
        VFX.Ring(target.AimPoint, 1.1f, GamblerQueen.Pink, 0.5f, 0.2f);
        SFX.Play(SoundId.MagicCast, transform.position, 0.5f, 1.7f);
    }

    // The living enemy commander nearest to where the throw is heading (its unguided arc), so in a 1vN battle the heart
    // goes to the fortress it was thrown at.
    Commander TargetAhead() {
        if (BattleManager.I == null || owner == null) return null;
        Commander best = null;
        float bestMiss = float.MaxValue;
        float wind = BattleManager.Wind * windFactor;
        foreach (Fortress f in BattleManager.I.EnemiesOf(owner)) {
            if (f == null || !f.CommanderAlive) continue;
            Vector3 aim = f.commander.AimPoint;
            float y = Ballistics.HeightAtX(transform.position, manualVelocity, gravityScale, wind, aim.x);
            float miss = float.IsNaN(y) ? 1000f + Vector3.Distance(transform.position, aim) : Mathf.Abs(y - aim.y);
            if (miss < bestMiss) { bestMiss = miss; best = f.commander; }
        }
        return best;
    }

    void DropTarget() {
        homingTarget = null;
        target = null;
        if (mark != null) Destroy(mark.gameObject);
        mark = null;
    }

    // It never sticks: on the commander it bursts into hearts and stars, anywhere else (the ground) into a few.
    void OnStopped(PiercingProjectile self, Vector3 point, Collider hit) {
        bool struck = hit != null && hit.GetComponentInParent<Commander>() != null;
        DropTarget();
        Burst(point, struck ? 1f : 0.5f);
        if (struck) SFX.Play(SoundId.MagicCast, point, 0.8f, 1.4f);
        Destroy(gameObject);
    }

    static void Burst(Vector3 at, float size) {
        VFX.MagicBurst(at, GamblerQueen.Pink, size);
        VFX.LightFlash(at, GamblerQueen.Pink, 6f * size, 6f * size, 0.4f);
        int n = Mathf.RoundToInt(8f * size);
        for (int i = 0; i < n; i++) {
            float a = (i + Random.value * 0.6f) / n * Mathf.PI * 2f;
            Vector3 v = new Vector3(Mathf.Cos(a), Mathf.Sin(a) + 0.5f, 0f) * Random.Range(2.5f, 4.5f) * size;
            GamblerQueenHeartBit.Spawn(at, v, Random.Range(0.18f, 0.3f));
        }
    }

    protected override void OnDestroy() {
        base.OnDestroy();
        if (mark != null) Destroy(mark.gameObject);
    }
}

// One of the little hearts the Queen's heart bursts into: it flies out, slows, floats up and shrinks away.
public class GamblerQueenHeartBit : MonoBehaviour {
    const float Life = 1f;

    Vector3 velocity;
    float t, spin;

    public static void Spawn(Vector3 at, Vector3 velocity, float size) {
        GameObject go = ModelKit.Root("Little Heart");
        go.transform.position = GamblerKit.Front(at) + Vector3.back * 0.1f;
        go.transform.localScale = Vector3.zero;
        GamblerKit.Suit(go.transform, CardSuit.Hearts, Vector3.zero, size, GamblerQueen.HeartMat);
        ModelKit.Shadows(go, false, false);
        GamblerQueenHeartBit bit = go.AddComponent<GamblerQueenHeartBit>();
        bit.velocity = velocity;
        bit.spin = Random.Range(-90f, 90f);
    }

    void Update() {
        float dt = Time.deltaTime;
        t += dt;
        if (t >= Life) { Destroy(gameObject); return; }
        velocity = Vector3.Lerp(velocity, Vector3.up * 1.5f, dt * 3f);
        transform.position += velocity * dt;
        transform.rotation = Quaternion.Euler(0f, 0f, spin * t + Mathf.Sin(t * 9f) * 15f);
        float k = t / Life;
        transform.localScale = Vector3.one * (k < 0.1f ? k / 0.1f : 1f - Mathf.SmoothStep(0f, 1f, (k - 0.5f) / 0.5f));
    }
}

using TMPro;
using UnityEngine;

// The Gambler's weapons: games of chance. Each weapon lives in its own file:
//   GamblerWeaponsDice.cs       Loaded Dice: two dice; both 3 or more = a lucky streak (GamblerLuck) for the other three
//   GamblerWeaponsDeck.cs       Deck of Fate: he picks a face-down card and throws what it is (number cards, Jack, Queen)
//   GamblerWeaponsFaces.cs      the King's sword, the Ace that falls like a blade and the Joker's exploding pins
//   GamblerWeaponsCoin.cs       Heads or Tails: a called coin; if the call comes up a giant coin crashes down where it lands
//   GamblerWeaponsRoulette.cs   Roulette Wheel: a spinning wheel saws through blocks, then its ball's pocket decides the blast
//   GamblerWeaponsSounds.cs     the class's own synthesised sounds
// Shared here: the playing cards (PlayingCard), what a Gambler holds between picking and throwing (GamblerHand), and the
// colours, materials, models (card, die, coin) and small effects his weapons, his look and his HUD share (GamblerKit).
// His look (commander, block skins, fortress decorations) is in World/GamblerLook*.cs; the card and coin pickers the
// human player answers before aiming are in UI/GamblerChoiceUI.cs.
static class GamblerWeapons {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() {
        WeaponFactory.Register(WeaponId.LuckyDice, GamblerDice.Fire, GamblerDice.Model);
        WeaponFactory.Register(WeaponId.DeckOfFate, GamblerDeck.Fire, GamblerDeck.Model);
        WeaponFactory.Register(WeaponId.CoinToss, GamblerCoin.Fire, GamblerCoin.Model);
        WeaponFactory.Register(WeaponId.Roulette, GamblerRoulette.Fire, GamblerRoulette.Model);
        HumanShotController.PendingChoice = GamblerHand.Prompt;
    }
}

public enum CardRank { Two = 2, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King, Ace, Joker }
public enum CardSuit { Spades, Hearts, Diamonds, Clubs }

// One card of a 54-card deck: 52 cards and two jokers (the black joker counts as Spades, the red one as Hearts).
public struct PlayingCard {
    public const int DeckSize = 54;
    public CardRank rank;
    public CardSuit suit;

    public PlayingCard(CardRank rank, CardSuit suit) { this.rank = rank; this.suit = suit; }

    // Deck index 0..53: 0..51 are Two..Ace, four suits each; 52 is the black joker, 53 the red one.
    public static PlayingCard FromIndex(int i) {
        i = Mathf.Clamp(i, 0, DeckSize - 1);
        if (i >= 52) return new PlayingCard(CardRank.Joker, i == 52 ? CardSuit.Spades : CardSuit.Hearts);
        return new PlayingCard((CardRank)(2 + i / 4), (CardSuit)(i % 4));
    }

    public int Index => rank == CardRank.Joker ? (IsRed ? 53 : 52) : ((int)rank - 2) * 4 + (int)suit;
    public bool IsNumber => rank <= CardRank.Ten;
    public bool IsRed => suit == CardSuit.Hearts || suit == CardSuit.Diamonds;
    public int Count => IsNumber ? (int)rank : 0;   // razor cards a number card throws
    public Color Ink => IsRed ? GamblerKit.CardRed : GamblerKit.CardBlack;

    // Corner index: "7", "10", "J", "Q", "K", "A", "JOKER".
    public string RankText {
        get {
            switch (rank) {
                case CardRank.Jack: return "J";
                case CardRank.Queen: return "Q";
                case CardRank.King: return "K";
                case CardRank.Ace: return "A";
                case CardRank.Joker: return "JOKER";
                default: return ((int)rank).ToString();
            }
        }
    }

    // "Seven of Clubs", "Red Joker".
    public string Name => rank == CardRank.Joker ? (IsRed ? "Red Joker" : "Black Joker") : rank + " of " + suit;

    // What it does when thrown, in a few words.
    public string Effect {
        get {
            switch (rank) {
                case CardRank.Jack: return "Jack-in-the-box!";
                case CardRank.Queen: return "The Queen's heart";
                case CardRank.King: return "The King's sword";
                case CardRank.Ace: return "The Ace falls!";
                case CardRank.Joker: return "The Joker juggles!";
                default: return Count + " razor cards";
            }
        }
    }

    public const int Kinds = 6;   // a number card (2-10), Jack, Queen, King, Ace, Joker

    // One card of each kind, shuffled: a number card of any rank, the four picture cards and aces in any suit, a joker
    // of either colour. Picking one of them face down gives every kind the same 1-in-6 chance.
    public static PlayingCard[] DealOneOfEach() {
        PlayingCard[] hand = {
            new PlayingCard((CardRank)UnityEngine.Random.Range((int)CardRank.Two, (int)CardRank.Ten + 1), AnySuit()),
            new PlayingCard(CardRank.Jack, AnySuit()), new PlayingCard(CardRank.Queen, AnySuit()),
            new PlayingCard(CardRank.King, AnySuit()), new PlayingCard(CardRank.Ace, AnySuit()),
            new PlayingCard(CardRank.Joker, UnityEngine.Random.value < 0.5f ? CardSuit.Spades : CardSuit.Hearts),
        };
        for (int i = hand.Length - 1; i > 0; i--) {
            int k = UnityEngine.Random.Range(0, i + 1);
            (hand[i], hand[k]) = (hand[k], hand[i]);
        }
        return hand;
    }

    // A card drawn the same way without the fan (a bot's throw): every kind at the same odds.
    public static PlayingCard DrawKind() { return DealOneOfEach()[UnityEngine.Random.Range(0, Kinds)]; }

    static CardSuit AnySuit() { return (CardSuit)UnityEngine.Random.Range(0, 4); }

    public override string ToString() { return Name; }
}

// What one Gambler fortress holds between choosing and throwing: the card he picked (face up until he throws it; switching
// weapons does not redraw it) and his heads-or-tails call. A human answers in the HUD (GamblerChoiceUI) before he can aim;
// a bot never does, so its card is drawn and its call made at random when it throws. Lives on the fortress object.
public class GamblerHand : MonoBehaviour {
    // Testing (AutoPlaytest bench): ForcedCard >= 0 makes every card thrown that deck index; ForcedFlip 0 makes every
    // coin land against the call, 1 for it (-1 = chance).
    public static int ForcedCard = -1, ForcedFlip = -1;

    public bool HasCard { get; private set; }
    public PlayingCard Card { get; private set; }
    public bool HasCall { get; private set; }
    public bool CallHeads { get; private set; }

    public static GamblerHand Of(Fortress f) {
        if (f == null) return null;
        GamblerHand h = f.GetComponent<GamblerHand>();
        return h != null ? h : f.gameObject.AddComponent<GamblerHand>();
    }

    // HumanShotController.PendingChoice: the question a human must answer before aiming this weapon (null = none).
    public static string Prompt(Fortress f, WeaponDef def) {
        if (f == null || def == null) return null;
        if (def.id == WeaponId.DeckOfFate && !Of(f).HasCard) return "Pick a card";
        if (def.id == WeaponId.CoinToss && !Of(f).HasCall) return "Call it: heads or tails?";
        return null;
    }

    public void Pick(PlayingCard card) { Card = card; HasCard = true; }
    public void Call(bool heads) { CallHeads = heads; HasCall = true; }

    // The card thrown now: the one picked, or a fresh draw (a bot: every kind at the same odds, as from the fan). It
    // leaves the hand.
    public PlayingCard TakeCard() {
        PlayingCard c = ForcedCard >= 0 ? PlayingCard.FromIndex(ForcedCard) : HasCard ? Card : PlayingCard.DrawKind();
        HasCard = false;
        return c;
    }

    // The call for the coin thrown now (a bot calls at random). It is used up.
    public bool TakeCall() {
        bool heads = HasCall ? CallHeads : UnityEngine.Random.value < 0.5f;
        HasCall = false;
        return heads;
    }

    // Whether a flipped coin comes up as called: an even chance (unless a test fixes it).
    public static bool FlipWins() { return ForcedFlip >= 0 ? ForcedFlip == 1 : UnityEngine.Random.value < 0.5f; }
}

// Colours, materials, models and small effects shared by the Gambler's weapons, his look and his HUD.
// Models face the camera (-Z), have no colliders, and are sized in metres.
public static class GamblerKit {
    public static readonly Color Neon = WeaponDefs.Hex("F0509A");              // the class colour: casino neon
    public static readonly Color NeonHdr = new Color(4f, 1.25f, 2.5f);          // its glow (above 1 blooms)
    public static readonly Color Felt = new Color(0.06f, 0.4f, 0.22f);          // card-table green
    public static readonly Color Paper = new Color(0.97f, 0.95f, 0.9f);         // card faces
    public static readonly Color CardRed = new Color(0.84f, 0.1f, 0.15f);
    public static readonly Color CardBlack = new Color(0.07f, 0.07f, 0.09f);
    public static readonly Color BackRed = new Color(0.62f, 0.07f, 0.12f);      // card backs
    public static readonly Color Gold = new Color(1f, 0.78f, 0.3f);
    public static readonly Color GoldHdr = new Color(3.4f, 2.4f, 0.8f);
    public static readonly Color Ivory = new Color(0.96f, 0.93f, 0.85f);        // dice

    public static Material PaperMat => Mats.Lit(Paper, 0.35f);
    public static Material BackMat => Mats.Lit(BackRed, 0.35f);
    public static Material RedInk => Mats.Lit(CardRed, 0.4f);
    public static Material BlackInk => Mats.Lit(CardBlack, 0.45f);
    public static Material GoldMat => Mats.Lit(Gold, 0.75f, 0.65f);
    public static Material GoldGlowMat => Mats.Emissive(Gold, GoldHdr);
    public static Material IvoryMat => Mats.Lit(Ivory, 0.55f);
    public static Material FeltMat => Mats.Lit(Felt, 0.1f);
    public static Material NeonMat => Mats.Emissive(Neon, NeonHdr);
    public static Material InkMat(PlayingCard c) { return c.IsRed ? RedInk : BlackInk; }

    // ------------------------------------------------------------------ playing card
    public const float CardAspect = 0.7f;   // width / height

    // A playing card `height` tall, its face towards the camera (faceUp) or its red back. Centred on the parent's origin,
    // in the XY plane. Number cards and aces show a big suit pip, picture cards their letter, jokers a star; every card
    // has its index in two corners.
    public static GameObject Card(Transform parent, PlayingCard card, float height, bool faceUp = true) {
        GameObject root = ModelKit.Root("Card " + card.Name, parent);
        Transform body = Pivot("Body", root.transform, Vector3.zero, faceUp ? Vector3.zero : new Vector3(0f, 180f, 0f));
        float w = height * CardAspect, t = Mathf.Max(0.004f, height * 0.016f);
        ModelKit.Part(body, PrimitiveType.Cube, new Vector3(0f, 0f, -t * 0.25f), new Vector3(w, height, t * 0.5f), PaperMat);
        ModelKit.Part(body, PrimitiveType.Cube, new Vector3(0f, 0f, t * 0.25f), new Vector3(w, height, t * 0.5f), BackMat);
        CardBack(body, w, height, t);

        Material ink = InkMat(card);
        float front = -t * 0.5f - 0.002f;
        switch (card.rank) {
            case CardRank.Joker:
                ModelKit.MeshPart(body, WorldMeshes.Star(height * 0.26f, height * 0.11f), new Vector3(0f, height * 0.04f, front), Vector3.one, ink);
                break;
            case CardRank.Jack:
            case CardRank.Queen:
            case CardRank.King:
                ModelKit.Part(body, PrimitiveType.Cube, new Vector3(0f, 0f, front * 0.6f), new Vector3(w * 0.62f, height * 0.62f, t * 0.2f), GoldMat);
                ModelKit.Part(body, PrimitiveType.Cube, new Vector3(0f, 0f, front * 0.8f), new Vector3(w * 0.54f, height * 0.54f, t * 0.2f), PaperMat);
                Text(body, card.RankText, new Vector3(0f, -height * 0.02f, front * 1.2f), height * 0.42f, card.Ink);
                break;
            default:
                Suit(body, card.suit, new Vector3(0f, 0f, front), height * (card.rank == CardRank.Ace ? 0.42f : 0.3f), ink);
                break;
        }
        // The index in the top-left and (upside down) bottom-right corners.
        float s = card.rank == CardRank.Joker ? height * 0.085f : height * 0.16f;
        Vector3 corner = new Vector3(-w * 0.34f, height * 0.38f, front);
        Text(body, card.RankText, corner, s, card.Ink);
        Text(body, card.RankText, new Vector3(-corner.x, -corner.y, front), s, card.Ink, 180f);
        return root;
    }

    // The red back's pattern: a white frame and a gold diamond in the middle (on the +Z side of the card body).
    static void CardBack(Transform body, float w, float h, float t) {
        float z = t * 0.5f + 0.002f, b = Mathf.Min(w, h) * 0.05f;
        Material white = PaperMat;
        ModelKit.Part(body, PrimitiveType.Cube, new Vector3(0f, h * 0.5f - b * 1.5f, z), new Vector3(w - b * 2f, b * 0.6f, t * 0.2f), white);
        ModelKit.Part(body, PrimitiveType.Cube, new Vector3(0f, -h * 0.5f + b * 1.5f, z), new Vector3(w - b * 2f, b * 0.6f, t * 0.2f), white);
        ModelKit.Part(body, PrimitiveType.Cube, new Vector3(w * 0.5f - b * 1.5f, 0f, z), new Vector3(b * 0.6f, h - b * 2f, t * 0.2f), white);
        ModelKit.Part(body, PrimitiveType.Cube, new Vector3(-w * 0.5f + b * 1.5f, 0f, z), new Vector3(b * 0.6f, h - b * 2f, t * 0.2f), white);
        Transform diamond = Pivot("Diamond", body, new Vector3(0f, 0f, z), Vector3.zero);
        diamond.localScale = new Vector3(0.7f, 1f, 1f);
        ModelKit.Part(diamond, PrimitiveType.Cube, Vector3.zero, new Vector3(w * 0.42f, w * 0.42f, t * 0.2f), GoldMat, new Vector3(0f, 0f, 45f));
    }

    // A suit symbol `size` tall, flat in the XY plane (a little depth), centred on `pos`, built from balls and blocks.
    public static GameObject Suit(Transform parent, CardSuit suit, Vector3 pos, float size, Material m) {
        Transform root = Pivot(suit.ToString(), parent, pos, Vector3.zero);
        float d = size * 0.12f;   // depth
        Mesh ball = WorldMeshes.Ball(0.5f);
        switch (suit) {
            case CardSuit.Hearts:
                ModelKit.MeshPart(root, ball, new Vector3(-size * 0.2f, size * 0.17f, 0f), new Vector3(size * 0.52f, size * 0.52f, d), m);
                ModelKit.MeshPart(root, ball, new Vector3(size * 0.2f, size * 0.17f, 0f), new Vector3(size * 0.52f, size * 0.52f, d), m);
                Rhombus(root, new Vector3(0f, -size * 0.08f, 0f), size * 0.55f, size * 0.62f, d, m);
                break;
            case CardSuit.Spades:
                ModelKit.MeshPart(root, ball, new Vector3(-size * 0.2f, -size * 0.06f, 0f), new Vector3(size * 0.5f, size * 0.5f, d), m);
                ModelKit.MeshPart(root, ball, new Vector3(size * 0.2f, -size * 0.06f, 0f), new Vector3(size * 0.5f, size * 0.5f, d), m);
                Rhombus(root, new Vector3(0f, size * 0.17f, 0f), size * 0.55f, size * 0.62f, d, m);
                Stem(root, size, d, m);
                break;
            case CardSuit.Clubs:
                ModelKit.MeshPart(root, ball, new Vector3(0f, size * 0.2f, 0f), new Vector3(size * 0.4f, size * 0.4f, d), m);
                ModelKit.MeshPart(root, ball, new Vector3(-size * 0.2f, -size * 0.06f, 0f), new Vector3(size * 0.4f, size * 0.4f, d), m);
                ModelKit.MeshPart(root, ball, new Vector3(size * 0.2f, -size * 0.06f, 0f), new Vector3(size * 0.4f, size * 0.4f, d), m);
                Stem(root, size, d, m);
                break;
            default:   // diamonds
                Rhombus(root, Vector3.zero, size * 0.72f, size, d, m);
                break;
        }
        return root.gameObject;
    }

    static void Rhombus(Transform parent, Vector3 pos, float width, float height, float depth, Material m) {
        Transform t = Pivot("Rhombus", parent, pos, Vector3.zero);
        t.localScale = new Vector3(width, height, 1f);
        ModelKit.Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.7071f, 0.7071f, depth), m, new Vector3(0f, 0f, 45f));
    }

    static void Stem(Transform parent, float size, float depth, Material m) {
        ModelKit.MeshPart(parent, WorldMeshes.Frustum(4, size * 0.16f, size * 0.03f, size * 0.3f), new Vector3(0f, -size * 0.5f, 0f),
                          new Vector3(1f, 1f, depth / (size * 0.16f)), m, new Vector3(0f, 45f, 0f));
    }

    // World-space text (the game's bold font) facing the camera; `size` is roughly the letter height in metres.
    public static TextMeshPro Text(Transform parent, string text, Vector3 localPos, float size, Color color, float roll = 0f) {
        GameAssets assets = GameAssets.I;
        if (assets == null || assets.fontBold == null) return null;
        GameObject go = new GameObject("Text " + text);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
        go.layer = parent.gameObject.layer;
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.font = assets.fontBold;
        t.text = text;
        t.color = color;
        t.fontSize = size * 10f;   // TMP font size 10 is about one metre-high capitals at scale 1
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.rectTransform.sizeDelta = new Vector2(size * 6f, size * 1.6f);
        return t;
    }

    // ------------------------------------------------------------------ die
    // An ivory die `size` on a side with black pips; 1 faces -Z (the camera), 6 +Z, 2 +Y, 5 -Y, 3 +X, 4 -X (opposite
    // faces add up to 7). Turn the returned body with DieFacing(value) to show a value to the camera.
    public static Transform Die(Transform parent, float size) {
        Transform body = Pivot("Die", parent, Vector3.zero, Vector3.zero);
        ModelKit.Part(body, PrimitiveType.Cube, Vector3.zero, Vector3.one * size, IvoryMat);
        Pips(body, size, Vector3.back, 1);
        Pips(body, size, Vector3.forward, 6);
        Pips(body, size, Vector3.up, 2);
        Pips(body, size, Vector3.down, 5);
        Pips(body, size, Vector3.right, 3);
        Pips(body, size, Vector3.left, 4);
        return body;
    }

    // The rotation that turns the face showing `value` towards the camera (-Z).
    public static Quaternion DieFacing(int value) {
        switch (Mathf.Clamp(value, 1, 6)) {
            case 6: return Quaternion.Euler(0f, 180f, 0f);
            case 2: return Quaternion.Euler(-90f, 0f, 0f);
            case 5: return Quaternion.Euler(90f, 0f, 0f);
            case 3: return Quaternion.Euler(0f, 90f, 0f);
            case 4: return Quaternion.Euler(0f, -90f, 0f);
            default: return Quaternion.identity;
        }
    }

    static readonly Vector2[][] PipLayout = {
        new Vector2[0],
        new[] { Vector2.zero },
        new[] { new Vector2(-1f, 1f), new Vector2(1f, -1f) },
        new[] { new Vector2(-1f, 1f), Vector2.zero, new Vector2(1f, -1f) },
        new[] { new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f) },
        new[] { new Vector2(-1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-1f, -1f), new Vector2(1f, -1f) },
        new[] { new Vector2(-1f, 1f), new Vector2(-1f, 0f), new Vector2(-1f, -1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(1f, -1f) },
    };

    static void Pips(Transform body, float size, Vector3 normal, int value) {
        Vector3 u = Mathf.Abs(normal.x) > 0.5f ? Vector3.forward : Vector3.right;
        Vector3 v = Mathf.Abs(normal.y) > 0.5f ? Vector3.forward : Vector3.up;
        Vector3 flat = new Vector3(Mathf.Abs(normal.x) > 0.5f ? 0.35f : 1f, Mathf.Abs(normal.y) > 0.5f ? 0.35f : 1f, Mathf.Abs(normal.z) > 0.5f ? 0.35f : 1f);
        Material m = value == 1 ? RedInk : BlackInk;   // the one is red, as on casino dice
        float pip = size * (value == 1 ? 0.26f : 0.18f);
        foreach (Vector2 p in PipLayout[value]) {
            Vector3 at = normal * size * 0.49f + (u * p.x + v * p.y) * size * 0.27f;
            ModelKit.MeshPart(body, WorldMeshes.Ball(0.5f), at, flat * pip, m, default, "Pip");
        }
    }

    // ------------------------------------------------------------------ coin
    // A gold coin of `radius` standing in the XY plane: heads (a crown) faces the camera, tails (a big "1") the back.
    // Turn the returned body 180 degrees about Y to show tails.
    public static Transform Coin(Transform parent, float radius) {
        Transform body = Pivot("Coin", parent, Vector3.zero, Vector3.zero);
        float t = radius * 0.16f;
        ModelKit.MeshPart(body, WorldMeshes.Frustum(28, radius, radius, t), new Vector3(0f, 0f, t * 0.5f), Vector3.one, GoldMat, new Vector3(-90f, 0f, 0f));
        Material relief = Mats.Lit(new Color(0.86f, 0.62f, 0.2f), 0.7f, 0.7f);
        float rim = t * 0.5f + radius * 0.02f;
        ModelKit.MeshPart(body, WorldMeshes.Frustum(28, radius * 0.86f, radius * 0.86f, radius * 0.04f), new Vector3(0f, 0f, -rim + radius * 0.02f), Vector3.one, relief, new Vector3(-90f, 0f, 0f));
        ModelKit.MeshPart(body, WorldMeshes.Frustum(28, radius * 0.86f, radius * 0.86f, radius * 0.04f), new Vector3(0f, 0f, rim + radius * 0.02f), Vector3.one, relief, new Vector3(-90f, 0f, 0f));
        // Heads: a crown on a band.
        float zh = -rim - radius * 0.02f;
        ModelKit.Part(body, PrimitiveType.Cube, new Vector3(0f, -radius * 0.2f, zh), new Vector3(radius * 0.9f, radius * 0.22f, radius * 0.06f), GoldMat);
        for (int k = -1; k <= 1; k++) {
            float h = k == 0 ? radius * 0.5f : radius * 0.38f;
            ModelKit.MeshPart(body, MeshKit.Cone(4, radius * 0.17f, h), new Vector3(k * radius * 0.32f, -radius * 0.1f, zh), new Vector3(1f, 1f, 0.35f), GoldMat);
            ModelKit.MeshPart(body, WorldMeshes.Ball(radius * 0.07f), new Vector3(k * radius * 0.32f, -radius * 0.1f + h + radius * 0.03f, zh), Vector3.one, GoldMat);
        }
        // Tails: a "1" (read from behind, so it is built mirrored in x).
        float zt = rim + radius * 0.02f;
        ModelKit.Part(body, PrimitiveType.Cube, new Vector3(0f, 0f, zt), new Vector3(radius * 0.16f, radius * 0.95f, radius * 0.06f), GoldMat);
        ModelKit.Part(body, PrimitiveType.Cube, new Vector3(radius * 0.13f, radius * 0.36f, zt), new Vector3(radius * 0.3f, radius * 0.13f, radius * 0.06f), GoldMat, new Vector3(0f, 0f, 35f));
        ModelKit.Part(body, PrimitiveType.Cube, new Vector3(0f, -radius * 0.46f, zt), new Vector3(radius * 0.5f, radius * 0.13f, radius * 0.06f), GoldMat);
        return body;
    }

    // ------------------------------------------------------------------ effects
    // Effects are drawn just in front of the blocks (see VFX.FrontZ).
    public static Vector3 Front(Vector3 p) { return new Vector3(p.x, p.y, Mathf.Min(p.z, VFX.FrontZ)); }

    // A shower of gold stars and a warm flash: luck, a win, a jackpot.
    public static void Sparkle(Vector3 pos, float size, bool light = true) {
        Vector3 at = Front(pos);
        int n = Mathf.Clamp(Mathf.RoundToInt(14f * size), 5, 40);
        for (int i = 0; i < n; i++)
            FxLayers.Star.Emit(at + (Vector3)(Random.insideUnitCircle * 0.4f * size), new Vector3(Random.Range(-3f, 3f), Random.Range(1f, 5f), 0f) * size,
                               Random.Range(0.18f, 0.35f) * size, Random.Range(0.5f, 0.9f), Gold);
        FxLayers.Glow.Emit(at, Vector3.zero, 2.4f * size, 0.35f, Gold);
        if (light) VFX.LightFlash(pos + Vector3.back, Gold, 5f * size, 7f * size, 0.35f);
    }

    // A short message over a point (what a card is, what the dice or the coin say).
    public static void Announce(Vector3 pos, string text, Color color, float scale = 1.2f) {
        VFX.FloatingText(pos, text, color, scale);
    }

    static Transform Pivot(string name, Transform parent, Vector3 localPos, Vector3 euler) {
        Transform t = ModelKit.Root(name, parent).transform;
        t.localPosition = localPos;
        t.localRotation = Quaternion.Euler(euler);
        return t;
    }
}

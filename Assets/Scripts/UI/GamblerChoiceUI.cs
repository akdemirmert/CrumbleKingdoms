using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The Gambler's questions before he aims (HumanShotController.PendingChoice = GamblerHand.Prompt), put in the HUD to
// the human whose turn it is:
//   Deck of Fate    a fan of five face-down cards above the weapon bar. He clicks one (or presses Q W E R T): it is his
//                   at once (that unlocks the aim) and turns over, the other four follow, dimmed, to show what he
//                   missed, and the fan slides away.
//   Heads or Tails  two gold coins, HEADS and TAILS (or H / T).
// Answered, a chip left of the weapon bar keeps showing what he holds (the card and what it does, or his call) while
// he aims. Polls BattleManager every frame like BattleHUD and shows nothing unless a human is aiming (a bot never
// answers: his card and call are drawn when he throws). Everything hides while paused and when the weapon or the
// aiming fortress changes (in hot seat each Gambler has his own hand); a fan closed unanswered is dealt afresh next
// time. Only the cards and coins catch the pointer, and only until he answers, so any other press still starts the aim.
public class GamblerChoiceUI : MonoBehaviour {
    const float CardH = 154f, CardW = CardH * GamblerKit.CardAspect, FanStep = 122f, FanTilt = 6f, Lift = 16f, CoinSize = 150f;
    const float PanelY = 282f, PanelH = 330f;   // above the first-shot drag hint (y 214-272), which returns once he has answered
    const float DealTime = 0.32f, DealStagger = 0.07f, FlipTime = 0.25f, OthersAfter = 0.35f, OthersStagger = 0.06f;
    const float RevealTime = 1.6f, CallHold = 0.45f;   // how long the answer stays up (unless he starts aiming first)
    static readonly Key[] CardKeys = { Key.Q, Key.W, Key.E, Key.R, Key.T };   // not 1-4 (weapons), Space/Enter (fire), Esc
    static readonly Color Table = UIKit.Alpha(UIKit.Darken(GamblerKit.Felt, 0.38f), 0.94f);   // the question panels are felt
    static readonly Color CoinInk = new Color(0.52f, 0.33f, 0.07f);                            // engraving on the gold coins

    class CardView {
        public RectTransform slot, visual, face;   // slot: fixed in the fan (the hit area); visual: deals in, lifts, flips
        public GameObject back, dim, key;
        public Image glow;
        public CanvasGroup group;
        public PlayingCard card;
        public float flipAt = -1f, lift;
    }

    class CoinView {
        public RectTransform spin;
        public CanvasGroup group;
    }

    readonly CardView[] cards = new CardView[5];
    readonly CoinView[] coins = new CoinView[2];   // heads, tails
    UIFade fanFade, coinFade, cardChipFade, callChipFade;
    CanvasGroup fanGroup, coinGroup;
    TextMeshProUGUI result, heldName, heldEffect, callText;
    RectTransform heldFace;
    GameObject callHeads, callTails;

    Fortress fanFor, coinFor;   // whom the fan / the coins were put up for (null: put them up afresh next time)
    int picked = -1, shownCard = -1, shownCall = -1;
    float dealtAt, pickedAt, revealUntil, callAt = -1f, callUntil;
    bool othersTurned, calledHeads;

    public static GamblerChoiceUI Create(RectTransform canvasRoot) {
        RectTransform root = UIKit.Rect(canvasRoot, "GamblerChoice").Fill();   // no graphic of its own: never in the way
        GamblerChoiceUI ui = root.gameObject.AddComponent<GamblerChoiceUI>();
        ui.BuildFan(root);
        ui.BuildCoins(root);
        ui.BuildChips(root);
        return ui;
    }

    // ------------------------------------------------------------------ per frame
    void Update() {
        BattleManager bm = BattleManager.I;
        HumanShotController human = bm != null ? bm.Human : null;
        Fortress f = human != null && bm.State == BattleState.Aiming && !TimeFX.Paused ? human.Active : null;
        WeaponId? weapon = f != null ? f.SelectedWeapon.id : (WeaponId?)null;
        bool deck = weapon == WeaponId.DeckOfFate, coin = weapon == WeaponId.CoinToss;
        GamblerHand hand = deck || coin ? GamblerHand.Of(f) : null;
        bool asked = hand != null && HumanShotController.ChoiceFor(f) != null;
        bool aimStarted = human != null && (human.IsDragging || human.HasAim);   // the answer then gets out of his way
        float now = Time.unscaledTime;
        Keyboard kb = Keyboard.current;

        // Deck of Fate: the fan is up while he has no card, and for a moment after he picks one. A reveal cut short (he
        // aims, switches weapon, pauses) does not come back.
        bool showFan = deck && (asked || (picked >= 0 && fanFor == f && now < revealUntil && !aimStarted));
        if (!showFan) revealUntil = 0f;
        if (showFan && (fanFor != f || (asked && picked >= 0))) Deal(f);
        if (!showFan && picked < 0) fanFor = null;   // closed unanswered: a fresh fan next time
        Set(fanFade, showFan);
        if (showFan && picked < 0 && kb != null)
            for (int i = 0; i < CardKeys.Length; i++)
                if (kb[CardKeys[i]].wasPressedThisFrame) { PickCard(i); break; }
        fanGroup.blocksRaycasts = showFan && picked < 0;   // answered, the cards never stand in the way of the aim
        if (fanFade.gameObject.activeSelf) AnimateFan(now);

        // Heads or Tails: the coins are up until he calls, and the called one is tossed for a moment.
        bool showCoins = coin && (asked || (callAt >= 0f && coinFor == f && now < callUntil && !aimStarted));
        if (!showCoins) callUntil = 0f;
        if (showCoins && (coinFor != f || (asked && callAt >= 0f))) ResetCoins(f);
        if (!showCoins && callAt < 0f) coinFor = null;
        Set(coinFade, showCoins);
        if (showCoins && callAt < 0f && kb != null) {
            if (kb.hKey.wasPressedThisFrame) CallCoin(true);
            else if (kb.tKey.wasPressedThisFrame) CallCoin(false);
        }
        coinGroup.blocksRaycasts = showCoins && callAt < 0f;
        if (coinFade.gameObject.activeSelf) AnimateCoins(now);

        // What he holds, beside the weapon bar, once the question has gone.
        bool showCard = deck && hand.HasCard && !fanFade.Visible;
        if (showCard && shownCard != hand.Card.Index) ShowCard(hand.Card);
        Set(cardChipFade, showCard);
        bool showCall = coin && hand.HasCall && !coinFade.Visible;
        if (showCall && shownCall != (hand.CallHeads ? 1 : 0)) ShowCall(hand.CallHeads);
        Set(callChipFade, showCall);
    }

    // A fresh fan for `f`: five cards off a shuffled deck, face down, dealt in one after another.
    void Deal(Fortress f) {
        fanFor = f;
        picked = -1;
        dealtAt = Time.unscaledTime;
        PlayingCard[] dealt = PlayingCard.Deal(cards.Length);
        for (int i = 0; i < cards.Length; i++) {
            CardView c = cards[i];
            c.card = dealt[i];
            c.flipAt = -1f;
            c.lift = 0f;
            c.key.SetActive(true);
            for (int k = c.face.childCount - 1; k >= 0; k--) Destroy(c.face.GetChild(k).gameObject);
            BuildFace(c.face, c.card, CardH);
        }
        result.text = "";
        GamblerSounds.Play(GamblerSounds.Shuffle, null, 0.5f);
    }

    // His card is his at once (that unlocks the aim); it turns over, then the four he did not take.
    void PickCard(int i) {
        if (picked >= 0 || fanFor == null || !fanFade.Visible) return;
        picked = i;
        pickedAt = Time.unscaledTime;
        revealUntil = pickedAt + RevealTime;
        othersTurned = false;
        PlayingCard card = cards[i].card;
        GamblerHand.Of(fanFor).Pick(card);
        int later = 0;
        for (int j = 0; j < cards.Length; j++) {
            cards[j].key.SetActive(false);
            cards[j].flipAt = j == i ? pickedAt : pickedAt + OthersAfter + OthersStagger * later++;
        }
        result.text = card.Name + " - <color=" + UIKit.HexOf(UIKit.Gold) + ">" + card.Effect + "</color>";
        GamblerSounds.Play(GamblerSounds.CardFlip, null, 0.8f);
    }

    // Dealing in, the hovered card lifting, the flips (scale x 1 -> 0, face swapped in, 0 -> 1) and the dimmed misses.
    void AnimateFan(float now) {
        int hover = picked < 0 ? Hovered() : -1;
        float k = UIKit.Approach(14f);
        for (int i = 0; i < cards.Length; i++) {
            CardView c = cards[i];
            float deal = Mathf.Clamp01((now - dealtAt - i * DealStagger) / DealTime);
            float flip = c.flipAt < 0f ? 0f : Mathf.Clamp01((now - c.flipAt) / FlipTime);
            bool faceUp = flip >= 0.5f;
            c.back.SetActive(!faceUp);
            c.face.gameObject.SetActive(faceUp);
            c.dim.SetActive(faceUp && i != picked);
            c.lift = Mathf.Lerp(c.lift, i == hover || i == picked ? 1f : 0f, k);
            float size = Mathf.LerpUnclamped(0.6f, 1f, UIKit.EaseOutBack(deal)) * (i == picked ? 1.06f : faceUp ? 0.94f : 1f);
            c.visual.anchoredPosition = new Vector2(0f, Lift * c.lift - 40f * (1f - deal));
            c.visual.localScale = new Vector3(size * Mathf.Abs(Mathf.Cos(flip * Mathf.PI)), size, 1f);
            c.group.alpha = Mathf.Clamp01(deal * 4f);
            c.glow.color = UIKit.Alpha(UIKit.Gold, i == picked ? 0.8f * Mathf.Clamp01((now - pickedAt) / 0.3f) : 0f);
        }
        if (picked >= 0 && !othersTurned && now >= pickedAt + OthersAfter) {
            othersTurned = true;
            GamblerSounds.Play(GamblerSounds.CardFlip, null, 0.45f, 1.15f);
        }
        result.alpha = picked < 0 ? 0f : Mathf.Clamp01((now - pickedAt - FlipTime * 0.5f) / 0.2f);
    }

    // The card under the pointer, tested against the fixed slots so a lifted card never slips out from under it.
    int Hovered() {
        Mouse m = Mouse.current;
        if (m == null) return -1;
        Vector2 at = m.position.ReadValue();
        for (int i = 0; i < cards.Length; i++)
            if (RectTransformUtility.RectangleContainsScreenPoint(cards[i].slot, at, null)) return i;
        return -1;
    }

    void ResetCoins(Fortress f) {
        coinFor = f;
        callAt = -1f;
        foreach (CoinView v in coins) {
            v.spin.localScale = Vector3.one;
            v.spin.anchoredPosition = Vector2.zero;
            v.group.alpha = 1f;
        }
    }

    void CallCoin(bool heads) {
        if (callAt >= 0f || coinFor == null || !coinFade.Visible) return;
        callAt = Time.unscaledTime;
        callUntil = callAt + CallHold;
        calledHeads = heads;
        GamblerHand.Of(coinFor).Call(heads);
        GamblerSounds.Play(GamblerSounds.CoinFlip, null, 0.8f);
    }

    // The called coin is tossed (it spins and hops), the other one fades.
    void AnimateCoins(float now) {
        if (callAt < 0f) return;
        float t = now - callAt;
        for (int i = 0; i < coins.Length; i++) {
            CoinView v = coins[i];
            if ((i == 0) == calledHeads) {
                v.spin.localScale = new Vector3(Mathf.Abs(Mathf.Cos(t * 22f)), 1f, 1f);
                v.spin.anchoredPosition = new Vector2(0f, 40f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / CallHold)));
            } else {
                v.group.alpha = Mathf.Max(0.3f, 1f - t * 4f);
            }
        }
    }

    void ShowCard(PlayingCard card) {
        shownCard = card.Index;
        for (int k = heldFace.childCount - 1; k >= 0; k--) Destroy(heldFace.GetChild(k).gameObject);
        BuildFace(heldFace, card, heldFace.sizeDelta.y);
        heldName.text = card.Name;
        heldEffect.text = card.Effect;
    }

    void ShowCall(bool heads) {
        shownCall = heads ? 1 : 0;
        callHeads.SetActive(heads);
        callTails.SetActive(!heads);
        callText.text = "Your call: <color=" + UIKit.HexOf(UIKit.Gold) + ">" + (heads ? "HEADS" : "TAILS") + "</color>";
    }

    static void Set(UIFade f, bool on) {
        if (f.Visible != on) f.Show(on);
    }

    // ------------------------------------------------------------------ building
    void BuildFan(RectTransform root) {
        RectTransform panel = QuestionPanel(root, "Cards", 720f, "Pick a card", out fanFade, out fanGroup);
        result = UIKit.Title(panel, "", 30f, UIKit.TextColor);
        result.AutoSize(18f, 30f);
        result.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(680f, 42f));
        RectTransform fan = UIKit.Rect(panel, "Fan").Place(new Vector2(0.5f, 0f), new Vector2(0f, 164f), Vector2.zero);
        for (int i = 0; i < cards.Length; i++) cards[i] = BuildCard(fan, i);
    }

    CardView BuildCard(RectTransform fan, int i) {
        CardView c = new CardView();
        int k = i - 2;
        Image hit = UIKit.Img(fan, null, Color.clear, "Card " + CardKeys[i]);
        hit.raycastTarget = true;
        c.slot = hit.rectTransform;
        c.slot.sizeDelta = new Vector2(CardW, CardH);
        c.slot.anchoredPosition = new Vector2(k * FanStep, -k * k * 5f);   // fanned: the outer cards lower and tilted out
        c.slot.localEulerAngles = new Vector3(0f, 0f, -k * FanTilt);
        c.group = c.slot.gameObject.AddComponent<CanvasGroup>();
        c.visual = UIKit.Rect(c.slot, "Visual").Fill();
        c.glow = UIKit.Img(c.visual, UIKit.Shadow, Color.clear, "Glow");
        c.glow.rectTransform.Fill(-36f, -36f, -36f, -36f);
        c.back = BuildBack(c.visual);
        c.face = UIKit.Rect(c.visual, "Face").Fill();
        Image dim = UIKit.RoundedImg(c.visual, new Color(0.02f, 0.03f, 0.06f, 0.5f), 10f, "Dim");
        dim.rectTransform.Fill();
        c.dim = dim.gameObject;
        RectTransform key = UIKit.KeyCap(c.slot, CardKeys[i].ToString());
        key.anchorMin = key.anchorMax = new Vector2(0.5f, 0f);
        key.anchoredPosition = new Vector2(0f, -22f);
        key.localScale = new Vector3(0.6f, 0.6f, 1f);
        c.key = key.gameObject;
        UIButton b = c.slot.gameObject.AddComponent<UIButton>();
        b.hoverScale = 1f;     // a hovered card lifts instead (AnimateFan)
        b.clickSound = null;   // the flip has its own sound
        b.onClick = () => PickCard(i);
        return c;
    }

    // The red back: a white inner border round a lattice of small diamonds.
    static GameObject BuildBack(RectTransform visual) {
        Image back = UIKit.RoundedImg(visual, GamblerKit.BackRed, 10f, "Back");
        back.rectTransform.Fill();
        UIKit.RoundedImg(back.transform, GamblerKit.Paper, 7f, "Border").rectTransform.Fill(6f, 6f, 6f, 6f);
        UIKit.RoundedImg(back.transform, GamblerKit.BackRed, 5f, "Inside").rectTransform.Fill(9f, 9f, 9f, 9f);
        Image diamonds = UIKit.Img(back.transform, Lattice, UIKit.Alpha(GamblerKit.Paper, 0.3f), "Diamonds");
        diamonds.type = Image.Type.Tiled;
        diamonds.pixelsPerUnitMultiplier = 2f;   // 16-unit tiles from the 32-pixel texture
        diamonds.rectTransform.Fill(12f, 12f, 12f, 12f);
        return back.gameObject;
    }

    void BuildCoins(RectTransform root) {
        RectTransform panel = QuestionPanel(root, "Coins", 480f, "Call it!", out coinFade, out coinGroup);
        coins[0] = BuildCoin(panel, true, -120f);
        coins[1] = BuildCoin(panel, false, 120f);
    }

    CoinView BuildCoin(RectTransform panel, bool heads, float x) {
        CoinView v = new CoinView();
        RectTransform holder = UIKit.Rect(panel, heads ? "Heads" : "Tails");
        holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0f);
        holder.anchoredPosition = new Vector2(x, 168f);
        holder.sizeDelta = new Vector2(CoinSize, CoinSize);
        v.group = holder.gameObject.AddComponent<CanvasGroup>();
        v.spin = UIKit.Rect(holder, "Spin").Fill();
        UIKit.Img(v.spin, UIKit.Circle, UIKit.Darken(GamblerKit.Gold, 0.45f), "Lip").rectTransform.Fill(0f, -7f, 0f, 7f);
        Image face = CoinFace(v.spin, CoinSize, heads);
        face.raycastTarget = true;
        UIButton b = holder.gameObject.AddComponent<UIButton>();
        b.face = face;
        b.faceRect = face.rectTransform;
        b.SetColor(GamblerKit.Gold, true);
        b.clickSound = null;   // the coin flip sounds instead
        b.onClick = () => CallCoin(heads);

        RectTransform caption = UIKit.Rect(panel, "Caption").Place(new Vector2(0.5f, 0f), new Vector2(x, 22f), new Vector2(180f, 44f));
        RectTransform key = UIKit.KeyCap(caption, heads ? "H" : "T");
        key.anchorMin = key.anchorMax = new Vector2(0f, 0.5f);
        key.anchoredPosition = new Vector2(24f, 0f);
        key.localScale = new Vector3(0.62f, 0.62f, 1f);
        UIKit.Title(caption, heads ? "HEADS" : "TAILS", 32f, UIKit.TextColor, TextAlignmentOptions.Left).rectTransform.Fill(52f, 0f, 0f, 0f);
        return v;
    }

    // A felt panel centred above the weapon bar with a title; hidden until its question is asked.
    static RectTransform QuestionPanel(RectTransform root, string name, float width, string title, out UIFade fade, out CanvasGroup group) {
        RectTransform panel = UIKit.Rect(root, name).Place(new Vector2(0.5f, 0f), new Vector2(0f, PanelY), new Vector2(width, PanelH));
        fade = panel.gameObject.AddComponent<UIFade>();
        fade.hiddenOffset = new Vector2(0f, -60f);
        fade.speed = 5f;
        group = panel.GetComponent<CanvasGroup>();
        UIKit.Panel(panel, Table, 26f).Fill();
        TextMeshProUGUI t = UIKit.Title(panel, title, 42f, UIKit.Gold);
        t.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(width - 40f, 52f));
        t.gameObject.AddComponent<UIPulse>().scaleAmount = 0.03f;
        fade.Show(false, true);
        return panel;
    }

    void BuildChips(RectTransform root) {
        RectTransform card = Chip(root, "HeldCard", new Vector2(330f, 106f), out cardChipFade);
        heldFace = UIKit.Rect(card, "Card").Place(new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(CardW, CardH) * 0.54f);
        UIKit.Label(card, "YOUR CARD", 17f, UIKit.TextDim, TextAlignmentOptions.Left).rectTransform
             .Place(new Vector2(0f, 1f), new Vector2(88f, -12f), new Vector2(228f, 22f));
        heldName = UIKit.Label(card, "", 24f, UIKit.TextColor, TextAlignmentOptions.Left);
        heldName.AutoSize(15f, 24f);
        heldName.rectTransform.Place(new Vector2(0f, 1f), new Vector2(88f, -36f), new Vector2(228f, 30f));
        heldEffect = UIKit.Label(card, "", 22f, UIKit.Gold, TextAlignmentOptions.Left);
        heldEffect.AutoSize(14f, 22f);
        heldEffect.rectTransform.Place(new Vector2(0f, 1f), new Vector2(88f, -68f), new Vector2(228f, 28f));

        RectTransform call = Chip(root, "Call", new Vector2(300f, 76f), out callChipFade);
        RectTransform coin = UIKit.Rect(call, "Coin").Place(new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(52f, 52f));
        callHeads = CoinFace(coin, 52f, true).gameObject;
        callTails = CoinFace(coin, 52f, false).gameObject;
        callText = UIKit.Label(call, "", 26f, UIKit.TextColor, TextAlignmentOptions.Left);
        callText.rectTransform.Fill(80f, 0f, 14f, 0f);
    }

    // A small panel left of the weapon bar (the aim readout has its right); nothing on it catches the pointer.
    static RectTransform Chip(RectTransform root, string name, Vector2 size, out UIFade fade) {
        RectTransform chip = UIKit.Rect(root, name);
        chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 0f);
        chip.pivot = new Vector2(1f, 0f);
        chip.anchoredPosition = new Vector2(-316f, 24f);
        chip.sizeDelta = size;
        fade = chip.gameObject.AddComponent<UIFade>();
        fade.hiddenOffset = new Vector2(30f, 0f);
        UIKit.Panel(chip, UIKit.PanelColor, 22f).Fill();
        fade.Show(false, true);
        return chip;
    }

    // A card face `h` tall filling `face`, drawn with images (not the font's suit glyphs): white paper, the index in the
    // card's ink in two corners, and in the middle a big suit (number cards, aces), a framed letter (picture cards) or a
    // star (jokers).
    static void BuildFace(RectTransform face, PlayingCard card, float h) {
        float w = h * GamblerKit.CardAspect;
        Vector2 mid = new Vector2(0.5f, 0.5f);
        UIKit.RoundedImg(face, GamblerKit.Paper, h * 0.065f, "Paper").rectTransform.Fill();
        switch (card.rank) {
            case CardRank.Joker:
                UIKit.Img(face, UIKit.Icon(UIIcon.Star), card.Ink, "Star").rectTransform.Place(mid, Vector2.zero, new Vector2(w * 0.66f, w * 0.66f));
                break;
            case CardRank.Jack:
            case CardRank.Queen:
            case CardRank.King:
                UIKit.RoundedImg(face, GamblerKit.Gold, h * 0.04f, "Frame").rectTransform.Place(mid, Vector2.zero, new Vector2(w * 0.62f, h * 0.62f));
                UIKit.RoundedImg(face, GamblerKit.Paper, h * 0.03f, "Inside").rectTransform.Place(mid, Vector2.zero, new Vector2(w * 0.54f, h * 0.54f));
                UIKit.Label(face, card.RankText, h * 0.4f, card.Ink, TextAlignmentOptions.Midline).rectTransform
                     .Place(mid, Vector2.zero, new Vector2(w * 0.54f, h * 0.54f));
                break;
            default:
                float s = h * (card.rank == CardRank.Ace ? 0.44f : 0.32f);
                UIKit.Img(face, SuitSprite(card.suit), card.Ink, "Suit").rectTransform.Place(mid, Vector2.zero, new Vector2(s, s));
                break;
        }
        CornerIndex(face, card, h, false);
        CornerIndex(face, card, h, true);
    }

    // The rank over a small suit (jokers: "JOKER") in the top-left corner, or upside down in the bottom-right one.
    static void CornerIndex(RectTransform face, PlayingCard card, float h, bool bottom) {
        bool joker = card.rank == CardRank.Joker;
        float gw = h * 0.21f, gh = h * 0.28f, x = gw * 0.5f + h * 0.025f, y = gh * 0.5f + h * 0.025f;
        RectTransform g = UIKit.Rect(face, "Index");
        g.anchorMin = g.anchorMax = bottom ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
        g.sizeDelta = new Vector2(gw, gh);
        g.anchoredPosition = bottom ? new Vector2(-x, y) : new Vector2(x, -y);
        if (bottom) g.localEulerAngles = new Vector3(0f, 0f, 180f);
        UIKit.Label(g, card.RankText, h * (joker ? 0.085f : 0.15f), card.Ink, joker ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Top)
             .rectTransform.Fill();
        if (!joker)
            UIKit.Img(g, SuitSprite(card.suit), card.Ink, "Suit").rectTransform
                 .Place(new Vector2(0.5f, 0f), new Vector2(0f, gh * 0.06f), new Vector2(h * 0.09f, h * 0.09f));
    }

    // A gold coin face filling `parent`: an engraved rim and a crown (heads) or a big "1" (tails), like the coin he throws.
    static Image CoinFace(RectTransform parent, float size, bool heads) {
        Image face = UIKit.Img(parent, UIKit.Circle, GamblerKit.Gold, heads ? "Heads" : "Tails");
        face.rectTransform.Fill();
        float rim = size * 0.07f, m = size * 0.24f;
        UIKit.Img(face.transform, UIKit.Ring, UIKit.Darken(GamblerKit.Gold, 0.25f), "Rim").rectTransform.Fill(rim, rim, rim, rim);
        if (heads) UIKit.Img(face.transform, Crown, CoinInk, "Crown").rectTransform.Fill(m, m, m, m);
        else UIKit.Label(face.transform, "1", size * 0.6f, CoinInk, TextAlignmentOptions.Midline).rectTransform.Fill();
        return face;
    }

    // ------------------------------------------------------------------ painted sprites (made once)
    // The suits, the crown and the back's diamonds are painted from circles, rotated squares (kites, rhombi) and
    // triangles into small anti-aliased sprites, so they stay crisp at any size.
    static Sprite lattice, crown;
    static readonly Sprite[] suits = new Sprite[4];

    static Sprite Lattice => lattice != null ? lattice : lattice = Paint(32, true, p =>
        (Mathf.Min(Mathf.Abs(p.x) + Mathf.Abs(p.y), 2f - Mathf.Abs(p.x) - Mathf.Abs(p.y)) - 0.62f) * 0.7071f);   // a diamond grid

    static Sprite Crown => crown != null ? crown : crown = Paint(128, false, CrownShape());

    static Sprite SuitSprite(CardSuit s) {
        int i = (int)s;
        if (suits[i] == null) suits[i] = Paint(128, false, SuitShape(s));
        return suits[i];
    }

    static Func<Vector2, float> SuitShape(CardSuit s) {
        Vector2[] stem = { new Vector2(-0.3f, -0.96f), new Vector2(0.3f, -0.96f), new Vector2(0f, -0.35f) };
        switch (s) {
            case CardSuit.Hearts: {   // two lobes over a point (the HUD's heart)
                Vector2[] point = { new Vector2(0f, -0.88f), new Vector2(0.84f, 0.14f), new Vector2(0f, 0.4f), new Vector2(-0.84f, 0.14f) };
                return p => Mathf.Min(Mathf.Min(Disc(p, -0.4f, 0.32f, 0.47f), Disc(p, 0.4f, 0.32f, 0.47f)), Convex(p, point));
            }
            case CardSuit.Spades: {   // the heart upside down, on a flared stem
                Vector2[] point = { new Vector2(0f, 0.88f), new Vector2(-0.84f, -0.14f), new Vector2(0f, -0.4f), new Vector2(0.84f, -0.14f) };
                return p => Mathf.Min(Mathf.Min(Disc(p, -0.4f, -0.32f, 0.47f), Disc(p, 0.4f, -0.32f, 0.47f)), Mathf.Min(Convex(p, point), Convex(p, stem)));
            }
            case CardSuit.Clubs:      // three lobes, a web between them and the stem
                return p => Mathf.Min(Mathf.Min(Disc(p, 0f, 0.38f, 0.36f), Mathf.Min(Disc(p, -0.42f, -0.14f, 0.36f), Disc(p, 0.42f, -0.14f, 0.36f))),
                                      Mathf.Min(Disc(p, 0f, -0.06f, 0.24f), Convex(p, stem)));
            default: {                // diamonds: a tall rhombus
                Vector2[] rhombus = { new Vector2(0f, -0.96f), new Vector2(0.7f, 0f), new Vector2(0f, 0.96f), new Vector2(-0.7f, 0f) };
                return p => Convex(p, rhombus);
            }
        }
    }

    // A band, three points and a ball on each.
    static Func<Vector2, float> CrownShape() {
        Vector2[] left = { new Vector2(-0.72f, -0.34f), new Vector2(-0.18f, -0.34f), new Vector2(-0.66f, 0.3f) };
        Vector2[] middle = { new Vector2(-0.3f, -0.34f), new Vector2(0.3f, -0.34f), new Vector2(0f, 0.5f) };
        Vector2[] right = { new Vector2(0.18f, -0.34f), new Vector2(0.72f, -0.34f), new Vector2(0.66f, 0.3f) };
        return p => Mathf.Min(Mathf.Min(Box(p, 0f, -0.47f, 0.72f, 0.14f), Mathf.Min(Convex(p, left), Mathf.Min(Convex(p, middle), Convex(p, right)))),
                              Mathf.Min(Disc(p, -0.66f, 0.38f, 0.11f), Mathf.Min(Disc(p, 0f, 0.6f, 0.12f), Disc(p, 0.66f, 0.38f, 0.11f))));
    }

    // A white sprite from a signed distance over p in [-1, 1]² (negative = inside), its edges anti-aliased.
    static Sprite Paint(int size, bool tiles, Func<Vector2, float> sdf) {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true) {
            wrapMode = tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, name = "GamblerChoice",
        };
        Color32[] px = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) {
                float a = Mathf.Clamp01(0.5f - sdf(new Vector2((x + 0.5f) / half - 1f, (y + 0.5f) / half - 1f)) * half);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        t.SetPixels32(px);
        t.Apply(true);
        return TexKit.ToSprite(t);
    }

    static float Disc(Vector2 p, float x, float y, float r) { return (p - new Vector2(x, y)).magnitude - r; }

    static float Box(Vector2 p, float x, float y, float hx, float hy) {
        float dx = Mathf.Abs(p.x - x) - hx, dy = Mathf.Abs(p.y - y) - hy;
        return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0f);
    }

    // Signed distance to a convex polygon (vertices in order round it, either way).
    static float Convex(Vector2 p, Vector2[] v) {
        float turn = Mathf.Sign((v[1].x - v[0].x) * (v[2].y - v[1].y) - (v[1].y - v[0].y) * (v[2].x - v[1].x));
        float d = float.MinValue;
        for (int i = 0; i < v.Length; i++) {
            Vector2 a = v[i], e = v[(i + 1) % v.Length] - a;
            d = Mathf.Max(d, Vector2.Dot(p - a, new Vector2(e.y, -e.x).normalized * turn));   // along the outward normal
        }
        return d;
    }
}

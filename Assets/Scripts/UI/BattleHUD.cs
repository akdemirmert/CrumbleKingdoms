using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Battle scene UI. Listens to BattleManager's events and polls its state; never drives the game itself
// except through the documented calls (SkipIntro, Human.SelectSlot, pause, scene changes).
//   top:    fortress cards with status chips (left team / enemies), turn banner with the turn number, wind, last shot
//   bottom: weapon bar of the human shooter, aim readout, mid-air activation hint, first-shot tutorial / self-cast hint
//   centre: animated banners (level intro, "X defeated!"), hot-seat "get ready", pause menu, results
// Runs before BattleManager (execution order) so it subscribes before the battle is built, and reads
// Esc before the shot controller can turn an aim-cancel into a pause.
[DefaultExecutionOrder(-50)]
public class BattleHUD : MonoBehaviour {
    const float BannerIn = 0.35f, BannerHold = 2.3f, BannerOut = 0.45f, CardGap = 10f;
    const string DragHint = "Press anywhere, drag back and release to fire", CastHint = "Click (or Space) to cast";

    BattleManager bm;
    bool subscribed, cardsBuilt, resultShown, draggedLastFrame, introBanner;
    float resultAt = -1f, calloutUntil, hotSeatUntil, bannerT = -1f, turnPunch;
    readonly Dictionary<Fortress, HudFortressCard> cards = new Dictionary<Fortress, HudFortressCard>();

    RectTransform canvasRoot;
    UIFade top, aimPanel, activateHint, tutorialHint, callout, hotSeat;
    RectTransform leftGroup, rightGroup, centre, turnBox, windArrows, calloutIcon, hotSeatPortrait;
    TextMeshProUGUI turnTitle, turnNumber, windText, calloutText, activateText, angleText, powerText;
    TextMeshProUGUI bannerTitle, bannerSub, skipHint, hotSeatTitle, tutorialText;
    WeaponDef tutorialFor;   // self-cast weapon the bottom hint currently explains (null = the drag tutorial)
    Image[] windChevrons;
    UIBar powerBar;
    // HUD polish: turn box glow + glint, banner glint, power glow and the MAX flash
    Image turnGlow, powerGlow;
    UISweep turnSweep, bannerSweep;
    TextMeshProUGUI powerLabel;
    Color turnColor = Color.white;
    float turnGlowT, turnSide = -1f;
    bool maxShown;
    HudWeaponBar weaponBar;
    HudPauseMenu pauseMenu;
    HudResultScreen result;
    UIButton pauseButton;
    CanvasGroup bannerGroup;
    RectTransform bannerRoot;
    float shownWind = float.NaN;
    int shownAngle = int.MinValue, shownPower = -1;

    // ------------------------------------------------------------------ lifecycle
    void Start() {
        bm = BattleManager.I;
        UIKit.EnsureEventSystem();
        Build();
        if (bm == null) { enabled = false; return; }
        bm.BattleBuilt += OnBuilt;
        bm.StateChanged += OnState;
        bm.TurnStarted += OnTurn;
        bm.ShotFired += OnShot;
        bm.FortressDefeated += OnDefeated;
        bm.BattleEnded += OnEnded;
        bm.Banner += OnBanner;
        subscribed = true;

        // Start order between objects is undefined: catch up if the battle was built before we subscribed.
        if (bm.Setup != null && bm.fortresses.Count > 0) {
            OnBuilt();
            if (bm.State == BattleState.Intro) OnBanner(bm.Setup.title, bm.Setup.subtitle);
            if (bm.Current != null && bm.State != BattleState.Finished) OnTurn(bm.Current);
            if (bm.State == BattleState.Finished) OnEnded();
        }
    }

    void OnDestroy() {
        if (!subscribed || ReferenceEquals(bm, null)) return;
        bm.BattleBuilt -= OnBuilt;
        bm.StateChanged -= OnState;
        bm.TurnStarted -= OnTurn;
        bm.ShotFired -= OnShot;
        bm.FortressDefeated -= OnDefeated;
        bm.BattleEnded -= OnEnded;
        bm.Banner -= OnBanner;
    }

    // ------------------------------------------------------------------ events
    void OnBuilt() {
        if (cardsBuilt || bm.Setup == null) return;
        cardsBuilt = true;
        int left = 0, right = 0;
        foreach (Fortress f in bm.fortresses) if (f.setup.team == 0) left++; else right++;
        float width = Mathf.Max(left, right) >= 3 ? 284f : 316f;
        foreach (Fortress f in bm.fortresses)
            cards[f] = HudFortressCard.Create(f.setup.team == 0 ? leftGroup : rightGroup, f, width, bm.Setup.defeatIntegrity);
        // The centre column sits in the middle of the free space between the two card groups.
        float lw = left * width + Mathf.Max(0, left - 1) * CardGap, rw = right * width + Mathf.Max(0, right - 1) * CardGap;
        centre.anchoredPosition = new Vector2((lw - rw) * 0.5f, centre.anchoredPosition.y);
    }

    void OnState(BattleState s) {
        if (s != BattleState.Intro && introBanner && bannerT >= 0f && bannerT < BannerIn + BannerHold)
            bannerT = BannerIn + BannerHold;   // intro over (or skipped): let its banner leave now
        if (s == BattleState.Finished) Tooltip.Hide();
    }

    void OnTurn(Fortress f) {
        if (f == null) return;
        bool hotSeatGame = bm.IsHotSeat;
        turnTitle.text = !f.IsHuman ? f.DisplayName + " is aiming…" : hotSeatGame ? f.DisplayName.ToUpperInvariant() + "'S TURN" : "YOUR TURN";
        turnTitle.color = UIKit.Lighten(f.TeamColor, 0.35f);
        turnNumber.text = "TURN " + bm.Turn;
        turnPunch = 1f;
        turnColor = f.TeamColor;                      // the turn box swooshes in from the side of the team and glows in its colour
        turnSide = f.setup.team == 0 ? -1f : 1f;
        turnGlowT = 1f;
        turnSweep.Play();
        if (f.IsHuman) weaponBar.Bind(f);
        if (f.IsHuman && hotSeatGame) ShowHotSeat(f);
    }

    void OnShot(Fortress f, WeaponDef def) {
        if (f == null || def == null) return;
        calloutText.text = "<color=" + UIKit.HexOf(UIKit.Lighten(f.TeamColor, 0.35f)) + ">" + f.DisplayName + "</color>  ·  " + def.title;
        foreach (Transform child in calloutIcon) Destroy(child.gameObject);
        UIKit.WeaponIcon(calloutIcon, def.id, 44f).Fill();
        calloutUntil = Time.unscaledTime + 2.4f;
        Set(callout, true);
    }

    void OnDefeated(Fortress f) {
        if (f != null && cards.TryGetValue(f, out HudFortressCard card) && card != null) card.MarkDefeated();
    }

    void OnEnded() {
        if (resultAt < 0f && !resultShown) resultAt = Time.unscaledTime + 1.1f;   // let the final collapse be seen first
    }

    void OnBanner(string title, string subtitle) {
        bannerTitle.text = title;
        bannerSub.text = subtitle ?? "";
        introBanner = bm.State == BattleState.Intro || bm.State == BattleState.Building;
        bannerT = 0f;
        bannerRoot.gameObject.SetActive(true);
        bannerSweep.Play(0.35f);
    }

    // ------------------------------------------------------------------ per frame
    void Update() {
        if (bm == null) return;
        BattleState s = bm.State;
        HumanShotController human = bm.Human;
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (s == BattleState.Intro && AnyPress(kb, mouse)) bm.SkipIntro();
        else if (kb != null && kb.escapeKey.wasPressedThisFrame) HandleEscape(human);
        draggedLastFrame = human != null && human.IsDragging;

        bool playing = s == BattleState.TurnStart || s == BattleState.Aiming || s == BattleState.Resolving || s == BattleState.Collapse;
        Fortress cur = bm.Current;
        bool humanTurn = cur != null && cur.IsHuman && (s == BattleState.TurnStart || s == BattleState.Aiming);
        if (human != null && human.Active != null) weaponBar.Bind(human.Active);

        Set(top, playing && !resultShown);
        weaponBar.SetVisible(humanTurn && !resultShown);
        bool aiming = s == BattleState.Aiming && human != null && human.Active != null;
        Set(aimPanel, aiming && human.HasAim);
        UpdateTutorialHint(aiming ? human : null);
        UpdateActivationHint(s, cur);
        if (aiming && human.HasAim) UpdateAimReadout(human);

        UpdateTurnBox();
        UpdateWind();
        AnimateBanner(s);
        if (callout.Visible && Time.unscaledTime > calloutUntil) Set(callout, false);
        if (hotSeat.Visible && Time.unscaledTime > hotSeatUntil) Set(hotSeat, false);
        pauseButton.gameObject.SetActive(!resultShown && s != BattleState.Finished && s != BattleState.Building);
        if (resultAt > 0f && Time.unscaledTime >= resultAt) ShowResult();
    }

    static bool AnyPress(Keyboard kb, Mouse mouse) {
        return (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) ||
               (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame));
    }

    void HandleEscape(HumanShotController human) {
        if (resultShown) return;
        if (pauseMenu.IsOpen) {
            if (!pauseMenu.BackFromSettings()) Resume();
            return;
        }
        if (human != null && (human.IsDragging || draggedLastFrame)) return;   // Esc cancels the aim instead
        Pause();
    }

    void Pause() {
        if (bm == null || resultShown || bm.State == BattleState.Finished || bm.State == BattleState.Building || pauseMenu.IsOpen) return;
        TimeFX.SetPaused(true);
        Tooltip.Hide();
        pauseMenu.Open();
    }

    void Resume() {
        TimeFX.SetPaused(false);
        pauseMenu.Close();
    }

    // Hint above the weapon bar while a human aims: how to cast a weapon used on yourself (no aiming), or
    // the drag tutorial before that fortress's first shot. `human` is null when nobody is aiming.
    void UpdateTutorialHint(HumanShotController human) {
        WeaponDef def = human != null ? human.Active.SelectedWeapon : null;
        bool cast = def != null && def.selfCast;
        bool drag = def != null && !cast && !human.HasAim && human.Active.shotsFired == 0 && HumanShotController.ChoiceFor(human.Active) == null;
        WeaponDef wanted = cast ? def : null;
        if ((cast || drag) && wanted != tutorialFor) {   // texts are only rebuilt when the weapon changes
            tutorialFor = wanted;
            tutorialText.text = cast ? CastHint + " <color=" + UIKit.HexOf(UIKit.Lighten(def.color, 0.4f)) + ">" + def.title + "</color>" : DragHint;
        }
        Set(tutorialHint, cast || drag);
    }

    void UpdateActivationHint(BattleState s, Fortress cur) {
        Projectile p = s == BattleState.Resolving && cur != null && cur.IsHuman ? Activatable(cur) : null;
        if (p != null && !activateHint.Visible) activateText.text = p.def.activateHint;
        Set(activateHint, p != null);
    }

    // First shot of the current human fortress that can still be activated in mid-air.
    static Projectile Activatable(Fortress owner) {
        List<Projectile> list = Projectile.Active;
        for (int i = 0; i < list.Count; i++) {
            Projectile p = list[i];
            if (p != null && p.owner == owner && p.CanActivate) return p;
        }
        return null;
    }

    void UpdateAimReadout(HumanShotController human) {
        int angle = Mathf.RoundToInt(human.AngleDeg), power = Mathf.RoundToInt(human.Power01 * 100f);
        if (angle != shownAngle) { shownAngle = angle; angleText.text = angle + "°"; }
        if (power != shownPower) { shownPower = power; powerText.text = power + "%"; }
        powerBar.SetValue(human.Power01, true);

        // the power glow grows with the pull; at full power the label flashes MAX!
        float p = human.Power01;
        bool max = p >= 0.985f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 16f);
        powerGlow.color = UIKit.Alpha(Color.Lerp(WeaponDefs.Hex("FF5A3D"), WeaponDefs.Hex("FFE066"), max ? pulse : p),
                                      0.6f * p * p * p + (max ? 0.25f * pulse : 0f));
        if (max != maxShown) {
            maxShown = max;
            powerLabel.text = max ? "MAX!" : "POWER";
            if (!max) { powerLabel.color = UIKit.TextDim; powerText.rectTransform.localScale = Vector3.one; }
        }
        if (max) {
            powerLabel.color = Color.Lerp(UIKit.Gold, Color.white, pulse);
            float s = 1f + 0.08f * pulse;
            powerText.rectTransform.localScale = new Vector3(s, s, 1f);
        }
    }

    void UpdateTurnBox() {
        float dt = Time.unscaledDeltaTime;
        turnPunch = Mathf.MoveTowards(turnPunch, 0f, dt * 3f);
        turnGlowT = Mathf.MoveTowards(turnGlowT, 0f, dt * 0.8f);
        float s = 1f + 0.18f * turnPunch * turnPunch;
        turnBox.localScale = new Vector3(s, s, 1f);
        turnBox.anchoredPosition = new Vector2(turnSide * 140f * turnPunch * turnPunch * turnPunch, 0f);   // swoosh in
        turnGlow.color = UIKit.Alpha(turnColor, 0.22f + 0.7f * turnGlowT * turnGlowT);
    }

    // Arrow chevrons point where the wind blows; 1-3 of them by strength, animated like a marquee.
    void UpdateWind() {
        float w = BattleManager.Wind;
        if (w != shownWind) {
            shownWind = w;
            float max = bm.Setup != null ? Mathf.Max(0.01f, bm.Setup.windMax) : 1f;
            float k = Mathf.Abs(w) / max;
            int count = w == 0f ? 0 : k < 0.34f ? 1 : k < 0.67f ? 2 : 3;
            for (int i = 0; i < windChevrons.Length; i++) windChevrons[i].gameObject.SetActive(i < count);
            windArrows.localEulerAngles = new Vector3(0f, 0f, w < 0f ? 180f : 0f);
            windText.text = w == 0f ? "Calm" : Mathf.Abs(w).ToString("0.0");
        }
        for (int i = 0; i < windChevrons.Length; i++) {
            float a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f - i * 1.3f));
            windChevrons[i].color = UIKit.Alpha(UIKit.TextColor, a);
        }
    }

    void AnimateBanner(BattleState s) {
        if (bannerT < 0f) return;
        bannerT += Time.unscaledDeltaTime;
        float alpha = 1f, scale = 1f, y = 0f;
        if (bannerT < BannerIn) {
            float k = bannerT / BannerIn;
            alpha = k;
            scale = Mathf.Lerp(1.35f, 1f, 1f - (1f - k) * (1f - k) * (1f - k));
        } else if (bannerT > BannerIn + BannerHold) {
            float k = (bannerT - BannerIn - BannerHold) / BannerOut;
            if (k >= 1f) { bannerT = -1f; bannerRoot.gameObject.SetActive(false); return; }
            alpha = 1f - k;
            y = 50f * k * k;
        }
        bannerGroup.alpha = alpha;
        bannerRoot.localScale = new Vector3(scale, scale, 1f);
        bannerRoot.anchoredPosition = new Vector2(0f, 120f + y);
        skipHint.gameObject.SetActive(s == BattleState.Intro);
    }

    void ShowHotSeat(Fortress f) {
        hotSeatTitle.text = f.DisplayName.ToUpperInvariant();
        hotSeatTitle.color = UIKit.Lighten(f.TeamColor, 0.3f);
        foreach (Transform child in hotSeatPortrait) Destroy(child.gameObject);
        UIKit.PortraitIcon(hotSeatPortrait, f.HeroClass, 190f).Fill();
        hotSeatUntil = Time.unscaledTime + 1.8f;
        Set(hotSeat, true);
    }

    void ShowResult() {
        resultAt = -1f;
        resultShown = true;
        if (pauseMenu.IsOpen) Resume();
        Set(hotSeat, false);
        Set(callout, false);
        bannerT = -1f;
        bannerRoot.gameObject.SetActive(false);
        Tooltip.Hide();
        result.Show(bm);
    }

    static void Set(UIFade f, bool on) {
        if (f.Visible != on) f.Show(on);
    }

    // ------------------------------------------------------------------ building
    void Build() {
        // the glassy panels take the colour of the planet being fought on
        GameSession.CurrentWorld(out ArenaTheme worldTheme, out bool worldNight);
        UIKit.Accent = UIKit.AccentFor(worldTheme, worldNight);
        UIKit.LiveAccent = UIKit.Accent;   // holographic buttons in the HUD glow in the same colour
        Canvas canvas = UIKit.CreateCanvas(transform, "HUD Canvas", 10);
        canvasRoot = (RectTransform)canvas.transform;
        BuildTop();
        weaponBar = HudWeaponBar.Create(canvasRoot);
        GamblerChoiceUI.Create(canvasRoot);   // the Gambler's face-down cards and heads-or-tails call
        BuildAimPanel();
        BuildHints();
        pauseButton = UIKit.IconButton(canvasRoot, UIIcon.Pause, UIKit.Secondary, Pause, 66f);
        ((RectTransform)pauseButton.transform).Place(Vector2.zero, new Vector2(18f, 24f), new Vector2(66f, 66f));
        TooltipTrigger.Add(pauseButton, () => "Pause", () => "Esc");
        BuildBanner();
        BuildHotSeat();
        pauseMenu = HudPauseMenu.Create(canvasRoot, Resume);
        result = HudResultScreen.Create(canvasRoot);
    }

    void BuildTop() {
        RectTransform area = UIKit.Rect(canvasRoot, "Top");
        area.anchorMin = new Vector2(0f, 1f);
        area.anchorMax = Vector2.one;
        area.pivot = new Vector2(0.5f, 1f);
        area.sizeDelta = new Vector2(0f, 240f);
        area.anchoredPosition = Vector2.zero;
        top = area.gameObject.AddComponent<UIFade>();
        top.hiddenOffset = new Vector2(0f, 150f);
        top.hiddenScale = 1f;

        leftGroup = UIKit.Row(area, CardGap, TextAnchor.UpperLeft);
        leftGroup.Place(new Vector2(0f, 1f), new Vector2(20f, -16f), new Vector2(900f, HudFortressCard.Height));
        rightGroup = UIKit.Row(area, CardGap, TextAnchor.UpperRight);
        rightGroup.Place(new Vector2(1f, 1f), new Vector2(-20f, -16f), new Vector2(1100f, HudFortressCard.Height));

        centre = UIKit.Rect(area, "Centre").Place(new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(400f, 220f));
        turnBox = UIKit.Panel(centre, UIKit.PanelColor, 22f).Place(new Vector2(0.5f, 1f), Vector2.zero, new Vector2(390f, 92f));
        turnNumber = UIKit.Label(turnBox, "", 19f, UIKit.TextDim);
        turnNumber.characterSpacing = 6f;
        turnNumber.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(360f, 24f));
        turnTitle = UIKit.Title(turnBox, "", 36f, UIKit.TextColor);
        turnTitle.enableAutoSizing = true;
        turnTitle.fontSizeMin = 20f;
        turnTitle.fontSizeMax = 36f;
        turnTitle.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(366f, 50f));
        turnGlow = UIKit.Img(turnBox, UIKit.Shadow, UIKit.Alpha(Color.white, 0f), "TurnGlow");
        turnGlow.rectTransform.Fill(-26f, -26f, -26f, -26f);
        turnGlow.rectTransform.SetAsFirstSibling();
        turnSweep = UISweep.Create(turnBox, 8f, new Color(1f, 1f, 1f, 0.35f));

        RectTransform wind = UIKit.Panel(centre, UIKit.PanelColor, 22f).Place(new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(260f, 46f));
        UIKit.Label(wind, "WIND", 18f, UIKit.TextDim, TextAlignmentOptions.Left).rectTransform
             .Place(new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(70f, 30f));
        windArrows = UIKit.Rect(wind, "Arrows").Place(new Vector2(0.5f, 0.5f), new Vector2(4f, 0f), new Vector2(84f, 30f));
        windChevrons = new Image[3];
        for (int i = 0; i < 3; i++) {
            windChevrons[i] = UIKit.Img(windArrows, UIKit.Icon(UIIcon.Chevron), UIKit.TextColor, "Chevron");
            windChevrons[i].rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 22f, 0f), new Vector2(26f, 26f));
        }
        windText = UIKit.Label(wind, "Calm", 24f, UIKit.TextColor, TextAlignmentOptions.Right);
        windText.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(80f, 32f));

        RectTransform shot = UIKit.Rect(centre, "Callout").Place(new Vector2(0.5f, 1f), new Vector2(0f, -156f), new Vector2(520f, 54f));
        callout = shot.gameObject.AddComponent<UIFade>();
        callout.hiddenOffset = new Vector2(0f, 16f);
        UIKit.Panel(shot, new Color(0.05f, 0.06f, 0.1f, 0.8f), 27f, false).Fill();
        calloutIcon = UIKit.Rect(shot, "Icon").Place(new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(44f, 44f));
        calloutText = UIKit.Label(shot, "", 24f, UIKit.TextColor, TextAlignmentOptions.Left);
        calloutText.enableAutoSizing = true;
        calloutText.fontSizeMin = 16f;
        calloutText.fontSizeMax = 24f;
        calloutText.overflowMode = TextOverflowModes.Ellipsis;
        calloutText.rectTransform.Fill(60f, 0f, 16f, 0f);

        callout.Show(false, true);
        top.Show(false, true);
    }

    void BuildAimPanel() {
        RectTransform panel = UIKit.Panel(canvasRoot, UIKit.PanelColor, 22f);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = Vector2.zero;
        panel.anchoredPosition = new Vector2(316f, 24f);
        panel.sizeDelta = new Vector2(250f, 128f);
        aimPanel = panel.gameObject.AddComponent<UIFade>();
        aimPanel.hiddenOffset = new Vector2(-30f, 0f);
        powerGlow = UIKit.Img(panel, UIKit.Shadow, UIKit.Alpha(WeaponDefs.Hex("FF5A3D"), 0f), "PowerGlow");
        powerGlow.rectTransform.Fill(-30f, -30f, -30f, -30f);
        powerGlow.rectTransform.SetAsFirstSibling();
        UIKit.Label(panel, "ANGLE", 17f, UIKit.TextDim).rectTransform.Place(new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(105f, 22f));
        powerLabel = UIKit.Label(panel, "POWER", 17f, UIKit.TextDim);
        powerLabel.rectTransform.Place(new Vector2(1f, 1f), new Vector2(-14f, -10f), new Vector2(105f, 22f));
        angleText = UIKit.Title(panel, "", 44f, UIKit.TextColor);
        angleText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(14f, -32f), new Vector2(105f, 52f));
        powerText = UIKit.Title(panel, "", 44f, UIKit.Gold);
        powerText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(-14f, -32f), new Vector2(105f, 52f));
        powerBar = UIBar.Create(panel, WeaponDefs.Hex("FF5A3D"), WeaponDefs.Hex("FFE066"), 12f);
        ((RectTransform)powerBar.transform).Place(new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(218f, 12f));
        aimPanel.Show(false, true);
    }

    void BuildHints() {
        // Mid-air activation ("Click to DETONATE"), pulsing above the bottom edge.
        RectTransform act = UIKit.Rect(canvasRoot, "ActivateHint").Place(new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(600f, 78f));
        activateHint = act.gameObject.AddComponent<UIFade>();
        RectTransform pulse = UIKit.Rect(act, "Pulse").Fill();
        pulse.gameObject.AddComponent<UIPulse>().scaleAmount = 0.06f;
        UIKit.Panel(pulse, new Color(0.08f, 0.06f, 0.02f, 0.88f), 39f).Fill();
        UIKit.Img(pulse, UIKit.Outline, UIKit.Alpha(UIKit.Gold, 0.8f), "Rim").rectTransform.Fill();
        UIKit.Img(pulse, UIKit.Icon(UIIcon.Mouse), UIKit.Gold, "Mouse").rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(40f, 48f));
        activateText = UIKit.Title(pulse, "", 34f, UIKit.Gold);
        activateText.rectTransform.Fill(76f, 0f, 24f, 0f);
        activateHint.Show(false, true);

        // First-shot tutorial for each human fortress, or how to cast a weapon used on yourself.
        RectTransform tut = UIKit.Rect(canvasRoot, "Tutorial").Place(new Vector2(0.5f, 0f), new Vector2(0f, 214f), new Vector2(700f, 58f));
        tutorialHint = tut.gameObject.AddComponent<UIFade>();
        UIKit.Panel(tut, new Color(0.05f, 0.06f, 0.1f, 0.78f), 29f, false).Fill();
        UIKit.Img(tut, UIKit.Icon(UIIcon.Mouse), UIKit.TextColor, "Mouse").rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(30f, 36f));
        tutorialText = UIKit.Label(tut, DragHint, 26f, UIKit.TextColor);
        tutorialText.AutoSize(18f, 26f);
        tutorialText.rectTransform.Fill(60f, 0f, 20f, 0f);
        tutorialHint.Show(false, true);
    }

    void BuildBanner() {
        bannerRoot = UIKit.Rect(canvasRoot, "Banner").Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1500f, 250f));
        bannerGroup = bannerRoot.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.blocksRaycasts = false;
        UIKit.Img(bannerRoot, UIKit.Shadow, new Color(0f, 0f, 0f, 0.6f), "Band").rectTransform.Fill(-60f, -10f, -60f, -10f);
        bannerTitle = UIKit.Title(bannerRoot, "", 90f, Color.white);
        bannerTitle.enableAutoSizing = true;
        bannerTitle.fontSizeMin = 48f;
        bannerTitle.fontSizeMax = 90f;
        UIKit.Gradient(bannerTitle, WeaponDefs.Hex("FFF1C1"), WeaponDefs.Hex("F2A93B"));
        bannerTitle.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(1400f, 112f));
        bannerSub = UIKit.Title(bannerRoot, "", 36f, UIKit.TextColor);
        bannerSub.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -142f), new Vector2(1400f, 48f));
        skipHint = UIKit.Label(bannerRoot, "Click to skip", 22f, UIKit.TextDim, TextAlignmentOptions.Center, false);
        skipHint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(400f, 30f));
        RectTransform sweepArea = UIKit.Rect(bannerRoot, "SweepArea").Place(new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(1000f, 112f));
        bannerSweep = UISweep.Create(sweepArea, 0f, new Color(1f, 0.95f, 0.8f, 0.3f));
        bannerRoot.gameObject.SetActive(false);
    }

    void BuildHotSeat() {
        RectTransform root = UIKit.Rect(canvasRoot, "HotSeat").Fill();
        hotSeat = root.gameObject.AddComponent<UIFade>();
        hotSeat.hiddenOffset = Vector2.zero;
        hotSeat.hiddenScale = 1f;   // the dismiss button below also owns this object's scale
        hotSeat.speed = 7f;
        Image dim = UIKit.Img(root, null, new Color(0.01f, 0.02f, 0.05f, 0.6f), "Dim");
        dim.rectTransform.Fill();
        dim.raycastTarget = true;   // the first click only dismisses this card
        UIButton dismiss = root.gameObject.AddComponent<UIButton>();
        dismiss.hoverScale = dismiss.pressScale = 1f;
        dismiss.hoverSound = false;
        dismiss.fx = ClickFxStyle.None;   // a click anywhere just dismisses the card: no burst
        dismiss.onClick = () => Set(hotSeat, false);

        UIKit.Img(root, UIKit.Glow, new Color(1f, 1f, 1f, 0.12f), "Halo").rectTransform
             .Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(420f, 420f));
        UIKit.Img(root, UIKit.Ring, Color.white, "Ring").rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(206f, 206f));
        hotSeatPortrait = UIKit.Rect(root, "Portrait").Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(190f, 190f));
        hotSeatTitle = UIKit.Title(root, "", 104f, Color.white);
        hotSeatTitle.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(1400f, 120f));
        UIKit.Title(root, "Get ready!", 44f, UIKit.TextColor).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, -130f), new Vector2(1000f, 56f));
        UIKit.Label(root, "Click to start", 24f, UIKit.TextDim, TextAlignmentOptions.Center, false).rectTransform
             .Place(new Vector2(0.5f, 0.5f), new Vector2(0f, -190f), new Vector2(600f, 34f));
        hotSeat.Show(false, true);
    }
}

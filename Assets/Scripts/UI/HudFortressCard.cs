using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Top-bar card for one fortress: class portrait in the team colour, name, commander HP and fortress
// integrity (with the defeat threshold marked). Glows while this fortress is shooting; greys out with a
// skull once it is defeated (and comes back if Dalí's clocks rewind the defeat). Under the card, status
// chips show the lasting effects on this fortress (TurnEffect.StatusOn) and "Slowed" while its throws are
// weakened. Reads the fortress every frame, so it never misses a change.
public class HudFortressCard : MonoBehaviour {
    public const float Height = 108f;
    const float ChipHeight = 28f, ChipGap = 6f;
    static readonly Color SlowColor = WeaponDefs.Hex("8E9BFF");

    Fortress fortress;
    UIBar hpBar, wallBar;
    TextMeshProUGUI nameText, hpText, wallText;
    Image glow;
    CanvasGroup group;
    GameObject skull;
    RectTransform body, chipRow;
    readonly List<Chip> chips = new List<Chip>();
    int shownHP = -1, shownWall = -1;
    bool defeatedShown;
    float lift;

    Color WallColor => Color.Lerp(fortress.TeamColor, Color.white, 0.25f);

    public static HudFortressCard Create(Transform parent, Fortress f, float width, float defeatIntegrity) {
        RectTransform root = UIKit.Rect(parent, "Card " + f.DisplayName);
        root.Size(width, Height);
        HudFortressCard card = root.gameObject.AddComponent<HudFortressCard>();
        card.fortress = f;
        card.group = root.gameObject.AddComponent<CanvasGroup>();
        card.group.blocksRaycasts = false;
        card.Build(root, width, defeatIntegrity);
        return card;
    }

    void Build(RectTransform root, float width, float defeatIntegrity) {
        Color team = fortress.TeamColor;
        body = UIKit.Rect(root, "Body").Fill();
        glow = UIKit.Img(body, UIKit.Shadow, UIKit.Alpha(team, 0f), "Glow");
        glow.rectTransform.Fill(-34f, -34f, -34f, -34f);
        RectTransform panel = UIKit.Panel(body, UIKit.PanelColor, 20f).Fill();
        UIKit.RoundedImg(panel, team, 3f, "TeamStripe").rectTransform
             .Place(new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(width - 40f, 5f));

        UIKit.Img(panel, UIKit.Circle, team, "Ring").rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(88f, 88f));
        UIKit.PortraitIcon(panel, fortress.HeroClass, 80f).Place(new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(80f, 80f));
        RectTransform skullBack = UIKit.Img(panel, UIKit.Circle, new Color(0.05f, 0.05f, 0.08f, 0.75f), "Defeated").rectTransform
                                       .Place(new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(80f, 80f));
        UIKit.Img(skullBack, UIKit.Icon(UIIcon.Skull), WeaponDefs.Hex("F1E9DA"), "Skull").rectTransform.Fill(16f, 16f, 16f, 16f);
        skull = skullBack.gameObject;
        skull.SetActive(false);

        float x = 108f, barW = width - x - 26f - 62f;
        nameText = UIKit.Label(panel, fortress.DisplayName, 24f, UIKit.TextColor, TextAlignmentOptions.Left);
        nameText.enableAutoSizing = true;   // keeps the "A/B/C" suffix of enemy names readable on narrow cards
        nameText.fontSizeMin = 16f;
        nameText.fontSizeMax = 24f;
        nameText.overflowMode = TextOverflowModes.Ellipsis;
        nameText.rectTransform.Place(new Vector2(0f, 1f), new Vector2(x, -10f), new Vector2(width - x - 12f, 30f));

        hpBar = Row(panel, UIIcon.Heart, WeaponDefs.Hex("FF6B6B"), x, -50f, barW, UIKit.Good, UIKit.Danger, out hpText);
        wallBar = Row(panel, UIIcon.Tower, WeaponDefs.Hex("C9D3E6"), x, -78f, barW, WallColor, UIKit.Danger, out wallText);
        if (defeatIntegrity > 0f) {   // only when fortress ruin itself can lose the battle
            RectTransform marker = wallBar.AddMarker(defeatIntegrity, WeaponDefs.Hex("FF3B3B"));
            marker.sizeDelta = new Vector2(3f, 10f);
        }
        hpBar.SetValue(1f, true);
        wallBar.SetValue(1f, true);

        // Status chips hang under the card (outside the scaled body, so the turn glow does not move them).
        chipRow = UIKit.Row(root, ChipGap, TextAnchor.MiddleLeft);
        chipRow.Place(Vector2.zero, new Vector2(4f, -8f - ChipHeight), new Vector2(width - 8f, ChipHeight));
    }

    static UIBar Row(RectTransform panel, UIIcon icon, Color iconColor, float x, float y, float barW, Color high, Color low, out TextMeshProUGUI value) {
        UIKit.Img(panel, UIKit.Icon(icon), iconColor, icon.ToString()).rectTransform.Place(new Vector2(0f, 1f), new Vector2(x, y + 2f), new Vector2(20f, 20f));
        UIBar bar = UIBar.Create(panel, high, low, 14f);
        ((RectTransform)bar.transform).Place(new Vector2(0f, 1f), new Vector2(x + 26f, y), new Vector2(barW, 14f));
        value = UIKit.Label(panel, "", 20f, UIKit.TextColor, TextAlignmentOptions.Right);
        value.rectTransform.Place(new Vector2(1f, 1f), new Vector2(-12f, y + 6f), new Vector2(58f, 26f));
        return bar;
    }

    void Update() {
        if (fortress == null) return;
        Commander c = fortress.commander;
        float hp = c != null ? Mathf.Max(0f, c.hp) : 0f;
        float maxHP = c != null && c.maxHP > 0f ? c.maxHP : 1f;
        hpBar.SetValue(hp / maxHP);
        int hpInt = Mathf.CeilToInt(hp);
        if (hpInt != shownHP) { shownHP = hpInt; hpText.text = hpInt.ToString(); }

        float wall = fortress.Integrity;
        wallBar.SetValue(wall);
        int wallInt = Mathf.RoundToInt(wall * 100f);
        if (wallInt != shownWall) { shownWall = wallInt; wallText.text = wallInt + "%"; }

        BattleManager bm = BattleManager.I;
        bool current = bm != null && bm.Current == fortress && !fortress.defeated;
        float pulse = 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f);
        glow.color = UIKit.Alpha(glow.color, Mathf.Lerp(glow.color.a, current ? pulse : 0f, UIKit.Approach(10f)));
        lift = Mathf.Lerp(lift, current ? 1f : 0f, UIKit.Approach(10f));
        body.localScale = Vector3.one * (1f + 0.05f * lift);

        if (fortress.defeated) MarkDefeated();
        else if (defeatedShown && fortress.CommanderAlive) ShowDefeated(false);   // a time rewind undid the defeat
        UpdateChips();
    }

    // Greys the card out with a skull (called by the HUD on FortressDefeated; also caught by polling).
    public void MarkDefeated() { ShowDefeated(true); }

    void ShowDefeated(bool on) {
        if (defeatedShown == on) return;
        defeatedShown = on;
        skull.SetActive(on);
        group.alpha = on ? 0.55f : 1f;
        nameText.fontStyle = on ? FontStyles.Strikethrough : FontStyles.Normal;
        hpBar.SetColors(on ? UIKit.Disabled : UIKit.Good, on ? UIKit.Disabled : UIKit.Danger);
        wallBar.SetColors(on ? UIKit.Disabled : WallColor, on ? UIKit.Disabled : UIKit.Danger);
    }

    // One chip per lasting effect shown on this fortress, plus "Slowed". Chips are pooled and texts are only
    // reassigned when they change, so the per-frame poll does not allocate.
    void UpdateChips() {
        int used = 0;
        if (!fortress.defeated) {
            List<TurnEffect> effects = TurnEffect.All;
            for (int i = 0; i < effects.Count; i++) {
                TurnEffect e = effects[i];
                if (e == null || e.StatusOn != fortress) continue;
                string text = e.StatusText;
                if (!string.IsNullOrEmpty(text)) GetChip(used++).Show(text, e.StatusColor);
            }
            if (fortress.launchSpeedScale < 0.999f) GetChip(used++).Show("Slowed", SlowColor);
        }
        for (int i = used; i < chips.Count; i++) chips[i].Hide();
    }

    Chip GetChip(int index) {
        while (chips.Count <= index) chips.Add(Chip.Create(chipRow));
        return chips[index];
    }

    // A small pill: coloured dot + short text on a dark tint of the effect's colour. Sized by its text.
    class Chip {
        GameObject root;
        Image back, dot;
        TextMeshProUGUI label;
        string text;
        Color color = Color.clear;

        public static Chip Create(RectTransform row) {
            RectTransform rt = UIKit.Rect(row, "Chip");
            rt.Size(-1f, ChipHeight);
            HorizontalLayoutGroup g = UIKit.Layout<HorizontalLayoutGroup>(rt, 6f, TextAnchor.MiddleLeft);
            g.padding = new RectOffset(9, 12, 0, 0);
            Chip c = new Chip { root = rt.gameObject };
            c.back = UIKit.RoundedImg(rt, Color.black, ChipHeight * 0.5f, "Back");
            c.back.rectTransform.Fill();
            c.back.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            c.dot = UIKit.Img(rt, UIKit.Circle, Color.white, "Dot");
            c.dot.Size(10f, 10f);
            c.label = UIKit.Label(rt, "", 17f, UIKit.TextColor, TextAlignmentOptions.Left);
            return c;
        }

        public void Show(string t, Color c) {
            if (!root.activeSelf) root.SetActive(true);
            if (t != text) { text = t; label.text = t; }
            if (c != color) {
                color = c;
                back.color = UIKit.Alpha(UIKit.Darken(c, 0.62f), 0.92f);
                dot.color = c;
                label.color = UIKit.Lighten(c, 0.6f);
            }
        }

        public void Hide() { if (root.activeSelf) root.SetActive(false); }
    }
}

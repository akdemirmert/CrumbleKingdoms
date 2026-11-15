using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Campaign level select: ten level cards (number, name, mode badge, theme, stars, lock), the class
// you play with and your total stars.
public class MenuLevelPage {
    class LevelCard {
        public int index;
        public UIButton button;
        public CanvasGroup content;
        public GameObject lockOverlay, starRow;
        public Image[] stars;
    }

    readonly MainMenuUI menu;
    readonly List<LevelCard> cards = new List<LevelCard>();
    readonly RectTransform portraitSlot;
    readonly TextMeshProUGUI className, starsText;
    int shownClass = -1;

    public MenuLevelPage(MainMenuUI menu, RectTransform page) {
        this.menu = menu;

        // Info strip: class chip on the left, star total on the right.
        RectTransform info = UIKit.Rect(page, "Info").Place(new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(1636f, 78f));
        RectTransform chip = UIKit.Panel(info, UIKit.PanelColor, 39f, false).Place(new Vector2(0f, 0.5f), Vector2.zero, new Vector2(470f, 78f));
        portraitSlot = UIKit.Rect(chip, "Portrait").Place(new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(64f, 64f));
        UIKit.Label(chip, "PLAYING AS", 18f, UIKit.TextDim, TextAlignmentOptions.BottomLeft).rectTransform
             .Place(new Vector2(0f, 1f), new Vector2(86f, -8f), new Vector2(200f, 24f));
        className = UIKit.Label(chip, "", 32f, UIKit.TextColor, TextAlignmentOptions.TopLeft);
        className.rectTransform.Place(new Vector2(0f, 0f), new Vector2(86f, 8f), new Vector2(200f, 40f));
        UIButton change = UIKit.Button(chip, "Change", ButtonStyle.Secondary, () => menu.Open("class"), new Vector2(150f, 54f), 24f);
        ((RectTransform)change.transform).Place(new Vector2(1f, 0.5f), new Vector2(-14f, 2f), new Vector2(150f, 54f));

        RectTransform total = UIKit.Panel(info, UIKit.PanelColor, 39f, false).Place(new Vector2(1f, 0.5f), Vector2.zero, new Vector2(250f, 78f));
        UIKit.Img(total, UIKit.Icon(UIIcon.Star), UIKit.Gold, "Star").rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(48f, 48f));
        starsText = UIKit.Label(total, "", 34f, UIKit.TextColor, TextAlignmentOptions.Left);
        starsText.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(84f, 0f), new Vector2(160f, 50f));

        // Level grid, 5 x 2.
        RectTransform grid = UIKit.Rect(page, "Grid").Place(new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(1640f, 560f));
        GridLayoutGroup g = grid.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(308f, 262f);
        g.spacing = new Vector2(24f, 24f);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 5;
        g.childAlignment = TextAnchor.MiddleCenter;
        for (int i = 0; i < LevelDefs.Count; i++) cards.Add(BuildCard(grid, i));

        menu.BackButton(menu.BottomBar(page), "class");
    }

    public void Refresh() {
        starsText.text = SaveData.TotalStars + " / " + LevelDefs.Count * 3;
        if (shownClass != (int)menu.SelectedClass) {
            shownClass = (int)menu.SelectedClass;
            for (int i = portraitSlot.childCount - 1; i >= 0; i--) Object.Destroy(portraitSlot.GetChild(i).gameObject);
            UIKit.PortraitIcon(portraitSlot, menu.SelectedClass, 64f).Fill();
            ClassDef def = ClassDefs.Get(menu.SelectedClass);
            className.text = "<color=" + UIKit.HexOf(UIKit.Lighten(def.color, 0.3f)) + ">" + def.name + "</color>";
        }
        foreach (LevelCard c in cards) RefreshCard(c);
    }

    LevelCard BuildCard(Transform grid, int index) {
        LevelDef L = LevelDefs.Levels[index];
        Color theme = UIKit.ThemeColor(L.theme);
        LevelCard card = new LevelCard { index = index };
        RectTransform root = UIKit.Rect(grid, "Level " + (index + 1));

        RectTransform panel = UIKit.Panel(root, Color.Lerp(UIKit.PanelColor, UIKit.Alpha(theme, 0.95f), 0.16f), 22f, out Image bg).Fill();
        bg.raycastTarget = true;
        card.content = panel.gameObject.AddComponent<CanvasGroup>();
        UIKit.RoundedImg(panel, UIKit.Alpha(theme, 0.3f), 18f, "Header").rectTransform
             .Place(new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(292f, 84f));

        RectTransform badge = UIKit.Img(panel, UIKit.Circle, new Color(0.05f, 0.06f, 0.1f, 0.85f), "Number").rectTransform
                                   .Place(new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(66f, 66f));
        UIKit.Title(badge, (index + 1).ToString(), 36f, UIKit.Gold).rectTransform.Fill(0f, 2f, 0f, 0f);

        int enemies = L.enemies.Length;
        RectTransform mode = UIKit.RoundedImg(panel, UIKit.ModeColor(enemies), 16f, "Mode").rectTransform
                                  .Place(new Vector2(1f, 1f), new Vector2(-18f, -28f), new Vector2(100f, 40f));
        UIKit.Label(mode, L.ModeLabel, 24f, Color.white).rectTransform.Fill();

        TextMeshProUGUI name = UIKit.Label(panel, L.name, 29f, UIKit.TextColor, TextAlignmentOptions.Center);
        name.textWrappingMode = TextWrappingModes.Normal;
        name.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(284f, 70f));

        RectTransform themeRow = UIKit.Row(panel, 6f);
        themeRow.Place(new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(284f, 30f));
        if (L.night) UIKit.Img(themeRow, UIKit.Icon(UIIcon.Moon), WeaponDefs.Hex("C9D6FF"), "Moon").Size(22f, 22f);
        UIKit.Label(themeRow, L.theme + (L.night ? " · Night" : ""), 22f, UIKit.Lighten(theme, 0.3f), TextAlignmentOptions.Center, false)
             .Size(-1f, 30f);

        RectTransform stars = UIKit.Row(panel, 8f);
        stars.Place(new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(200f, 46f));
        card.starRow = stars.gameObject;
        card.stars = new Image[3];
        for (int s = 0; s < 3; s++) {
            card.stars[s] = UIKit.Img(stars, UIKit.Icon(UIIcon.Star), Color.white, "Star");
            card.stars[s].Size(s == 1 ? 44f : 38f, s == 1 ? 44f : 38f);
        }

        RectTransform lockOverlay = UIKit.RoundedImg(root, new Color(0.03f, 0.04f, 0.07f, 0.72f), 22f, "Locked").rectTransform;
        lockOverlay.Fill();
        UIKit.Img(lockOverlay, UIKit.Icon(UIIcon.Lock), UIKit.TextDim, "Lock").rectTransform
             .Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(64f, 64f));
        UIKit.Label(lockOverlay, "Locked", 24f, UIKit.TextDim).rectTransform
             .Place(new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(200f, 32f));
        card.lockOverlay = lockOverlay.gameObject;

        card.button = root.gameObject.AddComponent<UIButton>();
        card.button.hoverScale = 1.05f;
        card.button.onClick = () => Play(index);
        TooltipTrigger.Add(root, () => L.name, () => Describe(index));
        return card;
    }

    void RefreshCard(LevelCard c) {
        bool unlocked = SaveData.IsUnlocked(c.index);
        int earned = SaveData.GetStars(c.index);
        c.button.Interactable = unlocked;
        c.lockOverlay.SetActive(!unlocked);
        c.starRow.SetActive(unlocked);
        c.content.alpha = unlocked ? 1f : 0.55f;
        for (int s = 0; s < 3; s++) c.stars[s].color = s < earned ? UIKit.Gold : new Color(1f, 1f, 1f, 0.16f);
    }

    static string Describe(int index) {
        LevelDef L = LevelDefs.Levels[index];
        if (!SaveData.IsUnlocked(index)) return "Win level " + index + " to unlock.";
        string enemies = "";
        foreach (EnemyDef e in L.enemies) enemies += (enemies.Length > 0 ? ", " : "") + ClassDefs.Get(e.heroClass).name;
        string s = L.subtitle + "\n<color=#9AA3BA>Enemies:</color> " + enemies +
                   "\n<color=#9AA3BA>Wind:</color> " + (L.wind <= 0f ? "calm" : "up to " + L.wind.ToString("0.0"));
        int stars = SaveData.GetStars(index);
        if (stars > 0) s += "\n<color=#FFC845>Best: " + stars + (stars == 1 ? " star" : " stars") + "</color>";
        return s;
    }

    void Play(int index) {
        if (!SaveData.IsUnlocked(index)) return;
        GameSession.PlayerClass = menu.SelectedClass;
        GameSession.StartCampaignLevel(index);
    }
}

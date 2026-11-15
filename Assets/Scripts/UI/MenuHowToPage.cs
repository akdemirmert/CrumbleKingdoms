using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// How to play: three tabs (controls with key caps, the rules, every weapon grouped by class). The weapons
// tab shows three classes per page; arrows on the panel's sides and dots below flip through the pages.
public class MenuHowToPage {
    const int ClassesPerPage = 3, WeaponsTab = 2;
    const float InnerHeight = 600f;   // panel height inside the tab insets

    readonly GameObject[] tabs = new GameObject[3];
    readonly List<GameObject> weaponPages = new List<GameObject>();
    GameObject pager;                 // arrows + dots, shown with the weapons tab only
    UIButton prevButton, nextButton;
    Image prevIcon, nextIcon;
    Image[] dots;
    int weaponPage;

    public MenuHowToPage(MainMenuUI menu, RectTransform page) {
        RectTransform panel = UIKit.Panel(page, UIKit.PanelColor, 26f);
        panel.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(1580f, InnerHeight + 60f));
        tabs[0] = Controls(panel);
        tabs[1] = Rules(panel);
        tabs[2] = Weapons(panel);

        RectTransform tabRow = UIKit.Rect(page, "Tabs").Place(new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(700f, 64f));
        UIChoice.Row(tabRow, new[] { "Controls", "Rules", "Weapons" }, 0, UIKit.Gold, new Vector2(220f, 60f), 28f, ShowTab);
        ShowTab(0);
        menu.BackButton(menu.BottomBar(page), "title");
    }

    void ShowTab(int index) {
        for (int i = 0; i < tabs.Length; i++) tabs[i].SetActive(i == index);
        pager.SetActive(index == WeaponsTab);
    }

    static RectTransform TabRoot(RectTransform panel, string name) {
        return UIKit.Rect(panel, name).Fill(40f, 30f, 40f, 30f);
    }

    // ------------------------------------------------------------------ controls
    GameObject Controls(RectTransform panel) {
        RectTransform root = TabRoot(panel, "Controls");
        RectTransform left = UIKit.Column(root, 14f, TextAnchor.UpperLeft);
        left.Place(new Vector2(0f, 1f), Vector2.zero, new Vector2(740f, InnerHeight));
        RectTransform right = UIKit.Column(root, 14f, TextAnchor.UpperLeft);
        right.Place(new Vector2(1f, 1f), Vector2.zero, new Vector2(740f, InnerHeight));

        Keys(left, "Press anywhere, drag back like a slingshot and release to fire. A longer pull means more power.", Key("", UIIcon.Mouse));
        Keys(left, "Cancel the aim.", Key("Right click"), Key("Esc"));
        Keys(left, "Choose a weapon (or click its slot at the bottom).", Key("1"), Key("2"), Key("3"), Key("4"));
        Keys(left, "Weapons you cast on yourself (cloak, space warp) need no aim: just click or press Space.", Key("Click"), Key("Space"));
        Keys(left, "While your shot flies: detonate, strike, open a portal, release the bats… (weapons that say so).", Key("Click"), Key("Space"));

        Keys(right, "Change the launch angle.", Key("", UIIcon.Arrow, 180f), Key("", UIIcon.Arrow));
        Keys(right, "Change the power (hold Shift to go faster).", Key("", UIIcon.Arrow, 90f), Key("", UIIcon.Arrow, -90f));
        Keys(right, "Fire the keyboard aim.", Key("Space"), Key("Enter"));
        Keys(right, "Zoom with the mouse wheel, pan with a right or middle drag.", Key("Wheel"), Key("Drag"));
        Keys(right, "Pause the game.", Key("Esc"));
        return root.gameObject;
    }

    // A key cap description; built lazily inside Keys() so it lands in the right row.
    struct KeySpec {
        public string text;
        public UIIcon? icon;
        public float angle;
    }

    static KeySpec Key(string text, UIIcon? icon = null, float angle = 0f) { return new KeySpec { text = text, icon = icon, angle = angle }; }

    static void Keys(Transform column, string description, params KeySpec[] keys) {
        RectTransform row = UIKit.Row(column, 16f, TextAnchor.MiddleLeft);
        row.Size(740f, 96f);
        RectTransform caps = UIKit.Row(row, 8f, TextAnchor.MiddleRight);
        caps.Size(250f, 96f);
        foreach (KeySpec k in keys) UIKit.KeyCap(caps, k.text, k.icon, k.angle);
        UIKit.Paragraph(row, description, 24f, UIKit.TextColor, TextAlignmentOptions.Left).Size(470f, 96f);
    }

    // ------------------------------------------------------------------ rules
    GameObject Rules(RectTransform panel) {
        RectTransform root = TabRoot(panel, "Rules");
        RectTransform grid = UIKit.Rect(root, "Grid").Fill();
        GridLayoutGroup g = grid.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(735f, 136f);
        g.spacing = new Vector2(30f, 18f);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 2;

        Rule(grid, UIIcon.Chevron, "Turns", "Sides take turns. Against several enemy fortresses, the enemies fire one after another while you answer each of them.");
        Rule(grid, UIIcon.Coin, "Who starts", "In the campaign you always throw first. A duel starts with a coin toss, and in a rematch the other side begins.");
        Rule(grid, UIIcon.Arrow, "Wind", "A new wind blows every turn (see the top of the screen). Each weapon feels it differently: bombs drift, Odin's spear ignores it.");
        Rule(grid, UIIcon.Pause, "Cooldowns", "Slot 1 is always ready. Stronger weapons need a few of your turns to recharge; the number on the slot shows how many.");
        Rule(grid, UIIcon.Skull, "Defeat", "You lose only when your commander dies (hit, crushed, burned or drowned). A ruined fortress just leaves him exposed.");
        Rule(grid, UIIcon.Shield, "Protected commanders", "Each commander shelters in a walled, roofed chamber and throws from its roof. Every intact block between him and a blast lets only 40 % of it through: break in first.");
        Rule(grid, UIIcon.Star, "Stars", "Win a level to unlock the next. You earn 1–3 stars for how healthy your commander and fortress are at the end.");
        Rule(grid, UIIcon.Tower, "Campaign", "Levels 1–3 are 1 v 1, levels 4–7 are 1 v 2 and levels 8–10 are 1 v 3. The bots aim better every level and learn from their misses.");
        return root.gameObject;
    }

    static void Rule(Transform grid, UIIcon icon, string title, string text) {
        RectTransform cell = UIKit.RoundedImg(grid, new Color(1f, 1f, 1f, 0.04f), 18f, title).rectTransform;
        RectTransform badge = UIKit.Img(cell, UIKit.Circle, UIKit.Alpha(UIKit.Gold, 0.18f), "Badge").rectTransform
                                   .Place(new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(66f, 66f));
        UIKit.Img(badge, UIKit.Icon(icon), UIKit.Gold, "Icon").rectTransform.Fill(15f, 15f, 15f, 15f);
        UIKit.Label(cell, title, 28f, UIKit.Gold, TextAlignmentOptions.Left).rectTransform
             .Place(new Vector2(0f, 1f), new Vector2(102f, -12f), new Vector2(615f, 36f));
        TextMeshProUGUI body = UIKit.Paragraph(cell, text, 20f, UIKit.TextColor);
        body.AutoSize(15f, 20f);
        body.rectTransform.Place(new Vector2(0f, 1f), new Vector2(102f, -50f), new Vector2(615f, 78f));
    }

    // ------------------------------------------------------------------ weapons
    GameObject Weapons(RectTransform panel) {
        RectTransform root = TabRoot(panel, "Weapons");
        for (int first = 0; first < Classes.Count; first += ClassesPerPage) {
            RectTransform page = UIKit.Row(root, 24f, TextAnchor.UpperCenter);
            page.Fill();
            for (int c = first; c < Mathf.Min(Classes.Count, first + ClassesPerPage); c++) ClassColumn(page, (HeroClass)c);
            weaponPages.Add(page.gameObject);
        }
        BuildPager(panel);
        ShowWeaponPage(0);
        return root.gameObject;
    }

    static void ClassColumn(Transform page, HeroClass hc) {
        ClassDef def = ClassDefs.Get(hc);
        RectTransform col = UIKit.Column(page, 8f, TextAnchor.UpperLeft);
        col.Size(484f, InnerHeight);
        RectTransform header = UIKit.Rect(col, "Header");
        header.Size(484f, 56f);
        UIKit.PortraitIcon(header, hc, 56f).Place(new Vector2(0f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
        UIKit.Title(header, def.name.ToUpperInvariant(), 32f, UIKit.Lighten(def.color, 0.3f), TextAlignmentOptions.Left).rectTransform
             .Place(new Vector2(0f, 1f), new Vector2(68f, 0f), new Vector2(412f, 34f));
        TextMeshProUGUI tagline = UIKit.Label(header, def.tagline, 18f, UIKit.TextDim, TextAlignmentOptions.Left, false);
        tagline.fontStyle = FontStyles.Italic;
        tagline.AutoSize(14f, 18f);
        tagline.rectTransform.Place(new Vector2(0f, 0f), new Vector2(68f, 0f), new Vector2(412f, 22f));
        for (int slot = 0; slot < 4; slot++) WeaponRow(col, WeaponDefs.ForClass(hc, slot));
    }

    static void WeaponRow(Transform column, WeaponDef def) {
        RectTransform row = UIKit.RoundedImg(column, new Color(1f, 1f, 1f, 0.04f), 14f, def.name).rectTransform;
        row.Size(484f, 124f);
        UIKit.WeaponIcon(row, def.id, 88f).Place(new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(88f, 88f));
        TextMeshProUGUI title = UIKit.Label(row, (def.slot + 1) + "  " + def.title, 22f, UIKit.TextColor, TextAlignmentOptions.Left);
        title.AutoSize(16f, 22f);
        title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(116f, -7f), new Vector2(356f, 28f));
        TextMeshProUGUI desc = UIKit.Paragraph(row, def.description, 17f, UIKit.TextDim);
        desc.AutoSize(13f, 17f);   // the longest descriptions shrink a little to stay on three lines
        desc.rectTransform.Place(new Vector2(0f, 1f), new Vector2(116f, -37f), new Vector2(356f, 58f));
        string info = def.cooldown == 0 ? "No cooldown" : "Cooldown " + def.cooldown;
        if (def.selfCast) info = "Cast on yourself  ·  " + info;
        if (def.CanActivate) info += "  ·  " + def.activateHint;
        TextMeshProUGUI infoText = UIKit.Label(row, info, 16f, UIKit.Gold, TextAlignmentOptions.Left, false);
        infoText.AutoSize(12f, 16f);
        infoText.rectTransform.Place(new Vector2(0f, 0f), new Vector2(116f, 7f), new Vector2(356f, 22f));
    }

    // Arrows straddling the panel's left and right edges, and one dot per page under the columns.
    void BuildPager(RectTransform panel) {
        RectTransform root = UIKit.Rect(panel, "Pager").Fill();
        prevButton = ArrowButton(root, true, () => ShowWeaponPage(weaponPage - 1), out prevIcon);
        nextButton = ArrowButton(root, false, () => ShowWeaponPage(weaponPage + 1), out nextIcon);
        RectTransform dotRow = UIKit.Row(root, 12f);
        dotRow.Place(new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(240f, 16f));
        dots = new Image[weaponPages.Count];
        for (int i = 0; i < dots.Length; i++) {
            dots[i] = UIKit.Img(dotRow, UIKit.Circle, Color.white, "Dot");
            dots[i].Size(14f, 14f);
        }
        pager = root.gameObject;
    }

    static UIButton ArrowButton(RectTransform parent, bool left, System.Action onClick, out Image icon) {
        const float size = 64f;
        UIButton b = UIKit.IconButton(parent, UIIcon.Chevron, UIKit.Secondary, onClick, size);
        ((RectTransform)b.transform).Place(new Vector2(left ? 0f : 1f, 0.5f), new Vector2(left ? -size * 0.5f : size * 0.5f, 0f), new Vector2(size, size));
        icon = b.faceRect.Find("Icon").GetComponent<Image>();
        if (left) icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);   // the chevron points right
        return b;
    }

    void ShowWeaponPage(int index) {
        weaponPage = Mathf.Clamp(index, 0, weaponPages.Count - 1);
        for (int i = 0; i < weaponPages.Count; i++) {
            weaponPages[i].SetActive(i == weaponPage);
            dots[i].color = i == weaponPage ? UIKit.Gold : new Color(1f, 1f, 1f, 0.22f);
        }
        SetArrow(prevButton, prevIcon, weaponPage > 0);
        SetArrow(nextButton, nextIcon, weaponPage < weaponPages.Count - 1);
    }

    static void SetArrow(UIButton button, Image icon, bool usable) {
        button.Interactable = usable;
        icon.color = UIKit.Alpha(UIKit.TextColor, usable ? 1f : 0.3f);
    }
}

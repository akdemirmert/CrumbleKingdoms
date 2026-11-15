using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Main menu scene: title, class select, level select, duel setup, how to play and settings pages.
// The whole uGUI hierarchy is built from code; a diorama of the real game plays behind it.
// Page contents live in MenuClassPage, MenuLevelPage, MenuDuelPage and MenuHowToPage.
public class MainMenuUI : MonoBehaviour {
    public HeroClass SelectedClass { get; set; }   // campaign class, shared by the class and level pages

    readonly Dictionary<string, UIFade> pages = new Dictionary<string, UIFade>();
    Canvas canvas;
    string current;
    bool built, shownByCall;
    MenuClassPage classPage;
    MenuLevelPage levelPage;

    void Start() {
        EnsureBuilt();
        Music.PlayMenu();
        if (!shownByCall) Open(string.IsNullOrEmpty(GameSession.MenuStartPanel) ? "title" : GameSession.MenuStartPanel, true);
        GameSession.MenuStartPanel = null;
    }

    // Opens a page by name: "title", "class", "levels", "duel", "howto", "settings" (used by the playtest too).
    public void Show(string panel) {
        shownByCall = true;
        EnsureBuilt();
        Open(panel, false);
    }

    public void Open(string key, bool instant = false) {
        if (key == null || !pages.ContainsKey(key)) key = "title";
        if (key == "class") classPage.Refresh();
        if (key == "levels") levelPage.Refresh();
        foreach (KeyValuePair<string, UIFade> p in pages) p.Value.Show(p.Key == key, instant);
        current = key;
        Tooltip.Hide();
    }

    void Update() {
        Keyboard kb = Keyboard.current;
        if (kb == null || !built || !kb.escapeKey.wasPressedThisFrame) return;
        string back = current == "levels" ? "class" : current == "title" ? null : "title";
        if (back == null) return;
        SFX.Play(SoundId.UIBack);
        Open(back);
    }

    void EnsureBuilt() {
        if (built) return;
        built = true;
        SelectedClass = SaveData.LastClass;
        UIKit.EnsureEventSystem();
        MenuBackdrop.Create(transform);
        canvas = UIKit.CreateCanvas(transform, "Menu Canvas", 10);
        Vignette();
        BuildTitle(Page("title", null));
        classPage = new MenuClassPage(this, Page("class", "Choose your class"));
        levelPage = new MenuLevelPage(this, Page("levels", "Campaign"));
        new MenuDuelPage(this, Page("duel", "Duel"));
        new MenuHowToPage(this, Page("howto", "How to play"));
        BuildSettings(Page("settings", "Settings"));
        foreach (UIFade f in pages.Values) f.Show(false, true);
    }

    // ------------------------------------------------------------------ helpers for the pages
    // Full-screen page with a dimmed background and a heading (null = no heading, no dim).
    public RectTransform Page(string key, string heading) {
        RectTransform root = UIKit.Rect(canvas.transform, "Page " + key).Fill();
        pages[key] = root.gameObject.AddComponent<UIFade>();
        if (heading == null) return root;
        UIKit.Img(root, null, new Color(0.02f, 0.03f, 0.07f, 0.5f), "Dim").rectTransform.Fill();
        TextMeshProUGUI h = UIKit.Title(root, heading.ToUpperInvariant(), 66f, UIKit.TextColor);
        h.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(1400f, 86f));
        h.characterSpacing = 4f;
        UIKit.Gradient(h, Color.white, WeaponDefs.Hex("FFD98A"));
        return root;
    }

    // Row of navigation buttons at the bottom centre of a page.
    public RectTransform BottomBar(RectTransform page) {
        RectTransform row = UIKit.Row(page, 24f);
        row.Place(new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1200f, 90f));
        return row;
    }

    public UIButton BackButton(Transform parent, string target) {
        UIButton b = UIKit.Button(parent, "Back", ButtonStyle.Secondary, () => Open(target), new Vector2(230f, 80f), 34f);
        b.clickSound = SoundId.UIBack;
        return b;
    }

    // Darkens the top and bottom of the screen so text reads well over the 3D diorama.
    void Vignette() {
        Color edge = new Color(0.02f, 0.03f, 0.07f, 0.75f), clear = new Color(0.02f, 0.03f, 0.07f, 0f);
        Texture2D tex = TexKit.Gradient(edge, clear, clear, UIKit.Alpha(edge, 0.55f));
        UIKit.Img(canvas.transform, TexKit.ToSprite(tex), Color.white, "Vignette").rectTransform.Fill();
    }

    // ------------------------------------------------------------------ title page
    void BuildTitle(RectTransform page) {
        RectTransform logo = UIKit.Rect(page, "Logo").Place(new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(1400f, 270f));
        UIPulse breathe = logo.gameObject.AddComponent<UIPulse>();
        breathe.speed = 1.3f;
        breathe.scaleAmount = 0.012f;

        TextMeshProUGUI title = UIKit.Title(logo, "CRUMBLE KINGDOMS", 148f, Color.white);
        title.rectTransform.Place(new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1400f, 176f));
        title.characterSpacing = 5f;
        title.AutoSize(96f, 148f);
        UIKit.Gradient(title, WeaponDefs.Hex("FFF1C1"), WeaponDefs.Hex("F2A93B"));

        RectTransform column = UIKit.Column(page, 16f);
        column.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(480f, 520f));
        UIKit.Button(column, "Campaign", ButtonStyle.Primary, () => Open("class"), new Vector2(460f, 98f), 46f);
        UIKit.Button(column, "Duel  (1 v 1)", ButtonStyle.Secondary, () => Open("duel"), new Vector2(460f, 80f), 34f);
        UIKit.Button(column, "How to Play", ButtonStyle.Secondary, () => Open("howto"), new Vector2(460f, 80f), 34f);
        UIKit.Button(column, "Settings", ButtonStyle.Secondary, () => Open("settings"), new Vector2(460f, 80f), 34f);
        UIButton quit = UIKit.Button(column, "Quit", ButtonStyle.Ghost, Application.Quit, new Vector2(460f, 70f), 30f);
        quit.clickSound = SoundId.UIBack;
    }

    // ------------------------------------------------------------------ settings page
    void BuildSettings(RectTransform page) {
        RectTransform panel = UIKit.Panel(page, UIKit.PanelColor, 24f);
        panel.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(900f, 540f));
        RectTransform column = UIKit.Column(panel, 10f, TextAnchor.MiddleCenter);
        column.Fill(40f, 30f, 40f, 30f);
        SettingsPanel.Build(column, true, () => levelPage.Refresh());
        BackButton(BottomBar(page), "title");
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Duel set-up: both players' classes, Human/Bot for player 2 (with bot skill), arena theme, night and wind.
// Starts from the last duel's options so a rematch from the menu keeps the choices.
public class MenuDuelPage {
    const int TileColumns = 3;
    const float TileW = 226f, TileH = 108f, CompactTileH = 70f, TileGap = 12f;   // compact: three rows still fit the panel

    readonly DuelOptions options;
    TextMeshProUGUI p1Tagline, p2Tagline;
    GameObject difficultyRow;

    public MenuDuelPage(MainMenuUI menu, RectTransform page) {
        DuelOptions last = GameSession.Duel ?? new DuelOptions();
        options = new DuelOptions {
            p1Class = last.p1Class, p2Class = last.p2Class, p2Controller = last.p2Controller, botDifficulty = last.botDifficulty,
            theme = last.theme, night = last.night, wind = last.wind,
        };

        RectTransform players = UIKit.Row(page, 24f);
        players.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 88f), new Vector2(1564f, 410f));
        p1Tagline = PlayerPanel(players, 0);
        p2Tagline = PlayerPanel(players, 1);
        ArenaPanel(page);

        RectTransform bar = menu.BottomBar(page);
        menu.BackButton(bar, "title");
        UIKit.Button(bar, "Start Duel", ButtonStyle.Primary, StartDuel, new Vector2(340f, 86f), 40f);
        RefreshTaglines();
    }

    // ------------------------------------------------------------------ players
    TextMeshProUGUI PlayerPanel(Transform parent, int player) {
        RectTransform panel = UIKit.Panel(parent, UIKit.PanelColor, 24f);
        panel.Size(770f, 410f);
        Color team = LevelDefs.TeamColors[player];
        UIKit.RoundedImg(panel, team, 3f, "TeamStripe").rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(700f, 6f));
        UIKit.Title(panel, "PLAYER " + (player + 1), 40f, UIKit.Lighten(team, 0.25f), TextAlignmentOptions.Left).rectTransform
             .Place(new Vector2(0f, 1f), new Vector2(30f, -22f), new Vector2(360f, 54f));

        if (player == 1) {
            RectTransform who = UIKit.Rect(panel, "Controller").Place(new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(300f, 60f));
            UIChoice.Row(who, new[] { "Human", "Bot" }, (int)options.p2Controller, UIKit.Gold, new Vector2(140f, 54f), 26f,
                         i => { options.p2Controller = (ControllerType)i; RefreshTaglines(); });
        }

        // Compact grid of class tiles: centred rows of three (a short last row stays centred); with a third row the
        // tiles are shorter so it still fits.
        int rows = (Classes.Count + TileColumns - 1) / TileColumns;
        float tileH = rows > 2 ? CompactTileH : TileH;
        float width = TileColumns * TileW + (TileColumns - 1) * TileGap;
        RectTransform tiles = UIKit.Column(panel, TileGap, TextAnchor.UpperCenter);
        tiles.name = "Classes";
        tiles.Place(new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(width, rows * tileH + (rows - 1) * TileGap));
        UIChoice classes = new UIChoice(Color.white);
        Image[] backs = new Image[Classes.Count];
        GameObject[] rings = new GameObject[Classes.Count];
        RectTransform row = null;
        for (int i = 0; i < Classes.Count; i++) {
            if (i % TileColumns == 0) {
                row = UIKit.Row(tiles, TileGap);
                row.Size(width, tileH);
            }
            classes.Add(ClassTile(row, (HeroClass)i, tileH, out backs[i], out rings[i]));
        }
        classes.styler = (i, on) => {
            backs[i].color = on ? UIKit.Alpha(ClassDefs.Get((HeroClass)i).color, 0.4f) : UIKit.SlotColor;
            rings[i].SetActive(on);
        };
        classes.Select((int)(player == 0 ? options.p1Class : options.p2Class), false);
        classes.onChange = i => {
            if (player == 0) options.p1Class = (HeroClass)i; else options.p2Class = (HeroClass)i;
            RefreshTaglines();
        };

        TextMeshProUGUI tagline = UIKit.Label(panel, "", 26f, UIKit.TextDim, TextAlignmentOptions.Center, false);
        tagline.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(700f, 40f));
        if (player == 1) DifficultyRow(panel);
        return tagline;
    }

    // Portrait on the left, class name beside it; the selected tile is tinted and ringed.
    UIButton ClassTile(Transform parent, HeroClass c, float tileH, out Image back, out GameObject ring) {
        ClassDef def = ClassDefs.Get(c);
        RectTransform tile = UIKit.Rect(parent, def.name);
        tile.Size(TileW, tileH);
        float portrait = Mathf.Round(tileH * 0.78f), nameX = 13f + portrait + 15f;   // 84 px on full-size tiles
        back = UIKit.RoundedImg(tile, UIKit.SlotColor, 18f, "Back");
        back.rectTransform.Fill();
        back.raycastTarget = true;
        Image r = UIKit.Img(tile, UIKit.Ring, UIKit.Lighten(def.color, 0.2f), "Ring");
        r.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(portrait + 10f, portrait + 10f));
        ring = r.gameObject;
        UIKit.PortraitIcon(tile, c, portrait).Place(new Vector2(0f, 0.5f), new Vector2(13f, 0f), new Vector2(portrait, portrait));
        TextMeshProUGUI name = UIKit.Label(tile, def.name, 30f, UIKit.Lighten(def.color, 0.35f), TextAlignmentOptions.Left);
        name.AutoSize(18f, 30f);
        name.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(nameX, 0f), new Vector2(TileW - nameX - 10f, 44f));
        UIButton b = tile.gameObject.AddComponent<UIButton>();
        b.hoverScale = 1.04f;
        TooltipTrigger.Add(tile, () => def.name + " — " + def.tagline, () => def.description);
        return b;
    }

    void DifficultyRow(RectTransform panel) {
        RectTransform row = UIKit.Row(panel, 12f);
        row.Place(new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(720f, 58f));
        UIKit.Label(row, "Bot skill", 26f, UIKit.TextDim, TextAlignmentOptions.Right, false).Size(130f, 54f);
        UIChoice.Row(row, LevelDefs.BotDifficultyNames, Mathf.Clamp(options.botDifficulty, 0, 3), UIKit.Gold, new Vector2(128f, 54f), 24f,
                     i => options.botDifficulty = i);
        difficultyRow = row.gameObject;
    }

    void RefreshTaglines() {
        p1Tagline.text = ClassDefs.Get(options.p1Class).tagline;
        p2Tagline.text = ClassDefs.Get(options.p2Class).tagline;
        bool bot = options.p2Controller == ControllerType.Bot;
        p2Tagline.gameObject.SetActive(!bot);
        if (difficultyRow != null) difficultyRow.SetActive(bot);
    }

    // ------------------------------------------------------------------ arena
    void ArenaPanel(RectTransform page) {
        RectTransform panel = UIKit.Panel(page, UIKit.PanelColor, 24f);
        panel.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, -248f), new Vector2(1564f, 172f));
        UIKit.Title(panel, "ARENA", 38f, UIKit.TextColor, TextAlignmentOptions.Left).rectTransform
             .Place(new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(170f, 60f));

        RectTransform themes = UIKit.Row(panel, 12f);
        themes.Place(new Vector2(0f, 0.5f), new Vector2(210f, 0f), new Vector2(910f, 132f));
        UIChoice choice = new UIChoice(Color.white);
        int count = System.Enum.GetValues(typeof(ArenaTheme)).Length;
        Image[] borders = new Image[count];
        for (int i = 0; i < count; i++) choice.Add(ThemeTile(themes, (ArenaTheme)i, out borders[i]));
        choice.styler = (i, on) => borders[i].color = on ? UIKit.Lighten(UIKit.ThemeColor((ArenaTheme)i), 0.3f) : new Color(1f, 1f, 1f, 0.06f);
        choice.Select((int)options.theme, false);
        choice.onChange = i => options.theme = (ArenaTheme)i;

        SwitchColumn(panel, "Night", new Vector2(-220f, 0f), options.night, v => options.night = v);
        SwitchColumn(panel, "Wind", new Vector2(-40f, 0f), options.wind, v => options.wind = v);
    }

    UIButton ThemeTile(Transform parent, ArenaTheme theme, out Image border) {
        RectTransform tile = UIKit.Rect(parent, theme.ToString());
        tile.Size(172f, 128f);
        Image back = UIKit.RoundedImg(tile, UIKit.SlotColor, 16f, "Back");
        back.rectTransform.Fill();
        back.raycastTarget = true;
        Color c = UIKit.ThemeColor(theme);
        Image swatch = UIKit.RoundedImg(tile, c, 12f, "Swatch");
        swatch.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(150f, 66f));
        Image sheen = UIKit.RoundedImg(swatch.transform, new Color(1f, 1f, 1f, 0.22f), 8f, "Sheen");
        sheen.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        sheen.rectTransform.anchorMax = Vector2.one;
        sheen.rectTransform.offsetMin = new Vector2(4f, 0f);
        sheen.rectTransform.offsetMax = new Vector2(-4f, -4f);
        UIKit.Label(tile, theme.ToString(), 24f, UIKit.TextColor).rectTransform
             .Place(new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(170f, 36f));
        border = UIKit.Img(tile, UIKit.Outline, Color.white, "Border");
        border.pixelsPerUnitMultiplier = 2f;
        border.rectTransform.Fill();
        UIButton b = tile.gameObject.AddComponent<UIButton>();
        b.hoverScale = 1.05f;
        return b;
    }

    static void SwitchColumn(RectTransform panel, string label, Vector2 pos, bool value, System.Action<bool> onChange) {
        RectTransform col = UIKit.Column(panel, 10f, TextAnchor.MiddleCenter);
        col.Place(new Vector2(1f, 0.5f), pos, new Vector2(160f, 120f));
        UIKit.Label(col, label, 28f, UIKit.TextColor).Size(160f, 36f);
        UISwitch.Create(col, value, UIKit.Good, onChange);
    }

    void StartDuel() {
        GameSession.Duel = new DuelOptions {
            p1Class = options.p1Class, p2Class = options.p2Class, p2Controller = options.p2Controller,
            botDifficulty = options.botDifficulty, theme = options.theme, night = options.night, wind = options.wind,
            seed = Random.Range(1, 1000000),
        };
        GameSession.StartDuel();
    }
}

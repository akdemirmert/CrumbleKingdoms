using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Campaign class selection: one card per class in rows of three. A card shows the portrait, name, tagline,
// description and the four weapons (cooldown and self-cast badges, tooltips).
public class MenuClassPage {
    const int Columns = 3;
    const float CardW = 548f, CardH = 380f, GapX = 24f, GapY = 20f;
    const float TextX = 210f;                                          // left edge of the text column
    static readonly Vector2 PortraitCentre = new Vector2(104f, -109f); // from the card's top-left corner

    class Card {
        public Image glow, border;
        public CanvasGroup group;
        public GameObject selectedTag;
    }

    readonly MainMenuUI menu;
    readonly UIChoice choice = new UIChoice(Color.white);
    readonly List<Card> cards = new List<Card>();

    public MenuClassPage(MainMenuUI menu, RectTransform page) {
        this.menu = menu;
        int rows = (Classes.Count + Columns - 1) / Columns;
        Vector2 size = new Vector2(Columns * CardW + (Columns - 1) * GapX, rows * CardH + (rows - 1) * GapY);
        RectTransform grid = UIKit.Rect(page, "Cards").Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), size);
        GridLayoutGroup g = grid.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(CardW, CardH);
        g.spacing = new Vector2(GapX, GapY);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = Columns;
        g.childAlignment = TextAnchor.MiddleCenter;
        for (int i = 0; i < Classes.Count; i++) BuildCard(grid, (HeroClass)i);
        choice.styler = Style;
        choice.onChange = i => menu.SelectedClass = (HeroClass)i;

        RectTransform bar = menu.BottomBar(page);
        menu.BackButton(bar, "title");
        UIKit.Button(bar, "Continue", ButtonStyle.Primary, () => menu.Open("levels"), new Vector2(320f, 86f), 40f);
    }

    public void Refresh() { choice.Select((int)menu.SelectedClass, false); }

    void BuildCard(Transform parent, HeroClass c) {
        ClassDef def = ClassDefs.Get(c);
        Card card = new Card();
        RectTransform root = UIKit.Rect(parent, "Card " + def.name);
        card.group = root.gameObject.AddComponent<CanvasGroup>();

        card.glow = UIKit.Img(root, UIKit.Shadow, UIKit.Alpha(def.color, 0f), "Glow");
        card.glow.rectTransform.Fill(-26f, -26f, -26f, -26f);
        RectTransform panel = UIKit.Panel(root, UIKit.PanelColor, 24f, out Image bg).Fill();
        bg.raycastTarget = true;
        card.border = UIKit.Img(root, UIKit.Outline, UIKit.Alpha(def.color, 0.2f), "Border");
        card.border.pixelsPerUnitMultiplier = 32f / 24f;
        card.border.rectTransform.Fill();

        // Portrait on a tinted plate; the SELECTED tag hangs on the plate's lower edge.
        UIKit.RoundedImg(panel, UIKit.Alpha(def.color, 0.22f), 18f, "Plate").rectTransform
             .Place(new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(176f, 186f));
        Centre(UIKit.Img(panel, UIKit.Glow, UIKit.Alpha(def.color, 0.55f), "Halo").rectTransform, PortraitCentre, new Vector2(220f, 220f));
        Centre(UIKit.Img(panel, UIKit.Ring, UIKit.Lighten(def.color, 0.2f), "Ring").rectTransform, PortraitCentre, new Vector2(164f, 164f));
        Centre(UIKit.PortraitIcon(panel, c, 150f), PortraitCentre, new Vector2(150f, 150f));
        RectTransform tag = Badge(panel, "SELECTED", def.color, UIKit.TextOn(def.color), 19f);
        Centre(tag, new Vector2(PortraitCentre.x, -202f), new Vector2(136f, 32f));
        card.selectedTag = tag.gameObject;

        // Text column beside the portrait.
        float w = CardW - TextX - 18f;
        TextMeshProUGUI name = UIKit.Title(panel, def.name.ToUpperInvariant(), 44f, UIKit.Lighten(def.color, 0.35f), TextAlignmentOptions.Left);
        name.characterSpacing = 2f;
        name.AutoSize(28f, 44f);
        name.rectTransform.Place(new Vector2(0f, 1f), new Vector2(TextX, -16f), new Vector2(w, 54f));
        TextMeshProUGUI tagline = UIKit.Label(panel, def.tagline, 21f, UIKit.TextDim, TextAlignmentOptions.Left, false);
        tagline.fontStyle = FontStyles.Italic;
        tagline.AutoSize(15f, 21f);
        tagline.rectTransform.Place(new Vector2(0f, 1f), new Vector2(TextX, -70f), new Vector2(w, 28f));
        TextMeshProUGUI desc = UIKit.Paragraph(panel, def.description, 19f, UIKit.TextColor);
        desc.AutoSize(14f, 19f);
        desc.rectTransform.Place(new Vector2(0f, 1f), new Vector2(TextX, -104f), new Vector2(w, 100f));

        RectTransform weapons = UIKit.Row(panel, 12f, TextAnchor.UpperCenter);
        weapons.Place(new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(CardW - 32f, 128f));
        for (int slot = 0; slot < 4; slot++) WeaponTile(weapons, WeaponDefs.ForClass(c, slot));

        UIButton button = root.gameObject.AddComponent<UIButton>();
        button.hoverScale = 1.025f;
        button.pressScale = 0.985f;
        choice.Add(button);
        cards.Add(card);
    }

    // Weapon icon with its name, a cooldown badge and a SELF badge for weapons cast on yourself; the tooltip explains it.
    static void WeaponTile(Transform parent, WeaponDef def) {
        RectTransform tile = UIKit.Rect(parent, def.name);
        tile.Size(112f, 128f);
        Image slot = UIKit.RoundedImg(tile, UIKit.SlotColor, 16f, "Slot");
        slot.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(92f, 92f));
        slot.raycastTarget = true;
        UIKit.WeaponIcon(slot.transform, def.id, 86f).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(86f, 86f));
        if (def.cooldown > 0)
            Badge(slot.transform, "CD " + def.cooldown, new Color(0f, 0f, 0f, 0.75f), UIKit.TextDim, 16f)
                .Place(new Vector2(1f, 1f), new Vector2(8f, 8f), new Vector2(48f, 24f));
        if (def.selfCast)
            Badge(slot.transform, "SELF", UIKit.Gold, UIKit.TextOn(UIKit.Gold), 15f)
                .Place(new Vector2(0f, 1f), new Vector2(-8f, 8f), new Vector2(52f, 24f));
        TextMeshProUGUI name = UIKit.Label(tile, def.name, 19f, UIKit.TextColor);
        name.AutoSize(13f, 19f);
        name.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(118f, 28f));
        TooltipTrigger.Add(slot, () => def.title, () => UIKit.WeaponInfo(def));
    }

    // Small pill with a label (SELECTED tag, cooldown and self-cast badges). The caller places it.
    static RectTransform Badge(Transform parent, string text, Color back, Color textColor, float fontSize) {
        RectTransform rt = UIKit.RoundedImg(parent, back, 12f, "Badge " + text).rectTransform;
        UIKit.Label(rt, text, fontSize, textColor).rectTransform.Fill(0f, 1f, 0f, 0f);
        return rt;
    }

    // Places a rect by its centre, measured from the top-left corner of its parent.
    static RectTransform Centre(RectTransform rt, Vector2 centre, Vector2 size) {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = centre;
        rt.sizeDelta = size;
        return rt;
    }

    void Style(int index, bool selected) {
        Card c = cards[index];
        Color color = ClassDefs.Get((HeroClass)index).color;
        c.glow.color = UIKit.Alpha(color, selected ? 0.85f : 0f);
        c.border.color = UIKit.Alpha(UIKit.Lighten(color, 0.2f), selected ? 1f : 0.2f);
        c.group.alpha = selected ? 1f : 0.78f;
        c.selectedTag.SetActive(selected);
    }
}

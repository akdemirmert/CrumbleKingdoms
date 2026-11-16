using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public enum ButtonStyle { Primary, Secondary, Danger, Ghost }
public enum UIIcon { Star, Lock, Skull, Heart, Tower, Chevron, Moon, Pause, Mouse, Arrow, Shield, Coin }

// Shared look of the menu and the battle HUD: palette, procedural sprites, canvas set-up and small
// factory methods. Every piece of UI in the game is built from code with these helpers (no prefabs).
public static class UIKit {
    // ------------------------------------------------------------------ palette
    public static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.12f, 0.88f);
    public static readonly Color SlotColor = WeaponDefs.Hex("1B2030");   // also the icon render background
    public static readonly Color TextColor = WeaponDefs.Hex("F4F1EA");
    public static readonly Color TextDim = new Color(0.68f, 0.72f, 0.82f);
    public static readonly Color Gold = WeaponDefs.Hex("FFC845");
    public static readonly Color Primary = WeaponDefs.Hex("F5A623");
    public static readonly Color Secondary = WeaponDefs.Hex("3A4766");
    public static readonly Color Danger = WeaponDefs.Hex("E5484D");
    public static readonly Color Good = WeaponDefs.Hex("4CD38A");
    public static readonly Color Disabled = new Color(0.3f, 0.32f, 0.38f, 1f);

    // Accent of the glassy panels (rim, star grid, glow). The menu uses the cool default; the battle HUD takes the
    // colour of the planet being fought on (BattleHUD sets it, MainMenuUI resets it).
    public static readonly Color DefaultAccent = WeaponDefs.Hex("8FB4FF");
    public static Color Accent = DefaultAccent;
    // The accent the buttons follow while the menu flies between places (blue at the title, amber at the observatory...).
    public static Color LiveAccent = DefaultAccent;
    public static readonly Color GlassFill = new Color(0.07f, 0.11f, 0.22f, 0.78f);

    // ---- holographic button shapes: angled corners (top-left and bottom-right cut deep), 9-sliced
    const int HoloN = 64, HoloBorder = 16;
    static Sprite holoFill, holoOutline, holoBracket;

    static float HoloCoverage(int px, int py, float k) {
        float sum = 0f;
        for (int sy = 0; sy < 3; sy++)
            for (int sx = 0; sx < 3; sx++) {
                float x = px + (sx + 0.5f) / 3f, y = py + (sy + 0.5f) / 3f, d = k * 1.4142f;
                bool inside = x >= k && x <= HoloN - k && y >= k && y <= HoloN - k
                           && x + (HoloN - y) >= 14f + d && (HoloN - x) + y >= 14f + d
                           && (HoloN - x) + (HoloN - y) >= 4f + d && x + y >= 4f + d;
                if (inside) sum += 1f / 9f;
            }
        return sum;
    }

    static Sprite HoloSprite(bool outline) {
        Texture2D t = new Texture2D(HoloN, HoloN, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = outline ? "HoloOutline" : "HoloFill" };
        Color[] px = new Color[HoloN * HoloN];
        for (int y = 0; y < HoloN; y++)
            for (int x = 0; x < HoloN; x++) {
                float a = outline ? Mathf.Clamp01(HoloCoverage(x, y, 0f) - HoloCoverage(x, y, 2.2f)) : HoloCoverage(x, y, 0f);
                px[y * HoloN + x] = new Color(1f, 1f, 1f, a);
            }
        t.SetPixels(px);
        t.Apply();
        return Sprite.Create(t, new Rect(0f, 0f, HoloN, HoloN), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                             new Vector4(HoloBorder, HoloBorder, HoloBorder, HoloBorder));
    }

    static Sprite HoloFill => holoFill != null ? holoFill : (holoFill = HoloSprite(false));
    static Sprite HoloOutline => holoOutline != null ? holoOutline : (holoOutline = HoloSprite(true));

    // Small L-shaped corner bracket (bottom-left; the other corner is this one rotated by 180 degrees).
    static Sprite HoloBracket {
        get {
            if (holoBracket != null) return holoBracket;
            const int N = 16;
            Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "HoloBracket" };
            Color[] px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    px[y * N + x] = new Color(1f, 1f, 1f, (x < 3 && y < 13) || (y < 3 && x < 13) ? 1f : 0f);
            t.SetPixels(px);
            t.Apply();
            holoBracket = Sprite.Create(t, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f), 100f);
            return holoBracket;
        }
    }

    public static Color AccentFor(ArenaTheme theme, bool night) {
        Color c = Lighten(ThemeColor(theme), 0.2f);
        return night ? Color.Lerp(c, WeaponDefs.Hex("9FB4FF"), 0.35f) : c;
    }

    // Faint field of tiny stars, tiled over panels.
    static Sprite starGrid;
    static Sprite StarGrid {
        get {
            if (starGrid != null) return starGrid;
            const int N = 32;
            Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "UIStarGrid" };
            Color[] px = new Color[N * N];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, 0f);
            Vector3[] dots = { new Vector3(8f, 8f, 1.4f), new Vector3(24f, 20f, 1f), new Vector3(15f, 27f, 0.8f) };
            foreach (Vector3 d in dots)
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++) {
                        float a = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(d.x, d.y)) / d.z);
                        if (a > px[y * N + x].a) px[y * N + x] = new Color(1f, 1f, 1f, a);
                    }
            t.SetPixels(px);
            t.Apply();
            starGrid = Sprite.Create(t, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            return starGrid;
        }
    }

    // Vertical colour ramp (bottom -> top), e.g. a shade behind the level cards.
    public static Sprite VerticalGradient(Color bottom, Color top) {
        Texture2D t = new Texture2D(2, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "UIGradient" };
        Color[] px = new Color[2 * 64];
        for (int y = 0; y < 64; y++) {
            Color c = Color.Lerp(bottom, top, y / 63f);
            px[y * 2] = c;
            px[y * 2 + 1] = c;
        }
        t.SetPixels(px);
        t.Apply();
        return Sprite.Create(t, new Rect(0f, 0f, 2f, 64f), new Vector2(0.5f, 0.5f), 100f);
    }

    public const int UILayer = 5;

    // ------------------------------------------------------------------ sprites (generated once)
    const int CornerPx = 32;   // corner radius baked into the rounded sprites
    static Sprite rounded, outline, shadow, circle, ring, glow;
    static readonly Sprite[] icons = new Sprite[Enum.GetValues(typeof(UIIcon)).Length];

    public static Sprite Rounded { get { if (rounded == null) rounded = TexKit.ToSprite(TexKit.RoundedRect(96, CornerPx), CornerPx + 1); return rounded; } }
    public static Sprite Outline { get { if (outline == null) outline = SlicedSdf(96, CornerPx + 1, (x, y, s) => Mathf.Abs(RoundedBoxPx(x, y, s, 0.5f, CornerPx) + 1.5f) - 1.5f); return outline; } }
    public static Sprite Shadow { get { if (shadow == null) shadow = MakeShadow(); return shadow; } }
    public static Sprite Circle { get { if (circle == null) circle = Paint(128, p => p.magnitude - 0.98f); return circle; } }
    public static Sprite Ring { get { if (ring == null) ring = Paint(128, p => Mathf.Abs(p.magnitude - 0.9f) - 0.08f); return ring; } }
    public static Sprite Glow { get { if (glow == null) glow = TexKit.ToSprite(TexKit.SoftCircle); return glow; } }

    public static Sprite Icon(UIIcon kind) {
        int i = (int)kind;
        if (icons[i] == null) icons[i] = Paint(128, IconShape(kind));
        return icons[i];
    }

    // ------------------------------------------------------------------ canvas
    public static Canvas CreateCanvas(Transform owner, string name, int sortingOrder) {
        GameObject go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
        go.transform.SetParent(owner, false);
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = sortingOrder;
        CanvasScaler cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return c;
    }

    // The scenes ship with an EventSystem; this only guards against a missing one.
    public static void EnsureEventSystem() {
        if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    // ------------------------------------------------------------------ rect helpers
    public static RectTransform Rect(Transform parent, string name) {
        GameObject go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    // Stretch over the parent with insets (left, bottom, right, top).
    public static RectTransform Fill(this RectTransform rt, float l = 0f, float b = 0f, float r = 0f, float t = 0f) {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        return rt;
    }

    // Fixed size, aligned to an anchor of the parent (pivot = anchor, so (0,1) means "top-left corner").
    public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size) {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static LayoutElement Size(this Component c, float width, float height = -1f) {
        if (!c.TryGetComponent(out LayoutElement le)) le = c.gameObject.AddComponent<LayoutElement>();
        if (width >= 0f) le.preferredWidth = le.minWidth = width;
        if (height >= 0f) le.preferredHeight = le.minHeight = height;
        return le;
    }

    public static T Layout<T>(Component c, float spacing, TextAnchor align, int pad = 0) where T : HorizontalOrVerticalLayoutGroup {
        T g = c.gameObject.AddComponent<T>();
        g.spacing = spacing;
        g.childAlignment = align;
        g.childControlWidth = g.childControlHeight = true;
        g.childForceExpandWidth = g.childForceExpandHeight = false;
        g.padding = new RectOffset(pad, pad, pad, pad);
        return g;
    }

    public static RectTransform Row(Transform parent, float spacing, TextAnchor align = TextAnchor.MiddleCenter) {
        RectTransform rt = Rect(parent, "Row");
        Layout<HorizontalLayoutGroup>(rt, spacing, align);
        return rt;
    }

    public static RectTransform Column(Transform parent, float spacing, TextAnchor align = TextAnchor.UpperCenter) {
        RectTransform rt = Rect(parent, "Column");
        Layout<VerticalLayoutGroup>(rt, spacing, align);
        return rt;
    }

    // ------------------------------------------------------------------ graphics
    public static Image Img(Transform parent, Sprite sprite, Color color, string name = "Image") {
        Image img = Rect(parent, name).gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null && sprite.border.sqrMagnitude > 0f) img.type = Image.Type.Sliced;
        return img;
    }

    // Rounded rectangle with the given corner radius (in canvas units).
    public static Image RoundedImg(Transform parent, Color color, float radius, string name = "Rounded") {
        Image img = Img(parent, Rounded, color, name);
        img.pixelsPerUnitMultiplier = CornerPx / Mathf.Max(1f, radius);
        return img;
    }

    // Dark rounded panel with a soft drop shadow and a faint rim. Content goes under the returned root.
    public static RectTransform Panel(Transform parent, Color color, float radius, out Image background, bool shadow = true) {
        RectTransform root = Rect(parent, "Panel");
        if (shadow) {
            Image s = Img(root, Shadow, new Color(0f, 0f, 0f, 0.55f), "Shadow");
            s.rectTransform.Fill(-30f, -38f, -30f, -22f);
            Img(root, Shadow, Alpha(Accent, 0.10f), "AccentGlow").rectTransform.Fill(-16f, -16f, -16f, -16f);
        }
        background = RoundedImg(root, color, radius, "Background");
        background.rectTransform.Fill();
        // holographic glass: a faint star grid, a lighter upper half and a thin glowing rim in the accent colour
        float inset = Mathf.Max(6f, radius * 0.3f);
        Image stars = Img(root, StarGrid, Alpha(Accent, 0.07f), "StarGrid");
        stars.type = Image.Type.Tiled;
        stars.pixelsPerUnitMultiplier = 0.7f;
        stars.rectTransform.Fill(inset, inset, inset, inset);
        Image glass = RoundedImg(root, new Color(1f, 1f, 1f, 0.035f), radius, "Glass");
        glass.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        glass.rectTransform.anchorMax = Vector2.one;
        glass.rectTransform.offsetMin = new Vector2(3f, 0f);
        glass.rectTransform.offsetMax = new Vector2(-3f, -3f);
        Image rim = Img(root, Outline, Alpha(Accent, 0.34f), "Rim");
        rim.pixelsPerUnitMultiplier = background.pixelsPerUnitMultiplier;
        rim.rectTransform.Fill();
        return root;
    }

    public static RectTransform Panel(Transform parent, Color color, float radius, bool shadow = true) {
        return Panel(parent, color, radius, out _, shadow);
    }

    // ------------------------------------------------------------------ text
    public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color,
                                        TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = true) {
        TextMeshProUGUI t = Rect(parent, "Text").gameObject.AddComponent<TextMeshProUGUI>();
        GameAssets a = GameAssets.I;
        TMP_FontAsset font = a == null ? null : bold ? a.fontBold : a.fontMedium;
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    // Big bold text with the outline + shadow material (titles over the 3D world).
    public static TextMeshProUGUI Title(Transform parent, string text, float size, Color color,
                                        TextAlignmentOptions align = TextAlignmentOptions.Center) {
        TextMeshProUGUI t = Label(parent, text, size, color, align, true);
        GameAssets a = GameAssets.I;
        if (a != null && a.fontBoldOutline != null && a.fontBold != null) t.fontSharedMaterial = a.fontBoldOutline;
        return t;
    }

    // Wrapping paragraph text.
    public static TextMeshProUGUI Paragraph(Transform parent, string text, float size, Color color,
                                            TextAlignmentOptions align = TextAlignmentOptions.TopLeft) {
        TextMeshProUGUI t = Label(parent, text, size, color, align, false);
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    // Lets TMP shrink the text (down to `min`) so it stays inside its rect.
    public static TMP_Text AutoSize(this TMP_Text t, float min, float max) {
        t.enableAutoSizing = true;
        t.fontSizeMin = min;
        t.fontSizeMax = max;
        return t;
    }

    // Vertical gold-ish gradient on a title.
    public static void Gradient(TMP_Text t, Color top, Color bottom) {
        t.enableVertexGradient = true;
        t.colorGradient = new VertexGradient(top, top, bottom, bottom);
        t.color = Color.white;
    }

    // ------------------------------------------------------------------ buttons
    public static Color StyleColor(ButtonStyle s) {
        switch (s) {
            case ButtonStyle.Primary: return Primary;
            case ButtonStyle.Danger: return Danger;
            case ButtonStyle.Ghost: return new Color(1f, 1f, 1f, 0.08f);
            default: return Secondary;
        }
    }

    // Chunky game button: coloured face over a darker "lip", a sheen on top, bold label.
    // Holographic button: a glass plate with angled corners, a thin glowing outline, corner brackets and a scan line
    // that sweeps across on hover. The glow follows LiveAccent (HoloSkin), so buttons take the colour of the place the
    // menu camera is at. Primary = brighter filled glass, Ghost = barely there, Danger = red.
    public static UIButton Button(Transform parent, string text, ButtonStyle style, Action onClick, Vector2 size, float fontSize = 32f) {
        RectTransform root = Rect(parent, "Button " + text);
        root.sizeDelta = size;
        root.Size(size.x, size.y);
        bool primary = style == ButtonStyle.Primary, ghost = style == ButtonStyle.Ghost, danger = style == ButtonStyle.Danger;
        Color acc = danger ? Danger : LiveAccent;
        Color fill = primary ? Alpha(Color.Lerp(acc, Color.black, 0.5f), 0.92f)
                   : danger ? Alpha(Color.Lerp(Danger, Color.black, 0.55f), 0.85f)
                   : ghost ? Alpha(GlassFill, 0.4f) : GlassFill;

        Image glow = Img(root, Shadow, Alpha(acc, 0.14f), "Glow");
        glow.rectTransform.Fill(-18f, -18f, -18f, -18f);
        Image face = Img(root, HoloFill, fill, "Face");
        face.type = Image.Type.Sliced;
        face.pixelsPerUnitMultiplier = 1f;
        face.rectTransform.Fill();
        face.raycastTarget = true;
        Image outline = Img(face.transform, HoloOutline, Alpha(acc, 0.8f), "Outline");
        outline.type = Image.Type.Sliced;
        outline.pixelsPerUnitMultiplier = 1f;
        outline.rectTransform.Fill();
        Image b1 = Img(face.transform, HoloBracket, Lighten(acc, 0.5f), "Bracket");
        b1.rectTransform.Place(new Vector2(0f, 0f), new Vector2(-1f, -1f), new Vector2(16f, 16f)).pivot = new Vector2(0f, 0f);
        Image b2 = Img(face.transform, HoloBracket, Lighten(acc, 0.5f), "Bracket");
        RectTransform r2 = b2.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(16f, 16f));
        r2.pivot = new Vector2(1f, 1f);
        r2.localEulerAngles = new Vector3(0f, 0f, 180f);
        UISweep sweep = UISweep.Create(face.rectTransform, 4f, new Color(1f, 1f, 1f, 0.3f));

        Color textColor = primary ? Color.white : TextColor;
        TextMeshProUGUI label = Label(face.transform, text, fontSize, textColor);
        label.rectTransform.Fill(12f, 2f, 12f, 0f);
        label.enableAutoSizing = true;
        label.fontSizeMin = 14f;
        label.fontSizeMax = fontSize;

        UIButton b = root.gameObject.AddComponent<UIButton>();
        b.face = face;
        b.faceRect = face.rectTransform;
        b.label = label;
        b.SetColor(fill, true);
        b.onClick = onClick;
        b.sweep = sweep;
        b.fx = primary ? ClickFxStyle.Primary : ClickFxStyle.Normal;
        root.gameObject.AddComponent<HoloSkin>().Init(b, glow, outline, new[] { b1, b2 }, primary, !danger);
        return b;
    }

    // Square button with an icon instead of text (pause button, pickers).
    public static UIButton IconButton(Transform parent, UIIcon icon, Color color, Action onClick, float size) {
        UIButton b = Button(parent, "", ButtonStyle.Secondary, onClick, new Vector2(size, size));
        b.SetColor(color, true);
        Image img = Img(b.faceRect, Icon(icon), TextColor, "Icon");
        img.rectTransform.Fill(size * 0.26f, size * 0.26f, size * 0.26f, size * 0.26f);
        return b;
    }

    // ------------------------------------------------------------------ icons with a fallback
    // Rendered 3D icon of a weapon, or a coloured circle with its initial if rendering failed.
    public static RectTransform WeaponIcon(Transform parent, WeaponId id, float size) {
        WeaponDef def = WeaponDefs.Get(id);
        return SpriteOrInitial(parent, IconRenderer.Weapon(id), def.color, def.name, size);
    }

    public static RectTransform PortraitIcon(Transform parent, HeroClass c, float size) {
        ClassDef def = ClassDefs.Get(c);
        return SpriteOrInitial(parent, IconRenderer.Portrait(c), def.color, def.name, size);
    }

    static RectTransform SpriteOrInitial(Transform parent, Sprite sprite, Color color, string name, float size) {
        RectTransform root = Rect(parent, "Icon " + name);
        root.sizeDelta = new Vector2(size, size);
        if (sprite != null) {
            Img(root, sprite, Color.white, "Render").rectTransform.Fill();
        } else {
            Img(root, Circle, Darken(color, 0.25f), "Fallback").rectTransform.Fill(size * 0.06f, size * 0.06f, size * 0.06f, size * 0.06f);
            Label(root, name.Substring(0, 1), size * 0.5f, TextColor).rectTransform.Fill();
        }
        return root;
    }

    // ------------------------------------------------------------------ game texts and colours
    // Tooltip body for a weapon: description, cooldown and how it is used (mid-air action or self-cast).
    public static string WeaponInfo(WeaponDef d) {
        string s = d.description + "\n<color=#9AA3BA>Cooldown: </color>" + (d.cooldown == 0 ? "none" : d.cooldown + (d.cooldown == 1 ? " turn" : " turns"));
        if (d.CanActivate) s += "\n<color=#FFC845>In flight: " + d.activateHint + "</color>";
        if (d.selfCast) s += "\n<color=#FFC845>Cast on yourself: click or Space, no aiming</color>";
        return s;
    }

    // Black or white text, whichever reads better on `background`.
    public static Color TextOn(Color background) {
        float luma = 0.3f * background.r + 0.59f * background.g + 0.11f * background.b;
        return luma > 0.45f ? WeaponDefs.Hex("1E1406") : TextColor;
    }

    public static Color ThemeColor(ArenaTheme t) {
        switch (t) {
            case ArenaTheme.Desert: return WeaponDefs.Hex("E2B35E");
            case ArenaTheme.Frost: return WeaponDefs.Hex("8FD3F4");
            case ArenaTheme.Volcano: return WeaponDefs.Hex("E5603A");
            case ArenaTheme.Asgard: return WeaponDefs.Hex("B48CFF");
            default: return WeaponDefs.Hex("6DBE5A");
        }
    }

    // 1 v 1 blue, 1 v 2 orange, 1 v 3 red.
    public static Color ModeColor(int enemies) {
        return enemies <= 1 ? WeaponDefs.Hex("3A86FF") : enemies == 2 ? WeaponDefs.Hex("F08A24") : WeaponDefs.Hex("E5484D");
    }

    // Keyboard key cap with a label ("Esc", "1") or an icon (arrows, mouse), rotated by `iconAngle`.
    public static RectTransform KeyCap(Transform parent, string text, UIIcon? icon = null, float iconAngle = 0f) {
        float w = icon.HasValue ? 52f : Mathf.Max(52f, 22f + text.Length * 15f);
        RectTransform root = Rect(parent, "Key " + text);
        root.sizeDelta = new Vector2(w, 52f);
        root.Size(w, 52f);
        RoundedImg(root, WeaponDefs.Hex("8C93A8"), 10f, "Lip").rectTransform.Fill(0f, -4f, 0f, 4f);
        Image face = RoundedImg(root, WeaponDefs.Hex("E9ECF4"), 10f, "Face");
        face.rectTransform.Fill();
        if (icon.HasValue) {
            Image img = Img(face.transform, Icon(icon.Value), WeaponDefs.Hex("2A3040"), "Icon");
            img.rectTransform.Fill(12f, 12f, 12f, 12f);
            img.rectTransform.localEulerAngles = new Vector3(0f, 0f, iconAngle);
        } else {
            Label(face.transform, text, 24f, WeaponDefs.Hex("2A3040")).rectTransform.Fill(4f, 2f, 4f, 0f);
        }
        return root;
    }

    // ------------------------------------------------------------------ colour helpers
    public static Color Darken(Color c, float k) { return new Color(c.r * (1f - k), c.g * (1f - k), c.b * (1f - k), c.a); }
    public static Color Lighten(Color c, float k) { return Color.Lerp(c, new Color(1f, 1f, 1f, c.a), k); }
    public static Color Alpha(Color c, float a) { c.a = a; return c; }
    public static string HexOf(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }

    // Smooth exponential approach that is frame-rate independent (UI runs on unscaled time).
    public static float Approach(float rate) { return 1f - Mathf.Exp(-rate * Time.unscaledDeltaTime); }

    public static float EaseOutBack(float t) {
        t = Mathf.Clamp01(t) - 1f;
        return 1f + t * t * (2.70158f * t + 1.70158f);
    }

    // ------------------------------------------------------------------ procedural sprite painting
    // Shapes are signed distance functions over p in [-1, 1]² (negative = inside); edges are anti-aliased.
    static Sprite Paint(int size, Func<Vector2, float> sdf) {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true) {
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, name = "UIIcon",
        };
        Color32[] px = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) {
                Vector2 p = new Vector2((x + 0.5f) / half - 1f, (y + 0.5f) / half - 1f);
                float a = Mathf.Clamp01(0.5f - sdf(p) * half);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        t.SetPixels32(px);
        t.Apply(true);
        return TexKit.ToSprite(t);
    }

    delegate float PixelSdf(float x, float y, int size);

    // 9-sliced sprite from a distance function in pixel units.
    static Sprite SlicedSdf(int size, int border, PixelSdf sdf) {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "UISliced" };
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) {
                float a = Mathf.Clamp01(0.5f - sdf(x + 0.5f, y + 0.5f, size));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        t.SetPixels32(px);
        t.Apply();
        return TexKit.ToSprite(t, border);
    }

    // Soft blurred rounded rectangle used as a drop shadow (blur 32 px around a 64 px core).
    static Sprite MakeShadow() {
        const int size = 128, blur = 32;
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "UIShadow" };
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) {
                float d = RoundedBoxPx(x + 0.5f, y + 0.5f, size, blur, 16f);
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d + 6f) / (blur + 6f)));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
        t.SetPixels32(px);
        t.Apply();
        return TexKit.ToSprite(t, blur + 16);
    }

    // Distance (px) to a rounded box inset by `inset` from the texture edges.
    static float RoundedBoxPx(float x, float y, int size, float inset, float radius) {
        float h = size * 0.5f;
        float dx = Mathf.Abs(x - h) - (h - inset) + radius, dy = Mathf.Abs(y - h) - (h - inset) + radius;
        return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
    }

    static float Circ(Vector2 p, float cx, float cy, float r) { return (p - new Vector2(cx, cy)).magnitude - r; }

    static float Box(Vector2 p, float cx, float cy, float hx, float hy, float r = 0f) {
        float dx = Mathf.Abs(p.x - cx) - hx + r, dy = Mathf.Abs(p.y - cy) - hy + r;
        return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0f) - r;
    }

    static float Capsule(Vector2 p, float ax, float ay, float bx, float by, float r) {
        Vector2 a = new Vector2(ax, ay), ba = new Vector2(bx, by) - a, pa = p - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - r;
    }

    // Exact distance to any simple polygon (after Inigo Quilez).
    static float Polygon(Vector2 p, Vector2[] v) {
        float d = Vector2.Dot(p - v[0], p - v[0]), s = 1f;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++) {
            Vector2 e = v[j] - v[i], w = p - v[i];
            Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
            d = Mathf.Min(d, Vector2.Dot(b, b));
            bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
            if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
        }
        return s * Mathf.Sqrt(d);
    }

    static Vector2[] StarPoints() {
        Vector2[] v = new Vector2[10];
        for (int i = 0; i < 10; i++) {
            float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f, r = i % 2 == 0 ? 0.97f : 0.43f;
            v[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r - 0.06f);
        }
        return v;
    }

    static Func<Vector2, float> IconShape(UIIcon kind) {
        switch (kind) {
            case UIIcon.Star: {
                Vector2[] star = StarPoints();
                return p => Polygon(p, star) - 0.03f;   // slightly rounded tips
            }
            case UIIcon.Lock:
                return p => {
                    float body = Box(p, 0f, -0.32f, 0.62f, 0.5f, 0.14f);
                    float arc = p.y > 0.18f ? Mathf.Abs(Circ(p, 0f, 0.18f, 0.38f)) - 0.11f : 1f;
                    float legs = Mathf.Min(Box(p, -0.38f, 0.08f, 0.11f, 0.12f), Box(p, 0.38f, 0.08f, 0.11f, 0.12f));
                    float hole = Mathf.Min(Circ(p, 0f, -0.24f, 0.13f), Box(p, 0f, -0.46f, 0.06f, 0.18f));
                    return Mathf.Max(Mathf.Min(body, Mathf.Min(arc, legs)), -hole);
                };
            case UIIcon.Skull:
                return p => {
                    float head = Mathf.Min(Circ(p, 0f, 0.16f, 0.72f), Box(p, 0f, -0.52f, 0.42f, 0.32f, 0.12f));
                    float eyes = Mathf.Min(Circ(p, -0.29f, 0.08f, 0.2f), Circ(p, 0.29f, 0.08f, 0.2f));
                    float nose = Polygon(p, new[] { new Vector2(0f, -0.14f), new Vector2(-0.1f, -0.32f), new Vector2(0.1f, -0.32f) });
                    float teeth = Mathf.Min(Box(p, -0.14f, -0.7f, 0.035f, 0.16f), Box(p, 0.14f, -0.7f, 0.035f, 0.16f));
                    return Mathf.Max(head, -Mathf.Min(Mathf.Min(eyes, nose), teeth));
                };
            case UIIcon.Heart:
                return p => Mathf.Min(Mathf.Min(Circ(p, -0.4f, 0.32f, 0.47f), Circ(p, 0.4f, 0.32f, 0.47f)),
                                      Polygon(p, new[] { new Vector2(-0.84f, 0.14f), new Vector2(0f, -0.88f), new Vector2(0.84f, 0.14f), new Vector2(0f, 0.4f) }));
            case UIIcon.Tower:
                return p => {
                    float body = Box(p, 0f, -0.28f, 0.56f, 0.64f);
                    float merlons = Mathf.Min(Box(p, -0.42f, 0.5f, 0.14f, 0.2f), Mathf.Min(Box(p, 0f, 0.5f, 0.14f, 0.2f), Box(p, 0.42f, 0.5f, 0.14f, 0.2f)));
                    float door = Box(p, 0f, -0.62f, 0.2f, 0.3f, 0.19f);
                    return Mathf.Max(Mathf.Min(body, merlons), -door);
                };
            case UIIcon.Chevron:
                return p => Mathf.Min(Capsule(p, -0.28f, 0.62f, 0.32f, 0f, 0.17f), Capsule(p, 0.32f, 0f, -0.28f, -0.62f, 0.17f));
            case UIIcon.Moon:
                return p => Mathf.Max(Circ(p, -0.05f, 0f, 0.8f), -Circ(p, 0.38f, 0.3f, 0.66f));
            case UIIcon.Pause:
                return p => Mathf.Min(Box(p, -0.3f, 0f, 0.17f, 0.64f, 0.07f), Box(p, 0.3f, 0f, 0.17f, 0.64f, 0.07f));
            case UIIcon.Mouse:
                return p => Mathf.Min(Mathf.Abs(Box(p, 0f, 0f, 0.5f, 0.82f, 0.48f)) - 0.09f, Capsule(p, 0f, 0.6f, 0f, 0.26f, 0.08f));
            case UIIcon.Shield: {   // heater shield with an engraved inner border
                Vector2[] shield = { new Vector2(-0.74f, 0.8f), new Vector2(0.74f, 0.8f), new Vector2(0.74f, 0.12f), new Vector2(0.56f, -0.36f),
                                     new Vector2(0f, -0.9f), new Vector2(-0.56f, -0.36f), new Vector2(-0.74f, 0.12f) };
                return p => {
                    float d = Polygon(p, shield);
                    return Mathf.Max(d - 0.04f, -(Mathf.Abs(d + 0.19f) - 0.05f));
                };
            }
            case UIIcon.Coin:       // disc with an engraved rim and a "1"
                return p => Mathf.Max(Circ(p, 0f, 0f, 0.9f), -Mathf.Min(Mathf.Abs(Circ(p, 0f, 0f, 0.66f)) - 0.05f, Box(p, 0f, 0f, 0.09f, 0.34f, 0.05f)));
            default: {   // Arrow pointing right
                Vector2[] arrow = { new Vector2(-0.72f, 0.15f), new Vector2(0.08f, 0.15f), new Vector2(0.08f, 0.5f), new Vector2(0.78f, 0f),
                                    new Vector2(0.08f, -0.5f), new Vector2(0.08f, -0.15f), new Vector2(-0.72f, -0.15f) };
                return p => Polygon(p, arrow);
            }
        }
    }
}

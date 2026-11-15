using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Small interactive building blocks. All animation runs on unscaled time so the UI keeps moving while
// the game is paused or in slow motion.

// Hover/press feedback (scale + colour + a pushed-down face), UI sounds and a click callback.
public class UIButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler {
    public Action onClick;
    public Image face;                 // tinted by state (optional)
    public RectTransform faceRect;     // pushed down while pressed (optional, must be stretched)
    public TextMeshProUGUI label;
    public SoundId? clickSound = SoundId.UIClick;
    public bool hoverSound = true;
    public float hoverScale = 1.04f, pressScale = 0.96f;

    Color baseColor = Color.white;
    bool interactable = true, over, down;
    float scale = 1f, push;

    public bool Interactable { get => interactable; set => interactable = value; }

    public void SetColor(Color c, bool instant = false) {
        baseColor = c;
        if (instant && face != null) face.color = TargetColor();
    }

    Color TargetColor() {
        if (!interactable) return Color.Lerp(baseColor, UIKit.Disabled, 0.65f);
        if (down) return UIKit.Darken(baseColor, 0.08f);
        if (over) return UIKit.Lighten(baseColor, 0.14f);
        return baseColor;
    }

    public void OnPointerEnter(PointerEventData e) {
        over = true;
        if (interactable && hoverSound) SFX.Play(SoundId.UIHover, null, 0.4f);
    }

    public void OnPointerExit(PointerEventData e) { over = down = false; }

    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) down = true; }

    public void OnPointerUp(PointerEventData e) { down = false; }

    public void OnPointerClick(PointerEventData e) {
        if (!interactable || e.button != PointerEventData.InputButton.Left) return;
        if (clickSound.HasValue) SFX.Play(clickSound.Value);
        onClick?.Invoke();
    }

    void OnDisable() {
        over = down = false;
        scale = 1f;
        push = 0f;
        transform.localScale = Vector3.one;
        if (faceRect != null) faceRect.offsetMin = faceRect.offsetMax = Vector2.zero;
    }

    void Update() {
        float k = UIKit.Approach(18f);
        float target = !interactable ? 1f : down ? pressScale : over ? hoverScale : 1f;
        if (scale != target) {   // only touch the transform while animating (keeps the canvas from rebuilding)
            scale = Mathf.Abs(target - scale) < 0.001f ? target : Mathf.Lerp(scale, target, k);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
        if (face != null) face.color = Color.Lerp(face.color, TargetColor(), k);
        float pushTarget = down && interactable ? 5f : 0f;
        if (faceRect != null && push != pushTarget) {
            push = Mathf.Abs(pushTarget - push) < 0.05f ? pushTarget : Mathf.Lerp(push, pushTarget, k);
            faceRect.offsetMin = new Vector2(0f, -push);
            faceRect.offsetMax = new Vector2(0f, -push);
        }
    }
}

// On/off pill switch.
public class UISwitch : MonoBehaviour {
    public Action<bool> onChange;
    Image track;
    RectTransform knob;
    Color accent;
    bool value;
    float pos;

    const float Width = 92f, Height = 46f, Knob = 38f;

    public bool Value { get => value; set => this.value = value; }

    public static UISwitch Create(Transform parent, bool value, Color accent, Action<bool> onChange) {
        RectTransform root = UIKit.Rect(parent, "Switch");
        root.sizeDelta = new Vector2(Width, Height);
        root.Size(Width, Height);
        UISwitch s = root.gameObject.AddComponent<UISwitch>();
        s.track = UIKit.RoundedImg(root, UIKit.Disabled, Height * 0.5f, "Track");
        s.track.rectTransform.Fill();
        s.track.raycastTarget = true;
        Image shadow = UIKit.Img(root, UIKit.Circle, new Color(0f, 0f, 0f, 0.3f), "KnobShadow");
        Image knob = UIKit.Img(shadow.transform, UIKit.Circle, Color.white, "Knob");
        knob.rectTransform.Fill(0f, 3f, 0f, -3f);
        s.knob = shadow.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(Knob, Knob));
        s.value = value;
        s.pos = value ? 1f : 0f;
        s.accent = accent;
        s.onChange = onChange;
        UIButton b = root.gameObject.AddComponent<UIButton>();
        b.hoverScale = 1.06f;
        b.onClick = () => { s.value = !s.value; s.onChange?.Invoke(s.value); };
        s.Update();
        return s;
    }

    void Update() {
        pos = Mathf.MoveTowards(pos, value ? 1f : 0f, Time.unscaledDeltaTime * 7f);
        float e = Mathf.SmoothStep(0f, 1f, pos);
        knob.anchoredPosition = new Vector2(Mathf.Lerp(4f, Width - Knob - 4f, e), 0f);
        track.color = Color.Lerp(UIKit.Disabled, accent, e);
    }
}

// Horizontal 0..1 slider in 5 % steps with a percentage readout.
public class UISlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {
    public Action<float> onChange;
    RectTransform area, fill, knob;
    TextMeshProUGUI readout;
    float value;
    bool dragging;

    const float Step = 0.05f;

    public static UISlider Create(Transform parent, float value, Color accent, float width, Action<float> onChange) {
        RectTransform root = UIKit.Rect(parent, "Slider");
        root.sizeDelta = new Vector2(width, 48f);
        root.Size(width, 48f);
        UISlider s = root.gameObject.AddComponent<UISlider>();
        Image hit = UIKit.Img(root, null, new Color(0f, 0f, 0f, 0f), "HitArea");   // invisible, catches the pointer
        hit.rectTransform.Fill();
        hit.raycastTarget = true;

        s.area = UIKit.Rect(root, "Area").Fill(14f, 0f, 96f, 0f);
        Image track = UIKit.RoundedImg(s.area, new Color(1f, 1f, 1f, 0.12f), 6f, "Track");
        track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        track.rectTransform.sizeDelta = new Vector2(0f, 12f);
        Image fill = UIKit.RoundedImg(track.transform, accent, 6f, "Fill");
        s.fill = fill.rectTransform;
        s.fill.anchorMin = Vector2.zero;
        s.fill.offsetMin = s.fill.offsetMax = Vector2.zero;

        Image shadow = UIKit.Img(s.area, UIKit.Circle, new Color(0f, 0f, 0f, 0.35f), "KnobShadow");
        s.knob = shadow.rectTransform;
        s.knob.sizeDelta = new Vector2(34f, 34f);
        UIKit.Img(s.knob, UIKit.Circle, Color.white, "Knob").rectTransform.Fill(0f, 3f, 0f, -3f);

        s.readout = UIKit.Label(root, "", 26f, UIKit.TextColor, TextAlignmentOptions.Right);
        s.readout.rectTransform.Place(new Vector2(1f, 0.5f), Vector2.zero, new Vector2(84f, 40f));
        s.value = Mathf.Clamp01(value);
        s.onChange = onChange;
        s.Refresh();
        return s;
    }

    public void OnPointerDown(PointerEventData e) { dragging = true; Drag(e); }
    public void OnDrag(PointerEventData e) { Drag(e); }

    public void OnPointerUp(PointerEventData e) {
        if (dragging) SFX.Play(SoundId.UIClick, null, 0.6f);   // lets the player hear the new volume
        dragging = false;
    }

    void Drag(PointerEventData e) {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out Vector2 local)) return;
        Rect r = area.rect;
        if (r.width <= 1f) return;
        float v = Mathf.Round(Mathf.Clamp01((local.x - r.xMin) / r.width) / Step) * Step;
        if (Mathf.Abs(v - value) < 0.001f) return;
        value = v;
        Refresh();
        onChange?.Invoke(value);
    }

    void Refresh() {
        fill.anchorMax = new Vector2(value, 1f);
        knob.anchorMin = knob.anchorMax = new Vector2(value, 0.5f);
        knob.anchoredPosition = Vector2.zero;
        readout.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}

// Health-style bar: the fill eases to the value, a pale "ghost" trails behind to show the damage taken.
public class UIBar : MonoBehaviour {
    RectTransform fill, ghost;
    Image fillImage;
    Color high, low;
    float target = 1f, shown = 1f, ghostValue = 1f, ghostDelay;

    public static UIBar Create(Transform parent, Color high, Color low, float height) {
        RectTransform root = UIKit.Rect(parent, "Bar");
        root.sizeDelta = new Vector2(100f, height);
        UIBar bar = root.gameObject.AddComponent<UIBar>();
        float r = height * 0.5f;
        UIKit.RoundedImg(root, new Color(0f, 0f, 0f, 0.5f), r, "Track").rectTransform.Fill();
        bar.ghost = UIKit.RoundedImg(root, new Color(1f, 0.93f, 0.75f, 0.85f), r, "Ghost").rectTransform;
        bar.fillImage = UIKit.RoundedImg(root, high, r, "Fill");
        bar.fill = bar.fillImage.rectTransform;
        Image sheen = UIKit.RoundedImg(bar.fill, new Color(1f, 1f, 1f, 0.22f), r * 0.5f, "Sheen");
        sheen.rectTransform.anchorMin = new Vector2(0f, 0.55f);
        sheen.rectTransform.anchorMax = Vector2.one;
        sheen.rectTransform.offsetMin = new Vector2(3f, 0f);
        sheen.rectTransform.offsetMax = new Vector2(-3f, -2f);
        bar.high = high;
        bar.low = low;
        bar.Apply();
        return bar;
    }

    public void SetValue(float v, bool instant = false) {
        v = Mathf.Clamp01(v);
        if (v < target - 0.001f) ghostDelay = 0.45f;   // hold the ghost for a moment, then let it drain
        target = v;
        if (instant || v > ghostValue) { shown = ghostValue = v; }
        Apply();
    }

    public void SetColors(Color high, Color low) { this.high = high; this.low = low; }

    // Thin tick at a fraction of the bar (e.g. the defeat threshold).
    public RectTransform AddMarker(float at, Color color) {
        RectTransform rt = UIKit.Img(transform, null, color, "Marker").rectTransform;
        rt.anchorMin = new Vector2(at, 0f);
        rt.anchorMax = new Vector2(at, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(3f, 8f);
        return rt;
    }

    void Update() {
        float dt = Time.unscaledDeltaTime;
        shown = Mathf.Abs(target - shown) < 0.001f ? target : Mathf.Lerp(shown, target, UIKit.Approach(10f));
        if (ghostDelay > 0f) ghostDelay -= dt;
        else ghostValue = Mathf.MoveTowards(ghostValue, target, dt * 0.6f);
        Apply();
    }

    void Apply() {
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(shown, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        ghost.anchorMin = Vector2.zero;
        ghost.anchorMax = new Vector2(Mathf.Max(ghostValue, shown), 1f);
        ghost.offsetMin = ghost.offsetMax = Vector2.zero;
        fillImage.color = Color.Lerp(low, high, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.7f, shown)));
        fillImage.enabled = shown > 0.004f;
    }
}

// Fades/slides a panel in and out (CanvasGroup) and deactivates it once hidden.
[RequireComponent(typeof(CanvasGroup))]
public class UIFade : MonoBehaviour {
    public Vector2 hiddenOffset = new Vector2(0f, -28f);
    public float hiddenScale = 0.97f, speed = 6f;

    CanvasGroup group;
    RectTransform rt;
    Vector2 basePos;
    float t;
    bool visible, ready;

    public bool Visible => visible;

    public void Show(bool on, bool instant = false) {
        Init();
        visible = on;
        if (on && !gameObject.activeSelf) gameObject.SetActive(true);
        group.blocksRaycasts = on;
        group.interactable = on;
        if (instant) {
            t = on ? 1f : 0f;
            Apply();
            if (!on) gameObject.SetActive(false);
        }
    }

    void Init() {
        if (ready) return;
        ready = true;
        group = GetComponent<CanvasGroup>();
        rt = (RectTransform)transform;
        basePos = rt.anchoredPosition;
    }

    void Update() {
        Init();
        t = Mathf.MoveTowards(t, visible ? 1f : 0f, Time.unscaledDeltaTime * speed);
        Apply();
        if (!visible && t <= 0f) gameObject.SetActive(false);
    }

    void Apply() {
        float e = visible ? 1f - Mathf.Pow(1f - t, 3f) : t * t;
        group.alpha = t;
        rt.anchoredPosition = basePos + hiddenOffset * (1f - e);
        float s = Mathf.Lerp(hiddenScale, 1f, e);
        rt.localScale = new Vector3(s, s, 1f);
    }
}

// A group of buttons where exactly one is selected (tabs, difficulty, Human/Bot, class and theme tiles).
// By default the selected button takes the accent colour; `styler` can draw selection differently.
public class UIChoice {
    public readonly System.Collections.Generic.List<UIButton> buttons = new System.Collections.Generic.List<UIButton>();
    public Action<int> onChange;
    public Action<int, bool> styler;     // (index, selected) -> custom visuals
    public int Selected { get; private set; } = -1;
    readonly Color accent;

    public UIChoice(Color accent) { this.accent = accent; }

    public UIButton Add(UIButton b) {
        int index = buttons.Count;
        buttons.Add(b);
        b.onClick = () => { if (index != Selected) Select(index, true); };
        return b;
    }

    public void Select(int index, bool notify) {
        Selected = index;
        for (int i = 0; i < buttons.Count; i++) {
            bool on = i == index;
            if (styler != null) { styler(i, on); continue; }
            buttons[i].SetColor(on ? accent : UIKit.Secondary);
            if (buttons[i].label != null) buttons[i].label.color = on && Luma(accent) > 0.55f ? WeaponDefs.Hex("2A1A05") : UIKit.TextColor;
        }
        if (notify) onChange?.Invoke(index);
    }

    // A row of text buttons.
    public static UIChoice Row(Transform parent, string[] labels, int selected, Color accent, Vector2 size, float fontSize, Action<int> onChange) {
        UIChoice c = new UIChoice(accent);
        RectTransform row = UIKit.Row(parent, 10f);
        foreach (string l in labels) c.Add(UIKit.Button(row, l, ButtonStyle.Secondary, null, size, fontSize));
        c.Select(selected, false);
        c.onChange = onChange;
        return c;
    }

    static float Luma(Color c) { return 0.3f * c.r + 0.59f * c.g + 0.11f * c.b; }
}

// Gentle breathing scale for things that want attention.
public class UIPulse : MonoBehaviour {
    public float speed = 4f, scaleAmount = 0.05f;

    void Update() {
        float k = 1f + scaleAmount * (Mathf.Sin(Time.unscaledTime * speed) + 1f) * 0.5f;
        transform.localScale = new Vector3(k, k, 1f);
    }
}

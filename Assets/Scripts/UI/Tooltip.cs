using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// One floating tooltip per scene (its own top-most canvas). It follows the mouse and flips to stay on screen.
public class Tooltip : MonoBehaviour {
    static Tooltip instance;

    const float MaxWidth = 440f, Pad = 18f, CursorGap = 22f;

    RectTransform canvasRect, panel;
    TextMeshProUGUI title, body;
    CanvasGroup group;
    bool showing;

    public static void Show(string titleText, string bodyText) {
        if (instance == null) Create();
        instance.Open(titleText, bodyText);
    }

    public static void Hide() {
        if (instance != null) instance.showing = false;
    }

    static void Create() {
        Canvas c = UIKit.CreateCanvas(null, "[Tooltip]", 500);
        instance = c.gameObject.AddComponent<Tooltip>();
        instance.canvasRect = (RectTransform)c.transform;
        instance.panel = UIKit.Panel(c.transform, new Color(0.05f, 0.06f, 0.1f, 0.96f), 14f);
        instance.panel.anchorMin = instance.panel.anchorMax = new Vector2(0.5f, 0.5f);
        instance.group = instance.panel.gameObject.AddComponent<CanvasGroup>();
        instance.group.blocksRaycasts = false;
        instance.group.alpha = 0f;
        instance.title = UIKit.Label(instance.panel, "", 26f, UIKit.Gold, TextAlignmentOptions.TopLeft);
        instance.body = UIKit.Paragraph(instance.panel, "", 21f, UIKit.TextColor);
    }

    void Open(string titleText, string bodyText) {
        showing = true;
        title.text = titleText ?? "";
        body.text = bodyText ?? "";
        body.gameObject.SetActive(!string.IsNullOrEmpty(bodyText));

        // Size the panel to its text (title on one line, body wrapped at MaxWidth).
        float titleW = title.GetPreferredValues(title.text).x;
        float bodyW = body.gameObject.activeSelf ? Mathf.Min(MaxWidth, body.GetPreferredValues(body.text).x) : 0f;
        float w = Mathf.Max(titleW, bodyW, 120f);
        float titleH = string.IsNullOrEmpty(title.text) ? 0f : 32f;
        float bodyH = body.gameObject.activeSelf ? body.GetPreferredValues(body.text, w, 0f).y : 0f;
        panel.sizeDelta = new Vector2(w + Pad * 2f, titleH + bodyH + Pad * 2f + (titleH > 0f && bodyH > 0f ? 6f : 0f));
        title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(Pad, -Pad), new Vector2(w, titleH));
        body.rectTransform.Place(new Vector2(0f, 0f), new Vector2(Pad, Pad), new Vector2(w, bodyH));
        Follow();
    }

    void LateUpdate() {
        group.alpha = Mathf.MoveTowards(group.alpha, showing ? 1f : 0f, Time.unscaledDeltaTime * 9f);
        if (showing) Follow();
    }

    void Follow() {
        Mouse m = Mouse.current;
        if (m == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, m.position.ReadValue(), null, out Vector2 local)) return;
        Rect area = canvasRect.rect;
        Vector2 size = panel.sizeDelta;
        // Prefer above-right of the cursor; flip when that would leave the screen.
        bool left = local.x + CursorGap + size.x > area.xMax;
        bool below = local.y + CursorGap + size.y > area.yMax;
        panel.pivot = new Vector2(left ? 1f : 0f, below ? 1f : 0f);
        Vector2 pos = local + new Vector2(left ? -CursorGap : CursorGap, below ? -CursorGap : CursorGap);
        pos.x = Mathf.Clamp(pos.x, area.xMin + (left ? size.x : 0f), area.xMax - (left ? 0f : size.x));
        pos.y = Mathf.Clamp(pos.y, area.yMin + (below ? size.y : 0f), area.yMax - (below ? 0f : size.y));
        panel.anchoredPosition = pos;
    }
}

// Shows a tooltip while the pointer is over this object (it needs a raycast-target graphic).
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    public Func<string> title, body;
    bool over;

    public static TooltipTrigger Add(Component target, Func<string> title, Func<string> body) {
        TooltipTrigger t = target.gameObject.AddComponent<TooltipTrigger>();
        t.title = title;
        t.body = body;
        return t;
    }

    public void OnPointerEnter(PointerEventData e) {
        over = true;
        Tooltip.Show(title?.Invoke(), body?.Invoke());
    }

    public void OnPointerExit(PointerEventData e) {
        over = false;
        Tooltip.Hide();
    }

    void OnDisable() {
        if (over) Tooltip.Hide();
        over = false;
    }
}

using UnityEngine;
using UnityEngine.UI;

// A soft diagonal glint that sweeps across a panel once (turn box, level banners). It lives in a clipped child
// rect, so it never leaves the panel. Runs on unscaled time like the rest of the UI.
public class UISweep : MonoBehaviour {
    const float Duration = 0.65f;

    RectTransform clip, bar;
    float t = -1f, delay;

    // Covers `parent` (minus `inset` on every side). The glint is invisible until Play().
    public static UISweep Create(RectTransform parent, float inset, Color color) {
        RectTransform clip = UIKit.Rect(parent, "Sweep").Fill(inset, inset, inset, inset);
        clip.gameObject.AddComponent<RectMask2D>();
        UISweep s = clip.gameObject.AddComponent<UISweep>();
        s.clip = clip;
        Image img = UIKit.Img(clip, UIKit.Glow, color, "Glint");
        s.bar = img.rectTransform;
        s.bar.anchorMin = s.bar.anchorMax = s.bar.pivot = new Vector2(0.5f, 0.5f);
        s.bar.localEulerAngles = new Vector3(0f, 0f, 18f);
        s.bar.gameObject.SetActive(false);
        return s;
    }

    public void Play(float startDelay = 0f) {
        delay = startDelay;
        t = 0f;
    }

    void Update() {
        if (t < 0f) return;
        if (delay > 0f) { delay -= Time.unscaledDeltaTime; return; }
        t += Time.unscaledDeltaTime / Duration;
        if (t >= 1f) {
            t = -1f;
            bar.gameObject.SetActive(false);
            return;
        }
        Rect r = clip.rect;
        if (!bar.gameObject.activeSelf) bar.gameObject.SetActive(true);
        float e = t * t * (3f - 2f * t);
        bar.sizeDelta = new Vector2(Mathf.Max(70f, r.width * 0.16f), r.height * 2.4f);
        bar.anchoredPosition = new Vector2(Mathf.Lerp(-0.62f, 0.62f, e) * r.width, 0f);
        Image img = bar.GetComponent<Image>();
        Color c = img.color;
        c.a = Mathf.Sin(t * Mathf.PI) * 0.9f;   // fades in and out around the middle of the sweep
        img.color = c;
    }
}

// Keeps a holographic button's glow, outline and brackets in the live accent colour: brighter on hover, flaring on click.
// Primary buttons also tint their glass fill with the accent. Danger buttons keep their red.
public class HoloSkin : MonoBehaviour {
    UIButton button;
    Image glow, outline;
    Image[] brackets;
    bool primary, followAccent;
    float hover;
    Color fixedColor = new Color(1f, 0.35f, 0.35f);

    public void Init(UIButton button, Image glow, Image outline, Image[] brackets, bool primary, bool followAccent) {
        this.button = button;
        this.glow = glow;
        this.outline = outline;
        this.brackets = brackets;
        this.primary = primary;
        this.followAccent = followAccent;
    }

    void Update() {
        if (button == null) return;
        Color acc = followAccent ? UIKit.LiveAccent : fixedColor;
        hover = Mathf.MoveTowards(hover, button.Hovered ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        float flare = button.FlashValue;
        float k = Mathf.Max(hover, flare);
        outline.color = Color.Lerp(UIKit.Alpha(acc, 0.8f), UIKit.Lighten(acc, 0.5f), k);
        glow.color = UIKit.Alpha(acc, Mathf.Lerp(0.14f, 0.5f, hover) + 0.4f * flare);
        Color bracket = UIKit.Lighten(acc, 0.5f);
        for (int i = 0; i < brackets.Length; i++) brackets[i].color = bracket;
        if (primary) button.SetColor(UIKit.Alpha(Color.Lerp(acc, Color.black, 0.5f), 0.92f));
    }
}

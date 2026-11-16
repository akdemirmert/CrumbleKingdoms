using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// How a button reacts when clicked. UIButton picks the style; ClickFX draws it on its own canvas, so the effect
// is the same in the menu and in the battle HUD and never disturbs the layout.
//   Normal  - a ring ripples out from the click point, a few star sparks fly off
//   Primary - shockwave: two rings, a big flash, a spray of gold sparks and a faint screen flash
//   Back    - a soft ring that shrinks inwards (Back, Quit, Cancel): the opposite of a "go" click
//   Weapon  - a pulse in the weapon's colour (HUD weapon slots, and when a weapon is ready again)
public enum ClickFxStyle { Normal, Primary, Back, Weapon, None }

public class ClickFX : MonoBehaviour {
    const int MaxLive = 140;
    static ClickFX instance;

    class Fx {
        public RectTransform rt;
        public Image img;
        public Vector2 from, travel;
        public float start, life, size0, size1, alpha, spin, rot0;
    }

    RectTransform root;
    readonly List<Fx> live = new List<Fx>();

    static ClickFX I {
        get {
            if (instance != null) return instance;
            Canvas c = UIKit.CreateCanvas(null, "[ClickFX]", 600);
            GraphicRaycaster gr = c.GetComponent<GraphicRaycaster>();
            if (gr != null) gr.enabled = false;   // purely visual
            instance = c.gameObject.AddComponent<ClickFX>();
            instance.root = (RectTransform)c.transform;
            return instance;
        }
    }

    // screenPoint: where it happened (pixels). size: radius of the effect in canvas units (about the button's size).
    public static void Play(Vector2 screenPoint, ClickFxStyle style, Color color, float size) {
        if (style == ClickFxStyle.None) return;
        ClickFX fx = I;
        if (fx.live.Count > MaxLive) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(fx.root, screenPoint, null, out Vector2 p)) return;
        Color bright = UIKit.Lighten(color, 0.45f);

        switch (style) {
            case ClickFxStyle.Primary:
                fx.Add(UIKit.Glow, p, Vector2.zero, size * 1.2f, size * 3.2f, 0.75f, 0.32f, color);
                fx.Add(UIKit.Ring, p, Vector2.zero, size * 0.4f, size * 1.9f, 0.95f, 0.5f, bright);
                fx.Add(UIKit.Ring, p, Vector2.zero, size * 0.3f, size * 2.8f, 0.6f, 0.68f, color);
                fx.Sparks(p, 12, 120f, 260f, 16f, 28f, 0.62f, bright);
                ScreenFlashOverlay.Show(new Color(1f, 0.86f, 0.55f, 0.10f), 0.18f);
                break;
            case ClickFxStyle.Back:
                fx.Add(UIKit.Glow, p, Vector2.zero, size * 1.6f, size * 0.6f, 0.35f, 0.3f, color);
                fx.Add(UIKit.Ring, p, Vector2.zero, size * 1.5f, size * 0.35f, 0.7f, 0.36f, color);
                break;
            case ClickFxStyle.Weapon:
                fx.Add(UIKit.Glow, p, Vector2.zero, size * 1.0f, size * 2.6f, 0.8f, 0.38f, color);
                fx.Add(UIKit.Ring, p, Vector2.zero, size * 0.6f, size * 1.6f, 0.95f, 0.42f, bright);
                fx.Sparks(p, 8, 70f, 150f, 14f, 22f, 0.5f, bright);
                break;
            default:
                fx.Add(UIKit.Glow, p, Vector2.zero, size * 0.9f, size * 1.7f, 0.5f, 0.2f, bright);
                fx.Add(UIKit.Ring, p, Vector2.zero, size * 0.35f, size * 1.15f, 0.85f, 0.38f, color);
                fx.Sparks(p, 6, 60f, 130f, 12f, 20f, 0.45f, bright);
                break;
        }
    }

    void Add(Sprite sprite, Vector2 pos, Vector2 travel, float size0, float size1, float alpha, float life, Color color, float spin = 0f) {
        Image img = UIKit.Img(root, sprite, color, "Fx");
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size0, size0);
        live.Add(new Fx {
            rt = rt, img = img, from = pos, travel = travel, start = Time.unscaledTime, life = life,
            size0 = size0, size1 = size1, alpha = alpha, spin = spin, rot0 = Random.Range(0f, 360f),
        });
    }

    void Sparks(Vector2 p, int count, float minDist, float maxDist, float minSize, float maxSize, float life, Color color) {
        for (int i = 0; i < count; i++) {
            float a = (i + Random.value * 0.7f) / count * Mathf.PI * 2f;
            float dist = Random.Range(minDist, maxDist);
            float s = Random.Range(minSize, maxSize);
            Add(UIKit.Icon(UIIcon.Star), p, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dist, s, s * 0.2f, 1f,
                life * Random.Range(0.8f, 1.15f), color, Random.Range(-240f, 240f));
        }
    }

    void Update() {
        float now = Time.unscaledTime;
        for (int i = live.Count - 1; i >= 0; i--) {
            Fx f = live[i];
            float t = (now - f.start) / f.life;
            if (t >= 1f || f.rt == null) {
                if (f.rt != null) Destroy(f.rt.gameObject);
                live.RemoveAt(i);
                continue;
            }
            float e = 1f - (1f - t) * (1f - t) * (1f - t);   // fast at first, then settling
            float size = Mathf.Lerp(f.size0, f.size1, e);
            f.rt.sizeDelta = new Vector2(size, size);
            f.rt.anchoredPosition = f.from + f.travel * e;
            f.rt.localEulerAngles = new Vector3(0f, 0f, f.rot0 + f.spin * t);
            Color c = f.img.color;
            c.a = f.alpha * Mathf.Pow(1f - t, 1.5f);
            f.img.color = c;
        }
    }
}

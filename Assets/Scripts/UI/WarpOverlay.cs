using UnityEngine;
using UnityEngine.UI;

// Screen-space hyperspace streaks (radial lines that stretch outwards) and a colour flood, drawn on their own
// canvas above the menu. Three users:
//   - SpaceBackdrop.SetBase: a gentle streak level that follows how fast the camera is flying
//   - Pulse: a short burst when a menu page changes
//   - SetFlood + Block: the dive into a planet (the screen fills with the atmosphere colour, clicks are blocked)
// Created lazily, and only when something actually needs it.
public class WarpOverlay : MonoBehaviour {
    const int Lines = 80;
    static WarpOverlay instance;

    RectTransform[] rects;
    Image[] images;
    float[] angRad, angDeg, speed, phase, bright;
    GameObject lineRoot;
    Image flood;
    GraphicRaycaster raycaster;
    float baseLevel, shown;
    float pulseT = 1f, pulseDur = 0.45f, pulseStrength;
    Color floodColor;
    float floodAlpha;

    static WarpOverlay I {
        get {
            if (instance == null) instance = Create();
            return instance;
        }
    }

    public static void SetBase(float level) {
        if (instance == null && level < 0.02f) return;
        I.baseLevel = Mathf.Clamp01(level);
    }

    public static void Pulse(float strength, float seconds) {
        WarpOverlay w = I;
        w.pulseStrength = Mathf.Clamp01(strength);
        w.pulseDur = Mathf.Max(0.1f, seconds);
        w.pulseT = 0f;
    }

    public static void SetFlood(Color color, float alpha) {
        if (instance == null && alpha <= 0f) return;
        I.floodColor = color;
        I.floodAlpha = Mathf.Clamp01(alpha);
    }

    // While blocking, the whole screen swallows clicks (no double click while diving into a planet).
    public static void Block(bool on) {
        WarpOverlay w = I;
        w.raycaster.enabled = on;
        w.flood.raycastTarget = on;
    }

    static WarpOverlay Create() {
        Canvas c = UIKit.CreateCanvas(null, "[Warp]", 800);
        WarpOverlay w = c.gameObject.AddComponent<WarpOverlay>();
        w.raycaster = c.GetComponent<GraphicRaycaster>();
        if (w.raycaster == null) w.raycaster = c.gameObject.AddComponent<GraphicRaycaster>();
        w.raycaster.enabled = false;
        RectTransform root = (RectTransform)c.transform;

        w.lineRoot = UIKit.Rect(root, "Lines").Fill().gameObject;
        w.rects = new RectTransform[Lines];
        w.images = new Image[Lines];
        w.angRad = new float[Lines];
        w.angDeg = new float[Lines];
        w.speed = new float[Lines];
        w.phase = new float[Lines];
        w.bright = new float[Lines];
        System.Random rng = new System.Random(5);
        for (int i = 0; i < Lines; i++) {
            Image img = UIKit.Img(w.lineRoot.transform, UIKit.Glow, Color.clear, "Line");
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);   // the streak starts at its position and points outwards
            w.rects[i] = rt;
            w.images[i] = img;
            float a = (i + (float)rng.NextDouble() * 0.8f) / Lines * Mathf.PI * 2f;
            w.angRad[i] = a;
            w.angDeg[i] = a * Mathf.Rad2Deg;
            w.speed[i] = 0.7f + (float)rng.NextDouble() * 0.9f;
            w.phase[i] = (float)rng.NextDouble();
            w.bright[i] = 0.5f + (float)rng.NextDouble() * 0.5f;
        }
        w.lineRoot.SetActive(false);

        w.flood = UIKit.Img(root, null, Color.clear, "Flood");
        w.flood.rectTransform.Fill();
        w.flood.raycastTarget = false;
        return w;
    }

    void Update() {
        float dt = Time.unscaledDeltaTime;
        float pulse = 0f;
        if (pulseT < 1f) {
            pulseT += dt / pulseDur;
            pulse = pulseStrength * Mathf.Sin(Mathf.Clamp01(pulseT) * Mathf.PI);
        }
        float target = Mathf.Max(baseLevel, pulse);
        shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-14f * dt));
        flood.color = new Color(floodColor.r, floodColor.g, floodColor.b, floodAlpha);

        bool active = shown > 0.02f;
        if (lineRoot.activeSelf != active) lineRoot.SetActive(active);
        if (!active) return;

        float time = Time.unscaledTime;
        for (int i = 0; i < Lines; i++) {
            float r = Mathf.Repeat(phase[i] + time * speed[i] * (0.5f + shown * 2.2f), 1f);
            float rad = Mathf.Lerp(90f, 1250f, r * r);                       // streaks speed up as they leave the centre
            float len = Mathf.Lerp(10f, 520f, shown) * (0.2f + r);
            float thick = 2f + 4f * r;
            float a = shown * Mathf.Clamp01(r * 1.6f) * bright[i];
            rects[i].anchoredPosition = new Vector2(Mathf.Cos(angRad[i]) * rad, Mathf.Sin(angRad[i]) * rad);
            rects[i].localEulerAngles = new Vector3(0f, 0f, angDeg[i]);
            rects[i].sizeDelta = new Vector2(len, thick);
            images[i].color = i % 3 == 0 ? new Color(1f, 0.92f, 0.78f, a) : i % 3 == 1 ? new Color(0.78f, 0.88f, 1f, a) : new Color(1f, 1f, 1f, a);
        }
    }
}

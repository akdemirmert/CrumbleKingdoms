using UnityEngine;
using UnityEngine.UI;

// Full-screen colour flash (lightning, holy light) on a lazily created overlay canvas, one per scene.
public class ScreenFlashOverlay : MonoBehaviour {
    const float MaxAlpha = 0.8f;   // never completely blind the player
    static ScreenFlashOverlay instance;

    Image image;
    Color color;
    float duration, t;

    public static void Show(Color color, float duration) {
        if (instance == null) instance = Create();
        color.a = Mathf.Clamp(color.a, 0f, MaxAlpha);
        if (instance.image.enabled && instance.CurrentAlpha > color.a) return;   // a weaker flash never cuts a stronger one short
        instance.color = color;
        instance.duration = Mathf.Max(0.02f, duration);
        instance.t = 0f;
        instance.image.enabled = true;
        instance.Apply();
    }

    static ScreenFlashOverlay Create() {
        GameObject go = new GameObject("[ScreenFlash]");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        GameObject img = new GameObject("Flash", typeof(RectTransform));
        img.transform.SetParent(go.transform, false);
        RectTransform rt = (RectTransform)img.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        ScreenFlashOverlay flash = go.AddComponent<ScreenFlashOverlay>();
        flash.image = img.AddComponent<Image>();
        flash.image.raycastTarget = false;                              // never blocks clicks
        flash.image.enabled = false;
        return flash;
    }

    float CurrentAlpha => image.color.a;

    void Update() {
        if (image == null || !image.enabled) return;
        t += Time.unscaledDeltaTime;
        Apply();
        if (t >= duration) image.enabled = false;
    }

    void Apply() {
        float k = Mathf.Clamp01(t / duration);
        Color c = color;
        c.a = color.a * (1f - k) * (1f - k);
        image.color = c;
    }
}

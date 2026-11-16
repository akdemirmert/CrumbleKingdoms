using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Fades to a colour, loads a scene, fades back in. Survives scene loads.
// The colour and the durations can be chosen per load: the planet dive floods the screen with the planet's
// atmosphere colour, and the battle then fades in from that same colour.
public class SceneFader : MonoBehaviour {
    public static readonly Color DefaultColor = new Color(0.02f, 0.02f, 0.04f, 1f);

    static SceneFader instance;
    Image overlay;
    bool busy;
    string pending;
    Color tint = DefaultColor;
    float fadeOut = 0.25f, fadeIn = 0.35f;

    static SceneFader I {
        get {
            if (instance != null) return instance;
            GameObject go = new GameObject("[SceneFader]");
            DontDestroyOnLoad(go);
            Canvas c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 1000;
            go.AddComponent<GraphicRaycaster>();   // so the overlay swallows clicks while it fades (no double 'Next level')
            GameObject img = new GameObject("Overlay", typeof(RectTransform));
            img.transform.SetParent(go.transform, false);
            RectTransform rt = img.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            instance = go.AddComponent<SceneFader>();
            instance.overlay = img.AddComponent<Image>();
            instance.overlay.color = new Color(DefaultColor.r, DefaultColor.g, DefaultColor.b, 0f);
            instance.overlay.raycastTarget = false;
            return instance;
        }
    }

    public static void Load(string scene) { Load(scene, null, 0.25f, 0.35f); }

    // tint = colour of the fade (null = near black). A load requested while another runs goes next and keeps its own look.
    public static void Load(string scene, Color? tint, float fadeOut = 0.25f, float fadeIn = 0.35f) {
        if (I.busy) { I.pending = scene; return; }
        I.tint = tint ?? DefaultColor;
        I.fadeOut = Mathf.Max(0.01f, fadeOut);
        I.fadeIn = Mathf.Max(0.01f, fadeIn);
        I.StartCoroutine(I.Run(scene));
    }

    IEnumerator Run(string scene) {
        busy = true;
        overlay.raycastTarget = true;
        overlay.color = new Color(tint.r, tint.g, tint.b, overlay.color.a);
        yield return Fade(0f, 1f, fadeOut);
        while (scene != null) {
            pending = null;
            TimeFX.Reset();
            AsyncOperation op = SceneManager.LoadSceneAsync(scene);
            while (op != null && !op.isDone) yield return null;
            yield return null;
            scene = pending;   // a load requested while this one ran goes next instead of being lost
        }
        yield return Fade(1f, 0f, fadeIn);
        overlay.raycastTarget = false;
        busy = false;
    }

    IEnumerator Fade(float from, float to, float seconds) {
        float t = 0f;
        Color c = overlay.color;
        while (t < seconds) {
            t += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(from, to, t / seconds);
            overlay.color = c;
            yield return null;
        }
        c.a = to;
        overlay.color = c;
    }
}

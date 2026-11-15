using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Fades to black, loads a scene, fades back in. Survives scene loads.
public class SceneFader : MonoBehaviour {
    static SceneFader instance;
    Image overlay;
    bool busy;
    string pending;

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
            instance.overlay.color = new Color(0.02f, 0.02f, 0.04f, 0f);
            instance.overlay.raycastTarget = false;
            return instance;
        }
    }

    public static void Load(string scene) {
        if (I.busy) { I.pending = scene; return; }
        I.StartCoroutine(I.Run(scene));
    }

    IEnumerator Run(string scene) {
        busy = true;
        overlay.raycastTarget = true;
        yield return Fade(0f, 1f, 0.25f);
        while (scene != null) {
            pending = null;
            TimeFX.Reset();
            AsyncOperation op = SceneManager.LoadSceneAsync(scene);
            while (op != null && !op.isDone) yield return null;
            yield return null;
            scene = pending;   // a load requested while this one ran goes next instead of being lost
        }
        yield return Fade(1f, 0f, 0.35f);
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

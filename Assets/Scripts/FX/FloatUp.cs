using TMPro;
using UnityEngine;

// Animates world-space floating text (damage numbers): pops in, rises and slows, fades out. It always
// faces the camera and scales with the camera distance so it stays readable at any zoom.
public class FloatUp : MonoBehaviour {
    public float scale = 1f;
    public float life = 1.15f;

    TMP_Text text;
    Vector3 start;
    float t, drift, baseAlpha = 1f;
    bool ready;

    public void Init(float scale) {
        this.scale = scale;
        Setup();
    }

    void Start() { Setup(); }

    void Setup() {
        if (ready) return;
        ready = true;
        text = GetComponent<TMP_Text>();
        if (text != null) baseAlpha = text.color.a;
        start = transform.position;
        drift = Random.Range(-0.4f, 0.4f);
        Apply();
    }

    void LateUpdate() {
        t += Time.unscaledDeltaTime;
        if (t >= life) { Destroy(gameObject); return; }
        Apply();
    }

    void Apply() {
        float zoom = 1f;
        Camera cam = Camera.main;
        if (cam != null) {
            transform.rotation = cam.transform.rotation;
            zoom = Mathf.Clamp(Vector3.Distance(cam.transform.position, start) / 40f, 0.45f, 2.2f);
        }
        float k = life > 0f ? Mathf.Clamp01(t / life) : 1f;
        float pop = k < 0.07f ? Mathf.Lerp(0.3f, 1.3f, k / 0.07f)
                  : k < 0.2f ? Mathf.Lerp(1.3f, 1f, (k - 0.07f) / 0.13f) : 1f;
        transform.localScale = Vector3.one * (scale * zoom * pop);
        float rise = 1f - (1f - k) * (1f - k);                         // fast at first, then slowing
        transform.position = start + new Vector3(drift * k, rise * 1.8f, 0f) * zoom;
        if (text != null) text.alpha = baseAlpha * (k < 0.65f ? 1f : 1f - (k - 0.65f) / 0.35f);
    }
}

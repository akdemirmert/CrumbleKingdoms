using System.Collections.Generic;
using UnityEngine;

// A point light that fades out fast (ease-out), then destroys its object. A global cap keeps the number
// of flash lights bounded: the oldest one goes when a new one would exceed it.
public class FadeLight : MonoBehaviour {
    const int MaxLive = 8;
    static readonly List<FadeLight> live = new List<FadeLight>();

    public float duration = 0.3f;

    Light lightSource;
    float startIntensity, t;

    void Awake() {
        live.Add(this);
        if (live.Count > MaxLive) {
            FadeLight oldest = live[0];
            live.RemoveAt(0);
            if (oldest != null) Destroy(oldest.gameObject);
        }
    }

    void Start() {
        lightSource = GetComponent<Light>();
        if (lightSource != null) startIntensity = lightSource.intensity;
    }

    void Update() {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / Mathf.Max(0.01f, duration));
        if (lightSource != null) lightSource.intensity = startIntensity * (1f - k) * (1f - k);
        if (k >= 1f) Destroy(gameObject);
    }

    void OnDestroy() { live.Remove(this); }
}

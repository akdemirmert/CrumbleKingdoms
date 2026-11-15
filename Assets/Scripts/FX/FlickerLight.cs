using UnityEngine;

// The warm, flickering light of a fire. Only a few exist at once (point lights are not free).
public class FlickerLight : MonoBehaviour {
    public const int Max = 4;
    public static int Active { get; private set; }

    Light lightSource;
    float baseIntensity, seed, fade = 1f, fadeSpeed;

    // Adds a light on a child of `host` (null when the limit is reached).
    public static FlickerLight Add(Transform host, Vector3 localPos, Color color, float intensity, float range) {
        if (Active >= Max) return null;
        GameObject go = new GameObject("Fire light");
        go.transform.SetParent(host, false);
        go.transform.localPosition = localPos;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = 0f;
        l.shadows = LightShadows.None;
        FlickerLight f = go.AddComponent<FlickerLight>();
        f.lightSource = l;
        f.baseIntensity = intensity;
        f.seed = Random.Range(0f, 100f);
        return f;
    }

    public void FadeOut(float seconds) { fadeSpeed = 1f / Mathf.Max(0.01f, seconds); }

    void OnEnable() { Active++; }
    void OnDisable() { Active--; }

    void Update() {
        if (lightSource == null) return;
        if (fadeSpeed > 0f) fade = Mathf.Max(0f, fade - fadeSpeed * Time.deltaTime);
        float flicker = 0.65f + 0.55f * Mathf.PerlinNoise(seed, Time.time * 7f);
        lightSource.intensity = baseIntensity * flicker * fade;
    }
}

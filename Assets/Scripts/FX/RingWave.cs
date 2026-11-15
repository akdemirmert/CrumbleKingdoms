using UnityEngine;
using UnityEngine.Rendering;

// An expanding, fading circle drawn with a LineRenderer in the XY plane (it faces the side-view camera).
public class RingWave : MonoBehaviour {
    const int Segments = 48;
    readonly Vector3[] points = new Vector3[Segments];
    LineRenderer line;
    Color color;
    float radius, duration, width, t;

    public void Init(Vector3 center, float radius, Color color, float duration, float width) {
        transform.position = center;
        this.color = FxAssets.SplitHdr(color, out float hdr);
        this.radius = radius;
        this.duration = Mathf.Max(0.05f, duration);
        this.width = Mathf.Max(0.02f, width);
        line = gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = FxAssets.Additive(FxAssets.Beam, 1.8f * hdr);
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = Segments;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = FxLayers.OrderGlow;
        Apply();
    }

    void Update() {
        if (line == null) { Destroy(gameObject); return; }
        t += Time.deltaTime;
        if (t >= duration) { Destroy(gameObject); return; }
        Apply();
    }

    void Apply() {
        float k = t / duration;
        float grow = 1f - (1f - k) * (1f - k) * (1f - k);              // fast at first, then slowing
        float r = radius * Mathf.Lerp(0.12f, 1f, grow);
        for (int i = 0; i < Segments; i++) {
            float a = i * Mathf.PI * 2f / Segments;
            points[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
        }
        line.SetPositions(points);
        line.widthMultiplier = width * (1f - 0.6f * k);
        Color c = color;
        c.a = color.a * (1f - k) * (1f - k * 0.5f);
        line.startColor = c;
        line.endColor = c;
    }
}

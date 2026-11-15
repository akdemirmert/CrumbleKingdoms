using System.Collections.Generic;
using UnityEngine;

// Trajectory dots (first part of the arc, gravity only) and the pulled "sling band" behind the commander.
public class AimVisuals : MonoBehaviour {
    const int DotCount = 26;
    readonly List<Transform> dots = new List<Transform>();
    readonly List<Vector3> pts = new List<Vector3>();
    LineRenderer band;
    GameObject root;
    Material dotMat;

    public float previewSeconds = 1.1f;

    void Build() {
        if (root != null) return;
        root = new GameObject("AimVisuals");
        dotMat = Mats.Emissive(new Color(1f, 1f, 1f), new Color(1.6f, 1.5f, 1.2f));
        Mesh sphere = ModelKit.Primitive(PrimitiveType.Sphere);
        for (int i = 0; i < DotCount; i++) {
            GameObject d = ModelKit.MeshPart(root.transform, sphere, Vector3.zero, Vector3.one * 0.18f, dotMat, default, "Dot");
            ModelKit.Shadows(d, false, false);
            dots.Add(d.transform);
        }
        GameObject b = new GameObject("Band");
        b.transform.SetParent(root.transform, false);
        band = b.AddComponent<LineRenderer>();
        band.sharedMaterial = Mats.ParticleAdditive(TexKit.SoftCircle);
        band.positionCount = 2;
        band.widthMultiplier = 0.16f;
        band.numCapVertices = 4;
        band.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        root.SetActive(false);
    }

    public void Show(Vector3 origin, Vector3 velocity, float gravityScale, Color color, float power01) {
        Build();
        root.SetActive(true);
        Ballistics.Trace(origin, velocity, gravityScale, 0f, previewSeconds, pts, 3);
        for (int i = 0; i < dots.Count; i++) {
            bool on = i + 1 < pts.Count;
            dots[i].gameObject.SetActive(on);
            if (!on) continue;
            dots[i].position = pts[i + 1];
            float k = 1f - i / (float)DotCount;
            dots[i].localScale = Vector3.one * Mathf.Lerp(0.07f, 0.2f, k);
        }
        Vector3 back = origin - velocity.normalized * Mathf.Lerp(0.4f, 2.2f, power01);
        band.SetPosition(0, origin);
        band.SetPosition(1, back);
        Color c = Color.Lerp(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.35f, 0.2f), power01) * 1.6f;
        c.a = 1f;
        band.startColor = color * 1.5f;
        band.endColor = c;
    }

    public void Hide() {
        if (root != null) root.SetActive(false);
    }

    void OnDestroy() {
        if (root != null) Destroy(root);
    }
}

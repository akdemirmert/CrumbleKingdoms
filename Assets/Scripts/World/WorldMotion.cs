using System.Collections.Generic;
using UnityEngine;

// Small animators for the arena scenery. None of them touch gameplay objects.

// Keeps "infinitely far" things (sky gradient, sun, moon, stars, aurora, horizon haze) centred on the camera:
// it copies the camera position and its heading, never its pitch or shake roll, so the horizon stays level.
// Runs after the camera scripts have moved the camera this frame.
[DefaultExecutionOrder(1000)]
public class SkyAnchor : MonoBehaviour {
    Camera cam;

    void LateUpdate() {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        Transform c = cam.transform;
        Vector3 flat = c.forward;
        flat.y = 0f;
        Quaternion heading = flat.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(flat) : Quaternion.identity;
        transform.SetPositionAndRotation(c.position, heading);
    }
}

// Rolls gentle waves through the near sea mesh (sum of three sine swells, fading to zero at the mesh edge
// so it meets the flat far sea without a crack), pulses the lava glow and drifts the foam texture.
public class SeaWaves : MonoBehaviour {
    Mesh mesh;
    Vector3[] rest, verts;
    float[] weight;
    float amplitude, speed;
    Material glow;
    Color glowColor;
    readonly List<Renderer> foam = new List<Renderer>();
    MaterialPropertyBlock mpb;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

    public void Init(Mesh m, float[] edgeWeight, float amp, float spd, Material lavaGlow) {
        mesh = m;
        mesh.MarkDynamic();
        rest = m.vertices;
        verts = (Vector3[])rest.Clone();
        weight = edgeWeight;
        amplitude = amp;
        speed = spd;
        glow = lavaGlow;
        if (glow != null) glowColor = glow.GetColor("_EmissionColor");
    }

    public void AddFoam(Renderer r) { foam.Add(r); }

    // Height of the swell at (x, z): also used to bob things that float.
    public static float Swell(float x, float z, float t) {
        return 0.55f * Mathf.Sin(0.31f * x + 1.2f * t) + 0.3f * Mathf.Sin(0.17f * x + 0.29f * z - 0.8f * t)
               + 0.15f * Mathf.Sin(0.7f * x - 0.45f * z + 2f * t);
    }

    void Update() {
        if (mesh == null) return;
        float t = Time.time * speed;
        for (int i = 0; i < verts.Length; i++) {
            Vector3 p = rest[i];
            if (weight[i] > 0f) p.y += amplitude * weight[i] * Swell(p.x, p.z, t);
            verts[i] = p;
        }
        mesh.vertices = verts;
        mesh.RecalculateNormals();

        if (glow != null) glow.SetColor("_EmissionColor", glowColor * (0.85f + 0.15f * Mathf.Sin(Time.time * 0.9f)));
        if (foam.Count == 0) return;
        mpb ??= new MaterialPropertyBlock();
        for (int i = 0; i < foam.Count; i++) {
            Renderer r = foam[i];
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            Color c = r.sharedMaterial.GetColor(BaseColorId);
            c.a *= 0.75f + 0.25f * Mathf.Sin(Time.time * 1.3f + i * 1.7f);
            mpb.SetColor(BaseColorId, c);
            mpb.SetVector(BaseMapStId, new Vector4(1f / SeaBuilder.FoamTile, 1f, Time.time * 0.03f + i * 0.37f, 0f));
            r.SetPropertyBlock(mpb);
        }
    }
}

// Moves its children sideways (clouds) and wraps them around; they drift with the battle's wind.
public class CloudDrift : MonoBehaviour {
    public float halfWidth = 700f;

    void Update() {
        float speed = 1.2f + BattleManager.Wind * 2.5f;
        float dx = speed * Time.deltaTime;
        foreach (Transform c in transform) {
            Vector3 p = c.localPosition;
            p.x += dx;
            if (p.x > halfWidth) p.x -= 2f * halfWidth;
            else if (p.x < -halfWidth) p.x += 2f * halfWidth;
            c.localPosition = p;
        }
    }
}

// Slow floating motion (floating rocks, icebergs).
public class Bobber : MonoBehaviour {
    public float height = 0.8f, period = 7f, tilt = 2f;
    Vector3 basePos;
    Quaternion baseRot;
    float phase;

    void Start() {
        basePos = transform.localPosition;
        baseRot = transform.localRotation;
        phase = Random.value * 10f;
    }

    void Update() {
        float s = Mathf.Sin((Time.time + phase) * 2f * Mathf.PI / period);
        transform.localPosition = basePos + Vector3.up * (height * s);
        transform.localRotation = baseRot * Quaternion.Euler(tilt * s, 0f, tilt * 0.6f * Mathf.Cos((Time.time + phase) * 1.3f));
    }
}

// An aurora curtain: a ribbon of vertices that sways and shimmers (vertex colour alpha).
public class AuroraRibbon : MonoBehaviour {
    Mesh mesh;
    Vector3[] rest, verts;
    Color[] baseColors, colors;
    float phase;

    public void Init(Mesh m) {
        mesh = m;
        mesh.MarkDynamic();
        rest = m.vertices;
        verts = (Vector3[])rest.Clone();
        baseColors = m.colors;
        colors = (Color[])baseColors.Clone();
        phase = Random.value * 10f;
    }

    void Update() {
        if (mesh == null) return;
        float t = Time.time * 0.25f + phase;
        for (int i = 0; i < rest.Length; i++) {
            Vector3 p = rest[i];
            float wave = Mathf.Sin(p.x * 0.011f + t) + 0.5f * Mathf.Sin(p.x * 0.027f - t * 1.7f);
            verts[i] = new Vector3(p.x, p.y + 6f * wave, p.z + 25f * Mathf.Sin(p.x * 0.006f + t * 0.6f));
            Color c = baseColors[i];
            c.a *= 0.65f + 0.35f * Mathf.Sin(p.x * 0.035f + t * 2.3f);
            colors[i] = c;
        }
        mesh.vertices = verts;
        mesh.colors = colors;
    }
}

// Twinkling bright stars: modulates the vertex colours of a star mesh (four vertices per star).
public class StarTwinkle : MonoBehaviour {
    Mesh mesh;
    Color[] baseColors, colors;

    public void Init(Mesh m) {
        mesh = m;
        mesh.MarkDynamic();
        baseColors = m.colors;
        colors = (Color[])baseColors.Clone();
    }

    void Update() {
        if (mesh == null) return;
        for (int star = 0; star * 4 < colors.Length; star++) {
            float k = 0.55f + 0.45f * Mathf.Sin(Time.time * (1.3f + star % 5 * 0.4f) + star * 2.1f);
            for (int v = 0; v < 4; v++) {
                Color c = baseColors[star * 4 + v];
                c.a *= k;
                colors[star * 4 + v] = c;
            }
        }
        mesh.colors = colors;
    }
}

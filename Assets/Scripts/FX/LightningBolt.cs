using UnityEngine;
using UnityEngine.Rendering;

// Jagged, flickering lightning between two points: a white-hot core, a coloured glow and a few short
// branches. The channel keeps its shape but shivers every few frames; the bolt flickers, then fades out.
public class LightningBolt : MonoBehaviour {
    const int Segments = 32;          // main channel (power of two for the midpoint displacement)
    const int BranchSegments = 8;

    LineRenderer core, glow;
    LineRenderer[] branches = new LineRenderer[0];
    Vector3[] channel, shaken;
    Vector3[][] branchPoints;
    Vector3 from, to, side;
    Color color;
    float width, duration, length, t, nextShake;

    public void Init(Vector3 from, Vector3 to, Color color, float width, float duration) {
        if (core != null) return;
        if (!Finite(from) || !Finite(to)) from = to = transform.position;   // nothing sensible to draw
        from.z = Mathf.Min(from.z, VFX.FrontZ);                         // in front of the fortress blocks
        to.z = Mathf.Min(to.z, VFX.FrontZ);
        this.from = from;
        this.to = to;
        this.color = FxAssets.SplitHdr(color, out _);
        this.width = Mathf.Max(0.02f, width);
        this.duration = Mathf.Max(0.05f, duration);
        Vector3 d = to - from;
        length = d.magnitude;
        side = length > 1e-4f ? Vector3.Cross(d / length, Vector3.forward) : Vector3.right;

        channel = new Vector3[Segments + 1];
        shaken = new Vector3[Segments + 1];
        Jag(channel, from, to, 0.08f);                                  // near the straight line: forks added by callers still touch it
        glow = Line("Glow", FxAssets.Additive(FxAssets.Beam, 1.8f), Segments + 1);
        core = Line("Core", FxAssets.Additive(FxAssets.Beam, 4f), Segments + 1);
        core.widthCurve = FxLooks.Curve(0f, 0.5f, 0.08f, 1f, 0.9f, 1f, 1f, 0.6f);

        int count = length > 2f ? Random.Range(2, 5) : 0;
        branches = new LineRenderer[count];
        branchPoints = new Vector3[count][];
        for (int i = 0; i < count; i++) {
            branches[i] = Line("Branch", FxAssets.Additive(FxAssets.Beam, 2.5f), BranchSegments + 1);
            branches[i].widthCurve = FxLooks.Curve(0f, 1f, 1f, 0.2f);
            branchPoints[i] = new Vector3[BranchSegments + 1];
        }
        Shake();
        Apply(1f);
    }

    void Update() {
        if (core == null) { Destroy(gameObject); return; }
        t += Time.deltaTime;
        if (t >= duration) { Destroy(gameObject); return; }
        if (t >= nextShake) {
            Shake();
            nextShake = t + Random.Range(0.035f, 0.075f);
        }
        float x = Mathf.Clamp01((t / duration - 0.3f) / 0.7f);
        float fade = 1f - x * x * (3f - 2f * x);                        // full for 30 %, then a smooth fade
        float flicker = Random.value < 0.2f ? 0.3f : 1f;
        Apply(fade * flicker);
    }

    // Midpoint displacement: every level splits each segment and pushes its middle sideways.
    static void Jag(Vector3[] pts, Vector3 a, Vector3 b, float roughness) {
        int n = pts.Length - 1;
        pts[0] = a;
        pts[n] = b;
        Vector3 d = b - a;
        float len = d.magnitude;
        Vector3 perp = len > 1e-4f ? Vector3.Cross(d / len, Vector3.forward) : Vector3.right;
        float amp = len * roughness;
        for (int step = n; step > 1; step /= 2, amp *= 0.6f)
            for (int i = 0; i + step <= n; i += step)
                pts[i + step / 2] = (pts[i] + pts[i + step]) * 0.5f + perp * Random.Range(-amp, amp);
    }

    // The channel shivers a little around its shape; branches grow anew.
    void Shake() {
        float amp = Mathf.Min(length * 0.012f, 0.25f);
        for (int i = 0; i <= Segments; i++) {
            bool end = i == 0 || i == Segments;
            shaken[i] = end ? channel[i] : channel[i] + side * Random.Range(-amp, amp);
        }
        core.SetPositions(shaken);
        glow.SetPositions(shaken);
        Vector3 dir = length > 1e-4f ? (to - from) / length : Vector3.down;
        for (int b = 0; b < branches.Length; b++) {
            Vector3 start = shaken[Random.Range(Segments / 5, Segments * 3 / 4)];
            float angle = Random.Range(25f, 50f) * (Random.value < 0.5f ? -1f : 1f);
            Vector3 end = start + Quaternion.Euler(0f, 0f, angle) * dir * length * Random.Range(0.12f, 0.28f);
            Jag(branchPoints[b], start, end, 0.2f);
            branches[b].SetPositions(branchPoints[b]);
        }
    }

    void Apply(float a) {
        Color hot = Color.Lerp(color, Color.white, 0.75f);
        hot.a = a;
        core.startColor = hot;
        core.endColor = hot;
        core.widthMultiplier = width * 0.4f;
        Color halo = color;
        halo.a = 0.55f * a;
        glow.startColor = halo;
        glow.endColor = halo;
        glow.widthMultiplier = width * 2.8f;
        Color twig = Color.Lerp(color, Color.white, 0.5f);
        twig.a = 0.85f * a;
        Color tip = twig;
        tip.a = 0f;
        foreach (LineRenderer b in branches) {
            b.startColor = twig;
            b.endColor = tip;
            b.widthMultiplier = width * 0.35f;
        }
    }

    static bool Finite(Vector3 v) {
        return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }

    LineRenderer Line(string name, Material mat, int points) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        LineRenderer l = go.AddComponent<LineRenderer>();
        l.sharedMaterial = mat;
        l.positionCount = points;
        l.useWorldSpace = true;
        l.alignment = LineAlignment.View;
        l.textureMode = LineTextureMode.Stretch;
        l.numCapVertices = 2;
        l.shadowCastingMode = ShadowCastingMode.Off;
        l.receiveShadows = false;
        l.sortingOrder = FxLayers.OrderFlash;
        return l;
    }
}

using System.Collections.Generic;
using UnityEngine;

// Pieces of a broken block. Free 3D physics for a few seconds, then they shrink away.
// A global cap keeps the number of live pieces bounded.
public class Debris : MonoBehaviour {
    static readonly LinkedList<Debris> live = new LinkedList<Debris>();
    const int MaxLive = 240;

    float dieAt, shrinkStart = -1f;
    Vector3 baseScale;
    LinkedListNode<Debris> node;
    bool splashed;

    public static void SpawnFrom(Block b, DamageInfo d) {
        Vector3 s = b.size;
        bool brittle = b.def.brittle;
        float cell = brittle ? 0.3f : 0.45f;
        int nx = Mathf.Clamp(Mathf.RoundToInt(s.x / cell), 1, 4);
        int ny = Mathf.Clamp(Mathf.RoundToInt(s.y / cell), 1, 4);
        int nz = s.z > 1.2f ? 2 : 1;
        while (nx * ny * nz > 14) { if (nx >= ny) nx--; else ny--; }
        Vector3 piece = new Vector3(s.x / nx, s.y / ny, s.z / nz);

        Material mat = BlockVisuals.Get(b.material, 2, b.skin);
        Rigidbody src = b.rb;
        Vector3 baseVel = src != null ? src.linearVelocity : Vector3.zero;
        Vector3 hitDir = d.impulse.sqrMagnitude > 0.01f ? d.impulse.normalized : Vector3.zero;
        float hitStrength = Mathf.Clamp(d.impulse.magnitude / Mathf.Max(0.25f, src != null ? src.mass : 1f), 0f, 12f);
        Vector3 center = b.transform.position;
        Quaternion rot = b.transform.rotation;
        Vector3 from = d.point == Vector3.zero ? center : d.point;

        for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
                for (int iz = 0; iz < nz; iz++) {
                    Vector3 local = new Vector3((ix + 0.5f) * piece.x - s.x / 2f, (iy + 0.5f) * piece.y - s.y / 2f, (iz + 0.5f) * piece.z - s.z / 2f);
                    Vector3 pos = center + rot * local;
                    Vector3 sz = Vector3.Scale(piece, new Vector3(Random.Range(0.72f, 0.97f), Random.Range(0.72f, 0.97f), Random.Range(0.8f, 0.97f)));
                    if (brittle) sz.z *= 0.35f;
                    sz = new Vector3(Step(sz.x), Step(sz.y), Step(sz.z));   // 5 cm steps: few distinct meshes in the box cache
                    Vector3 outward = pos - from;
                    outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : Random.onUnitSphere;
                    Vector3 v = baseVel + outward * Random.Range(1f, 3.5f) + hitDir * hitStrength * Random.Range(0.3f, 0.8f)
                                + Vector3.up * Random.Range(0.5f, 2f);
                    Quaternion r = rot * Quaternion.Euler(Random.Range(-12f, 12f), Random.Range(-12f, 12f), Random.Range(-12f, 12f));
                    Spawn(pos, r, sz, mat, b.def.density, v, Random.insideUnitSphere * 9f, brittle ? Random.Range(2f, 3f) : Random.Range(3.5f, 5.5f));
                }
    }

    static float Step(float v) { return Mathf.Max(0.05f, Mathf.Round(v * 20f) / 20f); }

    public static Debris Spawn(Vector3 pos, Quaternion rot, Vector3 size, Material mat, float density, Vector3 vel, Vector3 angVel, float life) {
        GameObject go = new GameObject("Debris") { layer = Layers.Debris };
        go.transform.SetPositionAndRotation(pos, rot);
        go.AddComponent<MeshFilter>().sharedMesh = MeshKit.Box(size);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.size = size;
        bc.sharedMaterial = PhysMats.Debris;
        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(0.05f, size.x * size.y * size.z * density);
        rb.linearVelocity = vel;
        rb.angularVelocity = angVel;
        rb.linearDamping = 0.1f;
        Debris d = go.AddComponent<Debris>();
        d.dieAt = Time.time + life;
        d.baseScale = Vector3.one;
        d.node = live.AddLast(d);
        while (live.Count > MaxLive) live.First.Value.BeginShrink();
        return d;
    }

    void Update() {
        Vector3 p = transform.position;
        if (!splashed && p.y < World.WaterY) {
            splashed = true;
            if (Random.value < 0.3f) VFX.Splash(new Vector3(p.x, World.WaterY, p.z), 0.4f);
        }
        if (p.y < World.KillY) { Destroy(gameObject); return; }
        if (shrinkStart < 0f && Time.time >= dieAt) BeginShrink();
        if (shrinkStart >= 0f) {
            float k = 1f - (Time.time - shrinkStart) / 0.5f;
            if (k <= 0f) { Destroy(gameObject); return; }
            transform.localScale = baseScale * k;
        }
    }

    void BeginShrink() {
        if (shrinkStart < 0f) shrinkStart = Time.time;
        Unlink();
    }

    void OnDestroy() { Unlink(); }

    void Unlink() {
        if (node == null) return;
        live.Remove(node);
        node = null;
    }
}

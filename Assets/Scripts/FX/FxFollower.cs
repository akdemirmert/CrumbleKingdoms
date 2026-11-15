using UnityEngine;

// Keeps a looping effect (trail, emitter, fire) on a moving target without staying its child. When the
// target is destroyed or hidden the effect stops emitting and fades out, instead of vanishing with it.
public class FxFollower : MonoBehaviour {
    Transform target;
    Vector3 offset;             // in the target's local space
    float linger;               // seconds the effect lives on after its target is gone
    float releasedAt = -1f;

    // If fx starts as a child of target, it detaches on its first frame, so local offsets a caller sets
    // right after creating it are kept.
    public static void Track(GameObject fx, Transform target, float linger) {
        FxFollower f = fx.AddComponent<FxFollower>();
        f.target = target;
        f.linger = Mathf.Max(0.05f, linger);
        if (target != null) f.offset = LocalOffset(target, fx.transform.position);
    }

    void LateUpdate() {
        if (releasedAt >= 0f) {
            if (Time.time - releasedAt >= linger) Destroy(gameObject);
            return;
        }
        if (target == null || !target.gameObject.activeInHierarchy) { Release(); return; }
        if (transform.parent == target) {
            offset = LocalOffset(target, transform.position);
            transform.SetParent(null, true);
        }
        transform.position = target.TransformPoint(offset);
    }

    void Release() {
        releasedAt = Time.time;
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        foreach (TrailRenderer tr in GetComponentsInChildren<TrailRenderer>()) tr.emitting = false;
        foreach (FlickerLight fl in GetComponentsInChildren<FlickerLight>()) fl.FadeOut(0.4f);
    }

    // The point in the target's local space (zero if the target is scaled to nothing).
    static Vector3 LocalOffset(Transform target, Vector3 world) {
        Vector3 local = target.InverseTransformPoint(world);
        bool finite = !(float.IsNaN(local.x) || float.IsNaN(local.y) || float.IsNaN(local.z) ||
                        float.IsInfinity(local.x) || float.IsInfinity(local.y) || float.IsInfinity(local.z));
        return finite ? local : Vector3.zero;
    }
}

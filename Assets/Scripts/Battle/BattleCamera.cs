using UnityEngine;
using UnityEngine.InputSystem;

// Side-view perspective camera: frames bounds, follows shots, shakes. Mouse wheel zooms,
// right/middle drag pans (until the next automatic move).
[RequireComponent(typeof(Camera))]
public class BattleCamera : MonoBehaviour {
    public static BattleCamera I { get; private set; }

    public float pitch = 4f;
    public float minDistance = 10f, maxDistance = 110f;

    Camera cam;
    BattleManager battle;
    Vector2 center, targetCenter, centerVel;
    float distance = 60f, targetDistance = 60f, distanceVel;
    float smooth = 0.45f;
    float trauma;
    bool following, userControl;
    Vector3 lastFollowPoint;
    float holdUntil, overviewAt = -1f;
    Vector2 panLast;
    bool panning;

    void Awake() {
        I = this;
        cam = GetComponent<Camera>();
        cam.fieldOfView = 40f;
        transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void OnDestroy() { if (I == this) I = null; }

    public void SetBattle(BattleManager bm) { battle = bm; }

    // Shake: amount 0..1 adds "trauma"; shake strength is trauma squared.
    public static void Shake(float amount) {
        if (I == null || !GameSettings.ScreenShake) return;
        I.trauma = Mathf.Clamp01(I.trauma + amount);
    }

    public void Frame(Bounds b, float pad = 4f, bool instant = false) {
        following = false;
        userControl = false;
        overviewAt = -1f;
        float halfH = b.extents.y + pad, halfW = b.extents.x + pad;
        float tan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float d = Mathf.Max(halfH / tan, halfW / (tan * cam.aspect));
        targetDistance = Mathf.Clamp(d, minDistance, maxDistance);
        targetCenter = new Vector2(b.center.x, Mathf.Max(b.center.y, World.WaterY + halfH * 0.55f));
        smooth = 0.45f;
        if (instant) { center = targetCenter; distance = targetDistance; centerVel = Vector2.zero; distanceVel = 0f; }
    }

    public void Overview() {
        if (battle == null) return;
        Bounds b = battle.ArenaBounds();
        b.Encapsulate(new Vector3(b.center.x, World.WaterY - 1f, 0f));
        Frame(b, 3f);
    }

    public void OverviewSoon(float delay) { overviewAt = Time.time + delay; }

    // Before a turn: the whole battlefield, slightly biased towards the shooter.
    public void FrameForAim(Fortress shooter) {
        if (battle == null) return;
        Bounds b = battle.ArenaBounds();
        b.Encapsulate(new Vector3(b.center.x, World.WaterY - 1f, 0f));
        Frame(b, 3f);
        targetCenter.x = Mathf.Lerp(targetCenter.x, shooter.transform.position.x, 0.08f);
    }

    public void FollowShot() {
        following = true;
        userControl = false;
        overviewAt = -1f;
        smooth = 0.3f;
    }

    void LateUpdate() {
        HandleInput();
        if (overviewAt > 0f && Time.time >= overviewAt) { overviewAt = -1f; Overview(); }

        if (following && !userControl) {
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (Projectile p in Projectile.Active) {
                if (p == null || !p.cameraFollows) continue;
                sum += p.transform.position + p.Velocity * 0.18f;
                n++;
            }
            if (n > 0) {
                lastFollowPoint = sum / n;
                holdUntil = Time.time + 1.1f;
                targetCenter = new Vector2(lastFollowPoint.x, Mathf.Max(lastFollowPoint.y, World.WaterY + 8f));
                targetDistance = 34f;
            } else if (Time.time > holdUntil) {
                following = false;
                Overview();
            } else {
                targetCenter = new Vector2(lastFollowPoint.x, Mathf.Max(lastFollowPoint.y, World.WaterY + 8f));
                targetDistance = 30f;
            }
        }

        float dt = Time.unscaledDeltaTime;
        center = Vector2.SmoothDamp(center, targetCenter, ref centerVel, smooth, Mathf.Infinity, dt);
        distance = Mathf.SmoothDamp(distance, targetDistance, ref distanceVel, smooth, Mathf.Infinity, dt);

        Quaternion rot = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 pos = new Vector3(center.x, center.y, 0f) - rot * Vector3.forward * distance;

        trauma = Mathf.Max(0f, trauma - dt * 1.4f);
        float s = trauma * trauma;
        if (s > 0f) {
            float tt = Time.unscaledTime * 22f;
            pos += new Vector3(Mathf.PerlinNoise(tt, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, tt) - 0.5f, 0f) * s * 2.4f;
            rot *= Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(tt * 0.6f, 5.1f) - 0.5f) * s * 6f);
        }
        transform.SetPositionAndRotation(pos, rot);
    }

    void HandleInput() {
        Mouse m = Mouse.current;
        if (m == null || TimeFX.Paused) return;
        float scroll = m.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f && !PointerOverUI()) {
            userControl = true;
            following = false;
            targetDistance = Mathf.Clamp(targetDistance * (scroll > 0f ? 0.88f : 1.13f), minDistance, maxDistance);
        }
        bool panButton = m.rightButton.isPressed || m.middleButton.isPressed;
        HumanShotController human = battle != null ? battle.Human : null;
        if (human != null && human.IsDragging) panButton = false;
        Vector2 mp = m.position.ReadValue();
        if (panButton) {
            if (!panning) { panning = true; panLast = mp; }
            Vector2 delta = mp - panLast;
            panLast = mp;
            if (delta.sqrMagnitude > 0f) {
                userControl = true;
                following = false;
                float worldPerPixel = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
                targetCenter -= delta * worldPerPixel;
                center -= delta * worldPerPixel;
            }
        } else panning = false;
    }

    static bool PointerOverUI() {
        return UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }
}

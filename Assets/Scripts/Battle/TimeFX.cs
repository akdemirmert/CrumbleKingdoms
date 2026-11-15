using UnityEngine;

// Pause and slow motion share Time.timeScale; this keeps them from fighting.
// The physics step is scaled with slow motion so falling blocks stay smooth.
public static class TimeFX {
    public static bool Paused { get; private set; }
    static float slowScale = 1f, slowUntil;
    static float speed = 1f;

    // Global game speed (1 = normal). Used by the automated playtest to run faster.
    public static float Speed { get => speed; set { speed = Mathf.Clamp(value, 0.1f, 4f); Apply(); } }

    public static void SetPaused(bool paused) { Paused = paused; Apply(); }

    // Overlapping calls combine: the slower scale and the later end win, so a short hit-stop
    // never cuts a longer slow motion short.
    public static void SlowMo(float scale, float realSeconds) {
        scale = Mathf.Clamp(scale, 0.05f, 1f);
        bool active = slowScale < 1f && Time.unscaledTime < slowUntil;
        slowScale = active ? Mathf.Min(slowScale, scale) : scale;
        slowUntil = Mathf.Max(active ? slowUntil : 0f, Time.unscaledTime + realSeconds);
        Apply();
    }

    public static void Reset() { Paused = false; slowScale = 1f; Apply(); }

    // Called every frame by BattleManager.
    public static void Tick() {
        if (slowScale < 1f && Time.unscaledTime >= slowUntil) { slowScale = 1f; Apply(); }
    }

    static void Apply() {
        Time.timeScale = Paused ? 0f : slowScale * speed;
        Time.fixedDeltaTime = World.FixedStep * Mathf.Max(0.05f, slowScale);
    }
}

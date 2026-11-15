using UnityEngine;

public enum SoundId {
    // launches
    Launch, LaunchHeavy, KnifeThrow, MagicCast, Illusion, SpearThrow,
    // hits on blocks
    ImpactWood, ImpactStone, ImpactMetal, ImpactGlass, Pierce, Stick, Bounce,
    // breaking
    BreakWood, BreakStone, BreakMetal, Shatter,
    // blasts and spells
    ExplosionBig, ExplosionSmall, Fuse, FireIgnite, LightningCrack, Thunder, Portal, MeteorFall,
    VoidHum, VoidBurst, Horn, HammerHit, HammerReturn, Splash,
    // commanders
    Hurt, Death,
    // interface and flow
    UIClick, UIHover, UIBack, TurnStart, Victory, Defeat, Star,
    // voice-count key for clips played with SFX.PlayClip (never synthesised by SoundBank)
    Custom,
}

// Sound effects API. Clips are synthesised (SoundBank) and played on a pool of 2D sources (SfxPool).
// World sounds are panned by their x offset from the camera and get quieter far off-screen.
public static class SFX {
    const int MaxSameSound = 4;          // voices of one sound playing at once
    const int ImpactBudget = 8;          // block impacts allowed per window
    const float ImpactWindow = 0.1f;

    static float impactWindowStart = -1f;
    static int impactCount;
    static float cachedVolume = -1f;

    // GameSettings.SfxVolume, cached and refreshed whenever the settings change.
    public static float Volume {
        get {
            if (cachedVolume < 0f) {
                cachedVolume = GameSettings.SfxVolume;
                GameSettings.Changed += () => cachedVolume = GameSettings.SfxVolume;
            }
            return cachedVolume;
        }
    }

    // pos == null: non-positional (UI). volume 0..1. pitch 1 = a random ±5 % variation.
    public static void Play(SoundId id, Vector3? pos = null, float volume = 1f, float pitch = 1f) {
        if (volume <= 0f || Volume <= 0f) return;
        SfxPool pool = SfxPool.I;
        if (pool.CountPlaying(id) >= MaxSameSound) return;
        AudioClip clip = SoundBank.Get(id);
        if (clip == null) return;
        float pan = 0f, attenuation = 1f;
        if (pos.HasValue) Spatialize(pos.Value, out pan, out attenuation);
        float p = pitch == 1f ? Random.Range(0.95f, 1.05f) : pitch;
        if (pos.HasValue && Time.timeScale > 0f && Time.timeScale < 1f) p *= Mathf.Lerp(0.8f, 1f, Time.timeScale);   // slow motion sounds deeper
        pool.Play(id, clip, volume * attenuation * SoundBank.Gain(id) * Volume, p, pan);
    }

    // A clip made elsewhere (e.g. a class's own synthesised sounds) through the same pool, panning and volume.
    public static void PlayClip(AudioClip clip, Vector3? pos = null, float volume = 1f, float pitch = 1f) {
        if (clip == null || volume <= 0f || Volume <= 0f) return;
        SfxPool pool = SfxPool.I;
        if (pool.CountPlaying(SoundId.Custom) >= 16) return;
        float pan = 0f, attenuation = 1f;
        if (pos.HasValue) Spatialize(pos.Value, out pan, out attenuation);
        float p = pitch == 1f ? Random.Range(0.95f, 1.05f) : pitch;
        pool.Play(SoundId.Custom, clip, volume * attenuation * Volume, p, pan);
    }

    // Block-on-block impacts, throttled; strength01 scales volume and pitch (harder = louder and deeper).
    public static void PlayImpact(BlockMaterial mat, Vector3 pos, float strength01) {
        float s = Mathf.Clamp01(strength01);
        if (s < 0.04f) return;
        float now = Time.unscaledTime;
        if (now - impactWindowStart > ImpactWindow) { impactWindowStart = now; impactCount = 0; }
        if (++impactCount > ImpactBudget) return;
        Play(ImpactSound(mat), pos, Mathf.Lerp(0.25f, 1f, s), Mathf.Lerp(1.12f, 0.9f, s) * Random.Range(0.96f, 1.04f));
    }

    // Looping sound that follows a transform (fuse hiss, void hum). Stop with SFX.Stop; if the transform is
    // destroyed first, the loop fades out by itself.
    public static AudioSource PlayLoop(SoundId id, Transform follow, float volume = 1f) {
        AudioClip clip = SoundBank.Get(id);
        if (clip == null) return null;
        GameObject go = new GameObject("[Loop] " + id);
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        go.AddComponent<LoopVoice>().Init(follow, volume * SoundBank.Gain(id));
        source.Play();
        return source;
    }

    public static void Stop(AudioSource source) {
        if (source == null) return;
        LoopVoice loop = source.GetComponent<LoopVoice>();
        if (loop != null) loop.FadeOut();
        else if (source.GetComponent<SfxPool>() != null) source.Stop();   // never destroy the shared pool
        else Object.Destroy(source.gameObject);
    }

    // Stereo pan from the x offset to the camera (full pan at the screen edge); off-screen sounds fade.
    public static void Spatialize(Vector3 pos, out float pan, out float attenuation) {
        pan = 0f;
        attenuation = 1f;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 c = cam.transform.position;
        float depth = Mathf.Max(5f, Mathf.Abs(pos.z - c.z));
        float halfWidth = cam.orthographic ? cam.orthographicSize * cam.aspect
                                           : depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;
        halfWidth = Mathf.Max(1f, halfWidth);
        float dx = pos.x - c.x;
        pan = Mathf.Clamp(dx / halfWidth, -1f, 1f) * 0.7f;
        float outside = Mathf.Max(0f, Mathf.Abs(dx) - halfWidth) / halfWidth;
        attenuation = 1f / (1f + outside * 1.5f);
        attenuation *= Mathf.Lerp(1f, 0.7f, Mathf.InverseLerp(35f, 110f, depth));   // zoomed far out
    }

    static SoundId ImpactSound(BlockMaterial m) {
        switch (m) {
            case BlockMaterial.Stone: return SoundId.ImpactStone;
            case BlockMaterial.Metal: return SoundId.ImpactMetal;
            case BlockMaterial.Glass:
            case BlockMaterial.Ice: return SoundId.ImpactGlass;
            default: return SoundId.ImpactWood;
        }
    }
}

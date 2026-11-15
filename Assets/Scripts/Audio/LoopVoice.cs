using UnityEngine;

// A looping sound (SFX.PlayLoop) that follows a transform with the same pan and distance rules as
// SFX.Play, fades in and out, pauses with the game, and fades out by itself if its target disappears.
public class LoopVoice : MonoBehaviour {
    const float FadeInTime = 0.08f, FadeOutTime = 0.15f;

    AudioSource source;
    Transform follow;
    bool positional, stopping;
    float volume, gain;

    public void Init(Transform follow, float volume) {
        source = GetComponent<AudioSource>();
        this.follow = follow;
        positional = follow != null;
        this.volume = volume;
    }

    public void FadeOut() { stopping = true; }

    void Update() {
        if (source == null) { Destroy(gameObject); return; }
        if (positional && follow == null) stopping = true;
        if (TimeFX.Paused) {
            if (stopping) Destroy(gameObject);
            else if (source.isPlaying) source.Pause();
            return;
        }
        if (!source.isPlaying) source.UnPause();
        gain = Mathf.MoveTowards(gain, stopping ? 0f : 1f, Time.unscaledDeltaTime / (stopping ? FadeOutTime : FadeInTime));
        if (stopping && gain <= 0f) { Destroy(gameObject); return; }
        float pan = 0f, attenuation = 1f;
        if (follow != null) {
            transform.position = follow.position;
            SFX.Spatialize(follow.position, out pan, out attenuation);
        }
        source.panStereo = pan;
        source.volume = volume * attenuation * gain * SFX.Volume;
    }
}

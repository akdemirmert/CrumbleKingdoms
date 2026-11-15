using System.Collections;
using UnityEngine;

// A hidden, persistent pool of 2D AudioSources for one-shot sounds. When the game starts it also
// synthesises every clip in the background, one per frame, so the first explosion never waits for its sound.
public class SfxPool : MonoBehaviour {
    const int Size = 20;
    static SfxPool instance;

    readonly AudioSource[] sources = new AudioSource[Size];
    readonly SoundId[] sounds = new SoundId[Size];
    readonly float[] startedAt = new float[Size];

    public static SfxPool I {
        get {
            if (instance == null) {
                GameObject go = new GameObject("[SFX]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<SfxPool>();
            }
            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { _ = I; }

    void Awake() {
        for (int i = 0; i < Size; i++) {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 0f;
            sources[i] = s;
        }
        StartCoroutine(WarmUp());
    }

    void OnDestroy() { if (instance == this) instance = null; }

    // Plays on a free source, or replaces the sound that has been playing longest.
    public void Play(SoundId id, AudioClip clip, float volume, float pitch, float pan) {
        int pick = 0;
        float oldest = float.MaxValue;
        for (int i = 0; i < Size; i++) {
            if (!sources[i].isPlaying) { pick = i; break; }
            if (startedAt[i] < oldest) { oldest = startedAt[i]; pick = i; }
        }
        AudioSource s = sources[pick];
        s.Stop();
        s.clip = clip;
        s.volume = Mathf.Clamp01(volume);
        s.pitch = Mathf.Clamp(pitch, 0.3f, 3f);
        s.panStereo = Mathf.Clamp(pan, -1f, 1f);
        s.Play();
        sounds[pick] = id;
        startedAt[pick] = Time.unscaledTime;
    }

    public int CountPlaying(SoundId id) {
        int n = 0;
        for (int i = 0; i < Size; i++)
            if (sounds[i] == id && sources[i].isPlaying) n++;
        return n;
    }

    IEnumerator WarmUp() {
        yield return null;                                  // let the first scene start first
        for (int i = 0; i < SoundBank.Count; i++) {
            SoundId id = (SoundId)i;
            if (SoundBank.IsBuilt(id)) continue;
            SoundBank.Get(id);
            yield return null;
        }
    }
}

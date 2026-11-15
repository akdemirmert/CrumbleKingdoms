using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Plays the procedural music: one looping track at a time with cross-fades, plus a wind-and-sea ambience
// under battles. It lives across scene loads; each track is generated on first use (a few ms per frame,
// see MusicComposer) and then cached.
public class MusicPlayer : MonoBehaviour {
    const float MusicLevel = 0.5f;            // the music sits quietly under the game
    const float AmbienceLevel = 0.22f;
    const float FadeSeconds = 2f;
    const float BuildTimeout = 30f;           // a track that never finishes building is given up on
    const int SliceSamples = 65536;           // clip upload per frame

    static MusicPlayer instance;

    readonly Dictionary<MusicTrack, AudioClip> clips = new Dictionary<MusicTrack, AudioClip>();
    readonly HashSet<MusicTrack> failed = new HashSet<MusicTrack>();
    readonly AudioSource[] decks = new AudioSource[2];
    readonly float[] deckGain = new float[2];
    AudioSource ambience;
    float ambienceGain, musicVolume, sfxVolume, buildStarted;
    int activeDeck = -1;
    MusicTrack? wanted, playing, building;
    bool wantAmbience;

    public static bool Exists => instance != null;

    public static MusicPlayer I {
        get {
            if (instance == null) {
                GameObject go = new GameObject("[Music]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<MusicPlayer>();
            }
            return instance;
        }
    }

    public void Play(MusicTrack track, bool withAmbience) {
        wanted = track;
        wantAmbience = withAmbience;
    }

    public void StopAll() {
        wanted = null;
        wantAmbience = false;
    }

    void Awake() {
        for (int i = 0; i < decks.Length; i++) decks[i] = NewSource();
        ambience = NewSource();
        ReadVolumes();
        GameSettings.Changed += ReadVolumes;
    }

    void OnDestroy() {
        GameSettings.Changed -= ReadVolumes;
        if (instance == this) instance = null;
    }

    void ReadVolumes() {
        musicVolume = GameSettings.MusicVolume;
        sfxVolume = GameSettings.SfxVolume;     // the ambience is a world sound
    }

    AudioSource NewSource() {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.volume = 0f;
        s.priority = 0;
        return s;
    }

    void Update() {
        BuildMissing();
        if (wanted != playing) SwitchTrack();
        float step = Time.unscaledDeltaTime / FadeSeconds;
        for (int d = 0; d < decks.Length; d++) {
            deckGain[d] = Mathf.MoveTowards(deckGain[d], d == activeDeck ? 1f : 0f, step);
            decks[d].volume = deckGain[d] * MusicLevel * musicVolume;
            if (deckGain[d] <= 0f && decks[d].isPlaying) decks[d].Stop();
        }
        UpdateAmbience(step);
    }

    // Generates the wanted track (then the ambience), one at a time.
    void BuildMissing() {
        if (building.HasValue) {
            if (Time.unscaledTime - buildStarted < BuildTimeout) return;
            failed.Add(building.Value);
            building = null;
        }
        if (wanted.HasValue && Missing(wanted.Value)) StartCoroutine(Build(wanted.Value));
        else if (wantAmbience && Missing(MusicTrack.Ambience)) StartCoroutine(Build(MusicTrack.Ambience));
    }

    bool Missing(MusicTrack track) { return !clips.ContainsKey(track) && !failed.Contains(track); }

    IEnumerator Build(MusicTrack track) {
        building = track;
        buildStarted = Time.unscaledTime;
        float[] data = null;
        if (track == MusicTrack.Ambience) yield return MusicComposer.RenderAmbience(d => data = d);
        else yield return MusicComposer.Render(MusicThemes.Get(track), d => data = d);
        if (data != null && data.Length > 0) {
            AudioClip clip = AudioClip.Create("Music " + track, data.Length, 1, Synth.Rate, false);
            float[] slice = new float[Mathf.Min(SliceSamples, data.Length)];
            for (int i = 0; i < data.Length; i += SliceSamples) {      // upload in slices, never one long stall
                int n = Mathf.Min(SliceSamples, data.Length - i);
                if (slice.Length != n) slice = new float[n];
                System.Array.Copy(data, i, slice, 0, n);
                clip.SetData(slice, i);
                yield return null;
            }
            clips[track] = clip;
        } else failed.Add(track);
        building = null;
    }

    // Starts the wanted track on the free deck as soon as its clip exists; the decks cross-fade in Update.
    void SwitchTrack() {
        if (!wanted.HasValue) {
            activeDeck = -1;
            playing = null;
            return;
        }
        if (!clips.TryGetValue(wanted.Value, out AudioClip clip)) return;
        int deck = activeDeck == 0 ? 1 : 0;
        decks[deck].clip = clip;
        decks[deck].time = 0f;
        decks[deck].Play();
        deckGain[deck] = 0f;
        activeDeck = deck;
        playing = wanted;
    }

    void UpdateAmbience(float step) {
        bool on = false;
        if (wantAmbience && clips.TryGetValue(MusicTrack.Ambience, out AudioClip clip)) {
            on = true;
            if (ambience.clip != clip) ambience.clip = clip;
            if (!ambience.isPlaying) ambience.Play();
        }
        ambienceGain = Mathf.MoveTowards(ambienceGain, on ? 1f : 0f, step);
        ambience.volume = ambienceGain * AmbienceLevel * sfxVolume;
        if (ambienceGain <= 0f && ambience.isPlaying) ambience.Stop();
    }
}

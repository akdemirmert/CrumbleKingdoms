using System;
using UnityEngine;

// Campaign progress (PlayerPrefs).
public static class SaveData {
    const string KeyUnlocked = "sog.unlocked", KeyStars = "sog.stars.", KeyClass = "sog.class";

    public static int UnlockedCount => Mathf.Clamp(PlayerPrefs.GetInt(KeyUnlocked, 1), 1, LevelDefs.Count);

    public static bool IsUnlocked(int level) { return level < UnlockedCount; }

    public static int GetStars(int level) { return PlayerPrefs.GetInt(KeyStars + level, 0); }

    public static int TotalStars {
        get { int s = 0; for (int i = 0; i < LevelDefs.Count; i++) s += GetStars(i); return s; }
    }

    // The automated playtest turns saving off so its bot battles never touch real progress.
    public static bool Disabled;

    public static void RecordWin(int level, int stars) {
        if (Disabled) return;
        if (stars > GetStars(level)) PlayerPrefs.SetInt(KeyStars + level, stars);
        int unlock = Mathf.Min(level + 2, LevelDefs.Count);
        if (unlock > UnlockedCount) PlayerPrefs.SetInt(KeyUnlocked, unlock);
        PlayerPrefs.Save();
    }

    public static HeroClass LastClass {
        get { return (HeroClass)Mathf.Clamp(PlayerPrefs.GetInt(KeyClass, 0), 0, Classes.Count - 1); }
        set { PlayerPrefs.SetInt(KeyClass, (int)value); PlayerPrefs.Save(); }
    }

    public static void ResetProgress() {
        PlayerPrefs.DeleteKey(KeyUnlocked);
        for (int i = 0; i < LevelDefs.Count; i++) PlayerPrefs.DeleteKey(KeyStars + i);
        PlayerPrefs.Save();
    }
}

// Player options (PlayerPrefs). Apply() pushes them into the engine.
public static class GameSettings {
    public static event Action Changed;

    public static float MasterVolume { get => PlayerPrefs.GetFloat("sog.vol.master", 0.85f); set { PlayerPrefs.SetFloat("sog.vol.master", value); Apply(); } }
    public static float MusicVolume { get => PlayerPrefs.GetFloat("sog.vol.music", 0.55f); set { PlayerPrefs.SetFloat("sog.vol.music", value); Apply(); } }
    public static float SfxVolume { get => PlayerPrefs.GetFloat("sog.vol.sfx", 0.9f); set { PlayerPrefs.SetFloat("sog.vol.sfx", value); Apply(); } }
    public static bool ScreenShake { get => PlayerPrefs.GetInt("sog.shake", 1) == 1; set { PlayerPrefs.SetInt("sog.shake", value ? 1 : 0); Apply(); } }
    public static bool Fullscreen {
        get => Screen.fullScreen;
        set { Screen.fullScreenMode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed; Changed?.Invoke(); }
    }

    public static void Apply() {
        AudioListener.volume = MasterVolume;
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}

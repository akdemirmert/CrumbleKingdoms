// Background music API: procedural loops that cross-fade and keep playing across scene loads (MusicPlayer).
public static class Music {
    public static void PlayMenu() { MusicPlayer.I.Play(MusicTrack.Menu, false); }

    // The arena's battle theme, with the wind-and-sea ambience underneath.
    public static void PlayBattle(ArenaTheme theme) { MusicPlayer.I.Play(TrackFor(theme), true); }

    public static void Stop() { if (MusicPlayer.Exists) MusicPlayer.I.StopAll(); }

    static MusicTrack TrackFor(ArenaTheme theme) {
        switch (theme) {
            case ArenaTheme.Desert: return MusicTrack.Desert;
            case ArenaTheme.Frost: return MusicTrack.Frost;
            case ArenaTheme.Volcano: return MusicTrack.Volcano;
            case ArenaTheme.Asgard: return MusicTrack.Asgard;
            default: return MusicTrack.Meadow;
        }
    }
}

using UnityEngine;

// Choices that travel from the menu scene to the battle scene, plus scene changes.
public static class GameSession {
    public const string MenuScene = "MainMenu";
    public const string BattleScene = "Battle";

    public static GameMode Mode = GameMode.Campaign;
    public static int LevelIndex;
    public static HeroClass PlayerClass = HeroClass.Soldier;
    public static DuelOptions Duel = new DuelOptions();

    // Which menu panel to open when the menu scene loads ("levels", "duel" or null for the title).
    public static string MenuStartPanel;

    // Testing / attract mode: every side is played by bots; ForcedSlot >= 0 makes bots use that slot.
    public static bool AutoPlay;
    public static int ForcedSlot = -1;
    public static bool HideBotAim;

    public static BattleSetup CreateSetup() {
        return Mode == GameMode.Campaign ? LevelDefs.CreateCampaign(LevelIndex, PlayerClass) : LevelDefs.CreateDuel(Duel);
    }

    public static void StartCampaignLevel(int index) {
        Mode = GameMode.Campaign;
        LevelIndex = Mathf.Clamp(index, 0, LevelDefs.Count - 1);
        SaveData.LastClass = PlayerClass;
        SceneFader.Load(BattleScene);
    }

    // A new duel: a coin toss decides who throws first.
    public static void StartDuel() {
        Mode = GameMode.Duel;
        Duel.firstTeam = Random.value < 0.5f ? 0 : 1;
        SceneFader.Load(BattleScene);
    }

    public static bool HasNextLevel => Mode == GameMode.Campaign && LevelIndex + 1 < LevelDefs.Count;

    public static void NextLevel() { StartCampaignLevel(LevelIndex + 1); }

    // Retry / rematch. In a duel the other side starts the rematch.
    public static void Restart() {
        if (Mode == GameMode.Duel) Duel.firstTeam = 1 - Duel.firstTeam;
        SceneFader.Load(BattleScene);
    }

    public static void ToMenu(string panel = null) {
        MenuStartPanel = panel;
        TimeFX.Reset();
        SceneFader.Load(MenuScene);
    }
}

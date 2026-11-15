using UnityEngine;

// The living diorama behind the main menu: two fortresses on their islands, built with the real battle
// code (so the menu shows the actual game), and a camera that slowly drifts around them.
// The arena theme follows the player's progress: it is the theme of the furthest unlocked level.
public class MenuBackdrop : MonoBehaviour {
    static readonly Vector3 Focus = new Vector3(0f, 8f, 0f);
    const float HalfWidth = 31f;   // metres that must stay in view on each side of the focus

    Camera cam;
    float time;

    public static MenuBackdrop Create(Transform parent) {
        GameObject go = new GameObject("MenuBackdrop");
        go.transform.SetParent(parent, false);
        return go.AddComponent<MenuBackdrop>();
    }

    void Start() {
        cam = Camera.main;
        if (cam != null && cam.TryGetComponent(out BattleCamera battleCam)) battleCam.enabled = false;
        Build();
        LateUpdate();
    }

    void Build() {
        LevelDef level = LevelDefs.Levels[Mathf.Clamp(SaveData.UnlockedCount - 1, 0, LevelDefs.Count - 1)];
        BattleSetup s = new BattleSetup { title = "Siege of Gods", subtitle = "", theme = level.theme, night = level.night, windMax = 0f };
        s.islands.Add(new IslandSpec(-31f, -9f, 0f));
        s.islands.Add(new IslandSpec(9f, 31f, 0f));
        HeroClass mine = SaveData.LastClass;
        s.fortresses.Add(Side(0, -20f, mine, FortressStyle.Keep));
        s.fortresses.Add(Side(1, 20f, (HeroClass)(((int)mine + 1) % Classes.Count), FortressStyle.Watchtower));
        try {
            ArenaBuilder.Build(s);
            for (int i = 0; i < s.fortresses.Count; i++) Fortress.Build(s.fortresses[i], i);
        } catch (System.Exception e) {
            // The menu must stay usable even if the world code has a problem.
            Debug.LogWarning("[Siege] menu backdrop failed: " + e);
        }
    }

    static FortressSetup Side(int team, float x, HeroClass c, FortressStyle style) {
        return new FortressSetup {
            team = team, name = team == 0 ? "Hero" : "Rival", heroClass = c, controller = ControllerType.Bot,
            ai = AIProfile.FromSkill(0.5f), style = style, tier = 1, glass = true, seed = 11 + team * 17,
            x = x, groundY = 0f, facing = team == 0 ? 1 : -1, commanderHP = 100f, teamColor = LevelDefs.TeamColors[team],
        };
    }

    void LateUpdate() {
        TimeFX.Tick();   // there is no BattleManager here, so a stray slow-motion would otherwise stick
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        time += Time.unscaledDeltaTime;
        // Slow orbit around the gap between the islands, framed so both fortresses stay in view.
        float yaw = Mathf.Sin(time * 0.06f) * 9f;
        float pitch = 5f + Mathf.Sin(time * 0.09f) * 1.5f;
        float tan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float distance = HalfWidth / (tan * Mathf.Max(1f, cam.aspect));
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        cam.transform.SetPositionAndRotation(Focus - rot * Vector3.forward * distance, rot);
    }
}

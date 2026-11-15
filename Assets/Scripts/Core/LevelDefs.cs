using System.Collections.Generic;
using UnityEngine;

// What a battle needs to be built: islands, fortresses, theme, wind. Campaign levels and duels both
// produce a BattleSetup; BattleManager only reads this.

public class IslandSpec {
    public float xMin, xMax, topY;
    public IslandSpec(float xMin, float xMax, float topY) { this.xMin = xMin; this.xMax = xMax; this.topY = topY; }
}

public class FortressSetup {
    public int team;                 // 0 = left side (player / player 1)
    public string name;
    public HeroClass heroClass;
    public ControllerType controller;
    public AIProfile ai;             // bots only
    public FortressStyle style;
    public int tier;                 // 0 wood .. 3 stone/metal heavy
    public bool glass, ice;          // glass windows, ice blocks
    public int kegs;                 // explosive powder kegs inside
    public int seed;
    public float x, groundY;         // centre of the base on its island top
    public int facing;               // +1 shoots right, -1 shoots left
    public float commanderHP = 100f;
    public Color teamColor;
}

public class BattleSetup {
    public string title, subtitle;
    public int levelIndex = -1;      // -1 = duel
    public ArenaTheme theme;
    public bool night;
    public float windMax;            // m/s² of horizontal acceleration
    public float defeatIntegrity = 0f;   // fortress ruin alone no longer defeats anyone (HUD marker only)
    public int firstTeam;            // team that takes the first turn (duels: decided by a coin toss)
    public readonly List<IslandSpec> islands = new List<IslandSpec>();
    public readonly List<FortressSetup> fortresses = new List<FortressSetup>();
}

public class LevelDef {
    public string name, subtitle;
    public ArenaTheme theme;
    public bool night;
    public float wind, skill, playerHP, enemyHP;
    public FortressStyle playerStyle;
    public int playerTier;
    public IslandSpec playerIsland;
    public float playerX;
    public EnemyDef[] enemies;

    public string ModeLabel => "1 v " + enemies.Length;
}

public class EnemyDef {
    public HeroClass heroClass;
    public FortressStyle style;
    public int tier;
    public bool glass, ice;
    public int kegs;
    public float x, groundY, islandMin, islandMax;

    public EnemyDef(HeroClass c, FortressStyle s, int tier, float x, float y, float iMin, float iMax,
                    bool glass = false, bool ice = false, int kegs = 0) {
        heroClass = c; style = s; this.tier = tier; this.x = x; groundY = y; islandMin = iMin; islandMax = iMax;
        this.glass = glass; this.ice = ice; this.kegs = kegs;
    }
}

public static class LevelDefs {
    public static readonly Color[] TeamColors = {
        WeaponDefs.Hex("3A86FF"), WeaponDefs.Hex("E63946"), WeaponDefs.Hex("9B5DE5"), WeaponDefs.Hex("F77F00"),
    };

    const HeroClass S = HeroClass.Soldier, W = HeroClass.Wizard, G = HeroClass.God,
                     V = HeroClass.Vampire, A = HeroClass.Alien, D = HeroClass.Dali;

    public static readonly LevelDef[] Levels = {
        new LevelDef { name = "First Siege", subtitle = "A wooden outpost guards the shore.", theme = ArenaTheme.Meadow,
            wind = 0f, skill = 0f, playerHP = 100, enemyHP = 70, playerStyle = FortressStyle.Keep, playerTier = 1,
            playerIsland = new IslandSpec(-38, -12, 0), playerX = -25,
            enemies = new[] { new EnemyDef(S, FortressStyle.Outpost, 0, 22, 0, 12, 33) } },
        new LevelDef { name = "The Wizard's Watch", subtitle = "A glass-windowed tower on the hill.", theme = ArenaTheme.Meadow,
            wind = 0.5f, skill = 0.12f, playerHP = 100, enemyHP = 80, playerStyle = FortressStyle.Keep, playerTier = 1,
            playerIsland = new IslandSpec(-38, -12, 0), playerX = -25,
            enemies = new[] { new EnemyDef(W, FortressStyle.Watchtower, 1, 24, 1.5f, 14, 35, glass: true) } },
        new LevelDef { name = "Hall of Thunder", subtitle = "A god waits in a frozen keep.", theme = ArenaTheme.Frost,
            wind = 0.8f, skill = 0.25f, playerHP = 100, enemyHP = 90, playerStyle = FortressStyle.Keep, playerTier = 1,
            playerIsland = new IslandSpec(-38, -12, 0), playerX = -25,
            enemies = new[] { new EnemyDef(G, FortressStyle.Keep, 1, 24, 0, 13, 36, ice: true) } },
        new LevelDef { name = "Twin Towers", subtitle = "Two towers, one above the other. Now it's 1 v 2.", theme = ArenaTheme.Desert,
            wind = 0.9f, skill = 0.3f, playerHP = 125, enemyHP = 70, playerStyle = FortressStyle.Bastion, playerTier = 1,
            playerIsland = new IslandSpec(-39, -11, 0), playerX = -25,
            enemies = new[] { new EnemyDef(S, FortressStyle.Watchtower, 1, 18, 0, 10, 26),
                              new EnemyDef(W, FortressStyle.Watchtower, 1, 33, 5, 26, 41, glass: true) } },
        new LevelDef { name = "Persistence of Sand", subtitle = "Melting clocks in the desert heat: Dalí and a god.", theme = ArenaTheme.Desert,
            wind = 1.0f, skill = 0.38f, playerHP = 125, enemyHP = 75, playerStyle = FortressStyle.Bastion, playerTier = 2,
            playerIsland = new IslandSpec(-39, -11, 0), playerX = -25,
            enemies = new[] { new EnemyDef(D, FortressStyle.Bastion, 1, 19, 0, 10, 27, kegs: 2),
                              new EnemyDef(G, FortressStyle.Outpost, 2, 35, 6, 27, 43) } },
        new LevelDef { name = "Frozen Bastion", subtitle = "A vampire's castle in the snow. Ice is slippery and brittle.", theme = ArenaTheme.Frost, night = true,
            wind = 1.15f, skill = 0.46f, playerHP = 125, enemyHP = 80, playerStyle = FortressStyle.Bastion, playerTier = 2,
            playerIsland = new IslandSpec(-39, -11, 0), playerX = -25,
            enemies = new[] { new EnemyDef(V, FortressStyle.Bastion, 2, 19, 0, 10, 27, ice: true),
                              new EnemyDef(S, FortressStyle.Watchtower, 2, 35, 6, 27, 43, ice: true, glass: true) } },
        new LevelDef { name = "Molten Gate", subtitle = "A wizard and a visitor from the stars on the volcano's flank.", theme = ArenaTheme.Volcano, night = true,
            wind = 1.3f, skill = 0.54f, playerHP = 130, enemyHP = 80, playerStyle = FortressStyle.Bastion, playerTier = 2,
            playerIsland = new IslandSpec(-39, -11, 0), playerX = -25,
            enemies = new[] { new EnemyDef(W, FortressStyle.Citadel, 2, 21, 0, 10, 31, kegs: 2),
                              new EnemyDef(A, FortressStyle.Ziggurat, 2, 38, 7, 31, 46) } },
        new LevelDef { name = "Three Crowns", subtitle = "Soldier, wizard and god. 1 v 3.", theme = ArenaTheme.Volcano,
            wind = 1.4f, skill = 0.58f, playerHP = 150, enemyHP = 70, playerStyle = FortressStyle.Citadel, playerTier = 2,
            playerIsland = new IslandSpec(-41, -11, 0), playerX = -26,
            enemies = new[] { new EnemyDef(S, FortressStyle.Outpost, 2, 16, 0, 9, 22),
                              new EnemyDef(W, FortressStyle.Watchtower, 2, 29, 4, 22, 36, glass: true),
                              new EnemyDef(G, FortressStyle.Keep, 2, 43, 8, 36, 51, kegs: 1) } },
        new LevelDef { name = "The Strange Alliance", subtitle = "A vampire, an alien and a painter on the rainbow road.", theme = ArenaTheme.Asgard,
            wind = 1.6f, skill = 0.68f, playerHP = 150, enemyHP = 80, playerStyle = FortressStyle.Citadel, playerTier = 3,
            playerIsland = new IslandSpec(-41, -11, 0), playerX = -26,
            enemies = new[] { new EnemyDef(V, FortressStyle.Watchtower, 3, 16, 0, 9, 22, glass: true),
                              new EnemyDef(A, FortressStyle.Ziggurat, 3, 30, 4, 22, 37),
                              new EnemyDef(D, FortressStyle.Bastion, 3, 44, 8, 37, 52, kegs: 2) } },
        new LevelDef { name = "Ragnarok", subtitle = "Three gods. The end of the world.", theme = ArenaTheme.Asgard, night = true,
            wind = 1.8f, skill = 0.8f, playerHP = 160, enemyHP = 90, playerStyle = FortressStyle.Citadel, playerTier = 3,
            playerIsland = new IslandSpec(-41, -11, 0), playerX = -26,
            enemies = new[] { new EnemyDef(G, FortressStyle.Keep, 3, 17, 0, 9, 24, glass: true),
                              new EnemyDef(G, FortressStyle.Citadel, 3, 32, 5, 24, 41, kegs: 2),
                              new EnemyDef(G, FortressStyle.Watchtower, 3, 46, 10, 41, 53) } },
    };

    public static int Count => Levels.Length;

    public static BattleSetup CreateCampaign(int index, HeroClass playerClass) {
        LevelDef L = Levels[Mathf.Clamp(index, 0, Levels.Length - 1)];
        BattleSetup s = new BattleSetup {
            title = "Level " + (index + 1) + " — " + L.name, subtitle = L.subtitle, levelIndex = index,
            theme = L.theme, night = L.night, windMax = L.wind,
        };
        s.islands.Add(L.playerIsland);
        s.fortresses.Add(new FortressSetup {
            team = 0, name = "You", heroClass = playerClass, controller = ControllerType.Human,
            style = L.playerStyle, tier = L.playerTier, glass = index >= 2, seed = 1000 + index,
            x = L.playerX, groundY = L.playerIsland.topY, facing = 1, commanderHP = L.playerHP, teamColor = TeamColors[0],
        });
        for (int i = 0; i < L.enemies.Length; i++) {
            EnemyDef e = L.enemies[i];
            s.islands.Add(new IslandSpec(e.islandMin, e.islandMax, e.groundY));
            string suffix = L.enemies.Length > 1 ? " " + (char)('A' + i) : "";
            s.fortresses.Add(new FortressSetup {
                team = 1, name = "Enemy " + ClassDefs.Get(e.heroClass).name + suffix, heroClass = e.heroClass,
                controller = ControllerType.Bot, ai = AIProfile.FromSkill(L.skill),
                style = e.style, tier = e.tier, glass = e.glass, ice = e.ice, kegs = e.kegs, seed = 7 + index * 31 + i * 7,
                x = e.x, groundY = e.groundY, facing = -1, commanderHP = L.enemyHP, teamColor = TeamColors[1 + i],
            });
        }
        return s;
    }

    public static readonly string[] BotDifficultyNames = { "Easy", "Normal", "Hard", "Brutal" };
    static readonly float[] botSkill = { 0.1f, 0.4f, 0.65f, 0.9f };

    public static BattleSetup CreateDuel(DuelOptions o) {
        ArenaTheme theme = o.theme;
        BattleSetup s = new BattleSetup {
            title = "Duel", subtitle = ClassDefs.Get(o.p1Class).name + " vs " + ClassDefs.Get(o.p2Class).name,
            theme = theme, night = o.night, windMax = o.wind ? 1.2f : 0f,
        };
        s.islands.Add(new IslandSpec(-37, -12, 0));
        s.islands.Add(new IslandSpec(12, 37, 0));
        int seed = o.seed;
        s.fortresses.Add(new FortressSetup {
            team = 0, name = "Player 1", heroClass = o.p1Class, controller = ControllerType.Human,
            style = FortressStyle.Keep, tier = 1, glass = true, seed = seed, x = -24, groundY = 0, facing = 1,
            commanderHP = 100, teamColor = TeamColors[0],
        });
        bool bot = o.p2Controller == ControllerType.Bot;
        int d = Mathf.Clamp(o.botDifficulty, 0, 3);
        s.fortresses.Add(new FortressSetup {
            team = 1, name = bot ? "Bot (" + BotDifficultyNames[d] + ")" : "Player 2", heroClass = o.p2Class,
            controller = o.p2Controller, ai = AIProfile.FromSkill(botSkill[d]),
            style = FortressStyle.Keep, tier = 1, glass = true, seed = seed, x = 24, groundY = 0, facing = -1,
            commanderHP = 100, teamColor = TeamColors[1],
        });
        s.firstTeam = Mathf.Clamp(o.firstTeam, 0, 1);
        s.subtitle += "  ·  " + s.fortresses[s.firstTeam].name + " goes first";
        return s;
    }
}

public class DuelOptions {
    public HeroClass p1Class = HeroClass.Soldier, p2Class = HeroClass.Wizard;
    public ControllerType p2Controller = ControllerType.Bot;
    public int botDifficulty = 1;
    public ArenaTheme theme = ArenaTheme.Meadow;
    public bool night;
    public bool wind = true;
    public int seed = 42;
    public int firstTeam;   // 0 = Player 1, 1 = Player 2 / bot (coin toss; a rematch swaps it)
}

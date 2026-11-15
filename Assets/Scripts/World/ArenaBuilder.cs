using System.Collections.Generic;
using UnityEngine;

// Builds the static world of a battle (or of the menu diorama) for its ArenaTheme and time of day:
// islands with their ground colliders and terrain (IslandBuilder, PropKit), the sea or lava (SeaBuilder),
// the sky and distant world (SkyBuilder) and the sun, fog and post-processing (ArenaLighting).
// Every colour and light value comes from ArenaPalette.
public static class ArenaBuilder {
    public const float BackdropFortressX = 20f;   // the menu diorama keeps its fortresses at x = -20 and +20

    public static ArenaTheme Theme { get; private set; }
    public static bool IsNight { get; private set; }

    static GameObject arena;

    public static void Build(BattleSetup s) {
        List<float> fortressXs = new List<float>();
        foreach (FortressSetup f in s.fortresses) fortressXs.Add(f.x);
        Build(s.theme, s.night, s.islands, fortressXs);
    }

    // The menu diorama: two islands around x = -20 and +20 (props keep clear of fortresses standing there),
    // with the sea, sky, background and lighting of the theme.
    public static void BuildBackdrop(ArenaTheme theme, bool night) {
        List<IslandSpec> islands = new List<IslandSpec> { new IslandSpec(-31f, -9f, 0f), new IslandSpec(9f, 31f, 0f) };
        Build(theme, night, islands, new List<float> { -BackdropFortressX, BackdropFortressX });
    }

    static void Build(ArenaTheme theme, bool night, List<IslandSpec> islands, List<float> fortressXs) {
        Theme = theme;
        IsNight = night;
        if (arena != null) Object.Destroy(arena);   // building again in the same scene replaces the old arena
        arena = ModelKit.Root("Arena");
        Transform root = arena.transform;
        ArenaPalette p = ArenaPalette.For(theme, night);

        // Gameplay first: the ground colliders must exist even if a piece of scenery fails.
        List<GameObject> grounds = new List<GameObject>();
        foreach (IslandSpec i in islands) grounds.Add(IslandBuilder.Collider(root, i));

        Safely("lighting", () => ArenaLighting.Apply(p));
        for (int i = 0; i < islands.Count; i++) {
            int index = i;
            Safely("island", () => IslandBuilder.Dress(grounds[index], p, islands, index, fortressXs));
        }
        Safely("sea", () => SeaBuilder.Build(root, p, islands));
        Safely("sky", () => SkyBuilder.Build(root, p));
    }

    // Scenery must never take the battle down with it: report the problem and carry on.
    static void Safely(string part, System.Action build) {
        try { build(); }
        catch (System.Exception e) { Debug.LogError("[Siege] arena " + part + " failed: " + e); }
    }
}

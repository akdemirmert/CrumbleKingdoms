using System.Collections.Generic;
using UnityEngine;

// Registry for the looks of the later classes (Vampire, Alien, Dalí). Each class file registers how its
// commander, its fortress blocks and its fortress decorations look, the same way weapons register with
// WeaponFactory; CommanderModel, ClassSkins/BlockVisuals and FortressDecor ask here first.
public static class ClassLooks {
    public delegate CommanderRig CommanderFn(Color teamColor, int facing, Transform parent);
    public delegate Material SkinFn(BlockMaterial m, int stage);   // null = neutral look for that material
    public delegate void DecorFn(Fortress f);

    class Look { public CommanderFn commander; public SkinFn skin; public DecorFn decor; }

    static readonly Dictionary<HeroClass, Look> looks = new Dictionary<HeroClass, Look>();

    public static void Register(HeroClass c, CommanderFn commander, SkinFn skin, DecorFn decor) {
        looks[c] = new Look { commander = commander, skin = skin, decor = decor };
    }

    public static bool Has(HeroClass c) { return looks.ContainsKey(c); }

    // The registered commander model, or null when the class has none registered.
    public static CommanderRig Commander(HeroClass c, Color teamColor, int facing, Transform parent) {
        return looks.TryGetValue(c, out Look l) && l.commander != null ? l.commander(teamColor, facing, parent) : null;
    }

    public static Material Skin(HeroClass c, BlockMaterial m, int stage) {
        return looks.TryGetValue(c, out Look l) && l.skin != null ? l.skin(m, stage) : null;
    }

    // True when the class decorated the fortress itself.
    public static bool Decorate(Fortress f) {
        if (f == null || !looks.TryGetValue(f.HeroClass, out Look l) || l.decor == null) return false;
        l.decor(f);
        return true;
    }
}

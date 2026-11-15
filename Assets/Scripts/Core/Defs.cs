using UnityEngine;

// All tuning numbers for weapons, classes and block materials live here.

public class WeaponDef {
    public WeaponId id;
    public HeroClass heroClass;
    public int slot;                 // 0..3 inside the class
    public string name;              // short name for the weapon bar
    public string title;             // e.g. "Thor — Mjolnir"
    public string description;       // one line for tooltips
    public string activateHint;      // shown while it flies; null = no mid-air click
    public bool selfCast;            // used on yourself (cloak, space warp): one click fires, no aiming
    public int cooldown;             // turns of the owner before it can be used again
    public float minSpeed, maxSpeed; // launch speed range in m/s
    public float gravityScale = 1f;  // multiplies World.Gravity
    public float windFactor = 1f;    // multiplies the wind acceleration
    public float damage;             // direct / per-block / per-blast damage
    public float radius;             // blast or effect radius
    public float force;              // blast velocity change (m/s) for a 1 kg body at the centre (see Explosion)
    public float pierce;             // pierce budget (0 = none, wood costs 1)
    public ArcPreference arc = ArcPreference.Any;
    public Color color = Color.white;

    public bool CanActivate => activateHint != null;
}

public static class WeaponDefs {
    static WeaponDef[] all;

    public static WeaponDef Get(WeaponId id) { Init(); return all[(int)id]; }

    public static WeaponDef ForClass(HeroClass c, int slot) { Init(); return all[(int)c * 4 + slot]; }

    public static WeaponDef[] All { get { Init(); return all; } }

    static void Init() {
        if (all != null) return;
        all = new WeaponDef[Classes.Count * 4];
        // ---------------------------------------------------------------- Soldier
        Add(new WeaponDef { id = WeaponId.GrapplingHook, name = "Hook", title = "Grappling Hook",
            description = "A hook on a rope: it catches the first block or commander it hits and yanks it out towards you.",
            cooldown = 0, minSpeed = 16, maxSpeed = 40, gravityScale = 0.8f, windFactor = 0.3f,
            damage = 20, force = 14, arc = ArcPreference.Low, color = Hex("C9B07A") });
        Add(new WeaponDef { id = WeaponId.Bomb, name = "Bomb", title = "Fuse Bomb",
            description = "Bouncing fuse bomb with a big blast. Click to detonate it early.",
            activateHint = "Click to DETONATE",
            cooldown = 1, minSpeed = 12, maxSpeed = 33, gravityScale = 1f, windFactor = 0.8f,
            damage = 95, radius = 3.6f, force = 14, arc = ArcPreference.Low, color = Hex("FF8A3D") });   // a high lob outlasts the fuse
        Add(new WeaponDef { id = WeaponId.Drone, name = "Drone", title = "Kamikaze Drone",
            description = "An explosive drone that follows your mouse in flight. Click to blow it up; it also explodes on contact.",
            activateHint = "Steer with the MOUSE, click to BLOW UP",
            cooldown = 2, minSpeed = 10, maxSpeed = 26, gravityScale = 0.6f, windFactor = 0f,
            damage = 55, radius = 2.4f, force = 10, arc = ArcPreference.Low, color = Hex("5ED0FF") });
        Add(new WeaponDef { id = WeaponId.Airstrike, name = "Airstrike", title = "Airstrike",
            description = "Throw a red smoke flare: a bomber sweeps over and drops a row of five bombs across it.",
            cooldown = 3, minSpeed = 12, maxSpeed = 32, gravityScale = 1f, windFactor = 0.5f,
            damage = 40, radius = 2.4f, force = 9, arc = ArcPreference.Any, color = Hex("FF4B3E") });
        // ---------------------------------------------------------------- Wizard
        Add(new WeaponDef { id = WeaponId.Fireball, name = "Fireball", title = "Fireball",
            description = "Explodes on contact and sets wood ablaze. Fire spreads to touching wood.",
            cooldown = 0, minSpeed = 14, maxSpeed = 34, gravityScale = 0.85f, windFactor = 0.9f,
            damage = 60, radius = 2.8f, force = 9, arc = ArcPreference.Any, color = Hex("FF7A1A") });
        Add(new WeaponDef { id = WeaponId.Lightning, name = "Lightning", title = "Lightning Orb",
            description = "Click (or hit) to call a bolt down that column. It chains to metal and commanders.",
            activateHint = "Click to STRIKE",
            cooldown = 1, minSpeed = 14, maxSpeed = 34, gravityScale = 0.8f, windFactor = 0.9f,
            damage = 85, radius = 4.5f, force = 5, arc = ArcPreference.Any, color = Hex("7FD8FF") });
        Add(new WeaponDef { id = WeaponId.MeteorOrb, name = "Meteor", title = "Meteor Orb",
            description = "Opens a portal in the sky: seven burning meteors rain around the spot.",
            activateHint = "Click to OPEN the portal",
            cooldown = 2, minSpeed = 14, maxSpeed = 34, gravityScale = 0.9f, windFactor = 1f,
            damage = 42, radius = 2.2f, force = 8, arc = ArcPreference.High, color = Hex("FFB347") });
        Add(new WeaponDef { id = WeaponId.VoidOrb, name = "Void", title = "Void Orb",
            description = "A black hole pulls the fortress in, crushes it, then bursts.",
            activateHint = "Click to COLLAPSE space",
            cooldown = 3, minSpeed = 12, maxSpeed = 33, gravityScale = 0.8f, windFactor = 0.8f,
            damage = 45, radius = 7f, force = 12, arc = ArcPreference.Any, color = Hex("B07CFF") });
        // ---------------------------------------------------------------- God
        Add(new WeaponDef { id = WeaponId.LokiDaggers, name = "Loki", title = "Loki — Trickster Daggers",
            description = "Four daggers in a fan: one real, three illusions. Each pierces two blocks.",
            cooldown = 0, minSpeed = 16, maxSpeed = 40, gravityScale = 0.6f, windFactor = 0.3f,
            damage = 30, pierce = 2f, arc = ArcPreference.Low, color = Hex("5FD38A") });
        Add(new WeaponDef { id = WeaponId.Mjolnir, name = "Thor", title = "Thor — Mjolnir",
            description = "Smashes through blocks, calls lightning on impact, then flies back to Thor.",
            cooldown = 1, minSpeed = 14, maxSpeed = 36, gravityScale = 1f, windFactor = 0.2f,
            damage = 70, radius = 3.2f, force = 12, arc = ArcPreference.Any, color = Hex("9FC3FF") });
        Add(new WeaponDef { id = WeaponId.Gjallarhorn, name = "Heimdall", title = "Heimdall — Gjallarhorn",
            description = "On impact or click the horn sounds: a rainbow shockwave hurls blocks away.",
            activateHint = "Click to SOUND the horn",
            cooldown = 2, minSpeed = 14, maxSpeed = 34, gravityScale = 0.9f, windFactor = 0.7f,
            damage = 30, radius = 8f, force = 22, arc = ArcPreference.High, color = Hex("FFE066") });
        Add(new WeaponDef { id = WeaponId.Gungnir, name = "Odin", title = "Odin — Gungnir",
            description = "The spear that never misses: homes onto a commander and pierces everything.",
            cooldown = 3, minSpeed = 18, maxSpeed = 40, gravityScale = 0.75f, windFactor = 0f,
            damage = 65, pierce = 99f, arc = ArcPreference.High, color = Hex("FFD24A") });
        // ---------------------------------------------------------------- Vampire
        Add(new WeaponDef { id = WeaponId.BatSwarm, name = "Bats", title = "Bat Swarm",
            description = "A swarm of bats dives at the enemy's blocks; the bats that go for the commander bite their way through his walls.",
            activateHint = "Click to RELEASE the bats",
            cooldown = 0, minSpeed = 14, maxSpeed = 34, gravityScale = 0.5f, windFactor = 0.6f,
            damage = 12, arc = ArcPreference.Any, color = Hex("8C2F45") });
        Add(new WeaponDef { id = WeaponId.VampireFang, name = "Fang", title = "Vampire Fang",
            description = "A thrown fang that pierces up to five blocks (fewer stone or metal). If it bites a commander it drains 30% of his maximum health into yours.",
            cooldown = 1, minSpeed = 18, maxSpeed = 42, gravityScale = 0.6f, windFactor = 0.3f,
            damage = 25, pierce = 5f, arc = ArcPreference.Low, color = Hex("EDE6D6") });
        Add(new WeaponDef { id = WeaponId.Candelabra, name = "Candelabra", title = "Burning Candelabra",
            description = "A spinning candelabra smashes through two blocks, then its three flames burst in fiery blasts and set wood alight.",
            cooldown = 2, minSpeed = 14, maxSpeed = 34, gravityScale = 1f, windFactor = 0.7f,
            damage = 40, radius = 2.4f, force = 8, arc = ArcPreference.Any, color = Hex("FFB547") });
        Add(new WeaponDef { id = WeaponId.BloodCloak, name = "Cloak", title = "Blood Cloak",
            description = "Wraps your commander in a huge cloak: it blocks one enemy hit or lasts two turns.",
            selfCast = true, cooldown = 3, minSpeed = 10, maxSpeed = 20, gravityScale = 1f, windFactor = 0f,
            damage = 0, arc = ArcPreference.Any, color = Hex("A31631") });
        // ---------------------------------------------------------------- Alien
        Add(new WeaponDef { id = WeaponId.Ufo, name = "UFO", title = "Flying Saucer",
            description = "Hovers over the target and beams blocks — and anyone exposed — up into the saucer.",
            activateHint = "Click to HOVER here",
            cooldown = 0, minSpeed = 14, maxSpeed = 34, gravityScale = 0.5f, windFactor = 0.5f,
            damage = 10, radius = 3f, arc = ArcPreference.Any, color = Hex("7CFFB2") });
        Add(new WeaponDef { id = WeaponId.RayGun, name = "Ray Gun", title = "Ray Gun",
            description = "A straight laser bolt: no arc, no wind. Burns through up to five blocks.",
            cooldown = 1, minSpeed = 70, maxSpeed = 90, gravityScale = 0f, windFactor = 0f,
            damage = 34, pierce = 5f, arc = ArcPreference.Low, color = Hex("39FF88") });
        Add(new WeaponDef { id = WeaponId.Cow, name = "Cow", title = "Flying Cow",
            description = "Moo. A heavy cow that bounces through the fortress and flattens what it lands on.",
            cooldown = 1, minSpeed = 12, maxSpeed = 32, gravityScale = 1f, windFactor = 0.3f,
            damage = 55, arc = ArcPreference.Any, color = Hex("F4F1EA") });
        Add(new WeaponDef { id = WeaponId.SpaceWarp, name = "Space", title = "Space Warp",
            description = "Pulls the battle into space for two rounds: low gravity, and enemy throws lose a quarter of their power.",
            selfCast = true, cooldown = 4, minSpeed = 10, maxSpeed = 20, gravityScale = 1f, windFactor = 0f,
            damage = 0, arc = ArcPreference.Any, color = Hex("5B47D6") });
        // ---------------------------------------------------------------- Dali
        Add(new WeaponDef { id = WeaponId.MustacheBirds, name = "Mustache", title = "Mustache Birds",
            description = "His mustache flies off as two doodle birds that weave along the throw, peck through a block or two and burst. He is bare-lipped until it grows back.",
            cooldown = 0, minSpeed = 14, maxSpeed = 34, gravityScale = 0.55f, windFactor = 0.7f,
            damage = 30, radius = 2.1f, force = 8, pierce = 2f, arc = ArcPreference.Any, color = Hex("3A2E2A") });
        Add(new WeaponDef { id = WeaponId.Anteater, name = "Anteater", title = "Pet Anteater",
            description = "Burrows under the enemy fortress and eats one block from below at the start of each of your turns (4 turns).",
            cooldown = 3, minSpeed = 12, maxSpeed = 32, gravityScale = 1f, windFactor = 0.2f,
            damage = 0, arc = ArcPreference.Any, color = Hex("B7835A") });
        Add(new WeaponDef { id = WeaponId.MeltingClocks, name = "Clocks", title = "Melting Clocks",
            description = "Time melts: everything rewinds to before the enemy's last move, and you move again.",
            activateHint = "Click to MELT time",
            cooldown = 4, minSpeed = 12, maxSpeed = 30, gravityScale = 0.8f, windFactor = 0.5f,
            damage = 0, arc = ArcPreference.Any, color = Hex("E9C46A") });
        Add(new WeaponDef { id = WeaponId.PaintedSun, name = "Sun", title = "Painted Sun",
            description = "Bathes the battle in painted sunshine: a shaft of sunlight burns the enemy commander for 9-13 every turn (4 turns).",
            cooldown = 3, minSpeed = 12, maxSpeed = 32, gravityScale = 0.9f, windFactor = 0.6f,
            damage = 11, arc = ArcPreference.High, color = Hex("FFC93C") });
    }

    static void Add(WeaponDef d) {
        d.heroClass = (HeroClass)((int)d.id / 4);
        d.slot = (int)d.id % 4;
        all[(int)d.id] = d;
    }

    public static Color Hex(string hex) {
        Color c;
        ColorUtility.TryParseHtmlString("#" + hex, out c);
        return c;
    }
}

public class ClassDef {
    public HeroClass id;
    public string name, tagline, description;
    public Color color;
}

public static class ClassDefs {
    static readonly ClassDef[] all = {
        new ClassDef { id = HeroClass.Soldier, name = "Soldier", tagline = "Steel, powder and nerve.",
            description = "Human know-how: a grappling hook, a fuse bomb, a mouse-steered kamikaze drone and an airstrike.",
            color = WeaponDefs.Hex("E0603F") },
        new ClassDef { id = HeroClass.Wizard, name = "Wizard", tagline = "Fire, storm, stars and void.",
            description = "Area spells: burning fireballs, lightning from the sky, a meteor shower and a black hole.",
            color = WeaponDefs.Hex("9466FF") },
        new ClassDef { id = HeroClass.God, name = "God", tagline = "The Norse pantheon goes to war.",
            description = "Loki's illusions, Thor's returning hammer, Heimdall's shockwave and Odin's homing spear.",
            color = WeaponDefs.Hex("F2B630") },
        new ClassDef { id = HeroClass.Vampire, name = "Vampire", tagline = "Night falls, and so do your walls.",
            description = "Dark gifts: a bat swarm, a life-draining fang, a burning candelabra and a blood cloak that shields you.",
            color = WeaponDefs.Hex("B3203D") },
        new ClassDef { id = HeroClass.Alien, name = "Alien", tagline = "We come in pieces.",
            description = "Strange tech: a beam-up UFO, a straight ray gun, a flying cow and a warp that drags the battle into space.",
            color = WeaponDefs.Hex("39D98A") },
        new ClassDef { id = HeroClass.Dali, name = "Dalí", tagline = "The persistence of destruction.",
            description = "Surreal art: mustache birds, a block-eating anteater, melting clocks that rewind time and a painted burning sun.",
            color = WeaponDefs.Hex("E9A23B") },
    };

    public static ClassDef Get(HeroClass c) { return all[(int)c]; }
}

public class BlockMatDef {
    public BlockMaterial id;
    public string name;
    public float density;        // mass per m³
    public float hpPerVolume;    // max HP = hpPerVolume * volume (at least minHP)
    public float minHP;
    public float fragility;      // fraction of max HP lost per m/s of impact speed above ImpactThreshold
    public float pierceCost;     // how much of a piercing weapon's budget one block eats
    public float friction, bounce;
    public bool flammable, conductive, brittle;
    public Color color;          // tint used for debris particles and fallbacks
}

public static class BlockMats {
    public const float ImpactThreshold = 5.5f; // m/s of velocity change before a hit hurts (sturdy: small knocks are free)

    static readonly BlockMatDef[] all = {
        new BlockMatDef { id = BlockMaterial.Wood, name = "Wood", density = 1.3f, hpPerVolume = 48f, minHP = 22f,
            fragility = 0.035f, pierceCost = 1f, friction = 0.85f, bounce = 0.05f, flammable = true,
            color = WeaponDefs.Hex("A8743F") },
        new BlockMatDef { id = BlockMaterial.Stone, name = "Stone", density = 2.4f, hpPerVolume = 100f, minHP = 40f,
            fragility = 0.03f, pierceCost = 1.6f, friction = 0.9f, bounce = 0.02f,
            color = WeaponDefs.Hex("9A9A94") },
        new BlockMatDef { id = BlockMaterial.Metal, name = "Metal", density = 4.0f, hpPerVolume = 170f, minHP = 65f,
            fragility = 0.016f, pierceCost = 3f, friction = 0.6f, bounce = 0.05f, conductive = true,
            color = WeaponDefs.Hex("6F7C88") },
        new BlockMatDef { id = BlockMaterial.Glass, name = "Glass", density = 1.2f, hpPerVolume = 10f, minHP = 6f,
            fragility = 0.2f, pierceCost = 0.35f, friction = 0.25f, bounce = 0.1f, brittle = true,
            color = WeaponDefs.Hex("A9E4F5") },
        new BlockMatDef { id = BlockMaterial.Ice, name = "Ice", density = 0.9f, hpPerVolume = 30f, minHP = 14f,
            fragility = 0.1f, pierceCost = 0.8f, friction = 0.04f, bounce = 0.05f, brittle = true,
            color = WeaponDefs.Hex("BFEAFF") },
        new BlockMatDef { id = BlockMaterial.Explosive, name = "Powder keg", density = 0.8f, hpPerVolume = 10f, minHP = 12f,
            fragility = 0.08f, pierceCost = 0.6f, friction = 0.6f, bounce = 0.05f, flammable = true,
            color = WeaponDefs.Hex("C0392B") },
    };

    public static BlockMatDef Get(BlockMaterial m) { return all[(int)m]; }
}

// How well a bot aims. Each level gives one 0..1 skill value; everything else follows from it.
public class AIProfile {
    public float skill;          // 0 = clumsy, 1 = deadly
    public float aimError;       // degrees of random angle error (1 sigma) on its first shot
    public float powerError;     // fraction of random speed error (1 sigma) on its first shot
    public float windSense;      // 0..1: how much of the wind it allows for
    public float thinkTime;      // seconds before it starts aiming

    public static AIProfile FromSkill(float s) {
        s = Mathf.Clamp01(s);
        return new AIProfile {
            skill = s,
            aimError = Mathf.Lerp(8f, 1.5f, s),
            powerError = Mathf.Lerp(0.10f, 0.025f, s),
            windSense = Mathf.Lerp(0.2f, 0.9f, s),
            thinkTime = Mathf.Lerp(1.1f, 0.6f, s),
        };
    }
}

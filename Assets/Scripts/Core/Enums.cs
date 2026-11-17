using UnityEngine;

public enum HeroClass { Soldier, Wizard, God, Vampire, Alien, Dali, Lich, Gambler, Candidate }
public enum ControllerType { Human, Bot }
public enum GameMode { Campaign, Duel }
public enum BlockMaterial { Wood, Stone, Metal, Glass, Ice, Explosive }
public enum DamageType { Impact, Explosion, Pierce, Fire, Lightning, Crush, Magic }
public enum ArcPreference { Low, High, Any }
public enum ArenaTheme { Meadow, Desert, Frost, Volcano, Asgard }
public enum FortressStyle { Outpost, Watchtower, Keep, Bastion, Ziggurat, Citadel }
public enum BattleState { Building, Intro, TurnStart, Aiming, Resolving, Collapse, Finished }

// Fixed order: the four weapons of each class are consecutive, in HeroClass order (class * 4 + slot).
public enum WeaponId {
    GrapplingHook, Bomb, Drone, Airstrike,
    Fireball, Lightning, MeteorOrb, VoidOrb,
    LokiDaggers, Mjolnir, Gjallarhorn, Gungnir,
    BatSwarm, VampireFang, Candelabra, BloodCloak,
    Ufo, RayGun, Cow, SpaceWarp,
    MustacheBirds, Anteater, MeltingClocks, PaintedSun,
    GargoyleDive, SkeletonLegion, BoneDragon, BoneHand,
    LuckyDice, DeckOfFate, CoinToss, Roulette,
    BallotStamp, RallyShout, CampaignBus, Signature
}

public static class Classes {
    public const int Count = 9;   // number of HeroClass values
}

public static class Layers {
    public const int Default = 0, Ground = 8, Block = 9, Projectile = 10, Debris = 11, Commander = 12, Effect = 13, IconStage = 14;
    public const int GroundMask = 1 << Ground;
    public const int BlockMask = 1 << Block;
    public const int CommanderMask = 1 << Commander;
    public const int SolidMask = (1 << Ground) | (1 << Block) | (1 << Commander);   // what projectiles hit
    public const int BodiesMask = (1 << Block) | (1 << Debris) | (1 << Commander);  // what forces push
}

public static class World {
    public const float BaseGravity = 14f;    // m/s²
    public static float GravityScale { get; private set; } = 1f;   // lowered by the Alien's space warp
    public static float Gravity => BaseGravity * GravityScale;      // applied through Physics.gravity

    public static void SetGravityScale(float scale) {
        GravityScale = Mathf.Clamp(scale, 0.1f, 2f);
        Physics.gravity = new Vector3(0f, -Gravity, 0f);
    }

    public const float WaterY = -5f;         // sea surface
    public const float KillY = -7f;          // below this everything is lost
    public const float BlockDepth = 1.6f;    // z size of fortress blocks
    public const float FixedStep = 1f / 60f;
    public const float ArenaHalfWidth = 140f; // projectiles beyond |x| are lost
}

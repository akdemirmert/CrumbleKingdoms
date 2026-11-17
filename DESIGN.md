# Crumble Kingdoms — Design & Technical Spec

A turn-based physics artillery game (Unity 6.6, URP, Input System). Two or more fortresses
stand on islands across the sea; each fortress has a **Commander** and one of nine classes with
**four weapons**. Players take turns hurling weapons that hook, pierce, explode, rain meteors, call
lightning, pull blocks into a black hole, beam them up or blow towers over with a shockwave.
Fortresses are built from physical blocks (wood, stone, metal, glass, ice, powder kegs) that crack,
break into debris and topple.

Course brief (IGDPD, Mission Demolition chapter): physics, collisions, mouse interaction,
levels, game state management.

---------------------------------------------------------------------------------------------
## 1. Game rules

* **Turn order**: teams alternate (player, enemy, player, ...). Inside a team the alive
  fortresses take turns in rotation, so a 1v3 battle is player → enemy A → player → enemy B ...
* **Aiming (mouse)**: press the left button anywhere on the battlefield, drag back like a
  slingshot, release to fire. Drag direction = launch direction (reversed), drag length = power.
  A dotted preview shows the first ~1 s of the arc (gravity only — wind is the player's job).
  Right click / Esc cancels a drag. Keyboard: ←/→ angle, ↑/↓ power, Space/Enter fire, 1–4 weapon.
* **Mid-air activation**: some weapons react to a left click while flying (bomb detonates,
  drone blows up, lightning strikes, meteor/void open early, Heimdall's horn blows, the bats are
  released, the UFO stops to hover, the clocks melt time). The Soldier's drone also follows the
  mouse cursor while it flies. Blood Cloak and Space Warp are cast on yourself: one click, no aim.
* **Wind**: a new horizontal wind (acceleration, m/s²) each turn; each weapon has a wind factor.
* **Cooldowns**: slot 1 has none (except Lucky Guy, whose dice in slot 1 cool down and whose cards in slot 2 do not); other slots have 1–4 turns (counted on the owner's turns). When the selected weapon is cooling down, the first ready one is selected (bots too); a silence always leaves the first weapon that never cools down.
* **Defeat** of a fortress: only when its Commander dies (HP 0, crushed, burned, or falls into the
  sea). A ruined fortress does not lose by itself — it just leaves the commander exposed. A defeated
  fortress collapses (remaining blocks crumble).
* **Who starts**: in the campaign the player throws first; in a duel a coin toss decides, and a
  rematch lets the other side start.
* **Victory**: the last team with an undefeated fortress wins.
* **Protected commanders**: every commander stands inside a walled, roofed chamber of his
  fortress and throws from above its roof. Every intact block between a blast and a commander
  lets only 40 % of the blast through (`Explosion.Cover`), so a fortress must be opened up
  (or pierced, or struck from above) before its commander can be killed. Fortresses are built
  to survive several hits: sturdy blocks, thick lower pillars, interlocking courses.
* **Class identity**: a fortress and its commander show their class at a glance — Soldier:
  granite, iron and oak, crenellations, banners; Wizard: rune-carved slate, violet glass,
  pointed roofs, floating crystals; God: white marble and gold, columns, golden roofs. The Vampire,
  Alien, Dalí, Lich, Lucky Guy and Candidate bring their own skins, decorations and commanders (`ClassLooks`); the Lich's
  is a necropolis of rune-glowing basalt, obsidian spires, soulfire braziers and tombstones, the Lucky Guy's a
  casino of green baize, mahogany and brass with neon suit signs, marquee bulbs, giant dice and chip stacks.
  Physics is identical across skins; the team colour appears only on flags and small accents.

## 2. Classes and weapons (WeaponId order is fixed)

| Class | Slot | WeaponId | Behaviour |
|---|---|---|---|
| Soldier | 1 | GrapplingHook | A hook on a rope. It **latches onto** the first enemy block (or commander) it hits and **yanks it out** towards the thrower, tearing the wall apart; then it reels back in. |
| Soldier | 2 | Bomb | Bouncing fuse bomb (3.2 s) — **click to detonate**. Big explosion, radial force. It bounces off the ground but thuds against an enemy wall or commander (keeps 25 % of its speed, none of it backwards), so it drops at the wall's foot instead of springing back into the sea; a bot sets it off on that first hit. |
| Soldier | 3 | Drone | Kamikaze quadcopter: after the toss it **flies to the mouse cursor** (bots fly it at the commander) for a few seconds; **click to blow it up**, it also explodes on contact. |
| Soldier | 4 | Airstrike | A red smoke flare marks the spot; a **bomber** flies over and drops a row of five bombs across it. |
| Wizard | 1 | Fireball | Explodes on contact and **sets wood on fire** (burning spreads to touching wood). |
| Wizard | 2 | Lightning | Charged orb; on impact or **click** a bolt strikes from the sky down that column and **chains** to nearby metal / commanders. |
| Wizard | 3 | MeteorOrb | On impact or **click** opens a sky portal: a **meteor shower** of 7 burning rocks. |
| Wizard | 4 | VoidOrb | On impact or **click** spawns a **black hole** that pulls and crushes blocks, then bursts. |
| God | 1 | LokiDaggers | Loki throws **4 daggers** — one real + 3 illusions in a fan; each pierces 2 blocks. |
| God | 2 | Mjolnir | Thor's hammer: heavy, smashes through, calls **lightning + shockwave** on impact, then **flies back** to Thor. |
| God | 3 | Gjallarhorn | Heimdall's horn: on impact or **click** a rainbow **shockwave** throws blocks away and shatters glass/ice. |
| God | 4 | Gungnir | Odin's spear: **homes** onto the nearest enemy commander after the apex and **pierces everything**. As it never misses it hits gently: 30 to the commander and a holy burst of 14 (about 40 in all). |
| Vampire | 1 | BatSwarm | On **click** (or impact) a swarm of bats is released; they dive at enemy blocks and the commander; the bats going for the commander bite through up to 3.2 of wall (pierce budget). |
| Vampire | 2 | VampireFang | A thrown fang that **pierces** up to five blocks (pierce budget 5); a bite on a commander drains 30 % of his maximum health into the vampire. |
| Vampire | 3 | Candelabra | A spinning candelabra that **smashes through** the first two blocks it meets (they take its fire damage and wooden ones catch fire), then its three flames burst in **fire blasts** and set wood alight. |
| Vampire | 4 | BloodCloak | **Self-cast**: a cloak over the vampire's chamber absorbs every hit on him for one enemy turn (the first that hits it), or lasts two of his turns. |
| Alien | 1 | Ufo | A saucer; on **click** it stops and hovers, and a **tractor beam** (3.2 s) abducts up to five free blocks and makes the rest of the cone nearly weightless (and lifts exposed commanders). |
| Alien | 2 | RayGun | A straight laser bolt: no arc, no wind; burns through up to five blocks (pierce budget 5). |
| Alien | 3 | Cow | A heavy, bouncy flying cow that breaks through blocks and flattens what it lands on; where it comes to rest it **blows up** ("MOO-BOOM!", radius 3, 60) before a saucer beams it back up. |
| Alien | 4 | SpaceWarp | **Self-cast**: two rounds of low gravity (×0.45) and enemy throws lose a quarter of their power. |
| Dalí | 1 | MustacheBirds | His mustache splits into two flat "M" doodle birds weaving along the aimed arc; each **pecks through** up to 2 of wall (pierce budget) and then bursts in a blast. It grows back at the start of his next turn. |
| Dalí | 2 | Anteater | Burrows to the enemy fortress and **eats one bottom block** at the start of each of Dalí's turns (four meals). |
| Dalí | 3 | MeltingClocks | On **click** (or impact) time melts: the battle is **rewound** to the start of the previous turn (`BattleSnapshot`) and Dalí plays again. |
| Dalí | 4 | PaintedSun | The arena lights up in painted sunshine; at the start of each of the target's next four turns a shaft of sunlight burns its commander for 9–13 and lights a wooden block. |
| Lich | 1 | GargoyleDive | A stone gargoyle. Asleep it lands like a heavy stone; on **click** it wakes and **dives straight down**, smashing through up to two blocks (pierce budget 3.2: two stone roof layers), and bursts where it stops. The bot throws it high over the enemy commander, above his roof. |
| Lich | 2 | SkeletonLegion | A tombstone: on the enemy's island **three skeletons** rise, run to his wall, climb it and **tear out a block each**; through an open wall they strike the commander. A falling block or a blast turns a skeleton to bones. The bot throws it in front of the wall. |
| Lich | 3 | BoneDragon | A dragon skull; on impact or **click** the **bone dragon** rises there and breathes soulfire (blasts that hurt stone too) on the spot, then perches on the Lich's fortress and sweeps the nearest enemy's top with soulfire at the start of his next two turns. It has 60 HP while it perches (`TeamBody`, `IDamageable`). |
| Lich | 4 | BoneHand | A bone charm; on impact or **click** a **giant skeletal hand** bursts from the ground under it, slams down on the top (a blast that drives everything downwards), then closes and sinks back for 1.3 s, **dragging down** the enemy's blocks, commander and rubble within 2.8 m (extra downward pull, and a crushing grip of 90 per second (30 per second to a commander in it) that breaks the blocks it holds; the Lich's own side is spared). |
| Lucky Guy | 1 | LuckyDice | (The Gambler in the code: `HeroClass.Gambler`, `Gambler*` files.) Self-cast. Two dice tumble over his chamber; if **both show 3 or more** (4 in 9) a **lucky streak** (`GamblerLuck`, a TurnEffect) makes his other three weapons hit ×1.5 harder and ×1.3 wider (blast radii and pierce budgets) on his next three turns; otherwise no luck. Rolling lucky again restarts the streak, it never stacks. Bots skip the dice while a streak runs. |
| Lucky Guy | 2 | DeckOfFate | A human first **picks one of six face-down cards, one of each kind** (a number card 2–10, Jack, Queen, King, Ace, Joker, shuffled), so every kind is a 1-in-6 chance (`GamblerChoiceUI`; the aim is held until he picks, `HumanShotController.PendingChoice`); a bot draws a kind at the same odds when it throws. The drawn card pops up over his head, then: **2–10** throw that many razor cards in a tight fan (each pierces like the Vampire's fang, budget 5: 12 a block, 6 to a commander); **Jack** a jack-in-the-box that sticks where it lands, cranks for a second and blows up (radius 3.6, 85); **Queen** a heart that homes onto the enemy commander after its apex and passes through every wall (30 to him); **King** he heals 20 and throws a sword that pierces (budget 6, 35 a block, 50 to a commander); **Ace** where a small ace strikes, a giant ace drops from 22 m like a guillotine, onto the enemy commander if he is within 3.5 m of the strike, cutting edge first through every block down to him or the ground (40 a block, 35 to a commander), and bursts there (radius 3, 30); **Joker** a jester tosses six exploding bowling pins along the aim (radius 2.4, 32 each). |
| Lucky Guy | 3 | CoinToss | A human **calls heads or tails** first (a bot calls at random); the coin itself does no damage. It stops where it first touches the other side and shows how it came up: if the call comes up (even chance) a **giant coin** crashes down on that spot from 22 m (budget 6, 45 a block, 70 to a commander) and sends out a shockwave (radius 3.2, 50); if not, nothing happens. |
| Lucky Guy | 4 | Roulette | A spinning roulette wheel saws through blocks (budget 4, 30 a block, 40 to a commander); where it stops its ball runs round and drops into a pocket of the European wheel: **red** bursts in fire (radius 2.6, 45) and sets enemy wood alight, **black** the same blast with 1.6× the push, the **green zero** (1 in 37) a jackpot (radius 4.2, 110). |
| Candidate | – | (passive) | **Votes** (`CandidateVotes`, a TurnEffect for the whole battle, a big red bar under his card with a mark every tier; his supporters gather in the background beside his fortress, on the enemy side but behind the line of fire, two more every tier, jumping and clapping): every point of damage he deals (`Fortress.damageDealt`) wins votes, 12 damage a percent (a good hit on a keep is worth about a tier), up to 100 %; they never fall. Every 20 % is a tier (0–5): his skills deal ×(1 + 0.15·tier) damage and reach ×(1 + 0.06·tier) (blast radii, pierce budgets, the shove), and some do more at higher tiers. |
| Candidate | 1 | BallotStamp | A voting stamp; where it first strikes, a giant stamp (bigger with each tier) drops from 20 m straight down: a vertical piercing sweep (budget 4, 30 a block, 35 to a commander) that bursts in ink (radius 2.6, 30) and leaves a tick-in-a-circle imprint. |
| Candidate | 2 | RallyShout | Self-cast. A shout wave rolls to the nearest enemy fortress and **shoves** it (a push of force 9 in radius 6, no damage), then echoes back as a **shield** (`CandidateShield`, Commander.shield) that absorbs every hit of the first enemy turn that hits it, or lasts two of his turns (tier ≥ 3: one turn longer; tier 5: two enemy turns that hit). Bots skip it while a shield is up. |
| Candidate | 3 | CampaignBus | A bus thrown **dead straight** (gravity 0, no wind) that plows through one block (any but metal) and blows up on the next thing (radius 3; damage 40 with no votes, growing 28 % a tier, faster than his other skills, to 96 at full votes); from tier 3 it plows through two. |
| Candidate | 4 | Signature | Self-cast. A giant pen signs a decree in the sky; its seal **silences** every enemy: weapons 2–4 locked for his next turn (cooldowns, so the HUD and bots follow; two turns from tier 4), shown as a "Silenced" chip; from tier 1 the ink also burns each enemy commander for 3.5 per tier (at most 17.5). |

Numbers live in `WeaponDefs` (Scripts/Core/Defs.cs) — the single source of truth.

## 3. Campaign (10 levels)

Bot accuracy rises with the level and **inside a battle** (the bot learns from its last miss).
Levels 1–3 are 1v1, 4–7 are 1v2 (the second enemy fortress stands on a higher cliff behind the
first — "stacked"), 8–10 are 1v3 (the maximum). Themes: Meadow, Desert, Frost, Volcano, Asgard.
Stars: 1–3 by the average of the commander's health and the fortress's integrity at the end
(70 % or more = 3, 40 % or more = 2, otherwise 1). Progress is saved (PlayerPrefs).

**Duel (1v1)**: Player 1 vs Player 2 (hot-seat) or vs Bot (Easy/Normal/Hard/Brutal), any classes,
any arena, day or night, wind on/off.

---------------------------------------------------------------------------------------------
## 4. World conventions

* Gameplay plane z = 0. +x = right. Player / Player 1 on the left, facing +1.
* `World.Gravity` = 14 m/s² (Physics.gravity set at boot). Fixed step 1/60 s.
* Island tops are at y = 0 or higher; sea surface `World.WaterY` = -5; anything below
  `World.KillY` = -7 is lost (blocks count as destroyed, commanders die, projectiles end).
* Blocks: depth (z) `World.BlockDepth` = 1.6 m. Rigidbodies of blocks and commanders are
  constrained to the plane (freeze position Z, rotation X and Y). Debris is free 3D.
* Layers (`Layers` class): Ground 8, Block 9, Projectile 10, Debris 11, Commander 12, Effect 13,
  IconStage 14. Projectile↔Projectile, Projectile↔Debris, Debris↔Debris, Debris↔Commander and
  everything↔Effect do **not** collide (set at boot).
* Projectiles never collide with their own team's blocks and commanders.

## 5. Code map (Assets/Scripts)

| Folder | Files |
|---|---|
| Core | Enums, Defs (WeaponDefs, ClassDefs), LevelDefs (+BattleSetup), GameSession, SaveData, GameSettings, GameAssets, Boot |
| Battle | BattleManager, Team, Fortress, Commander, Block, Debris, TurnActor, Damage (DamageInfo, IDamageable), Explosion, Ballistics, HumanShotController, BotShotController, BattleCamera, TimeFX |
| Weapons | Projectile (base), PiercingProjectile (shared pierce), WeaponFactory; files per class |
| FX | VFX (particles, flashes, rings, floating text), LightningBolt, ScreenFlash |
| Audio | SFX (SoundId, synthesis, pooled playback), Music |
| World | ArenaBuilder (terrain, sky, sea, background per theme), FortressGenerator (blueprints), CommanderModel, ClassSkins, ClassLooks + Vampire/Alien/Dalí/Lich/Gambler/Candidate looks |
| UI | UIKit (helpers), MainMenuUI (a galaxy of planets behind the pages: SpaceBackdrop, SpacePlanet, SpaceScenery; warp page changes; starting a battle dives into its planet), BattleHUD (+ HudFX, ClickFX polish), GamblerChoiceUI, IconRenderer |
| Util | Mats (runtime material cache), MeshKit (boxes with metre UVs, rocks, cones), ModelKit (primitive parts), TexKit (procedural textures) |
| Debug | AutoPlaytest (`-autotest <dir>` screenshots) |
| Editor | SiegeBuilder (project settings, fonts, base materials, GameAssets, scenes, Windows build), PhysicsProbe (fortress stability check) |

### Rules for all code
* C# 9, Unity 6000.6 API: `Rigidbody.linearVelocity`, `linearDamping`, `PhysicsMaterial`,
  `FindAnyObjectByType`, `FindObjectsByType<T>()`, `GetEntityId()`, TMP `textWrappingMode`.
* Input **only** through `UnityEngine.InputSystem` (`Mouse.current`, `Keyboard.current`);
  the legacy `Input` class throws in this project.
* **Never `Shader.Find` at runtime.** Materials come from `Mats` (copies of base materials in
  `GameAssets`), textures from `TexKit`, meshes from `MeshKit` / `ModelKit`.
* Visual-only parts must have **no colliders** (use `ModelKit.Part`, which strips them).
* Anything that must finish before the turn ends adds a `TurnActor` component and removes it
  (or destroys its GameObject) when done. The turn also has a hard timeout.
* No namespaces (textbook style). Keep classes small; no speculative abstractions.

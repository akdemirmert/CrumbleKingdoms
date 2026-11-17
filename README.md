# Crumble Kingdoms

**SENG 216 – Introduction to Game Design and Development · Group 5**

Mert AKDEMİR · Mehmet BELEN · Cem ÇİFTÇİ · Ahmet Buğra KARA · Derviş Can TAM

A turn-based physics artillery game made in Unity 6.6 for the physics-game chapter
(physics, collisions, mouse interaction, levels, game state management), inspired by
catapult and artillery games such as *Crossbows and Catapults*, *Worms* and *Scorched Earth*.

Two or more fortresses stand on islands across the sea. Each fortress has a **Commander** and one of
**nine classes with four weapons each**. Fortresses are built from physical blocks (wood, stone, metal,
glass, ice, powder kegs) that crack, break into debris and topple.

---

## Running the game

Unzip the package first. The unzipped `GROUP05_CrumbleKingdoms` folder **is the Unity project**
(`Assets`, `Packages`, `ProjectSettings`); it also holds the Windows build (`Windows Build/`) and
`Play Crumble Kingdoms.bat`.

* **Windows build:** run `Windows Build/CrumbleKingdoms.exe`, or double-click `Play Crumble Kingdoms.bat`.
* **Unity project:** in Unity Hub choose **Add ▸ Add project from disk**, select the unzipped
  `GROUP05_CrumbleKingdoms` folder and open it with **Unity 6000.6.9f1** (Unity 6.6, URP). The first open
  takes a few minutes while Unity downloads the packages and rebuilds its Library folder. The editor then
  opens the main menu scene: press **Play** to play the game inside Unity.
  All the game code is in `Assets/Scripts` (editor tools in `Assets/Editor`).
  Scenes: `Assets/Scenes/MainMenu.unity` (start here) and `Assets/Scenes/Battle.unity`.
  The menu **Crumble Kingdoms ▸ 1. Set up project / 2. Build assets / 3. Build scenes / 4. Build Windows player**
  regenerates everything from code (the player goes to `Builds/Windows/CrumbleKingdoms.exe` inside the project);
  it is not needed to play, the scenes are already built.

## How to play

| Action | Mouse | Keyboard |
|---|---|---|
| Aim and fire | press the left button anywhere, **drag back** like a slingshot, release | ←/→ angle, ↑/↓ power (Shift = faster), then Space / Enter to fire |
| Cancel a drag | right click while dragging | Esc while dragging |
| Choose a weapon | click a slot in the weapon bar | 1 – 4 |
| Special power while the shot flies (bomb, drone, lightning, meteor, void, horn, bats, UFO, clocks, gargoyle, dragon skull, bone charm) | left click | Space |
| Steer the Soldier's drone | move the mouse | |
| Use a self-cast weapon (Blood Cloak, Space Warp, Loaded Dice, Rally Shout, Signature) – no aiming | left click | Space / Enter |
| Lucky Guy: pick a face-down card (Deck of Fate) or call heads or tails (Heads or Tails) before aiming | click a card / a coin | Q W E R T Y (cards), H / T (coin) |
| Zoom / pan the camera | mouse wheel / right or middle drag | |
| Pause | pause button (bottom left) | Esc |

* Teams take turns. The dotted line shows only the start of the arc and ignores the **wind**
  (shown at the top of the screen, new every turn).
* Weapons 2–4 have **cooldowns** (turns until they can be used again).
* Some weapons keep working for several turns (cloak, space warp, anteater, painted sun, lucky streak, shield, silence, votes); a small
  chip on the fortress card shows each one and how many turns it has left.
* A fortress is **defeated** only when its commander dies (damage, crushed by rubble, thrown into the
  sea); a ruined fortress just leaves him exposed. The last team standing wins.
* Duels start with a coin toss for the first throw; a rematch lets the other side begin.
* The **Candidate** wins **votes** with every point of damage he deals (the red bar under his card; his supporters gather
  and cheer beside his fortress, in the background, as they grow). Every 20 % of the vote
  makes all four of his skills stronger (more damage, wider reach; the shield lasts longer, the silence two turns).

## Modes

* **Campaign – 10 levels.** Pick a class, then beat levels in order. The enemy bot aims better
  with every level *and* with every shot inside a battle. Levels 1–3 are 1 v 1, levels 4–7 are
  1 v 2 (a second fortress stands higher, behind the first), levels 8–10 are 1 v 3 (the maximum).
  1–3 stars per level depending on how healthy your commander and your fortress are at the end (their
  average: 70 % or more = 3 stars, 40 % or more = 2, otherwise 1); progress is saved.
* **Duel (1 v 1).** Player vs Player on one computer (hot-seat) or Player vs Bot
  (Easy / Normal / Hard / Brutal), any classes, any arena, day or night, wind on/off.

## Classes and weapons

| Class | 1 | 2 | 3 | 4 |
|---|---|---|---|---|
| **Soldier** | **Grappling Hook** – latches onto a block (or the commander) and yanks it out of the wall | **Fuse Bomb** – bouncing bomb, click to detonate | **Kamikaze Drone** – follows your mouse in flight, click to blow it up | **Airstrike** – a smoke flare calls in a bomber that drops five bombs across it |
| **Wizard** | **Fireball** – explodes and sets wood on fire; fire spreads | **Lightning Orb** – on impact or click a bolt strikes down that column and chains to metal blocks and enemy commanders | **Meteor Orb** – opens a sky portal, seven meteors rain down | **Void Orb** – a black hole pulls the fortress in, then bursts |
| **God** | **Loki – Trickster Daggers** – four daggers, one real and three illusions | **Thor – Mjolnir** – smashes through, calls lightning, flies back to Thor | **Heimdall – Gjallarhorn** – rainbow shockwave hurls blocks away | **Odin – Gungnir** – homes onto a commander and pierces everything |
| **Vampire** | **Bat Swarm** – click to release bats that dive at blocks and the commander; the ones going for the commander bite through his walls | **Vampire Fang** – pierces up to five blocks (fewer stone or metal); a bite on the commander drains 30 % of his maximum health into yours | **Burning Candelabra** – smashes through two blocks (wood catches fire), then its three flames burst in fire blasts and light wood | **Blood Cloak** – absorbs every hit on your commander for one enemy turn (the first that hits it), or lasts two turns |
| **Alien** | **UFO (Flying Saucer)** – click to hover; a strong tractor beam lifts up to five blocks (and exposed commanders) | **Ray Gun** – straight laser, no arc or wind, burns through up to five blocks (fewer stone or metal) | **Flying Cow** – a heavy cow that bounces through the fortress and blows up where it comes to rest | **Space Warp** – two rounds of low gravity; enemy throws lose a quarter of their power |
| **Dalí** | **Mustache Birds** – his mustache splits into two doodle birds that weave along the aimed arc, peck through a block or two and burst (it grows back at the start of his next turn) | **Pet Anteater** – burrows under the enemy fortress and eats one block per turn for four turns | **Melting Clocks** – click to melt time: the enemy's last move is undone and Dalí plays again | **Painted Sun** – the arena lights up in painted sunshine and the enemy commander burns for 9–13 every turn for four turns |
| **Lich** | **Gargoyle Dive** – a stone gargoyle; click and it wakes and dives straight down, smashing through up to two roof blocks before it bursts | **Skeleton Legion** – a tombstone on the enemy's island raises three skeletons that climb his wall and tear out a block each (through an open wall they strike the commander) | **Bone Dragon** – a dragon skull; the bone dragon rises where it lands and breathes soulfire (it burns stone too), then perches on your fortress and sweeps the enemy again on your next two turns (enemies can shoot it down) | **Bone Hand** – a bone charm; where it lands (or on a click) a giant skeletal hand bursts from the ground, slams down on the top and crushes the enemy blocks in its grip, dragging them down into the ground |
| **Lucky Guy** | **Loaded Dice** – roll two dice: if both show 3 or more, his other three weapons hit 50 % harder and 30 % wider for his next three turns; if not, no luck | **Deck of Fate** – pick one of six face-down cards, one of each kind (a number, Jack, Queen, King, Ace and Joker), then throw it. 2–10: that many razor cards fan out, each cutting through a block. Jack: a jack-in-the-box cranks its tune and blows up. Queen: a heart that seeks the enemy commander through every wall. King: a sword that pierces three or four blocks, and he heals 20. Ace: a giant ace drops from the sky like a guillotine and bursts. Joker: a jester hurls six exploding bowling pins | **Heads or Tails** – call heads or tails and flip the coin at the enemy: if the call comes up, a giant coin crashes down where it landed, cutting through the roof, and sends out a shockwave; if not, nothing happens | **Roulette Wheel** – a spinning wheel saws through up to four blocks, then its ball drops into a pocket: red bursts in fire and sets wood alight, black hits harder, the green zero is a huge jackpot blast |
| **Candidate** | **Ballot Stamp** – a voting stamp: where it lands a giant stamp comes down from the sky, cuts through the roof and bursts in ink | **Rally Shout** – he shouts at the nearest enemy: the wave shoves his fortress about (no damage) and echoes back as a shield that blocks one enemy turn's hits or lasts two turns | **Campaign Bus** – a bus thrown dead straight (no arc, no wind) that rams through a block and blows up behind it | **Signature** – a giant pen signs a decree in the air: every enemy is silenced (only his first weapon; Lucky Guy keeps his cards) for his next turn |

## Where the course topics are

* **Physics:** rigidbodies with masses from material density, physics materials (friction,
  bounce), explosions with distance falloff, radial pull (black hole) and push (shockwave),
  a rope pull (grappling hook), piercing by swept sphere casts, debris pieces, fire spreading,
  a sea that sinks whatever falls in (lost blocks, drowned commanders) and puts out fire,
  a tractor beam (UFO), changing world gravity (Space Warp), straight gravity-free shots (Ray Gun).
* **Collisions:** block damage from the collision impulse (Δv above a threshold × fragility),
  continuous collision for fast projectiles, a layer collision matrix, projectiles ignore their
  own team, commanders crushed by falling blocks.
* **Mouse interaction:** slingshot drag aiming with a trajectory preview, mid-air click powers,
  a drone steered with the mouse, camera zoom/pan, the whole UI.
* **Levels:** `LevelDefs` (10 levels: themes, islands, fortress styles, materials, wind, bot skill),
  procedural fortresses (`FortressGenerator`), 1 v 1 → 1 v 3.
* **Game state management:** `GameSession` (menu → battle data), `BattleManager` state machine
  (intro → turn start → aiming → resolving → collapse → finished), cooldowns, multi-turn effects
  (`TurnEffect`), a full battle snapshot for the time rewind (`BattleSnapshot`), pause, `SaveData`
  (unlocked levels, stars, settings).

## Code map (`Assets/Scripts`)

* `Core` – enums, weapon, class and block-material numbers (`Defs`), levels, session, saving, boot (physics settings).
* `Battle` – turn loop, fortresses, blocks, debris, commanders, explosions, ballistics, player and bot aiming, camera,
  multi-turn effects and time-rewind snapshots.
* `Weapons` – projectile base class, shared piercing flight, weapon registry, files per class.
* `World` – arena (islands, sea, sky, lighting per theme), fortress generator, commander models, class looks
  (`ClassSkins` for the Soldier, Wizard and God; `ClassLooks` + the Vampire, Alien, Dalí, Lich, Lucky Guy (`Gambler*`) and Candidate look files).
* `FX`, `Audio` – particle effects; procedurally synthesised sounds and music.
* `UI` – menus (a galaxy of planets behind them, `SpaceBackdrop`; starting a battle dives into its planet), HUD, icons.
* `Util` – procedural materials, meshes, models and textures (`Mats`, `MeshKit`, `ModelKit`, `TexKit`).
* `Editor/SiegeBuilder.cs` – builds settings, fonts, materials, scenes and the player from code;
  `Editor/PhysicsProbe.cs` – a fortress stability check run in batch mode;
  `Editor/OpenMainMenuOnStart.cs` – opens the main menu scene when the project is opened.
* `Debug/AutoPlaytest.cs` – `CrumbleKingdoms.exe -autotest <folder>` plays itself and saves screenshots (testing aid).

## Credits

All models, textures, sounds and music are generated by code at runtime. Font: **Fredoka**
(SIL Open Font License, `Assets/Fonts/OFL.txt`). Made with Unity 6.6 and the Universal Render Pipeline.

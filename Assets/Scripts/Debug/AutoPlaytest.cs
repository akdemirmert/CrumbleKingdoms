using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Automated playtest (development aid, not part of normal play). Start the player with
//   CrumbleKingdoms.exe -autotest <folder>
// It walks through the menu panels, fires every weapon in bot-vs-bot duels, plays one player turn
// through the real aiming code, opens 1v2/1v3 campaign levels, saves screenshots and writes
// report.txt (errors, exceptions, frame rate) into the folder, then quits.
public class AutoPlaytest : MonoBehaviour {
    static string dir;

    int errors, warnings;
    readonly List<string> errorLines = new List<string>();
    readonly List<string> notes = new List<string>();
    readonly Dictionary<Fortress, int> turnsTaken = new Dictionary<Fortress, int>();
    bool impactSeen;
    bool forceSlots;
    int benchSlot = -1;                     // slot the bots must use during the benchmark (-1 = their choice)
    BattleManager hooked;
    int lastCaptured;                       // turn number of the last captured turn
    string lastLabel;                       // that turn's shooter and weapon
    readonly Dictionary<Fortress, float> lastIntegrity = new Dictionary<Fortress, float>(), lastHP = new Dictionary<Fortress, float>();
    readonly Dictionary<Fortress, int[]> losses = new Dictionary<Fortress, int[]>();   // blocks broken, sunk, vanished
    readonly HashSet<Block> watched = new HashSet<Block>();
    float fpsWindow, worstFps = 999f, fpsSum;
    int fpsFrames, fpsSamples;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-autotest") dir = args[i + 1];
        if (dir == null || FindAnyObjectByType<AutoPlaytest>() != null) return;
        Directory.CreateDirectory(dir);
        GameObject go = new GameObject("[AutoPlaytest]");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoPlaytest>();
    }

    void Awake() {
        Application.runInBackground = true;
        SaveData.Disabled = true;
        Application.logMessageReceived += OnLog;
        Projectile.AnyImpact += OnImpact;
    }

    void OnDestroy() {
        Application.logMessageReceived -= OnLog;
        Projectile.AnyImpact -= OnImpact;
        if (hooked != null) hooked.TurnStarted -= OnTurnStarted;
    }

    void OnLog(string msg, string stack, LogType type) {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) {
            errors++;
            if (errorLines.Count < 60) {
                string s = stack ?? "";
                int cut = s.IndexOf('\n', Math.Min(s.Length, 1));
                errorLines.Add(type + ": " + msg + (s.Length > 0 ? "\n    at " + s.Substring(0, Math.Min(s.Length, 400)).Replace("\n", "\n    ") : ""));
            }
        } else if (type == LogType.Warning) warnings++;
    }

    void OnImpact(Projectile p, Vector3 point) { impactSeen = true; }

    void Update() {
        if (SceneManager.GetActiveScene().name != GameSession.BattleScene) return;
        fpsWindow += Time.unscaledDeltaTime;
        fpsFrames++;
        if (fpsWindow >= 0.5f) {
            float fps = fpsFrames / fpsWindow;
            worstFps = Mathf.Min(worstFps, fps);
            fpsSum += fps;
            fpsSamples++;
            fpsWindow = 0f;
            fpsFrames = 0;
        }
    }

    IEnumerator Start() {
        // -quick: four duels (classes 0v1, 2v3, 4v5, 6v7) fire every weapon once; skips the rest.
        // -only N: just the weapon duel of class N against class N+1.
        // -bench T [-classes 3,5]: weapon damage benchmark (see Bench), T trials per weapon.
        // -duels N [-classes 3,5]: full bot duels of those classes against every other class (see DuelSeries).
        string[] args = Environment.GetCommandLineArgs();
        int bench = Array.IndexOf(args, "-bench"), duels = Array.IndexOf(args, "-duels");
        StartCoroutine(Watchdog(Time.realtimeSinceStartup + (bench >= 0 || duels >= 0 ? 7200f : 1500f)));
        TimeFX.Speed = 1.4f;
        if (bench >= 0 || duels >= 0) {
            int at = bench >= 0 ? bench : duels;
            int count = at + 1 < args.Length && int.TryParse(args[at + 1], out int t0) ? Mathf.Clamp(t0, 1, 10) : 3;
            List<int> classes = new List<int>();
            int ci = Array.IndexOf(args, "-classes");
            if (ci >= 0 && ci + 1 < args.Length)
                foreach (string s in args[ci + 1].Split(',')) if (int.TryParse(s, out int c1) && c1 >= 0 && c1 < Classes.Count) classes.Add(c1);
            if (classes.Count == 0) for (int c = 0; c < Classes.Count; c++) classes.Add(c);
            yield return new WaitForSecondsRealtime(2f);
            yield return bench >= 0 ? Bench(count, classes) : DuelSeries(count, classes);
            Finish("done");
            yield break;
        }
        bool quick = Array.IndexOf(args, "-quick") >= 0;
        int only = Array.IndexOf(args, "-only");
        if (only >= 0 && only + 1 < args.Length && int.TryParse(args[only + 1], out int c0)) {
            yield return new WaitForSecondsRealtime(2f);
            yield return WeaponDuel(Mathf.Clamp(c0, 0, Classes.Count - 1));
            Finish("done");
            yield break;
        }
        yield return Menus();
        for (int c = 0; c < Classes.Count; c += quick ? 2 : 1) yield return WeaponDuel(c);
        if (!quick) {
            yield return HumanTurn();
            yield return FullDuel();
            foreach (int level in new[] { 3, 7, 9 }) yield return CampaignLevel(level);
        }
        Finish("done");
    }

    // ------------------------------------------------------------------ scenarios
    IEnumerator Menus() {
        yield return new WaitForSecondsRealtime(3f);
        yield return Shot("m1_title");
        MainMenuUI ui = FindAnyObjectByType<MainMenuUI>();
        if (ui == null) { notes.Add("no MainMenuUI in the menu scene"); yield break; }
        string[] panels = { "class", "levels", "duel", "howto", "settings" };
        for (int i = 0; i < panels.Length; i++) {
            ui.Show(panels[i]);
            yield return new WaitForSecondsRealtime(1.3f);
            yield return Shot("m" + (i + 2) + "_" + panels[i]);
        }
        ui.Show("title");
    }

    // Bot vs bot duel where each side uses slot 1, 2, 3, 4 in order, so every weapon fires.
    IEnumerator WeaponDuel(int c) {
        GameSession.AutoPlay = true;
        GameSession.Duel = new DuelOptions {
            p1Class = (HeroClass)c, p2Class = (HeroClass)((c + 1) % Classes.Count), p2Controller = ControllerType.Bot, botDifficulty = 2,
            theme = (ArenaTheme)((c * 2 + 1) % 5), night = c == 2, wind = c != 0, seed = 11 + c,
        };
        forceSlots = true;
        yield return LoadBattle(GameSession.StartDuel);
        BattleManager bm = BattleManager.I;
        yield return new WaitForSecondsRealtime(1.2f);
        yield return Shot("w" + c + "_0_intro");
        for (int t = 0; t < 8 && Alive(bm); t++) yield return CaptureTurn(bm, "w" + c);
        if (Alive(bm)) NoteChanges(bm, "w" + c);
        forceSlots = false;
        GameSession.ForcedSlot = -1;
    }

    // One turn played through HumanShotController (aim computed with the real ballistics).
    IEnumerator HumanTurn() {
        GameSession.AutoPlay = false;
        GameSession.PlayerClass = HeroClass.Soldier;
        yield return LoadBattle(() => GameSession.StartCampaignLevel(0));
        BattleManager bm = BattleManager.I;
        yield return WaitFor(() => !Alive(bm) || (bm.State == BattleState.Aiming && bm.Human.Active != null), 30f);
        if (!Alive(bm) || bm.Human.Active == null) { notes.Add("human turn never started"); GameSession.AutoPlay = true; yield break; }
        Fortress me = bm.Human.Active;
        bm.Human.SelectSlot(2);
        Fortress enemy = bm.EnemiesOf(me)[0];
        WeaponDef def = me.SelectedWeapon;
        Ballistics.Solve(me.LaunchOrigin, enemy.commander.AimPoint, def, 0f, def.arc == ArcPreference.High, out Vector3 v);
        bm.Human.SimulateAim(Ballistics.AngleOf(v, me.Facing), Mathf.InverseLerp(def.minSpeed, def.maxSpeed, v.magnitude));
        yield return new WaitForSecondsRealtime(0.8f);
        yield return Shot("h1_player_aim");
        bm.Human.SimulateFire();
        yield return new WaitForSeconds(0.8f);
        yield return Shot("h2_player_shot_flying");
        yield return WaitFor(() => !Alive(bm) || bm.State != BattleState.Resolving, 25f);
        yield return Shot("h3_after_player_shot");
        GameSession.AutoPlay = true;    // the bot takes over the player's side from here
        yield return CaptureTurn(bm, "h");
    }

    // A whole bot-vs-bot duel to the end, for the result screen and balance (turn count).
    IEnumerator FullDuel() {
        GameSession.AutoPlay = true;
        GameSession.Duel = new DuelOptions {
            p1Class = HeroClass.God, p2Class = HeroClass.Soldier, p2Controller = ControllerType.Bot, botDifficulty = 2,
            theme = ArenaTheme.Asgard, night = false, wind = true, seed = 5,
        };
        yield return LoadBattle(GameSession.StartDuel);
        BattleManager bm = BattleManager.I;
        float start = Time.realtimeSinceStartup;
        int lastShotTurn = 0;
        while (Alive(bm) && bm.State != BattleState.Finished && Time.realtimeSinceStartup - start < 300f) {
            if (bm.Turn >= lastShotTurn + 4 && bm.State == BattleState.Resolving) {
                lastShotTurn = bm.Turn;
                yield return new WaitForSeconds(0.8f);
                yield return Shot("f_turn" + bm.Turn.ToString("00"));
            }
            yield return null;
        }
        if (Alive(bm)) {
            notes.Add("full duel: " + (bm.State == BattleState.Finished ? "finished" : "NOT finished") + " after " + bm.Turn + " turns, winner team " +
                      (bm.Winner != null ? bm.Winner.id.ToString() : "-") + ", " + (Time.realtimeSinceStartup - start).ToString("0") + " s");
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("f_result");
        }
    }

    IEnumerator CampaignLevel(int level) {
        GameSession.AutoPlay = true;
        GameSession.PlayerClass = (HeroClass)(level % Classes.Count);
        yield return LoadBattle(() => GameSession.StartCampaignLevel(level));
        BattleManager bm = BattleManager.I;
        yield return new WaitForSecondsRealtime(0.9f);
        yield return Shot("c" + (level + 1).ToString("00") + "_0_intro");
        yield return WaitFor(() => !Alive(bm) || bm.State == BattleState.Aiming, 20f);
        yield return new WaitForSeconds(0.4f);
        yield return Shot("c" + (level + 1).ToString("00") + "_1_overview");
        for (int t = 0; t < 2 && Alive(bm); t++) yield return CaptureTurn(bm, "c" + (level + 1).ToString("00"));
    }

    // Weapon damage benchmark: every aimed weapon of the given classes (not self-casts or the clocks) is thrown once,
    // with a perfect throw and no wind, at a fresh Soldier keep, `trials` times on different layouts (the same layouts
    // for every weapon). Notes the enemy commander's damage, the keep's integrity loss and the blocks it lost, measured
    // when the target's turn starts, so the first flare of a painted sun counts (the anteater eats on Dalí's own turns,
    // so it shows nothing here, and the bone dragon's later passes are missed). A kill also counts the defeated keep's
    // collapse in its integrity and blocks. The Gambler's chance weapons are benched per outcome (BenchVariants).
    IEnumerator Bench(int trials, List<int> classes) {
        foreach (int c in classes) {
            for (int slot = 0; slot < 4; slot++) {
                WeaponDef def = WeaponDefs.ForClass((HeroClass)c, slot);
                if (def.selfCast || def.id == WeaponId.MeltingClocks) continue;
                foreach (var variant in BenchVariants(def)) {
                    GamblerHand.ForcedCard = variant.card;
                    GamblerHand.ForcedFlip = variant.flip;
                    yield return BenchWeapon(c, slot, def, variant.label, trials);
                }
            }
        }
        GamblerHand.ForcedCard = -1;
        GamblerHand.ForcedFlip = -1;
        benchSlot = -1;
        TimeFX.Speed = 1.4f;
    }

    IEnumerator BenchWeapon(int c, int slot, WeaponDef def, string label, int trials) {
        AIProfile perfect = new AIProfile { skill = 1f, aimError = 0f, powerError = 0f, windSense = 1f, thinkTime = 0.2f };
        float hpSum = 0f, integritySum = 0f;
        int lostSum = 0, kills = 0, runs = 0;
        List<string> each = new List<string>();
        for (int t = 0; t < trials; t++) {
            benchSlot = slot;
            GameSession.AutoPlay = true;
            GameSession.Mode = GameMode.Duel;
            GameSession.Duel = new DuelOptions {
                p1Class = (HeroClass)c, p2Class = HeroClass.Soldier, p2Controller = ControllerType.Bot, botDifficulty = 2,
                theme = ArenaTheme.Meadow, night = false, wind = false, seed = 200 + t, firstTeam = 0,
            };
            yield return LoadBattle(() => SceneFader.Load(GameSession.BattleScene));
            BattleManager bm = BattleManager.I;
            if (!Alive(bm) || bm.fortresses.Count < 2) { each.Add("no battle"); continue; }
            Fortress me = bm.fortresses[0], enemy = bm.fortresses[1];
            me.setup.ai = perfect;   // read when its first turn starts, after the intro
            TimeFX.Speed = 2.5f;
            yield return WaitFor(() => !Alive(bm) || (bm.State == BattleState.Aiming && bm.Current == me), 30f);
            if (!Alive(bm) || bm.Current != me) { each.Add("no turn"); continue; }
            float hp0 = enemy.commander.hp, integrity0 = enemy.Integrity;
            losses.Remove(enemy);
            yield return WaitFor(() => !Alive(bm) || bm.State == BattleState.Finished || (bm.State == BattleState.Aiming && bm.Current == enemy), 60f);
            if (!Alive(bm)) { each.Add("lost battle"); continue; }
            float hp = hp0 - (enemy.CommanderAlive ? enemy.commander.hp : 0f), integrity = (integrity0 - enemy.Integrity) * 100f;
            losses.TryGetValue(enemy, out int[] n);
            int lost = n != null ? n[0] + n[1] + n[2] : 0;
            runs++;
            hpSum += hp;
            integritySum += integrity;
            lostSum += lost;
            if (!enemy.CommanderAlive) kills++;
            each.Add(Mathf.RoundToInt(hp) + "/" + Mathf.RoundToInt(integrity) + "%/" + lost);
        }
        int k = Mathf.Max(1, runs);
        notes.Add("BENCH " + ((HeroClass)c).ToString().PadRight(8) + (def.id + (label.Length > 0 ? " " + label : "")).PadRight(18) + " cd" + def.cooldown +
                  "  commander -" + (hpSum / k).ToString("0").PadLeft(3) + "  integrity -" + (integritySum / k).ToString("0").PadLeft(2) +
                  "%  blocks " + ((float)lostSum / k).ToString("0.0").PadLeft(4) + "  kills " + kills + "/" + runs +
                  "   [" + string.Join("  ", each) + "]");
    }

    // What one weapon is benched as: the Gambler's Deck of Fate once per kind of card (deck index: 0 = Two, 16 = Six, 32 = Ten,
    // 36 Jack, 40 Queen, 44 King, 48 Ace, 52 Joker), his coin as a won call (a lost one does nothing); anything else once.
    static List<(string label, int card, int flip)> BenchVariants(WeaponDef def) {
        if (def.id == WeaponId.DeckOfFate)
            return new List<(string, int, int)> { ("2", 0, -1), ("6", 16, -1), ("10", 32, -1), ("J", 36, -1), ("Q", 40, -1), ("K", 44, -1), ("A", 48, -1), ("Joker", 52, -1) };
        if (def.id == WeaponId.CoinToss) return new List<(string, int, int)> { ("won", -1, 1) };
        return new List<(string, int, int)> { ("", -1, -1) };
    }

    // Full bot-vs-bot duels (both sides the same Hard bot, wind on, the arena changing) of each given class against
    // every other class, `games` per pairing with the sides and the first throw alternating. Notes the wins per pairing;
    // a duel still going after 40 turns counts as a draw.
    IEnumerator DuelSeries(int games, List<int> classes) {
        foreach (int c in classes) {
            int won = 0, played = 0;
            List<string> pairs = new List<string>();
            for (int o = 0; o < Classes.Count; o++) {
                if (o == c) continue;
                int w = 0, l = 0, d = 0;
                for (int g = 0; g < games; g++) {
                    bool left = g % 2 == 0;   // which side this class plays
                    GameSession.AutoPlay = true;
                    GameSession.Mode = GameMode.Duel;
                    GameSession.Duel = new DuelOptions {
                        p1Class = (HeroClass)(left ? c : o), p2Class = (HeroClass)(left ? o : c), p2Controller = ControllerType.Bot, botDifficulty = 2,
                        theme = (ArenaTheme)((c + o + g) % 5), night = false, wind = true, seed = 300 + 7 * g + o, firstTeam = (g / 2) % 2,
                    };
                    yield return LoadBattle(() => SceneFader.Load(GameSession.BattleScene));
                    BattleManager bm = BattleManager.I;
                    if (!Alive(bm)) continue;
                    foreach (Fortress f in bm.fortresses) f.setup.ai = AIProfile.FromSkill(0.65f);
                    TimeFX.Speed = 2.5f;
                    yield return WaitFor(() => !Alive(bm) || bm.State == BattleState.Finished || bm.Turn > 40, 600f);
                    if (!Alive(bm)) continue;
                    if (bm.State != BattleState.Finished || bm.Winner == null) d++;
                    else if (bm.Winner.id == (left ? 0 : 1)) w++;
                    else l++;
                }
                won += w;
                played += w + l + d;
                pairs.Add(((HeroClass)o) + " " + w + "-" + l + (d > 0 ? " (" + d + " draw)" : ""));
                notes.Add("  duels " + ((HeroClass)c) + " vs " + pairs[pairs.Count - 1]);   // kept if the watchdog stops the series
            }
            notes.Add("DUELS " + ((HeroClass)c).ToString().PadRight(8) + " won " + won + "/" + played + "   " + string.Join(", ", pairs));
        }
        TimeFX.Speed = 1.4f;
    }

    // ------------------------------------------------------------------ helpers
    IEnumerator CaptureTurn(BattleManager bm, string tag) {
        yield return WaitFor(() => !Alive(bm) || bm.State == BattleState.Finished || (bm.State == BattleState.Aiming && bm.Turn > lastCaptured), 30f);
        if (!Alive(bm) || bm.State == BattleState.Finished) yield break;
        lastCaptured = bm.Turn;
        NoteChanges(bm, tag);
        string name = tag + "_t" + bm.Turn.ToString("00") + "_" + (bm.Current != null ? bm.Current.HeroClass.ToString() : "?");
        yield return new WaitForSeconds(1.15f);
        yield return Shot(name + "_a_aim");
        impactSeen = false;
        yield return WaitFor(() => !Alive(bm) || bm.State != BattleState.Aiming, 20f);
        if (!Alive(bm)) yield break;
        string weapon = bm.LastShot != null && bm.Current != null ? bm.Current.Weapon(bm.LastShot.slot).id.ToString() : "unknown";
        lastLabel = "t" + bm.Turn.ToString("00") + " " + (bm.Current != null ? bm.Current.HeroClass.ToString() : "?") + " " + weapon;
        yield return new WaitForSeconds(0.4f);
        if (Alive(bm) && !impactSeen && bm.State == BattleState.Resolving) yield return Shot(name + "_b0_" + weapon + "_flight");
        yield return WaitFor(() => !Alive(bm) || impactSeen || bm.State != BattleState.Resolving, 12f);
        yield return new WaitForSeconds(0.3f);
        yield return Shot(name + "_b_" + weapon + "_impact");
        yield return new WaitForSeconds(1.0f);
        yield return Shot(name + "_c_" + weapon + "_after");
        yield return WaitFor(() => !Alive(bm) || bm.State != BattleState.Resolving, 30f);
    }

    // What changed since the last captured aim (that turn's shot, plus the effects at the start of this one):
    // fortress integrity, commander health, and how blocks were lost (b broken, s sunk, v vanished).
    void NoteChanges(BattleManager bm, string tag) {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(tag + " after " + (lastLabel ?? "start") + ":");
        foreach (Fortress f in bm.fortresses) {
            float integrity = f.Integrity, hp = f.CommanderAlive ? f.commander.hp : 0f;
            if (!lastIntegrity.TryGetValue(f, out float i0)) i0 = integrity;
            if (!lastHP.TryGetValue(f, out float h0)) h0 = hp;
            losses.TryGetValue(f, out int[] n);
            n ??= new int[3];
            sb.Append("  " + f.HeroClass + " " + Mathf.RoundToInt(i0 * 100f) + ">" + Mathf.RoundToInt(integrity * 100f) + "% hp " +
                      Mathf.RoundToInt(h0) + ">" + Mathf.RoundToInt(hp) + " b" + n[0] + " s" + n[1] + " v" + n[2]);
            lastIntegrity[f] = integrity;
            lastHP[f] = hp;
            losses[f] = new int[3];
        }
        notes.Add(sb.ToString());
    }

    // Diagnostics: the first moments a block moves very fast or ends up below its fortress's ground, with what
    // it overlaps, then its path for a few physics steps.
    readonly HashSet<Block> traced = new HashSet<Block>();
    readonly List<(Block b, int steps)> following = new List<(Block, int)>();
    int traceCount;

    void FixedUpdate() {
        if (hooked == null) return;
        for (int i = following.Count - 1; i >= 0; i--) {
            var (b, steps) = following[i];
            if (b == null || steps <= 0) { following.RemoveAt(i); continue; }
            notes.Add("    step: pos " + b.transform.position.ToString("F2") + " vel " + b.rb.linearVelocity.ToString("F1") + " sleeping " + b.rb.IsSleeping());
            following[i] = (b, steps - 1);
        }
        if (traceCount >= 12) return;
        foreach (Fortress f in hooked.fortresses) {
            float ground = f.transform.position.y;
            foreach (Block b in f.blocks) {
                if (b == null || b.rb == null || traced.Contains(b)) continue;
                bool fast = b.rb.linearVelocity.sqrMagnitude > 25f * 25f, below = b.transform.position.y < ground - 1.5f;
                if (!fast && !below) continue;
                traced.Add(b);
                traceCount++;
                Trace(b, fast ? "FAST" : "BELOW GROUND " + ground.ToString("F1"));
                following.Add((b, 6));
                if (traceCount >= 12) return;
            }
        }
    }

    void Trace(Block b, string why) {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("TRACE turn " + hooked.Turn + " " + hooked.State + " " + why + ": " + b.name + " of " + b.fortress.HeroClass + " size " + b.size.ToString("F2") +
                  " pos " + b.transform.position.ToString("F2") + " rot " + b.transform.eulerAngles.ToString("F0") + " vel " + b.rb.linearVelocity.ToString("F1") +
                  " ang " + b.rb.angularVelocity.ToString("F1") + " kin " + b.rb.isKinematic + " box " + b.box.enabled + " detect " + b.rb.detectCollisions +
                  " hp " + b.hp.ToString("F0") + "/" + b.maxHP.ToString("F0"));
        Collider[] near = Physics.OverlapBox(b.transform.position, b.size * 0.5f + Vector3.one * 0.05f, b.transform.rotation, ~0, QueryTriggerInteraction.Collide);
        foreach (Collider c in near) {
            if (c == b.box) continue;
            Rigidbody r = c.attachedRigidbody;
            sb.Append(" | " + c.name + " L" + c.gameObject.layer + (r == null ? " static" : r.isKinematic ? " kinematic" : " dynamic v" + r.linearVelocity.magnitude.ToString("F0")) +
                      (c.isTrigger ? " trigger" : "") + (c.enabled ? "" : " disabled"));
        }
        foreach (Projectile p in Projectile.Active) if (p != null) sb.Append(" || projectile " + p.name + " at " + p.transform.position.ToString("F1"));
        notes.Add(sb.ToString());
    }

    void WatchBlocks() {
        if (hooked == null) return;
        foreach (Fortress f in hooked.fortresses)
            foreach (Block b in f.blocks)
                if (b != null && watched.Add(b)) b.Broken += OnBlockGone;
    }

    void OnBlockGone(Block b) {
        if (b == null || b.fortress == null) return;
        if (!losses.TryGetValue(b.fortress, out int[] n)) losses[b.fortress] = n = new int[3];
        n[b.IsBroken ? 0 : b.transform.position.y < World.KillY ? 1 : 2]++;
    }

    IEnumerator LoadBattle(Action start) {
        if (hooked != null) hooked.TurnStarted -= OnTurnStarted;
        hooked = null;
        turnsTaken.Clear();
        BattleManager previous = BattleManager.I;
        start();
        // Wait for the NEW battle: the old scene stays active while the fader fades out.
        yield return WaitFor(() => SceneManager.GetActiveScene().name == GameSession.BattleScene && BattleManager.I != null &&
                                   BattleManager.I != previous && BattleManager.I.State != BattleState.Building, 30f);
        hooked = BattleManager.I;
        lastCaptured = 0;
        lastLabel = null;
        lastIntegrity.Clear();
        lastHP.Clear();
        losses.Clear();
        watched.Clear();
        traced.Clear();
        following.Clear();
        traceCount = 0;
        if (hooked != null) {
            hooked.TurnStarted += OnTurnStarted;
            WatchBlocks();
        } else notes.Add("battle did not load");
    }

    void OnTurnStarted(Fortress f) {
        turnsTaken.TryGetValue(f, out int n);
        turnsTaken[f] = n + 1;
        GameSession.ForcedSlot = forceSlots ? n % 4 : benchSlot;
        WatchBlocks();   // blocks rebuilt by a time rewind are new
    }

    static bool Alive(BattleManager bm) { return bm != null; }

    static IEnumerator WaitFor(Func<bool> cond, float timeoutRealtime) {
        float t = 0f;
        while (!cond() && t < timeoutRealtime) { t += Time.unscaledDeltaTime; yield return null; }
    }

    IEnumerator Shot(string name) {
        string path = Path.Combine(dir, name + ".png");
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        float t = 0f;
        while (!File.Exists(path) && t < 5f) { t += Time.unscaledDeltaTime; yield return null; }
        yield return new WaitForSecondsRealtime(0.1f);
    }

    IEnumerator Watchdog(float deadline) {
        while (Time.realtimeSinceStartup < deadline) yield return new WaitForSecondsRealtime(1f);
        Finish("TIMEOUT");
    }

    void Finish(string status) {
        List<string> lines = new List<string> {
            "status: " + status,
            "errors+exceptions: " + errors + ", warnings: " + warnings,
            "battle fps: avg " + (fpsSamples > 0 ? (fpsSum / fpsSamples).ToString("0") : "-") + ", worst 0.5 s window " + worstFps.ToString("0"),
            "", "notes:",
        };
        lines.AddRange(notes);
        lines.Add("");
        lines.Add("errors:");
        lines.AddRange(errorLines);
        File.WriteAllLines(Path.Combine(dir, "report.txt"), lines);
        Debug.Log("[AUTOTEST] " + status + " errors=" + errors);
        Application.Quit();
    }
}

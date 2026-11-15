using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Game state for one battle: builds the arena from GameSession's BattleSetup, then runs the turn loop
// (pick shooter -> wind -> aim (player or bot) -> fire -> resolve physics -> check defeats) until one
// team is left. UI and camera listen to the events; nothing here knows about the UI.
[RequireComponent(typeof(HumanShotController), typeof(BotShotController))]
public class BattleManager : MonoBehaviour {
    public static BattleManager I { get; private set; }
    public static float Wind { get; private set; }

    public BattleSetup Setup { get; private set; }
    public readonly List<Team> teams = new List<Team>();
    public readonly List<Fortress> fortresses = new List<Fortress>();
    public Fortress Current { get; private set; }
    public int Turn { get; private set; }
    public BattleState State { get; private set; }
    public Team Winner { get; private set; }
    public int Stars { get; private set; }
    public float Elapsed { get; private set; }
    public ShotCommand LastShot { get; private set; }
    public Vector3? LastImpact { get; private set; }

    public bool PlayerWon => Winner != null && (Winner.HasHuman || (GameSession.AutoPlay && Winner.id == 0));
    public bool IsHotSeat { get { int n = 0; foreach (Fortress f in fortresses) if (f.IsHuman) n++; return n > 1; } }
    public bool IsCampaign => Setup != null && Setup.levelIndex >= 0;

    public HumanShotController Human { get; private set; }
    public BotShotController Bot { get; private set; }

    public event Action BattleBuilt;
    public event Action<BattleState> StateChanged;
    public event Action<Fortress> TurnStarted;
    public event Action<Fortress, WeaponDef> ShotFired;
    public event Action<Fortress> FortressDefeated;
    public event Action BattleEnded;
    public event Action<string, string> Banner;   // big centred title + subtitle

    int teamCursor = -1;
    bool skipIntro;
    Fortress forcedNext;
    BattleSnapshot currentSnapshot, previousSnapshot;   // taken at the start of the last two turns

    void Awake() {
        I = this;
        Human = GetComponent<HumanShotController>();
        Bot = GetComponent<BotShotController>();
        TimeFX.Reset();
        TurnActor.ClearAll();
        Projectile.Active.Clear();
        Wind = 0f;
        World.SetGravityScale(1f);
    }

    void OnEnable() { Projectile.AnyImpact += OnImpact; }

    void OnDisable() { Projectile.AnyImpact -= OnImpact; }

    void OnDestroy() {
        if (I == this) I = null;
        TimeFX.Reset();
        Wind = 0f;
        World.SetGravityScale(1f);   // a space warp must not outlive its battle
    }

    void Update() {
        TimeFX.Tick();
        if (State != BattleState.Finished && State != BattleState.Building) Elapsed += Time.deltaTime;
    }

    IEnumerator Start() {
        Setup = GameSession.CreateSetup();
        teamCursor = Setup.firstTeam - 1;   // NextTeam() moves on to firstTeam
        Build();
        BattleBuilt?.Invoke();
        Music.PlayBattle(Setup.theme);
        yield return Intro();
        while (State != BattleState.Finished) yield return RunTurn();
    }

    // ------------------------------------------------------------------ build
    void Build() {
        SetState(BattleState.Building);
        ArenaBuilder.Build(Setup);
        for (int i = 0; i < Setup.fortresses.Count; i++) {
            FortressSetup fs = Setup.fortresses[i];
            Team team = teams.Find(t => t.id == fs.team);
            if (team == null) { team = new Team { id = fs.team }; teams.Add(team); }
            Fortress f = Fortress.Build(fs, i);
            f.team = team;
            team.fortresses.Add(f);
            fortresses.Add(f);
        }
        teams.Sort((a, b) => a.id.CompareTo(b.id));
        if (BattleCamera.I != null) BattleCamera.I.SetBattle(this);
    }

    IEnumerator Intro() {
        SetState(BattleState.Intro);
        Banner?.Invoke(Setup.title, Setup.subtitle);
        BattleCamera cam = BattleCamera.I;
        if (cam != null) {
            Fortress last = fortresses[fortresses.Count - 1];
            cam.Frame(last.GetBounds(), 6f, instant: true);
            cam.OverviewSoon(0.9f);
        }
        float t = 0f;
        while (t < 2.8f && !skipIntro) { t += Time.deltaTime; yield return null; }
    }

    public void SkipIntro() { skipIntro = true; }

    // ------------------------------------------------------------------ turns
    IEnumerator RunTurn() {
        Fortress f;
        if (forcedNext != null && !forcedNext.defeated) {
            f = forcedNext;                      // Dalí's melting clocks gave him this turn
            teamCursor = teams.IndexOf(f.team);
        } else {
            Team team = NextTeam();
            if (team == null) { EndBattle(); yield break; }
            f = team.NextShooter();
        }
        forcedNext = null;
        if (f == null) { EndBattle(); yield break; }
        Current = f;
        Turn++;
        f.TickCooldowns();
        Wind = Setup.windMax > 0f ? Mathf.Round(UnityEngine.Random.Range(-Setup.windMax, Setup.windMax) * 10f) / 10f : 0f;
        LastImpact = null;

        SetState(BattleState.TurnStart);
        TurnStarted?.Invoke(f);
        SFX.Play(SoundId.TurnStart, null, 0.6f);
        if (BattleCamera.I != null) BattleCamera.I.FrameForAim(f);

        // Effects that work every turn (painted sun, anteater, cloak, space warp) act now.
        foreach (TurnEffect e in TurnEffect.All.ToArray()) {
            if (e == null) continue;
            yield return e.OnTurnStart(f);
        }
        // They may have finished a commander (this shooter or another one): settle that before anyone aims.
        if (AnyFallen()) {
            yield return CheckDefeats();
            if (State == BattleState.Finished) yield break;
            if (f.CommanderAlive && !f.defeated) {   // the fall was shown: back to this shooter's turn
                SetState(BattleState.TurnStart);
                if (BattleCamera.I != null) BattleCamera.I.FrameForAim(f);
            }
        }
        previousSnapshot = currentSnapshot;   // taken even when this turn ends here: the clocks rewind turn by turn
        currentSnapshot = BattleSnapshot.Take(this);
        if (!f.CommanderAlive || f.defeated) yield break;
        yield return new WaitForSeconds(0.55f);

        SetState(BattleState.Aiming);
        ShotCommand cmd = null;
        if (f.IsHuman) yield return Human.TakeTurn(f, c => cmd = c);
        else yield return Bot.TakeTurn(f, c => cmd = c);
        if (cmd == null || !f.CommanderAlive) {   // he fell while aiming (fire, rubble, the sea): no throw from the grave
            yield return CheckDefeats();
            yield break;
        }

        Fire(f, cmd);
        SetState(BattleState.Resolving);
        yield return Resolve();
        if (cmd.fromBot && LastImpact.HasValue)
            Debug.Log("[Siege] " + f.DisplayName + " " + f.Weapon(cmd.slot).name + " missed by " + (LastImpact.Value - cmd.target).magnitude.ToString("0.0") + " m");
        if (f.CommanderAlive) f.commander.ResetPose();
        yield return CheckDefeats();
    }

    Team NextTeam() {
        for (int i = 1; i <= teams.Count; i++) {
            int k = (teamCursor + i) % teams.Count;
            if (teams[k].IsAlive) { teamCursor = k; return teams[k]; }
        }
        return null;
    }

    void Fire(Fortress f, ShotCommand cmd) {
        WeaponDef def = f.Weapon(cmd.slot);
        f.selectedSlot = cmd.slot;
        f.Consume(cmd.slot);
        LastShot = cmd;
        if (f.CommanderAlive) f.commander.PlayThrow();
        Vector3 origin = f.LaunchOrigin;
        VFX.MagicBurst(origin, def.color, 0.6f);   // shows where the throw comes out (above the chamber roof)
        WeaponFactory.Fire(f, def, origin, cmd.velocity, cmd);
        if (!def.selfCast) SFX.Play(def.id == WeaponId.Mjolnir ? SoundId.LaunchHeavy : SoundId.Launch, origin, 0.8f);
        ShotFired?.Invoke(f, def);
        if (BattleCamera.I != null) BattleCamera.I.FollowShot();
    }

    IEnumerator Resolve() {
        float t = 0f;
        while (TurnActor.Count > 0 && t < 18f) { t += Time.deltaTime; yield return null; }
        if (TurnActor.Count > 0) {
            Debug.LogWarning("[Siege] turn actors timed out: " + TurnActor.Count);
            TurnActor.ClearAll();
        }
        float s = 0f;
        while (s < 3.5f) {
            s += Time.deltaTime;
            if (s > 0.7f && WorldSettled()) break;
            yield return null;
        }
    }

    bool WorldSettled() {
        foreach (Fortress f in fortresses) {
            foreach (Block b in f.blocks)
                if (b != null && b.rb != null && !b.rb.IsSleeping() && b.rb.linearVelocity.sqrMagnitude > 0.36f) return false;
            if (f.CommanderAlive && f.commander.rb.linearVelocity.sqrMagnitude > 0.36f) return false;
        }
        return true;
    }

    bool AnyFallen() {
        foreach (Fortress f in fortresses) if (f.ShouldBeDefeated(out _)) return true;
        return false;
    }

    IEnumerator CheckDefeats() {
        List<(Fortress f, string reason)> fallen = new List<(Fortress, string)>();
        foreach (Fortress f in fortresses)
            if (f.ShouldBeDefeated(out string reason)) fallen.Add((f, reason));
        if (fallen.Count > 0) {
            SetState(BattleState.Collapse);
            foreach (var x in fallen) {
                Banner?.Invoke(x.f.DisplayName + " defeated!", x.reason);
                if (BattleCamera.I != null) BattleCamera.I.Frame(x.f.GetBounds(), 5f);
                FortressDefeated?.Invoke(x.f);
                yield return x.f.Collapse(x.reason);
            }
            yield return new WaitForSeconds(0.6f);
        }
        int alive = 0;
        foreach (Team t in teams) if (t.IsAlive) alive++;
        if (alive <= 1) EndBattle();
    }

    // Dalí's melting clocks: back to the start of the previous turn (the enemy's last move is undone), and
    // the requester gets the next turn. His own cooldowns stay as they are now. False = nothing to rewind.
    public bool RewindTime(Fortress requester) {
        BattleSnapshot s = previousSnapshot;   // none on the first turn: nothing has happened yet
        if (s == null || requester == null) return false;
        foreach (TurnEffect e in TurnEffect.All.ToArray())
            if (e != null && e.createdTurn >= s.turn) e.Cancel();
        s.Restore(requester);
        TurnActor.ClearAll();
        forcedNext = requester;
        previousSnapshot = null;   // the same past cannot be rewound to twice
        Banner?.Invoke("Time melts…", requester.DisplayName + " rewinds the last move and plays again");
        return true;
    }

    void EndBattle() {
        if (State == BattleState.Finished) return;
        foreach (TurnEffect e in TurnEffect.All.ToArray()) if (e != null) e.Cancel();
        Winner = teams.Find(t => t.IsAlive);
        Current = null;
        Wind = 0f;
        if (IsCampaign && PlayerWon) {
            Fortress p = fortresses.Find(x => x.setup.team == 0);
            float health = p == null ? 0f : ((p.CommanderAlive ? p.commander.hp / p.commander.maxHP : 0f) + p.Integrity) * 0.5f;
            Stars = health >= 0.7f ? 3 : health >= 0.4f ? 2 : 1;
            SaveData.RecordWin(Setup.levelIndex, Stars);
        }
        SetState(BattleState.Finished);
        SFX.Play(PlayerWon || IsHotSeat ? SoundId.Victory : SoundId.Defeat);
        if (BattleCamera.I != null) BattleCamera.I.Overview();
        BattleEnded?.Invoke();
    }

    void SetState(BattleState s) {
        State = s;
        StateChanged?.Invoke(s);
    }

    void OnImpact(Projectile p, Vector3 point) {
        if (State == BattleState.Resolving && LastImpact == null && p.owner == Current) LastImpact = point;
    }

    // ------------------------------------------------------------------ queries for UI / AI
    public Fortress HumanFortress => fortresses.Find(f => f.IsHuman);

    public List<Fortress> EnemiesOf(Fortress f) {
        return fortresses.FindAll(x => x.team != f.team && !x.defeated);
    }

    public Bounds ArenaBounds() {
        Bounds b = fortresses.Count > 0 ? fortresses[0].GetBounds() : new Bounds(Vector3.zero, Vector3.one * 10f);
        foreach (Fortress f in fortresses) b.Encapsulate(f.GetBounds());
        return b;
    }
}

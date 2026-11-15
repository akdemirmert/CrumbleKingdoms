using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Player aiming: press the left mouse button anywhere, drag back like a slingshot, release to fire.
// Keyboard: Left/Right = angle, Up/Down = power, Space/Enter = fire, 1-4 = weapon.
// While a shot of the player is flying, a left click activates it (detonate, strike, open a portal...).
public class HumanShotController : MonoBehaviour {
    public Fortress Active { get; private set; }     // fortress currently aiming (null when not our turn)
    public bool IsDragging { get; private set; }
    public bool HasAim { get; private set; }         // an aim is shown (drag or keyboard)
    public float AngleDeg { get; private set; }
    public float Power01 { get; private set; }

    public event Action<int> WeaponSelected;
    public event Action<Projectile> Activated;

    const float MinPullPixels = 14f;
    AimVisuals visuals;
    Vector2 pressPos;
    bool finished;
    Action<ShotCommand> onDone;

    void Awake() { visuals = gameObject.AddComponent<AimVisuals>(); }

    public IEnumerator TakeTurn(Fortress f, Action<ShotCommand> done) {
        Active = f;
        onDone = done;
        finished = false;
        IsDragging = false;
        HasAim = false;
        AngleDeg = f.lastAimAngle;
        Power01 = f.lastAimPower;
        if (!f.IsReady(f.selectedSlot)) f.selectedSlot = 0;
        WeaponSelected?.Invoke(f.selectedSlot);
        while (!finished && f.CommanderAlive) yield return null;   // a commander who dies while aiming ends the turn unfired
        Active = null;
        visuals.Hide();
    }

    public void SelectSlot(int slot) {
        Fortress f = Active;
        // Picked during the short pause before aiming starts: it is kept for the coming turn.
        BattleManager bm = BattleManager.I;
        if (f == null && bm != null && bm.State == BattleState.TurnStart && bm.Current != null && bm.Current.IsHuman) f = bm.Current;
        if (f == null || slot < 0 || slot > 3 || !f.IsReady(slot)) return;
        if (f.selectedSlot != slot) SFX.Play(SoundId.UIClick, null, 0.6f);
        f.selectedSlot = slot;
        WeaponSelected?.Invoke(slot);
        if (Active != null && HasAim) Preview();
    }

    public void CancelAim() {
        IsDragging = false;
        HasAim = false;
        visuals.Hide();
        if (Active != null && Active.CommanderAlive) Active.commander.ResetPose();
    }

    void Update() {
        if (TimeFX.Paused) return;
        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;
        if (Active != null) Aim(mouse, kb);
        else ActivateInFlight(mouse);
    }

    void Aim(Mouse mouse, Keyboard kb) {
        if (kb != null) {
            if (kb.digit1Key.wasPressedThisFrame) SelectSlot(0);
            if (kb.digit2Key.wasPressedThisFrame) SelectSlot(1);
            if (kb.digit3Key.wasPressedThisFrame) SelectSlot(2);
            if (kb.digit4Key.wasPressedThisFrame) SelectSlot(3);
        }
        // Weapons used on yourself (cloak, space warp) need no aim: one click or Space casts them.
        if (Active.SelectedWeapon.selfCast) {
            IsDragging = false;
            HasAim = false;
            visuals.Hide();
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOverUI();
            bool key = kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame);
            if (click || key) { AngleDeg = 80f; Power01 = 0.3f; FireNow(); }
            return;
        }
        if (mouse != null) {
            if (mouse.leftButton.wasPressedThisFrame && !PointerOverUI()) {
                pressPos = mouse.position.ReadValue();
                IsDragging = true;
            }
            if (IsDragging) {
                Vector2 pull = pressPos - mouse.position.ReadValue();
                if (pull.magnitude >= MinPullPixels) {
                    float fx = pull.x * Active.Facing, fy = pull.y;
                    float ang = Mathf.Atan2(fy, fx) * Mathf.Rad2Deg;
                    if (ang < -100f) ang = 89f;   // pulled backwards and down: treat as straight up
                    AngleDeg = Mathf.Clamp(ang, -25f, 89f);
                    Power01 = Mathf.Clamp01(pull.magnitude / (Screen.height * 0.32f));
                    HasAim = true;
                    Preview();
                }
                if (mouse.rightButton.wasPressedThisFrame || (kb != null && kb.escapeKey.wasPressedThisFrame)) { CancelAim(); return; }
                if (mouse.leftButton.wasReleasedThisFrame) {
                    IsDragging = false;
                    if (pull.magnitude >= MinPullPixels) { FireNow(); return; }
                    CancelAim();
                }
            }
        }
        if (kb != null && !IsDragging) {
            float da = 0f, dp = 0f;
            if (kb.leftArrowKey.isPressed) da += Active.Facing > 0 ? 1f : -1f;
            if (kb.rightArrowKey.isPressed) da -= Active.Facing > 0 ? 1f : -1f;
            if (kb.upArrowKey.isPressed) dp += 1f;
            if (kb.downArrowKey.isPressed) dp -= 1f;
            if (da != 0f || dp != 0f) {
                float fast = kb.shiftKey.isPressed ? 2.5f : 1f;
                AngleDeg = Mathf.Clamp(AngleDeg + da * 35f * fast * Time.unscaledDeltaTime, -25f, 89f);
                Power01 = Mathf.Clamp01(Power01 + dp * 0.45f * fast * Time.unscaledDeltaTime);
                HasAim = true;
                Preview();
            }
            if (HasAim && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) FireNow();
        }
    }

    void Preview() {
        if (Active == null) return;
        WeaponDef def = Active.SelectedWeapon;
        Vector3 v = Ballistics.FromAngle(AngleDeg, Mathf.Lerp(Active.MinSpeed(def), Active.MaxSpeed(def), Power01), Active.Facing);
        visuals.Show(Active.LaunchOrigin, v, def.gravityScale, def.color, Power01);
        if (Active.CommanderAlive) Active.commander.SetAim(AngleDeg);
    }

    void FireNow() {
        if (Active == null || finished) return;
        WeaponDef def = Active.SelectedWeapon;
        ShotCommand cmd = new ShotCommand {
            slot = Active.selectedSlot,
            velocity = Ballistics.FromAngle(AngleDeg, Mathf.Lerp(Active.MinSpeed(def), Active.MaxSpeed(def), Power01), Active.Facing),
        };
        if (!def.selfCast) {                   // a cast on yourself has no aim worth remembering
            Active.lastAimAngle = AngleDeg;
            Active.lastAimPower = Power01;
        }
        visuals.Hide();
        HasAim = false;
        IsDragging = false;
        finished = true;
        onDone?.Invoke(cmd);
    }

    // Automation hooks (used by the automated playtest to drive a player turn).
    public void SimulateAim(float angleDeg, float power01) {
        if (Active == null) return;
        AngleDeg = Mathf.Clamp(angleDeg, -25f, 89f);
        Power01 = Mathf.Clamp01(power01);
        HasAim = true;
        Preview();
    }

    public void SimulateFire() { if (HasAim) FireNow(); }

    void ActivateInFlight(Mouse mouse) {
        BattleManager bm = BattleManager.I;
        if (bm == null || bm.State != BattleState.Resolving || bm.Current == null || !bm.Current.IsHuman) return;
        bool click = mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOverUI();
        bool key = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        if (!click && !key) return;
        foreach (Projectile p in Projectile.Active.ToArray()) {
            if (p != null && p.owner == bm.Current && p.CanActivate) {
                p.TryActivate();
                Activated?.Invoke(p);
                break;
            }
        }
    }

    static bool PointerOverUI() {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}

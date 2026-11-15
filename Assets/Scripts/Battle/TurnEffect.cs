using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Something that keeps working over several turns: Dalí's painted sun and anteater, the Vampire's blood
// cloak, the Alien's space warp. BattleManager runs OnTurnStart of every effect at the start of every
// turn (the shooter is chosen, before he aims) and waits for it. An effect that has played out destroys
// its GameObject. A time rewind cancels effects created after the moment it rewinds to.
public abstract class TurnEffect : MonoBehaviour {
    public static readonly List<TurnEffect> All = new List<TurnEffect>();

    public Fortress owner;     // who created it
    public Fortress target;    // who it works on (null = everybody)
    public int createdTurn;    // BattleManager.Turn when it was created

    protected virtual void OnEnable() { All.Add(this); }
    protected virtual void OnDisable() { All.Remove(this); }

    public void Init(Fortress owner, Fortress target) {
        this.owner = owner;
        this.target = target;
        createdTurn = BattleManager.I != null ? BattleManager.I.Turn : 0;
    }

    // Runs at the start of each turn; `shooter` is about to aim. The turn waits for the coroutine.
    public virtual IEnumerator OnTurnStart(Fortress shooter) { yield break; }

    // Removed by a time rewind or at the end of the battle: undo any lasting change (gravity, shields...).
    public virtual void Cancel() { Destroy(gameObject); }

    // The counter a time rewind saves and puts back (meals or flares left, turns passed) for an effect that is
    // older than the moment rewound to.
    public virtual int RewindState { get => 0; set { } }

    // HUD status chip on a fortress card, e.g. "Cloak 2" or "Sun 3". Null = not shown.
    public virtual string StatusText => null;
    public virtual Color StatusColor => Color.white;
    public virtual Fortress StatusOn => target != null ? target : owner;   // whose card shows the chip
}

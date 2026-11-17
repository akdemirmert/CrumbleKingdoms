using System.Collections;
using UnityEngine;

// Heads or Tails: the Gambler calls heads or tails (a human in the HUD before he aims, a bot at random) and flips a small
// gold coin (GamblerCoinToss) at the enemy. It tumbles end over end, its faces flashing, and stops dead where it first
// touches the other side (a block, a commander) or the ground; there it turns its face to the camera, rocks to a stop and
// shows how it came up. If that is the call, the jackpot rings and a giant coin (GamblerCoinGiant) appears high over the
// little one and crashes straight down through whatever stands there; where it stops, a shockwave. If not, no luck: the
// little coin lies there a moment and is gone. The flip itself never does damage. A lucky streak (GamblerLuck) makes the
// giant coin bigger (its size, pierce budget and shockwave radius) and harder (its piercing, commander hit and shockwave).
public static class GamblerCoin {
    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        bool call = GamblerHand.Of(owner).TakeCall();   // true = heads
        bool win = GamblerHand.FlipWins();
        GamblerKit.Announce(origin + Vector3.up * 0.8f, call ? "Calls HEADS!" : "Calls TAILS!", GamblerKit.Neon);
        GamblerSounds.Play(GamblerSounds.CoinFlip, origin, 0.8f);
        GamblerCoinToss.Throw(owner, def, origin, velocity, win ? call : !call, win, GamblerLuck.Damage(owner), GamblerLuck.Radius(owner));
    }

    // Weapon icon: a gold coin turned a little, so its rim shows.
    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Heads or Tails", parent);
        GamblerKit.Coin(root.transform, 0.45f).localRotation = Quaternion.Euler(0f, 25f, 0f);
        return root;
    }
}

// The flipped coin: a small gold coin that does no damage. It flips end over end along the throw and stops dead on its
// first contact (its own side's blocks and commanders never touch it). How it comes up was decided as it left the hand,
// so it is built that way round and the flip running out shows that face. It holds the turn until the giant coin is on
// its way (a win) or until it has gone (no luck); after a win it stays as the giant coin's mark until that lands on it.
public class GamblerCoinToss : Projectile {
    const float Radius = 0.28f;                     // the model
    const float BodyRadius = 0.2f, Mass = 0.12f;    // the collider: where it stops, its rim digs in a little
    const float FlipSpeed = 1500f;                  // deg/s end over end
    const float SettleTime = 0.3f;                  // landed: the flip runs out on its face...
    const float WobbleTime = 0.45f, Wobble = 16f;   // ...and it rocks to a stop (degrees)
    const float GiantDelay = 0.45f;                 // jackpot -> the giant coin appears
    const float LieTime = 1f, FadeTime = 0.4f;      // no luck: it lies there, then shrinks away
    const float MarkTimeout = 4f;                   // safety net: the giant coin never came down on it
    static readonly Color Grey = new Color(0.62f, 0.62f, 0.66f);

    bool headsUp, win, landed;
    float damageBoost, radiusBoost, spin;
    Transform flip;

    // headsUp: the face it comes up on; win: whether that is the call. The boosts are the lucky streak's, read at Fire.
    public static void Throw(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity, bool headsUp, bool win,
                             float damageBoost, float radiusBoost) {
        WeaponFactory.Spawn<GamblerCoinToss>("Coin", owner, def, origin, velocity, c => {
            c.headsUp = headsUp;
            c.win = win;
            c.damageBoost = damageBoost;
            c.radiusBoost = radiusBoost;
        });
    }

    protected override void OnLaunch(Vector3 velocity) {
        flip = ModelKit.Root("Flip", transform).transform;
        GamblerKit.Coin(flip, Radius).localRotation = headsUp ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
        gameObject.AddComponent<SphereCollider>().radius = BodyRadius;
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;   // only the model flips
        VFX.Trail(transform, new Color(1f, 0.85f, 0.4f, 0.7f), 0.16f, 0.2f);                          // a glint behind it
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (!finished && !landed && transform.position.y < World.WaterY) Sink();
    }

    void Update() {
        if (finished || landed) return;
        spin = Mathf.Repeat(spin + FlipSpeed * Time.deltaTime, 360f);
        flip.localRotation = Quaternion.Euler(spin, 0f, 0f);
    }

    void OnCollisionEnter(Collision c) {
        if (finished || landed || IsOwnTeam(c.collider)) return;
        bool touched = c.contactCount > 0;
        Land(touched ? c.GetContact(0).point : transform.position, touched ? c.GetContact(0).normal : Vector3.up);
    }

    // It stops dead where it touched: out of physics from now on, just off the surface.
    void Land(Vector3 point, Vector3 normal) {
        if (point.y < World.WaterY) { Sink(); return; }   // it struck the island's foot under the waterline
        landed = true;
        LichKit.Freeze(this);
        normal.z = 0f;
        Vector3 spot = point + (normal.sqrMagnitude > 1e-4f ? normal.normalized : Vector3.up) * BodyRadius;
        spot.z = 0f;
        transform.position = spot;
        maxLifetime = age + 8f;   // safety net: the whole show takes about two seconds
        RaiseImpact(point);
        GamblerSounds.Play(GamblerSounds.CoinLand, spot, 0.8f);
        StartCoroutine(Reveal(spot));
    }

    // The flip runs out on its face (whichever way round is nearer), the coin rocks to a stop, and the verdict.
    IEnumerator Reveal(Vector3 spot) {
        float from = spin, to = Mathf.Round(spin / 360f) * 360f;
        for (float t = 0f; t < SettleTime; t += Time.deltaTime) {
            float k = 1f - t / SettleTime;
            flip.localRotation = Quaternion.Euler(Mathf.Lerp(from, to, 1f - k * k * k), 0f, 0f);
            yield return null;
        }
        GamblerKit.Announce(spot + Vector3.up * 0.9f, headsUp ? "HEADS!" : "TAILS!", GamblerKit.Gold, 1.4f);
        for (float t = 0f; t < WobbleTime; t += Time.deltaTime) {
            float w = Wobble * Mathf.Exp(-t * 7f) * Mathf.Sin(t * 30f);
            flip.localRotation = Quaternion.Euler(w, 0f, w * 0.5f);
            yield return null;
        }
        flip.localRotation = Quaternion.identity;

        if (!win) {
            GamblerSounds.Play(GamblerSounds.NoLuck, spot, 0.8f);
            GamblerKit.Announce(spot + Vector3.up * 0.5f, "No luck", Grey);   // below the face, which has risen by now
            yield return new WaitForSeconds(LieTime);
            for (float t = 0f; t < FadeTime; t += Time.deltaTime) {
                flip.localScale = Vector3.one * Mathf.Max(0.001f, 1f - Mathf.SmoothStep(0f, 1f, t / FadeTime));
                yield return null;
            }
            Finish(true);
            yield break;
        }

        GamblerSounds.Play(GamblerSounds.Jackpot, spot, 0.9f);
        GamblerKit.Sparkle(spot, 1.2f);
        GamblerCoinGiant.FrameDrop(spot);
        yield return new WaitForSeconds(GiantDelay);
        GamblerCoinGiant giant = GamblerCoinGiant.Drop(owner, def, spot, headsUp, damageBoost, radiusBoost);
        Finish(false);   // the giant coin holds the turn from here
        for (float t = 0f; t < MarkTimeout && giant != null && !giant.finished && giant.Bottom > spot.y + Radius; t += Time.deltaTime)
            yield return null;
        GamblerKit.Sparkle(spot, 0.4f, false);
        Destroy(gameObject);
    }

    // Into the sea before it touched anything: a little splash and the turn ends. No verdict, no coin from the sky.
    void Sink() {
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);
        if (!impacted) RaiseImpact(p);
        VFX.Splash(p, 0.5f);
        SFX.Play(SoundId.Splash, p, 0.4f);
        Finish(true);
    }
}

// The giant coin of a call that came up: it grows in high over the little coin with a flash, showing the winning face,
// hangs there a moment and crashes straight down at a steady speed (no gravity, no wind), piercing what stands in its way
// (PiercingProjectile: every block eats part of its budget and slows it down). Where it stops (the ground, its budget
// spent, or a commander) it sends out a shockwave, stays dug in a moment, then sinks away. In the sea it only splashes.
public class GamblerCoinGiant : PiercingProjectile {
    const float Height = 22f;                       // over the little coin
    const float BaseRadius = 1.4f, Speed = 40f;
    const float GrowTime = 0.25f, Hover = 0.3f;     // it grows in and hangs this long before it falls
    const float CommanderHit = 70f, ShockDamage = 50f;
    const float Dig = 0.55f;                        // share of its radius it ends up dug into what stopped it
    const float HoldTime = 0.8f, SinkTime = 0.6f;
    const float FrameWidth = 16f;
    static readonly Color DustTint = new Color(0.74f, 0.68f, 0.58f, 0.6f);

    float radius, damageBoost, radiusBoost, stuckFor;
    bool headsUp, falling;
    Transform pivot;
    GameObject trail;
    Vector3 rest;

    // The lowest point of the coin.
    public float Bottom => transform.position.y - radius;

    public static GamblerCoinGiant Drop(Fortress owner, WeaponDef def, Vector3 spot, bool headsUp, float damageBoost, float radiusBoost) {
        float r = BaseRadius * radiusBoost;
        return WeaponFactory.Spawn<GamblerCoinGiant>("Giant coin", owner, def, spot + Vector3.up * Height, Vector3.zero, g => {
            g.radius = r;
            g.headsUp = headsUp;
            g.damageBoost = damageBoost;
            g.radiusBoost = radiusBoost;
            g.sweepRadius = r * 0.8f;
            g.budget = def.pierce * radiusBoost;
            g.damage = def.damage * damageBoost;
            g.commanderDamage = CommanderHit * damageBoost;
        });
    }

    // The camera takes in the sky the giant coin will come from and the spot it will hit.
    public static void FrameDrop(Vector3 spot) {
        if (BattleCamera.I == null) return;
        BattleCamera.I.Frame(new Bounds(spot + Vector3.up * (Height * 0.5f), new Vector3(FrameWidth, Height + 5f, 0f)), 2f);
    }

    protected override void OnLaunch(Vector3 velocity) {
        base.OnLaunch(velocity);
        gravityScale = 0f;              // it keeps its own steady speed: it hangs, then falls at Speed
        windFactor = 0f;
        maxLifetime = Hover + 4f;       // safety net: the fall takes well under a second
        pivot = ModelKit.Root("Giant coin", transform).transform;
        GamblerKit.Coin(pivot, radius).localRotation = headsUp ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
        pivot.localScale = Vector3.one * 0.001f;
        trail = VFX.Trail(transform, new Color(1f, 0.82f, 0.4f, 0.45f), radius * 1.4f, 0.18f);
        Stopped += OnStopped;
        GamblerKit.Sparkle(transform.position, 1.6f);
        GamblerSounds.Play(GamblerSounds.CoinFlip, transform.position, 0.8f, 0.5f);   // the flip's ping, a giant's
    }

    protected override void FixedUpdate() {
        if (!falling && !finished && age >= Hover) {
            falling = true;
            manualVelocity = Vector3.down * Speed;
        }
        base.FixedUpdate();
        if (!finished && transform.position.y < World.WaterY) Splash();
    }

    void Update() {
        if (pivot == null) return;
        if (!IsStuck) {
            float k = Mathf.Clamp01(age / GrowTime) - 1f;
            pivot.localScale = Vector3.one * Mathf.Max(0.001f, 1f + 2.7f * k * k * k + 1.7f * k * k);   // grows in, overshooting a little
            pivot.rotation = Quaternion.identity;   // upright, its face to the camera, however the flight turns the projectile
            return;
        }
        stuckFor += Time.deltaTime;
        pivot.rotation = Quaternion.Euler(0f, 0f, 4f * Mathf.Exp(-stuckFor * 6f) * Mathf.Sin(stuckFor * 45f));   // the slam rings through it
        if (stuckFor < HoldTime) return;
        float s = Mathf.Clamp01((stuckFor - HoldTime) / SinkTime);
        transform.position = rest + Vector3.down * radius * 0.8f * s * s;
        pivot.localScale = Vector3.one * Mathf.Max(0.001f, 1f - s * s);
        if (s >= 1f) Destroy(gameObject);
    }

    void OnStopped(PiercingProjectile self, Vector3 point, Collider hit) {
        // It stays where it stopped on its own: a block or commander breaking under it must neither take it along nor drop
        // it as debris (PiercingProjectile leaves alone a projectile that has a body of its own).
        transform.SetParent(null, true);
        Embedded holder = GetComponent<Embedded>();
        if (holder != null) Destroy(holder);
        gameObject.AddComponent<Rigidbody>().isKinematic = true;
        rest = new Vector3(point.x, point.y + radius * (1f - Dig), 0f);
        transform.position = rest;
        TrailRenderer streak = trail != null ? trail.GetComponent<TrailRenderer>() : null;
        if (streak != null) streak.emitting = false;

        Vector3 at = new Vector3(point.x, point.y, 0f);
        float blast = def.radius * radiusBoost;
        Explosion.Create(at, blast, ShockDamage * damageBoost, def.force, owner, effects: false);
        Crash(at, blast);
        GamblerSounds.Play(GamblerSounds.CoinCrash, at, 1f);
        BattleCamera.Shake(0.55f);
    }

    // The crash: a flash and a gold shock ring, dust thrown out low to both sides, gold sparks and a shower of sparkles.
    static void Crash(Vector3 at, float blast) {
        Vector3 c = GamblerKit.Front(at);
        FxLayers.Flash.Emit(c, Vector3.zero, blast * 1.4f, 0.14f, new Color(1f, 0.93f, 0.75f));
        FxLayers.Shock.Emit(c, Vector3.zero, blast * 2.6f, 0.4f, new Color(1f, 0.8f, 0.35f, 0.7f));
        for (int i = 0; i < 18; i++) {
            float side = i % 2 == 0 ? 1f : -1f;
            Vector3 v = new Vector3(side * Random.Range(1f, 3f) * blast, Random.Range(0.3f, 2f), Random.Range(-0.5f, 0f));
            FxLayers.Dust.Emit(c + new Vector3(side * Random.Range(0f, 0.4f) * blast, Random.Range(-0.2f, 0.4f), 0f), v,
                               Random.Range(0.3f, 0.55f) * blast, Random.Range(0.8f, 1.4f), DustTint, Random.Range(-40f, 40f));
        }
        VFX.Sparks(at, GamblerKit.Gold, 30);
        GamblerKit.Sparkle(at + Vector3.up * 1.2f, 2f);
    }

    // Nothing stood under it all the way down to the sea.
    void Splash() {
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);
        VFX.Splash(p, 2.5f);
        SFX.Play(SoundId.Splash, p, 1f);
        Finish(true);
    }
}

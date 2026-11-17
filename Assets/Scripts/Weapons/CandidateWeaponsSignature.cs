using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Signature (self-cast; the aim means nothing). A mote of gold light flies up from the Candidate's hand and becomes a
// giant fountain pen high over the middle of the battle (half way from him to his enemies, some nine metres above the
// higher fortress top). The pen signs a loopy cursive signature in glowing gold ink (a new one every time) and underlines
// it with a flourish; the signature flares, then gathers into a glowing wax seal (one per enemy) as the decree is sealed,
// and the pen lifts away. Each seal flies to an enemy fortress and stamps itself onto it over the commander: he is
// SILENCED (CandidateSilence: all his weapons but one that never cools down locked for his next turn, for two from vote tier 4), and from tier 1 the
// ink burns him (WeaponDef.damage per tier, magic: a decree, not a blast). The seal then hovers over him while the
// silence lasts. More votes: a bigger signature, pen and seals (CandidateVotes.Radius and the tier), brighter ink and
// more sparkle. Animated by hand, no physics and no colliders: nothing can get in its way. The turn waits for the whole
// show, about three seconds.
public class CandidateSignature : Projectile {
    // timeline (s)
    const float RiseTime = 0.22f;        // the mote flies from his hand up to where the pen appears
    const float PopTime = 0.15f;         // the pen appears there
    const float WriteTime = 1.15f;       // the name
    const float UnderlineTime = 0.25f;   // the flourish beneath it
    const float GleamTime = 0.12f;       // the finished signature flares...
    const float CondenseTime = 0.2f;     // ...and gathers into the seal(s)
    const float PenFadeTime = 0.35f;     // meanwhile the pen lifts away
    const float FlightBase = 0.25f, FlightPerMetre = 0.01f, FlightMin = 0.4f, FlightMax = 0.75f;
    const float PressTime = 0.12f;       // the seal stamps down...
    const float SettleTime = 0.25f;      // ...then shrinks up over the silenced commander (CandidateSilence.IntroTime)
    const float SealsTimeout = 3f;       // safety net for the deliveries

    // layout (m; all of it times `size`)
    const float SkyGap = 8f;             // the signature's middle: this much (plus its size) above the higher fortress top
    const float RunWidth = 3.4f;         // the run of letters after the capital
    const float InkWidth = 0.13f, HaloWidth = 0.5f;
    const float SealRadius = 0.75f;      // at the stamp
    const float SealSpacing = 2.2f;      // seal radii between the seals formed for several enemies
    const float InkZ = -1.2f, PenZ = -1.4f;
    public const float SealZ = -1.25f;   // seals stay in front of the blocks
    const float PenTilt = -122f;         // the pen's +X (towards the nib) points down and to the left, as a hand holds it

    // growth with the votes
    const float SizePerTier = 0.04f;     // on top of CandidateVotes.Radius
    const float GlowPerTier = 0.25f;
    const int LongSilenceTier = 4;       // from here the silence lasts two turns

    static readonly Color InkGold = new Color(1f, 0.8f, 0.34f);
    static readonly Color FlashGold = new Color(1f, 0.9f, 0.6f);
    static readonly Color MoteTrail = new Color(1f, 0.85f, 0.45f, 0.8f);
    static readonly Color SilencedText = new Color(1f, 0.9f, 0.6f);
    static readonly Color SectionColor = new Color(0.05f, 0.06f, 0.12f);   // the pen's grip: near black
    static readonly Color WaxColor = new Color(0.96f, 0.68f, 0.2f), WaxGlow = new Color(0.9f, 0.55f, 0.12f);
    static readonly Color RimColor = new Color(1f, 0.9f, 0.6f), RimGlow = new Color(1.2f, 1f, 0.55f);
    static readonly Color SquiggleGlow = new Color(1.6f, 1.2f, 0.45f);
    static Mesh waxMesh, rimMesh, markMesh, squiggleMesh;

    int tier;                  // vote tier, read at Fire
    float size = 1f;           // the whole show's scale, read at Fire
    float glow, burn, lightBase;
    int turns;
    readonly List<Fortress> targets = new List<Fortress>();   // the enemies to silence, left to right
    Vector3 center;            // the signature's middle, in the sky
    Vector2 extent;            // its half size
    Vector3[] path;            // the signature then its underline (world)
    float[] dist;              // arc length to each point
    int nameEnd;               // the last point of the name; the underline follows
    int cursor, placed;        // Reveal's progress
    LineRenderer ink, halo;
    Transform pen;
    Light nibLight;
    TurnActor hold;
    bool released;             // the seals are on their way: his fall no longer stops the decree
    int sealsOut;
    float lean, sparkleIn;
    Vector3 lastHead;

    public override bool CanActivate => false;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        int tier = CandidateVotes.Tier(owner);
        float radius = CandidateVotes.Radius(owner);
        WeaponFactory.Spawn<CandidateSignature>("Signature", owner, def, origin, Vector3.zero, s => {
            s.tier = tier;
            s.size = radius * (1f + SizePerTier * tier);
        });
    }

    // Weapon-bar icon: the pen along +X, nib forward (IconRenderer tips every weapon model 38 degrees up to the right,
    // which gives the slant), and below its nib a short gold squiggle, laid at -38 degrees so it comes out level.
    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Signature", parent);
        Transform icon = BuildPen(root.transform);
        icon.localPosition = new Vector3(0.5f, 0f, 0f);
        icon.localScale = Vector3.one * 0.42f;
        ModelKit.MeshPart(root.transform, SquiggleMesh, new Vector3(0.27f, -0.63f, 0f), Vector3.one,
                          Mats.Emissive(CandidateKit.Gold, SquiggleGlow), new Vector3(0f, 0f, -38f), "Squiggle");
        return root;
    }

    protected override void OnLaunch(Vector3 velocity) {
        maxLifetime = 8f;                  // safety net: the show takes about three seconds
        cameraFollows = false;             // the camera frames the whole show instead (FrameShow)
        hold = GetComponent<TurnActor>();
        glow = 1f + GlowPerTier * tier;
        turns = tier >= LongSilenceTier ? 2 : 1;
        burn = tier >= 1 && def != null ? def.damage * tier : 0f;
        if (BattleManager.I != null && owner != null)
            foreach (Fortress f in BattleManager.I.EnemiesOf(owner))
                if (f != null && f.team != owner.team && f.CommanderAlive) targets.Add(f);
        targets.Sort((a, b) => a.commander.transform.position.x.CompareTo(b.commander.transform.position.x));
        Vector3 hand = transform.position;
        center = SkySpot();
        transform.position = center;
        Compose();
        BuildInk();
        pen = BuildPen(transform);
        nibLight = new GameObject("Nib light").AddComponent<Light>();
        nibLight.transform.SetParent(pen, false);
        nibLight.transform.localPosition = new Vector3(-0.15f, 0f, -0.7f);
        nibLight.type = LightType.Point;
        nibLight.color = InkGold;
        nibLight.range = 4.5f * size;
        nibLight.shadows = LightShadows.None;
        lightBase = 2.5f + 0.4f * tier;
        PosePen(path[0], 0f, 0f);
        StartCoroutine(Show(hand));
    }

    // Over the middle of the battle: half way from him to his enemies, SkyGap (and its size) above the highest top.
    Vector3 SkySpot() {
        Vector3 me = owner != null && owner.CommanderAlive ? owner.commander.transform.position : transform.position;
        float top = owner != null ? owner.GetBounds().max.y : me.y;
        float x = me.x + (owner != null ? owner.Facing : 1) * 12f;
        if (targets.Count > 0) {
            float sum = 0f;
            foreach (Fortress f in targets) {
                sum += f.commander.transform.position.x;
                top = Mathf.Max(top, f.GetBounds().max.y);
            }
            x = (me.x + sum / targets.Count) * 0.5f;
        }
        return new Vector3(x, top + SkyGap + size, InkZ);
    }

    // ------------------------------------------------------------------ the show
    IEnumerator Show(Vector3 hand) {
        yield return null;                 // BattleManager follows the shot right after Fire: frame the show after that
        if (Interrupted()) yield break;
        FrameShow();

        // A mote of gold light flies from his hand up to where the pen appears.
        Vector3 start = path[0];
        Transform mote = ModelKit.Root("Mote", transform).transform;
        mote.position = hand;
        VFX.Trail(mote, MoteTrail, 0.3f * size, 0.25f);
        for (float t = 0f; t < RiseTime; t += Time.deltaTime) {
            if (Interrupted()) yield break;
            float k = t / RiseTime;
            Vector3 p = Vector3.Lerp(hand, start, k * k * (3f - 2f * k)) + Vector3.up * (2f * k * (1f - k));
            mote.position = p;
            FxLayers.Glow.Emit(CandidateKit.Front(p), Vector3.zero, 0.7f, 0.12f, InkGold);
            yield return null;
        }
        Destroy(mote.gameObject);

        // The pen appears.
        VFX.MagicBurst(start, InkGold, 0.7f * size);
        for (float t = 0f; t < PopTime; t += Time.deltaTime) {
            if (Interrupted()) yield break;
            PosePen(start, BackOut(t / PopTime), 0f);
            yield return null;
        }
        PosePen(start, 1f, 0f);

        // It signs: the name...
        CandidateSounds.Play(CandidateSounds.PenScribble, start, 0.9f);
        float nameLength = dist[nameEnd], total = dist[path.Length - 1];
        lastHead = start;
        for (float t = 0f; t < WriteTime; t += Time.deltaTime) {
            if (Interrupted()) yield break;
            float k = t / WriteTime;
            Nib(Reveal(nameLength * Mathf.Lerp(k, k * k * (3f - 2f * k), 0.35f)));
            yield return null;
        }
        // ...and the flourish beneath it.
        CandidateSounds.Play(CandidateSounds.PenScribble, path[nameEnd], 0.7f, 1.3f);
        for (float t = 0f; t < UnderlineTime; t += Time.deltaTime) {
            if (Interrupted()) yield break;
            float k = t / UnderlineTime;
            Nib(Reveal(Mathf.Lerp(nameLength, total, k * k * (3f - 2f * k))));
            yield return null;
        }
        Vector3 end = Reveal(total);
        PosePen(end, 1f, lean);
        Sparkle(end, 0.6f * size, 8 + 2 * tier);

        // The signature flares as the pen lifts off it.
        Gleam();
        for (float t = 0f; t < GleamTime; t += Time.deltaTime) {
            if (Interrupted()) yield break;
            float k = t / GleamTime, f = Mathf.Sin(Mathf.PI * k);
            ink.widthMultiplier = InkWidth * size * (1f + 0.5f * f);
            halo.widthMultiplier = HaloWidth * size * (1f + 0.7f * f);
            PosePen(end + Vector3.up * (0.25f * size * k), 1f, lean);
            yield return null;
        }

        // The ink gathers into the seal(s), one per enemy, and the pen goes.
        int n = Mathf.Max(1, targets.Count);
        Transform[] seals = new Transform[n];
        for (int i = 0; i < n; i++) {
            seals[i] = BuildSeal(transform, glow);
            seals[i].position = new Vector3(center.x + (i - (n - 1) * 0.5f) * SealSpacing * SealRadius * size, center.y, SealZ);
            seals[i].localScale = Vector3.one * 0.001f;
        }
        StartCoroutine(PenAway(end + Vector3.up * (0.25f * size)));
        Vector3[] work = new Vector3[path.Length];
        ink.positionCount = halo.positionCount = path.Length;
        for (float t = 0f; t < CondenseTime; t += Time.deltaTime) {
            if (Interrupted()) yield break;
            float k = t / CondenseTime, e = k * k;
            for (int i = 0; i < work.Length; i++) work[i] = Vector3.LerpUnclamped(path[i], center, e);
            ink.SetPositions(work);
            halo.SetPositions(work);
            ink.widthMultiplier = InkWidth * size * (1f - 0.5f * e);
            halo.widthMultiplier = HaloWidth * size * (1f - 0.3f * e);
            float s = Mathf.Max(0.001f, SealRadius * size * BackOut(k));
            foreach (Transform seal in seals) seal.localScale = Vector3.one * s;
            yield return null;
        }
        if (Interrupted()) yield break;

        // The decree is sealed: off they fly.
        released = true;
        Destroy(ink.gameObject);
        Destroy(halo.gameObject);
        foreach (Transform seal in seals) seal.localScale = Vector3.one * (SealRadius * size);
        CandidateSounds.Play(CandidateSounds.Decree, center, 1f);
        FxLayers.Flash.Emit(center, Vector3.zero, 3.5f * size, 0.22f, FlashGold);
        VFX.Ring(center, 2.4f * size, InkGold, 0.45f, 0.35f);
        VFX.LightFlash(center, InkGold, 5f + tier, 10f * size, 0.4f);
        Sparkle(center, size, 18 + 5 * tier);
        if (targets.Count == 0) Burst(seals[0]);   // nobody to silence
        else {
            sealsOut = targets.Count;
            for (int i = 0; i < targets.Count; i++) StartCoroutine(Deliver(seals[i], targets[i]));
        }
        for (float t = 0f; sealsOut > 0 && t < SealsTimeout; t += Time.deltaTime) {
            if (Interrupted()) yield break;
            yield return null;
        }
        for (float t = 0f; t < SettleTime; t += Time.deltaTime) {   // the seals shrink up over the silenced commanders
            if (Interrupted()) yield break;
            yield return null;
        }
        Finish(true);
    }

    // The turn went on without it (a time rewind or the turn timeout cleared its TurnActor): it just goes. His commander
    // fell before the decree was sealed: the ink and the pen dissolve and nobody is silenced.
    bool Interrupted() {
        if (finished) return true;
        if (hold == null) { Finish(true); return true; }
        if (!released && (owner == null || !owner.CommanderAlive)) { Fizzle(); return true; }
        return false;
    }

    protected override void OnLost() { Fizzle(); }

    // Gold dust where the ink and the pen were, and the cast is over.
    void Fizzle() {
        if (finished) return;
        if (ink != null) {
            int shown = Mathf.Min(ink.positionCount, path.Length);
            for (int i = 0; i < 24 && shown > 0; i++)
                FxLayers.Star.Emit(path[Random.Range(0, shown)], new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.2f, 1.2f), 0f),
                                   Random.Range(0.14f, 0.26f) * size, Random.Range(0.4f, 0.7f), InkGold, Random.Range(-90f, 90f));
        }
        if (pen != null) Sparkle(pen.position, 0.6f * size, 10);
        Finish(true);
    }

    // One seal flies to `target` on an arc, spinning, and stamps itself onto his fortress over the commander.
    IEnumerator Deliver(Transform seal, Fortress target) {
        if (!Alive(target)) {
            Burst(seal);
            sealsOut--;
            yield break;
        }
        Vector3 from = seal.position;
        float span = Vector3.Distance(from, StampPoint(target));
        float time = Mathf.Clamp(FlightBase + FlightPerMetre * span, FlightMin, FlightMax);
        float spin = Random.value < 0.5f ? -360f : 360f, starIn = 0f;   // one whole turn: it leaves and lands upright
        for (float t = 0f; t < time; t += Time.deltaTime) {
            if (finished) yield break;     // the cast was cut short: the seal goes with it
            if (!Alive(target)) break;
            float k = t / time, e = Mathf.Lerp(k, k * k, 0.6f), u = 1f - e;
            Vector3 to = StampPoint(target);
            Vector3 mid = (from + to) * 0.5f + Vector3.up * (1.5f + 0.06f * span);
            Vector3 p = u * u * from + 2f * u * e * mid + e * e * to;
            float turn = 1f - (1f - k) * (1f - k);                       // fast at first, slowing to the stamp
            seal.SetPositionAndRotation(p, Quaternion.Euler(18f * Mathf.Sin(Mathf.PI * k), 0f, spin * turn));
            seal.localScale = Vector3.one * (SealRadius * size * (1f + 0.3f * k * k));   // it rears up to stamp
            FxLayers.Glow.Emit(p + Vector3.back * 0.05f, Vector3.zero, 1.1f * SealRadius * size, 0.18f, InkGold);
            starIn -= Time.deltaTime;
            if (starIn <= 0f) {
                starIn = 0.05f;
                FxLayers.Star.Emit(p + (Vector3)(Random.insideUnitCircle * 0.4f * size), new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.2f, 0.9f), 0f),
                                   Random.Range(0.14f, 0.24f) * size, Random.Range(0.35f, 0.6f), InkGold, Random.Range(-90f, 90f));
            }
            yield return null;
        }
        if (finished) yield break;
        if (!Alive(target)) {   // he fell on the way: nobody left to silence
            Burst(seal);
            sealsOut--;
            yield break;
        }
        Vector3 at = StampPoint(target);
        seal.SetPositionAndRotation(at, Quaternion.identity);
        CandidateSilence silence = Stamp(target, at);
        for (float t = 0f; t < PressTime && !finished; t += Time.deltaTime) {
            float k = t / PressTime;
            float s = k < 0.5f ? Mathf.Lerp(1.3f, 0.9f, k * 2f) : Mathf.Lerp(0.9f, 1f, k * 2f - 1f);
            seal.localScale = Vector3.one * (SealRadius * size * s);
            if (Alive(target)) seal.position = StampPoint(target);
            yield return null;
        }
        if (silence != null) silence.Adopt(seal);   // it hovers over him from now on
        else Burst(seal);
        sealsOut--;
    }

    // The seal strikes: a flash, a ring and a shower of gold; from vote tier 1 the burning ink; then, if he stands, the
    // silence. Returns the silence (null if he fell).
    CandidateSilence Stamp(Fortress target, Vector3 at) {
        FxLayers.Flash.Emit(at, Vector3.zero, 2.6f * size, 0.16f, FlashGold);
        VFX.Ring(at, 1.8f * size, InkGold, 0.4f, 0.3f);
        VFX.LightFlash(at, InkGold, 5f, 7f * size, 0.3f);
        Sparkle(at, 0.9f * size, 16 + 4 * tier);
        BattleCamera.Shake(0.12f);
        CandidateSounds.Play(CandidateSounds.Silence, at, 0.95f);
        if (burn > 0f && owner != null && target.team != owner.team && target.CommanderAlive) {
            InkSplash(at, size, tier);
            target.commander.ApplyDamage(new DamageInfo(burn, DamageType.Magic, target.commander.AimPoint, owner));
        }
        if (!target.CommanderAlive) return null;
        VFX.FloatingText(target.commander.transform.position + Vector3.up * 3.3f, "SILENCED", SilencedText, 1.3f);
        return CandidateSilence.Apply(owner, target, turns);
    }

    // The pen lifts away, shrinking with a little twist, and is gone in a puff of gold.
    IEnumerator PenAway(Vector3 from) {
        float lean0 = lean;
        for (float t = 0f; t < PenFadeTime && pen != null; t += Time.deltaTime) {
            float k = t / PenFadeTime;
            PosePen(from + new Vector3(0.4f, 1.2f, 0f) * (size * k), 1f - k * k, lean0 + 40f * k * k);
            yield return null;
        }
        if (pen == null) yield break;
        Sparkle(pen.position, 0.5f * size, 8 + 2 * tier);
        Destroy(pen.gameObject);
        pen = null;
    }

    // ------------------------------------------------------------------ ink
    void BuildInk() {
        halo = NewLine("Ink glow", FxAssets.Additive(FxAssets.Beam, 0.8f + 0.25f * tier),
                       new Color(1f, 0.72f, 0.25f, 0.26f + 0.05f * tier), HaloWidth * size, FxLayers.OrderGlow);
        ink = NewLine("Ink", FxAssets.Alpha(FxAssets.Beam, 1.3f + 0.2f * tier), InkGold, InkWidth * size, FxLayers.OrderGlow + 1);
    }

    LineRenderer NewLine(string name, Material mat, Color color, float width, int order) {
        LineRenderer lr = new GameObject(name).AddComponent<LineRenderer>();
        lr.transform.SetParent(transform, false);
        lr.useWorldSpace = true;
        lr.positionCount = 0;
        lr.sharedMaterial = mat;
        lr.startColor = lr.endColor = color;
        lr.widthMultiplier = width;
        lr.numCornerVertices = 3;
        lr.numCapVertices = 4;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sortingOrder = order;
        return lr;
    }

    // Shows the ink up to arc length s (it only ever grows) and returns where the nib is.
    Vector3 Reveal(float s) {
        int last = path.Length - 1;
        while (cursor < last - 1 && dist[cursor + 1] <= s) cursor++;
        float seg = dist[cursor + 1] - dist[cursor];
        float f = seg > 1e-5f ? Mathf.Clamp01((s - dist[cursor]) / seg) : 1f;
        bool tip = f > 0.001f;
        Vector3 head = tip ? Vector3.Lerp(path[cursor], path[cursor + 1], f) : path[cursor];
        int count = cursor + (tip ? 2 : 1);
        if (ink.positionCount != count) ink.positionCount = halo.positionCount = count;
        for (; placed <= cursor; placed++) {
            ink.SetPosition(placed, path[placed]);
            halo.SetPosition(placed, path[placed]);
        }
        if (tip) {
            ink.SetPosition(cursor + 1, head);
            halo.SetPosition(cursor + 1, head);
        }
        return head;
    }

    // The pen follows its nib, leaning with the stroke; the nib glows and sheds sparks.
    void Nib(Vector3 head) {
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        float vx = (head.x - lastHead.x) / dt;
        lastHead = head;
        lean = Mathf.Lerp(lean, Mathf.Clamp(vx * 0.5f, -12f, 12f), 1f - Mathf.Exp(-12f * dt));
        PosePen(head, 1f, lean);
        Vector3 p = new Vector3(head.x, head.y, PenZ - 0.05f);
        FxLayers.Glow.Emit(p, Vector3.zero, 0.5f * size, 0.12f, InkGold);
        sparkleIn -= dt;
        if (sparkleIn > 0f) return;
        sparkleIn = 0.07f / (1f + 0.3f * tier);
        FxLayers.Star.Emit(p, new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(0.2f, 1.4f), 0f), Random.Range(0.12f, 0.22f) * size,
                           Random.Range(0.35f, 0.6f), Color.Lerp(InkGold, Color.white, 0.3f), Random.Range(-90f, 90f));
    }

    // The finished signature flares: a flash at its middle and stars all along it.
    void Gleam() {
        FxLayers.Flash.Emit(center, Vector3.zero, 3f * size, 0.18f, FlashGold);
        int n = 16 + 6 * tier;
        for (int i = 0; i < n; i++)
            FxLayers.Star.Emit(path[Random.Range(0, path.Length)] + Vector3.back * 0.1f,
                               new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.3f, 1.6f), 0f), Random.Range(0.18f, 0.32f) * size,
                               Random.Range(0.4f, 0.75f), Color.Lerp(InkGold, Color.white, Random.value * 0.5f), Random.Range(-90f, 90f));
        if (tier >= 3) VFX.LightFlash(center, InkGold, 3f + tier, 9f * size, 0.3f);
    }

    // The signature: a lead-in, a capital loop, a run of cursive letters (a trochoid whose arches vary: small loops, tall
    // loops, round humps, now and then a descender), a tail swinging up, then the underline sweeping round and back
    // beneath it all. A new one every cast, about 5 x 2.3 m (times `size`) and 16-20 m of ink. Centred on `center`, in
    // world space; nameEnd = the name's last point.
    void Compose() {
        List<Vector2> pts = new List<Vector2>(256);
        int letters = Random.Range(4, 7);
        float k = RunWidth / (2f * Mathf.PI * letters);   // advance per radian
        float[] up = new float[letters + 1], wide = new float[letters + 1], down = new float[letters + 1];
        for (int i = 0; i <= letters; i++) {
            float kind = Random.value;
            if (kind < 0.3f) {                             // a tall loop (l, h)
                up[i] = Random.Range(0.7f, 0.95f);
                wide[i] = k * Random.Range(1.35f, 1.7f);
            } else if (kind < 0.55f) {                     // a round hump, no loop (n, m): wide < 0 broadens its top
                up[i] = Random.Range(0.25f, 0.4f);
                wide[i] = k * Random.Range(-0.6f, 0.1f);
            } else {                                       // a small loop (e)
                up[i] = Random.Range(0.28f, 0.48f);
                wide[i] = k * Random.Range(1.6f, 2.3f);
            }
            down[i] = Random.value < 0.2f ? Random.Range(0.5f, 0.65f) : Random.Range(0.16f, 0.28f);   // now and then a descender
        }
        up[1] = Random.Range(1.1f, 1.3f);                  // the capital
        wide[1] = k * Random.Range(2.6f, 3.1f);
        float slant = Random.Range(0.2f, 0.34f), phase = Random.Range(0f, 2f * Mathf.PI);

        // Loop m (its top at th = 2 pi m) keeps its height and width between the bottoms either side of it; bottom j (at
        // th = (2j + 1) pi) its depth between the tops either side: each switch happens where it changes nothing.
        Vector2 Body(float th) {
            int m = Mathf.Clamp(Mathf.FloorToInt((th + Mathf.PI) / (2f * Mathf.PI)), 1, letters);
            int j = Mathf.Clamp(Mathf.FloorToInt(th / (2f * Mathf.PI)), 0, letters);
            float c = Mathf.Cos(th);
            float y = (c >= 0f ? up[m] : down[j]) * c + 0.07f * Mathf.Sin(0.21f * th + phase);
            return new Vector2(k * th - wide[m] * Mathf.Sin(th) + slant * y, y);
        }

        float th0 = Mathf.PI, th1 = Mathf.PI * (1 + 2 * letters);
        Vector2 b0 = Body(th0);
        Bezier(pts, b0 + new Vector2(-0.5f, -0.32f), b0 + new Vector2(-0.22f, -0.02f), b0, 6, 0, 5);   // lead-in
        int steps = letters * 30;
        for (int i = 0; i <= steps; i++) pts.Add(Body(Mathf.Lerp(th0, th1, (float)i / steps)));
        Vector2 e = pts[pts.Count - 1];
        Bezier(pts, e, e + new Vector2(0.45f, 0f), e + new Vector2(0.85f, 0.5f), 10, 1, 10);         // the tail
        nameEnd = pts.Count - 1;
        float xMin = float.MaxValue, yMin = float.MaxValue;
        foreach (Vector2 p in pts) { xMin = Mathf.Min(xMin, p.x); yMin = Mathf.Min(yMin, p.y); }
        Vector2 t0 = pts[nameEnd];
        Bezier(pts, t0, t0 + new Vector2(0.5f, -0.8f), new Vector2(xMin + 0.9f, yMin - 0.75f),       // the underline
               new Vector2(xMin - 0.15f, yMin - 0.3f), 28, 1, 28);

        Vector2 lo = pts[0], hi = pts[0];
        foreach (Vector2 p in pts) { lo = Vector2.Min(lo, p); hi = Vector2.Max(hi, p); }
        Vector2 mid = (lo + hi) * 0.5f;
        extent = (hi - lo) * (0.5f * size);
        path = new Vector3[pts.Count];
        dist = new float[pts.Count];
        for (int i = 0; i < pts.Count; i++) {
            Vector2 p = (pts[i] - mid) * size;
            path[i] = new Vector3(center.x + p.x, center.y + p.y, InkZ);
            if (i > 0) dist[i] = dist[i - 1] + Vector3.Distance(path[i - 1], path[i]);
        }
    }

    // Samples from..to (of n steps) of a quadratic Bezier.
    static void Bezier(List<Vector2> pts, Vector2 a, Vector2 b, Vector2 c, int n, int from, int to) {
        for (int i = from; i <= to; i++) {
            float t = (float)i / n, u = 1f - t;
            pts.Add(u * u * a + 2f * u * t * b + t * t * c);
        }
    }

    // Samples from..to (of n steps) of a cubic Bezier.
    static void Bezier(List<Vector2> pts, Vector2 a, Vector2 b, Vector2 c, Vector2 d, int n, int from, int to) {
        for (int i = from; i <= to; i++) {
            float t = (float)i / n, u = 1f - t;
            pts.Add(u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d);
        }
    }

    // ------------------------------------------------------------------ helpers
    // The camera takes in the signature (and the pen over it) and every fortress a seal will fly to.
    void FrameShow() {
        if (BattleCamera.I == null) return;
        Bounds view = new Bounds(new Vector3(center.x, center.y, 0f), new Vector3(extent.x * 2f, extent.y * 2f, 0f));
        view.Encapsulate(new Vector3(center.x + extent.x, center.y + extent.y + 2f * size, 0f));
        foreach (Fortress f in targets) if (f != null) view.Encapsulate(f.GetBounds());
        if (targets.Count == 0 && owner != null) view.Encapsulate(owner.GetBounds());
        BattleCamera.I.Frame(view, 2.5f);
    }

    void PosePen(Vector3 nib, float scale, float tilt) {
        if (pen == null) return;
        pen.SetPositionAndRotation(new Vector3(nib.x, nib.y, PenZ), Quaternion.Euler(0f, 0f, PenTilt + tilt));
        pen.localScale = Vector3.one * Mathf.Max(0.001f, scale * size);
        if (nibLight != null) nibLight.intensity = lightBase * Mathf.Clamp01(scale);
    }

    static bool Alive(Fortress f) { return f != null && !f.defeated && f.CommanderAlive; }

    // Where a seal stamps: on the fortress over the commander, in front of the blocks.
    static Vector3 StampPoint(Fortress f) {
        Vector3 c = f.commander.AimPoint;
        return new Vector3(c.x, c.y + 0.3f, SealZ);
    }

    static float BackOut(float k) {
        float x = Mathf.Clamp01(k) - 1f;
        return 1f + 2.70158f * x * x * x + 1.70158f * x * x;
    }

    // A shower of gold stars and a warm glow.
    internal static void Sparkle(Vector3 at, float scale, int count) {
        Vector3 p = CandidateKit.Front(at);
        for (int i = 0; i < count; i++)
            FxLayers.Star.Emit(p + (Vector3)(Random.insideUnitCircle * 0.35f * scale), new Vector3(Random.Range(-3f, 3f), Random.Range(0.5f, 4.5f), 0f) * scale,
                               Random.Range(0.16f, 0.3f) * scale, Random.Range(0.45f, 0.85f), Color.Lerp(InkGold, Color.white, Random.value * 0.4f),
                               Random.Range(-90f, 90f));
        FxLayers.Glow.Emit(p, Vector3.zero, 2.2f * scale, 0.3f, InkGold);
    }

    // The burning ink: violet drops flung out of the stamp and violet sparks, more with more votes.
    static void InkSplash(Vector3 at, float scale, int tier) {
        Vector3 p = CandidateKit.Front(at);
        Color drop = new Color(CandidateKit.Ink.r, CandidateKit.Ink.g, CandidateKit.Ink.b, 0.95f);
        int n = 10 + 4 * tier;
        for (int i = 0; i < n; i++) {
            Vector2 d = Random.insideUnitCircle.normalized;
            Vector3 dir = new Vector3(d.x, d.y, 0f);
            FxLayers.Droplet.Emit(p + dir * (0.3f * scale), dir * (Random.Range(3f, 7f) * scale) + Vector3.up * Random.Range(0.5f, 2.5f),
                                  Random.Range(0.12f, 0.24f) * scale, Random.Range(0.5f, 0.9f), drop);
        }
        FxLayers.Glow.Emit(p, Vector3.zero, 2f * scale, 0.35f, CandidateKit.Ink);
        VFX.Sparks(p, CandidateKit.InkHdr, 8 + 3 * tier);
    }

    // A seal with nobody to silence bursts into gold dust.
    static void Burst(Transform seal) {
        if (seal == null) return;
        Sparkle(seal.position, Mathf.Max(0.4f, seal.lossyScale.x), 14);
        Destroy(seal.gameObject);
    }

    // ------------------------------------------------------------------ models
    // The fountain pen, the tip of its nib at the origin and its body along -X (2.53 m): a navy cap and barrel with a
    // gold band, ring, clip and finial, a dark grip and a gold nib whose slit and breather hole face the camera.
    static Transform BuildPen(Transform parent) {
        Transform pen = ModelKit.Root("Pen", parent).transform;
        Material navy = CandidateKit.NavyMat, gold = CandidateKit.GoldMat, dark = Mats.Lit(SectionColor, 0.6f);
        Vector3 alongX = new Vector3(0f, 0f, -90f);   // meshes built along +Y, turned to run along +X
        ModelKit.MeshPart(pen, MeshKit.Cone(10, 0.11f, 0.55f), new Vector3(-0.55f, 0f, 0f), new Vector3(1f, 1f, 0.32f), gold, alongX, "Nib");
        ModelKit.Part(pen, PrimitiveType.Cube, new Vector3(-0.17f, 0f, -0.03f), new Vector3(0.27f, 0.014f, 0.006f), dark);          // slit
        ModelKit.MeshPart(pen, WorldMeshes.Disc(10, 0.025f), new Vector3(-0.32f, 0f, -0.04f), Vector3.one, dark, default, "Breather");
        ModelKit.MeshPart(pen, WorldMeshes.Frustum(14, 0.125f, 0.115f, 0.27f), new Vector3(-0.8f, 0f, 0f), Vector3.one, dark, alongX, "Grip");
        Rod(pen, -0.8f, 0.05f, 0.145f, gold);                                                                                    // ring
        ModelKit.MeshPart(pen, WorldMeshes.Frustum(18, 0.17f, 0.145f, 0.78f), new Vector3(-1.58f, 0f, 0f), Vector3.one, navy, alongX, "Barrel");
        Rod(pen, -1.62f, 0.09f, 0.185f, gold);                                                                                   // band
        Rod(pen, -2.06f, 0.84f, 0.175f, navy);                                                                                   // cap
        ModelKit.Part(pen, PrimitiveType.Sphere, new Vector3(-2.48f, 0f, 0f), new Vector3(0.1f, 0.27f, 0.27f), gold);            // finial
        ModelKit.Part(pen, PrimitiveType.Cube, new Vector3(-2.07f, 0.2f, -0.04f), new Vector3(0.58f, 0.045f, 0.075f), gold);     // clip
        ModelKit.Part(pen, PrimitiveType.Cube, new Vector3(-2.33f, 0.18f, -0.04f), new Vector3(0.07f, 0.06f, 0.075f), gold);
        ModelKit.Part(pen, PrimitiveType.Sphere, new Vector3(-1.79f, 0.195f, -0.04f), Vector3.one * 0.085f, gold);
        ModelKit.Shadows(pen.gameObject, false);
        return pen;
    }

    // A cylinder along X: centre x, length, radius.
    static void Rod(Transform pen, float x, float length, float radius, Material mat) {
        ModelKit.Part(pen, PrimitiveType.Cylinder, new Vector3(x, 0f, 0f), new Vector3(radius * 2f, length * 0.5f, radius * 2f), mat, new Vector3(0f, 0f, 90f));
    }

    // The decree's seal, radius 1 (scale it), facing the camera: a disc of glowing gold wax with a wavy edge, a bright
    // raised rim and the Candidate's mark, a navy tick in a circle. glow: how brightly the wax glows (the votes).
    internal static Transform BuildSeal(Transform parent, float glow) {
        Transform seal = ModelKit.Root("Seal", parent).transform;
        ModelKit.MeshPart(seal, WaxMesh, Vector3.zero, Vector3.one, Mats.Emissive(WaxColor, WaxGlow * glow), default, "Wax");
        ModelKit.MeshPart(seal, RimMesh, Vector3.zero, Vector3.one, Mats.Emissive(RimColor, RimGlow * glow), default, "Rim");
        ModelKit.MeshPart(seal, MarkMesh, Vector3.zero, Vector3.one, CandidateKit.NavyMat, default, "Mark");
        ModelKit.Shadows(seal.gameObject, false, false);
        return seal;
    }

    // The wax: a splat about 1 across in radius and 0.22 thick, its edge wavy, its face domed a little towards the camera.
    static Mesh WaxMesh {
        get {
            if (waxMesh != null) return waxMesh;
            const int Sides = 36;
            const float Half = 0.11f, Dome = 0.05f;
            Vector3[] edge = new Vector3[Sides];
            for (int i = 0; i < Sides; i++) {
                float a = i * 2f * Mathf.PI / Sides;
                float r = 1f + 0.05f * Mathf.Sin(5f * a + 0.7f) + 0.035f * Mathf.Sin(9f * a + 2.1f) + 0.02f * Mathf.Sin(14f * a);
                edge[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            MeshBuilder b = new MeshBuilder();
            Vector3 front = Vector3.back * Half, rear = Vector3.forward * Half;
            int fc = b.Vertex(front + Vector3.back * Dome, new Vector2(0.5f, 0.5f));
            int rc = b.Vertex(rear, new Vector2(0.5f, 0.5f));
            int[] f = new int[Sides], r0 = new int[Sides], wf = new int[Sides], wr = new int[Sides];
            for (int i = 0; i < Sides; i++) {
                Vector2 uv = new Vector2(edge[i].x * 0.5f + 0.5f, edge[i].y * 0.5f + 0.5f);
                f[i] = b.Vertex(edge[i] + front, uv);
                r0[i] = b.Vertex(edge[i] + rear, uv);
                wf[i] = b.Vertex(edge[i] + front, new Vector2(i / (float)Sides, 1f));   // the wall has its own vertices: a crisp edge
                wr[i] = b.Vertex(edge[i] + rear, new Vector2(i / (float)Sides, 0f));
            }
            for (int i = 0; i < Sides; i++) {
                int j = (i + 1) % Sides;
                b.Triangle(fc, f[j], f[i]);        // the face, towards the camera
                b.Triangle(rc, r0[i], r0[j]);      // the back
                b.Triangle(wf[i], wr[j], wr[i]);   // the wall, outwards
                b.Triangle(wf[i], wf[j], wr[j]);
            }
            waxMesh = b.ToMesh("Candidate seal wax");
            return waxMesh;
        }
    }

    static Mesh RimMesh {
        get {
            if (rimMesh != null) return rimMesh;
            MeshBuilder b = new MeshBuilder();
            Stroke(b, Circle(0.8f, 40), 0.08f, -0.172f, true);
            rimMesh = b.ToMesh("Candidate seal rim");
            return rimMesh;
        }
    }

    // The Candidate's mark: a tick in a circle.
    static Mesh MarkMesh {
        get {
            if (markMesh != null) return markMesh;
            const float Z = -0.175f, W = 0.1f;
            MeshBuilder b = new MeshBuilder();
            Stroke(b, Circle(0.5f, 36), 0.085f, Z, true);
            Vector2 a = new Vector2(-0.27f, 0.03f), c = new Vector2(-0.08f, -0.2f), d = new Vector2(0.3f, 0.25f);
            Vector2 d1 = (c - a).normalized, d2 = (d - c).normalized;
            Stroke(b, new[] { a, c + d1 * (W * 0.5f) }, W, Z, false);   // the arms overlap at the corner, so it stays filled
            Stroke(b, new[] { c - d2 * (W * 0.5f), d }, W, Z, false);
            markMesh = b.ToMesh("Candidate mark");
            return markMesh;
        }
    }

    // The icon's squiggle: two little loops ending at the origin (where the nib is), about 0.6 long.
    static Mesh SquiggleMesh {
        get {
            if (squiggleMesh != null) return squiggleMesh;
            const int N = 48;
            const float K = 0.045f;
            List<Vector2> pts = new List<Vector2>(N + 1);
            for (int i = 0; i <= N; i++) {
                float th = Mathf.PI * (1f + 4f * i / N), c = Mathf.Cos(th);
                pts.Add(new Vector2(K * th - 0.085f * Mathf.Sin(th), (c >= 0f ? 0.13f : 0.05f) * c));
            }
            Vector2 end = pts[N];
            for (int i = 0; i <= N; i++) pts[i] -= end;
            MeshBuilder b = new MeshBuilder();
            Stroke(b, pts, 0.045f, 0f, false);
            squiggleMesh = b.ToMesh("Candidate squiggle");
            return squiggleMesh;
        }
    }

    static List<Vector2> Circle(float radius, int n) {
        List<Vector2> pts = new List<Vector2>(n);
        for (int i = 0; i < n; i++) {
            float a = i * 2f * Mathf.PI / n;
            pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
        }
        return pts;
    }

    // Appends a flat stroke `width` wide along pts (XY, at depth z) facing the camera; closed = a loop. The side offset
    // is always to the left of the way the stroke runs, which keeps every quad's winding towards the camera.
    static void Stroke(MeshBuilder b, IList<Vector2> pts, float width, float z, bool closed) {
        int n = pts.Count;
        if (n < 2) return;
        int firstL = -1, firstR = -1, prevL = -1, prevR = -1;
        for (int i = 0; i < n; i++) {
            Vector2 a = closed ? pts[(i - 1 + n) % n] : pts[Mathf.Max(0, i - 1)];
            Vector2 c = closed ? pts[(i + 1) % n] : pts[Mathf.Min(n - 1, i + 1)];
            Vector2 dir = c - a;
            dir = dir.sqrMagnitude > 1e-10f ? dir.normalized : Vector2.right;
            Vector2 side = new Vector2(-dir.y, dir.x) * (width * 0.5f), p = pts[i];
            float u = i / (float)(n - 1);
            int l = b.Vertex(new Vector3(p.x + side.x, p.y + side.y, z), new Vector2(u, 1f));
            int r = b.Vertex(new Vector3(p.x - side.x, p.y - side.y, z), new Vector2(u, 0f));
            if (i == 0) { firstL = l; firstR = r; }
            else b.Quad(prevR, prevL, l, r);
            prevL = l;
            prevR = r;
        }
        if (closed) b.Quad(prevR, prevL, firstL, firstR);
    }
}

// A fortress silenced by the Candidate's Signature: all its weapons but one stay locked (Fortress.cooldowns, set by Apply when
// the seal stamps) for its next `turns` turns, shown as "Silenced" on its card while the decree's seal hovers, glowing,
// over its commander. It counts the fortress's turns in OnTurnStart and breaks up once they have been played (or when
// its commander falls). A second silence on the same fortress lengthens this one instead of adding another.
public class CandidateSilence : TurnEffect {
    const float HoverRadius = 0.34f;          // the seal over his head (m)
    const float HoverHeight = 2.55f;          // above his feet
    const float IntroTime = 0.25f;            // the stamped seal shrinks up to its place
    const float TwinkleEvery = 0.45f;
    static readonly string[] Labels = { "Silenced", "Silenced", "Silenced 2", "Silenced 3", "Silenced 4" };
    static readonly Color TextColor = new Color(1f, 0.9f, 0.6f);

    int turns;                 // his turns it lasts
    int seen;                  // his turns begun since the stamp
    bool over;
    Transform seal;
    Vector3 introFrom;
    float introScale, introT = -1f, pulse, twinkleIn;

    public override string StatusText => over || Left <= 0 ? null : Labels[Mathf.Clamp(Left, 1, Labels.Length - 1)];
    public override Color StatusColor => CandidateKit.Gold;
    public override int RewindState { get => seen; set => seen = value; }

    // His silenced turns still to come, the one he is playing included.
    int Left {
        get {
            BattleManager bm = BattleManager.I;
            bool playing = seen > 0 && bm != null && bm.Current == target;
            return turns - seen + (playing ? 1 : 0);
        }
    }

    // The silence on `f`, if any.
    public static CandidateSilence On(Fortress f) {
        if (f == null) return null;
        foreach (TurnEffect e in All)
            if (e is CandidateSilence s && s.target == f && !s.over) return s;
        return null;
    }

    // Silences `target` for his next `turns` turns: every weapon but his first one that never cools down is locked (a
    // cooldown of turns + 1 skips exactly that many of his turns; a longer one of his own stays). That free weapon is
    // slot 1 for every class but Lucky Guy, whose dice cool down: he keeps his cards, so he is never left with nothing.
    public static CandidateSilence Apply(Fortress owner, Fortress target, int turns) {
        if (target == null || turns <= 0) return null;
        int free = 0;
        while (free < 3 && target.Weapon(free).cooldown > 0) free++;
        for (int i = 0; i < 4; i++) if (i != free) target.cooldowns[i] = Mathf.Max(target.cooldowns[i], turns + 1);
        CandidateSilence s = On(target);
        if (s != null) {
            s.turns = Mathf.Max(s.turns, s.seen + turns);
            return s;
        }
        GameObject go = new GameObject("Silenced " + target.DisplayName);
        s = go.AddComponent<CandidateSilence>();
        s.Init(owner, target);
        s.turns = turns;
        return s;
    }

    // The stamped seal comes to hover over his commander; if one already does, the new one melts into it.
    public void Adopt(Transform incoming) {
        if (incoming == null) return;
        if (over || seal != null) {
            CandidateSignature.Sparkle(incoming.position, Mathf.Max(0.4f, incoming.lossyScale.x), 12);
            Destroy(incoming.gameObject);
            pulse = 1f;
            return;
        }
        seal = incoming;
        seal.SetParent(transform, true);
        introFrom = seal.position;
        introScale = seal.lossyScale.x;
        introT = 0f;
    }

    void Update() {
        if (over) return;
        if (target == null || target.defeated || !target.CommanderAlive) { End(true); return; }
        if (seal == null) return;
        Vector3 hover = Hover(target.commander) + Vector3.up * (0.07f * Mathf.Sin(Time.time * 2.4f));
        float s = HoverRadius;
        if (introT >= 0f) {
            introT += Time.deltaTime;
            float k = Mathf.Clamp01(introT / IntroTime), e = 1f - (1f - k) * (1f - k) * (1f - k);
            hover = Vector3.Lerp(introFrom, hover, e);
            s = Mathf.Lerp(introScale, HoverRadius, e);
            if (k >= 1f) introT = -1f;
        }
        pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 2.5f);
        seal.SetPositionAndRotation(hover, Quaternion.Euler(0f, 0f, 7f * Mathf.Sin(Time.time * 1.7f)));
        seal.localScale = Vector3.one * (s * (1f + 0.35f * pulse));
        twinkleIn -= Time.deltaTime;
        if (twinkleIn > 0f || introT >= 0f) return;
        twinkleIn = TwinkleEvery;
        Vector3 at = CandidateKit.Front(hover + (Vector3)(Random.insideUnitCircle * 0.3f));
        FxLayers.Star.Emit(at, new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.4f, 0.9f), 0f), Random.Range(0.12f, 0.2f),
                           Random.Range(0.6f, 0.9f), CandidateKit.Gold, Random.Range(-90f, 90f));
    }

    static Vector3 Hover(Commander c) {
        Vector3 p = c.transform.position;
        return new Vector3(p.x, p.y + HoverHeight, CandidateSignature.SealZ + 0.1f);
    }

    public override IEnumerator OnTurnStart(Fortress shooter) {
        if (over) yield break;
        if (target == null || target.defeated || !target.CommanderAlive) { End(true); yield break; }
        if (shooter == target) {
            seen++;
            if (seen > turns) { End(true); yield break; }   // he plays twice in a row: this one is free
            Remind();
        } else if (seen >= turns) End(true);               // his silenced turns have been played
    }

    // His silenced turn begins: the seal flares and the word is said again, so he sees why his weapons are locked.
    void Remind() {
        pulse = 1f;
        Vector3 at = target.commander.transform.position + Vector3.up * 3.3f;
        VFX.FloatingText(at, "SILENCED", TextColor, 1f);
        CandidateSounds.Play(CandidateSounds.Silence, at, 0.55f);
    }

    void End(bool puff) {
        if (over) return;
        over = true;
        if (puff && seal != null) CandidateSignature.Sparkle(seal.position, 0.5f, 10);
        Destroy(gameObject);
    }

    // A time rewind past the stamp, or the end of the battle.
    public override void Cancel() { End(false); }
}

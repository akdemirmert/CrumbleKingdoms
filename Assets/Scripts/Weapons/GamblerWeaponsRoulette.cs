using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Roulette Wheel: a roulette wheel about 0.9 m across (a dark wooden rim, a ring of 37 pockets - red, black and the
// green zero - and a gold turret) thrown face to the camera, spinning like a buzzsaw along the aimed arc. It is a
// PiercingProjectile: it saws through blocks until def.pierce worth of them is spent (each takes def.damage) and
// bites a commander for CommanderHit. Where it stops (stuck in a block, a commander or the ground, or its budget gone)
// it spins down while a white ball runs round its rim and drops into a pocket, and that pocket decides the one blast
// at the wheel: red bursts in fire and sets the enemy's wood alight, black hits harder (more force, a heavier, darker
// blast), and the green zero (1 in 37) is the jackpot: a huge blast and a shower of gold. The turn waits for that
// blast. In the sea or out of the arena there is no bet. A lucky streak (GamblerLuck, read once at the throw) scales
// every damage by its Damage boost and the pierce budget and the blast radii by its Radius boost.
public static class GamblerRoulette {
    // Testing (AutoPlaytest bench): ForcedNumber 0..36 makes every ball fall into that number (-1 = chance).
    public static int ForcedNumber = -1;

    public const int Pockets = 37;
    // The European wheel's numbers, clockwise from the zero (red and black alternate after it).
    static readonly int[] Order = { 0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10, 5, 24, 16, 33, 1,
                                    20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26 };
    static readonly int[] Reds = { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };

    // The model, in metres: radii from the axis, depths along z (the face looks towards the camera, -Z).
    const float Radius = 0.45f, RimIn = 0.395f, PocketOut = 0.348f, PocketIn = 0.235f;
    const float BodyDepth = 0.1f, RimZ = -0.085f, TrackZ = -0.06f, PocketZ = -0.058f, FretZ = -0.064f;
    const float ConeTipZ = -0.1f, HandleZ = -0.15f;
    public const float TrackRadius = 0.362f, PocketRadius = 0.29f, BallZ = -0.105f;   // where the ball runs and lands
    const float BallSize = 0.09f;
    const int SubRed = 0, SubBlack = 1, SubGreen = 2, SubGold = 3, SubWood = 4;         // the face's submeshes

    static Mesh faceMesh;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        float luckDamage = GamblerLuck.Damage(owner), luckRadius = GamblerLuck.Radius(owner);
        SFX.Play(SoundId.KnifeThrow, origin, 0.6f, 0.7f);
        VFX.Sparks(origin, GamblerKit.Gold, 10);
        WeaponFactory.Spawn<GamblerRouletteWheel>("Roulette Wheel", owner, def, origin, velocity, w => {
            w.sweepRadius = GamblerRouletteWheel.SweepRadius;
            w.slowPerCost = GamblerRouletteWheel.SlowPerCost;
            w.budget = def.pierce * luckRadius;
            w.damage = def.damage * luckDamage;
            w.commanderDamage = GamblerRouletteWheel.CommanderHit * luckDamage;
            w.damageBoost = luckDamage;
            w.radiusBoost = luckRadius;
        });
    }

    // Weapon bar icon: the wheel face-on, leaning back a little, its ball resting in a pocket.
    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Roulette Wheel", parent);
        Transform lean = ModelKit.Root("Lean", root.transform).transform;
        lean.localRotation = Quaternion.Euler(18f, 0f, 0f);
        Transform wheel = Build(lean).transform;
        float a = PocketAngle(4) * Mathf.Deg2Rad;   // a black pocket: the white ball shows best there
        AddBall(wheel).localPosition = new Vector3(Mathf.Cos(a) * PocketRadius, Mathf.Sin(a) * PocketRadius, BallZ);
        return root;
    }

    // ------------------------------------------------------------------ the wheel's numbers
    public static bool IsRed(int number) { return System.Array.IndexOf(Reds, number) >= 0; }

    public static int PocketOf(int number) { return Mathf.Max(0, System.Array.IndexOf(Order, number)); }

    // A pocket's centre on the wheel, in degrees counter-clockwise from +X: the zero at the top, the rest clockwise.
    public static float PocketAngle(int pocket) { return 90f - pocket * 360f / Pockets; }

    // The number the ball falls into: chance, unless a test fixes it.
    public static int NextNumber() { return ForcedNumber >= 0 && ForcedNumber < Pockets ? ForcedNumber : Random.Range(0, Pockets); }

    public static string Label(int number) { return number == 0 ? "0 - JACKPOT!" : number + (IsRed(number) ? " RED" : " BLACK"); }

    // Floating text has a dark outline, so black is called in white (as the numbers are written on the wheel).
    public static Color LabelColor(int number) {
        return number == 0 ? GamblerKit.Gold : IsRed(number) ? new Color(1f, 0.3f, 0.3f) : new Color(0.9f, 0.9f, 0.95f);
    }

    // ------------------------------------------------------------------ model
    public static Material WoodMat => Mats.Lit(new Color(0.27f, 0.12f, 0.06f), 0.6f);     // polished dark wood
    static Material ZeroMat => Mats.Lit(new Color(0.06f, 0.5f, 0.24f), 0.45f);
    static Material BallMat => Mats.Emissive(Color.white, new Color(0.7f, 0.7f, 0.7f), 0.9f);

    // The wheel, centred on the parent's origin with its face to the camera: a wooden bowl and rim, the ball track,
    // the ring of pockets with gold frets, a wooden cone and the gold turret (a spindle with a cross handle).
    public static GameObject Build(Transform parent) {
        GameObject root = ModelKit.Root("Roulette Wheel", parent);
        Transform r = root.transform;
        Material wood = WoodMat, gold = GamblerKit.GoldMat;
        ModelKit.MeshPart(r, WorldMeshes.Frustum(32, Radius, Radius, BodyDepth), new Vector3(0f, 0f, BodyDepth * 0.5f), Vector3.one, wood,
                          new Vector3(-90f, 0f, 0f), "Bowl");
        GameObject face = ModelKit.MeshPart(r, Face(), Vector3.zero, Vector3.one, wood, default, "Face");
        face.GetComponent<MeshRenderer>().sharedMaterials = new[] { GamblerKit.RedInk, GamblerKit.BlackInk, ZeroMat, gold, wood };
        ModelKit.MeshPart(r, WorldMeshes.Frustum(10, 0.055f, 0.02f, 0.085f), new Vector3(0f, 0f, ConeTipZ + 0.01f), Vector3.one, gold,
                          new Vector3(-90f, 0f, 0f), "Spindle");
        for (int k = 0; k < 2; k++)
            ModelKit.Part(r, PrimitiveType.Cylinder, new Vector3(0f, 0f, HandleZ), new Vector3(0.018f, 0.1f, 0.018f), gold, new Vector3(0f, 0f, k * 90f));
        for (int k = 0; k < 4; k++) {
            float a = k * Mathf.PI * 0.5f;
            ModelKit.MeshPart(r, WorldMeshes.Ball(0.5f), new Vector3(Mathf.Cos(a) * 0.1f, Mathf.Sin(a) * 0.1f, HandleZ), Vector3.one * 0.042f,
                              gold, default, "Knob");
        }
        ModelKit.MeshPart(r, WorldMeshes.Ball(0.5f), new Vector3(0f, 0f, ConeTipZ - 0.085f), Vector3.one * 0.045f, gold, default, "Cap");
        return root;
    }

    // The white ball, a child of the wheel (place it with localPosition).
    public static Transform AddBall(Transform wheel) {
        return ModelKit.MeshPart(wheel, WorldMeshes.Ball(0.5f), new Vector3(0f, PocketRadius, BallZ), Vector3.one * BallSize, BallMat,
                                 default, "Ball").transform;
    }

    // The wheel's face, built once: the pockets (red, black and green submeshes), the gold frets and rings, and the
    // wooden track, rim and centre cone. Submeshes in the order of the materials Build gives it.
    static Mesh Face() {
        if (faceMesh != null) return faceMesh;
        List<Vector3> v = new List<Vector3>();
        List<int>[] t = new List<int>[5];
        for (int i = 0; i < t.Length; i++) t[i] = new List<int>();
        float step = 360f / Pockets;
        for (int i = 0; i < Pockets; i++) {
            int n = Order[i];
            float a = PocketAngle(i);
            Sector(v, t[n == 0 ? SubGreen : IsRed(n) ? SubRed : SubBlack], PocketIn, PocketOut, a - step * 0.5f, a + step * 0.5f, PocketZ, 2);
            Fret(v, t[SubGold], PocketIn, PocketOut, a + step * 0.5f, FretZ, 0.006f);
        }
        Sector(v, t[SubGold], PocketIn - 0.008f, PocketIn + 0.008f, 0f, 360f, FretZ, 64);
        Sector(v, t[SubGold], PocketOut - 0.006f, PocketOut + 0.006f, 0f, 360f, FretZ, 64);
        Sector(v, t[SubWood], PocketOut, RimIn, 0f, 360f, TrackZ, 64);              // the ball track
        Sector(v, t[SubWood], RimIn, Radius, 0f, 360f, RimZ, 64);                   // the rim...
        Band(v, t[SubWood], RimIn, RimZ, TrackZ, 64, false);                        // ...its inner edge
        Band(v, t[SubWood], Radius, RimZ, -BodyDepth * 0.5f, 64, true);             // ...and its outer edge, down to the bowl
        Cone(v, t[SubWood], PocketIn, PocketZ, ConeTipZ, 32);
        Mesh m = new Mesh { name = "Roulette Face" };
        m.SetVertices(v);
        m.subMeshCount = t.Length;
        for (int i = 0; i < t.Length; i++) m.SetTriangles(t[i], i);
        m.RecalculateNormals();
        m.RecalculateBounds();
        faceMesh = m;
        return m;
    }

    // A flat ring sector facing the camera: radii r0..r1, angles a0..a1 (degrees, a0 < a1), at depth z.
    static void Sector(List<Vector3> v, List<int> t, float r0, float r1, float a0, float a1, float z, int segments) {
        int start = v.Count;
        for (int k = 0; k <= segments; k++) {
            float a = Mathf.Lerp(a0, a1, k / (float)segments) * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            v.Add(d * r0 + new Vector3(0f, 0f, z));
            v.Add(d * r1 + new Vector3(0f, 0f, z));
        }
        for (int k = 0; k < segments; k++) {
            int i0 = start + k * 2, o0 = i0 + 1, i1 = i0 + 2, o1 = i0 + 3;   // clockwise as the camera sees them
            t.Add(i0); t.Add(i1); t.Add(o1);
            t.Add(i0); t.Add(o1); t.Add(o0);
        }
    }

    // A thin bar facing the camera from radius r0 to r1 along the angle a (degrees).
    static void Fret(List<Vector3> v, List<int> t, float r0, float r1, float a, float z, float halfWidth) {
        float rad = a * Mathf.Deg2Rad;
        Vector3 d = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f), side = new Vector3(-d.y, d.x, 0f) * halfWidth, depth = new Vector3(0f, 0f, z);
        int i = v.Count;
        v.Add(d * r0 - side + depth);
        v.Add(d * r0 + side + depth);
        v.Add(d * r1 + side + depth);
        v.Add(d * r1 - side + depth);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
        t.Add(i); t.Add(i + 2); t.Add(i + 3);
    }

    // A cylindrical strip of radius r between the depths zFront and zBack, facing away from the axis (or towards it).
    static void Band(List<Vector3> v, List<int> t, float r, float zFront, float zBack, int segments, bool outward) {
        int start = v.Count;
        for (int k = 0; k <= segments; k++) {
            float a = k * Mathf.PI * 2f / segments;
            Vector3 d = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            v.Add(d + new Vector3(0f, 0f, zFront));
            v.Add(d + new Vector3(0f, 0f, zBack));
        }
        for (int k = 0; k < segments; k++) {
            int f0 = start + k * 2, b0 = f0 + 1, f1 = f0 + 2, b1 = f0 + 3;
            if (outward) { t.Add(f0); t.Add(f1); t.Add(b1); t.Add(f0); t.Add(b1); t.Add(b0); }
            else { t.Add(f0); t.Add(b1); t.Add(f1); t.Add(f0); t.Add(b0); t.Add(b1); }
        }
    }

    // A shallow cone facing the camera: its base ring of radius r at depth zBase, its tip on the axis at zTip.
    static void Cone(List<Vector3> v, List<int> t, float r, float zBase, float zTip, int segments) {
        int tip = v.Count;
        v.Add(new Vector3(0f, 0f, zTip));
        for (int k = 0; k <= segments; k++) {
            float a = k * Mathf.PI * 2f / segments;
            v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, zBase));
        }
        for (int k = 0; k < segments; k++) { t.Add(tip); t.Add(tip + k + 2); t.Add(tip + k + 1); }
    }
}

// The thrown wheel (see GamblerRoulette): its flight and cuts, the ball's run once it stops, and the blast.
public class GamblerRouletteWheel : PiercingProjectile {
    public const float SweepRadius = 0.4f, SlowPerCost = 0.12f, CommanderHit = 40f;
    const float BlastDamage = 45f, BlackForce = 1.6f;                  // red and black (radius and force: def)
    const float JackpotRadius = 4.2f, JackpotDamage = 110f, JackpotForce = 16f;
    const float IgniteSeconds = 5f;
    const float FlightSpin = 1440f, StopSpin = 480f, EndSpin = 70f;   // deg/s: a buzzsaw, the jolt of the stop, at the end
    const float SpinDecay = 0.35f;                                     // seconds for the spin-down
    const float BallRun = 0.9f, Suspense = 0.3f;                       // seconds: the ball's run, then the blast
    const float DropAt = 0.55f;                                        // share of the run before it leaves the track
    const float BallTurns = 720f;                                      // it laps the wheel twice, then finds its pocket
    const float GroundLift = 0.22f;                                    // stuck in the ground, most of its face shows
    const float StuckZ = -(World.BlockDepth * 0.5f + 0.12f);           // stopped, it sits just in front of the walls
    static readonly Collider[] near = new Collider[128];
    static FxLayer coins;

    public float damageBoost = 1f, radiusBoost = 1f;   // the owner's luck when it was thrown

    Transform spin, ball;
    GameObject trail;
    TurnActor hold;            // keeps the turn waiting from the stop to the blast
    float spinAngle, spinDir, wheelZ, stopTime = -1f, ballFrom, ballTurn;
    int number;

    protected override void OnLaunch(Vector3 velocity) {
        base.OnLaunch(velocity);
        spin = GamblerRoulette.Build(transform).transform;
        spinDir = -travelDir;                 // clockwise when thrown to the right: it rolls forward
        spinAngle = Random.Range(0f, 360f);
        wheelZ = transform.position.z;
        trail = VFX.Trail(transform, GamblerKit.Gold, 0.3f, 0.18f);
        BlockPierced += OnCut;
        Stopped += OnStopped;
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (!finished && transform.position.y < World.WaterY) Sink();
    }

    // The wheel is placed in world space: it spins about its own axis whatever the body under it does (the flight
    // direction, a tumbling host), and once stopped it slides out in front of the walls so the ball can be seen.
    void LateUpdate() {
        if (spin == null) return;
        float dt = Time.deltaTime, speed = FlightSpin, z = transform.position.z;
        if (stopTime >= 0f) {
            float t = Time.time - stopTime;
            speed = EndSpin + (StopSpin - EndSpin) * Mathf.Exp(-t / SpinDecay);
            z = StuckZ;
            if (ball != null) PlaceBall(t);
        }
        spinAngle = Mathf.Repeat(spinAngle + spinDir * speed * dt, 360f);
        wheelZ = Mathf.MoveTowards(wheelZ, z, 8f * dt);
        Vector3 c = transform.position;
        spin.SetPositionAndRotation(new Vector3(c.x, c.y, wheelZ), Quaternion.Euler(0f, 0f, spinAngle));
    }

    // Biting through a block: a fan of sparks off the rim and a metal screech (the block throws its own chips).
    void OnCut(PiercingProjectile self, Block block, Vector3 point) {
        Vector3 back = manualVelocity.sqrMagnitude > 0.01f ? -manualVelocity.normalized : Vector3.left * travelDir;
        Grind(point, back);
        SFX.Play(SoundId.ImpactMetal, point, 0.45f, 1.6f);
        BattleCamera.Shake(0.08f);
    }

    // Stuck, or its budget spent: the bet is on. Right after this the projectile finishes, so the wheel holds the turn
    // itself (TurnActor) until its blast. If its host broke under it, PiercingProjectile lets it drop (Embedded) and
    // the ball still runs while it falls; the blast is wherever it lies by then.
    void OnStopped(PiercingProjectile self, Vector3 point, Collider hit) {
        Fade(trail);
        trail = null;
        if (point.y < World.WaterY) { Sink(); return; }
        hold = gameObject.AddComponent<TurnActor>();
        stopTime = Time.time;
        if (hit != null && hit.gameObject.layer == Layers.Ground) transform.position += Vector3.up * GroundLift;
        number = GamblerRoulette.NextNumber();
        ballFrom = Random.Range(0f, 360f);
        float dir = -spinDir;                 // the ball runs against the wheel
        float pocket = GamblerRoulette.PocketAngle(GamblerRoulette.PocketOf(number));
        ballTurn = dir * (BallTurns + Mathf.Repeat(dir * (pocket - ballFrom), 360f));
        ball = GamblerRoulette.AddBall(spin);
        PlaceBall(0f);
        VFX.Trail(ball, new Color(1.6f, 1.6f, 1.6f), 0.06f, 0.1f);
        StartCoroutine(Roll());

        VFX.Sparks(point, GamblerKit.Gold, 16);
        BattleCamera.Shake(0.2f);
        if (BattleCamera.I != null) BattleCamera.I.Frame(new Bounds(new Vector3(point.x, point.y, 0f), new Vector3(12f, 8f, 1f)), 3f);
    }

    // The ball's run: round the track against the wheel, slowing, then skipping over the frets into its pocket, where
    // it rides with the wheel. Its angle is kept relative to the wheel, so it ends exactly in the drawn number's pocket.
    void PlaceBall(float t) {
        float u = Mathf.Clamp01(t / BallRun);
        float angle = (ballFrom + ballTurn * (1f - (1f - u) * (1f - u))) * Mathf.Deg2Rad;
        float drop = Mathf.Clamp01((u - DropAt) / (1f - DropAt));
        float hop = Mathf.Abs(Mathf.Sin(drop * Mathf.PI * 3f)) * (1f - drop);
        float r = Mathf.Lerp(GamblerRoulette.TrackRadius, GamblerRoulette.PocketRadius, Mathf.SmoothStep(0f, 1f, drop * 2f)) + hop * 0.03f;
        ball.localPosition = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, GamblerRoulette.BallZ - hop * 0.04f);
    }

    IEnumerator Roll() {
        GamblerSounds.Play(GamblerSounds.RouletteSpin, transform.position, 0.9f);
        while (Time.time - stopTime < BallRun) {
            if (Gone()) yield break;
            yield return null;
        }
        if (Gone()) yield break;
        Land();
        while (Time.time - stopTime < BallRun + Suspense) {
            if (Gone()) yield break;
            yield return null;
        }
        if (!Gone()) Blast();
    }

    // The turn went on without it (a time rewind or the turn timeout cleared its TurnActor), or it sank: no bet.
    bool Gone() {
        if (hold == null) { Destroy(gameObject); return true; }
        if (transform.position.y < World.WaterY) { Sink(); return true; }
        return false;
    }

    // The ball drops in and the number is called over the wheel.
    void Land() {
        Vector3 at = (ball != null ? ball.position : transform.position) + Vector3.back * 0.1f;
        FxLayers.Flash.Emit(at, Vector3.zero, 0.8f, 0.25f, new Color(1f, 0.95f, 0.8f));
        GamblerKit.Announce(transform.position + Vector3.up * 1.2f, GamblerRoulette.Label(number), GamblerRoulette.LabelColor(number),
                            number == 0 ? 1.6f : 1.25f);
        if (number != 0) return;
        GamblerKit.Sparkle(at, 0.8f);
        GamblerSounds.Play(GamblerSounds.Jackpot, at, 1f);
    }

    // The one blast at the wheel, as its pocket says; then the wheel flies apart.
    void Blast() {
        Vector3 p = transform.position;
        p.z = 0f;
        if (number == 0) Jackpot(p);
        else {
            bool red = GamblerRoulette.IsRed(number);
            float radius = def.radius * radiusBoost;
            Explosion.Create(p, radius, BlastDamage * damageBoost, def.force * (red ? 1f : BlackForce), owner, DamageType.Explosion,
                             red ? BlastStyle.Fire : BlastStyle.Keg);
            if (red) {
                SetAblaze(p, radius);
                VFX.Ring(p, radius, new Color(2.4f, 0.35f, 0.3f), 0.45f, 0.3f);
            } else Soot(p, radius);
        }
        BreakUp();
    }

    // The zero: a huge blast in a burst of golden light, and a shower of gold coins.
    void Jackpot(Vector3 p) {
        float radius = JackpotRadius * radiusBoost;
        Explosion.Create(p, radius, JackpotDamage * damageBoost, JackpotForce, owner, DamageType.Explosion, BlastStyle.Fire);
        VFX.Explosion(p, radius * 0.8f, BlastStyle.Holy);
        GamblerKit.Sparkle(p, 2.5f);
        GoldShower(p);
        VFX.ScreenFlash(new Color(1f, 0.85f, 0.45f, 0.35f), 0.3f);
        TimeFX.SlowMo(0.35f, 0.3f);
    }

    // Red: the enemy's wood (and powder kegs) within the blast catches fire; it spreads by itself (Block.SpreadFire).
    void SetAblaze(Vector3 p, float radius) {
        int n = Physics.OverlapSphereNonAlloc(p, radius, near, Layers.BlockMask, QueryTriggerInteraction.Ignore);
        List<Block> wood = new List<Block>();
        for (int i = 0; i < n; i++) {
            Block b = near[i] != null ? near[i].GetComponent<Block>() : null;
            if (b != null && b.IsAlive && b.def.flammable && !IsOwnTeam(b)) wood.Add(b);
        }
        foreach (Block b in wood) b.Ignite(IgniteSeconds, owner);
    }

    // Black: a pall of dark smoke over the blast and a deeper boom under it.
    static void Soot(Vector3 p, float radius) {
        Vector3 at = GamblerKit.Front(p);
        for (int i = 0; i < 12; i++) {
            Vector3 d = Random.insideUnitCircle;
            FxLayers.Smoke.Emit(at + d * radius * 0.35f, d * radius * 0.9f + Vector3.up * Random.Range(0.6f, 1.6f), radius * Random.Range(0.5f, 0.85f),
                                Random.Range(1.4f, 2.4f), new Color(0.05f, 0.05f, 0.06f, 0.85f), Random.Range(-40f, 40f));
        }
        SFX.Play(SoundId.ExplosionBig, p, 0.55f, 0.7f);
    }

    // Gold coins thrown up out of the jackpot, tumbling down: particles of a layer of their own, so no objects.
    static void GoldShower(Vector3 p) {
        if (coins == null) {
            LayerLook look = FxLooks.Chunk(GamblerKit.GoldMat);
            look.mesh = ModelKit.Primitive(PrimitiveType.Cylinder);
            coins = FxLayer.Create("Gold coins", look, null);
        }
        Vector3 at = GamblerKit.Front(p);
        for (int i = 0; i < 40; i++) {
            float d = Random.Range(0.14f, 0.22f);
            Vector3 v = new Vector3(Random.Range(-6f, 6f), Random.Range(6f, 13f), Random.Range(-1.5f, 0f));
            coins.EmitChunk(at + (Vector3)(Random.insideUnitCircle * 0.6f), v, new Vector3(d, d * 0.07f, d), Random.Range(1.6f, 2.4f));
        }
    }

    // The wheel flies apart: splinters of its rim, chips of its pockets and its turret, gone in a couple of seconds.
    void BreakUp() {
        Vector3 at = spin != null ? spin.position : transform.position;
        Material wood = GamblerRoulette.WoodMat;
        for (int i = 0; i < 9; i++) {
            float a = (i + Random.Range(0f, 0.6f)) * Mathf.PI * 2f / 9f;
            Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            bool rim = i % 3 != 2;
            Vector3 size = rim ? new Vector3(0.25f, 0.07f, 0.1f) : new Vector3(0.12f, 0.08f, 0.05f);
            Material m = rim ? wood : i % 2 == 0 ? GamblerKit.RedInk : GamblerKit.BlackInk;
            Debris.Spawn(at + d * 0.3f, Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f), size, m, 1.5f,
                         d * Random.Range(4f, 8f) + Vector3.up * Random.Range(2f, 5f), Random.insideUnitSphere * 12f, Random.Range(1.2f, 2f));
        }
        Debris.Spawn(at, Random.rotation, new Vector3(0.1f, 0.1f, 0.15f), GamblerKit.GoldMat, 3f, Vector3.up * Random.Range(4f, 7f),
                     Random.insideUnitSphere * 15f, 1.8f);
        Destroy(gameObject);
    }

    // Into the sea: no bet, nothing bursts.
    void Sink() {
        Vector3 p = transform.position;
        VFX.Splash(new Vector3(p.x, World.WaterY, 0f), 0.9f);
        SFX.Play(SoundId.Splash, p, 0.7f);
        if (!impacted) RaiseImpact(p);
        Finish(true);
    }

    // Sparks thrown off the rim where it bites, fanning back and up like a grinder's.
    static void Grind(Vector3 p, Vector3 back) {
        Vector3 at = GamblerKit.Front(p);
        Color hot = new Color(1f, 0.82f, 0.45f);
        for (int i = 0; i < 14; i++) {
            Vector3 v = (back + Vector3.up * 0.7f + (Vector3)(Random.insideUnitCircle * 0.6f)).normalized * Random.Range(5f, 12f);
            FxLayers.Spark.Emit(at, v, Random.Range(0.05f, 0.1f), Random.Range(0.25f, 0.6f), hot);
        }
        FxLayers.Flash.Emit(at, Vector3.zero, 0.7f, 0.08f, hot);
    }

    // A trail stops drawing and fades out where it is.
    static void Fade(GameObject fx) {
        if (fx == null) return;
        foreach (TrailRenderer tr in fx.GetComponentsInChildren<TrailRenderer>()) tr.emitting = false;
        Destroy(fx, 0.4f);
    }
}

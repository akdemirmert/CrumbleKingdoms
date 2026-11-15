using UnityEngine;
using UnityEngine.InputSystem;

// Kamikaze Drone (SoldierDrone): a small olive quadcopter with a warhead strapped under it. It is tossed like any
// throw; SpinUpTime later its four rotors spin up and it flies itself for BatteryTime seconds, with no gravity and
// no wind. It heads for a goal at CruiseSpeed and changes its velocity by at most Accel per second, so it curves
// and banks like a drone; a hard throw keeps its extra speed while the goal is far ahead and loses it gradually.
//  * Human pilot: the goal is the mouse cursor on the battle plane (the drone slows down and hovers on it). Until
//    the mouse is moved the drone flies straight on in the direction it was thrown. A click blows it up.
//  * Bot: the goal is the point the bot aimed at (the enemy commander), reached along a path over the top of his
//    fortress: to a point at a safe height in front of it, on to the point above the target, then a dive onto it.
// It explodes on its first contact with anything that is not its own team (blocks, commanders, the ground). When
// the battery is flat it powers down: gravity returns, the rotors wind down and it tumbles down trailing smoke, to
// blow up where it lands. In the sea it splashes and fizzles out. Its red light blinks faster as the battery
// drains, and it beeps when the battery is nearly flat.
public class SoldierDrone : Projectile {
    // flight
    const float SpinUpTime = 0.35f;          // seconds of plain throw before the rotors take over
    const float BatteryTime = 4f;            // seconds of powered flight
    const float CruiseSpeed = 11f;           // m/s
    const float Accel = 22f;                 // m/s²: the most its velocity can change per second (turning, braking)
    const float Bleed = 4f;                  // m/s²: how fast it loses the extra speed of a hard throw
    const float StopRadius = 2.5f;           // a human's drone slows down this close to the cursor and hovers there
    const float Radius = 0.3f, Mass = 1.2f;
    // human pilot
    const float TakeOverPixels = 30f;        // the mouse takes over once it has moved this far towards the front...
    const float AnyMovePixels = 150f;        // ...or this far in any direction
    // bot path
    const float SafeHeight = 1.5f;           // above the top of the target fortress
    const float MinDiveHeight = 5f;          // above the target itself
    const float EntryOffset = 8f;            // the path reaches the safe height this far before the target
    const float BotStopRadius = 5f;          // it slows down over the target...
    const float DiveReach = 2f;              // ...and dives once this close to the point above it
    const float DiveSpeed = 15f;
    // looks
    const float ArmReach = 0.3f;             // the motors sit this far out along x and z
    const float ViewTilt = -20f;             // the model shows its top to the camera, so the rotors read from the side
    const float MaxBank = 40f;
    const float LeanPerSpeed = 0.6f;         // visual only: it leans into its flight like against air drag
    const float TossBank = 30f;              // its tilt while it is still being thrown
    const float TumbleSpeed = 220f;          // degrees per second once the battery is flat
    const float RotorFast = 2900f, RotorIdle = 400f;   // degrees per second
    const float LowBattery = 0.3f;           // share of the battery left when the beeping starts

    static readonly Color Olive = WeaponDefs.Hex("6A7042"), OliveDark = WeaponDefs.Hex("454B2C"), Black = WeaponDefs.Hex("1E2023"),
                          Steel = WeaponDefs.Hex("8C929A"), Brass = WeaponDefs.Hex("B8904E"), Accent = WeaponDefs.Hex("E5482D"),
                          Lens = WeaponDefs.Hex("5ED0FF");
    static readonly Color BlurColor = new Color(0.86f, 0.9f, 0.94f, 0.22f);
    static readonly Color LightColor = new Color(1f, 0.25f, 0.15f);
    static readonly Color TrailColor = new Color(0.55f, 0.85f, 1f, 0.45f);
    static readonly Color SmokeColor = new Color(0.2f, 0.19f, 0.18f, 0.6f);

    enum Phase { Thrown, Powered, Flat }

    Phase phase;
    float battery = BatteryTime;
    Vector3 heading;                 // straight on: the direction of the throw
    Vector3 accel;                   // the steering's acceleration, smoothed (the model leans into it)
    // human pilot
    bool mouseControl, cursorTookOver;
    Vector2 mouseAtLaunch;
    // bot path
    bool hasTarget;
    int leg;                         // 0 = to the entry point, 1 = to the point above the target, 2 = the dive
    float side;                      // +1 when the target lies to the right of the launch point
    Vector3 target, entry, above;
    // looks
    Transform pivot;
    Transform[] rotors;
    GameObject[] blurs;
    Renderer bulb;
    Material bulbOn, bulbOff;
    Light blinkLight;
    AudioSource buzz;
    float rotorSpeed, bank, yaw, blinkPhase;
    bool blurOn, blinkOn;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        SoldierDrone drone = WeaponFactory.Spawn<SoldierDrone>("Kamikaze drone", owner, def, origin, velocity);
        drone.botActivateX = float.NaN;   // bots fly it onto the target instead (Spawn sets their mid-air hint after OnLaunch)
    }

    // Weapon icon: the drone with its warning light on.
    public static GameObject Model(Transform parent) {
        GameObject root = BuildDrone(parent, out _, out _, out Renderer light);
        light.sharedMaterial = SoldierWeapons.BandMaterial(true);
        return root;
    }

    protected override void OnLaunch(Vector3 velocity) {
        maxLifetime = 15f;   // safety net: the battery is flat after ~4.4 s; even one flown straight up under the space
                             // warp's low gravity is back down in under 12 s
        pivot = ModelKit.Root("Pivot", transform).transform;
        BuildDrone(pivot, out rotors, out blurs, out bulb);
        foreach (GameObject b in blurs) b.SetActive(false);
        bulbOn = SoldierWeapons.BandMaterial(true);
        bulbOff = SoldierWeapons.BandMaterial(false);
        blinkLight = SoldierFx.PointLight(transform, LightColor, 3.5f);
        blinkLight.transform.localPosition = new Vector3(0f, 0.3f, -0.7f);   // in front, so it lights the side we see
        gameObject.AddComponent<SphereCollider>().radius = Radius;
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;   // the model banks, the body does not turn
        VFX.Trail(transform, TrailColor, 0.12f, 0.35f);
        heading = new Vector3(velocity.x, velocity.y, 0f).normalized;
        yaw = travelDir > 0 ? 0f : 180f;
        bank = -travelDir * TossBank;
        mouseControl = owner != null && owner.IsHuman;
        if (mouseControl) {
            if (Mouse.current != null) mouseAtLaunch = Mouse.current.position.ReadValue();
        } else PlanBotPath(WeaponFactory.CurrentShot);
        Pose(0f);
    }

    // Bots: the point the bot aimed at (the enemy commander's chest) and a path to it over the top of his fortress.
    void PlanBotPath(ShotCommand shot) {
        if (shot == null || !shot.fromBot) return;   // no target: the drone flies straight on
        hasTarget = true;
        target = new Vector3(shot.target.x, shot.target.y, 0f);
        // A clumsy bot steers less precisely: its aim error (degrees) becomes a miss of a few metres at most.
        AIProfile ai = owner != null ? owner.setup.ai : null;
        float miss = ai != null ? ai.aimError * 0.3f : 0.5f;   // easy bot about 2.4 m, hard bot about 0.5 m
        target.x += Random.Range(-miss, miss);
        float top = shot.targetFortress != null ? shot.targetFortress.GetBounds().max.y : target.y;
        float safeY = Mathf.Max(target.y + MinDiveHeight, top + SafeHeight);
        side = target.x >= transform.position.x ? 1f : -1f;
        entry = new Vector3(target.x - side * EntryOffset, safeY, 0f);
        above = new Vector3(target.x, safeY, 0f);
    }

    // ------------------------------------------------------------------ flight
    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished) return;
        if (rb.position.y < World.WaterY) { Fizzle(); return; }
        if (phase == Phase.Thrown && age >= SpinUpTime) PowerUp();
        if (phase != Phase.Powered) return;
        float dt = Time.fixedDeltaTime;
        Fly(dt);
        battery -= dt;
        if (battery <= 0f) PowerDown();
    }

    // The rotors bite: from here on the drone flies itself.
    void PowerUp() {
        phase = Phase.Powered;
        rb.useGravity = false;
        gravityScale = 1f;   // with the body's own gravity off, the base class adds none either
        windFactor = 0f;
        buzz = DroneSounds.Loop(DroneSounds.Buzz, transform, 0.45f);
        buzz.pitch = 0.4f;   // winds up to full speed in Update
        DroneSounds.Play(DroneSounds.Beep, rb.position, 0.35f, 0.8f);
        VFX.Dust(rb.position + Vector3.down * 0.4f, 0.5f, new Color(0.9f, 0.92f, 0.95f, 0.35f));   // a puff of downwash
    }

    // This step's goal: the cursor (human), the next point of the path (bot), or else straight on.
    void Fly(float dt) {
        Vector3 p = rb.position;
        p.z = 0f;
        Vector3 goal = p + heading * 50f;
        float speed = CruiseSpeed, stopRadius = 0f;
        if (mouseControl) {
            if (CursorGoal(p, out Vector3 cursor)) { goal = cursor; stopRadius = StopRadius; }
        } else if (hasTarget) {
            goal = BotGoal(p);
            if (leg == 1) stopRadius = BotStopRadius;
            else if (leg == 2) speed = DiveSpeed;
        }
        Steer(p, goal, speed, stopRadius, dt);
    }

    // Turns the velocity towards `speed` straight at the goal, changing it by at most Accel per second, so the drone
    // curves instead of snapping round. Within stopRadius of the goal it slows down to stop on it. Extra speed from
    // the throw is kept while there is still room to brake before the goal, and bleeds off at Bleed.
    void Steer(Vector3 p, Vector3 goal, float speed, float stopRadius, float dt) {
        Vector3 v = rb.linearVelocity;
        Vector3 to = goal - p;
        to.z = 0f;
        float dist = to.magnitude;
        Vector3 dir = dist > 0.01f ? to / dist : Vector3.zero;
        float want = stopRadius > 0f ? speed * Mathf.Clamp01(dist / stopRadius) : speed;
        float along = Vector3.Dot(v, dir);
        if (along > want && dist > along * along / (2f * Accel) + 1.5f) want = Mathf.Max(want, along - Bleed * dt);
        Vector3 next = Vector3.MoveTowards(v, dir * want, Accel * dt);
        next.z = 0f;
        accel = Vector3.Lerp(accel, (next - v) / dt, 0.25f);
        rb.linearVelocity = next;
    }

    // The cursor on the battle plane, once the pilot has taken over. At the throw the mouse is behind the drone (the
    // sling was pulled back), so only a move of TakeOverPixels that puts the cursor in front of it, or a big move
    // anywhere, hands the drone to the cursor; until then it flies straight on by itself.
    bool CursorGoal(Vector3 p, out Vector3 goal) {
        goal = p;
        Mouse mouse = Mouse.current;
        Camera cam = Camera.main;
        if (mouse == null || cam == null) return false;
        Vector2 screen = mouse.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(screen);
        if (Mathf.Abs(ray.direction.z) < 1e-4f) return false;
        float t = -ray.origin.z / ray.direction.z;   // where the ray crosses z = 0
        if (t <= 0f) return false;
        goal = ray.origin + ray.direction * t;
        goal.z = 0f;
        if (!cursorTookOver) {
            float moved = (screen - mouseAtLaunch).magnitude;
            cursorTookOver = moved >= AnyMovePixels || (moved >= TakeOverPixels && Vector3.Dot(goal - p, heading) > 0f);
        }
        return cursorTookOver;
    }

    // The bot's path: the entry point at the safe height in front of the target fortress, the point above the target
    // (slowing down), then the target itself.
    Vector3 BotGoal(Vector3 p) {
        if (leg == 0 && ((p.x - entry.x) * side >= 0f || (entry - p).sqrMagnitude < 9f)) leg = 1;
        if (leg == 1 && (above - p).sqrMagnitude < DiveReach * DiveReach) leg = 2;
        return leg == 0 ? entry : leg == 1 ? above : target;
    }

    // The battery is flat: gravity returns, the motors die with a falling whine and it tumbles down trailing smoke.
    void PowerDown() {
        phase = Phase.Flat;
        rb.useGravity = true;
        gravityScale = 1f;
        accel = Vector3.zero;
        SFX.Stop(buzz);
        buzz = null;
        DroneSounds.Play(DroneSounds.PowerDown, rb.position, 0.6f);
        VFX.Sparks(rb.position, new Color(1f, 0.8f, 0.45f), 8);
        VFX.Emitter(transform, SmokeColor, 0.3f, 18f);
    }

    // ------------------------------------------------------------------ the end
    protected override void OnActivate() { Detonate(transform.position); }   // click: blow up where the player sees it

    void OnCollisionEnter(Collision c) {
        if (finished) return;
        SoldierWeapons.FirstContact(c, rb.position, out Vector3 point, out _);
        SoldierWeapons.LimitShove(c);
        Detonate(point);
    }

    void Detonate(Vector3 point) {
        if (finished) return;
        point.z = 0f;
        if (!impacted) RaiseImpact(point);
        Explosion.Create(point, def.radius, def.damage, def.force, owner, DamageType.Explosion, BlastStyle.Fire);
        VFX.Sparks(point, new Color(1f, 0.65f, 0.3f), 26);   // shrapnel
        End();
    }

    // Into the sea: a splash and a puff of steam, no blast.
    void Fizzle() {
        Vector3 p = rb.position;
        if (!impacted) RaiseImpact(new Vector3(p.x, World.WaterY, 0f));
        SoldierFx.Splash(p, 0.7f);
        VFX.Dust(new Vector3(p.x, World.WaterY + 0.3f, 0f), 0.6f, new Color(0.92f, 0.93f, 0.95f, 0.55f));
        End();
    }

    protected override void OnLost() { End(); }   // left the arena (or the safety timer ran out): nothing to blow up

    void End() {
        SFX.Stop(buzz);
        buzz = null;
        Finish(true);
    }

    protected override void OnDestroy() {
        base.OnDestroy();
        SFX.Stop(buzz);   // removed without End (a time rewind)
    }

    // ------------------------------------------------------------------ looks
    void Update() {
        if (finished) return;
        float dt = Time.deltaTime;
        SpinRotors(dt);
        Pose(dt);
        Blink(dt);
        if (buzz != null) buzz.pitch = Mathf.MoveTowards(buzz.pitch, 1f + 0.25f * Mathf.Clamp01(accel.magnitude / Accel), 2.5f * dt);
    }

    // Quick to spin up, slow to wind down; above 40 % of full speed the propellers show as blur discs.
    void SpinRotors(float dt) {
        float want = phase == Phase.Powered ? RotorFast : phase == Phase.Thrown ? RotorIdle : 0f;
        rotorSpeed = Mathf.MoveTowards(rotorSpeed, want, (phase == Phase.Flat ? 1200f : 9000f) * dt);
        for (int i = 0; i < rotors.Length; i++) rotors[i].Rotate(0f, (i % 2 == 0 ? rotorSpeed : -rotorSpeed) * dt, 0f, Space.Self);
        bool blur = rotorSpeed > RotorFast * 0.4f;
        if (blur == blurOn) return;
        blurOn = blur;
        foreach (GameObject b in blurs) b.SetActive(blur);
    }

    // Leans into its acceleration like a real quadcopter (the thrust points along acceleration + air drag + gravity),
    // turns to face where it is going, and tumbles once the battery is flat.
    void Pose(float dt) {
        Vector3 v = Velocity;
        if (Mathf.Abs(v.x) > 0.5f) yaw = Mathf.MoveTowardsAngle(yaw, v.x > 0f ? 0f : 180f, 400f * dt);
        if (phase == Phase.Flat) bank -= travelDir * TumbleSpeed * dt;
        else if (phase == Phase.Powered) {
            Vector3 thrust = accel + v * LeanPerSpeed + Vector3.up * World.Gravity;
            float lean = Mathf.Clamp(-Mathf.Atan2(thrust.x, thrust.y) * Mathf.Rad2Deg, -MaxBank, MaxBank);
            bank = Mathf.LerpAngle(bank, lean, 1f - Mathf.Exp(-8f * dt));
        }
        pivot.localRotation = Quaternion.Euler(ViewTilt, 0f, 0f) * Quaternion.Euler(0f, 0f, bank) * Quaternion.Euler(0f, yaw, 0f);
    }

    // The warning light blinks faster as the battery drains (1.5 Hz -> 10 Hz) and beeps with it once the battery is
    // nearly flat; a flat battery puts it out.
    void Blink(float dt) {
        float used = 1f - battery / BatteryTime;
        blinkPhase += dt * Mathf.Lerp(1.5f, 10f, used * used);
        bool on = phase != Phase.Flat && blinkPhase % 1f < 0.5f;
        blinkLight.intensity = on ? 3f : 0f;
        if (on == blinkOn) return;
        blinkOn = on;
        bulb.sharedMaterial = on ? bulbOn : bulbOff;
        if (on && phase == Phase.Powered && battery < BatteryTime * LowBattery) DroneSounds.Play(DroneSounds.Beep, transform.position, 0.3f);
    }

    // ------------------------------------------------------------------ model
    // The quadcopter, +X = forward, about 1 m across its rotors: an olive body with a battery pack and a camera under
    // the nose (its lens in the weapon's colour), four black arms out to steel motors with two-bladed propellers (each
    // with a pale blur disc for when it spins fast), an olive warhead with a red band and a brass fuse strapped
    // underneath, and an antenna carrying the red warning light. Returns the parts the projectile animates.
    static GameObject BuildDrone(Transform parent, out Transform[] rotors, out GameObject[] blurs, out Renderer bulb) {
        GameObject root = ModelKit.Root("Kamikaze drone", parent);
        Transform r = root.transform;
        Material olive = Mats.Lit(Olive, 0.3f), dark = Mats.Lit(OliveDark, 0.3f), black = Mats.Lit(Black, 0.35f);
        Material steel = Mats.Lit(Steel, 0.6f, 0.4f), brass = Mats.Lit(Brass, 0.55f, 0.4f), red = Mats.Lit(Accent, 0.35f);
        Material lens = Mats.Emissive(Lens * 0.6f, new Color(0.45f, 1.3f, 2f));
        Material blur = Mats.Transparent(BlurColor, 0.2f);

        ModelKit.Part(r, PrimitiveType.Cube, Vector3.zero, new Vector3(0.4f, 0.11f, 0.24f), olive);                     // body
        ModelKit.Part(r, PrimitiveType.Cube, new Vector3(-0.03f, 0.075f, 0f), new Vector3(0.26f, 0.05f, 0.17f), dark);   // battery pack
        ModelKit.Part(r, PrimitiveType.Sphere, new Vector3(0.2f, -0.04f, 0f), Vector3.one * 0.09f, black);              // camera
        ModelKit.Part(r, PrimitiveType.Sphere, new Vector3(0.24f, -0.045f, 0f), Vector3.one * 0.04f, lens);

        rotors = new Transform[4];
        blurs = new GameObject[4];
        for (int i = 0; i < 4; i++) {
            // corners in turn round the body, so rotors 0 & 2 and 1 & 3 are diagonal pairs (they spin the same way)
            float sx = i < 2 ? 1f : -1f, sz = i == 0 || i == 3 ? 1f : -1f;
            Vector3 m = new Vector3(sx * ArmReach, 0f, sz * ArmReach);
            ModelKit.Part(r, PrimitiveType.Cube, m * 0.5f + Vector3.up * 0.02f, new Vector3(m.magnitude, 0.03f, 0.045f), black,
                          new Vector3(0f, Mathf.Atan2(-m.z, m.x) * Mathf.Rad2Deg, 0f));                                    // arm
            ModelKit.Part(r, PrimitiveType.Cylinder, m + Vector3.up * 0.05f, new Vector3(0.075f, 0.035f, 0.075f), steel); // motor
            Transform rotor = ModelKit.Root("Rotor", r).transform;
            rotor.localPosition = m + Vector3.up * 0.1f;
            rotor.localRotation = Quaternion.Euler(0f, 25f + i * 50f, 0f);
            ModelKit.Part(rotor, PrimitiveType.Cube, Vector3.zero, new Vector3(0.4f, 0.008f, 0.045f), black);            // two blades
            blurs[i] = ModelKit.Part(rotor, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.42f, 0.002f, 0.42f), blur);
            rotors[i] = rotor;
        }

        Vector3 w = new Vector3(0.02f, -0.12f, 0f);   // the warhead's middle
        Vector3 lying = new Vector3(0f, 0f, 90f);     // a cylinder along x
        ModelKit.Part(r, PrimitiveType.Cylinder, w, new Vector3(0.11f, 0.15f, 0.11f), dark, lying);
        ModelKit.Part(r, PrimitiveType.Cylinder, w + new Vector3(0.05f, 0f, 0f), new Vector3(0.116f, 0.02f, 0.116f), red, lying);
        ModelKit.MeshPart(r, MeshKit.Cone(10, 0.055f, 0.13f), w + new Vector3(0.15f, 0f, 0f), Vector3.one, dark, new Vector3(0f, 0f, -90f), "Nose");
        ModelKit.Part(r, PrimitiveType.Cylinder, w + new Vector3(0.29f, 0f, 0f), new Vector3(0.03f, 0.015f, 0.03f), brass, lying);   // fuse
        for (int s = -1; s <= 1; s += 2)
            ModelKit.Part(r, PrimitiveType.Cube, new Vector3(w.x + s * 0.09f, -0.085f, 0f), new Vector3(0.025f, 0.07f, 0.125f), black);   // straps

        ModelKit.Part(r, PrimitiveType.Cylinder, new Vector3(-0.17f, 0.17f, 0f), new Vector3(0.014f, 0.09f, 0.014f), black, new Vector3(0f, 0f, 20f));
        bulb = ModelKit.Part(r, PrimitiveType.Sphere, new Vector3(-0.2f, 0.26f, 0f), Vector3.one * 0.06f, SoldierWeapons.BandMaterial(false))
            .GetComponent<Renderer>();
        return root;
    }
}

// The drone's own sounds, synthesised the first time each is needed and kept for the rest of the game (Synth; see
// SoundBank for the house recipes): the motor buzz (looped), the warning beep and the power-down whine.
static class DroneSounds {
    static AudioClip buzz, beep, powerDown;

    public static AudioClip Buzz => buzz != null ? buzz : buzz = Make("Drone buzz", BuzzData());
    public static AudioClip Beep => beep != null ? beep : beep = Make("Drone beep", BeepData());
    public static AudioClip PowerDown => powerDown != null ? powerDown : powerDown = Make("Drone power down", PowerDownData());

    public static void Play(AudioClip clip, Vector3 pos, float volume, float pitch = 1f) { SFX.PlayClip(clip, pos, volume, pitch); }

    // A looping clip that follows a transform; stop it with SFX.Stop (it also fades out by itself if the transform goes).
    public static AudioSource Loop(AudioClip clip, Transform follow, float volume) {
        GameObject go = new GameObject("[Loop] " + clip.name);
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        go.AddComponent<LoopVoice>().Init(follow, volume);
        source.Play();
        return source;
    }

    static AudioClip Make(string name, float[] data) {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Synth.Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Four small motors: two slightly detuned buzzing saws beating against each other, their octave, the thin whine
    // of the electronics and the hiss of the propellers, fluttering with the blades. Every frequency (and vibrato and
    // flutter rate) fits a whole number of cycles into the 1.2 s loop, so it repeats without a seam.
    static float[] BuzzData() => new Synth(1.4f, 301)
        .Tone(0f, 1.4f, 187.5f, 0.32f, Env.Flat, Waveform.Saw, 2.5f, 0.004f, Filter.Low(1900f, 0.9f))
        .Tone(0f, 1.4f, 192.5f, 0.28f, Env.Flat, Waveform.Saw, 5f, 0.004f, Filter.Low(1900f, 0.9f))
        .Tone(0f, 1.4f, 375f, 0.12f, Env.Flat, Waveform.Sine)
        .Tone(0f, 1.4f, 1150f, 0.05f, Env.Flat, Waveform.Sine, 5f, 0.008f)
        .Noise(0f, 1.4f, 0.14f, Env.Flat, Filter.Band(800f, 1.1f))
        .Tremolo(20f, 0.18f)
        .DoneLoop(0.2f, 0.8f);

    // A short electronic beep.
    static float[] BeepData() => new Synth(0.1f, 302)
        .Tone(0f, 0.075f, 2300f, 0.4f, Env.Gate(0.002f, 0.015f), Waveform.Square, 0f, 0f, Filter.Low(5000f))
        .Tone(0f, 0.075f, 2300f, 0.5f, Env.Gate(0.002f, 0.015f), Waveform.Sine)
        .Done(0.7f);

    // The motors dying: the buzz sliding down two octaves, a whine falling with it and the crackle of a short circuit.
    static float[] PowerDownData() => new Synth(0.8f, 303)
        .Tone(0f, 0.75f, new Sweep(190f, 45f), 0.5f, Env.Hit(0.005f, 0.28f), Waveform.Saw, 0f, 0f, Filter.Low(new Sweep(1800f, 300f), 0.9f))
        .Tone(0f, 0.6f, new Sweep(1150f, 200f), 0.12f, Env.Hit(0.005f, 0.2f), Waveform.Sine)
        .Crackle(0f, 0.2f, 8, 0.35f, 2000f, 6500f, 1.5f, 0.004f)
        .Done(0.8f);
}

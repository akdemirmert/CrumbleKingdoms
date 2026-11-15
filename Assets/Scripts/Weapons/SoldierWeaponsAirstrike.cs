using System.Collections;
using UnityEngine;

// Airstrike: the soldier throws a red smoke flare that tumbles end over end, trailing red smoke. On its first
// contact (the ground, or a block or commander of the other team; also if it ever comes to rest without one) it
// stops and stands where it fell, pouring out a thick column of red smoke with a red glow, and calls in the strike.
// About 0.6 s later a twin-engine bomber (AirstrikeBomber) flies in level from the thrower's side, about 14 m above
// the highest block near the flare (at least 10 m above the flare, and clear of everything else on its way), and
// drops a stick of five bombs (AirstrikeBomb) across the flare, 2.4 m apart and centred on it. Each bomb whistles
// down and explodes on its first contact; bombs that reach the sea only splash.
// The flare holds the turn until the bomber has gone; bombs still falling hold it themselves. If what the flare
// stands on is blown away, it drops onto whatever is below. A flare that falls into the sea goes out in a puff of
// red smoke and a splash, and no bomber comes.
public class AirstrikeFlare : Projectile {
    const float Radius = 0.12f, Mass = 0.8f;
    const float Tumble = 620f;                       // deg/s end over end in flight
    const float RestSpeed = 0.3f, RestTime = 0.4f;   // this slow for this long in flight: it has come to rest
    const float Lean = 18f;                          // a planted flare leans this far off vertical, the way it flew
    const float Rest = 0.22f;                        // its centre stands this high over the surface (the butt sinks in)
    const float CallDelay = 0.6f;                    // landing -> the bomber sets out
    const float StrikeTimeout = 10f;                 // safety net after landing: a strike takes about 4 s
    const float BurnDown = 3.6f;                     // the smoke thins away for this long after the bomber has gone
    const float FrameHalfWidth = 14f;                // the camera shows at least this much either side of the flare
    const float SparkEvery = 0.05f, GlowEvery = 0.08f;
    static readonly Color FlareLight = new Color(1f, 0.3f, 0.16f), SparkColor = new Color(1f, 0.62f, 0.4f);
    static readonly Color GlowColor = new Color(1f, 0.28f, 0.16f), Steam = new Color(0.92f, 0.93f, 0.95f, 0.55f);

    enum Phase { Flying, Burning, Spent }

    Phase phase;
    Transform model, tip;
    ParticleSystem smoke;
    Light glow;
    AudioSource hiss;
    AirstrikeBomber bomber;
    bool bomberSent, doused;
    float restClock, fallSpeed, sparkClock, glowClock, glowLevel, spentTime, lightSeed;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        SFX.Play(SoundId.FireIgnite, origin, 0.45f, 1.6f);   // the striker cap: the flare catches
        WeaponFactory.Spawn<AirstrikeFlare>("Smoke flare", owner, def, origin, velocity);
    }

    // Weapon icon: the bomber, scaled down to about a metre.
    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Airstrike", parent);
        AirstrikeKit.Bomber(root.transform, out _, out _, out _).transform.localScale = Vector3.one * 0.12f;
        return root;
    }

    protected override void OnLaunch(Vector3 velocity) {
        model = AirstrikeKit.Flare(transform, out tip).transform;
        model.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
        gameObject.AddComponent<SphereCollider>().radius = Radius;
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;   // only the model tumbles
        smoke = AirstrikeKit.RedSmoke(transform, tip.position);
        glow = SoldierFx.PointLight(transform, FlareLight, 4f);
        lightSeed = Random.Range(0f, 100f);
        hiss = SFX.PlayLoop(SoundId.Fuse, transform, 0.35f);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished || phase != Phase.Flying) return;
        Vector3 p = transform.position;
        if (p.y < World.WaterY) { Fizzle(); return; }
        // Resting means slow with something right under it (not the slow apex of a steep throw in low gravity).
        bool resting = rb.linearVelocity.sqrMagnitude < RestSpeed * RestSpeed &&
                       Physics.Raycast(p, Vector3.down, Radius + 0.1f, Layers.SolidMask, QueryTriggerInteraction.Ignore);
        restClock = resting ? restClock + Time.fixedDeltaTime : 0f;
        if (restClock >= RestTime) Land(p + Vector3.down * Radius, Vector3.up);
    }

    void OnCollisionEnter(Collision c) {
        if (finished || phase != Phase.Flying) return;
        SoldierWeapons.FirstContact(c, transform.position, out Vector3 point, out Vector3 normal);
        SoldierWeapons.LimitShove(c);
        Land(point, normal);
    }

    void Update() {
        float dt = Time.deltaTime;
        switch (phase) {
            case Phase.Flying:
                if (finished) return;
                model.Rotate(0f, 0f, -travelDir * Tumble * dt, Space.Self);
                AirstrikeSounds.WarmUp();
                break;
            case Phase.Burning:
                if (!doused) Settle(dt);
                if (bomberSent && bomber == null) EndStrike();   // the bomber has left
                break;
            case Phase.Spent:
                spentTime += dt;
                model.localScale = Vector3.one * Mathf.Clamp01((BurnDown - spentTime) / 0.6f);   // burnt down to nothing
                break;
        }
        Burn(dt);
    }

    // ------------------------------------------------------------------ landing and the strike
    // Down it goes: the flare stands where it touched (placed by hand from now on: no more physics, no more
    // contacts), the smoke turns into a column and the strike is called in.
    void Land(Vector3 point, Vector3 normal) {
        point.z = 0f;
        if (point.y < World.WaterY + 0.1f) { Fizzle(); return; }   // it struck the foot of a cliff at the waterline
        phase = Phase.Burning;
        RaiseImpact(point);
        cameraFollows = false;
        maxLifetime = age + StrikeTimeout;
        rb.linearVelocity = Vector3.zero;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;   // the only mode kinematic bodies support
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        foreach (Collider c in GetComponents<Collider>()) c.enabled = false;
        normal.z = 0f;
        transform.position = point + (normal.sqrMagnitude > 1e-4f ? normal.normalized : Vector3.up) * Radius;
        model.localRotation = Quaternion.Euler(0f, 0f, 90f - travelDir * Lean);
        Settle(0f);
        AirstrikeKit.Column(smoke);
        SFX.Play(SoundId.Bounce, point, 0.35f, 1.4f);                 // the canister clatters down
        VFX.Dust(point, 0.35f, SoldierFx.GroundDust);
        AirstrikeSounds.Play(AirstrikeSounds.Radio, point, 0.5f);    // the strike is called in
        StartCoroutine(CallIn(point));
    }

    // The bombing run comes from the thrower's side, straight across the flare. The camera shows the flare, the sky
    // the bomber will cross and the ground below.
    IEnumerator CallIn(Vector3 spot) {
        int dir = ApproachDirection(spot.x);
        float altitude = AirstrikeBomber.Altitude(spot, dir);
        if (BattleCamera.I != null) {
            Bounds b = new Bounds(spot, Vector3.zero);
            b.Encapsulate(new Vector3(spot.x - FrameHalfWidth, altitude + 2f, 0f));
            b.Encapsulate(new Vector3(spot.x + FrameHalfWidth, Mathf.Min(spot.y, AirstrikeBomber.GroundBelow(spot.x)) - 0.5f, 0f));
            BattleCamera.I.Frame(b, 3f);
        }
        yield return new WaitForSeconds(CallDelay);
        bomber = AirstrikeBomber.Launch(this, spot.x, altitude, dir);
        bomberSent = true;
    }

    int ApproachDirection(float x) {
        if (owner == null) return travelDir;
        float home = owner.CommanderAlive ? owner.commander.transform.position.x : owner.transform.position.x;
        if (Mathf.Abs(x - home) < 0.5f) return owner.Facing;
        return x > home ? 1 : -1;
    }

    // The flare stands where it landed. If nothing holds it up (it struck a wall, or its block was blown away), it
    // drops onto whatever is below; in the sea it goes out.
    void Settle(float dt) {
        Vector3 p = transform.position;
        float step = (fallSpeed + World.Gravity * dt) * dt;
        if (Physics.Raycast(p + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 0.2f + Rest + step + 0.02f,
                            Layers.SolidMask, QueryTriggerInteraction.Ignore)) {
            fallSpeed = 0f;
            p.y = hit.point.y + Rest;
        } else {
            fallSpeed += World.Gravity * dt;
            p.y -= fallSpeed * dt;
        }
        transform.position = p;
        if (p.y < World.WaterY) Douse();
    }

    // Its stand was blown away over the sea: the flare drops in and goes out. The strike already called keeps coming.
    void Douse() {
        doused = true;
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);
        SoldierFx.Splash(p, 0.5f);
        VFX.Dust(p + Vector3.up * 0.3f, 0.6f, Steam);
        StopBurning();
        model.gameObject.SetActive(false);
    }

    // The bomber has gone (or, as a safety net, the strike took far too long): the flare burns down and its smoke
    // thins away, and the bombs still falling hold the turn.
    void EndStrike() {
        if (finished) return;
        StopAllCoroutines();
        if (bomber != null) Destroy(bomber.gameObject);
        bomber = null;
        phase = Phase.Spent;
        StopBurning();
        Finish(true, BurnDown);
    }

    // Off the map or too long in the air: end quietly. After landing: the strike's safety net.
    protected override void OnLost() {
        if (phase == Phase.Flying) End();
        else EndStrike();
    }

    // Into the sea before it landed anywhere: a puff of red smoke and a splash. No airstrike.
    void Fizzle() {
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);
        if (!impacted) RaiseImpact(p);
        SoldierFx.Splash(p, 0.6f);
        AirstrikeKit.Puff(p + Vector3.up * 0.4f);
        End();
    }

    // A flare that never landed: its trail fades out on its own.
    void End() {
        StopBurning();
        if (smoke != null) SoldierFx.Release(smoke.gameObject, 2f);
        smoke = null;
        Finish(true);
    }

    void StopBurning() {
        if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        SFX.Stop(hiss);
        hiss = null;
    }

    // The burning head: the smoke pours from it, it lights its surroundings in red and, once planted, it sputters
    // sparks and glares. Smoke and glare on the ground are drawn just in front of the blocks so walls never cut them.
    void Burn(float dt) {
        Vector3 head = tip.position;
        Vector3 front = new Vector3(head.x, head.y, VFX.FrontZ);
        bool lit = phase == Phase.Flying || (phase == Phase.Burning && !doused);
        if (smoke != null) smoke.transform.position = phase == Phase.Flying ? head : front;
        if (glow != null) {
            glowLevel = Mathf.MoveTowards(glowLevel, lit ? (phase == Phase.Flying ? 2.2f : 4.5f) : 0f, dt * 6f);
            glow.transform.position = head + Vector3.back * 1.2f;
            glow.range = phase == Phase.Flying ? 4f : 7f;
            glow.intensity = glowLevel * (0.7f + 0.5f * Mathf.PerlinNoise(lightSeed, Time.time * 9f));
        }
        if (phase != Phase.Burning || doused) return;
        for (sparkClock += dt; sparkClock >= SparkEvery; sparkClock -= SparkEvery) {
            Vector3 v = new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(2f, 4.5f), Random.Range(-0.5f, 0f));
            FxLayers.Spark.Emit(front, v, Random.Range(0.04f, 0.08f), Random.Range(0.25f, 0.5f), SparkColor);
        }
        glowClock += dt;
        if (glowClock >= GlowEvery) {
            glowClock = 0f;
            FxLayers.Glow.Emit(front, Vector3.zero, Random.Range(1.2f, 1.8f), 0.24f, GlowColor);
        }
    }

    protected override void OnDestroy() {
        base.OnDestroy();
        SFX.Stop(hiss);
    }
}

// The bomber of an airstrike (see AirstrikeFlare). It appears Approach metres before the flare on the thrower's side,
// flies level across it at Speed, drops the stick as its bomb bay passes each release point, climbs away and removes
// itself Exit metres past the flare. It is visual only (no colliders) and lives under its flare, so a time rewind that
// clears the flare clears it too. Its engine drone (an AudioSource of its own) fades in from afar, drops in pitch as
// the bomber passes the middle of the view and fades out before it is removed.
public class AirstrikeBomber : MonoBehaviour {
    const float Speed = 28f;                     // m/s
    const float Approach = 45f, Exit = 45f;      // from the flare's x: where it appears and where it is removed
    const float Height = 14f, MinHeight = 10f;   // over the highest block near the flare / over the flare itself
    const float NearFlare = 9f;                  // blocks this close to the flare (in x) set the height
    const float Clearance = 4f;                  // over everything else on its way
    const int Bombs = 5;
    const float Spacing = 2.4f;
    const float Roll = 14f;                      // banked a little towards the camera, so the wings read
    const float PropSpin = 1500f;                // deg/s
    const float ClimbSpeed = 6f, ClimbAccel = 7f;
    const float EngineVolume = 0.75f, FadeIn = 1.2f, FadeOutDistance = 14f, Doppler = 0.08f;

    readonly float[] releaseX = new float[Bombs];
    AirstrikeFlare flare;
    Transform[] props;
    Transform bay;
    AudioSource engines;
    int dir, dropped;
    float x, y, climb, t, endX, maxTime, fade;

    public static AirstrikeBomber Launch(AirstrikeFlare flare, float targetX, float altitude, int dir) {
        GameObject go = new GameObject("Bomber");
        go.transform.SetParent(flare.transform, false);
        AirstrikeBomber b = go.AddComponent<AirstrikeBomber>();
        b.Init(flare, targetX, altitude, dir);
        return b;
    }

    void Init(AirstrikeFlare flare, float targetX, float altitude, int dir) {
        this.flare = flare;
        this.dir = dir;
        x = targetX - dir * Approach;
        y = altitude;
        endX = targetX + dir * Exit;
        maxTime = (Approach + Exit) / Speed + 3f;
        // Centred on the flare; each bomb is released Lead early, so it lands on its mark.
        for (int k = 0; k < Bombs; k++)
            releaseX[k] = targetX + dir * ((k - (Bombs - 1) * 0.5f) * Spacing - AirstrikeBomb.Lead);
        Place();                                 // in position before anything that trails behind it is made
        AirstrikeKit.Bomber(transform, out props, out bay, out Transform[] exhausts);
        foreach (Transform e in exhausts) AirstrikeKit.Exhaust(e);
        GameObject sound = ModelKit.Root("Engines", transform);
        engines = sound.AddComponent<AudioSource>();
        engines.clip = AirstrikeSounds.Engines;
        engines.loop = true;
        engines.playOnAwake = false;
        engines.spatialBlend = 0f;
        engines.volume = 0f;
        engines.Play();
    }

    // Level flight Height over the highest block near the flare, at least MinHeight over the flare, and Clearance over
    // everything else between where the bomber appears and where it leaves (fortresses, commanders, the islands).
    public static float Altitude(Vector3 spot, int dir) {
        float a = spot.x - dir * Approach, b = spot.x + dir * Exit;
        float lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
        float near = spot.y, path = World.WaterY;
        BattleManager bm = BattleManager.I;
        if (bm != null)
            foreach (Fortress f in bm.fortresses) {
                if (f == null) continue;
                foreach (Block block in f.blocks) {
                    if (block == null || !block.IsAlive || block.rend == null) continue;
                    Bounds bb = block.rend.bounds;
                    if (bb.max.x < lo || bb.min.x > hi) continue;
                    path = Mathf.Max(path, bb.max.y);
                    if (bb.max.x >= spot.x - NearFlare && bb.min.x <= spot.x + NearFlare) near = Mathf.Max(near, bb.max.y);
                }
                if (f.CommanderAlive) {
                    Vector3 c = f.commander.transform.position;
                    if (c.x >= lo && c.x <= hi) path = Mathf.Max(path, c.y + 2f);
                }
            }
        for (float sx = lo; sx <= hi; sx += 4f) path = Mathf.Max(path, GroundBelow(sx));
        return Mathf.Max(near + Height, spot.y + MinHeight, path + Clearance);
    }

    // The top of the island under x, else the sea.
    public static float GroundBelow(float x) {
        return Physics.Raycast(new Vector3(x, 200f, 0f), Vector3.down, out RaycastHit hit, 400f, Layers.GroundMask, QueryTriggerInteraction.Ignore)
            ? hit.point.y : World.WaterY;
    }

    void Update() {
        float dt = Time.deltaTime;
        t += dt;
        x += dir * Speed * dt;
        if (dropped >= Bombs) {                  // the stick is gone: climb away
            climb = Mathf.MoveTowards(climb, ClimbSpeed, ClimbAccel * dt);
            y += climb * dt;
        }
        Place();
        foreach (Transform p in props) p.Rotate(PropSpin * dt, 0f, 0f, Space.Self);
        while (dropped < Bombs && (bay.position.x - releaseX[dropped]) * dir >= 0f) Drop();
        Engines(dt);
        if ((x - endX) * dir >= 0f || t > maxTime) Destroy(gameObject);
    }

    // Nose along the flight (pitched up while it climbs), banked towards the camera, bobbing and swaying a little.
    void Place() {
        transform.position = new Vector3(x, y + Mathf.Sin(t * 1.7f) * 0.12f, 0f);
        float pitch = Mathf.Atan2(climb, Speed) * Mathf.Rad2Deg + Mathf.Sin(t * 1.1f) * 0.8f;
        float roll = -dir * Roll + Mathf.Sin(t * 1.3f) * 2f;
        transform.rotation = Quaternion.Euler(0f, dir > 0 ? 0f : 180f, 0f) * Quaternion.AngleAxis(pitch, Vector3.forward) *
                             Quaternion.AngleAxis(roll, Vector3.right);
    }

    // The next bomb leaves the bay at its release point (exactly there, even if a slow frame carried the bomber past).
    void Drop() {
        float rx = releaseX[dropped++];
        if (flare == null) return;
        Vector3 p = new Vector3(rx, bay.position.y, 0f);
        WeaponFactory.Spawn<AirstrikeBomb>("Airstrike bomb", flare.owner, flare.def, p, new Vector3(dir * AirstrikeBomb.DropSpeed, -1.5f, 0f));
    }

    // Panned and faded like every world sound (SFX.Spatialize); higher while the bomber comes towards the middle of
    // the view, lower once it has passed; deeper in slow motion; paused with the game.
    void Engines(float dt) {
        if (engines == null) return;
        if (TimeFX.Paused) {
            if (engines.isPlaying) engines.Pause();
            return;
        }
        if (!engines.isPlaying) engines.UnPause();
        fade = Mathf.MoveTowards(fade, 1f, dt / FadeIn);
        float leaving = Mathf.Clamp01((endX - x) * dir / FadeOutDistance);
        SFX.Spatialize(transform.position, out float pan, out float attenuation);
        engines.panStereo = pan;
        engines.volume = EngineVolume * fade * leaving * attenuation * SFX.Volume;
        Camera cam = Camera.main;
        float toward = cam != null ? Mathf.Clamp((cam.transform.position.x - x) * dir / 18f, -1f, 1f) : 0f;
        float slow = Time.timeScale > 0f && Time.timeScale < 1f ? Mathf.Lerp(0.8f, 1f, Time.timeScale) : 1f;
        engines.pitch = (1f + Doppler * toward) * slow;
    }

    void OnDestroy() {
        if (engines != null) engines.Stop();
    }
}

// One bomb of the stick: a finned bomb that whistles down under gravity (the wind does not move it), turning nose
// first, and explodes on its first contact. It leaves the bomber with a little forward speed that the air takes off
// at once; Lead is how far that carries it, so the bomber releases it that much early. In the sea it only splashes.
public class AirstrikeBomb : Projectile {
    public const float DropSpeed = 5f, AirBrake = 5f;   // m/s forward at release; 1/s horizontal drag
    public const float Lead = DropSpeed / AirBrake;     // metres
    const float Radius = 0.22f, Mass = 2.5f;            // the collider sits round the nose (the model hangs behind it)
    const float Spin = 160f;                            // deg/s around its own axis, from the fins

    Transform model;
    float wobble;

    protected override void OnLaunch(Vector3 velocity) {
        windFactor = 0f;
        maxLifetime = 8f;
        cameraFollows = false;                          // the camera stays on the target area
        model = AirstrikeKit.Bomb(transform).transform;
        gameObject.AddComponent<SphereCollider>().radius = Radius;
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;   // only the model turns
        wobble = Random.Range(0f, 10f);
        Turn();
        VFX.Trail(transform, new Color(1f, 1f, 1f, 0.22f), 0.12f, 0.18f);   // a faint streak of air
        AirstrikeSounds.Play(AirstrikeSounds.Whistle, transform.position, 0.4f, Random.Range(0.93f, 1.08f));
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished) return;
        rb.AddForce(new Vector3(-rb.linearVelocity.x * AirBrake, 0f, 0f), ForceMode.Acceleration);
        if (transform.position.y < World.WaterY) Splash();
    }

    void Update() {
        if (!finished) Turn();
    }

    // Nose along the flight, with a slight flutter and the slow spin of the fins.
    void Turn() {
        Vector3 v = Velocity;
        if (v.sqrMagnitude < 0.01f) return;
        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg + Mathf.Sin((age + wobble) * 11f) * 3f;
        model.rotation = Quaternion.Euler(0f, 0f, angle) * Quaternion.Euler(age * Spin, 0f, 0f);
    }

    void OnCollisionEnter(Collision c) {
        if (finished) return;
        SoldierWeapons.FirstContact(c, transform.position, out Vector3 p, out _);
        SoldierWeapons.LimitShove(c);
        p.z = 0f;
        if (!impacted) RaiseImpact(p);
        Explosion.Create(p, def.radius, def.damage, def.force, owner, DamageType.Explosion, BlastStyle.Fire);
        Finish(true);
    }

    void Splash() {
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);
        if (!impacted) RaiseImpact(p);
        SoldierFx.Splash(p, 1.4f);
        Finish(true);
    }

    protected override void OnLost() { Finish(true); }
}

// Models and effects of the airstrike: the smoke flare, the bomber and its bombs (visual only, +X = forward), the
// red signal smoke, the engines' exhaust and a puff of red smoke.
static class AirstrikeKit {
    static readonly Color Olive = new Color(0.34f, 0.38f, 0.21f), OliveDark = new Color(0.26f, 0.29f, 0.17f);
    static readonly Color Steel = new Color(0.43f, 0.45f, 0.48f), Black = new Color(0.07f, 0.07f, 0.08f);
    static readonly Color Glass = new Color(0.15f, 0.21f, 0.27f), White = new Color(0.9f, 0.89f, 0.84f);
    static readonly Color Brass = WeaponDefs.Hex("B8904E"), Accent = WeaponDefs.Hex("C9281F"), Orange = WeaponDefs.Hex("F08A24");
    static readonly Color FlareRed = WeaponDefs.Hex("D42A1F");
    static readonly Color SmokeRed = new Color(0.93f, 0.17f, 0.12f, 0.85f), SmokePale = new Color(1f, 0.4f, 0.32f, 0.7f);
    static readonly Color ExhaustGrey = new Color(0.32f, 0.31f, 0.3f, 0.35f);

    // ------------------------------------------------------------------ models
    // The smoke flare: a red canister about 0.65 m long with a black end cap, a white label band, a steel collar and
    // the glowing striker head at the front (`tip`, where the smoke comes out).
    public static GameObject Flare(Transform parent, out Transform tip) {
        GameObject root = ModelKit.Root("Smoke flare", parent);
        Transform r = root.transform;
        Vector3 along = new Vector3(0f, 0f, -90f);                       // primitive cylinders lie along +X
        ModelKit.Part(r, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.17f, 0.26f, 0.17f), Mats.Lit(FlareRed, 0.45f), along);
        ModelKit.Part(r, PrimitiveType.Cylinder, new Vector3(-0.06f, 0f, 0f), new Vector3(0.176f, 0.055f, 0.176f), Mats.Lit(White, 0.3f), along);
        ModelKit.Part(r, PrimitiveType.Cylinder, new Vector3(-0.285f, 0f, 0f), new Vector3(0.19f, 0.035f, 0.19f), Mats.Lit(Black, 0.35f), along);
        ModelKit.Part(r, PrimitiveType.Cylinder, new Vector3(0.27f, 0f, 0f), new Vector3(0.15f, 0.02f, 0.15f), Mats.Lit(Steel, 0.6f, 0.6f), along);
        tip = ModelKit.Part(r, PrimitiveType.Sphere, new Vector3(0.3f, 0f, 0f), new Vector3(0.1f, 0.13f, 0.13f),
                            Mats.Emissive(new Color(1f, 0.45f, 0.3f), new Color(7f, 1.5f, 0.6f))).transform;
        return root;
    }

    // A finned bomb about a metre long, its nose near the origin and its body behind it: olive drab with a red band,
    // a brass fuze and a cross of steel fins.
    public static GameObject Bomb(Transform parent) {
        GameObject root = ModelKit.Root("Bomb", parent);
        Transform r = root.transform;
        Material body = Mats.Lit(OliveDark, 0.35f), fins = Mats.Lit(Steel, 0.5f, 0.5f);
        Section(r, 0.03f, 0.15f, 0.05f, 0.2f, body);                          // nose
        Section(r, -0.02f, 0.156f, 0.156f, 0.05f, Mats.Lit(Accent, 0.4f));    // band
        Section(r, -0.37f, 0.15f, 0.15f, 0.4f, body);                         // body
        Section(r, -0.37f, 0.15f, 0.06f, 0.26f, body, true);                  // tail cone
        ModelKit.Part(r, PrimitiveType.Sphere, new Vector3(0.235f, 0f, 0f), Vector3.one * 0.08f, Mats.Lit(Brass, 0.55f, 0.6f));   // fuze
        ModelKit.Part(r, PrimitiveType.Cube, new Vector3(-0.6f, 0f, 0f), new Vector3(0.24f, 0.42f, 0.025f), fins);
        ModelKit.Part(r, PrimitiveType.Cube, new Vector3(-0.6f, 0f, 0f), new Vector3(0.24f, 0.025f, 0.42f), fins);
        return root;
    }

    // The bomber, about 8.3 m long and 11 m across: an olive drab fuselage with a glazed nose, a canopy and a gun
    // turret on top, a straight wing with a little dihedral carrying two engines with red spinners and three-bladed
    // propellers (`props`: they spin about their own X axis), twin tail fins with red tips, roundels on the fuselage
    // and the wings, and the bomb bay (`bay`: where the bombs leave). `exhausts`: just behind the engines.
    public static GameObject Bomber(Transform parent, out Transform[] props, out Transform bay, out Transform[] exhausts) {
        GameObject root = ModelKit.Root("Bomber", parent);
        Transform r = root.transform;
        Material olive = Mats.Lit(Olive, 0.3f), dark = Mats.Lit(OliveDark, 0.3f), black = Mats.Lit(Black, 0.4f);
        Material glass = Mats.Lit(Glass, 0.9f, 0.3f), steel = Mats.Lit(Steel, 0.6f, 0.6f), red = Mats.Lit(Accent, 0.4f);

        // fuselage, nose to tail, with a flat face to each side
        Section(r, 2.8f, 0.46f, 0.14f, 0.9f, glass);                          // glazed nose
        Section(r, 1.8f, 0.62f, 0.46f, 1f, olive);
        Section(r, -1.4f, 0.62f, 0.62f, 3.2f, olive);
        Section(r, -1.4f, 0.62f, 0.16f, 3.2f, olive, true);
        ModelKit.MeshPart(r, WorldMeshes.Dome(8, 2, 0.42f), new Vector3(1.55f, 0.5f, 0f), new Vector3(1.5f, 0.8f, 0.75f), glass, default, "Canopy");
        ModelKit.MeshPart(r, WorldMeshes.Dome(8, 2, 0.3f), new Vector3(-0.5f, 0.53f, 0f), Vector3.one, steel, default, "Turret");
        for (int s = -1; s <= 1; s += 2)
            ModelKit.Part(r, PrimitiveType.Cylinder, new Vector3(-0.95f, 0.66f, s * 0.08f), new Vector3(0.05f, 0.3f, 0.05f), black, new Vector3(0f, 0f, 90f));
        ModelKit.Part(r, PrimitiveType.Cube, new Vector3(0.3f, -0.58f, 0f), new Vector3(2f, 0.04f, 0.42f), black);   // bomb bay doors
        bay = ModelKit.Root("Bomb bay", r).transform;
        bay.localPosition = new Vector3(0.3f, -0.8f, 0f);
        float side = 0.62f * Mathf.Cos(22.5f * Mathf.Deg2Rad);               // the flat side of the fuselage (0.47 m high)
        Roundel(r, new Vector3(-0.95f, 0f, -side), Vector3.zero, 0.25f);
        Roundel(r, new Vector3(-0.95f, 0f, side), new Vector3(0f, 180f, 0f), 0.25f);

        // tail: stabiliser and twin fins with red tips
        ModelKit.Part(r, PrimitiveType.Cube, new Vector3(-4f, 0.12f, 0f), new Vector3(1.25f, 0.12f, 4.2f), olive);
        for (int s = -1; s <= 1; s += 2) {
            ModelKit.Part(r, PrimitiveType.Cube, new Vector3(-4.15f, 0.8f, s * 2f), new Vector3(1.15f, 1.5f, 0.1f), dark);
            ModelKit.Part(r, PrimitiveType.Cube, new Vector3(-4.15f, 1.43f, s * 2f), new Vector3(1.17f, 0.3f, 0.12f), red);
        }

        // wing halves (tips raised a little), each with an engine
        props = new Transform[2];
        exhausts = new Transform[2];
        for (int i = 0; i < 2; i++) {
            float s = i == 0 ? -1f : 1f;
            Transform wing = ModelKit.Root("Wing", r).transform;
            wing.localPosition = new Vector3(0.5f, -0.12f, s * 0.45f);
            wing.localRotation = Quaternion.Euler(-s * 5f, 0f, 0f);
            ModelKit.Part(wing, PrimitiveType.Cube, new Vector3(0f, 0f, s * 1.2f), new Vector3(1.9f, 0.17f, 2.4f), olive);
            ModelKit.Part(wing, PrimitiveType.Cube, new Vector3(0.275f, 0f, s * 3.7f), new Vector3(1.35f, 0.13f, 2.6f), olive);
            Roundel(wing, new Vector3(0.275f, 0.066f, s * 3.9f), new Vector3(90f, 0f, 0f), 0.5f);
            Transform engine = ModelKit.Root("Engine", wing).transform;
            engine.localPosition = new Vector3(0f, -0.05f, s * 1.75f);
            Section(engine, 0.15f, 0.34f, 0.36f, 1.5f, dark);                 // nacelle
            Section(engine, 0.15f, 0.34f, 0.08f, 1.1f, dark, true);           // fairing behind the wing
            Section(engine, 1.65f, 0.39f, 0.33f, 0.32f, black);               // cowling
            props[i] = Propeller(engine, new Vector3(2f, 0f, 0f), red, black);
            exhausts[i] = ModelKit.Root("Exhaust", engine).transform;
            exhausts[i].localPosition = new Vector3(-0.95f, 0f, 0f);
        }
        return root;
    }

    // A red spinner and three black blades with orange tips, set at a pitch; the returned hub spins about its X axis.
    static Transform Propeller(Transform parent, Vector3 pos, Material spinner, Material blade) {
        Transform hub = ModelKit.Root("Propeller", parent).transform;
        hub.localPosition = pos;
        Section(hub, 0f, 0.17f, 0.02f, 0.36f, spinner);
        Material tipMat = Mats.Lit(Orange, 0.4f);
        for (int k = 0; k < 3; k++) {
            Transform arm = ModelKit.Root("Blade", hub).transform;
            arm.localRotation = Quaternion.Euler(k * 120f, 0f, 0f);
            ModelKit.Part(arm, PrimitiveType.Cube, new Vector3(0.06f, 0.78f, 0f), new Vector3(0.05f, 1.3f, 0.17f), blade, new Vector3(0f, 22f, 0f));
            ModelKit.Part(arm, PrimitiveType.Cube, new Vector3(0.06f, 1.36f, 0f), new Vector3(0.055f, 0.14f, 0.175f), tipMat, new Vector3(0f, 22f, 0f));
        }
        return hub;
    }

    // A tapered octagonal section starting at x = `at` and running `length` forward (+X) or backwards, turned so a
    // flat face looks to each side and up and down.
    static void Section(Transform parent, float at, float r0, float r1, float length, Material mat, bool backwards = false) {
        ModelKit.MeshPart(parent, WorldMeshes.Frustum(8, r0, r1, length), new Vector3(at, 0f, 0f), Vector3.one, mat,
                          new Vector3(22.5f, 0f, backwards ? 90f : -90f), "Section");
    }

    // Black ring, white ring, red centre: three discs stacked a hair apart, facing the pivot's -Z.
    static void Roundel(Transform parent, Vector3 pos, Vector3 euler, float radius) {
        Transform t = ModelKit.Root("Roundel", parent).transform;
        t.localPosition = pos;
        t.localRotation = Quaternion.Euler(euler);
        ModelKit.MeshPart(t, WorldMeshes.Disc(16, radius), new Vector3(0f, 0f, -0.004f), Vector3.one, Mats.Lit(Black, 0.3f), default, "Ring");
        ModelKit.MeshPart(t, WorldMeshes.Disc(16, radius * 0.7f), new Vector3(0f, 0f, -0.008f), Vector3.one, Mats.Lit(White, 0.3f), default, "Ring");
        ModelKit.MeshPart(t, WorldMeshes.Disc(16, radius * 0.38f), new Vector3(0f, 0f, -0.012f), Vector3.one, Mats.Lit(Accent, 0.4f), default, "Centre");
    }

    // ------------------------------------------------------------------ effects
    // Red signal smoke: alpha-blended puffs that rise, billow and drift with the wind. A trail while the flare flies
    // (per metre and per second); Column turns it into the thick column of a flare burning on the ground.
    public static ParticleSystem RedSmoke(Transform parent, Vector3 at) {
        LayerLook look = FxLooks.Smoke();
        look.gravity = -0.12f;                          // warm smoke rises
        look.drag = 1.2f;
        look.noise = 0.45f;
        look.size = FxLooks.Curve(0f, 0.3f, 0.25f, 0.85f, 1f, 1.8f);
        look.maxParticles = 240;
        GameObject go = ModelKit.Root("Red smoke", parent);
        go.transform.position = at;
        ParticleSystem ps = FxLayer.AddLoop(go, look, 12f, SmokeRed);
        ParticleSystem.MainModule main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(SmokeRed, SmokePale);
        FxLayer.SetRanges(ps, 0.8f, 1.3f, 0.25f, 0.45f, 0.2f, 0.7f);
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverDistance = 2.5f;
        ParticleSystem.ForceOverLifetimeModule force = ps.forceOverLifetime;
        force.enabled = true;
        force.space = ParticleSystemSimulationSpace.World;
        force.x = BattleManager.Wind * 0.4f;
        force.y = 0f;
        force.z = 0f;
        ps.Play();
        return ps;
    }

    // From trail to column: big, slow, long-lived puffs pouring straight up out of the flare.
    public static void Column(ParticleSystem ps) {
        if (ps == null) return;
        FxLayer.SetRanges(ps, 2.4f, 3.4f, 0.8f, 1.3f, 2.4f, 3.8f);
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverDistance = 0f;
        emission.rateOverTime = 26f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 9f;
        shape.radius = 0.12f;
        shape.rotation = new Vector3(-90f, 0f, 0f);    // straight up
        shape.randomDirectionAmount = 0f;
    }

    // Thin grey exhaust smoke shed behind an engine (per metre flown).
    public static void Exhaust(Transform at) {
        LayerLook look = FxLooks.Smoke();
        look.maxParticles = 80;
        ParticleSystem ps = FxLayer.AddLoop(ModelKit.Root("Exhaust smoke", at), look, 0f, ExhaustGrey);
        FxLayer.SetRanges(ps, 0.6f, 1f, 0.3f, 0.55f, 0.1f, 0.4f);
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverDistance = 0.8f;
        ps.Play();
    }

    // A short puff of red smoke (a flare that went out in the sea).
    public static void Puff(Vector3 at) {
        at.z = VFX.FrontZ;
        for (int i = 0; i < 9; i++) {
            Vector3 v = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(0.6f, 2.2f), Random.Range(-0.4f, 0f));
            FxLayers.Smoke.Emit(at + v * 0.15f, v, Random.Range(0.6f, 1.1f), Random.Range(1f, 1.6f),
                                Color.Lerp(SmokeRed, SmokePale, Random.value), Random.Range(-40f, 40f));
        }
    }
}

// The airstrike's own sounds, synthesised (Synth; see SoundBank for the house recipes) the first time each is needed
// and then kept: the bomber's engine drone (a loop), the whistle of a falling bomb and the radio call when a flare
// lands. WarmUp builds them one per frame while a flare is in the air, so none is made in the middle of a strike.
static class AirstrikeSounds {
    const float Loop = 1.7f, Crossfade = 0.3f;   // the engine loop: every partial fits a whole number of cycles into it
    static AudioClip engines, whistle, radio;

    public static AudioClip Engines => engines != null ? engines : engines = Make("Bomber engines", EnginesData());
    public static AudioClip Whistle => whistle != null ? whistle : whistle = Make("Bomb whistle", WhistleData());
    public static AudioClip Radio => radio != null ? radio : radio = Make("Strike radio", RadioData());

    public static void WarmUp() {
        if (radio == null) _ = Radio;
        else if (engines == null) _ = Engines;
        else if (whistle == null) _ = Whistle;
    }

    public static void Play(AudioClip clip, Vector3 pos, float volume, float pitch = 1f) { SFX.PlayClip(clip, pos, volume, pitch); }

    static AudioClip Make(string name, float[] data) {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Synth.Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Two big piston engines a little out of tune with each other (a slow beat between them), throbbing with the
    // beat of the propeller blades, over the rush of air.
    static float[] EnginesData() {
        float d = Loop + Crossfade;
        return new Synth(d, 301)
            .Tone(0f, d, 100f / Loop, 0.5f, Env.Flat, Waveform.Saw, 0f, 0f, Filter.Low(480f, 1.2f))
            .Tone(0f, d, 104f / Loop, 0.42f, Env.Flat, Waveform.Saw, 0f, 0f, Filter.Low(480f, 1.2f))
            .Tone(0f, d, 200f / Loop, 0.16f, Env.Flat, Waveform.Square, 0f, 0f, Filter.Low(900f))
            .Noise(0f, d, 0.22f, Env.Flat, Filter.Band(650f, 1.1f))
            .Tremolo(38f / Loop, 0.4f)
            .DoneLoop(Crossfade, 0.8f);
    }

    // A falling bomb: a pure tone sliding down nearly two octaves with a slight flutter, and the hiss of the air.
    static float[] WhistleData() => new Synth(1.8f, 302)
        .Tone(0f, 1.75f, new Sweep(1900f, 560f), 0.45f, Env.Gate(0.3f, 0.1f), Waveform.Sine, 6.5f, 0.006f)
        .Tone(0f, 1.75f, new Sweep(3800f, 1120f), 0.05f, Env.Gate(0.4f, 0.1f), Waveform.Sine, 6.5f, 0.006f)
        .Noise(0f, 1.75f, 0.1f, Env.Gate(0.7f, 0.1f), Filter.Band(new Sweep(3200f, 1000f), 1.6f))
        .Done(0.8f);

    // The strike called in over the radio: a key click, two short beeps, a breath of static and the key released.
    static float[] RadioData() => new Synth(0.55f, 303)
        .Noise(0f, 0.04f, 0.6f, Env.Hit(0.0005f, 0.01f), Filter.Band(2400f, 1.2f))
        .Tone(0.06f, 0.1f, 1250f, 0.3f, Env.Gate(0.004f, 0.02f), Waveform.Square, 0f, 0f, Filter.Band(1500f, 1.4f))
        .Tone(0.2f, 0.14f, 1660f, 0.3f, Env.Gate(0.004f, 0.03f), Waveform.Square, 0f, 0f, Filter.Band(1800f, 1.4f))
        .Noise(0.04f, 0.45f, 0.07f, Env.Gate(0.01f, 0.08f), Filter.Band(2600f, 0.9f))
        .Noise(0.48f, 0.04f, 0.4f, Env.Hit(0.0005f, 0.01f), Filter.Band(2000f, 1.2f))
        .Done(0.7f);
}

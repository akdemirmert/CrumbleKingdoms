using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Pet Anteater: Dalí throws his anteater curled up in a ball. Wherever it lands it burrows in and travels underground
// (a trail of dust along the ground, bubbles under the sea) to the foot of the nearest enemy fortress, and pops out
// there. Then, at the start of each of Dalí's turns, it eats one of that fortress's lowest blocks, undermining the
// walls (AnteaterEffect); after four meals it waddles off.
public class AnteaterBall : Projectile {
    const float Radius = 0.36f, Mass = 3f;
    const float TravelSpeed = 15f, MaxTravelTime = 4f;   // underground; long trips go faster
    const float DigTime = 0.35f, PuffEvery = 0.1f, RumbleEvery = 0.6f;
    const float OutsideWall = 1.4f;                       // it surfaces this far from the fortress's outer wall

    bool landed;
    Transform visual;
    GameObject trail;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        DaliSounds.Play(DaliSound.Pop, origin, 0.5f, 0.8f);
        WeaponFactory.Spawn<AnteaterBall>("Pet Anteater", owner, def, origin, velocity);
    }

    public static GameObject Model(Transform parent) { return AnteaterModel.Curled(parent); }

    protected override void OnLaunch(Vector3 velocity) {
        visual = AnteaterModel.Curled(transform).transform;
        gameObject.AddComponent<SphereCollider>().radius = Radius;
        AddBody(Mass, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;   // the model rolls, not the body
        trail = VFX.Trail(transform, new Color(0.9f, 0.8f, 0.65f, 0.35f), 0.35f, 0.2f);
    }

    void Update() {
        if (!landed && visual != null) visual.Rotate(0f, 0f, -travelDir * 540f * Time.deltaTime, Space.Self);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished || landed || transform.position.y > World.WaterY) return;
        // Missed every island: it sinks.
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);
        RaiseImpact(p);
        VFX.Splash(p, 1f);
        SFX.Play(SoundId.Splash, p, 0.8f);
        End();
    }

    void OnCollisionEnter(Collision c) {
        if (finished || landed) return;
        Land(c.contactCount > 0 ? c.GetContact(0).point : transform.position);
    }

    // Flew off the map, or (a safety net) the trip underground never ended.
    protected override void OnLost() { End(); }

    // Wherever it touches down it digs in on the ground below, then heads for the nearest enemy fortress.
    void Land(Vector3 p) {
        landed = true;
        RaiseImpact(p);
        DaliWeapons.Freeze(this);
        maxLifetime = age + DigTime + MaxTravelTime + 5f;                // time to dig and tunnel (refined below)
        Fortress target = DaliWeapons.NearestEnemy(owner, p.x);
        if (target == null || !GroundAt(p.x, out float ground)) {
            DaliFx.Dirt(p, 0.8f);
            End();
            return;
        }
        StartCoroutine(Burrow(new Vector3(p.x, ground, 0f), target));
    }

    IEnumerator Burrow(Vector3 hole, Fortress target) {
        DaliFx.Dirt(hole, 1f);
        DaliSounds.Play(DaliSound.Dig, hole, 0.9f);
        BattleCamera.Shake(0.12f);
        DaliFx.Release(trail);
        trail = null;
        Vector3 start = transform.position;
        for (float t = 0f; t < DigTime; t += Time.deltaTime) {          // spins down into the hole
            float k = t / DigTime;
            transform.position = Vector3.Lerp(start, hole + Vector3.down * 0.3f, k);
            visual.localScale = Vector3.one * (1f - k);
            visual.Rotate(0f, 0f, -travelDir * 900f * Time.deltaTime, Space.Self);
            yield return null;
        }
        visual.gameObject.SetActive(false);

        if (!Destination(target, hole.x, out Vector3 spot, out int facing)) { End(); yield break; }
        float distance = Mathf.Abs(spot.x - hole.x);
        float speed = Mathf.Max(TravelSpeed, distance / MaxTravelTime);
        maxLifetime = age + distance / speed + 4f;                        // the trip is allowed its time (OnLost is the safety net)
        int dir = spot.x >= hole.x ? 1 : -1;
        float x = hole.x, puff = 0f, rumble = 0f;
        while ((spot.x - x) * dir > 0f) {
            float dt = Time.deltaTime;
            x = dir > 0 ? Mathf.Min(x + speed * dt, spot.x) : Mathf.Max(x - speed * dt, spot.x);
            bool land = GroundAt(x, out float y);
            transform.position = new Vector3(x, (land ? y : World.WaterY) + 0.2f, 0f);   // the camera follows the trail
            puff -= dt;
            if (puff <= 0f) {
                puff = PuffEvery;
                if (land) VFX.Dust(new Vector3(x, y + 0.1f, 0f), 0.55f, DaliFx.Earth);
                else VFX.Splash(new Vector3(x, World.WaterY, 0f), 0.3f);           // bubbles where it passes under the sea
            }
            rumble -= dt;
            if (rumble <= 0f) {
                rumble = RumbleEvery;
                DaliSounds.Play(DaliSound.Burrow, transform.position, 0.55f);
                BattleCamera.Shake(0.04f);
            }
            yield return null;
        }
        if (target == null || target.defeated) { End(); yield break; }
        AnteaterEffect.Arrive(owner, target, spot, facing);
        yield return new WaitForSeconds(0.6f);                           // the camera stays for the pop-out
        End();
    }

    // The foot of the fortress on the side the anteater comes from, just outside its outer wall (a second anteater
    // waits a little further out). facing: the way it looks, towards the fortress.
    static bool Destination(Fortress f, float fromX, out Vector3 spot, out int facing) {
        spot = Vector3.zero;
        facing = 1;
        if (f == null || f.defeated) return false;
        float min = float.MaxValue, max = float.MinValue;
        foreach (Block b in f.blocks) {
            if (b == null || !b.IsAlive) continue;
            Bounds bb = b.box.bounds;
            min = Mathf.Min(min, bb.min.x);
            max = Mathf.Max(max, bb.max.x);
        }
        if (min > max) return false;                                     // nothing left to eat
        facing = fromX < (min + max) * 0.5f ? 1 : -1;
        float wall = facing > 0 ? min : max;
        float x = wall - facing * (OutsideWall + 1.6f * AnteaterEffect.CountOn(f));
        spot = new Vector3(x, f.transform.position.y, 0f);
        return true;
    }

    // The island's surface at x (the sea has none).
    static bool GroundAt(float x, out float y) {
        if (Physics.Raycast(new Vector3(x, 80f, 0f), Vector3.down, out RaycastHit hit, 200f, Layers.GroundMask, QueryTriggerInteraction.Ignore)) {
            y = hit.point.y;
            return true;
        }
        y = World.WaterY;
        return false;
    }

    void End() {
        DaliFx.Release(trail);
        trail = null;
        Finish(true);
    }
}

// The anteater at the foot of an enemy fortress. At the start of each of its owner's turns it walks along the
// front of the walls to the lowest block near it, licks it with its long tongue and swallows it whole
// (Block.Vanish): four meals, then it waddles off. Cancel (a time rewind, the end of the battle) sends it back
// underground at once.
public class AnteaterEffect : TurnEffect {
    const int Meals = 4;
    const float WalkSpeed = 1.8f;
    const float Z = -1.15f;                       // walks just in front of the walls (blocks span z = ±0.8)
    const float Reach = 0.3f;                     // its snout stops this far from the block it eats
    const float Course = 0.35f;                   // blocks this close to the lowest one count as "the bottom"
    static readonly Color Brown = WeaponDefs.Hex("B7835A");

    AnteaterModel model;
    int mealsLeft = Meals, facing = 1;
    bool leaving, ate;
    float homeX, homeHalf, ground;                // the fortress's footprint when it arrived and the ground level

    public override string StatusText => leaving ? null : "Anteater " + mealsLeft;
    public override int RewindState { get => mealsLeft; set => mealsLeft = value; }
    public override Color StatusColor => Brown;

    public static int CountOn(Fortress f) {
        int n = 0;
        foreach (TurnEffect e in TurnEffect.All) if (e is AnteaterEffect && e.target == f) n++;
        return n;
    }

    public static void Arrive(Fortress owner, Fortress target, Vector3 spot, int facing) {
        AnteaterEffect a = new GameObject("Pet Anteater").AddComponent<AnteaterEffect>();
        a.Init(owner, target);
        a.ground = spot.y;
        a.transform.position = new Vector3(spot.x, spot.y, Z);
        Bounds b = target.GetBounds();
        a.homeX = b.center.x;
        a.homeHalf = b.extents.x;
        a.model = AnteaterModel.Standing(a.transform);
        a.Face(facing);
        a.StartCoroutine(a.PopOut());
    }

    // It bursts out of the ground: a hop, a shower of earth, a pop.
    IEnumerator PopOut() {
        Vector3 at = transform.position;
        DaliFx.Dirt(at, 1.1f);
        DaliSounds.Play(DaliSound.Pop, at, 0.9f);
        DaliSounds.Play(DaliSound.Dig, at, 0.7f);
        BattleCamera.Shake(0.15f);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime) {
            float k = t / 0.5f;
            transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, Mathf.SmoothStep(0f, 1f, k));
            transform.position = at + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.7f;
            yield return null;
        }
        transform.localScale = Vector3.one;
        transform.position = at;
    }

    public override IEnumerator OnTurnStart(Fortress shooter) {
        if (leaving) yield break;
        if (mealsLeft <= 0 || target == null || target.defeated || owner == null || owner.defeated) {
            yield return Leave();                 // full, or nobody left to feed it
            yield break;
        }
        if (shooter != owner) yield break;
        Block food = Lowest();
        if (food == null) {
            yield return Leave();
            yield break;
        }
        Focus(food);
        yield return WalkTo(food);
        if (this == null) yield break;
        ate = false;
        yield return Eat(food);
        if (this == null) yield break;
        if (ate) mealsLeft--;                     // the block broke or sank before it could eat it: no meal spent
        yield return new WaitForSeconds(0.5f);    // the wall above sags into the gap
        if (this != null && mealsLeft <= 0) yield return Leave();
        if (BattleCamera.I != null && shooter != null) BattleCamera.I.FrameForAim(shooter);
    }

    public override void Cancel() {
        DaliFx.Dirt(transform.position, 0.8f);
        base.Cancel();
    }

    // The lowest living block of the fortress (any block in its bottom course counts), nearest to the anteater.
    // Blocks that tumbled far from the fortress are left alone.
    Block Lowest() {
        float floor = float.MaxValue;
        List<Block> near = new List<Block>();
        foreach (Block b in target.blocks) {
            if (b == null || !b.IsAlive) continue;
            Bounds bb = b.box.bounds;
            if (Mathf.Abs(bb.center.x - homeX) > homeHalf + 3f || bb.min.y < ground - 1f) continue;
            near.Add(b);
            floor = Mathf.Min(floor, bb.min.y);
        }
        Block best = null;
        float bestDistance = float.MaxValue;
        foreach (Block b in near) {
            Bounds bb = b.box.bounds;
            if (bb.min.y > floor + Course) continue;
            float d = Mathf.Abs(bb.center.x - transform.position.x);
            if (d < bestDistance) { bestDistance = d; best = b; }
        }
        return best;
    }

    // Shows the fortress and its anteater while it eats.
    void Focus(Block food) {
        if (BattleCamera.I == null) return;
        Bounds b = target.GetBounds();
        b.Encapsulate(transform.position);
        b.Encapsulate(food.box.bounds);
        BattleCamera.I.Frame(b, 2.5f);
    }

    // Along the front of the walls until its snout is just short of the block's near side.
    IEnumerator WalkTo(Block food) {
        Bounds bb = food.box.bounds;
        int dir = bb.center.x >= transform.position.x ? 1 : -1;
        Face(dir);
        float snout = Mathf.Abs(model.SnoutTip.x - transform.position.x);
        float goal = (dir > 0 ? bb.min.x : bb.max.x) - dir * (snout + Reach);
        for (float t = 0f; Mathf.Abs(transform.position.x - goal) > 0.01f && t < 3f; t += Time.deltaTime) {
            Vector3 p = transform.position;
            p.x = Mathf.MoveTowards(p.x, goal, WalkSpeed * Time.deltaTime);
            transform.position = p;
            model.Waddle();
            yield return null;
            if (this == null) yield break;
        }
        model.Stand();
    }

    // The tongue darts into the block; the block shrinks into the snout (a copy of it, while the block itself
    // hides and keeps holding the wall), then it is gone: Block.Vanish. A satisfied gulp.
    IEnumerator Eat(Block food) {
        if (food == null || !food.IsAlive) yield break;
        Bounds bb = food.box.bounds;
        Vector3 bite = new Vector3(Mathf.Clamp(model.SnoutTip.x + facing * 0.5f, bb.min.x, bb.max.x),
                                   Mathf.Clamp(model.SnoutTip.y, bb.min.y + 0.05f, bb.max.y - 0.05f), Z + 0.3f);
        DaliSounds.Play(DaliSound.Slurp, bite, 0.9f);
        for (float t = 0f; t < 0.18f; t += Time.deltaTime) {
            model.Lick(Vector3.Lerp(model.SnoutTip, bite, t / 0.18f));
            yield return null;
            if (this == null) yield break;
        }
        if (food == null || !food.IsAlive) { model.Lick(model.SnoutTip); yield break; }

        BlockMaterial mat = food.material;
        Transform ghost = Ghost(food);
        foreach (Renderer r in food.GetComponentsInChildren<Renderer>()) r.enabled = false;
        Vector3 from = ghost.position, size = ghost.localScale;
        for (float t = 0f; t < 0.5f; t += Time.deltaTime) {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.5f);
            ghost.position = Vector3.Lerp(from, model.SnoutTip, k);
            float wobble = 1f + 0.12f * Mathf.Sin(t * 40f) * (1f - k);
            ghost.localScale = new Vector3(size.x * (1f - k) * wobble, size.y * (1f - k) / wobble, size.z * (1f - k));
            model.Lick(ghost.position);
            yield return null;
            if (this == null) yield break;
        }
        Destroy(ghost.gameObject);
        model.Lick(model.SnoutTip);
        Vector3 at = food != null ? food.transform.position : bite;
        if (food != null && food.IsAlive) {
            food.Vanish();
            ate = true;
        }
        VFX.Dust(at, 0.9f, DaliFx.Earth);
        VFX.Chips(model.SnoutTip, mat, 6, Vector3.up);
        model.Gulp();
        DaliSounds.Play(DaliSound.Gulp, model.SnoutTip, 0.9f);
        BattleCamera.Shake(0.12f);
    }

    // A copy of the block's look (it is shrunk into the snout; the real block keeps holding the wall until then).
    Transform Ghost(Block b) {
        GameObject g = new GameObject("Eaten " + b.name);
        g.transform.SetPositionAndRotation(b.transform.position, b.transform.rotation);
        g.transform.SetParent(transform, true);
        g.AddComponent<MeshFilter>().sharedMesh = b.GetComponent<MeshFilter>().sharedMesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = b.rend.sharedMaterial;
        return g.transform;
    }

    // Full (or with nothing left to eat): it turns away from the fortress, waddles off and digs itself in.
    IEnumerator Leave() {
        leaving = true;
        int away = transform.position.x >= homeX ? 1 : -1;
        Face(away);
        for (float t = 0f; t < 1.2f; t += Time.deltaTime) {
            transform.position += Vector3.right * away * WalkSpeed * Time.deltaTime;
            model.Waddle();
            yield return null;
            if (this == null) yield break;
        }
        Vector3 at = transform.position;
        DaliFx.Dirt(at, 0.9f);
        DaliSounds.Play(DaliSound.Dig, at, 0.7f);
        for (float t = 0f; t < 0.35f; t += Time.deltaTime) {
            float k = t / 0.35f;
            transform.localScale = Vector3.one * (1f - k);
            transform.position = at + Vector3.down * k * 0.6f;
            yield return null;
            if (this == null) yield break;
        }
        Destroy(gameObject);
    }

    void Face(int dir) {
        facing = dir >= 0 ? 1 : -1;
        model.transform.localRotation = Quaternion.Euler(0f, facing > 0 ? 0f : 180f, 0f);
    }
}

// The anteater: grey-brown with a long tapering snout, the black-and-white stripe across its shoulder, white front
// legs, a huge bushy tail and a gold collar (it is a pet). Standing (with its little moves) or curled up in a ball
// for the throw. Built facing +X, feet at y = 0, about 2 m long.
public class AnteaterModel : MonoBehaviour {
    const float Scale = 0.8f;
    static readonly Color Fur = new Color(0.56f, 0.48f, 0.4f), Dark = new Color(0.14f, 0.11f, 0.09f), Pale = new Color(0.88f, 0.85f, 0.78f);
    static readonly Color TailFur = new Color(0.37f, 0.31f, 0.26f), Snout = new Color(0.64f, 0.58f, 0.5f), Tongue = new Color(0.95f, 0.45f, 0.55f);

    Transform body, tail, tongue, snoutTip;
    Transform[] legs;
    float walk, seed, gulpAt = -10f;

    public Vector3 SnoutTip => snoutTip.position;

    public static AnteaterModel Standing(Transform parent) {
        GameObject root = ModelKit.Root("Anteater", parent);
        root.transform.localScale = Vector3.one * Scale;
        AnteaterModel a = root.AddComponent<AnteaterModel>();
        a.seed = Random.value * 10f;
        Transform r = root.transform;
        Material fur = Mats.Lit(Fur, 0.2f), dark = Mats.Lit(Dark, 0.25f), pale = Mats.Lit(Pale, 0.2f);

        a.body = Pivot("Body", r, Vector3.zero);
        Part(a.body, WorldMeshes.Ball(0.34f), new Vector3(0f, 0.58f, 0f), new Vector3(1.55f, 0.9f, 0.82f), fur);
        Part(a.body, WorldMeshes.Ball(0.35f), new Vector3(0.28f, 0.6f, 0f), new Vector3(0.3f, 0.94f, 0.86f), pale, new Vector3(0f, 0f, -28f));
        Part(a.body, WorldMeshes.Ball(0.35f), new Vector3(0.22f, 0.6f, 0f), new Vector3(0.26f, 0.95f, 0.87f), dark, new Vector3(0f, 0f, -28f));
        // the head: a long snout tapering forward and a little down, ears, an eye on each side, a gold collar
        Vector3 neck = new Vector3(0.45f, 0.66f, 0f), along = Quaternion.Euler(0f, 0f, -8f) * Vector3.right;
        Part(a.body, WorldMeshes.Frustum(8, 0.17f, 0.035f, 0.78f), neck, Vector3.one, Mats.Lit(Snout, 0.25f), new Vector3(0f, 0f, -98f));
        Part(a.body, WorldMeshes.Frustum(12, 0.19f, 0.18f, 0.05f), neck + along * 0.04f, Vector3.one, DaliArt.GoldMetal, new Vector3(0f, 0f, -98f));
        for (int s = -1; s <= 1; s += 2) {
            Part(a.body, WorldMeshes.Ball(0.05f), new Vector3(0.48f, 0.84f, s * 0.08f), Vector3.one, fur);
            Part(a.body, WorldMeshes.Ball(0.026f), new Vector3(0.64f, 0.71f, s * 0.115f), Vector3.one, dark);
        }
        a.snoutTip = Pivot("Snout Tip", a.body, neck + along * 0.78f);
        Part(a.snoutTip, WorldMeshes.Ball(0.032f), Vector3.zero, Vector3.one, dark);
        a.tongue = Pivot("Tongue", a.snoutTip, Vector3.zero);
        Part(a.tongue, WorldMeshes.Frustum(5, 0.02f, 0.008f, 1f), Vector3.zero, Vector3.one, Mats.Lit(Tongue, 0.6f), new Vector3(0f, 0f, -90f));
        a.tongue.gameObject.SetActive(false);

        // the bushy tail, raised a little behind
        a.tail = Pivot("Tail", r, new Vector3(-0.5f, 0.66f, 0f));
        Material tailFur = Mats.Lit(TailFur, 0.15f);
        Part(a.tail, WorldMeshes.Ball(0.32f), new Vector3(-0.3f, 0.06f, 0f), new Vector3(1.35f, 0.62f, 0.32f), tailFur, new Vector3(0f, 0f, 28f));
        Part(a.tail, WorldMeshes.Ball(0.26f), new Vector3(-0.56f, 0.22f, 0f), new Vector3(1.1f, 0.55f, 0.28f), tailFur, new Vector3(0f, 0f, 38f));

        // legs hanging from hip pivots (they swing when it walks); white front legs with a dark band, big claws
        a.legs = new Transform[4];
        for (int i = 0; i < 4; i++) {
            bool front = i < 2;
            Transform hip = Pivot("Leg", r, new Vector3(front ? 0.3f : -0.3f, 0.36f, i % 2 == 0 ? -0.15f : 0.15f));
            Part(hip, WorldMeshes.Frustum(6, 0.07f, 0.085f, 0.36f), new Vector3(0f, -0.36f, 0f), Vector3.one, front ? pale : fur);
            if (front) Part(hip, WorldMeshes.Frustum(6, 0.078f, 0.078f, 0.06f), new Vector3(0f, -0.26f, 0f), Vector3.one, dark);
            Part(hip, WorldMeshes.Ball(0.06f), new Vector3(0.035f, -0.35f, 0f), new Vector3(1.4f, 0.55f, 1f), dark);
            a.legs[i] = hip;
        }
        return a;
    }

    // Curled up for the throw: a ball of fur with the stripe round it, the bushy tail wrapped over the top, the
    // snout tucked under, front paws and a sleepy closed eye.
    public static GameObject Curled(Transform parent) {
        GameObject root = ModelKit.Root("Curled Anteater", parent);
        Transform r = root.transform;
        Material fur = Mats.Lit(Fur, 0.2f), dark = Mats.Lit(Dark, 0.25f), pale = Mats.Lit(Pale, 0.2f);
        Part(r, WorldMeshes.Ball(0.3f), Vector3.zero, new Vector3(1f, 0.95f, 0.9f), fur);
        Part(r, WorldMeshes.Ball(0.305f), new Vector3(0.04f, 0f, 0f), new Vector3(0.28f, 1f, 0.95f), dark, new Vector3(0f, 0f, -25f));
        Part(r, WorldMeshes.Ball(0.3f), new Vector3(-0.08f, 0.12f, 0f), new Vector3(1.15f, 0.62f, 1.08f), Mats.Lit(TailFur, 0.15f), new Vector3(0f, 0f, 30f));
        Part(r, WorldMeshes.Frustum(8, 0.1f, 0.03f, 0.42f), new Vector3(0.14f, -0.1f, -0.1f), Vector3.one, Mats.Lit(Snout, 0.25f), new Vector3(0f, 0f, -128f));
        Part(r, WorldMeshes.Ball(0.05f), new Vector3(0.12f, 0.03f, -0.26f), new Vector3(1.2f, 0.3f, 0.5f), dark);
        for (int s = -1; s <= 1; s += 2)
            Part(r, WorldMeshes.Ball(0.06f), new Vector3(0.2f, -0.2f, s * 0.11f), new Vector3(1.2f, 0.7f, 1f), pale);
        return root;
    }

    // Legs swinging in diagonal pairs and a bobbing body: a waddle (call every frame while it walks).
    public void Waddle() {
        walk += Time.deltaTime * 12f;
        float s = Mathf.Sin(walk);
        for (int i = 0; i < legs.Length; i++) legs[i].localRotation = Quaternion.Euler(0f, 0f, (i == 0 || i == 3 ? s : -s) * 28f);
        body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Cos(walk)) * 0.05f, 0f);
    }

    public void Stand() {
        foreach (Transform leg in legs) leg.localRotation = Quaternion.identity;
        body.localPosition = Vector3.zero;
    }

    public void Gulp() { gulpAt = Time.time; }

    // The tongue from the snout tip to a point (world space); at the tip itself it is tucked away.
    public void Lick(Vector3 to) {
        Vector3 d = to - snoutTip.position;
        float length = d.magnitude;
        bool show = length > 0.03f;
        tongue.gameObject.SetActive(show);
        if (!show) return;
        tongue.rotation = Quaternion.FromToRotation(Vector3.right, d / length);
        tongue.localScale = new Vector3(length / Mathf.Max(0.01f, snoutTip.lossyScale.x), 1f, 1f);
    }

    void Update() {
        if (tail != null) tail.localRotation = Quaternion.Euler(0f, 0f, 5f * Mathf.Sin(Time.time * 1.6f + seed));
        if (body == null) return;
        float g = (Time.time - gulpAt) / 0.4f;   // the swallowed block goes down: a swell running through the body
        float k = g >= 0f && g < 1f ? Mathf.Sin(g * Mathf.PI) : 0f;
        body.localScale = new Vector3(1f + 0.12f * k, 1f + 0.18f * k, 1f + 0.12f * k);
    }

    static Transform Pivot(string name, Transform parent, Vector3 at) {
        Transform t = ModelKit.Root(name, parent).transform;
        t.localPosition = at;
        return t;
    }

    static void Part(Transform parent, Mesh mesh, Vector3 at, Vector3 scale, Material m, Vector3 euler = default) {
        ModelKit.MeshPart(parent, mesh, at, scale, m, euler);
    }
}

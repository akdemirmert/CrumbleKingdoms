using System.Collections;
using UnityEngine;

// Mustache Birds: Dalí throws his waxed mustache. It splits into its two halves, which fly off as a pair of birds
// drawn like a child draws them, a flat flapping "M" stroke (weaving around the aimed arc in mirror image), and
// each bursts on contact in a small blast and a puff of black feathers. He stays bare-lipped until the start of
// his next turn (MustacheRegrowth).
public class MustacheBird : Projectile {
    const float Weave = 0.45f;              // metres either side of the aimed arc
    const float WeavesPerSecond = 1.4f;
    const float WeaveRamp = 0.3f;           // seconds for the weave to grow in after the throw
    const float FlapsPerSecond = 4.5f;
    const float Span = 0.7f;                // half the wingspan of the drawn bird
    const int Segments = 8;                 // stroke pieces per wing
    const float Radius = 0.22f, Mass = 0.6f;

    int side = 1;                           // +1 / -1: the two halves weave in mirror image
    Vector3 arcPos, arcVel;                 // the plain ballistic arc the pair follows on average
    Transform visual;
    Mesh doodle;
    Vector3[] doodleVerts;
    GameObject trail, feathers;
    float flapPhase;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        LoseMustache(owner);
        DaliSounds.Play(DaliSound.Flutter, origin, 0.9f);
        DaliFx.Feathers(origin, 8, 2.5f);
        for (int s = -1; s <= 1; s += 2) {
            int half = s;
            WeaponFactory.Spawn<MustacheBird>("Mustache Bird", owner, def, origin, velocity, b => b.side = half);
        }
    }

    // The mustache leaves his face in a puff of feathers; it grows back at the start of his next turn.
    static void LoseMustache(Fortress owner) {
        if (owner == null) return;
        CommanderRig rig = owner.commander != null ? owner.commander.rig : null;
        if (rig != null && rig.mustache != null && rig.mustache.activeSelf) {
            DaliFx.Feathers(rig.mustache.transform.position, 6, 1.5f);
            rig.SetMustache(false);
        }
        MustacheRegrowth.Begin(owner);
    }

    protected override void OnLaunch(Vector3 velocity) {
        // The arc (gravity and wind) is integrated here, so the base class must not add forces of its own.
        gravityScale = 1f;
        windFactor = 0f;
        maxLifetime = 10f;
        arcPos = transform.position;
        arcVel = velocity;
        flapPhase = side > 0 ? 0f : 0.5f;
        visual = BuildBird(transform, out doodle, out doodleVerts).transform;
        gameObject.AddComponent<SphereCollider>().radius = Radius;
        AddBody(Mass, velocity, false);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;   // the model turns, not the body
        trail = VFX.Trail(transform, new Color(0.85f, 0.88f, 0.95f, 0.35f), 0.12f, 0.3f);              // a pale streak of air
        feathers = DaliFx.FeatherTrail(transform, 4f);
        FaceFlight(velocity);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished) return;
        float dt = Time.fixedDeltaTime;
        arcVel += new Vector3(BattleManager.Wind * def.windFactor, -World.Gravity * def.gravityScale, 0f) * dt;
        arcPos += arcVel * dt;
        // Steer the body to the arc plus its weave: it gets there by the end of this physics step.
        Vector3 across = new Vector3(-arcVel.y, arcVel.x, 0f).normalized;
        float weave = Mathf.Sin(age * WeavesPerSecond * Mathf.PI * 2f) * Weave * Mathf.Clamp01(age / WeaveRamp) * side;
        Vector3 steer = (arcPos + across * weave - rb.position) / dt;
        rb.linearVelocity = Vector3.ClampMagnitude(steer, arcVel.magnitude + 30f);
        if (rb.position.y < World.WaterY) Drown();
    }

    void Update() {
        if (finished) return;
        FaceFlight(Velocity);
        flapPhase += Time.deltaTime * FlapsPerSecond;
        DrawBird(doodleVerts, Mathf.Sin(flapPhase * Mathf.PI * 2f));
        doodle.vertices = doodleVerts;
        doodle.RecalculateBounds();
    }

    // A drawn bird stays upright and only banks a little with the climb or dive of its flight.
    void FaceFlight(Vector3 v) {
        if (v.sqrMagnitude < 0.01f) return;
        float bank = Mathf.Atan2(v.y, Mathf.Abs(v.x)) * Mathf.Rad2Deg * (v.x < 0f ? -1f : 1f);
        visual.rotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(bank * 0.5f, -25f, 25f));
    }

    protected override void OnDestroy() {
        base.OnDestroy();
        if (doodle != null) Destroy(doodle);
    }

    void OnCollisionEnter(Collision c) {
        if (finished) return;
        Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        p.z = 0f;
        RaiseImpact(p);
        Explosion.Create(p, def.radius, def.damage, def.force, owner, DamageType.Explosion, BlastStyle.Small);
        DaliFx.Feathers(p, 22, 5f);
        DaliSounds.Play(DaliSound.FeatherPop, p, 0.8f);
        End();
    }

    // Into the sea: a splash and a few feathers floating on the water.
    void Drown() {
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);
        if (!impacted) RaiseImpact(p);
        VFX.Splash(p, 0.6f);
        SFX.Play(SoundId.Splash, p, 0.5f, 1.3f);
        DaliFx.Feathers(p + Vector3.up * 0.3f, 6, 1.5f);
        End();
    }

    protected override void OnLost() { End(); }

    void End() {
        DaliFx.Release(trail);
        DaliFx.Release(feathers);
        trail = feathers = null;
        Finish(true);
    }

    // ------------------------------------------------------------------ models
    // The bird as a flat ink stroke facing the camera, drawn in front of the walls so it always reads. Its mesh is
    // redrawn every frame by DrawBird (both windings, so it shows from either side).
    static GameObject BuildBird(Transform parent, out Mesh mesh, out Vector3[] verts) {
        mesh = BirdMesh(0f, out verts);
        GameObject bird = ModelKit.MeshPart(parent, mesh, new Vector3(0f, 0f, -1f), Vector3.one, Mats.Unlit(DaliArt.Ink), default, "Drawn Bird");
        ModelKit.Shadows(bird, false, false);
        return bird;
    }

    static Mesh BirdMesh(float beat, out Vector3[] verts) {
        verts = new Vector3[2 * (Segments + 1) * 2];
        DrawBird(verts, beat);
        int[] tris = new int[2 * Segments * 12];
        int q = 0;
        for (int w = 0; w < 2; w++)
            for (int i = 0; i < Segments; i++) {
                int a = (w * (Segments + 1) + i) * 2, b = a + 1, c = a + 2, d = a + 3;
                tris[q++] = a; tris[q++] = b; tris[q++] = c;
                tris[q++] = b; tris[q++] = d; tris[q++] = c;
                tris[q++] = a; tris[q++] = c; tris[q++] = b;
                tris[q++] = b; tris[q++] = c; tris[q++] = d;
            }
        Mesh mesh = new Mesh { name = "Drawn Bird" };
        mesh.MarkDynamic();
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    // Two arched wings meeting in a dip, like a flattened "M": each wing is a curved stroke from the middle to its
    // tip, thick in the middle and thin at the tip. beat -1..1 flaps them (+1 wings up, -1 wings down).
    static void DrawBird(Vector3[] v, float beat) {
        float peak = 0.42f + 0.2f * beat, tip = -0.05f + 0.28f * beat;   // the wing's curve control height and tip height
        int o = 0;
        for (int w = -1; w <= 1; w += 2) {
            Vector2 p1 = new Vector2(w * Span * 0.4f, peak), p2 = new Vector2(w * Span, tip);
            for (int i = 0; i <= Segments; i++) {
                float t = i / (float)Segments;
                Vector2 c = 2f * (1f - t) * t * p1 + t * t * p2;                       // quadratic curve from the middle
                Vector2 dir = (2f * (1f - t) * p1 + 2f * t * (p2 - p1)).normalized;
                Vector2 n = new Vector2(-dir.y, dir.x) * Mathf.Lerp(0.065f, 0.022f, t);
                v[o++] = new Vector3(c.x - n.x, c.y - n.y, 0f);
                v[o++] = new Vector3(c.x + n.x, c.y + n.y, 0f);
            }
        }
    }

    static Mesh iconWingsUp, iconWingsDown;

    // Weapon icon: two drawn birds in pale ink (a dark stroke would vanish on the dark weapon slot). The icon
    // renderer tilts every weapon model (IconRenderer.Weapon); a drawing has to face the camera, so it undoes that.
    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Mustache Birds", parent);
        root.transform.localRotation = Quaternion.Inverse(Quaternion.Euler(12f, -28f, 38f));
        Material ink = Mats.Unlit(new Color(0.95f, 0.92f, 0.86f));
        if (iconWingsUp == null) iconWingsUp = BirdMesh(0.7f, out _);
        if (iconWingsDown == null) iconWingsDown = BirdMesh(-0.2f, out _);
        ModelKit.MeshPart(root.transform, iconWingsUp, new Vector3(-0.12f, 0.1f, 0f), Vector3.one, ink, default, "Bird");
        ModelKit.MeshPart(root.transform, iconWingsDown, new Vector3(0.38f, -0.32f, 0f), Vector3.one * 0.7f, ink, default, "Bird");
        return root;
    }
}

// Dalí without his mustache until the start of his next turn: then it grows back with a sparkle. A time rewind or
// the end of the battle gives it back at once (Cancel).
public class MustacheRegrowth : TurnEffect {
    public static void Begin(Fortress owner) {
        foreach (TurnEffect e in TurnEffect.All)
            if (e is MustacheRegrowth && e.owner == owner) return;   // already growing back
        new GameObject("Mustache Regrowth").AddComponent<MustacheRegrowth>().Init(owner, null);
    }

    public override IEnumerator OnTurnStart(Fortress shooter) {
        if (shooter != owner) yield break;
        Regrow(true);
        Destroy(gameObject);
        yield return new WaitForSeconds(0.4f);   // a moment to see it sparkle before he aims
    }

    public override void Cancel() {
        Regrow(false);
        base.Cancel();
    }

    void Regrow(bool sparkle) {
        CommanderRig rig = owner != null && owner.commander != null ? owner.commander.rig : null;
        if (rig == null || rig.mustache == null) return;
        rig.SetMustache(true);
        if (!sparkle) return;
        Vector3 p = rig.mustache.transform.position;
        VFX.MagicBurst(p, DaliArt.Gold, 0.45f);
        DaliSounds.Play(DaliSound.Regrow, p, 0.8f);
    }
}

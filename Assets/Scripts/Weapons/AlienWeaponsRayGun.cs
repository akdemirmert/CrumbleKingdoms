using UnityEngine;
using UnityEngine.Rendering;

// The Alien's Ray Gun: a straight, fast laser bolt (gravityScale 0 and no wind in WeaponDefs, so the aim line
// and the bot both shoot straight). It is a PiercingProjectile: every block it passes eats part of its budget
// (def.pierce) and takes def.damage; light hardly slows down. Each block it burns through gets sparks and a
// scorch line across its face; where the bolt runs out it fizzles in a burst of green light.
public class AlienRayBolt : PiercingProjectile {
    static readonly Color Hot = new Color(0.8f, 1f, 0.88f);

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        AlienSounds.Play(AlienSounds.Pew, origin, 0.9f);
        VFX.Sparks(origin, AlienKit.Laser, 12);
        VFX.LightFlash(origin, AlienKit.Laser, 6f, 7f, 0.15f);
        WeaponFactory.Spawn<AlienRayBolt>("Ray Bolt", owner, def, origin, velocity, b => {
            b.sweepRadius = 0.12f;
            b.budget = def.pierce;
            b.damage = def.damage;
            b.slowPerCost = 0.04f;           // light barely slows down
            b.stopSpeed = 10f;
            b.embedDepth = 0f;
            b.blockPush = 0.5f;
        });
    }

    protected override void OnLaunch(Vector3 velocity) {
        base.OnLaunch(velocity);
        maxLifetime = 5f;                    // at 70+ m/s it leaves the arena long before
        // a white-hot core with a long green beam behind it (the position is the bolt's head)
        ModelKit.Part(transform, PrimitiveType.Capsule, new Vector3(-0.4f, 0f, 0f), new Vector3(0.1f, 0.5f, 0.1f),
                      Mats.Emissive(Hot, new Color(2.5f, 6f, 3.2f)), new Vector3(0f, 0f, 90f));
        ModelKit.Shadows(gameObject, false, false);
        VFX.Trail(transform, AlienKit.Laser * 2.2f, 0.3f, 0.16f);
        VFX.Trail(transform, Hot * 2f, 0.1f, 0.09f);
        AlienKit.Glow(transform, AlienKit.Laser, 4f, 7f);
        BlockPierced += OnPierced;
        Stopped += OnStopped;
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished || transform.position.y >= World.WaterY) return;
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);   // into the sea: a hiss of steam
        VFX.Splash(p, 0.6f);
        VFX.Dust(p + Vector3.up * 0.4f, 0.9f, new Color(0.92f, 0.96f, 1f, 0.6f));
        AlienSounds.Play(AlienSounds.Sizzle, p, 0.7f, 0.8f);
        if (!impacted) RaiseImpact(p);
        Finish(true);
    }

    void OnPierced(PiercingProjectile self, Block block, Vector3 point) {
        VFX.Sparks(point, AlienKit.Laser, 12);
        AlienSounds.Play(AlienSounds.Sizzle, point, 0.55f);
        if (block != null && block.IsAlive) AlienScorch.Burn(block, point, transform.right);
        BattleCamera.Shake(0.05f);
    }

    // Light has nothing to stick with: it bursts and is gone.
    void OnStopped(PiercingProjectile self, Vector3 point, Collider hit) {
        VFX.Sparks(point, AlienKit.Laser, 24);
        VFX.MagicBurst(point, AlienKit.Laser, 0.5f);
        VFX.LightFlash(point, AlienKit.Laser, 5f, 6f, 0.25f);
        if (hit != null && hit.gameObject.layer == Layers.Ground) VFX.Dust(point, 0.5f, new Color(0.2f, 0.2f, 0.18f, 0.6f));
        AlienSounds.Play(AlienSounds.Sizzle, point, 0.8f, 0.85f);
        BattleCamera.Shake(0.1f);
        Destroy(gameObject);
    }
}

// A scorch line where a ray bolt burned through a block: it glows hot green, cools to a dark burn mark and fades
// after a while. It lies on the block's camera side and is parented to it, so it falls (and breaks) with it.
public class AlienScorch : MonoBehaviour {
    const float CoolTime = 1.2f, Life = 10f, FadeTime = 1.5f;
    const int MaxLive = 40;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly Color GlowColor = new Color(1.4f, 3.2f, 1.8f, 1f), SootColor = new Color(0.07f, 0.07f, 0.06f, 0.85f);
    static Texture2D streak;
    static int live;

    Renderer glow, soot;
    MaterialPropertyBlock mpb;
    float t;

    // entry: where the bolt went in; dir: its flight direction.
    public static void Burn(Block b, Vector3 entry, Vector3 dir) {
        if (live >= MaxLive || b.box == null || !b.box.enabled) return;
        Vector3 exit = entry + dir * 0.4f;   // where it comes out: the far face of the block's box
        if (b.box.Raycast(new Ray(entry + dir * 8f, -dir), out RaycastHit hit, 8f)) exit = hit.point;
        Transform host = b.transform;
        Vector3 a = host.InverseTransformPoint(entry), c = host.InverseTransformPoint(exit), d = c - a;
        GameObject go = ModelKit.Root("Scorch", host);
        go.transform.localPosition = new Vector3((a.x + c.x) * 0.5f, (a.y + c.y) * 0.5f, -b.size.z * 0.5f - 0.012f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        float length = Mathf.Max(0.25f, new Vector2(d.x, d.y).magnitude + 0.15f);
        AlienScorch s = go.AddComponent<AlienScorch>();
        s.soot = ModelKit.MeshPart(go.transform, WorldMeshes.Quad(), Vector3.zero, new Vector3(length, 0.22f, 1f),
                                   Mats.UnlitTransparent(SootColor, Streak), default, "Soot").GetComponent<Renderer>();
        s.glow = ModelKit.MeshPart(go.transform, WorldMeshes.Quad(), new Vector3(0f, 0f, -0.004f), new Vector3(length, 0.12f, 1f),
                                   Mats.ParticleAdditive(Streak), default, "Glow").GetComponent<Renderer>();
        ModelKit.Shadows(go, false, false);
        s.mpb = new MaterialPropertyBlock();
        s.Update();
    }

    void OnEnable() { live++; }
    void OnDisable() { live--; }

    void Update() {
        t += Time.deltaTime;
        float heat = Mathf.Clamp01(1f - t / CoolTime);
        if (glow.enabled) {
            mpb.SetColor(BaseColorId, GlowColor * heat);
            glow.SetPropertyBlock(mpb);
            if (heat <= 0f) glow.enabled = false;
        }
        Color c = SootColor;
        c.a *= Mathf.Clamp01((Life - t) / FadeTime);
        mpb.SetColor(BaseColorId, c);
        soot.SetPropertyBlock(mpb);
        if (t >= Life) Destroy(gameObject);
    }

    // A soft line with ragged edges (alpha), white so the materials can tint it.
    static Texture2D Streak {
        get {
            if (streak != null) return streak;
            const int W = 64, H = 16;
            streak = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "Scorch", wrapMode = TextureWrapMode.Clamp };
            Color[] px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++) {
                    float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                    float halfWidth = 0.32f + 0.12f * Mathf.PerlinNoise(u * 9f, 3.7f);
                    float across = Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) / halfWidth);
                    float along = Mathf.Clamp01(Mathf.Min(u, 1f - u) * 6f);
                    px[y * W + x] = new Color(1f, 1f, 1f, across * across * along);
                }
            streak.SetPixels(px);
            streak.Apply(false);
            return streak;
        }
    }
}

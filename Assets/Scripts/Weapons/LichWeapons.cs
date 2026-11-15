using UnityEngine;

// The Lich's weapons: dead and fantastic creatures he raises to fight for him. Each weapon lives in its own file:
//   LichWeaponsGargoyle.cs    Gargoyle Dive: a stone gargoyle that wakes in mid-air and dives straight down
//   LichWeaponsSkeletons.cs   Skeleton Legion: a tombstone; three skeletons climb the enemy wall and tear it apart
//   LichWeaponsDragon.cs      Bone Dragon: a dragon skull; the bone dragon breathes soulfire and stays two more turns
//   LichWeaponsHand.cs        Bone Hand: a giant skeletal hand bursts from the ground, slams down and drags it all under
// Shared colours, materials, model helpers, effects and his synthesised sounds are below; his own look (commander,
// block skins, fortress decorations) is in World/LichLook*.cs.
static class LichWeapons {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() {
        WeaponFactory.Register(WeaponId.GargoyleDive, GargoyleDive.Fire, GargoyleDive.Model);
        WeaponFactory.Register(WeaponId.SkeletonLegion, Tombstone.Fire, Tombstone.Model);
        WeaponFactory.Register(WeaponId.BoneDragon, DragonSkull.Fire, DragonSkull.Model);
        WeaponFactory.Register(WeaponId.BoneHand, BoneCharm.Fire, BoneCharm.Model);
    }
}

// Colours, materials and small helpers shared by the Lich's creatures and his look.
public static class LichKit {
    public static readonly Color Soul = new Color(0.55f, 1f, 0.35f);           // soulfire green
    public static readonly Color SoulHdr = new Color(1.5f, 3.6f, 1.05f);       // its glow (above 1 blooms)
    public static readonly Color Bone = new Color(0.88f, 0.85f, 0.75f);
    public static readonly Color BoneDark = new Color(0.56f, 0.53f, 0.45f);
    public static readonly Color Stone = new Color(0.46f, 0.49f, 0.46f);
    public static readonly Color StoneDark = new Color(0.27f, 0.29f, 0.28f);
    public static readonly Color Robe = new Color(0.07f, 0.09f, 0.08f);
    public static readonly Color Gold = new Color(0.95f, 0.74f, 0.3f);
    public static readonly Color Rust = new Color(0.42f, 0.27f, 0.17f);

    public static Material BoneMat => Mats.Lit(Bone, 0.3f);
    public static Material BoneDarkMat => Mats.Lit(BoneDark, 0.2f);
    public static Material StoneMat => Mats.Lit(Stone, 0.12f);
    public static Material StoneDarkMat => Mats.Lit(StoneDark, 0.1f);
    public static Material SocketMat => Mats.Lit(new Color(0.03f, 0.035f, 0.03f), 0.1f);
    public static Material SoulMat => Mats.Emissive(Soul, SoulHdr);
    public static Material GoldMat => Mats.Lit(Gold, 0.7f, 0.6f);
    public static Material RustMat => Mats.Lit(Rust, 0.3f, 0.45f);
    public static Material RobeMat => Mats.Lit(Robe, 0.25f);

    // ------------------------------------------------------------------ model helpers
    public static Transform Pivot(string name, Transform parent, Vector3 localPos, Vector3 euler = default) {
        Transform t = ModelKit.Root(name, parent).transform;
        t.localPosition = localPos;
        t.localRotation = Quaternion.Euler(euler);
        return t;
    }

    public static GameObject Part(Transform parent, Mesh mesh, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default) {
        return ModelKit.MeshPart(parent, mesh, pos, scale, m, euler);
    }

    public static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default) {
        return ModelKit.Part(parent, type, pos, scale, m, euler);
    }

    // A tapered bone (or horn, or spar) from a to b in the parent's space: radius r0 at a, r1 at b.
    public static GameObject Rod(Transform parent, Vector3 a, Vector3 b, float r0, float r1, Material m, int sides = 6) {
        Vector3 d = b - a;
        if (d.sqrMagnitude < 1e-6f) d = Vector3.up * 0.001f;
        return ModelKit.MeshPart(parent, WorldMeshes.Frustum(sides, r0, r1, d.magnitude), a, Vector3.one, m,
                                 Quaternion.FromToRotation(Vector3.up, d).eulerAngles, "Bone");
    }

    // A green point light a little in front of `parent`, so it lights the side the camera sees.
    public static Light Glow(Transform parent, Vector3 localPos, float intensity, float range) {
        GameObject go = new GameObject("Soul Glow");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = Soul;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        return light;
    }

    // ------------------------------------------------------------------ effects
    // Effects are drawn just in front of the blocks (see VFX.FrontZ).
    public static Vector3 Front(Vector3 p) { return new Vector3(p.x, p.y, Mathf.Min(p.z, VFX.FrontZ)); }

    // A gust of green soulfire: flames licking upwards, a few embers, a flash of green light.
    public static void SoulBurst(Vector3 pos, float size, bool light = true) {
        Vector3 at = Front(pos);
        int n = Mathf.Clamp(Mathf.RoundToInt(10f * size), 4, 26);
        for (int i = 0; i < n; i++) {
            Vector3 v = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(1.2f, 3.2f), 0f) * size;
            FxLayers.Fire.Emit(at + (Vector3)(Random.insideUnitCircle * 0.3f * size), v, Random.Range(0.5f, 0.95f) * size,
                               Random.Range(0.35f, 0.6f), Soul);
        }
        for (int i = 0; i < n / 2; i++)
            FxLayers.Ember.Emit(at, new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(1.5f, 4.5f), 0f), Random.Range(0.05f, 0.1f),
                                Random.Range(0.5f, 0.9f), Soul);
        FxLayers.Glow.Emit(at, Vector3.zero, 2.2f * size, 0.3f, Soul);
        if (light) VFX.LightFlash(pos + Vector3.back, Soul, 4f * size, 6f * size, 0.3f);
    }

    // A creature falls apart: bone chips and dust.
    public static void BoneScatter(Vector3 pos, float size) {
        VFX.Burst(Front(pos), Mathf.RoundToInt(16f * size), 3.5f * size, 0.12f, 0.8f, Bone, 1.2f, false, TexKit.SoftCircle);
        VFX.Dust(pos, 0.5f * size, new Color(0.75f, 0.72f, 0.64f, 0.5f));
    }

    // ------------------------------------------------------------------ world queries
    // The island's surface at x (the sea has none).
    public static bool GroundAt(float x, out float y) {
        if (Physics.Raycast(new Vector3(x, 80f, 0f), Vector3.down, out RaycastHit hit, 200f, Layers.GroundMask, QueryTriggerInteraction.Ignore)) {
            y = hit.point.y;
            return true;
        }
        y = World.WaterY;
        return false;
    }

    // The undefeated enemy fortress whose middle is nearest to x, or null.
    public static Fortress NearestEnemy(Fortress owner, float x) {
        if (BattleManager.I == null || owner == null) return null;
        Fortress best = null;
        float bestDistance = float.MaxValue;
        foreach (Fortress f in BattleManager.I.EnemiesOf(owner)) {
            if (f == null) continue;
            float d = Mathf.Abs(f.GetBounds().center.x - x);
            if (d < bestDistance) { bestDistance = d; best = f; }
        }
        return best;
    }

    // Stops a projectile's body where it is: no more physics, it is moved by hand from now on.
    public static void Freeze(Projectile p) {
        Rigidbody rb = p.rb;
        if (rb != null && !rb.isKinematic) {
            rb.linearVelocity = Vector3.zero;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;   // the only mode kinematic bodies support
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
        }
        foreach (Collider c in p.GetComponentsInChildren<Collider>()) c.enabled = false;
    }
}

// The Lich's own sounds, synthesised like the rest of the game's (Synth; see SoundBank for the house recipes) the
// first time each is needed, then kept. They play through the shared pool (SFX.PlayClip).
public static class LichSounds {
    static AudioClip rattle, roar, wake, rise, breath, grind;

    public static AudioClip Rattle => rattle != null ? rattle : rattle = Make("Lich rattle", RattleData());
    public static AudioClip Roar => roar != null ? roar : roar = Make("Lich dragon roar", RoarData());
    public static AudioClip Wake => wake != null ? wake : wake = Make("Lich gargoyle wake", WakeData());
    public static AudioClip Rise => rise != null ? rise : rise = Make("Lich rise", RiseData());
    public static AudioClip Breath => breath != null ? breath : breath = Make("Lich soulfire", BreathData());
    public static AudioClip Grind => grind != null ? grind : grind = Make("Lich bone grind", GrindData());

    public static void Play(AudioClip clip, Vector3? pos, float volume, float pitch = 1f) { SFX.PlayClip(clip, pos, volume, pitch); }

    // Builds all the clips at once (a Lich is in this battle), so no throw waits for them.
    public static void WarmUp() {
        _ = Rattle; _ = Roar; _ = Wake; _ = Rise; _ = Breath; _ = Grind;
    }

    static AudioClip Make(string name, float[] data) {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Synth.Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Dry bones: a clatter of clicks and hollow clacks.
    static float[] RattleData() => new Synth(0.5f, 701)
        .Crackle(0f, 0.42f, 28, 0.75f, 1300f, 4200f, 1.2f, 0.003f)
        .Pings(0f, 0.4f, 9, 0.35f, 700f, 1900f, 0.025f)
        .Done(0.8f);

    // The bone dragon: a deep growl swelling and falling away, a rasp of breath over it.
    static float[] RoarData() => new Synth(1.4f, 702)
        .Tone(0f, 1.3f, new Sweep(62f, 118f, 50f, 0.35f), 0.6f, Env.Swell(0.3f), Waveform.Saw, 7f, 0.05f,
              Filter.Low(new Sweep(450f, 1500f, 380f, 0.35f), 1.2f))
        .Tone(0f, 1.3f, new Sweep(96f, 176f, 74f, 0.35f), 0.32f, Env.Swell(0.3f), Waveform.Square, 9f, 0.06f, Filter.Low(900f, 1f))
        .Noise(0f, 1.3f, 0.5f, Env.Swell(0.3f), Filter.Band(new Sweep(500f, 1700f, 420f, 0.35f), 1.4f))
        .Saturate(1.6f)
        .Reverb(0.25f)
        .Done(0.9f);

    // A gargoyle wakes: stone grinds, then a shriek as it dives.
    static float[] WakeData() => new Synth(0.7f, 703)
        .Noise(0f, 0.25f, 0.7f, Env.Hit(0.01f, 0.18f), Filter.Band(new Sweep(260f, 700f), 1.6f))
        .Crackle(0f, 0.22f, 20, 0.45f, 300f, 1400f, 1.5f, 0.006f)
        .Tone(0.12f, 0.55f, new Sweep(1500f, 2300f, 900f, 0.3f), 0.35f, Env.Swell(0.25f), Waveform.Saw, 13f, 0.02f,
              Filter.Band(new Sweep(1800f, 2600f, 1200f, 0.3f), 2.5f))
        .Done(0.85f);

    // The earth splits and the dead rise: a deep rumble under a hollow wind.
    static float[] RiseData() => new Synth(1.1f, 704)
        .Noise(0f, 1f, 0.8f, Env.Swell(0.25f), Filter.Low(new Sweep(220f, 90f), 0.9f))
        .Tone(0f, 0.9f, new Sweep(52f, 38f), 0.5f, Env.Swell(0.2f), Waveform.Triangle)
        .Noise(0.1f, 0.9f, 0.3f, Env.Swell(0.5f), Filter.Band(new Sweep(500f, 900f, 450f, 0.5f), 3f))
        .Reverb(0.3f)
        .Done(0.85f);

    // Soulfire breath: a long, roaring rush.
    static float[] BreathData() => new Synth(1.2f, 706)
        .Noise(0f, 1.15f, 0.85f, Env.Swell(0.2f), Filter.Band(new Sweep(420f, 1400f, 700f, 0.3f), 1.1f))
        .Noise(0f, 1.15f, 0.4f, Env.Swell(0.3f), Filter.Low(260f, 0.8f))
        .Crackle(0.05f, 1f, 40, 0.25f, 800f, 3000f, 1.5f, 0.003f)
        .Done(0.85f);

    // The bone hand dragging everything down: a deep grinding groan, bones creaking and earth pouring.
    static float[] GrindData() => new Synth(1.5f, 707)
        .Tone(0f, 1.45f, new Sweep(70f, 48f), 0.5f, Env.Swell(0.25f), Waveform.Saw, 5f, 0.06f, Filter.Low(new Sweep(420f, 220f), 1.1f))
        .Noise(0f, 1.45f, 0.6f, Env.Swell(0.3f), Filter.Low(new Sweep(300f, 120f), 0.8f))
        .Crackle(0f, 1.4f, 50, 0.35f, 500f, 2200f, 1.5f, 0.005f)
        .Saturate(1.4f)
        .Reverb(0.25f)
        .Done(0.9f);
}

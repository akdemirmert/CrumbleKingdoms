using UnityEngine;

// Loki: Trickster Daggers. One throw, four daggers: the real one flies exactly where you aimed and three
// illusions fan out around it. All of them pierce (PiercingProjectile). The illusions hit softer and vanish in
// a puff of green magic when they stop; the real dagger stays stuck in whatever stopped it.
public class LokiDagger : PiercingProjectile {
    static readonly float[] IllusionAngles = { -7f, 6f, 12f };   // degrees above (+) / below (-) the aim
    const float AngleJitter = 1.5f;
    const float IllusionDamage = 0.6f;     // share of def.damage
    const float EmbedDepth = 0.1f;         // the blade sinks in up to the guard

    // Blade profile: stations along +X and the half width at each, starting just in front of the guard.
    static readonly float[] BladeX = { 0f, 0.06f, 0.22f, 0.36f, 0.46f };
    static readonly float[] BladeW = { 0.035f, 0.052f, 0.044f, 0.026f, 0f };
    const float BladeStart = -0.03f;

    public bool illusion;

    GameObject trail;
    Light glow;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        SFX.Play(SoundId.KnifeThrow, origin, 0.9f);
        SFX.Play(SoundId.Illusion, origin, 0.8f);
        VFX.MagicBurst(origin, GodKit.LokiGreen, 0.5f);
        WeaponFactory.Spawn<LokiDagger>("Loki Dagger", owner, def, origin, velocity, d => d.embedDepth = EmbedDepth);
        float up = velocity.x >= 0f ? 1f : -1f;   // positive angles lift the throw on either side of the map
        foreach (float angle in IllusionAngles) {
            float a = (angle + Random.Range(-AngleJitter, AngleJitter)) * up;
            Vector3 v = Quaternion.Euler(0f, 0f, a) * velocity * Random.Range(0.96f, 1.04f);
            WeaponFactory.Spawn<LokiDagger>("Loki Illusion", owner, def, origin, v, d => {
                d.illusion = true;
                d.damage = def.damage * IllusionDamage;
                d.embedDepth = EmbedDepth;
                d.cameraFollows = false;          // the camera follows the real dagger
            });
        }
    }

    public static GameObject Model(Transform parent) { return BuildModel(parent, false); }

    protected override void OnLaunch(Vector3 velocity) {
        base.OnLaunch(velocity);
        BuildModel(transform, illusion);
        Color trailColor = illusion ? new Color(0.45f, 1f, 0.6f, 0.5f) : GodKit.LokiGreen;
        trail = VFX.Trail(transform, trailColor, illusion ? 0.12f : 0.16f, 0.25f);
        trail.transform.localPosition = new Vector3(-0.3f, 0f, 0f);   // streams from the pommel
        if (!illusion) glow = GodKit.Glow(transform, GodKit.LokiGreen, 1.6f, 3f);
        Stopped += OnStopped;
    }

    void OnStopped(PiercingProjectile self, Vector3 point, Collider hit) {
        GodKit.Release(trail);
        if (glow != null) Destroy(glow.gameObject);
        if (!illusion) {
            GodKit.DropIfFloating(this, hit);
            return;
        }
        // An illusion never sticks: it bursts into green magic.
        VFX.MagicBurst(point, GodKit.LokiGreen, 0.7f);
        SFX.Play(SoundId.Illusion, point, 0.6f, 1.2f);
        Destroy(gameObject);
    }

    // Gold-green dagger with a horned guard (Loki's helmet). The illusion is a see-through green copy
    // with glowing edges.
    static GameObject BuildModel(Transform parent, bool illusion) {
        GameObject root = ModelKit.Root(illusion ? "Loki Illusion" : "Loki Dagger", parent);
        Transform r = root.transform;
        Material steel, gold, grip, glow;
        if (illusion) {
            steel = gold = grip = Mats.Transparent(new Color(0.5f, 1f, 0.65f, 0.45f), 0.9f);
            glow = Mats.Emissive(GodKit.LokiGreen, GodKit.LokiGreen * 3f);
        } else {
            steel = Mats.Lit(new Color(0.76f, 0.9f, 0.8f), 0.8f, 0.5f);
            gold = GodKit.GoldMetal;
            grip = Mats.Lit(new Color(0.1f, 0.32f, 0.18f), 0.3f);
            glow = Mats.Emissive(new Color(0.25f, 0.7f, 0.4f), GodKit.LokiGreen * 2.5f);
        }

        ModelKit.MeshPart(r, GodKit.Blade("lokiBlade", BladeX, BladeW, 0.02f), new Vector3(BladeStart, 0f, 0f), Vector3.one, steel, default, "Blade");
        ModelKit.Part(r, PrimitiveType.Cube, new Vector3(0.15f, 0f, 0f), new Vector3(0.26f, 0.01f, 0.05f), glow);   // glowing fuller
        if (illusion) {
            for (int i = 0; i + 1 < BladeX.Length; i++)
                for (int s = -1; s <= 1; s += 2)
                    GodKit.Rod(r, new Vector3(BladeStart + BladeX[i], s * BladeW[i], 0f),
                               new Vector3(BladeStart + BladeX[i + 1], s * BladeW[i + 1], 0f), 0.012f, glow, PrimitiveType.Cube);
        }

        ModelKit.Part(r, PrimitiveType.Cube, new Vector3(-0.045f, 0f, 0f), new Vector3(0.035f, 0.2f, 0.06f), gold);   // guard
        Mesh horn = MeshKit.Cone(6, 0.02f, 0.1f);
        ModelKit.MeshPart(r, horn, new Vector3(-0.045f, 0.095f, 0f), Vector3.one, gold, new Vector3(0f, 0f, -50f), "Horn");
        ModelKit.MeshPart(r, horn, new Vector3(-0.045f, -0.095f, 0f), Vector3.one, gold, new Vector3(0f, 0f, -130f), "Horn");
        GodKit.Rod(r, new Vector3(-0.29f, 0f, 0f), new Vector3(-0.06f, 0f, 0f), 0.05f, grip);
        GodKit.Rod(r, new Vector3(-0.27f, 0f, 0f), new Vector3(-0.25f, 0f, 0f), 0.06f, gold);                           // grip ring
        ModelKit.Part(r, PrimitiveType.Sphere, new Vector3(-0.31f, 0f, 0f), Vector3.one * 0.07f, gold);                   // pommel
        ModelKit.Part(r, PrimitiveType.Sphere, new Vector3(-0.335f, 0f, 0f), Vector3.one * 0.045f, glow);                 // emerald
        if (illusion) ModelKit.Shadows(root, false);   // a mirage casts no shadow
        return root;
    }
}

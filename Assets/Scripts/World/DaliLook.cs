using UnityEngine;

// Dalí's look, registered with ClassLooks before the first scene loads: the commander (DaliCommander, below), the
// fortress block skins (DaliSkins, DaliLookSkins.cs) and the fortress decorations (DaliDecor, DaliLookDecor.cs).
// Shared shapes and textures live in DaliLookArt.cs.
public static class DaliLook {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() { ClassLooks.Register(HeroClass.Dali, DaliCommander.Build, DaliSkins.Block, DaliDecor.Apply); }
}

// The surrealist painter, an affectionate caricature: slicked-back black hair, wide astonished eyes, the long waxed
// mustache with its points turned up to the brows (a separate object, rig.mustache: he throws it), an ochre-gold
// frock coat with chocolate lapels and gold buttons, an ivory cravat, and a long paintbrush he leans on like a
// cane. The team colour is only the paint on the brush and the flower in his buttonhole.
// Same rig contract and chibi proportions as CommanderModel: ~1.5 m (the brush reaches higher), feet at y = 0,
// facing the camera turned a little towards the enemy (mirrored for facing -1), no colliders.
public static class DaliCommander {
    const float BodyYaw = -15f, HeadYaw = -12f;          // negative yaw turns the face towards +x
    const float NeckY = 0.94f, ArmLength = 0.46f;
    static readonly Vector3 ShoulderBody = new Vector3(0.27f, 0.86f, 0f);
    static readonly Vector3 Octagon = new Vector3(0f, 22.5f, 0f);   // turns an octagonal frustum flat side to the camera

    static readonly Color Skin = new Color(0.94f, 0.75f, 0.6f);
    static readonly Color CoatOchre = new Color(0.86f, 0.58f, 0.2f);      // the class colour, a shade deeper
    static readonly Color Chocolate = new Color(0.3f, 0.17f, 0.08f);
    static readonly Color Trousers = new Color(0.17f, 0.14f, 0.14f);
    static readonly Color Ivory = new Color(0.97f, 0.94f, 0.86f);
    static readonly Color Lacquer = new Color(0.45f, 0.13f, 0.08f);       // the brush handle

    class Materials {
        public Material skin, coat, lapel, gold, ivory, trousers, shoes, wax, eyeWhite, pupil, team;
    }

    public static CommanderRig Build(Color team, int facing, Transform parent) {
        GameObject root = ModelKit.Root("Model", parent);
        root.transform.localScale = new Vector3(facing >= 0 ? 1f : -1f, 1f, 1f);
        CommanderRig rig = root.AddComponent<CommanderRig>();
        rig.armRest = 8f;   // hanging, slightly away from the body
        DaliIdle idle = root.AddComponent<DaliIdle>();

        Quaternion yaw = Quaternion.Euler(0f, BodyYaw, 0f);
        rig.body = Pivot("Body", root.transform, Vector3.zero, new Vector3(0f, BodyYaw, 0f));
        idle.torso = Pivot("Torso", rig.body, Vector3.zero);
        rig.head = Pivot("Head", rig.body, new Vector3(0f, NeckY, 0f), new Vector3(0f, HeadYaw, 0f));
        // The throwing arm swings in the root's x-y plane, just in front of the body (see CommanderModel).
        Vector3 shoulder = yaw * ShoulderBody;
        rig.armPivot = Pivot("ArmPivot", root.transform, new Vector3(shoulder.x, shoulder.y, -0.27f));
        rig.hand = Pivot("Hand", rig.armPivot, new Vector3(0f, -ArmLength, 0f));

        Materials m = new Materials {
            skin = Mats.Lit(Skin, 0.3f), coat = Mats.Lit(CoatOchre, 0.4f), lapel = Mats.Lit(Chocolate, 0.45f),
            gold = DaliArt.GoldMetal, ivory = Mats.Lit(Ivory, 0.5f), trousers = Mats.Lit(Trousers, 0.25f),
            shoes = Mats.Lit(new Color(0.06f, 0.05f, 0.05f), 0.85f), wax = DaliArt.Wax,
            eyeWhite = Mats.Lit(new Color(0.98f, 0.97f, 0.95f), 0.6f), pupil = Mats.Lit(new Color(0.12f, 0.08f, 0.05f), 0.8f),
            team = Mats.Lit(team, 0.4f),
        };
        Legs(rig.body, m);
        Coat(idle.torso, rig.body, m);
        Arm(rig.armPivot, m);
        Transform offArm = Pivot("OffArm", rig.body, new Vector3(-ShoulderBody.x, ShoulderBody.y, -0.02f), new Vector3(0f, 0f, -4f));
        Arm(offArm, m);                                   // the off hand closes on the brush
        Head(rig, idle, m);
        rig.prop = PaintBrush(rig.body, m);

        rig.renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        return rig;
    }

    // ------------------------------------------------------------------ body
    // Dark trousers under the coat and glossy pointed shoes.
    static void Legs(Transform body, Materials m) {
        for (int s = -1; s <= 1; s += 2) {
            Part(body, WorldMeshes.Frustum(6, 0.08f, 0.078f, 0.3f), new Vector3(s * 0.11f, 0.06f, 0f), Vector3.one, m.trousers);
            Part(body, WorldMeshes.Frustum(6, 0.09f, 0.075f, 0.1f), new Vector3(s * 0.11f, 0f, -0.02f), new Vector3(1f, 1f, 1.3f), m.shoes);
            Part(body, WorldMeshes.Ball(0.06f), new Vector3(s * 0.11f, 0.035f, -0.13f), new Vector3(0.85f, 0.55f, 1.5f), m.shoes);   // pointed toe
        }
    }

    // An ochre frock coat flaring to the knees with a gold hem, chocolate lapels over an ivory shirt, gold buttons,
    // a high collar with a puffed ivory cravat and a gold pin, and a team-colour flower in the buttonhole.
    static void Coat(Transform torso, Transform body, Materials m) {
        Part(torso, WorldMeshes.Frustum(8, 0.3f, 0.245f, 0.37f), new Vector3(0f, 0.1f, 0f), new Vector3(1f, 1f, 0.84f), m.coat, Octagon);    // skirt
        Part(torso, WorldMeshes.Frustum(8, 0.306f, 0.3f, 0.04f), new Vector3(0f, 0.095f, 0f), new Vector3(1f, 1f, 0.85f), m.gold, Octagon);  // gold hem
        Part(torso, WorldMeshes.Frustum(8, 0.25f, 0.215f, 0.5f), new Vector3(0f, 0.44f, 0f), new Vector3(1f, 1f, 0.82f), m.coat, Octagon);   // body
        Part(torso, WorldMeshes.Frustum(8, 0.252f, 0.25f, 0.035f), new Vector3(0f, 0.45f, 0f), new Vector3(1f, 1f, 0.83f), m.lapel, Octagon); // waist seam
        for (int s = -1; s <= 1; s += 2) {
            Part(torso, WorldMeshes.Ball(0.105f), new Vector3(s * 0.24f, 0.86f, 0f), new Vector3(1.1f, 0.72f, 1.05f), m.coat);                 // shoulders
            Part(torso, PrimitiveType.Cube, new Vector3(s * 0.07f, 0.74f, -0.186f), new Vector3(0.075f, 0.33f, 0.02f), m.lapel, new Vector3(0f, 0f, -s * 17f));
            Part(torso, PrimitiveType.Cube, new Vector3(s * 0.105f, 0.74f, -0.19f), new Vector3(0.01f, 0.33f, 0.01f), m.gold, new Vector3(0f, 0f, -s * 17f));
            for (int k = 0; k < 3; k++)
                Part(torso, WorldMeshes.Ball(0.018f), new Vector3(s * 0.075f, 0.5f + k * 0.075f, -0.19f), Vector3.one, m.gold);              // buttons
        }
        Part(torso, PrimitiveType.Cube, new Vector3(0f, 0.76f, -0.178f), new Vector3(0.12f, 0.3f, 0.02f), m.ivory);                         // shirt front
        Part(body, WorldMeshes.Frustum(8, 0.13f, 0.115f, 0.08f), new Vector3(0f, 0.88f, 0f), Vector3.one, m.ivory);                          // collar
        Part(body, WorldMeshes.Ball(0.07f), new Vector3(0f, 0.9f, -0.15f), new Vector3(1.25f, 0.8f, 0.8f), m.ivory);                         // cravat knot
        for (int s = -1; s <= 1; s += 2)
            Part(body, WorldMeshes.Ball(0.05f), new Vector3(s * 0.03f, 0.81f, -0.18f), new Vector3(0.75f, 1.6f, 0.5f), m.ivory, new Vector3(0f, 0f, s * 12f));
        Part(body, WorldMeshes.Ball(0.016f), new Vector3(0f, 0.9f, -0.205f), Vector3.one, m.gold);                                           // pin
        Part(torso, WorldMeshes.Ball(0.036f), new Vector3(-0.115f, 0.82f, -0.2f), Vector3.one, m.team);                                       // buttonhole flower
    }

    // An arm hanging from its pivot: shoulder ball, ochre sleeve, embroidered gold cuff, a lace frill, the hand.
    static void Arm(Transform pivot, Materials m) {
        Part(pivot, WorldMeshes.Ball(0.095f), Vector3.zero, Vector3.one, m.coat);
        Part(pivot, WorldMeshes.Frustum(6, 0.08f, 0.074f, 0.36f), new Vector3(0f, -0.4f, 0f), Vector3.one, m.coat);
        Part(pivot, WorldMeshes.Frustum(6, 0.087f, 0.085f, 0.07f), new Vector3(0f, -0.41f, 0f), Vector3.one, m.gold);
        Part(pivot, WorldMeshes.Frustum(6, 0.05f, 0.075f, 0.04f), new Vector3(0f, -0.445f, 0f), Vector3.one, m.ivory);
        Part(pivot, WorldMeshes.Ball(0.072f), new Vector3(0f, -0.47f, 0f), Vector3.one, m.skin);
    }

    // ------------------------------------------------------------------ head
    static void Head(CommanderRig rig, DaliIdle idle, Materials m) {
        Transform head = rig.head;
        Part(head, WorldMeshes.Ball(0.25f), new Vector3(0f, 0.24f, 0f), Vector3.one, m.skin);
        for (int s = -1; s <= 1; s += 2) {
            Part(head, WorldMeshes.Ball(0.06f), new Vector3(s * 0.25f, 0.24f, 0.01f), Vector3.one, m.skin);                                   // ears
            Part(head, WorldMeshes.Ball(0.05f), new Vector3(s * 0.15f, 0.17f, -0.19f), new Vector3(1f, 0.6f, 0.5f), Mats.Lit(new Color(1f, 0.6f, 0.58f), 0.3f));
            Part(head, PrimitiveType.Cube, new Vector3(s * 0.235f, 0.27f, -0.06f), new Vector3(0.03f, 0.1f, 0.06f), m.wax);                   // sideburns
        }
        // Slicked-back hair: a glossy cap tilted back (a high hairline), with a little wave on top.
        Part(head, WorldMeshes.Dome(12, 4, 0.262f), new Vector3(0f, 0.24f, 0f), new Vector3(1f, 1.08f, 1f), m.wax, new Vector3(55f, 0f, 0f));
        Part(head, WorldMeshes.Ball(0.12f), new Vector3(0f, 0.47f, -0.06f), new Vector3(1.15f, 0.42f, 0.9f), m.wax);

        // Wide, astonished eyes: big whites, small dark pupils glancing towards the enemy, brows arched high.
        Transform eyes = Pivot("Eyes", head, new Vector3(0f, 0.275f, 0f));
        idle.eyes = eyes;
        for (int s = -1; s <= 1; s += 2) {
            Part(eyes, WorldMeshes.Ball(0.07f), new Vector3(s * 0.09f, 0f, -0.2f), new Vector3(1f, 1.15f, 0.6f), m.eyeWhite);
            Part(eyes, WorldMeshes.Ball(0.026f), new Vector3(s * 0.09f + 0.012f, 0.004f, -0.236f), Vector3.one, m.pupil);
            Part(head, PrimitiveType.Cube, new Vector3(s * 0.095f, 0.385f, -0.205f), new Vector3(0.11f, 0.022f, 0.025f), m.wax, new Vector3(0f, 0f, -s * 16f));
        }
        Part(head, WorldMeshes.Ball(0.055f), new Vector3(0f, 0.2f, -0.25f), new Vector3(0.9f, 1.1f, 1f), m.skin);                             // nose
        Part(head, PrimitiveType.Cube, new Vector3(0f, 0.105f, -0.215f), new Vector3(0.07f, 0.014f, 0.02f), Mats.Lit(new Color(0.45f, 0.18f, 0.16f), 0.3f));

        // The mustache: two waxed halves with their points turned up, a separate object (he throws it).
        Transform mustache = Pivot("Mustache", head, new Vector3(0f, 0.155f, -0.258f));
        Part(mustache, DaliArt.MustacheHalf(), Vector3.zero, Vector3.one, m.wax);
        Part(mustache, DaliArt.MustacheHalf(), Vector3.zero, new Vector3(-1f, 1f, 1f), m.wax);
        Part(mustache, WorldMeshes.Ball(0.036f), Vector3.zero, new Vector3(1.3f, 0.9f, 0.9f), m.wax);
        rig.mustache = mustache.gameObject;
        idle.mustache = mustache;
    }

    // A long paintbrush planted like a cane beside him, its bristles loaded with team-colour paint.
    static Transform PaintBrush(Transform body, Materials m) {
        Transform brush = Pivot("Brush", body, new Vector3(-0.33f, 0f, -0.12f), new Vector3(0f, 0f, 4f));
        Part(brush, WorldMeshes.Ball(0.03f), new Vector3(0f, 0.02f, 0f), Vector3.one, m.gold);
        Part(brush, WorldMeshes.Frustum(6, 0.022f, 0.027f, 1f), new Vector3(0f, 0.02f, 0f), Vector3.one, Mats.Lit(Lacquer, 0.75f));
        Part(brush, WorldMeshes.Frustum(8, 0.034f, 0.042f, 0.11f), new Vector3(0f, 1f, 0f), Vector3.one, m.gold);                            // ferrule
        Part(brush, WorldMeshes.Frustum(8, 0.044f, 0.042f, 0.09f), new Vector3(0f, 1.11f, 0f), Vector3.one, Mats.Lit(new Color(0.85f, 0.72f, 0.5f), 0.2f));
        Part(brush, WorldMeshes.Frustum(8, 0.042f, 0.006f, 0.15f), new Vector3(0f, 1.2f, 0f), Vector3.one, m.team);                            // the paint
        Part(brush, WorldMeshes.Ball(0.02f), new Vector3(0.03f, 1.16f, -0.025f), new Vector3(0.8f, 1.6f, 0.8f), m.team);                       // a drip
        return brush;
    }

    // ------------------------------------------------------------------ helpers
    static Transform Pivot(string name, Transform parent, Vector3 localPos, Vector3 euler = default) {
        Transform t = ModelKit.Root(name, parent).transform;
        t.localPosition = localPos;
        t.localRotation = Quaternion.Euler(euler);
        return t;
    }

    static GameObject Part(Transform parent, Mesh mesh, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default) {
        return ModelKit.MeshPart(parent, mesh, pos, scale, mat, euler);
    }

    static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default) {
        return ModelKit.Part(parent, type, pos, scale, mat, euler);
    }
}

// Dalí's idle life: breathing, blinking his big eyes and, now and then, a proud twitch of the mustache. Eyes shut
// while he is dead; a time rewind can bring him back, so this keeps checking. Also runs without a Commander
// (menu, icons).
public class DaliIdle : MonoBehaviour {
    public Transform torso, eyes, mustache;
    Commander owner;
    float seed, nextBlink, blinkUntil, nextTwitch, twitchAt = -10f;

    void Start() {
        owner = GetComponentInParent<Commander>();
        seed = Random.value * 10f;
        nextBlink = Time.time + Random.Range(1f, 3f);
        nextTwitch = Time.time + Random.Range(2f, 5f);
    }

    void Update() {
        if (owner != null && !owner.IsAlive) {
            if (eyes != null) eyes.localScale = new Vector3(1f, 0.12f, 1f);
            return;
        }
        float now = Time.time;
        if (torso != null) torso.localScale = new Vector3(1f, 1f + 0.018f * Mathf.Sin((now + seed) * 2.4f), 1f);
        if (eyes != null) {
            if (now >= nextBlink) {
                blinkUntil = now + 0.1f;
                nextBlink = now + Random.Range(2.5f, 5.5f);
            }
            eyes.localScale = new Vector3(1f, now < blinkUntil ? 0.12f : 1f, 1f);
        }
        if (mustache != null) {
            if (now >= nextTwitch) {
                twitchAt = now;
                nextTwitch = now + Random.Range(3f, 7f);
            }
            float k = Mathf.Clamp01((now - twitchAt) / 0.35f);
            float bump = Mathf.Sin(k * Mathf.PI * 2f) * (1f - k);
            mustache.localScale = new Vector3(1f + 0.06f * bump, 1f + 0.14f * bump, 1f);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

// The Alien class look, registered with ClassLooks: the commander (this file), the fortress block skins
// (AlienLookSkin.cs) and the fortress decorations (AlienLookDecor.cs).
// The commander: a small green alien with a big head, huge glossy black eyes and two antennae with glowing tips
// poking out of a glass bubble helmet, a silver jumpsuit with glowing green trim and moon boots, and a little ray
// gun in the off hand. The team colour is only the belt and the rim of the chest emblem.
// Rig contract as in CommanderModel: about 1.65 m (the antennae reach 1.8), feet at y = 0, facing the camera
// (-z) turned a little towards the enemy, mirrored for facing -1; armPivot at the throwing shoulder with the arm
// hanging along -Y at rotation 0, the hand at its end. No colliders.
public static class AlienLook {
    const float BodyYaw = -15f, HeadYaw = -12f;          // negative yaw turns the face towards +x
    const float NeckY = 0.94f, ArmLength = 0.46f;
    static readonly Vector3 ShoulderBody = new Vector3(0.25f, 0.85f, 0f);

    static readonly Color Skin = new Color(0.36f, 0.86f, 0.53f);       // the class green, a little lighter
    static readonly Color EyeBlack = new Color(0.02f, 0.02f, 0.03f);
    static readonly Color Silver = new Color(0.8f, 0.83f, 0.88f), Boots = new Color(0.25f, 0.27f, 0.31f);
    static readonly Color Trim = new Color(0.35f, 1f, 0.6f), TipGlow = new Color(0.75f, 1f, 0.35f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() { ClassLooks.Register(HeroClass.Alien, Commander, AlienSkin.Block, AlienDecor.Apply); }

    // Materials of the model.
    class Look {
        public Material skin, eye, suit, boots, team, trim, tip, glass, shine;
    }

    public static CommanderRig Commander(Color teamColor, int facing, Transform parent) {
        GameObject root = ModelKit.Root("Model", parent);
        root.transform.localScale = new Vector3(facing >= 0 ? 1f : -1f, 1f, 1f);
        CommanderRig rig = root.AddComponent<CommanderRig>();
        rig.armRest = 8f;
        CommanderIdle idle = root.AddComponent<CommanderIdle>();
        Look l = new Look {
            skin = Mats.Lit(Skin, 0.45f), eye = Mats.Lit(EyeBlack, 0.95f), suit = Mats.Lit(Silver, 0.7f, 0.35f),
            boots = Mats.Lit(Boots, 0.45f, 0.3f), team = Mats.Lit(teamColor, 0.35f), trim = Mats.Emissive(Trim * 0.6f, Trim * 2.4f),
            tip = Mats.Emissive(TipGlow * 0.7f, TipGlow * 4f), glass = Mats.Transparent(new Color(0.72f, 0.95f, 1f, 0.17f), 0.95f),
            shine = Mats.Emissive(Color.white, Color.white * 1.6f),
        };

        Quaternion yaw = Quaternion.Euler(0f, BodyYaw, 0f);
        rig.body = Pivot("Body", root.transform, Vector3.zero, new Vector3(0f, BodyYaw, 0f));
        idle.torso = Pivot("Torso", rig.body, Vector3.zero);
        rig.head = Pivot("Head", rig.body, new Vector3(0f, NeckY, 0f), new Vector3(0f, HeadYaw, 0f));
        // The throwing arm swings in the x-y plane of the root, just in front of the body (as in CommanderModel).
        Vector3 shoulder = yaw * ShoulderBody;
        rig.armPivot = Pivot("ArmPivot", root.transform, new Vector3(shoulder.x, shoulder.y, -0.25f));
        rig.hand = Pivot("Hand", rig.armPivot, new Vector3(0f, -ArmLength, 0f));

        Suit(rig.body, idle.torso, l);
        Arm(rig.armPivot, l);
        Transform offArm = Pivot("OffArm", rig.body, new Vector3(-ShoulderBody.x, ShoulderBody.y, -0.02f), new Vector3(0f, 0f, -8f));
        Arm(offArm, l);
        Transform gun = AlienKit.RayGunModel(offArm).transform;     // raised in the off hand, beside the body
        gun.localPosition = new Vector3(0f, -0.47f, -0.05f);
        gun.localRotation = Quaternion.Euler(0f, 0f, 75f);
        gun.localScale = Vector3.one * 0.42f;
        rig.prop = gun;
        Head(rig.head, idle, l);

        // The glass bubble stays out of the damage flash: flashing it would turn it into an opaque red ball.
        List<Renderer> flash = new List<Renderer>();
        foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
            if (r.sharedMaterial != l.glass && r.sharedMaterial != l.shine) flash.Add(r);
        rig.renderers = flash.ToArray();
        return rig;
    }

    // Moon boots, thin silver legs, a silver suit with a team-colour belt, a glowing hexagonal emblem on the chest
    // (team-colour rim), glowing trim down the front and round shoulder pads.
    static void Suit(Transform body, Transform torso, Look l) {
        for (int s = -1; s <= 1; s += 2) {
            Part(body, WorldMeshes.Frustum(6, 0.065f, 0.07f, 0.28f), new Vector3(s * 0.1f, 0.1f, 0f), l.suit);
            Part(body, WorldMeshes.Frustum(8, 0.105f, 0.085f, 0.15f), new Vector3(s * 0.1f, 0f, -0.03f), l.boots, default, new Vector3(1f, 1f, 1.3f));
            Part(body, WorldMeshes.Frustum(8, 0.087f, 0.087f, 0.025f), new Vector3(s * 0.1f, 0.13f, -0.03f), l.trim, default, new Vector3(1f, 1f, 1.3f));
        }
        Vector3 octagon = new Vector3(0f, 22.5f, 0f);
        Part(torso, WorldMeshes.Frustum(8, 0.22f, 0.19f, 0.62f), new Vector3(0f, 0.33f, 0f), l.suit, octagon, new Vector3(1f, 1f, 0.84f));
        Part(torso, WorldMeshes.Frustum(8, 0.232f, 0.228f, 0.07f), new Vector3(0f, 0.42f, 0f), l.team, octagon, new Vector3(1f, 1f, 0.85f));
        Part(torso, WorldMeshes.Frustum(8, 0.2f, 0.17f, 0.06f), new Vector3(0f, 0.92f, 0f), l.suit, octagon, new Vector3(1f, 1f, 0.86f));   // collar
        float front = -0.16f;                                    // the suit's camera-facing side at chest height
        Part(torso, WorldMeshes.Disc(6, 0.075f), new Vector3(0f, 0.7f, front - 0.004f), l.team, new Vector3(0f, 0f, 30f));
        Part(torso, WorldMeshes.Disc(6, 0.055f), new Vector3(0f, 0.7f, front - 0.01f), l.trim, new Vector3(0f, 0f, 30f));
        Part(torso, PrimitiveType.Cube, new Vector3(0f, 0.53f, -0.168f), new Vector3(0.025f, 0.15f, 0.012f), l.trim);   // trim down to the belt
        Part(torso, WorldMeshes.Disc(6, 0.035f), new Vector3(0f, 0.455f, -0.188f), l.trim, new Vector3(0f, 0f, 30f));      // buckle
        for (int s = -1; s <= 1; s += 2)
            Part(torso, WorldMeshes.Dome(8, 2, 0.1f), new Vector3(s * 0.2f, 0.86f, 0f), l.suit, new Vector3(0f, 0f, -s * 25f), new Vector3(1f, 0.6f, 1f));
    }

    // An arm hanging from its pivot: shoulder ball, sleeve with a glowing cuff, a green three-fingered hand.
    static void Arm(Transform pivot, Look l) {
        Part(pivot, WorldMeshes.Ball(0.085f), Vector3.zero, l.suit);
        Part(pivot, WorldMeshes.Frustum(6, 0.066f, 0.07f, 0.36f), new Vector3(0f, -0.4f, 0f), l.suit);
        Part(pivot, WorldMeshes.Frustum(6, 0.074f, 0.074f, 0.03f), new Vector3(0f, -0.4f, 0f), l.trim);
        Part(pivot, WorldMeshes.Ball(0.068f), new Vector3(0f, -0.45f, 0f), l.skin);
        for (int k = -1; k <= 1; k++)
            Part(pivot, WorldMeshes.Ball(0.026f), new Vector3(k * 0.035f, -0.51f, -0.01f), l.skin, default, new Vector3(1f, 1.5f, 1f));
    }

    // The big green head with huge glossy almond eyes, two nostrils and a small mouth; the glass bubble around
    // it on a silver collar; two antennae with glowing tips poking out of the bubble.
    static void Head(Transform head, CommanderIdle idle, Look l) {
        Part(head, WorldMeshes.Frustum(6, 0.06f, 0.05f, 0.1f), new Vector3(0f, -0.03f, 0f), l.skin);
        Part(head, WorldMeshes.Ball(0.25f), new Vector3(0f, 0.27f, 0f), l.skin, default, new Vector3(0.98f, 1.1f, 0.95f));
        Transform eyes = Pivot("Eyes", head, new Vector3(0f, 0.26f, 0f));
        idle.eyes = eyes;
        for (int s = -1; s <= 1; s += 2) {
            Part(eyes, WorldMeshes.Ball(0.072f), new Vector3(s * 0.105f, 0f, -0.205f), l.eye, new Vector3(0f, 0f, s * 24f), new Vector3(1.4f, 0.82f, 0.5f));
            Part(eyes, WorldMeshes.Ball(0.016f), new Vector3(s * 0.105f + 0.03f, 0.025f, -0.238f), l.shine);
            Part(head, WorldMeshes.Ball(0.012f), new Vector3(s * 0.02f, 0.19f, -0.228f), l.eye);
        }
        Part(head, PrimitiveType.Cube, new Vector3(0f, 0.12f, -0.2f), new Vector3(0.07f, 0.012f, 0.012f), Mats.Lit(new Color(0.12f, 0.35f, 0.2f), 0.3f));

        Part(head, WorldMeshes.Frustum(12, 0.22f, 0.2f, 0.06f), new Vector3(0f, -0.02f, 0f), l.suit);   // helmet collar
        GameObject bubble = Part(head, PrimitiveType.Sphere, new Vector3(0f, 0.29f, 0f), Vector3.one * 0.66f, l.glass);
        ModelKit.Shadows(bubble, false);
        Part(head, WorldMeshes.Ball(0.05f), new Vector3(-0.165f, 0.488f, -0.205f), l.shine, new Vector3(0f, 0f, 40f), new Vector3(1.6f, 0.45f, 0.3f));

        Transform antennae = Pivot("Antennae", head, new Vector3(0f, 0.52f, 0f));
        idle.plume = antennae;                                   // sways gently
        for (int s = -1; s <= 1; s += 2) {
            Vector3 tip = new Vector3(s * 0.15f, 0.26f, 0.02f);
            AlienKit.Rod(antennae, new Vector3(s * 0.06f, 0f, 0f), tip, 0.024f, l.skin);
            GameObject light = Part(antennae, WorldMeshes.Ball(0.042f), tip, l.tip);
            AlienBlink.Add(light, 1.6f, s < 0 ? 0f : 0.5f, true, 0.85f, 1.35f);
        }
    }

    static Transform Pivot(string name, Transform parent, Vector3 localPos, Vector3 euler = default) {
        Transform t = ModelKit.Root(name, parent).transform;
        t.localPosition = localPos;
        t.localRotation = Quaternion.Euler(euler);
        return t;
    }

    static GameObject Part(Transform parent, Mesh mesh, Vector3 pos, Material m, Vector3 euler = default, Vector3? scale = null) {
        return ModelKit.MeshPart(parent, mesh, pos, scale ?? Vector3.one, m, euler);
    }

    static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default) {
        return ModelKit.Part(parent, type, pos, scale, m, euler);
    }
}

// Makes a small glowing part blink (a quick flash and a pause: antenna tips) or pulse smoothly (beacon cores)
// by scaling it.
public class AlienBlink : MonoBehaviour {
    public float period = 1.4f, phase, min = 0.55f, max = 1.2f;
    public bool sharp = true;
    Vector3 size;

    public static AlienBlink Add(GameObject go, float period, float phase, bool sharp, float min = 0.55f, float max = 1.2f) {
        AlienBlink b = go.AddComponent<AlienBlink>();
        b.period = period;
        b.phase = phase;
        b.sharp = sharp;
        b.min = min;
        b.max = max;
        return b;
    }

    void Start() { size = transform.localScale; }

    void Update() {
        float t = Mathf.Repeat(Time.time / period + phase, 1f);
        float k = sharp ? (t < 0.2f ? Mathf.Sin(t / 0.2f * Mathf.PI) : 0f) : 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f);
        transform.localScale = size * Mathf.Lerp(min, max, k);
    }
}

using UnityEngine;

// The Lich's look, registered with ClassLooks: the commander (this file), the block skins (LichLookSkins.cs) and the
// fortress decorations (LichLookDecor.cs).
// The commander is an undying sorcerer-king: a bare skull with green fire in its sockets and a grinning jaw under a
// tarnished gold crown set with green gems; a long, tattered black robe trimmed with bone-white, its open front
// showing the ribs; skull pauldrons; bony hands; and a staff of black wood crowned with a skull holding a soul orb.
// The team colour is only the crown's front gem and a sash at the waist.
// Same rig contract and chibi proportions as CommanderModel (~1.65 m, feet at y = 0, faces -z turned a little
// towards the enemy, mirrored for facing -1, the throwing arm hangs along -y at rotation 0, no colliders).
public static class LichLook {
    const float BodyYaw = -15f, HeadYaw = -12f;          // negative yaw turns the face towards +x
    const float NeckY = 0.94f, ArmLength = 0.46f;
    static readonly Vector3 ShoulderBody = new Vector3(0.27f, 0.86f, 0f);

    static readonly Color RobeBlack = new Color(0.08f, 0.09f, 0.085f);
    static readonly Color RobeTrim = new Color(0.78f, 0.75f, 0.66f);
    static readonly Color Wood = new Color(0.12f, 0.1f, 0.09f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() {
        ClassLooks.Register(HeroClass.Lich, BuildCommander, LichSkins.Block, LichDecor.Apply);
    }

    class Look {
        public Material bone, boneDark, robe, trim, gold, team, socket, soul, wood;
    }

    public static CommanderRig BuildCommander(Color teamColor, int facing, Transform parent) {
        GameObject root = ModelKit.Root("Model", parent);
        root.transform.localScale = new Vector3(facing >= 0 ? 1f : -1f, 1f, 1f);
        CommanderRig rig = root.AddComponent<CommanderRig>();
        rig.armRest = 8f;
        CommanderIdle idle = root.AddComponent<CommanderIdle>();
        Look l = new Look {
            bone = LichKit.BoneMat, boneDark = LichKit.BoneDarkMat, robe = Mats.Lit(RobeBlack, 0.2f), trim = Mats.Lit(RobeTrim, 0.3f),
            gold = Mats.Lit(new Color(0.82f, 0.64f, 0.27f), 0.6f, 0.7f), team = Mats.Lit(teamColor, 0.5f),
            socket = LichKit.SocketMat, soul = LichKit.SoulMat, wood = Mats.Lit(Wood, 0.2f),
        };

        Quaternion yaw = Quaternion.Euler(0f, BodyYaw, 0f);
        rig.body = Pivot("Body", root.transform, Vector3.zero, new Vector3(0f, BodyYaw, 0f));
        idle.torso = Pivot("Torso", rig.body, Vector3.zero);
        rig.head = Pivot("Head", rig.body, new Vector3(0f, NeckY, 0f), new Vector3(0f, HeadYaw, 0f));
        Vector3 shoulder = yaw * ShoulderBody;
        rig.armPivot = Pivot("ArmPivot", root.transform, new Vector3(shoulder.x, shoulder.y, -0.27f));
        rig.hand = Pivot("Hand", rig.armPivot, new Vector3(0f, -ArmLength, 0f));

        Robe(idle.torso, rig.body, l);
        Skull(rig.head, idle, l);
        Arm(rig.armPivot, l);
        Transform offArm = Pivot("OffArm", rig.body, new Vector3(-ShoulderBody.x, ShoulderBody.y, -0.02f), new Vector3(0f, 0f, -10f));
        Arm(offArm, l);
        Staff(rig.body, idle, l);

        rig.renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        return rig;
    }

    // ------------------------------------------------------------------ body
    // A floor-length robe (an octagon turned flat side to the camera) flaring to a tattered hem, bone-white trim, a
    // team-coloured sash, the ribs showing through the open front, and a skull on each shoulder.
    static void Robe(Transform t, Transform body, Look l) {
        Vector3 octagon = new Vector3(0f, 22.5f, 0f);
        Part(t, WorldMeshes.Frustum(8, 0.34f, 0.22f, 0.94f), new Vector3(0f, 0.02f, 0f), new Vector3(1f, 1f, 0.85f), l.robe, octagon);
        Part(t, WorldMeshes.Frustum(8, 0.345f, 0.33f, 0.05f), new Vector3(0f, 0.02f, 0f), new Vector3(1f, 1f, 0.86f), l.trim, octagon);
        Part(t, WorldMeshes.Frustum(8, 0.262f, 0.255f, 0.07f), new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 0.87f), l.team, octagon);   // sash
        for (int k = 0; k < 7; k++) {                                                                                              // tatters
            float a = Mathf.Lerp(-70f, 70f, k / 6f) * Mathf.Deg2Rad;
            Vector3 at = new Vector3(Mathf.Sin(a) * 0.3f, 0.03f, -Mathf.Cos(a) * 0.26f);
            Part(t, MeshKit.Cone(3, 0.06f, 0.1f), at, new Vector3(1f, 1f, 0.4f), l.robe, new Vector3(0f, -a * Mathf.Rad2Deg, 180f));
        }
        // The open front: a dark gap with three ribs and the breastbone.
        Part(t, PrimitiveType.Cube, new Vector3(0f, 0.74f, -0.19f), new Vector3(0.16f, 0.26f, 0.02f), l.socket);
        Part(t, PrimitiveType.Cube, new Vector3(0f, 0.74f, -0.2f), new Vector3(0.025f, 0.24f, 0.015f), l.bone);
        for (int k = 0; k < 3; k++)
            for (int s = -1; s <= 1; s += 2)
                Part(t, PrimitiveType.Cube, new Vector3(s * 0.045f, 0.82f - k * 0.07f, -0.205f), new Vector3(0.08f, 0.018f, 0.012f), l.bone, new Vector3(0f, 0f, s * -14f));
        for (int s = -1; s <= 1; s += 2) {                                                                                         // robe edges
            Part(t, PrimitiveType.Cube, new Vector3(s * 0.09f, 0.5f, -0.205f), new Vector3(0.03f, 0.9f, 0.015f), l.trim, new Vector3(0f, 0f, s * 5f));
            // Skull pauldrons on the shoulders.
            Transform pad = Pivot("Pauldron", body, new Vector3(s * 0.27f, 0.9f, -0.02f));
            Part(pad, WorldMeshes.Ball(0.1f), Vector3.zero, new Vector3(1f, 0.85f, 1f), l.bone);
            Part(pad, WorldMeshes.Ball(0.025f), new Vector3(s * 0.02f - 0.03f, 0.01f, -0.09f), Vector3.one, l.socket);
            Part(pad, WorldMeshes.Ball(0.025f), new Vector3(s * 0.02f + 0.03f, 0.01f, -0.09f), Vector3.one, l.socket);
        }
        // Bony toes peeping out under the hem.
        for (int s = -1; s <= 1; s += 2)
            for (int k = -1; k <= 1; k++)
                Part(body, WorldMeshes.Ball(0.022f), new Vector3(s * 0.1f + k * 0.03f, 0.02f, -0.3f), new Vector3(1f, 0.7f, 1.4f), l.bone);
    }

    // An arm hanging from its pivot: a wide black sleeve with a bone-white cuff and a skeletal hand.
    static void Arm(Transform pivot, Look l) {
        Part(pivot, WorldMeshes.Ball(0.09f), Vector3.zero, Vector3.one, l.robe);
        Part(pivot, WorldMeshes.Frustum(6, 0.075f, 0.1f, 0.36f), new Vector3(0f, -0.38f, 0f), Vector3.one, l.robe);
        Part(pivot, WorldMeshes.Frustum(6, 0.105f, 0.1f, 0.04f), new Vector3(0f, -0.4f, 0f), Vector3.one, l.trim);
        Part(pivot, WorldMeshes.Ball(0.045f), new Vector3(0f, -0.44f, 0f), Vector3.one, l.bone);
        for (int k = -1; k <= 1; k++)
            Part(pivot, WorldMeshes.Frustum(4, 0.012f, 0.007f, 0.08f), new Vector3(k * 0.022f, -0.46f, -0.01f), Vector3.one, l.bone, new Vector3(180f, 0f, 0f));
    }

    // ------------------------------------------------------------------ head
    // A bare skull: dark sockets with green fire (the blink pivot squints them), a nose hole, cheekbones, a jaw with a
    // row of teeth, and a spiked gold crown with green gems and the team gem at the front.
    static void Skull(Transform head, CommanderIdle idle, Look l) {
        Part(head, WorldMeshes.Ball(0.235f), new Vector3(0f, 0.27f, 0f), new Vector3(1f, 1.05f, 1f), l.bone);
        Part(head, WorldMeshes.Ball(0.16f), new Vector3(0f, 0.11f, -0.04f), new Vector3(1f, 0.75f, 1f), l.bone);   // jaw
        Transform eyes = Pivot("Eyes", head, new Vector3(0f, 0.27f, 0f));
        idle.eyes = eyes;
        for (int s = -1; s <= 1; s += 2) {
            Part(head, WorldMeshes.Ball(0.065f), new Vector3(s * 0.085f, 0.27f, -0.19f), new Vector3(1f, 0.9f, 0.6f), l.socket);
            Part(eyes, WorldMeshes.Ball(0.03f), new Vector3(s * 0.085f, 0f, -0.215f), Vector3.one, l.soul);
            Part(head, WorldMeshes.Ball(0.05f), new Vector3(s * 0.13f, 0.19f, -0.17f), new Vector3(1.3f, 0.7f, 0.8f), l.bone);   // cheekbones
        }
        Part(head, MeshKit.Cone(3, 0.03f, 0.06f), new Vector3(0f, 0.17f, -0.225f), new Vector3(1f, 1f, 0.5f), l.socket, new Vector3(180f, 0f, 0f));   // nose
        Part(head, PrimitiveType.Cube, new Vector3(0f, 0.09f, -0.17f), new Vector3(0.17f, 0.035f, 0.02f), l.socket);                                // mouth
        for (int k = -3; k <= 3; k++)
            Part(head, PrimitiveType.Cube, new Vector3(k * 0.022f, 0.093f, -0.178f), new Vector3(0.014f, 0.03f, 0.012f), l.bone);                   // teeth

        Transform crown = Pivot("Crown", head, new Vector3(0f, 0.42f, 0f), new Vector3(-6f, 0f, 0f));
        Part(crown, WorldMeshes.Frustum(10, 0.2f, 0.215f, 0.08f), Vector3.zero, Vector3.one, l.gold);
        for (int k = 0; k < 7; k++) {
            float a = Mathf.Lerp(-80f, 80f, k / 6f) * Mathf.Deg2Rad;
            Vector3 at = new Vector3(Mathf.Sin(a) * 0.205f, 0.07f, -Mathf.Cos(a) * 0.205f);
            float h = k == 3 ? 0.17f : k % 2 == 0 ? 0.13f : 0.09f;
            Part(crown, MeshKit.Cone(4, 0.035f, h), at, Vector3.one, l.gold, new Vector3(-Mathf.Cos(a) * 8f, 0f, -Mathf.Sin(a) * 8f));
            if (k % 2 == 1) Part(crown, WorldMeshes.Ball(0.022f), at + new Vector3(0f, 0.035f, -0.012f), Vector3.one, l.soul);
        }
        Part(crown, WorldMeshes.Ball(0.032f), new Vector3(0f, 0.04f, -0.215f), new Vector3(1f, 1.2f, 0.6f), l.team);
    }

    // ------------------------------------------------------------------ staff
    // In the off hand: a crooked shaft of black wood, bone-white bands, a skull at the top holding a pulsing soul orb.
    static void Staff(Transform body, CommanderIdle idle, Look l) {
        Transform staff = Pivot("Staff", body, new Vector3(-0.31f, 0.02f, -0.14f));
        Part(staff, WorldMeshes.Frustum(6, 0.03f, 0.024f, 1.05f), Vector3.zero, Vector3.one, l.wood);
        Transform upper = Pivot("Upper", staff, new Vector3(0f, 1.05f, 0f), new Vector3(0f, 0f, 7f));
        Part(upper, WorldMeshes.Frustum(6, 0.024f, 0.02f, 0.7f), Vector3.zero, Vector3.one, l.wood);
        for (int k = 0; k < 3; k++) Part(staff, WorldMeshes.Frustum(6, 0.034f, 0.034f, 0.03f), new Vector3(0f, 0.55f + k * 0.2f, 0f), Vector3.one, l.trim);
        Transform top = Pivot("Top", upper, new Vector3(0f, 0.72f, 0f));
        Part(top, WorldMeshes.Ball(0.075f), Vector3.zero, new Vector3(1f, 1.05f, 0.95f), l.bone);
        for (int s = -1; s <= 1; s += 2) {
            Part(top, WorldMeshes.Ball(0.02f), new Vector3(s * 0.027f, 0.01f, -0.063f), Vector3.one, l.soul);
            Part(top, MeshKit.Cone(4, 0.016f, 0.11f), new Vector3(s * 0.05f, 0.04f, 0f), Vector3.one, l.boneDark, new Vector3(0f, 0f, -s * 35f));   // horns
        }
        Transform orb = Pivot("Orb", top, new Vector3(0f, 0.13f, 0f));
        GameObject glow = Part(orb, WorldMeshes.Ball(0.055f), Vector3.zero, Vector3.one, l.soul);
        ModelKit.Shadows(glow, false);
        idle.orb = orb;
    }

    // ------------------------------------------------------------------ helpers
    static Transform Pivot(string name, Transform parent, Vector3 localPos, Vector3 euler = default) {
        return LichKit.Pivot(name, parent, localPos, euler);
    }

    static GameObject Part(Transform parent, Mesh mesh, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default) {
        return ModelKit.MeshPart(parent, mesh, pos, scale, m, euler);
    }

    static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default) {
        return ModelKit.Part(parent, type, pos, scale, m, euler);
    }
}

using UnityEngine;
using UnityEngine.Rendering;

// Builds the visual model of a commander: a chunky low-poly figure (~1.65 m, feet at y = 0) that faces the
// camera (-z) and is turned a little towards the enemy (+x; the root is mirrored for facing -1).
// The class colours dominate: a soldier in steel with a crimson tabard, a wizard in an indigo robe with gold
// stars under a tall blue hat, a god in white and gold with a golden cape and aura. The team colour is only a
// small accent (soldier: plume and belt, wizard: sash, god: cape hem and chest jewel), enough to tell two
// commanders of the same class apart. Later classes register their own model in ClassLooks; a class without
// one falls back to the soldier.
// Rig contract (see CommanderRig): armPivot sits at the throwing shoulder, rotation z = 0 lets the arm hang
// straight down and positive angles swing it forward (+x) and up; the hand is at the end of the arm.
// The model has no colliders; the Commander's capsule (radius 0.32) does the physics.
public static class CommanderModel {
    const float BodyYaw = -15f, HeadYaw = -12f;          // negative yaw turns the face towards +x
    const float NeckY = 0.94f, ArmLength = 0.46f;
    const float HelmetY = 0.33f;                         // head-local height of a helmet's rim (just above the eyes)
    static readonly Vector3 ShoulderBody = new Vector3(0.27f, 0.86f, 0f);

    static readonly Color Skin = new Color(0.98f, 0.79f, 0.65f);
    static readonly Color Leather = new Color(0.42f, 0.27f, 0.16f);
    static readonly Color Steel = new Color(0.66f, 0.7f, 0.76f);
    static readonly Color Gold = new Color(1f, 0.77f, 0.28f);
    static readonly Color Ink = new Color(0.08f, 0.07f, 0.09f);
    static readonly Color White = new Color(0.95f, 0.94f, 0.9f);
    static readonly Color Crimson = new Color(0.62f, 0.07f, 0.1f);
    static readonly Color Robe = new Color(0.25f, 0.13f, 0.47f), HatBlue = new Color(0.16f, 0.27f, 0.72f);
    static readonly Color CapeGold = new Color(0.86f, 0.6f, 0.18f), WingWhite = new Color(1f, 0.95f, 0.8f);

    // Materials of one model; the class builders fill in what differs.
    class Look {
        public Material skin, sleeve, hand, torso, legs, boots, accent, team, gold, eye;
    }

    public static CommanderRig Build(HeroClass heroClass, Color teamColor, int facing, Transform parent) {
        CommanderRig custom = ClassLooks.Commander(heroClass, teamColor, facing, parent);   // the later classes build their own
        if (custom != null) return custom;
        GameObject root = ModelKit.Root("Model", parent);
        root.transform.localScale = new Vector3(facing >= 0 ? 1f : -1f, 1f, 1f);
        CommanderRig rig = root.AddComponent<CommanderRig>();
        rig.armRest = 8f;   // hanging, slightly away from the body
        CommanderIdle idle = root.AddComponent<CommanderIdle>();

        Quaternion yaw = Quaternion.Euler(0f, BodyYaw, 0f);
        rig.body = Pivot("Body", root.transform, Vector3.zero, new Vector3(0f, BodyYaw, 0f));
        idle.torso = Pivot("Torso", rig.body, Vector3.zero);
        rig.head = Pivot("Head", rig.body, new Vector3(0f, NeckY, 0f), new Vector3(0f, HeadYaw, 0f));
        // The throwing arm is not under the turned body: it swings in the x-y plane of the root, just in
        // front of the body so a wind-up over the head does not cut through the face.
        Vector3 shoulder = yaw * ShoulderBody;
        rig.armPivot = Pivot("ArmPivot", root.transform, new Vector3(shoulder.x, shoulder.y, -0.27f));
        rig.hand = Pivot("Hand", rig.armPivot, new Vector3(0f, -ArmLength, 0f));

        Look look = MakeLook(heroClass, teamColor);
        Face(rig.head, look, idle, heroClass == HeroClass.Wizard);
        Arm(rig.armPivot, look, heroClass);
        Transform offArm = Pivot("OffArm", rig.body, new Vector3(-ShoulderBody.x, ShoulderBody.y, -0.02f), new Vector3(0f, 0f, -10f));
        Arm(offArm, look, heroClass);
        switch (heroClass) {
            case HeroClass.Wizard: Wizard(rig, idle, look, offArm); break;
            case HeroClass.God: God(rig, idle, look); break;
            default: Soldier(rig, idle, look); break;
        }

        rig.renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        return rig;
    }

    static Look MakeLook(HeroClass c, Color team) {
        Look l = new Look {
            skin = Mats.Lit(Skin, 0.3f), hand = Mats.Lit(Skin, 0.3f), team = Mats.Lit(team, 0.3f),
            gold = Mats.Lit(Gold, 0.65f, 0.85f), eye = Mats.Lit(Ink, 0.7f), boots = Mats.Lit(Leather, 0.3f),
        };
        switch (c) {
            case HeroClass.Wizard:
                l.sleeve = l.torso = Mats.Lit(Robe, 0.25f);                // deep indigo robe
                l.legs = Mats.Lit(new Color(0.16f, 0.1f, 0.24f), 0.2f);
                l.accent = Mats.Lit(HatBlue, 0.3f);                        // tall blue hat
                break;
            case HeroClass.God:
                l.sleeve = l.torso = l.legs = Mats.Lit(White, 0.55f, 0.15f);
                l.boots = l.gold;
                l.accent = Mats.Lit(WingWhite, 0.45f);                     // helmet wings
                l.eye = Mats.Emissive(new Color(0.75f, 0.9f, 1f), new Color(1.5f, 2.6f, 3.8f));   // glowing eyes bloom
                break;
            default:
                l.sleeve = l.torso = Mats.Lit(Steel, 0.55f, 0.7f);         // steel plate
                l.hand = Mats.Lit(Leather, 0.3f);                          // gloves
                l.legs = Mats.Lit(new Color(0.28f, 0.29f, 0.34f), 0.2f);
                l.accent = Mats.Lit(Steel, 0.6f, 0.75f);                   // helmet, shield rim, sword
                break;
        }
        return l;
    }

    // ------------------------------------------------------------------ shared parts
    static void Face(Transform head, Look l, CommanderIdle idle, bool bigNose) {
        Part(head, WorldMeshes.Ball(0.25f), new Vector3(0f, 0.24f, 0f), Vector3.one, l.skin);
        Transform eyes = Pivot("Eyes", head, new Vector3(0f, 0.27f, 0f));
        idle.eyes = eyes;
        for (int s = -1; s <= 1; s += 2) {
            Part(eyes, WorldMeshes.Ball(0.036f), new Vector3(s * 0.085f, 0f, -0.222f), Vector3.one, l.eye);
            Part(head, WorldMeshes.Ball(0.05f), new Vector3(s * 0.14f, 0.18f, -0.2f), new Vector3(1f, 0.6f, 0.5f), Mats.Lit(new Color(1f, 0.6f, 0.6f), 0.3f));
            Part(head, WorldMeshes.Ball(0.06f), new Vector3(s * 0.25f, 0.24f, 0.01f), Vector3.one, l.skin);
        }
        Part(head, WorldMeshes.Ball(bigNose ? 0.06f : 0.045f), new Vector3(0f, 0.2f, -0.25f), Vector3.one, l.skin);
        Part(head, WorldMeshes.Ball(0.03f), new Vector3(0f, 0.12f, -0.225f), new Vector3(1.5f, 0.5f, 0.6f), Mats.Lit(new Color(0.45f, 0.18f, 0.16f), 0.3f));
    }

    // An arm hanging from its pivot: shoulder ball, sleeve, hand. The pivot is the joint.
    static void Arm(Transform pivot, Look l, HeroClass c) {
        bool wizard = c == HeroClass.Wizard;
        Part(pivot, WorldMeshes.Ball(c == HeroClass.Soldier ? 0.1f : 0.09f), Vector3.zero, Vector3.one, c == HeroClass.God ? l.gold : l.sleeve);
        Part(pivot, WorldMeshes.Frustum(6, wizard ? 0.105f : 0.072f, 0.075f, 0.38f), new Vector3(0f, -0.4f, 0f), Vector3.one, l.sleeve);
        if (c == HeroClass.God) Part(pivot, WorldMeshes.Frustum(6, 0.08f, 0.078f, 0.1f), new Vector3(0f, -0.36f, 0f), Vector3.one, l.gold);
        Part(pivot, WorldMeshes.Ball(0.08f), new Vector3(0f, -0.45f, 0f), Vector3.one, l.hand);
    }

    // Short legs with boots poking forward (hidden under the wizard's robe).
    static void Legs(Transform body, Look l) {
        for (int s = -1; s <= 1; s += 2) {
            Part(body, WorldMeshes.Frustum(6, 0.085f, 0.08f, 0.26f), new Vector3(s * 0.11f, 0.1f, 0f), Vector3.one, l.legs);
            Part(body, WorldMeshes.Frustum(6, 0.1f, 0.09f, 0.14f), new Vector3(s * 0.11f, 0f, -0.03f), new Vector3(1f, 1f, 1.35f), l.boots);
        }
    }

    // Barrel torso (an octagon turned so a flat side faces the camera) with a belt and buckle.
    static void Torso(Transform torso, Look l, Material body, Material belt) {
        Part(torso, WorldMeshes.Frustum(8, 0.25f, 0.21f, 0.62f), new Vector3(0f, 0.34f, 0f), new Vector3(1f, 1f, 0.82f), body, new Vector3(0f, 22.5f, 0f));
        Part(torso, WorldMeshes.Frustum(8, 0.262f, 0.262f, 0.08f), new Vector3(0f, 0.48f, 0f), new Vector3(1f, 1f, 0.84f), belt, new Vector3(0f, 22.5f, 0f));
        Part(torso, PrimitiveType.Cube, new Vector3(0f, 0.52f, -0.225f), new Vector3(0.1f, 0.08f, 0.03f), l.gold);
    }

    // ------------------------------------------------------------------ classes
    // Steel plate with a crimson tabard, steel helmet with a team-colour plume, team-colour belt, crimson round
    // shield, sword on the back.
    static void Soldier(CommanderRig rig, CommanderIdle idle, Look l) {
        Legs(rig.body, l);
        Torso(idle.torso, l, l.torso, l.team);
        Part(idle.torso, WorldMeshes.Frustum(8, 0.29f, 0.255f, 0.15f), new Vector3(0f, 0.28f, 0f), new Vector3(1f, 1f, 0.84f), l.torso, new Vector3(0f, 22.5f, 0f));
        // tabard: front and back panels with a white cross
        Material crimson = Mats.Lit(Crimson, 0.3f);
        Part(idle.torso, PrimitiveType.Cube, new Vector3(0f, 0.7f, -0.2f), new Vector3(0.32f, 0.5f, 0.04f), crimson);
        Part(idle.torso, PrimitiveType.Cube, new Vector3(0f, 0.7f, 0.2f), new Vector3(0.32f, 0.5f, 0.04f), crimson);
        Material white = Mats.Lit(White, 0.3f);
        Part(idle.torso, PrimitiveType.Cube, new Vector3(0f, 0.72f, -0.223f), new Vector3(0.05f, 0.3f, 0.01f), white);
        Part(idle.torso, PrimitiveType.Cube, new Vector3(0f, 0.76f, -0.223f), new Vector3(0.2f, 0.05f, 0.01f), white);

        // helmet (rim above the eyes): dome, rim, nose guard, plume arching backwards (away from the enemy)
        Part(rig.head, WorldMeshes.Dome(10, 3, 0.275f), new Vector3(0f, HelmetY, 0f), Vector3.one, l.accent);
        Part(rig.head, WorldMeshes.Frustum(10, 0.29f, 0.29f, 0.04f), new Vector3(0f, HelmetY - 0.02f, 0f), Vector3.one, l.accent);
        Part(rig.head, PrimitiveType.Cube, new Vector3(0f, HelmetY - 0.02f, -0.275f), new Vector3(0.045f, 0.12f, 0.03f), l.accent);
        Transform plume = Pivot("Plume", rig.head, new Vector3(0f, HelmetY + 0.26f, 0.02f));
        idle.plume = plume;
        Part(plume, WorldMeshes.Ball(0.085f), new Vector3(0f, 0.05f, 0f), new Vector3(0.8f, 1.5f, 0.8f), l.team);
        Part(plume, WorldMeshes.Ball(0.075f), new Vector3(-0.09f, 0.11f, 0.02f), Vector3.one, l.team);
        Part(plume, WorldMeshes.Ball(0.06f), new Vector3(-0.18f, 0.1f, 0.03f), Vector3.one, l.team);

        // round shield held in front, on the off side
        Transform shield = Pivot("Shield", rig.body, new Vector3(-0.12f, 0.58f, -0.27f), new Vector3(-90f, 0f, 0f));
        Part(shield, WorldMeshes.Frustum(12, 0.205f, 0.205f, 0.04f), Vector3.zero, Vector3.one, l.accent);
        Part(shield, WorldMeshes.Frustum(12, 0.175f, 0.175f, 0.025f), new Vector3(0f, 0.03f, 0f), Vector3.one, crimson);
        Part(shield, PrimitiveType.Cube, new Vector3(0f, 0.056f, 0f), new Vector3(0.06f, 0.01f, 0.33f), white, new Vector3(0f, 40f, 0f));
        Part(shield, WorldMeshes.Ball(0.05f), new Vector3(0f, 0.06f, 0f), Vector3.one, l.accent);
        rig.prop = shield;

        // sword on the back, hilt over the off shoulder
        Transform sword = Pivot("Sword", rig.body, new Vector3(-0.05f, 0.86f, 0.25f), new Vector3(0f, 0f, 35f));
        Part(sword, PrimitiveType.Cube, new Vector3(0f, -0.05f, 0f), new Vector3(0.065f, 0.62f, 0.015f), l.accent);
        Part(sword, PrimitiveType.Cube, new Vector3(0f, 0.27f, 0f), new Vector3(0.22f, 0.04f, 0.05f), l.gold);
        Part(sword, WorldMeshes.Frustum(6, 0.022f, 0.022f, 0.13f), new Vector3(0f, 0.29f, 0f), Vector3.one, l.boots);
        Part(sword, WorldMeshes.Ball(0.035f), new Vector3(0f, 0.44f, 0f), Vector3.one, l.gold);
    }

    // Indigo robe sprinkled with glowing gold stars and a team-colour sash, white beard, tall crooked blue hat
    // with glowing stars, staff with a glowing orb.
    static void Wizard(CommanderRig rig, CommanderIdle idle, Look l, Transform offArm) {
        Transform t = idle.torso;
        Vector3 octagon = new Vector3(0f, 22.5f, 0f);
        Part(t, WorldMeshes.Frustum(8, 0.32f, 0.21f, 0.94f), new Vector3(0f, 0.02f, 0f), new Vector3(1f, 1f, 0.85f), l.torso, octagon);
        Part(t, WorldMeshes.Frustum(8, 0.326f, 0.31f, 0.06f), new Vector3(0f, 0.02f, 0f), new Vector3(1f, 1f, 0.86f), l.gold, octagon);
        Part(t, WorldMeshes.Frustum(8, 0.252f, 0.247f, 0.07f), new Vector3(0f, 0.545f, 0f), new Vector3(1f, 1f, 0.87f), l.team, octagon);
        Part(t, WorldMeshes.Ball(0.04f), new Vector3(0.08f, 0.5f, -0.22f), Vector3.one, l.gold);
        Material star = Mats.Emissive(Gold, new Color(2.2f, 1.7f, 0.6f));
        RobeStar(t, star, -0.13f, 0.16f, 0.045f);
        RobeStar(t, star, 0.12f, 0.3f, 0.04f);
        RobeStar(t, star, -0.05f, 0.42f, 0.032f);
        RobeStar(t, star, 0.1f, 0.74f, 0.035f);
        RobeStar(t, star, -0.11f, 0.82f, 0.028f);
        for (int s = -1; s <= 1; s += 2)
            Part(rig.body, WorldMeshes.Ball(0.07f), new Vector3(s * 0.1f, 0.04f, -0.25f), new Vector3(1f, 0.7f, 1.3f), l.boots);

        // beard (a cone hanging forward from the chin), moustache and bushy eyebrows
        Material beard = Mats.Lit(new Color(0.96f, 0.96f, 0.98f), 0.2f);
        Part(rig.head, MeshKit.Cone(8, 0.19f, 0.42f), new Vector3(0f, 0.13f, -0.11f), new Vector3(1f, 1f, 0.65f), beard, new Vector3(192f, 0f, 0f));
        for (int s = -1; s <= 1; s += 2) {
            Part(rig.head, WorldMeshes.Ball(0.06f), new Vector3(s * 0.06f, 0.14f, -0.24f), new Vector3(1.5f, 0.6f, 0.7f), beard, new Vector3(0f, 0f, s * 15f));
            Part(rig.head, PrimitiveType.Cube, new Vector3(s * 0.09f, 0.335f, -0.215f), new Vector3(0.1f, 0.03f, 0.03f), beard, new Vector3(0f, 0f, s * -12f));
        }

        // hat: brim, band, three cone sections bending backwards, little glowing stars
        Transform hat = Pivot("Hat", rig.head, new Vector3(0f, 0.4f, 0f));
        Part(hat, WorldMeshes.Frustum(12, 0.33f, 0.31f, 0.035f), Vector3.zero, Vector3.one, l.accent);
        Part(hat, WorldMeshes.Frustum(10, 0.215f, 0.205f, 0.06f), new Vector3(0f, 0.03f, 0f), Vector3.one, l.gold);
        Part(hat, WorldMeshes.Frustum(10, 0.21f, 0.13f, 0.3f), new Vector3(0f, 0.03f, 0f), Vector3.one, l.accent);
        Transform bend = Pivot("Bend", hat, new Vector3(0f, 0.32f, 0f), new Vector3(0f, 0f, 16f));
        Part(bend, WorldMeshes.Frustum(10, 0.13f, 0.06f, 0.22f), Vector3.zero, Vector3.one, l.accent);
        Transform tip = Pivot("Tip", bend, new Vector3(0f, 0.21f, 0f), new Vector3(0f, 0f, 28f));
        Part(tip, MeshKit.Cone(10, 0.065f, 0.17f), Vector3.zero, Vector3.one, l.accent);
        HatStar(hat, star, 0.12f, -20f, 0.035f);
        HatStar(hat, star, 0.24f, 35f, 0.03f);
        HatStar(hat, star, 0.29f, -55f, 0.025f);

        // staff in the off hand: wooden shaft, three claws and a glowing orb
        Transform staff = Pivot("Staff", rig.body, new Vector3(-0.31f, 0.02f, -0.14f));
        Material wood = Mats.Lit(new Color(0.45f, 0.3f, 0.18f), 0.25f);
        Part(staff, WorldMeshes.Frustum(6, 0.03f, 0.024f, 1.78f), Vector3.zero, Vector3.one, wood);
        for (int k = 0; k < 3; k++)
            Part(staff, WorldMeshes.Frustum(4, 0.016f, 0.008f, 0.14f), new Vector3(0f, 1.76f, 0f), Vector3.one, wood, new Vector3(0f, k * 120f, 25f));
        Transform orb = Pivot("Orb", staff, new Vector3(0f, 1.88f, 0f));
        Color glow = new Color(0.55f, 0.92f, 1f);
        Part(orb, WorldMeshes.Ball(0.085f), Vector3.zero, Vector3.one, Mats.Emissive(glow, glow * 3.2f));
        Sparkles(orb, glow, 0.12f, 5f, 0.05f);
        idle.orb = orb;
        rig.prop = staff;
        offArm.localRotation = Quaternion.Euler(0f, 0f, -4f);   // the off hand closes on the shaft
    }

    static void HatStar(Transform hat, Material m, float height, float angle, float size) {
        // a point on the first cone section, facing out of the surface
        float r = Mathf.Lerp(0.21f, 0.13f, (height - 0.03f) / 0.3f) + 0.015f;   // just outside the faceted cone
        float a = angle * Mathf.Deg2Rad;
        Vector3 outward = new Vector3(Mathf.Sin(a), 0.25f, -Mathf.Cos(a)).normalized;
        Vector3 pos = new Vector3(Mathf.Sin(a) * r, height, -Mathf.Cos(a) * r);
        GameObject go = ModelKit.MeshPart(hat, WorldMeshes.Star(size, size * 0.42f), pos, Vector3.one, m, default, "Star");
        go.transform.localRotation = Quaternion.LookRotation(-outward);   // the star mesh faces -z
    }

    // A star on the front face of the robe (an octagonal frustum, flat side to the camera, squashed to 0.85 in z),
    // x across and y up from the torso pivot.
    static void RobeStar(Transform torso, Material m, float x, float y, float size) {
        float r = Mathf.Lerp(0.32f, 0.21f, (y - 0.02f) / 0.94f);
        float face = r * Mathf.Cos(22.5f * Mathf.Deg2Rad) * 0.85f + 0.008f;   // apothem of the octagon, squashed
        ModelKit.MeshPart(torso, WorldMeshes.Star(size, size * 0.42f), new Vector3(x, y, -face), Vector3.one, m, default, "Star");
    }

    // White and gold armour, a golden cape with a team-colour hem, a team-colour jewel on the chest, golden
    // winged helmet, glowing eyes and a golden aura.
    static void God(CommanderRig rig, CommanderIdle idle, Look l) {
        Legs(rig.body, l);
        Torso(idle.torso, l, l.torso, l.gold);
        Vector3 octagon = new Vector3(0f, 22.5f, 0f);
        Part(idle.torso, WorldMeshes.Frustum(8, 0.3f, 0.255f, 0.16f), new Vector3(0f, 0.27f, 0f), new Vector3(1f, 1f, 0.84f), l.torso, octagon);
        Part(idle.torso, WorldMeshes.Frustum(8, 0.305f, 0.3f, 0.04f), new Vector3(0f, 0.26f, 0f), new Vector3(1f, 1f, 0.85f), l.gold, octagon);
        Part(idle.torso, WorldMeshes.Frustum(8, 0.23f, 0.19f, 0.06f), new Vector3(0f, 0.92f, 0f), new Vector3(1f, 1f, 0.85f), l.gold, octagon);
        Part(idle.torso, WorldMeshes.Disc(10, 0.075f), new Vector3(0f, 0.74f, -0.205f), Vector3.one, l.gold);
        Part(idle.torso, WorldMeshes.Ball(0.036f), new Vector3(0f, 0.74f, -0.215f), new Vector3(1f, 1f, 0.6f), l.team);
        Part(rig.body, WorldMeshes.Ball(0.12f), new Vector3(-ShoulderBody.x, ShoulderBody.y + 0.03f, 0f), new Vector3(1.1f, 0.75f, 1.1f), l.gold);

        // cape hanging from the shoulders, swaying gently (CommanderIdle), its hem in the team colour
        Transform cape = Pivot("Cape", rig.body, new Vector3(0f, 0.92f, 0f));
        Part(cape, CapeMesh(), Vector3.zero, Vector3.one, Mats.Lit(CapeGold, 0.35f, 0.2f));
        Part(cape, CapeHemMesh(), Vector3.zero, Vector3.one, l.team);
        idle.cape = cape;

        // winged helmet (rim above the glowing eyes)
        Part(rig.head, WorldMeshes.Dome(10, 3, 0.272f), new Vector3(0f, HelmetY, 0f), Vector3.one, l.gold);
        Part(rig.head, WorldMeshes.Frustum(10, 0.286f, 0.286f, 0.045f), new Vector3(0f, HelmetY - 0.02f, 0f), Vector3.one, l.gold);
        Part(rig.head, PrimitiveType.Cube, new Vector3(0f, HelmetY + 0.13f, -0.21f), new Vector3(0.05f, 0.14f, 0.05f), l.gold, new Vector3(-30f, 0f, 0f));
        for (int s = -1; s <= 1; s += 2) {
            Transform wing = Pivot("Wing", rig.head, new Vector3(s * 0.25f, HelmetY + 0.08f, 0.02f), new Vector3(0f, 0f, -s * 12f));
            for (int k = 0; k < 3; k++)
                Part(wing, WorldMeshes.Ball(0.1f), new Vector3(s * k * 0.035f, 0.08f + k * 0.012f, 0.02f * k), new Vector3(0.38f, 1.2f - k * 0.22f, 0.22f),
                     l.accent, new Vector3(0f, 0f, -s * (8f + k * 18f)));
        }

        // the aura: rising golden sparkles and a soft golden glow around the body
        Color aura = new Color(1f, 0.82f, 0.4f);
        Sparkles(rig.body, aura, 0.45f, 12f, 0.1f).transform.localPosition = new Vector3(0f, 0.85f, 0f);
        Halo(rig.body, aura).transform.localPosition = new Vector3(0f, 0.85f, 0.1f);
    }

    // A cape: a double-sided curved strip around the back (+z), flaring from radius 0.25 at the shoulders (y = 0)
    // to 0.37 at the hem (y = -0.84). The hem trim is a band of the same shape lying just outside both sides.
    static Mesh capeMesh, capeHemMesh;

    static Mesh CapeMesh() { return capeMesh != null ? capeMesh : capeMesh = CapeBand("Cape", -0.84f, 0f, 0f); }

    static Mesh CapeHemMesh() { return capeHemMesh != null ? capeHemMesh : capeHemMesh = CapeBand("Cape Hem", -0.845f, -0.74f, 0.008f); }

    static Mesh CapeBand(string name, float bottom, float top, float offset) {
        MeshBuilder b = new MeshBuilder();
        const int cols = 7;
        int[,] outer = new int[cols, 2], inner = new int[cols, 2];
        for (int i = 0; i < cols; i++) {
            float a = Mathf.Lerp(30f, 150f, i / (cols - 1f)) * Mathf.Deg2Rad;   // 90 degrees = straight behind (+z)
            for (int row = 0; row < 2; row++) {
                float y = row == 0 ? bottom : top, r = Mathf.Lerp(0.25f, 0.37f, -y / 0.84f);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                outer[i, row] = b.Vertex(dir * (r + offset) + Vector3.up * y, new Vector2(i / (cols - 1f), row));
                inner[i, row] = b.Vertex(dir * (r - offset) + Vector3.up * y, new Vector2(i / (cols - 1f), row));
            }
        }
        for (int i = 0; i < cols - 1; i++) {
            // Seen from behind, column i + 1 is to the right of column i: clockwise there = the outer side.
            // The inner side (what the camera sees around the body) uses its own vertices, reversed.
            b.Quad(outer[i, 0], outer[i, 1], outer[i + 1, 1], outer[i + 1, 0]);
            b.Quad(inner[i + 1, 0], inner[i + 1, 1], inner[i, 1], inner[i, 0]);
        }
        return b.ToMesh(name);
    }

    // A soft golden glow around the body: a few large, faint additive puffs (the god's aura).
    static ParticleSystem Halo(Transform parent, Color color) {
        GameObject go = ModelKit.Root("Halo", parent);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 1.9f);
        main.startColor = new Color(color.r, color.g, color.b, 0.2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 6;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 2.2f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Mats.ParticleAdditive(TexKit.SoftCircle);
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return ps;
    }

    // Faint looping sparkles (the god's aura, the wizard's orb). World space, so they trail when moved.
    static ParticleSystem Sparkles(Transform parent, Color color, float radius, float rate, float size) {
        GameObject go = ModelKit.Root("Sparkles", parent);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = color;
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 30;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = rate;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Mats.ParticleAdditive(TexKit.Star);
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return ps;
    }

    // ------------------------------------------------------------------ helpers
    static Transform Pivot(string name, Transform parent, Vector3 localPos, Vector3 euler = default) {
        Transform t = ModelKit.Root(name, parent).transform;
        t.localPosition = localPos;
        t.localRotation = Quaternion.Euler(euler);
        return t;
    }

    static GameObject Part(Transform parent, Mesh mesh, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default) {
        return ModelKit.MeshPart(parent, mesh, pos, scale, m, euler);
    }

    static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default) {
        return ModelKit.Part(parent, type, pos, scale, m, euler);
    }
}

// Small idle life for a commander: breathing, blinking, a swaying cape or plume, a pulsing orb.
// Stops (eyes shut) when the commander dies; also runs on models without a Commander (menu, icons).
public class CommanderIdle : MonoBehaviour {
    public Transform torso, eyes, cape, plume, orb;
    Commander owner;
    float seed, nextBlink, blinkUntil;

    void Start() {
        owner = GetComponentInParent<Commander>();
        seed = Random.value * 10f;
        nextBlink = Time.time + Random.Range(1f, 3f);
    }

    void Update() {
        if (owner != null && !owner.IsAlive) {
            if (eyes != null) eyes.localScale = new Vector3(1f, 0.15f, 1f);
            enabled = false;
            return;
        }
        float t = Time.time + seed;
        if (torso != null) torso.localScale = new Vector3(1f, 1f + 0.018f * Mathf.Sin(t * 2.4f), 1f);
        if (cape != null) cape.localRotation = Quaternion.Euler(-4f - 3f * Mathf.Sin(t * 1.7f), 0f, 0f);   // hem swings backwards
        if (plume != null) plume.localRotation = Quaternion.Euler(0f, 0f, 5f * Mathf.Sin(t * 3.1f));
        if (orb != null) orb.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * 5f));
        if (eyes != null) {
            if (Time.time >= nextBlink) {
                blinkUntil = Time.time + 0.12f;
                nextBlink = Time.time + Random.Range(2f, 5f);
            }
            eyes.localScale = new Vector3(1f, Time.time < blinkUntil ? 0.12f : 1f, 1f);
        }
    }
}

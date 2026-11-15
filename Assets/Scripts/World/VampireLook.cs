using UnityEngine;

// The Vampire's look, registered with ClassLooks: the commander (this file), the block skins (VampireLookSkins.cs)
// and the fortress decorations (VampireLookDecor.cs).
// The commander is a pale count: slicked-back black hair with a widow's peak, pointed ears, faintly glowing red
// eyes and two small fangs; a black tailcoat over a crimson waistcoat, a white jabot and a gold medallion. A high
// stand-up collar and a long cape, black outside and lined with blood red, frame him, so the class colour shows
// all around the figure. The team colour is only the medallion's stone and a band at the cape's hem.
// Same rig contract and chibi proportions as CommanderModel (~1.65 m, feet at y = 0, faces -z turned a little
// towards the enemy, mirrored for facing -1, the throwing arm hangs along -y at rotation 0, no colliders).
public static class VampireLook {
    const float BodyYaw = -15f, HeadYaw = -12f;          // negative yaw turns the face towards +x
    const float NeckY = 0.94f, ArmLength = 0.46f;
    static readonly Vector3 ShoulderBody = new Vector3(0.27f, 0.86f, 0f);

    static readonly Color Pale = new Color(0.87f, 0.87f, 0.91f);
    static readonly Color Black = new Color(0.07f, 0.06f, 0.08f);
    static readonly Color Hair = new Color(0.035f, 0.03f, 0.045f);
    static readonly Color Lining = WeaponDefs.Hex("B3203D");
    static readonly Color Shirt = new Color(0.95f, 0.94f, 0.92f);
    static readonly Color Gold = new Color(1f, 0.77f, 0.3f);

    static Mesh capeOuter, capeInner, capeHem, collarOuter, collarInner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() {
        ClassLooks.Register(HeroClass.Vampire, BuildCommander, VampireSkins.Block, VampireDecor.Apply);
    }

    // Materials of one model.
    class Look {
        public Material skin, coat, lining, hair, shirt, gold, team, eye, boots;
    }

    public static CommanderRig BuildCommander(Color teamColor, int facing, Transform parent) {
        GameObject root = ModelKit.Root("Model", parent);
        root.transform.localScale = new Vector3(facing >= 0 ? 1f : -1f, 1f, 1f);
        CommanderRig rig = root.AddComponent<CommanderRig>();
        rig.armRest = 8f;
        CommanderIdle idle = root.AddComponent<CommanderIdle>();
        Look l = new Look {
            skin = Mats.Lit(Pale, 0.35f), coat = Mats.Lit(Black, 0.35f), lining = Mats.Lit(Lining, 0.4f),
            hair = Mats.Lit(Hair, 0.75f), shirt = Mats.Lit(Shirt, 0.3f), gold = Mats.Lit(Gold, 0.65f, 0.85f),
            team = Mats.Lit(teamColor, 0.5f), boots = Mats.Lit(Black, 0.7f, 0.2f),
            eye = Mats.Emissive(new Color(0.85f, 0.08f, 0.1f), new Color(2.2f, 0.15f, 0.2f)),   // a faint red glow
        };

        Quaternion yaw = Quaternion.Euler(0f, BodyYaw, 0f);
        rig.body = Pivot("Body", root.transform, Vector3.zero, new Vector3(0f, BodyYaw, 0f));
        idle.torso = Pivot("Torso", rig.body, Vector3.zero);
        rig.head = Pivot("Head", rig.body, new Vector3(0f, NeckY, 0f), new Vector3(0f, HeadYaw, 0f));
        // The throwing arm swings in the x-y plane of the root, just in front of the body (as in CommanderModel).
        Vector3 shoulder = yaw * ShoulderBody;
        rig.armPivot = Pivot("ArmPivot", root.transform, new Vector3(shoulder.x, shoulder.y, -0.27f));
        rig.hand = Pivot("Hand", rig.armPivot, new Vector3(0f, -ArmLength, 0f));

        Legs(rig.body, l);
        Torso(idle.torso, l);
        Head(rig.head, idle, l);
        Arm(rig.armPivot, l);
        Transform offArm = Pivot("OffArm", rig.body, new Vector3(-ShoulderBody.x, ShoulderBody.y, -0.02f), new Vector3(0f, 0f, -10f));
        Arm(offArm, l);
        Cape(rig.body, idle, l);

        rig.renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        return rig;
    }

    // ------------------------------------------------------------------ body
    // Black trousers and glossy black boots poking forward.
    static void Legs(Transform body, Look l) {
        for (int s = -1; s <= 1; s += 2) {
            Part(body, WorldMeshes.Frustum(6, 0.085f, 0.08f, 0.26f), new Vector3(s * 0.11f, 0.1f, 0f), Vector3.one, l.coat);
            Part(body, WorldMeshes.Frustum(6, 0.1f, 0.09f, 0.14f), new Vector3(s * 0.11f, 0f, -0.03f), new Vector3(1f, 1f, 1.35f), l.boots);
        }
    }

    // A black tailcoat (an octagon turned flat side to the camera) flaring below the waist, open over a crimson
    // waistcoat and a white shirt with a jabot, a gold medallion with the team stone on a crimson ribbon.
    static void Torso(Transform t, Look l) {
        Vector3 octagon = new Vector3(0f, 22.5f, 0f);
        Part(t, WorldMeshes.Frustum(8, 0.25f, 0.21f, 0.62f), new Vector3(0f, 0.34f, 0f), new Vector3(1f, 1f, 0.82f), l.coat, octagon);
        Part(t, WorldMeshes.Frustum(8, 0.3f, 0.255f, 0.18f), new Vector3(0f, 0.24f, 0f), new Vector3(1f, 1f, 0.84f), l.coat, octagon);
        Part(t, PrimitiveType.Cube, new Vector3(0f, 0.62f, -0.178f), new Vector3(0.2f, 0.36f, 0.03f), l.lining);    // waistcoat
        Part(t, PrimitiveType.Cube, new Vector3(0f, 0.74f, -0.19f), new Vector3(0.075f, 0.24f, 0.02f), l.shirt);     // shirt front
        for (int s = -1; s <= 1; s += 2) {
            Part(t, PrimitiveType.Cube, new Vector3(s * 0.088f, 0.7f, -0.192f), new Vector3(0.045f, 0.32f, 0.02f), l.coat, new Vector3(0f, 0f, s * 14f));   // lapels
            Part(t, PrimitiveType.Cube, new Vector3(s * 0.03f, 0.74f, -0.205f), new Vector3(0.018f, 0.2f, 0.01f), l.lining, new Vector3(0f, 0f, s * 12f)); // ribbon
            Part(t, WorldMeshes.Ball(0.016f), new Vector3(s * 0.06f, 0.5f, -0.196f), Vector3.one, l.gold);            // buttons
        }
        Part(t, WorldMeshes.Ball(0.055f), new Vector3(0f, 0.86f, -0.17f), new Vector3(1.3f, 0.8f, 0.7f), l.shirt);   // jabot
        Part(t, WorldMeshes.Ball(0.045f), new Vector3(0f, 0.8f, -0.19f), new Vector3(1.2f, 0.8f, 0.6f), l.shirt);
        Part(t, WorldMeshes.Disc(12, 0.06f), new Vector3(0f, 0.62f, -0.21f), Vector3.one, l.gold);                    // medallion
        Part(t, WorldMeshes.Ball(0.034f), new Vector3(0f, 0.62f, -0.218f), new Vector3(1f, 1f, 0.6f), l.team);
    }

    // An arm hanging from its pivot: shoulder, black sleeve with a crimson cuff, pale hand. The pivot is the joint.
    static void Arm(Transform pivot, Look l) {
        Part(pivot, WorldMeshes.Ball(0.095f), Vector3.zero, Vector3.one, l.coat);
        Part(pivot, WorldMeshes.Frustum(6, 0.072f, 0.075f, 0.38f), new Vector3(0f, -0.4f, 0f), Vector3.one, l.coat);
        Part(pivot, WorldMeshes.Frustum(6, 0.082f, 0.08f, 0.06f), new Vector3(0f, -0.41f, 0f), Vector3.one, l.lining);
        Part(pivot, WorldMeshes.Ball(0.075f), new Vector3(0f, -0.46f, 0f), Vector3.one, l.skin);
    }

    // ------------------------------------------------------------------ head
    // Pale face with glowing red eyes under sharp brows, a thin mouth with two fangs, pointed ears; black hair
    // slicked back from a high hairline with a widow's peak.
    static void Head(Transform head, CommanderIdle idle, Look l) {
        Part(head, WorldMeshes.Ball(0.25f), new Vector3(0f, 0.24f, 0f), Vector3.one, l.skin);
        Transform eyes = Pivot("Eyes", head, new Vector3(0f, 0.27f, 0f));
        idle.eyes = eyes;
        Material dark = Mats.Lit(new Color(0.3f, 0.03f, 0.07f), 0.4f);
        for (int s = -1; s <= 1; s += 2) {
            Part(eyes, WorldMeshes.Ball(0.034f), new Vector3(s * 0.085f, 0f, -0.222f), Vector3.one, l.eye);
            Part(head, PrimitiveType.Cube, new Vector3(s * 0.09f, 0.335f, -0.212f), new Vector3(0.1f, 0.022f, 0.03f), l.hair, new Vector3(0f, 0f, s * 18f));   // brows, frowning
            Part(head, MeshKit.Cone(5, 0.05f, 0.14f), new Vector3(s * 0.235f, 0.27f, 0.02f), Vector3.one, l.skin, new Vector3(0f, 0f, -s * 62f));            // pointed ears
            Part(head, MeshKit.Cone(5, 0.012f, 0.045f), new Vector3(s * 0.026f, 0.118f, -0.232f), Vector3.one, l.shirt, new Vector3(180f, 0f, 0f));          // fangs
        }
        Part(head, WorldMeshes.Ball(0.042f), new Vector3(0f, 0.2f, -0.25f), Vector3.one, l.skin);                                      // nose
        Part(head, WorldMeshes.Ball(0.03f), new Vector3(0f, 0.125f, -0.227f), new Vector3(1.7f, 0.35f, 0.5f), dark);                    // mouth

        // Hair: a cap tilted back (the hairline high at the front, low at the nape), volume at the back, and the
        // widow's peak pointing down the forehead.
        Part(head, WorldMeshes.Dome(10, 3, 0.262f), new Vector3(0f, 0.3f, 0.01f), Vector3.one, l.hair, new Vector3(24f, 0f, 0f));
        Part(head, WorldMeshes.Ball(0.12f), new Vector3(0f, 0.36f, 0.16f), new Vector3(1.6f, 1f, 1.3f), l.hair);
        Part(head, MeshKit.Cone(4, 0.065f, 0.11f), new Vector3(0f, 0.435f, -0.19f), new Vector3(1f, 1f, 0.45f), l.hair, new Vector3(30f, 0f, 180f));
    }

    // ------------------------------------------------------------------ cape and collar
    // A long cape hanging from the shoulders (it sways: CommanderIdle) and a high stand-up collar framing the head,
    // both black outside and crimson inside; the cape's hem is lined with the team colour.
    static void Cape(Transform body, CommanderIdle idle, Look l) {
        Transform cape = Pivot("Cape", body, new Vector3(0f, 0.92f, 0f));
        Part(cape, CapeMesh(ref capeOuter, "Cape Outer", 0f, -0.86f, 0f, false), Vector3.zero, Vector3.one, l.coat);
        Part(cape, CapeMesh(ref capeInner, "Cape Lining", 0f, -0.86f, 0f, true), Vector3.zero, Vector3.one, l.lining);
        Part(cape, CapeMesh(ref capeHem, "Cape Hem", -0.76f, -0.865f, -0.008f, true), Vector3.zero, Vector3.one, l.team);
        idle.cape = cape;
        Part(body, CollarMesh(ref collarOuter, "Collar Outer", false), Vector3.zero, Vector3.one, l.coat);
        Part(body, CollarMesh(ref collarInner, "Collar Lining", true), Vector3.zero, Vector3.one, l.lining);
    }

    static float CapeRadius(float y) { return Mathf.Lerp(0.26f, 0.38f, Mathf.InverseLerp(0f, -0.86f, y)); }

    // One side of a curved strip around the back (+z) between heights top and bottom (cape-local, 0 = shoulders),
    // flaring towards the hem, from 15 to 165 degrees (90 = straight behind). inner = the side facing the body
    // (and the camera around the figure); offset moves it towards that side.
    static Mesh CapeMesh(ref Mesh cache, string name, float top, float bottom, float offset, bool inner) {
        if (cache != null) return cache;
        MeshBuilder b = new MeshBuilder();
        const int cols = 9;
        int[,] v = new int[cols, 2];
        for (int i = 0; i < cols; i++) {
            float a = Mathf.Lerp(15f, 165f, i / (cols - 1f)) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            for (int row = 0; row < 2; row++) {
                float y = row == 0 ? bottom : top;
                v[i, row] = b.Vertex(dir * (CapeRadius(y) + (inner ? offset : -offset)) + Vector3.up * y, new Vector2(i / (cols - 1f), row));
            }
        }
        AddStrip(b, v, cols, inner);
        cache = b.ToMesh(name);
        return cache;
    }

    // The collar: from the neck (radius 0.19) up and out (0.36) behind the head, its top edge rising into points
    // beside the ears.
    static Mesh CollarMesh(ref Mesh cache, string name, bool inner) {
        if (cache != null) return cache;
        MeshBuilder b = new MeshBuilder();
        const int cols = 9;
        int[,] v = new int[cols, 2];
        for (int i = 0; i < cols; i++) {
            float a = Mathf.Lerp(25f, 155f, i / (cols - 1f)) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float topY = 1.3f + 0.2f * Mathf.Abs(Mathf.Cos(a));
            v[i, 0] = b.Vertex(dir * 0.19f + Vector3.up * 0.86f, new Vector2(i / (cols - 1f), 0f));
            v[i, 1] = b.Vertex(dir * 0.36f + Vector3.up * topY, new Vector2(i / (cols - 1f), 1f));
        }
        AddStrip(b, v, cols, inner);
        cache = b.ToMesh(name);
        return cache;
    }

    // Seen from behind, column i + 1 lies to the right of column i, so this winding faces outwards (+z);
    // the inner side uses the reverse.
    static void AddStrip(MeshBuilder b, int[,] v, int cols, bool inner) {
        for (int i = 0; i < cols - 1; i++) {
            if (inner) b.Quad(v[i + 1, 0], v[i + 1, 1], v[i, 1], v[i, 0]);
            else b.Quad(v[i, 0], v[i, 1], v[i + 1, 1], v[i + 1, 0]);
        }
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

using UnityEngine;

// The Gambler's look, registered with ClassLooks: the commander (this file), the block skins (GamblerLookSkins.cs) and
// the fortress decorations (GamblerLookDecor.cs).
// The commander is a riverboat card sharp dressed as a casino high roller: a tall black silk top hat cocked to one side,
// its band in the team colour with the ace of spades tucked in it; slicked black hair, heavy-lidded eyes under one raised
// brow, a thin pencil mustache over a sly, lopsided grin; a black tailcoat open over a burgundy waistcoat with gold
// buttons and a gold watch chain, a white shirt and a black bow tie, a team-colour pocket square, and the coat's tails
// hanging to the knees behind grey trousers. His coat sleeves are pushed up (nothing up his sleeves) over white shirt
// sleeves held by garters in the class neon. The off hand shows four aces (the one in his hat makes five) and the
// throwing hand pinches a face-down card, ready to flick it.
// The only letters the camera sees are the aces' "A", which reads the same mirrored (facing -1).
// Same rig contract and chibi proportions as CommanderModel (~1.65 m, the hat reaches ~1.85 m, feet at y = 0, faces -z
// turned a little towards the enemy, mirrored for facing -1, the throwing arm hangs along -y at rotation 0, no colliders).
public static class GamblerLook {
    const float BodyYaw = -15f, HeadYaw = -12f;          // negative yaw turns the face towards +x
    const float NeckY = 0.94f, ArmLength = 0.46f, HandY = 0.475f;
    const float FanCardSize = 0.18f, HatCardSize = 0.16f, HeldCardSize = 0.15f;   // card heights
    static readonly Vector3 ShoulderBody = new Vector3(0.27f, 0.86f, 0f);
    static readonly Vector3 Octagon = new Vector3(0f, 22.5f, 0f);   // turns an octagonal frustum flat side to the camera

    static readonly Color SkinTone = new Color(0.96f, 0.78f, 0.64f);
    static readonly Color HairBlack = new Color(0.07f, 0.055f, 0.05f);
    static readonly Color SilkBlack = new Color(0.06f, 0.06f, 0.07f);
    static readonly Color CoatBlack = new Color(0.085f, 0.085f, 0.1f);
    static readonly Color SatinBlack = new Color(0.035f, 0.035f, 0.04f);
    static readonly Color Burgundy = new Color(0.48f, 0.07f, 0.17f);
    static readonly Color ShirtWhite = new Color(0.96f, 0.95f, 0.92f);
    static readonly Color TrouserGrey = new Color(0.27f, 0.27f, 0.3f);

    static readonly PlayingCard AceOfSpades = new PlayingCard(CardRank.Ace, CardSuit.Spades);
    static readonly PlayingCard HiddenCard = new PlayingCard(CardRank.Joker, CardSuit.Hearts);
    static readonly PlayingCard[] Aces = {   // back to front, left to right
        new PlayingCard(CardRank.Ace, CardSuit.Clubs), new PlayingCard(CardRank.Ace, CardSuit.Diamonds),
        new PlayingCard(CardRank.Ace, CardSuit.Spades), new PlayingCard(CardRank.Ace, CardSuit.Hearts),
    };

    static Mesh tails;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() {
        ClassLooks.Register(HeroClass.Gambler, BuildCommander, GamblerSkins.Block, GamblerDecor.Apply);
    }

    // Materials of one model.
    class Look {
        public Material skin, hair, ink, mouth, silk, coat, satin, waistcoat, shirt, trousers, shoes, gold, garter, team;
    }

    public static CommanderRig BuildCommander(Color teamColor, int facing, Transform parent) {
        GameObject root = ModelKit.Root("Model", parent);
        root.transform.localScale = new Vector3(facing >= 0 ? 1f : -1f, 1f, 1f);
        CommanderRig rig = root.AddComponent<CommanderRig>();
        rig.armRest = 8f;
        CommanderIdle idle = root.AddComponent<CommanderIdle>();
        Look l = new Look {
            skin = Mats.Lit(SkinTone, 0.3f), hair = Mats.Lit(HairBlack, 0.85f), ink = Mats.Lit(new Color(0.08f, 0.07f, 0.09f), 0.7f),
            mouth = Mats.Lit(new Color(0.4f, 0.11f, 0.11f), 0.3f), silk = Mats.Lit(SilkBlack, 0.7f), coat = Mats.Lit(CoatBlack, 0.35f),
            satin = Mats.Lit(SatinBlack, 0.85f), waistcoat = Mats.Lit(Burgundy, 0.5f), shirt = Mats.Lit(ShirtWhite, 0.3f),
            trousers = Mats.Lit(TrouserGrey, 0.2f), shoes = Mats.Lit(new Color(0.05f, 0.05f, 0.06f), 0.85f, 0.2f),
            gold = GamblerKit.GoldMat, garter = Mats.Lit(GamblerKit.Neon, 0.45f), team = Mats.Lit(teamColor, 0.45f),
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
        Coat(idle.torso, rig.body, l);
        Head(rig.head, idle, l);
        Hat(rig.head, l);
        Arm(rig.armPivot, l);
        HeldCard(rig.hand, l);
        // The off arm reaches forward and a little out, showing his hand beside the body rather than over the waistcoat.
        Transform offArm = Pivot("OffArm", rig.body, new Vector3(-ShoulderBody.x, ShoulderBody.y, -0.02f), new Vector3(40f, 0f, -4f));
        Arm(offArm, l);
        rig.prop = Fan(rig.body, offArm, idle, l);

        rig.renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        return rig;
    }

    // ------------------------------------------------------------------ body
    // Grey morning trousers (the seat fills the gap under the coat's cut-away front) and glossy pointed black shoes.
    static void Legs(Transform body, Look l) {
        Part(body, WorldMeshes.Frustum(8, 0.232f, 0.24f, 0.2f), new Vector3(0f, 0.24f, 0f), new Vector3(1f, 1f, 0.82f), l.trousers, Octagon);
        for (int s = -1; s <= 1; s += 2) {
            Part(body, WorldMeshes.Frustum(6, 0.082f, 0.078f, 0.3f), new Vector3(s * 0.1f, 0.06f, 0f), Vector3.one, l.trousers);
            Part(body, WorldMeshes.Frustum(6, 0.09f, 0.075f, 0.1f), new Vector3(s * 0.1f, 0f, -0.02f), new Vector3(1f, 1f, 1.3f), l.shoes);
            Part(body, WorldMeshes.Ball(0.06f), new Vector3(s * 0.1f, 0.035f, -0.13f), new Vector3(0.85f, 0.55f, 1.5f), l.shoes);
        }
    }

    // The tailcoat: a black body (an octagon turned flat side to the camera) open over the waistcoat, satin lapels, square
    // shoulders and the tails. The burgundy waistcoat ends in two points below the waist and has four gold buttons and
    // the watch chain; a white shirt front above it, the bow tie at the collar and the pocket square on the breast.
    static void Coat(Transform t, Transform body, Look l) {
        Part(t, WorldMeshes.Frustum(8, 0.248f, 0.21f, 0.54f), new Vector3(0f, 0.42f, 0f), new Vector3(1f, 1f, 0.82f), l.coat, Octagon);
        Part(t, TailsMesh(), Vector3.zero, Vector3.one, l.coat);
        Part(t, PrimitiveType.Cube, new Vector3(0f, 0.6f, -0.178f), new Vector3(0.2f, 0.36f, 0.03f), l.waistcoat);
        Part(t, PrimitiveType.Cube, new Vector3(0f, 0.8f, -0.188f), new Vector3(0.09f, 0.24f, 0.02f), l.shirt);
        for (int s = -1; s <= 1; s += 2) {
            Part(t, MeshKit.Cone(4, 0.05f, 0.05f), new Vector3(s * 0.05f, 0.425f, -0.178f), new Vector3(1f, 1f, 0.3f), l.waistcoat, new Vector3(180f, 0f, 0f));
            Part(t, PrimitiveType.Cube, new Vector3(s * 0.105f, 0.69f, -0.195f), new Vector3(0.05f, 0.36f, 0.022f), l.satin, new Vector3(0f, 0f, s * 8f));   // lapels
            Part(t, WorldMeshes.Ball(0.105f), new Vector3(s * 0.235f, 0.86f, 0f), new Vector3(1.1f, 0.72f, 1.05f), l.coat);                                // shoulders
        }
        for (int k = 0; k < 4; k++)
            Part(t, WorldMeshes.Ball(0.016f), new Vector3(0f, 0.47f + k * 0.065f, -0.197f), Vector3.one, l.gold);
        WatchChain(t, l);
        PocketSquare(t, l);
        // The bow tie is on the body, not the breathing torso, so it stays put under the chin.
        Part(body, WorldMeshes.Ball(0.024f), new Vector3(0f, 0.885f, -0.205f), new Vector3(1f, 1.1f, 0.8f), l.satin);
        for (int s = -1; s <= 1; s += 2)
            Part(body, MeshKit.Cone(4, 0.04f, 0.07f), new Vector3(s * 0.07f, 0.885f, -0.198f), new Vector3(1f, 1f, 0.45f), l.satin, new Vector3(0f, 0f, s * 90f));
    }

    // A gold watch chain from the second button, sagging into the waistcoat pocket on the enemy side.
    static void WatchChain(Transform t, Look l) {
        Vector3 from = new Vector3(0.01f, 0.535f, -0.198f), to = new Vector3(0.075f, 0.52f, -0.198f);
        for (int k = 0; k <= 6; k++) {
            float u = k / 6f;
            Part(t, WorldMeshes.Ball(0.009f), Vector3.Lerp(from, to, u) + Vector3.down * (0.1f * u * (1f - u)), Vector3.one, l.gold);
        }
        Part(t, PrimitiveType.Cube, new Vector3(0.075f, 0.515f, -0.195f), new Vector3(0.045f, 0.008f, 0.008f), l.satin);   // the pocket
    }

    // A team-colour pocket square peeking out of the breast pocket, which sits on the coat's angled front face (turned
    // 39 degrees towards the enemy side), outside the lapel and clear of the hanging throwing arm.
    static void PocketSquare(Transform t, Look l) {
        Transform pocket = Pivot("PocketSquare", t, new Vector3(0.148f, 0.765f, -0.118f), new Vector3(0f, -39f, 0f));
        Part(pocket, PrimitiveType.Cube, new Vector3(0f, -0.004f, -0.004f), new Vector3(0.07f, 0.01f, 0.01f), l.satin);
        Part(pocket, MeshKit.Cone(4, 0.03f, 0.06f), new Vector3(-0.014f, 0f, -0.006f), new Vector3(0.85f, 1f, 0.35f), l.team, new Vector3(0f, 0f, 12f));
        Part(pocket, MeshKit.Cone(4, 0.03f, 0.06f), new Vector3(0.016f, 0f, -0.006f), new Vector3(0.85f, 0.8f, 0.35f), l.team, new Vector3(0f, 0f, -10f));
    }

    // An arm hanging from its pivot: the coat sleeve pushed up above the elbow, the white shirt sleeve below it held by a
    // neon garter, a white cuff with a gold link, the hand. The pivot is the joint.
    static void Arm(Transform pivot, Look l) {
        Part(pivot, WorldMeshes.Ball(0.095f), Vector3.zero, Vector3.one, l.coat);
        Part(pivot, WorldMeshes.Frustum(6, 0.076f, 0.082f, 0.2f), new Vector3(0f, -0.2f, 0f), Vector3.one, l.coat);
        Part(pivot, WorldMeshes.Frustum(6, 0.088f, 0.086f, 0.045f), new Vector3(0f, -0.235f, 0f), Vector3.one, l.coat);   // the pushed-up roll
        Part(pivot, WorldMeshes.Frustum(6, 0.068f, 0.074f, 0.2f), new Vector3(0f, -0.42f, 0f), Vector3.one, l.shirt);
        Part(pivot, WorldMeshes.Frustum(6, 0.079f, 0.079f, 0.03f), new Vector3(0f, -0.29f, 0f), Vector3.one, l.garter);
        Part(pivot, WorldMeshes.Frustum(6, 0.078f, 0.076f, 0.045f), new Vector3(0f, -0.435f, 0f), Vector3.one, l.shirt);   // cuff
        Part(pivot, WorldMeshes.Ball(0.012f), new Vector3(0f, -0.412f, -0.074f), Vector3.one, l.gold);                     // cuff link
        Part(pivot, WorldMeshes.Ball(0.07f), new Vector3(0f, -HandY, 0f), Vector3.one, l.skin);
    }

    // A face-down card pinched between the throwing hand's fingers and thumb, its red back to the camera: a card sharp
    // never shows his next card.
    static void HeldCard(Transform hand, Look l) {
        Transform pinch = Pivot("HeldCard", hand, new Vector3(0.045f, -0.075f, -0.045f), new Vector3(0f, 0f, 35f));
        GamblerKit.Card(pinch, HiddenCard, HeldCardSize, false);
        Part(hand, WorldMeshes.Ball(0.03f), new Vector3(0.02f, -0.035f, -0.07f), Vector3.one, l.skin);   // thumb
    }

    // Four aces fanned out in the off hand, faces to the camera, their feet in the fist and a thumb over them. The fan
    // rocks gently (CommanderIdle's plume).
    static Transform Fan(Transform body, Transform offArm, CommanderIdle idle, Look l) {
        Vector3 hand = offArm.localPosition + offArm.localRotation * new Vector3(0f, -HandY, 0f);
        Transform fan = Pivot("Fan", body, hand + new Vector3(0f, 0.02f, -0.03f), new Vector3(0f, 8f, 0f));   // turned back towards the camera
        Transform sway = Pivot("Sway", fan, Vector3.zero);
        idle.plume = sway;
        for (int k = 0; k < Aces.Length; k++) {
            float roll = Mathf.Lerp(36f, -36f, k / (Aces.Length - 1f));
            Transform slot = Pivot("Slot", sway, new Vector3(0f, 0f, -0.016f * k), new Vector3(0f, 0f, roll));   // each card in front of the last
            GamblerKit.Card(slot, Aces[k], FanCardSize).transform.localPosition = new Vector3(0f, FanCardSize * 0.5f + 0.02f, 0f);
        }
        Part(sway, WorldMeshes.Ball(0.03f), new Vector3(0.025f, 0.03f, -0.085f), Vector3.one, l.skin);
        return fan;
    }

    // The coat's two tails: panels around the back (90 degrees = straight behind, +z) from the waist down to the knees,
    // cut away towards their front edges, split by a vent at the back and flaring as they hang. Both sides are built,
    // each with its own vertices (so their normals stay apart): the camera sees the inside around the legs.
    static Mesh TailsMesh() {
        if (tails != null) return tails;
        MeshBuilder b = new MeshBuilder();
        Tail(b, 12f, 86f, true);
        Tail(b, 94f, 168f, false);
        tails = b.ToMesh("Coat Tails");
        return tails;
    }

    // One tail between the angles `from` and `to`; frontFirst = its front edge is at `from`.
    static void Tail(MeshBuilder b, float from, float to, bool frontFirst) {
        const int cols = 7;
        const float waist = 0.52f, hem = 0.14f;
        int[,] outer = new int[cols, 2], inner = new int[cols, 2];
        for (int i = 0; i < cols; i++) {
            float k = i / (cols - 1f);
            float a = Mathf.Lerp(from, to, k) * Mathf.Deg2Rad;
            float back = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((frontFirst ? k : 1f - k) * 1.8f));   // 0 at the front edge, 1 from mid-tail on
            for (int row = 0; row < 2; row++) {
                float y = row == 0 ? Mathf.Lerp(0.42f, hem, back) : waist;
                float flare = Mathf.InverseLerp(waist, hem, y);
                Vector3 p = new Vector3(Mathf.Cos(a) * Mathf.Lerp(0.25f, 0.33f, flare), y, Mathf.Sin(a) * Mathf.Lerp(0.2f, 0.29f, flare));
                outer[i, row] = b.Vertex(p, new Vector2(k, row));
                inner[i, row] = b.Vertex(p, new Vector2(k, row));
            }
        }
        // Seen from behind, column i + 1 lies to the right of column i, so this winding faces outwards; the inside reversed.
        for (int i = 0; i < cols - 1; i++) {
            b.Quad(outer[i, 0], outer[i, 1], outer[i + 1, 1], outer[i + 1, 0]);
            b.Quad(inner[i + 1, 0], inner[i + 1, 1], inner[i, 1], inner[i, 0]);
        }
    }

    // ------------------------------------------------------------------ head
    // Heavy-lidded eyes (the lids sit in the blinking pivot), one brow raised on the enemy side and the other low, a thin
    // pencil mustache over a lopsided grin showing the upper teeth; black hair slicked back under the hat, with sideburns.
    // Eyes, lids and the mouth parts share one ball size each, so they keep their proportions whatever the mesh cache
    // rounds the radius to.
    static void Head(Transform head, CommanderIdle idle, Look l) {
        Part(head, WorldMeshes.Ball(0.25f), new Vector3(0f, 0.24f, 0f), Vector3.one, l.skin);
        Part(head, WorldMeshes.Ball(0.045f), new Vector3(0f, 0.2f, -0.25f), Vector3.one, l.skin);   // nose
        Transform eyes = Pivot("Eyes", head, new Vector3(0f, 0.27f, 0f));
        idle.eyes = eyes;
        for (int s = -1; s <= 1; s += 2) {
            Part(head, WorldMeshes.Ball(0.06f), new Vector3(s * 0.25f, 0.24f, 0.01f), Vector3.one, l.skin);   // ears
            Part(eyes, WorldMeshes.Ball(0.036f), new Vector3(s * 0.085f, 0f, -0.222f), Vector3.one, l.ink);
            Part(eyes, WorldMeshes.Ball(0.036f), new Vector3(s * 0.085f, 0.018f, -0.226f), new Vector3(1.22f, 0.67f, 0.9f), l.skin);   // lid over the top
            Part(head, PrimitiveType.Cube, new Vector3(s * 0.235f, 0.27f, -0.06f), new Vector3(0.03f, 0.1f, 0.07f), l.hair);           // sideburns
            Part(head, PrimitiveType.Cube, new Vector3(s * 0.032f, 0.152f, -0.236f), new Vector3(0.052f, 0.011f, 0.014f), l.hair,
                 new Vector3(0f, -s * 15f, -s * 7f));                                                                                 // mustache halves
        }
        Part(head, PrimitiveType.Cube, new Vector3(0.09f, 0.36f, -0.205f), new Vector3(0.1f, 0.022f, 0.03f), l.hair, new Vector3(0f, -20f, -12f));   // raised brow
        Part(head, PrimitiveType.Cube, new Vector3(-0.09f, 0.325f, -0.212f), new Vector3(0.1f, 0.022f, 0.03f), l.hair, new Vector3(0f, 20f, -10f));  // low brow
        Part(head, WorldMeshes.Ball(0.03f), new Vector3(0.01f, 0.108f, -0.224f), new Vector3(2f, 0.55f, 0.55f), l.mouth, new Vector3(0f, 0f, 9f));
        Part(head, WorldMeshes.Ball(0.03f), new Vector3(0.011f, 0.113f, -0.232f), new Vector3(1.6f, 0.28f, 0.35f), l.shirt, new Vector3(0f, 0f, 9f));   // teeth
        Part(head, PrimitiveType.Cube, new Vector3(0.078f, 0.128f, -0.212f), new Vector3(0.024f, 0.007f, 0.01f), l.mouth, new Vector3(0f, 0f, 60f));   // smirk crease
        Part(head, WorldMeshes.Dome(10, 3, 0.262f), new Vector3(0f, 0.27f, 0.02f), Vector3.one, l.hair, new Vector3(30f, 0f, 0f));
        Part(head, WorldMeshes.Ball(0.12f), new Vector3(0f, 0.27f, 0.16f), new Vector3(1.5f, 0.8f, 1.1f), l.hair);
    }

    // ------------------------------------------------------------------ hat
    // A tall silk top hat cocked away from the enemy (its brim rises clear of the throwing arm's swing), its band in the
    // team colour with the ace of spades tucked in it on the side the camera sees.
    static void Hat(Transform head, Look l) {
        Transform hat = Pivot("Hat", head, new Vector3(0f, 0.39f, 0f), new Vector3(4f, 0f, 9f));
        Part(hat, WorldMeshes.Frustum(16, 0.31f, 0.3f, 0.025f), Vector3.zero, Vector3.one, l.silk);                    // brim
        Part(hat, WorldMeshes.Frustum(16, 0.205f, 0.222f, 0.45f), new Vector3(0f, 0.02f, 0f), Vector3.one, l.silk);    // crown
        Part(hat, WorldMeshes.Frustum(16, 0.213f, 0.215f, 0.075f), new Vector3(0f, 0.025f, 0f), Vector3.one, l.team);  // band
        float a = 38f * Mathf.Deg2Rad;
        Vector3 outward = new Vector3(-Mathf.Sin(a), 0f, -Mathf.Cos(a));
        Quaternion face = Quaternion.LookRotation(-outward);   // a card's face is its -z side
        Transform ace = GamblerKit.Card(hat, AceOfSpades, HatCardSize).transform;
        ace.localPosition = outward * 0.218f + Vector3.up * 0.135f;
        ace.localRotation = face * Quaternion.Euler(0f, 0f, 14f);
        // A piece of the band over the card's foot holds it in.
        Part(hat, PrimitiveType.Cube, outward * 0.226f + Vector3.up * 0.0625f, new Vector3(0.1f, 0.075f, 0.006f), l.team, face.eulerAngles);
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

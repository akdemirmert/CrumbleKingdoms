using UnityEngine;

// The Candidate's look, registered with ClassLooks: the commander (this file), the block skins (CandidateLookSkins.cs) and
// the fortress decorations (CandidateLookDecor.cs, which also starts his vote count).
// The commander is a campaigning politician of no party in particular: neat dark hair with a side part, swept up from it
// into a glossy wave; eyebrows raised high over a big, confident, toothy grin and a firm chin; a dark navy suit with padded
// shoulders, dark satin lapels and a gold button; a white shirt and a tie in the team colour; a teal sash across the
// chest, from under the throwing shoulder's pad down to the far hip, where it is knotted; and on the chest beside the
// lapel a round rosette with gold pleats and team-colour ribbons bearing his mark, a teal tick in a circle. The off hand is
// raised high in a campaign wave (it rocks gently: CommanderIdle's plume); the throwing hand holds a big fountain pen,
// ready to sign.
// No letters anywhere; the rosette is turned back for facing -1, so the tick never reads backwards.
// Same rig contract and chibi proportions as CommanderModel (~1.5 m to the top of the hair, feet at y = 0, faces -z
// turned a little towards the enemy, mirrored for facing -1, the throwing arm hangs along -y at rotation 0, no colliders).
public static class CandidateLook {
    const float BodyYaw = -15f, HeadYaw = -12f;          // negative yaw turns the face towards +x
    const float NeckY = 0.94f, ArmLength = 0.46f, HandY = 0.465f;
    // The jacket: an octagonal frustum turned flat side to the camera, then squashed front to back.
    const float JacketY = 0.32f, JacketH = 0.64f, JacketR0 = 0.255f, JacketR1 = 0.21f, Squash = 0.82f;
    const float FaceFront = 270f, FaceOff = 225f;        // jacket faces, by the angle of their middle: the front, the off-side front
    // The shirt's V between the lapels, in the front face's plane (x across, y up as the torso's).
    const float VTop = 0.955f, VBottom = 0.715f, VHalf = 0.05f, LapelWidth = 0.028f;
    // The sash lies in a plane rising SashSlope per metre towards the throwing side (+x), SashY high on the body's axis.
    const float SashY = 0.625f, SashSlope = 1.05f, SashHalfWidth = 0.036f, SashLift = 0.01f;
    static readonly float Cos22 = Mathf.Cos(22.5f * Mathf.Deg2Rad);
    static readonly Vector3 ShoulderBody = new Vector3(0.27f, 0.86f, 0f);
    static readonly Vector3 Octagon = new Vector3(0f, 22.5f, 0f);   // turns an octagonal frustum flat side to the camera

    static readonly Color SkinTone = new Color(0.96f, 0.77f, 0.63f);
    static readonly Color HairBrown = new Color(0.15f, 0.1f, 0.07f);
    static readonly Color LapelNavy = new Color(0.06f, 0.08f, 0.19f);
    static readonly Color ShirtWhite = new Color(0.96f, 0.96f, 0.94f);
    static readonly Color PenNavy = new Color(0.04f, 0.06f, 0.15f);

    static Mesh sash;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() {
        ClassLooks.Register(HeroClass.Candidate, BuildCommander, CandidateSkins.Block, CandidateDecor.Apply);
    }

    // Materials of one model.
    class Look {
        public Material skin, hair, eye, white, mouth, blush, suit, lapel, shirt, shoes, pen, gold, teal, paper, team;
    }

    public static CommanderRig BuildCommander(Color teamColor, int facing, Transform parent) {
        GameObject root = ModelKit.Root("Model", parent);
        root.transform.localScale = new Vector3(facing >= 0 ? 1f : -1f, 1f, 1f);
        CommanderRig rig = root.AddComponent<CommanderRig>();
        rig.armRest = 8f;
        CommanderIdle idle = root.AddComponent<CommanderIdle>();
        Look l = new Look {
            skin = Mats.Lit(SkinTone, 0.3f), hair = Mats.Lit(HairBrown, 0.7f), eye = Mats.Lit(new Color(0.08f, 0.07f, 0.09f), 0.7f),
            white = Mats.Lit(new Color(0.98f, 0.98f, 0.96f), 0.6f), mouth = Mats.Lit(new Color(0.42f, 0.1f, 0.12f), 0.3f),
            blush = Mats.Lit(new Color(1f, 0.62f, 0.6f), 0.3f), suit = CandidateKit.NavyMat, lapel = Mats.Lit(LapelNavy, 0.65f),
            shirt = Mats.Lit(ShirtWhite, 0.3f), shoes = Mats.Lit(new Color(0.05f, 0.05f, 0.06f), 0.85f, 0.2f),
            pen = Mats.Lit(PenNavy, 0.85f, 0.3f), gold = CandidateKit.GoldMat, teal = CandidateKit.TealMat,
            paper = CandidateKit.PaperMat, team = Mats.Lit(teamColor, 0.55f),
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
        Suit(idle.torso, l);
        Sash(idle.torso, l);
        Rosette(idle.torso, l, facing);
        Head(rig.head, idle, l);
        Hair(rig.head, l);
        Arm(rig.armPivot, l);
        Pen(rig.hand, l);
        rig.prop = WavingArm(rig.body, idle, l);

        rig.renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        return rig;
    }

    // ------------------------------------------------------------------ body
    // Navy trouser legs reaching up into the jacket, and glossy black shoes.
    static void Legs(Transform body, Look l) {
        for (int s = -1; s <= 1; s += 2) {
            Part(body, WorldMeshes.Frustum(6, 0.082f, 0.078f, 0.3f), new Vector3(s * 0.1f, 0.06f, 0f), Vector3.one, l.suit);
            Part(body, WorldMeshes.Frustum(6, 0.09f, 0.075f, 0.1f), new Vector3(s * 0.1f, 0f, -0.02f), new Vector3(1f, 1f, 1.3f), l.shoes);
            Sphere(body, 0.06f, new Vector3(s * 0.1f, 0.035f, -0.13f), new Vector3(0.85f, 0.55f, 1.5f), l.shoes);   // toe caps
        }
    }

    // The navy jacket with padded shoulders. On its front face: the white shirt's V between dark satin lapels, the
    // shirt's collar points over them, the team-colour tie (a knot and a blade with a centre crease, running down into
    // the jacket where the lapels meet) and one gold button below the sash.
    static void Suit(Transform t, Look l) {
        Transform jacket = Pivot("Jacket", t, Vector3.zero);
        jacket.localScale = new Vector3(1f, 1f, Squash);   // squashed after the turn, so the front face stays square to the camera
        Part(jacket, WorldMeshes.Frustum(8, JacketR0, JacketR1, JacketH), new Vector3(0f, JacketY, 0f), Vector3.one, l.suit, Octagon);
        for (int s = -1; s <= 1; s += 2)
            Sphere(t, 0.105f, new Vector3(s * 0.235f, 0.86f, 0f), new Vector3(1.1f, 0.72f, 1.05f), l.suit);   // shoulder pads

        Transform front = OnJacket("Front", t, FaceFront, 0f, 0f);   // the front face's plane: x and y as the torso's
        Part(front, MeshKit.Wedge(Vector3.one), new Vector3(0f, VTop, -0.002f), new Vector3(2f * VHalf, VTop - VBottom, 0.004f), l.shirt,
             new Vector3(0f, 0f, 180f));                                                                    // the V, point down
        Vector2 edge = new Vector2(VHalf, VTop - VBottom);
        float tilt = Mathf.Atan2(edge.x, edge.y) * Mathf.Rad2Deg;
        for (int s = -1; s <= 1; s += 2) {
            Vector2 c = new Vector2(s * VHalf * 0.5f, (VTop + VBottom) * 0.5f) + new Vector2(s * edge.y, -edge.x).normalized * (LapelWidth * 0.5f);
            Part(front, PrimitiveType.Cube, new Vector3(c.x, c.y, -0.011f), new Vector3(LapelWidth, edge.magnitude, 0.02f), l.lapel,
                 new Vector3(0f, 0f, -s * tilt));                                                           // lapel along the V
            Part(front, MeshKit.Wedge(Vector3.one), new Vector3(s * 0.02f, 0.937f, -0.013f), new Vector3(0.042f, 0.055f, 0.02f), l.shirt,
                 new Vector3(0f, 0f, 180f + s * 25f));                                                      // collar point
        }
        // Four-sided frustums squashed front to back: a flat diamond whose front corner is the tie's crease.
        Part(front, WorldMeshes.Frustum(4, 0.017f, 0.023f, 0.04f), new Vector3(0f, 0.895f, -0.009f), new Vector3(1f, 1f, 0.3f), l.team);
        Part(front, WorldMeshes.Frustum(4, 0.028f, 0.021f, 0.17f), new Vector3(0f, 0.725f, -0.008f), new Vector3(1f, 1f, 0.25f), l.team);
        Sphere(front, 0.016f, new Vector3(0f, 0.48f, -0.006f), new Vector3(1f, 1f, 0.6f), l.gold);   // button
    }

    // A teal sash all the way round the jacket, rising from the far hip across the chest to under the throwing shoulder's
    // pad (and down the back again); knotted on the hip, with two swallow-tailed ends hanging to the jacket's hem.
    static void Sash(Transform t, Look l) {
        Part(t, SashMesh(), Vector3.zero, Vector3.one, l.teal);
        Transform knot = OnJacket("SashKnot", t, FaceOff, SashCentre(FaceOff), SashLift);
        for (int s = -1; s <= 1; s += 2)
            Part(knot, WorldMeshes.Banner(0.04f, 0.125f, 0.016f), new Vector3(s * 0.014f, -0.01f, -0.004f), Vector3.one, l.teal,
                 new Vector3(0f, 0f, s * 12f));
        Sphere(knot, 0.036f, new Vector3(0f, 0f, -0.01f), new Vector3(1.15f, 0.85f, 0.45f), l.teal);
    }

    // A round campaign rosette pinned beside the off-side lapel, where the camera sees the chest best: two team-colour
    // ribbons, gold pleats, and a white face with his mark, a teal tick in a teal circle. Turned back for facing -1 (the
    // whole model is mirrored then), so the tick never reads backwards.
    static void Rosette(Transform t, Look l, int facing) {
        Transform r = OnJacket("Rosette", t, FaceOff, 0.79f, 0.004f);
        if (facing < 0) r.localScale = new Vector3(-1f, 1f, 1f);
        for (int s = -1; s <= 1; s += 2)
            Part(r, WorldMeshes.Banner(0.03f, 0.085f, 0.014f), new Vector3(s * 0.016f, -0.035f, -0.001f), Vector3.one, l.team,
                 new Vector3(0f, 0f, s * 14f));
        Part(r, WorldMeshes.Disc(20, 0.058f), new Vector3(0f, 0f, -0.004f), Vector3.one, l.gold);
        for (int k = 0; k < 12; k++) {
            float a = k * 30f * Mathf.Deg2Rad;
            Sphere(r, 0.014f, new Vector3(Mathf.Cos(a) * 0.056f, Mathf.Sin(a) * 0.056f, -0.003f), new Vector3(1f, 1f, 0.5f), l.gold);
        }
        Part(r, WorldMeshes.Disc(20, 0.042f), new Vector3(0f, 0f, -0.007f), Vector3.one, l.paper);
        Part(r, WorldMeshes.Disc(20, 0.036f), new Vector3(0f, 0f, -0.009f), Vector3.one, l.teal);   // the circle...
        Part(r, WorldMeshes.Disc(20, 0.029f), new Vector3(0f, 0f, -0.011f), Vector3.one, l.paper);
        Tick(r, new Vector3(0f, 0f, -0.013f), 0.029f, l.teal);                                       // ...and the tick in it
    }

    // His mark's tick, in the x-y plane of `parent` (seen from -z), fitting a circle of radius `size` around `at`.
    static void Tick(Transform parent, Vector3 at, float size, Material m) {
        Vector2 a = new Vector2(-0.58f, 0f) * size, b = new Vector2(-0.16f, -0.47f) * size, c = new Vector2(0.62f, 0.55f) * size;
        Stroke(parent, at, a, b, 0.24f * size, m);
        Stroke(parent, at, b, c, 0.24f * size, m);
    }

    // A straight stroke from a to b, overrunning both ends by half its width so the tick's corner closes.
    static void Stroke(Transform parent, Vector3 at, Vector2 a, Vector2 b, float width, Material m) {
        Vector2 d = b - a;
        Part(parent, PrimitiveType.Cube, at + (Vector3)((a + b) * 0.5f), new Vector3(d.magnitude + width, width, 0.003f), m,
             new Vector3(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg));
    }

    // ------------------------------------------------------------------ head
    // A big, confident grin (an open mouth with a row of white teeth across its top, its corners turned up into the
    // cheeks), eyebrows raised high in two arches, a twinkle in each eye, rosy cheeks and a firm chin.
    static void Head(Transform head, CommanderIdle idle, Look l) {
        Sphere(head, 0.25f, new Vector3(0f, 0.24f, 0f), Vector3.one, l.skin);
        Sphere(head, 0.09f, new Vector3(0f, 0.06f, -0.12f), new Vector3(1.4f, 0.7f, 1f), l.skin);   // chin
        Sphere(head, 0.047f, new Vector3(0f, 0.2f, -0.25f), Vector3.one, l.skin);                    // nose
        Transform eyes = Pivot("Eyes", head, new Vector3(0f, 0.27f, 0f));
        idle.eyes = eyes;
        for (int s = -1; s <= 1; s += 2) {
            Sphere(head, 0.06f, new Vector3(s * 0.25f, 0.24f, 0.01f), Vector3.one, l.skin);                                   // ears
            Sphere(eyes, 0.036f, new Vector3(s * 0.085f, 0f, -0.222f), Vector3.one, l.eye);
            Sphere(eyes, 0.011f, new Vector3(s * 0.085f + 0.012f, 0.013f, -0.255f), Vector3.one, l.white);                     // twinkle
            Sphere(head, 0.05f, new Vector3(s * 0.145f, 0.175f, -0.198f), new Vector3(1f, 0.6f, 0.5f), l.blush);              // cheeks
            // Each brow is an arch: an inner piece rising outwards and an outer one falling away, turned with the head.
            Part(head, PrimitiveType.Cube, new Vector3(s * 0.062f, 0.372f, -0.2f), new Vector3(0.055f, 0.022f, 0.03f), l.hair,
                 new Vector3(0f, -s * 14f, s * 10f));
            Part(head, PrimitiveType.Cube, new Vector3(s * 0.112f, 0.368f, -0.183f), new Vector3(0.05f, 0.022f, 0.03f), l.hair,
                 new Vector3(0f, -s * 30f, -s * 22f));
            Part(head, PrimitiveType.Cube, new Vector3(s * 0.083f, 0.142f, -0.212f), new Vector3(0.028f, 0.009f, 0.016f), l.mouth,
                 new Vector3(0f, -s * 22f, s * 38f));                                                                          // smile corner
        }
        Sphere(head, 0.05f, new Vector3(0f, 0.125f, -0.222f), new Vector3(1.5f, 0.68f, 0.4f), l.mouth);
        Sphere(head, 0.05f, new Vector3(0f, 0.137f, -0.231f), new Vector3(1.22f, 0.34f, 0.34f), l.white);   // teeth
    }

    // Neat dark hair: a cap tilted back over a high forehead, combed flat on the far side of a side part and swept up from
    // it into a glossy wave over the brow, falling away towards the enemy; a full back and short sideburns.
    static void Hair(Transform head, Look l) {
        Part(head, WorldMeshes.Dome(12, 4, 0.265f), new Vector3(0f, 0.27f, 0.025f), Vector3.one, l.hair, new Vector3(40f, 0f, 0f));
        Sphere(head, 0.12f, new Vector3(0f, 0.25f, 0.16f), new Vector3(1.55f, 0.85f, 1.1f), l.hair);
        Sphere(head, 0.1f, new Vector3(0.06f, 0.465f, -0.06f), new Vector3(1.8f, 0.75f, 1.45f), l.hair, new Vector3(0f, 0f, -10f));   // the wave
        for (int s = -1; s <= 1; s += 2)
            Part(head, PrimitiveType.Cube, new Vector3(s * 0.237f, 0.27f, -0.05f), new Vector3(0.03f, 0.09f, 0.07f), l.hair);
    }

    // ------------------------------------------------------------------ arms
    // An arm hanging from its pivot: the suit sleeve, a white shirt cuff, the hand. The pivot is the joint.
    static void Arm(Transform pivot, Look l) {
        Sphere(pivot, 0.095f, Vector3.zero, Vector3.one, l.suit);
        Part(pivot, WorldMeshes.Frustum(6, 0.074f, 0.082f, 0.37f), new Vector3(0f, -0.4f, 0f), Vector3.one, l.suit);
        Part(pivot, WorldMeshes.Frustum(6, 0.068f, 0.068f, 0.04f), new Vector3(0f, -0.425f, 0f), Vector3.one, l.shirt);   // cuff
        Sphere(pivot, 0.07f, new Vector3(0f, -HandY, 0f), Vector3.one, l.skin);
    }

    // A big fountain pen through the throwing fist, nib forward and down, ready to sign: navy lacquer with a gold end,
    // cap band, clip, ring and nib, the thumb pressed over it.
    static void Pen(Transform hand, Look l) {
        Transform pen = Pivot("Pen", hand, new Vector3(0.005f, -0.01f, -0.03f), new Vector3(0f, 0f, -125f));   // +y points to the nib
        Part(pen, WorldMeshes.Frustum(8, 0.02f, 0.019f, 0.21f), new Vector3(0f, -0.13f, 0f), Vector3.one, l.pen);     // cap and barrel
        Sphere(pen, 0.02f, new Vector3(0f, -0.13f, 0f), new Vector3(1f, 0.7f, 1f), l.gold);
        Part(pen, WorldMeshes.Frustum(8, 0.022f, 0.022f, 0.012f), new Vector3(0f, -0.086f, 0f), Vector3.one, l.gold);
        Part(pen, PrimitiveType.Cube, new Vector3(0f, -0.105f, -0.022f), new Vector3(0.008f, 0.042f, 0.005f), l.gold);   // clip
        Part(pen, WorldMeshes.Frustum(8, 0.016f, 0.013f, 0.04f), new Vector3(0f, 0.08f, 0f), Vector3.one, l.pen);       // grip
        Part(pen, WorldMeshes.Frustum(8, 0.02f, 0.02f, 0.008f), new Vector3(0f, 0.078f, 0f), Vector3.one, l.gold);
        Part(pen, MeshKit.Cone(4, 0.5f, 1f), new Vector3(0f, 0.12f, 0f), new Vector3(0.028f, 0.05f, 0.012f), l.gold);   // nib
        Sphere(hand, 0.028f, new Vector3(0.03f, -0.035f, -0.055f), Vector3.one, l.skin);                               // thumb
    }

    // The off arm raised high in a campaign wave: the upper arm out and up, the forearm upright and the open palm to the
    // crowd (the camera), the thumb towards his head. The forearm rocks from the elbow (CommanderIdle's plume).
    static Transform WavingArm(Transform body, CommanderIdle idle, Look l) {
        Transform arm = Pivot("OffArm", body, new Vector3(-ShoulderBody.x, ShoulderBody.y, -0.02f), new Vector3(0f, 0f, -145f));
        Sphere(arm, 0.095f, Vector3.zero, Vector3.one, l.suit);
        Part(arm, WorldMeshes.Frustum(6, 0.078f, 0.082f, 0.2f), new Vector3(0f, -0.2f, 0f), Vector3.one, l.suit);
        Transform elbow = Pivot("Elbow", arm, new Vector3(0f, -0.2f, 0f), new Vector3(0f, 0f, -25f));
        Transform wave = Pivot("Wave", elbow, Vector3.zero);
        idle.plume = wave;
        Sphere(wave, 0.08f, Vector3.zero, Vector3.one, l.suit);
        Part(wave, WorldMeshes.Frustum(6, 0.072f, 0.078f, 0.17f), new Vector3(0f, -0.17f, 0f), Vector3.one, l.suit);
        Part(wave, WorldMeshes.Frustum(6, 0.066f, 0.066f, 0.04f), new Vector3(0f, -0.2f, 0f), Vector3.one, l.shirt);   // cuff
        Sphere(wave, 0.062f, new Vector3(0f, -0.255f, 0f), new Vector3(1.05f, 1.05f, 0.55f), l.skin);                 // palm
        for (int k = 0; k < 4; k++) {                                                                                  // fingers, fanned
            float side = k - 1.5f, length = k == 1 || k == 2 ? 1f : 0.85f;
            Sphere(wave, 0.019f, new Vector3(side * 0.031f, -0.315f - 0.02f * length, 0f), new Vector3(0.85f, 2.1f * length, 0.75f), l.skin,
                   new Vector3(0f, 0f, side * 7f));
        }
        Sphere(wave, 0.019f, new Vector3(-0.075f, -0.265f, -0.005f), new Vector3(0.9f, 1.9f, 0.8f), l.skin, new Vector3(0f, 0f, -50f));   // thumb
        return arm;
    }

    // ------------------------------------------------------------------ jacket geometry
    // The jacket's radius (its octagon's corners) at height y, also beyond its ends.
    static float JacketR(float y) { return Mathf.LerpUnclamped(JacketR0, JacketR1, (y - JacketY) / JacketH); }

    // A point on the jacket's surface at height y (torso space), `angle` degrees around it (0 = +x, 270 = the middle of the
    // front face). Its faces have their middles at multiples of 45 degrees.
    static Vector3 JacketPoint(float angle, float y) {
        float fromMiddle = Mathf.Repeat(angle + 22.5f, 45f) - 22.5f;
        float rho = JacketR(y) * Cos22 / Mathf.Cos(fromMiddle * Mathf.Deg2Rad);
        float a = angle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a) * rho, y, Mathf.Sin(a) * rho * Squash);
    }

    // A pivot on the middle of the jacket face at `angle` (a multiple of 45), at height y and `lift` out of it: x along the
    // face, y up it (the faces lean back as the jacket narrows), -z out of it.
    static Transform OnJacket(string name, Transform torso, float angle, float y, float lift) {
        float a = angle * Mathf.Deg2Rad;
        Vector3 n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a) / Squash).normalized;
        Vector3 p = JacketPoint(angle, y);
        float lean = Mathf.Atan2(Vector3.Dot(p - JacketPoint(angle, y + 0.1f), n), 0.1f) * Mathf.Rad2Deg;
        Transform t = Pivot(name, torso, p + n * lift);
        t.localRotation = Quaternion.LookRotation(-n) * Quaternion.Euler(lean, 0f, 0f);
        return t;
    }

    // The height of the sash's middle where it crosses the jacket at `angle` (the radius changes with height: settle it).
    static float SashCentre(float angle) {
        float y = SashY;
        for (int k = 0; k < 3; k++) y = SashY + SashSlope * JacketPoint(angle, y).x;
        return y;
    }

    // The sash: a band round the jacket just outside its faces, bending at its corners (every third column is one). Kept
    // on the jacket, so under the shoulder pad its top edge stops at the jacket's top.
    static Mesh SashMesh() {
        if (sash != null) return sash;
        const int Steps = 48;
        float half = SashHalfWidth * Mathf.Sqrt(1f + SashSlope * SashSlope);   // half its height, for SashHalfWidth across it
        MeshBuilder b = new MeshBuilder();
        int lastLo = -1, lastHi = -1;
        for (int i = 0; i <= Steps; i++) {
            float angle = i * 360f / Steps, mid = SashCentre(angle), u = i / (float)Steps;
            int lo = b.Vertex(Lifted(JacketPoint(angle, Mathf.Max(mid - half, JacketY + 0.01f))), new Vector2(u, 0f));
            int hi = b.Vertex(Lifted(JacketPoint(angle, Mathf.Min(mid + half, JacketY + JacketH - 0.01f))), new Vector2(u, 1f));
            if (i > 0) b.Quad(lastLo, lastHi, hi, lo);   // seen from outside, a growing angle runs to the right
            lastLo = lo;
            lastHi = hi;
        }
        sash = b.ToMesh("Candidate Sash");
        return sash;
    }

    static Vector3 Lifted(Vector3 p) {
        return p + new Vector3(p.x, 0f, p.z).normalized * SashLift;
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

    // A ball of exactly this radius: the mesh cache rounds radii to the centimetre, so one cached ball is scaled instead.
    static GameObject Sphere(Transform parent, float radius, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default) {
        return ModelKit.MeshPart(parent, WorldMeshes.Ball(0.5f), pos, scale * (2f * radius), m, euler);
    }
}

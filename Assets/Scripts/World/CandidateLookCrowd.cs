using System.Collections.Generic;
using UnityEngine;

// The Candidate's supporters. As his votes grow (CandidateVotes.Tier) they gather in the background beside his fortress:
// on the open ground on the enemy's side (the battle camera frames the fortresses tightly, so the far side is off screen),
// but well behind the line of fire (two rows deep in z). Two more every tier up to ten, each popping in with a burst of
// confetti. They bounce on their toes and clap all the time, and cheer (jump high, arms up and waving) when a new tier comes
// in or he lands a hit. Every third holds up a placard with his tick, every fourth a little pennant. Visual only: no
// colliders, no physics. Spots without ground under them (the sea, a cliff edge) are skipped. When his fortress falls they
// hang their heads and sink away.
public class CandidateCrowd : MonoBehaviour {
    const int PerTier = 2, Most = 10;
    const float FirstGap = 2.2f, Spacing = 0.8f;    // m from the fortress's front edge to the first (past his podium), then between two in a row
    const float FrontRowZ = 1.7f, BackRowZ = 2.6f;  // behind the blocks (z up to 0.8) and the shots (z 0)
    const float Size = 1.3f, EdgeMargin = 0.5f;     // larger than life, so they read on the overview
    const float CheerTime = 1.6f, HitCheer = 1.1f, PopTime = 0.35f, LeaveTime = 0.8f;
    static readonly Color[] Shirts = {
        CandidateKit.Teal, new Color(0.96f, 0.96f, 0.94f), CandidateKit.Gold, new Color(0.9f, 0.16f, 0.2f), CandidateKit.Navy,
    };
    static readonly Color[] Skins = { new Color(0.96f, 0.78f, 0.64f), new Color(0.8f, 0.58f, 0.42f), new Color(0.5f, 0.33f, 0.22f) };
    static readonly Color[] Hairs = { new Color(0.12f, 0.08f, 0.05f), new Color(0.45f, 0.28f, 0.13f), new Color(0.85f, 0.7f, 0.38f) };

    class Fan {
        public Transform root, body, armL, armR;
        public Vector3 spot;
        public float phase, rate, hop, born = -1f;
        public bool sign, pennant;
    }

    Fortress fortress;
    readonly List<Fan> fans = new List<Fan>();
    int shown;
    float cheerUntil, lastVotes, leaving = -1f;

    public static CandidateCrowd Create(Fortress f) {
        GameObject go = new GameObject("Supporters");
        go.transform.SetParent(f.transform, false);   // goes with the battle
        CandidateCrowd c = go.AddComponent<CandidateCrowd>();
        c.fortress = f;
        return c;
    }

    // The fortress is complete by now: find the spots beside it and build everybody (hidden until their tier comes).
    void Start() {
        if (fortress == null) { Destroy(gameObject); return; }
        Bounds b = fortress.GetBounds();
        int front = fortress.Facing >= 0 ? 1 : -1;                        // towards the enemy
        float edge = front > 0 ? b.max.x : b.min.x, floorY = fortress.transform.position.y;
        for (int i = 0; i < Most; i++) {
            float x = edge + front * (FirstGap + (i / 2) * Spacing + (i % 2) * Spacing * 0.5f);
            if (!Level(x, floorY, out float y) || !Level(x + front * EdgeMargin, floorY, out _)) continue;   // the sea or a cliff edge
            Vector3 spot = new Vector3(x, y, i % 2 == 0 ? FrontRowZ : BackRowZ);                             // two rows, the second further back
            fans.Add(Build(fans.Count, spot, front));
        }
    }

    // Ground at about the fortress's own level at x (not the sea, not a terrace above or below).
    static bool Level(float x, float floorY, out float y) {
        return LichKit.GroundAt(x, out y) && Mathf.Abs(y - floorY) <= 1.2f;
    }

    void Update() {
        if (fortress == null) { Destroy(gameObject); return; }
        float t = Time.time;
        if (leaving < 0f && (fortress.defeated || !fortress.CommanderAlive)) leaving = t;
        if (leaving >= 0f) { Leave(t); return; }

        float votes = CandidateVotes.Votes(fortress);
        if (votes > lastVotes + 0.5f) cheerUntil = Mathf.Max(cheerUntil, t + HitCheer);   // he landed a hit
        lastVotes = votes;
        int want = Mathf.Min(fans.Count, CandidateVotes.Tier(fortress) * PerTier);
        if (want > shown) {
            for (int i = shown; i < want; i++) Arrive(fans[i], t);
            shown = want;
            cheerUntil = t + CheerTime;
        }
        bool cheering = t < cheerUntil;
        for (int i = 0; i < shown; i++) Animate(fans[i], t, cheering);
    }

    // A new supporter pops up out of the crowd with a burst of confetti.
    void Arrive(Fan f, float t) {
        f.born = t;
        f.root.gameObject.SetActive(true);
        Vector3 at = f.spot + new Vector3(0f, 0.7f * Size, -0.3f);      // just in front of him, at his depth
        foreach (Color c in Shirts)
            FxLayers.Star.Emit(at, new Vector3(Random.Range(-2f, 2f), Random.Range(2f, 4.5f), 0f), Random.Range(0.12f, 0.2f), Random.Range(0.5f, 0.8f), c);
    }

    // Bouncing and clapping (or pumping a placard); cheering: big jumps with both arms up, waving.
    void Animate(Fan f, float t, bool cheering) {
        float k = Mathf.Clamp01((t - f.born) / PopTime);
        float grow = k < 1f ? 1f + 2.70158f * Mathf.Pow(k - 1f, 3f) + 1.70158f * Mathf.Pow(k - 1f, 2f) : 1f;   // pops in, a little too big
        f.root.localScale = Vector3.one * (Size * Mathf.Max(0.01f, grow));
        float s = t * f.rate + f.phase;
        float jump = (cheering ? 0.38f : f.hop) * Size * Mathf.Abs(Mathf.Sin(s * Mathf.PI));
        f.root.position = f.spot + Vector3.up * jump;
        float clap = 0.5f + 0.5f * Mathf.Sin(s * Mathf.PI * 3f);
        if (cheering) {
            float wave = 18f * Mathf.Sin(s * Mathf.PI * 2f);
            f.armL.localRotation = Quaternion.Euler(0f, 0f, -150f + wave);
            f.armR.localRotation = Quaternion.Euler(0f, 0f, 150f - wave);
        } else if (f.sign || f.pennant) {
            f.armL.localRotation = Quaternion.Euler(0f, 0f, -45f - 30f * clap);
            f.armR.localRotation = Quaternion.Euler(0f, 0f, 135f + 25f * clap);   // pumping what he holds up
        } else {   // clapping over the head: both arms up and in, the hands meeting and parting
            float apart = 14f * (1f - clap);
            f.armL.localRotation = Quaternion.Euler(0f, 0f, 148f + apart);
            f.armR.localRotation = Quaternion.Euler(0f, 0f, -148f - apart);
        }
        f.body.localRotation = Quaternion.Euler(0f, 0f, 4f * Mathf.Sin(s * Mathf.PI * 0.5f));
    }

    // His fortress fell: heads down, and they sink away.
    void Leave(float t) {
        float k = Mathf.Clamp01((t - leaving) / LeaveTime);
        for (int i = 0; i < shown; i++) {
            Fan f = fans[i];
            f.armL.localRotation = Quaternion.Euler(0f, 0f, -8f);
            f.armR.localRotation = Quaternion.Euler(0f, 0f, 8f);
            f.body.localRotation = Quaternion.Euler(18f, 0f, 0f);
            f.root.position = f.spot + Vector3.down * 0.9f * Size * k * k;
            f.root.localScale = Vector3.one * (Size * (1f - k));
        }
        if (k >= 1f) Destroy(gameObject);
    }

    // ------------------------------------------------------------------ model
    // About a metre tall, facing the camera, turned a little towards the fortress: legs, a shirt in the campaign colours,
    // a head with hair or a cap, and two arms on shoulder pivots (rotation 0 = hanging down; positive z swings an arm
    // counter-clockwise as the camera sees it: the right arm out and up, the left arm in and up). Every third holds a
    // placard with his tick, every fourth a pennant.
    Fan Build(int index, Vector3 spot, int front) {
        Fan f = new Fan { spot = spot, phase = Random.value * 2f, rate = Random.Range(1.6f, 2.2f), hop = Random.Range(0.06f, 0.13f) };
        f.root = ModelKit.Root("Supporter", transform).transform;
        f.root.position = spot;
        f.root.rotation = Quaternion.Euler(0f, front * 18f, 0f);   // a little towards the fortress (behind him, away from the enemy)
        Material shirt = Mats.Lit(Shirts[index % Shirts.Length], 0.35f), skin = Mats.Lit(Skins[(index * 7 + 1) % Skins.Length], 0.3f);
        Material hair = Mats.Lit(Hairs[(index * 5 + 2) % Hairs.Length], 0.5f), dark = Mats.Lit(new Color(0.16f, 0.17f, 0.22f), 0.3f);
        Mesh ball = WorldMeshes.Ball(0.5f);
        for (int s = -1; s <= 1; s += 2)
            ModelKit.MeshPart(f.root, WorldMeshes.Frustum(6, 0.055f, 0.05f, 0.38f), new Vector3(s * 0.07f, 0f, 0f), Vector3.one, dark);
        f.body = ModelKit.Root("Body", f.root).transform;
        f.body.localPosition = new Vector3(0f, 0.38f, 0f);
        ModelKit.MeshPart(f.body, WorldMeshes.Frustum(8, 0.16f, 0.13f, 0.34f), Vector3.zero, new Vector3(1f, 1f, 0.75f), shirt);
        ModelKit.MeshPart(f.body, ball, new Vector3(0f, 0.47f, 0f), Vector3.one * 0.26f, skin);                        // head
        if (index % 3 == 1) {                                                                                          // a cap
            ModelKit.MeshPart(f.body, WorldMeshes.Dome(10, 3, 0.135f), new Vector3(0f, 0.5f, 0f), Vector3.one, CandidateKit.TealMat);
            ModelKit.MeshPart(f.body, WorldMeshes.Frustum(10, 0.1f, 0.1f, 0.02f), new Vector3(0f, 0.5f, -0.1f), new Vector3(1f, 1f, 0.8f), CandidateKit.TealMat);
        } else ModelKit.MeshPart(f.body, WorldMeshes.Dome(10, 3, 0.135f), new Vector3(0f, 0.49f, 0.01f), new Vector3(1f, 0.8f, 1f), hair);
        for (int s = -1; s <= 1; s += 2) {
            ModelKit.MeshPart(f.body, ball, new Vector3(s * 0.045f, 0.49f, -0.12f), Vector3.one * 0.03f, dark);       // eyes
        }
        ModelKit.Part(f.body, PrimitiveType.Cube, new Vector3(0f, 0.42f, -0.125f), new Vector3(0.07f, 0.02f, 0.01f), Mats.Lit(new Color(0.45f, 0.1f, 0.12f), 0.3f));   // a smile
        f.armL = Arm(f.body, -1, shirt, skin);
        f.armR = Arm(f.body, 1, shirt, skin);
        f.sign = index % 3 == 2;
        f.pennant = !f.sign && index % 4 == 3;
        if (f.sign) Placard(f.armR);
        else if (f.pennant) Pennant(f.armR);
        f.root.gameObject.SetActive(false);
        return f;
    }

    static Transform Arm(Transform body, int side, Material sleeve, Material skin) {
        Transform pivot = ModelKit.Root(side > 0 ? "Arm R" : "Arm L", body).transform;
        pivot.localPosition = new Vector3(side * 0.15f, 0.31f, 0f);
        ModelKit.MeshPart(pivot, WorldMeshes.Frustum(6, 0.04f, 0.035f, 0.26f), Vector3.zero, Vector3.one, sleeve, new Vector3(0f, 0f, 180f));
        ModelKit.MeshPart(pivot, WorldMeshes.Ball(0.5f), new Vector3(0f, -0.28f, 0f), Vector3.one * 0.07f, skin);   // hand
        return pivot;
    }

    // A white placard on a stick with his tick in a ring, held in the hand (the stick runs on along the arm).
    static void Placard(Transform arm) {
        Transform hold = ModelKit.Root("Placard", arm).transform;
        hold.localPosition = new Vector3(0f, -0.28f, 0f);
        hold.localRotation = Quaternion.Euler(0f, 0f, 180f);   // up along the raised arm
        ModelKit.Part(hold, PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(0.025f, 0.45f, 0.025f), Mats.Lit(new Color(0.55f, 0.4f, 0.25f), 0.3f));
        ModelKit.Part(hold, PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(0.36f, 0.26f, 0.02f), CandidateKit.PaperMat);
        ModelKit.MeshPart(hold, CandidateStamp.Mark, new Vector3(0f, 0.5f, -0.012f), Vector3.one * 0.1f, CandidateKit.TealMat);
    }

    // A little teal pennant on a stick.
    static void Pennant(Transform arm) {
        Transform hold = ModelKit.Root("Pennant", arm).transform;
        hold.localPosition = new Vector3(0f, -0.28f, 0f);
        hold.localRotation = Quaternion.Euler(0f, 0f, 180f);
        ModelKit.Part(hold, PrimitiveType.Cube, new Vector3(0f, 0.22f, 0f), new Vector3(0.02f, 0.5f, 0.02f), CandidateKit.GoldMat);
        Transform flag = ModelKit.Root("Flag", hold).transform;
        flag.localPosition = new Vector3(0.12f, 0.4f, 0f);
        flag.localScale = new Vector3(0.24f, 0.16f, 1f);
        ModelKit.Part(flag, PrimitiveType.Cube, Vector3.zero, new Vector3(1f, 1f, 0.05f), CandidateKit.TealMat);
        ModelKit.Part(flag, PrimitiveType.Cube, new Vector3(-0.12f, 0f, -0.03f), new Vector3(0.5f, 0.35f, 0.02f), CandidateKit.PaperMat);
    }
}

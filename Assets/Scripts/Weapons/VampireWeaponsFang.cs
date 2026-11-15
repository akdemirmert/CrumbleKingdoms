using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Vampire Fang: a curved ivory fang thrown flat and fast. It pierces like a knife (PiercingProjectile with budget
// def.pierce and damage def.damage) and stays stuck where it stops. If it bites an enemy commander it drains
// DrainShare of his maximum health as Magic damage, and the blood flies back to the vampire in a stream
// (VampireBloodStream) that heals him by what was really drained (nothing if a shield took the bite).
public class VampireFang : PiercingProjectile {
    const float DrainShare = 0.3f;
    const float RollSpeed = 420f;          // deg/s: the fang turns about its length as it flies

    Transform roll;
    ParticleSystem drips;
    Light glow;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        SFX.Play(SoundId.KnifeThrow, origin, 0.8f, 0.85f);
        VampireSounds.Play(VampireSounds.Hiss, origin, 0.6f);
        VFX.MagicBurst(origin, VampireKit.Crimson, 0.4f);
        WeaponFactory.Spawn<VampireFang>("Vampire Fang", owner, def, origin, velocity, f => {
            f.sweepRadius = 0.1f;
            f.slowPerCost = 0.12f;
            f.stopSpeed = 6f;
            f.embedDepth = 0.22f;
            f.commanderDamage = 0f;        // a bitten commander is drained instead (OnStopped)
        });
    }

    public static GameObject Model(Transform parent) { return Build(parent); }

    protected override void OnLaunch(Vector3 velocity) {
        base.OnLaunch(velocity);
        roll = ModelKit.Root("Roll", transform).transform;
        Build(roll);
        GameObject trail = VFX.Trail(transform, new Color(1f, 0.3f, 0.35f, 0.75f), 0.12f, 0.22f);
        trail.transform.localPosition = new Vector3(-0.3f, 0f, 0f);   // streams from the root of the tooth
        drips = VampireKit.Drips(transform, VampireKit.BloodRed, 22f, 0.06f);
        glow = VampireKit.Glow(transform, VampireKit.BloodGlow, 1.4f, 3f);
        Stopped += OnStopped;
    }

    void Update() {
        if (roll != null && !IsStuck && !finished) roll.Rotate(RollSpeed * Time.deltaTime, 0f, 0f, Space.Self);
    }

    void OnStopped(PiercingProjectile self, Vector3 point, Collider hit) {
        VampireKit.Release(drips, 1f);
        drips = null;
        if (glow != null) Destroy(glow.gameObject);
        Commander victim = hit != null ? hit.GetComponentInParent<Commander>() : null;
        if (victim != null && victim.IsAlive && !IsOwnTeam(victim)) Drain(victim, point);
    }

    // The bite: the victim loses DrainShare of his maximum health; what he really lost flies back to the vampire.
    void Drain(Commander victim, Vector3 point) {
        float before = victim.hp;
        victim.ApplyDamage(new DamageInfo(victim.maxHP * DrainShare, DamageType.Magic, point, owner));
        float drained = before - victim.hp;
        if (drained <= 0.5f) return;   // a shield took it
        VampireSounds.Play(VampireSounds.Slurp, point, 0.85f);
        VFX.Burst(point, 24, 3.5f, 0.09f, 0.8f, VampireKit.BloodRed, 1f, false, TexKit.SoftCircle);
        VFX.MagicBurst(point, VampireKit.BloodGlow, 0.6f);
        if (owner != null && owner.CommanderAlive) VampireBloodStream.Create(owner.commander, drained, point);
    }

    // ------------------------------------------------------------------ model
    // An ivory canine about 0.7 m long (+X = the point): a curved, tapering tooth, dipped in blood at the tip and
    // set in a dark red gum at the root.
    static GameObject Build(Transform parent) {
        GameObject root = ModelKit.Root("Vampire Fang", parent);
        Transform r = root.transform;
        ModelKit.MeshPart(r, VampireKit.Cached("fangTooth", () => Tooth(0f, 1f, 1f)), Vector3.zero, Vector3.one,
                          Mats.Lit(VampireKit.Ivory, 0.62f), default, "Tooth");
        ModelKit.MeshPart(r, VampireKit.Cached("fangBlood", () => Tooth(0.68f, 1f, 1.1f)), Vector3.zero, Vector3.one,
                          Mats.Emissive(new Color(0.55f, 0.02f, 0.06f), new Color(0.9f, 0.03f, 0.08f), 0.8f), default, "Blood");
        ModelKit.MeshPart(r, WorldMeshes.Ball(0.085f), new Vector3(-0.33f, 0.04f, 0f), new Vector3(0.75f, 1f, 1f),
                          Mats.Lit(new Color(0.42f, 0.05f, 0.08f), 0.5f), default, "Gum");
        return root;
    }

    // The part of the tooth from s0 to s1 of its length (0 = root, 1 = tip), its radius scaled by `fat`.
    static Mesh Tooth(float s0, float s1, float fat) {
        const int N = 12;
        Vector3[] path = new Vector3[N];
        float[] radii = new float[N];
        for (int i = 0; i < N; i++) {
            float s = Mathf.Lerp(s0, s1, i / (N - 1f));
            path[i] = new Vector3(-0.32f + 0.7f * s, 0.04f - 0.16f * s * s, 0f);   // curving down towards the point
            radii[i] = (0.075f * Mathf.Pow(1f - s, 0.85f) + 0.003f) * fat;
        }
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        VampireKit.AddTube(v, t, path, radii, 10);
        return VampireKit.ToMesh("Fang", v, t);
    }
}

// The drained blood flying back to the vampire: a glowing red ribbon along an arc whose head races ahead and
// whose tail follows, shedding drops. When the head reaches the vampire's mouth he heals by `amount`. It holds
// the turn (TurnActor) until it is gone, so the healing always lands in the turn of the bite.
public class VampireBloodStream : MonoBehaviour {
    const int Points = 24;
    const float Speed = 16f, MinTravel = 0.8f, MaxTravel = 1.8f;   // seconds for the head to arrive
    const float TailLag = 0.35f;            // the tail sets off this share of the travel time after the head
    const float Z = -1f;                     // just in front of the fortresses, so walls never hide the stream

    Commander drinker;
    float amount, travel, t;
    Vector3 from, control, lastMouth;
    bool healed;
    LineRenderer glow, core;
    ParticleSystem drops;
    readonly Vector3[] points = new Vector3[Points];

    public static void Create(Commander drinker, float amount, Vector3 from) {
        GameObject go = new GameObject("Blood Stream");
        go.AddComponent<TurnActor>();
        VampireBloodStream s = go.AddComponent<VampireBloodStream>();
        s.drinker = drinker;
        s.amount = amount;
        s.from = new Vector3(from.x, from.y, Z);
        Vector3 to = s.Mouth();
        float d = Vector3.Distance(s.from, to);
        s.travel = Mathf.Clamp(d / Speed, MinTravel, MaxTravel);
        s.control = (s.from + to) * 0.5f + Vector3.up * Mathf.Clamp(d * 0.3f, 2.5f, 12f);
        s.glow = s.Line("Glow", FxAssets.Additive(FxAssets.Beam, 2f), 0.42f, new Color(1f, 0.12f, 0.2f, 0.9f), 1);
        s.core = s.Line("Core", Mats.ParticleAlpha(FxAssets.Beam), 0.16f, new Color(0.4f, 0f, 0.04f, 0.95f), 2);
        s.drops = VampireKit.Drips(go.transform, VampireKit.BloodRed, 45f, 0.09f);
        s.Draw();
    }

    void Update() {
        t += Time.deltaTime;
        if (!healed && t >= travel) Arrive();
        if (t >= travel * (1f + TailLag) + 0.05f || t > MaxTravel * 3f) {
            if (!healed) Arrive();
            Destroy(gameObject);
            return;
        }
        Draw();
    }

    // The ribbon between the tail and the head along the arc, rippling as it flows.
    void Draw() {
        Vector3 to = Mouth();
        float head = Mathf.Clamp01(t / travel), tail = Mathf.Clamp01(t / travel - TailLag);
        for (int i = 0; i < Points; i++) {
            float k = Mathf.Lerp(tail, head, i / (Points - 1f));
            Vector3 p = VampireKit.Bezier(from, control, to, k);
            p.y += Mathf.Sin(k * 18f - t * 12f) * 0.12f * Mathf.Sin(Mathf.PI * k);
            points[i] = p;
        }
        glow.SetPositions(points);
        core.SetPositions(points);
        drops.transform.position = points[Points - 1];
        if (healed && drops.isEmitting) drops.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void Arrive() {
        healed = true;
        if (drinker == null || !drinker.IsAlive) return;
        Vector3 mouth = Mouth();
        drinker.Heal(amount);
        VFX.MagicBurst(mouth, VampireKit.BloodGlow, 0.8f);
        VFX.Sparks(mouth, VampireKit.BloodGlow, 12);
        VampireSounds.Play(VampireSounds.Gulp, mouth, 0.8f);
    }

    Vector3 Mouth() {
        if (drinker != null) lastMouth = drinker.transform.position + Vector3.up * 1.25f;
        return new Vector3(lastMouth.x, lastMouth.y, Z);
    }

    LineRenderer Line(string name, Material mat, float width, Color color, int order) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = Points;
        lr.sharedMaterial = mat;
        lr.widthMultiplier = width;
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0.75f));
        Gradient g = new Gradient();   // the tail end fades in
        g.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(color.a, 0.3f), new GradientAlphaKey(color.a, 1f) });
        lr.colorGradient = g;
        lr.numCapVertices = 4;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sortingOrder = order;
        return lr;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Melting Clocks: Dalí throws a soft pocket watch, wobbling and dripping gold and ticking as it flies. When it
// touches anything, falls in the sea, or the player clicks, time itself melts: the picture drains to sepia,
// rings of time ripple out and soft clocks float around, melting, their hands racing backwards (TimeMelt) - and
// then, last of all, the battle rewinds to the start of the previous turn (BattleManager.RewindTime): the enemy's
// last move is undone and Dalí plays again. With nothing to rewind the watch just drips away.
public class MeltingClock : Projectile {
    const float Radius = 0.42f;             // the watch in flight
    const float MeltTime = 1.5f;
    const float TickEvery = 0.45f;

    SoftWatch watch;
    Transform visual;
    GameObject trail, drips;
    bool melting, tock;
    float tickTimer, spin, meltAge;

    public override bool CanActivate => base.CanActivate && !melting;

    public static void Fire(Fortress owner, WeaponDef def, Vector3 origin, Vector3 velocity) {
        DaliSounds.Play(DaliSound.Tick, origin, 0.8f);
        VFX.MagicBurst(origin, DaliArt.Gold, 0.5f);
        WeaponFactory.Spawn<MeltingClock>("Melting Clock", owner, def, origin, velocity);
    }

    // Weapon icon: a soft watch drooping as if hung over a branch, hands at ten past ten.
    public static GameObject Model(Transform parent) {
        GameObject root = ModelKit.Root("Melting Clock", parent);
        SoftWatch.Create(root.transform, 0.42f, 0.7f, true);
        return root;
    }

    protected override void OnLaunch(Vector3 velocity) {
        visual = ModelKit.Root("Visual", transform).transform;
        watch = SoftWatch.Create(visual, Radius, 0.35f, true);
        watch.handSpeed = 360f;                                         // a minute a second
        gameObject.AddComponent<SphereCollider>().radius = 0.32f;
        AddBody(1.2f, velocity);
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;   // the model tumbles, not the body
        trail = VFX.Trail(transform, DaliArt.Gold * 1.4f, 0.25f, 0.3f);
        drips = VFX.Emitter(transform, DaliArt.Gold * 1.3f, 0.09f, 14f);
        DaliFx.Glow(transform, DaliArt.Gold, 1.6f, 3.5f);
    }

    void Update() {
        if (finished || melting) return;
        float t = Time.time;
        watch.melt = 0.32f + 0.12f * Mathf.Sin(t * 3.1f);               // wobbles like jelly
        spin += Time.deltaTime * 70f;
        visual.localRotation = Quaternion.Euler(15f * Mathf.Sin(t * 2.1f), 22f * Mathf.Sin(t * 1.6f), -travelDir * spin);
        tickTimer -= Time.deltaTime;
        if (tickTimer > 0f) return;
        tickTimer = TickEvery;
        DaliSounds.Play(tock ? DaliSound.Tock : DaliSound.Tick, transform.position, 0.45f);
        tock = !tock;
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (finished || melting || transform.position.y > World.WaterY) return;
        Vector3 p = new Vector3(transform.position.x, World.WaterY, 0f);
        VFX.Splash(p, 0.8f);
        SFX.Play(SoundId.Splash, p, 0.7f);
        BeginMelt(p + Vector3.up * 0.3f);
    }

    protected override void OnActivate() { BeginMelt(transform.position); }

    void OnCollisionEnter(Collision c) { BeginMelt(c.contactCount > 0 ? c.GetContact(0).point : transform.position); }

    // Out of the arena or flying too long: time melts all the same. The base class keeps calling this while the
    // watch is out of bounds, so during the melt it only ends the turn if the melt never finished (safety net).
    protected override void OnLost() {
        if (!melting) BeginMelt(transform.position);
        else if (age > meltAge + MeltTime + 4f) Finish(true);
    }

    void BeginMelt(Vector3 p) {
        if (melting || finished) return;
        melting = true;
        activated = true;                                                // no more clicks
        if (!impacted) RaiseImpact(p);
        DaliWeapons.Freeze(this);
        meltAge = age;
        maxLifetime = age + MeltTime + 5f;
        DaliFx.Release(trail);
        DaliFx.Release(drips);
        trail = drips = null;
        StartCoroutine(Melt(p));
    }

    IEnumerator Melt(Vector3 p) {
        TimeMelt fx = TimeMelt.Begin(p, MeltTime);
        DaliSounds.Play(DaliSound.TimeRewind, p, 1f);
        BattleCamera.Shake(0.2f);
        watch.handSpeed = -2200f;                                         // the hands race backwards
        Quaternion tilt = visual.localRotation;
        for (float t = 0f; t < MeltTime; t += Time.deltaTime) {
            float k = Mathf.Clamp01(t / MeltTime);
            watch.melt = Mathf.Lerp(0.35f, 1f, k);
            visual.localScale = Vector3.one * (1f + 0.7f * k);
            visual.localRotation = Quaternion.Slerp(tilt, Quaternion.identity, k * 3f);
            yield return null;
        }
        // Last of all, the rewind. It removes every projectile (this watch too) and rebuilds the fortresses.
        bool rewound = BattleManager.I != null && BattleManager.I.RewindTime(owner);
        if (fx != null) fx.End(rewound);
        if (!rewound) StandStill(p);
        Finish(true);
    }

    // Nothing to rewind (the first turn of the battle): the watch drips away with a sad tock.
    void StandStill(Vector3 p) {
        VFX.FloatingText(p + Vector3.up * 1.2f, "Time stands still", DaliArt.Gold, 0.8f);
        DaliSounds.Play(DaliSound.TimeStill, p, 0.9f);
        VFX.Burst(DaliFx.Front(p), 18, 2f, 0.12f, 1f, DaliArt.Gold, 1f, false, TexKit.SoftCircle);
        watch.handSpeed = 0f;
        visual.SetParent(null, true);
        DaliFader.Begin(visual.gameObject, 0f, 0.6f);
    }
}

// The look and sound of time melting, on an object of its own (the rewind removes every projectile, the watch
// that started it too): the picture drains to sepia (a temporary post-processing volume), rings of time ripple
// over the screen, and soft clocks float around the spot, melting, their hands racing backwards. After the rewind
// it all fades away.
public class TimeMelt : MonoBehaviour {
    const int Clocks = 6;
    const float FadeIn = 0.35f, FadeOut = 0.8f;
    static Sprite ripples, vignette;

    readonly List<SoftWatch> clocks = new List<SoftWatch>();
    readonly List<Vector3> drift = new List<Vector3>();
    Volume volume;
    VolumeProfile profile;
    RectTransform rings;
    Image ringImage, edgeImage;
    float duration, t, endedAt = -1f;

    public static TimeMelt Begin(Vector3 at, float duration) {
        TimeMelt m = new GameObject("[Time Melt]").AddComponent<TimeMelt>();
        m.duration = duration;
        m.BuildVolume();
        m.BuildOverlay();
        m.BuildClocks(at);
        m.Apply(0f);
        return m;
    }

    // The rewind happened (or not): flash and fade out.
    public void End(bool rewound) {
        if (endedAt >= 0f) return;
        endedAt = t;
        if (rewound) VFX.ScreenFlash(new Color(1f, 0.9f, 0.7f, 0.6f), 0.5f);
        foreach (SoftWatch w in clocks) if (w != null) w.handSpeed = rewound ? 0f : -200f;
    }

    void Update() {
        t += Time.deltaTime;
        if (endedAt < 0f && t > duration + 4f) End(false);            // the watch never reported back
        float k = endedAt < 0f ? Mathf.Clamp01(t / FadeIn) : 1f - Mathf.Clamp01((t - endedAt) / FadeOut);
        Apply(k);
        for (int i = 0; i < clocks.Count; i++) {
            SoftWatch w = clocks[i];
            if (w == null) continue;
            if (endedAt < 0f) w.melt = Mathf.Clamp01(t / duration);
            Transform c = w.transform.parent;
            c.position += drift[i] * Time.deltaTime;
            c.localScale = Vector3.one * Mathf.Max(0.001f, Mathf.SmoothStep(0f, 1f, k));
        }
        if (endedAt >= 0f && t - endedAt >= FadeOut) Destroy(gameObject);
    }

    // k = 0..1: how much time has melted (sepia, rings, the clocks' size).
    void Apply(float k) {
        if (volume != null) volume.weight = k;
        if (ringImage != null) {
            ringImage.color = new Color(0.85f, 0.62f, 0.32f, 0.22f * k);
            rings.localRotation = Quaternion.Euler(0f, 0f, -t * 45f);
            rings.localScale = Vector3.one * (0.95f + 0.1f * Mathf.Sin(t * 2.2f));     // the ripples breathe
        }
        if (edgeImage != null) edgeImage.color = new Color(0.3f, 0.17f, 0.05f, 0.5f * k);
    }

    void OnDestroy() {
        if (profile == null) return;
        foreach (VolumeComponent c in profile.components) if (c != null) Destroy(c);
        Destroy(profile);
    }

    // A global volume of our own, above the arena's: desaturated, warm and a little brighter, with a brown
    // vignette. Its weight fades the look in and out; the arena's own profile is never touched.
    void BuildVolume() {
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        ColorAdjustments grade = profile.Add<ColorAdjustments>();
        grade.saturation.Override(-75f);
        grade.colorFilter.Override(new Color(1f, 0.84f, 0.62f));
        grade.contrast.Override(12f);
        grade.postExposure.Override(0.15f);
        Vignette edge = profile.Add<Vignette>();
        edge.intensity.Override(0.45f);
        edge.smoothness.Override(0.5f);
        edge.color.Override(new Color(0.25f, 0.13f, 0.03f));
        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.weight = 0f;
        volume.sharedProfile = profile;
    }

    // Ripples of time turning over the whole screen and a warm vignette, on an overlay that never takes clicks.
    void BuildOverlay() {
        GameObject go = new GameObject("Overlay");
        go.transform.SetParent(transform, false);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 850;
        float size = Mathf.Max(Screen.width, Screen.height) * 1.6f;
        ringImage = OverlayImage(go.transform, "Ripples", RippleSprite);
        rings = (RectTransform)ringImage.transform;
        rings.sizeDelta = new Vector2(size, size);
        edgeImage = OverlayImage(go.transform, "Vignette", EdgeSprite);
        RectTransform edge = (RectTransform)edgeImage.transform;
        edge.anchorMin = Vector2.zero;
        edge.anchorMax = Vector2.one;
        edge.sizeDelta = Vector2.zero;
    }

    static Image OverlayImage(Transform parent, string name, Sprite sprite) {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    // Soft clocks floating in a ring around the spot (in front of the battle), drifting slowly down as they melt.
    void BuildClocks(Vector3 at) {
        Transform holder = new GameObject("Clocks").transform;
        holder.SetParent(transform, false);
        for (int i = 0; i < Clocks; i++) {
            float a = (i + Random.Range(-0.25f, 0.25f)) * Mathf.PI * 2f / Clocks;
            float d = Random.Range(3.5f, 6.5f);
            Transform c = new GameObject("Clock").transform;
            c.SetParent(holder, false);
            c.position = new Vector3(at.x + Mathf.Cos(a) * d * 1.3f, at.y + Mathf.Sin(a) * d * 0.75f, -3.5f);
            c.rotation = Quaternion.Euler(0f, Random.Range(-20f, 20f), Random.Range(-25f, 25f));
            c.localScale = Vector3.one * 0.001f;
            SoftWatch w = SoftWatch.Create(c, Random.Range(0.45f, 1f), 0f, true);
            w.handSpeed = -Random.Range(900f, 1800f);
            ModelKit.Shadows(c.gameObject, false, false);
            clocks.Add(w);
            drift.Add(new Vector3(Random.Range(-0.2f, 0.2f), -Random.Range(0.3f, 0.7f), 0f));
        }
    }

    static Sprite RippleSprite => ripples != null ? ripples : (ripples = TexKit.ToSprite(Radial("Time Ripples", r => {
        float ring = 0.5f + 0.5f * Mathf.Sin(r * 26f);
        return ring * ring * Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 3f);
    })));

    static Sprite EdgeSprite => vignette != null ? vignette : (vignette = TexKit.ToSprite(Radial("Time Vignette", r => Mathf.SmoothStep(0.45f, 1.05f, r))));

    // A white square texture whose alpha is a function of the distance from its centre (1 at the edge's middle).
    static Texture2D Radial(string name, System.Func<float, float> alpha) {
        const int Size = 128;
        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
        Color[] px = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++) {
                float dx = (x + 0.5f) / Size * 2f - 1f, dy = (y + 0.5f) / Size * 2f - 1f;
                px[y * Size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(Mathf.Sqrt(dx * dx + dy * dy))));
            }
        tex.SetPixels(px);
        tex.Apply(false);
        return tex;
    }
}

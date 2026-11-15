using System.Collections;
using UnityEngine;

// The Vampire's own sounds, synthesised the first time each one is needed (like SoundBank) and played through
// the shared SFX pool (SFX.PlayClip), so they pan, fade with distance and respect the volume settings.
static class VampireSounds {
    static readonly float[] BellRatios = { 1f, 2f, 2.76f, 4.07f, 5.4f }, BellAmps = { 1f, 0.4f, 0.25f, 0.12f, 0.06f };
    static readonly Wavetable Organ = new Wavetable(1f, 0.6f, 0.15f, 0.45f, 0.05f, 0.2f, 0f, 0.15f);

    static AudioClip screech, flutter, bite, hiss, slurp, gulp, whirl, clang, cloakFlap, cloakBlock, cloakTear, drape;

    public static AudioClip Screech => Get(ref screech, "Bat Screech", RenderScreech);
    public static AudioClip Flutter => Get(ref flutter, "Bat Wings", RenderFlutter);
    public static AudioClip Bite => Get(ref bite, "Bat Bite", RenderBite);
    public static AudioClip Hiss => Get(ref hiss, "Vampire Hiss", RenderHiss);
    public static AudioClip Slurp => Get(ref slurp, "Slurp", RenderSlurp);
    public static AudioClip Gulp => Get(ref gulp, "Gulp", RenderGulp);
    public static AudioClip Whirl => Get(ref whirl, "Fire Whirl", RenderWhirl);
    public static AudioClip Clang => Get(ref clang, "Candelabra Clang", RenderClang);
    public static AudioClip CloakFlap => Get(ref cloakFlap, "Cloak Flap", RenderCloakFlap);
    public static AudioClip CloakBlock => Get(ref cloakBlock, "Cloak Block", RenderCloakBlock);
    public static AudioClip CloakTear => Get(ref cloakTear, "Cloak Tear", RenderCloakTear);
    public static AudioClip Drape => Get(ref drape, "Cloak Drape", RenderDrape);

    public static void Play(AudioClip clip, Vector3? pos, float volume = 1f, float pitch = 1f) {
        SFX.PlayClip(clip, pos, volume, pitch);
    }

    // Builds the clips in the background, one per frame (called when a vampire fortress is built), so the first
    // screech in battle never waits for its synthesis. Clips that already exist are skipped.
    public static void WarmUp() { Runner.Run(WarmUpRoutine()); }

    static IEnumerator WarmUpRoutine() {
        System.Func<AudioClip>[] clips = {
            () => Screech, () => Flutter, () => Bite, () => Hiss, () => Slurp, () => Gulp,
            () => Whirl, () => Clang, () => CloakFlap, () => CloakBlock, () => CloakTear, () => Drape,
        };
        foreach (System.Func<AudioClip> clip in clips) {
            clip();
            yield return null;
        }
    }

    // A looping voice that follows `follow` (pan, distance, pause) until SFX.Stop or until the target is gone.
    public static AudioSource Loop(AudioClip clip, Transform follow, float volume) {
        if (clip == null) return null;
        GameObject go = new GameObject("[Loop] " + clip.name);
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        go.AddComponent<LoopVoice>().Init(follow, volume);
        source.Play();
        return source;
    }

    static AudioClip Get(ref AudioClip clip, string name, System.Func<float[]> render) {
        if (clip == null) {
            float[] data = render();
            clip = AudioClip.Create(name, data.Length, 1, Synth.Rate, false);
            clip.SetData(data, 0);
        }
        return clip;
    }

    // ================================================================== bats
    // Four quick falling chirps and a squeaky shriek.
    static float[] RenderScreech() {
        Synth s = new Synth(0.5f, 301);
        for (int i = 0; i < 4; i++) {
            float t0 = i * 0.07f + s.Range(0f, 0.012f), f = s.Range(3400f, 4600f);
            s.Tone(t0, 0.055f, new Sweep(f, f * 0.55f), 0.5f, Env.Hit(0.002f, 0.016f), Waveform.Triangle);
            s.Tone(t0, 0.05f, new Sweep(f * 2f, f * 1.1f), 0.12f, Env.Hit(0.002f, 0.01f));
        }
        s.Tone(0.27f, 0.2f, new Sweep(2300f, 3300f, 2000f, 0.35f), 0.4f, Env.Swell(0.25f), Waveform.Saw, 36f, 0.05f, Filter.Band(2900f, 2.5f));
        s.Noise(0f, 0.45f, 0.06f, Env.Swell(0.3f), Filter.High(5000f));
        return s.Reverb(0.2f).Done();
    }

    // Wing beats (looped): breathy noise pulsing at the beat rate, a soft body and the odd squeak.
    static float[] RenderFlutter() {
        Synth s = new Synth(1.2f, 302);
        s.Noise(0f, 1.2f, 0.8f, Env.Flat, Filter.Band(new Sweep(650f, 1100f, 650f, 0.5f), 1.1f));
        s.Noise(0f, 1.2f, 0.35f, Env.Flat, Filter.Low(380f));
        s.Tremolo(16f, 0.85f).Wobble(5f, 0.35f);
        for (int i = 0; i < 5; i++) {
            float t = s.Range(0.05f, 1.05f), f = s.Range(3800f, 5200f);
            s.Tone(t, 0.05f, new Sweep(f, f * 0.6f), 0.07f, Env.Hit(0.002f, 0.012f), Waveform.Triangle);
        }
        return s.DoneLoop(0.12f, 0.8f);
    }

    // A bite: a wet snap, a dull thud and a squeak.
    static float[] RenderBite() => new Synth(0.25f, 303)
        .Noise(0f, 0.04f, 0.8f, Env.Hit(0.0005f, 0.008f), Filter.Band(1700f, 1.2f))
        .Tone(0f, 0.16f, new Sweep(210f, 85f), 0.6f, Env.Hit(0.002f, 0.035f))
        .Tone(0.025f, 0.08f, new Sweep(4000f, 2700f), 0.16f, Env.Hit(0.002f, 0.02f), Waveform.Triangle)
        .Done();

    // ================================================================== fang
    // A vampire's hiss over a low growl.
    static float[] RenderHiss() => new Synth(0.6f, 304)
        .Noise(0f, 0.55f, 0.9f, Env.Swell(0.2f), Filter.High(new Sweep(2800f, 5200f)))
        .Noise(0f, 0.5f, 0.35f, Env.Swell(0.3f), Filter.Band(2400f, 2f))
        .Tone(0f, 0.5f, 105f, 0.25f, Env.Swell(0.3f), Waveform.Saw, 24f, 0.05f, Filter.Low(650f))
        .Done();

    // Slurp: sucking noise through a rising formant, bubbles, then a gulp.
    static float[] RenderSlurp() {
        Synth s = new Synth(0.85f, 305);
        s.Noise(0f, 0.62f, 0.75f, Env.Swell(0.45f), Filter.Band(new Sweep(320f, 1300f, 600f, 0.6f), 3f));
        s.Tone(0f, 0.58f, new Sweep(115f, 210f), 0.35f, Env.Swell(0.5f), Waveform.Saw, 9f, 0.06f, Filter.Low(new Sweep(450f, 1100f)));
        for (int i = 0; i < 10; i++) {
            float t = s.Range(0.04f, 0.6f), f = s.Range(240f, 680f);
            s.Tone(t, 0.05f, new Sweep(f, f * 1.9f), 0.28f, Env.Hit(0.002f, 0.012f));
        }
        s.Tone(0.64f, 0.2f, new Sweep(190f, 95f), 0.65f, Env.Hit(0.004f, 0.05f));
        s.Noise(0.64f, 0.12f, 0.3f, Env.Hit(0.003f, 0.03f), Filter.Band(500f, 2f));
        return s.Wobble(11f, 0.25f).Done();
    }

    // The stolen blood arrives: a deep gulp and a dark little D minor chord.
    static float[] RenderGulp() {
        Synth s = new Synth(0.9f, 306);
        s.Tone(0f, 0.22f, new Sweep(210f, 100f), 0.7f, Env.Hit(0.004f, 0.05f));
        s.Noise(0f, 0.14f, 0.3f, Env.Hit(0.003f, 0.03f), Filter.Band(480f, 2f));
        foreach (float f in new[] { 293.7f, 349.2f, 440f })
            s.Tone(0.05f, 0.8f, f, 0.12f, Env.Hit(0.02f, 0.25f), Waveform.Triangle, 5f, 0.006f);
        return s.Reverb(0.35f).Done();
    }

    // ================================================================== candelabra
    // A spinning fire (looped): a roar pulsing with every turn, and crackles.
    static float[] RenderWhirl() {
        Synth s = new Synth(0.9f, 307);
        s.Noise(0f, 0.9f, 0.9f, Env.Flat, Filter.Band(new Sweep(380f, 900f, 380f, 0.5f), 1.3f));
        s.Noise(0f, 0.9f, 0.4f, Env.Flat, Filter.Low(260f));
        s.Tremolo(11f, 0.6f);
        s.Crackle(0f, 0.9f, 18, 0.35f, 1800f, 5000f, 1f);
        return s.DoneLoop(0.1f, 0.8f);
    }

    // The candelabra hits: a ringing golden clang over a thud.
    static float[] RenderClang() => new Synth(1.4f, 308)
        .Noise(0f, 0.03f, 0.7f, Env.Hit(0.0004f, 0.006f), Filter.Band(3500f, 1f))
        .Partials(0f, 980f, 0.55f, BellRatios, BellAmps, new[] { 0.45f, 0.3f, 0.22f, 0.14f, 0.1f }, 0.003f)
        .Partials(0.01f, 1470f, 0.25f, BellRatios, BellAmps, new[] { 0.3f, 0.2f, 0.15f, 0.1f, 0.07f })
        .Tone(0f, 0.25f, new Sweep(140f, 80f), 0.45f, Env.Hit(0.002f, 0.05f))
        .Done();

    // ================================================================== cloak
    // The cloak leaves the shoulders: heavy cloth flapping three times and a rising whoosh.
    static float[] RenderCloakFlap() {
        Synth s = new Synth(0.8f, 309);
        for (int i = 0; i < 3; i++)
            s.Noise(i * 0.12f, 0.16f, 0.8f - i * 0.15f, Env.Hit(0.004f, 0.035f), Filter.Low(new Sweep(2600f, 500f)));
        s.Noise(0.05f, 0.7f, 0.5f, Env.Swell(0.6f), Filter.Band(new Sweep(300f, 1400f), 1.5f));
        return s.Done();
    }

    // A blow stopped by the cloak: a padded thump, a cloth snap and a dark, wavering ring.
    static float[] RenderCloakBlock() => new Synth(0.7f, 310)
        .Tone(0f, 0.3f, new Sweep(95f, 45f), 0.8f, Env.Hit(0.003f, 0.07f))
        .Noise(0f, 0.08f, 0.6f, Env.Hit(0.001f, 0.015f), Filter.Band(1500f, 1.2f))
        .Partials(0f, 220f, 0.3f, new[] { 1f, 1.19f, 1.5f, 2.38f }, new[] { 1f, 0.7f, 0.5f, 0.3f }, new[] { 0.2f, 0.16f, 0.12f, 0.08f }, 0.006f)
        .Reverb(0.3f).Done();

    // The cloak rips apart into bats: tearing cloth and screeches.
    static float[] RenderCloakTear() {
        Synth s = new Synth(1f, 311);
        s.Noise(0f, 0.45f, 0.8f, Env.Hit(0.01f, 0.14f), Filter.Band(new Sweep(1100f, 3200f), 1.8f));
        s.Crackle(0f, 0.45f, 34, 0.6f, 1200f, 5000f, 0.8f, 0.004f);
        for (int i = 0; i < 5; i++) {
            float t = 0.15f + i * 0.09f + s.Range(0f, 0.03f), f = s.Range(3300f, 4700f);
            s.Tone(t, 0.06f, new Sweep(f, f * 0.55f), 0.3f, Env.Hit(0.002f, 0.018f), Waveform.Triangle);
        }
        return s.Reverb(0.25f).Done();
    }

    // The cloak settles over the chamber: a short, dark organ chord (D minor) and a falling whoosh.
    static float[] RenderDrape() {
        Synth s = new Synth(1.6f, 312);
        foreach (float f in new[] { 73.42f, 146.83f, 174.61f, 220f, 293.66f })
            s.TableTone(0f, 1.3f, f, 0.22f, Env.Gate(0.03f, 0.6f), Organ, 5f, 0.003f);
        s.Noise(0f, 0.5f, 0.4f, Env.Swell(0.2f), Filter.Low(new Sweep(1800f, 300f)));
        return s.Reverb(0.45f, 0.86f).Done(0.85f);
    }
}

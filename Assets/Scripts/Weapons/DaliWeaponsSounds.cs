using UnityEngine;

public enum DaliSound {
    Flutter, FeatherPop, Regrow,                      // mustache birds
    Dig, Burrow, Pop, Slurp, Gulp,                    // pet anteater
    Tick, Tock, TimeRewind, TimeStill,                // melting clocks
    Splat, Brush, Shimmer, SunFlare,                  // painted sun
}

// Dalí's own sounds: small synthesis recipes (Audio/Synth.cs, like SoundBank's), each built the first time it is
// needed and kept for the rest of the game, played through the shared pool with SFX.PlayClip.
static class DaliSounds {
    static readonly AudioClip[] clips = new AudioClip[System.Enum.GetValues(typeof(DaliSound)).Length];

    public static void Play(DaliSound id, Vector3? pos, float volume = 1f, float pitch = 1f) {
        SFX.PlayClip(Get(id), pos, volume, pitch);
    }

    static AudioClip Get(DaliSound id) {
        int i = (int)id;
        if (clips[i] == null) {
            float[] data = Render(id);
            AudioClip clip = AudioClip.Create("Dali " + id, data.Length, 1, Synth.Rate, false);
            clip.SetData(data, 0);
            clips[i] = clip;
        }
        return clips[i];
    }

    static float[] Render(DaliSound id) {
        switch (id) {
            case DaliSound.Flutter: return Flutter();
            case DaliSound.FeatherPop: return FeatherPop();
            case DaliSound.Regrow: return Regrow();
            case DaliSound.Dig: return Dig();
            case DaliSound.Burrow: return Burrow();
            case DaliSound.Pop: return Pop();
            case DaliSound.Slurp: return Slurp();
            case DaliSound.Gulp: return Gulp();
            case DaliSound.Tick: return Tick(2600f, 301);
            case DaliSound.Tock: return Tick(1900f, 302);
            case DaliSound.TimeRewind: return TimeRewind();
            case DaliSound.TimeStill: return TimeStill();
            case DaliSound.Splat: return Splat();
            case DaliSound.Brush: return Brush();
            case DaliSound.Shimmer: return Shimmer();
            default: return SunFlare();
        }
    }

    // ================================================================== mustache birds
    // Wings beating (soft paper-like flaps), two little tweets and a whoosh: the mustache takes off.
    static float[] Flutter() {
        Synth s = new Synth(0.7f, 201);
        for (int i = 0; i < 9; i++)
            s.Noise(i * 0.065f + s.Range(0f, 0.012f), 0.05f, 0.5f * (1f - i / 12f), Env.Hit(0.004f, 0.012f), Filter.Band(s.Range(900f, 1600f), 1.4f));
        s.Tone(0.05f, 0.12f, new Sweep(2600f, 3900f, 3100f, 0.6f), 0.25f, Env.Hit(0.005f, 0.03f), Waveform.Sine, 30f, 0.03f);
        s.Tone(0.22f, 0.1f, new Sweep(2900f, 4200f, 3400f, 0.55f), 0.2f, Env.Hit(0.005f, 0.025f), Waveform.Sine, 30f, 0.03f);
        s.Noise(0f, 0.5f, 0.25f, Env.Swell(0.3f), Filter.Band(new Sweep(600f, 2200f), 1.2f));
        return s.Done();
    }

    // A soft poof, rustling feathers and a startled falling tweet.
    static float[] FeatherPop() => new Synth(0.6f, 202)
        .Noise(0f, 0.25f, 0.8f, Env.Hit(0.002f, 0.05f), Filter.Low(new Sweep(3000f, 600f)))
        .Crackle(0.02f, 0.5f, 18, 0.3f, 2500f, 6000f, 1.3f, 0.004f)
        .Tone(0.03f, 0.25f, new Sweep(3600f, 1800f), 0.18f, Env.Hit(0.004f, 0.06f), Waveform.Sine, 25f, 0.02f)
        .Done();

    // The mustache grows back: a quick rising "zwip", twinkles and a little bell.
    static float[] Regrow() {
        Synth s = new Synth(0.9f, 203);
        s.Tone(0f, 0.25f, new Sweep(500f, 1600f), 0.35f, Env.Swell(0.7f), Waveform.Triangle);
        s.Pings(0.15f, 0.5f, 9, 0.25f, 2500f, 6500f, 0.05f, 1.2f);
        s.Partials(0.2f, 1568f, 0.25f, SoundBank.BellRatios, SoundBank.BellAmps, new[] { 0.25f, 0.18f, 0.12f, 0.08f, 0.05f });
        return s.Reverb(0.3f).Done(0.85f);
    }

    // ================================================================== pet anteater
    // Earth thrown up: a dull thud, crumbling soil.
    static float[] Dig() => new Synth(0.6f, 204)
        .Noise(0f, 0.5f, 0.8f, Env.Hit(0.01f, 0.12f), Filter.Low(new Sweep(900f, 250f)))
        .Crackle(0f, 0.45f, 26, 0.5f, 600f, 2500f, 1.2f, 0.006f)
        .Tone(0f, 0.3f, new Sweep(110f, 60f), 0.4f, Env.Hit(0.004f, 0.07f))
        .Done();

    // Tunnelling: a low, crumbling rumble that comes and goes.
    static float[] Burrow() => new Synth(1.2f, 205)
        .Noise(0f, 1.2f, 0.9f, Env.Gate(0.15f, 0.3f), Filter.Low(new Sweep(260f, 180f)))
        .Crackle(0f, 1.15f, 30, 0.35f, 400f, 1600f, 1f, 0.008f)
        .Wobble(10f, 0.45f)
        .Done();

    // A cartoon cork pop and a rising whistle: out it comes.
    static float[] Pop() => new Synth(0.4f, 206)
        .Tone(0f, 0.08f, new Sweep(300f, 1100f), 0.6f, Env.Hit(0.001f, 0.02f))
        .Noise(0f, 0.05f, 0.5f, Env.Hit(0.0005f, 0.01f), Filter.Band(1800f, 1f))
        .Tone(0.04f, 0.3f, new Sweep(600f, 1400f), 0.2f, Env.Hit(0.01f, 0.08f), Waveform.Triangle, 12f, 0.04f)
        .Done();

    // The tongue darts out and slurps: a wet, rising-and-falling hiss and little bubbles.
    static float[] Slurp() {
        Synth s = new Synth(0.55f, 207);
        s.Noise(0f, 0.4f, 0.9f, Env.Swell(0.6f), Filter.Band(new Sweep(500f, 2600f, 900f, 0.55f), 4f));
        for (int i = 0; i < 6; i++) {
            float t = s.Range(0.05f, 0.38f), f = s.Range(400f, 900f);
            s.Tone(t, 0.06f, new Sweep(f, f * 1.8f), 0.3f, Env.Hit(0.002f, 0.015f));
        }
        return s.Done();
    }

    // Swallowed whole: a deep, satisfied gulp.
    static float[] Gulp() => new Synth(0.45f, 208)
        .Tone(0f, 0.3f, new Sweep(240f, 80f), 0.7f, Env.Hit(0.006f, 0.07f), Waveform.Triangle, 0f, 0f, Filter.Low(900f))
        .Noise(0f, 0.12f, 0.35f, Env.Hit(0.004f, 0.03f), Filter.Band(450f, 2f))
        .Tone(0.18f, 0.2f, new Sweep(160f, 120f), 0.3f, Env.Hit(0.01f, 0.05f))
        .Done();

    // ================================================================== melting clocks
    // A watch tick: a tiny click and a short metallic ring.
    static float[] Tick(float freq, int seed) => new Synth(0.1f, seed)
        .Noise(0f, 0.02f, 0.8f, Env.Hit(0.0003f, 0.003f), Filter.Band(freq, 3f))
        .Partials(0f, freq * 0.5f, 0.4f, new[] { 1f, 2.76f }, new[] { 1f, 0.4f }, new[] { 0.012f, 0.008f })
        .Done();

    // Time melting: ticks played backwards (swelling, cut off) faster and faster, a tape-rewind warble rising, a
    // reversed cymbal and gong swelling into a sudden stop.
    static float[] TimeRewind() {
        Synth s = new Synth(1.9f, 209);
        float t = 0f, gap = 0.22f;
        for (int i = 0; t < 1.55f; i++) {
            s.Noise(t, 0.05f, 0.6f, Env.Swell(0.95f), Filter.Band(i % 2 == 0 ? 2600f : 1900f, 3f));
            t += gap;
            gap = Mathf.Max(0.035f, gap * 0.82f);
        }
        s.Tone(0.1f, 1.5f, new Sweep(180f, 1400f), 0.22f, Env.Swell(0.85f), Waveform.Saw, 7f, 0.05f, Filter.Low(new Sweep(800f, 4000f)));
        s.Noise(0.2f, 1.4f, 0.5f, Env.Swell(0.97f), Filter.High(new Sweep(2000f, 6000f)));
        foreach (float f in new[] { 220f, 330f, 554f })
            s.Tone(0.3f, 1.3f, f, 0.3f * 220f / f, Env.Swell(0.96f), Waveform.Sine, 3f, 0.01f);
        return s.Reverb(0.25f).Done(0.9f);
    }

    // Nothing to rewind: a slow, sinking "tock... tock" and a deflating warble.
    static float[] TimeStill() {
        Synth s = new Synth(1.1f, 210);
        s.Partials(0f, 950f, 0.5f, new[] { 1f, 2.76f }, new[] { 1f, 0.4f }, new[] { 0.05f, 0.03f });
        s.Partials(0.35f, 700f, 0.45f, new[] { 1f, 2.76f }, new[] { 1f, 0.4f }, new[] { 0.06f, 0.035f });
        s.Tone(0.1f, 0.9f, new Sweep(520f, 180f), 0.2f, Env.Hit(0.02f, 0.3f), Waveform.Triangle, 6f, 0.03f);
        return s.Reverb(0.25f).Done(0.8f);
    }

    // ================================================================== painted sun
    // A wet splat: a fat burst, a low squelch and droplets.
    static float[] Splat() => new Synth(0.5f, 211)
        .Noise(0f, 0.12f, 1f, Env.Hit(0.001f, 0.03f), Filter.Low(new Sweep(2500f, 500f)))
        .Tone(0f, 0.15f, new Sweep(180f, 70f), 0.6f, Env.Hit(0.002f, 0.04f))
        .Crackle(0.01f, 0.35f, 14, 0.35f, 800f, 3000f, 1.5f, 0.006f)
        .Done();

    // Brush strokes: four quick swishes with the scratch of bristles.
    static float[] Brush() {
        Synth s = new Synth(1.3f, 212);
        for (int i = 0; i < 4; i++) {
            float t0 = i * 0.28f;
            s.Noise(t0, 0.3f, 0.6f, Env.Swell(0.4f), Filter.Band(new Sweep(1500f, 4200f, 2200f, 0.5f), 1.6f));
            s.Crackle(t0, 0.25f, 8, 0.15f, 3000f, 7000f, 1f, 0.003f);
        }
        return s.Done();
    }

    // The painting is finished: a rising arpeggio of bells and a sparkle.
    static float[] Shimmer() {
        Synth s = new Synth(1.3f, 213);
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
        for (int i = 0; i < notes.Length; i++)
            s.Partials(i * 0.07f, notes[i], 0.3f, SoundBank.BellRatios, SoundBank.BellAmps, new[] { 0.4f, 0.28f, 0.2f, 0.12f, 0.08f });
        s.Pings(0.2f, 0.7f, 8, 0.12f, 3000f, 7000f, 0.06f, 1.2f);
        return s.Reverb(0.35f).Done(0.85f);
    }

    // The sun flares: a warm whoom opening up, a bright choir chord and crackling heat.
    static float[] SunFlare() {
        Synth s = new Synth(1.5f, 214);
        s.Noise(0f, 1.2f, 0.8f, Env.Hit(0.08f, 0.35f), Filter.Low(new Sweep(300f, 2400f, 800f, 0.3f)));
        foreach (float f in new[] { 261.63f, 329.63f, 392f, 523.25f })
            s.TableTone(0.02f, 1.3f, f, 0.18f, Env.Hit(0.05f, 0.4f), Wavetables.Choir, 5f, 0.006f);
        s.Crackle(0.1f, 1f, 14, 0.25f, 1800f, 5000f, 1.2f);
        return s.Reverb(0.35f).Done(0.85f);
    }
}

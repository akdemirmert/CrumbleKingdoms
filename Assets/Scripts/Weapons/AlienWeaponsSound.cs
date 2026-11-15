using UnityEngine;

// The Alien's own sounds, synthesised like the rest of the game's (Synth; see SoundBank for the house recipes)
// the first time each is needed, then cached. One-shots play through the shared pool (SFX.PlayClip); loops
// follow a transform like SFX.PlayLoop does and stop with SFX.Stop.
static class AlienSounds {
    static AudioClip warble, hum, beamOn, beamHum, abduct, zoom, pew, sizzle, moo, warp, warpOut;

    public static AudioClip Warble => warble != null ? warble : warble = Make("Alien warble", WarbleData());
    public static AudioClip Hum => hum != null ? hum : hum = Make("Saucer hum", HumData());
    public static AudioClip BeamOn => beamOn != null ? beamOn : beamOn = Make("Beam on", BeamOnData());
    public static AudioClip BeamHum => beamHum != null ? beamHum : beamHum = Make("Beam hum", BeamHumData());
    public static AudioClip Abduct => abduct != null ? abduct : abduct = Make("Abduct", AbductData());
    public static AudioClip Zoom => zoom != null ? zoom : zoom = Make("Saucer zoom", ZoomData());
    public static AudioClip Pew => pew != null ? pew : pew = Make("Pew", PewData());
    public static AudioClip Sizzle => sizzle != null ? sizzle : sizzle = Make("Sizzle", SizzleData());
    public static AudioClip Moo => moo != null ? moo : moo = Make("Moo", MooData());
    public static AudioClip Warp => warp != null ? warp : warp = Make("Space warp", WarpData());
    public static AudioClip WarpOut => warpOut != null ? warpOut : warpOut = Make("Warp out", WarpOutData());

    public static void Play(AudioClip clip, Vector3? pos, float volume = 1f, float pitch = 1f) { SFX.PlayClip(clip, pos, volume, pitch); }

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

    static AudioClip Make(string name, float[] data) {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Synth.Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ================================================================== the saucer
    // The theremin "woo-ooo" of a flying saucer: a sine sliding up and back with a deep vibrato, a fifth above.
    static float[] WarbleData() => new Synth(0.95f, 201)
        .Tone(0f, 0.9f, new Sweep(520f, 980f, 700f, 0.35f), 0.5f, Env.Gate(0.03f, 0.25f), Waveform.Sine, 7.5f, 0.045f)
        .Tone(0f, 0.9f, new Sweep(780f, 1470f, 1050f, 0.35f), 0.18f, Env.Gate(0.05f, 0.25f), Waveform.Triangle, 7.5f, 0.045f)
        .Noise(0f, 0.6f, 0.15f, Env.Swell(0.3f), Filter.Band(new Sweep(1500f, 4000f), 2f))
        .Reverb(0.3f).Done(0.85f);

    // Engine hum (looped): a soft low buzz, its octave and a thin wavering whine, pulsing gently.
    static float[] HumData() => new Synth(1.8f, 202)
        .Tone(0f, 1.8f, 98f, 0.5f, Env.Flat, Waveform.Saw, 0.6f, 0.01f, Filter.Low(420f, 1.2f))
        .Tone(0f, 1.8f, 196.5f, 0.3f, Env.Flat, Waveform.Sine, 5f, 0.004f)
        .Tone(0f, 1.8f, 1180f, 0.05f, Env.Flat, Waveform.Sine, 6.25f, 0.03f)
        .Tremolo(10f, 0.25f)
        .DoneLoop(0.2f, 0.8f);

    // The tractor beam switching on: a filtered "vwoooom" rising an octave and a half, with a shimmer on top.
    static float[] BeamOnData() => new Synth(1.1f, 203)
        .Tone(0f, 1f, new Sweep(110f, 330f), 0.6f, Env.Gate(0.02f, 0.35f), Waveform.Saw, 6f, 0.01f, Filter.Low(new Sweep(300f, 2400f), 1.5f))
        .Tone(0f, 1f, new Sweep(220f, 660f), 0.25f, Env.Gate(0.05f, 0.35f), Waveform.Sine)
        .Noise(0f, 0.9f, 0.25f, Env.Swell(0.6f), Filter.Band(new Sweep(600f, 3000f), 1.5f))
        .Pings(0.3f, 0.6f, 8, 0.12f, 2500f, 5500f, 0.05f, 1f)
        .Reverb(0.3f).Done(0.85f);

    // The beam at work (looped): a buzzing open fifth throbbing eight times a second over airy hiss.
    static float[] BeamHumData() => new Synth(1.5f, 204)
        .Tone(0f, 1.5f, 165f, 0.45f, Env.Flat, Waveform.Saw, 0.8f, 0.01f, Filter.Band(700f, 1.5f))
        .Tone(0f, 1.5f, 247.5f, 0.25f, Env.Flat, Waveform.Triangle, 0.5f, 0.008f)
        .Tone(0f, 1.5f, 330f, 0.2f, Env.Flat, Waveform.Sine, 4f, 0.01f)
        .Noise(0f, 1.5f, 0.2f, Env.Flat, Filter.Band(2200f, 2f))
        .Tremolo(8f, 0.45f)
        .DoneLoop(0.25f, 0.8f);

    // "Bloop": something taken up into the saucer.
    static float[] AbductData() => new Synth(0.45f, 205)
        .Tone(0f, 0.3f, new Sweep(260f, 1400f), 0.6f, Env.Hit(0.005f, 0.09f), Waveform.Sine)
        .Tone(0f, 0.3f, new Sweep(520f, 2800f), 0.2f, Env.Hit(0.005f, 0.07f), Waveform.Triangle)
        .Pings(0.12f, 0.25f, 5, 0.15f, 3000f, 6000f, 0.04f, 1f)
        .Done(0.85f);

    // The saucer zips off: a sine shooting up with a fast wobble and a rising whoosh.
    static float[] ZoomData() => new Synth(0.8f, 206)
        .Tone(0f, 0.7f, new Sweep(600f, 2600f), 0.4f, Env.Swell(0.25f), Waveform.Sine, 12f, 0.02f)
        .Noise(0f, 0.7f, 0.5f, Env.Swell(0.3f), Filter.Band(new Sweep(500f, 3500f), 1.8f))
        .Done(0.85f);

    // ================================================================== the ray gun
    // "Pew": a bright square-and-sine zap diving two octaves, a click at the front and a faint echo.
    static float[] PewData() => new Synth(0.38f, 207)
        .Tone(0f, 0.22f, new Sweep(2600f, 240f), 0.5f, Env.Hit(0.002f, 0.07f), Waveform.Square, 0f, 0f, Filter.Low(5000f))
        .Tone(0f, 0.25f, new Sweep(1900f, 180f), 0.45f, Env.Hit(0.002f, 0.08f), Waveform.Sine)
        .Noise(0f, 0.04f, 0.3f, Env.Hit(0.0005f, 0.008f), Filter.High(3000f))
        .Tone(0.1f, 0.2f, new Sweep(2000f, 300f), 0.12f, Env.Hit(0.002f, 0.05f), Waveform.Sine)
        .Done(0.85f);

    // A hot sizzle where the bolt burns through: crackling hiss and a short falling ping.
    static float[] SizzleData() => new Synth(0.35f, 208)
        .Noise(0f, 0.3f, 0.6f, Env.Hit(0.003f, 0.08f), Filter.High(new Sweep(5000f, 2500f)))
        .Crackle(0f, 0.25f, 14, 0.5f, 2500f, 7000f, 1.2f, 0.003f)
        .Tone(0f, 0.12f, new Sweep(1600f, 900f), 0.2f, Env.Hit(0.002f, 0.03f), Waveform.Sine)
        .Done(0.8f);

    // ================================================================== the cow
    // "Mmmooooo": a buzzy voice whose pitch rises and sags, through two vowel formants that open from a nasal
    // "mm" to a wide "oo", a growl (fast tremolo) and a little breath.
    static float[] MooData() {
        Synth s = new Synth(1.35f, 209);
        Sweep pitch = new Sweep(112f, 152f, 96f, 0.35f);
        s.Tone(0f, 1.25f, pitch, 0.7f, Env.Gate(0.12f, 0.35f), Waveform.Saw, 5f, 0.012f, Filter.Band(new Sweep(380f, 780f, 520f, 0.4f), 3f));
        s.Tone(0f, 1.25f, pitch, 0.35f, Env.Gate(0.18f, 0.35f), Waveform.Saw, 5f, 0.012f, Filter.Band(new Sweep(900f, 1250f, 1000f, 0.4f), 4f));
        s.Tone(0f, 1.25f, pitch, 0.4f, Env.Gate(0.08f, 0.3f), Waveform.Sine, 5f, 0.012f);
        s.Noise(0f, 1.2f, 0.06f, Env.Gate(0.1f, 0.4f), Filter.Band(1200f, 1f));
        return s.Tremolo(30f, 0.18f).Done(0.85f);
    }

    // ================================================================== the space warp
    // Space folding: a reverse swell rushes in, a deep drop, then a shimmering chord rising out of it.
    static float[] WarpData() {
        Synth s = new Synth(2.4f, 210);
        s.Noise(0f, 0.7f, 0.6f, Env.Swell(0.95f), Filter.Band(new Sweep(300f, 4000f), 1.2f));
        s.Tone(0.65f, 1.6f, new Sweep(160f, 40f), 0.9f, Env.Hit(0.01f, 0.5f), Waveform.Sine);
        s.Noise(0.65f, 1.5f, 0.5f, Env.Hit(0.005f, 0.35f), Filter.Low(new Sweep(1800f, 150f)));
        foreach (float f in new[] { 523.25f, 659.25f, 783.99f, 987.77f })    // C E G B
            s.Tone(0.7f, 1.6f, new Sweep(f * 0.5f, f), 0.12f, Env.Gate(0.4f, 0.8f), Waveform.Triangle, 6f, 0.01f);
        s.Pings(0.7f, 1.4f, 14, 0.1f, 2500f, 7000f, 0.08f, 1.2f);
        return s.Reverb(0.45f, 0.88f, 0.3f).Done(0.9f);
    }

    // Back to the ground: the chord sinks away under a falling whoosh and lands with a soft thump.
    static float[] WarpOutData() {
        Synth s = new Synth(1.4f, 211);
        foreach (float f in new[] { 523.25f, 659.25f, 783.99f, 987.77f })
            s.Tone(0f, 1f, new Sweep(f, f * 0.5f), 0.12f, Env.Gate(0.05f, 0.5f), Waveform.Triangle, 6f, 0.01f);
        s.Noise(0f, 0.9f, 0.4f, Env.Swell(0.2f), Filter.Band(new Sweep(3500f, 400f), 1.4f));
        s.Tone(0.8f, 0.5f, new Sweep(90f, 45f), 0.6f, Env.Hit(0.005f, 0.12f), Waveform.Sine);
        return s.Reverb(0.35f).Done(0.85f);
    }
}

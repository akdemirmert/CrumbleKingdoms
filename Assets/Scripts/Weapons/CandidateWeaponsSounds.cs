using UnityEngine;

// The Candidate's own sounds, synthesised like the rest of the game's (Synth; see SoundBank and GamblerSounds for the house
// recipes) the first time each is needed, then kept. They play through the shared pool (SFX.PlayClip).
public static class CandidateSounds {
    // The modes of a thin glass shell (the shield breaking).
    static readonly float[] GlassRatios = { 1f, 1.73f, 2.48f, 3.11f }, GlassAmps = { 1f, 0.6f, 0.4f, 0.25f };

    static AudioClip cheer, stampThump, stampWhoosh, shout, echo, shieldUp, shieldPop, busHorn, busEngine, penScribble, decree, silence;

    public static AudioClip Cheer => cheer != null ? cheer : cheer = Make("Candidate cheer", CheerData());                  // a crowd cheering and clapping: a new vote tier
    public static AudioClip StampThump => stampThump != null ? stampThump : stampThump = Make("Candidate stamp", StampThumpData()); // a giant rubber stamp slams down: a deep thump with a wet smack
    public static AudioClip StampWhoosh => stampWhoosh != null ? stampWhoosh : stampWhoosh = Make("Candidate stamp whoosh", StampWhooshData()); // the giant stamp falling: a rising whoosh
    public static AudioClip Shout => shout != null ? shout : shout = Make("Candidate shout", ShoutData());                   // a megaphone shout: a buzzy, distorted vowel
    public static AudioClip Echo => echo != null ? echo : echo = Make("Candidate echo", EchoData());                      // the shout's echo coming back: the same, thinner, with a long tail
    public static AudioClip ShieldUp => shieldUp != null ? shieldUp : shieldUp = Make("Candidate shield up", ShieldUpData());      // a shield of sound forms: a shimmering rising hum
    public static AudioClip ShieldPop => shieldPop != null ? shieldPop : shieldPop = Make("Candidate shield pop", ShieldPopData());  // the shield takes a hit and breaks: a glassy pop
    public static AudioClip BusHorn => busHorn != null ? busHorn : busHorn = Make("Candidate bus horn", BusHornData());          // a bus horn: two blasts of a low chord
    public static AudioClip BusEngine => busEngine != null ? busEngine : busEngine = Make("Candidate bus engine", BusEngineData());  // a diesel engine revving as the bus is thrown
    public static AudioClip PenScribble => penScribble != null ? penScribble : penScribble = Make("Candidate pen", PenScribbleData()); // a big pen scratching a signature: fast scratchy strokes
    public static AudioClip Decree => decree != null ? decree : decree = Make("Candidate decree", DecreeData());             // the decree is sealed: a ceremonial brass flourish
    public static AudioClip Silence => silence != null ? silence : silence = Make("Candidate silence", SilenceData());          // an enemy is silenced: a muffled "shh" and a low bell

    public static void Play(AudioClip clip, Vector3? pos, float volume, float pitch = 1f) { SFX.PlayClip(clip, pos, volume, pitch); }

    // Builds all the clips at once (a Candidate is in this battle), so no throw waits for them.
    public static void WarmUp() {
        _ = Cheer; _ = StampThump; _ = StampWhoosh; _ = Shout; _ = Echo; _ = ShieldUp; _ = ShieldPop; _ = BusHorn; _ = BusEngine;
        _ = PenScribble; _ = Decree; _ = Silence;
    }

    static AudioClip Make(string name, float[] data) {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Synth.Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ================================================================== the crowd
    // A crowd cheers: "yeah!"s at four pitches and a whistle over a roar that surges and fades, and a round of applause.
    static float[] CheerData() => new Synth(1.4f, 901)
        .Noise(0f, 1.3f, 0.4f, Env.Swell(0.2f), Filter.Band(new Sweep(600f, 850f, 700f, 0.25f), 1.4f))
        .Noise(0f, 1.3f, 0.2f, Env.Swell(0.22f), Filter.Band(new Sweep(1300f, 1700f, 1450f, 0.25f), 2f))
        .Tone(0f, 1.2f, new Sweep(205f, 265f, 230f, 0.22f), 0.5f, Env.Swell(0.2f), Waveform.Saw, 4.7f, 0.02f,
              Filter.Band(new Sweep(420f, 680f, 640f, 0.2f), 2.5f))
        .Tone(0.03f, 1.15f, new Sweep(265f, 345f, 300f, 0.25f), 0.5f, Env.Swell(0.22f), Waveform.Saw, 5.6f, 0.025f,
              Filter.Band(new Sweep(500f, 880f, 840f, 0.22f), 2.5f))
        .Tone(0.06f, 1.05f, new Sweep(330f, 440f, 385f, 0.28f), 0.45f, Env.Swell(0.25f), Waveform.Saw, 6.3f, 0.03f,
              Filter.Band(new Sweep(650f, 1150f, 1100f, 0.25f), 2.5f))
        .Tone(0.1f, 0.95f, new Sweep(410f, 560f, 480f, 0.3f), 0.35f, Env.Swell(0.28f), Waveform.Saw, 7.1f, 0.03f,
              Filter.Band(new Sweep(800f, 1500f, 1450f, 0.28f), 2.5f))
        .Tone(0.18f, 0.55f, new Sweep(1900f, 2800f, 2400f, 0.4f), 0.15f, Env.Swell(0.45f), Waveform.Sine, 9f, 0.01f)
        .Wobble(10f, 0.3f)
        .Crackle(0.03f, 1.25f, 110, 1.8f, 1200f, 3600f, 1f, 0.006f)
        .Reverb(0.15f)
        .Done(0.85f);

    // ================================================================== the ballot stamp
    // A giant rubber stamp slams down: a deep thump, the wet smack of the inked rubber, a squelch, ink spatter and a rumble.
    static float[] StampThumpData() => new Synth(0.85f, 902)
        .Tone(0f, 0.6f, new Sweep(95f, 36f), 1f, Env.Hit(0.002f, 0.13f))
        .Noise(0f, 0.3f, 0.8f, Env.Hit(0.001f, 0.045f), Filter.Low(new Sweep(900f, 200f)))
        .Tone(0f, 0.18f, new Sweep(260f, 150f), 0.45f, Env.Hit(0.002f, 0.035f), Waveform.Triangle)
        .Noise(0.004f, 0.07f, 0.8f, Env.Hit(0.0005f, 0.016f), Filter.Band(new Sweep(3200f, 900f), 2.2f))
        .Noise(0.05f, 0.16f, 0.2f, Env.Swell(0.4f), Filter.Band(new Sweep(600f, 1900f), 3f))
        .Crackle(0.01f, 0.22f, 10, 0.3f, 1500f, 4500f, 1.6f, 0.002f)
        .Noise(0.01f, 0.75f, 0.4f, Env.Hit(0.02f, 0.18f), Filter.Low(new Sweep(320f, 80f)))
        .Saturate(1.7f)
        .Done(0.95f);

    // The giant stamp falls: a whoosh rising in pitch and loudness over a low drone, its broad face fluttering the air.
    static float[] StampWhooshData() => new Synth(0.8f, 903)
        .Noise(0f, 0.78f, 0.9f, Env.Swell(0.85f), Filter.Band(new Sweep(260f, 1500f), 1.3f))
        .Noise(0f, 0.78f, 0.3f, Env.Swell(0.9f), Filter.High(new Sweep(2000f, 5500f)))
        .Tone(0f, 0.78f, new Sweep(50f, 105f), 0.35f, Env.Swell(0.85f), Waveform.Triangle)
        .Tremolo(12f, 0.25f)
        .Done(0.85f);

    // ================================================================== the rally shout
    // A megaphone shout, "heyyy": a breathy "h", then a buzzy vowel gliding from "e" to "y" through the horn's nasal honk, clipped hard.
    static float[] ShoutData() => new Synth(0.8f, 904)
        .Noise(0f, 0.08f, 0.12f, Env.Swell(0.7f), Filter.Band(1500f, 1f))
        .Tone(0.05f, 0.7f, new Sweep(205f, 255f, 225f, 0.25f), 0.9f, Env.Gate(0.025f, 0.22f), Waveform.Saw, 5.5f, 0.012f,
              Filter.Band(new Sweep(760f, 700f, 400f, 0.35f), 4f))
        .Tone(0.05f, 0.7f, new Sweep(205f, 255f, 225f, 0.25f), 1f, Env.Gate(0.025f, 0.22f), Waveform.Saw, 5.5f, 0.012f,
              Filter.Band(new Sweep(1800f, 1900f, 2300f, 0.35f), 5f))
        .Tone(0.05f, 0.7f, new Sweep(205f, 255f, 225f, 0.25f), 0.5f, Env.Gate(0.025f, 0.22f), Waveform.Saw, 5.5f, 0.012f,
              Filter.Band(2800f, 6f))
        .Tone(0.05f, 0.7f, new Sweep(205f, 255f, 225f, 0.25f), 0.5f, Env.Gate(0.025f, 0.22f), Waveform.Square, 5.5f, 0.012f,
              Filter.Band(1250f, 3f))
        .Noise(0.05f, 0.7f, 0.06f, Env.Gate(0.025f, 0.22f), Filter.Band(2600f, 1.5f))
        .Saturate(2f)
        .Done(0.85f);

    // The shout comes back: three thinner "hey"s, each fainter than the last, washing into a long, dark tail.
    static float[] EchoData() => new Synth(1.6f, 905)
        .Noise(0f, 0.05f, 0.12f, Env.Swell(0.7f), Filter.Band(2000f, 1.5f))
        .Tone(0.03f, 0.3f, new Sweep(215f, 250f, 225f, 0.3f), 0.8f, Env.Gate(0.02f, 0.12f), Waveform.Saw, 5.5f, 0.012f,
              Filter.Band(new Sweep(1800f, 2300f), 4f))
        .Tone(0.03f, 0.3f, new Sweep(215f, 250f, 225f, 0.3f), 0.4f, Env.Gate(0.02f, 0.12f), Waveform.Square, 5.5f, 0.012f, Filter.Band(1250f, 3f))
        .Noise(0.3f, 0.05f, 0.08f, Env.Swell(0.7f), Filter.Band(2000f, 1.5f))
        .Tone(0.33f, 0.3f, new Sweep(215f, 250f, 225f, 0.3f), 0.5f, Env.Gate(0.02f, 0.12f), Waveform.Saw, 5.5f, 0.012f,
              Filter.Band(new Sweep(1800f, 2300f), 4f))
        .Tone(0.33f, 0.3f, new Sweep(215f, 250f, 225f, 0.3f), 0.25f, Env.Gate(0.02f, 0.12f), Waveform.Square, 5.5f, 0.012f, Filter.Band(1250f, 3f))
        .Noise(0.6f, 0.05f, 0.05f, Env.Swell(0.7f), Filter.Band(2000f, 1.5f))
        .Tone(0.63f, 0.3f, new Sweep(215f, 250f, 225f, 0.3f), 0.3f, Env.Gate(0.02f, 0.12f), Waveform.Saw, 5.5f, 0.012f,
              Filter.Band(new Sweep(1800f, 2300f), 4f))
        .Tone(0.63f, 0.3f, new Sweep(215f, 250f, 225f, 0.3f), 0.15f, Env.Gate(0.02f, 0.12f), Waveform.Square, 5.5f, 0.012f, Filter.Band(1250f, 3f))
        .Reverb(0.35f, 0.86f, 0.55f)
        .Done(0.8f);

    // A shield of sound forms: a choir-like hum and its fifth rising an octave, a brightening sheen, a shimmer and sparkles.
    static float[] ShieldUpData() => new Synth(1.3f, 906)
        .TableTone(0f, 1f, new Sweep(150f, 300f, 300f, 0.6f), 0.5f, Env.Gate(0.3f, 0.3f), Wavetables.Choir, 5f, 0.006f)
        .Tone(0f, 1f, new Sweep(225f, 450f, 450f, 0.6f), 0.25f, Env.Gate(0.35f, 0.3f), Waveform.Triangle, 6f, 0.006f)
        .Tone(0f, 1f, new Sweep(600f, 1200f, 1200f, 0.6f), 0.08f, Env.Gate(0.45f, 0.3f), Waveform.Sine, 7f, 0.008f)
        .Noise(0f, 1f, 0.15f, Env.Swell(0.65f), Filter.Band(new Sweep(1200f, 4800f), 3f))
        .Tremolo(13f, 0.35f)
        .Pings(0.3f, 0.7f, 10, 0.1f, 2800f, 6500f, 0.05f, 0.9f)
        .Reverb(0.3f)
        .Done(0.8f);

    // The shield breaks: a sharp crack and a bubble-like pop, a glassy ring, shards tinkling down and the hum dropping away.
    static float[] ShieldPopData() => new Synth(0.8f, 907)
        .Noise(0f, 0.04f, 0.9f, Env.Hit(0.0004f, 0.006f), Filter.High(3500f))
        .Tone(0f, 0.07f, new Sweep(1100f, 220f), 0.9f, Env.Hit(0.0008f, 0.014f))
        .Partials(0f, 2300f, 0.5f, GlassRatios, GlassAmps, new[] { 0.11f, 0.08f, 0.06f, 0.045f }, 0.005f)
        .Pings(0.01f, 0.55f, 22, 0.32f, 2600f, 8000f, 0.035f, 1.8f)
        .Tone(0f, 0.35f, new Sweep(560f, 140f), 0.3f, Env.Hit(0.003f, 0.07f), Waveform.Triangle, 16f, 0.05f)
        .Reverb(0.2f)
        .Done(0.85f);

    // ================================================================== the campaign bus
    // A bus horn: two blasts, a short one and a long one, of a low two-note chord (E3 and G#3), brassy, buzzy and breathy.
    static float[] BusHornData() => new Synth(1.15f, 908)
        .TableTone(0f, 0.22f, new Sweep(155f, 164.8f, 164.8f, 0.15f), 0.6f, Env.Gate(0.012f, 0.04f), Wavetables.HornLow)
        .TableTone(0f, 0.22f, new Sweep(195f, 207.7f, 207.7f, 0.15f), 0.5f, Env.Gate(0.012f, 0.04f), Wavetables.HornLow)
        .Tone(0f, 0.22f, new Sweep(155f, 164.8f, 164.8f, 0.15f), 0.25f, Env.Gate(0.012f, 0.04f), Waveform.Square, 0f, 0f, Filter.Band(1000f, 2f))
        .Tone(0f, 0.22f, new Sweep(195f, 207.7f, 207.7f, 0.15f), 0.22f, Env.Gate(0.012f, 0.04f), Waveform.Square, 0f, 0f, Filter.Band(1000f, 2f))
        .Noise(0f, 0.22f, 0.06f, Env.Gate(0.01f, 0.04f), Filter.Band(1800f, 1.2f))
        .TableTone(0.3f, 0.5f, new Sweep(155f, 164.8f, 164.8f, 0.07f), 0.6f, Env.Gate(0.012f, 0.08f), Wavetables.HornLow)
        .TableTone(0.3f, 0.5f, new Sweep(195f, 207.7f, 207.7f, 0.07f), 0.5f, Env.Gate(0.012f, 0.08f), Wavetables.HornLow)
        .Tone(0.3f, 0.5f, new Sweep(155f, 164.8f, 164.8f, 0.07f), 0.25f, Env.Gate(0.012f, 0.08f), Waveform.Square, 0f, 0f, Filter.Band(1000f, 2f))
        .Tone(0.3f, 0.5f, new Sweep(195f, 207.7f, 207.7f, 0.07f), 0.22f, Env.Gate(0.012f, 0.08f), Waveform.Square, 0f, 0f, Filter.Band(1000f, 2f))
        .Noise(0.3f, 0.5f, 0.06f, Env.Gate(0.01f, 0.08f), Filter.Band(1800f, 1.2f))
        .Saturate(1.7f)
        .Reverb(0.15f)
        .Done(0.9f);

    // A diesel engine revs up: it catches with a thump, chugs at idle, then climbs, injectors clattering, exhaust growling.
    static float[] BusEngineData() => new Synth(1.35f, 909)
        .Tone(0f, 0.15f, new Sweep(80f, 40f), 0.6f, Env.Hit(0.002f, 0.04f))
        .Tone(0f, 1.3f, new Sweep(28f, 34f, 98f, 0.2f), 0.7f, Env.Gate(0.35f, 0.3f), Waveform.Saw, 0f, 0f,
              Filter.Low(new Sweep(400f, 450f, 1400f, 0.2f), 1.4f))
        .Tone(0f, 1.3f, new Sweep(56f, 68f, 196f, 0.2f), 0.6f, Env.Gate(0.25f, 0.3f), Waveform.Square, 0f, 0f,
              Filter.Band(new Sweep(380f, 420f, 1000f, 0.2f), 2f))
        .Tone(0f, 1.3f, new Sweep(28f, 34f, 98f, 0.2f), 1f, Env.Gate(0.03f, 0.3f), Waveform.Saw, 0f, 0f, Filter.Band(2200f, 5f))
        .Noise(0f, 1.3f, 0.3f, Env.Gate(0.35f, 0.3f), Filter.Low(new Sweep(300f, 350f, 1200f, 0.2f)))
        .Wobble(22f, 0.3f)
        .Saturate(1.6f)
        .Done(0.85f);

    // ================================================================== the signature
    // A big pen signs with a flourish: fast loopy strokes of a scratchy nib, paper grain, a long final stroke and a tap.
    static float[] PenScribbleData() => new Synth(1.1f, 910)
        .Noise(0f, 0.97f, 0.3f, Env.Gate(0.02f, 0.1f), Filter.Band(1300f, 1.5f))
        .Tremolo(8f, 0.6f)
        .Noise(0f, 0.15f, 0.7f, Env.Swell(0.35f), Filter.Band(new Sweep(1800f, 4200f, 2600f, 0.45f), 2.2f))
        .Noise(0.13f, 0.12f, 0.6f, Env.Swell(0.5f), Filter.Band(new Sweep(2600f, 5200f, 3000f, 0.5f), 2.2f))
        .Noise(0.24f, 0.16f, 0.75f, Env.Swell(0.4f), Filter.Band(new Sweep(2000f, 4600f, 2200f, 0.55f), 2.2f))
        .Noise(0.38f, 0.11f, 0.55f, Env.Swell(0.5f), Filter.Band(new Sweep(3000f, 5500f, 3400f, 0.4f), 2.2f))
        .Noise(0.48f, 0.15f, 0.7f, Env.Swell(0.45f), Filter.Band(new Sweep(1900f, 4400f, 2500f, 0.5f), 2.2f))
        .Noise(0.61f, 0.12f, 0.6f, Env.Swell(0.5f), Filter.Band(new Sweep(2800f, 5000f, 3200f, 0.45f), 2.2f))
        .Noise(0.72f, 0.24f, 0.8f, Env.Swell(0.3f), Filter.Band(new Sweep(2200f, 6000f), 1.8f))
        .Tremolo(37f, 0.35f)
        .Crackle(0f, 0.95f, 70, 0.3f, 2500f, 7000f, 1f, 0.0015f)
        .Noise(0.99f, 0.03f, 0.5f, Env.Hit(0.0005f, 0.006f), Filter.Band(1800f, 1.2f))
        .Tone(0.99f, 0.06f, new Sweep(900f, 500f), 0.25f, Env.Hit(0.001f, 0.012f))
        .Done(0.8f);

    // The decree is sealed: a ceremonial brass flourish, "ta-ta-ta-taaa" (D D D up to a held G major chord), and a timpani stroke.
    static float[] DecreeData() => new Synth(1.5f, 911)
        .TableTone(0f, 0.1f, 587.33f, 0.65f, Env.Hit(0.006f, 0.06f), Wavetables.BrassBright)
        .TableTone(0f, 0.1f, 293.66f, 0.4f, Env.Hit(0.006f, 0.06f), Wavetables.BrassDark)
        .TableTone(0.09f, 0.1f, 587.33f, 0.65f, Env.Hit(0.006f, 0.06f), Wavetables.BrassBright)
        .TableTone(0.09f, 0.1f, 293.66f, 0.4f, Env.Hit(0.006f, 0.06f), Wavetables.BrassDark)
        .TableTone(0.18f, 0.1f, 587.33f, 0.65f, Env.Hit(0.006f, 0.06f), Wavetables.BrassBright)
        .TableTone(0.18f, 0.1f, 293.66f, 0.4f, Env.Hit(0.006f, 0.06f), Wavetables.BrassDark)
        .Brass(0.27f, 0.75f, 783.99f, 0.28f, 0.03f, 0.4f)
        .Brass(0.27f, 0.75f, 587.33f, 0.2f, 0.035f, 0.4f)
        .Brass(0.27f, 0.75f, 493.88f, 0.2f, 0.04f, 0.4f)
        .Brass(0.27f, 0.75f, 392f, 0.22f, 0.04f, 0.4f)
        .Brass(0.27f, 0.75f, 196f, 0.25f, 0.05f, 0.4f)
        .Tone(0.27f, 0.6f, new Sweep(98f, 92f), 0.45f, Env.Hit(0.003f, 0.15f))
        .Noise(0.27f, 0.05f, 0.25f, Env.Hit(0.001f, 0.012f), Filter.Band(260f, 1f))
        .Reverb(0.3f)
        .Done(0.85f);

    // An enemy is silenced: a hushed, muffled "shh", then a low bell (D3) tolling once.
    static float[] SilenceData() => new Synth(1.6f, 912)
        .Noise(0f, 0.5f, 0.8f, Env.Swell(0.28f), Filter.Band(new Sweep(2500f, 2100f), 2f))
        .Noise(0f, 0.5f, 0.3f, Env.Swell(0.28f), Filter.Low(1500f))
        .Partials(0.3f, 146.83f, 0.7f, SoundBank.BellRatios, SoundBank.BellAmps, new[] { 0.3f, 0.2f, 0.15f, 0.1f, 0.07f }, 0.003f)
        .Tone(0.3f, 0.1f, new Sweep(180f, 120f), 0.3f, Env.Hit(0.002f, 0.02f))
        .Reverb(0.3f, 0.84f, 0.4f)
        .Done(0.8f);
}

using UnityEngine;

// Every SoundId as a small synthesis recipe (the project has no audio files). A clip is built the first
// time it is needed; SfxPool also warms them all up in the background, one per frame.
public static class SoundBank {
    public static readonly int Count = System.Enum.GetValues(typeof(SoundId)).Length;
    static readonly AudioClip[] clips = new AudioClip[Count];

    // Partial sets (frequency ratios and levels) for struck materials.
    static readonly float[] MetalRatios = { 1f, 2.32f, 3.87f, 5.43f, 6.9f, 8.6f };
    static readonly float[] MetalAmps = { 1f, 0.75f, 0.55f, 0.4f, 0.28f, 0.18f };
    static readonly float[] WoodRatios = { 1f, 2.27f, 4.1f, 6.9f };
    static readonly float[] WoodAmps = { 1f, 0.6f, 0.35f, 0.18f };
    static readonly float[] GlassRatios = { 1f, 1.73f, 2.48f, 3.11f };
    static readonly float[] GlassAmps = { 1f, 0.6f, 0.4f, 0.25f };
    public static readonly float[] BellRatios = { 1f, 2f, 2.76f, 4.07f, 5.4f };
    public static readonly float[] BellAmps = { 1f, 0.35f, 0.22f, 0.1f, 0.05f };

    public static bool IsBuilt(SoundId id) { int i = (int)id; return i >= 0 && i < Count && clips[i] != null; }

    public static AudioClip Get(SoundId id) {
        int i = (int)id;
        if (i < 0 || i >= Count) return null;
        if (clips[i] == null) {
            float[] data = Render(id);
            AudioClip clip = AudioClip.Create(id.ToString(), data.Length, 1, Synth.Rate, false);
            clip.SetData(data, 0);
            clips[i] = clip;
        }
        return clips[i];
    }

    // Relative loudness of each sound (clips are all peak-normalised).
    public static float Gain(SoundId id) {
        switch (id) {
            case SoundId.ExplosionBig: case SoundId.HammerHit: return 1f;
            case SoundId.LightningCrack: case SoundId.VoidBurst: return 0.9f;
            case SoundId.ExplosionSmall: case SoundId.Thunder: case SoundId.Horn: return 0.85f;
            case SoundId.Victory: case SoundId.LaunchHeavy: return 0.7f;
            case SoundId.Defeat: case SoundId.Stick: case SoundId.BreakStone: return 0.65f;
            case SoundId.UIHover: return 0.22f;
            case SoundId.Fuse: case SoundId.UIBack: return 0.4f;
            case SoundId.UIClick: case SoundId.TurnStart: case SoundId.ImpactMetal: case SoundId.ImpactGlass: return 0.45f;
            case SoundId.KnifeThrow: case SoundId.MagicCast: case SoundId.Illusion: case SoundId.VoidHum: return 0.5f;
            case SoundId.Launch: case SoundId.FireIgnite: case SoundId.BreakMetal: case SoundId.Shatter:
            case SoundId.Hurt: case SoundId.Star: case SoundId.Bounce: return 0.55f;
            default: return 0.6f;
        }
    }

    static float[] Render(SoundId id) {
        switch (id) {
            case SoundId.Launch: return Launch();
            case SoundId.LaunchHeavy: return LaunchHeavy();
            case SoundId.KnifeThrow: return KnifeThrow();
            case SoundId.MagicCast: return MagicCast();
            case SoundId.Illusion: return Illusion();
            case SoundId.SpearThrow: return SpearThrow();
            case SoundId.ImpactWood: return ImpactWood();
            case SoundId.ImpactStone: return ImpactStone();
            case SoundId.ImpactMetal: return ImpactMetal();
            case SoundId.ImpactGlass: return ImpactGlass();
            case SoundId.Pierce: return Pierce();
            case SoundId.Stick: return Stick();
            case SoundId.Bounce: return Bounce();
            case SoundId.BreakWood: return BreakWood();
            case SoundId.BreakStone: return BreakStone();
            case SoundId.BreakMetal: return BreakMetal();
            case SoundId.Shatter: return Shatter();
            case SoundId.ExplosionBig: return ExplosionBig();
            case SoundId.ExplosionSmall: return ExplosionSmall();
            case SoundId.Fuse: return Fuse();
            case SoundId.FireIgnite: return FireIgnite();
            case SoundId.LightningCrack: return LightningCrack();
            case SoundId.Thunder: return Thunder();
            case SoundId.Portal: return Portal();
            case SoundId.MeteorFall: return MeteorFall();
            case SoundId.VoidHum: return VoidHum();
            case SoundId.VoidBurst: return VoidBurst();
            case SoundId.Horn: return Horn();
            case SoundId.HammerHit: return HammerHit();
            case SoundId.HammerReturn: return HammerReturn();
            case SoundId.Splash: return Splash();
            case SoundId.Hurt: return Hurt();
            case SoundId.Death: return Death();
            case SoundId.UIClick: return UIClick();
            case SoundId.UIHover: return UIHover();
            case SoundId.UIBack: return UIBack();
            case SoundId.TurnStart: return TurnStart();
            case SoundId.Victory: return Victory();
            case SoundId.Defeat: return Defeat();
            case SoundId.Star: return Star();
            default: return new Synth(0.05f).Done();
        }
    }

    // ================================================================== launches
    // Whoosh: band-passed noise sweeping up and down, plus the snap of the release.
    static float[] Launch() => new Synth(0.5f, 11)
        .Noise(0f, 0.42f, 0.9f, Env.Swell(0.32f), Filter.Band(new Sweep(380f, 1800f, 520f, 0.38f), 1.6f))
        .Noise(0f, 0.3f, 0.2f, Env.Swell(0.4f), Filter.High(new Sweep(2500f, 5000f)))
        .Tone(0f, 0.15f, new Sweep(190f, 80f), 0.4f, Env.Hit(0.002f, 0.035f))
        .Done();

    // The catapult arm: a wooden thunk, a deep thump and a slower, lower whoosh.
    static float[] LaunchHeavy() => new Synth(0.7f, 12)
        .Tone(0f, 0.4f, new Sweep(95f, 42f), 0.9f, Env.Hit(0.003f, 0.1f))
        .Partials(0f, 118f, 0.35f, WoodRatios, WoodAmps, new[] { 0.09f, 0.06f, 0.04f, 0.025f })
        .Noise(0.02f, 0.6f, 0.75f, Env.Swell(0.35f), Filter.Band(new Sweep(220f, 950f, 300f, 0.4f), 1.4f))
        .Noise(0f, 0.04f, 0.35f, Env.Hit(0.0005f, 0.008f), Filter.Band(1800f, 1.2f))
        .Saturate(1.4f).Done();

    // "Shing": a metallic scrape and ring, then a thin whoosh.
    static float[] KnifeThrow() => new Synth(0.45f, 13)
        .Noise(0f, 0.25f, 0.45f, Env.Hit(0.02f, 0.06f), Filter.High(new Sweep(3500f, 6500f)))
        .Partials(0.01f, 2350f, 0.35f, new[] { 1f, 1.52f, 2.13f, 2.91f }, new[] { 1f, 0.7f, 0.5f, 0.3f }, new[] { 0.07f, 0.055f, 0.045f, 0.035f }, 0.004f)
        .Noise(0.03f, 0.35f, 0.45f, Env.Swell(0.3f), Filter.Band(new Sweep(1200f, 3800f, 1500f, 0.35f), 1.8f))
        .Done();

    // Shimmer: a quick rising arpeggio over a soft swell and airy sparkle.
    static float[] MagicCast() {
        Synth s = new Synth(0.95f, 14);
        float[] notes = { 784f, 988f, 1175f, 1568f, 1976f };
        for (int i = 0; i < notes.Length; i++) s.Tone(i * 0.055f, 0.5f, notes[i], 0.3f, Env.Hit(0.004f, 0.12f), Waveform.Triangle, 6f, 0.004f);
        s.Noise(0f, 0.7f, 0.25f, Env.Swell(0.45f), Filter.Band(new Sweep(2500f, 7000f), 2f));
        s.Tone(0f, 0.6f, new Sweep(220f, 330f), 0.3f, Env.Swell(0.3f));
        return s.Reverb(0.35f).Done();
    }

    // A swirl: detuned voices bending around each other over a sweeping noise "phaser".
    static float[] Illusion() {
        Synth s = new Synth(0.85f, 15);
        foreach (float f in new[] { 420f, 530f, 640f })
            s.Tone(0f, 0.75f, new Sweep(f, f * 1.6f, f * 1.15f, 0.5f), 0.22f, Env.Swell(0.4f), Waveform.Triangle, 9f, 0.03f);
        s.Noise(0f, 0.75f, 0.35f, Env.Swell(0.45f), Filter.Band(new Sweep(900f, 3500f, 1200f, 0.5f), 3f));
        return s.Reverb(0.4f).Done();
    }

    // A long, deep whoosh with a whistling tip and a thrumming shaft.
    static float[] SpearThrow() => new Synth(0.7f, 16)
        .Noise(0f, 0.55f, 0.9f, Env.Swell(0.35f), Filter.Band(new Sweep(240f, 1300f, 380f, 0.4f), 1.5f))
        .Tone(0.03f, 0.45f, new Sweep(1150f, 720f), 0.12f, Env.Swell(0.4f))
        .Tone(0f, 0.4f, 90f, 0.35f, Env.Hit(0.01f, 0.09f), Waveform.Triangle, 18f, 0.08f)
        .Done();

    // ================================================================== hits on blocks
    // Knock: a short click exciting the low modes of a plank.
    static float[] ImpactWood() => new Synth(0.35f, 21)
        .Noise(0f, 0.03f, 0.6f, Env.Hit(0.0005f, 0.006f), Filter.Band(1100f, 1.1f))
        .Partials(0f, 240f, 0.8f, WoodRatios, WoodAmps, new[] { 0.07f, 0.045f, 0.03f, 0.018f })
        .Tone(0f, 0.2f, new Sweep(120f, 75f), 0.35f, Env.Hit(0.002f, 0.035f))
        .Done();

    // Clack: a hard, bright click, short high modes and a little grit.
    static float[] ImpactStone() => new Synth(0.35f, 22)
        .Noise(0f, 0.05f, 0.8f, Env.Hit(0.0004f, 0.01f), Filter.Band(2300f, 1.4f))
        .Partials(0f, 640f, 0.45f, new[] { 1f, 2.21f, 3.6f }, new[] { 1f, 0.6f, 0.35f }, new[] { 0.035f, 0.025f, 0.018f })
        .Tone(0f, 0.25f, new Sweep(95f, 60f), 0.55f, Env.Hit(0.002f, 0.045f))
        .Crackle(0.01f, 0.14f, 6, 0.25f, 1500f, 4000f)
        .Done();

    // Clang: long inharmonic metal partials with a slow beat.
    static float[] ImpactMetal() => new Synth(1.8f, 23)
        .Noise(0f, 0.03f, 0.6f, Env.Hit(0.0004f, 0.006f), Filter.Band(3200f, 1f))
        .Partials(0f, 430f, 0.7f, MetalRatios, MetalAmps, new[] { 0.42f, 0.32f, 0.25f, 0.18f, 0.13f, 0.09f }, 0.003f)
        .Tone(0f, 0.2f, new Sweep(140f, 90f), 0.35f, Env.Hit(0.002f, 0.04f))
        .Done();

    // Tink: a few pure, high, quickly fading partials.
    static float[] ImpactGlass() => new Synth(0.6f, 24)
        .Noise(0f, 0.02f, 0.35f, Env.Hit(0.0003f, 0.004f), Filter.High(4000f))
        .Partials(0f, 2900f, 0.6f, GlassRatios, GlassAmps, new[] { 0.12f, 0.08f, 0.06f, 0.04f })
        .Done();

    // Thunk and rip: a dull hit and fibres tearing as the blade goes through.
    static float[] Pierce() => new Synth(0.35f, 25)
        .Noise(0f, 0.06f, 0.7f, Env.Hit(0.0005f, 0.018f), Filter.Band(900f, 1.4f))
        .Tone(0f, 0.2f, new Sweep(150f, 85f), 0.6f, Env.Hit(0.002f, 0.04f))
        .Noise(0.01f, 0.25f, 0.35f, Env.Hit(0.004f, 0.05f), Filter.Band(new Sweep(2200f, 1200f), 2f))
        .Crackle(0.01f, 0.12f, 8, 0.3f, 1500f, 3500f, 1.2f)
        .Done();

    // A thud and the quiver of a blade stuck in wood.
    static float[] Stick() => new Synth(0.55f, 26)
        .Tone(0f, 0.25f, new Sweep(130f, 70f), 0.75f, Env.Hit(0.002f, 0.05f))
        .Partials(0f, 210f, 0.4f, new[] { 1f, 2.3f, 4.2f }, new[] { 1f, 0.5f, 0.25f }, new[] { 0.06f, 0.04f, 0.025f })
        .Tone(0.01f, 0.5f, 290f, 0.22f, Env.Hit(0.003f, 0.11f), Waveform.Triangle, 22f, 0.04f)
        .Done();

    // Tonk: an iron ball bouncing off planks.
    static float[] Bounce() => new Synth(0.35f, 27)
        .Tone(0f, 0.25f, new Sweep(170f, 110f), 0.7f, Env.Hit(0.002f, 0.045f))
        .Partials(0f, 320f, 0.4f, new[] { 1f, 2.35f, 4.5f }, new[] { 1f, 0.5f, 0.25f }, new[] { 0.07f, 0.04f, 0.025f })
        .Done();

    // ================================================================== breaking
    // Crack: the main snap, the body of the plank, fibres snapping one after another and splinters.
    static float[] BreakWood() {
        Synth s = new Synth(0.8f, 31);
        s.Noise(0f, 0.1f, 1f, Env.Hit(0.0005f, 0.02f), Filter.High(700f));
        s.Partials(0f, 150f, 0.6f, WoodRatios, WoodAmps, new[] { 0.12f, 0.08f, 0.05f, 0.03f });
        for (int i = 0; i < 5; i++)
            s.Noise(s.Range(0.02f, 0.35f), 0.06f, s.Range(0.3f, 0.7f), Env.Hit(0.0005f, 0.012f), Filter.Band(s.Range(900f, 2800f), 1.3f));
        s.Noise(0.01f, 0.6f, 0.25f, Env.Hit(0.01f, 0.11f), Filter.Band(3000f, 1f));
        return s.Done();
    }

    // Crumble: a heavy thud, a low rumble and many small stones clattering down.
    static float[] BreakStone() => new Synth(1.1f, 32)
        .Noise(0f, 0.08f, 0.8f, Env.Hit(0.0005f, 0.015f), Filter.Band(1800f, 0.9f))
        .Tone(0f, 0.4f, new Sweep(85f, 50f), 0.8f, Env.Hit(0.003f, 0.08f))
        .Noise(0f, 1f, 0.7f, Env.Hit(0.01f, 0.2f), Filter.Low(new Sweep(600f, 180f)))
        .Crackle(0.01f, 0.85f, 36, 0.55f, 700f, 3200f, 1.8f, 0.006f)
        .Done();

    // Crash: a big clang, a burst of noise and smaller pieces clattering after.
    static float[] BreakMetal() {
        Synth s = new Synth(2f, 33);
        s.Noise(0f, 0.6f, 0.7f, Env.Hit(0.001f, 0.09f), Filter.Band(4200f, 0.8f));
        s.Partials(0f, 265f, 0.8f, MetalRatios, MetalAmps, new[] { 0.5f, 0.38f, 0.3f, 0.22f, 0.16f, 0.12f }, 0.005f);
        s.Tone(0f, 0.45f, new Sweep(110f, 55f), 0.6f, Env.Hit(0.002f, 0.09f));
        for (int i = 0; i < 3; i++)
            s.Partials(s.Range(0.12f, 0.6f), s.Range(600f, 1100f), 0.25f, MetalRatios, MetalAmps, new[] { 0.12f, 0.09f, 0.07f, 0.05f, 0.04f, 0.03f });
        return s.Done();
    }

    // Glass shatter: a bright burst, a crunch and a shower of tinkling shards.
    static float[] Shatter() => new Synth(1.2f, 34)
        .Noise(0f, 0.15f, 0.9f, Env.Hit(0.0005f, 0.03f), Filter.High(2500f))
        .Noise(0f, 0.1f, 0.5f, Env.Hit(0.001f, 0.02f), Filter.Band(900f, 1f))
        .Pings(0f, 0.9f, 34, 0.45f, 2600f, 8500f, 0.045f, 1.7f)
        .Done();

    // ================================================================== blasts and spells
    // Big explosion: a crack, a body of noise closing down from bright to dark, a sub boom, a rolling
    // rumble and debris pattering down, soft-clipped for weight.
    static float[] ExplosionBig() => new Synth(2.4f, 41)
        .Noise(0f, 0.15f, 0.9f, Env.Hit(0.0008f, 0.03f), Filter.Band(2600f, 0.7f))
        .Noise(0f, 1.8f, 1.2f, Env.Hit(0.004f, 0.42f), Filter.Low(new Sweep(1600f, 120f)))
        .Tone(0f, 1f, new Sweep(78f, 30f), 1.1f, Env.Hit(0.003f, 0.28f))
        .Noise(0.05f, 2.3f, 0.6f, Env.Hit(0.06f, 0.6f), Filter.Low(new Sweep(320f, 110f)))
        .Crackle(0.1f, 1.3f, 22, 0.25f, 1500f, 5000f, 1.6f)
        .Saturate(2.2f).Done(0.95f);

    static float[] ExplosionSmall() => new Synth(1.1f, 42)
        .Noise(0f, 0.1f, 0.8f, Env.Hit(0.0006f, 0.02f), Filter.Band(3000f, 0.7f))
        .Noise(0f, 0.9f, 1f, Env.Hit(0.003f, 0.18f), Filter.Low(new Sweep(2400f, 260f)))
        .Tone(0f, 0.6f, new Sweep(115f, 48f), 0.9f, Env.Hit(0.002f, 0.12f))
        .Noise(0.03f, 1.05f, 0.4f, Env.Hit(0.03f, 0.25f), Filter.Low(new Sweep(500f, 160f)))
        .Saturate(1.8f).Done(0.95f);

    // Fuse hiss (looped): bright noise with sputtering crackles and a nervous flutter.
    static float[] Fuse() => new Synth(1.3f, 43)
        .Noise(0f, 1.3f, 0.5f, Env.Flat, Filter.High(3200f))
        .Noise(0f, 1.3f, 0.3f, Env.Flat, Filter.Band(6500f, 1.5f))
        .Crackle(0f, 1.3f, 32, 0.6f, 2500f, 7000f, 1f, 0.003f)
        .Wobble(9f, 0.35f)
        .DoneLoop(0.12f, 0.8f);

    // Fwoosh: noise opening up from dark to bright, a low rising whump and crackles.
    static float[] FireIgnite() => new Synth(1f, 44)
        .Noise(0f, 0.95f, 0.9f, Env.Hit(0.06f, 0.22f), Filter.Low(new Sweep(250f, 2600f, 900f, 0.3f)))
        .Tone(0f, 0.4f, new Sweep(70f, 120f), 0.45f, Env.Swell(0.3f))
        .Crackle(0.08f, 0.8f, 16, 0.3f, 1800f, 5000f, 1.2f)
        .Done();

    // A razor-sharp crack, crackling arcs and a short low thump.
    static float[] LightningCrack() => new Synth(0.9f, 45)
        .Noise(0f, 0.06f, 1.2f, Env.Hit(0.0002f, 0.012f), default)
        .Noise(0f, 0.6f, 0.7f, Env.Hit(0.001f, 0.1f), Filter.Low(new Sweep(5000f, 900f)))
        .Crackle(0.005f, 0.25f, 14, 0.8f, 1500f, 6000f, 1.3f, 0.003f)
        .Tone(0f, 0.6f, new Sweep(70f, 40f), 0.6f, Env.Hit(0.002f, 0.12f))
        .Saturate(1.6f).Done(0.95f);

    // A long rolling rumble: a distant crack, a dark body with several swells and a fluttering roll.
    static float[] Thunder() {
        Synth s = new Synth(3.4f, 46);
        s.Noise(0f, 0.1f, 0.6f, Env.Hit(0.001f, 0.025f), Filter.Band(2200f, 0.6f));
        s.Noise(0f, 3.4f, 1f, Env.Hit(0.08f, 0.9f), Filter.Low(new Sweep(420f, 140f)));
        for (int i = 0; i < 4; i++)
            s.Noise(s.Range(0.15f, 1.9f), s.Range(0.5f, 1.1f), s.Range(0.4f, 0.8f), Env.Swell(0.3f), Filter.Low(s.Range(180f, 320f)));
        s.Tone(0f, 1.6f, new Sweep(55f, 35f), 0.5f, Env.Hit(0.02f, 0.4f));
        return s.Wobble(6f, 0.4f).Saturate(1.4f).Done(0.9f);
    }

    // Portal: a rising open chord with tremolo, a brightening noise swell and twinkles.
    static float[] Portal() {
        Synth s = new Synth(1.6f, 47);
        foreach (float r in new[] { 1f, 1.5f, 2f })
            s.Tone(0f, 1.5f, new Sweep(140f * r, 290f * r), 0.3f, Env.Gate(0.6f, 0.35f), Waveform.Triangle, 7f, 0.012f);
        s.Noise(0f, 1.5f, 0.3f, Env.Gate(0.7f, 0.3f), Filter.Band(new Sweep(1200f, 4500f), 2f));
        s.Pings(0.3f, 1.1f, 12, 0.12f, 2500f, 6000f, 0.06f, 0.8f);
        return s.Tremolo(11f, 0.3f).Reverb(0.35f).Done(0.85f);
    }

    // Falling meteor: a descending whistle over a roar that grows as it comes down.
    static float[] MeteorFall() => new Synth(1.5f, 48)
        .Tone(0f, 1.4f, new Sweep(1700f, 320f), 0.3f, Env.Gate(0.5f, 0.08f), Waveform.Sine, 9f, 0.008f)
        .Noise(0f, 1.4f, 0.8f, Env.Gate(0.9f, 0.08f), Filter.Band(new Sweep(3200f, 650f), 1.2f))
        .Noise(0f, 1.4f, 0.4f, Env.Gate(1f, 0.08f), Filter.Low(new Sweep(300f, 900f)))
        .Crackle(0.3f, 1.1f, 16, 0.2f, 1500f, 4000f, 0.7f)
        .Done();

    // Black-hole drone (looped): a low root and fifth with a slow throb and dark rumbling air.
    static float[] VoidHum() => new Synth(2.3f, 49)
        .Tone(0f, 2.3f, 55f, 0.7f, Env.Flat, Waveform.Sine, 0.5f, 0.012f)
        .Tone(0f, 2.3f, 82.6f, 0.35f, Env.Flat, Waveform.Triangle, 0.75f, 0.01f)
        .Tone(0f, 2.3f, 110.3f, 0.25f, Env.Flat, Waveform.Sine, 0.5f, 0.015f)
        .Noise(0f, 2.3f, 0.35f, Env.Flat, Filter.Band(new Sweep(180f, 420f, 180f, 0.5f), 2.5f))
        .Tremolo(1.5f, 0.35f)
        .DoneLoop(0.3f, 0.85f);

    // Space sucked in (a rising swell), then a deep burst with a ringing, wavering chord.
    static float[] VoidBurst() => new Synth(1.5f, 50)
        .Noise(0f, 0.36f, 0.8f, Env.Swell(0.97f), Filter.Low(new Sweep(200f, 3200f)))
        .Tone(0f, 0.36f, new Sweep(45f, 190f), 0.6f, Env.Swell(0.95f), Waveform.Triangle)
        .Noise(0.35f, 1.1f, 1f, Env.Hit(0.002f, 0.2f), Filter.Band(new Sweep(1400f, 300f), 0.8f))
        .Tone(0.35f, 1.1f, new Sweep(70f, 32f), 1f, Env.Hit(0.003f, 0.25f))
        .Partials(0.35f, 220f, 0.3f, new[] { 1f, 1.26f, 1.5f, 2.02f }, new[] { 1f, 0.8f, 0.7f, 0.4f }, new[] { 0.16f, 0.14f, 0.12f, 0.1f }, 0.01f)
        .Reverb(0.3f).Saturate(1.6f).Done(0.95f);

    // Gjallarhorn: a deep, brassy open fifth that swells in, wavers and rings out in a big hall.
    static float[] Horn() {
        Synth s = new Synth(3.3f, 51);
        float[] notes = { 73.42f, 110f, 146.83f };                     // D2, A2, D3
        float[] amps = { 0.9f, 0.6f, 0.45f };
        for (int i = 0; i < notes.Length; i++) {
            float f = notes[i], t0 = i * 0.04f;
            s.TableTone(t0, 2.7f, new Sweep(f * 0.96f, f, f, 0.06f), amps[i], Env.Gate(0.18f, 0.55f), Wavetables.BrassDark, 4.8f, 0.004f);
            s.TableTone(t0, 2.7f, new Sweep(f * 0.963f, f * 1.003f, f * 1.003f, 0.07f), amps[i] * 0.7f, Env.Gate(0.35f, 0.5f), Wavetables.HornLow, 5.2f, 0.005f);
        }
        s.Noise(0f, 2.7f, 0.08f, Env.Gate(0.3f, 0.5f), Filter.Band(900f, 1f));
        return s.Reverb(0.45f, 0.88f, 0.3f).Saturate(1.3f).Done(0.9f);
    }

    // Mjolnir lands: a huge clang, the crack of lightning and a booming thunder roll.
    static float[] HammerHit() => new Synth(2.4f, 52)
        .Noise(0f, 0.06f, 1f, Env.Hit(0.0004f, 0.01f), Filter.High(1500f))
        .Partials(0f, 330f, 0.7f, MetalRatios, MetalAmps, new[] { 0.6f, 0.45f, 0.35f, 0.25f, 0.18f, 0.13f }, 0.004f)
        .Tone(0f, 1.5f, new Sweep(62f, 30f), 1f, Env.Hit(0.003f, 0.35f))
        .Noise(0.02f, 2.35f, 0.7f, Env.Hit(0.05f, 0.5f), Filter.Low(new Sweep(400f, 120f)))
        .Crackle(0f, 0.25f, 10, 0.4f, 2000f, 6000f, 1.4f)
        .Saturate(1.8f).Done(0.95f);

    // Mjolnir flies home: a whoosh with a rising magical hum, crackles, and a clink as Thor catches it.
    static float[] HammerReturn() => new Synth(1f, 53)
        .Noise(0f, 0.7f, 0.8f, Env.Swell(0.55f), Filter.Band(new Sweep(300f, 1500f, 500f, 0.55f), 1.6f))
        .Tone(0f, 0.7f, new Sweep(220f, 340f, 250f, 0.55f), 0.18f, Env.Swell(0.55f), Waveform.Triangle, 7f, 0.01f)
        .Crackle(0.05f, 0.6f, 10, 0.2f, 3000f, 7000f, 1f)
        .Partials(0.72f, 1750f, 0.35f, new[] { 1f, 2.4f, 3.9f }, new[] { 1f, 0.5f, 0.3f }, new[] { 0.06f, 0.04f, 0.03f })
        .Done();

    // Splash: the plunge, spray hiss and a few rising bubbles.
    static float[] Splash() {
        Synth s = new Synth(1f, 54);
        s.Noise(0f, 0.4f, 1f, Env.Hit(0.002f, 0.08f), Filter.Low(new Sweep(1600f, 400f)));
        s.Tone(0f, 0.2f, new Sweep(140f, 70f), 0.5f, Env.Hit(0.003f, 0.05f));
        s.Noise(0.02f, 0.95f, 0.28f, Env.Hit(0.02f, 0.2f), Filter.High(2200f));
        for (int i = 0; i < 12; i++) {
            float t = s.Range(0.04f, 0.55f), f = s.Range(280f, 700f);
            s.Tone(t, 0.07f, new Sweep(f, f * 2.2f), s.Range(0.25f, 0.5f) * (1f - t), Env.Hit(0.002f, 0.018f));
        }
        return s.Done();
    }

    // ================================================================== commanders
    // A short grunt: a buzzy voice through two vowel formants ("uh") with a falling pitch.
    static float[] Hurt() {
        Synth s = new Synth(0.3f, 61);
        Sweep pitch = new Sweep(165f, 178f, 118f, 0.2f);
        s.Tone(0f, 0.28f, pitch, 0.8f, Env.Hit(0.012f, 0.08f), Waveform.Saw, 0f, 0f, Filter.Band(650f, 4f));
        s.Tone(0f, 0.28f, pitch, 0.5f, Env.Hit(0.012f, 0.07f), Waveform.Saw, 0f, 0f, Filter.Band(1150f, 5f));
        s.Noise(0f, 0.15f, 0.12f, Env.Hit(0.005f, 0.04f), Filter.Band(1400f, 1f));
        return s.Done();
    }

    // A falling, wavering tone with a ghostly breath: the soul leaving.
    static float[] Death() => new Synth(1.4f, 62)
        .Tone(0f, 1.1f, new Sweep(560f, 140f), 0.6f, Env.Gate(0.02f, 0.5f), Waveform.Triangle, 6f, 0.02f)
        .Tone(0f, 1.1f, new Sweep(1120f, 280f), 0.15f, Env.Gate(0.02f, 0.5f), Waveform.Sine, 6f, 0.02f)
        .Noise(0f, 1.2f, 0.12f, Env.Swell(0.3f), Filter.Band(new Sweep(2400f, 800f), 1.5f))
        .Reverb(0.35f).Done();

    // ================================================================== interface and flow
    static float[] UIClick() => new Synth(0.09f, 71)
        .Tone(0f, 0.08f, new Sweep(1350f, 880f), 0.6f, Env.Hit(0.0008f, 0.016f))
        .Noise(0f, 0.012f, 0.25f, Env.Hit(0.0003f, 0.002f), Filter.Band(3500f, 1f))
        .Done();

    static float[] UIHover() => new Synth(0.06f, 72)
        .Tone(0f, 0.06f, 2100f, 0.5f, Env.Hit(0.001f, 0.01f))
        .Done();

    static float[] UIBack() => new Synth(0.22f, 73)
        .Tone(0f, 0.08f, 940f, 0.5f, Env.Hit(0.001f, 0.022f), Waveform.Triangle)
        .Tone(0.065f, 0.15f, 630f, 0.5f, Env.Hit(0.001f, 0.03f), Waveform.Triangle)
        .Done();

    // A soft bell, then a quieter one a fifth above.
    static float[] TurnStart() => new Synth(2.2f, 74)
        .Partials(0f, 880f, 0.5f, BellRatios, BellAmps, new[] { 0.6f, 0.4f, 0.28f, 0.18f, 0.12f })
        .Partials(0.09f, 1318.5f, 0.28f, BellRatios, BellAmps, new[] { 0.55f, 0.36f, 0.25f, 0.16f, 0.1f })
        .Reverb(0.3f).Done(0.8f);

    // Victory fanfare: a quick major arpeggio, then the chord held by brass, with a sparkle.
    static float[] Victory() {
        Synth s = new Synth(2.4f, 75);
        float[] chord = { 523.25f, 659.25f, 783.99f, 1046.5f };         // C5 E5 G5 C6
        for (int i = 0; i < chord.Length; i++)
            s.TableTone(i * 0.11f, 0.45f, chord[i], 0.35f, Env.Hit(0.006f, 0.12f), Wavetables.BrassBright);
        foreach (float f in chord) s.Brass(0.46f, 1.5f, f, 0.2f, 0.04f, 0.6f);
        s.Pings(0.46f, 1.2f, 10, 0.1f, 3000f, 7000f, 0.08f, 1.2f);
        return s.Reverb(0.35f).Done(0.85f);
    }

    // Defeat: a slow minor descent settling on a low, dark chord.
    static float[] Defeat() {
        Synth s = new Synth(2.4f, 76);
        float[] line = { 392f, 311.13f, 261.63f };                      // G4 Eb4 C4
        for (int i = 0; i < line.Length; i++)
            s.Tone(i * 0.32f, 0.6f, new Sweep(line[i], line[i] * 0.985f), 0.45f, Env.Hit(0.02f, 0.2f), Waveform.Triangle, 5f, 0.006f);
        s.Tone(0.96f, 1.3f, 130.81f, 0.4f, Env.Gate(0.05f, 0.9f), Waveform.Triangle, 4f, 0.005f);   // C3
        s.Tone(0.96f, 1.3f, 155.56f, 0.3f, Env.Gate(0.05f, 0.9f), Waveform.Triangle, 4f, 0.005f);   // Eb3
        return s.Reverb(0.35f).Done(0.85f);
    }

    // A high bell ding with a few sparkles.
    static float[] Star() => new Synth(1.3f, 77)
        .Partials(0f, 2093f, 0.5f, new[] { 1f, 2f, 3f }, new[] { 1f, 0.3f, 0.12f }, new[] { 0.3f, 0.16f, 0.09f })
        .Pings(0.03f, 0.5f, 7, 0.18f, 3500f, 8000f, 0.05f, 1.3f)
        .Reverb(0.3f).Done(0.8f);
}

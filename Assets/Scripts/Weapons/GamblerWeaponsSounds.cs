using UnityEngine;

// The Gambler's own sounds, synthesised like the rest of the game's (Synth; see SoundBank and LichSounds for the house
// recipes) the first time each is needed, then kept. They play through the shared pool (SFX.PlayClip).
public static class GamblerSounds {
    // Partial sets: a die (hard and hollow), a coin (the modes of a thin disc), a music-box tine (a bar clamped at one
    // end) and a sword blade (a free bar).
    static readonly float[] DieRatios = { 1f, 2.21f, 3.6f }, DieAmps = { 1f, 0.6f, 0.35f };
    static readonly float[] CoinRatios = { 1f, 1.73f, 2.33f, 3.91f, 4.11f }, CoinAmps = { 1f, 0.7f, 0.5f, 0.3f, 0.25f };
    static readonly float[] TineRatios = { 1f, 6.27f }, TineAmps = { 1f, 0.3f }, TineDecays = { 0.3f, 0.03f };
    static readonly float[] BladeRatios = { 1f, 2.76f, 5.4f, 8.93f }, BladeAmps = { 1f, 0.6f, 0.35f, 0.2f };
    static readonly float[] ChimeDecays = { 0.25f, 0.16f, 0.1f, 0.07f, 0.05f };

    static AudioClip diceRattle, diceLand, lucky, noLuck, shuffle, cardFlick, cardFlip, coinFlip, coinLand, coinCrash,
                     jackpot, rouletteSpin, crank, boing, honk, swordRing;

    public static AudioClip DiceRattle => diceRattle != null ? diceRattle : diceRattle = Make("Gambler dice rattle", DiceRattleData());
    public static AudioClip DiceLand => diceLand != null ? diceLand : diceLand = Make("Gambler dice land", DiceLandData());
    public static AudioClip Lucky => lucky != null ? lucky : lucky = Make("Gambler lucky", LuckyData());
    public static AudioClip NoLuck => noLuck != null ? noLuck : noLuck = Make("Gambler no luck", NoLuckData());
    public static AudioClip Shuffle => shuffle != null ? shuffle : shuffle = Make("Gambler shuffle", ShuffleData());
    public static AudioClip CardFlick => cardFlick != null ? cardFlick : cardFlick = Make("Gambler card flick", CardFlickData());
    public static AudioClip CardFlip => cardFlip != null ? cardFlip : cardFlip = Make("Gambler card flip", CardFlipData());
    public static AudioClip CoinFlip => coinFlip != null ? coinFlip : coinFlip = Make("Gambler coin flip", CoinFlipData());
    public static AudioClip CoinLand => coinLand != null ? coinLand : coinLand = Make("Gambler coin land", CoinLandData());
    public static AudioClip CoinCrash => coinCrash != null ? coinCrash : coinCrash = Make("Gambler coin crash", CoinCrashData());
    public static AudioClip Jackpot => jackpot != null ? jackpot : jackpot = Make("Gambler jackpot", JackpotData());
    public static AudioClip RouletteSpin => rouletteSpin != null ? rouletteSpin : rouletteSpin = Make("Gambler roulette", RouletteSpinData());
    public static AudioClip Crank => crank != null ? crank : crank = Make("Gambler crank", CrankData());
    public static AudioClip Boing => boing != null ? boing : boing = Make("Gambler boing", BoingData());
    public static AudioClip Honk => honk != null ? honk : honk = Make("Gambler honk", HonkData());
    public static AudioClip SwordRing => swordRing != null ? swordRing : swordRing = Make("Gambler sword", SwordRingData());

    public static void Play(AudioClip clip, Vector3? pos, float volume, float pitch = 1f) { SFX.PlayClip(clip, pos, volume, pitch); }

    // Builds all the clips at once (a Gambler is in this battle), so no throw waits for them.
    public static void WarmUp() {
        _ = DiceRattle; _ = DiceLand; _ = Lucky; _ = NoLuck; _ = Shuffle; _ = CardFlick; _ = CardFlip; _ = CoinFlip;
        _ = CoinLand; _ = CoinCrash; _ = Jackpot; _ = RouletteSpin; _ = Crank; _ = Boing; _ = Honk; _ = SwordRing;
    }

    static AudioClip Make(string name, float[] data) {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Synth.Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ================================================================== dice
    // Two dice shaken in a cupped hand: three shakes of hard clicks and hollow knocks, then they tumble out.
    static float[] DiceRattleData() => new Synth(0.7f, 801)
        .Crackle(0f, 0.08f, 9, 0.8f, 1800f, 5000f, 1.3f, 0.002f)
        .Pings(0f, 0.08f, 3, 0.3f, 1200f, 2600f, 0.01f, 1.3f)
        .Crackle(0.14f, 0.08f, 9, 0.8f, 1800f, 5000f, 1.3f, 0.002f)
        .Pings(0.14f, 0.08f, 3, 0.3f, 1200f, 2600f, 0.01f, 1.3f)
        .Crackle(0.28f, 0.08f, 9, 0.8f, 1800f, 5000f, 1.3f, 0.002f)
        .Pings(0.28f, 0.08f, 3, 0.3f, 1200f, 2600f, 0.01f, 1.3f)
        .Crackle(0.42f, 0.24f, 12, 0.6f, 1500f, 4500f, 1.6f, 0.003f)
        .Pings(0.42f, 0.24f, 5, 0.22f, 1000f, 2200f, 0.012f, 1.6f)
        .Done(0.8f);

    // Two dice clack down on the table, one just after the other, and roll a moment before they settle.
    static float[] DiceLandData() => new Synth(0.5f, 802)
        .Noise(0f, 0.03f, 0.9f, Env.Hit(0.0003f, 0.004f), Filter.Band(3000f, 1.1f))
        .Partials(0f, 1450f, 0.5f, DieRatios, DieAmps, new[] { 0.03f, 0.018f, 0.012f })
        .Tone(0f, 0.12f, new Sweep(240f, 150f), 0.3f, Env.Hit(0.001f, 0.02f))
        .Noise(0.065f, 0.03f, 0.8f, Env.Hit(0.0003f, 0.004f), Filter.Band(3500f, 1.1f))
        .Partials(0.065f, 1720f, 0.42f, DieRatios, DieAmps, new[] { 0.026f, 0.016f, 0.01f })
        .Crackle(0.1f, 0.3f, 14, 0.45f, 1500f, 4500f, 1.5f, 0.0025f)
        .Pings(0.1f, 0.3f, 5, 0.2f, 1300f, 2000f, 0.015f, 1.5f)
        .Done(0.85f);

    // ================================================================== luck
    // A win: a quick, bright major arpeggio of chimes (G5 C6 E6 G6) up to a ringing C7, with a sparkle over it.
    static float[] LuckyData() => new Synth(1.5f, 803)
        .Partials(0f, 784f, 0.3f, SoundBank.BellRatios, SoundBank.BellAmps, ChimeDecays)
        .Partials(0.06f, 1046.5f, 0.3f, SoundBank.BellRatios, SoundBank.BellAmps, ChimeDecays)
        .Partials(0.12f, 1318.5f, 0.3f, SoundBank.BellRatios, SoundBank.BellAmps, ChimeDecays)
        .Partials(0.18f, 1568f, 0.3f, SoundBank.BellRatios, SoundBank.BellAmps, ChimeDecays)
        .Partials(0.24f, 2093f, 0.4f, SoundBank.BellRatios, SoundBank.BellAmps, new[] { 0.4f, 0.26f, 0.17f, 0.11f, 0.07f }, 0.003f)
        .Pings(0.26f, 0.8f, 12, 0.14f, 3500f, 8000f, 0.06f, 1.3f)
        .Noise(0.2f, 0.9f, 0.1f, Env.Swell(0.25f), Filter.High(6500f))
        .Reverb(0.35f)
        .Done(0.85f);

    // A loss: the sad trombone, "wah, wah, wah, waaah", a semitone lower each time, the last note wobbling away.
    static float[] NoLuckData() => new Synth(1.5f, 804)
        .Tone(0f, 0.22f, new Sweep(284f, 293.7f, 291f, 0.25f), 0.55f, Env.Gate(0.02f, 0.06f), Waveform.Saw, 0f, 0f,
              Filter.Low(new Sweep(300f, 1500f, 550f, 0.4f), 2.2f))
        .Tone(0.26f, 0.22f, new Sweep(268f, 277.2f, 275f, 0.25f), 0.55f, Env.Gate(0.02f, 0.06f), Waveform.Saw, 0f, 0f,
              Filter.Low(new Sweep(300f, 1450f, 520f, 0.4f), 2.2f))
        .Tone(0.52f, 0.22f, new Sweep(253f, 261.6f, 259f, 0.25f), 0.55f, Env.Gate(0.02f, 0.06f), Waveform.Saw, 0f, 0f,
              Filter.Low(new Sweep(300f, 1400f, 500f, 0.4f), 2.2f))
        .Tone(0.78f, 0.62f, new Sweep(239f, 246.9f, 226f, 0.2f), 0.6f, Env.Gate(0.03f, 0.3f), Waveform.Saw, 5.5f, 0.03f,
              Filter.Low(new Sweep(300f, 1500f, 420f, 0.2f), 2.2f))
        .Reverb(0.2f)
        .Done(0.85f);

    // ================================================================== cards
    // A riffle shuffle: a fast "brrrrt" of cards flicking off the thumbs, the bridge as they cascade together, a tap.
    static float[] ShuffleData() => new Synth(0.85f, 805)
        .Crackle(0.02f, 0.5f, 70, 0.7f, 2500f, 7000f, 1f, 0.0018f)
        .Noise(0f, 0.55f, 0.25f, Env.Gate(0.03f, 0.08f), Filter.Band(4500f, 1f))
        .Noise(0.52f, 0.2f, 0.45f, Env.Swell(0.35f), Filter.Band(new Sweep(2000f, 4500f), 1.3f))
        .Crackle(0.52f, 0.18f, 18, 0.35f, 3000f, 7000f, 1f, 0.0015f)
        .Noise(0.74f, 0.04f, 0.6f, Env.Hit(0.0005f, 0.008f), Filter.Band(1300f, 1.2f))
        .Tone(0.74f, 0.08f, new Sweep(240f, 150f), 0.35f, Env.Hit(0.002f, 0.015f))
        .Done(0.8f);

    // A card flicked off the fingers: a snap, then a short papery whoosh fluttering as it spins away.
    static float[] CardFlickData() => new Synth(0.4f, 806)
        .Noise(0.01f, 0.34f, 0.8f, Env.Swell(0.3f), Filter.Band(new Sweep(1500f, 4800f, 2200f, 0.3f), 1.5f))
        .Tremolo(32f, 0.55f)
        .Noise(0f, 0.025f, 0.9f, Env.Hit(0.0003f, 0.003f), Filter.High(2500f))
        .Tone(0f, 0.04f, new Sweep(2400f, 1300f), 0.3f, Env.Hit(0.0005f, 0.006f))
        .Done(0.8f);

    // A card turned over: a tiny swish and a crisp snap.
    static float[] CardFlipData() => new Synth(0.16f, 807)
        .Noise(0f, 0.05f, 0.3f, Env.Swell(0.7f), Filter.Band(3000f, 1.2f))
        .Noise(0.04f, 0.03f, 1f, Env.Hit(0.0003f, 0.004f), Filter.Band(3800f, 0.9f))
        .Noise(0.04f, 0.07f, 0.4f, Env.Hit(0.001f, 0.012f), Filter.Band(1400f, 1.3f))
        .Tone(0.04f, 0.05f, new Sweep(1900f, 1000f), 0.25f, Env.Hit(0.0005f, 0.008f))
        .Done(0.8f);

    // ================================================================== coins
    // A coin flicked off the thumb: a click and a bright "ting" shimmering as the coin spins through the air.
    static float[] CoinFlipData() => new Synth(1f, 808)
        .Partials(0f, 3000f, 0.6f, CoinRatios, CoinAmps, new[] { 0.3f, 0.22f, 0.16f, 0.1f, 0.08f }, 0.004f)
        .Noise(0f, 0.9f, 0.08f, Env.Swell(0.3f), Filter.Band(6500f, 2f))
        .Tremolo(15f, 0.5f)
        .Noise(0f, 0.015f, 0.7f, Env.Hit(0.0003f, 0.002f), Filter.High(3000f))
        .Reverb(0.2f)
        .Done(0.8f);

    // A small coin lands: a clink, two little bounces, then it wobbles flat ever faster and lies still.
    static float[] CoinLandData() => new Synth(0.8f, 809)
        .Noise(0f, 0.015f, 0.6f, Env.Hit(0.0003f, 0.002f), Filter.High(4000f))
        .Partials(0f, 3300f, 0.55f, CoinRatios, CoinAmps, new[] { 0.14f, 0.1f, 0.07f, 0.05f, 0.04f }, 0.004f)
        .Partials(0.12f, 3300f, 0.3f, CoinRatios, CoinAmps, new[] { 0.08f, 0.06f, 0.04f, 0.03f, 0.02f })
        .Partials(0.2f, 3300f, 0.18f, CoinRatios, CoinAmps, new[] { 0.06f, 0.04f, 0.03f, 0.02f, 0.015f })
        .Tone(0.26f, 0.44f, new Sweep(12f, 55f), 1.4f, Env.Swell(0.85f), Waveform.Square, 0f, 0f, Filter.Band(3300f, 4f))
        .Done(0.8f);

    // A giant coin slams down: a crack, a deep metallic boom and a long ring wobbling as the coin settles.
    static float[] CoinCrashData() => new Synth(1.5f, 810)
        .Partials(0f, 180f, 0.8f, CoinRatios, CoinAmps, new[] { 0.4f, 0.32f, 0.25f, 0.18f, 0.13f }, 0.004f)
        .Partials(0f, 760f, 0.3f, CoinRatios, CoinAmps, new[] { 0.45f, 0.35f, 0.28f, 0.2f, 0.15f }, 0.003f)
        .Tremolo(6f, 0.3f)
        .Noise(0f, 0.08f, 1f, Env.Hit(0.0005f, 0.012f), Filter.Band(2200f, 0.7f))
        .Tone(0f, 0.9f, new Sweep(72f, 34f), 1.1f, Env.Hit(0.003f, 0.22f))
        .Noise(0f, 1.1f, 0.5f, Env.Hit(0.01f, 0.25f), Filter.Low(new Sweep(600f, 120f)))
        .Reverb(0.2f)
        .Saturate(1.5f)
        .Done(0.95f);

    // Jackpot: the machine's bell ringing "rrrring" and a cascade of coins pouring into the tray.
    static float[] JackpotData() => new Synth(1.8f, 811)
        .Tone(0f, 1.15f, 1400f, 0.5f, Env.Gate(0.004f, 0.35f))
        .Tone(0f, 1.15f, 2800f, 0.15f, Env.Gate(0.004f, 0.3f))
        .Tone(0f, 1.15f, 3864f, 0.18f, Env.Gate(0.004f, 0.25f))
        .Tone(0f, 1.15f, 5698f, 0.08f, Env.Gate(0.004f, 0.2f))
        .Tremolo(17f, 0.85f)
        .Pings(0.12f, 1.45f, 70, 0.4f, 2800f, 7000f, 0.045f, 1.1f)
        .Crackle(0.12f, 1.45f, 60, 0.45f, 1500f, 5000f, 1.1f, 0.003f)
        .Reverb(0.2f)
        .Done(0.85f);

    // ================================================================== roulette
    // The roulette ball: a whirring roll rattling slower and slower, a drop, bounces in the pockets and a final clack.
    static float[] RouletteSpinData() => new Synth(1.5f, 812)
        .Noise(0f, 1.05f, 0.25f, Env.Gate(0.04f, 0.35f), Filter.Band(new Sweep(1800f, 700f), 1.5f))
        .Tone(0f, 1f, new Sweep(26f, 7f), 1.5f, Env.Gate(0.02f, 0.25f), Waveform.Square, 0f, 0f, Filter.Band(3000f, 2.5f))
        .Wobble(12f, 0.4f)
        .Noise(1f, 0.05f, 0.7f, Env.Hit(0.0004f, 0.006f), Filter.Band(2800f, 1.2f))
        .Crackle(1.06f, 0.2f, 6, 0.55f, 1800f, 4000f, 1.5f, 0.003f)
        .Noise(1.3f, 0.05f, 0.9f, Env.Hit(0.0004f, 0.007f), Filter.Band(2400f, 1.2f))
        .Partials(1.3f, 820f, 0.4f, DieRatios, DieAmps, new[] { 0.05f, 0.03f, 0.02f })
        .Done(0.85f);

    // ================================================================== jack-in-the-box
    // A jack-in-the-box crank: the ratchet ticking under music-box plinks of "Pop Goes the Weasel" (its first line).
    static float[] CrankData() => new Synth(1.5f, 813)
        .Tone(0f, 0.85f, 7f, 1f, Env.Gate(0.01f, 0.05f), Waveform.Square, 0f, 0f, Filter.Band(2200f, 3f))
        .Partials(0f, 1046.5f, 0.45f, TineRatios, TineAmps, TineDecays)
        .Partials(0.17f, 1046.5f, 0.4f, TineRatios, TineAmps, TineDecays)
        .Partials(0.255f, 1174.7f, 0.42f, TineRatios, TineAmps, TineDecays)
        .Partials(0.425f, 1174.7f, 0.4f, TineRatios, TineAmps, TineDecays)
        .Partials(0.51f, 1318.5f, 0.42f, TineRatios, TineAmps, TineDecays)
        .Partials(0.595f, 1568f, 0.42f, TineRatios, TineAmps, TineDecays)
        .Partials(0.68f, 1318.5f, 0.4f, TineRatios, TineAmps, TineDecays)
        .Partials(0.765f, 1046.5f, 0.45f, TineRatios, TineAmps, new[] { 0.2f, 0.03f })
        .Reverb(0.25f)
        .Done(0.8f);

    // A spring popping out: a pop, then a twangy "boi-oi-oing" wobbling fast round a rising, falling pitch.
    static float[] BoingData() => new Synth(0.65f, 814)
        .Noise(0f, 0.03f, 0.6f, Env.Hit(0.0005f, 0.006f), Filter.Band(1600f, 1f))
        .Tone(0f, 0.6f, new Sweep(150f, 330f, 250f, 0.12f), 0.8f, Env.Hit(0.004f, 0.17f), Waveform.Triangle, 17f, 0.16f)
        .Tone(0f, 0.6f, new Sweep(150f, 330f, 250f, 0.12f), 0.3f, Env.Hit(0.004f, 0.11f), Waveform.Saw, 17f, 0.16f,
              Filter.Band(new Sweep(900f, 2400f, 1200f, 0.12f), 3f))
        .Done(0.85f);

    // A clown horn: two quick squeezes of the rubber bulb, "honk-honk", buzzy and nasal.
    static float[] HonkData() => new Synth(0.45f, 815)
        .Tone(0f, 0.16f, new Sweep(270f, 330f, 310f, 0.3f), 0.7f, Env.Gate(0.012f, 0.04f), Waveform.Saw, 30f, 0.01f, Filter.Band(1100f, 2.5f))
        .Tone(0f, 0.16f, new Sweep(270f, 330f, 310f, 0.3f), 0.35f, Env.Gate(0.012f, 0.04f), Waveform.Square, 30f, 0.01f, Filter.Band(2600f, 3f))
        .Noise(0f, 0.16f, 0.1f, Env.Gate(0.01f, 0.04f), Filter.Band(1800f, 1.5f))
        .Tone(0.22f, 0.18f, new Sweep(290f, 350f, 330f, 0.3f), 0.8f, Env.Gate(0.012f, 0.05f), Waveform.Saw, 30f, 0.01f, Filter.Band(1100f, 2.5f))
        .Tone(0.22f, 0.18f, new Sweep(290f, 350f, 330f, 0.3f), 0.4f, Env.Gate(0.012f, 0.05f), Waveform.Square, 30f, 0.01f, Filter.Band(2600f, 3f))
        .Noise(0.22f, 0.18f, 0.1f, Env.Gate(0.01f, 0.05f), Filter.Band(1800f, 1.5f))
        .Saturate(1.8f)
        .Done(0.85f);

    // ================================================================== the King's sword
    // A sword drawn and thrown: a rising metallic "shhhing", a long bright ring and the whoosh of the throw.
    static float[] SwordRingData() => new Synth(1.5f, 816)
        .Noise(0f, 0.28f, 0.6f, Env.Swell(0.8f), Filter.High(new Sweep(2500f, 7000f)))
        .Noise(0f, 0.28f, 0.4f, Env.Swell(0.85f), Filter.Band(new Sweep(2200f, 6000f), 3f))
        .Partials(0.23f, 1500f, 0.5f, BladeRatios, BladeAmps, new[] { 0.35f, 0.24f, 0.16f, 0.1f }, 0.003f)
        .Noise(0.3f, 0.5f, 0.5f, Env.Swell(0.4f), Filter.Band(new Sweep(600f, 2000f, 900f, 0.4f), 1.4f))
        .Reverb(0.25f)
        .Done(0.85f);
}

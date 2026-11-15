using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Turns a TrackSpec into one seamless loop of samples. It runs as a coroutine and works only a few
// milliseconds per frame, so generating a track never stalls the game.
// Every note is rendered with the Synth voices into a scratch buffer and mixed into the loop with
// wrap-around (a note ringing past the end continues at the start); the reverb runs twice around the
// loop so its tail wraps as well.
public static class MusicComposer {
    const double BudgetMs = 3.0;              // work per frame
    const float Humanize = 0.004f;            // seconds of random timing on plucked notes and drums
    static readonly float[] BellDecays = { 0.8f, 0.5f, 0.35f, 0.22f, 0.14f };

    struct Note {
        public float time, length, freq, level;
        public Instrument instrument;

        public Note(float time, float length, float freq, float level, Instrument instrument) {
            this.time = time; this.length = length; this.freq = freq; this.level = level; this.instrument = instrument;
        }
    }

    public static IEnumerator Render(TrackSpec spec, Action<float[]> done) {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        float beat = 60f / spec.bpm;
        float[] mix = new float[Mathf.Max(Synth.Rate, Mathf.RoundToInt(spec.bars * 4 * beat * Synth.Rate))];
        Synth voice = new Synth(new float[Synth.Rate * 6], spec.seed);
        System.Random rng = new System.Random(spec.seed);

        foreach (Note note in Notes(spec, beat)) {
            Play(voice, note);
            float jitter = IsPlucked(note.instrument) ? Jitter(rng) : 0f;
            MixIn(mix, note.time + jitter, voice.data, voice.Length, 1f);
            if (watch.Elapsed.TotalMilliseconds > BudgetMs) { yield return null; watch.Restart(); }
        }

        foreach (DrumLine line in spec.drums) {
            if (string.IsNullOrEmpty(line.steps)) continue;
            float[] hit = DrumSample(line.drum);
            for (int k = 0; k < spec.bars * 16; k++) {
                char c = line.steps[k % line.steps.Length];
                if (c != '.') MixIn(mix, k * 0.25f * beat + Jitter(rng), hit, hit.Length, line.level * Velocity(c));
            }
            if (watch.Elapsed.TotalMilliseconds > BudgetMs) { yield return null; watch.Restart(); }
        }

        Reverb reverb = new Reverb(0.86f, 0.35f);
        for (int pass = 0; pass < 2; pass++)                 // the first pass only fills the reverb with the loop's end
            for (int i = 0; i < mix.Length; i++) {
                float dry = mix[i];
                float wet = reverb.Process(dry);
                if (pass == 1) mix[i] = dry + wet * spec.reverb;
                if ((i & 4095) == 0 && watch.Elapsed.TotalMilliseconds > BudgetMs) { yield return null; watch.Restart(); }
            }

        float peak = 1e-6f;
        for (int i = 0; i < mix.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(mix[i]));
        yield return null;
        float gain = 0.8f / peak;
        for (int i = 0; i < mix.Length; i++) mix[i] *= gain;
        done(mix);
    }

    // Wind gusts and sea swells as overlapping noise voices, crossfaded into a 16-second loop.
    public static IEnumerator RenderAmbience(Action<float[]> done) {
        const float Seconds = 16f, Crossfade = 1.5f;
        Synth s = new Synth(Seconds + Crossfade, 777);
        for (int i = 0; i < 8; i++) {                       // gusts: two or three always overlap
            float lo = s.Range(240f, 380f);
            Sweep howl = new Sweep(lo, lo * s.Range(1.8f, 2.6f), lo * 1.2f, 0.5f);
            s.Noise(i * 2.4f - 1f, 5f, s.Range(0.3f, 0.5f), Env.Swell(s.Range(0.35f, 0.6f)), Filter.Band(howl, s.Range(1.2f, 2f)));
            yield return null;
        }
        for (int i = 0; i < 6; i++) {                       // swells washing onto the shore
            Sweep wash = new Sweep(260f, s.Range(800f, 1200f), 320f, 0.35f);
            s.Noise(i * 3f + s.Range(-0.9f, -0.1f), 4.5f, s.Range(0.6f, 0.9f), Env.Swell(0.35f), Filter.Low(wash));
            yield return null;
        }
        done(s.DoneLoop(Crossfade, 0.8f));
    }

    // ------------------------------------------------------------------ the score
    static IEnumerable<Note> Notes(TrackSpec s, float beat) {
        int beats = s.bars * 4;

        foreach (ChordPart part in s.chordParts)
            for (float b = 0f; b < beats - 1e-3f; b += Mathf.Max(0.25f, part.everyBeats)) {
                int root = ChordRoot(s, b);
                yield return new Note(b * beat, part.lengthBeats * beat, Hz(s, root, part.octave), part.level, part.instrument);
                if (!part.fifthsOnly) yield return new Note(b * beat, part.lengthBeats * beat, Hz(s, root + 2, part.octave), part.level, part.instrument);
                yield return new Note(b * beat, part.lengthBeats * beat, Hz(s, root + 4, part.octave), part.level, part.instrument);
            }

        if (!string.IsNullOrEmpty(s.bassRhythm)) {
            string r = s.bassRhythm;
            for (int step = 0; step < s.bars * 16; step++) {
                char c = r[step % r.Length];
                if (c == '.') continue;
                int next = step + 1;                       // the note lasts until the next one (at most a bar)
                while (next < step + 16 && r[next % r.Length] == '.') next++;
                float b = step * 0.25f;
                yield return new Note(b * beat, (next - step) * 0.25f * beat, Hz(s, ChordRoot(s, b), -1), s.bassLevel * Velocity(c), Instrument.Bass);
            }
        }

        if (s.arpDegrees != null && s.arpDegrees.Length > 0 && s.arpStep > 0f) {
            int i = 0;
            for (float b = 0f; b < beats - 1e-3f; b += s.arpStep, i++) {
                int degree = ChordRoot(s, b) + s.arpDegrees[i % s.arpDegrees.Length];
                float accent = Mathf.Abs(b - Mathf.Round(b)) < 0.01f ? 1f : 0.75f;   // on the beat a little louder
                yield return new Note(b * beat, s.arpStep * beat, Hz(s, degree, s.arpOctave), s.arpLevel * accent, s.arp);
            }
        }

        if (s.melody != null) {
            float b = s.melodyBar * 4f;
            for (int k = 0; k + 1 < s.melody.Length; k += 2) {
                float degree = s.melody[k], length = s.melody[k + 1];
                if (degree > MusicThemes.Rest + 1f)
                    yield return new Note(b * beat, length * beat, Hz(s, Mathf.RoundToInt(degree), s.leadOctave), s.leadLevel, s.lead);
                b += length;
            }
        }
    }

    static int ChordRoot(TrackSpec s, float beat) {
        int bar = Mathf.FloorToInt(beat / 4f);
        return s.chords.Length > 0 ? s.chords[bar % s.chords.Length] : 0;
    }

    // Frequency of a scale degree (degrees past the scale continue into the next octave).
    static float Hz(TrackSpec s, int degree, int octave) {
        int n = s.scale.Length;
        int o = Mathf.FloorToInt(degree / (float)n);
        int midi = s.root + 12 * (o + octave) + s.scale[degree - o * n];
        return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
    }

    static float Velocity(char c) { return c == 'X' ? 1f : c == 'o' ? 0.35f : 0.65f; }

    static float Jitter(System.Random rng) { return ((float)rng.NextDouble() * 2f - 1f) * Humanize; }

    // ------------------------------------------------------------------ the instruments
    static void Play(Synth s, Note n) {
        float f = n.freq, a = n.level, d = n.length;
        switch (n.instrument) {
            case Instrument.Harp: s.Begin(2f); s.Pluck(0f, f, a, 1.8f, 0.75f); break;
            case Instrument.Lute: s.Begin(1.2f); s.Pluck(0f, f, a, 1.1f, 0.6f); break;
            case Instrument.Pizz: s.Begin(0.5f); s.Pluck(0f, f, a, 0.45f, 0.85f); break;
            case Instrument.Oud:
                s.Begin(0.9f);
                s.Pluck(0f, f, a, 0.8f, 0.42f);
                s.Noise(0f, 0.02f, a * 0.3f, Env.Hit(0.0005f, 0.004f), Filter.Band(Mathf.Min(f * 4f, 8000f), 2f));   // plectrum
                break;
            case Instrument.Bell: s.Begin(3f); s.Partials(0f, f, a, SoundBank.BellRatios, SoundBank.BellAmps, BellDecays); break;
            case Instrument.Bass:
                s.Begin(d + 0.3f);
                s.TableTone(0f, d + 0.3f, f, a, Env.Hit(0.006f, Mathf.Max(0.15f, d * 0.6f)), Wavetables.Bass);
                break;
            case Instrument.Strings: Pad(s, d, f, a, Wavetables.Strings, 0.35f, 0.6f, 0.003f); break;
            case Instrument.Choir: Pad(s, d, f, a, Wavetables.Choir, 0.5f, 0.8f, 0.005f); break;
            case Instrument.Airy:
                Pad(s, d, f, a, Wavetables.Soft, 0.9f, 1f, 0.004f);
                s.Noise(0f, d + 1f, a * 0.1f, Env.Gate(0.9f, 1f), Filter.Band(f * 3f, 3f));                        // breath
                break;
            case Instrument.Horn:
                s.Begin(d + 0.35f);
                s.Brass(0f, d + 0.3f, new Sweep(f * 0.985f, f, f, 0.08f), a, 0.08f, 0.3f);                          // scoops up into the note
                break;
            case Instrument.LowBrass: s.Begin(d + 0.2f); s.Brass(0f, d + 0.15f, f, a, 0.015f, 0.15f, 0.002f); break;
            case Instrument.Flute:
                s.Begin(d + 0.15f);
                s.TableTone(0f, d + 0.12f, f, a, Env.Gate(0.05f, 0.12f), Wavetables.Soft, 5f, 0.006f);
                s.Noise(0f, d + 0.12f, a * 0.05f, Env.Gate(0.03f, 0.12f), Filter.Band(f * 2f, 2f));               // breath
                break;
        }
    }

    // Two slightly detuned voices with slow attack and release, held for the note's length.
    static void Pad(Synth s, float length, float f, float a, Wavetable table, float attack, float release, float vibrato) {
        float total = length + release;
        s.Begin(total);
        s.TableTone(0f, total, f * 0.9965f, a * 0.6f, Env.Gate(attack, release), table, 4.5f, vibrato);
        s.TableTone(0f, total, f * 1.0035f, a * 0.6f, Env.Gate(attack, release), table, 5.1f, vibrato);
    }

    static bool IsPlucked(Instrument i) {
        return i == Instrument.Harp || i == Instrument.Lute || i == Instrument.Oud || i == Instrument.Pizz || i == Instrument.Bell;
    }

    static float[] DrumSample(Drum d) {
        switch (d) {
            case Drum.Kick:
                return new Synth(0.45f, 901).Tone(0f, 0.4f, new Sweep(120f, 45f), 1f, Env.Hit(0.002f, 0.12f))
                    .Noise(0f, 0.02f, 0.3f, Env.Hit(0.0005f, 0.004f), Filter.Band(2500f, 1f)).Done();
            case Drum.Taiko:
                return new Synth(0.9f, 902).Tone(0f, 0.85f, new Sweep(95f, 55f), 1f, Env.Hit(0.003f, 0.28f))
                    .Tone(0f, 0.3f, 185f, 0.25f, Env.Hit(0.002f, 0.08f))
                    .Noise(0f, 0.25f, 0.45f, Env.Hit(0.002f, 0.05f), Filter.Low(900f)).Done();
            case Drum.Dum:
                return new Synth(0.45f, 903).Tone(0f, 0.42f, new Sweep(150f, 95f), 1f, Env.Hit(0.002f, 0.13f))
                    .Noise(0f, 0.04f, 0.35f, Env.Hit(0.0005f, 0.008f), Filter.Band(1200f, 1f)).Done();
            case Drum.Tek:
                return new Synth(0.12f, 904).Noise(0f, 0.1f, 0.8f, Env.Hit(0.0008f, 0.018f), Filter.Band(3200f, 1.5f))
                    .Tone(0f, 0.08f, 520f, 0.4f, Env.Hit(0.001f, 0.022f)).Done();
            case Drum.Snare:
                return new Synth(0.3f, 905).Noise(0f, 0.28f, 0.8f, Env.Hit(0.001f, 0.07f), Filter.Band(2000f, 0.8f))
                    .Tone(0f, 0.12f, new Sweep(220f, 180f), 0.5f, Env.Hit(0.001f, 0.04f)).Done();
            case Drum.Shaker:
                return new Synth(0.09f, 906).Noise(0f, 0.08f, 0.6f, Env.Swell(0.35f), Filter.High(6000f)).Done();
            default:   // timpani
                return new Synth(1.8f, 907).Tone(0f, 1.7f, new Sweep(76f, 72f), 1f, Env.Hit(0.004f, 0.45f))
                    .Partials(0f, 76f, 0.25f, new[] { 1.5f, 1.99f, 2.44f }, new[] { 1f, 0.7f, 0.4f }, new[] { 0.35f, 0.25f, 0.18f })
                    .Noise(0f, 0.1f, 0.35f, Env.Hit(0.002f, 0.025f), Filter.Low(500f)).Done();
        }
    }

    // Adds src[0..count) into the loop at `time`, wrapping around its end.
    static void MixIn(float[] mix, float time, float[] src, int count, float gain) {
        int length = mix.Length;
        int start = Mathf.RoundToInt(time * Synth.Rate) % length;
        if (start < 0) start += length;
        int n = Mathf.Min(count, length);
        for (int i = 0; i < n; i++) {
            int k = start + i;
            if (k >= length) k -= length;
            mix[k] += src[i] * gain;
        }
    }
}

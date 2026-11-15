using UnityEngine;

// A sound being synthesised: a float buffer (44.1 kHz mono) plus a seeded noise source.
// Voices add themselves into the buffer and calls chain:  new Synth(0.5f).Noise(...).Tone(...).Done()
// Envelope, pitch and filter cutoff are evaluated every 16 samples and interpolated in between, which keeps
// the per-sample work tiny (a few multiply-adds) so clips build in a millisecond or two.
public class Synth {
    public const int Rate = 44100;
    const int Block = 16;
    const int EndFade = 64;        // every voice ends with a 1.5 ms ramp, so a cut-off voice never clicks
    const float Dt = 1f / Rate;

    public readonly float[] data;
    public int Length { get; private set; }   // samples in use (a reused scratch buffer can be longer)
    uint state;

    public Synth(float seconds, int seed = 1) : this(new float[Mathf.Max(16, Mathf.CeilToInt(seconds * Rate))], seed) { }

    // Wraps a reusable buffer (the music renders note after note into one scratch buffer).
    public Synth(float[] buffer, int seed) {
        data = buffer;
        Length = buffer.Length;
        state = (uint)seed * 2654435761u | 1u;
    }

    public float Seconds => Length / (float)Rate;

    // Clears the first `seconds` of the buffer and limits the voices to them.
    public void Begin(float seconds) {
        Length = Mathf.Clamp(Mathf.CeilToInt(seconds * Rate), 16, data.Length);
        System.Array.Clear(data, 0, Length);
    }

    // ------------------------------------------------------------------ randomness (repeatable per sound)
    public float Rand() {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (state & 0xFFFFFF) / 16777216f;
    }

    public float Range(float a, float b) { return a + (b - a) * Rand(); }

    float White() { return Rand() * 2f - 1f; }

    // ------------------------------------------------------------------ voices
    // A pitched voice: frequency path, envelope, optional vibrato and filter.
    public Synth Tone(float start, float dur, Sweep freq, float amp, Env env, Waveform wave = Waveform.Sine,
                      float vibratoHz = 0f, float vibratoDepth = 0f, Filter filter = default) {
        return Voice(start, dur, freq, amp, env, wave, null, vibratoHz, vibratoDepth, filter);
    }

    // Like Tone, but reads a band-limited wavetable (strings, brass, choir...).
    public Synth TableTone(float start, float dur, Sweep freq, float amp, Env env, Wavetable table,
                           float vibratoHz = 0f, float vibratoDepth = 0f) {
        return Voice(start, dur, freq, amp, env, Waveform.Sine, table, vibratoHz, vibratoDepth, default);
    }

    // Brass: a dark body plus a bright layer that blooms in a little later (loud brass is brighter).
    public Synth Brass(float start, float dur, Sweep freq, float amp, float attack, float release, float vibratoDepth = 0.004f) {
        TableTone(start, dur, freq, amp, Env.Gate(attack, release), Wavetables.BrassDark, 5f, vibratoDepth);
        TableTone(start, dur, freq, amp * 0.55f, Env.Gate(attack * 2f, release * 0.7f), Wavetables.BrassBright, 5f, vibratoDepth);
        return this;
    }

    // Filtered white noise with a moving cutoff: whooshes, hisses, rumbles, crashes.
    public Synth Noise(float start, float dur, float amp, Env env, Filter filter) {
        int i0 = Index(start), n = Span(i0, dur);
        Svf svf = default;
        for (int b = 0; b < n; b += Block) {
            int m = Mathf.Min(Block, n - b);
            float t0 = b * Dt, t1 = (b + m) * Dt;
            float e = env.At(t0, dur) * EndRamp(b, n), de = (env.At(t1, dur) * EndRamp(b + m, n) - e) / m;
            float gain = 1f;
            if (filter.on) {
                float fc = filter.cutoff.At(t0 / dur);
                svf.Set(fc, filter.q);
                gain = NoiseGain(filter.pass, fc, filter.q);
            }
            for (int j = 0; j < m; j++) {
                float x = White();
                if (filter.on) x = svf.Process(x, filter.pass) * gain;
                data[i0 + b + j] += amp * e * x;
                e += de;
            }
        }
        return this;
    }

    // Inharmonic partials with their own decays: bells, metal, glass. `beat` adds a detuned twin for shimmer.
    public Synth Partials(float start, float f0, float amp, float[] ratios, float[] amps, float[] decays, float beat = 0f, float attack = 0.001f) {
        for (int p = 0; p < ratios.Length; p++) {
            float f = f0 * ratios[p];
            if (f >= Rate * 0.45f) continue;
            float dur = decays[p] * 6f;                                // about -52 dB
            Tone(start, dur, f, amp * amps[p], Env.Hit(attack, decays[p]));
            if (beat > 0f) Tone(start, dur, f * (1f + beat), amp * amps[p] * 0.5f, Env.Hit(attack, decays[p] * 0.8f));
        }
        return this;
    }

    // Random short clicks, denser near the start when bias > 1: crackling fire, grit, crumbling stone.
    public Synth Crackle(float start, float dur, int count, float amp, float freqLo, float freqHi, float bias = 1.5f, float decay = 0.004f) {
        for (int c = 0; c < count; c++) {
            float k = Mathf.Pow(Rand(), bias);
            Noise(start + k * dur, decay * 8f, amp * Range(0.25f, 1f) * (1f - 0.7f * k), Env.Hit(0.0003f, decay),
                  Filter.Band(Range(freqLo, freqHi), 1.2f));
        }
        return this;
    }

    // Short random high pings: glass shards, sparkles.
    public Synth Pings(float start, float dur, int count, float amp, float freqLo, float freqHi, float decay = 0.05f, float bias = 1.5f) {
        for (int c = 0; c < count; c++) {
            float k = Mathf.Pow(Rand(), bias), f = Range(freqLo, freqHi), a = amp * Range(0.3f, 1f) * (1f - 0.6f * k);
            Tone(start + k * dur, decay * 6f, f, a, Env.Hit(0.0005f, decay));
            if (f * 2.7f < Rate * 0.45f) Tone(start + k * dur, decay * 4f, f * 2.7f, a * 0.35f, Env.Hit(0.0005f, decay * 0.6f));
        }
        return this;
    }

    // Plucked string (Karplus-Strong): a burst of noise circulating in a tuned, damped delay line.
    // `decay` is the time to fall by 60 dB; `brightness` (0..1) colours the pluck.
    public Synth Pluck(float start, float freq, float amp, float decay, float brightness) {
        int i0 = Index(start), n = Span(i0, decay);
        if (n <= 0) return this;
        freq = Mathf.Clamp(freq, 30f, 3000f);
        // The averaging filter delays by half a sample; a first-order allpass supplies the fraction.
        float delay = Rate / freq - 0.5f;
        int len = (int)delay;
        float frac = delay - len;
        if (frac < 0.1f) { len--; frac += 1f; }
        float c = (1f - frac) / (1f + frac);
        float g = Mathf.Pow(0.001f, 1f / (Mathf.Max(0.05f, decay) * freq));   // loop gain per round trip

        float[] line = new float[len];
        float lp = 0f, mean = 0f, peak = 1e-6f, k = Mathf.Clamp(brightness, 0.05f, 1f);
        for (int i = 0; i < len; i++) { lp += (White() - lp) * k; line[i] = lp; mean += lp; }
        mean /= len;
        for (int i = 0; i < len; i++) { line[i] -= mean; peak = Mathf.Max(peak, Mathf.Abs(line[i])); }
        for (int i = 0; i < len; i++) line[i] /= peak;

        int pos = 0;
        float prev = 0f, apIn = 0f, apOut = 0f;
        for (int i = 0; i < n; i++) {
            float x = line[pos];
            float avg = 0.5f * (x + prev);
            prev = x;
            float y = c * avg + apIn - c * apOut;                       // allpass: the fractional delay
            apIn = avg;
            apOut = y;
            line[pos] = y * g;
            pos = pos + 1 == len ? 0 : pos + 1;
            data[i0 + i] += amp * x;
        }
        return this;
    }

    // ------------------------------------------------------------------ whole-buffer processing
    // Slow random loudness wobble (rolling thunder, fuse flutter): smooth value noise, `rate` points per second.
    public Synth Wobble(float rate, float depth) {
        int step = Mathf.Max(1, (int)(Rate / Mathf.Max(0.1f, rate)));
        float a = Rand(), b = Rand();
        for (int i = 0; i < Length; i++) {
            int k = i % step;
            if (k == 0 && i > 0) { a = b; b = Rand(); }
            float x = (float)k / step;
            x = x * x * (3f - 2f * x);
            data[i] *= 1f - depth * (a + (b - a) * x);
        }
        return this;
    }

    // Periodic loudness pulse (shimmering hums).
    public Synth Tremolo(float rate, float depth) {
        for (int i = 0; i < Length; i++) data[i] *= 1f - depth * (0.5f + 0.5f * Osc.Sine(Osc.Frac(rate * i * Dt)));
        return this;
    }

    // Soft clipping for weight and punch (explosions, thunder).
    public Synth Saturate(float drive) {
        Scale(1f / Peak());
        float norm = 1f / Tanh(drive);
        for (int i = 0; i < Length; i++) data[i] = Tanh(data[i] * drive) * norm;
        return this;
    }

    public Synth Reverb(float mix, float room = 0.82f, float damp = 0.3f) {
        var reverb = new global::Reverb(room, damp);
        for (int i = 0; i < Length; i++) data[i] += reverb.Process(data[i]) * mix;
        return this;
    }

    // Peak-normalises and fades the last few milliseconds so a cut tail never clicks.
    public float[] Done(float peak = 0.9f) {
        Scale(peak / Peak());
        int fade = Mathf.Min(Length, Rate / 200);
        for (int i = 0; i < fade; i++) data[Length - 1 - i] *= i / (float)fade;
        if (Length == data.Length) return data;
        float[] trimmed = new float[Length];
        System.Array.Copy(data, trimmed, Length);
        return trimmed;
    }

    // Seamless loop: the last `crossfade` seconds are blended into the start (equal power), then dropped.
    public float[] DoneLoop(float crossfade, float peak = 0.9f) {
        int x = Mathf.Clamp(Mathf.RoundToInt(crossfade * Rate), 1, Length / 2);
        int len = Length - x;
        float[] loop = new float[len];
        System.Array.Copy(data, loop, len);
        for (int i = 0; i < x; i++) {
            float k = (float)i / x;
            loop[i] = data[i] * Mathf.Sqrt(k) + data[len + i] * Mathf.Sqrt(1f - k);
        }
        float max = 1e-6f;
        for (int i = 0; i < len; i++) max = Mathf.Max(max, Mathf.Abs(loop[i]));
        for (int i = 0; i < len; i++) loop[i] *= peak / max;
        return loop;
    }

    // ------------------------------------------------------------------ internals
    Synth Voice(float start, float dur, Sweep freq, float amp, Env env, Waveform wave, Wavetable table,
                float vibHz, float vibDepth, Filter filter) {
        int i0 = Index(start), n = Span(i0, dur);
        Svf svf = default;
        float phase = 0f;
        for (int b = 0; b < n; b += Block) {
            int m = Mathf.Min(Block, n - b);
            float t0 = b * Dt, t1 = (b + m) * Dt;
            float e = env.At(t0, dur) * EndRamp(b, n), de = (env.At(t1, dur) * EndRamp(b + m, n) - e) / m;
            float f = Pitch(freq, t0, dur, vibHz, vibDepth), df = (Pitch(freq, t1, dur, vibHz, vibDepth) - f) / m;
            if (filter.on) svf.Set(filter.cutoff.At(t0 / dur), filter.q);
            for (int j = 0; j < m; j++) {
                phase += f * Dt;
                if (phase >= 1f) phase -= (int)phase;
                float x = table != null ? table.At(phase) : Osc.Shape(wave, phase);
                if (filter.on) x = svf.Process(x, filter.pass);
                data[i0 + b + j] += amp * e * x;
                e += de;
                f += df;
            }
        }
        return this;
    }

    static float EndRamp(int i, int n) { return Mathf.Clamp01((n - i) / (float)EndFade); }

    static float Pitch(Sweep freq, float t, float dur, float vibHz, float vibDepth) {
        float f = freq.At(t / dur);
        if (vibDepth > 0f) f *= 1f + vibDepth * Osc.Sine(Osc.Frac(vibHz * t));
        return f;
    }

    // Filtered noise keeps roughly the loudness of full-band noise, whatever its bandwidth.
    static float NoiseGain(FilterPass pass, float fc, float q) {
        const float Nyquist = Rate * 0.5f;
        float bandwidth = pass == FilterPass.Low ? fc : pass == FilterPass.Band ? fc / Mathf.Max(0.1f, q) : Nyquist - Mathf.Min(fc, Nyquist * 0.9f);
        return Mathf.Min(10f, Mathf.Sqrt(Nyquist / Mathf.Max(20f, bandwidth)));
    }

    int Index(float seconds) { return Mathf.Clamp((int)(seconds * Rate), 0, Length); }

    int Span(int i0, float seconds) { return Mathf.Clamp(Mathf.CeilToInt(seconds * Rate), 0, Length - i0); }

    float Peak() {
        float max = 1e-6f;
        for (int i = 0; i < Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
        return max;
    }

    void Scale(float k) { for (int i = 0; i < Length; i++) data[i] *= k; }

    static float Tanh(float x) { return (float)System.Math.Tanh(x); }
}

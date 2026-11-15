using UnityEngine;

public enum Waveform { Sine, Triangle, Saw, Square }
public enum FilterPass { Low, Band, High }

// Amplitude envelope of a voice, evaluated at time t (seconds) of a voice lasting `dur` seconds.
public readonly struct Env {
    readonly int kind;
    readonly float a, b;

    Env(int kind, float a, float b) { this.kind = kind; this.a = a; this.b = b; }

    // Linear attack, then exponential decay (time constant `decay`): hits, plucks, bells.
    public static Env Hit(float attack, float decay) { return new Env(0, Mathf.Max(1e-4f, attack), Mathf.Max(1e-4f, decay)); }

    // Smooth rise to full level at fraction `peakAt` of the duration, smooth fall to silence at the end: whooshes.
    public static Env Swell(float peakAt) { return new Env(1, Mathf.Clamp(peakAt, 0.01f, 0.99f), 0f); }

    // Attack, hold at full level, release over the last `release` seconds: sustained notes and loops.
    public static Env Gate(float attack, float release) { return new Env(2, Mathf.Max(1e-4f, attack), Mathf.Max(1e-4f, release)); }

    public static Env Flat => Gate(0.002f, 0.002f);

    public float At(float t, float dur) {
        switch (kind) {
            case 0: return t < a ? t / a : Mathf.Exp(-(t - a) / b);
            case 1: {
                float k = dur > 0f ? t / dur : 1f;
                float x = Mathf.Clamp01(k < a ? k / a : (1f - k) / (1f - a));
                return x * x * (3f - 2f * x);
            }
            default: return Mathf.Min(Mathf.Clamp01(t / a), Mathf.Clamp01((dur - t) / b));
        }
    }
}

// A frequency path over a voice: from -> peak (at fraction `mid`) -> to, interpolated exponentially
// (equal musical steps). A plain number converts to a constant frequency.
public readonly struct Sweep {
    readonly float from, peak, to, mid;

    public Sweep(float from, float to) : this(from, Mathf.Sqrt(Mathf.Abs(from * to)), to, 0.5f) { }

    public Sweep(float from, float peak, float to, float mid) {
        this.from = Mathf.Max(1f, from);
        this.peak = Mathf.Max(1f, peak);
        this.to = Mathf.Max(1f, to);
        this.mid = Mathf.Clamp(mid, 0.001f, 0.999f);
    }

    public static implicit operator Sweep(float f) { return new Sweep(f, f, f, 0.5f); }

    public float At(float k) {
        if (from == peak && peak == to) return from;
        if (k <= mid) return from * Mathf.Pow(peak / from, Mathf.Clamp01(k / mid));
        return peak * Mathf.Pow(to / peak, Mathf.Clamp01((k - mid) / (1f - mid)));
    }
}

// Optional filter on a voice (default = none).
public readonly struct Filter {
    public readonly bool on;
    public readonly FilterPass pass;
    public readonly Sweep cutoff;
    public readonly float q;

    Filter(FilterPass pass, Sweep cutoff, float q) { on = true; this.pass = pass; this.cutoff = cutoff; this.q = q; }

    public static Filter Low(Sweep cutoff, float q = 0.7f) { return new Filter(FilterPass.Low, cutoff, q); }
    public static Filter Band(Sweep cutoff, float q = 1f) { return new Filter(FilterPass.Band, cutoff, q); }
    public static Filter High(Sweep cutoff, float q = 0.7f) { return new Filter(FilterPass.High, cutoff, q); }
}

// Zero-delay-feedback state-variable filter (Simper / Cytomic): stable at any cutoff, cheap to sweep.
public struct Svf {
    float ic1, ic2, a1, a2, a3, k;

    public void Set(float cutoff, float q) {
        float g = Mathf.Tan(Mathf.PI * Mathf.Clamp(cutoff, 10f, Synth.Rate * 0.45f) / Synth.Rate);
        k = 1f / Mathf.Max(0.1f, q);
        a1 = 1f / (1f + g * (g + k));
        a2 = g * a1;
        a3 = g * a2;
    }

    // Low and high pass have unity gain; the band pass is normalised to unity gain at its centre.
    public float Process(float x, FilterPass pass) {
        float v3 = x - ic2;
        float v1 = a1 * ic1 + a2 * v3;
        float v2 = ic2 + a2 * ic1 + a3 * v3;
        ic1 = 2f * v1 - ic1;
        ic2 = 2f * v2 - ic2;
        switch (pass) {
            case FilterPass.Low: return v2;
            case FilterPass.Band: return k * v1;
            default: return x - k * v1 - v2;
        }
    }
}

// Oscillator shapes at a phase in [0, 1).
public static class Osc {
    const int TableSize = 4096;
    static readonly float[] sine = BuildSine();

    public static float Sine(float phase) {
        float x = phase * TableSize;
        int i = (int)x;
        float f = x - i;
        i &= TableSize - 1;
        return sine[i] + (sine[i + 1] - sine[i]) * f;
    }

    public static float Shape(Waveform wave, float p) {
        switch (wave) {
            case Waveform.Triangle: return 4f * Mathf.Abs(p - 0.5f) - 1f;
            case Waveform.Saw: return 2f * p - 1f;
            case Waveform.Square: return p < 0.5f ? 1f : -1f;
            default: return Sine(p);
        }
    }

    public static float Frac(float x) { return x - Mathf.Floor(x); }

    static float[] BuildSine() {
        float[] t = new float[TableSize + 1];
        for (int i = 0; i <= TableSize; i++) t[i] = Mathf.Sin(2f * Mathf.PI * i / TableSize);
        return t;
    }
}

// One cycle of a waveform built from harmonic amplitudes. Band-limited, so rich timbres don't alias.
public class Wavetable {
    const int Size = 2048;
    readonly float[] table = new float[Size + 1];

    public Wavetable(params float[] harmonics) {
        float peak = 1e-6f;
        for (int i = 0; i <= Size; i++) {
            float s = 0f;
            for (int h = 0; h < harmonics.Length; h++) s += harmonics[h] * Mathf.Sin(2f * Mathf.PI * (h + 1) * i / Size);
            table[i] = s;
            peak = Mathf.Max(peak, Mathf.Abs(s));
        }
        for (int i = 0; i <= Size; i++) table[i] /= peak;
    }

    public float At(float phase) {
        float x = phase * Size;
        int i = (int)x;
        float f = x - i;
        i &= Size - 1;
        return table[i] + (table[i + 1] - table[i]) * f;
    }
}

// Instrument timbres shared by the sound effects and the music.
public static class Wavetables {
    public static readonly Wavetable Strings = new Wavetable(1f, 0.5f, 0.33f, 0.25f, 0.2f, 0.16f, 0.13f, 0.1f, 0.08f, 0.06f);
    public static readonly Wavetable BrassDark = new Wavetable(1f, 0.55f, 0.3f, 0.16f, 0.08f, 0.04f);
    public static readonly Wavetable BrassBright = new Wavetable(0.6f, 0.75f, 0.8f, 0.7f, 0.6f, 0.48f, 0.38f, 0.3f, 0.22f, 0.16f, 0.11f, 0.07f);
    public static readonly Wavetable Choir = new Wavetable(1f, 0.6f, 0.9f, 0.5f, 0.25f, 0.12f, 0.08f, 0.05f);
    public static readonly Wavetable Soft = new Wavetable(1f, 0.12f, 0.2f, 0.05f, 0.04f);
    public static readonly Wavetable Bass = new Wavetable(1f, 0.6f, 0.35f, 0.2f, 0.1f, 0.05f);
    // Deep horn: many harmonics peaking around the 4th, so low notes still sound brassy.
    public static readonly Wavetable HornLow = new Wavetable(Harmonics(28, n => n == 1 ? 0.6f : n / (n + 3f) * Mathf.Exp(-n / 9f)));

    static float[] Harmonics(int count, System.Func<int, float> amplitude) {
        float[] h = new float[count];
        for (int n = 1; n <= count; n++) h[n - 1] = amplitude(n);
        return h;
    }
}

// Small mono Freeverb-style reverb: four damped comb filters in parallel, then two allpass diffusers.
public class Reverb {
    static readonly int[] CombSizes = { 1116, 1188, 1277, 1356 };
    static readonly int[] AllpassSizes = { 556, 441 };

    readonly float[][] combs = new float[4][];
    readonly float[] combStore = new float[4];
    readonly int[] combPos = new int[4];
    readonly float[][] allpasses = new float[2][];
    readonly int[] allpassPos = new int[2];
    readonly float feedback, damp;

    public Reverb(float room = 0.82f, float damp = 0.3f) {
        feedback = Mathf.Clamp(room, 0f, 0.97f);
        this.damp = Mathf.Clamp01(damp);
        for (int i = 0; i < 4; i++) combs[i] = new float[CombSizes[i]];
        for (int i = 0; i < 2; i++) allpasses[i] = new float[AllpassSizes[i]];
    }

    // The wet signal for one input sample.
    public float Process(float x) {
        float input = x * 0.25f, wet = 0f;
        for (int i = 0; i < 4; i++) {
            float[] buf = combs[i];
            int p = combPos[i];
            float y = buf[p];
            combStore[i] = y * (1f - damp) + combStore[i] * damp;   // damping: highs die faster
            buf[p] = input + combStore[i] * feedback;
            combPos[i] = p + 1 == buf.Length ? 0 : p + 1;
            wet += y;
        }
        for (int i = 0; i < 2; i++) {
            float[] buf = allpasses[i];
            int p = allpassPos[i];
            float b = buf[p];
            buf[p] = wet + b * 0.5f;
            wet = b - wet;
            allpassPos[i] = p + 1 == buf.Length ? 0 : p + 1;
        }
        return wet;
    }
}

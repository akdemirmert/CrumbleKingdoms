using UnityEngine;

public enum MusicTrack { Menu, Meadow, Desert, Frost, Volcano, Asgard, Ambience }
public enum Instrument { Harp, Lute, Oud, Pizz, Bell, Bass, Strings, Choir, Airy, Horn, LowBrass, Flute }
public enum Drum { Kick, Taiko, Dum, Tek, Snare, Shaker, Timpani }

// One instrument playing the bar's chord: every `everyBeats` it plays it for `lengthBeats`.
public struct ChordPart {
    public Instrument instrument;
    public float level, everyBeats, lengthBeats;
    public int octave;
    public bool fifthsOnly;                     // root and fifth only (a drone)
}

// One drum line: 16 steps per bar ('X' accent, 'x' normal, 'o' ghost, '.' rest); a longer string spans several bars.
public struct DrumLine {
    public Drum drum;
    public string steps;
    public float level;

    public DrumLine(Drum drum, string steps, float level) { this.drum = drum; this.steps = steps; this.level = level; }
}

// A theme as numbers: tempo, mode, chords, instruments and patterns. MusicComposer turns it into a loop.
public class TrackSpec {
    public float bpm = 100f;
    public int bars = 8;
    public int root = 50;                       // MIDI note of scale degree 0 (50 = D3)
    public int[] scale = Scales.Minor;
    public int[] chords = { 0 };                // chord root (scale degree) of each bar, repeating
    public ChordPart[] chordParts = new ChordPart[0];
    public string bassRhythm;                   // 16 steps per bar; the bass plays the chord root
    public float bassLevel = 0.2f;
    public Instrument arp;
    public int[] arpDegrees;                    // scale degrees above the chord root, one per step (null = no arpeggio)
    public float arpStep = 0.5f;                // beats per arpeggio note
    public float arpLevel = 0.15f;
    public int arpOctave = 1;
    public Instrument lead;
    public float[] melody;                      // pairs: scale degree (or MusicThemes.Rest), length in beats
    public int melodyBar;                       // bar where the melody starts
    public float leadLevel = 0.15f;
    public int leadOctave;
    public DrumLine[] drums = new DrumLine[0];
    public float reverb = 0.3f;
    public int seed = 1;
}

public static class Scales {
    public static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };
    public static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };
    public static readonly int[] Phrygian = { 0, 1, 3, 5, 7, 8, 10 };
    public static readonly int[] PhrygianDominant = { 0, 1, 4, 5, 7, 8, 10 };   // the "desert" scale
}

// The tracks: a calm heroic menu theme and one battle theme per arena. Loops are 23-27 seconds long.
public static class MusicThemes {
    public const float Rest = -100f;

    public static TrackSpec Get(MusicTrack track) {
        switch (track) {
            case MusicTrack.Meadow: return Meadow();
            case MusicTrack.Desert: return Desert();
            case MusicTrack.Frost: return Frost();
            case MusicTrack.Volcano: return Volcano();
            case MusicTrack.Asgard: return Asgard();
            default: return Menu();
        }
    }

    // D minor, slow: strings, a harp arpeggio and a warm horn melody over soft timpani.
    static TrackSpec Menu() => new TrackSpec {
        bpm = 72f, bars = 8, root = 50, scale = Scales.Minor, chords = new[] { 0, 5, 2, 6, 0, 5, 3, 6 },
        chordParts = new[] { new ChordPart { instrument = Instrument.Strings, level = 0.1f, everyBeats = 4f, lengthBeats = 4f } },
        bassRhythm = "X.......x.......", bassLevel = 0.2f,
        arp = Instrument.Harp, arpDegrees = new[] { 0, 2, 4, 7, 9, 7, 4, 2 }, arpStep = 0.5f, arpLevel = 0.13f,
        lead = Instrument.Horn, leadLevel = 0.2f, leadOctave = 0, melodyBar = 0,
        melody = new[] { 4f, 2f, 7f, 2f,   6f, 3f, 5f, 1f,   4f, 2f, 2f, 2f,   3f, 4f,
                         4f, 2f, 7f, 1f, 8f, 1f,   9f, 3f, 8f, 0.5f, 7f, 0.5f,   6f, 2f, 5f, 2f,   4f, 4f },
        drums = new[] { new DrumLine(Drum.Timpani, "X" + new string('.', 31), 0.35f) },
        reverb = 0.42f, seed = 101,
    };

    // Meadow: bright D major, lute arpeggios, a playful flute and light hand drums.
    static TrackSpec Meadow() => new TrackSpec {
        bpm = 108f, bars = 12, root = 50, scale = Scales.Major, chords = new[] { 0, 4, 5, 3, 0, 4, 3, 0, 1, 4, 3, 4 },
        chordParts = new[] { new ChordPart { instrument = Instrument.Strings, level = 0.07f, everyBeats = 4f, lengthBeats = 4f } },
        bassRhythm = "X...x...X...x...", bassLevel = 0.2f,
        arp = Instrument.Lute, arpDegrees = new[] { 0, 4, 7, 4, 2, 4, 7, 9 }, arpStep = 0.5f, arpLevel = 0.15f,
        lead = Instrument.Flute, leadLevel = 0.15f, leadOctave = 1, melodyBar = 4,
        melody = new[] { 4f, 1f, 2f, 1f, 0f, 1f, 2f, 1f,   1f, 1.5f, 2f, 0.5f, 4f, 2f,   3f, 1f, 4f, 1f, 5f, 1f, 4f, 1f,   2f, 3f, Rest, 1f,
                         1f, 1f, 2f, 1f, 1f, 1f, -1f, 1f,   4f, 2f, 6f, 2f,   7f, 1.5f, 6f, 0.5f, 5f, 1f, 4f, 1f,   4f, 3f, Rest, 1f },
        drums = new[] { new DrumLine(Drum.Kick, "X.......X.......", 0.45f), new DrumLine(Drum.Tek, "....x.......x.x.", 0.3f),
                        new DrumLine(Drum.Shaker, "x.x.x.x.x.x.x.x.", 0.12f) },
        reverb = 0.28f, seed = 202,
    };

    // Desert: the exotic phrygian-dominant scale on E, an oud ostinato, a drone, a ney-like flute and doumbek.
    static TrackSpec Desert() => new TrackSpec {
        bpm = 96f, bars = 10, root = 52, scale = Scales.PhrygianDominant, chords = new[] { 0, 0, 1, 0, 3, 1, 0, 0, 6, 1 },
        chordParts = new[] { new ChordPart { instrument = Instrument.Strings, level = 0.06f, everyBeats = 4f, lengthBeats = 4f, fifthsOnly = true } },
        bassRhythm = "X.....x.........", bassLevel = 0.22f,
        arp = Instrument.Oud, arpDegrees = new[] { 0, 0, 1, 0, 4, 2, 1, 0 }, arpStep = 0.5f, arpLevel = 0.17f,
        lead = Instrument.Flute, leadLevel = 0.13f, leadOctave = 1, melodyBar = 4,
        melody = new[] { 7f, 1f, 8f, 0.5f, 7f, 0.5f, 5f, 1f, 3f, 1f,   1f, 2f, 2f, 1f, 1f, 1f,   0f, 3f, Rest, 1f,
                         4f, 1f, 5f, 1f, 4f, 0.5f, 2f, 0.5f, 1f, 1f,   6f, 2f, 5f, 1f, 3f, 1f,   1f, 2f, 0f, 2f },
        drums = new[] { new DrumLine(Drum.Dum, "X.......X.......", 0.5f), new DrumLine(Drum.Tek, "..x...x.....x...", 0.35f),
                        new DrumLine(Drum.Shaker, "x.x.x.x.x.x.x.x.", 0.08f) },
        reverb = 0.32f, seed = 303,
    };

    // Frost: airy and slow, an "ooh" pad and glassy bells in a wide space.
    static TrackSpec Frost() => new TrackSpec {
        bpm = 80f, bars = 8, root = 52, scale = Scales.Minor, chords = new[] { 0, 5, 2, 6, 0, 3, 5, 6 },
        chordParts = new[] { new ChordPart { instrument = Instrument.Airy, level = 0.1f, everyBeats = 4f, lengthBeats = 4f } },
        bassRhythm = "X...............", bassLevel = 0.14f,
        arp = Instrument.Bell, arpDegrees = new[] { 7, 4, 9, 2, 11, 4, 9, 7 }, arpStep = 1f, arpLevel = 0.1f,
        drums = new[] { new DrumLine(Drum.Kick, "X" + new string('.', 31), 0.3f),
                        new DrumLine(Drum.Shaker, "........x...............x.......", 0.1f) },
        reverb = 0.5f, seed = 404,
    };

    // Volcano: darker C phrygian, pounding taiko, low brass stabs and a driving bass.
    static TrackSpec Volcano() => new TrackSpec {
        bpm = 116f, bars = 12, root = 48, scale = Scales.Phrygian, chords = new[] { 0, 0, 1, 0, 0, 5, 6, 0, 3, 1, 6, 0 },
        chordParts = new[] {
            new ChordPart { instrument = Instrument.Strings, level = 0.07f, everyBeats = 4f, lengthBeats = 4f },
            new ChordPart { instrument = Instrument.LowBrass, level = 0.13f, everyBeats = 4f, lengthBeats = 0.75f },
        },
        bassRhythm = "X.x.x.x.X.x.x.x.", bassLevel = 0.2f,
        arp = Instrument.Pizz, arpDegrees = new[] { 0, 0, 1, 0 }, arpStep = 0.5f, arpLevel = 0.08f, arpOctave = 0,
        lead = Instrument.Horn, leadLevel = 0.16f, leadOctave = 0, melodyBar = 8,
        melody = new[] { 3f, 2f, 4f, 1f, 3f, 1f,   1f, 3f, 0f, 1f,   -1f, 2f, 1f, 2f,   0f, 4f },
        drums = new[] { new DrumLine(Drum.Taiko, "X..X..X.X.X.X...", 0.55f), new DrumLine(Drum.Tek, "..x...x...x...x.", 0.18f) },
        reverb = 0.3f, seed = 505,
    };

    // Asgard: epic D minor, a choir and a brass section, taiko and snare, a heroic horn line.
    static TrackSpec Asgard() => new TrackSpec {
        bpm = 92f, bars = 10, root = 50, scale = Scales.Minor, chords = new[] { 0, 5, 2, 6, 0, 3, 5, 6, 5, 6 },
        chordParts = new[] {
            new ChordPart { instrument = Instrument.Choir, level = 0.09f, everyBeats = 4f, lengthBeats = 4f },
            new ChordPart { instrument = Instrument.Horn, level = 0.07f, everyBeats = 2f, lengthBeats = 1.7f },
        },
        bassRhythm = "X.......X...X...", bassLevel = 0.22f,
        arp = Instrument.Pizz, arpDegrees = new[] { 0, 4, 7, 4 }, arpStep = 0.5f, arpLevel = 0.1f,
        lead = Instrument.Horn, leadLevel = 0.2f, leadOctave = 0, melodyBar = 4,
        melody = new[] { 0f, 1f, 4f, 2f, 3f, 1f,   5f, 2f, 4f, 1f, 3f, 1f,   2f, 1.5f, 3f, 0.5f, 4f, 2f,
                         6f, 3f, 4f, 1f,   5f, 2f, 7f, 2f,   6f, 3f, Rest, 1f },
        drums = new[] { new DrumLine(Drum.Taiko, "X.......X.x.....", 0.55f), new DrumLine(Drum.Snare, "....x.......x...", 0.22f),
                        new DrumLine(Drum.Timpani, "X" + new string('.', 63), 0.3f) },
        reverb = 0.42f, seed = 606,
    };
}

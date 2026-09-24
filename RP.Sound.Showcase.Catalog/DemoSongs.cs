using RP.Sound.IO;

namespace RP.Sound.Showcase;

/// <summary>The two songs the file-format demos author in code, encode, decode and then perform.</summary>
internal static class DemoSongs
{
    // A four-bar I–vi–IV–V loop in C: marimba melody, organ pads, bass and the GM drum kit —
    // enough channels and programs to hear the General MIDI mapping at work.
    public static MidiSequence Midi(double bpm)
    {
        double beat = 60 / bpm;
        var notes = new List<MidiNote>();
        int[][] chords = { new[] { 60, 64, 67 }, new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 } };

        for (int bar = 0; bar < 4; bar++)
        {
            double barStart = bar * 4 * beat;
            int[] chord = chords[bar];

            // Organ pad: the chord held for the bar.
            foreach (int key in chord)
                notes.Add(new MidiNote(barStart, 4 * beat, key, velocity: 52, channel: 2, program: 18));

            // Bass: the root two octaves down, on 1 and 3.
            notes.Add(new MidiNote(barStart, 1.8 * beat, chord[0] - 24, velocity: 96, channel: 1, program: 33));
            notes.Add(new MidiNote(barStart + 2 * beat, 1.8 * beat, chord[0] - 24, velocity: 88, channel: 1, program: 33));

            // Marimba melody: chord tones arpeggiated in eighths, up then down.
            int[] arp = { 0, 1, 2, 1, 0, 2, 1, 0 };
            for (int n = 0; n < 8; n++)
                notes.Add(new MidiNote(barStart + n * 0.5 * beat, 0.45 * beat, chord[arp[n]] + 12, velocity: 76, channel: 0, program: 12));

            // GM percussion on channel 9: kick 1 & 3, snare 2 & 4, closed hats in eighths.
            for (int beatIndex = 0; beatIndex < 4; beatIndex++)
            {
                double onBeat = barStart + beatIndex * beat;
                notes.Add(new MidiNote(onBeat, 0.1, beatIndex % 2 == 0 ? 36 : 38, velocity: beatIndex % 2 == 0 ? 110 : 100, channel: 9));
                notes.Add(new MidiNote(onBeat, 0.1, 42, velocity: 60, channel: 9));
                notes.Add(new MidiNote(onBeat + 0.5 * beat, 0.1, 42, velocity: 45, channel: 9));
            }
        }

        notes.Add(new MidiNote(0, 0.3, 49, velocity: 90, channel: 9)); // opening crash
        return new MidiSequence(notes, bpm);
    }

    // A one-pattern module in A minor with four hand-built samples — square lead (arpeggio and
    // vibrato in the effect column), triangle bass, a noise hat and a swept-sine kick.
    public static ModModule Mod(int speed)
    {
        // Sample 1: a 32-byte square loop — Paula plays it at (clock / 2·period) / 32 Hz.
        var square = new sbyte[32];
        for (int i = 0; i < 32; i++) square[i] = i < 16 ? (sbyte)90 : (sbyte)-90;

        // Sample 2: a 64-byte triangle loop, mellower for the bass.
        var triangle = new sbyte[64];
        for (int i = 0; i < 64; i++) triangle[i] = (sbyte)(i < 32 ? -100 + i * 200 / 32 : 100 - (i - 32) * 200 / 32);

        // Sample 3: a short decaying noise burst (deterministic LCG — no seeds in file data).
        var noise = new sbyte[64];
        uint lcg = 0x12345678;
        for (int i = 0; i < 64; i++)
        {
            lcg = lcg * 1664525 + 1013904223;
            noise[i] = (sbyte)((int)(lcg >> 24) * (64 - i) / 64 / 2);
        }

        // Sample 4: a swept sine — the classic tracker kick, pitch drop baked into the recording.
        var kick = new sbyte[300];
        double phase = 0;
        for (int i = 0; i < 300; i++)
        {
            phase += 0.35 * (1 - 0.8 * i / 300.0);
            kick[i] = (sbyte)(110 * Math.Sin(phase) * (1 - (double)i / 300));
        }

        var cells = new ModCell[ModPattern.Rows, 4];
        cells[0, 3] = ModCell.Create(effect: 0xF, argument: speed);

        // Periods from ProTracker's table: A-1 508, F-1 640, C-2 428, G-1 570 — the Am–F–C–G loop.
        int[] bassPeriods = { 508, 640, 428, 570 };
        int[] leadRows = { 0, 4, 8, 12, 16, 20, 24, 28 };
        int[] leadPeriods = { 254, 285, 320, 285, 254, 226, 254, 320 }; // A-2 G-2 F-2 … the pentatonic ride

        for (int quarter = 0; quarter < 4; quarter++)
        {
            int start = quarter * 16;
            for (int r = 0; r < 16; r += 8)
            {
                cells[start + r, 0] = ModCell.Create(sampleNumber: 4, period: 428);                      // kick
                cells[start + r + 4, 0] = ModCell.Create(sampleNumber: 3, period: 428);                  // hat offbeat
            }

            cells[start, 1] = ModCell.Create(sampleNumber: 2, period: bassPeriods[quarter]);             // bass root
            cells[start + 8, 1] = ModCell.Create(sampleNumber: 2, period: bassPeriods[quarter], effect: 0xA, argument: 0x02); // fading repeat

            // The lead: minor-chord arpeggio (0x37 = +3, +7 semitones) answered by plain notes with vibrato.
            cells[start, 2] = ModCell.Create(sampleNumber: 1, period: leadPeriods[quarter * 2], effect: 0x0, argument: 0x37);
            cells[start + 4, 2] = ModCell.Create(sampleNumber: 1, period: leadPeriods[quarter * 2 + 1], effect: 0x4, argument: 0x52);
        }

        var samples = new[]
        {
            new ModSample("square lead", square, volume: 40, loopStart: 0, loopLength: 32),
            new ModSample("triangle bass", triangle, volume: 64, loopStart: 0, loopLength: 64),
            new ModSample("noise hat", noise, volume: 44),
            new ModSample("swept kick", kick, volume: 64),
        };

        return new ModModule("rp.sound demo", samples, new[] { new ModPattern(cells) }, new[] { 0 });
    }
}

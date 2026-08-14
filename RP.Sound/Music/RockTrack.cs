using RP.Sound.Effects;
using RP.Sound.Instruments;

namespace RP.Sound.Music;

/// <summary>
/// A rock backing track from the genre's corpus-verified conventions:
/// <list type="bullet">
/// <item><b>Straight 4/4 with a backbeat</b>: snare on 2 and 4 over kick on 1 and 3 — the
/// defining rhythmic marker of rock since the mid-1960s (Moore, Rock: The Primary Text, 2001;
/// Everett, The Foundations of Rock, 2009) — with eighth-note hi-hats underneath.</item>
/// <item><b>Harmony favouring IV and ♭VII</b>: corpus analysis of 200 charting rock songs finds
/// IV the most common chord after I and the Mixolydian ♭VII far more common than in classical
/// practice (de Clercq &amp; Temperley, "A corpus analysis of rock harmony", Popular Music 30(1),
/// 2011) — so the default progression is two four-bar phrases, I–♭VII–IV–I and I–♭VII–IV–V.</item>
/// <item><b>Distorted power chords</b>: root + fifth (+ octave), the third omitted because
/// distortion's intermodulation keeps the fifth's simple 3:2 ratio harmonic where a third would
/// turn to mud (Walser, Running with the Devil, 1993) — chugging in eighths.</item>
/// <item><b>Pentatonic riffing</b>: the melodic hook drawn from the minor pentatonic, the scale
/// rock melody leans on (Temperley, The Musical Language of Rock, 2018).</item>
/// <item><b>Eight-bar phrase architecture</b>: sections built in 4- and 8-bar units (Covach,
/// "Form in Rock Music: A Primer", 2005), with a crash on phrase downbeats and a tom fill
/// leading each phrase back around.</item>
/// </list>
/// Tempo defaults to 120 BPM, the centre of rock's 110–140 cluster.
/// </summary>
public sealed class RockTrack : ISound
{
    /// <summary>Chord root offsets for the 8-bar loop: I–♭VII–IV–I, I–♭VII–IV–V.</summary>
    private static readonly int[] BarChords = { 0, 10, 5, 0, 0, 10, 5, 7 };

    public Frequency Root { get; }
    public int Bars { get; }
    public double Bpm { get; }
    public Level Level { get; }

    public Groove Groove => Groove.Straight(Bpm);

    /// <summary>The loopable length: exactly the bars, no ring-out.</summary>
    public double LoopDuration => Bars * Groove.BarSeconds;

    /// <summary>The loop plus a ring-out for the last crash and chord.</summary>
    public double Duration => LoopDuration + 2;

    public RockTrack(Frequency? root = null, int bars = 8, double bpm = 120, Level? level = null)
    {
        this.Root = root ?? Frequency.FromNote("E2");
        if (this.Root.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(root), root, "The key must have a positive root pitch.");
        if (bars < 1)
            throw new ArgumentOutOfRangeException(nameof(bars), bars, "A track needs at least one bar.");
        if (bpm is < 90 or > 160)
            throw new ArgumentOutOfRangeException(nameof(bpm), bpm, "Rock tempo clusters at 110–140 BPM; this generator accepts 90–160.");
        this.Bars = bars;
        this.Bpm = bpm;
        this.Level = level ?? Level.Unity;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        Groove groove = Groove;
        DeterministicRandom random = context.CreateRandom($"rock:{Root.Hertz:0.###}:{Bpm:0.###}:{Bars}");
        Scale pentatonic = Scale.MinorPentatonic(Root.Transposed(12));
        var events = new List<(double, ISound)>();

        double eighth = groove.SecondsPerBeat * 0.5;

        // The riff: a fixed 2-bar pentatonic hook, chosen once per track from the seed, repeated —
        // repetition is what makes it a hook rather than noodling.
        var riffDegrees = new int[4];
        for (int n = 0; n < riffDegrees.Length; n++) riffDegrees[n] = (int)(random.NextDouble() * 5);

        for (int bar = 0; bar < Bars; bar++)
        {
            int chord = BarChords[bar % BarChords.Length];
            bool lastBarOfPhrase = bar % 4 == 3;

            for (int beat = 0; beat < 4; beat++)
            {
                double onBeat = groove.TimeOf(bar, beat);
                double offBeat = groove.TimeOf(bar, beat + 0.5);

                // Drums: backbeat. The occasional extra kick on an offbeat is rock's most common
                // syncopation; everything else stays planted.
                if (beat is 0 or 2) events.Add((onBeat, new KickDrum(punch: 0.7, level: Level.FromDecibels(-7))));
                if (beat == 2 && random.NextChance(0.3)) events.Add((offBeat, new KickDrum(punch: 0.7, level: Level.FromDecibels(-10))));
                if (beat is 1 or 3) events.Add((onBeat, new SnareDrum(snappy: 0.75, level: Level.FromDecibels(-7))));
                events.Add((onBeat, HiHat.Closed(Level.FromDecibels(-15))));
                events.Add((offBeat, HiHat.Closed(Level.FromDecibels(-17))));

                // Guitar: power-chord chug on every eighth, distorted.
                foreach (double time in new[] { onBeat, offBeat })
                {
                    events.Add((time, Guitar.PowerChord(
                            Root.Transposed(chord),
                            duration: eighth * 1.6,
                            damping: 0.4,
                            level: Level.FromDecibels(-16))
                        .Distorted(5, Level.FromDecibels(-4))));
                }

                // Bass: roots in eighths, an octave below the guitar.
                foreach (double time in new[] { onBeat, offBeat })
                {
                    events.Add((time, new BassGuitar(
                        Root.Transposed(chord - 12),
                        duration: eighth * 0.95,
                        tone: 0.5,
                        level: Level.FromDecibels(-6))));
                }
            }

            // The pentatonic hook rides over the first half of every second bar.
            if (bar % 2 == 0)
            {
                for (int n = 0; n < riffDegrees.Length; n++)
                {
                    events.Add((groove.TimeOf(bar, n * 0.5), new Guitar(
                            pentatonic.Degree(riffDegrees[n]),
                            duration: eighth * 2,
                            damping: 0.15,
                            level: Level.FromDecibels(-14))
                        .Distorted(3, Level.FromDecibels(-8))));
                }
            }

            // Phrase punctuation: crash on the downbeat that opens each 4-bar phrase, tom fill
            // closing it.
            if (bar % 4 == 0)
                events.Add((groove.TimeOf(bar, 0), new Cymbal(level: Level.FromDecibels(-14))));

            if (lastBarOfPhrase)
            {
                double[] fillBeats = { 3.0, 3.25, 3.5, 3.75 };
                Frequency[] fillPitches = { 180, 150, 120, 96 };
                for (int n = 0; n < fillBeats.Length; n++)
                    events.Add((groove.TimeOf(bar, fillBeats[n]), new TomDrum(fillPitches[n], level: Level.FromDecibels(-9))));
            }
        }

        return new Timeline(events).Render(context, duration).NormalizedOrDefault(new Level(0.9));
    }
}

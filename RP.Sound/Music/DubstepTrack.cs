using RP.Sound.Effects;
using RP.Sound.Instruments;
using RP.Sound.Synthesis;

namespace RP.Sound.Music;

/// <summary>
/// A dubstep track from the genre's defining, quantifiable conventions (Snoman, Dance Music
/// Manual, 3rd ed. 2013, dubstep chapter, unless noted):
/// <list type="bullet">
/// <item><b>140 BPM</b> — the genre's universally cited home tempo (accepted band 138–142).</item>
/// <item><b>The half-time feel</b>: 4/4 at 140 but with kick on beat 1 and the snare on beat 3
/// <em>only</em> — not 2-and-4 — so the perceived pulse is ~70 BPM. Snare-on-3 is the genre's
/// rhythmic fingerprint.</item>
/// <item><b>The wobble bass</b>: a harmonically rich oscillator pair through a resonant low-pass
/// whose cutoff is driven by a tempo-synced LFO at note-division rates — 1/4, 1/8, 1/16 —
/// re-chosen per bar so the bass "talks" (see <see cref="SynthPatch.Wobble"/>).</item>
/// <item><b>The sub</b>: a clean sine an octave below the wobble, carrying the root
/// unprocessed for weight.</item>
/// <item><b>Minor and sparse</b>: harmony is a static minor-pentatonic riff, not a progression;
/// percussion beyond the core hits is sparse 1/16 hats.</item>
/// <item><b>Build → drop</b>: tension bars (riser, hats, no kick) resolving into the full-weight
/// drop — the EDM structural climax, here in miniature so short renders still tell the story.</item>
/// </list>
/// </summary>
public sealed class DubstepTrack : ISound
{
    public Frequency Root { get; }
    public int Bars { get; }
    public double Bpm { get; }
    public Level Level { get; }

    /// <summary>How many opening bars are the build; the drop lands on the next bar.</summary>
    public int BuildBars { get; }

    public Groove Groove => Groove.Straight(Bpm);

    public double LoopDuration => Bars * Groove.BarSeconds;
    public double Duration => LoopDuration + 1.5;

    public DubstepTrack(Frequency? root = null, int bars = 8, double bpm = 140, int buildBars = 2, Level? level = null)
    {
        this.Root = root ?? Frequency.FromNote("A1");
        if (this.Root.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(root), root, "The key must have a positive root pitch.");
        if (bars < 1)
            throw new ArgumentOutOfRangeException(nameof(bars), bars, "A track needs at least one bar.");
        if (bpm is < 135 or > 145)
            throw new ArgumentOutOfRangeException(nameof(bpm), bpm, "Dubstep lives at ~140 BPM (accepted band 138–142); this generator allows 135–145.");
        if (buildBars < 0 || buildBars >= bars)
            throw new ArgumentOutOfRangeException(nameof(buildBars), buildBars, "The build must be shorter than the whole track.");
        this.Bars = bars;
        this.Bpm = bpm;
        this.BuildBars = buildBars;
        this.Level = level ?? Level.Unity;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        Groove groove = Groove;
        DeterministicRandom random = context.CreateRandom($"dubstep:{Root.Hertz:0.###}:{Bpm:0.###}:{Bars}");
        var events = new List<(double, ISound)>();

        // Wobble LFO rates as note divisions of the tempo: 1/4 note = one cycle per beat.
        double beatHertz = Bpm / 60.0;
        double[] wobbleRates = { beatHertz, beatHertz * 2, beatHertz * 4 };

        // The riff: two notes per bar from the low minor pentatonic, mostly the root — dubstep
        // harmony is a static riff, not a progression.
        int[] riffChoices = { 0, 0, 0, 3, 10 };

        var snareReverb = new Reverb(0.5, 0.5, Level.FromDecibels(-8));

        for (int bar = 0; bar < Bars; bar++)
        {
            bool build = bar < BuildBars;
            double halfBar = groove.SecondsPerBeat * 2;

            // Sparse 1/16 hats run through build and drop alike.
            for (double sixteenth = 0; sixteenth < 4; sixteenth += 0.25)
            {
                bool onBeat = sixteenth % 1 == 0;
                if (random.NextChance(onBeat ? 0.6 : 0.3))
                    events.Add((groove.TimeOf(bar, sixteenth), HiHat.Closed(Level.FromDecibels(build ? -16 : -18))));
            }

            if (build)
            {
                // The build: riser and rolling snares tightening toward the drop, no kick, sub only.
                if (bar == 0)
                    events.Add((groove.TimeOf(bar, 0), new Riser(BuildBars * groove.BarSeconds, intensity: 0.8, level: Level.FromDecibels(-12))));
                if (bar == BuildBars - 1)
                {
                    for (int n = 0; n < 8; n++)
                        events.Add((groove.TimeOf(bar, n * 0.5), new SnareDrum(snappy: 0.9, decay: 0.12, level: Level.FromDecibels(-14 + n))));
                }

                events.Add((groove.TimeOf(bar, 0), Sub(0, groove.BarSeconds, Level.FromDecibels(-8))));
                continue;
            }

            // --- The drop ---
            events.Add((groove.TimeOf(bar, 0), new KickDrum(new Frequency(45), punch: 0.8, decay: 0.5, level: Level.FromDecibels(-4))));
            if (random.NextChance(0.35))
                events.Add((groove.TimeOf(bar, 3.5), new KickDrum(new Frequency(45), punch: 0.6, decay: 0.3, level: Level.FromDecibels(-9))));

            // The fingerprint: snare on beat 3 (index 2), big with a reverb tail.
            events.Add((groove.TimeOf(bar, 2), snareReverb.Apply(
                new SnareDrum(new Frequency(200), snappy: 0.9, decay: 0.35, level: Level.FromDecibels(-5)))));

            // Wobble + sub, two half-bar notes, the LFO rate re-rolled each bar.
            double rate = wobbleRates[(int)(random.NextDouble() * wobbleRates.Length)];
            for (int half = 0; half < 2; half++)
            {
                int note = riffChoices[(int)(random.NextDouble() * riffChoices.Length)];
                double at = groove.TimeOf(bar, half * 2);
                events.Add((at, new Synthesizer(
                    SynthPatch.Wobble(new Frequency(rate)),
                    Root.Transposed(note + 12),
                    duration: halfBar,
                    level: Level.FromDecibels(-7))));
                events.Add((at, Sub(note, halfBar, Level.FromDecibels(-6))));
            }
        }

        return new Timeline(events).Render(context, duration).NormalizedOrDefault(new Level(0.9));
    }

    // The sub: a pure sine on the root register, faded at the edges so note joins never click.
    private ISound Sub(int semitones, double noteSeconds, Level level) =>
        new Oscillator(Waveform.Sine, Root.Transposed(semitones), noteSeconds, level)
            .Shaped(Envelope.Sustained(0.01, 0.05));
}

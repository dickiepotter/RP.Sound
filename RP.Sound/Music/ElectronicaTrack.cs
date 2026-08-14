using RP.Sound.Effects;
using RP.Sound.Instruments;

namespace RP.Sound.Music;

/// <summary>
/// A downtempo electronica track, drawing on the two documented traditions the term covers:
/// <list type="bullet">
/// <item><b>Downtempo/trip-hop groove</b> (Snoman, Dance Music Manual, chill-out chapter):
/// ~60–110 BPM, heavily swung, kick on 1 with the snare landing on 2 and 4 <em>late</em> —
/// deliberately 10–30 ms behind the grid, the "laid-back" placement — under dusty, low-passed
/// drum timbres.</item>
/// <item><b>Ambient layering</b> (Brian Eno, liner notes to Ambient 1: Music for Airports, 1978):
/// slow harmonic rhythm (one chord per two bars, extended voicings), long-attack pads, and
/// Eno's structural device of <b>incommensurate loops</b> — melodic fragments repeating at
/// mutually prime intervals (here 7, 11 and 13 beats), so their combinations keep shifting and
/// the texture never exactly repeats within a render, yet remains fully deterministic.</item>
/// </list>
/// Harmony is a static minor loop (i9 – VImaj7) rather than a progression, with long reverb on
/// the melodic loops — the "wash" the style is named for.
/// </summary>
public sealed class ElectronicaTrack : ISound
{
    public Frequency Root { get; }
    public int Bars { get; }
    public double Bpm { get; }
    public Level Level { get; }

    /// <summary>How far behind the grid the snare sits, seconds: the laid-back placement.</summary>
    public const double SnareLateness = 0.02;

    public Groove Groove => new(Bpm, swing: 0.6);

    public double LoopDuration => Bars * Groove.BarSeconds;
    public double Duration => LoopDuration + 2.5;

    public ElectronicaTrack(Frequency? root = null, int bars = 8, double bpm = 85, Level? level = null)
    {
        this.Root = root ?? Frequency.FromNote("A2");
        if (this.Root.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(root), root, "The key must have a positive root pitch.");
        if (bars < 1)
            throw new ArgumentOutOfRangeException(nameof(bars), bars, "A track needs at least one bar.");
        if (bpm is < 60 or > 110)
            throw new ArgumentOutOfRangeException(nameof(bpm), bpm, "Downtempo electronica spans roughly 60–110 BPM.");
        this.Bars = bars;
        this.Bpm = bpm;
        this.Level = level ?? Level.Unity;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        Groove groove = Groove;
        DeterministicRandom random = context.CreateRandom($"electronica:{Root.Hertz:0.###}:{Bpm:0.###}:{Bars}");
        var events = new List<(double, ISound)>();

        // The chord loop: i9 and VImaj7, one chord per two bars — slow harmonic rhythm.
        var loop = new (int RootOffset, int[] Chord)[]
        {
            (0, new[] { 0, 3, 7, 10, 14 }),  // minor 9th
            (-4, new[] { 0, 4, 7, 11 }),     // major 7th
        };

        for (int bar = 0; bar < Bars; bar++)
        {
            (int chordRoot, int[] chord) = loop[bar / 2 % loop.Length];

            // Pads: one voicing sustained across each two-bar chord.
            if (bar % 2 == 0)
            {
                var notes = new Frequency[chord.Length];
                for (int i = 0; i < chord.Length; i++) notes[i] = Root.Transposed(chordRoot + chord[i] + 12);
                events.Add((groove.TimeOf(bar, 0), SynthPad.Chord(notes, duration: 2 * groove.BarSeconds, level: Level.FromDecibels(-9))));
            }

            // Bass: one long, dark root note per bar.
            events.Add((groove.TimeOf(bar, 0), new BassGuitar(
                Root.Transposed(chordRoot - 12),
                duration: groove.BarSeconds * 0.95,
                tone: 0.2,
                level: Level.FromDecibels(-7))));

            // The dusty trip-hop kit: kick on 1 (and sometimes the "and of 3"), snare late on 2
            // and 4, sparse swung hats — all low-passed into the background.
            events.Add((groove.TimeOf(bar, 0), new KickDrum(punch: 0.4, decay: 0.4, level: Level.FromDecibels(-9))));
            if (random.NextChance(0.6))
                events.Add((groove.TimeOf(bar, 2.5), new KickDrum(punch: 0.4, decay: 0.3, level: Level.FromDecibels(-12))));
            foreach (int beat in new[] { 1, 3 })
            {
                events.Add((groove.TimeOf(bar, beat) + SnareLateness,
                    new SnareDrum(new Frequency(180), snappy: 0.5, decay: 0.3, level: Level.FromDecibels(-12)).LowPassed(3500)));
            }

            for (double e = 0.5; e < 4; e += 1)
            {
                if (random.NextChance(0.5))
                    events.Add((groove.TimeOf(bar, e), HiHat.Closed(Level.FromDecibels(-22))));
            }
        }

        // Eno's incommensurate loops: three melodic fragments repeating every 7, 11 and 13 beats.
        // The periods are mutually prime, so the pattern of coincidences only repeats after
        // 7×11×13 = 1001 beats — far longer than any render, hence "never the same twice".
        var loops = new (double PeriodBeats, ISound Voice)[]
        {
            (7, Reverb.Hall.Apply(Mallet.Glockenspiel(Root.Transposed(31), Level.FromDecibels(-17)))),
            (11, Reverb.Hall.Apply(new Flute(Root.Transposed(27), duration: 1.8, breathiness: 0.5, level: Level.FromDecibels(-16)))),
            (13, Reverb.Hall.Apply(Mallet.Marimba(Root.Transposed(19), Level.FromDecibels(-13)))),
        };
        double totalBeats = Bars * groove.BeatsPerBar;
        foreach ((double period, ISound voice) in loops)
        {
            for (double beat = 0; beat < totalBeats; beat += period)
                events.Add((groove.TimeOf((int)(beat / groove.BeatsPerBar), beat % groove.BeatsPerBar), voice));
        }

        return new Timeline(events).Render(context, duration).NormalizedOrDefault(new Level(0.9));
    }
}

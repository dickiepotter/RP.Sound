using RP.Sound.Instruments;
using RP.Sound.Synthesis;

namespace RP.Sound.Music;

/// <summary>
/// A house track from the genre's defining conventions (Snoman, Dance Music Manual, house
/// chapter; Butler, Unlocking the Groove, Indiana UP 2006, on the rhythmic design):
/// <list type="bullet">
/// <item><b>~124 BPM</b>: house's classic centre inside its 120–130 band.</item>
/// <item><b>Four-on-the-floor</b>: the kick on every quarter note — the genre's ground truth
/// (Butler 2006).</item>
/// <item><b>Open hi-hat on every offbeat eighth</b> — the second definitional layer, pumping
/// against the kick — with lightly swung sixteenth closed hats between (production convention
/// 54–62% swing on 16ths, the Linn/MPC lineage).</item>
/// <item><b>Clap on 2 and 4</b> layered over the floor.</item>
/// <item><b>Offbeat bass</b>: the bassline sounding between the kicks, not on them, so kick and
/// bass pump in alternation.</item>
/// <item><b>Loop harmony with extended chords</b>: a static two-chord minor loop (i7–VImaj7)
/// voiced as sevenths, stabbed off the beat — classic-house piano/organ language (Snoman; Tagg,
/// Everyday Tonality II, on aeolian loops).</item>
/// </list>
/// </summary>
public sealed class HouseTrack : ISound
{
    public Frequency Root { get; }
    public int Bars { get; }
    public double Bpm { get; }
    public Level Level { get; }

    /// <summary>Sixteenths swing at 58% — the middle of the house convention — while the eighth-grid offbeats stay planted.</summary>
    public Groove Groove => new(Bpm, swing: 0.58, swingUnit: 0.25);

    public double LoopDuration => Bars * Groove.BarSeconds;
    public double Duration => LoopDuration + 1;

    public HouseTrack(Frequency? root = null, int bars = 8, double bpm = 124, Level? level = null)
    {
        this.Root = root ?? Frequency.FromNote("A2");
        if (this.Root.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(root), root, "The key must have a positive root pitch.");
        if (bars < 1)
            throw new ArgumentOutOfRangeException(nameof(bars), bars, "A track needs at least one bar.");
        if (bpm is < 118 or > 130)
            throw new ArgumentOutOfRangeException(nameof(bpm), bpm, "House tempo is 120–130 BPM (classic centre ~124); this generator allows 118–130.");
        this.Bars = bars;
        this.Bpm = bpm;
        this.Level = level ?? Level.Unity;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        Groove groove = Groove;
        DeterministicRandom random = context.CreateRandom($"house:{Root.Hertz:0.###}:{Bpm:0.###}:{Bars}");
        var events = new List<(double, ISound)>();

        double eighth = groove.SecondsPerBeat * 0.5;

        // The two-chord loop, two bars each: i7 on the root, VImaj7 a major third below.
        // (In A minor: Am7 and Fmaj7.)
        var loop = new (int RootOffset, int[] Chord)[]
        {
            (0, new[] { 0, 3, 7, 10 }),   // minor 7th
            (-4, new[] { 0, 4, 7, 11 }),  // major 7th
        };

        for (int bar = 0; bar < Bars; bar++)
        {
            (int chordRoot, int[] chord) = loop[bar / 2 % loop.Length];

            for (int beat = 0; beat < 4; beat++)
            {
                double onBeat = groove.TimeOf(bar, beat);
                double offBeat = groove.TimeOf(bar, beat + 0.5);

                // The floor: kick every beat; clap layered on 2 and 4; open hat on every "and".
                events.Add((onBeat, new KickDrum(punch: 0.75, decay: 0.35, level: Level.FromDecibels(-5))));
                if (beat is 1 or 3)
                    events.Add((onBeat, new SnareDrum(new Frequency(220), snappy: 1.0, decay: 0.15, level: Level.FromDecibels(-10))));
                events.Add((offBeat, new HiHat(open: true, decay: 0.22, level: Level.FromDecibels(-14))));

                // Swung sixteenth closed hats in the gaps (the .25 and .75 positions swing;
                // the eighth grid does not — see Groove).
                foreach (double s in new[] { 0.25, 0.75 })
                {
                    if (random.NextChance(0.6))
                        events.Add((groove.TimeOf(bar, beat + s), HiHat.Closed(Level.FromDecibels(-21))));
                }

                // The offbeat bass: between the kicks, mostly the root, sometimes the octave.
                int octave = random.NextChance(0.3) ? 12 : 0;
                events.Add((offBeat, new Synthesizer(
                    SynthPatch.Bass,
                    Root.Transposed(chordRoot + octave - 12),
                    duration: eighth * 0.9,
                    level: Level.FromDecibels(-6))));
            }

            // Organ stabs off the beat — the classic-house chord hit, short and percussive.
            foreach (double stabBeat in new[] { 1.5, 3.5 })
            {
                if (!random.NextChance(0.8)) continue;
                foreach (int tone in chord)
                {
                    events.Add((groove.TimeOf(bar, stabBeat), new Organ(
                        Root.Transposed(chordRoot + tone + 12),
                        duration: 0.18,
                        registration: "888800000",
                        level: Level.FromDecibels(-19))));
                }
            }
        }

        return new Timeline(events).Render(context, duration).NormalizedOrDefault(new Level(0.9));
    }
}

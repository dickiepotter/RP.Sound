using RP.Sound.Instruments;

namespace RP.Sound.Music;

/// <summary>
/// A blues backing track built from the genre's defining, citable conventions rather than taste:
/// <list type="bullet">
/// <item>The <b>12-bar form</b>: I–I–I–I / IV–IV–I–I / V–IV–I–I, every chord a dominant 7th, with
/// a V turnaround closing bar 12 — the canonical chorus (Open Music Theory, "Blues Harmony").</item>
/// <item>The <b>shuffle</b>: swung eighths at the nominal 2:1 triplet split
/// (see <see cref="Groove"/> for why 2:1 is the convention and what players actually do).</item>
/// <item>The <b>backbeat</b>: snare on beats 2 and 4, kick on 1 and 3.</item>
/// <item>The <b>boogie line</b>: bass walking root–3–5–6 under each chord, and guitar dyads
/// alternating root+5th / root+6th on the shuffled eighths (the standard blues rhythm-guitar
/// pattern; 12bar.de, "Blues Rhythm Guitar").</item>
/// <item>Lead fills drawn from the <b>hexatonic blues scale</b> (<see cref="Scale.Blues"/>) in
/// the gaps between phrases — sparse, because the form leaves room for the singer.</item>
/// </list>
/// Tempo defaults to a medium shuffle; the accepted range spans ~60 (slow 12/8) to ~160 (boogie).
/// </summary>
public sealed class BluesTrack : ISound
{
    /// <summary>Chord root offsets (semitones above the key) for the 12 bars: the canonical changes.</summary>
    private static readonly int[] BarChords = { 0, 0, 0, 0, 5, 5, 0, 0, 7, 5, 0, 0 };

    public Frequency Root { get; }
    public int Choruses { get; }
    public double Bpm { get; }
    public Level Level { get; }

    public Groove Groove => Groove.Shuffle(Bpm);

    /// <summary>The loopable length: exactly the choruses, no ring-out.</summary>
    public double LoopDuration => Choruses * 12 * Groove.BarSeconds;

    /// <summary>The loop plus a short ring-out for the final chord to die away.</summary>
    public double Duration => LoopDuration + 1.5;

    public BluesTrack(Frequency? root = null, int choruses = 1, double bpm = 96, Level? level = null)
    {
        this.Root = root ?? Frequency.FromNote("E2");
        if (this.Root.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(root), root, "The key must have a positive root pitch.");
        if (choruses < 1)
            throw new ArgumentOutOfRangeException(nameof(choruses), choruses, "A track needs at least one 12-bar chorus.");
        if (bpm is < 60 or > 160)
            throw new ArgumentOutOfRangeException(nameof(bpm), bpm, "Blues tempo spans roughly 60 (slow 12/8) to 160 (uptempo boogie) BPM.");
        this.Choruses = choruses;
        this.Bpm = bpm;
        this.Level = level ?? Level.Unity;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        Groove groove = Groove;
        DeterministicRandom random = context.CreateRandom($"blues:{Root.Hertz:0.###}:{Bpm:0.###}:{Choruses}");
        Scale lead = Scale.Blues(Root.Transposed(24)); // licks two octaves above the key root
        var events = new List<(double, ISound)>();

        double eighth = groove.SecondsPerBeat * 0.5;

        for (int chorus = 0; chorus < Choruses; chorus++)
        {
            for (int bar = 0; bar < 12; bar++)
            {
                int absoluteBar = chorus * 12 + bar;
                int chord = BarChords[bar];

                for (int beat = 0; beat < 4; beat++)
                {
                    // Bar 12's back half is the turnaround: the V that pulls back to bar 1.
                    int beatChord = bar == 11 && beat >= 2 ? 7 : chord;
                    double onBeat = groove.TimeOf(absoluteBar, beat);
                    double offBeat = groove.TimeOf(absoluteBar, beat + 0.5);

                    // Drums: kick 1 and 3, backbeat snare 2 and 4, shuffled ride on the hat.
                    if (beat is 0 or 2) events.Add((onBeat, new KickDrum(level: Level.FromDecibels(-8))));
                    if (beat is 1 or 3) events.Add((onBeat, new SnareDrum(level: Level.FromDecibels(-9))));
                    events.Add((onBeat, HiHat.Closed(Level.FromDecibels(-16))));
                    events.Add((offBeat, HiHat.Closed(Level.FromDecibels(-20))));

                    // Bass boogie: root–3–5–6, one note per beat, an octave below the key.
                    int boogie = beat switch { 0 => 0, 1 => 4, 2 => 7, _ => 9 };
                    events.Add((onBeat, new BassGuitar(
                        Root.Transposed(beatChord + boogie - 12),
                        duration: groove.SecondsPerBeat * 0.95,
                        tone: 0.35,
                        level: Level.FromDecibels(-5))));

                    // Guitar comping: the boogie dyad — root+5th on the beat, root+6th on the
                    // shuffled "and".
                    foreach ((double time, int colour) in new[] { (onBeat, 7), (offBeat, 9) })
                    {
                        events.Add((time, Guitar.Strum(
                            new[] { Root.Transposed(beatChord + 12), Root.Transposed(beatChord + 12 + colour) },
                            duration: eighth * 1.8,
                            strumSeconds: 0.008,
                            damping: 0.35,
                            level: Level.FromDecibels(-15))));
                    }
                }

                // A lead lick in the second half of every other bar, sometimes — the fills live
                // in the vocal gaps. Descending runs on the blues scale, shuffled eighths.
                if (bar % 2 == 1 && random.NextChance(0.45))
                {
                    int noteCount = 3 + (int)(random.NextDouble() * 3);
                    int startDegree = 4 + (int)(random.NextDouble() * 4);
                    for (int n = 0; n < noteCount; n++)
                    {
                        double at = groove.TimeOf(absoluteBar, 2 + n * 0.5);
                        int degree = System.Math.Max(0, startDegree - n);
                        events.Add((at, new Guitar(
                            lead.Degree(degree),
                            duration: eighth * 2.5,
                            damping: 0.1,
                            level: Level.FromDecibels(-12))));
                    }
                }
            }
        }

        return new Timeline(events).Render(context, duration).NormalizedOrDefault(new Level(0.9));
    }
}

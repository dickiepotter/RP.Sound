namespace RP.Sound.Music;

/// <summary>
/// A tempo, a meter and a feel — the rhythmic ground a piece of music stands on. Tempo is beats
/// per minute; the meter is how many beats make a bar; and <see cref="Swing"/> encodes the feel:
/// how unevenly each pair of subdivisions is split. Swing is stated the way drum machines have
/// stated it since Roger Linn's MPC: 50% is perfectly straight, 66.7% delays every second note to
/// the last third of the pair — the exact triplet "shuffle". (Measured jazz swing is actually
/// tempo-dependent — around 3:1 at slow tempos, approaching straight as tempo rises, with the
/// short note plateauing near 100 ms: Friberg &amp; Sundström, "Swing Ratios and Ensemble Timing
/// in Jazz Performance", Music Perception 19(3), 2002 — so the presets here pick the nominal 2:1
/// convention and callers can deviate knowingly.) The swing unit says which level swings: eighth
/// pairs for blues and jazz, sixteenth pairs for house.
/// </summary>
public readonly struct Groove
{
    /// <summary>Beats per minute.</summary>
    public double Bpm { get; }

    /// <summary>Beats in one bar.</summary>
    public int BeatsPerBar { get; }

    /// <summary>The fraction of each subdivision pair given to the first note: 0.5 straight … 0.667 triplet shuffle.</summary>
    public double Swing { get; }

    /// <summary>The subdivision that swings, in beats: 0.5 = eighth pairs (jazz/blues), 0.25 = sixteenth pairs (house).</summary>
    public double SwingUnit { get; }

    public Groove(double bpm, double swing = 0.5, double swingUnit = 0.5, int beatsPerBar = 4)
    {
        if (bpm is < 20 or > 300 || !double.IsFinite(bpm))
            throw new ArgumentOutOfRangeException(nameof(bpm), bpm, "A groove's tempo stays within 20–300 BPM.");
        if (swing is < 0.5 or > 0.8)
            throw new ArgumentOutOfRangeException(nameof(swing), swing, "Swing runs from 0.5 (straight) to 0.8 (beyond triplet feel); below 0.5 would rush, which no groove does.");
        if (swingUnit is not (0.5 or 0.25))
            throw new ArgumentOutOfRangeException(nameof(swingUnit), swingUnit, "The swing unit is 0.5 (eighth pairs) or 0.25 (sixteenth pairs) of a beat.");
        if (beatsPerBar is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(beatsPerBar), beatsPerBar, "Beats per bar stays within 1–12.");
        this.Bpm = bpm;
        this.Swing = swing;
        this.SwingUnit = swingUnit;
        this.BeatsPerBar = beatsPerBar;
    }

    /// <summary>No swing: every subdivision exactly where the grid says.</summary>
    public static Groove Straight(double bpm, int beatsPerBar = 4) => new(bpm, 0.5, 0.5, beatsPerBar);

    /// <summary>The blues/jazz shuffle: eighth pairs split 2:1 (nominal triplet feel).</summary>
    public static Groove Shuffle(double bpm) => new(bpm, 2.0 / 3.0);

    public double SecondsPerBeat => 60.0 / Bpm;
    public double BarSeconds => BeatsPerBar * SecondsPerBeat;

    /// <summary>
    /// The moment (in seconds) of a possibly fractional beat within a bar, with the groove's
    /// swing applied. Each pair of swing-unit subdivisions is warped so its midpoint lands at the
    /// <see cref="Swing"/> fraction instead of halfway — notes on the grid's pair boundaries do
    /// not move, which is why the backbeat stays planted while the offbeats lean.
    /// </summary>
    public double TimeOf(int bar, double beat)
    {
        if (bar < 0) throw new ArgumentOutOfRangeException(nameof(bar), bar, "Bars count upward from 0.");
        if (beat < 0 || !double.IsFinite(beat))
            throw new ArgumentOutOfRangeException(nameof(beat), beat, "A beat position must be finite and non-negative.");

        double pairLength = SwingUnit * 2;
        double pairIndex = System.Math.Floor(beat / pairLength);
        double local = (beat - pairIndex * pairLength) / pairLength; // 0–1 within the pair
        double warped = local <= 0.5
            ? local * (Swing / 0.5)
            : Swing + (local - 0.5) * ((1 - Swing) / 0.5);
        double swungBeat = (pairIndex + warped) * pairLength;
        return (bar * BeatsPerBar + swungBeat) * SecondsPerBeat;
    }

    public override string ToString() =>
        $"{Bpm:0.#} BPM, {BeatsPerBar}/4{(Swing > 0.5 ? $", swing {Swing:P0} on {(SwingUnit == 0.5 ? "8ths" : "16ths")}" : "")}";
}

namespace RP.Sound.Synthesis;

/// <summary>
/// A low-frequency oscillator: a slow wave, below hearing, used not as sound but as
/// <em>movement</em> — it wiggles some parameter of a sound that would otherwise sit still.
/// Each classic destination has a name every musician knows: LFO→pitch is <b>vibrato</b>,
/// LFO→loudness is <b>tremolo</b>, and LFO→filter-cutoff is the <b>wah/wobble</b> (at dubstep
/// rates, the wobble bass). One LFO can drive all three destinations at once; a depth of zero
/// simply disconnects that destination.
/// </summary>
public readonly struct Lfo
{
    public Waveform Waveform { get; }

    /// <summary>How fast the movement cycles. 0.1–1 Hz breathes; ~5 Hz is vocal vibrato; faster reads as an effect.</summary>
    public Frequency Rate { get; }

    /// <summary>Vibrato depth: how far the pitch swings either side of the note, in cents (100 = a semitone).</summary>
    public double PitchCents { get; }

    /// <summary>Wobble depth: how far the filter cutoff swings either side of its base, in octaves.</summary>
    public double CutoffOctaves { get; }

    /// <summary>Tremolo depth, 0–1: at 1 the loudness dips all the way to silence each cycle.</summary>
    public double TremoloDepth { get; }

    public Lfo(Waveform waveform, Frequency rate, double pitchCents = 0, double cutoffOctaves = 0, double tremoloDepth = 0)
    {
        if (rate.Hertz < 0 || rate.Hertz > 100)
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "An LFO rate belongs below hearing (0–100 Hz); faster modulation is audio-rate synthesis, not an LFO.");
        if (pitchCents is < 0 or > 1200 || !double.IsFinite(pitchCents))
            throw new ArgumentOutOfRangeException(nameof(pitchCents), pitchCents, "Vibrato depth is 0–1200 cents (up to an octave).");
        if (cutoffOctaves is < 0 or > 6 || !double.IsFinite(cutoffOctaves))
            throw new ArgumentOutOfRangeException(nameof(cutoffOctaves), cutoffOctaves, "Wobble depth is 0–6 octaves.");
        if (tremoloDepth is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(tremoloDepth), tremoloDepth, "Tremolo depth is a fraction between 0 and 1.");
        this.Waveform = waveform;
        this.Rate = rate;
        this.PitchCents = pitchCents;
        this.CutoffOctaves = cutoffOctaves;
        this.TremoloDepth = tremoloDepth;
    }

    /// <summary>No movement at all — the modulation-free patch.</summary>
    public static readonly Lfo None = default;

    /// <summary>Classic performance vibrato: a sine at ~5 Hz, a fraction of a semitone deep.</summary>
    public static Lfo Vibrato(double cents = 15, Frequency? rate = null) =>
        new(Waveform.Sine, rate ?? new Frequency(5), pitchCents: cents);

    /// <summary>
    /// The dubstep wobble: a sine driving the filter cutoff, deep. Sync the rate to the music —
    /// at 140 BPM, quarter-note wobble is 140/60 ≈ 2.33 Hz, eighth-note is 4.67 Hz.
    /// </summary>
    public static Lfo Wobble(Frequency rate, double octaves = 2.5) =>
        new(Waveform.Sine, rate, cutoffOctaves: octaves);

    /// <summary>The current LFO value in [−1, 1] at a moment in time (a zero-rate LFO holds at 0).</summary>
    public double Sample(double time) =>
        Rate.Hertz <= 0 ? 0 : Oscillator.Sample(Waveform, time * Rate.Hertz % 1.0);
}

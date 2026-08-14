namespace RP.Sound.Synthesis;

/// <summary>
/// Two-operator frequency modulation: one oscillator (the <em>modulator</em>) bends the pitch of
/// another (the <em>carrier</em>) at audio rate. Below about 20 Hz this would be vibrato — see
/// <see cref="Lfo"/> — but push the modulator up into the audible range and the ear stops hearing
/// movement and starts hearing <em>timbre</em>: sidebands appear around the carrier, spaced by the
/// modulator's frequency.
///
/// That spacing is the whole trick, and it is what <see cref="Ratio"/> controls. At a whole-number
/// ratio the sidebands land on whole multiples of the fundamental, so the result is a harmonic tone
/// — a bell-like but still musical note. At a ratio such as 2.41 they land <em>between</em> the
/// harmonics, and a spectrum with no common fundamental is precisely what the ear labels metallic,
/// clangorous or synthetic. Every convincing sci-fi zap in this library is a non-integer ratio.
///
/// This is really phase modulation, as implemented by Yamaha's DX7 (1983) and every "FM" chip
/// since: modulating phase rather than frequency gives the same sidebands but keeps the carrier
/// centred on its nominal pitch instead of letting it drift.
///
/// The carrier pitch may also glide from <see cref="Start"/> to <see cref="End"/> over the note.
/// The sweep lives here rather than in a separate <see cref="FrequencySweep"/> because the
/// modulator tracks the carrier: as the pitch falls, the whole sideband structure falls with it,
/// keeping the timbre constant. Two chained objects could not express that.
/// </summary>
public sealed class FmOscillator : ISound
{
    /// <summary>The carrier's pitch at the start of the note.</summary>
    public Frequency Start { get; }

    /// <summary>The carrier's pitch at the end of the note (equal to <see cref="Start"/> for a steady tone).</summary>
    public Frequency End { get; }

    /// <summary>
    /// The modulator's frequency as a multiple of the carrier's. Whole numbers give harmonic
    /// (musical) tones; anything else gives inharmonic (metallic) ones.
    /// </summary>
    public double Ratio { get; }

    /// <summary>
    /// The modulation index — how far the modulator swings the carrier's phase, in radians. This is
    /// the classic FM depth control: 0 leaves a pure carrier, ~1 adds a first pair of sidebands,
    /// and beyond about 5 the spectrum is dense and the tone frankly synthetic.
    /// </summary>
    public double Index { get; }

    /// <summary>The carrier's shape. Sine is the classic choice — FM already supplies the harmonics.</summary>
    public Waveform Carrier { get; }

    /// <summary>The modulator's shape. Sine gives the textbook sideband pattern; others give denser spectra.</summary>
    public Waveform Modulator { get; }

    /// <summary>
    /// Whether the pitch glide is exponential (equal musical intervals per second) rather than
    /// linear (equal hertz per second). Pitch is heard logarithmically, so exponential is the one
    /// that sounds like an even slide; linear is occasionally wanted for a deliberate lurch.
    /// </summary>
    public bool ExponentialSweep { get; }

    /// <summary>
    /// Cyclic movement on top of the glide. Only the vibrato and tremolo destinations apply here —
    /// this voice has no filter, so <see cref="Lfo.CutoffOctaves"/> is ignored; for a moving
    /// cutoff see <see cref="Effects.FilterSweep"/>.
    /// </summary>
    public Lfo Lfo { get; }

    public Level Level { get; }

    public double Duration { get; }

    public FmOscillator(
        Frequency start,
        double duration = 1.0,
        Frequency? end = null,
        double ratio = 1.0,
        double index = 0,
        Waveform carrier = Waveform.Sine,
        Waveform modulator = Waveform.Sine,
        bool exponentialSweep = true,
        Lfo lfo = default,
        Level? level = null)
    {
        if (duration <= 0 || !double.IsFinite(duration))
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "An oscillator's duration must be finite and positive.");
        if (ratio is < 0 or > 64 || !double.IsFinite(ratio))
            throw new ArgumentOutOfRangeException(nameof(ratio), ratio, "The modulator ratio is kept within 0–64; beyond that the sidebands fold back as aliasing rather than adding tone.");
        if (index is < 0 or > 24 || !double.IsFinite(index))
            throw new ArgumentOutOfRangeException(nameof(index), index, "The modulation index is kept within 0–24 radians; beyond that the spectrum is noise, not timbre.");

        this.Start = start;
        this.End = end ?? start;
        if (exponentialSweep && (this.Start.Hertz <= 0 || this.End.Hertz <= 0))
            throw new ArgumentOutOfRangeException(nameof(end), "An exponential glide needs strictly positive endpoint frequencies; pass exponentialSweep: false to glide to or from zero.");

        this.Ratio = ratio;
        this.Index = index;
        this.Carrier = carrier;
        this.Modulator = modulator;
        this.ExponentialSweep = exponentialSweep;
        this.Lfo = lfo;
        this.Duration = duration;
        this.Level = level ?? Level.Unity;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        int active = System.Math.Min(samples.Length, context.SampleCount(System.Math.Min(Duration, duration)));

        double glide = Start.Hertz <= 0 ? 1 : End.Hertz / Start.Hertz;
        double phase = 0, modulatorPhase = 0;

        for (int i = 0; i < active; i++)
        {
            double time = (double)i / context.SampleRate;
            double progress = time / Duration;

            double hertz = ExponentialSweep
                ? Start.Hertz * System.Math.Pow(glide, progress)
                : Start.Hertz + (End.Hertz - Start.Hertz) * progress;

            double movement = Lfo.Sample(time);
            if (Lfo.PitchCents > 0) hertz *= System.Math.Pow(2, Lfo.PitchCents * movement / 1200.0);

            // Phase modulation. Index is in radians by convention, while Oscillator.Sample takes a
            // phase in cycles, so the deviation is divided by a full turn before it is added.
            double deviation = Index <= 0 ? 0 : Index * Oscillator.Sample(Modulator, modulatorPhase) / (2 * System.Math.PI);
            double tremolo = 1 - Lfo.TremoloDepth * (1 - movement) / 2;
            samples[i] = (float)(Oscillator.Sample(Carrier, Wrap(phase + deviation)) * tremolo * Level.Linear);

            phase = Wrap(phase + hertz / context.SampleRate);
            modulatorPhase = Wrap(modulatorPhase + hertz * Ratio / context.SampleRate);
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }

    // Math.Floor rather than a subtract-if-over-one, because the modulation deviation can push the
    // phase more than a whole turn past the end in one step, and can push it negative.
    private static double Wrap(double phase) => phase - System.Math.Floor(phase);
}

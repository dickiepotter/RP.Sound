namespace RP.Sound.Synthesis;

/// <summary>
/// Everything a subtractive synthesizer needs to know to make a sound, gathered into one
/// immutable description — a <em>patch</em>, named for the patch cables of the modular era.
/// The architecture is the classic one that Moog fixed in place with the Minimoog (1970) and
/// that nearly every synthesizer since has followed:
///
///   oscillators (bright, raw)  →  filter (carve away)  →  amplifier (shape in time)
///
/// "Subtractive" because the oscillators start with more harmonics than wanted and the filter
/// subtracts; the two envelopes then animate loudness (amplifier) and brightness (filter cutoff)
/// independently, and one <see cref="Lfo"/> adds cyclic movement. Understand this one signal
/// path and most synthesizers ever sold become readable.
/// </summary>
public sealed class SynthPatch
{
    /// <summary>The first oscillator's shape — the patch's basic tone colour.</summary>
    public Waveform Oscillator1 { get; }

    /// <summary>The second oscillator's shape, mixed in for thickness or contrast.</summary>
    public Waveform Oscillator2 { get; }

    /// <summary>
    /// How far oscillator 2 sits from the note, in cents. A few cents of detune makes the pair
    /// beat slowly — the "fat" analogue chorus effect; 1200 is an octave up, −1200 an octave down
    /// (the classic sub-oscillator).
    /// </summary>
    public double Oscillator2DetuneCents { get; }

    /// <summary>The balance of the two oscillators: 0 = all oscillator 1, 1 = all oscillator 2.</summary>
    public double OscillatorMix { get; }

    /// <summary>How much white noise joins the oscillators (0–1): breath, wind and percussion live here.</summary>
    public double NoiseMix { get; }

    /// <summary>The low-pass filter's resting corner frequency — the patch's basic brightness.</summary>
    public Frequency FilterCutoff { get; }

    /// <summary>Filter resonance (Q): 0.707 is flat; higher values ring and whistle at the cutoff.</summary>
    public double FilterResonance { get; }

    /// <summary>
    /// How far the filter envelope pushes the cutoff above its resting point at full envelope, in
    /// octaves. This is the single most characteristic subtractive gesture: the "wow" of a note
    /// opening bright and closing dark.
    /// </summary>
    public double FilterEnvelopeOctaves { get; }

    /// <summary>The loudness contour, applied at the amplifier.</summary>
    public Envelope AmplitudeEnvelope { get; }

    /// <summary>The brightness contour, applied at the filter cutoff.</summary>
    public Envelope FilterEnvelope { get; }

    /// <summary>The patch's movement: vibrato, wobble and/or tremolo from one LFO.</summary>
    public Lfo Lfo { get; }

    public SynthPatch(
        Waveform oscillator1 = Waveform.Sawtooth,
        Waveform oscillator2 = Waveform.Sawtooth,
        double oscillator2DetuneCents = 7,
        double oscillatorMix = 0.5,
        double noiseMix = 0,
        Frequency? filterCutoff = null,
        double filterResonance = 0.9,
        double filterEnvelopeOctaves = 2,
        Envelope? amplitudeEnvelope = null,
        Envelope? filterEnvelope = null,
        Lfo lfo = default)
    {
        if (oscillator2DetuneCents is < -2400 or > 2400 || !double.IsFinite(oscillator2DetuneCents))
            throw new ArgumentOutOfRangeException(nameof(oscillator2DetuneCents), oscillator2DetuneCents, "Detune is measured in cents and stays within ±2400 (two octaves).");
        if (oscillatorMix is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(oscillatorMix), oscillatorMix, "The oscillator mix is a fraction between 0 and 1.");
        if (noiseMix is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(noiseMix), noiseMix, "The noise mix is a fraction between 0 and 1.");
        this.FilterCutoff = filterCutoff ?? new Frequency(2000);
        if (this.FilterCutoff.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(filterCutoff), filterCutoff, "The filter cutoff must be positive.");
        if (filterResonance is < 0.05 or > 20 || !double.IsFinite(filterResonance))
            throw new ArgumentOutOfRangeException(nameof(filterResonance), filterResonance, "Filter resonance (Q) is kept within 0.05–20; beyond that the filter is an oscillator.");
        if (filterEnvelopeOctaves is < 0 or > 8 || !double.IsFinite(filterEnvelopeOctaves))
            throw new ArgumentOutOfRangeException(nameof(filterEnvelopeOctaves), filterEnvelopeOctaves, "The filter envelope depth is 0–8 octaves.");

        this.Oscillator1 = oscillator1;
        this.Oscillator2 = oscillator2;
        this.Oscillator2DetuneCents = oscillator2DetuneCents;
        this.OscillatorMix = oscillatorMix;
        this.NoiseMix = noiseMix;
        this.FilterResonance = filterResonance;
        this.FilterEnvelopeOctaves = filterEnvelopeOctaves;
        this.AmplitudeEnvelope = amplitudeEnvelope ?? Envelope.Adsr(0.01, 0.1, Level.FromDecibels(-3), 0.2);
        this.FilterEnvelope = filterEnvelope ?? Envelope.Adsr(0.005, 0.25, Level.FromDecibels(-9), 0.2);
        this.Lfo = lfo;
    }

    // ---- The presets: one worked example of each classic patch family. Each is a teaching
    // example — read the parameters against the class doc and the sound explains itself. ----

    /// <summary>Fat detuned saws, darkish filter, snappy envelope — the everyday synth bass.</summary>
    public static SynthPatch Bass { get; } = new(
        oscillator2DetuneCents: 5,
        filterCutoff: new Frequency(400),
        filterResonance: 1.2,
        filterEnvelopeOctaves: 2.5,
        amplitudeEnvelope: Envelope.Adsr(0.005, 0.15, Level.FromDecibels(-6), 0.1),
        filterEnvelope: Envelope.Adsr(0.002, 0.12, Level.FromDecibels(-12), 0.1));

    /// <summary>Saw against square, resonant filter, vibrato — a singing solo voice.</summary>
    public static SynthPatch Lead { get; } = new(
        oscillator2: Waveform.Square,
        oscillator2DetuneCents: 10,
        filterCutoff: new Frequency(1500),
        filterResonance: 2,
        filterEnvelopeOctaves: 1.5,
        amplitudeEnvelope: Envelope.Adsr(0.02, 0.1, Level.FromDecibels(-3), 0.15),
        lfo: Lfo.Vibrato(cents: 20));

    /// <summary>A fast-closing filter over a fast-dying note: the synthetic pluck.</summary>
    public static SynthPatch Pluck { get; } = new(
        oscillator2DetuneCents: 4,
        filterCutoff: new Frequency(300),
        filterResonance: 1.5,
        filterEnvelopeOctaves: 3.5,
        amplitudeEnvelope: Envelope.Percussive(0.5),
        filterEnvelope: Envelope.Percussive(0.2));

    /// <summary>Wide detune, slow envelopes, gently breathing filter — the sustained bed.</summary>
    public static SynthPatch Pad { get; } = new(
        oscillator2DetuneCents: 15,
        filterCutoff: new Frequency(900),
        filterResonance: 0.8,
        filterEnvelopeOctaves: 0.8,
        amplitudeEnvelope: new Envelope(0.8, 0.3, Level.FromDecibels(-2), 1.2, EnvelopeCurve.Linear),
        filterEnvelope: new Envelope(1.2, 0.5, Level.FromDecibels(-4), 1.0, EnvelopeCurve.Linear),
        lfo: new Lfo(Waveform.Sine, new Frequency(0.5), cutoffOctaves: 0.3));

    /// <summary>
    /// The dubstep wobble bass: saw + square into a deep resonant filter driven hard by the LFO.
    /// Pass the LFO rate that matches the music — see <see cref="Synthesis.Lfo.Wobble"/>.
    /// </summary>
    public static SynthPatch Wobble(Frequency lfoRate) => new(
        oscillator2: Waveform.Square,
        oscillator2DetuneCents: -1200, // sub-octave square underneath the saw
        filterCutoff: new Frequency(250),
        filterResonance: 3,
        filterEnvelopeOctaves: 0,
        amplitudeEnvelope: Envelope.Adsr(0.005, 0, Level.Unity, 0.05),
        lfo: Lfo.Wobble(lfoRate));
}

namespace RP.Sound.Instruments;

/// <summary>
/// A hi-hat cymbal pair. Real cymbal vibration is so modally dense it borders on noise, but plain
/// noise sounds like tape hiss, not metal. The trick — taken from the Roland TR-808's cymbal
/// circuit, whose schematic drives its hats from six square-wave oscillators at deliberately
/// unrelated frequencies — is a stack of inharmonic square waves: their beating intermodulation
/// is what reads as "metallic". The stack is then high-passed hard so only the sizzle remains,
/// and shaped by one exponential decay: a few tens of milliseconds for a closed hat (the cymbals
/// choke each other), half a second or more for an open one.
/// </summary>
public sealed class HiHat : ISound
{
    // The TR-808's six oscillator frequencies (Hz), as measured from its schematic in circuit
    // analyses of the 808 cymbal voice. Deliberately inharmonic: no frequency is a whole-number
    // multiple of another, so the sum never settles into a pitched tone.
    private static readonly double[] OscillatorFrequencies = { 205.3, 304.4, 369.6, 522.7, 540.0, 800.0 };

    /// <summary>Open hats ring on; closed hats are choked short.</summary>
    public bool IsOpen { get; }

    /// <summary>Seconds until the hat has died away to −60 dB.</summary>
    public double Decay { get; }

    public Level Level { get; }

    public double Duration => Decay;

    public HiHat(bool open = false, double? decay = null, Level? level = null)
    {
        this.IsOpen = open;
        this.Decay = decay ?? (open ? 0.5 : 0.08);
        if (this.Decay <= 0 || !double.IsFinite(this.Decay))
            throw new ArgumentOutOfRangeException(nameof(decay), decay, "A hi-hat's decay must be finite and positive (seconds).");
        this.Level = level ?? Level.Unity;
    }

    /// <summary>The tight tick that keeps time on the offbeats.</summary>
    public static HiHat Closed(Level? level = null) => new(open: false, level: level);

    /// <summary>The sustained sizzle that marks accents and offbeat "and"s.</summary>
    public static HiHat Open(Level? level = null) => new(open: true, level: level);

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        int active = System.Math.Min(samples.Length, context.SampleCount(System.Math.Min(Duration, duration)));

        double rate = 6.9 / Decay;
        Span<double> phases = stackalloc double[OscillatorFrequencies.Length];

        // Two cascaded one-pole high-passes at ~7 kHz strip the square stack down to its sizzle.
        double coefficient = System.Math.Exp(-2 * System.Math.PI * 7000.0 / context.SampleRate);
        double hp1 = 0, hp2 = 0, previous1 = 0, previous2 = 0;

        for (int i = 0; i < active; i++)
        {
            double sum = 0;
            for (int o = 0; o < phases.Length; o++)
            {
                sum += phases[o] % 1.0 < 0.5 ? 1 : -1;
                phases[o] += OscillatorFrequencies[o] / context.SampleRate;
            }

            sum /= phases.Length;
            hp1 = coefficient * (hp1 + sum - previous1);
            previous1 = sum;
            hp2 = coefficient * (hp2 + hp1 - previous2);
            previous2 = hp1;

            double t = (double)i / context.SampleRate;
            samples[i] = (float)(hp2 * System.Math.Exp(-rate * t) * Level.Linear * 2.5);
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

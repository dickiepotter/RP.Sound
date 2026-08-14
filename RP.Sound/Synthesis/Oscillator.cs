namespace RP.Sound.Synthesis;

/// <summary>The four classic waveform shapes, in rising order of harmonic richness.</summary>
public enum Waveform
{
    /// <summary>A pure tone: one frequency, no harmonics — the atom every other sound decomposes into.</summary>
    Sine,

    /// <summary>Odd harmonics falling as 1/n² — soft and flute-like.</summary>
    Triangle,

    /// <summary>Odd harmonics falling as 1/n — hollow, clarinet-like.</summary>
    Square,

    /// <summary>Every harmonic, falling as 1/n — the brightest, and the classic raw material for filtering.</summary>
    Sawtooth,
}

/// <summary>
/// A steady tone at one frequency. The waveforms are generated in their ideal mathematical shape,
/// which is the clearest to read and to teach; the cost is a little aliasing on bright waveforms
/// at high pitches (band-limited generation such as polyBLEP is noted as future work).
/// </summary>
public sealed class Oscillator : ISound
{
    public Waveform Waveform { get; }
    public Frequency Frequency { get; }
    public Level Level { get; }
    public double Duration { get; }

    public Oscillator(Waveform waveform, Frequency frequency, double duration, Level? level = null)
    {
        if (duration < 0 || !double.IsFinite(duration))
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "An oscillator's duration must be finite and non-negative.");
        this.Waveform = waveform;
        this.Frequency = frequency;
        this.Duration = duration;
        this.Level = level ?? Level.Unity;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        int active = System.Math.Min(samples.Length, context.SampleCount(System.Math.Min(Duration, duration)));
        double phaseStep = Frequency.Hertz / context.SampleRate;
        double phase = 0;
        for (int i = 0; i < active; i++)
        {
            samples[i] = (float)(Sample(Waveform, phase) * Level.Linear);
            phase += phaseStep;
            if (phase >= 1) phase -= 1;
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }

    /// <summary>One cycle of each waveform, from a phase in [0, 1).</summary>
    public static double Sample(Waveform waveform, double phase) => waveform switch
    {
        Waveform.Sine => System.Math.Sin(2 * System.Math.PI * phase),
        Waveform.Triangle => 1 - 4 * System.Math.Abs(phase - 0.5),
        Waveform.Square => phase < 0.5 ? 1 : -1,
        Waveform.Sawtooth => 2 * phase - 1,
        _ => throw new ArgumentOutOfRangeException(nameof(waveform)),
    };
}

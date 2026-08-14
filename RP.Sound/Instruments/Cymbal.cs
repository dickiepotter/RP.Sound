namespace RP.Sound.Instruments;

/// <summary>
/// A crash/ride cymbal. A cymbal has so many closely packed vibration modes that its response is
/// effectively a dense inharmonic spectrum (Fletcher &amp; Rossing describe cymbal vibration as
/// hundreds of modes shading into chaos), so instead of modelling individual modes the way
/// <see cref="RP.Sound.Physics.ModalBody"/> does for a bar, this voice scatters several dozen
/// partials at deterministic random positions, uniform in log-frequency across 300 Hz–12 kHz
/// (equal partials per octave — how the ear hears spectral density). Low partials outlast high
/// ones, so the strike's broadband "crash" mellows into a lingering shimmer, exactly the arc of
/// the real instrument.
/// </summary>
public sealed class Cymbal : ISound
{
    private const int PartialCount = 48;

    /// <summary>Seconds until the cymbal has died away to −60 dB.</summary>
    public double Decay { get; }

    public Level Level { get; }

    public double Duration => Decay;

    public Cymbal(double decay = 2.5, Level? level = null)
    {
        if (decay <= 0 || !double.IsFinite(decay))
            throw new ArgumentOutOfRangeException(nameof(decay), decay, "A cymbal's decay must be finite and positive (seconds).");
        this.Decay = decay;
        this.Level = level ?? Level.Unity;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        int active = System.Math.Min(samples.Length, context.SampleCount(System.Math.Min(Duration, duration)));

        // The partial positions come from a named stream, so every render of this description is
        // the same cymbal — re-seed the context for a differently hammered one.
        DeterministicRandom random = context.CreateRandom($"cymbal:{Decay:0.###}");

        double logLow = System.Math.Log(300);
        double logHigh = System.Math.Log(System.Math.Min(12000, context.SampleRate * 0.45));

        for (int p = 0; p < PartialCount; p++)
        {
            double frequency = System.Math.Exp(random.Range(logLow, logHigh));
            double phase = random.NextDouble() * 2 * System.Math.PI;
            double amplitude = random.Range(0.5, 1.0) / PartialCount;

            // Each partial's decay shortens gently with frequency ((300/f)^0.4): highs die first,
            // and the full Decay is only reached by the lowest shimmer.
            double partialDecay = Decay * System.Math.Pow(300 / frequency, 0.4);
            double decayPerSample = System.Math.Exp(-6.9 / (partialDecay * context.SampleRate));
            double omega = 2 * System.Math.PI * frequency / context.SampleRate;

            double envelope = amplitude;
            for (int i = 0; i < active; i++)
            {
                samples[i] += (float)(envelope * System.Math.Sin(phase));
                phase += omega;
                envelope *= decayPerSample;
                if (envelope < 1e-7) break;
            }
        }

        // The strike itself: a short broadband noise burst that fuses the partials into one "crash".
        int burstSamples = System.Math.Min(active, context.SampleCount(0.01));
        double previous = 0, highPassed = 0;
        for (int i = 0; i < burstSamples; i++)
        {
            double noise = random.NextSigned();
            highPassed = 0.9 * (highPassed + noise - previous);
            previous = noise;
            double window = 1.0 - (double)i / burstSamples;
            samples[i] += (float)(highPassed * window * 0.4);
        }

        for (int i = 0; i < active; i++) samples[i] = (float)(samples[i] * Level.Linear * 2.2);

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

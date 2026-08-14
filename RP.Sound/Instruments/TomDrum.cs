namespace RP.Sound.Instruments;

/// <summary>
/// A tom-tom: the pitched drum. The same physics as the <see cref="KickDrum"/> — a struck
/// membrane whose tension transient makes the pitch start high and settle — but tuned higher,
/// swept less, and rung longer, with the membrane's first overtone at 1.59× the fundamental
/// (the ideal circular membrane's 11/01 mode ratio; Fletcher &amp; Rossing) added for body.
/// A row of these at different pitches is a fill waiting to happen.
/// </summary>
public sealed class TomDrum : ISound
{
    /// <summary>The resting fundamental. Floor toms ~80 Hz, rack toms 110–180 Hz.</summary>
    public Frequency Pitch { get; }

    /// <summary>Seconds until the drum has died away to −60 dB.</summary>
    public double Decay { get; }

    public Level Level { get; }

    public double Duration => Decay;

    public TomDrum(Frequency? pitch = null, double decay = 0.4, Level? level = null)
    {
        this.Pitch = pitch ?? new Frequency(110);
        if (this.Pitch.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(pitch), pitch, "A tom's pitch must be positive.");
        if (decay <= 0 || !double.IsFinite(decay))
            throw new ArgumentOutOfRangeException(nameof(decay), decay, "A tom's decay must be finite and positive (seconds).");
        this.Decay = decay;
        this.Level = level ?? Level.Unity;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        int active = System.Math.Min(samples.Length, context.SampleCount(System.Math.Min(Duration, duration)));
        DeterministicRandom random = context.CreateRandom($"tom:{Pitch.Hertz:0.###}");

        // A gentler sweep than the kick: 1.4× down to resting over ~50 ms.
        const double sweepRatio = 0.4;
        const double sweepTime = 0.05;
        double ampRate = 6.9 / Decay;
        double overtonePhase = random.NextDouble() * 2 * System.Math.PI;

        double phase = 0;
        for (int i = 0; i < active; i++)
        {
            double t = (double)i / context.SampleRate;
            double frequency = Pitch.Hertz * (1 + sweepRatio * System.Math.Exp(-t / sweepTime));
            phase += frequency / context.SampleRate;
            double envelope = System.Math.Exp(-ampRate * t);

            // The overtone is quieter and dies twice as fast — the fundamental carries the note.
            double fundamental = System.Math.Sin(2 * System.Math.PI * phase);
            double overtone = 0.35 * System.Math.Sin(2 * System.Math.PI * phase * 1.59 + overtonePhase) * System.Math.Exp(-ampRate * t);
            samples[i] = (float)((fundamental + overtone) * envelope * Level.Linear * 0.8);
        }

        // A soft strike thump: 3 ms of low-passed noise, quieter than the kick's click.
        int thumpSamples = System.Math.Min(active, context.SampleCount(0.003));
        double state = 0;
        for (int i = 0; i < thumpSamples; i++)
        {
            state = 0.85 * state + 0.15 * random.NextSigned();
            double window = 1.0 - (double)i / thumpSamples;
            samples[i] += (float)(state * window * 0.3 * Level.Linear);
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

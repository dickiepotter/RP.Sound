namespace RP.Sound.Instruments;

/// <summary>
/// A kick (bass) drum, synthesised the classic way: a sine wave that sweeps rapidly down onto its
/// resting pitch, plus a short click at the strike. The recipe is the standard one taught for
/// analogue drum synthesis (Gordon Reid, "Synth Secrets: Practical Bass Drum Synthesis", Sound on
/// Sound, 2002 — the model behind the Roland TR-808/909 kick), and it has a physical basis: a
/// struck drumhead is momentarily stretched tighter, and tighter means higher-pitched, so the
/// membrane's pitch really does start high and fall as the tension transient settles (the same
/// tension-modulation glide Fletcher &amp; Rossing document for timpani).
/// </summary>
public sealed class KickDrum : ISound
{
    /// <summary>The resting fundamental the sweep lands on. 40–60 Hz is the felt-not-heard club range.</summary>
    public Frequency Pitch { get; }

    /// <summary>
    /// 0 soft … 1 hard: sets both how far the pitch sweep starts above the resting pitch and how
    /// loud the strike click is — the two cues that together read as "hit harder".
    /// </summary>
    public double Punch { get; }

    /// <summary>Seconds until the drum has died away to −60 dB.</summary>
    public double Decay { get; }

    public Level Level { get; }

    public double Duration => Decay;

    public KickDrum(Frequency? pitch = null, double punch = 0.6, double decay = 0.5, Level? level = null)
    {
        this.Pitch = pitch ?? new Frequency(50);
        if (this.Pitch.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(pitch), pitch, "A kick drum's pitch must be positive.");
        if (punch is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(punch), punch, "Punch is a fraction between 0 and 1.");
        if (decay <= 0 || !double.IsFinite(decay))
            throw new ArgumentOutOfRangeException(nameof(decay), decay, "A kick drum's decay must be finite and positive (seconds).");
        this.Punch = punch;
        this.Decay = decay;
        this.Level = level ?? Level.Unity;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        int active = System.Math.Min(samples.Length, context.SampleCount(System.Math.Min(Duration, duration)));
        DeterministicRandom random = context.CreateRandom($"kick:{Pitch.Hertz:0.###}");

        // Pitch sweep: starts up to 2.5× above the resting pitch and falls exponentially with a
        // ~40 ms time constant — fast enough to read as one "thump", not a slide.
        double sweepRatio = 2.5 * Punch;
        const double sweepTime = 0.04;

        // Amplitude reaches −60 dB (e^−6.9) at Decay.
        double ampRate = 6.9 / Decay;

        double phase = 0;
        for (int i = 0; i < active; i++)
        {
            double t = (double)i / context.SampleRate;
            double frequency = Pitch.Hertz * (1 + sweepRatio * System.Math.Exp(-t / sweepTime));
            phase += frequency / context.SampleRate;
            double envelope = System.Math.Exp(-ampRate * t);
            samples[i] = (float)(System.Math.Sin(2 * System.Math.PI * phase) * envelope * Level.Linear);
        }

        // The strike click: 2 ms of low-passed noise. It carries the attack transient that lets a
        // kick cut through a mix even on small speakers that reproduce none of the fundamental.
        int clickSamples = System.Math.Min(active, context.SampleCount(0.002));
        double clickState = 0;
        for (int i = 0; i < clickSamples; i++)
        {
            clickState = 0.7 * clickState + 0.3 * random.NextSigned();
            double window = 1.0 - (double)i / clickSamples;
            samples[i] += (float)(clickState * window * Punch * 0.5 * Level.Linear);
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

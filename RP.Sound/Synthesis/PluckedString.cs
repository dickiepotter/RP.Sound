namespace RP.Sound.Synthesis;

/// <summary>
/// A plucked string by the Karplus–Strong algorithm (Karplus &amp; Strong, 1983): a delay line the
/// length of one period is filled with noise (the pluck), then recirculated through a gentle
/// low-pass filter. Each round trip is one vibration of the string, and the filter makes the high
/// harmonics die first — exactly what a real string does. Remarkably string-like for a dozen
/// lines of code, which is why it is the classic teaching example of physical modelling.
/// </summary>
public sealed class PluckedString : ISound
{
    public Frequency Frequency { get; }
    public double Duration { get; }

    /// <summary>How quickly the string's brightness dies: 0 = rings for ages, 1 = damps almost at once.</summary>
    public double Damping { get; }

    public Level Level { get; }

    public PluckedString(Frequency frequency, double duration, double damping = 0.1, Level? level = null)
    {
        if (frequency.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "A string must have a positive pitch.");
        if (duration < 0 || !double.IsFinite(duration))
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A pluck's duration must be finite and non-negative.");
        if (damping is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(damping), damping, "Damping is a fraction between 0 and 1.");
        this.Frequency = frequency;
        this.Duration = duration;
        this.Damping = damping;
        this.Level = level ?? Level.Unity;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        int active = System.Math.Min(samples.Length, context.SampleCount(System.Math.Min(Duration, duration)));

        // The delay line is one period long: the wave travels the string and back once per cycle.
        int period = System.Math.Max(2, (int)System.Math.Round(context.SampleRate / Frequency.Hertz));
        var line = new double[period];
        DeterministicRandom random = context.CreateRandom($"pluck:{Frequency.Hertz:0.###}");
        for (int i = 0; i < period; i++) line[i] = random.NextSigned();

        // Feedback below 1 sets overall decay; the two-point average is the low-pass that
        // brightens-then-mellows the tone (it costs half a sample of delay, slightly flattening
        // the pitch — accepted here for clarity).
        double feedback = 0.996 - 0.1 * Damping;
        int index = 0;
        for (int i = 0; i < active; i++)
        {
            double current = line[index];
            int next = (index + 1) % period;
            line[index] = feedback * 0.5 * (current + line[next]);
            samples[i] = (float)(current * Level.Linear);
            index = next;
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

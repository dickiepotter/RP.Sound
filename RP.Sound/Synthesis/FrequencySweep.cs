namespace RP.Sound.Synthesis;

/// <summary>
/// A tone that glides from one frequency to another. The glide is exponential — equal musical
/// intervals per second rather than equal hertz — because pitch perception is logarithmic: a
/// linear sweep sounds like it slows down as it rises.
/// </summary>
public sealed class FrequencySweep : ISound
{
    public Frequency Start { get; }
    public Frequency End { get; }
    public Waveform Waveform { get; }
    public Level Level { get; }
    public double Duration { get; }

    public FrequencySweep(Frequency start, Frequency end, double duration, Waveform waveform = Waveform.Sine, Level? level = null)
    {
        if (duration <= 0 || !double.IsFinite(duration))
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A sweep's duration must be finite and positive.");
        if (start.Hertz <= 0 || end.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(start), "An exponential sweep needs strictly positive endpoint frequencies.");
        this.Start = start;
        this.End = end;
        this.Duration = duration;
        this.Waveform = waveform;
        this.Level = level ?? Level.Unity;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        int active = System.Math.Min(samples.Length, context.SampleCount(System.Math.Min(Duration, duration)));
        double ratio = End.Hertz / Start.Hertz;
        double phase = 0;
        for (int i = 0; i < active; i++)
        {
            double t = (double)i / context.SampleRate / Duration;
            double hertz = Start.Hertz * System.Math.Pow(ratio, t);
            samples[i] = (float)(Oscillator.Sample(Waveform, phase) * Level.Linear);
            phase += hertz / context.SampleRate;
            phase -= System.Math.Floor(phase);
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

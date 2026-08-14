namespace RP.Sound.Effects;

/// <summary>
/// A discrete repeating echo: the signal fed back onto itself at a fixed delay, each repeat
/// quieter by the feedback factor. Distinct from <see cref="Reverb"/>, whose reflections are so
/// dense they fuse into a wash — an echo's repeats are meant to be heard individually.
/// </summary>
public sealed class Echo
{
    public double Delay { get; }
    public Level Feedback { get; }
    public Level WetLevel { get; }

    public Echo(double delay, Level feedback, Level? wetLevel = null)
    {
        if (delay <= 0 || !double.IsFinite(delay))
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "An echo delay must be finite and positive.");
        if (feedback.Linear >= 1)
            throw new ArgumentOutOfRangeException(nameof(feedback), feedback, "Feedback at or above unity never decays — the echo would grow without bound.");
        this.Delay = delay;
        this.Feedback = feedback;
        this.WetLevel = wetLevel ?? Level.Half;
    }

    /// <summary>Applies the echo, extending the buffer until the repeats fall below −60 dB.</summary>
    public AudioBuffer Apply(AudioBuffer buffer)
    {
        // Number of repeats until feedback^n < −60 dB, the conventional "inaudible" floor.
        int repeats = Feedback.Linear <= 0 ? 1 : (int)System.Math.Ceiling(-60.0 / Feedback.Decibels);
        int delaySamples = (int)System.Math.Round(Delay * buffer.SampleRate);
        var samples = new float[buffer.Length + repeats * delaySamples];
        for (int i = 0; i < buffer.Length; i++) samples[i] = buffer[i];

        for (int i = delaySamples; i < samples.Length; i++)
            samples[i] += (float)(samples[i - delaySamples] * Feedback.Linear * WetLevel.Linear);

        return AudioBuffer.TakeOwnership(samples, buffer.SampleRate);
    }

    public ISound Apply(ISound sound) => new EchoedSound(sound, this);

    private sealed class EchoedSound(ISound source, Echo echo) : ISound
    {
        public double Duration => source.Duration; // the tail is available when a longer render is requested
        public AudioBuffer Render(AudioRenderContext context, double duration) =>
            echo.Apply(source.Render(context, System.Math.Min(duration, source.Duration))).FittedToDuration(duration);
    }
}

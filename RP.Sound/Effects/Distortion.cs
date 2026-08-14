namespace RP.Sound.Effects;

/// <summary>
/// Soft-clipping distortion: the signal is boosted then bent through tanh, which flattens the
/// peaks smoothly and adds the odd harmonics we hear as grit and aggression. Used sparingly it
/// reads as power and danger — which is why the threat-leaning moods reach for it.
/// </summary>
public static class Distortion
{
    public static AudioBuffer Apply(AudioBuffer buffer, double drive, Level? outputLevel = null)
    {
        if (drive < 1 || !double.IsFinite(drive))
            throw new ArgumentOutOfRangeException(nameof(drive), drive, "Drive is a pre-gain and must be at least 1.");
        double output = (outputLevel ?? Level.Unity).Linear / System.Math.Tanh(drive);
        var samples = new float[buffer.Length];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (float)(System.Math.Tanh(buffer[i] * drive) * output);
        return AudioBuffer.TakeOwnership(samples, buffer.SampleRate);
    }

    public static ISound Apply(ISound sound, double drive, Level? outputLevel = null) =>
        new DistortedSound(sound, drive, outputLevel);

    private sealed class DistortedSound(ISound source, double drive, Level? outputLevel) : ISound
    {
        public double Duration => source.Duration;
        public AudioBuffer Render(AudioRenderContext context, double duration) =>
            Apply(source.Render(context, duration), drive, outputLevel);
    }
}

public static class DistortionExtensions
{
    public static AudioBuffer Distorted(this AudioBuffer buffer, double drive, Level? outputLevel = null) =>
        Distortion.Apply(buffer, drive, outputLevel);

    public static ISound Distorted(this ISound sound, double drive, Level? outputLevel = null) =>
        Distortion.Apply(sound, drive, outputLevel);
}

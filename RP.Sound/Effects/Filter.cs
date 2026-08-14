namespace RP.Sound.Effects;

/// <summary>
/// The three everyday filters, as pure buffer→buffer operations (static form) and as sound
/// wrappers (extension form on <see cref="ISound"/>). Q is resonance: 0.707 is maximally flat;
/// higher values ring at the corner frequency.
/// </summary>
public static class Filter
{
    /// <summary>Keeps what is below the cutoff — muffles; also the "far away / behind a wall" cue.</summary>
    public static AudioBuffer LowPass(AudioBuffer buffer, Frequency cutoff, double q = 0.707) =>
        Apply(buffer, Biquad.LowPass(buffer.SampleRate, cutoff.Hertz, q));

    /// <summary>Keeps what is above the cutoff — thins; removes rumble and body.</summary>
    public static AudioBuffer HighPass(AudioBuffer buffer, Frequency cutoff, double q = 0.707) =>
        Apply(buffer, Biquad.HighPass(buffer.SampleRate, cutoff.Hertz, q));

    /// <summary>Keeps a band around the centre — the shape of resonance, whistles and radios.</summary>
    public static AudioBuffer BandPass(AudioBuffer buffer, Frequency centre, double q = 1) =>
        Apply(buffer, Biquad.BandPass(buffer.SampleRate, centre.Hertz, q));

    private static AudioBuffer Apply(AudioBuffer buffer, Biquad biquad)
    {
        var samples = new float[buffer.Length];
        for (int i = 0; i < samples.Length; i++) samples[i] = (float)biquad.Process(buffer[i]);
        return AudioBuffer.TakeOwnership(samples, buffer.SampleRate);
    }
}

public static class FilterExtensions
{
    public static AudioBuffer LowPassed(this AudioBuffer buffer, Frequency cutoff, double q = 0.707) => Filter.LowPass(buffer, cutoff, q);
    public static AudioBuffer HighPassed(this AudioBuffer buffer, Frequency cutoff, double q = 0.707) => Filter.HighPass(buffer, cutoff, q);
    public static AudioBuffer BandPassed(this AudioBuffer buffer, Frequency centre, double q = 1) => Filter.BandPass(buffer, centre, q);

    public static ISound LowPassed(this ISound sound, Frequency cutoff, double q = 0.707) =>
        new FilteredSound(sound, buffer => Filter.LowPass(buffer, cutoff, q));

    public static ISound HighPassed(this ISound sound, Frequency cutoff, double q = 0.707) =>
        new FilteredSound(sound, buffer => Filter.HighPass(buffer, cutoff, q));

    public static ISound BandPassed(this ISound sound, Frequency centre, double q = 1) =>
        new FilteredSound(sound, buffer => Filter.BandPass(buffer, centre, q));

    internal sealed class FilteredSound(ISound source, Func<AudioBuffer, AudioBuffer> apply) : ISound
    {
        public double Duration => source.Duration;
        public AudioBuffer Render(AudioRenderContext context, double duration) =>
            apply(source.Render(context, duration));
    }
}

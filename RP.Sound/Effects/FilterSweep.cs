namespace RP.Sound.Effects;

/// <summary>
/// A filter whose corner frequency glides across the sound, rather than sitting still as
/// <see cref="Filter"/> does. This is the single most useful gesture in sound design: a noise burst
/// is just noise until its cutoff sweeps, at which point it becomes a whoosh, a passing vehicle, a
/// door, or a thruster. The ear reads a moving cutoff as movement in the world.
///
/// The glide is exponential — equal octaves per second — for the same reason a pitch glide is:
/// brightness is judged logarithmically, so a linear sweep in hertz spends almost all its time in
/// the top octave and sounds like it stalls at the bottom.
///
/// The filter is retuned in place through <see cref="Biquad.RetuneLowPass"/> rather than rebuilt,
/// which is what keeps the sweep continuous; a fresh filter each sample would forget the samples
/// just past and click on every step.
/// </summary>
public static class FilterSweep
{
    /// <summary>Sweeps a low-pass corner from <paramref name="start"/> to <paramref name="end"/> across the buffer.</summary>
    public static AudioBuffer LowPass(AudioBuffer buffer, Frequency start, Frequency end, double q = 0.707)
    {
        if (start.Hertz <= 0 || end.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(start), "A filter sweep glides in octaves, so both endpoint cutoffs must be positive.");

        var samples = new float[buffer.Length];
        if (samples.Length == 0) return AudioBuffer.TakeOwnership(samples, buffer.SampleRate);

        Biquad biquad = Biquad.LowPass(buffer.SampleRate, start.Hertz, q);
        double glide = end.Hertz / start.Hertz;

        // Retuned every sample. Real-time synths recompute at a slower control rate to save the
        // trigonometry, but these renders are offline and short, and per-sample is the version that
        // is obviously correct.
        for (int i = 0; i < samples.Length; i++)
        {
            double progress = samples.Length == 1 ? 0 : (double)i / (samples.Length - 1);
            biquad.RetuneLowPass(buffer.SampleRate, start.Hertz * System.Math.Pow(glide, progress), q);
            samples[i] = (float)biquad.Process(buffer[i]);
        }

        return AudioBuffer.TakeOwnership(samples, buffer.SampleRate);
    }

    public static ISound LowPass(ISound sound, Frequency start, Frequency end, double q = 0.707) =>
        new FilterExtensions.FilteredSound(sound, buffer => LowPass(buffer, start, end, q));
}

public static class FilterSweepExtensions
{
    public static AudioBuffer LowPassSwept(this AudioBuffer buffer, Frequency start, Frequency end, double q = 0.707) =>
        FilterSweep.LowPass(buffer, start, end, q);

    public static ISound LowPassSwept(this ISound sound, Frequency start, Frequency end, double q = 0.707) =>
        FilterSweep.LowPass(sound, start, end, q);
}

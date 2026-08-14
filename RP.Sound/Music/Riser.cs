using RP.Sound.Effects;
using RP.Sound.Synthesis;

namespace RP.Sound.Music;

/// <summary>
/// A riser: the "something is about to happen" crescendo before a hit, drop or jump-scare.
/// It stacks every escalation cue at once — pitch climbing, noise brightening, loudness swelling,
/// a pulse accelerating — because each is a rate-of-change signal, and together they make the
/// arrival feel inevitable (Huron's prediction response, deliberately wound up). The riser ends
/// at its loudest instant: whatever follows lands on the peak.
/// </summary>
public sealed class Riser : ISound
{
    public double Duration { get; }

    /// <summary>0 gentle swell … 1 full horror build.</summary>
    public double Intensity { get; }

    public Level Level { get; }

    public Riser(double duration = 4, double intensity = 0.7, Level? level = null)
    {
        if (duration <= 0 || !double.IsFinite(duration))
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A riser's duration must be finite and positive.");
        if (intensity is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(intensity), intensity, "Intensity is a fraction between 0 and 1.");
        this.Duration = duration;
        this.Intensity = intensity;
        this.Level = level ?? Level.FromDecibels(-6);
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        double activeDuration = System.Math.Min(Duration, duration);

        // The tonal climb: two octaves up, ending on the arrival instant.
        AudioBuffer tone = new FrequencySweep(110, 440, activeDuration, Waveform.Sawtooth, new Level(0.4))
            .Render(context, activeDuration)
            .LowPassed(new Frequency(900 + 2600 * Intensity));

        // The noise swell, brightening as it grows (the band opens upward through the build).
        var noise = new float[context.SampleCount(activeDuration)];
        DeterministicRandom random = context.CreateRandom($"riser:{Intensity:0.###}");
        const int block = 256;
        for (int start = 0; start < noise.Length; start += block)
        {
            double t = (double)start / System.Math.Max(1, noise.Length);
            Biquad band = Biquad.BandPass(context.SampleRate, 300 + 4200 * t * t, 0.8);
            int end = System.Math.Min(noise.Length, start + block);
            for (int i = start; i < end; i++)
                noise[i] = (float)(band.Process(random.NextSigned()) * 1.6);
        }

        // An accelerating pulse — the countdown made audible.
        var pulse = new float[noise.Length];
        double phase = 0;
        for (int i = 0; i < pulse.Length; i++)
        {
            double t = (double)i / System.Math.Max(1, pulse.Length);
            double rate = 2 + 14 * t * t * Intensity;
            phase += rate / context.SampleRate;
            if (phase >= 1) phase -= 1;
            pulse[i] = (float)((phase < 0.12 ? 1 - phase / 0.12 : 0) * 0.5 * Intensity);
        }

        AudioBuffer mixed = tone
            .MixedWith(AudioBuffer.TakeOwnership(noise, context.SampleRate))
            .MixedWith(AudioBuffer.TakeOwnership(pulse, context.SampleRate));

        // The x² loudness ramp: growth that keeps growing reads as approach, not fade-in reversed.
        var samples = new float[mixed.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            double t = (double)i / System.Math.Max(1, samples.Length);
            samples[i] = (float)(mixed[i] * t * t);
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate)
            .NormalizedOrDefault(new Level(0.95))
            .Amplified(Level)
            .FittedToDuration(duration);
    }
}

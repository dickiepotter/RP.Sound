using RP.Sound.Effects;
using RP.Sound.Synthesis;

namespace RP.Sound.Ambience;

/// <summary>
/// A thunderclap heard from a distance. The physics is in what distance does to the sound: air
/// absorbs high frequencies far faster than low ones, so a close strike <em>cracks</em> and a
/// distant one only <em>rumbles</em> — the model low-passes harder with every kilometre. The body
/// of the clap is brown noise under a jagged multi-peaked envelope (the sound arrives from
/// different heights of the strike at different times).
/// </summary>
public sealed class Thunder : ISound
{
    /// <summary>Distance to the strike, metres.</summary>
    public double Distance { get; }

    public double Duration { get; }

    public Thunder(double distance = 2000)
    {
        if (distance < 0 || !double.IsFinite(distance))
            throw new ArgumentOutOfRangeException(nameof(distance), distance, "A distance must be finite and non-negative (m).");
        this.Distance = distance;
        // Distant thunder stretches: reflections and the strike's height spread the arrival out.
        this.Duration = 1.5 + System.Math.Min(4, distance / 1500.0);
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        DeterministicRandom random = context.CreateRandom($"thunder:{Distance:0.###}");
        double activeDuration = System.Math.Min(Duration, duration);

        AudioBuffer body = new Noise(NoiseColor.Brown, stream: $"thunder:{Distance:0.###}").Render(context, activeDuration);

        // The jagged envelope: 3–6 overlapping peaks, the first the strongest.
        int peaks = 3 + (int)(random.NextDouble() * 4);
        int length = body.Length;
        var envelope = new double[length];
        for (int p = 0; p < peaks; p++)
        {
            double centre = (p == 0 ? 0.12 : random.Range(0.15, 0.85)) * activeDuration;
            double width = random.Range(0.1, 0.35) * activeDuration;
            double height = p == 0 ? 1 : random.Range(0.25, 0.7);
            for (int i = 0; i < length; i++)
            {
                double t = (double)i / context.SampleRate;
                double x = (t - centre) / width;
                envelope[i] = System.Math.Max(envelope[i], height * System.Math.Exp(-x * x * 4));
            }
        }

        var samples = new float[length];
        for (int i = 0; i < length; i++) samples[i] = (float)(body[i] * envelope[i]);
        AudioBuffer clap = AudioBuffer.TakeOwnership(samples, context.SampleRate);

        // Atmospheric absorption: roughly exponential loss of highs with distance. Near strikes
        // keep a bright initial crack; far ones are all rumble.
        double cutoff = System.Math.Max(60, 8000 * System.Math.Exp(-Distance / 1200.0));
        clap = clap.LowPassed(cutoff).Amplified(new Level(System.Math.Min(1, 800.0 / System.Math.Max(200, Distance)) * 2.2));

        if (Distance < 800)
        {
            AudioBuffer crack = new Noise(NoiseColor.White, stream: $"thunder-crack:{Distance:0.###}")
                .Render(context, 0.08)
                .HighPassed(1500)
                .Amplified(new Level(1.0 - Distance / 800.0));
            clap = clap.MixedAt(crack, 0.05);
        }

        return clap.FittedToDuration(duration);
    }
}

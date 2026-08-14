using RP.Sound.Effects;
using RP.Sound.Synthesis;

namespace RP.Sound.Ambience;

/// <summary>
/// Rain: thousands of droplet impacts a second. Individually each drop is a tiny click; together
/// they fuse into the familiar wash, so the model is a two-part sum — a broadband bed for the
/// countless distant drops, plus a Poisson scatter of individually audible near ones. The surface
/// matters exactly as it does for <see cref="Physics.Impact"/>: hard surfaces make each drop
/// shorter and brighter (tin roof vs. soft ground), so hardness maps to the drops' brightness.
/// </summary>
public sealed class Rain : ISound
{
    /// <summary>0 drizzle … 1 downpour.</summary>
    public double Intensity { get; }

    /// <summary>0 soft ground … 1 tin roof: how bright each audible drop is.</summary>
    public double SurfaceHardness { get; }

    public double Duration => double.PositiveInfinity;

    public Rain(double intensity = 0.5, double surfaceHardness = 0.4)
    {
        if (intensity is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(intensity), intensity, "Rain intensity is a fraction between 0 and 1.");
        if (surfaceHardness is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(surfaceHardness), surfaceHardness, "Surface hardness is a fraction between 0 and 1.");
        this.Intensity = intensity;
        this.SurfaceHardness = surfaceHardness;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        int length = context.SampleCount(duration);
        DeterministicRandom random = context.CreateRandom($"rain:{Intensity:0.###}:{SurfaceHardness:0.###}");

        // The bed: pink noise (nature's spectrum) high-passed so it hisses rather than rumbles.
        AudioBuffer bed = new Noise(NoiseColor.Pink, stream: $"rain-bed:{Intensity:0.###}")
            .Render(context, duration)
            .HighPassed(400 + 600 * SurfaceHardness)
            .Amplified(new Level(0.25 + 0.75 * Intensity));

        // The audible drops: Poisson arrivals, each a few-millisecond ping whose brightness rises
        // with surface hardness and whose rate rises steeply with intensity.
        var drops = new float[length];
        double dropsPerSecond = 8 + Intensity * 250;
        Biquad colour = Biquad.BandPass(context.SampleRate, 1500 + 5000 * SurfaceHardness, 1.5);
        double burst = 0;
        int remaining = 0;
        for (int i = 0; i < length; i++)
        {
            if (remaining <= 0 && random.NextChance(dropsPerSecond / context.SampleRate))
            {
                remaining = (int)(context.SampleRate * (0.002 + 0.006 * random.NextDouble()));
                burst = 0.15 + 0.85 * random.NextDouble() * random.NextDouble(); // few loud, many quiet
            }

            double excitation = 0;
            if (remaining > 0)
            {
                excitation = random.NextSigned() * burst;
                remaining--;
            }

            drops[i] = (float)(colour.Process(excitation) * 2.5 * (0.3 + 0.7 * Intensity));
        }

        return bed.MixedWith(AudioBuffer.TakeOwnership(drops, context.SampleRate));
    }
}

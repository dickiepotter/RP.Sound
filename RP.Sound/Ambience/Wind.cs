using RP.Sound.Effects;
using RP.Sound.Synthesis;

namespace RP.Sound.Ambience;

/// <summary>
/// Wind. Physically, wind noise is turbulence — broadband noise shaped by whatever it blows
/// around — so the model is noise through a resonant band whose loudness and pitch ride on a slow
/// wandering gust envelope. Gusts are why static noise reads as "wind" at all: the ear keys on
/// the 0.1–1 Hz swell far more than on the hiss itself.
/// </summary>
public sealed class Wind : ISound
{
    /// <summary>0 breeze … 1 gale. Raises loudness, brightness and the whistle.</summary>
    public double Strength { get; }

    /// <summary>0 steady … 1 blustery. Depth of the slow gust swells.</summary>
    public double Gustiness { get; }

    public double Duration => double.PositiveInfinity;

    public Wind(double strength = 0.5, double gustiness = 0.5)
    {
        if (strength is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(strength), strength, "Wind strength is a fraction between 0 and 1.");
        if (gustiness is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(gustiness), gustiness, "Gustiness is a fraction between 0 and 1.");
        this.Strength = strength;
        this.Gustiness = gustiness;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        int length = context.SampleCount(duration);
        var samples = new float[length];
        DeterministicRandom random = context.CreateRandom($"wind:{Strength:0.###}:{Gustiness:0.###}");

        // The gust envelope: a random walk smoothed hard, so it swells over seconds, not samples.
        double gust = 0.5;
        double gustTarget = 0.5;
        double smoothing = System.Math.Exp(-1.0 / (0.8 * context.SampleRate));

        double centre = 200 + Strength * 500;
        const int block = 512;
        for (int start = 0; start < length; start += block)
        {
            if (random.NextChance(0.05)) gustTarget = random.NextDouble();
            double blockGust = 0;

            Biquad body = Biquad.BandPass(context.SampleRate, centre * (0.8 + 0.5 * gust), 0.5);
            // The whistle: a narrow resonance that only speaks in strong wind (wires, corners, cracks).
            Biquad whistle = Biquad.BandPass(context.SampleRate, 800 + 900 * gust * Strength, 8);
            int end = System.Math.Min(length, start + block);
            for (int i = start; i < end; i++)
            {
                gust = smoothing * gust + (1 - smoothing) * gustTarget;
                blockGust = gust;
                double noise = random.NextSigned();
                double depth = 1 - Gustiness + Gustiness * gust;
                double loudness = Strength * depth;
                samples[i] = (float)((body.Process(noise) * 2.2 + whistle.Process(noise) * 0.9 * Strength * gust) * loudness);
            }

            gust = blockGust;
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

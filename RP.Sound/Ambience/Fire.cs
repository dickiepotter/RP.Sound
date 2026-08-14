using RP.Sound.Effects;
using RP.Sound.Synthesis;

namespace RP.Sound.Ambience;

/// <summary>
/// Fire is three sounds at once, and listeners recognise all three: the low <b>roar</b> of the
/// flame itself (turbulent combustion — brown noise), the <b>hiss</b> of escaping gases and
/// steam, and the sparse bright <b>crackles</b> of wood fibres snapping (a Poisson process, like
/// PhISEM grains but rarer and ringier). Intensity moves the balance: embers are nearly all
/// crackle, an inferno nearly all roar.
/// </summary>
public sealed class Fire : ISound
{
    /// <summary>0 dying embers … 1 roaring blaze.</summary>
    public double Intensity { get; }

    public double Duration => double.PositiveInfinity;

    public Fire(double intensity = 0.5)
    {
        if (intensity is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(intensity), intensity, "Fire intensity is a fraction between 0 and 1.");
        this.Intensity = intensity;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        int length = context.SampleCount(duration);
        DeterministicRandom random = context.CreateRandom($"fire:{Intensity:0.###}");

        AudioBuffer roar = new Noise(NoiseColor.Brown, stream: $"fire-roar:{Intensity:0.###}")
            .Render(context, duration)
            .LowPassed(120 + Intensity * 280)
            .Amplified(new Level(0.2 + 1.1 * Intensity * Intensity));

        AudioBuffer hiss = new Noise(NoiseColor.Pink, stream: $"fire-hiss:{Intensity:0.###}")
            .Render(context, duration)
            .HighPassed(2500)
            .Amplified(new Level(0.03 + 0.09 * Intensity));

        // Crackles: rare sharp events, each a one-impulse strike into a random high resonance —
        // a different fibre snaps each time, so each crackle gets its own pitch.
        var crackles = new float[length];
        double cracklesPerSecond = 1.5 + Intensity * 12;
        double ring = 0, ringCoefficient = 0, ringFrequency = 0, ringPhase = 0;
        for (int i = 0; i < length; i++)
        {
            if (random.NextChance(cracklesPerSecond / context.SampleRate))
            {
                ring = 0.3 + 0.7 * random.NextDouble();
                ringFrequency = 2 * System.Math.PI * random.Range(1200, 5200) / context.SampleRate;
                ringCoefficient = System.Math.Exp(-1.0 / (random.Range(0.002, 0.012) * context.SampleRate));
                ringPhase = 0;
            }

            if (ring > 1e-4)
            {
                crackles[i] = (float)(ring * System.Math.Sin(ringPhase) * 0.9);
                ringPhase += ringFrequency;
                ring *= ringCoefficient;
            }
        }

        return roar.MixedWith(hiss).MixedWith(AudioBuffer.TakeOwnership(crackles, context.SampleRate));
    }
}

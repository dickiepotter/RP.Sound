namespace RP.Sound.Synthesis;

/// <summary>
/// The spectral colour of a noise source — how its energy is distributed across frequency.
/// </summary>
public enum NoiseColor
{
    /// <summary>Equal energy at every frequency — bright and hissy (rain on a window, tape hiss).</summary>
    White,

    /// <summary>Energy falling 3 dB per octave — equal energy per octave, how most of nature sounds (wind, waterfalls).</summary>
    Pink,

    /// <summary>Energy falling 6 dB per octave — deep rumble (distant thunder, heavy surf).</summary>
    Brown,
}

/// <summary>
/// A noise source. Deterministic like everything else: the same context and stream name always
/// produce the same "random" samples.
/// </summary>
public sealed class Noise : ISound
{
    public NoiseColor Color { get; }
    public Level Level { get; }
    public double Duration => double.PositiveInfinity;
    private readonly string stream;

    public Noise(NoiseColor color, Level? level = null, string stream = "noise")
    {
        this.Color = color;
        this.Level = level ?? Level.Unity;
        this.stream = stream;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        DeterministicRandom random = context.CreateRandom(this.stream);
        var samples = new float[context.SampleCount(duration)];
        switch (Color)
        {
            case NoiseColor.White:
                for (int i = 0; i < samples.Length; i++)
                    samples[i] = (float)(random.NextSigned() * Level.Linear);
                break;

            case NoiseColor.Pink:
            {
                // Paul Kellet's economy pink filter: three one-pole lowpasses at staggered rates,
                // summed — approximates the 1/f slope to within ±0.5 dB over the audible band.
                double b0 = 0, b1 = 0, b2 = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    double white = random.NextSigned();
                    b0 = 0.99765 * b0 + white * 0.0990460;
                    b1 = 0.96300 * b1 + white * 0.2965164;
                    b2 = 0.57000 * b2 + white * 1.0526913;
                    samples[i] = (float)((b0 + b1 + b2 + white * 0.1848) * 0.2 * Level.Linear);
                }

                break;
            }

            case NoiseColor.Brown:
            {
                // Brownian noise is integrated white noise; the small leak keeps the random walk
                // from wandering off as DC offset.
                double value = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    value = 0.998 * value + 0.02 * random.NextSigned();
                    samples[i] = (float)(value * 3.5 * Level.Linear);
                }

                break;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(Color));
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

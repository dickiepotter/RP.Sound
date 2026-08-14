using RP.Sound.Effects;

namespace RP.Sound.Physics;

/// <summary>
/// An object moving fast through air — a swung sword, a passing car, a thrown rock. The pitch
/// centre comes from real aeroacoustics: vortex shedding behind a body of diameter d at speed v
/// whistles at the Strouhal frequency f ≈ 0.2·v/d (St ≈ 0.2 across a huge range of flows — the
/// same law that makes telephone wires sing in the wind). A pass-by sweeps that centre downward
/// (the Doppler cue) with loudness peaking at the closest approach.
/// </summary>
public sealed class Whoosh : ISound
{
    /// <summary>Speed through the air, m/s.</summary>
    public double Speed { get; }

    /// <summary>Diameter of the moving object, m.</summary>
    public double Size { get; }

    public double Duration { get; }

    /// <summary>True to sweep pitch down through the pass (a fly-by); false for a stationary swish.</summary>
    public bool PassBy { get; }

    public Whoosh(double speed, double size, double duration, bool passBy = true)
    {
        if (speed <= 0 || !double.IsFinite(speed)) throw new ArgumentOutOfRangeException(nameof(speed), speed, "A whoosh speed must be finite and positive (m/s).");
        if (size <= 0 || !double.IsFinite(size)) throw new ArgumentOutOfRangeException(nameof(size), size, "A whoosh object size must be finite and positive (m).");
        if (duration <= 0 || !double.IsFinite(duration)) throw new ArgumentOutOfRangeException(nameof(duration), duration, "A whoosh duration must be finite and positive.");
        this.Speed = speed;
        this.Size = size;
        this.Duration = duration;
        this.PassBy = passBy;
    }

    /// <summary>The Strouhal shedding frequency f = St·v/d with St = 0.2, clamped into the audible band.</summary>
    public Frequency SheddingFrequency => System.Math.Clamp(0.2 * Speed / Size, 30, 4000);

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        int length = context.SampleCount(duration);
        int active = System.Math.Min(length, context.SampleCount(System.Math.Min(Duration, duration)));
        var samples = new float[length];
        DeterministicRandom random = context.CreateRandom($"whoosh:{Speed:0.###}:{Size:0.###}");

        double baseFrequency = SheddingFrequency.Hertz;
        double loudness = System.Math.Tanh(Speed / 15.0);

        const int block = 256;
        for (int start = 0; start < active; start += block)
        {
            double t = (double)start / context.SampleRate / System.Math.Min(Duration, duration);

            // Doppler-style glide: higher approaching, lower receding — ±30% over the pass.
            double sweep = PassBy ? 1.3 - 0.6 * t : 1.0;

            // Raised-cosine loudness: silent at the edges, peaking at the closest approach.
            double proximity = System.Math.Sin(System.Math.PI * System.Math.Clamp(t, 0, 1));

            Biquad body = Biquad.BandPass(context.SampleRate, baseFrequency * sweep, 1.2);
            Biquad turbulence = Biquad.BandPass(context.SampleRate, System.Math.Min(6000, baseFrequency * sweep * 6), 0.9);
            int end = System.Math.Min(active, start + block);
            for (int i = start; i < end; i++)
            {
                double noise = random.NextSigned();
                samples[i] = (float)((body.Process(noise) * 2.2 + turbulence.Process(noise) * 0.5) * proximity * proximity * loudness * 2);
            }
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

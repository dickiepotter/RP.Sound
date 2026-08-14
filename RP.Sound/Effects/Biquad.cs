namespace RP.Sound.Effects;

/// <summary>
/// The standard two-pole/two-zero digital filter section, with coefficients from Robert
/// Bristow-Johnson's Audio EQ Cookbook — the de-facto reference formulas for audio filters.
/// This is transient render state (like <see cref="DeterministicRandom"/>), not a description,
/// so it is mutable and internal; the public API is the immutable filtered-sound descriptions.
/// </summary>
internal sealed class Biquad
{
    private readonly double b0, b1, b2, a1, a2;
    private double x1, x2, y1, y2;

    private Biquad(double b0, double b1, double b2, double a0, double a1, double a2)
    {
        // Normalising by a0 up front saves a divide per sample.
        this.b0 = b0 / a0;
        this.b1 = b1 / a0;
        this.b2 = b2 / a0;
        this.a1 = a1 / a0;
        this.a2 = a2 / a0;
    }

    public static Biquad LowPass(int sampleRate, double cutoff, double q)
    {
        (double sin, double cos, double alpha) = Prepare(sampleRate, cutoff, q);
        return new Biquad((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
    }

    public static Biquad HighPass(int sampleRate, double cutoff, double q)
    {
        (double sin, double cos, double alpha) = Prepare(sampleRate, cutoff, q);
        return new Biquad((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
    }

    public static Biquad BandPass(int sampleRate, double centre, double q)
    {
        (double sin, double cos, double alpha) = Prepare(sampleRate, centre, q);
        return new Biquad(alpha, 0, -alpha, 1 + alpha, -2 * cos, 1 - alpha);
    }

    private static (double Sin, double Cos, double Alpha) Prepare(int sampleRate, double frequency, double q)
    {
        // Clamp to just below Nyquist: a corner at or past half the sample rate has no meaning
        // and sends the coefficient formulas unstable.
        double clamped = System.Math.Clamp(frequency, 1, sampleRate * 0.49);
        double omega = 2 * System.Math.PI * clamped / sampleRate;
        double sin = System.Math.Sin(omega);
        return (sin, System.Math.Cos(omega), sin / (2 * System.Math.Max(0.05, q)));
    }

    public double Process(double x)
    {
        double y = this.b0 * x + this.b1 * this.x1 + this.b2 * this.x2 - this.a1 * this.y1 - this.a2 * this.y2;
        this.x2 = this.x1;
        this.x1 = x;
        this.y2 = this.y1;
        this.y1 = y;
        return y;
    }
}

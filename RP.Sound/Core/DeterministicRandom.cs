namespace RP.Sound;

/// <summary>
/// A small, fast, deterministic pseudo-random generator (xorshift64*).
/// The library never uses <see cref="System.Random"/>: its algorithm is not guaranteed stable
/// across runtime versions, and determinism — same description + same seed ⇒ identical samples —
/// is a library-wide convention. A generator is transient render state, not a description,
/// so unlike the descriptions it is deliberately mutable.
/// </summary>
public sealed class DeterministicRandom
{
    private ulong state;
    private double? cachedGaussian;

    public DeterministicRandom(ulong seed)
    {
        // xorshift has a single forbidden state (all zero bits); nudge it onto the golden-ratio
        // constant so a zero seed is valid input rather than a silent degenerate generator.
        this.state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
    }

    private ulong NextUInt64()
    {
        this.state ^= this.state >> 12;
        this.state ^= this.state << 25;
        this.state ^= this.state >> 27;
        return this.state * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>A uniform double in [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>A uniform double in [-1, 1).</summary>
    public double NextSigned() => NextDouble() * 2.0 - 1.0;

    /// <summary>A uniform double in [min, max).</summary>
    public double Range(double min, double max) => min + NextDouble() * (max - min);

    /// <summary>True with the given probability (0 = never, 1 = always).</summary>
    public bool NextChance(double probability) => NextDouble() < probability;

    /// <summary>A standard-normal sample (Box–Muller, pairs cached).</summary>
    public double NextGaussian()
    {
        if (this.cachedGaussian is double cached)
        {
            this.cachedGaussian = null;
            return cached;
        }

        // Box–Muller needs u1 strictly above zero for the logarithm.
        double u1 = 1.0 - NextDouble();
        double u2 = NextDouble();
        double radius = System.Math.Sqrt(-2.0 * System.Math.Log(u1));
        this.cachedGaussian = radius * System.Math.Sin(2.0 * System.Math.PI * u2);
        return radius * System.Math.Cos(2.0 * System.Math.PI * u2);
    }
}

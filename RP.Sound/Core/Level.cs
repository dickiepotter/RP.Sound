namespace RP.Sound;

/// <summary>
/// An amount of loudness — a linear amplitude gain wrapped in a dedicated type so a bare
/// <see cref="double"/> never leaves you guessing whether it is linear gain or decibels
/// (the same discipline RP.Math applies to angles with its <c>Angle</c> type).
/// Stored linear; read or written as decibels via <see cref="FromDecibels"/> / <see cref="Decibels"/>.
/// </summary>
public readonly struct Level : IEquatable<Level>, IComparable<Level>
{
    /// <summary>The linear amplitude multiplier (1 = unchanged, 0 = silence).</summary>
    public double Linear { get; }

    public Level(double linear)
    {
        if (linear < 0 || !double.IsFinite(linear))
            throw new ArgumentOutOfRangeException(nameof(linear), linear, "A level is a linear amplitude gain and must be finite and non-negative.");
        this.Linear = linear;
    }

    /// <summary>No change: a gain of 1 (0 dB).</summary>
    public static readonly Level Unity = new(1);

    /// <summary>No sound at all: a gain of 0 (−∞ dB).</summary>
    public static readonly Level Silence = new(0);

    /// <summary>Half power, the everyday “noticeably quieter” step: −6 dB.</summary>
    public static readonly Level Half = FromDecibels(-6);

    /// <summary>
    /// Builds a level from decibels: dB = 20·log₁₀(gain), so gain = 10^(dB/20).
    /// Decibels are the perceptual scale — equal dB steps sound like equal loudness steps.
    /// </summary>
    public static Level FromDecibels(double decibels) => new(System.Math.Pow(10, decibels / 20.0));

    /// <summary>This level expressed in decibels (−∞ for silence).</summary>
    public double Decibels => Linear <= 0 ? double.NegativeInfinity : 20.0 * System.Math.Log10(Linear);

    /// <summary>Gains compose by multiplying their linear values (equivalently, adding their dB).</summary>
    public static Level operator *(Level a, Level b) => new(a.Linear * b.Linear);

    // Reading a level out as its linear gain is lossless, so the cast is implicit;
    // building one from a bare double asserts the non-negative precondition, so that cast is explicit.
    public static implicit operator double(Level level) => level.Linear;
    public static explicit operator Level(double linear) => new(linear);

    public int CompareTo(Level other) => Linear.CompareTo(other.Linear);
    public bool Equals(Level other) => Linear.Equals(other.Linear);
    public override bool Equals(object? obj) => obj is Level other && Equals(other);
    public override int GetHashCode() => Linear.GetHashCode();
    public static bool operator ==(Level a, Level b) => a.Equals(b);
    public static bool operator !=(Level a, Level b) => !a.Equals(b);
    public static bool operator <(Level a, Level b) => a.Linear < b.Linear;
    public static bool operator >(Level a, Level b) => a.Linear > b.Linear;
    public static bool operator <=(Level a, Level b) => a.Linear <= b.Linear;
    public static bool operator >=(Level a, Level b) => a.Linear >= b.Linear;

    public override string ToString() => Linear <= 0 ? "-inf dB" : $"{Decibels:0.##} dB";
}

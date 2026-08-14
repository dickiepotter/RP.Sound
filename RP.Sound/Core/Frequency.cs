namespace RP.Sound;

/// <summary>
/// A rate of vibration wrapped in a dedicated type, stored in hertz but convertible to and from
/// MIDI note numbers and note names ("A4", "C#3") — so a bare <see cref="double"/> never leaves
/// you guessing which unit it is in. Mirrors the RP.Math <c>Angle</c> design: a bare number
/// converts implicitly and is treated as hertz.
/// </summary>
public readonly struct Frequency : IEquatable<Frequency>, IComparable<Frequency>
{
    /// <summary>Cycles per second.</summary>
    public double Hertz { get; }

    public Frequency(double hertz)
    {
        if (hertz < 0 || !double.IsFinite(hertz))
            throw new ArgumentOutOfRangeException(nameof(hertz), hertz, "A frequency must be finite and non-negative.");
        this.Hertz = hertz;
    }

    /// <summary>Concert pitch A4 — the tuning reference for the MIDI mapping.</summary>
    public static readonly Frequency A440 = new(440);

    /// <summary>Middle C (C4, MIDI 60).</summary>
    public static readonly Frequency MiddleC = FromMidiNote(60);

    /// <summary>
    /// Equal temperament: each of the 12 semitones in an octave multiplies frequency by 2^(1/12),
    /// and MIDI note 69 is defined as 440 Hz — so note n is 440·2^((n−69)/12).
    /// </summary>
    public static Frequency FromMidiNote(double note) => new(440.0 * System.Math.Pow(2, (note - 69.0) / 12.0));

    /// <summary>The MIDI note number of this frequency (fractional between semitones).</summary>
    public double MidiNote => Hertz <= 0
        ? throw new InvalidOperationException("A zero frequency has no pitch, so it has no MIDI note.")
        : 69.0 + 12.0 * System.Math.Log2(Hertz / 440.0);

    /// <summary>Parses a note name such as "A4", "C#3" or "Eb2". Throws on anything else.</summary>
    public static Frequency FromNote(string name) =>
        TryFromNote(name, out Frequency frequency)
            ? frequency
            : throw new FormatException($"'{name}' is not a note name (expected letter A–G, optional # or b, then an octave, e.g. \"C#4\").");

    /// <summary>The safe form of <see cref="FromNote"/>: false instead of throwing.</summary>
    public static bool TryFromNote(string? name, out Frequency frequency)
    {
        frequency = default;
        if (string.IsNullOrEmpty(name) || name.Length < 2) return false;

        // Semitone offset of each natural note within the octave, C = 0.
        int semitone = char.ToUpperInvariant(name[0]) switch
        {
            'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5, 'G' => 7, 'A' => 9, 'B' => 11,
            _ => -1,
        };
        if (semitone < 0) return false;

        int index = 1;
        if (name[index] == '#') { semitone++; index++; }
        else if (name[index] == 'b') { semitone--; index++; }

        if (!int.TryParse(name[index..], out int octave)) return false;

        // MIDI convention: C4 = 60, so C of octave k is 12·(k+1).
        frequency = FromMidiNote(12 * (octave + 1) + semitone);
        return true;
    }

    /// <summary>This frequency shifted by a number of equal-temperament semitones (may be fractional or negative).</summary>
    public Frequency Transposed(double semitones) => new(Hertz * System.Math.Pow(2, semitones / 12.0));

    // A bare number is treated as hertz, exactly as RP.Math treats a bare double as radians.
    public static implicit operator Frequency(double hertz) => new(hertz);
    public static implicit operator double(Frequency frequency) => frequency.Hertz;

    public int CompareTo(Frequency other) => Hertz.CompareTo(other.Hertz);
    public bool Equals(Frequency other) => Hertz.Equals(other.Hertz);
    public override bool Equals(object? obj) => obj is Frequency other && Equals(other);
    public override int GetHashCode() => Hertz.GetHashCode();
    public static bool operator ==(Frequency a, Frequency b) => a.Equals(b);
    public static bool operator !=(Frequency a, Frequency b) => !a.Equals(b);
    public static bool operator <(Frequency a, Frequency b) => a.Hertz < b.Hertz;
    public static bool operator >(Frequency a, Frequency b) => a.Hertz > b.Hertz;
    public static bool operator <=(Frequency a, Frequency b) => a.Hertz <= b.Hertz;
    public static bool operator >=(Frequency a, Frequency b) => a.Hertz >= b.Hertz;

    public override string ToString() => $"{Hertz:0.###} Hz";
}

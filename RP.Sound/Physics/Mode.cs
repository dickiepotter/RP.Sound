namespace RP.Sound.Physics;

/// <summary>
/// One resonant mode of a vibrating object: a frequency it likes to ring at, how long that
/// ringing lasts, and how strongly the mode speaks. A struck object's sound is (very nearly)
/// nothing but its modes — a sum of decaying sine waves. This decomposition is modal synthesis,
/// the standard physically-based model for contact sounds (van den Doel &amp; Pai's FoleyAutomatic).
/// </summary>
public readonly struct Mode
{
    public Frequency Frequency { get; }

    /// <summary>The time constant of the exponential die-away, seconds (amplitude ∝ e^(−t/decay)).</summary>
    public double DecaySeconds { get; }

    public Level Level { get; }

    public Mode(Frequency frequency, double decaySeconds, Level level)
    {
        if (decaySeconds <= 0 || !double.IsFinite(decaySeconds))
            throw new ArgumentOutOfRangeException(nameof(decaySeconds), decaySeconds, "A mode's decay time must be finite and positive.");
        this.Frequency = frequency;
        this.DecaySeconds = decaySeconds;
        this.Level = level;
    }

    public override string ToString() => $"{Frequency} for {DecaySeconds:0.###} s at {Level}";
}

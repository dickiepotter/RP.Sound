using RP.Sound.Effects;

namespace RP.Sound.Mixing;

/// <summary>
/// Where a sound sits relative to the listener — pan across the stereo field and distance away.
/// Distance applies two physical cues together: inverse-distance loudness (6 dB per doubling,
/// referenced to 1 m) and air absorption, which eats high frequencies first — the reason far
/// things sound both quiet <em>and</em> muffled, and why loudness alone never reads as "far".
/// </summary>
public readonly struct SoundPlacement
{
    /// <summary>−1 hard left … 0 centre … +1 hard right.</summary>
    public double Pan { get; }

    /// <summary>Distance from the listener, metres (1 m is the neutral reference).</summary>
    public double Distance { get; }

    public SoundPlacement(double pan = 0, double distance = 1)
    {
        if (pan is < -1 or > 1) throw new ArgumentOutOfRangeException(nameof(pan), pan, "Pan runs from −1 (left) to +1 (right).");
        if (distance < 0 || !double.IsFinite(distance)) throw new ArgumentOutOfRangeException(nameof(distance), distance, "A distance must be finite and non-negative (m).");
        this.Pan = pan;
        this.Distance = distance;
    }

    /// <summary>Directly in front at the reference distance — placement that changes nothing.</summary>
    public static readonly SoundPlacement Here = new();

    /// <summary>Inverse-distance gain, clamped inside the 1 m reference so nearby sounds do not blow up.</summary>
    public Level DistanceAttenuation => new(1.0 / System.Math.Max(1, Distance));

    /// <summary>The air-absorption cutoff: full bandwidth at the listener, darker with every metre.</summary>
    public Frequency AirAbsorptionCutoff => new(System.Math.Max(300, 18000 * System.Math.Exp(-Distance / 60.0)));

    /// <summary>Applies distance (gain + air), then pans — the mono world becoming stereo.</summary>
    public StereoBuffer Apply(AudioBuffer mono)
    {
        AudioBuffer travelled = Distance > 1 ? mono.LowPassed(AirAbsorptionCutoff) : mono;
        return StereoBuffer.FromMono(travelled.Amplified(DistanceAttenuation), Pan);
    }

    public override string ToString() => $"pan {Pan:+0.##;-0.##;centre}, {Distance:0.##} m";
}

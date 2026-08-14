namespace RP.Sound.Mixing;

/// <summary>
/// A finished stereo mix: a left and a right <see cref="AudioBuffer"/> of equal length and rate.
/// Mono until the mix is the library's deliberate split: a sound describes <em>what</em> is heard
/// and stays mono; <em>where</em> it sits (pan, distance) belongs to its placement in the mix —
/// the same separation RP.Math draws between a conceptual shape and its placing Pose.
/// </summary>
public sealed class StereoBuffer
{
    public AudioBuffer Left { get; }
    public AudioBuffer Right { get; }

    public StereoBuffer(AudioBuffer left, AudioBuffer right)
    {
        if (left.SampleRate != right.SampleRate)
            throw new ArgumentException($"Stereo channels must share a sample rate ({left.SampleRate} and {right.SampleRate}).", nameof(right));
        if (left.Length != right.Length)
            throw new ArgumentException($"Stereo channels must be the same length ({left.Length} and {right.Length} samples).", nameof(right));
        this.Left = left;
        this.Right = right;
    }

    public int SampleRate => Left.SampleRate;
    public int Length => Left.Length;
    public double Duration => Left.Duration;

    /// <summary>
    /// Places a mono buffer in the stereo field with equal-power panning: gains of cos θ and
    /// sin θ, so the total acoustic power stays constant as a sound moves across — a linear
    /// crossfade would dip audibly in the middle.
    /// </summary>
    public static StereoBuffer FromMono(AudioBuffer mono, double pan = 0)
    {
        if (pan is < -1 or > 1) throw new ArgumentOutOfRangeException(nameof(pan), pan, "Pan runs from −1 (left) to +1 (right).");
        double angle = (pan + 1) * System.Math.PI / 4;
        return new StereoBuffer(
            mono.Amplified(new Level(System.Math.Cos(angle))),
            mono.Amplified(new Level(System.Math.Sin(angle))));
    }

    public StereoBuffer MixedWith(StereoBuffer other) =>
        new(Left.MixedWith(other.Left), Right.MixedWith(other.Right));

    public Level PeakLevel => Left.PeakLevel > Right.PeakLevel ? Left.PeakLevel : Right.PeakLevel;

    /// <summary>Scales both channels together so the louder one peaks at the target (silence unchanged).</summary>
    public StereoBuffer NormalizedOrDefault(Level target)
    {
        Level peak = PeakLevel;
        if (peak.Linear <= 0) return this;
        var gain = new Level(target.Linear / peak.Linear);
        return new StereoBuffer(Left.Amplified(gain), Right.Amplified(gain));
    }

    public StereoBuffer SoftClipped() => new(Left.SoftClipped(), Right.SoftClipped());
}

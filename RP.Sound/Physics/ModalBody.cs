namespace RP.Sound.Physics;

/// <summary>
/// A physical object that can ring: a <see cref="Material"/> given a size. From those two facts
/// alone it derives its resonant <see cref="Modes"/>, using the vibration formula for a free bar
/// (the classic struck object — think xylophone key, dropped plank, ringing pipe):
///
///   f₁ = (k₁²/2π) · (h/L²) · √(E/12ρ)      with k₁L = 4.730 for a free–free bar
///
/// Higher modes sit at the bar's fixed inharmonic ratios (≈ 2.76, 5.40, 8.93 × the fundamental) —
/// which is why struck objects "clang" rather than sound musical. Each mode decays in
/// 1/(π·f·η) seconds, so brighter modes die faster and every object mellows as it rings.
/// </summary>
public sealed class ModalBody
{
    /// <summary>Frequency ratios of a free–free bar's transverse modes: ((kₙL)/(k₁L))² for kₙL = 4.730, 7.853, 10.996, 14.137, 17.279.</summary>
    private static readonly double[] BarRatios = { 1.0, 2.756, 5.404, 8.933, 13.345 };

    public Material Material { get; }

    /// <summary>Characteristic length L, metres. The bar's thickness is assumed to be L/10.</summary>
    public double Size { get; }

    /// <summary>The derived resonant modes, loudest (and lowest) first.</summary>
    public IReadOnlyList<Mode> Modes { get; }

    /// <summary>A default mass for impact energy: the bar's volume L·(L/10)² times its density.</summary>
    public double Mass => Material.Density * Size * Size * Size / 100.0;

    public ModalBody(Material material, double size)
    {
        if (size <= 0 || !double.IsFinite(size))
            throw new ArgumentOutOfRangeException(nameof(size), size, "A body's size must be finite and positive (metres).");
        this.Material = material;
        this.Size = size;

        // Free–free bar fundamental. h = L/10 keeps proportions constant so size alone moves pitch:
        // halve the length and (h/L² halving too) the pitch doubles — as struck bars really behave.
        double thickness = size / 10.0;
        double stiffnessTerm = System.Math.Sqrt(material.YoungsModulus / (12.0 * material.Density));
        double fundamental = (4.730 * 4.730 / (2 * System.Math.PI)) * (thickness / (size * size)) * stiffnessTerm;

        // Very small objects would ring above hearing; fold the fundamental down into the audible
        // band rather than producing silence (a coin still clinks — its lowest usable mode does).
        while (fundamental > 8000) fundamental /= 2;
        fundamental = System.Math.Max(fundamental, 25);

        var modes = new List<Mode>();
        for (int i = 0; i < BarRatios.Length; i++)
        {
            double frequency = fundamental * BarRatios[i];
            if (frequency > 18000) break; // beyond hearing (and courting aliasing)

            // Decay time constant of a mode with loss factor η: τ = 1/(π·f·η). Clamped so steel
            // does not ring for minutes and rubber is not over before it starts.
            double decay = System.Math.Clamp(1.0 / (System.Math.PI * frequency * material.LossFactor), 0.005, 6.0);

            // Struck bars put most energy into the lowest modes; 1/(n+1) is the simple honest profile.
            modes.Add(new Mode(frequency, decay, new Level(1.0 / (i + 1))));
        }

        this.Modes = modes;
    }

    public override string ToString() => $"{Material} bar, {Size:0.###} m ({Modes.Count} modes from {Modes[0].Frequency})";
}

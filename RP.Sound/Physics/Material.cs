namespace RP.Sound.Physics;

/// <summary>
/// What something is made of, described by the real physical properties that shape how it sounds.
/// All units are SI. The two big levers: stiffness-to-density ratio (E/ρ) sets how <em>high</em>
/// an object of a given size rings (√(E/ρ) is the speed of sound in the material), and the loss
/// factor sets how <em>long</em> it rings (steel sings for seconds, wood thuds in milliseconds).
/// </summary>
public sealed class Material
{
    public string Name { get; }

    /// <summary>Mass per volume, kg/m³. Heavier materials carry more energy into an impact.</summary>
    public double Density { get; }

    /// <summary>Young's modulus, Pa — stiffness. Stiffer (relative to density) rings higher.</summary>
    public double YoungsModulus { get; }

    /// <summary>
    /// Loss factor η — the fraction of vibrational energy lost per radian of vibration.
    /// Metal ~0.0002 (rings for seconds); wood ~0.02 (thuds); rubber ~0.15 (barely a thump).
    /// A mode's decay time is 1/(π·f·η), so in every material the high modes die first.
    /// </summary>
    public double LossFactor { get; }

    /// <summary>0 soft … 1 hard. Harder contact is shorter contact, which excites brighter modes.</summary>
    public double Hardness { get; }

    /// <summary>
    /// Coefficient of restitution: the fraction of impact <em>speed</em> kept after a bounce
    /// (0 = dead drop, 1 = perfectly elastic). Drives <see cref="BounceSequence"/> timing.
    /// </summary>
    public double Restitution { get; }

    public Material(string name, double density, double youngsModulus, double lossFactor, double hardness, double restitution)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A material needs a name.", nameof(name));
        if (density <= 0 || !double.IsFinite(density)) throw new ArgumentOutOfRangeException(nameof(density), density, "Density must be finite and positive (kg/m³).");
        if (youngsModulus <= 0 || !double.IsFinite(youngsModulus)) throw new ArgumentOutOfRangeException(nameof(youngsModulus), youngsModulus, "Young's modulus must be finite and positive (Pa).");
        if (lossFactor <= 0 || lossFactor > 1) throw new ArgumentOutOfRangeException(nameof(lossFactor), lossFactor, "The loss factor is a small positive fraction (0, 1].");
        if (hardness is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(hardness), hardness, "Hardness is a fraction between 0 and 1.");
        if (restitution is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(restitution), restitution, "Restitution is a fraction between 0 and 1.");
        this.Name = name;
        this.Density = density;
        this.YoungsModulus = youngsModulus;
        this.LossFactor = lossFactor;
        this.Hardness = hardness;
        this.Restitution = restitution;
    }

    /// <summary>√(E/ρ): the speed of sound inside the material, m/s — its "voice speed".</summary>
    public double SoundSpeed => System.Math.Sqrt(YoungsModulus / Density);

    // The presets use handbook values (density and Young's modulus are measured constants;
    // loss factor, hardness and restitution are representative mid-range figures).
    public static readonly Material Steel = new("steel", 7850, 200e9, 0.0002, 0.90, 0.75);
    public static readonly Material Glass = new("glass", 2500, 70e9, 0.001, 0.95, 0.80);
    public static readonly Material Wood = new("wood", 700, 12e9, 0.02, 0.60, 0.50);
    public static readonly Material Stone = new("stone", 2700, 50e9, 0.004, 0.85, 0.45);
    public static readonly Material Plastic = new("plastic", 1200, 3e9, 0.03, 0.50, 0.55);
    public static readonly Material Rubber = new("rubber", 1100, 0.05e9, 0.15, 0.20, 0.85);
    public static readonly Material Ceramic = new("ceramic", 2400, 70e9, 0.0008, 0.95, 0.65);
    public static readonly Material Ice = new("ice", 917, 9e9, 0.008, 0.80, 0.40);

    /// <summary>All the built-in materials, for enumeration (e.g. by a UI).</summary>
    public static IReadOnlyList<Material> Presets { get; } =
        new[] { Steel, Glass, Wood, Stone, Plastic, Rubber, Ceramic, Ice };

    /// <summary>Finds a preset by name (case-insensitive). Strict form of <see cref="TryFromName"/>.</summary>
    public static Material FromName(string name) =>
        TryFromName(name, out Material? material) ? material : throw new ArgumentException($"No material preset named '{name}'.", nameof(name));

    public static bool TryFromName(string? name, out Material material)
    {
        foreach (Material preset in Presets)
        {
            if (string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                material = preset;
                return true;
            }
        }

        material = Steel;
        return false;
    }

    public override string ToString() => Name;
}

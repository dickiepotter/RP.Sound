using RP.Sound.Effects;

namespace RP.Sound.Physics;

/// <summary>
/// A loose granular surface — gravel, sand, dry leaves, snow — modelled by Perry Cook's PhISEM
/// (Physically Informed Stochastic Event Modeling): instead of tracking thousands of grains,
/// treat their collisions as a Poisson process whose rate follows the system's energy. Each
/// collision is a tiny ping through the grain's resonance. One shot of energy (a footfall, a
/// shake) then decays away, and the crunch thins out with it — which is precisely how a real
/// handful of gravel settles.
/// </summary>
public sealed class ParticleSurface : ISound
{
    public string Name { get; }

    /// <summary>Collisions per second while the system is at full energy.</summary>
    public double CollisionRate { get; }

    /// <summary>The resonant frequency of a single grain collision.</summary>
    public Frequency GrainFrequency { get; }

    /// <summary>Resonance sharpness of a grain ping.</summary>
    public double GrainQ { get; }

    /// <summary>Time constant of the system-energy decay after the excitation, seconds.</summary>
    public double EnergyDecay { get; }

    /// <summary>How much energy the event puts in, 0…1 (a stamp vs. a tiptoe).</summary>
    public double Energy { get; }

    public ParticleSurface(string name, double collisionRate, Frequency grainFrequency, double grainQ, double energyDecay, double energy = 1)
    {
        if (collisionRate <= 0 || !double.IsFinite(collisionRate)) throw new ArgumentOutOfRangeException(nameof(collisionRate), collisionRate, "The collision rate must be finite and positive (per second).");
        if (grainQ <= 0 || !double.IsFinite(grainQ)) throw new ArgumentOutOfRangeException(nameof(grainQ), grainQ, "The grain resonance Q must be finite and positive.");
        if (energyDecay <= 0 || !double.IsFinite(energyDecay)) throw new ArgumentOutOfRangeException(nameof(energyDecay), energyDecay, "The energy decay must be finite and positive (seconds).");
        if (energy is <= 0 or > 1) throw new ArgumentOutOfRangeException(nameof(energy), energy, "Energy is a fraction in (0, 1].");
        this.Name = name;
        this.CollisionRate = collisionRate;
        this.GrainFrequency = grainFrequency;
        this.GrainQ = grainQ;
        this.EnergyDecay = energyDecay;
        this.Energy = energy;
    }

    // Presets tuned by ear around Cook's published shaker parameters: what varies is grain
    // brightness (frequency), how many grains move (rate) and how quickly they settle (decay).
    public static ParticleSurface Gravel(double energy = 1) => new("gravel", 900, 3300, 3.0, 0.12, energy);
    public static ParticleSurface Sand(double energy = 1) => new("sand", 6000, 5500, 1.2, 0.08, energy);
    public static ParticleSurface Leaves(double energy = 1) => new("leaves", 400, 2500, 1.5, 0.18, energy);
    public static ParticleSurface Snow(double energy = 1) => new("snow", 1600, 1100, 0.8, 0.10, energy);

    public static IReadOnlyList<string> PresetNames { get; } = new[] { "gravel", "sand", "leaves", "snow" };

    public static ParticleSurface FromName(string name, double energy = 1) => name.ToLowerInvariant() switch
    {
        "gravel" => Gravel(energy),
        "sand" => Sand(energy),
        "leaves" => Leaves(energy),
        "snow" => Snow(energy),
        _ => throw new ArgumentException($"No particle surface preset named '{name}'.", nameof(name)),
    };

    public double Duration => EnergyDecay * 5;

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        int length = context.SampleCount(duration);
        int active = System.Math.Min(length, context.SampleCount(System.Math.Min(Duration, duration)));
        var samples = new float[length];
        DeterministicRandom random = context.CreateRandom($"particles:{Name}:{Energy:0.###}");

        Biquad resonator = Biquad.BandPass(context.SampleRate, GrainFrequency.Hertz, GrainQ);
        Biquad clickPath = Biquad.HighPass(context.SampleRate, GrainFrequency.Hertz * 0.8, 0.707);

        double energyPerSample = 1.0 / (EnergyDecay * context.SampleRate);
        double systemEnergy = Energy;
        for (int i = 0; i < active; i++)
        {
            // PhISEM's core line: collisions arrive at random, at a rate proportional to how much
            // energy is still sloshing about.
            double impulse = 0;
            if (random.NextChance(CollisionRate * systemEnergy / context.SampleRate))
                impulse = (0.3 + 0.7 * random.NextDouble()) * systemEnergy * (random.NextChance(0.5) ? 1 : -1);

            samples[i] = (float)((resonator.Process(impulse) * 6 + clickPath.Process(impulse) * 2) * 1.2);
            systemEnergy = System.Math.Max(0, systemEnergy - systemEnergy * energyPerSample);
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

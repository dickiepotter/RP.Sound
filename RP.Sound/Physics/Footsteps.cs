namespace RP.Sound.Physics;

/// <summary>
/// A walker crossing a surface. A footstep is not one sound but two — the heel strike then the
/// toe slap — and the gap between them closes as the gait speeds up until a run merges them.
/// Cadence comes from the physics of walking: steps ≈ speed ÷ stride length (~0.75 m), so the
/// rhythm follows the character's actual velocity with no animation timing to hand-tune.
/// Hard surfaces ring like the material they are (modal impacts); loose surfaces crunch
/// (a PhISEM <see cref="ParticleSurface"/> burst per step).
/// </summary>
public sealed class Footsteps : ISound
{
    /// <summary>The hard surface underfoot, if this is a hard-surface walker.</summary>
    public Material? Surface { get; }

    /// <summary>The loose surface underfoot, if this is a granular-surface walker.</summary>
    public ParticleSurface? LooseSurface { get; }

    /// <summary>Walking speed, m/s (1.4 is a stroll, 3+ is a run).</summary>
    public double Speed { get; }

    /// <summary>Walker's mass, kg — a heavier tread strikes harder.</summary>
    public double Weight { get; }

    public double Duration { get; }

    private Footsteps(Material? surface, ParticleSurface? looseSurface, double speed, double duration, double weight)
    {
        if (speed <= 0 || !double.IsFinite(speed)) throw new ArgumentOutOfRangeException(nameof(speed), speed, "A walking speed must be finite and positive (m/s).");
        if (duration < 0 || !double.IsFinite(duration)) throw new ArgumentOutOfRangeException(nameof(duration), duration, "A footsteps duration must be finite and non-negative.");
        if (weight <= 0 || !double.IsFinite(weight)) throw new ArgumentOutOfRangeException(nameof(weight), weight, "A walker's weight must be finite and positive (kg).");
        this.Surface = surface;
        this.LooseSurface = looseSurface;
        this.Speed = speed;
        this.Duration = duration;
        this.Weight = weight;
    }

    /// <summary>Footsteps on a hard surface — a floor of the given material.</summary>
    public Footsteps(Material surface, double speed, double duration, double weight = 75)
        : this(surface, null, speed, duration, weight)
    {
    }

    /// <summary>Footsteps on a loose granular surface — gravel, sand, leaves, snow.</summary>
    public Footsteps(ParticleSurface surface, double speed, double duration, double weight = 75)
        : this(null, surface, speed, duration, weight)
    {
    }

    /// <summary>Steps per second: speed over stride length, kept within a plausible human gait.</summary>
    public double Cadence => System.Math.Clamp(Speed / 0.75, 0.6, 5);

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        DeterministicRandom random = context.CreateRandom($"steps:{Surface?.Name ?? LooseSurface!.Name}:{Speed:0.###}");
        double interval = 1.0 / Cadence;

        // The heel–toe gap shrinks with pace and vanishes into a run.
        double heelToeGap = System.Math.Max(0.02, 0.14 - Speed * 0.03);
        double stepVelocity = (0.4 + Speed * 0.25) * (Weight / 75.0);

        AudioBuffer step;
        if (Surface is not null)
        {
            // The "instrument" is a floor board of the surface material; the foot is a soft striker.
            var board = new ModalBody(Surface, 0.6);
            var heel = new Impact(board, stepVelocity, Weight * 0.03, strikerHardness: 0.35);
            var toe = new Impact(board, stepVelocity * 0.5, Weight * 0.02, strikerHardness: 0.3);
            step = heel.MixedWith(toe.Delayed(heelToeGap)).Render(context, System.Math.Min(0.7, heel.Duration + heelToeGap));
        }
        else
        {
            ParticleSurface crunch = LooseSurface!;
            var heel = new ParticleSurface(crunch.Name, crunch.CollisionRate, crunch.GrainFrequency, crunch.GrainQ, crunch.EnergyDecay, System.Math.Min(1, crunch.Energy * (0.6 + stepVelocity * 0.3)));
            step = heel.Render(context, System.Math.Min(0.7, heel.Duration));
        }

        AudioBuffer result = AudioBuffer.Silence(duration, context.SampleRate);
        double time = 0.05;
        int foot = 0;
        while (time < System.Math.Min(Duration, duration))
        {
            // No two footfalls are identical: ±1.5 dB and ±4% timing, alternating feet slightly offset.
            double gain = System.Math.Pow(10, random.Range(-1.5, 1.5) / 20) * (foot % 2 == 0 ? 1 : 0.92);
            result = result.MixedAt(step.Amplified(new Level(gain)), time);
            time += interval * (1 + 0.04 * random.NextSigned());
            foot++;
        }

        return result.FittedToDuration(duration);
    }
}

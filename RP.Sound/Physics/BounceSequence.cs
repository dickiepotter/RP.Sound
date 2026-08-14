namespace RP.Sound.Physics;

/// <summary>
/// A dropped object bouncing to rest. Pure projectile physics sets the rhythm: an object leaving
/// the floor at speed v is back after 2v/g seconds, and each bounce keeps only the restitution
/// fraction e of its speed — so both the intervals and the loudness shrink geometrically. That
/// accelerating "bdd-d-d-drrp" pattern is the audible signature of a bounce, and it falls
/// straight out of v_{k+1} = e·v_k with no hand animation.
/// </summary>
public sealed class BounceSequence : ISound
{
    public ModalBody Body { get; }

    /// <summary>Speed of the first impact, m/s.</summary>
    public double InitialVelocity { get; }

    /// <summary>Fraction of speed kept per bounce (defaults to the body material's restitution).</summary>
    public double Restitution { get; }

    /// <summary>m/s², explicit so a moon bounce is one parameter away.</summary>
    public double Gravity { get; }

    /// <summary>
    /// The computed schedule: when each impact lands and how fast, first bounce first. Public so
    /// a game can sync visuals (or tests can check the physics) against the audio.
    /// </summary>
    public IReadOnlyList<(double Time, double Velocity)> Bounces { get; }

    public BounceSequence(ModalBody body, double initialVelocity, double? restitution = null, double gravity = 9.81)
    {
        if (initialVelocity <= 0 || !double.IsFinite(initialVelocity))
            throw new ArgumentOutOfRangeException(nameof(initialVelocity), initialVelocity, "The initial impact velocity must be finite and positive (m/s).");
        if (gravity <= 0 || !double.IsFinite(gravity))
            throw new ArgumentOutOfRangeException(nameof(gravity), gravity, "Gravity must be finite and positive (m/s²).");
        double e = restitution ?? body.Material.Restitution;
        if (e is < 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(restitution), e, "Restitution must be in [0, 1) — at 1 the bouncing never ends.");

        this.Body = body;
        this.InitialVelocity = initialVelocity;
        this.Restitution = e;
        this.Gravity = gravity;

        var bounces = new List<(double, double)>();
        double time = 0;
        double velocity = initialVelocity;
        while (velocity > 0.05 && bounces.Count < 40)
        {
            bounces.Add((time, velocity));
            velocity *= e;                    // the floor keeps (1−e) of the speed…
            time += 2 * velocity / gravity;   // …and the flight back up and down takes 2v/g
        }

        this.Bounces = bounces;
    }

    /// <summary>A drop from a height: first impact at √(2gh).</summary>
    public static BounceSequence FromDrop(ModalBody body, double height, double? restitution = null, double gravity = 9.81) =>
        new(body, System.Math.Sqrt(2 * gravity * System.Math.Max(0, height)), restitution, gravity);

    public double Duration
    {
        get
        {
            (double lastTime, double lastVelocity) = this.Bounces[^1];
            return lastTime + new Impact(Body, lastVelocity).Duration;
        }
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        DeterministicRandom random = context.CreateRandom($"bounce:{Body.Material.Name}:{InitialVelocity:0.###}");
        AudioBuffer result = AudioBuffer.Silence(duration, context.SampleRate);
        foreach ((double time, double velocity) in this.Bounces)
        {
            // Real floors are not perfectly flat; ±2% timing jitter keeps the tail from sounding
            // machine-metronomic without disturbing the audible acceleration.
            double at = System.Math.Max(0, time * (1 + 0.02 * random.NextSigned()));
            if (at >= duration) break;
            var impact = new Impact(Body, velocity);
            result = result.MixedAt(impact.Render(context, System.Math.Min(impact.Duration, duration - at)), at);
        }

        return result.FittedToDuration(duration);
    }
}

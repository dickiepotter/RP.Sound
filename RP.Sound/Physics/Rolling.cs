using RP.Sound.Effects;

namespace RP.Sound.Physics;

/// <summary>
/// An object rolling along a surface — Gaver's third fundamental contact sound, and really a
/// hybrid of the other two: a rapid stream of tiny impacts (one per surface bump the wheel meets)
/// over a continuous low rumble. The impact rate comes straight from the geometry — a wheel of
/// radius r moving at v turns v/2πr times a second — so the sound automatically speeds up and
/// brightens as the object accelerates, and slows to individual clicks as it stops.
/// </summary>
public sealed class Rolling : ISound
{
    public ModalBody Body { get; }

    /// <summary>Rolling radius, m.</summary>
    public double Radius { get; }

    /// <summary>Travel speed, m/s.</summary>
    public double Speed { get; }

    public double Duration { get; }

    public Rolling(ModalBody body, double radius, double speed, double duration)
    {
        if (radius <= 0 || !double.IsFinite(radius)) throw new ArgumentOutOfRangeException(nameof(radius), radius, "A rolling radius must be finite and positive (m).");
        if (speed <= 0 || !double.IsFinite(speed)) throw new ArgumentOutOfRangeException(nameof(speed), speed, "A rolling speed must be finite and positive (m/s).");
        if (duration < 0 || !double.IsFinite(duration)) throw new ArgumentOutOfRangeException(nameof(duration), duration, "A rolling duration must be finite and non-negative.");
        this.Body = body;
        this.Radius = radius;
        this.Speed = speed;
        this.Duration = duration;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        DeterministicRandom random = context.CreateRandom($"roll:{Body.Material.Name}:{Speed:0.###}");
        double activeDuration = System.Math.Min(Duration, duration);

        // One micro-impact per surface bump: revolutions/s × bumps per revolution.
        double revolutionsPerSecond = Speed / (2 * System.Math.PI * Radius);
        double impactsPerSecond = revolutionsPerSecond * 12;

        // Render one quiet template strike and stamp copies — the bumps differ in force and
        // timing, not in the physics of the body they ring.
        var template = new Impact(Body, System.Math.Min(1.5, 0.05 + Speed * 0.06), Body.Mass * 0.2, 0.8);
        AudioBuffer stamp = template.Render(context, System.Math.Min(template.Duration, 0.4));

        AudioBuffer result = AudioBuffer.Silence(duration, context.SampleRate);
        double time = 0;
        while (time < activeDuration)
        {
            double gain = 0.3 + 0.7 * random.NextDouble();
            result = result.MixedAt(stamp.Amplified(new Level(gain)), time);
            // Jittered bump spacing: surfaces are irregular, so ±40% around the mean interval.
            time += (1.0 / impactsPerSecond) * (0.6 + 0.8 * random.NextDouble());
        }

        // The rumble underneath: the body's mass pressing along the surface reads as low noise
        // that rises and brightens with speed.
        AudioBuffer rumble = new Synthesis.Noise(Synthesis.NoiseColor.Brown, stream: $"roll-rumble:{Speed:0.###}")
            .Render(context, activeDuration)
            .LowPassed(80 + Speed * 60)
            .Amplified(new Level(System.Math.Tanh(Speed / 5.0) * 1.2));

        return result.MixedWith(rumble).FadedOut(0.05).FittedToDuration(duration);
    }
}

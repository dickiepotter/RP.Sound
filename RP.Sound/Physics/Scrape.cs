using RP.Sound.Effects;

namespace RP.Sound.Physics;

/// <summary>
/// One surface dragged across another — Gaver's second fundamental contact sound. The model
/// (after van den Doel &amp; Pai): sliding across surface bumps produces a noisy excitation whose
/// centre frequency is <c>speed × bump density</c> — drag twice as fast and the hiss rises an
/// octave, which is exactly the cue ears use to judge scraping speed. The noise then rings the
/// scraped body's own modes, which is why scraping steel sounds steely and scraping wood woody.
/// </summary>
public sealed class Scrape : ISound
{
    public ModalBody Body { get; }

    /// <summary>Sliding speed, m/s.</summary>
    public double Speed { get; }

    /// <summary>Pressing force, N — more force, more excitation.</summary>
    public double Force { get; }

    /// <summary>0 polished … 1 coarse. Sets the surface's bump density (50–2000 bumps per metre).</summary>
    public double Roughness { get; }

    public double Duration { get; }

    public Scrape(ModalBody body, double speed, double duration, double force = 5, double roughness = 0.5)
    {
        if (speed <= 0 || !double.IsFinite(speed)) throw new ArgumentOutOfRangeException(nameof(speed), speed, "A scrape speed must be finite and positive (m/s).");
        if (duration < 0 || !double.IsFinite(duration)) throw new ArgumentOutOfRangeException(nameof(duration), duration, "A scrape's duration must be finite and non-negative.");
        if (force <= 0 || !double.IsFinite(force)) throw new ArgumentOutOfRangeException(nameof(force), force, "A scrape force must be finite and positive (N).");
        if (roughness is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(roughness), roughness, "Roughness is a fraction between 0 and 1.");
        this.Body = body;
        this.Speed = speed;
        this.Duration = duration;
        this.Force = force;
        this.Roughness = roughness;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        int length = context.SampleCount(duration);
        int active = System.Math.Min(length, context.SampleCount(System.Math.Min(Duration, duration)));
        var excitation = new float[length];
        DeterministicRandom random = context.CreateRandom($"scrape:{Body.Material.Name}:{Speed:0.###}");

        double bumpsPerMetre = 50 + Roughness * 1950;
        double amplitude = System.Math.Tanh(0.05 * Force * System.Math.Pow(Speed, 0.7));

        // The centre frequency tracks a slowly wandering speed (nobody drags perfectly evenly),
        // so the filter is rebuilt every few milliseconds — cheap, and it keeps the hiss alive.
        const int block = 256;
        double speedDrift = 1;
        for (int start = 0; start < active; start += block)
        {
            speedDrift = System.Math.Clamp(speedDrift + 0.05 * random.NextSigned(), 0.8, 1.2);
            double centre = System.Math.Clamp(Speed * speedDrift * bumpsPerMetre, 40, 8000);
            Biquad band = Biquad.BandPass(context.SampleRate, centre, 1.5);
            int end = System.Math.Min(active, start + block);
            for (int i = start; i < end; i++)
                excitation[i] = (float)(band.Process(random.NextSigned()) * amplitude * 2);
        }

        // The continuous excitation drives the body's own resonances — quietly, since a scrape
        // feeds energy in gently compared with a strike.
        AudioBuffer result = AudioBuffer.TakeOwnership(excitation, context.SampleRate);
        foreach (Mode mode in Body.Modes)
        {
            AudioBuffer ringing = result.BandPassed(mode.Frequency, 25).Amplified(new Level(1.2 * mode.Level.Linear));
            result = result.MixedWith(ringing);
        }

        return result.FadedIn(0.02).FadedOut(0.05).FittedToDuration(duration);
    }
}

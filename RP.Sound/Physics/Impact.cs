namespace RP.Sound.Physics;

/// <summary>
/// One object striking another — the fundamental contact sound (Gaver's ecological taxonomy puts
/// impact, scraping and rolling at the root of everything we hear solids do). The recipe, from
/// van den Doel &amp; Pai's FoleyAutomatic: a short contact "click" excites the body's modes, and
/// what we hear is the modes ringing down.
///
/// The physics decides the character:
/// - <b>Velocity</b> sets energy (½mv²), so faster is louder — and slightly brighter.
/// - <b>Hardness</b> sets contact time: hard-on-hard contact is short, and a short tap excites
///   high modes a long dull thud cannot (a marble vs. a rubber mallet on the same glass).
/// - The <b>material and size</b> of the body decide which modes exist at all.
/// </summary>
public sealed class Impact : ISound
{
    public ModalBody Body { get; }

    /// <summary>Impact speed, m/s.</summary>
    public double Velocity { get; }

    /// <summary>Striking mass, kg (defaults to the body's own mass).</summary>
    public double Mass { get; }

    /// <summary>Hardness of whatever does the striking, 0 soft … 1 hard.</summary>
    public double StrikerHardness { get; }

    public Impact(ModalBody body, double velocity, double? mass = null, double strikerHardness = 0.7)
    {
        if (velocity < 0 || !double.IsFinite(velocity))
            throw new ArgumentOutOfRangeException(nameof(velocity), velocity, "An impact velocity must be finite and non-negative (m/s).");
        if (mass is <= 0 or double.NaN or double.PositiveInfinity)
            throw new ArgumentOutOfRangeException(nameof(mass), mass, "A striking mass must be finite and positive (kg).");
        if (strikerHardness is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(strikerHardness), strikerHardness, "Hardness is a fraction between 0 and 1.");
        this.Body = body;
        this.Velocity = velocity;
        this.Mass = mass ?? body.Mass;
        this.StrikerHardness = strikerHardness;
    }

    /// <summary>
    /// The impact of a drop from a height under gravity: v = √(2gh) — energy conservation, with
    /// the gravity explicit so the same drop sounds right on other worlds.
    /// </summary>
    public static Impact FromDrop(ModalBody body, double height, double gravity = 9.81, double? mass = null, double strikerHardness = 0.7)
    {
        if (height < 0 || gravity < 0) throw new ArgumentOutOfRangeException(nameof(height), "A drop height and gravity must be non-negative.");
        return new Impact(body, System.Math.Sqrt(2 * gravity * height), mass, strikerHardness);
    }

    /// <summary>
    /// How long striker and body stay touching. Hertz contact theory says harder pairings touch
    /// more briefly; this is that trend as a simple documented heuristic: 0.2 ms (hard on hard)
    /// up to ~4 ms (soft on soft).
    /// </summary>
    public double ContactTime
    {
        get
        {
            double effectiveHardness = Body.Material.Hardness * StrikerHardness;
            return 0.0002 + 0.004 * (1 - effectiveHardness);
        }
    }

    /// <summary>
    /// Loudness from energy: amplitude grows with v·√m (i.e. with the square root of kinetic
    /// energy), squashed through tanh so a cannonball is louder than a pebble but not a
    /// thousand times louder — matching loudness perception's compressive nature.
    /// </summary>
    public Level Loudness => new(System.Math.Tanh(0.5 * Velocity * System.Math.Sqrt(Mass)));

    public double Duration
    {
        get
        {
            double longest = 0;
            foreach (Mode mode in Body.Modes) longest = System.Math.Max(longest, mode.DecaySeconds);
            return ContactTime + 6.9 * longest; // e^−6.9 ≈ −60 dB: the ring has died away
        }
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        int active = System.Math.Min(samples.Length, context.SampleCount(System.Math.Min(Duration, duration)));
        DeterministicRandom random = context.CreateRandom($"impact:{Body.Material.Name}:{Velocity:0.###}");

        double contactTime = ContactTime;
        double amplitude = Loudness.Linear;

        // Sum of decaying sines — the modal ring. A force pulse of width τc has little energy
        // above 1/τc, so each mode is weighted by 1/(1+(f·τc)²): the brightness-vs-hardness law.
        double totalWeight = 0;
        Span<double> weights = stackalloc double[Body.Modes.Count];
        Span<double> phases = stackalloc double[Body.Modes.Count];
        for (int m = 0; m < Body.Modes.Count; m++)
        {
            Mode mode = Body.Modes[m];
            double excitation = 1.0 / (1.0 + System.Math.Pow(mode.Frequency.Hertz * contactTime, 2));
            weights[m] = mode.Level.Linear * excitation;
            totalWeight += weights[m];
            phases[m] = random.NextDouble() * 2 * System.Math.PI; // striking spot varies; pitch does not
        }

        if (totalWeight <= 0) totalWeight = 1;
        for (int m = 0; m < Body.Modes.Count; m++)
        {
            Mode mode = Body.Modes[m];
            double weight = weights[m] / totalWeight * amplitude;
            double omega = 2 * System.Math.PI * mode.Frequency.Hertz / context.SampleRate;
            double decayPerSample = System.Math.Exp(-1.0 / (mode.DecaySeconds * context.SampleRate));
            double envelope = weight;
            double phase = phases[m];
            for (int i = 0; i < active; i++)
            {
                samples[i] += (float)(envelope * System.Math.Sin(phase));
                phase += omega;
                envelope *= decayPerSample;
                if (envelope < 1e-6) break;
            }
        }

        // The contact click: a burst of noise exactly as long as the contact, low-passed to the
        // same bandwidth the pulse width allows. It is what makes the first millisecond "snap".
        int clickSamples = System.Math.Min(active, System.Math.Max(1, (int)(contactTime * context.SampleRate)));
        double clickLevel = amplitude * 0.4 * Body.Material.Hardness * StrikerHardness;
        double clickState = 0;
        double clickCoefficient = System.Math.Exp(-2 * System.Math.PI * System.Math.Min(0.45 * context.SampleRate, 1.0 / contactTime) / context.SampleRate);
        for (int i = 0; i < clickSamples; i++)
        {
            clickState = clickCoefficient * clickState + (1 - clickCoefficient) * random.NextSigned();
            double window = 1.0 - (double)i / clickSamples;
            samples[i] += (float)(clickState * clickLevel * window * 8);
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

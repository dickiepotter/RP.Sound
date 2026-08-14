namespace RP.Sound.Instruments;

/// <summary>
/// A snare drum: two sounds happening at once, and both are needed for the ear to say "snare"
/// (Gordon Reid, "Synth Secrets: Practical Snare Drum Synthesis", Sound on Sound, 2002).
/// The <em>shell tone</em> is the batter head's lowest two membrane modes ringing briefly — the
/// fundamental plus the first overtone at 1.59× (the 11/01 mode ratio of an ideal circular
/// membrane; Fletcher &amp; Rossing, The Physics of Musical Instruments). The <em>snap</em> is the
/// wires stretched across the resonant head rattling against it — dense, bright, and best
/// modelled as exactly what it sounds like: a burst of high-passed noise.
/// </summary>
public sealed class SnareDrum : ISound
{
    /// <summary>The batter head's fundamental. ~180–200 Hz for a standard 14-inch snare.</summary>
    public Frequency Pitch { get; }

    /// <summary>0 … 1: the balance of wire snap over shell tone. 0 is a tom with aspirations; 1 is all rattle.</summary>
    public double Snappy { get; }

    /// <summary>Seconds until the drum has died away to −60 dB.</summary>
    public double Decay { get; }

    public Level Level { get; }

    public double Duration => Decay;

    public SnareDrum(Frequency? pitch = null, double snappy = 0.7, double decay = 0.25, Level? level = null)
    {
        this.Pitch = pitch ?? new Frequency(190);
        if (this.Pitch.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(pitch), pitch, "A snare drum's pitch must be positive.");
        if (snappy is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(snappy), snappy, "Snappy is a fraction between 0 and 1.");
        if (decay <= 0 || !double.IsFinite(decay))
            throw new ArgumentOutOfRangeException(nameof(decay), decay, "A snare drum's decay must be finite and positive (seconds).");
        this.Snappy = snappy;
        this.Decay = decay;
        this.Level = level ?? Level.Unity;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        int active = System.Math.Min(samples.Length, context.SampleCount(System.Math.Min(Duration, duration)));
        DeterministicRandom random = context.CreateRandom($"snare:{Pitch.Hertz:0.###}");

        // --- Shell tone: the two lowest membrane modes, dying faster than the overall decay so
        // the noise tail is what lingers — as on the real drum. ---
        double toneLevel = (1 - 0.6 * Snappy) * Level.Linear;
        double toneRate = 6.9 / (Decay * 0.6);
        double phase1 = random.NextDouble() * 2 * System.Math.PI;
        double phase2 = random.NextDouble() * 2 * System.Math.PI;
        double omega1 = 2 * System.Math.PI * Pitch.Hertz / context.SampleRate;
        double omega2 = omega1 * 1.59; // the circular membrane's 11 mode over its 01 fundamental

        // --- The snap: white noise, high-passed so it sits above the shell tone. ---
        double noiseLevel = Snappy * Level.Linear;
        double noiseRate = 6.9 / Decay;
        double previousNoise = 0, highPassed = 0;

        for (int i = 0; i < active; i++)
        {
            double t = (double)i / context.SampleRate;
            double tone = (System.Math.Sin(phase1) + 0.5 * System.Math.Sin(phase2)) * System.Math.Exp(-toneRate * t) * toneLevel * 0.6;
            phase1 += omega1;
            phase2 += omega2;

            // One-pole high-pass (differentiator with leak) keeps only the noise's bright half.
            double noise = random.NextSigned();
            highPassed = 0.95 * (highPassed + noise - previousNoise);
            previousNoise = noise;
            double snap = highPassed * System.Math.Exp(-noiseRate * t) * noiseLevel * 0.7;

            samples[i] = (float)(tone + snap);
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

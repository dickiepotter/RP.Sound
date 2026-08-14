namespace RP.Sound.Instruments;

/// <summary>
/// A drawbar organ — additive synthesis in its oldest commercial form. The Hammond organ gives
/// the player nine drawbars, each adding one near-sinusoidal partial at a fixed pitch relation to
/// the key: sub-octave (16′), sub-third (5⅓′), unison (8′), octave (4′), then the 3rd, 4th, 5th,
/// 6th and 8th harmonics (2⅔′, 2′, 1⅗′, 1⅓′, 1′). A registration is written as nine digits 0–8,
/// each drawbar stop worth about 3 dB (the Hammond convention) — so "888000000" is the classic
/// jazz registration and "888888888" is full organ. Notes start and stop almost instantly (pipes
/// and tonewheels have no envelope to speak of), and the tiny burst as the key contacts close —
/// the famous Hammond key click — is part of the sound, so it is modelled, not suppressed.
/// </summary>
public sealed class Organ : ISound
{
    /// <summary>Each drawbar's pitch as a multiple of the played note.</summary>
    private static readonly double[] HarmonicRatios = { 0.5, 1.5, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 8.0 };

    public Frequency Note { get; }
    public double Duration { get; }

    /// <summary>The nine drawbar settings, 0 (silent) to 8 (full).</summary>
    public IReadOnlyList<int> Registration { get; }

    public Level Level { get; }

    public Organ(Frequency note, double duration = 1.0, string registration = "888000000", Level? level = null)
    {
        if (note.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(note), note, "An organ note must have a positive pitch.");
        if (duration < 0 || !double.IsFinite(duration))
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A note's duration must be finite and non-negative.");
        if (registration is null || registration.Length != 9)
            throw new ArgumentException("A registration is nine drawbar digits, each 0–8 (e.g. \"888000000\").", nameof(registration));

        var bars = new int[9];
        for (int i = 0; i < 9; i++)
        {
            bars[i] = registration[i] - '0';
            if (bars[i] is < 0 or > 8)
                throw new ArgumentException($"Drawbar {i + 1} is '{registration[i]}'; each digit must be 0–8.", nameof(registration));
        }

        this.Note = note;
        this.Duration = duration;
        this.Registration = bars;
        this.Level = level ?? Level.Unity;
    }

    /// <summary>The classic jazz registration: sub-octave, sub-third and unison, nothing else.</summary>
    public static Organ Jazz(Frequency note, double duration = 1.0, Level? level = null) =>
        new(note, duration, "888000000", level);

    /// <summary>Every drawbar out — the full-organ roar.</summary>
    public static Organ Full(Frequency note, double duration = 1.0, Level? level = null) =>
        new(note, duration, "888888888", level);

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        int active = System.Math.Min(samples.Length, context.SampleCount(System.Math.Min(Duration, duration)));
        DeterministicRandom random = context.CreateRandom($"organ:{Note.Hertz:0.###}");

        double total = 0;
        for (int h = 0; h < 9; h++)
        {
            if (Registration[h] == 0) continue;
            double frequency = Note.Hertz * HarmonicRatios[h];
            if (frequency > context.SampleRate * 0.45) continue;

            // Drawbar stops step ~3 dB: full out (8) is unity, each stop in halves the power.
            double amplitude = System.Math.Pow(10, (Registration[h] - 8) * 3.0 / 20.0);
            total += amplitude;
            double omega = 2 * System.Math.PI * frequency / context.SampleRate;
            double phase = random.NextDouble() * 2 * System.Math.PI;
            for (int i = 0; i < active; i++)
            {
                samples[i] += (float)(amplitude * System.Math.Sin(phase));
                phase += omega;
            }
        }

        if (total <= 0) total = 1;

        // Near-instant on/off: 5 ms ramps stop the edges clicking harder than intended…
        int ramp = System.Math.Min(active / 2, context.SampleCount(0.005));
        for (int i = 0; i < ramp; i++)
        {
            samples[i] *= (float)i / ramp;
            samples[active - 1 - i] *= (float)i / ramp;
        }

        // …while the deliberate key click — 1 ms of bright noise as the key contacts close —
        // stays. Hammond tried to engineer it out; players declared it part of the instrument.
        int clickSamples = System.Math.Min(active, context.SampleCount(0.001));
        for (int i = 0; i < clickSamples; i++)
        {
            double window = 1.0 - (double)i / clickSamples;
            samples[i] += (float)(random.NextSigned() * window * 0.1 * total);
        }

        double normalise = Level.Linear / total;
        for (int i = 0; i < active; i++) samples[i] = (float)(samples[i] * normalise);

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

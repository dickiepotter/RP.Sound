namespace RP.Sound.Instruments;

/// <summary>
/// A flute-like voice. Measured flute spectra at soft-to-moderate dynamics are dominated by the
/// fundamental with the upper harmonics far below it (Fletcher &amp; Rossing, The Physics of
/// Musical Instruments, ch. 16), so the model is nearly a sine — plus the two things that make a
/// near-sine read as breath and not a test tone: a band of breath noise around the played pitch
/// (the turbulence of the air jet at the embouchure, filtered by the same tube resonance as the
/// note), and vibrato that arrives only after the note settles, as a player's does.
/// </summary>
public sealed class Flute : ISound
{
    public Frequency Note { get; }
    public double Duration { get; }

    /// <summary>0 pure … 1 airy: how much of the jet's turbulence is heard alongside the tone.</summary>
    public double Breathiness { get; }

    public Level Level { get; }

    public Flute(Frequency note, double duration = 1.5, double breathiness = 0.3, Level? level = null)
    {
        if (note.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(note), note, "A flute note must have a positive pitch.");
        if (duration < 0 || !double.IsFinite(duration))
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A note's duration must be finite and non-negative.");
        if (breathiness is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(breathiness), breathiness, "Breathiness is a fraction between 0 and 1.");
        this.Note = note;
        this.Duration = duration;
        this.Breathiness = breathiness;
        this.Level = level ?? Level.Unity;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        int active = System.Math.Min(samples.Length, context.SampleCount(System.Math.Min(Duration, duration)));
        DeterministicRandom random = context.CreateRandom($"flute:{Note.Hertz:0.###}");

        double noteLength = System.Math.Min(Duration, duration);

        // Vibrato: ~5 Hz (the natural rate singers and wind players converge on), ±15 cents,
        // fading in over the first 0.4 s — an instant-on wobble sounds mechanical.
        const double vibratoRate = 5.0;
        const double vibratoDepth = 0.009; // 2^(15/1200) − 1 ≈ 0.87 % ≈ 15 cents

        // Breath noise: white noise band-passed at the played pitch by a resonant two-pole —
        // the tube colours the breath exactly as it colours the tone.
        double r = System.Math.Exp(-System.Math.PI * (Note.Hertz * 0.5) / context.SampleRate); // bandwidth ≈ half the pitch
        double cosOmega = System.Math.Cos(2 * System.Math.PI * System.Math.Min(Note.Hertz, context.SampleRate * 0.45) / context.SampleRate);
        double breath1 = 0, breath2 = 0;

        double phase = 0;
        for (int i = 0; i < active; i++)
        {
            double t = (double)i / context.SampleRate;

            // Soft attack and release: air, not a hammer.
            double envelope = System.Math.Min(1, t / 0.08);
            double remaining = noteLength - t;
            if (remaining < 0.1) envelope *= System.Math.Max(0, remaining / 0.1);

            double vibrato = 1 + vibratoDepth * System.Math.Min(1, t / 0.4) * System.Math.Sin(2 * System.Math.PI * vibratoRate * t);
            phase += Note.Hertz * vibrato / context.SampleRate;

            // Fundamental with faint 2nd and 3rd harmonics (−18 dB, −24 dB): almost, but not
            // quite, a sine.
            double angle = 2 * System.Math.PI * phase;
            double tone = System.Math.Sin(angle) + 0.125 * System.Math.Sin(2 * angle) + 0.06 * System.Math.Sin(3 * angle);

            double breath = 2 * r * cosOmega * breath1 - r * r * breath2 + random.NextSigned();
            breath2 = breath1;
            breath1 = breath;

            samples[i] = (float)((tone * 0.85 + breath * 0.02 * Breathiness) * envelope * Level.Linear);
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

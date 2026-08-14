namespace RP.Sound.Instruments;

/// <summary>
/// A brass-like voice built on the single most important fact about brass tone, from Risset &amp;
/// Mathews' landmark computer analysis of trumpet notes (1969): <em>brightness follows loudness</em>.
/// As a brass player blows harder the lips' pulses sharpen and upper harmonics grow faster than
/// the fundamental — so a static waveform through a static filter can never sound like brass, but
/// a sawtooth through a low-pass whose cutoff rides the amplitude envelope immediately does.
/// A slight upward pitch scoop into each note (the lips finding their slot) completes the cue.
/// </summary>
public sealed class Brass : ISound
{
    public Frequency Note { get; }
    public double Duration { get; }

    /// <summary>0 mellow horn … 1 blazing trumpet: how far the cutoff rides above the note at full level.</summary>
    public double Brightness { get; }

    public Level Level { get; }

    public Brass(Frequency note, double duration = 1.0, double brightness = 0.7, Level? level = null)
    {
        if (note.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(note), note, "A brass note must have a positive pitch.");
        if (duration < 0 || !double.IsFinite(duration))
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A note's duration must be finite and non-negative.");
        if (brightness is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(brightness), brightness, "Brightness is a fraction between 0 and 1.");
        this.Note = note;
        this.Duration = duration;
        this.Brightness = brightness;
        this.Level = level ?? Level.Unity;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        int active = System.Math.Min(samples.Length, context.SampleCount(System.Math.Min(Duration, duration)));

        double noteLength = System.Math.Min(Duration, duration);

        // The brass envelope: a deliberate ~70 ms attack (slower than a pluck, faster than a
        // pad — the pipe taking a moment to speak), light decay to a strong sustain, quick release.
        static double EnvelopeAt(double t, double length)
        {
            double value = t < 0.07 ? t / 0.07 : t < 0.17 ? 1 - 0.15 * (t - 0.07) / 0.1 : 0.85;
            double remaining = length - t;
            return remaining < 0.08 ? value * System.Math.Max(0, remaining / 0.08) : value;
        }

        // One-pole low-pass whose cutoff is recomputed every sample from the envelope:
        // cutoff = note × (1.5 + 10 × brightness × envelope) — quiet moments are dark, the
        // sustain blazes. This coupling IS the Risset trumpet insight, in one line.
        double lowPassState = 0;
        double phase = 0;
        for (int i = 0; i < active; i++)
        {
            double t = (double)i / context.SampleRate;
            double envelope = EnvelopeAt(t, noteLength);

            // The scoop: starting ~2 % flat and sliding up over 40 ms.
            double scoop = 1 - 0.02 * System.Math.Exp(-t / 0.04);
            phase += Note.Hertz * scoop / context.SampleRate;
            if (phase >= 1) phase -= 1;
            double saw = 2 * phase - 1;

            double cutoff = System.Math.Min(Note.Hertz * (1.5 + 10 * Brightness * envelope), context.SampleRate * 0.45);
            double coefficient = System.Math.Exp(-2 * System.Math.PI * cutoff / context.SampleRate);
            lowPassState = coefficient * lowPassState + (1 - coefficient) * saw;

            samples[i] = (float)(lowPassState * envelope * Level.Linear * 1.6);
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

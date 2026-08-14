namespace RP.Sound.Instruments;

/// <summary>
/// An electric bass note: the same Karplus–Strong plucked string as
/// <see cref="RP.Sound.Synthesis.PluckedString"/>, with the two refinements Jaffe &amp; Smith
/// (1983, "Extensions of the Karplus-Strong Plucked-String Algorithm") describe for realism —
/// the excitation noise is pre-low-passed (a finger pluck injects far less treble than an ideal
/// impulse; on a bass it is the felt of the thumb or the flat of the finger), and the output is
/// rounded off with a further low-pass, standing in for the bass's big body and flatwound tone.
/// The result reads as "bass guitar" rather than "banjo an octave down".
/// </summary>
public sealed class BassGuitar : ISound
{
    public Frequency Note { get; }
    public double Duration { get; }

    /// <summary>0 dark (dub thump) … 1 bright (fingerstyle click). Sets how much treble survives the pluck.</summary>
    public double Tone { get; }

    public Level Level { get; }

    public BassGuitar(Frequency note, double duration = 1.5, double tone = 0.4, Level? level = null)
    {
        if (note.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(note), note, "A bass note must have a positive pitch.");
        if (duration < 0 || !double.IsFinite(duration))
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A note's duration must be finite and non-negative.");
        if (tone is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(tone), tone, "Tone is a fraction between 0 and 1.");
        this.Note = note;
        this.Duration = duration;
        this.Tone = tone;
        this.Level = level ?? Level.Unity;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        int active = System.Math.Min(samples.Length, context.SampleCount(System.Math.Min(Duration, duration)));

        int period = System.Math.Max(2, (int)System.Math.Round(context.SampleRate / Note.Hertz));
        var line = new double[period];
        DeterministicRandom random = context.CreateRandom($"bass:{Note.Hertz:0.###}");

        // Jaffe–Smith pluck softening: run the noise through a one-pole low-pass before it enters
        // the string. Darker tone = heavier filtering of the initial excitation.
        double excitationCoefficient = 0.3 + 0.65 * (1 - Tone);
        double excitationState = 0;
        for (int i = 0; i < period; i++)
        {
            excitationState = excitationCoefficient * excitationState + (1 - excitationCoefficient) * random.NextSigned();
            line[i] = excitationState;
        }

        // A long-sustaining string: bass strings are heavy and lose energy slowly.
        const double feedback = 0.998;

        // Body round-off: a one-pole low-pass whose corner tracks the note (4× the fundamental,
        // so every register keeps the same tonal balance rather than high notes thinning out).
        double bodyCoefficient = System.Math.Exp(-2 * System.Math.PI * System.Math.Min(4 * Note.Hertz, 2000) / context.SampleRate);
        double body = 0;

        int index = 0;
        for (int i = 0; i < active; i++)
        {
            double current = line[index];
            int next = (index + 1) % period;
            line[index] = feedback * 0.5 * (current + line[next]);
            index = next;

            body = bodyCoefficient * body + (1 - bodyCoefficient) * current;
            samples[i] = (float)(body * Level.Linear * 2.0);
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

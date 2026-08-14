namespace RP.Sound.Instruments;

/// <summary>
/// A guitar note: Karplus–Strong with the pick-position comb filter from Jaffe &amp; Smith (1983).
/// Plucking a string a fraction <c>p</c> along its length cannot excite the harmonics with a node
/// at that point, so the spectrum has notches at multiples of 1/p — which is exactly what
/// subtracting a copy of the excitation delayed by <c>p</c> of a period produces. That one comb
/// is most of the difference between "generic pluck" and "guitar picked near the bridge".
/// Chords come from <see cref="Strum"/> (strings hit a few milliseconds apart, as a hand
/// actually crosses them) and rock gets <see cref="PowerChord"/> — root and fifth only, because
/// the fifth's simple 3:2 ratio keeps distortion's intermodulation products harmonic where a
/// third's would turn to mud (Walser, Running with the Devil, 1993).
/// </summary>
public sealed class Guitar : ISound
{
    public Frequency Note { get; }
    public double Duration { get; }

    /// <summary>How quickly the string's brightness dies: 0 = rings for ages, 1 = damps almost at once.</summary>
    public double Damping { get; }

    /// <summary>Where along the string it is plucked, as a fraction of its length (0.1–0.3 ≈ near the bridge).</summary>
    public double PickPosition { get; }

    public Level Level { get; }

    public Guitar(Frequency note, double duration = 2.0, double damping = 0.15, double pickPosition = 0.2, Level? level = null)
    {
        if (note.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(note), note, "A guitar note must have a positive pitch.");
        if (duration < 0 || !double.IsFinite(duration))
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A note's duration must be finite and non-negative.");
        if (damping is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(damping), damping, "Damping is a fraction between 0 and 1.");
        if (pickPosition is <= 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(pickPosition), pickPosition, "The pick position is a fraction strictly between 0 and 1.");
        this.Note = note;
        this.Duration = duration;
        this.Damping = damping;
        this.PickPosition = pickPosition;
        this.Level = level ?? Level.Unity;
    }

    /// <summary>
    /// Several notes strummed: each string starts <paramref name="strumSeconds"/> after the last,
    /// the sweep of a hand across the strings made audible (at 0.015 s it reads as one chord with
    /// life in it; much slower and it becomes an arpeggio).
    /// </summary>
    public static ISound Strum(IReadOnlyList<Frequency> notes, double duration = 2.0, double strumSeconds = 0.015, double damping = 0.15, Level? level = null)
    {
        if (notes.Count == 0) throw new ArgumentException("A strum needs at least one note.", nameof(notes));
        if (strumSeconds < 0 || !double.IsFinite(strumSeconds))
            throw new ArgumentOutOfRangeException(nameof(strumSeconds), strumSeconds, "The strum spread must be finite and non-negative (seconds).");
        Level each = level ?? Level.Unity;
        var events = new List<(double, ISound)>();
        for (int i = 0; i < notes.Count; i++)
            events.Add((i * strumSeconds, new Guitar(notes[i], duration, damping, level: each)));
        return new Timeline(events);
    }

    /// <summary>Root, fifth and octave — the rock voicing built to survive distortion.</summary>
    public static ISound PowerChord(Frequency root, double duration = 2.0, double damping = 0.15, Level? level = null) =>
        Strum(new[] { root, root.Transposed(7), root.Transposed(12) }, duration, damping: damping, level: level);

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        int active = System.Math.Min(samples.Length, context.SampleCount(System.Math.Min(Duration, duration)));

        int period = System.Math.Max(2, (int)System.Math.Round(context.SampleRate / Note.Hertz));
        var line = new double[period];
        DeterministicRandom random = context.CreateRandom($"guitar:{Note.Hertz:0.###}");

        // Fill with noise, then apply the pick-position comb: e[n] − e[n − p·period]. Harmonics
        // with a node at the pluck point cancel — the "where you picked" of the tone.
        for (int i = 0; i < period; i++) line[i] = random.NextSigned();
        int combDelay = System.Math.Max(1, (int)System.Math.Round(PickPosition * period));
        var combed = new double[period];
        for (int i = 0; i < period; i++) combed[i] = line[i] - line[(i - combDelay + period) % period];
        combed.CopyTo(line, 0);

        double feedback = 0.996 - 0.1 * Damping;
        int index = 0;
        for (int i = 0; i < active; i++)
        {
            double current = line[index];
            int next = (index + 1) % period;
            line[index] = feedback * 0.5 * (current + line[next]);
            samples[i] = (float)(current * Level.Linear * 0.7);
            index = next;
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

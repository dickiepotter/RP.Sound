namespace RP.Sound.Music;

/// <summary>
/// A set of pitches to draw from: a root plus a pattern of semitone steps. The pattern is what
/// carries the feeling — major's pattern lands on consonant intervals, Phrygian leads with the
/// unsettling flat second, and the cluster is barely a scale at all, just packed dissonance.
/// Degrees wrap upward by octaves, so a melody generator can ask for any degree it likes.
/// </summary>
public sealed class Scale
{
    public string Name { get; }
    public Frequency Root { get; }

    /// <summary>Semitone offsets within one octave, starting at 0 (the root).</summary>
    public IReadOnlyList<int> Intervals { get; }

    public Scale(string name, Frequency root, params int[] intervals)
    {
        if (intervals.Length == 0) throw new ArgumentException("A scale needs at least one interval.", nameof(intervals));
        foreach (int interval in intervals)
        {
            if (interval is < 0 or > 11)
                throw new ArgumentOutOfRangeException(nameof(intervals), interval, "Scale intervals are semitone offsets within one octave (0–11).");
        }

        this.Name = name;
        this.Root = root;
        this.Intervals = (int[])intervals.Clone();
    }

    public static Scale Major(Frequency root) => new("major", root, 0, 2, 4, 5, 7, 9, 11);
    public static Scale NaturalMinor(Frequency root) => new("minor", root, 0, 2, 3, 5, 7, 8, 10);
    public static Scale Phrygian(Frequency root) => new("phrygian", root, 0, 1, 3, 5, 7, 8, 10);
    public static Scale Lydian(Frequency root) => new("lydian", root, 0, 2, 4, 6, 7, 9, 11);
    public static Scale WholeTone(Frequency root) => new("wholetone", root, 0, 2, 4, 6, 8, 10);
    public static Scale MinorPentatonic(Frequency root) => new("minor pentatonic", root, 0, 3, 5, 7, 10);

    /// <summary>
    /// The hexatonic blues scale: the minor pentatonic plus the ♭5 "blue note" —
    /// 1, ♭3, 4, ♭5, 5, ♭7. Its friction against major-quality harmony is definitional to the
    /// blues (Open Music Theory, "Blues Melodies and the Blues Scale").
    /// </summary>
    public static Scale Blues(Frequency root) => new("blues", root, 0, 3, 5, 6, 7, 10);

    /// <summary>Packed semitones — not music so much as pressure. The horror scale.</summary>
    public static Scale Cluster(Frequency root) => new("cluster", root, 0, 1, 2, 6, 7);

    /// <summary>The pitch of a scale degree (0 = root); each full cycle of the pattern climbs an octave.</summary>
    public Frequency Degree(int degree)
    {
        if (degree < 0) throw new ArgumentOutOfRangeException(nameof(degree), degree, "Scale degrees count upward from 0 (the root).");
        int octave = degree / Intervals.Count;
        int step = Intervals[degree % Intervals.Count];
        return Root.Transposed(12 * octave + step);
    }

    /// <summary>Several degrees at once — a chord voiced from this scale.</summary>
    public Frequency[] Chord(params int[] degrees)
    {
        var result = new Frequency[degrees.Length];
        for (int i = 0; i < degrees.Length; i++) result[i] = Degree(degrees[i]);
        return result;
    }

    public override string ToString() => $"{Name} on {Root}";
}

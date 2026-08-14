namespace RP.Sound.Music;

/// <summary>
/// The emotional target of a scene, expressed in the coordinates psychology actually measures:
/// Russell's circumplex model of affect places every emotion on two axes — <b>valence</b>
/// (unpleasant → pleasant) and <b>arousal</b> (calm → energised) — and a century of music
/// psychology maps those axes onto sound: valence to consonance/mode/brightness, arousal to
/// tempo/loudness/density. A third axis, <b>tension</b>, captures Huron's unresolved-expectation
/// dimension (his ITPRA theory: sustained unresolved anticipation is what "suspense" is made of),
/// which games lean on constantly — horror is low valence + high tension, a chase is high
/// arousal, "fun" is high valence with everything else relaxed.
///
/// A genre word like "horror" is just a named point in this space; the mapping properties below
/// turn the coordinates into concrete synthesis decisions, so every generator agrees about what
/// the mood sounds like.
/// </summary>
public readonly struct Mood
{
    /// <summary>Unpleasant (−1) → pleasant (+1).</summary>
    public double Valence { get; }

    /// <summary>Calm (−1) → energised (+1).</summary>
    public double Arousal { get; }

    /// <summary>Resolved (0) → maximally unresolved/suspenseful (1).</summary>
    public double Tension { get; }

    public Mood(double valence, double arousal, double tension)
    {
        if (valence is < -1 or > 1) throw new ArgumentOutOfRangeException(nameof(valence), valence, "Valence runs from −1 (unpleasant) to +1 (pleasant).");
        if (arousal is < -1 or > 1) throw new ArgumentOutOfRangeException(nameof(arousal), arousal, "Arousal runs from −1 (calm) to +1 (energised).");
        if (tension is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(tension), tension, "Tension runs from 0 (resolved) to 1 (maximally suspenseful).");
        this.Valence = valence;
        this.Arousal = arousal;
        this.Tension = tension;
    }

    // The named genres, as points in the space.
    public static readonly Mood Calm = new(0.6, -0.6, 0.05);
    public static readonly Mood Fun = new(0.8, 0.5, 0.1);
    public static readonly Mood FastPaced = new(0.3, 0.9, 0.4);
    public static readonly Mood Anticipation = new(0.0, 0.3, 0.7);
    public static readonly Mood Threat = new(-0.7, 0.4, 0.8);
    public static readonly Mood Horror = new(-0.9, 0.2, 0.9);
    public static readonly Mood Sad = new(-0.6, -0.5, 0.2);
    public static readonly Mood Triumphant = new(0.9, 0.7, 0.15);

    public static IReadOnlyList<(string Name, Mood Mood)> Presets { get; } = new[]
    {
        ("calm", Calm), ("fun", Fun), ("fastpaced", FastPaced), ("anticipation", Anticipation),
        ("threat", Threat), ("horror", Horror), ("sad", Sad), ("triumphant", Triumphant),
    };

    public static Mood FromName(string name) =>
        TryFromName(name, out Mood mood) ? mood : throw new ArgumentException($"No mood preset named '{name}'.", nameof(name));

    public static bool TryFromName(string? name, out Mood mood)
    {
        foreach ((string presetName, Mood preset) in Presets)
        {
            if (string.Equals(presetName, name, StringComparison.OrdinalIgnoreCase))
            {
                mood = preset;
                return true;
            }
        }

        mood = Calm;
        return false;
    }

    // ---- The mapping: coordinates → synthesis decisions. Each line cites its rationale. ----

    /// <summary>Beats per minute. Tempo tracks arousal — the strongest single arousal cue there is.</summary>
    public double Tempo => 72 + Arousal * 48;

    /// <summary>
    /// The tonal centre. Darker moods sit lower (register is a threat cue — big things growl),
    /// and tension pushes lower still.
    /// </summary>
    public Frequency Root => Frequency.FromMidiNote(45 + 7 * Valence - 8 * Tension);

    /// <summary>
    /// The scale to draw pitches from. Positive valence gets major; neutral gets minor; negative
    /// gets Phrygian (its flat second is the classic menace interval); high tension collapses
    /// toward the semitone cluster — dissonance tracks falling valence.
    /// </summary>
    public Scale Scale =>
        Tension > 0.75 ? Scale.Cluster(Root)
        : Valence > 0.3 ? Scale.Major(Root)
        : Valence > -0.3 ? Scale.NaturalMinor(Root)
        : Scale.Phrygian(Root);

    /// <summary>Filter brightness, as a cutoff. Bright reads happy/energetic; dark reads sombre/threatening.</summary>
    public Frequency Brightness => new(600 + 2600 * System.Math.Clamp(0.5 + 0.35 * Valence + 0.25 * Arousal, 0, 1));

    /// <summary>
    /// Detuning between voices, in semitones. Beating between close pitches is heard as
    /// psychoacoustic roughness (Zwicker &amp; Fastl) — the texture of unease — so it rises with
    /// tension and falling valence.
    /// </summary>
    public double Detune => 0.03 + 0.35 * Tension + 0.15 * System.Math.Max(0, -Valence);

    /// <summary>How often decorative events fire, per minute — density tracks arousal.</summary>
    public double EventsPerMinute => 2 + 10 * System.Math.Max(0, Arousal) + 4 * Tension;

    public override string ToString() => $"Mood(valence {Valence:+0.##;-0.##}, arousal {Arousal:+0.##;-0.##}, tension {Tension:0.##})";
}

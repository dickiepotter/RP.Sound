namespace RP.Sound.IO;

/// <summary>
/// One note of a MIDI performance, in real time rather than ticks: when it starts (seconds), how
/// long it sounds, which key was pressed how hard, and on which channel with which General MIDI
/// program. Immutable, like every description in the library. Channel 9 (the tenth, counting from
/// one) is percussion by General MIDI convention — there the note number names a drum, not a pitch.
/// </summary>
public sealed record MidiNote
{
    /// <summary>When the note starts, in seconds from the beginning of the sequence.</summary>
    public double Start { get; }

    /// <summary>How long the key is held, in seconds.</summary>
    public double Duration { get; }

    /// <summary>The MIDI key number, 0–127 (middle C is 60).</summary>
    public int NoteNumber { get; }

    /// <summary>How hard the key was struck, 1–127. Zero is not a velocity — a note-on with velocity 0 <em>is</em> a note-off in the wire format.</summary>
    public int Velocity { get; }

    /// <summary>The MIDI channel, 0–15. Channel 9 is the General MIDI percussion channel.</summary>
    public int Channel { get; }

    /// <summary>The General MIDI program (instrument), 0–127, in force when the note started.</summary>
    public int Program { get; }

    public MidiNote(double start, double duration, int noteNumber, int velocity = 100, int channel = 0, int program = 0)
    {
        if (start < 0 || !double.IsFinite(start))
            throw new ArgumentOutOfRangeException(nameof(start), start, "A note's start must be finite and non-negative (seconds).");
        if (duration <= 0 || !double.IsFinite(duration))
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A note's duration must be finite and positive (seconds).");
        if (noteNumber is < 0 or > 127)
            throw new ArgumentOutOfRangeException(nameof(noteNumber), noteNumber, "A MIDI note number is 0–127.");
        if (velocity is < 1 or > 127)
            throw new ArgumentOutOfRangeException(nameof(velocity), velocity, "A note velocity is 1–127 (0 means note-off on the wire).");
        if (channel is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "A MIDI channel is 0–15.");
        if (program is < 0 or > 127)
            throw new ArgumentOutOfRangeException(nameof(program), program, "A General MIDI program is 0–127.");

        Start = start;
        Duration = duration;
        NoteNumber = noteNumber;
        Velocity = velocity;
        Channel = channel;
        Program = program;
    }

    /// <summary>When the key is released, in seconds.</summary>
    public double End => Start + Duration;

    /// <summary>Whether this note lives on the General MIDI percussion channel (channel 9), where note numbers name drums.</summary>
    public bool IsPercussion => Channel == 9;

    /// <summary>The equal-temperament pitch of the key (meaningless for percussion notes).</summary>
    public Frequency Pitch => Frequency.FromMidiNote(NoteNumber);

    public override string ToString() =>
        $"MidiNote({(IsPercussion ? $"drum {NoteNumber}" : Pitch.ToString())} at {Start:0.###} s for {Duration:0.###} s, vel {Velocity}, ch {Channel}, prog {Program})";
}

/// <summary>
/// A whole MIDI performance flattened to what actually matters for rendering: the notes, each with
/// absolute times in seconds. Reading a file resolves ticks, tempo changes and note-on/off pairing
/// into this form once, so nothing downstream ever thinks about ticks again. Immutable — the notes
/// are copied in and sorted by start time.
/// </summary>
public sealed class MidiSequence
{
    private readonly MidiNote[] notes;

    /// <summary>
    /// The tempo used when <em>writing</em> the sequence back to a file, in beats per minute.
    /// Note times are already absolute seconds, so this never changes how the sequence sounds —
    /// it only decides how seconds are spelled as ticks on disk (and what a sequencer's bar lines
    /// will show). Reading a file records the file's first tempo here.
    /// </summary>
    public double TempoBpm { get; }

    public MidiSequence(IEnumerable<MidiNote> notes, double tempoBpm = 120)
    {
        if (tempoBpm is <= 0 or > 1000 || !double.IsFinite(tempoBpm))
            throw new ArgumentOutOfRangeException(nameof(tempoBpm), tempoBpm, "A tempo must be positive (BPM) and sane (≤ 1000).");
        this.notes = notes.ToArray();
        if (this.notes.Any(n => n is null))
            throw new ArgumentNullException(nameof(notes), "A sequence cannot contain a null note.");
        Array.Sort(this.notes, (a, b) => a.Start.CompareTo(b.Start));
        TempoBpm = tempoBpm;
        foreach (MidiNote note in this.notes) Duration = System.Math.Max(Duration, note.End);
    }

    /// <summary>The notes, sorted by start time.</summary>
    public IReadOnlyList<MidiNote> Notes => this.notes;

    /// <summary>How many notes the sequence holds.</summary>
    public int Count => this.notes.Length;

    /// <summary>When the last note is released, in seconds. An empty sequence is a valid zero-length silence.</summary>
    public double Duration { get; }

    /// <summary>Every note shifted by the same number of semitones — percussion notes excluded, because their numbers name drums, not pitches.</summary>
    public MidiSequence Transposed(int semitones) => new(
        this.notes.Select(n => n.IsPercussion
            ? n
            : new MidiNote(n.Start, n.Duration, System.Math.Clamp(n.NoteNumber + semitones, 0, 127), n.Velocity, n.Channel, n.Program)),
        TempoBpm);

    public override string ToString() => $"MidiSequence({Count} notes over {Duration:0.###} s at {TempoBpm:0.###} BPM)";
}

using RP.Sound.IO;

namespace RP.Sound.Music;

/// <summary>
/// A MIDI performance made audible: every note of a <see cref="MidiSequence"/> voiced through the
/// library's instruments and scheduled on a <see cref="Timeline"/>. The mapping from program and
/// key to instrument is <see cref="GeneralMidi"/> unless a custom voice function is supplied —
/// so the same sequence can be re-orchestrated without touching the notes, which is the whole
/// point of MIDI being instructions rather than audio.
/// </summary>
public sealed class MidiSong : ISound
{
    private readonly Timeline timeline;

    public MidiSequence Sequence { get; }
    public Level Level { get; }

    /// <param name="sequence">The notes to perform.</param>
    /// <param name="voice">
    /// Chooses the sound for each note; null uses the General MIDI mapping. Returning null from
    /// the function skips that note — the escape hatch for percussion keys with no drum.
    /// </param>
    /// <param name="level">Master level applied after the mix is normalised.</param>
    public MidiSong(MidiSequence sequence, Func<MidiNote, ISound?>? voice = null, Level? level = null)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        Sequence = sequence;
        Level = level ?? Level.Unity;

        voice ??= DefaultVoice;
        var events = new List<(double, ISound)>(sequence.Count);
        foreach (MidiNote note in sequence.Notes)
        {
            ISound? sound = voice(note);
            if (sound is not null) events.Add((note.Start, sound));
        }

        this.timeline = new Timeline(events);
    }

    /// <summary>The General MIDI voicing: percussion channel keys become drums, everything else goes through the program-family map.</summary>
    public static ISound? DefaultVoice(MidiNote note)
    {
        ArgumentNullException.ThrowIfNull(note);
        Level level = GeneralMidi.VelocityLevel(note.Velocity);
        return note.IsPercussion
            ? GeneralMidi.Percussion(note.NoteNumber, level)
            : GeneralMidi.Voice(note.Program, note.Pitch, note.Duration, level);
    }

    /// <summary>Ends when the last instrument stops ringing — instruments may sound past their note's release, so this can exceed the sequence's duration.</summary>
    public double Duration => this.timeline.Duration;

    public AudioBuffer Render(AudioRenderContext context, double duration) =>
        this.timeline.Render(context, duration).NormalizedOrDefault(new Level(0.9)).Amplified(Level);

    public override string ToString() => $"MidiSong({Sequence.Count} notes over {Duration:0.###} s)";
}

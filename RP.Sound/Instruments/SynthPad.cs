using RP.Sound.Synthesis;

namespace RP.Sound.Instruments;

/// <summary>
/// A sustained synthesizer pad — the one member of the instrument set that is deliberately
/// electronic rather than physically modelled, because "pad" <em>is</em> a synthesizer patch
/// family, not an acoustic instrument. It simply plays <see cref="SynthPatch.Pad"/> on the
/// <see cref="Synthesizer"/>, and exists so the instrument set is complete in one namespace and
/// so there is a worked example of wrapping a patch as an instrument.
/// </summary>
public sealed class SynthPad : ISound
{
    private readonly Synthesizer voice;

    public Frequency Note => this.voice.Note;
    public double Duration => this.voice.Duration;
    public Level Level => this.voice.Level;

    public SynthPad(Frequency note, double duration = 4.0, Level? level = null) =>
        this.voice = new Synthesizer(SynthPatch.Pad, note, duration, level);

    /// <summary>Several notes as one slab of texture — the pad's natural habitat is the chord.</summary>
    public static ISound Chord(IReadOnlyList<Frequency> notes, double duration = 4.0, Level? level = null)
    {
        if (notes.Count == 0) throw new ArgumentException("A chord needs at least one note.", nameof(notes));
        Level each = new((level ?? Level.Unity).Linear / notes.Count);
        var voices = new ISound[notes.Count];
        for (int i = 0; i < notes.Count; i++) voices[i] = new SynthPad(notes[i], duration, each);
        return Sounds.Mix(voices);
    }

    public AudioBuffer Render(AudioRenderContext context, double duration) =>
        this.voice.Render(context, duration);
}

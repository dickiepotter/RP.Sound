using RP.Sound.Effects;
using RP.Sound.Physics;
using RP.Sound.Synthesis;

namespace RP.Sound.Music;

/// <summary>
/// A stinger: the short orchestral "hit" that punctuates a reveal, a kill, a discovery. Its
/// anatomy is a chord (voiced by the mood — consonant for triumph, a semitone cluster for
/// horror), an impact transient to give it a front edge, and a reverb tail to give it weight.
/// It is the mood system and the physics system meeting in one sound.
/// </summary>
public sealed class Stinger : ISound
{
    public Mood Mood { get; }
    public Level Level { get; }
    public double Duration { get; }

    public Stinger(Mood mood, Level? level = null, double duration = 2.5)
    {
        if (duration <= 0 || !double.IsFinite(duration))
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A stinger's duration must be finite and positive.");
        this.Mood = mood;
        this.Level = level ?? Level.FromDecibels(-4);
        this.Duration = duration;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        Scale scale = Mood.Scale;

        // Wide voicing: root, fifth, octave, plus the scale's colour tone. In the cluster scale
        // those land on packed semitones — the horror chord voices itself.
        Frequency[] chord = scale.Chord(0, 2, 4, 7);

        var envelope = Envelope.Percussive(decay: System.Math.Max(0.4, Duration * 0.6), attack: 0.005);
        ISound tones = Sounds.Silence(0.01);
        foreach (Frequency tone in chord)
        {
            tones = tones.MixedWith(Sounds.Mix(
                    new Oscillator(Waveform.Sawtooth, tone, Duration, new Level(0.3)),
                    new Oscillator(Waveform.Sawtooth, tone.Transposed(Mood.Detune), Duration, new Level(0.3)))
                .Shaped(envelope));
        }

        tones = tones.LowPassed(Mood.Brightness);

        // The front edge: a real modal impact — dark moods strike a big dull body, bright moods
        // a smaller, ringing one.
        var body = new ModalBody(Mood.Valence < 0 ? Material.Stone : Material.Steel, Mood.Valence < 0 ? 1.2 : 0.4);
        ISound thump = new Impact(body, 4, strikerHardness: 0.8);

        AudioBuffer mixed = tones.MixedWith(thump).Render(context, System.Math.Min(Duration, duration));
        return Reverb.Hall.Apply(mixed)
            .NormalizedOrDefault(new Level(0.95))
            .Amplified(Level)
            .FittedToDuration(duration);
    }
}

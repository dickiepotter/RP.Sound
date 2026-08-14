using RP.Sound.Effects;
using RP.Sound.Synthesis;

namespace RP.Sound.Music;

/// <summary>
/// A sustained harmonic bed — the workhorse of game underscore, because it carries mood without
/// demanding attention. Each chord tone is a pair of sawtooth voices detuned against each other
/// (their slow beating gives the pad its motion and, pushed harder, its unease) plus a sine an
/// octave below for weight, all low-passed to the mood's brightness with a slow filter sweep so
/// the bed breathes instead of freezing.
/// </summary>
public sealed class Drone : ISound
{
    public IReadOnlyList<Frequency> Chord { get; }

    /// <summary>Detune between paired voices, semitones. Small = lush; large = uneasy.</summary>
    public double Detune { get; }

    /// <summary>The low-pass cutoff the bed sits under.</summary>
    public Frequency Brightness { get; }

    public Level Level { get; }
    public double Duration => double.PositiveInfinity;

    public Drone(IReadOnlyList<Frequency> chord, double detune = 0.08, Frequency? brightness = null, Level? level = null)
    {
        if (chord.Count == 0) throw new ArgumentException("A drone needs at least one chord tone.", nameof(chord));
        if (detune < 0 || !double.IsFinite(detune)) throw new ArgumentOutOfRangeException(nameof(detune), detune, "Detune must be finite and non-negative (semitones).");
        this.Chord = chord;
        this.Detune = detune;
        this.Brightness = brightness ?? new Frequency(1800);
        this.Level = level ?? Level.FromDecibels(-12);
    }

    /// <summary>
    /// The drone a mood asks for: root and fifth always (the anchor), the third only when valence
    /// commits to major or minor, and — the horror special — a tritone or flat second grafted on
    /// as tension climbs, per the mood's own scale.
    /// </summary>
    public static Drone ForMood(Mood mood)
    {
        Scale scale = mood.Scale;
        var degrees = new List<int> { 0, 4 };            // root + fifth in a 7-note scale
        if (System.Math.Abs(mood.Valence) > 0.2) degrees.Add(2);  // commit to the third
        if (mood.Tension > 0.5) degrees.Add(1);          // the scale's darkest near-root step
        return new Drone(scale.Chord(degrees.ToArray()), mood.Detune, mood.Brightness);
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        AudioBuffer result = AudioBuffer.Silence(duration, context.SampleRate);
        foreach (Frequency tone in Chord)
        {
            ISound voice = Sounds.Mix(
                new Oscillator(Waveform.Sawtooth, tone.Transposed(-Detune / 2), duration, new Level(0.5)),
                new Oscillator(Waveform.Sawtooth, tone.Transposed(Detune / 2), duration, new Level(0.5)),
                new Oscillator(Waveform.Sine, tone.Transposed(-12), duration, new Level(0.35)));
            result = result.MixedWith(voice.Render(context, duration));
        }

        // Two passes of the same low-pass: a steeper, smoother roll-off than one (24 dB/octave),
        // which is what keeps a sawtooth bed from sounding buzzy.
        result = result.LowPassed(Brightness).LowPassed(new Frequency(Brightness.Hertz * 1.5));

        // The slow breathing: a ±3 dB swell over ~11 s. An unmoving pad reads as a test tone.
        var samples = new float[result.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            double t = (double)i / context.SampleRate;
            double swell = 1 + 0.35 * System.Math.Sin(2 * System.Math.PI * t / 11.0);
            samples[i] = (float)(result[i] * swell);
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate)
            .NormalizedOrDefault(new Level(0.9))
            .Amplified(Level)
            .FadedIn(0.5)
            .FadedOut(0.5);
    }
}

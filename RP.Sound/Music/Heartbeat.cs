using RP.Sound.Effects;
using RP.Sound.Synthesis;

namespace RP.Sound.Music;

/// <summary>
/// A heartbeat — the oldest tension device in the book, because it is not a metaphor: fear
/// raises the listener's own pulse, and hearing a quickened heart invites the body to match it.
/// The "lub-dub" is two low thumps (the two heart-valve closures, ~180 ms apart) built from a
/// pitch-dropping sine burst, low-passed so it is felt as much as heard.
/// </summary>
public sealed class Heartbeat : ISound
{
    /// <summary>Beats per minute. A resting 60; a frightened 120+.</summary>
    public double Bpm { get; }

    public Level Level { get; }
    public double Duration => double.PositiveInfinity;

    public Heartbeat(double bpm = 70, Level? level = null)
    {
        if (bpm is <= 20 or > 240 || !double.IsFinite(bpm))
            throw new ArgumentOutOfRangeException(nameof(bpm), bpm, "A heart rate must be between 20 and 240 BPM.");
        this.Bpm = bpm;
        this.Level = level ?? Level.FromDecibels(-8);
    }

    /// <summary>The pulse a mood implies: arousal and tension drive the rate up from resting.</summary>
    public static Heartbeat ForMood(Mood mood) =>
        new(60 + 30 * System.Math.Max(0, mood.Arousal) + 40 * mood.Tension);

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        // One thump: a sine dropping 55→35 Hz over 90 ms — the pitch drop is what reads as
        // "muscle" rather than "drum machine".
        AudioBuffer thump = new FrequencySweep(55, 35, 0.09, Waveform.Sine)
            .Render(context, 0.09)
            .Amplified(new Level(0.9));
        thump = Envelope.Percussive(decay: 0.085, attack: 0.008).Apply(thump);

        AudioBuffer result = AudioBuffer.Silence(duration, context.SampleRate);
        double beatInterval = 60.0 / Bpm;
        for (double time = 0.1; time < duration; time += beatInterval)
        {
            result = result.MixedAt(thump, time);                              // lub
            double dubAt = time + 0.18;
            if (dubAt < duration) result = result.MixedAt(thump.Amplified(new Level(0.7)), dubAt); // dub
        }

        return result.LowPassed(150).Amplified(Level * new Level(4)).FittedToDuration(duration);
    }
}

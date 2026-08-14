namespace RP.Sound.Effects;

/// <summary>
/// Reverberation — the wash of thousands of overlapping reflections a room adds to any sound —
/// by Schroeder's classic 1962 structure: four parallel comb filters build the dense decaying
/// reflections, then two series all-pass filters scramble their phase so the combs' metallic
/// ringing smears into a smooth tail. Room size scales the comb feedback (bigger room, longer
/// decay); damping low-passes inside each comb loop, so high frequencies die faster — as they do
/// against real soft surfaces.
/// </summary>
public sealed class Reverb
{
    /// <summary>0 = a cupboard, 1 = a cathedral.</summary>
    public double RoomSize { get; }

    /// <summary>0 = hard shiny walls (bright tail), 1 = heavy curtains (dark tail).</summary>
    public double Damping { get; }

    /// <summary>How much of the reverberated signal is blended in.</summary>
    public Level WetLevel { get; }

    public Reverb(double roomSize, double damping, Level? wetLevel = null)
    {
        if (roomSize is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(roomSize), roomSize, "Room size is a fraction between 0 and 1.");
        if (damping is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(damping), damping, "Damping is a fraction between 0 and 1.");
        this.RoomSize = roomSize;
        this.Damping = damping;
        this.WetLevel = wetLevel ?? Level.FromDecibels(-9);
    }

    public static Reverb Room => new(0.3, 0.5);
    public static Reverb Hall => new(0.7, 0.4, Level.FromDecibels(-7));
    public static Reverb Cave => new(0.95, 0.15, Level.FromDecibels(-5));

    // Schroeder's delay lengths (in seconds) — mutually prime so the combs' echoes never align
    // into an audible repeating pattern.
    private static readonly double[] CombDelays = { 0.0297, 0.0371, 0.0411, 0.0437 };
    private static readonly double[] AllPassDelays = { 0.005, 0.0017 };

    public AudioBuffer Apply(AudioBuffer buffer)
    {
        double feedback = 0.7 + 0.28 * RoomSize;

        // Tail length until feedback^(t/delay) < −60 dB (the RT60 convention).
        double rt60 = CombDelays[^1] * (-60.0 / (20.0 * System.Math.Log10(feedback)));
        int length = buffer.Length + (int)(rt60 * buffer.SampleRate);

        var wet = new double[length];
        foreach (double delaySeconds in CombDelays)
        {
            int delay = (int)(delaySeconds * buffer.SampleRate);
            var line = new double[delay];
            double filterState = 0;
            int index = 0;
            for (int i = 0; i < length; i++)
            {
                double input = i < buffer.Length ? buffer[i] : 0;
                double output = line[index];
                // The one-pole low-pass inside the loop is the damping: every trip around the
                // room loses more treble than bass.
                filterState = output * (1 - Damping) + filterState * Damping;
                line[index] = input + filterState * feedback;
                wet[i] += output * 0.25;
                index = (index + 1) % delay;
            }
        }

        foreach (double delaySeconds in AllPassDelays)
        {
            int delay = (int)(delaySeconds * buffer.SampleRate);
            var line = new double[delay];
            int index = 0;
            const double g = 0.5;
            for (int i = 0; i < length; i++)
            {
                double input = wet[i];
                double delayed = line[index];
                line[index] = input + delayed * g;
                wet[i] = delayed - g * line[index];
                index = (index + 1) % delay;
            }
        }

        var samples = new float[length];
        for (int i = 0; i < length; i++)
        {
            double dry = i < buffer.Length ? buffer[i] : 0;
            samples[i] = (float)(dry + wet[i] * WetLevel.Linear);
        }

        return AudioBuffer.TakeOwnership(samples, buffer.SampleRate);
    }

    public ISound Apply(ISound sound) => new ReverberantSound(sound, this);

    private sealed class ReverberantSound(ISound source, Reverb reverb) : ISound
    {
        public double Duration => source.Duration;
        public AudioBuffer Render(AudioRenderContext context, double duration) =>
            reverb.Apply(source.Render(context, System.Math.Min(duration, source.Duration))).FittedToDuration(duration);
    }
}

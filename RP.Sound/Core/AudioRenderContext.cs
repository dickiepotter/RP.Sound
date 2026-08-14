namespace RP.Sound;

/// <summary>
/// Everything a sound needs to turn its immutable description into samples: the sample rate and
/// the master random seed. Rendering is a pure function — (description, context) ⇒ samples — so
/// the same description rendered twice with the same context produces bit-identical audio.
/// </summary>
public sealed record AudioRenderContext(int SampleRate = 44100, int Seed = 0)
{
    public static readonly AudioRenderContext Default = new();

    /// <summary>The number of whole samples covering the given duration in seconds.</summary>
    public int SampleCount(double seconds)
    {
        if (seconds < 0 || !double.IsFinite(seconds))
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "A render duration must be finite and non-negative.");
        return (int)System.Math.Round(seconds * SampleRate);
    }

    /// <summary>The duration of one sample, in seconds.</summary>
    public double SamplePeriod => 1.0 / SampleRate;

    /// <summary>
    /// A deterministic random stream for a named component. Deriving per-component streams from
    /// the master seed (rather than sharing one generator) means composing sounds never changes
    /// the randomness any one of them sees — a mix stays deterministic no matter the render order.
    /// </summary>
    public DeterministicRandom CreateRandom(string streamName)
    {
        // FNV-1a over the stream name, folded with the master seed.
        ulong hash = 14695981039346656037UL ^ (ulong)(uint)Seed * 0x100000001B3UL;
        foreach (char c in streamName)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }

        return new DeterministicRandom(hash);
    }
}

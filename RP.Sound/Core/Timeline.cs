namespace RP.Sound;

/// <summary>
/// A schedule of sounds, each starting at its own moment — the backbone of sequenced music.
/// Composing a track from <see cref="SoundCompositionExtensions.Delayed"/> +
/// <see cref="Sounds.Mix"/> would work, but every mix allocates a full-length intermediate
/// buffer, so a track of hundreds of notes churns gigabytes. A timeline renders each event once
/// at its own natural length and adds it into a single shared output buffer instead: the same
/// result, one big array. Like every description it is immutable — the events are copied in.
/// </summary>
public sealed class Timeline : ISound
{
    private readonly (double Start, ISound Sound)[] events;

    public Timeline(IEnumerable<(double Start, ISound Sound)> events)
    {
        this.events = events.ToArray();
        foreach ((double start, ISound sound) in this.events)
        {
            if (start < 0 || !double.IsFinite(start))
                throw new ArgumentOutOfRangeException(nameof(events), start, "An event's start time must be finite and non-negative (seconds).");
            if (sound is null)
                throw new ArgumentNullException(nameof(events), "A timeline event needs a sound.");
            if (double.IsFinite(sound.Duration))
                Duration = System.Math.Max(Duration, start + sound.Duration);
        }
    }

    /// <summary>How many events are scheduled.</summary>
    public int Count => this.events.Length;

    /// <summary>
    /// Ends when the last finite event ends. An empty timeline is a valid zero-length silence —
    /// a generator legitimately produces no events at low density, and silence is the honest
    /// rendering of that. Endless events (infinite <see cref="ISound.Duration"/>) do not extend
    /// the timeline; they fill whatever duration is asked for.
    /// </summary>
    public double Duration { get; }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        foreach ((double start, ISound sound) in this.events)
        {
            if (start >= duration) continue;
            double slice = System.Math.Min(sound.Duration, duration - start);
            AudioBuffer rendered = sound.Render(context, slice);
            int offset = context.SampleCount(start);
            int copy = System.Math.Min(rendered.Length, samples.Length - offset);
            for (int i = 0; i < copy; i++) samples[offset + i] += rendered[i];
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }

    public override string ToString() => $"Timeline({Count} events over {Duration:0.###} s)";
}

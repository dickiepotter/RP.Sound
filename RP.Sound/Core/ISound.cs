namespace RP.Sound;

/// <summary>
/// The one contract every audible thing in the library shares. A sound is an immutable
/// <em>description</em> — what would be heard — and <see cref="Render"/> turns it into samples
/// deterministically: the same description and context always produce identical audio.
/// </summary>
public interface ISound
{
    /// <summary>
    /// The sound's natural length in seconds. Ambient, looping sounds (wind, rain, a drone) have
    /// no natural end and report <see cref="double.PositiveInfinity"/> — ask them for the
    /// duration you want.
    /// </summary>
    double Duration { get; }

    /// <summary>
    /// Renders exactly <paramref name="duration"/> seconds of audio: shorter naturals are padded
    /// with silence, longer ones are cut. Every implementation honours this so sounds can be
    /// mixed and sequenced without special cases.
    /// </summary>
    AudioBuffer Render(AudioRenderContext context, double duration);
}

public static class SoundRenderExtensions
{
    /// <summary>Renders a finite sound at its natural duration.</summary>
    public static AudioBuffer Render(this ISound sound, AudioRenderContext context) =>
        double.IsFinite(sound.Duration)
            ? sound.Render(context, sound.Duration)
            : throw new InvalidOperationException($"{sound.GetType().Name} has no natural end — pass the duration you want rendered.");
}

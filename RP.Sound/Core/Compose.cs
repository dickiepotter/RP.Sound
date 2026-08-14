namespace RP.Sound;

/// <summary>
/// The combinators: every sound composes with every other sound through the same handful of
/// operations, each returning a new immutable description (nothing is ever modified in place).
/// </summary>
public static class Sounds
{
    /// <summary>A finite stretch of silence — the identity element for mixing and a spacer for sequencing.</summary>
    public static ISound Silence(double duration) => new SilenceSound(duration);

    /// <summary>All the sounds together, starting at the same instant.</summary>
    public static ISound Mix(params ISound[] sounds) => new MixedSound(sounds);

    private sealed class SilenceSound(double duration) : ISound
    {
        public double Duration { get; } = duration;
        public AudioBuffer Render(AudioRenderContext context, double duration) =>
            AudioBuffer.Silence(duration, context.SampleRate);
    }

    private sealed class MixedSound : ISound
    {
        private readonly ISound[] sounds;

        public MixedSound(ISound[] sounds)
        {
            if (sounds.Length == 0) throw new ArgumentException("Mix requires at least one sound.", nameof(sounds));
            this.sounds = (ISound[])sounds.Clone();
            foreach (ISound s in this.sounds) Duration = System.Math.Max(Duration, s.Duration);
        }

        public double Duration { get; }

        public AudioBuffer Render(AudioRenderContext context, double duration)
        {
            AudioBuffer result = AudioBuffer.Silence(duration, context.SampleRate);
            foreach (ISound s in this.sounds)
                result = result.MixedWith(s.Render(context, System.Math.Min(duration, s.Duration)).FittedToDuration(duration));
            return result;
        }
    }
}

public static class SoundCompositionExtensions
{
    /// <summary>The same sound at a different level.</summary>
    public static ISound Amplified(this ISound sound, Level gain) => new AmplifiedSound(sound, gain);

    /// <summary>The same sound with a loudness contour applied over its length.</summary>
    public static ISound Shaped(this ISound sound, Envelope envelope) => new ShapedSound(sound, envelope);

    /// <summary>The same sound starting after a stretch of silence.</summary>
    public static ISound Delayed(this ISound sound, double seconds) => new DelayedSound(sound, seconds);

    /// <summary>This sound, then another.</summary>
    public static ISound Then(this ISound sound, ISound next) => new SequencedSound(sound, next);

    /// <summary>This sound mixed with another, both starting together.</summary>
    public static ISound MixedWith(this ISound sound, ISound other) => Sounds.Mix(sound, other);

    /// <summary>The sound repeated, each start separated by <paramref name="interval"/> seconds (repeats may overlap).</summary>
    public static ISound Repeated(this ISound sound, int count, double interval) => new RepeatedSound(sound, count, interval);

    /// <summary>The sound cut (or padded) to an exact length — how an endless ambience is given an end.</summary>
    public static ISound Trimmed(this ISound sound, double duration) => new TrimmedSound(sound, duration);

    private sealed class AmplifiedSound(ISound source, Level gain) : ISound
    {
        public double Duration => source.Duration;
        public AudioBuffer Render(AudioRenderContext context, double duration) =>
            source.Render(context, duration).Amplified(gain);
    }

    private sealed class ShapedSound(ISound source, Envelope envelope) : ISound
    {
        public double Duration => source.Duration;
        public AudioBuffer Render(AudioRenderContext context, double duration) =>
            envelope.Apply(source.Render(context, duration));
    }

    private sealed class DelayedSound : ISound
    {
        private readonly ISound source;
        private readonly double delay;

        public DelayedSound(ISound source, double delay)
        {
            if (delay < 0 || !double.IsFinite(delay))
                throw new ArgumentOutOfRangeException(nameof(delay), delay, "A delay must be finite and non-negative.");
            this.source = source;
            this.delay = delay;
        }

        public double Duration => source.Duration + this.delay;

        public AudioBuffer Render(AudioRenderContext context, double duration)
        {
            double remaining = System.Math.Max(0, duration - this.delay);
            return AudioBuffer.Silence(duration, context.SampleRate)
                .MixedAt(this.source.Render(context, System.Math.Min(remaining, this.source.Duration)), this.delay)
                .FittedToDuration(duration);
        }
    }

    private sealed class SequencedSound(ISound first, ISound second) : ISound
    {
        public double Duration => first.Duration + second.Duration;

        public AudioBuffer Render(AudioRenderContext context, double duration)
        {
            double firstLength = System.Math.Min(first.Duration, duration);
            AudioBuffer head = first.Render(context, firstLength);
            double remaining = duration - firstLength;
            return remaining <= 0
                ? head.FittedToDuration(duration)
                : head.Then(second.Render(context, System.Math.Min(remaining, second.Duration))).FittedToDuration(duration);
        }
    }

    private sealed class RepeatedSound : ISound
    {
        private readonly ISound source;
        private readonly int count;
        private readonly double interval;

        public RepeatedSound(ISound source, int count, double interval)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "A repeat count must be at least one.");
            if (interval < 0 || !double.IsFinite(interval))
                throw new ArgumentOutOfRangeException(nameof(interval), interval, "A repeat interval must be finite and non-negative.");
            this.source = source;
            this.count = count;
            this.interval = interval;
        }

        public double Duration => (this.count - 1) * this.interval + this.source.Duration;

        public AudioBuffer Render(AudioRenderContext context, double duration)
        {
            AudioBuffer result = AudioBuffer.Silence(duration, context.SampleRate);
            AudioBuffer one = this.source.Render(context, this.source.Duration);
            for (int i = 0; i < this.count; i++)
            {
                double at = i * this.interval;
                if (at >= duration) break;
                result = result.MixedAt(one, at);
            }

            return result.FittedToDuration(duration);
        }
    }

    private sealed class TrimmedSound(ISound source, double duration) : ISound
    {
        public double Duration { get; } = double.IsFinite(duration) && duration >= 0
            ? duration
            : throw new ArgumentOutOfRangeException(nameof(duration), duration, "A trimmed duration must be finite and non-negative.");

        public AudioBuffer Render(AudioRenderContext context, double duration) =>
            source.Render(context, System.Math.Min(duration, Duration)).FittedToDuration(duration);
    }
}

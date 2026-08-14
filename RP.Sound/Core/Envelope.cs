namespace RP.Sound;

/// <summary>How an envelope segment travels between two levels.</summary>
public enum EnvelopeCurve
{
    /// <summary>A straight line — simple, and right for short fades.</summary>
    Linear,

    /// <summary>
    /// An exponential approach — how physical vibrations actually die away (each period loses the
    /// same <em>fraction</em> of its energy), so decays sound natural rather than mechanical.
    /// </summary>
    Exponential,
}

/// <summary>
/// The classic ADSR loudness contour: Attack (rise to full), Decay (fall to the sustain level),
/// Sustain (hold), Release (fall to silence). Immutable; times are seconds. The sustain segment
/// stretches to fill whatever total duration the envelope is applied over.
/// </summary>
public readonly struct Envelope
{
    public double Attack { get; }
    public double Decay { get; }

    /// <summary>The level held during the sustain segment (relative to the attack peak).</summary>
    public Level Sustain { get; }

    public double Release { get; }
    public EnvelopeCurve Curve { get; }

    public Envelope(double attack, double decay, Level sustain, double release, EnvelopeCurve curve = EnvelopeCurve.Exponential)
    {
        if (attack < 0 || decay < 0 || release < 0 || !double.IsFinite(attack + decay + release))
            throw new ArgumentOutOfRangeException(nameof(attack), "Envelope segment times must be finite and non-negative.");
        if (sustain.Linear > 1)
            throw new ArgumentOutOfRangeException(nameof(sustain), sustain, "The sustain level is relative to the attack peak and cannot exceed unity.");
        this.Attack = attack;
        this.Decay = decay;
        this.Sustain = sustain;
        this.Release = release;
        this.Curve = curve;
    }

    /// <summary>A standard ADSR envelope.</summary>
    public static Envelope Adsr(double attack, double decay, Level sustain, double release) =>
        new(attack, decay, sustain, release);

    /// <summary>
    /// A struck or plucked shape: near-instant attack, exponential die-away, no sustain — the
    /// contour of almost every physical impact sound.
    /// </summary>
    public static Envelope Percussive(double decay, double attack = 0.002) =>
        new(attack, decay, Level.Silence, 0);

    /// <summary>A held shape: quick fade in, full sustain, gentle fade out — for beds and drones.</summary>
    public static Envelope Sustained(double fadeIn, double fadeOut) =>
        new(fadeIn, 0, Level.Unity, fadeOut, EnvelopeCurve.Linear);

    /// <summary>
    /// The gain at <paramref name="time"/> seconds into a note lasting <paramref name="duration"/>
    /// seconds in total (release included). Always in [0, 1].
    /// </summary>
    public double Amplitude(double time, double duration)
    {
        if (time < 0 || time >= duration) return 0;

        double hold = System.Math.Max(0, duration - Attack - Decay - Release);

        if (time < Attack) return Attack <= 0 ? 1 : time / Attack;
        time -= Attack;

        if (time < Decay) return Blend(1, Sustain.Linear, Decay <= 0 ? 1 : time / Decay);
        time -= Decay;

        if (time < hold) return Sustain.Linear;
        time -= hold;

        return Release <= 0 ? 0 : Blend(Sustain.Linear, 0, System.Math.Min(1, time / Release));
    }

    private double Blend(double from, double to, double t)
    {
        // The exponential curve squares the progress on falling segments: energy dies away
        // proportionally to what remains, and t² is the cheapest smooth approximation of that.
        if (Curve == EnvelopeCurve.Exponential) t *= t;
        return from + (to - from) * t;
    }

    /// <summary>Applies this envelope across the full length of a buffer.</summary>
    public AudioBuffer Apply(AudioBuffer buffer)
    {
        var result = new float[buffer.Length];
        double duration = buffer.Duration;
        for (int i = 0; i < result.Length; i++)
            result[i] = (float)(buffer[i] * Amplitude((double)i / buffer.SampleRate, duration));
        return AudioBuffer.TakeOwnership(result, buffer.SampleRate);
    }
}

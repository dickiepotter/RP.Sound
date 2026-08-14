namespace RP.Sound;

/// <summary>
/// A finished piece of mono audio: samples plus the rate they were rendered at. Immutable — every
/// operation returns a new buffer, so buffers behave like values and are safe to share and cache.
/// All operations exist in static and instance form; the instance form calls the static one.
/// </summary>
public sealed class AudioBuffer
{
    private readonly float[] samples;

    /// <summary>Samples per second these samples were rendered at.</summary>
    public int SampleRate { get; }

    private AudioBuffer(float[] samples, int sampleRate)
    {
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "A sample rate must be positive.");
        this.samples = samples;
        this.SampleRate = sampleRate;
    }

    /// <summary>Creates a buffer by copying the given samples (the caller keeps its array).</summary>
    public static AudioBuffer FromSamples(ReadOnlySpan<float> samples, int sampleRate) =>
        new(samples.ToArray(), sampleRate);

    /// <summary>A buffer of silence.</summary>
    public static AudioBuffer Silence(double seconds, int sampleRate) =>
        new(new float[checked((int)System.Math.Round(seconds * sampleRate))], sampleRate);

    // Internal fast path for renderers that hand over a freshly built array they will not touch again.
    internal static AudioBuffer TakeOwnership(float[] samples, int sampleRate) => new(samples, sampleRate);

    public int Length => this.samples.Length;
    public double Duration => (double)this.samples.Length / SampleRate;
    public float this[int index] => this.samples[index];
    public ReadOnlySpan<float> Samples => this.samples;

    /// <summary>The largest absolute sample value, as a <see cref="Level"/>.</summary>
    public Level PeakLevel
    {
        get
        {
            float peak = 0;
            foreach (float s in this.samples) peak = System.Math.Max(peak, System.Math.Abs(s));
            return new Level(peak);
        }
    }

    /// <summary>Root-mean-square level — the buffer's average energy, closer to perceived loudness than the peak.</summary>
    public Level RmsLevel
    {
        get
        {
            if (this.samples.Length == 0) return Level.Silence;
            double sum = 0;
            foreach (float s in this.samples) sum += (double)s * s;
            return new Level(System.Math.Sqrt(sum / this.samples.Length));
        }
    }

    /// <summary>Scales every sample by a gain.</summary>
    public static AudioBuffer Amplify(AudioBuffer buffer, Level gain)
    {
        var result = new float[buffer.Length];
        for (int i = 0; i < result.Length; i++) result[i] = (float)(buffer.samples[i] * gain.Linear);
        return new AudioBuffer(result, buffer.SampleRate);
    }

    public AudioBuffer Amplified(Level gain) => Amplify(this, gain);

    /// <summary>
    /// Sums buffers sample-by-sample (they start together; the result is as long as the longest).
    /// Mixing different sample rates would silently change pitch and speed, so it throws instead.
    /// </summary>
    public static AudioBuffer Mix(params AudioBuffer[] buffers)
    {
        if (buffers.Length == 0) throw new ArgumentException("Mix requires at least one buffer.", nameof(buffers));
        int rate = buffers[0].SampleRate;
        int length = 0;
        foreach (AudioBuffer b in buffers)
        {
            if (b.SampleRate != rate)
                throw new ArgumentException($"Cannot mix buffers with different sample rates ({rate} and {b.SampleRate}).", nameof(buffers));
            length = System.Math.Max(length, b.Length);
        }

        var result = new float[length];
        foreach (AudioBuffer b in buffers)
            for (int i = 0; i < b.Length; i++)
                result[i] += b.samples[i];
        return new AudioBuffer(result, rate);
    }

    public AudioBuffer MixedWith(AudioBuffer other) => Mix(this, other);

    /// <summary>Joins buffers end to end.</summary>
    public static AudioBuffer Concat(params AudioBuffer[] buffers)
    {
        if (buffers.Length == 0) throw new ArgumentException("Concat requires at least one buffer.", nameof(buffers));
        int rate = buffers[0].SampleRate;
        int length = 0;
        foreach (AudioBuffer b in buffers)
        {
            if (b.SampleRate != rate)
                throw new ArgumentException($"Cannot concatenate buffers with different sample rates ({rate} and {b.SampleRate}).", nameof(buffers));
            length += b.Length;
        }

        var result = new float[length];
        int offset = 0;
        foreach (AudioBuffer b in buffers)
        {
            b.samples.CopyTo(result, offset);
            offset += b.Length;
        }

        return new AudioBuffer(result, rate);
    }

    public AudioBuffer Then(AudioBuffer next) => Concat(this, next);

    /// <summary>Pads with silence or cuts so the buffer is exactly the given length in seconds.</summary>
    public static AudioBuffer FitToDuration(AudioBuffer buffer, double seconds)
    {
        int length = checked((int)System.Math.Round(seconds * buffer.SampleRate));
        if (length == buffer.Length) return buffer;
        var result = new float[length];
        Array.Copy(buffer.samples, result, System.Math.Min(length, buffer.Length));
        return new AudioBuffer(result, buffer.SampleRate);
    }

    public AudioBuffer FittedToDuration(double seconds) => FitToDuration(this, seconds);

    /// <summary>Mixes another buffer in, starting at an offset in seconds.</summary>
    public static AudioBuffer MixAt(AudioBuffer buffer, AudioBuffer other, double offsetSeconds)
    {
        if (buffer.SampleRate != other.SampleRate)
            throw new ArgumentException($"Cannot mix buffers with different sample rates ({buffer.SampleRate} and {other.SampleRate}).", nameof(other));
        int offset = checked((int)System.Math.Round(offsetSeconds * buffer.SampleRate));
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offsetSeconds), offsetSeconds, "The offset must be non-negative.");

        var result = new float[System.Math.Max(buffer.Length, offset + other.Length)];
        buffer.samples.CopyTo(result, 0);
        for (int i = 0; i < other.Length; i++) result[offset + i] += other.samples[i];
        return new AudioBuffer(result, buffer.SampleRate);
    }

    public AudioBuffer MixedAt(AudioBuffer other, double offsetSeconds) => MixAt(this, other, offsetSeconds);

    /// <summary>A linear fade from silence over the first <paramref name="seconds"/>.</summary>
    public static AudioBuffer FadeIn(AudioBuffer buffer, double seconds)
    {
        var result = (float[])buffer.samples.Clone();
        int fade = System.Math.Min(result.Length, (int)System.Math.Round(seconds * buffer.SampleRate));
        for (int i = 0; i < fade; i++) result[i] *= (float)i / fade;
        return new AudioBuffer(result, buffer.SampleRate);
    }

    public AudioBuffer FadedIn(double seconds) => FadeIn(this, seconds);

    /// <summary>A linear fade to silence over the last <paramref name="seconds"/>.</summary>
    public static AudioBuffer FadeOut(AudioBuffer buffer, double seconds)
    {
        var result = (float[])buffer.samples.Clone();
        int fade = System.Math.Min(result.Length, (int)System.Math.Round(seconds * buffer.SampleRate));
        for (int i = 0; i < fade; i++) result[result.Length - 1 - i] *= (float)i / fade;
        return new AudioBuffer(result, buffer.SampleRate);
    }

    public AudioBuffer FadedOut(double seconds) => FadeOut(this, seconds);

    /// <summary>
    /// Scales the buffer so its peak sits at the target level (full scale by default).
    /// Strict: silence has no peak to scale, so this throws; see <see cref="NormalizedOrDefault(Level)"/>.
    /// </summary>
    public AudioBuffer Normalized(Level target) =>
        PeakLevel.Linear <= 0 ? throw new NormalizeSilentBufferException() : Amplified(new Level(target.Linear / PeakLevel.Linear));

    public AudioBuffer Normalized() => Normalized(Level.Unity);

    /// <summary>The safe form of <see cref="Normalized()"/>: silence comes back unchanged.</summary>
    public AudioBuffer NormalizedOrDefault(Level target) =>
        PeakLevel.Linear <= 0 ? this : Amplified(new Level(target.Linear / PeakLevel.Linear));

    public AudioBuffer NormalizedOrDefault() => NormalizedOrDefault(Level.Unity);

    /// <summary>
    /// Hard-limits every sample into [−1, +1] with a gentle tanh knee above the threshold, so an
    /// over-hot mix distorts musically instead of wrapping. 16-bit output clips hard otherwise.
    /// </summary>
    public AudioBuffer SoftClipped()
    {
        var result = new float[Length];
        for (int i = 0; i < result.Length; i++)
        {
            float s = this.samples[i];
            result[i] = System.Math.Abs(s) <= 0.8f ? s : (float)(System.Math.Sign(s) * (0.8 + 0.2 * System.Math.Tanh((System.Math.Abs(s) - 0.8) / 0.2)));
        }

        return new AudioBuffer(result, SampleRate);
    }

    public override string ToString() => $"AudioBuffer({Duration:0.###} s @ {SampleRate} Hz, peak {PeakLevel})";
}

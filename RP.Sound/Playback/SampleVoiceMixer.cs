namespace RP.Sound.Playback;

/// <summary>
/// A pool of <see cref="SampleVoice"/>s, a looping bed, and a damped stereo delay, summed into one
/// interleaved stereo block. This is the real-time counterpart to <see cref="Mixing.Mixer"/>: that
/// one composes a finished piece offline, this one is fed by a game as events happen and is filled
/// from an audio device's callback thread.
///
/// The delay is the part that earns its place. Nothing in a typical game scene is in a room, and a
/// sound with no reflections at all is heard as small and close by; adding offset repeats that
/// darken as they decay puts every event in a large cold space, and does more for the character of
/// a whole palette than any individual voice does. The two taps are at different lengths and the
/// feedback crosses between channels, so repeats bounce side to side rather than sitting still.
///
/// Everything is sized up front and nothing allocates during <see cref="Fill"/> — a collection
/// while the device is waiting for samples is audible as a dropout, which is the one bug in an
/// audio path that users always notice.
/// </summary>
public sealed class SampleVoiceMixer
{
    /// <summary>How fast the bed's level and pitch chase their targets, per sample.</summary>
    private const float BedSmoothing = 0.0002f;

    private readonly SampleVoice[] voices;
    private readonly SampleVoice bed = new();
    private readonly object gate = new();

    private readonly int sampleRate;
    private readonly int maxFrames;
    private readonly float[] sendBuffer;

    private readonly float[] delayLeft;
    private readonly float[] delayRight;
    private readonly int tapLeft;
    private readonly int tapRight;
    private int delayPosition;
    private float dampedLeft, dampedRight;

    private AudioBuffer? bedSource;
    private double bedRate = 1, bedTargetRate = 1;
    private float bedLevel, bedTargetLevel;

    /// <param name="sampleRate">Must match the device being fed, and the buffers being played.</param>
    /// <param name="maxFrames">The largest block <see cref="Fill"/> will ever be asked for.</param>
    /// <param name="maxVoices">
    /// How many one-shots may sound at once. When they are all busy a new request is refused
    /// rather than stealing a voice, because a dropped sound in a dense moment is inaudible while
    /// a truncated one is a click.
    /// </param>
    public SampleVoiceMixer(int sampleRate = 44100, int maxFrames = 1024, int maxVoices = 24)
    {
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "A sample rate must be positive.");
        if (maxFrames <= 0) throw new ArgumentOutOfRangeException(nameof(maxFrames), maxFrames, "A block size must be positive.");
        if (maxVoices <= 0) throw new ArgumentOutOfRangeException(nameof(maxVoices), maxVoices, "A voice pool needs at least one voice.");

        this.sampleRate = sampleRate;
        this.maxFrames = maxFrames;
        this.sendBuffer = new float[maxFrames * 2];

        // One second of delay memory is ample for taps this short, and keeps the wrap arithmetic
        // to a single comparison.
        this.delayLeft = new float[sampleRate];
        this.delayRight = new float[sampleRate];
        this.tapLeft = (int)(sampleRate * 0.227);
        this.tapRight = (int)(sampleRate * 0.313);

        this.voices = new SampleVoice[maxVoices];
        for (int i = 0; i < this.voices.Length; i++) this.voices[i] = new SampleVoice();
    }

    /// <summary>Master gain applied before the saturator, 0–1.</summary>
    public Level Volume { get; set; } = new(0.6);

    /// <summary>How much delayed signal returns into the mix, 0–1.</summary>
    public double DelayMix { get; set; } = 0.44;

    /// <summary>
    /// How much of each repeat feeds the next, 0–0.9. High enough that a sound gets several
    /// audible reflections: a single repeat reads as a slapback artefact, whereas a decaying train
    /// of them reads as a large empty space.
    /// </summary>
    public double DelayFeedback { get; set; } = 0.52;

    /// <summary>Whether the bed is mixed in at all — the setting a player expects to be able to turn off.</summary>
    public bool BedEnabled { get; set; } = true;

    /// <summary>
    /// Starts a one-shot if a voice is free. Returns false when the pool is saturated, which a
    /// caller can safely ignore.
    /// </summary>
    public bool Play(AudioBuffer buffer, double rate = 1, Level? gain = null, double pan = 0, double send = 0)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (buffer.SampleRate != this.sampleRate)
            throw new ArgumentException($"This mixer runs at {this.sampleRate} Hz; playing a {buffer.SampleRate} Hz buffer would change its pitch and speed.", nameof(buffer));

        lock (this.gate)
        {
            foreach (SampleVoice voice in this.voices)
            {
                if (voice.Active) continue;
                voice.Start(buffer, rate, gain, pan, send);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Sets the looping bed and the rate and level it should settle at. Both are approached
    /// gradually rather than jumped to, so a bed that tracks a changing game state slides instead
    /// of stepping. Pass a different buffer to swap the bed outright.
    /// </summary>
    public void SetBed(AudioBuffer buffer, double rate = 1, Level? level = null)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (buffer.SampleRate != this.sampleRate)
            throw new ArgumentException($"This mixer runs at {this.sampleRate} Hz; playing a {buffer.SampleRate} Hz buffer would change its pitch and speed.", nameof(buffer));
        if (rate <= 0 || !double.IsFinite(rate))
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "A playback rate must be finite and positive.");

        lock (this.gate)
        {
            if (!ReferenceEquals(this.bedSource, buffer))
            {
                this.bedSource = buffer;
                this.bed.Start(buffer, rate, Level.Silence, looping: true);
                this.bedRate = rate;
            }

            this.bedTargetRate = rate;
            this.bedTargetLevel = (float)(level ?? Level.Silence).Linear;
        }
    }

    /// <summary>Silences everything at once, including the delay memory, so nothing rings on.</summary>
    public void StopAll()
    {
        lock (this.gate)
        {
            foreach (SampleVoice voice in this.voices) voice.Stop();
            this.bed.Stop();
            this.bedSource = null;
            this.bedLevel = 0;
            this.bedTargetLevel = 0;
        }

        Array.Clear(this.delayLeft);
        Array.Clear(this.delayRight);
        this.dampedLeft = this.dampedRight = 0;
    }

    /// <summary>
    /// Fills an interleaved stereo block — left, right, left, right — overwriting whatever was
    /// there. Called from the audio thread.
    /// </summary>
    public void Fill(float[] interleaved, int frames)
    {
        ArgumentNullException.ThrowIfNull(interleaved);
        if (frames < 0 || frames > this.maxFrames)
            throw new ArgumentOutOfRangeException(nameof(frames), frames, $"This mixer was built for blocks of up to {this.maxFrames} frames.");
        if (interleaved.Length < frames * 2)
            throw new ArgumentException("An interleaved stereo block needs two samples per frame.", nameof(interleaved));

        int samples = frames * 2;
        Array.Clear(interleaved, 0, samples);
        Array.Clear(this.sendBuffer, 0, samples);

        lock (this.gate)
        {
            foreach (SampleVoice voice in this.voices) voice.Render(interleaved, this.sendBuffer, frames);
            AdvanceBed(frames);
            this.bed.Render(interleaved, this.sendBuffer, frames);
        }

        ApplyDelay(interleaved, frames);

        // Soft saturation. With a full pool plus delay returns the sum can exceed full scale, and
        // hard clipping would buzz; this compresses peaks smoothly and leaves quiet material alone.
        float master = (float)Volume.Linear;
        for (int i = 0; i < samples; i++)
        {
            float value = interleaved[i] * master;
            interleaved[i] = value / (1 + System.Math.Abs(value));
        }
    }

    // The bed's smoothing is specified per sample, but its gain is only set once per block, so the
    // coefficient is converted: chasing a target once with 1−(1−k)^n lands in the same place as
    // chasing it n times with k.
    private void AdvanceBed(int frames)
    {
        if (this.bedSource is null || frames == 0) return;

        float target = BedEnabled ? this.bedTargetLevel : 0;
        float step = 1 - (float)System.Math.Pow(1 - BedSmoothing, frames);

        this.bedLevel += (target - this.bedLevel) * step;
        this.bedRate += (this.bedTargetRate - this.bedRate) * step;

        this.bed.Adjust(this.bedRate, new Level(this.bedLevel));
    }

    private void ApplyDelay(float[] interleaved, int frames)
    {
        float mix = (float)System.Math.Clamp(DelayMix, 0, 1);
        float feedback = (float)System.Math.Clamp(DelayFeedback, 0, 0.9);
        if (mix <= 0.001f) return;

        int length = this.delayLeft.Length;
        for (int i = 0; i < frames; i++)
        {
            int readLeft = this.delayPosition - this.tapLeft;
            if (readLeft < 0) readLeft += length;
            int readRight = this.delayPosition - this.tapRight;
            if (readRight < 0) readRight += length;

            float echoLeft = this.delayLeft[readLeft];
            float echoRight = this.delayRight[readRight];

            // Damp the feedback path so each repeat is darker than the last, the way a real space
            // absorbs high frequencies first. Undamped repeats sound like a digital fault.
            this.dampedLeft += (echoLeft - this.dampedLeft) * 0.42f;
            this.dampedRight += (echoRight - this.dampedRight) * 0.42f;

            // Cross the feedback between channels so repeats bounce side to side.
            this.delayLeft[this.delayPosition] = this.sendBuffer[i * 2] + this.dampedRight * feedback;
            this.delayRight[this.delayPosition] = this.sendBuffer[i * 2 + 1] + this.dampedLeft * feedback;

            interleaved[i * 2] += echoLeft * mix;
            interleaved[i * 2 + 1] += echoRight * mix;

            if (++this.delayPosition >= length) this.delayPosition = 0;
        }
    }
}

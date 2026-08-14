namespace RP.Sound.Playback;

/// <summary>
/// One buffer being played back, right now. Where the rest of the library describes sounds and
/// renders them offline, this is the other half of the job: a game has already rendered its
/// palette and needs to hear one of those buffers <em>at this instant</em>, at a pitch and
/// position the simulation decided a moment ago.
///
/// Pitch comes from the read rate, the way a sampler has always done it: read the buffer faster
/// than it was written and it plays higher and shorter. That is not free — a rate far from 1
/// audibly shortens the sound and stretches its formants — so a caller wanting a wide pitch range
/// should render the same description at a handful of base pitches and pick the nearest, keeping
/// the rate within roughly ±60% of unity. Between samples the read is linearly interpolated, which
/// is enough at these rates and, unlike anything better, costs nothing.
///
/// This is transient render state, like <see cref="DeterministicRandom"/> and the internal
/// <c>Biquad</c>: mutable by nature, never part of a description. Voices are pooled and reused
/// rather than allocated, because a garbage collection during a buffer fill is audible as a
/// dropout — so nothing here allocates once it is running.
/// </summary>
public sealed class SampleVoice
{
    private AudioBuffer? source;
    private double position;
    private double rate = 1;
    private float leftGain, rightGain, sendLevel;
    private float targetLeftGain, targetRightGain;
    private bool looping;

    /// <summary>Whether this voice is currently sounding, and therefore not free to be claimed.</summary>
    public bool Active => this.source is not null;

    /// <summary>
    /// Claims this voice and starts it from the top.
    /// </summary>
    /// <param name="buffer">The already-rendered samples to play.</param>
    /// <param name="rate">Playback speed, and therefore pitch: 2 is an octave up and half as long.</param>
    /// <param name="gain">How loud, before panning.</param>
    /// <param name="pan">−1 hard left to +1 hard right, using the library's constant-power law.</param>
    /// <param name="send">How much of this voice is fed to the mixer's delay bus, 0–1.</param>
    /// <param name="looping">Whether to wrap at the end rather than stop — for beds and engine hums.</param>
    public void Start(AudioBuffer buffer, double rate = 1, Level? gain = null, double pan = 0, double send = 0, bool looping = false)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (rate <= 0 || !double.IsFinite(rate))
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "A playback rate must be finite and positive.");
        if (pan is < -1 or > 1)
            throw new ArgumentOutOfRangeException(nameof(pan), pan, "Pan runs from −1 (left) to +1 (right).");
        if (send is < 0 or > 1 || !double.IsFinite(send))
            throw new ArgumentOutOfRangeException(nameof(send), send, "A send level is a fraction between 0 and 1.");

        this.source = buffer;
        this.position = 0;
        this.rate = rate;
        this.looping = looping;
        this.sendLevel = (float)send;
        SetGain(gain ?? Level.Unity, pan);
        this.leftGain = this.targetLeftGain;
        this.rightGain = this.targetRightGain;
    }

    /// <summary>Silences the voice and frees it for reuse.</summary>
    public void Stop() => this.source = null;

    /// <summary>
    /// Adjusts a sounding voice — for a looping bed whose loudness or pitch tracks the game state.
    /// The new gain is a <em>target</em>: <see cref="Render"/> slides to it across the block rather
    /// than snapping, because a gain that steps between blocks clicks at every boundary.
    /// </summary>
    public void Adjust(double rate, Level gain, double pan = 0)
    {
        if (rate <= 0 || !double.IsFinite(rate))
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "A playback rate must be finite and positive.");
        this.rate = rate;
        SetGain(gain, pan);
    }

    // The constant-power law that StereoBuffer.FromMono uses: the two gains are the cosine and
    // sine of one angle, so their squares always sum to 1 and a sound keeps its loudness as it
    // crosses the stereo field. A straight linear crossfade dips in the middle.
    private void SetGain(Level gain, double pan)
    {
        double angle = (System.Math.Clamp(pan, -1, 1) + 1) * System.Math.PI / 4;
        this.targetLeftGain = (float)(System.Math.Cos(angle) * gain.Linear);
        this.targetRightGain = (float)(System.Math.Sin(angle) * gain.Linear);
    }

    /// <summary>
    /// Adds this voice into an interleaved stereo block, and into the delay-send block alongside
    /// it. Called from the audio thread; allocates nothing.
    /// </summary>
    public void Render(float[] dry, float[] send, int frames)
    {
        AudioBuffer? buffer = this.source;
        if (buffer is null) return;

        int length = buffer.Length;
        if (length == 0)
        {
            this.source = null;
            return;
        }

        // Slide toward the requested gain across the block instead of jumping to it at the
        // boundary. One step per block would be a discontinuity in the waveform, heard as a click
        // every time a bed's level moves.
        float leftStep = frames == 0 ? 0 : (this.targetLeftGain - this.leftGain) / frames;
        float rightStep = frames == 0 ? 0 : (this.targetRightGain - this.rightGain) / frames;

        for (int i = 0; i < frames; i++)
        {
            if (this.position >= length)
            {
                if (!this.looping)
                {
                    this.source = null;
                    this.leftGain = this.targetLeftGain;
                    this.rightGain = this.targetRightGain;
                    return;
                }

                this.position %= length;
            }

            this.leftGain += leftStep;
            this.rightGain += rightStep;

            int index = (int)this.position;
            int next = index + 1;
            if (next >= length) next = this.looping ? 0 : index;

            float here = buffer[index];
            float sample = here + (float)((buffer[next] - here) * (this.position - index));

            float left = sample * this.leftGain;
            float right = sample * this.rightGain;
            dry[i * 2] += left;
            dry[i * 2 + 1] += right;

            if (this.sendLevel > 0)
            {
                send[i * 2] += left * this.sendLevel;
                send[i * 2 + 1] += right * this.sendLevel;
            }

            this.position += this.rate;
        }

        // Land exactly on the target rather than accumulating float error across many blocks.
        this.leftGain = this.targetLeftGain;
        this.rightGain = this.targetRightGain;
    }
}

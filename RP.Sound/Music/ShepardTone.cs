namespace RP.Sound.Music;

/// <summary>
/// The Shepard tone (Shepard, 1964): the auditory barber's pole. Several sine voices spaced an
/// octave apart all glide upward together; a fixed loudness window over log-frequency fades each
/// voice in at the bottom and out at the top, so as one voice leaves the ceiling another slips in
/// at the floor — and the ear, tracking relative motion, hears a rise that never arrives.
/// Endlessly deferred arrival is Huron's tension response in its purest form, which is why the
/// device scores chases and countdowns (Zimmer's Dunkirk soundtrack is built on it).
/// </summary>
public sealed class ShepardTone : ISound
{
    /// <summary>Seconds for the ensemble to climb (or fall) one octave.</summary>
    public double CycleSeconds { get; }

    /// <summary>+1 rising (anticipation building), −1 falling (endless descent).</summary>
    public int Direction { get; }

    /// <summary>The centre of the loudness window.</summary>
    public Frequency Centre { get; }

    public int Voices { get; }
    public Level Level { get; }
    public double Duration => double.PositiveInfinity;

    public ShepardTone(double cycleSeconds = 8, int direction = 1, Frequency? centre = null, int voices = 6, Level? level = null)
    {
        if (cycleSeconds <= 0 || !double.IsFinite(cycleSeconds))
            throw new ArgumentOutOfRangeException(nameof(cycleSeconds), cycleSeconds, "The cycle time must be finite and positive.");
        if (direction is not (1 or -1))
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Direction is +1 (rising) or −1 (falling).");
        if (voices is < 2 or > 12)
            throw new ArgumentOutOfRangeException(nameof(voices), voices, "Between 2 and 12 octave-spaced voices.");
        this.CycleSeconds = cycleSeconds;
        this.Direction = direction;
        this.Centre = centre ?? new Frequency(440);
        this.Voices = voices;
        this.Level = level ?? Level.FromDecibels(-10);
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        var phases = new double[Voices];
        double span = Voices; // octaves covered by the window

        for (int i = 0; i < samples.Length; i++)
        {
            double t = (double)i / context.SampleRate;
            double cyclePosition = Direction * t / CycleSeconds;
            double sum = 0;
            double weightSum = 0;
            for (int v = 0; v < Voices; v++)
            {
                // Each voice's position in octaves relative to the window centre, wrapping so a
                // voice leaving the top re-enters at the bottom.
                double octave = ((v + cyclePosition) % span + span) % span - span / 2;
                double frequency = Centre.Hertz * System.Math.Pow(2, octave);

                // The Gaussian loudness window over log-frequency — the heart of the illusion:
                // voices near the edges are inaudible, so their entries and exits are seamless.
                double weight = System.Math.Exp(-octave * octave / (2 * 1.1 * 1.1));

                sum += weight * System.Math.Sin(phases[v]);
                weightSum += weight;
                phases[v] += 2 * System.Math.PI * frequency / context.SampleRate;
            }

            samples[i] = (float)(sum / System.Math.Max(1e-9, weightSum) * Level.Linear);
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

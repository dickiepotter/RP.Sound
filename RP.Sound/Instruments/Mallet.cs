namespace RP.Sound.Instruments;

/// <summary>
/// The mallet-percussion family — marimba, xylophone, glockenspiel — as tuned-bar modal
/// synthesis. A uniform free bar rings at the inharmonic ratios 1 : 2.76 : 5.40
/// (see <see cref="RP.Sound.Physics.ModalBody"/>), which is why a glockenspiel's plain steel
/// bars shimmer with a bell-like clang. Marimba and xylophone makers carve a deep arch into the
/// underside of each wooden bar to <em>retune</em> those overtones onto musical intervals:
/// the marimba to 1 : 4 : 10 (double octave — mellow), the xylophone to 1 : 3 (an octave plus a
/// fifth — the bright "quint tuning" that gives it its edge). These ratios and the practice of
/// arch-tuning are standard references (Fletcher &amp; Rossing, The Physics of Musical
/// Instruments, ch. 19). Wood damps fast and steel barely at all, so the presets' decays follow
/// their materials.
/// </summary>
public sealed class Mallet : ISound
{
    /// <summary>Which instrument this bar belongs to (for display; the ratios are what matter).</summary>
    public string Name { get; }

    public Frequency Note { get; }

    /// <summary>Overtone frequencies as multiples of the fundamental (the fundamental's 1 included).</summary>
    public IReadOnlyList<double> ModeRatios { get; }

    /// <summary>Decay to −60 dB of the fundamental, seconds; higher modes die proportionally faster.</summary>
    public double Decay { get; }

    /// <summary>0 soft yarn … 1 hard brass: a harder mallet is a shorter contact, exciting brighter modes.</summary>
    public double MalletHardness { get; }

    public Level Level { get; }

    public double Duration => Decay;

    private Mallet(string name, Frequency note, double[] modeRatios, double decay, double malletHardness, Level? level)
    {
        if (note.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(note), note, "A bar must have a positive pitch.");
        if (decay <= 0 || !double.IsFinite(decay))
            throw new ArgumentOutOfRangeException(nameof(decay), decay, "A bar's decay must be finite and positive (seconds).");
        if (malletHardness is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(malletHardness), malletHardness, "Mallet hardness is a fraction between 0 and 1.");
        this.Name = name;
        this.Note = note;
        this.ModeRatios = modeRatios;
        this.Decay = decay;
        this.MalletHardness = malletHardness;
        this.Level = level ?? Level.Unity;
    }

    /// <summary>Rosewood bar arch-tuned to 1 : 4 : 10, struck with soft yarn — warm and round.</summary>
    public static Mallet Marimba(Frequency note, Level? level = null) =>
        new("marimba", note, new[] { 1.0, 4.0, 10.0 }, decay: DecayForPitch(note, 1.4), malletHardness: 0.25, level: level);

    /// <summary>Hard wood quint-tuned to 1 : 3 : 6, struck hard — the bright, cutting voice.</summary>
    public static Mallet Xylophone(Frequency note, Level? level = null) =>
        new("xylophone", note, new[] { 1.0, 3.0, 6.0 }, decay: DecayForPitch(note, 0.7), malletHardness: 0.8, level: level);

    /// <summary>Plain steel bar at the free bar's natural 1 : 2.76 : 5.40 — inharmonic sparkle that rings on.</summary>
    public static Mallet Glockenspiel(Frequency note, Level? level = null) =>
        new("glockenspiel", note, new[] { 1.0, 2.756, 5.404 }, decay: DecayForPitch(note, 3.5), malletHardness: 0.9, level: level);

    // Bigger (lower) bars ring longer: scale a preset's reference decay (its value at middle C)
    // by the square root of the pitch ratio — the gentle trend, not a cliff.
    private static double DecayForPitch(Frequency note, double decayAtMiddleC) =>
        System.Math.Clamp(decayAtMiddleC * System.Math.Sqrt(Frequency.MiddleC.Hertz / note.Hertz), 0.1, 6.0);

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        int active = System.Math.Min(samples.Length, context.SampleCount(System.Math.Min(Duration, duration)));
        DeterministicRandom random = context.CreateRandom($"mallet:{Name}:{Note.Hertz:0.###}");

        // Contact time shrinks as the mallet hardens (~0.3–2 ms); a mode weighted by
        // 1/(1+(f·τc)²) fades out above the bandwidth the strike can excite — the same
        // brightness-vs-hardness law as Physics.Impact.
        double contactTime = 0.0003 + 0.0017 * (1 - MalletHardness);

        for (int m = 0; m < ModeRatios.Count; m++)
        {
            double frequency = Note.Hertz * ModeRatios[m];
            if (frequency > context.SampleRate * 0.45) break;

            double excitation = 1.0 / (1.0 + System.Math.Pow(frequency * contactTime, 2));
            double amplitude = excitation / (m + 1);
            double modeDecay = Decay / ModeRatios[m]; // higher modes die proportionally faster
            double decayPerSample = System.Math.Exp(-6.9 / (modeDecay * context.SampleRate));
            double omega = 2 * System.Math.PI * frequency / context.SampleRate;
            double phase = random.NextDouble() * 2 * System.Math.PI;

            double envelope = amplitude;
            for (int i = 0; i < active; i++)
            {
                samples[i] += (float)(envelope * System.Math.Sin(phase));
                phase += omega;
                envelope *= decayPerSample;
                if (envelope < 1e-7) break;
            }
        }

        // The mallet's contact thump — brief, and darker for softer mallets.
        int thumpSamples = System.Math.Min(active, System.Math.Max(1, (int)(contactTime * 2 * context.SampleRate)));
        double state = 0;
        double thumpCoefficient = 0.5 + 0.4 * (1 - MalletHardness);
        for (int i = 0; i < thumpSamples; i++)
        {
            state = thumpCoefficient * state + (1 - thumpCoefficient) * random.NextSigned();
            double window = 1.0 - (double)i / thumpSamples;
            samples[i] += (float)(state * window * 0.25 * MalletHardness);
        }

        for (int i = 0; i < active; i++) samples[i] = (float)(samples[i] * Level.Linear);

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }

    public override string ToString() => $"{Name} {Note}";
}

namespace RP.Sound.Mixing;

/// <summary>
/// What a layer is <em>for</em> — its narrative priority, lowest to highest. When layers compete,
/// the mixer automatically ducks lower roles under higher ones, so a footstep is never buried by
/// its own weather and a critical cue is never buried by anything.
/// </summary>
public enum MixRole
{
    /// <summary>The world's bed: weather, room tone. Heard, rarely listened to.</summary>
    Ambience = 0,

    /// <summary>The underscore: drones, beds, motifs.</summary>
    Music = 1,

    /// <summary>The character's own contact with the world: footsteps, cloth, handling.</summary>
    Foley = 2,

    /// <summary>Discrete events the player must notice: impacts, whooshes, stingers.</summary>
    Effects = 3,

    /// <summary>Must always cut through: alarms, dialogue-adjacent cues, jump-scare hits.</summary>
    Critical = 4,
}

/// <summary>One placed, levelled, role-tagged sound in a mix.</summary>
public sealed class MixLayer
{
    public string Name { get; }
    public ISound Sound { get; }
    public MixRole Role { get; }
    public Level Trim { get; }
    public SoundPlacement Placement { get; }

    public MixLayer(string name, ISound sound, MixRole role, Level? trim = null, SoundPlacement? placement = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A mix layer needs a name.", nameof(name));
        this.Name = name;
        this.Sound = sound;
        this.Role = role;
        this.Trim = trim ?? Level.Unity;
        this.Placement = placement ?? SoundPlacement.Here;
    }
}

/// <summary>
/// The scene mixer: renders every layer, places it, and — the important part — keeps the layers
/// <em>functional</em> in Bregman's auditory-scene-analysis sense. Simultaneous sounds mask each
/// other, so intent must be protected structurally; the mixer's tool is sidechain ducking: each
/// layer is attenuated while any higher-priority layer is loud, by 3 dB per priority step (up to
/// 12 dB), with fast attack and slow release so the duck is felt as focus, not pumping.
/// (Two other separation tools — spectral slots and onset spacing — belong to sound design and
/// scheduling; the README's layering chapter covers all three.)
/// </summary>
public sealed class Mixer
{
    public IReadOnlyList<MixLayer> Layers { get; }

    public Mixer(params MixLayer[] layers)
    {
        if (layers.Length == 0) throw new ArgumentException("A mix needs at least one layer.", nameof(layers));
        this.Layers = (MixLayer[])layers.Clone();
    }

    /// <summary>A new mixer with one more layer — mixers are immutable like everything else.</summary>
    public Mixer With(MixLayer layer)
    {
        var layers = new MixLayer[Layers.Count + 1];
        for (int i = 0; i < Layers.Count; i++) layers[i] = Layers[i];
        layers[^1] = layer;
        return new Mixer(layers);
    }

    public StereoBuffer Render(AudioRenderContext context, double duration)
    {
        int length = context.SampleCount(duration);

        // Render every layer mono first (placement comes later; ducking works on the mono energy).
        var rendered = new AudioBuffer[Layers.Count];
        for (int i = 0; i < Layers.Count; i++)
        {
            MixLayer layer = Layers[i];
            rendered[i] = layer.Sound
                .Render(context, System.Math.Min(duration, layer.Sound.Duration))
                .FittedToDuration(duration)
                .Amplified(layer.Trim);
        }

        // Loudness envelopes (RMS over ~30 ms windows) per role priority, for the sidechains.
        var envelopes = new double[Layers.Count][];
        for (int i = 0; i < Layers.Count; i++) envelopes[i] = LoudnessEnvelope(rendered[i], context.SampleRate);

        StereoBuffer? mix = null;
        for (int i = 0; i < Layers.Count; i++)
        {
            double[] duck = DuckingGain(i, envelopes, length, context.SampleRate);
            var samples = new float[length];
            for (int s = 0; s < length; s++) samples[s] = (float)(rendered[i][s] * duck[s]);
            StereoBuffer placed = Layers[i].Placement.Apply(AudioBuffer.TakeOwnership(samples, context.SampleRate));
            mix = mix is null ? placed : mix.MixedWith(placed);
        }

        return mix!.SoftClipped();
    }

    private static double[] LoudnessEnvelope(AudioBuffer buffer, int sampleRate)
    {
        // A one-pole envelope follower on the squared signal ≈ running RMS.
        double attack = System.Math.Exp(-1.0 / (0.005 * sampleRate));
        double release = System.Math.Exp(-1.0 / (0.05 * sampleRate));
        var envelope = new double[buffer.Length];
        double state = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            double power = (double)buffer[i] * buffer[i];
            double coefficient = power > state ? attack : release;
            state = coefficient * state + (1 - coefficient) * power;
            envelope[i] = System.Math.Sqrt(state);
        }

        return envelope;
    }

    private double[] DuckingGain(int layerIndex, double[][] envelopes, int length, int sampleRate)
    {
        var gain = new double[length];
        MixRole role = Layers[layerIndex].Role;

        // How hard each higher-priority layer pushes this one down: 3 dB per priority step, 12 max.
        double maxDuckDb = 0;
        var sidechains = new List<(double[] Envelope, double DuckDb)>();
        for (int other = 0; other < Layers.Count; other++)
        {
            int priorityGap = (int)Layers[other].Role - (int)role;
            if (priorityGap <= 0) continue;
            double duckDb = System.Math.Min(12, 3.0 * priorityGap);
            sidechains.Add((envelopes[other], duckDb));
            maxDuckDb = System.Math.Max(maxDuckDb, duckDb);
        }

        if (sidechains.Count == 0)
        {
            Array.Fill(gain, 1);
            return gain;
        }

        // Attack 10 ms (duck in fast, before the masking happens), release 400 ms (let go slowly,
        // so the bed swells back instead of pumping).
        double attack = System.Math.Exp(-1.0 / (0.010 * sampleRate));
        double release = System.Math.Exp(-1.0 / (0.400 * sampleRate));
        const double threshold = 0.02; // sidechain RMS where ducking starts to bite
        double state = 0;
        for (int i = 0; i < length; i++)
        {
            double wantedDb = 0;
            foreach ((double[] envelope, double duckDb) in sidechains)
            {
                double drive = System.Math.Clamp((envelope[i] - threshold) / (0.2 - threshold), 0, 1);
                wantedDb = System.Math.Max(wantedDb, duckDb * drive);
            }

            double coefficient = wantedDb > state ? attack : release;
            state = coefficient * state + (1 - coefficient) * wantedDb;
            gain[i] = System.Math.Pow(10, -state / 20.0);
        }

        return gain;
    }
}

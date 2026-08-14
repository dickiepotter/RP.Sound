using RP.Sound.Ambience;
using RP.Sound.Mixing;

namespace RP.Sound.Music;

/// <summary>
/// A complete generative soundscape for a scene: hand it a mood (or a genre preset) and a bit of
/// weather, and it assembles the layered mix a game scene needs — ambient beds underneath, a
/// mood-voiced drone above them, tension devices when the mood calls for them, and sparse
/// stochastic accent events — all through the <see cref="Mixer"/>, so the layering keeps every
/// element functional (beds duck under accents automatically). Deterministic like everything
/// else: one seed, one scene; a new seed, a fresh but same-mood scene.
/// </summary>
public sealed class GenerativeScene
{
    public Mood Mood { get; }
    public bool HasWind { get; }
    public bool HasRain { get; }
    public bool HasFire { get; }
    public bool HasThunder { get; }

    public GenerativeScene(Mood mood, bool wind = true, bool rain = false, bool fire = false, bool thunder = false)
    {
        this.Mood = mood;
        this.HasWind = wind;
        this.HasRain = rain;
        this.HasFire = fire;
        this.HasThunder = thunder;
    }

    public StereoBuffer Render(AudioRenderContext context, double duration)
    {
        var layers = new List<MixLayer>();
        DeterministicRandom random = context.CreateRandom($"scene:{Mood}");

        // --- The beds (role: Ambience). Darker, tenser moods get stronger, gustier weather. ---
        double unease = System.Math.Max(0, -Mood.Valence) * 0.5 + Mood.Tension * 0.5;
        if (HasWind)
        {
            layers.Add(new MixLayer(
                "wind",
                new Wind(strength: 0.25 + 0.5 * unease, gustiness: 0.3 + 0.6 * unease),
                MixRole.Ambience,
                Level.FromDecibels(-14)));
        }

        if (HasRain)
            layers.Add(new MixLayer("rain", new Rain(0.3 + 0.5 * unease), MixRole.Ambience, Level.FromDecibels(-16)));

        if (HasFire)
            layers.Add(new MixLayer("fire", new Fire(0.4 + 0.3 * Mood.Arousal), MixRole.Ambience, Level.FromDecibels(-14), new SoundPlacement(pan: -0.3, distance: 3)));

        // --- The underscore (role: Music). ---
        layers.Add(new MixLayer("drone", Drone.ForMood(Mood), MixRole.Music, Level.FromDecibels(-6)));

        // The tension devices arrive only when the mood asks for them — an endless rise for
        // sustained suspense, the listener's own pulse for dread.
        if (Mood.Tension > 0.55)
        {
            layers.Add(new MixLayer(
                "shepard",
                new ShepardTone(cycleSeconds: 14 - 6 * Mood.Tension, centre: Mood.Root.Transposed(24), level: Level.FromDecibels(-22 + 8 * Mood.Tension)),
                MixRole.Music));
        }

        if (Mood.Tension > 0.65)
            layers.Add(new MixLayer("heartbeat", Heartbeat.ForMood(Mood), MixRole.Foley, Level.FromDecibels(-10)));

        // --- Sparse accent events (role: Effects), Poisson-spaced at the mood's event density,
        // each placed somewhere off-centre and a little away — the scene has edges. ---
        double meanInterval = 60.0 / Mood.EventsPerMinute;
        double time = random.Range(0.5, meanInterval);
        ISound? events = null;
        while (time < duration)
        {
            ISound accent = random.NextChance(0.35)
                ? new Riser(duration: random.Range(2, 3.5), intensity: 0.3 + 0.6 * Mood.Tension, level: Level.FromDecibels(-14))
                : new Stinger(Mood, Level.FromDecibels(-12), duration: 2);
            ISound placedAccent = accent.Delayed(time);
            events = events is null ? placedAccent : events.MixedWith(placedAccent);
            time += meanInterval * random.Range(0.5, 1.5);
        }

        if (events is not null)
        {
            layers.Add(new MixLayer("events", events, MixRole.Effects, Level.Unity, new SoundPlacement(pan: random.Range(-0.6, 0.6), distance: random.Range(2, 8))));
        }

        return new Mixer(layers.ToArray()).Render(context, duration).NormalizedOrDefault(new Level(0.9));
    }
}

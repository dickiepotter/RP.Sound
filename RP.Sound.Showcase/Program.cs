using RP.Sound;
using RP.Sound.Ambience;
using RP.Sound.Effects;
using RP.Sound.Instruments;
using RP.Sound.IO;
using RP.Sound.Music;
using RP.Sound.Physics;
using RP.Sound.Synthesis;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Every endpoint renders a deterministic buffer from an immutable description and returns it as
// WAV. `seed` re-rolls the random character of a sound without changing its physics.
static AudioRenderContext Context(int seed) => new(SampleRate: 44100, Seed: seed);

static double ClampDuration(double duration) => Math.Clamp(duration, 0.1, 30);

static IResult Wav(AudioBuffer buffer) => Results.Bytes(WavFile.ToBytes(buffer.SoftClipped()), "audio/wav");

// ---- Metadata for the client UI ----
app.MapGet("/api/meta", () => Results.Json(new
{
    materials = Material.Presets.Select(m => m.Name).ToArray(),
    surfaces = ParticleSurface.PresetNames,
    moods = Mood.Presets.Select(p => p.Name).ToArray(),
}));

// ---- Physics: Gaver's taxonomy — impact, scrape, roll — plus what falls out of it ----
app.MapGet("/api/physics/impact", (string material = "steel", double size = 0.4, double velocity = 3, double hardness = 0.7, int seed = 0) =>
    Wav(new Impact(new ModalBody(Material.FromName(material), size), velocity, strikerHardness: hardness)
        .Render(Context(seed))));

app.MapGet("/api/physics/drop", (string material = "wood", double size = 0.25, double height = 1.5, double gravity = 9.81, int seed = 0) =>
    Wav(BounceSequence.FromDrop(new ModalBody(Material.FromName(material), size), height, gravity: gravity)
        .Render(Context(seed))));

app.MapGet("/api/physics/scrape", (string material = "stone", double speed = 1, double roughness = 0.5, double force = 5, double duration = 2, int seed = 0) =>
    Wav(new Scrape(new ModalBody(Material.FromName(material), 0.5), speed, ClampDuration(duration), force, roughness)
        .Render(Context(seed))));

app.MapGet("/api/physics/roll", (string material = "wood", double radius = 0.1, double speed = 2, double duration = 3, int seed = 0) =>
    Wav(new Rolling(new ModalBody(Material.FromName(material), 0.3), radius, speed, ClampDuration(duration))
        .Render(Context(seed))));

app.MapGet("/api/physics/surface", (string name = "gravel", double energy = 1, int seed = 0) =>
    Wav(ParticleSurface.FromName(name, energy).Render(Context(seed))));

app.MapGet("/api/physics/footsteps", (string surface = "gravel", double speed = 1.4, double weight = 75, double duration = 4, int seed = 0) =>
{
    Footsteps steps = Material.TryFromName(surface, out Material material)
        ? new Footsteps(material, speed, ClampDuration(duration), weight)
        : new Footsteps(ParticleSurface.FromName(surface), speed, ClampDuration(duration), weight);
    return Wav(steps.Render(Context(seed)));
});

app.MapGet("/api/physics/whoosh", (double speed = 20, double size = 0.05, double duration = 1.2, bool passBy = true, int seed = 0) =>
    Wav(new Whoosh(speed, size, ClampDuration(duration), passBy).Render(Context(seed))));

// ---- Synthesis ----
app.MapGet("/api/synth/pluck", (string note = "A3", double damping = 0.1, int seed = 0) =>
    Wav(new PluckedString(Frequency.FromNote(note), 2.5, damping).Render(Context(seed))));

// ---- Ambience ----
app.MapGet("/api/ambience/wind", (double strength = 0.5, double gustiness = 0.5, double duration = 8, int seed = 0) =>
    Wav(new Wind(strength, gustiness).Render(Context(seed), ClampDuration(duration))));

app.MapGet("/api/ambience/rain", (double intensity = 0.5, double hardness = 0.4, double duration = 8, int seed = 0) =>
    Wav(new Rain(intensity, hardness).Render(Context(seed), ClampDuration(duration))));

app.MapGet("/api/ambience/fire", (double intensity = 0.5, double duration = 8, int seed = 0) =>
    Wav(new Fire(intensity).Render(Context(seed), ClampDuration(duration))));

app.MapGet("/api/ambience/thunder", (double distance = 2000, int seed = 0) =>
    Wav(new Thunder(distance).Render(Context(seed))));

// ---- Mood and tension ----
app.MapGet("/api/music/drone", (string mood = "calm", double duration = 8, int seed = 0) =>
    Wav(Drone.ForMood(Mood.FromName(mood)).Render(Context(seed), ClampDuration(duration))));

app.MapGet("/api/music/shepard", (double cycle = 8, int direction = 1, double duration = 12, int seed = 0) =>
    Wav(new ShepardTone(cycle, direction).Render(Context(seed), ClampDuration(duration))));

app.MapGet("/api/music/riser", (double duration = 4, double intensity = 0.7, int seed = 0) =>
    Wav(new Riser(ClampDuration(duration), intensity).Render(Context(seed))));

app.MapGet("/api/music/stinger", (string mood = "horror", int seed = 0) =>
    Wav(new Stinger(Mood.FromName(mood)).Render(Context(seed))));

app.MapGet("/api/music/heartbeat", (double bpm = 90, double duration = 6, int seed = 0) =>
    Wav(new Heartbeat(bpm).Render(Context(seed), ClampDuration(duration))));

// ---- The flagship: a full generative scene, layered and ducked by the mixer ----
app.MapGet("/api/scene", (string mood = "horror", bool wind = true, bool rain = false, bool fire = false, double duration = 15, int seed = 0) =>
{
    var scene = new GenerativeScene(Mood.FromName(mood), wind, rain, fire);
    return Results.Bytes(WavFile.ToBytes(scene.Render(Context(seed), ClampDuration(duration))), "audio/wav");
});

// ---- Instruments: the rhythm-section voices ----
app.MapGet("/api/instruments/kick", (double pitch = 50, double punch = 0.6, double decay = 0.5, int seed = 0) =>
    Wav(new KickDrum(pitch, punch, decay).Render(Context(seed))));

app.MapGet("/api/instruments/snare", (double pitch = 190, double snappy = 0.7, double decay = 0.25, int seed = 0) =>
    Wav(new SnareDrum(pitch, snappy, decay).Render(Context(seed))));

app.MapGet("/api/instruments/hihat", (bool open = false, int seed = 0) =>
    Wav(new HiHat(open).Render(Context(seed))));

app.MapGet("/api/instruments/tom", (double pitch = 110, double decay = 0.4, int seed = 0) =>
    Wav(new TomDrum(pitch, decay).Render(Context(seed))));

app.MapGet("/api/instruments/cymbal", (double decay = 2.5, int seed = 0) =>
    Wav(new Cymbal(decay).Render(Context(seed))));

app.MapGet("/api/instruments/bass", (string note = "E1", double tone = 0.4, int seed = 0) =>
    Wav(new BassGuitar(Frequency.FromNote(note), 2, tone).Render(Context(seed))));

app.MapGet("/api/instruments/guitar", (string note = "A3", double damping = 0.15, double pickPosition = 0.2, int seed = 0) =>
    Wav(new Guitar(Frequency.FromNote(note), 2.5, damping, pickPosition).Render(Context(seed))));

app.MapGet("/api/instruments/powerchord", (string note = "E2", double drive = 5, int seed = 0) =>
    Wav(Guitar.PowerChord(Frequency.FromNote(note), 2.5).Distorted(Math.Clamp(drive, 1, 12), Level.FromDecibels(-3))
        .Render(Context(seed), 2.5)));

app.MapGet("/api/instruments/mallet", (string kind = "marimba", string note = "C4", int seed = 0) =>
{
    Frequency pitch = Frequency.FromNote(note);
    Mallet mallet = kind.ToLowerInvariant() switch
    {
        "xylophone" => Mallet.Xylophone(pitch),
        "glockenspiel" => Mallet.Glockenspiel(pitch),
        _ => Mallet.Marimba(pitch),
    };
    return Wav(mallet.Render(Context(seed)));
});

app.MapGet("/api/instruments/organ", (string note = "C3", string registration = "888000000", double duration = 1.5, int seed = 0) =>
    Wav(new Organ(Frequency.FromNote(note), ClampDuration(duration), registration).Render(Context(seed))));

app.MapGet("/api/instruments/flute", (string note = "A4", double breathiness = 0.3, double duration = 1.5, int seed = 0) =>
    Wav(new Flute(Frequency.FromNote(note), ClampDuration(duration), breathiness).Render(Context(seed))));

app.MapGet("/api/instruments/brass", (string note = "C3", double brightness = 0.7, double duration = 1.2, int seed = 0) =>
    Wav(new Brass(Frequency.FromNote(note), ClampDuration(duration), brightness).Render(Context(seed))));

// ---- The synthesizer: every patch parameter exposed, plus the preset shortcuts ----
app.MapGet("/api/synth/play", (
    string note = "A3", double duration = 1.5,
    string osc1 = "sawtooth", string osc2 = "sawtooth", double detune = 7, double mix = 0.5, double noise = 0,
    double cutoff = 2000, double resonance = 0.9, double filterOctaves = 2,
    double attack = 0.01, double decay = 0.1, double sustainDb = -3, double release = 0.2,
    string lfoWave = "sine", double lfoRate = 5, double vibrato = 0, double wobble = 0, double tremolo = 0,
    int seed = 0) =>
{
    static Waveform Parse(string name) => Enum.TryParse(name, ignoreCase: true, out Waveform w) ? w : Waveform.Sawtooth;
    var amplitude = Envelope.Adsr(Math.Clamp(attack, 0, 5), Math.Clamp(decay, 0, 5), Level.FromDecibels(Math.Min(0, sustainDb)), Math.Clamp(release, 0, 5));
    var patch = new SynthPatch(
        Parse(osc1), Parse(osc2),
        oscillator2DetuneCents: Math.Clamp(detune, -2400, 2400),
        oscillatorMix: Math.Clamp(mix, 0, 1),
        noiseMix: Math.Clamp(noise, 0, 1),
        filterCutoff: Math.Clamp(cutoff, 20, 12000),
        filterResonance: Math.Clamp(resonance, 0.05, 20),
        filterEnvelopeOctaves: Math.Clamp(filterOctaves, 0, 8),
        amplitudeEnvelope: amplitude,
        lfo: new Lfo(Parse(lfoWave), Math.Clamp(lfoRate, 0, 30),
            pitchCents: Math.Clamp(vibrato, 0, 100),
            cutoffOctaves: Math.Clamp(wobble, 0, 4),
            tremoloDepth: Math.Clamp(tremolo, 0, 1)));
    return Wav(new Synthesizer(patch, Frequency.FromNote(note), ClampDuration(duration)).Render(Context(seed)));
});

app.MapGet("/api/synth/preset", (string name = "bass", string note = "A2", double duration = 1.5, double wobbleRate = 4.67, int seed = 0) =>
{
    SynthPatch patch = name.ToLowerInvariant() switch
    {
        "lead" => SynthPatch.Lead,
        "pluck" => SynthPatch.Pluck,
        "pad" => SynthPatch.Pad,
        "wobble" => SynthPatch.Wobble(Math.Clamp(wobbleRate, 0.5, 20)),
        _ => SynthPatch.Bass,
    };
    return Wav(new Synthesizer(patch, Frequency.FromNote(note), ClampDuration(duration)).Render(Context(seed)));
});

// ---- Background music: the genre generators (each renders its natural loop + ring-out) ----
static double MusicLength(double natural) => Math.Min(natural, 40);

app.MapGet("/api/music/genre/blues", (string root = "E2", double bpm = 96, int choruses = 1, int seed = 0) =>
{
    var track = new BluesTrack(Frequency.FromNote(root), Math.Clamp(choruses, 1, 2), Math.Clamp(bpm, 60, 160));
    return Wav(track.Render(Context(seed), MusicLength(track.Duration)));
});

app.MapGet("/api/music/genre/rock", (string root = "E2", double bpm = 120, int bars = 8, int seed = 0) =>
{
    var track = new RockTrack(Frequency.FromNote(root), Math.Clamp(bars, 4, 16), Math.Clamp(bpm, 90, 160));
    return Wav(track.Render(Context(seed), MusicLength(track.Duration)));
});

app.MapGet("/api/music/genre/dubstep", (string root = "A1", double bpm = 140, int bars = 8, int seed = 0) =>
{
    var track = new DubstepTrack(Frequency.FromNote(root), Math.Clamp(bars, 4, 16), Math.Clamp(bpm, 135, 145));
    return Wav(track.Render(Context(seed), MusicLength(track.Duration)));
});

app.MapGet("/api/music/genre/house", (string root = "A2", double bpm = 124, int bars = 8, int seed = 0) =>
{
    var track = new HouseTrack(Frequency.FromNote(root), Math.Clamp(bars, 4, 16), Math.Clamp(bpm, 118, 130));
    return Wav(track.Render(Context(seed), MusicLength(track.Duration)));
});

app.MapGet("/api/music/genre/electronica", (string root = "A2", double bpm = 85, int bars = 8, int seed = 0) =>
{
    var track = new ElectronicaTrack(Frequency.FromNote(root), Math.Clamp(bars, 4, 16), Math.Clamp(bpm, 60, 110));
    return Wav(track.Render(Context(seed), MusicLength(track.Duration)));
});

app.Run();

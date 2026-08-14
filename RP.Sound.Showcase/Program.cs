using RP.Sound;
using RP.Sound.Ambience;
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

app.Run();

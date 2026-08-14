using RP.Sound;
using RP.Sound.Games;
using RP.Sound.IO;

// Deliberately in the global namespace, like Program.cs's own top-level statements, so wiring it
// up costs that file a single line and no using directive.

/// <summary>
/// The science-fiction palette, one endpoint per gesture. These live in their own file rather than
/// in <c>Program.cs</c> because they are the palette two games audition against while their sound
/// is being ported: being able to play the library's version of a zap next to the game's own is
/// the whole verification method.
/// </summary>
public static class SciFiEndpoints
{
    public static void MapSciFi(this WebApplication app)
    {
        static AudioRenderContext Context(int seed) => new(SampleRate: 44100, Seed: seed);

        static IResult Wav(ISound sound, int seed) =>
            Results.Bytes(WavFile.ToBytes(sound.Render(Context(seed), sound.Duration).SoftClipped()), "audio/wav");

        // Pitch is the one control each preset shares: a game maps it from whatever it knows —
        // mass, size, charge — so the showcase exposes it directly.
        app.MapGet("/api/scifi/zap", (double pitch = 900, int seed = 0) => Wav(SciFi.Zap(pitch), seed));
        app.MapGet("/api/scifi/implode", (double pitch = 320, int seed = 0) => Wav(SciFi.Implode(pitch), seed));
        app.MapGet("/api/scifi/chime", (double pitch = 520, int seed = 0) => Wav(SciFi.Chime(pitch), seed));
        app.MapGet("/api/scifi/fission", (double pitch = 620, int seed = 0) => Wav(SciFi.Fission(pitch), seed));

        // Fixed rather than pitched: both are properties of the machine, not of what it acts on.
        app.MapGet("/api/scifi/shimmer", (int seed = 0) => Wav(SciFi.Shimmer(), seed));
        app.MapGet("/api/scifi/thrust", (int seed = 0) => Wav(SciFi.Thrust(), seed));

        // Rendered as two loops back to back, so the seam is audible — or, if the cycle-snapping
        // is doing its job, inaudible.
        app.MapGet("/api/scifi/drone", (double pitch = 55, double duration = 2, int seed = 0) =>
        {
            double length = Math.Clamp(duration, 0.5, 10);
            AudioBuffer loop = SciFi.Drone(pitch, length).Render(Context(seed), length);
            return Results.Bytes(WavFile.ToBytes(loop.Then(loop).SoftClipped()), "audio/wav");
        });
    }
}

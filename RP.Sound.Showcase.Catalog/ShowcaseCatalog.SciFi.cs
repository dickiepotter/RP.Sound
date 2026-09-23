using RP.Sound.Games;
using RP.Sound.IO;

namespace RP.Sound.Showcase;

/// <summary>
/// The science-fiction palette, one entry per gesture. These live in their own file rather than
/// beside the rest of the catalog because they are the palette two games audition against while
/// their sound is being ported: being able to play the library's version of a zap next to the
/// game's own is the whole verification method.
/// </summary>
public static partial class ShowcaseCatalog
{
    private static void AddSciFi()
    {
        static byte[] Render(ISound sound, DemoParameters q) =>
            WavFile.ToBytes(sound.Render(Context(q), sound.Duration).SoftClipped());

        // Pitch is the one control each preset shares: a game maps it from whatever it knows —
        // mass, size, charge — so the showcase exposes it directly.
        Add("scifi/zap", q => Render(SciFi.Zap(q.Double("pitch", 900)), q));
        Add("scifi/implode", q => Render(SciFi.Implode(q.Double("pitch", 320)), q));
        Add("scifi/chime", q => Render(SciFi.Chime(q.Double("pitch", 520)), q));
        Add("scifi/fission", q => Render(SciFi.Fission(q.Double("pitch", 620)), q));

        // Fixed rather than pitched: both are properties of the machine, not of what it acts on.
        Add("scifi/shimmer", q => Render(SciFi.Shimmer(), q));
        Add("scifi/thrust", q => Render(SciFi.Thrust(), q));

        // Rendered as two loops back to back, so the seam is audible — or, if the cycle-snapping
        // is doing its job, inaudible.
        Add("scifi/drone", q =>
        {
            double length = Math.Clamp(q.Double("duration", 2), 0.5, 10);
            AudioBuffer loop = SciFi.Drone(q.Double("pitch", 55), length).Render(Context(q), length);
            return WavFile.ToBytes(loop.Then(loop).SoftClipped());
        });
    }
}

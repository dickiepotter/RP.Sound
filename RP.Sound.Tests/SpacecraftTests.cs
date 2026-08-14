using RP.Sound.Games;

namespace RP.Sound.Tests;

public class SpacecraftTests
{
    private static readonly AudioRenderContext Context = new(SampleRate: 44100, Seed: 0);

    public static IEnumerable<object[]> Palette()
    {
        yield return new object[] { "engine", Spacecraft.EngineDrone() };
        yield return new object[] { "zap", Spacecraft.Zap() };
        yield return new object[] { "impact", Spacecraft.Impact() };
        yield return new object[] { "clang", Spacecraft.Clang() };
        yield return new object[] { "fizz", Spacecraft.ShieldFizz() };
        yield return new object[] { "warning", Spacecraft.Warning(880) };
        yield return new object[] { "chatter", Spacecraft.Chatter(1) };
        yield return new object[] { "missile", Spacecraft.MissileLaunch() };
        yield return new object[] { "hiss", Spacecraft.Hiss() };
        yield return new object[] { "hitTick", Spacecraft.HitTick() };
        yield return new object[] { "explosion", Spacecraft.Explosion() };
    }

    [Theory]
    [MemberData(nameof(Palette))]
    public void EveryPreset_SoundsAndLeavesHeadroom(string name, ISound sound)
    {
        // These are mixed several at a time under a master gain, so each needs to be clearly
        // audible on its own and still leave room for the others.
        AudioBuffer buffer = sound.Render(Context, sound.Duration);
        Assert.InRange(buffer.PeakLevel.Linear, 0.05, 0.9);
        Assert.True(buffer.RmsLevel.Linear > 0.005, $"{name} is too quiet to hear in a mix");
    }

    [Theory]
    [MemberData(nameof(Palette))]
    public void EveryPreset_IsDeterministic(string name, ISound sound)
    {
        Assert.Equal(
            sound.Render(Context, sound.Duration).Samples.ToArray(),
            sound.Render(Context, sound.Duration).Samples.ToArray());
    }

    [Fact]
    public void Explosions_DifferBySeed()
    {
        // Every blast sounding identical is the thing that gives procedural audio away, so the
        // render context's seed has to reach the noise inside.
        ISound explosion = Spacecraft.Explosion();
        Assert.NotEqual(
            explosion.Render(Context with { Seed = 1 }, 1.1).Samples.ToArray(),
            explosion.Render(Context with { Seed = 2 }, 1.1).Samples.ToArray());
    }

    [Fact]
    public void Chatter_GivesEachCallSignItsOwnRhythm()
    {
        // Variants must differ in cadence, not just in noise, or every voice on the radio sounds
        // like the same person. Rendered under one seed so only the variant can be responsible.
        AudioBuffer first = Spacecraft.Chatter(1).Render(Context, 0.9);
        AudioBuffer second = Spacecraft.Chatter(4).Render(Context, 0.9);
        Assert.NotEqual(first.Samples.ToArray(), second.Samples.ToArray());

        // And the same call sign always speaks the same way.
        Assert.Equal(
            Spacecraft.Chatter(3).Render(Context, 0.9).Samples.ToArray(),
            Spacecraft.Chatter(3).Render(Context, 0.9).Samples.ToArray());
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void EngineDrone_MeetsItselfAtTheSeam(double duration)
    {
        // It plays for the whole game, so the join has to be inaudible: the step across it must be
        // no larger than the steps the waveform already takes inside the loop.
        AudioBuffer loop = Spacecraft.EngineDrone(duration).Render(Context, duration);

        float seam = System.Math.Abs(loop[0] - loop[loop.Length - 1]);
        float largestInside = 0;
        for (int i = 1; i < loop.Length; i++)
            largestInside = System.Math.Max(largestInside, System.Math.Abs(loop[i] - loop[i - 1]));

        Assert.True(seam <= largestInside, $"seam step {seam} exceeded the largest internal step {largestInside}");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Warning_LastsAsLongAsItsPipsAndGaps(int beeps)
    {
        // The alarm family is one generator varied by pitch and count, so the length has to follow
        // the count exactly or the cockpit alarms overlap each other.
        ISound warning = Spacecraft.Warning(880, beeps, beepSeconds: 0.07, gapSeconds: 0.05);
        Assert.Equal(beeps * 0.07 + (beeps - 1) * 0.05, warning.Duration, 3);
    }

    [Fact]
    public void Warning_RejectsAnAlarmWithNoPips()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Spacecraft.Warning(880, beeps: 0));
    }

    [Fact]
    public void Warning_PitchIsWhatSeparatesTheAlarms()
    {
        // Missile lock is high and fast, hull critical low and slow. They must be plainly different
        // sounds, not the same sound at two volumes.
        AudioBuffer lockOn = Spacecraft.Warning(980, 4, 0.05, 0.04).Render(Context, 0.32);
        AudioBuffer hull = Spacecraft.Warning(340, 3, 0.14, 0.10).Render(Context, 0.62);
        Assert.True(hull.Duration > lockOn.Duration * 1.5);
    }
}

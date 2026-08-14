using RP.Sound.Games;

namespace RP.Sound.Tests;

public class SciFiTests
{
    private static readonly AudioRenderContext Context = new(SampleRate: 44100, Seed: 0);

    public static IEnumerable<object[]> OneShots()
    {
        yield return new object[] { "zap", SciFi.Zap(900), 0.142 };
        yield return new object[] { "implode", SciFi.Implode(320), 0.606 };
        yield return new object[] { "chime", SciFi.Chime(520), 0.360 };
        yield return new object[] { "fission", SciFi.Fission(620), 0.244 };
        yield return new object[] { "shimmer", SciFi.Shimmer(), 0.500 };
        yield return new object[] { "thrust", SciFi.Thrust(), 0.275 };
    }

    [Theory]
    [MemberData(nameof(OneShots))]
    public void EveryPreset_LastsAsLongAsItsLongestVoice(string name, ISound sound, double expected)
    {
        Assert.Equal(expected, sound.Duration, 3);
        Assert.Equal(expected, sound.Render(Context, sound.Duration).Duration, 3);
    }

    [Theory]
    [MemberData(nameof(OneShots))]
    public void EveryPreset_LeavesHeadroom(string name, ISound sound, double expected)
    {
        // These are mixed together several at a time under a master gain, so each one has to
        // arrive well below full scale. A preset that peaks near 1 on its own would distort the
        // moment a second event overlapped it.
        AudioBuffer buffer = sound.Render(Context, sound.Duration);
        Assert.InRange(buffer.PeakLevel.Linear, 0.05, 0.8);
    }

    [Theory]
    [MemberData(nameof(OneShots))]
    public void EveryPreset_IsDeterministic(string name, ISound sound, double expected)
    {
        Assert.Equal(
            sound.Render(Context, sound.Duration).Samples.ToArray(),
            sound.Render(Context, sound.Duration).Samples.ToArray());
    }

    [Theory]
    [InlineData(34)]
    [InlineData(55)]
    [InlineData(78)]
    public void Drone_MeetsItselfAtTheSeam(double hertz)
    {
        // The whole point of snapping to a whole number of cycles: played end to end, the step
        // across the join has to be no larger than the steps the waveform already takes inside the
        // loop, or the seam is a discontinuity and clicks once per repeat.
        AudioBuffer loop = SciFi.Drone(hertz, 2.0).Render(Context, 2.0);

        float seam = System.Math.Abs(loop[0] - loop[loop.Length - 1]);
        float largestInside = 0;
        for (int i = 1; i < loop.Length; i++)
            largestInside = System.Math.Max(largestInside, System.Math.Abs(loop[i] - loop[i - 1]));

        Assert.True(seam <= largestInside, $"seam step {seam} exceeded the largest internal step {largestInside}");
    }

    [Fact]
    public void Drone_SnapsToAWholeNumberOfCycles()
    {
        // 55.4 Hz over 2 s is 110.8 cycles, which cannot close a loop; it is nudged to the nearest
        // whole 111 cycles, which over 2 s is 55.5 Hz. The nudge is small enough to be inaudible
        // and is what buys a seamless join.
        AudioBuffer asked = SciFi.Drone(55.4, 2.0).Render(Context, 2.0);
        AudioBuffer snapped = SciFi.Drone(55.5, 2.0).Render(Context, 2.0);
        Assert.Equal(snapped.Samples.ToArray(), asked.Samples.ToArray());
    }

    [Fact]
    public void Drone_StaysLowAndSteady()
    {
        AudioBuffer loop = SciFi.Drone(55, 2.0).Render(Context, 2.0);
        Assert.InRange(loop.PeakLevel.Linear, 0.3, 1.0);

        // A bed has to be even: no section of it should be much louder than any other, or it
        // pumps under everything else.
        int slice = loop.Length / 8;
        double quietest = double.MaxValue, loudest = 0;
        for (int i = 0; i < 8; i++)
        {
            double rms = AudioBuffer.FromSamples(loop.Samples.Slice(i * slice, slice), loop.SampleRate).RmsLevel.Linear;
            quietest = System.Math.Min(quietest, rms);
            loudest = System.Math.Max(loudest, rms);
        }

        Assert.True(loudest < quietest * 1.5, "the bed should not pump");
    }
}

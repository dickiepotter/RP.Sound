using RP.Sound.Music;

namespace RP.Sound.Tests;

public class MusicTests
{
    private static readonly AudioRenderContext Context = new(SampleRate: 22050, Seed: 5);

    [Fact]
    public void Scale_DegreesWrapUpwardByOctaves()
    {
        Scale major = Scale.Major(Frequency.FromNote("C4"));
        Assert.Equal(major.Root.Hertz, major.Degree(0).Hertz, 6);
        Assert.Equal(major.Root.Transposed(12).Hertz, major.Degree(7).Hertz, 6); // 7 steps = the octave
        Assert.Equal(major.Root.Transposed(7).Hertz, major.Degree(4).Hertz, 6);  // the fifth
    }

    [Fact]
    public void MoodPresets_AllProduceValidMappings()
    {
        foreach ((string name, Mood mood) in Mood.Presets)
        {
            Assert.InRange(mood.Tempo, 20, 200);
            Assert.True(mood.Root.Hertz > 20);
            Assert.True(mood.Brightness.Hertz > 200);
            Assert.NotNull(mood.Scale);
            Assert.True(Mood.TryFromName(name, out _));
        }
    }

    [Fact]
    public void Mood_HigherArousalIsFaster()
    {
        Assert.True(Mood.FastPaced.Tempo > Mood.Calm.Tempo);
    }

    [Fact]
    public void Mood_DarkMoodsSitLowerAndDarker()
    {
        Assert.True(Mood.Horror.Root < Mood.Fun.Root);
        Assert.True(Mood.Horror.Brightness < Mood.Fun.Brightness);
        Assert.True(Mood.Horror.Detune > Mood.Calm.Detune);
    }

    [Fact]
    public void Mood_TensionSelectsTheClusterScale()
    {
        Assert.Equal("cluster", Mood.Horror.Scale.Name);
        Assert.Equal("major", Mood.Fun.Scale.Name);
    }

    [Fact]
    public void ShepardTone_KeepsSteadyLoudness_WhileEndlesslyRising()
    {
        // The whole illusion: it must not actually get louder or quieter over a cycle.
        AudioBuffer buffer = new ShepardTone(cycleSeconds: 2).Render(Context, 6);
        double first = buffer.FittedToDuration(2).RmsLevel.Linear;
        AudioBuffer last2s = AudioBuffer.FromSamples(buffer.Samples[(buffer.Length * 2 / 3)..], buffer.SampleRate);
        Assert.InRange(last2s.RmsLevel.Linear / first, 0.7, 1.4);
    }

    [Fact]
    public void Heartbeat_ForMood_RaisesThePulseWithTension()
    {
        Assert.True(Heartbeat.ForMood(Mood.Horror).Bpm > Heartbeat.ForMood(Mood.Calm).Bpm);
    }

    [Fact]
    public void Drone_ForMood_RendersAudibly()
    {
        AudioBuffer bed = Drone.ForMood(Mood.Anticipation).Render(Context, 2);
        Assert.True(bed.RmsLevel.Linear > 0.01);
        Assert.True(bed.PeakLevel.Linear <= 1);
    }

    [Fact]
    public void Riser_GrowsTowardsItsEnd()
    {
        AudioBuffer riser = new Riser(2, 0.8).Render(Context, 2);
        double firstHalf = riser.FittedToDuration(1).RmsLevel.Linear;
        AudioBuffer secondHalf = AudioBuffer.FromSamples(riser.Samples[(riser.Length / 2)..], riser.SampleRate);
        Assert.True(secondHalf.RmsLevel.Linear > firstHalf * 2);
    }
}

using RP.Sound.Synthesis;

namespace RP.Sound.Tests;

public class TimelineTests
{
    private static readonly AudioRenderContext Context = new(SampleRate: 22050, Seed: 5);

    [Fact]
    public void Timeline_EndsWhenTheLastEventEnds()
    {
        var timeline = new Timeline(new (double, ISound)[]
        {
            (0.0, new Oscillator(Waveform.Sine, 440, 1)),
            (2.0, new Oscillator(Waveform.Sine, 440, 0.5)),
        });
        Assert.Equal(2.5, timeline.Duration, 6);
    }

    [Fact]
    public void Timeline_EmptyIsZeroLengthSilence()
    {
        var timeline = new Timeline(Array.Empty<(double, ISound)>());
        Assert.Equal(0, timeline.Duration);
        Assert.Equal(0, timeline.Render(Context, 1).RmsLevel.Linear);
    }

    [Fact]
    public void Timeline_PlacesEventsAtTheirStartTimes()
    {
        var timeline = new Timeline(new (double, ISound)[] { (0.5, new Oscillator(Waveform.Sine, 440, 0.25)) });
        AudioBuffer buffer = timeline.Render(Context);
        Assert.Equal(0, buffer.FittedToDuration(0.45).RmsLevel.Linear); // silence before the event
        Assert.True(AudioBuffer.FromSamples(buffer.Samples[(int)(0.5 * Context.SampleRate)..], Context.SampleRate).RmsLevel.Linear > 0.1);
    }

    [Fact]
    public void Timeline_MatchesTheEquivalentDelayedMix()
    {
        var a = new Oscillator(Waveform.Sine, 440, 1);
        var b = new Oscillator(Waveform.Square, 220, 0.5, Level.Half);
        AudioBuffer viaTimeline = new Timeline(new (double, ISound)[] { (0.0, a), (0.25, b) }).Render(Context, 1.5);
        AudioBuffer viaCombinators = a.MixedWith(b.Delayed(0.25)).Render(Context, 1.5);
        Assert.True(viaTimeline.Samples.SequenceEqual(viaCombinators.Samples));
    }

    [Fact]
    public void Timeline_RejectsNegativeStartTimes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Timeline(new (double, ISound)[] { (-1, new Oscillator(Waveform.Sine, 440, 1)) }));
    }
}

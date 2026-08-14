using RP.Sound.Effects;
using RP.Sound.Synthesis;

namespace RP.Sound.Tests;

public class EffectsTests
{
    private static readonly AudioRenderContext Context = new(SampleRate: 22050, Seed: 1);

    private static AudioBuffer Tone(double hertz) =>
        new Oscillator(Waveform.Sine, hertz, 0.5, new Level(0.5)).Render(Context);

    [Fact]
    public void LowPass_PassesLowAndAttenuatesHigh()
    {
        AudioBuffer low = Tone(100).LowPassed(1000);
        AudioBuffer high = Tone(8000).LowPassed(1000);
        Assert.True(low.RmsLevel.Linear > 0.25);          // essentially untouched
        Assert.True(high.RmsLevel.Linear < 0.05);          // well down
    }

    [Fact]
    public void HighPass_IsTheMirrorImage()
    {
        AudioBuffer low = Tone(100).HighPassed(1000);
        AudioBuffer high = Tone(8000).HighPassed(1000);
        Assert.True(high.RmsLevel.Linear > 0.25);
        Assert.True(low.RmsLevel.Linear < 0.05);
    }

    [Fact]
    public void BandPass_PrefersItsCentre()
    {
        AudioBuffer centre = Tone(1000).BandPassed(1000, 2);
        AudioBuffer off = Tone(4000).BandPassed(1000, 2);
        Assert.True(centre.RmsLevel.Linear > off.RmsLevel.Linear * 3);
    }

    [Fact]
    public void Echo_AddsARepeatAtTheDelay()
    {
        // A single-sample click echoes into a click train.
        var click = new float[2205];
        click[0] = 1;
        AudioBuffer echoed = new Echo(0.05, Level.Half, Level.Unity).Apply(AudioBuffer.FromSamples(click, 22050));
        int delaySamples = (int)(0.05 * 22050);
        Assert.Equal(Level.Half.Linear, echoed[delaySamples], 3);
    }

    [Fact]
    public void Echo_UnityFeedback_CannotBeConstructed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Echo(0.1, Level.Unity));
    }

    [Fact]
    public void Reverb_AddsATailBeyondTheDrySound()
    {
        AudioBuffer dry = Tone(500);
        AudioBuffer wet = Reverb.Hall.Apply(dry);
        Assert.True(wet.Length > dry.Length);

        AudioBuffer tail = AudioBuffer.FromSamples(wet.Samples[dry.Length..], wet.SampleRate);
        Assert.True(tail.RmsLevel.Linear > 1e-4); // the room keeps sounding after the source stops
    }

    [Fact]
    public void Distortion_KeepsUnityOutputCeiling()
    {
        AudioBuffer distorted = Tone(200).Distorted(drive: 10);
        Assert.True(distorted.PeakLevel.Linear <= 1.0 + 1e-6);
    }

    [Fact]
    public void PluckedString_RingsAtItsPitch()
    {
        // Count zero crossings: a 220 Hz string crosses zero ~440 times a second. The string is
        // deliberately harmonic-rich, so isolate the fundamental with a low-pass before counting,
        // and skip the first cycles, which are still raw pluck noise.
        AudioBuffer pluck = new PluckedString(220, 0.5).Render(Context).LowPassed(300);
        int start = (int)(0.2 * pluck.SampleRate);
        int crossings = 0;
        for (int i = start + 1; i < pluck.Length; i++)
        {
            if ((pluck[i - 1] < 0) != (pluck[i] < 0)) crossings++;
        }

        double measured = crossings / 2.0 / (pluck.Duration - 0.2);
        Assert.InRange(measured, 170, 280);
    }
}

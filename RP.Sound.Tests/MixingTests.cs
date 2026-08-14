using RP.Sound.Mixing;
using RP.Sound.Physics;
using RP.Sound.Synthesis;

namespace RP.Sound.Tests;

public class MixingTests
{
    private static readonly AudioRenderContext Context = new(SampleRate: 22050, Seed: 9);

    [Fact]
    public void EqualPowerPan_KeepsTotalPowerConstant()
    {
        AudioBuffer mono = new Oscillator(Waveform.Sine, 440, 0.2, new Level(0.5)).Render(Context);
        StereoBuffer centre = StereoBuffer.FromMono(mono, 0);
        StereoBuffer left = StereoBuffer.FromMono(mono, -1);

        static double Power(StereoBuffer s) =>
            s.Left.RmsLevel.Linear * s.Left.RmsLevel.Linear + s.Right.RmsLevel.Linear * s.Right.RmsLevel.Linear;

        Assert.Equal(Power(centre), Power(left), 6);
        Assert.True(left.Right.RmsLevel.Linear < 1e-9); // hard left leaves the right channel silent
    }

    [Fact]
    public void Placement_DistanceQuietensAndDarkens()
    {
        AudioBuffer bright = new Oscillator(Waveform.Sawtooth, 440, 0.3, new Level(0.5)).Render(Context);
        StereoBuffer near = new SoundPlacement(0, 1).Apply(bright);
        StereoBuffer far = new SoundPlacement(0, 40).Apply(bright);

        Assert.True(far.Left.RmsLevel.Linear < near.Left.RmsLevel.Linear / 10); // 1/d law

        static double HighShare(AudioBuffer b) =>
            Effects.Filter.HighPass(b, 3000).RmsLevel.Linear / System.Math.Max(1e-12, b.RmsLevel.Linear);
        Assert.True(HighShare(far.Left) < HighShare(near.Left)); // air absorption
    }

    [Fact]
    public void Mixer_DucksTheBedUnderAHigherPriorityEvent()
    {
        // A steady ambience bed, and a loud effect burst in the middle of the render.
        // Wood dies away in tens of milliseconds, so by the measurement window (1.35 s+) only the
        // ducking release — not the hit itself — is left in the mix.
        ISound bed = new Noise(NoiseColor.Pink, new Level(0.4), "bed");
        ISound burst = new Impact(new ModalBody(Material.Wood, 0.6), 6).Delayed(1.0);

        var withoutDucking = new Mixer(new MixLayer("bed", bed, MixRole.Ambience));
        var withDucking = new Mixer(
            new MixLayer("bed", bed, MixRole.Ambience),
            new MixLayer("hit", burst, MixRole.Effects));

        // Compare the bed's loudness in the ducked mix against the solo mix, in the window just
        // after the hit lands. The mixed version also contains the hit itself, so measure where
        // the hit has decayed but the release hasn't ended (1.3–1.6 s).
        static double WindowRms(StereoBuffer s, double from, double to)
        {
            int start = (int)(from * s.SampleRate);
            int end = (int)(to * s.SampleRate);
            double sum = 0;
            for (int i = start; i < end; i++) sum += (double)s.Left[i] * s.Left[i];
            return System.Math.Sqrt(sum / (end - start));
        }

        StereoBuffer solo = withoutDucking.Render(Context, 3);
        StereoBuffer ducked = withDucking.Render(Context, 3);

        double before = WindowRms(ducked, 0.2, 0.8) / WindowRms(solo, 0.2, 0.8);
        double after = WindowRms(ducked, 1.35, 1.6) / WindowRms(solo, 1.35, 1.6);
        Assert.True(after < before * 0.9, $"expected the bed to duck (before ratio {before:0.###}, after {after:0.###})");
    }

    [Fact]
    public void Mixer_IsImmutable_WithReturnsANewMixer()
    {
        var mixer = new Mixer(new MixLayer("a", Sounds.Silence(1), MixRole.Ambience));
        Mixer bigger = mixer.With(new MixLayer("b", Sounds.Silence(1), MixRole.Music));
        Assert.Single(mixer.Layers);
        Assert.Equal(2, bigger.Layers.Count);
    }

    [Fact]
    public void StereoBuffer_RefusesMismatchedChannels()
    {
        AudioBuffer a = AudioBuffer.Silence(1, 22050);
        AudioBuffer b = AudioBuffer.Silence(2, 22050);
        Assert.Throws<ArgumentException>(() => new StereoBuffer(a, b));
    }
}

using RP.Sound.Games;
using RP.Sound.Playback;
using RP.Sound.Synthesis;

namespace RP.Sound.Tests;

public class PlaybackTests
{
    private const int Rate = 44100;

    private static AudioBuffer Ramp(int length)
    {
        var samples = new float[length];
        for (int i = 0; i < length; i++) samples[i] = (float)i / length;
        return AudioBuffer.FromSamples(samples, Rate);
    }

    private static float[] Block(int frames) => new float[frames * 2];

    // ---- SampleVoice ----

    [Fact]
    public void Voice_AtUnityRate_ReproducesItsSource()
    {
        AudioBuffer source = Ramp(64);
        var voice = new SampleVoice();

        // Hard left, so the left channel carries the whole signal at the panning law's cos(0) = 1.
        voice.Start(source, pan: -1);
        float[] dry = Block(64), send = Block(64);
        voice.Render(dry, send, 64);

        for (int i = 0; i < 64; i++) Assert.Equal(source[i], dry[i * 2], 5);
    }

    [Fact]
    public void Voice_AtDoubleRate_FinishesInHalfTheFrames()
    {
        var voice = new SampleVoice();
        voice.Start(Ramp(64), rate: 2, pan: -1);

        float[] dry = Block(64), send = Block(64);
        voice.Render(dry, send, 64);

        Assert.False(voice.Active);
        for (int i = 32; i < 64; i++) Assert.Equal(0, dry[i * 2], 6);
    }

    [Fact]
    public void Voice_Panning_KeepsConstantPower()
    {
        // The two gains are cos and sin of one angle, so their squares sum to 1 wherever the sound
        // sits. A flat sample makes that directly measurable.
        AudioBuffer flat = AudioBuffer.FromSamples(new float[] { 1, 1, 1, 1 }, Rate);

        foreach (double pan in new[] { -1, -0.5, 0, 0.5, 1.0 })
        {
            var voice = new SampleVoice();
            voice.Start(flat, pan: pan);
            float[] dry = Block(4), send = Block(4);
            voice.Render(dry, send, 4);

            Assert.Equal(1.0, dry[0] * dry[0] + dry[1] * dry[1], 5);
        }
    }

    [Fact]
    public void Voice_Looping_NeverRunsOut()
    {
        var voice = new SampleVoice();
        voice.Start(Ramp(16), looping: true);

        float[] dry = Block(256), send = Block(256);
        voice.Render(dry, send, 256);
        Assert.True(voice.Active);
    }

    [Fact]
    public void Voice_Send_FeedsTheBusInProportion()
    {
        var voice = new SampleVoice();
        voice.Start(AudioBuffer.FromSamples(new float[] { 1, 1 }, Rate), pan: -1, send: 0.5);

        float[] dry = Block(2), send = Block(2);
        voice.Render(dry, send, 2);
        Assert.Equal(dry[0] * 0.5f, send[0], 5);
    }

    [Fact]
    public void Voice_GainChanges_SlideAcrossTheBlockRatherThanStepping()
    {
        // A bed whose level jumps at a block boundary is a step discontinuity in the waveform, and
        // steps are heard as clicks. The gain has to walk to its new value across the block.
        AudioBuffer flat = AudioBuffer.FromSamples(Enumerable.Repeat(1f, 128).ToArray(), Rate);
        var voice = new SampleVoice();
        voice.Start(flat, gain: Level.Silence, pan: -1, looping: true);

        voice.Adjust(1, Level.Unity, -1);
        float[] dry = Block(64), send = Block(64);
        voice.Render(dry, send, 64);

        Assert.True(dry[0] < 0.05f, "it should start from where it was");
        Assert.True(dry[63 * 2] > 0.9f, "and reach the new level by the end of the block");
        for (int i = 1; i < 64; i++)
            Assert.True(System.Math.Abs(dry[i * 2] - dry[(i - 1) * 2]) < 0.05f, "with no step along the way");
    }

    [Fact]
    public void Voice_RejectsImpossibleParameters()
    {
        var voice = new SampleVoice();
        Assert.Throws<ArgumentOutOfRangeException>(() => voice.Start(Ramp(4), rate: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => voice.Start(Ramp(4), pan: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => voice.Start(Ramp(4), send: -1));
    }

    // ---- SampleVoiceMixer ----

    [Fact]
    public void Mixer_RefusesToPlayOnceThePoolIsFull()
    {
        var mixer = new SampleVoiceMixer(Rate, maxFrames: 64, maxVoices: 2);
        AudioBuffer clip = Ramp(4096);

        Assert.True(mixer.Play(clip));
        Assert.True(mixer.Play(clip));
        Assert.False(mixer.Play(clip));
    }

    [Fact]
    public void Mixer_FreesVoicesAsTheyFinish()
    {
        var mixer = new SampleVoiceMixer(Rate, maxFrames: 64, maxVoices: 1);
        Assert.True(mixer.Play(Ramp(32)));
        Assert.False(mixer.Play(Ramp(32)));

        mixer.Fill(Block(64), 64);          // the first clip runs out inside this block
        Assert.True(mixer.Play(Ramp(32)));
    }

    [Fact]
    public void Mixer_RefusesABufferAtTheWrongSampleRate()
    {
        var mixer = new SampleVoiceMixer(Rate, maxFrames: 64);
        Assert.Throws<ArgumentException>(() => mixer.Play(AudioBuffer.Silence(0.1, 22050)));
    }

    [Fact]
    public void Mixer_DelayReturnsTheSoundLater()
    {
        var mixer = new SampleVoiceMixer(Rate, maxFrames: 1024) { Volume = Level.Unity };

        // A single click, sent hard to the bus. Nothing should come back before the shorter tap.
        var click = new float[1];
        click[0] = 1;
        mixer.Play(AudioBuffer.FromSamples(click, Rate), gain: Level.Unity, pan: -1, send: 1);

        int tap = (int)(Rate * 0.227);
        float beforeTap = 0, atTap = 0;
        for (int block = 0; block * 1024 < tap + 2048; block++)
        {
            float[] output = Block(1024);
            mixer.Fill(output, 1024);
            for (int i = 0; i < 1024; i++)
            {
                int frame = block * 1024 + i;
                float value = System.Math.Abs(output[i * 2]);
                if (frame > 1 && frame < tap - 1) beforeTap = System.Math.Max(beforeTap, value);
                if (frame >= tap - 1 && frame <= tap + 1) atTap = System.Math.Max(atTap, value);
            }
        }

        Assert.True(beforeTap < 1e-6, "nothing should return before the first tap");
        Assert.True(atTap > 0.01, "the repeat should arrive at the tap");
    }

    [Fact]
    public void Mixer_SaturatesRatherThanClipping()
    {
        var mixer = new SampleVoiceMixer(Rate, maxFrames: 64, maxVoices: 8) { Volume = Level.Unity };
        AudioBuffer loud = AudioBuffer.FromSamples(Enumerable.Repeat(1f, 64).ToArray(), Rate);
        for (int i = 0; i < 8; i++) mixer.Play(loud, gain: Level.Unity);

        float[] output = Block(64);
        mixer.Fill(output, 64);

        foreach (float sample in output) Assert.InRange(sample, -1f, 1f);
    }

    [Fact]
    public void Mixer_Bed_FadesInRatherThanJumping()
    {
        var mixer = new SampleVoiceMixer(Rate, maxFrames: 1024) { Volume = Level.Unity, DelayMix = 0 };
        mixer.SetBed(SciFi.Drone(55).Render(new AudioRenderContext(Rate), 2.0), level: Level.Unity);

        float[] first = Block(1024);
        mixer.Fill(first, 1024);
        float firstPeak = Peak(first);

        float lastPeak = 0;
        for (int i = 0; i < 400; i++)
        {
            float[] later = Block(1024);
            mixer.Fill(later, 1024);
            lastPeak = Peak(later);
        }

        Assert.True(firstPeak < lastPeak / 3, "the bed should arrive gradually, not at full level in the first block");
        Assert.True(lastPeak > 0.1f, "and should be properly audible once it has settled");

        static float Peak(float[] block)
        {
            float peak = 0;
            foreach (float s in block) peak = System.Math.Max(peak, System.Math.Abs(s));
            return peak;
        }
    }

    [Fact]
    public void Mixer_StopAll_LeavesNothingRinging()
    {
        var mixer = new SampleVoiceMixer(Rate, maxFrames: 1024) { Volume = Level.Unity };
        mixer.Play(Ramp(4096), send: 1);
        mixer.Fill(Block(1024), 1024);

        mixer.StopAll();
        float[] output = Block(1024);
        mixer.Fill(output, 1024);
        foreach (float sample in output) Assert.Equal(0, sample, 6);
    }

    [Fact]
    public void Mixer_FillAllocatesNothing()
    {
        // The reason voices are pooled: a collection while the device waits for samples is audible
        // as a dropout. This is the test that keeps that property from quietly regressing.
        var mixer = new SampleVoiceMixer(Rate, maxFrames: 1024);
        AudioBuffer clip = Ramp(44100);
        mixer.SetBed(clip, level: Level.Half);
        for (int i = 0; i < 8; i++) mixer.Play(clip, rate: 1.5, send: 0.5);

        float[] output = Block(1024);
        mixer.Fill(output, 1024);                       // let any first-call warm-up happen

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 50; i++) mixer.Fill(output, 1024);
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    [Fact]
    public void Mixer_RejectsABlockLargerThanItWasBuiltFor()
    {
        var mixer = new SampleVoiceMixer(Rate, maxFrames: 256);
        Assert.Throws<ArgumentOutOfRangeException>(() => mixer.Fill(Block(512), 512));
    }
}

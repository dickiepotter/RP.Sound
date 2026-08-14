namespace RP.Sound.Tests;

public class AudioBufferTests
{
    private static AudioBuffer Buffer(params float[] samples) => AudioBuffer.FromSamples(samples, 4);

    [Fact]
    public void Mix_SumsAndTakesTheLongestLength()
    {
        AudioBuffer mixed = AudioBuffer.Mix(Buffer(1, 1), Buffer(0.5f, 0.5f, 0.5f));
        Assert.Equal(3, mixed.Length);
        Assert.Equal(1.5f, mixed[0]);
        Assert.Equal(0.5f, mixed[2]);
    }

    [Fact]
    public void Mix_RefusesDifferentSampleRates()
    {
        AudioBuffer a = AudioBuffer.FromSamples(new float[4], 44100);
        AudioBuffer b = AudioBuffer.FromSamples(new float[4], 22050);
        Assert.Throws<ArgumentException>(() => AudioBuffer.Mix(a, b));
    }

    [Fact]
    public void Concat_JoinsEndToEnd()
    {
        AudioBuffer joined = Buffer(1, 2).Then(Buffer(3));
        Assert.Equal(new float[] { 1, 2, 3 }, joined.Samples.ToArray());
    }

    [Fact]
    public void MixedAt_PlacesTheOtherBufferAtTheOffset()
    {
        AudioBuffer result = Buffer(1, 1, 1, 1).MixedAt(Buffer(2), 0.5); // 0.5 s at 4 Hz = 2 samples
        Assert.Equal(new float[] { 1, 1, 3, 1 }, result.Samples.ToArray());
    }

    [Fact]
    public void FitToDuration_PadsWithSilenceAndCuts()
    {
        Assert.Equal(4, Buffer(1, 2).FittedToDuration(1).Length);
        Assert.Equal(0, Buffer(1, 2).FittedToDuration(1)[3]);
        Assert.Equal(1, Buffer(1, 2, 3, 4).FittedToDuration(0.25).Length);
    }

    [Fact]
    public void Normalized_ScalesThePeakToTarget()
    {
        AudioBuffer normalized = Buffer(0.25f, -0.5f).Normalized();
        Assert.Equal(1, normalized.PeakLevel.Linear, 6);
    }

    [Fact]
    public void Normalized_ThrowsOnSilence_ButOrDefaultDoesNot()
    {
        AudioBuffer silence = Buffer(0, 0);
        Assert.Throws<NormalizeSilentBufferException>(() => silence.Normalized());
        Assert.Same(silence, silence.NormalizedOrDefault());
    }

    [Fact]
    public void RmsLevel_OfAConstantSignal_IsThatValue()
    {
        Assert.Equal(0.5, Buffer(0.5f, -0.5f, 0.5f, -0.5f).RmsLevel.Linear, 6);
    }

    [Fact]
    public void SoftClipped_KeepsSamplesInRange()
    {
        AudioBuffer clipped = Buffer(3f, -3f, 0.5f).SoftClipped();
        Assert.InRange(clipped[0], 0.8f, 1f);
        Assert.InRange(clipped[1], -1f, -0.8f);
        Assert.Equal(0.5f, clipped[2]); // below the knee, untouched
    }
}

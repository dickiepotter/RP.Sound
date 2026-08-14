using RP.Sound.IO;
using RP.Sound.Mixing;

namespace RP.Sound.Tests;

public class WavFileTests
{
    [Fact]
    public void MonoWav_HasACorrectHeader()
    {
        AudioBuffer buffer = AudioBuffer.FromSamples(new float[] { 0, 0.5f, -0.5f, 1 }, 44100);
        byte[] wav = WavFile.ToBytes(buffer);

        Assert.Equal((byte)'R', wav[0]);
        Assert.Equal((byte)'W', wav[8]);                        // "WAVE"
        Assert.Equal(44 + 4 * 2, wav.Length);                    // header + 4 samples × 16-bit
        Assert.Equal(1, BitConverter.ToInt16(wav, 22));          // mono
        Assert.Equal(44100, BitConverter.ToInt32(wav, 24));      // sample rate
        Assert.Equal(16, BitConverter.ToInt16(wav, 34));         // bits per sample
    }

    [Fact]
    public void FullScaleSample_QuantisesToShortMax()
    {
        byte[] wav = WavFile.ToBytes(AudioBuffer.FromSamples(new float[] { 1 }, 44100));
        Assert.Equal(short.MaxValue, BitConverter.ToInt16(wav, 44));
    }

    [Fact]
    public void OverRangeSamples_ClampInsteadOfWrapping()
    {
        byte[] wav = WavFile.ToBytes(AudioBuffer.FromSamples(new float[] { 5f, -5f }, 44100));
        Assert.Equal(short.MaxValue, BitConverter.ToInt16(wav, 44));
        Assert.Equal(-short.MaxValue, BitConverter.ToInt16(wav, 46));
    }

    [Fact]
    public void StereoWav_InterleavesChannels()
    {
        var stereo = new StereoBuffer(
            AudioBuffer.FromSamples(new float[] { 1, 1 }, 44100),
            AudioBuffer.FromSamples(new float[] { 0, 0 }, 44100));
        byte[] wav = WavFile.ToBytes(stereo);

        Assert.Equal(2, BitConverter.ToInt16(wav, 22));          // stereo
        Assert.Equal(short.MaxValue, BitConverter.ToInt16(wav, 44)); // L
        Assert.Equal(0, BitConverter.ToInt16(wav, 46));              // R
    }
}

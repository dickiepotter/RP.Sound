namespace RP.Sound.IO;

using RP.Sound.Mixing;

/// <summary>
/// Encodes buffers as standard 16-bit PCM WAV — the format every browser, editor and engine
/// accepts without question. Samples are clamped into [−1, 1] before quantising, so an over-hot
/// buffer clips rather than wraps into garbage.
/// </summary>
public static class WavFile
{
    public static byte[] ToBytes(AudioBuffer buffer) => Encode(buffer.SampleRate, 1, Interleave(buffer));

    public static byte[] ToBytes(StereoBuffer buffer) => Encode(buffer.SampleRate, 2, Interleave(buffer.Left, buffer.Right));

    public static void Save(AudioBuffer buffer, string path) => File.WriteAllBytes(path, ToBytes(buffer));

    public static void Save(StereoBuffer buffer, string path) => File.WriteAllBytes(path, ToBytes(buffer));

    private static short[] Interleave(params AudioBuffer[] channels)
    {
        int length = channels[0].Length;
        var samples = new short[length * channels.Length];
        for (int i = 0; i < length; i++)
        {
            for (int c = 0; c < channels.Length; c++)
            {
                double clamped = System.Math.Clamp(channels[c][i], -1f, 1f);
                samples[i * channels.Length + c] = (short)System.Math.Round(clamped * short.MaxValue);
            }
        }

        return samples;
    }

    private static byte[] Encode(int sampleRate, short channelCount, short[] samples)
    {
        const short bitsPerSample = 16;
        int dataBytes = samples.Length * 2;
        int byteRate = sampleRate * channelCount * bitsPerSample / 8;

        using var stream = new MemoryStream(44 + dataBytes);
        using var writer = new BinaryWriter(stream);

        // The RIFF/WAVE container: a 12-byte header, an "fmt " chunk describing the samples,
        // then the "data" chunk holding them.
        writer.Write("RIFF"u8);
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8);

        writer.Write("fmt "u8);
        writer.Write(16);                     // fmt chunk size
        writer.Write((short)1);               // PCM
        writer.Write(channelCount);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)(channelCount * bitsPerSample / 8)); // block align
        writer.Write(bitsPerSample);

        writer.Write("data"u8);
        writer.Write(dataBytes);
        foreach (short sample in samples) writer.Write(sample);

        return stream.ToArray();
    }
}

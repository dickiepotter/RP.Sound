using System.Text;

namespace RP.Sound.IO;

/// <summary>
/// Reads and writes ProTracker modules (.mod) — the Amiga tracker format (Ultimate Soundtracker,
/// Karsten Obarski 1987; standardised as the 31-sample tagged layout by ProTracker, 1990). The
/// file is a single fixed-layout blob: a 20-byte title, 31 sample headers, the order list, a
/// 4-byte tag identifying the variant, the pattern data, then the raw signed 8-bit sample
/// recordings back to back.
/// <para>
/// Supported: the tagged 31-sample variants — "M.K." / "M!K!" / "FLT4" (4 channels), "6CHN"
/// (6), "8CHN" / "FLT8" / "OCTA" (8). Deliberately rejected: untagged 15-sample Ultimate
/// Soundtracker files (no signature means detection is guesswork) — see "Future considerations"
/// in the README. Reading is strict about structure but forgiving about the two defects almost
/// every surviving module has: sample data truncated a few bytes short (padded with silence) and
/// loop points overrunning their sample (clamped).
/// </para>
/// </summary>
public static class ModFile
{
    private const int TitleLength = 20;
    private const int SampleNameLength = 22;
    private const int SampleHeaderLength = 30;
    private const int OrderCapacity = 128;
    private const int HeaderLength = TitleLength + ModModule.SampleSlots * SampleHeaderLength + 2 + OrderCapacity + 4; // = 1084

    public static ModModule Load(string path) => Read(File.ReadAllBytes(path));

    public static void Save(ModModule module, string path) => File.WriteAllBytes(path, ToBytes(module));

    /// <summary>Parses a tagged 31-sample module.</summary>
    public static ModModule Read(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length < HeaderLength)
            throw new FormatException($"A tagged module is at least {HeaderLength} bytes of header; this is {bytes.Length}.");

        int channelCount = ChannelsFromTag(bytes.AsSpan(1080, 4));
        string title = ReadString(bytes, 0, TitleLength);

        // Sample headers first; their data waits at the end of the file.
        var headers = new (string Name, int LengthBytes, int Finetune, int Volume, int LoopStartBytes, int LoopLengthBytes)[ModModule.SampleSlots];
        for (int i = 0; i < headers.Length; i++)
        {
            int offset = TitleLength + i * SampleHeaderLength;
            headers[i] = (
                Name: ReadString(bytes, offset, SampleNameLength),
                LengthBytes: ReadWord(bytes, offset + 22) * 2,
                Finetune: ((bytes[offset + 24] & 0x0F) ^ 8) - 8, // A signed nibble: 0–7 positive, 8–15 are −8…−1.
                Volume: System.Math.Min(bytes[offset + 25], (byte)64),
                LoopStartBytes: ReadWord(bytes, offset + 26) * 2,
                LoopLengthBytes: ReadWord(bytes, offset + 28) * 2);
        }

        int songLength = bytes[950];
        if (songLength is < 1 or > OrderCapacity)
            throw new FormatException($"The order list claims {songLength} positions; the format allows 1–128.");
        // Byte 951 is the restart position — rarely meaningful (127 in most files) and ignored here.

        var order = new int[songLength];
        int patternCount = 0;
        for (int i = 0; i < OrderCapacity; i++)
        {
            int position = bytes[952 + i];
            if (i < songLength) order[i] = position;
            // The file never states its pattern count; the convention (documented in the
            // community's MOD format specs) is one more than the highest position named anywhere
            // in the full 128-entry table, played or not.
            patternCount = System.Math.Max(patternCount, position + 1);
        }

        int patternBytes = ModPattern.Rows * channelCount * 4;
        if (HeaderLength + patternCount * patternBytes > bytes.Length)
            throw new FormatException($"The order table names {patternCount} patterns but the file only holds {(bytes.Length - HeaderLength) / patternBytes}.");

        var patterns = new ModPattern[patternCount];
        for (int p = 0; p < patternCount; p++)
        {
            var cells = new ModCell[ModPattern.Rows, channelCount];
            int patternOffset = HeaderLength + p * patternBytes;
            for (int row = 0; row < ModPattern.Rows; row++)
            {
                for (int channel = 0; channel < channelCount; channel++)
                {
                    int o = patternOffset + (row * channelCount + channel) * 4;
                    // Four bytes per cell: sample number split across the top nibbles of bytes 0
                    // and 2, a 12-bit period, an effect nibble and its argument byte.
                    cells[row, channel] = new ModCell(
                        SampleNumber: (bytes[o] & 0xF0) | (bytes[o + 2] >> 4),
                        Period: ((bytes[o] & 0x0F) << 8) | bytes[o + 1],
                        Effect: bytes[o + 2] & 0x0F,
                        Argument: bytes[o + 3]);
                }
            }

            patterns[p] = new ModPattern(cells);
        }

        var samples = new ModSample[ModModule.SampleSlots];
        int dataOffset = HeaderLength + patternCount * patternBytes;
        for (int i = 0; i < samples.Length; i++)
        {
            (string name, int length, int finetune, int volume, int loopStart, int loopLength) = headers[i];

            // Forgiveness clause 1: countless real modules are truncated a few bytes short in
            // their final sample; pad the missing tail with silence rather than reject the file.
            var data = new sbyte[length];
            int available = System.Math.Clamp(bytes.Length - dataOffset, 0, length);
            for (int b = 0; b < available; b++) data[b] = unchecked((sbyte)bytes[dataOffset + b]);
            dataOffset += length;

            // Forgiveness clause 2: loop points that overrun the sample are clamped to fit.
            loopStart = System.Math.Min(loopStart, length);
            if (loopLength > 2) loopLength = System.Math.Min(loopLength, length - loopStart) & ~1;

            samples[i] = new ModSample(name, data, finetune, volume, loopStart, loopLength);
        }

        return new ModModule(title, samples, patterns, order, channelCount);
    }

    /// <summary>
    /// Encodes the module in the tagged layout: "M.K." for 4 channels ("M!K!" past 64 patterns),
    /// "6CHN"/"8CHN" otherwise. The format has no pattern count field — readers infer it from the
    /// highest position in the order table — so every pattern must be reachable from the order
    /// list, and unreferenced trailing patterns are an error rather than silent loss.
    /// </summary>
    public static byte[] ToBytes(ModModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (module.Order.Max() + 1 != module.Patterns.Count)
            throw new ArgumentException(
                $"The order list reaches pattern {module.Order.Max()} but the module holds {module.Patterns.Count} patterns; " +
                "a .mod file cannot represent unreferenced trailing patterns because readers infer the count from the order table.",
                nameof(module));

        int patternBytes = ModPattern.Rows * module.ChannelCount * 4;
        var bytes = new byte[HeaderLength + module.Patterns.Count * patternBytes + module.Samples.Sum(s => s.Length)];

        WriteString(bytes, 0, TitleLength, module.Title);
        for (int i = 0; i < ModModule.SampleSlots; i++)
        {
            ModSample sample = module.Samples[i];
            int offset = TitleLength + i * SampleHeaderLength;
            WriteString(bytes, offset, SampleNameLength, sample.Name);
            WriteWord(bytes, offset + 22, sample.Length / 2);
            bytes[offset + 24] = (byte)(sample.Finetune & 0x0F);
            bytes[offset + 25] = (byte)sample.Volume;
            WriteWord(bytes, offset + 26, sample.LoopStart / 2);
            WriteWord(bytes, offset + 28, System.Math.Max(sample.LoopLength, 2) / 2); // 1 word is the format's "no loop".
        }

        bytes[950] = (byte)module.Order.Count;
        bytes[951] = 127; // Restart position: the "none" convention.
        for (int i = 0; i < module.Order.Count; i++) bytes[952 + i] = (byte)module.Order[i];

        string tag = module.ChannelCount switch
        {
            4 => module.Patterns.Count > 64 ? "M!K!" : "M.K.",
            6 => "6CHN",
            _ => "8CHN",
        };
        Encoding.ASCII.GetBytes(tag, bytes.AsSpan(1080, 4));

        for (int p = 0; p < module.Patterns.Count; p++)
        {
            ModPattern pattern = module.Patterns[p];
            int patternOffset = HeaderLength + p * patternBytes;
            for (int row = 0; row < ModPattern.Rows; row++)
            {
                for (int channel = 0; channel < module.ChannelCount; channel++)
                {
                    ModCell cell = pattern[row, channel];
                    int o = patternOffset + (row * module.ChannelCount + channel) * 4;
                    bytes[o] = (byte)((cell.SampleNumber & 0xF0) | (cell.Period >> 8));
                    bytes[o + 1] = (byte)cell.Period;
                    bytes[o + 2] = (byte)(((cell.SampleNumber & 0x0F) << 4) | cell.Effect);
                    bytes[o + 3] = (byte)cell.Argument;
                }
            }
        }

        int dataOffset = HeaderLength + module.Patterns.Count * patternBytes;
        foreach (ModSample sample in module.Samples)
        {
            for (int b = 0; b < sample.Length; b++) bytes[dataOffset + b] = unchecked((byte)sample.Data[b]);
            dataOffset += sample.Length;
        }

        return bytes;
    }

    private static int ChannelsFromTag(ReadOnlySpan<byte> tag) => Encoding.ASCII.GetString(tag) switch
    {
        "M.K." or "M!K!" or "FLT4" => 4,
        "6CHN" => 6,
        "8CHN" or "FLT8" or "OCTA" => 8,
        var other => throw new FormatException(
            $"Unrecognised module tag \"{other}\". Tagged 31-sample variants (M.K., M!K!, FLT4, 6CHN, 8CHN, FLT8, OCTA) are supported; " +
            "an untagged file is probably a 15-sample Ultimate Soundtracker module, which is not."),
    };

    private static string ReadString(byte[] bytes, int offset, int length)
    {
        int end = offset;
        while (end < offset + length && bytes[end] != 0) end++;
        return Encoding.ASCII.GetString(bytes, offset, end - offset).TrimEnd();
    }

    private static void WriteString(byte[] bytes, int offset, int length, string value)
    {
        for (int i = 0; i < value.Length && i < length; i++)
            bytes[offset + i] = value[i] < 128 ? (byte)value[i] : (byte)'?';
    }

    private static int ReadWord(byte[] bytes, int offset) => (bytes[offset] << 8) | bytes[offset + 1];

    private static void WriteWord(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)(value >> 8);
        bytes[offset + 1] = (byte)value;
    }
}

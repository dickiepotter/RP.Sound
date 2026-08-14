using RP.Sound.IO;
using RP.Sound.Music;

namespace RP.Sound.Tests;

public class ModFileTests
{
    private static readonly AudioRenderContext Context = new(SampleRate: 22050, Seed: 42);

    /// <summary>A 4-byte looping square wave: the smallest sample that sustains a pitch.</summary>
    private static ModSample SquareSample(int volume = 64) =>
        new("square", new sbyte[] { 100, 100, -100, -100 }, volume: volume, loopStart: 0, loopLength: 4);

    private static ModModule OneCellModule(ModCell cell, ModSample? sample = null)
    {
        var cells = new ModCell[ModPattern.Rows, 4];
        cells[0, 0] = cell;
        return new ModModule("test", new[] { sample ?? SquareSample() }, new[] { new ModPattern(cells) }, new[] { 0 });
    }

    [Fact]
    public void RoundTrip_PreservesTheModule()
    {
        var cells = new ModCell[ModPattern.Rows, 4];
        cells[0, 0] = ModCell.Create(sampleNumber: 1, period: 428);
        cells[4, 1] = ModCell.Create(sampleNumber: 17, period: 214, effect: 0xC, argument: 32); // sample number > 15 exercises the split nibble
        cells[63, 3] = ModCell.Create(effect: 0xF, argument: 3);

        var original = new ModModule(
            "round trip",
            new[] { new ModSample("lead", new sbyte[] { 1, 2, 3, 4, 5, 6 }, finetune: -3, volume: 48, loopStart: 2, loopLength: 4) },
            new[] { new ModPattern(cells), ModPattern.Silent() },
            new[] { 0, 1, 0 });

        ModModule decoded = ModFile.Read(ModFile.ToBytes(original));

        Assert.Equal(original.Title, decoded.Title);
        Assert.Equal(original.ChannelCount, decoded.ChannelCount);
        Assert.Equal(original.Order, decoded.Order);
        Assert.Equal(original.Patterns.Count, decoded.Patterns.Count);
        for (int row = 0; row < ModPattern.Rows; row++)
            for (int channel = 0; channel < 4; channel++)
                Assert.Equal(original.Patterns[0][row, channel], decoded.Patterns[0][row, channel]);

        ModSample a = original.Samples[0], b = decoded.Samples[0];
        Assert.Equal(a.Name, b.Name);
        Assert.Equal(a.Data.ToArray(), b.Data.ToArray());
        Assert.Equal(a.Finetune, b.Finetune);
        Assert.Equal(a.Volume, b.Volume);
        Assert.Equal(a.LoopStart, b.LoopStart);
        Assert.Equal(a.LoopLength, b.LoopLength);
        Assert.Equal(a.IsLooped, b.IsLooped);
    }

    [Fact]
    public void WrittenFile_CarriesTheClassicTag()
    {
        byte[] bytes = ModFile.ToBytes(OneCellModule(ModCell.Create(sampleNumber: 1, period: 428)));
        Assert.Equal("M.K."u8.ToArray(), bytes[1080..1084]);
    }

    [Fact]
    public void UnknownTag_IsRejectedWithAHelpfulError()
    {
        byte[] bytes = ModFile.ToBytes(OneCellModule(ModCell.Create(period: 428)));
        "????"u8.CopyTo(bytes.AsSpan(1080, 4));
        var error = Assert.Throws<FormatException>(() => ModFile.Read(bytes));
        Assert.Contains("Soundtracker", error.Message);
    }

    [Fact]
    public void TruncatedSampleData_IsPaddedWithSilenceNotRejected()
    {
        byte[] bytes = ModFile.ToBytes(OneCellModule(ModCell.Create(sampleNumber: 1, period: 428)));
        ModModule decoded = ModFile.Read(bytes[..^2]); // lose the last two sample bytes
        Assert.Equal(4, decoded.Samples[0].Length);
        Assert.Equal(0, decoded.Samples[0].Data[3]);
    }

    [Fact]
    public void UnreferencedTrailingPatterns_CannotBeWritten()
    {
        var module = new ModModule("gap", new[] { SquareSample() }, new[] { ModPattern.Silent(), ModPattern.Silent() }, new[] { 0 });
        Assert.Throws<ArgumentException>(() => ModFile.ToBytes(module));
    }

    [Fact]
    public void Model_ValidatesItsRanges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModSample("odd", new sbyte[] { 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModSample("loud", new sbyte[] { 1, 2 }, volume: 65));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModSample("tuning", new sbyte[] { 1, 2 }, finetune: 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => ModCell.Create(sampleNumber: 32));
        Assert.Throws<ArgumentOutOfRangeException>(() => ModCell.Create(period: 0x1000));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModPattern(new ModCell[32, 4]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModModule("ch", new[] { SquareSample() }, new[] { ModPattern.Silent() }, new[] { 0 }, channelCount: 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModModule("order", new[] { SquareSample() }, new[] { ModPattern.Silent() }, new[] { 1 }));
    }

    // ---- The replayer ----

    [Fact]
    public void ModSong_PlaysAPeriodAtThePaulaRate()
    {
        // Period 428 (middle C): Paula replays sample bytes at 7093789.2 / (2 × 428) ≈ 8287 Hz.
        // The 4-byte square loop then sounds at ≈ 2072 Hz — count zero crossings to verify.
        var song = new ModSong(OneCellModule(ModCell.Create(sampleNumber: 1, period: 428)));
        AudioBuffer audio = song.Render(new AudioRenderContext(SampleRate: 44100), 0.5);

        int crossings = 0;
        for (int i = 1; i < audio.Length; i++)
            if (audio[i - 1] < 0 != audio[i] < 0) crossings++;

        double measuredHz = crossings / 2.0 / audio.Duration;
        double expectedHz = ModSong.PaulaClockPal / (2.0 * 428) / 4;
        Assert.InRange(measuredHz, expectedHz * 0.98, expectedHz * 1.02);
    }

    [Fact]
    public void ModSong_DefaultTiming_Is6TicksPerRowAt125Bpm()
    {
        // 64 rows × 6 ticks × 0.02 s per tick = 7.68 s for one pattern.
        var song = new ModSong(OneCellModule(ModCell.Create(sampleNumber: 1, period: 428)));
        Assert.Equal(7.68, song.LoopDuration, 3);
        Assert.False(song.Loops);
        Assert.Equal(song.LoopDuration, song.Duration);
    }

    [Fact]
    public void SetSpeed_ChangesRowLength()
    {
        // F03 on the first row: every remaining row takes 3 ticks instead of 6.
        var cells = new ModCell[ModPattern.Rows, 4];
        cells[0, 0] = ModCell.Create(sampleNumber: 1, period: 428, effect: 0xF, argument: 3);
        var song = new ModSong(new ModModule("speed", new[] { SquareSample() }, new[] { new ModPattern(cells) }, new[] { 0 }));
        Assert.Equal(64 * 3 * 0.02, song.LoopDuration, 3);
    }

    [Fact]
    public void PatternBreak_SkipsTheRestOfThePattern()
    {
        // D00 on row 0 of the only pattern: position advances past the order list — one row long.
        var song = new ModSong(OneCellModule(ModCell.Create(sampleNumber: 1, period: 428, effect: 0xD, argument: 0)));
        Assert.Equal(6 * 0.02, song.LoopDuration, 3);
    }

    [Fact]
    public void PositionJumpBackwards_MakesTheSongLoopForever()
    {
        var song = new ModSong(OneCellModule(ModCell.Create(sampleNumber: 1, period: 428, effect: 0xB, argument: 0)));
        Assert.True(song.Loops);
        Assert.Equal(double.PositiveInfinity, song.Duration);
        Assert.Equal(6 * 0.02, song.LoopDuration, 3); // one row before it jumps back
        Assert.Equal(22050, song.Render(Context, 1).Length); // and it happily fills any duration asked for
    }

    [Fact]
    public void SetVolume_ScalesTheChannel()
    {
        AudioBuffer loud = new ModSong(OneCellModule(ModCell.Create(sampleNumber: 1, period: 428, effect: 0xC, argument: 64))).Render(Context, 0.3);
        AudioBuffer half = new ModSong(OneCellModule(ModCell.Create(sampleNumber: 1, period: 428, effect: 0xC, argument: 32))).Render(Context, 0.3);
        Assert.InRange(half.PeakLevel.Linear / loud.PeakLevel.Linear, 0.45, 0.55);
    }

    [Fact]
    public void UnloopedSample_PlaysOnceAndStops()
    {
        var oneShot = new ModSample("blip", new sbyte[] { 100, 100, 100, 100 }); // no loop: 4 bytes at ~8287 Hz ≈ 0.48 ms
        var song = new ModSong(OneCellModule(ModCell.Create(sampleNumber: 1, period: 428), oneShot));
        AudioBuffer audio = song.Render(Context, 0.5);

        int lastSounding = -1;
        for (int i = 0; i < audio.Length; i++)
            if (System.Math.Abs(audio[i]) > 0.001f) lastSounding = i;
        Assert.InRange(lastSounding, 0, (int)(0.01 * audio.SampleRate));
    }

    [Fact]
    public void Arpeggio_RaisesPitchOnAlternatingTicks()
    {
        // 047: root, +4, +7 semitones cycling per tick — a major chord from one channel. The
        // average pitch over a row must sit above the root's.
        AudioBuffer plain = new ModSong(OneCellModule(ModCell.Create(sampleNumber: 1, period: 428))).Render(Context, 0.12);
        AudioBuffer arpeggiated = new ModSong(OneCellModule(ModCell.Create(sampleNumber: 1, period: 428, effect: 0x0, argument: 0x47))).Render(Context, 0.12);

        static int Crossings(AudioBuffer b)
        {
            int n = 0;
            for (int i = 1; i < b.Length; i++)
                if (b[i - 1] < 0 != b[i] < 0) n++;
            return n;
        }

        Assert.True(Crossings(arpeggiated) > Crossings(plain) * 1.05);
    }

    [Fact]
    public void ModSong_RendersDeterministically()
    {
        var song = new ModSong(OneCellModule(ModCell.Create(sampleNumber: 1, period: 428, effect: 0x4, argument: 0x63)));
        Assert.Equal(
            song.Render(Context, 0.5).Samples.ToArray(),
            song.Render(Context, 0.5).Samples.ToArray());
    }
}

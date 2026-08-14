using RP.Sound.IO;
using RP.Sound.Music;

namespace RP.Sound.Tests;

public class MidiFileTests
{
    private static readonly AudioRenderContext Context = new(SampleRate: 22050, Seed: 42);

    [Fact]
    public void RoundTrip_PreservesEveryNoteField()
    {
        var original = new MidiSequence(new[]
        {
            new MidiNote(0.0, 0.5, 60, velocity: 100, channel: 0, program: 0),
            new MidiNote(0.5, 0.25, 64, velocity: 64, channel: 1, program: 33),
            new MidiNote(0.75, 1.0, 36, velocity: 127, channel: 9),
        });

        MidiSequence decoded = MidiFile.Read(MidiFile.ToBytes(original));

        Assert.Equal(original.Count, decoded.Count);
        for (int i = 0; i < original.Count; i++)
        {
            MidiNote a = original.Notes[i], b = decoded.Notes[i];
            // Times survive to tick resolution: half a tick at 480 ticks per beat, 120 BPM ≈ 0.5 ms.
            Assert.Equal(a.Start, b.Start, 2);
            Assert.Equal(a.Duration, b.Duration, 2);
            Assert.Equal(a.NoteNumber, b.NoteNumber);
            Assert.Equal(a.Velocity, b.Velocity);
            Assert.Equal(a.Channel, b.Channel);
            Assert.Equal(a.Program, b.Program);
        }
    }

    [Fact]
    public void RoundTrip_PreservesTempo()
    {
        var sequence = new MidiSequence(new[] { new MidiNote(0, 1, 60) }, tempoBpm: 90);
        Assert.Equal(90, MidiFile.Read(MidiFile.ToBytes(sequence)).TempoBpm, 3);
    }

    [Fact]
    public void WrittenFile_HasFormat0HeaderAndEndOfTrack()
    {
        byte[] bytes = MidiFile.ToBytes(new MidiSequence(new[] { new MidiNote(0, 1, 60) }));

        Assert.Equal("MThd"u8.ToArray(), bytes[..4]);
        Assert.Equal(0, (bytes[8] << 8) | bytes[9]);   // format 0
        Assert.Equal(1, (bytes[10] << 8) | bytes[11]); // one track
        Assert.Equal(0x2F, bytes[^2]);                 // ends with the End of Track meta event
    }

    [Fact]
    public void NoteOnWithVelocityZero_IsANoteOff()
    {
        // Hand-built format-0 file: note-on C4, then (via running status) "note-on" velocity 0.
        byte[] bytes = SmfWithTrack(
            0x00, 0x90, 60, 100,   // delta 0, note on
            0x60, 60, 0,           // delta 96 (a beat at division 96), running status, velocity 0 = off
            0x00, 0xFF, 0x2F, 0x00);

        MidiSequence sequence = MidiFile.Read(bytes);

        MidiNote note = Assert.Single(sequence.Notes);
        Assert.Equal(60, note.NoteNumber);
        Assert.Equal(0.5, note.Duration, 3); // one beat at the default 120 BPM
    }

    [Fact]
    public void TempoChangeMidFile_ChangesSecondsFromThatPointOn()
    {
        // A beat at 120 BPM (0.5 s), then tempo doubles to 240 BPM, then another beat (0.25 s).
        byte[] bytes = SmfWithTrack(
            0x00, 0x90, 60, 100,
            0x60, 0x80, 60, 64,               // one beat later: first note off at 0.5 s
            0x00, 0xFF, 0x51, 0x03, 0x03, 0xD0, 0x90, // Set Tempo: 250 000 µs per beat = 240 BPM
            0x00, 0x90, 62, 100,
            0x60, 0x80, 62, 64,               // one more beat = 0.25 s at the new tempo
            0x00, 0xFF, 0x2F, 0x00);

        MidiSequence sequence = MidiFile.Read(bytes);

        Assert.Equal(0.5, sequence.Notes[0].Duration, 3);
        Assert.Equal(0.5, sequence.Notes[1].Start, 3);
        Assert.Equal(0.25, sequence.Notes[1].Duration, 3);
    }

    [Fact]
    public void UnterminatedNote_IsReleasedAtEndOfTrack()
    {
        byte[] bytes = SmfWithTrack(
            0x00, 0x90, 60, 100,
            0x60, 0xFF, 0x2F, 0x00); // end of track one beat later, note still held

        MidiNote note = Assert.Single(MidiFile.Read(bytes).Notes);
        Assert.Equal(0.5, note.Duration, 3);
    }

    [Fact]
    public void Format2AndSmpteDivision_AreRejectedWithClearErrors()
    {
        byte[] format2 = SmfWithTrack(0x00, 0xFF, 0x2F, 0x00);
        format2[9] = 2;
        Assert.Throws<FormatException>(() => MidiFile.Read(format2));

        byte[] smpte = SmfWithTrack(0x00, 0xFF, 0x2F, 0x00);
        smpte[12] = 0xE8; // −24 frames/s in the high byte: SMPTE division
        Assert.Throws<FormatException>(() => MidiFile.Read(smpte));
    }

    [Fact]
    public void NotAMidiFile_IsRejected()
    {
        Assert.Throws<FormatException>(() => MidiFile.Read("RIFF but not midi"u8.ToArray()));
    }

    [Fact]
    public void MidiNote_ValidatesItsRanges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MidiNote(-1, 1, 60));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MidiNote(0, 0, 60));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MidiNote(0, 1, 128));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MidiNote(0, 1, 60, velocity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MidiNote(0, 1, 60, channel: 16));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MidiNote(0, 1, 60, program: 128));
    }

    [Fact]
    public void Transposed_ShiftsPitchesButNeverDrums()
    {
        var sequence = new MidiSequence(new[]
        {
            new MidiNote(0, 1, 60, channel: 0),
            new MidiNote(0, 1, 36, channel: 9),
        });

        MidiSequence up = sequence.Transposed(7);

        Assert.Equal(67, up.Notes.Single(n => !n.IsPercussion).NoteNumber);
        Assert.Equal(36, up.Notes.Single(n => n.IsPercussion).NoteNumber);
    }

    [Fact]
    public void MidiSong_RendersDeterministically()
    {
        var song = new MidiSong(new MidiSequence(new[]
        {
            new MidiNote(0, 0.3, 60, program: 24),
            new MidiNote(0.1, 0.3, 36, channel: 9),
        }));

        Assert.Equal(
            song.Render(Context, 1).Samples.ToArray(),
            song.Render(Context, 1).Samples.ToArray());
    }

    [Fact]
    public void MidiSong_MakesSoundFromEveryProgramFamily()
    {
        for (int program = 0; program < 128; program += 8)
        {
            var song = new MidiSong(new MidiSequence(new[] { new MidiNote(0, 0.3, 60, program: program) }));
            Assert.True(song.Render(Context, 0.5).PeakLevel.Linear > 0, $"Program {program} rendered silence.");
        }
    }

    [Fact]
    public void MidiSong_VoicesPercussionFromChannel10()
    {
        var song = new MidiSong(new MidiSequence(new[] { new MidiNote(0, 0.1, 38, channel: 9) }));
        Assert.True(song.Render(Context, 0.5).PeakLevel.Linear > 0);
    }

    [Fact]
    public void MidiSong_DurationCoversInstrumentRingOut()
    {
        var sequence = new MidiSequence(new[] { new MidiNote(0, 0.2, 40, program: 25) });
        var song = new MidiSong(sequence);
        Assert.True(song.Duration >= sequence.Duration);
    }

    /// <summary>Wraps raw track bytes in a format-0 header at division 96 (96 ticks = one beat).</summary>
    private static byte[] SmfWithTrack(params byte[] track)
    {
        var bytes = new List<byte>();
        bytes.AddRange("MThd"u8.ToArray());
        bytes.AddRange(new byte[] { 0, 0, 0, 6, 0, 0, 0, 1, 0, 96 });
        bytes.AddRange("MTrk"u8.ToArray());
        bytes.AddRange(new byte[] { 0, 0, 0, (byte)track.Length });
        bytes.AddRange(track);
        return bytes.ToArray();
    }
}

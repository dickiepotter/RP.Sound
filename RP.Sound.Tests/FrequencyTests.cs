namespace RP.Sound.Tests;

public class FrequencyTests
{
    [Fact]
    public void MidiNote69_IsConcertA440()
    {
        Assert.Equal(440, Frequency.FromMidiNote(69).Hertz, 9);
    }

    [Fact]
    public void MidiNote_RoundTrips()
    {
        Frequency f = Frequency.FromMidiNote(52.5);
        Assert.Equal(52.5, f.MidiNote, 9);
    }

    [Theory]
    [InlineData("A4", 440)]
    [InlineData("C4", 261.6256)]
    [InlineData("C#4", 277.1826)]
    [InlineData("Eb2", 77.7817)]
    public void FromNote_ParsesNamesToPitch(string name, double expectedHertz)
    {
        Assert.Equal(expectedHertz, Frequency.FromNote(name).Hertz, 3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("H4")]
    [InlineData("A")]
    [InlineData("A#x")]
    public void TryFromNote_RejectsNonNotes(string? name)
    {
        Assert.False(Frequency.TryFromNote(name, out _));
        Assert.Throws<FormatException>(() => Frequency.FromNote(name ?? "H4"));
    }

    [Fact]
    public void Transposed_ByOctave_DoublesFrequency()
    {
        Assert.Equal(880, Frequency.A440.Transposed(12).Hertz, 9);
        Assert.Equal(220, Frequency.A440.Transposed(-12).Hertz, 9);
    }

    [Fact]
    public void BareDouble_IsTreatedAsHertz()
    {
        Frequency f = 123.5;
        Assert.Equal(123.5, (double)f, 12);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidHertz_CannotBeConstructed(double hertz)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Frequency(hertz));
    }

    [Fact]
    public void ZeroFrequency_HasNoMidiNote()
    {
        Assert.Throws<InvalidOperationException>(() => new Frequency(0).MidiNote);
    }
}

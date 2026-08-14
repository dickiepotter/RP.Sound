namespace RP.Sound.Tests;

public class LevelTests
{
    [Fact]
    public void Decibels_RoundTrip()
    {
        Assert.Equal(-6, Level.FromDecibels(-6).Decibels, 9);
        Assert.Equal(0, Level.Unity.Decibels, 9);
    }

    [Fact]
    public void Silence_IsNegativeInfinityDecibels()
    {
        Assert.Equal(double.NegativeInfinity, Level.Silence.Decibels);
    }

    [Fact]
    public void Gains_ComposeByMultiplication()
    {
        Level combined = Level.FromDecibels(-6) * Level.FromDecibels(-6);
        Assert.Equal(-12, combined.Decibels, 9);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidLinearGain_CannotBeConstructed(double linear)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Level(linear));
    }

    [Fact]
    public void Casts_FollowTheConvention()
    {
        double linear = Level.Half;              // reading out is implicit (lossless)
        var level = (Level)0.5;                  // building in is explicit (asserts non-negative)
        Assert.Equal(Level.Half.Linear, linear, 9);
        Assert.Equal(0.5, level.Linear, 9);
    }
}

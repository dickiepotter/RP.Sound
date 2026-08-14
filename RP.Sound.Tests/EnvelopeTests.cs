namespace RP.Sound.Tests;

public class EnvelopeTests
{
    [Fact]
    public void Amplitude_StaysWithinZeroAndOne()
    {
        var envelope = Envelope.Adsr(0.1, 0.2, Level.Half, 0.3);
        for (double t = -0.5; t < 3; t += 0.01)
        {
            double a = envelope.Amplitude(t, 2);
            Assert.InRange(a, 0, 1);
        }
    }

    [Fact]
    public void Adsr_HoldsTheSustainLevel()
    {
        var envelope = new Envelope(0.1, 0.1, Level.Half, 0.1, EnvelopeCurve.Linear);
        Assert.Equal(Level.Half.Linear, envelope.Amplitude(1.0, 2), 9);
    }

    [Fact]
    public void Attack_ReachesFullLevel()
    {
        var envelope = new Envelope(0.1, 0.1, Level.Half, 0.1, EnvelopeCurve.Linear);
        Assert.Equal(1, envelope.Amplitude(0.0999, 2), 2);
    }

    [Fact]
    public void OutsideTheDuration_IsSilent()
    {
        var envelope = Envelope.Percussive(0.5);
        Assert.Equal(0, envelope.Amplitude(-0.01, 1));
        Assert.Equal(0, envelope.Amplitude(1.0, 1));
    }

    [Fact]
    public void NegativeSegmentTimes_CannotBeConstructed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Envelope(-0.1, 0, Level.Unity, 0));
    }

    [Fact]
    public void SustainAboveUnity_CannotBeConstructed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Envelope(0, 0, new Level(1.5), 0));
    }
}

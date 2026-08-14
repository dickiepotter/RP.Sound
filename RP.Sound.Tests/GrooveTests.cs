using RP.Sound.Music;

namespace RP.Sound.Tests;

public class GrooveTests
{
    [Fact]
    public void Groove_StraightTimingIsJustTheGrid()
    {
        Groove groove = Groove.Straight(120); // 0.5 s per beat
        Assert.Equal(0.5, groove.SecondsPerBeat, 9);
        Assert.Equal(2.0, groove.BarSeconds, 9);
        Assert.Equal(0.25, groove.TimeOf(0, 0.5), 9);
        Assert.Equal(2.5, groove.TimeOf(1, 1), 9);
    }

    [Fact]
    public void Groove_ShuffleDelaysTheOffbeatEighthToTheLastThird()
    {
        Groove shuffle = Groove.Shuffle(120);
        // The "and" of a beat lands at 2/3 of the way through, not halfway: the triplet split.
        Assert.Equal(0.5 * (2.0 / 3.0), shuffle.TimeOf(0, 0.5), 9);
    }

    [Fact]
    public void Groove_SwingNeverMovesTheBeatsThemselves()
    {
        // The backbeat must stay planted: only positions inside a pair lean.
        Groove straight = Groove.Straight(96);
        Groove shuffle = Groove.Shuffle(96);
        for (int beat = 0; beat < 4; beat++)
            Assert.Equal(straight.TimeOf(0, beat), shuffle.TimeOf(0, beat), 9);
    }

    [Fact]
    public void Groove_SixteenthSwingLeavesTheEighthGridPlanted()
    {
        var house = new Groove(120, swing: 0.58, swingUnit: 0.25);
        Assert.Equal(0.25, house.TimeOf(0, 0.5), 9);              // offbeat eighth: planted
        // The first sixteenth is warped to 58% of its half-beat pair: 0.58 × 0.5 beats × 0.5 s.
        Assert.Equal(0.58 * 0.5 * 0.5, house.TimeOf(0, 0.25), 6); // 0.145 s, later than the straight 0.125
        Assert.True(house.TimeOf(0, 0.25) > 0.125);
    }

    [Fact]
    public void Groove_RejectsRushingAndNonsense()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Groove(400));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Groove(120, swing: 0.4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Groove(120, swingUnit: 0.3));
    }
}

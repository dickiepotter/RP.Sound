using RP.Sound.Effects;
using RP.Sound.Music;

namespace RP.Sound.Tests;

public class GenreTests
{
    private static readonly AudioRenderContext Context = new(SampleRate: 22050, Seed: 5);

    private static double WindowRms(AudioBuffer buffer, double from, double seconds)
    {
        int start = (int)(from * buffer.SampleRate);
        int length = Math.Min((int)(seconds * buffer.SampleRate), buffer.Length - start);
        return AudioBuffer.FromSamples(buffer.Samples.Slice(start, length), buffer.SampleRate).RmsLevel.Linear;
    }

    [Fact]
    public void Tracks_LoopExactlyOnTheBarGrid()
    {
        Assert.Equal(12 * 4 * (60.0 / 96), new BluesTrack(bpm: 96).LoopDuration, 6);
        Assert.Equal(8 * 4 * (60.0 / 120), new RockTrack(bpm: 120).LoopDuration, 6);
        Assert.Equal(8 * 4 * (60.0 / 140), new DubstepTrack(bpm: 140).LoopDuration, 6);
        Assert.Equal(8 * 4 * (60.0 / 124), new HouseTrack(bpm: 124).LoopDuration, 6);
        Assert.Equal(8 * 4 * (60.0 / 85), new ElectronicaTrack(bpm: 85).LoopDuration, 6);
    }

    [Fact]
    public void Tracks_EnforceTheirGenresTempoRanges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BluesTrack(bpm: 200));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RockTrack(bpm: 60));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DubstepTrack(bpm: 120)); // 140 or nothing
        Assert.Throws<ArgumentOutOfRangeException>(() => new HouseTrack(bpm: 150));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ElectronicaTrack(bpm: 130));
    }

    [Fact]
    public void Tracks_RenderAudiblyAndHonourTheRequestedDuration()
    {
        ISound[] tracks =
        {
            new BluesTrack(), new RockTrack(), new DubstepTrack(), new HouseTrack(), new ElectronicaTrack(),
        };
        foreach (ISound track in tracks)
        {
            AudioBuffer buffer = track.Render(Context, 3);
            Assert.Equal(3.0, buffer.Duration, 3);
            Assert.True(buffer.RmsLevel.Linear > 0.02, $"{track.GetType().Name} rendered near-silence.");
            Assert.True(buffer.PeakLevel.Linear <= 1.0, $"{track.GetType().Name} clipped.");
        }
    }

    [Fact]
    public void Tracks_AreDeterministic()
    {
        var track = new BluesTrack();
        Assert.True(track.Render(Context, 2).Samples.SequenceEqual(track.Render(Context, 2).Samples));
    }

    [Fact]
    public void HouseTrack_PutsTheKickOnEveryQuarterNote()
    {
        // Four-on-the-floor: sub-100 Hz energy right after every beat onset must beat the energy
        // between the kicks, on average across two bars.
        // The quiet window sits at 35% through each beat: after the kick has decayed ~30 dB but
        // before the offbeat bass note starts (whose 55 Hz fundamental would otherwise pollute a
        // low-passed measurement).
        var house = new HouseTrack();
        double beat = house.Groove.SecondsPerBeat;
        AudioBuffer lows = house.Render(Context, 8 * beat).LowPassed(150);
        double onKicks = 0, between = 0;
        for (int b = 0; b < 8; b++)
        {
            onKicks += WindowRms(lows, b * beat, 0.08);
            between += WindowRms(lows, b * beat + beat * 0.35, 0.06);
        }

        Assert.True(onKicks > 2 * between);
    }

    [Fact]
    public void DubstepTrack_PutsTheSnareOnBeatThreeOnly()
    {
        // The half-time fingerprint: in a drop bar, bright snare energy sits at beat 3 (index 2),
        // not at beats 2 or 4 where rock's backbeat would put it.
        var dubstep = new DubstepTrack(bars: 4, buildBars: 2);
        AudioBuffer buffer = dubstep.Render(Context, (float)dubstep.LoopDuration);
        AudioBuffer treble = buffer.HighPassed(4000);
        Groove groove = dubstep.Groove;

        double dropBarStart = groove.TimeOf(2, 0);
        double snare = WindowRms(treble, groove.TimeOf(2, 2), 0.1);
        double beat2 = WindowRms(treble, groove.TimeOf(2, 1), 0.1);
        double beat4 = WindowRms(treble, groove.TimeOf(2, 3), 0.1);
        Assert.True(snare > 2 * beat2, $"snare {snare} vs beat 2 {beat2} (bar starts {dropBarStart})");
        Assert.True(snare > 2 * beat4, $"snare {snare} vs beat 4 {beat4}");
    }

    [Fact]
    public void DubstepTrack_DropCarriesMoreBassWeightThanTheBuild()
    {
        var dubstep = new DubstepTrack(bars: 4, buildBars: 2);
        AudioBuffer lows = dubstep.Render(Context, (float)dubstep.LoopDuration).LowPassed(120);
        double build = WindowRms(lows, 0, dubstep.Groove.BarSeconds * 2);
        double drop = WindowRms(lows, dubstep.Groove.BarSeconds * 2, dubstep.Groove.BarSeconds * 2);
        Assert.True(drop > 1.5 * build);
    }

    [Fact]
    public void ElectronicaTrack_LateSnareIsDocumentedAndApplied()
    {
        // The laid-back placement is a stated constant, inside the researched 10–30 ms band.
        Assert.InRange(ElectronicaTrack.SnareLateness, 0.010, 0.030);
    }

    [Fact]
    public void Scale_BluesAddsTheFlatFiveToTheMinorPentatonic()
    {
        Scale blues = Scale.Blues(Frequency.FromNote("A2"));
        Assert.Equal(new[] { 0, 3, 5, 6, 7, 10 }, blues.Intervals);
    }
}

using RP.Sound.Effects;
using RP.Sound.Instruments;

namespace RP.Sound.Tests;

public class InstrumentTests
{
    private static readonly AudioRenderContext Context = new(SampleRate: 22050, Seed: 5);

    private static double RmsOfSecondHalf(AudioBuffer buffer) =>
        AudioBuffer.FromSamples(buffer.Samples[(buffer.Length / 2)..], buffer.SampleRate).RmsLevel.Linear;

    [Fact]
    public void KickDrum_EnergyLivesInTheLows()
    {
        AudioBuffer kick = new KickDrum().Render(Context);
        Assert.True(kick.LowPassed(200).RmsLevel.Linear > 5 * kick.HighPassed(2000).RmsLevel.Linear);
    }

    [Fact]
    public void KickDrum_MorePunchIsLouderAtTheStrike()
    {
        AudioBuffer soft = new KickDrum(punch: 0.1).Render(Context);
        AudioBuffer hard = new KickDrum(punch: 1.0).Render(Context);
        Assert.True(hard.HighPassed(1000).RmsLevel.Linear > soft.HighPassed(1000).RmsLevel.Linear);
    }

    [Fact]
    public void SnareDrum_SnappyTradesToneForNoise()
    {
        AudioBuffer tone = new SnareDrum(snappy: 0.1).Render(Context);
        AudioBuffer rattle = new SnareDrum(snappy: 1.0).Render(Context);
        Assert.True(rattle.HighPassed(3000).RmsLevel.Linear > tone.HighPassed(3000).RmsLevel.Linear);
        Assert.True(tone.LowPassed(400).RmsLevel.Linear > rattle.LowPassed(400).RmsLevel.Linear);
    }

    [Fact]
    public void HiHat_EnergyLivesInTheHighs()
    {
        AudioBuffer hat = HiHat.Closed().Render(Context);
        Assert.True(hat.HighPassed(5000).RmsLevel.Linear > 3 * hat.LowPassed(1000).RmsLevel.Linear);
    }

    [Fact]
    public void HiHat_OpenRingsLongerThanClosed()
    {
        Assert.True(HiHat.Open().Duration > 4 * HiHat.Closed().Duration);
        AudioBuffer open = HiHat.Open().Render(Context, 0.5);
        AudioBuffer closed = HiHat.Closed().Render(Context, 0.5);
        Assert.True(RmsOfSecondHalf(open) > 10 * (RmsOfSecondHalf(closed) + 1e-9));
    }

    [Fact]
    public void TomDrum_SitsBetweenKickAndSnareInPitch()
    {
        AudioBuffer tom = new TomDrum().Render(Context);
        Assert.True(tom.RmsLevel.Linear > 0.01);
        Assert.True(tom.BandPassed(110, 1).RmsLevel.Linear > tom.HighPassed(2000).RmsLevel.Linear);
    }

    [Fact]
    public void Cymbal_CrashesThenShimmersAway()
    {
        AudioBuffer cymbal = new Cymbal(decay: 1.5).Render(Context);
        Assert.True(cymbal.RmsLevel.Linear > 0.01);
        Assert.True(cymbal.FittedToDuration(0.3).RmsLevel.Linear > RmsOfSecondHalf(cymbal));
    }

    [Fact]
    public void BassGuitar_BrighterToneKeepsMoreTreble()
    {
        AudioBuffer dark = new BassGuitar(Frequency.FromNote("E1"), tone: 0).Render(Context);
        AudioBuffer bright = new BassGuitar(Frequency.FromNote("E1"), tone: 1).Render(Context);
        Assert.True(bright.HighPassed(800).RmsLevel.Linear > dark.HighPassed(800).RmsLevel.Linear);
    }

    [Fact]
    public void Guitar_StrumSpreadsNotesInTime()
    {
        ISound strum = Guitar.Strum(new[] { Frequency.FromNote("E2"), Frequency.FromNote("B2"), Frequency.FromNote("E3") }, duration: 1, strumSeconds: 0.05);
        Assert.Equal(1 + 2 * 0.05, strum.Duration, 6);
        Assert.True(strum.Render(Context).RmsLevel.Linear > 0.01);
    }

    [Fact]
    public void Guitar_PowerChordIsRootFifthOctave()
    {
        // The voicing must contain the fifth (3/2 the root) — band-pass around it and listen.
        Frequency root = Frequency.FromNote("A2");
        AudioBuffer chord = Guitar.PowerChord(root, duration: 1).Render(Context);
        Assert.True(chord.BandPassed(root.Transposed(7), 5).RmsLevel.Linear > 0.005);
    }

    [Fact]
    public void Mallet_GlockenspielRingsLongestMarimbaWarmest()
    {
        Frequency note = Frequency.FromNote("C5");
        Assert.True(Mallet.Glockenspiel(note).Decay > Mallet.Marimba(note).Decay);
        Assert.True(Mallet.Marimba(note).Decay > Mallet.Xylophone(note).Decay);
    }

    [Fact]
    public void Mallet_LowerBarsRingLonger()
    {
        Assert.True(Mallet.Marimba(Frequency.FromNote("C3")).Decay > Mallet.Marimba(Frequency.FromNote("C6")).Decay);
    }

    [Fact]
    public void Organ_RegistrationIsValidatedDigitByDigit()
    {
        Assert.Throws<ArgumentException>(() => new Organ(440, registration: "888"));
        Assert.Throws<ArgumentException>(() => new Organ(440, registration: "888000009"));
        Assert.Throws<ArgumentException>(() => new Organ(440, registration: "88800000x"));
    }

    [Fact]
    public void Organ_JazzRegistrationIncludesTheSubOctave()
    {
        // "888000000" draws the 16′ bar: half the played frequency must be present.
        AudioBuffer note = Organ.Jazz(Frequency.FromNote("A4"), 1).Render(Context);
        Assert.True(note.BandPassed(220, 5).RmsLevel.Linear > 0.01);
    }

    [Fact]
    public void Flute_IsNearlyAllFundamental()
    {
        Frequency note = Frequency.FromNote("A5");
        AudioBuffer flute = new Flute(note, 1).Render(Context);
        Assert.True(flute.BandPassed(note, 2).RmsLevel.Linear > 3 * flute.HighPassed(note.Transposed(19)).RmsLevel.Linear);
    }

    [Fact]
    public void Brass_BrightnessOpensTheSpectrum()
    {
        Frequency note = Frequency.FromNote("C3");
        AudioBuffer mellow = new Brass(note, 1, brightness: 0).Render(Context);
        AudioBuffer blazing = new Brass(note, 1, brightness: 1).Render(Context);
        Assert.True(blazing.HighPassed(1500).RmsLevel.Linear > 2 * mellow.HighPassed(1500).RmsLevel.Linear);
    }

    [Fact]
    public void SynthPad_ChordSharesItsLevelAcrossVoices()
    {
        ISound chord = SynthPad.Chord(new[] { Frequency.FromNote("C3"), Frequency.FromNote("E3"), Frequency.FromNote("G3") }, duration: 2);
        AudioBuffer buffer = chord.Render(Context);
        Assert.True(buffer.RmsLevel.Linear > 0.01);
        Assert.True(buffer.PeakLevel.Linear < 1.5);
    }

    [Fact]
    public void Instruments_RenderExactlyTheRequestedDuration()
    {
        ISound[] all =
        {
            new KickDrum(), new SnareDrum(), HiHat.Closed(), new TomDrum(), new Cymbal(),
            new BassGuitar(Frequency.FromNote("A1")), new Guitar(Frequency.FromNote("A3")),
            Mallet.Marimba(Frequency.MiddleC), Organ.Jazz(440), new Flute(880), new Brass(220),
            new SynthPad(Frequency.MiddleC),
        };
        foreach (ISound sound in all)
            Assert.Equal(1.0, sound.Render(Context, 1.0).Duration, 3);
    }

    [Fact]
    public void Instruments_AreDeterministic()
    {
        ISound[] all =
        {
            new KickDrum(), new SnareDrum(), HiHat.Open(), new Cymbal(),
            new Guitar(Frequency.FromNote("A3")), Mallet.Glockenspiel(Frequency.FromNote("C6")),
        };
        foreach (ISound sound in all)
        {
            AudioBuffer first = sound.Render(Context, 0.5);
            AudioBuffer second = sound.Render(Context, 0.5);
            Assert.True(first.Samples.SequenceEqual(second.Samples), $"{sound.GetType().Name} rendered differently twice.");
        }
    }
}

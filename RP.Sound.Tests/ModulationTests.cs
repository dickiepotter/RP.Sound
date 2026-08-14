using RP.Sound.Effects;
using RP.Sound.Synthesis;

namespace RP.Sound.Tests;

/// <summary>
/// The three modulation primitives — FM, ring modulation and the swept filter. Their whole purpose
/// is to put energy at frequencies the input did not contain, so most of these tests measure the
/// spectrum directly rather than trusting a level meter.
/// </summary>
public class ModulationTests
{
    private static readonly AudioRenderContext Context = new(SampleRate: 22050, Seed: 1);

    private const double Duration = 0.5;

    /// <summary>
    /// The magnitude of one frequency in a buffer, by correlating against a complex exponential
    /// (one bin of a DFT, the Goertzel way). At half a second the bin spacing is 2 Hz, so every
    /// frequency asserted below sits exactly on a bin and leakage is not a factor.
    /// </summary>
    private static double Magnitude(AudioBuffer buffer, double hertz)
    {
        double omega = 2 * System.Math.PI * hertz / buffer.SampleRate;
        double real = 0, imaginary = 0;
        for (int i = 0; i < buffer.Length; i++)
        {
            real += buffer[i] * System.Math.Cos(omega * i);
            imaginary += buffer[i] * System.Math.Sin(omega * i);
        }

        return 2 * System.Math.Sqrt(real * real + imaginary * imaginary) / buffer.Length;
    }

    private static AudioBuffer Tone(double hertz) =>
        new Oscillator(Waveform.Sine, hertz, Duration, new Level(0.5)).Render(Context, Duration);

    // ---- FM ----

    [Fact]
    public void Fm_AtZeroIndex_IsAPlainOscillator()
    {
        // The operator must be perfectly transparent when it is turned off, or every patch that
        // leaves the modulator idle pays for it in tone.
        AudioBuffer plain = new Oscillator(Waveform.Sine, 400, Duration).Render(Context, Duration);
        AudioBuffer modulated = new FmOscillator(400, Duration, ratio: 2.41, index: 0).Render(Context, Duration);
        Assert.Equal(plain.Samples.ToArray(), modulated.Samples.ToArray());
    }

    [Fact]
    public void Fm_AtWholeRatio_PutsEnergyOnTheHarmonics()
    {
        // Carrier 400, modulator 800: sidebands land at |400 + 800n| — that is, on the odd multiples
        // of 400 and nowhere else. Sidebands driven below zero hertz reflect back as audible ones,
        // but a harmonic ratio reflects them onto the same series, so the series stays clean.
        AudioBuffer buffer = new FmOscillator(400, Duration, ratio: 2, index: 3).Render(Context, Duration);
        Assert.True(Magnitude(buffer, 1200) > 0.05, "the odd harmonics carry the sidebands");
        Assert.True(Magnitude(buffer, 2000) > 0.05);
        Assert.True(Magnitude(buffer, 800) < 0.01, "the even harmonics stay empty");
        Assert.True(Magnitude(buffer, 1600) < 0.01);
    }

    [Fact]
    public void Fm_AtFractionalRatio_PutsEnergyBetweenTheHarmonics()
    {
        // Carrier 400, modulator 400 × 2.41 = 964: the first sidebands land at 1364 and |400−964| =
        // 564 — neither of them a multiple of 400. A spectrum with no common fundamental is exactly
        // what the ear calls metallic, and is the reason the sci-fi palette uses ratios like this.
        AudioBuffer buffer = new FmOscillator(400, Duration, ratio: 2.41, index: 5).Render(Context, Duration);
        Assert.True(Magnitude(buffer, 1364) > 0.05, "the upper inharmonic sideband should be strong");
        Assert.True(Magnitude(buffer, 564) > 0.05, "the reflected lower sideband should be strong");
        Assert.True(Magnitude(buffer, 800) < Magnitude(buffer, 1364) / 3, "the harmonic series should stay comparatively empty");
    }

    [Fact]
    public void Fm_RaisingTheIndex_MovesEnergyOutOfTheCarrierIntoTheSidebands()
    {
        // Sideband amplitudes follow the Bessel functions Jn(index), which oscillate rather than
        // climbing forever — so "more index, more of everything" is false in general. What does hold
        // over the first stretch is the trade: the carrier J0 drains as the first sidebands J1 fill.
        // A high carrier over a slow modulator keeps every sideband above zero hertz, so nothing
        // reflects back and confuses the measurement.
        AudioBuffer At(double index) =>
            new FmOscillator(4000, Duration, ratio: 0.25, index: index).Render(Context, Duration);

        AudioBuffer none = At(0), some = At(1), more = At(2);

        Assert.True(Magnitude(none, 4000) > Magnitude(some, 4000));
        Assert.True(Magnitude(some, 4000) > Magnitude(more, 4000));

        Assert.True(Magnitude(more, 5000) > Magnitude(some, 5000));
        Assert.True(Magnitude(some, 5000) > Magnitude(none, 5000));
    }

    [Fact]
    public void Fm_SweepsTheCarrierAndTakesTheModulatorWithIt()
    {
        // Falling 1600 -> 200 with the modulator locked to the carrier. Measured over the last
        // tenth, the carrier should be near 200 Hz; if the modulator did not track it, the sidebands
        // would smear the count badly.
        AudioBuffer buffer = new FmOscillator(1600, Duration, end: 200, ratio: 2, index: 2).Render(Context, Duration);
        AudioBuffer tail = AudioBuffer.FromSamples(buffer.Samples[(int)(buffer.Length * 0.9)..], buffer.SampleRate);
        Assert.True(Magnitude(tail, 200) > Magnitude(tail, 1600) * 5);
    }

    [Fact]
    public void Fm_ExponentialSweep_SpendsHalfTheTimeBelowTheGeometricMean()
    {
        // 1600 -> 100 is four octaves; halfway through an exponential glide is two octaves down at
        // 400 Hz, not the linear midpoint of 850. This is the whole reason the default is exponential.
        var sweep = new FmOscillator(1600, Duration, end: 100);
        AudioBuffer half = sweep.Render(Context, Duration);
        AudioBuffer middle = AudioBuffer.FromSamples(half.Samples[(int)(half.Length * 0.45)..(int)(half.Length * 0.55)], half.SampleRate);
        Assert.True(Magnitude(middle, 400) > Magnitude(middle, 850));
    }

    [Fact]
    public void Fm_RejectsParametersThatWouldAliasOrGlideThroughZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FmOscillator(400, ratio: 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FmOscillator(400, index: 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FmOscillator(400, duration: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FmOscillator(400, end: new Frequency(0)));

        // ...but a glide to zero is allowed once you have asked for a linear one.
        _ = new FmOscillator(400, end: new Frequency(0), exponentialSweep: false);
    }

    [Fact]
    public void Fm_Vibrato_MovesThePitchWithoutMovingItsCentre()
    {
        var steady = new FmOscillator(400, Duration).Render(Context, Duration);
        var wobbling = new FmOscillator(400, Duration, lfo: Lfo.Vibrato(cents: 100, rate: new Frequency(6))).Render(Context, Duration);

        // The vibrato spreads energy off the exact carrier bin into neighbours either side.
        Assert.True(Magnitude(wobbling, 400) < Magnitude(steady, 400));
        Assert.True(Magnitude(wobbling, 424) > Magnitude(steady, 424));
    }

    // ---- Ring modulation ----

    [Fact]
    public void RingModulation_ReplacesTheToneWithSumAndDifference()
    {
        // 1000 Hz rung at 400 Hz becomes 600 and 1400, and the original 1000 vanishes entirely —
        // the defining property, and why the result has no pitch to hear.
        AudioBuffer rung = Tone(1000).RingModulated(400);
        Assert.True(Magnitude(rung, 600) > 0.2);
        Assert.True(Magnitude(rung, 1400) > 0.2);
        Assert.True(Magnitude(rung, 1000) < 0.01);
    }

    [Fact]
    public void RingModulation_MixControlsHowMuchFundamentalSurvives()
    {
        Assert.True(Magnitude(Tone(1000).RingModulated(400, mix: 0.5), 1000) > 0.2);

        // A dry mix is the identity.
        Assert.Equal(Tone(1000).Samples.ToArray(), Tone(1000).RingModulated(400, mix: 0).Samples.ToArray());
    }

    [Fact]
    public void RingModulation_RejectsAMixOutsideUnitRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Tone(1000).RingModulated(400, mix: 1.5));
    }

    // ---- Filter sweep ----

    [Fact]
    public void FilterSweep_WithEqualEndpoints_IsTheStaticFilter()
    {
        AudioBuffer source = Tone(500).MixedWith(Tone(4000));
        Assert.Equal(
            Filter.LowPass(source, 1200).Samples.ToArray(),
            FilterSweep.LowPass(source, 1200, 1200).Samples.ToArray());
    }

    [Fact]
    public void FilterSweep_ClosingDown_DarkensAsItGoes()
    {
        // A steady bright tone under a closing filter: loud at the start, gone by the end. This is
        // the gesture that turns a noise burst into a whoosh.
        AudioBuffer swept = FilterSweep.LowPass(Tone(4000), 8000, 200);
        AudioBuffer opening = AudioBuffer.FromSamples(swept.Samples[..(swept.Length / 10)], swept.SampleRate);
        AudioBuffer closing = AudioBuffer.FromSamples(swept.Samples[(swept.Length * 9 / 10)..], swept.SampleRate);
        Assert.True(opening.RmsLevel.Linear > closing.RmsLevel.Linear * 10);
    }

    [Fact]
    public void FilterSweep_OpeningUp_IsTheMirrorImage()
    {
        AudioBuffer swept = FilterSweep.LowPass(Tone(4000), 200, 8000);
        AudioBuffer opening = AudioBuffer.FromSamples(swept.Samples[..(swept.Length / 10)], swept.SampleRate);
        AudioBuffer closing = AudioBuffer.FromSamples(swept.Samples[(swept.Length * 9 / 10)..], swept.SampleRate);
        Assert.True(closing.RmsLevel.Linear > opening.RmsLevel.Linear * 10);
    }

    [Fact]
    public void FilterSweep_RejectsEndpointsItCannotGlideBetween()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FilterSweep.LowPass(Tone(1000), 0, 8000));
    }

    // ---- Determinism, the library-wide contract ----

    [Fact]
    public void TheWholeChain_RendersIdenticallyEveryTime()
    {
        ISound zap = new FmOscillator(900, 0.2, end: 200, ratio: 2.41, index: 5.5)
            .RingModulated(140)
            .LowPassSwept(9000, 700);

        Assert.Equal(
            zap.Render(Context, 0.2).Samples.ToArray(),
            zap.Render(Context, 0.2).Samples.ToArray());
    }
}

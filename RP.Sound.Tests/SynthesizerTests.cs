using RP.Sound.Effects;
using RP.Sound.Synthesis;

namespace RP.Sound.Tests;

public class SynthesizerTests
{
    private static readonly AudioRenderContext Context = new(SampleRate: 22050, Seed: 5);

    private static double WindowRms(AudioBuffer buffer, double from, double seconds)
    {
        int start = (int)(from * buffer.SampleRate);
        int length = (int)(seconds * buffer.SampleRate);
        return AudioBuffer.FromSamples(buffer.Samples.Slice(start, length), buffer.SampleRate).RmsLevel.Linear;
    }

    [Fact]
    public void SynthPatch_RejectsNonsense()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SynthPatch(oscillator2DetuneCents: 5000));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SynthPatch(oscillatorMix: 1.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SynthPatch(filterResonance: 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SynthPatch(filterEnvelopeOctaves: 20));
    }

    [Fact]
    public void Lfo_RejectsAudioRateAndExcessDepth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Lfo(Waveform.Sine, new Frequency(2000)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Lfo(Waveform.Sine, 5, pitchCents: 2000));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Lfo(Waveform.Sine, 5, tremoloDepth: 2));
    }

    [Fact]
    public void Lfo_NoneHoldsAtZero()
    {
        Assert.Equal(0, Lfo.None.Sample(0.123));
        Assert.Equal(0, Lfo.None.Sample(7.7));
    }

    [Fact]
    public void Synthesizer_TremoloMakesLoudnessBreathe()
    {
        // A single oscillator (mix 0): two detuned oscillators would beat, which is its own
        // (correct) loudness fluctuation and would muddy what this test isolates.
        var steady = new SynthPatch(oscillatorMix: 0, amplitudeEnvelope: Envelope.Sustained(0.01, 0.01));
        var breathing = new SynthPatch(
            oscillatorMix: 0,
            amplitudeEnvelope: Envelope.Sustained(0.01, 0.01),
            lfo: new Lfo(Waveform.Sine, new Frequency(2), tremoloDepth: 1));

        AudioBuffer flat = new Synthesizer(steady, 220, 2).Render(Context);
        AudioBuffer wobbly = new Synthesizer(breathing, 220, 2).Render(Context);

        // At 2 Hz the loudness dips every half second; a steady patch stays level.
        static double Contrast(AudioBuffer b)
        {
            double loud = 0, quiet = double.MaxValue;
            for (double t = 0.25; t + 0.1 < 2; t += 0.125)
            {
                double rms = WindowRms(b, t, 0.1);
                loud = Math.Max(loud, rms);
                quiet = Math.Min(quiet, rms);
            }

            return loud / (quiet + 1e-9);
        }

        Assert.True(Contrast(wobbly) > 3);
        Assert.True(Contrast(flat) < 1.5);
    }

    [Fact]
    public void Synthesizer_PluckClosesItsFilterAsItDies()
    {
        AudioBuffer pluck = new Synthesizer(SynthPatch.Pluck, 220, 1.5).Render(Context);
        double brightStart = WindowRms(pluck.HighPassed(1500), 0.0, 0.1);
        double darkTail = WindowRms(pluck.HighPassed(1500), 0.8, 0.1);
        Assert.True(brightStart > 3 * (darkTail + 1e-9));
    }

    [Fact]
    public void Synthesizer_PadFadesInSlowly()
    {
        AudioBuffer pad = new Synthesizer(SynthPatch.Pad, 220, 4).Render(Context);
        Assert.True(WindowRms(pad, 0, 0.2) < 0.5 * WindowRms(pad, 1.5, 0.2));
    }

    [Fact]
    public void Synthesizer_WobbleActuallyWobbles()
    {
        // The LFO swings the cutoff ±2.5 octaves at 2 Hz: high-frequency content must come and
        // go at that rate, which shows up as contrast between the brightest and darkest windows.
        AudioBuffer wobble = new Synthesizer(SynthPatch.Wobble(new Frequency(2)), 55, 2).Render(Context);
        AudioBuffer treble = wobble.HighPassed(600);
        double loud = 0, quiet = double.MaxValue;
        for (double t = 0.25; t + 0.1 < 2; t += 0.125)
        {
            double rms = WindowRms(treble, t, 0.1);
            loud = Math.Max(loud, rms);
            quiet = Math.Min(quiet, rms);
        }

        Assert.True(loud > 2 * (quiet + 1e-9));
    }

    [Fact]
    public void Synthesizer_IsDeterministic()
    {
        var synth = new Synthesizer(SynthPatch.Lead, 440, 1);
        Assert.True(synth.Render(Context).Samples.SequenceEqual(synth.Render(Context).Samples));
    }

    [Fact]
    public void Synthesizer_HonoursTheRenderContract()
    {
        var synth = new Synthesizer(SynthPatch.Bass, 110, 1.0);
        Assert.Equal(0.5, synth.Render(Context, 0.5).Duration, 3);  // cut short
        Assert.Equal(2.0, synth.Render(Context, 2.0).Duration, 3);  // padded with silence
    }
}

using RP.Sound.Effects;

namespace RP.Sound.Synthesis;

/// <summary>
/// One note played on a subtractive synthesizer: a <see cref="SynthPatch"/> (what the instrument
/// is) applied to a note and a duration (what is played). The render walks the classic signal
/// path sample by sample — oscillators → mix (+noise) → low-pass filter → amplifier — with the
/// filter's cutoff recomputed on the way from its envelope and the LFO, and the LFO's other two
/// destinations (pitch, loudness) applied where they belong. See <see cref="SynthPatch"/> for
/// why this architecture is the one worth learning.
/// </summary>
public sealed class Synthesizer : ISound
{
    public SynthPatch Patch { get; }
    public Frequency Note { get; }
    public double Duration { get; }
    public Level Level { get; }

    public Synthesizer(SynthPatch patch, Frequency note, double duration = 1.0, Level? level = null)
    {
        if (note.Hertz <= 0)
            throw new ArgumentOutOfRangeException(nameof(note), note, "A synthesizer note must have a positive pitch.");
        if (duration < 0 || !double.IsFinite(duration))
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A note's duration must be finite and non-negative.");
        this.Patch = patch ?? throw new ArgumentNullException(nameof(patch));
        this.Note = note;
        this.Duration = duration;
        this.Level = level ?? Level.Unity;
    }

    public AudioBuffer Render(AudioRenderContext context, double duration)
    {
        var samples = new float[context.SampleCount(duration)];
        int active = System.Math.Min(samples.Length, context.SampleCount(System.Math.Min(Duration, duration)));
        DeterministicRandom random = context.CreateRandom($"synth:{Note.Hertz:0.###}");

        double noteLength = System.Math.Min(Duration, duration);
        double detuneRatio = System.Math.Pow(2, Patch.Oscillator2DetuneCents / 1200.0);

        var filter = Biquad.LowPass(context.SampleRate, Patch.FilterCutoff.Hertz, Patch.FilterResonance);

        // The filter cutoff moves every block of 16 samples (~0.4 ms at 44.1 kHz) rather than
        // every sample: far finer than the ear can follow a filter sweep, at a sixteenth of the
        // coefficient-recomputation cost.
        const int block = 16;

        double phase1 = 0, phase2 = 0;
        for (int i = 0; i < active; i++)
        {
            double t = (double)i / context.SampleRate;
            double lfo = Patch.Lfo.Sample(t);

            if (i % block == 0)
            {
                // Envelope and LFO both act in octaves (multiplicatively) because pitch and
                // brightness are heard logarithmically.
                double filterEnvelope = Patch.FilterEnvelope.Amplitude(t, noteLength);
                double octaves = Patch.FilterEnvelopeOctaves * filterEnvelope + Patch.Lfo.CutoffOctaves * lfo;
                double cutoff = System.Math.Clamp(Patch.FilterCutoff.Hertz * System.Math.Pow(2, octaves), 20, context.SampleRate * 0.45);
                filter.RetuneLowPass(context.SampleRate, cutoff, Patch.FilterResonance);
            }

            // Vibrato bends both oscillators together; the detune ratio then splits them.
            double vibratoRatio = System.Math.Pow(2, Patch.Lfo.PitchCents * lfo / 1200.0);
            double hertz = Note.Hertz * vibratoRatio;
            phase1 += hertz / context.SampleRate;
            phase2 += hertz * detuneRatio / context.SampleRate;
            if (phase1 >= 1) phase1 -= 1;
            if (phase2 >= 1) phase2 -= 1;

            double mixed =
                Oscillator.Sample(Patch.Oscillator1, phase1) * (1 - Patch.OscillatorMix) +
                Oscillator.Sample(Patch.Oscillator2, phase2) * Patch.OscillatorMix;
            if (Patch.NoiseMix > 0)
                mixed = mixed * (1 - Patch.NoiseMix) + random.NextSigned() * Patch.NoiseMix;

            double filtered = filter.Process(mixed);

            double amplitude = Patch.AmplitudeEnvelope.Amplitude(t, noteLength);
            double tremolo = 1 - Patch.Lfo.TremoloDepth * 0.5 * (1 + lfo);
            samples[i] = (float)(filtered * amplitude * tremolo * Level.Linear);
        }

        return AudioBuffer.TakeOwnership(samples, context.SampleRate);
    }
}

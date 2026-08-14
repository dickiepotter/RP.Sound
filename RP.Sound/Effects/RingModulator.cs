namespace RP.Sound.Effects;

using RP.Sound.Synthesis;

/// <summary>
/// Ring modulation: multiply the signal by a second oscillator. Multiplying two sine waves is the
/// same as adding a pair at their sum and difference frequencies — so every partial in the input is
/// replaced by two new ones, and the original fundamental disappears entirely.
///
/// That last part is why the effect is so recognisable. The output's partials are no longer whole
/// multiples of anything, so the ear cannot assign it a pitch; it hears a clang instead of a note.
/// Broadcast engineers built ring modulators from a ring of four diodes (hence the name) to shift
/// radio carriers, and the BBC Radiophonic Workshop borrowed one in 1963 to make the Daleks speak —
/// still the defining sound of a hostile machine, and the reason it belongs in a sci-fi palette.
/// </summary>
public static class RingModulator
{
    /// <summary>
    /// Rings a buffer against an oscillator at <paramref name="frequency"/>. <paramref name="mix"/>
    /// blends the result back against the untouched signal: at 1 the fundamental is gone completely,
    /// and lower values leave some of the original pitch audible under the clang.
    /// </summary>
    public static AudioBuffer Apply(AudioBuffer buffer, Frequency frequency, double mix = 1, Waveform waveform = Waveform.Sine)
    {
        if (mix is < 0 or > 1 || !double.IsFinite(mix))
            throw new ArgumentOutOfRangeException(nameof(mix), mix, "The ring-modulation mix is a fraction between 0 (dry) and 1 (fully modulated).");

        var samples = new float[buffer.Length];
        double step = frequency.Hertz / buffer.SampleRate;
        double phase = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double modulated = buffer[i] * Oscillator.Sample(waveform, phase);
            samples[i] = (float)(buffer[i] + (modulated - buffer[i]) * mix);
            phase += step;
            phase -= System.Math.Floor(phase);
        }

        return AudioBuffer.TakeOwnership(samples, buffer.SampleRate);
    }

    public static ISound Apply(ISound sound, Frequency frequency, double mix = 1, Waveform waveform = Waveform.Sine) =>
        new FilterExtensions.FilteredSound(sound, buffer => Apply(buffer, frequency, mix, waveform));
}

public static class RingModulatorExtensions
{
    public static AudioBuffer RingModulated(this AudioBuffer buffer, Frequency frequency, double mix = 1, Waveform waveform = Waveform.Sine) =>
        RingModulator.Apply(buffer, frequency, mix, waveform);

    public static ISound RingModulated(this ISound sound, Frequency frequency, double mix = 1, Waveform waveform = Waveform.Sine) =>
        RingModulator.Apply(sound, frequency, mix, waveform);
}

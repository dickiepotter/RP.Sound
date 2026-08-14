namespace RP.Sound.Games;

using RP.Sound.Effects;
using RP.Sound.Synthesis;

/// <summary>
/// The science-fiction sound palette: the events a game needs when the fiction is energy weapons
/// and materialising matter rather than wood, stone and weather. Where the
/// <see cref="Physics"/> namespace derives a sound from what physically happened, these are
/// derived from a convention — decades of film and television have taught the ear that a falling
/// inharmonic tone is a discharge, that a clang with no fundamental is a failing machine, and that
/// a rising sweep with vibrato is something arriving out of nowhere. The vocabulary is learned, not
/// physical, so these are compositions rather than models, and the doc comments say what each
/// gesture is doing and why the ear reads it that way.
///
/// Every preset takes the pitch it should centre on, so a caller can map it to whatever the game
/// knows: mass, size, charge, distance. They are mono, like everything else in the library — pan
/// and distance belong to <see cref="Mixing.SoundPlacement"/> or to the playback layer.
///
/// The building blocks are <see cref="FmOscillator"/> for inharmonic tone,
/// <see cref="RingModulator"/> for clang, and <see cref="FilterSweep"/> over
/// <see cref="Noise"/> for air and thrust.
/// </summary>
public static class SciFi
{
    /// <summary>
    /// A hard energy-weapon discharge. A steep downward glide is the canonical zap — pitch falling
    /// that fast reads as energy being spent — and the FM at a deliberately fractional ratio gives
    /// it a synthetic bite that no plain waveform produces. The noise tick on the front is the
    /// spark of contact; without it the tone starts too politely to sound like a hit.
    /// </summary>
    public static ISound Zap(Frequency pitch)
    {
        const double attack = 0.002, decay = 0.14;

        ISound tone = new FmOscillator(pitch, attack + decay, end: pitch.Hertz * 0.22, ratio: 2.41, index: 5.5, level: new Level(0.30))
            .Shaped(Strike(attack, decay));

        ISound spark = new Noise(NoiseColor.White, new Level(0.11), stream: "scifi-zap")
            .Trimmed(0.051)
            .LowPassSwept(13300, 2000)
            .Shaped(Strike(0.001, 0.05));

        return tone.MixedWith(spark);
    }

    /// <summary>
    /// A reactor losing containment. Ring modulation over a long downward glide is the whole
    /// effect: stripping the fundamental leaves a sound with no pitch to hold on to, which is why
    /// it reads as a machine failing rather than an instrument playing. A triangle an octave below
    /// gives it weight, and the vent noise darkens as it fades — a bright hiss going dull is the
    /// sound of pressure running out.
    /// </summary>
    public static ISound Implode(Frequency pitch)
    {
        const double attack = 0.004, decay = 0.55;

        ISound collapse = new FmOscillator(pitch, attack + decay, end: pitch.Hertz * 0.18, ratio: 1.37, index: 3.2, level: new Level(0.34))
            .RingModulated(pitch.Hertz * 0.51)
            .Shaped(Strike(attack, decay));

        ISound weight = new FmOscillator(pitch.Hertz * 0.5, 0.606, end: pitch.Hertz * 0.16, carrier: Waveform.Triangle, level: new Level(0.26))
            .Shaped(Strike(0.006, 0.60));

        ISound vent = new Noise(NoiseColor.White, new Level(0.20), stream: "scifi-implode")
            .Trimmed(0.422)
            .LowPassSwept(5600, 290)
            .Shaped(Strike(0.002, 0.42));

        return Sounds.Mix(collapse, weight, vent);
    }

    /// <summary>
    /// Something materialising. The mirror of <see cref="Zap"/>: the glide rises instead of
    /// falling, over a much slower attack, so the ear hears arrival rather than impact. The fast
    /// shallow vibrato is the tell that it is not quite solid yet, and the shimmer opening from
    /// dull to bright is the thing resolving into place.
    /// </summary>
    public static ISound Chime(Frequency pitch)
    {
        const double attack = 0.020, decay = 0.34;

        ISound materialise = new FmOscillator(
            pitch.Hertz * 0.5, attack + decay,
            end: pitch.Hertz * 2.2, ratio: 3.02, index: 2.6,
            lfo: new Lfo(Waveform.Sine, new Frequency(17), pitchCents: 38),
            level: new Level(0.20))
            .Shaped(Strike(attack, decay));

        ISound shimmer = new Noise(NoiseColor.White, new Level(0.07), stream: "scifi-chime")
            .Trimmed(0.29)
            .LowPassSwept(5600, 18000)
            .Shaped(Strike(0.030, 0.26));

        return materialise.MixedWith(shimmer);
    }

    /// <summary>
    /// A replicator cycle — one thing becoming two. Two ring-modulated voices climbing at slightly
    /// different rates, the second detuned by half a percent so the pair beats slowly against
    /// itself. The beating is the point: two things almost but not quite in step is what division
    /// sounds like.
    /// </summary>
    public static ISound Fission(Frequency pitch)
    {
        ISound first = new FmOscillator(pitch, 0.204, end: pitch.Hertz * 1.32, ratio: 2.0, index: 1.8, level: new Level(0.18))
            .RingModulated(pitch.Hertz * 0.74)
            .Shaped(Strike(0.004, 0.20));

        ISound second = new FmOscillator(pitch.Hertz * 1.005, 0.244, end: pitch.Hertz * 1.49, ratio: 2.0, index: 1.8, level: new Level(0.15))
            .RingModulated(pitch.Hertz * 0.76)
            .Shaped(Strike(0.004, 0.24));

        return first.MixedWith(second);
    }

    /// <summary>
    /// A transporter beam. Deep vibrato and ring modulation over a long rising glide: the vibrato
    /// is fast enough to be heard as instability rather than expression, and the ring modulation
    /// keeps the pitch from ever settling. The air behind it opens from almost closed to fully
    /// open, so the sound seems to be arriving from somewhere rather than starting here.
    ///
    /// Fixed rather than pitched, because a transporter is a property of the machine, not of
    /// whatever is being sent.
    /// </summary>
    public static ISound Shimmer()
    {
        const double attack = 0.040, decay = 0.46;

        ISound beam = new FmOscillator(
            420, attack + decay,
            end: 2400, ratio: 1.51, index: 2.2,
            lfo: new Lfo(Waveform.Sine, new Frequency(23), pitchCents: 87),
            level: new Level(0.15))
            .RingModulated(143)
            .Shaped(Strike(attack, decay));

        ISound air = new Noise(NoiseColor.White, new Level(0.08), stream: "scifi-shimmer")
            .Trimmed(0.45)
            .LowPassSwept(2500, 18000)
            .Shaped(Strike(0.050, 0.40));

        return beam.MixedWith(air);
    }

    /// <summary>
    /// A thruster igniting. The filter opening from dull to bright across the noise is the entire
    /// effect — that one gesture is what the ear hears as something accelerating away — and the
    /// rising tone underneath supplies the sense of power behind it rather than just air.
    /// </summary>
    public static ISound Thrust()
    {
        ISound blast = new Noise(NoiseColor.White, new Level(0.24), stream: "scifi-thrust")
            .Trimmed(0.275)
            .LowPassSwept(590, 16200)
            .Shaped(Strike(0.015, 0.26));

        ISound push = new FmOscillator(180, 0.232, end: 1150, ratio: 1.98, index: 1.4, level: new Level(0.13))
            .Shaped(Strike(0.012, 0.22));

        return blast.MixedWith(push);
    }

    /// <summary>
    /// The bed a machine hums under everything else. Built as a whole number of cycles across the
    /// buffer so the end meets the beginning exactly and it can be looped without a click at the
    /// seam — the same trick every engine loop in games is built on.
    ///
    /// Three partials at fixed ratios, the upper two detuned slightly so the stack breathes
    /// instead of sitting still. Pass a lower frequency for something bigger and more ominous.
    /// </summary>
    public static ISound Drone(Frequency pitch, double duration = 2.0)
    {
        // Snap the fundamental so a whole number of cycles fits the loop; the partials are whole
        // multiples of it, so they close the loop too.
        double cycles = System.Math.Max(1, System.Math.Round(pitch.Hertz * duration));
        double fundamental = cycles / duration;

        return Sounds.Mix(
            new Oscillator(Waveform.Sine, fundamental, duration, new Level(0.60)),
            new Oscillator(Waveform.Sine, fundamental * 2, duration, new Level(0.22)),
            new Oscillator(Waveform.Triangle, fundamental * 3, duration, new Level(0.10)));
    }

    /// <summary>
    /// The contour every one-shot here shares: rise, then lose most of the level immediately and
    /// trail off. <see cref="EnvelopeCurve.Steep"/> rather than <see cref="EnvelopeCurve.Exponential"/>
    /// because these are discharges and strikes — they have to leave full level at once or they
    /// come out as beeps.
    /// </summary>
    private static Envelope Strike(double attack, double decay) =>
        new(attack, decay, Level.Silence, 0, EnvelopeCurve.Steep);
}

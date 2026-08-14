namespace RP.Sound.Games;

using RP.Sound.Effects;
using RP.Sound.Physics;
using RP.Sound.Synthesis;

/// <summary>
/// The sound vocabulary of a ship and a fight: the engine that hums under everything, the weapons,
/// the things that hit the hull, the alarms in the cockpit and the voices on the radio.
///
/// Like <see cref="SciFi"/> these are conventions rather than models — no physics says a laser goes
/// "pew" — but they lean on the physical namespace wherever the fiction happens to coincide with
/// reality. A hull taking a hit really is a struck steel plate, so <see cref="Clang"/> is a
/// <see cref="ModalBody"/> and not a stack of hand-tuned sine waves.
///
/// Every preset is deterministic from the render context, so a caller wanting several distinct
/// explosions renders the same description with different <see cref="AudioRenderContext.Seed"/>
/// values rather than passing seeds in here.
/// </summary>
public static class Spacecraft
{
    /// <summary>
    /// The engine bed. Five partials in a fixed ratio — a deep rumble with two upper bands for
    /// turbine body — snapped to a whole number of cycles across the buffer so it loops without a
    /// click. Play it faster or slower to track the throttle.
    /// </summary>
    public static ISound EngineDrone(double duration = 1.0)
    {
        // The partials 48, 72, 96, 144 and 216 Hz are all multiples of 24, so snapping that common
        // base to whole cycles closes the loop for every one of them at once.
        double cycles = System.Math.Max(1, System.Math.Round(24 * duration));
        double baseHertz = cycles / duration;

        (int Multiple, double Weight)[] partials = [(2, 1.00), (3, 0.55), (4, 0.35), (6, 0.20), (9, 0.10)];
        double total = 0;
        foreach ((_, double weight) in partials) total += weight;

        var voices = new ISound[partials.Length];
        for (int i = 0; i < partials.Length; i++)
            voices[i] = new Oscillator(Waveform.Sine, baseHertz * partials[i].Multiple, duration, new Level(partials[i].Weight / total));

        return Sounds.Mix(voices);
    }

    /// <summary>
    /// Weapon fire: a bright tone falling fast, with a square wave under it for edge. The glide is
    /// linear rather than exponential here, which is the wrong choice musically and the right one
    /// for a weapon — it lurches downward instead of sliding, and the lurch is the aggression.
    /// </summary>
    public static ISound Zap(double duration = 0.12)
    {
        ISound tone = new FmOscillator(900, duration, end: 300, exponentialSweep: false, level: new Level(0.6));
        ISound edge = new FmOscillator(900, duration, end: 300, carrier: Waveform.Square, exponentialSweep: false, level: new Level(0.24));
        return tone.MixedWith(edge).Shaped(Fall(duration));
    }

    /// <summary>
    /// A round finding armour: a bright noise crack with just enough tone under it to have a pitch.
    /// Short and dry, so it reads as a hit rather than as an explosion.
    /// </summary>
    public static ISound Impact(double duration = 0.16)
    {
        ISound crack = new Noise(NoiseColor.White, new Level(0.7), stream: "spacecraft-impact")
            .Trimmed(duration)
            .HighPassed(1200);
        ISound body = new Oscillator(Waveform.Sine, 520, duration, new Level(0.3));

        // Trimmed back so noise peaks landing on top of the tone still leave headroom: at full
        // level the two sum past full scale on the loudest transients.
        return crack.MixedWith(body).Shaped(Fall(duration, attack: 0.001)).Amplified(new Level(0.55));
    }

    /// <summary>
    /// Metal on metal — a hull scraping or slamming something solid. This is the one weapon-adjacent
    /// sound that is genuinely physical, so it is modelled rather than composed: a struck steel
    /// plate, whose modes are inharmonic and decay at their own rates because that is what a real
    /// plate does.
    /// </summary>
    public static ISound Clang(double duration = 0.45) =>
        new Impact(new ModalBody(Material.Steel, 0.6), velocity: 6, strikerHardness: 0.9)
            .Trimmed(duration)
            .Amplified(new Level(0.7));

    /// <summary>
    /// A shield shedding energy: noise ring-modulated by a falling carrier. Ring modulation strips
    /// the fundamental, so the result fizzes without ever settling on a pitch — deliberately
    /// nothing like <see cref="Impact"/>, so the ear can tell shield from armour without being told.
    /// </summary>
    public static ISound ShieldFizz(double duration = 0.22) =>
        new Noise(NoiseColor.White, new Level(0.7), stream: "spacecraft-fizz")
            .Trimmed(duration)
            .LowPassed(6000)
            .RingModulated(1150)
            .Shaped(Fall(duration, attack: 0.002));

    /// <summary>
    /// A cockpit alarm: <paramref name="beeps"/> pips at <paramref name="frequencyHz"/>. One
    /// generator covers the whole alarm family by varying pitch and count — missile lock high and
    /// fast, collision mid, hull critical low and slow — which is what keeps them recognisable as
    /// members of one set while staying distinguishable from each other.
    /// </summary>
    public static ISound Warning(Frequency frequencyHz, int beeps = 3, double beepSeconds = 0.07, double gapSeconds = 0.05)
    {
        if (beeps < 1) throw new ArgumentOutOfRangeException(nameof(beeps), beeps, "A warning needs at least one pip.");

        // Soft edges on each pip. Without them the abrupt start and stop is a click, which reads as
        // a fault in the game rather than an alarm in the fiction.
        // An alarm is a sustained square-ish tone at full envelope, so unlike the one-shots here it
        // sits at its peak for its whole length. It therefore needs its headroom taken off the top
        // rather than earned back by a decay.
        ISound pip = new Oscillator(Waveform.Sine, frequencyHz, beepSeconds, new Level(0.7))
            .MixedWith(new Oscillator(Waveform.Square, frequencyHz, beepSeconds, new Level(0.3)))
            .Shaped(new Envelope(0.004, 0, Level.Unity, 0.004, EnvelopeCurve.Linear))
            .Amplified(new Level(0.55));

        return pip.Repeated(beeps, beepSeconds + gapSeconds);
    }

    /// <summary>
    /// Radio chatter: a squelch-framed burst of syllable-rhythm noise — a voice on a distant channel
    /// with no actual words in it. The cadence is what sells it, so the syllables are laid out from
    /// <paramref name="variant"/> arithmetically rather than randomly, which means a given call sign
    /// always "speaks" with the same rhythm.
    /// </summary>
    public static ISound Chatter(int variant = 0, double duration = 0.9)
    {
        int syllables = 4 + System.Math.Abs(variant) % 5;
        double pitch = 95 + System.Math.Abs(variant) % 7 * 10;

        var bursts = new List<(double Start, ISound Sound)>();
        for (int i = 0; i < syllables; i++)
        {
            // Nudge each syllable off the grid by an amount that depends on both its index and the
            // variant, so no two call signs land on the same rhythm and none of them sound metronomic.
            double jitter = ((i * 7 + variant * 3) % 5 - 2) * 0.012;
            double start = duration * (i + 0.5) / syllables + jitter;
            double length = 0.055 + (i * 3 + variant) % 4 * 0.015;
            double level = 0.55 + (i * 5 + variant) % 3 * 0.15;

            // A glottal buzz amplitude-modulating band-passed noise: telephone-band, voice-shaped,
            // and entirely wordless.
            ISound syllable = new Noise(NoiseColor.White, new Level(level), stream: $"chatter-{variant}-{i}")
                .Trimmed(length)
                .BandPassed(1400, q: 1.4)
                .RingModulated(pitch, mix: 0.6)
                .Shaped(new Envelope(length * 0.3, 0, Level.Unity, length * 0.7, EnvelopeCurve.Linear));

            bursts.Add((System.Math.Max(0, start), syllable));
        }

        // The squelch clicks that frame a transmission — the radio opening and closing.
        ISound squelch = new Noise(NoiseColor.White, new Level(0.5), stream: $"squelch-{variant}")
            .Trimmed(0.012)
            .Shaped(Fall(0.012, attack: 0.0005));

        bursts.Add((0, squelch));
        bursts.Add((duration - 0.012, squelch));

        // Band-passing white noise to a telephone band throws most of its energy away, so the
        // make-up gain here is large. Chatter still sits below the rest of the palette on purpose:
        // it is background colour, and a radio that competes with the guns is a nuisance.
        return new Timeline(bursts).Trimmed(duration).Amplified(new Level(0.9));
    }

    /// <summary>
    /// A missile leaving the rail: the motor's noise opening from a dull thump to a bright roar as
    /// it spools, under a swell that fades as it goes away. The low kick at ignition is what gives
    /// it weight — without it the sound is air with nothing behind it.
    /// </summary>
    public static ISound MissileLaunch(double duration = 0.7)
    {
        ISound motor = new Noise(NoiseColor.White, new Level(0.8), stream: "spacecraft-missile")
            .Trimmed(duration)
            .LowPassSwept(400, 7000)
            .Shaped(new Envelope(duration * 0.15, duration * 0.85, Level.Silence, 0, EnvelopeCurve.Steep));

        ISound kick = new FmOscillator(70, 0.05, end: 45, level: new Level(0.8))
            .Shaped(Fall(0.05, attack: 0.001));

        return motor.MixedWith(kick).Amplified(new Level(0.6));
    }

    /// <summary>
    /// A ruptured line venting: broadband hiss swelling and trailing off. High-passed toward a
    /// "sss" rather than a rumble, because the brightness is what makes it read as escaping gas
    /// under pressure instead of as distant wind.
    /// </summary>
    public static ISound Hiss(double duration = 1.1) =>
        new Noise(NoiseColor.White, new Level(0.8), stream: "spacecraft-hiss")
            .Trimmed(duration)
            .HighPassed(900)
            .Shaped(new Envelope(duration * 0.2, duration * 0.8, Level.Silence, 0, EnvelopeCurve.Steep))
            .Amplified(new Level(0.4));

    /// <summary>
    /// The hit marker: a tiny bright tick confirming the player's own round connected. Deliberately
    /// dry, instant and unpitched-sounding, so it reads as interface rather than as something
    /// happening out in the world.
    /// </summary>
    public static ISound HitTick(double duration = 0.05) =>
        new Oscillator(Waveform.Sine, 1900, duration, new Level(0.7))
            .MixedWith(new Oscillator(Waveform.Sine, 2850, duration, new Level(0.3)))
            .Shaped(Fall(duration, attack: 0.0005))
            .Amplified(new Level(0.5));

    /// <summary>
    /// An explosion: filtered noise over a sinking sub boom. The boom falling in pitch as it decays
    /// is what makes it read as a large release of energy rather than as a burst of static — big
    /// things get lower as they lose energy.
    /// </summary>
    public static ISound Explosion(double duration = 1.1)
    {
        ISound blast = new Noise(NoiseColor.White, new Level(0.7), stream: "spacecraft-explosion")
            .Trimmed(duration)
            .LowPassed(2200);

        ISound boom = new FmOscillator(60, duration, end: 35, exponentialSweep: false, level: new Level(0.5));

        return blast.MixedWith(boom).Shaped(Fall(duration, attack: 0.002)).Amplified(new Level(0.8));
    }

    /// <summary>The contour these one-shots share: arrive at once, lose most of the level immediately, trail off.</summary>
    private static Envelope Fall(double duration, double attack = 0.002) =>
        new(attack, System.Math.Max(0, duration - attack), Level.Silence, 0, EnvelopeCurve.Steep);
}

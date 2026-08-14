using RP.Sound.Instruments;
using RP.Sound.IO;
using RP.Sound.Synthesis;

namespace RP.Sound.Music;

/// <summary>
/// The General MIDI level 1 sound set (MMA, 1991) voiced with this library's instruments. GM
/// standardised two things a bare MIDI file leaves open: which of the 128 programs is which
/// instrument, and that channel 10 (9 counting from zero) is percussion with fixed key-to-drum
/// assignments. This class maps both onto the physically modelled voices the library already has,
/// by instrument <em>family</em> — GM groups its 128 programs into 16 families of 8, and the
/// family (piano, organ, guitar, bass, brass…) is what decides the synthesis model; the variants
/// within a family differ less than our modelled voices differ from the "real" thing anyway.
/// <para>
/// Velocity becomes amplitude through the square law (gain = (velocity/127)²), the common
/// synthesizer convention: perceived loudness tracks roughly the square of velocity because key
/// velocity maps to hammer/pluck energy, not directly to pressure (Dannenberg, "The Interpretation
/// of MIDI Velocity", ICMC 2006 surveys the practice).
/// </para>
/// </summary>
public static class GeneralMidi
{
    /// <summary>The gain a MIDI velocity maps to: (velocity/127)², the square-law convention.</summary>
    public static Level VelocityLevel(int velocity) => new(System.Math.Pow(System.Math.Clamp(velocity, 0, 127) / 127.0, 2));

    /// <summary>
    /// The voice for a melodic note: the GM program family chooses the instrument, the note
    /// supplies pitch, duration and level. Never null — every family has a voice, with the
    /// synthesizer as the honest stand-in for families the library has no physical model for.
    /// </summary>
    public static ISound Voice(int program, Frequency pitch, double duration, Level level)
    {
        if (program is < 0 or > 127)
            throw new ArgumentOutOfRangeException(nameof(program), program, "A General MIDI program is 0–127.");

        return (program / 8) switch
        {
            // Pianos have no model here; the Pluck patch's sharp attack and exponential decay is
            // the nearest synthesis archetype.
            0 => new Synthesizer(SynthPatch.Pluck, pitch, System.Math.Max(duration, 0.5), level),
            1 => ChromaticPercussion(program, pitch, level),
            2 => new Organ(pitch, duration, level: level),
            3 => new Guitar(pitch, System.Math.Max(duration, 0.7), level: level),
            4 => new BassGuitar(pitch, System.Math.Max(duration, 0.6), level: level),
            5 or 6 => new SynthPad(pitch, duration, level),                          // Strings and ensembles: sustained, slow-attack.
            7 => new Brass(pitch, duration, level: level),
            8 or 9 => new Flute(pitch, duration, level: level),                      // Reeds and pipes: the wind model covers both families.
            10 => new Synthesizer(SynthPatch.Lead, pitch, duration, level),
            11 or 12 => new SynthPad(pitch, duration, level),                        // Synth pads and effects.
            13 => new Guitar(pitch, System.Math.Max(duration, 0.6), damping: 0.35, level: level), // Ethnic pluck (banjo, shamisen…): a heavily damped string.
            14 => Mallet.Marimba(pitch, level),                                      // Percussive family.
            _ => new Synthesizer(SynthPatch.Pad, pitch, duration, level),            // Sound effects: an unpitched pad beats silence.
        };
    }

    private static ISound ChromaticPercussion(int program, Frequency pitch, Level level) => program switch
    {
        11 or 12 => Mallet.Marimba(pitch, level),      // Vibraphone, marimba.
        13 => Mallet.Xylophone(pitch, level),
        _ => Mallet.Glockenspiel(pitch, level),        // Celesta, glockenspiel, music box, tubular bells: free-bar/bell timbres.
    };

    /// <summary>
    /// The voice for a channel-10 percussion key, from the GM percussion map (keys 35–81), or
    /// null for keys the library has no drum for (claves, whistles, congas…) — a documented gap:
    /// silence is more honest than a wrong drum. Toms descend in pitch with their key exactly as
    /// the map orders them, low floor tom (41) to high tom (50).
    /// </summary>
    public static ISound? Percussion(int noteNumber, Level level) => noteNumber switch
    {
        35 or 36 => new KickDrum(level: level),
        37 => new SnareDrum(snappy: 0.2, decay: 0.08, level: level),   // Side stick: the shell without the rattle.
        38 or 40 => new SnareDrum(level: level),
        39 => new SnareDrum(pitch: 220, snappy: 1, decay: 0.15, level: level), // Hand clap: all rattle.
        41 or 43 or 45 or 47 or 48 or 50 => new TomDrum(TomPitch(noteNumber), level: level),
        42 or 44 => HiHat.Closed(level),
        46 => HiHat.Open(level),
        49 or 55 or 57 => new Cymbal(level: level),                    // Crashes and splash.
        51 or 53 or 59 => new Cymbal(decay: 1.2, level: level),        // Rides: shorter, more ping than wash.
        _ => null,
    };

    /// <summary>The GM tom keys 41–50 spread over the drum's usable range, low to high.</summary>
    private static Frequency TomPitch(int noteNumber) => noteNumber switch
    {
        41 => 80, 43 => 96, 45 => 115, 47 => 135, 48 => 155, _ => 180,
    };
}

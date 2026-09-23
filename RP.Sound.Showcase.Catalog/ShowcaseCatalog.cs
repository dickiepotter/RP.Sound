using RP.Sound.Ambience;
using RP.Sound.Effects;
using RP.Sound.Instruments;
using RP.Sound.IO;
using RP.Sound.Music;
using RP.Sound.Physics;
using RP.Sound.Synthesis;

namespace RP.Sound.Showcase;

/// <summary>
/// Every sound the showcase can play, keyed by its path under <c>/api/</c>. The table is the whole
/// demo: the ASP.NET server maps it onto HTTP, and the WebAssembly build calls it directly from the
/// browser, so the two hosts cannot drift apart.
/// </summary>
/// <remarks>
/// Every entry renders a deterministic buffer from an immutable description and returns it as WAV.
/// <c>seed</c> re-rolls the random character of a sound without changing its physics.
/// </remarks>
public static partial class ShowcaseCatalog
{
    private static readonly Dictionary<string, Func<DemoParameters, byte[]>> Renderers =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The paths the catalog can render, such as <c>physics/impact</c>.</summary>
    public static IEnumerable<string> Paths => Renderers.Keys;

    /// <summary>
    /// Renders the sound at <paramref name="path"/> to WAV bytes, or returns <see langword="false"/>
    /// when there is no such sound. A malformed parameter throws <see cref="FormatException"/>.
    /// </summary>
    public static bool TryRender(string path, DemoParameters parameters, out byte[] wav)
    {
        if (Renderers.TryGetValue(path.Trim('/'), out var render))
        {
            wav = render(parameters);
            return true;
        }
        wav = [];
        return false;
    }

    /// <summary>The preset names the client UI offers in its drop-downs.</summary>
    public static object Meta() => new
    {
        materials = Material.Presets.Select(m => m.Name).ToArray(),
        surfaces = ParticleSurface.PresetNames,
        moods = Mood.Presets.Select(p => p.Name).ToArray(),
    };

    private static AudioRenderContext Context(int seed) => new(SampleRate: 44100, Seed: seed);

    private static AudioRenderContext Context(DemoParameters q) => Context(q.Int("seed", 0));

    private static double ClampDuration(double duration) => Math.Clamp(duration, 0.1, 30);

    private static double MusicLength(double natural) => Math.Min(natural, 40);

    private static byte[] Wav(AudioBuffer buffer) => WavFile.ToBytes(buffer.SoftClipped());

    private static void Add(string path, Func<DemoParameters, byte[]> render) => Renderers.Add(path, render);

    static ShowcaseCatalog()
    {
        // ---- Physics: Gaver's taxonomy — impact, scrape, roll — plus what falls out of it ----
        Add("physics/impact", q =>
            Wav(new Impact(new ModalBody(Material.FromName(q.String("material", "steel")), q.Double("size", 0.4)),
                    q.Double("velocity", 3), strikerHardness: q.Double("hardness", 0.7))
                .Render(Context(q))));

        Add("physics/drop", q =>
            Wav(BounceSequence.FromDrop(new ModalBody(Material.FromName(q.String("material", "wood")), q.Double("size", 0.25)),
                    q.Double("height", 1.5), gravity: q.Double("gravity", 9.81))
                .Render(Context(q))));

        Add("physics/scrape", q =>
            Wav(new Scrape(new ModalBody(Material.FromName(q.String("material", "stone")), 0.5),
                    q.Double("speed", 1), ClampDuration(q.Double("duration", 2)), q.Double("force", 5), q.Double("roughness", 0.5))
                .Render(Context(q))));

        Add("physics/roll", q =>
            Wav(new Rolling(new ModalBody(Material.FromName(q.String("material", "wood")), 0.3),
                    q.Double("radius", 0.1), q.Double("speed", 2), ClampDuration(q.Double("duration", 3)))
                .Render(Context(q))));

        Add("physics/surface", q =>
            Wav(ParticleSurface.FromName(q.String("name", "gravel"), q.Double("energy", 1)).Render(Context(q))));

        Add("physics/footsteps", q =>
        {
            string surface = q.String("surface", "gravel");
            double speed = q.Double("speed", 1.4), weight = q.Double("weight", 75), duration = ClampDuration(q.Double("duration", 4));
            Footsteps steps = Material.TryFromName(surface, out Material material)
                ? new Footsteps(material, speed, duration, weight)
                : new Footsteps(ParticleSurface.FromName(surface), speed, duration, weight);
            return Wav(steps.Render(Context(q)));
        });

        Add("physics/whoosh", q =>
            Wav(new Whoosh(q.Double("speed", 20), q.Double("size", 0.05), ClampDuration(q.Double("duration", 1.2)), q.Bool("passBy", true))
                .Render(Context(q))));

        // ---- Synthesis ----
        Add("synth/pluck", q =>
            Wav(new PluckedString(Frequency.FromNote(q.String("note", "A3")), 2.5, q.Double("damping", 0.1)).Render(Context(q))));

        // ---- Ambience ----
        Add("ambience/wind", q =>
            Wav(new Wind(q.Double("strength", 0.5), q.Double("gustiness", 0.5)).Render(Context(q), ClampDuration(q.Double("duration", 8)))));

        Add("ambience/rain", q =>
            Wav(new Rain(q.Double("intensity", 0.5), q.Double("hardness", 0.4)).Render(Context(q), ClampDuration(q.Double("duration", 8)))));

        Add("ambience/fire", q =>
            Wav(new Fire(q.Double("intensity", 0.5)).Render(Context(q), ClampDuration(q.Double("duration", 8)))));

        Add("ambience/thunder", q =>
            Wav(new Thunder(q.Double("distance", 2000)).Render(Context(q))));

        // ---- Mood and tension ----
        Add("music/drone", q =>
            Wav(Drone.ForMood(Mood.FromName(q.String("mood", "calm"))).Render(Context(q), ClampDuration(q.Double("duration", 8)))));

        Add("music/shepard", q =>
            Wav(new ShepardTone(q.Double("cycle", 8), q.Int("direction", 1)).Render(Context(q), ClampDuration(q.Double("duration", 12)))));

        Add("music/riser", q =>
            Wav(new Riser(ClampDuration(q.Double("duration", 4)), q.Double("intensity", 0.7)).Render(Context(q))));

        Add("music/stinger", q =>
            Wav(new Stinger(Mood.FromName(q.String("mood", "horror"))).Render(Context(q))));

        Add("music/heartbeat", q =>
            Wav(new Heartbeat(q.Double("bpm", 90)).Render(Context(q), ClampDuration(q.Double("duration", 6)))));

        // ---- The flagship: a full generative scene, layered and ducked by the mixer ----
        Add("scene", q =>
        {
            var scene = new GenerativeScene(Mood.FromName(q.String("mood", "horror")),
                q.Bool("wind", true), q.Bool("rain", false), q.Bool("fire", false));
            return WavFile.ToBytes(scene.Render(Context(q), ClampDuration(q.Double("duration", 15))));
        });

        // ---- Instruments: the rhythm-section voices ----
        Add("instruments/kick", q =>
            Wav(new KickDrum(q.Double("pitch", 50), q.Double("punch", 0.6), q.Double("decay", 0.5)).Render(Context(q))));

        Add("instruments/snare", q =>
            Wav(new SnareDrum(q.Double("pitch", 190), q.Double("snappy", 0.7), q.Double("decay", 0.25)).Render(Context(q))));

        Add("instruments/hihat", q =>
            Wav(new HiHat(q.Bool("open", false)).Render(Context(q))));

        Add("instruments/tom", q =>
            Wav(new TomDrum(q.Double("pitch", 110), q.Double("decay", 0.4)).Render(Context(q))));

        Add("instruments/cymbal", q =>
            Wav(new Cymbal(q.Double("decay", 2.5)).Render(Context(q))));

        Add("instruments/bass", q =>
            Wav(new BassGuitar(Frequency.FromNote(q.String("note", "E1")), 2, q.Double("tone", 0.4)).Render(Context(q))));

        Add("instruments/guitar", q =>
            Wav(new Guitar(Frequency.FromNote(q.String("note", "A3")), 2.5, q.Double("damping", 0.15), q.Double("pickPosition", 0.2))
                .Render(Context(q))));

        Add("instruments/powerchord", q =>
            Wav(Guitar.PowerChord(Frequency.FromNote(q.String("note", "E2")), 2.5)
                .Distorted(Math.Clamp(q.Double("drive", 5), 1, 12), Level.FromDecibels(-3))
                .Render(Context(q), 2.5)));

        Add("instruments/mallet", q =>
        {
            Frequency pitch = Frequency.FromNote(q.String("note", "C4"));
            Mallet mallet = q.String("kind", "marimba").ToLowerInvariant() switch
            {
                "xylophone" => Mallet.Xylophone(pitch),
                "glockenspiel" => Mallet.Glockenspiel(pitch),
                _ => Mallet.Marimba(pitch),
            };
            return Wav(mallet.Render(Context(q)));
        });

        Add("instruments/organ", q =>
            Wav(new Organ(Frequency.FromNote(q.String("note", "C3")), ClampDuration(q.Double("duration", 1.5)),
                    q.String("registration", "888000000"))
                .Render(Context(q))));

        Add("instruments/flute", q =>
            Wav(new Flute(Frequency.FromNote(q.String("note", "A4")), ClampDuration(q.Double("duration", 1.5)), q.Double("breathiness", 0.3))
                .Render(Context(q))));

        Add("instruments/brass", q =>
            Wav(new Brass(Frequency.FromNote(q.String("note", "C3")), ClampDuration(q.Double("duration", 1.2)), q.Double("brightness", 0.7))
                .Render(Context(q))));

        // ---- The synthesizer: every patch parameter exposed, plus the preset shortcuts ----
        Add("synth/play", q =>
        {
            static Waveform Parse(string name) => Enum.TryParse(name, ignoreCase: true, out Waveform w) ? w : Waveform.Sawtooth;
            var amplitude = Envelope.Adsr(
                Math.Clamp(q.Double("attack", 0.01), 0, 5),
                Math.Clamp(q.Double("decay", 0.1), 0, 5),
                Level.FromDecibels(Math.Min(0, q.Double("sustainDb", -3))),
                Math.Clamp(q.Double("release", 0.2), 0, 5));
            var patch = new SynthPatch(
                Parse(q.String("osc1", "sawtooth")), Parse(q.String("osc2", "sawtooth")),
                oscillator2DetuneCents: Math.Clamp(q.Double("detune", 7), -2400, 2400),
                oscillatorMix: Math.Clamp(q.Double("mix", 0.5), 0, 1),
                noiseMix: Math.Clamp(q.Double("noise", 0), 0, 1),
                filterCutoff: Math.Clamp(q.Double("cutoff", 2000), 20, 12000),
                filterResonance: Math.Clamp(q.Double("resonance", 0.9), 0.05, 20),
                filterEnvelopeOctaves: Math.Clamp(q.Double("filterOctaves", 2), 0, 8),
                amplitudeEnvelope: amplitude,
                lfo: new Lfo(Parse(q.String("lfoWave", "sine")), Math.Clamp(q.Double("lfoRate", 5), 0, 30),
                    pitchCents: Math.Clamp(q.Double("vibrato", 0), 0, 100),
                    cutoffOctaves: Math.Clamp(q.Double("wobble", 0), 0, 4),
                    tremoloDepth: Math.Clamp(q.Double("tremolo", 0), 0, 1)));
            return Wav(new Synthesizer(patch, Frequency.FromNote(q.String("note", "A3")), ClampDuration(q.Double("duration", 1.5)))
                .Render(Context(q)));
        });

        Add("synth/preset", q =>
        {
            SynthPatch patch = q.String("name", "bass").ToLowerInvariant() switch
            {
                "lead" => SynthPatch.Lead,
                "pluck" => SynthPatch.Pluck,
                "pad" => SynthPatch.Pad,
                "wobble" => SynthPatch.Wobble(Math.Clamp(q.Double("wobbleRate", 4.67), 0.5, 20)),
                _ => SynthPatch.Bass,
            };
            return Wav(new Synthesizer(patch, Frequency.FromNote(q.String("note", "A2")), ClampDuration(q.Double("duration", 1.5)))
                .Render(Context(q)));
        });

        // ---- Background music: the genre generators (each renders its natural loop + ring-out) ----
        Add("music/genre/blues", q =>
        {
            var track = new BluesTrack(Frequency.FromNote(q.String("root", "E2")),
                Math.Clamp(q.Int("choruses", 1), 1, 2), Math.Clamp(q.Double("bpm", 96), 60, 160));
            return Wav(track.Render(Context(q), MusicLength(track.Duration)));
        });

        Add("music/genre/rock", q =>
        {
            var track = new RockTrack(Frequency.FromNote(q.String("root", "E2")),
                Math.Clamp(q.Int("bars", 8), 4, 16), Math.Clamp(q.Double("bpm", 120), 90, 160));
            return Wav(track.Render(Context(q), MusicLength(track.Duration)));
        });

        Add("music/genre/dubstep", q =>
        {
            var track = new DubstepTrack(Frequency.FromNote(q.String("root", "A1")),
                Math.Clamp(q.Int("bars", 8), 4, 16), Math.Clamp(q.Double("bpm", 140), 135, 145));
            return Wav(track.Render(Context(q), MusicLength(track.Duration)));
        });

        Add("music/genre/house", q =>
        {
            var track = new HouseTrack(Frequency.FromNote(q.String("root", "A2")),
                Math.Clamp(q.Int("bars", 8), 4, 16), Math.Clamp(q.Double("bpm", 124), 118, 130));
            return Wav(track.Render(Context(q), MusicLength(track.Duration)));
        });

        Add("music/genre/electronica", q =>
        {
            var track = new ElectronicaTrack(Frequency.FromNote(q.String("root", "A2")),
                Math.Clamp(q.Int("bars", 8), 4, 16), Math.Clamp(q.Double("bpm", 85), 60, 110));
            return Wav(track.Render(Context(q), MusicLength(track.Duration)));
        });

        // ---- Music file formats: both demos are authored in code, round-tripped through the actual
        // file encoder and decoder (ToBytes → Read), then performed — proving read AND write work. ----
        Add("formats/midi", q =>
        {
            MidiSequence sequence = DemoSongs.Midi(Math.Clamp(q.Double("bpm", 110), 70, 160))
                .Transposed(Math.Clamp(q.Int("transpose", 0), -12, 12));
            var song = new MidiSong(MidiFile.Read(MidiFile.ToBytes(sequence)));
            return Wav(song.Render(Context(q), MusicLength(song.Duration)));
        });

        Add("formats/mod", q =>
        {
            ModModule module = ModFile.Read(ModFile.ToBytes(DemoSongs.Mod(Math.Clamp(q.Int("speed", 6), 3, 10))));
            var song = new ModSong(module);
            return Wav(song.Render(Context(q), MusicLength(song.LoopDuration)));
        });

        // ---- The science-fiction palette, in its own file ----
        AddSciFi();
    }
}

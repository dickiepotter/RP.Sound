<script>
  import SoundCard from './lib/SoundCard.svelte';
  import SynthCard from './lib/SynthCard.svelte';

  const materials = ['steel', 'glass', 'wood', 'stone', 'plastic', 'rubber', 'ceramic', 'ice'];
  const surfaces = ['gravel', 'sand', 'leaves', 'snow'];
  const allSurfaces = [...surfaces, ...materials];
  const moods = ['calm', 'fun', 'fastpaced', 'anticipation', 'threat', 'horror', 'sad', 'triumphant'];
  const lowNotes = ['E1', 'A1', 'D2', 'E2', 'G2', 'A2'];
  const midNotes = ['E2', 'A2', 'C3', 'E3', 'A3', 'C4', 'E4'];
  const highNotes = ['C4', 'E4', 'G4', 'A4', 'C5', 'E5', 'G5'];
  const registrations = ['888000000', '888800000', '888888888', '808808008'];
  const keys = ['E1', 'G1', 'A1', 'C2', 'D2', 'E2', 'G2', 'A2'];
</script>

<header>
  <h1><span>RP.Sound</span> — physically-based &amp; generative game audio</h1>
  <p>
    Every sound on this page is synthesised on the server from an immutable description — no
    samples, no recordings. Materials use real physical constants (density, stiffness, damping);
    impacts, bounces, scrapes and rolls follow the actual mechanics; moods are coordinates in the
    valence–arousal–tension space psychology uses to describe emotion. Same seed ⇒ identical audio;
    re-roll for a fresh take on the same physics.
  </p>
</header>

<main>
  <section>
    <h2>Contact physics — impact, bounce, scrape, roll</h2>
    <p>
      Gaver's ecological taxonomy: almost everything a solid object does audibly is one of these.
      The material and size derive the resonant modes; velocity, gravity and hardness do the rest.
    </p>
    <div class="grid">
      <SoundCard
        title="Impact"
        blurb="A struck object: the strike's velocity sets loudness, striker hardness sets brightness, material and size set the ring."
        endpoint="/api/physics/impact"
        selects={[{ name: 'material', label: 'material', options: materials, value: 'steel' }]}
        params={[
          { name: 'size', label: 'size', min: 0.05, max: 1.5, step: 0.05, value: 0.4, unit: ' m' },
          { name: 'velocity', label: 'velocity', min: 0.2, max: 12, step: 0.2, value: 3, unit: ' m/s' },
          { name: 'hardness', label: 'striker', min: 0.05, max: 1, step: 0.05, value: 0.7 },
        ]} />
      <SoundCard
        title="Drop &amp; bounce"
        blurb="Pure projectile physics: intervals and loudness both shrink by the restitution each bounce. Try lunar gravity."
        endpoint="/api/physics/drop"
        selects={[{ name: 'material', label: 'material', options: materials, value: 'wood' }]}
        params={[
          { name: 'size', label: 'size', min: 0.05, max: 0.8, step: 0.05, value: 0.25, unit: ' m' },
          { name: 'height', label: 'drop height', min: 0.1, max: 5, step: 0.1, value: 1.5, unit: ' m' },
          { name: 'gravity', label: 'gravity', min: 1.6, max: 25, step: 0.1, value: 9.81, unit: ' m/s²' },
        ]} />
      <SoundCard
        title="Scrape"
        blurb="Drag speed × surface bump density = the hiss's pitch; the scraped body's modes colour it."
        endpoint="/api/physics/scrape"
        selects={[{ name: 'material', label: 'material', options: materials, value: 'stone' }]}
        params={[
          { name: 'speed', label: 'speed', min: 0.1, max: 4, step: 0.1, value: 1, unit: ' m/s' },
          { name: 'roughness', label: 'roughness', min: 0, max: 1, step: 0.05, value: 0.5 },
          { name: 'force', label: 'force', min: 1, max: 30, step: 1, value: 5, unit: ' N' },
        ]} />
      <SoundCard
        title="Rolling"
        blurb="One micro-impact per surface bump — the rate falls out of v/2πr — over a speed-dependent rumble."
        endpoint="/api/physics/roll"
        selects={[{ name: 'material', label: 'material', options: materials, value: 'wood' }]}
        params={[
          { name: 'radius', label: 'radius', min: 0.02, max: 0.5, step: 0.01, value: 0.1, unit: ' m' },
          { name: 'speed', label: 'speed', min: 0.2, max: 10, step: 0.2, value: 2, unit: ' m/s' },
        ]} />
      <SoundCard
        title="Whoosh"
        blurb="Vortex shedding at the Strouhal frequency f = 0.2·v/d, swept for the pass-by Doppler cue."
        endpoint="/api/physics/whoosh"
        params={[
          { name: 'speed', label: 'speed', min: 2, max: 80, step: 1, value: 20, unit: ' m/s' },
          { name: 'size', label: 'size', min: 0.01, max: 0.5, step: 0.01, value: 0.05, unit: ' m' },
          { name: 'duration', label: 'duration', min: 0.4, max: 3, step: 0.1, value: 1.2, unit: ' s' },
        ]}
        checks={[{ name: 'passBy', label: 'pass-by sweep', value: true }]} />
      <SoundCard
        title="Plucked string"
        blurb="Karplus–Strong: a noise burst in a filtered delay line. The classic teaching physical model."
        endpoint="/api/synth/pluck"
        selects={[{ name: 'note', label: 'note', options: ['E2', 'A2', 'D3', 'A3', 'E4', 'A4'], value: 'A3' }]}
        params={[{ name: 'damping', label: 'damping', min: 0, max: 1, step: 0.05, value: 0.1 }]} />
    </div>
  </section>

  <section>
    <h2>Granular surfaces &amp; movement</h2>
    <p>
      Loose surfaces (Cook's PhISEM model: stochastic grain collisions following the system's
      energy) and footsteps whose cadence comes from actual walking speed.
    </p>
    <div class="grid">
      <SoundCard
        title="Surface crunch"
        blurb="One footfall's worth of energy into a bed of grains; the crunch thins as the energy dies."
        endpoint="/api/physics/surface"
        selects={[{ name: 'name', label: 'surface', options: surfaces, value: 'gravel' }]}
        params={[{ name: 'energy', label: 'energy', min: 0.1, max: 1, step: 0.05, value: 1 }]} />
      <SoundCard
        title="Footsteps"
        blurb="Heel then toe, alternating feet, cadence = speed ÷ stride. Hard floors ring; loose ground crunches."
        endpoint="/api/physics/footsteps"
        selects={[{ name: 'surface', label: 'surface', options: allSurfaces, value: 'gravel' }]}
        params={[
          { name: 'speed', label: 'speed', min: 0.5, max: 4, step: 0.1, value: 1.4, unit: ' m/s' },
          { name: 'weight', label: 'weight', min: 40, max: 140, step: 5, value: 75, unit: ' kg' },
          { name: 'duration', label: 'duration', min: 2, max: 10, step: 1, value: 4, unit: ' s' },
        ]} />
    </div>
  </section>

  <section>
    <h2>Ambience</h2>
    <p>
      Endless procedural beds — ask for any duration. Wind is gusts before it is hiss; rain is a
      bed plus audible drops; fire is roar + hiss + crackle; thunder is distance made audible.
    </p>
    <div class="grid">
      <SoundCard
        title="Wind"
        blurb="Noise through a resonant band, ridden by a slow wandering gust envelope — with a whistle in the gale."
        endpoint="/api/ambience/wind"
        params={[
          { name: 'strength', label: 'strength', min: 0, max: 1, step: 0.05, value: 0.5 },
          { name: 'gustiness', label: 'gustiness', min: 0, max: 1, step: 0.05, value: 0.5 },
          { name: 'duration', label: 'duration', min: 2, max: 20, step: 1, value: 8, unit: ' s' },
        ]} />
      <SoundCard
        title="Rain"
        blurb="A Poisson scatter of individually audible drops over the fused wash of the countless distant ones."
        endpoint="/api/ambience/rain"
        params={[
          { name: 'intensity', label: 'intensity', min: 0, max: 1, step: 0.05, value: 0.5 },
          { name: 'hardness', label: 'surface', min: 0, max: 1, step: 0.05, value: 0.4 },
          { name: 'duration', label: 'duration', min: 2, max: 20, step: 1, value: 8, unit: ' s' },
        ]} />
      <SoundCard
        title="Fire"
        blurb="Roar, hiss and crackle in an intensity-dependent balance — embers are all crackle, a blaze is all roar."
        endpoint="/api/ambience/fire"
        params={[
          { name: 'intensity', label: 'intensity', min: 0, max: 1, step: 0.05, value: 0.5 },
          { name: 'duration', label: 'duration', min: 2, max: 20, step: 1, value: 8, unit: ' s' },
        ]} />
      <SoundCard
        title="Thunder"
        blurb="Air absorption eats the highs with every metre: close cracks, far rumbles. Distance is the only slider."
        endpoint="/api/ambience/thunder"
        params={[{ name: 'distance', label: 'distance', min: 100, max: 10000, step: 100, value: 2000, unit: ' m' }]} />
    </div>
  </section>

  <section>
    <h2>Instruments — a rhythm section from first principles</h2>
    <p>
      Every voice states its synthesis model and its source: drums are swept sines, filtered noise
      and inharmonic square stacks (the analogue drum-machine recipes); strings are Karplus–Strong
      with the Jaffe–Smith extensions; mallets are tuned-bar modal synthesis (marimba carved to
      1:4:10, xylophone to 1:3); the organ is Hammond drawbar additive; brass follows Risset's law
      that brightness rides loudness.
    </p>
    <div class="grid">
      <SoundCard
        title="Kick drum"
        blurb="A sine sweeping fast onto its resting pitch + a click — the 808/909 recipe, with the membrane-tension physics to back it."
        endpoint="/api/instruments/kick"
        params={[
          { name: 'pitch', label: 'pitch', min: 35, max: 90, step: 1, value: 50, unit: ' Hz' },
          { name: 'punch', label: 'punch', min: 0, max: 1, step: 0.05, value: 0.6 },
          { name: 'decay', label: 'decay', min: 0.1, max: 1.5, step: 0.05, value: 0.5, unit: ' s' },
        ]} />
      <SoundCard
        title="Snare drum"
        blurb="Two membrane modes (the shell) + high-passed noise (the wires). Snappy sets the balance."
        endpoint="/api/instruments/snare"
        params={[
          { name: 'pitch', label: 'pitch', min: 120, max: 300, step: 5, value: 190, unit: ' Hz' },
          { name: 'snappy', label: 'snappy', min: 0, max: 1, step: 0.05, value: 0.7 },
          { name: 'decay', label: 'decay', min: 0.1, max: 0.8, step: 0.05, value: 0.25, unit: ' s' },
        ]} />
      <SoundCard
        title="Hi-hat"
        blurb="Six inharmonic square waves (the TR-808's trick) high-passed to sizzle; open rings, closed chokes."
        endpoint="/api/instruments/hihat"
        checks={[{ name: 'open', label: 'open', value: false }]} />
      <SoundCard
        title="Tom & cymbal"
        blurb="The tom is a gentler kick with a 1.59× membrane overtone; hear the cymbal card below for the modal-chaos approach."
        endpoint="/api/instruments/tom"
        params={[
          { name: 'pitch', label: 'pitch', min: 70, max: 220, step: 5, value: 110, unit: ' Hz' },
          { name: 'decay', label: 'decay', min: 0.15, max: 1, step: 0.05, value: 0.4, unit: ' s' },
        ]} />
      <SoundCard
        title="Cymbal"
        blurb="48 partials scattered log-uniformly over 300 Hz–12 kHz — modal density shading into chaos, highs dying first."
        endpoint="/api/instruments/cymbal"
        params={[{ name: 'decay', label: 'decay', min: 0.5, max: 5, step: 0.1, value: 2.5, unit: ' s' }]} />
      <SoundCard
        title="Bass guitar"
        blurb="Karplus–Strong with the excitation pre-filtered (Jaffe–Smith) and the body rounded off — thumb, not banjo."
        endpoint="/api/instruments/bass"
        selects={[{ name: 'note', label: 'note', options: lowNotes, value: 'E1' }]}
        params={[{ name: 'tone', label: 'tone', min: 0, max: 1, step: 0.05, value: 0.4 }]} />
      <SoundCard
        title="Guitar"
        blurb="The pick-position comb filter: harmonics with a node where you pick simply cancel. Slide it and listen."
        endpoint="/api/instruments/guitar"
        selects={[{ name: 'note', label: 'note', options: midNotes, value: 'A3' }]}
        params={[
          { name: 'pickPosition', label: 'pick position', min: 0.05, max: 0.45, step: 0.05, value: 0.2 },
          { name: 'damping', label: 'damping', min: 0, max: 1, step: 0.05, value: 0.15 },
        ]} />
      <SoundCard
        title="Power chord"
        blurb="Root + fifth + octave, distorted. The third is omitted because distortion's intermodulation would turn it to mud."
        endpoint="/api/instruments/powerchord"
        selects={[{ name: 'note', label: 'root', options: lowNotes, value: 'E2' }]}
        params={[{ name: 'drive', label: 'drive', min: 1, max: 12, step: 0.5, value: 5 }]} />
      <SoundCard
        title="Mallets"
        blurb="The same bar, three tunings: marimba arched to 1:4:10, xylophone quint-tuned to 1:3, glockenspiel left at the free bar's 1:2.76:5.40."
        endpoint="/api/instruments/mallet"
        selects={[
          { name: 'kind', label: 'instrument', options: ['marimba', 'xylophone', 'glockenspiel'], value: 'marimba' },
          { name: 'note', label: 'note', options: highNotes, value: 'C4' },
        ]} />
      <SoundCard
        title="Drawbar organ"
        blurb="Nine near-sine partials at the Hammond footages, ~3 dB per drawbar stop — plus the key click they couldn't engineer out."
        endpoint="/api/instruments/organ"
        selects={[
          { name: 'note', label: 'note', options: midNotes, value: 'C3' },
          { name: 'registration', label: 'drawbars', options: registrations, value: '888000000' },
        ]}
        params={[{ name: 'duration', label: 'length', min: 0.5, max: 4, step: 0.1, value: 1.5, unit: ' s' }]} />
      <SoundCard
        title="Flute"
        blurb="Nearly a sine (measured flute spectra are fundamental-dominated) + tube-coloured breath + vibrato that waits."
        endpoint="/api/instruments/flute"
        selects={[{ name: 'note', label: 'note', options: highNotes, value: 'A4' }]}
        params={[
          { name: 'breathiness', label: 'breath', min: 0, max: 1, step: 0.05, value: 0.3 },
          { name: 'duration', label: 'length', min: 0.5, max: 4, step: 0.1, value: 1.5, unit: ' s' },
        ]} />
      <SoundCard
        title="Brass"
        blurb="Risset's trumpet law in one line: the filter cutoff rides the loudness envelope, so louder is brighter."
        endpoint="/api/instruments/brass"
        selects={[{ name: 'note', label: 'note', options: midNotes, value: 'C3' }]}
        params={[
          { name: 'brightness', label: 'brightness', min: 0, max: 1, step: 0.05, value: 0.7 },
          { name: 'duration', label: 'length', min: 0.5, max: 3, step: 0.1, value: 1.2, unit: ' s' },
        ]} />
    </div>
  </section>

  <section>
    <h2>The synthesizer</h2>
    <p>
      Subtractive synthesis, complete and inspectable: start bright, carve with the filter, shape
      with envelopes, move with the LFO.
    </p>
    <div class="grid">
      <SynthCard />
    </div>
  </section>

  <section>
    <h2>Background music — genres as specifications</h2>
    <p>
      Each generator is built only from documented, citable conventions — the 12-bar form, the
      backbeat, four-on-the-floor, the 140 BPM half-time drop, Eno's incommensurate loops — with
      the sources in the class docs and the README. Re-roll the seed for a new performance of the
      same specification.
    </p>
    <div class="grid">
      <SoundCard
        title="Blues"
        blurb="12-bar I–IV–V with dominant 7ths and a V turnaround; 2:1 shuffle; boogie bass; root+5/root+6 comping; blues-scale fills."
        endpoint="/api/music/genre/blues"
        selects={[{ name: 'root', label: 'key', options: keys, value: 'E2' }]}
        params={[
          { name: 'bpm', label: 'tempo', min: 60, max: 160, step: 2, value: 96, unit: ' BPM' },
          { name: 'choruses', label: 'choruses', min: 1, max: 2, step: 1, value: 1 },
        ]} />
      <SoundCard
        title="Rock"
        blurb="Straight 4/4 backbeat, I–♭VII–IV changes (the rock corpus's favourites), distorted power-chord chug, pentatonic hook, tom fills."
        endpoint="/api/music/genre/rock"
        selects={[{ name: 'root', label: 'key', options: keys, value: 'E2' }]}
        params={[
          { name: 'bpm', label: 'tempo', min: 90, max: 160, step: 2, value: 120, unit: ' BPM' },
          { name: 'bars', label: 'bars', min: 4, max: 16, step: 4, value: 8 },
        ]} />
      <SoundCard
        title="Dubstep"
        blurb="140 BPM half-time: kick on 1, snare on 3 only; tempo-synced wobble re-rolled per bar; clean sine sub; build → drop."
        endpoint="/api/music/genre/dubstep"
        selects={[{ name: 'root', label: 'key', options: ['E1', 'G1', 'A1', 'C2'], value: 'A1' }]}
        params={[
          { name: 'bpm', label: 'tempo', min: 135, max: 145, step: 1, value: 140, unit: ' BPM' },
          { name: 'bars', label: 'bars', min: 4, max: 16, step: 4, value: 8 },
        ]} />
      <SoundCard
        title="House"
        blurb="Four-on-the-floor at 124, open hats on every offbeat, swung 16th hats, clap on 2 &amp; 4, offbeat bass, i7–VImaj7 organ stabs."
        endpoint="/api/music/genre/house"
        selects={[{ name: 'root', label: 'key', options: ['E2', 'G2', 'A2', 'C3'], value: 'A2' }]}
        params={[
          { name: 'bpm', label: 'tempo', min: 118, max: 130, step: 1, value: 124, unit: ' BPM' },
          { name: 'bars', label: 'bars', min: 4, max: 16, step: 4, value: 8 },
        ]} />
      <SoundCard
        title="Electronica"
        blurb="85 BPM downtempo: late snares, dusty kit, slow i9–VImaj7 pads — and three melodic loops of 7, 11 and 13 beats that never re-align."
        endpoint="/api/music/genre/electronica"
        selects={[{ name: 'root', label: 'key', options: ['E2', 'G2', 'A2', 'C3'], value: 'A2' }]}
        params={[
          { name: 'bpm', label: 'tempo', min: 60, max: 110, step: 1, value: 85, unit: ' BPM' },
          { name: 'bars', label: 'bars', min: 4, max: 16, step: 4, value: 8 },
        ]} />
    </div>
  </section>

  <section>
    <h2>Music file formats — MIDI &amp; MOD</h2>
    <p>
      Both demos are authored in code, written out through the real file encoder, parsed back by
      the real decoder, and then performed — a full round-trip of each format. The MIDI piece is
      voiced by the General MIDI map onto the library's instruments; the module is replayed the
      way the Amiga's Paula chip did it: periods for pitch, ticks for time, effects re-firing
      fifty times a second.
    </p>
    <div class="grid">
      <SoundCard
        title="Standard MIDI File"
        blurb="A four-bar I–vi–IV–V .mid: marimba arpeggios, organ pads, bass and the GM drum map. Transpose shifts every pitched note; the drums stay put."
        endpoint="/api/formats/midi"
        params={[
          { name: 'bpm', label: 'tempo', min: 70, max: 160, step: 2, value: 110, unit: ' BPM' },
          { name: 'transpose', label: 'transpose', min: -12, max: 12, step: 1, value: 0, unit: ' st' },
        ]} />
      <SoundCard
        title="ProTracker module"
        blurb="A one-pattern .mod from four hand-built samples: square-wave arpeggio lead, triangle bass, noise hat, swept-sine kick. Speed is ticks per row — the tracker's tempo."
        endpoint="/api/formats/mod"
        params={[{ name: 'speed', label: 'speed', min: 3, max: 10, step: 1, value: 6, unit: ' ticks/row' }]} />
    </div>
  </section>

  <section>
    <h2>Mood, tension &amp; genre</h2>
    <p>
      Emotion as coordinates: valence, arousal and tension (Russell's circumplex + Huron's
      expectation theory), mapped to register, scale, brightness, detune and tempo. The same
      generator plays every genre — only the coordinates move.
    </p>
    <div class="grid">
      <SoundCard
        title="Mood drone"
        blurb="The underscore bed a mood implies: chord voicing, register and brightness all come from the coordinates."
        endpoint="/api/music/drone"
        selects={[{ name: 'mood', label: 'mood', options: moods, value: 'calm' }]}
        params={[{ name: 'duration', label: 'duration', min: 4, max: 20, step: 1, value: 8, unit: ' s' }]} />
      <SoundCard
        title="Shepard tone"
        blurb="The endless rise (Dunkirk's engine of dread): octave voices under a fixed loudness window."
        endpoint="/api/music/shepard"
        selects={[{ name: 'direction', label: 'direction', options: [1, -1], value: 1 }]}
        params={[
          { name: 'cycle', label: 'octave time', min: 2, max: 20, step: 1, value: 8, unit: ' s' },
          { name: 'duration', label: 'duration', min: 4, max: 24, step: 1, value: 12, unit: ' s' },
        ]} />
      <SoundCard
        title="Riser"
        blurb="Every escalation cue at once — pitch, brightness, loudness and pulse rate all climbing to the arrival."
        endpoint="/api/music/riser"
        params={[
          { name: 'duration', label: 'duration', min: 1, max: 8, step: 0.5, value: 4, unit: ' s' },
          { name: 'intensity', label: 'intensity', min: 0, max: 1, step: 0.05, value: 0.7 },
        ]} />
      <SoundCard
        title="Stinger"
        blurb="The accent hit: a mood-voiced chord (triumph gets consonance, horror gets the cluster) over a real modal impact."
        endpoint="/api/music/stinger"
        selects={[{ name: 'mood', label: 'mood', options: moods, value: 'horror' }]} />
      <SoundCard
        title="Heartbeat"
        blurb="Lub-dub at a chosen pulse — the tension device that invites the listener's body to follow."
        endpoint="/api/music/heartbeat"
        params={[
          { name: 'bpm', label: 'BPM', min: 40, max: 180, step: 5, value: 90 },
          { name: 'duration', label: 'duration', min: 2, max: 16, step: 1, value: 6, unit: ' s' },
        ]} />
    </div>
  </section>

  <section>
    <h2>Generative scenes</h2>
    <p>
      The whole library at once: beds, mood drone, tension devices and sparse accents, layered
      through the priority mixer so effects duck the beds automatically and every layer stays
      functional. Pick a genre; re-roll for a different take on the same mood.
    </p>
    <div class="grid">
      <SoundCard
        title="Scene"
        blurb="A complete stereo soundscape from one mood + weather. Horror brings the cluster drone, Shepard rise and heartbeat by itself."
        endpoint="/api/scene"
        selects={[{ name: 'mood', label: 'genre / mood', options: moods, value: 'horror' }]}
        params={[{ name: 'duration', label: 'duration', min: 5, max: 30, step: 1, value: 15, unit: ' s' }]}
        checks={[
          { name: 'wind', label: 'wind', value: true },
          { name: 'rain', label: 'rain', value: false },
          { name: 'fire', label: 'fire', value: false },
        ]} />
    </div>
  </section>
</main>

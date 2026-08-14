<script>
  import SoundCard from './lib/SoundCard.svelte';

  const materials = ['steel', 'glass', 'wood', 'stone', 'plastic', 'rubber', 'ceramic', 'ice'];
  const surfaces = ['gravel', 'sand', 'leaves', 'snow'];
  const allSurfaces = [...surfaces, ...materials];
  const moods = ['calm', 'fun', 'fastpaced', 'anticipation', 'threat', 'horror', 'sad', 'triumphant'];
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

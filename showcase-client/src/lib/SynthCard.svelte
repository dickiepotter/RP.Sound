<script>
  // The full subtractive synthesizer panel: every SynthPatch parameter as a control, a preset
  // selector that loads the same values the C# presets use (so choosing one *shows* the patch),
  // and a two-octave keyboard — click a key, hear the patch play that note via /api/synth/play.
  const waveforms = ['sine', 'triangle', 'square', 'sawtooth'];

  // Mirrors of SynthPatch's C# presets, so the knobs reflect what the library defines.
  const presets = {
    init:   { osc1: 'sawtooth', osc2: 'sawtooth', detune: 7,  mix: 0.5, noise: 0, cutoff: 2000, resonance: 0.9, filterOctaves: 2,   attack: 0.01,  decay: 0.1,  sustainDb: -3, release: 0.2, lfoWave: 'sine', lfoRate: 5,   vibrato: 0,  wobble: 0,   tremolo: 0 },
    bass:   { osc1: 'sawtooth', osc2: 'sawtooth', detune: 5,  mix: 0.5, noise: 0, cutoff: 400,  resonance: 1.2, filterOctaves: 2.5, attack: 0.005, decay: 0.15, sustainDb: -6, release: 0.1, lfoWave: 'sine', lfoRate: 5,   vibrato: 0,  wobble: 0,   tremolo: 0 },
    lead:   { osc1: 'sawtooth', osc2: 'square',   detune: 10, mix: 0.5, noise: 0, cutoff: 1500, resonance: 2,   filterOctaves: 1.5, attack: 0.02,  decay: 0.1,  sustainDb: -3, release: 0.15, lfoWave: 'sine', lfoRate: 5,  vibrato: 20, wobble: 0,   tremolo: 0 },
    pluck:  { osc1: 'sawtooth', osc2: 'sawtooth', detune: 4,  mix: 0.5, noise: 0, cutoff: 300,  resonance: 1.5, filterOctaves: 3.5, attack: 0.002, decay: 0.3,  sustainDb: -60, release: 0.05, lfoWave: 'sine', lfoRate: 5, vibrato: 0,  wobble: 0,   tremolo: 0 },
    pad:    { osc1: 'sawtooth', osc2: 'sawtooth', detune: 15, mix: 0.5, noise: 0, cutoff: 900,  resonance: 0.8, filterOctaves: 0.8, attack: 0.8,   decay: 0.3,  sustainDb: -2, release: 1.2, lfoWave: 'sine', lfoRate: 0.5, vibrato: 0,  wobble: 0.3, tremolo: 0 },
    wobble: { osc1: 'sawtooth', osc2: 'square',   detune: -1200, mix: 0.5, noise: 0, cutoff: 250, resonance: 3, filterOctaves: 0,   attack: 0.005, decay: 0.05, sustainDb: 0,  release: 0.05, lfoWave: 'sine', lfoRate: 4.67, vibrato: 0, wobble: 2.5, tremolo: 0 },
  };

  let preset = $state('init');
  let p = $state({ ...presets.init });
  let duration = $state(1.5);
  let busy = $state(false);
  let lastNote = $state('A3');
  let canvas = $state();
  let source = null;

  function loadPreset() {
    p = { ...presets[preset] };
  }

  // Two octaves, C3 to C5. Black keys carry their sharp name and sit between the whites.
  const octaves = [3, 4];
  const whites = ['C', 'D', 'E', 'F', 'G', 'A', 'B'];
  const sharps = { C: 'C#', D: 'D#', F: 'F#', G: 'G#', A: 'A#' };
  const keys = [];
  for (const octave of octaves) {
    for (const w of whites) {
      keys.push({ note: `${w}${octave}`, black: false });
      if (sharps[w]) keys.push({ note: `${sharps[w]}${octave}`, black: true });
    }
  }
  keys.push({ note: 'C5', black: false });

  async function play(note) {
    lastNote = note;
    busy = true;
    try {
      const audio = (SynthCard_ctx ??= new (window.AudioContext || window.webkitAudioContext)());
      if (audio.state === 'suspended') await audio.resume();
      const query = new URLSearchParams({ ...p, note, duration });
      const response = await fetch(`/api/synth/play?${query}`);
      if (!response.ok) throw new Error(await response.text());
      const buffer = await audio.decodeAudioData(await response.arrayBuffer());
      draw(buffer);
      source?.stop();
      source = audio.createBufferSource();
      source.buffer = buffer;
      source.connect(audio.destination);
      source.start();
    } catch (error) {
      console.error('[RP.Sound] Synthesizer:', error);
    } finally {
      busy = false;
    }
  }

  function draw(buffer) {
    const context = canvas.getContext('2d');
    const width = (canvas.width = canvas.clientWidth * devicePixelRatio);
    const height = (canvas.height = canvas.clientHeight * devicePixelRatio);
    const data = buffer.getChannelData(0);
    context.clearRect(0, 0, width, height);
    context.strokeStyle = getComputedStyle(document.documentElement).getPropertyValue('--wave');
    context.lineWidth = devicePixelRatio;
    context.beginPath();
    const step = Math.max(1, Math.floor(data.length / width));
    for (let x = 0; x < width; x++) {
      let min = 1, max = -1;
      for (let i = x * step; i < (x + 1) * step && i < data.length; i++) {
        if (data[i] < min) min = data[i];
        if (data[i] > max) max = data[i];
      }
      const mid = height / 2;
      context.moveTo(x, mid - max * mid * 0.95);
      context.lineTo(x, mid - min * mid * 0.95);
    }
    context.stroke();
  }
</script>

<script module>
  let SynthCard_ctx;
</script>

<div class="card synth">
  <h3>The subtractive synthesizer</h3>
  <p class="blurb">
    The classic Minimoog signal path: two oscillators → low-pass filter → amplifier, with an
    envelope on loudness, an envelope on brightness, and one LFO whose three destinations have
    the names every musician knows — pitch is vibrato, cutoff is wah/wobble, loudness is tremolo.
    Pick a preset to see how each patch family sets the same knobs, then play the keyboard.
  </p>

  <div class="control">
    <label for="synth-preset">preset</label>
    <select id="synth-preset" bind:value={preset} onchange={loadPreset}>
      {#each Object.keys(presets) as name}<option value={name}>{name}</option>{/each}
    </select>
  </div>

  <div class="columns">
    <div class="group">
      <h4>Oscillators</h4>
      <div class="control"><label for="s-osc1">osc 1</label>
        <select id="s-osc1" bind:value={p.osc1}>{#each waveforms as w}<option>{w}</option>{/each}</select></div>
      <div class="control"><label for="s-osc2">osc 2</label>
        <select id="s-osc2" bind:value={p.osc2}>{#each waveforms as w}<option>{w}</option>{/each}</select></div>
      <div class="control"><label for="s-detune">detune</label>
        <input id="s-detune" type="range" min="-1200" max="1200" step="1" bind:value={p.detune} />
        <span class="value">{p.detune} ¢</span></div>
      <div class="control"><label for="s-mix">osc mix</label>
        <input id="s-mix" type="range" min="0" max="1" step="0.05" bind:value={p.mix} />
        <span class="value">{p.mix}</span></div>
      <div class="control"><label for="s-noise">noise</label>
        <input id="s-noise" type="range" min="0" max="1" step="0.05" bind:value={p.noise} />
        <span class="value">{p.noise}</span></div>
    </div>

    <div class="group">
      <h4>Filter</h4>
      <div class="control"><label for="s-cutoff">cutoff</label>
        <input id="s-cutoff" type="range" min="50" max="8000" step="10" bind:value={p.cutoff} />
        <span class="value">{p.cutoff} Hz</span></div>
      <div class="control"><label for="s-res">resonance</label>
        <input id="s-res" type="range" min="0.3" max="8" step="0.1" bind:value={p.resonance} />
        <span class="value">{p.resonance}</span></div>
      <div class="control"><label for="s-focts">env depth</label>
        <input id="s-focts" type="range" min="0" max="6" step="0.1" bind:value={p.filterOctaves} />
        <span class="value">{p.filterOctaves} oct</span></div>
    </div>

    <div class="group">
      <h4>Amplifier envelope</h4>
      <div class="control"><label for="s-att">attack</label>
        <input id="s-att" type="range" min="0" max="2" step="0.005" bind:value={p.attack} />
        <span class="value">{p.attack} s</span></div>
      <div class="control"><label for="s-dec">decay</label>
        <input id="s-dec" type="range" min="0" max="2" step="0.01" bind:value={p.decay} />
        <span class="value">{p.decay} s</span></div>
      <div class="control"><label for="s-sus">sustain</label>
        <input id="s-sus" type="range" min="-60" max="0" step="1" bind:value={p.sustainDb} />
        <span class="value">{p.sustainDb} dB</span></div>
      <div class="control"><label for="s-rel">release</label>
        <input id="s-rel" type="range" min="0" max="3" step="0.01" bind:value={p.release} />
        <span class="value">{p.release} s</span></div>
      <div class="control"><label for="s-len">note length</label>
        <input id="s-len" type="range" min="0.3" max="4" step="0.1" bind:value={duration} />
        <span class="value">{duration} s</span></div>
    </div>

    <div class="group">
      <h4>LFO (movement)</h4>
      <div class="control"><label for="s-lwave">shape</label>
        <select id="s-lwave" bind:value={p.lfoWave}>{#each waveforms as w}<option>{w}</option>{/each}</select></div>
      <div class="control"><label for="s-lrate">rate</label>
        <input id="s-lrate" type="range" min="0.1" max="15" step="0.1" bind:value={p.lfoRate} />
        <span class="value">{p.lfoRate} Hz</span></div>
      <div class="control"><label for="s-vib">→ pitch</label>
        <input id="s-vib" type="range" min="0" max="100" step="1" bind:value={p.vibrato} />
        <span class="value">{p.vibrato} ¢</span></div>
      <div class="control"><label for="s-wob">→ cutoff</label>
        <input id="s-wob" type="range" min="0" max="4" step="0.1" bind:value={p.wobble} />
        <span class="value">{p.wobble} oct</span></div>
      <div class="control"><label for="s-trem">→ loudness</label>
        <input id="s-trem" type="range" min="0" max="1" step="0.05" bind:value={p.tremolo} />
        <span class="value">{p.tremolo}</span></div>
    </div>
  </div>

  <div class="keyboard" role="group" aria-label="keyboard">
    {#each keys as key}
      <button
        class="key"
        class:black={key.black}
        class:active={lastNote === key.note}
        disabled={busy}
        onclick={() => play(key.note)}
        title={key.note}>{key.black ? '' : key.note}</button>
    {/each}
  </div>

  <canvas class="wave" bind:this={canvas}></canvas>
</div>

<style>
  .synth { grid-column: 1 / -1; }

  .columns {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(230px, 1fr));
    gap: 0.9rem 1.4rem;
  }

  .group h4 {
    margin: 0 0 0.4rem;
    font-size: 0.75rem;
    text-transform: uppercase;
    letter-spacing: 0.06em;
    color: var(--accent-2);
  }

  .group .control { margin-bottom: 0.3rem; }
  .group .control label { flex-basis: 4.8rem; }

  .keyboard {
    display: flex;
    align-items: flex-start;
    margin-top: 0.4rem;
    height: 84px;
  }

  .key {
    position: relative;
    flex: 1;
    height: 100%;
    border-radius: 0 0 5px 5px;
    border: 1px solid var(--panel-edge);
    background: #e8ebf0;
    color: #5a6270;
    font-size: 0.6rem;
    font-weight: 400;
    padding: 0;
    display: flex;
    align-items: flex-end;
    justify-content: center;
    padding-bottom: 4px;
  }

  .key.black {
    flex: 0 0 24px;
    height: 55%;
    margin: 0 -12px;
    z-index: 1;
    background: #1a1e26;
    border-color: #000;
  }

  .key.active { background: var(--accent); color: #06121f; }
  .key.black.active { background: var(--accent); }
  .key:hover { filter: brightness(1.08); }
</style>

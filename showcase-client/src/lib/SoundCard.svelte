<script>
  // One demo card: parameter controls -> GET /api/... -> decode -> draw waveform -> play.
  let { title, blurb, endpoint, params = [], selects = [], checks = [] } = $props();

  let values = $state({
    ...Object.fromEntries(params.map((p) => [p.name, p.value])),
    ...Object.fromEntries(selects.map((s) => [s.name, s.value])),
    ...Object.fromEntries(checks.map((c) => [c.name, c.value])),
  });
  let seed = $state(0);
  let busy = $state(false);
  let playing = $state(false);
  let canvas = $state();
  let source = null;

  async function play() {
    // The button is a toggle: while a sound is playing it reads Stop, and stopping the source
    // fires onended, which resets the state.
    if (playing) {
      source?.stop();
      return;
    }
    busy = true;
    try {
      // One shared AudioContext for the whole page (browsers cap how many you may create),
      // created lazily inside the click handler to satisfy autoplay policies.
      const audio = (SoundCard_ctx ??= new (window.AudioContext || window.webkitAudioContext)());
      if (audio.state === 'suspended') await audio.resume();
      const query = new URLSearchParams({ ...values, seed });
      const response = await fetch(`${endpoint}?${query}`);
      if (!response.ok) throw new Error(await response.text());
      const buffer = await audio.decodeAudioData(await response.arrayBuffer());
      draw(buffer);
      source?.stop();
      source = audio.createBufferSource();
      source.buffer = buffer;
      source.connect(audio.destination);
      source.onended = () => (playing = false);
      source.start();
      playing = true;
    } catch (error) {
      console.error(`[RP.Sound] ${title}:`, error);
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
  let SoundCard_ctx;
</script>

<div class="card">
  <h3>{title}</h3>
  <p class="blurb">{blurb}</p>

  {#each selects as select}
    <div class="control">
      <label for={`${title}-${select.name}`}>{select.label}</label>
      <select id={`${title}-${select.name}`} bind:value={values[select.name]}>
        {#each select.options as option}
          <option value={option}>{option}</option>
        {/each}
      </select>
    </div>
  {/each}

  {#each params as param}
    <div class="control">
      <label for={`${title}-${param.name}`}>{param.label}</label>
      <input
        id={`${title}-${param.name}`}
        type="range"
        min={param.min}
        max={param.max}
        step={param.step}
        bind:value={values[param.name]} />
      <span class="value">{values[param.name]}{param.unit ?? ''}</span>
    </div>
  {/each}

  {#each checks as check}
    <div class="control">
      <label for={`${title}-${check.name}`}>{check.label}</label>
      <input id={`${title}-${check.name}`} type="checkbox" bind:checked={values[check.name]} />
    </div>
  {/each}

  <canvas class="wave" bind:this={canvas}></canvas>

  <div class="actions">
    <button onclick={play} disabled={busy}>{busy ? 'Rendering…' : playing ? '■ Stop' : '► Play'}</button>
    <button class="secondary" onclick={() => (seed = Math.floor(Math.random() * 100000))}>Re-roll</button>
    <span class="seed">seed {seed}</span>
  </div>
</div>

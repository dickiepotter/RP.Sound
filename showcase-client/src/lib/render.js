// Where the sounds come from. The ASP.NET showcase renders them on the server behind /api/; the
// static build (`npm run build:pages`, for GitHub Pages) renders the very same catalog in the
// browser, from the C# library compiled to WebAssembly. Both hand the cards a WAV ArrayBuffer.

export const inBrowser = import.meta.env.MODE === 'pages';

let worker;
let nextId = 0;
const pending = new Map();

/** Renders `endpoint` (an /api/... path) with `params` and resolves to the WAV bytes. */
export async function render(endpoint, params) {
  const query = new URLSearchParams(params).toString();
  if (!inBrowser) {
    const response = await fetch(`${endpoint}?${query}`);
    if (!response.ok) throw new Error(await response.text());
    return response.arrayBuffer();
  }

  // The .NET runtime lives in a worker, so a long render (a genre track, a full scene) never
  // freezes the page while it runs.
  worker ??= startWorker();
  const id = nextId++;
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    worker.postMessage({ id, path: endpoint.replace(/^\/api\//, ''), query });
  });
}

function startWorker() {
  const started = new Worker(new URL('./render-worker.js', import.meta.url), { type: 'module' });
  started.onmessage = ({ data: { id, wav, error, fatal } }) => {
    if (fatal) return abandon(started, error);
    const call = pending.get(id);
    pending.delete(id);
    if (error) call.reject(new Error(error));
    else call.resolve(wav);
  };
  started.onerror = (event) => abandon(started, event.message || 'The render worker failed to start.');
  // The runtime sits beside index.html, wherever the site is hosted (a GitHub Pages project site
  // lives under /<repository>/, not at the root).
  started.postMessage({ frameworkUrl: new URL('_framework/dotnet.js', document.baseURI).href });
  return started;
}

// A worker whose runtime failed to load (or that died) cannot be revived in place: the browser
// caches the failed import. Fail everything waiting on it and let the next click start afresh.
function abandon(failed, error) {
  failed.terminate();
  if (worker === failed) worker = undefined;
  for (const call of pending.values()) call.reject(new Error(error));
  pending.clear();
}

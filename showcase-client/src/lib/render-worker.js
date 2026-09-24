// Hosts the WebAssembly build of the showcase catalog (RP.Sound.Showcase.Browser) off the main
// thread. The first message says where the runtime is; every later one is a render request.

let frameworkUrl;
let exports;

self.onmessage = async ({ data }) => {
  if (data.frameworkUrl) {
    frameworkUrl = data.frameworkUrl;
    return;
  }
  const { id, path, query } = data;
  let Render;
  try {
    ({ Render } = await (exports ??= load()));
  } catch (error) {
    // Fatal: the page discards this worker and starts a new one on the next click.
    self.postMessage({ fatal: true, error: `The WebAssembly runtime failed to load: ${error?.message ?? error}` });
    return;
  }
  try {
    // Render returns a fresh copy of the bytes, so its buffer can be handed over rather than cloned.
    const wav = Render(path, query);
    self.postMessage({ id, wav: wav.buffer }, [wav.buffer]);
  } catch (error) {
    self.postMessage({ id, error: String(error?.message ?? error) });
  }
};

async function load() {
  // Without this flag dotnet.js takes any worker for one of its own runtime threads and waits
  // forever for a main thread that never comes; with it, the worker hosts a whole runtime.
  globalThis.dotnetSidecar = true;
  const { dotnet } = await import(/* @vite-ignore */ frameworkUrl);
  const runtime = await dotnet.create();
  const assembly = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
  return assembly.ShowcaseExports;
}

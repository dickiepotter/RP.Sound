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
  try {
    exports ??= load();
    const { Render } = await exports;
    // Render returns a fresh copy of the bytes, so its buffer can be handed over rather than cloned.
    const wav = Render(path, query);
    self.postMessage({ id, wav: wav.buffer }, [wav.buffer]);
  } catch (error) {
    self.postMessage({ id, error: String(error?.message ?? error) });
  }
};

async function load() {
  try {
    // Without this flag dotnet.js takes any worker for one of its own runtime threads and waits
    // forever for a main thread that never comes; with it, the worker hosts a whole runtime.
    globalThis.dotnetSidecar = true;
    const { dotnet } = await import(/* @vite-ignore */ frameworkUrl);
    const runtime = await dotnet.create();
    const assembly = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
    return assembly.ShowcaseExports;
  } catch (error) {
    exports = undefined; // let the next click try again rather than caching the failure
    throw error;
  }
}

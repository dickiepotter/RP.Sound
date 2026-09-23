import { defineConfig } from 'vite';
import { svelte } from '@sveltejs/vite-plugin-svelte';

export default defineConfig(({ mode }) => ({
  plugins: [svelte()],
  // Relative asset URLs, so the static build works wherever it is hosted, including a GitHub Pages
  // project site under /<repository>/.
  base: mode === 'pages' ? './' : '/',
  build:
    mode === 'pages'
      ? // `npm run build:pages` builds a self-contained static site: the client renders every sound
        // in the browser from the WebAssembly build, whose _framework folder is copied in beside it.
        { outDir: 'dist', emptyOutDir: true }
      : // The Svelte app builds straight into the ASP.NET project's static-files folder,
        // so `dotnet run` serves the whole showcase from one process.
        { outDir: '../RP.Sound.Showcase/wwwroot', emptyOutDir: true },
  server: {
    // `npm run dev` proxies API calls to the ASP.NET backend for live-reload development.
    proxy: { '/api': 'http://localhost:5225' },
  },
}));

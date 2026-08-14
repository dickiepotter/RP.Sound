import { defineConfig } from 'vite';
import { svelte } from '@sveltejs/vite-plugin-svelte';

export default defineConfig({
  plugins: [svelte()],
  build: {
    // The Svelte app builds straight into the ASP.NET project's static-files folder,
    // so `dotnet run` serves the whole showcase from one process.
    outDir: '../RP.Sound.Showcase/wwwroot',
    emptyOutDir: true,
  },
  server: {
    // `npm run dev` proxies API calls to the ASP.NET backend for live-reload development.
    proxy: { '/api': 'http://localhost:5225' },
  },
});

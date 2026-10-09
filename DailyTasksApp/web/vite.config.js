import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig(({ mode }) => {
  // The config runs in Node, so .env values are read here rather than via import.meta.env.
  // Production is served under a path prefix (see .env.production); dev stays at the root.
  const env = loadEnv(mode, process.cwd());

  return {
    base: env.VITE_BASE_PATH || '/',
    plugins: [react(), tailwindcss()],
    server: {
      port: 5174,
      strictPort: true,
      // Reachable from a phone on the same wifi; the API itself stays on localhost behind the proxy.
      host: true,
      proxy: {
        '/api': {
          target: 'http://localhost:5278',
          changeOrigin: true,
        },
      },
    },
  };
});

import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
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
});

import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    strictPort: true,

    // Listen on every interface so a phone on the same wifi can reach the dev server.
    // The API is deliberately NOT exposed this way: it stays on localhost and is reached
    // through the proxy below, so nothing but this one port is open on the network.
    host: true,

    // Vite refuses a request whose Host header it does not recognise. A tunnel hands it
    // a random subdomain, so those are named here - development only, and it changes
    // nothing about who the API will talk to.
    allowedHosts: ['.ngrok-free.dev', '.ngrok-free.app', '.ngrok.io', '.ngrok.app', '.trycloudflare.com'],

    // Same shape as production, where nginx serves the app and forwards /api to the API
    // (see nginx/nginx.conf). Because the browser only ever talks to the origin it loaded
    // the page from, no device needs the API's address and CORS never enters the picture.
    proxy: {
      '/api': {
        target: 'http://localhost:5140',
        changeOrigin: true,
      },
    },
  },
});

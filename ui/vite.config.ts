import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

// The browser only ever talks to the Vite origin: /api is proxied to the API with the prefix
// stripped, so there is no CORS and the API keeps its own paths (README, ADR-007).
export default defineConfig({
  plugins: [vue()],
  server: {
    proxy: {
      '/api': {
        target: process.env.API_URL ?? 'http://localhost:8080',
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
})

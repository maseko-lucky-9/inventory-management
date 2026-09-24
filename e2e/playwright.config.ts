import { defineConfig, devices } from '@playwright/test'

// The UI's Vite dev server proxies /api to API_URL, so the browser talks to one origin.
const apiUrl = process.env.API_URL ?? 'http://localhost:8080'

export default defineConfig({
  testDir: '.',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5173',
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npm --prefix ../ui run dev -- --port 5173 --strictPort',
    url: 'http://localhost:5173',
    env: { API_URL: apiUrl },
    reuseExistingServer: true,
  },
})

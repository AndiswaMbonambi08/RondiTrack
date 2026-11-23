import { defineConfig, devices } from '@playwright/test'
import { SIPHO_STATE } from './e2e/paths'

const isCI = Boolean(process.env.CI)
const baseURL = 'http://localhost:3000'

// Runs against the PRODUCTION build (npm run build, then npm run start), never next dev.
// The API, database and seed come from docker compose (see compose.e2e.yaml).
// One worker: the tests share one saved session, and the login limit is 5 per minute.
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  retries: 0, // a retry would hide a flaky test
  forbidOnly: isCI,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: isCI ? [['html', { open: 'never' }], ['github']] : [['html', { open: 'never' }], ['list']],
  use: { baseURL, trace: isCI ? 'retain-on-failure' : 'on' },
  projects: [
    // Signs in once per person and saves the sessions. Everything else reuses them.
    { name: 'setup', testMatch: /.*\.setup\.ts/ },
    {
      name: 'chromium',
      testIgnore: /.*\.setup\.ts/,
      dependencies: ['setup'],
      use: { ...devices['Desktop Chrome'], storageState: SIPHO_STATE },
    },
  ],
  webServer: {
    command: 'npm run start',
    url: baseURL,
    reuseExistingServer: !isCI,
    timeout: 120_000,
  },
})
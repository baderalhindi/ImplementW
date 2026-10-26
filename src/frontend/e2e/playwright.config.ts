import { defineConfig, devices } from '@playwright/test';

// SIT has a certificate from a public CA; the e2e stack's edge has one from Caddy's internal CA, which the browser is told to
// accept there and only there (docs/testing/e2e-suite.md §3).
const acceptStackCertificate = process.env.E2E_TARGET === 'compose';

export default defineConfig({
  testDir: '.',
  testMatch: ['journeys/**/*.spec.ts', 'participation/**/*.spec.ts'],
  outputDir: '../test-results/artifacts',
  fullyParallel: false,
  workers: 1,
  // test-strategy.md §10: no retry-on-failure. A failing journey is a failed run.
  retries: 0,
  forbidOnly: true,
  timeout: 10 * 60 * 1000,
  expect: { timeout: 20_000 },
  reporter: [
    ['list'],
    ['html', { outputFolder: '../playwright-report', open: 'never' }],
    ['junit', { outputFile: '../test-results/junit.xml' }],
    ['json', { outputFile: '../test-results/results.json' }],
  ],
  use: {
    ...devices['Desktop Chrome'],
    actionTimeout: 20_000,
    navigationTimeout: 30_000,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    ignoreHTTPSErrors: acceptStackCertificate,
  },
});

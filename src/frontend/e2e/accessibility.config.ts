import { defineConfig } from '@playwright/test';

import journeys from './playwright.config.ts';

// The platform accessibility audit (TASK-087, docs/testing/accessibility-audit.md): the journeys' browser, stack and settings,
// run over accessibility/ instead of the journeys. `audit-data` creates the records the screens show; every screen and language
// is then its own test, so one failing screen never stops the others being recorded. `accessibility/run.sh` starts it.
export default defineConfig({
  ...journeys,
  testMatch: undefined,
  timeout: 3 * 60 * 1000,
  reporter: [
    ['list'],
    ['html', { outputFolder: '../playwright-report/accessibility', open: 'never' }],
    ['json', { outputFile: '../test-results/accessibility-results.json' }],
  ],
  projects: [
    { name: 'audit-data', testMatch: 'accessibility/audit-data.setup.ts' },
    { name: 'audit', testMatch: 'accessibility/**/*.spec.ts', dependencies: ['audit-data'] },
  ],
});

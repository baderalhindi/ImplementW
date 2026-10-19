import { defineConfig } from '@playwright/test';

import journeys from './playwright.config.ts';

// The UAT data set (TASK-088, docs/testing/uat/uat-execution.md §5): the journeys' browser, stack and settings, run over uat/
// instead of the journeys. Every fixture of uat/data-set.json is its own test, so one that fails never stops the others being
// staged. `uat/run.sh` starts it.
export default defineConfig({
  ...journeys,
  testMatch: 'uat/**/*.spec.ts',
  timeout: 5 * 60 * 1000,
  reporter: [
    ['list'],
    ['html', { outputFolder: '../playwright-report/uat', open: 'never' }],
    ['json', { outputFile: '../test-results/uat-results.json' }],
  ],
});

/// <reference types="node" />
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

/**
 * The app's stylesheet as written. jsdom lays out and paints nothing, and Vitest hands CSS imports over empty, so a test of a rule reads
 * the file itself (Vitest runs from src/frontend).
 */
export const APP_STYLES = readFileSync(resolve(process.cwd(), 'src/app/styles.css'), 'utf8');

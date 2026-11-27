/// <reference types="node" />
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

/**
 * The app's stylesheet as written. jsdom lays out and paints nothing, and Vitest hands CSS imports over empty, so a test of a rule reads
 * the file itself (Vitest runs from src/frontend).
 */
export const APP_STYLES = readFileSync(resolve(process.cwd(), 'src/app/styles.css'), 'utf8');

/** The text between the braces of the block that opens at `start`, nested blocks included. */
function blockAt(styles: string, start: number): string {
  const open = styles.indexOf('{', start);
  let depth = 0;
  for (let i = open; i < styles.length; i += 1) {
    if (styles[i] === '{') depth += 1;
    if (styles[i] === '}') depth -= 1;
    if (depth === 0) return styles.slice(open + 1, i);
  }
  throw new Error(`unclosed block at ${String(start)}`);
}

/**
 * The declarations of the first rule whose selector is exactly `selector`, at the top level of `styles` or, with `within`, inside the
 * first block whose prelude is exactly `within` (an @media or @starting-style), as property → value. Undefined when there is none.
 */
export function ruleDeclarations(
  selector: string,
  within?: string,
  styles: string = APP_STYLES,
): Record<string, string> | undefined {
  let scope = styles;
  let indent = '';
  if (within !== undefined) {
    const at = styles.indexOf(`\n${within} {`);
    if (at < 0) return undefined;
    scope = blockAt(styles, at);
    indent = /\n( +)\S/.exec(scope)?.[1] ?? '';
  }
  const start = scope.indexOf(`\n${indent}${selector} {`);
  if (start < 0) return undefined;
  return Object.fromEntries(
    blockAt(scope, start)
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .split(';')
      .filter((line) => line.includes(':'))
      .map((line) => {
        const colon = line.indexOf(':');
        return [
          line.slice(0, colon).trim(),
          line
            .slice(colon + 1)
            .replace(/\s+/g, ' ')
            .trim(),
        ];
      }),
  );
}

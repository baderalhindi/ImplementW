import { describe, expect, test } from 'vitest';

import { ruleDeclarations } from '@/test/stylesheet.ts';

import { contrastRatio } from '../design/textBox.ts';

// Stage QA F-02: a map's project count is small text, so it needs 4.5:1 on its circle (WCAG 1.4.3) in both themes. jsdom paints nothing,
// so the rule is read from the stylesheet and its colours resolved against each theme's tokens.

/** The declarations of the first rule whose selector is exactly `selector`. */
function declarations(selector: string): Record<string, string> {
  const rule = ruleDeclarations(selector);
  expect(rule, selector).toBeDefined();
  return rule ?? {};
}

const THEMES = { light: declarations(':root'), dark: declarations(":root[data-theme='dark']") };

function resolve(value: string | undefined, theme: keyof typeof THEMES): string {
  const token = /^var\((--[\w-]+)\)$/.exec(value ?? '')?.[1];
  const resolved = token === undefined ? value : (THEMES[theme][token] ?? THEMES.light[token]);
  const hex = resolved === '#fff' ? '#ffffff' : resolved;
  expect(hex, `${String(value)} in the ${theme} theme`).toMatch(/^#[0-9a-f]{6}$/i);
  return hex ?? '';
}

describe.each(['light', 'dark'] as const)('the map count bubble in the %s theme', (theme) => {
  test.each(['.map-bubble', '.map-bubble--selected'])('%s reads at 4.5:1 or more', (selector) => {
    const rule = { ...declarations('.map-bubble'), ...declarations(selector) };
    expect(
      contrastRatio(resolve(rule.color, theme), resolve(rule.background, theme)),
    ).toBeGreaterThanOrEqual(4.5);
  });
});

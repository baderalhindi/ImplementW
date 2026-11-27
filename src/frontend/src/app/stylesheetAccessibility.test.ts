import { describe, expect, test } from 'vitest';

import { contrastRatio } from '@/features/dashboards/design/textBox.ts';
import { ruleDeclarations } from '@/test/stylesheet.ts';

// Stage QA, the platform accessibility audit (TASK-087 harness, e2e/accessibility): what it found on the deployed build that jsdom cannot
// lay out or paint, held here by the rules that fixed it.

const NARROW = '@media (max-width: 59.99rem)';

function rule(selector: string, within?: string): Record<string, string> {
  const declarations = ruleDeclarations(selector, within);
  expect(declarations, `${within ?? ''} ${selector}`).toBeDefined();
  return declarations ?? {};
}

describe('reflow at 320 px (WCAG 1.4.10)', () => {
  test('the closed drawer is not displayed, so nothing of it lies beside the window', () => {
    expect(rule('.rail', NARROW).display).toBe('none');
    expect(rule('.rail.rail--drawer-open', NARROW).display).toBe('flex');
  });

  test('the drawer still slides in from its edge when opened', () => {
    expect(rule('.rail.rail--drawer-open', '  @starting-style').transform).toBe(
      'translateX(-100%)',
    );
    expect(rule('.rail.rail--drawer-open', NARROW).transition).toBe('transform 0.25s ease');
    // Closing, or the window narrowing, takes it away at once: no transition keeps it displayed beside the window.
    expect(rule('.rail', NARROW).transition).toBeUndefined();
  });

  test("the quick actions' measuring row is as wide as the window and clips the rest, its items at their natural width", () => {
    const row = rule('.home__actions-bar .home__actions.home__actions--measure');
    expect(row['inline-size']).toBe('100%');
    expect(row.overflow).toBe('hidden');
    expect(rule('.home__actions-bar .home__actions.home__actions--measure > li').flex).toBe('none');
  });
});

describe('the Home banner text has its own ground (WCAG 1.4.3, F24)', () => {
  test('the banner gradient is drawn over the rail teal', () => {
    expect(rule('.home__hero').background).toMatch(/\)\s*var\(--color-rail\)$/);
  });

  test.each([
    ['light', '.app'],
    ['dark', ":root[data-theme='dark'] .app"],
  ])('the banner text reads at 4.5:1 or more on that teal in the %s theme', (_theme, tokens) => {
    const ground = rule(tokens)['--color-rail'] ?? '';
    const text = rule('.app')['--color-rail-text'] ?? '';
    expect(contrastRatio(text, ground)).toBeGreaterThanOrEqual(4.5);
  });
});

describe('a card link shows its focus on the link itself (WCAG 2.4.7)', () => {
  test.each([
    [
      '.report-list__link:focus-visible',
      '.report-list__item:has(.report-list__link:focus-visible)',
    ],
    ['.home__tile-link:focus-visible', '.home__tile--linked:has(.home__tile-link:focus-visible)'],
  ])('%s is underlined and its card ringed', (link, card) => {
    expect(rule(link)['text-decoration-line']).toBe('underline');
    expect(rule(card).outline).toMatch(/solid/);
  });
});

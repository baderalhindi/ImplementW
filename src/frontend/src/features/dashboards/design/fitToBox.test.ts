import { describe, expect, test } from 'vitest';

import { fitScale, fitsToBox, largestFit, MIN_FIT_SCALE } from './fitToBox.ts';

// Fit to box (20 Nov 2026: "free to minimize a visual or container, not fixed"): a body larger than its room is drawn smaller, whole.

describe('fit to box', () => {
  test('content that fits is drawn at its size, never larger', () => {
    expect(fitScale({ width: 300, height: 200 }, { width: 300, height: 200 })).toBe(1);
    expect(fitScale({ width: 100, height: 50 }, { width: 600, height: 400 })).toBe(1);
    // A pixel of rounding is not an overflow.
    expect(fitScale({ width: 301, height: 201 }, { width: 300, height: 200 })).toBe(1);
  });

  test('content taller or wider than its room shrinks evenly by the smaller ratio', () => {
    // The On-plan indicator's four boxes, 320 px tall, in a visual sized to 2 rows (a 120 px body).
    expect(fitScale({ width: 400, height: 320 }, { width: 400, height: 120 })).toBe(0.375);
    expect(fitScale({ width: 800, height: 100 }, { width: 400, height: 300 })).toBe(0.5);
    // Both: the tighter axis decides.
    expect(fitScale({ width: 500, height: 400 }, { width: 400, height: 100 })).toBe(0.25);
  });

  test('unknown or empty sizes leave the content as it is', () => {
    expect(fitScale({ width: 0, height: 0 }, { width: 400, height: 100 })).toBe(1);
    expect(fitScale({ width: 400, height: 320 }, { width: 0, height: 0 })).toBe(1);
  });

  test('cards, gauges, tables, pies, donuts, sections and widgets are fitted; bars, columns, lines and the map draw at their size', () => {
    for (const visualType of ['CARD', 'GAUGE', 'TABLE', 'PIE', 'DONUT', 'SECTION']) {
      expect(fitsToBox({ visualType }), visualType).toBe(true);
    }
    expect(fitsToBox({ visualType: 'SECTION', section: 'DASHBOARD_WIDGET' })).toBe(true);
    for (const visualType of ['BAR', 'COLUMN', 'LINE']) {
      expect(fitsToBox({ visualType }), visualType).toBe(false);
    }
    expect(fitsToBox({ visualType: 'SECTION', section: 'PROJECT_LOCATIONS' })).toBe(false);
  });

  test('laid out wider as it is drawn smaller, content takes the largest scale that still fits', () => {
    // A table of 20 rows of 30 px in a 600 × 200 px room: its rows wrap to two lines under 900 px of width.
    const room = { width: 600, height: 200 };
    const heightAt = (width: number) => (width < 900 ? 1200 : 600);
    const fitsAt = (scale: number) => heightAt(room.width / scale) * scale <= room.height;
    const least = fitScale({ width: 600, height: heightAt(600) }, room);
    expect(least).toBeCloseTo(1 / 6);
    // At a third, laid out 1800 px wide, its 600 px fit 200 px: larger than a sixth, and within a search step (1/128 of the range) of a third.
    const best = largestFit(least, fitsAt);
    expect(fitsAt(best)).toBe(true);
    expect(best).toBeGreaterThan(0.32);
    expect(best).toBeLessThanOrEqual(1 / 3);
    expect(largestFit(0.5, () => true)).toBe(1);
  });
});

describe('a readable least scale (20 Nov 2026)', () => {
  test('a body is fitted down to half its size; one that would need less scrolls at that size instead', () => {
    expect(MIN_FIT_SCALE).toBe(0.5);
    // The On-plan boxes at height 2 need 0.53: fitted. Two 30-row tables in a short visual need 0.32: scrolled.
    expect(fitScale({ width: 600, height: 300 }, { width: 600, height: 159 })).toBeGreaterThan(
      MIN_FIT_SCALE,
    );
    expect(fitScale({ width: 600, height: 1000 }, { width: 600, height: 320 })).toBeLessThan(
      MIN_FIT_SCALE,
    );
  });
});

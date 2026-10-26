import { contextIn, expect, test } from '../support/journey.ts';
import { blocking, reflowAt, runAxe, walkWithTab } from './audit.ts';

// The audit's negative control (docs/testing/accessibility-audit.md §3.3). A clean audit is only evidence if the instrument
// would have seen a defect, so every run first plants one defect of each kind the audit counts on a real screen, under the
// production headers, and requires the harness to report each. If axe stopped running, the walk stopped pressing Tab or the
// focus check stopped looking, this test fails and the audit's zero means nothing.

test('the harness reports planted defects of every kind it counts', async ({ browser }) => {
  const context = await contextIn(browser, 'en');
  const page = await context.newPage();
  try {
    await page.goto('/sign-in');
    await expect(page.locator('h1').first()).toBeVisible();
    await page.evaluate(() => {
      const main = document.querySelector('main') ?? document.body;
      const image = document.createElement('img'); // image-alt (critical): no text alternative
      image.width = 20;
      image.height = 20;
      const field = document.createElement('input'); // label (critical): no accessible name
      field.type = 'text';
      const faint = document.createElement('p'); // color-contrast (serious): about 1.3:1
      faint.textContent = 'Planted faint text';
      faint.style.color = '#dddddd';
      faint.style.backgroundColor = '#ffffff';
      const unreachable = document.createElement('div'); // a control Tab cannot reach
      unreachable.setAttribute('role', 'button');
      unreachable.setAttribute('aria-label', 'Planted unreachable');
      unreachable.textContent = 'Planted unreachable';
      const invisible = document.createElement('button'); // focus with no visible indicator
      invisible.type = 'button';
      invisible.textContent = 'Planted invisible focus';
      invisible.style.setProperty('outline', 'none', 'important');
      invisible.style.setProperty('box-shadow', 'none', 'important');
      const trap = document.createElement('input'); // a keyboard trap: Tab never leaves it
      trap.setAttribute('aria-label', 'Planted trap');
      trap.addEventListener('keydown', (event) => {
        if (event.key === 'Tab') event.preventDefault();
      });
      main.append(image, field, faint, unreachable, invisible, trap);
    });

    const axe = await runAxe(page);
    expect(blocking(axe).map((v) => v.rule)).toEqual(
      expect.arrayContaining(['image-alt', 'label', 'color-contrast']),
    );

    const keyboard = await walkWithTab(page);
    expect(keyboard.unreached).toContain('button "Planted unreachable"');
    expect(keyboard.noVisibleFocus).toContain('button "Planted invisible focus"');
    expect(keyboard.trapped).toContain('input "Planted trap"');

    await page.evaluate(() => {
      const wide = document.createElement('div'); // reflow (WCAG 1.4.10): wider than 320 px, in no scroll container
      wide.className = 'planted-wide';
      wide.textContent = 'Planted wide';
      wide.style.width = '600px';
      (document.querySelector('main') ?? document.body).append(wide);
    });
    const reflow = await reflowAt(page);
    expect(reflow.scrollWidth).toBeGreaterThan(reflow.width);
    expect(reflow.overflowing).toContain('div.planted-wide "Planted wide"');
  } finally {
    await context.close();
  }
});

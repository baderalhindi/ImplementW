import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { type Browser, type Page } from '@playwright/test';

import { PlatformApi } from '../support/api.ts';
import { contextIn, expect, signedInContext, test, type Language } from '../support/journey.ts';
import {
  blocking,
  languageOfParts,
  reflowAt,
  reviewContrast,
  runAxe,
  walkWithTab,
} from './audit.ts';
import { AUDIT_DATA_FILE, SCREENS, type AuditData, type Screen } from './screens.ts';

// TASK-087 — Execute Platform-Wide Accessibility Audit (docs/testing/accessibility-audit.md).
//
// Every audited screen, in Arabic (RTL) and in English (LTR), opened as the person who uses it most, with the data
// audit-data.setup.ts created: axe-core at WCAG 2.1 AA (the working baseline until AHDA confirms PTBC-040, OQ-010) and a Tab walk through every control. Each run
// writes one JSON record per screen and language to test-results/accessibility, which `accessibility-audit.py --record` turns
// into the recorded run the report cites. A screen fails on any Critical or Serious baseline violation that AHDA has not
// accepted as a deferral (accepted-deferrals.json), contrast measured below 1.4.3 where axe could not decide, any control Tab
// cannot reach, a keyboard trap, a focus with no visible indicator, focus escaping a modal or not returning when it closes,
// or content that does not reflow at 320 px.

const OUT = resolve(import.meta.dirname, '../../test-results/accessibility');
const LANGUAGES: Language[] = ['ar', 'en'];

interface Deferral {
  finding: string;
  rule: string;
  screens: string[];
  acceptedBy: string;
  reference: string;
}

const deferrals = JSON.parse(
  readFileSync(resolve(import.meta.dirname, 'accepted-deferrals.json'), 'utf8'),
) as Deferral[];

/** Read when a screen opens, not when the file loads: audit-data.setup.ts writes it after the tests are listed. */
const auditData = (): AuditData => JSON.parse(readFileSync(AUDIT_DATA_FILE, 'utf8')) as AuditData;

/** The screen has rendered its content: its heading, no loading state, and the language's direction on <html>. */
async function settled(page: Page, language: Language): Promise<void> {
  await expect(page.locator('html')).toHaveAttribute('lang', language);
  await expect(page.locator('html')).toHaveAttribute('dir', language === 'ar' ? 'rtl' : 'ltr');
  await expect(page.locator('h1').first()).toBeVisible();
  await expect(page.locator('.state--loading')).toHaveCount(0);
  await page.waitForLoadState('networkidle');
}

async function open(
  browser: Browser,
  screen: Screen,
  language: Language,
): Promise<{ page: Page; close: () => Promise<void> }> {
  if (screen.persona === null) {
    const context = await contextIn(browser, language);
    return { page: await context.newPage(), close: () => context.close() };
  }
  const api = await PlatformApi.signIn(screen.persona);
  const context = await signedInContext(browser, api, language);
  return {
    page: await context.newPage(),
    close: async () => {
      await context.close();
      await api.dispose();
    },
  };
}

for (const language of LANGUAGES) {
  for (const screen of SCREENS) {
    test(`${screen.id} ${screen.name} [${language}]`, async ({ browser }, testInfo) => {
      const { page, close } = await open(browser, screen, language);
      try {
        await page.goto(screen.path(auditData()));
        await settled(page, language);
        if (screen.state !== undefined) {
          // Remember what had focus outside any modal, so focus can be checked to come back to it on close.
          await page.evaluate(() => {
            document.addEventListener('focusin', (event) => {
              if (
                !(event.target instanceof Element) ||
                event.target.closest('dialog[open]') === null
              )
                (window as unknown as { a11yOpener: unknown }).a11yOpener = event.target;
            });
          });
          await screen.state.enter(page);
          await page.waitForLoadState('networkidle');
        }
        const modal = screen.state?.modal === true;
        // The screen at rest first: its accessibility tree (the screen-reader review, §5) and how it looks.
        const name = `${screen.id}.${language}`;
        const ariaSnapshot = await page.locator('body').ariaSnapshot();
        const screenshot = await page.screenshot({ fullPage: true });
        const axe = await runAxe(page);
        const contrast = await reviewContrast(page, axe);
        const languageParts = await languageOfParts(page, language);
        const keyboard = await walkWithTab(page, modal ? 'dialog[open]' : 'body');
        let focusReturned: boolean | null = null;
        if (modal) {
          await page.keyboard.press('Escape');
          await expect(page.locator('dialog[open]')).toHaveCount(0);
          focusReturned = await page.evaluate(
            () =>
              document.activeElement === (window as unknown as { a11yOpener: unknown }).a11yOpener,
          );
        }
        // Last, as it resizes the page: a modal state is measured closed, as its screen.
        const reflow = await reflowAt(page);
        await testInfo.attach(`${name}.png`, { body: screenshot, contentType: 'image/png' });
        writeFileSync(resolve(OUT, `${name}.aria.yml`), ariaSnapshot);
        writeFileSync(resolve(OUT, `${name}.png`), screenshot);
        writeFileSync(
          resolve(OUT, `${name}.json`),
          JSON.stringify(
            {
              screen: {
                id: screen.id,
                name: screen.name,
                scr: screen.scr,
                basis: screen.basis,
                topTen: screen.topTen,
                persona: screen.persona,
              },
              language,
              url: new URL(page.url()).pathname,
              title: await page.title(),
              recordedAt: new Date().toISOString(),
              state: screen.state === undefined ? null : { modal },
              axe,
              contrast,
              languageParts,
              keyboard,
              focusReturned,
              reflow,
            },
            null,
            2,
          ),
        );

        const outstanding = blocking(axe).filter(
          (v) => !deferrals.some((d) => d.rule === v.rule && d.screens.includes(screen.id)),
        );
        expect
          .soft(
            outstanding.map(
              (v) => `${v.impact} ${v.rule}: ${v.targets.map((t) => t.selector).join(', ')}`,
            ),
            'Critical/Serious WCAG 2.1 AA violations',
          )
          .toEqual([]);
        expect
          .soft(
            contrast.filter((c) => !c.passes),
            'contrast axe left undecided, measured below WCAG 1.4.3',
          )
          .toEqual([]);
        expect.soft(keyboard.unreached, 'controls Tab never reaches').toEqual([]);
        expect.soft(keyboard.trapped, 'keyboard traps').toEqual([]);
        expect.soft(keyboard.noVisibleFocus, 'focus without a visible indicator').toEqual([]);
        expect
          .soft(keyboard.escaped, 'focus that left the modal for the page behind it')
          .toEqual([]);
        if (modal) expect.soft(focusReturned, 'focus back on the opener after Escape').toBe(true);
        expect
          .soft(reflow.overflowing, 'content past 320 px outside its own scroll container (1.4.10)')
          .toEqual([]);
      } finally {
        await close();
      }
    });
  }
}

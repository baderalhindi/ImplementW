import {
  test as base,
  expect,
  type Browser,
  type BrowserContext,
  type Locator,
  type Page,
} from '@playwright/test';

import { PlatformApi } from './api.ts';
import { credentialOf, environment, type Persona } from './environment.ts';

// The journeys' test object (docs/testing/e2e-suite.md §6). `actor(persona)` signs a person in once and gives back their
// browser page and their API session, sharing one session as the SPA would: the page starts signed in with it. Contexts and
// sessions are closed when the test ends.

const SESSION_KEY = 'pmplatform.session'; // src/features/identity-access/session/sessionStore.ts
const LANGUAGE_KEY = 'pmplatform.language'; // src/shared/i18n/i18n.ts; the SPA's default is Arabic

export interface Actor {
  persona: Persona;
  page: Page;
  api: PlatformApi;
}

export type Language = 'ar' | 'en';

/** A browser context in one interface language; the SPA reads it from localStorage before its first render. */
export async function contextIn(browser: Browser, language: Language): Promise<BrowserContext> {
  const context = await browser.newContext({
    baseURL: environment.baseUrl(),
    locale: language === 'ar' ? 'ar-SA' : 'en-GB',
    ignoreHTTPSErrors: environment.target === 'compose', // the e2e stack's internal CA (playwright.config.ts)
  });
  await context.addInitScript(
    ([languageKey, value]) => {
      if (window.localStorage.getItem(languageKey) === null)
        window.localStorage.setItem(languageKey, value);
    },
    [LANGUAGE_KEY, language] as const,
  );
  return context;
}

/** A context whose tab starts signed in with the API session, in the given language. */
export async function signedInContext(
  browser: Browser,
  api: PlatformApi,
  language: Language = 'en',
): Promise<BrowserContext> {
  // The journeys read English labels; both locales are audited by the accessibility audit (docs/testing/accessibility-audit.md).
  const context = await contextIn(browser, language);
  // Only when the tab holds no session yet, so a token the SPA refreshes is never overwritten by the original on reload.
  await context.addInitScript(
    ([sessionKey, session]) => {
      if (window.sessionStorage.getItem(sessionKey) === null)
        window.sessionStorage.setItem(sessionKey, session);
    },
    [SESSION_KEY, JSON.stringify(api.session)] as const,
  );
  return context;
}

export const test = base.extend<{ actor: (persona: Persona) => Promise<Actor>; visitor: Page }>({
  /** A browser tab with no session: someone about to sign in through the form. */
  visitor: async ({ browser }, use) => {
    const context = await contextIn(browser, 'en');
    await use(await context.newPage());
    await context.close();
  },
  actor: async ({ browser }, use) => {
    const opened: { context: BrowserContext; api: PlatformApi }[] = [];
    await use(async (persona) => {
      const api = await PlatformApi.signIn(persona);
      const context = await signedInContext(browser, api);
      opened.push({ context, api });
      return { persona, page: await context.newPage(), api };
    });
    for (const { context, api } of opened) {
      await context.close();
      await api.dispose();
    }
  },
});

export { expect };

/**
 * Signs in through the SPA's own form (SignInPage), second factor included: the path a person takes, proved once per
 * journey that starts from it. Leaves the page on the target route.
 */
export async function signInThroughTheForm(
  page: Page,
  persona: Persona,
  target = '/',
): Promise<void> {
  const credential = credentialOf(persona);
  await page.goto('/sign-in');
  await page.getByLabel('Username').fill(credential.username);
  await page.getByLabel('Password').fill(credential.password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  if (credential.mfaCode !== undefined) {
    await page.getByLabel('Verification code').fill(credential.mfaCode);
    await page.getByRole('button', { name: 'Verify' }).click();
  }
  await expect(page).not.toHaveURL(/\/sign-in/);
  if (target !== '/') await page.goto(target);
}

/** Translated parameters are wrapped in U+2068/U+2069 (src/shared/i18n); compare text without them. */
export function plain(text: string | null): string {
  return (text ?? '').replace(/[⁨⁩]/g, '').replace(/\s+/g, ' ').trim();
}

/** Waits until the element's text, without the isolates, contains `text`: getByText cannot see past them. */
export async function expectText(locator: Locator, text: string): Promise<void> {
  await expect
    .poll(async () => plain(await locator.first().textContent()), { message: `text "${text}"` })
    .toContain(text);
}

/** A label unique to this run, so journeys never meet each other's records on a shared environment. */
export function unique(label: string): string {
  return `${label} ${new Date().toISOString().slice(0, 19).replace('T', ' ')} ${Math.random().toString(36).slice(2, 6)}`;
}

/** Today's date in UTC, the API's "today" (TASK-053), plus a number of days, as yyyy-mm-dd. */
export function utcDate(daysFromToday = 0): string {
  const date = new Date();
  date.setUTCDate(date.getUTCDate() + daysFromToday);
  return date.toISOString().slice(0, 10);
}

import { render, type RenderResult } from '@testing-library/react';
import axe from 'axe-core';
import { createMemoryRouter, RouterProvider } from 'react-router';

import { appRoutes } from '@/app/routes.tsx';
import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { sessionStore } from '@/features/identity-access/session/sessionStore.ts';
import { type Language } from '@/shared/i18n/i18n.ts';
import { I18nProvider } from '@/shared/i18n/I18nProvider.tsx';

interface RenderAppOptions {
  path: string;
  language?: Language;
  session?: Session | null;
}

/** The whole application (shell, guard, routes) at a path, as a browser would load it. */
export function renderApp({
  path,
  language = 'en',
  session = null,
}: RenderAppOptions): RenderResult & {
  router: ReturnType<typeof createMemoryRouter>;
} {
  sessionStore.signOut();
  if (session !== null) {
    sessionStore.setSession(session);
  }
  const router = createMemoryRouter(appRoutes, { initialEntries: [path] });
  const result = render(
    <I18nProvider initialLanguage={language}>
      <RouterProvider router={router} />
    </I18nProvider>,
  );
  return { ...result, router };
}

/**
 * axe-core over the rendered document. jsdom does no layout or painting, so colour contrast is not measurable here:
 * it is checked by Lighthouse in a real browser (TASK-032 record, verification).
 */
export async function accessibilityViolations(): Promise<axe.Result[]> {
  const results = await axe.run(document, {
    rules: { 'color-contrast': { enabled: false } },
  });
  return results.violations;
}

export function describeViolations(violations: axe.Result[]): string {
  return violations
    .map((v) => `${v.id}: ${v.help}\n  ${v.nodes.map((n) => n.html).join('\n  ')}`)
    .join('\n');
}

import { screen } from '@testing-library/react';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { mockApi, problem } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

import { sessionStore } from './sessionStore.ts';

const en = (key: string) => translate('en', key);

// DEV-D1-01: a session that ends on its own (idle past the refresh lifetime, revoked by reuse) or in another tab lands
// on the sign-in page with a sentence saying so, not on a bare form.
describe('the sign-in page after a session ended', () => {
  test('says the session has ended when the refresh is refused', async () => {
    mockApi()
      .on('GET', /.*/, problem(401, 'AUTHENTICATION_REQUIRED'))
      .on('POST', /^\/sessions\/current\/refresh$/, problem(401, 'AUTHENTICATION_REQUIRED'));
    const { router } = renderApp({ path: '/', session: sessionFor(['R02']) });

    expect(await screen.findByText(en('identityAccess.signIn.sessionEnded'))).toBeTruthy();
    expect(router.state.location.pathname).toBe('/sign-in');
  });

  test('says nothing after the person signs out in this tab', async () => {
    mockApi().on('POST', /^\/sessions\/current\/sign-out$/, { status: 204 });
    renderApp({ path: '/sign-in', session: sessionFor(['R02']) });

    await sessionStore.endSession();

    expect(
      await screen.findByRole('heading', { level: 1, name: en('identityAccess.signIn.title') }),
    ).toBeTruthy();
    expect(screen.queryByText(en('identityAccess.signIn.sessionEnded'))).toBeNull();
    expect(screen.queryByText(en('identityAccess.signIn.signedOutElsewhere'))).toBeNull();
  });
});

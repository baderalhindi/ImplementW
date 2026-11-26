import { screen, waitFor } from '@testing-library/react';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { mockApi } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

// A business screen opens only for a holder of the permission it reads or writes under, as the administration screens do
// (RequirePermission): any other role is shown "Access denied", not the screen and then the API's 403 (QA D1-04, D2-03).
// The roles' permissions are the shipped matrix (permissionFixtures.ts). Navigation, not protection: the API decides.

const en = (key: string) => translate('en', key);

const DENIED: [path: string, roles: string[]][] = [
  ['/projects', ['R01']],
  ['/projects/new', ['R01', 'R05', 'R06', 'R07', 'NONE']],
  ['/projects/import', ['R01', 'NONE']],
  ['/risks', ['R01']],
  ['/tasks', ['R01']],
  ['/documents', ['R01', 'R08', 'NONE']],
  ['/documents/recent', ['R01', 'NONE']],
  ['/documents/8d8d8d8d-0000-4000-8000-000000000014', ['R01', 'NONE']],
  ['/external-requests', ['R01', 'R06', 'R07', 'NONE']],
  ['/external-requests/new', ['R01', 'R06']],
  ['/external-requests/monitor', ['R01', 'R06', 'R07', 'NONE']],
  ['/external-requests/applications', ['R01', 'R06', 'R07', 'NONE']],
  ['/reports/explorer', ['R01', 'R04', 'R05', 'R06', 'NONE']],
  ['/reports/saved-views', ['R01', 'R04', 'R05', 'R06', 'NONE']],
];

/** The shell's own reads on every screen: unread count, audit navigation, banner images. */
const SHELL_PATHS = ['/notifications/unread-count', '/audit-access', '/banner-images'];

const CASES = DENIED.flatMap(([path, roles]) => roles.map((role) => [path, role] as const));

function sessionOf(role: string) {
  return sessionFor(role === 'NONE' ? [] : [role]);
}

describe('business screens are guarded by the permission they need', () => {
  test.each(CASES)('%s is denied to %s and calls no API of its own', async (path, role) => {
    const api = mockApi();
    renderApp({ path, session: sessionOf(role) });

    expect(
      await screen.findByRole('heading', { level: 1, name: en('common.forbidden.title') }),
    ).toBeTruthy();
    expect(api.requests.filter((request) => !SHELL_PATHS.includes(request.path))).toEqual([]);
  });

  test.each([
    ['/projects/new', 'R04'],
    ['/projects/new', 'R02'],
    ['/reports/explorer', 'R03'],
    ['/external-requests', 'R05'],
  ])('%s opens for %s, who holds its permission', async (path, role) => {
    const api = mockApi();
    renderApp({ path, session: sessionOf(role) });

    // Past the guard, the screen asks the API for what it shows.
    await waitFor(() => {
      expect(api.requests.filter((request) => !SHELL_PATHS.includes(request.path))).not.toEqual([]);
    });
    expect(
      screen.queryByRole('heading', { level: 1, name: en('common.forbidden.title') }),
    ).toBeNull();
  });
});

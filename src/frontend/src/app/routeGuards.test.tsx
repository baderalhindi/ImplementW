import { screen, waitFor } from '@testing-library/react';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { entitySession } from '@/test/projectFixtures.ts';
import { mockApi } from '@/test/mockApi.ts';
import { renderApp } from '@/test/renderApp.tsx';

// A business screen opens only for a holder of the permission it reads or writes under, as the administration screens do
// (RequirePermission): any other role is shown "Access denied", not the screen and then the API's 403 (QA D1-04, D2-03).
// The roles' permissions are the shipped matrix (permissionFixtures.ts). Navigation, not protection: the API decides.

const en = (key: string) => translate('en', key);

/** Any record: the guard decides before the record is asked for. */
const RECORD_ID = '9a9a9a9a-0000-4000-8000-000000000001';

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
  // QA INT-05: deep links outside a role's menu.
  ['/milestones', ['R01', 'R08', 'NONE']],
  ['/kpis', ['R01', 'NONE']],
  ['/issues-challenges', ['R01', 'NONE']],
  ['/issues-challenges/challenges', ['R01', 'NONE']],
  ['/issues-challenges/escalations', ['R01', 'NONE']],
  [`/issues-challenges/escalations/${RECORD_ID}`, ['R01', 'NONE']],
  ['/change-requests', ['R01', 'R05', 'R08', 'NONE']],
  [`/change-requests/${RECORD_ID}`, ['R01', 'R05', 'R08', 'NONE']],
  ['/change-requests/new', ['R01', 'R05', 'R06', 'R07', 'NONE']],
  ['/suspension-requests', ['R01', 'R08', 'NONE']],
  [`/suspension-requests/${RECORD_ID}`, ['R01', 'R08', 'NONE']],
  ['/suspension-requests/new', ['R01', 'R05', 'R06', 'R07']],
  ['/closure-requests', ['R01', 'R08', 'NONE']],
  [`/closure-requests/completion/${RECORD_ID}`, ['R01', 'R08', 'NONE']],
  ['/closure-requests/new', ['R01', 'R05', 'R06', 'R07']],
  ['/approvals/inbox', ['R01', 'R04', 'R05', 'R06', 'R08', 'NONE']],
  ['/approvals/delegations', ['R01', 'R04', 'R05', 'R06', 'R08', 'NONE']],
  ['/approvals/requests', ['R01', 'R05', 'R06', 'R08', 'NONE']],
  [`/approvals/instances/${RECORD_ID}`, ['R01', 'R05', 'R06', 'R08', 'NONE']],
  [`/projects/${RECORD_ID}`, ['R01', 'NONE']],
  [`/projects/${RECORD_ID}/activity-history`, ['R01', 'NONE']],
  [`/projects/${RECORD_ID}/audit-history`, ['R01', 'NONE']],
  [`/projects/${RECORD_ID}/card`, ['R01', 'NONE']],
  [`/projects/${RECORD_ID}/edit`, ['R01', 'R05', 'R06', 'R07', 'NONE']],
  ['/reports', ['R01', 'NONE']],
  ['/reports/portfolio', ['R01', 'NONE']],
  [`/external-requests/${RECORD_ID}`, ['R06', 'R07', 'NONE']],
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

  test('the denial says the page is not the person’s, not that it is for administrators (QA DEV-D2-01)', async () => {
    mockApi();
    renderApp({ path: '/risks', session: sessionOf('R01') });

    await screen.findByRole('heading', { level: 1, name: en('common.forbidden.title') });
    expect(screen.getByText(en('common.forbidden.description')).textContent).toBe(
      "You don't have access to this page. Choose another screen from the menu.",
    );
    expect(document.body.textContent).not.toContain('administrators');
  });

  test.each(['/reports', '/reports/portfolio', '/reports/exports'])(
    '%s is denied to an external user, who runs no report (ADR-019), and calls no API of its own',
    async (path) => {
      const api = mockApi();
      renderApp({ path, session: entitySession() });

      expect(
        await screen.findByRole('heading', { level: 1, name: en('common.forbidden.title') }),
      ).toBeTruthy();
      expect(api.requests.filter((request) => !SHELL_PATHS.includes(request.path))).toEqual([]);
    },
  );

  test.each([
    ['/projects/new', 'R04'],
    ['/projects/new', 'R02'],
    ['/reports/explorer', 'R03'],
    ['/external-requests', 'R05'],
    ['/milestones', 'R05'],
    ['/kpis', 'R08'],
    ['/issues-challenges/escalations', 'R07'],
    ['/suspension-requests', 'R06'],
    ['/closure-requests', 'R05'],
    ['/change-requests', 'R06'],
    ['/approvals/requests', 'R04'],
    ['/approvals/inbox', 'R07'],
    [`/projects/${RECORD_ID}`, 'R08'],
    ['/reports', 'R06'],
    ['/reports/exports', 'R06'],
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

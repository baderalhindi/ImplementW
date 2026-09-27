import { screen, waitFor } from '@testing-library/react';
import { describe, expect, test } from 'vitest';

import {
  departments,
  entities,
  internalUser,
  roles,
  sessionFor,
  withIdentityAccessData,
} from '@/test/identityAccessFixtures.ts';
import { mockApi, page } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';
import { translate } from '@/shared/i18n/i18n.ts';

// Acceptance criteria 1 and 3: every ADM-002–013 screen is reachable for R01 and renders in both English (LTR)
// and Arabic (RTL), with no axe-core violation in either.

const R01 = sessionFor(['R01']);

const SCREENS = [
  { id: 'ADM-002', path: '/admin/users', title: 'identityAccess.users.list.title' },
  { id: 'ADM-003', path: `/admin/users/${internalUser.id}`, title: null },
  { id: 'ADM-004', path: '/admin/users/new', title: 'identityAccess.users.create.title' },
  {
    id: 'ADM-005',
    path: `/admin/users/${internalUser.id}/edit`,
    title: 'identityAccess.users.edit.title',
  },
  { id: 'ADM-006', path: '/admin/roles', title: 'identityAccess.roles.list.title' },
  { id: 'ADM-007', path: `/admin/roles/${roles[1]?.id ?? ''}`, title: null },
  {
    id: 'ADM-008',
    path: `/admin/roles/${roles[1]?.id ?? ''}/edit`,
    title: 'identityAccess.roles.edit.title',
  },
  {
    id: 'ADM-009',
    path: '/admin/permission-matrix',
    title: 'identityAccess.permissionMatrix.title',
  },
  { id: 'ADM-010', path: '/admin/role-assignments', title: 'identityAccess.assignments.title' },
  { id: 'ADM-011', path: '/admin/organization', title: 'identityAccess.organization.title' },
  {
    id: 'ADM-012',
    path: '/admin/organization/departments',
    title: 'identityAccess.departments.title',
  },
  { id: 'ADM-013', path: '/admin/organization/entities', title: 'identityAccess.entities.title' },
] as const;

describe.each(['en', 'ar'] as const)('in %s', (language) => {
  test.each(SCREENS)(
    '$id renders for R01 with the document direction and no axe violation',
    async ({ path, title }) => {
      withIdentityAccessData(mockApi());
      renderApp({ path, language, session: R01 });

      const heading = await screen.findByRole('heading', { level: 1 });
      if (title !== null) {
        expect(heading.textContent).toBe(translate(language, title));
      }
      await waitFor(() => {
        expect(screen.queryByText(translate(language, 'common.states.loading'))).toBeNull();
      });
      expect(document.documentElement.lang).toBe(language);
      expect(document.documentElement.dir).toBe(language === 'ar' ? 'rtl' : 'ltr');

      const violations = await accessibilityViolations();
      expect(violations, describeViolations(violations)).toEqual([]);
    },
  );
});

describe('empty states for zero-result lists', () => {
  test.each([
    ['/admin/users', /^\/users$/, 'identityAccess.users.list.empty'],
    ['/admin/role-assignments', /^\/access-relationships$/, 'identityAccess.assignments.empty'],
    ['/admin/organization/departments', /^\/departments$/, 'identityAccess.departments.empty'],
    ['/admin/organization/entities', /^\/external-entities$/, 'identityAccess.entities.empty'],
  ] as const)('%s shows its empty state', async (path, collection, message) => {
    withIdentityAccessData(mockApi()).on('GET', collection, { body: page([]) });
    renderApp({ path, session: R01 });

    expect(await screen.findByText(translate('en', message))).toBeTruthy();
  });

  test('a filtered user list with no match says so, not that there are no users', async () => {
    withIdentityAccessData(mockApi()).on('GET', /^\/users$/, { body: page([]) });
    renderApp({ path: '/admin/users?status=DISABLED', session: R01 });

    expect(
      await screen.findByText(translate('en', 'identityAccess.users.list.emptyFiltered')),
    ).toBeTruthy();
  });
});

test('the organization structure nests a department under its parent', async () => {
  withIdentityAccessData(mockApi());
  renderApp({ path: '/admin/organization', session: R01 });

  const child = await screen.findByText(/Quality Assurance/);
  const parentItem = screen.getByText(/Project Management Office/).closest('li');
  expect(parentItem?.contains(child)).toBe(true);
  expect(screen.getByText(/Contractor A/)).toBeTruthy();
  expect(departments).toHaveLength(2);
  expect(entities).toHaveLength(1);
});

test('the permission matrix shows the scope each profile grants', async () => {
  withIdentityAccessData(mockApi());
  renderApp({ path: '/admin/permission-matrix', session: R01 });

  const row = (await screen.findByText('Manage users')).closest('tr');
  const cells = Array.from(row?.querySelectorAll('td') ?? []).map((cell) => cell.textContent);
  expect(cells).toEqual(['All', '—Not granted', '—Not granted']);
});

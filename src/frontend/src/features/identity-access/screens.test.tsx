import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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

// Acceptance criteria 1 and 3: every ADM-002–013 screen (ADM-011 dropped in the 10 Nov 2026 client review) is reachable for R01 and renders in both English (LTR)
// and Arabic (RTL), with no axe-core violation in either.

const R01 = sessionFor(['R01']);

function first<T>(items: readonly T[]): T {
  const [item] = items;
  if (item === undefined) {
    throw new Error('Nothing found.');
  }
  return item;
}

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

test('the old Organization structure address opens Departments, which names each parent', async () => {
  withIdentityAccessData(mockApi());
  renderApp({ path: '/admin/organization', session: R01 });

  expect(
    await screen.findByRole('heading', {
      level: 1,
      name: translate('en', 'identityAccess.departments.title'),
    }),
  ).toBeTruthy();
  const childRow = (await screen.findByText('Quality Assurance', { selector: 'td' })).closest('tr');
  expect(childRow?.textContent).toContain('Project Management Office');
  expect(
    screen.queryByRole('link', { name: translate('en', 'identityAccess.nav.departments') }),
  ).toBeTruthy();
  expect(departments).toHaveLength(2);
  expect(entities).toHaveLength(1);
});

test('external entities: select all, then one command for every chosen entity that allows it', async () => {
  const api = withIdentityAccessData(mockApi());
  const second = {
    ...first(entities),
    id: '00000000-0000-4000-8000-0000000000e2',
    code: 'CONTRACTOR-B',
    name: { ar: 'ب', en: 'Contractor B' },
    status: 'SUSPENDED' as const,
  };
  api.on('GET', /^\/external-entities$/, { body: page([...entities, second]) });
  api.on('POST', /^\/external-entities\/[^/]+\/suspend$/, { body: first(entities) });
  renderApp({ path: '/admin/organization/entities', session: R01 });
  const user = userEvent.setup();

  await user.click(
    await screen.findByRole('checkbox', { name: translate('en', 'common.table.selectAll') }),
  );
  expect(screen.getByText(translate('en', 'common.table.selected', { count: 2 }))).toBeTruthy();
  const bar = screen
    .getByText(translate('en', 'common.table.selected', { count: 2 }))
    .closest('.selection-bar');
  if (!(bar instanceof HTMLElement)) {
    throw new Error('No selection bar.');
  }
  await user.click(
    within(bar).getByRole('button', {
      name: translate('en', 'identityAccess.entities.transitions.suspend.action'),
    }),
  );
  const dialog = screen.getByRole('dialog');
  expect(dialog.textContent).toContain('Contractor A');
  expect(dialog.textContent).not.toContain('Contractor B');
  await user.click(
    within(dialog).getByRole('button', {
      name: translate('en', 'identityAccess.entities.transitions.suspend.action'),
    }),
  );
  await waitFor(() => {
    expect(api.requestsTo('POST', /\/suspend$/)).toHaveLength(1);
  });
  expect(api.requestsTo('POST', /\/suspend$/)[0]?.path).toBe(
    `/external-entities/${first(entities).id}/suspend`,
  );
});

test('the permission matrix shows the scope each profile grants', async () => {
  withIdentityAccessData(mockApi());
  renderApp({ path: '/admin/permission-matrix', session: R01 });

  const row = (await screen.findByText('Manage users')).closest('tr');
  const cells = Array.from(row?.querySelectorAll('td') ?? []).map((cell) => cell.textContent);
  expect(cells).toEqual(['All', '—Not granted', '—Not granted']);
});

test('the permission matrix heads each permission group as a row group (WCAG 1.3.1, TASK-087 A11Y-F-001)', async () => {
  withIdentityAccessData(mockApi());
  renderApp({ path: '/admin/permission-matrix', session: R01 });

  const row = (await screen.findByText('Manage users')).closest('tr');
  const group = row?.closest('tbody')?.querySelector('tr.matrix__group th');
  expect(group?.textContent).toBe('IDENTITY_ACCESS');
  expect(group?.getAttribute('scope')).toBe('rowgroup');
  expect(document.querySelector('th[scope="colgroup"]')).toBeNull();
});

describe('master data', () => {
  const catalogues = [
    { id: 'c-region', code: 'REGION', name: { ar: 'المناطق', en: 'Regions' } },
    { id: 'c-priority', code: 'PRIORITY', name: { ar: 'الأولويات', en: 'Priorities' } },
  ].map((catalogue) => ({
    ...catalogue,
    allowsHierarchy: false,
    isSystem: true,
    parentCatalogueId: null,
  }));

  test('the catalogue is chosen outside the filters: a required choice, always visible', async () => {
    const api = withIdentityAccessData(mockApi())
      .on('GET', /^\/master-data-catalogues$/, { body: catalogues })
      .on('GET', /^\/master-data-items$/, { body: page([]) })
      .on('GET', /^\/project-import\/value-requests$/, { body: page([]) })
      .on('GET', /value-requests/, { body: page([]) });
    renderApp({ path: '/admin/master-data', session: R01 });
    const user = userEvent.setup();

    const select = await screen.findByRole('combobox', { name: /Catalogue|List/ });
    expect(select.closest('[hidden]')).toBeNull();
    expect(screen.queryByRole('button', { name: /^Filters/ })).toBeNull();
    await user.selectOptions(select, 'PRIORITY');

    await waitFor(() => {
      expect(
        api
          .requestsTo('GET', /^\/master-data-items$/)
          .at(-1)
          ?.query.get('catalogueId'),
      ).toBe('c-priority');
    });
  });
});

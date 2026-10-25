import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import {
  ADMIN_ID,
  profiles,
  roles,
  sessionFor,
  withIdentityAccessData,
} from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi, page, problem } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

import { type PermissionProfileDetail, type PermissionProfileVersion } from '../api/types.ts';

// ADM-008 permission profile authoring (TASK-110; the 24 Oct 2026 review, C4): a version's grid beside its base role's shipped
// default, edited by its author while DRAFT, and the governed steps offered by state.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const R01 = sessionFor(['R01']);
const administrator =
  roles[0] ??
  (() => {
    throw new Error('The fixtures have no R01.');
  })();
const OTHER_ADMIN = '00000000-0000-4000-8000-000000000099';

function version(overrides: Partial<PermissionProfileVersion> = {}): PermissionProfileVersion {
  return {
    id: 'version-custom-1',
    permissionProfileId: 'profile-custom',
    versionNo: 1,
    lifecycleState: 'DRAFT',
    publishedAt: null,
    retiredAt: null,
    grants: [{ permissionId: 'p-user-view', permissionCode: 'USER_VIEW', dataScope: 'ALL' }],
    changeSummary: null,
    createdBy: ADMIN_ID,
    validatedByUserId: null,
    publishedByUserId: null,
    ...overrides,
  };
}

function withCustomProfile(api: MockApi, custom: PermissionProfileVersion): MockApi {
  const detail: PermissionProfileDetail = {
    id: 'profile-custom',
    code: 'ADMIN_READER',
    name: { ar: 'قارئ الإدارة', en: 'Administration reader' },
    baseRoleId: administrator.id,
    baseRoleCode: administrator.code,
    isShippedDefault: false,
    versions: [custom],
  };
  return withIdentityAccessData(api)
    .on('GET', /^\/permission-profiles$/, { body: page([...profiles, detail], 200) })
    .on('GET', /^\/permission-profiles\/profile-custom$/, { body: detail })
    .on('GET', /^\/permission-profile-versions\/version-custom-1$/, {
      body: custom,
      headers: { ETag: '"5"' },
    });
}

/** The table row of a permission, by its code. */
function rowOf(code: string): HTMLElement {
  const row = screen.getByText(code).closest('tr');
  if (row === null) {
    throw new Error(`No row for ${code}.`);
  }
  return row;
}

const scopeOf = (code: string) =>
  screen.getByRole('combobox', {
    name: en('identityAccess.profiles.version.scopeOf', { permission: code }),
  });

describe('a permission profile version', () => {
  test('its author edits the DRAFT grid, compared with the shipped default, and saves the whole grid with If-Match', async () => {
    const api = withCustomProfile(mockApi(), version()).on(
      'PUT',
      /^\/permission-profile-versions\/version-custom-1\/grants$/,
      {
        body: version({ grants: [] }),
        headers: { ETag: '"6"' },
      },
    );
    renderApp({ path: '/admin/permission-profile-versions/version-custom-1', session: R01 });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'R01 — Administration reader' }),
    ).toBeTruthy();
    // The shipped R01 default also holds USER_MANAGE: the one difference.
    const manage = rowOf('USER_MANAGE');
    expect(within(manage).getByText(en('identityAccess.profiles.version.changed'))).toBeTruthy();
    expect(
      within(rowOf('USER_VIEW')).queryByText(en('identityAccess.profiles.version.changed')),
    ).toBeNull();

    fireEvent.change(scopeOf('USER_MANAGE'), { target: { value: 'ALL' } });
    expect(within(manage).queryByText(en('identityAccess.profiles.version.changed'))).toBeNull();
    fireEvent.change(
      screen.getByRole('textbox', { name: en('identityAccess.profiles.fields.changeSummary') }),
      {
        target: { value: 'Match the default.' },
      },
    );
    fireEvent.click(
      screen.getByRole('button', { name: en('identityAccess.profiles.version.save') }),
    );

    await waitFor(() => {
      expect(api.requestsTo('PUT', /\/grants$/)).toHaveLength(1);
    });
    const [put] = api.requestsTo('PUT', /\/grants$/);
    expect(put?.headers.get('If-Match')).toBe('"5"');
    expect(put?.body).toEqual({
      grants: [
        { permissionCode: 'USER_VIEW', dataScope: 'ALL' },
        { permissionCode: 'USER_MANAGE', dataScope: 'ALL' },
      ],
      changeSummary: { text: 'Match the default.', language: 'en' },
    });
  });

  test('a DRAFT someone else wrote is read only, and offered for validation', async () => {
    const api = withCustomProfile(mockApi(), version({ createdBy: OTHER_ADMIN })).on(
      'POST',
      /^\/permission-profile-versions\/version-custom-1\/validate$/,
      problem(422, 'IDENTITY_ACCESS_SELF_APPROVAL'),
    );
    renderApp({ path: '/admin/permission-profile-versions/version-custom-1', session: R01 });

    await screen.findByRole('heading', { level: 1, name: 'R01 — Administration reader' });
    expect(screen.queryByRole('combobox', { name: /USER_VIEW/ })).toBeNull();
    expect(
      screen.queryByRole('button', { name: en('identityAccess.profiles.version.save') }),
    ).toBeNull();

    fireEvent.click(
      screen.getByRole('button', { name: en('identityAccess.profiles.version.validate') }),
    );
    expect(await screen.findByText(en('identityAccess.problems.selfApproval'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/validate$/)[0]?.headers.get('If-Match')).toBe('"5"');
  });

  test('a VALIDATED version is published', async () => {
    const api = withCustomProfile(
      mockApi(),
      version({ lifecycleState: 'VALIDATED', validatedByUserId: OTHER_ADMIN }),
    ).on('POST', /^\/permission-profile-versions\/version-custom-1\/publish$/, {
      body: version({ lifecycleState: 'PUBLISHED' }),
      headers: { ETag: '"7"' },
    });
    renderApp({ path: '/admin/permission-profile-versions/version-custom-1', session: R01 });

    fireEvent.click(
      await screen.findByRole('button', { name: en('identityAccess.profiles.version.publish') }),
    );
    await waitFor(() => {
      expect(api.requestsTo('POST', /\/publish$/)).toHaveLength(1);
    });
  });

  test('a PUBLISHED version offers a new version and moving its holders onto it', async () => {
    withCustomProfile(
      mockApi(),
      version({
        lifecycleState: 'PUBLISHED',
        validatedByUserId: OTHER_ADMIN,
        publishedByUserId: OTHER_ADMIN,
      }),
    ).on('POST', /^\/permission-profile-versions\/version-custom-1\/assignment-migrations$/, {
      body: { permissionProfileVersionId: 'version-custom-1', migratedAssignments: 3 },
    });
    renderApp({ path: '/admin/permission-profile-versions/version-custom-1', session: R01 });
    expect(
      await screen.findByRole('button', { name: en('identityAccess.profiles.version.newVersion') }),
    ).toBeTruthy();
    fireEvent.click(
      screen.getByRole('button', { name: en('identityAccess.profiles.version.migrate') }),
    );
    expect(
      await screen.findByText(en('identityAccess.profiles.version.migrated', { count: 3 })),
    ).toBeTruthy();
  });

  test('has no axe violation', async () => {
    withCustomProfile(mockApi(), version());
    renderApp({ path: '/admin/permission-profile-versions/version-custom-1', session: R01 });
    await screen.findByRole('heading', { level: 1, name: 'R01 — Administration reader' });
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });
});

test('a new profile copies the chosen version, takes its base role from it, and opens the draft', async () => {
  const api = withCustomProfile(mockApi(), version()).on('POST', /^\/permission-profiles$/, {
    status: 201,
    body: version(),
    headers: { ETag: '"1"' },
  });
  renderApp({ path: '/admin/permission-profiles/new?sourceVersionId=version-R01-1', session: R01 });

  fireEvent.change(await screen.findByRole('textbox', { name: /Profile code/ }), {
    target: { value: 'ADMIN_READER' },
  });
  fireEvent.change(screen.getByRole('textbox', { name: /Name \(Arabic\)/ }), {
    target: { value: 'قارئ الإدارة' },
  });
  fireEvent.change(screen.getByRole('textbox', { name: /Name \(English\)/ }), {
    target: { value: 'Administration reader' },
  });
  fireEvent.click(
    screen.getByRole('button', { name: en('identityAccess.profiles.create.submit') }),
  );

  expect(
    await screen.findByRole('heading', { level: 1, name: 'R01 — Administration reader' }),
  ).toBeTruthy();
  expect(api.requestsTo('POST', /^\/permission-profiles$/)[0]?.body).toEqual({
    code: 'ADMIN_READER',
    name: { ar: 'قارئ الإدارة', en: 'Administration reader' },
    baseRoleId: administrator.id,
    sourceVersionId: 'version-R01-1',
  });
});

import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import {
  DEPARTMENT_ID,
  ENTITY_ID,
  externalUser,
  internalUser,
  PROJECT_ID,
  sessionFor,
  sponsorUser,
  withIdentityAccessData,
} from '@/test/identityAccessFixtures.ts';
import { mockApi, problem } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// MOD-081 Assign Role then MOD-082 Assign Department/Entity (ADM-010), for an internal user and for ADR-013's
// external user with a per-project grant and a named sponsor.

const en = (key: string) => translate('en', key);
const created = {
  id: 'new-assignment',
  permissionProfileId: 'profile-R03',
  permissionProfileCode: 'R03_DEFAULT',
  permissionProfileVersionNo: 1,
};

async function openAssignDialog(path: string, heading: string) {
  const api = withIdentityAccessData(mockApi());
  renderApp({ path, session: sessionFor(['R01']) });
  await screen.findByRole('heading', { level: 1, name: heading });
  const user = userEvent.setup();
  await user.click(screen.getByRole('button', { name: en('identityAccess.assignRole.open') }));
  const dialog = await screen.findByRole('dialog', { name: en('identityAccess.assignRole.title') });
  return { api, user, dialog };
}

describe('MOD-081 and MOD-082 assign role', () => {
  test('an internal user is given a published profile version, scoped to a department', async () => {
    const { api, user, dialog } = await openAssignDialog(
      `/admin/users/${internalUser.id}`,
      internalUser.displayName,
    );
    api.on('POST', /^\/access-relationships$/, { status: 201, body: created });

    await user.selectOptions(
      await within(dialog).findByRole('combobox', { name: /Role and permission profile/ }),
      'profile-R03',
    );
    const version = await within(dialog).findByRole('combobox', { name: /Profile version/ });
    expect((version as HTMLSelectElement).value).toBe('version-R03-1');
    await user.click(within(dialog).getByRole('button', { name: en('common.actions.next') }));

    expect(
      within(dialog).getByRole('heading', { name: en('identityAccess.assignScope.title') }),
    ).toBeTruthy();
    await user.selectOptions(
      within(dialog).getByRole('combobox', { name: /Department/ }),
      DEPARTMENT_ID,
    );
    await user.click(
      within(dialog).getByRole('button', { name: en('identityAccess.assignScope.submit') }),
    );

    expect(await screen.findByText(en('identityAccess.users.detail.notice.assigned'))).toBeTruthy();
    const [request] = api.requestsTo('POST', /^\/access-relationships$/);
    expect(request?.body).toEqual({
      userId: internalUser.id,
      permissionProfileVersionId: 'version-R03-1',
      departmentId: DEPARTMENT_ID,
      externalEntityId: null,
      projectId: null,
      sponsorUserId: null,
      startsAt: null,
      endsAt: null,
    });
  });

  test('MOD-081 will not continue without a profile', async () => {
    const { user, dialog } = await openAssignDialog(
      `/admin/users/${internalUser.id}`,
      internalUser.displayName,
    );

    await user.click(within(dialog).getByRole('button', { name: en('common.actions.next') }));

    const profile = within(dialog).getByRole('combobox', { name: /Role and permission profile/ });
    expect(profile.getAttribute('aria-invalid')).toBe('true');
    expect(
      within(dialog).queryByRole('heading', { name: en('identityAccess.assignScope.title') }),
    ).toBeNull();
  });

  test('ADR-013: an external user sees only external-eligible roles, keeps their entity and needs a sponsor', async () => {
    const { api, user, dialog } = await openAssignDialog(
      `/admin/users/${externalUser.id}`,
      externalUser.displayName,
    );
    api.on('POST', /^\/access-relationships$/, { status: 201, body: created });

    const profile = await within(dialog).findByRole('combobox', {
      name: /Role and permission profile/,
    });
    const offered = within(profile)
      .getAllByRole('option')
      .map((option) => option.getAttribute('value'));
    expect(offered).toEqual(['', 'profile-R04']);
    await user.selectOptions(profile, 'profile-R04');
    await within(dialog).findByRole('combobox', { name: /Profile version/ });
    await user.click(within(dialog).getByRole('button', { name: en('common.actions.next') }));

    expect(within(dialog).queryByRole('combobox', { name: /Department/ })).toBeNull();
    expect(within(dialog).getByText('Contractor A')).toBeTruthy();
    await user.type(within(dialog).getByRole('textbox', { name: /Project ID/ }), PROJECT_ID);
    await user.click(
      within(dialog).getByRole('button', { name: en('identityAccess.assignScope.submit') }),
    );
    expect(
      within(dialog)
        .getByRole('group', { name: /AHDA sponsor/ })
        .getAttribute('aria-invalid'),
    ).toBe('true');
    expect(api.requestsTo('POST', /^\/access-relationships$/)).toHaveLength(0);

    await user.type(within(dialog).getByRole('searchbox', { name: /Search by name/ }), 'Omar');
    await user.click(within(dialog).getByRole('button', { name: en('common.actions.search') }));
    await user.click(await within(dialog).findByRole('button', { name: /Omar Sponsor/ }));
    expect(
      api
        .requestsTo('GET', /^\/users$/)
        .at(-1)
        ?.query.get('userType'),
    ).toBe('INTERNAL');
    await user.click(
      within(dialog).getByRole('button', { name: en('identityAccess.assignScope.submit') }),
    );

    await waitFor(() => {
      expect(api.requestsTo('POST', /^\/access-relationships$/)).toHaveLength(1);
    });
    expect(api.requestsTo('POST', /^\/access-relationships$/)[0]?.body).toEqual({
      userId: externalUser.id,
      permissionProfileVersionId: 'version-R04-1',
      departmentId: null,
      externalEntityId: ENTITY_ID,
      projectId: PROJECT_ID,
      sponsorUserId: sponsorUser.id,
      startsAt: null,
      endsAt: null,
    });
  });

  test('an ADR-013 refusal lands on the fields the API names, and a MOD-081 field returns to MOD-081', async () => {
    const { api, user, dialog } = await openAssignDialog(
      `/admin/users/${internalUser.id}`,
      internalUser.displayName,
    );
    api.on(
      'POST',
      /^\/access-relationships$/,
      problem(422, 'IDENTITY_ACCESS_INVALID_PERIOD', [
        { field: 'startsAt', code: 'DATE_BEFORE_START' },
      ]),
    );

    await user.selectOptions(
      await within(dialog).findByRole('combobox', { name: /Role and permission profile/ }),
      'profile-R03',
    );
    await within(dialog).findByRole('combobox', { name: /Profile version/ });
    await user.click(within(dialog).getByRole('button', { name: en('common.actions.next') }));
    await user.click(
      within(dialog).getByRole('button', { name: en('identityAccess.assignScope.submit') }),
    );

    const startsAt = await within(dialog).findByLabelText(/Starts/);
    expect(startsAt.getAttribute('aria-invalid')).toBe('true');
    expect(within(dialog).getByRole('alert').textContent).toBe(
      en('identityAccess.problems.invalidPeriod'),
    );
  });

  test('ADM-010 assigns a role to a user chosen in the dialog, and the dialog has no axe violation', async () => {
    const { api, user, dialog } = await openAssignDialog(
      '/admin/role-assignments',
      en('identityAccess.assignments.title'),
    );
    api.on('POST', /^\/access-relationships$/, { status: 201, body: created });

    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);

    await user.type(within(dialog).getByRole('searchbox', { name: /Search by name/ }), 'Sara');
    await user.click(within(dialog).getByRole('button', { name: en('common.actions.search') }));
    await user.click(await within(dialog).findByRole('button', { name: /Sara Internal/ }));
    await user.selectOptions(
      within(dialog).getByRole('combobox', { name: /Role and permission profile/ }),
      'profile-R03',
    );
    await within(dialog).findByRole('combobox', { name: /Profile version/ });
    await user.click(within(dialog).getByRole('button', { name: en('common.actions.next') }));
    await user.click(
      within(dialog).getByRole('button', { name: en('identityAccess.assignScope.submit') }),
    );

    expect(await screen.findByText(en('identityAccess.users.detail.notice.assigned'))).toBeTruthy();
    expect(api.requestsTo('POST', /^\/access-relationships$/)[0]?.body).toMatchObject({
      userId: internalUser.id,
    });
  });

  test('ending an assignment asks first, then ends it', async () => {
    const api = withIdentityAccessData(mockApi()).on('POST', /\/end$/, {
      body: { ...created, status: 'ENDED' },
    });
    renderApp({ path: '/admin/role-assignments', session: sessionFor(['R01']) });
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'End assignment R03' }));
    const dialog = screen.getByRole('dialog', { name: en('identityAccess.assignments.endTitle') });
    await user.click(
      within(dialog).getByRole('button', { name: en('identityAccess.assignments.end') }),
    );

    expect(await screen.findByText(en('identityAccess.users.detail.notice.ended'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/end$/)[0]?.path).toBe(
      '/access-relationships/00000000-0000-4000-8000-0000000000c1/end',
    );
  });
});

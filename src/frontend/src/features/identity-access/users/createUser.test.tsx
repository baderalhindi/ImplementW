import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import {
  ENTITY_ID,
  internalUser,
  sessionFor,
  withIdentityAccessData,
} from '@/test/identityAccessFixtures.ts';
import { mockApi, problem } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// ADM-004 Create User: field-level validation, the saving state, the server's refusals on their fields, and the
// ADR-013 external user with their entity.

const R01 = sessionFor(['R01']);
const en = (key: string) => translate('en', key);

async function openCreateUser(language: 'en' | 'ar' = 'en') {
  const api = withIdentityAccessData(mockApi());
  renderApp({ path: '/admin/users/new', language, session: R01 });
  await screen.findByRole('heading', { level: 1 });
  return api;
}

describe('ADM-004 Create User', () => {
  test('an empty submit shows each required field error on its input and focuses the first', async () => {
    const api = await openCreateUser();
    const user = userEvent.setup();

    await user.click(screen.getByRole('button', { name: en('common.actions.save') }));

    expect(screen.getByRole('alert').textContent).toBe(
      translate('en', 'common.form.fixErrors', { count: 4 }),
    );
    const displayName = screen.getByRole('textbox', { name: /Full name/ });
    expect(displayName.getAttribute('aria-invalid')).toBe('true');
    expect(displayName.getAttribute('aria-describedby')).toContain('error');
    expect(document.activeElement).toBe(displayName);
    expect(screen.getAllByText(en('common.fieldErrors.required'))).toHaveLength(4);
    expect(api.requestsTo('POST', /^\/users$/)).toHaveLength(0);
  });

  test('a malformed email and mobile number are refused before the API is called', async () => {
    const api = await openCreateUser();
    const user = userEvent.setup();

    await user.type(screen.getByRole('textbox', { name: /Full name/ }), 'Noura Test');
    await user.type(screen.getByRole('textbox', { name: /Username/ }), 'noura.test');
    await user.type(screen.getByRole('textbox', { name: /Email/ }), 'not-an-email');
    await user.type(screen.getByRole('textbox', { name: /Mobile number/ }), '0501234567');
    await user.type(screen.getByRole('textbox', { name: /Directory identifier/ }), 'dir-noura');
    await user.click(screen.getByRole('button', { name: en('common.actions.save') }));

    expect(screen.getByRole('textbox', { name: /Email/ }).getAttribute('aria-invalid')).toBe(
      'true',
    );
    expect(
      screen.getByRole('textbox', { name: /Mobile number/ }).getAttribute('aria-invalid'),
    ).toBe('true');
    expect(screen.getAllByText(en('common.fieldErrors.malformed'))).toHaveLength(2);
    expect(api.requestsTo('POST', /^\/users$/)).toHaveLength(0);
  });

  test('an internal user is created with a sensitive-write key, while saving shows its state', async () => {
    const api = await openCreateUser();
    let release: () => void = () => undefined;
    api.on(
      'POST',
      /^\/users$/,
      () =>
        new Promise((resolve) => {
          release = () => {
            resolve({ status: 201, body: internalUser, headers: { ETag: '"1"' } });
          };
        }),
    );
    const user = userEvent.setup();

    await user.type(screen.getByRole('textbox', { name: /Full name/ }), 'Sara Internal');
    await user.type(screen.getByRole('textbox', { name: /Username/ }), 'sara.internal');
    await user.type(screen.getByRole('textbox', { name: /Email/ }), 'sara.internal@example.test');
    await user.type(screen.getByRole('textbox', { name: /Directory identifier/ }), 'dir-sara');
    await user.click(screen.getByRole('button', { name: en('common.actions.save') }));

    const saving = await screen.findByRole('button', { name: en('common.states.saving') });
    expect((saving as HTMLButtonElement).disabled).toBe(true);
    release();

    expect(await screen.findByText(en('identityAccess.users.detail.notice.created'))).toBeTruthy();
    const [request] = api.requestsTo('POST', /^\/users$/);
    expect(request?.body).toEqual({
      userType: 'INTERNAL',
      username: 'sara.internal',
      displayName: 'Sara Internal',
      email: 'sara.internal@example.test',
      mobileNumber: null,
      preferredLanguage: 'ar',
      directorySubjectId: 'dir-sara',
      jobTitle: null,
      externalEntityId: null,
    });
    expect(request?.headers.get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/);
    expect(request?.headers.get('Authorization')).toBe('Bearer test-access-token');
  });

  test("a taken username is shown on the username field, from the API's errors[]", async () => {
    const api = await openCreateUser();
    api.on(
      'POST',
      /^\/users$/,
      problem(409, 'IDENTITY_ACCESS_DUPLICATE_KEY', [{ field: 'username', code: 'DUPLICATE' }]),
    );
    const user = userEvent.setup();

    await user.type(screen.getByRole('textbox', { name: /Full name/ }), 'Sara Internal');
    await user.type(screen.getByRole('textbox', { name: /Username/ }), 'sara.internal');
    await user.type(screen.getByRole('textbox', { name: /Email/ }), 'sara.internal@example.test');
    await user.type(screen.getByRole('textbox', { name: /Directory identifier/ }), 'dir-sara');
    await user.click(screen.getByRole('button', { name: en('common.actions.save') }));

    expect(await screen.findByText(en('identityAccess.fieldErrors.duplicate'))).toBeTruthy();
    const username = screen.getByRole('textbox', { name: /Username/ });
    expect(username.getAttribute('aria-invalid')).toBe('true');
    await waitFor(() => {
      expect(document.activeElement).toBe(username);
    });
    expect(screen.getByRole('alert').textContent).toBe(en('identityAccess.problems.duplicateKey'));
  });

  test('ADR-013: an external user needs their entity, and has no directory attributes note', async () => {
    const api = await openCreateUser();
    api.on('POST', /^\/users$/, { status: 201, body: { ...internalUser, userType: 'EXTERNAL' } });
    const user = userEvent.setup();

    await user.click(screen.getByRole('radio', { name: en('identityAccess.userType.EXTERNAL') }));
    expect(screen.queryByText(en('identityAccess.users.form.directoryAttributesNote'))).toBeNull();
    await user.type(screen.getByRole('textbox', { name: /Full name/ }), 'Lina External');
    await user.type(screen.getByRole('textbox', { name: /Username/ }), 'lina.external');
    await user.type(screen.getByRole('textbox', { name: /Email/ }), 'lina@example.test');
    await user.click(screen.getByRole('button', { name: en('common.actions.save') }));

    const entity = screen.getByRole('combobox', { name: /External entity/ });
    expect(entity.getAttribute('aria-invalid')).toBe('true');

    await user.selectOptions(entity, ENTITY_ID);
    await user.type(screen.getByRole('textbox', { name: /Job title/ }), 'Site engineer');
    await user.click(screen.getByRole('button', { name: en('common.actions.save') }));

    await waitFor(() => {
      expect(api.requestsTo('POST', /^\/users$/)).toHaveLength(1);
    });
    expect(api.requestsTo('POST', /^\/users$/)[0]?.body).toMatchObject({
      userType: 'EXTERNAL',
      externalEntityId: ENTITY_ID,
      directorySubjectId: null,
      jobTitle: 'Site engineer',
    });
  });

  test.each(['en', 'ar'] as const)(
    'has no axe violation with errors shown (%s)',
    async (language) => {
      await openCreateUser(language);
      const user = userEvent.setup();

      await user.click(
        screen.getByRole('button', { name: translate(language, 'common.actions.save') }),
      );

      const violations = await accessibilityViolations();
      expect(violations, describeViolations(violations)).toEqual([]);
    },
  );
});

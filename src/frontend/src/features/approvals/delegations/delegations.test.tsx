import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type ApprovalDelegationList } from '@/features/approvals/api/types.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import {
  externalUser,
  internalUser,
  withIdentityAccessData,
} from '@/test/identityAccessFixtures.ts';
import {
  APPROVER_ID,
  delegation,
  DELEGATOR_ID,
  sessionAs,
  withApprovalNames,
} from '@/test/approvalFixtures.ts';
import { mockApi, problem } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// SCR-114 Delegated Approvals and MOD-043 Delegate.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const given = delegation();
const received = delegation({
  id: '4a4a4a4a-0000-4000-8000-000000000e02',
  delegatorUserId: DELEGATOR_ID,
  delegateUserId: APPROVER_ID,
  routingKey: 'CHANGE_REQUEST',
});

function openDelegations(list: ApprovalDelegationList = { given: [given], received: [received] }) {
  // The UserPicker searches FG-03's /users; the names come from the approval fixtures, which are added last and win.
  const api = withApprovalNames(withIdentityAccessData(mockApi())).on(
    'GET',
    /^\/approval-delegations$/,
    {
      body: list,
    },
  );
  renderApp({ path: '/approvals/delegations', session: sessionAs(APPROVER_ID) });
  return api;
}

describe('SCR-114 Delegated Approvals', () => {
  test('lists the delegations given and received, and only a given active one can be revoked', async () => {
    openDelegations();

    const givenTable = await screen.findByRole('table', {
      name: en('approvals.delegations.givenCaption'),
    });
    expect(await within(givenTable).findByText('Faisal Requester')).toBeTruthy();
    expect(within(givenTable).getByText(en('approvals.delegations.allRoutingKeys'))).toBeTruthy();
    expect(
      within(givenTable).getByRole('button', { name: en('approvals.revoke.action') }),
    ).toBeTruthy();

    const receivedTable = screen.getByRole('table', {
      name: en('approvals.delegations.receivedCaption'),
    });
    expect(await within(receivedTable).findByText('Huda Delegator')).toBeTruthy();
    expect(within(receivedTable).getByText('CHANGE_REQUEST')).toBeTruthy();
    expect(within(receivedTable).queryByRole('button')).toBeNull();
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('each list has its own empty state', async () => {
    openDelegations({ given: [], received: [] });

    expect(await screen.findByText(en('approvals.delegations.givenEmpty'))).toBeTruthy();
    expect(screen.getByText(en('approvals.delegations.receivedEmpty'))).toBeTruthy();
  });

  test('revoking asks first, then posts and reads the list again', async () => {
    const api = openDelegations();
    api.on('POST', /\/revoke$/, { body: { ...given, status: 'REVOKED' } });
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: en('approvals.revoke.action') }));
    const dialog = screen.getByRole('dialog', { name: en('approvals.revoke.title') });
    api.on('GET', /^\/approval-delegations$/, {
      body: { given: [{ ...given, status: 'REVOKED' }], received: [] },
    });
    await user.click(within(dialog).getByRole('button', { name: en('approvals.revoke.confirm') }));

    expect(await screen.findByText(en('approvals.done.revoke'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/revoke$/)[0]?.path).toBe(
      `/approval-delegations/${given.id}/revoke`,
    );
    expect(await screen.findByText(en('approvals.delegationStatus.REVOKED'))).toBeTruthy();
    expect(screen.queryByRole('button', { name: en('approvals.revoke.action') })).toBeNull();
  });
});

describe('MOD-043 Delegate', () => {
  async function openDialog() {
    const api = openDelegations({ given: [], received: [] });
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: en('approvals.delegate.action') }));
    const dialog = screen.getByRole('dialog', { name: en('approvals.delegate.title') });
    return { api, user, dialog };
  }

  test('a delegate and an end are required before anything is sent', async () => {
    const { api, user, dialog } = await openDialog();

    await user.click(
      within(dialog).getByRole('button', { name: en('approvals.delegate.confirm') }),
    );

    expect(within(dialog).getByRole('alert').textContent).toBe(
      en('common.form.fixErrors', { count: 2 }),
    );
    expect(api.requestsTo('POST', /^\/approval-delegations$/)).toHaveLength(0);
  });

  test('only internal users are searched, and the delegation is sent as the API expects', async () => {
    const { api, user, dialog } = await openDialog();
    api.on('POST', /^\/approval-delegations$/, { status: 201, body: given });

    await user.click(within(dialog).getByRole('button', { name: en('common.actions.search') }));
    await waitFor(() => {
      expect(api.requestsTo('GET', /^\/users$/)).toHaveLength(1);
    });
    expect(api.requestsTo('GET', /^\/users$/)[0]?.query.get('userType')).toBe('INTERNAL');
    const results = await within(dialog).findByRole('list');
    expect(within(results).queryByText(new RegExp(externalUser.displayName))).toBeNull();
    await user.click(
      within(results).getByRole('button', { name: new RegExp(internalUser.displayName) }),
    );

    await user.type(
      within(dialog).getByRole('textbox', { name: en('approvals.delegate.routingKey') }),
      'CHANGE_REQUEST',
    );
    await user.type(within(dialog).getByLabelText(/Until/), '2099-01-01T09:00');
    await user.click(
      within(dialog).getByRole('button', { name: en('approvals.delegate.confirm') }),
    );

    expect(await screen.findByText(en('approvals.done.delegate'))).toBeTruthy();
    const body = api.requestsTo('POST', /^\/approval-delegations$/)[0]?.body as Record<
      string,
      unknown
    >;
    expect(body.routingKey).toBe('CHANGE_REQUEST');
    expect(body.validFrom).toBeNull();
    expect(body.validTo).toBe(new Date('2099-01-01T09:00').toISOString());
    expect(body.delegateUserId).toBe(internalUser.id);
  });

  test('an external or inactive delegate refused by the API is explained', async () => {
    const { api, user, dialog } = await openDialog();
    api.on('POST', /^\/approval-delegations$/, problem(422, 'APPROVAL_DELEGATE_INVALID'));

    await user.click(within(dialog).getByRole('button', { name: en('common.actions.search') }));
    const results = await within(dialog).findByRole('list');
    await user.click(
      within(results).getByRole('button', { name: new RegExp(internalUser.displayName) }),
    );
    await user.type(within(dialog).getByLabelText(/Until/), '2099-01-01T09:00');
    await user.click(
      within(dialog).getByRole('button', { name: en('approvals.delegate.confirm') }),
    );

    await waitFor(() => {
      expect(within(dialog).getByRole('alert').textContent).toBe(
        en('approvals.problems.delegateInvalid'),
      );
    });
  });
});

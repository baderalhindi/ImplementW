import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { type Language } from '@/shared/i18n/resources.ts';
import {
  APPROVER_ID,
  approvalRun,
  inboxItem,
  sessionAs,
  task,
  TASK_ID,
  withApprovalNames,
} from '@/test/approvalFixtures.ts';
import { mockApi, page, problem } from '@/test/mockApi.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// MOD-040 approve, MOD-041 reject, MOD-042 return from SCR-100. Acceptance criterion 2 and the workbook's check:
// rejecting or returning with an empty reason is refused by the client and, if sent, by the server.

const en = (key: string) => translate('en', key);

async function openDecision(decision: 'approve' | 'reject' | 'return', language: Language = 'en') {
  const t = (key: string) => translate(language, key);
  let inboxReads = 0;
  const api = withApprovalNames(mockApi()).on('GET', /^\/approval-tasks$/, () => {
    inboxReads += 1;
    return { body: page(inboxReads === 1 ? [inboxItem()] : []) };
  });
  renderApp({ path: '/approvals/inbox', language, session: sessionAs(APPROVER_ID) });
  const user = userEvent.setup();
  const row = (await screen.findAllByRole('row'))[1];
  if (row === undefined) {
    throw new Error('No inbox row.');
  }
  await user.click(
    within(row).getByRole('button', { name: t(`approvals.decision.${decision}.action`) }),
  );
  const dialog = screen.getByRole('dialog', { name: t(`approvals.decision.${decision}.title`) });
  return { api, user, dialog, t, inboxReads: () => inboxReads };
}

const decided = (status: 'APPROVED' | 'REJECTED' | 'RETURNED') =>
  approvalRun({ status, tasks: [task({ status, actingUserId: APPROVER_ID })] });

describe('MOD-041 reject and MOD-042 return need a reason', () => {
  test.each(['reject', 'return'] as const)(
    '%s with an empty or blank reason is refused before the API is called',
    async (decision) => {
      const { api, user, dialog } = await openDecision(decision);
      const reason = within(dialog).getByRole('textbox', { name: /Reason/ });
      expect(reason.hasAttribute('required')).toBe(true);

      await user.click(
        within(dialog).getByRole('button', { name: en(`approvals.decision.${decision}.confirm`) }),
      );
      expect(within(dialog).getByText(en('common.fieldErrors.required'))).toBeTruthy();
      expect(reason.getAttribute('aria-invalid')).toBe('true');
      expect(document.activeElement).toBe(reason);

      await user.type(reason, '   ');
      await user.click(
        within(dialog).getByRole('button', { name: en(`approvals.decision.${decision}.confirm`) }),
      );
      expect(within(dialog).getByText(en('common.fieldErrors.required'))).toBeTruthy();

      expect(api.requestsTo('POST', /^\/approval-tasks\//)).toHaveLength(0);
      expect(screen.getByRole('dialog')).toBeTruthy();
      expect(describeViolations(await accessibilityViolations())).toBe('');
    },
  );

  test.each(['reject', 'return'] as const)(
    "the server's refusal of an empty reason lands on the reason field and nothing is decided",
    async (decision) => {
      const { api, user, dialog } = await openDecision(decision);
      // As the API answers `{reason: {text: ""}}` (NarrativeTextRequest) and a body without a reason.
      api.on(
        'POST',
        new RegExp(`/${decision}$`),
        problem(400, 'VALIDATION_FAILED', [{ field: 'reason.text', code: 'REQUIRED' }]),
      );

      await user.type(within(dialog).getByRole('textbox', { name: /Reason/ }), 'x');
      await user.click(
        within(dialog).getByRole('button', { name: en(`approvals.decision.${decision}.confirm`) }),
      );

      await waitFor(() => {
        expect(within(dialog).getByRole('alert').textContent).toBe(
          en('common.problems.validationFailed'),
        );
      });
      expect(within(dialog).getByText(en('common.fieldErrors.required'))).toBeTruthy();
      expect(screen.queryByText(en(`approvals.done.${decision}`))).toBeNull();
      expect(screen.getByRole('dialog')).toBeTruthy();
    },
  );

  test('a reason is sent with the language it was written in, and the inbox is read again', async () => {
    const { api, user, dialog, inboxReads } = await openDecision('reject');
    api.on('POST', /\/reject$/, { body: decided('REJECTED') });

    await user.type(within(dialog).getByRole('textbox', { name: /Reason/ }), '  Out of budget.  ');
    await user.click(
      within(dialog).getByRole('button', { name: en('approvals.decision.reject.confirm') }),
    );

    expect(await screen.findByText(en('approvals.done.reject'))).toBeTruthy();
    const [request] = api.requestsTo('POST', /\/reject$/);
    expect(request?.path).toBe(`/approval-tasks/${TASK_ID}/reject`);
    expect(request?.body).toEqual({ reason: { text: 'Out of budget.', language: 'en' } });
    expect(request?.headers.get('Idempotency-Key')).not.toBeNull();
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(await screen.findByText(en('approvals.inbox.empty'))).toBeTruthy();
    expect(inboxReads()).toBe(2);
  });

  test('a reason written in the Arabic interface is tagged ar', async () => {
    const { api, user, dialog, t } = await openDecision('return', 'ar');
    api.on('POST', /\/return$/, { body: decided('RETURNED') });

    await user.type(
      within(dialog).getByRole('textbox', { name: /السبب/ }),
      'يلزم تحديث الجدول الزمني.',
    );
    await user.click(
      within(dialog).getByRole('button', { name: t('approvals.decision.return.confirm') }),
    );

    expect(await screen.findByText(t('approvals.done.return'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/return$/)[0]?.body).toEqual({
      reason: { text: 'يلزم تحديث الجدول الزمني.', language: 'ar' },
    });
  });
});

describe('MOD-040 approve', () => {
  test('approving needs no reason and sends none', async () => {
    const { api, user, dialog } = await openDecision('approve');
    api.on('POST', /\/approve$/, { body: decided('APPROVED') });

    const reason = within(dialog).getByRole('textbox', { name: /Reason/ });
    expect(reason.hasAttribute('required')).toBe(false);
    await user.click(
      within(dialog).getByRole('button', { name: en('approvals.decision.approve.confirm') }),
    );

    expect(await screen.findByText(en('approvals.done.approve'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/approve$/)[0]?.body).toEqual({});
  });

  test.each([
    ['TERMINAL_STATE', 409, 'approvals.problems.terminalState'],
    ['APPROVAL_CONCURRENT_DECISION', 409, 'approvals.problems.concurrentDecision'],
  ])(
    'a task decided elsewhere (%s) closes the dialog, says so, and reads the inbox again',
    async (code, status, message) => {
      const { api, user, dialog, inboxReads } = await openDecision('approve');
      api.on('POST', /\/approve$/, problem(status, code));

      await user.click(
        within(dialog).getByRole('button', { name: en('approvals.decision.approve.confirm') }),
      );

      expect(await screen.findByText(en(message))).toBeTruthy();
      expect(screen.queryByRole('dialog')).toBeNull();
      await waitFor(() => {
        expect(inboxReads()).toBe(2);
      });
    },
  );

  test('a later stage, or a route that is not configured, is explained in the dialog', async () => {
    const { api, user, dialog } = await openDecision('approve');
    api.on('POST', /\/approve$/, problem(422, 'CONFIGURATION_MISSING'));

    await user.click(
      within(dialog).getByRole('button', { name: en('approvals.decision.approve.confirm') }),
    );

    await waitFor(() => {
      expect(within(dialog).getByRole('alert').textContent).toBe(
        en('approvals.problems.configurationMissing'),
      );
    });
  });
});

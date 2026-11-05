import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { translate } from '@/shared/i18n/i18n.ts';
import { sessionFor } from '@/test/identityAccessFixtures.ts';
import { type MockApi, mockApi, page, problem } from '@/test/mockApi.ts';
import {
  CONTRIBUTION_ID,
  myRequest,
  REQUEST_A,
  REQUEST_B,
  threeRequests,
  withMyUpdates,
} from '@/test/monthlyUpdateFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

import { pluralForm } from './counts.ts';

// The monthly update window for any responder (the 5 Nov 2026 review §2.1): once per sign-in, back from the bell, drafts kept.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const plain = (text: string | null | undefined) => (text ?? '').replace(/[⁨⁩]/g, '');
const SESSION = sessionFor(['R04']);

function homeApi(): MockApi {
  return mockApi()
    .on('GET', /^\/dashboards$/, { body: page([]) })
    .on('GET', /^\/approval-tasks$/, { body: page([]) })
    .on('GET', /^\/approval-instances$/, { body: page([]) })
    .on('GET', /^\/projects$/, { body: page([]) })
    .on('GET', /^\/notifications$/, { body: page([]) })
    .on('GET', /^\/notifications\/unread-count$/, { body: { unreadCount: 0 } })
    .on('GET', /^\/external-update-requests$/, { body: page([]) });
}

const closeWindow = async () => {
  await userEvent.click(
    screen.getByRole('button', { name: en('externalParticipation.monthly.window.close') }),
  );
};

const windowDialog = () => screen.findByRole('dialog', { name: /update$/ });

const openBell = async () => {
  await userEvent.click(screen.getByRole('button', { name: /^Notifications/ }));
};

const projectRow = (name: string) => screen.getByRole('button', { name: new RegExp(name) });

describe('the window at sign-in', () => {
  test('opens by itself with the month, the count and the earliest date, once per sign-in, not on a reload', async () => {
    withMyUpdates(homeApi(), threeRequests());
    const first = renderApp({ path: '/', session: SESSION });

    const dialog = await windowDialog();
    expect(within(dialog).getByRole('heading', { level: 2 }).textContent).toContain('November');
    expect(plain(dialog.textContent)).toContain(
      '3 projects waiting for your update, due by 10 November',
    );
    expect(within(dialog).getByRole('progressbar').getAttribute('aria-valuemax')).toBe('3');

    first.unmount();
    // A reload: the session is still in the tab's storage, the store starts again.
    const shown = Object.keys(sessionStorage).filter((key) =>
      key.startsWith('pmplatform.monthlyWindowShown.'),
    );
    expect(shown).toHaveLength(1);
    renderApp({ path: '/', session: SESSION });
    await waitFor(() => {
      expect(document.querySelector('.topbar__bell-count')?.textContent).toBe('1');
    });
    expect(screen.queryByRole('dialog', { name: /update$/ })).toBeNull();
  });

  test('a new sign-in opens it again', async () => {
    withMyUpdates(homeApi(), threeRequests());
    sessionStorage.setItem(
      `pmplatform.monthlyWindowShown.${SESSION.user.id}.${SESSION.sessionExpiresAt}`,
      'true',
    );
    renderApp({
      path: '/',
      session: { ...SESSION, sessionExpiresAt: '2099-02-02T00:00:00Z' },
    });
    expect(await windowDialog()).toBeTruthy();
  });

  test('stays shut with nothing due, and the bell shows no pinned item', async () => {
    const api = homeApi();
    renderApp({ path: '/', session: SESSION });
    await screen.findByRole('button', { name: /^Notifications/ });
    await waitFor(() => {
      expect(api.shellMyUpdateReads.length).toBeGreaterThan(0);
    });
    await openBell();
    expect(screen.queryByRole('button', { name: /Monthly update:/ })).toBeNull();
    expect(screen.queryByRole('dialog', { name: /update$/ })).toBeNull();
  });

  test('reads in Arabic with the agreeing plural', async () => {
    withMyUpdates(homeApi(), threeRequests());
    renderApp({ path: '/', session: SESSION, language: 'ar' });
    const dialog = await screen.findByRole('dialog', { name: /نوفمبر/ });
    expect(plain(dialog.textContent)).toContain('3 مشاريع بانتظار تحديثك، آخر موعد 10 نوفمبر');
    expect(pluralForm('ar', 27)).toBe('many');
    expect(pluralForm('ar', 1)).toBe('one');
    expect(pluralForm('ar', 0)).toBe('other');
  });
});

describe('the pinned item in the bell', () => {
  test('reopens the window; reading it does not clear it; late says so in red', async () => {
    const items = [myRequest({ overdue: true })];
    withMyUpdates(homeApi(), items);
    renderApp({ path: '/', session: SESSION });
    await windowDialog();
    await closeWindow();
    await waitFor(() => {
      expect(screen.queryByRole('dialog', { name: /update$/ })).toBeNull();
    });

    expect(document.querySelector('.topbar__bell-count')?.textContent).toBe('1');
    await openBell();
    const pinned = await screen.findByRole('button', { name: /Monthly update: .*1.* project due/ });
    expect(
      within(pinned).getByText(en('externalParticipation.monthly.overdue')).className,
    ).toContain('badge--negative');
    await userEvent.click(pinned);
    expect(await windowDialog()).toBeTruthy();
    expect(document.querySelector('.topbar__bell-count')?.textContent).toBe('1');
  });

  test('leaves after the last submit', async () => {
    const api = homeApi();
    withMyUpdates(api, [myRequest()]);
    renderApp({ path: '/', session: SESSION });
    const dialog = await windowDialog();
    await userEvent.click(within(dialog).getByRole('button', { name: /Coastal road upgrade/ }));
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('externalParticipation.monthly.form.submit') }),
    );

    await within(dialog).findByText(en('externalParticipation.monthly.window.allDone'));
    await waitFor(() => {
      expect(document.querySelector('.topbar__bell-count')).toBeNull();
    });
    await closeWindow();
    await openBell();
    expect(screen.queryByRole('button', { name: /Monthly update:/ })).toBeNull();
  });
});

describe('one form per project', () => {
  test('marks it opened once, shows the plan and prefill, and saves on leaving with the right languages', async () => {
    const api = homeApi();
    withMyUpdates(api, threeRequests());
    renderApp({ path: '/', session: SESSION });
    const dialog = await windowDialog();

    await userEvent.click(projectRow('Coastal road upgrade'));
    const form = within(dialog).getByRole('form', { name: 'Coastal road upgrade' });
    expect(plain(form.textContent)).toContain('Planned progress: 50%');
    const progress = within(form).getByRole('textbox', {
      name: new RegExp(
        en('externalParticipation.fields.actualProgressPercent').replace(/[()%]/g, '.'),
      ),
    });
    expect((progress as HTMLInputElement).value).toBe('40');
    expect(describeViolations(await accessibilityViolations())).toBe('');
    await waitFor(() => {
      expect(api.requestsTo('POST', /mark-opened$/)).toHaveLength(1);
    });
    expect(api.requestsTo('POST', /mark-opened$/)[0]?.path).toContain(REQUEST_A);

    await userEvent.clear(progress);
    await userEvent.type(progress, '55');
    const note = within(form).getByRole('textbox', {
      name: en('externalParticipation.fields.note'),
    });
    await userEvent.type(note, 'Typed here');

    await userEvent.click(projectRow('Harbour dredging'));
    await waitFor(() => {
      expect(api.requestsTo('POST', /^\/external-contributions$/)).toHaveLength(1);
    });
    const body = api.requestsTo('POST', /^\/external-contributions$/)[0]?.body as {
      externalUpdateRequestId: string;
      fields: { fieldCode: string; value: string; language: string | null }[];
    };
    expect(body.externalUpdateRequestId).toBe(REQUEST_A);
    expect(body.fields).toEqual([
      { fieldCode: 'actualProgressPercent', value: '55', language: null },
      { fieldCode: 'currentObstacle', value: 'Permit delay', language: 'ar' },
      { fieldCode: 'note', value: 'Typed here', language: 'en' },
    ]);
  });

  test('closing keeps a draft: saved on close, the values back when reopened from the bell, a later save is a PUT with If-Match', async () => {
    const api = homeApi();
    withMyUpdates(api, threeRequests());
    renderApp({ path: '/', session: SESSION });
    await windowDialog();
    await userEvent.click(projectRow('Coastal road upgrade'));
    const note = screen.getByRole('textbox', { name: en('externalParticipation.fields.note') });
    await userEvent.type(note, 'Keep me');
    await closeWindow();

    await waitFor(() => {
      expect(api.requestsTo('POST', /^\/external-contributions$/)).toHaveLength(1);
    });
    await openBell();
    await userEvent.click(await screen.findByRole('button', { name: /Monthly update:/ }));
    await windowDialog();
    const again = screen.getByRole('textbox', { name: en('externalParticipation.fields.note') });
    expect((again as HTMLTextAreaElement).value).toBe('Keep me');

    await userEvent.type(again, ' too');
    await userEvent.click(
      screen.getByRole('button', { name: en('externalParticipation.actions.saveDraft') }),
    );
    await screen.findByText(en('externalParticipation.monthly.form.saved'));
    const put = api.requestsTo('PUT', /^\/external-contributions\//)[0];
    expect(put?.path).toContain(CONTRIBUTION_ID);
    expect(put?.headers.get('If-Match')).toBe('"701"');
  });

  test('a failed save on leaving is not lost: the row says unsaved and the values stay', async () => {
    const api = homeApi();
    withMyUpdates(api, threeRequests());
    api.on('POST', /^\/external-contributions$/, problem(500, 'INTERNAL_ERROR'));
    renderApp({ path: '/', session: SESSION });
    await windowDialog();
    await userEvent.click(projectRow('Coastal road upgrade'));
    await userEvent.type(
      screen.getByRole('textbox', { name: en('externalParticipation.fields.note') }),
      'Keep me',
    );
    await userEvent.click(projectRow('Harbour dredging'));
    await waitFor(() => {
      expect(api.requestsTo('POST', /^\/external-contributions$/)).toHaveLength(1);
    });
    expect(
      within(projectRow('Coastal road upgrade')).getByText(
        en('externalParticipation.monthly.state.unsaved'),
      ),
    ).toBeTruthy();
    await userEvent.click(projectRow('Coastal road upgrade'));
    expect(
      screen.getByRole<HTMLTextAreaElement>('textbox', {
        name: en('externalParticipation.fields.note'),
      }).value,
    ).toBe('Keep me');
  });

  test('submit saves, submits with the saved ETag and moves to the next project', async () => {
    const api = homeApi();
    withMyUpdates(api, threeRequests());
    renderApp({ path: '/', session: SESSION });
    const dialog = await windowDialog();
    await userEvent.click(projectRow('Coastal road upgrade'));
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('externalParticipation.monthly.form.submit') }),
    );

    await within(dialog).findByRole('form', { name: 'Harbour dredging' });
    expect(api.requestsTo('POST', /\/submit$/)[0]?.headers.get('If-Match')).toBe('"701"');
    expect(within(dialog).getByRole('progressbar').getAttribute('aria-valuenow')).toBe('1');
    await waitFor(() => {
      expect(api.requestsTo('POST', /mark-opened$/).map((request) => request.path)).toContain(
        `/my-update-requests/${REQUEST_B}/mark-opened`,
      );
    });
    expect(
      within(dialog).getByText(en('externalParticipation.monthly.state.submitted')),
    ).toBeTruthy();
    // Nothing is written to the frozen revision after it was submitted.
    expect(api.requestsTo('PUT', /^\/external-contributions\//)).toHaveLength(0);
  });

  test('a required value stops the submit; a returned draft shows why and saves with its version', async () => {
    const api = homeApi();
    withMyUpdates(api, [
      myRequest({
        contributionId: CONTRIBUTION_ID,
        contributionVersion: 88,
        contributionStatus: 'DRAFT',
        previouslyReturned: true,
        returnReason: { text: 'The spend figure is wrong.', language: 'EN' },
        prefill: {},
        prefillLanguages: {},
      }),
    ]);
    renderApp({ path: '/', session: SESSION });
    const dialog = await windowDialog();
    await userEvent.click(projectRow('Coastal road upgrade'));
    expect(within(dialog).getByText('The spend figure is wrong.')).toBeTruthy();

    await userEvent.click(
      within(dialog).getByRole('button', { name: en('externalParticipation.monthly.form.submit') }),
    );
    expect(
      within(dialog).getByText(en('externalParticipation.fieldErrors.answerRequired')),
    ).toBeTruthy();
    expect(api.requestsTo('POST', /\/submit$/)).toHaveLength(0);

    await userEvent.type(
      within(dialog).getByRole('textbox', {
        name: new RegExp(
          en('externalParticipation.fields.actualProgressPercent').replace(/[()%]/g, '.'),
        ),
      }),
      '70',
    );
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('externalParticipation.monthly.form.submit') }),
    );
    await waitFor(() => {
      expect(api.requestsTo('POST', /\/submit$/)).toHaveLength(1);
    });
    expect(api.requestsTo('PUT', /^\/external-contributions\//)[0]?.headers.get('If-Match')).toBe(
      '"88"',
    );
  });
});

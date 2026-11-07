import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type ProjectSummary } from '@/features/projects/api/types.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import { type MockApi, mockApi, page, problem } from '@/test/mockApi.ts';
import { pickPerson } from '@/test/personPicker.ts';
import { BATCH_ID, batchDetail, REQUEST_A } from '@/test/monthlyUpdateFixtures.ts';
import {
  ENTITY_ID,
  OTHER_PERSON_ID,
  projectSummary,
  reviewerSession,
  REVIEWER_ID,
  withProjectLookups,
} from '@/test/projectFixtures.ts';
import {
  accessibilityViolations,
  describeViolations,
  openFilters,
  renderApp,
} from '@/test/renderApp.tsx';

// The three-step "طلب تحديث" and the batch page (the 5 Nov 2026 review §2.2).

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);
const plain = (text: string | null | undefined) => (text ?? '').replace(/[⁨⁩]/g, '');
const OTHER_ENTITY = '6f6f6f6f-0000-4000-8000-000000000999';
const FUTURE = '2099-11-10';

function project(id: string, overrides: Partial<ProjectSummary> = {}): ProjectSummary {
  return projectSummary({
    id,
    status: 'ACTIVE',
    title: { text: `Project ${id}`, language: 'EN' },
    entityContactUserId: OTHER_PERSON_ID,
    ...overrides,
  });
}

/** `/projects`, paged and filtered by status as the API does. */
function withProjects(api: MockApi, all: ProjectSummary[], pageSize = 25): MockApi {
  return api.on('GET', /^\/projects$/, (request) => {
    const status = request.query.get('status');
    const rows = all.filter((item) => status === null || item.status === status);
    const size = Number(request.query.get('pageSize') ?? pageSize);
    const number = Number(request.query.get('page') ?? '1');
    return {
      body: {
        items: rows.slice((number - 1) * size, number * size),
        page: number,
        pageSize: size,
        totalCount: rows.length,
      },
    };
  });
}

function wizardApi(all: ProjectSummary[]): MockApi {
  const api = mockApi();
  withProjectLookups(api);
  withProjects(api, all);
  api.on('GET', /^\/users$/, problem(403, 'PERMISSION_DENIED'));
  return api;
}

const next = () => screen.getByRole('button', { name: en('common.actions.next') });
const selectAllButton = () =>
  screen.findByRole('button', { name: en('externalParticipation.batch.step1.selectAll') });

const counter = () => plain(document.querySelector('.steps__counter')?.textContent);

async function fillRequest() {
  await userEvent.type(
    screen.getByLabelText(new RegExp(en('externalParticipation.fields.dueDate'))),
    FUTURE,
  );
  await userEvent.type(
    screen.getByRole('textbox', {
      name: new RegExp(en('externalParticipation.fields.instructions')),
    }),
    'Report the month.',
  );
}

describe('step 1: choosing projects', () => {
  test("the register's primary action leads here for a sender", async () => {
    const api = wizardApi([]);
    api.on('GET', /^\/external-update-requests$/, { body: page([]) });
    renderApp({ path: '/external-requests', session: reviewerSession() });
    const link = await screen.findByRole('link', { name: en('externalParticipation.batch.new') });
    expect(link.getAttribute('href')).toBe('/external-requests/batches/new');
  });

  test('a project that cannot be requested is disabled and says why', async () => {
    wizardApi([
      project('a1'),
      project('a2', { status: 'DRAFT' }),
      project('a3', { externalEntityId: null }),
    ]);
    renderApp({ path: '/external-requests/batches/new', session: reviewerSession() });
    const draft = await screen.findByRole('checkbox', { name: 'Project a2' });
    expect(draft.hasAttribute('disabled')).toBe(true);
    expect(screen.getByText(en('externalParticipation.batch.ineligible.STATUS'))).toBeTruthy();
    expect(screen.getByRole('checkbox', { name: 'Project a3' }).hasAttribute('disabled')).toBe(
      true,
    );
    expect(screen.getByText(en('externalParticipation.batch.ineligible.NO_ENTITY'))).toBeTruthy();
    expect(next().hasAttribute('disabled')).toBe(true);

    await userEvent.click(screen.getByRole('checkbox', { name: 'Project a1' }));
    expect(counter()).toContain('Selected: 1 project across 1 entity');
    expect(next().hasAttribute('disabled')).toBe(false);
  });

  test('select all takes everything matching the filter across all pages, leaving out the ineligible', async () => {
    const many = [
      ...Array.from({ length: 30 }, (_, index) =>
        project(`b${String(index)}`, {
          externalEntityId: index % 2 === 0 ? ENTITY_ID : OTHER_ENTITY,
        }),
      ),
      project('s1', { status: 'SUSPENDED' }),
      project('d1', { status: 'DRAFT' }),
    ];
    const api = wizardApi(many);
    const { router } = renderApp({
      path: '/external-requests/batches/new?status=ACTIVE',
      session: reviewerSession(),
    });
    await openFilters();
    await userEvent.click(await selectAllButton());

    await waitFor(() => {
      expect(counter()).toContain('Selected: 30 projects across 2 entities');
    });
    const reads = api
      .requestsTo('GET', /^\/projects$/)
      .filter((request) => request.query.get('pageSize') === '200');
    expect(reads.length).toBeGreaterThan(0);
    expect(reads[0]?.query.get('status')).toBe('ACTIVE');
    expect(router.state.location.search).toContain('status=ACTIVE');
  });

  test('select all with no status filter leaves out what cannot be requested, and says how many', async () => {
    wizardApi([project('a1'), project('d1', { status: 'DRAFT' })]);
    renderApp({ path: '/external-requests/batches/new', session: reviewerSession() });
    await userEvent.click(await selectAllButton());
    await waitFor(() => {
      expect(counter()).toContain('Selected: 1 project across 1 entity');
    });
    expect(plain(screen.getByText(/left out/).textContent)).toContain(
      '1 matching projects were left out',
    );
  });
});

describe('steps 2 and 3', () => {
  const toStep2 = async () => {
    await userEvent.click(await selectAllButton());
    await waitFor(() => {
      expect(next().hasAttribute('disabled')).toBe(false);
    });
    await userEvent.click(next());
  };

  test('a project without a contact is flagged and blocks Next until given one; the helper covers its entity', async () => {
    const CHOSEN = '11111111-0000-4000-8000-000000000abc';
    const api = wizardApi([
      project('c1', { entityContactUserId: null }),
      project('c2', { entityContactUserId: null }),
      project('c3'),
    ]);
    renderApp({ path: '/external-requests/batches/new', session: reviewerSession() });
    await toStep2();
    await fillRequest();

    expect(screen.getAllByText(en('externalParticipation.batch.step2.noResponder'))).toHaveLength(
      2,
    );
    expect(next().hasAttribute('disabled')).toBe(true);
    expect(document.querySelectorAll('tr[data-flagged="true"]')).toHaveLength(2);
    expect(describeViolations(await accessibilityViolations())).toBe('');

    await pickPerson(api, document.body, CHOSEN);
    await userEvent.click(screen.getByRole('button', { name: /Apply to the/ }));
    expect(document.querySelectorAll('tr[data-flagged="true"]')).toHaveLength(0);
    expect(next().hasAttribute('disabled')).toBe(false);
  });

  test('step 3 words the summary and posts the batch; only the named responders are sent', async () => {
    const CHOSEN = '11111111-0000-4000-8000-000000000abc';
    const api = wizardApi([
      project('c1', { entityContactUserId: null }),
      project('c2', { externalEntityId: OTHER_ENTITY }),
    ]);
    api.on('POST', /^\/update-request-batches$/, { status: 201, body: batchDetail() });
    api.on('GET', new RegExp(`^/update-request-batches/${BATCH_ID}$`), { body: batchDetail() });
    const { router } = renderApp({
      path: '/external-requests/batches/new',
      session: reviewerSession(),
    });
    await toStep2();
    await fillRequest();
    await pickPerson(api, document.body, CHOSEN);
    await userEvent.click(
      screen.getByRole('checkbox', { name: en('externalParticipation.batch.step2.repeat') }),
    );
    await userEvent.selectOptions(
      screen.getByRole('combobox', {
        name: new RegExp(en('externalParticipation.batch.step2.repeatDay')),
      }),
      '5',
    );
    await userEvent.click(next());

    const summary = document.querySelector('[data-field="batchSummary"]');
    expect(plain(summary?.textContent)).toBe(
      '2 requests to 2 entities, repeating monthly on day 5',
    );
    await userEvent.click(
      screen.getByRole('button', { name: en('externalParticipation.batch.step3.send') }),
    );
    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/external-requests/batches/${BATCH_ID}`);
    });
    expect(api.requestsTo('POST', /^\/update-request-batches$/)[0]?.body).toEqual({
      items: [{ projectId: 'c1', responsibleUserId: CHOSEN }, { projectId: 'c2' }],
      dueDate: FUTURE,
      repeatMonthly: true,
      repeatDay: 5,
      instructions: 'Report the month.',
      instructionsLanguage: 'en',
      reviewerUserId: REVIEWER_ID,
    });
  });

  test('a 422 comes back on the rows it names, and blocks sending until acted on', async () => {
    const api = wizardApi([project('c1'), project('c2')]);
    api.on(
      'POST',
      /^\/update-request-batches$/,
      problem(422, 'EXTERNAL_PARTICIPATION_BATCH_INVALID', [
        { field: 'items[1].responsibleUserId', code: 'NOT_ALLOWED' },
        { field: 'items[0].projectId', code: 'PROJECT_STATE_NOT_PERMITTED' },
      ]),
    );
    renderApp({ path: '/external-requests/batches/new', session: reviewerSession() });
    await toStep2();
    await fillRequest();
    await userEvent.click(next());
    await userEvent.click(
      screen.getByRole('button', { name: en('externalParticipation.batch.step3.send') }),
    );

    await screen.findByRole('heading', { name: en('externalParticipation.batch.step2.title') });
    expect(screen.getByText(en('externalParticipation.batch.rowIssue.status'))).toBeTruthy();
    expect(
      screen.getByText(en('externalParticipation.batch.rowIssue.responderIneligible')),
    ).toBeTruthy();
    expect(next().hasAttribute('disabled')).toBe(true);

    await userEvent.click(
      screen.getByRole('button', {
        name: `${en('externalParticipation.batch.step2.remove')}: Project c1`,
      }),
    );
    expect(screen.queryByText(en('externalParticipation.batch.rowIssue.status'))).toBeNull();
    expect(next().hasAttribute('disabled')).toBe(true);
  });

  test('the due date and instructions are required before step 3', async () => {
    wizardApi([project('c1')]);
    renderApp({ path: '/external-requests/batches/new', session: reviewerSession() });
    await toStep2();
    await userEvent.click(next());
    expect(screen.queryByText(en('externalParticipation.batch.step3.send'))).toBeNull();
    expect(
      screen
        .getByRole('textbox', { name: new RegExp(en('externalParticipation.fields.instructions')) })
        .getAttribute('aria-invalid'),
    ).toBe('true');
  });
});

describe('the batch page', () => {
  function batchApi(detail = batchDetail()): MockApi {
    const api = mockApi();
    withProjectLookups(api);
    api.on('GET', new RegExp(`^/update-request-batches/${BATCH_ID}$`), { body: detail });
    return api;
  }

  test('shows each state, counts on top, the overdue, and links rows to their requests', async () => {
    batchApi();
    renderApp({ path: `/external-requests/batches/${BATCH_ID}`, session: reviewerSession() });
    const counts = await screen.findByRole('list', {
      name: en('externalParticipation.batch.countsLabel'),
    });
    for (const [state, count] of [
      ['SENT', '1'],
      ['DRAFT', '1'],
      ['ACCEPTED', '1'],
      ['OVERDUE', '1'],
    ]) {
      expect(counts.querySelector(`[data-state="${state ?? ''}"] strong`)?.textContent).toBe(count);
    }
    expect(counts.querySelector('[data-state="RETURNED"]')).toBeNull();
    expect(describeViolations(await accessibilityViolations())).toBe('');
    const row = screen.getByRole('link', { name: 'Coastal road upgrade' });
    expect(row.getAttribute('href')).toBe(`/external-requests/${REQUEST_A}`);
    const [, firstRow] = screen.getAllByRole('row');
    expect(
      within(firstRow ?? document.body).getByText(en('externalParticipation.monthly.overdue')),
    ).toBeTruthy();
  });

  test('reminds the late ones after a confirmation', async () => {
    const api = batchApi();
    api.on('POST', /remind-late$/, { body: batchDetail() });
    renderApp({ path: `/external-requests/batches/${BATCH_ID}`, session: reviewerSession() });
    await userEvent.click(await screen.findByRole('button', { name: /Remind the late ones/ }));
    const dialog = await screen.findByRole('dialog', {
      name: en('externalParticipation.batch.remindTitle'),
    });
    await userEvent.click(within(dialog).getByRole('button', { name: /Remind the late ones/ }));
    await screen.findByText(en('externalParticipation.batch.reminded'));
    expect(api.requestsTo('POST', /remind-late$/)).toHaveLength(1);
  });

  test('withdraws with a reason; a withdrawn batch offers no more commands', async () => {
    const api = batchApi();
    api.on('POST', /withdraw$/, { body: batchDetail() });
    renderApp({ path: `/external-requests/batches/${BATCH_ID}`, session: reviewerSession() });
    await userEvent.click(
      await screen.findByRole('button', { name: en('externalParticipation.batch.withdraw') }),
    );
    const dialog = await screen.findByRole('dialog', {
      name: en('externalParticipation.batch.withdraw'),
    });
    await userEvent.type(within(dialog).getByRole('textbox'), 'Sent by mistake');
    await userEvent.click(
      within(dialog).getByRole('button', { name: en('externalParticipation.batch.withdraw') }),
    );
    await screen.findByText(en('externalParticipation.batch.withdrawnDone'));
    expect(api.requestsTo('POST', /withdraw$/)[0]?.body).toMatchObject({
      reason: 'Sent by mistake',
    });
  });

  test("the sender's commands are not offered once withdrawn", async () => {
    const detail = batchDetail();
    batchApi({
      ...detail,
      batch: { ...detail.batch, withdrawnAt: '2026-11-02T08:00:00Z' },
    });
    renderApp({ path: `/external-requests/batches/${BATCH_ID}`, session: reviewerSession() });
    await screen.findByRole('list', { name: en('externalParticipation.batch.countsLabel') });
    expect(
      screen.queryByRole('button', { name: en('externalParticipation.batch.withdraw') }),
    ).toBeNull();
  });

  test('the list pages the batches', async () => {
    const api = mockApi();
    api.on('GET', /^\/update-request-batches$/, {
      body: { items: [batchDetail().batch], page: 1, pageSize: 25, totalCount: 1 },
    });
    renderApp({ path: '/external-requests/batches', session: reviewerSession() });
    const link = await screen.findByRole('link', { name: /2026/ });
    expect(link.getAttribute('href')).toBe(`/external-requests/batches/${BATCH_ID}`);
  });
});

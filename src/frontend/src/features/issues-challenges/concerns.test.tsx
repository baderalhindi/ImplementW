import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { type Language, translate } from '@/shared/i18n/i18n.ts';
import {
  concern,
  CONCERN_ETAG,
  concernProject,
  type ConcernState,
  CULVERT_ID,
  CULVERT_OPEN_ESCALATION_ID,
  CULVERT_RESOLVED_ESCALATION_ID,
  escalation,
  HIGH_PRIORITY_ID,
  managerSession,
  PERMIT_ID,
  SITE_CATEGORY_ID,
  UTILITY_CHALLENGE_ID,
  UTILITY_WITHDRAWN_ESCALATION_ID,
  withConcerns,
} from '@/test/concernFixtures.ts';
import { type MockApi, mockApi, problem } from '@/test/mockApi.ts';
import {
  entitySession,
  PROJECT_ID,
  reviewerSession,
  withProject,
  withProjectLookups,
} from '@/test/projectFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';
import { COST_DIMENSION_ID, SCHEDULE_DIMENSION_ID } from '@/test/riskFixtures.ts';
import { day } from '@/test/taskFixtures.ts';

// SCR-083 to SCR-088 and MOD-036 to MOD-039 (TASK-058) against the TASK-057 API. Acceptance criteria: (1) Severity is
// displayed read-only (computed, not editable) while Priority remains an editable field for authorized roles; (2) the
// Escalations list clearly differentiates open vs. resolved escalations with a filter default of open-only. The
// workbook's validation checks: the Severity field is rendered read-only in the Issue Detail form; the Escalations
// list's default filter excludes resolved items and can be toggled to show them.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const ISSUES_PATH = `/projects/${PROJECT_ID}/issues-challenges`;
const CHALLENGES_PATH = `${ISSUES_PATH}/challenges`;
const CULVERT_PATH = `${ISSUES_PATH}/${CULVERT_ID}`;
const ESCALATIONS_PATH = '/issues-challenges/escalations';

function open(
  path: string,
  state: ConcernState = {},
  {
    session = managerSession(),
    project = concernProject(),
    language = 'en',
  }: { session?: Session; project?: ProjectDetail; language?: Language } = {},
): MockApi {
  const api = withConcerns(withProject(withProjectLookups(mockApi()), project), state);
  renderApp({ path, session, language });
  return api;
}

function rowsOf(table: HTMLElement): HTMLElement[] {
  return within(table).getAllByRole('row').slice(1);
}

function titlesIn(table: HTMLElement): string[] {
  return rowsOf(table).map((row) => within(row).getAllByRole('link')[0]?.textContent ?? '');
}

function rowOf(table: HTMLElement, title: string): HTMLElement {
  const row = within(table).getByRole('link', { name: title }).closest('tr');
  if (row === null) {
    throw new Error(`No row for ${title}`);
  }
  return row;
}

function escalationRow(table: HTMLElement, escalationId: string): HTMLElement {
  const row = table.querySelector<HTMLElement>(`[data-escalation="${escalationId}"]`);
  if (row === null) {
    throw new Error(`No row for escalation ${escalationId}`);
  }
  return row;
}

async function openDialog(name: string, title: string): Promise<HTMLElement> {
  await userEvent.click(await screen.findByRole('button', { name }));
  return screen.findByRole('dialog', { name: title });
}

async function press(scope: HTMLElement, name: string) {
  await userEvent.click(within(scope).getByRole('button', { name }));
}

describe('SCR-083 Issue Register and SCR-085 Challenge Register', () => {
  test("lists the project's open issues most severe first, with the computed severity, priority, overdue and escalation", async () => {
    open(ISSUES_PATH);

    const table = await screen.findByRole('region', {
      name: en('issuesChallenges.issues.projectCaption'),
    });
    // Challenges are not issues; the closed fence is outside the default view.
    expect(titlesIn(table)).toEqual(['Collapsed culvert', 'Missing excavation permit']);
    const culvert = rowOf(table, 'Collapsed culvert');
    expect(culvert.className).toBe('row--overdue');
    expect(within(culvert).getByText('Major')).toBeTruthy();
    expect(
      within(culvert).getByText(en('issuesChallenges.table.overallImpact', { level: 4 })),
    ).toBeTruthy();
    expect(within(culvert).getByText('Low')).toBeTruthy();
    expect(within(culvert).getByText(en('issuesChallenges.table.overdue'))).toBeTruthy();
    expect(within(culvert).getByText(en('issuesChallenges.table.escalated'))).toBeTruthy();
    // An unassessed issue says so: never a low severity.
    const permit = rowOf(table, 'Missing excavation permit');
    expect(within(permit).getByText(en('issuesChallenges.severity.none'))).toBeTruthy();
    expect(
      within(table).getByRole('link', { name: 'Collapsed culvert' }).getAttribute('href'),
    ).toBe(CULVERT_PATH);
    // The register shows severity, it offers no way to change it.
    expect(within(table).queryByRole('combobox')).toBeNull();

    expect(document.querySelector('[data-summary="open"] dd')?.textContent).toBe('2');
    expect(document.querySelector('[data-summary="escalated"] dd')?.textContent).toBe('1');
    expect(document.querySelector('[data-summary="overdue"] dd')?.textContent).toBe('1');
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('the views and the severity filter narrow the register', async () => {
    open(ISSUES_PATH);
    const table = await screen.findByRole('region', {
      name: en('issuesChallenges.issues.projectCaption'),
    });
    expect(titlesIn(table)).toHaveLength(2);

    await userEvent.selectOptions(
      screen.getByRole('combobox', { name: en('issuesChallenges.filter.view') }),
      'closed',
    );
    const closed = screen.getByRole('region', {
      name: en('issuesChallenges.issues.projectCaption'),
    });
    expect(titlesIn(closed)).toEqual(['Damaged fence']);
    expect(rowOf(closed, 'Damaged fence').className).toBe('row--muted');

    await userEvent.selectOptions(
      screen.getByRole('combobox', { name: en('issuesChallenges.filter.view') }),
      'all',
    );
    await userEvent.selectOptions(
      screen.getByRole('combobox', { name: en('issuesChallenges.filter.severity') }),
      en('issuesChallenges.severity.none'),
    );
    expect(
      titlesIn(screen.getByRole('region', { name: en('issuesChallenges.issues.projectCaption') })),
    ).toEqual(['Missing excavation permit']);
  });

  test('SCR-085 lists the challenges under their own register, beside the issues', async () => {
    open(CHALLENGES_PATH);

    const table = await screen.findByRole('region', {
      name: en('issuesChallenges.challenges.projectCaption'),
    });
    expect(titlesIn(table)).toEqual(['Utility approval pending']);
    const nav = screen.getByRole('navigation', { name: en('issuesChallenges.nav.registers') });
    expect(
      within(nav)
        .getByRole('link', { name: en('issuesChallenges.nav.challenges') })
        .getAttribute('aria-current'),
    ).toBe('page');
  });

  test('across projects, each issue names its project', async () => {
    open('/issues-challenges');

    const table = await screen.findByRole('region', {
      name: en('issuesChallenges.issues.caption'),
    });
    expect(titlesIn(table)).toEqual([
      'Collapsed culvert',
      'Missing excavation permit',
      'Crane breakdown',
    ]);
    expect(within(rowOf(table, 'Crane breakdown')).getByText('Harbour bridge repair')).toBeTruthy();
  });

  test('the delivering entity raises an issue (MOD-036): no severity is sent, and it lands on the new issue', async () => {
    const api = open(ISSUES_PATH, {}, { session: entitySession() });
    api.on('POST', /^\/management-concerns$/, {
      status: 201,
      body: concern({ id: PERMIT_ID, status: 'OPEN', openEscalation: null }),
    });

    const dialog = await openDialog(
      en('issuesChallenges.actions.createIssue'),
      en('issuesChallenges.form.createIssue'),
    );
    expect(within(dialog).getByText(en('issuesChallenges.form.severityOnCreate'))).toBeTruthy();
    expect(
      within(dialog).queryByRole('textbox', { name: en('issuesChallenges.fields.severity') }),
    ).toBeNull();
    await press(dialog, en('common.actions.save'));
    expect(api.requestsTo('POST', /^\/management-concerns$/)).toHaveLength(0);

    await userEvent.type(
      within(dialog).getByRole('textbox', {
        name: new RegExp(en('issuesChallenges.fields.title')),
      }),
      'Missing excavation permit',
    );
    await userEvent.type(
      within(dialog).getByRole('textbox', {
        name: new RegExp(en('issuesChallenges.fields.description')),
      }),
      'The municipality has not issued the permit.',
    );
    await userEvent.selectOptions(
      within(dialog).getByRole('combobox', {
        name: new RegExp(en('issuesChallenges.fields.category')),
      }),
      'Site conditions',
    );
    await userEvent.selectOptions(
      within(dialog).getByRole('combobox', {
        name: new RegExp(en('issuesChallenges.fields.priority')),
      }),
      'High',
    );
    await press(dialog, en('common.actions.save'));

    await screen.findByText(en('issuesChallenges.done.raised'));
    const [request] = api.requestsTo('POST', /^\/management-concerns$/);
    expect(request?.body).toEqual({
      projectId: PROJECT_ID,
      concernType: 'ISSUE',
      title: { text: 'Missing excavation permit', language: 'en' },
      description: { text: 'The municipality has not issued the permit.', language: 'en' },
      categoryItemId: SITE_CATEGORY_ID,
      priorityItemId: HIGH_PRIORITY_ID,
      targetResolutionDate: null,
      impacts: [],
    });
  });

  test('MOD-038 raises a challenge as a challenge', async () => {
    const api = open(CHALLENGES_PATH);
    api.on('POST', /^\/management-concerns$/, {
      status: 201,
      body: concern({ id: UTILITY_CHALLENGE_ID, concernType: 'CHALLENGE', openEscalation: null }),
    });
    const dialog = await openDialog(
      en('issuesChallenges.actions.createChallenge'),
      en('issuesChallenges.form.createChallenge'),
    );
    await userEvent.type(
      within(dialog).getByRole('textbox', {
        name: new RegExp(en('issuesChallenges.fields.title')),
      }),
      'Land handover',
    );
    await userEvent.type(
      within(dialog).getByRole('textbox', {
        name: new RegExp(en('issuesChallenges.fields.description')),
      }),
      'Waiting for the municipality.',
    );
    await userEvent.selectOptions(
      within(dialog).getByRole('combobox', {
        name: new RegExp(en('issuesChallenges.fields.category')),
      }),
      'Stakeholder',
    );
    await userEvent.selectOptions(
      within(dialog).getByRole('combobox', {
        name: new RegExp(en('issuesChallenges.fields.priority')),
      }),
      'Low',
    );
    await press(dialog, en('common.actions.save'));
    await waitFor(() => {
      expect(api.requestsTo('POST', /^\/management-concerns$/)).toHaveLength(1);
    });
    expect(
      (api.requestsTo('POST', /^\/management-concerns$/)[0]?.body as { concernType: string })
        .concernType,
    ).toBe('CHALLENGE');
  });

  test('the department manager reads the register but is offered no raise', async () => {
    open(ISSUES_PATH, {}, { session: reviewerSession() });
    await screen.findByRole('region', { name: en('issuesChallenges.issues.projectCaption') });
    expect(
      screen.queryByRole('button', { name: en('issuesChallenges.actions.createIssue') }),
    ).toBeNull();
  });
});

describe('SCR-084 Issue Detail (acceptance criterion 1: severity read-only, priority editable)', () => {
  test('the detail shows the computed severity read-only beside the priority, with the impacts it came from', async () => {
    open(CULVERT_PATH);

    await screen.findByRole('heading', { level: 2, name: 'Collapsed culvert' });
    const severity = document.querySelector('[data-field="severity"]');
    expect(severity?.textContent).toBe('Major');
    expect(severity?.querySelector('input, select, textarea')).toBeNull();
    expect(screen.getByText(en('issuesChallenges.detail.severityComputed'))).toBeTruthy();
    expect(document.querySelector('[data-field="priority"]')?.textContent).toBe('Low');
    expect(
      screen.getByText(en('issuesChallenges.detail.pinnedCurrent', { version: 1 })),
    ).toBeTruthy();
    const impacts = screen.getByRole('region', {
      name: en('issuesChallenges.detail.impactsCaption'),
    });
    expect(within(impacts).getByText('Cost')).toBeTruthy();
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('validation check: MOD-037 renders Severity as a read-only field and Priority as an editable one, and sends no severity', async () => {
    const api = open(CULVERT_PATH);
    api.on('PUT', /^\/management-concerns\/[^/]+$/, {
      body: concern({ priorityItemId: HIGH_PRIORITY_ID }),
      headers: { ETag: '"82"' },
    });

    const dialog = await openDialog(
      en('issuesChallenges.actions.editIssue'),
      en('issuesChallenges.form.editIssue'),
    );
    const severityField = within(dialog).getByRole('textbox', {
      name: en('issuesChallenges.fields.severity'),
    });
    expect(severityField).toHaveProperty('readOnly', true);
    expect((severityField as HTMLInputElement).value).toBe('Major');
    expect(within(dialog).getByText(en('issuesChallenges.form.severityReadOnly'))).toBeTruthy();
    // Typing into it changes nothing.
    await userEvent.type(severityField, 'Minor');
    expect((severityField as HTMLInputElement).value).toBe('Major');
    // No severity choice anywhere in the form.
    expect(
      within(dialog).queryByRole('combobox', {
        name: new RegExp(en('issuesChallenges.fields.severity')),
      }),
    ).toBeNull();

    const priority = within(dialog).getByRole('combobox', {
      name: new RegExp(en('issuesChallenges.fields.priority')),
    });
    expect(priority).toHaveProperty('disabled', false);
    await userEvent.selectOptions(priority, 'High');
    await press(dialog, en('common.actions.save'));

    await screen.findByText(en('issuesChallenges.done.updated'));
    const [request] = api.requestsTo('PUT', /^\/management-concerns\/[^/]+$/);
    expect(request?.headers.get('If-Match')).toBe(CONCERN_ETAG);
    expect(request?.body).toEqual({
      title: { text: 'Collapsed culvert', language: 'en' },
      description: {
        text: 'The culvert under chainage 4+200 collapsed after rain.',
        language: 'en',
      },
      categoryItemId: SITE_CATEGORY_ID,
      priorityItemId: HIGH_PRIORITY_ID,
      // The overdue target date is kept as it was: only a changed date must be today or later.
      targetResolutionDate: day(-2),
    });
    expect(Object.keys(request?.body as object).some((key) => /severity|impact/i.test(key))).toBe(
      false,
    );
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('someone not authorised to manage reads both, edits neither', async () => {
    open(CULVERT_PATH, {}, { session: entitySession() });
    await screen.findByRole('heading', { level: 2, name: 'Collapsed culvert' });
    expect(
      screen.queryByRole('button', { name: en('issuesChallenges.actions.editIssue') }),
    ).toBeNull();
    expect(
      screen.queryByRole('group', { name: en('issuesChallenges.detail.commands') }),
    ).toBeNull();
    expect(screen.queryByRole('combobox', { name: /priority/i })).toBeNull();
    expect(document.querySelector('[data-field="priority"]')?.textContent).toBe('Low');
  });

  test('once the resolution is with validation, nothing is editable', async () => {
    open(CULVERT_PATH, {
      concerns: [
        concern({
          status: 'PENDING_VALIDATION',
          resolution: { text: 'Culvert rebuilt.', language: 'EN' },
          openEscalation: null,
        }),
      ],
    });
    await screen.findByText(en('issuesChallenges.detail.withValidation', { revision: 1 }));
    expect(
      screen.queryByRole('button', { name: en('issuesChallenges.actions.editIssue') }),
    ).toBeNull();
    expect(
      screen.queryByRole('button', { name: en('issuesChallenges.actions.reassess') }),
    ).toBeNull();
  });

  test('a stale edit reads the issue again', async () => {
    const api = open(CULVERT_PATH);
    api.on('PUT', /^\/management-concerns\/[^/]+$/, problem(412, 'PRECONDITION_FAILED'));
    const dialog = await openDialog(
      en('issuesChallenges.actions.editIssue'),
      en('issuesChallenges.form.editIssue'),
    );
    await press(dialog, en('common.actions.save'));
    await screen.findByText(en('issuesChallenges.done.stale'));
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  test('the impact assessment offers the matrix version’s levels and sends levels only', async () => {
    const api = open(`${ISSUES_PATH}/${PERMIT_ID}`);
    api.on('POST', /\/assess$/, { body: concern({ id: PERMIT_ID, openEscalation: null }) });
    const dialog = await openDialog(
      en('issuesChallenges.actions.assess'),
      en('issuesChallenges.assess.title', { concern: 'Missing excavation permit' }),
    );
    await press(dialog, en('issuesChallenges.assess.confirm'));
    expect(
      within(dialog).getByText(en('issuesChallenges.fieldErrors.impactsRequired')),
    ).toBeTruthy();
    expect(api.requestsTo('POST', /\/assess$/)).toHaveLength(0);

    await userEvent.selectOptions(within(dialog).getByRole('combobox', { name: 'Cost' }), '2');
    await userEvent.selectOptions(within(dialog).getByRole('combobox', { name: 'Schedule' }), '4');
    expect(
      within(dialog).getByText(en('issuesChallenges.assess.preview', { level: 4 })),
    ).toBeTruthy();
    await press(dialog, en('issuesChallenges.assess.confirm'));

    await screen.findByText(en('issuesChallenges.done.assessed'));
    expect(api.requestsTo('POST', /\/assess$/)[0]?.body).toEqual({
      impacts: [
        { impactDimensionItemId: COST_DIMENSION_ID, impactLevel: 2, rationale: null },
        { impactDimensionItemId: SCHEDULE_DIMENSION_ID, impactLevel: 4, rationale: null },
      ],
    });
  });

  test('without a published matrix there is nothing to assess against', async () => {
    open(`${ISSUES_PATH}/${PERMIT_ID}`, { matrix: 'missing' });
    const dialog = await openDialog(
      en('issuesChallenges.actions.assess'),
      en('issuesChallenges.assess.title', { concern: 'Missing excavation permit' }),
    );
    expect(within(dialog).queryByRole('combobox')).toBeNull();
    expect(
      within(dialog).queryByRole('button', { name: en('issuesChallenges.assess.confirm') }),
    ).toBeNull();
  });

  test('a resolution is submitted for validation, never blank', async () => {
    const api = open(`${ISSUES_PATH}/${UTILITY_CHALLENGE_ID}`);
    api.on('POST', /\/submit-resolution$/, {
      body: concern({
        id: UTILITY_CHALLENGE_ID,
        status: 'PENDING_VALIDATION',
        openEscalation: null,
      }),
    });
    const dialog = await openDialog(
      en('issuesChallenges.actions.submitResolution'),
      en('issuesChallenges.submit.title', { concern: 'Utility approval pending' }),
    );
    await userEvent.type(within(dialog).getByRole('textbox'), '   ');
    await press(dialog, en('issuesChallenges.submit.confirm'));
    expect(
      within(dialog).getByText(en('issuesChallenges.fieldErrors.resolutionRequired')),
    ).toBeTruthy();
    expect(api.requestsTo('POST', /\/submit-resolution$/)).toHaveLength(0);

    await userEvent.type(within(dialog).getByRole('textbox'), 'Approval obtained.');
    await press(dialog, en('issuesChallenges.submit.confirm'));
    await screen.findByText(en('issuesChallenges.done.submitted'));
    const [request] = api.requestsTo('POST', /\/submit-resolution$/);
    expect(request?.body).toEqual({ resolution: { text: 'Approval obtained.', language: 'en' } });
    expect(request?.headers.get('If-Match')).toBe(CONCERN_ETAG);
  });

  test('an issue is closed only once no escalation is open', async () => {
    open(CULVERT_PATH, {
      concerns: [concern({ status: 'RESOLVED', resolvedAt: '2026-10-01T09:00:00Z' })],
    });
    await screen.findByText(en('issuesChallenges.detail.closeWaitsForEscalation'));
    expect(
      screen.queryByRole('button', { name: en('issuesChallenges.command.close.title') }),
    ).toBeNull();
  });
});

describe('MOD-039 Escalate Item', () => {
  test('says escalation is not approval, refuses a blank reason, and a retry resends the same Idempotency-Key', async () => {
    const api = open(`${ISSUES_PATH}/${PERMIT_ID}`);
    let attempts = 0;
    api.on('POST', /^\/concern-escalations$/, () => {
      attempts += 1;
      return attempts === 1
        ? problem(503, 'SERVICE_UNAVAILABLE')
        : { status: 201, body: escalation({ managementConcernId: PERMIT_ID }) };
    });

    const dialog = await openDialog(
      en('issuesChallenges.actions.escalate'),
      en('issuesChallenges.escalate.title', { concern: 'Missing excavation permit' }),
    );
    expect(within(dialog).getByText(en('issuesChallenges.escalate.notApproval'))).toBeTruthy();
    expect(within(dialog).getByText(en('issuesChallenges.escalate.statusKeptIssue'))).toBeTruthy();
    await press(dialog, en('issuesChallenges.escalate.confirm'));
    expect(
      within(dialog).getByText(en('issuesChallenges.fieldErrors.reasonRequired')),
    ).toBeTruthy();
    expect(api.requestsTo('POST', /^\/concern-escalations$/)).toHaveLength(0);

    await userEvent.type(within(dialog).getByRole('textbox'), 'The permit blocks all excavation.');
    await press(dialog, en('issuesChallenges.escalate.confirm'));
    await waitFor(() => {
      expect(api.requestsTo('POST', /^\/concern-escalations$/)).toHaveLength(1);
    });
    await press(dialog, en('issuesChallenges.escalate.confirm'));
    await screen.findByText(en('issuesChallenges.done.escalated'));

    const [first, retry] = api.requestsTo('POST', /^\/concern-escalations$/);
    expect(first?.body).toEqual({
      managementConcernId: PERMIT_ID,
      reason: { text: 'The permit blocks all excavation.', language: 'en' },
    });
    expect(first?.headers.get('Idempotency-Key')).toBeTruthy();
    expect(retry?.headers.get('Idempotency-Key')).toBe(first?.headers.get('Idempotency-Key'));
  });

  test('a different reason is a different request with its own key', async () => {
    const api = open(`${ISSUES_PATH}/${PERMIT_ID}`);
    api.on('POST', /^\/concern-escalations$/, problem(503, 'SERVICE_UNAVAILABLE'));
    const dialog = await openDialog(
      en('issuesChallenges.actions.escalate'),
      en('issuesChallenges.escalate.title', { concern: 'Missing excavation permit' }),
    );
    await userEvent.type(within(dialog).getByRole('textbox'), 'First reason.');
    await press(dialog, en('issuesChallenges.escalate.confirm'));
    await waitFor(() => {
      expect(api.requestsTo('POST', /^\/concern-escalations$/)).toHaveLength(1);
    });
    await userEvent.type(within(dialog).getByRole('textbox'), ' More.');
    await press(dialog, en('issuesChallenges.escalate.confirm'));
    await waitFor(() => {
      expect(api.requestsTo('POST', /^\/concern-escalations$/)).toHaveLength(2);
    });
    const [first, second] = api.requestsTo('POST', /^\/concern-escalations$/);
    expect(second?.headers.get('Idempotency-Key')).not.toBe(first?.headers.get('Idempotency-Key'));
  });

  test('an issue with an open escalation is not offered another', async () => {
    open(CULVERT_PATH);
    await screen.findByRole('heading', { level: 2, name: 'Collapsed culvert' });
    expect(
      screen.queryByRole('button', { name: en('issuesChallenges.actions.escalate') }),
    ).toBeNull();
  });
});

describe('SCR-087 Escalations (acceptance criterion 2)', () => {
  test('validation check: the list opens on open escalations only, read without any escalation history', async () => {
    const api = open(ESCALATIONS_PATH, {}, { session: reviewerSession() });

    const table = await screen.findByRole('region', {
      name: en('issuesChallenges.escalations.caption'),
    });
    const status = screen.getByRole('combobox', {
      name: en('issuesChallenges.escalations.filter.status'),
    });
    expect((status as HTMLSelectElement).value).toBe('open');
    expect(within(status).getByRole('option', { selected: true }).textContent).toBe(
      en('issuesChallenges.escalations.filter.views.open'),
    );
    const rows = rowsOf(table);
    expect(rows.map((row) => row.dataset.escalation)).toEqual([CULVERT_OPEN_ESCALATION_ID]);
    const openRow = escalationRow(table, CULVERT_OPEN_ESCALATION_ID);
    expect(within(openRow).getByText(en('issuesChallenges.escalationStatus.OPEN'))).toBeTruthy();
    expect(within(openRow).getByText('Department Manager (R03)')).toBeTruthy();
    expect(within(table).queryByText(en('issuesChallenges.escalationStatus.RESOLVED'))).toBeNull();
    expect(
      screen.getByText(en('issuesChallenges.escalations.showing.open', { count: 1 })),
    ).toBeTruthy();
    expect(document.querySelector('[data-summary="open"] dd')?.textContent).toBe('1');
    expect(api.requestsTo('GET', /^\/concern-escalations$/)).toHaveLength(0);
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('validation check: toggled to show them, resolved and withdrawn escalations appear muted, after the open ones', async () => {
    open(ESCALATIONS_PATH, {}, { session: reviewerSession() });
    await screen.findByRole('region', { name: en('issuesChallenges.escalations.caption') });

    await userEvent.selectOptions(
      screen.getByRole('combobox', { name: en('issuesChallenges.escalations.filter.status') }),
      'all',
    );
    const table = await screen.findByRole('region', {
      name: en('issuesChallenges.escalations.caption'),
    });
    await waitFor(() => {
      expect(rowsOf(table)).toHaveLength(3);
    });
    const rows = rowsOf(table);
    expect(rows.map((row) => row.dataset.status)).toEqual(['OPEN', 'WITHDRAWN', 'RESOLVED']);
    expect(rows.map((row) => row.className)).toEqual(['', 'row--muted', 'row--muted']);
    const resolved = escalationRow(table, CULVERT_RESOLVED_ESCALATION_ID);
    expect(
      within(resolved).getByText(en('issuesChallenges.escalationStatus.RESOLVED')),
    ).toBeTruthy();
    // Translated parameters are wrapped in bidi isolates, so the sentence is matched by its parts.
    expect(resolved.textContent).toMatch(/Ended .*by .*You/);

    await userEvent.selectOptions(
      screen.getByRole('combobox', { name: en('issuesChallenges.escalations.filter.status') }),
      'resolved',
    );
    await waitFor(() => {
      expect(
        rowsOf(
          screen.getByRole('region', { name: en('issuesChallenges.escalations.caption') }),
        ).map((row) => row.dataset.escalation),
      ).toEqual([CULVERT_RESOLVED_ESCALATION_ID]);
    });

    await userEvent.selectOptions(
      screen.getByRole('combobox', { name: en('issuesChallenges.escalations.filter.status') }),
      'open',
    );
    await waitFor(() => {
      expect(
        rowsOf(
          screen.getByRole('region', { name: en('issuesChallenges.escalations.caption') }),
        ).map((row) => row.dataset.escalation),
      ).toEqual([CULVERT_OPEN_ESCALATION_ID]);
    });
    expect(
      within(
        screen.getByRole('region', { name: en('issuesChallenges.escalations.caption') }),
      ).queryByText(en('issuesChallenges.escalationStatus.WITHDRAWN')),
    ).toBeNull();
  });

  test('with nothing open, the list says so and how to see the ended ones', async () => {
    open(
      ESCALATIONS_PATH,
      { concerns: [concern({ openEscalation: null })] },
      { session: reviewerSession() },
    );
    await screen.findByText(en('issuesChallenges.escalations.empty.open'));
    expect(screen.getByText(en('issuesChallenges.escalations.empty.hint'))).toBeTruthy();
  });

  test('reads in Arabic, mirrored, without an accessibility violation', async () => {
    open(ESCALATIONS_PATH, {}, { session: reviewerSession(), language: 'ar' });
    await screen.findByRole('region', {
      name: translate('ar', 'issuesChallenges.escalations.caption'),
    });
    expect(document.documentElement.dir).toBe('rtl');
    expect(
      screen.getByText(translate('ar', 'issuesChallenges.escalationStatus.OPEN')),
    ).toBeTruthy();
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });
});

describe('SCR-088 Escalation Detail', () => {
  test('the addressed role resolves it with a direction, even without reading the project', async () => {
    const api = open(
      `${ESCALATIONS_PATH}/${CULVERT_OPEN_ESCALATION_ID}`,
      { projectReadable: false },
      { session: reviewerSession() },
    );
    api.on('POST', /\/resolve$/, {
      body: escalation({ status: 'RESOLVED', resolvedAt: '2026-10-06T10:00:00Z' }),
    });

    await screen.findByRole('heading', {
      level: 1,
      name: en('issuesChallenges.escalationDetail.title', {
        number: 2,
        concern: 'Collapsed culvert',
      }),
    });
    expect(
      screen.getByText(en('issuesChallenges.escalationDetail.projectUnreadable')),
    ).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Collapsed culvert' }).getAttribute('href')).toBe(
      CULVERT_PATH,
    );
    expect(
      screen.queryByRole('button', { name: en('issuesChallenges.withdrawEscalation.confirm') }),
    ).toBeNull();

    const dialog = await openDialog(
      en('issuesChallenges.resolveEscalation.confirm'),
      en('issuesChallenges.resolveEscalation.title', { number: 2 }),
    );
    await press(dialog, en('issuesChallenges.resolveEscalation.confirm'));
    expect(api.requestsTo('POST', /\/resolve$/)).toHaveLength(0);
    await userEvent.type(within(dialog).getByRole('textbox'), 'Fund the culvert from contingency.');
    await press(dialog, en('issuesChallenges.resolveEscalation.confirm'));
    await screen.findByText(en('issuesChallenges.done.escalationResolved'));
    expect(api.requestsTo('POST', /\/resolve$/)[0]?.body).toEqual({
      resolution: { text: 'Fund the culvert from contingency.', language: 'en' },
    });
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });

  test('its escalator withdraws it and is not offered to resolve it', async () => {
    const api = open(`${ESCALATIONS_PATH}/${CULVERT_OPEN_ESCALATION_ID}`);
    api.on('POST', /\/withdraw$/, { body: escalation({ status: 'WITHDRAWN' }) });
    await screen.findByRole('heading', { level: 1 });
    expect(
      screen.queryByRole('button', { name: en('issuesChallenges.resolveEscalation.confirm') }),
    ).toBeNull();
    const dialog = await openDialog(
      en('issuesChallenges.withdrawEscalation.confirm'),
      en('issuesChallenges.withdrawEscalation.title', { number: 2 }),
    );
    await press(dialog, en('issuesChallenges.withdrawEscalation.confirm'));
    await screen.findByText(en('issuesChallenges.done.escalationWithdrawn'));
    expect(api.requestsTo('POST', /\/withdraw$/)).toHaveLength(1);
  });

  test('an ended escalation shows its direction and offers nothing', async () => {
    open(
      `${ESCALATIONS_PATH}/${CULVERT_RESOLVED_ESCALATION_ID}`,
      {},
      { session: reviewerSession() },
    );
    await screen.findByText('The contractor pays; AHDA covers the survey.');
    expect(screen.getByText(en('issuesChallenges.escalationStatus.RESOLVED'))).toBeTruthy();
    expect(
      screen.queryByRole('button', { name: en('issuesChallenges.resolveEscalation.confirm') }),
    ).toBeNull();
  });

  test('a withdrawn escalation reads as withdrawn on its concern', async () => {
    open(`${ISSUES_PATH}/${UTILITY_CHALLENGE_ID}`);
    const table = await screen.findByRole('region', {
      name: en('issuesChallenges.detail.escalationsCaption'),
    });
    const row = escalationRow(table, UTILITY_WITHDRAWN_ESCALATION_ID);
    expect(row.className).toBe('row--muted');
    expect(within(row).getByText(en('issuesChallenges.escalationStatus.WITHDRAWN'))).toBeTruthy();
  });
});

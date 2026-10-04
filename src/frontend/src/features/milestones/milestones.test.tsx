import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { type Language, translate } from '@/shared/i18n/i18n.ts';
import { type MockApi, mockApi, problem } from '@/test/mockApi.ts';
import {
  CERTIFICATE_TYPE_ID,
  coastalRevisions,
  DESIGN_ID,
  EVIDENCE_DOCUMENT_ID,
  EVIDENCE_VERSION_ID,
  FOUNDATIONS_REVISION_ID,
  KEY_DELIVERY_ID,
  milestone,
  MILESTONE_ETAG,
  type MilestoneState,
  pendingEvidence,
  photoLink,
  requiredEvidence,
  REVISION_ETAG,
  revision,
  withMilestones,
} from '@/test/milestoneFixtures.ts';
import {
  ENTITY_USER_ID,
  entitySession,
  projectDetail,
  PROJECT_ID,
  reviewerSession,
  withProject,
  withProjectLookups,
} from '@/test/projectFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';
import { EXCAVATION_ID } from '@/test/scheduleFixtures.ts';
import { day } from '@/test/taskFixtures.ts';

// SCR-046 Project Milestones, SCR-062 Milestone Register, MOD-016 Create/Edit Milestone and MOD-019 Milestone
// Achievement (TASK-051), against the TASK-050 API. Acceptance criteria: (1) submission requires the evidence the policy
// makes mandatory for the category, and until a policy is published the evidence is labelled optional pending policy
// rather than silently allowing an unvalidated submission; (2) a returned achievement shows the reviewer's reason
// prominently. The workbook's validation check: submit without evidence and confirm the policy label matches what the
// backend enforces now; a returned achievement shows the reason without an extra click.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

const MILESTONES_PATH = `/projects/${PROJECT_ID}/milestones`;

/** The Coastal road upgrade, ACTIVE, managed by the entity's user, Huda (TASK-050 D-10, ADR-013). */
function activeProject(overrides: Partial<ProjectDetail> = {}): ProjectDetail {
  return projectDetail({
    status: 'ACTIVE',
    projectManagerUserId: ENTITY_USER_ID,
    activatedAt: '2026-10-01T10:00:00Z',
    ...overrides,
  });
}

function open(
  path: string,
  state: MilestoneState = {},
  {
    session = entitySession(),
    project = activeProject(),
    language = 'en',
  }: { session?: Session; project?: ProjectDetail; language?: Language } = {},
): MockApi {
  const api = withMilestones(withProject(withProjectLookups(mockApi()), project), state);
  renderApp({ path, session, language });
  return api;
}

/** A milestone's MOD-019, opened from its title in a table. */
async function openMilestone(title: string): Promise<HTMLElement> {
  await userEvent.click(await screen.findByRole('button', { name: title }));
  return screen.findByRole('dialog', { name: title });
}

async function press(scope: HTMLElement, name: string) {
  await userEvent.click(within(scope).getByRole('button', { name }));
}

function rowOf(title: string): HTMLElement {
  const row = screen.getByRole('button', { name: title }).closest('tr');
  if (row === null) {
    throw new Error(`No row for ${title}.`);
  }
  return row;
}

function titlesIn(table: HTMLElement): string[] {
  return within(table)
    .getAllByRole('row')
    .slice(1)
    .map((row) => within(row).getAllByRole('button')[0]?.textContent ?? '');
}

describe('SCR-046 Project Milestones', () => {
  test('lists the milestones, earliest forecast first, with category, baseline, status and where each claim stands', async () => {
    open(MILESTONES_PATH);

    const table = await screen.findByRole('region', { name: en('milestones.project.caption') });
    expect(titlesIn(table)).toEqual([
      'Site handover',
      'Design approval',
      'Foundations complete',
      'Temporary access road',
    ]);
    const handover = within(rowOf('Site handover'));
    expect(await handover.findByText('Key delivery')).toBeTruthy();
    expect(handover.getByText(en('milestones.status.ACHIEVED'))).toBeTruthy();
    expect(handover.getByText(en('milestones.achievement.accepted'))).toBeTruthy();
    expect(handover.getByText(en('milestones.table.achievedOn', { date: day(-21) }))).toBeTruthy();
    expect(
      handover.getByText(
        en('milestones.table.baseline', {
          date: day(-22),
          variance: en('schedule.variance.late', { days: 2 }),
        }),
      ),
    ).toBeTruthy();
    expect(
      within(rowOf('Foundations complete')).getByText(en('milestones.achievement.claim.draft')),
    ).toBeTruthy();
    expect(
      within(rowOf('Temporary access road')).getByText(en('milestones.status.CANCELLED')),
    ).toBeTruthy();
  });

  test('criterion 2: a returned claim shows the reviewer’s reason on its row, with no click', async () => {
    open(MILESTONES_PATH);

    await screen.findByRole('region', { name: en('milestones.project.caption') });
    const design = rowOf('Design approval');
    expect(
      within(design).getByText(
        en('milestones.returned.line', { reason: 'Attach the signed design approval letter.' }),
      ),
    ).toBeTruthy();
    expect(within(design).getByText(en('milestones.achievement.claim.returned'))).toBeTruthy();
    expect(design.className).toBe('row--returned');
    expect(within(design).getByText(en('tasks.overdue.many', { days: 2 }))).toBeTruthy();
  });

  test('the Project Manager adds milestones; anyone else does not see the action', async () => {
    open(MILESTONES_PATH);
    expect(
      await screen.findByRole('button', { name: en('milestones.actions.create') }),
    ).toBeTruthy();
  });

  test('another person sees the milestones without the add action', async () => {
    open(MILESTONES_PATH, {}, { session: reviewerSession() });
    await screen.findByRole('region', { name: en('milestones.project.caption') });
    expect(screen.queryByRole('button', { name: en('milestones.actions.create') })).toBeNull();
  });

  test('an empty project tells its Project Manager what to do', async () => {
    open(MILESTONES_PATH, { milestones: [] });
    expect(await screen.findByText(en('milestones.empty.project.title'))).toBeTruthy();
    expect(screen.getByText(en('milestones.empty.project.planner'))).toBeTruthy();
  });

  test('without MILESTONE_VIEW the milestones are listed and the claims are said to be unavailable', async () => {
    open(MILESTONES_PATH, { claims: false });
    await screen.findByRole('region', { name: en('milestones.project.caption') });
    expect(screen.getByText(en('milestones.claimsForbidden'))).toBeTruthy();
    expect(
      within(rowOf('Design approval')).getByText(en('milestones.table.claimsHidden')),
    ).toBeTruthy();
  });

  test('no axe violations on the tab', async () => {
    open(MILESTONES_PATH);
    await screen.findByRole('region', { name: en('milestones.project.caption') });
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });
});

describe('MOD-016 Create/Edit Milestone', () => {
  test('creates a PLANNED milestone with the request the API reads, then opens its MOD-019', async () => {
    const api = open(MILESTONES_PATH);
    api.on('POST', /^\/project-milestones$/, {
      status: 201,
      body: milestone({ title: { text: 'Foundations complete', language: 'EN' } }),
    });

    await userEvent.click(
      await screen.findByRole('button', { name: en('milestones.actions.create') }),
    );
    const dialog = await screen.findByRole('dialog', { name: en('milestones.form.createTitle') });
    await press(dialog, en('common.actions.save'));
    expect(within(dialog).getByText(en('common.form.fixErrors', { count: 3 }))).toBeTruthy();
    expect(api.requestsTo('POST', /^\/project-milestones$/)).toHaveLength(0);

    await userEvent.type(within(dialog).getByLabelText(/^Title/), 'Foundations complete');
    fireEvent.change(await within(dialog).findByLabelText(/^Category/), {
      target: { value: KEY_DELIVERY_ID },
    });
    fireEvent.change(within(dialog).getByLabelText(/^Forecast date/), {
      target: { value: day(10) },
    });
    fireEvent.change(await within(dialog).findByLabelText(/^Schedule activity/), {
      target: { value: EXCAVATION_ID },
    });
    await press(dialog, en('common.actions.save'));

    await screen.findByRole('dialog', { name: 'Foundations complete' });
    expect(api.requestsTo('POST', /^\/project-milestones$/)[0]?.body).toEqual({
      projectId: PROJECT_ID,
      title: { text: 'Foundations complete', language: 'en' },
      milestoneCategoryItemId: KEY_DELIVERY_ID,
      forecastDate: day(10),
      scheduleActivityId: EXCAVATION_ID,
      sortOrder: 0,
    });
  });

  test('without the categories (MASTER_DATA_VIEW) a milestone cannot be added, and the form says why', async () => {
    open(MILESTONES_PATH, { catalogues: false });
    await userEvent.click(
      await screen.findByRole('button', { name: en('milestones.actions.create') }),
    );
    const dialog = await screen.findByRole('dialog', { name: en('milestones.form.createTitle') });
    expect(within(dialog).getByText(en('milestones.form.categoriesUnreadableCreate'))).toBeTruthy();
  });

  test('an edit sends the ETag; a milestone changed meanwhile (412) is read again', async () => {
    const api = open(MILESTONES_PATH);
    api.on('PUT', /^\/project-milestones\/[^/]+$/, problem(412, 'PRECONDITION_FAILED'));

    const drawer = await openMilestone('Foundations complete');
    await press(drawer, en('milestones.actions.edit'));
    const dialog = await screen.findByRole('dialog', { name: en('milestones.form.editTitle') });
    const title = await within(dialog).findByLabelText(/^Title/);
    await userEvent.clear(title);
    await userEvent.type(title, 'Foundations poured');
    await press(dialog, en('common.actions.save'));

    await screen.findByRole('dialog', { name: 'Foundations complete' });
    expect(api.requestsTo('PUT', /^\/project-milestones\//)[0]?.headers.get('If-Match')).toBe(
      MILESTONE_ETAG,
    );
    expect(await screen.findByText(en('milestones.done.stale'))).toBeTruthy();
  });
});

describe('MOD-019 Milestone Achievement', () => {
  test('criterion 2: a returned claim’s reason is the first thing in the drawer, with who returned it and when', async () => {
    open(MILESTONES_PATH);

    const drawer = await openMilestone('Design approval');
    const callout = await within(drawer).findByRole('region', {
      name: en('milestones.returned.titleBy', { reviewer: 'Faisal Reviewer', date: day(-3) }),
    });
    expect(within(callout).getByText('Attach the signed design approval letter.')).toBeTruthy();
    // Before the details and the claim: nothing to expand or scroll to.
    const details = within(drawer).getByText(en('milestones.fields.category'));
    expect(
      callout.compareDocumentPosition(details) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect(
      within(drawer).getByRole('button', { name: en('milestones.claim.start.afterReturn') }),
    ).toBeTruthy();
  });

  test('claims again after a return: a new DRAFT revision with the request the API reads', async () => {
    const api = open(MILESTONES_PATH);
    api.on('POST', /^\/milestone-achievements$/, { status: 201, body: revision({ id: 'new' }) });

    const drawer = await openMilestone('Design approval');
    await press(drawer, en('milestones.claim.start.afterReturn'));
    fireEvent.change(within(drawer).getByLabelText(/^Date achieved/), {
      target: { value: day(1) },
    });
    await press(drawer, en('milestones.claim.create'));
    expect(
      within(drawer).getAllByText(en('milestones.fieldErrors.claimedDateInFuture')).length,
    ).toBeGreaterThan(0);
    expect(api.requestsTo('POST', /^\/milestone-achievements$/)).toHaveLength(0);

    fireEvent.change(within(drawer).getByLabelText(/^Date achieved/), {
      target: { value: day(-2) },
    });
    await userEvent.type(within(drawer).getByLabelText(/^What was achieved/), 'Letter signed.');
    await press(drawer, en('milestones.claim.create'));

    await waitFor(() => {
      expect(api.requestsTo('POST', /^\/milestone-achievements$/)).toHaveLength(1);
    });
    expect(api.requestsTo('POST', /^\/milestone-achievements$/)[0]?.body).toEqual({
      projectMilestoneId: DESIGN_ID,
      claimedAchievementDate: day(-2),
      narrative: { text: 'Letter signed.', language: 'en' },
    });
  });

  test('criterion 1, no policy published: evidence is labelled optional pending policy, and a claim with none is sent only after the person confirms', async () => {
    const api = open(MILESTONES_PATH);
    api.on('POST', /\/submit$/, { body: revision({ status: 'SUBMITTED' }) });

    const drawer = await openMilestone('Foundations complete');
    const label = await within(drawer).findByText(en('milestones.evidence.pending.title'));
    expect(label.closest('[data-policy]')?.getAttribute('data-policy')).toBe('pending');
    expect(within(drawer).getByText(en('milestones.evidence.pending.body'))).toBeTruthy();
    expect(within(drawer).getByText(en('milestones.evidence.none'))).toBeTruthy();

    await press(drawer, en('milestones.submit.action'));
    expect(within(drawer).getByText(en('milestones.submit.confirmWithoutEvidence'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/submit$/)).toHaveLength(0);

    await press(drawer, en('milestones.submit.confirm'));
    await waitFor(() => {
      expect(api.requestsTo('POST', /\/submit$/)).toHaveLength(1);
    });
    const submitted = api.requestsTo('POST', /\/submit$/)[0];
    expect(submitted?.path).toBe(`/milestone-achievements/${FOUNDATIONS_REVISION_ID}/submit`);
    expect(submitted?.headers.get('If-Match')).toBe(REVISION_ETAG);
  });

  test('validation check: when the server enforces a policy the label did not show, its refusal is shown and the label follows the server', async () => {
    const evidence = { [FOUNDATIONS_REVISION_ID]: pendingEvidence() };
    const api = open(MILESTONES_PATH, { evidence });
    api.on('POST', /\/submit$/, () => {
      // A policy was published meanwhile: the server now requires the certificate.
      evidence[FOUNDATIONS_REVISION_ID] = requiredEvidence();
      return problem(422, 'MILESTONE_EVIDENCE_REQUIRED', [{ field: 'evidence', code: 'REQUIRED' }]);
    });

    const drawer = await openMilestone('Foundations complete');
    await within(drawer).findByText(en('milestones.evidence.pending.title'));
    await press(drawer, en('milestones.submit.action'));
    await press(drawer, en('milestones.submit.confirm'));

    expect(
      await within(drawer).findByText(en('milestones.problems.evidenceRequired')),
    ).toBeTruthy();
    expect(await within(drawer).findByText(en('milestones.evidence.required.title'))).toBeTruthy();
    expect(within(drawer).queryByText(en('milestones.evidence.pending.title'))).toBeNull();
    expect(
      within(drawer).getByRole('button', { name: en('milestones.submit.action') }),
    ).toHaveProperty('disabled', true);
  });

  test('criterion 1, a policy in force: a missing mandatory type blocks submission and is named; nothing is sent', async () => {
    const api = open(MILESTONES_PATH, {
      evidence: {
        [FOUNDATIONS_REVISION_ID]: requiredEvidence({
          links: [photoLink()],
          satisfiedEvidenceTypeItemIds: [],
        }),
      },
    });

    const drawer = await openMilestone('Foundations complete');
    const policy = await within(drawer).findByText(en('milestones.evidence.required.title'));
    expect(policy.closest('[data-policy]')?.getAttribute('data-policy')).toBe('required');
    const submit = within(drawer).getByRole('button', { name: en('milestones.submit.action') });
    expect(submit).toHaveProperty('disabled', true);
    const reason = document.getElementById(submit.getAttribute('aria-describedby') ?? '');
    expect(reason?.textContent).toBe(
      en('milestones.submit.missing', { types: 'Completion certificate' }),
    );
    expect(api.requestsTo('POST', /\/submit$/)).toHaveLength(0);
  });

  test('criterion 1: once the mandatory evidence is held, the claim is submitted without a question', async () => {
    const api = open(MILESTONES_PATH, {
      evidence: {
        [FOUNDATIONS_REVISION_ID]: requiredEvidence({
          links: [photoLink()],
          satisfiedEvidenceTypeItemIds: [CERTIFICATE_TYPE_ID],
        }),
      },
    });
    api.on('POST', /\/submit$/, { body: revision({ status: 'SUBMITTED' }) });

    const drawer = await openMilestone('Foundations complete');
    await within(drawer).findByText(en('milestones.evidence.required.held'));
    await press(drawer, en('milestones.submit.action'));
    await waitFor(() => {
      expect(api.requestsTo('POST', /\/submit$/)).toHaveLength(1);
    });
    expect(screen.queryByText(en('milestones.submit.confirmWithoutEvidence'))).toBeNull();
  });

  test('an undetermined policy (422 CONFIGURATION_MISSING) blocks submission and says so', async () => {
    const api = open(MILESTONES_PATH);
    api.on('GET', /\/evidence$/, problem(422, 'CONFIGURATION_MISSING'));

    const drawer = await openMilestone('Foundations complete');
    expect(
      await within(drawer).findByText(en('milestones.evidence.undetermined.title')),
    ).toBeTruthy();
    expect(
      within(drawer).getByRole('button', { name: en('milestones.submit.action') }),
    ).toHaveProperty('disabled', true);
  });

  test('attaches a clean document as evidence of a type through MOD-054, pinning its latest version with the ETag', async () => {
    const api = open(MILESTONES_PATH, {
      evidence: { [FOUNDATIONS_REVISION_ID]: requiredEvidence() },
    });
    api.on('POST', /\/evidence$/, { status: 201, body: photoLink().evidence[0] });

    const drawer = await openMilestone('Foundations complete');
    await within(drawer).findByText(en('milestones.evidence.required.title'));
    await press(drawer, en('milestones.evidence.attach'));
    const attach = await screen.findByRole('dialog', { name: en('documents.attach.title') });
    await userEvent.click(
      await within(attach).findByRole('radio', { name: 'Foundations completion certificate' }),
    );
    await press(attach, en('documents.attach.confirm'));
    expect(within(attach).getByText(en('common.fieldErrors.required'))).toBeTruthy();

    fireEvent.change(within(attach).getByLabelText(/^Evidence of/), {
      target: { value: CERTIFICATE_TYPE_ID },
    });
    await press(attach, en('documents.attach.confirm'));

    await screen.findByRole('dialog', { name: 'Foundations complete' });
    const sent = api.requestsTo('POST', /\/evidence$/)[0];
    expect(sent?.path).toBe(`/milestone-achievements/${FOUNDATIONS_REVISION_ID}/evidence`);
    expect(sent?.body).toEqual({
      documentId: EVIDENCE_DOCUMENT_ID,
      documentVersionId: EVIDENCE_VERSION_ID,
      evidenceTypeItemId: CERTIFICATE_TYPE_ID,
    });
    expect(sent?.headers.get('If-Match')).toBe(REVISION_ETAG);
  });

  test('withdraws a piece of evidence, named, with the ETag', async () => {
    const api = open(MILESTONES_PATH, {
      evidence: { [FOUNDATIONS_REVISION_ID]: pendingEvidence({ links: [photoLink()] }) },
    });
    api.on('POST', /\/withdraw$/, { body: photoLink().evidence[0] });

    const drawer = await openMilestone('Foundations complete');
    await userEvent.click(
      await within(drawer).findByRole('button', {
        name: en('milestones.evidence.withdrawNamed', {
          document: 'Foundations completion certificate',
        }),
      }),
    );
    await waitFor(() => {
      expect(api.requestsTo('POST', /\/withdraw$/)).toHaveLength(1);
    });
    expect(api.requestsTo('POST', /\/withdraw$/)[0]?.headers.get('If-Match')).toBe(REVISION_ETAG);
  });

  test('a draft is saved with the ETag, and submission waits for the change to be saved', async () => {
    const api = open(MILESTONES_PATH);
    api.on('PUT', /^\/milestone-achievements\/[^/]+$/, { body: revision() });

    const drawer = await openMilestone('Foundations complete');
    const narrative = await within(drawer).findByLabelText(/^What was achieved/);
    await userEvent.type(narrative, 'Slab poured.');
    const submit = within(drawer).getByRole('button', { name: en('milestones.submit.action') });
    expect(submit).toHaveProperty('disabled', true);
    expect(within(drawer).getByText(en('milestones.submit.saveFirst'))).toBeTruthy();

    await press(drawer, en('milestones.claim.save'));
    await waitFor(() => {
      expect(api.requestsTo('PUT', /^\/milestone-achievements\//)).toHaveLength(1);
    });
    const saved = api.requestsTo('PUT', /^\/milestone-achievements\//)[0];
    expect(saved?.headers.get('If-Match')).toBe(REVISION_ETAG);
    expect(saved?.body).toEqual({
      claimedAchievementDate: day(-1),
      narrative: { text: 'Slab poured.', language: 'en' },
    });
  });

  test('anyone but the Project Manager reads the draft without changing or submitting it', async () => {
    open(MILESTONES_PATH, {}, { session: reviewerSession() });

    const drawer = await openMilestone('Foundations complete');
    expect(await within(drawer).findByText(en('milestones.claim.draftReadOnly'))).toBeTruthy();
    expect(within(drawer).getByText(en('milestones.evidence.pending.title'))).toBeTruthy();
    expect(
      within(drawer).queryByRole('button', { name: en('milestones.submit.action') }),
    ).toBeNull();
    expect(
      within(drawer).queryByRole('button', { name: en('milestones.actions.edit') }),
    ).toBeNull();
  });

  test('an accepted achievement says when and by whom, keeps its revisions, and is corrected by a new one', async () => {
    open(MILESTONES_PATH);

    const drawer = await openMilestone('Site handover');
    expect(
      await within(drawer).findByText(
        en('milestones.detail.accepted', {
          date: day(-21),
          revision: 1,
          reviewer: 'Faisal Reviewer',
        }),
      ),
    ).toBeTruthy();
    expect(within(drawer).getByText(en('milestones.claim.correctionNote'))).toBeTruthy();
    expect(
      within(drawer).getByRole('button', { name: en('milestones.claim.start.correction') }),
    ).toBeTruthy();
    expect(within(drawer).getByText(en('milestones.revision.current'))).toBeTruthy();
  });

  test('a cancelled milestone cannot be claimed, and says why', async () => {
    open(MILESTONES_PATH);
    const drawer = await openMilestone('Temporary access road');
    expect(await within(drawer).findByText(en('milestones.claim.why.cancelled'))).toBeTruthy();
  });

  test('no axe violations in the drawer', async () => {
    open(MILESTONES_PATH, {
      evidence: { [FOUNDATIONS_REVISION_ID]: requiredEvidence({ links: [photoLink()] }) },
    });
    const drawer = await openMilestone('Foundations complete');
    await within(drawer).findByText(en('milestones.evidence.required.title'));
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });
});

describe('SCR-062 Milestone Register', () => {
  test('lists every visible project’s milestones, earliest forecast first, with the project; a filter narrows it', async () => {
    open('/milestones');

    const table = await screen.findByRole('region', { name: en('milestones.register.caption') });
    expect(titlesIn(table)).toEqual([
      'Site handover',
      'Bridge deck poured',
      'Design approval',
      'Foundations complete',
      'Temporary access road',
    ]);
    expect(
      within(rowOf('Bridge deck poured')).getByRole('link', { name: 'Harbour bridge repair' }),
    ).toBeTruthy();
    expect(
      within(rowOf('Bridge deck poured')).getByText(en('milestones.achievement.claim.submitted')),
    ).toBeTruthy();

    fireEvent.change(screen.getByLabelText(en('milestones.filter.label')), {
      target: { value: 'returned' },
    });
    expect(titlesIn(table)).toEqual(['Design approval']);
    expect(
      screen.getByText(
        en('milestones.returned.line', { reason: 'Attach the signed design approval letter.' }),
      ),
    ).toBeTruthy();

    fireEvent.change(screen.getByLabelText(en('milestones.filter.label')), {
      target: { value: 'cancelled' },
    });
    expect(titlesIn(table)).toEqual(['Temporary access road']);
  });

  test('opens MOD-019 for another project’s milestone: a submitted claim is read only', async () => {
    open('/milestones');

    const drawer = await openMilestone('Bridge deck poured');
    expect(
      await within(drawer).findByText(
        en('milestones.claim.submittedBy', { person: 'Faisal Reviewer', date: day(-5) }),
      ),
    ).toBeTruthy();
    expect(
      within(drawer).queryByRole('button', { name: en('milestones.submit.action') }),
    ).toBeNull();
  });

  test('an empty register says how milestones get there', async () => {
    open('/milestones', { milestones: [], revisions: [] });
    expect(await screen.findByText(en('milestones.empty.register.title'))).toBeTruthy();
  });

  test('a filter that matches nothing says so', async () => {
    open('/milestones', {
      revisions: coastalRevisions().filter((item) => item.status !== 'RETURNED'),
    });
    await screen.findByRole('region', { name: en('milestones.register.caption') });
    fireEvent.change(screen.getByLabelText(en('milestones.filter.label')), {
      target: { value: 'returned' },
    });
    expect(screen.getByText(en('milestones.empty.filtered.title'))).toBeTruthy();
  });

  test('reached from the sidebar, and renders in Arabic without axe violations', async () => {
    open('/milestones', {}, { language: 'ar' });
    await screen.findByRole('region', { name: translate('ar', 'milestones.register.caption') });
    expect(
      screen.getByRole('link', { name: translate('ar', 'milestones.nav.register') }),
    ).toBeTruthy();
    const violations = await accessibilityViolations();
    expect(violations, describeViolations(violations)).toEqual([]);
  });
});

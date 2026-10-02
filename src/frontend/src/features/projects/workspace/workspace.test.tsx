import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import { type MockApi, mockApi, page, problem } from '@/test/mockApi.ts';
import {
  ENTITY_USER_ID,
  entitySession,
  ETAG,
  FULL_PROFILE_ID,
  MAKKAH_REGION_ID,
  OTHER_DEPARTMENT_ID,
  projectDetail,
  PROJECT_ID,
  REVIEWER_ID,
  reviewerSession,
  RIYADH_CITY_ID,
  RIYADH_REGION_ID,
  RUN_ID,
  withProject,
  withProjectLookups,
} from '@/test/projectFixtures.ts';
import { accessibilityViolations, describeViolations, renderApp } from '@/test/renderApp.tsx';

// SCR-040 Project Workspace with its tabs (SCR-041, SCR-042, SCR-043, SCR-035) and MOD-001 to MOD-003. Acceptance
// criterion 3: the tabs follow the signed-in person's RBAC scope.

const en = (key: string, params?: Record<string, string | number>) => translate('en', key, params);

function openWorkspace(
  project: ProjectDetail,
  session: Session,
  path = `/projects/${project.id}`,
): MockApi {
  const api = withProject(withProjectLookups(mockApi()), project)
    .on('GET', /^\/approval-instances$/, {
      body: page([
        {
          id: RUN_ID,
          subject: { module: 'Project', type: 'Project', id: project.id, revisionNo: 1 },
          routingKey: 'PROJECT_REGISTRATION',
          requestedByUserId: REVIEWER_ID,
          requestedAt: '2026-10-01T10:00:00Z',
          status: 'RETURNED',
          completedAt: '2026-10-02T10:00:00Z',
        },
      ]),
    })
    .on('GET', /^\/documents$/, { body: page([]) });
  renderApp({ path, session });
  return api;
}

function tabNames(): string[] {
  const tabs = screen.getByRole('navigation', { name: en('projects.workspace.tabs.label') });
  return within(tabs)
    .getAllByRole('link')
    .map((link) => link.textContent);
}

const TAB = {
  overview: en('projects.workspace.tabs.overview'),
  registration: en('projects.workspace.tabs.registration'),
  location: en('projects.workspace.tabs.location'),
  progress: en('projects.workspace.tabs.progress'),
  reviews: en('projects.workspace.tabs.reviews'),
  documents: en('projects.workspace.tabs.documents'),
};

const button = (key: string) => screen.queryByRole('button', { name: en(key) });

describe('SCR-040 workspace tabs follow the RBAC scope', () => {
  test('an AHDA user whose department owns the project sees every tab', async () => {
    openWorkspace(projectDetail(), reviewerSession());
    await screen.findByRole('heading', { level: 1, name: 'Coastal road upgrade' });

    expect(tabNames()).toEqual([
      TAB.overview,
      TAB.registration,
      TAB.location,
      TAB.progress,
      TAB.reviews,
      TAB.documents,
    ]);
  });

  test('an external entity user reaching the project sees no review history (ADR-013; TASK-035 F-8)', async () => {
    openWorkspace(projectDetail(), entitySession());
    await screen.findByRole('heading', { level: 1 });

    expect(tabNames()).toEqual([
      TAB.overview,
      TAB.registration,
      TAB.location,
      TAB.progress,
      TAB.documents,
    ]);
  });

  test('an AHDA user whose assignments do not reach the project sees only what the API showed them', async () => {
    openWorkspace(projectDetail(), reviewerSession(OTHER_DEPARTMENT_ID));
    await screen.findByRole('heading', { level: 1 });

    expect(tabNames()).toEqual([TAB.overview, TAB.registration, TAB.location]);
    // Nothing to change either: no assignment reaches it.
    expect(button('projects.actions.submit')).toBeNull();
    expect(screen.queryByRole('link', { name: en('projects.actions.edit') })).toBeNull();
  });

  test('a tab opened by its address is refused to someone it is not offered to, and nothing is read', async () => {
    const api = openWorkspace(projectDetail(), entitySession(), `/projects/${PROJECT_ID}/reviews`);

    expect(await screen.findByText(en('projects.workspace.tabUnavailable'))).toBeTruthy();
    expect(api.requestsTo('GET', /^\/approval-instances$/)).toHaveLength(0);
  });

  test('SCR-042 lists the review runs of the project for an AHDA user', async () => {
    const api = openWorkspace(
      projectDetail({ status: 'RETURNED' }),
      reviewerSession(),
      `/projects/${PROJECT_ID}/reviews`,
    );

    const table = await screen.findByRole('table', { name: en('projects.reviews.caption') });
    expect(
      within(table)
        .getByRole('link', { name: en('projects.reviews.revision', { revision: 1 }) })
        .getAttribute('href'),
    ).toBe(`/approvals/instances/${RUN_ID}`);
    expect(within(table).getByText(en('approvals.instanceStatus.RETURNED'))).toBeTruthy();
    expect(api.requestsTo('GET', /^\/approval-instances$/)[0]?.query.toString()).toBe(
      `subjectModule=Project&subjectType=Project&subjectId=${PROJECT_ID}`,
    );
  });

  test('SCR-043 lists the project’s documents inside the workspace', async () => {
    const api = openWorkspace(
      projectDetail(),
      entitySession(),
      `/projects/${PROJECT_ID}/documents`,
    );

    expect(
      await screen.findByRole('heading', { level: 2, name: en('projects.documents.title') }),
    ).toBeTruthy();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(api.requestsTo('GET', /^\/documents$/)[0]?.query.get('projectId')).toBe(PROJECT_ID);
  });

  test('has no axe violation', async () => {
    openWorkspace(projectDetail(), reviewerSession());
    await screen.findByRole('heading', { level: 1 });
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });
});

describe('SCR-040 actions follow the state and the person', () => {
  test('the creator of a draft may edit, submit and delete it; there is no status to change', async () => {
    openWorkspace(projectDetail(), entitySession());
    await screen.findByRole('heading', { level: 1 });

    expect(screen.getByRole('link', { name: en('projects.actions.edit') })).toBeTruthy();
    expect(button('projects.actions.submit')).not.toBeNull();
    expect(button('projects.actions.delete')).not.toBeNull();
    expect(button('projects.actions.changeStatus')).toBeNull();
  });

  test('someone else’s draft cannot be deleted by them', async () => {
    openWorkspace(projectDetail({ createdBy: REVIEWER_ID }), entitySession());
    await screen.findByRole('heading', { level: 1 });

    expect(button('projects.actions.delete')).toBeNull();
  });

  test('an external user may only withdraw a submission; AHDA may also start the review', async () => {
    const user = userEvent.setup();
    openWorkspace(
      projectDetail({ status: 'SUBMITTED', projectManagerUserId: ENTITY_USER_ID }),
      entitySession(),
    );
    await user.click(
      await screen.findByRole('button', { name: en('projects.actions.changeStatus') }),
    );
    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog)
        .getAllByRole('radio')
        .map((radio) => radio.parentElement?.textContent),
    ).toEqual([en('projects.changeStatus.withdraw.label')]);
  });

  test('an AHDA user is offered the review on a submission and activation on an approved project', async () => {
    const user = userEvent.setup();
    openWorkspace(projectDetail({ status: 'SUBMITTED' }), reviewerSession());
    await user.click(
      await screen.findByRole('button', { name: en('projects.actions.changeStatus') }),
    );
    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog)
        .getAllByRole('radio')
        .map((radio) => radio.parentElement?.textContent),
    ).toEqual([
      en('projects.changeStatus.withdraw.label'),
      en('projects.changeStatus.startReview.label'),
    ]);
  });

  test('an external user is never offered activation (ADR-013: AHDA keeps the gates)', async () => {
    openWorkspace(
      projectDetail({ status: 'APPROVED_PLANNED', formalProjectId: 'PRJ-000001' }),
      entitySession(),
    );
    await screen.findByRole('heading', { level: 1 });

    expect(button('projects.actions.changeStatus')).toBeNull();
    expect(
      screen.getByText(en('projects.formalProjectId.issued', { id: 'PRJ-000001' })),
    ).toBeTruthy();
  });
});

describe('MOD-001 Delete Draft', () => {
  test('is confirmed, sends the ETag, and returns to the register', async () => {
    const user = userEvent.setup();
    const api = openWorkspace(projectDetail(), entitySession());
    api
      .on('DELETE', /^\/projects\//, { status: 204 })
      .on('GET', /^\/projects$/, { body: page([]) });

    await user.click(await screen.findByRole('button', { name: en('projects.actions.delete') }));
    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog).getByText(en('projects.delete.body', { title: 'Coastal road upgrade' })),
    ).toBeTruthy();
    await user.click(within(dialog).getByRole('button', { name: en('projects.delete.confirm') }));

    expect(await screen.findByText(en('projects.done.deleted'))).toBeTruthy();
    expect(
      screen.getByRole('heading', { level: 1, name: en('projects.register.title') }),
    ).toBeTruthy();
    expect(api.requestsTo('DELETE', /^\/projects\//)[0]?.headers.get('If-Match')).toBe(ETAG);
  });

  test('a draft another record names is refused and stays', async () => {
    const user = userEvent.setup();
    const api = openWorkspace(projectDetail(), entitySession());
    api.on('DELETE', /^\/projects\//, problem(409, 'PROJECT_IN_USE'));

    await user.click(await screen.findByRole('button', { name: en('projects.actions.delete') }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: en('projects.delete.confirm') }));

    expect(await screen.findByText(en('projects.problems.inUse'))).toBeTruthy();
    expect(screen.getByRole('heading', { level: 1, name: 'Coastal road upgrade' })).toBeTruthy();
  });
});

describe('MOD-002 Assign Project Manager (submit for review)', () => {
  test('is enabled only once the budget and planned dates are in, and says what is missing', async () => {
    const user = userEvent.setup();
    const api = openWorkspace(
      projectDetail({ registrationBudgetSar: null, plannedEndDate: null }),
      entitySession(),
    );

    await user.click(await screen.findByRole('button', { name: en('projects.actions.submit') }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(en('projects.fields.registrationBudget'))).toBeTruthy();
    expect(within(dialog).getByText(en('projects.fields.plannedEndDate'))).toBeTruthy();
    expect(
      within(dialog).getByRole('button', {
        name: en('projects.submit.confirm'),
      }),
    ).toHaveProperty('disabled', true);
    expect(api.requestsTo('POST', /\/submit$/)).toHaveLength(0);
  });

  test('the governance profile’s mandatory fields are needed too (ADR-015)', async () => {
    const user = userEvent.setup();
    openWorkspace(
      projectDetail({ governanceProfileItemId: FULL_PROFILE_ID, description: null }),
      entitySession(),
    );

    await user.click(await screen.findByRole('button', { name: en('projects.actions.submit') }));
    const dialog = await screen.findByRole('dialog');
    const missing = within(dialog).getByRole('list');
    expect(
      within(missing)
        .getAllByRole('listitem')
        .map((item) => item.textContent),
    ).toEqual([en('projects.fields.description'), en('projects.fields.region')]);
    expect(
      within(dialog).getByRole('button', {
        name: en('projects.submit.confirm'),
      }),
    ).toHaveProperty('disabled', true);
  });

  test('names the submitter as Project Manager by default and sends the ETag', async () => {
    const user = userEvent.setup();
    const api = openWorkspace(projectDetail(), entitySession());
    api.on('POST', /\/submit$/, { body: projectDetail({ status: 'SUBMITTED' }) });

    await user.click(await screen.findByRole('button', { name: en('projects.actions.submit') }));
    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog).getByLabelText(en('projects.submit.self', { name: 'Huda Contractor' })),
    ).toHaveProperty('checked', true);
    api.on('GET', new RegExp(`^/projects/${PROJECT_ID}$`), {
      body: projectDetail({ status: 'SUBMITTED', projectManagerUserId: ENTITY_USER_ID }),
      headers: { ETag: '"12"' },
    });
    await user.click(within(dialog).getByRole('button', { name: en('projects.submit.confirm') }));

    expect(await screen.findByText(en('projects.done.submitted'))).toBeTruthy();
    const [request] = api.requestsTo('POST', /\/submit$/);
    expect(request?.body).toEqual({ projectManagerUserId: ENTITY_USER_ID });
    expect(request?.headers.get('If-Match')).toBe(ETAG);
    // The workspace shows the project as the API now has it.
    expect(await screen.findByText(en('projects.status.SUBMITTED'))).toBeTruthy();
  });

  test('an ineligible Project Manager is explained', async () => {
    const user = userEvent.setup();
    const api = openWorkspace(projectDetail(), entitySession());
    api.on('POST', /\/submit$/, problem(422, 'PROJECT_MANAGER_INVALID'));

    await user.click(await screen.findByRole('button', { name: en('projects.actions.submit') }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: en('projects.submit.confirm') }));

    expect(await screen.findByText(en('projects.problems.managerInvalid'))).toBeTruthy();
  });
});

describe('MOD-003 Change Status', () => {
  test('starting the review is one command, sent with the ETag', async () => {
    const user = userEvent.setup();
    const api = openWorkspace(projectDetail({ status: 'SUBMITTED' }), reviewerSession());
    api.on('POST', /\/start-review$/, { body: projectDetail({ status: 'UNDER_REVIEW' }) });

    await user.click(
      await screen.findByRole('button', { name: en('projects.actions.changeStatus') }),
    );
    const dialog = await screen.findByRole('dialog');
    const apply = within(dialog).getByRole('button', { name: en('projects.changeStatus.confirm') });
    expect(apply).toHaveProperty('disabled', true);
    await user.click(within(dialog).getByLabelText(en('projects.changeStatus.startReview.label')));
    expect(
      within(dialog).getByText(en('projects.changeStatus.startReview.consequence')),
    ).toBeTruthy();
    await user.click(apply);

    expect(await screen.findByText(en('projects.done.reviewStarted'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/start-review$/)[0]?.headers.get('If-Match')).toBe(ETAG);
    expect(api.requestsTo('POST', /\/withdraw$/)).toHaveLength(0);
  });

  test('no review route configured is explained', async () => {
    const user = userEvent.setup();
    const api = openWorkspace(projectDetail({ status: 'SUBMITTED' }), reviewerSession());
    api.on('POST', /\/start-review$/, problem(422, 'CONFIGURATION_MISSING'));

    await user.click(
      await screen.findByRole('button', { name: en('projects.actions.changeStatus') }),
    );
    await user.click(await screen.findByLabelText(en('projects.changeStatus.startReview.label')));
    await user.click(screen.getByRole('button', { name: en('projects.changeStatus.confirm') }));

    expect(await screen.findByText(en('projects.problems.reviewRouteMissing'))).toBeTruthy();
  });

  test('activation is its own command on an approved project', async () => {
    const user = userEvent.setup();
    const api = openWorkspace(
      projectDetail({ status: 'APPROVED_PLANNED', formalProjectId: 'PRJ-000001' }),
      reviewerSession(),
    );
    api.on('POST', /\/activate$/, { body: projectDetail({ status: 'ACTIVE' }) });

    await user.click(
      await screen.findByRole('button', { name: en('projects.actions.changeStatus') }),
    );
    const dialog = await screen.findByRole('dialog');
    // The only command: chosen already.
    await user.click(
      within(dialog).getByRole('button', { name: en('projects.changeStatus.confirm') }),
    );

    expect(await screen.findByText(en('projects.done.activated'))).toBeTruthy();
    expect(api.requestsTo('POST', /\/activate$/)).toHaveLength(1);
  });
});

describe('SCR-035 Project Location', () => {
  test('changes only the location, as the whole registration under the ETag', async () => {
    const user = userEvent.setup();
    const project = projectDetail({ regionItemId: RIYADH_REGION_ID, cityItemId: RIYADH_CITY_ID });
    const api = openWorkspace(project, entitySession(), `/projects/${PROJECT_ID}/location`);
    api.on('PUT', /^\/projects\//, { body: project });

    expect(await screen.findByText('Riyadh Region')).toBeTruthy();
    expect(screen.getByText(en('projects.location.noMap'))).toBeTruthy();
    await user.click(screen.getByRole('button', { name: en('projects.location.edit') }));
    await user.selectOptions(screen.getByLabelText(/Region/), MAKKAH_REGION_ID);
    expect(screen.getByLabelText(/City/)).toHaveProperty('value', '');
    await user.type(screen.getByLabelText(/Latitude/), '21.5');
    await user.click(screen.getByRole('button', { name: en('common.actions.save') }));

    expect(await screen.findByText(en('projects.done.locationUpdated'))).toBeTruthy();
    const [request] = api.requestsTo('PUT', /^\/projects\//);
    expect(request?.headers.get('If-Match')).toBe(ETAG);
    expect(request?.body).toMatchObject({
      title: { text: 'Coastal road upgrade', language: 'en' },
      registrationBudgetSar: '1500000.00',
      regionItemId: MAKKAH_REGION_ID,
      cityItemId: null,
      latitude: 21.5,
    });
  });

  test('is read-only once the project is under review', async () => {
    openWorkspace(
      projectDetail({ status: 'UNDER_REVIEW' }),
      entitySession(),
      `/projects/${PROJECT_ID}/location`,
    );
    await screen.findByText(en('projects.location.noMap'));

    expect(screen.queryByRole('button', { name: en('projects.location.edit') })).toBeNull();
  });
});

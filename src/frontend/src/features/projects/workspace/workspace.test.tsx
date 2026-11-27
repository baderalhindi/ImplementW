import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test } from 'vitest';

import { type Session } from '@/features/identity-access/session/sessionApi.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';
import { translate } from '@/shared/i18n/i18n.ts';
import { type MockApi, mockApi, page, problem } from '@/test/mockApi.ts';
import {
  ENTITY_USER_ID,
  entityManagerSession,
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
import { APP_STYLES as styles } from '@/test/stylesheet.ts';
import { offeredTabs } from '@/test/workspaceTabs.ts';

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

const TAB = {
  overview: en('projects.workspace.tabs.overview'),
  registration: en('projects.workspace.tabs.registration'),
  location: en('projects.workspace.tabs.location'),
  progress: en('projects.workspace.tabs.progress'),
  schedule: en('projects.workspace.tabs.schedule'),
  tasks: en('projects.workspace.tabs.tasks'),
  milestones: en('projects.workspace.tabs.milestones'),
  financials: en('projects.workspace.tabs.financials'),
  kpis: en('projects.workspace.tabs.kpis'),
  risks: en('projects.workspace.tabs.risks'),
  issuesChallenges: en('projects.workspace.tabs.issuesChallenges'),
  changeRequests: en('projects.workspace.tabs.changeRequests'),
  suspension: en('projects.workspace.tabs.suspension'),
  closeout: en('projects.workspace.tabs.closeout'),
  externalRequests: en('projects.workspace.tabs.externalRequests'),
  reviews: en('projects.workspace.tabs.reviews'),
  documents: en('projects.workspace.tabs.documents'),
};

const button = (key: string) => screen.queryByRole('button', { name: en(key) });

/** The project as the API sends it to a caller without FINANCIAL_VIEW_SENSITIVE: the budget omitted and named in maskedFields. */
function withheldBudget(project: ProjectDetail): ProjectDetail {
  const { registrationBudgetSar: _omitted, ...rest } = project;
  return { ...rest, maskedFields: ['registrationBudgetSar'] };
}

/** The page's "More" actions (review round 8, point 06: edit and delete are there, delete last), by name; none when there is no "More". */
async function moreActions(user: ReturnType<typeof userEvent.setup>): Promise<string[]> {
  const more = screen.queryByRole('button', {
    name: (name) => name.includes(en('common.actions.more')),
  });
  if (more === null) {
    return [];
  }
  await user.click(more);
  return screen.getAllByRole('menuitem').map((item) => item.textContent);
}

describe('SCR-040 workspace tabs follow the RBAC scope', () => {
  test('an AHDA user whose department owns the project sees every tab', async () => {
    const user = userEvent.setup();
    openWorkspace(projectDetail(), reviewerSession());
    const heading = await screen.findByRole('heading', { level: 1, name: 'Coastal road upgrade' });
    // The project title is read in the language it was entered in (WCAG 3.1.2, TASK-087 A11Y-F-002).
    expect(heading.getAttribute('lang')).toBe('en');

    expect(await offeredTabs(user)).toEqual([
      TAB.overview,
      TAB.schedule,
      TAB.tasks,
      TAB.milestones,
      TAB.progress,
      TAB.financials,
      TAB.kpis,
      TAB.risks,
      TAB.issuesChallenges,
      TAB.changeRequests,
      TAB.suspension,
      TAB.closeout,
      TAB.externalRequests,
      TAB.registration,
      TAB.location,
      TAB.documents,
      TAB.reviews,
    ]);
  });

  test('the entity Project Manager sees every tab of the project it manages but review history (ADR-013; TASK-035 F-8)', async () => {
    const user = userEvent.setup();
    openWorkspace(projectDetail(), entityManagerSession());
    await screen.findByRole('heading', { level: 1 });

    expect(await offeredTabs(user)).toEqual([
      TAB.overview,
      TAB.schedule,
      TAB.tasks,
      TAB.milestones,
      TAB.progress,
      TAB.financials,
      TAB.kpis,
      TAB.risks,
      TAB.issuesChallenges,
      TAB.changeRequests,
      TAB.suspension,
      TAB.closeout,
      TAB.externalRequests,
      TAB.registration,
      TAB.location,
      TAB.documents,
    ]);
  });

  test('an entity contributor sees the tabs its R08 permissions read, and no schedule, governance request or document (the review, M-12)', async () => {
    const user = userEvent.setup();
    openWorkspace(projectDetail(), entitySession());
    await screen.findByRole('heading', { level: 1 });

    expect(await offeredTabs(user)).toEqual([
      TAB.overview,
      TAB.tasks,
      TAB.progress,
      TAB.financials,
      TAB.kpis,
      TAB.risks,
      TAB.issuesChallenges,
      TAB.externalRequests,
      TAB.registration,
      TAB.location,
    ]);
  });

  test('an AHDA user whose assignments do not reach the project sees only what the API showed them', async () => {
    const user = userEvent.setup();
    openWorkspace(projectDetail(), reviewerSession(OTHER_DEPARTMENT_ID));
    await screen.findByRole('heading', { level: 1 });

    expect(await offeredTabs(user)).toEqual([TAB.overview, TAB.registration, TAB.location]);
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

  test('SCR-042: select all chooses every review run, for Export selected', async () => {
    openWorkspace(
      projectDetail({ status: 'RETURNED' }),
      reviewerSession(),
      `/projects/${PROJECT_ID}/reviews`,
    );

    const table = await screen.findByRole('table', { name: en('projects.reviews.caption') });
    await userEvent.click(
      within(table).getByRole('checkbox', { name: en('common.table.selectAll') }),
    );
    expect(screen.getByText(en('common.table.selected', { count: 1 }))).toBeTruthy();
    expect(screen.getByRole('button', { name: en('common.table.exportSelected') })).toBeTruthy();
  });

  test('SCR-043 lists the project’s documents inside the workspace', async () => {
    const api = openWorkspace(
      projectDetail(),
      entityManagerSession(),
      `/projects/${PROJECT_ID}/documents`,
    );

    expect(
      await screen.findByRole('heading', { level: 2, name: en('projects.documents.title') }),
    ).toBeTruthy();
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
    expect(api.requestsTo('GET', /^\/documents$/)[0]?.query.get('projectId')).toBe(PROJECT_ID);
  });

  test('the overview shows an imported English title and strategic flag', async () => {
    openWorkspace(projectDetail({ titleEn: 'Coastal road', isStrategic: true }), reviewerSession());

    const title = await screen.findByText(en('projects.fields.titleEn'));
    expect(title.closest('.details__row')?.textContent).toContain('Coastal road');
    const flag = screen.getByText(en('projects.fields.isStrategic'));
    expect(flag.closest('.details__row')?.textContent).toContain(en('common.values.yes'));
  });

  test('the overview says not available when the API has neither', async () => {
    openWorkspace(projectDetail({ titleEn: null }), reviewerSession());

    const title = await screen.findByText(en('projects.fields.titleEn'));
    expect(title.closest('.details__row')?.textContent).toContain(
      en('projects.followUp.notAvailable'),
    );
    const flag = screen.getByText(en('projects.fields.isStrategic'));
    expect(flag.closest('.details__row')?.textContent).toContain(
      en('projects.followUp.notAvailable'),
    );
  });

  test('keeps the manager action apart from the name with a logical margin, so it does not run into it in Arabic', async () => {
    openWorkspace(projectDetail(), reviewerSession());

    const action = await screen.findByRole('button', {
      name: (name) => name.startsWith(en('projects.changeManager.action')),
    });
    expect(action.classList.contains('details__action')).toBe(true);
    expect(styles).toMatch(/\n\.details__action \{\s*margin-inline-start: var\(--space-2\);\s*\}/);
  });

  test('has no axe violation', async () => {
    openWorkspace(projectDetail(), reviewerSession());
    await screen.findByRole('heading', { level: 1 });
    expect(describeViolations(await accessibilityViolations())).toBe('');
  });
});

describe('SCR-040 actions follow the state and the person', () => {
  test('the creator of a draft may edit, submit and delete it; there is no status to change', async () => {
    const user = userEvent.setup();
    openWorkspace(projectDetail(), entitySession());
    await screen.findByRole('heading', { level: 1 });

    expect(button('projects.actions.submit')).not.toBeNull();
    expect(button('projects.actions.changeStatus')).toBeNull();
    expect(await moreActions(user)).toEqual([
      en('projects.actions.edit'),
      en('projects.actions.delete'),
    ]);
  });

  test('someone else’s draft cannot be deleted by them', async () => {
    const user = userEvent.setup();
    openWorkspace(projectDetail({ createdBy: REVIEWER_ID }), entitySession());
    await screen.findByRole('heading', { level: 1 });

    expect(await moreActions(user)).not.toContain(en('projects.actions.delete'));
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

describe('SCR-040 the registration budget follows FINANCIAL_VIEW_SENSITIVE (D-4)', () => {
  test('a withheld budget reads Restricted on the overview and the registration, never as none', async () => {
    openWorkspace(withheldBudget(projectDetail()), entitySession());
    const term = await screen.findByText(en('projects.fields.registrationBudget'));
    expect(term.parentElement?.querySelector('[data-value-state="RESTRICTED"]')).toBeTruthy();
  });

  test('the registration tab says Restricted too', async () => {
    const project = withheldBudget(projectDetail());
    openWorkspace(project, entitySession(), `/projects/${project.id}/registration`);
    const term = await screen.findByText(en('projects.fields.registrationBudget'));
    expect(term.parentElement?.textContent).toContain(en('financialKpi.value.restricted'));
  });

  test('a budget the caller holds is shown as the amount', async () => {
    openWorkspace(projectDetail(), entitySession());

    const term = await screen.findByText(en('projects.fields.registrationBudget'));
    expect(term.parentElement?.querySelector('[data-value-state="RESTRICTED"]')).toBeNull();
    expect(term.parentElement?.querySelector('dd span[title]')).toBeTruthy();
  });
});

describe('MOD-001 Delete Draft', () => {
  test('is confirmed, sends the ETag, and returns to the register', async () => {
    const user = userEvent.setup();
    const api = openWorkspace(projectDetail(), entitySession());
    api
      .on('DELETE', /^\/projects\//, { status: 204 })
      .on('GET', /^\/projects$/, { body: page([]) });

    await screen.findByRole('heading', { level: 1 });
    await moreActions(user);
    await user.click(screen.getByRole('menuitem', { name: en('projects.actions.delete') }));
    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog).getByText(en('projects.delete.body', { title: 'Coastal road upgrade' })),
    ).toBeTruthy();
    // The client's delete (13 Nov 2026) is confirmed by typing the project's number; a draft has none, so its title.
    await user.type(within(dialog).getByRole('textbox'), 'Coastal road upgrade');
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

    await screen.findByRole('heading', { level: 1 });
    await moreActions(user);
    await user.click(screen.getByRole('menuitem', { name: en('projects.actions.delete') }));
    const dialog = await screen.findByRole('dialog');
    // The client's delete (13 Nov 2026) is confirmed by typing the project's number; a draft has none, so its title.
    await user.type(within(dialog).getByRole('textbox'), 'Coastal road upgrade');
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

  test('a budget withheld from the caller is not taken as missing (D-4)', async () => {
    const user = userEvent.setup();
    openWorkspace(withheldBudget(projectDetail()), entitySession());

    await user.click(await screen.findByRole('button', { name: en('projects.actions.submit') }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).queryByText(en('projects.fields.registrationBudget'))).toBeNull();
    expect(
      within(dialog).getByRole('button', {
        name: en('projects.submit.confirm'),
      }),
    ).toHaveProperty('disabled', false);
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

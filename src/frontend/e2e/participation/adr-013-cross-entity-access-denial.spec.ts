import { type PlatformApi } from '../support/api.ts';
import { expect, expectText, test, unique, utcDate } from '../support/journey.ts';
import { type ProjectTask, startedTask } from '../support/participation.ts';
import { activeProject, project } from '../support/projects.ts';
import { itemId } from '../support/reference.ts';

// ADR-013 participation amendment (TASK-085): "add denied cross-entity access tests".
// ADR-013 (docs/architecture/adrs/ADR-register.md): an entity's people reach their own entity's projects only; the entity
// Project Manager's authority is scoped to the project they manage; contributors who do not manage it stay request-driven.
// The engine refuses an external user any subject anchored on another entity, and a single record out of scope answers 404
// NOT_FOUND exactly as a nonexistent one (authorization-engine.md D-3, api-conventions.md R-47; external-participation.md
// D-7). Integration evidence: ExternalParticipation/EntityIsolationTests.cs, Project/ProjectAuthorizationTests.cs,
// Identity/AuthorizationEndpointTests.cs.

interface Page<T> {
  items: T[];
  totalCount?: number;
}

interface Identified {
  id: string;
}

test('ADR-013 denied cross-entity access: entity B reaches nothing of entity A’s project, and A’s contributor no PM-only write', async ({
  actor,
}) => {
  const entityPm = await actor('entityProjectManager');
  const contributor = await actor('entityContributor');
  const outsider = await actor('otherEntityProjectManager');
  const department = await actor('departmentManager');
  const portfolio = await actor('portfolioManager');
  const title = unique('ADR-013 Entity A quay');

  // Entity A's project, managed by its own Project Manager, with a record in each of the modules they read.
  const subject = await activeProject(
    {
      creator: entityPm.api,
      reviewer: department.api,
      approver: portfolio.api,
      manager: entityPm.api,
    },
    { title, participationMode: 'ENTITY_MANAGED' },
  );
  const projectId = subject.id;
  const task: ProjectTask = await startedTask(entityPm.api, projectId, 'Quay wall piling');
  const risk = await entityPm.api.post<Identified>('/risks', {
    projectId,
    title: { text: 'Tidal flooding of the works', language: 'en' },
    description: { text: 'Spring tides may flood the excavation.', language: 'en' },
    riskCategoryItemId: await itemId(portfolio.api, 'RISK_CATEGORY', 'E2E_WEATHER'),
    ownerUserId: entityPm.api.userId,
    identifiedDate: utcDate(0),
    nextReviewDate: null,
  });
  const concern = await entityPm.api.post<Identified>('/management-concerns', {
    projectId,
    concernType: 'ISSUE',
    title: { text: 'Crane permit pending', language: 'en' },
    description: { text: 'The port has not issued the crane permit.', language: 'en' },
    categoryItemId: await itemId(portfolio.api, 'CONCERN_CATEGORY', 'E2E_SITE_ACCESS'),
    priorityItemId: await itemId(portfolio.api, 'PRIORITY', 'E2E_HIGH'),
    impacts: [],
    targetResolutionDate: null,
  });
  // AHDA's update request to entity A on the project (the Department Manager manages WF-13 at DEPT).
  const request = await department.api.post<Identified>('/external-update-requests', {
    projectId,
    externalEntityId: subject.externalEntityId,
    contributionTypeItemId: await itemId(portfolio.api, 'CONTRIBUTION_TYPE', 'PROJECT_INFORMATION'),
    targetId: null,
    instructions: { text: 'Describe the state of the quay works.', language: 'en' },
    responsibleUserId: contributor.api.userId,
    reviewerUserId: department.api.userId,
    dueDate: utcDate(14),
  });
  await department.api.post(`/external-update-requests/${request.id}/issue`);

  // What entity A's Project Manager reads of the project; each must be out of entity B's reach.
  const records = [
    `/projects/${projectId}`,
    `/project-tasks/${task.id}`,
    `/risks/${risk.id}`,
    `/management-concerns/${concern.id}`,
    `/external-update-requests/${request.id}`,
  ];
  const collections = [
    `/project-schedules?projectId=${projectId}`,
    `/schedule-activities?projectId=${projectId}`,
    `/project-baselines?projectId=${projectId}`,
    `/project-tasks?projectId=${projectId}`,
    `/risks?projectId=${projectId}`,
    `/management-concerns?projectId=${projectId}`,
    `/external-update-requests?projectId=${projectId}`,
  ];

  await test.step('control: entity A’s Project Manager reads every one of them', async () => {
    for (const path of records) {
      const read = await entityPm.api.send('GET', path);
      expect([path, read.status]).toEqual([path, 200]);
    }
    for (const path of collections) {
      const page = await entityPm.api.get<Page<unknown>>(path);
      expect([path, page.items.length > 0]).toEqual([path, true]);
    }
  });

  await test.step('entity B’s Project Manager does not find the project in the register', async () => {
    const page = outsider.page;
    await page.goto('/projects');
    await page.getByRole('searchbox', { name: 'Title or Formal Project ID' }).fill(title);
    await page.getByRole('button', { name: 'Search' }).click();
    await expect(page.getByRole('main')).not.toContainText(title);
    await expect(page.locator(`a[href="/projects/${projectId}"]`)).toHaveCount(0);
    const listed = await outsider.api.get<Page<Identified>>(
      `/projects?q=${encodeURIComponent(title)}`,
    );
    expect(listed.items.map((p) => p.id)).not.toContain(projectId);
  });

  await test.step('the direct links show not-found, as for a project that does not exist', async () => {
    const page = outsider.page;
    for (const tab of ['', '/schedule', '/tasks', '/risks', '/external-requests']) {
      await page.goto(`/projects/${projectId}${tab}`);
      await expectText(
        page.getByRole('main'),
        'The project was not found, or you do not have access to it.',
      );
      await expect(page.getByRole('main')).not.toContainText(title);
    }
    await page.goto(`/external-requests/${request.id}`);
    await expectText(
      page.getByRole('main'),
      'This update request was not found, or you do not have access to it.',
    );
  });

  await test.step('the API answers 404 for the project and each of its records, and lists nothing of it', async () => {
    for (const path of records) {
      const read = await outsider.api.send('GET', path);
      expect([path, read.status, read.code]).toEqual([path, 404, 'NOT_FOUND']);
    }
    for (const path of collections) {
      const read = await outsider.api.send<Page<unknown>>('GET', path);
      expect([path, read.status, read.body.items.length]).toEqual([path, 200, 0]);
    }
  });

  const counts = async () => ({
    status: (await project(portfolio.api, projectId)).status,
    revisionNo: (await project(portfolio.api, projectId)).revisionNo,
    tasks: await count(portfolio.api, `/project-tasks?projectId=${projectId}`),
    activities: await count(portfolio.api, `/schedule-activities?projectId=${projectId}`),
    risks: await count(portfolio.api, `/risks?projectId=${projectId}`),
    concerns: await count(portfolio.api, `/management-concerns?projectId=${projectId}`),
    task: (await portfolio.api.get<ProjectTask>(`/project-tasks/${task.id}`)).actualPercentComplete,
  });
  const before = await counts();

  await test.step('entity B’s writes against the project are refused without effect', async () => {
    const writes: [string, unknown][] = [
      [`/projects/${projectId}/withdraw`, {}],
      [
        '/project-tasks',
        {
          projectId,
          parentTaskId: null,
          scheduleActivityId: null,
          title: { text: 'Planted task', language: 'en' },
          description: null,
          assigneeUserId: null,
          priorityItemId: null,
          plannedStartDate: utcDate(0),
          plannedFinishDate: utcDate(3),
        },
      ],
      [`/project-tasks/${task.id}/report-progress`, { actualPercentComplete: 90 }],
      [
        '/schedule-activities',
        {
          projectId,
          parentActivityId: null,
          wbsCode: '9',
          name: { text: 'Planted activity', language: 'en' },
          requestedStartDate: utcDate(7),
          plannedDurationDays: 5,
          sortOrder: 9,
        },
      ],
      [
        '/risks',
        {
          projectId,
          title: { text: 'Planted risk', language: 'en' },
          description: { text: 'Planted.', language: 'en' },
          riskCategoryItemId: await itemId(portfolio.api, 'RISK_CATEGORY', 'E2E_WEATHER'),
          ownerUserId: null,
          identifiedDate: utcDate(0),
          nextReviewDate: null,
        },
      ],
      [
        '/external-contributions',
        {
          externalUpdateRequestId: request.id,
          fields: [{ fieldCode: 'response', value: 'Planted answer.', language: 'en' }],
        },
      ],
    ];
    for (const [path, body] of writes) {
      const refused = await outsider.api.send('POST', path, body);
      expect([path, refused.status, refused.code]).toEqual([path, 404, 'NOT_FOUND']);
    }
    expect(await counts()).toEqual(before);
  });

  await test.step('entity A’s contributor reads the project but is refused the Project Manager’s writes', async () => {
    expect((await contributor.api.send('GET', `/projects/${projectId}`)).status).toBe(200);
    const page = contributor.page;
    await page.goto(`/projects/${projectId}/tasks`);
    await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Add task' })).toHaveCount(0);
    await page.goto(`/projects/${projectId}/schedule`);
    await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Add activity' })).toHaveCount(0);

    const writes: [string, unknown][] = [
      [
        '/project-tasks',
        {
          projectId,
          parentTaskId: null,
          scheduleActivityId: null,
          title: { text: 'Contributor task', language: 'en' },
          description: null,
          assigneeUserId: null,
          priorityItemId: null,
          plannedStartDate: utcDate(0),
          plannedFinishDate: utcDate(3),
        },
      ],
      [
        '/schedule-activities',
        {
          projectId,
          parentActivityId: null,
          wbsCode: '8',
          name: { text: 'Contributor activity', language: 'en' },
          requestedStartDate: utcDate(7),
          plannedDurationDays: 5,
          sortOrder: 8,
        },
      ],
      [
        '/risks',
        {
          projectId,
          title: { text: 'Contributor risk', language: 'en' },
          description: { text: 'Contributor.', language: 'en' },
          riskCategoryItemId: await itemId(portfolio.api, 'RISK_CATEGORY', 'E2E_WEATHER'),
          ownerUserId: null,
          identifiedDate: utcDate(0),
          nextReviewDate: null,
        },
      ],
      ['/project-baselines', { projectId }],
    ];
    for (const [path, body] of writes) {
      const refused = await contributor.api.send('POST', path, body);
      expect([path, refused.status, refused.code]).toEqual([path, 403, 'PERMISSION_DENIED']);
    }
    // A task the contributor does not own is not theirs to see (R08 TASK_VIEW/TASK_UPDATE at ASSIGNED).
    const progress = await contributor.api.send(
      'POST',
      `/project-tasks/${task.id}/report-progress`,
      { actualPercentComplete: 90 },
    );
    expect([progress.status, progress.code]).toEqual([404, 'NOT_FOUND']);
    expect(await counts()).toEqual(before);
  });
});

async function count(api: PlatformApi, path: string): Promise<number> {
  return (await api.get<Page<unknown>>(`${path}&pageSize=100`)).items.length;
}

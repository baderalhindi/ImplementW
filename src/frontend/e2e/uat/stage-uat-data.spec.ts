import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { expect, test } from '@playwright/test';

import { uploadDocument, untilScanned } from '../support/access-audit.ts';
import { PlatformApi } from '../support/api.ts';
import { type Persona } from '../support/environment.ts';
import { scheduleWithOneActivity } from '../support/execution.ts';
import { utcDate } from '../support/journey.ts';
import {
  activeBaseline,
  approveTaskOn,
  registration,
  untilStatus,
  type ProjectCrew,
  type ProjectDetail,
  type ProjectStatus,
} from '../support/projects.ts';
import { DATA_SET, itemId } from '../support/reference.ts';

// The UAT data set (TASK-088, docs/testing/uat/uat-execution.md §5). Each fixture of data-set.json is one test, staged through
// the product's API by the people who own each step — the same WF-01/WF-03 path the journeys take — so every record is in a
// state the platform itself produced. Each test asserts its fixture reached its state and writes what testers need (ids,
// titles, states) to test-results/uat/fixtures/<id>.json; uat/run.sh stages a fresh stack.

interface Text {
  text: string;
  language: 'ar' | 'en';
}

interface Fixture {
  id: string;
  kind: 'draft' | 'submitted' | 'underReview' | 'approvedPlanned' | 'active';
  title: Text;
  budgetSar: string;
  startsInDays: number;
  department?: 'otherDepartment';
  entity?: 'entityA' | 'entityB';
  records?: boolean;
  purpose: string;
}

const HERE = dirname(fileURLToPath(import.meta.url));
const FIXTURES = (
  JSON.parse(readFileSync(join(HERE, 'data-set.json'), 'utf8')) as { fixtures: Fixture[] }
).fixtures;
const OUTPUT = join(HERE, '../../test-results/uat/fixtures');

const EXPECTED_STATUS: Record<Fixture['kind'], ProjectStatus> = {
  draft: 'DRAFT',
  submitted: 'SUBMITTED',
  underReview: 'UNDER_REVIEW',
  approvedPlanned: 'APPROVED_PLANNED',
  active: 'ACTIVE',
};

/** Who creates, reviews, approves and manages a fixture's project: AHDA's people, or an entity's own Project Manager. */
function crewOf(fixture: Fixture): Record<keyof ProjectCrew, Persona> {
  if (fixture.entity === 'entityA')
    return {
      creator: 'entityProjectManager',
      reviewer: 'departmentManager',
      approver: 'portfolioManager',
      manager: 'entityProjectManager',
    };
  if (fixture.entity === 'entityB')
    return {
      creator: 'otherEntityProjectManager',
      reviewer: 'departmentManager',
      approver: 'portfolioManager',
      manager: 'otherEntityProjectManager',
    };
  const department =
    fixture.department === 'otherDepartment' ? 'otherDepartmentManager' : 'departmentManager';
  return {
    creator: department,
    reviewer: department,
    approver: 'portfolioManager',
    manager: 'projectManager',
  };
}

async function signedIn(crew: Record<keyof ProjectCrew, Persona>): Promise<ProjectCrew> {
  const sessions = new Map<Persona, PlatformApi>();
  for (const persona of new Set(Object.values(crew)))
    sessions.set(persona, await PlatformApi.signIn(persona));
  const of = (persona: Persona): PlatformApi => {
    const session = sessions.get(persona);
    if (session === undefined) throw new Error(`no session for ${persona}`);
    return session;
  };
  return {
    creator: of(crew.creator),
    reviewer: of(crew.reviewer),
    approver: of(crew.approver),
    manager: of(crew.manager),
  };
}

/** The registration body with the fixture's own title, description, budget and dates. */
async function body(crew: ProjectCrew, fixture: Fixture): Promise<Record<string, unknown>> {
  const registered = await registration(crew.approver, {
    participationMode: fixture.entity === undefined ? 'AHDA_MANAGED' : 'ENTITY_MANAGED',
    entityCode: fixture.entity === 'entityB' ? DATA_SET.entityB : DATA_SET.entityA,
    departmentCode:
      fixture.department === 'otherDepartment' ? DATA_SET.otherDepartment : DATA_SET.department,
    startsInDays: fixture.startsInDays,
    budgetSar: fixture.budgetSar,
  });
  return {
    ...registered,
    title: fixture.title,
    description: { text: `${fixture.id}: ${fixture.purpose}.`, language: 'en' },
  };
}

/** WF-01 (and WF-03 for the planned and active kinds) step by step, each by the person who owns the step, up to its state. */
async function staged(crew: ProjectCrew, fixture: Fixture): Promise<ProjectDetail> {
  const draft = await crew.creator.post<ProjectDetail>('/projects', await body(crew, fixture));
  if (fixture.kind === 'draft') return draft;
  await crew.creator.post(`/projects/${draft.id}/submit`, {
    projectManagerUserId: crew.manager.userId,
  });
  if (fixture.kind === 'submitted') return draft;
  await crew.reviewer.post(`/projects/${draft.id}/start-review`);
  if (fixture.kind === 'underReview') return draft;
  await approveTaskOn(crew.approver, draft.id, 'PROJECT_REGISTRATION');
  await untilStatus(crew.approver, draft.id, 'APPROVED_PLANNED');
  if (fixture.kind === 'approvedPlanned') {
    await scheduleWithOneActivity(crew.manager, draft.id);
    return draft;
  }
  await activeBaseline(crew.manager, crew.approver, draft.id);
  await crew.approver.post(`/projects/${draft.id}/activate`);
  return draft;
}

/** An ACTIVE project's working records, created by its own Project Manager: what WF-04 to WF-08 and WF-12 scenarios open. */
async function records(
  crew: ProjectCrew,
  projectId: string,
  withDocuments: boolean,
): Promise<Record<string, string[]>> {
  const pm = crew.manager;
  const tasks: string[] = [];
  for (const [title, start] of [
    ['Site survey and utilities mapping', true],
    ['Procure long-lead equipment', true],
    ['Prepare the handover plan', false],
  ] as const) {
    const task = await pm.post<{ id: string }>('/project-tasks', {
      projectId,
      title: { text: title, language: 'en' },
      assigneeUserId: pm.userId,
      plannedStartDate: utcDate(0),
      plannedFinishDate: utcDate(21),
    });
    if (start) await pm.post(`/project-tasks/${task.id}/start`);
    tasks.push(task.id);
  }
  // Category and priority ids are read by an AHDA session: shipped entity roles cannot read master data (UAT-D-004).
  const risks: string[] = [];
  for (const [title, description, category] of [
    [
      'Late delivery of long-lead equipment',
      'The supplier may deliver the switchgear after the planned date.',
      'E2E_COMMERCIAL',
    ],
    [
      'Summer heat limits working hours',
      'Site work may be restricted at midday from June to August.',
      'E2E_WEATHER',
    ],
  ] as const) {
    const risk = await pm.post<{ id: string }>('/risks', {
      projectId,
      title: { text: title, language: 'en' },
      description: { text: description, language: 'en' },
      riskCategoryItemId: await itemId(crew.approver, 'RISK_CATEGORY', category),
      ownerUserId: pm.userId,
      identifiedDate: utcDate(0),
      nextReviewDate: utcDate(14),
    });
    risks.push(risk.id);
  }
  const issue = await pm.post<{ id: string }>('/management-concerns', {
    projectId,
    concernType: 'ISSUE',
    title: { text: 'Utility crossing permit outstanding', language: 'en' },
    description: {
      text: 'The utility owner has not yet issued the crossing permit.',
      language: 'en',
    },
    categoryItemId: await itemId(crew.approver, 'CONCERN_CATEGORY', 'E2E_SITE_ACCESS'),
    priorityItemId: await itemId(crew.approver, 'PRIORITY', 'E2E_HIGH'),
    impacts: [],
    targetResolutionDate: utcDate(30),
  });
  const result: Record<string, string[]> = { tasks, risks, issues: [issue.id] };
  if (!withDocuments) return result;
  const change = await pm.post<{ id: string }>('/change-requests', {
    projectId,
    changeType: 'SCHEDULE',
    title: { text: 'Ten more days for the utility crossing', language: 'en' },
    justification: { text: 'The crossing permit is outstanding.', language: 'en' },
    costImpactSar: null,
    scheduleImpactDays: 10,
    scopeImpact: null,
    isContractualObligation: false,
    requestedGovernanceProfileItemId: null,
  });
  result.changeRequests = [change.id];
  // TD-03: one CLEAN version, one QUARANTINED and one held pending (the scanner stand-in decides by file name; AHDA's scanner
  // with an EICAR file and a held version on UAT).
  const documents: string[] = [];
  for (const [title, fileName] of [
    ['Site survey report', 'site-survey-report.pdf'],
    ['Contractor submission (infected sample)', 'contractor-submission-infected.pdf'],
    ['Design drawings awaiting scan', 'design-drawings-held.pdf'],
  ] as const) {
    const document = await uploadDocument(crew.reviewer, {
      projectId,
      title,
      fileName,
      content: `%PDF-1.4\nTASK-088 UAT data set: ${title}\n%%EOF`,
    });
    documents.push(document.id);
  }
  const scanned = await untilScanned(crew.reviewer, documents[0] ?? '');
  expect(scanned.latestVersion?.scanState).toBe('CLEAN');
  result.documents = documents;
  return result;
}

for (const fixture of FIXTURES) {
  test(`${fixture.id} ${fixture.kind}: ${fixture.purpose}`, async () => {
    test.setTimeout(5 * 60 * 1000);
    const crew = await signedIn(crewOf(fixture));
    try {
      const project = await staged(crew, fixture);
      const read = await untilStatus(crew.approver, project.id, EXPECTED_STATUS[fixture.kind]);
      expect(read.title.text).toBe(fixture.title.text);
      const made =
        fixture.records === true
          ? await records(crew, project.id, fixture.entity === undefined)
          : {};
      mkdirSync(OUTPUT, { recursive: true });
      writeFileSync(
        join(OUTPUT, `${fixture.id}.json`),
        JSON.stringify(
          {
            id: fixture.id,
            title: fixture.title,
            purpose: fixture.purpose,
            projectId: read.id,
            formalProjectId: read.formalProjectId,
            status: read.status,
            projectManagerUserId: read.projectManagerUserId,
            externalEntityId: read.externalEntityId,
            records: made,
          },
          null,
          2,
        ),
      );
    } finally {
      await Promise.all(
        [...new Set([crew.creator, crew.reviewer, crew.approver, crew.manager])].map((session) =>
          session.dispose(),
        ),
      );
    }
  });
}

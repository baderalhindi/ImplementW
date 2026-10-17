import { eventually, type PlatformApi } from './api.ts';
import { unique, utcDate } from './journey.ts';
import { DATA_SET, departmentId, entityId, itemId } from './reference.ts';

// WF-01 and WF-03 through the API, as the people who own each step: what every other journey needs before it starts — a
// project of its own, registered, approved by AHDA, baselined and activated by the command (ADR-009). UJ-001 drives the same
// steps through the browser; the others take them from here.

export type ProjectStatus =
  | 'DRAFT'
  | 'SUBMITTED'
  | 'UNDER_REVIEW'
  | 'RETURNED'
  | 'APPROVED_PLANNED'
  | 'ACTIVE'
  | 'SUSPENDED'
  | 'COMPLETED'
  | 'CLOSED';

export interface ProjectDetail {
  id: string;
  formalProjectId: string | null;
  title: { text: string; language: string };
  departmentId: string;
  externalEntityId: string | null;
  projectManagerUserId: string | null;
  status: ProjectStatus;
  revisionNo: number;
  activatedAt: string | null;
}

interface ApprovalInboxItem {
  taskId: string;
  instance: {
    id: string;
    subject: { module: string; type: string; id: string };
    routingKey: string;
  };
}

export interface RegistrationOptions {
  title?: string;
  /** AHDA_MANAGED (default) or ENTITY_MANAGED, which names entity A unless another is given. */
  participationMode?: 'AHDA_MANAGED' | 'ENTITY_MANAGED';
  entityCode?: string;
  departmentCode?: string;
  startsInDays?: number;
  budgetSar?: string;
}

/** A complete registration body; reference ids are read with `lookup`, a session that may read master data and organisations. */
export async function registration(
  lookup: PlatformApi,
  options: RegistrationOptions = {},
): Promise<Record<string, unknown>> {
  const entityManaged = options.participationMode === 'ENTITY_MANAGED';
  const start = options.startsInDays ?? 7;
  return {
    title: { text: options.title ?? unique('E2E project'), language: 'en' },
    description: { text: 'Created by the cross-domain journey suite (TASK-085).', language: 'en' },
    classificationItemId: await itemId(lookup, 'PROJECT_CLASSIFICATION', DATA_SET.classification),
    departmentId: await departmentId(lookup, options.departmentCode ?? DATA_SET.department),
    externalEntityId: entityManaged
      ? await entityId(lookup, options.entityCode ?? DATA_SET.entityA)
      : null,
    participationMode: options.participationMode ?? 'AHDA_MANAGED',
    governanceProfileItemId: await itemId(lookup, 'GOVERNANCE_PROFILE', DATA_SET.governanceProfile),
    registrationBudgetSar: options.budgetSar ?? '12500000.00',
    plannedStartDate: utcDate(start),
    plannedEndDate: utcDate(start + 365),
    regionItemId: await itemId(lookup, 'REGION', DATA_SET.region),
    cityItemId: await itemId(lookup, 'CITY', DATA_SET.city),
    latitude: null,
    longitude: null,
  };
}

export function project(api: PlatformApi, projectId: string): Promise<ProjectDetail> {
  return api.get<ProjectDetail>(`/projects/${projectId}`);
}

export function untilStatus(
  api: PlatformApi,
  projectId: string,
  status: ProjectStatus,
): Promise<ProjectDetail> {
  return eventually(
    () => project(api, projectId),
    (p) => p.status === status,
    `project ${projectId} to be ${status}`,
  );
}

/**
 * Approves the open task of the run on `subjectId` that `approver` may decide, once WF-11 has assigned it, and returns the
 * run's id. The outcome reaches the source through the outbox; callers wait for the source's own state.
 */
export async function approveTaskOn(
  approver: PlatformApi,
  subjectId: string,
  routingKey?: string,
): Promise<string> {
  const item = await eventually(
    async () =>
      (
        await approver.get<{ items: ApprovalInboxItem[] }>('/approval-tasks?pageSize=100')
      ).items.find(
        (i) =>
          i.instance.subject.id === subjectId &&
          (routingKey === undefined || i.instance.routingKey === routingKey),
      ),
    (found) => found !== undefined,
    `an approval task on ${subjectId} for ${approver.persona}`,
  );
  if (item === undefined) throw new Error('unreachable');
  await approver.post(`/approval-tasks/${item.taskId}/approve`, { reason: null });
  return item.instance.id;
}

/** ADR-009's activation precondition through WF-03: a one-activity schedule, its baseline submitted and approved, ACTIVE. */
export async function activeBaseline(
  manager: PlatformApi,
  approver: PlatformApi,
  projectId: string,
): Promise<string> {
  await manager.post('/project-schedules', { projectId });
  await manager.post('/schedule-activities', {
    projectId,
    parentActivityId: null,
    wbsCode: '1',
    name: { text: 'Works', language: 'en' },
    requestedStartDate: utcDate(7),
    plannedDurationDays: 60,
    sortOrder: 0,
  });
  const baseline = await manager.post<{ id: string }>('/project-baselines', { projectId });
  await manager.post(`/project-baselines/${baseline.id}/submit`, { changeAuthorizationId: null });
  await approveTaskOn(approver, baseline.id, 'SCHEDULE_BASELINE');
  await eventually(
    () => manager.get<{ status: string }>(`/project-baselines/${baseline.id}`),
    (b) => b.status === 'ACTIVE',
    `baseline ${baseline.id} to be ACTIVE`,
  );
  return baseline.id;
}

export interface ProjectCrew {
  /** Creates and submits the draft: the department manager for an AHDA project, the entity's own PM for an entity one. */
  creator: PlatformApi;
  /** Starts AHDA's review (PROJECT_REVIEW); never the approver, since a requester cannot decide their own run. */
  reviewer: PlatformApi;
  /** Decides the review and the baseline and activates (E2E-R02). */
  approver: PlatformApi;
  /** The Project Manager the submission names; owns the schedule. */
  manager: PlatformApi;
}

/** A project of the journey's own, ACTIVE, as WF-01 and WF-03 make one. */
export async function activeProject(
  crew: ProjectCrew,
  options: RegistrationOptions = {},
): Promise<ProjectDetail> {
  const draft = await crew.creator.post<ProjectDetail>(
    '/projects',
    await registration(crew.approver, options),
  );
  await crew.creator.post(`/projects/${draft.id}/submit`, {
    projectManagerUserId: crew.manager.userId,
  });
  await crew.reviewer.post(`/projects/${draft.id}/start-review`);
  await approveTaskOn(crew.approver, draft.id, 'PROJECT_REGISTRATION');
  await untilStatus(crew.approver, draft.id, 'APPROVED_PLANNED');
  await activeBaseline(crew.manager, crew.approver, draft.id);
  await crew.approver.post(`/projects/${draft.id}/activate`);
  return untilStatus(crew.approver, draft.id, 'ACTIVE');
}

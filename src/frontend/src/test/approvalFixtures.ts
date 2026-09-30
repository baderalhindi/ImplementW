import {
  type ApprovalDelegationDetail,
  type ApprovalInboxItem,
  type ApprovalInstanceDetail,
  type ApprovalTaskDetail,
} from '@/features/approvals/api/types.ts';
import { type RoleSummary } from '@/features/identity-access/api/types.ts';
import { type Session } from '@/features/identity-access/session/sessionApi.ts';

import { sessionFor } from './identityAccessFixtures.ts';
import { type MockApi, problem } from './mockApi.ts';

// Test data only: invented names and fixed ids. Each id starts differently, so a shortened id is recognisable.

export const APPROVER_ID = 'a1a1a1a1-0000-4000-8000-000000000002';
export const REQUESTER_ID = 'b2b2b2b2-0000-4000-8000-000000000003';
export const DELEGATOR_ID = 'c3c3c3c3-0000-4000-8000-000000000004';
export const ROLE_ID = 'd4d4d4d4-0000-4000-8000-0000000000b2';
export const ESCALATION_ROLE_ID = 'e5e5e5e5-0000-4000-8000-0000000000b9';
export const RUN_ID = 'f6f6f6f6-0000-4000-8000-000000000a01';
export const TASK_ID = '17171717-0000-4000-8000-000000000b01';

const PEOPLE: Record<string, string> = {
  [APPROVER_ID]: 'Noura Approver',
  [REQUESTER_ID]: 'Faisal Requester',
  [DELEGATOR_ID]: 'Huda Delegator',
};

const approvalRoles: RoleSummary[] = [
  {
    id: ROLE_ID,
    code: 'R02',
    name: { ar: 'مدير المحفظة', en: 'Portfolio Manager' },
    isSystem: true,
    isExternalEligible: false,
  },
  {
    id: ESCALATION_ROLE_ID,
    code: 'R09',
    name: { ar: 'المدير التنفيذي', en: 'Executive Director' },
    isSystem: true,
    isExternalEligible: false,
  },
];

export function sessionAs(userId: string, roleCodes: string[] = ['R02']): Session {
  const session = sessionFor(roleCodes);
  const displayName = PEOPLE[userId] ?? 'Test User';
  return {
    ...session,
    user: { ...session.user, id: userId, displayName, username: displayName.toLowerCase() },
  };
}

export const subject = {
  module: 'ChangeRequest',
  type: 'CHANGE_REQUEST',
  id: '28282828-0000-4000-8000-000000000c01',
  revisionNo: 2,
};

export function task(overrides: Partial<ApprovalTaskDetail> = {}): ApprovalTaskDetail {
  return {
    id: TASK_ID,
    sequenceNo: 1,
    assignedRoleId: ROLE_ID,
    assignedUserId: null,
    actingUserId: null,
    approvalDelegationId: null,
    status: 'PENDING',
    dueAt: '2099-01-10T09:00:00Z',
    decidedAt: null,
    decisionReason: null,
    escalatedToTaskId: null,
    createdAt: '2026-09-28T09:00:00Z',
    ...overrides,
  };
}

export function approvalRun(
  overrides: Partial<ApprovalInstanceDetail> = {},
): ApprovalInstanceDetail {
  return {
    id: RUN_ID,
    subject,
    routingKey: 'CHANGE_REQUEST',
    authorityConfigurationVersionId: '39393939-0000-4000-8000-000000000d01',
    scopeProjectId: null,
    scopeDepartmentId: null,
    requestedByUserId: REQUESTER_ID,
    requestedAt: '2026-09-28T09:00:00Z',
    status: 'PENDING',
    completedAt: null,
    outcomeDeliveredAt: null,
    previousInstanceId: null,
    tasks: [task()],
    ...overrides,
  };
}

export function inboxItem(overrides: Partial<ApprovalInboxItem> = {}): ApprovalInboxItem {
  const { tasks: _tasks, ...instance } = approvalRun();
  return {
    taskId: TASK_ID,
    sequenceNo: 1,
    assignedRoleId: ROLE_ID,
    dueAt: '2099-01-10T09:00:00Z',
    onBehalfOfUserId: null,
    instance: {
      id: instance.id,
      subject: instance.subject,
      routingKey: instance.routingKey,
      requestedByUserId: instance.requestedByUserId,
      requestedAt: instance.requestedAt,
      status: instance.status,
      completedAt: instance.completedAt,
    },
    ...overrides,
  };
}

export function delegation(
  overrides: Partial<ApprovalDelegationDetail> = {},
): ApprovalDelegationDetail {
  return {
    id: '4a4a4a4a-0000-4000-8000-000000000e01',
    delegatorUserId: APPROVER_ID,
    delegateUserId: REQUESTER_ID,
    routingKey: null,
    validFrom: '2026-09-28T09:00:00Z',
    validTo: '2099-01-01T00:00:00Z',
    status: 'ACTIVE',
    revokedAt: null,
    ...overrides,
  };
}

/**
 * The FG-03 reads the WF-11 screens make for names. `namesReadable: false` is an approver without USER_VIEW or
 * ROLE_VIEW, who gets 403 for both (TASK-036 F-1).
 */
export function withApprovalNames(api: MockApi, { namesReadable = true } = {}): MockApi {
  if (!namesReadable) {
    return api
      .on('GET', /^\/roles$/, problem(403, 'PERMISSION_DENIED'))
      .on('GET', /^\/users\/[^/]+$/, problem(403, 'PERMISSION_DENIED'));
  }
  return api
    .on('GET', /^\/roles$/, { body: approvalRoles })
    .on('GET', /^\/users\/[^/]+$/, (request) => {
      const id = request.path.split('/').at(-1) ?? '';
      const displayName = PEOPLE[id];
      return displayName === undefined
        ? problem(404, 'NOT_FOUND')
        : { body: { id, displayName, username: displayName.toLowerCase(), userType: 'INTERNAL' } };
    });
}

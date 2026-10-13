import { describe, expect, test } from 'vitest';

import { projectAccess, visibleTabs } from '@/features/projects/access.ts';
import {
  AUDIT_REVIEWER_ACCESS,
  INTEGRATION_REVIEWER_ACCESS,
  NO_AUDIT_ACCESS,
  PROJECT_MANAGER_ACCESS,
  SYSTEM_ADMINISTRATOR_ACCESS,
} from '@/test/auditActivityFixtures.ts';
import { projectDetail, reviewerSession } from '@/test/projectFixtures.ts';

import {
  auditNavigation,
  canReadAuditTrail,
  canReadIntegrationAudit,
  canReadProjectAudit,
  canReadSystemEvents,
  projectAuditCategories,
} from './access.ts';
import {
  type AuditFilterValues,
  endOfDay,
  filterProblems,
  startOfDay,
  toActivityQuery,
  toAuditQuery,
} from './filters.ts';
import { ACTIVITY_KINDS, sourceRecordPath } from './presentation.ts';

// TASK-074's rules: which FG-06 screen opens for which permissions, and what each sends.

const PROJECT = '4d4d4d4d-0000-4000-8000-000000000104';
const ID = '9c000000-0000-4000-8000-000000000501';

const blank: AuditFilterValues = {
  category: '',
  eventClass: '',
  outcome: '',
  from: '',
  to: '',
  eventType: '',
  projectId: '',
  actorUserId: '',
  correlationId: '',
};

describe('who reaches which FG-06 screen (access.ts)', () => {
  test('SCR-058 needs a project audit category; R01 as shipped holds administration audit only and does not reach it', () => {
    expect(canReadProjectAudit(SYSTEM_ADMINISTRATOR_ACCESS)).toBe(false);
    expect(projectAuditCategories(SYSTEM_ADMINISTRATOR_ACCESS)).toEqual([]);
    expect(canReadProjectAudit(AUDIT_REVIEWER_ACCESS)).toBe(true);
    expect(projectAuditCategories(AUDIT_REVIEWER_ACCESS)).toEqual(['FINANCIAL', 'BUSINESS']);
    expect(canReadProjectAudit(INTEGRATION_REVIEWER_ACCESS)).toBe(false);
    expect(canReadProjectAudit(PROJECT_MANAGER_ACCESS)).toBe(false);
    expect(canReadProjectAudit(null)).toBe(false);
  });

  test('ADM-050 opens on any category, ADM-053 on integration audit, ADM-052 on the operations permission', () => {
    expect(canReadAuditTrail(SYSTEM_ADMINISTRATOR_ACCESS)).toBe(true);
    expect(canReadAuditTrail(PROJECT_MANAGER_ACCESS)).toBe(false);
    expect(canReadIntegrationAudit(SYSTEM_ADMINISTRATOR_ACCESS)).toBe(false);
    expect(canReadIntegrationAudit(INTEGRATION_REVIEWER_ACCESS)).toBe(true);
    expect(canReadSystemEvents(SYSTEM_ADMINISTRATOR_ACCESS)).toBe(true);
    expect(canReadSystemEvents(AUDIT_REVIEWER_ACCESS)).toBe(false);
  });

  test('the sidebar lists exactly the screens the permissions open', () => {
    const labels = (access: Parameters<typeof auditNavigation>[0]) =>
      auditNavigation(access).map((item) => item.to);
    expect(labels(SYSTEM_ADMINISTRATOR_ACCESS)).toEqual([
      '/admin/audit-trail',
      '/admin/system-event-logs',
    ]);
    expect(labels(INTEGRATION_REVIEWER_ACCESS)).toEqual([
      '/admin/audit-trail',
      '/admin/integration-audit-logs',
    ]);
    expect(labels(PROJECT_MANAGER_ACCESS)).toEqual([]);
    expect(labels(null)).toEqual([]);
  });

  test('the workspace offers Activity and Audit as two tabs, each on its own permission (acceptance criterion 1)', () => {
    const tabs = (access: Parameters<typeof projectAccess>[2]) =>
      visibleTabs(projectAccess(reviewerSession().user, projectDetail(), access))
        .map((tab) => tab.key)
        .filter((key) => key === 'activity' || key === 'audit');
    expect(tabs(AUDIT_REVIEWER_ACCESS)).toEqual(['activity', 'audit']);
    expect(tabs(PROJECT_MANAGER_ACCESS)).toEqual(['activity']);
    expect(tabs(SYSTEM_ADMINISTRATOR_ACCESS)).toEqual([]);
    expect(tabs(NO_AUDIT_ACCESS)).toEqual([]);
    expect(tabs(null)).toEqual([]);
  });
});

describe('what the filters send (filters.ts)', () => {
  test('SCR-058 asks for exactly the project categories held, narrowed to a chosen one, on its project', () => {
    const categories = projectAuditCategories(AUDIT_REVIEWER_ACCESS);
    expect(toAuditQuery(blank, { categories, projectId: PROJECT })).toMatchObject({
      categories: ['FINANCIAL', 'BUSINESS'],
      projectId: PROJECT,
    });
    expect(
      toAuditQuery({ ...blank, category: 'BUSINESS' }, { categories, projectId: PROJECT })
        .categories,
    ).toEqual(['BUSINESS']);
    // A category typed into the address that the screen does not read is ignored, not sent.
    expect(
      toAuditQuery({ ...blank, category: 'SECURITY' }, { categories, projectId: PROJECT })
        .categories,
    ).toEqual(['FINANCIAL', 'BUSINESS']);
    // The screen's project wins over an address parameter.
    expect(
      toAuditQuery({ ...blank, projectId: ID }, { categories, projectId: PROJECT }).projectId,
    ).toBe(PROJECT);
  });

  test('only the API’s values are sent: a malformed id or unknown choice is dropped', () => {
    const query = toAuditQuery({
      ...blank,
      outcome: 'MAYBE',
      eventClass: 'DATA_CHANGE',
      actorUserId: 'not-an-id',
      correlationId: ID,
      from: '2026-10-01',
      to: '2026-10-31',
    });
    expect(query.outcomes).toEqual([]);
    expect(query.eventClasses).toEqual(['DATA_CHANGE']);
    expect(query.actorUserId).toBeUndefined();
    expect(query.correlationId).toBe(ID);
    expect(query.occurredFrom).toBe(startOfDay('2026-10-01'));
    expect(query.occurredTo).toBe(endOfDay('2026-10-31'));
    expect(new Date(endOfDay('2026-10-31')).getTime()).toBeGreaterThan(
      new Date(startOfDay('2026-10-31')).getTime(),
    );
  });

  test('a filter is refused before it is sent when an id is malformed or the range ends before it starts', () => {
    expect(filterProblems({ ...blank, projectId: 'abc' })).toEqual({ projectId: 'malformed' });
    expect(filterProblems({ ...blank, from: '2026-10-05', to: '2026-10-01' })).toEqual({
      to: 'dateBeforeStart',
    });
    expect(filterProblems({ ...blank, projectId: PROJECT, from: '2026-10-01' })).toEqual({});
  });

  test('Activity asks for the project, one known domain and the period', () => {
    expect(
      toActivityQuery({ kind: 'RISK', from: '', to: '2026-10-31' }, PROJECT, ACTIVITY_KINDS),
    ).toEqual({
      projectId: PROJECT,
      activityKinds: ['RISK'],
      occurredFrom: undefined,
      occurredTo: endOfDay('2026-10-31'),
    });
    expect(
      toActivityQuery({ kind: 'DROP TABLE', from: '', to: '' }, PROJECT, ACTIVITY_KINDS)
        .activityKinds,
    ).toEqual([]);
  });
});

describe('source links (presentation.ts)', () => {
  test('a record with a screen of its own is linked by id; one without is not', () => {
    const subject = (subjectModule: string, subjectType: string) => ({
      subjectModule,
      subjectType,
      subjectId: ID,
    });
    expect(sourceRecordPath(subject('Risk', 'Risk'), PROJECT)).toBe(
      `/projects/${PROJECT}/risks/${ID}`,
    );
    expect(sourceRecordPath(subject('Risk', 'Risk'), null)).toBeNull();
    expect(sourceRecordPath(subject('Closure', 'ClosureCase'), PROJECT)).toBe(
      `/closure-requests/closure/${ID}`,
    );
    expect(sourceRecordPath(subject('ChangeRequest', 'ChangeRequest'), PROJECT)).toBe(
      `/change-requests/${ID}`,
    );
    expect(sourceRecordPath(subject('Progress', 'ProgressCycle'), PROJECT)).toBeNull();
    expect(
      sourceRecordPath({ subjectModule: 'Risk', subjectType: 'Risk', subjectId: null }, PROJECT),
    ).toBeNull();
  });
});

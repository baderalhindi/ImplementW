import { describe, expect, test } from 'vitest';

import {
  DEPARTMENT_ID,
  entitySession,
  ENTITY_ID,
  FULL_PROFILE_ID,
  LIGHT_PROFILE_ID,
  OTHER_DEPARTMENT_ID,
  PROFILE_SETTINGS,
  projectDetail,
  PROJECT_ID,
  reviewerSession,
} from '@/test/projectFixtures.ts';

import {
  assignmentsReaching,
  ownEntityId,
  type ProjectAccess,
  statusCommands,
  visibleTabs,
} from './access.ts';
import { type ProjectStatus } from './api/types.ts';
import { profileMandatoryFields } from './governance.ts';
import { formatSar } from './presentation.ts';
import {
  checkRegistration,
  EMPTY_REGISTRATION,
  moneyString,
  type RegistrationValues,
} from './registration.ts';

const internalReached: ProjectAccess = { internal: true, reached: true, creator: false };
const externalReached: ProjectAccess = { internal: false, reached: true, creator: true };
const internalUnreached: ProjectAccess = { internal: true, reached: false, creator: false };

describe('scope reach (access.ts)', () => {
  test('an assignment reaches a project when each of its anchors matches, and an unanchored one reaches all', () => {
    const project = projectDetail();
    const user = reviewerSession().user;
    const [base] = user.roleAssignments;
    if (base === undefined) {
      throw new Error('reviewerSession has one assignment');
    }
    const anchored = (overrides: object) => ({
      ...user,
      roleAssignments: [{ ...base, ...overrides }],
    });

    expect(assignmentsReaching(anchored({ departmentId: DEPARTMENT_ID }), project)).toHaveLength(1);
    expect(
      assignmentsReaching(anchored({ departmentId: OTHER_DEPARTMENT_ID }), project),
    ).toHaveLength(0);
    expect(assignmentsReaching(anchored({ departmentId: null }), project)).toHaveLength(1);
    expect(
      assignmentsReaching(anchored({ departmentId: null, projectId: PROJECT_ID }), project),
    ).toHaveLength(1);
    expect(
      assignmentsReaching(
        anchored({ departmentId: null, projectId: '00000000-0000-4000-8000-000000000000' }),
        project,
      ),
    ).toHaveLength(0);
    expect(
      assignmentsReaching(entitySession().user, projectDetail({ externalEntityId: null })),
    ).toHaveLength(0);
  });

  test('an external user registers for their one entity; an internal user for none', () => {
    expect(ownEntityId(entitySession().user)).toBe(ENTITY_ID);
    expect(ownEntityId(reviewerSession().user)).toBeNull();
  });

  test('tabs: review history is AHDA’s, progress, schedule, tasks, milestones, financials, KPIs, risks and documents need reach, the rest are for anyone the API showed the project', () => {
    const keys = (access: ProjectAccess) => visibleTabs(access).map((tab) => tab.key);
    expect(keys(internalReached)).toEqual([
      'overview',
      'registration',
      'location',
      'progress',
      'schedule',
      'tasks',
      'milestones',
      'financials',
      'kpis',
      'risks',
      'reviews',
      'documents',
    ]);
    expect(keys(externalReached)).toEqual([
      'overview',
      'registration',
      'location',
      'progress',
      'schedule',
      'tasks',
      'milestones',
      'financials',
      'kpis',
      'risks',
      'documents',
    ]);
    expect(keys(internalUnreached)).toEqual(['overview', 'registration', 'location']);
  });

  test('MOD-003 never offers an AHDA gate to an external user, nor anything without reach', () => {
    const all: ProjectStatus[] = [
      'DRAFT',
      'SUBMITTED',
      'UNDER_REVIEW',
      'RETURNED',
      'APPROVED_PLANNED',
      'ACTIVE',
    ];
    for (const status of all) {
      const external = statusCommands(status, externalReached);
      expect(external).not.toContain('startReview');
      expect(external).not.toContain('activate');
      expect(statusCommands(status, internalUnreached)).toEqual([]);
    }
    expect(statusCommands('SUBMITTED', internalReached)).toEqual(['withdraw', 'startReview']);
    expect(statusCommands('APPROVED_PLANNED', internalReached)).toEqual(['activate']);
    expect(statusCommands('UNDER_REVIEW', internalReached)).toEqual([]);
  });
});

describe('registration checks (registration.ts)', () => {
  const values = (overrides: Partial<RegistrationValues>): RegistrationValues => ({
    ...EMPTY_REGISTRATION,
    ...overrides,
  });

  test('only the required fields are required, and each shape is the API’s', () => {
    expect(checkRegistration(values({}), ['title']).title).toBe('REQUIRED');
    expect(checkRegistration(values({ title: 'x'.repeat(2001) }), []).title).toBe('MAX_LENGTH');
    expect(
      checkRegistration(values({ registrationBudgetSar: '-5' }), []).registrationBudgetSar,
    ).toBe('MALFORMED');
    expect(
      checkRegistration(values({ registrationBudgetSar: '1.005' }), []).registrationBudgetSar,
    ).toBe('MALFORMED');
    expect(
      checkRegistration(values({ registrationBudgetSar: '12' }), []).registrationBudgetSar,
    ).toBeNull();
    expect(checkRegistration(values({ latitude: '-90' }), []).latitude).toBeNull();
    expect(checkRegistration(values({ latitude: '90.1' }), []).latitude).toBe('MALFORMED');
    expect(checkRegistration(values({ longitude: '-180' }), []).longitude).toBeNull();
    expect(
      checkRegistration(
        values({ plannedStartDate: '2026-05-02', plannedEndDate: '2026-05-01' }),
        [],
      ).plannedEndDate,
    ).toBe('DATE_BEFORE_START');
    expect(
      checkRegistration(
        values({ plannedStartDate: '2026-05-01', plannedEndDate: '2026-05-01' }),
        [],
      ).plannedEndDate,
    ).toBeUndefined();
  });

  test('money is sent with exactly two decimals (R-16) and shown grouped', () => {
    expect(moneyString('12')).toBe('12.00');
    expect(moneyString('12.5')).toBe('12.50');
    expect(formatSar('1500000.50')).toBe('1,500,000.50');
    expect(formatSar('-999.00')).toBe('-999.00');
  });
});

describe('governance profile fields (governance.ts)', () => {
  test('a profile’s codes name registration properties; other codes ask nothing of the form', () => {
    expect(profileMandatoryFields(PROFILE_SETTINGS, FULL_PROFILE_ID)).toEqual([
      'regionItemId',
      'description',
    ]);
    expect(profileMandatoryFields(PROFILE_SETTINGS, LIGHT_PROFILE_ID)).toEqual([]);
    expect(profileMandatoryFields(undefined, FULL_PROFILE_ID)).toEqual([]);
  });
});

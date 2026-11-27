import { type PermissionCode } from '@/features/identity-access/permissions/permissionCodes.ts';
import { type Permissions } from '@/features/identity-access/permissions/permissions.ts';
import { type SessionUser } from '@/features/identity-access/session/sessionApi.ts';
import { holdsOnProject, isClosed, isInternal } from '@/features/projects/access.ts';
import { type ProjectDetail } from '@/features/projects/api/types.ts';

import {
  type FinancialProgressUpdateDetail,
  type KpiAssignmentDetail,
  type KpiMeasurementDetail,
} from './api/types.ts';

// What the WF-14 screens offer a person. This is navigation, not protection: every command is decided again by the API
// and a refusal is shown as it comes. Only the two views ship today (TASK-052 D-10, F-2); FINANCIAL_SUBMIT and
// KPI_RECORD are decided on the project's anchors with its Project Manager as owner, so the project's own Project
// Manager is offered entry, on an ACTIVE project (FINANCIAL_KPI_PROJECT_NOT_ELIGIBLE otherwise).

type ProjectFacts = Pick<
  ProjectDetail,
  'id' | 'status' | 'projectManagerUserId' | 'departmentId' | 'externalEntityId'
>;

function manages(user: SessionUser, project: ProjectFacts): boolean {
  return project.status === 'ACTIVE' && project.projectManagerUserId === user.id;
}

/**
 * AHDA's gate (ADR-013, D-15): internal, holding the review permission on the project (FINANCIAL_REVIEW or KPI_REVIEW:
 * R02 ALL, R03 DEPT), and not the person whose figures they are; never on a CLOSED project, which takes no write.
 */
function reviews(
  user: SessionUser,
  permissions: Permissions,
  project: ProjectFacts,
  code: PermissionCode,
  authorId: string | null,
): boolean {
  return (
    !isClosed(project.status) &&
    isInternal(user) &&
    holdsOnProject(permissions, user, project, code) &&
    authorId !== user.id
  );
}

/** MOD-023: start, edit and submit a period's financial update. */
export function canReportFinancials(user: SessionUser, project: ProjectFacts): boolean {
  return manages(user, project);
}

/** Start the review of, return or publish a submitted update; never the submitter. */
export function canReviewFinancials(
  user: SessionUser,
  permissions: Permissions,
  project: ProjectFacts,
  update: FinancialProgressUpdateDetail,
): boolean {
  return (
    (update.status === 'SUBMITTED' || update.status === 'UNDER_REVIEW') &&
    reviews(user, permissions, project, 'FINANCIAL_REVIEW', update.submittedByUserId)
  );
}

/** MOD-024: record, edit and submit a value of an ACTIVE assignment. */
export function canRecordKpi(
  user: SessionUser,
  project: ProjectFacts,
  assignment: Pick<KpiAssignmentDetail, 'status'>,
): boolean {
  return manages(user, project) && assignment.status === 'ACTIVE';
}

/** Publish a submitted value; never the person who recorded it. */
export function canPublishKpi(
  user: SessionUser,
  permissions: Permissions,
  project: ProjectFacts,
  measurement: KpiMeasurementDetail,
): boolean {
  return (
    measurement.status === 'SUBMITTED' &&
    reviews(user, permissions, project, 'KPI_REVIEW', measurement.recordedByUserId)
  );
}

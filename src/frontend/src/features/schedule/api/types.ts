import { type NarrativeText, type NarrativeTextRequest } from '@/features/projects/api/types.ts';

// The WF-03 representations (TASK-046, schedule-baseline.md §4) as the API serialises them: enums in
// SNAKE_CASE_UPPER, ids as strings, calendar dates as ISO 8601 `YYYY-MM-DD` (UTC calendar dates, TASK-046 F-13).
// Every planned, forecast and baseline date is the backend's calculation; the client sends only the inputs.

export type ScheduleActivityKind = 'SUMMARY' | 'ACTIVITY';

export type ScheduleActivityStatus = 'PLANNED' | 'IN_PROGRESS' | 'COMPLETED' | 'CANCELLED';

/** Finish-to-start, start-to-start, finish-to-finish. Start-to-finish is refused (TASK-046 F-11). */
export type ScheduleDependencyType = 'FS' | 'SS' | 'FF';

/** UNDER_REVIEW is in the value set but never produced (TASK-046 F-8). */
export type ProjectBaselineStatus =
  | 'DRAFT'
  | 'SUBMITTED'
  | 'UNDER_REVIEW'
  | 'RETURNED'
  | 'ACTIVE'
  | 'SUPERSEDED'
  | 'REJECTED'
  | 'WITHDRAWN';

/** APPROVED through WF-11 or ADR-015's direct activation; DECLARED is ADR-014's intake baseline. */
export type BaselineType = 'APPROVED' | 'DECLARED';

/** A rating whose inputs are missing is UNKNOWN, never coerced to a colour. */
export type ScheduleHealth = 'GREEN' | 'AMBER' | 'RED' | 'UNKNOWN';

/** One per project once initialized; `activeBaselineId` is the single ACTIVE baseline, if any. */
export interface ProjectScheduleDetail {
  id: string;
  projectId: string;
  calendarItemId: string | null;
  activeBaselineId: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
}

/**
 * An activity with its working plan, its Current Forecast and the ACTIVE baseline's dates side by side (BR-SCH-002).
 * The kind is derived: a node with children is a SUMMARY. Variances are signed working days, positive late, null
 * without a baseline entry (TASK-046 D-9).
 */
export interface ScheduleActivityDetail {
  id: string;
  projectId: string;
  projectScheduleId: string;
  parentActivityId: string | null;
  wbsCode: string;
  name: NarrativeText;
  activityKind: ScheduleActivityKind;
  status: ScheduleActivityStatus;
  sortOrder: number;
  requestedStartDate: string;
  plannedStartDate: string;
  plannedFinishDate: string;
  plannedDurationDays: number;
  forecastStartDate: string;
  forecastFinishDate: string;
  actualStartDate: string | null;
  actualFinishDate: string | null;
  baselineId: string | null;
  baselineStartDate: string | null;
  baselineFinishDate: string | null;
  startVarianceDays: number | null;
  finishVarianceDays: number | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
}

export interface ScheduleDependencyDetail {
  id: string;
  projectId: string;
  predecessorActivityId: string;
  successorActivityId: string;
  dependencyType: ScheduleDependencyType;
  lagDays: number;
  createdAt: string;
  createdBy: string;
}

export interface ProjectBaselineDetail {
  id: string;
  projectId: string;
  baselineType: BaselineType;
  versionNo: number;
  revisionNo: number;
  status: ProjectBaselineStatus;
  baselineFinishDate: string;
  activatedAt: string | null;
  supersededAt: string | null;
  supersededByBaselineId: string | null;
  changeAuthorizationId: string | null;
  projectIntakeId: string | null;
  declaredEndDate: string | null;
  declaredScope: NarrativeText | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
}

/** A baseline's frozen copy of one activity, written when the baseline activated. */
export interface BaselineActivityDetail {
  scheduleActivityId: string;
  parentActivityId: string | null;
  activityKind: ScheduleActivityKind;
  plannedStartDate: string;
  plannedFinishDate: string;
  plannedDurationDays: number;
}

export interface BaselineDependencyDetail {
  predecessorActivityId: string;
  successorActivityId: string;
  dependencyType: ScheduleDependencyType;
  lagDays: number;
}

/** CURRENT/LIVE Schedule Health: the project's finish variance against the ACTIVE baseline, rated by WF-03. */
export interface ScheduleHealthStatusDetail {
  id: string;
  projectId: string;
  projectBaselineId: string | null;
  scheduleHealth: ScheduleHealth;
  finishVarianceDays: number | null;
  computedAt: string;
  healthRuleConfigurationVersionId: string | null;
}

/** An activity's inputs, as a whole (R-5). Planned and forecast dates are calculated, never sent (DCL-SCH-03). */
export interface ScheduleActivityRequest {
  parentActivityId: string | null;
  wbsCode: string;
  name: NarrativeTextRequest;
  requestedStartDate: string;
  plannedDurationDays: number;
  sortOrder: number;
}

export interface ScheduleActivityCreateRequest extends ScheduleActivityRequest {
  projectId: string;
}

export interface ScheduleDependencyRequest {
  predecessorActivityId: string;
  successorActivityId: string;
  dependencyType: ScheduleDependencyType;
  lagDays: number;
}

export interface ScheduleForecastRequest {
  forecastStartDate: string;
  forecastFinishDate: string;
}

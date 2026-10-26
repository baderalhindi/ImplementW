import { checkText, CODE_LENGTH } from '@/features/identity-access/forms.ts';
import { narrativeRequest } from '@/features/progress/progressUpdate.ts';
import { TEXT_LENGTH } from '@/features/projects/registration.ts';
import { type Language } from '@/shared/i18n/i18n.ts';

import {
  type ScheduleActivityDetail,
  type ScheduleActivityRequest,
  type ScheduleDependencyDetail,
} from './api/types.ts';
import { checkDays, dayNumber, fromDayNumber } from './dependencyRules.ts';
import { descendantsOf } from './gantt.ts';
import { type FieldCodes } from './useFieldErrors.ts';

// The activity form of SCR-060: the inputs ScheduleActivityRequest.Validate checks, checked the same way. The planned
// and forecast dates are the backend's calculation and are never typed (DCL-SCH-03).

export interface ActivityValues {
  parentActivityId: string;
  wbsCode: string;
  name: string;
  requestedStartDate: string;
  plannedDurationDays: string;
  sortOrder: string;
}

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

/** REQUIRED when blank, MALFORMED unless it is a real `YYYY-MM-DD` calendar date. */
export function checkDate(value: string): string | null {
  if (value.trim() === '') {
    return 'REQUIRED';
  }
  return ISO_DATE.test(value) && fromDayNumber(dayNumber(value)) === value ? null : 'MALFORMED';
}

export function checkActivity(values: ActivityValues): FieldCodes {
  return {
    wbsCode: checkText(values.wbsCode, { required: true, maxLength: CODE_LENGTH }),
    name: checkText(values.name, { required: true, maxLength: TEXT_LENGTH }),
    requestedStartDate: checkDate(values.requestedStartDate),
    plannedDurationDays: checkDays(values.plannedDurationDays, { min: 1, required: true }),
    sortOrder: checkDays(values.sortOrder, { min: 0, required: false }),
  };
}

export function emptyActivity(sortOrder: number, requestedStartDate: string): ActivityValues {
  return {
    parentActivityId: '',
    wbsCode: '',
    name: '',
    requestedStartDate,
    plannedDurationDays: '',
    sortOrder: String(sortOrder),
  };
}

export function activityValuesOf(activity: ScheduleActivityDetail): ActivityValues {
  return {
    parentActivityId: activity.parentActivityId ?? '',
    wbsCode: activity.wbsCode,
    name: activity.name.text,
    requestedStartDate: activity.requestedStartDate,
    plannedDurationDays: String(activity.plannedDurationDays),
    sortOrder: String(activity.sortOrder),
  };
}

/** The request, as a whole (R-5). An unchanged name keeps the language it was entered in. */
export function toActivityRequest(
  values: ActivityValues,
  language: Language,
  before: ScheduleActivityDetail | null,
): ScheduleActivityRequest {
  return {
    parentActivityId: values.parentActivityId === '' ? null : values.parentActivityId,
    wbsCode: values.wbsCode.trim(),
    name: narrativeRequest(values.name, language, before?.name ?? null),
    requestedStartDate: values.requestedStartDate,
    plannedDurationDays: Number(values.plannedDurationDays),
    sortOrder: values.sortOrder.trim() === '' ? 0 : Number(values.sortOrder),
  };
}

/**
 * The activities that may be the parent (D-4): a live activity of the schedule, never the activity itself or one under
 * it (VAL-SCH-003), and never a dependency end, which would make it a summary (BR-SCH-026).
 */
export function parentChoices(
  activities: ScheduleActivityDetail[],
  dependencies: ScheduleDependencyDetail[],
  editing: ScheduleActivityDetail | null,
): ScheduleActivityDetail[] {
  const excluded = editing === null ? new Set<string>() : descendantsOf(editing.id, activities);
  const linked = new Set(
    dependencies.flatMap((dependency) => [
      dependency.predecessorActivityId,
      dependency.successorActivityId,
    ]),
  );
  return activities.filter(
    (activity) =>
      activity.status !== 'CANCELLED' && !excluded.has(activity.id) && !linked.has(activity.id),
  );
}

/** The activity's re-sent inputs with only its requested start changed: the PUT a drag of the plan makes. */
export function withRequestedStart(
  activity: ScheduleActivityDetail,
  requestedStartDate: string,
): ScheduleActivityRequest {
  return {
    parentActivityId: activity.parentActivityId,
    wbsCode: activity.wbsCode,
    // Unchanged, so it keeps the language it was entered in; the interface language is never used.
    name: narrativeRequest(activity.name.text, 'en', activity.name),
    requestedStartDate,
    plannedDurationDays: activity.plannedDurationDays,
    sortOrder: activity.sortOrder,
  };
}

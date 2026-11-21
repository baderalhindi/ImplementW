import { type OnPlanStatus, type ProjectSummary } from '@/features/projects/api/types.ts';
import { apiRequest } from '@/shared/api/httpClient.ts';

// The Project Manager's Home list (review round 8, point 13): every project they manage, what needs action this month, filter chips, search,
// sorting by any column with "needs action first" by default, and pages of 20. The rules are here, apart from the table, so they can be read
// and tested on their own.

/** Review round 8, point 13: pages of 20 rows. */
export const MANAGED_PAGE_SIZE = 20;

/** What waits on one project, as the API counts it (GET /users/current/managed-project-attention). */
export interface ManagedProjectAttention {
  projectId: string;
  /** The earliest reporting period due by the end of this month and not yet published; null when none is. */
  updateDueDate: string | null;
  overdueTasks: number;
  overdueMilestones: number;
}

export interface ManagedProjectAttentionList {
  today: string;
  monthEnd: string;
  items: ManagedProjectAttention[];
}

export const managedProjectsApi = {
  attention: async (signal?: AbortSignal) =>
    (
      await apiRequest<ManagedProjectAttentionList>('/users/current/managed-project-attention', {
        signal,
      })
    ).data,
};

/** One row: the project, what waits on it, and the update requests waiting on the person for it. */
export interface ManagedRow {
  project: ProjectSummary;
  attention: ManagedProjectAttention | null;
  requestsWaiting: number;
}

/** "يحتاج إجراء هذا الشهر": an update due, a task or milestone overdue, or a request waiting. */
export function needsAction(row: ManagedRow): boolean {
  const attention = row.attention;
  return (
    row.requestsWaiting > 0 ||
    (attention !== null &&
      (attention.updateDueDate !== null ||
        attention.overdueTasks > 0 ||
        attention.overdueMilestones > 0))
  );
}

/** How many things wait on the project: the "needs action" column's order. */
export function actionCount(row: ManagedRow): number {
  const attention = row.attention;
  return (
    row.requestsWaiting +
    (attention === null
      ? 0
      : (attention.updateDueDate === null ? 0 : 1) +
        attention.overdueTasks +
        attention.overdueMilestones)
  );
}

export type ManagedChip = 'needsAction' | 'lateOrStalled' | 'noStagePlan';

export const MANAGED_CHIPS: readonly ManagedChip[] = [
  'needsAction',
  'lateOrStalled',
  'noStagePlan',
];

const CHIP_TESTS: Record<ManagedChip, (row: ManagedRow) => boolean> = {
  needsAction,
  lateOrStalled: (row) =>
    row.project.indicators?.onPlan === 'LATE' || row.project.indicators?.onPlan === 'STALLED',
  // Point 02: a project with no method, or on the stage method with no plan, asks for its set-up.
  noStagePlan: (row) => row.project.indicators?.progress?.needsSetup === true,
};

/** A project found by its name or its number (formal or RPMO's earlier one), ignoring case. */
export function matchesSearch(project: ProjectSummary, search: string): boolean {
  const wanted = search.trim().toLocaleLowerCase();
  if (wanted === '') {
    return true;
  }
  return [project.title.text, project.formalProjectId, project.legacyReference].some(
    (value) => value?.toLocaleLowerCase().includes(wanted) === true,
  );
}

/** The rows the chips (all of them at once) and the search keep. */
export function filterRows(
  rows: readonly ManagedRow[],
  chips: ReadonlySet<ManagedChip>,
  search: string,
): ManagedRow[] {
  return rows.filter(
    (row) =>
      matchesSearch(row.project, search) && [...chips].every((chip) => CHIP_TESTS[chip](row)),
  );
}

export type ManagedSortKey =
  'needsAction' | 'title' | 'onPlan' | 'daysRemaining' | 'nextAction' | 'requestsWaiting';

export interface ManagedSort {
  key: ManagedSortKey;
  direction: 'ascending' | 'descending';
}

/** Review round 8, point 13: the default order puts what needs action first. */
export const DEFAULT_MANAGED_SORT: ManagedSort = { key: 'needsAction', direction: 'descending' };

/** Worst first when descending: stalled, late, on track, then not measured. */
const ON_PLAN_SEVERITY: Record<OnPlanStatus, number> = { STALLED: 3, LATE: 2, ON_TRACK: 1 };

function onPlanSeverity(row: ManagedRow): number {
  const onPlan = row.project.indicators?.onPlan;
  return onPlan == null ? 0 : ON_PLAN_SEVERITY[onPlan];
}

/** An overdue next action before a scheduled one, and both before none. */
function nextActionRank(row: ManagedRow): number {
  const due = row.project.indicators?.actionDue;
  return due === 'OVERDUE' ? 2 : due === 'SCHEDULED' ? 1 : 0;
}

/** Each column's value to compare; null sorts last whichever the direction. */
const SORT_VALUES: Record<ManagedSortKey, (row: ManagedRow) => number | string | null> = {
  needsAction: actionCount,
  title: (row) => row.project.title.text,
  onPlan: onPlanSeverity,
  daysRemaining: (row) => row.project.indicators?.daysRemaining ?? null,
  nextAction: nextActionRank,
  requestsWaiting: (row) => row.requestsWaiting,
};

function compareValues(a: number | string, b: number | string, language: string): number {
  return typeof a === 'string' && typeof b === 'string'
    ? a.localeCompare(b, language, { numeric: true })
    : Number(a) - Number(b);
}

/**
 * The rows in the chosen order. Ties fall back to the default order (needs action, then the worst on-plan, then the fewest days left) and
 * finally the title, so the order is the same on every read.
 */
export function sortRows(
  rows: readonly ManagedRow[],
  sort: ManagedSort,
  language: string,
): ManagedRow[] {
  const sign = sort.direction === 'ascending' ? 1 : -1;
  const value = SORT_VALUES[sort.key];
  const fallback = (a: ManagedRow, b: ManagedRow) =>
    actionCount(b) - actionCount(a) ||
    onPlanSeverity(b) - onPlanSeverity(a) ||
    (a.project.indicators?.daysRemaining ?? Number.MAX_SAFE_INTEGER) -
      (b.project.indicators?.daysRemaining ?? Number.MAX_SAFE_INTEGER) ||
    a.project.title.text.localeCompare(b.project.title.text, language, { numeric: true });
  return [...rows].sort((a, b) => {
    const first = value(a);
    const second = value(b);
    if (first === null || second === null) {
      return first === second ? fallback(a, b) : first === null ? 1 : -1;
    }
    return sign * compareValues(first, second, language) || fallback(a, b);
  });
}

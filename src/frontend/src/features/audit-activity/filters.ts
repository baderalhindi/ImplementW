import {
  type ActivityQuery,
  type AuditCategory,
  type AuditEventClass,
  type AuditEventQuery,
  type AuditOutcome,
} from './api/types.ts';

// The audit and Activity filters, kept in the URL so a reload or a shared link shows the same rows. Dates are whole
// days in the person's time zone, sent as the instants that bound them. Only values the API accepts are sent: an id is
// a UUID, a choice is one of the API's values, and a range ends no earlier than it starts.

export const AUDIT_EVENT_CLASSES: readonly AuditEventClass[] = [
  'AUTHENTICATION',
  'AUTHORIZATION_DENIAL',
  'PRIVILEGED_ACTION',
  'PERMISSION_CHANGE',
  'LIFECYCLE_TRANSITION',
  'DATA_CHANGE',
  'APPROVAL_DECISION',
  'INTEGRATION',
  'CONFIGURATION_CHANGE',
];

export const AUDIT_OUTCOMES: readonly AuditOutcome[] = ['SUCCESS', 'DENIED', 'FAILED'];

/** The audit filters a screen may offer, by URL parameter. */
export interface AuditFilterValues {
  category: string;
  eventClass: string;
  outcome: string;
  from: string;
  to: string;
  eventType: string;
  projectId: string;
  actorUserId: string;
  correlationId: string;
}

export type AuditFilterName = keyof AuditFilterValues;

export const AUDIT_FILTER_NAMES: readonly AuditFilterName[] = [
  'category',
  'eventClass',
  'outcome',
  'from',
  'to',
  'eventType',
  'projectId',
  'actorUserId',
  'correlationId',
];

/** The id filters: free text that must be a UUID before it is sent. */
export const ID_FILTERS: readonly AuditFilterName[] = ['projectId', 'actorUserId', 'correlationId'];

export type FilterProblem = 'malformed' | 'dateBeforeStart';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const DAY = /^\d{4}-\d{2}-\d{2}$/;

export function isUuid(value: string): boolean {
  return UUID.test(value);
}

function oneOf<T extends string>(value: string, allowed: readonly T[]): T | undefined {
  return allowed.find((candidate) => candidate === value);
}

export function auditFilterValues(params: URLSearchParams): AuditFilterValues {
  const read = (name: AuditFilterName) => params.get(name)?.trim() ?? '';
  return {
    category: read('category'),
    eventClass: read('eventClass'),
    outcome: read('outcome'),
    from: read('from'),
    to: read('to'),
    eventType: read('eventType'),
    projectId: read('projectId'),
    actorUserId: read('actorUserId'),
    correlationId: read('correlationId'),
  };
}

/** What is wrong with a set of filters, by field; empty when they can be sent. */
export function filterProblems(
  values: Pick<AuditFilterValues, 'from' | 'to'> & Partial<AuditFilterValues>,
): Partial<Record<AuditFilterName, FilterProblem>> {
  const problems: Partial<Record<AuditFilterName, FilterProblem>> = {};
  for (const name of ID_FILTERS) {
    const value = values[name] ?? '';
    if (value !== '' && !isUuid(value)) {
      problems[name] = 'malformed';
    }
  }
  for (const name of ['from', 'to'] as const) {
    if (values[name] !== '' && !DAY.test(values[name])) {
      problems[name] = 'malformed';
    }
  }
  if (
    problems.from === undefined &&
    problems.to === undefined &&
    values.from !== '' &&
    values.to !== '' &&
    values.to < values.from
  ) {
    problems.to = 'dateBeforeStart';
  }
  return problems;
}

/** The first instant of a day in the person's time zone. */
export function startOfDay(day: string): string {
  return new Date(`${day}T00:00:00`).toISOString();
}

/** The last instant of a day in the person's time zone. */
export function endOfDay(day: string): string {
  return new Date(`${day}T23:59:59.999`).toISOString();
}

/**
 * The API query for a screen's filters. `categories` are the ones the screen reads: a chosen category is narrowed to
 * them, and with none chosen all of them are asked for (an empty list leaves it to the API: every category held).
 */
export function toAuditQuery(
  values: AuditFilterValues,
  scope: { categories?: readonly AuditCategory[]; projectId?: string | undefined } = {},
): AuditEventQuery {
  const offered = scope.categories ?? [];
  const chosen = offered.find((category) => category === values.category);
  const eventClass = oneOf(values.eventClass, AUDIT_EVENT_CLASSES);
  const outcome = oneOf(values.outcome, AUDIT_OUTCOMES);
  const id = (value: string) => (isUuid(value) ? value : undefined);
  const day = (value: string, bound: (day: string) => string) =>
    DAY.test(value) ? bound(value) : undefined;
  return {
    categories: chosen === undefined ? offered : [chosen],
    eventClasses: eventClass === undefined ? [] : [eventClass],
    outcomes: outcome === undefined ? [] : [outcome],
    occurredFrom: day(values.from, startOfDay),
    occurredTo: day(values.to, endOfDay),
    eventType: values.eventType === '' ? undefined : values.eventType,
    projectId: scope.projectId ?? id(values.projectId),
    actorUserId: id(values.actorUserId),
    correlationId: id(values.correlationId),
  };
}

/** SCR-057's filters: a source domain and a period. */
export interface ActivityFilterValues {
  kind: string;
  from: string;
  to: string;
}

export function activityFilterValues(params: URLSearchParams): ActivityFilterValues {
  return {
    kind: params.get('kind')?.trim() ?? '',
    from: params.get('from')?.trim() ?? '',
    to: params.get('to')?.trim() ?? '',
  };
}

export function toActivityQuery(
  values: ActivityFilterValues,
  projectId: string,
  kinds: readonly string[],
): ActivityQuery {
  const kind = oneOf(values.kind, kinds);
  return {
    projectId,
    activityKinds: kind === undefined ? [] : [kind],
    occurredFrom: DAY.test(values.from) ? startOfDay(values.from) : undefined,
    occurredTo: DAY.test(values.to) ? endOfDay(values.to) : undefined,
  };
}

import { type NavigationItem } from '@/features/identity-access/routes.tsx';

import {
  type InvocationQuery,
  type InvocationStatus,
  type IntegrationSummary,
  type OperationalAlertStatus,
} from './api/types.ts';

// Which integration each FG-05 screen shows, the screens' addresses (Step 16's proposed routes), and the ADM-048
// filters. The registry is TASK-075's seed (D-15): six codes. ADM-041 to ADM-043 are provider screens, chosen by code;
// ADM-044 is every other integration registered later — an internal system needs no code change to appear there.

export const INTEGRATIONS_OVERVIEW_PATH = '/admin/integrations-overview';
export const SSO_DIRECTORY_PATH = '/admin/sso-directory-integration';
export const EMAIL_EXCHANGE_PATH = '/admin/email-exchange-integration';
export const NAFATH_PATH = '/admin/nafath-integration';
export const INTERNAL_SYSTEMS_PATH = '/admin/internal-system-integrations';
export const API_CATALOGUE_PATH = '/admin/api-integration-catalogue';
export const INTEGRATION_DETAIL_PATH = '/admin/integration-detail-configuration';
export const INTEGRATION_MONITORING_PATH = '/admin/integration-monitoring';
export const INTEGRATION_LOGS_PATH = '/admin/integration-logs';

/** ADM-046 for one integration. Step 16's route names no id; the detail takes it, as ADM-051 does (TASK-074 D-3). */
export function integrationDetailPath(integrationId: string): string {
  return `${INTEGRATION_DETAIL_PATH}/${integrationId}`;
}

/** ADM-048's attempt detail, with its lineage. */
export function invocationPath(invocationId: string): string {
  return `${INTEGRATION_LOGS_PATH}/${invocationId}`;
}

/** ADM-048 filtered: a correlation id finds a request's whole technical lineage (TASK-075 D-14). */
export function logsFilteredBy(name: 'integrationId' | 'correlationId', value: string): string {
  return `${INTEGRATION_LOGS_PATH}?${new URLSearchParams({ [name]: value }).toString()}`;
}

/** ADM-041: the identity runtime of sign-in — directory, SSO and the MFA provider. */
export const SSO_DIRECTORY_CODES: readonly string[] = ['DIRECTORY', 'SSO', 'MFA'];
/** ADM-042: the communication providers WF-15 sends through; SMS beside Exchange per ADR-004. */
export const MESSAGING_CODES: readonly string[] = ['EXCHANGE_EMAIL', 'SMS'];
/** ADM-043: Nafath identity verification. */
export const NAFATH_CODES: readonly string[] = ['NAFATH'];

const PROVIDER_CODES = [...SSO_DIRECTORY_CODES, ...MESSAGING_CODES, ...NAFATH_CODES];

/**
 * The integrations MOD-100 tests: `POST /identity-integration/test` binds to the directory and resolves the SSO and
 * Nafath discovery documents (TASK-028 D-10, TASK-068 D-11). No other integration has a connection test (F-1).
 */
export const CONNECTION_TEST_CODES: readonly string[] = ['DIRECTORY', 'SSO', 'NAFATH'];

export function withCodes(
  items: readonly IntegrationSummary[],
  codes: readonly string[],
): IntegrationSummary[] {
  return codes.flatMap((code) => items.filter((item) => item.code === code));
}

/** ADM-044: every registered integration that is not one of the provider screens' — internal and future systems. */
export function internalSystems(items: readonly IntegrationSummary[]): IntegrationSummary[] {
  return items.filter((item) => !PROVIDER_CODES.includes(item.code));
}

export function hasConnectionTest(code: string): boolean {
  return CONNECTION_TEST_CODES.includes(code);
}

/** The operation FG-05 runs to reconcile an integration; offered only when the API lists it (TASK-075 D-8). */
export const RECONCILE_OPERATION = 'RECONCILE';

/** The alerts still needing an operator: OPEN and ACKNOWLEDGED (TASK-075 D-9). */
export const LIVE_ALERT_STATUSES: readonly OperationalAlertStatus[] = ['OPEN', 'ACKNOWLEDGED'];
export const ALERT_STATUSES: readonly OperationalAlertStatus[] = [
  'OPEN',
  'ACKNOWLEDGED',
  'RESOLVED',
  'DISMISSED',
];

export const INVOCATION_STATUSES: readonly InvocationStatus[] = [
  'IN_PROGRESS',
  'SUCCEEDED',
  'FAILED',
  'RETRY_SCHEDULED',
  'DEAD_LETTER',
];

/** The sidebar's integration group, in the order an operator works: overview, monitoring, logs, then each kind. */
export const integrationAdminNavigation: NavigationItem[] = [
  {
    to: INTEGRATIONS_OVERVIEW_PATH,
    label: 'integrationAdmin.nav.overview',
    permissions: ['INTEGRATION_VIEW'],
  },
  {
    to: INTEGRATION_MONITORING_PATH,
    label: 'integrationAdmin.nav.monitoring',
    permissions: ['INTEGRATION_VIEW'],
  },
  {
    to: INTEGRATION_LOGS_PATH,
    label: 'integrationAdmin.nav.logs',
    permissions: ['INTEGRATION_LOG_VIEW'],
  },
  {
    to: SSO_DIRECTORY_PATH,
    label: 'integrationAdmin.nav.ssoDirectory',
    permissions: ['IDENTITY_INTEGRATION_MANAGE'],
  },
  {
    to: EMAIL_EXCHANGE_PATH,
    label: 'integrationAdmin.nav.emailExchange',
    permissions: ['INTEGRATION_VIEW'],
  },
  { to: NAFATH_PATH, label: 'integrationAdmin.nav.nafath', permissions: ['INTEGRATION_VIEW'] },
  {
    to: INTERNAL_SYSTEMS_PATH,
    label: 'integrationAdmin.nav.internalSystems',
    permissions: ['INTEGRATION_VIEW'],
  },
  {
    to: API_CATALOGUE_PATH,
    label: 'integrationAdmin.nav.apiCatalogue',
    permissions: ['INTEGRATION_VIEW'],
  },
];

const PLAIN_CODE = /^[A-Z][A-Z0-9_]{0,63}$/;

/**
 * A code from a connection test, shown only if it is a plain upper-case code. The API already sends nothing else
 * (ConnectionTestResult: "a fixed code, never the provider's own message"); this keeps any other text — a provider's
 * message that could echo a credential — off the screen should that ever change (acceptance criterion 1).
 */
export function plainCode(value: string | null): string | null {
  return value !== null && PLAIN_CODE.test(value) ? value : null;
}

/** ADM-048's filters, kept in the URL so a reload or a shared link shows the same attempts. */
export interface LogFilterValues {
  integrationId: string;
  status: string;
  operationCode: string;
  correlationId: string;
  idempotencyKey: string;
  sourceReference: string;
  from: string;
  to: string;
}

export type LogFilterName = keyof LogFilterValues;

export const LOG_FILTER_NAMES: readonly LogFilterName[] = [
  'integrationId',
  'status',
  'operationCode',
  'correlationId',
  'idempotencyKey',
  'sourceReference',
  'from',
  'to',
];

export type LogFilterProblem = 'malformed' | 'tooLong' | 'dateBeforeStart';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const DAY = /^\d{4}-\d{2}-\d{2}$/;
/** The API's limits (IntegrationMonitoringQueries): an operation code 100, a key or reference 200. */
const MAX_LENGTH: Partial<Record<LogFilterName, number>> = {
  operationCode: 100,
  idempotencyKey: 200,
  sourceReference: 200,
};

export function isUuid(value: string): boolean {
  return UUID.test(value);
}

export function logFilterValues(params: URLSearchParams): LogFilterValues {
  const read = (name: LogFilterName) => params.get(name)?.trim() ?? '';
  return {
    integrationId: read('integrationId'),
    status: read('status'),
    operationCode: read('operationCode'),
    correlationId: read('correlationId'),
    idempotencyKey: read('idempotencyKey'),
    sourceReference: read('sourceReference'),
    from: read('from'),
    to: read('to'),
  };
}

/** What is wrong with a set of filters, by field; empty when they can be sent. */
export function logFilterProblems(
  values: LogFilterValues,
): Partial<Record<LogFilterName, LogFilterProblem>> {
  const problems: Partial<Record<LogFilterName, LogFilterProblem>> = {};
  for (const name of ['integrationId', 'correlationId'] as const) {
    if (values[name] !== '' && !isUuid(values[name])) {
      problems[name] = 'malformed';
    }
  }
  for (const [name, limit] of Object.entries(MAX_LENGTH) as [LogFilterName, number][]) {
    if (values[name].length > limit) {
      problems[name] = 'tooLong';
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
function startOfDay(day: string): string {
  return new Date(`${day}T00:00:00`).toISOString();
}

/** The last instant of a day in the person's time zone. */
function endOfDay(day: string): string {
  return new Date(`${day}T23:59:59.999`).toISOString();
}

/** The API query for the filters: only values the API accepts are sent. */
export function toInvocationQuery(values: LogFilterValues): InvocationQuery {
  const status = INVOCATION_STATUSES.find((candidate) => candidate === values.status);
  const text = (name: LogFilterName) => {
    const value = values[name];
    return value === '' || value.length > (MAX_LENGTH[name] ?? Infinity) ? undefined : value;
  };
  return {
    integrationId: isUuid(values.integrationId) ? values.integrationId : undefined,
    statuses: status === undefined ? [] : [status],
    operationCode: text('operationCode'),
    correlationId: isUuid(values.correlationId) ? values.correlationId : undefined,
    idempotencyKey: text('idempotencyKey'),
    sourceReference: text('sourceReference'),
    startedFrom: DAY.test(values.from) ? startOfDay(values.from) : undefined,
    startedTo: DAY.test(values.to) ? endOfDay(values.to) : undefined,
  };
}

/** ADM-046's measures window, in days back from now; the API's default (one day) when none is chosen. */
export const METRICS_WINDOWS = [1, 7, 30] as const;
export type MetricsWindow = (typeof METRICS_WINDOWS)[number];

export function metricsWindowFrom(params: URLSearchParams): MetricsWindow {
  const days = Number(params.get('window') ?? '1');
  return METRICS_WINDOWS.find((candidate) => candidate === days) ?? 1;
}

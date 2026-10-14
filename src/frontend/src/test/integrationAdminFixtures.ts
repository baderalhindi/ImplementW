import {
  type IdentityIntegrationStatus,
  type IdentityIntegrationTestResult,
  type IntegrationDefinitionStatus,
  type IntegrationDetail,
  type IntegrationInstanceView,
  type IntegrationSummary,
  type InvocationSummary,
  type OperationalAlertView,
  type SyncRunView,
} from '@/features/integration-admin/api/types.ts';

import { type MockApi, page, problem } from './mockApi.ts';

// FG-05 as TASK-075 answers it, for the integration administration UI tests. The mock enforces what the API enforces:
// activation needs the detail's ETag in If-Match and refuses a no-op (409), a retry is allowed only where the API says
// `retryPermitted`, an alert is closed only with a note, and a set is read comma-joined.
//
// It also plants values a regression could leak — a bind password and a client secret added to the status, a
// provider message added to the connection test, a problem title naming a password — so the tests can show the UI
// renders none of them (TASK-076 acceptance criterion 1). The real API sends none (TASK-028 D-10, TASK-075 D-11).

export const PLANTED_SECRETS = [
  'S3cret-Bind-Passw0rd',
  'oidc-client-secret-9f8e7d',
  'Bearer eyJhbGciOiJSUzI1NiJ9.planted',
] as const;

export const DIRECTORY_ID = '7a000000-0000-4000-8000-000000000001';
export const SSO_ID = '7a000000-0000-4000-8000-000000000002';
export const MFA_ID = '7a000000-0000-4000-8000-000000000003';
export const NAFATH_ID = '7a000000-0000-4000-8000-000000000004';
export const EXCHANGE_ID = '7a000000-0000-4000-8000-000000000005';
export const SMS_ID = '7a000000-0000-4000-8000-000000000006';
export const FINANCE_ID = '7a000000-0000-4000-8000-000000000007';

export const CORRELATION_ID = '7c000000-0000-4000-8000-000000000001';
export const DEAD_LETTER_ID = '7d000000-0000-4000-8000-000000000003';
export const FIRST_ATTEMPT_ID = '7d000000-0000-4000-8000-000000000001';
export const RETRY_ATTEMPT_ID = '7d000000-0000-4000-8000-000000000004';
export const SIGN_IN_CALL_ID = '7d000000-0000-4000-8000-000000000009';
export const OPEN_ALERT_ID = '7e000000-0000-4000-8000-000000000001';
export const ACKNOWLEDGED_ALERT_ID = '7e000000-0000-4000-8000-000000000002';

function instance(
  definitionId: string,
  endpointConfigurationKey: string,
  overrides: Partial<IntegrationInstanceView> = {},
): IntegrationInstanceView {
  return {
    id: definitionId.replace('7a000000', '7b000000'),
    code: 'PRIMARY',
    endpointConfigurationKey,
    isEnabled: true,
    availability: 'UNKNOWN',
    freshness: 'UNKNOWN',
    lastSuccessAt: null,
    lastFailureAt: null,
    evaluatedAt: null,
    openAlertCount: 0,
    ...overrides,
  };
}

function definition(
  id: string,
  code: string,
  en: string,
  ar: string,
  rest: Pick<IntegrationSummary, 'direction' | 'integrationKind'> & {
    instance: IntegrationInstanceView;
    status?: IntegrationDefinitionStatus;
  },
): IntegrationSummary {
  return {
    id,
    code,
    name: { en, ar },
    direction: rest.direction,
    integrationKind: rest.integrationKind,
    status: rest.status ?? 'ACTIVE',
    instances: [rest.instance],
  };
}

/**
 * The seeded registry after an outage (TASK-075 §7.3): the directory reachable but stale — the case where the two
 * indicators disagree — SSO down, Nafath and SMS never called.
 */
export function seededCatalogue(): IntegrationSummary[] {
  return [
    definition(DIRECTORY_ID, 'DIRECTORY', 'Directory (AD/LDAP)', 'دليل المستخدمين (AD/LDAP)', {
      direction: 'INBOUND',
      integrationKind: 'IDENTITY',
      instance: instance(DIRECTORY_ID, 'AD_LDAP_URL', {
        availability: 'UP',
        freshness: 'STALE',
        lastSuccessAt: '2026-10-13T08:00:00Z',
        lastFailureAt: '2026-10-13T07:00:00Z',
        evaluatedAt: '2026-10-14T09:00:00Z',
        openAlertCount: 2,
      }),
    }),
    definition(SSO_ID, 'SSO', 'Single sign-on (OpenID Connect)', 'الدخول الموحد (OpenID Connect)', {
      direction: 'INBOUND',
      integrationKind: 'IDENTITY',
      instance: instance(SSO_ID, 'SSO_OIDC_AUTHORITY', {
        availability: 'DOWN',
        freshness: 'FRESH',
        lastSuccessAt: '2026-10-14T08:59:00Z',
        lastFailureAt: '2026-10-14T09:00:00Z',
        evaluatedAt: '2026-10-14T09:00:00Z',
        openAlertCount: 1,
      }),
    }),
    definition(MFA_ID, 'MFA', 'MFA verification provider', 'مزود التحقق متعدد العوامل', {
      direction: 'OUTBOUND',
      integrationKind: 'IDENTITY',
      instance: instance(MFA_ID, 'MFA_PROVIDER_URL', {
        availability: 'UP',
        lastSuccessAt: '2026-10-14T08:00:00Z',
        evaluatedAt: '2026-10-14T08:00:00Z',
      }),
    }),
    definition(NAFATH_ID, 'NAFATH', 'Nafath identity verification', 'التحقق من الهوية عبر نفاذ', {
      direction: 'OUTBOUND',
      integrationKind: 'VERIFICATION',
      instance: instance(NAFATH_ID, 'NAFATH_AUTHORITY'),
    }),
    definition(EXCHANGE_ID, 'EXCHANGE_EMAIL', 'Exchange e-mail relay', 'خادم البريد Exchange', {
      direction: 'OUTBOUND',
      integrationKind: 'MESSAGING',
      instance: instance(EXCHANGE_ID, 'SMTP_HOST', {
        availability: 'UP',
        lastSuccessAt: '2026-10-14T08:30:00Z',
        evaluatedAt: '2026-10-14T08:30:00Z',
      }),
    }),
    definition(SMS_ID, 'SMS', 'SMS provider', 'مزود الرسائل النصية', {
      direction: 'OUTBOUND',
      integrationKind: 'MESSAGING',
      status: 'DISABLED',
      instance: instance(SMS_ID, 'SMS_PROVIDER_URL'),
    }),
  ];
}

/** An internal system registered after the seed, as the TASK-075 suite registers one. */
export function internalSystem(): IntegrationSummary {
  return definition(FINANCE_ID, 'FINANCE_LEDGER', 'Finance ledger', 'دفتر المالية', {
    direction: 'INBOUND',
    integrationKind: 'FINANCE',
    instance: instance(FINANCE_ID, 'FINANCE_LEDGER_URL'),
  });
}

function detailOf(item: IntegrationSummary): IntegrationDetail {
  const [first] = item.instances;
  return {
    integration: item,
    ownerRoleId: null,
    maxAttempts: 3,
    retryBackoffSeconds: 60,
    operations: item.code === 'DIRECTORY' ? ['READ_PERSON', 'RECONCILE'] : [],
    metrics:
      first === undefined
        ? []
        : [
            {
              integrationInstanceId: first.id,
              from: '2026-10-13T09:00:00Z',
              to: '2026-10-14T09:00:00Z',
              attempts: item.code === 'SMS' ? 0 : 40,
              failedAttempts: item.code === 'SMS' ? 0 : 4,
              failureRate: item.code === 'SMS' ? null : 0.1,
              deliveredLineages: item.code === 'SMS' ? 0 : 35,
              undeliveredLineages: item.code === 'SMS' ? 0 : 1,
              deliveryRate: item.code === 'SMS' ? null : 0.9722,
              medianLatencyMilliseconds: item.code === 'SMS' ? null : 120,
              p95LatencyMilliseconds: item.code === 'SMS' ? null : 840,
              receipts: 0,
              medianReceiptLatencyMilliseconds: null,
            },
          ],
  };
}

function invocation(overrides: Partial<InvocationSummary>): InvocationSummary {
  return {
    id: FIRST_ATTEMPT_ID,
    integrationInstanceId: DIRECTORY_ID.replace('7a000000', '7b000000'),
    integrationCode: 'DIRECTORY',
    syncRunId: null,
    operationCode: 'RECONCILE',
    correlationId: CORRELATION_ID,
    idempotencyKey: 'reconcile-1',
    attemptNo: 1,
    sourceModule: 'IntegrationMonitoring',
    sourceReference: null,
    startedAt: '2026-10-14T08:00:00Z',
    completedAt: '2026-10-14T08:00:05Z',
    status: 'FAILED',
    failureCategory: 'TECHNICAL',
    responseCode: 'UNREACHABLE',
    errorSummary: 'SocketException',
    nextAttemptAt: null,
    ...overrides,
  };
}

/** A reconciliation retried to its dead letter (TASK-075 §7.3 check 1), and one observed sign-in call. */
export function seededInvocations(): InvocationSummary[] {
  return [
    invocation({ id: FIRST_ATTEMPT_ID, attemptNo: 1, startedAt: '2026-10-14T08:00:00Z' }),
    invocation({
      id: '7d000000-0000-4000-8000-000000000002',
      attemptNo: 2,
      startedAt: '2026-10-14T08:01:00Z',
    }),
    invocation({
      id: DEAD_LETTER_ID,
      attemptNo: 3,
      startedAt: '2026-10-14T08:03:00Z',
      status: 'DEAD_LETTER',
    }),
    invocation({
      id: SIGN_IN_CALL_ID,
      integrationInstanceId: SSO_ID.replace('7a000000', '7b000000'),
      integrationCode: 'SSO',
      operationCode: 'AUTHORIZE',
      correlationId: '7c000000-0000-4000-8000-000000000002',
      idempotencyKey: null,
      sourceModule: 'IdentityAccess',
      attemptNo: 1,
      startedAt: '2026-10-14T09:00:00Z',
      status: 'FAILED',
      responseCode: '[REDACTED]',
      errorSummary: 'HttpRequestException',
    }),
  ];
}

export function seededAlerts(): OperationalAlertView[] {
  const base = {
    integrationInstanceId: DIRECTORY_ID.replace('7a000000', '7b000000'),
    integrationCode: 'DIRECTORY',
    syncRunId: null,
    acknowledgedByUserId: null,
    acknowledgedAt: null,
    resolvedAt: null,
    resolutionNote: null,
  };
  return [
    {
      ...base,
      id: OPEN_ALERT_ID,
      invocationId: DEAD_LETTER_ID,
      alertType: 'DEAD_LETTER',
      severity: 'WARNING',
      raisedAt: '2026-10-14T08:03:01Z',
      status: 'OPEN',
      detail: 'RECONCILE dead-lettered after 3 attempts',
    },
    {
      ...base,
      id: ACKNOWLEDGED_ALERT_ID,
      invocationId: null,
      alertType: 'RECONCILIATION_MISMATCH',
      severity: 'WARNING',
      raisedAt: '2026-10-14T07:00:00Z',
      status: 'ACKNOWLEDGED',
      acknowledgedByUserId: '00000000-0000-4000-8000-000000000001',
      acknowledgedAt: '2026-10-14T07:30:00Z',
      detail: '3 people differ: 00000000-0000-4000-8000-0000000000c5 (jobTitle, manager)',
    },
  ];
}

export function seededSyncRuns(): SyncRunView[] {
  return [
    {
      id: '7f000000-0000-4000-8000-000000000001',
      integrationInstanceId: DIRECTORY_ID.replace('7a000000', '7b000000'),
      integrationCode: 'DIRECTORY',
      runType: 'RECONCILIATION',
      correlationId: CORRELATION_ID,
      startedAt: '2026-10-14T07:00:00Z',
      completedAt: '2026-10-14T07:00:02Z',
      status: 'SUCCEEDED',
      recordsRead: 7,
      recordsApplied: 0,
      recordsRejected: 0,
      mismatchCount: 3,
    },
  ];
}

/** The identity status as the API answers it, plus planted secret fields a regression could add. */
export function identityStatus(
  overrides: { nafathEnabled?: boolean } = {},
): IdentityIntegrationStatus {
  const planted = {
    bindPassword: PLANTED_SECRETS[0],
    clientSecret: PLANTED_SECRETS[1],
  };
  return {
    directory: {
      isConfigured: true,
      urlConfigured: true,
      bindDnConfigured: true,
      bindPasswordConfigured: true,
      usesTransportSecurity: true,
      usernameAttribute: 'uid',
      subjectAttribute: 'entryUUID',
      jobTitleAttribute: 'title',
      departmentAttribute: 'departmentNumber',
      managerAttribute: 'manager',
      ...planted,
    },
    singleSignOn: {
      isConfigured: true,
      authority: 'https://login.example.test/realm',
      clientId: 'pmplatform-spa',
      callbackUrl: 'https://pmplatform.example.test/sign-in/callback',
      clientSecretConfigured: false,
      subjectClaim: 'sub',
      scopes: ['openid', 'profile'],
      ...planted,
    },
    identityVerification: {
      isEnabled: overrides.nafathEnabled ?? false,
      isConfigured: false,
      authority: null,
      clientIdConfigured: false,
      clientSecretConfigured: false,
      callbackUrl: null,
      applicationId: null,
      scopes: [],
      ...planted,
    },
  };
}

/**
 * A connection test: the directory refuses the service account, SSO answers, Nafath is not configured. Each result
 * also carries a planted provider message, and `singleSignOnFailure` replaces SSO's code with a provider's sentence.
 */
export function connectionTest(
  overrides: { singleSignOnFailure?: string } = {},
): IdentityIntegrationTestResult {
  const providerMessage = `invalid credentials: password=${PLANTED_SECRETS[0]}`;
  return {
    directory: {
      outcome: 'FAILED',
      testedAt: '2026-10-14T09:10:00Z',
      failureCode: 'BIND_REJECTED',
      providerMessage,
    },
    singleSignOn:
      overrides.singleSignOnFailure === undefined
        ? { outcome: 'SUCCEEDED', testedAt: '2026-10-14T09:10:01Z', failureCode: null }
        : {
            outcome: 'FAILED',
            testedAt: '2026-10-14T09:10:01Z',
            failureCode: overrides.singleSignOnFailure,
          },
    identityVerification: {
      outcome: 'NOT_CONFIGURED',
      testedAt: '2026-10-14T09:10:02Z',
      failureCode: null,
      providerMessage,
    },
  } as IdentityIntegrationTestResult;
}

export interface IntegrationState {
  items?: IntegrationSummary[];
  invocations?: InvocationSummary[];
  alerts?: OperationalAlertView[];
  runs?: SyncRunView[];
  nafathEnabled?: boolean;
  test?: IdentityIntegrationTestResult;
  environment?: string;
}

const ETAG = (item: IntegrationSummary) => `"${item.status === 'ACTIVE' ? '4' : '5'}"`;

/** FG-05 and the identity-integration endpoints over `state`, which the writes change as the API would. */
export function withIntegrations(api: MockApi, state: IntegrationState = {}): MockApi {
  const items = state.items ?? seededCatalogue();
  const invocations = state.invocations ?? seededInvocations();
  const alerts = state.alerts ?? seededAlerts();
  const runs = state.runs ?? seededSyncRuns();
  const find = (id: string | undefined) => items.find((item) => item.id === id);
  const idOf = (path: string, index: number) => path.split('/')[index];

  return api
    .on('GET', /^\/integrations$/, {
      body: { environment: state.environment ?? 'Development', items },
    })
    .on('GET', /^\/integrations\/[^/]+$/, (request) => {
      const item = find(idOf(request.path, 2));
      return item === undefined
        ? problem(404, 'NOT_FOUND')
        : { body: detailOf(item), headers: { ETag: ETAG(item) } };
    })
    .on('POST', /^\/integrations\/[^/]+\/(enable|disable)$/, (request) => {
      const item = find(idOf(request.path, 2));
      if (item === undefined) {
        return problem(404, 'NOT_FOUND');
      }
      const ifMatch = request.headers.get('If-Match');
      if (ifMatch !== null && ifMatch !== ETAG(item)) {
        return problem(412, 'PRECONDITION_FAILED');
      }
      const enable = request.path.endsWith('/enable');
      if (item.status === 'RETIRED') {
        return problem(409, 'TERMINAL_STATE');
      }
      if ((item.status === 'ACTIVE') === enable) {
        return problem(409, 'INVALID_TRANSITION');
      }
      item.status = enable ? 'ACTIVE' : 'DISABLED';
      return { body: detailOf(item), headers: { ETag: ETAG(item) } };
    })
    .on('GET', /^\/integration-invocations$/, (request) => {
      const statuses = (request.query.get('status') ?? '').split(',').filter((s) => s !== '');
      const integrationId = request.query.get('integrationId');
      const code = integrationId === null ? null : (find(integrationId)?.code ?? '');
      const correlationId = request.query.get('correlationId');
      const found = invocations
        .filter(
          (candidate) =>
            (statuses.length === 0 || statuses.includes(candidate.status)) &&
            (code === null || candidate.integrationCode === code) &&
            (correlationId === null || candidate.correlationId === correlationId),
        )
        .sort((a, b) => b.startedAt.localeCompare(a.startedAt));
      return { body: page(found) };
    })
    .on('GET', /^\/integration-invocations\/[^/]+$/, (request) => {
      const attempt = invocations.find((candidate) => candidate.id === idOf(request.path, 2));
      if (attempt === undefined) {
        return problem(404, 'NOT_FOUND');
      }
      const lineage = invocations
        .filter(
          (candidate) =>
            candidate.integrationCode === attempt.integrationCode &&
            candidate.operationCode === attempt.operationCode &&
            candidate.idempotencyKey === attempt.idempotencyKey,
        )
        .sort((a, b) => a.attemptNo - b.attemptNo);
      const latest = lineage.at(-1);
      return {
        body: {
          invocation: attempt,
          lineage,
          retryPermitted:
            attempt.sourceModule === 'IntegrationMonitoring' &&
            latest?.id === attempt.id &&
            (attempt.status === 'DEAD_LETTER' || attempt.status === 'RETRY_SCHEDULED'),
        },
      };
    })
    .on('POST', /^\/integration-invocations\/[^/]+\/retry$/, (request) => {
      const attempt = invocations.find((candidate) => candidate.id === idOf(request.path, 2));
      if (attempt === undefined) {
        return problem(404, 'NOT_FOUND');
      }
      if (attempt.sourceModule !== 'IntegrationMonitoring') {
        return problem(422, 'RETRY_NOT_PERMITTED');
      }
      if (attempt.status !== 'DEAD_LETTER') {
        return problem(409, 'INVOCATION_NOT_RETRYABLE');
      }
      attempt.status = 'FAILED';
      const next = {
        ...attempt,
        id: RETRY_ATTEMPT_ID,
        attemptNo: attempt.attemptNo + 1,
        startedAt: '2026-10-14T09:30:00Z',
        status: 'DEAD_LETTER' as const,
      };
      invocations.push(next);
      return { body: { invocation: next, lineage: [attempt, next], retryPermitted: true } };
    })
    .on('POST', /^\/integration-reconciliations$/, (request) => {
      const { integrationId } = request.body as { integrationId: string };
      const item = find(integrationId);
      if (item === undefined) {
        return problem(404, 'NOT_FOUND');
      }
      if (item.code !== 'DIRECTORY') {
        return problem(422, 'RECONCILIATION_NOT_AVAILABLE');
      }
      const started = invocation({
        id: '7d000000-0000-4000-8000-000000000010',
        idempotencyKey: 'reconcile-2',
        correlationId: '7c000000-0000-4000-8000-000000000010',
        status: 'SUCCEEDED',
        failureCategory: null,
        responseCode: null,
        errorSummary: null,
      });
      invocations.push(started);
      return {
        status: 201,
        body: { invocation: started, lineage: [started], retryPermitted: false },
      };
    })
    .on('GET', /^\/sync-runs$/, (request) => {
      const integrationId = request.query.get('integrationId');
      const code = integrationId === null ? null : find(integrationId)?.code;
      return { body: page(runs.filter((run) => code === null || run.integrationCode === code)) };
    })
    .on('GET', /^\/operational-alerts$/, (request) => {
      const statuses = (request.query.get('status') ?? '').split(',').filter((s) => s !== '');
      const integrationId = request.query.get('integrationId');
      const code = integrationId === null ? null : find(integrationId)?.code;
      return {
        body: page(
          alerts.filter(
            (alert) =>
              (statuses.length === 0 || statuses.includes(alert.status)) &&
              (code === null || alert.integrationCode === code),
          ),
        ),
      };
    })
    .on('POST', /^\/operational-alerts\/[^/]+\/acknowledge$/, (request) => {
      const alert = alerts.find((candidate) => candidate.id === idOf(request.path, 2));
      if (alert === undefined) {
        return problem(404, 'NOT_FOUND');
      }
      if (alert.status !== 'OPEN') {
        return problem(409, 'INVALID_TRANSITION');
      }
      alert.status = 'ACKNOWLEDGED';
      alert.acknowledgedAt = '2026-10-14T09:20:00Z';
      return { body: alert };
    })
    .on('POST', /^\/operational-alerts\/[^/]+\/(resolve|dismiss)$/, (request) => {
      const alert = alerts.find((candidate) => candidate.id === idOf(request.path, 2));
      if (alert === undefined) {
        return problem(404, 'NOT_FOUND');
      }
      const note = (request.body as { note?: { text?: string; language?: 'AR' | 'EN' } }).note;
      if (note?.text === undefined || note.text.trim() === '' || note.language === undefined) {
        return problem(400, 'VALIDATION_FAILED', [{ field: 'note.text', code: 'REQUIRED' }]);
      }
      if (alert.status === 'RESOLVED' || alert.status === 'DISMISSED') {
        return problem(409, 'TERMINAL_STATE');
      }
      alert.status = request.path.endsWith('/resolve') ? 'RESOLVED' : 'DISMISSED';
      alert.resolvedAt = '2026-10-14T09:25:00Z';
      alert.resolutionNote = { text: note.text, language: note.language };
      return { body: alert };
    })
    .on('GET', /^\/identity-integration$/, {
      body: identityStatus({ nafathEnabled: state.nafathEnabled ?? false }),
    })
    .on('POST', /^\/identity-integration\/test$/, { body: state.test ?? connectionTest() });
}

// FG-05's wire contracts as TASK-075 built them (integration-monitoring.md §5; docs/api/openapi.v1.json, tag
// IntegrationMonitoring), and ADM-041's identity-integration status and connection test (TASK-028 D-10, TASK-068
// D-11). No contract carries a secret, an endpoint value or a payload: an endpoint is named by its configuration key,
// and a secret is reduced to whether it is set.

export type IntegrationDirection = 'INBOUND' | 'OUTBOUND' | 'BIDIRECTIONAL';
export type IntegrationKind =
  'IDENTITY' | 'MESSAGING' | 'VERIFICATION' | 'FINANCE' | 'SECURITY' | 'DATA';
/** Activation (MOD-101), never health: a DISABLED integration is not a failure (FG-05 §5.2). */
export type IntegrationDefinitionStatus = 'ACTIVE' | 'DISABLED' | 'RETIRED';
/** Reachability: the latest technical outcome (TASK-075 D-5). */
export type IntegrationAvailability = 'UP' | 'DOWN' | 'UNKNOWN';
/** Currency: the last success against a configured window; UNKNOWN without one (TASK-075 D-5). */
export type IntegrationFreshness = 'FRESH' | 'STALE' | 'UNKNOWN';

export interface BilingualLabel {
  ar: string;
  en: string;
}

export interface IntegrationInstanceView {
  id: string;
  code: string;
  /** The name of the configuration entry holding the endpoint (`AD_LDAP_URL`), never its value. */
  endpointConfigurationKey: string;
  isEnabled: boolean;
  availability: IntegrationAvailability;
  freshness: IntegrationFreshness;
  lastSuccessAt: string | null;
  lastFailureAt: string | null;
  evaluatedAt: string | null;
  openAlertCount: number;
}

export interface IntegrationSummary {
  id: string;
  code: string;
  name: BilingualLabel;
  direction: IntegrationDirection;
  integrationKind: IntegrationKind;
  status: IntegrationDefinitionStatus;
  instances: IntegrationInstanceView[];
}

export interface IntegrationCatalogueView {
  /** The environment the catalogue describes (TASK-075 F-9: the host's environment name). */
  environment: string;
  items: IntegrationSummary[];
}

/** ADR-004's measures for one instance over a window; a measure over no sample is null, never 0 (D-13). */
export interface IntegrationMetrics {
  integrationInstanceId: string;
  from: string;
  to: string;
  attempts: number;
  failedAttempts: number;
  failureRate: number | null;
  deliveredLineages: number;
  undeliveredLineages: number;
  deliveryRate: number | null;
  medianLatencyMilliseconds: number | null;
  p95LatencyMilliseconds: number | null;
  receipts: number;
  medianReceiptLatencyMilliseconds: number | null;
}

export interface IntegrationDetail {
  integration: IntegrationSummary;
  ownerRoleId: string | null;
  maxAttempts: number;
  retryBackoffSeconds: number;
  /** The operations FG-05 itself runs for this integration (`RECONCILE` for the directory). */
  operations: string[];
  metrics: IntegrationMetrics[];
}

export type InvocationStatus =
  'IN_PROGRESS' | 'SUCCEEDED' | 'FAILED' | 'RETRY_SCHEDULED' | 'DEAD_LETTER';
export type InvocationFailureCategory = 'TECHNICAL' | 'BUSINESS_VALIDATION' | 'SECURITY';

export interface InvocationSummary {
  id: string;
  integrationInstanceId: string;
  integrationCode: string;
  syncRunId: string | null;
  operationCode: string;
  correlationId: string;
  idempotencyKey: string | null;
  attemptNo: number;
  /** The module that made the attempt; `IntegrationMonitoring` when FG-05 ran it (TASK-075 D-2). */
  sourceModule: string | null;
  sourceReference: string | null;
  startedAt: string;
  completedAt: string | null;
  status: InvocationStatus;
  failureCategory: InvocationFailureCategory | null;
  /** A plain code, or `[REDACTED]`: the API replaces anything else before storing it (D-11). */
  responseCode: string | null;
  errorSummary: string | null;
  nextAttemptAt: string | null;
}

export interface InvocationDetail {
  invocation: InvocationSummary;
  /** Every attempt of the lineage, oldest first. */
  lineage: InvocationSummary[];
  /** Whether FG-05 would retry the lineage now: the backend decides (FG-05 §20.1). */
  retryPermitted: boolean;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export type InvocationPage = Paged<InvocationSummary>;

export interface InvocationQuery {
  integrationId?: string | undefined;
  statuses?: readonly InvocationStatus[];
  operationCode?: string | undefined;
  correlationId?: string | undefined;
  idempotencyKey?: string | undefined;
  sourceReference?: string | undefined;
  startedFrom?: string | undefined;
  startedTo?: string | undefined;
}

export type SyncRunType = 'DIRECTORY_SYNC' | 'RECONCILIATION';
export type SyncRunStatus = 'RUNNING' | 'SUCCEEDED' | 'PARTIAL' | 'FAILED';

export interface SyncRunView {
  id: string;
  integrationInstanceId: string;
  integrationCode: string;
  runType: SyncRunType;
  correlationId: string;
  startedAt: string;
  completedAt: string | null;
  status: SyncRunStatus;
  recordsRead: number;
  recordsApplied: number;
  recordsRejected: number;
  mismatchCount: number;
}

export type SyncRunPage = Paged<SyncRunView>;

export type OperationalAlertType =
  'AVAILABILITY' | 'FRESHNESS' | 'RECONCILIATION_MISMATCH' | 'DEAD_LETTER' | 'DELIVERY_RATE';
export type OperationalAlertSeverity = 'INFO' | 'WARNING' | 'CRITICAL';
export type OperationalAlertStatus = 'OPEN' | 'ACKNOWLEDGED' | 'RESOLVED' | 'DISMISSED';

export interface NarrativeText {
  text: string;
  language: 'AR' | 'EN';
}

export interface OperationalAlertView {
  id: string;
  integrationInstanceId: string;
  integrationCode: string;
  syncRunId: string | null;
  invocationId: string | null;
  alertType: OperationalAlertType;
  severity: OperationalAlertSeverity;
  raisedAt: string;
  status: OperationalAlertStatus;
  acknowledgedByUserId: string | null;
  acknowledgedAt: string | null;
  resolvedAt: string | null;
  resolutionNote: NarrativeText | null;
  /** What the condition is about, naming people by id and attributes by name, never a value (D-8). */
  detail: string | null;
}

export type OperationalAlertPage = Paged<OperationalAlertView>;

export interface OperationalAlertQuery {
  integrationId?: string | undefined;
  statuses?: readonly OperationalAlertStatus[];
}

/** ADM-041: which directory settings are set — a secret only as whether it is set — and the attribute mapping. */
export interface DirectoryIntegrationStatus {
  isConfigured: boolean;
  urlConfigured: boolean;
  bindDnConfigured: boolean;
  bindPasswordConfigured: boolean;
  usesTransportSecurity: boolean;
  usernameAttribute: string;
  subjectAttribute: string;
  jobTitleAttribute: string;
  departmentAttribute: string;
  managerAttribute: string;
}

export interface SingleSignOnIntegrationStatus {
  isConfigured: boolean;
  authority: string | null;
  clientId: string | null;
  callbackUrl: string | null;
  clientSecretConfigured: boolean;
  subjectClaim: string;
  scopes: string[];
}

export interface IdentityVerificationIntegrationStatus {
  /** `Identity:Nafath:Enabled`: whether Nafath is part of sign-in in this environment (TASK-068). */
  isEnabled: boolean;
  isConfigured: boolean;
  authority: string | null;
  clientIdConfigured: boolean;
  clientSecretConfigured: boolean;
  callbackUrl: string | null;
  applicationId: string | null;
  scopes: string[];
}

export interface IdentityIntegrationStatus {
  directory: DirectoryIntegrationStatus;
  singleSignOn: SingleSignOnIntegrationStatus;
  identityVerification: IdentityVerificationIntegrationStatus;
}

export type ConnectionTestOutcome = 'SUCCEEDED' | 'FAILED' | 'NOT_CONFIGURED';

export interface ConnectionTestResult {
  outcome: ConnectionTestOutcome;
  testedAt: string;
  /** A fixed code (`UNREACHABLE`, `BIND_REJECTED`, `ISSUER_MISMATCH`), never the provider's message. */
  failureCode: string | null;
}

/** MOD-100: one test of each identity connection with the configured values. */
export interface IdentityIntegrationTestResult {
  directory: ConnectionTestResult;
  singleSignOn: ConnectionTestResult;
  identityVerification: ConnectionTestResult;
}

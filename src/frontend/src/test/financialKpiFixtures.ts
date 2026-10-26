import {
  type FinancialCommitmentDetail,
  type FinancialPositionDetail,
  type FinancialProgressUpdateDetail,
  type FinancialSourceModeDetail,
  type KpiAssignmentDetail,
  type KpiDefinitionSummary,
  type KpiMeasurementDetail,
  type KpiPortfolioAggregate,
  type KpiTargetVersionDetail,
  type PublishedFinancialSnapshotDetail,
} from '@/features/financial-kpi/api/types.ts';
import { monthOf } from '@/features/financial-kpi/financialKpiRules.ts';
import { todayUtc } from '@/features/tasks/taskRules.ts';
import { type MasterDataItemSummary } from '@/shared/api/masterData.ts';

import { type MockApi, page, problem } from './mockApi.ts';
import { CYCLE_ID, EARLIER_CYCLE_ID } from './progressFixtures.ts';
import { CATALOGUES, ENTITY_USER_ID, ITEMS, PROJECT_ID, REVIEWER_ID } from './projectFixtures.ts';

// Test data only: fixed ids, each starting differently so a shortened id is recognisable. KPI periods are calendar
// months relative to today, so "this period" holds whenever the suite runs.
//
// The Coastal road upgrade (PROJECT_ID) is managed by the entity's user, Huda. Its finances: an Approved Budget of
// SAR 1,000,000.00 (version 1, manual, "Board minute 14", as of 2026-08-15); August published as STALE, so the official
// actual and forecast are Unknown; September's update a DRAFT, MISSING, every figure null.
// Its KPI "On-time delivery" (percent, monthly): target version 1 (95) superseded by version 2 (80). Three months ago:
// 92 against version 1, AMBER, published. Two months ago: MISSING against version 1, published. Last month: 92 against
// version 2, GREEN, submitted by Huda. This month: nothing recorded.

export const COMMITMENT_ID = 'f1000000-0000-4000-8000-000000000d01';
export const UPDATE_ID = 'f2000000-0000-4000-8000-000000000d02';
export const PUBLISHED_UPDATE_ID = 'f2000000-0000-4000-8000-000000000d03';
export const SNAPSHOT_ID = 'f3000000-0000-4000-8000-000000000d04';
export const THRESHOLDS_ID = 'f4000000-0000-4000-8000-000000000d05';
export const ASSIGNMENT_ID = 'f5000000-0000-4000-8000-000000000e01';
export const KPI_DEFINITION_ID = 'f6000000-0000-4000-8000-000000000e02';
export const TARGET_V1_ID = 'f7000000-0000-4000-8000-000000000e03';
export const TARGET_V2_ID = 'f7000000-0000-4000-8000-000000000e04';
export const MEASUREMENT_1_ID = 'f8000000-0000-4000-8000-000000000e05';
export const MEASUREMENT_2_ID = 'f8000000-0000-4000-8000-000000000e06';
export const MEASUREMENT_3_ID = 'f8000000-0000-4000-8000-000000000e07';
export const PERCENT_UNIT_ID = 'f9000000-0000-4000-8000-000000000e08';
export const DAYS_UNIT_ID = 'f9000000-0000-4000-8000-000000000e09';
export const MONTHLY_ID = 'fa000000-0000-4000-8000-000000000e10';
export const KPI_UNIT_CATALOGUE_ID = 'c0000000-0000-4000-8000-000000000061';
export const FREQUENCY_CATALOGUE_ID = 'c0000000-0000-4000-8000-000000000062';
export const UPDATE_ETAG = '"71"';
export const MEASUREMENT_ETAG = '"81"';

const AT = '2026-09-01T09:00:00Z';

/** The calendar month `months` before the one today falls in. */
export function monthBefore(months: number): { start: string; end: string } {
  const [year = 2026, month = 1] = todayUtc().split('-').map(Number);
  const shifted = new Date(Date.UTC(year, month - 1 - months, 1)).toISOString().slice(0, 10);
  return monthOf(shifted);
}

export function commitment(
  overrides: Partial<FinancialCommitmentDetail> = {},
): FinancialCommitmentDetail {
  return {
    id: COMMITMENT_ID,
    projectId: PROJECT_ID,
    commitmentType: 'APPROVED_BUDGET',
    versionNo: 1,
    revisionNo: 1,
    status: 'ACTIVE',
    isCurrent: true,
    amountSar: '1000000.00',
    effectiveFrom: '2026-08-01',
    activatedAt: '2026-08-16T10:00:00Z',
    supersededByCommitmentId: null,
    sourceType: 'MANUAL',
    sourceReference: 'Board minute 14',
    asOfDate: '2026-08-15',
    enteredByUserId: ENTITY_USER_ID,
    createdAt: AT,
    createdBy: ENTITY_USER_ID,
    maskedFields: [],
    ...overrides,
  };
}

/** September's DRAFT as the API opens it: MISSING, every figure null — nothing presumed. */
export function financialUpdate(
  overrides: Partial<FinancialProgressUpdateDetail> = {},
): FinancialProgressUpdateDetail {
  return {
    id: UPDATE_ID,
    projectId: PROJECT_ID,
    reportingCycleId: CYCLE_ID,
    revisionNo: 1,
    status: 'DRAFT',
    actualExpenditureToDateSar: null,
    forecastAtCompletionSar: null,
    valueStatus: 'MISSING',
    narrative: null,
    projectIntakeId: null,
    sourceType: 'MANUAL',
    sourceReference: null,
    asOfDate: '2026-09-30',
    enteredByUserId: ENTITY_USER_ID,
    submittedByUserId: null,
    submittedAt: null,
    reviewedByUserId: null,
    reviewedAt: null,
    returnReason: null,
    createdAt: AT,
    updatedAt: AT,
    maskedFields: [],
    ...overrides,
  };
}

/** August as published: STALE, so the actual and the forecast are null and the status UNKNOWN. */
export function financialSnapshot(
  overrides: Partial<PublishedFinancialSnapshotDetail> = {},
): PublishedFinancialSnapshotDetail {
  return {
    id: SNAPSHOT_ID,
    projectId: PROJECT_ID,
    reportingCycleId: EARLIER_CYCLE_ID,
    financialProgressUpdateId: PUBLISHED_UPDATE_ID,
    financialCommitmentId: COMMITMENT_ID,
    semanticState: 'PUBLISHED_OFFICIAL',
    publishedAt: '2026-09-05T10:00:00Z',
    publishedByUserId: REVIEWER_ID,
    approvedBudgetSar: '1000000.00',
    actualExpenditureToDateSar: null,
    forecastAtCompletionSar: null,
    valueStatus: 'STALE',
    financialStatus: 'UNKNOWN',
    thresholdConfigurationVersionId: THRESHOLDS_ID,
    sourceType: 'MANUAL',
    sourceReference: 'Finance return 08',
    asOfDate: '2026-08-31',
    enteredByUserId: ENTITY_USER_ID,
    maskedFields: [],
    ...overrides,
  };
}

/** The live position: the budget beside September's DRAFT, which has no figures yet. */
export function financialPosition(
  overrides: Partial<FinancialPositionDetail> = {},
): FinancialPositionDetail {
  return {
    projectId: PROJECT_ID,
    semanticState: 'CURRENT_LIVE',
    financialCommitmentId: COMMITMENT_ID,
    approvedBudgetSar: '1000000.00',
    financialProgressUpdateId: UPDATE_ID,
    updateStatus: 'DRAFT',
    actualExpenditureToDateSar: null,
    forecastAtCompletionSar: null,
    valueStatus: 'MISSING',
    financialStatus: 'UNKNOWN',
    thresholdConfigurationVersionId: null,
    asOfDate: '2026-09-30',
    computedAt: '2026-10-01T09:00:00Z',
    maskedFields: [],
    ...overrides,
  };
}

/** Every field MANUAL, as the API answers a project that set none; OPEN_COMMITMENT listed too, as the API does. */
export function sourceModes(
  overrides: Partial<
    Record<FinancialSourceModeDetail['fieldCode'], FinancialSourceModeDetail['sourceMode']>
  > = {},
): FinancialSourceModeDetail[] {
  return (
    ['APPROVED_BUDGET', 'ACTUAL_EXPENDITURE', 'FORECAST_AT_COMPLETION', 'OPEN_COMMITMENT'] as const
  ).map((fieldCode) => ({
    id: null,
    projectId: PROJECT_ID,
    fieldCode,
    sourceMode: overrides[fieldCode] ?? 'MANUAL',
    configuredAt: null,
  }));
}

interface FinancialState {
  snapshots?: PublishedFinancialSnapshotDetail[];
  positions?: FinancialPositionDetail[];
  commitments?: FinancialCommitmentDetail[];
  updates?: FinancialProgressUpdateDetail[];
  modes?: FinancialSourceModeDetail[];
  /** FINANCIAL_VIEW; false is 403. */
  readable?: boolean;
}

/** The WF-14 financial reads of one project. */
export function withFinancials(
  api: MockApi,
  {
    snapshots = [financialSnapshot()],
    positions = [financialPosition()],
    commitments = [commitment()],
    updates = [financialUpdate()],
    modes = sourceModes(),
    readable = true,
  }: FinancialState = {},
): MockApi {
  const forbidden = problem(403, 'PERMISSION_DENIED');
  const answer = (items: unknown[]) => (readable ? { body: page(items, 200) } : forbidden);
  api
    .on('GET', /^\/published-financial-snapshots$/, answer(snapshots))
    .on('GET', /^\/financial-positions$/, answer(positions))
    .on('GET', /^\/financial-commitments$/, answer(commitments))
    .on('GET', /^\/financial-progress-updates$/, answer(updates))
    .on('GET', /^\/financial-source-modes$/, answer(modes))
    .on('GET', /^\/financial-progress-updates\/[^/]+$/, (request) => {
      const found = updates.find((update) => request.path.endsWith(update.id));
      return found === undefined
        ? problem(404, 'NOT_FOUND')
        : { body: found, headers: { ETag: UPDATE_ETAG } };
    });
  return api;
}

export function kpiAssignment(overrides: Partial<KpiAssignmentDetail> = {}): KpiAssignmentDetail {
  return {
    id: ASSIGNMENT_ID,
    projectId: PROJECT_ID,
    kpiDefinitionId: KPI_DEFINITION_ID,
    unitItemId: PERCENT_UNIT_ID,
    ownerUserId: ENTITY_USER_ID,
    measurementFrequencyItemId: MONTHLY_ID,
    status: 'ACTIVE',
    assignedAt: AT,
    ...overrides,
  };
}

/** Version 2 (80) in force; version 1 (95) superseded by it, every other field as approved. Newest first. */
export function targetVersions(): KpiTargetVersionDetail[] {
  return [
    {
      id: TARGET_V2_ID,
      kpiAssignmentId: ASSIGNMENT_ID,
      versionNo: 2,
      revisionNo: 1,
      status: 'ACTIVE',
      isCurrent: true,
      targetValue: '80.0000',
      greenThreshold: '80.0000',
      amberThreshold: '75.0000',
      effectiveFrom: monthBefore(1).start,
      activatedAt: `${monthBefore(1).start}T08:00:00Z`,
      supersededByTargetVersionId: null,
    },
    {
      id: TARGET_V1_ID,
      kpiAssignmentId: ASSIGNMENT_ID,
      versionNo: 1,
      revisionNo: 1,
      status: 'SUPERSEDED',
      isCurrent: false,
      targetValue: '95.0000',
      greenThreshold: '95.0000',
      amberThreshold: '90.0000',
      effectiveFrom: monthBefore(3).start,
      activatedAt: `${monthBefore(3).start}T08:00:00Z`,
      supersededByTargetVersionId: TARGET_V2_ID,
    },
  ];
}

export function measurement(overrides: Partial<KpiMeasurementDetail> = {}): KpiMeasurementDetail {
  const period = monthBefore(3);
  return {
    id: MEASUREMENT_1_ID,
    kpiAssignmentId: ASSIGNMENT_ID,
    projectId: PROJECT_ID,
    kpiTargetVersionId: TARGET_V1_ID,
    targetVersionNo: 1,
    targetValue: '95.0000',
    periodStart: period.start,
    periodEnd: period.end,
    measuredValue: '92.0000',
    valueStatus: 'MEASURED',
    ragStatus: 'AMBER',
    asOfDate: period.end,
    recordedByUserId: ENTITY_USER_ID,
    narrative: null,
    status: 'PUBLISHED',
    submittedAt: AT,
    publishedByUserId: REVIEWER_ID,
    publishedAt: AT,
    createdAt: AT,
    updatedAt: AT,
    maskedFields: [],
    ...overrides,
  };
}

/** Latest period first, as the API lists them; nothing for the month today falls in. */
export function measurements(): KpiMeasurementDetail[] {
  const last = monthBefore(1);
  const earlier = monthBefore(2);
  return [
    measurement({
      id: MEASUREMENT_3_ID,
      kpiTargetVersionId: TARGET_V2_ID,
      targetVersionNo: 2,
      targetValue: '80.0000',
      periodStart: last.start,
      periodEnd: last.end,
      asOfDate: last.end,
      ragStatus: 'GREEN',
      status: 'SUBMITTED',
      publishedByUserId: null,
      publishedAt: null,
    }),
    measurement({
      id: MEASUREMENT_2_ID,
      periodStart: earlier.start,
      periodEnd: earlier.end,
      asOfDate: earlier.end,
      measuredValue: null,
      valueStatus: 'MISSING',
      ragStatus: 'UNKNOWN',
    }),
    measurement(),
  ];
}

export const KPI_DEFINITIONS: KpiDefinitionSummary[] = [
  {
    id: KPI_DEFINITION_ID,
    code: 'ON_TIME',
    name: { en: 'On-time delivery', ar: 'التسليم في الموعد' },
    unitItemId: PERCENT_UNIT_ID,
    direction: 'HIGHER_IS_BETTER',
    lifecycleState: 'PUBLISHED',
  },
];

function kpiItem(id: string, catalogueId: string, en: string, ar: string): MasterDataItemSummary {
  return {
    id,
    catalogueId,
    code: en.toUpperCase(),
    label: { en, ar },
    parentItemId: null,
    sortOrder: 1,
    lifecycleState: 'PUBLISHED',
    isSystem: false,
  };
}

const KPI_ITEMS: Record<string, MasterDataItemSummary[]> = {
  [KPI_UNIT_CATALOGUE_ID]: [
    kpiItem(PERCENT_UNIT_ID, KPI_UNIT_CATALOGUE_ID, 'Percent', 'نسبة مئوية'),
    kpiItem(DAYS_UNIT_ID, KPI_UNIT_CATALOGUE_ID, 'Days', 'أيام'),
  ],
  [FREQUENCY_CATALOGUE_ID]: [kpiItem(MONTHLY_ID, FREQUENCY_CATALOGUE_ID, 'Monthly', 'شهري')],
};

interface KpiState {
  assignments?: KpiAssignmentDetail[];
  targets?: KpiTargetVersionDetail[];
  measurements?: KpiMeasurementDetail[];
  aggregate?: KpiPortfolioAggregate;
  /** MASTER_DATA_VIEW for the KPI catalogue and its units; false is 403, as today for everyone but R01. */
  catalogue?: boolean;
}

/** The WF-14 KPI reads. Layer over withProjectLookups: the catalogues answer the project's and the KPI's. */
export function withKpis(
  api: MockApi,
  {
    assignments = [kpiAssignment()],
    targets = targetVersions(),
    measurements: values = measurements(),
    aggregate,
    catalogue = true,
  }: KpiState = {},
): MockApi {
  const forbidden = problem(403, 'PERMISSION_DENIED');
  const ofAssignment = <T extends { kpiAssignmentId: string }>(
    items: T[],
    query: URLSearchParams,
  ) => items.filter((item) => item.kpiAssignmentId === query.get('kpiAssignmentId'));
  api
    .on('GET', /^\/kpi-assignments$/, (request) => ({
      body: page(
        assignments.filter((item) => item.projectId === request.query.get('projectId')),
        200,
      ),
    }))
    .on('GET', /^\/kpi-assignments\/[^/]+$/, (request) => {
      const found = assignments.find((item) => request.path.endsWith(item.id));
      return found === undefined ? problem(404, 'NOT_FOUND') : { body: found };
    })
    .on('GET', /^\/kpi-target-versions$/, (request) => ({
      body: page(ofAssignment(targets, request.query), 200),
    }))
    .on('GET', /^\/kpi-measurements$/, (request) => ({
      body: page(ofAssignment(values, request.query), 200),
    }))
    .on('GET', /^\/kpi-measurements\/[^/]+$/, (request) => {
      const found = values.find((item) => request.path.endsWith(item.id));
      return found === undefined
        ? problem(404, 'NOT_FOUND')
        : { body: found, headers: { ETag: MEASUREMENT_ETAG } };
    })
    .on('GET', /^\/kpi-definitions$/, catalogue ? { body: page(KPI_DEFINITIONS, 200) } : forbidden)
    .on(
      'GET',
      /^\/master-data-catalogues$/,
      catalogue
        ? {
            body: [
              ...CATALOGUES,
              { id: KPI_UNIT_CATALOGUE_ID, code: 'KPI_UNIT' },
              { id: FREQUENCY_CATALOGUE_ID, code: 'MEASUREMENT_FREQUENCY' },
            ],
          }
        : forbidden,
    )
    .on('GET', /^\/master-data-items$/, (request) => {
      const id = request.query.get('catalogueId') ?? '';
      return { body: page(KPI_ITEMS[id] ?? ITEMS[id] ?? [], 200) };
    });
  if (aggregate !== undefined) {
    api.on('GET', /^\/kpi-portfolio-aggregates$/, { body: aggregate });
  }
  return api;
}

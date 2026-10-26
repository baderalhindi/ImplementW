import {
  type ReportAllowlistEntry,
  type ReportCatalogueEntry,
  type ReportCell,
  type ReportColumnView,
  type ReportJobDetail,
  type ReportJobSummary,
  type ReportResultPage,
  type ReportView,
  type SavedViewDetail,
  type SavedViewSummary,
} from '@/features/reports/api/types.ts';
import { type Session } from '@/features/identity-access/session/sessionApi.ts';

import { sessionFor } from './identityAccessFixtures.ts';
import { type MockApi, type MockReply, page, problem } from './mockApi.ts';

// FG-02 as TASK-071 (and TASK-072's sharing) answer it, for the reports UI tests. The catalogue follows the seed: ten
// reports, each presented by one of the seven experiences. The allowlist stands for a PUBLISHED REPORT_RULES version.

export const OWNER_ID = '00000000-0000-4000-8000-0000000000b2';
export const COLLEAGUE_ID = '00000000-0000-4000-8000-0000000000b3';
export const PROJECT_A = '00000000-0000-4000-8000-0000000000a1';
export const PROJECT_B = '00000000-0000-4000-8000-0000000000a2';
export const DEPARTMENT_ID = '00000000-0000-4000-8000-0000000000d1';

/** A Portfolio Manager (R02) — one of ADR-019's composers — signed in as OWNER_ID. */
export function composerSession(roleCode = 'R02'): Session {
  const session = sessionFor([roleCode]);
  return {
    ...session,
    user: { ...session.user, id: OWNER_ID, displayName: 'Noura Portfolio' },
  };
}

const label = (en: string, ar: string) => ({ en, ar });

function entry(
  code: ReportCatalogueEntry['code'],
  family: ReportCatalogueEntry['audienceFamily'],
  en: string,
  ar: string,
): ReportCatalogueEntry {
  return {
    code,
    reportDefinitionId: `def-${code}`,
    versionNo: 1,
    name: label(en, ar),
    description: label(`${en}: what it shows.`, `${ar}: ما يعرضه.`),
    audienceFamily: family,
    allowsSavedViews: true,
  };
}

export const CATALOGUE: ReportCatalogueEntry[] = [
  entry('GOVERNANCE_CHANGE', 'EXECUTIVE', 'Governance and Change', 'الحوكمة والتغيير'),
  entry('PORTFOLIO_SUMMARY', 'PORTFOLIO', 'Portfolio Summary', 'ملخص المحفظة'),
  entry('PROJECT_REGISTER', 'DEPARTMENT', 'Project Register', 'سجل المشاريع'),
  entry('PROJECT_REPORT', 'PROJECT', 'Project Report', 'تقرير المشروع'),
  entry('SCHEDULE_DELIVERY', 'PROJECT', 'Schedule and Delivery', 'الجدول الزمني والتسليم'),
  entry('FINANCIAL_PERFORMANCE', 'FINANCIAL', 'Financial Performance', 'الأداء المالي'),
  entry('KPI_PERFORMANCE', 'FINANCIAL', 'KPI Performance', 'أداء مؤشرات الأداء'),
  entry('PROGRESS_REPORTING', 'PROGRESS', 'Progress Reporting Status', 'حالة تقارير التقدم'),
  entry('PROGRESS_HISTORY', 'PROGRESS', 'Published Progress History', 'سجل التقدم المنشور'),
  entry('RISK_ISSUE', 'RISK_ISSUE', 'Risks and Issues', 'المخاطر والقضايا'),
];

function column(
  sourceEntityCode: string,
  fieldCode: string,
  en: string,
  ar: string,
  valueType: ReportColumnView['valueType'],
  extra: Partial<ReportColumnView> = {},
): ReportColumnView {
  return {
    sourceEntityCode,
    fieldCode,
    label: label(en, ar),
    valueType,
    projectionCode: null,
    semanticState: null,
    isDefaultVisible: true,
    isSensitive: false,
    ...extra,
  };
}

/** Financial Performance: identity columns, the live financial condition and two SAR amounts (one hidden by default). */
export const FINANCIAL_COLUMNS: ReportColumnView[] = [
  column('PROJECT', 'FORMAL_PROJECT_ID', 'Project ID', 'رقم المشروع', 'TEXT'),
  column('PROJECT', 'TITLE', 'Project', 'اسم المشروع', 'TEXT'),
  column('PROJECT', 'DEPARTMENT', 'Sector', 'القطاع', 'REFERENCE'),
  column(
    'FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT',
    'FINANCIAL_STATUS',
    'Published financial condition',
    'الحالة المالية المنشورة',
    'CODE',
    {
      projectionCode: 'FINANCIAL_KPI.PUBLISHED_FINANCIAL_SNAPSHOT',
      semanticState: 'PUBLISHED_OFFICIAL',
    },
  ),
  column(
    'FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT',
    'APPROVED_BUDGET',
    'Approved budget',
    'الميزانية المعتمدة',
    'SAR',
    {
      projectionCode: 'FINANCIAL_KPI.PUBLISHED_FINANCIAL_SNAPSHOT',
      semanticState: 'PUBLISHED_OFFICIAL',
      isSensitive: true,
    },
  ),
  column(
    'FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT',
    'ACTUAL_EXPENDITURE_TO_DATE',
    'Actual expenditure',
    'الصرف الفعلي',
    'SAR',
    {
      projectionCode: 'FINANCIAL_KPI.PUBLISHED_FINANCIAL_SNAPSHOT',
      semanticState: 'PUBLISHED_OFFICIAL',
      isSensitive: true,
      isDefaultVisible: false,
    },
  ),
];

export function reportView(overrides: Partial<ReportView> = {}): ReportView {
  return {
    code: 'FINANCIAL_PERFORMANCE',
    reportDefinitionId: 'def-FINANCIAL_PERFORMANCE',
    versionNo: 1,
    name: label('Financial Performance', 'الأداء المالي'),
    description: label('The financial position in SAR.', 'الوضع المالي بالريال.'),
    audienceFamily: 'FINANCIAL',
    allowsSavedViews: true,
    columns: FINANCIAL_COLUMNS,
    parameters: [
      {
        code: 'DEPARTMENT',
        label: label('Sector', 'القطاع'),
        dataType: 'DEPARTMENT',
        isRequired: false,
        options: [
          {
            value: DEPARTMENT_ID,
            label: label('Strategy Department', 'إدارة الاستراتيجية'),
            catalogueEntryReference: null,
          },
        ],
      },
      {
        code: 'FINANCIAL_CONDITION',
        label: label('Financial condition', 'الحالة المالية'),
        dataType: 'OPTION',
        isRequired: false,
        options: [
          { value: 'RED', label: label('Red', 'أحمر'), catalogueEntryReference: null },
          { value: 'AMBER', label: label('Amber', 'كهرماني'), catalogueEntryReference: null },
        ],
      },
    ],
    exportFormats: ['PDF', 'XLSX', 'CSV'],
    mayExport: true,
    maySaveView: true,
    ...overrides,
  };
}

/** The Project Report: one project, required. */
export function projectReportView(): ReportView {
  return reportView({
    code: 'PROJECT_REPORT',
    reportDefinitionId: 'def-PROJECT_REPORT',
    name: label('Project Report', 'تقرير المشروع'),
    audienceFamily: 'PROJECT',
    columns: FINANCIAL_COLUMNS.slice(0, 2),
    parameters: [
      {
        code: 'PROJECT',
        label: label('Project', 'المشروع'),
        dataType: 'PROJECT',
        isRequired: true,
        options: [
          {
            value: PROJECT_A,
            label: label('PRJ-1 Ring road', 'PRJ-1 Ring road'),
            catalogueEntryReference: null,
          },
        ],
      },
    ],
  });
}

export function cell(value: string | null, overrides: Partial<ReportCell> = {}): ReportCell {
  return {
    value,
    label: null,
    freshness: value === null ? 'UNKNOWN' : 'FRESH',
    asOf: value === null ? null : '2026-10-08T09:00:00Z',
    unknownReason: value === null ? 'MISSING' : null,
    isMasked: false,
    ...overrides,
  };
}

/**
 * A run of Financial Performance's default columns over two projects: A is current and complete; B's condition is
 * stale (its value kept, its own as-of), its budget masked for the audience, and its department named.
 */
export function financialResult(
  columns = FINANCIAL_COLUMNS.filter((c) => c.isDefaultVisible),
): ReportResultPage {
  const rowA: Record<string, ReportCell> = {
    FORMAL_PROJECT_ID: cell('PRJ-1'),
    TITLE: cell('Ring road'),
    DEPARTMENT: cell(DEPARTMENT_ID, { label: label('Strategy Department', 'إدارة الاستراتيجية') }),
    FINANCIAL_STATUS: cell('GREEN'),
    APPROVED_BUDGET: cell('1250000.00'),
    ACTUAL_EXPENDITURE_TO_DATE: cell('800000.00'),
  };
  const rowB: Record<string, ReportCell> = {
    FORMAL_PROJECT_ID: cell('PRJ-2'),
    TITLE: cell('طريق الملك'),
    DEPARTMENT: cell(DEPARTMENT_ID, { label: label('Strategy Department', 'إدارة الاستراتيجية') }),
    FINANCIAL_STATUS: cell('AMBER', { freshness: 'STALE', asOf: '2026-09-01T09:00:00Z' }),
    APPROVED_BUDGET: cell(null, { unknownReason: 'RESTRICTED', isMasked: true }),
    ACTUAL_EXPENDITURE_TO_DATE: cell(null, { unknownReason: 'SOURCE_UNAVAILABLE' }),
  };
  return {
    reportCode: 'FINANCIAL_PERFORMANCE',
    versionNo: 1,
    columns,
    sections: [
      {
        projectionCode: 'FINANCIAL_KPI.PUBLISHED_FINANCIAL_SNAPSHOT',
        sourceDomain: 'FINANCIAL_KPI',
        projectionVersion: '1',
        projection: {
          semanticState: 'PUBLISHED_OFFICIAL',
          freshness: 'STALE',
          asOf: '2026-09-01T09:00:00Z',
          coverage: 'PARTIAL',
        },
        unknownReason: null,
        coverage: { eligibleCount: 3, includedCount: 2, excludedCount: 1, staleCount: 1 },
      },
    ],
    items: [
      {
        projectId: PROJECT_A,
        snapshotIndex: null,
        cells: columns.map((c) => rowA[c.fieldCode] ?? cell(null)),
      },
      {
        projectId: PROJECT_B,
        snapshotIndex: null,
        cells: columns.map((c) => rowB[c.fieldCode] ?? cell(null)),
      },
    ],
    page: 1,
    pageSize: 25,
    totalCount: 2,
    departmentOptions: [DEPARTMENT_ID],
    generatedAt: '2026-10-11T08:00:00Z',
  };
}

function allowlistEntry(
  sourceEntityCode: string,
  fieldCode: string,
  en: string,
  ar: string,
  valueType: ReportAllowlistEntry['valueType'],
  extra: Partial<ReportAllowlistEntry> = {},
): ReportAllowlistEntry {
  return {
    id: `entry-${sourceEntityCode}-${fieldCode}`,
    sourceEntityCode,
    fieldCode,
    label: label(en, ar),
    valueType,
    isFilterable: false,
    isSortable: true,
    operators: [],
    values: [],
    projectionCode: null,
    semanticState: null,
    isSensitive: false,
    ...extra,
  };
}

/** The PUBLISHED REPORT_RULES allowlist the explorer is offered: four fields over two sources. */
export const ALLOWLIST: ReportAllowlistEntry[] = [
  allowlistEntry('PROJECT', 'FORMAL_PROJECT_ID', 'Project ID', 'رقم المشروع', 'TEXT', {
    isFilterable: true,
    operators: ['EQ', 'CONTAINS'],
  }),
  allowlistEntry('PROJECT', 'TITLE', 'Project', 'اسم المشروع', 'TEXT'),
  allowlistEntry(
    'PROJECT_LIFECYCLE_STATE',
    'LIFECYCLE_STATE',
    'Lifecycle state',
    'حالة دورة الحياة',
    'CODE',
    {
      isFilterable: true,
      operators: ['EQ', 'NEQ', 'IN'],
      values: ['ACTIVE', 'SUSPENDED', 'CLOSED'],
      projectionCode: 'PROJECT.LIFECYCLE_STATE',
      semanticState: 'CURRENT_LIVE',
    },
  ),
  allowlistEntry('RISK_RISK_EXPOSURE', 'OPEN_RISKS', 'Open risks', 'المخاطر المفتوحة', 'COUNT', {
    isFilterable: true,
    operators: ['GT', 'GTE', 'LT', 'LTE', 'BETWEEN'],
    projectionCode: 'RISK.RISK_EXPOSURE',
    semanticState: 'CURRENT_LIVE',
  }),
];

export function explorerResult(request: { columns: { fieldCode: string }[] }): ReportResultPage {
  const columns = request.columns.map(
    (ref) =>
      FINANCIAL_COLUMNS.find((c) => c.fieldCode === ref.fieldCode) ??
      column('PROJECT_LIFECYCLE_STATE', ref.fieldCode, ref.fieldCode, ref.fieldCode, 'CODE', {
        projectionCode: 'PROJECT.LIFECYCLE_STATE',
        semanticState: 'CURRENT_LIVE',
      }),
  );
  return {
    ...financialResult(columns),
    reportCode: null,
    versionNo: null,
    sections: [],
    items: [
      {
        projectId: PROJECT_A,
        snapshotIndex: null,
        cells: columns.map((c) =>
          c.fieldCode === 'LIFECYCLE_STATE'
            ? cell('SUSPENDED')
            : cell(c.fieldCode === 'TITLE' ? 'Ring road' : 'PRJ-1'),
        ),
      },
    ],
    totalCount: 1,
  };
}

export function jobSummary(overrides: Partial<ReportJobSummary> = {}): ReportJobSummary {
  return {
    id: 'job-1',
    requestedByUserId: OWNER_ID,
    kind: 'REPORT',
    reportCode: 'FINANCIAL_PERFORMANCE',
    exportFormat: 'XLSX',
    status: 'COMPLETED',
    failureCode: null,
    requestedAt: '2026-10-10T07:00:00Z',
    completedAt: '2026-10-10T07:01:00Z',
    sensitivity: 'SENSITIVE',
    expiresAt: '2026-10-11T07:01:00Z',
    ...overrides,
  };
}

export function jobDetail(overrides: Partial<ReportJobDetail> = {}): ReportJobDetail {
  return {
    id: 'job-new',
    requestedByUserId: OWNER_ID,
    kind: 'REPORT',
    reportCode: 'FINANCIAL_PERFORMANCE',
    reportDefinitionId: 'def-FINANCIAL_PERFORMANCE',
    exportFormat: 'CSV',
    reportLanguage: 'EN',
    status: 'REQUESTED',
    requestedAt: '2026-10-11T08:00:00Z',
    startedAt: null,
    completedAt: null,
    failureCode: null,
    correlationId: 'corr-1',
    output: null,
    ...overrides,
  };
}

export function savedViewDetail(overrides: Partial<SavedViewDetail> = {}): SavedViewDetail {
  return {
    id: 'view-own',
    ownerUserId: OWNER_ID,
    viewType: 'REPORT_PARAMETERS',
    name: { text: 'Red projects', language: 'EN' },
    reportCode: 'FINANCIAL_PERFORMANCE',
    reportDefinitionId: 'def-FINANCIAL_PERFORMANCE',
    reportVersionNo: 1,
    parameters: [{ code: 'FINANCIAL_CONDITION', value: 'RED' }],
    columns: [],
    filters: [],
    sharedWithRoleCodes: ['R03'],
    compatibility: 'VALID',
    issues: [],
    createdAt: '2026-10-09T07:00:00Z',
    updatedAt: '2026-10-09T07:00:00Z',
    ...overrides,
  };
}

export function summaryOf(view: SavedViewDetail): SavedViewSummary {
  return {
    id: view.id,
    ownerUserId: view.ownerUserId,
    viewType: view.viewType,
    name: view.name,
    reportCode: view.reportCode,
    sharedWithRoleCodes: view.sharedWithRoleCodes,
    compatibility: view.compatibility,
    updatedAt: view.updatedAt,
  };
}

/** A colleague's composition, shared with R02: the person reads it, never its other recipients. */
export const SHARED_VIEW = savedViewDetail({
  id: 'view-shared',
  ownerUserId: COLLEAGUE_ID,
  viewType: 'EXPLORER_COMPOSITION',
  name: { text: 'المشاريع المعلقة', language: 'AR' },
  reportCode: null,
  reportDefinitionId: null,
  reportVersionNo: null,
  parameters: [],
  columns: [
    { sourceEntityCode: 'PROJECT', fieldCode: 'FORMAL_PROJECT_ID', sortDirection: 'ASC' },
    {
      sourceEntityCode: 'PROJECT_LIFECYCLE_STATE',
      fieldCode: 'LIFECYCLE_STATE',
      sortDirection: null,
    },
  ],
  filters: [
    {
      sourceEntityCode: 'PROJECT_LIFECYCLE_STATE',
      fieldCode: 'LIFECYCLE_STATE',
      operator: 'EQ',
      value: 'SUSPENDED',
    },
  ],
  sharedWithRoleCodes: [],
});

export interface ReportState {
  catalogue?: ReportCatalogueEntry[];
  reports?: ReportView[];
  allowlist?: ReportAllowlistEntry[] | MockReply;
  jobs?: ReportJobSummary[];
  views?: SavedViewDetail[];
}

/** FG-02's routes over the fixtures. A report or view the state does not hold is 404, as the API answers (R-47). */
export function withReports(api: MockApi, state: ReportState = {}): MockApi {
  const reports = state.reports ?? [reportView(), projectReportView()];
  const views = state.views ?? [savedViewDetail(), SHARED_VIEW];
  return api
    .on('GET', /^\/reports$/, { body: page(state.catalogue ?? CATALOGUE) })
    .on('GET', /^\/reports\/[A-Z_]+$/, (request) => {
      const report = reports.find((candidate) => request.path.endsWith(`/${candidate.code}`));
      return report === undefined ? problem(404, 'NOT_FOUND') : { body: report };
    })
    .on('POST', /^\/reports\/[A-Z_]+\/run$/, (request) => {
      const body = request.body as { columns: { fieldCode: string }[] | null };
      const shown =
        body.columns === null
          ? FINANCIAL_COLUMNS.filter((c) => c.isDefaultVisible)
          : body.columns.flatMap((ref) =>
              FINANCIAL_COLUMNS.filter((c) => c.fieldCode === ref.fieldCode),
            );
      return { body: financialResult(shown) };
    })
    .on('POST', /^\/reports\/[A-Z_]+\/export$/, { status: 202, body: jobDetail() })
    .on('GET', /^\/report-allowlist-entries$/, () => {
      const allowlist = state.allowlist ?? ALLOWLIST;
      return Array.isArray(allowlist)
        ? {
            body: {
              ...page(allowlist, 200),
              configurationVersionId: 'rules-2',
              configurationVersionNo: 2,
            },
          }
        : allowlist;
    })
    .on('POST', /^\/report-explorer\/run$/, (request) => ({
      body: explorerResult(request.body as { columns: { fieldCode: string }[] }),
    }))
    .on('POST', /^\/report-explorer\/export$/, {
      status: 202,
      body: jobDetail({ kind: 'EXPLORER', reportCode: null }),
    })
    .on('GET', /^\/report-jobs$/, { body: page(state.jobs ?? [jobSummary()]) })
    .on('GET', /^\/report-jobs\/[^/]+$/, { body: jobDetail() })
    .on('GET', /^\/saved-views$/, { body: page(views.map(summaryOf)) })
    .on('GET', /^\/saved-views\/[^/]+$/, (request) => {
      const view = views.find((candidate) => request.path.endsWith(`/${candidate.id}`));
      return view === undefined
        ? problem(404, 'NOT_FOUND')
        : { body: view, headers: { ETag: '"7"' } };
    })
    .on('POST', /^\/saved-views$/, (request) => ({
      status: 201,
      body: savedViewDetail({ id: 'view-new', ...(request.body as Partial<SavedViewDetail>) }),
      headers: { ETag: '"1"' },
    }))
    .on('PUT', /^\/saved-views\/[^/]+$/, (request) => ({
      body: savedViewDetail(request.body as Partial<SavedViewDetail>),
      headers: { ETag: '"8"' },
    }))
    .on('DELETE', /^\/saved-views\/[^/]+$/, { status: 204 })
    .on('GET', /^\/users\/[^/]+$/, problem(403, 'PERMISSION_DENIED'));
}

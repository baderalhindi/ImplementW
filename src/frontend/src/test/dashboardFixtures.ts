import {
  type DashboardCatalogueEntry,
  type DashboardCode,
  type DashboardView,
  type DashboardWidgetResult,
} from '@/features/dashboards/api/types.ts';

import { type MockApi, type MockReply, page } from './mockApi.ts';

// FG-01's API (TASK-069 dashboards.md §5) over fixtures. The catalogue follows the seeded audience of the three
// dashboards (`seed-master-data.sql` §6) and the API's landing rule, so a role signs in to what it would really get.

export const DEPARTMENT_A = 'dddddddd-0000-4000-8000-0000000000a1';
export const DEPARTMENT_B = 'dddddddd-0000-4000-8000-0000000000b2';
export const REFRESHED_AT = '2026-10-10T09:00:00Z';

const NAMES: Record<DashboardCode, { en: string; ar: string }> = {
  PORTFOLIO: { en: 'Portfolio Dashboard', ar: 'لوحة المحفظة' },
  PROJECT: { en: 'Project Dashboard', ar: 'لوحة المشروع' },
  GOVERNANCE: { en: 'Governance Dashboard', ar: 'لوحة الحوكمة' },
};

/** The seeded audience: the roles each dashboard opens for, and those that land on it. */
const AUDIENCE: Record<DashboardCode, { roles: string[]; landing: string[] }> = {
  PORTFOLIO: { roles: ['R02', 'R03', 'R06', 'R07'], landing: ['R02', 'R03', 'R06', 'R07'] },
  PROJECT: {
    roles: ['R02', 'R03', 'R04', 'R05', 'R06', 'R07', 'R08'],
    landing: ['R08'],
  },
  GOVERNANCE: { roles: ['R01', 'R02', 'R03', 'R04', 'R05', 'R07'], landing: ['R01', 'R04', 'R05'] },
};

const CODES: DashboardCode[] = ['PORTFOLIO', 'PROJECT', 'GOVERNANCE'];

export function catalogueEntry(
  code: DashboardCode,
  overrides: Partial<DashboardCatalogueEntry> = {},
): DashboardCatalogueEntry {
  return {
    code,
    dashboardDefinitionId: `00000000-0000-4000-8000-00000000d0${String(CODES.indexOf(code))}0`,
    versionNo: 1,
    name: NAMES[code],
    description: { en: `${NAMES[code].en}: what it shows.`, ar: `${NAMES[code].ar}: ما تعرضه.` },
    contextKind: code === 'PROJECT' ? 'PROJECT' : 'PORTFOLIO',
    allowsPersonalization: code === 'PORTFOLIO',
    isDefaultLanding: false,
    ...overrides,
  };
}

/**
 * The catalogue the API gives a person holding `roles`: the dashboards their roles are an audience of (an external
 * person the Project Dashboard only, ADR-019), landing where their first role in code order lands (TBC-DSH-003).
 */
export function catalogueFor(roles: string[], external = false): DashboardCatalogueEntry[] {
  const open = CODES.filter(
    (code) =>
      (!external || code === 'PROJECT') &&
      AUDIENCE[code].roles.some((role) => roles.includes(role)),
  );
  const first = [...roles]
    .sort()
    .find((role) => open.some((code) => AUDIENCE[code].landing.includes(role)));
  return open.map((code) =>
    catalogueEntry(code, {
      isDefaultLanding: first !== undefined && AUDIENCE[code].landing.includes(first),
    }),
  );
}

export function widget(overrides: Partial<DashboardWidgetResult> = {}): DashboardWidgetResult {
  return {
    code: 'CURRENT_HEALTH',
    title: { en: 'Current Overall Project Health', ar: 'الصحة العامة الحالية' },
    widgetType: 'METRIC_CARD',
    projectionCode: 'PROGRESS.PROJECT_HEALTH_STATUS',
    projectionVersion: '1',
    sourceDomain: 'WF-02',
    layoutRow: 1,
    layoutColumn: 1,
    layoutSpan: 6,
    isOptionalVisibility: false,
    isHidden: false,
    personalSortOrder: null,
    projection: {
      semanticState: 'CURRENT_LIVE',
      freshness: 'FRESH',
      asOf: '2026-10-09T08:00:00Z',
      coverage: 'COMPLETE',
    },
    unknownReason: null,
    data: { state: 'AMBER', figures: [], distribution: [], series: [] },
    coverageDetail: null,
    maskedFields: [],
    drillTargetScreenId: null,
    ...overrides,
  };
}

/** A widget whose source cannot say: UNKNOWN, with its reason and no data (TASK-069 D-3). */
export function unknownWidget(
  reason: NonNullable<DashboardWidgetResult['unknownReason']>,
  overrides: Partial<DashboardWidgetResult> = {},
): DashboardWidgetResult {
  return widget({
    unknownReason: reason,
    data: null,
    projection: {
      semanticState: 'CURRENT_LIVE',
      freshness: 'UNKNOWN',
      asOf: null,
      coverage: 'NONE',
    },
    ...overrides,
  });
}

export function dashboardView(
  code: DashboardCode,
  overrides: Partial<DashboardView> = {},
): DashboardView {
  const entry = catalogueEntry(code);
  return {
    code,
    dashboardDefinitionId: entry.dashboardDefinitionId,
    versionNo: entry.versionNo,
    name: entry.name,
    description: entry.description,
    contextKind: entry.contextKind,
    allowsPersonalization: entry.allowsPersonalization,
    projectId: null,
    departmentId: null,
    departmentOptions: code === 'PROJECT' ? [] : [DEPARTMENT_A, DEPARTMENT_B],
    refreshedAt: REFRESHED_AT,
    widgets: [widget()],
    ...overrides,
  };
}

export interface DashboardState {
  catalogue?: DashboardCatalogueEntry[];
  /** A view, or a refusal, per dashboard; the Project Dashboard is answered for the project asked for. */
  views?: Partial<Record<DashboardCode, DashboardView | MockReply>>;
}

function isView(value: DashboardView | MockReply): value is DashboardView {
  return 'widgets' in value;
}

/** GET /dashboards and GET /dashboards/{code}, answering every dashboard by default. */
export function withDashboards(api: MockApi, state: DashboardState = {}): MockApi {
  return api
    .on('GET', /^\/dashboards$/, { body: page(state.catalogue ?? catalogueFor(['R02'])) })
    .on('GET', /^\/dashboards\/(PORTFOLIO|PROJECT|GOVERNANCE)$/, (request) => {
      const code = request.path.split('/').at(-1) as DashboardCode;
      const reply = state.views?.[code] ?? dashboardView(code);
      if (!isView(reply)) {
        return reply;
      }
      return {
        body: {
          ...reply,
          projectId: request.query.get('projectId') ?? reply.projectId,
          departmentId: request.query.get('departmentId') ?? reply.departmentId,
        },
      };
    });
}

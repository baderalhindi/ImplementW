import {
  type PortfolioBreakdownRow,
  type PortfolioCard,
  type PortfolioInsights,
} from '@/features/dashboards/api/insightTypes.ts';

import {
  type DesignView,
  type FieldAggregation,
  type FieldDescriptor,
  type FieldGrain,
  type FieldType,
  type FOLLOW_UP_MEASURES,
  type GroupBy,
  type Measure,
  type ON_PLAN_MEASURES,
  type VisualGroup,
  type VisualResult,
  type VisualType,
} from '@/features/dashboards/design/designApi.ts';

import { PROJECT_ID } from './projectFixtures.ts';

// Test data only. Ids of the catalogue items named by the rows; the item names fall back to "Item xxxxxxxx" without lookups.

export const PORTFOLIO_A = 'aa000000-0000-4000-8000-000000000001';
export const INITIATIVE_A = 'ab000000-0000-4000-8000-000000000001';
export const SECTOR_A = 'ac000000-0000-4000-8000-000000000001';

export function breakdownRow(
  overrides: Partial<PortfolioBreakdownRow> = {},
): PortfolioBreakdownRow {
  return {
    itemId: PORTFOLIO_A,
    parentItemId: null,
    projects: 167,
    valueSar: '1500000.00',
    valueEntered: 167,
    measured: 97,
    averagePlannedPercent: 61.5,
    averageActualPercent: 54.25,
    gapPoints: -7.25,
    onPlan: 'LATE',
    needingFollowUp: 12,
    ...overrides,
  };
}

export function fullInsights(overrides: Partial<PortfolioInsights> = {}): PortfolioInsights {
  return {
    headline: {
      registeredProjects: 167,
      inProgressProjects: 120,
      totalValueSar: '1500000.00',
      valueEntered: 167,
      averagePlannedPercent: 61.5,
      averageActualPercent: 54.25,
      measured: 97,
      gapPoints: -7.25,
      onPlan: 'LATE',
      needingFollowUp: 12,
      overdueActions: 4,
    },
    byPortfolio: {
      rows: [breakdownRow()],
      total: breakdownRow({ itemId: null, projects: 200, measured: 99, needingFollowUp: 15 }),
    },
    byInitiative: {
      rows: [breakdownRow({ itemId: INITIATIVE_A, parentItemId: PORTFOLIO_A })],
      total: breakdownRow({ itemId: null }),
    },
    bySector: {
      rows: [
        breakdownRow({
          itemId: SECTOR_A,
          averagePlannedSpendPercent: null,
          averageActualSpendPercent: null,
          spendMeasured: 0,
          portfolioSplit: [{ portfolioItemId: PORTFOLIO_A, projects: 167 }],
        }),
      ],
      total: breakdownRow({ itemId: null, spendMeasured: 0 }),
    },
    stages: [{ status: 'ACTIVE', projects: 120, percent: 71.9 }],
    onPlan: { onTrack: 50, late: 30, stalled: 17, notMeasured: 70 },
    followUpReasons: {
      stalled: 5,
      suspended: 1,
      late: 3,
      overdueAction: 2,
      openObstacle: 1,
      total: 12,
    },
    topFollowUp: [
      {
        projectId: PROJECT_ID,
        formalProjectId: 'PRJ-2026-0042',
        legacyReference: null,
        title: { text: 'Coastal road upgrade', language: 'EN' },
        portfolioItemId: PORTFOLIO_A,
        reason: 'STALLED',
        currentObstacle: { text: 'Permit delay', language: 'EN' },
        nextAction: { text: 'Escalate the permit', language: 'EN' },
        nextActionOwner: 'Nora',
        nextActionDueDate: '2026-11-20',
      },
    ],
    ...overrides,
  };
}

export const NO_SECTIONS: PortfolioInsights = {
  headline: null,
  byPortfolio: null,
  byInitiative: null,
  bySector: null,
  stages: null,
  onPlan: null,
  followUpReasons: null,
  topFollowUp: null,
};

export function portfolioCard(): PortfolioCard {
  return {
    dimension: 'PORTFOLIO',
    itemId: PORTFOLIO_A,
    summary: breakdownRow(),
    projects: [
      {
        projectId: PROJECT_ID,
        formalProjectId: 'PRJ-2026-0042',
        legacyReference: null,
        title: { text: 'Coastal road upgrade', language: 'EN' },
        status: 'ACTIVE',
        departmentId: 'd1',
        externalEntityId: null,
        valueSar: '1500000.00',
        plannedStartDate: '2026-01-01',
        plannedEndDate: '2026-12-31',
        plannedPercent: null,
        actualPercent: null,
        gapPoints: null,
        onPlan: null,
        currentObstacle: null,
        nextAction: { text: 'Escalate the permit', language: 'EN' },
        nextActionOwner: null,
        nextActionDueDate: null,
      },
    ],
  };
}

type Figures = Pick<VisualGroup, 'projects' | 'measured' | 'valueEntered' | 'onPlan'> &
  Record<Measure, number | null>;

function visual(
  en: string,
  visualType: VisualType,
  measures: Measure[],
  groupBy: GroupBy,
  place: [number, number, number, number],
  groups: Figures[] | null,
  keys: (string | null)[] = [null],
): VisualResult {
  const [layoutColumn, layoutRow, layoutWidth, layoutHeight] = place;
  return {
    title: { ar: en, en },
    visualType,
    measures,
    groupBy,
    layoutColumn,
    layoutRow,
    layoutWidth,
    layoutHeight,
    groups:
      groups?.map((figures, index) => ({
        key: keys[index] ?? null,
        label: null,
        projects: figures.projects,
        measured: figures.measured,
        valueEntered: figures.valueEntered,
        onPlan: figures.onPlan,
        values: measures.map((measure) => figures[measure]),
      })) ?? null,
  };
}

/** The on-plan and follow-up reason figures (dashboard-designer-box-sections.md): the boxes' counts over the whole, none for a group. */
function boxFigures(
  insights: PortfolioInsights | null,
): Pick<Figures, (typeof ON_PLAN_MEASURES)[number] | (typeof FOLLOW_UP_MEASURES)[number]> {
  const onPlan = insights?.onPlan ?? null;
  const reasons = insights?.followUpReasons ?? null;
  return {
    ON_TRACK: onPlan?.onTrack ?? null,
    LATE: onPlan?.late ?? null,
    STALLED: onPlan?.stalled ?? null,
    NOT_MEASURED: onPlan?.notMeasured ?? null,
    FOLLOW_UP_STALLED: reasons?.stalled ?? null,
    FOLLOW_UP_SUSPENDED: reasons?.suspended ?? null,
    FOLLOW_UP_LATE: reasons?.late ?? null,
    FOLLOW_UP_ACTION_OVERDUE: reasons?.overdueAction ?? null,
    FOLLOW_UP_OPEN_OBSTACLE: reasons?.openObstacle ?? null,
  };
}

/**
 * The standard design of the Portfolio dashboard (PortfolioDesignService.StandardDesign) with the figures the API would calculate from these
 * insights: the headline cards from `headline`, the portfolio charts from `byPortfolio`, the status, stage and on-plan charts from theirs; a
 * section carries no figures of its own.
 */
export function standardDesignOf(insights: PortfolioInsights): DesignView {
  const headline = insights.headline;
  const whole: Figures[] | null =
    headline === null
      ? null
      : [
          {
            projects: headline.registeredProjects,
            measured: headline.measured,
            valueEntered: headline.valueEntered,
            onPlan: headline.onPlan,
            PROJECTS: headline.registeredProjects,
            IN_PROGRESS: headline.inProgressProjects,
            VALUE_SAR: headline.totalValueSar === null ? null : Number(headline.totalValueSar),
            AVERAGE_PLANNED: headline.averagePlannedPercent,
            AVERAGE_ACTUAL: headline.averageActualPercent,
            GAP_POINTS: headline.gapPoints,
            NEEDING_FOLLOW_UP: headline.needingFollowUp,
            OVERDUE_ACTIONS: headline.overdueActions,
            ...boxFigures(insights),
          },
        ];
  const rows = headline === null ? null : (insights.byPortfolio?.rows ?? []);
  const byPortfolio: Figures[] | null =
    rows?.map((row) => ({
      projects: row.projects,
      measured: row.measured,
      valueEntered: row.valueEntered,
      onPlan: row.onPlan,
      PROJECTS: row.projects,
      IN_PROGRESS: null,
      VALUE_SAR: row.valueSar === null ? null : Number(row.valueSar),
      AVERAGE_PLANNED: row.averagePlannedPercent,
      AVERAGE_ACTUAL: row.averageActualPercent,
      GAP_POINTS: row.gapPoints,
      NEEDING_FOLLOW_UP: row.needingFollowUp,
      OVERDUE_ACTIONS: null,
      ...boxFigures(null),
    })) ?? null;
  const portfolioKeys = rows?.map((row) => row.itemId) ?? [];
  return {
    isStandard: true,
    updatedAt: null,
    visuals: [
      visual(
        'Registered projects',
        'CARD',
        ['PROJECTS', 'IN_PROGRESS'],
        'NONE',
        [0, 0, 4, 2],
        whole,
      ),
      visual('Total value', 'CARD', ['VALUE_SAR'], 'NONE', [4, 0, 4, 2], whole),
      visual('Average planned', 'CARD', ['AVERAGE_PLANNED'], 'NONE', [8, 0, 4, 2], whole),
      visual('Average actual', 'CARD', ['AVERAGE_ACTUAL'], 'NONE', [0, 2, 4, 2], whole),
      visual('Gap (points)', 'CARD', ['GAP_POINTS'], 'NONE', [4, 2, 4, 2], whole),
      visual(
        'Needing follow-up',
        'CARD',
        ['NEEDING_FOLLOW_UP', 'OVERDUE_ACTIONS'],
        'NONE',
        [8, 2, 4, 2],
        whole,
      ),
      visual(
        'Projects by portfolio',
        'BAR',
        ['PROJECTS'],
        'PORTFOLIO',
        [0, 4, 6, 5],
        byPortfolio,
        portfolioKeys,
      ),
      visual(
        'Planned and actual by portfolio',
        'COLUMN',
        ['AVERAGE_PLANNED', 'AVERAGE_ACTUAL'],
        'PORTFOLIO',
        [6, 4, 6, 5],
        byPortfolio,
        portfolioKeys,
      ),
      visual(
        'Projects by status',
        'BAR',
        ['PROJECTS'],
        'STATUS',
        [0, 9, 6, 5],
        insights.stages?.map((stage) => counted(stage.projects)) ?? null,
        insights.stages?.map((stage) => stage.status) ?? [],
      ),
      visual(
        'Projects by stage',
        'BAR',
        ['PROJECTS'],
        'STAGE',
        [6, 9, 6, 5],
        insights.byCurrentStage
          ?.filter((stage) => stage.projects > 0)
          .map((stage) => counted(stage.projects)) ?? null,
        insights.byCurrentStage
          ?.filter((stage) => stage.projects > 0)
          .map((stage) => stage.stageCode) ?? [],
      ),
      visual(
        'On-plan indicator',
        'DONUT',
        ['PROJECTS'],
        'ON_PLAN',
        [0, 14, 6, 5],
        insights.onPlan === null
          ? null
          : [
              insights.onPlan.onTrack,
              insights.onPlan.late,
              insights.onPlan.stalled,
              insights.onPlan.notMeasured,
            ].map(counted),
        ['ON_TRACK', 'LATE', 'STALLED', null],
      ),
      ...(
        [
          ['Update completion by entity', 'ENTITY_COMPLETION', [6, 14, 6, 5]],
          ['By portfolio', 'PORTFOLIO_TABLE', [0, 19, 12, 6]],
          ['By initiative', 'INITIATIVE_TABLE', [0, 25, 12, 6]],
          ['By sector', 'SECTOR_TABLE', [0, 31, 12, 8]],
          ['Projects by status', 'STATUS_TABLE', [0, 39, 6, 5]],
          ['Projects by stage', 'CURRENT_STAGE_TABLE', [6, 39, 6, 5]],
          ['On-plan indicator', 'ON_PLAN_COUNTS', [0, 44, 6, 4]],
          ['Why projects need follow-up', 'FOLLOW_UP_REASONS', [6, 44, 6, 4]],
          ['Top 10 to follow up', 'TOP_FOLLOW_UP', [0, 48, 12, 7]],
          ['Projects by location', 'PROJECT_LOCATIONS', [0, 55, 12, 7]],
          ['Dashboard widgets', 'DASHBOARD_WIDGETS', [0, 62, 12, 17]],
          ['Update completion by entity (table)', 'ENTITY_COMPLETION_TABLE', [0, 79, 12, 8]],
        ] as const
      ).map(([en, section, place]) => ({
        ...visual(en, 'SECTION', [], 'NONE', [...place], []),
        section,
      })),
    ],
  };
}

/** A group's figures where the only measure is its count of projects. */
function counted(projects: number): Figures {
  return {
    projects,
    measured: 0,
    valueEntered: 0,
    onPlan: null,
    PROJECTS: projects,
    IN_PROGRESS: null,
    VALUE_SAR: null,
    AVERAGE_PLANNED: null,
    AVERAGE_ACTUAL: null,
    GAP_POINTS: null,
    NEEDING_FOLLOW_UP: null,
    OVERDUE_ACTIONS: null,
    ...boxFigures(null),
  };
}

// The designer's data fields (18 Nov 2026, dashboard-designer-data-fields.md): GET /portfolio-insights/design/fields.

/** The calculations each field type allows, in the contract's order: the first is what a newly chosen field starts with. */
export const AGGREGATIONS_OF: Record<FieldType, FieldAggregation[]> = {
  CODE: ['COUNT'],
  COUNT: ['COUNT', 'SUM', 'AVERAGE', 'MIN', 'MAX'],
  DAYS: ['COUNT', 'SUM', 'AVERAGE', 'MIN', 'MAX'],
  SAR: ['COUNT', 'SUM', 'AVERAGE', 'MIN', 'MAX'],
  PERCENT: ['COUNT', 'AVERAGE', 'MIN', 'MAX'],
  BOOLEAN: ['COUNT', 'COUNT_TRUE', 'SHARE_TRUE'],
  DATE: ['COUNT', 'MIN', 'MAX'],
  DATE_TIME: ['COUNT', 'MIN', 'MAX'],
  TEXT: ['COUNT'],
  ITEM: ['COUNT'],
};

/** The status codes of the RAG-like projections (the register's Values for their CODE fields). */
const RAG = ['GREEN', 'AMBER', 'RED', 'UNKNOWN'];

export const FIELD = {
  FINANCIAL_CONDITION: 'FINANCIAL_KPI.FINANCIAL_POSITION.FINANCIAL_CONDITION',
  APPROVED_BUDGET: 'FINANCIAL_KPI.FINANCIAL_POSITION.APPROVED_BUDGET',
  ACTUAL_EXPENDITURE: 'FINANCIAL_KPI.FINANCIAL_POSITION.ACTUAL_EXPENDITURE_TO_DATE',
  FORECAST_AT_COMPLETION: 'FINANCIAL_KPI.FINANCIAL_POSITION.FORECAST_AT_COMPLETION',
  OVERALL_HEALTH: 'PROGRESS.PROJECT_HEALTH_STATUS.OVERALL_HEALTH',
  ACTUAL_PERCENT: 'PROGRESS.PROJECT_HEALTH_STATUS.ACTUAL_PERCENT',
  PLANNED_PERCENT: 'PROGRESS.PROJECT_HEALTH_STATUS.PLANNED_PERCENT',
  OPEN_RISKS: 'RISK.RISK_EXPOSURE.OPEN_RISKS',
  NOT_ASSESSED: 'RISK.RISK_EXPOSURE.NOT_ASSESSED',
  REVIEW_OVERDUE: 'RISK.RISK_EXPOSURE.REVIEW_OVERDUE',
  SCHEDULE_HEALTH: 'SCHEDULE.SCHEDULE_HEALTH_STATUS.SCHEDULE_HEALTH',
  FINISH_VARIANCE_DAYS: 'SCHEDULE.SCHEDULE_HEALTH_STATUS.FINISH_VARIANCE_DAYS',
  // The project record's own attributes (more fields contract §1) and the published progress history (§4, snapshot grain).
  REGISTRATION_BUDGET: 'PROJECT.PROJECT_PROFILE.REGISTRATION_BUDGET',
  FUNDING_SOURCE: 'PROJECT.PROJECT_PROFILE.FUNDING_SOURCE',
  DECLARED_ACTUAL_PERCENT: 'PROJECT.PROJECT_PROFILE.DECLARED_ACTUAL_PERCENT',
  IS_STRATEGIC: 'PROJECT.PROJECT_PROFILE.IS_STRATEGIC',
  PLANNED_END_DATE: 'PROJECT.PROJECT_PROFILE.PLANNED_END_DATE',
  ACTIVATED_AT: 'PROJECT.PROJECT_PROFILE.ACTIVATED_AT',
  HISTORY_PUBLISHED_AT: 'PROGRESS.PUBLISHED_PROGRESS_HISTORY.PUBLISHED_AT',
  HISTORY_PERIOD_START: 'PROGRESS.PUBLISHED_PROGRESS_HISTORY.PERIOD_START',
  HISTORY_PERIOD_END: 'PROGRESS.PUBLISHED_PROGRESS_HISTORY.PERIOD_END',
  HISTORY_HEALTH: 'PROGRESS.PUBLISHED_PROGRESS_HISTORY.OVERALL_HEALTH',
  HISTORY_ACTUAL_PERCENT: 'PROGRESS.PUBLISHED_PROGRESS_HISTORY.ACTUAL_PERCENT',
  HISTORY_PLANNED_PERCENT: 'PROGRESS.PUBLISHED_PROGRESS_HISTORY.PLANNED_PERCENT',
} as const;

function field(
  key: string,
  sourceDomain: string,
  type: FieldType,
  {
    isSensitive = false,
    values = [],
    grain = 'PROJECT',
  }: { isSensitive?: boolean; values?: string[]; grain?: FieldGrain } = {},
): FieldDescriptor {
  const fieldCode = key.split('.').at(-1) ?? key;
  return {
    key,
    projectionCode: key.slice(0, key.length - fieldCode.length - 1),
    sourceDomain,
    fieldCode,
    type,
    isSensitive,
    values,
    aggregations: AGGREGATIONS_OF[type],
    grain,
  };
}

/**
 * The fields endpoint's body: four project-grain projections of the demo register (their fields, types, sensitivity and values as the preview
 * API's /dashboard-projections lists them), in the register's order.
 */
export function designFieldsFixture(): { fields: FieldDescriptor[] } {
  return {
    fields: [
      field(FIELD.FINANCIAL_CONDITION, 'WF-14', 'CODE', { values: RAG }),
      field(FIELD.APPROVED_BUDGET, 'WF-14', 'SAR', { isSensitive: true }),
      field(FIELD.ACTUAL_EXPENDITURE, 'WF-14', 'SAR', { isSensitive: true }),
      field(FIELD.FORECAST_AT_COMPLETION, 'WF-14', 'SAR', { isSensitive: true }),
      field(FIELD.OVERALL_HEALTH, 'WF-02', 'CODE', { values: RAG }),
      field(FIELD.ACTUAL_PERCENT, 'WF-02', 'PERCENT'),
      field(FIELD.PLANNED_PERCENT, 'WF-02', 'PERCENT'),
      field(FIELD.OPEN_RISKS, 'WF-06', 'COUNT'),
      field(FIELD.NOT_ASSESSED, 'WF-06', 'COUNT'),
      field(FIELD.REVIEW_OVERDUE, 'WF-06', 'COUNT'),
      field(FIELD.SCHEDULE_HEALTH, 'WF-03', 'CODE', { values: RAG }),
      field(FIELD.FINISH_VARIANCE_DAYS, 'WF-03', 'DAYS'),
      field(FIELD.REGISTRATION_BUDGET, 'WF-01', 'SAR', { isSensitive: true }),
      field(FIELD.DECLARED_ACTUAL_PERCENT, 'WF-01', 'PERCENT'),
      field(FIELD.FUNDING_SOURCE, 'WF-01', 'CODE', {
        values: ['STRATEGY_BUDGET_FULL', 'STRATEGY_BUDGET_PARTIAL', 'NOT_STRATEGY_BUDGET'],
      }),
      field(FIELD.IS_STRATEGIC, 'WF-01', 'BOOLEAN'),
      field(FIELD.PLANNED_END_DATE, 'WF-01', 'DATE'),
      field(FIELD.ACTIVATED_AT, 'WF-01', 'DATE_TIME'),
      field(FIELD.HISTORY_PUBLISHED_AT, 'WF-02', 'DATE_TIME', { grain: 'SNAPSHOT' }),
      field(FIELD.HISTORY_PERIOD_START, 'WF-02', 'DATE', { grain: 'SNAPSHOT' }),
      field(FIELD.HISTORY_PERIOD_END, 'WF-02', 'DATE', { grain: 'SNAPSHOT' }),
      field(FIELD.HISTORY_HEALTH, 'WF-02', 'CODE', { values: RAG, grain: 'SNAPSHOT' }),
      field(FIELD.HISTORY_ACTUAL_PERCENT, 'WF-02', 'PERCENT', { grain: 'SNAPSHOT' }),
      field(FIELD.HISTORY_PLANNED_PERCENT, 'WF-02', 'PERCENT', { grain: 'SNAPSHOT' }),
    ],
  };
}

// The Risks record source (20 Nov 2026, dashboard-designer-record-sources.md): GET /portfolio-insights/design/fields?source=RISKS.

/** The risk statuses, in the Risk module's order. */
export const RISK_STATUSES = ['IDENTIFIED', 'ASSESSED', 'TREATMENT', 'MONITORING', 'CLOSED'];

/** The risk matrix's rating codes (riskFixtures.ts's standard ones); probability and impact are its levels. */
export const RISK_RATINGS = ['LOW', 'MEDIUM', 'HIGH', 'CRITICAL'];
const RISK_LEVELS = ['1', '2', '3', '4', '5'];

export const RISK_FIELD = {
  RISK: 'RISK.RISK',
  TITLE: 'RISK.TITLE',
  PROJECT: 'RISK.PROJECT',
  STATUS: 'RISK.STATUS',
  CATEGORY: 'RISK.CATEGORY',
  OWNER: 'RISK.OWNER',
  RATING: 'RISK.RATING',
  PROBABILITY: 'RISK.PROBABILITY',
  IMPACT: 'RISK.IMPACT',
  SCORE: 'RISK.SCORE',
  IDENTIFIED_DATE: 'RISK.IDENTIFIED_DATE',
  NEXT_REVIEW_DATE: 'RISK.NEXT_REVIEW_DATE',
  CLOSED_DATE: 'RISK.CLOSED_DATE',
  REVIEW_OVERDUE: 'RISK.REVIEW_OVERDUE',
  MATERIALISED: 'RISK.MATERIALISED',
  OPEN_ACTIONS: 'RISK.OPEN_ACTIONS',
  REOPENED: 'RISK.REOPENED',
} as const;

/** The fields endpoint's body for the Risks source: one row per risk, in the contract table's order. */
export function riskFieldsFixture(): { fields: FieldDescriptor[] } {
  return {
    fields: [
      field(RISK_FIELD.RISK, 'WF-06', 'COUNT'),
      field(RISK_FIELD.TITLE, 'WF-06', 'TEXT'),
      field(RISK_FIELD.PROJECT, 'WF-06', 'ITEM'),
      field(RISK_FIELD.STATUS, 'WF-06', 'CODE', { values: RISK_STATUSES }),
      field(RISK_FIELD.CATEGORY, 'WF-06', 'ITEM'),
      field(RISK_FIELD.OWNER, 'WF-06', 'ITEM'),
      field(RISK_FIELD.RATING, 'WF-06', 'CODE', { values: RISK_RATINGS }),
      field(RISK_FIELD.PROBABILITY, 'WF-06', 'CODE', { values: RISK_LEVELS }),
      field(RISK_FIELD.IMPACT, 'WF-06', 'CODE', { values: RISK_LEVELS }),
      field(RISK_FIELD.SCORE, 'WF-06', 'COUNT'),
      field(RISK_FIELD.IDENTIFIED_DATE, 'WF-06', 'DATE'),
      field(RISK_FIELD.NEXT_REVIEW_DATE, 'WF-06', 'DATE'),
      field(RISK_FIELD.CLOSED_DATE, 'WF-06', 'DATE'),
      field(RISK_FIELD.REVIEW_OVERDUE, 'WF-06', 'BOOLEAN'),
      field(RISK_FIELD.MATERIALISED, 'WF-06', 'BOOLEAN'),
      field(RISK_FIELD.OPEN_ACTIONS, 'WF-06', 'COUNT'),
      field(RISK_FIELD.REOPENED, 'WF-06', 'COUNT'),
    ],
  };
}

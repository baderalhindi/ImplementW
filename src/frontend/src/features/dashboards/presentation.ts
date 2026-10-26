import { type BilingualLabel } from '@/features/identity-access/api/types.ts';
import { financialStatusTone, ragTone } from '@/features/financial-kpi/presentation.ts';
import { formatPercent, healthTone } from '@/features/progress/presentation.ts';
import { formatSar, isProjectStatus, statusTone } from '@/features/projects/presentation.ts';
import { type Language, type TranslationKey } from '@/shared/i18n/i18n.ts';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';

import {
  type ProjectionSemanticState,
  type WidgetFigure,
  type WidgetUnknownReason,
} from './api/types.ts';

// How FG-01's widget values read on screen. Every value is the source's own word for its own state, labelled as the
// source labels it (WF-01's lifecycle, WF-02's health, WF-14's conditions); the colour only repeats the word (WCAG
// 1.4.1). Green is reserved for a GREEN rating a source gave: a workflow state is blue or grey, so a widget whose
// value is "No data" never carries a success colour (TASK-053 D-3). Nothing here computes a value (BR-DSH-001).

type Translate = (key: TranslationKey) => string;

/** The semantic state's word and its badge: solid for published, dashed for live, dotted for history. */
export const SEMANTIC_STATES: Record<
  ProjectionSemanticState,
  { label: TranslationKey; badge: string }
> = {
  CURRENT_LIVE: { label: 'dashboards.semanticState.CURRENT_LIVE', badge: 'badge--live' },
  PUBLISHED_OFFICIAL: {
    label: 'dashboards.semanticState.PUBLISHED_OFFICIAL',
    badge: 'badge--official',
  },
  HISTORICAL_SNAPSHOT: {
    label: 'dashboards.semanticState.HISTORICAL_SNAPSHOT',
    badge: 'badge--historical',
  },
};

/** A value's vocabulary: whose states it counts or shows, and what a count of them counts. */
type Dimension =
  | 'lifecycle'
  | 'health'
  | 'reporting'
  | 'riskRating'
  | 'kpiCondition'
  | 'financialStatus'
  | 'definitionState';

/** The registered projections (TASK-069 dashboards.md §4), by the vocabulary of their values. */
const DIMENSIONS: Readonly<Record<string, Dimension>> = {
  'PROJECT.LIFECYCLE_STATE': 'lifecycle',
  'PROGRESS.PROJECT_HEALTH_STATUS': 'health',
  'PROGRESS.PUBLISHED_PROGRESS_SNAPSHOT': 'health',
  'PROGRESS.PUBLISHED_PROGRESS_HISTORY': 'health',
  'PROGRESS.PUBLISHED_SCHEDULE_HEALTH': 'health',
  'PROGRESS.REPORTING_COMPLETENESS': 'reporting',
  'SCHEDULE.SCHEDULE_HEALTH_STATUS': 'health',
  'RISK.RISK_EXPOSURE': 'riskRating',
  'FINANCIAL_KPI.FINANCIAL_POSITION': 'financialStatus',
  'FINANCIAL_KPI.PUBLISHED_FINANCIAL_SNAPSHOT': 'financialStatus',
  'FINANCIAL_KPI.KPI_CONDITION': 'kpiCondition',
  'DASHBOARDS.DEFINITION_BACKLOG': 'definitionState',
};

/** What a distribution counts, for its column heading: projects, risks, KPI assignments or dashboard versions. */
const COUNTED: Record<Dimension, TranslationKey> = {
  lifecycle: 'dashboards.counted.projects',
  health: 'dashboards.counted.projects',
  reporting: 'dashboards.counted.projects',
  financialStatus: 'dashboards.counted.projects',
  riskRating: 'dashboards.counted.risks',
  kpiCondition: 'dashboards.counted.kpiAssignments',
  definitionState: 'dashboards.counted.versions',
};

const HEALTH = new Set(['GREEN', 'AMBER', 'RED', 'UNKNOWN']);
const RAG = new Set(['GREEN', 'AMBER', 'RED', 'UNKNOWN', 'NOT_APPLICABLE']);

/** The word for one source value; null when the vocabulary has none for it (the code is then shown as it came). */
function valueKey(dimension: Dimension, key: string): TranslationKey | null {
  switch (dimension) {
    case 'lifecycle':
      return isProjectStatus(key) ? `projects.status.${key}` : null;
    case 'health':
      return HEALTH.has(key) ? (`progress.health.${key}` as TranslationKey) : null;
    case 'financialStatus':
      return HEALTH.has(key) ? (`financialKpi.financialStatus.${key}` as TranslationKey) : null;
    case 'kpiCondition':
      return key === 'NOT_PUBLISHED'
        ? 'dashboards.values.NOT_PUBLISHED'
        : RAG.has(key)
          ? (`financialKpi.rag.${key}` as TranslationKey)
          : null;
    case 'reporting':
      return key === 'UP_TO_DATE' || key === 'OVERDUE' ? `dashboards.values.${key}` : null;
    case 'riskRating':
      return key === 'NOT_ASSESSED' ? 'dashboards.values.NOT_ASSESSED' : null;
    case 'definitionState':
      return key === 'DRAFT' || key === 'VALIDATED' || key === 'PUBLISHED'
        ? `dashboards.values.definition.${key}`
        : null;
  }
}

function valueTone(dimension: Dimension, key: string): StatusTone {
  switch (dimension) {
    case 'lifecycle':
      return isProjectStatus(key) ? statusTone(key) : 'neutral';
    case 'health':
      return HEALTH.has(key) ? healthTone(key as Parameters<typeof healthTone>[0]) : 'neutral';
    case 'financialStatus':
      return HEALTH.has(key)
        ? financialStatusTone(key as Parameters<typeof financialStatusTone>[0])
        : 'neutral';
    case 'kpiCondition':
      return RAG.has(key) ? ragTone(key as Parameters<typeof ragTone>[0]) : 'neutral';
    case 'reporting':
      return key === 'OVERDUE' ? 'warning' : 'info';
    // A rating's colours are its matrix's, which the projection does not carry: no colour is invented for it.
    case 'riskRating':
    case 'definitionState':
      return 'neutral';
  }
}

export interface ValueLabel {
  text: string;
  tone: StatusTone;
}

/**
 * A source value as words: the source's own label when it sent one (a risk rating), else the vocabulary's word for
 * the projection, else the code itself — a value is never dropped or recoloured for want of a word.
 */
export function labelOfValue(
  projectionCode: string,
  key: string,
  label: BilingualLabel | null,
  language: Language,
  t: Translate,
): ValueLabel {
  const dimension = DIMENSIONS[projectionCode];
  if (dimension === undefined) {
    return { text: label?.[language] ?? key, tone: 'neutral' };
  }
  const word = valueKey(dimension, key);
  return {
    text: label?.[language] ?? (word === null ? key : t(word)),
    tone: valueTone(dimension, key),
  };
}

export function countedKey(projectionCode: string): TranslationKey {
  const dimension = DIMENSIONS[projectionCode];
  return dimension === undefined ? 'dashboards.counted.items' : COUNTED[dimension];
}

const MEASURES: Readonly<Record<string, TranslationKey>> = {
  ACTUAL_PERCENT: 'dashboards.measures.ACTUAL_PERCENT',
  PLANNED_PERCENT: 'dashboards.measures.PLANNED_PERCENT',
  ACTUAL_PERCENT_OVERRIDDEN: 'dashboards.measures.ACTUAL_PERCENT_OVERRIDDEN',
  OVERDUE_PERIODS: 'dashboards.measures.OVERDUE_PERIODS',
  FINISH_VARIANCE_DAYS: 'dashboards.measures.FINISH_VARIANCE_DAYS',
  OPEN_RISKS: 'dashboards.measures.OPEN_RISKS',
  NOT_ASSESSED: 'dashboards.measures.NOT_ASSESSED',
  REVIEW_OVERDUE: 'dashboards.measures.REVIEW_OVERDUE',
  ACTIVE_ASSIGNMENTS: 'dashboards.measures.ACTIVE_ASSIGNMENTS',
  NOT_PUBLISHED: 'dashboards.measures.NOT_PUBLISHED',
  APPROVED_BUDGET: 'dashboards.measures.APPROVED_BUDGET',
  ACTUAL_EXPENDITURE_TO_DATE: 'dashboards.measures.ACTUAL_EXPENDITURE_TO_DATE',
  FORECAST_AT_COMPLETION: 'dashboards.measures.FORECAST_AT_COMPLETION',
};

export function measureLabel(measure: string, t: Translate): string {
  const key = MEASURES[measure];
  return key === undefined ? measure : t(key);
}

/** ADR-009's override flag is a marker beside the percentage it qualifies, not a figure of its own. */
export const OVERRIDE_MEASURE = 'ACTUAL_PERCENT_OVERRIDDEN';

const COUNT_FORMAT = new Intl.NumberFormat('en');
const DAYS_FORMAT = new Intl.NumberFormat('en', { signDisplay: 'exceptZero' });

/**
 * A figure's value in its unit, Latin digits in both languages (TASK-032 D-5): the API's exact string, grouped, never
 * rounded through a float for money. Null when there is no value — the caller says why (masked, or absent).
 */
export function formatFigure(
  figure: WidgetFigure,
  t: (key: TranslationKey, params?: Record<string, string | number>) => string,
): string | null {
  if (figure.isMasked || figure.value === null) {
    return null;
  }
  switch (figure.unit) {
    case 'COUNT':
      return COUNT_FORMAT.format(Number(figure.value));
    case 'PERCENT':
      return formatPercent(figure.value);
    case 'DAYS':
      return t('dashboards.units.days', { value: DAYS_FORMAT.format(Number(figure.value)) });
    case 'SAR':
      return t('dashboards.units.sar', { amount: formatSar(figure.value) });
    default:
      return figure.value;
  }
}

/** The figures a widget shows as figures: the override flag is drawn as a marker, not listed. */
export function shownFigures(figures: WidgetFigure[]): WidgetFigure[] {
  return figures.filter((figure) => figure.measure !== OVERRIDE_MEASURE);
}

export function isOverridden(figures: WidgetFigure[]): boolean {
  return figures.some((figure) => figure.measure === OVERRIDE_MEASURE && figure.value === 'true');
}

/** The words for a widget with no data, and the sentence that explains them. */
export const UNKNOWN_REASONS: Record<
  WidgetUnknownReason,
  { label: TranslationKey; explanation: TranslationKey }
> = {
  MISSING: {
    label: 'dashboards.unknown.MISSING.label',
    explanation: 'dashboards.unknown.MISSING.explanation',
  },
  NOT_APPLICABLE: {
    label: 'dashboards.unknown.NOT_APPLICABLE.label',
    explanation: 'dashboards.unknown.NOT_APPLICABLE.explanation',
  },
  RESTRICTED: {
    label: 'dashboards.unknown.RESTRICTED.label',
    explanation: 'dashboards.unknown.RESTRICTED.explanation',
  },
  SOURCE_UNAVAILABLE: {
    label: 'dashboards.unknown.SOURCE_UNAVAILABLE.label',
    explanation: 'dashboards.unknown.SOURCE_UNAVAILABLE.explanation',
  },
};

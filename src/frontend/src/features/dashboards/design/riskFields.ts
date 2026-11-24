import { impactLabel, probabilityLabel, type RiskMatrix } from '@/features/risks/riskRules.ts';
import { type Language, translate, type TranslationKey } from '@/shared/i18n/i18n.ts';

import {
  type FieldValue,
  type GroupBy,
  type TableCell,
  type TableColumn,
  type TableRow,
  type Visual,
  type VisualType,
} from './designApi.ts';

// The Risks record source's fields (20 Nov 2026, docs/architecture/dashboard-designer-record-sources.md): their keys, the figures a visual
// starts with on Risks, and how a risk's codes read, in the Risk feature's own words (its statuses) and the risk matrix in force (its ratings
// and levels). The API calculates every figure.

type Translate = (key: TranslationKey, params?: Record<string, string | number>) => string;

/** The projection code of every risk field: `RISK.<CODE>`. */
export const RISK_PROJECTION = 'RISK';

/** The risk field one per risk: its COUNT is the number of risks. */
export const RISK_COUNT_FIELD = 'RISK.RISK';

/** A risk visual's first value: the number of risks. */
export const RISK_COUNT: FieldValue = { field: RISK_COUNT_FIELD, aggregation: 'COUNT' };

/** What a chart on Risks starts grouped by: the risk's rating. */
export const RISK_GROUP_FIELD = 'RISK.RATING';

/** What a line on Risks starts grouped by: the month each risk was identified. */
export const RISK_LINE_GROUP_FIELD = 'RISK.IDENTIFIED_DATE';

/** The risk's title, a column listing the risks one per row; and its project, whose cell's key is the project's id. */
export const RISK_TITLE_FIELD = 'RISK.TITLE';
export const RISK_PROJECT_FIELD = 'RISK.PROJECT';

/** The columns a table on Risks starts with: the risks one per row, with their project, rating, status, owner and next review date. */
export const RISK_TABLE_COLUMNS: readonly TableColumn[] = [
  RISK_TITLE_FIELD,
  RISK_PROJECT_FIELD,
  'RISK.RATING',
  'RISK.STATUS',
  'RISK.OWNER',
  'RISK.NEXT_REVIEW_DATE',
].map((key) => ({ kind: 'FIELD', key, aggregation: 'NONE' }));

/** The project attributes a risk is grouped and filtered by, through its project: no stage and no on-plan indicator. */
export const RISK_PROJECT_DIMENSIONS: readonly Exclude<GroupBy, 'NONE'>[] = [
  'PORTFOLIO',
  'INITIATIVE',
  'SECTOR',
  'DEPARTMENT',
  'ENTITY',
  'REGION',
  'CITY',
  'PRIORITY',
  'STATUS',
];

/**
 * The figures a visual of `type` draws on Risks before anyone chooses others: the number of risks, a chart grouped by rating, a line by the
 * month the risks were identified, a card by nothing. A table's are its columns (RISK_TABLE_COLUMNS).
 */
export function riskFigures(
  type: VisualType,
): Pick<
  Visual,
  'measures' | 'values' | 'groupBy' | 'groupField' | 'groupDateUnit' | 'columnField'
> {
  const grouped = type === 'BAR' || type === 'COLUMN' || type === 'PIE' || type === 'DONUT';
  return {
    measures: [],
    values: [RISK_COUNT],
    groupBy: 'NONE',
    // A heatmap is the risk matrix: probability by impact.
    groupField:
      type === 'HEATMAP'
        ? 'RISK.PROBABILITY'
        : type === 'LINE'
          ? RISK_LINE_GROUP_FIELD
          : grouped
            ? RISK_GROUP_FIELD
            : null,
    columnField: type === 'HEATMAP' ? 'RISK.IMPACT' : null,
    groupDateUnit: type === 'LINE' ? 'MONTH' : null,
  };
}

/** Whether a field key is a risk field. */
export function isRiskField(key: string): boolean {
  // RISK.<CODE> exactly: a projects field of the risk exposure (RISK.RISK_EXPOSURE.OPEN_RISKS) has three parts and is not one.
  const parts = key.split('.');
  return parts.length === 2 && parts[0] === RISK_PROJECTION;
}

/** Whether a value is the number of risks, which reads "Number of risks" rather than "Count of …". */
export function isRiskCount(
  value: Pick<FieldValue, 'field'> & { aggregation: string | null },
): boolean {
  return value.field === RISK_COUNT_FIELD && value.aggregation === 'COUNT';
}

/** "Number of risks", as a title, a series and a column heading read it. */
export function riskCountText(language: Language): string {
  return translate(language, 'dashboards.designer.riskCount');
}

/** The risk fields whose value is the current assessment's: none when the risk is not assessed. */
const ASSESSMENT_CODES: readonly string[] = ['RATING', 'PROBABILITY', 'IMPACT', 'SCORE'];

/** The words for a risk field with no value: "Not assessed" for the assessment's, else null (the designer's own "not set"). */
export function riskMissingText(fieldCode: string, t: Translate): string | null {
  return ASSESSMENT_CODES.includes(fieldCode) ? t('risks.rating.none') : null;
}

/**
 * A risk CODE field's value in words: a status as the Risk feature names it; a rating, probability or impact level as the risk matrix in force
 * names it ("3 · Likely"); without the matrix, a usual rating's word (High) or the code itself.
 */
export function riskCodeText(
  fieldCode: string,
  value: string,
  language: Language,
  t: Translate,
  matrix: RiskMatrix | null,
): string {
  switch (fieldCode) {
    case 'STATUS': {
      const key = `risks.status.${value}`;
      const word = translate(language, key);
      return word === key ? value : word;
    }
    case 'RATING': {
      // The matrix's own words; where it is not read, the usual ratings' words, else the code.
      const own = `dashboards.designer.fieldValue.RISK.RATING.${value}`;
      const word = translate(language, own);
      return (
        matrix?.ratings.find((rating) => rating.code === value)?.label[language] ??
        (word === own ? value : word)
      );
    }
    case 'PROBABILITY': {
      const label = probabilityLabel(matrix, Number(value));
      return label === null
        ? value
        : t('risks.matrix.probabilityLevel', { level: value, label: label[language] });
    }
    case 'IMPACT': {
      const label = impactLabel(matrix, Number(value));
      return label === null
        ? t('risks.matrix.impactLevel', { level: value })
        : t('risks.matrix.probabilityLevel', { level: value, label: label[language] });
    }
    default:
      return value;
  }
}

/**
 * Where a cell of a table on Risks opens: a project cell its project; a risk title its risk, in its project's workspace (the row's project, or
 * the key of the row's project cell). Undefined for any other cell, and for a cell with no key.
 */
export function riskCellPath(
  column: TableColumn,
  cell: TableCell,
  row: TableRow,
  columns: readonly TableColumn[],
): string | undefined {
  if (column.kind !== 'FIELD' || column.aggregation !== 'NONE' || cell.key === null) {
    return undefined;
  }
  if (column.key === RISK_PROJECT_FIELD) {
    return `/projects/${cell.key}`;
  }
  if (column.key !== RISK_TITLE_FIELD) {
    return undefined;
  }
  const projectAt = columns.findIndex(
    (other) =>
      other.kind === 'FIELD' && other.key === RISK_PROJECT_FIELD && other.aggregation === 'NONE',
  );
  const projectId = row.projectId ?? (projectAt < 0 ? null : (row.cells[projectAt]?.key ?? null));
  return projectId === null ? undefined : `/projects/${projectId}/risks/${cell.key}`;
}

import { type BilingualLabel } from '@/features/identity-access/api/types.ts';
import { labelOfValue } from '@/features/dashboards/presentation.ts';
import { formatPercent } from '@/features/progress/presentation.ts';
import { formatSar, shortId } from '@/features/projects/presentation.ts';
import {
  DATE_LOCALES,
  type Language,
  type TranslationKey,
  type TranslationParams,
} from '@/shared/i18n/i18n.ts';
import { type StatusTone } from '@/shared/ui/StatusBadge.tsx';
import { type ValueStateIcon } from '@/shared/ui/ValueState.tsx';

import {
  type ReportAudienceFamily,
  type ReportCatalogueEntry,
  type ReportCell,
  type ReportColumnView,
  type ReportFilterOperator,
  type ReportJobStatus,
  type ReportUnknownReason,
  type ReportValueType,
  type SavedViewSummary,
} from './api/types.ts';

// How FG-02's payloads read on screen. Nothing here computes a business value (FG-02 §4.1): a cell is the source's own
// string in its unit, a code in its source's words, and a cell with no value is its reason in words — never blank,
// never 0, never green (FG-02 §7.1, FE-CMP-008).

type Translate = (key: TranslationKey, params?: TranslationParams) => string;

/**
 * SCR-131 to SCR-137: the seven standard report experiences, one per audience family, each with its address under
 * /reports and its screen id. A report is presented by the experience its definition names.
 */
export const FAMILIES: Record<
  ReportAudienceFamily,
  { slug: string; screen: string; label: TranslationKey; description: TranslationKey }
> = {
  EXECUTIVE: {
    slug: 'executive',
    screen: 'SCR-131',
    label: 'reports.family.EXECUTIVE.label',
    description: 'reports.family.EXECUTIVE.description',
  },
  PORTFOLIO: {
    slug: 'portfolio',
    screen: 'SCR-132',
    label: 'reports.family.PORTFOLIO.label',
    description: 'reports.family.PORTFOLIO.description',
  },
  DEPARTMENT: {
    slug: 'department',
    screen: 'SCR-133',
    label: 'reports.family.DEPARTMENT.label',
    description: 'reports.family.DEPARTMENT.description',
  },
  PROJECT: {
    slug: 'project',
    screen: 'SCR-134',
    label: 'reports.family.PROJECT.label',
    description: 'reports.family.PROJECT.description',
  },
  FINANCIAL: {
    slug: 'financial',
    screen: 'SCR-135',
    label: 'reports.family.FINANCIAL.label',
    description: 'reports.family.FINANCIAL.description',
  },
  PROGRESS: {
    slug: 'progress',
    screen: 'SCR-136',
    label: 'reports.family.PROGRESS.label',
    description: 'reports.family.PROGRESS.description',
  },
  RISK_ISSUE: {
    slug: 'risk-issue',
    screen: 'SCR-137',
    label: 'reports.family.RISK_ISSUE.label',
    description: 'reports.family.RISK_ISSUE.description',
  },
};

export const FAMILY_ORDER = Object.keys(FAMILIES) as ReportAudienceFamily[];

export function familyOfSlug(slug: string | undefined): ReportAudienceFamily | null {
  return FAMILY_ORDER.find((family) => FAMILIES[family].slug === slug) ?? null;
}

export function reportPath(family: ReportAudienceFamily, code: string): string {
  return `/reports/${FAMILIES[family].slug}/${code}`;
}

/**
 * Where a saved view opens: the explorer for a composition, its report's shell for a report's parameters — null when
 * the report is not in the person's catalogue (it cannot be run by them, whoever shared it).
 */
export function viewPath(view: SavedViewSummary, catalogue: ReportCatalogueEntry[]): string | null {
  if (view.viewType === 'EXPLORER_COMPOSITION') {
    return `/reports/explorer?view=${view.id}`;
  }
  const entry = catalogue.find((candidate) => candidate.code === view.reportCode);
  return entry === undefined
    ? null
    : `${reportPath(entry.audienceFamily, entry.code)}?view=${view.id}`;
}

/** ADR-019's saved-view roles: the only roles a view may be shared with (TBC-RPT-20). */
export const SHARE_ROLES = ['R02', 'R03', 'R07'] as const;

export type ShareRole = (typeof SHARE_ROLES)[number];

export function roleLabel(roleCode: string, t: Translate): string {
  return (SHARE_ROLES as readonly string[]).includes(roleCode)
    ? t(`reports.roles.${roleCode as ShareRole}`)
    : roleCode;
}

/** A bilingual label read in the interface language. */
export function inLanguage(label: BilingualLabel, language: Language): string {
  return label[language];
}

/** The API writes a text's entry language `AR`/`EN`; the `lang` attribute and the language toggle want `ar`/`en`. */
export function entryLanguage(language: string): Language {
  return language.toLowerCase() === 'ar' ? 'ar' : 'en';
}

const COUNT_FORMAT = new Intl.NumberFormat('en');
const DAYS_FORMAT = new Intl.NumberFormat('en', { signDisplay: 'exceptZero' });

function formatDate(value: string, language: Language): string {
  const date = new Date(`${value.slice(0, 10)}T00:00:00Z`);
  return Number.isNaN(date.getTime())
    ? value
    : new Intl.DateTimeFormat(DATE_LOCALES[language], {
        dateStyle: 'medium',
        timeZone: 'UTC',
      }).format(date);
}

/** The flag icon of each reason a value is absent. */
export const REASON_ICONS = {
  MISSING: 'missing',
  NOT_APPLICABLE: 'notApplicable',
  RESTRICTED: 'restricted',
  SOURCE_UNAVAILABLE: 'unavailable',
} as const satisfies Record<ReportUnknownReason, ValueStateIcon>;

/** What a cell shows: a value in words or figures (with its badge tone for a code), or the reason it has none. */
export type CellContent =
  | { kind: 'value'; text: string; tone: StatusTone | null; isText: boolean }
  | { kind: 'unknown'; reason: ReportUnknownReason };

/**
 * A cell as it reads. A masked cell is Restricted and an empty one with no reason is No data: a missing value is
 * never shown as blank or 0. Figures keep the API's exact strings, grouped, Latin digits in both languages.
 */
export function cellContent(
  cell: ReportCell,
  column: Pick<ReportColumnView, 'valueType' | 'projectionCode'>,
  language: Language,
  t: Translate,
  formatDateTime: (value: string) => string,
): CellContent {
  if (cell.isMasked) {
    return { kind: 'unknown', reason: 'RESTRICTED' };
  }
  if (cell.unknownReason !== null) {
    return { kind: 'unknown', reason: cell.unknownReason };
  }
  if (cell.value === null) {
    return { kind: 'unknown', reason: 'MISSING' };
  }
  const value = cell.value;
  const figure = (text: string) => ({ kind: 'value' as const, text, tone: null, isText: false });
  switch (column.valueType) {
    case 'CODE': {
      const label = labelOfValue(column.projectionCode ?? '', value, cell.label, language, t);
      return { kind: 'value', text: label.text, tone: label.tone, isText: false };
    }
    case 'REFERENCE':
      return {
        kind: 'value',
        text: cell.label?.[language] ?? t('reports.cell.reference', { id: shortId(value) }),
        tone: null,
        isText: true,
      };
    case 'TEXT':
      return { kind: 'value', text: value, tone: null, isText: true };
    case 'COUNT':
      return figure(COUNT_FORMAT.format(Number(value)));
    case 'PERCENT':
      return figure(formatPercent(value) ?? value);
    case 'DAYS':
      return figure(t('reports.units.days', { value: DAYS_FORMAT.format(Number(value)) }));
    case 'SAR':
      return figure(t('reports.units.sar', { amount: formatSar(value) }));
    case 'BOOLEAN':
      return figure(value === 'true' ? t('common.values.yes') : t('common.values.no'));
    case 'DATE':
      return figure(formatDate(value, language));
    case 'DATE_TIME':
      return figure(formatDateTime(value));
  }
}

/** A sort is offered on every type but a reference (the API's rule, ReportField.IsSortableType). */
export function isSortableType(valueType: ReportValueType): boolean {
  return valueType !== 'REFERENCE';
}

export const OPERATOR_LABELS: Record<ReportFilterOperator, TranslationKey> = {
  EQ: 'reports.operators.EQ',
  NEQ: 'reports.operators.NEQ',
  IN: 'reports.operators.IN',
  GT: 'reports.operators.GT',
  GTE: 'reports.operators.GTE',
  LT: 'reports.operators.LT',
  LTE: 'reports.operators.LTE',
  BETWEEN: 'reports.operators.BETWEEN',
  CONTAINS: 'reports.operators.CONTAINS',
};

/** A job not yet ended: the export history and MOD-060 keep reading it until it is. */
export function isJobActive(status: ReportJobStatus): boolean {
  return (
    status === 'REQUESTED' || status === 'VALIDATING' || status === 'QUEUED' || status === 'RUNNING'
  );
}

/** Cancellable while it has not started running (reports.md §4). */
export function isJobCancellable(status: ReportJobStatus): boolean {
  return status === 'REQUESTED' || status === 'VALIDATING' || status === 'QUEUED';
}

/** A job's state is a workflow state: blue while it works, red when it failed, grey otherwise — never green. */
export function jobTone(status: ReportJobStatus): StatusTone {
  if (isJobActive(status)) {
    return 'info';
  }
  return status === 'FAILED' ? 'negative' : 'neutral';
}

const FAILURE_CODES = new Set([
  'REPORT_EXPORT_NOT_PERMITTED',
  'REPORT_ACCESS_DENIED',
  'REPORT_NOT_ACTIVE',
  'REPORT_PARAMETER_UNAUTHORIZED',
  'REPORT_EXPORT_SIZE_LIMIT_EXCEEDED',
  'REPORT_OUTPUT_GENERATION_FAILED',
]);

/** A FAILED job's code in words (FG-02 REP-011: a code, never a message, from the API). */
export function failureMessage(code: string | null, t: Translate): string {
  return code !== null && FAILURE_CODES.has(code)
    ? t(`reports.failure.${code}` as TranslationKey)
    : t('reports.failure.unknown', { code: code ?? '—' });
}

const SIZE_UNITS = ['B', 'KB', 'MB'] as const;

export function formatSize(bytes: number): string {
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < SIZE_UNITS.length - 1) {
    value /= 1024;
    unit += 1;
  }
  return `${COUNT_FORMAT.format(Math.round(value * 10) / 10)} ${SIZE_UNITS[unit] ?? 'B'}`;
}

const ISSUE_CODES = new Set([
  'FIELD_NOT_ALLOWLISTED',
  'JOIN_NOT_ALLOWLISTED',
  'FIELD_NOT_IN_REPORT',
  'GRAIN_MISMATCH',
  'OPERATOR_NOT_SUPPORTED',
  'VALUE_INVALID',
  'NOT_FOUND',
  'NOT_ALLOWED',
  'REQUIRED',
  'DUPLICATE',
]);

/** Why part of a saved view no longer fits (SAV-012), in words; an unknown code is shown as it came. */
export function issueMessage(code: string, t: Translate): string {
  return ISSUE_CODES.has(code) ? t(`reports.issues.${code}` as TranslationKey) : code;
}

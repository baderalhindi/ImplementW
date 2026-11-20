import { type TranslationKey } from '@/shared/i18n/resources.ts';

import { type Section, type Visual } from './designApi.ts';

// A table section's columns the designer lets the Portfolio Manager hide (20 Nov 2026, docs/architecture/dashboard-designer-table-columns.md).
// The first column (what the row is) is always shown and not listed; at least one listed column stays shown.

export type TableSection =
  | 'PORTFOLIO_TABLE'
  | 'INITIATIVE_TABLE'
  | 'SECTOR_TABLE'
  | 'STATUS_TABLE'
  | 'CURRENT_STAGE_TABLE'
  | 'TOP_FOLLOW_UP'
  | 'ENTITY_COMPLETION_TABLE';

/** A listed column: its code, as saved in `hiddenColumns`, and the key of the table's own heading for it. */
export interface SectionColumn {
  code: string;
  label: TranslationKey;
}

/** One table of a section: its caption's key where the section has more than one table (the sector's), and its listed columns. */
export interface SectionTable {
  caption: TranslationKey | null;
  columns: readonly SectionColumn[];
}

/** The codes a table section's drawing leaves out; empty when every column is shown. */
export type HiddenColumns = ReadonlySet<string>;

export const NO_HIDDEN_COLUMNS: HiddenColumns = new Set<string>();

export const BREAKDOWN_COLUMNS: readonly SectionColumn[] = [
  { code: 'PROJECTS', label: 'dashboards.insights.projects' },
  { code: 'VALUE', label: 'dashboards.insights.value' },
  { code: 'MEASURED', label: 'dashboards.insights.measured' },
  { code: 'AVERAGE_PLANNED', label: 'dashboards.insights.averagePlanned' },
  { code: 'AVERAGE_ACTUAL', label: 'dashboards.insights.averageActual' },
  { code: 'GAP', label: 'dashboards.insights.gap' },
  { code: 'ON_PLAN', label: 'dashboards.insights.label' },
  { code: 'NEEDING_FOLLOW_UP', label: 'dashboards.insights.needingFollowUp' },
];

export const SECTOR_SPEND_COLUMNS: readonly SectionColumn[] = [
  { code: 'PLANNED_SPEND', label: 'dashboards.insights.plannedSpend' },
  { code: 'ACTUAL_SPEND', label: 'dashboards.insights.actualSpend' },
  { code: 'SPLIT', label: 'dashboards.insights.split' },
];

const SECTION_TABLES: Record<TableSection, readonly SectionTable[]> = {
  PORTFOLIO_TABLE: [{ caption: null, columns: BREAKDOWN_COLUMNS }],
  INITIATIVE_TABLE: [{ caption: null, columns: BREAKDOWN_COLUMNS }],
  SECTOR_TABLE: [
    { caption: 'dashboards.insights.bySector', columns: BREAKDOWN_COLUMNS },
    { caption: 'dashboards.insights.sectorSpend', columns: SECTOR_SPEND_COLUMNS },
  ],
  STATUS_TABLE: [
    {
      caption: null,
      columns: [
        { code: 'PROJECTS', label: 'dashboards.insights.projects' },
        { code: 'PERCENT', label: 'dashboards.insights.percent' },
      ],
    },
  ],
  CURRENT_STAGE_TABLE: [
    {
      caption: null,
      columns: [
        { code: 'PROJECTS', label: 'dashboards.insights.projects' },
        { code: 'VALUE', label: 'dashboards.insights.value' },
      ],
    },
  ],
  TOP_FOLLOW_UP: [
    {
      caption: null,
      columns: [
        { code: 'TITLE', label: 'projects.fields.title' },
        { code: 'PORTFOLIO', label: 'dashboards.insights.portfolio' },
        { code: 'REASON', label: 'projects.followUpList.reason' },
        { code: 'OBSTACLE', label: 'projects.followUpList.obstacle' },
        { code: 'NEXT_ACTION', label: 'projects.followUpList.nextAction' },
      ],
    },
  ],
  ENTITY_COMPLETION_TABLE: [
    {
      caption: null,
      columns: [
        { code: 'ISSUED', label: 'externalParticipation.completion.issued' },
        { code: 'SUBMITTED', label: 'externalParticipation.completion.submitted' },
        { code: 'ACCEPTED', label: 'externalParticipation.completion.accepted' },
        { code: 'OVERDUE', label: 'externalParticipation.completion.overdue' },
        { code: 'PERCENT', label: 'externalParticipation.completion.percent' },
      ],
    },
  ],
};

export function isTableSection(section: Section | null | undefined): section is TableSection {
  return section !== null && section !== undefined && section in SECTION_TABLES;
}

/** A table section's tables and their listed columns; none for any other section. */
export function sectionTables(section: Section | null | undefined): readonly SectionTable[] {
  return isTableSection(section) ? SECTION_TABLES[section] : [];
}

function listedCodes(section: Section | null | undefined): string[] {
  return sectionTables(section).flatMap((table) => table.columns.map((column) => column.code));
}

export function hiddenColumnsOf(visual: Pick<Visual, 'hiddenColumns'>): HiddenColumns {
  return visual.hiddenColumns === undefined || visual.hiddenColumns === null
    ? NO_HIDDEN_COLUMNS
    : new Set(visual.hiddenColumns);
}

/** Whether any of `columns` is shown: a table none of whose listed columns is shown is left out. */
export function anyShown(columns: readonly SectionColumn[], hidden: HiddenColumns): boolean {
  return columns.some((column) => !hidden.has(column.code));
}

/** What a visual sends: on a table section the hidden listed codes in the table's order, null when none; on any other visual nothing. */
export function sentHiddenColumns(
  visual: Pick<Visual, 'section' | 'hiddenColumns'>,
): string[] | null | undefined {
  if (!isTableSection(visual.section)) {
    return undefined;
  }
  const hidden = hiddenColumnsOf(visual);
  const codes = listedCodes(visual.section).filter((code) => hidden.has(code));
  return codes.length === 0 ? null : codes;
}

/** The visual with a listed column shown or hidden; the last shown one stays shown. */
export function withColumnShown(visual: Visual, code: string, shown: boolean): Visual {
  const hidden = new Set(hiddenColumnsOf(visual));
  if (shown) {
    hidden.delete(code);
  } else if (listedCodes(visual.section).some((other) => other !== code && !hidden.has(other))) {
    hidden.add(code);
  }
  const next = sentHiddenColumns({ section: visual.section, hiddenColumns: [...hidden] });
  return { ...visual, hiddenColumns: next ?? null };
}

/** Whether a listed column is the last one shown, which may not be hidden. */
export function isLastShown(visual: Visual, code: string): boolean {
  const hidden = hiddenColumnsOf(visual);
  return (
    !hidden.has(code) &&
    listedCodes(visual.section).every((other) => other === code || hidden.has(other))
  );
}

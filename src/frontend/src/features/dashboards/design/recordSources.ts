import {
  type FieldDescriptor,
  type GroupBy,
  type RecordSource,
  type Visual,
  type VisualType,
} from './designApi.ts';
import { NO_FIELD_DATA } from './fields.ts';
import { asHeatmap } from './heatmap.ts';
import { RISK_PROJECT_DIMENSIONS, RISK_TABLE_COLUMNS, riskFigures } from './riskFields.ts';
import { NEW_TABLE_COLUMNS } from './tableColumns.ts';
import { asLine, firstGrouping, groupBysFor, newVisual, VISUAL_TYPES } from './visuals.ts';

// What a visual counts (20 Nov 2026, docs/architecture/dashboard-designer-record-sources.md): projects, one row each (the default), or risks.
// A visual on Risks draws risk fields only — no Portfolio figures — grouped by a risk field or a project attribute through the risk's project.
// The API checks the same rules (PortfolioDesignService).

/** The record sources, in the Source select's order. */
export const RECORD_SOURCES: readonly RecordSource[] = ['PROJECTS', 'RISKS'];

/** The types that may count another source than projects: a gauge, a map, a text box, a section and a container count projects only. */
export const SOURCE_TYPES: readonly VisualType[] = [
  'CARD',
  'BAR',
  'COLUMN',
  'PIE',
  'DONUT',
  'LINE',
  'TABLE',
  'HEATMAP',
];

/** Whether a visual of `type` offers the Source select. */
export function takesSource(type: VisualType): boolean {
  return SOURCE_TYPES.includes(type);
}

/** The records a visual counts: projects unless it says otherwise. */
export function sourceOf(visual: Pick<Visual, 'source'>): RecordSource {
  return visual.source ?? 'PROJECTS';
}

/** The types a visual on `source` may change to. */
export function typesFor(source: RecordSource): readonly VisualType[] {
  return source === 'PROJECTS' ? VISUAL_TYPES : VISUAL_TYPES.filter(takesSource);
}

/** The project attributes a visual of `type` on `source` may be grouped by: a risk's through its project, which has no stage or on-plan indicator. */
export function groupBysOf(type: VisualType, source: RecordSource): readonly GroupBy[] {
  const groupings = groupBysFor(type);
  return source === 'PROJECTS'
    ? groupings
    : groupings.filter(
        (groupBy) =>
          groupBy === 'NONE' || (RISK_PROJECT_DIMENSIONS as readonly GroupBy[]).includes(groupBy),
      );
}

/**
 * The visual counting `source`, its figures replaced by that source's first ones: on Risks the number of risks (a chart grouped by rating, a
 * line by the month identified, a table listing the risks); on Projects what a new visual of its type starts with (`projectFields`, the
 * projects' catalogue, gives a line its first value). Its place, size, titles and axes are kept; its filters, which tested the other source,
 * are not. The visual unchanged when it already counts `source`, or its type takes no other.
 */
export function withSource(
  visual: Visual,
  source: RecordSource,
  projectFields: readonly FieldDescriptor[] = [],
): Visual {
  if (sourceOf(visual) === source || !takesSource(visual.visualType)) {
    return visual;
  }
  const type = visual.visualType;
  const cleared: Visual = {
    ...visual,
    ...NO_FIELD_DATA,
    source: source === 'PROJECTS' ? null : source,
    columns: null,
    showTotals: null,
  };
  if (type === 'TABLE') {
    return {
      ...cleared,
      measures: [],
      groupBy: 'NONE',
      columns: [...(source === 'RISKS' ? RISK_TABLE_COLUMNS : NEW_TABLE_COLUMNS)],
      showTotals: true,
    };
  }
  if (type === 'HEATMAP') {
    // A heatmap starts again on the new source's first two axis fields, the risk matrix on Risks (heatmap.ts).
    return source === 'RISKS'
      ? { ...cleared, ...riskFigures(type) }
      : (asHeatmap({ ...cleared, measures: [], groupBy: 'NONE' }, projectFields) ?? cleared);
  }
  if (source === 'RISKS') {
    return { ...cleared, ...riskFigures(type) };
  }
  if (type === 'LINE') {
    return asLine({ ...cleared, measures: [], groupBy: 'NONE' }, projectFields) ?? cleared;
  }
  // A new visual's first figures for the type: the gallery's.
  const fresh = newVisual(type, [], projectFields);
  return {
    ...cleared,
    measures: fresh?.measures ?? ['PROJECTS'],
    groupBy: fresh?.groupBy ?? firstGrouping(type),
  };
}

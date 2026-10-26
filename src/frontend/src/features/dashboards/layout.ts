import { type DashboardWidgetResult } from './api/types.ts';
import { chartForm } from './charts.ts';

// The order widgets are drawn in. The governed layout is a twelve-column grid, row by row (TASK-069 D-13); drawn in
// reading order with each widget's span, a grid that flows fills the same places, and mirrors itself in Arabic.
// ADR-019's personal choices change presentation only (DSH-CC-29): hidden widgets are left out, and a personal order
// moves the optional widgets among their own places — a governed widget never moves.

function governedOrder(a: DashboardWidgetResult, b: DashboardWidgetResult): number {
  return a.layoutRow - b.layoutRow || a.layoutColumn - b.layoutColumn;
}

/** Every widget in the governed order with the caller's order applied to the optional ones (hidden included). */
export function arrangedWidgets(widgets: DashboardWidgetResult[]): DashboardWidgetResult[] {
  const governed = [...widgets].sort(governedOrder);
  const optional = governed.filter((widget) => widget.isOptionalVisibility);
  const rank = (widget: DashboardWidgetResult) =>
    widget.personalSortOrder ?? optional.indexOf(widget);
  const personal = [...optional].sort((a, b) => rank(a) - rank(b));
  let next = 0;
  return governed.map((widget) =>
    widget.isOptionalVisibility ? (personal[next++] ?? widget) : widget,
  );
}

/**
 * The widgets the canvas draws: not hidden by the person, and not restricted outright — a widget the person may not see at all is left
 * out rather than drawn as "restricted" (the 24 Oct 2026 review, M-5). One that shows part of its data, with the rest masked, stays.
 * They are grouped by what they draw (`GROUPS`), each group in the arranged order.
 */
export function visibleWidgets(widgets: DashboardWidgetResult[]): DashboardWidgetResult[] {
  const shown = arrangedWidgets(widgets).filter(
    (widget) =>
      !widget.isHidden && !(widget.unknownReason === 'RESTRICTED' && widget.data === null),
  );
  return GROUPS.flatMap((group) => shown.filter((widget) => groupOf(widget) === group));
}

/**
 * What a widget draws, in the order the groups are drawn: figure cards (METRIC_CARD and the PROGRESS_INDICATOR gauge),
 * then bar and column charts, then donuts, then trends.
 */
const GROUPS = ['figure', 'bars', 'donut', 'trend'] as const;
type Group = (typeof GROUPS)[number];

function groupOf(widget: DashboardWidgetResult): Group {
  switch (widget.widgetType) {
    case 'METRIC_CARD':
    case 'PROGRESS_INDICATOR':
      return 'figure';
    case 'LINE_TREND':
      return 'trend';
    default:
      return chartForm(widget.widgetType, widget.data?.distribution.length ?? 0) === 'donut'
        ? 'donut'
        : 'bars';
  }
}

/** Columns a chart takes, so charts of a kind sit side by side; a figure card keeps its governed span. */
const GROUP_SPANS: Record<Exclude<Group, 'figure'>, number> = { bars: 6, donut: 4, trend: 12 };

/** The columns a widget asks for before its row is filled (`filledSpans`). */
export function drawnSpan(widget: DashboardWidgetResult): number {
  const group = groupOf(widget);
  return group === 'figure' ? widget.layoutSpan : GROUP_SPANS[group];
}

const COLUMNS = 12;

/**
 * Each widget's span in the drawn order, packed into rows of twelve: a row the next widget does not fit shares its spare
 * columns among its widgets (any remainder to the last), so the grid never has a hole.
 */
export function filledSpans(spans: number[]): number[] {
  const filled = spans.map((span) => Math.min(COLUMNS, Math.max(1, span)));
  const rows: number[][] = [];
  let used = COLUMNS;
  filled.forEach((span, index) => {
    if (used + span > COLUMNS) {
      rows.push([]);
      used = 0;
    }
    rows.at(-1)?.push(index);
    used += span;
  });
  rows.forEach((row) => {
    const spare = COLUMNS - row.reduce((sum, index) => sum + (filled[index] ?? 0), 0);
    const share = Math.floor(spare / row.length);
    row.forEach((index, position) => {
      const remainder = position === row.length - 1 ? spare - share * row.length : 0;
      filled[index] = (filled[index] ?? 0) + share + remainder;
    });
  });
  return filled;
}

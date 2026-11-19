import { type DashboardWidgetResult } from '../api/types.ts';
import { filledSpans, visibleWidgets } from '../layout.ts';
import { type Visual } from './designApi.ts';
import { COLUMNS, freePlace } from './visuals.ts';

// One visual per governed widget (19 Nov 2026, docs/architecture/dashboard-designer-widgets.md): the API cannot read the dashboard's widget
// codes, so the frontend turns a DASHBOARD_WIDGETS section (all of them in one visual) into a DASHBOARD_WIDGET visual per widget, and offers
// the widgets no visual draws.

/** Rows a widget's visual takes: a metric card is a figure, any other widget a chart. */
function widgetHeight(widget: DashboardWidgetResult): number {
  return widget.widgetType === 'METRIC_CARD' ? 5 : 7;
}

/** Columns a widget's visual takes: its governed span, in the same twelve columns. */
function widgetWidth(widget: DashboardWidgetResult): number {
  return Math.min(COLUMNS, Math.max(1, widget.layoutSpan));
}

/** A visual drawing `widget`, titled with the widget's own title, at a place. */
function widgetVisual(
  widget: DashboardWidgetResult,
  layoutColumn: number,
  layoutRow: number,
  layoutWidth = widgetWidth(widget),
): Visual {
  return {
    title: { ar: widget.title.ar, en: widget.title.en },
    visualType: 'SECTION',
    measures: [],
    groupBy: 'NONE',
    section: 'DASHBOARD_WIDGET',
    widgetCode: widget.code,
    layoutColumn,
    layoutRow,
    layoutWidth,
    layoutHeight: widgetHeight(widget),
  };
}

/** The widget codes the design's DASHBOARD_WIDGET visuals draw. */
function placedCodes(visuals: readonly Visual[]): Set<string> {
  return new Set(
    // A widget's visual may be in a container.
    visuals
      .flatMap((visual) => [visual, ...(visual.children ?? [])])
      .flatMap((visual) =>
        visual.section === 'DASHBOARD_WIDGET' &&
        visual.widgetCode !== undefined &&
        visual.widgetCode !== null
          ? [visual.widgetCode]
          : [],
      ),
  );
}

/** Whether the design has a DASHBOARD_WIDGETS section the view's widgets can replace. */
export function hasWidgetsToSplit(
  visuals: readonly Visual[],
  widgets: DashboardWidgetResult[],
): boolean {
  return (
    visuals.some((visual) => visual.section === 'DASHBOARD_WIDGETS') &&
    visibleWidgets(widgets).length > 0
  );
}

/**
 * The design with each DASHBOARD_WIDGETS section replaced, in its place in the order, by a DASHBOARD_WIDGET visual per visible widget (in
 * `visibleWidgets` order) that no other visual already draws, laid out row by row from the section's old place. Each takes its governed span,
 * a row's spare columns shared among its widgets as the widget grid always drew them (`filledSpans`), so the rows leave no empty columns.
 * The caller packs the result. A design whose view has no visible widget is returned as it is.
 */
export function splitWidgets<T extends Visual>(
  visuals: readonly T[],
  widgets: DashboardWidgetResult[],
  make: (visual: Visual) => T,
): T[] {
  if (!hasWidgetsToSplit(visuals, widgets)) {
    return [...visuals];
  }
  const placed = placedCodes(visuals);
  const missing = visibleWidgets(widgets).filter((widget) => !placed.has(widget.code));
  let split = false;
  return visuals.flatMap((visual) => {
    if (visual.section !== 'DASHBOARD_WIDGETS') {
      return [visual];
    }
    // A second DASHBOARD_WIDGETS section has nothing left to draw: every widget is a visual of the first.
    if (split) {
      return [];
    }
    split = true;
    const widths = filledSpans(missing.map(widgetWidth));
    let column = 0;
    let row = visual.layoutRow;
    let rowHeight = 0;
    return missing.map((widget, index) => {
      const width = widths[index] ?? widgetWidth(widget);
      if (column + width > COLUMNS) {
        column = 0;
        row += rowHeight;
        rowHeight = 0;
      }
      const next = widgetVisual(widget, column, row, width);
      column += width;
      rowHeight = Math.max(rowHeight, next.layoutHeight);
      return make(next);
    });
  });
}

/**
 * The view's visible widgets no visual draws, in `visibleWidgets` order: none while a DASHBOARD_WIDGETS section draws them all.
 */
export function unplacedWidgets(
  visuals: readonly Visual[],
  widgets: DashboardWidgetResult[],
): DashboardWidgetResult[] {
  if (visuals.some((visual) => visual.section === 'DASHBOARD_WIDGETS')) {
    return [];
  }
  const placed = placedCodes(visuals);
  return visibleWidgets(widgets).filter((widget) => !placed.has(widget.code));
}

/** A new visual drawing `widget` at the first free place; null when the grid has no room left for it. */
export function newWidgetVisual(widget: DashboardWidgetResult, visuals: Visual[]): Visual | null {
  const place = freePlace(visuals, widgetWidth(widget), widgetHeight(widget));
  return place === null ? null : widgetVisual(widget, place.column, place.row);
}

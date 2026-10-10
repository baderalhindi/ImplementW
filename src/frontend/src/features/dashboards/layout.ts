import { type DashboardWidgetResult } from './api/types.ts';

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

/** The widgets the dashboard shows. */
export function visibleWidgets(widgets: DashboardWidgetResult[]): DashboardWidgetResult[] {
  return arrangedWidgets(widgets).filter((widget) => !widget.isHidden);
}

# Portfolio Dashboard Designer — one visual per governed widget (contract, 19 Nov 2026)

Extends the designer contracts (`dashboard-designer*.md`). The user (19 Nov 2026): the "Dashboard widgets" section draws every governed
widget of the Portfolio dashboard (ADM-036's definition: "Open risks by rating", "Projects by lifecycle state", the financial cards…) inside
one visual; each must be **its own visual**, moved, sized, titled and removed on its own.

## Model

- `PortfolioSection` gains `DashboardWidget = 13`: a section visual that draws **one** governed widget, named by a new visual property
  **`widgetCode`** (the widget's `code` in the dashboard view, 1–50 characters `^[A-Za-z0-9_.-]+$`). `widgetCode` is set exactly when the
  section is `DASHBOARD_WIDGET`, null otherwise.
- Several `DASHBOARD_WIDGET` visuals may be on a design, one per widget code: the unique index on `section` no longer covers
  `DASHBOARD_WIDGET`, and a new filtered unique index covers `widget_code`. Rule: no two visuals with the same `widgetCode` (422
  `PROJECT_PORTFOLIO_DESIGN_INVALID`, path `visuals[i].widgetCode`, `DUPLICATE`). The Project module cannot read the Dashboards module's
  definitions (edge 47), so the API checks the code's shape only; a code the dashboard no longer has draws nothing.
- The existing `DASHBOARD_WIDGETS` section (all of them in one visual) stays valid for designs already saved.
- Storage: nullable `widget_code character varying(50)` on `project.portfolio_visual`; CHECK `(section = 'DASHBOARD_WIDGET') = (widget_code IS NOT
  NULL)`; migration `TASK-112_AddPortfolioVisualWidgets` (DROP CONSTRAINT/INDEX in Up only through `migrationBuilder.Sql(... --
  EXPAND-THEN-CONTRACT-REVIEWED: ...)`).
- API: `widgetCode?: string | null` on the visual (input, result, request), echoed.

## Standard design and existing designs (frontend)

The backend cannot know the widget codes, so the **frontend** splits: when the design read (standard or saved) has a `DASHBOARD_WIDGETS`
section and the dashboard view has widgets, the **designer** replaces that section with one `DASHBOARD_WIDGET` visual per visible widget
(`visibleWidgets(view.widgets)` order), titled with the widget's own `title`, laid out inside the section's old area: each widget's width is
its `layoutSpan` (in the same 12 columns), its height 5 rows for a metric card and 7 for any other widget type; then `packed()`. The change is
a normal unsaved edit (the "unsaved changes" state shows) until Save. The **dashboard** (DesignedVisuals) does the same split when it draws
an unsplit `DASHBOARD_WIDGETS` section, so a viewer sees separate visuals before anyone saves.

## Drawing

- A `DASHBOARD_WIDGET` visual draws its widget with the existing `WidgetCard` (same states, coverage line, "Live · not yet approved" badge,
  Open link), filling its visual; its inner title is hidden (the visual's own title shows, as other sections do). A widget the caller
  may not see, hidden, or no longer in the view leaves no hole (left out, as other withheld visuals).
- Settings: like other sections — titles (default the widget's own) and size, and "Remove from dashboard".
- Gallery: under the visual types, a **"Widgets"** group lists only the view's widgets **not on the design**, each a tile with its title and
  "+ Add"; the group is absent when every widget is placed. (This is not the removed "Dashboard sections" list: it holds only what is
  missing.)

## Who builds what (parallel; do not edit another's files; do not commit — the lead integrates)

| Agent | Owns |
| --- | --- |
| Backend | everything under `src/backend/` (unit + integration tests), `docs/architecture/erd.dbml` and `erd.md`, `docs/api/openapi.v1.json` (regenerate with `.github/scripts/openapi-publish.sh`, copy `artifacts/api/openapi.v1.json` over it) |
| Frontend | everything under `src/frontend/` (incl. tests and fixtures), `artifacts/designer/*.mjs` |

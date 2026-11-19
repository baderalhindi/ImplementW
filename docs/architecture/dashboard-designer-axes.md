# Portfolio Dashboard Designer — chart axes, and no sections list (contract, 19 Nov 2026)

Extends [dashboard-designer-data-fields.md](dashboard-designer-data-fields.md) and
[dashboard-designer-more-fields.md](dashboard-designer-more-fields.md). The user's decisions (19 Nov 2026):

1. **The "Dashboard sections" list is removed** from the designer's gallery. Sections already on the design stay and can still be moved,
   sized and removed; a removed section comes back only with "Restore the standard design". Nothing changes in the API for this.
2. **Axes, Power BI style**, for `BAR`, `COLUMN` and `LINE`.

## Axes — what the user sees

- The Data panel of a bar, column or line chart is two boxes, as Power BI's field wells:
  - **X-axis** for column and line, **Y-axis** for bar (the category axis): what the chart is grouped by — the existing Group by choice
    (project attributes, status, yes/no and date fields, with the Year/Month select).
  - **Y-axis** for column and line, **X-axis** for bar (the value axis): the values and their calculations — the existing values list,
    search and field groups.
  - Filters stay below, unchanged. Other visual types keep today's panel.
- A **Format** part below the boxes (only for these three types):
  - Category axis: **Show axis** (on by default), **Axis title** in Arabic and English (empty = none).
  - Value axis: **Show axis** (on), **Axis title** Arabic/English, **Start** and **End** (empty = automatic), **Gridlines** (on).
- The charts draw real axes: the value axis with round ticks and gridlines and the values' format (`n%`, SAR compact, count, days, date);
  the category axis with the group labels; the titles beside their axes. Hidden axis = no line, ticks or labels (the bars stay). A start/end
  outside the data clips the bars at the edge. Arabic mirrors the category axis of a column chart (first group on the right) as the existing
  charts do; a line's time axis stays left to right.

## API (camelCase JSON) — an optional `axes` on the visual

```jsonc
"axes": {                                   // optional; null or absent = the defaults (both shown, no titles, automatic range, gridlines)
  "category": { "show": true, "title": { "ar": "المحفظة", "en": "Portfolio" } },          // title optional (null)
  "value":    { "show": true, "title": null, "min": 0, "max": 100, "gridlines": true }   // min/max optional (null = automatic)
}
```

C# (`PortfolioDesign.cs`): `PortfolioAxis(bool Show, BilingualLabel? Title)`, `PortfolioValueAxis(bool Show, BilingualLabel? Title,
decimal? Min, decimal? Max, bool Gridlines)`, `PortfolioAxes(PortfolioAxis Category, PortfolioValueAxis Value)`; `PortfolioVisualInput` and
`PortfolioVisualResult` gain a trailing optional `PortfolioAxes? Axes = null`; the request model too. Echoed on every result.

Rules (422 `PROJECT_PORTFOLIO_DESIGN_INVALID`, path `visuals[i].axes…`): only `BAR`, `COLUMN`, `LINE` take `axes`; a title, when given, has
both languages, each 1–60 characters after trimming; `min` < `max` when both are given; each between −10¹² and 10¹².

Storage: one nullable `jsonb` column `axes` on `project.portfolio_visual` (camelCase, as the API), migration `TASK-112_AddPortfolioVisualAxes`;
null when the visual has the defaults or is not a bar, column or line. Saving a visual whose axes equal the defaults stores null.

## Who builds what (parallel; do not edit another's files; do not commit — the lead integrates)

| Agent | Owns |
| --- | --- |
| Backend | everything under `src/backend/` (incl. unit + integration tests: `PortfolioDesignRulesTests.cs`, `PortfolioDesignTests.cs`), `docs/architecture/erd.dbml`, `docs/api/openapi.v1.json` (regenerate with `.github/scripts/openapi-publish.sh`, then copy `artifacts/api/openapi.v1.json` over it) |
| Frontend | everything under `src/frontend/` (incl. tests and fixtures), `artifacts/designer/*.mjs` |

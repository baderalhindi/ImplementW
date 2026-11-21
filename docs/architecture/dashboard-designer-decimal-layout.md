# Portfolio Dashboard Designer — decimal sizes and places (contract, 20 Nov 2026)

Extends the designer contracts (`dashboard-designer*.md`; placement is free; containers hold children on their inner grid). The user
(20 Nov 2026): "I want to take 4.5 or 4.7, like float numbers, not fixed on something, so I can adjust freely."

## Rule

`layoutColumn`, `layoutRow`, `layoutWidth`, `layoutHeight` — of every visual, containers and children included — are **decimals with at most
one decimal place** (steps of 0.1). Ranges are unchanged in meaning: width 1.0–12.0 columns, height 1.0–24.0 rows, column ≥ 0 and
column + width ≤ 12 (a child: ≤ its container's width), row ≥ 0; a container's height ≥ 2.0. Two visuals overlap when their rectangles overlap
by more than 0 (touching edges do not). A value with more than one decimal place is refused (422 `PROJECT_PORTFOLIO_DESIGN_INVALID`,
`visuals[i].layout` `OUTSIDE_RANGE`); whole numbers stay valid, so every saved design reads as before.

## Backend

- API (request and result): the four layout properties become JSON numbers that may carry one decimal (C# `decimal`).
- `project.portfolio_visual.layout_column|layout_row|layout_width|layout_height`: `smallint` → `numeric(4,1)`; the layout CHECKs keep their
  ranges on the decimal columns. Migration `TASK-112_AddPortfolioVisualDecimalLayout` (a type change of a column in use: write it so the
  migration dry-run gate passes — any DROP CONSTRAINT through `migrationBuilder.Sql(... -- EXPAND-THEN-CONTRACT-REVIEWED: ...)`; `ALTER
  COLUMN ... TYPE numeric(4,1)` keeps every value).
- Every rule that compares places (overlap, fit in the grid, fit in a container, standard design) works on decimals.

## Frontend

- **Drawing:** the grid is no longer CSS grid placement (which spans whole tracks only). On screens of 48rem and wider, each visual (and each
  child in its container) is positioned absolutely from its four numbers: with `col = (canvas width − 11 × gap) / 12` and `row` the row
  height, `left = column × (col + gap)`, `width = width × col + (width − 1) × gap`, the same for rows; the canvas (and a container's inner area)
  is as tall as its lowest visual. Arabic mirrors (inset-inline-start). Under 48rem visuals stack in reading order as today. The designer's
  background grid lines still mark whole columns and rows. The dashboard draws the same way.
- **Settings:** Width and Height inputs take one decimal (`step="0.1"`), hints "Columns, 1 to 12" / "Rows, 1 to 24" (say decimals are
  allowed, e.g. "1 to 12, e.g. 4.5").
- **Drag and resize:** snap to 0.1 of a column/row (not whole cells); arrow keys keep whole steps, Shift+arrow moves/sizes by 0.1.
- **Layout helpers** (`visuals.ts`, `containers.ts`: `settled`, `overlaps`, `fits`, `freePlace`, `withoutEmptyRows`, `withoutEmptyInnerRows`,
  `placedInContainer`, `containerMinSize`, …) work on decimals; every computed value is rounded to one decimal (`Math.round(v * 10) / 10`) so
  no 4.699999 is sent. `withoutEmptyRows` removes empty bands of any height (intervals), not whole rows only.

## Who builds what (parallel; do not edit another's files; do not commit — the lead integrates)

| Agent | Owns |
| --- | --- |
| Backend | everything under `src/backend/` (unit + integration tests), `docs/architecture/erd.dbml`/`erd.md`, `docs/api/openapi.v1.json` |
| Frontend | everything under `src/frontend/` (incl. tests and fixtures), `artifacts/designer/*.mjs` |

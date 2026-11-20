# Portfolio Dashboard Designer — a Power BI-style table (contract, 20 Nov 2026)

Extends the designer contracts (`dashboard-designer*.md`). The user (20 Nov 2026): the table's Columns should be "like Power BI: group by and
aggregation — by default Don't summarize, otherwise Count, Count distinct, Sum…". Two parts, both built now:

1. The **TABLE** visual takes **columns**: each a dimension, a data field or a portfolio figure, with a calculation per column. The columns
   left as *Don't summarize* are what the rows are grouped by (Power BI's implicit grouping); the others are calculated within each row.
2. **Make editable** on the table sections `PORTFOLIO_TABLE`, `INITIATIVE_TABLE`, `SECTOR_TABLE`, `STATUS_TABLE`, `CURRENT_STAGE_TABLE`:
   replaces the section with a TABLE visual set up with the same columns (frontend only). `TOP_FOLLOW_UP` and `ENTITY_COMPLETION_TABLE` keep
   their own logic (ranking, completion rates) and are not offered; they keep the hide/show columns of `dashboard-designer-table-columns.md`.

## Columns (API, camelCase JSON) — on a `TABLE` visual only

```jsonc
"columns": [                                                        // 1–12; when present the visual's measures [], values null,
  { "kind": "DIMENSION", "key": "SECTOR",  "aggregation": "NONE" },  // groupBy NONE, groupField null
  { "kind": "MEASURE",   "key": "PROJECTS", "aggregation": null },
  { "kind": "FIELD",     "key": "PROJECT.PROJECT_PROFILE.REGISTRATION_BUDGET", "aggregation": "SUM" }
],
"showTotals": true                                                  // optional, default true
```

- **DIMENSION** keys: the `PortfolioGroupBy` dimensions `PORTFOLIO`, `INITIATIVE`, `SECTOR`, `DEPARTMENT`, `ENTITY`, `STATUS`, `STAGE`, `ON_PLAN`,
  `REGION`, `CITY`, `PRIORITY`, plus `TITLE` and `FORMAL_PROJECT_ID` (one per project). Aggregations: `NONE`, `COUNT` (projects with a value),
  `COUNT_DISTINCT` (distinct values). Each needs the permission its grouping needs today (`PermissionOf(groupBy)`; `TITLE`/`FORMAL_PROJECT_ID`
  as `NONE` does).
- **FIELD** keys: the designer's field catalogue (`/design/fields`), project grain only. Aggregations: the field type's existing ones plus `NONE`
  (group by its value) and `COUNT_DISTINCT`.
- **MEASURE** keys: the portfolio figures `PROJECTS`, `IN_PROGRESS`, `VALUE_SAR`, `AVERAGE_PLANNED`, `AVERAGE_ACTUAL`, `GAP_POINTS`,
  `NEEDING_FOLLOW_UP`, `OVERDUE_ACTIONS`, plus new `MEASURED` (projects with progress measured), `ON_PLAN` (the row's on-plan status, as a
  breakdown row has it) and `PROJECT_SHARE` (the row's projects ÷ all the table's projects × 100). `aggregation` is null: a measure is
  calculated over the row's projects exactly as the insights calculate it for a group (same masking of amounts).
- Rules (422 `PROJECT_PORTFOLIO_DESIGN_INVALID`, path `visuals[i].columns[j]…`): only on `TABLE` (`NOT_ALLOWED`); 1–12 columns; a known key of
  its kind; an aggregation allowed for it (`NONE` allowed for DIMENSION and FIELD only; MEASURE takes null); no two equal (kind, key,
  aggregation); with `columns`, the visual's own `measures`/`values`/`groupBy`/`groupField` are empty/null/`NONE`; filters still apply.
- Storage: inside the existing `data_fields` jsonb (`{"columns":[…],"showTotals":…}` beside the existing keys); no new column; the CHECK
  that a non-section visual has measures or data_fields still holds (a column table has `data_fields`).

## Calculation and result

The rows are the visual's population (as today: scope, dashboard filters, the visual's filters) grouped by the tuple of its `NONE` columns'
values (`TITLE`/`FORMAL_PROJECT_ID` → one row per project); with no `NONE` column, one row. Each other column is calculated over the row's
projects. Rows ordered by their first `NONE` column's label (then the next), ascending; at most 500 rows (`truncated: true` beyond).

```jsonc
"table": {
  "columnTypes": ["ITEM", "COUNT", "SAR"],   // per column, for formatting: ITEM (an item/entity/department with a label), CODE, TEXT, COUNT,
                                               // PERCENT, DAYS, SAR, DATE (day number as values today), BOOLEAN, ON_PLAN, POINTS
  "rows": [ { "projects": 30, "projectId": null,
              "cells": [ { "key": "<guid>", "label": {"ar":"الصحة","en":"الصحة"}, "value": null },
                         { "key": null, "label": null, "value": 30 },
                         { "key": null, "label": null, "value": 5718150197, "masked": false } ] } ],
  "totals": [ … same cells, over all the rows' projects; null when showTotals is false … ],
  "truncated": false
}
```

A cell the caller may not see (amounts without `FINANCIAL_VIEW_SENSITIVE`, or a field's withheld cell) has `value` null and `masked` true;
missing is null (never 0), except counts. `projectId` is set on one-per-project rows. The visual's `groups` is `[]` for a column table.

## Frontend

- **Settings of a TABLE visual:** the Data panel's values box becomes **Columns**: an ordered list (drag to reorder, × to remove), each with
  its calculation select — "Don't summarize", "Count", "Count distinct", "Sum", "Average", "Minimum", "Maximum", "Count of yes", "Share of yes"
  — limited to what the column takes (a measure shows its name and no select). **Add column** opens the field list: *Project details* (the
  dimensions, title, project ID), *Portfolio figures* (the measures), then each data source; search as today. **Show totals** tick.
  A new TABLE from the gallery starts as `SECTOR` Don't summarize + `PROJECTS`.
- **Drawing (designer and dashboard):** a table with the columns' headings ("Sector", "Count of Projects", "Sum of Registration budget" —
  Power BI style, or the measure's name), cells formatted by `columnTypes` (SAR compact with full figure on hover, n%, ±points, dates, On plan
  badge as the sections draw it), dimension cells linking to the projects list where `drillPath` can filter by them, title cells linking to
  the project, the totals row, clickable headers that sort the drawn rows (viewer-side only, not saved), and the `truncated` note.
- **Make editable:** a button in the settings of the five table sections. It replaces the section by a TABLE visual at the same place, size and
  titles, with columns: `PORTFOLIO_TABLE`/`INITIATIVE_TABLE`/`SECTOR_TABLE` → the dimension `NONE`, then `PROJECTS`, `VALUE_SAR`, `MEASURED`,
  `AVERAGE_PLANNED`, `AVERAGE_ACTUAL`, `GAP_POINTS`, `ON_PLAN`, `NEEDING_FOLLOW_UP` (minus the section's hidden columns); `STATUS_TABLE` →
  `STATUS` `NONE`, `PROJECTS`, `PROJECT_SHARE`; `CURRENT_STAGE_TABLE` → `STAGE` `NONE`, `PROJECTS`, `VALUE_SAR`. An unsaved change as any
  other (Discard brings the section back). The sector's second table (spend and portfolio split) is not carried over; the settings say so.

## Who builds what (parallel; do not edit another's files; do not commit — the lead integrates)

| Agent | Owns |
| --- | --- |
| Backend | everything under `src/backend/` (unit + integration tests), `docs/architecture/erd.dbml`/`erd.md`, `docs/api/openapi.v1.json` |
| Frontend | `src/frontend/src/` product code (everything except `*.test.ts(x)` and `src/test/`), i18n, styles |
| Tests | `src/frontend/src/features/dashboards/design/*.test.ts(x)`, `src/frontend/src/test/` fixtures, `artifacts/designer/*.mjs` |

Frontend type contract (for the tests agent now): in `design/designApi.ts` — `TableColumnKind = 'DIMENSION' | 'FIELD' | 'MEASURE'`;
`TableAggregation = 'NONE' | 'COUNT' | 'COUNT_DISTINCT' | 'SUM' | 'AVERAGE' | 'MIN' | 'MAX' | 'COUNT_TRUE' | 'SHARE_TRUE'`;
`TableColumn = { kind; key: string; aggregation: TableAggregation | null }`; `Visual.columns?: TableColumn[] | null`, `Visual.showTotals?: boolean | null`;
`TableCell = { key: string | null; label: BilingualLabel | null; value: number | null; masked?: boolean }`;
`VisualResult.table?: { columnTypes: string[]; rows: { projects: number; projectId: string | null; cells: TableCell[] }[]; totals: TableCell[] | null; truncated: boolean } | null`.
i18n keys under `dashboards.designer.`: `columnsLabel`, `addColumn`, `showTotals`, `makeEditable`, `makeEditableHint`, `aggregation.NONE`
("Don't summarize"), `aggregation.COUNT_DISTINCT` ("Count distinct"), `projectDetails`, `tableTruncated`; the Columns list is a `role="list"`
named by `columnsLabel`; each calculation select is labelled "Calculation for {column}".

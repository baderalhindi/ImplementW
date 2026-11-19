# Portfolio Dashboard Designer — more fields (contract, 18 Nov 2026)

Extends [dashboard-designer-data-fields.md](dashboard-designer-data-fields.md) (read it first; everything there stays). The user asked
whether the designer offers every field; it offered 37 of the register's 44 and none of the project record's own attributes. Four additions,
all through the governed register (no free SQL, each value under its source's permission):

1. A new registered projection **`PROJECT.PROJECT_PROFILE`** with the project record's own attributes.
2. **`BOOLEAN`** fields in the designer.
3. **`DATE`** and **`DATE_TIME`** fields in the designer.
4. **Snapshot-grain** fields (the published progress history) on a new **`LINE`** visual and on column, bar and table: figures over time.

## 1. `PROJECT.PROJECT_PROFILE`

Registered in `DashboardProjections.All` beside `PROJECT.LIFECYCLE_STATE`: source domain `WF-01`, `CurrentLive`, contexts Portfolio and
Project, widget types `[MetricCard]`, permission `PROJECT_VIEW`, `IsSensitive` false at projection level, eligibility `AnyProject`, grain
`Project`, drill target `SCR-025`, business meaning "The project record's own registered attributes as WF-01 holds them: registration
budget, declared progress, funding, dates and flags." Source `ProjectProfileSource` (Dashboards/Projections) reads from `ProjectFacts`, which
gains the attributes it lacks (`RegistrationBudgetSar`, `DeclaredActualPercent`, `DeclaredPlannedPercent`, `DeclaredActualSpendPercent`,
`DeclaredPlannedSpendPercent`, `FundingSource`, `IsStrategic`, `EntityUpdateCompliant`, `ClosedAt`, `NextActionDueDate`) as optional
trailing parameters filled where `ProjectFacts` is built.

| Field code | Type | Notes |
| --- | --- | --- |
| `REGISTRATION_BUDGET` | `SAR` | **sensitive**; the source masks it (figure `IsMasked`) unless the caller holds `FINANCIAL_VIEW_SENSITIVE` (the same rule the insights' `amounts` use) |
| `DECLARED_ACTUAL_PERCENT`, `DECLARED_PLANNED_PERCENT` | `PERCENT` | the entity-declared progress |
| `DECLARED_ACTUAL_SPEND_PERCENT`, `DECLARED_PLANNED_SPEND_PERCENT` | `PERCENT` | |
| `CURRENT_STAGE_PERCENT` | `PERCENT` | |
| `FUNDING_SOURCE` | `CODE` | values `STRATEGY_BUDGET_FULL`, `STRATEGY_BUDGET_PARTIAL`, `NOT_STRATEGY_BUDGET` |
| `PARTICIPATION_MODE` | `CODE` | the `ParticipationMode` names |
| `PROGRESS_METHOD` | `CODE` | `SCHEDULE`, `STAGES` |
| `IS_STRATEGIC` | `BOOLEAN` | |
| `ENTITY_UPDATE_COMPLIANT` | `BOOLEAN` | |
| `PLANNED_START_DATE`, `PLANNED_END_DATE`, `NEXT_ACTION_DUE_DATE` | `DATE` | |
| `ACTIVATED_AT`, `CLOSED_AT` | `DATE_TIME` | |

A value that is not set is a missing cell (never 0, never false). The projection is then also offered to the Custom report builder (it is in
`Reportable`), as every registered project-grain projection is.

## 2–3. Field types and aggregations

`PortfolioFieldType` gains `Boolean = 6, Date = 7, DateTime = 8`. `PortfolioFieldAggregation` gains `CountTrue = 6, ShareTrue = 7`.

| Field type | Aggregations | Value |
| --- | --- | --- |
| `BOOLEAN` | `COUNT`, `COUNT_TRUE`, `SHARE_TRUE` | `COUNT_TRUE` the projects whose value is true; `SHARE_TRUE` = true ÷ revealed × 100, 2 places (null when none revealed) |
| `DATE`, `DATE_TIME` | `COUNT`, `MIN`, `MAX` | `MIN`/`MAX` (earliest/latest) as the **day number since 1970-01-01** (a `DATE_TIME` by its UTC date); the frontend formats it as a date from `valueTypes` |

Rule 3 (gauge, pie, donut) is unchanged, except a gauge also takes a `BOOLEAN` field with `SHARE_TRUE`, and pie/donut take `COUNT_TRUE`.

**Grouping** (`groupField`) now takes a `CODE` field, a `BOOLEAN` field (keys `TRUE`, `FALSE`, `null`) or a `DATE`/`DATE_TIME` field with
the new visual property **`groupDateUnit`**: `YEAR` (key `2026`) or `MONTH` (key `2026-03`), required when the group field is a date and
null otherwise. Date groups are ordered by key ascending, `null` last (not by size).

**Filters** now take a `BOOLEAN` field (values `TRUE`/`FALSE`) and a `DATE`/`DATE_TIME` field by year (values `^\d{4}$`, the project passes when
its date's year is one of them).

## 4. Over time: snapshot grain and the `LINE` visual

- `PortfolioVisualType` gains `Line = 9` (domain, CHECK on `visual_type`, migration `TASK-112_AddPortfolioVisualLine`, which also needs no
  data change). A line takes 1–3 field values (no fixed measures) and must be grouped, by `groupField` only: its `groupBy` is `NONE`.
- The fields endpoint now also lists the **snapshot-grain** fields of `PROGRESS.PUBLISHED_PROGRESS_HISTORY` (`PUBLISHED_AT` `DATE_TIME`,
  `PERIOD_START`/`PERIOD_END` `DATE`, `OVERALL_HEALTH` `CODE`, `ACTUAL_PERCENT`/`PLANNED_PERCENT` `PERCENT`), each with a new descriptor property
  **`grain`**: `PROJECT` or `SNAPSHOT` (every earlier field is `PROJECT`).
- Rule 7 (new): a visual's `values` and `groupField` are all of one grain. A snapshot visual must group by one of its projection's `DATE`
  fields with a `groupDateUnit` (a period chart), or by its `CODE` field; it is a line, column, bar or table. Filters may still be any
  project-grain field or dimension (they narrow the projects whose snapshots are read) or the snapshot projection's `CODE` field (they narrow
  the snapshots).
- Calculation: the rows are the population's snapshots (each project's published history, read through `IProjectionRowReader` with base
  `PROJECT.LIFECYCLE_STATE`), grouped by the snapshot's period; `Projects` = the distinct projects with a snapshot in the group, `Measured` =
  `ValueEntered` = the snapshots whose first value is revealed. `IPortfolioFieldReader.ReadAsync` returns snapshot cells with their
  `SnapshotIndex` (a new optional member of `PortfolioFieldCell`).
- The frontend draws a line with one series per value, the groups as the x axis in key order, an accessible table fallback (the same
  `TableContainer` pattern the other charts use), and the gallery tile "Line chart".

## Frontend

- `designApi.ts`: `FieldType` adds `BOOLEAN`, `DATE`, `DATE_TIME`; `FieldAggregation` adds `COUNT_TRUE`, `SHARE_TRUE`; `FieldDescriptor`
  gains `grain: 'PROJECT' | 'SNAPSHOT'`; `Visual` gains `groupDateUnit?: 'YEAR' | 'MONTH' | null`; `VisualType` adds `LINE`.
- Data panel: snapshot fields under their source heading with a small "over time" hint; ticking one on a project-grain visual (or the
  reverse) replaces the values, with the one-line note. Group by lists date fields with a Year/Month select beside them. Filters: yes/no ticks
  for a boolean field; a year list (the last 10 years to next 5) for a date field.
- Formatting: `SHARE_TRUE` as `n%`; `COUNT_TRUE` as a count; a date `MIN`/`MAX` as a localized date (Gregorian, the app's date format);
  boolean group keys as Yes/No/Not set; month keys as "Mar 2026" localized.
- Names in `i18n/en.json` and `ar.json` for the new source, its 16 fields, the 6 history fields, the funding source, participation mode and
  progress method values (if `labelOfValue` has no words for them), the aggregations, Year/Month and the Line type.

## Who builds what (parallel; do not edit another's files; do not commit — the lead integrates)

| Agent | Owns |
| --- | --- |
| Backend | everything under `src/backend/` (incl. unit tests), `docs/architecture/erd.dbml` |
| Frontend | everything under `src/frontend/` (incl. its tests and fixtures), `artifacts/designer/*.mjs` |

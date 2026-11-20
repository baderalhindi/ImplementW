# Portfolio Dashboard Designer — choosing a table section's columns (contract, 20 Nov 2026)

Extends the designer contracts (`dashboard-designer*.md`). The user (20 Nov 2026), on the "By sector" section: "I don't see the columns in the
right pane, as if I want to add a column or delete a column."

## What the user gets

The settings of a **table section** list its columns under **Columns**, each with a tick (on by default); untick one and the table, in the
designer and on the dashboard, no longer shows it; tick it again and it comes back. The first column (what the row is: the sector, the
portfolio, the project…) is always shown and not listed. At least one listed column stays ticked. The choice is saved with the design, for
everyone.

Table sections and their listed column codes (frontend `design/sectionColumns.ts`, words from the table's own headings):

| Section | Codes, in the table's order |
| --- | --- |
| `PORTFOLIO_TABLE`, `INITIATIVE_TABLE`, `SECTOR_TABLE` | `PROJECTS`, `VALUE`, `MEASURED`, `AVERAGE_PLANNED`, `AVERAGE_ACTUAL`, `GAP`, `ON_PLAN`, `NEEDING_FOLLOW_UP` (+ for `SECTOR_TABLE`'s spend table: `PLANNED_SPEND`, `ACTUAL_SPEND`, `SPLIT`) |
| `STATUS_TABLE` | `PROJECTS`, `PERCENT` |
| `CURRENT_STAGE_TABLE` | its columns after the stage (the frontend agent lists them from `CurrentStages`) |
| `TOP_FOLLOW_UP` | `TITLE`, `PORTFOLIO`, `REASON`, `OBSTACLE`, `NEXT_ACTION` |
| `ENTITY_COMPLETION_TABLE` | its columns after the entity (from the completion table component) |

A column the caller may not see anyway (the value without amounts) stays hidden whatever the tick.

## API

The visual gains `hiddenColumns?: string[] | null` (input, request, result; echoed): the codes unticked. Rules (422
`PROJECT_PORTFOLIO_DESIGN_INVALID`, path `visuals[i].hiddenColumns`): only on a `SECTION` visual whose section is one of the seven above
(`NOT_ALLOWED`); each code `^[A-Z_]{1,40}$`, at most 20, no repeats (`DUPLICATE`). The backend does not know each section's columns (it
draws none), so it checks the shape only; an unknown code hides nothing. `[]` and null mean all shown.

Storage: nullable `text[]` column `hidden_columns` on `project.portfolio_visual` (null when none hidden); migration
`TASK-112_AddPortfolioVisualHiddenColumns`.

## Who builds what (parallel; do not edit another's files; do not commit — the lead integrates)

| Agent | Owns |
| --- | --- |
| Backend | everything under `src/backend/` (unit + integration tests), `docs/architecture/erd.dbml`/`erd.md`, `docs/api/openapi.v1.json` |
| Frontend | everything under `src/frontend/` (incl. tests and fixtures), `artifacts/designer/*.mjs` |

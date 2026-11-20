# Portfolio Dashboard Designer — record sources: Risks (contract, 20 Nov 2026)

Extends the designer contracts (`dashboard-designer*.md`, especially `-data-fields`, `-more-fields`, `-power-table`). The user (20 Nov 2026):
"I need to consider the risks if needed in the dashboard — as I said, anything in the DB." Until now every visual counts **projects** (one row
per project). A visual now chooses its **record source**: `PROJECTS` (as today, the default) or `RISKS` (one row per risk). The model is
generic so further sources (concerns, change requests, tasks, milestones…) follow the same pattern later; this round builds `RISKS` only.

## API

- The visual gains `source?: 'PROJECTS' | 'RISKS' | null` (input, request, result; null/absent = PROJECTS). Stored in `data_fields`
  (`{"source":"RISKS", ...}`); no migration unless a CHECK needs the key.
- `GET /api/v1/portfolio-insights/design/fields?source=RISKS` (DASHBOARD_DESIGN) lists the risk fields, same descriptor shape as today plus the
  new types below; without `source`, today's list (unchanged).
- New field types for record sources: `TEXT` (a title, a name) and `ITEM` (a master-data item, a person or a project, with a label).

### Risk fields (keys `RISK.<CODE>`, all read through the Risk module's contracts under the caller's `RISK_VIEW` scope — never a raw table read)

| Code | Type | Is |
| --- | --- | --- |
| `RISK` | COUNT | one per risk (its COUNT is "Number of risks") |
| `TITLE` | TEXT | the risk's title |
| `PROJECT` | ITEM | its project (label: the project's title; links to the project) |
| `STATUS` | CODE | IDENTIFIED, ASSESSED, TREATMENT, MONITORING, CLOSED |
| `CATEGORY` | ITEM | RISK_CATEGORY item |
| `OWNER` | ITEM | the owner's display name |
| `RATING`, `PROBABILITY`, `IMPACT` | CODE | from the current assessment (the risk matrix's codes; null when not assessed) |
| `SCORE` | COUNT | the current assessment's score (null when not assessed) |
| `IDENTIFIED_DATE`, `NEXT_REVIEW_DATE`, `CLOSED_DATE` | DATE | |
| `REVIEW_OVERDUE`, `MATERIALISED` | BOOLEAN | next review date past and not closed; materialised |
| `OPEN_ACTIONS` | COUNT | its open treatment actions |
| `REOPENED` | COUNT | times reopened |

The project dimensions (`PORTFOLIO`, `INITIATIVE`, `SECTOR`, `DEPARTMENT`, `ENTITY`, `REGION`, `CITY`, `PRIORITY`, project `STATUS`) apply to a
risk through its project, for grouping and filtering, as today.

### Calculation

The population: the risks the caller may see of the projects the dashboard's filters keep (the same project population as a PROJECTS visual,
then each project's risks under `RISK_VIEW`), then the visual's filters (on risk fields and on project dimensions). Then exactly as a
PROJECTS visual: groups by `groupBy` (a project dimension) or `groupField` (a risk CODE/ITEM/BOOLEAN/DATE field, with `groupDateUnit`), values =
field aggregations over the group's risks (`RISK` COUNT = number of risks); the Power table's columns with `kind: 'FIELD'` keys `RISK.*`
(`NONE` = group by it, so `TITLE` NONE lists the risks one per row, ≤ 500 rows) and `kind: 'DIMENSION'` for the project dimensions; `MEASURE`
columns (portfolio figures) are not allowed on RISKS. Cells of `TEXT`/`ITEM` carry `label`; a project cell's key is the project id.

### Rules (422 `PROJECT_PORTFOLIO_DESIGN_INVALID`)

`source` RISKS on CARD, BAR, COLUMN, PIE, DONUT, LINE, TABLE (not GAUGE, MAP, TEXT, CONTAINER, SECTION → `visuals[i].source` NOT_ALLOWED);
with RISKS, `measures` must be empty (`values`/`columns` carry the figures), field keys must be risk fields (or project dimensions), and the
usual per-type rules hold.

## Frontend

- Data panel: a **Source** select at the top for the allowed types — "Projects" / "Risks" (Arabic "المشاريع" / "المخاطر"). Switching it
  replaces the visual's figures with that source's defaults (Risks: Number of risks; a chart grouped by Rating; a table [Title NONE, Project
  NONE, Rating NONE, Status NONE, Owner NONE, Next review date NONE]) with the one-line note used elsewhere. The field list then shows the
  risk fields (and the project dimensions under "Project details"); "Portfolio figures" are hidden for Risks.
- Gallery: unchanged; a visual starts as Projects.
- Drawing: TEXT and ITEM cells show their labels; a project cell links to the project; a risk title links to the risk where a route exists
  (find the risk details route in src/frontend/src/features/risks); risk STATUS/RATING/PROBABILITY/IMPACT words from the Risk feature's i18n.
- Automatic titles: "Number of risks by Rating", etc.

## Who builds what (parallel; do not edit another's files; do not commit — the lead integrates)

| Agent | Owns |
| --- | --- |
| Backend | everything under `src/backend/` (unit + integration tests), `docs/architecture/erd.dbml`/`erd.md`, `docs/api/openapi.v1.json` |
| Frontend | `src/frontend/src/` product code (not `*.test.ts(x)`, not `src/test/`), i18n, styles |
| Tests | `src/frontend/src/features/dashboards/design/*.test.ts(x)`, `src/frontend/src/test/` fixtures, `artifacts/designer/*.mjs` |

Frontend type contract: `RecordSource = 'PROJECTS' | 'RISKS'`; `Visual.source?: RecordSource | null`; `FieldType` adds `'TEXT' | 'ITEM'`;
`designApi.fields(signal, source?)`; i18n `dashboards.designer.source.label` ("Source"), `source.PROJECTS`, `source.RISKS`,
`dashboards.designer.fieldSource.RISK` ("Risks"), `dashboards.designer.field.RISK.<CODE>`; the Source select is labelled by `source.label`.

# Portfolio Dashboard Designer — a Map visual (contract, 20 Nov 2026)

Extends the designer contracts (`dashboard-designer*.md`; supersedes the Map tile of `-map-text.md` and `-text-format.md`). The user (20 Nov
2026): "the map visual: I want it new, not when I drag a new one it gives the existing one — new, with the same settings as the existing one,
and customizable."

## Model

`PortfolioVisualType` gains `Map = 12` (`MAP`): a map of the visual's figure per place.

- **Figure:** one fixed measure (any count or amount: `PROJECTS` [default], `IN_PROGRESS`, `VALUE_SAR`, `NEEDING_FOLLOW_UP`, `OVERDUE_ACTIONS`,
  `ON_TRACK`, `LATE`, `STALLED`, `NOT_MEASURED`, the `FOLLOW_UP_*` figures — not averages or `GAP_POINTS`) **or** one field value (COUNT, SUM,
  COUNT_TRUE of a project-grain field), as a column chart takes.
- **Places:** `groupBy` `CITY` [default] or `REGION`; no `groupField`.
- **Filters:** the visual's `filters` as every visual has (portfolio, sector, status, entity, … and status fields).
- **Options:** `map: { "showList": true, "showCounts": true }` — the list of regions and cities beside the map; the number on each marker.
  Optional; absent = both true.
- Rules (422 `PROJECT_PORTFOLIO_DESIGN_INVALID`, `visuals[i]…`): MAP takes exactly one measure or one value from the lists above
  (`measures`/`values` NOT_ALLOWED otherwise), `groupBy` CITY or REGION (`groupBy` NOT_ALLOWED), no axes/columns/text/section/widget/children/
  hiddenColumns; `map` only on MAP (`NOT_ALLOWED`). Several MAP visuals may be on a design (no uniqueness). It may be a container's child.
- Calculation: as a bar chart grouped by CITY/REGION — the groups (key = the place's item id, null = projects with no place) with their value;
  the permission of the CITY/REGION grouping. Result echoes `map`.
- Storage: `map` inside the existing `data_fields` jsonb (`{"map": {...}}` beside the other keys); CHECKs: `visual_type` includes `MAP`, and the
  measures CHECK lets a MAP with one known measure or with data_fields through. Migration `TASK-112_AddPortfolioVisualMap` (DROPs in Up via
  `migrationBuilder.Sql(... -- EXPAND-THEN-CONTRACT-REVIEWED: ...)`; Down deletes MAP visuals first).

## Frontend

- The gallery's **Map** tile adds a **new MAP visual** every time (click: first free place; drag: the drop cell or into a container), 12 × 7,
  titled "Projects by location" / "المشاريع حسب الموقع", `measures: ['PROJECTS']`, `groupBy: 'CITY'`, `map: {showList: true, showCounts: true}`.
  It is never disabled. The existing `PROJECT_LOCATIONS` section stays as it is on the design (it is moved, sized and removed as a section).
- Drawing (designer and dashboard): the existing map component (`ProjectLocations` / `LocationsMap`, same look) fed from the visual's groups —
  each group's place (city → its region through the view's `places`; region directly) with the group's value instead of a project count; the
  marker numbers and the list show the figure formatted by its kind (count / SAR compact / …); `showList` false hides the side list,
  `showCounts` false hides the marker numbers; projects with no place are named under the map as the section names them. Clicking a place
  opens the projects list filtered by it and by the visual's filters where `drillPath` can.
- Settings: titles; **Figure** (the measures above and the Data panel's fields as for a column chart, one value); **Places** (City / Region);
  **Filters** (the existing filters editor); **Map options** (Show the list beside the map, Show the numbers on the markers); size; no axes.

## Who builds what (parallel; do not edit another's files; do not commit — the lead integrates)

| Agent | Owns |
| --- | --- |
| Backend | everything under `src/backend/` (unit + integration tests), `docs/architecture/erd.dbml`/`erd.md`, `docs/api/openapi.v1.json` |
| Frontend | everything under `src/frontend/` (incl. tests and fixtures), `artifacts/designer/*.mjs` |

Frontend type contract: `VisualType` adds `'MAP'`; `MapOptions = { showList: boolean; showCounts: boolean }`; `Visual.map?: MapOptions | null`.

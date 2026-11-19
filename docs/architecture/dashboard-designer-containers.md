# Portfolio Dashboard Designer — containers (contract, 19 Nov 2026)

Extends the designer contracts (`dashboard-designer*.md`; placement is free since 19 Nov: a visual stays where it is dropped, what it lands on
moves down below it — `settled()` in `design/visuals.ts`). The user (19 Nov 2026) chose option 1: a **container** — a framed, titled box on
the dashboard that holds visuals; moving the container moves them, sizing it gives them room, dragging a visual in or out adds or removes it.

## Model

- `PortfolioVisualType` gains `Container = 10` (`CONTAINER`). A container has titles (Arabic and English), a place and a size on the
  dashboard grid like any visual, and **no** measures, values, group field, filters, axes, section or widget code.
- A container holds **`children`**: an ordered list of visuals (any type except `CONTAINER`: one level only; sections and widgets may go in
  a container). A child's `layoutColumn`/`layoutRow` are **inside the container**: the container's inner grid has `layoutWidth` columns and
  `layoutHeight − 1` rows (the first row is the container's title bar); a child's `layoutWidth`/`layoutHeight` count those columns and rows.
- API (camelCase): the visual gains `children?: Visual[] | null` (input, request, result; a result child carries its own groups/figures as any
  visual). Only a container has `children` (null/absent on every other visual; `[]` allowed on a container).

### Rules (422 `PROJECT_PORTFOLIO_DESIGN_INVALID`, paths like `visuals[i].children[j].layoutColumn`)

1. `children` only on a `CONTAINER` (`NOT_ALLOWED`); a child is never a `CONTAINER` (`visuals[i].children[j].visualType`, `NOT_ALLOWED`).
2. A container: no measures/values/groupField/filters/axes/section/widgetCode (`NOT_ALLOWED`); `groupBy` `NONE`; height ≥ 2.
3. Each child fits inside its container's inner grid (`OUTSIDE_RANGE`) and no two children overlap (`OVERLAP`, as top-level visuals).
4. Every child obeys every rule a top-level visual obeys (its type's measures/values, axes, widget code…); widget codes and sections stay
   unique across the **whole** design, children included.
5. At most 60 visuals in the design, children counted; at most 20 children per container.
6. Top-level visuals (containers included) still never overlap.

### Storage

`project.portfolio_visual` gains nullable `parent_id uuid` → `portfolio_visual(id)` (`ON DELETE CASCADE`), index on it; a child row has its
container's id; `sort_order` orders the children within their container. CHECKs: `visual_type` includes `CONTAINER`; a `CONTAINER` row has
no measures/data_fields/axes/section/widget_code. The service enforces that a parent is a `CONTAINER` and one level only. The repository keeps
"replace the whole design in one transaction", inserting containers before their children. Migration `TASK-112_AddPortfolioVisualContainers`
(any DROP in Up through `migrationBuilder.Sql(... -- EXPAND-THEN-CONTRACT-REVIEWED: ...)`). The view (`GET design`, preview, save
response) returns children nested under their container, each child calculated as a top-level visual is.

## Designer (frontend)

- Gallery: a **Container** tile (icon: a frame with a title bar) after the visual types; a new container is 6 columns × 6 rows at the first
  free place, titled "Container" / "حاوية".
- Drawing: a container is a framed region (a visible border and a light background, distinct from a visual's card) with its title bar
  (drag handle, title, edit pencil) and an inner grid of its own columns, rows as tall as the dashboard's; its children are drawn there as
  visuals (handle, pencil, size corner), selectable and editable as any visual.
- Dragging a visual (top-level or child): on drop, the **pointer's cell** decides where it goes — inside a container's inner grid it becomes
  (or stays) that container's child at that inner cell (clamped to fit; what it lands on inside moves down, `settled` within the container; if it
  cannot fit — wider than the container — it stays outside and the designer says so); anywhere else on the dashboard it becomes (or stays) a
  top-level visual at that cell. A container cannot go inside another (it is placed at the cell as a top-level visual).
- Moving a container moves its children with it (their inner places are unchanged). Sizing a container never makes it smaller than what its
  children need (the size stops there).
- Removing a container asks (a dialog): **Remove the container and its visuals**, or **Keep its visuals on the dashboard** (each child moves to
  the dashboard at the place it had on screen, then `settled`), or Cancel.
- Settings of a container: titles and size, Remove. Keyboard: the arrow keys on a child's move handle move it within its container; on the
  container's handle they move the container.
- Dashboard (`DesignedVisuals`): a container draws as the same frame and title with its children at their inner places; a child the caller
  may not see is left out (rows nothing covers inside are taken out, as on the dashboard); a container with no child left to show is left out.

## Who builds what (parallel; do not edit another's files; do not commit — the lead integrates)

| Agent | Owns |
| --- | --- |
| Backend | everything under `src/backend/` (unit + integration tests), `docs/architecture/erd.dbml` and `erd.md`, `docs/api/openapi.v1.json` (regenerate with `.github/scripts/openapi-publish.sh`, copy `artifacts/api/openapi.v1.json` over it) |
| Frontend | `src/frontend/src/` product code (everything except `*.test.ts(x)` and `src/test/`), i18n, styles |
| Tests | `src/frontend/src/features/dashboards/design/*.test.ts(x)`, `src/frontend/src/test/` fixtures, `artifacts/designer/*.mjs` (browser walk) |

Frontend type contract (so the tests agent can write against it now): in `design/designApi.ts` `VisualType` adds `'CONTAINER'`; `Visual` gains
`children?: Visual[] | null`; `VisualResult` children are `VisualResult[]`. i18n keys under `dashboards.designer.`: `type.CONTAINER`
("Container"), `containerDefaultTitle`, `removeContainer` (dialog title), `removeWithVisuals`, `keepVisuals`, `cancel`, `noRoomInContainer`.
A container's frame is `role="region"` named by its title; its children are inside that region.

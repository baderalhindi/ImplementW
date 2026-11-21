# Portfolio Dashboard Designer — box sections become containers of cards (contract, 20 Nov 2026)

Extends the designer contracts (`dashboard-designer*.md`, especially `-containers.md` and `-power-table.md`). The user (20 Nov 2026), on the
On-plan indicator: "I want any container existing on the dashboard to be editable like this one: I can edit the cards inside and add visuals."

The two sections made of boxes, `ON_PLAN_COUNTS` (On track, Late, Stalled, Not measured) and `FOLLOW_UP_REASONS` (Stalled, Suspended, Late,
Action overdue, Open obstacle, Total), get **Make editable**: the section becomes a `CONTAINER` at its place, size and titles, holding one
`CARD` per box, each card showing that box's count — the same number the section shows.

## Backend — new portfolio figures (`PortfolioMeasure`)

| Code | Is | Same as |
| --- | --- | --- |
| `ON_TRACK`, `LATE`, `STALLED`, `NOT_MEASURED` | projects of the group with that on-plan status (`NOT_MEASURED`: none) | the On-plan indicator's boxes (`PortfolioOnPlan`) |
| `FOLLOW_UP_STALLED`, `FOLLOW_UP_SUSPENDED`, `FOLLOW_UP_LATE`, `FOLLOW_UP_ACTION_OVERDUE`, `FOLLOW_UP_OPEN_OBSTACLE` | projects needing follow-up for that reason | the follow-up reasons' boxes (the insights' reasons) |

`NEEDING_FOLLOW_UP` already exists (the reasons' Total). Each is a count, calculated over a visual's (or a table row's) projects exactly as the
insights calculate the section's box for the same population — reuse that code, do not re-derive the rules. They are offered wherever the
eight figures are (cards, charts' values where a count is taken, the Power table's MEASURE columns), need the permission the section needs,
and are refused nowhere a count is allowed. The domain enum, the CHECK on `measures` codes (if any lists them), the request model and the
OpenAPI snapshot follow. Migration only if a CHECK lists measure codes: `TASK-112_AddPortfolioBoxMeasures` (DROPs via `migrationBuilder.Sql(...
-- EXPAND-THEN-CONTRACT-REVIEWED: ...)`).

## Frontend

- Words for the new figures (`dashboards.designer.measure.<CODE>`, en/ar) the same as the boxes' labels; drill links where the section's
  boxes link (`drillPath`: on-plan → the projects list filtered by on-plan status where it can; reasons → the follow-up list filtered by
  reason where it can; otherwise none).
- **Make editable** in the settings of `ON_PLAN_COUNTS` and `FOLLOW_UP_REASONS` (beside the existing one of the table sections): replaces the
  section by a `CONTAINER` with the section's place, size and titles, and one `CARD` child per box in the section's order — a card titled
  with the box's label (both languages), measures `[<the figure>]`, laid out in one row across the container's inner width (equal widths to
  a tenth, height 2.5 rows; wrapping to a second row when a card would be under 2 columns wide), the container's height grown to hold them.
  An unsaved change; Discard brings the section back.
- Inside a container, a card drawn small enough reads like the section's boxes (label above, number under it) — reuse the card's existing
  look; the container style already draws children as bordered boxes.

## Who builds what (parallel; do not edit another's files; do not commit — the lead integrates)

| Agent | Owns |
| --- | --- |
| Backend | everything under `src/backend/` (unit + integration tests), `docs/architecture/erd.dbml`/`erd.md`, `docs/api/openapi.v1.json` |
| Frontend | everything under `src/frontend/` (incl. tests and fixtures), `artifacts/designer/*.mjs` |

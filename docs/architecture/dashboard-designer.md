# Portfolio Dashboard Designer

| Field | Value |
| --- | --- |
| Task | TASK-112 (client review follow-up, 17 Nov 2026) |
| Branch | `feat/task-112-dashboard-builder`, stacked on `feat/task-112-review-round8` |
| Status | Built and walked in a browser against a copy of the demo database (English and Arabic); not deployed |

## What the client asked

The Portfolio dashboard should be changeable "like Power BI": the business adds a pie, bar or other chart, moves and sizes it, and chooses what it shows, without a developer.

## Decisions

| # | Decision | Source |
| --- | --- | --- |
| D-1 | **One design, the same for everyone.** The Portfolio Manager (R02, `DASHBOARD_DESIGN` at ALL) saves it and it takes effect at once, with **no second or third person**. This departs from ADM-036's DRAFT → VALIDATED → PUBLISHED for this dashboard's visuals only; the governed widgets below them are unchanged | The user, 17 Nov 2026 ("local.r02 is the manager, he can do whatever he can in the dashboard") |
| D-2 | **Reached from Dashboards and Reports**: a "Design dashboard" tab for holders of the permission, and a "Design this dashboard" button on the Portfolio dashboard | The user |
| D-3 | **The fixed top section is editable.** The six headline cards and the two charts by portfolio are now the standard design; with nothing saved the dashboard looks as before. The Executive's view (round 8, point 14) keeps its own top | The user |
| D-4 | **Portfolio dashboard only** | The user |
| D-5 | **Fixed lists, never a formula** (BR-DSH-030). A visual is a type (card, bar, column, pie, donut, table, gauge), one to three measures (projects, in progress, total value, average planned, average actual, gap, needing follow-up, overdue actions) and a grouping (none, portfolio, initiative, sector, department, entity, status, stage, on-plan, region, city, priority), placed on a twelve-column grid. A new kind of data is a developer task | Recommended by the delivery team; the user accepted |
| D-6 | **The same figures as the insights.** Every visual is calculated over the projects `PortfolioInsightsService` counts (`PortfolioProjectRows`: scope, past registration, the filter bar), per group with the insights' own arithmetic (`RowOf`). A grouping is read under the insights permission that shows that breakdown today (`PermissionOf`), so the designer shows no one more than before; amounts stay withheld without `FINANCIAL_VIEW_SENSITIVE` | Delivery team |
| D-7 | **Every figure still opens its records** (round 8, point 01): a card's title, a bar, a column group, a slice and a table row link to the register or the Follow-up list with the dashboard's filters and the group's own filter. In the designer nothing links | Delivery team |
| D-8 | **Shapes that read.** Cards and gauges are one figure; charts are grouped; a table either. A bar, pie, donut and gauge draw one measure, a card two, columns and tables three. A gauge draws a percentage; a pie or donut only a total that adds up, its five largest groups and the rest as one | Delivery team |
| D-9 | **No "Customize layout".** The personal show, hide and reorder (ADR-019) is gone from the dashboard, since the Portfolio Manager's design now decides the layout for everyone. Its API (`/dashboards/{code}/personalize`) is kept, so a choice already saved still applies to the governed widgets | The user, 18 Nov 2026 |
| D-10 | **The whole dashboard is designed** (the user, 18 Nov 2026: "why not all the dashboard on the design, including tables"). Besides cards and charts, a design places the dashboard's own sections (`SECTION` + `section`): the tables by portfolio, initiative and sector, projects by status and by stage, the on-plan counts, follow-up reasons, top 10, entity completion (donut and table), the projects map and the governed widgets. Each keeps its own figures, links and permission and is placed at most once; the status, stage and on-plan charts are ordinary bar and donut visuals. The standard design is the whole dashboard in its former order. What the caller may not see is left out and the rest rises into its room. The Executive's dashboard (round 8, point 14) keeps its own layout | The user |
| D-11 | **Moving and sizing push, never refuse** (the user, 18 Nov 2026: "I can't expand or move"). The standard design fills every column, so the first designer refused almost every move as an overlap. Now a visual dragged by its handle or its whole title bar, or sized by its corner, takes its place; the visuals it lands on move down below it and the others rise into the room left, as a tile grid does. A size stops at the grid's edge; a visual is up to 24 rows tall | The user |
| D-12 | **No white space; the others move at once** (the user, 18 Nov 2026). A design is an order: the visuals are packed in it, each in the first place from the top it fits, and any room none fits is taken by a neighbour widening or growing into it (the visual just sized keeps its size). Dragging a visual over another moves it to that place in the order and everything reflows while the pointer is still down; sizing and removing reflow at once; the arrow keys move a visual earlier or later. The dashboard packs what the caller sees the same way, so a hidden section leaves no hole. This replaces D-11's push-down. A drag follows the pointer on the window: a browser drops a pointer capture when its element moves | The user |

## Interface

- `GET /api/v1/portfolio-insights/design` (any signed-in user; the filter bar's parameters): the saved design or the standard one (`isStandard`), each visual with `groups`, null when the caller may not see that grouping.
- `POST /api/v1/portfolio-insights/design/preview` (`DASHBOARD_DESIGN`): a design being edited, calculated, not saved.
- `PUT /api/v1/portfolio-insights/design` (`DASHBOARD_DESIGN`, sensitive write): replaces the design as a whole; 422 `PROJECT_PORTFOLIO_DESIGN_INVALID` for a rule above or two visuals on one grid cell.
- `POST /api/v1/portfolio-insights/design/reset` (`DASHBOARD_DESIGN`, sensitive write): back to the standard design.
- Table `project.portfolio_visual` (migrations `TASK-112_AddPortfolioVisuals` and `TASK-112_AddPortfolioVisualSections`), its rules also as CHECK constraints; a section is unique by a filtered index.

## Open

- Two Portfolio Managers saving at the same moment: the later save wins (no `If-Match`); one design-wide version can be added if the business needs it.
- The OpenAPI snapshot and integration tests for the four endpoints are part of round 8's deferred CI step.
- `docs/architecture/shipped-permission-matrix.md` does not list the portfolio insight permissions of 5 Nov nor `DASHBOARD_DESIGN`; to be brought up to date with the RACI matrix.

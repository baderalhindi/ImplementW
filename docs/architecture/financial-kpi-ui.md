# Financial & KPI UI (WF-14 Frontend)

| Field | Value |
| --- | --- |
| Task | TASK-053 — Build Financial & KPI UI (WF-14 Frontend) (P8 - Execution & Performance) |
| Depends on | TASK-052 — the WF-14 API (`financial-kpi.md` §4, §5, §6 item 1): figures null with a `valueStatus`, withheld fields omitted and named in `maskedFields`, `semanticState`, measurements pinned to their target version, aggregates with `isPartial`, `coverage` and `exclusions`. TASK-044/045 — WF-02's reporting periods and the official/live badges. TASK-042, TASK-051 — the project workspace and the earlier screen records this follows |
| Record date | 2026-10-05 |
| Status | **BUILT AND VERIFIED LOCALLY**: component tests against a mocked API (§4 row 4), 12 mutation tests (§4.1), and **the workbook's validation check in Chromium against the real TASK-052 API** on a scratch database, signed in for real (§4 rows 8, 9: 37 of 37). **In a real environment** nobody can enter, review or publish a figure until Appendix A grants it (TASK-052 F-2); nothing is published until AHDA sets the financial-status thresholds (TASK-052 F-4); KPIs, units and frequencies are named by id without `MASTER_DATA_VIEW` (F-3) |
| Branch | `feat/task-053-wf14-financial-kpi-frontend` |
| Deliverables | React components and routes for SCR-049, SCR-050, SCR-071, SCR-072, SCR-073, MOD-023 and MOD-024 in `src/frontend/src/features/financial-kpi` (`ProjectFinancials.tsx`, `FinancialHistory.tsx`, `ProjectKpis.tsx`, `KpiRegisterPage.tsx`, `KpiHistory.tsx`, `routes.tsx`, `dialogs/`, `components/`, `api/`, `access.ts`, `financialKpiRules.ts`, `presentation.ts`, `problems.ts`, `useFinancialData.ts`, `useKpiData.ts`); i18n `features/financial-kpi/i18n/{ar,en}.json`. The workspace's Financials and KPIs tabs, the `/kpis` route and its navigation (D-1). 46 frontend tests. This record |
| Environment variables / secrets | None |
| Gate decision applied | No currency selector (ADR-008): amounts are SAR, said once beside each figure, never chosen (D-6). Commitments field hidden: OPEN_COMMITMENT is never shown, in the sources or the budget history (D-6). Financial widgets are classified sensitive and masked by audience (ADR-010): a "Sensitive" marker heads them and a withheld figure reads "Restricted" (D-3) |
| Participation amendment | **ADR-008 extended**: every financial figure shows its source and as-of date, so a manual figure is visibly manual (D-4) |
| Workbook read | The TASK-053 row of `AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`, read 2026-10-05: description, acceptance criteria, directory, deliverables, branch, gate decision, and the validation check "Render a KPI with no measurement for the current period and confirm the UI shows an explicit 'No data' state, not a 0 value or green status; visually confirm the trend chart preserves historical target context". `financial-kpi.md`; `progress-ui.md`; `milestone-ui.md`. Blueprint Appendix B (the screen inventory) and a WF-14 specification are not in the repository (F-1) |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | SCR-049 Project Financials (workspace tab); SCR-071 Financial Progress (its history page); SCR-050 Project KPIs (workspace tab); SCR-073 KPI History/Trend (a KPI's page under it); SCR-072 KPI Register (`/kpis`) | This task |
| **In** | MOD-023 Update Financial Progress (a period's actual and forecast, or why there is none); MOD-024 Update KPI Value (a period's value, or why there is none) | This task |
| **In** | AHDA's review of a financial update (start, return, publish) and publication of a submitted KPI value — the workflow's other half, needed for anything to become official | This task |
| **Out** | Entering an Approved Budget version and its referenced document; source-mode configuration; KPI assignment and target versions (KPI_MANAGE) | Not in the deliverables (F-2) |
| **Out** | A portfolio financial screen over `/financial-portfolio-aggregates` | Not in the deliverables (F-8) |
| **Out** | WF-11's decision on budget and target versions | TASK-036 approval inbox |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Where it lives.** `features/financial-kpi`. SCR-049 is the workspace's Financials tab and SCR-050 its KPIs tab, after Milestones, audience `reached` (`FINANCIAL_VIEW` and `KPI_VIEW` ship to the delivering entity, TASK-052 D-10), lent the workspace's context by `projects/workspace/FinancialsTab.tsx` and `KpisTab.tsx`. SCR-071 is `/projects/:id/financials/history`, SCR-073 `/projects/:id/kpis/:assignmentId`, both behind their tab's guard. SCR-072 is `/kpis`, behind `RequireSession`, with a "KPIs" sidebar group and a home-page link. SCR-071 is read as the project's financial record over time (as SCR-070 is progress's) | ADR-002 module → feature folder; F-1 |
| D-2 | **Criterion 1: a figure is shown only when the API says it is MEASURED** (`figureOf`). Otherwise it is one of three explicit states, each a bordered flag with an icon and words, `data-value-state` naming it: **Missing** "No data" (crossed circle), **Stale** "Stale" (clock, amber), **N/A** "Not applicable" (dash). A figure the API sent beside another status is not shown. No figure at all reads in its own words — "No approved budget", "No forecast given", and on SCR-050/072 **"No data for this period"** for a KPI with no measurement for the period today falls in (the workbook's check). A rating without a value is "Not rated" and a financial status without figures "Unknown: figures missing", both grey. Nothing Unknown is ever blank, 0 or "SAR 0.00" | Acceptance criterion 1; TASK-052 D-2 |
| D-3 | **Green is reserved for a GREEN rating the API gave.** Only `ragStatus`/`financialStatus` GREEN uses the success colour; workflow states that read as success elsewhere (Published, In force, Active, Complete coverage) are blue here, so a row whose value is "No data" never carries a success colour. A withheld figure (absent from the JSON and named in `maskedFields`, ADR-010) reads **"Restricted"** with a lock, never "No data" and never 0; a withheld source or as-of date says so. The "Sensitive" marker heads SCR-049 and SCR-071 | Acceptance criterion 1 ("never … a green success color"); gate decision (ADR-010) |
| D-4 | **ADR-008 extended: every financial figure carries its provenance** (`ProvenanceLine`): the source ("Manual entry" on a dashed border with a pencil, "Etimad", "Other system"), the as-of date, the reference and who entered it, under the figure. The budget's comes from its commitment version; the actual's and forecast's from the snapshot (official) or the update the live position names (live) — the position itself carries only an as-of date, so the commitments and updates are read with it | Participation amendment |
| D-5 | **Official and live never merged (M-12).** SCR-049 and SCR-071 show the latest PUBLISHED/OFFICIAL snapshot and the CURRENT/LIVE position side by side with progress's solid and dashed badges; the live view says when it includes figures AHDA has not yet published | TASK-052 D-9 |
| D-6 | **Gate decision.** No currency is ever chosen: amounts are "SAR 1,250,000.00" and MOD-023's inputs are labelled "(SAR)". OPEN_COMMITMENT is hidden from the source list (the API lists it) and from the budget history | Gate decision |
| D-7 | **Criterion 2: the trend draws each value against its own pinned target** (`trendPoints`, `targetSegments`, `KpiTrendChart`). The target line is built from each measurement's `targetVersionNo`/`targetValue`, one step per run of the same version, with a dashed rule and "Target v2: 80" where it changes — never from the version in force now, so approving a target draws a new step from the next value on and leaves earlier points where they were. A value recorded late under a newer target makes its own step between two of the older one. A period without a value is a hollow square in a "No value" row under the plot, labelled "No data"/"Stale"/"N/A"/"Restricted", never a point at 0; the line breaks there. The value axis spans the values and targets, with no presumed 0. Time runs left to right in both languages (`dir="ltr"` on the figure); the SVG is named and described (`aria-label`, `<desc>` listing each target step), and the values table under it carries the same data with the pinned version on each row | Acceptance criterion 2; workbook check |
| D-8 | **MOD-023** edits the DRAFT the API opens (MISSING, every figure null): what is known — "The figures are known", "No data", "Stale", "Not applicable" — then, only when known, the actual (required) and the forecast (optional, "No forecast given" when empty) as SAR digits with up to two places, written with two (`toMoney`); the as-of date (required, not after today, UTC), a source reference (≤ 200) and a narrative. Any other status sends both figures as null, whatever was typed. An INTEGRATED actual removes "The figures are known" and says why; an INTEGRATED forecast is not offered (TASK-052 D-8). "Save draft" and "Save and submit", each with the ETag; the submit uses the saved one. "Start the financial update" opens the next period's draft | TASK-052 D-2, D-4, D-7 |
| D-9 | **MOD-024** records a new value pinned to the target version in force — the dialog says which ("rated against target version 2 (80), for good") — or edits a DRAFT, which keeps the version it was pinned to. Its period (the current calendar month is offered; the frequency cannot be read, F-3), what is known, the value (four places) when known, the as-of date and a narrative; any other status sends null. Offered to the project's Project Manager on an ACTIVE project while no DRAFT is open and a target is approved; "No approved target" otherwise. A value submitted for an earlier period does not hold up a new one | TASK-052 D-13, D-14 |
| D-10 | **Review is AHDA's gate** (`access.ts`, navigation only). Starting the review, returning (with a reason) and publishing a financial update, and publishing a KPI value, are offered to an internal user reaching the project who is not the submitter or recorder; never to an external user | ADR-013; TASK-052 D-15 |
| D-11 | **SCR-072 reads every visible project's KPIs** (projects APPROVED_PLANNED to CLOSED, six at a time, then each one's assignments and measurements), and for each KPI the API's own aggregate across the projects that carry it: an average only within one unit; otherwise "Not combined: the projects measure this KPI in different units"; with nothing counted, "No average: no project has a published measured value" (not "units differ", F-17); the RAG counts; a partial aggregate lists each project left out and why. Filters: all, no data this period, red, amber, green | TASK-052 D-11; `financial-kpi.md` §6 item 1 |
| D-12 | **Additions to the foundation**, backward compatible: `apiRequest`'s query takes a list, sent as the parameter repeated (the aggregates bind arrays); `financials` and `kpis` in `WorkspaceTabKey` and `WORKSPACE_TABS`; the `financialKpi` i18n namespace and `common.home.kpis`; `.progress > *` may shrink below its content (a table or chart in a section scrolled the page at 390 px); `.value-state`, `.provenance`, `.figure-stack`, `.sensitive-note`, `.badge--sensitive` and `.kpi-trend` styles; `src/test/financialKpiFixtures.ts`. The tab assertions in `projects` include Financials and KPIs. Reused: progress's `SemanticStateBadge`, `periodLabel`, `usePeriods`, `narrativeRequest`; tasks' `todayUtc` | — |

## 3. Screens and routes

| Screen | Route | Component |
| --- | --- | --- |
| SCR-049 Project Financials | `/projects/:projectId/financials` (workspace tab) | `ProjectFinancials` (via `FinancialsTab`) |
| SCR-071 Financial Progress | `/projects/:projectId/financials/history` | `FinancialHistory` |
| SCR-050 Project KPIs | `/projects/:projectId/kpis` (workspace tab) | `ProjectKpis` (via `KpisTab`) |
| SCR-073 KPI History/Trend | `/projects/:projectId/kpis/:assignmentId` | `KpiHistory`, `KpiTrendChart` |
| SCR-072 KPI Register | `/kpis` | `KpiRegisterPage` |
| MOD-023 Update Financial Progress | SCR-049 "Update financial progress" / "Start the financial update" | `FinancialUpdateDialog` |
| Review financial update | SCR-049 "Review" | `ReviewFinancialDialog` |
| MOD-024 Update KPI Value | SCR-050 "Record value" / "Edit value" | `KpiValueDialog` |
| Publish KPI value | SCR-050 "Publish value" | `PublishMeasurementDialog` |

### 3.1 Empty states

| Screen | When | Title / text |
| --- | --- | --- |
| SCR-049 | Nothing published / no position | "Nothing has been published yet." / "No current position is available." |
| SCR-049 | No update in the workflow | "No financial update is in progress" (with "Start the financial update" for the Project Manager; "Financial figures are reported once the project is active." otherwise) |
| SCR-050 | No assignment | "No KPIs are assigned to this project" — AHDA assigns KPIs and approves their targets |
| SCR-073 | No value | "No values recorded yet" |
| SCR-072 | None / filter matches none | "No KPIs to show" / "No KPI matches this choice" |
| All | 403 on the reads | "You do not have access to this project's financials." / "You do not have access to KPIs." |

## 4. Verification

Run 2026-10-04/05 on macOS with Docker Desktop: frontend in `AHDA-frontend` (Node 24); the API image built from TASK-052 (`ahda-api`, schema at `20261004183817_TASK-052_GuardFinancialKpiHistory`).

| # | Command | Result |
| --- | --- | --- |
| 1 | `npm run lint` | 0 problems |
| 2 | `npx prettier --check src` | All matched files use Prettier code style |
| 3 | `npm run typecheck` | No errors |
| 4 | `npm test` | 37 files, 524 tests passed (35 files, 477 on `dev`, `milestone-ui.md` §4 row 4). 47 new: 13 in `financial-kpi/rules.test.ts`, 33 in `financial-kpi/financialKpi.test.tsx`, 1 in `shared/api/httpClient.test.ts` (a list sent as a repeated parameter). The two tab assertions in `projects` include Financials and KPIs |
| 5 | `npm run build` | Built. 1,333 KB JS (329 KB gzip); the >500 KB chunk warning was already on `dev` (F-14) |
| 6 | `npm audit` | 0 vulnerabilities. No dependency added |
| 7 | — | No backend file changed; the `dotnet` gates do not apply |
| 8 | Live fixture (git-ignored `artifacts/task-053/live`, not committed; `reset.sh` rebuilds it): scratch database `task053_live` in `AHDA-postgres`, migrated by the `ahda-migrate` image and seeded by the compose seed (no integrity violation); `setup.sql` is TASK-052's fixture (projects, periods, KPI catalogue, thresholds, approval routes, test grants of TASK-052 F-2); a second API container on 5083. `prepare.py` over HTTP as local.r04 and local.r02: project A's Approved Budget 2,000,000.00 with a referenced document (the CLEAN verdict by SQL, no scanner locally) approved in `/approval-tasks`; period 1 published STALE; period 2 submitted MEASURED; KPI "Safe man-hours" with target v1 95 → July 92 (AMBER) and August MISSING, both published → target v2 80 approved → September 92 (GREEN) submitted → nothing for October; "Rework rate" N/A for October; project B's "Handover delay" for the aggregate | All records created by the real API |
| 9 | **Browser check** (`check.mjs`): Chromium headless with puppeteer-core 24 inside `AHDA-frontend`, against a second Vite on 5174 forwarding `/api` to the scratch API. Part 1 signs in for real as **local.r08** (R08, ENT-LOCAL), whose shipped grants read project A: nothing is served by the browser. Part 2 signs in for real as local.r04 and writes through the real API; only the two project reads are served (TASK-041 F-1) | **37 of 37 checks passed.** **Validation check**: "Safe man-hours" this period read "No data for this period" with its icon, no digit and no figure in the cell, "Not rated" on grey (`rgb(236, 239, 242)`), no success colour in the row; its last published value read "No data" (August). "Rework rate" read "Not applicable" twice, grey. **Trend** (screenshot `02` reviewed): "Target v1: 95" from July and "Target v2: 80" from September with a dashed rule at the change; July's point pinned to v1 (95) although v2 is in force, September's to v2; August a "No data" square under the plot, no point. SCR-049: the official period read "Stale" for actual and forecast, "Unknown: figures missing", no green; every figure "Manual entry" with "as of …", "ref. Board minute 14/2026"; the live view SAR 300,000.00 / 2,050,000.00 "not yet published"; "Sensitive"; no currency choice; no commitments. SCR-072: "No average: no project has a published measured value" (after the fix of F-17's wording). **MOD-024** (local.r04): "rated against target version 2 (80…)", "No data" → real **201** with `measuredValue: null`, MISSING, UNKNOWN, v2; submit 200 with the ETag; the row then read "No data", "Not rated", "Submitted". **MOD-023**: start → real 201, every figure null, MISSING; "Stale" → real PUT 200 with If-Match and both figures null; submit 200; B's live view "Stale", "No approved budget". axe with colour contrast on SCR-049, -050, -071, -072, -073, MOD-023, MOD-024 and Arabic SCR-050/073: no violation. No horizontal page scroll at 390 px on all five screens. Screenshots `out/01`–`14` |

The first browser run found two defects, fixed and run again from a fresh database: the trend page and the register scrolled sideways at 390 px (a table in a section of the `.progress` grid kept the grid at its content width; D-12), and an aggregate with nothing counted read "units differ" (F-17).

The tests, by acceptance criterion:

| Item | Tests |
| --- | --- |
| 1. A Missing/Stale/N/A value renders with an explicit label/icon, never blank-as-zero or green | `financialKpi.test.tsx`: SCR-049 STALE official and MISSING live (flag, icon, class, grey status, no green, no "SAR 0.00"); withheld → "Restricted"; MOD-023 sends null, never "0.00"; SCR-071 rows; SCR-050 "No data for this period", "Not rated" grey, no green, no 0; N/A and Stale this period; no catalogue; the review dialog; the register. `rules.test.ts`: `figureOf` for each state, a value beside a non-measured status hidden, restricted before unknown, absent; forms send null. Live: row 9. M-1 to M-4, M-7, M-8, M-11 |
| 2. The trend reflects a target-version change without back-editing older points | `financialKpi.test.tsx`: segments v1 95 / v2 80 with their first periods; July pinned to v1 though v2 is in force; August a gap, not a point; the values table with each pinned version; superseded target kept. `rules.test.ts`: points and segments, a late value under a newer target. Live: row 9. M-5, M-6 |
| Workbook validation check | Live, row 9; the SCR-050 tests |
| Gate decision, ADR-008 extended | Sensitive marker, no currency choice, commitments hidden (M-10); provenance on every figure, manual marked |
| MOD-023, MOD-024, review, publication | Exact bodies, If-Match on save and the saved ETag on submit (M-12); a grouped amount and a future as-of refused before sending; INTEGRATED actual; pinned version on a new value and a draft; no target, no record; publish with the ETag, never by the recorder (M-9) |

### 4.1 Mutation tests

Each mutation was applied to the source, `vitest run src/features/financial-kpi` run in `AHDA-frontend`, and the source restored (`artifacts/task-053/mutations`). M-9 survived the first run — the only recorder in the tests was external, refused for that reason — and was caught after a test with an internal recorder was added.

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | An Unknown status shows the figure that came with it | 5 |
| M-2 | A figure with no value reads as 0 | 1 |
| M-3 | "This period" shows the latest measurement, whatever its period | 3 |
| M-4 | No measurement this period is rated Green | 2 |
| M-5 | Every point is drawn against the latest value's target (back-edited) | 3 |
| M-6 | A period without a value is plotted at 0 | 3 |
| M-7 | MOD-023 sends "0.00" for an Unknown actual | 2 |
| M-8 | A withheld figure reads as missing | 2 |
| M-9 | The recorder may publish their own value | 1 (after the added test) |
| M-10 | The commitments field is shown | 1 |
| M-11 | A Stale or Missing flag takes the success colour | 2 |
| M-12 | MOD-023 saves without If-Match | 1 |

## 5. Acceptance criteria and deliverables

| Item | Result |
| --- | --- |
| A KPI or financial value with status Missing/Stale/N/A renders with an explicit label/icon, never blank-as-zero or a green success color | **MET.** D-2, D-3; §4 rows 4, 9; M-1–M-4, M-7, M-8, M-11 |
| The KPI History/Trend chart correctly reflects a target-version change without back-editing older data points | **MET.** D-7; §4 rows 4, 9; M-5, M-6 |
| Description: SCR-049, SCR-050, SCR-071–073, MOD-023/024, Missing/Stale/N/A explicit | **MET.** §3; D-2 |
| Deliverables: React components/routes for SCR-049/050/071-073 and MOD-023-024 | **MET.** §3 |
| Gate decision: no currency selector, commitments hidden, sensitive and masked by audience | **MET.** D-3, D-6. Masking is the API's; nothing is classified yet (TASK-052 F-6) |
| ADR-008 extended: every figure shows source and as-of date, manual visibly manual | **MET.** D-4 |
| Workbook validation check | **MET** in Chromium against the real TASK-052 API (§4 row 9) |

## 6. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **No screen inventory and no WF-14 specification.** These screens follow the API contract and the earlier UI records; SCR-071 is read as the project's financial history (D-1) | PMO | Layout and wording may differ from the Blueprint's |
| F-2 | **No screen enters an Approved Budget, a source mode, a KPI assignment or a target version.** They are outside the deliverables; the API has them (TASK-052 §4). Until a screen exists they are entered through the API | PMO; a follow-up task | Budgets and KPIs cannot be set up from the SPA |
| F-3 | **KPIs, units and frequencies are named by id** without `MASTER_DATA_VIEW` (R01 only, TASK-038 F-1): "KPI 53530000", "Item 53530000". Two KPIs whose ids share a prefix read alike (seen live). The direction is not shown; MOD-024 cannot suggest a period from the frequency and offers the month | Engineering Architect (labels in the representations); PMO | KPIs are hard to tell apart for everyone but R01 |
| F-4 | **People are named by id** without `USER_VIEW` (TASK-049 F-3) | Identity; PMO | "User 00000000" in provenance and history |
| F-5 | **An internal Project Manager cannot read their own project** (TASK-041 F-1): the tabs are unreachable for them; the browser check served the project reads for local.r04 | PMO (Appendix A); TASK-110 | Internal Project Managers cannot report figures through the UI |
| F-6 | **Nothing can be entered, reviewed or published in a real environment** until Appendix A grants FINANCIAL_SUBMIT/REVIEW and KPI_RECORD/REVIEW (TASK-052 F-2); publication needs the thresholds (TASK-052 F-4) | PMO; AHDA Finance | The screens show only "No data" |
| F-7 | **No cross-project KPI query.** SCR-072 reads 1 + N + M requests per visit (projects, assignments, measurements) and one aggregate per KPI | Engineering Architect, with TASK-052 | A slower register for many projects |
| F-8 | **The financial portfolio aggregate is not shown** on any of these screens (no deliverable is a portfolio financial view) | PMO | `/financial-portfolio-aggregates` has no consumer yet |
| F-9 | **"This period" is the calendar period containing today (UTC)**, since a KPI's frequency cannot be read (F-3): a monthly value recorded after its month ends leaves the new month "No data" until the next value | PMO | Expected, and stated in words |
| F-10 | **The live position carries no provenance of its own**; it is taken from the commitment and the update it names (D-4) | Engineering Architect | Three reads where one would do |
| F-11 | **No deletion of a draft** update or value from the SPA (the API allows it) | PMO | A draft started by mistake stays until submitted |
| F-12 | **Arabic strings are the delivery team's**, not reviewed by AHDA | AHDA | Wording may change |
| F-13 | **"Today" is the UTC date** (TASK-050 F-13): between midnight and 03:00 in Riyadh the as-of date cannot be the local today | Engineering Architect | Early-morning entries need yesterday's date |
| F-14 | **The production bundle is 1,333 KB in one chunk** (1,208 KB after TASK-051); no route is code-split (TASK-038 F-5) | Frontend lead | Initial load grows with every module |
| F-15 | **Security Lead review (CTL-43) cannot be requested**: the CODEOWNERS teams do not exist (TASK-031 F-13). This change renders classified financial data | Maintainer | The PR's CTL-43 box stays unticked |
| F-16 | **The register's aggregate reads only up to 200 projects per KPI** (the API's limit) | Engineering Architect | A larger portfolio is aggregated in part without saying so |
| F-17 | **The API reports `isUnitCompatible: false` when no figure exists at all** (no unit to compare, `KpiAggregation`). The SPA reads `measuredCount: 0` first and says "No average" (D-11); another client could say "units differ" | Engineering Architect (TASK-052) | Misleading wording in other consumers |

## 7. Change log

| Date | Change |
| --- | --- |
| 2026-10-05 | Created (TASK-053) |

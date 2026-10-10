# Dashboards UI (FG-01 Frontend)

| Field | Value |
| --- | --- |
| Task | TASK-070 — Build Dashboards UI (FG-01 Frontend) (P12 - Management Intelligence (FG-01/02)) |
| Depends on | TASK-069 — the FG-01 API (`dashboards.md` §5–§7): the role-aware catalogue with `isDefaultLanding`, each dashboard's composition with every widget's R-20(c) `projection` metadata, `unknownReason`, `coverageDetail` and `maskedFields`, the department filter's `departmentOptions`, ADR-019's personalisation commands. TASK-042 — the SCR-040 workspace the Project Dashboard composes into. TASK-045, TASK-053 — the official/live badges and the value-state flags this follows |
| Record date | 2026-10-10 |
| Status | **BUILT AND VERIFIED LOCALLY**: component tests against a mocked API (§4 row 4), 15 mutation tests (§4.1), and **both workbook validation checks in Chromium against the real TASK-069 API** on a scratch database, signed in for real as local.r02 to local.r08 (§4 row 6: 104 of 104). local.r01 cannot sign in on the compose stack (no MFA provider, F-3). **In a real environment** most roles' widgets read *Restricted* until Blueprint Appendix A grants them their source view permissions (`dashboards.md` F-3) |
| Branch | `feat/task-070-fg01-dashboards-frontend` |
| Deliverables | React components for DSH-001 to DSH-012 in `src/frontend/src/features/dashboards`: the role-aware Home that hosts them (`HomeDashboardPage.tsx`, `HostedDashboard.tsx`, `ProjectLanding.tsx`, `PersonalizeDialog.tsx`), DSH-009 as a composed section only (`ProjectDashboard.tsx`, placed by SCR-040's `OverviewTab.tsx`), the widget renderings (`components/`), `api/`, `landing.ts`, `layout.ts`, `presentation.ts`, `drill.ts`, `problems.ts`, `useDepartmentNames.ts`, `routes.tsx` (navigation only); i18n `features/dashboards/i18n/{ar,en}.json`. Shared: `shared/ui/ValueState.tsx`. 51 frontend tests. This record |
| Environment variables / secrets | None |
| Gate decision applied | **"FIXED at 3 dashboards (ADR-006). Bilingual UI (ADR-012)."** — the UI knows the three dashboards of the API and renders DSH-001 to DSH-012 as their renderings, never a fourth (D-1, D-2); every word in Arabic and English, every label the API holds read in the interface language (D-11) |
| Participation amendment | **ADR-013**: DSH-008 is built at launch — the entity's person lands on their own projects and reads the Project Dashboard in SCR-040 in their entity's scope, financial figures masked by audience (*Restricted*), nothing of another entity listed or counted (D-7). **ADR-006 MAPPED**: rendered as the API maps it — Project Dashboard (DSH-009, DSH-008), Governance Dashboard (DSH-005), with the difference from the amendment's DSH-004 recorded (F-1). **ADR-017**: no projection carries the median update completion time (F-2). **ADR-019**: the Project Dashboard offers no personalisation; R08 receives it read only, with no report composition or export (D-7, D-8) |
| Sources read | The TASK-070 row as supplied on 2026-10-10, checked against the workbook's Implementation Plan row (branch, validation checks: "Confirm no standalone route exists for DSH-009 by attempting direct navigation; log in as each role and confirm the landing dashboard matches the Section 20.2 mapping"). **Functional Blueprint v2.0** §20 (navigation, §20.2 role landing pages, §20.3), §21 and §21.1 (DSH-009 "no duplicate page"), Appendix B rows DSH-001–012 (`Project Files.zip`). **Step 16 Frontend Registers** sheets 01, 02 (DSH-001–008 and DSH-010–012: "No independent route; authorized host binding required"; DSH-009: "Embedded in SCR-040; no separate dashboard route"), 03 (FE-CMP-004, FE-CMP-008 "Never replace missing with zero or Unknown with Green"). **FG-01 Dashboards Functional Specification v1.0** §5, §5.1 (Home resolution), §5.2, §6, §7. `dashboards.md` (TASK-069) whole; `financial-kpi-ui.md` D-2, D-3; `external-participation-ui.md` D-7, F-5; the seed's §6 |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | The role-aware Home: the landing of Blueprint §20.2 per role, the other dashboards a person's roles open, the department filter, ADR-019's personalisation of the Portfolio Dashboard | This task |
| **In** | DSH-009 Project Dashboard as a section of SCR-040 Project Overview, and DSH-008 as its entity rendering | This task |
| **In** | Every widget type of FG-01 §10 the API offers, each with its semantic state, freshness, as-of, source and coverage | This task |
| **Out** | ADM-036 Dashboard Configuration screens (draft, validate, publish, retire) and the projection register screen; the API exists (`dashboards.md` §5) | Not in the deliverables (F-9) |
| **Out** | Formal reports, saved views, exports, the detailed DSH-010–012 listings | TASK-071, TASK-072 (ADR-006) |
| **Out** | Widgets over workflows FG-01 does not project yet (approvals, changes, tasks, milestones, issues, documents, external requests, ADR-017's completion time) | `dashboards.md` F-4; F-2 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **No dashboard has a route.** The Home (`/`, the application's index route) hosts the dashboards: Step 16 binds DSH-001–008 and DSH-010–012 to the authorised shell ("No independent route; authorized host binding required"), Blueprint §20.1 makes Home "my/role dashboard", and FG-01 §5.1 says the Home route "does not create a new canonical screen ID". The other dashboards a person may open are chosen with `?dashboard=GOVERNANCE`, an allowlisted value only the catalogue's portfolio-context entries match. **DSH-009 has no route at all**: `ProjectDashboard` is placed by SCR-040's Overview tab and nowhere else; `/dashboards/…`, `/projects/:id/dashboard` and the like match no route (Not found), and `?dashboard=PROJECT` is ignored (acceptance criterion 1). The Home's former list of module links is gone: the shell's navigation has every one of them, and a "Dashboards · My dashboard" group leads back home | Blueprint §20.1, §20.3, §21.1; Step 16 sheet 02; FG-01 §5.1 |
| D-2 | **The landing is the API's; its name is Blueprint §20.2's** (acceptance criterion 3). The Home shows the catalogue entry marked `isDefaultLanding` (TASK-069 D-2), never a mapping of its own. On it, the heading is the §20.2 name of the person's landing role — Administration (R01), Portfolio (R02), Department (R03), My Work / Project Manager (R04), My Actions (R05), Read-Only (R06), Executive (R07), External Contributor (R08) — and the line under it names the DSH and the governed dashboard rendering it ("DSH-003 · shown by the Portfolio Dashboard, version 1"). The landing role is the person's first role in code order, the API's own rule for choosing the landing (TBC-DSH-003), so the name always matches the dashboard chosen. A dashboard opened by choice is headed by its own name and description. A catalogue with nothing marked lands on its first hosted dashboard; an empty one says no dashboard is published for the person's roles | Blueprint §20.2; TASK-069 D-2 |
| D-3 | **Every widget states what it is before what it says** (acceptance criterion 2; FE-CMP-008). Each is a named region whose status line carries the semantic state in words and style — *Current · live* (dashed), *Published · official* (solid), *Historical · snapshot* (dotted) — the as-of ("As of …", or "No as-of date: there is no value to date"), and the source and projection version. A STALE value keeps its value and as-of, gains a *Stale* flag (clock, warning), a warning edge and a sentence saying why; an aggregate's line says how many projects it counted of how many and what it left out, by reason, and how many counted values are stale. `data-semantic-state` and `data-freshness` name the metadata in the DOM | FG-01 §7; `dashboards.md` D-3, D-5, D-7 |
| D-4 | **No data is never a number or a colour.** A widget whose freshness is UNKNOWN draws no body: its reason as a flag with an icon and words — *No data*, *Not applicable*, *Restricted*, *Source unavailable* — and a sentence ("The source holds no value for this yet. It is not zero."). Within a body, a masked figure reads *Restricted* and an absent one *No data*. A zero the source counted (no open risks, an empty backlog state) is shown as 0 (`dashboards.md` F-13). Green is a source's GREEN rating only: workflow states are blue or grey, risk ratings — whose colours are their matrix's, not in the projection — are grey with the matrix's own bilingual label | BR-DSH-013 to -015; `financial-kpi-ui.md` D-2, D-3 |
| D-5 | **The widget types.** METRIC_CARD: the source's state as its badge and its figures. STATUS_DISTRIBUTION, BAR_COLUMN, DONUT_PIE: one table per widget — the value, the count, a bar repeating the count — headed by what is counted (projects, open risks, KPI assignments, dashboard versions); the table is the chart and its own text alternative. PROGRESS_INDICATOR: each source-owned percentage with its bar. LINE_TREND: WF-02's published history as two lines (actual solid blue, planned dashed orange — the validated reference palette's first two slots, told apart by line style and point shape too), a legend, end labels placed so they never overlap, a value on hover per point, a gap where a period has no figure, and the same values in a table. ADR-009's override is a marker beside the percentage it qualifies. Figures use the API's exact strings: counts grouped, percentages to four places, days signed, SAR grouped with two places; Latin digits in both languages | FG-01 §10; BR-DSH-018; TASK-032 D-5 |
| D-6 | **The governed layout, flowing.** Widgets are drawn in governed order (row, then column) with their own span of a twelve-column grid; on a screen under 64 rem each takes the full width, and the grid mirrors in Arabic. ADR-019's choices only hide optional widgets and reorder them among their own places — a governed widget never moves | `dashboards.md` D-10, D-13 |
| D-7 | **DSH-008 and ADR-019 for entities.** An entity's person lands on "External Contributor Dashboard": the projects the API lists for them (their entity's, never another's), each opening SCR-040, where the Project Dashboard renders with "Shown for your entity: read only, within your entity's scope". Their Home reads no Portfolio or Governance composition and offers no switch; no dashboard offers them personalisation, report composition or export. A Project Dashboard the API does not open for a person (404) is a quiet note, never an alert, and says nothing more | ADR-013, ADR-019; `dashboards.md` D-6 |
| D-8 | **Personalisation is the Portfolio Dashboard's** (ADR-019): *Customise layout* is offered when the API says a dashboard is personalisable and the person holds R02, R03 or R07 (`LAYOUT_PERSONALIZE`'s shipped holders; the session carries roles, not permissions — navigation, not protection). The dialog lists only the optional widgets, each with *Show* and *Move up/down*; *Save layout* sends them all, numbered in order; *Restore the standard layout* resets. The Project Dashboard is never offered it | ADR-019; `dashboards.md` D-10 |
| D-9 | **The department filter narrows, never widens.** It offers only the view's `departmentOptions`, named through the department list when the person may read it and by short id otherwise; the value lives in the address (`?department=`). A department the API refuses (422 `DASHBOARD_FILTER_VALUE_UNAUTHORIZED`) is said so, and *Try again* clears it | DSH-CC-17; `dashboards.md` D-6 |
| D-10 | **Drill-through to built screens only.** A widget's `drillTargetScreenId` links to the screen when it is built for the context: in SCR-040, SCR-049, SCR-050 and SCR-080 are the project's tabs; on the Home, SCR-025 is `/projects` and SCR-080 `/risks`. SCR-027 and SCR-028 are not built and SCR-040 is the page itself, so they are not linked; a restricted widget has no target (the API sends none). The target re-authorises (BR-DSH-011) | FG-01 §5.2 |
| D-11 | **Bilingual.** Every interface word is in `dashboards` `{ar,en}.json`; dashboard names, descriptions, widget titles and risk ratings come from the API in both languages and are read in the interface language. Values reuse the vocabularies their modules already translate (`projects.status`, `progress.health`, `financialKpi.financialStatus`, `financialKpi.rag`) | ADR-012 |
| D-12 | **Additions to the foundation**, backward compatible: `shared/ui/ValueState.tsx` — the value-state flag and its icons, extracted from WF-14's `Figures.tsx` (which now uses it, unchanged in output) with a *source unavailable* icon added; `PageHeader`'s `description` accepts `undefined`; the `dashboards` i18n namespace; the dashboard, widget, trend and personalisation styles and a `.badge--historical`; `src/test/dashboardFixtures.ts`, whose catalogue follows the seeded audience and the API's landing rule. Changed tests: the two that asserted the Home's link list now assert the shell's navigation; the closed-workspace sweep answers the overview's dashboard read and does not count *Refresh* as a write | — |

## 3. Screens

| Screen | Where | Component |
| --- | --- | --- |
| DSH-001, DSH-004, DSH-005 (Governance Dashboard renderings) | `/` as the landing of R01, R04, R05; `/?dashboard=GOVERNANCE` for R02, R03, R07 | `HomeDashboardPage`, `HostedDashboard` |
| DSH-002, DSH-003, DSH-006, DSH-007 and the summary widgets of DSH-010–012 (Portfolio Dashboard renderings) | `/` as the landing of R02, R03, R06, R07 | `HomeDashboardPage`, `HostedDashboard` |
| DSH-008 External Contributor Dashboard | `/` for R08 (its projects), then SCR-040 | `ProjectLanding`, `ProjectDashboard` |
| DSH-009 Project Dashboard | A section of SCR-040 Project Overview (`/projects/:id`, Overview tab); no route | `ProjectDashboard` |
| ADR-019 Customise layout | The Portfolio Dashboard's *Customise layout* | `PersonalizeDialog` |

| State | When | What is shown |
| --- | --- | --- |
| No dashboard | The catalogue is empty | "No dashboard is published for your roles" |
| No project | An R08 landing with no project | "No project is shared with you yet" |
| Project Dashboard not opened | 404 for the project | "The project dashboard is not available to you for this project." |
| Widget unknown | `unknownReason` set | *No data* / *Not applicable* / *Restricted* / *Source unavailable*, with its sentence |
| Filter refused | 422 `DASHBOARD_FILTER_VALUE_UNAUTHORIZED` | "That department is not one you may filter by. Show all your departments instead." |

## 4. Verification

Run 2026-10-10 on macOS, in the `AHDA-frontend` container (Node 24).

| # | Command | Result |
| --- | --- | --- |
| 1 | `docker exec -w /repo/src/frontend AHDA-frontend npm run lint` | 0 problems |
| 2 | `npm run typecheck` | 0 errors |
| 3 | `npx prettier --check .` | All matched files use Prettier code style |
| 4 | `npx vitest run` | **874 passed, 50 files** (823, 48 files on `dev` at 83bad68: 51 new — `dashboards.test.tsx` 40, `rules.test.ts` 11). Changed: `approvals/access.test.tsx` and `notifications/inbox/inbox.test.tsx` assert the shell's links from the Home; `closedWorkspace.test.tsx` mocks the dashboard read (its overview sweep passes) |
| 5 | `npm run build` | Built; the chunk-size warning is the one on `dev` |
| 6 | Live (`artifacts/task-070/live`, git-ignored: `reset.sh` — a fresh `task070_live` migrated by the `ahda-migrate` image and seeded by the compose seed, TASK-069's fixture with project A's open reporting period made overdue (`setup.sql`), `AHDA-api-070` from the `ahda-api` image built from TASK-069 on port 5100; a second Vite on 5182; `check.mjs` in Chromium inside `AHDA-frontend`; output `check.out`, screenshots `out/`) | **104 of 104 checks.** **Landings**: local.r02 to local.r07 each sign in and land on their §20.2 page, named with its DSH, the Home reading the right composition and never PROJECT (R02, R03, R06, R07 → Portfolio; R04, R05 → Governance); local.r08 lands on the External Contributor Dashboard listing only its entity's project, linked to SCR-040, with no composition read and no switch. **No DSH-009 route**: `/dashboards`, `/dashboards/PROJECT`, `/dashboards/PROJECT?projectId=…`, `/dashboards/project`, `/dashboards/DSH-009`, `/projects/:id/dashboard`, `/project-dashboard` — each *Page not found*, no dashboard read; `/?dashboard=PROJECT` shows the landing. **Criterion 2 on every widget the real API returned** (9 Portfolio, 5 Governance, 12 Project, for every role): semantic state in words, as-of and source on each; every UNKNOWN widget's reason in words with no digit; every STALE one flagged. Project A's published health STALE, *Green* kept with its as-of of 20 days ago, beside its live *Amber* FRESH; project C's nine silent widgets *No data*; local.r08 on its project: published health and schedule shown, internal widgets *Restricted*. **ADR-019**: local.r02 hides a widget (200, gone), restores (200, back). **Filter**: offered only the view's department, read with it. **axe with colour contrast**: 0 violations on the Home (en, ar), SCR-040 as local.r02 and as local.r08; **390 px**: no sideways scroll. **Arabic**: local.r03 lands on "لوحة الإدارة", right to left |

The live run found a chart defect the component tests did not: on a one-period trend whose actual was below planned, the two end labels overlapped. They are now placed by value and pushed apart, and the planned point is a ring so an equal actual point stays visible. The component tests found one accessibility defect: the visually hidden suffix of *Open* and *Move up/down* lost its separating space ("OpenOpen risks by rating").

### 4.1 Mutation tests

`artifacts/task-070/mutations/run.py` applies each mutation, runs the dashboards suite and the closed-workspace sweep in `AHDA-frontend`, and restores the file. 15 mutations, 15 killed.

| # | Mutation | Killed by |
| --- | --- | --- |
| M-1 | A widget with no data draws 0 | `… with no data reads … in words, with no number and no colour` |
| M-2 | A stale widget is not flagged Stale | `a stale value keeps its value, flagged Stale …` |
| M-3 | Every widget is labelled current/live | `each widget carries its semantic state in words …` |
| M-4 | A masked figure is shown as a value | `a figure withheld from the audience reads Restricted …` |
| M-5 | The Home hosts the Project Dashboard | `the Home shows what the address asks for only among the dashboards it hosts` |
| M-6 | DSH-009 gets a route of its own | `navigating directly to /dashboards/PROJECT finds no page …` |
| M-7 | The landing is named for the last role | `the landing role is the first role in code order …` |
| M-8 | The landing ignores `isDefaultLanding` | `with no landing marked, the first hosted dashboard is shown …` |
| M-9 | Personalisation offered to any role | `personalisation is offered … to R02, R03 and R07, never on PROJECT` |
| M-10 | Hidden widgets are drawn | `a hidden widget is left out of the dashboard` |
| M-11 | The department filter is not sent | `it offers only the dashboard's own departments … and reads the one chosen` |
| M-12 | SCR-040 does not compose the Project Dashboard | `it renders as a section of SCR-040 Project Overview, bound to that project` |
| M-13 | Coverage hides what was left out | `a population says how much it counted and what it left out …` |
| M-14 | The Project Dashboard is read without its project | `it renders as a section of SCR-040 …` |
| M-15 | A personal order is ignored | `a personal order moves optional widgets among their own places …` |

## 5. Acceptance criteria, deliverables and checks

| Item | Result |
| --- | --- |
| DSH-009 does not exist as a standalone route — it renders only as a composed section of SCR-040 | **MET.** D-1; tests `acceptance criterion 1`; §4 row 6; M-5, M-6, M-12 |
| Every widget visibly indicates its semantic state (current/published/historical) and any staleness/missing-data condition rather than presenting an unlabeled number | **MET.** D-3, D-4, D-5; tests `acceptance criterion 2`; §4 row 6 on every widget the real API returned; M-1 to M-4, M-13 |
| Each role's default landing dashboard matches Blueprint Section 20.2 | **MET** for R02–R08 live and R01–R08 in tests (D-2, D-7; §4 row 6; M-7, M-8). R01's live sign-in is blocked by the compose stack's missing MFA provider (F-3). R04's landing is the Governance Dashboard as the API seeds it, under §20.2's name; the amendment's mapping differs (F-1) |
| Validation: confirm no standalone route exists for DSH-009 by attempting direct navigation | **MET** in Chromium (§4 row 6) |
| Validation: log in as each role and confirm the landing matches the Section 20.2 mapping | **MET** for local.r02 to local.r08 in Chromium; local.r01 F-3 |
| Deliverables: React components/routes for DSH-001–012 (DSH-009 as a composed section only) | **MET** (§3; header row "Deliverables"); no route by design (D-1) |
| Gate decision: 3 dashboards; bilingual UI | **MET** (D-1, D-2, D-11) |
| Participation amendment: DSH-008 at launch, financial widgets masked by audience, no cross-entity data or counts; Project Dashboard not personalised; R08 read only, no report composition or export | **MET** (D-4, D-7, D-8; §4 row 6). ADR-006's DSH-004 mapping and ADR-017's completion time: F-1, F-2 |

## 6. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **DSH-004's dashboard differs between this task's amendment and the seed.** The amendment maps DSH-004 into the Project Dashboard; TASK-069 seeded R04's landing on the Governance Dashboard (`dashboards.md` D-2, F-1, "the delivery team's"), which FG-01 §6.4 also fits ("My authorized Projects … obligations"). The UI follows `isDefaultLanding` and names the landing "My Work / Project Manager Dashboard" either way; a Project Dashboard landing already renders as the person's projects list (as for R08), so AHDA's choice is a new version on ADM-036 with no code change | PMO Engagement Lead + AHDA Business Sponsor | R04 lands on the Governance Dashboard |
| F-2 | **The Governance Dashboard's amendment content has no projection**: approvals and data-quality flags (`dashboards.md` F-4), and ADR-017's median update completion time, which needs TASK-107's `PeriodicUpdateSession`. Overdue updates are there (reporting completeness). A widget a later version adds renders without code; its values show their codes until their words are added to `presentation.ts` | Engineering Architect; TASK-107 | The Governance Dashboard shows reporting, lifecycle, risk, KPI and configuration attention only |
| F-3 | **R01 cannot sign in on the compose stack** (R01 requires MFA; no provider is configured): its Administration Dashboard landing is verified in the component tests, not live | DevOps (MFA provider) | — |
| F-4 | **Most roles see *Restricted*.** Live, R03, R05, R06 and R07 had no source view grant and every widget read *Restricted*, which is the correct answer to the grants (`dashboards.md` F-3) | PMO (Appendix A) | AHDA's managers see no data until the grants are decided |
| F-5 | **Department names need the organisation read** (`ORGANIZATION_VIEW`, R01's): anyone else's filter names a department by short id ("Item 00000000"), as the project screens do | PMO (Appendix A) | The filter is hard to read |
| F-6 | **Personalisation is offered by role** (R02, R03, R07), not by permission: the session carries no permissions. A holder whose profile drops `LAYOUT_PERSONALIZE` is told "Your role cannot customise this dashboard" on saving | FG-03 owner | A refused offer in that case |
| F-7 | **ADR-017 and ADR-019 are not in `adrs/ADR-register.md`** (`dashboards.md` F-2) | Engineering Architect | The rules are traceable to the task rows only |
| F-8 | **The rest of FG-01's API is not built** (`dashboards.md` F-5): no attention lists, no drill resolver, no per-widget refresh. The UI reads a dashboard whole, refreshes it whole, and links to built screens by id (D-10) | PMO / Business Analyst | — |
| F-9 | **ADM-036 has no screens.** Dashboard definitions are drafted, validated, published and retired over the API only; DSH-001 links nowhere for it | PMO (a task for ADM-036's UI) | Configuring a dashboard version needs the API |
| F-10 | **CTL-43 does not apply, and a review is still advisable.** This change alters no authentication, SSO, RBAC or data-scope rule, upload, WF-13 or Nafath path — the API decides every widget and every audience — so the PR's CTL-43 box is ticked as not applicable. It does render an authorisation-filtered surface to external users, and a Security Lead review cannot be requested in any case: the CODEOWNERS teams do not exist (TASK-031 F-13) | Maintainer | — |

## 7. Change log

| Date | Change |
| --- | --- |
| 2026-10-10 | Created (TASK-070) |

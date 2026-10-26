# Risk Register & Detail UI (WF-06 Frontend)

| Field | Value |
| --- | --- |
| Task | TASK-056 — Build Risk Register & Detail UI (WF-06 Frontend) (P9 - Risk & Issue Management) |
| Depends on | TASK-055 — the WF-06 API (`risk-management.md` §4, §5, §6 item 2): `/risks` and its read-only histories, the commands, assessments pinned to the RISK_MATRIX version in force, and the closure rationale required by the API (400). FG-04's resolution API (`master-data-configuration.md` D-14). TASK-042, TASK-051, TASK-053 — the project workspace and the earlier screen records this follows |
| Record date | 2026-10-06 |
| Status | **BUILT AND VERIFIED LOCALLY**: 41 component and rule tests against a mocked API (§4 row 4), 8 mutation tests (§4.1), and **the workbook's two validation checks in Chromium against the real TASK-055 API** on a scratch database, signed in for real, nothing served by the browser (§4 rows 8, 9: 40 of 40). **In a real environment** nobody can see the heat-map: the matrix needs CONFIGURATION_VIEW, which only R01 holds (F-1), and no RISK_MATRIX version is published yet (TASK-055 F-4); nobody can assess, accept or reopen (TASK-055 F-2) |
| Branch | `feat/task-056-wf06-risk-register-frontend` |
| Deliverables | React components and routes for SCR-080, SCR-081, SCR-082 and MOD-030 to MOD-035 in `src/frontend/src/features/risks` (`ProjectRisks.tsx`, `RiskRegisterPage.tsx`, `CriticalRisksPage.tsx`, `RiskDetail.tsx`, `routes.tsx`, `dialogs/`, `components/` with `RiskHeatMap.tsx`, `api/`, `access.ts`, `riskRules.ts`, `presentation.ts`, `problems.ts`, `useRiskData.ts`); i18n `features/risks/i18n/{ar,en}.json`. The workspace's Risks tab, the `/risks` routes and their navigation (D-1). The Risk Dashboard's composition inputs, `riskSummary` (D-6). 41 frontend tests. This record |
| Environment variables / secrets | None |
| Gate decision applied | None recorded for TASK-056 (the workbook's Gate Decision cell is empty) |
| Workbook read | The TASK-056 row of `AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`, read 2026-10-06: description, acceptance criteria, directory, deliverables, branch, and the validation checks "Change the published matrix configuration and confirm the heat-map re-renders accordingly on next load; attempt to close a risk with an empty rationale and confirm rejection". The WF-06 Functional Specification §8 (SCR-080–082, §8.7 the Risk Matrix component, §8.8 MOD-030–035), §5.1, §12.3, §12.10, §13.2; the Step 16 Frontend Registers (audiences, accessibility FE-AX-003/004); the FG-01 DSH-010 description — all in `Project Files.zip`, which TASK-055 had not read (its F-7). `risk-management.md`; `financial-kpi-ui.md`; `milestone-ui.md` |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | SCR-080 Risk Register: a project's (workspace tab) and across the projects a person may see (`/risks`) | This task |
| **In** | SCR-081 Critical Risks (`/risks/critical`): the exposure heat-map and the risks at the matrix's most severe rating | This task |
| **In** | SCR-082 Risk Detail: header and current exposure, description, assessment history, treatment plan, acceptances, closure | This task |
| **In** | MOD-030 Create Risk, MOD-031 Edit Risk, MOD-032 Risk Assessment, MOD-033 Assign Risk Owner, MOD-034 Add Mitigation / Response, MOD-035 Close Risk; the risk's other commands (start treatment, monitor, accept, revoke acceptance, reopen) and a treatment action's (start, complete, cancel) | This task |
| **In** | The 5×5 matrix as a visual heat-map from the published configuration (WF-06 §8.7) | This task |
| **In** | The Risk Dashboard's composition inputs (`riskSummary`): the counts DSH-010 composes | This task; the dashboard itself is TASK-070's |
| **Out** | Inherent and residual ratings, trend, cause/event/effect, escalation (SCR-087/088, MOD-039), Task linkage, comments and audit panels | Not in the TASK-055 API (F-3) |
| **Out** | MOD-036 Create Issue (materialisation): the API answers 503 until WF-07 | TASK-057 (TASK-055 F-1) |
| **Out** | Editing or publishing the matrix: read-only to business users (WF-06 §8.7) | FG-04 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Where it lives.** `features/risks`. SCR-080 for a project is the workspace's Risks tab, after KPIs, audience `reached` (RISK_VIEW ships to the Project Manager, TASK-055), lent the workspace's context by `projects/workspace/RisksTab.tsx`; SCR-082 is `/projects/:id/risks/:riskId` behind the same guard. SCR-080 across projects is `/risks` and SCR-081 `/risks/critical`, behind `RequireSession`, with a "Risks" sidebar group and a home-page link. The Step 16 `/views/scr-0xx/…` routes are proposals only ("does not define backend endpoint"); the repository's convention is followed | ADR-002 module → feature folder; `milestone-ui.md` D-1 |
| D-2 | **Criterion 1: the heat-map is the published RISK_MATRIX version, read on every load, never built in** (`RiskHeatMap`, `riskMatrixOf`). `GET /configuration-resolutions/RISK_MATRIX` is read whenever a screen opens and not kept, so a new version shows on the next load. Rows are the version's probability levels (highest at the top) labelled with the version's labels; columns the overall impact levels any dimension defines; each cell the version's rating for (probability, impact), in words, or "Not mapped" for a cell the version leaves out. The size, labels, ratings and mapping all follow the version: a 3×3 version with two ratings draws a 3×3 map (§4 row 4, row 9). The caption names the version and its effective date; a legend lists the ratings least to most severe. It is a `<table>` with row and column headers, so each cell is read with its probability and impact; a marked cell has a thick frame and its words ("This risk", "Chosen levels"), and colour only repeats the label (WCAG 1.4.1, FE-AX-003) | Acceptance criterion 1; WF-06 §8.7; workbook check 1 |
| D-3 | **No matrix, no grid.** Without CONFIGURATION_VIEW (403) the screen says the matrix cannot be read and why; with none published (422 CONFIGURATION_MISSING) it says none is published and risks cannot be assessed. Neither draws a default grid, and MOD-032 offers nothing to send. The register still shows each risk's rating in words (the server's label), and the critical count reads "Unknown", never 0 | Criterion 1; TASK-055 F-4; WF-06 §8 "no false Low/zero values" |
| D-4 | **"Critical" is the matrix's most severe rating.** No high/critical threshold is configured (TBC-RSK-005) and the API has no rating filter (TASK-055 F-9), so SCR-081 lists open risks whose latest assessment carries the rating last in the version's published order (`sortOrder`; `RiskRatingDefinition` documents "Low, Medium, High, Critical"). The screen states the criterion with that rating's label and the version. No rating code is known to the SPA. A risk rated under an older version whose codes differ is not critical under the new one | WF-06 §8 SCR-081 ("criteria derive from configured rating"); F-2 |
| D-5 | **Ratings are the server's; the matrix only places them.** A risk's rating is `currentAssessment.rating`, the label the pinned version gave (TASK-055 D-5); the SPA never rates. Its cell is marked on the matrix in force at (probability, overall impact); when the assessment pinned another version, SCR-082 says the rating is kept as given and the map is today's. A rating's colour is its severity step (D-7) in the matrix in force; one that version does not define is shown in words on a neutral ground | WF-06 §5.1, §12.3; BR-DSH-004 |
| D-6 | **The dashboard's inputs** (`riskSummary` in `riskRules.ts`): over the risks a person may see — open, not assessed, review due, accepted, by status, by rating (the version's ratings, then any older rating a risk still carries), critical (null without the matrix), and open assessed risks per cell. SCR-080's summary cards and SCR-081's exposure counts are drawn from it; TASK-070's Risk Dashboard (DSH-010) composes the same inputs. Nothing is recalculated | WF-06 §13.2; FG-01 DSH-010, MET-RISK-CRIT, BR-DSH-004 |
| D-7 | **Rating colours follow the published order.** The version carries no colours (TBC-RSK-005), so a rating's rank among the version's ratings is spread over five steps, least to most severe (`severityStep`): a light ground with a dark text of 6.4:1 to 8.9:1. The most severe rating is always the top step whatever the number of ratings | TBC-RSK-005; WCAG 1.4.3 |
| D-8 | **Criterion 2: MOD-035 refuses a blank rationale before sending, and shows the API's refusal on the same field.** `checkClosure` requires text that is not only whitespace (≤ 2000), so "Close risk" with nothing or spaces sends nothing and marks the field "Enter the rationale."; the API's own 400 (`rationale` or `rationale.text` REQUIRED) lands on the same field. The dialog says what closing does: an ACTIVE acceptance ends, open actions stay (counted), history is kept | Acceptance criterion 2; workbook check 2; TASK-055 §4 |
| D-9 | **MOD-032** offers the version's probability levels and, for each impact dimension the version defines (named from IMPACT_DIMENSION), that dimension's levels, with their labels. A reassessment starts from the latest levels that still fit the version. The preview marks the cell (probability, highest impact) on the matrix and names its rating, saying the rating saved is the server's. Only the levels and an optional basis are sent (TASK-055 D-4) | WF-06 §8.8 MOD-032; §12.3 |
| D-10 | **MOD-031 and MOD-033 are the same PUT** (R-5): MOD-031 edits the narrative, category and dates and keeps the owner; MOD-033 re-sends every field with another owner, the unchanged text keeping its original language. Both send the ETag. MOD-030 names the owner on creation. The owner field is the tasks' (`AssigneeField`, given a `name` so the API's `ownerUserId` refusal lands on it): the person, the Project Manager, the action owners known, someone else by search or id, or none. The API decides who may own (RISK_OWNER_NOT_ELIGIBLE) | TASK-055 §4; `task-boards-ui.md` |
| D-11 | **MOD-034** plans a treatment action: response (mitigate, avoid, transfer, contingency), title, details, target date and owner (the risk's by default). The dialog says the rating does not change until reassessed. A live action is edited, started, completed or cancelled from the treatment plan; its command reads the action for its own ETag first | WF-06 §8.8 MOD-034; TASK-055 D-6 |
| D-12 | **Commands are confirmed in words** (`ConfirmCommandDialog`): start treatment, monitor, revoke acceptance, reopen, and an action's start/complete/cancel each say what they do before anything is sent. A 412, an invalid transition, a closed risk or a final action reads the risk again with "The risk changed while you were working on it"; any other refusal stays in the dialog | `milestone-ui.md` D-9 |
| D-13 | **What is offered** (`access.ts`, navigation only). Register (MOD-030): the project's own Project Manager on an APPROVED_PLANNED, ACTIVE or SUSPENDED project. Edit, owner, actions, treatment, monitoring and close: the Project Manager on an open risk, COMPLETED projects too. Assess, accept, revoke and reopen: an internal person the project's scope reaches — the API refuses RISK_ASSESS and RISK_ACCEPT to an external user, and RISK_REOPEN is its own permission (TASK-055 D-8) | ADR-013; TASK-055 §4 |
| D-14 | **The register's order and views.** Most severe first (the version's order), then the earliest review; unassessed after rated, closed last. Views: open (default), review due, not assessed, closed, all; the rating filter offers the version's ratings. An unassessed risk reads "Not assessed", never a low rating; a review due on or before today (UTC) marks the row and says "Review due" with an icon. Across projects the API is read one project at a time, six at once (TASK-055 F-9, as `milestone-ui.md` D-2) | WF-06 §8 SCR-080 |

## 3. Screens and routes

| Screen | Route | Reads | Offers |
| --- | --- | --- | --- |
| SCR-080 (project) | `/projects/:id/risks` | `/risks?projectId`, the matrix, RISK_CATEGORY/IMPACT_DIMENSION | Summary cards, view and rating filters, the table; MOD-030 |
| SCR-080 (across projects) | `/risks` | `/projects?status=…`, then each project's `/risks` | The same, each risk naming its project |
| SCR-081 | `/risks/critical` | The matrix, then as `/risks` | The exposure heat-map with counts per cell; the critical list and its criterion |
| SCR-082 | `/projects/:id/risks/:riskId` | `/risks/{id}` (ETag), `/risk-assessments`, `/risk-acceptances`, `/risk-treatment-actions`, the matrix | The commands (D-13), current exposure on the matrix, histories |
| MOD-030/031 | SCR-080, SCR-082 | — | `POST /risks`, `PUT /risks/{id}` |
| MOD-032 | SCR-082 | The matrix | `POST /risks/{id}/assess` |
| MOD-033 | SCR-082 | — | `PUT /risks/{id}` |
| MOD-034 | SCR-082 | `/risk-treatment-actions/{id}` (edit) | `POST`/`PUT /risk-treatment-actions` |
| MOD-035 | SCR-082 | — | `POST /risks/{id}/close` |

### 3.1 Empty and unavailable states

| Where | State | Text |
| --- | --- | --- |
| SCR-080 | No risks | "No risks registered" — "Register the first risk…" for the Project Manager, "The Project Manager registers this project's risks." otherwise |
| SCR-080 | Filters match nothing | "No risks match this view." (WF-06 §8) |
| SCR-081 | No risk at the top rating | "No open risk has the most severe rating." |
| Any matrix | 403 / 422 | "You cannot read the risk matrix…" / "No risk matrix is published yet…" (D-3) |
| SCR-082 | Not assessed | "The risk has not been assessed, so it has no rating yet." |

## 4. Verification

Run 2026-10-06 on macOS with Docker Desktop: frontend in `AHDA-frontend` (Node 24); the API image built from TASK-055 (`ahda-api`).

| # | Command | Result |
| --- | --- | --- |
| 1 | `npm run lint` | 0 problems |
| 2 | `npm run format:check` | All matched files use Prettier code style |
| 3 | `npm run typecheck` | No errors |
| 4 | `npm test` | 39 files, 565 tests passed (37 files, 524 on `dev`). 41 new: 17 in `risks/rules.test.ts`, 24 in `risks/risks.test.tsx`. The two tab assertions in `projects` include Risks |
| 5 | `npm run build` | Built. 1,427 KB JS (348 KB gzip); the >500 KB chunk warning was already on `dev` |
| 6 | `npm audit` | 1 high (`source-map-js` 1.2.1, GHSA-68fv-2mgg-jv7q, through `jsdom` and `vite`): not introduced here, `package.json` and the lockfile are unchanged (F-5) |
| 7 | — | No backend file changed; the `dotnet` gates do not apply |
| 8 | Live fixture (git-ignored `artifacts/task-056/live`, not committed; `reset.sh` rebuilds it): scratch database `task056_live`, migrated by `ahda-migrate` and seeded by the compose seed; `setup.sql` adds project A (ACTIVE, managed by local.r02), two RISK_CATEGORY items, a published GOVERNANCE_PROFILE, and **test grants to R02** — the risk permissions (TASK-055 F-2) and PROJECT_VIEW, CONFIGURATION_VIEW, MASTER_DATA_VIEW (F-1); a second API container on 5086. `prepare.py` publishes RISK_MATRIX v2 in SQL (Low < Medium < High) and, as local.r02 over HTTP, registers three risks and assesses two | The real API's representations match `api/types.ts`: the resolution's `versionId`, `versionNo`, `effectiveFrom`, `content.{probabilityLevels, impactLevels, riskRatings, riskMatrixCells}` and `RiskDetail.currentAssessment`. **Server side of criterion 2**: close with no rationale → 400 `rationale` REQUIRED; empty and whitespace-only text → 400 `rationale.text` REQUIRED; the risk stays ASSESSED |
| 9 | **Browser check** (`check.mjs`): Chromium headless with puppeteer-core 24 inside `AHDA-frontend`, a second Vite on 5176 forwarding `/api` to the scratch API, signed in for real as local.r02; nothing is served by the browser. Phase *before* on v2; `prepare.py republish` publishes v3 (Minor < Severe, another mapping); phase *after* loads the same screens again; phase *a11y* | **40 of 40 checks passed** (`check-output.txt`). **Validation check 1**: v2 drew 5 × 5 with Low/Medium/High, row 5 `LOW, MEDIUM, HIGH, HIGH, HIGH`, "Flooding" (4 × 5 → High) listed critical under "rated High, … version 2"; after v3 was published, the next load drew Minor/Severe, row 5 `MINOR, SEVERE, SEVERE, SEVERE, SEVERE`, "rated Severe, … version 3", no risk critical; SCR-082 kept "High" as given, marked 4:5 on v3's map and said the rating came from an earlier version. **Validation check 2**: "Close risk" with an empty, then a whitespace-only rationale read "Enter the rationale." and sent no request. Register order and "Not assessed"; Arabic mirrored with the version's Arabic labels. axe with colour contrast on SCR-080 (both), SCR-081 and SCR-082 in English and Arabic, and on MOD-030 to MOD-035: no violation; no sideways page scroll at 390 px on the four screens in both languages; every field of every modal inside its dialog. Screenshots `out/` |

The first browser run found two defects, fixed and run again from a fresh database: MOD-032's preview widened the dialog past its edge (the matrix's width held the form's grid column; the preview now shrinks and the matrix scrolls in a focusable, named region, which axe then required), and the per-cell count read "1 risks" (now "Risks: 1").

The tests, by acceptance criterion:

| Item | Tests |
| --- | --- |
| 1. The matrix reflects the published configuration, not a hardcoded one | `risks.test.tsx`: SCR-081 draws v1's cells, row and column headers, counts and caption; a re-published 3 × 3 version re-renders with its own cells, labels and criterion on the next load; 422 and 403 draw no grid; SCR-082 marks the risk's cell and says when its rating came from another version; MOD-032's options and preview from the version; a missing matrix offers nothing to send. `rules.test.ts`: `riskMatrixOf`, `ratingAt`, unmapped cells, `severityStep`, `criticalRating`/`isCritical` under two versions, reassessment levels that no longer fit. Live: rows 8, 9. M-3 to M-8 |
| 2. Closing requires a non-empty rationale, client- and server-side | `risks.test.tsx`: empty and whitespace-only refused with no POST; the API's 400 on `rationale.text` shown on the field; a valid close sends the trimmed text and the ETag; 412 reads the risk again. `rules.test.ts`: `checkClosure`. Live: rows 8 (server) and 9 (client). M-1, M-2 |
| Workbook validation checks | Live, rows 8, 9 |
| MOD-030 to MOD-034, commands | Exact bodies and If-Match on create, edit, owner, assessment, action and an action's command; client checks before sending; an ineligible owner's refusal on the field; who is offered what (`rules.test.ts`, access) |

### 4.1 Mutation tests

Each mutation was applied to the source, `vitest run src/features/risks` run in `AHDA-frontend`, and the source restored (`artifacts/task-056/mutate.py`, `mutation-results.txt`). All eight were caught; the restored baseline passed 41 of 41.

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | The closure rationale is optional in the form | 2 |
| M-2 | The API's field refusal is not shown on MOD-035 | 1 |
| M-3 | The impact columns are 1–5 whatever the version defines | 2 |
| M-4 | Every cell takes the top rating, ignoring the published mapping | 6 |
| M-5 | "Critical" is the code `CRITICAL`, not the version's top rating | 2 |
| M-6 | The probability rows are 5–1 whatever the version defines | 2 |
| M-7 | An unreadable matrix (403) is an error, not said | 1 |
| M-8 | A missing matrix (422) is drawn as a default grid | 2 |

## 5. Acceptance criteria and deliverables

| Item | Status |
| --- | --- |
| The risk matrix visualization correctly reflects the currently published probability/impact configuration (not a hardcoded matrix) | **Met** (D-2, D-3; §4 rows 4, 9; M-3 to M-8) |
| Closing a risk requires a non-empty closure rationale enforced client- and server-side | **Met** (D-8; §4 rows 4, 8, 9; M-1, M-2). The server side is TASK-055's and was confirmed against it |
| React components/routes for SCR-080–082 and MOD-030–035 | **Delivered** (§3) |
| Risk Dashboard widget composition inputs | **Delivered** as `riskSummary` (D-6); the widgets are TASK-070's |
| The 5×5 matrix rendered as a visual heat-map | **Delivered** (D-2, D-7); 5 × 5 when the version is |

## 6. Findings

| # | Finding | Owner | Effect until resolved |
| --- | --- | --- | --- |
| F-1 | **The Project Manager cannot read the matrix or the risk catalogues.** The heat-map needs CONFIGURATION_VIEW and category and dimension names MASTER_DATA_VIEW; both ship to R01 only, while RISK_VIEW/RISK_MANAGE ship to R04. In a real environment SCR-081 and SCR-082 say "You cannot read the risk matrix", MOD-032 cannot be filled, and MOD-030 cannot choose a category ("The risk categories cannot be read"). Same cause as `project-registration-ui.md` F-1. Either Appendix A grants R04 (and the AHDA assessors) CONFIGURATION_VIEW and MASTER_DATA_VIEW, or the API serves the RISK_MATRIX in force and the PUBLISHED RISK_CATEGORY / IMPACT_DIMENSION items to anyone holding RISK_VIEW | PMO (Appendix A); Engineering Architect (API) | No heat-map and no risk registration for any shipped role |
| F-2 | **"Critical" has no configured threshold** (TBC-RSK-005). SCR-081 takes the version's most severe rating (D-4); AHDA may want "High and above", or a flag on the rating. Ratings must be published in ascending severity for D-4 and D-7 to hold — the configuration contract does not say so | AHDA (OQ-006); FG-04 | SCR-081 lists only the top rating |
| F-3 | **The spec's risk model is wider than the API's.** WF-06 §4 and §10 have Draft/Open/Withdrawn/Materialized states, inherent and residual ratings, trend, cause/event/effect, escalation, review records and Task linkage; TASK-055 serves IDENTIFIED…CLOSED, one assessment type and none of the rest. The screens show what the API has; SCR-080's trend, inherent/residual and escalation columns and filters are absent, and MOD-030 asks for one description ("cause, uncertain event and effect") | Engineering Architect; WF-06 owner | Those columns and panels are missing |
| F-4 | **No dashboard endpoint** (`POST /api/dashboards/risk/data`, API-DSH-013, is not built) and no cross-project risk list (TASK-055 F-9): SCR-080 across projects and SCR-081 read every visible project's risks, six at a time | TASK-070; Engineering Architect | Slower with many projects |
| F-5 | **`npm audit` reports one high advisory** (`source-map-js` ≤ 1.2.1, GHSA-68fv-2mgg-jv7q, a dev-time dependency of `jsdom` and `vite`), published after TASK-053's 0. This change adds no dependency; `npm audit fix` changes the lockfile and belongs in its own change. CI runs no `npm audit` | Maintainer | None at runtime (dev tooling) |
| F-6 | **No Security Lead review is requested (CTL-43)**: this change touches no authentication, RBAC, data scope or upload; commands are offered as navigation only and decided by the API | Maintainer | — |

## 7. Change log

| Date | Change |
| --- | --- |
| 2026-10-06 | Created (TASK-056) |

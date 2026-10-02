# Progress Reporting UI (WF-02 Frontend)

| Field | Value |
| --- | --- |
| Task | TASK-045 — Build Progress & Schedule UI (WF-02/WF-03 Frontend) (P8 - Execution & Performance). Built as WF-02 only: `solution-architecture.md` §11.4(a) resolved that TASK-045's content is WF-02 (F-5) |
| Depends on | TASK-044 — the WF-02 API (`progress-update.md` §3–§5): every screen reads and writes through it, and every refusal shown is one it returns. TASK-042 — the project workspace (SCR-040) the Progress tab sits in (`project-registration-ui.md`). TASK-032, TASK-036 — the SPA foundation |
| Record date | 2026-10-02 |
| Status | **BUILT AND VERIFIED LOCALLY**: component tests against a mocked API (§4 row 4), nine mutation tests (§4.1), and the request shapes and refusals against the compose API rebuilt from `dev` (§4 rows 8–9). **In a real environment no progress can be reported through it yet**: the API derives no figure until WF-03, WF-04 and WF-14 exist (TASK-044 F-1), no role holds `PROGRESS_REVIEW` (TASK-044 F-2), and the cadence and health thresholds are unconfigured (TASK-044 F-3, F-4). Not driven end to end in a browser (F-11) |
| Branch | `feat/task-045-wf02-progress-frontend` |
| Deliverables | React components and routes for SCR-048, SCR-070 and MOD-020–022 in `src/frontend/src/features/progress` (`ProjectProgress.tsx`, `ProgressHistory.tsx`, `dialogs/`, `components/`, `api/`, `access.ts`, `progressUpdate.ts`, `presentation.ts`, `problems.ts`, `useHealthView.ts`, `usePeriods.ts`); i18n `features/progress/i18n/{ar,en}.json`. The workspace's Progress tab and its routes (D-1). 58 frontend tests. This record |
| Environment variables / secrets | None |
| Gate decision applied | **ADR-009**: planned and actual percentage side by side, with the variance, at project level; an overridden figure is marked and shows the calculated value beside it (D-3). Task and summary level have no API to read yet (F-2) |
| Participation amendment | **ADR-014**: a legacy-intake project shows its permanent intake marker on SCR-048 and SCR-070; its opening position has no variance, and each later variance is labelled as running from the intake date (D-5, F-6). **ADR-013**: the entity Project Manager reports; review is AHDA's and never the submitter's (D-7, D-8). **ADR-017**: the draft arrives pre-filled and confirming submits it unchanged (D-6) |
| Workbook read | The TASK-045 row as given in the task request, 2026-10-02 (description, acceptance criteria, directory, deliverables, gate decision, participation amendment); `progress-update.md`; `solution-architecture.md` §11.4(a), M-12; `erd.md` §4.1; `ADR-register.md` ADR-009, ADR-013; `project-registration-ui.md`. Blueprint Appendix B (the screen inventory) and the ADR-014 and ADR-017 texts are not in the repository (F-1, F-6) |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | SCR-048 Project Progress tab, SCR-070 Progress Update History, MOD-020 Submit Progress Update, MOD-021 Edit Progress Update Draft, MOD-022 Review Progress Update; loading, empty, error and refused states; Arabic and English | This task |
| **In** | The Progress tab in the project workspace (SCR-040) | This task (D-1) |
| **Out** | WF-03 schedule screens, and planned/actual at task and summary level | TASK-046/TASK-047, TASK-048 (F-2, F-5) |
| **Out** | The consolidated periodic update session (ADR-017) | TASK-107 |
| **Out** | Dashboards that render health (FG-01, FG-02) | TASK-069, TASK-071 |
| **Out** | The legacy intake path that records the opening position | TASK-104 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Where it lives.** The screens are in `features/progress` and take the project and the person as props. `projects/workspace/ProgressTab.tsx` lends them the workspace's context, as `DocumentsTab` does for the document library. Routes: `/projects/:projectId/progress` (SCR-048) and `/projects/:projectId/progress/history` (SCR-070), both behind `WorkspaceTabGuard`. The tab's audience is `reached`: `PROGRESS_VIEW` ships to R04 at OWN and R08 at ENTITY (TASK-044 D-10). An unreached caller would get an empty page, which reads as "nothing reported" rather than a refusal. The tab link is no longer matched exactly except for the overview, so the Progress tab stays current on its history page | ADR-002 module 2 → `features/progress`; `project-registration-ui.md` D-4 |
| D-2 | **Two semantic states, never merged (M-12).** Both screens open with the latest PUBLISHED/OFFICIAL snapshot and the CURRENT/LIVE health side by side, official first. Each has a badge that differs in word, fill and border, not colour alone: official is solid primary, live is a dashed outline. Each section carries the same border style and states its freshness: published at, or computed at (TASK-044 F-10). A published revision in the history is badged official in its row. The live value never shows an override marker: it is computed from the derived figures only (TASK-044 D-3) | Acceptance criterion 2; WCAG 1.4.1; `progress-update.md` §6 item 3 |
| D-3 | **ADR-009 side by side.** Planned, actual and variance appear together in each place a figure is shown: the official and live sections, the update in progress, the review modal and each history row. Variance is actual minus planned in percentage points, to the API's four places, with a true minus sign; behind plan is amber. An override shows an "Overridden" badge and the calculated value beside it ("Calculated 25%"). The figures are never inputs. The variance is a difference of two published figures shown beside them, not a health rating; health is always the API's stored value (M-12) | Gate decision; TASK-044 D-2, D-3 |
| D-4 | **Health is a word.** GREEN positive, AMBER warning, RED negative, UNKNOWN neutral grey, always with its label. No plan reads "No plan", and its variance "No variance without a plan" | TASK-044 D-4: UNKNOWN is never coerced to a colour |
| D-5 | **ADR-014.** The marker is the project's `legacyIntakeDate`, ERD's "permanent intake marker" (`erd.md` §5). SCR-048 and SCR-070 show it ("Legacy intake 2026-07-15") with what it means. The opening position, the revision with `projectIntakeId`, is labelled "Opening position at intake" and has no variance. Its figure was entered once and has no plan to vary from (TASK-044 D-7). Every other variance on such a project is labelled "since intake {date}". Periods themselves start the day after intake (TASK-044 D-5), so the figures run from intake forward without the UI computing anything else | Participation amendment; F-6 |
| D-6 | **MOD-020 and MOD-021 are one form.** Figures are shown, not typed. The person writes the narrative and chooses "Use the calculated figure" or "Override". An override asks for a percentage and a reason. The percentage is checked as `ProgressOverrideRequest.Validate` checks it: blank REQUIRED, not a number MALFORMED, below 0, above 100 or more than four decimal places OUT_OF_RANGE. A wrongly shaped value is flagged as typed; a missing one once saving is tried, with the count in the form alert and focus on the first invalid input. The API's field errors land on the input they name (`override.actualPercent`, `override.reason`, `narrative`; a `.text` suffix maps to the same input). The percentage has messages of its own, because the platform reads OUT_OF_RANGE as "choose a listed value". MOD-021 saves (PUT with If-Match). MOD-020 saves only if the form differs from the draft, then submits with the ETag the save returned. A pre-filled draft confirmed as it is is one command (ADR-017). 412, `PROGRESS_NOT_EDITABLE` and `INVALID_TRANSITION` close the modal, say the update changed, and read it again | Acceptance criterion 1; TASK-044 D-3, D-8; R-21 |
| D-7 | **MOD-022 is AHDA's gate.** A SUBMITTED revision is taken into review ("Start review"). One UNDER_REVIEW is published or returned, and returning needs a reason. Publishing says before it is sent that it is final and closes the period. The reviewer sees the figures side by side, an override with its reason, the narrative and who submitted it. The modal is offered to an internal person whose assignments reach the project and who did not submit the revision; the API decides (403 otherwise) | ADR-013; TASK-044 D-9 |
| D-8 | **Who reports.** Start, edit and submit are offered to the project's own Project Manager on an ACTIVE project: R04 at OWN, a project's owner anchor being its Project Manager (TASK-044 D-10). Anyone else sees whose move it is ("Waiting for AHDA to start the review") | ADR-013 amendment; F-8 |
| D-9 | **Starting.** "Start progress update" posts `{projectId}` and opens MOD-020 on the new draft. 409 `PROGRESS_SUBMISSION_EXISTS` is the retried start TASK-044 F-17 describes, or someone else's: the screen reads again and shows the update, as a notice, not an error. Other refusals (422 `PROGRESS_ROLLUP_UNAVAILABLE`, `CONFIGURATION_MISSING`, `PROGRESS_PROJECT_NOT_ACTIVE`; 409 `PROGRESS_NOTHING_TO_REPORT`) are explained beside the button | TASK-044 §4, §5 |
| D-10 | **SCR-070 pages in the address.** `?page=` with 25 rows (R-29), newest first as the API orders them. Loading, error with retry, refused (403) and empty states are distinct and never shown together. Each row names its period by dates from `/reporting-cycles`, read once in one page of 200 (F-3). A returned revision shows its reason. People are named as the workspace names them, or shortened when the reader may not read users | `useListParams`; `project-registration-ui.md` D-2 |
| D-11 | **Additions to the foundation, all backward compatible.** `progress` in `WorkspaceTabKey` and `WORKSPACE_TABS` (after Location); its tab label in `projects` i18n; the `progress` i18n namespace; `.badge--official`, `.badge--live` and the progress layout in `styles.css`; `src/test/progressFixtures.ts`. The three existing tab assertions now include Progress | — |

## 3. Screens and routes

All under `/projects/:projectId`, inside the SCR-040 workspace, behind `RequireSession` and `WorkspaceTabGuard tab="progress"`.

| Screen | Route | Component |
| --- | --- | --- |
| SCR-048 Project Progress tab | `progress` | `ProjectProgress` (via `ProgressTab`) |
| SCR-070 Progress Update History | `progress/history` | `ProgressHistory` (via `ProgressHistoryTab`) |
| MOD-020 Submit Progress Update | from SCR-048 | `ProgressUpdateDialog mode="submit"` |
| MOD-021 Edit Progress Update Draft | from SCR-048 | `ProgressUpdateDialog mode="edit"` |
| MOD-022 Review Progress Update | from SCR-048 | `ReviewProgressDialog` |

## 4. Verification

Run 2026-10-02 on macOS with Docker Desktop: frontend in `AHDA-frontend` (Node 24.21.0). The compose stack was rebuilt from this branch: `docker compose -f infra/docker/docker-compose.yml up -d --build --wait`. Before that, the running API predated TASK-044 and answered 404 on every progress path.

| # | Command | Result |
| --- | --- | --- |
| 1 | `npm run lint` | 0 problems |
| 2 | `npm run format:check` | All matched files use Prettier code style |
| 3 | `npm run typecheck` | No errors |
| 4 | `npm test` | 29 files, 305 tests passed (27 files and 247 before; 58 new in `features/progress`: 26 in `rules.test.ts`, 32 in `progress.test.tsx`). The three tab assertions in `projects` updated for the new tab |
| 5 | `npm run build` | Built. 942 KB JS (248 KB gzip), up from 897 KB (239 KB); the >500 KB chunk warning was already on `dev` (TASK-038 F-5) |
| 6 | `npm audit` | 0 vulnerabilities. No dependency added |
| 7 | The nine `docs/architecture/*-check.py` gates | All OK |
| 8 | curl through the SPA's proxy (`localhost:5173/api/v1`) as `local.r04`, with the reads the screens make, for a project id the caller cannot see | `GET /reporting-cycles?…&pageSize=200`, `/progress-submissions?…&page=2&pageSize=25`, `/published-progress-snapshots?…&pageSize=1`, `/project-health-statuses?…`: 200 `{items: [], page, pageSize, totalCount: 0}`, the page honoured. Without `projectId`: 400. `POST /progress-submissions {projectId}`: 404 R-23 envelope |
| 9 | `PUT /progress-submissions/{id}` with the body MOD-020 builds, override `120`, then `42.12345` | 400 `VALIDATION_FAILED`, `errors: [{field: "override.actualPercent", code: "OUT_OF_RANGE"}]` for both: the field path and code the modal maps to its percentage message (D-6), and the values `checkPercent` refuses before sending |

Not run: a browser walk through start → submit → review → publish. The local database holds no project. Reaching an ACTIVE one takes WF-01 review and activation with grants no role ships, and no figure can be derived (TASK-044 F-1) (F-11). jsdom's axe pass (contrast off) is clean on SCR-048 for an intake project, on SCR-070 with both states and a published row, and with MOD-022 open. The new badge colours are existing pairs: white on `--color-primary`, and `--color-info` on the background.

The tests, by acceptance criterion and amendment:

| Item | Tests |
| --- | --- |
| 1. An out-of-range percentage is rejected client- and server-side with a field-level error | `progress.test.tsx`: `120`, `-1`, `100.5`, `42.12345` flagged on the field as typed, form alert and focus on submit, nothing sent; the API's `override.actualPercent OUT_OF_RANGE` lands on the field with the percentage message, nothing submitted, cleared by typing; an override without its reason refused on the reason. `rules.test.ts`: `checkPercent` accepts 0, 100, 42.5, 33.3333, 100.0000 and refuses 12 values with the API's code |
| 2. History paginates; Published and current/live each have a distinct badge | `progress.test.tsx`: 30 revisions read as 25 + 5, `page=2&pageSize=25&projectId=…` on Next, the page status shown; official and live badges with different classes on SCR-048 and SCR-070; a published row badged official, the current one not; empty history; a returned revision's reason |
| ADR-009 | Planned, actual and variance in both states and rows; the override badge and calculated value in the history and in MOD-022; the live value never marked overridden; `toRequest` sends no derived figure |
| ADR-013 | The submitter is not offered review; an AHDA reviewer starts the review, returns with a reason and publishes; a reviewer is not offered the start; `canReview`/`canReport` by person and state |
| ADR-014 | The marker on SCR-048 and SCR-070; the opening position labelled and without variance; a later variance "since intake"; no marker on a project without intake |
| ADR-017 | Confirming a pre-filled draft sends only the submit; `differsFrom` treats `45` and `45.0000` as unchanged |
| States and refusals | Loading, retry after a failed read, 403 as the refused state, nothing reported yet, not ACTIVE, 409 `PROGRESS_SUBMISSION_EXISTS` as a notice, 422 explained beside the button, 412 closing the modal with the stale notice, the returned reason on the next draft |

### 4.1 Mutation tests

Each mutation was applied to the source, `vitest run src/features/progress` run in `AHDA-frontend`, and the source restored (script and output in the git-ignored `artifacts/task-045-mutations/`).

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | `checkPercent` accepts a value above 100 | 5: `120`, `100.5` client-side; `100.0001`, `101`, `250` rules |
| M-2 | The API's field errors are dropped | 1: server-side OUT_OF_RANGE |
| M-3 | The percentage reads the platform OUT_OF_RANGE message | 5: the four client-side values and server-side |
| M-4 | Both semantic states get the official badge | 2: SCR-048 and SCR-070 distinct badges |
| M-5 | The history always reads page 1 | 1: pagination |
| M-6 | Review is offered to the submitter | 1: `canReview` |
| M-7 | The opening position gets a variance | 1: ADR-014 on SCR-070 |
| M-8 | MOD-020 submits without saving the edit | 3: MOD-020 save-then-submit, MOD-021 save, server-side OUT_OF_RANGE |
| M-9 | An override is not marked | 3: SCR-048, SCR-070, MOD-022 |

## 5. Acceptance criteria and deliverables

| Item | Result |
| --- | --- |
| Submitting a progress update with an out-of-range percentage is rejected client- and server-side with a field-level error | **MET.** D-6; §4 rows 4 and 9; M-1, M-2, M-3 |
| The Progress History view paginates and shows Published vs. current/live values with a visibly distinct badge for each semantic state | **MET.** D-2, D-10; M-4, M-5 |
| Description: SCR-070, SCR-048, MOD-020–022, loading/empty/error states, inline validation for percentage/date fields | **MET for percentage fields; no date field exists** to validate: WF-02 takes no date from a person (F-4) |
| Deliverables: React components/routes for SCR-048/070 and MOD-020–022 | **MET.** §3 |
| Gate decision ADR-009 | **MET at project level**, the only level the API has. Task and summary level wait for WF-04 and WF-03 (F-2) |
| ADR-014 | **MET as read** (D-5); the decision text is not in the repository (F-6) |
| WF-03 (Schedule) in the task name | **Not built**: TASK-045 is WF-02 only (F-5) |

## 6. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **No screen inventory.** Blueprint Appendix B is not in the repository, so SCR-048, SCR-070 and MOD-020–022 have no field or layout specification. These screens follow the API contract (`progress-update.md`) and the earlier UI records | PMO | The layout may differ from the Blueprint's |
| F-2 | **Task and summary level have no source.** ADR-009 shows planned and actual "at task, summary and project level". WF-02's API is project level only, and WF-04 (TASK-048) and WF-03 (TASK-046) are not built (TASK-044 F-1). `Figures.tsx` takes any planned/actual pair, so those screens can reuse it | TASK-046, TASK-047, TASK-048 | Only project-level figures are shown |
| F-3 | **Periods are read in one page of 200** to name each revision's period. A project with more than 200 periods (a weekly cadence over four years) shows later revisions' periods by shortened id. A period on the revision, or a period filter, would remove the read | Engineering Architect, with TASK-044 | Long projects lose period dates in the history |
| F-4 | **No date field to validate.** The task asks for inline validation of date fields. WF-02 takes no date from a person: periods are generated from the cadence (TASK-044 D-5) and every request carries only the narrative, the override and a return reason. Schedule dates are WF-03's (F-5) | PMO (task wording) | — |
| F-5 | **WF-03 is not in this task.** `solution-architecture.md` §11.4(a) resolved TASK-045 as WF-02 only. Authorising the rename to "Build Progress UI (WF-02 Frontend)" is open (§11.4 Q5) | PMO | The task name still promises a schedule UI |
| F-6 | **ADR-014 and ADR-017 are not in the register** (ADR-001 to ADR-013). "Variance computed from intake forward" appears only in the task row; D-5 reads it with TASK-044 D-7 and ERD's intake marker | PMO; Engineering Architect | If ADR-014 means a variance re-based on the opening position, D-5 changes |
| F-7 | **No progress can be reported in a real environment yet**: TASK-044 F-1 (no inputs, 422 `PROGRESS_ROLLUP_UNAVAILABLE`, health UNKNOWN), F-2 (no review grant), F-3 and F-4 (422 `CONFIGURATION_MISSING`). The screens explain each refusal | As TASK-044 | The screens show empty states and refusals only |
| F-8 | **Reporting is offered to the project's Project Manager only** (D-8), from the one shipped grant. If AHDA grants `PROGRESS_SUBMIT` elsewhere, those holders would get the API's permission and no button | PMO (Appendix A); this module | Such holders cannot start an update from the UI |
| F-9 | **MOD-020 is two commands when the form changed**: a save, then the submit. If the submit is refused after the save succeeded, the draft keeps the edit and the modal shows the refusal; submitting again sends only the submit | Engineering Architect | — |
| F-10 | **Arabic strings are the delivery team's**, not reviewed by AHDA | AHDA | Wording may change |
| F-11 | **No browser end-to-end run** (§4): no ACTIVE project can be reached locally without test grants and a derivable figure, and no e2e suite is committed (TASK-085) | TASK-085 | The flow is verified in jsdom and by request shape only |

## 7. Change log

| Date | Change |
| --- | --- |
| 2026-10-02 | Created (TASK-045) |

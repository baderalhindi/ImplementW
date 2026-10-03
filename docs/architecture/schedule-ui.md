# Schedule, Gantt & Baseline UI (WF-03 Frontend)

| Field | Value |
| --- | --- |
| Task | TASK-047 — Build Schedule, Gantt & Baseline UI (WF-03 Frontend) (P8 - Execution & Performance) |
| Depends on | TASK-046 — the WF-03 API (`schedule-baseline.md` §3–§5): every screen reads and writes through it, every date shown is its calculation, and every refusal shown is one it returns. TASK-035 — WF-11, through which MOD-018 decides (`approval-framework.md` §3). TASK-042 — the project workspace (SCR-040) the Schedule tab sits in. TASK-032, TASK-036 — the SPA foundation |
| Record date | 2026-10-03 |
| Status | **BUILT AND VERIFIED LOCALLY**: component tests against a mocked API (§4 row 4), 14 mutation tests (§4.1), the request shapes and refusals against the compose API rebuilt from this branch (§4 row 8), and a browser check in Chromium at 1366 px and 390 px, in English and Arabic, with the real sign-in and the real API receiving every command (§4 row 9). **In a real environment a schedule can be planned and a first baseline activated under a Light profile, but**: no one can open MOD-018 until a role holds `APPROVAL_DECIDE` (TASK-035 F-1), a Standard or Full baseline cannot be submitted until AHDA configures the route (TASK-046 F-4), and no rebaseline can be authorised (TASK-046 F-1). Not driven from submission to activation in a browser (F-9) |
| Branch | `feat/task-047-wf03-schedule-gantt-frontend` |
| Deliverables | React components and routes for SCR-060, SCR-045, SCR-061 and MOD-015, MOD-017, MOD-018 in `src/frontend/src/features/schedule` (`ScheduleManager.tsx`, `GanttView.tsx`, `BaselineView.tsx`, `dialogs/`, `components/`, `api/`, `access.ts`, `dependencyRules.ts`, `gantt.ts`, `activityForm.ts`, `moveActivity.ts`, `presentation.ts`, `problems.ts`, `useFieldErrors.ts`, `useScheduleView.ts`, `useBaselineReview.ts`); i18n `features/schedule/i18n/{ar,en}.json`. The workspace's Schedule tab and its routes (D-1). 78 frontend tests. This record |
| Environment variables / secrets | None |
| Workbook read | The TASK-047 row as given in the task request, 2026-10-03 (description, acceptance criteria, directory, deliverables), and the workbook's own row (branch; validation check: "attempt to create a circular dependency in the UI and confirm client-side rejection with a clear message; visually confirm baseline vs. forecast are distinguishable in the Gantt legend"). `schedule-baseline.md`; `approval-framework.md` §3, D-10, F-1; `progress-ui.md`; `project-registration-ui.md`; `solution-architecture.md` §11.4(a). Blueprint Appendix B (the screen inventory) is not in the repository (F-1) |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | SCR-060 Schedule Manager, SCR-045 Gantt View, SCR-061 Baseline View; MOD-015 Add Dependency, MOD-017 Submit Baseline, MOD-018 Approve Baseline; drag-adjustment of dates held to the dependency rules; loading, empty, error and refused states; Arabic and English | This task |
| **In** | The activity form, the forecast form and the confirmations SCR-060 needs to be usable (add, edit, cancel an activity; update a forecast; remove a dependency; delete a draft baseline), which the task row names no modal for (F-2) | This task |
| **In** | The Schedule tab in the project workspace (SCR-040) | This task (D-1) |
| **Out** | SCR-044 (named in TASK-046's scope row, not in this task's row) | F-2 |
| **Out** | Milestones, execution actuals, working calendars | TASK-050, TASK-048, FG-04 (TASK-046 F-3, F-7, F-12) |
| **Out** | A change-request picker for a rebaseline | TASK-060 (F-5) |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Where it lives.** The screens are in `features/schedule` and take the project and the person as props. `projects/workspace/ScheduleTab.tsx` lends them the workspace's context, as `ProgressTab` does. Routes: `/projects/:projectId/schedule` (SCR-060), `…/schedule/gantt` (SCR-045), `…/schedule/baselines` (SCR-061, `?baselineId=` chooses the baseline shown), all behind `WorkspaceTabGuard tab="schedule"`; a pill navigation links the three. The tab follows Progress, audience `reached`: `SCHEDULE_VIEW` ships to R04 at OWN (TASK-046 D-12) and the API answers an empty page to anyone it does not reach | ADR-002 module → `features/schedule`; `progress-ui.md` D-1 |
| D-2 | **The schedule is read whole.** `useScheduleView` reads the schedule, every activity, every dependency, the baselines and the live Schedule Health together, page after page of 200 (R-29). The cycle check, the drag rules and the Gantt need every row; TASK-046 F-20 already reads the whole schedule on every write | Acceptance criterion 2 needs the whole graph |
| D-3 | **Every date shown is the backend's.** Planned, forecast and baseline dates, the kind (summary or activity) and each variance are shown as the API returns them; a person types only the inputs (parent, WBS code, name, requested start, duration, sort order; a forecast; a dependency). The variance is the API's signed working days, worded "2 days late", "On the baseline", and "No baseline" when null, never 0. Schedule Health is the stored value (TASK-046 D-17), worded On track / At risk / Delayed / Unknown, UNKNOWN grey. The two finishes in the summary (latest live leaf) are shown beside it, not rated | TASK-046 D-3, D-9; BR-SCH-002 |
| D-4 | **The Approved Baseline and the Current Forecast differ in shape, fill and position, not colour alone** (acceptance criterion 1, WCAG 1.4.1). On SCR-045 each row draws the forecast as a solid bar in the upper half and the baseline as a thinner hatched bar beneath it; a summary is a thin dark bar. The legend shows each mark with its name and a sentence, and names the ACTIVE baseline's version. SCR-060 shows Planned, Current Forecast and Approved Baseline as three labelled columns. A screen reader hears each row's two ranges and its variance | Workbook validation check: "distinguishable in the Gantt legend" |
| D-5 | **The Gantt fits the width it is given** (acceptance criterion 3). A bounded label column (7–17 rem) and a timeline taking the rest; every bar and axis label is positioned in per cent of the timeline (a week of margin either side), so nothing scrolls sideways at any width. The axis is weekly up to 70 days, else monthly, at most 12 labels, none so near the end that it would be cut off. SCR-060's and SCR-061's tables keep their date columns narrow (start over finish) and wrap their actions, so they fit at 1366 px too; below that the platform's `TableContainer` scrolls inside its own focusable, named region, never the page | "No horizontal scroll trap on a 1366 px laptop" |
| D-6 | **A cycle is refused before the request** (acceptance criterion 2). `dependencyRules.ts` mirrors `DependencyGraph`: the new predecessor → successor closes a cycle exactly when the successor already reaches the predecessor; the search is breadth first, so the chain shown is the shortest. MOD-015 shows it on the successor field as soon as the pair is chosen ("This would create a circular dependency: 1.1 → 1.2 → 2 → 1.1. …"), and on submit adds the form alert, moves focus to the field and sends nothing. The same check runs MOD-015's other rules in the API's order: both ends, two different activities, a pair not already linked (unique whatever the type, as the database's key), a lag of 0 to 9999. A cycle closed meanwhile by someone else is the API's 422 `SCHEDULE_DEPENDENCY_CIRCULAR`, explained in the dialog | VAL-SCH-008; TASK-046 D-4 |
| D-7 | **Drag-adjustment, held to the dependency rules.** What moves follows TASK-046 D-5: before an ACTIVE baseline the plan (a PUT of the activity with its requested start moved; the backend recalculates it and everything after), after it the Current Forecast (`reforecast`, duration kept); the baseline never moves. A live leaf's bar is a `role="slider"`: dragged with the pointer (pixels turned into whole days by the track's width), or moved with the arrow keys and Page Up/Down for a week, Enter to save, Escape to cancel. A start before the earliest its predecessors allow (FS the day after the predecessor's finish, SS its start, FF no finish before its finish, each plus the lag, on the same track) is held there, and the status line says why ("Cannot start before 2026-11-18: 1.2 comes first (finish to start, lag 0 days)."). A drop at the held position sends nothing. The reforecast form applies the same rule. Summaries never move; the plan does not move while a candidate is under review (TASK-046 D-8). In Arabic the timeline mirrors, and the arrows follow the screen | Task description "drag-adjustment of dates constrained by validated dependency rules"; F-3 |
| D-8 | **A drag never overwrites a change made meanwhile.** Before saving, the activity is read again for its ETag; if its `updatedAt` differs from the one drawn, nothing is sent and the chart is read again with the platform's stale notice, as a 412 would be. The PUT and `reforecast` send the ETag | R-21 |
| D-9 | **A forecast left behind its predecessor's slipped forecast is marked.** The plan cannot be in conflict (the backend holds it); a forecast is the planner's, so when a predecessor's forecast slips its successors can start too early until they are reforecast. Those rows get a dashed red outline and the legend explains it. Only the moved activity is held, not its successors, so a chain can be reforecast in any order | TASK-046 D-5 |
| D-10 | **MOD-017 says both outcomes before sending.** The client cannot read the governance profile's `requires_baseline_approval` (the resolved settings carry no such field), so the dialog says: with approval it goes to AHDA and the plan is frozen; without, it becomes the active baseline at once. The notice after follows the returned status (SUBMITTED or ACTIVE). A rebaseline over an APPROVED baseline asks for the change authorisation's ID (a UUID, checked before sending); replacing a Declared Baseline does not (TASK-046 D-13). `CONFIGURATION_MISSING` and `SCHEDULE_CHANGE_AUTHORIZATION_REQUIRED` are explained in the dialog | TASK-046 D-10, BR-SCH-034 |
| D-11 | **MOD-018 finds its task in the inbox.** `GET /approval-tasks` needs `APPROVAL_DECIDE`, the permission deciding needs, and lists only the tasks the caller may decide now; listing or reading a run needs `APPROVAL_VIEW`, which an approver may not hold (TASK-035 D-10). So the dialog is offered when the inbox holds a task whose subject is this baseline's revision (`Schedule`/`ProjectBaseline`/id/revision), to an internal person who did not submit it (ADR-013), and decides that task: approve, return or reject, a reason required to return or reject. A delegated task says on whose behalf. After approval the notice says the baseline becomes active when the approval is applied: WF-11 hands the outcome to the schedule through the outbox (TASK-046 D-7). The run's history is linked when the person may read runs; a 403 on either read means no part in the review, not an error | Found by the live check, §4 row 8 |
| D-12 | **SCR-061 shows only the ACTIVE baseline's variance.** A superseded baseline's frozen dates are history; no variance against them is computed in the browser. A candidate has no copy until it activates (TASK-046 D-8), a Declared Baseline none at all; each says so | TASK-046 D-9, D-13 |
| D-13 | **Additions to the foundation, all backward compatible.** `schedule` in `WorkspaceTabKey` and `WORKSPACE_TABS` (after Progress); its tab label in `projects` i18n; the `schedule` i18n namespace; the schedule and Gantt styles in `styles.css`; a pointer-capture stub in `src/test/setup.ts` (jsdom has none); `src/test/scheduleFixtures.ts`. The three existing tab assertions now include Schedule | — |

## 3. Screens and routes

All under `/projects/:projectId`, inside the SCR-040 workspace, behind `RequireSession` and `WorkspaceTabGuard tab="schedule"`.

| Screen | Route | Component |
| --- | --- | --- |
| SCR-060 Schedule Manager | `schedule` | `ScheduleManager` (via `ScheduleTab`); `ActivityDialog`, `ReforecastDialog`, `ConfirmDialog` |
| SCR-045 Gantt View | `schedule/gantt` | `GanttView` (via `GanttTab`): `GanttLegend`, `GanttChart` |
| SCR-061 Baseline View | `schedule/baselines` | `BaselineView` (via `BaselinesTab`) |
| MOD-015 Add Dependency | from SCR-060 | `AddDependencyDialog` |
| MOD-017 Submit Baseline | from SCR-061 | `SubmitBaselineDialog` |
| MOD-018 Approve Baseline | from SCR-061 | `ApproveBaselineDialog` |

## 4. Verification

Run 2026-10-03 on macOS with Docker Desktop: frontend in `AHDA-frontend` (Node 24). The compose stack was rebuilt from this branch: `docker compose -f infra/docker/docker-compose.yml up -d --build --wait`.

| # | Command | Result |
| --- | --- | --- |
| 1 | `npm run lint` | 0 problems |
| 2 | `npm run format:check` | All matched files use Prettier code style |
| 3 | `npm run typecheck` | No errors |
| 4 | `npm test` | 31 files, 383 tests passed (29 files and 305 before; 78 new in `features/schedule`: 38 in `rules.test.ts`, 40 in `schedule.test.tsx`). The three tab assertions in `projects` updated for the new tab. One run failed once in `documents/detail` ("a quarantined document is flagged…") with no change to that feature; it passed on rerun alone and in the full suite (F-10) |
| 5 | `npm run build` | Built. 1,049 KB JS (272 KB gzip); the >500 KB chunk warning was already on `dev` (TASK-038 F-5) |
| 6 | `npm audit` | 0 vulnerabilities. No dependency added |
| 7 | — | No backend file changed; the `dotnet` gates do not apply |
| 8 | curl through the SPA's proxy (`localhost:5173/api/v1`) as `local.r04`, with the reads the screens make and the bodies the dialogs build, for ids the local database does not hold | The five reads with `page=1&pageSize=200` (or none): 200 empty pages, the page size honoured; without `projectId`: 400 `projectId REQUIRED`. MOD-015's body: 422 `SCHEDULE_DEPENDENCY_INVALID`, `predecessorActivityId NOT_FOUND`; with `SF` and lag −1: 400 `dependencyType ENUM_VALUE`, `lagDays OUT_OF_RANGE` (both refused client-side first). The reforecast body with finish before start: 400 `forecastFinishDate DATE_BEFORE_START`. The activity PUT with duration 0: 400 `plannedDurationDays OUT_OF_RANGE`; without If-Match: 428. `GET /approval-instances?subjectModule=Schedule…` and `GET /approval-tasks`: **403** for R04, which holds neither approval permission — the finding behind D-11; both are read as "no part in the review" |
| 9 | Browser check (harness in the git-ignored `artifacts/task-047-browser`, not committed; F-9): Chromium 154 headless with puppeteer-core 24 inside `AHDA-frontend`, against the SPA (Vite) and the compose API. Sign-in is real (`local.r04`); every command goes to the real API. Only the reads the local database cannot answer are fixtures: an ACTIVE project managed by the caller, a 15-row schedule (3 summaries, 12 chained leaves, November 2026 to December 2027, the later ones forecast up to 18 days late), its ACTIVE baseline and copy, and AMBER health | **11 of 11 checks passed.** At 1366 × 768, English: SCR-045, SCR-060 and SCR-061 have no horizontal page scroll and no element scrolling sideways (0 px); the chart is 1,062 px in a 1,126 px main column and every bar lies inside its track. The legend's and the chart's marks differ by computed style: forecast solid `rgb(11, 93, 75)`, 17.6 px tall; baseline a `repeating-linear-gradient` hatch, 9.6 px tall. A real mouse drag of 3.1 by ten days sent `reforecast {"forecastStartDate":"2027-10-25","forecastFinishDate":"2027-11-13"}` (from 2027-10-15 to 2027-11-03, duration kept); the API answered 404 (no such activity locally) and the screen showed the stale notice and read again (D-8). MOD-015 with 3.3 → 1.1: the field invalid and focused, the chain of all twelve leaves explained, the form alert shown, 0 POSTs. At 1366 px in Arabic: no horizontal scroll on SCR-045 and SCR-060, and time runs right to left. At 390 px: no horizontal scroll on SCR-045. Screenshots were reviewed; three things were fixed and the check run again: the last axis label was cut off at the chart's end, the drag instructions touched the legend, and "Update forecast" wrapped in the actions column |

The tests, by acceptance criterion:

| Item | Tests |
| --- | --- |
| 1. The Gantt distinguishes Approved Baseline from Current Forecast | `schedule.test.tsx`: the legend's two items carry the two marks and name the ACTIVE baseline; each row's forecast and baseline bars have different classes, the same width, the forecast starting later (two days late); the row's text names both ranges. Browser row 9: computed fill, pattern and height differ |
| 2. A circular dependency is blocked with an explanatory error before the API call | `schedule.test.tsx`: 2 → 1.1 over 1.1 → 1.2 → 2 explained on the successor field with the chain as soon as chosen, linked by `aria-describedby`; on submit the form alert and focus, **no POST**; the same activity and a linked pair refused before sending; a valid pair sent as typed; the API's 422 explained. `rules.test.ts`: `cyclePath` finds the chain, finds none along it, a cycle of one; `checkDependency` codes in order; lag 0–9999. Browser row 9: 0 POSTs in Chromium |
| 3. Usable at 1366 px without a horizontal scroll trap | `schedule.test.tsx`: every bar and tick placed in per cent. `rules.test.ts`: `barPosition`, `daysForDistance`, at most 12 ticks, none past 92 %. Browser row 9: 0 px overflow, no sideways-scrolling element, English and Arabic |
| Drag-adjustment held by dependency rules | Keyboard move of the plan PUT with the requested start and the ETag; a move before the predecessor held at 2026-11-06 with the reason, nothing sent; a pointer drag reforecasts, the baseline bar unmoved; Arabic arrows mirrored; a changed activity not overwritten; Escape cancels; frozen plan not draggable. `rules.test.ts`: FS, SS, FF with lag; forecast vs plan track; the latest constraint binds; `moveDates` held; conflicts |
| MOD-017, MOD-018, SCR-061 | First baseline submitted with `changeAuthorizationId: null`; a rebaseline's ID checked, then sent, ACTIVE notice; `CONFIGURATION_MISSING` explained; the reviewer decides on the inbox task (`/approval-tasks/{taskId}/approve`), return needs a reason, the run never read; the submitter never offered it; a person without approval permissions offered nothing and shown no error; a delegated task names its delegator; the ACTIVE baseline's frozen copy with the API's variance; a candidate says it has no copy yet |
| States and access | Not initialized (the Project Manager sets it up, anyone else told who will); before a baseline nothing measured; frozen plan; 403 shown as the refused state; axe clean on SCR-060, MOD-015 with the cycle, SCR-045 in English and Arabic, and MOD-018 |

### 4.1 Mutation tests

Each mutation was applied to the source, `vitest run src/features/schedule` run in `AHDA-frontend`, and the source restored from a copy (script and output in the git-ignored `artifacts/task-047-mutations/`). The first run let M-10 survive: the only submitter in the tests was an external user, so `isInternal` hid the rule. An internal-submitter case was added; it now fails.

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | MOD-015 ignores a cycle | 2 |
| M-2 | The cycle check follows direct dependencies only | 3 |
| M-3 | The baseline bar is drawn as a forecast bar | 1: criterion 1 |
| M-4 | The legend gives the baseline the forecast mark | 1: criterion 1 |
| M-5 | Bars are placed in pixels | 1: criterion 3 |
| M-6 | A drag is not held by its predecessors | 2 |
| M-7 | FS allows a start on the predecessor's finish day | 6 |
| M-8 | The timeline does not mirror in Arabic | 1 |
| M-9 | A drag overwrites an activity changed meanwhile | 1 |
| M-10 | MOD-018 is offered to the submitter | 1 (after the added case) |
| M-11 | A plan drag is sent without If-Match | 1 |
| M-12 | The reforecast form ignores the predecessors | 1 |
| M-13 | A frozen plan can be dragged | 1 |
| M-14 | MOD-018 decides a task of another revision | 1 |

## 5. Acceptance criteria and deliverables

| Item | Result |
| --- | --- |
| The Gantt view visually distinguishes Approved Baseline from Current Forecast dates | **MET.** D-4; §4 rows 4, 9; M-3, M-4 |
| Attempting to create a circular dependency is blocked with an explanatory error before the API call is made | **MET.** D-6; §4 rows 4, 9; M-1, M-2 |
| Usable (no horizontal scroll trap) on a 1366 px laptop viewport | **MET.** D-5; §4 row 9 (0 px, English and Arabic); M-5 |
| Description: SCR-060, SCR-045, SCR-061, MOD-015, MOD-017, MOD-018, drag-adjustment constrained by dependency rules | **MET.** §3; D-7; M-6, M-7, M-12, M-13 |
| Deliverables: React components/routes for SCR-045/060/061 and MOD-015/017/018 | **MET.** §3 |
| Workbook validation check | **MET.** Circular dependency refused client-side with the chain (browser row 9); baseline and forecast distinguishable in the legend (browser row 9, screenshot `02-legend.png`) |

## 6. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **No screen inventory.** Blueprint Appendix B is not in the repository, so SCR-045/060/061 and MOD-015/017/018 have no field or layout specification. These screens follow the API contract (`schedule-baseline.md`) and the earlier UI records | PMO | The layout may differ from the Blueprint's |
| F-2 | **Modal numbering.** The row names MOD-015 and MOD-017–018; the activity, forecast and confirmation dialogs SCR-060 needs carry no MOD number here (MOD-016 is presumably one of them). SCR-044, in TASK-046's scope row, is in no task row read | PMO | Numbering may change when Appendix B is available |
| F-3 | **The forecast's dependency rule is the UI's alone.** The API holds the plan to the dependencies but takes any forecast with start ≤ finish (`ScheduleForecastRequest`, TASK-046 D-5). The drag and the reforecast form hold a forecast's start to its predecessors' forecasts (D-7); another client could set one that breaks them, which SCR-045 then marks (D-9) | Engineering Architect, with TASK-046 | Forecasts set outside this UI can contradict the dependencies |
| F-4 | **No one can decide a baseline in a real environment yet.** MOD-018 needs `APPROVAL_DECIDE`, which no role holds (TASK-035 F-1); the review-history link needs `APPROVAL_VIEW`, likewise. The live check shows 403 on both for R04 | PMO (Appendix A); TASK-110 | Standard and Full baselines cannot be approved; the screens show no decision and no error |
| F-5 | **A rebaseline asks for a change authorisation's ID by hand.** WF-08 is not built (TASK-060), so there is no picker, and no authorisation the API accepts (TASK-046 F-1) | TASK-060 | Rebaselines are refused |
| F-6 | **MOD-017 cannot tell in advance whether approval is needed.** The resolved GOVERNANCE_PROFILE settings the SPA reads carry no `requires_baseline_approval` (D-10) | Engineering Architect (configuration contract) | The dialog states both outcomes |
| F-7 | **The inbox is scanned for the task.** MOD-018 pages through the caller's inbox (200 a page) to find the baseline's task; a subject filter on `GET /approval-tasks` would make it one read | Engineering Architect, with TASK-035 | One extra page read per 200 open tasks |
| F-8 | **No dependency arrows on the Gantt.** Predecessors are listed under each activity on SCR-060 and in each row's text, not drawn | PMO | Dependencies are read, not seen, on the chart |
| F-9 | **No browser end-to-end run on real data** (§4 row 9): the local database holds no project, and reaching an ACTIVE one with a schedule takes WF-01 review and activation with grants no role ships. Row 9 checked layout, the drag, the cycle refusal and the real API's answers in a browser with fixture reads; no e2e suite is committed (TASK-085) | TASK-085 | Submission through activation is verified in jsdom and by request shape only |
| F-10 | **A flaky test outside this change.** `documents/detail` "a quarantined document is flagged…" failed once and passed on rerun; another of its tests is recorded as flaky in the same file (axe `document-title`, 2026-10-02) | TASK-038 owner | CI may need a rerun |
| F-11 | **Arabic strings are the delivery team's**, not reviewed by AHDA | AHDA | Wording may change |
| F-12 | **Every day is a working day** in the drag arithmetic and the duration hints, as in the API (TASK-046 F-3) | AHDA PMO; FG-04 | A calendar changes `dependencyRules.ts`' date functions, as it changes `WorkingDays` |

## 7. Change log

| Date | Change |
| --- | --- |
| 2026-10-03 | Created (TASK-047) |
| 2026-10-03 | TASK-049 (`task-boards-ui.md` D-11): `cyclePath` moved to `shared/graph`, the page reader to `shared/api/paging.ts` `readAllPages`, `useFieldErrors` to `shared/forms` (this module's file wraps it with `scheduleFieldMessage`); `ConfirmDialog` takes another feature's wording. `.table-container` is positioned, so a date range's hidden "to" no longer widens the page when SCR-060's table scrolls on a narrow screen. Behaviour unchanged: the 78 tests pass as they were |

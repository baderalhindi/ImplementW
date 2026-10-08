# Suspension & Closure UI (WF-09/WF-10 Frontend)

| Field | Value |
| --- | --- |
| Task | TASK-064 — Build Suspension & Closure UI (WF-09/WF-10 Frontend) (P10 - Governance & Change Control (WF-08/09/10)) |
| Depends on | TASK-062 — the WF-09 API (`suspension.md` §4, §5, §6 item 1): `/suspension-requests`, `/active-suspensions`, 10 operations. TASK-063 — the WF-10 API (`closure.md` §4, §5, §6 item 4): `/completion-cases`, `/closure-cases`, `/post-project-obligations`, 30 of its 31 operations used here (all but `ListReadinessChecks`: the case carries its latest readiness); the closed-project guard (D-8). TASK-035/036 — WF-11 and its modals (`approval-framework.md`, `approvals-ui.md`) |
| Record date | 2026-10-08 |
| Status | **BUILT AND VERIFIED LOCALLY**: 89 component, rule and sweep tests against a mocked API (§4 row 4), 14 mutation tests (§4.1), and **both workbook validation checks in Chromium against the real TASK-062/063 API** on a scratch database, signed in for real as the Project Manager (local.r04), the department's manager (local.r03, shipped grants) and AHDA's approver (local.r02, test grants): 30 of 30 checks, and 32 of 32 accessibility and layout checks (§4 rows 8, 9). **In a real environment** the Project Manager cannot read their own project on the shipped grants, so the workspace's Suspension and Closeout tabs are out of their reach and SCR-109/110/112/113 are reached by address (F-1); and nothing can be reviewed until AHDA publishes the four routes (TASK-062 F-3, TASK-063 F-3) |
| Branch | `feat/task-064-wf09-10-suspension-closure-frontend` |
| Deliverables | React components and routes for SCR-108–113 in `src/frontend/src/features/suspension-closure`: `SuspensionRegisterPage.tsx`, `ProjectSuspension.tsx`, `SuspensionFormPage.tsx`, `SuspensionDetailPage.tsx` (SCR-108–110); `CloseoutRegisterPage.tsx`, `ProjectCloseout.tsx`, `CloseoutFormPage.tsx`, `CloseoutDetailPage.tsx` (SCR-111–113); `components/` (`CloseoutStages`, `ReadinessPanel`, `ObligationsPanel`, `GovernedState`, `LifecycleBanner`, the two register views), `dialogs/` (`WaiveCheckDialog`, `ObligationDialog`), `api/`, `governedRequest.ts`, `suspensionRules.ts`, `closeoutRules.ts`, `presentation.ts`, `problems.ts`, `paths.ts`, `routes.tsx`, `useSuspensionClosureData.ts`; i18n `features/suspension-closure/i18n/{ar,en}.json`. The workspace's Suspension and Closeout tabs, its lifecycle banner and read-only mode; `/suspension-requests` and `/closure-requests` with a sidebar group and home-page links; WF-11's subject links. `projects/access.ts` `isClosed`, applied to the six controls that ignored the project's state (D-7). 89 frontend tests. This record |
| Environment variables / secrets | None |
| Gate decision applied | None recorded for TASK-064 (the workbook's Gate Decision cell is empty) |
| Workbook read | The TASK-064 row of `AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`, read 2026-10-08: description, acceptance criteria ("The Closure UI visually distinguishes the Completion stage from the Closure stage as two sequential steps, not one combined action; a Closed Project's workspace renders in a clearly read-only mode with edit controls disabled or hidden"), directory, deliverables, branch, and the validation checks "Walk a Project through Completion then Closure in the UI and confirm the two stages are presented distinctly; open a Closed Project and confirm no edit control is present or enabled". The WF-09 Functional Specification §13 and §13.1 (SCR-108–110, resumption as a sub-flow of the suspension's detail), the WF-10 Functional Specification §14, §14.1–§14.3 (SCR-111–113 reused as a two-stage closeout, "approval vs activation shown separately", CapabilityBanner); the Step 16 Frontend Registers rows for SCR-108–113 — all in `Project Files.zip`. `suspension.md`, `closure.md`, `change-request-ui.md` |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | SCR-108 Suspension Requests: a project's (workspace tab, with its suspension periods) and across projects, with the specification's views (open, approved pending activation, in effect, historical) | This task |
| **In** | SCR-109 Create Suspension Request, for a suspension or a resumption, also used to correct and resubmit | This task |
| **In** | SCR-110 Suspension Request Detail: approval and effect apart, the period, the resumption as its sub-flow, WF-11's MOD-040–042 and MOD-044 | This task |
| **In** | SCR-111–113 reused for the closeout's two stages: the stage stepper, Create Closure Request for either stage, the case's detail with the server's readiness, waivers and post-project obligations | This task |
| **In** | A CLOSED project's workspace read-only, on every tab | This task |
| **Out** | SCR-110's impact, readiness and review tabs; SCR-113's reconciliation, deliverables, open items, lessons and documents tabs; SCR-031 Suspended Projects | No API (TASK-062 F-1, TASK-063 F-1); SCR-031 is FG-01's |
| **Out** | The WF-08/09/10 regression suite | TASK-065 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Where it lives.** A project's registers are two workspace tabs after Change requests, audience `reached`: **Suspension** (`/projects/:id/suspension`, SCR-108) and **Closeout** (`/projects/:id/closeout`, SCR-111). The forms and details are under `/suspension-requests` (`new?projectId=&type=`, `:id`, `:id/edit`) and `/closure-requests` (`new?projectId=&stage=`, `:stage/:caseId`, `:stage/:caseId/edit`), and the two cross-project registers are their index routes, with a sidebar group and home-page links. The details and forms work without the project (403 and 404 alike), as SCR-106/107 do (`change-request-ui.md` D-1). WF-11's `SubjectSummary` links `Suspension.SuspensionRequest`, `Closure.CompletionCase` and `Closure.ClosureCase` subjects to their details, so an approver reads the record before deciding. The Step 16 `/views/scr-1xx/…` routes are proposals only | ADR-002; `change-request-ui.md` D-1 |
| D-2 | **Acceptance criterion 1: two sequential stages, never one action.** `CloseoutStages` draws the closeout as an ordered list of two steps — "Stage 1 of 2 · Completion", "Stage 2 of 2 · Closure" — each with its own marker, state badge, sentence, case link and action. The states come from `closeoutStagesOf(project status, completion cases, closure cases)`: Stage 1 is *Ready to start* on an ACTIVE project, *In progress* while a case is open, *Done* once effected, *On hold* while SUSPENDED, *Skipped* when a suspended project was closed without it; Stage 2 *Waits for Stage 1* (dashed, no action) until the project is COMPLETED, then *Ready to start*, or *Ready* on the terminal path from SUSPENDED. The stage whose turn it is carries `aria-current="step"`. An **approved** completion does not open Stage 2: only its activation (the project COMPLETED) does. Each stage offers only its own action (*Raise completion request*, *Raise closure request* or *Raise closure without completion*); there is no combined action anywhere. SCR-112 says which stage it raises and what follows; SCR-113 shows the stepper with its own stage outlined as "This request" | Acceptance criterion 1; workbook check 1; WF-10 §14, TASK-063 D-3, D-4 |
| D-3 | **Approval and activation are two labelled dimensions** (`GovernedStateSection`, both workflows): **Approval (WF-11)** — Not submitted, Awaiting review, In review, Returned, Approved, Rejected, Withdrawn — and **Effect on the project** — Not applicable yet, *Approved, not yet in effect*, In effect — with the lifecycle status, the project's lifecycle and a sentence per type and status ("Approved, not yet in effect. The project stays active until the request is activated; its readiness is checked again first"). An effected record keeps *Approved*. The live run caught a case in the window between WF-11's approval and the activation pass, reading exactly that | WF-10 §14.2 "approval vs activation shown separately"; TASK-062 D-5, TASK-063 D-4 |
| D-4 | **Readiness is the server's** (`ReadinessPanel`): the roll-up, when it was evaluated, and each criterion with its result, blocking count, whether it may be waived and the waiver given. A failing criterion links to the workspace tab where its records are settled (`CHECK_SOURCES`, TASK-063 F-7). The raiser evaluates it on SCR-113; submission and activation re-evaluate on the server, and a CLOSURE_BLOCKER_EXISTS refusal names each failing criterion ("These criteria fail without a waiver: Progress reported…"). AHDA's internal reviewer, never the raiser, waives a criterion that failed and may be waived, with a reason (`WaiveCheckDialog`); `DECISIONS_SETTLED`, `SUSPENSION_REQUESTS_SETTLED` and the obligation criteria say *Cannot be waived*. The SPA computes no roll-up | TASK-063 D-5; Step 16 "No browser business calculation" |
| D-5 | **Post-project obligations** (`ObligationsPanel`) are listed on the Closeout tab and on SCR-113, recorded against the case `obligationCaseOf` names (the open or effected completion case, or an open terminal closure), and moved by the Project Manager (start, satisfy, cancel) or waived by AHDA; a settled one offers nothing. The owner is chosen with the tasks module's `AssigneeField` — the person, the Project Manager, an existing owner, or someone else found (USER_VIEW) or given by id — because USER_VIEW ships to R01 only (F-3). The panel says they may stay open after Completion and that Closure waits for them | TASK-063 D-9 |
| D-6 | **WF-09: resumption is a sub-flow of the suspension** (WF-09 §13): SCR-110 of a suspension in effect shows its period and either links its resumption request or offers *Request resumption* to the Project Manager; a resumption links back to the suspension it ends. SCR-108 offers *Request suspension* on an ACTIVE project and *Request resumption* on a SUSPENDED one, only while no request of that type is open. A planned resumption date is labelled "planning information only" | TASK-062 D-8, D-10, BR-SUS-031 |
| D-7 | **Acceptance criterion 2: a CLOSED project is read-only on every tab.** The workspace wraps itself in `workspace--read-only` and shows `LifecycleBanner` on every tab — "Closed: read-only. This project was closed on {date}. Its records are kept as history; nothing in any tab can be changed", with a link to the closeout (suspended and completed projects get their own banners). An audit of every tab found most write controls already gated on the project's state, and six that were not: the Documents tab's *Upload* (no predicate), the progress review, the financial review and KPI publication (`reviews()`), the baseline decision (MOD-018) and the issue detail's escalation resolve and withdraw. `projects/access.ts` gains `isClosed(status)`, applied to those six; WF-09/10's own commands apply it through `governedRequest.ts` and `obligationCommands`. Tasks and milestones with no records now say they are not added while the project is suspended, completed or closed, not "planned once approved" | Acceptance criterion 2; workbook check 2; TASK-063 D-8 |
| D-8 | **Who is offered what** (`governedRequest.ts`, navigation only). Raise, edit, delete a draft, submit, withdraw before review: the requester or the project's Project Manager (…_RAISE at OWN, internal or entity). Start review and *Activate now*: an internal person who did not raise it and whose assignments reach the project, or anyone internal but the raiser when the project cannot be read (ADR-013). A decision: an internal person whose inbox holds the task, never the run's requester; MOD-044 by the run's requester while PENDING. Activation is the worker's; *Activate now* is AHDA's immediate apply or retry (F-6). Nothing is offered on a CLOSED project | TASK-062 D-9, TASK-063 D-10 |
| D-9 | **Lists.** Most recently changed first (the API's order; across projects merged by `updatedAt`, projects ACTIVE to CLOSED read first, six at a time with `readInBatches`). SCR-108 filters by view and type; SCR-111 by stage and readiness, each case naming its stage, the project's lifecycle across projects, its readiness and how many criteria fail | WF-09 §13 SCR-108, WF-10 §14.1 |

## 3. Screens and routes

| Screen | Route | Reads | Offers |
| --- | --- | --- | --- |
| SCR-108 (project) | `/projects/:id/suspension` | `/suspension-requests?projectId`, `/active-suspensions?projectId` | View and type filters; periods; *Request suspension* / *Request resumption* to the Project Manager |
| SCR-108 (across) | `/suspension-requests` | `/projects?status=…`, then each project's requests | Filters, table naming the project |
| SCR-109 | `/suspension-requests/new?projectId=&type=`, `/suspension-requests/:id/edit` | `/projects/{id}` (optional), `/suspension-requests/{id}` (ETag) | Save draft; Submit for review (`POST`/`PUT`, then `submit`, If-Match) |
| SCR-110 | `/suspension-requests/:id` | the request (ETag), the project (optional), the project's requests, `/approval-tasks`, `/approval-instances?subjectModule=Suspension…` | Withdraw, delete, start review, activate (D-8); MOD-040–042, MOD-044; resumption (D-6) |
| SCR-111 (project) | `/projects/:id/closeout` | `/completion-cases`, `/closure-cases`, `/post-project-obligations` `?projectId` | The two stages and their actions; the cases; obligations |
| SCR-111 (across) | `/closure-requests` | `/projects?status=…`, then each project's cases of both stages | Stage and readiness filters |
| SCR-112 | `/closure-requests/new?projectId=&stage=`, `/closure-requests/:stage/:caseId/edit` | `/projects/{id}` (optional), the case (ETag) | Save draft (`POST`/`PUT`) |
| SCR-113 | `/closure-requests/:stage/:caseId` | the case (ETag), the project (optional), the project's cases and obligations, WF-11 as SCR-110 | Evaluate readiness, waive, submit, withdraw, delete, start review, activate; MOD-040–042, MOD-044; obligations |

## 4. Verification

Run 2026-10-08 on macOS with Docker Desktop: frontend in `AHDA-frontend` (Node 24); the API and migrate images as built for TASK-063 (no backend change since).

| # | Command | Result |
| --- | --- | --- |
| 1 | `docker exec -w /repo/src/frontend AHDA-frontend npm run lint` | 0 problems |
| 2 | `npm run typecheck` | 0 errors |
| 3 | `npm run format:check` | All matched files use Prettier code style |
| 4 | `npx vitest run` | **750 passed, 46 files** (661 on `dev`; 89 new: `rules.test.ts` 20, `suspensionClosure.test.tsx` 19, `closedWorkspace.test.tsx` 48, one each in `concerns.test.tsx` and `schedule.test.tsx`). Changed: the workspace's two tab-order assertions include the two tabs. Two full runs, both green |
| 5 | `npm run build` | Built; the chunk-size warning is the one `dev` has |
| 6 | Criterion 1 in tests | `an ACTIVE project shows Stage 1 Completion ready and Stage 2 Closure waiting, each its own step with its own action` (two list items in order, `aria-current` on Stage 1, one raise link, none in Stage 2); `once Stage 1 is in effect the project is COMPLETED…`; `a SUSPENDED project holds Stage 1 and offers a closure without completion…`; `an approved completion reads Approved … not yet in effect, Stage 1 marked as shown`; SCR-112's exact body; blockers named; waivers; rule tests for every stage path |
| 7 | Criterion 2 in tests | `closedWorkspace.test.tsx`: every one of the 16 tabs of a CLOSED project, holding tasks, milestones, risks, issues with an open escalation, a change request, a submitted progress update, a submitted KPI value, a financial update and an obligation, as the Project Manager and as the department's manager — **no enabled write control** in the panel, the header, or the first record's detail. **Control:** the same data on an ACTIVE project offers writes in 11 tabs to the Project Manager and 3 to the department's manager (14 cases), so the sweep is shown to detect them. The banner (and axe); the closeout tab with both stages done |
| 8 | Live, phase `main` (`artifacts/task-064/live`, git-ignored: `reset.sh` — a fresh `task064_live` migrated and seeded, TASK-063's fixture extended with SUSPENSION and RESUMPTION routes, local.r02's view grants, local.r04's PROJECT_VIEW re-scoped ENTITY → OWN and a PROJECT_VIEW at DEPT for local.r03 so the workspace opens for them (F-1, F-2); `AHDA-api-064` on 5094 with both activation passes every 10 s; `check.mjs` — a second Vite on 5180, Chromium) | **30 of 30**, every step through the screens. **WF-09**: local.r04 requests a suspension on SCR-109 (effective today); local.r03 starts the review; local.r02 approves through MOD-040; SCR-110 reads Approved / *Approved, not yet in effect*, then In effect once the pass activates it — the project SUSPENDED, the banner shown; the resumption requested from SCR-110, reviewed, approved and activated — ACTIVE. **Check 1**: the Closeout tab shows two ordered stages, Stage 1 *Ready to start* with the only action (*Raise completion request*), Stage 2 *Waits for Stage 1* with no link or button. SCR-112 raises Stage 1; an obligation is recorded with "Me" as owner; *Evaluate readiness*: Not ready, Progress reported failing; Submit is refused "These criteria fail without a waiver: Progress reported…"; local.r03 waives it (Ready with conditions); submitted, reviewed, approved — **Approved, not yet in effect, the project still ACTIVE**; then In effect, the project COMPLETED; the tab now shows Stage 1 *Done* and Stage 2 current with *Raise closure request*. The obligation satisfied, Stage 2 raised, evaluated Ready, submitted, reviewed, approved and activated: CLOSED. **Check 2**: as local.r04, local.r03 and local.r02, the banner reads "Closed: read-only…" and **no enabled write control on any of the 16 tabs** (panel, header, first record's detail); a closed case's detail offers no command. Documents answers local.r02 and local.r03 403 (no document grant in the fixture), shown as an error with a retry and no write |
| 9 | Live, phase `a11y` | **32 of 32**: axe with colour contrast and no sideways scroll at 1366 and 390 px, English and Arabic, on both tabs, the three details, both cross-project registers and the closed overview |

The first live runs found harness timing only (a badge read before the record was read again); the screenshots then showed the closed project's empty Tasks tab saying "Tasks are planned once the project is approved", fixed (D-7).

### 4.1 Mutation tests

Each mutation was applied, the whole suite run in `AHDA-frontend`, and the source restored (`artifacts/task-064/mutations/run.py`). 14 mutations, 14 killed. M-13 and M-14 survived the first run — the sweep opens no issue detail and its schedule has no submitted baseline — and are killed by the two tests added for them.

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | Stage 2 opens while the project is still ACTIVE | 5, among them the criterion 1 test |
| M-2 | A case is offered whatever its stage's state | 5 |
| M-3 | An approved record reads as in effect | 3 (rule; SCR-110; SCR-113) |
| M-4 | `isClosed` never true | 12, among them the sweep |
| M-5 | The Documents tab offers Upload on a closed project | 2 (sweep, both people) |
| M-6 | Progress review ignores CLOSED | 3 |
| M-7 | Financial review and KPI publication ignore CLOSED | 3 |
| M-8 | WF-09/10 commands ignore CLOSED | 1 |
| M-9 | The CLOSED banner is not shown | 1 |
| M-10 | A second suspension offered while one is open | 2 |
| M-11 | Obligation commands offered on a closed project | 1 |
| M-12 | A failed criterion that may not be waived offers a waiver | 2 |
| M-13 | The issue detail's escalation actions ignore CLOSED | `a CLOSED project's open escalation is history…` |
| M-14 | The baseline decision ignores CLOSED | `no baseline decision is offered on a CLOSED project…` |

## 5. Acceptance criteria, deliverables and checks

| Item | Result |
| --- | --- |
| The Closure UI visually distinguishes the Completion stage from the Closure stage as two sequential steps, not one combined action | **MET.** D-2, D-3; §4 rows 6, 8; M-1 to M-3 |
| A Closed Project's workspace renders in a clearly read-only mode with edit controls disabled or hidden | **MET.** D-7; §4 rows 7, 8; M-4 to M-9, M-11, M-13, M-14 |
| Validation: walk a Project through Completion then Closure in the UI and confirm the two stages are presented distinctly | **MET** in Chromium against the real API (§4 row 8) |
| Validation: open a Closed Project and confirm no edit control is present or enabled | **MET** in Chromium against the real API, three people, 16 tabs (§4 row 8) |
| Description: SCR-108, SCR-109, SCR-110 and the reused Closure screen family SCR-111–113 for the two-stage closeout | **MET** (§3) |
| Deliverables: React components/routes for SCR-108–113 | **MET** (header row "Deliverables") |

## 6. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **On the shipped grants a Project Manager cannot read their own project** (R04 PROJECT_VIEW at ENTITY; `change-request-ui.md` F-1). They raise suspensions and closeout cases through the API (…_RAISE at OWN), so SCR-109/110/112/113 work for them by address (D-1), but the Suspension and Closeout tabs never show them their project. The live fixture re-scoped the grant to OWN to walk the workspace | PMO (Blueprint Appendix A); TASK-110 | A Project Manager reaches the closeout only through a link |
| F-2 | **The department's manager cannot list projects** (R03 ships no PROJECT_VIEW, TASK-058 F-1) and nothing notifies them that a request awaits review (TASK-062 F-6) | PMO; TASK-039 configuration | Reviews start from an address or WF-11's link |
| F-3 | **USER_VIEW ships to R01 only**: an obligation's owner is chosen by name from the person, the Project Manager and existing owners, or else given by user id (D-5) | PMO (Appendix A) | Anyone else is named by id |
| F-4 | **The specifications' wider screens have no API**: SCR-110's impact, readiness and review tabs; SCR-113's reconciliation, deliverables, open items, lessons and documents tabs; capability projections (TASK-062 F-1, TASK-063 F-1). Each readiness criterion is a count linked to its register (TASK-063 F-7) | PMO / BA | A completion is reviewed on counts and narrative |
| F-5 | **No route exists** for SUSPENSION, RESUMPTION, COMPLETION or CLOSURE (TASK-062 F-3, TASK-063 F-3): *Start review* answers CONFIGURATION_MISSING, which the screens word as "No approval route is configured for this request yet" | AHDA | Nothing is reviewed until AHDA publishes them |
| F-6 | ***Activate now* is offered to AHDA's internal reviewers**, but …_ACTIVATE ships to no role (the worker activates): on the shipped grants the API refuses it, shown in the dialog. It is kept for an AHDA operator granted it to retry a stuck activation (TASK-062 F-7) | PMO (Appendix A) | A reviewer may see a refusal where they expected an action |
| F-7 | **While a detail's project read is in flight it counts as unreadable**, so an internal person not reaching the project may see AHDA's commands for a moment before they go; the API refuses them either way (`change-request-ui.md` behaves the same) | Frontend | A brief flicker of commands |
| F-8 | **Outside the workspace**, the change-request detail and the escalation queue gate on the record's state, not the project's. A CLOSED project has no open change request or decision left (CHANGES_DISPOSITIONED, DECISIONS_SETTLED) and the API refuses any write 409 PROJECT_CLOSED; these screens were left unchanged | Frontend | A stale screen could offer a command the API refuses |

## 7. Change log

| Date | Change |
| --- | --- |
| 2026-10-08 | Created (TASK-064) |

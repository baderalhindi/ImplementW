# Task Boards & My Tasks UI (WF-04 Frontend)

| Field | Value |
| --- | --- |
| Task | TASK-049 — Build Task Boards & My Tasks UI (WF-04 Frontend) (P8 - Execution & Performance) |
| Depends on | TASK-048 — the WF-04 API (`project-task.md` §3–§5): every screen reads and writes through it, every percentage shown is its roll-up, every state change is one of its commands, and every refusal shown is one it returns. TASK-047 — the schedule activities a task names, and the helpers this task moves to `shared` (D-11). TASK-042 — the project workspace (SCR-040) the Tasks tab sits in. TASK-032, TASK-036 — the SPA foundation |
| Record date | 2026-10-03 |
| Status | **BUILT AND VERIFIED LOCALLY**: component tests against a mocked API (§4 row 4), 14 mutation tests (§4.1), the request shapes and refusals against the compose API (§4 row 8), and a browser check in Chromium at 1366 px and 390 px, in English and Arabic, with the real sign-in and the real API answering every task read and command (§4 row 9). **In a real environment** a Project Manager who holds `PROJECT_VIEW` over their project plans and works its tasks; but an internal Project Manager cannot read their own project (TASK-041 F-1, F-5 here), a task owner who is not the Project Manager is refused every command (TASK-048 F-2, F-6), and an owner not yet on the project can be named only by user ID (F-3) |
| Branch | `feat/task-049-wf04-task-boards-frontend` |
| Deliverables | React components and routes for SCR-047, SCR-063, SCR-064, SCR-065, SCR-066 and MOD-010 to MOD-015 in `src/frontend/src/features/tasks` (`ProjectTasks.tsx`, `TaskListPage.tsx`, `routes.tsx`, `dialogs/`, `components/`, `api/`, `access.ts`, `assignee.ts`, `taskRules.ts`, `presentation.ts`, `problems.ts`, `useTaskData.ts`); i18n `features/tasks/i18n/{ar,en}.json`. The workspace's Tasks tab, the `/tasks` routes and their navigation (D-1). 51 frontend tests. This record |
| Environment variables / secrets | None |
| Workbook read | The TASK-049 row as given in the task request, 2026-10-03 (description, acceptance criteria, directory, deliverables), and the workbook's own row (branch; validation check: "Run an axe-core accessibility scan on the Task Detail modal; attempt to assign a task to an unrelated user via the UI and confirm the server-side rejection is surfaced clearly"). `project-task.md`; `schedule-ui.md`; `progress-ui.md`; `project-registration-ui.md`; `approvals-ui.md` F-2. Blueprint Appendix B (the screen inventory) is not in the repository (F-1) |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | SCR-047 Project Tasks (the workspace's Tasks tab); SCR-063 My Tasks, SCR-064 Team Tasks, SCR-065 Overdue Tasks, SCR-066 Updates Required | This task |
| **In** | MOD-010 Create Task, MOD-011 Edit Task, MOD-012 Task Detail, MOD-013 Add Subtask, MOD-014 Assign Task, MOD-015 Add Dependency (between tasks) | This task (numbering F-2) |
| **In** | Every WF-04 command from MOD-012: start, block (with a reason), unblock, complete, report progress, reopen, cancel; removing a dependency | This task |
| **In** | Overdue tasks flagged without colour alone; an empty state per list; loading, error and refused states; Arabic and English | This task |
| **Out** | A query of a person's tasks across projects, and a lookup of the people who may own a project's tasks | Engineering Architect, with TASK-048 (F-3, F-4) |
| **Out** | Notifications of assignment, blocking and overdue tasks | TASK-039 configuration (TASK-048 F-14) |
| **Out** | Task execution on the Gantt (actual dates) | TASK-048 F-3 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Where it lives.** The screens are in `features/tasks`. SCR-047 is the workspace's Tasks tab, after Schedule, audience `reached` (`TASK_VIEW` ships to the Project Manager, and an owner must hold a role over the project), lent the workspace's context by `projects/workspace/TasksTab.tsx`; route `/projects/:projectId/tasks` behind `WorkspaceTabGuard tab="tasks"`. SCR-063–066 are `/tasks`, `/tasks/team`, `/tasks/overdue` and `/tasks/updates`, behind `RequireSession`, with a "Tasks" group in the sidebar and a home-page link | ADR-002 module → `features/tasks`; `schedule-ui.md` D-1 |
| D-2 | **The cross-project lists read every visible project's tasks.** The API lists tasks one project at a time (R-3; TASK-048 F-8), so `useTaskPortfolio` reads `GET /projects?status=APPROVED_PLANNED,ACTIVE,SUSPENDED,COMPLETED,CLOSED` (the states a task can exist in, TASK-048 D-9) page after page, then each project's tasks, six projects at a time, every page of 200 (R-29). The four lists are filters of that one read (`taskRules.ts` `listEntries`). A 403 on the tasks (no `TASK_VIEW`) is "You do not have access to tasks", not an error | F-4 |
| D-3 | **The four lists** (no WF-04 specification defines them, F-7). SCR-063 My Tasks: the tasks the person owns. SCR-064 Team Tasks: every task of the projects the person manages, whoever owns it. SCR-065 Overdue: among both, an open task (NOT_STARTED, IN_PROGRESS, BLOCKED) whose planned finish is before today (UTC, TASK-048 F-9); a task due today is not overdue. SCR-066 Updates Required: among both, a NOT_STARTED task past its planned start, or a started leaf (IN_PROGRESS or BLOCKED, no live subtask) with no figure entered, or whose figure has not changed for 7 days (`STALE_UPDATE_DAYS`, read from `updatedAt`); a parent's figure is rolled up, never entered (TASK-048 D-7). Each row of SCR-066 says why. Every list is most overdue first, then by planned finish. SCR-063, SCR-064 and SCR-047 show open tasks by default, behind a filter offering each state and all | Task description |
| D-4 | **An overdue task is told apart without colour** (acceptance criterion 1, WCAG 1.4.1). Under its title: a bordered badge with a warning icon (an SVG triangle, `aria-hidden`) and the words "Overdue by 3 days"; its row has a 4 px edge on the reading side (mirrored in Arabic). The warning colours repeat what the icon, the words and the edge say; a screen reader hears the words with the title. The same flag is in MOD-012 | Acceptance criterion 1 |
| D-5 | **Each list has its own empty state** (§3.1), saying what the list holds and how a task gets there; a filter that matches nothing says so and how to widen it; SCR-064 tells a person who manages no project that it is about the projects they manage | Task description |
| D-6 | **One task modal at a time** (`dialogs/TaskDialogs.tsx`, `taskDialog.ts`). A title opens MOD-012; from it MOD-011, MOD-013, MOD-014 and MOD-015 open in its place and return to it when they close, saved or not, with the screen's notice; a new task's MOD-012 opens once it is created. MOD-012 reads the task on its own for its ETag; every command and the PUT send it (R-21), and a 412, `TASK_NOT_EDITABLE` or 404 reads the task again with "Someone changed this task meanwhile". The board behind stays on screen while it is read again (`useApiResource` `keepWhileReloading`, D-11), so the open modal is never unmounted. Opening another task's MOD-012 from one (a subtask, the parent) replaces it: Close returns to the list (F-12) | R-21; `schedule-ui.md` D-8 |
| D-7 | **MOD-012 offers exactly the state machine's edges** (`access.ts` `taskCommands`, TASK-048 §3): Start (NOT_STARTED), Complete (IN_PROGRESS), Unblock (BLOCKED), Report progress (a leaf IN_PROGRESS or BLOCKED), Block (NOT_STARTED or IN_PROGRESS), Reopen (COMPLETED, under a parent that is not, to the holder of `TASK_REOPEN`), Cancel (an open task with no live subtask and no dependency, to the planner). **A blocked task is never offered Complete**, and the dialog says it is unblocked first. Unmet dependencies are shown ("Waiting for: Lay drainage (finish to start).") but not pre-empted: the API decides, and its 422 `TASK_DEPENDENCY_UNMET` is shown. Block, Report progress and Cancel open a form or confirmation in place (`aria-expanded`); the confirmation's button is "Yes, cancel the task", never a second "Cancel task" | TASK-048 D-4–D-7 |
| D-8 | **Keyboard and screen reader on MOD-012** (acceptance criterion 3). The modal is the platform's native `<dialog>` (`showModal`: focus held inside, the page behind inert, Escape closes). Every action is a native `<button>` or form control in reading order (status, details, work, subtasks, dependencies, Edit, Assign, Close), none taken out of the tab order; each section has a heading; a removal names its task ("Remove the dependency on Survey the plot"); a form opened with Enter is the next Tab stop | Acceptance criterion 3 |
| D-9 | **MOD-014 leaves eligibility to the API and shows its refusal** (acceptance criterion 2). The task's plan is re-sent as a whole with the new owner and the ETag. Offered: the person, the Project Manager, everyone who owns a task of the project, no owner, and someone else — found with the user search where `USER_VIEW` allows, otherwise given by user ID (checked to be one) (F-3). Whether that person holds a role over the project is the API's decision (TASK-048 D-12): its 422 `TASK_ASSIGNEE_NOT_ELIGIBLE` is shown in the form's alert ("The server did not accept this owner: they hold no role on this project. Nothing was changed.") and on the owner field ("This person holds no role on this project, so they cannot own its tasks…"), linked by `aria-describedby`, with focus moved to the chosen option; the dialog stays open with the choice. MOD-010 and MOD-013 name the owner the same way | Acceptance criterion 2; workbook validation check |
| D-10 | **What is checked before sending mirrors the API.** MOD-010/011/013: a title (2000 characters at most), both dates, the finish not before the start (`DATE_BEFORE_START`); a subtask shows its parent's schedule activity (TASK-048 D-5) and starts with the parent's dates. A percentage: 0–100 to four places. MOD-015: both ends among the live leaf tasks, two different tasks, a pair not already linked, no cycle (the chain is named, as in TASK-047 D-6, through the shared `cyclePath`), and not already broken (the successor has taken the step it would gate). The API stays the authority; its field codes land on the field they name (`problems.ts` `taskFieldMessage`) | `project-task.md` §5 |
| D-11 | **Additions to the foundation**, all backward compatible. `shared/api/paging.ts` `readAllPages` and `MAX_PAGE_SIZE` (schedule's private `allItems`, now shared); `shared/graph/dependencyGraph.ts` `cyclePath` (schedule's, now shared, as the backend's `DependencyGraph` moved to `Common` in TASK-048); `shared/forms/useFieldErrors.ts`, taking the feature's field wording (schedule's `useFieldErrors.ts` wraps it); `useApiResource`'s `keepWhileReloading` option (D-6) — the first version read a loader's `null` result as no result, which the progress suite caught (M-14); `ConfirmDialog` takes a feature's problem wording; `projectsApi.listAll`; `tasks` in `WorkspaceTabKey` and `WORKSPACE_TABS`; the `tasks` i18n namespace and `common.home.tasks`; `.table-container { position: relative }`, so visually hidden text in a table scrolled sideways (a date range's "to") is clipped with it instead of widening the page on a narrow screen — found by the 390 px check, and latent in TASK-047's tables; `src/test/taskFixtures.ts`. The three tab assertions in `projects` include Tasks | — |
| D-12 | **Access is navigation, not protection** (`access.ts`). Planning (MOD-010, 011, 013, 014, 015, cancel, removing a dependency): the project's own Project Manager while the project is APPROVED_PLANNED or ACTIVE (`TASK_MANAGE` at OWN). Execution: the Project Manager or the task's owner while it is ACTIVE (`TASK_UPDATE`). Reopen: the Project Manager while it is ACTIVE (`TASK_REOPEN`). The API decides every command again | TASK-048 D-9, D-11 |

## 3. Screens and routes

| Screen | Route | Component |
| --- | --- | --- |
| SCR-047 Project Tasks | `/projects/:projectId/tasks` (workspace tab) | `ProjectTasks` (via `TasksTab`) |
| SCR-063 My Tasks | `/tasks` | `TaskListPage list="mine"` |
| SCR-064 Team Tasks | `/tasks/team` | `TaskListPage list="team"` |
| SCR-065 Overdue Tasks | `/tasks/overdue` | `TaskListPage list="overdue"` |
| SCR-066 Updates Required | `/tasks/updates` | `TaskListPage list="updates"` |
| MOD-010 Create Task | SCR-047 "Add task" | `TaskFormDialog` (`create`) |
| MOD-011 Edit Task | MOD-012 "Edit" | `TaskFormDialog` (`edit`) |
| MOD-012 Task Detail | a task's title, on any of the five screens | `TaskDetailDialog` |
| MOD-013 Add Subtask | MOD-012 "Add subtask" | `TaskFormDialog` (`subtask`) |
| MOD-014 Assign Task | MOD-012 "Assign" | `AssignTaskDialog` |
| MOD-015 Add Dependency | SCR-047 or MOD-012 "Add dependency" | `TaskDependencyDialog` |

### 3.1 Empty states

| Screen | When | Title | Body |
| --- | --- | --- | --- |
| SCR-047 | No task, to the Project Manager | This project has no tasks yet | Add the first task to plan the work against the schedule. (with "Add task") |
| SCR-047 | No task, to anyone else, project APPROVED_PLANNED or ACTIVE | This project has no tasks yet | The Project Manager has not planned any task yet. |
| SCR-047 | No task, project in any other state | This project has no tasks yet | Tasks are planned once the project is approved. |
| SCR-063 | The person owns no task | No tasks are assigned to you | When a Project Manager makes you the owner of a task, it appears here, whichever project it belongs to. |
| SCR-064 | The person manages no project | You do not manage a project | Team tasks lists every task of the projects you manage. Your own tasks are under My tasks. |
| SCR-064 | Their projects have no task | The projects you manage have no tasks yet | Plan tasks in a project's Tasks tab; every task of the projects you manage appears here. |
| SCR-065 | Nothing is overdue | Nothing is overdue | A task is overdue when its planned finish has passed and it is neither completed nor cancelled. Your tasks and those of the projects you manage are checked. |
| SCR-066 | Nothing is due | No task needs an update | A task needs an update when its planned start has passed and it has not started, or when it has started and has no progress figure, or its figure has not changed for 7 days. |
| SCR-047, 063, 064 | The filter matches nothing | No task matches this choice | Open tasks are those not started, in progress or blocked. Choose "All tasks" to see completed and cancelled tasks too. |
| All five | No `TASK_VIEW` (403) | You do not have access to tasks. | — |

## 4. Verification

Run 2026-10-03 on macOS with Docker Desktop: frontend in `AHDA-frontend` (Node 24), against the compose stack already running `dev` (be80b16, TASK-048's routes present: §4 row 8).

| # | Command | Result |
| --- | --- | --- |
| 1 | `npm run lint` | 0 problems |
| 2 | `npm run format:check` | All matched files use Prettier code style |
| 3 | `npm run typecheck` | No errors |
| 4 | `npm test` | 33 files, 434 tests passed (31 files and 383 before: TASK-048 changed no frontend file; 51 new in `features/tasks`: 19 in `rules.test.ts`, 32 in `tasks.test.tsx`). The three tab assertions in `projects` updated for the new tab; the 78 schedule and 58 progress tests pass unchanged over the shared helpers (D-11) |
| 5 | `npm run build` | Built. 1,125 KB JS (288 KB gzip); the >500 KB chunk warning was already on `dev` (TASK-038 F-5) |
| 6 | `npm audit` | 0 vulnerabilities. No dependency added |
| 7 | — | No backend file changed; the `dotnet` gates do not apply |
| 8 | curl through the SPA's proxy (`localhost:5173/api/v1`) as `local.r04`, against a fixture in the local database (git-ignored `artifacts/task-049-live/setup.sql`: an ACTIVE project in DEPT-LOCAL with no delivering entity, managed by local.r04; removed after by `cleanup.sql`) | The bodies MOD-010 and MOD-013 build: three tasks and a subtask 201; `start` 200 ×3; MOD-015's body 201; `block` with a reason 200; `report-progress` 20 → 200, and the parent's `percentComplete` 20.0 on the list (200, each parent then its subtask). The owner change MOD-014 sends, naming **local.r08** (external, ENT-LOCAL, no role over this project), with If-Match: **422 `TASK_ASSIGNEE_NOT_ELIGIBLE`, `assigneeUserId NOT_ALLOWED`**, the owner unchanged. `complete` on the BLOCKED task 409 `INVALID_TRANSITION`; `start` with an unmet FS predecessor 422 `TASK_DEPENDENCY_UNMET`; 120 % 400 `actualPercentComplete OUT_OF_RANGE`; the closing link 422 `TASK_DEPENDENCY_CIRCULAR`; tasks without `projectId` 400 `projectId REQUIRED`; local.r08 listing the project's tasks 403. For local.r04: `GET /users` 403 and `GET /master-data-catalogues` 403 (D-9, F-3, F-9), and **`GET /projects` 0 projects, `GET /projects/{id}` 404** for the project it manages — the finding behind F-5 |
| 9 | Browser check (harness in the git-ignored `artifacts/task-049-browser`, not committed; F-10): Chromium 154 headless with puppeteer-core 24 inside `AHDA-frontend`, against the SPA (Vite) and the compose API. Sign-in is real (`local.r04`); every task read and command goes to the real API over row 8's fixture rows. Only the two project reads are served from the browser, because local.r04 cannot read the project it manages (F-5) | **21 of 21 checks passed.** At 1366 × 768, English: SCR-047 lists the real tasks, each parent before its subtask; the overdue row carries "Overdue by 3 days", a drawn 16 × 16 icon with `aria-hidden`, a solid-bordered badge and an inset 4 px edge, and no other row is flagged. **axe on MOD-012 in Chromium, colour contrast included: no violation** (and in Arabic). **Tab from the dialog reached all 7 controls of the BLOCKED task's MOD-012 in order without leaving it** (Unblock → Report progress → Remove → Add dependency → Edit → Assign → Close); Enter on Report progress, then Tab, reached its field; Escape closed the dialog. **Assigning "Survey the plot" to local.r08 by user ID in MOD-014: the real API answered 422 `TASK_ASSIGNEE_NOT_ELIGIBLE` on `assigneeUserId`; the form alert and the owner field said why, the field had focus** (screenshot `03-assign-refused-1366-en.png`); local.r03 (DEPT-LOCAL) was then accepted (200) and MOD-012 shown again. `/tasks`, `/tasks/team`, `/tasks/overdue`, `/tasks/updates`: shown, no horizontal page scroll or scroll trap. At 390 px: no horizontal page scroll on SCR-065 and SCR-047 (tables scroll inside their named region). In Arabic: right to left, the edge on the right, the icon kept. Screenshots were reviewed; four things were fixed and the check run again: the page scrolled sideways at 390 px (D-11's `.table-container`, and the Tasks tab's grid column), the flag ran into the title, the filter's label was cut off, and the dependency lines used the long type sentence |

The tests, by acceptance criterion:

| Item | Tests |
| --- | --- |
| 1. Overdue tasks are visually distinct without relying on colour alone | `tasks.test.tsx`: the overdue row's flag has the words, a visible drawn icon with `aria-hidden`, the row the `row--overdue` edge, the words in the row's text; a task not overdue has none; SCR-065 flags each row with its own words; the flag in Arabic keeps its icon. `rules.test.ts`: overdue by whole days; due today, completed late or cancelled late is not. Browser row 9. M-1 to M-4 |
| 2. Assigning a task to a user without the required project relationship is rejected server-side and the UI surfaces the error | `tasks.test.tsx`: by user ID (no `USER_VIEW`) and through the user search (`USER_VIEW`), the API's 422 is shown in the alert and on the owner field, the chosen option invalid and focused, the dialog left open; the PUT carries the whole plan, the new owner and If-Match; a person of the project is assigned and MOD-012 shown again. Live: row 8 (the API's 422), row 9 (the UI over the real API). M-5, M-6 |
| 3. Keyboard navigation reaches all interactive elements on the Task Detail modal | `tasks.test.tsx`: every interactive element of MOD-012 is listed in reading order and Tab visits each in turn; a form opened with Enter joins the order; axe finds nothing. Browser row 9: Tab in Chromium's native modal, Escape. M-7 |
| Workbook validation check | axe on MOD-012 in Chromium (colour contrast included) and in jsdom: no violation; assignment to an unrelated user through the UI refused by the real API and shown (row 9) |
| SCR-047, SCR-063–066 and the empty states | Each list's rows and order; the project named on the cross-project lists; completed tasks behind the filter; each of the four lists' empty state; SCR-064 for someone managing no project; SCR-047's empty state for the Project Manager and for anyone else; the filtered empty state; 403 on SCR-047 and the lists; an owner on another manager's project works the task from the list (no Edit, Assign or Cancel) and the list is read again. `rules.test.ts`: the four lists' definitions and order. M-11, M-12 |
| MOD-010, 011, 013, 015 and the commands | A finish before the start refused on the field, nothing sent, then the exact body; MOD-011's PUT with If-Match and the owner kept; MOD-013 on the parent's activity and dates; MOD-015's cycle refused with the chain and focus, no POST, then a valid SS link; a blocked task offered Unblock, not Complete, the unblock sent with If-Match; a start's 422 explained; 120 % refused before sending, 62.5 sent; a block needs a reason, sent in the typed language; a completed subtask reopened from its parent's MOD-012; a free task cancelled after confirming. `rules.test.ts`: the commands each state and right allow, S/F met, unmet dependencies, MOD-015's checks in order, the form checks, the owner given by ID. M-8 to M-10, M-13 |

### 4.1 Mutation tests

Each mutation was applied to the source, `vitest run src/features/tasks` (M-14: `src/features/progress`) run in `AHDA-frontend`, and the source restored from a copy (script and output in the git-ignored `artifacts/task-049-mutations/`). The first run let M-2 survive: the criterion-1 test checked that an icon element existed, not that it was visible and drawn. The test now checks both; M-2 fails it.

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | The overdue flag shows the icon only, without the words | 3: criterion 1, SCR-065, Arabic |
| M-2 | The overdue flag's icon is hidden | 1 (after the strengthened check): criterion 1 |
| M-3 | A task due today counts as overdue | 1 |
| M-4 | A completed or cancelled task can be overdue | 4 |
| M-5 | MOD-014 does not put the API's refusal on the owner field | 2: criterion 2 |
| M-6 | `NOT_ALLOWED` on the owner reads as the platform's generic message | 2: criterion 2 |
| M-7 | MOD-012's command buttons are taken out of the tab order | 1: criterion 3 |
| M-8 | A blocked task is offered completion | 3 |
| M-9 | Reopen is offered on the planning right instead of the reopen right | 1 |
| M-10 | MOD-015 ignores a cycle | 1 |
| M-11 | Team Tasks lists the person's own tasks instead of their projects' | 3 |
| M-12 | A figure unchanged for exactly a week is not stale | 1 |
| M-13 | Commands are sent without If-Match | 1 |
| M-14 | `useApiResource` reads a loader's `null` as no result (the regression found while building D-6) | 11 (progress) |

## 5. Acceptance criteria and deliverables

| Item | Result |
| --- | --- |
| Overdue tasks are visually distinct without relying on color alone (icon or text label, for accessibility) | **MET.** Icon and text label, and a bordered badge and a row edge. D-4; §4 rows 4, 9; M-1 to M-4 |
| Assigning a task to a user without the required project relationship is rejected server-side and the UI surfaces the resulting error | **MET.** D-9; §4 rows 4, 8, 9 (the real API's 422, shown in the alert and on the field); M-5, M-6. An owner not yet on the project is named by user ID where the person cannot search users (F-3) |
| Keyboard navigation reaches all interactive elements on the Task Detail modal | **MET.** D-8; §4 rows 4, 9 (Chromium's native modal); M-7 |
| Description: SCR-063, 064, 065, 066, SCR-047 and MOD-010 to MOD-015, overdue items visually flagged, a documented empty state per list | **MET.** §3, §3.1 (the documented empty states); D-3 defines the lists without a WF-04 specification (F-7) |
| Deliverables: React components/routes for SCR-047/063-066 and MOD-010-015 | **MET.** §3 |
| Workbook validation check | **MET.** axe on MOD-012: no violation, in Chromium with colour contrast and in jsdom; an unrelated user's assignment refused by the real API and surfaced in the UI (§4 row 9, `03-assign-refused-1366-en.png`) |

## 6. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **No screen inventory.** Blueprint Appendix B is not in the repository, so SCR-047, SCR-063–066 and MOD-010–015 have no field or layout specification. These screens follow the API contract (`project-task.md`) and the earlier UI records | PMO | The layout may differ from the Blueprint's |
| F-2 | **MOD-015 is named twice.** TASK-047 built MOD-015 as the schedule's Add Dependency; this row's "Create/Edit/Detail/Subtask/Assign/Dependency modals (MOD-010-015)" names MOD-015 again for tasks' dependencies. MOD-010 to MOD-015 are mapped in the row's order (§3); the two dependency dialogs share their rules (`cyclePath`) but not their fields (lag, the SF type) | PMO | Numbering may change when Appendix B is available |
| F-3 | **A Project Manager cannot search for an owner.** The user search needs `USER_VIEW`, which only R01 holds (TASK-032); R04 gets 403 (§4 row 8). MOD-014 offers by name the people already on the project's tasks, and anyone else by user ID; names read "User 1a2b3c4d" without `USER_VIEW`. As `approvals-ui.md` F-2 for delegates | Engineering Architect / Identity (a lookup of the people holding a role over a project, any Project Manager may call); PMO (Appendix A) | New owners are named by an ID the Project Manager must obtain elsewhere |
| F-4 | **No cross-project task query** (TASK-048 F-8). SCR-063–066 read every visible project, then each one's tasks: 1 + N requests per visit, six at a time. `GET /project-tasks?assigneeUserId=` across projects (index I-33), filtered per project by the engine, would make SCR-063 one request | Engineering Architect, with TASK-048 | Slower lists for people who see many projects |
| F-5 | **The lists find tasks through the projects a person may see.** `GET /projects` needs `PROJECT_VIEW`, which R04 and R08 hold at ENTITY only (TASK-041 F-1): an internal Project Manager with no entity anchor sees no project — local.r04 got 0 projects and 404 on the project it manages (§4 row 8) — so their workspace, Tasks tab and lists are empty although the task API answers them. A task owner without `PROJECT_VIEW` over the project likewise sees none of their tasks on SCR-063 | PMO (Appendix A); TASK-110; with F-4 | Internal Project Managers cannot reach their tasks in a real environment |
| F-6 | **Owners are offered execution the API refuses.** Only R04's task grants ship (TASK-048 F-2): an owner who is not the Project Manager is offered Start, Block, Complete and Report progress (D-12) and gets 403 until Appendix A gives the owners' role `TASK_VIEW` and `TASK_UPDATE` at ASSIGNED | PMO (Appendix A); TASK-110 | Owners see their tasks' commands refused |
| F-7 | **The lists are defined here** (D-3): no WF-04 specification says what "Team", "Overdue" or "Updates Required" mean (TASK-048 F-5). "Team" is read as the projects the person manages (the API has no team); an update is due after 7 days without a change, read from `updatedAt`, which any write moves, not only a progress report; the 7 days is a constant, not configuration | PMO (WF-04 specification) | A specification may change a list's contents |
| F-8 | **No notifications** of assignment, blocking or overdue tasks (TASK-048 F-14): SCR-065 and SCR-066 are the only signal | TASK-039 configuration | Owners learn of work only by opening the lists |
| F-9 | **Priorities need `MASTER_DATA_VIEW`** (R01 only, TASK-038 F-1): R04 cannot choose one; the form says so and keeps the one set; a priority reads "Item 1a2b3c4d" | PMO (Appendix A) | Tasks are planned without priorities |
| F-10 | **No browser end-to-end run on real project reads** (§4 row 9): local.r04 cannot read its project (F-5), so the two project reads were served from the browser; every task read and command was real. No e2e suite is committed (TASK-085) | TASK-085 | The project reads are verified in jsdom and by request shape only |
| F-11 | **Arabic strings are the delivery team's**, not reviewed by AHDA | AHDA | Wording may change |
| F-12 | **Moving between tasks keeps no history.** A subtask's or parent's MOD-012 opened from another replaces it; Close returns to the list | PMO | One more click to go back to the first task |
| F-13 | **"Today" is the UTC date**, as the API dates tasks (TASK-048 F-9): from midnight to 03:00 in Riyadh a task due the day before is not yet overdue | Engineering Architect; AHDA PMO | Overdue flags appear up to three hours late |

## 7. Change log

| Date | Change |
| --- | --- |
| 2026-10-03 | Created (TASK-049) |

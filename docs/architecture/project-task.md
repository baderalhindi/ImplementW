# Task Management (WF-04 Backend)

| Field | Value |
| --- | --- |
| Task | TASK-048 — Build Task Management (WF-04 Backend) (P8 - Execution & Performance) |
| Depends on | TASK-046 — Build Schedule & Baseline Management (`schedule-baseline.md`): the schedule activity a task executes against, read through `IScheduleActivityReader` (ADR-003 §8.2 edge 9). TASK-041 (`project-registration.md`): the project's anchors and lifecycle state through `IProjectFactsReader` (edge 36, added by this task, F-1). TASK-030 and TASK-033: the authorization engine and the audit trail. TASK-034: the PRIORITY catalogue |
| Record date | 2026-10-03 |
| Status | **BUILT AND VERIFIED LOCALLY** against `AHDA-postgres` and `AHDA-ldap`, in process through the real API pipeline (§7). In a real environment the Project Manager plans, executes, reopens and cancels tasks; a task owner who is not the Project Manager can do nothing until Appendix A grants the owners' role an ASSIGNED scope (F-2); WF-02 cannot yet read the Activity Execution Progress (F-4) |
| Branch | `feat/task-048-wf04-task-management-backend` |
| Directory | `src/backend/PMPlatform.Application/Features/ProjectTask` — the row's `Features/Task`, renamed `ProjectTask` by solution architecture §4.4b and S-2 |
| Deliverables | **WF-04 task/subtask service**: `IProjectTaskService` (`ProjectTaskService`), `ITaskExecutionService` (`TaskExecutionService`), `ITaskDependencyService` (`TaskDependencyService`), three controllers, 15 operations (§4). **State machine**: `ProjectTaskWorkflow` (10 edges, one command per edge family, the permission each is decided on) and `TaskDependencyRules` (the FS/SS/FF/SF blocking rules); migration `TASK-048_GuardProjectTask`, which refuses every other change in the database. **Aggregation logic**: `TaskProgressRollup` (a parent's percentage on read, an activity's from its live leaves, ADR-009's duration weighting) and `ActivityProgressProjection`, which keeps `activity_execution_progress` in step in the writing transaction. **Tests**: 79 unit, 19 integration, 9 mutations (§7), and a check against the running stack (§7 row 9). Supporting: the `project_task` schema (three migrations), `IProjectTaskRepository`, `IScheduleActivityReader` (Schedule contract), four permissions with R04's grants, a CI step |
| Environment variables / secrets | None |
| Gate decision applied | **ADR-009**: a task's actual percentage is maintained by its owner and editable by the Project Manager — D-7; tasks carry their planned duration, the roll-up weight — D-3, D-8 |
| Participation amendment | **ADR-013**: direct task action extends to an assigned entity Project Manager on their own project — D-11 |
| Sources read | The TASK-048 row as supplied on 2026-10-03 (description, acceptance criteria, directory, deliverables, gate decision, participation amendment). No WF-04 functional specification was available to this task (F-5). ERD §5.7, §6 row 15, §7 rows 2 and 11, F-020 to F-024; `erd.dbml` `project_task.*`; `indexing-strategy.md` I-33, I-34; `solution-architecture.md` §4.4b, M-1 to M-13, §8.2 edges 1–9, §9 (the physical progress pair), §10; ADR-009 and ADR-013 (`adrs/ADR-register.md`); `schedule-baseline.md` §6 item 3, F-3, F-12; `progress-update.md` D-2, F-1; `authorization-engine.md` D-3, D-9; `api-conventions.md` R-2 to R-5, R-21, R-27, R-40, R-47 |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | Tasks and one level of subtasks: plan, owner, priority, schedule activity | This task |
| **In** | Execution: NOT_STARTED → IN_PROGRESS ⇄ BLOCKED → COMPLETED, and the controlled reopen and cancel | This task |
| **In** | Dependencies between tasks and the blocking rules they impose | This task |
| **In** | A task's actual percentage (ADR-009), a parent's roll-up, and the Activity Execution Progress aggregate | This task |
| **Out** | Reporting actual dates and IN_PROGRESS/COMPLETED to `schedule.schedule_activity` | Needs a command edge to Schedule (F-3) |
| **Out** | WF-02 reading the Activity Execution Progress | Needs a Progress → ProjectTask edge (`progress-update.md` F-1; F-4) |
| **Out** | A person's tasks across projects (SCR-063 My Tasks, index I-33) | TASK-049 (F-8) |
| **Out** | Notifications on assignment, blocking and overdue tasks | TASK-039 configuration (F-14) |
| **Out** | SCR-047 and SCR-063–066 | TASK-049 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Module shape.** `Features/ProjectTask`: `Contracts` (`IProjectTaskService`, `ITaskExecutionService`, `ITaskDependencyService`, the DTOs, inputs and pages, `ProjectTaskErrorCodes`, `Events`), the three services, `ProjectTaskWorkflow`, `TaskDependencyRules`, `TaskProgressRollup`, `ActivityProgressProjection`, `ProjectTaskGate` (project facts, authorization, the project's lock and the board), `TaskBoard`, `ProjectTaskAccess`, `ProjectTaskReferences`, `ProjectTaskAudit`, `ProjectTaskMapping` and the repository port. Entities in `Domain/ProjectTask` (`ProjectTask`, `TaskDependency`, `ActivityExecutionProgress`); repository and configurations in `Infrastructure/Persistence`. The entity is `ProjectTask`, so the module's files alias it (`ProjectTaskEntity`) where the namespace of the same name is in scope | ADR-003 §4.3 row 4, §4.4b, §6 |
| D-2 | **Two specific edges.** Edge 9 as registered: `IScheduleActivityReader`, new in Schedule's contracts, returns an activity's project, kind, status and planned dates and authorizes no one, as `IScheduleHealthReader` does. Edge 36, added to §8.2 and `ModuleRegistry` with this task: ProjectTask → Project (query) through `IProjectFactsReader`, as edges 1–6 give every other core module. A task is authorized on its project's anchors (M-7 makes the caller supply them) and executed only while its project is ACTIVE; a task without a schedule activity still belongs to a project. The alternative — Schedule re-exporting Project's facts through edge 9 — hides a ProjectTask → Project dependency inside another module's contract, which A-6 exists to make visible. No cycle: Project calls no module back. F-1 asks the Engagement Architect to confirm | ADR-003 §10 A-6: "adding an edge means revising §8.2 first" |
| D-3 | **Tables.** ERD §5.7: `project_task` (task and subtask, `parent_task_id`), `task_dependency`, `activity_execution_progress`, with indexes I-33 and I-34. Beyond the ERD (F-7): CHECK constraints that hold the state machine's facts (BLOCKED exactly while a reason is set; COMPLETED exactly while `completed_at` and `actual_finish_date` are; NOT_STARTED without an actual start), `planned_duration_days = planned_finish_date − planned_start_date + 1`, the percentage 0–100, and `xmin` as the concurrency token of tasks and dependencies. `planned_duration_days` is derived from the dates and stored (ERD §7 row 2), every day a working day as in WF-03 (`schedule-baseline.md` F-3); a client never sends it | ERD §5.7, §7 |
| D-4 | **The state machine** (§3), ten edges in `ProjectTaskWorkflow`, one command per edge family (R-4): `start` NOT_STARTED → IN_PROGRESS; `block` NOT_STARTED or IN_PROGRESS → BLOCKED, with a reason; `unblock` BLOCKED → where it was blocked from, IN_PROGRESS once started, NOT_STARTED otherwise (the actual start tells); `complete` IN_PROGRESS → COMPLETED; `reopen` COMPLETED → IN_PROGRESS; `cancel` any live state but COMPLETED → CANCELLED. **There is no BLOCKED → COMPLETED edge**: a blocked task is 409 `INVALID_TRANSITION` until unblocked. CANCELLED is final (409 `TASK_NOT_EDITABLE`); COMPLETED is left only by a reopen. `start` sets the actual start, which never moves again; `complete` sets the actual finish, `completed_at` and a leaf's 100 %; `reopen` clears the finish and `completed_at` and raises `reopened_count` by one | Acceptance criterion 1; ERD §6 row 15 |
| D-5 | **Subtasks, one level.** A subtask's parent is a live, not COMPLETED, top-level task of the same project that is no dependency end (422 `TASK_HIERARCHY_INVALID`); the parent never changes. A subtask executes against its parent's schedule activity: it names none or the same one (422 `TASK_SCHEDULE_ACTIVITY_INVALID`), and follows its parent when the parent's activity changes. A parent completes only when every live subtask is COMPLETED, and a subtask reopens only under a parent that is not (409 `TASK_SUBTASKS_OPEN`). A task is cancelled only with no live subtask (409 `TASK_IN_USE`). A parent whose subtasks are all cancelled is a leaf again | ERD §5.7 "one level only (rule)" |
| D-6 | **Dependencies and blocking rules** (`TaskDependencyRules`). FS and SS gate the successor's start, FF and SF its completion; the predecessor meets an F by being COMPLETED and an S by having started. A start or completion the rules hold back is 422 `TASK_DEPENDENCY_UNMET`. A dependency joins two distinct live leaf tasks of one project (422 `TASK_DEPENDENCY_INVALID`) — a parent's progress and completion follow its subtasks, so with leaves only and no cycle every task can always be reached — never closes a cycle (422 `TASK_DEPENDENCY_CIRCULAR`, the shared `DependencyGraph`), is unique (409 `TASK_DEPENDENCY_EXISTS`), and is refused if the successor already took the step it would gate (422 `TASK_DEPENDENCY_UNMET`). It is never edited, only removed (HARD_WORKING) and added again. A dependency end is not cancelled (409 `TASK_IN_USE`). Reopening a predecessor does not reopen or block successors that went ahead (F-6). SF is accepted, as the ERD lists it; WF-03 defers it for activities | The row's "dependency and blocking rules" |
| D-7 | **ADR-009: the actual percentage.** `report-progress {actualPercentComplete}`, 0–100 to four places, on a leaf (no live subtask) that is IN_PROGRESS or BLOCKED (422 `TASK_PROGRESS_NOT_ENTERABLE`), decided on `TASK_UPDATE`: the owner through an ASSIGNED grant (the task's owner is its subject's assigned person), the Project Manager through OWN (the project's owner anchor). A parent's percentage is never entered. `complete` sets 100; `reopen` keeps the figure for the owner to correct (F-6) | Gate decision |
| D-8 | **Aggregation** (`TaskProgressRollup`). A leaf counts its entered figure, 0 until one is entered, 100 once COMPLETED; CANCELLED counts nowhere, weight included. A parent's percentage is the planned-duration-weighted mean of its live subtasks, computed on read and returned as `percentComplete`, never stored (ERD §5.7). An activity's Activity Execution Progress is the same mean over the live leaf tasks that execute against it, stored one row per activity (ERD §7 row 11) and rewritten by `ActivityProgressProjection` in the transaction of every task write that moves it, audited when the figure changes; an activity left with no live leaf keeps its row at 0. Four places, rounded half away from zero, as WF-02 rounds | Acceptance criterion 3; ERD §7 row 11; §9 physical progress pair |
| D-9 | **Lifecycle.** Tasks are planned (create, edit, dependencies, cancel) while the project is APPROVED_PLANNED or ACTIVE, as its schedule is, and executed (start, block, unblock, complete, reopen, report progress) only while it is ACTIVE; otherwise 422 `TASK_PROJECT_NOT_ELIGIBLE` | ADR-009 makes an approved baseline the condition of ACTIVE; work is reported against it |
| D-10 | **Concurrency.** The schema has no row per project to lock, so every write takes a transaction-scoped advisory lock on the project (`project_task.lock_project`, keyed on the project id) and reads the whole board under it; the database guards take the same lock before every check that reads other rows, so two writers — say A → B and B → A — cannot each miss the other's change. Edits require `If-Match` (R-21, 428 without, 412 when stale); commands honour it when sent | R-21; `schedule-baseline.md` D-4's lock, for a schema with no parent row |
| D-11 | **Four permissions; only R04's grants ship.** `TASK_VIEW` (Read), `TASK_UPDATE` (execution and the percentage), `TASK_MANAGE` (plan, assign, dependencies, cancel) and `TASK_REOPEN` (reopen only), group `PROJECT_TASK`. Reopen is decided on `TASK_REOPEN` alone, so neither execution nor planning rights reopen a task (403); cancel on `TASK_MANAGE`, so a task's owner does not cancel it. R04 holds all four at OWN: the project's owner anchor is its Project Manager, so OWN reaches exactly the projects the holder manages, internal or entity — ADR-013's amendment: an assigned entity Project Manager acts directly on the tasks of their own project, and an external holder's per-project assignment keeps it to that project. A task's subject adds its owner as the assigned person, so an ASSIGNED grant of `TASK_VIEW` and `TASK_UPDATE` is what lets an owner work their tasks; which roles hold one is Appendix A's (F-2) | Acceptance criterion 2; ADR-009; ADR-013 |
| D-12 | **What a task names** (`ProjectTaskReferences`). Its schedule activity is a live leaf activity of its own project's schedule (422 `TASK_SCHEDULE_ACTIVITY_INVALID`): a summary's progress is WF-02's roll-up. Its owner holds some role over the project's anchors now, as IdentityAccess's `IRoleHolderDirectory` decides it — internal, or external of the delivering entity — the ERD's "must hold a project relationship" (422 `TASK_ASSIGNEE_NOT_ELIGIBLE`; F-10). Its priority is a PUBLISHED item of PRIORITY (422 `TASK_PRIORITY_INVALID`). An edit checks only the references it changes, so an owner who has since lost their role does not block an edit of the dates | ERD §5.7 column notes |
| D-13 | **The database holds the rules** (migration `TASK-048_GuardProjectTask`). A task is born NOT_STARTED with no figure and a zero count; its status moves only along the ten edges; a start waits for FS and SS predecessors, a completion for FF and SF predecessors and live subtasks; a cancelled task changes no more; a task with a live subtask or a dependency is not cancelled; the reopen count rises by one with each reopen and only then; the actual start never moves; the figure changes only while IN_PROGRESS or BLOCKED, or to 100 on completion; project and parent never change; a subtask is one level under a live top-level task of its project that is no dependency end. A dependency joins two live leaf tasks of one project, is never updated and closes no cycle. Tasks and Activity Execution Progress are never deleted or truncated, and a progress row never moves to another activity | Acceptance criterion 1, for any writer |
| D-14 | **API shape (R-2, R-3, R-4).** Collections are the ERD entities' plurals with the project as a filter: `/project-tasks`, `/task-dependencies`, `/activity-execution-progresses`, offset-paged, `projectId` required. Tasks list each parent followed by its subtasks, in creation order. A caller who may not see the project's tasks as a whole sees the ones they own, where their grant reaches those; a project they may not see at all is an empty page; a task they cannot see is 404, one they may see but not change 403 (R-47). `report-progress` is a command that is not a transition, as WF-03's `reforecast` (F-16) | api-conventions |
| D-15 | **Every change is audited** in the saving transaction (event-conventions §4 row 19): `TaskCreated`, `TaskChanged` (title and description withheld), `TaskProgressReported`, `DependencyCreated`, `DependencyDeleted`, `ActivityProgressRecomputed` (DATA_CHANGE); `TaskStarted`, `TaskBlocked` (reason withheld), `TaskUnblocked`, `TaskCompleted`, `TaskReopened` (with the count), `TaskCancelled` (LIFECYCLE_TRANSITION). Refusals are audited by the authorization engine | event-conventions EV-9 |

## 3. The task state machine

| From | To | Command | Permission | Waits for | Writes |
| --- | --- | --- | --- | --- | --- |
| — | NOT_STARTED | `POST /project-tasks` | `TASK_MANAGE` | the project APPROVED_PLANNED or ACTIVE | the plan; the duration from the dates |
| NOT_STARTED | IN_PROGRESS | `start` | `TASK_UPDATE` | FS predecessors COMPLETED, SS predecessors started | the actual start |
| NOT_STARTED, IN_PROGRESS | BLOCKED | `block {reason}` | `TASK_UPDATE` | — | the reason |
| BLOCKED | IN_PROGRESS (started) or NOT_STARTED | `unblock` | `TASK_UPDATE` | — | the reason cleared |
| IN_PROGRESS | COMPLETED | `complete` | `TASK_UPDATE` | FF predecessors COMPLETED, SF predecessors started, live subtasks COMPLETED | the actual finish, `completed_at`, a leaf's 100 % |
| COMPLETED | IN_PROGRESS | `reopen` | `TASK_REOPEN` | a parent not COMPLETED | the finish and `completed_at` cleared; `reopened_count` + 1 |
| NOT_STARTED, IN_PROGRESS, BLOCKED | CANCELLED | `cancel` | `TASK_MANAGE` | no live subtask, no dependency; the project APPROVED_PLANNED or ACTIVE | the reason cleared |

Every command but `cancel` needs the project ACTIVE. No other move exists: BLOCKED → COMPLETED, NOT_STARTED → COMPLETED and anything out of CANCELLED are 409 `INVALID_TRANSITION` or `TASK_NOT_EDITABLE`, and the database refuses them.

## 4. Endpoints

All paths are under `/api/v1`, tag `ProjectTask`, operation ids `ProjectTask_*`. Every non-2xx answer is the R-23 envelope; every `POST` and `PUT` also answers 400 `IDEMPOTENCY_KEY_REQUIRED`/`…_INVALID`. Commands honour `If-Match` when sent (R-21).

| Operation | Method and path | Permission | Success | Refusals beyond 401/403/404 |
| --- | --- | --- | --- | --- |
| `ListProjectTasks` | `GET /project-tasks?projectId=` | `TASK_VIEW` | 200 `ProjectTaskPage`, each parent then its subtasks, with `percentComplete` and `subtaskCount` | 400 |
| `GetProjectTask` | `GET /project-tasks/{id}` | `TASK_VIEW` | 200, `ETag` | — |
| `CreateProjectTask` | `POST /project-tasks {projectId, parentTaskId, scheduleActivityId, title, description, assigneeUserId, priorityItemId, plannedStartDate, plannedFinishDate}` | `TASK_MANAGE` | 201, NOT_STARTED | 400; 422 `TASK_PROJECT_NOT_ELIGIBLE`, `TASK_HIERARCHY_INVALID`, `TASK_SCHEDULE_ACTIVITY_INVALID`, `TASK_ASSIGNEE_NOT_ELIGIBLE`, `TASK_PRIORITY_INVALID` |
| `UpdateProjectTask` | `PUT /project-tasks/{id}`, `If-Match` | `TASK_MANAGE` | 200 | 400; 409 `TASK_NOT_EDITABLE`; 412; 422 as create but the hierarchy; 428 |
| `StartProjectTask` | `POST /project-tasks/{id}/start` | `TASK_UPDATE` | 200 | 409 `INVALID_TRANSITION`, `TASK_NOT_EDITABLE`; 412; 422 `TASK_PROJECT_NOT_ELIGIBLE`, `TASK_DEPENDENCY_UNMET` |
| `BlockProjectTask` | `POST /project-tasks/{id}/block {reason}` | `TASK_UPDATE` | 200 | 400; 409, 412, 422 as start but the dependency |
| `UnblockProjectTask` | `POST /project-tasks/{id}/unblock` | `TASK_UPDATE` | 200 | as block |
| `CompleteProjectTask` | `POST /project-tasks/{id}/complete` | `TASK_UPDATE` | 200 | 409 `INVALID_TRANSITION` (a BLOCKED task), `TASK_NOT_EDITABLE`, `TASK_SUBTASKS_OPEN`; 412; 422 `TASK_PROJECT_NOT_ELIGIBLE`, `TASK_DEPENDENCY_UNMET` |
| `ReopenProjectTask` | `POST /project-tasks/{id}/reopen` | `TASK_REOPEN` | 200 | 409 `INVALID_TRANSITION`, `TASK_NOT_EDITABLE`, `TASK_SUBTASKS_OPEN`; 412; 422 `TASK_PROJECT_NOT_ELIGIBLE` |
| `CancelProjectTask` | `POST /project-tasks/{id}/cancel` | `TASK_MANAGE` | 200 | 409 `INVALID_TRANSITION`, `TASK_NOT_EDITABLE`, `TASK_IN_USE`; 412; 422 `TASK_PROJECT_NOT_ELIGIBLE` |
| `ReportProjectTaskProgress` | `POST /project-tasks/{id}/report-progress {actualPercentComplete}` | `TASK_UPDATE` | 200 | 400; 412; 422 `TASK_PROJECT_NOT_ELIGIBLE`, `TASK_PROGRESS_NOT_ENTERABLE` |
| `ListTaskDependencies` | `GET /task-dependencies?projectId=` | `TASK_VIEW` | 200 `TaskDependencyPage`, oldest first | 400 |
| `CreateTaskDependency` | `POST /task-dependencies {predecessorTaskId, successorTaskId, dependencyType}` | `TASK_MANAGE` | 201 | 400; 409 `TASK_DEPENDENCY_EXISTS`; 422 `TASK_PROJECT_NOT_ELIGIBLE`, `TASK_DEPENDENCY_INVALID`, `TASK_DEPENDENCY_CIRCULAR`, `TASK_DEPENDENCY_UNMET` |
| `DeleteTaskDependency` | `DELETE /task-dependencies/{id}` | `TASK_MANAGE` | 204, also when not there (R-40) | 422 `TASK_PROJECT_NOT_ELIGIBLE` |
| `ListActivityExecutionProgresses` | `GET /activity-execution-progresses?projectId=` | `TASK_VIEW` | 200 `ActivityExecutionProgressPage`, by schedule activity | 400 |

## 5. Error codes

`ProjectTaskErrorCodes` (R-27), beside the platform's `INVALID_TRANSITION`:

| Code | Status | When |
| --- | --- | --- |
| `TASK_PROJECT_NOT_ELIGIBLE` | 422 | Planning while the project is not APPROVED_PLANNED or ACTIVE; execution while it is not ACTIVE |
| `TASK_NOT_EDITABLE` | 409 | The task is CANCELLED |
| `TASK_HIERARCHY_INVALID` | 422 | The parent is not a live, not completed top-level task of the project, or is a dependency end |
| `TASK_SCHEDULE_ACTIVITY_INVALID` | 422 | The activity is not a live leaf of the project's schedule, or a subtask names another than its parent's |
| `TASK_ASSIGNEE_NOT_ELIGIBLE` | 422 | The owner holds no role over the project now |
| `TASK_PRIORITY_INVALID` | 422 | The priority is not a PUBLISHED PRIORITY item |
| `TASK_DEPENDENCY_UNMET` | 422 | A predecessor holds back the start or the completion; or a new dependency the successor has already broken |
| `TASK_SUBTASKS_OPEN` | 409 | A parent's completion with a live subtask not COMPLETED; a subtask's reopen under a COMPLETED parent |
| `TASK_IN_USE` | 409 | A cancellation while a live subtask or a dependency hangs on the task |
| `TASK_PROGRESS_NOT_ENTERABLE` | 422 | A percentage on a parent, or on a task not IN_PROGRESS or BLOCKED |
| `TASK_DEPENDENCY_INVALID` | 422 | Ends not two distinct live leaf tasks of one project |
| `TASK_DEPENDENCY_CIRCULAR` | 422 | The dependency would close a cycle |
| `TASK_DEPENDENCY_EXISTS` | 409 | The two tasks are already linked that way |

## 6. How other modules build on it

1. **WF-02 inputs** (`progress-update.md` F-1): once §8.2 gives Progress an edge to ProjectTask, `IProgressInputs` reads `activity_execution_progress` — the actual percentage per activity — and weights activities by the ACTIVE baseline's durations. A non-authorizing reader in ProjectTask's contracts, like `IScheduleHealthReader`, is the shape; it is not added before its consumer exists.
2. **WF-03 execution actuals** (`schedule-baseline.md` §6 item 3): reporting a task's start and completion to its activity needs a Schedule command and edge 9 revised from query to command (F-3).
3. **TASK-049** (SCR-047, SCR-063–066): `GET /project-tasks?projectId=` gives the board, with each task's `percentComplete`; a person's tasks across projects needs a query of its own (F-8).
4. **AHDA's Appendix A** decides which roles hold `TASK_VIEW` and `TASK_UPDATE` at ASSIGNED — the task owners — and who else views tasks (F-2).

## 7. Verification

Run 2026-10-03 on macOS, Docker Desktop, PostgreSQL 17 (`AHDA-postgres`) and `AHDA-ldap`, with `NUGET_PACKAGES` set to `/Volumes/SanDisk/Bader/Development/Caches/nuget`. Counts on `dev` (76f1af4) were taken from a worktree of it: 842 unit, 534 integration.

| # | Command | Result |
| --- | --- | --- |
| 1 | `dotnet build src/backend/PMPlatform.slnx -warnaserror`, and `--configuration Release -warnaserror` | 0 warnings, 0 errors, both |
| 2 | `dotnet test src/backend/PMPlatform.Tests.Unit` | 921 passed, 79 new: `TaskDependencyRulesTests` 20, `TaskProgressRollupTests` 11 (five of them the mixed-state scenarios), `ProjectTaskWorkflowTests` 9, and `ShippedGrantTests` 39 (three facts — `TasksGoToTheProjectManagerOnly`, `OnlyTheProjectsOwnManagerActsOnItsTasks`, `ReopeningNeedsTheReopenPermissionNotGeneralEditPermission` — and the shipped-grant theories' 36 rows for the four new permissions). `DependencyGraphTests` (9) moved to `Application/Common` with the class. A-1 to A-6, with edge 36 registered, and `TheModelHasNoChangeWithoutAMigration` pass |
| 3 | `DB_CONNECTION_STRING=… dotnet test src/backend/PMPlatform.Tests.Integration` | 553 passed, 19 new: `TaskExecutionTests` 7, `TaskDependencyTests` 4, `ProjectTaskEndpointTests` 3, `ActivityProgressTests` 2, `ProjectTaskGuardTests` 2, `TaskConcurrencyTests` 1. Changed with this task: two count assertions moved with the catalogue (`SeedDataTests` 116 → 120 labels, `AdministrationContractTests` 32 → 36 permissions). `EveryMigrationRollsBackToTheSchemaBeforeIt` runs the three new Downs |
| 4 | `.github/scripts/migration-dry-run.sh`, then `seed-dry-run.sh`, on one scratch database in `AHDA-postgres` (dropped after) | "1751 statement(s), applied twice, no destructive statement"; "12 table(s) seeded, unchanged by a second run, no integrity violation" |
| 5 | `UPDATE_OPENAPI_SNAPSHOT=1 … --filter ProjectContractTests`, then a JSON diff of `docs/api/openapi.v1.json` against `dev` | 4 passed. The snapshot gains 12 paths (15 operations), 13 schemas and the `ProjectTask` tag; no existing operation or schema changed. The enums are the wire values (`NOT_STARTED`…`CANCELLED`, `FS`/`SS`/`FF`/`SF`) |
| 6 | `python3 docs/architecture/contract-check.py docs/api/openapi.v1.json` | 166 operations, 729 findings (664 on `dev`). The 65 new ones are all on the ProjectTask surface and in classes the platform already has: C-4 15, C-5 15, C-7 17 and C-12 17 (the R-52 extensions no module emits, `project-lifecycle-contract-tests.md` F-1), and C-8 1 on the `PUT`, as on every `PUT`. No C-9: every collection is paged |
| 7 | `contract-check.py --self-test`, and the eight other `docs/architecture/*-check.py` gates (`cicd-pipeline-check.py` with the new CI step) | 29 mutations, 0 missed; all OK |
| 8 | `.github/scripts/migration-forward-only.sh` (`BASE_REF=origin/dev`) | "no migration on origin/dev was changed; 3 added after 20261003084051_TASK-046_GuardScheduleHistory" |
| 9 | The running stack (`docker compose … up -d --build --wait`, the three migrations applied by its `migrate` service), driven with `curl` as local.r04 against an ACTIVE project fixture managed by them | Create 201; `start` 200 IN_PROGRESS; `block` 200 BLOCKED; `complete` **409 `INVALID_TRANSITION`**, the row still BLOCKED with no `completed_at` or actual finish; `unblock` 200; `complete` 200 COMPLETED at 100. The fixture project, task and classification item were removed after; the five audit events stay, as the trail is append-only |

The tests, by criterion:

| Criterion | Tests |
| --- | --- |
| 1. A blocked task cannot transition to Completed without first being unblocked (state machine enforced server-side) | Row 9, a direct API call against the running stack. `ABlockedTaskCannotBeCompletedWithoutFirstBeingUnblocked` (the owner and the Project Manager each 409 `INVALID_TRANSITION`; still BLOCKED in the database; unblocked, it completes at 100 %; five audit events). `TheDatabaseHoldsTheTaskStateMachine`: a raw `UPDATE` from BLOCKED to COMPLETED that sets every other column right is refused; ten other refusals. `ProjectTaskWorkflowTests` (`ABlockedTaskCannotBeCompletedUntilItIsUnblocked`, `TheWorkflowHasExactlyTheTenEdgesOfTask048`, `TheCommandsTakeExactlyTheDeclaredEdges`, `CancelledIsFinal`, …). `ATaskBlockedBeforeItStartedIsNotStartedOnceUnblocked` |
| 2. Reopening a completed task requires the specific reopen permission, not general edit permission | `ReopeningACompletedTaskRequiresTheReopenPermissionNotGeneralEditPermission`: local.r02, holding `TASK_UPDATE` and `TASK_MANAGE` at ALL, 403; the task's owner 403; local.r05, holding `TASK_REOPEN` for the projects they manage, 404; still COMPLETED; the Project Manager reopens — IN_PROGRESS, count 1, audited. `ReopeningNeedsTheReopenPermissionNotGeneralEditPermission` (the engine: ALL-scope edit rights are 403 `NOT_GRANTED` on `TASK_REOPEN`; an ASSIGNED `TASK_REOPEN` alone is allowed). `ReopenAndCancelAreDecidedOnTheirOwnPermissions` |
| 3. Task completion percentage aggregation is covered by a unit test with multiple subtasks in mixed states | `MixedStateSubtasksRollUpIntoTheParentAndTheActivity`, five scenarios, each asserted for the parent and its activity: 45 (all five states), 65 (IN_PROGRESS, COMPLETED, a heavy CANCELLED), 31.6667 (two BLOCKED, NOT_STARTED, COMPLETED), 100 (COMPLETED with a CANCELLED), 0 (started without a figure, NOT_STARTED, CANCELLED). `AParentIsTheDurationWeightedMeanOfItsSubtasksInMixedStates` (NOT_STARTED, IN_PROGRESS 50 %, BLOCKED 25 %, COMPLETED, CANCELLED, durations 2/4/2/2/10: 45 %), `AnActivityIsRolledUpFromItsLiveLeavesOnly`, `AParentWhoseSubtasksAreAllCancelledIsALeafAgain`, `FiguresAreRoundedToFourPlaces`; end to end, `SubtasksInMixedStatesRollUpIntoTheParentAndTheActivity` (the parent's `percentComplete` and the stored `activity_execution_progress` both 45.0000) |
| Gate decision ADR-009 | `TheOwnerMaintainsTheActualPercentageAndTheProjectManagerMayEditIt` (the owner 40, the Project Manager 55.5; someone else's task 404; not before the task starts; 101 is 400); planned duration derived from the dates, a sent one ignored (`ATaskNamesOnlyWhatItsProjectAllows`) |
| Participation amendment ADR-013 | `OnlyTheProjectsOwnManagerActsOnItsTasks` (unit: an external R04 on their own project, refused on another manager's and another project's); `AManagerReachesTheirProjectsAndAnOwnerTheirTasks` (local.r08, the entity Project Manager, creates and executes on their project; another manager's is an empty page and 404) |
| Description: dependency and blocking rules, controlled reopen/cancel | `AFinishToStartPredecessorHoldsBackTheStart`, `AFinishToFinishPredecessorHoldsBackTheCompletionOnly`, `CircularDuplicateAndBrokenLinksAreRefused`, `ADependencyJoinsLeafTasksOnly`, `TwoLinksThatTogetherCloseACycleCannotBothCommit`, `TheDatabaseHoldsTheBlockingRulesAndTheNetwork`, `CancellingIsControlledAndFinal`, `AParentCompletesAfterItsSubtasksAndASubtaskReopensOnlyUnderAnOpenParent`, `TasksArePlannedBeforeActivationButExecutedOnlyOnAnActiveProject`, `TaskDependencyRulesTests` |

### 7.1 Mutation tests

Each mutation was applied, the unit and integration test projects rebuilt, the named tests run, and the source restored from a copy (script and output in the git-ignored `artifacts/task-048`). The restored solution built with `-warnaserror` and passed again.

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | `complete` also takes a BLOCKED task (application) | `ABlockedTaskCannotBeCompletedUntilItIsUnblocked`, `TheCommandsTakeExactlyTheDeclaredEdges`, `ABlockedTaskCannotBeCompletedWithoutFirstBeingUnblocked` (the database refused the write, as a 500) |
| M-2 | `reopen` decided on `TASK_UPDATE` | `ReopenAndCancelAreDecidedOnTheirOwnPermissions`. The integration test still passed: the endpoint's `RequirePermission(TASK_REOPEN)` gate refuses the same callers before the service runs, so the rule is held twice |
| M-3 | A parent counts its cancelled subtasks | `AParentIsTheDurationWeightedMeanOfItsSubtasksInMixedStates`, `AParentWhoseSubtasksAreAllCancelledIsALeafAgain`, `SubtasksInMixedStatesRollUpIntoTheParentAndTheActivity` |
| M-4 | The database allows BLOCKED → COMPLETED | `TheDatabaseHoldsTheTaskStateMachine` |
| M-5 | FS is met by the predecessor's start | `APredecessorMeetsTheDependencyAtThePointItsTypeNames`, `ADependencyBrokenOnArrivalIsRecognised`, `OnlyTheDependenciesOfTheStepAskedForAreUnmet`, `AFinishToStartPredecessorHoldsBackTheStart` |
| M-6 | The database's dependency check runs without the project's lock | `TwoLinksThatTogetherCloseACycleCannotBothCommit` |
| M-7 | Tasks are executed on an APPROVED_PLANNED project | `TasksArePlannedBeforeActivationButExecutedOnlyOnAnActiveProject` |
| M-8 | `cancel` ignores the task's dependencies | `ADependencyJoinsLeafTasksOnly` (the database refused the write, as a 500) |
| M-9 | The roll-up is unweighted | `AParentIsTheDurationWeightedMeanOfItsSubtasksInMixedStates`, `AnActivityIsRolledUpFromItsLiveLeavesOnly`, `FiguresAreRoundedToFourPlaces`, `SubtasksInMixedStatesRollUpIntoTheParentAndTheActivity` |

## 8. Acceptance criteria, deliverables and amendments

| Item | Result |
| --- | --- |
| A blocked Task cannot transition to Completed without first being unblocked (state-machine enforced server-side) | **MET.** D-4, D-13; §7 criterion 1; M-1, M-4 |
| Reopening a completed Task requires the specific reopen permission, not general edit permission | **MET.** D-11; §7 criterion 2; M-2 |
| Task completion percentage aggregation is covered by a unit test with multiple subtasks in mixed states | **MET.** D-8; §7 criterion 3; M-3, M-9 |
| Description: Task/Subtask execution, assignment, dependency and blocking rules, the Activity Execution Progress aggregate, controlled reopen/cancel | **MET** against the sources in the header, without a WF-04 specification (F-5); execution actuals are not reported to the schedule (F-3) |
| Deliverables: WF-04 task/subtask service, state machine, aggregation logic, tests | **MET** (header row "Deliverables") |
| Gate decision ADR-009 | **MET.** D-7, D-8; the owners' grants wait for Appendix A (F-2) |
| Participation amendment ADR-013 | **MET.** D-11 |

## 9. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **§8.2 edge 36 was added by the delivery team.** ProjectTask needs its project's anchors and lifecycle state (D-2), which only Project holds; edge 9 gives it Schedule only. The edge is registered in `solution-architecture.md` and `ModuleRegistry` with this task, as edges 1–6 are for the other core modules, and awaits the Engagement Architect's confirmation | Engagement Architect (ADR-003 §8.2) | If declined, WF-04 needs another source for the anchors, and every authorization decision changes with it |
| F-2 | **Partly resolved 2026-10-09 by TASK-066**: its gate decision limits an external contributor's direct source action to assigned tasks, so R08 holds `TASK_VIEW` and `TASK_UPDATE` at ASSIGNED — an entity user works the tasks it owns (`external-participation.md` D-3). Was: **Only R04's task grants ship** (D-11). ADR-009 has the task owner maintain the percentage, but no controlled source says which roles own tasks or at what scope; Blueprint Appendix A is not in the repository. The tests grant R06 `TASK_VIEW` and `TASK_UPDATE` at ASSIGNED, and R02 `TASK_VIEW`, `TASK_UPDATE` and `TASK_MANAGE` at ALL | PMO (Appendix A); TASK-110 | Only Project Managers can work tasks; an owner who is not one cannot report their own progress |
| F-3 | **Execution actuals do not reach the schedule.** `schedule_activity` keeps PLANNED and no actual dates: writing them is a Schedule command, edge 9 is a query, and `guard_schedule_activity()` allows only PLANNED → CANCELLED (`schedule-baseline.md` §6 item 3). Nor does WF-03 know an activity's tasks: an activity that becomes a summary or is cancelled keeps the tasks that execute against it | Engineering Architect (edge 9 as a command); TASK-046 owner | The Gantt shows no actual dates; tasks can sit on a summary or a cancelled activity |
| F-4 | **WF-02 cannot read the Activity Execution Progress**: §8.2 gives Progress no edge to ProjectTask (`progress-update.md` F-1). The aggregate is stored, current and served by the API | Engagement Architect | No progress can be reported from tasks in a real environment |
| F-5 | **No WF-04 functional specification was read.** The rules are the row's, the ERD's and these decisions: blocking before work starts, unblock returning to the prior state, cancel without a reason, execution only on an ACTIVE project, SF accepted, a reopened task keeping its figure | PMO (WF-04 specification) | A specification may change a rule, and its error code with it |
| F-6 | **Reopen is a controlled exception, not an undo.** A reopened task keeps its 100 % until its owner enters a figure, and successors that went ahead on it stay as they are | PMO | The roll-up counts a reopened task as done until its figure is corrected |
| F-7 | **Beyond the ERD** (D-3): the CHECK constraints, `xmin` on two tables, and the guard functions `lock_project` and `has_live_subtasks`. `erd.dbml` is not changed | Engineering Architect (TASK-008) | The ERD understates the schema |
| F-8 | **No cross-project "my tasks" query.** Collections require `projectId` (R-3); SCR-063 My Tasks and index I-33 need one by owner across projects, filtered per project by the engine | TASK-049 | The UI lists tasks one project at a time |
| F-9 | **Dates are UTC calendar dates**, as in `progress-update.md` F-7, and every day is a working day (`schedule-baseline.md` F-3) | Engineering Architect; AHDA PMO | Durations count weekends and holidays |
| F-10 | **"Must hold a project relationship" is read as "holds some role over the project's anchors now"** (D-12). An owner who loses that role keeps their tasks; the check runs only when the owner is set | PMO | A former member stays owner until reassigned |
| F-11 | **The roll-up is reconciled only by writes.** A row is recomputed whenever a task of its project is written; nothing recomputes it on its own | Engineering Architect | A row written by hand stays wrong until the next task write |
| F-12 | **Security Lead review (CTL-43) cannot be requested**: the CODEOWNERS teams do not exist (TASK-031 F-13). This change touches RBAC | Maintainer | The PR's CTL-43 box stays unticked |
| F-13 | **Idempotency keys are required, not replayed** (TASK-031 F-2). A retried task create creates a second task | Engineering Architect | The SPA must not retry a create blindly |
| F-14 | **No notifications.** Assignment, blocking and overdue tasks raise none: NOTIFICATION_ROUTING has no WF-04 event family | TASK-039 configuration | Owners learn of assignments in the UI only |
| F-15 | **No field is masked** (R-20): FIELD_CLASSIFICATION classifies no task field yet | AHDA Cybersecurity (UGV-01) | — |
| F-16 | **`report-progress` is a command but not a lifecycle transition** (R-4 names commands for transitions), as WF-03's `reforecast`. The owner may enter the figure and nothing else of the task, which a `PUT` of the whole task would not allow | Engineering Architect (api-conventions) | — |
| F-17 | **Every task write reads the project's whole board** under its lock. Fine at a project's size | Engineering Architect | A very large project makes each write slower |
| F-18 | **The unit-test host crashes intermittently, outside this module.** `SecretStoreConfigurationProvider` (TASK-019) disposes its refresh timer and semaphore without waiting for a refresh in flight, whose `Release` then throws `ObjectDisposedException` on a thread-pool thread; about 1 run in 14 of `PMPlatform.Tests.Unit` aborted locally with a partial count. Unchanged on `dev` | TASK-019 owner | A CI run can abort on it; at shutdown it can crash the API process |

## 10. Change log

| Date | Change |
| --- | --- |
| 2026-10-03 | Created (TASK-048) |
| 2026-10-03 | Validation pass: `MixedStateSubtasksRollUpIntoTheParentAndTheActivity` (five mixed-state scenarios) added; the blocked-completion refusal checked by a direct API call against the running stack (§7 row 9); F-18 recorded |
| 2026-10-03 | TASK-049 built SCR-047 and SCR-063–066 on this API (`task-boards-ui.md`). F-8 stands: the lists read each visible project's tasks (`task-boards-ui.md` F-4), which also ties them to `PROJECT_VIEW` (F-5 there). The live check confirmed 422 `TASK_ASSIGNEE_NOT_ELIGIBLE` on `assigneeUserId` for an owner with no role over the project |
| 2026-10-09 | TASK-066 (`external-participation.md` D-3, D-10): R08 holds `TASK_VIEW` and `TASK_UPDATE` at ASSIGNED (WF-13 Path A; F-2 partly resolved). `ITaskProgressContributions`, new in the contracts (ADR-003 §8.2 edge 19): WF-13 applies an entity's accepted report of a task's percentage through it — the task's row version read afresh under the project's task lock, a changed task refused as a conflict, WF-04's own rule for entering a percentage (D-7), the roll-up, and `TaskProgressReported` carrying the external lineage. The repository gains `ReadTaskAsync`, a fresh untracked read with the row version |

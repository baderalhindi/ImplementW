# Progress Update & Overall Health (WF-02 Backend)

| Field | Value |
| --- | --- |
| Task | TASK-044 — Build Progress Update & Overall Health (WF-02 Backend) (P8 - Execution & Performance) |
| Depends on | TASK-041 — Build Project Creation & Registration (`project-registration.md`): the project's identity, anchors and lifecycle state, read through the new `IProjectFactsReader` (ADR-003 §8.2 edge 1). TASK-034 — configuration resolution (`master-data-configuration.md`): the reporting cadence and the health thresholds. TASK-030 and TASK-033: the authorization engine and the audit trail |
| Record date | 2026-10-02 |
| Status | **BUILT AND VERIFIED LOCALLY** against `AHDA-postgres` and `AHDA-ldap`, in process through the real API pipeline (§7). In a real environment no figure can be derived yet: WF-03, WF-04 and WF-14 do not exist and §8.2 gives Progress no edge to them (F-1). No one can review (F-2), and no period can be generated nor health computed until AHDA configures the cadence and the thresholds (F-3, F-4) |
| Branch | `feat/task-044-wf02-progress-health-backend` |
| Deliverables | **WF-02 progress submission, review and publication service**: `IProgressService` (`ProgressService`) and four controllers, 11 operations (§4); `ProgressWorkflow` (4 edges) and migration `TASK-044_GuardProgressHistory`, which refuses every other change in the database. **Overall Health calculation engine**: `OverallHealthRule` (ICD-03) over `ProgressRollup` (ADR-009's duration-weighted roll-up and baseline plan), with thresholds from `ProgressPolicy`; the published snapshot is immutable and the live projection is a separate row. **Tests**: 83 unit, 19 integration, 7 mutations (§7). Supporting: the `progress` schema (three migrations), `IProgressRepository`, `IProjectFactsReader` (Project contract), `IProjectHealthReader` (the read side's ICD-03 contract), `ProgressOpeningPosition` (ADR-014), three permissions with ADR-013's grants, a CI step |
| Environment variables / secrets | None. Two FG-04 configuration values (D-4) and the governance profile cadence (D-5) are configuration, not environment |
| Gate decision applied | **ADR-009, SCOPED**: progress derived, never typed in at project level; duration-weighted roll-up; planned from the approved baseline and never editable; actual and planned side by side; a project override needs a recorded reason, keeps the calculated value and is flagged; override tolerance TBC — D-2, D-3, F-5 |
| Participation amendment | **ADR-013**: an assigned entity Project Manager submits progress on their own project; published progress remains governed — D-9, D-10. **ADR-014**: a legacy-intake project's progress starts from an opening position entered once, not reconstructed — D-7. **ADR-017**: progress is pre-filled from the last period, so the default action is confirm — D-8 |
| Sources read | The TASK-044 row as supplied on 2026-10-02 (description, acceptance criteria, directory, deliverables, gate decision, participation amendment); the workbook itself was not opened. ERD §5.5, §6 row 10, §7 row 8, F-008 to F-011; `erd.dbml` `progress.*` and `project.project_intake`; `solution-architecture.md` M-12, §8.2 edges 1, 13, 29, 35, §9, §11.1, §11.2, S-2; ADR-009 and ADR-013 (`adrs/ADR-register.md`). ADR-014, ADR-015 and ADR-017 are not in the register, which holds ADR-001 to ADR-013; they are read as the row and the ERD quote them. `api-conventions.md` R-2 to R-5, R-21, R-27 to R-29, R-47; `indexing-strategy.md` I-37, I-38; `unassigned-gated-values.csv` UGV-02; `project-registration.md` F-12 |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | Reporting periods generated from the governance profile's cadence | This task |
| **In** | A period's progress submission: the derived actual and planned figures, the override with its reason, the narrative; its review, return and publication | This task |
| **In** | Overall Project Health, computed by WF-02 alone (ICD-03): published in an immutable snapshot, and current in a live projection | This task |
| **In** | ADR-014's opening position, ADR-017's pre-fill, ADR-013's submission by the entity Project Manager | This task |
| **Out** | The sources of task-level progress, the baseline, Schedule Health and the financial status | TASK-048 WF-04, TASK-046 WF-03, TASK-052 WF-14 (F-1) |
| **Out** | `ProjectIntakeRecorded` and the intake path that raises it | TASK-104 (F-6) |
| **Out** | The consolidated periodic update session, `periodic_update_session` and its items (ERD §5.5) | TASK-107 (F-11) |
| **Out** | SCR-048, SCR-070, MOD-020 to MOD-022 | TASK-045 |
| **Out** | Dashboards that render health | TASK-069, through `IProjectHealthReader` (D-13) |
| **Out** | Contract tests across the execution domain | TASK-054 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Module shape.** `Features/Progress`: `Contracts` (`IProgressService`, `IProjectHealthReader`, the DTOs and pages, `ProgressErrorCodes`, `Events`), `ProgressService`, `ProgressWorkflow`, `ProgressRollup`, `OverallHealthRule`, `ProgressPolicy`, `ReportingCalendar`, `ProgressAccess`, `ProgressAudit`, `ProgressMapping`, `ProgressOpeningPosition`, `ProjectHealthReader`, the repository port and the inputs port. Entities in `Domain/Progress`; repository and configurations in `Infrastructure/Persistence`. Progress references only Project's, IdentityAccess's and MasterDataConfig's contracts (§8.2 edge 1, E-U1, E-U2); A-1 to A-6 pass unchanged | ADR-003 §4.3 row 2, §6 |
| D-2 | **Progress is derived, never typed in (ADR-009).** `ProgressRollup.Actual` rolls actual progress up the work breakdown weighted by planned duration. Weighting each leaf by its own duration gives the same figure as rolling each summary up from its children, so the project figure is computed from the leaves; a summary's own percentage is not read. `ProgressRollup.Planned` is the share of the baseline's planned days elapsed, each activity accruing linearly over its days, both ends included, as of the period's end, or today while the period runs. No request carries an actual or planned figure, and a client that sends one is ignored. The figures are derived when an update starts, derived again on submission, and fixed from then on, in the application and in the database. Without a work breakdown to derive from, the answer is 422 `PROGRESS_ROLLUP_UNAVAILABLE`, never a figure entered instead | Gate decision; ERD `actual_percent_calculated` "at submission" |
| D-3 | **The override (ADR-009).** The only project-level figure a person enters: `override {actualPercent 0–100 to four places, reason}` on a DRAFT, the reason required, in the language entered. The calculated value stays beside it, `isOverridden` flags it, and the reported figure (`actualPercent`) is the override. `ck_progress_submission_override` refuses one without the other. The published snapshot carries the flag; the live projection never uses an unpublished override. The tolerance is not built: no value exists (UGV-02, F-5) | Gate decision |
| D-4 | **Overall Project Health (ICD-03).** `OverallHealthRule` rates three dimensions: progress, as the slippage of actual behind planned in percentage points against `PROGRESS_HEALTH_AMBER_SLIPPAGE_PERCENT` and `PROGRESS_HEALTH_RED_SLIPPAGE_PERCENT` (WORKFLOW_POLICY values, AMBER from the first, RED from the second, 0 < AMBER ≤ RED); WF-03's Schedule Health; WF-14's financial status. Overall is the worst of the three. A dimension without its inputs is UNKNOWN and so is the overall: a missing input is never coerced to a colour. The rule version used is pinned on every row it computed (ERD D-13). Missing or incoherent thresholds fail closed, 422 `CONFIGURATION_MISSING`. The formula and the family are this task's (F-3) | Acceptance criterion 1; ERD `overall_health` "UNKNOWN when inputs are missing — never coerced"; Blueprint Section 12 |
| D-5 | **Reporting periods.** Generated when an update starts, never when read: consecutive, without gap or overlap, each the governance profile's `update_cadence_days` long (ADR-015), from the activation date, or from the day after the intake date for an intake project (D-7). A period is due at its end (F-9). An update is for the earliest OPEN period that has begun, one period at a time: a second start while one is in progress is 409 `PROGRESS_SUBMISSION_EXISTS`, and once every begun period is published, 409 `PROGRESS_NOTHING_TO_REPORT`. Publication therefore follows the periods' order, and the latest snapshot is the latest period. Progress is reported on an ACTIVE project only (422 `PROGRESS_PROJECT_NOT_ACTIVE`). Dates are UTC (F-7) | ERD `reporting_cycle` |
| D-6 | **The workflow** (§3): DRAFT → SUBMITTED → UNDER_REVIEW → RETURNED or PUBLISHED, four edges, each one command. RETURNED and PUBLISHED are final. Returning records the reason and opens revision + 1 of the same period as a DRAFT with the returned revision's content (ERD: "a RETURNED request creates revision+1"). Publishing, in one transaction, writes the snapshot, closes the period and recomputes the live health. One snapshot per period: `ix_published_progress_snapshot_reporting_cycle_id` is unique, an index the ERD does not list (F-18) | ERD §6 row 10; acceptance criterion 3 |
| D-7 | **ADR-014's opening position.** `ProgressOpeningPosition.RecordAsync` writes a one-day period on the intake date and revision 1 of it, SUBMITTED by the person who recorded the intake, with the intake's `opening_percent_complete` as its figure and `project_intake_id` set. It is reviewed and published like any period: published progress remains governed. It is recorded once per intake; a redelivered event changes nothing. Its figure is never derived again, including on a returned and resubmitted revision. The next period starts the day after the intake. TASK-104's `ProjectIntakeRecorded` consumer calls it (§8.2 edge 35; F-6) | ADR-014: "entered once, not reconstructed" |
| D-8 | **ADR-017's pre-fill.** A new update copies the narrative and the override, with its reason, from the project's last published revision; a revision opened by a return copies the returned one. The calculated figures are always derived fresh. Confirming is submitting what was pre-filled | ADR-017: "the default action is confirm" |
| D-9 | **Review is AHDA's gate, inside WF-02.** `PROGRESS_REVIEW` starts the review, returns and publishes. An external user is refused even when the engine allows them, as are AHDA's other gates (ADR-013), and so is the person who submitted the revision: no one publishes their own progress. Both refusals are 403 and audited `ReviewRefused` with the reason `EXTERNAL_USER` or `SUBMITTER`. The review is not a WF-11 run: §8.2 has no Progress → Approval edge (F-8) | ADR-013: "published progress remains governed" |
| D-10 | **Three permissions; only the amendment's grants ship.** `PROGRESS_VIEW` (Read), `PROGRESS_SUBMIT` (Write: start, edit, submit), `PROGRESS_REVIEW` (Write), group `PROGRESS`. R04 holds view and submit at OWN: a project's owner anchor is its Project Manager, so OWN reaches exactly the projects the holder manages. That covers the entity Project Manager (ADR-013's amendment) and an internal one alike, R04 being employer-neutral, and keeps an entity Project Manager off the other projects of their own entity. R08 views at ENTITY ("entities see progress … and health on their own projects"). Review ships to no one (F-2) | Only source-backed grants ship (`authorization-engine.md` D-9) |
| D-11 | **The database holds the history** (migration `TASK-044_GuardProgressHistory`). A published snapshot is never updated, deleted or truncated, and it must record a submission of its own project and period. A period is never deleted, its dates never change, and once CLOSED it changes no more. A submission is born DRAFT, or SUBMITTED as an opening position, into an OPEN period of its own project. It moves only along the four edges. Its figures, override and narrative change only while DRAFT, RETURNED and PUBLISHED are final, and only a DRAFT is deleted (HARD_DRAFT). The live health row is never deleted and never moves to another project. CHECK constraints tie who submitted and who decided to the states that set them | Acceptance criterion 3, for any writer |
| D-12 | **Reading a project.** `IProjectFactsReader` in Project's contracts (edge 1) returns the identity, anchors, lifecycle state, governance profile, intake marker and activation time. It authorizes no one; Progress decides access on those anchors through the engine. This is the query `project-registration.md` F-12 said a consuming module would add | M-7; edge 1 "project identity, classification, lifecycle state for a reporting cycle" |
| D-13 | **Two semantic states, never merged (M-12).** `progress.published_progress_snapshot` is PUBLISHED/OFFICIAL and `progress.project_health_status` is CURRENT/LIVE, each its own collection. The read side gets both through `IProjectHealthReader` (edge 29) as stored, and never reaches the calculation. `HealthOwnershipTests` (architecture) fails the build if any type outside the Progress module and its own persistence references `OverallHealthRule`, `ProgressRollup`, the thresholds, or the two health entities | Acceptance criteria 1 and 2 |
| D-14 | **API shape (R-2, R-3, R-28).** The collections are the ERD entities' plurals, with the project as a filter, never a path prefix: `/reporting-cycles`, `/progress-submissions`, `/published-progress-snapshots`, `/project-health-statuses`, each offset-paged. `projectId` is required, because the progress rows hold no anchor to filter a caller's scope by. A collection for a project the caller cannot see is an empty page; the check uses the engine's `EvaluateAsync`, which records nothing, because an empty list is not a refusal. A command on a submission the caller cannot see is 404, and on one they can see but may not change, 403 (R-47) | api-conventions |
| D-15 | **Every change is audited** in the saving transaction (event-conventions §4 row 17): `ReportingCycleCreated`, `ProgressUpdateStarted`, `ProgressUpdateChanged` (narrative and reason withheld), `OpeningPositionRecorded`, `ProjectHealthRecomputed` (DATA_CHANGE); `ProgressSubmitted`, `ReviewStarted`, `ProgressReturned`, `ProgressPublished` (LIFECYCLE_TRANSITION, with the state change and revision; publication with the snapshot, its figures, health and rule version); `ReviewRefused` (AUTHORIZATION_DENIAL, recorded on its own) | event-conventions EV-9; CTL-25 |
| D-16 | **The inputs port.** `IProgressInputs` (module-internal) is where the facts other modules own are read. Its only implementation is `NoProgressInputs`, which has nothing to give, and the integration tests substitute `FakeProgressInputs`. Connecting a real source is an ADR-003 revision first (F-1) | ADR-003: "a new edge is a revision of ADR-003 first" |

## 3. The workflow

| From | To | Cause | Who | Writes |
| --- | --- | --- | --- | --- |
| — | DRAFT | `POST /progress-submissions {projectId}` | `PROGRESS_SUBMIT` on the project | the period (generated if due), derived figures, pre-fill |
| — | SUBMITTED | `ProgressOpeningPosition` (ADR-014) | TASK-104's consumer | the intake's one-day period and figure |
| DRAFT | SUBMITTED | `submit` | `PROGRESS_SUBMIT` | figures derived again and fixed; submitter; live health |
| SUBMITTED | UNDER_REVIEW | `start-review` | `PROGRESS_REVIEW`, internal, not the submitter | — |
| UNDER_REVIEW | RETURNED | `return {reason}` | as above | reviewer, reason; revision + 1 as DRAFT |
| UNDER_REVIEW | PUBLISHED | `publish` | as above | reviewer; the snapshot with Overall Health; the period CLOSED; live health |

## 4. Endpoints

All paths are under `/api/v1`, tag `Progress`, operation ids `Progress_*`. Every non-2xx answer is the R-23 envelope; every `POST` and `PUT` also answers 400 `IDEMPOTENCY_KEY_REQUIRED`/`…_INVALID`. Commands honour `If-Match` when sent (R-21).

| Operation | Method and path | Permission | Success | Refusals beyond 401/403/404 |
| --- | --- | --- | --- | --- |
| `ListReportingCycles` | `GET /reporting-cycles?projectId=&page=&pageSize=` | `PROGRESS_VIEW` | 200 `ReportingCyclePage`, earliest first | 400 |
| `ListProgressSubmissions` | `GET /progress-submissions?projectId=&page=&pageSize=` | `PROGRESS_VIEW` | 200 `ProgressSubmissionPage`, newest first (SCR-070, I-37) | 400 |
| `GetProgressSubmission` | `GET /progress-submissions/{id}` | `PROGRESS_VIEW` | 200 `ProgressSubmissionDetail`, `ETag` | — |
| `StartProgressSubmission` | `POST /progress-submissions {projectId}` | `PROGRESS_SUBMIT` | 201, `Location`, `ETag`; DRAFT | 400; 409 `PROGRESS_SUBMISSION_EXISTS`, `PROGRESS_NOTHING_TO_REPORT`; 422 `PROGRESS_PROJECT_NOT_ACTIVE`, `PROGRESS_ROLLUP_UNAVAILABLE`, `CONFIGURATION_MISSING` |
| `UpdateProgressSubmission` | `PUT /progress-submissions/{id} {narrative, override}`, `If-Match` | `PROGRESS_SUBMIT` | 200 | 400; 409 `PROGRESS_NOT_EDITABLE`; 412; 428 |
| `SubmitProgressSubmission` | `POST /progress-submissions/{id}/submit` | `PROGRESS_SUBMIT` | 200 | 409 `INVALID_TRANSITION`; 412; 422 `PROGRESS_PROJECT_NOT_ACTIVE`, `PROGRESS_ROLLUP_UNAVAILABLE`, `CONFIGURATION_MISSING` |
| `StartProgressSubmissionReview` | `POST /progress-submissions/{id}/start-review` | `PROGRESS_REVIEW`, internal, not the submitter | 200 | 409 `INVALID_TRANSITION`; 412 |
| `ReturnProgressSubmission` | `POST /progress-submissions/{id}/return {reason}` | as above | 200 | 400; 409 `INVALID_TRANSITION`; 412 |
| `PublishProgressSubmission` | `POST /progress-submissions/{id}/publish` | as above | 200 | 409 `INVALID_TRANSITION`; 412; 422 `CONFIGURATION_MISSING` |
| `ListPublishedProgressSnapshots` | `GET /published-progress-snapshots?projectId=&page=&pageSize=` | `PROGRESS_VIEW` | 200 `PublishedProgressSnapshotPage`, latest first (I-38) | 400 |
| `ListProjectHealthStatuses` | `GET /project-health-statuses?projectId=&page=&pageSize=` | `PROGRESS_VIEW` | 200 `ProjectHealthStatusPage`, one item once computed | 400 |

## 5. Error codes

`ProgressErrorCodes` (R-27):

| Code | Status | When |
| --- | --- | --- |
| `PROGRESS_PROJECT_NOT_ACTIVE` | 422 | An update of a project that is not ACTIVE (D-5) |
| `PROGRESS_ROLLUP_UNAVAILABLE` | 422 | No work breakdown with planned durations to derive actual progress from (D-2, F-1) |
| `PROGRESS_NOTHING_TO_REPORT` | 409 | Every period that has begun is published (D-5) |
| `PROGRESS_SUBMISSION_EXISTS` | 409 | The period already has a revision in progress (D-5) |
| `PROGRESS_NOT_EDITABLE` | 409 | An edit of a revision that is not DRAFT |

## 6. How other modules build on it

1. **A source of inputs** — WF-04's task progress, WF-03's baseline and Schedule Health, WF-14's financial status. First revise ADR-003 §8.2 with the edge (F-1). Then implement `IProgressInputs`, or replace `NoProgressInputs` with an implementation that reads each source's contract. TASK-046 also adds `fk_progress_submission_…_baseline_id` once `schedule.project_baseline` exists.
2. **The intake path** (TASK-104) calls `ProgressOpeningPosition.RecordAsync` from Progress's consumer of `ProjectIntakeRecorded` (F-6).
3. **The read side** (TASK-069 FG-01, TASK-071 FG-02) reads `IProjectHealthReader` and renders `Published` as the official value, with `Current` beside it when shown. It never recalculates (M-12), and `HealthOwnershipTests` holds it to that.
4. **FinancialKpi's period alignment** (§8.2 edge 13): TASK-052 adds the query it needs to `Progress.Contracts` (F-13).
5. **AHDA's configuration**: the cadence of each governance profile (GOVERNANCE_PROFILE) and the two health thresholds (WORKFLOW_POLICY), through FG-04 (F-3, F-4).

## 7. Verification

Run 2026-10-02 on macOS, Docker Desktop, PostgreSQL 17 (`AHDA-postgres`) and `AHDA-ldap`.

| # | Command | Result |
| --- | --- | --- |
| 1 | `dotnet build src/backend -warnaserror`, and `--configuration Release -warnaserror` | 0 warnings, 0 errors, both |
| 2 | `dotnet test src/backend/PMPlatform.Tests.Unit` | 769 passed, 83 new: `ProgressRollupTests` 15, `OverallHealthRuleTests` 18, `ProgressPolicyTests` 7, `ReportingCalendarTests` 6, `ProgressWorkflowTests` 5, `HealthOwnershipTests` 3 (architecture), and 29 `ShippedGrantTests` cases for the three PROGRESS permissions, including `ProgressGoesToTheProjectManagerAndTheEntityOnly` and `OnlyTheProjectsOwnManagerSubmitsItsProgress`. The shipped-grant probe now places an OWN grant of the entity Project Manager on their assigned project. A-1 to A-6 pass unchanged |
| 3 | `DB_CONNECTION_STRING=… dotnet test src/backend/PMPlatform.Tests.Integration` | 509 passed, 19 new: `PublicationTests` 3, `DerivationTests` 3, `ReviewTests` 3, `ReportingCycleTests` 3, `ProgressGuardTests` 1, `ProgressEndpointTests` 6. Two count assertions moved with the catalogue: `SeedDataTests` 111 → 114 labels, `AdministrationContractTests` 27 → 30 permissions. `MigrationRollbackTests` runs the three new Downs |
| 4 | The CI steps' filter, `--configuration Release --filter "…Integration.Project\|…Integration.Progress"` | 136 passed |
| 5 | `.github/scripts/migration-dry-run.sh`, then `seed-dry-run.sh`, on one scratch database in `AHDA-postgres` (dropped after) | "1377 statement(s), applied twice, no destructive statement"; "12 table(s) seeded, unchanged by a second run, no integrity violation" |
| 6 | `UPDATE_OPENAPI_SNAPSHOT=1 … --filter ProjectContractTests`, then a JSON diff of `docs/api/openapi.v1.json` against `dev` | 4 passed. The snapshot gains 9 paths (11 operations), 15 schemas and the `Progress` tag; no existing path or schema changed |
| 7 | `python3 docs/architecture/contract-check.py docs/api/openapi.v1.json` | 132 operations, 589 findings (544 before). The 45 new ones are all on the Progress surface and all in classes every module already has: C-4 11, C-5 11, C-7 10 and C-12 12 (the R-52 extensions no module emits, `project-lifecycle-contract-tests.md` F-1), and C-8 1 on the `PUT`, as on every `PUT`. No C-9: every collection is paged |
| 8 | `contract-check.py --self-test`, and the eight other `docs/architecture/*-check.py` gates (`cicd-pipeline-check.py` with the new CI step) | 29 mutations, 0 missed; all OK |

The tests, by criterion:

| Criterion | Tests |
| --- | --- |
| 1. Overall Project Health is computed and stored only by this module | `OverallHealthIsComputedAndStoredByWf02AtPublication`: 10 points behind plan is AMBER under the configured thresholds, stored on the snapshot with the rule version, and the live row is stored beside it. `OnlyProgressReachesTheHealthCalculation` and `OnlyProgressReachesTheStoredHealth` (architecture, every build). `OverallHealthRuleTests` (18 cases, including UNKNOWN never coerced), `ProgressRollupTests` |
| 2. A consumer reads the published value and never recalculates it | `AConsumerReadsThePublishedHealthAndNeverARecalculation`: after the inputs collapse, `IProjectHealthReader` still returns the published AMBER, its figure and its pinned rule version, which is the stored row. D-13's architecture tests keep the calculation out of every other module's reach |
| 3. Publishing a snapshot does not alter any prior Published snapshot (the immutability test) | `PublishingAPeriodNeverAltersAnEarlierPublishedSnapshot`: a second publication, with every input changed, leaves the first snapshot and the submission it published byte-identical (`to_jsonb`). The database refuses to update, delete or truncate the snapshot, or to change the published submission. `TheDatabaseHoldsTheReviewWorkflowAndTheHistory` (11 refusals) |
| ADR-009 | `ProgressIsDerivedFromTheWorkBreakdownAndNeverTypedIn` (no breakdown, 422; figures sent by a client ignored; figures derived again on submission and fixed in the database); `AnOverrideNeedsItsReasonKeepsTheCalculatedValueAndIsFlagged` (400 without a reason; calculated kept; published flagged and rated on the override; live rated on the derived figure) |
| ADR-013 | `AnEntityProjectManagerSubmitsOnlyOnTheProjectTheyManage`; `PublishedProgressIsGovernedByAhdaAndNeverByItsSubmitter` (external user and submitter refused, audited, nothing changed; no publication without review); `OnlyTheProjectsOwnManagerSubmitsItsProgress` (unit) |
| ADR-014 | `AnOpeningPositionIsEnteredOnceAndPublishedThroughReview` (recorded once, governed by review, UNKNOWN health with no plan, next period from the day after the intake) |
| ADR-017 | `TheNextPeriodIsPrefilledFromTheLastPublishedSoConfirmingIsTheDefault` |
| Workflow, periods, API | `AReturnedSubmissionContinuesAsTheNextRevision`, `PeriodsAreGeneratedFromTheProfileCadenceAndReportedEarliestFirst`, `ProgressIsReportedOnAnActiveProjectAndOnlyForPeriodsThatHaveBegun`, `ProgressEndpointTests` (6) |

### 7.1 Mutation tests

Each mutation was applied, the solution rebuilt, the named tests run, and the source restored (script and output kept outside the repository).

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | The guard lets a snapshot be updated (trigger on DELETE only) | `PublishingAPeriodNeverAltersAnEarlierPublishedSnapshot` |
| M-2 | A missing schedule or financial input counts as GREEN | `AMissingInputMakesOverallUnknown` (2 cases) |
| M-3 | The published health is rated on the live derived figure instead of what is published | `AnOverrideNeedsItsReasonKeepsTheCalculatedValueAndIsFlagged` |
| M-4 | A Dashboards type calls `OverallHealthRule` | `OnlyProgressReachesTheHealthCalculation` |
| M-5 | The review gate admits the submitter | `PublishedProgressIsGovernedByAhdaAndNeverByItsSubmitter` |
| M-6 | Submission keeps the figure derived at start | `ProgressIsDerivedFromTheWorkBreakdownAndNeverTypedIn` |
| M-7 | A period with a revision in progress can be started again | `PeriodsAreGeneratedFromTheProfileCadenceAndReportedEarliestFirst`, `AnOpeningPositionIsEnteredOnceAndPublishedThroughReview` |

## 8. Acceptance criteria, deliverables and amendments

| Item | Result |
| --- | --- |
| Overall Project Health is computed and stored only by this module | **MET.** D-4, D-13; §7 criterion 1; M-2, M-3, M-4 |
| Any dashboard/report consuming Health reads the published value, never recalculates it | **MET for what exists.** `IProjectHealthReader` returns the stored values and the architecture tests keep the calculation out of every other module. No dashboard or report is built yet (TASK-069, TASK-071) |
| Publishing a progress snapshot does not alter any prior Published snapshot (verified by an immutability test) | **MET.** D-6, D-11; §7 criterion 3; M-1 |
| Description: reporting cycles, review/publication workflow, calculated and published Overall Health, Published snapshots immutable and distinct from current/live | **MET**, with the figures' sources pending F-1 |
| Deliverables: progress submission/review/publication service; Overall Health calculation engine | **MET** (header row "Deliverables") |
| Gate decision ADR-009 | **MET**, except the override tolerance, which has no value (UGV-02, F-5). D-2, D-3 |
| ADR-013 | **MET.** D-9, D-10 |
| ADR-014 | **MET on WF-02's side.** D-7; the event that triggers it is TASK-104's (F-6) |
| ADR-017 | **MET for the progress record.** D-8; the consolidated session is TASK-107's (F-11) |

## 9. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **WF-02 has no path to its inputs.** ADR-009 has WF-02 roll up WF-04's task progress against WF-03's baseline, and the ERD has the snapshot copy WF-03's Schedule Health and WF-14's financial status. But ADR-003 §8.2 gives Progress no edge to ProjectTask, Schedule or FinancialKpi, and none of the three is built. A direct Progress → FinancialKpi query would also close a cycle with edge 13. Until §8.2 is revised and the modules exist, `NoProgressInputs` answers: an update is 422 `PROGRESS_ROLLUP_UNAVAILABLE` and every health is UNKNOWN. `progress_submission.baseline_id` has no foreign key until TASK-046 creates `schedule.project_baseline` | Engineering Architect (ADR-003 revision), with TASK-046, TASK-048, TASK-052 | No progress can be reported in a real environment |
| F-2 | **No role holds `PROGRESS_REVIEW`**: Blueprint Appendix A is not in the repository; only ADR-013's grants ship (D-10). The tests grant review to R02 and R03 | PMO (Appendix A); TASK-110 | Progress can be submitted but never published |
| F-3 | **The Overall Health formula is this task's**: ICD-03 (Blueprint) is not in the repository. Worst of progress slippage, Schedule Health and financial status, with UNKNOWN propagating. The thresholds are AHDA's and have no value; that they live in WORKFLOW_POLICY is the delivery team's choice | AHDA PMO | Submission and publication answer 422 `CONFIGURATION_MISSING` until the two values are published |
| F-4 | **No governance profile has a cadence**: the seed holds no profile settings (`seed-data-and-integrity.md`; OQ-014 and the cadence are AHDA's) | AHDA PMO; TASK-105 | No period can be generated: 422 `CONFIGURATION_MISSING` |
| F-5 | **The override tolerance (UGV-02) is not built**: no value exists. When AHDA sets one, it is a WORKFLOW_POLICY value checked at submission | AHDA PMO | Any override with a reason is accepted, flagged |
| F-6 | **`ProjectIntakeRecorded` does not exist** (TASK-104). `ProgressOpeningPosition` is ready and tested in process; the consumer and its per-consumer outbox delivery are TASK-104's (`approval-framework.md` F-13) | TASK-104 | Intake projects start from their first regular period, with no opening position |
| F-7 | **Dates are UTC calendar dates**: the platform has no business time zone. A period boundary falls at 03:00 in Riyadh | Engineering Architect | An update started between 00:00 and 03:00 local time belongs to the previous UTC day |
| F-8 | **Review is not a WF-11 run.** The ERD's note on `revision_no` mentions "a new ApprovalInstance (TASK-035)", but §8.2 has no Progress → Approval edge, so review is WF-02's own gate (D-9) | PMO; Engineering Architect | If progress must route through WF-11, §8.2 gains the edge and review becomes a run, as Project's is |
| F-9 | **A period is due at its end**: no source states a grace period | PMO | Reminders (WF-15) would fire on the last day of the period |
| F-10 | **The live health is recomputed on submission and publication only**: no WF-03, WF-04 or WF-14 change can trigger a recompute until F-1. `computedAt` states its freshness | Engineering Architect, with F-1 | The current value can be older than the data it summarises |
| F-11 | **The periodic update session is not built**: its two tables are TASK-107's consolidated flow (ADR-017). TASK-044 applies ADR-017's pre-fill on the progress record itself | TASK-107 | — |
| F-12 | **No notification on submission, return or publication** (E-U4): NOTIFICATION_ROUTING has no progress event family | PMO; TASK-039 configuration | Reviewers learn of a submission only from their screens |
| F-13 | **No query contract for edge 13** (FinancialKpi → Progress, period alignment): TASK-052 adds the one it needs | TASK-052 | — |
| F-14 | **Security Lead review (CTL-43) cannot be requested**: the CODEOWNERS teams do not exist (TASK-031 F-13). This change touches RBAC | Maintainer | The PR's CTL-43 box stays unticked |
| F-15 | **`NarrativeText` is written `{text, language: "EN"}`**, the platform's shape (`approval-framework.md` F-12) | Engineering Architect | As that finding |
| F-16 | **No field is masked** (R-20): FIELD_CLASSIFICATION classifies no progress field yet | AHDA Cybersecurity (UGV-01) | Progress figures are shown to everyone who may view them |
| F-17 | **Idempotency keys are required, not replayed** (TASK-031 F-2). A retried start answers 409 `PROGRESS_SUBMISSION_EXISTS` rather than creating a second revision: the period's revision number is unique | Engineering Architect | The SPA must treat that 409 after a retry as success |
| F-18 | **Beyond the ERD**: a unique index on `published_progress_snapshot.reporting_cycle_id` (one official record per period), CHECK constraints on percentages and on the submitter and reviewer columns, and `xmin` as the submission's concurrency token (D-16). `erd.dbml` is not changed | Engineering Architect (TASK-008) | The ERD understates the constraints |

## 10. Change log

| Date | Change |
| --- | --- |
| 2026-10-02 | Created (TASK-044) |

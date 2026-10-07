# Execution Domain Contract and Integration Tests (WF-02/03/04/05/14)

| Field | Value |
| --- | --- |
| Task | TASK-054 — Contract & Integration Tests for Execution Domain (WF-02/03/04/05/14) |
| Depends on | TASK-044 — WF-02 progress and Overall Health (`progress-update.md`). TASK-046 — WF-03 schedule and baselines (`schedule-baseline.md`). TASK-048 — WF-04 tasks (`project-task.md`). TASK-050 — WF-05 milestones (`milestone-achievement.md`). TASK-052 — WF-14 financial progress and KPIs (`financial-kpi.md`). TASK-043 — the contract-test machinery this extends (`project-lifecycle-contract-tests.md`) |
| Record date | 2026-10-05 |
| Status | **BUILT AND VERIFIED LOCALLY** against `AHDA-postgres` and `AHDA-ldap` (§5), and wired into the `backend` job of `ci-quality-gates` (D-9). Each of the four separations, and the contract gates, fails when broken on purpose (§5.1) |
| Branch | `test/task-054-wf02-14-execution-integration-tests` |
| Deliverables | **Cross-module regression suite** under `src/backend/PMPlatform.Tests.Integration/Execution`: `OverallHealthAuthorityTests` (3), `SharedMilestoneTests` (1), `PublishedSnapshotTests` (1), `ApprovedBaselineReferenceTests` (1), `ExecutionContractTests` (15). **CI wiring**: step "Execution domain contract and integration tests" in `.github/workflows/ci-quality-gates.yml`. Supporting: `OpenApiDocument.BuiltAsync`, `LintAsync` and `OpenPlatformFindings`, moved from `ProjectContractTests` (D-7); `MilestoneTestHost.Rebaselines` (D-6); `CONTRIBUTING.md`; this record |
| Environment variables / secrets | None. `UPDATE_OPENAPI_SNAPSHOT=1` rewrites the snapshot, as for TASK-043 |
| Sources read | The TASK-054 row as supplied on 2026-10-05 (description, acceptance criteria, directory, deliverables); the five dependency records above; `solution-architecture.md` §8.2 (edges 10, 13, 29), §9, M-12, ICD-03 and ICD-04 (line 400); `erd.md` F-015, F-017, F-062; `api-conventions.md` R-10, R-11, §9 row 5; the existing architecture tests `HealthOwnershipTests`, `ScheduleHealthOwnershipTests`, `MilestoneIdentityTests` |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | The four separations of the TASK-054 row, each tested through the real API, WF-11 and the outbox, and against the database | This task |
| **In** | The WF-02, WF-03, WF-04, WF-05 and WF-14 APIs against the committed snapshot (R-10) and the TASK-009 lint: TASK-043 F-4 for these five tags | This task |
| **In** | Running both in CI on every pull request | This task |
| **Out** | Connecting WF-02 to its real inputs (`IProgressInputs`): an ADR-003 §8.2 revision first | Engineering Architect (F-1) |
| **Out** | Example replay of the execution operations (§9 row 5 (c)) | F-5 |
| **Out** | WF-04 has no separation of its own in the row; its API is covered by the contract gates only | — |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Separation 1, WF-02 alone holds Overall Health, is tested where a reimplementation would show.** `HealthOwnershipTests` (unit, Mono.Cecil) already fails when another module reaches `OverallHealthRule` or the stored health. It cannot see a module that computes a health of its own. So `OverallHealthAuthorityTests` fails when (a) any column named `*health*` exists outside the exact list WF-02 and WF-03 own (`information_schema`), (b) any API outside Progress and Schedule serves a property named `*health*` (the built OpenAPI document, per tag), or (c) publishing WF-14's figures, the input WF-02 rates, writes any live or published Overall Health (rows, and `IProjectHealthReader`) | The row asks for "a second Health calculation" to fail the suite. A second calculation that is stored or served is caught by (a) or (b) whatever its code looks like (M-1) |
| D-2 | **Separation 2, one shared milestone row, follows one milestone through every write either authority makes.** In order: WF-03 plans it on an activity; baseline 1 through WF-11; WF-03 reforecasts; rebaseline 2 under a change authorisation; WF-05 claims and WF-11 accepts (WF-03 records ACHIEVED); WF-05 corrects (revision 2 accepted). After each step: the project has exactly one `project_milestone` row, and it is this one; the tables with a foreign key to it are exactly `baseline_milestone` and `milestone_achievement` (`pg_constraint`); every row in them names this milestone. Then both APIs show it: WF-03's as ACHIEVED against baseline 2, and WF-05's two revisions naming it | The table has no natural key, so nothing in the database stops a duplicate (F-2). The only existing count was after a WF-05 correction. Rebaselining and a WF-03 edit after baselining were not covered (M-2) |
| D-3 | **Separation 3, WF-14's published period, applies every later correction the design allows and reads the period back whole.** A period is published: the financial snapshot (AMBER) and a KPI measurement (AMBER). The API then refuses a correction in place: an edit of the update or the measurement (409 `FINANCIAL_KPI_NOT_EDITABLE`), a second publication (409), or a second measurement for the month (409 `KPI_MEASUREMENT_EXISTS`). Then the allowed corrections are applied: a new Approved Budget version, the next period restating the to-date figures, and a new KPI target version. The live position shows them. The snapshot, the update and the measurement, as the API serves them, are `DeepEquals` to what they were, and their three rows are identical. Raw `UPDATE` and `DELETE` are refused | `financial-kpi.md` D-9, D-13; a published figure has no correction (F-12, here F-6). The earlier test compared two snapshot fields through the API. This compares every field, so a read side that re-derives any of them fails (M-3) |
| D-4 | **Separation 4, the Approved Baseline as the one reference, walks a rebaseline through every state that is not approval.** Baseline 1 is approved and the forecast slips 6 days (AMBER). The plan is redrawn and baseline 2 is drafted, then submitted to WF-11, then returned. The schedule is written after each step, which is what restages health (D-9 of `schedule-baseline.md`). Throughout, every reader names baseline 1: the health API, `IScheduleHealthReader` (edge 29), the stored `schedule_health_status` row and the activity's variance, all agreeing with the project's one ACTIVE baseline in the database. On approval, all of them switch to baseline 2 at once (GREEN 0), and the next slip is measured from it alone (RED) | `schedule-baseline.md` D-8, D-9, D-17. No test had a pending or returned candidate beside an ACTIVE baseline while health was restaged (M-4) |
| D-5 | **Contract gates for the five tags** (`ExecutionContractTests`), as TASK-043 D-2 to D-4 do for Project. For each tag: the R-10 diff against `docs/api/openapi.v1.json`; the snapshot equal to the API as built, with its operation count (Progress 11, Schedule 25, ProjectTask 15, Milestone 9, FinancialKpi 46); and the TASK-009 lint. The lint accepts TASK-043's platform-wide classes and exactly the 15 module findings these surfaces carry today (F-4), so a new finding fails and a cleared one must be removed from the list | TASK-043 F-4: "other modules' changes are not gated until their contract tasks … add their tags". The snapshot already held the five tags and matched the build |
| D-6 | **Each test runs on the host of the module it starts from**, in the `Execution` namespace and that module's xUnit collection: Separation 1 and 3 on `FinancialKpiTestHost`, 2 on `MilestoneTestHost`, 4 and the contract gates on `ScheduleTestHost`. One merged host would publish the hosts' WORKFLOW_POLICY and APPROVAL_AUTHORITY versions side by side, and the thresholds the existing tests assert would depend on which applies. `MilestoneTestHost` now registers `FakeRebaselineAuthorization` (`Rebaselines`), as `ScheduleTestHost` does, so a milestone can be rebaselined; no existing Milestone test changes | KISS: the module fixtures already seed what each interaction needs |
| D-7 | **The contract machinery is shared, not copied.** `OpenApiDocument.BuiltAsync` (fetch, and rewrite the snapshot under `UPDATE_OPENAPI_SNAPSHOT=1`), `LintAsync(tag)` (run `contract-check.py`, return the findings on the tag's operations and schemas) and `OpenPlatformFindings` moved there from `ProjectContractTests`, which now calls them. `LintAsync` writes a uniquely named file and deletes it, because the Project and Execution contract tests run in parallel collections | DRY. The Project contract tests pass unchanged (§5 row 5) |
| D-8 | **The violations are proven, not assumed.** `artifacts/task-054/mutations.py` (local, not committed: `artifacts/` is ignored) applies each mutation of §5.1 to production code, builds, runs the Execution suite, restores the file with a fresh mtime, and rebuilds the clean source at the end | The row's third criterion |
| D-9 | **CI**: one step in the `backend` job of `ci-quality-gates.yml`, after the FinancialKpi step, with `--filter FullyQualifiedName~PMPlatform.Tests.Integration.Execution` on the same PostgreSQL service and test directory. The workflow runs on every pull request to `main`, `dev` and `stage` with no path filter, so any PR touching these modules runs it; the job is a required check, and `ci-cd-pipeline.yml` calls the same workflow. The step runs in about 7 s. No module step's filter matches `…Integration.Execution`, so nothing runs twice | The row's second criterion |

## 3. The separation suite

| Test | Separation | Fails when |
| --- | --- | --- |
| `OverallHealthAuthorityTests.OnlyWf02AndWf03StoreAHealth` | 1 | A `*health*` column exists outside `progress.project_health_status`, `progress.published_progress_snapshot` (with its Schedule Health copy) and `schedule.schedule_health_status` |
| `OverallHealthAuthorityTests.OnlyWf02ServesOverallHealth` | 1 | An API other than Progress (`overallHealth`, `scheduleHealth`, `healthRuleConfigurationVersionId`) or Schedule (`scheduleHealth`, `healthRuleConfigurationVersionId`) serves or accepts a `*health*` property |
| `OverallHealthAuthorityTests.PublishingWf14FiguresComputesNoOverallHealth` | 1 | Publishing a WF-14 period (RED) leaves any live or published Overall Health for the project, in the rows or through `IProjectHealthReader` |
| `SharedMilestoneTests.OneMilestoneStaysOneRowThroughEveryWriteOfWf03AndWf05` | 2 | After any of six writes the project has a second `project_milestone` row, another table references the shared row, or a reference names another milestone (D-2) |
| `PublishedSnapshotTests.APublishedPeriodIsUnchangedByEveryLaterCorrection` | 3 | Any field of the published snapshot, update or measurement differs through the API or in its row after the corrections; or the API accepts a correction in place (D-3) |
| `ApprovedBaselineReferenceTests.EveryReaderMeasuresAgainstTheActiveApprovedBaselineAndOnlyIt` | 4 | A reader names a baseline other than the one ACTIVE, the readers disagree, or the variance is not measured from it (D-4) |
| `ExecutionContractTests` × 15 | Contract | Per tag: an R-10 break, a surface not equal to the snapshot or an operation count changed, or a lint finding not recorded (D-5) |

## 4. Interfaces exercised

| Interaction | Through |
| --- | --- |
| WF-14 → WF-02 periods (edge 13) | `progress.reporting_cycle` periods; financial publication over them |
| WF-02 read side (edge 29) | `IProjectHealthReader` |
| WF-05 → WF-03 shared milestone (edge 10) | `/project-milestones`, `/milestone-achievements`, WF-11 outcome → `IMilestoneAchievementRecorder` |
| WF-03 → WF-11 baseline approval (edge 21), WF-08 authorisation (edge 11) | `/project-baselines` submit, WF-11 decision and outbox delivery; a real WF-08 authorisation, issued by `ChangeAuthorizationFixture` (TASK-060) |
| WF-03 read side (edge 29) | `IScheduleHealthReader`, `/schedule-health-statuses` |
| WF-14 → WF-11 budget and target approval (edge 26), → DocumentManagement (edge 38) | `/financial-commitments`, `/kpi-target-versions`; the budget letter scanned CLEAN |

## 5. Verification

Run 2026-10-05 on macOS, Docker Desktop, PostgreSQL 17 (`AHDA-postgres`) and `AHDA-ldap`.

| # | Command | Result |
| --- | --- | --- |
| 1 | `dotnet build src/backend -warnaserror`, and `--configuration Release -warnaserror` | 0 warnings, 0 errors, both |
| 2 | `dotnet test src/backend/PMPlatform.Tests.Unit --configuration Release` | 1089 passed (architecture tests unchanged) |
| 3 | `DB_CONNECTION_STRING=… dotnet test src/backend/PMPlatform.Tests.Integration --configuration Release` | 612 passed, 21 new. Every existing test unchanged |
| 4 | The CI step's command: `… --configuration Release --filter FullyQualifiedName~PMPlatform.Tests.Integration.Execution` | 21 passed, 7.3 s wall clock |
| 5 | The Project and Milestone CI steps' commands (shared contract helpers, D-7; the Milestone host, D-6) | 117 passed; 20 passed |
| 6 | `python3 docs/architecture/contract-check.py --self-test` and the eight other `docs/architecture/*-check.py` gates | 29 mutations, 0 missed; all OK |

### 5.1 Mutation tests

Each mutation was applied to production code, the solution built, the Execution suite run, and the source restored (D-8). The restored source then passed again (row 4).

| # | Violation introduced | Tests failed |
| --- | --- | --- |
| M-1 | **A second Health calculation.** WF-14's `PublishedFinancialSnapshotDetail` serves an `OverallHealth` it derives | `OnlyWf02ServesOverallHealth`; `EachExecutionSnapshotIsTheApiAsBuilt(FinancialKpi)` |
| M-2 | A WF-03 milestone edit writes a new `project_milestone` row for the same milestone beside the edited one | `OneMilestoneStaysOneRowThroughEveryWriteOfWf03AndWf05`, at the reforecast: "the project has 2 shared milestone rows" |
| M-3 | The published snapshots are served with the Approved Budget in force now instead of the one each copied | `APublishedPeriodIsUnchangedByEveryLaterCorrection`: "The published snapshot changed" |
| M-4 | Schedule Health is staged against the newest baseline not superseded, so a pending candidate becomes the reference | `EveryReaderMeasuresAgainstTheActiveApprovedBaselineAndOnlyIt`, with the candidate at WF-11: health named baseline 2 (GREEN 0), not baseline 1 (AMBER 6). Not at the draft step: health is restaged by a schedule write, and drafting is not one (F-7) |
| M-5 | A Schedule response property removed: `ScheduleHealthStatusDetail.ComputedAt` hidden with `[JsonIgnore]` | `EachExecutionApiKeepsEveryPromiseOfTheSnapshot(Schedule)`, `EachExecutionSnapshotIsTheApiAsBuilt(Schedule)` |
| M-6 | A Progress route renamed against the conventions: `api/v1/projectHealthStatuses` | `EachExecutionApiFollowsTheConventions(Progress)`, `EachExecutionApiKeepsEveryPromiseOfTheSnapshot(Progress)`, `EachExecutionSnapshotIsTheApiAsBuilt(Progress)` |

### 5.2 Validation in CI

M-1, the row's example, was also run in CI on 2026-10-05: draft PR #61 (never merged; closed and its branch deleted) committed a duplicate Overall Health in WF-14 (`PublishedFinancialSnapshotDetail.OverallHealth`) on top of this branch.

| Run | Commit | Result |
| --- | --- | --- |
| 37354073725 | `df6be80`, the violation | `backend` **failed**, only in "Execution domain contract and integration tests": `OnlyWf02ServesOverallHealth` ("Found: FinancialKpi: overallHealth; …") and `EachExecutionSnapshotIsTheApiAsBuilt(FinancialKpi)`, 2 of 21. Every earlier step passed, the unit architecture tests and the FinancialKpi suite included, so the new step is the one that catches it |
| 37354774276 | `8912fb2`, the revert (tree identical to this branch) | All checks pass; the Execution step 21 of 21 |

## 6. Acceptance criteria and deliverables

| Item | Result |
| --- | --- |
| Each of the 4 separation rules has at least one automated regression test | **MET.** §3: Separation 1 by three tests, 2, 3 and 4 by one each |
| The suite runs in CI on every PR touching these modules | **MET.** D-9: every PR to `main`, `dev` or `stage`, as a required check |
| A deliberately introduced violation (e.g. a second Health calculation) fails the suite | **MET.** §5.1: M-1 to M-4, one per separation, M-1 being the row's example; M-5 and M-6 for the contract gates |
| Description: the separations hold "under real interaction" | **MET for every interaction built**, through the API, WF-11 and the outbox (§4). WF-02's inputs from WF-03 and WF-14 are not built (F-1) |
| Deliverables: cross-module integration/regression suite, CI wiring | **MET** (header row "Deliverables") |

## 7. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **WF-02 rates no real input.** The only production `IProgressInputs` is `NoProgressInputs` (`progress-update.md` F-1, `financial-kpi.md` F-14): WF-02 reads neither WF-03's Schedule Health nor WF-14's financial status, and ADR-003 §8.2 has no edge for it. Separation 1 is proven as "no other module stores, serves or writes a health", not as "WF-02's published health is the rule over the stored Schedule Health and WF-14 status". When the inputs are connected, add that test here: publish WF-03 and WF-14 values, publish WF-02, and assert the snapshot's copies equal the stored sources | Engineering Architect (ADR-003 revision), then this suite | Overall Health is UNKNOWN in every real environment, and the end-to-end health path is untested |
| F-2 | **`schedule.project_milestone` has no natural key**, so "one row" rests on a single insert path (`ProjectMilestoneService.CreateAsync`). D-2 catches a duplicate from every write path that exists today. A new writer, such as legacy intake (`milestone-achievement.md` F-8, TASK-104), must add its step to `SharedMilestoneTests` | WF-03 owner; TASK-104 | A writer added without a test can duplicate a milestone unnoticed |
| F-3 | **A Declared Baseline (ADR-014) is ACTIVE without WF-11 approval**, and Schedule Health is measured against it (`schedule-baseline.md` D-13). Separation 4 reads "Approved Baseline" as the one ACTIVE baseline, which is approved except at legacy intake. That case is not exercised here | TASK-104 | None while intake is unbuilt; then a Declared Baseline is the reference by design |
| F-4 | **15 lint findings on the execution surfaces beyond the platform's**, accepted by exact text (D-5). The C-2 findings are on child collections under a baseline (3), a milestone achievement (2) and a financial commitment (2). The C-9 findings are unpaged lists on achievement evidence (2), commitment documents (2) and the two portfolio aggregates (4) | Schedule, Milestone and FinancialKpi owners, with the TASK-009 owner for whether R-3/R-4 should admit these paths | They stand as accepted deviations; each fix removes its line from the list |
| F-5 | **Example replay (§9 row 5 (c)) is not run for the 106 execution operations.** TASK-043 replays Project's 9 in process (its D-8). Nothing checks that an execution response matches its documented schema | Module owners, or a later contract task | A response that drifts from its document is caught only by consumers |
| F-6 | **A published WF-14 figure has no correction** (`financial-kpi.md` F-12). D-3 covers the corrections the design allows, and asserts that the API refuses one in place | PMO (whether a correction path is needed) | A wrong published value stands |
| F-7 | **Schedule Health is restaged only by a schedule write** (`schedule-baseline.md` D-9), so a mutant that took a candidate as the reference was caught at the first write after submission, not at drafting (M-4). D-4 writes the schedule after each candidate state, so the defect cannot pass the test. It is noted because the stored row can lag the baseline states until the next write | WF-03 owner | None for the separation; the freshness is `computedAt`'s |

## 8. Change log

| Date | Change |
| --- | --- |
| 2026-10-05 | Created (TASK-054) |
| 2026-10-07 | TASK-060 (`change-request.md`): the rebaselines of `ApprovedBaselineReferenceTests` and `SharedMilestoneTests` apply a real WF-08 change authorisation, issued by `ChangeAuthorizationFixture`, in place of the deleted `FakeRebaselineAuthorization` |

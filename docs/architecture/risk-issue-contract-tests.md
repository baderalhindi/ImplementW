# Risk and Issue Domain Contract and Regression Tests (WF-06/07)

| Field | Value |
| --- | --- |
| Task | TASK-059 — Contract & Regression Tests for Risk/Issue Domain (WF-06/07) (P9 - Risk & Issue Management (WF-06/07)) |
| Depends on | TASK-055 — WF-06 risk management (`risk-management.md`). TASK-057 — WF-07 issue and challenge management (`management-concern.md`). TASK-043 and TASK-054 — the contract-test machinery this extends (`project-lifecycle-contract-tests.md`, `execution-domain-contract-tests.md`) |
| Record date | 2026-10-07 |
| Status | **BUILT AND VERIFIED** locally against `AHDA-postgres` and `AHDA-ldap` (§5), and in CI, where it is wired into the `backend` job of `ci-quality-gates` (D-9). Each invariant test, and each contract gate, fails when its rule is broken on purpose (§5.1); the row's violation failed it in CI (§5.2) |
| Branch | `test/task-059-wf06-07-risk-issue-tests` |
| Deliverables | **Invariant regression tests** under `src/backend/PMPlatform.Tests.Integration/RiskIssue`: `RiskRatingPinningTests` (1) and `ConcernSeverityPinningTests` (1) for invariant 1; `SeverityInputTests` (2) for invariant 2. **Contract test suite**: `RiskIssueContractTests` (6), `RiskResponseContractTests` (1), `ConcernResponseContractTests` (1). **CI wiring**: step "Risk and issue domain contract and regression tests" in `.github/workflows/ci-quality-gates.yml`, and the Risk step's filter narrowed (D-9). Supporting: `RiskIssueApi`; `DocumentedResponses`, `OpenApiDocument.SchemasReachedBy` and `RequestPropertyNames` (D-7); `RiskDriver.PublishMatrixAsync`'s optional labels (D-6); `CONTRIBUTING.md`; this record |
| Environment variables / secrets | None. `UPDATE_OPENAPI_SNAPSHOT=1` rewrites the snapshot, as for TASK-043 |
| Sources read | The TASK-059 row of the workbook's Implementation Plan (description, acceptance criteria, directory, deliverables, branch, validation checks); the two dependency records above, in particular `risk-management.md` D-4, D-13, §4, F-11 and `management-concern.md` D-4, D-10, D-12, F-9; the TASK-043 and TASK-054 records and their tests; `docs/api/openapi.v1.json` (the Risk and ManagementConcern surfaces). The WF-06 and WF-07 functional specifications were not re-read: both invariants are the row's, as the dependency records built them |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | Invariant 1: a rating, and a severity, are pinned to the RISK_MATRIX version in force when the assessment is made | This task |
| **In** | Invariant 2: Severity — and a risk's rating, the overall impact and the pin — is never accepted as raw client input | This task |
| **In** | The WF-06 and WF-07 APIs against the committed snapshot (R-10), the TASK-009 lint, and every operation's answer against its documented schema | This task |
| **In** | Running all of it in CI on every pull request | This task |
| **Out** | The modules' other rules (lifecycle, reopen permission, acceptance expiry, escalation exactly once, ADR-013/015) | Their own suites, `Risk` and `ManagementConcern`, unchanged |
| **Out** | Refusal answers (4xx) against the document: the document declares none (F-3) | TASK-009 owner |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Invariant 1 is held on both sides of the domain.** A risk's rating (`risk-management.md` D-4) and a concern's severity (`management-concern.md` D-4) are both computed from a RISK_MATRIX version and pinned to it. `RiskRatingPinningTests` guards the first, `ConcernSeverityPinningTests` the second, including an issue a risk materialised into (edge 15) | The row names "assessment ratings"; WF-07's severity is computed and pinned by the same matrix, so a guard on one side only would leave the other unguarded |
| D-2 | **Invariant 1 compares whole representations on every read that serves the value.** A value is recorded under version A; version B is published to change it — for a risk, B rates the same cell the other way *and* relabels every rating; for a concern, B maps the same level to another severity. Every read is then `DeepEquals` to what it served before, and the row is unchanged. The reads are all GET operations whose answer reaches `RiskRatingDetail` (4) or `ConcernDetail` (2), taken from the built OpenAPI document, so a new read that serves a rating or severity fails the test until it is added. The risk is identified ten days before it is assessed (the pin is the version in force at assessment, not at identification); its assess request claims a rating, an overall impact and a version of its own; a command's answer (`monitor`) is compared too; and the stored rating row is checked to belong to the pinned version, which the database does not hold (`risk-management.md` F-11). A reassessment then pins B, and the first version keeps A's | TASK-055's test compares the rating code and pin on three reads, not the register; a read that relabels from the matrix in force passed it (M-3) |
| D-3 | **Invariant 2 is held twice.** (a) Static: no property of any WF-06 or WF-07 request body, at any depth, matches `severity`, `rating`, `overallImpact` or `matrixConfigurationVersion` in the generated OpenAPI document. (b) Behavioural: every WF-07 operation that takes a body (7), and WF-06's materialisation (the other way a concern is raised), is sent with every computed field claiming CRITICAL at level 5 under a version that does not exist; after each, the concern's severity is the computed one in the answer, on a read and in its row, and the escalation's NotificationIntent carries the computed severity. The set of writes is taken from the document, so a new one fails until it is added | (a) catches a severity added to a request model whatever the service does with it; (b) catches one taken without a documented property (M-2), which (a) and the snapshot gate cannot see |
| D-4 | **Contract gates for the two tags** (`RiskIssueContractTests`), as TASK-054 D-5: per tag, the R-10 diff against `docs/api/openapi.v1.json`; the snapshot equal to the API as built, with its operation count (Risk 22, ManagementConcern 15); and the TASK-009 lint, accepting only the platform's open finding classes. Neither surface carries a finding of its own (`risk-management.md` §7 row 6, `management-concern.md` §7 row 6), so none is recorded | Acceptance criterion: "contract tests catch a breaking schema change in either domain's API" |
| D-5 | **Every one of the 37 operations is called and its answer checked against its documented schema** (`RiskResponseContractTests`, `ConcernResponseContractTests`), as TASK-043 D-8 does for Project: one risk from registration through assessment, treatment, acceptance, materialisation, closure and reopen; one concern from raise through assessment, assignment, an escalation resolved and one withdrawn, review, WF-11 validation and closure. Each test asserts that the operations it checked are exactly those the tag carries | A drift between the wire and the document with the document unchanged is invisible to the snapshot gates (M-7). TASK-054 left this undone for the execution operations (its F-5) |
| D-6 | **Each test runs on the host of the module it starts from**, as TASK-054 D-6: the gates, the WF-06 replay and rating pinning on `RiskTestHost`, which grants assessment, acceptance and reopen; the WF-07 replay, severity pinning and severity input on `ConcernTestHost`, which has the real WF-07 register, the severity mapping and the escalation routes. No new host. Tests that publish a matrix republish the host's own in a `finally`, as `ConcernSeverityTests` does, since a collection's tests share it. `RiskDriver.PublishMatrixAsync` takes optional English labels for the relabelling of D-2; its callers are unchanged | KISS: the module fixtures already seed what each test needs |
| D-7 | **The contract machinery is shared, not copied.** `DocumentedResponses` (the per-response check `ProjectContractTests` held as a local function) is extracted; that test now uses it. `OpenApiDocument` gains `SchemasReachedBy`, which `Surface` now uses, and `RequestPropertyNames` | DRY. The Project contract tests pass unchanged (§5 row 4) |
| D-8 | **The violations are proven, not assumed.** `artifacts/task-059/mutations.py` (local, not committed: `artifacts/` is ignored) applies each mutation of §5.1 to production code, builds, runs this suite beside the Risk and ManagementConcern suites, restores the source with a fresh mtime and rebuilds the clean source at the end | The row: each test must "fail if either invariant is violated" |
| D-9 | **CI.** One step in the `backend` job of `ci-quality-gates.yml`, after the ManagementConcern step, `--filter FullyQualifiedName~PMPlatform.Tests.Integration.RiskIssue`, on the same PostgreSQL service and test directory; about 8 s locally. It runs whenever the build passed (`if: ${{ !cancelled() && steps.build.outcome == 'success' }}`, the build step given `id: build`), even after an earlier test step failed, so the invariants' verdict is always in the log: the first validation run showed it skipped behind the ManagementConcern step, which fails on the same violation (§5.2). The Risk step's filter now ends in `.` (`…Integration.Risk.`): it matched `…Integration.RiskIssue` as well, and would have run this suite twice. The workflow runs on every pull request to `main`, `dev` and `stage` with no path filter, so every PR touching WF-06 or WF-07 runs it, and the `backend` job is a required check; `ci-cd-pipeline.yml` calls the same workflow | Acceptance criterion: "run in CI on every PR touching WF-06/WF-07" |

## 3. The suite

| Test | Guards | Fails when |
| --- | --- | --- |
| `RiskRatingPinningTests.EveryReadOfARecordedAssessmentServesTheRatingOfTheVersionItPinned` | Invariant 1, WF-06 | Any field of a recorded assessment, on any of the 4 reads or a command's answer, differs after a re-publication; the row's pin or rating row changes or the rating row is not the pinned version's; the request's claim is taken; a reassessment does not pin the version in force; a GET serving a rating is not read (D-2) |
| `ConcernSeverityPinningTests.EveryReadOfAConcernServesTheSeverityOfTheVersionItPinned` | Invariant 1, WF-07 | Any field of a concern or a materialised issue, on either read, differs after a re-publication; a row changes; a reassessment does not pin the version in force; a GET serving a concern is not read (D-2) |
| `SeverityInputTests.NoWf06OrWf07RequestCanCarryAComputedField` | Invariant 2, static | A request body of either API documents a severity, rating, overall impact or matrix version property (D-3 (a)) |
| `SeverityInputTests.EveryWriteThatRaisesOrChangesAConcernComputesItsSeverity` | Invariant 2, behavioural | After any of the 8 writes, a concern's severity answered, read or stored is not the computed one, or the escalation notifies another; a write is added and not sent a claim (D-3 (b)) |
| `RiskIssueContractTests` × 6 | Contract | Per tag: an R-10 break, a surface not equal to the snapshot or an operation count changed, or a lint finding beyond the platform's (D-4) |
| `RiskResponseContractTests.EveryRiskResponseIsWhatItsOperationDocuments`, `ConcernResponseContractTests.EveryManagementConcernResponseIsWhatItsOperationDocuments` | Contract | An answer has a property, JSON type, enum value or null its schema does not document, lacks a required property, or comes with an undocumented status; an operation of the tag is not called (D-5) |

## 4. Interfaces exercised

| Interaction | Through |
| --- | --- |
| WF-06 and WF-07 → FG-04 RISK_MATRIX (the pin) | Versions published through FG-04's author, reviewer and publisher; `IConfigurationResolver`; `master_data_config.risk_rating_definition` |
| WF-06 → WF-07 (edge 15) | `/risks/{id}/materialise` into WF-07's real register (`ConcernTestHost`) and `TestIssueRegister` (`RiskTestHost`) |
| WF-07 → WF-11 (edges 24, 28) | `submit-resolution`, the validator's decision and the outbox delivery |
| WF-07 → WF-15 (E-U4) | The `ManagementConcern.ConcernEscalated` outbox message and its `severity` parameter |
| The API's own description | `/openapi/v1.json`, against `docs/api/openapi.v1.json` and `contract-check.py` |

## 5. Verification

Run 2026-10-07 on macOS, Docker Desktop, PostgreSQL 17 (`AHDA-postgres`) and `AHDA-ldap`, with `NUGET_PACKAGES` set to `/Volumes/SanDisk/Bader/Development/Caches/nuget`. The branch is `dev` at d5fd54f plus this change; on `dev`, before any change, the Project, Risk and ManagementConcern subsets passed 150 of 150.

| # | Command | Result |
| --- | --- | --- |
| 1 | `dotnet build src/backend/PMPlatform.slnx -warnaserror`, and `--configuration Release -warnaserror` | 0 warnings, 0 errors, both |
| 2 | `dotnet test src/backend/PMPlatform.Tests.Unit --configuration Release` | 1210 passed (unchanged; no unit test is added) |
| 3 | `DB_CONNECTION_STRING=… dotnet test src/backend/PMPlatform.Tests.Integration --configuration Release` | 657 passed, 0 failed: 645 before, 12 new. Every existing test unchanged |
| 4 | The CI steps' commands, each `--configuration Release --filter FullyQualifiedName~PMPlatform.Tests.Integration.<X>`, for `RiskIssue`, `Risk.`, `ManagementConcern.` and `Project.` | 12 passed (about 8 s wall clock); 17 (the Risk suite alone, as before); 16; 117 (the shared helpers, D-7) |
| 5 | `python3 docs/architecture/contract-check.py --self-test` and the eight other `docs/architecture/*-check.py` gates | 29 mutations, 0 missed; all OK |

### 5.1 Mutation tests

Each mutation was applied to production code, the solution built, this suite run beside the Risk and ManagementConcern suites, and the source restored (D-8). The restored source then passed again (rows 1, 3, 4). Seven mutations, none survived this suite. "Also failed" names the existing tests that caught the same mutation, so the table shows where this suite is the only guard.

| # | Violation introduced | This suite failed | Also failed |
| --- | --- | --- | --- |
| M-1 | **The row's check: raise accepts a client-supplied severity**, through a `severityItemId` added to `ConcernCreateRequest` and applied over the computed one | `NoWf06OrWf07RequestCanCarryAComputedField` ("POST /api/v1/management-concerns: severityItemId"), `EveryWriteThatRaisesOrChangesAConcernComputesItsSeverity`, `EachRiskIssueSnapshotIsTheApiAsBuilt(ManagementConcern)` | `ConcernSeverityTests.AClientSuppliedSeverityIsIgnoredAndTheComputedOneStored` |
| M-2 | Raise accepts a client-supplied severity through `[JsonExtensionData]`, with no named request property | `EveryWriteThatRaisesOrChangesAConcernComputesItsSeverity` ("After POST /api/v1/management-concerns claiming CRITICAL, the severity is not the computed …"). Not the static half nor the snapshot gate: the extension data is not documented (D-3) | `ConcernSeverityTests.AClientSuppliedSeverityIsIgnoredAndTheComputedOneStored` |
| M-3 | A recorded rating keeps its pinned id and code but takes its label from the matrix version in force | `EveryReadOfARecordedAssessmentServesTheRatingOfTheVersionItPinned` ("GET /api/v1/risks/{riskId} no longer serves the assessment as recorded") | **None** |
| M-4 | A concern reassessment keeps the scale version its first assessment pinned | `EveryReadOfAConcernServesTheSeverityOfTheVersionItPinned` | `ConcernSeverityTests.ARepublishedScaleChangesNoRecordedSeverity` |
| M-5 | A Risk response property removed: `RiskDetail.acceptedUntil` hidden with `[JsonIgnore]` | `EachRiskIssueApiKeepsEveryPromiseOfTheSnapshot(Risk)`, `EachRiskIssueSnapshotIsTheApiAsBuilt(Risk)` | `RiskLifecycleTests.AnAcceptanceExpiresAndReturnsTheRiskForReview` |
| M-6 | A ManagementConcern response property renamed: `escalationNo` served as `escalationNumber` | `EachRiskIssueApiKeepsEveryPromiseOfTheSnapshot(ManagementConcern)`, `EachRiskIssueSnapshotIsTheApiAsBuilt(ManagementConcern)` | `ConcernEscalationTests.EachEscalationEventIsNotifiedOnce` |
| M-7 | Wire drift with the document unchanged: a risk with no issue sends `materialisedIssueIds` as `null` | `EveryRiskResponseIsWhatItsOperationDocuments` ("materialisedIssueIds: null, documented as not nullable", on every answer that carries a risk) | `RiskMaterialisationTests.ARefusedIssueLeavesTheRiskUnmaterialised` |

M-1 does not fail `EachRiskIssueApiKeepsEveryPromiseOfTheSnapshot`: an added optional request property is not an R-10 break. The as-built gate and the static half of invariant 2 are what catch it.

### 5.2 Validation in CI

The row's validation check was run in CI on 2026-10-07: draft PR #67 (never merged; closed and its branch deleted) committed M-1 — `POST /management-concerns` accepting a `severityItemId` and applying it over the computed severity — on top of this branch, then reverted it.

| Run | Commit | Result |
| --- | --- | --- |
| 37651705511 | `950452c`, the violation on `43d6511` | `backend` **failed** in "ManagementConcern integration tests" (`ConcernSeverityTests.AClientSuppliedSeverityIsIgnoredAndTheComputedOneStored`, 1 of 16). The new step was **skipped** behind it, so this suite never ran. This led to D-9's `if:` (`5fee994`) |
| 37652856562 | `c808a01`, the violation on `5fee994` | `backend` **failed** in both. ManagementConcern: 1 of 16. "Risk and issue domain contract and regression tests": 3 of 12 — `NoWf06OrWf07RequestCanCarryAComputedField` ("POST /api/v1/management-concerns: severityItemId"), `EveryWriteThatRaisesOrChangesAConcernComputesItsSeverity` ("After POST /api/v1/management-concerns claiming CRITICAL, the severity is not the computed …"), `EachRiskIssueSnapshotIsTheApiAsBuilt(ManagementConcern)`. Every earlier step passed |
| 37653933064 | `dbbdb80`, the revert (tree identical to `5fee994`) | `backend` passed: Risk 17, ManagementConcern 16, this step 12 of 12. `frontend` failed on the first attempt in `documents/detail/detail.test.tsx` (an axe `document-title` violation, a known flake; no frontend code changes here) and passed on rerun; all checks pass |
| 37652848213 | `5fee994`, this branch (PR #66) | All checks pass; Risk 17 (the narrowed filter), ManagementConcern 16, this step 12 |

## 6. Acceptance criteria and deliverables

| Item | Result |
| --- | --- |
| Both invariant tests exist | **MET.** Invariant 1: `RiskRatingPinningTests`, `ConcernSeverityPinningTests` (D-1, D-2). Invariant 2: `SeverityInputTests` (D-3) |
| They run in CI on every PR touching WF-06/WF-07 | **MET** (D-9) |
| They fail if either invariant is violated | **MET.** §5.1 M-1 to M-4; M-1 is the row's validation check |
| Contract tests catch a breaking schema change in either domain's API | **MET.** M-5 (Risk) and M-6 (ManagementConcern) by the R-10 gate; M-7, a drift the document does not show, by the replay |
| Deliverables: contract test suite, invariant regression tests, CI wiring | **MET** (header row "Deliverables") |
| Validation: run the suite in CI and confirm green; temporarily accept client-supplied Severity in a test branch and confirm the regression test fails, then revert | **MET** (§5.2): green on this branch; with the violation, 3 of the suite's 12 tests failed in CI; green again after the revert |

## 7. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **"Never input" rests on unknown request properties being ignored** (`management-concern.md` D-4). M-2 shows a path no document records: a request model that binds extension data. The behavioural half catches it only for the field names it claims (`severityItemId`, `severity`, `severityConfigurationVersionId`, `overallImpactLevel`, `rating`, `ratingCode`, `riskRatingDefinitionId`, `matrixConfigurationVersionId`). Refusing unknown properties on these request models (`JsonUnmappedMemberHandling.Disallow`, 400) would make the invariant structural; it changes the API's answer to an extra field, so it is an R-11 decision, not this task's | Engineering Architect | A severity taken under another name, without a documented property, would pass every gate |
| F-2 | **WF-07 serves a concern's current severity only**: its assessment history is in the audit trail (`management-concern.md` F-9). Invariant 1 is therefore proven for the severity as recorded now and for a reassessment's new pin, not for earlier severities, which no read serves | Engineering Architect | — |
| F-3 | **The replay checks success answers only.** The document declares no 4xx answer and no `default` (the platform's open C-5 finding, `project-lifecycle-contract-tests.md` F-1), so a refusal's R-23 envelope cannot be checked against it; `DocumentedResponses` fails on any undocumented status | TASK-009 owner | A refusal that departs from the envelope is caught only by the modules' own tests |
| F-4 | **One command stands for all after a re-publication** (D-2): `monitor`. The ten risk commands and the four reads share one representation (`RiskViews.DetailsAsync`), so a command that bypassed it would be missed | WF-06 owner | A future command with its own projection must add itself to `RiskRatingPinningTests` |
| F-5 | **The rating row's version is checked for API writes only.** The database does not hold that a rating row belongs to the version pinned beside it (`risk-management.md` F-11); D-2 asserts it for every assessment the test writes | Engineering Architect | A hand-written row could pair a version with another version's rating |

## 8. Change log

| Date | Change |
| --- | --- |
| 2026-10-07 | Created (TASK-059) |

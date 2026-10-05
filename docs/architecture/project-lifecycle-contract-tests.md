# Project Lifecycle Contract and Integration Tests (WF-01)

| Field | Value |
| --- | --- |
| Task | TASK-043 — Contract & Integration Tests for Project Lifecycle |
| Depends on | TASK-041 — the WF-01 API and lifecycle (`project-registration.md` §3–§5). TASK-042 — the register and creation UI (`project-registration-ui.md`), whose request shapes the snapshot now pins. TASK-009 — the API conventions (`api-conventions.md`): R-10 defines a breaking change, R-11 the snapshot, §9 row 5 the contract test |
| Record date | 2026-10-02 |
| Status | **BUILT AND VERIFIED LOCALLY** against `AHDA-postgres` and `AHDA-ldap` (§5), and wired into the `backend` job of `ci-quality-gates` (D-12). The Project API's generated document now describes its wire format (D-6) |
| Branch | `test/task-043-wf01-lifecycle-contract-tests` |
| Deliverables | **Contract test suite**: `ProjectContractTests` (4), `OpenApiBreakingChangeTests` (17), `OpenApiBreakingChanges`, `OpenApiDocument`, and the snapshot `docs/api/openapi.v1.json`. **E2e lifecycle suite**: `ProjectLifecycleEndToEndTests` (9). **CI wiring**: step "Project lifecycle and contract tests" in `.github/workflows/ci-quality-gates.yml`. All under `src/backend/PMPlatform.Tests.Integration/Project`. Supporting: the OpenAPI document fix (D-6), C-13 reading OpenAPI 3.1 nullable types (D-7), `CONTRIBUTING.md`, this record |
| Environment variables / secrets | None. `UPDATE_OPENAPI_SNAPSHOT=1` is a developer switch that rewrites the snapshot (D-2) |
| Sources read | The TASK-043 row as supplied on 2026-10-02 (description, acceptance criteria, directory, deliverables); `api-conventions.md` R-10, R-11, R-16, R-19, R-47, R-52 to R-55, §8, §9 row 5; `project-registration.md`; `approval-framework.md` D-6; `event-conventions.md` §4; `contract-check.py`. "TASK for API conventions" in the row is TASK-009 (`api-conventions.md` §9 row 5) |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | Contract tests of the Project API against the TASK-009 conventions and the committed snapshot | This task |
| **In** | The Project lifecycle end to end across WF-01, WF-11 (approval) and FG-06 (audit), including refused transitions | This task |
| **In** | Running both in CI on every pull request | This task |
| **In** | Making the generated document state what the API sends (D-6): a contract test over a document that misstates the wire format checks nothing | This task |
| **Out** | Emitting R-52 extensions, `default` responses, response headers and examples, which every module lacks | Platform (F-1) |
| **Out** | Contract tests of the other modules (TASK-054, TASK-059, TASK-065) | Those tasks (F-4) |
| **Out** | ACTIVE ↔ SUSPENDED, COMPLETED → CLOSED | TASK-062, TASK-063 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **"Contract test" is `api-conventions.md` §9 row 5**: (a) the TASK-009 lint over the generated document, (b) a schema diff against the committed snapshot that fails on any R-10 break, (c) example replay. (a) is `TheProjectApiFollowsTheConventions`, (b) is `TheProjectApiKeepsEveryPromiseOfTheSnapshot`. (c) cannot run as written, because the document has no examples and there is no DEV environment (F-2). `EveryProjectResponseIsWhatItsOperationDocuments` replays every Project operation in process instead | The row names the definition. The substitute checks what (c) exists to check: that responses match the document |
| D-2 | **The snapshot is the whole generated v1 document** at `docs/api/openapi.v1.json`, the path R-11 names, without `servers` (the host is not part of the contract). Only operations tagged `Project` are diffed (F-4). It is rewritten by running the Project contract tests with `UPDATE_OPENAPI_SNAPSHOT=1`, and committed with the change | R-11: "updated deliberately in the same PR as the break, which is what makes a break visible". A file under review shows a break in the diff |
| D-3 | **Two gates on the snapshot.** The R-10 diff fails on a break. `TheSnapshotIsTheProjectApiAsBuilt` fails when the Project surface (its operations and every schema they reach) is not identical to the build. Without the second gate, a property added without a snapshot update could later be removed without a failure | Acceptance criterion 1 asks for failure on any break. Only what the snapshot records can be protected |
| D-4 | **R-10, checked mechanically, direction-aware** (`OpenApiBreakingChanges`). Breaks: an operation or status code removed; a response property removed or no longer required; an enum value removed; a type, format or nullability changed; a parameter or request body that becomes required, or a required request property added; validation tightened on a request value (pattern added or changed, enum introduced, maximum lowered, minimum raised). Additions do not break. `$ref` and the 3.1 nullable forms (`type: [null, …]`, `oneOf` with `{type: null}`) are normalized first. A named schema is compared once per direction, so a break in a shared schema is reported once, at the first operation that reaches it | R-10's list. A removed request property is not on it: the server ignores unknown properties |
| D-5 | **The diff is itself tested.** `OpenApiBreakingChangeTests` applies 11 breaking and 5 non-breaking changes to a copy of the snapshot. Each break must be reported and each addition must not be, including a change to another module's operation | A diff that reports nothing would otherwise pass the gate |
| D-6 | **The generated document describes the wire.** The OpenAPI generator reads the minimal-API `JsonOptions`, not MVC's, so it never saw the R-19 enum converter or the R-16 money converter. Lifecycle states appeared as `type: integer` with no values, and `Money` as `{amount: number}`. Without this fix, removing or renaming a state could not be detected. `Program.cs` now gives both option sets the same converters (`AddWireConverters`), and `MoneySarSchemaTransformer` declares `Money`, and request `…Sar` strings read with `MoneySar.Parse`, as the R-16 pattern. The change affects the document only: no endpoint reads the minimal-API options (no `Map*`, `Results.*` or `*JsonAsync` in the API), and `ApiProblem` has its own | Acceptance criterion 1. Mutation M-6 confirms it (§5.1) |
| D-7 | **The lint is invoked, not reimplemented.** The test writes the generated document to the test output directory, runs `contract-check.py` on it, and fails on any finding about a Project operation or schema except the open classes F-1 lists, each an exact message pattern. C-13 now treats the OpenAPI 3.1 nullable string form, `type: ["null", "string"]`, as a string, which it is. The self-test still detects all 29 mutations | One copy of the conventions. A new finding class on the Project surface still fails |
| D-8 | **Responses are checked against the document.** Every Project operation is called once: create, read, edit, submit, withdraw, resubmit, start review, approve, activate, list, delete. Each response body is validated against the schema documented for its status: JSON type, enum membership, required properties present, and no undocumented property. The test also asserts that the operations it called are exactly the Project operations in the document, so a new operation fails until it is covered | §9 row 5 (c)'s intent (F-2). D-6 was found this way |
| D-9 | **The e2e suite goes through the public API.** Registration, review start and activation use `/projects`. The review is decided through `/approval-tasks/{id}/approve` and `/return`, and read through `/approval-instances`. Only the outbox dispatch runs in process, as its worker would; the worker is idle in tests so each test controls the timing. FG-06 is read from `audit_activity`, because there is no audit query API yet (F-7) | TASK-041's tests decide reviews through `IApprovalWorkflowService`. This suite also covers the approval API, its authorization, and the outcome delivery between modules |
| D-10 | **Every transition is exercised, and the test proves it.** The happy path goes DRAFT → SUBMITTED → DRAFT → SUBMITTED → UNDER_REVIEW → RETURNED → SUBMITTED (revision 2) → UNDER_REVIEW → APPROVED_PLANNED → ACTIVE. It asserts that the set of `status` changes in the project's audit trail equals `ProjectLifecycle.Transitions`, read from the state machine itself (`InternalsVisibleTo` added to Application). When TASK-062 or TASK-063 add an edge, this test fails until a test takes it | Acceptance criterion 3, enforced by the test rather than maintained by hand |
| D-11 | **Each refused transition is checked four ways**: the status R-47 gives (403 for a project the caller can see, 404 for one they cannot); the project row unchanged to the byte; no successful event added to the trail; and exactly one `AUTHORIZATION_DENIAL` audit event, found by the request's correlation id, naming the caller (CTL-25). Eight cases (§4): six against project commands and two against the WF-11 decision | Acceptance criterion 2 asks for at least 3. Each case has a different reason for refusal |
| D-12 | **CI**: a step in the `backend` job of `ci-quality-gates.yml`, after the Identity tests, which uses the same PostgreSQL service and test directory, with `--filter FullyQualifiedName~PMPlatform.Tests.Integration.Project`. The job is a required check on the protected branches, and `ci-cd-pipeline.yml` calls the same workflow, so the gate on pull requests is also the gate on promotion. The suite runs in about 7 s; the job previously took about 3 minutes against its 10-minute budget | Acceptance criterion 1: "runs in CI and fails on any breaking API change" |

## 3. The contract suite

| Test | Fails when |
| --- | --- |
| `TheProjectApiKeepsEveryPromiseOfTheSnapshot` | The build breaks a promise the snapshot makes (D-4). The message lists each break and repeats R-11's procedure |
| `TheSnapshotIsTheProjectApiAsBuilt` | The Project surface differs from the snapshot in any way (D-3), or the number of Project operations is not 9 |
| `TheProjectApiFollowsTheConventions` | `contract-check.py` reports a finding on the Project surface outside F-1's classes (D-7) |
| `EveryProjectResponseIsWhatItsOperationDocuments` | A response differs from its documented schema, or a Project operation is not called (D-8) |
| `OpenApiBreakingChangeTests` (16 + 1) | The diff misses one of 11 kinds of break, reports one of 5 kinds of addition, or reports a break of the snapshot against itself (D-5) |

## 4. The e2e lifecycle suite

| Test | Covers |
| --- | --- |
| `ADraftReachesActiveThroughEveryTransitionAndEachIsAudited` | The happy path of D-10. WF-01: each command's resulting state, the revision, and the Formal Project ID issued only on approval. WF-11: two runs, one per revision, the second linked to the first, each with one task decided by local.r02. FG-06: the project's ten events in order, with class, outcome, actor and state change; the audit hash chain intact; every edge of `ProjectLifecycle` taken |
| `AnUnauthorizedTransitionIsRefusedAndChangesNothing` × 6 | local.r06 (no project permission) submits a DRAFT: 403. local.r03 (AHDA reviewer, no `PROJECT_ACTIVATE`) activates: 403. local.r08 (entity user, granted review) starts a review: 403. local.r08 (granted activation) activates: 403 (ADR-013 gate). local.r03 starts a review in a department they do not manage: 404. local.r08 withdraws another entity's submission: 404 |
| `OnlyTheRoutedApproverDecidesTheReview` × 2 | local.r02 started the review, so as requester cannot approve it: 403 (TASK-035 D-6). local.r08 approves the review of their own project: 403. In both cases the project stays UNDER_REVIEW with no identifier and the run stays PENDING |

These are in addition to TASK-041's lifecycle tests (`RegistrationLifecycleTests`, `LifecycleGuardTests`, `ProjectAuthorizationTests`), which still pass unchanged.

## 5. Verification

Run 2026-10-02 on macOS, Docker Desktop, PostgreSQL 17 (`AHDA-postgres`) and `AHDA-ldap`.

| # | Command | Result |
| --- | --- | --- |
| 1 | `dotnet build src/backend -warnaserror`, and `--configuration Release -warnaserror` | 0 warnings, 0 errors, both |
| 2 | `dotnet test src/backend/PMPlatform.Tests.Unit` | 686 passed |
| 3 | `DB_CONNECTION_STRING=… dotnet test src/backend/PMPlatform.Tests.Integration` | 490 passed, 30 new: `ProjectContractTests` 4, `OpenApiBreakingChangeTests` 17, `ProjectLifecycleEndToEndTests` 9. Every existing test unchanged |
| 4 | The CI step's command: `… --configuration Release --filter FullyQualifiedName~PMPlatform.Tests.Integration.Project` | 117 passed, about 6 s |
| 5 | `python3 docs/architecture/contract-check.py --self-test`, and the sample document | 29 mutations, 0 missed; sample 0 findings |
| 6 | `python3 docs/architecture/contract-check.py docs/api/openapi.v1.json` | 121 operations, 544 findings (546 before D-6 and D-7). On the Project surface: 39, all in F-1's classes; C-13 on Project, 0 (2 before) |
| 7 | The eight other `docs/architecture/*-check.py` gates | All OK |

### 5.1 Mutation tests

Each mutation was applied to production code, the solution rebuilt, the Project tests run, and the source restored. The restored source then passed again (row 4).

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | A response field removed: `ProjectDetail.FormalProjectId` hidden with `[JsonIgnore]` | `TheProjectApiKeepsEveryPromiseOfTheSnapshot`, `TheSnapshotIsTheProjectApiAsBuilt`, `ADraftReachesActiveThroughEveryTransitionAndEachIsAudited`, `AnEntityDraftIsApprovedByAhdaAndBecomesActiveOnlyByTheCommand` |
| M-2 | A field's type changed: `ProjectDetail.RevisionNo` `int` → `long` | `TheProjectApiKeepsEveryPromiseOfTheSnapshot`, `TheSnapshotIsTheProjectApiAsBuilt` |
| M-3 | A status code changed: `CreateProject` documented as 200 | `TheProjectApiKeepsEveryPromiseOfTheSnapshot`, `TheSnapshotIsTheProjectApiAsBuilt`, `EveryProjectResponseIsWhatItsOperationDocuments` |
| M-4 | A lifecycle edge, ACTIVE → SUSPENDED, added to `ProjectLifecycle` with no test that takes it | `ADraftReachesActiveThroughEveryTransitionAndEachIsAudited` |
| M-5 | Activation authorized by `PROJECT_VIEW`, in both the endpoint attribute and the service's gate | `AnUnauthorizedTransitionIsRefusedAndChangesNothing(3, activate, …)`, `AnExternalUserNeverStartsAReviewNorActivatesEvenWithTheGrant`. Weakening the endpoint attribute alone failed no test, because the service checks `PROJECT_ACTIVATE` on the record itself, so that mutation changed no behavior |
| M-6 | D-6 reverted: the generator not given the wire converters | `TheProjectApiKeepsEveryPromiseOfTheSnapshot`, `TheSnapshotIsTheProjectApiAsBuilt`, `EveryProjectResponseIsWhatItsOperationDocuments` |

## 6. Acceptance criteria and deliverables

| Item | Result |
| --- | --- |
| The contract test suite runs in CI and fails on any breaking API change (removed field, changed type) | **MET** for every break a document can show. D-2 to D-6, D-12; M-1, M-2, M-3, M-6; `OpenApiBreakingChangeTests`. A change of meaning and the removal of an undocumented error status cannot be seen in the document (F-1, F-5) |
| The e2e suite covers at least the happy path and 3 negative-authorization cases | **MET.** One happy path and 8 refused transitions (§4); M-5 |
| Every state transition in the Project lifecycle is exercised by at least one automated test | **MET**, and checked by a test: D-10; M-4. All 7 edges of `ProjectLifecycle` and the creation of the draft |
| Description: validated against the TASK-009 OpenAPI conventions, and end to end across WF-01, WF-11 and FG-06 including unauthorized transitions | **MET.** D-1, D-7, D-9, D-11 |
| Deliverables: contract test suite, e2e lifecycle test suite, CI wiring | **MET** (header row "Deliverables") |

## 7. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **The lint findings no module can clear yet.** No API operation emits the R-52 extensions (`x-module`, `x-write-class`), a `default` response, its 4xx responses, or its `X-Correlation-Id`, `Location` and `ETag` headers. On the Project surface these are 39 findings: C-4 9, C-5 9, C-7 10, C-8 1, C-12 10. The test accepts exactly these message patterns. A document transformer that writes them from the C# attributes (R-52: "emitted from a C# attribute") would clear them for every module | Engineering Architect (platform) | Error responses are not documented, so removing or renumbering an error status is an R-10 break the diff cannot see |
| F-2 | **Example replay (§9 row 5 (c)) cannot run**: the document has no examples (R-55), and no DEV environment exists (`ci-cd-pipeline.yml` preflight is not ready). D-8 replays every Project operation in process instead | Platform; TASK-018 for DEV | Nothing checks examples when they are added |
| F-3 | **MasterDataConfig sends amounts as JSON numbers**: `minAmountSar`, `assignmentMinBudgetSar` and `costThresholdSar` in its entry and request schemas (6 C-13 findings, R-16) | TASK-034 owner | Each is a `decimal` that a JavaScript client may round. Fixing it changes their type, which is an R-10 break of that API |
| F-4 | **Only the Project tag is diffed.** The snapshot holds the whole v1 document, but other modules' changes are not gated until their contract tasks (TASK-054, TASK-059, TASK-065) add their tags, which takes one call each | Those tasks | Other modules' entries in the snapshot can go stale, and a break in them is not caught |
| F-5 | **A change of meaning does not show in the document** (R-10: "changing the meaning of a property without renaming it") | Review, `api-conventions.md` §8.3 | It is caught only in review |
| F-6 | **The e2e suite uses test grants for AHDA's side**, as TASK-041's tests do (`project-registration.md` F-1). No role ships `PROJECT_REVIEW` or `PROJECT_ACTIVATE` | PMO (Appendix A); TASK-110 | The suite proves the lifecycle under the fixture's grants, not those of a real environment |
| F-7 | **FG-06 has no query API**, so the e2e suite reads `audit_activity.audit_event` directly | The audit viewer task | When the API exists, the e2e suite should read the trail through it |

## 8. Change log

| Date | Change |
| --- | --- |
| 2026-10-02 | Created (TASK-043) |
| 2026-10-05 | TASK-054 (`execution-domain-contract-tests.md`): F-4 resolved for the Progress, Schedule, ProjectTask, Milestone and FinancialKpi tags, which `ExecutionContractTests` now diffs, compares as built and lints. `BuiltAsync`, the lint run and `OpenPlatformFindings` moved from `ProjectContractTests` to `OpenApiDocument` (that record's D-7); the Project tests are unchanged in what they assert. F-4 stands for the tags not yet gated (TASK-059, TASK-065) |

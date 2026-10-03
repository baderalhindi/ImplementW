# Project Creation & Registration (WF-01 Backend)

| Field | Value |
| --- | --- |
| Task | TASK-041 — Build Project Creation & Registration (WF-01 Backend) (P7 - Project Establishment) |
| Depends on | TASK-025 — Implement Core Platform Schema (`core-platform-schema.md`): `project.project`, its unique `formal_project_id` and the APPROVED_PLANNED-onwards check. TASK-035 — Build Shared Approval Framework (`approval-framework.md`): the review is a WF-11 run started through `IApprovalRequests`, and its outcome reaches Project through `IApprovalOutcomeHandler` |
| Record date | 2026-10-02 |
| Status | **BUILT AND VERIFIED LOCALLY** against `AHDA-postgres` and `AHDA-ldap`, in process, through the real API pipeline and on the Compose stack (§7). In a fresh environment no one can review or activate a project (F-1), no review can start until AHDA publishes a `PROJECT_REGISTRATION` route (F-2), and no draft can be created until PROJECT_CLASSIFICATION has a published item (F-10) |
| Branch | `feat/task-041-wf01-project-registration-backend` |
| Deliverables | **WF-01 Project aggregate service**: `IProjectService` (`ProjectService`) and `ProjectsController`, 10 operations (§4). **Lifecycle state machine**: `ProjectLifecycle` (7 edges) and migration `TASK-041_GuardProjectLifecycle`, which refuses every other change in the database. **Approval integration**: the review starts a WF-11 run on `PROJECT_REGISTRATION`; `ProjectApprovalOutcomeHandler` applies its outcome and issues the Formal Project ID. **Tests**: 86 unit, 85 integration, 5 mutations (§7). Supporting: `IProjectRepository` and `ProjectRepository`, four permissions with ADR-013's grants, `IOrganizationDirectory` (E-U1), the R-16 money converter |
| Environment variables / secrets | None |
| Gate decision applied | **ADR-013 (changed): external entity users may create a project DRAFT for their own entity; AHDA reviews and approves before a Formal Project ID is issued; Project Managers are no longer resolved from the AHDA directory alone** — D-3, D-5, D-9, D-10 |
| Participation amendment | The same ADR-013 change, and **ADR-014: WF-01 gains a legacy intake path; a project already under way enters with a Declared Baseline, structurally distinguishable from an Approved Baseline wherever it is displayed** — D-14 (the intake path itself is TASK-104's, F-9) |
| Sources read | The TASK-041 row as supplied on 2026-10-02 (description, acceptance criteria, deliverables, gate decision, participation amendment); the workbook itself was not opened. ERD §5.4, §6 row 8, §7 rows 18–20, E-3, E-7; `erd.dbml` `project.project`; `core-platform-schema.md` N-1, O-1; `solution-architecture.md` §4.5, M-1 to M-11, §8.2 edges 7, 8, 20, 28, 35, §9, §11.1; `approval-framework.md` §5; `api-conventions.md` R-4, R-5, R-16, R-19 to R-21, R-35, R-40, R-47, §4.4; `event-conventions.md` EV-4, EV-5; `docs/governance/ptbc-themes.csv` PTBC-002; ADR-013 (`adrs/ADR-register.md`) |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | The Project master aggregate's early lifecycle: DRAFT → SUBMITTED → UNDER_REVIEW → RETURNED / APPROVED_PLANNED, and the Planned → Active command | This task |
| **In** | Review through WF-11: the run, its routing inputs, the outcome handler, the Formal Project ID issued on approval | This task |
| **In** | ADR-013 as tested rules: entity drafts, AHDA-only gates, an employer-neutral Project Manager | This task |
| **In** | The rule that nothing activates or completes a project implicitly — no date, no progress, no worker — in the application and in the database | This task |
| **Out** | SCR-025/026 and MOD-001 | TASK-042 |
| **Out** | Contract and integration tests across the lifecycle (TASK-043) | TASK-043 |
| **Out** | The legacy intake path, its Declared Baseline and `ProjectIntakeRecorded` (ADR-014) | TASK-104 (F-9) |
| **Out** | Governance profile assignment by rule and its override (O-1) | TASK-105 (F-8) |
| **Out** | ACTIVE ↔ SUSPENDED, COMPLETED → CLOSED | TASK-062, TASK-063 (§6) |
| **Out** | A fast-track past UNDER_REVIEW | PTBC-002, Open (F-6) |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Module shape.** `Features/Project`: `Contracts` (`IProjectService`, the DTOs, `ProjectErrorCodes`, `ProjectApprovalRouting`, `Events`), `ProjectService`, `ProjectLifecycle`, `ProjectAccess`, `ProjectReferences`, `ProjectManagerEligibility`, `ProjectAudit`, `ProjectMapping`, the repository port, and `EventHandlers/ProjectApprovalOutcomeHandler`. The entity is TASK-025's `Domain/Project/Project`; the repository is `Infrastructure/Persistence/Project`. Project references only Approval's and IdentityAccess's and MasterDataConfig's contracts (§8.2 edge 20, E-U1, E-U2); A-1 to A-6 pass unchanged | ADR-003 §4.3 row 1, §6 |
| D-2 | **The state machine has seven edges, each with one cause** (§3). No edge enters APPROVED_PLANNED but from UNDER_REVIEW, so no project skips review. No edge enters ACTIVE but the activation command's, and none leaves it: TASK-062 and TASK-063 add their own. A command not allowed from the project's state is 409 `INVALID_TRANSITION`; from CLOSED, 409 `TERMINAL_STATE` | Acceptance criteria 2 and 3; api-conventions R-4 ("one command exists per transition") |
| D-3 | **The review is the WF-11 run, and it starts when AHDA starts the review**, not on submission. `start-review` moves SUBMITTED → UNDER_REVIEW and calls `IApprovalRequests.StartAsync` in the same transaction: subject `Project`/`Project`/id/`revision_no`, routing key `PROJECT_REGISTRATION`, scope anchors the project and its department, routing inputs the governance profile and the registration budget. The requester is the AHDA reviewer who started it, so the framework's own rule keeps them from deciding it (F-11). `approval-framework.md` §5 suggests starting the run on submission; that would let an approval arrive while the project is still SUBMITTED, which either skips UNDER_REVIEW or leaves an outcome nowhere to go | Acceptance criterion 3; ADR-013 "AHDA reviews and approves"; M-11 (one command, one transaction) |
| D-4 | **The database holds the machine too.** Trigger `project.guard_project` (migration `TASK-041_GuardProjectLifecycle`) refuses, whoever writes: a change of `lifecycle_state` that is not one of the seven edges; any change to an issued `formal_project_id`, or its issue outside UNDER_REVIEW → APPROVED_PLANNED; any change to a set `legacy_intake_date`; `activated_at` set other than by APPROVED_PLANNED → ACTIVE, ACTIVE without it, or a change to it once set; `revision_no` moving other than by one on RETURNED → SUBMITTED; deleting a project that is not DRAFT. An INSERT is not a transition and is not guarded (TASK-104 inserts intake projects; fixtures insert rows in any state). TASK-062 and TASK-063 add their edges by replacing the function | "Only reachable through the documented command, never implicitly" holds for any writer, not just this service |
| D-5 | **One authoritative Formal Project ID.** Issued once, by the handler, when the review's outcome is APPROVED: `PRJ-` and the next value of sequence `project.formal_project_id_seq`, six digits or more (F-3). TASK-025's unique index refuses a second project with the same identifier, its check refuses APPROVED_PLANNED or later without one, and D-4 refuses any change to it. The API takes no identifier from a client: `formalProjectId` is not in the request and an unknown property is ignored | Acceptance criterion 1; ADR-013 "before a Formal Project ID is issued" |
| D-6 | **Applying the outcome.** APPROVED → APPROVED_PLANNED with the identifier; RETURNED, REJECTED and WITHDRAWN → RETURNED, from which the registrant edits and resubmits as revision + 1, which a new run linked to the last reviews (TASK-035 D-9). An outcome applies only to the revision under review while the project is UNDER_REVIEW; applying it leaves that state, so the same outcome again, or one for an older revision, changes nothing and is audited `ApprovalOutcomeIgnored` (EV-4, EV-5). The handler runs inside the outbox dispatch transaction, so the change commits with the delivery mark or not at all; `updated_by` is the decision's author | `approval-framework.md` §5; the workbook names RETURNED and APPROVED_PLANNED as review's only ends (F-4) |
| D-7 | **Activation is a command and nothing else.** `activate` moves APPROVED_PLANNED → ACTIVE and stamps `activated_at` with the command time. No worker, query or date reads a project's state to change it; no progress figure reaches this module. A test moves the clock 800 days past the planned dates and runs every scheduled worker twice: the row is byte-for-byte unchanged | Description: "100% progress or a date reached never auto-completes or auto-activates a Project"; ERD §7 row 20 |
| D-8 | **What a registration names is usable when written.** Required at create: title, classification, owning department, participation mode, governance profile (F-8). Each master data reference must be a PUBLISHED item of its catalogue, the department active and the entity ACTIVE (`IMasterDataResolver`, `IOrganizationDirectory`); a city whose item names a parent region may not be filed with another region (core-platform-schema N-1 (b)); an ENTITY_MANAGED project names its entity. Submission checks all of it again and requires the registration budget and both planned dates (F-13). Shape is the API's: R-16 money strings (a converter now writes every `Money` as one), end date not before start, latitude and longitude in range | T-3 (shape at the edge, meaning in the module); ADR-013; ERD D-5 |
| D-9 | **The Project Manager is an R04 holder, not a directory entry** (ADR-013 change). Named at submission (the ERD keeps it null while DRAFT, so withdrawing clears it). Eligible is whoever `IRoleHolderDirectory` finds holding R04 over the project's anchors now: an internal holder, or an external holder of the delivering entity itself, while that entity is ACTIVE. Anyone else is 422 `PROJECT_MANAGER_INVALID` | ADR-013: R04 is employer-neutral; "Project Managers are no longer resolved from the AHDA directory alone" |
| D-10 | **Four permissions; only the amendment's grants ship.** `PROJECT_VIEW` (Read), `PROJECT_REGISTER` (Write: create, edit, submit, withdraw, delete), `PROJECT_REVIEW` and `PROJECT_ACTIVATE` (Write), group `PROJECT`. R04 and R08 hold `PROJECT_VIEW` and `PROJECT_REGISTER` at ENTITY: ADR-013's amendment lets external entity users create a draft for their own entity and lets entities see their own projects; ENTITY reaches only a holder with an entity, and the engine's cross-entity isolation keeps them to it. The AHDA gates ship to no one (F-1). `start-review` and `activate` refuse an external user even when the engine allows them, and audit `LifecycleGateRefused`: the check is on the person, as approval authority's is (TASK-035 D-6) | Only source-backed grants ship (`authorization-engine.md` D-9); ADR-013 "AHDA retains every approval and lifecycle gate" |
| D-11 | **HARD_DRAFT.** A DRAFT is deleted by the person who created it (ERD §5.4 "by its owner"), with a `ProjectDeleted` audit event; a project referenced elsewhere (a document, an assignment, approval history) is 409 `PROJECT_IN_USE`. `DELETE` of a project that is not there, or not the caller's to see, is 204 (R-40), which also reveals nothing. A project's audit events carry `scope_project_id` only once it has entered review (F-5) | ERD delete policy; R-40, R-47 |
| D-12 | **Editing.** `PUT` replaces the registration of a DRAFT or RETURNED project under `If-Match` (R-21); any other state is 409 `PROJECT_NOT_EDITABLE`. Moving a project to another department or entity needs the permission there too, so an entity user cannot move a draft out of their entity. `xmin` is the project's concurrency token now (ERD D-16), a model change that emits no DDL | R-5, R-21; ADR-013 isolation |
| D-13 | **Every change is audited** through `IAuditTrail` in the saving transaction (event-conventions §4 row 16): `ProjectCreated`, `ProjectChanged`, `ProjectDeleted` (DATA_CHANGE, with each changed field; title and description withheld); `ProjectSubmitted`, `SubmissionWithdrawn`, `ReviewStarted`, `RegistrationReturned`, `RegistrationApproved`, `ProjectActivated` (LIFECYCLE_TRANSITION, with the state change and revision); `ApprovalOutcomeIgnored` (FAILED); `LifecycleGateRefused` (AUTHORIZATION_DENIAL, recorded on its own) | event-conventions EV-9; CTL-25 |
| D-14 | **ADR-014 here is a marker, guarded and shown.** `legacyIntakeDate` is on every representation, null for a registered project; D-4 makes it set-once. The intake command, the Declared Baseline and `ProjectIntakeRecorded` are TASK-104's (F-9) | ERD §5.4 "one nullable column is both the flag and the date"; `solution-architecture.md` §11.1 |
| D-15 | **`IOrganizationDirectory`** in IdentityAccess's contracts answers whether a department or entity is active without the caller holding ADM-011–013's permissions | E-U1; a registrant names a department they cannot administer |

## 3. The state machine

| From | To | Cause | Who | Writes |
| --- | --- | --- | --- | --- |
| — | DRAFT | `POST /projects` | `PROJECT_REGISTER` on the new anchors | revision 1, no identifier, no manager |
| DRAFT | SUBMITTED | `submit` | `PROJECT_REGISTER` | the Project Manager |
| SUBMITTED | DRAFT | `withdraw` | `PROJECT_REGISTER` | clears the manager |
| SUBMITTED | UNDER_REVIEW | `start-review` | `PROJECT_REVIEW`, internal only | the WF-11 run |
| UNDER_REVIEW | RETURNED | outcome RETURNED, REJECTED or WITHDRAWN | the review's decider, or its requester withdrawing | — |
| UNDER_REVIEW | APPROVED_PLANNED | outcome APPROVED | the review's last approver | the Formal Project ID |
| RETURNED | SUBMITTED | `submit` | `PROJECT_REGISTER` | revision + 1, the manager |
| APPROVED_PLANNED | ACTIVE | `activate` | `PROJECT_ACTIVATE`, internal only | `activated_at` |

## 4. Endpoints

All paths are under `/api/v1`, tag `Project`, operation ids `Project_*`. Every non-2xx answer is the R-23 envelope; every `POST` and `PUT` also answers 400 `IDEMPOTENCY_KEY_REQUIRED`/`…_INVALID`; a project outside the caller's scope is 404 (R-47).

| Operation | Method and path | Permission | Success | Refusals beyond 401/403/404 |
| --- | --- | --- | --- | --- |
| `ListProjects` | `GET /projects?status=&departmentId=&externalEntityId=&projectManagerUserId=&q=&page=&pageSize=` | `PROJECT_VIEW` | 200 `ProjectPage`, only projects the caller reaches, most recently changed first; `q` matches the title or the Formal Project ID; `projectManagerUserId` serves SCR-026 My Projects (I-04) and narrows the caller's scope, never widens it | 400 |
| `GetProject` | `GET /projects/{id}` | `PROJECT_VIEW` | 200 `ProjectDetail`, `ETag` | — |
| `CreateProject` | `POST /projects` | `PROJECT_REGISTER` | 201, `Location`, `ETag`; DRAFT | 400; 422 `PROJECT_REFERENCE_INVALID`, `PROJECT_PARTICIPATION_INVALID` |
| `UpdateProject` | `PUT /projects/{id}`, `If-Match` | `PROJECT_REGISTER` | 200 | 400; 409 `PROJECT_NOT_EDITABLE`; 412; 422 as create; 428 |
| `DeleteProject` | `DELETE /projects/{id}` | `PROJECT_REGISTER`, the creator | 204 | 409 `PROJECT_NOT_EDITABLE`, `PROJECT_IN_USE`; 412 |
| `SubmitProject` | `POST /projects/{id}/submit` `{projectManagerUserId}` | `PROJECT_REGISTER` | 200 | 400; 409 `INVALID_TRANSITION`; 412; 422 as create, `PROJECT_INCOMPLETE`, `PROJECT_MANAGER_INVALID` |
| `WithdrawProject` | `POST /projects/{id}/withdraw` | `PROJECT_REGISTER` | 200 | 409 `INVALID_TRANSITION`; 412 |
| `StartProjectReview` | `POST /projects/{id}/start-review` | `PROJECT_REVIEW`, internal only | 200 | 409 `INVALID_TRANSITION`; 412; 422 `CONFIGURATION_MISSING` |
| `ActivateProject` | `POST /projects/{id}/activate` | `PROJECT_ACTIVATE`, internal only | 200 | 409 `INVALID_TRANSITION`; 412 |

Commands honour `If-Match` when sent (R-21). A project's review history is `GET /approval-instances?subjectModule=Project&subjectType=Project&subjectId={id}` (TASK-035).

## 5. Error codes

`ProjectErrorCodes` (R-27):

| Code | Status | When |
| --- | --- | --- |
| `PROJECT_REFERENCE_INVALID` | 422 | A classification, governance profile, region or city not PUBLISHED in its catalogue, an inactive department or entity, a city outside the region named with it (D-8) |
| `PROJECT_PARTICIPATION_INVALID` | 422 | ENTITY_MANAGED with no entity (D-8) |
| `PROJECT_INCOMPLETE` | 422 | Submission without the budget or planned dates (D-8) |
| `PROJECT_MANAGER_INVALID` | 422 | The named manager is not an R04 holder over the project now (D-9) |
| `PROJECT_NOT_EDITABLE` | 409 | Edit outside DRAFT/RETURNED, delete outside DRAFT (D-11, D-12) |
| `PROJECT_IN_USE` | 409 | Delete of a draft another record references (D-11) |

## 6. How other modules build on it

1. **A lifecycle command of another module** (TASK-062 suspension, TASK-063 closure; §8.2 edges 7, 8) adds its edge to `ProjectLifecycle`, a command to `IProjectService`, and a migration that replaces `project.guard_project()` with the new edge. Without the migration the database refuses the transition with "is not a lifecycle transition".
2. **Reading a project** (§8.2 edges 1–6, 18): no query contract is published beyond `IProjectService`, which authorizes a caller. A module that needs a project's identity and state for its own checks adds a query to `Contracts` (F-12).
3. **A review route** is configured by AHDA in APPROVAL_AUTHORITY under `PROJECT_REGISTRATION` (F-2).

## 7. Verification

Run 2026-10-02 on macOS, Docker Desktop, PostgreSQL 17 (`AHDA-postgres`) and `AHDA-ldap`.

| # | Command | Result |
| --- | --- | --- |
| 1 | `dotnet build src/backend/PMPlatform.slnx -warnaserror`, and `--configuration Release -warnaserror` | 0 warnings, 0 errors, both |
| 2 | `dotnet test src/backend/PMPlatform.Tests.Unit` | 686 passed, 86 new: `ProjectLifecycleTests` (84: the seven edges, no path to APPROVED_PLANNED or ACTIVE around UNDER_REVIEW, all 72 ordered state pairs against ACTIVE, editability, the identifier format) and `ShippedGrantTests` (2: the four PROJECT grants; an R08 holder registers for their own entity only). Architecture tests A-1 to A-6 pass unchanged |
| 3 | `DB_CONNECTION_STRING=… dotnet test src/backend/PMPlatform.Tests.Integration` | 458 passed, 85 new (`RegistrationLifecycleTests` 7, `LifecycleGuardTests` 69, `ProjectAuthorizationTests` 4, `ProjectEndpointTests` 5). Two count assertions moved with the catalogue: `SeedDataTests` 107 → 111 labels, `AdministrationContractTests` 23 → 27 permissions. `MigrationRollbackTests` runs the new Down |
| 4 | `.github/scripts/migration-dry-run.sh`, then `seed-dry-run.sh`, on scratch databases in `AHDA-postgres` (dropped after) | "1227 statement(s), applied twice, no destructive statement"; "12 table(s) seeded, unchanged by a second run, no integrity violation" |
| 5 | `docker compose -f infra/docker/docker-compose.yml up -d --build --wait` | Migrates to `20261002122746_TASK-041_GuardProjectLifecycle`; integrity: no violation in 110 foreign keys, 88 check constraints, 197 indexes, 50 audited tables, 21 master data references; 27 permissions |
| 6 | Sign in as `local.r08` and `local.r02` on the Compose API; call the register and create with curl | r08 `GET /projects` 200 (empty); r02 403 (no grant, F-1); r08 create 422 `PROJECT_REFERENCE_INVALID` `classificationItemId NOT_FOUND` (no published classification, F-10); create without `Idempotency-Key` 400; no token 401 |
| 7 | `python3 docs/architecture/contract-check.py <openapi v1 of the running API>` | 121 operations, 546 findings; 39 on the 10 Project operations: C-4 9, C-5 9, C-7 10, C-12 10 — the R-52 classes no module emits yet, as every earlier record — and C-8 1 on the `PUT`, as on every other `PUT` (12) |
| 8 | The eight other `docs/architecture/*-check.py` gates | All OK |

The tests, by criterion:

| Criterion | Tests |
| --- | --- |
| 1. One authoritative Formal Project ID, enforced by a unique constraint | `AFormalProjectIdIsUniqueIssuedOnlyByApprovalAndNeverChanged`: two approvals issue two identifiers; a second project with the first's is refused by `ix_project_formal_project_id`; changing or clearing an issued one, or issuing one to a SUBMITTED project, is refused by the guard; APPROVED_PLANNED without one is refused by `ck_project_formal_project_id`. `ARegistrationNamesOnlyUsableReferences`: a client-sent `formalProjectId` is ignored |
| 2. Planned → Active only through the documented command, never implicitly | The workbook's validation check, both halves: `ADraftCannotBeForcedToActiveThroughTheApi` (a DRAFT's `activate` by a holder of `PROJECT_ACTIVATE` is 409 `INVALID_TRANSITION`; an edit carrying `status: ACTIVE`, `activatedAt` and `formalProjectId` changes none of them; no `ProjectActivated` event), and the full happy path through WF-11, `AnEntityDraftIsApprovedByAhdaAndBecomesActiveOnlyByTheCommand` (DRAFT → SUBMITTED → UNDER_REVIEW → approved by R02 and delivered through the outbox → APPROVED_PLANNED with its identifier → ACTIVE only after `activate`; a second `activate` is 409). `ADateReachedAndEveryWorkerRunNeverActivateAProject` (clock +800 days, every worker twice, row unchanged). `ActiveIsEnteredOnlyFromApprovedPlannedAndLeftByNoEdgeYet` (unit, 72 cases). The guard refuses ACTIVE from every other state and without `activated_at` (`TheDatabaseRefusesAChangeOfStateThatIsNoEdge`, `TheDatabaseHoldsActivationIntakeRevisionAndDeletionToTheirRules`) |
| 3. No project skips UNDER_REVIEW unless an authorised fast-track rule exists | No such rule exists (PTBC-002 Open), so none is built. `NoCommandTakesAProjectPastReview` (from DRAFT, SUBMITTED, UNDER_REVIEW and RETURNED every command but the next edge is 409, and RETURNED has no identifier); `NoPathReachesApprovedPlannedWithoutUnderReview` (unit, a search of the machine with UNDER_REVIEW removed); `TheDatabaseRefusesAChangeOfStateThatIsNoEdge` (all 65 forbidden pairs, including SUBMITTED → APPROVED_PLANNED) |
| WF-11 integration | `AReturnedRegistrationComesBackAsTheNextRevisionUnderANewRun` (revision 2, a new run linked to the returned one); `ARejectedOrWithdrawnReviewReturnsTheRegistration` (2 cases); `AnOutcomeHandedToTheProjectTwiceIsAppliedOnce` |
| ADR-013 | `AnEntityUserRegistersAndSeesOnlyTheirOwnEntitysProjects`; `AnExternalUserNeverStartsAReviewNorActivatesEvenWithTheGrant` (refused and audited `EXTERNAL_USER` although the engine allows it); `TheProjectManagerIsAnR04HolderOverTheProjectInternalOrOfItsEntity` |
| API | `ARegistrationIsShapeCheckedBeforeAnythingIsWritten`, `ARegistrationNamesOnlyUsableReferences`, `ADraftWithoutItsBudgetAndDatesIsNotSubmitted`, `AnEditNeedsTheCurrentVersion`, `TheRegisterFiltersByStateAndFindsAFormalProjectId`, `ASubmissionIsWithdrawnAndOnlyItsCreatorDeletesTheDraft`, `ARoleWithoutAProjectPermissionIsRefusedAtTheGate` |

### 7.1 Mutation tests

Each mutation was applied, the solution rebuilt, the Project tests run, and the source restored; the restored source passed again (84 integration tests; 85 since the forced-activation test was added).

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | `ProjectLifecycle` allows SUBMITTED → ACTIVE | `TheMachineHasExactlyTheSevenEdgesOfTask041`, `NoPathReachesApprovedPlannedWithoutUnderReview`, `ActiveIsEnteredOnlyFromApprovedPlannedAndLeftByNoEdgeYet(Submitted, Active)` (unit); `NoCommandTakesAProjectPastReview` (the guard still refused it, as a 500) |
| M-2 | The guard allows SUBMITTED → APPROVED_PLANNED | `TheDatabaseRefusesAChangeOfStateThatIsNoEdge(SUBMITTED, APPROVED_PLANNED)` |
| M-3 | The guard lets an issued Formal Project ID change | `AFormalProjectIdIsUniqueIssuedOnlyByApprovalAndNeverChanged` |
| M-4 | The AHDA gates admit an external user | `AnExternalUserNeverStartsAReviewNorActivatesEvenWithTheGrant` |
| M-5 | The handler applies an outcome whatever the project's state | `AnOutcomeHandedToTheProjectTwiceIsAppliedOnce` |

## 8. Acceptance criteria, deliverables and amendments

| Item | Result |
| --- | --- |
| Every Project has exactly one authoritative Formal Project ID enforced by a unique constraint | **MET.** D-5; §7 criterion 1; M-3 |
| The Planned-to-Active transition is only reachable through the documented command, never implicitly | **MET.** D-2, D-4, D-7; §7 criterion 2; M-1 |
| A Project cannot skip Under Review unless an explicitly authorized fast-track rule exists (else flagged as Open Question, PTBC-002) | **MET.** No fast-track exists or is built; PTBC-002 stays Open (F-6). D-2, D-3, D-4; §7 criterion 3; M-1, M-2 |
| Description: the aggregate and its early lifecycle, the Planned → Active command, WF-11 for approval routing, no auto-completion or auto-activation | **MET** against the sources in the header. Blueprint Sections 5 and 9 and ICD-02 are not in the repository (`solution-architecture.md` S-1), so the machine is the workbook row's and the ERD's (§6 row 8) |
| Deliverables: aggregate service, lifecycle state machine, approval integration, tests | **MET** (header row "Deliverables") |
| Gate decision / ADR-013 change: entity drafts; AHDA approves before the identifier; Project Managers not from the AHDA directory alone | **MET.** D-9, D-10; §7 ADR-013 row; M-4 |
| ADR-014: legacy intake path; Declared Baseline distinguishable wherever displayed | **PARTLY, by scope.** The marker is exposed and guarded (D-14); the intake path and the Declared Baseline are TASK-104's and WF-03's (F-9) |

## 9. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **No role holds `PROJECT_REVIEW` or `PROJECT_ACTIVATE`, and no internal role holds `PROJECT_VIEW` or `PROJECT_REGISTER`.** Blueprint Appendix A is not in the repository; only ADR-013's entity grants ship (D-10). The tests grant AHDA's side to R02 and R03 | PMO (Appendix A); TASK-110 | Entities can register, but no one at AHDA can see, review or activate a project in a real environment |
| F-2 | **No `PROJECT_REGISTRATION` route is published in APPROVAL_AUTHORITY** (OQ-005), and WORKFLOW_POLICY lacks `APPROVAL_TASK_DUE_DAYS` (`approval-framework.md` F-2). `start-review` answers 422 `CONFIGURATION_MISSING` | AHDA, through FG-04 | No review can start |
| F-3 | **The Formal Project ID's format is this task's choice**: `PRJ-` and a sequence number, six digits or more. No source states one | AHDA PMO | A different format applies only to identifiers issued after it; issued ones never change (D-4) |
| F-4 | **REJECTED and WITHDRAWN return the registration**, because ERD §6 has no cancelled or rejected project state (ERD E-3, owed before this task) and the workbook names RETURNED and APPROVED_PLANNED as review's only ends. A rejected project stays RETURNED; after review it cannot be deleted (its approval history references it) | Engagement Architect, against WF-01 | Abandoned registrations accumulate in RETURNED |
| F-5 | **`audit_event.scope_project_id` references `project.project` with RESTRICT**, so a draft scoped on its own audit events could never be deleted (HARD_DRAFT). A project's events carry the scope only from review on; before that they are found by subject (`Project`, id) | Engineering Architect (ERD D-14, TASK-033) | A draft's pre-review history is missing from project-scoped activity queries |
| F-6 | **PTBC-002 is Open: no fast-track exists.** Adding one later is a new edge in `ProjectLifecycle` and a migration replacing the guard | AHDA PMO / Business Sponsor | — |
| F-7 | **§8.2 edge 20 reads "registration / activation approval"; activation is built as an AHDA command with no WF-11 run.** The workbook row asks for "the integrated Planned-to-Active command"; no source names an activation route | PMO (WF-01, ICD-02) | If activation needs approval, a `PROJECT_ACTIVATION` route and its handling are added |
| F-8 | **The registrant names the governance profile**, which the column requires; assignment by rule on budget and duration, its override and O-1 are TASK-105's | TASK-105 | An entity can propose any published profile; AHDA sees it at review |
| F-9 | **The legacy intake path is not built** (ADR-014): no intake command, no `ProjectIntakeRecorded`, no Declared Baseline. TASK-104 owns it; its event has four consumers and needs per-consumer outbox delivery (`approval-framework.md` F-13) | TASK-104 | Projects already under way cannot enter yet |
| F-10 | **PROJECT_CLASSIFICATION, REGION and CITY have no published items** in any environment (`seed-data-and-integrity.md` F-2) | AHDA master data owners | No draft can be created until an administrator publishes a classification |
| F-11 | **The review's requester is the AHDA reviewer**, not the entity submitter (D-3). The entity follows the project's state, not My Requests; the reviewer cannot also decide the review | PMO | If entity submitters should see the run in SCR-101, the requester changes to the submitter |
| F-12 | **No project query contract for other modules**, and DocumentManagement and IdentityAccess still read `project.project` directly (their F-6): as foundation modules they may call no domain module (§4.5), so the reads cannot move to a Project contract without an ADR-003 change | Engineering Architect | The two reads stay |
| F-13 | **What submission requires is this task's choice**: the budget and both planned dates, the inputs of profile assignment. The profile's mandatory field list is TASK-105's | TASK-105; PMO | A profile's other mandatory fields are not enforced at submission |
| F-14 | **No field is masked** (R-20 `maskedFields`): FIELD_CLASSIFICATION classifies no project field yet, and no module applies masks | AHDA Cybersecurity (UGV-01) | The registration budget is shown to everyone who sees the project |
| F-15 | **`NarrativeText` is written `{text, language: "EN"}`**, the platform's shape (`approval-framework.md` F-12) | Engineering Architect | As that finding |
| F-16 | **Idempotency keys are required, not replayed** (TASK-031 F-2): a retried create makes a second draft | Engineering Architect | The SPA must not retry a create blindly |
| F-17 | **Security Lead review (CTL-43) cannot be requested**: the CODEOWNERS teams do not exist (TASK-031 F-13). This change touches RBAC | Maintainer | The PR's CTL-43 box stays unticked |

## 10. Change log

| Date | Change |
| --- | --- |
| 2026-10-02 | Created (TASK-041) |
| 2026-10-02 | TASK-042 (`project-registration-ui.md` D-9): `ListProjects` takes `projectManagerUserId` for SCR-026 My Projects, one more predicate inside the caller's `RecordScope`; `MyProjectsAreTheOnesTheCallerManagesWithinTheirScope` |
| 2026-10-02 | TASK-043 (`project-lifecycle-contract-tests.md`): as built, the WF-01 API has 9 operations (the header's "10" counts one too many; §4 lists 9), all pinned by the contract snapshot `docs/api/openapi.v1.json`. Their document now states `status`, `participationMode` and `language` as UPPER_SNAKE enums and `registrationBudgetSar` as the R-16 string, as the API has always sent them. The two C-13 findings on `registrationBudgetSar`, which §7 row 7 did not count, are cleared |
| 2026-10-02 | TASK-044 (`progress-update.md` D-12): F-12's query contract exists for the core modules — `IProjectFactsReader` returns a project's identity, anchors, lifecycle state, governance profile, intake marker and activation time, unauthorized, for §8.2 edges 1–6. Progress is its first caller. The foundation modules' direct reads (F-12) are unchanged. |
| 2026-10-03 | TASK-046 (`schedule-baseline.md` D-11): the Planned → Active command has a precondition, ADR-009's — the project needs an ACTIVE baseline (an APPROVED one, or ADR-014's Declared one for a legacy-intake project), else 422 `SCHEDULE_ACTIVE_BASELINE_REQUIRED`. `IProjectActivationPrecondition` in Project's contracts is implemented by Schedule, so the dependency stays Schedule → Project (§8.2 edge 2). `RegistrationLifecycleTests`, `ProjectLifecycleEndToEndTests` and `ProjectContractTests` give the project an ACTIVE baseline before they activate it. The check is the application's; the database's `guard_project` does not read another schema (M-13). |
| 2026-10-03 | TASK-048 (`project-task.md` D-2): ProjectTask reads `IProjectFactsReader` under the new §8.2 edge 36, for the anchors a task is authorized on and the lifecycle state that gates its execution. No change to Project. |
| 2026-10-03 | TASK-050 (`milestone-achievement.md` D-3): Milestone reads `IProjectFactsReader` under the new §8.2 edge 37, for the anchors an achievement claim is authorized on and the ACTIVE state it needs. |

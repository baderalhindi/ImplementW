# Shared Approval Framework (WF-11 Backend)

| Field | Value |
| --- | --- |
| Task | TASK-035 — Build Shared Approval Framework (WF-11 Backend) (P6 - Shared Platform Services) |
| Depends on | TASK-034 — Build Master Data & Configuration Service (`master-data-configuration.md`): APPROVAL_AUTHORITY and WORKFLOW_POLICY are resolved through `IConfigurationResolver`. TASK-030 — Implement Server-Side RBAC & Data-Scope Authorization Engine (`authorization-engine.md`): authority is evaluated by `IAuthorizationEngine` |
| Record date | 2026-09-28 |
| Status | **BUILT AND VERIFIED LOCALLY** against `AHDA-postgres` and `AHDA-ldap`, in process and through the real API pipeline (§6). No role holds the approval permissions (F-1) and no APPROVAL_AUTHORITY or WORKFLOW_POLICY version is published (F-2), so on a fresh environment no approval can start or be decided until both are supplied |
| Branch | `feat/task-035-wf11-shared-approval-framework-backend` |
| Deliverables | **WF-11 approval engine**: `PMPlatform.Application/Features/Approval` (`ApprovalRequestService`, `ApprovalWorkflowService`, `ApprovalRouting`, `ApprovalStages`, `ApprovalOutcomes`, the repository port) and the domain in `PMPlatform.Domain/Approval`. **Routing / delegation / escalation services**: `ApprovalRouting`, `ApprovalAuthority`, `ApprovalDelegationService`, `ApprovalEscalation`, `ApprovalMaintenance`. **Source-callback contracts**: `Features/Approval/Contracts` — `IApprovalRequests` (start, query by subject), `IApprovalOutcomeHandler` and `Events/ApprovalOutcomeRecorded` with `ApprovalOutcomeData` (event-conventions EV-11). Supporting: the transactional outbox (`Application/Common/Events`, `common.outbox_message`, the dispatcher), four migrations, eleven endpoints, and tests (§6) |
| Environment variables / secrets | None. Two optional configuration sections with defaults: `Outbox` (`PollInterval` 5 s, `BatchSize` 50, `RetryBaseDelay` 30 s) and `Approval:Maintenance` (`PollInterval` 5 min, `BatchSize` 100) |
| Gate decision applied | None on the workbook row |
| Participation amendment | **ADR-013: no external approval authority of any kind. Recorded explicitly in the approval framework** — D-6 |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), Implementation Plan rows TASK-034, TASK-035, TASK-036, TASK-041, TASK-046, TASK-057, TASK-060, TASK-062, TASK-073, as read 2026-09-28; ERD §5 `approval`, D-13, D-14, D-15, `common.outbox_message`; `solution-architecture.md` §4.5, M-7, M-8, M-11, §8.2 edges 20–28; `event-conventions.md` EV-4 to EV-11 and §8 row 5; `indexing-strategy.md` I-22 to I-25; `master-data-configuration.md` F-13 |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | Routing a subject revision through the APPROVAL_AUTHORITY matrix, pinned; approval tasks per stage; approve, reject, return; approval history | This task |
| **In** | Authority evaluation through FG-03 at decision time; delegation that never widens authority; escalation of overdue tasks; withdrawal | This task |
| **In** | The idempotent outcome callback to the source module: `ApprovalOutcomeRecorded` through a transactional outbox, applied once | This task |
| **In** | ADR-013 as an explicit, tested and database-enforced rule of the framework | This task |
| **Out** | The source modules and their handlers (Project, Schedule, ChangeRequest, Suspension, ManagementConcern, Milestone, FinancialKpi, Closure) | TASK-041, 046, 050, 052, 057, 060, 062, 063 |
| **Out** | SCR-100/101/114/115 and MOD-040–045 | TASK-036 |
| **Out** | Notifications of assignment and escalation (`Approval.ApprovalTaskAssigned` intents) | TASK-039 (F-9) |
| **Out** | Which roles decide approvals (Appendix A) and the matrix and policy values (OQ-005) | PMO, AHDA (F-1, F-2) |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Module shape.** `Features/Approval` with `Contracts` (what other modules and the API see) and internal services; entities in `Domain/Approval`; tables in the `approval` schema exactly as ERD §5 draws them (`ApprovalSchemaTests`). The module references only `Application/Common` and the E-U1/E-U2 contracts. | ADR-003 §4.3 row 11; A-1 to A-6 pass unchanged. |
| D-2 | **A source starts a run in its own transaction.** `IApprovalRequests.StartAsync(ApprovalStart)` takes the subject (module, type, id, revision), the routing key, the requester, the scope anchors and the routing inputs (governance profile, band, amount). It stages the run and its tasks and returns; the source's own save commits them with the source's transition to SUBMITTED. A retried start of the same PENDING revision returns that run. A module with no registered `IApprovalOutcomeHandler` is refused at start, because its outcome could never be applied. | M-8 (subject-agnostic), M-11 (one command, one transaction), §8.2 edges 20–27. |
| D-3 | **Routing is the matrix, pinned, and fails closed.** The APPROVAL_AUTHORITY version in force when the run starts is resolved and its id pinned on the run (ERD D-13). A row applies when its subject type is the routing key and each of its conditions (profile, band, minimum amount) is absent or met; every applicable row becomes a task, and stages run in sequence order. No applicable row, or two applicable rows naming the same role for the same stage (ERD O-4), is `ConfigurationMissingException` → 422 `CONFIGURATION_MISSING`. All stages' tasks are created at start, so the history shows the whole route; a later stage's tasks get their due date when the stage is reached. Every routed row must approve (F-6). | Blueprint Section 12 as TASK-034 built it; D-13 "approval routing" is a pinned configuration. |
| D-4 | **Authority is FG-03's, evaluated when it is exercised.** A user may decide a task when they are an active internal user, did not request the run, and hold `APPROVAL_DECIDE` *through the task's role* with a grant whose scope covers the run's anchors (project, department). `AuthorizationRequest.RoleCode` restricts the engine to that role's grants; `IAuthorizationEngine.EvaluateAsync` answers the same question without an audit event, for questions that are not requests (a delegator's authority, an inbox's contents). Nothing is cached across requests: an ended assignment, a disabled account or a changed profile takes effect on the next decision. The decision records `eligibility_revalidated_at`. | Workbook: "authority evaluation via FG-03", "decision-time eligibility/security revalidation"; M-7 (the run supplies the subject). |
| D-5 | **Delegation never expands authority.** A standing delegation (SCR-114) names a delegate, an optional routing key and a period. When the delegate acts, the delegator's *own* authority is evaluated at that moment (D-4) within the routing key and period; what the delegator holds only through another delegation is not their own, so a chain conveys nothing beyond its first link. The task records both people: `assigned_user_id` is whose authority decided, `acting_user_id` who acted, `approval_delegation_id` the delegation. Revoked, expired or out-of-period delegations convey nothing. Only the delegator revokes. | Acceptance criterion 2; ERD `approval_task` notes. |
| D-6 | **ADR-013 — no external approval authority of any kind.** Stated in `ApprovalAuthority`'s remarks and enforced three ways: (1) the application refuses an external user as decider, as the authority behind a decision, and as either side of a delegation, whatever role or grant they hold — the check is on the person, because roles such as R04 are held by internal and external users alike (TASK-034 F-13); (2) the refusal is audited as `Approval.DecisionRefused` with `refusal_reason = EXTERNAL_USER`; (3) migration `TASK-035_GuardApprovalHistory` refuses, in the database, an external user in `approval_task.acting_user_id` or `assigned_user_id`, or in either column of `approval_delegation` (constraint name `ck_approval_internal_authority`). An external requester may submit, see, withdraw and escalate their own request: none of those is approval authority, and escalation writes no decider. | Workbook participation amendment; ERD notes "Internal only (ADR-013)". |
| D-7 | **Decisions.** Approve advances the run: when no task of the stage is left PENDING the next stage starts its clock; when none is left at all the run is APPROVED. Reject and return end the run (REJECTED, RETURNED), cancel the open tasks and need a reason (`APPROVAL_REASON_REQUIRED`; the API also answers 400 `reason REQUIRED`). A task of a later stage cannot be decided yet (409 `INVALID_TRANSITION`). Every change to a run also changes the run's row, whose `xmin` is its concurrency token, so two decisions on one run cannot both commit: the second answers 409 `APPROVAL_CONCURRENT_DECISION`. A retried decision answers 409 `TERMINAL_STATE` and publishes nothing. | TASK-036's reason rule; R-21 concurrency. |
| D-8 | **The outcome is delivered exactly once.** Ending a run stages `Approval.ApprovalOutcomeRecorded` (`ApprovalOutcomeData`, EV-11) in `common.outbox_message` in the deciding transaction; the message key is `<eventType>:<outcome_idempotency_key>`, unique with the kind, so a run publishes one outcome. The dispatcher, after commit, opens a transaction, locks the message (`FOR UPDATE SKIP LOCKED`) only if it is still undispatched, calls the one consumer, and marks it dispatched in that same transaction. `ApprovalOutcomeDispatch` hands the outcome to the source's `IApprovalOutcomeHandler` (chosen by `subject.module`) and marks the run's `outcome_delivered_at` in that transaction too, skipping a run already delivered. The source's change, the run's mark and the message's mark therefore commit together or not at all: a second dispatch, a concurrent one, a redrive after failure, or the same callback fired again finds the outcome delivered and changes nothing. A handler failure rolls everything back; the attempt is counted and retried with exponential back-off, at most five times (EV-6), then left with its `last_error`. | Acceptance criterion 1; event-conventions EV-4 to EV-6, §8 row 5 ("not a per-source callback interface": one contract, chosen by module). |
| D-9 | **A returned request comes back as a new revision and a new run.** A run is unique on (subject module, type, id, revision) (ERD D-15). A start for a revision not newer than the subject's last run is `APPROVAL_REVISION_STALE`; a start while a run is PENDING is 409 `APPROVAL_ALREADY_PENDING`. The new run links to the one before it (`previous_instance_id`). A decided run is never changed again: the database refuses any change to its subject, route or requester at any time, any change to a decided run except recording its outcome delivery once, any change to a decided task, and any deletion (RETAIN). | Acceptance criterion 3. |
| D-10 | **Two permissions, no grants.** `APPROVAL_VIEW` (Read) and `APPROVAL_DECIDE` (Write), group `APPROVAL`, in `PermissionCatalogue` and the seed. No shipped profile grants either, by the user's decision on 2026-09-28 (F-1). A run is visible to its requester, to a holder of `APPROVAL_VIEW` whose scope covers it (OWN covers the requester's own), and to whoever holds authority over one of its current tasks; anything else is 404 (R-47). | TASK-030's rule: only source-backed grants ship. |
| D-11 | **Escalation mechanics.** WORKFLOW_POLICY names `APPROVAL_TASK_DUE_DAYS` (DURATION_DAYS, how long a stage's approvers have from when it is reached) and `APPROVAL_ESCALATION_ROLE` (TEXT, a role code); neither has a default (F-7). An overdue task of the current stage is escalated by the maintenance worker (as `svc.approval-workflow`) or by its requester (MOD-045): it becomes ESCALATED, linked through `escalated_to_task_id` to a new task for the escalation role in the same stage, due afresh. A task created by an escalation, or a stage the escalation role already holds, is not escalated again. The replacement's decider meets D-4 like any other. The same worker marks lapsed delegations EXPIRED. | Workbook "escalation mechanics"; ERD `approval_task.escalated_to_task_id`. |
| D-12 | **Every step is audited** through `IAuditTrail` (TASK-033), in the saving transaction: `ApprovalRequested`, `TaskEscalated`, `ApprovalCompleted`, `OutcomeDelivered` (LIFECYCLE_TRANSITION); `TaskApproved`, `TaskRejected`, `TaskReturned` (APPROVAL_DECISION, with the authority user and delegation); `DecisionRefused` (AUTHORIZATION_DENIAL, recorded on its own); `DelegationCreated`, `DelegationRevoked`, `DelegationExpired` (PERMISSION_CHANGE). Reasons are free text and are not copied into the audit store. | event-conventions row 13; CTL-25. |
| D-13 | **The transactional outbox is built here.** No earlier task built `common.outbox_message`, and EV-6 requires an outcome to be dispatched after commit. `EventEnvelope<TData>`, `EventSerialization`, `IOutbox`, `IDomainEventConsumer` and `IOutboxDispatcher` are in `Application/Common/Events` (M-10); the table, the writer, the dispatcher and its worker in Infrastructure. Only DOMAIN_EVENT messages are dispatched, each to exactly one consumer (F-13). | EV-6, EV-8; event-conventions §8 row 7 left it unassigned. |
| D-14 | **Four migrations, one schema each** (migrations README R-7): `TASK-035_CreateOutboxMessage` (`common`), `TASK-035_CreateApprovalTables`, `TASK-035_AddApprovalCrossModuleForeignKeys`, `TASK-035_GuardApprovalHistory` (`approval`). The two unique keys whose generated names exceed 63 characters are named (`ix_approval_instance_subject_revision`, `ix_approval_task_instance_stage_role`). | TASK-024 conventions; ERD D-14. |
| D-15 | **One polling loop.** `Infrastructure/Hosting/PollingWorker` is the loop the SIEM forwarder already had; the outbox dispatcher, the approval maintenance pass and the SIEM forwarder derive from it. | DRY; the SIEM worker's behaviour is unchanged (its tests pass). |
| D-16 | **Two service principals** in the seed: `svc.approval-workflow` (`…00fc`, `ApprovalServicePrincipal.Id`) and `svc.outbox-dispatch` (`…00fb`, `OutboxDispatcher.DispatchPrincipalId`), so a worker's change has a `created_by`/`updated_by` (ERD D-2). | ERD D-2. |

## 3. Endpoints

All paths are under `/api/v1`, tag `Approval`, operation ids `Approval_*`. Every non-2xx answer is the R-23 envelope; every `POST` also answers 400 `IDEMPOTENCY_KEY_REQUIRED`/`…_INVALID`. A run is started only in process (D-2).

| Operation | Method and path | Permission | Success | Refusals beyond 401/403 |
| --- | --- | --- | --- | --- |
| `ListApprovalTasks` | `GET /approval-tasks?page=&pageSize=` | `APPROVAL_DECIDE` | 200 `ApprovalInboxPage`: tasks the caller may decide now, earliest due first, with `onBehalfOfUserId` under a delegation | 400 |
| `ApproveApprovalTask` | `POST /approval-tasks/{id}/approve` `{reason?}` | `APPROVAL_DECIDE` | 200 `ApprovalInstanceDetail` | 404; 409 `TERMINAL_STATE`, `INVALID_TRANSITION`, `APPROVAL_CONCURRENT_DECISION`; 422 `CONFIGURATION_MISSING` |
| `RejectApprovalTask` / `ReturnApprovalTask` | `POST /approval-tasks/{id}/reject` \| `/return` `{reason}` | `APPROVAL_DECIDE` | 200 | 400 `reason REQUIRED`; as approve |
| `EscalateApprovalTask` | `POST /approval-tasks/{id}/escalate` `{reason?}`, by the requester | `APPROVAL_VIEW` | 200 | 404; 409 `TERMINAL_STATE`; 422 `APPROVAL_ESCALATION_NOT_ALLOWED`, `CONFIGURATION_MISSING` |
| `ListApprovalInstances` | `GET /approval-instances?requestedBy=me` or `?subjectModule=&subjectType=&subjectId=`; `&status=&page=&pageSize=` | `APPROVAL_VIEW` | 200 `ApprovalInstancePage`, only runs the caller may see | 400 (neither or both modes) |
| `GetApprovalInstance` | `GET /approval-instances/{id}` | `APPROVAL_VIEW` | 200 `ApprovalInstanceDetail` with every task (SCR-115) | 404 |
| `WithdrawApprovalInstance` | `POST /approval-instances/{id}/withdraw`, by the requester | `APPROVAL_VIEW` | 200; outcome WITHDRAWN | 404; 409 `TERMINAL_STATE` |
| `ListApprovalDelegations` | `GET /approval-delegations` | `APPROVAL_DECIDE` | 200 `{given[], received[]}` (unpaged, F-11) | — |
| `CreateApprovalDelegation` | `POST /approval-delegations` `{delegateUserId, routingKey?, validFrom?, validTo}` | `APPROVAL_DECIDE` | 201, Location | 400; 422 `APPROVAL_DELEGATE_INVALID`, `APPROVAL_DELEGATION_PERIOD_INVALID` |
| `RevokeApprovalDelegation` | `POST /approval-delegations/{id}/revoke`, by the delegator | `APPROVAL_DECIDE` | 200 | 404; 409 `TERMINAL_STATE` |

## 4. Error codes

`ApprovalErrorCodes` (R-27):

| Code | Status | When |
| --- | --- | --- |
| `APPROVAL_ALREADY_PENDING` | 409 | A start while another run of the subject is PENDING (D-9) |
| `APPROVAL_CONCURRENT_DECISION` | 409 | Another decision on the run committed first (D-7) |
| `APPROVAL_REVISION_STALE` | 422 | A start for a revision not newer than the subject's last run (D-9) |
| `APPROVAL_REASON_REQUIRED` | 422 | Reject or return without a reason, in process (D-7) |
| `APPROVAL_ESCALATION_NOT_ALLOWED` | 422 | Not overdue, not the current stage, already an escalation, or nowhere further to go (D-11) |
| `APPROVAL_DELEGATE_INVALID` | 422 | The delegate is the delegator, or not an active internal user (D-5, D-6) |
| `APPROVAL_DELEGATION_PERIOD_INVALID` | 422 | `validFrom` in the past, or `validTo` not after it |
| `APPROVAL_REQUESTER_INVALID` | 422 | In process: the requester is unknown or inactive |
| `CONFIGURATION_MISSING` (platform) | 422 | No route, an ambiguous route, or a WORKFLOW_POLICY key missing (D-3, D-11) |

## 5. How a source module uses it

1. **Submit.** In the command that moves the record to SUBMITTED, call `IApprovalRequests.StartAsync` with the record's module, type, id and current `revision_no`, the routing key (an APPROVAL_AUTHORITY `subject_type_code`), the anchors and routing inputs; then save. The run commits with the record.
2. **Handle the outcome.** Implement `IApprovalOutcomeHandler` in `Features/<Module>/EventHandlers`, with `SubjectModule` = the module's ADR-003 name, and register it. Apply the decision to `outcome.Subject.Id` only if `outcome.Subject.RevisionNo` is still the record's current revision (EV-5: an older one is stale and is audited, not applied); record `outcome.IdempotencyKey` on the record under a unique constraint (EV-4); save through the request's context — the dispatcher's transaction commits it with the delivery mark. Throwing leaves nothing applied and the outcome is retried.
3. **Resubmit after RETURNED.** Increment `revision_no` and start again; the new run links to the returned one.
4. **Show approvals.** Query `IApprovalRequests.FindBySubjectAsync` (M-4: the record does not store its run's id).

## 6. Verification

Run 2026-09-28 on macOS, Docker Desktop, PostgreSQL 17 (`AHDA-postgres`) and `AHDA-ldap`. No other container was started.

| # | Command | Result |
| --- | --- | --- |
| 1 | `dotnet build src/backend -warnaserror`, and `--configuration Release -warnaserror` | Build succeeded, 0 warnings, 0 errors, both |
| 2 | `dotnet test src/backend/PMPlatform.Tests.Unit` | 425 passed, 16 new: `ApprovalRoutingTests` (4), `ApprovalAuthorityTests` (8 facts and a 2-case theory), `EffectiveAuthorizationTests.ARoleRestrictedRequestCountsOnlyThatRolesGrants`, `AuthorizationAuditTests.AnEvaluationIsNotAudited`. Architecture tests A-1 to A-6 pass |
| 3 | `DB_CONNECTION_STRING=… dotnet test src/backend/PMPlatform.Tests.Integration` | 309 passed: 27 new (`OutcomeCallbackTests` 4, `ReturnedRevisionTests` 3 and a 7-case theory, `DelegationTests` 5, `EscalationTests` 4, `ApprovalEndpointTests` 3, `ApprovalSchemaTests` 1), 282 existing. Two existing count assertions moved with the catalogue (`SeedDataTests` 98 → 100 labels, `AdministrationContractTests` 14 → 16 permissions). `MigrationRollbackTests` runs the four new Downs |
| 4 | `.github/scripts/migration-dry-run.sh` and `seed-dry-run.sh` on a scratch database in `AHDA-postgres` (dropped after) | "893 statement(s), applied twice, no destructive statement"; "12 table(s) seeded, unchanged by a second run, no integrity violation" (89 foreign keys, 58 check constraints, 155 indexes, 41 audited tables, 18 master data references) |
| 5 | `docker compose -f infra/docker/docker-compose.yml up -d --build --wait api` | Migrates to `20260928204130_TASK-035_GuardApprovalHistory`; seed and integrity as row 4 |
| 6 | Sign in as `local.r02` and `local.r06` on the compose API; call the three collections and an approve, with curl | 403 for all eight calls (no role holds the permissions, F-1); no token 401 |
| 7 | `python3 docs/architecture/contract-check.py <openapi v1 of the running API>` | 79 operations, 357 findings; 49 on the 11 Approval operations: C-4 11, C-5 11, C-7 13, C-12 12 — the R-52 classes the platform does not emit yet, as FG-04's F-3 — and C-9 2 on the unpaged delegation list (F-11) |
| 8 | The nine `docs/architecture/*-check.py` gates | All OK |

The tests, by acceptance criterion and validation check:

| Criterion | Tests |
| --- | --- |
| 1. A decision calls back to the source exactly once even under retry | `TheSameOutcomeFiredTwiceWithTheSameKeyChangesTheSourceOnce` — the workbook's check: dispatched, dispatched again, the due queue run, and the consumer called again directly with the same payload and key; the source's naive counter is 1, one `ApprovalCompleted` and one `OutcomeDelivered` audit event. `ConcurrentDispatchesOfOneOutcomeDeliverItOnce` (8 at once, one delivery). `AFailedDeliveryIsRolledBackAndItsRetryAppliesTheOutcomeOnce` (the handler's write rolled back, attempt counted with back-off, the retry applies once). `ARetriedDecisionAndARepublishedOutcomeAreRefused` (409 `TERMINAL_STATE`; a second message with the same key violates the unique key) |
| 2. A delegated approver never gains authority the delegator did not have | `AlongADelegationChainNoDelegateEverExceedsTheirDelegator` (unit, the workbook's check as a property: chain 1 → 2 → 3 → 4 over eight roles and three runs; every delegated decision is one the delegator could make themselves, and the full decidable set is asserted). `ADelegationChainNeverExceedsTheFirstDelegatorsAuthority` (integration, chain 2 → 5 → 6: 5 decides under 2's authority; 6 gets nothing; neither reaches an R03 task 2 could not decide; the refusal is audited). `ADelegatorWhoLostTheirAuthorityConveysNone`, `ADelegationForAnotherRoutingKeyConveysNothingHere`, `ARevokedOrLapsedDelegationConveysNothing`, `ADelegateDecidesUnderTheDelegatorsAuthorityAndTheTaskRecordsBoth` |
| 3. A returned request creates a new revision and a new linked run, not a mutation | `AReturnedRequestComesBackAsANewRevisionWithANewLinkedRun`: revision 1 returned and delivered; its run and tasks, every column as JSON, identical after revision 2 is started, approved and delivered; revision 1 refused as stale; the new run links to the old. `ADecidedRunIsNeverRewrittenOrDeleted` (7 statements refused by the database). `ARetriedStartIsTheSameRunAndANewerRevisionWaits` |
| ADR-013 (D-6) | `AnExternalUserNeverDecidesEvenWithTheRoleAndScope`, `NeitherAnExternalDelegateNorAnExternalDelegatorCarriesAuthority` (unit); `NoExternalUserHoldsApprovalAuthorityOfAnyKind` (integration: `local.r08` holds R04 with `APPROVAL_DECIDE` on the run's project and is refused, audited `EXTERNAL_USER`; delegation to or from them refused; three direct writes refused by `ck_approval_internal_authority`) |
| Routing, stages, escalation, withdrawal | `ApprovalRoutingTests` (4); `StagesRunInOrderAndTheLastApprovalEndsTheRun`, `AnOverdueTaskIsEscalatedToTheEscalationRoleAndItsReplacementDecides`, `OnlyTheRequesterEscalatesAndOnlyWhenOverdue`, `TheRequesterWithdrawsAPendingRunAndItsOutcomeIsWithdrawn` |
| API and schema | `ApprovalEndpointTests` (gates, 400/403/404/409 answers, reason capture, delegation lifecycle); `ApprovalSchemaTests` (the four tables are the ERD's); `IndexingStrategyTests` now holds I-22 to I-25 |

### 6.1 Mutation tests

Each mutation was applied, the solution rebuilt, the approval tests run, and the source restored and touched.

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | The outcome consumer ignores `outcome_delivered_at` | `TheSameOutcomeFiredTwiceWithTheSameKeyChangesTheSourceOnce` |
| M-2 | The dispatcher reads the message without `FOR UPDATE SKIP LOCKED` | **None.** `ConcurrentDispatchesOfOneOutcomeDeliverItOnce` still passes (3 runs): the run's row version independently lets only one concurrent delivery commit, and the losers roll back their source write. Exactly-once holds with either guard; no test isolates the lock |
| M-3 | External users are not refused (only SERVICE) | `AnExternalUserNeverDecidesEvenWithTheRoleAndScope`, `NeitherAnExternalDelegateNorAnExternalDelegatorCarriesAuthority`, `NoExternalUserHoldsApprovalAuthorityOfAnyKind` |
| M-4 | `APPROVAL_DECIDE` through any role counts | `AGrantOfAnotherRoleOrScopeIsNoAuthority`, `AlongADelegationChainNoDelegateEverExceedsTheirDelegator`, `ADelegationChainNeverExceedsTheFirstDelegatorsAuthority`, `RequestsAreGatedValidatedAndAnsweredByR47` |
| M-5 | A delegator's delegated authority is passed on (transitive) | `AlongADelegationChainNoDelegateEverExceedsTheirDelegator`, `ADelegationChainNeverExceedsTheFirstDelegatorsAuthority` |

## 7. Acceptance criteria, validation and amendment

| Item | Result |
| --- | --- |
| An approval decision calls back to the source module exactly once even under retry (proven by an idempotency test) | **MET.** D-8; §6 criterion 1; M-1 |
| A delegated approver never gains authority the delegator did not have | **MET.** D-5; §6 criterion 2; M-4, M-5 |
| A returned request creates a new business revision and a new linked approval instance rather than mutating the rejected one | **MET.** D-9; §6 criterion 3 |
| Validation: fire the same outcome callback twice with the same idempotency key; the source record changes exactly once | **MET.** `TheSameOutcomeFiredTwiceWithTheSameKeyChangesTheSourceOnce` |
| Validation: a delegation chain; the delegate's effective authority never exceeds the delegator's | **MET.** `AlongADelegationChainNoDelegateEverExceedsTheirDelegator`, `ADelegationChainNeverExceedsTheFirstDelegatorsAuthority` |
| Deliverables: engine, routing/delegation/escalation services, source-callback contracts | **MET** (header row "Deliverables"). No source module implements the contract yet (F-4) |
| Description: routing, FG-03 authority, tasks, decisions, delegation, escalation, history, idempotent callbacks; decision-time revalidation; no expansion by delegation | **MET** (D-2 to D-11), **per the workbook, the ERD and ADR-003/event conventions**: Blueprint Section 11 itself is not in the repository (F-3) |
| ADR-013: no external approval authority of any kind, recorded explicitly | **MET.** D-6; M-3 |

## 8. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **No role holds `APPROVAL_VIEW` or `APPROVAL_DECIDE`.** The user chose on 2026-09-28 to ship them ungranted: Blueprint Appendix A, which decides who approves, is not in the repository (`authorization-engine.md` F-1). The tests grant them to test roles | PMO (Appendix A); TASK-110 (profiles) | In a real environment no one can see an inbox or decide; the §6 row 6 calls show 403 |
| F-2 | **No APPROVAL_AUTHORITY or WORKFLOW_POLICY version is published** (OQ-005). A start answers 422 `CONFIGURATION_MISSING` | AHDA, through FG-04 | No approval can start |
| F-3 | **Blueprint Section 11 is not in the repository** (`solution-architecture.md` S-1, Q6). The runtime follows the workbook row, the ERD `approval` tables, M-8 and EV-11; anything Section 11 adds is unchecked | PMO (document custody) | A Section 11 rule absent from those sources is missing |
| F-4 | **No source module consumes the framework yet.** The tests play one (`TestSource`). The eight source tasks implement `IApprovalOutcomeHandler` (§5); three routes are inferred (`solution-architecture.md` S-4) | TASK-041, 046, 050, 052, 057, 060, 062, 063 | — |
| F-5 | **`DELEGATED` and `EXPIRED` task statuses are never produced.** They are in the ERD's value set; a delegate acts on the task itself (D-5) and an overdue task is escalated (D-11). The unique key (instance, stage, role) also rules out a second task for a per-task delegate | Engineering Architect (ERD); TASK-036 (MOD-043 shown as a delegation) | Two unused values |
| F-6 | **`is_mandatory` has no weaker meaning here**: every routed row must approve. No source says what a non-mandatory row does, and `approval_task` has no column to carry it | AHDA (OQ-005), then ERD | A non-mandatory row blocks like a mandatory one — never fewer approvals than configured |
| F-7 | **`APPROVAL_TASK_DUE_DAYS` and `APPROVAL_ESCALATION_ROLE` are named by this task**, not by a source (TASK-034 F-2: value keys are not fixed per family). One escalation role serves every routing key | AHDA (OQ-005); Engineering Architect | A different key or per-route escalation is a code change |
| F-8 | **`approval_instance` has no external-entity anchor.** An external user's ENTITY-scoped `APPROVAL_VIEW` never covers a run (cross-entity isolation needs an entity to match); an external requester still sees their own runs by the requester rule (D-10) | Engineering Architect (ERD re-issue, if entity users should see their entity's runs) | Entity colleagues of a requester do not see the run |
| F-9 | **No notification intents** (assignment, escalation): TASK-039 and the NOTIFICATION_INTENT consumer are not built | TASK-039 | Approvers learn of tasks only from the inbox |
| F-10 | **Idempotency keys are required, not replayed** (TASK-031 F-2): a retried decision answers 409 `TERMINAL_STATE`, a retried delegation creates a second one | Engineering Architect (`common.idempotency_record`) | The SPA treats those 409s as "already done" |
| F-11 | **The OpenAPI document misses R-49 to R-55** on the 11 operations (§6 row 7); `GET /approval-delegations` is unpaged (C-9) | TASK-011, TASK-015, TASK-094; this module for C-9 if the list grows | As TASK-031 F-3 |
| F-12 | **`NarrativeText` is written `{text, language: "EN"}`**, not R-18's `{text, lang: "en"}`. It is the platform's shape since TASK-034, and Approval's `decisionReason` follows it rather than differ | Engineering Architect; one change across modules | Clients read a non-conforming shape |
| F-13 | **The outbox delivers each DOMAIN_EVENT to one consumer**, with one `dispatched_at`. `ProjectIntakeRecorded` has four consumers (EV-6: one transaction per event and handler) and needs per-consumer delivery state, which the ERD's outbox has no column for | TASK-104; Engineering Architect (ERD) | A second consumer of one event type makes dispatch fail, visibly |
| F-14 | **Audit events are still written directly**, not through the outbox (TASK-033 D-2) | TASK-073 | As TASK-033 |
| F-15 | **Separation of duties is the requester rule only.** No source says whether one person may decide two stages of one run | PMO | One person holding two stage roles may approve both |
| F-16 | **Security Lead review (CTL-43) cannot be requested**: the CODEOWNERS teams do not exist (TASK-031 F-13). This change touches RBAC | Maintainer | The PR's CTL-43 box stays unticked |

## 9. Change log

| Date | Change |
| --- | --- |
| 2026-09-28 | Created (TASK-035) |
| 2026-10-02 | TASK-041 (`project-registration.md`): Project is the first source module (F-4). Its review starts a run on routing key `PROJECT_REGISTRATION` when AHDA starts the review, not when the entity submits (that record's D-3); its `IApprovalOutcomeHandler` maps APPROVED to APPROVED_PLANNED and RETURNED, REJECTED and WITHDRAWN to RETURNED. |
| 2026-10-03 | TASK-050 (`milestone-achievement.md` D-7, D-9): Milestone is the third source module, on routing key `MILESTONE_ACHIEVEMENT` (S-4's edge 25). `IApprovalRunReader` added to the contracts — a subject's runs over the repository alone — because a source's `IApprovalOutcomeHandler` that read its run through `IApprovalRequests` closed a dependency cycle with `ApprovalRequestService`, which takes every handler; `IApprovalRequests.FindBySubjectAsync` delegates to it, unchanged in behaviour. |
| 2026-10-04 | TASK-052 (`financial-kpi.md` D-5): FinancialKpi is the fourth source module, with two subject types — `FinancialCommitment` on routing key `COMMITMENT`, which passes the amount for materiality routing, and `KpiTargetVersion` on `KPI_TARGET`, the ERD's APPROVAL_AUTHORITY codes (S-4's edge 26). One `IApprovalOutcomeHandler` applies both: APPROVED activates and supersedes, RETURNED returns for the next revision, REJECTED and WITHDRAWN end the version. No behaviour of WF-11 changes. |
| 2026-10-07 | TASK-060 (`change-request.md` D-5, D-6): ChangeRequest is the sixth source module, on routing key `CHANGE_REQUEST` (edge 22), with the materiality band as `BandNo` and the absolute cost impact as the amount, so APPROVAL_AUTHORITY rows route by band. The run starts when AHDA starts the review, as TASK-041's does, but its **requester is the change's originator**, not the reviewer, so the framework's own rule keeps the originator from approving their own change, and lets them withdraw the run. `ChangeRequestApprovalOutcomeHandler` applies the outcome (edge 28) and, on approval, issues the change authorisations; it calls no target module |

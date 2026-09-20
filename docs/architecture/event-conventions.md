# Event Conventions — Typed Event Contract and Schema Style Guide

| Field | Value |
| --- | --- |
| Task | TASK-009 — Define Platform API & Event Contract Conventions (P1 - Architecture Decisions) |
| Companion record | `docs/architecture/api-conventions.md` — status, sources, residual items (S-1 to S-8), acceptance check and confirmation are kept there once for both records |
| Record date | 2026-09-20 |
| Status | as `api-conventions.md`: **RATIFIED (delivery team) — PROVISIONAL on ADR-002 and ADR-003 approval** |
| Decision owner | Engagement Architect |
| Deliverables | This record; `event-envelope.schema.json` (the envelope, JSON Schema 2020-12); `event-samples/` (three sample events, §7); checked by `contract-check.py` |
| Controlled sources and authority order | per TASK-001 |

## 1. What this record is

ADR-003 draws twelve dashed edges: four universal (audit, notify) and eight specific (approval outcomes, intake, integration telemetry). Each is "an asynchronous typed event (publisher → subscriber)" (solution architecture §7), and the ERD gives all of them one physical home — `common.outbox_message`, whose `payload jsonb` "is a serialised envelope, never queried by attribute" (D-17). Neither record says what the envelope is, what an event is called, how a consumer knows it has already handled one, or what happens when handling fails.

This record does. It is the "typed-event contract format used for cross-module integration" of the TASK-009 row:

1. **Three kinds of message, one envelope** (§3, §5) — domain events, notification intents and audit events share `EventEnvelope`, which is exactly the outbox row's payload; `kind` is the outbox `message_type`.
2. **The event catalogue** (§4) — every event the workbook and ADR-003 imply, named under EV-1, with its producer, consumers, edge, subject and idempotency key. It is the event-side counterpart of ADR-003 §8: architecture test A-10 (api-conventions §8.2) fails when an event exists that this table does not list.
3. **Twelve rules** (§6, EV-1 to EV-12) — naming, envelope, payload, idempotency, delivery, commit-before-publish, versioning, placement, the three fixed data schemas, and schema publication.

The Step 14A INT-001-030 and EVT-001-024 families the row references are not obtainable (api-conventions §3, S-2). The catalogue's last column is blank and ready for them; nothing in it is quoted from Step 14A.

## 2. Scope

| | Subject | Owner |
| --- | --- | --- |
| **Decides** | Event kinds, naming, the envelope, payload rules, idempotency, delivery semantics, versioning, code placement, the audit / notification-intent / approval-outcome data schemas, schema publication | This record |
| **Decides** | The initial event catalogue | §4 — extended by each Backend task, never bypassed (A-10) |
| **Does not decide** | Which module may publish to which — the edges | **ADR-003 §8.** This record names the events on those edges; a new edge revises §8.2 first |
| **Does not decide** | The audit hash chain, redaction on capture, the Activity projection | TASK-073, TASK-083 |
| **Does not decide** | Notification routing, recipient resolution, channel matrices | TASK-039, FG-04 (TASK-034) |
| **Does not decide** | Integration invocation semantics, retry policy per integration | TASK-075 (`IntegrationDefinition.max_attempts`, `retry_backoff_seconds`) |
| **Does not decide** | HTTP-level idempotency | api-conventions R-34 to R-40. The two layers are distinct (R-39, EV-4) |

## 3. Three kinds, one envelope

| `kind` (= outbox `message_type`) | ADR-003 mechanism | What it is | Consumer |
| --- | --- | --- | --- |
| `DOMAIN_EVENT` | *event* and *outcome event* (§8.2 edges 28, 31–33, 35) | A fact one module has committed that another module acts on | The modules named on the edge |
| `NOTIFICATION_INTENT` | E-U4 | A request that someone be told, published after commit (M-5) | Notifications, only |
| `AUDIT_EVENT` | E-U3 | A mandatory audit-class record, captured in the writing transaction (M-6) | AuditActivity, only |

One business occurrence often produces more than one kind: a change-request submission writes an `AUDIT_EVENT` (class `LIFECYCLE_TRANSITION`) and, if the routing matrix says so, a `NOTIFICATION_INTENT`; an approval decision writes an `AUDIT_EVENT` (`APPROVAL_DECISION`) and a `DOMAIN_EVENT` (the outcome). They share the `eventType` and the `idempotencyKey`; they differ in `kind`, which is why the outbox is unique on `(message_type, message_key)` and not on `message_key` alone.

Integration telemetry (edges 31–33) is a `DOMAIN_EVENT` whose consumer is IntegrationMonitoring; there is no fourth kind.

## 4. Event catalogue

Producer and consumer are ADR-003 modules; *edge* is the ADR-003 §8 row; *subject* is the aggregate the envelope's `subject` names; *key* is the producer's natural idempotency key (EV-4). The INT/EVT column is blank pending Step 14A (S-2).

| # | `eventType` | Kind | Producer | Consumers | Edge | Subject | Key | Data schema | INT/EVT |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | `Project.ProjectIntakeRecorded` | DOMAIN_EVENT | Project | Schedule, Progress, Milestone, FinancialKpi | 35 | `ProjectIntake` | `ProjectIntake.id` | `ProjectIntakeRecordedData` | |
| 2 | `Approval.ApprovalOutcomeRecorded` | DOMAIN_EVENT | Approval | the `subject.module` (Project, Schedule, ChangeRequest, Suspension, ManagementConcern, Milestone, FinancialKpi, Closure) | 28 | the approval subject, with `revisionNo` | `ApprovalInstance.outcome_idempotency_key` | `ApprovalOutcomeData` (EV-11) | |
| 3 | `IdentityAccess.IntegrationInvocationCompleted` | DOMAIN_EVENT | IdentityAccess | IntegrationMonitoring | 31 | `SyncRun` or the invocation's own id | invocation id | `IntegrationInvocationData` | |
| 4 | `Notifications.IntegrationInvocationCompleted` | DOMAIN_EVENT | Notifications | IntegrationMonitoring | 32 | `NotificationDelivery` | `NotificationDelivery.id` + attempt | `IntegrationInvocationData` | |
| 5 | `ExternalParticipation.IntegrationInvocationCompleted` | DOMAIN_EVENT | ExternalParticipation | IntegrationMonitoring | 33 | `SourceApplication` or the Nafath call | `SourceApplication.idempotency_key` | `IntegrationInvocationData` | |
| 6 | `<Module>.<Fact>` — one per mandatory audit class occurrence | AUDIT_EVENT | every module | AuditActivity | E-U3 | the written aggregate | the producer's transaction-scoped key (aggregate id + `revisionNo`, or the approval/escalation id) | `AuditEventData` (EV-9) | |
| 7 | `<Module>.<Fact>` — one per notifiable occurrence, e.g. `ManagementConcern.ConcernEscalated`, `Risk.ReviewDue`, `Reports.ReportJobCompleted`, `Approval.ApprovalTaskAssigned` | NOTIFICATION_INTENT | every module | Notifications | E-U4 | the occurrence's aggregate | `ConcernEscalation.id`, `ReportJob.id`, `ApprovalTask.id`, … — the `source_reference` of the ERD's unique index | `NotificationIntentData` (EV-10) | |

Rows 6 and 7 are families: their concrete `eventType`s are declared by each Backend task in its `Contracts/Events` folder and appended to this table in the same PR (A-10). Nothing else is an event. In particular, the synchronous edges of ADR-003 §8.2 (queries, commands, read projections, the split-authority contract) are **not** events and must not be re-expressed as events to avoid a dependency — that is what M-9's "one direction a call, the other an event" rule already decides per edge.

**Scheduled reminders.** TASK-039 requires a reminder to "revalidate the current source condition before sending". The intent carries the condition (EV-10 `condition`), and Notifications evaluates it with a typed query against the source module's `Contracts` at send time. That is a Notifications → source *query* edge, which ADR-003 §8.2 does not list; it is recorded as S-7 for the ADR-003 re-issue rather than silently added here.

## 5. The envelope

`event-envelope.schema.json` is normative; this table is the readable form. The envelope is the entire `common.outbox_message.payload`; the columns beside it are copies for dispatch, never a second source of truth.

| Property | Type | Outbox column | Content |
| --- | --- | --- | --- |
| `eventId` | uuid | `id` | Generated by the producer |
| `eventType` | string | — | `<ProducerModule>.<EventName>` (EV-1) |
| `schemaVersion` | integer ≥ 1 | — | Version of `data`'s schema (EV-7) |
| `kind` | enum | `message_type` | `DOMAIN_EVENT` \| `NOTIFICATION_INTENT` \| `AUDIT_EVENT` |
| `messageKey` | string ≤ 200 | `message_key` | `<eventType>:<idempotencyKey>` — unique with `kind` |
| `idempotencyKey` | string ≤ 150 | — | The producer's natural key (EV-4) |
| `occurredAt` | date-time UTC | `occurred_at` | When the producer committed the fact |
| `sourceModule` | enum of 21 | `source_module` | Producer |
| `correlationId` | uuid | — | From the originating request (api-conventions R-42) |
| `causationId` | uuid or null | — | `eventId` of the event whose handling produced this one; null for a request |
| `actor` | `{ actorType, userId }` | `created_by` | `USER` \| `SERVICE` \| `INTEGRATION`; `userId` is `User.id` (service principals are users, ERD D-2) |
| `subject` | `{ module, type, id, revisionNo }` | — | Opaque aggregate reference (M-4, D-15); `revisionNo` null only for unrevisioned aggregates |
| `scope` | `{ projectId, departmentId, externalEntityId }` | — | Authorization and routing anchors supplied by the producer (M-7); each nullable |
| `data` | object | — | The event body; schema `<eventType>.v<schemaVersion>` (EV-3) |

`additionalProperties` is `false` at every level of the envelope. A producer that needs another field is proposing an envelope revision, not adding a property.

## 6. The rules

| # | Rule |
| --- | --- |
| **EV-1** | **Name: `<ProducerModule>.<EventName>`**, both PascalCase, the module from TASK-007 §4.3. A domain or audit event names a **fact in the past tense** — `ProjectIntakeRecorded`, `ApprovalOutcomeRecorded`, `ConcernEscalated`, `BaselineActivated` — never an instruction (`ActivateProject`) and never a state (`ProjectActive`). A notification intent carries the same name as the occurrence that produced it, so `NotificationIntent.source_event_type` (ERD) is the `eventType`. The event's C# type is `<EventName>` in `Features/<Producer>/Contracts/Events`; its data schema is published as `<eventType>.v<schemaVersion>`. |
| **EV-2** | **One envelope** (§5, `event-envelope.schema.json`) for every kind. No kind adds or omits an envelope property; the differences are in `data`. |
| **EV-3** | **`data` carries identifiers and the facts the consumer needs to act without calling back** — and nothing else. Never the full aggregate; never a consumer-specific field (a second consumer with different needs queries the producer, M-4); never a value classified sensitive under ADR-010 (a notification intent carries a reference and a deep link, not the figure — the ADR-004 SMS rule, and TASK-083's redaction rule for logs, both follow from this); never a raw secret or token. Value representation follows api-conventions R-15 to R-19: UTC timestamps, `MoneySar` strings, `BilingualLabel` and `Narrative` objects, UPPER_SNAKE enums equal to the ERD state names. |
| **EV-4** | **Idempotency is the producer's natural key.** `idempotencyKey` is the identity of the occurrence in the producer's own tables — `ProjectIntake.id`, `ConcernEscalation.id`, `ApprovalInstance.outcome_idempotency_key`, `ReportJob.id` — never a fresh uuid, never the HTTP `Idempotency-Key` (R-39), never the `correlationId` (R-45). `messageKey` is `<eventType>:<idempotencyKey>`, and the outbox's unique `(message_type, message_key)` makes a duplicate *publish* impossible. A duplicate *delivery* (EV-5) is made harmless by the consumer storing the key in its own row under a unique constraint: `NotificationIntent (source_event_type, source_reference)`, `ProjectBaseline.project_intake_id` for a DECLARED baseline, `ApprovalInstance.outcome_idempotency_key` on the source's side. A consumer with no natural column for the key gets one in the ERD; there is no generic "processed events" inbox table, because it would be a second place the fact lives (D-8). The one consumer that lacks such a column today is AuditActivity — S-8. |
| **EV-5** | **Delivery is at-least-once, unordered across subjects, ordered within a subject only by (`occurredAt`, `subject.revisionNo`).** A consumer is idempotent (EV-4), tolerates reordering, and treats an event as *a fact that happened*, not as *the current state* — it queries the producer for the current state when it needs it (M-4). An outcome for a `subject.revisionNo` that is no longer current is rejected as stale and audited, not applied (TASK-035: a returned request creates a new revision and a new instance). |
| **EV-6** | **Commit before publish, dispatch after commit.** The producer writes the outbox row inside its own transaction (M-5, M-6; Blueprint Sections 15 and 18); a hosted-service dispatcher in `PMPlatform.Infrastructure` reads undispatched rows after commit and invokes each consumer's handler **in its own transaction**, one per (event, handler). No message broker at launch: the platform is one deployable (ADR-003 §5 point 1) and the dispatcher is the seam a broker would later replace (§5 point 5). Retry: five attempts with exponential back-off from `next_attempt_at`; after the fifth, the row stays undispatched with `last_error` set and is surfaced by the readiness check (TASK-091, "outbox backlog") and as an `OperationalAlert` (TASK-075). A failed handler never rolls back the producer's transaction (TASK-039's criterion) — it cannot, because that transaction committed before dispatch began. |
| **EV-7** | **`schemaVersion` versions `data`.** Adding an optional property keeps the version; removing or retyping a property, or adding a required one, increments it. The producer emits only the current version. Because producer and consumers ship in one release, a consumer handles the current version and the one before it (N−1) for one release, so rows still undispatched at deploy time are not lost. The envelope itself is versioned by its `$id` (`…:event-envelope:1`) and changes only by revising this record. |
| **EV-8** | **Placement.** The event type lives in `Features/<Producer>/Contracts/Events/`; handlers live in `Features/<Consumer>/EventHandlers/` and reference only the producer's `Contracts` (TASK-007 A-1). The producer never references a handler. Every event type derives from `EventEnvelope<TData>` in `Application/Common` (M-10) and is registered in §4 (A-10). A-6 already sees the resulting edge and fails if ADR-003 §8.2 does not allow it. |
| **EV-9** | **Audit events** carry `AuditEventData`: `eventClass` (one of the ERD's nine: `AUTHENTICATION`, `AUTHORIZATION_DENIAL`, `PRIVILEGED_ACTION`, `PERMISSION_CHANGE`, `LIFECYCLE_TRANSITION`, `DATA_CHANGE`, `APPROVAL_DECISION`, `INTEGRATION`, `CONFIGURATION_CHANGE`), `outcome` (`SUCCESS` \| `DENIED` \| `FAILED`), `dataClassificationItemId`, and `attributes[] { name, oldValue, newValue }` **already redacted by classification in the producer** — AuditActivity stores what it receives and never re-derives it (M-6). The hash chain, the SIEM forwarding subset (PTBC-029) and the Activity projection are AuditActivity's and not visible in the contract. The mandatory classes (PTBC-011) decide *when* one is emitted; this rule decides only *what* it contains. |
| **EV-10** | **Notification intents** carry `NotificationIntentData`: `eventFamilyCode` (an FG-04 `NotificationEventFamily` code — the routing key into the ADR-004 matrices), `sourceReference` (= `idempotencyKey`), `scheduledFor` (null for immediate), `deepLink` (an SPA route, never a URL with a token), `parameters[] { name, value }` for template rendering (strings only, none sensitive — EV-3), and, for reminders, `condition { subjectType, subjectId, satisfiedWhenStatusIn[] }`, which Notifications re-evaluates before sending (§4, S-7). **Recipients are never named in the intent**: Notifications resolves them from `eventFamilyCode` and `scope` through FG-04's recipient-role matrix and FG-03's eligibility recheck (TASK-039), which is what keeps E-U4 free of any knowledge of who reads what. |
| **EV-11** | **Approval outcomes** carry `ApprovalOutcomeData`: `approvalInstanceId`, `routingKey`, `decision` (`APPROVED` \| `REJECTED` \| `RETURNED` \| `WITHDRAWN`), `decidedAt`, `decidedByUserId`, `authorityConfigurationVersionId` (the pinned authority version, ERD D-13). The consumer is `subject.module`; it applies the decision to `subject.id` at `subject.revisionNo` exactly once (EV-4, EV-5) and produces its own `AUDIT_EVENT` for the resulting transition, so approval completion and lifecycle activation are "two distinct, separately auditable events" (TASK-062). Approval holds no type from any source module (M-8): the data schema is the same for all eight producers of approvals. |
| **EV-12** | **Schemas are published, not hand-written.** JSON Schema 2020-12 is generated from the C# contract types in the same CI step that generates OpenAPI (api-conventions R-49), one file per `<eventType>.v<schemaVersion>`, each an `allOf` of the envelope with `data` constrained, under `docs/api/events/`. The set of generated schemas must equal §4 plus the per-task rows 6 and 7 (A-10). `contract-check.py` validates every sample under `event-samples/` against the envelope and fails on an unknown property, a non-UTC timestamp, a `messageKey` that is not `<eventType>:<idempotencyKey>`, or an `idempotencyKey` equal to the `correlationId`. |

## 7. Sample events

Three samples under `event-samples/`, one per kind that has a fixed data schema, matching the three sample endpoints of api-conventions §7:

| File | `eventType` | Kind | Shows |
| --- | --- | --- | --- |
| `project-intake-recorded.json` | `Project.ProjectIntakeRecorded` | DOMAIN_EVENT | Edge 35; four consumers each writing their own fact from one event (solution architecture §11.1); `MoneySar` strings; `subject.revisionNo` null for an unrevisioned aggregate; `causationId` null for a user action |
| `approval-outcome-recorded.json` | `Approval.ApprovalOutcomeRecorded` | DOMAIN_EVENT | Edge 28; `subject` is a `ChangeRequest` at `revisionNo` 2; `idempotencyKey` is the ERD's `outcome_idempotency_key`; the pinned authority configuration version |
| `concern-escalated-intent.json` | `ManagementConcern.ConcernEscalated` | NOTIFICATION_INTENT | E-U4; `sourceReference` = `ConcernEscalation.id` (TASK-057's "exactly once per escalation event"); a deep link and rendering parameters, no recipient and no sensitive value |

```
$ python3 docs/architecture/contract-check.py
api-conventions-samples.openapi.json: 3 operations, 3 event samples, 0 findings
```

The six event mutations of the self-test (missing `subject`, an `eventType` without a module prefix, a `messageKey` mismatch, an unknown envelope property, a non-UTC `occurredAt`, and `idempotencyKey` equal to `correlationId`) are all caught (api-conventions §7).

## 8. Consequences

Event-specific consequences for the workbook and the two upstream records, specified not applied (TASK-001 §4; authorisation is api-conventions Q3). The API-side consequences are api-conventions §9.

| # | Where | Change | Basis |
| --- | --- | --- | --- |
| 1 | **ADR-003 §8.2** (re-issue) | Add a *query* edge Notifications → source module for reminder condition revalidation (§4, EV-10). Until it is added, A-6 will fail on the first reminder handler | TASK-039 acceptance criterion; S-7 |
| 2 | **TASK-008 ERD** (re-issue) | `audit_activity.audit_event.event_id uuid not null unique` — the envelope's `eventId` — so that an at-least-once delivery cannot insert the same audit event twice into an APPEND_ONLY, hash-chained table | EV-4, EV-5; S-8 |
| 3 | **TASK-008 ERD** (re-issue) | `common.outbox_message`: `message_type` note reads "= EventEnvelope.kind"; `payload` note references `event-envelope.schema.json`; add `correlation_id uuid not null` as a dispatch-side copy so the dispatcher can log without parsing the payload | §5 |
| 4 | **TASK-039, TASK-073, TASK-075** | Add to Detailed Description: consumes `NOTIFICATION_INTENT` / `AUDIT_EVENT` / `IntegrationInvocationCompleted` per `event-conventions.md` EV-10 / EV-9 / §4; the dispatcher's dead-letter surfaces as an OperationalAlert (TASK-075) and in readiness (TASK-091) | EV-6 |
| 5 | **TASK-035** | The "source-callback contracts" deliverable is `Approval.ApprovalOutcomeRecorded` with `ApprovalOutcomeData` (EV-11), consumed by each source module's handler — not a per-source callback interface | M-8, EV-11 |
| 6 | **TASK-104** | Names `Project.ProjectIntakeRecorded` (§4 row 1) as the path by which the four owning modules write their facts | solution architecture §11.1 |
| 7 | **TASK-011** | `EventEnvelope<TData>`, the outbox writer, the dispatcher hosted service and A-10 in the skeleton | EV-6, EV-8 |

## 9. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-20 | Initial record. Three kinds on one envelope mapped to ADR-003's dashed edges and the ERD's outbox (§3, §5); event catalogue of five named events and two families with blank INT/EVT columns (§4); twelve rules EV-1 to EV-12 (§6); three sample events validated by `contract-check.py` (§7); seven consequences including the missing Notifications → source query edge (S-7) and the audit-event dedupe column (S-8) (§8). | Architecture (TASK-009) |

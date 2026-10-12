# Formal Audit & Activity Service (FG-06 Backend)

| Field | Value |
| --- | --- |
| Task | TASK-073 — Build Formal Audit & Activity Service (FG-06 Backend) (P13 - Technical Operations (FG-05/06)) |
| Depends on | TASK-024/025 (core platform schema), TASK-028/029 (AD/LDAP, SSO and MFA sign-in). Builds on TASK-033 (`auth-access-audit-logging.md`): the `IAuditTrail` seam every module records through, the three `audit_activity` tables, the database-owned hash chain and append-only triggers, and SIEM forwarding. Uses TASK-030's authorization engine (`authorization-engine.md`) and TASK-035's outbox (`event-conventions.md` EV-6) |
| Record date | 2026-10-12 |
| Status | **BUILT AND VERIFIED LOCALLY** against `AHDA-postgres` and `AHDA-ldap`, in process through the real API pipeline (§7), and on the DEV compose stack built from this branch (§7.3) |
| Branch | `feat/task-073-fg06-audit-activity-backend` |
| Directory | `src/backend/PMPlatform.Application/Features/AuditActivity` |
| Deliverables | **Append-only audit store:** `audit_event.attribute_digest`, covered by the chained hash (migration `TASK-073_CreateActivityProjection`); `AuditEvidenceGuard`, which refuses any update or delete of committed evidence in the data layer; `AuditIntegrityVerifier`. **Outbox-based durable capture:** `AuditTrail` → `AuditCapture` (validated, redacted) → `IAuditCaptureStore` (an `AUDIT_EVENT` outbox message in the producer's unit of work) → `IAuditIngestor` (stored once, at commit by `AuditCommitInterceptor` and by `AuditIngestionWorker`). **Activity projection builder:** `ActivityProjection` (the one rule), `ActivityTemplateCatalogue` (bilingual lines for 187 event types), `IActivityProjectionBuilder` with its checkpoint, `business_activity_entry` (I-48), `ActivityProjectionWorker`. **Rebuild tooling:** `IActivityProjectionBuilder.CheckAsync` / `RebuildAsync`, exposed as `POST /api/v1/activity-projection-checks` and `-rebuilds` and as the release image's `activity-projection-check` and `activity-projection-rebuild` commands, with `audit-integrity-verify`. **Audit read API:** ADM-050/051 (`/audit-events`), SCR-057 (`/activity-entries`), `/audit-access`, `/audit-ingestion-status`, authorised by category, scope and sensitivity. Nine permissions. This record |
| Environment variables / secrets | None. `Audit:Ingestion` (`PollInterval` 5 s, `BatchSize` 100, `RetryBaseDelay` 5 s, `MaxRetryDelay` 5 min, `LockTimeout` 5 s) and `Audit:Activity` (`PollInterval` 5 s, `BatchSize` 500) have defaults in code |
| Gate decision applied | None in the workbook row. PTBC-011 (Audit Reviewer, hold and disposal authorities) and PTBC-029 (SIEM catalogue) remain Open; the reviewer grants here are configuration (D-11, F-2) |
| Participation amendment | **ADR-013**: an entity Project Manager's actions are audited identically to an internal user's — the same trail, event types, actor type and attribution, with no branch on user type (D-1; `AuditCaptureTests.TheEntityProjectManagerIsAuditedAsAnInternalUserIs`) — and an entity sees its own activity history: `ACTIVITY_VIEW` ships to R08 at ENTITY and to R04 at OWN, cross-entity isolation holds, and AHDA's internal actors are not named to an entity (D-12; `AuditAccessTests.AnEntitySeesItsOwnActivityHistoryAndNothingElse`) |
| Sources read | The TASK-073 row as supplied on 2026-10-12, checked against the workbook's Implementation Plan row (branch, validation checks). The **FG-06 Audit & Activity Functional Specification v1.0** (`Project Files.zip`), read whole. **Blueprint v2.0** §18 and Appendix A (the indicative matrix's "Audit / activity" row). `auth-access-audit-logging.md`, `event-conventions.md`, `solution-architecture.md` (§4.5, M-6, M-7), `authorization-engine.md`, `erd.md` §5.22, `indexing-strategy.md` |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | Capture of every audit class through the transactional outbox, ingestion into the authoritative store once committed, idempotently, with validation and secret redaction (FG-06 §4.1–4.3) | This task |
| **In** | Tamper evidence over the event's attributes as well as its row; refusal of any change to committed evidence in the data layer as well as the database; integrity verification of the whole chain (§4.4, AUD-CC-27) | This task |
| **In** | The business Activity projection, derived from the store, kept apart from it and from operational logs, rebuildable, checked and rebuilt by API and command (§4.5, §7) | This task |
| **In** | ADM-050/051 and SCR-057's backend: audit read by category, scope and sensitivity, distinct from R01; Activity by current project access (§6, §8) | This task |
| **Out** | Audit screens ADM-050–053, SCR-057/058 | TASK-074 |
| **Out** | Retention classes, archive, holds and governed disposal (§10); FG-02 audit export (§11.1); filter-value lists (AUD-API-015–017); correlation/causation lineage views (AUD-API-046); ADM-052 system events and ADM-053 integration audit | F-4 |
| **Out** | The redaction policy's field registry and personal-data masking by classification | TASK-083 |
| **Out** | Running the API as a role that owns nothing (so no role can switch the triggers off) | DevOps, F-1 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Capture through the outbox, behind the same seam.** `IAuditTrail.Stage` now writes an `AUDIT_EVENT` message to `common.outbox_message` in the producer's unit of work; `RecordAsync` writes one in a unit of work of its own. No producer changed: the ~300 event types of 19 modules keep calling `IAuditTrail`. Because every class is captured inside the producer's transaction, an event that cannot be captured fails its action: all nine classes are fail-closed (BR-AUD-010; TBC-AUD-03's strictest reading, F-5). | M-6 and FG-06 §4.1 ask for "a durable audit/outbox record transactionally with the material action"; TASK-033 D-2 had deferred the outbox to this task and noted that only `AuditTrail` would change. |
| D-2 | **Ingestion at commit, and by a worker.** `AuditCommitInterceptor` (an EF save and transaction interceptor, scoped with the request) ingests the messages a context staged once that context's save — or the explicit transaction around it — has committed, then brings the Activity projection up to date. So an event is stored before the request that caused it answers. `AuditIngestionWorker` stores whatever was not (the API stopped, the store refused, a lock timed out). Each message is ingested in a transaction of its own: locked `FOR UPDATE SKIP LOCKED`, read, validated, inserted unless already stored, marked dispatched. A failure is rolled back and recorded on the message (`last_error`, `attempt_count`, an exponential back-off capped at 5 minutes) and retried for as long as it takes. A `SET LOCAL lock_timeout` keeps ingestion from ever waiting on a lock its own request still holds. | §4.2's RECEIVED → VALIDATED → STORED with FAILED_INGESTION "recoverable from durable queue/outbox". The chain's advisory lock is now held for one event's ingestion, not for a whole business transaction (TASK-033 F-7 narrowed). Evidence is never dead-lettered: an audit event that cannot be stored is a defect to fix, not a message to drop. |
| D-3 | **The payload is AuditActivity's capture record, not the envelope.** `AuditCapture` carries the event id, schema version, time, class, type, outcome, actor, subject, anchors, classification, correlation id and attributes, serialised as the outbox serialises envelopes. | The envelope requires a subject and an acting user (event-envelope.schema.json, `Outbox.Stage`); a refused anonymous sign-in has neither. Only AuditActivity writes and reads the message, so it crosses no module boundary. `event-conventions.md` §3 records it. |
| D-4 | **The stored event's id is the captured event's id.** Ingestion stores a capture only if no event has its id, inside the transaction that locked its message, so a repeated delivery or a replay stores nothing (AUD-CC-07). No `event_id` column is added (event-conventions S-8 closed). | EV-4: the consumer keeps the idempotency key under a unique constraint; the primary key is one. |
| D-5 | **The chain covers each event's attributes.** Ingestion stores `attribute_digest`, SHA-256 over the attributes in name order, each name and value written as `-` (null) or `+<UTF-8 length>:<value>`. The migration redefines `audit_event_hash` to include it; `concat_ws` skips a null argument, so every event stored before this task hashes exactly as it did and the existing chain stays valid (`AuditActivitySchemaTests.TheHashCoversTheAttributeDigest`). Verification recomputes the digest from the stored attributes and the hash from the stored row. | TASK-033 F-2: an attribute could be changed undetected by anyone able to switch the triggers off. |
| D-6 | **Validated and redacted at capture, and again at ingestion.** An event type must be `<Module>.<Name>`; class, outcome and actor type known; a SERVICE event names its principal; a subject complete; attribute names identifiers, unique. A malformed event throws at capture, so its producer's action fails rather than store doubtful evidence; at ingestion it is refused with its FG-06 §22 code (`AUDIT_SCHEMA_UNSUPPORTED`, `AUDIT_EVENT_TYPE_UNSUPPORTED`, …). An attribute named as a secret (`password`, `secret`, `token`, `credential`, `private_key`, `api_key`, `authorization_header`, `cookie`) or whose value has the shape of a bearer or basic credential, a JWT or a PEM private key keeps its name and loses its value: `[REDACTED]`. | AUD-CC-14, §25.2. Redaction happens before the outbox, so a secret never reaches any table. No declared attribute is a secret by this rule (`NoDeclaredAttributeIsTakenForASecret`). |
| D-7 | **Committed evidence cannot be edited or deleted — in three layers.** No API route writes an event (`PUT`, `PATCH`, `DELETE` on `/audit-events/{id}` are 405). `AuditEvidenceGuard`, a save interceptor on every context, refuses a save that would update or delete an `IAuditEvidence` row (`AuditEvent`, `AuditEventAttribute`) with `AUDIT_EVENT_IMMUTABLE`, before any SQL is sent. TASK-033's triggers refuse `UPDATE`, `DELETE` and `TRUNCATE` from every role, the application's included (42501). | The acceptance criterion: "enforced at the data-access layer, not only the UI". Whatever code path an administrator's request takes, the context and then the database refuse. Only a role that owns the tables can switch the triggers off; that change is detected (D-5, D-13), not prevented (F-1). |
| D-8 | **The Activity projection is derived, separate and checkpointed.** `ActivityProjection.Project` is the one rule: a successful `LIFECYCLE_TRANSITION`, `DATA_CHANGE` or `APPROVAL_DECISION` with a project scope whose type has a template becomes one `business_activity_entry` line — the template's Arabic and English text, the source domain (`activity_kind`, e.g. `RISK`), the time and the actor. A pass reads stored events after the checkpoint in commit order (`recorded_at`, strictly increasing under the chain lock), projects them and moves the checkpoint, in one transaction holding the checkpoint row. A failure leaves the store and the checkpoint untouched (FAILED_INDEXING). | FG-06 §3.1: "Audit Store is authoritative evidence. Search Index and Activity Projection are rebuildable derived views", and BR-AUD-005/006 keep Activity apart from Audit and from logs. A function of the store alone is what makes a rebuild reproduce it. |
| D-9 | **Check and rebuild apply the same rule.** A check compares, batch by batch, what the store derives for the events up to the checkpoint with the stored lines — each Missing, Unexpected or Changed — and reports the events after the checkpoint as pending lag, not differences. A rebuild compares every event, adds, removes or repairs each differing line and moves the checkpoint to the head. Both run through the API (`AUDIT_OPERATIONS_MANAGE`) and as commands of the release image, which exit 1 when a check finds a difference or a verification finds tampering. Each run is audited (`AuditActivity.ActivityProjectionChecked`, `.ActivityProjectionRebuilt`) by the person, or by `svc.audit-capture` from the command line. | The acceptance criterion's rebuild-and-diff, and the validation check's "rebuild the Activity index from Audit and diff against the live projection, confirming zero unexplained differences": lag is the one explained difference, reported apart. |
| D-10 | **Audit is read by category, scope and sensitivity.** An event's category follows from its class and, for business classes, its producer: SECURITY (authentication, refusals), ADMINISTRATION (privileged actions, permission and configuration changes), INTEGRATION, FINANCIAL (WF-14), EXTERNAL (WF-13), BUSINESS (the rest). Each has its own read permission in a group of its own, so holding one category makes another's event a 404, like an unknown id (R-47). The scope is the permission's data scope over the event's anchors as they are now: its project's owning department and Project Manager, and its entity (the event's, else its project's). The sensitivity is the permission's clearance over the event's classification. Search applies all three in SQL, before counting (AUD-CC-20); naming a category one does not hold is 403 and audited; an event out of reach is 404 and audited. The client address and the name typed at sign-in are request context, shown only with `AUDIT_CONTEXT_VIEW` and otherwise named in `maskedAttributes`. Opening a SECURITY event, a classified event, or request context records `AuditActivity.SensitiveDetailViewed`; search and paging are not audited. | FG-06 §6, AUD-CC-02 to -05, -20, -31; BR-AUD-023: "Audit access requires explicit permission and is not implied by R01". The engine already evaluates permission, scope and clearance (M-7): AuditActivity supplies the anchors. |
| D-11 | **Shipped grants: R01 reads administration audit only.** R01 holds `AUDIT_ADMINISTRATION_VIEW` and `AUDIT_OPERATIONS_MANAGE` at ALL (Appendix A: "V technical, immutable") and nothing of the other five categories or of request context. The Audit Reviewer and Security Reviewer of FG-06 §6.3 are assignments AHDA has not named (TBC-AUD-05 to -07, PTBC-011): no role ships with them; a permission profile grants them (TASK-110). | The acceptance criterion: "Audit access is separately authorized … distinct from general R01 access". |
| D-12 | **Activity follows current access, and keeps entities apart.** `/activity-entries` lists the lines of the projects the caller reaches under `ACTIVITY_VIEW` now — R04 at OWN (the projects the holder manages), R08 at ENTITY (Appendix A "V own history"; ADR-013) — with the line's event classification against the permission's clearance. An external viewer sees an actor only when the actor is an external user of the viewer's own entity; AHDA's people are not named. A line carries no value, name, title or note of the record: what changed is the template's fixed wording. | §6.2 (normal users follow current access), §7.4 (no internal approver details, no restricted title, no sensitive value), AUD-CC-16 and -18. |
| D-13 | **Operating the store is its own permission.** `AUDIT_OPERATIONS_MANAGE` runs integrity verification (whole chain, every digest; AUD-API-037), the projection check and rebuild, and reads the pipeline's status: pending and failing messages with the last error code, events stored, the store's head and the projection's lag (AUD-API-039/040). It reveals counts and event ids, never content. | FG-06 §24.4 self-monitoring; an operator needs no audit category to keep the store healthy. |

## 3. How it fits together

```
producer service ──Stage(entry)──► IAuditTrail (AuditTrail: validate, redact, add correlation id and client address)
                                        │ AuditCapture
                                        ▼
                     IAuditCaptureStore ── outbox_message (AUDIT_EVENT) ── committed with the producer's change, or not at all
                                        │ after commit (AuditCommitInterceptor)      │ later (AuditIngestionWorker)
                                        ▼                                            ▼
                                   IAuditIngestor: lock message ─ validate ─ insert unless stored ─ mark dispatched   (one transaction)
                                        │                    └──► audit_event (+ attributes, + digest)  ◄── BEFORE INSERT: chain lock, recorded_at, hash
                                        │                    └──► audit_forwarding_record (PENDING) ─► SiemForwardingWorker ─► SIEM
                                        ▼
                     IActivityProjectionBuilder: checkpoint FOR UPDATE ─ events after it ─ ActivityProjection.Project ─► business_activity_entry
                                        ▲                                                                        (rebuild / check: same rule)
                                        └── ActivityProjectionWorker, /activity-projection-checks|-rebuilds, activity-projection-check|-rebuild

reads:  /audit-events ── engine: category permission ─ RecordScope over current project anchors ─ clearance  (AuditReadRepository)
        /activity-entries ── engine: ACTIVITY_VIEW scope ─ actor named only within the viewer's entity
```

## 4. Audit access

| Category | Classes and producers | Read permission | Shipped |
| --- | --- | --- | --- |
| SECURITY | `AUTHENTICATION`, `AUTHORIZATION_DENIAL` | `AUDIT_SECURITY_VIEW` | — |
| ADMINISTRATION | `PRIVILEGED_ACTION`, `PERMISSION_CHANGE`, `CONFIGURATION_CHANGE` | `AUDIT_ADMINISTRATION_VIEW` | R01 at ALL |
| INTEGRATION | `INTEGRATION` | `AUDIT_INTEGRATION_VIEW` | — |
| FINANCIAL | business classes produced by FinancialKpi | `AUDIT_FINANCIAL_VIEW` | — |
| EXTERNAL | business classes produced by ExternalParticipation | `AUDIT_EXTERNAL_VIEW` | — |
| BUSINESS | every other `LIFECYCLE_TRANSITION`, `DATA_CHANGE`, `APPROVAL_DECISION` | `AUDIT_BUSINESS_VIEW` | — |
| — | request context (client address, name typed at sign-in) | `AUDIT_CONTEXT_VIEW` | — |
| — | the store's operations | `AUDIT_OPERATIONS_MANAGE` | R01 at ALL |
| — | the Activity timeline | `ACTIVITY_VIEW` | R04 at OWN, R08 at ENTITY |

An event with no project and no entity (a sign-in, a configuration change) is reached only by an ALL-scope grant. An external user never reaches an event of no entity or of another entity, whatever the grant (ADR-013, the engine's isolation rule).

## 5. API

| Operation | Permission | Answer |
| --- | --- | --- |
| `GET /api/v1/audit-events` (`category`, `eventClass`, `outcome` sets; `occurredFrom`/`To`, `actorUserId`, `eventType`, `projectId`, `subjectType`, `subjectId`, `correlationId`; paged, newest first) | per category | 200 page of the events the caller reaches; 403 for a named category not held |
| `GET /api/v1/audit-events/{id}` | the event's category, scope, clearance | 200 detail with attributes, `maskedAttributes`, integrity (`eventHash`, `previousEventHash`, `attributeDigest`), SIEM forwarding; else 404 |
| `PUT`, `PATCH`, `DELETE /api/v1/audit-events/{id}` | — | 405: no such operation |
| `GET /api/v1/audit-access` | signed in | the caller's categories, `requestContextVisible`, `activityVisible`, `operationsPermitted` |
| `GET /api/v1/activity-entries` (`projectId`, `subjectType`, `subjectId`, `activityKind` set, `actorUserId`, `occurredFrom`/`To`; paged) | `ACTIVITY_VIEW` | 200 page of lines |
| `GET /api/v1/audit-ingestion-status` | `AUDIT_OPERATIONS_MANAGE` | pending, failing, oldest pending, last error code, events stored, store head, projection lag and checkpoint |
| `POST /api/v1/audit-integrity-verifications` (sensitive write) | `AUDIT_OPERATIONS_MANAGE` | `AuditIntegrityReport`: events verified, broken links, digest mismatches, events without a digest, findings |
| `POST /api/v1/activity-projection-checks` (sensitive write) | `AUDIT_OPERATIONS_MANAGE` | `ActivityProjectionReport` with `matches` and `pendingEvents` |
| `POST /api/v1/activity-projection-rebuilds` (sensitive write) | `AUDIT_OPERATIONS_MANAGE` | `ActivityProjectionReport`, `rebuilt: true` |

Commands of the release image (`dotnet PMPlatform.Api.dll <command>`, run like `migrate` as an execution of the environment's job): `audit-integrity-verify`, `activity-projection-check` (both exit 1 on a finding) and `activity-projection-rebuild`. `GET /audit-access` and `/audit-ingestion-status` are single representations the convention checker reads as collections (C-9), recorded in `AuditActivityContractTests` as FinancialKpi's aggregates are.

## 6. Verification

All run on 2026-10-12, macOS, against `AHDA-postgres` (PostgreSQL 17) and `AHDA-ldap`, from the repository root.

| # | Command | Result |
| --- | --- | --- |
| 1 | `dotnet build src/backend -warnaserror` (Debug and Release) | 0 warnings, 0 errors |
| 2 | `dotnet test src/backend/PMPlatform.Tests.Unit` | **1798 passed** (1653 on `dev`): +145, of which `AuditTrailTests` 9, `AuditRecordFactoryTests` 4, `AuditAttributeDigestTests` 3, `AuditCaptureRulesTests` 20, `ActivityTemplateCatalogueTests` 4, `ActivityProjectionTests` 10, `ActivityProjectionBuilderTests` 3, `AuditCategoriesTests` 10, `AuditIntegrityVerifierTests` 5, `ShippedGrantTests` 2 new facts; the rest are the shipped-grant theories' cells for the nine new permissions (counts as `--list-tests` lists them). Architecture tests A-1 to A-6 unchanged and green |
| 3 | `dotnet test src/backend/PMPlatform.Tests.Integration` | **817 passed** (784 on `dev`), including `Persistence` and `RegisterQueryPlanTests`. New: `AuditCaptureTests` 6, `AuditImmutabilityTests` 10, `ActivityRebuildTests` 2, `AuditAccessTests` 6, `AuditIntegrityTests` 4, `AuditActivityContractTests` 4, `AuditActivitySchemaTests.TheHashCoversTheAttributeDigest` 1. Updated: the permission count (84), R01's grant count (18), the bilingual label count (404), the audit tables (5), and `ConcernEscalationTests`, which counted outbox rows by key alone and now names the notification-intent type (an audit event shares the event type, event-conventions §3) |
| 4 | `.github/scripts/migration-dry-run.sh` (scratch database) | 3966 statements, applied twice, no destructive statement |
| 5 | `.github/scripts/seed-dry-run.sh` (same database) | 21 tables seeded, unchanged by a second run, no integrity violation |
| 6 | `docs/architecture/*-check.py`, `contract-check.py --self-test` | All OK; `erd-check.py` 124 tables; self-test 29/29 |
| 7 | OpenAPI snapshot | +8 paths and +21 schemas, nothing else changed. `contract-check.py` on the snapshot: 31 new findings, all on the AuditActivity surface — C-4, C-5, C-12 8 each and C-7 3 (the platform-wide classes `OpenApiDocument.OpenPlatformFindings` records), C-9 4 (the two single representations, §5) |

### 6.1 Mutation tests: each protection fails a test when removed

`artifacts/task-073/mutations/run.py` (not committed) applied each mutation alone, ran the unit tests and the AuditActivity integration tests with `AuditActivitySchemaTests`, and restored the file. **21 of 21 killed.**

| # | Mutation | Killed by |
| --- | --- | --- |
| M-1 | The data layer lets a change to committed evidence through | `TheDataLayerRefusesToChangeCommittedEvidence` |
| M-2 | A secret is captured as the producer handed it | `ASecretIsRedactedBeforeItIsCaptured` |
| M-3 | A malformed event is captured | `AMalformedEventIsRefusedAtCapture` |
| M-4 | Ingestion stores an event again on a repeated delivery | `ARepeatedDeliveryStoresNothingTwice` |
| M-5 | Ingestion stores a capture it should refuse | `AMessageThatCannotBeStoredStaysInTheOutboxWithItsErrorUntilItCan` |
| M-6 | The attribute digest is not stored | `AnEventCommitsWithItsChangeAndIsStoredAndProjectedAtOnce`, `AnAttributeChangedOutOfBandIsFound` and three more |
| M-7 | The chain does not cover the attribute digest (the migration's function) | `TheHashCoversTheAttributeDigest` |
| M-8 | The commit path does not ingest | both `ActivityRebuildTests`, `AnEventCommitsWithItsChangeAndIsStoredAndProjectedAtOnce` and others |
| M-9 | A refusal recorded on its own is not ingested at once | `RequestContextIsShownOnlyToWhoMayReadIt`, `AnAdministratorReadsAdministrationAuditAndNothingElse` and others |
| M-10 | A refused or failed event becomes Activity | `AnythingElseIsLeftOut` |
| M-11 | A projection pass does not move the checkpoint | both `ActivityRebuildTests`, `TheIngestionStatusShowsTheStoreAndTheProjectionUpToDate`; `TheLiveProjectionIsWhatARebuildDerives` looped instead of failing, and its loop is now bounded |
| M-12 | A rebuild keeps a line the store does not derive | `ACheckFindsEveryLineThatDiffersAndARebuildRepairsIt` (both suites) |
| M-13 | Search ignores the category | `AnAdministratorReadsAdministrationAuditAndNothingElse` |
| M-14 | An event is read under the administration permission whatever its category | `AnAdministratorReadsAdministrationAuditAndNothingElse` |
| M-15 | A department scope reaches every department | `AReviewerSeesTheProjectsTheirScopeReachesAndNoOther` |
| M-16 | Clearance is not applied to a classified event | `AnEventAboveTheReviewersClearanceIsHidden` |
| M-17 | Request context is shown to every reader | `RequestContextIsShownOnlyToWhoMayReadIt` |
| M-18 | An external viewer sees AHDA's internal actors | `AnEntitySeesItsOwnActivityHistoryAndNothingElse` |
| M-19 | Opening sensitive evidence is not audited | `AnEventAboveTheReviewersClearanceIsHidden` |
| M-20 | The verifier ignores a changed attribute | `AnAttributeChangedOutOfBandIsFound`, `AChangedAttributeNoLongerHasItsDigest` |
| M-21 | R01 ships with security audit | `AdministratorsReadOnlyTheAdministrationCategory` |

### 6.2 The full suite found a producer the capture rule refused

The first full integration run had every Risk test fail at fixture set-up with 500: FG-04's configuration audit names attributes `riskRatings_count`, which the first attribute-name rule (snake case only) refused, so capture failed the publication (D-6, F-5). The rule now accepts any identifier; nothing else changed.

### 6.3 Validation check on the local DEV stack

`artifacts/task-073/live` (not committed): this branch's `ahda-migrate` and `ahda-api` images on a scratch database `task073_live`, the compose seed, TASK-056's risk fixture, and an MFA stand-in speaking `mfa-privileged-access.md` §4 so that local.r01 can sign in (it requires MFA and the stack has no provider). The API ran with default ingestion and projection settings.

| Step | Observed |
| --- | --- |
| Business activity | local.r02 registered two risks, assessed one and closed the other through the API. Four Activity lines on project A: "Risk registered" ×2, "Risk assessed", "Risk closed", each with its Arabic line, domain `RISK` |
| Request context | A failed sign-in recorded `client_address` `::ffff:192.168.65.1`. local.r02, granted security audit without request context for the check, saw `authentication_method` and `failure_reason`; `client_address` and `username` were masked |
| R01's access | `GET /audit-access`: categories `[ADMINISTRATION]`, no request context, no Activity, operations permitted. R01's list held only ADMINISTRATION events |
| **Check 1 — edit or delete as R01, through the API** | `DELETE`, `PUT`, `PATCH /audit-events/{id}` on an event R01 can read: **405** each. A business event by id: 404; `?category=SECURITY`: 403 |
| **Check 1 — through the data layer** | As the API's own database role: `DELETE FROM audit_activity.audit_event` — *"audit_activity.audit_event is append-only: DELETE is not allowed"*; `UPDATE audit_event`, `UPDATE audit_event_attribute` — refused the same way. The event's hash and outcome unchanged; `audit-integrity-verify`: exit 0, 13 events, 0 broken links, 0 digest mismatches |
| **Check 2 — rebuild and diff** | R01's API check: matches, pending 0. `activity-projection-check`: exit 0, 0 missing, 0 unexpected, 0 changed. The projection was then dropped (0 lines) and `activity-projection-rebuild` derived 4 lines from the store. **Live 4 lines, rebuilt 4 lines, identical; 0 differing lines.** `activity-projection-check` afterwards: exit 0 |

The in-process tests assert the same on every build (`AuditImmutabilityTests`, `ActivityRebuildTests`). SIT and UAT do not exist yet; this is the check's procedure on DEV.


## 7. Acceptance criteria and validation check

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Committed Audit events cannot be edited or deleted by an ordinary administrator (enforced at the data-access layer, not only the UI) | **MET.** No API route writes an event (405); the context refuses any update or delete of an event or attribute before SQL is sent (`AUDIT_EVENT_IMMUTABLE`); the database refuses the application's role for `UPDATE`, `DELETE`, `TRUNCATE` (42501). A table owner who switches the triggers off is detected by verification, not prevented (F-1) | D-7; `AuditImmutabilityTests` (10); M-1; §6.3 check 1 |
| 2 | Rebuilding the search/index from the Audit store reproduces the same Activity projection (verified by a rebuild-and-diff test) | **MET.** The live projection, built at commit as events arrive, equals what a rebuild derives, in memory and against PostgreSQL; a check finds every changed, missing and unexpected line, and a rebuild repairs them | D-8, D-9; `ActivityRebuildTests.RebuildingTheProjectionFromTheAuditStoreReproducesTheLiveProjection`, `.ACheckFindsEveryLineThatDiffersAndARebuildRepairsIt`; `ActivityProjectionBuilderTests`; M-11, M-12; §6.3 check 2 |
| 3 | Audit access is separately authorized by category/scope/sensitivity, distinct from general R01 access | **MET.** One permission per category, scope over current project anchors, clearance over the event's classification; R01 reads administration audit only | D-10, D-11; `AuditAccessTests` (6); `AuditCategoriesTests`; M-13 to M-17, M-21; §6.3 |
| V | Validation check: delete or edit a committed event via data layer/API as R01 and confirm rejection; rebuild the Activity index and diff against the live projection, zero unexplained differences | **Done on the local DEV stack.** Every attempt refused; 0 differences, the projection's lag reported apart (0) | §6.3 |
| — | ADR-013 participation amendment | **MET.** The entity Project Manager's withdrawal is audited with the same event, actor type and attribution as an internal user's; an entity reads its own projects' Activity with its own people named and AHDA's not | `AuditCaptureTests.TheEntityProjectManagerIsAuditedAsAnInternalUserIs`, `AuditAccessTests.AnEntitySeesItsOwnActivityHistoryAndNothingElse`; M-18 |


## 8. Findings

| # | Finding | Owner | Action |
| --- | --- | --- | --- |
| F-1 | **The owner of the audit tables can still switch the protection off** (TASK-033 F-4). Triggers bind every role but the owner, who can `DISABLE TRIGGER`; locally and, until DevOps splits the roles, in every environment the API connects as the owner. A change made that way is now found by verification for attributes as well as rows (D-5), not prevented | DevOps (TASK-020 roles) | Run the API as a role that owns nothing and holds only `SELECT`, `INSERT` on `audit_activity.audit_event`/`audit_event_attribute`; schedule `audit-integrity-verify` (TBC-AUD-21 sets its frequency) |
| F-2 | **Only R01, R04 and R08 ship with FG-06 grants.** Appendix A's asterisked cells — R02 "V business audit*", R03 "V dept audit*", R05 "V own changes", R07 "V summary*" — and FG-06's Audit Reviewer and Security Reviewer (TBC-AUD-05 to -07, PTBC-011) are not granted: no security, business, financial, external or integration audit is readable on a fresh install except through a permission profile AHDA authors | AHDA (PTBC-011) | Name the reviewer assignments and the scopes of the asterisked cells; they are grants, no code change |
| F-3 | **A foundation module reads two other modules' tables.** `AuditReadRepository` joins `project.project` (department, Project Manager, entity: the current anchors audit is authorised on) and `identity_access.user` (actor name, type and entity): AuditActivity may call no domain module (ADR-003 §4.5) and Project publishes no anchors contract for foundation modules — the same read as `document-management.md` F-6 | Engineering Architect (ADR-003) | Move the reads when a contract exists |
| F-4 | **Not built here.** Retention classes, archive, holds and governed disposal (§10; retention is still unset, TASK-033 F-12); FG-02 audit export (§11.1); filter-value lists (AUD-API-015–017); correlation/causation lineage (AUD-API-046; the store has no causation column); ADM-052 and ADM-053; FG-06's finer actor model (EXTERNAL_ENTITY_USER, SYSTEM_JOB) and delegation lineage, which the ERD's three actor types do not carry | TASK-074 (screens), AHDA (retention, PTBC-011), a later FG-06 task | Specify with the decisions they wait on |
| F-5 | **Every class is fail-closed.** Capture is in the producer's transaction for all nine classes, and a malformed event throws at capture, so its action fails (TBC-AUD-03 names no catalogue; this is its strictest reading). The full suite found one producer whose attribute names the first rule refused — FG-04's `riskRatings_count` — and the rule was widened to any identifier | AHDA Cybersecurity (TBC-AUD-03) | Confirm, or name classes that may be captured best-effort |
| F-6 | **A DRAFT or SUBMITTED project's events have no project scope** (`ProjectAudit.ScopeOf`, Project's own rule), so they are not on that project's Activity timeline: the entity Project Manager's withdrawal is audited but shows no Activity line until the project is registered | Project (TASK-041) | Decide whether pre-registration events belong on the timeline |
| F-7 | **An Activity line names the action, not the record.** Templates are fixed bilingual text per event type ("Risk closed"); the record's title is not copied (§7.4), and the line carries the subject's id for a drill-through the source authorises again. Changing a template changes no stored line until a rebuild | TASK-074, AHDA (TBC-AUD-30) | Resolve record labels at the source when the screen needs them; rebuild after a template change |
| F-8 | **Ingestion and projection are serialised platform-wide**, by the chain's advisory lock and the checkpoint row. The commit path adds two short transactions to each audited save. Unmeasured beyond the test suites' volume | TASK-092 | Measure under the performance test; batch ingestion or partition the chain if needed |
| F-9 | **A rebuild is one transaction**, tracking every line it repairs. Right for a projection of this size; a store of millions of events would want batched commits | TASK-092 | Revisit with F-8 |
| F-10 | **The client address cannot be recorded in process.** The test server has no remote address, so the masking test uses the name typed at sign-in, the other request-context attribute; the compose check (§7.3) shows the address recorded | — | None |
| F-11 | **`event-conventions.md` row 29 is malformed** (TASK-071): the row stops after its event list, without its remaining cells. Row 30 is added after it unchanged | TASK-071 owner | Complete the row |
| F-12 | **Security Lead review (CTL-43) cannot be requested**: the change touches RBAC and the audit store, and the repository has no CODEOWNERS teams (TASK-031 F-13) | Maintainer | Review before merge |

## 9. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-10-12 | Initial record. Capture through the outbox and ingestion at commit and by a worker; the attribute digest in the chain; the data-layer guard; the Activity projection with its checkpoint, check and rebuild by API and command; audit read by category, scope and sensitivity; nine permissions and their shipped grants; 13 decisions, 12 findings. | Backend (TASK-073) |

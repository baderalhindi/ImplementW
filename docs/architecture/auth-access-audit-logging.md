# Authentication and Access Audit Logging

| Field | Value |
| --- | --- |
| Task | TASK-033 — Implement Authentication & Access Audit Logging (P5 - Identity & Access Management, FG-03) |
| Depends on | TASK-030 — Server-Side RBAC & Data-Scope Authorization Engine (`authorization-engine.md`): the engine whose refusals are audited. Also builds on TASK-028/029 (`sso-directory-integration.md`, `mfa-privileged-access.md`), the sign-in paths, and TASK-031 (`identity-access-administration.md`), the privileged actions |
| Record date | 2026-09-28 |
| Status | **BUILT AND VERIFIED LOCALLY** against PostgreSQL 17 (`AHDA-postgres`), the test directory (`AHDA-ldap`) and a SIEM endpoint hosted in the test process. **Not run against AHDA's SIEM**: no environment exists and the SIEM product is not named (F-1) |
| Branch | `sec/task-033-auth-access-audit-logging` |
| Deliverables | **Audit event emitters:** `IAuditTrail` (`Application/Common/Auditing`), `AuditTrail` (`Features/AuditActivity`), emitters in `AuthenticationService`, `AuthorizationEngine`, the FG-03 administration services, `IdentityIntegrationService` and the API pipeline (`IAccessAudit`); the catalogue `IdentityAccessAuditEvents`. **Audit store:** migrations `TASK-033_CreateAuditActivityTables` and `TASK-033_AddAuditActivityCrossModuleForeignKeys` (hash chain and append-only triggers). **SIEM forwarding integration:** `SiemForwarder`, `HttpSiemClient`, `SiemForwardingWorker`. **Redaction test suite:** `RedactionTests`, with `AuditTrailTests`, `SiemForwardingTests` and `AuditActivitySchemaTests` (38 integration tests) and 22 unit tests. This record |
| Environment variables / secrets | `SIEM_ENDPOINT_URL` (configuration store, Public) and `SIEM_API_TOKEN` (secret store, Secret). Both already in the sheet. `SIEM_API_TOKEN` is on `ApplicationSecrets.OptionalKeys`: the sheet scopes it to SIT/UAT/PROD, and without it events are stored and wait to be forwarded |
| Gate decision applied | None in the workbook row. **PTBC-011** (mandatory audit classes) and **PTBC-029** (SIEM forwarding subset) are both Open with AHDA Cybersecurity. The classes are the ERD's nine; the forwarded subset is configuration (D-9) |
| Implements | CTL-25 (identity and access audit classes), CTL-26 (SIEM forwarding), and TASK-033's share of CTL-27 (no secret in a log line, the audit store or the SIEM). CTL-24 (the full Formal Audit store) remains TASK-073's |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), Implementation Plan rows TASK-033, TASK-073, TASK-074 and TASK-083, as read 2026-09-28; Environment and Secrets snapshot `environment-and-secrets.csv` rows 25–26 |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | An immutable audit event for every authentication attempt that fails or succeeds, every authorization refusal, every FG-03 privileged action and every role assignment change | This task |
| **In** | The three `audit_activity` tables the events need (ERD §5.22), the hash chain, and the database refusing any change to a stored event | This task |
| **In** | Forwarding a configured subset of classes to the SIEM, with delivery state kept apart from the event | This task |
| **In** | Keeping passwords, tokens, second-factor codes and secrets out of log lines, audit rows and SIEM events, proven by test | This task |
| **Out** | `business_activity_entry`, the Activity projection, audit read APIs, audit access by category and scope, the outbox | TASK-073 |
| **Out** | Audit screens ADM-050–053, SCR-057/058 | TASK-074 |
| **Out** | The platform-wide log redaction policy and its field registry | TASK-083 |
| **Out** | Permission-profile authoring events (who authored, published, what changed) | TASK-110, through the same `IAuditTrail` |
| **Out** | The audit events of every other module (lifecycle, approval, data change) | Each module's backend task, through the same `IAuditTrail` |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **One seam: `IAuditTrail` in `Application/Common/Auditing`.** A producer builds an `AuditEntry` (class, type, outcome, actor, subject, scope, attributes) and either **stages** it, so its own next save commits it with the change, or **records** it, committed at once in a unit of work of its own. The store, the chain and the SIEM are behind the interface in `Features/AuditActivity`. | ADR-003 E-U3: every module may emit audit events, and the module registry says E-U3 crosses "via the outbox envelope kinds in Application/Common, not via a module reference". A producer depends on Common only, so no module edge is added and the architecture tests pass unchanged. |
| D-2 | **Not through the outbox yet.** Staging adds the rows to the request's `PMPlatformDbContext`, which every FG-03 repository saves through, so the change and its event commit in one transaction or not at all. A refusal changes nothing, so it is committed on its own. | `common.outbox_message` does not exist; TASK-073 builds the outbox. Staging into the writing transaction gives the durable capture M-6 asks of the outbox. When TASK-073 moves capture to the outbox, only `AuditTrail` changes, not the producers. |
| D-3 | **The database owns the hash chain and the immutability.** A `BEFORE INSERT` trigger takes a transaction-scoped advisory lock, reads the chain head (I-55), sets `recorded_at` to a strictly increasing `clock_timestamp()`, sets `previous_event_hash` to the head's hash, and sets `event_hash = audit_event_hash(row)`: SHA-256 over the previous hash and every other column, joined with `\|`. `BEFORE UPDATE OR DELETE` and `BEFORE TRUNCATE` triggers on `audit_event` and `audit_event_attribute` raise SQLSTATE 42501 for every role. | A writer cannot choose its place in the chain or its hash, and cannot rewrite what it stored, whatever the application does. "Immutable" is then a property of the table, not of the code path. The lock serialises chain extension, so two transactions cannot both claim the same predecessor. |
| D-4 | **Where each class is emitted.** AUTHENTICATION: `AuthenticationService`, where every sign-in, second-factor, refresh and step-up attempt now ends in `SucceedAsync` or `FailAsync`, which audit before answering; plus the bearer handler for a presented access token that fails validation. AUTHORIZATION_DENIAL: `AuthorizationEngine.AuthorizeAsync` for every refusal it decides (endpoint gate and record check, 403 and 404); the permission handler when MFA refuses before the engine; the step-up middleware. PERMISSION_CHANGE: `AccessRelationshipService`, staged in `End()` and on create, so no path ends or grants an assignment unaudited. PRIVILEGED_ACTION: every FG-03 write (user, role, department, external entity) and the ADM-041 connection test. The 28 event types are rows 8–11 of `event-conventions.md` §4. | Auditing at the decision point, not at each endpoint, means a future endpoint that uses the engine or the sign-in service is audited without doing anything. |
| D-5 | **A failed attempt names the account as subject, not actor.** `actor_user_id` is null (nobody proved who they were); `subject` is the user the attempt named, when known. The event records why it failed (`failure_reason`: `CREDENTIALS_REJECTED`, `NO_PLATFORM_ACCOUNT`, `ACCOUNT_INACTIVE`, `TOKEN_INVALID`, `TOKEN_EXPIRED`, `SECOND_FACTOR_REJECTED`, `MULTI_FACTOR_MISSING`, `ENROLMENT_NOT_ALLOWED`, `PROVIDER_UNAVAILABLE`, `NOT_CONFIGURED`). A password sign-in records the name typed, truncated to 100 characters. The caller still gets one generic 401. | A SIEM detects password spraying and brute force by account and reason, which the caller must never learn (TASK-028, no user enumeration). The name typed is what such detection keys on (F-11). |
| D-6 | **An event with no known user is attributed to `svc.audit-capture`.** A third SERVICE principal (`…00fd`) is seeded and is the `created_by` of an anonymous refusal. | ERD D-2: `created_by` is never empty and names a user; the integrity validator's ORPHANED_AUDIT_ACTOR check reads it. |
| D-7 | **A refusal is audited or it fails.** `RecordAsync` takes no cancellation token and throws if the event cannot be committed, so the request ends in 500. It never ends in the normal answer without its event. | "100% of failed authentication attempts … produce an Audit event": a caller who aborts the request, or a database that refuses the write, must not produce an unaudited refusal. |
| D-8 | **Redaction happens on capture.** Attributes hold ids, codes, enum values and the name typed at sign-in; never a password, token, second-factor code or secret. A changed email address or mobile number is recorded as `[WITHHELD]` → `[WITHHELD]`: the event says which contact field changed, not its value. No new log line carries a value: the SIEM client logs a status code or an exception type, never the token or the event. | event-conventions EV-9: attributes arrive "already redacted by classification in the producer". The classification taxonomy is still outstanding (UGV-01), so the two personal contact fields are withheld outright (F-10). |
| D-9 | **SIEM forwarding is configuration over stored events.** `Audit:Siem:ForwardedClasses` (default AUTHENTICATION, AUTHORIZATION_DENIAL, PRIVILEGED_ACTION, PERMISSION_CHANGE) decides which events get a PENDING `audit_forwarding_record`, in the same unit of work as the event. Start-up fails if AUTHENTICATION is missing or a class name is unknown. `SiemForwardingWorker` sends the queue oldest first, one POST per event, and marks each FORWARDED or FAILED. A FAILED record is sent again on the next pass. Delivery is at least once, and `eventId` identifies a repeat. The endpoint must be HTTPS, and the token is read at each use. | PTBC-029: "forwarding is deployment configuration over events already captured". CTL-26 requires at least the authentication-failure class end to end, so it cannot be configured away (as CTL-07 does for R01's MFA). An event cannot be stored without being queued. The event row is never updated (ERD: forwarding state lives apart). |
| D-10 | **Expected forwarding latency.** With an empty backlog an event reaches the SIEM within `Audit:Siem:PollInterval` (default 5 s) plus one delivery. A full batch (`BatchSize`, default 100) is followed at once by the next. The workbook gives no number, so the default is provisional (F-8). | The validation check asks for "the expected forwarding latency". The value is stated so it can be tested and changed. |

## 3. How it fits together

```
HTTP request ──► CorrelationId ─► Authentication (JwtBearer) ─► Authorization ─► StepUpAuthentication ─► controller ─► service
                                   │ token refused                │ engine refuses       │ stale step-up           │
                                   ▼                              ▼                      ▼                         ▼
                              IAccessAudit ─────────────────► IAuditTrail ◄──────────────────────────── Stage (with the change)
                                                                   │ RecordAsync (own unit of work)            │ producer's SaveChanges
                                                                   ▼                                          ▼
                                             audit_event (+ attributes, + PENDING forwarding record) ◄── BEFORE INSERT: lock, chain, hash
                                                                   │
                                   SiemForwardingWorker ─► SiemForwarder ─► HttpSiemClient ─► POST SIEM_ENDPOINT_URL (Bearer SIEM_API_TOKEN)
                                                                   └─► audit_forwarding_record: FORWARDED | FAILED
```

`IAuditRequestContext` (implemented in the API) adds the request's `X-Correlation-Id`, the client address and the token's user to every event. Outside a request, as in the worker, there is no client or user.

## 4. The SIEM contract

The SIEM product is not named, so the platform posts its own contract, as the MFA adapter does (`mfa-privileged-access.md` §4). The selected product's ingestion API gets an adapter of its own. One `POST` per event, `Content-Type: application/json`, `Authorization: Bearer SIEM_API_TOKEN`. Any 2xx is acceptance; anything else, or no answer within `Audit:Siem:Timeout` (10 s), is a failed delivery.

```json
{
  "eventId": "0199912a-7c55-7b4e-9a0e-3f3c1d1e8b21",
  "eventClass": "AUTHENTICATION",
  "eventType": "IdentityAccess.SignInFailed",
  "outcome": "FAILED",
  "occurredAt": "2026-09-28T18:52:10.4132200+00:00",
  "recordedAt": "2026-09-28T18:52:10.4218960+00:00",
  "actor": { "actorType": "USER", "userId": null },
  "subject": null,
  "scopeProjectId": null,
  "scopeExternalEntityId": null,
  "correlationId": "5b2a0f6e-1f4d-4a47-9d2b-0c7e3f8a9b10",
  "eventHash": "9f2c…64 hex…",
  "previousEventHash": "41ab…64 hex…",
  "attributes": [
    { "name": "authentication_method", "oldValue": null, "newValue": "DIRECTORY" },
    { "name": "client_address", "oldValue": null, "newValue": "203.0.113.7" },
    { "name": "failure_reason", "oldValue": null, "newValue": "CREDENTIALS_REJECTED" },
    { "name": "username", "oldValue": null, "newValue": "a.person" }
  ]
}
```

## 5. Configuration

| Key | Where | Default | Meaning |
| --- | --- | --- | --- |
| `SIEM_ENDPOINT_URL` | Configuration store | — | Ingestion URL; HTTPS required. Unset: nothing is sent, events wait |
| `SIEM_API_TOKEN` | Secret store (optional key) | — | Bearer token; read at each send, so a rotation needs no restart |
| `Audit:Siem:ForwardedClasses` | `appsettings.json` | the four identity classes | ERD `event_class` values; must include `AUTHENTICATION` |
| `Audit:Siem:PollInterval` | configuration | `00:00:05` | Wait after a pass that emptied the queue |
| `Audit:Siem:BatchSize` | configuration | `100` | Events per pass |
| `Audit:Siem:Timeout` | configuration | `00:00:10` | Per delivery |
| `Audit:Siem:RequireHttps` | configuration | `true` | Cleared only by the tests' in-process SIEM |

## 6. Verification

All run on 2026-09-28 against `AHDA-postgres` (PostgreSQL 17.11) and `AHDA-ldap`, from the repository root, with `DB_CONNECTION_STRING` naming the local server.

| # | Command | Result |
| --- | --- | --- |
| 1 | `dotnet build src/backend -warnaserror` | 0 warnings, 0 errors |
| 2 | `dotnet test src/backend/PMPlatform.Tests.Unit` | 327 passed (22 new: `AuditTrailTests` 7, `AuditValueTests` 4, `SiemForwarderTests` 4, `AuthorizationAuditTests` 3, `AdministrationAuditTests` 4); 305 existing, including the architecture tests A-1 to A-6 unchanged |
| 3 | `dotnet test src/backend/PMPlatform.Tests.Integration` | 259 passed, twice in a row (38 new: `AuditTrailTests` 27, `SiemForwardingTests` 4, `RedactionTests` 1, `AuditActivitySchemaTests` 6); 221 existing, one of which (`ClosingAProjectEndsTheAccessItGave`) no longer deletes the project it created, because that project is now named by an audit event |
| 4 | `dotnet ef migrations script --idempotent` applied twice to a scratch database in `AHDA-postgres`, as `migration-dry-run.sh` does | Both runs exit 0; 11 migrations recorded; the three triggers present; no `DROP TABLE` or `DROP COLUMN`. The scratch database was dropped |
| 5 | The nine `docs/architecture/*-check.py` scripts CI runs | All OK |

### 6.1 Mutation tests: each protection fails its test when removed

Each mutation was applied alone, the named tests run, and the file restored (and touched, so the next build does not reuse the mutated binary).

| # | Mutation | Test that failed |
| --- | --- | --- |
| M-1 | Sign-in copies the password into an audit attribute (reaches the store and the SIEM) | `RedactionTests.NoSecretReachesTheLogsTheAuditStoreOrTheSiem` |
| M-2 | Sign-in writes the password to a log line | `RedactionTests.NoSecretReachesTheLogsTheAuditStoreOrTheSiem` |
| M-3 | A forged refresh token is refused without `FailAsync` | `EveryFailedAuthenticationProducesOneAuditEvent("forged refresh token")` |
| M-4 | `UserAdministrationService` builds its event but does not stage it | `PrivilegedActionsAndRoleChangesAreAuditedWithTheirChange` |
| M-5 | The SIEM client sends no `Authorization` header | `SiemForwardingTests.AFailedSignInReachesTheSiem` |
| M-6 | The append-only trigger is disabled and a stored event is changed or deleted | `AuditActivitySchemaTests.AChangedOrDeletedEventIsDetected` finds exactly the changed event, or the event after the deleted one (the test performs this tampering itself) |

### 6.2 Validation check on the local DEV stack

SIT and UAT do not exist (`environments.json`: no organization, empty `APP_BASE_URL`), so the check was run on 2026-09-28 against the only non-PROD environment there is: the Compose stack, rebuilt from this branch. `migrate` applied the TASK-033 migrations, and `seed` with its data-integrity check exited 0. AHDA-api used default forwarding settings (5 s poll, HTTPS requirement lifted for the stand-in). AHDA's SIEM and MFA provider were replaced by a stand-in running as a host process (not a container). It spoke §4 and the MFA contract, and required the bearer token on every call. The script called the API over HTTP with a known `X-Correlation-Id` per step.

| Step | Observed |
| --- | --- |
| Failed login: `local.r02`, wrong password | 401 `AUTHENTICATION_REQUIRED`. One event: `AUTHENTICATION` / `IdentityAccess.SignInFailed` / `FAILED`, no actor, `failure_reason=CREDENTIALS_REJECTED`, `username=local.r02`, `client_address` set. Forwarding record `FORWARDED` |
| Login failure in the SIEM | Received **3.29 s after the attempt** (3.26 s after the event was stored), within the 5 s poll interval plus one delivery (D-10). The hash the SIEM received equals the hash in the store |
| Role change: `local.r01` (password + second factor) assigns R02 to `local.r05` | 201. One event: `PERMISSION_CHANGE` / `IdentityAccess.RoleAssigned` / `SUCCESS`, actor `local.r01`, `role_code=R02`, `user_id` of `local.r05`. Forwarded, and received by the SIEM 4.77 s after the response |
| Restore: the assignment ended | 200. One event: `IdentityAccess.RoleAssignmentEnded`, `status=ACTIVE>ENDED`, `end_reason=MANUAL`. Forwarded |
| Retry | The first attempt reached a stand-in that could not read .NET's chunked request body. The event stayed `FAILED`, was retried on the next pass once the stand-in was fixed, and ended `FORWARDED` |
| Chain and immutability | 0 broken links over the 9 events in that database. `DELETE` of the failed-login event was refused: `audit_activity.audit_event is append-only: DELETE is not allowed` |
| Credentials in the API's 1,131 log lines | None of the directory password, the wrong password, the bind password, the SIEM token, the MFA key or the second-factor code |

This is the check's procedure on DEV. It does not replace the run in SIT against AHDA's SIEM (F-1).

## 7. Acceptance criteria, validation and gate decision

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | 100% of failed authentication attempts and privileged actions produce an immutable Audit event | **MET locally.** 13 failure paths (wrong password, unknown person, no platform account, disabled account, suspended entity, forged SSO code, forged MFA token, wrong second factor, forged refresh token, wrong step-up code, forged and expired access token, directory unreachable) each produce exactly one FAILED AUTHENTICATION event. Every FG-03 write stages its event in the same transaction, and a change refused at save leaves none. UPDATE, DELETE and TRUNCATE are refused by the database (42501) | `AuditTrailTests`; `AuditActivitySchemaTests`; M-3, M-4 |
| 2 | No password, token or secret value appears in any log line (verified by log-redaction test) | **MET locally.** Directory, bind and client passwords, the signing key, the MFA and SIEM credentials, MFA tokens, second-factor codes, access and refresh tokens: none in any log line at Trace level, in any audit row, or in any SIEM event body. The SIEM token appears only in the `Authorization` header | `RedactionTests`; `CredentialLoggingTests` (TASK-028/029, unchanged); M-1, M-2 |
| 3 | SIEM forwarding is confirmed end-to-end for at least the authentication-failure event class | **MET against the in-process SIEM; OWED against AHDA's (F-1).** A failed sign-in reaches the SIEM with the same hash the store holds, and its forwarding record is FORWARDED. A refused delivery is FAILED and succeeds on a retry. Start-up refuses a subset without AUTHENTICATION | `SiemForwardingTests`; M-5 |
| V | Validation check: trigger a failed login and a role change in a non-PROD environment; confirm both appear as Audit events and, for the login failure, in the SIEM within the expected forwarding latency | **Done on the local DEV stack, owed in SIT.** Both appear as audit events. The login failure reached the stand-in SIEM 3.29 s after the attempt, with default settings (5 s poll). The role change was forwarded as well (§6.2). The same is asserted on every build by the integration tests. SIT waits on provisioning (ADR-001 Q1–Q22) and on AHDA naming the SIEM | §6.2; `AuditTrailTests.PrivilegedActionsAndRoleChangesAreAuditedWithTheirChange`, `EveryFailedAuthenticationProducesOneAuditEvent`; `SiemForwardingTests.AFailedSignInReachesTheSiem` |
| G | Gate decision | No gate cell. PTBC-011 and PTBC-029 are Open; the classes and the subset are configuration and are listed in F-8 for AHDA Cybersecurity | F-8 |

## 8. Findings

| # | Finding | Owner | Action |
| --- | --- | --- | --- |
| F-1 | **Not run against AHDA's SIEM.** No environment exists and the SIEM product and its ingestion API are not named. The contract in §4 is the platform's own | AHDA Cybersecurity (product, endpoint, token); DevOps (first environment) | Name the product; write its adapter if it cannot take §4; run `SiemForwardingTests`' check in SIT with the real endpoint and record the measured latency |
| F-2 | **The hash chain covers the event row, not its attributes.** An attribute is inserted after its event, so the insert trigger cannot see it. Attributes are append-only like events, but a table owner who disables the triggers could change one undetected | TASK-073 | Decide whether to add an attribute digest to `audit_event` (an ERD change) or to hash attributes at commit |
| F-3 | **`audit_forwarding_record.invocation_id` has no foreign key.** Its target, `integration_monitoring.invocation`, is TASK-075's and does not exist. The column is there and is left null | TASK-075 | Add the constraint with the table, and record each SIEM delivery as an invocation |
| F-4 | **The table owner can switch the protection off.** Triggers bind every role but the owner, who can `DISABLE TRIGGER`. Locally the application connects as the owner. The chain detects such a change (M-6) but does not prevent it | DevOps (TASK-020 roles); TASK-073 (CTL-24) | Run the API as a role that owns nothing and holds only INSERT/SELECT on `audit_activity`, with migrations run as the owner |
| F-5 | **`client_address` is the TCP peer.** Behind AHDA's load balancer it is the balancer until forwarded headers are configured for the trusted proxy | TASK-078 / DevOps | Configure `ForwardedHeaders` for the load balancer's range only |
| F-6 | **No `event_id` column (event-conventions S-8).** It exists to deduplicate outbox deliveries; this path writes directly and inserts each event once | TASK-073 | Add it with the outbox |
| F-7 | **Chain extension is serialised platform-wide.** The advisory lock is held from an audited insert to its commit. Harmless at FG-03's volume; a hot path (e.g. project updates) would queue behind it | TASK-073, TASK-092 | Measure lock waits under load; partition the chain (e.g. per day) if needed |
| F-8 | **The classes, the subset and the latency are provisional.** PTBC-011 (which classes are mandatory) and PTBC-029 (which are forwarded) are Open. This task records all four identity classes and forwards all four; the poll interval is 5 s | AHDA Cybersecurity | Confirm or change `Audit:Siem:ForwardedClasses` and the latency target; no code change needed |
| F-9 | **An expired access token is an audited failure.** Every access-token expiry of an active SPA session produces one `AccessTokenRejected` (`TOKEN_EXPIRED`) before the refresh. Forwarding is by class, so it goes to the SIEM with the other authentication events | AHDA Cybersecurity | If the volume is unwanted, ask for rules finer than a class (type or reason); that is a code change |
| F-10 | **What is not audited, by decision.** A request that fails validation (400, e.g. no password field) never reaches an authentication step. An administration change refused by a rule (409/422) is not an action taken. Email address and mobile number are withheld, not masked by classification (UGV-01 outstanding) | AHDA Cybersecurity; TASK-083 | Confirm, or name the refusals that must also be audited |
| F-11 | **The name typed at sign-in is stored and forwarded.** It is what brute-force detection keys on, but someone may type their password into the name field. It is truncated to 100 characters and not otherwise altered | AHDA Cybersecurity | Confirm, or require it to be hashed (detection would then need the same hash on the SIEM side) |
| F-12 | **Retention of audit rows is unset** (OQ-003), and nothing may delete them | AHDA / PMO | Decide retention; any purge must be a governed, audited operation designed with TASK-073 |
| F-13 | **Security Lead review (CTL-43).** The change touches authentication and RBAC. The repository has no CODEOWNERS teams to request, so the review cannot be requested from the PR | Security Lead | Review before merge |

## 9. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-28 | Initial record. `IAuditTrail` in Application/Common with staged and recorded capture; the three `audit_activity` tables in two migrations, with the database setting the hash chain and refusing change; 28 IdentityAccess event types emitted from sign-in, the engine, the API pipeline and FG-03 administration; SIEM forwarding of a configured subset with delivery state apart from the event; `svc.audit-capture` seeded; 22 unit and 38 integration tests, six mutations each caught; 13 findings. | Security (TASK-033) |
| 2026-09-28 | §6.2: the validation check run on the local DEV stack. The failed login and the role change were audited; the login failure reached the stand-in SIEM in 3.29 s with default settings; a failed delivery was retried; the chain is intact and a DELETE was refused. §7 row V updated. | Security (TASK-033) |
| 2026-09-28 | `SiemForwardingWorker` now derives from `Infrastructure/Hosting/PollingWorker`, the loop it shared with TASK-035's outbox and approval workers; behaviour unchanged (a full batch runs the next at once, a failed pass is logged by exception type and retried). Approval's 11 audit event types go through `IAuditTrail` as this record's do (`approval-framework.md` D-12). | Backend (TASK-035) |

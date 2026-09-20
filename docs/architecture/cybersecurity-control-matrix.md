# Cybersecurity Control Matrix — CS-001 to CS-033 Overlay Mapping

| Field | Value |
| --- | --- |
| Task | TASK-010 — Ratify Cybersecurity Control Overlay Mapping (CS-001-033) (P1 - Architecture Decisions) |
| Depends on | TASK-005 — Hosting & Data Localisation ADR (`docs/architecture/adrs/ADR-001-hosting-and-data-localisation.md`, APPROVED — CONDITIONAL) |
| Record date | 2026-09-20 |
| Status | **NOT RATIFIED — PROVISIONAL.** Control catalogue complete (55 controls across 8 families, 50 implementing tasks). CS register: 3 of 33 rows anchored (CS-023, CS-024, CS-028); 30 rows blocked on the policy text itself — Blueprint v2.0 Section 22 is not available (§2, OQ-015) |
| Decision owner | AHDA Cybersecurity + Engagement Architect |
| Branch | `chore/task-010-task-010-cyber-control-matrix` |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), sheets Implementation Plan (112 rows), Architecture Decisions (ADR-001–013), Open Questions (OQ-001–012), Environment and Secrets, Release Checklist, as exported 2026-09-20 |
| Controlled sources and authority order | per TASK-001 |

## 1. What this record is

The TASK-010 definition asks for one thing: a matrix that maps each of AHDA's cyber policies CS-001 through CS-033 to a concrete platform control, with an implementing task ID from the workbook against each, so that every Security-category task builds against one authoritative list rather than its own reading of the requirement.

That matrix has two halves, and they are in different states:

1. **The control catalogue (§4)** — the concrete platform controls, grouped into the eight control families the TASK-010 row itself names (architecture, API security, data protection, logging/audit, monitoring, backup/DR, secure SDLC, third-party/cloud). Every row names the implementing task, the workbook cell, ADR or PTBC it is sourced from, any AHDA-gated value it depends on, and the verification the task already commits to. **This half is complete** and is what the Security-category tasks implement against from W1.
2. **The CS register (§5)** — the 33 rows, one per CS-0xx policy, each pointing at the catalogue rows and task that implement it. **This half cannot be filled from the sources available.** The policy titles and texts live in Blueprint v2.0 Section 22, which is not in the repository or the connected Google Drive (§2). Three rows are anchored because one workbook cell cites them by number; the other thirty carry an Open Question, not a guess.

The record is therefore a ratification *candidate*: the catalogue can be accepted now, and the register is filled by the procedure in §6 when Section 22 arrives. Nothing in this record allocates a CS number to a control by inference. That is the same rule TASK-001 §1 and TASK-004 §1 apply to gated business values, and a policy text is not a weaker thing than a threshold.

Two notes on the brief this task was run against:

- The TASK-010 row's *Gate Decision Applied* and *Participation Amendment* cells are **empty** in the workbook. The gate text supplied with this task (SAR-only money type, bilingual labels, five-level impact, duration weighting; financial provenance, language attribute, Declared Baseline marker) is TASK-008's, verbatim. It governs the ERD (`docs/architecture/erd.md`), not a control matrix, and is not applied here.
- The TASK-010 family list has no identity-and-access family, yet two of the ten Security-category tasks are FG-03 identity tasks (TASK-029, TASK-033). Identity and access controls are carried under the API-security family (CF-2) rather than invented as a ninth family, so the catalogue's families remain the eight the row names.

## 2. The source problem

### 2.1 What is not available

| Needed | Where it lives | Availability |
| --- | --- | --- |
| The 33 policy titles and texts, CS-001–CS-033 | Blueprint v2.0 **Section 22** (rank 3) | **Not in the repository or the connected Drive.** TASK-001 §5.1 records the gap for every controlled document; TASK-004 §9.1 records it for Appendix F.2 of the same document. Drive searched again on 2026-09-20 by title (Blueprint, Cyber) and full text (CS-001, CS-033, Section 22): no hit. |
| The subjects of PTBC-026, PTBC-028 and PTBC-030 (cybersecurity governance themes) | Blueprint v2.0 **Appendix F.2** | Not available — TASK-004 §9.1. |
| The governing regulatory instrument and AHDA's data classification | Not in the workbook | ADR-001 R-4, open with AHDA Cybersecurity. |

### 2.2 What is on record about Section 22

Every Section 22 fragment the workbook quotes or paraphrases. This is the *entire* Section 22 content available to this record.

| # | Fragment | Where quoted | Family |
| --- | --- | --- | --- |
| F-1 | "DEV/SIT/UAT/PROD separation and controlled CI/CD promotion" (Section 22.1) | TASK-016 Detailed Description | CF-1, CF-7 |
| F-2 | "controlled CI/CD promotion/change process with required quality/security gates" (Section 22.1) | TASK-018 Detailed Description | CF-7 |
| F-3 | No secret in source code, configuration files, logs, screenshots or test fixtures (Section 22.1, paraphrased) | TASK-019 Detailed Description | CF-3 |
| F-4 | "Blueprint Section 22.1's secure-SDLC requirement (**CS-023/024/028**)" — the penetration test | TASK-082 Detailed Description; Release Checklist "External Penetration Test Passed"; ADR-002 §4.2.1 | CF-7 |
| F-5 | "Monitoring: availability/errors/latency/queue/integration failures" (Section 22) | TASK-090 Detailed Description | CF-5 |
| F-6 | Section 22 named as source spec for PTBC-027 (MFA/PAM, step-up), PTBC-029 (SIEM subset, with Section 18) and PTBC-048 (RPO/RTO, retention) | `docs/governance/ptbc-themes.csv` | CF-2, CF-4, CF-6 |

F-4 is the only place a CS number appears anywhere in the workbook, the repository or the Drive. It ties three numbers to one family. The numbering scheme, the number of policies per family and the family order of the numbers are otherwise unknown. (The TASK-010 family list places secure SDLC seventh of eight, and 23, 24 and 28 sit in the upper-middle of 1–33, which is consistent with a family-ordered list — but consistency is not evidence, and this record does not allocate on it.)

### 2.3 Consequence

The mapping direction that *can* be built from the workbook is control → task. The mapping CS → control is a lookup once Section 22 is in hand (§6). Building the catalogue now, rather than waiting, is the right order: the W1 secure-engineering tasks (TASK-016–023, TASK-028–033, TASK-078–080, TASK-083 per the wave plan) start before Section 22 is likely to arrive, and they need the control statements, not the policy numbers, to build against.

## 3. Control families

The eight families are those the TASK-010 row enumerates, in its order. Each is given an ID so catalogue and register rows can cite it.

| ID | Family | Scope in this platform | Section 22 fragments on record |
| --- | --- | --- | --- |
| CF-1 | Architecture and hosting | Where the platform runs, environment topology, network boundaries, infrastructure provenance | F-1 |
| CF-2 | API security, identity and access | Authentication, MFA/step-up, authorization, administration least-privilege, endpoint hardening, contract hygiene | F-6 (PTBC-027) |
| CF-3 | Data protection | Encryption, secrets, field-level restriction, documents, minimisation, channel content, non-production data | F-3 |
| CF-4 | Logging and audit | Formal Audit store, mandatory audit classes, SIEM, redaction, log residency, traceability | F-6 (PTBC-029) |
| CF-5 | Monitoring | APM, health, operational alerting, integration health, incident handling | F-5 |
| CF-6 | Backup and disaster recovery | Backups, PITR, restore drills, RTO/RPO, release rollback | F-6 (PTBC-048) |
| CF-7 | Secure SDLC | Source-control governance, CI gates, promotion, artifact integrity, scanning, threat modelling, penetration testing, supported runtimes | F-1, F-2, F-4 |
| CF-8 | Third-party and cloud | Tenancy, CI execution location, external processors, remote access, integration registry, handover | — (ADR-001 §7) |

## 4. Control catalogue

Each row is one platform control. **Implements** names the primary task (bold) and the tasks that carry part of it. **Source** names the workbook cell, ADR, PTBC or repository record the control statement is taken from; nothing is sourced from Section 22 beyond the fragments in §2.2. **Gated value** names any AHDA decision the control cannot be finished without. **Verification** is the task's own acceptance criterion or validation check, condensed.

Task wave assignments are per TASK-003 (`docs/planning/wave-to-phase-crossreference.md`).

### CF-1 Architecture and hosting

| ID | Control | Implements | Source | Gated value | Verification |
| --- | --- | --- | --- | --- | --- |
| CTL-01 | **In-Kingdom hosting.** Every resource in every environment is created in the named in-Kingdom GCP region; all data, backups and replicas remain inside Saudi Arabia. Enforced by organisation policy (`constraints/gcp.resourceLocations`) so a non-compliant apply fails rather than relying on review. | **TASK-016**, TASK-017 | ADR-001 §5, §7 C-1; TASK-016 Gate cell: "No resource may be created in any region outside Saudi Arabia" | UGV-07 — region name and tenancy ownership (ADR-001 R-2, R-3) | An apply naming any other region is rejected by policy |
| CTL-02 | **Environment separation.** Four isolated environments (DEV, SIT, UAT, PROD), each with its own database instance, its own secret-store namespace, its own network boundary and data; no credential shared between PROD and any non-PROD environment; documented promotion path with named approval gates. | **TASK-016** | Section 22.1, F-1 (TASK-016) | — | DEV credential rejected against SIT database; PROD secrets unreadable from non-PROD secret-store scopes |
| CTL-03 | **Network segmentation and least privilege between tiers.** The database tier has no public endpoint; only the API tier can reach the database port; security-group rules between tiers follow least privilege. | **TASK-021** | TASK-021 Detailed Description and Acceptance Criteria | — | External port scan shows only 443 reachable; direct database connection from outside the API tier's security group is blocked |
| CTL-04 | **HTTPS-only, in-region ingress.** All inbound traffic terminates TLS 1.2+ at a *regional* load balancer with WAF in the named region, so TLS never terminates at an edge outside the Kingdom. | **TASK-021** | TASK-021; ADR-001 §7 C-2; TASK-021 Gate cell: "In-Kingdom edge and load balancing" | UGV-07 (WAF product availability in the named region — ADR-001 C-2) | Load balancer and security policy are regional resources in the named region |
| CTL-05 | **Infrastructure as code.** 100% of environment infrastructure is created from version-controlled IaC with zero manual console changes, peer-reviewed like application code, idempotent, and statically scanned with zero HIGH/CRITICAL findings. | **TASK-017** | TASK-017 Acceptance Criteria and Validation Checks | UGV-07 (region before first apply) | `terraform plan` twice with zero diff; `terraform validate` and tfsec (or equivalent) with zero HIGH/CRITICAL |

### CF-2 API security, identity and access

| ID | Control | Implements | Source | Gated value | Verification |
| --- | --- | --- | --- | --- | --- |
| CTL-06 | **Enterprise SSO.** Authentication against AHDA's directory with single sign-on; the directory is authoritative for department, manager and job title; platform roles are assigned in the platform, never inherited from directory groups; authentication failure returns a generic error (no user enumeration); session/token expiry and refresh implemented; no credential in application logs. | **TASK-028** | ADR-007; TASK-028 Acceptance Criteria; `api-conventions.md` 401 `AUTHENTICATION_REQUIRED` "Generic — no user enumeration" | PTBC-036 (resolved); session-invalidation behaviour "confirmed with AHDA IT at environment setup" (ADR-007 Impact) | Integration tests: successful login, invalid credentials, directory-unavailable; log check for credentials |
| CTL-07 | **MFA and step-up authentication.** MFA enforced before a session is granted for at least R01; a privileged or sensitive action requires a fresh authentication context no older than a configurable threshold (403 `STEP_UP_REQUIRED`); MFA cannot be bypassed via any API path. Trigger list is configuration, not code. | **TASK-029** | PTBC-027 (source spec: FG-03; Section 22); ADR-010 "Step-up authentication applies to privileged and sensitive actions"; TASK-029 Gate cell | PTBC-027 — exact MFA/PAM policy; UGV-01 — step-up trigger list follows the classification taxonomy | Privileged action with a stale session requires re-authentication; direct privileged API call without MFA is rejected |
| CTL-08 | **Server-side authorization on every protected endpoint.** Effective authorization = permission + data scope + project/business relationship + assignment/ownership + record/lifecycle state + workflow authority + sensitivity restriction (Blueprint Section 10.1); scopes ALL/DEPT/OWN/ASSIGNED/ENTITY/READ-ONLY for R01–R08; hiding a UI control is never the only protection; cross-entity isolation is absolute; effective permission resolves against the assigned profile version. | **TASK-030** | TASK-030 Detailed Description and Acceptance Criteria; ADR-013; ADR-018 | — | Direct API call with a role/scope marked "—" in Appendix A returns 403; one allow and one deny test per role/scope combination |
| CTL-09 | **Least-privilege administration.** Permission profiles composed only from the protected FG-03 catalogue, versioned, published and activated with author/reviewer/publisher separation; a published version cannot be edited in place; R01 cannot self-approve a profile granting business-data access; R01–R08 undeletable; no per-user permanent grants (BR-IAM-023); a profile cannot grant an unauthorised classification or bypass field-level masking; deactivation never rewrites historical attribution. | **TASK-110**, TASK-031 | ADR-018; TASK-110 Acceptance Criteria; TASK-031 (Appendix A.1) | — | Profile change auditable to a person, a time and a diff; R01 self-approval attempt rejected; deactivated user still shown as historical owner |
| CTL-10 | **External identity.** Nafath verifies external-entity identity at onboarding only, never internal sign-in; a persistent authenticated session follows; the Nafath-unavailable path is an explicit retry/error state that neither silently grants nor silently denies; an entity Project Manager holds R04 scoped to their own project only, with a named AHDA sponsor and access ending on closure or role change. | **TASK-068**, TASK-066, TASK-031 | ADR-007; ADR-013; TASK-068 Acceptance Criteria; TASK-031 Participation Amendment | PTBC-031 / OQ-007 — data-minimisation boundary (CTL-21) | Simulated Nafath outage surfaces an explicit state; cross-entity access attempt denied |
| CTL-11 | **Input validation and output encoding.** Server-side schema/DTO validation on every API endpoint, independent of client-side validation; context-aware output encoding on every frontend render path; shape validated in the API tier (400), meaning in the module (422). | **TASK-077** | TASK-077; `api-conventions.md` R-24 | — | OWASP ZAP baseline against SIT: zero High/Critical injection findings; stored-XSS payload in a comment renders inert |
| CTL-12 | **Endpoint hardening.** Per-client rate limiting on authentication and sensitive endpoints (429 `RATE_LIMITED` with `Retry-After`); CORS restricted to an allowlist of AHDA origins; Content-Security-Policy, X-Content-Type-Options, X-Frame-Options, Strict-Transport-Security and Referrer-Policy on every response. | **TASK-078** | TASK-078; `api-conventions.md` §4.8 429 envelope; Environment and Secrets `CORS_ALLOWED_ORIGINS`, `RATE_LIMIT_STORE_CONNECTION_STRING` | — (thresholds are delivery-team engineering values, TASK-004 §7) | Scripted login burst throttled at the configured threshold; non-allowlisted Origin rejected; header scan reports no missing mandatory header |
| CTL-13 | **CSRF protection** on every state-changing endpoint reachable from a browser session, using a strategy consistent with the session/token model from CTL-06 and documented so new endpoints inherit it by default. | **TASK-079** | TASK-079 | — | Replayed cross-origin POST without the token is rejected; same-origin request with the token succeeds |
| CTL-14 | **Contract hygiene as a security control.** Every sensitive write carries an `Idempotency-Key` scoped to (principal, path) and stored transactionally (R-34–R-39); every response echoes `X-Correlation-Id` (R-41); a 500 carries `code`, `correlationId`, `timestamp` and nothing else — no exception type, message or stack trace leaves the server in any environment (R-26); 401 is generic. | **TASK-009** (Completed), enforced on every Backend task by `contract-check.py` in the CI gate (TASK-015, TASK-018) | `docs/architecture/api-conventions.md` §4.6, §4.7, R-26 | — | `contract-check.py` C-6 fails the build if `correlationId` or `idempotencyKey` is dropped from the envelope |
| CTL-15 | **Controlled query surface.** SCR-138 report explorer is an explicit allowlist of fields, joins and filters — never arbitrary SQL, joins or formulas; a generated report output is authorised at download time under the same rules as the underlying data; a shared saved view carries configuration only and every viewer is authorised independently at execution. | **TASK-071**, TASK-112 | TASK-071 Acceptance Criteria; ADR-019; TASK-112 | — | Out-of-allowlist field via direct API is rejected; download of another user's output with an unauthorised session is rejected |
| CTL-16 | **Inbound callback endpoints are threat-modelled and authenticated.** The SMS delivery-receipt callback (`SMS_DLR_CALLBACK_URL`) is a new inbound surface and is in the threat model scope. | **TASK-103**, TASK-081 | ADR-004; TASK-081 Participation Amendment: "the SMS delivery-receipt callback is a new inbound endpoint and belongs in the threat model" | OQ-012 / UGV-10 — provider selection | Threat model document covers the callback; receipt recorded only for a message the platform sent |

### CF-3 Data protection

| ID | Control | Implements | Source | Gated value | Verification |
| --- | --- | --- | --- | --- | --- |
| CTL-17 | **Encryption at rest and in transit for the database.** Encryption at rest enabled and verifiable via the provider console/CLI; database instances reject non-TLS connections. | **TASK-020** | TASK-020 Acceptance Criteria | — | Non-TLS connection attempt rejected; encryption-at-rest state read from the provider |
| CTL-18 | **Secrets management.** Every secret in the Environment and Secrets sheet is read at runtime from the approved secret store, never from a checked-in file; no secret in source code, configuration files, logs, screenshots or test fixtures; per-environment namespaces (CTL-02); rotation procedure documented and rehearsed; Secret Manager replication pinned to the in-Kingdom region; environment templates list names only; the bootstrap credential itself is held outside the repository. | **TASK-019**, TASK-013 | Section 22.1, F-3 (TASK-019); ADR-001 §7 C-5; TASK-013 Acceptance Criteria; TASK-019 Gate cell: "Use the approved GCP secret-management service unless AHDA IT nominates an alternative" | — | Full-git-history secret scan finds zero secrets (CTL-44); a rotated test secret is picked up by the running application without a code change; each secret's replication policy names only the in-Kingdom region |
| CTL-19 | **Field-level restriction by audience.** Financial values and every field AHDA classifies as sensitive are masked or withheld by audience consistently across screens, dashboards, reports, exports and API projections; a masked value is represented distinctly from absent and projected values (R-20); sensitive financial fields masked in exports and for external entities. | **TASK-030** (engine); TASK-052, TASK-053, TASK-069–072, TASK-067 (projections) | ADR-010; TASK-030 Gate cell; TASK-071 Gate cell: "Sensitive financial fields masked in exports"; `api-conventions.md` R-20 | **UGV-01** — classification taxonomy and field list (ADR-010 "taxonomy outstanding"); gate Before Build/Integration Build | Same field masked identically across all five projection types for the same audience |
| CTL-20 | **Document and evidence protection.** Private object storage in the in-Kingdom region; secure upload with malware scanning and SCAN_PENDING / CLEAN / QUARANTINED / SCAN_FAILED states; a document not CLEAN cannot satisfy an evidence requirement or be downloaded through the evidence path; access to a document requires an explicit authorization check, not merely project membership; DocumentVersions are immutable; unlink never deletes. | **TASK-037** | Blueprint Section 13 (as cited by TASK-037); TASK-037 Acceptance Criteria; Environment and Secrets `DOCUMENT_STORAGE_CONNECTION_STRING` note "must be in an in-Kingdom region (ADR-001)"; ADR-001 C-4 | — | EICAR upload lands in QUARANTINED and is not retrievable via the evidence endpoint; unlinked clean document remains retrievable by an authorised audit query |
| CTL-21 | **Data minimisation for external identity.** Only the minimum identity attributes required by the confirmed Nafath use case are stored, and the minimisation decision is a written record. | **TASK-068** | TASK-068 Acceptance Criteria; ADR-007 | **OQ-007 / PTBC-031** — the minimisation boundary, open with AHDA Cybersecurity | Stored fields reviewed against the documented minimisation decision |
| CTL-22 | **Out-of-band channel content restriction.** SMS carries the event and a deep link only, never sensitive content; a template containing a restricted field is blocked; redaction policy prevents any sensitive value reaching an SMS template; mobile numbers are E.164-validated and verified before use; security and critical-escalation families are mandatory and cannot be opted out of. | **TASK-103**, TASK-039, TASK-083, TASK-031 | ADR-004 (TASK-039, TASK-083, TASK-031 Participation Amendments); TASK-103 Acceptance Criteria | UGV-03 — event-family channel matrices (FG-04 configuration) | Template with a restricted field is blocked; unverified number receives nothing |
| CTL-23 | **Non-production data is governed identically.** DEV, SIT and UAT are provisioned in-Kingdom under CTL-01; they hold seeded and migration-rehearsal data; PROD secrets are unreadable from non-PROD scopes (CTL-02); all four environments are treated as in scope of localisation until AHDA Cybersecurity states otherwise. | **TASK-016**, TASK-027, TASK-089 | ADR-001 §7 C-7 | ADR-001 R-4 — data classification (sets whether the environment label or the data class carries the obligation) | All four environments provision into the named region |

### CF-4 Logging and audit

| ID | Control | Implements | Source | Gated value | Verification |
| --- | --- | --- | --- | --- | --- |
| CTL-24 | **Append-only, tamper-evident Formal Audit store.** Mandatory audit classes captured through a durable outbox inside the writing transaction; committed Audit events cannot be edited or deleted by an ordinary administrator, enforced at the data-access layer; audit access is separately authorised by category, scope and sensitivity, distinct from general R01 access; the Business Activity projection is derived, rebuildable and kept separate from both Audit and operational logs. | **TASK-073** | Blueprint Section 18 (as cited by TASK-073); `solution-architecture.md` M-6, E-U3; TASK-073 Acceptance Criteria | — | R01 delete/edit of a committed Audit event via direct data layer or API is rejected; rebuild-and-diff of the Activity projection shows zero unexplained differences |
| CTL-25 | **Mandatory identity and access audit classes.** Every authentication attempt, authorization denial, privileged action, role/permission change and permission-profile change (who authored, who published, what changed, which assignments were migrated) produces an immutable Audit event; entity Project Manager actions are audited identically to internal users. | **TASK-033**, TASK-110, TASK-073 | PTBC-011; ADR-018 (TASK-083 Participation Amendment); ADR-013 (TASK-073 Participation Amendment); TASK-033 Acceptance Criteria | — | 100% of failed authentications and privileged actions produce an Audit event (failed login and role change traced in non-PROD) |
| CTL-26 | **SIEM forwarding.** A defined subset of audit and security event classes is forwarded to AHDA's SIEM, confirmed end-to-end for at least the authentication-failure class; raw secrets are never SIEM-worthy content. | **TASK-033** | PTBC-029 (source spec: FG-06; Sections 18, 22); Environment and Secrets `SIEM_ENDPOINT_URL`, `SIEM_API_TOKEN` (owner AHDA Cybersecurity): "Forwarding scope depends on the audit catalogue, still open" | **PTBC-029** — the forwarded subset; gate Before Production | Login failure appears in the SIEM within the expected forwarding latency |
| CTL-27 | **Log redaction and sensitive-data handling.** Passwords, tokens, national ID numbers and every field the classification taxonomy marks sensitive are stripped or masked from application logs, error reports and APM payloads; the redacted-field registry is explicit and reviewed against the Section 18 Audit/Log/SIEM boundary; redaction is distinct from, and never applied to, the Formal Audit store's governed content. | **TASK-083**, TASK-090, TASK-033 | TASK-083 Acceptance Criteria and Gate cell: "Redaction policy must align with the classification taxonomy under ADR-010"; TASK-090 Acceptance Criteria | **UGV-01** — classification taxonomy | Marker value submitted through a form never appears in plaintext in log output or in a captured APM payload |
| CTL-28 | **Log and telemetry residency.** Log buckets, monitoring and APM data stay in the named region; the log-bucket location is fixed at project creation; any external telemetry destination is named and accepted in writing by AHDA Cybersecurity, because redaction (CTL-27) is a reduction, not a guarantee. | **TASK-016** (project creation), TASK-033, TASK-083, TASK-090, TASK-092 | ADR-001 §7 C-6 | ADR-001 confirmation request Q9 — acceptance of any external telemetry destination | Log bucket locations equal the named region; external destinations listed and accepted |
| CTL-29 | **End-to-end traceability.** A correlation id accompanies every request and response (R-41), every integration invocation and retry (TASK-075), every APM error (TASK-090) and every deployment (commit SHA + Task ID, TASK-018). | **TASK-009** (Completed), TASK-075, TASK-090, TASK-018 | `api-conventions.md` R-41; TASK-075, TASK-090, TASK-018 Acceptance Criteria | — | Thrown exception attributed to its correlation id in the APM dashboard within 5 minutes |

### CF-5 Monitoring

| ID | Control | Implements | Source | Gated value | Verification |
| --- | --- | --- | --- | --- | --- |
| CTL-30 | **Application performance and error monitoring** across backend and frontend: latency, throughput, error rates, queue and integration failures, unhandled exceptions — with PII redaction per CTL-27. | **TASK-090** | Section 22, F-5 (TASK-090) | ADR-001 C-6 / confirmation request Q9 if the APM is a SaaS outside the Kingdom | Deliberate unhandled exception appears in the APM dashboard within 5 minutes, correctly attributed |
| CTL-31 | **Health checks and uptime monitoring.** Liveness/readiness endpoints for API and background workers; readiness reports unhealthy when the database is unreachable; external uptime monitoring alerts an on-call channel per environment. | **TASK-091** | TASK-091 Acceptance Criteria | — | Simulated database outage: readiness reports unhealthy and the alert reaches the on-call channel |
| CTL-32 | **Operational dashboards and alert thresholds** over integration health, queue depth, retry/dead-letter counts, API latency and error rate, with alerts for availability, freshness, queue backlog and reconciliation mismatch. | **TASK-092** | TASK-092; Blueprint Section 17.1 (via TASK-075) | PTBC-037 / PTBC-046 — threshold values; gate Before Production | Forced dead-letter/backlog condition reflected on the dashboard and alerted within the configured window |
| CTL-33 | **Integration health isolated from business state.** A failed integration invocation never silently changes any business object's lifecycle state; reconciliation mismatches raise an Operational Alert and never auto-repair authoritative data; retries are idempotent and correlation-traceable; every external integration (directory/SSO, Exchange, Nafath, SMS) is monitored for delivery, failure and latency. | **TASK-075** | Blueprint Section 17.1 (as cited by TASK-075); ADR-004 (TASK-075 Participation Amendment) | — | Simulated downstream outage leaves business-lifecycle fields unchanged; forced mismatch raises an alert, not a data change |
| CTL-34 | **Incident management.** On-call rotation by role, incident-severity classification that is unambiguous and testable, response-time targets, escalation path; covers SMS-provider failure and its fallback to email and in-app; RPO/RTO fixed inputs. | **TASK-093** | TASK-093; TASK-093 Gate cell and Participation Amendment; Release Checklist "Operational Runbooks & On-Call Ready" | OQ-009 / UGV-06 — contractual SLA table; gate Before Production | Sample P1 incident walked through the runbook with every step actionable |

### CF-6 Backup and disaster recovery

| ID | Control | Implements | Source | Gated value | Verification |
| --- | --- | --- | --- | --- | --- |
| CTL-35 | **Automated backups and point-in-time recovery** for every environment's database, sized to **RPO 1 hour**; backup storage single-region in the named in-Kingdom region; retention period configurable with no default committed. | **TASK-020** | PTBC-048 (approved RPO/RTO, AHDA gate 19 Sep 2026); ADR-001 §5, §7 C-3, C-4; TASK-020 Gate cell; Environment and Secrets `DB_BACKUP_STORAGE_CONNECTION_STRING` note | **OQ-003 / PTBC-048** — backup retention period, owner AHDA Cybersecurity + Records; gate Before Production | Backup job history shows successful recent runs; backup location equals the named region |
| CTL-36 | **Restore and disaster-recovery runbook** covering database, document store and audit store; restore executed against a non-PROD environment from an automated backup and validated by row-count/checksum comparison; measured against **RTO 4–6 hours**; DR is in-Kingdom only with no out-of-Kingdom dependency; a hot standby is not assumed by the RTO; on-call owner named. | **TASK-023** | PTBC-048; ADR-001 §7 C-11; TASK-023 Gate cell; Release Checklist "Backup & DR Restore Drill Passed" — "THRESHOLDS SET" | OQ-003 retention period (fields marked TBC, not invented) | Full restore drill into an isolated environment with row counts and checksums matched and time-to-restore recorded against RTO |
| CTL-37 | **Release and rollback safety.** Documented rollback triggers (error-rate threshold, failed health check) and exact procedure; schema migrations backward-compatible with the running release (expand-then-contract); migration up-then-down-then-up reproduces an identical schema checksum; rollback rehearsed within 30 days of go-live; PROD deployment approval recorded (CTL-42). | **TASK-098**, TASK-089 | TASK-098 Acceptance Criteria; TASK-089 Acceptance Criteria; Release Checklist "Rollback Procedure Rehearsed Within 30 Days" | — | Table-top rollback of the latest release candidate restores the prior good state within target |

### CF-7 Secure SDLC

| ID | Control | Implements | Source | Gated value | Verification |
| --- | --- | --- | --- | --- | --- |
| CTL-38 | **Source-control governance.** Trunk-based development on short-lived branches; branch protection on `main` requiring at least one approving review and passing CI; CODEOWNERS covering every top-level module directory; PR template enforcing linked Task ID, test evidence and a security checklist; direct push to `main` rejected. | **TASK-012** | TASK-012 Acceptance Criteria | — | Direct push to `main` from a non-admin account rejected |
| CTL-39 | **CI quality gates as required checks.** Backend build with analyzers (zero warnings), frontend lint and type-check, unit tests for both tiers, contract check (CTL-14); a violation fails the PR and passing is a required branch-protection check. | **TASK-015**, TASK-011 | TASK-015 Acceptance Criteria; TASK-011 Acceptance Criteria | — | Deliberately broken PR fails red; fixed PR goes green |
| CTL-40 | **Controlled promotion with security gates.** Build–test–package–deploy pipeline promoting DEV → SIT → UAT → PROD; PROD requires an explicit human approval recorded with approver identity and timestamp; a failed quality gate, security scan or migration dry-run blocks promotion automatically; every deployment traceable to a commit SHA and Task ID. | **TASK-018** | Section 22.1, F-2 (TASK-018); Release Checklist "Production Deployment Approval Recorded" — "cannot be bypassed" | ADR-001 C-8 — runner location (CTL-49) | PROD deploy attempt halts and waits for approval; failed scan blocks promotion |
| CTL-41 | **Artifact integrity and dependency scanning.** Signed/tagged deployable artifact per commit on `main`; software bill of materials; base-image vulnerability scanning; SCA scanning of backend and frontend manifests; a build containing a known CRITICAL CVE with an available fix is blocked from promotion past DEV. | **TASK-022** | TASK-022 Acceptance Criteria | — | Test branch with a known CRITICAL CVE blocked; removal unblocks |
| CTL-42 | **Secret scanning and dependency vulnerability gate.** Secret scanning of the full history and incrementally per PR; a credential-looking string blocks merge; scheduled full-history scan at least weekly reporting to a named owner; new CRITICAL vulnerability with an available fix blocks merge; both are required branch-protection checks. | **TASK-080** | TASK-080 Acceptance Criteria; TASK-019 Acceptance Criteria (relies on this scan); Release Checklist "Dependency & Secret Scanning Clean" | — | Fake credential in a test PR blocked by the scanner; removal unblocks |
| CTL-43 | **Threat modelling and security review** (STRIDE or equivalent) for authentication/SSO, RBAC and data-scope engine, document upload/evidence, WF-13 external participation, Nafath, and the SMS receipt callback; every High-rated threat has a merged mitigation or a signed AHDA risk-acceptance record before UAT sign-off; security review verifies REV-IAM-002 remains closed. | **TASK-081**, TASK-110 | TASK-081 Acceptance Criteria; Release Checklist "Security Review & Threat Modeling Signed Off"; TASK-110 Validation Checks | — | Findings log: every High threat linked to a merged PR or a signed risk acceptance before the UAT gate is green |
| CTL-44 | **Independent penetration test** against UAT/pre-production covering authentication, authorization boundaries, injection and the external-entity attack surface; zero unresolved Critical findings; every High finding remediated or formally risk-accepted by AHDA before go-live; retest evidence closes remediated findings. | **TASK-082** | **CS-023 / CS-024 / CS-028** via Section 22.1's secure-SDLC requirement, F-4 (TASK-082); Release Checklist "External Penetration Test Passed"; TASK-100 depends on TASK-082 | — | Report finding list reconciled against the remediation tracker; each Critical/High has a closed retest or a signed AHDA risk acceptance |
| CTL-45 | **Supported-runtime policy.** Only LTS runtime lines are built on; the runtime remains in vendor support through the build and into the operations period; one in-support LTS upgrade is budgeted inside the ~22-month operations period rather than treated as a defect. | **TASK-011**, TASK-097 | ADR-002 §4.2.1 (.NET 10 LTS, S-2), which cites the Section 22.1 secure-SDLC requirement | — | Handover package (TASK-097) carries the S-2 upgrade commitment |
| CTL-46 | **Security testing is a named layer of the test strategy**, with a coverage threshold enforced as a build-breaking gate, alongside the ZAP baseline (CTL-11), the redaction suites (CTL-27) and the authorization allow/deny suite (CTL-08). | **TASK-084** | TASK-084 Detailed Description ("non-functional (performance, accessibility, security) testing layers") | — | Test strategy document approved by the PMO engagement lead; coverage gate enforced in CI |

### CF-8 Third-party and cloud

| ID | Control | Implements | Source | Gated value | Verification |
| --- | --- | --- | --- | --- | --- |
| CTL-47 | **Tenancy ownership.** The platform sits in AHDA's own GCP organisation, so AHDA's IAM, billing and organisation policies bind it and the delivery vendor operates inside a boundary AHDA owns. This is the condition the "internal hosting" reading in ADR-001 §6 rests on, and the decision is re-taken if the answer is the vendor's organisation. | **TASK-016**, TASK-017 | ADR-001 §6, R-3; UGV-07 | **UGV-07** — tenancy ownership; owed before the first `terraform apply` | Written answer from AHDA IT (ADR-001 confirmation request Q1) |
| CTL-48 | **Cloud credentials least-privilege.** Deploy credentials (`DEPLOY_SERVICE_ACCOUNT_KEY`, per environment) live in the CI/CD platform's native secret store, never committed, scoped to least privilege; the container registry token likewise. | **TASK-017**, TASK-018 | Environment and Secrets rows `DEPLOY_SERVICE_ACCOUNT_KEY` ("scoped, least-privilege permissions"), `CONTAINER_REGISTRY_TOKEN`; TASK-017 Environment Variables cell | — | Pipeline deploy stage succeeds with scoped permissions; secret scan (CTL-42) finds no committed credential |
| CTL-49 | **CI/CD execution location is a decision.** GitHub-hosted runners execute outside the Kingdom; either source, build artefacts and test data are confirmed out of the localisation scope, or any job that touches data or PROD credentials runs on self-hosted runners in the named region. | **TASK-018**, TASK-022 | ADR-001 §7 C-8 | ADR-001 R-4 (scope) and the runner decision itself (confirmation request) | A named decision on runner location, with data-touching jobs pinned accordingly |
| CTL-50 | **External processors are named and process in-Kingdom.** The SMS provider is CST-licensed with in-Kingdom processing and a registered sender ID; the email path, Nafath, APM and uptime services are each named with their processing location; any out-of-Kingdom destination is accepted in writing by AHDA Cybersecurity. AHDA holds the SMS provider contract so the sender ID, personal data and billing survive handover. | **TASK-103**, TASK-039, TASK-068, TASK-090, TASK-091 | ADR-004; ADR-001 §7 C-6, C-10; OQ-012 Resolution ("Recommended: AHDA holds the provider contract") | OQ-012 / UGV-10 — provider selection and sender-ID registration | Each external processor listed with its processing location and acceptance |
| CTL-51 | **Remote and vendor administrative access.** The access model for delivery-vendor operations staff — including access from outside the Kingdom over the ~22-month operations period — is recorded and accepted by AHDA Cybersecurity; secret-store access is per-environment and bootstrap credentials are held outside the repository. | **TASK-019**, TASK-093, TASK-097 | ADR-001 §7 C-9 | ADR-001 R-4 (regime governing remote access) | Access model recorded and accepted in writing |
| CTL-52 | **Integration registry.** Every external integration is an Integration Definition → Instance → Invocation record with credentials sourced from the secret store, retry/dead-letter/reconciliation and Operational Alerts (CTL-33); no integration is wired ad hoc. | **TASK-075**, TASK-019 | Blueprint Section 17.1 (as cited by TASK-075) | — | Every registered integration visible with live health on the operational dashboard (CTL-32) |
| CTL-53 | **Operational handover.** Runbooks (backup/DR, on-call, secret rotation), security procedures, architecture guide and API docs are assembled into a package formally accepted by AHDA's receiving operations team with a signed knowledge-transfer acknowledgment. | **TASK-097** | TASK-097 Acceptance Criteria; Release Checklist "Operational Handover Package Accepted" | — | Signed acknowledgment; every linked document resolves |
| CTL-54 | **Compute and data-platform products confirmed available in the named region before the region is fixed** — the regional load balancer/WAF pairing, the managed PostgreSQL product (ADR-002 R-7) and the compute runtime (ADR-001 R-6). | **TASK-017**, TASK-020, TASK-021 | ADR-001 §7 closing note; R-6, R-7 | UGV-07; ADR-001 R-6, R-7 | Product availability recorded against the named region in the confirmation response |
| CTL-55 | **Third-party dependency currency during operations.** Dependency and base-image scanning (CTL-41, CTL-42) continue on the weekly schedule through the operations period, with findings routed to the named owner; the supported-runtime commitment (CTL-45) is carried in the handover package. | **TASK-080**, TASK-097 | TASK-080 Acceptance Criteria ("scheduled full-history scan runs at least weekly and reports to a named owner"); ADR-002 S-2 | — | Weekly scan evidence and owner named in the handover package |

### 4.1 Catalogue count

55 controls. 50 distinct implementing tasks: TASK-009, 011, 012, 013, 015, 016, 017, 018, 019, 020, 021, 022, 023, 027, 028, 029, 030, 031, 033, 037, 039, 052, 053, 066, 067, 068, 069, 070, 071, 072, 073, 075, 077, 078, 079, 080, 081, 082, 083, 084, 089, 090, 091, 092, 093, 097, 098, 103, 110, 112 — of which all ten Security-category tasks appear as the primary implementer of at least one control (§7).

## 5. The CS-001 to CS-033 register

One row per policy. **Policy subject** is left PENDING wherever Section 22 is the only source for it; it is never paraphrased from a control. **Implements** is filled only where a workbook cell ties the number to a control. Every unfilled row carries the same Open Question, OQ-015, because the blocker is the same document.

| CS | Policy subject (Blueprint v2.0 Section 22) | Family | Catalogue rows | Implementing task | Gated value / Open Question | Row status |
| --- | --- | --- | --- | --- | --- | --- |
| CS-001 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-002 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-003 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-004 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-005 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-006 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-007 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-008 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-009 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-010 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-011 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-012 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-013 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-014 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-015 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-016 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-017 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-018 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-019 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-020 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-021 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-022 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-023 | PENDING text — cited by TASK-082 as part of "Blueprint Section 22.1's secure-SDLC requirement (CS-023/024/028)" | CF-7 | CTL-44 (primary); CTL-43, CTL-41, CTL-42, CTL-40 as the secure-SDLC chain the test verifies | **TASK-082**; TASK-081, TASK-080, TASK-022, TASK-018 | OQ-015 (subject text) | **Anchored** — number-to-family tie on record; subject text pending |
| CS-024 | PENDING text — cited by TASK-082 as above | CF-7 | CTL-44 (primary); CTL-43, CTL-41, CTL-42, CTL-40 | **TASK-082**; TASK-081, TASK-080, TASK-022, TASK-018 | OQ-015 (subject text) | **Anchored** |
| CS-025 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-026 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-027 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-028 | PENDING text — cited by TASK-082 as above | CF-7 | CTL-44 (primary); CTL-43, CTL-41, CTL-42, CTL-40 | **TASK-082**; TASK-081, TASK-080, TASK-022, TASK-018 | OQ-015 (subject text) | **Anchored** |
| CS-029 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-030 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-031 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-032 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |
| CS-033 | PENDING — Section 22 not available | PENDING | — | — | OQ-015 | Unallocated |

TASK-082 cites all three numbers for one task, so it is not known whether CS-023, CS-024 and CS-028 are three policies that the penetration test jointly satisfies (for example: vulnerability assessment, penetration testing, remediation tracking) or whether the test is one of several controls under each. Both readings keep TASK-082 as the primary implementer; the supporting chain is listed so that whichever reading Section 22 confirms, the row already names the task that carries it.

**OQ-015 (proposed, §10.1):** *Blueprint v2.0 Section 22 — supply the CS-001 to CS-033 policy register (title and text per policy), together with Appendix F.2 for PTBC-026, PTBC-028 and PTBC-030.* Owner: PMO Engagement Lead (document custody) / AHDA Cybersecurity (policy owner). Proposed gate: **Before Build/Integration Build** — W1 starts the secure-engineering tasks, and a policy that requires something the catalogue does not carry (§9) must be found before those tasks are built, not after.

## 6. Allocation procedure on receipt of Section 22

Filling §5 is a lookup against §4, performed once, by the Engagement Architect with AHDA Cybersecurity, and re-issued as a new revision of this record (TASK-001 §4: revisions are re-issued, never overwritten).

For each CS-0xx row:

1. **Record the subject verbatim** from Section 22 in the Policy subject column, with the Section 22 sub-clause reference.
2. **Assign the family** (CF-1 to CF-8) from the subject. A policy that spans families is recorded under its primary family with the secondary in parentheses.
3. **Select the catalogue rows** that satisfy the policy. Cite CTL IDs; do not restate the control.
4. **Name the implementing task** — the primary task of the first-cited CTL row, plus supporting tasks where the policy needs more than one.
5. **Check for a gated value.** If the policy states a value (a retention period, a session lifetime, a review frequency, a rotation cadence) that AHDA has not approved, the row carries the PTBC, UGV or OQ ID that governs it, per TASK-004 §1. The value is not copied from Section 22 as if approved unless Section 22 itself states it as AHDA's decision.
6. **If no catalogue row satisfies the policy**, the policy is a **gap**: add a CTL row, name the task that will carry it (or specify a new task as a required workbook edit, §10), and record the gap in §9. A policy is never mapped to the nearest existing control to close the row.
7. **Confirm the three anchored rows** (CS-023, CS-024, CS-028) against the text: the anchor is a number-to-family tie, and the text may distribute the three numbers across CTL-40 to CTL-46 differently from the primary/supporting split assumed in §5.
8. **Cross-check the count**: 33 rows, none Unallocated, each with a task ID or an Open Question — the TASK-010 validation check.

## 7. Security-category task traceability

The second TASK-010 acceptance criterion: no Security-category task exists without a traceable CS-0xx source or an explicit non-RFP security-best-practice justification. The workbook has ten Security-category tasks. Since only one of them cites a CS number, the traceability is recorded at three levels: a CS number, a Section 22 fragment or PTBC whose source spec is Section 22, or a best-practice justification stated here explicitly.

| Task | Wave | Primary CTL rows | CS-0xx source | Section 22 / ADR / PTBC trace | Non-RFP best-practice justification (where the trace is not to Section 22) | Trace class |
| --- | --- | --- | --- | --- | --- | --- |
| TASK-019 Secret Management Store | W1 | CTL-18 | pending (OQ-015) | **Section 22.1, F-3** — no secret in source, config, logs, screenshots or fixtures; ADR-001 C-5 | — | **Section 22 (fragment)** |
| TASK-029 MFA & Privileged Access | W1 | CTL-07 | pending (OQ-015) | **PTBC-027** (source spec: FG-03; Section 22); ADR-010 | — | **Section 22 (via PTBC-027)** |
| TASK-033 Auth & Access Audit Logging | W1 | CTL-25, CTL-26, CTL-27 | pending (OQ-015) | **PTBC-029** (source spec: FG-06; Sections 18, 22); PTBC-011; Blueprint Section 18 | — | **Section 22 (via PTBC-029)** |
| TASK-077 Input Validation & Output Encoding | W2 | CTL-11 | pending (OQ-015); indirect: TASK-082's CS-023/024/028 test scope names "injection", and this control is what makes that scope pass | `api-conventions.md` R-24 (400/422 split) | **OWASP ASVS 4.0 V5** (Validation, Sanitization and Encoding); OWASP Top 10 2021 A03 Injection. No workbook cell cites an RFP or Blueprint source for this task. | **Best practice + indirect CS** |
| TASK-078 Rate Limiting, CORS & Headers | W1 | CTL-12 | pending (OQ-015) | `api-conventions.md` §4.8 (429 envelope) | **OWASP ASVS 4.0 V2.2.1** (anti-automation on authentication), **V14.4** (HTTP security headers), **V14.5** (HTTP request header validation / CORS); OWASP Top 10 2021 A05 Security Misconfiguration. No workbook cell cites an RFP or Blueprint source for this task. | **Best practice** |
| TASK-079 CSRF Protection | W1 | CTL-13 | pending (OQ-015) | Dependent on the session model from TASK-028 (ADR-007) | **OWASP ASVS 4.0 V4.2.2** (CSRF protection for state-changing functionality). No workbook cell cites an RFP or Blueprint source for this task. | **Best practice** |
| TASK-080 Secret Scanning & Dependency Gate | W1 | CTL-42 | pending (OQ-015) | **Section 22.1, F-3** (TASK-019's acceptance criterion relies on this scan) and **F-2** ("required quality/security gates"); Release Checklist "Dependency & Secret Scanning Clean" | — | **Section 22 (fragments)** |
| TASK-081 Threat Modeling & Security Review | W5 | CTL-43 | pending (OQ-015); indirect: precondition of TASK-082 (CS-023/024/028) | **Section 22.1 secure-SDLC requirement** (F-4, as the dependency TASK-082 is gated on); Release Checklist "Security Review & Threat Modeling Signed Off" | Threat modelling (STRIDE) is the standard secure-SDLC practice the pen test is scoped from | **Section 22 (secure SDLC) + indirect CS** |
| TASK-082 External Penetration Test | W6 | CTL-44 | **CS-023 / CS-024 / CS-028** | Section 22.1, F-4; Release Checklist "External Penetration Test Passed" | — | **CS number (direct)** |
| TASK-083 Logging Redaction | W1 | CTL-27, CTL-22 | pending (OQ-015) | **ADR-010** (redaction aligns with the classification taxonomy); **Blueprint Section 18** Audit/Log/SIEM boundary; ADR-004; ADR-018; Section 22 F-5 via TASK-090's "PII redaction consistent with the logging-redaction policy" | — | **ADR + Section 18 + Section 22 (fragment)** |

Result: **10 of 10** Security-category tasks are traceable — 1 to a CS number, 6 to a Section 22 fragment or a PTBC sourced from Section 22, and 3 (TASK-077, TASK-078, TASK-079) to an explicit OWASP ASVS best-practice justification with no RFP source claimed. The three best-practice rows are the ones AHDA Cybersecurity must either accept as non-RFP additions or re-source to a CS number when Section 22 arrives (§11.3); either outcome keeps the task.

The justification text is recorded here, not in the workbook. §10.3 specifies the one-line workbook edit that puts it on the task row itself, so the criterion holds in the workbook and not only in this record.

## 8. PTBC-026 to PTBC-030 linkage

The TASK-010 validation check requires each of the five cybersecurity governance themes to be linked to a matrix row.

| PTBC | Subject (per `ptbc-themes.csv`) | Matrix row | Task | Status |
| --- | --- | --- | --- | --- |
| PTBC-026 | PENDING — not recoverable; within the cybersecurity governance group | None assignable | — | **Unlinked** — subject unknown (Appendix F.2, OQ-015) |
| PTBC-027 | MFA and privileged-access policy; step-up trigger list | **CTL-07** | TASK-029 | **Linked.** Partially resolved (ADR-010); MFA/PAM detail and trigger list open; trigger list follows UGV-01 |
| PTBC-028 | PENDING — as above | None assignable | — | **Unlinked** — subject unknown |
| PTBC-029 | SIEM forwarding subset | **CTL-26** | TASK-033 | **Linked.** Open; gate Before Production |
| PTBC-030 | PENDING — as above | None assignable | — | **Unlinked** — subject unknown |

Two observations for whoever fills these from Appendix F.2, recorded as observations and not as allocations:

- **UGV-01** (data sensitivity and field-level classification taxonomy, owner AHDA Cybersecurity + PMO) is a cybersecurity governance value that TASK-004 §6 found to carry no PTBC ID anywhere. It is the kind of subject the 026–030 group would hold. If Appendix F.2 confirms it is one of 026/028/030, UGV-01 is retired into that ID and CTL-07, CTL-19 and CTL-27 re-cite it.
- **The backup retention period** is already PTBC-048 and is not a candidate for this group.

## 9. Candidate gaps — policies the catalogue may not carry

These are controls that a cybersecurity policy set of this kind commonly requires and that **no workbook task carries today**. They are listed so that step 6 of §6 has somewhere to look, and so that AHDA Cybersecurity can say now whether any of them is in Section 22. They are not requirements of this record, and no task is created for them here.

| # | Candidate control | Nearest task | What the workbook says today | If Section 22 requires it |
| --- | --- | --- | --- | --- |
| G-1 | **Anonymisation or masking of production-derived data in non-production environments** | TASK-089, TASK-027 | TASK-089 tests against a "production-representative data volume"; nothing states whether that is synthetic or a PROD copy. CTL-23 covers residency of non-PROD data, not its content. | New control under CF-3; TASK-089 carries it; depends on ADR-001 R-4 |
| G-2 | **Session lifetime, idle timeout, concurrent-session and revocation-on-deactivation values** | TASK-028, TASK-031 | TASK-028 implements "session/token expiry and refresh"; ADR-007 defers "session-invalidation behavior" to environment setup. The values are AHDA's and carry no PTBC/UGV ID. | Values registered as a UGV (TASK-004 §6) and seeded as configuration; CTL-06 extended |
| G-3 | **Key and secret rotation cadence** (e.g. `JWT_SIGNING_KEY`, `SIEM_API_TOKEN`, provider keys) | TASK-019 | Rotation *procedure* is documented and rehearsed (CTL-18); no cadence is stated. | Cadence is an AHDA value; register as UGV; runbook carries the schedule |
| G-4 | **Periodic access review / recertification** of role and profile assignments | TASK-031, TASK-110 | FG-03 provides assignment, versioning and audit; no task provides a periodic review or attestation. | New control under CF-2; FG-03 report or FG-06 audit query; owner AHDA |
| G-5 | **Vulnerability management during operations** — patching cadence for runtime, base images and dependencies after go-live, beyond the CI scan | TASK-080, TASK-097 | CTL-55 keeps the weekly scan; no task states a remediation SLA for findings during the operations period. | Remediation SLA is contractual (OQ-009 / UGV-06); runbook carries it |
| G-6 | **Security incident response** as distinct from operational incident response — breach notification path, evidence preservation, regulator notification | TASK-093 | TASK-093 covers operational severity and escalation; nothing names a security-incident procedure or a notification obligation. | Depends on ADR-001 R-4 (regulatory instrument); new control under CF-5; TASK-093 carries it |
| G-7 | **Web application firewall rule baseline and tuning** — which managed rule sets are enabled and how false positives are handled | TASK-021 | CTL-04 stands up the WAF; no rule baseline is stated. | Engineering value, not an AHDA value; TASK-021 records the baseline |

## 10. Required workbook edits (not applied)

Per the workbook's rule — revisions are re-issued, not overwritten in place (TASK-001 §4) — these are specified, not applied.

| # | Edit | Rows | Why |
| --- | --- | --- | --- |
| 10.1 | Add **OQ-015** to the Open Questions sheet as stated in §5 (Section 22 policy register and Appendix F.2; owner PMO Engagement Lead / AHDA Cybersecurity; blocking TASK-010 ratification and, through §9, the W1 secure-engineering tasks; proposed gate Before Build/Integration Build). OQ-013 and OQ-014 are already owed under TASK-004 §8.3, so this is the next free identifier. | 1 | 30 register rows and 3 PTBC themes are blocked on one document with no Open Question tracking it |
| 10.2 | Append to the Detailed Description of the ten Security-category tasks (TASK-019, 029, 033, 077, 078, 079, 080, 081, 082, 083): `Implements CTL-nn per docs/architecture/cybersecurity-control-matrix.md §4; CS-0xx source per §5 (pending OQ-015).` with the CTL IDs from §7 | 10 | Makes the catalogue the thing the task builds against, from the task row itself |
| 10.3 | Append to TASK-077, TASK-078 and TASK-079: `Non-RFP security best practice: OWASP ASVS 4.0 <chapter> (cybersecurity-control-matrix.md §7).` with the chapter from §7 | 3 | The second acceptance criterion requires the justification to be *explicit*; today these three rows cite no source at all |
| 10.4 | Add `docs/architecture/cybersecurity-control-matrix.md` to the TASK-010 Deliverables cell as the canonical path, and clear the Gate Decision Applied / Participation Amendment cells if TASK-008's text has been copied into them in any revision | 1 | §1 |
| 10.5 | In TASK-082, once Section 22 is available, expand "CS-023/024/028" to the three policy titles so the only CS citation in the workbook is readable without the Blueprint | 1 | §5 |
| 10.6 | Register the seven §9 candidates that AHDA Cybersecurity confirms as in-scope: G-2, G-3 and G-5 as UGV rows in `unassigned-gated-values.csv` (TASK-004 §6), the rest as scope additions to the named nearest task | ≤7 | Keeps a policy that no task carries from being discovered at UAT |

## 11. Open items blocking RATIFICATION

Owner: PMO Engagement Lead unless stated.

| # | Item | Why it blocks |
| --- | --- | --- |
| 11.1 | **Blueprint v2.0 Section 22 — the CS-001 to CS-033 policy text** (OQ-015). | 30 of 33 register rows cannot be allocated; the three anchored rows have no subject text. The TASK-010 validation check "cross-check all 33 CS policies are represented" cannot be executed against the policies, only against the numbers. |
| 11.2 | **Blueprint v2.0 Appendix F.2** — subjects of PTBC-026, PTBC-028, PTBC-030 (TASK-004 §9.1). | Three of the five governance themes the TASK-010 validation check names cannot be linked to a row. |
| 11.3 | **AHDA Cybersecurity acceptance of the three best-practice justifications** (TASK-077, 078, 079 — §7), or their re-sourcing to a CS number. Owner: AHDA Cybersecurity. | The second acceptance criterion is met by the delivery team's justification; whether a non-RFP addition is *accepted* is AHDA's call. |
| 11.4 | **ADR-001 R-4 — the governing regulatory instrument and AHDA's data classification.** Owner: AHDA Cybersecurity. Extended here with one question: **are CS-001 to CS-033 AHDA's own policy set, or a profile of a national framework** (for example the NCA Essential Cybersecurity Controls)? If the latter, the framework's control IDs give the register a second, verifiable axis. | Scopes CTL-23, CTL-28, CTL-49, CTL-51 and G-1, G-6; and determines whether Section 22 is the *whole* obligation or a subset of a larger one. |
| 11.5 | **AHDA Cybersecurity acceptance of the §4 catalogue** as the control set the Security-category tasks build against from W1, independent of 11.1. Owner: AHDA Cybersecurity. | Without it, W1 tasks build against a catalogue that has not been ratified by the policy owner, and a Section 22 policy that contradicts a CTL row would be found after build. |
| 11.6 | **§9 candidate gaps** — a yes/no per row from AHDA Cybersecurity. Owner: AHDA Cybersecurity. | A "yes" on G-1, G-2 or G-6 changes W1 scope. |

## 12. Acceptance-criteria check

| # | Criterion | Result |
| --- | --- | --- |
| 1 | Control matrix has one row per CS-0xx policy with a named implementing task ID from this workbook (or an Open Question if the policy value is AHDA-TBC, e.g. exact RPO/RTO) | **PARTIAL.** 33 rows exist (§5). **3** name an implementing task (CS-023, CS-024, CS-028 → TASK-082 with a supporting chain) from the only CS citation in the workbook. **30** carry an Open Question, OQ-015 — but the Open Question is for the *policy text*, not for an AHDA-TBC *value*, which is not the case the criterion's parenthesis contemplates. The criterion is therefore met in form and not in substance until Section 22 arrives; §6 makes the remaining work a lookup. The RPO/RTO example the criterion gives is itself resolved (PTBC-048, CTL-35/36); only the retention period remains open (OQ-003). |
| 2 | No Security-category task exists without a traceable CS-0xx source or an explicit non-RFP security-best-practice justification | **MET in this record; NOT YET in the workbook.** All 10 Security-category tasks are traced in §7: 1 to a CS number, 6 to Section 22 fragments or Section-22-sourced PTBCs, 3 to an explicit OWASP ASVS justification. The workbook rows for TASK-077, 078 and 079 cite no source; §10.3 specifies the edit. AHDA acceptance of the three justifications is 11.3. |
| Validation | Cross-check all 33 CS policies are represented in the matrix | **MET for the identifiers, NOT EXECUTABLE for the policies.** CS-001 to CS-033 each have a row. Representation of the *policy* cannot be checked without its text (11.1). |
| Validation | Confirm PTBC-026 through PTBC-030 are each linked to a matrix row | **PARTIAL — 2 of 5.** PTBC-027 → CTL-07, PTBC-029 → CTL-26. PTBC-026, 028, 030 have no recoverable subject (11.2). |

## 13. Sign-off

| Role | Decision | Name | Date |
| --- | --- | --- | --- |
| AHDA Cybersecurity | §4 catalogue (CTL-01 to CTL-55) accepted as the control set for W1 onward: Accepted / Revised (state which). §7 best-practice justifications for TASK-077/078/079: Accepted / Re-sourced. §9 gaps G-1 to G-7: in scope Y/N per row. 11.4: policy set origin stated | | |
| PMO Engagement Lead | OQ-015 raised and Section 22 / Appendix F.2 supplied (11.1, 11.2). §10 workbook edits: authorised / declined | | |
| Engagement Architect | §5 register re-issued per §6 within one working week of receipt of Section 22; §8 links completed on receipt of Appendix F.2 | | |
| AHDA IT | CTL-01, CTL-04, CTL-47, CTL-54 inputs (UGV-07: region, tenancy, product availability) per ADR-001 confirmation request | | |

On 11.1, 11.2 and 11.5 closing, the status in the header becomes **RATIFIED** and §5 is re-issued with every row allocated.

## 14. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-20 | Initial record. Eight control families fixed from the TASK-010 row. Control catalogue of 55 controls across 50 implementing tasks, each sourced to a workbook cell, ADR, PTBC or repository record. CS register of 33 rows: 3 anchored to TASK-082's citation, 30 blocked on Section 22 (OQ-015 proposed). Allocation procedure recorded. All 10 Security-category tasks traced (1 CS, 6 Section 22 / PTBC, 3 OWASP ASVS). PTBC-027 and PTBC-029 linked; 026/028/030 unlinked pending Appendix F.2. 7 candidate gaps, 6 workbook edits and 6 ratification blockers recorded. | Architecture (TASK-010) |

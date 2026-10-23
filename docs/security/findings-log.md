# Security Review Findings Log

| Field | Value |
| --- | --- |
| Task | TASK-081 — Conduct Threat Modeling & Security Review for Sensitive Features |
| Reviewed code | `dev` at `6f19809`, 2026-10-16 |
| Models | `threat-model-authentication.md`, `threat-model-authorization.md`, `threat-model-documents.md`, `threat-model-external-participation.md`, `threat-model-nafath.md`, `threat-model-sms-delivery-receipt.md`, `threat-model-data-import.md` |
| Rules | `README.md` §3 (rating), §4 (when a finding is raised, dispositions), §5 (the gate) |
| Check | `python3 docs/security/findings-log-check.py [--gate [--verify-prs]]` |

## 1. Gate status

The Release Checklist's "Security Review & Threat Modeling Signed Off" needs every High or Critical finding closed. A finding is closed by merged mitigating PRs, or by a risk acceptance AHDA has signed.

| | Count |
| --- | --- |
| Findings | 80 |
| High or Critical | 37 |
| — closed by merged PRs (`Mitigated`) | 27 |
| — **open** | **10**: SR-10, SR-13, SR-26, SR-38, SR-41, SR-53, SR-74, SR-75, SR-78, SR-80 |
| Risk acceptances signed by AHDA | 0 of 3 requested (`risk-acceptances.md`) |

**The gate is not green.** Each open High needs one of the following:

| Finding | What closes it | Owner |
| --- | --- | --- |
| SR-10 Refresh tokens cannot be revoked | Rotation with reuse detection, plus server-side sign-out (a fix PR); or RA-01 signed | Engagement Architect; AHDA Cybersecurity for RA-01 |
| SR-13 R01 enrols a second factor at first sign-in | `Identity:Mfa:AllowEnrolmentAtSignIn=false` in SIT, UAT and PROD, with out-of-band enrolment (a configuration PR); or RA-02 signed | AHDA Cybersecurity (PTBC-027) |
| SR-26 No field is classified, so nothing is masked by audience | AHDA publishes the DATA_CLASSIFICATION items and a FIELD_CLASSIFICATION version (UGV-01); the model is then updated to MITIGATED on PR #34 | AHDA Cybersecurity |
| SR-38 Uploads trusted by declared type | Signature check, extension allowlist, declared-against-sniffed comparison (a fix PR) | Backend lead (TASK-037 follow-up) |
| SR-41 No malware scanner selected | Provider selected (in-Kingdom, no retention) and its adapter merged | Security Lead |
| SR-53 Nafath does not bind the person to the account | An expected identity captured at onboarding and compared with `sub` (a fix PR, after OQ-007); or RA-03 signed | AHDA Cybersecurity, FG-03 owner |
| SR-74, SR-75 SMS receipt callback | TASK-103 merges with requirements R-1 to R-10 of its model; or the callback is out of the UAT release and the models say so | TASK-103 owner |
| SR-78 Workbook XML from an upload | The PR that merges TASK-102's import (controls and tests already on `dev`) | Backend lead |
| SR-80 The import's authority outlives the cutover | AHDA accepts it for the cutover window, and `DATA_IMPORT` is withdrawn from R01 afterwards | AHDA Cybersecurity; PMO |

**Recommended before UAT, though Medium:** SR-27. REV-IAM-002 is marked Closed in FG-03, CTL-43 asks this review to verify it, and it does not hold: an R01 can sign in as any business user (RBAC-T07). The fix is small: make `directory_subject_id` directory-authoritative once set, as the job title already is, and put `IdentityAccess_UpdateUser` on the step-up list.

## 2. Findings

Rating is the highest inherent rating among the finding's threats. Evidence for `Mitigated` names the merged PRs that shipped the control; follow-ups that do not change the rating are listed with them.

| ID | Threats | Rating | Finding | Owner | Disposition | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| SR-01 | AUTH-T01, AUTH-T02 | Critical | Directory sign-in: LDAP filter injection and empty-password (anonymous) bind | Backend lead | Mitigated | PR #32 (escaping, three-layer empty-password refusal), PR #87 (`LdapFilterTests`) |
| SR-02 | AUTH-T07, AUTH-T11 | Critical | Forged or replayed OIDC response; forged or confused platform token | Backend lead | Mitigated | PR #32. Follow-up: an explicit `alg:none` test |
| SR-03 | AUTH-T06 | High | Directory traffic intercepted or the DC impersonated | Backend lead, DevOps | Mitigated | PR #32. Follow-ups: a test that an untrusted LDAPS certificate is refused; the container trusts AHDA's internal CA |
| SR-04 | AUTH-T15 | High | A session without the required second factor, by any path | Backend lead | Mitigated | PR #33 |
| SR-05 | AUTH-T22, AUTH-T23 | High | Credentials or tokens leaked; CSRF on session endpoints | Backend lead | Mitigated | PR #32 (`CredentialLoggingTests`), PR #88 (origin allowlist), PR #89 (`CsrfProtection`) |
| SR-06 | AUTH-T03 | High | Password spraying from many addresses | DevOps; AHDA IT | Mitigated | PR #88 (per-address limit), PR #32 (rejection floor), PR #38 (audit and SIEM). Follow-ups: provision Memorystore so instances share counters (`endpoint-hardening.md` F-2); a per-account signal in the SIEM (AHDA Cybersecurity) |
| SR-07 | AUTH-T04 | Medium | Deliberate AD lockout of named accounts | AHDA IT | Open | Confirm AD's lockout threshold and window against the platform's 30 a minute per address (CS-011) |
| SR-08 | AUTH-T08, NAF-T05 | High | ID-token validation has no algorithm pin and no negative tests for issuer, audience, signature or expiry | Backend lead | Mitigated | PR #32, PR #78 (library validation of each). Follow-ups: pin `ValidAlgorithms` to the IdP's and Nafath's algorithm; add the four negative tests to `SingleSignOnTests` and the Nafath suite |
| SR-09 | AUTH-T09 | High | The SSO subject claim must be immutable and equal the directory subject | AHDA IT (Identity) | Mitigated | PR #32 (exact match on a configured claim; no role claims). Follow-up: AHDA IT confirms the claim at environment setup (`sso-directory-integration.md` F-2) |
| SR-10 | AUTH-T13 | High | A stolen refresh token keeps a session for up to 8 h; tokens are not rotated, reuse is not detected, sign-out is client-only | Engagement Architect | Open | Fix: rotate refresh tokens with reuse detection and add a server-side session revocation list (also ends sessions on disable). Or AHDA signs RA-01 |
| SR-11 | AUTH-T14 | High | Script on the SPA's origin reads the tokens from `sessionStorage` | Frontend lead, DevOps | Mitigated | PR #87 (sink lint, `isAppPath`), PR #88 (CSP on API responses). Follow-up: the SPA's serving sends `script-src 'self'` (`endpoint-hardening.md` F-3) |
| SR-12 | AUTH-T16 | High | Second-factor code guessing, or another person's challenge | Security Lead; AHDA Cybersecurity | Mitigated | PR #33 (1 s floor, shared limit, subject sent, explicit verdict). Follow-up: the selected provider limits attempts per user and binds the challenge to the user (`mfa-privileged-access.md` F-1, F-4) |
| SR-13 | AUTH-T17 | High | Whoever holds an unenrolled R01's password enrols their own factor at sign-in | AHDA Cybersecurity | Open | Set `Identity:Mfa:AllowEnrolmentAtSignIn=false` in SIT, UAT and PROD and enrol R01 out of band (`mfa-privileged-access.md` F-2). Or AHDA signs RA-02 |
| SR-14 | AUTH-T19 | High | Privileged operations outside the step-up list | AHDA Cybersecurity | Mitigated | PR #33 (step-up mechanism, seven operations). Follow-up: AHDA confirms the list; candidates are `IdentityAccess_UpdateRole`, `_CreateUser`, `_UpdateUser` (SR-27), the external-entity status operations, integration enable and disable, and the Activity rebuild |
| SR-15 | AUTH-T26 | High | Trusting the IdP's `amr` as the second factor | AHDA IT, AHDA Cybersecurity | Mitigated | PR #33 (trusted only when configured, with a capped `auth_time`) |
| SR-16 | AUTH-T18 | Medium | A refresh token alone enrols a new second factor through step-up, and nothing can reset an enrolment | Backend lead | Open | Step-up verifies only: refuse enrolment there. Add an audited administrator reset of `mfa_enrolled_at` |
| SR-17 | AUTH-T10 | Medium | Login CSRF on the SSO callback | Frontend lead | Open | The `/auth/callback` route reads the transaction only from `sessionStorage` written at the redirect and clears it after one use |
| SR-18 | AUTH-T12 | Medium | `JWT_SIGNING_KEY` disclosure mints any session and opens SSO and Nafath transactions | DevOps; AHDA Cybersecurity | Open | Secret Manager IAM limited to the runtime; rotation in the runbook; consider separate keys per token kind |
| SR-19 | AUTH-T20 | Medium | A disabled user keeps own-record endpoints for up to 15 min | Backend lead | Open | Check the principal's status in `[AllowAnyAuthenticatedUser]` actions, or accept the 15-minute tail |
| SR-20 | AUTH-T21 | Medium | Throttled sign-ins are not audited; the SIEM is not named | AHDA Cybersecurity | Open | Decide whether a throttled burst is an AUTHENTICATION event (`endpoint-hardening.md` F-6); name the SIEM (`auth-access-audit-logging.md` F-1) |
| SR-21 | AUTH-T25 | Low | A `.` or `..` challenge id changes the MFA verify path | Backend lead | Open | Refuse a challenge id that is not a provider token (e.g. `^[A-Za-z0-9_-]+$`) |
| SR-22 | RBAC-T01, RBAC-T23 | High | An endpoint without a declaration; an engine failure granting access | Backend lead | Mitigated | PR #34 |
| SR-23 | RBAC-T02, RBAC-T03 | High | IDOR by id; collection and count leaks | Backend lead | Mitigated | PR #34 (engine), PR #42 (record scopes), and each module's record check (the sweep in `threat-model-authorization.md` §3 found no exception). Follow-up: a contract test per by-id route that an out-of-scope id is 404 |
| SR-24 | RBAC-T04, RBAC-T12, RBAC-T17, RBAC-T21 | Critical | Cross-entity enumeration; stale grants; mass assignment of scope; approval through the wrong role | Backend lead | Mitigated | PR #34, PR #35, PR #76 (`EntityIsolationTests`) |
| SR-25 | RBAC-T05 | High | ALL scope bypassing classification, state or actor | Backend lead | Mitigated | PR #34 |
| SR-26 | RBAC-T22 | High | No field is classified, so an entity sees financial figures ADR-013 and CTL-19 mean to mask by audience | AHDA Cybersecurity | Open | AHDA publishes the DATA_CLASSIFICATION items and a FIELD_CLASSIFICATION version (UGV-01; `financial-kpi.md` F-6). The mechanism is PR #34 and is tested |
| SR-27 | RBAC-T06, RBAC-T07 | Medium | REV-IAM-002 does not hold: an R01 re-points a user's `directory_subject_id` and signs in as that user | Backend lead | Open | Refuse a change to `directory_subject_id` once set (as for the job title), or require step-up and a second administrator; add `IdentityAccess_UpdateUser` to the step-up list; a test. Recommended before UAT (§1) |
| SR-28 | RBAC-T10 | Medium | One administrator re-ranks DATA_CLASSIFICATION or retires the FIELD_CLASSIFICATION in force, unmasking everything | Backend lead | Open | Refuse `sort_order` changes on a PUBLISHED classification item; put retirement of FIELD_CLASSIFICATION under separation of duties, or fail closed when no version is in force |
| SR-29 | RBAC-T08 | Medium | Privileged assignments carry no reason or second approval | PMO; Backend lead | Open | FG-03 §10.1: a reason, and dual control for R01 assignments |
| SR-30 | RBAC-T11, EXT-T03, EXT-T25 | Medium | R08 entity-wide assignments; a request addressed to an entity outside the project | PMO; FG-03 owner | Open | Decide ADR-013's multi-entity model: per-project R08 for a non-delivering entity, and which entities a request may address (`external-participation.md` F-15) |
| SR-31 | RBAC-T13 | Medium | Effective dates untested; no expiry audit event | Backend lead | Open | Tests with a future `starts_at` and a past `ends_at`; the ROLE_ASSIGNMENT_EXPIRED event (FG-03 §22.2) |
| SR-32 | RBAC-T14 | Medium | DEPT scope follows a department left since sign-in | PMO | Open | Accept the bound (8 h), or act on the TASK-075 reconciliation's mismatches |
| SR-33 | RBAC-T20 | Low | Denials through reads are not rate-limited | Backend lead | Open | A per-user read limit, or a SIEM rule on denial bursts |
| SR-34 | RBAC-T25 | Medium | A per-project or OWN grant moves a project into another department | Backend lead | Open | Authorize the target department as well when `departmentId` changes |
| SR-35 | RBAC-T26 | Medium | Any RAISE holder edits or submits another person's request and can then approve it | Backend lead | Open | Edit and submit by the requester only, or record the submitter as the requester for SoD |
| SR-36 | RBAC-T27 | Medium | One person may approve several stages of one run | PMO (WF-11) | Open | Confirm WF-11's rule; if distinct approvers are required, enforce it in `ApprovalAuthority` |
| SR-37 | RBAC-T28, RBAC-T29 | Low | Owner fields accept any user; small existence oracles | Backend lead | Open | Restrict owners to the project's eligible people; answer 404 for an out-of-scope project on upload |
| SR-38 | DOC-T01 | High | Uploads are trusted by declared type: no signature check or extension allowlist | Backend lead | Open | Sniff the content signature, allowlist extensions per type, refuse a mismatch, keep archives out (WF-12 §5.1, DOC-CC-05); tests |
| SR-39 | DOC-T04, DOC-T05 | High | Stored XSS through served content; non-CLEAN bytes served | Backend lead | Mitigated | PR #42 (one CLEAN gate, attachment, `nosniff`), PR #43 (Blob save), PR #88 (CSP), PR #94 (the proving tests run in CI) |
| SR-40 | DOC-T08 | High | The scanner fails open | Security Lead | Mitigated | PR #42 (the port fails closed). The provider adapter must map every provider answer the same way (SR-41) |
| SR-41 | DOC-T07, DOC-T21, DOC-T24 | High | No malware scanner selected: residency, transport, verdict integrity and bomb limits unknown | Security Lead | Open | Select an in-Kingdom provider with no retention (CS-019, CS-031, CTL-50); adapter with TLS and authenticated verdicts; SCAN_FAILED on bombs |
| SR-42 | DOC-T09, DOC-T10, DOC-T11 | High | Evidence pinned wrongly; IDOR on documents; cross-entity reads | Backend lead | Mitigated | PR #42, PR #56 (`AchievementEvidenceTests`), PR #94 (the module suite in CI). Follow-up: a mismatched document and version test |
| SR-43 | DOC-T02 | Medium | Bidi and format characters in file names | Backend lead | Open | Strip Unicode category Cf from names |
| SR-44 | DOC-T06, DOC-T17 | Medium | No hash check on scan or serve; the runtime can overwrite and delete objects | Backend lead; DevOps | Open | Verify `checksum_sha256` or the object generation before scanning and serving (WF-12 §5.3); grant the runtime create and get only |
| SR-45 | DOC-T12 | Medium | External users see prior versions, links and internal-only classifications | PMO; AHDA Cybersecurity | Open | Appendix A grants and UGV-01 (an internal-only flag); hide prior versions from external audiences |
| SR-46 | DOC-T14 | Medium | Another project's or an archived document satisfies an evidence gate | Backend lead | Open | Link and designate only a document of the record's project, ACTIVE, with the link permission (DOC-CC-15) |
| SR-47 | DOC-T15 | Medium | A classification downgrade needs no stronger governance | Security Lead | Open | Put the downgrade on the step-up list, or require a second manager |
| SR-48 | DOC-T16 | Medium | Downloads are not audited | Security Lead | Open | An audit class for sensitive access (`document-management.md` F-10) |
| SR-49 | DOC-T19, DOC-T20 | Medium | Memory exhaustion by parallel transfers; no quota or fair scan queue | Backend lead; DevOps | Open | Stream from Cloud Storage without a temp copy; a download rate limit; per-project quotas and fair scanning |
| SR-50 | DOC-T23, DOC-T25 | Low | Orphaned objects; idempotency keys not replayed | DevOps; Engineering Architect | Open | A sweeper for unreferenced objects (`document-management.md` F-9); R-37 replay (F-15) |
| SR-51 | NAF-T03, NAF-T04, NAF-T07, NAF-T17 | Critical | Login CSRF, code theft, token confusion, transaction forgery | Backend lead | Mitigated | PR #78, PR #32 |
| SR-52 | NAF-T09, NAF-T10, NAF-T11 | High | Fail-open outage; national id kept; client secret leaked | Backend lead | Mitigated | PR #78 |
| SR-53 | NAF-T01 | High | Nafath verification does not bind the person to the account: a stolen password and the attacker's own Nafath account pass | AHDA Cybersecurity; FG-03 owner | Open | Capture an expected identity at onboarding (e.g. a keyed hash of the national id AHDA enters in FG-03) and compare it with `sub` (OQ-007, TBC-EXT-004). Or AHDA signs RA-03 |
| SR-54 | NAF-T08 | High | An unverified session continues after the flag is turned on | Backend lead | Mitigated | PR #78. Follow-ups: gate `BeginStepUpAsync`; a step-up refusal test |
| SR-55 | NAF-T18 | High | A stolen verification token completes the verification with the thief's identity | Frontend lead | Mitigated | PR #78 (`no-store`, single-purpose audience, user re-read). Follow-up: design how the SPA holds the token (F-4) |
| SR-56 | NAF-T21 | Medium | With the flag on, the SPA breaks for external users | Frontend lead | Open | A verification step in the SPA (`nafath-identity-verification.md` F-4); keep the flag off until it ships |
| SR-57 | NAF-T02, NAF-T14 | Medium | One Nafath identity verifies many accounts; who verified an account cannot be shown | AHDA Cybersecurity | Open | OQ-007 (keep a hash of `sub`, option B); confirm Nafath's traceability (F-2); make concurrent completions keep their audit event |
| SR-58 | NAF-T12 | Medium | `authorization_endpoint` not checked; HTTP allowed by configuration | Backend lead | Open | Require HTTPS and the authority's origin for every discovered endpoint; refuse `RequireHttpsMetadata=false` and an `http` callback outside Development |
| SR-59 | NAF-T15 | Medium | A reassigned account keeps the previous holder's verification | FG-03 owner | Open | Reset the Nafath columns when username, e-mail or `directory_subject_id` changes (`nafath-identity-verification.md` F-7) |
| SR-60 | NAF-T16 | Medium | Nafath columns can be set directly in the database | Database lead | Open | A guard trigger, or a reconciliation of references against `IdentityVerified` events |
| SR-61 | NAF-T22 | Low | Local refusals skew Nafath's monitored failure rate | Backend lead | Open | Publish telemetry only for calls that reached Nafath |
| SR-62 | EXT-T01, EXT-T02, EXT-T07, EXT-T09, EXT-T11, EXT-T13 | Critical | Cross-entity IDOR and enumeration; note leakage; spoofed submission; entity PM authority; value tampering | Backend lead | Mitigated | PR #76, PR #34 |
| SR-63 | EXT-T06 | High | The external projection still shows the task title and internal GUIDs | PMO; AHDA Cybersecurity | Mitigated | PR #76, PR #77 (projection by audience, withheld fields omitted). Follow-up: TBC-EXT-020 decides whether a task title is safe |
| SR-64 | EXT-T08, EXT-T10 | High | Acting after suspension; a colleague answering for the named responder | Backend lead, QA | Mitigated | PR #34, PR #76. Follow-ups: an integration test suspending an entity mid-session; an API test with a second entity user |
| SR-65 | EXT-T19 | High | Stored XSS from an entity's answer into AHDA's session | Frontend lead | Mitigated | PR #87, PR #77. Follow-up: any export or e-mail template of answers encodes them again |
| SR-66 | EXT-T04 | Medium | Activity reveals DRAFT requests to the entity | Backend lead | Open | Do not project DRAFT events to external audiences |
| SR-67 | EXT-T05 | Low | Masked fields usable as filters by external callers | Backend lead | Open | Ignore `reviewerUserId` and `actorUserId` filters for external callers |
| SR-68 | EXT-T12 | Medium | No content hash of submitted values; the authorising assignment not recorded | Backend lead | Open | Store a hash of the submitted values and the assignment id at submit (§23.2–23.3) |
| SR-69 | EXT-T18 | Low | One AHDA user may draft, review and apply | PMO | Open | Decide whether WF-13 needs reviewer separation |
| SR-70 | EXT-T20, EXT-T26 | Low | Unthrottled reads with per-row lookups; a 500 on a save-cancel race | Backend lead | Open | Batch the list's lookups; map the guard's refusal to 409 |
| SR-71 | EXT-T22 | Medium | Identity proofing and invitation of external users | FG-03 owner | Open | WF-13 §9.2 onboarding policy (with SR-53) |
| SR-72 | EXT-T23 | Medium | No test of WF-13 on a CLOSED or COMPLETED project | QA | Open | Appendix C scenario 9 as an integration test |
| SR-73 | EXT-T24 | Medium | Entity users hold report export and Activity beyond ADR-019 | Engineering Architect; PMO | Open | Add ADR-019 to the register and reconcile it with `reports.md` D-6 and the R08 grants |
| SR-74 | SMS-T01, SMS-T02, SMS-T05, SMS-T07, SMS-T09 | High | The SMS receipt callback must authenticate every receipt before reading it, match it to a sent SMS, and bound its load | TASK-103 owner | Open | TASK-103 ships R-1, R-2, R-5, R-6 and R-10 of `threat-model-sms-delivery-receipt.md` |
| SR-75 | SMS-T03, SMS-T04 | High | A receipt must never mean read, and must be idempotent and monotonic; the guard allows READ on SMS | TASK-103 owner | Open | TASK-103 ships R-3 and R-4, including the tightened `guard_notification_delivery` |
| SR-76 | SMS-T06, SMS-T10 | Medium | Receipt contact data in logs; receipts untraceable | TASK-103 owner | Open | R-7, R-9 |
| SR-77 | SMS-T08 | Medium | A provider that only sends form or GET receipts would need a CSRF exemption | TASK-103 owner; Security Lead | Open | R-8: choose a provider whose receipts pass `CsrfProtection` unchanged |
| SR-78 | DI-T02 | High | Workbook XML parsed from an untrusted upload (XXE, entity expansion) | Backend lead | Open | Controls built on `dev` (commit `e3cb86b`: DTDs prohibited, nothing resolved, parts capped, `XlsxImportWorkbookTests`); closed as Mitigated by the PR that merges TASK-102 |
| SR-79 | DI-T04 | Medium | A full-size import's transaction and register lock have not been measured | Backend lead | Open | Time a 5,000-row import of each register on representative volume before the cutover rehearsal |
| SR-80 | DI-T05 | High | `DATA_IMPORT` loads records around approval workflows, and stays granted after the cutover | AHDA Cybersecurity; PMO | Open | Each row is held to its module's rules and audited, governed rows land DRAFT; AHDA to accept the import's authority for the cutover window and withdraw `DATA_IMPORT` from R01 once it closes (a new profile version, TASK-110) |

## 3. Not threats: process and record items

| # | Item | Owner |
| --- | --- | --- |
| P-1 | **No Security Lead review has ever been requested.** Every feature record here carries the same finding: the CODEOWNERS teams do not exist on this personal-account repository, so the PR template's CTL-43 item could not be ticked (e.g. `sso-directory-integration.md` F-10, `authorization-engine.md` F-10). This review is not that review: it is the delivery team's threat model, and the Security Lead's sign-off of it is the gate | Maintainer; Security Lead |
| P-2 | **Blueprint v2.0 §22 and Appendix A are available** in `Project Files.zip`. `cybersecurity-control-matrix.md` §2 records §22 as unavailable and `authorization-engine.md` F-1 says the same of Appendix A. §22 names CS references per control area (e.g. Files: CS-012/019/028), and the Cybersecurity Guide's Appendix A gives the CS-xxx register | Engagement Architect (TASK-010 record) |
| P-3 | **Stale claims in the feature records**, listed in each model's last section. The records are not edited here; each owner corrects theirs | Record owners |
| P-4 | **ADR-018 and ADR-019 are not in the ADR register** (`sso-directory-integration.md` F-8; SR-73) | Engineering Architect |

## 4. Change log

| Date | Change |
| --- | --- |
| 2026-10-16 | First log (TASK-081): 77 findings, 8 open High |
| 2026-10-23 | TASK-102: `threat-model-data-import.md` added; SR-78 to SR-80 |

# External Penetration Test

| Field | Value |
| --- | --- |
| Task | TASK-082 — Commission External Penetration Test Before Production (P14 - Security Hardening & Compliance, P0) |
| Depends on | TASK-081 (threat models and findings log, this directory) |
| Record date | 2026-10-16 |
| Code at record date | `dev` at `b758fb6` (PR #94 merged) |
| Status | **NOT COMMISSIONED. GATE NOT GREEN.** No provider has been named and no environment exists to test (§2). No report has been received, so `penetration-test-report.md` and `penetration-test-tracker.md` hold no findings, and `penetration-test-check.py --gate` fails on purpose |
| Branch | `sec/task-082-external-penetration-test` |
| Directory | `docs/security` |
| Deliverables | **Penetration test report** (registered in `penetration-test-report.md`; the signed copy stays in AHDA's controlled store, §4.4), **remediation tracker** and **retest evidence** (`penetration-test-tracker.md`), checked by `penetration-test-check.py` |
| Environment variables / secrets | None (as the workbook row says). Test-account credentials are issued to the provider out of band (§4.3) |
| Gate decision applied | None in the workbook row |
| Control | CTL-44 (`cybersecurity-control-matrix.md`); AHDA CS-023 (vulnerability remediation by severity), CS-024 (penetration testing: separate environment, remediation plan, retest), CS-028 ("pen test web app before production and remediate vulnerabilities; gate release on critical/high finding closure or formal risk acceptance"), per the Cybersecurity Guide's Appendix A |
| Release gates | Release Checklist "External Penetration Test Passed" (owner: Security Lead); promotion gate G-4, UAT → PROD (`environment-separation.md` §4); TASK-100 depends on this task |
| Sources read | The TASK-082 row as supplied on 2026-10-16, checked against the workbook (`AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`): description, acceptance criteria, deliverables, directory, branch, validation check, and the Release Checklist row. From `Project Files.zip`: Blueprint v2.0 §22 and §22.1; the PM Platform Development & Cybersecurity Guide §10.1, §10.3, §11, §13, §15 and Appendix A (CS-023, CS-024, CS-028). In this repository: `cybersecurity-control-matrix.md` (CTL-44), `environment-separation.md` §4, `infrastructure-as-code.md` (R-2, R-3), and the TASK-081 records in this directory |

## 1. What this is

The delivery team cannot run this test itself. The workbook asks for an independent party, and the Cybersecurity Guide gives the choice of provider to AHDA Cybersecurity (§15: "Who performs vulnerability scanning and penetration testing; required tool/provider; lead time?"). This record is the delivery team's part of the engagement:

- what must exist before testing starts (§2);
- the statement of work the provider is engaged against (§3);
- the rules of engagement (§4);
- what the report and the retest must contain (§5);
- how findings are handled (§6);
- the records and the check that decide the Release Checklist row (§7).

How each part of the acceptance criterion is evidenced:

| Acceptance criterion | Evidence | Checked by |
| --- | --- | --- |
| A penetration test report exists | A `Final` report block in `penetration-test-report.md`: the provider, the tested commit, the test window, where the signed copy is held and its SHA-256, and coverage of areas A–D (§3.2) | `penetration-test-check.py` PR-1, G-1 |
| Zero unresolved Critical findings | Every Critical row in the tracker is `Remediated`, and the provider's latest retest records it `Closed`. A Critical cannot be risk-accepted | TR-2, G-2 |
| Every High remediated or formally risk-accepted by AHDA before go-live | Every High is `Remediated` with a `Closed` retest, or `Risk accepted` under an RA record in `risk-acceptances.md` that AHDA has signed | TR-2, G-3 |
| Retest evidence confirms remediated findings are closed | A retest block (`RT-n`) from the provider for each round, naming the build it retested. Every `Remediated` finding of any severity has a `Closed` latest result | RT-1, TR-3, G-4 |
| Validation check: the report's finding list reconciled against the tracker | Each finding in the report's list has exactly one tracker row with the same severity, and the tracker has no row the report lacks | TR-1 |

## 2. Prerequisites

None of these is met on 2026-10-16. They are listed in the order they block.

| # | Prerequisite | Owner | State on 2026-10-16 |
| --- | --- | --- | --- |
| PRE-1 | **A provider is named and engaged.** It must be independent of the delivery team: no person who built, reviewed or operates the platform, and no company in the delivery team. AHDA Cybersecurity names it, sets any accreditation it requires (CS-030, third parties), and contracts it against §3–§5 | AHDA Cybersecurity | Not named. Guide §15 lists this question as "Early in build"; it has not been asked in the workbook's Open Questions sheet. **Raise it now:** lead time is the provider's, and the test, the fixes and the retest must all finish before go-live |
| PRE-2 | **An environment to test.** CS-024 allows production or "an equivalent separate environment"; Appendix A prefers a production-like dedicated one. This test runs against **UAT**, deployed by Terraform with the release candidate's image and PROD's configuration: TLS, the edge and WAF (CS-028), the `Origin` allowlist, rate limits backed by a shared store, and the SPA served with `script-src 'self'` (`endpoint-hardening.md` F-2, F-3) | DevOps Lead; AHDA IT | **No environment exists.** Every `terraform apply`, DEV included, is held until AHDA's written confirmation (`infrastructure-as-code.md` R-1). The tenancy (R-3, UGV-07) and the DNS zone that `APP_BASE_URL` needs (`environment-separation.md` F-1) are empty. The region is set provisionally to `me-central2`, pending the same confirmation. Testing the local compose stack would not satisfy CS-024: it has no edge, no WAF, no MFA provider and no scanner |
| PRE-3 | **The release candidate is frozen** for the test window. The image digest promoted to UAT through G-3 and its commit are recorded before testing starts. A change deployed during the window is logged and reported to the provider | Delivery Lead | — |
| PRE-4 | **The known High findings are closed first.** G-3 (SIT → UAT) already requires every High or Critical finding in `findings-log.md` to be mitigated or risk-accepted. A test run while they are open spends the provider's days rediscovering them. On 2026-10-16, 8 are open (`findings-log.md` §1). SR-27 (an R01 can sign in as any user) is Medium, but the fix is small; fix it too | Finding owners, `findings-log.md` §1 | 8 High open; 0 of 3 risk acceptances signed |
| PRE-5 | **The integrations the test needs exist in UAT:** an MFA provider, so R01 can sign in and step-up can be attacked (TASK-029 D-8); a malware scanner, so uploads reach `CLEAN` (SR-41); and AHDA's IdP registered for UAT's callback. Nafath is tested only if it will be on in PROD. In that case it is on in UAT against Nafath's test service, with the SPA's verification step shipped (SR-56) | Security Lead; AHDA IT | No MFA provider or scanner selected |
| PRE-6 | **Criticality is classified** by AHDA (Guide §10.3). A critical system is tested at least every six months, an internet-exposed one at least annually. This engagement is the pre-launch test either way; the classification sets the next date (§8) | AHDA Cybersecurity | Not classified |
| PRE-7 | **The rules of engagement (§4) are signed** by AHDA Cybersecurity, the UAT environment owner and the provider | AHDA Cybersecurity | — |

## 3. Statement of work

### 3.1 Target

The UAT environment's single origin (`APP_BASE_URL`; the SPA at `/`, the API at `/api`, `environment-separation.md` D-1), reached the way PROD will be reached: through its load balancer and WAF. The API surface is `docs/api/openapi.v1.json`, with 309 paths and 402 operations at `b758fb6`. The provider uses the copy at the tested commit.

### 3.2 Areas

Areas A–D are the four the workbook names, and the report must state coverage of each. E and F are in CS-028 and the Guide's §11 examples ("file upload abuse, API weaknesses"). Each area lists the threat model the provider is given, so it can test whether the shipped controls hold rather than rediscover them.

| Area | What the provider attacks | Threat model |
| --- | --- | --- |
| **A. Authentication** | Directory sign-in (LDAP filter injection, empty and anonymous binds, user enumeration); the SSO callback (state, nonce, login CSRF, code replay); token forgery (`alg`, key, audience, issuer, expiry); refresh-token theft and reuse after sign-out; second-factor guessing, challenge confusion, enrolment by a password holder; step-up bypass; the sign-in throttle; session lifetimes | `threat-model-authentication.md` |
| **B. Authorization boundaries** | Vertical: every role R02–R08 against R01's administration and the step-up operations. Horizontal: every by-id route with another department's, another entity's and another owner's ids (IDOR), across list, count, export and filter endpoints. Data scope (OWN, DEPT, ENTITY, ALL); mass assignment of scope or owner fields; approval by the wrong role or by the requester; an R01 re-pointing a user's directory subject (SR-27) | `threat-model-authorization.md` |
| **C. Injection** | SQL (every raw-SQL site, sort and filter parameters); LDAP; header and path injection, including `.` and `..` in path segments (SR-21); stored and reflected XSS through request text, entity answers, file names (bidi and format characters, SR-43) and report exports; formula injection in CSV and spreadsheet exports; SSRF through any configurable URL (OIDC discovery, integration endpoints) | All models; `input-validation.md`, `output-encoding-checklist.md` |
| **D. External-entity attack surface** | With external accounts in two entities: cross-entity reads, enumeration and writes; what the WF-13 projection, Activity and filters reveal (SR-63, SR-66, SR-67); acting after the entity or user is suspended; answering for another responder; Nafath verification, if in scope (PRE-5) | `threat-model-external-participation.md`, `threat-model-nafath.md` |
| **E. Documents** | Content-type and extension spoofing (SR-38); malware and the EICAR file; archive and decompression bombs; serving a non-CLEAN version; download authorization; XSS through served content | `threat-model-documents.md` |
| **F. Web and API hardening** | TLS configuration; security headers and CSP on the SPA and the API; CORS and the `Origin` allowlist; CSRF (`CROSS_SITE_REQUEST_REFUSED`); rate limits; error disclosure; what is exposed outside Development (OpenAPI, health); the WAF's effect | `endpoint-hardening.md`, `csrf-protection.md` |

### 3.3 Out of scope

- Denial of service and load. Load belongs to TASK-086. Here the rate limits are tested only to confirm they engage.
- Social engineering, phishing and physical access.
- The services the platform consumes: AHDA's directory and IdP, Nafath's service, the e-mail and SMS providers, and the GCP control plane. How the platform *uses* them is in scope (A, D).
- Production, and every AHDA system other than the UAT environment.
- The SMS delivery-receipt callback, unless TASK-103 is merged into the tested build. If it is, add it to area A/F with `threat-model-sms-delivery-receipt.md`.

### 3.4 Approach

**Grey box.** The provider is given the OpenAPI document, the threat models and findings log in this directory, and the test accounts (§3.5). It is not given source access unless AHDA Cybersecurity asks for a code-assisted test.

**Reference standards** (proposed by the delivery team; AHDA Cybersecurity confirms): OWASP Web Security Testing Guide v4.2 for method, the OWASP API Security Top 10 (2023) for the API, and OWASP ASVS 4.0.3 Level 2 as the bar a control is judged against.

### 3.5 Test accounts and data

All accounts and data are synthetic (Guide §11.1). DevOps creates them in UAT before testing starts, and disables them when the final retest ends.

| Accounts | Why |
| --- | --- |
| Two users for each internal role R01–R07, in two departments | Horizontal tests need a peer; DEPT scope needs a second department |
| One R01 with a second factor enrolled, one without | Step-up; enrolment at first sign-in (SR-13) |
| Two external entities, each with an R08 and an external R04 (ADR-013) | Cross-entity tests (area D) |
| One disabled user, and one user of a suspended entity | Session and token behaviour after access ends |

Data: projects in each department and each entity, in ACTIVE, SUSPENDED and CLOSED states; a WF-13 request in each state; documents both CLEAN and quarantined.

## 4. Rules of engagement

### 4.1 Window and contacts

The test window, the provider's source addresses and the contacts are fixed in the signed rules of engagement (PRE-7). Each side names a contact reachable during the window. The delivery team's contact is the Security Lead.

### 4.2 WAF

The application is tested with the provider's addresses exempted from the WAF's blocking rules, which still log. Otherwise the report describes the WAF rather than the application, and a rule change later would expose what was never tested. The WAF itself is tested separately in area F, without the exemption.

### 4.3 Stop and report at once

The provider stops and calls the Security Lead immediately, without waiting for the report, when:

- it reaches anything outside the UAT environment;
- it reaches data that is not synthetic;
- it finds a Critical, or a High in area A, B or D. The delivery team starts the fix during the window (§6).

Test-account credentials are issued out of band, never by e-mail with the username. They are rotated after the test, and disabled after the final retest.

### 4.4 Handling the report

**This repository is public.** The report, its evidence and the retest reports are kept only in AHDA's controlled document store, at the location AHDA Cybersecurity names. They are never committed here, attached to an issue, PR, CI artifact or chat, or pasted into an AI tool (Guide §10.2).

This repository holds only the register of each report: its metadata, the SHA-256 of the signed copy, and the finding list. Until a finding's retest closes it, its title in the register names a category and an area (for example "Authorization: object-level access, area B"), never an endpoint, parameter or payload. After it closes, the title may be made specific. A fix PR describes what the code now does, not how the flaw was exploited.

## 5. What the provider delivers

### 5.1 The report

- Scope tested; the tested commit and image digest; the test window; the method and the standards applied.
- **A coverage statement for each area A–F:** what was tested, including what was tested and found sound. Without it, a report with no findings proves nothing.
- For each finding: the provider's id, a title, a severity with a CVSS vector, the affected area and component, reproduction steps, evidence and a recommendation.
- The testers' names and qualifications, and a statement of independence from the delivery team.

### 5.2 Severity

The provider's severity is final. The delivery team records it unchanged and cannot lower it. If the team disputes a finding, the provider re-rates or withdraws it in writing, in a retest round. When a CVSS v3.1 or v4.0 score is given, it maps as: Critical 9.0–10.0; High 7.0–8.9; Medium 4.0–6.9; Low 0.1–3.9; Informational 0.

### 5.3 Retest

For each round of fixes, the provider retests every finding marked `Remediated` against a named build (commit and image digest, deployed to UAT). It issues a retest report giving each finding `Closed`, `Not closed` or `Withdrawn`. Retests continue until the gate passes (§7). The contract must provide for at least two rounds.

## 6. Handling findings

| Severity | Treatment | Can AHDA accept it? |
| --- | --- | --- |
| Critical | Fixed at once; G-4 is blocked until the provider's retest closes it | **No.** The acceptance criterion asks for zero unresolved Critical findings |
| High | Fixed and retested; or AHDA Cybersecurity signs a risk acceptance that covers production (`risk-acceptances.md`) | Yes, signed by AHDA. An acceptance that, like RA-01 to RA-03, "does not extend to PROD" does not satisfy this gate |
| Medium, Low, Informational | Tracked with an owner and a due date the Security Lead sets, by severity (CS-023) | Not gated here |

Each finding is also read against the threat models. A finding that shows a threat the models missed, or a control that does not hold, adds or changes a threat row and its `SR` finding in the same PR (CTL-43). The tracker's `Log` column names that SR.

## 7. Records and the gate

| Record | Holds |
| --- | --- |
| [`penetration-test-report.md`](penetration-test-report.md) | One block per report the provider issues (`PTR-n`), and the report's finding list (`PT-nn`) as issued |
| [`penetration-test-tracker.md`](penetration-test-tracker.md) | The remediation tracker: one row per `PT-nn` with owner, due date, disposition, fix and retest result. Then the retest evidence: one block per round (`RT-n`) |
| [`risk-acceptances.md`](risk-acceptances.md) | The RA records that accept a High `PT-nn`, alongside TASK-081's |

```
python3 docs/security/penetration-test-check.py                       # consistency: runs in repo-checks on every PR
python3 docs/security/penetration-test-check.py --gate                # "External Penetration Test Passed"
python3 docs/security/penetration-test-check.py --gate --verify-prs   # also asks GitHub that each cited fix PR is merged (needs gh)
```

Without `--gate`, the check makes sure the three records agree. With `--gate`, it also needs:

- a `Final` report covering areas A–D;
- no Critical without a `Closed` retest;
- no High without a `Closed` retest or a signed acceptance;
- no `Remediated` finding without a `Closed` retest.

The Release Checklist row "External Penetration Test Passed" is marked green only on a passing `--gate --verify-prs` run, recorded against the release candidate's commit. That run is also a precondition of G-4.

The gate passes on the test of one build. A fix merged after the final retest is retested before go-live if it touches areas A–F.

## 8. Open items

| # | Item | Owner |
| --- | --- | --- |
| F-1 | **Ask the provider question now.** PRE-1 is not in the workbook's Open Questions sheet. Propose a new OQ: "Who performs the pre-launch penetration test, with what accreditation, and what is its lead time?" (Guide §15) | PMO Engagement Lead |
| F-2 | **The hosting confirmation blocks this task as well as the infrastructure tasks.** No UAT means no test (PRE-2). The written confirmation, the tenancy (UGV-07) and the DNS zone are on this task's critical path to go-live, and the plan should show it | Delivery Lead |
| F-3 | **The next test date** follows the criticality decision (PRE-6): six months after go-live if critical, twelve if not. It is row C-5 of the operations calendar handed over in TASK-097 (`docs/handover/README.md` §6) | AHDA Cybersecurity; TASK-097 |
| F-4 | **No Security Lead review exists in this repository** (`findings-log.md` P-1). It applies here too: the Security Lead owns the Release Checklist row this record decides | Maintainer; Security Lead |

## 9. Change log

| Date | Change |
| --- | --- |
| 2026-10-16 | First record (TASK-082): engagement defined; not commissioned |

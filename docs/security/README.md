# Threat Models and Security Review

| Field | Value |
| --- | --- |
| Task | TASK-081 — Conduct Threat Modeling & Security Review for Sensitive Features (P14 - Security Hardening & Compliance) |
| Depends on | TASK-037 (WF-12 backend), TASK-066 (WF-13 backend), TASK-028 (AD/LDAP and SSO), TASK-030 (RBAC and data-scope engine) |
| Record date | 2026-10-16 |
| Reviewed code | `dev` at `6f19809` (PR #90 merged) |
| Status | **REVIEW COMPLETE; GATE NOT GREEN.** Six threat models and the findings log are written. AHDA has not yet signed any risk acceptance, and some High findings are still open (`findings-log.md` §2) |
| Branch | `sec/task-081-threat-modeling-security-review` |
| Directory | `docs/security` |
| Deliverables | **Threat model documents per sensitive feature** (§2) and the **security review findings log** (`findings-log.md`), with the risk-acceptance register AHDA signs (`risk-acceptances.md`) and the gate check (`findings-log-check.py`) |
| Environment variables / secrets | None (as the workbook row says) |
| Gate decision applied | None in the workbook row |
| Control | CTL-43 (`cybersecurity-control-matrix.md`), with CTL-16 for the SMS receipt callback; AHDA CS-003 (risk assessment and treatment) and CS-004 (security impact assessment: data flows, trust boundaries, threats, control decisions), per the Cybersecurity Guide's Appendix A |
| Sources read | The TASK-081 row as supplied on 2026-10-16, checked against the workbook (`AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`): description, acceptance criteria, deliverables, directory, validation check, and the Release Checklist row "Security Review & Threat Modeling Signed Off". The Participation Amendment (ADR-004: the SMS delivery-receipt callback). From `Project Files.zip`: Blueprint v2.0 §22, the PM Platform Development & Cybersecurity Guide (Appendix A), FG-03 §11, §22 and §24 (REV-IAM-001 to -009), WF-12 §8.3 and §23, WF-13 §8 and §23, WF-15 §22 and its API and error catalogue. The feature records under `docs/architecture` named in each model, and the code on `dev` |

## 1. What this is

One threat model per sensitive feature, and one findings log across them. Each model lists the feature's threats, rates each one, and names the control that mitigates it in the code on `dev` (file and line), with the test that proves it. A threat with no shipped control gets a finding in the log, and the finding gets an owner and either a fix or an AHDA risk-acceptance decision.

The UAT gate reads only the findings log. "Security Review & Threat Modeling Signed Off" is green when `findings-log-check.py --gate` passes. That means every High or Critical finding is closed by a merged PR or by a risk acceptance AHDA has signed (§5).

## 2. The models

| Feature | Model | Feature records | Built by |
| --- | --- | --- | --- |
| Authentication and SSO (password, SSO, MFA, step-up, sessions) | [`threat-model-authentication.md`](threat-model-authentication.md) | `sso-directory-integration.md`, `mfa-privileged-access.md`, `auth-access-audit-logging.md`, `endpoint-hardening.md`, `csrf-protection.md` | TASK-028, 029, 033, 078, 079 |
| RBAC and data-scope authorization engine | [`threat-model-authorization.md`](threat-model-authorization.md) | `authorization-engine.md`, `identity-access-administration.md` | TASK-030, 031 |
| Document upload and evidence (WF-12) | [`threat-model-documents.md`](threat-model-documents.md) | `document-management.md`, `document-library-ui.md` | TASK-037, 038 |
| External entity participation (WF-13) | [`threat-model-external-participation.md`](threat-model-external-participation.md) | `external-participation.md`, `external-participation-ui.md` | TASK-066, 067 |
| Nafath identity verification | [`threat-model-nafath.md`](threat-model-nafath.md) | `nafath-identity-verification.md`, `nafath-data-minimisation.md` | TASK-068 |
| SMS delivery-receipt callback (ADR-004) | [`threat-model-sms-delivery-receipt.md`](threat-model-sms-delivery-receipt.md) | `notification-runtime.md`, `integration-monitoring.md` | TASK-103 (not built) |

Nafath is in scope: TASK-068 shipped it, switched off by `Identity:Nafath:Enabled`. Because it is shipped, it can be switched on in any environment without a code change. The SMS receipt callback is not built. Its model is a design review, so its controls are requirements on TASK-103, and its findings stay open until TASK-103 merges with them.

## 3. Method

**STRIDE per element.** Each model first draws the feature's data flow: actors, processes, stores, and the trust boundaries a request crosses. Then it takes each element that crosses a boundary through the six categories: **S**poofing, **T**ampering, **R**epudiation, **I**nformation disclosure, **D**enial of service, **E**levation of privilege. A threat is written about this code, not as a textbook category ("an entity user changes the request id in `GET /external-update-requests/{id}`", not "IDOR").

**Evidence, not assertion.** A control counts as shipped only when it is on `dev`. Each one is cited by file and line, read in the code at the reviewed commit, and named with the test that fails without it. A test counts only if a CI step runs it. A record's claim that a control exists was checked against the code, and claims found stale are listed in each model's last section.

**Rating.** CS-003 asks for likelihood and impact; the Guide fixes no scale, so this one is used:

| Likelihood (L) | Meaning |
| --- | --- |
| 1 | Needs a privileged insider, or several independent failures |
| 2 | Needs an authenticated user, a particular network position, or a non-trivial precondition |
| 3 | Any anonymous or external party can do it, and it is easy |

| Impact (I) | Meaning |
| --- | --- |
| 1 | Nuisance: no loss of confidentiality or integrity of business data |
| 2 | One user's or one record's data or action, recoverable |
| 3 | Authentication bypass, privilege escalation, data exposed across entities or platform-wide, malware delivered to users, or audit or evidence tampering |

**Inherent** = L × I before any control: 1–2 Low, 3–4 Medium, 6 High, 9 Critical. **Residual** is the same scale with the shipped controls in place. "High-rated threat" in the acceptance criterion means **inherent** High or Critical: those are the threats whose mitigation the gate must see.

An authenticated external entity user is a less trusted actor than AHDA staff. An attack any entity user can make with an ordinary account rates at least L = 2, and L = 3 if it is trivial, such as changing an id.

**Status** of a threat:

| Status | Meaning |
| --- | --- |
| MITIGATED | The control is on `dev` and a CI-run test proves it; the residual rating is Medium or lower |
| PARTIAL | A control is shipped but leaves a stated gap; the gap is a finding |
| GAP | No control; a finding |
| DEPENDS | The control rests on a decision or a system outside the code (an MFA provider, AHDA's IdP configuration, a scanner); a finding names the owner |
| NOT BUILT | The surface does not exist yet; the control is a requirement on the task that builds it, and a finding holds it open |

## 4. Findings log and risk acceptance

[`findings-log.md`](findings-log.md) has one row per finding, `SR-nn`. A finding is raised for:

- every threat rated High or Critical inherent, so the gate can see how each one is closed;
- every threat with status PARTIAL, GAP, DEPENDS or NOT BUILT, whatever its rating;
- every defect the review found in the code.

Each row names its threat ids, its ratings, its owner, and one disposition:

| Disposition | Closed when |
| --- | --- |
| `Mitigated` | The row lists the merged PR(s) that shipped the control (`PR #n`), and the residual rating is Medium or lower |
| `Risk accepted` | The row names an `RA-nn` in [`risk-acceptances.md`](risk-acceptances.md), and that record is signed by AHDA |
| `Open` | Neither, yet. The row says what closes it |

The delivery team cannot accept a risk on AHDA's behalf. `risk-acceptances.md` holds the acceptance requests this review prepared, each with its risk statement, compensating controls, conditions and review date. Each stays `Pending AHDA signature` until AHDA Cybersecurity signs it. The signer's name, role, date and a reference to the signed copy are then entered in the record, in a PR the Security Lead reviews.

## 5. The UAT gate

The workbook's validation check: "Confirm every High-rated threat in the findings log is linked to either a merged mitigating PR or a signed risk-acceptance record before the UAT gate (see Release Checklist sheet) is marked green."

```
python3 docs/security/findings-log-check.py           # consistency: runs in repo-checks on every PR
python3 docs/security/findings-log-check.py --gate    # the UAT gate: exit 0 only when every High/Critical finding is closed
python3 docs/security/findings-log-check.py --gate --verify-prs   # also asks GitHub that each cited PR is merged (needs gh)
```

Without `--gate`, the check makes sure the models and the log agree. Every model's threat appears with the same ratings. Every inherent High or Critical threat, and every non-MITIGATED threat, has a finding. Every rating equals L × I. Every `PR #n` and `RA-nn` a row cites exists. It runs in `repo-checks`, so a model edited without its log, or the reverse, fails the PR.

With `--gate`, it also lists each High or Critical finding that is not closed and exits 1 if there is any. With `--verify-prs`, each cited PR must be MERGED on GitHub. The Release Checklist's row is marked green only on a passing `--gate --verify-prs` run against the release candidate's commit.

## 6. The penetration test (TASK-082)

The independent penetration test before production has its own records here, and its own gate: the Release Checklist's "External Penetration Test Passed".

| Record | Holds |
| --- | --- |
| [`penetration-test.md`](penetration-test.md) | The engagement: its prerequisites, statement of work, rules of engagement, what the provider delivers, and how findings are handled |
| [`penetration-test-report.md`](penetration-test-report.md) | The register of each report the provider issues, and its finding list (`PT-nn`). The report itself stays out of this public repository |
| [`penetration-test-tracker.md`](penetration-test-tracker.md) | The remediation tracker and the retest evidence |

`penetration-test-check.py` keeps the three in agreement on every PR (repo-checks). With `--gate`, it decides the Release Checklist row. A High `PT-nn` is risk-accepted in `risk-acceptances.md`, like an `SR-nn`. A penetration-test finding that shows a threat the models missed also changes its model and this log (`penetration-test.md` §6).

## 7. Keeping the models current

A change to authentication, SSO, RBAC, data scope, document upload, WF-13, Nafath or the SMS callback updates its threat model in the same PR. A new threat, or a control that moves, is a row in the model and, if it qualifies (§4), a row in the log. The PR template's CTL-43 item says so. CODEOWNERS sends `docs/security/` to the Security Lead.

## 8. Change log

| Date | Change |
| --- | --- |
| 2026-10-16 | First review (this record) |
| 2026-10-16 | §6: the penetration test records (TASK-082) |

# Operational Handover Package

| Field | Value |
| --- | --- |
| Task | TASK-097 — Compile Operational Handover Package (P17 - Documentation) |
| Depends on | TASK-093 — SLAs and on-call ([`on-call-runbook.md`](../operations/on-call-runbook.md)); TASK-023 — backup, restore and DR ([`backup-restore-dr-runbook.md`](../operations/backup-restore-dr-runbook.md)); TASK-095 — onboarding and architecture guide ([`developer-onboarding-guide.md`](../onboarding/developer-onboarding-guide.md)); TASK-094 — API documentation ([`docs/api/README.md`](../api/README.md)) |
| Record date | 2026-10-24 |
| Status | **COMPILED — NOT YET REVIEWED OR ACCEPTED BY AHDA.** The index (§3, §4) cross-references every document of the workbook's five Documentation-category tasks and the operational records around them, each with the status the record itself states; `handover-package-check.py` keeps the links and those statuses current on every PR. The training and knowledge-transfer record and the acknowledgment form are written. No knowledge-transfer session has been held, AHDA has not named its receiving operations team (F-1), and the acknowledgment is unsigned, so the acceptance criterion is not met (§9) |
| Branch | `docs/task-097-operational-handover-package` |
| Directory | `docs/handover` |
| Deliverables | **Operational handover package:** this index; [`knowledge-transfer-record.md`](knowledge-transfer-record.md) (Client Scope §12, by area, with the session log); [`knowledge-transfer-acknowledgment.md`](knowledge-transfer-acknowledgment.md) (the form AHDA signs, and its receipt record); [`handover-package-check.py`](handover-package-check.py) (the guard, in `repo-checks`). **Signed AHDA acknowledgment:** not yet — §9 |
| Control | CTL-53 (operational handover); CTL-55 (dependency currency during operations, §6); CTL-51 (vendor remote access, §7 O-9). Release Checklist row "Operational Handover Package Accepted" |
| Environment variables / secrets | None, as the workbook row says |
| Gate decision applied | None in the workbook row |
| Sources read | The TASK-097 row of `AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx` and of the Google Sheet "Implementation work" (Category column of all 112 rows), Release Checklist rows "Operational Runbooks & On-Call Ready" and "Operational Handover Package Accepted", Open Question OQ-009; *Client Original Project Scope* §2(e), §6, §11, §12, §13; *PM Platform Development and Cybersecurity Guide* §13 (go-live rows "Operations" and "Users"), §14, §15, Appendix D; *AHDA Master Plan* activities 3.9, 3.10 and 4.9; every record in §3 and §4, read 2026-10-24 |

## 1. What this package is

The contract runs 24 months: implementation and launch, then about 22 months of operations, maintenance and technical
support (*Client Original Project Scope* §13). The Release Checklist requires this package to be accepted "before the
engagement transitions from implementation to the ~22-month operations/maintenance/support period".

It is an index, not a copy. Every document it lists stays where its task put it and is kept current there. When this
index and a record disagree, the record wins and the index is wrong; HP-3 of the check catches a changed status.

| Source requirement | Where the package meets it |
| --- | --- |
| Client Scope §11 — operation, maintenance and technical support: corrective and preventive maintenance, faults, patches, health checks, periodic performance reports, monthly visits | §4.2 to §4.5 (run, recover, change, secure); §6 (the recurring obligations) |
| Client Scope §12 — training and knowledge transfer in five areas, hands-on during implementation and continuing through the contract | [`knowledge-transfer-record.md`](knowledge-transfer-record.md) |
| Client Scope §6 — "the technical architecture, the main use cases and the platform's governance and operating model must be documented" | Architecture: §4.1. Use cases: the WF/FG functional specifications, registered in [`controlled-source-baseline.md`](../baseline/controlled-source-baseline.md) (H-90). Operating model: [`on-call-runbook.md`](../operations/on-call-runbook.md) (H-01) and this package |
| Client Scope §2(e) — user guide and technical documentation; knowledge base and FAQ | §3: TASK-096 (user guide, FAQ, knowledge base), TASK-094 and TASK-095 (technical) |
| Guide §13 go-live, "Operations": runbook, support contacts, incident path, SLA, health checks, monitoring dashboard | H-01, H-02, H-30, H-31. Support contacts and SLA: open (§7 O-1, O-2) |
| Guide §13 go-live, "Users": UAT sign-off, training, admin handover, user guides | H-84 (UAT sign-off, not signed); knowledge-transfer record; §5 (admin task map); H-08 |
| Guide §14 — keep ADRs, API documentation, runbooks and user/admin guides current | §6 row C-12; HP-3 |
| Master Plan 3.9 / 4.9 — role-based guides, admin runbook, support guide, training materials; documentation kept current through operations | §3, §5; [`knowledge-transfer-record.md`](knowledge-transfer-record.md); §8 F-2, F-4 |
| CTL-53 — runbooks (backup/DR, on-call, secret rotation), security procedures, architecture guide and API docs, accepted with a signed knowledge-transfer acknowledgment | H-01, H-40, H-36; §4.5; H-07, H-20; H-03; §9 |
| CTL-55 — weekly dependency scanning continues, owner named; the supported-runtime commitment carried | §6 rows C-1, C-2, C-10 |

### 1.1 Who it is for

| Reader | Uses |
| --- | --- |
| **AHDA's receiving operations team** — not yet named (F-1); by the on-call runbook's roles, AHDA IT, the AHDA IT service desk and AHDA Cybersecurity | The whole index; reviews and accepts it (§9) |
| AHDA IT service desk | H-02 (classifying a report), H-08 to H-11 (what users are told), §6 |
| AHDA platform administrators (R01) | §5, H-70 to H-76 |
| Delivery-team on-call engineers | H-01, H-02, H-40, H-36, H-30, H-31 |
| A developer joining for maintenance | H-07, then H-20 to H-27 |

## 2. How to read the index

Each row names one record, the task that produced it, what the receiving team uses it for, and its status **as the
record states it**, quoted in bold, followed by what that means for operations. "BUILT — NOT APPLIED" and similar
mean the record is complete but nothing has been provisioned: no DEV, SIT, UAT or PROD environment exists yet, because
ADR-001's written hosting confirmation (R-1), region (R-2) and tenancy (R-3) and the DNS zone are outstanding. That one
cause stands behind most of §7.

A record without a status line in its header shows "—".

## 3. Index — the Documentation-category tasks

The workbook's Documentation category is TASK-093 to TASK-097, in the local workbook and the Google Sheet alike (read
2026-10-24). Every document those tasks produced is below.

| ID | Record | Task | The receiving team uses it to | Status as the record states it |
| --- | --- | --- | --- | --- |
| H-01 | [`on-call-runbook.md`](../operations/on-call-runbook.md) | TASK-093 | Run an unplanned incident: 12 roles, who is paged at each tier, the six-step procedure, nine playbooks (PB-1 to PB-9), notices, post-incident review | **WRITTEN — NOT YET STAFFED OR EXERCISED LIVE.** Walked at the desk twice (§8 there); roster, on-call tool and ticketing system do not exist (O-2 to O-4); gate RC-5 to RC-8 open |
| H-02 | [`incident-severity-classification.md`](../operations/incident-severity-classification.md) | TASK-093 | Give an incident one tier, P1 to P4, by observable rules; 24 sample incidents with answers | **WRITTEN — WORKING DEFINITIONS, NOT YET THE CONTRACT'S.** Every response, update and resolution time is TBC on OQ-009 (O-1) |
| H-03 | [`docs/api/README.md`](../api/README.md) | TASK-094 | Find the API's published OpenAPI document (CI artifact `openapi-v1` from every build of `main`) and its Swagger UI (`/api-docs`, DEV and SIT only, read-only); re-run the published-spec audit | **BUILT; DEV AND SIT NOT YET PROVISIONED.** Audit 8/8 against the release image; the live DEV audit is owed (its F-7) |
| H-04 | [`openapi.v1.json`](../api/openapi.v1.json) | TASK-094 | Read the API's contract: 417 operations in 21 modules, equal byte for byte to what the API generates | — |
| H-05 | [`spec-audit.py`](../api/spec-audit.py), [`spec-audit-sample.json`](../api/spec-audit-sample.json) | TASK-094 | Check a deployed environment against the published document (§5.1 there) | — |
| H-06 | [`api-documentation-check.py`](../api/api-documentation-check.py) | TASK-094 | The guard that keeps the publication and the DEV/SIT-only UI in place | — |
| H-07 | [`developer-onboarding-guide.md`](../onboarding/developer-onboarding-guide.md) | TASK-095 | Take a maintenance developer from no checkout to a running stack and a merged first PR; the module map; where the controlled sources are | **DRY-RUN BY A STAND-IN; A HUMAN DRY RUN IS STILL OWED.** Two runs by an AI agent new to the codebase, 16 steps fixed; no person has run it (§9.3 there) |
| H-08 | [`user-guide-faq.md`](../architecture/user-guide-faq.md) | TASK-096 | Know what the in-app Help and Knowledge menu tells users, and the rule that a changed workflow changes its article in the same PR (F-5 there) | **BUILT; BILINGUAL REVIEW PENDING.** The bilingual business reviewer's sign-off is owed (§6.2 there) |
| H-09 | [`guide.json`](../../src/frontend/src/content/help/en/guide.json), [`faq.json`](../../src/frontend/src/content/help/en/faq.json), [`knowledge-base.json`](../../src/frontend/src/content/help/en/knowledge-base.json) | TASK-096 | The English content: 9 guide articles, 21 FAQ answers, 19 knowledge-base terms | — |
| H-10 | [`guide.json`](../../src/frontend/src/content/help/ar/guide.json), [`faq.json`](../../src/frontend/src/content/help/ar/faq.json), [`knowledge-base.json`](../../src/frontend/src/content/help/ar/knowledge-base.json) | TASK-096 | The Arabic content, written in its own files (D-5 there) | — |
| H-11 | [`help.ts`](../../src/frontend/src/content/help/help.ts) | TASK-096 | Which article each role lands on (Blueprint §20.2), and the article metadata | — |
| H-12 | [`README.md`](README.md) | TASK-097 | This index | **COMPILED — NOT YET REVIEWED OR ACCEPTED BY AHDA.** |
| H-13 | [`knowledge-transfer-record.md`](knowledge-transfer-record.md) | TASK-097 | Plan and record training and knowledge transfer in Client Scope §12's five areas | **PLANNED — NO SESSION HELD.** |
| H-14 | [`knowledge-transfer-acknowledgment.md`](knowledge-transfer-acknowledgment.md) | TASK-097 | The acknowledgment AHDA's receiving team signs, and the receipt record of the signed original | **UNSIGNED.** |
| H-15 | [`handover-package-check.py`](handover-package-check.py) | TASK-097 | Keep this index whole and current (HP-1 to HP-5); `--gate` for the Release Checklist row | — |

## 4. Index — the operational records around them

CTL-53 lists runbooks, security procedures, the architecture and the API documentation; Client Scope §11 adds
maintenance, patching and health checks. These records hold them.

### 4.1 Architecture and design

| ID | Record | Task | The receiving team uses it to | Status as the record states it |
| --- | --- | --- | --- | --- |
| H-20 | [`solution-architecture.md`](../architecture/solution-architecture.md) | TASK-007 | Understand the three tiers and the 21 modules (ADR-003) | **PROPOSED — PENDING AHDA APPROVAL.** ADR-003 is not approved (O-16) |
| H-21 | [`ADR-register.md`](../architecture/adrs/ADR-register.md) | — (reference copy of the workbook's Architecture Decisions sheet, added with TASK-025) | See the decisions of the Architecture Decisions sheet, ADR-001 to ADR-013, and their approval (ADR-014 to ADR-019, cited by task amendments, are not in the register) | **Approved (AHDA gate, 19 Sep 2026) — written confirmation and region naming outstanding** — the register's ADR-001 row; ADR-002 and ADR-003 are Proposed |
| H-22 | [`ADR-001-hosting-and-data-localisation.md`](../architecture/adrs/ADR-001-hosting-and-data-localisation.md) | TASK-005 | Hosting on GCP in-Kingdom (tenancy open, R-3); RPO/RTO; vendor remote access (C-9) | **APPROVED — CONDITIONAL.** R-1 to R-3 open (O-5) |
| H-23 | [`ADR-002-technology-stack.md`](../architecture/adrs/ADR-002-technology-stack.md) | TASK-006 | The stack and its support dates: .NET 10 LTS to November 2028 (S-2, §6 C-10) | **PROPOSED — PENDING AHDA APPROVAL.** (O-16) |
| H-24 | [`erd.md`](../architecture/erd.md) | TASK-008 | The data model | **PROPOSED — PENDING APPENDIX D/C RECONCILIATION AND ADR-003 APPROVAL.** |
| H-25 | [`api-conventions.md`](../architecture/api-conventions.md) | TASK-009 | The rules every API follows (errors, paging, idempotency, versioning) | **RATIFIED (delivery team) — PROVISIONAL on ADR-002 and ADR-003 approval.** |
| H-26 | [`event-conventions.md`](../architecture/event-conventions.md) | TASK-009 | The event envelope and the outbox | **RATIFIED (delivery team) — PROVISIONAL on ADR-002 and ADR-003 approval** |
| H-27 | [`local-development-environment.md`](../architecture/local-development-environment.md) | TASK-014 | Run the whole platform locally to reproduce a fault | **BUILT — PROVISIONAL ON THE TASK-014 GATE.** |

### 4.2 Run and support

| ID | Record | Task | The receiving team uses it to | Status as the record states it |
| --- | --- | --- | --- | --- |
| H-30 | [`health-checks-uptime-monitoring.md`](../architecture/health-checks-uptime-monitoring.md) | TASK-091 | Read `/health/live`, `/health/ready` and `/health`; the uptime check and its on-call alert; the database-outage drill | **BUILT; VERIFIED LOCALLY END TO END, WITH A STAND-IN FOR CLOUD MONITORING.** Not applied to an environment |
| H-31 | [`operational-dashboards.md`](../operations/operational-dashboards.md) | TASK-092 | The FG-05 operational dashboard and alerts; what to do per alert type (§7.2 there, playbook PB-6) | **BUILT AND VERIFIED LOCALLY.** Every threshold is AHDA's and open; not applied (its F-1) |
| H-32 | [`alert-thresholds.json`](../operations/alert-thresholds.json) | TASK-092 | The alert thresholds in force, in one file | — |
| H-33 | [`apm-error-tracking.md`](../architecture/apm-error-tracking.md) | TASK-090 | Find an exception or trace by correlation id | **BUILT AND VERIFIED LOCALLY** — no APM chosen for the environments (O-8) |
| H-34 | [`integration-monitoring.md`](../architecture/integration-monitoring.md) | TASK-075 | How each integration's health, retries and dead letters are recorded | **BUILT AND VERIFIED LOCALLY** |
| H-35 | [`notification-runtime.md`](../architecture/notification-runtime.md) | TASK-039 | How notifications are routed, retried, dead-lettered and redriven (D-13) | **BUILT AND VERIFIED LOCALLY** — no routing or template published, no SMS provider (O-13) |
| H-36 | [`secret-rotation-runbook.md`](../../infra/secrets/secret-rotation-runbook.md) | TASK-019 | Rotate a secret, routinely or in an emergency (§6 there), and roll it back | **WRITTEN — REHEARSED AGAINST THE APPLICATION.** Not yet against Secret Manager |
| H-37 | [`secret-management.md`](../architecture/secret-management.md) | TASK-019 | Which secrets exist, who owns each, where they live | **BUILT — NOT APPLIED.** |

### 4.3 Backup, recovery and data

| ID | Record | Task | The receiving team uses it to | Status as the record states it |
| --- | --- | --- | --- | --- |
| H-40 | [`backup-restore-dr-runbook.md`](../operations/backup-restore-dr-runbook.md) | TASK-023 | Restore the database, documents or audit store: scenarios S-1 to S-7, RPO 1 h, RTO 4–6 h, the drill | **WRITTEN — DRILLED LOCALLY, NOT IN A NON-PROD ENVIRONMENT.** The SIT drill E-4 is owed (O-7) |
| H-41 | [`restore-drill-evidence-log.md`](../operations/restore-drill-evidence-log.md) | TASK-023 | Record every drill; read the local evidence | **Validation cell executed locally (E-3): PASS.** Acceptance criterion not met until E-4 |
| H-42 | [`database-provisioning.md`](../architecture/database-provisioning.md) | TASK-020 | The Cloud SQL instance, its backups and encryption | **BUILT — NOT APPLIED.** |
| H-43 | [`database-migrations.md`](../architecture/database-migrations.md) | TASK-024 | Apply and roll back schema migrations (`migrate`) | **BUILT — VERIFIED LOCALLY, NOT APPLIED TO AN ENVIRONMENT.** |
| H-44 | [`rollback-validation.md`](../../db/migrations/rollback-validation.md) | TASK-089 | The rollback path, tested at representative volume | **Criteria 1 and 2 MET on the local stand-in; criterion 3 BLOCKED (OQ-008).** |
| H-45 | [`legacy-data-migration.md`](../../db/migrations/legacy-data-migration.md) | TASK-089 | The legacy-data plan | **BLOCKED on OQ-008.** |

### 4.4 Infrastructure, release and change

| ID | Record | Task | The receiving team uses it to | Status as the record states it |
| --- | --- | --- | --- | --- |
| H-50 | [`infrastructure-as-code.md`](../architecture/infrastructure-as-code.md) | TASK-017 | Change any environment through Terraform, never by hand | **BUILT — NOT APPLIED.** |
| H-51 | [`environment-separation.md`](../architecture/environment-separation.md) | TASK-016 | The four environments and what keeps them apart | **BUILT — NOT APPLIED.** |
| H-52 | [`environment-templates.md`](../architecture/environment-templates.md) | TASK-013 | Every environment variable, per environment | **BUILT.** |
| H-53 | [`environment-and-secrets.csv`](../architecture/environment-and-secrets.csv) | TASK-013 | The Environment and Secrets sheet as the repository holds it: owner and storage of each variable | — |
| H-54 | [`network-security.md`](../architecture/network-security.md) | TASK-021 | The WAF, load balancer, TLS and segmentation; the WAF false-positive procedure (§3.4 there) | **BUILT — NOT APPLIED.** |
| H-55 | [`cicd-pipeline.md`](../architecture/cicd-pipeline.md) | TASK-018 | Promote a release DEV → SIT → UAT → PROD through the approval gates; roll back by re-promoting the previous release (D-4 there) | **BUILT — NOT APPLIED.** |
| H-56 | [`ci-quality-gates.md`](../architecture/ci-quality-gates.md) | TASK-015 | What every change must pass | **BUILT — PROVISIONAL ON APPLYING THE RULESET.** |
| H-57 | [`branching-strategy.md`](../architecture/branching-strategy.md) | TASK-012 | Branches, PRs, protected branches and code owners | **PROTECTION APPLIED, CI-ONLY (2026-10-18).** The CODEOWNERS teams do not exist yet (S-2 there) |
| H-58 | [`artifact-build-and-scanning.md`](../architecture/artifact-build-and-scanning.md) | TASK-022 | How the release image is built, scanned and signed (keyless), and its SBOM | **BUILT — NOT APPLIED.** |

### 4.5 Security

| ID | Record | Task | The receiving team uses it to | Status as the record states it |
| --- | --- | --- | --- | --- |
| H-60 | [`cybersecurity-control-matrix.md`](../architecture/cybersecurity-control-matrix.md) | TASK-010 | Every control CTL-01 to CTL-55, who implements it, and the gaps G-1 to G-7 | **NOT RATIFIED — PROVISIONAL.** |
| H-61 | [`docs/security/README.md`](../security/README.md) | TASK-081 | The threat models and how a finding is rated and closed | **REVIEW COMPLETE; GATE NOT GREEN.** |
| H-62 | [`findings-log.md`](../security/findings-log.md) | TASK-081 | Every security finding and its state | — `findings-log-check.py --gate`, 2026-10-24: 10 High/Critical without a merged mitigation or a signed acceptance (O-14) |
| H-63 | [`risk-acceptances.md`](../security/risk-acceptances.md) | TASK-081, TASK-082 | The risks AHDA is asked to accept in writing | **Pending AHDA signature** |
| H-64 | [`penetration-test.md`](../security/penetration-test.md) | TASK-082 | The pre-launch penetration test and when the next is due (PRE-6, §6 C-5) | **NOT COMMISSIONED.** (O-14) |
| H-65 | [`secret-and-dependency-scanning.md`](../architecture/secret-and-dependency-scanning.md) | TASK-080 | The weekly secret and dependency scans during operations (CTL-55) | **BUILT AND VERIFIED** |
| H-66 | [`scan-owner.yaml`](../../.github/security/scan-owner.yaml) | TASK-080 | Who the weekly scans report to: the Security Lead's account, held by the repository owner until AHDA names one | — |
| H-67 | [`mfa-privileged-access.md`](../architecture/mfa-privileged-access.md) | TASK-029 | MFA and step-up for privileged actions | **BUILT AND VERIFIED LOCALLY** — no MFA provider selected (PTBC-027) |
| H-68 | [`auth-access-audit-logging.md`](../architecture/auth-access-audit-logging.md) | TASK-033 | What sign-in and access events are audited, and what reaches the SIEM | **BUILT AND VERIFIED LOCALLY** |

### 4.6 Administration

| ID | Record | Task | The receiving team uses it to | Status as the record states it |
| --- | --- | --- | --- | --- |
| H-70 | [`identity-access-administration.md`](../architecture/identity-access-administration.md) | TASK-031 | Users, roles, permissions and assignments: the rules behind ADM-002 to ADM-013 | **BUILT AND VERIFIED LOCALLY** |
| H-71 | [`identity-access-administration-ui.md`](../architecture/identity-access-administration-ui.md) | TASK-032 | The administration screens for them | **BUILT AND VERIFIED LOCALLY** |
| H-72 | [`master-data-configuration.md`](../architecture/master-data-configuration.md) | TASK-034 | Master data and governed configuration versions (author, validate, publish) | **BUILT AND VERIFIED LOCALLY** — API only, no screen (its F-5; O-17) |
| H-73 | [`integration-admin-ui.md`](../architecture/integration-admin-ui.md) | TASK-076 | Integration configuration, monitoring and logs (ADM-040 to ADM-048) | **BUILT AND VERIFIED LOCALLY** |
| H-74 | [`audit-activity-ui.md`](../architecture/audit-activity-ui.md) | TASK-074 | The audit trail and system logs (ADM-050 to ADM-053) | **BUILT AND VERIFIED LOCALLY** |
| H-75 | [`data-import.md`](../architecture/data-import.md) | TASK-102 | Load legacy registers from the XLSX templates (Administration › Data migration) | **Built and verified locally** |
| H-76 | [`sso-directory-integration.md`](../architecture/sso-directory-integration.md) | TASK-028 | Sign-in through AHDA's directory, and what AHDA IT owns in it | **BUILT AND VERIFIED LOCALLY** |

### 4.7 Test and acceptance evidence

| ID | Record | Task | The receiving team uses it to | Status as the record states it |
| --- | --- | --- | --- | --- |
| H-80 | [`test-strategy.md`](../testing/test-strategy.md) | TASK-084 | What is tested where, and the coverage policy | **DRAFT — AWAITING APPROVAL BY THE PMO ENGAGEMENT LEAD** |
| H-81 | [`load-test.md`](../testing/load-test.md) | TASK-086 | The capacity baseline and the screens that breach the latency budget | **RUN AND REPORTED on the e2e stack standing in for SIT.** |
| H-82 | [`accessibility-audit.md`](../testing/accessibility-audit.md) | TASK-087 | The WCAG 2.1 AA results and open findings | **RUN, REMEDIATED AND RE-RUN on the e2e stack.** |
| H-83 | [`uat-execution.md`](../testing/uat/uat-execution.md) | TASK-088 | The UAT cases, data set and defect tracker | **PREPARED, NOT EXECUTED.** Nine Critical readiness defects (O-15) |
| H-84 | [`uat-sign-off.md`](../testing/uat/uat-sign-off.md) | TASK-088 | AHDA's UAT sign-off | **NOT SIGNED.** |
| H-85 | [`e2e-suite.md`](../testing/e2e-suite.md) | TASK-085 | The cross-domain journeys run every night | **BUILT.** |

### 4.8 Governance and open values

| ID | Record | Task | The receiving team uses it to | Status as the record states it |
| --- | --- | --- | --- | --- |
| H-90 | [`controlled-source-baseline.md`](../baseline/controlled-source-baseline.md) | TASK-001 | Find the controlled documents (Blueprint, WF/FG specifications, workbook) and their authority order | **PROVISIONAL** |
| H-91 | [`ptbc-tbc-tracker.md`](../governance/ptbc-tbc-tracker.md) | TASK-004 | Every value AHDA has still to set, and its gate | **PROVISIONAL — 18 of 48 themes populated** |
| H-92 | [`unassigned-gated-values.csv`](../governance/unassigned-gated-values.csv) | TASK-004 | The gated values with no PTBC ID, such as UGV-06 (the SLA table) | — |

## 5. Not yet in the package

These belong in it and do not exist yet. Each is added to the index when its task delivers it. The status is the
Google Sheet's Status column, read 2026-10-24; the local xlsx has no Status column.

| Document | Task | Status in the workbook | Why the receiving team needs it |
| --- | --- | --- | --- |
| `docs/operations/release-and-rollback-strategy.md` | TASK-098 | Not Started | Rollback triggers and the exact rollback procedure for a release; until then, PB-8 of the on-call runbook and D-4 of `cicd-pipeline.md` are the procedure |
| Soak test report | TASK-099 | Not Started | Resource behaviour over 24 hours: the baseline the on-call engineer compares against |
| Production deployment record, go-live sign-off | TASK-100 | Not Started | What is in PROD and when it went in |
| Hypercare daily log, exit review sign-off | TASK-101 | Not Started | The defect trend and the formal move to standard support |
| Cutover reconciliation report, decommission record | TASK-102 | Not Started (the import mechanism the cutover uses is delivered: H-75) | What was migrated and what the legacy system no longer holds |
| An administrator guide | — (F-2) | No task | §5.1 stands in for it |
| A trainer pack for AHDA's own trainers | — (F-4) | No task | KT-5 of the knowledge-transfer record |

The Release Checklist puts acceptance before the operations period starts, which is after TASK-100 and TASK-101. The
package is therefore issued for acceptance once those records exist, or accepted with them listed as exceptions on
the acknowledgment (§9 step 4).

### 5.1 Administrator task map

No administrator guide exists. Until one does, this is where each administration task is done and which record states
its rules. Every screen below is under Administration and needs the permission its record names; most are R01's.

| Task | Where | Rules |
| --- | --- | --- |
| Create, edit, disable a user | `/admin/users` — ADM-002 to ADM-005; *Disable* asks for step-up MFA | H-70 D-3, D-11 |
| Roles and the permission matrix | `/admin/roles` — ADM-006 to ADM-008; `/admin/permission-matrix` — ADM-009 | H-70 |
| Assign a role to a user over a department, entity or project | `/admin/role-assignments` — ADM-010 | H-70 |
| Departments and external entities | `/admin/organization` — ADM-011 to ADM-013 | H-70 |
| Integration configuration: directory and SSO, Exchange, Nafath, internal systems, the API catalogue | `/admin/integrations-overview` — ADM-040; the per-integration screens ADM-041 to ADM-046 (`/admin/sso-directory-integration`, `/admin/email-exchange-integration`, `/admin/nafath-integration`, `/admin/internal-system-integrations`, `/admin/api-integration-catalogue`, `/admin/integration-detail-configuration/:integrationId`) | H-73, H-76 |
| Integration health, attempts, dead letters, retry | `/admin/integration-monitoring` — ADM-047; `/admin/integration-logs` — ADM-048 | H-34, H-73 |
| Audit trail, system event logs, integration audit logs | `/admin/audit-trail` — ADM-050, ADM-051; `/admin/system-event-logs` — ADM-052; `/admin/integration-audit-logs` — ADM-053 | H-74 |
| Load legacy data | `/admin/data-migration` | H-75 |
| Master data catalogues and configuration versions (ADM-020 to ADM-029), including NOTIFICATION_ROUTING | **API only**: `/api/v1/master-data-catalogues`, `/api/v1/configuration-versions` with `validate` and `publish` — authored, validated and published by three different people | H-72 D-2, F-5; H-35 D-6 (routing) |
| Notification templates; delivery list; redrive a dead letter | **API only**: `/api/v1/notification-templates`, `/api/v1/notification-deliveries`, `/api/v1/notification-deliveries/{deliveryId}/redrive`, `/api/v1/notification-intents/{intentId}/redrive` (R01: `NOTIFICATION_*`) | H-35 D-8 (templates), D-13, D-14 |

The two API-only rows need a client that holds a session and its CSRF token. Swagger UI cannot stand in: it is enabled
only in DEV, SIT and the local compose stack, and it cannot send API requests ("Try it out" is off; H-03 D-4, D-7). See F-3.

## 6. Operations calendar

What recurs during the operations period, and who owns it. Where the period is AHDA's to set, it says TBC. An owner
marked *(proposed)* is not named by any record; the receiving team confirms it at review (§9 step 3).

| # | Activity | When | Owner | Source |
| --- | --- | --- | --- | --- |
| C-1 | Full-history secret scan, reported to the named owner | Weekly, Monday 03:17 UTC (`secret-scan.yml`); issue #92 | Security Lead (`scan-owner.yaml`) | H-65 D-7; CTL-42, CTL-55 |
| C-2 | Dependency vulnerability scan, HIGH and CRITICAL, reported to the named owner | Weekly, Monday 03:47 UTC (`dependency-vuln-gate.yml`); issue #93 | Security Lead | H-65 D-6, D-7; CTL-55 |
| C-3 | Cross-domain journeys; accessibility audit | Nightly 21:40 UTC (`e2e-journeys.yml`); weekly, Sunday 22:10 UTC (`accessibility-audit.yml`) | QA *(proposed)* | H-85, H-82 |
| C-4 | On-call rotation handover | Weekly, at the start of the first working day (proposed) | DevOps/Platform Lead | H-01 §3 |
| C-5 | Next penetration test | Six months after go-live if AHDA classifies the platform critical, twelve if not (PRE-6, not yet classified) | AHDA Cybersecurity | H-64 F-3 |
| C-6 | Monthly periodic visit; post-incident reviews of P1/P2 presented there | Monthly | PMO Engagement Lead *(proposed)* | Client Scope §11; H-01 §7 |
| C-7 | Periodic technical performance report | Cadence TBC — Client Scope §11 says "periodic" | PMO Engagement Lead *(proposed)* | Client Scope §11 |
| C-8 | Patch and vulnerability cycle for runtime, base images and dependencies | Monthly or quarterly by system classification (Guide §14); remediation times TBC on OQ-009 (G-5) | DevOps/Platform Lead, Security Lead | Guide §14; H-60 G-5 |
| C-9 | Restore drill | Before the first PROD release; after any change to backup configuration or to a restore procedure; standing cadence TBC with the retention period (OQ-003) | DevOps/Platform Lead | H-40 §8 |
| C-10 | Runtime upgrade: .NET 10 LTS support ends November 2028; one in-support LTS upgrade is budgeted inside the operations period | Plan before November 2028 | AHDA IT (H-23 S-2); the delivery team carries out the upgrade | H-23 S-2; CTL-45 |
| C-11 | Access review of role and profile assignments; secret rotation | Cadences TBC — no control or value yet (G-4, G-3) | AHDA IT; Security Lead | Guide §14; H-60 G-3, G-4 |
| C-12 | Documentation currency: a change to a documented behaviour updates its record, and its help article, in the same PR; this index fails CI when a record's status changes and it does not | Every PR | Each PR's author and reviewer | Guide §14; Master Plan 4.9; H-08 F-5; HP-3 |

## 7. What the receiving team inherits open

Each item is open in the record named; it is gathered here so that the acknowledgment (§9) can list what AHDA accepts
as open. None is closed by this task.

| # | Item | Record | Owner | Gate |
| --- | --- | --- | --- | --- |
| O-1 | The contractual SLA table — response, update and resolution times, hours of cover, service credits (OQ-009 / UGV-06) | H-01 F-1, H-02 F-1 | AHDA Contracts / PMO Engagement Lead | Before Production |
| O-2 | The on-call roster: people, deputies, contacts, held in AHDA's on-call tool | H-01 F-2, H-40 BR-6 | DevOps/Platform Lead; PMO | Before Production |
| O-3 | The on-call tool, and so the alert channel type | H-01 F-3 | DevOps/Platform Lead, AHDA IT | Release Checklist "Operational Runbooks & On-Call Ready" (H-01 RC-6) |
| O-4 | The ticketing system for incident records | H-01 F-4 | AHDA IT | No gate set in the record |
| O-5 | No environment exists: ADR-001 R-1 (written confirmation), R-2 (region), R-3 (tenancy), the DNS zone | H-22, H-50, H-55 | AHDA IT | Before the first `terraform apply` |
| O-6 | No operator path into the VPC, needed by every restore verification | H-40 BR-1 | TASK-021; AHDA Cybersecurity | Before the SIT drill |
| O-7 | The SIT restore drill from a Cloud SQL automated backup (E-4) | H-40, H-41 | DevOps/Platform Lead | Release Checklist "Backup & DR Restore Drill Passed" |
| O-8 | No APM chosen for the environments; no alert on the APM or on failed backups | H-33 F-2; H-01 F-5, F-6 | DevOps/Platform Lead | No gate set in the records |
| O-9 | Vendor remote access, including from outside the Kingdom, not yet accepted by AHDA Cybersecurity (CTL-51) | H-01 F-8; H-22 C-9 | Security Lead; AHDA Cybersecurity | No gate set; until accepted, the rota is staffed only by engineers who can work from where the access model allows |
| O-10 | Backup retention period (OQ-003) | H-40 BR-8 | AHDA Cybersecurity + Records | Before Production |
| O-11 | No DR target for loss of the region (S-7 cannot meet the RTO) | H-40 BR-5 | AHDA IT | Accepted gap until AHDA IT answers |
| O-12 | The security incident procedure rests on AHDA's CS-026-2025, not transcribed | H-01 F-7 | AHDA Cybersecurity; Security Lead | No gate set in the record |
| O-13 | No SMS provider (TASK-103); PB-5 unexercised | H-01 F-10; H-35 F-2 | TASK-103 | Before SMS is used |
| O-14 | Security gate not green: 10 High/Critical findings open; penetration test not commissioned | H-62, H-64 | Security Lead; AHDA Cybersecurity | Release Checklist security rows |
| O-15 | UAT not executed; nine Critical readiness defects | H-83 §8 | PMO; QA *(proposed)* | Release Checklist UAT row |
| O-16 | ADR-002 (stack) and ADR-003 (architecture pattern) not approved by AHDA | H-21, H-20, H-23 | AHDA IT | Release Checklist "Architecture Decisions Approved" |
| O-17 | Master data, configuration and notification operations have no screen (§5.1) | H-72 F-5 | PMO (follow-up task) | Before AHDA administers configuration itself |
| O-18 | The release and rollback strategy is not written (TASK-098) | §5 | TASK-098 | Before go-live |
| O-19 | The onboarding guide's human dry run; the help content's bilingual business sign-off | H-07 §9.3; H-08 §6.2 | PMO | TASK-095, TASK-096 acceptance |
| O-20 | AHDA's receiving operations team is not named | F-1 | AHDA IT | Before §9 can start |

## 8. Findings

| # | Finding | Owner | Action |
| --- | --- | --- | --- |
| F-1 | **AHDA's receiving operations team is not named.** The acceptance criterion needs it to review and sign; no source names it. The Guide asks AHDA for the "support model, ticketing, on-call/escalation, maintenance windows, monitoring ownership" (§15), and its Appendix D assigns "Support/ticketing/escalation/SLA process" to AHDA IT / Service Management "before go-live transition", which makes that the likely owner | AHDA IT | Name the team and its signatory; then §9 |
| F-2 | **No administrator guide.** Master Plan 3.9 asks for an admin runbook and support guide, and the Guide's go-live "Users" row for an admin handover. The rules are in the module records (§4.6); §5.1 maps tasks to them, which is an index, not a guide | PMO | A task for an R01 guide, bilingual if AHDA's administrators need it |
| F-3 | **Configuration and notification operations are API-only.** An AHDA administrator cannot publish a NOTIFICATION_ROUTING version, a template or a master-data change, or redrive a dead letter, without an API client that handles the session and CSRF token. Until a screen exists these stay with the delivery team | PMO; Frontend | An FG-04 and WF-15 operations UI task (H-72 F-5, `notification-center-ui.md` scope) |
| F-4 | **No trainer pack.** Client Scope §12 asks that AHDA staff be able to train others (KT-5). The user guide is written for users, not for trainers | PMO; Business Analyst | Write it once the UAT build is stable (Master Plan 3.9) |
| F-5 | **No knowledge-transfer session has been held**, and none of the hands-on ones can be until an environment exists to train on (O-5) | PMO Engagement Lead | Hold the sessions in `knowledge-transfer-record.md` §2; log each |
| F-6 | **The package is accepted before its last records exist.** The Release Checklist puts acceptance before the operations period, but TASK-098 to TASK-102 produce records the receiving team needs (§5). Either issue the package after TASK-101 or accept it with those as listed exceptions and re-issue | PMO Engagement Lead, with AHDA | Decide at the first review session |
| F-7 | **The repository's ownership during operations is not settled.** It is public, under a personal account, and the CODEOWNERS teams do not exist (H-57 S-2). Who administers it, the CI and the artifact retention for 22 months is part of the tenancy question (Decision Log, "GCP tenancy: AHDA's or the vendor's — affects ownership and end-of-contract handover") | AHDA IT; PMO | Decide with UGV-07 |
| F-8 | **The acknowledgment has no Arabic text yet.** AHDA signs it, so it is issued in both languages, as ADR-012 requires of the interface and controlled content; a machine-only translation would not do for a signed document (the TASK-096 rule) | Bilingual business reviewer | Prepare and check the Arabic before issue |

## 9. Review and acceptance

**Acceptance criterion:** "Handover package is reviewed and formally accepted by AHDA's receiving operations team,
with a signed knowledge-transfer acknowledgment; the package index cross-references every document produced by this
workbook's Documentation-category tasks."

| Part | Status |
| --- | --- |
| The index cross-references every document of the Documentation-category tasks | **MET.** §3; HP-1 fails the build if one is dropped |
| Every linked document resolves to an existing, current file (validation check) | **MET** at this commit: HP-2 resolves every link, HP-3 compares every stated status with its record (§10) |
| Reviewed and formally accepted by AHDA's receiving operations team | **NOT MET.** The team is not named (F-1) |
| Signed knowledge-transfer acknowledgment | **NOT MET.** The form is ready; nothing is signed |

**Procedure.**

1. AHDA names the receiving operations team and its signatory (F-1).
2. The PMO Engagement Lead issues the package at a named commit of `main`, and holds the knowledge-transfer sessions
   (`knowledge-transfer-record.md` §2), logging each.
3. The receiving team reviews §3 to §7. Its comments are kept in AHDA's document or ticketing system, not in this
   public repository. Each comment that changes a record is fixed in that record by PR.
4. For each item of §7 and §5 the receiving team either accepts it as open, listing it on the acknowledgment as an
   exception, or requires it closed before signing.
5. The signatories sign the acknowledgment. **The signed original stays in AHDA's document control**: it carries names
   and signatures, and the repository is public (the same rule as the on-call roster, H-01 F-2).
6. One PR then records the receipt in `knowledge-transfer-acknowledgment.md` §4 (where the original is held, the date,
   the commit reviewed, the signatories' roles), marks the acknowledgment SIGNED and this package ACCEPTED. HP-4 refuses
   either without the other, and `--gate` turns green. The Release Checklist row "Operational Handover Package
   Accepted" can then be set to Complete.

## 10. Verification

| # | What | Result |
| --- | --- | --- |
| 1 | The workbook's Documentation category, from the local xlsx and the Google Sheet (all 112 rows) | TASK-093 to TASK-097 in both |
| 2 | `python3 docs/handover/handover-package-check.py` (in `repo-checks`) | Passes: 71 index rows, every Documentation-category deliverable indexed, every link resolves, every stated status current |
| 3 | `python3 docs/handover/handover-package-check.py --gate` | Fails, as it must: "the package has not been accepted by AHDA's receiving operations team" |
| 4 | Mutation run, `artifacts/task-097/mutations.py` (local, not committed: `artifacts/` is ignored) | 13 of 13 caught: deliverable dropped and wrong task (HP-1); missing file and duplicate ID (HP-2); a record's status changed without the index (HP-3); ACCEPTED without a signed acknowledgment, a receipt filled or SIGNED while COMPILED, a short commit, the gate before acceptance (HP-4); an area dropped, an unknown index row, an undated session (HP-5). The positive case — acceptance recorded in full, with H-12 and H-14 updated — passes `--gate`. The tree is unchanged afterwards |
| 5 | Every other `repo-checks` script | All pass |

## 11. Change log

| Date | Change |
| --- | --- |
| 2026-10-24 | Compiled (this record): index of 71 rows, admin task map, operations calendar, inherited open items, knowledge-transfer record, acknowledgment form, guard |
| 2026-10-24 | Independent fact-check of every cross-reference against the cited records, routes, OpenAPI document and controlled sources: 10 corrections — gates of O-3, O-4, O-8, O-9, O-12 now as the records give them (most set none); C-10's owner AHDA IT; §5.1's routing and template rules, integration routes and the Swagger UI limits; H-21's scope (ADR-001 to ADR-013); H-22's open tenancy; §5's status source and TASK-102's delivered import; F-8's basis. Owners no record names marked *(proposed)*. All statuses quoted in §3 and §4 were found correct |

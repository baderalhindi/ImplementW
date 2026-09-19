# Wave-to-Phase Cross-Reference (W0–W6 ↔ P0–P18)

| Field | Value |
| --- | --- |
| Task | TASK-003 — Ratify Delivery Wave Plan (W0-W6) (P0 - Discovery & Governance) |
| Depends on | TASK-002 — Step 14B RTM Findings Closure (`docs/rtm/step-14b-rtm-findings-closure.md`, DISPOSITIONED — AWAITING AHDA SIGN-OFF) |
| Record date | 2026-09-19 |
| Status | **PROPOSED — AWAITING PMO ENGAGEMENT LEAD ACCEPTANCE** (sign-off block in §8) |
| Branch | `chore/task-003-wave-plan-ratification` |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), sheet Implementation Plan, columns Phase and Depends On Task Name, as exported 2026-09-19: 102 task rows, TASK-001–TASK-102, 19 phases P0–P18 |
| Controlled sources and authority order | per TASK-001 |

## 1. What this record is

The workbook sequences delivery by **Phase** (P0–P18, one phase per row in the Implementation Plan sheet). The engagement's delivery plan sequences it by **Wave** (W0–W6, defined only in the TASK-003 description). This record ties the two together so that a phase can be located in the wave plan and a wave can be located in the workbook without interpretation.

It contains: the wave definitions as stated (§2), the mapping rule (§3), the phase-level 1:1 cross-reference the acceptance criterion asks for (§4), the task-level assignment that the phase-level table is derived from (§5), the dependency-order check from the TASK-003 validation note (§6), and the points the PMO Engagement Lead must decide or confirm at ratification (§7).

The wave plan itself is not a controlled source; its only written form is the TASK-003 description. This record does not change the wave sequence; it maps to it.

## 2. Wave definitions (as stated in TASK-003)

| Wave | Content, verbatim from the TASK-003 description | Workbook domains read into it |
| --- | --- | --- |
| W0 | scope/acceptance/policy gates | Discovery & governance; architecture decision records |
| W1 | secure engineering, identity, audit, configuration, integration | Repo/CI foundation; environments and infrastructure; database foundation; FG-03 identity; FG-04 configuration; FG-06 audit service; platform security controls |
| W2 | documents, approvals, notifications, first Project registration | WF-12; WF-11; WF-15; WF-01 |
| W3 | schedule, tasks, risk, issues, financial/KPI | WF-03; WF-04; WF-06; WF-07; WF-14 (and WF-02 as their data dependency — see §7.3) |
| W4 | milestones, change control, official progress | WF-05; WF-08; WF-02 official-progress increment (§7.3) |
| W5 | external participation, suspension, dashboards, reports, audit review | WF-13; WF-09; FG-01; FG-02; FG-06 audit UI |
| W6 | closure, handover, production assurance | WF-10; documentation and handover; testing, observability, security assurance, release |

Waves are sequential: the prerequisite waves of Wn are W0…W(n−1).

## 3. Mapping rule

1. **Each task is assigned to exactly one wave** from the wave text in §2. Where the text does not name a task's subject, the task goes to the wave whose purpose it serves (e.g. observability → W6 "production assurance").
2. **Each phase is mapped to exactly one wave: the wave in which the phase's last task is owed** (its completion wave). A phase is not complete until every task in it is complete, so this is the only wave assignment under which "phase Pn is done" and "wave Wm is done" can be checked against each other.
3. Tasks inside a phase that the wave text places in an *earlier* wave than the phase's completion wave are **pulled-forward items**. They are listed per phase in §4 and per task in §5. They are owed in their own wave, not the phase's.
4. A wave assignment is the wave in which the deliverable is **owed**. It is not an earliest-start date; a task may start earlier when its dependencies allow.

## 4. Phase-to-wave cross-reference (1:1)

| Phase | Workbook phase name | Tasks | Wave | Basis in wave text | Pulled-forward items (owed earlier than the phase wave) |
| --- | --- | --- | --- | --- | --- |
| P0 | Discovery & Governance | TASK-001–004 | **W0** | scope/acceptance/policy gates | — |
| P1 | Architecture Decisions | TASK-005–010 | **W0** | policy gates (ADR-001–012; Release Checklist gate "Architecture Decisions Approved") | — |
| P2 | Project Setup & Foundation | TASK-011–015 | **W1** | secure engineering | — |
| P3 | Infrastructure & DevOps | TASK-016–023 | **W1** | secure engineering | — |
| P4 | Database Foundation | TASK-024–027 | **W1** | configuration (core schema incl. master data) | — |
| P5 | Identity & Access Management (FG-03) | TASK-028–033 | **W1** | identity; audit (TASK-033) | — |
| P6 | Shared Platform Services | TASK-034–040 | **W2** | documents (WF-12), approvals (WF-11), notifications (WF-15) | TASK-034 FG-04 → W1 (configuration) |
| P7 | Project Establishment (WF-01) | TASK-041–043 | **W2** | first Project registration | — |
| P8 | Execution & Performance (WF-02/03/04/05/14) | TASK-044–054 | **W4** | milestones (WF-05); official progress | TASK-044–049, 052, 053 → W3 (schedule, tasks, financial/KPI, and WF-02 as their dependency) |
| P9 | Risk & Issue Management (WF-06/07) | TASK-055–059 | **W3** | risk, issues | — |
| P10 | Governance & Change Control (WF-08/09/10) | TASK-060–065 | **W6** | closure (WF-10) | TASK-060, 061 WF-08 → W4 (change control); TASK-062 WF-09 → W5 (suspension) |
| P11 | Collaboration & External Participation (WF-13) | TASK-066–068 | **W5** | external participation | — |
| P12 | Management Intelligence (FG-01/02) | TASK-069–072 | **W5** | dashboards, reports | — |
| P13 | Technical Operations (FG-05/06) | TASK-073–076 | **W5** | audit review (TASK-074) | TASK-073 FG-06 service → W1 (audit); TASK-075, 076 FG-05 → W2 (see §7.2) |
| P14 | Security Hardening & Compliance | TASK-077–083 | **W6** | production assurance (TASK-082 penetration test) | TASK-078, 079, 080, 083 → W1 (secure engineering); TASK-077 → W2; TASK-081 → W5 |
| P15 | Testing & QA | TASK-084–089 | **W6** | production assurance | TASK-084 test strategy → W1 |
| P16 | Observability | TASK-090–093 | **W6** | production assurance | — |
| P17 | Documentation | TASK-094–097 | **W6** | handover | — |
| P18 | Release & Go-Live | TASK-098–102 | **W6** | production assurance | — |

Reverse view (wave → phases whose completion wave it is):

| Wave | Phases completed in this wave | Tasks owed in this wave (from §5) |
| --- | --- | --- |
| W0 | P0, P1 | 10 |
| W1 | P2, P3, P4, P5 | 30 |
| W2 | P6, P7 | 12 |
| W3 | P9 | 13 |
| W4 | P8 | 5 |
| W5 | P11, P12, P13 | 10 |
| W6 | P10, P14, P15, P16, P17, P18 | 22 |

Every phase P0–P18 appears exactly once in the first table; the acceptance criterion names P1–P18, and P0 is included because it exists in the workbook (§7.4).

## 5. Task-to-wave assignment

The phase-level table in §4 is derived from this table. "Depends on" lists the workbook's Depends On Task Name column resolved to task IDs, each with the wave it is owed in.

| Task | Phase | Task name | Wave | Depends on |
| --- | --- | --- | --- | --- |
| TASK-001 | P0 | Establish Controlled Source Baseline | W0 | — |
| TASK-002 | P0 | Close Step 14B RTM Findings | W0 | TASK-001 (W0) |
| TASK-003 | P0 | Ratify Delivery Wave Plan (W0-W6) | W0 | TASK-002 (W0) |
| TASK-004 | P0 | Stand Up PTBC / TBC Governance Tracker | W0 | TASK-002 (W0) |
| TASK-005 | P1 | Resolve Hosting & Data Localisation Architecture Decision Record | W0 | TASK-001 (W0) |
| TASK-006 | P1 | Ratify Application Technology Stack ADR | W0 | TASK-005 (W0) |
| TASK-007 | P1 | Define Three-Tier Solution Architecture & Module Boundaries | W0 | TASK-006 (W0) |
| TASK-008 | P1 | Produce Canonical Entity-Relationship Diagram | W0 | TASK-007 (W0) |
| TASK-009 | P1 | Define Platform API & Event Contract Conventions | W0 | TASK-007 (W0) |
| TASK-010 | P1 | Ratify Cybersecurity Control Overlay Mapping (CS-001-033) | W0 | TASK-005 (W0) |
| TASK-011 | P2 | Initialize Monorepo & Solution Structure | W1 | TASK-006 (W0) |
| TASK-012 | P2 | Define Branching Strategy, PR Template & Code Owners | W1 | TASK-011 (W1) |
| TASK-013 | P2 | Author Environment Variable Templates per Environment | W1 | TASK-011 (W1) |
| TASK-014 | P2 | Build Local Development Environment (Docker Compose) | W1 | TASK-013 (W1) |
| TASK-015 | P2 | Configure Quality Gates: Lint, Format, Type-Check, Test in CI | W1 | TASK-011 (W1) |
| TASK-016 | P3 | Provision DEV / SIT / UAT / PROD Environment Separation | W1 | TASK-005 (W0) |
| TASK-017 | P3 | Author Infrastructure as Code for Approved Hosting Target | W1 | TASK-016 (W1) |
| TASK-018 | P3 | Build CI/CD Pipeline with Controlled Promotion Gates | W1 | TASK-015 (W1) |
| TASK-019 | P3 | Integrate Approved Secret Management Store | W1 | TASK-017 (W1) |
| TASK-020 | P3 | Provision Managed AlloyDB for PostgreSQL with Backup & Encryption | W1 | TASK-017 (W1) |
| TASK-021 | P3 | Configure Network Security: WAF, Load Balancer & Segmentation | W1 | TASK-017 (W1) |
| TASK-022 | P3 | Establish Container/Artifact Build & Dependency Scanning | W1 | TASK-018 (W1) |
| TASK-023 | P3 | Define Backup, Restore & Disaster Recovery Runbook | W1 | TASK-020 (W1) |
| TASK-024 | P4 | Establish Database Migration Framework & Conventions | W1 | TASK-008 (W0), TASK-020 (W1) |
| TASK-025 | P4 | Implement Core Platform Schema (Project, User, Org, Master Data) | W1 | TASK-024 (W1) |
| TASK-026 | P4 | Define Indexing Strategy for Query, Filter, Sort & Pagination Paths | W1 | TASK-025 (W1) |
| TASK-027 | P4 | Build Seed Data & Data-Integrity Validation Scripts | W1 | TASK-025 (W1) |
| TASK-028 | P5 | Integrate AD/LDAP and SSO Authentication | W1 | TASK-025 (W1) |
| TASK-029 | P5 | Implement MFA & Privileged Access Controls | W1 | TASK-028 (W1) |
| TASK-030 | P5 | Implement Server-Side RBAC & Data-Scope Authorization Engine | W1 | TASK-028 (W1) |
| TASK-031 | P5 | Build Users, Roles & Permissions Administration (FG-03 Backend) | W1 | TASK-030 (W1) |
| TASK-032 | P5 | Build Users, Roles & Permissions Administration (FG-03 Frontend) | W1 | TASK-031 (W1) |
| TASK-033 | P5 | Implement Authentication & Access Audit Logging | W1 | TASK-030 (W1) |
| TASK-034 | P6 | Build Master Data & Configuration Service (FG-04 Backend) | W1 | TASK-025 (W1) |
| TASK-035 | P6 | Build Shared Approval Framework (WF-11 Backend) | W2 | TASK-034 (W1), TASK-030 (W1) |
| TASK-036 | P6 | Build Approvals UI: Inbox, My Requests & History (WF-11 Frontend) | W2 | TASK-035 (W2) |
| TASK-037 | P6 | Build Document & Evidence Management (WF-12 Backend) | W2 | TASK-034 (W1) |
| TASK-038 | P6 | Build Document Library & Upload UI (WF-12 Frontend) | W2 | TASK-037 (W2) |
| TASK-039 | P6 | Build Notifications, Reminders & Escalations Runtime (WF-15 Backend) | W2 | TASK-034 (W1), TASK-028 (W1) |
| TASK-040 | P6 | Build Notification Center UI (WF-15 Frontend) | W2 | TASK-039 (W2) |
| TASK-041 | P7 | Build Project Creation & Registration (WF-01 Backend) | W2 | TASK-025 (W1), TASK-035 (W2) |
| TASK-042 | P7 | Build Project Register & Creation UI (WF-01 Frontend) | W2 | TASK-041 (W2) |
| TASK-043 | P7 | Contract & Integration Tests for Project Lifecycle | W2 | TASK-041 (W2), TASK-042 (W2) |
| TASK-044 | P8 | Build Progress Update & Overall Health (WF-02 Backend) | W3 | TASK-041 (W2) |
| TASK-045 | P8 | Build Progress & Schedule UI (WF-02/WF-03 Frontend) | W3 | TASK-044 (W3) |
| TASK-046 | P8 | Build Schedule & Baseline Management (WF-03 Backend) | W3 | TASK-041 (W2) |
| TASK-047 | P8 | Build Schedule, Gantt & Baseline UI (WF-03 Frontend) | W3 | TASK-046 (W3) |
| TASK-048 | P8 | Build Task Management (WF-04 Backend) | W3 | TASK-046 (W3) |
| TASK-049 | P8 | Build Task Boards & My Tasks UI (WF-04 Frontend) | W3 | TASK-048 (W3) |
| TASK-050 | P8 | Build Milestone Management (WF-05 Backend) | W4 | TASK-046 (W3) |
| TASK-051 | P8 | Build Milestone Register & Achievement UI (WF-05 Frontend) | W4 | TASK-050 (W4) |
| TASK-052 | P8 | Build Financial Progress & KPI Performance (WF-14 Backend) | W3 | TASK-044 (W3), TASK-041 (W2) |
| TASK-053 | P8 | Build Financial & KPI UI (WF-14 Frontend) | W3 | TASK-052 (W3) |
| TASK-054 | P8 | Contract & Integration Tests for Execution Domain (WF-02/03/04/05/14) | W4 | TASK-044 (W3), TASK-046 (W3), TASK-048 (W3), TASK-050 (W4), TASK-052 (W3) |
| TASK-055 | P9 | Build Risk Management (WF-06 Backend) | W3 | TASK-041 (W2) |
| TASK-056 | P9 | Build Risk Register & Detail UI (WF-06 Frontend) | W3 | TASK-055 (W3) |
| TASK-057 | P9 | Build Issue & Challenge Management (WF-07 Backend) | W3 | TASK-041 (W2) |
| TASK-058 | P9 | Build Issue, Challenge & Escalation UI (WF-07 Frontend) | W3 | TASK-057 (W3) |
| TASK-059 | P9 | Contract & Regression Tests for Risk/Issue Domain (WF-06/07) | W3 | TASK-055 (W3), TASK-057 (W3) |
| TASK-060 | P10 | Build Change Request & Authorization (WF-08 Backend) | W4 | TASK-041 (W2), TASK-035 (W2) |
| TASK-061 | P10 | Build Change Request UI (WF-08 Frontend) | W4 | TASK-060 (W4) |
| TASK-062 | P10 | Build Suspension & Resumption (WF-09 Backend) | W5 | TASK-041 (W2) |
| TASK-063 | P10 | Build Completion & Closure (WF-10 Backend) | W6 | TASK-041 (W2), TASK-052 (W3) |
| TASK-064 | P10 | Build Suspension & Closure UI (WF-09/WF-10 Frontend) | W6 | TASK-062 (W5), TASK-063 (W6) |
| TASK-065 | P10 | Contract & Regression Tests for Governance Domain (WF-08/09/10) | W6 | TASK-060 (W4), TASK-062 (W5), TASK-063 (W6) |
| TASK-066 | P11 | Build External Entity Update & Review (WF-13 Backend) | W5 | TASK-041 (W2), TASK-028 (W1), TASK-037 (W2) |
| TASK-067 | P11 | Build External Participation UI (WF-13 Frontend) | W5 | TASK-066 (W5) |
| TASK-068 | P11 | Integrate Nafath Identity Verification (If Confirmed In Scope) | W5 | TASK-066 (W5) |
| TASK-069 | P12 | Build Dashboard Projection & Definition Service (FG-01 Backend) | W5 | TASK-044 (W3), TASK-046 (W3), TASK-055 (W3), TASK-052 (W3) |
| TASK-070 | P12 | Build Dashboards UI (FG-01 Frontend) | W5 | TASK-069 (W5) |
| TASK-071 | P12 | Build Reports, Filters & Export Service (FG-02 Backend) | W5 | TASK-069 (W5) |
| TASK-072 | P12 | Build Reports Center UI (FG-02 Frontend) | W5 | TASK-071 (W5) |
| TASK-073 | P13 | Build Formal Audit & Activity Service (FG-06 Backend) | W1 | TASK-025 (W1), TASK-028 (W1) |
| TASK-074 | P13 | Build Audit & Activity UI (FG-06 Frontend) | W5 | TASK-073 (W1) |
| TASK-075 | P13 | Build Integration Monitoring Runtime (FG-05 Backend) | W2 | TASK-028 (W1), TASK-039 (W2), TASK-073 (W1) |
| TASK-076 | P13 | Build Integration Administration UI (FG-05 Frontend) | W2 | TASK-075 (W2) |
| TASK-077 | P14 | Implement Platform-Wide Input Validation & Output Encoding | W2 | TASK-009 (W0), TASK-041 (W2) |
| TASK-078 | P14 | Implement Rate Limiting, CORS & Secure HTTP Headers | W1 | TASK-009 (W0) |
| TASK-079 | P14 | Implement CSRF Protection for Session-Based Flows | W1 | TASK-028 (W1) |
| TASK-080 | P14 | Add Secret Scanning & Dependency Vulnerability Gate to CI | W1 | TASK-019 (W1) |
| TASK-081 | P14 | Conduct Threat Modeling & Security Review for Sensitive Features | W5 | TASK-037 (W2), TASK-066 (W5), TASK-028 (W1), TASK-030 (W1) |
| TASK-082 | P14 | Commission External Penetration Test Before Production | W6 | TASK-081 (W5) |
| TASK-083 | P14 | Implement Logging Redaction & Sensitive-Data Handling Policy | W1 | TASK-033 (W1) |
| TASK-084 | P15 | Author Platform Test Strategy & Coverage Policy | W1 | TASK-015 (W1) |
| TASK-085 | P15 | Implement Cross-Domain End-to-End Test Suite | W6 | TASK-043 (W2), TASK-054 (W4), TASK-059 (W3), TASK-065 (W6) |
| TASK-086 | P15 | Execute Performance & Load Testing Against Capacity Targets | W6 | TASK-026 (W1) |
| TASK-087 | P15 | Execute Platform-Wide Accessibility Audit | W6 | TASK-032 (W1), TASK-042 (W2) |
| TASK-088 | P15 | Prepare & Execute UAT Catalogue Scenarios | W6 | TASK-085 (W6), TASK-084 (W1) |
| TASK-089 | P15 | Validate Data Migration, Rollback & Seed Integrity | W6 | TASK-024 (W1), TASK-027 (W1) |
| TASK-090 | P16 | Integrate Application Performance Monitoring & Error Tracking | W6 | TASK-018 (W1) |
| TASK-091 | P16 | Implement Health Checks & Uptime Monitoring | W6 | TASK-016 (W1) |
| TASK-092 | P16 | Build Operational Dashboards & Alerting Thresholds (FG-05 Alerts) | W6 | TASK-075 (W2), TASK-090 (W6) |
| TASK-093 | P16 | Document Operational SLAs & On-Call Runbook | W6 | TASK-091 (W6), TASK-023 (W1) |
| TASK-094 | P17 | Publish API Documentation (OpenAPI/Swagger) | W6 | TASK-009 (W0) |
| TASK-095 | P17 | Author Developer Onboarding & Architecture Guide | W6 | TASK-007 (W0), TASK-014 (W1) |
| TASK-096 | P17 | Author End-User Guide & FAQ (Arabic + English) | W6 | TASK-042 (W2), TASK-070 (W5) |
| TASK-097 | P17 | Compile Operational Handover Package | W6 | TASK-093 (W6), TASK-023 (W1), TASK-095 (W6), TASK-094 (W6) |
| TASK-098 | P18 | Define Release & Rollback Strategy | W6 | TASK-018 (W1) |
| TASK-099 | P18 | Execute Staging/Pre-Production Soak Test | W6 | TASK-088 (W6), TASK-086 (W6) |
| TASK-100 | P18 | Execute Production Go-Live | W6 | TASK-098 (W6), TASK-099 (W6), TASK-082 (W6), TASK-088 (W6) |
| TASK-101 | P18 | Run Post-Launch Hypercare & Stabilization Period | W6 | TASK-100 (W6) |
| TASK-102 | P18 | Confirm Data Migration Cutover & Legacy Decommission | W6 | TASK-089 (W6), TASK-100 (W6) |

## 6. Dependency-order check (TASK-003 validation note)

Rule checked: *no Phase references a wave-owned deliverable before that wave's prerequisite waves are marked complete*. Applied as: for every task, every task it depends on is owed in the same or an earlier wave; and for every phase, every phase it depends on has the same or an earlier completion wave.

| Check | Scope | Result |
| --- | --- | --- |
| Task level: wave(dependency) ≤ wave(task) | 102 tasks, 147 dependency edges | **PASS — 0 violations** |
| Phase level: wave(dependency's phase) ≤ wave(task's phase) | 19 phases | **PASS — 0 violations** |
| Every phase P0–P18 mapped to exactly one wave | 19 phases | **PASS** |
| Every task assigned to exactly one wave | 102 tasks | **PASS** |

Method: the Implementation Plan sheet was exported on 2026-09-19; Depends On Task Name values were resolved to task IDs by exact name match (all 147 resolved); the two inequalities above were evaluated for every edge. The check is reproducible from the two tables in §4 and §5 without the workbook.

Workbook statements that reference phases, translated to waves so they can be read against the wave plan:

| Where | Statement | In wave terms |
| --- | --- | --- |
| ADR-001 Impact; OQ-001 | "Unblocks all Infrastructure/DevOps tasks in Phase P3" | Unblocks W1 |
| ADR-011 Rationale | Impact model "fixed before Phase P9 build" | Fixed before W3 |
| OQ-004 Impact | 32 inventory-only components must be dispositioned "before Phase P7 onward begins consuming the inventory" | Before W2 (first frontend consumers are TASK-036/038/040/042 in W2) |
| Release Checklist "Step 14B RTM QA Closed" | "before UAT" | Before W6 (TASK-088) |

## 7. Points for decision or confirmation at ratification

None of these changes the mapping in §4; each is either a decision the PMO Engagement Lead makes by accepting this record, or a confirmation to be recorded with the acceptance.

| # | Point | Proposed handling |
| --- | --- | --- |
| 7.1 | **Six phases straddle waves** (P6, P8, P10, P13, P14, P15). The workbook's phases are grouped by domain; the waves are grouped by delivery order, and the two do not coincide for these phases. The 1:1 mapping therefore uses the completion wave (§3 rule 2) and lists pulled-forward items. The widest spread is P14 (tasks owed in W1, W2, W5 and W6) and P10 (W4, W5, W6). | Accept the completion-wave rule and the pulled-forward lists as the ratified reading. Alternative, not recommended: re-cut the workbook phases along wave lines, which is a rename of 6 phases across ~35 rows of a shared workbook. |
| 7.2 | **W1 names "integration", but FG-05 Integration Monitoring (TASK-075) depends on the WF-15 notifications runtime (TASK-039), which is W2.** FG-05 therefore cannot be owed in W1 without breaking the dependency rule. Assigned W2 (earliest feasible). | Confirm either (a) W2 for FG-05 as assigned, or (b) that W1 "integration" refers to identity and infrastructure integration (TASK-019, TASK-028) rather than FG-05, in which case the assignment stands and the wave text is simply narrower than its wording. |
| 7.3 | **W4 names "official progress", but WF-02 Progress Update (TASK-044) is a dependency of WF-14 (W3) and FG-01 (W5).** The WF-02 backend and UI are assigned W3. The workbook has no separate task for a W4 "official progress" increment (e.g. formal periodic progress submission), so nothing is mapped to that W4 item. | Confirm whether "official progress" is (a) satisfied by WF-02 landing in W3 (nothing further owed in W4), or (b) a distinct WF-02 increment, in which case a task must be added to the workbook under P8 and assigned W4. |
| 7.4 | **The acceptance criterion says P1–P18; the workbook has P0–P18.** P0 is mapped to W0. | Accept P0 → W0 and correct the criterion text to "P0-P18" on the next workbook re-issue. |
| 7.5 | **W6 carries 22 tasks across 6 phases**, including tasks whose dependencies are all satisfied by W1 (TASK-086, 089, 090, 091, 094, 095, 098). They are owed in W6 (§3 rule 4) but may start earlier. | No decision required for the mapping. Flag for wave-level capacity planning. |
| 7.6 | **Task count.** The baseline record (`controlled-source-baseline.md`, header) states 103 tasks; the sheet as exported on 2026-09-19 holds 102 (TASK-001–TASK-102). | Confirm 102 and correct the baseline header on its next re-issue. |
| 7.7 | **The wave plan has no controlled written form** other than the TASK-003 description; this record is now its most complete written statement. | On acceptance, treat §2 as the reference text for W0–W6 until a wave plan document is issued and cited from the baseline register. |

## 8. Acceptance-criteria check and sign-off

| # | Criterion | Result |
| --- | --- | --- |
| 1 | Every Phase in this workbook (P1–P18) is mapped 1:1 to a wave (W0–W6) in a cross-reference table | **MET.** §4 maps P0–P18 (19 phases, including P0 beyond the stated range) to one wave each; §5 gives the task-level derivation; §6 shows the dependency-order check passes. |
| 2 | The mapping is reviewed and accepted by the PMO engagement lead | **NOT YET MET.** Awaiting the sign-off below, including decisions on §7.1–7.4. |

| Role | Name | Decision | Date |
| --- | --- | --- | --- |
| PMO Engagement Lead | | §4 mapping: Accepted / Rejected. §7.1 rule: Accepted / Alternative. §7.2: (a) / (b). §7.3: (a) / (b). §7.4: Accepted | |

On acceptance, the Status field in the header changes to **RATIFIED** and TASK-003 moves to Completed in the workbook.

## 9. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-19 | Initial cross-reference: 19 phases and 102 tasks assigned to W0–W6; dependency-order check passed with 0 violations; 7 points raised for ratification. | Discovery (TASK-003) |

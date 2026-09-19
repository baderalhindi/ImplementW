# Controlled Source Baseline

| Field | Value |
| --- | --- |
| Task | TASK-001 — Establish Controlled Source Baseline (P0 - Discovery & Governance) |
| Baseline date | 2026-09-19 |
| Status | **PROVISIONAL** — register assembled; physical file locations unconfirmed (see §5) |
| Branch | `chore/task-001-source-baseline` |
| Source of task definitions | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), Implementation Plan sheet, 103 tasks (TASK-001–TASK-103) |

This document is the single reference every later task cites for *which* documents govern delivery and *in what order* they win when they disagree. It does not restate their content.

## 1. Authority order

When two controlled sources conflict, the higher-ranked source governs. Rank 1 is highest.

| Rank | Source | Rule |
| --- | --- | --- |
| 1 | **Latest issued WF/FG functional specification** (WF-01–WF-15, FG-01–FG-06) | The most recently issued revision of the spec for the domain in question governs that domain. |
| 2 | **Step 14A Integration & Reconciliation Review** (QA CLOSED) | Governs cross-domain integration decisions (ICD-nn) and reconciliations between specs. |
| 3 | **Functional Blueprint v2.0** (QA CLOSED) | Governs platform-wide structure, sections, appendices and anything no spec or ICD addresses. |
| 4 | **Adjusted Project Scope** | Governs what is in or out of the engagement; it does not override functional definitions above it. |

Rules of use:

- A task cites the highest-ranked source that answers its question, and names the section/ICD/spec it relied on.
- **Functional Blueprint v1.0 is superseded and is not an authoritative source.** It must not be cited as authority anywhere in the workbook or the repository.
- Superseded documents are retained for traceability but never edited in place; a new revision is issued instead.
- A business value that is PTBC/TBC-gated (approval limits, formulas, retention periods, RPO/RTO, thresholds) is not sourced from any of these documents by inference. It is sourced from the PTBC/TBC governance tracker (TASK-004) once AHDA approves it.
- Where the workbook's Architecture Decisions sheet has ratified a controlled-source decision (e.g. ADR-004 per Blueprint v2.0 Section 15, ADR-005 per ICD-17), the ADR is the citation of record and points back to the source.

## 2. Baseline register

QA status is as stated in the TASK-001 definition. "Location" is the physical file location; none is confirmed in the repository or the connected Google Drive at baseline date (see §5).

### 2.1 Platform-level documents

| Ref | Document | Version | QA status | Location | Rank |
| --- | --- | --- | --- | --- | --- |
| BP-2.0 | Functional Blueprint | v2.0 | QA CLOSED | **PENDING — not in repo/Drive** | 3 |
| S14A | Step 14A Integration & Reconciliation Review | — (not stated) | QA CLOSED | **PENDING — not in repo/Drive** | 2 |
| SCOPE | Adjusted Project Scope | — (not stated) | **PENDING** (no QA status stated) | **PENDING — not in repo/Drive** | 4 |

Blueprint v2.0 sections and appendices cited by workbook tasks: Sections 3–8, 5–6, 8.1, 10.1, 11, 12, 13, 15, 17.1, 18, 19, 20.2, 22, 22.1; Appendices B, C, D, F.2. Step 14A ICDs cited: ICD-02, ICD-03, ICD-04, ICD-17.

### 2.2 Functional specifications — Workflows (WF)

Domain titles are taken from the workbook's task names and must be confirmed against each spec's title page. No spec revision number is stated anywhere in the workbook; the Version column is therefore blank, not "1.0".

| Ref | Domain | Version | QA status | Location | Workbook phase |
| --- | --- | --- | --- | --- | --- |
| WF-01 | Project Creation & Registration | PENDING | PENDING | PENDING | P7 |
| WF-02 | Progress Update & Overall Health | PENDING | PENDING | PENDING | P8 |
| WF-03 | Schedule & Baseline Management | PENDING | PENDING | PENDING | P8 |
| WF-04 | Task Management | PENDING | PENDING | PENDING | P8 |
| WF-05 | Milestone Management | PENDING | PENDING | PENDING | P8 |
| WF-06 | Risk Management | PENDING | PENDING | PENDING | P9 |
| WF-07 | Issue & Challenge Management | PENDING | PENDING | PENDING | P9 |
| WF-08 | Change Request & Authorization | PENDING | PENDING | PENDING | P10 |
| WF-09 | Suspension & Resumption | PENDING | PENDING | PENDING | P10 |
| WF-10 | Completion & Closure | PENDING | PENDING | PENDING | P10 |
| WF-11 | Shared Approval Framework | PENDING | PENDING | PENDING | P6 |
| WF-12 | Document & Evidence Management | PENDING | PENDING | PENDING | P6 |
| WF-13 | External Entity Update & Review | PENDING | PENDING | PENDING | P11 |
| WF-14 | Financial Progress & KPI Performance | PENDING | PENDING | PENDING | P8 |
| WF-15 | Notifications, Reminders & Escalations | PENDING | PENDING | PENDING | P6 |

### 2.3 Functional specifications — Functional Groups (FG)

| Ref | Domain | Version | QA status | Location | Workbook phase |
| --- | --- | --- | --- | --- | --- |
| FG-01 | Dashboards | PENDING | PENDING | PENDING | P12 |
| FG-02 | Reports, Filters & Export | PENDING | PENDING | PENDING | P12 |
| FG-03 | Users, Roles & Permissions (Identity & Access) | PENDING | PENDING | PENDING | P5 |
| FG-04 | Master Data & Configuration | PENDING | PENDING | PENDING | P6 |
| FG-05 | Integration Monitoring & Administration | PENDING | PENDING | PENDING | P13, P16 |
| FG-06 | Formal Audit & Activity | PENDING | PENDING | PENDING | P13 |

### 2.4 Related documents that are NOT controlled sources

Listed so tasks do not promote them to authority by mistake.

| Document | Status | Relationship to baseline |
| --- | --- | --- |
| Functional Blueprint v1.0 | **SUPERSEDED** by v2.0 | Never cite as authority. |
| Step 14B Requirements Traceability Matrix (19,533 rows, 75.2% traced) | "conditionally ready" — 8 open findings | Traceability instrument, not a source. Becomes QA CLOSED via TASK-002. |
| Step 18 UAT Catalogue (4,079 draft scenarios, 15 cross-domain journeys) | DRAFT | Test instrument; consumed by TASK-084, TASK-085 and TASK-088. |
| RFP and BoQ | Contractual inputs | Feed the Adjusted Project Scope (rank 4); not cited directly for functional definitions. Their dashboard/report count conflict is resolved by ADR-006. |
| Architecture Decisions sheet (ADR-001–ADR-012) | Living register | Records ratifications of controlled-source decisions; not itself a controlled source. |
| PTBC/TBC tracker (48 themes, 511 source TBCs) | To be created by TASK-004 | Source of AHDA-approved business values. |

## 3. Acceptance-criteria check

| # | Criterion | Result |
| --- | --- | --- |
| 1 | Register lists every controlled document with version, QA status and file location | **PARTIAL.** All 24 documents (Blueprint v2.0, Step 14A, Scope, 21 specs) are listed. Version and QA status are populated only where the TASK-001 definition states them (Blueprint v2.0 and Step 14A = QA CLOSED). Spec versions, spec QA statuses, and all file locations are PENDING — see §5. |
| 2 | Authority order written down and referenced by TASK id from every later task's Detailed Description | **NOT MET in the workbook.** The authority order is written down (§1). As of baseline date, no task other than TASK-001 references "TASK-001" anywhere in the Implementation Plan sheet (grep across all 103 rows). TASK-002 and TASK-005 depend on it by name only. Required workbook edit is in §4. |
| 3 | No task cites a superseded source (Blueprint v1.0) as authoritative | **MET.** The only occurrence of "v1.0" in the workbook is TASK-001's own acceptance criterion. No task cites Blueprint v1.0. |

## 4. Required workbook edit (not applied)

To satisfy criterion 2, append the following sentence to the Detailed Description of TASK-002 through TASK-103 in the Implementation Plan sheet:

> Controlled sources and authority order per TASK-001 (docs/baseline/controlled-source-baseline.md).

This is a 102-row edit to a shared Google Sheet and was not applied by this task. It should be made by the workbook owner or explicitly authorised, as the workbook's own rule is that revisions are superseded and re-issued, never overwritten in place.

## 5. Open items blocking baseline LOCK

The register is PROVISIONAL until each item below is closed. Owner: PMO Engagement Lead unless stated.

| # | Item | Why it blocks |
| --- | --- | --- |
| 5.1 | Physical location of all 24 controlled documents. Neither the repository (`ImplementW/`, single commit, CLAUDE.md only) nor the connected Google Drive (searched by title and full text for Blueprint, Step 14A, WF-/FG-, Scope) contains them. | Cannot lock a baseline against files that cannot be checked out or hashed. Recommended: place read-only copies (or a manifest with SHA-256 per file) under `docs/baseline/sources/`. |
| 5.2 | Revision number and QA status for each of the 21 WF/FG specs. | Rank 1 is "latest issued spec" — undefined until the issued revision per spec is recorded. |
| 5.3 | Revision identifier and QA status for the Adjusted Project Scope. | Rank 4 source has no stated version or QA status. |
| 5.4 | Revision/date identifier for the Step 14A review. | Recorded as QA CLOSED but with no revision identifier. |
| 5.5 | File-count cross-check. The TASK-001 validation note asks to cross-check "the 33 WF/FG + Blueprint + Step14A file count" against the RTM's "242 canonical components". The TASK-001 description defines **21** specs, giving 23 files with Blueprint and Step 14A, not 33; and the figure 242 appears nowhere else in the workbook. Both numbers need confirmation before the check can be executed. | Validation step in the task definition is not executable as written. |
| 5.6 | Confirm the 21 domain titles in §2.2–2.3 against each spec's title page. | Titles were derived from workbook task names. |

## 6. How later tasks cite this baseline

- In a task's Detailed Description: `per TASK-001` plus the specific source, e.g. `WF-11 §4.2 (rank 1)`, `ICD-17 (rank 2)`, `Blueprint v2.0 Section 15 (rank 3)`.
- In code, migrations and ADRs: cite the same way in the header comment or Rationale field.
- If a task finds a conflict between sources, it records the conflict and the winning source under the authority order; it does not choose silently.

## 7. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-19 | Initial register assembled from the Implementation Plan workbook; status PROVISIONAL. | Discovery (TASK-001) |

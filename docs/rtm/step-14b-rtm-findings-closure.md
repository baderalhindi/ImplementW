# Step 14B RTM — Findings Closure Record

| Field | Value |
| --- | --- |
| Task | TASK-002 — Close Step 14B RTM Findings (P0 - Discovery & Governance) |
| Depends on | TASK-001 — Controlled Source Baseline (`docs/baseline/controlled-source-baseline.md`, PROVISIONAL) |
| Record date | 2026-09-19 |
| RTM status before this record | "conditionally ready" — 8 open findings (7 Important, 1 documentation); 19,533 rows, 75.2% traced |
| RTM status after this record | **DISPOSITIONED — AWAITING AHDA SIGN-OFF** (see §6 for what flips it to QA CLOSED) |
| Branch | `chore/task-002-rtm-findings-closure` |
| Controlled sources and authority order | per TASK-001 |

## 1. What this record is

This is the disposition addendum to the Step 14B Requirements Traceability Matrix assessment. It records, per finding, the decision, the evidence for it, who approved it (or must), and which later task consumes it.

It is a separate file, not an edit of the assessment, because **the Step 14B RTM assessment document is not in the repository or the connected Google Drive** (searched by title and full text on 2026-09-19; TASK-001 §5.1 records the same gap for every controlled document). Finding titles below are taken from the TASK-002 definition in the Implementation Plan workbook; they must be reconciled to the RTM's own finding identifiers and wording when the document is located (§7, item 7.1).

## 2. Evidence consulted

| Source | Used for |
| --- | --- |
| Implementation Plan workbook, sheet Implementation Plan — TASK-002, TASK-003, TASK-004, TASK-020, TASK-023, TASK-069–072, TASK-084/085/088, including the "Gate Decision Applied" column | Finding list; downstream consumers; gate outcomes applied per task |
| Workbook sheet Architecture Decisions — ADR-005, ADR-006, ADR-007–012 | Dashboard/report count decision; export baseline; gate approvals dated 19 Sep 2026 |
| Workbook sheet Open Questions — OQ-002, OQ-003, OQ-004, column "Resolution (19 Sep 2026)" | AHDA gate outcomes on count, retention and inventory-only components |
| Workbook sheet Release Checklist — gate "Step 14B RTM QA Closed" | Closure evidence expected: "RTM assessment document shows Status = QA CLOSED with all 8 findings dispositioned" |
| `docs/baseline/controlled-source-baseline.md` | Authority order; PENDING status of Scope revision (§5.3) and the 242-component figure (§5.5) |

Not available: the RTM assessment itself, Functional Blueprint v2.0 (Appendix B, F.2), the RFP, the BoQ, the Adjusted Project Scope, the Step 18 UAT Catalogue. Nothing below is quoted from those documents; where the workbook quotes them, the workbook is cited.

## 3. Finding register

Disposition status legend: **CLOSED** = AHDA-approved decision on record; **DEFERRED** = disposition is a named gate, no value invented; **PROPOSED** = disposition written, AHDA approval not yet on record.

| ID | Finding (per TASK-002) | Class | Disposition | Status | Approval basis |
| --- | --- | --- | --- | --- | --- |
| F-01 | Adjusted scope coverage | Important | Coverage check runs against the identified Scope revision; each scope line item maps to ≥1 RTM row or is marked out-of-scope | PROPOSED | Needs PMO Engagement Lead + AHDA Business Sponsor |
| F-02 | Dashboard/report count discrepancy | Important | **3 dashboards and 10 reports.** BoQ "24 outputs" are the delivery company's project progress reports, not platform features. Export formats do not multiply the count | CLOSED | AHDA gate 19 Sep 2026 (OQ-002 resolution; ADR-006; TASK-069–072 gate notes) |
| F-03 | Backup retention period | Important | RPO 1 h / RTO 4–6 h confirmed. Retention period: no value; deferred to PTBC-048, gate **Before Production**; mechanism built with the period configurable | DEFERRED | RPO/RTO: AHDA IT BCP via gate (OQ-003). Retention: AHDA Cybersecurity + Records, open |
| F-04 | 32 inventory-only components | Important | Default rule: not in the build list unless traced to a rank 1–3 source; per-component build/defer/drop list to be produced from the RTM | PROPOSED | OQ-004 "Unchanged by the gate"; needs PMO Engagement Lead |
| F-05 | Generic story references | Important | Replace with specific spec citations per TASK-001 §6; rows that cannot be cited specifically are reclassified untraced | PROPOSED | Needs PMO Engagement Lead (traceability method, not a business value) |
| F-06 | Delivery acceptance criteria | Important | Acceptance criteria are Step 18 UAT scenarios; every RTM requirement row links to ≥1 scenario ID or is flagged | PROPOSED | Needs PMO Engagement Lead + QA Lead |
| F-07 | TBC crosswalk (511 source TBCs → 48 PTBC themes) | Documentation | Owned by the TASK-004 tracker; RTM cites PTBC IDs only; every source TBC carries a resolution gate | DEFERRED | Handed to TASK-004; approval of gate assignments by AHDA at each gate |
| F-08 | *Not identified in the workbook* | Important (by elimination) | Cannot be dispositioned until the RTM document names it | OPEN | — |

Count check: TASK-002 states 8 findings but names 7 subjects. F-07 is the documentation finding by nature; F-08 is the unnamed seventh Important finding. If the RTM instead classifies one of F-01–F-06 as documentation, re-label on reconciliation (§7.1).

## 4. Dispositions

### F-01 Adjusted scope coverage

- **Finding.** RTM coverage of the Adjusted Project Scope is unverified.
- **Evidence.** The Scope is rank 4 in the authority order and has no recorded revision identifier or QA status (baseline §5.3). A coverage percentage against an unidentified revision is not meaningful.
- **Disposition.** (a) PMO records the Scope revision identifier in the baseline register (closes baseline §5.3). (b) Every line item of that revision is mapped to at least one RTM row, or is explicitly marked out-of-scope with the reason. (c) Items in the RTM with no scope line item are listed as candidates for F-04 treatment. The finding closes when (b) has zero unmapped, unmarked items.
- **Owner / approver.** PMO Engagement Lead prepares; AHDA Business Sponsor approves the out-of-scope markings.
- **Consumed by.** TASK-003 (wave plan must not schedule out-of-scope items); all P5–P13 build tasks.

### F-02 Dashboard/report count discrepancy

- **Finding.** RFP states "3 dashboards + 10 reports"; BoQ states "24 outputs"; Blueprint Appendix B lists 12 DSH- dashboards and 11 SCR-13x report screens.
- **Evidence.** Open Questions OQ-002, "Resolution (19 Sep 2026)": *"RESOLVED by ADR-006. 3 dashboards and 10 reports. The 24 BoQ outputs are the delivery company's project progress reports, not platform features. Export format does not multiply the count."* TASK-069 and TASK-070 gate notes: *"FIXED at 3 dashboards (ADR-006)."* TASK-071 and TASK-072: *"FIXED at 10 reports (ADR-006)."* ADR-005 (approved): export baseline PDF/XLSX/CSV.
- **Disposition.** **The platform delivers 3 dashboards and 10 reports.** This is the single figure carried into the FG-01/FG-02 tasks (TASK-069, TASK-070, TASK-071, TASK-072). The BoQ's 24 outputs are engagement deliverables (progress reporting by the delivery company) and are tracked outside the RTM. Export formats (ADR-005) are properties of a report, not additional reports.
- **Status.** CLOSED — AHDA gate 19 Sep 2026, owner PMO Engagement Lead + AHDA Business Sponsor.
- **Inconsistency to correct in the workbook.** The ADR-006 row still reads "Not yet selected — Pending resolution via Step 14B RTM finding closure (TASK-002)", status "Open — Blocking", while OQ-002 and four task rows record it as resolved. The Release Checklist gate "Architecture Decisions Approved" requires ADR-001–006 = Approved. Required edit is in §7.2.
- **Residual item (not a reopening of the count).** TASK-070 still describes DSH-001–012 and TASK-072 SCR-130–140. A catalogue mapping is needed stating which Appendix B DSH-/SCR- screens compose each of the 3 dashboards and 10 reports, and which Appendix B screens are consequently out of scope, deferred, or composed sections (e.g. DSH-009 inside SCR-040 per TASK-070). Under the authority order, FG-01/FG-02 specs (rank 1) define what a dashboard or report *is*; the Scope (rank 4) fixes how many are delivered. Owner: PMO Engagement Lead, before TASK-069 starts. The count does not change while this is done.

### F-03 Backup retention period

- **Finding.** Retention period for backups is unspecified (PTBC-048).
- **Evidence.** OQ-003, "Resolution (19 Sep 2026)": RPO 1 hour and RTO 4–6 hours confirmed from AHDA IT's business continuity plan; *"STILL OPEN: the backup retention period, with AHDA Cybersecurity and Records."* TASK-020 gate note: *"Retention period is still TBC (OQ-003) — build the mechanism, leave the period configurable."* TASK-023: *"Retention period still TBC."*
- **Disposition.** No retention value is set in this record; per TASK-001 §1, a TBC-gated value is sourced only from the PTBC tracker after AHDA approval. Disposition is a deferral: PTBC-048 (retention component), resolution gate **Before Production**, decision owner AHDA Cybersecurity + Records. TASK-020 implements retention as a configuration value with no default committed as a business decision; TASK-023 records the retention field as TBC in the runbook. The Release Checklist gate "Backup & DR Restore Drill Passed" is scored on RPO/RTO now and on retention once approved.
- **Status.** DEFERRED to a named gate (satisfies the TASK-002 validation rule "resolved or explicitly deferred to a named PTBC gate").
- **Consumed by.** TASK-004 (PTBC-048 row), TASK-020, TASK-023.

### F-04 32 inventory-only components

- **Finding.** 32 components appear in the RTM component inventory with no traced requirement, and have no disposition (build / defer / drop).
- **Evidence.** OQ-004 status Open, *"Unchanged by the gate."* Blocking tasks: this task and indirectly every WF/FG frontend task. The 32-row list lives in the RTM document, which is not available here; the RTM's inventory total (242 canonical components per the TASK-001 validation note, unconfirmed — baseline §5.5) is likewise unverified.
- **Disposition.** (a) Rule: a component with no trace to a rank 1–3 source is **not** in the build list. It is not built by inference. (b) PMO extracts the 32 rows from the RTM and marks each Build (with the spec section that traces it — which moves it out of "inventory-only"), Defer (with the gate), or Drop. (c) Frontend tasks P7–P13 consume only components marked Build. Default for any row left unmarked when a consuming task starts is Defer.
- **Owner / approver.** PMO Engagement Lead prepares; AHDA Business Sponsor approves Drop decisions.
- **Status.** PROPOSED.

### F-05 Generic story references

- **Finding.** RTM rows cite generic user-story references rather than a specific controlled-source section.
- **Evidence.** TASK-001 §6 sets the citation form: `per TASK-001` plus the source and section, e.g. `WF-11 §4.2 (rank 1)`. A generic story reference does not identify a governing source and cannot be resolved under the authority order when sources disagree.
- **Disposition.** Each affected row is re-cited to a specific spec section, ICD, or Blueprint section. A row that cannot be so cited is reclassified as untraced and its requirement is treated under F-04 rule (a). The traced percentage is recomputed after re-citation (§8). The RTM does not retain generic story IDs as the sole trace.
- **Owner / approver.** PMO Engagement Lead. This is a traceability-method decision, not an AHDA business value; AHDA sign-off is on the resulting percentage, not each row.
- **Status.** PROPOSED.

### F-06 Delivery acceptance criteria

- **Finding.** RTM requirement rows lack measurable delivery acceptance criteria.
- **Evidence.** The Step 18 UAT Catalogue holds 4,079 draft scenarios and 15 cross-domain journeys; TASK-084 maps test layers to it, TASK-088 executes a defined subset "tied to the 15 cross-domain journeys and every P0-priority task". The Release Checklist gate "UAT Catalogue Executed & Signed Off" is the acceptance instrument.
- **Disposition.** The acceptance criterion for an RTM requirement row is the set of Step 18 scenario IDs that verify it. Every traced row links to ≥1 scenario ID; a traced row with no scenario is flagged "no acceptance test" and listed as TASK-088 input, not silently accepted. Acceptance is not restated in prose in the RTM. The 15 cross-domain journeys are mandatory coverage.
- **Owner / approver.** PMO Engagement Lead + QA Lead; AHDA Business Sponsor signs the UAT subset at TASK-088.
- **Status.** PROPOSED.

### F-07 TBC crosswalk

- **Finding.** The crosswalk from the 511 source-level TBCs to the 48 consolidated PTBC themes (PTBC-001–048) is not documented in the RTM.
- **Evidence.** TASK-004 creates exactly this tracker, "tagged with their resolution gate (Before Build/Integration Build, Before UAT, Before Production, or May Remain Configurable)", and depends on this task.
- **Disposition.** The crosswalk is owned by the TASK-004 tracker, not the RTM. The RTM cites PTBC IDs only; the tracker holds the 511 → 48 mapping and one resolution gate per source TBC. This record hands TASK-004 the requirement that no source TBC is left without a gate. Resolved TBCs on record at handover: PTBC-048 RPO/RTO (F-03); ADR-007–012 gate outcomes; open items per OQ-005–010.
- **Owner.** Discovery (TASK-004). AHDA approves values gate by gate, not the crosswalk itself.
- **Status.** DEFERRED to TASK-004 (documentation finding; closes when the tracker exists with all 511 rows gated).

### F-08 Unnamed finding

- **Finding.** TASK-002 states 8 findings and names 7 subjects. The eighth is not identifiable from the workbook.
- **Disposition.** None possible. Recorded so the count in this register is honest. Dispositioned on reconciliation with the RTM document (§7.1).
- **Status.** OPEN.

## 5. Figure carried forward

**Dashboards: 3. Reports: 10.** Source: AHDA gate 19 Sep 2026 via OQ-002 / ADR-006. Consumers: TASK-069, TASK-070 (FG-01), TASK-071, TASK-072 (FG-02). Note: the TASK-002 acceptance criterion says "TASK-062+"; TASK-062 is WF-09 Suspension & Resumption in the current workbook. The FG-01/FG-02 tasks are TASK-069–072, and the figure is already recorded in their "Gate Decision Applied" column.

## 6. RTM status and the path to QA CLOSED

The Release Checklist gate "Step 14B RTM QA Closed" requires: *"RTM assessment document shows Status = QA CLOSED with all 8 findings dispositioned."* This record does not meet that as of its date, and says so. Status is **DISPOSITIONED — AWAITING AHDA SIGN-OFF**. It changes to QA CLOSED when all of the following are true:

| # | Condition | Finding | Owner |
| --- | --- | --- | --- |
| 6.1 | RTM assessment document located and finding IDs reconciled to F-01–F-08 | all, F-08 | PMO Engagement Lead |
| 6.2 | AHDA Business Sponsor signs the dispositions of F-01, F-04, F-05, F-06 (F-02 is already approved; F-03 and F-07 are deferrals to named gates) | F-01, F-04–F-06 | AHDA Business Sponsor |
| 6.3 | 32-component build/defer/drop list produced | F-04 | PMO Engagement Lead |
| 6.4 | Re-citation of generic story rows done and percentage recomputed | F-05 | PMO Engagement Lead |
| 6.5 | Sign-off block in §10 completed | all | AHDA Business Sponsor, PMO Engagement Lead |

A deferral (F-03, F-07) does not block QA CLOSED: the RTM is a traceability instrument, and a TBC value deferred to a named gate is a complete trace. The value itself is tracked in TASK-004.

## 7. Required actions outside this repository (not applied)

| # | Action | Why not applied here |
| --- | --- | --- |
| 7.1 | Place the Step 14B RTM assessment (and its finding list) under `docs/rtm/` or record its location and SHA-256 in the baseline register | File not available; same as baseline §5.1 |
| 7.2 | Update the ADR-006 row in the Architecture Decisions sheet: Selected Option = "3 dashboards and 10 reports (RFP figure); BoQ 24 outputs are engagement progress reports; export formats do not multiply the count"; Status = "Approved (AHDA gate, 19 Sep 2026)" | Shared workbook; per the workbook's own rule, revisions are re-issued, not overwritten in place (baseline §4) |
| 7.3 | Update OQ-004 with the F-04 rule and the 32-row list once produced | Same |
| 7.4 | Correct "TASK-062+" in the TASK-002 acceptance criterion to "TASK-069–072" | Same |
| 7.5 | Produce the DSH-/SCR- to 3-dashboard/10-report catalogue mapping before TASK-069 starts (F-02 residual) | Requires Blueprint Appendix B and the FG-01/FG-02 specs, which are not available here |

## 8. Validation checks from the TASK-002 definition

| Check | Result |
| --- | --- |
| RTM row count and traceability percentage re-validated (currently 19,533 rows, 75.2% traced) | **NOT DONE.** RTM not available. Both figures are carried unchanged from the TASK-001/TASK-002 definitions. Re-validation must follow F-05 re-citation, which will change the percentage. |
| 511 source TBCs each resolved or explicitly deferred to a named PTBC gate | **NOT DONE here; assigned to TASK-004** (F-07). Resolved at gate 19 Sep 2026 and recorded in the workbook: RPO/RTO (PTBC-048 part), and the subjects of ADR-007–012. |

## 9. Acceptance-criteria check

| # | Criterion | Result |
| --- | --- | --- |
| 1 | Each of the 8 findings has a written, AHDA-approved disposition recorded in the RTM assessment | **PARTIAL.** 8 rows written (§3–4). AHDA approval on record for F-02; F-03 and F-07 are deferrals to named gates; F-01, F-04, F-05, F-06 are proposed and await sign-off (§6.2); F-08 is unidentifiable without the RTM. Recorded in this addendum, not in the assessment file (§1). |
| 2 | RTM status changes from "conditionally ready" to QA CLOSED | **NOT MET.** Status is DISPOSITIONED — AWAITING AHDA SIGN-OFF. Conditions in §6. |
| 3 | Final dashboard and report count is a single unambiguous number carried into the FG-01/FG-02 tasks | **MET.** 3 dashboards, 10 reports (§5); already present in TASK-069–072 gate notes. |

## 10. Sign-off

| Role | Name | Decision | Date |
| --- | --- | --- | --- |
| AHDA Business Sponsor | | F-01, F-04, F-05, F-06 dispositions: Approved / Rejected (state which) | |
| PMO Engagement Lead | | §6.1, 6.3, 6.4 complete; RTM status set to QA CLOSED | |
| QA Lead | | F-06 scenario-linkage rule accepted for TASK-084/088 | |

## 11. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-19 | Initial closure record: 8 findings registered, F-02 closed on the 19 Sep 2026 gate, F-03/F-07 deferred to named gates, F-01/F-04/F-05/F-06 proposed, F-08 open. | Discovery (TASK-002) |

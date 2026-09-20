# PTBC / TBC Governance Tracker

| Field | Value |
| --- | --- |
| Task | TASK-004 — Stand Up PTBC / TBC Governance Tracker (P0 - Discovery & Governance) |
| Depends on | TASK-002 — Step 14B RTM Findings Closure (`docs/rtm/step-14b-rtm-findings-closure.md`, DISPOSITIONED — AWAITING AHDA SIGN-OFF), finding F-07 |
| Record date | 2026-09-20 |
| Status | **PROVISIONAL — 18 of 48 themes populated** (see §9 for what blocks LOCK) |
| Branch | `chore/task-004-ptbc-governance-tracker` |
| Register files | `ptbc-themes.csv` (48 rows), `ptbc-task-crossreference.csv` (52 links), `source-tbc-crosswalk.csv` (8 of 511), `unassigned-gated-values.csv` (11 rows), `ptbc-tbc-tracker.xlsx` (generated) |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), all five sheets, as exported 2026-09-20: 112 task rows (TASK-001–TASK-112), ADR-001–013, OQ-001–012 |
| Controlled sources and authority order | per TASK-001 |

## 1. What this tracker is

TASK-001 §1 states the rule this tracker exists to serve:

> A business value that is PTBC/TBC-gated (approval limits, formulas, retention periods, RPO/RTO, thresholds) is not sourced from any of these documents by inference. It is sourced from the PTBC/TBC governance tracker (TASK-004) once AHDA approves it.

This is the register that rule points at. It holds one row per consolidated PTBC theme, one row per source-level TBC, and a link from every workbook task that touches a gated value to the theme that governs it. It records **who decides**, **by when**, and **what is on record so far** — never a value the delivery team chose.

The operative rule: **a task does not set a gated value by inference.** Until AHDA approves it, the value stays absent and the mechanism is built to accept it later. A delivery-team proposal is recorded in the "Working baseline (not approved)" column and never in "AHDA-approved value".

The tracker does not decide anything itself. Gate assignments in §3 are proposed by Discovery; AHDA approves values gate by gate, as TASK-002 F-07 records.

## 2. Register files and how to use them

The CSVs are the tracked source of record — they diff, review and merge. `ptbc-tbc-tracker.xlsx` is a generated view of the same four tables for PMO use, rebuilt by `python3 docs/governance/build-tracker.py` after any CSV edit. Do not edit the workbook by hand; the edit will be lost on the next build.

| File | Rows | Holds |
| --- | --- | --- |
| `ptbc-themes.csv` | 48 | PTBC-001–048: theme, category, source spec, resolution gate, gate basis, FG-04 configurability, status, decision owner, approved value, working baseline, linked tasks, earliest linked wave, evidence |
| `ptbc-task-crossreference.csv` | 52 | Task ↔ PTBC links, with the wave the task is owed in (per TASK-003) and whether the link is already in the workbook or is a required edit |
| `source-tbc-crosswalk.csv` | 8 | Source-level TBC IDs recoverable from the workbook, with resolution and gate. 503 of 511 are not loadable — §5 |
| `unassigned-gated-values.csv` | 11 | Gated AHDA values that are live in the workbook but carry **no** PTBC ID — §6 |

## 3. Resolution gates

The four gates are those named in the TASK-004 definition. A gate is a **point of no return**, not a preference: it is the last moment at which the value can arrive without rework.

| Gate | Meaning | Latest point | Test for assigning it |
| --- | --- | --- | --- |
| **Before Build/Integration Build** | The value changes structure — schema, state machine, stored fields, captured events, or which personal data is retained. Deciding later means migrating data or losing records that were never captured. | Before the owning module's backend task starts (see "Earliest linked wave") | Would a later answer force a migration, a re-approval of live records, or the loss of an event that cannot be reconstructed? |
| **Before UAT** | The value does not change structure but determines whether a behaviour is correct. The module is built generically and the value is seeded as configuration. Until it exists, UAT cannot score the behaviour. | Before TASK-088 (Prepare & Execute UAT Catalogue Scenarios) | Can the mechanism be built and tested with a placeholder, but not *accepted* without the real value? |
| **Before Production** | An operational or contractual target with no effect on build or on functional UAT. It is scored at a release gate. | Before TASK-100 (Execute Production Go-Live) | Does only a release-checklist gate depend on it? |
| **May Remain Configurable** | No committed value is needed at all. AHDA sets and changes it in FG-04 configuration after go-live without a release. The row closes when the mechanism exists, not when a value arrives. | — | Is there any point at which an unset value blocks a gate? If not, this. |

Gate and configurability are separate questions and the register keeps them in separate columns. Most Before-UAT themes are *also* FG-04 configuration: the mechanism is configurable, and the value is still gated because UAT cannot be signed off against an empty configuration. No theme is currently assigned **May Remain Configurable** — every populated theme has at least one gate that depends on its value.

## 4. The 48 PTBC themes — current state

`ptbc-themes.csv` holds all 48 rows, PTBC-001 through PTBC-048. Population is uneven because the consolidated theme list itself is not available (§9.1):

| Population | Rows | IDs |
| --- | --- | --- |
| Subject, category, gate and evidence recorded | 18 | 001, 002, 003, 006, 009, 011, 017, 019, 024, 025, 027, 029, 031, 036, 037, 040, 043, 048 |
| Category and linked task known, subject not recoverable | 4 | 026, 028, 030 (cybersecurity governance group, per TASK-010); 046 (operational threshold, cited jointly with 037 at TASK-092) |
| No reference anywhere in the workbook | 26 | 004, 005, 007, 008, 010, 012–016, 018, 020–023, 032–035, 038, 039, 041, 042, 044, 045, 047 |

Every populated row is sourced from a named workbook cell, ADR or Open Question; the Evidence column quotes it. The 26 empty rows exist as reserved IDs so that no later task can claim an unused number, and so the deficit is visible rather than silent.

### 4.1 Status

| Status | Rows | Meaning |
| --- | --- | --- |
| Resolved | 1 | PTBC-036 — AHDA decision on record (ADR-007) |
| Partially Resolved | 6 | 003, 009, 017, 027, 031, 048 — part approved at the 19–20 Sep 2026 gates, part still open |
| Open | 9 | 001, 002, 006, 011, 019, 024, 025, 029, 037 |
| Open (baseline proposed) | 2 | 040, 043 — a delivery-team working baseline is recorded; **no AHDA decision exists** |
| Unpopulated | 30 | 26 with no reference, plus 026, 028, 030, 046 |

### 4.2 Gate distribution

| Gate | Rows | IDs |
| --- | --- | --- |
| Before Build/Integration Build | 5 | 002, 003, 011, 031, 036 |
| Before UAT | 10 | 001, 006, 009, 017, 019, 024, 025, 027, 040, 043 |
| Before Production | 4 | 029, 037, 046, 048 |
| May Remain Configurable | 0 | — |
| PENDING | 29 | 26 unreferenced, plus 026, 028, 030 |

### 4.3 The values that are on record

These are the only AHDA-approved entries in the register. Everything else is empty by design.

| PTBC | Approved | Basis | Still open |
| --- | --- | --- | --- |
| 036 | AHDA's existing directory with SSO; directory authoritative for department, manager and job title; platform roles assigned in the platform, **not** inherited from directory groups | ADR-007, AHDA gate 19 Sep 2026 | — |
| 048 | RPO 1 hour; RTO 4 to 6 hours | AHDA IT business continuity plan, AHDA gate 19 Sep 2026 (ADR-001, OQ-003) | **Backup retention period — no value.** Owner AHDA Cybersecurity + Records |
| 031 | Nafath in scope for external entity users, identity verification at onboarding only, never internal sign-in; a persistent sign-in session follows | ADR-007, ADR-013 | Data-minimisation boundary: which identity attributes are stored |
| 003 | Entity-initiated project draft with AHDA approval; R04 Project Manager employer-neutral, scoped to the entity's own project | ADR-013, AHDA PMO 20 Sep 2026 | — (eligibility criteria per entity not separately stated) |
| 017 | Shape only: multi-dimensional impact (cost, schedule, reputation, ≥1 operational), five levels per dimension, same scale for risks and issues | ADR-011, AHDA gate 19 Sep 2026 | Level descriptions, quantitative boundaries, 5×5 mapping, rating labels |
| 009 | Shape only: three bands across cost, schedule and scope; highest band any dimension triggers; changes accumulate against the active baseline | ADR-016 | All band boundary values |
| 027 | Step-up authentication applies to privileged and sensitive actions | ADR-010, AHDA gate 19 Sep 2026 | MFA/PAM policy detail; the step-up trigger list, which follows the classification taxonomy (UGV-01) |

PTBC-040 (WCAG 2.1 Level AA) and PTBC-043 (150 named users, 25 concurrent, 50 at peak) are **delivery-team positions recorded as working baselines, not approvals.** The Release Checklist rows for both say the same thing: "BASELINE PROPOSED … awaiting AHDA confirmation." They are not written into the approved-value column, and the tracker does not treat them as decided.

## 5. Source-level TBCs — 8 of 511 loaded

TASK-002 F-07 assigned this tracker the requirement that no source TBC is left without a gate. That requirement is **not met** and cannot be met from the sources available.

The source TBC identifier format is `TBC-<domain>-<nnn>` — for example `TBC-IAM-002`, `TBC-RPT-20`. Eight such IDs appear in the workbook, all introduced by the 20 Sep 2026 ADR-018/ADR-019 amendments to TASK-067, TASK-110, TASK-111 and TASK-112. They are loaded into `source-tbc-crosswalk.csv` with their resolution and gate. Six are Resolved, two (TBC-RPT-01, TBC-RPT-03) partially.

The remaining **503 are not loadable.** The 511 → 48 crosswalk lives in the Step 14B RTM and the consolidated theme list in Blueprint v2.0 Appendix F.2; neither is in the repository or the connected Google Drive (searched by title and full text on 2026-09-20; TASK-001 §5.1 records the same gap for every controlled document). Their PTBC theme column therefore reads PENDING for all eight loaded rows as well: the crosswalk direction cannot be inferred from a source TBC's domain prefix.

## 6. Gated values with no PTBC ID

Eleven AHDA values are live in the workbook, block a named task, and carry **no PTBC identifier**. They are recorded in `unassigned-gated-values.csv` as UGV-01 to UGV-11 so they are governed by the same rule even before they are reconciled to the theme list. Each has a proposed gate and a decision owner.

The three that matter most:

- **UGV-01 — data sensitivity and field-level classification taxonomy.** ADR-010 is approved but its status line reads "taxonomy outstanding". Field-level masking applies to screens, dashboards, reports, exports and API projections; ADR-010's own rationale says deciding late "would force a revisit of every projection in the platform". Proposed gate: **Before Build/Integration Build**. It also supplies the step-up trigger list that PTBC-027 needs. Owner: AHDA Cybersecurity + PMO.
- **UGV-03 — notification channel scope per event family.** TASK-039 cites "SMS Conditional/TBC per PTBC" with **no identifier at all** — a dangling reference. ADR-004 has since enabled SMS at launch with the event-to-channel matrices as FG-04 configuration AHDA populates.
- **UGV-02 — project-level progress override tolerance.** TASK-044's gate note reads "Override tolerance still TBC". No PTBC ID, no Open Question row.

Two of the eleven are blocked on Open Questions that **do not exist**: TASK-105 cites OQ-014 and TASK-106 cites OQ-013, but the Open Questions sheet holds only OQ-001 to OQ-012. Similarly, ADR-014 through ADR-019 are cited by the 19–20 Sep 2026 amendments across 27 task rows but the Architecture Decisions sheet holds only ADR-001 to ADR-013. Both are recorded as required workbook edits in §8.

## 7. Task cross-reference

`ptbc-task-crossreference.csv` links **35 tasks** to PTBC themes across **52 task-theme pairs**, each with the value the task touches and the wave the task is owed in (per TASK-003 §5).

| Link status | Links | Tasks |
| --- | --- | --- |
| Present in workbook — the row already cites the PTBC ID | 21 | 13: TASK-010, 020, 023, 028, 029, 033, 041, 051, 055, 068, 086, 087, 092 |
| Required workbook edit — the task touches the value but does not cite the ID | 31 | 22: TASK-013, 026, 027, 034, 035, 050, 052, 053, 056, 057, 059, 060, 061, 066, 067, 073, 075, 083, 084, 091, 093, 106 |

Scope rule used: a task touches a PTBC-gated value if it implements, seeds, configures, tests or scores a value that an Open Questions row or the task's own TBC/outstanding wording identifies as an AHDA decision. Engineering configuration that no Open Question governs (rate-limit thresholds, log levels, CI coverage gates) is out of scope; those are delivery-team values and the register does not claim them.

TASK-002 and TASK-004 reference the PTBC system itself rather than a gated value and are deliberately not in the cross-reference.

The heaviest concentration is **W1**: PTBC-001, 009, 011, 017, 024, 025, 031, 036, 040, 043 and 048 all have their earliest linked task owed in wave W1, largely through TASK-027 (seed data) and TASK-034 (FG-04 configuration service). FG-04 is the single largest consumer — five themes publish through it — which is why TASK-034's own acceptance criterion already requires that a missing required configuration value "causes the requesting operation to fail explicitly rather than silently defaulting". That behaviour is the technical enforcement of this tracker's rule, and it should not be relaxed.

## 8. Required workbook edits (not applied)

Per the workbook's own rule — revisions are re-issued, not overwritten in place (TASK-001 §4) — these are specified, not applied. The Implementation Plan sheet is shared and owned outside this repository.

| # | Edit | Rows | Why |
| --- | --- | --- | --- |
| 8.1 | Append to the Detailed Description of each of the 22 tasks listed as "Required workbook edit" in §7: `PTBC-gated value: see PTBC-0nn (docs/governance/ptbc-tbc-tracker.md).` using the IDs in `ptbc-task-crossreference.csv` | 22 | Satisfies the TASK-004 acceptance criterion from the workbook side |
| 8.2 | Replace "SMS Conditional/TBC per PTBC" in TASK-039 with a PTBC ID, or with an explicit statement that the channel scope is governed by UGV-03 and ADR-004 | 1 | A PTBC reference with no identifier cannot be traced |
| 8.3 | Add rows OQ-013 (change materiality band values) and OQ-014 (governance profile assignment thresholds) to the Open Questions sheet | 2 | Both are cited as blocking by TASK-105 and TASK-106 but have no row |
| 8.4 | Add rows ADR-014 through ADR-019 to the Architecture Decisions sheet | 6 | Cited across 27 task rows by the 19–20 Sep 2026 amendments; ADR-018 and ADR-019 carry six of the eight resolved source TBCs |
| 8.5 | Record the eleven UGV rows on the Open Questions sheet, or assign each a PTBC ID once Appendix F.2 is available | 11 | Gated values currently governed by nothing |
| 8.6 | Update the ADR-006 row per TASK-002 §7.2 (still outstanding at this record's date) | 1 | Carried forward from TASK-002 |

## 9. Open items blocking tracker LOCK

Owner: PMO Engagement Lead unless stated.

| # | Item | Why it blocks |
| --- | --- | --- |
| 9.1 | **Blueprint v2.0 Appendix F.2** — the consolidated PTBC theme list. Not in the repository or Drive. | 26 of 48 rows have no subject and 29 have no gate. The TASK-004 validation check — "spot-check 10 PTBC rows against Blueprint Appendix F.2 for exact wording" — cannot be executed at all; only 18 rows have wording to check, and none of it is from Appendix F.2. |
| 9.2 | **Step 14B RTM** — the 511 source TBCs and their 511 → 48 crosswalk. Not available (TASK-002 §7.1). | 503 of 511 source TBCs are unloaded and ungated. TASK-002 F-07 closes only when the tracker holds all 511 gated. |
| 9.3 | AHDA approval of the proposed gate assignments for the 19 gated themes and the 11 UGV rows. | A gate is a commitment about when a decision is owed; it is proposed here, not agreed. |
| 9.4 | Confirmation of the positional reading of OQ-006 → PTBC-017 / 024 / 025 (risk matrix, KPI formulas/targets, financial thresholds, in that order). | Three theme subjects rest on the order in which one sentence lists them. |
| 9.5 | Subjects for PTBC-019 and PTBC-046, each currently defined only as "the other half of the pair cited at TASK-051 / TASK-092". | Two rows carry a gate inherited from their pair rather than from their own subject. |
| 9.6 | Wave assignment for TASK-103–112 (added 19–20 Sep 2026, after TASK-003 mapped 102 tasks). | TASK-106 appears in the cross-reference with no wave, so its gate deadline cannot be read off the wave plan; the other nine have not been screened against the wave plan at all. |

## 10. Acceptance-criteria check

| # | Criterion | Result |
| --- | --- | --- |
| 1 | Tracker contains all 48 PTBC rows with category, source spec, resolution gate and current status | **PARTIAL.** All 48 rows exist (PTBC-001–048) with a status on every row. Category is populated on 22, source spec and resolution gate on 19 (gate on 19; 026/028/030 have a category but no gate). The remaining rows are marked PENDING against Blueprint Appendix F.2 rather than filled by inference — §9.1. |
| 2 | Every task that touches a PTBC-gated value links back to the corresponding PTBC row by ID | **MET in the tracker, NOT MET in the workbook.** All 35 such tasks are linked by ID in `ptbc-task-crossreference.csv`. 13 tasks already carry the ID in the workbook; the other 22 need the one-line edit specified in §8.1, which has not been applied to the shared sheet. |
| Validation | Spot-check 10 PTBC rows against Blueprint Appendix F.2 for exact wording | **NOT DONE.** Appendix F.2 is not available (§9.1). All 18 populated rows are worded from the workbook, and the Evidence column names the cell each came from. |
| Validation | Confirm no row has an invented value in the "selected" field before a gate gives AHDA the chance to approve it | **MET.** The AHDA-approved value column is non-empty on 7 rows, each carrying its approval basis (ADR-001, 007, 010, 011, 013, 016 and the 19–20 Sep 2026 AHDA gates). The two delivery-team positions (PTBC-040, PTBC-043) are held in a separate "Working baseline (not approved)" column and labelled as proposals. No other row carries a value. |

## 11. Sign-off

| Role | Name | Decision | Date |
| --- | --- | --- | --- |
| PMO Engagement Lead | | Gate assignments in `ptbc-themes.csv` and `unassigned-gated-values.csv`: Accepted / Revised (state which). §8 workbook edits: authorised / declined | |
| AHDA Business Sponsor | | Decision ownership per theme accepted; approval to be given gate by gate, not on the register as a whole | |
| AHDA Cybersecurity | | UGV-01 classification taxonomy gate (Before Build/Integration Build): Accepted / Revised. PTBC-048 retention period owner confirmed | |

## 12. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-20 | Register stood up: 48 PTBC rows (18 populated, 4 partial, 26 reserved), 52 task links across 35 tasks, 8 of 511 source TBCs, 11 unassigned gated values. Four gates defined as points of no return. 6 required workbook edits and 6 LOCK blockers recorded. | Discovery (TASK-004) |

# Legacy-data migration plan — Blocked status record

| Field | Value |
| --- | --- |
| Task | TASK-089 — Validate Data Migration, Rollback & Seed Integrity (P15 - Testing & QA), third acceptance criterion |
| Record date | 2026-10-19 |
| Status | **BLOCKED on OQ-008.** AHDA has not said what system holds its existing project records, in what format, at what volume, or how its fields map to the platform. No plan can be written for a source nobody has described, so this record logs the block, the questions that lift it, and the requirements the plan must meet once they are answered |
| Blocking question | **OQ-008** (workbook, Open Questions): "Legacy project-records source system, format, volume and field mapping for data migration/cutover are not yet documented." Status **Blocked**; the 19 Sep 2026 gate left it unchanged. Same item as UGV-08 (`docs/governance/unassigned-gated-values.csv`), which needs it "before the migration rehearsal, not before build" |
| Decision owner | AHDA IT / Data Owner |
| What it blocks | This plan; TASK-102 (Confirm Data Migration Cutover & Legacy Decommission); the Release Checklist gate "Data Migration Cutover Rehearsed", whose evidence is "Migration rollback validation report and a rehearsed legacy cutover reconciliation report both show pass". The first half is `rollback-validation.md`; the second cannot exist until this block is lifted |
| Secret | `LEGACY_SYSTEM_CONNECTION_STRING` (workbook, Environment and Secrets): read-only, cutover window only, revoked afterwards. **Not created**, because there is no source to connect to. TASK-102 owns it |
| Workbook read | Local copy `AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`, sheets Implementation Plan (TASK-089, TASK-102), Open Questions (OQ-008), Release Checklist, Environment and Secrets, read 2026-10-19 |

## 1. Why this is logged as Blocked and not drafted

The acceptance criterion offers two outcomes: a plan "once AHDA confirms source system details", or the plan "explicitly
logged as Blocked until then". AHDA has not confirmed anything. Every element of a migration plan depends on the source:
the extract method (database export, files, an API), the field mapping, code-list translation, which records are in scope,
how much history to carry, quality rules and the cutover window. A plan written before those answers would invent them.
Step 17 says the same about the document half (TBC-DOC-018: "Do not hard-code unresolved source value").

Two things are not blocked and are done:

- **The platform side of a migration is validated.** Migrations roll back and re-apply at representative volume without
  losing data outside what each Down drops (`rollback-validation.md`). The data-integrity validator that a legacy load must
  pass afterwards is in place and runs after every migration and data load (`docs/architecture/seed-data-and-integrity.md` D-9).
- **Projects already under way have a route that needs no bulk import.** ADR-014 gives WF-01 a legacy intake path: a
  project enters once with a Declared Baseline and its opening progress, spend and milestones (`erd.md` §5.4,
  `ProjectIntake`). That path is TASK-104's and is not built yet (`project-registration.md` F-9). Whether AHDA's existing
  records enter that way, by bulk import, or both, is question Q-2 below.

## 2. Questions that lift the block

Each answer belongs to AHDA IT / Data Owner unless noted. The plan can be written once Q-1 to Q-6 are answered. Q-7 to Q-12
are needed before the first rehearsal.

| # | Question | Why the plan needs it |
| --- | --- | --- |
| Q-1 | Which system or systems hold the existing project records, and in which version? Spreadsheets count | Extract method, tooling and who can run it |
| Q-2 | Which records move: all projects, only active ones, or closed ones as well? Which enter through the ADR-014 intake path (TASK-104) instead of a bulk import? | Scope. The intake path records an opening position. It does not reconstruct history |
| Q-3 | How is the data extracted: database export, file export (CSV, Excel), or an API? Who runs the extract, and how is it handed over? | The read-only connection the workbook plans (`LEGACY_SYSTEM_CONNECTION_STRING`) only fits a database or API source |
| Q-4 | Volume per record type: projects, tasks, milestones, risks, issues, documents (count and total size), users | Rehearsal sizing; whether the TASK-026 representative volume (3,000 projects) is the right stand-in |
| Q-5 | Field mapping: who owns it, and what are the legacy codes for status, department, classification, region and the other catalogues the platform has (`seed-data-and-integrity.md` §3)? | Code lists are master data AHDA owns (ADR-012); unmapped codes must be quarantined, not guessed |
| Q-6 | How much history: past progress periods, approvals, audit records, superseded document versions? | History cannot be entered later through the intake path |
| Q-7 | People: how are legacy owners and project managers identified, and do they all exist in AD? | Every platform row names a user; an owner the directory does not know needs a decision |
| Q-8 | Documents (TBC-DOC-018): which repository, which files, what metadata, what classification? | Every migrated file passes the malware scan and gets an import audit record (WF-12) |
| Q-9 | Known quality problems: duplicates, missing owners, inconsistent dates or statuses | Guide §9.5 step 3 (profiling); rules for which rows to reject |
| Q-10 | Classification of the data, and approval to use a production extract or a masked copy in a non-PROD rehearsal | Guide §9.3: no live production data in DEV/TEST without explicit approval |
| Q-11 | Cutover date, freeze window, and who signs the reconciliation | TASK-102's acceptance: field-by-field sample signed off by AHDA before decommission |
| Q-12 | What happens to the legacy system afterwards: decommission, read-only archive, or retain? | TASK-102 "per AHDA's instruction" |

## 3. What the plan must satisfy

These requirements already apply to the plan. They come from controlled sources, not from this record.

| Source | Requirement |
| --- | --- |
| Development and Cybersecurity Guide §9.5 | 1. Inventory sources and name a business owner for each dataset. 2. Define the target field mapping and transformation rules. 3. Profile quality: duplicates, invalid values, missing owners, inconsistent dates and statuses. 4. Write repeatable transformation scripts, not one-off manual copy and paste. 5. Rehearse with representative data. 6. Reconcile record counts and control totals. 7. Document rejected records and business decisions. 8. Back up and define rollback before the final cutover. 9. Run the final reconciliation and get business sign-off after the production migration |
| Guide §9.3 | Synthetic or approved masked data for DEV/TEST; a production copy only with explicit approval and the controls its classification requires |
| Step 17 BE-027 (proposed mechanism) | "map legacy IDs, quarantine invalid rows and preserve import lineage. Rollback or forward-fix choice follows approved data-loss constraints." Verification: "Re-run import without duplicate identities; compare counts, relationships and approved control totals; rehearse recovery." Gate: before migration implementation and cutover |
| WF-12 §24/§25, TBC-DOC-018 | Documents: source ID and path, project mapping, metadata mapping, validation, scanning, duplicate warning, import audit. Large-scale ETL is a separate workstream |
| TASK-102 acceptance | Post-cutover counts reconcile table by table with zero unexplained variance unless documented; AHDA signs off a field-by-field sample before the legacy system is decommissioned |
| `seed-data-and-integrity.md` D-9 | `validate-data-integrity.sql` runs after every data load and fails the load on any orphan, constraint or catalogue violation |

## 4. What the platform already gives the plan

When the block lifts, these parts exist and do not need to be designed again:

| Need | Already in place |
| --- | --- |
| Integrity after a load | `db/seed/validate-data-integrity.sql`: every foreign key, CHECK, index, audited actor and master data catalogue reference, read from the database catalogue on each run, so it covers every table |
| Re-runnable inserts with stable identities | The seed's pattern (`seed-data-and-integrity.md` D-3, D-4): rows keyed on a business key, ids `md5('<table>:<key>')::uuid`. A legacy id in that key gives BE-027's "re-run import without duplicate identities" |
| A rehearsal database at volume, and a comparison of two states | `db/migrations/validation/` builds a PostgreSQL 17 database at the TASK-026 volume through the release image, and fingerprints every table, row count and column. A rehearsed legacy load can be added as one more SQL file in the load chain |
| Rollback before cutover | Restore from the pre-cutover backup, following the TASK-023 runbook (`docs/operations/backup-restore-dr-runbook.md`). A data load is not a migration, so rolling back a schema migration does not undo it |
| Projects already under way | ADR-014 intake path (TASK-104, not built) |

## 5. What lifts the block, and what follows

1. AHDA answers Q-1 to Q-6 and OQ-008 is closed in the workbook.
2. The plan replaces §2 of this record: source inventory, mapping, quality rules, the transformation scripts' location and
   owner, rehearsal schedule, reconciliation queries and control totals, rejected-record handling, cutover and rollback.
   The plan is written by whoever takes TASK-089's third criterion forward at that point; the Data Owner signs off the mapping.
3. TASK-102 rehearses the cutover against a masked or approved copy (Q-10) in a non-PROD environment, reconciles it, and
   produces the second half of the Release Checklist evidence.

Until then the Release Checklist row "Data Migration Cutover Rehearsed" cannot close, and neither can TASK-102.

## 6. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-10-19 | Initial record: logged as Blocked on OQ-008. Twelve questions for AHDA; the controlled requirements the plan must satisfy; what the platform already provides for it | Testing (TASK-089) |

# Seed Data and Data-Integrity Validation

| Field | Value |
| --- | --- |
| Task | TASK-027 — Build Seed Data & Data-Integrity Validation Scripts (P4 - Database Foundation) |
| Depends on | TASK-025 — Implement Core Platform Schema (`core-platform-schema.md`) |
| Record date | 2026-09-26 |
| Status | **BUILT AND VERIFIED LOCALLY** on PostgreSQL 17.11, through the release image and through Compose. Wired into the pipeline, where the scratch-database step runs on every push to `main`. **Not yet run in an environment**: none exists (F-5) |
| Branch | `feat/task-027-seed-and-integrity-validation` |
| Deliverables | `db/seed/seed-master-data.sql`; `db/seed/validate-data-integrity.sql`; the release image's `seed` and `validate-data-integrity` commands (`PMPlatform.Infrastructure/Persistence/DatabaseScripts.cs`, `PMPlatform.Api/Program.cs`); `.github/scripts/seed-dry-run.sh` and its step in `ci-cd-pipeline.yml`; the post-migration executions in `.github/scripts/migrate-environment.sh`; `SeedDataTests` and `DataIntegrityTests` (`PMPlatform.Tests.Integration/Persistence`); this record |
| Environment variables / secrets | `DB_CONNECTION_STRING`, read by both commands through the same configuration and secret-store path as `migrate` (TASK-019, TASK-024). Nothing is added to the sheet. `seed-dry-run.sh` also takes `DATABASE_URL` for `psql`, the variable `migration-dry-run.sh` already uses, and it names the throwaway CI database |
| Gate decisions applied | ADR-012 (bilingual master data), ADR-011 with OQ-006 (generic risk and issue scale), ADR-015 (three governance profiles), ADR-016 with OQ-013 (materiality bands — not seeded, §3.2) |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), Implementation Plan rows TASK-018, TASK-027, TASK-030, TASK-034, TASK-089, TASK-105, TASK-106, TASK-110, as read 2026-09-26 |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | Idempotent seed of the eight canonical roles and the master data the platform ships; a validation script for referential integrity, orphans and constraint violations; running both after every migration in CI and in each environment | This task |
| **In** | Retiring the local stack's interim roles script (`infra/docker/postgres/seed/01-seed-roles.sql`, which TASK-014 wrote "until TASK-027") | This task |
| **Out** | Values AHDA has not given: item lists of most catalogues, rating labels and the 5×5 matrix, band values, profile settings (§3.2) | AHDA, through FG-04 (TASK-034) once the values arrive |
| **Out** | Permission grants on the shipped-default profiles | TASK-030, TASK-110 |
| **Out** | Local test users. They stay local-only in `infra/docker/postgres/seed/seed-local-users.sql` and are never promoted | TASK-014 |
| **Out** | Legacy data migration and the up/down/up validation at production volume | TASK-089 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **The two SQL files in `db/seed` are the only source, and they are pure SQL**: no psql meta-command, no variable. | The same bytes run everywhere: `psql` locally and in Compose, the release image in CI and in each environment. A psql-only script could not run in an environment (D-2); a C# seed would put master data in code, where a reviewer of the data cannot read it. |
| D-2 | **In an environment, the release image runs them**: `dotnet PMPlatform.Api.dll seed` and `… validate-data-integrity`, executed on the migration job with the container's arguments overridden. The files are embedded in `PMPlatform.Infrastructure`. | The database has no public IP, so nothing can reach it from the runner (TASK-024 D-5, CTL-49). The migration job already has the identity, VPC path and secret store. Embedding means the script that runs is exactly the release's. Overriding arguments reuses the job rather than adding two Terraform jobs with the same configuration. |
| D-3 | **Idempotency: the seed owns structure, AHDA owns wording.** Every insert is keyed on the business key (`code`, or the table's unique key). On conflict the seed restores structural columns the platform depends on (`is_system`, a role's `is_external_eligible`, a profile's `base_role_id` and `is_shipped_default`), and only when they differ. Labels, sort order and configuration rows are inserted once and never updated. | A deployment must never revert a label AHDA corrected on ADM-020–029. ERD §5.3 treats a label correction as an audited edit. The `WHERE … IS DISTINCT FROM` guard means a second run writes nothing, so `updated_at` and the audit trail do not churn. The workbook's "idempotent upsert pattern" holds: no duplicate row, and no change on re-run. |
| D-4 | **Deterministic ids**: `md5('<table>:<business key>')::uuid` for new rows. Roles, profiles and versions keep the ids the local stack has used since TASK-014. | The same row has the same id in every environment, so environments can be compared and support queries can be shared. Rows match on the business key, not the id, so an existing database whose rows have other ids (the local volumes) upgrades in place (§5 row 8). |
| D-5 | **The risk and issue scale is seeded as a DRAFT configuration version.** Version 1 of `RISK_MATRIX` holds five probability levels and five impact levels for each ADR-011 dimension, labelled "Level n" / "المستوى n", with no description or boundary. Levels are added only while that version is DRAFT. | This is what "seeded generically" can mean without inventing a value (OQ-006). Resolution reads PUBLISHED versions and fails closed without one (TASK-034), so nothing resolves against an unapproved scale. AHDA fills in the version on FG-04 and publishes it; the seed never touches it again. ADR-011 uses the same dimensions for risks and issues, so one set of impact levels serves both. |
| D-6 | **The validator checks the database's own catalogue, not a list of tables.** Every foreign key, CHECK constraint, index and audited table in every non-system schema is enumerated from `pg_catalog` on each run. | A table added by a later migration is covered without editing the script. The one list it keeps is the catalogue map (D-7), because that fact exists nowhere in the schema. |
| D-7 | **Two checks go beyond what constraints enforce**: audit actors (`created_by`/`updated_by` have no FK by design, ERD D-2) and the catalogue of a master data reference (an FK to `master_data_item` cannot tell a region from a governance profile). The map covers all 37 master data references in `erd.dbml`. A reference the map lacks is itself a violation. | These are the orphans and wrong references the database cannot refuse. Failing on an unmapped reference makes a module's migration add its catalogue in the same pull request. The scratch-database step fails first, so this never reaches a deployment. |
| D-8 | **The validator fails with SQLSTATE 23000 and lists every violation in the message**, not in `DETAIL`. | psql exits 3 and the release image exits non-zero, which is what fails the pipeline step. Npgsql hides `DETAIL` unless the connection string opts in, so the list goes in the message, where both runners print it. |
| D-9 | **Order: `migrate` → `seed` → `validate-data-integrity`**, in CI and in each environment, each a separate step that fails the deployment. | The workbook asks for validation "after each migration or data load". One validation after both covers both, and a violation in either stops the deploy before `terraform apply`. |

## 3. What is seeded

### 3.1 Rows

| Table | Rows | Content | Source |
| --- | --- | --- | --- |
| `identity_access.user` | 1 | `svc.platform-seed`, the SERVICE principal every seeded row is attributed to (ERD D-2). No role, no directory subject, address in `.invalid` | ERD `user.user_type` |
| `identity_access.role` | 8 | R01–R08, `is_system`; R04 and R08 `is_external_eligible` | ERD F-080; ADR-013. Labels provisional (F-1) |
| `identity_access.permission_profile` | 8 | `R0n-DEFAULT`, `is_shipped_default` | ADR-018, TASK-110 |
| `identity_access.permission_profile_version` | 8 | Version 1 of each, PUBLISHED, no grants | ADR-018 |
| `master_data_config.master_data_catalogue` | 21 | One per master data reference in the ERD (list below) | ERD; ADM-020–029 |
| `master_data_config.master_data_item` | 10 | `EXTERNAL_ENTITY_TYPE`: GOVERNMENT, PUBLIC_AUTHORITY, PRIVATE_COMPANY. `GOVERNANCE_PROFILE`: LIGHT, STANDARD, FULL. `IMPACT_DIMENSION`: COST, SCHEDULE, REPUTATION, OPERATIONAL. All PUBLISHED, `is_system` | ERD `external_entity`; ADR-015; ADR-011 |
| `master_data_config.configuration_family` | 12 | The twelve families of the ERD | ERD `configuration_family` |
| `master_data_config.configuration_version` | 1 | `RISK_MATRIX` version 1, DRAFT | D-5 |
| `master_data_config.probability_level_definition` | 5 | Levels 1–5, generic labels, no boundaries | ADR-011, OQ-006 |
| `master_data_config.impact_level_definition` | 20 | Levels 1–5 × four dimensions, generic labels, no descriptions or boundaries | ADR-011, OQ-006 |

The 21 catalogues: `EXTERNAL_ENTITY_TYPE`, `GOVERNANCE_PROFILE`, `IMPACT_DIMENSION`, `DATA_CLASSIFICATION`, `DOCUMENT_CONTROL_LEVEL`, `PROJECT_CLASSIFICATION`, `REGION`, `CITY`, `WORKING_CALENDAR`, `MILESTONE_CATEGORY`, `EVIDENCE_TYPE`, `DOCUMENT_TYPE`, `PRIORITY`, `RISK_CATEGORY`, `CONCERN_CATEGORY`, `CONCERN_SEVERITY`, `CONTRIBUTION_TYPE`, `UPDATE_REQUEST_TYPE`, `ETIMAD_COST_CATEGORY`, `KPI_UNIT`, `MEASUREMENT_FREQUENCY`. Seven are named by the ERD. The others are derived from the reference columns' names (F-7).

Every one of the 84 labels is bilingual: the Arabic label contains Arabic script, the English label contains none (`EverySeededLabelIsBilingual`). The Arabic wording is the delivery team's (F-8).

### 3.2 Not seeded, and why

| Subject | Why not | What unblocks it |
| --- | --- | --- |
| Materiality bands (ADR-016) | Band values are OQ-013's, and OQ-013 has no row in the Open Questions sheet yet (`ptbc-tbc-tracker.md` 8.3). The `MATERIALITY_BAND` family is seeded; no version or band is | OQ-013 answered → a DRAFT `MATERIALITY_BAND` version per profile, added here the same way as D-5 |
| Governance profile settings (ADR-015) | Every `governance_profile_setting` column is `NOT NULL`, and only two values are stated (baseline approval and risk management: off for Light, on for Standard and Full; TASK-046, TASK-055). Update cadence, band count and document control level are not stated anywhere; the assignment thresholds wait on OQ-014 | Those values → a `GOVERNANCE_PROFILE` version |
| Rating labels and the 5×5 mapping | OQ-006 (ADR-011 "OUTSTANDING") | AHDA completes the DRAFT version on FG-04 |
| Items of the other 18 catalogues (project classification, regions, cities, document types, Etimad categories, …) | AHDA's values, listed in Blueprint v2.0 ADM-020–029, which is not in the repository (`controlled-source-baseline.md` BP-2.0: "PENDING — not in repo/Drive") | The Blueprint in `docs/baseline/sources/`, or AHDA entering them on ADM-020–029 without a release |
| Data classification items | ADR-010 taxonomy outstanding | AHDA Cybersecurity |
| Project and record statuses | Not master data: every state column is an enum with a CHECK listing its values (`core-platform-schema.md` §3). The workbook's "statuses" is satisfied by the schema | — |

## 4. The validation script

| Check | Finds | Why the database alone does not stop it |
| --- | --- | --- |
| `ORPHANED_FOREIGN_KEY` | A row whose foreign key names no parent (MATCH SIMPLE: keys with a null column are skipped) | A load with triggers off (`session_replication_role = replica`, `pg_restore --disable-triggers`) or a constraint added `NOT VALID` |
| `CHECK_VIOLATION` | A row for which a CHECK is false | A constraint added `NOT VALID`, or dropped and re-added around a load |
| `INVALID_INDEX` | An index marked invalid or not ready; for a unique index, "its unique key is not enforced" | A failed `CREATE INDEX CONCURRENTLY` leaves one behind |
| `ORPHANED_AUDIT_ACTOR` | `created_by` or `updated_by` naming no user | ERD D-2: those columns have no FK |
| `WRONG_CATALOGUE` | A master data reference to another catalogue's item | The FK to `master_data_item` is satisfied |
| `UNMAPPED_MASTER_DATA_REFERENCE` | A column referencing `master_data_item` that the map does not cover | — (D-7) |

The script is read-only (`SET TRANSACTION READ ONLY`). On the current schema it checks 70 foreign keys, 43 CHECK constraints, 122 indexes, 34 audited tables and 17 master data references.

## 5. Verification

Local run on 2026-09-26: macOS, .NET SDK 10.0.401, PostgreSQL 17.11 in `AHDA-postgres`. Scratch databases were created on that server and dropped afterwards; no other container was started (the two `docker compose run --rm` containers belong to the stack's own services and were removed on exit).

| # | What | Command | Result |
| --- | --- | --- | --- |
| 1 | Build | `dotnet build src/backend --configuration Release -warnaserror` | 0 warnings, 0 errors |
| 2 | Unit tests | `dotnet test src/backend/PMPlatform.Tests.Unit --configuration Release` | 39 passed |
| 3 | Persistence tests, 16 of them new | `DB_CONNECTION_STRING=… dotnet test src/backend/PMPlatform.Tests.Integration --filter FullyQualifiedName~PMPlatform.Tests.Integration.Persistence` | 110 passed, in 10 of 11 runs. One run failed a TASK-026 test (F-10) |
| 4 | **Workbook cell: seed twice, counts unchanged** | `psql … --single-transaction -f db/seed/seed-master-data.sql`, `database-fingerprint.sql` before and after a further run | Fingerprints identical: row counts and md5 of every row, `updated_at` included. `RunningTheSeedAgainChangesNoRow` asserts the same through the `seed` command |
| 5 | **Workbook cell: an orphan in a non-PROD copy is flagged** | Orphaned `department` inserted with `session_replication_role = replica` | `ORPHANED_FOREIGN_KEY identity_access.department fk_department_department_parent_department_id: 1 row(s) …`; psql exit 3; the `validate-data-integrity` command exits non-zero (134, the .NET unhandled-exception exit, as `migrate` does on failure) |
| 6 | All violation classes in one run | One of each injected in a rolled-back transaction | 5 violations listed, one line each; exit 3 |
| 7 | **The pipeline's steps, as the job runs them** | `migration-dry-run.sh`, then `seed-dry-run.sh`, on a fresh scratch database | `migration dry-run passed: 545 statement(s)…`; `seed dry-run passed: 10 table(s) seeded, unchanged by a second run, no integrity violation`. With a committed orphan: exit 1, the violation listed |
| 8 | Upgrade of an existing local volume (seeded by the TASK-014 scripts) | Validator read-only; then new seed + local users + validator in a rolled-back transaction | No violation before or after. Catalogues 1 → 21 and items 1 → 10: the existing `EXTERNAL_ENTITY_TYPE` and `PRIVATE_COMPANY` rows were matched by code, not duplicated |
| 9 | Compose `seed` service | `docker compose -f infra/docker/docker-compose.yml run --rm --no-deps seed`, twice; `verify-seed.sql` | Exit 0 both times, no violation; one active local user per R01–R08 |
| 10 | Release image | `docker compose … build migrate`; `docker compose … run --rm --no-deps migrate seed`, then `… validate-data-integrity` | Both exit 0; the image carries the embedded scripts |
| 11 | Environment script | `migrate-environment.sh` with `gcloud` stubbed to echo | `jobs update`, `jobs execute`, `jobs execute --args seed`, `jobs execute --args validate-data-integrity`, in that order. `--args` is a documented flag of `gcloud run jobs execute` (SDK 586) |
| 12 | Repository checks | all nine `docs/architecture/*-check.py` | 9 of 9 pass |

### 5.1 Mutation tests: each check fails when it should

| # | Mutation | Expected to fail | Result |
| --- | --- | --- | --- |
| M-1 | Role upsert without its `IS DISTINCT FROM` guard (rewrites every run) | `RunningTheSeedAgainChangesNoRow` | Failed; the other 15 passed |
| M-2 | Item upsert also overwrites `label_ar` | `ReseedingKeepsAhdaWordingAndRestoresStructure` | Failed; the other 15 passed |
| M-3 | One catalogue's Arabic name written in English | `EverySeededLabelIsBilingual` | Failed; the other 15 passed |
| M-4 | Orphan check computes but never records a violation | both orphan cases and `TheValidateCommandFailsOnACommittedOrphan` | Those 3 failed; the other 13 passed |
| M-5 | One line (`project.city_item_id`) dropped from the catalogue map | `EveryErdMasterDataReferenceHasASeededCatalogue`, `ASeededDatabaseHasNoViolation`, and every injected case (each now reports two violations) | 9 failed, as expected |

Each mutation was reverted; the committed files are byte-identical to the unmutated copies.

## 6. Acceptance criteria, validation and gate decisions

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Seed scripts can be run repeatedly with no duplicate rows (idempotent upsert pattern) | **MET.** A second run adds no row and changes no row, on a fresh database and on an existing local volume | §5 rows 4, 7, 8, 9; `RunningTheSeedAgainChangesNoRow`; M-1 |
| 2 | The validation script exits non-zero on any orphaned foreign key or constraint violation | **MET.** SQLSTATE 23000; psql exit 3; the release image's command exits non-zero | §5 rows 5, 6; `DataIntegrityTests` (7 cases); M-4 |
| 3 | …and is wired into the CI/CD pipeline as a post-migration step | **MET in the pipeline; not yet executed in an environment.** `migration-dry-run` runs it after migrating its scratch database, and it is a need of `deploy-dev`. Each deploy stage runs it after `migrate` (D-9). No environment exists (F-5) | `ci-cd-pipeline.yml`; `migrate-environment.sh`; §5 rows 7, 11 |
| — | Validation: run the seed twice; row counts unchanged on the second run | **MET** | §5 row 4 |
| — | Validation: insert an orphan in a non-PROD copy; the script flags it | **MET** | §5 row 5 |
| — | ADR-012: seed data bilingual for all controlled master data | **MET.** 84 of 84 labels | `EverySeededLabelIsBilingual`; M-3 |
| — | OQ-006: risk and issue scales seeded generically | **MET.** Five levels per scale, generic labels, no boundaries, DRAFT | `TheRiskAndIssueScaleIsGenericAndUnpublished` |
| — | ADR-015: seed data includes the three governance profiles | **MET** for the profiles. Their settings are not seeded (§3.2) | `TheThreeGovernanceProfilesArePublished` |
| — | ADR-016: materiality bands once OQ-013 is answered | **NOT YET APPLICABLE.** OQ-013 is unanswered, so no band is seeded, and the test asserts none is | §3.2; `TheRiskAndIssueScaleIsGenericAndUnpublished` |

## 7. Findings

| # | Finding | Owner | Consequence if left |
| --- | --- | --- | --- |
| F-1 | **Role labels are provisional.** Blueprint Appendix A is not in the repository; the labels follow the role dashboards DSH-001–008, as TASK-014's interim seed did. Under D-3 the seed will not overwrite them. Correcting them is a one-off data change, or an edit on the role administration screen | PMO (Appendix A); TASK-031 | Screens show a label the Blueprint may word differently |
| F-2 | **18 catalogues ship empty** (§3.2). Forms that need a classification, region or document type have nothing to offer until AHDA enters values or the Blueprint's ADM-020–029 lists are supplied | PMO: place Blueprint v2.0 in `docs/baseline/sources/` (TASK-001 5.1); AHDA master data owners | Registration (TASK-041) cannot complete on a fresh environment without an administrator entering items first |
| F-3 | **Governance profile settings are not seeded** (§3.2). TASK-105's lifecycle has nothing to resolve until a `GOVERNANCE_PROFILE` version exists | PMO: add the cadence, band count and document control level per profile to ADR-015; OQ-014 for thresholds | TASK-105 fails closed (by design) until AHDA publishes one |
| F-4 | **The deploy account needs `run.jobs.runWithOverrides`** to execute the migration job with `--args`. It is in `roles/run.developer` and `roles/run.admin`. The deploy account is created outside this repository (`cicd-pipeline.md` §4.3), so this cannot be checked here | DevOps/Platform Lead, at first environment | The first deployment stops at `seed` with a permission error, after `migrate` succeeded |
| F-5 | **Not run in an environment.** No GCP environment exists; provisioning waits on AHDA's answers to the ADR-001 confirmation request. The first deployment's "Run database migrations" step is this task's live check | DevOps/Platform Lead, with TASK-024 F-1 | — |
| F-6 | **OQ-013 has no row** in the Open Questions sheet, yet this task's amendment waits on it (`ptbc-tbc-tracker.md` 8.3) | PMO | The ADR-016 half of the amendment has no question to track |
| F-7 | **Fourteen catalogue codes are derived here, not named by the ERD**: `DOCUMENT_CONTROL_LEVEL`, `REGION`, `CITY`, `WORKING_CALENDAR`, `MILESTONE_CATEGORY`, `EVIDENCE_TYPE`, `DOCUMENT_TYPE`, `PRIORITY` (shared by tasks and issues), `RISK_CATEGORY`, `CONCERN_CATEGORY`, `CONCERN_SEVERITY`, `UPDATE_REQUEST_TYPE`, `MEASUREMENT_FREQUENCY`, `EXTERNAL_ENTITY_TYPE`. `REGION` and `CITY` are separate catalogues; if the ERD adopts `core-platform-schema.md` N-1 option (a), a city's parent is a region in another catalogue | Engagement Architect (ERD re-issue: name the catalogue on every `*_item_id` note) | A module that expects a different code must rename it in both `db/seed` files, which `EveryErdMasterDataReferenceHasASeededCatalogue` enforces |
| F-8 | **Arabic wording is the delivery team's.** ADR-012 is approved by the delivery team, pending AHDA. AHDA can reword any label on ADM-020–029 without a release, and D-3 keeps that edit | AHDA PMO | None beyond the wording itself |
| F-9 | **The validator scans every table once per constraint.** On the seeded databases above that is instant; it was not timed at the TASK-026 target volume (3,000 projects). `audit_activity.audit_event` at years of volume will not be small | TASK-073 (audit), when it lands | Deployment time grows with the audit table; exclude it or check it incrementally then |
| F-10 | **An intermittent failure in TASK-026's `RegisterQueryPlanTests`** (`SCR-081 critical count`): 1 failure in 11 full runs of the Persistence suite, not reproduced in 5 runs of that class alone or 9 further full runs. The failure message was not captured. The test holds a known full scan to a 50 ms ceiling against a 16 ms baseline, so the extra parallel load from this task's two test classes is a plausible cause, not a confirmed one | Database (TASK-026 owner) | An occasional red `backend` check on an unrelated pull request |
| F-11 | The TASK-023 drill scripts (`rehearse-restore.sh`, `run-restore-drill.sh`) now run the image's `seed` after `migrate`. They were syntax-checked, **not re-run**, to avoid creating containers outside the AHDA stack | DevOps (TASK-023) | The next drill is the first on this seed |

## 8. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-26 | Initial record. Platform seed (8 roles with shipped-default profile versions, 21 catalogues, 10 items, 12 configuration families, a generic DRAFT risk scale) and a six-check integrity validator in `db/seed`; `seed` and `validate-data-integrity` commands in the release image; scratch-database step in `migration-dry-run` and post-migration executions in every deploy stage; Compose seeds and validates in one transaction; interim roles script retired. 16 tests, five mutation groups. Eleven findings. | Database (TASK-027) |

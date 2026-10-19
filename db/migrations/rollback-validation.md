# Migration up/down validation report

| Field | Value |
| --- | --- |
| Task | TASK-089 — Validate Data Migration, Rollback & Seed Integrity (P15 - Testing & QA, P0) |
| Depends on | TASK-024 — migration framework (`docs/architecture/database-migrations.md`); TASK-027 — seed and data-integrity validation (`docs/architecture/seed-data-and-integrity.md`) |
| Record date | 2026-10-19 |
| Status | **Criteria 1 and 2 MET on the local stand-in; criterion 3 BLOCKED (OQ-008).** Every one of the 76 migrations was rolled back and re-applied with the TASK-026 representative volume loaded, through the release image's `migrate` command. The schema checksum after down and up again is identical to the first one, and no rollback changed data outside the tables and columns its own migration created. The run is on PostgreSQL 17 in Docker, not in a non-PROD environment: none exists (F-2). The rehearsal found one defect, fixed here: a failing `migrate` hung its container instead of exiting (F-1). The legacy-data plan is logged as Blocked in `legacy-data-migration.md` |
| Branch | `test/task-089-migration-rollback-validation` |
| Deliverables | **Migration up/down validation report:** this record, with the recorded run in `validation/results/2026-10-19-full-cycle.json`. **Legacy-data migration plan:** Blocked status record, `legacy-data-migration.md`. The harness: `validation/run.sh`, `validation/rollback_validation.py`, `validation/schema-snapshot.sql` (now also read by `MigrationRollbackTests`); `.github/workflows/migration-rollback-validation.yml`. The fix for F-1 in `src/backend/PMPlatform.Api/Program.cs` |
| Environment variables / secrets | `DB_CONNECTION_STRING`: the server the harness creates its throwaway databases on (Npgsql format; only server and credentials are used, as in the integration tests' `ThrowawayDatabase`) and the value passed to each `migrate` run. `run.sh` sets it to the PostgreSQL it starts; nothing is committed and nothing is added to the sheet |
| Workbook read | Local copy `AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`, sheets Implementation Plan (TASK-089, TASK-102), Open Questions (OQ-008), Release Checklist, Environment and Secrets, read 2026-10-19 |

## 1. Scope

The workbook asks for the full migration up/down cycle "against a production-representative data volume". The validation
cell: "Run migrate-up, migrate-down, migrate-up again against a scratch database and diff schema checksums at each step;
confirm zero unintended data loss in a rollback rehearsal".

TASK-024 already rolls every migration back on every pull request (`MigrationRollbackTests`), but on an **empty**
database. An empty table accepts almost any Down. Re-adding a `NOT NULL` column without a default works on an empty table
and fails on a table with rows. A Down that deletes or rewrites rows deletes and rewrites nothing on an empty table. Both
kinds of defect stay invisible until a rollback runs against real data (§5, P1, P2 and P4). This task adds the data.

| | Subject | Where |
| --- | --- | --- |
| **In** | Every migration rolled back and re-applied at representative volume; a schema checksum at every state; data compared column by column at every step; a rehearsal of rolling back the most recent migration; a check that runs on every pull request that adds migrations | This record, `validation/` |
| **In** | Defects the rehearsal finds in the rollback path | F-1, fixed |
| **Out** | The rehearsal in a real non-PROD environment | Owed when SIT exists (F-2) |
| **Out** | The legacy-data migration plan | Blocked on OQ-008 (`legacy-data-migration.md`) |
| **Out** | The legacy cutover and its reconciliation | TASK-102 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **The volume is the load test's** (`docs/testing/load-test/volume/*.sql`, TASK-086): the platform seed, the e2e data set, then 3,000 projects with two years of history, **1,863,483 rows in 122 tables, 874 MiB**, loaded into the real module tables and checked by `validate-data-integrity.sql`. | It is the TASK-026 target volume (`indexing-strategy.md` §5.2), already reviewed as production-representative, and it is loaded through the same chain the load stack uses. A second generator would have to be kept in step with every new module. |
| D-2 | **Migrations run through the release image**, `dotnet PMPlatform.Api.dll migrate [<target>]`, built from the tree (`infra/docker/api.Dockerfile`), as a fresh container per step. | This is the command each environment's migration job runs, and the one an operator uses to revert a schema (`Persistence/Migrations/README.md`, "In an environment"). Running it found F-1, which `dotnet ef` and the in-process tests could not have found. |
| D-3 | **The schema checksum is the SHA-256 of `schema-snapshot.sql`'s lines**, sorted by byte value and joined with `\n`. The query is the one `MigrationRollbackTests` used, moved unchanged into its own file, and `SchemaSnapshot.cs` now reads that file. | It covers schemas, relations, columns with type, nullability and default, constraints, indexes, functions (by body hash), triggers and enum labels. One definition means the pull-request tests and this run compare the same thing. On a mismatch the differing lines are printed, not just the checksum. |
| D-4 | **Each state is judged against a reference ladder**: a second database with no data, migrated up one migration at a time. A state at level *k* must have the reference's checksum for *k*, whether it was reached by Down from above with data in place or by Up from below. | Comparing the way down with the way up is not enough. A Down that leaves an index behind leaves it on both passes, and `IF NOT EXISTS` in the Up hides it again (P3). The reference is what Up builds from nothing, which is what `MigrationRollbackTests` compares against. |
| D-5 | **Data is fingerprinted per column**: for each table, the row count, and for each column the sum of a 64-bit hash (`hashtextextended`) of (primary key, value) over every row. | A step that drops a column changes every whole-row hash, so a whole-row fingerprint (`database-fingerprint.sql`) cannot tell "the Down dropped its own column" from "the Down rewrote my data". Per-column hashes keyed on the primary key can: every column present on both sides with the same type must keep its hash. The sum is order-independent, so no sort of 1.8 million rows is needed and a state is fingerprinted in about a second. |
| D-6 | **A step may lose only what its Down drops**: whole tables and whole columns, each listed in the results. Any other change in row count or in a surviving column's values fails the step. The history table is compared row by row instead: after each step it must hold exactly the migrations of that level. | This is the acceptance criterion's "without data loss for unaffected tables", made mechanical. A table the migration created holds rows only because the migration exists, so dropping it is the rollback's purpose, not a loss to an unaffected table. |
| D-7 | **A pull request rehearses the migrations it adds**: `migration-rollback-validation.yml` runs at a depth equal to the number of migrations added (at least 1) when migrations, `db/`, the volume or the e2e provision files change. `workflow_dispatch` runs any depth; `all` is the recorded run. | The newest migrations are the ones an environment would roll back if a release failed. The full cycle takes 13 minutes locally; running it on every pull request would add a long required wait for a result that only changes when a migration is added. |
| D-8 | **In a non-PROD environment the same comparison uses the restore runbook's tools**: fingerprint the database before (`docs/operations/verify-restore.sh fingerprint`), revert with the migration job, then `verify-restore.sh compare`. Only the history table and the tables the Down drops may differ (§6). | The databases have no public IP and no operator path exists yet (runbook BR-1), so the harness cannot reach them. The runbook's fingerprint is read-only and already approved for PROD. The full run's results list, per migration, the tables its Down drops, which is the expected difference. |

## 3. How to run it

```sh
db/migrations/validation/run.sh                 # full cycle: every migration down and up again, then reload (~13 min)
db/migrations/validation/run.sh --depth 2       # the two newest only (what a pull request adding two runs)
python3 db/migrations/validation/rollback_validation.py --summary artifacts/migration-validation/<run>/results.json
```

`run.sh` builds the release image, starts PostgreSQL 17 (`mirror.gcr.io/library/postgres:17`) and a psql client on a
Docker network of its own, runs the harness and removes everything on exit. Requires Docker and Python 3.11+. The steps,
for depth *d* of *n* migrations:

1. **Reference ladder**: a database with no data, migrated to level *n − d*, then up one migration at a time to *n*.
2. **Up and load**: a second database, migrated to the newest migration; seed, e2e data set and volume, then
   `validate-data-integrity.sql`, in one transaction.
3. **Most recent migration**: roll back, check, run the integrity validator; re-apply, check, run it again.
4. **Walk**: down *d* migrations one at a time with the data in place, then up again one at a time.
5. **Reload** (full cycle only): the seed, data set and volume again, into the re-applied schema.

Exit status 0 only when every check passes. A `migrate` that does not exit within 10 minutes fails the run (F-1).

## 4. Results

Recorded run, 2026-10-19 18:43–18:56 UTC, commit `59d814a`, PostgreSQL 17.11, 76 migrations
(`validation/results/2026-10-19-full-cycle.json`). Times are wall clock per `migrate` container, including about 1.5 s of
container and host start-up.

### 4.1 Up, down and up again: the schema checksum

| State | Checksum | Matches the reference ladder |
| --- | --- | --- |
| Up to `20261013164900_TASK-075_GuardIntegrationMonitoringHistory`, before and after the volume | `7f56c143b8a451b07adcdeb600fd49b09991427b7b26d7afe46094aa110d458c` | yes |
| After all 76 down, one at a time, with the data in place (level 0) | `61ae0f6295be9ec2ca2d81665e7ee426df3c012c9a842fae50f7eeb24a689aa1` | yes: `common` and the history table, as TASK-024 D-6 intends |
| After all 76 up again, one at a time | `7f56c143b8a451b07adcdeb600fd49b09991427b7b26d7afe46094aa110d458c` | yes: **identical to the first** |
| Every intermediate state | | **152 of 152** (76 down, 76 up) |

The 77 reference checksums, one per level, are the `checksums` list of the results file. They form a checksum registry
(Step 17 BE-027): a database at a given migration should have that checksum on PostgreSQL 17.

### 4.2 Rolling back the most recent migration

| Migration | Rollback | Unchanged | Dropped | Integrity validator | Re-apply | Result |
| --- | ---: | ---: | ---: | --- | ---: | --- |
| `20261013164900_TASK-075_GuardIntegrationMonitoringHistory` | 4.0 s | 122 tables, 1,863,483 rows | none | passes after rollback and after re-apply | 3.1 s | **pass** |

Its Down drops the guard triggers and functions it created and touches no data.

### 4.3 The walk at volume

| | Value |
| --- | --- |
| Down, one at a time | 76 steps, 164 s; median 1.9 s, slowest 6.5 s (`TASK-062_GuardSuspensionHistory`) |
| Up, one at a time | 76 steps, 199 s |
| Steps that lose no data | **50 of 76**: guards, triggers, foreign keys, indexes, seed-shaped changes. Every table and every column keeps its values |
| Steps that drop data | **26 of 76**, each dropping only the tables or columns its own Up created: 122 tables (71 held rows when dropped, 51 were empty) and two columns (`audit_activity.audit_event.attribute_digest`, TASK-073; `suspension.active_suspension.end_reason`, TASK-063). The largest: `TASK-039_CreateNotificationTables` (5 tables, 666,669 rows), `TASK-052_CreateFinancialKpiTables` (9, 323,038), `TASK-055_CreateRiskTables` (5, 225,000) |
| Data violations | **0**: no surviving table changed row count, no surviving column changed value, no column changed type |
| History | exactly the migrations of each level, at all 152 states |
| Reload into the re-applied schema | 1,863,483 rows in 33.1 s, the same count in every table, integrity validator passes |

The results file lists, for each migration, the tables and columns its Down drops and how many rows they held. That is the
expected difference for the environment procedure (§6), and the cost of reverting that migration in a database in use.

## 5. Negative controls

Each check was shown to fail when it should. Probe migrations were added after the newest migration in a separate
worktree, the image was rebuilt from it, and the harness and `MigrationRollbackTests` were run. No probe is committed. The
sources are kept in the git-ignored `artifacts/task-089/probes`.

| # | Planted defect | `MigrationRollbackTests` (empty database) | This harness (volume) |
| --- | --- | --- | --- |
| P1 | Down deletes 10 rows of `notifications.notification_delivery` | passes: no rows to delete | **fails**: `notifications.notification_delivery: 400000 rows before, 399990 after` |
| P2 | Down rewrites one project's title | passes: no rows to rewrite | **fails**: `project.project.title: values changed` |
| P3 | Down forgets the index its Up created (`CREATE INDEX IF NOT EXISTS`) | **fails** on both tests, naming `ix_probe_title` (the shared `schema-snapshot.sql` still catches it) | **fails** at every level from the rollback down and back up: checksum is not the reference, extra `index … ix_probe_title` |
| P4 | A contract migration whose Down re-adds a `NOT NULL` column and then its default | passes on both tests: an empty table takes the column | **fails**: the rollback cannot run, `column "probe_code" of relation "project" contains null values` |
| — | P1–P3 together, depth 3 | | 26 failures reported, exit 1 |
| — | The unmodified tree (§4) | passes | passes, exit 0 |

P4 also exposed F-1. Before the fix, the failing `migrate` container did not exit (§7).

## 6. Rolling back in a non-PROD environment

The procedure the acceptance criterion names, for when SIT exists. It adds two steps to the authoring guide's "Rolling back a
schema" (`src/backend/PMPlatform.Infrastructure/Persistence/Migrations/README.md`). It needs a drill host in the
environment's VPC (runbook BR-1).

1. **Rehearse first.** `db/migrations/validation/run.sh --depth <k>` on the release being rolled back, where *k* is the
   number of migrations to revert. It must pass, and its results name the tables and columns each Down drops.
2. **Fingerprint.** From the drill host: `docs/operations/verify-restore.sh fingerprint "<connection>" > before.fp`.
3. **Revert.** `gcloud run jobs execute <resource_prefix>-migrate --args=migrate,<last migration ID to keep> --wait`
   with the image that is running now (only it contains the Down). After F-1, a failure ends the execution with exit
   status 1 and the reason in the job's log.
4. **Compare.** `docs/operations/verify-restore.sh compare before.fp "<connection>"`. Exit 1 is expected. The lines that differ
   must be the history table, the tables step 1 lists as dropped (now absent), and any table whose dropped column changes
   its whole-row hash. Any other differing table is data loss: stop and restore (runbook §5).
5. **Integrity.** Execute the job with `--args=validate-data-integrity`; it must succeed.
6. Record before.fp, the comparison and the job execution IDs in the release record.

## 7. Findings

| # | Finding | Owner | Status |
| --- | --- | --- | --- |
| F-1 | **A failing `migrate` never exited.** The release image runs `dotnet` as PID 1 with no init. An unhandled exception aborts the runtime, the kernel ignores the abort signal for PID 1, and the process spun at full CPU indefinitely. Reproduced with `migrate NoSuchMigration`: still running at 99% CPU after 30 s and, in P4, after 10 minutes. With `docker run --init` the same command exits 134 at once. The same applied to `seed` and `validate-data-integrity`, which rethrow, so a data-integrity violation in a deployment would have held the job until its 30-minute timeout instead of failing the step. TASK-024 §4 row 8 recorded the message for an unknown target but not the exit. **Fixed:** every one-shot command now catches the failure, writes it to stderr and exits 1 (`Program.cs`, `RunToCompletionAsync`); P4 now ends in seconds with exit 1. The harness gives each `migrate` a 10-minute limit, so a regression fails the run instead of stalling it | Testing (TASK-089) | **Fixed** |
| F-2 | **Not rehearsed in a non-PROD environment.** No environment exists (ADR-001 R-1 to R-3; TASK-024 F-1) and no operator path into a VPC exists (runbook BR-1). Criterion 2 is met on the local stand-in, with the same image and command an environment would use. The §6 procedure is owed on SIT, and its first execution is this criterion's live check | DevOps/Platform Lead, at SIT | Open |
| F-3 | **51 of the 122 tables were empty when their migration was rolled back.** The load test's volume does not load external participation, audit, delegation or report history (`load-test.md` §4.4), and several other tables are written only by workflows. The Downs of those migrations ran against no rows. Their surviving neighbours held data, so the data rule was still exercised at those steps. Adding a volume file for a module extends this check with no change to the harness | Module owners, with the load test (TASK-086) | Open, low |
| F-4 | **The PID 1 behaviour in Cloud Run is unverified.** F-1 is fixed for the one-shot commands whatever the platform does. If the API **service** itself fails at start-up (an unhandled exception before it serves), it still aborts, and in Docker it would spin rather than restart. Running the image under an init (`tini`, or `--init` where the platform offers it) would cover every path. That changes the shipped entrypoint and belongs with the image's owner | DevOps/Platform Lead (TASK-014 image, TASK-018 pipeline) | Open |
| F-5 | **The checksum is PostgreSQL-version specific.** `pg_get_indexdef`, `pg_get_constraintdef` and `format_type` render text that can change between major versions, so the registry (§4.1) holds for PostgreSQL 17, the version ADR-002 and TASK-020 fix. Re-record the full run after a major upgrade | Database | Noted |
| F-6 | **Rolling back a table-creating migration in a database in use loses that table's rows**, by design (26 of 76 migrations). The guide already says to revert a schema only when the migration itself is the fault, and expand-then-contract (R-6) lets the previous release run on the newer schema. The results file now tells the operator what each revert would drop before doing it | — | Noted |

## 8. Acceptance criteria

| Criterion | Status | Evidence |
| --- | --- | --- |
| Migration up-then-down-then-up-again reproduces an identical schema checksum | **MET.** `7f56c143…` up, all 76 down and all 76 up again with the volume in place, `7f56c143…` again; every one of the 152 intermediate states equals the data-free reference ladder | §4.1; P3 shows a leftover object fails it |
| A rollback of the most recent migration in a non-PROD environment completes without data loss for unaffected tables | **MET on the local stand-in; the environment run is owed (F-2).** Rolled back in 4.0 s at volume, 122 tables and 1,863,483 rows unchanged, integrity validator passing; re-applied with nothing changed. Each of the other 75 rollbacks changed no data outside the tables and columns its own migration created | §4.2, §4.3; P1, P2 and P4 show the check fails; §6 for the environment |
| The legacy-data migration plan is documented once AHDA confirms source system details, or explicitly logged as Blocked until then | **Logged as Blocked** on OQ-008, with the twelve questions that lift it and the controlled requirements the plan must meet | `legacy-data-migration.md` |
| Validation cell: up, down, up on a scratch database, diff checksums at each step; zero unintended data loss in a rollback rehearsal | **MET** as above. The checksum is taken and compared at every step, and differences are printed line by line | §4, §5 |

The Release Checklist gate "Data Migration Cutover Rehearsed" needs this report **and** a rehearsed legacy cutover
reconciliation. The second waits for OQ-008, so the gate stays Not Started.

## 9. Verification

Local, 2026-10-19: macOS, Docker 29.8, .NET SDK 10, PostgreSQL 17.11 (Docker), Python 3.14.

| # | What | Command | Result |
| --- | --- | --- | --- |
| 1 | Full cycle at volume | `db/migrations/validation/run.sh` | PASS, exit 0, 13 min (§4) |
| 2 | Trial at depth 1, before the reference ladder was added | `run.sh --depth 1` | PASS |
| 3 | Negative controls P1–P4 | §5 | each reported as listed; exit 1 |
| 4 | F-1 before the fix | `docker run … migrate NoSuchMigration` (no `--init`); then with `--init` | still running at 99% CPU after 30 s; with `--init`, exited 134 |
| 5 | F-1 after the fix | P4 through the rebuilt image | `migrate failed: … contains null values`, exit 1 within seconds |
| 6 | Build | `dotnet build src/backend -c Release -warnaserror` | 0 warnings, 0 errors |
| 7 | Unit tests | `dotnet test src/backend/PMPlatform.Tests.Unit -c Release` | 1,911 passed |
| 8 | Rollback, integrity and seed tests, reading the shared `schema-snapshot.sql` | `dotnet test … --filter MigrationRollbackTests\|DataIntegrityTests\|SeedDataTests` | 19 passed |
| 9 | Repository checks | `test-strategy-check.py`, `cicd-pipeline-check.py`, `secret-and-dependency-scanning-check.py`, `database-check.py`; `migration-forward-only.sh` against `origin/dev` | all exit 0; "no migration on origin/dev was changed; 0 added" |
| 10 | Workflow lint | `actionlint` 1.7.7 on `migration-rollback-validation.yml` | no findings |
| 11 | Full cycle again, on the PR head (`ff817ba`) | `run.sh` | PASS, exit 0: same checksum `7f56c143…`, 152 of 152 states on the reference ladder, 0 data violations, newest rolled back in 4.5 s with 122 tables unchanged |
| 12 | **Independent check**, sharing no code with the harness: the same release image's `migrate`; schema checksum = SHA-256 of `pg_dump --schema-only` (17.11, `\restrict` lines dropped); data judged by `docs/operations/verify-restore.sh` (TASK-023) | local script, git-ignored (`artifacts/task-089/independent-check.sh`) | **Empty database:** up `d4a08f31…`, down to 0, up again `d4a08f31…`, identical. **With the volume** (1,863,559 rows incl. history, 877 MB): loading leaves the checksum `d4a08f31…`. Rolling back the newest migration changes the checksum (173 dump lines), and the fingerprint diff shows **only** `common.__EFMigrationsHistory` 76 → 75; the integrity validator passes. Re-applying restores `d4a08f31…`, and `verify-restore.sh compare` against the pre-rollback fingerprint exits 0: every table, sequence and object is identical. Down to 0 at volume equals down to 0 when empty (`727747db…`); up again gives `d4a08f31…` |

## 10. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-10-19 | Initial record. Harness, recorded full cycle (76 migrations at 1.86 M rows: checksum identical, 152 of 152 states on the reference ladder, 0 data violations), four negative controls, pull-request workflow. F-1 found and fixed. Legacy plan logged as Blocked on OQ-008 | Testing (TASK-089) |

# Database migration framework and conventions

| Field | Value |
| --- | --- |
| Task | TASK-024 — Establish Database Migration Framework & Conventions (P4 - Database Foundation) |
| Depends on | TASK-008 — canonical ERD (`erd.md`, `erd.dbml`); TASK-020 — managed PostgreSQL (`database-provisioning.md`). The workbook titles TASK-020 "AlloyDB"; ADR-002 §4.2.3 settled it as Cloud SQL for PostgreSQL 17 |
| Record date | 2026-09-25 |
| Status | **BUILT — VERIFIED LOCALLY, NOT APPLIED TO AN ENVIRONMENT.** The framework, the baseline migration, the `migrate` command and its Cloud Run job are written. Migrations apply, roll back and re-apply on PostgreSQL 17, and the checks that enforce the conventions were each shown to fail on a violation (§4). No GCP environment exists yet, so the job has not run in one (§6 F-1) |
| Branch | `feat/task-024-migration-framework` |
| Deliverables | **Framework configuration:** `PMPlatformDbContext`, `DatabaseOptions`, `PMPlatformDbContextFactory`, `DatabaseMigration` (`src/backend/PMPlatform.Infrastructure/Persistence/`); the baseline migration `20260925151339_TASK-024_CreateModuleSchemas`; `.config/dotnet-tools.json`; `google_cloud_run_v2_job.migrate` (`infra/terraform/compute`); `.github/scripts/migration-forward-only.sh`. `.editorconfig` in the API image build (F-6). **Tests:** `MigrationConventionTests` (unit) and `MigrationRollbackTests` (PostgreSQL), both run by the `backend` gate. **Authoring guide:** `src/backend/PMPlatform.Infrastructure/Persistence/Migrations/README.md`. **This record** |
| Environment variables / secrets | `DB_CONNECTION_STRING`. Read by the API and by the `migrate` command through the existing configuration and secret-store path (TASK-019), and by `dotnet ef` from the process environment. Nothing new is added to the sheet, and no value is committed: the CI value names a throwaway service container |
| Implements | ADR-003 L-3 (EF Core and migrations only in Infrastructure) and M-13 (one schema per module); ERD D-4 (snake_case) and D-17 (history table outside the module schemas); CTL-37 (expand-then-contract, via the TASK-018 dry-run); closes the job and entrypoint half of `cicd-pipeline.md` F-2 |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), Implementation Plan rows TASK-008, TASK-020, TASK-023, TASK-024 and TASK-025, as read 2026-09-25 |

## 1. Scope

TASK-024 builds the machinery; TASK-025 writes the first tables. The workbook gives TASK-025 "Initial
schema migration for Project/Org/User/MasterData tables", so this task creates no business table. It
provides:

- the tool and its configuration: EF Core 10 migrations on Npgsql, one context, one history;
- the conventions (naming, rollback, forward-only), each enforced by a check, not by review alone;
- a path that applies migrations in an environment, which TASK-018 left to this task (`cicd-pipeline.md` F-2);
- one migration, the baseline, which creates the 21 module schemas that every later migration writes into.

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **EF Core Migrations, one `DbContext`, one history.** `PMPlatformDbContext` picks up every `IEntityTypeConfiguration<T>` in Infrastructure; each module maps its tables into its own schema. | The acceptance criterion is a single `dotnet ef database update` that reproduces the full schema. One history per module (21 contexts) would need 21 updates and would allow a partial platform schema. Module ownership is enforced by schema (M-13), not by history. `erd.md` §11 suggested "migrations generated per module schema, `common` first". Per-module *schemas* are kept; per-module migration *streams* are not. |
| D-2 | **The ID is `<timestamp>_<TaskID>_<Description>`**, e.g. `20260925151339_TASK-024_CreateModuleSchemas`, produced by `dotnet ef migrations add TASK-024_CreateModuleSchemas`. | EF Core keeps the hyphen in the ID and drops it only from the class name (`TASK024_CreateModuleSchemas`), so the ID is exactly the workbook's format and can be searched for alongside the task. The timestamp comes first, so ID order is application order. |
| D-3 | **The history table is `common."__EFMigrationsHistory"`**, with snake_case columns `migration_id` and `product_version`. | ERD D-17 puts it outside the module schemas. The snake_case naming convention (ERD D-4) also applies to the history table. That is consistent because the API, `dotnet ef` and the tests all take their options from `DatabaseOptions`. Removing the convention would orphan the history, so the convention and the history table are configured in one place. |
| D-4 | **Infrastructure is its own startup project.** `PMPlatformDbContextFactory` builds the context for `dotnet ef`, and `Microsoft.EntityFrameworkCore.Design` is `PrivateAssets="all"` in Infrastructure. | Tooling needs no API host, secret store or database, and the design package (with Roslyn and MSBuild) never enters the API image: a Release publish contains the EF Core runtime and no `*.Design`, `Microsoft.CodeAnalysis.*` or `Microsoft.Build.*` assembly (§4). `migration-dry-run.sh` changed its default startup project to match. |
| D-5 | **The release image migrates itself:** `dotnet PMPlatform.Api.dll migrate [<target>]` builds the host, applies (or, given a target, migrates up or down to) the migrations and exits without serving. | The databases have no public IP (TASK-020), so migrations cannot run from the runner. Running the release image means the migrations applied are exactly the release's, and it reads `DB_CONNECTION_STRING` from the secret store with the runtime identity, like the service. An EF bundle would be a second artifact with a second configuration path. EF Core 9+ takes a database lock while migrating, so two concurrent runs cannot interleave. |
| D-6 | **The baseline creates the 21 module schemas and not `common`.** `Down` drops them with plain `DROP SCHEMA`, not `CASCADE`. | Schema names come from `erd.dbml` and equal the modules in `ModuleRegistry`, snake-cased (checked by `MigrationsCreateExactlyTheModuleSchemas`). EF Core creates `common` for the history table, and no `Down` could drop a schema that still holds the history. Without `CASCADE`, a rollback fails loudly when an object is left behind instead of destroying it. |
| D-7 | **Rollback is tested on every pull request, against PostgreSQL 17.** `backend` gains a service container. `MigrationRollbackTests` applies each migration from empty, reverts each one singly, checks that schema and history equal what they were before it, and re-applies. A second test runs the workbook's cell as written: roll back the last three in one step, re-apply, compare. | "Every migration has a tested rollback path" holds only if every migration's `Down` is executed. Comparing the whole catalogue (schemas, relations, columns, constraints, indexes, functions, triggers, enum labels) catches a partial `Down`; checking that `DownOperations` is non-empty does not. |
| D-8 | **Forward-only is enforced by diff.** `migration-forward-only.sh` fails a pull request that modifies, deletes or renames a migration already on the base branch, or adds one whose ID sorts before the base's newest. | A database never re-runs a migration whose ID it has recorded, so an edited migration changes the code and no database. An out-of-order ID would run after migrations written without it. The model snapshot is exempt because every `migrations add` rewrites it. |

## 3. How it fits together

### 3.1 Authoring

The guide (`Persistence/Migrations/README.md`) states eight rules (R-1 to R-8), the commands, and how a
rollback is done in an environment. The checks behind the rules:

| Rule | Check | Where it runs |
| --- | --- | --- |
| ID names its task | `EveryMigrationIdNamesItsTask`: `^\d{14}_TASK-\d{3}_[A-Z][A-Za-z0-9]*$`, timestamp a real date | `backend` (unit tests) |
| Up and Down both exist | `EveryMigrationHasAnUpAndADown` | `backend` |
| Down is correct | `MigrationRollbackTests` (2 tests) | `backend` (PostgreSQL 17 service) |
| No model change without a migration | `TheModelHasNoChangeWithoutAMigration` (`HasPendingModelChanges`) | `backend` |
| One schema per module | `MigrationsCreateExactlyTheModuleSchemas` | `backend` |
| History table in `common` | `TheHistoryTableIsInTheCommonSchema` | `backend` |
| Forward-only, in order | `migration-forward-only.sh` | `repo-checks` (pull requests) |
| Expand-then-contract; script idempotent | `migration-dry-run.sh` (TASK-018, unchanged in substance) | `ci-cd-pipeline` → `migration-dry-run` |

### 3.2 From empty

`dotnet ef database update` creates `common`, the history table and the 21 module schemas, and records
one row. §4 shows the result.

### 3.3 The history table and the backup scope

The history table is an ordinary table in the application database. Every automated backup and every
PITR restore of the Cloud SQL instance (TASK-020) therefore includes it, with nothing to configure. The
restore runbook verifies that it came back. `database-fingerprint.sql` covers every table in every
non-system schema, and on a migrated database it prints `table|common.__EFMigrationsHistory|1|<md5>`
(§4). V-4 (`backup-restore-dr-runbook.md` §6.1) now names the query that compares it with the release in
service.

### 3.4 In an environment

`infra/terraform/compute` defines `google_cloud_run_v2_job.migrate`, named `<resource_prefix>-migrate`
(`ahda-pmplatform-migrate`) in every environment. Each environment is its own project, so one name is
safe, and the pipeline's single `MIGRATION_JOB` repository variable can name it. The job shares the
service's image, runtime service account, Direct VPC egress, network tag and plain environment
(`SECRET_STORE_ENDPOINT`), and runs with `args = ["migrate"]`, `max_retries = 0` and a 30-minute timeout.
The deploy sequence is unchanged (`cicd-pipeline.md` §3.6). `migrate-environment.sh` now points the job
at the release digest with `gcloud run jobs update`, not `deploy`, so it cannot create a job that lacks
the environment's identity and network path, then executes the job and waits. The `terraform apply`
that follows sets the same digest, so the apply leaves no drift. Terraform creates the job on the
environment's first apply.

### 3.5 The CI gate

`backend` keeps its name, so the required checks (TASK-012) are unchanged. It now has a
`postgres:17` service and one extra step, which runs only `PMPlatform.Tests.Integration.Persistence`.
The rest of that project is reserved for tests against the Compose stack. `repo-checks` checks out full
history so the forward-only script can diff against the base branch. `ci-cd-pipeline.yml` installs
`dotnet-ef` from the tool manifest instead of whatever `--global` resolves to on the day, so the
dry-run uses the same tool version as the author.

## 4. Verification

Local run on 2026-09-25: macOS, .NET SDK 10.0.401, PostgreSQL 17 in Docker (`postgres:17`).

| # | What | Command | Result |
| --- | --- | --- | --- |
| 1 | Build | `dotnet build src/backend --configuration Release -warnaserror` | 0 warnings, 0 errors |
| 2 | Unit tests (33 existing + 6 new) | `dotnet test src/backend/PMPlatform.Tests.Unit --configuration Release` | 39 passed, 0 failed |
| 3 | Migration tests on PostgreSQL 17 | `DB_CONNECTION_STRING=… dotnet test src/backend/PMPlatform.Tests.Integration --filter FullyQualifiedName~PMPlatform.Tests.Integration.Persistence` | 2 passed, 0 failed; each throwaway database dropped afterwards |
| 4 | **Acceptance: from empty** | `dotnet ef database update` on a new database | 21 module schemas + `common`; history holds `20260925151339_TASK-024_CreateModuleSchemas` |
| 5 | **Workbook validation cell**, checked independently with `pg_dump --schema-only` | update → dump → roll back the last three (with one migration: to `0`) → update → dump; `diff` | Full and re-applied dumps **identical**. The rolled-back dump differs from the pre-migration one only by `common` and the history table, which a rollback keeps by design. No manual step |
| 6 | History table in the backup fingerprint | `psql … -f docs/operations/database-fingerprint.sql` | `table|common.__EFMigrationsHistory|1|158e5d92…`, `objects|common|constraints=1|indexes=1|…` |
| 7 | Pipeline dry-run, now load-bearing | `.github/scripts/migration-dry-run.sh` (Release build) | Passed: 93 statements, applied twice, none destructive |
| 8 | `migrate` command, published build | `dotnet PMPlatform.Api.dll migrate`, then `migrate 0`, then `migrate NoSuchMigration`, then `migrate` with `DB_CONNECTION_STRING` unset | Applied; reverted; `The migration 'NoSuchMigration' was not found.` with nothing applied; `DB_CONNECTION_STRING is not configured.` |
| 9 | Design package kept out of the image | `dotnet publish src/backend/PMPlatform.Api -c Release`, list the output | EF Core, Relational, Npgsql provider and naming conventions present; no Design, CodeAnalysis or MSBuild assembly |
| 10 | Terraform | `fmt -check`; `validate` on dev/sit/uat/prod; `terraform-check.py`; Trivy 0.74.0 config scan at HIGH/CRITICAL | All clean; Trivy 0 findings |
| 11 | Repository checks | all ten `docs/architecture/*-check.py` | 10 of 10 pass |
| 12 | Release image runs `migrate` (what the Cloud Run job runs) | `docker build -f infra/docker/api.Dockerfile`; `docker run <image> migrate` against a new database; then the service with and without `DB_CONNECTION_STRING` | Exit 0, baseline applied and recorded. `/health`: `Healthy 200` with the database, `Unhealthy 503` without, as before this task. The first build **failed** (F-6), and passed after the fix |

### 4.1 Mutation tests: each check fails when it should

| # | Mutation | Expected to fail | Result |
| --- | --- | --- | --- |
| M-1 | Probe migration whose `Down` drops its table but not the index it added on another table | both `MigrationRollbackTests` | Both failed; the diff named `column\|project.ix_probe_a.id` |
| M-2 | M-1 corrected, five migrations in total (so "last three" keeps two) | none | Both passed |
| M-3 | ID without a task (`20260925160001_ProbeA`) | `EveryMigrationIdNamesItsTask` | Failed, naming the ID |
| M-4 | Empty `Down` | `EveryMigrationHasAnUpAndADown` | Failed: `ProbeB: 1 up, 0 down` |
| M-5 | Forward-only: first migration; newer migration plus snapshot and README change; edit to a merged migration; deleted merged designer; added migration older than base's newest | pass, pass, fail, fail, fail | As expected in all five. The drill found and fixed one defect: the script aborted silently on a base with no migrations (`grep` under `pipefail`) |

The probe migrations were deleted after the run. None is committed.

## 5. Acceptance criteria

| Criterion | Status | Evidence |
| --- | --- | --- |
| `dotnet ef database update` applies cleanly to an empty database and reproduces the full schema | **MET** locally on PostgreSQL 17. The full schema is currently the module schemas; TASK-025 adds tables through the same path | §4 rows 4, 5 |
| Every migration has a tested rollback path | **MET**: every migration's `Down` is executed and compared on every pull request, and a migration without one fails the build | §4 rows 2, 3; §4.1 M-1, M-4 |
| Migration history table records the applied migration list and is part of the backup scope | **MET**: it records exactly the applied list (asserted after every step of D-7), lives in the application database that TASK-020 backs up, and is in the TASK-023 restore fingerprint and V-4 | §3.3; §4 row 6 |
| Validation cell: from empty, roll back the last 3, re-apply, schema matches both ways, zero manual intervention | **MET** as far as one migration allows: "last three" is all one. `TheLastThreeMigrationsRollBackAndReapplyInOneStepEach` runs the cell literally from TASK-025 onwards, and M-2 exercised it with five | §4 row 5; §4.1 M-2 |

## 6. Findings and open items

| # | Finding | Owner |
| --- | --- | --- |
| F-1 | **Not run in an environment.** No GCP environment exists (ADR-001 R-1 to R-3). The Cloud Run job validates in Terraform and scans clean, but has not executed. When DEV exists, set the `MIGRATION_JOB` repository variable to `ahda-pmplatform-migrate`. The first deploy's `Run database migrations` step is this task's live check | DevOps/Platform Lead, at first environment |
| F-2 | **The job authenticates exactly as the service does**, and that path is not proven. TASK-020 issues no database password (the runtime service account authenticates). Whatever `DB_CONNECTION_STRING` and the service do to reach Cloud SQL, the job does the same, but neither has run against Cloud SQL | TASK-019/TASK-020 owners, with F-1 |
| F-3 | **CLOSED by TASK-025 (2026-09-25):** the bridge schema is deleted and compose runs `migrate` (`core-platform-schema.md` §7). Original finding: **The local bridge schema stays.** `infra/docker/postgres/init/01-schema.sql` holds nine TASK-025 tables, so it cannot be deleted by this task. Handed to TASK-025 in `local-development-environment.md` F-2. Until then, reverting the baseline on the Compose database fails by design (guide, "Commands") | TASK-025 |
| F-4 | **Branch protection is unchanged**, deliberately: the migration tests run inside the required `backend` job, and the forward-only check inside the required `repo-checks` job. The forward-only check runs on `pull_request` only; a direct push to a protected branch is already blocked by the ruleset | — |
| F-5 | **Workbook cells to correct.** TASK-024 "Depends On" names "Provision Managed AlloyDB for PostgreSQL"; the product is Cloud SQL (ADR-002 §4.2.3, already listed there). `erd.md` §11's note on this row ("migrations generated per module schema, `common` first") is superseded by D-1 | PMO |
| F-6 | **Found and fixed: the image compiled without the repository's analyzer configuration.** `.dockerignore` sent only `global.json`, `src/backend` and `src/frontend`, so `api.Dockerfile` never saw the root `.editorconfig`. With the first migration that surfaced as CA1707 on EF Core's `TASK024_…` class name, which `.editorconfig` exempts as generated code. The image now copies `.editorconfig`, so it builds under the same rules as the `backend` gate (TASK-014's image, TASK-018 D-1: "what is tested is what ships") | Fixed here |

## 7. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-26 | TASK-027 gives the release image two more commands beside `migrate`: `seed` and `validate-data-integrity`, which run `db/seed/*.sql` embedded in Infrastructure (`Persistence/DatabaseScripts.cs`). Each environment runs them as executions of the same migration job after `migrate` (`seed-data-and-integrity.md` §2). | Database (TASK-027) |
| 2026-09-25 | TASK-025 adds the first tables through this framework: four single-schema migrations (`core-platform-schema.md` §2). F-3 closed: the bridge schema is deleted and compose migrates. `TheLastThreeMigrationsRollBackAndReapplyInOneStepEach` now exercises three real migrations. | Database (TASK-025) |
| 2026-09-25 | Initial record. EF Core 10 migrations with one context and one history in `common` (D-1 to D-4); `migrate` command and Cloud Run job (D-5); baseline `TASK-024_CreateModuleSchemas` (D-6); rollback tests in `backend` (D-7); forward-only check in `repo-checks` (D-8); authoring guide. Verified locally (§4), five mutation groups (§4.1). Bridge-schema deletion handed to TASK-025. `api.Dockerfile` and `.dockerignore` now include `.editorconfig` (F-6). | Database (TASK-024) |

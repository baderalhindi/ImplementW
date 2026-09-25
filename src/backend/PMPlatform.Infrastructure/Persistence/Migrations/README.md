# Authoring a migration

Every schema change is an EF Core migration in this folder. The framework, the decisions behind it and
the evidence that it works are in [`docs/architecture/database-migrations.md`](../../../../../docs/architecture/database-migrations.md)
(TASK-024). This page covers writing one.

## The rules

| # | Rule | Enforced by |
| --- | --- | --- |
| R-1 | **The ID names the task:** `<timestamp>_<TaskID>_<Description>`, e.g. `20260925151339_TASK-024_CreateModuleSchemas`. Pass `TASK-nnn_<Description>` to `migrations add` and EF Core adds the timestamp. One task can own several migrations; one migration belongs to one task. | `MigrationConventionTests.EveryMigrationIdNamesItsTask` |
| R-2 | **Every migration has a Down that reverses its Up**, restoring the schema to exactly what it was before. A generated Down is a draft: read it. EF Core cannot reverse `migrationBuilder.Sql(...)`, so every `Sql` call in Up needs its inverse in Down. | `MigrationConventionTests.EveryMigrationHasAnUpAndADown` (Down exists); `MigrationRollbackTests` (Down is correct, against PostgreSQL) |
| R-3 | **Forward-only.** Once a migration is on `dev`, it is never edited, renamed or deleted, because a database that applied it will not run it again. Fix a mistake with a new migration. `dotnet ef migrations remove` is only for a migration that has not left your branch. | `.github/scripts/migration-forward-only.sh` in `repo-checks` |
| R-4 | **The ID sorts after every migration already on `dev`.** If `dev` gained a newer migration while your branch was open, remove yours and add it again to get a current timestamp. | Same script |
| R-5 | **No model change without its migration.** Change an entity or a configuration, then add the migration in the same commit. | `MigrationConventionTests.TheModelHasNoChangeWithoutAMigration` |
| R-6 | **Expand, then contract** (CTL-37). A release may not drop a table, column or constraint that the release before it still reads. Add the new structure in one release and drop the old one in a later release, marking that drop `EXPAND-THEN-CONTRACT-REVIEWED` in a comment that names the release that stopped reading it. | `.github/scripts/migration-dry-run.sh` |
| R-7 | **A migration touches only its module's schema** (ADR-003 M-13). A foreign key may cross schemas only to another module's referenceable table (ERD D-14). | Review, with `docs/architecture/erd.dbml` as the reference |
| R-8 | **Literals, not code.** Write schema names, lengths and seed values into the migration as literals. A migration records what was applied, so it must not change when a constant elsewhere does. | Review |

The module schemas already exist (`TASK-024_CreateModuleSchemas`). Map a new table into its module's
schema with `ToTable("<table>", "<module_schema>")` in an `IEntityTypeConfiguration<T>` in this assembly.
Names in the database are snake_case automatically (ERD D-4), so the C# stays PascalCase.

## Commands

Run these from the repository root. `dotnet tool restore` installs the pinned `dotnet-ef`
(`.config/dotnet-tools.json`). Infrastructure is both the project and the startup project, so only
`database update` needs a database.

```sh
P=src/backend/PMPlatform.Infrastructure

dotnet ef migrations add TASK-025_CreateProjectTables --project $P --startup-project $P --output-dir Persistence/Migrations
dotnet ef migrations script --idempotent --project $P --startup-project $P   # read the SQL before you commit
dotnet ef migrations remove --project $P --startup-project $P                # only before the branch is pushed

export DB_CONNECTION_STRING='Host=localhost;Port=5432;Database=<scratch>;Username=…;Password=…'
dotnet ef database update --project $P --startup-project $P                  # apply everything
dotnet ef database update <MigrationId> --project $P --startup-project $P    # migrate up or down to that migration
dotnet ef database update 0 --project $P --startup-project $P                # revert everything
```

Before you push, run the same tests CI runs. `DB_CONNECTION_STRING` names a server where the role can
`CREATE DATABASE`. Each test creates its own database there and drops it afterwards.

```sh
docker run -d --rm --name pg17 -e POSTGRES_PASSWORD=scratch -p 55432:5432 postgres:17
dotnet test src/backend/PMPlatform.Tests.Unit --filter FullyQualifiedName~MigrationConvention
DB_CONNECTION_STRING='Host=localhost;Port=55432;Database=postgres;Username=postgres;Password=scratch' \
  dotnet test src/backend/PMPlatform.Tests.Integration --filter FullyQualifiedName~PMPlatform.Tests.Integration.Persistence
```

A migration may touch only its module's schema (R-7), even when two modules reference each other. Create the
tables first and add the foreign key that leaves the schema in a later migration of the same task, once its target
exists: TASK-025 does this for `identity_access` ↔ `master_data_config` (`docs/architecture/core-platform-schema.md` §2).
`dotnet ef migrations add` also emits `EnsureSchema` for a module schema the model has not used before; delete that
call, because the schema is TASK-024's baseline and `MigrationsCreateExactlyTheModuleSchemas` counts it.

The Compose stack applies the migrations itself: its one-shot `migrate` service runs before `seed` and `api`
(`infra/docker/docker-compose.yml`). To test a rollback, use a scratch database, not the Compose one, whose seed rows
a Down would delete.

## In an environment

No one runs `dotnet ef` against DEV, SIT, UAT or PROD. The databases have no public IP. Each release
applies its migrations with the environment's Cloud Run job, `<resource_prefix>-migrate`, which runs
`dotnet PMPlatform.Api.dll migrate` from the release image before the service moves to that image
(`.github/scripts/migrate-environment.sh`).

**Rolling back a schema** means reverting to the last migration of the release you are returning to,
using the image that is running now, because only that image contains the Down you need. Do this
before re-promoting the earlier release:

```sh
gcloud run jobs execute <resource_prefix>-migrate --project <project> --region <region> \
  --args=migrate,<last migration ID of the earlier release> --wait
```

Under expand-then-contract (R-6), the earlier release still runs on the newer schema, so most rollbacks
only need the re-promotion (`docs/architecture/cicd-pipeline.md` §4.4). Revert the schema only when the
migration itself is the fault.

## The history table

`common."__EFMigrationsHistory"`, with columns `migration_id` and `product_version`. The names are
snake_case because the naming convention applies to the history table too. It lives in the `common`
schema (ERD D-17) inside the application database, so every backup and every PITR restore includes it.
The restore runbook's fingerprint covers it (`docs/operations/database-fingerprint.sql`), and V-4 compares it
with the release in service (`docs/operations/backup-restore-dr-runbook.md` §6.1).

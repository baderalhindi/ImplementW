# Contributing

This is the PMPlatform monorepo. Its layout is fixed by ADR-002 §6 (stack and directory scheme) and ADR-003 (module boundaries). Both records are **PROPOSED — PENDING AHDA APPROVAL**; until they are approved the layout below is provisional and nothing from this repository is released (`docs/architecture/monorepo-bootstrap.md`).

## Layout

```
/
├── .editorconfig                  solution-wide editor and C# analyzer baseline
├── .github/                       CODEOWNERS, PR template, branch-protection ruleset, workflows (ci-quality-gates, pr-policy)
├── global.json                    .NET SDK pin
├── .nvmrc                         Node.js line for the frontend toolchain
├── db/seed/                       SQL seed and data-integrity scripts (TASK-027)
├── docs/                          architecture records, ADRs, governance, planning
├── infra/
│   ├── docker/                    local stack — docker-compose (TASK-014)
│   ├── environments/              per-environment configuration (TASK-016)
│   ├── secrets/                   secret *templates* only; real values are never committed (TASK-019)
│   └── terraform/                 network (TASK-021), database (TASK-020)
└── src/
    ├── backend/
    │   ├── PMPlatform.slnx
    │   ├── Directory.Build.props          analyzers, warnings-as-errors, .NET 10
    │   ├── Directory.Packages.props       central NuGet versions
    │   ├── PMPlatform.Domain/             entities, value objects, domain rules — references nothing
    │   │   └── Common/                    Money (SAR), BilingualLabel, NarrativeText, FinancialProvenance
    │   ├── PMPlatform.Application/        application services, one folder per module
    │   │   ├── Common/                    cross-cutting, domain-neutral code
    │   │   └── Features/<Module>/         21 modules; Contracts/ is the module's public surface
    │   ├── PMPlatform.Infrastructure/     EF Core, migrations, outbound adapters
    │   │   ├── Identity/                  AD/LDAP, SSO, Nafath adapters
    │   │   ├── Notifications/             email and SMS channel adapters
    │   │   └── Persistence/Migrations/    EF Core migrations
    │   ├── PMPlatform.Api/                controllers, middleware, health checks; Program.cs is the composition root
    │   │   └── appsettings.Template.json  every environment variable by name, no values (TASK-013)
    │   ├── PMPlatform.Tests.Unit/         xUnit; Architecture/ holds the boundary tests (A-1 to A-6)
    │   └── PMPlatform.Tests.Integration/  xUnit against the containerised stack
    └── frontend/
        ├── .env.example                   public, VITE_-prefixed variables by name, no values (TASK-013)
        ├── e2e/                           Playwright suite (TASK-085)
        └── src/
            ├── content/help/              in-app help content
            └── features/<module>/         one folder per module, kebab-case
```

### Module directories

`<Module>` is not free text. The 21 names are ADR-003 §4.3 and are enumerated in
`src/backend/PMPlatform.Tests.Unit/Architecture/ModuleRegistry.cs`; a namespace under `Features/` that is not
one of them fails the build's test step. Backend folders are PascalCase and equal to the namespace; frontend
folders are kebab-case (ADR-002 §6.4 — the case difference is intentional).

| Backend `Features/` | Frontend `features/` | Domain |
| --- | --- | --- |
| Project | projects | WF-01 |
| Progress | progress | WF-02 |
| Schedule | schedule | WF-03 |
| ProjectTask | tasks | WF-04 |
| Milestone | milestones | WF-05 |
| Risk | risks | WF-06 |
| ManagementConcern | issues-challenges | WF-07 |
| ChangeRequest | change-requests | WF-08 |
| Suspension | suspension-closure | WF-09 |
| Closure | suspension-closure | WF-10 |
| Approval | approvals | WF-11 |
| DocumentManagement | documents | WF-12 |
| ExternalParticipation | external-participation | WF-13 |
| FinancialKpi | financial-kpi | WF-14 |
| Notifications | notifications | WF-15 |
| Dashboards | dashboards | FG-01 |
| Reports | reports | FG-02 |
| IdentityAccess | identity-access | FG-03 |
| MasterDataConfig | — | FG-04 |
| IntegrationMonitoring | integration-admin | FG-05 |
| AuditActivity | audit-activity | FG-06 |

A module is one folder in `Application/Features/<Module>`, its entities in `Domain/<Module>`, its EF
configuration and repositories in `Infrastructure/Persistence/<Module>`, and its controllers in
`Api/Controllers/<Module>` (ADR-003 L-5). The architecture tests attribute a type to a module by the first
namespace segment that names one.

## Toolchain

| | Pinned in | Version |
| --- | --- | --- |
| .NET SDK | `global.json` | 10.0.4xx (LTS; `rollForward: latestPatch`) |
| Node.js | `.nvmrc`, `package.json` `engines` | 24 (build toolchain only — the SPA ships as static assets) |
| npm | `package.json` `engines` | 11+ |
| NuGet packages | `src/backend/Directory.Packages.props` | central package management; no versions in `.csproj` files |
| npm packages | `src/frontend/package.json`, `package-lock.json` | exact versions, `npm ci` in CI |

## Build and verify

Backend — the acceptance bar is zero warnings; `Directory.Build.props` makes every warning an error:

```sh
dotnet build src/backend -warnaserror
dotnet test src/backend/PMPlatform.Tests.Unit
```

Frontend:

```sh
cd src/frontend
npm ci
npm run lint          # ESLint: typescript-eslint strict + stylistic (type-checked), react-hooks, react-refresh
npm run format:check  # Prettier (npm run format to fix)
npm run typecheck     # tsc -b --noEmit, strict
npm test              # Vitest, jsdom; src/**/*.test.{ts,tsx} (npm run test:watch while developing)
npm run build         # tsc -b && vite build
```

Without a local SDK, both run unchanged inside the official images:

```sh
podman run --rm -v "$PWD:/repo" -w /repo/src/backend mcr.microsoft.com/dotnet/sdk:10.0 sh -c 'dotnet build -warnaserror && dotnet test --no-build'
podman run --rm -v "$PWD/src/frontend:/w" -w /w node:24-alpine sh -c 'npm ci && npm run lint && npm test && npm run build'
```

## Local configuration

Every environment variable the platform reads is named, with no value, in two templates that mirror the workbook's
Environment and Secrets sheet (`docs/architecture/environment-and-secrets.csv`; record:
`docs/architecture/environment-templates.md`). Copy, then fill only the values whose scope includes DEV:

```sh
cp src/backend/PMPlatform.Api/appsettings.Template.json src/backend/PMPlatform.Api/appsettings.Development.Local.json
cp src/frontend/.env.example src/frontend/.env.local
```

Both copies are git-ignored and excluded from `dotnet publish`. `Program.cs` reads
`appsettings.{Environment}.Local.json` after `appsettings.{Environment}.json`; an environment variable still
overrides both, exactly as in SIT, UAT and PROD, where no value ever comes from a file. Keep the templates in
step with the sheet — `python3 docs/architecture/env-template-check.py` fails on a missing or extra variable, a
Secret-classified variable in the frontend template, or any value in either template.

## Local development stack

The stack gives you PostgreSQL, the API and the frontend dev server with seeded test data — no cloud
access needed (TASK-014; record: `docs/architecture/local-development-environment.md`). The compose file lives
under `infra/docker/`, so every command names it with `-f`; a bare `docker compose up` from the repository root
reports `no configuration file provided: not found`.

```sh
docker compose -f infra/docker/docker-compose.yml up --build --wait   # from the repository root
curl http://localhost:5080/health                                      # Healthy once the API has reached the database
open http://localhost:5173                                             # frontend, hot-reloading from src/frontend
docker compose -f infra/docker/docker-compose.yml down -v              # reset: drops the volume, re-seeds on next up
```

- To drop the `-f`, point Compose at the file once per shell with `export
  COMPOSE_FILE=infra/docker/docker-compose.yml`, or put that line in a root `.env` — Compose reads `.env` from
  the working directory for its own settings. That file is git-ignored, so the setting stays yours; the commands
  above are the ones that work on any checkout.
- `infra/docker/smoke-test.sh` is the acceptance check: clean state, build, up, `/health` 200, frontend 200, and
  one active local user per role R01–R08.
- Every value in `docker-compose.yml` is a local default and non-secret; the database password and
  `JWT_SIGNING_KEY` are labelled as such. **No SIT/UAT/PROD value ever goes in that file** — those are injected
  from the secret store (TASK-016, TASK-019), and the templates above still hold names only.
- The seed creates `local.r01` … `local.r08`, one per canonical role, each bound to that role's shipped-default
  permission profile version. They are data, not accounts: there is no credential and no local sign-in path until
  TASK-028.
- `infra/docker/postgres/init/01-schema.sql` is a **bridge** — nine ERD tables so the seed has somewhere to go
  before TASK-023/TASK-024 create the schema through EF Core migrations. It is deleted when they land.
  `python3 docs/architecture/local-stack-check.py` fails if any column drifts from `erd.dbml`.

## Quality baselines

- **`.editorconfig`** is the single formatting and naming source for both tiers. C# rules are enforced at build
  (`EnforceCodeStyleInBuild`, `AnalysisLevel=latest-recommended`, `TreatWarningsAsErrors`). Files under
  `Persistence/Migrations/` are generated code and are not analysed.
- **Architecture tests** (`PMPlatform.Tests.Unit/Architecture`) run with every `dotnet test` and fail the build on:
  a module referencing another module outside its `Contracts` (A-1); a repository, `DbContext` or `DbSet` used
  outside its owner or outside `Persistence` (A-2); a Domain type holding another module's entity (A-3); a
  layering violation, including `Api` touching `Infrastructure` anywhere but `Program.cs` (A-4); a migration
  or SQL write statement outside `Infrastructure/Persistence` (A-5); a cross-module reference that ADR-003 §8
  does not list (A-6). **To add an edge, revise ADR-003 §8.2 first, then `ModuleRegistry.cs`.**
- **Frontend**: TypeScript `strict` plus `noUncheckedIndexedAccess` and `exactOptionalPropertyTypes`; only
  `VITE_`-prefixed variables reach the bundle (T-1); no database driver may be added to `package.json`.
- **CI gate** (`.github/workflows/ci-quality-gates.yml`; TASK-015, record: `docs/architecture/ci-quality-gates.md`) runs the commands above on every pull request
  and fails it on any violation, so nothing merges unchecked. Three jobs, each capped at ten minutes:
  `backend` (restore, `-warnaserror` build with the analyzers, unit tests), `frontend` (`npm ci`, lint,
  format, `tsc -b --noEmit`, Vitest, build) and `repo-checks` (the four `python3 docs/architecture/*-check.py`
  scripts, standard library only). Integration tests need the containerised stack and are not in this gate.

## Branches, pull requests, and main

Full policy: `docs/architecture/branching-strategy.md` (TASK-012).

- Trunk-based development: `main` is the trunk; every change is a short-lived branch off `main`, merged by pull
  request and deleted on merge. Squash merge by default. `dev` and `stage` are pre-existing environment branches,
  protected identically until TASK-018 retires them.
- Branch names are `type/task-id-short-description`, e.g. `chore/task-011-repo-bootstrap`; `type` is one of
  `feat`, `fix`, `chore`, `docs`, `refactor`, `test`, `ci`, `build`, `perf`, `revert`, `hotfix`. The workbook's
  Branch column is authoritative. One Task ID per branch.
- Every PR is opened from `.github/PULL_REQUEST_TEMPLATE.md`: Task ID, description, test evidence and a security
  checklist. The `pr-policy` check fails the PR until the Task ID is present, the evidence is filled in and every
  checklist box is ticked.
- `main` (and `dev`, `stage`) are protected by `.github/branch-protection/protected-branches.ruleset.json`:
  a pull request with **one approving review** and a review from the directory's code owners
  (`.github/CODEOWNERS`); the `backend`, `frontend`, `repo-checks` and `pr-policy` checks **must pass** on a branch that is current
  with the target; no direct push, no force-push, no bypass.
- Commit messages: imperative subject line, task id in the body or the PR title.

## Where things do not go

- Business rules do not go in `PMPlatform.Api` (T-3) or in the SPA (T-6).
- Nothing that means something to one domain goes in `Domain/Common` or `Application/Common` (M-10).
- No secret, connection string or `.env` file is committed; `infra/secrets/` holds templates only. A value
  never goes into `appsettings.Template.json` or `.env.example` — not even a local one.
- No `deleted_at`, `currency_code` or `row_version` column, anywhere (ERD D-3, D-5, D-16).

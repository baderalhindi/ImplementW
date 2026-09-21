# Contributing

This is the PMPlatform monorepo. Its layout is fixed by ADR-002 §6 (stack and directory scheme) and ADR-003 (module boundaries). Both records are **PROPOSED — PENDING AHDA APPROVAL**; until they are approved the layout below is provisional and nothing from this repository is released (`docs/architecture/monorepo-bootstrap.md`).

## Layout

```
/
├── .editorconfig                  solution-wide editor and C# analyzer baseline
├── .github/workflows/ci.yml       required status checks on main (backend, frontend)
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
    │   ├── PMPlatform.Tests.Unit/         xUnit; Architecture/ holds the boundary tests (A-1 to A-6)
    │   └── PMPlatform.Tests.Integration/  xUnit against the containerised stack
    └── frontend/
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
dotnet test src/backend
```

Frontend:

```sh
cd src/frontend
npm ci
npm run lint          # ESLint: typescript-eslint strict + stylistic (type-checked), react-hooks, react-refresh
npm run format:check  # Prettier (npm run format to fix)
npm run typecheck     # tsc -b --noEmit, strict
npm run build         # tsc -b && vite build
```

Without a local SDK, both run unchanged inside the official images:

```sh
podman run --rm -v "$PWD:/repo" -w /repo/src/backend mcr.microsoft.com/dotnet/sdk:10.0 sh -c 'dotnet build -warnaserror && dotnet test --no-build'
podman run --rm -v "$PWD/src/frontend:/w" -w /w node:24-alpine sh -c 'npm ci && npm run lint && npm run build'
```

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

## Branches, pull requests, and main

- Branch names are `type/task-id-short-description`, e.g. `chore/task-011-repo-bootstrap` (ADR-002 §6.4; TASK-012 owns the full strategy, CODEOWNERS and the PR template).
- `main` is protected: a pull request is required, and the `backend` and `frontend` jobs of `.github/workflows/ci.yml`
  are **required status checks** that must pass before merge. Direct pushes and force-pushes to `main` are disabled.
- Commit messages: imperative subject line, task id in the body or the PR title.

## Where things do not go

- Business rules do not go in `PMPlatform.Api` (T-3) or in the SPA (T-6).
- Nothing that means something to one domain goes in `Domain/Common` or `Application/Common` (M-10).
- No secret, connection string or `.env` file is committed; `infra/secrets/` holds templates only.
- No `deleted_at`, `currency_code` or `row_version` column, anywhere (ERD D-3, D-5, D-16).

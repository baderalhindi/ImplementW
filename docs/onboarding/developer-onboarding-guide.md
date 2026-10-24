# Developer Onboarding and Architecture Guide

| Field | Value |
| --- | --- |
| Task | TASK-095 — Author Developer Onboarding & Architecture Guide (P17 - Documentation) |
| Depends on | TASK-007 — module boundaries (`docs/architecture/solution-architecture.md`); TASK-014 — local stack (`docs/architecture/local-development-environment.md`) |
| Also draws on | TASK-012 — branching and PRs (`docs/architecture/branching-strategy.md`); TASK-001 — controlled sources (`docs/baseline/controlled-source-baseline.md`) |
| Record date | 2026-10-24 |
| Status | **DRY-RUN BY A STAND-IN; A HUMAN DRY RUN IS STILL OWED.** The workbook's validation was run from a fresh clone with cold images by an AI agent with no prior knowledge of the codebase, following only this guide. Run 1 took 240s to signed in and logged 13 steps that needed outside help. Run 2, on the revised guide, took 225s and logged 3. All 16 are fixed, and every check passed in both runs (§9). The acceptance criterion names one developer new to the codebase, and no person has done that run yet (§9.3) |
| Branch | `docs/task-095-developer-onboarding-guide` |

This guide takes you from no checkout to a running local stack and a merged first pull request. Do the sections in
order. Each one ends with a check. If a check fails and the guide does not tell you why, that is a defect in the
guide: note it for your dry-run entry (§8) and keep going.

When this guide and a linked record disagree, the record wins. This guide summarises the records and does not
replace them.

## 1. Before you start

### 1.1 Access to request

Ask for all three on your first day, in writing. Nothing below needs a conversation.

| What | From | Needed for |
| --- | --- | --- |
| Write access to `github.com/baderalhindi/ImplementW` | Repository administrator | Pushing a branch and opening a PR (§6). The repository is **public**; anyone can clone it, but only collaborators can push |
| The controlled-source package `Project Files.zip` and the workbook `AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx` | PMO Engagement Lead | Reading your task row and the specification behind it (§7) |
| Read access to the Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`) | PMO Engagement Lead | The live copy of the workbook, which the records cite |

### 1.2 Tools to install

| Tool | Version | Why |
| --- | --- | --- |
| Docker Desktop (or podman with `DOCKER_HOST` set) | Docker Desktop 4.92 / Compose 5.5 tested | Runs the whole local stack |
| git | any recent | — |
| GitHub CLI `gh` | any recent; run `gh auth login` once | Opening the PR and reading its checks |
| .NET SDK | `10.0.401`, pinned in `global.json` (`rollForward: latestPatch`) | Backend build and tests on the host. Optional: §5.3 shows the container alternative |
| Python 3 | any 3.x; standard library only | The `repo-checks` scripts |

You do **not** need Node.js on the host. The frontend's lint, typecheck and tests run inside the `AHDA-frontend`
container, which has the pinned Node 24 toolchain.

The stack publishes four ports: `5432` (PostgreSQL), `3389` (test directory), `5080` (API) and `5173` (frontend).
If one is taken, set `PMPLATFORM_POSTGRES_PORT`, `PMPLATFORM_LDAP_PORT`, `PMPLATFORM_API_PORT` or
`PMPLATFORM_FRONTEND_PORT` before `up`. If you move the frontend off 5173, the API refuses the browser's origin
(`CORS_ALLOWED_ORIGINS` is `http://localhost:5173`), so prefer moving the others. Export the variable in every
shell that runs `up` (or put it in your shell profile). A shell without it binds the default port again:

```sh
export PMPLATFORM_POSTGRES_PORT=15432 PMPLATFORM_LDAP_PORT=13389   # example values
```

If you move PostgreSQL or LDAP, the integration tests (§5.3) need the same ports: use `Port=15432` in their
`DB_CONNECTION_STRING`, and set `AD_LDAP_URL=ldap://localhost:13389/dc=pmplatform,dc=local`. That URL is the default
the tests use when the variable is unset, with port 3389.

The stack was verified on macOS with Docker Desktop and podman. Windows (WSL2) and Linux Docker Engine are not yet
verified (`local-development-environment.md` F-6). If you are on one of them, your dry-run entry is the first
evidence for it.

**Check:** `docker compose version`, `gh auth status` and `python3 --version` all answer. If you installed the SDK,
`dotnet --version` prints `10.0.4xx`.

## 2. Get the code

```sh
git clone https://github.com/baderalhindi/ImplementW.git
cd ImplementW
git switch dev
```

`dev` is the default branch and the integration branch: **every PR targets `dev`**. `main` is behind `dev` and is
fed only from it (§6.1). Run every command in this guide from the repository root unless it says otherwise.

**Check:** `git log --oneline -1` prints the latest merge on `dev`.

## 3. Run the local stack

### 3.1 Start it

```sh
docker compose -f infra/docker/docker-compose.yml up --build --wait
```

The compose file lives under `infra/docker/`, so every command names it with `-f`. A bare `docker compose up` from
the root fails with `no configuration file provided`. To skip the `-f`, run `export
COMPOSE_FILE=infra/docker/docker-compose.yml` once per shell.

What `up` does, in order: `postgres` and `ldap` become healthy. The one-shot `migrate` container applies every
EF Core migration and exits. The one-shot `seed` container then loads the platform seed, one local user per role
and the data-integrity check, all in one transaction, and exits. `api` starts after that, and `frontend` starts
alongside. `--wait` returns once everything is healthy, not just started.

The compose file sets the project name `ahda`. Its containers are `AHDA-postgres`, `AHDA-ldap`, `AHDA-migrate`,
`AHDA-seed`, `AHDA-api` and `AHDA-frontend`, and its volumes are `ahda_*`. To run a second copy beside an existing
one, add `-p <other-name>` to every compose command. You also need an override file that gives the containers other
names, because the names are fixed in the compose file. Save one outside the checkout, for example as
`../ahda2.override.yml`:

```yaml
services:
  postgres: {container_name: AHDA2-postgres}
  ldap: {container_name: AHDA2-ldap}
  migrate: {container_name: AHDA2-migrate}
  seed: {container_name: AHDA2-seed}
  api: {container_name: AHDA2-api}
  frontend: {container_name: AHDA2-frontend}
```

Then run every compose command as `docker compose -p ahda2 -f infra/docker/docker-compose.yml -f
../ahda2.override.yml …`. Your volumes become `ahda2_*` and your images `ahda2-*`. The published ports still clash,
so also move them (§1.2). Wherever this guide says `AHDA-…`, use your container names.

How long it takes: the first build pulls base images and restores packages with empty caches, and took 160s on a
clean Docker Desktop (108s build, 52s to healthy). The §9 dry run took 240s from `git clone` to signed in. Later
starts take 10–60s.

### 3.2 Check it

```sh
curl http://localhost:5080/health      # "Healthy": the API has reached the database
open http://localhost:5173             # the frontend (use xdg-open on Linux)
```

`/health` answers 200 only when the API can query PostgreSQL; a stopped database turns it to 503. The API's
reference docs are in Swagger UI at http://localhost:5080/api-docs/ (`docs/api/README.md`). That address redirects
to `/api-docs/index.html`, so `curl` without `-L` shows only a 301.

### 3.3 Sign in

The seed creates one user per canonical role. Each one signs in through the test directory `AHDA-ldap`, which
stands in for AHDA's Active Directory. Every person shares one local default password: the `userPassword` of any
`uid=local.r01` … `uid=local.r08` entry in `infra/docker/ldap/directory.ldif`. The file's header comment also names
it. It is a non-secret local value that is valid only on this stack. Never reuse it anywhere else, and never paste
it into a fixture, a log or a screenshot.

| Username | Role | Notes |
| --- | --- | --- |
| `local.r01` | R01 System Administrator | **Cannot sign in on this stack**: R01 requires MFA and the stack has no MFA provider, so `POST /api/v1/sessions` answers 503. Use another role |
| `local.r02` | R02 Portfolio Manager | Good default for a first look |
| `local.r03` | R03 Sector Manager | Holds the department anchor `DEPT-LOCAL` |
| `local.r04` | R04 Project Manager | — |
| `local.r05` | R05 Liaison | — |
| `local.r06` | R06 Viewer | — |
| `local.r07` | R07 Executive | — |
| `local.r08` | R08 External Entity User | `EXTERNAL` user with the entity anchor |

The local database holds no projects. Most domain screens open to an empty register until you create data.

**Check:** you are signed in at http://localhost:5173 as `local.r02`. From a terminal, the same sign-in through the
frontend's proxy answers `201`. Reading the password from the file keeps it out of your shell history:

```sh
P=$(awk '/^dn: uid=local.r02,/{f=1} f&&/^userPassword:/{print $2;exit}' infra/docker/ldap/directory.ldif)
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5173/api/v1/sessions \
  -H 'Content-Type: application/json' -d "{\"username\":\"local.r02\",\"password\":\"$P\"}"
```

### 3.4 Day to day

| To | Run |
| --- | --- |
| Stop the stack, keeping its data | `docker compose -f infra/docker/docker-compose.yml stop`. Afterwards `docker ps -a` shows `AHDA-frontend` as `Exited (1)`: the dev server was killed, which is not a fault |
| Start it again | the `up --build --wait` command of §3.1 (`--build` picks up backend changes) |
| Reset to a freshly seeded database | `docker compose -f infra/docker/docker-compose.yml down -v`, then the §3.1 `up --build --wait` command |
| Run the stack's acceptance check | `infra/docker/smoke-test.sh`. **It starts with `down --volumes --remove-orphans`**: it deletes your local data and any extra container in the `ahda` project. It takes no `-p` and no override, so it always acts on the default `ahda` stack. Never run it while an `ahda` stack you need is up |

Frontend edits hot-reload: `src/frontend` is bind-mounted into `AHDA-frontend`. Backend edits need a rebuild of the
`api` image, which `up --build` does.

### 3.5 When it does not come up

| Symptom | Cause | Fix |
| --- | --- | --- |
| `service "seed" didn't complete successfully: exit 3` | The data-integrity check found a violation in your existing database, usually rows left by manual experiments (`docker logs AHDA-seed` names it) | `down -v`, then `up`. A fresh volume always passes |
| `Bind for 0.0.0.0:5432 failed: port is already allocated` | Another PostgreSQL (or another stack) owns the port | Set `PMPLATFORM_POSTGRES_PORT` (or the matching variable from §1.2) and `up` again |
| A route you just added answers 404 with an empty body | The `AHDA-api` container runs an image built before your change | `up --build --wait` |
| `curl /health` answers 503 | The API cannot reach PostgreSQL | `docker compose -f infra/docker/docker-compose.yml ps`; start `postgres` |
| Sign-in answers 503 | The user needs MFA (R01, or anyone you granted R01), or `AHDA-ldap` is down | Use a non-R01 user; check `ldap` is healthy |

The full record of the stack, including why each value in the compose file is a non-secret local default, is
`docs/architecture/local-development-environment.md`.

## 4. How the code is organised

Read this before your first change. These are the rules the architecture tests enforce (§4.4), and they fail the
build, not just a review.

### 4.1 Three tiers

```
React SPA (src/frontend)  ──HTTPS/JSON──▶  ASP.NET Core API (src/backend)  ──EF Core──▶  PostgreSQL 17
        holds no database credential          sole writer of the data tier            one schema per module
```

- The SPA never reaches the database. No database driver or `DB_` variable goes anywhere near
  `src/frontend` (T-1).
- Every write enters through an API endpoint that calls exactly one module's application service (T-2).
- Controllers hold no business rule. They bind, call and map. Validation of *meaning* belongs to the module (T-3).
- Authorization is decided on the server for every request. Hiding a button in the SPA is presentation, never a
  control (T-4).
- A screen may combine several modules by making several calls. A rule that spans two modules lives in the
  backend, in an application service, never in the SPA (T-6).

### 4.2 Four backend projects

| Project | Holds | May reference |
| --- | --- | --- |
| `PMPlatform.Domain` | Entities, value objects, domain rules | nothing (L-1) |
| `PMPlatform.Application` | Application services and the interfaces Infrastructure implements, one folder per module | Domain (L-2) |
| `PMPlatform.Infrastructure` | EF Core, configurations, migrations, outbound adapters (directory, email, SMS, storage) | Application, Domain (L-3) |
| `PMPlatform.Api` | Controllers, middleware, health checks; `Program.cs` is the composition root | Application, plus Infrastructure in `Program.cs` only (L-4) |

Tests: `PMPlatform.Tests.Unit` (xUnit, including `Architecture/`) and `PMPlatform.Tests.Integration` (xUnit
against PostgreSQL and `AHDA-ldap`).

### 4.3 Twenty-one modules

One module per WF/FG domain, 1:1. A module is the same name in four places: `Application/Features/<Module>`,
`Domain/<Module>`, `Infrastructure/Persistence/<Module>` and `Api/Controllers/<Module>`. Its frontend folder is
`src/frontend/src/features/<kebab-name>`. The full table of names and folders is in `CONTRIBUTING.md`, "Module
directories". The names are fixed by `src/backend/PMPlatform.Tests.Unit/Architecture/ModuleRegistry.cs`; a
`Features/` folder with any other name fails the tests.

The module's group decides what it may call:

| Group | Modules | Rule |
| --- | --- | --- |
| Platform foundation | IdentityAccess, MasterDataConfig, DocumentManagement, AuditActivity | Callable by any module; calls no domain module, ever |
| Workflow service | Approval | Started by a domain module; returns its outcome as an event, never by calling back |
| Core domain | Project, Progress, Schedule, ProjectTask, Milestone, Risk, ManagementConcern, ChangeRequest, Suspension, Closure, FinancialKpi | An acyclic graph rooted at Project |
| Participation channel | ExternalParticipation | Applies external contributions through one typed adapter, never by writing another module's data |
| Read side and sinks | Dashboards, Reports, Notifications, IntegrationMonitoring | Read or receive; never write domain data, never recompute a fact another module owns |

The module boundaries are architecture decision ADR-003. Its full record is
`docs/architecture/solution-architecture.md`; the PR template's "ADR-003 §8.2" means §8.2 of that file. The call
rules you will meet first are in its §6:

- **One owner per fact.** Only the owning module's code writes its tables (M-1).
- **Another module's public surface is `Features/<Module>/Contracts` and nothing else.** Commands, queries, DTOs
  and events are public. Handlers, repositories, entities and `DbSet`s are not, whatever their C# accessibility
  (M-2).
- **Writing to another module means calling its application service with a typed command** (M-3). **Reading from
  it means calling its query service** (M-4). A reference to another module's entity is held as an id, never as a
  navigation property.
- **Notifications go out after commit, as a `NotificationIntent`**. Nothing calls Notifications synchronously
  (M-5). **Audit events go through the outbox inside the writing transaction** (M-6).
- **One PostgreSQL schema per module.** A module's migrations touch only its schema (M-13).
- **Shared code in `Application/Common` and `Domain/Common` is domain-neutral** (M-10). If a type means something
  to one domain, it belongs to that domain's module.

A module's query and command services are the `I…Reader`, `I…Service` and `I…Commands` interfaces in its
`Contracts/` folder. For example, Project's read surface is `Features/Project/Contracts/IProjectFactsReader.cs`.
Before you add a call from module X to module Y, find the X → Y row in the edge table of `solution-architecture.md`
§8.2. If the row exists with the mechanism you need, no register change is needed. If it does not, §4.4 applies.

Worked example: to let Risk read a project's status, Risk's application service calls `IProjectFactsReader` (edge
Risk → Project in §8.2) and keeps the `ProjectId` as an id. It never touches Project's repository, entities,
`DbSet` or tables.

### 4.4 What enforces it

`dotnet test src/backend/PMPlatform.Tests.Unit` runs the architecture suite on every build and in CI:

| Test | Fails when |
| --- | --- |
| A-1 | A module references another module's types outside its `Contracts/` |
| A-2 | A repository, `DbContext` or `DbSet` is used outside its owning module or outside `Persistence` |
| A-3 | An entity holds a navigation property to another module's entity |
| A-4 | A project references a layer it may not (§4.2) |
| A-5 | A migration or SQL write sits outside `Infrastructure/Persistence` |
| A-6 | The real cross-module edges differ from the edge register in `solution-architecture.md` §8.2 |

If your change needs a new cross-module edge, **revise `solution-architecture.md` §8.2 first, then
`ModuleRegistry.cs`**, in the same PR, and say so in the PR description.

Before you write code in a module, read its record under `docs/architecture/` (for example `risk-management.md`
and `risk-management-ui.md` for WF-06). It states what was built, the decisions taken and the open findings.

## 5. Make a change and check it locally

### 5.1 Pick up a task

Every change belongs to one workbook task, `TASK-nnn`. Open its row in the workbook's Implementation Plan sheet
(§7.2). Read Detailed Description, Acceptance Criteria, Canonical File Directory, Deliverables,
**GitHub Branch Name** and Validation Checks / Notes. The Branch column is the branch you create.

### 5.2 Branch

```sh
git switch dev && git pull
git switch -c feat/task-046-schedule-baseline     # example: the task's Branch column, verbatim
```

Names are `type/task-nnn-short-description`. `type` is one of `feat`, `fix`, `chore`, `infra`, `sec`, `docs`,
`test`, `release`, `refactor`, `ci`, `build`, `perf`, `revert`, `hotfix`, and the description is lowercase
kebab-case. One task per branch. A task that needs a second PR reuses its `task-nnn` with a different description.

Commit messages use an imperative subject with the type prefix and the task id, as `git log` on `dev` shows:
`docs: publish the API documentation — … (TASK-094)`.

### 5.3 Run what CI runs

The PR cannot merge until the required checks pass (§6.2). Run the relevant subset locally first:

| CI job | Local command | When |
| --- | --- | --- |
| `backend` (build, analyzers, unit and architecture tests) | `dotnet build src/backend -warnaserror && dotnet test src/backend/PMPlatform.Tests.Unit --no-build` | Any backend change |
| integration jobs (`backend-*`) | `docker compose -f infra/docker/docker-compose.yml up -d --wait postgres ldap`, then `DB_CONNECTION_STRING="Host=localhost;Port=5432;Database=postgres;Username=pmplatform;Password=pmplatform" dotnet test src/backend/PMPlatform.Tests.Integration` | Backend change touching persistence, an API or a module's behaviour. Both containers must be up: without `ldap` about 120 tests fail at sign-in with 503. **If you moved the ports (§1.2), change the command too**: put your `PMPLATFORM_POSTGRES_PORT` in place of `Port=5432`, and set `AD_LDAP_URL=ldap://localhost:<your PMPLATFORM_LDAP_PORT>/dc=pmplatform,dc=local`. As printed, the command reaches whatever owns 5432 and 3389, which may be another stack's database. If you run a second copy (§3.1), add your `-p` and override to the `up` |
| `frontend` | `docker exec -w /repo/src/frontend AHDA-frontend sh -c 'npm run lint && npm run format:check && npm run typecheck && npm test'` | Any frontend change |
| `repo-checks` | the command below this table: the job's 19 Python scripts plus the forward-only migration check, about 5s | Every PR; it is cheap |
| `api-documentation` | regenerate `docs/api/openapi.v1.json` as `CONTRIBUTING.md` "Build and verify" shows | Any API contract change |

The `repo-checks` job, run locally. The command reads the job's own `run:` lines, so it stays current when a
script is added:

```sh
sed -n '/^  repo-checks:/,/^  terraform:/s/^ *run: \(python3 .*\)/\1/p' .github/workflows/ci-quality-gates.yml | sh -e \
  && BASE_REF=origin/dev .github/scripts/migration-forward-only.sh
```

Notes from the dry run (§9):

- With no SDK on the host, the backend commands run unchanged in `mcr.microsoft.com/dotnet/sdk:10.0`
  (`CONTRIBUTING.md`, "Build and verify").
- The Vitest suite is sensitive to machine load. With other Vite or API containers running, a few `findBy…`
  waits time out after 5s, and a different set fails on each run. Stop the extra containers and rerun before
  investigating. A real failure repeats.
- `CONTRIBUTING.md` holds the details this table leaves out: OpenAPI snapshots, coverage thresholds, migrations
  and seed rules.

## 6. Open the pull request

### 6.1 Where it goes

PRs go from your task branch into **`dev`**, which is the repository's default, so `gh pr create` picks it up.
`branching-strategy.md` §2 describes the target model, where `main` is the trunk. That model is not in force yet:
`main` is far behind `dev` until item S-3 of that record (fast-forward `main`, retire `dev`) is done. A push to
`main` starts the deployment pipeline (`.github/workflows/ci-cd-pipeline.yml`), which is not your concern on a task branch.

### 6.2 Steps

```sh
git push -u origin <your-branch>
mkdir -p artifacts && cp .github/PULL_REQUEST_TEMPLATE.md artifacts/pr-body.md   # artifacts/ is git-ignored; fill this copy in
gh pr create --base dev --title "docs: TASK-nnn <what changed>" --body-file artifacts/pr-body.md   # title form of PRs #120, #121
gh pr edit <number> --body-file artifacts/pr-body.md         # after any later change to the body
gh pr checks <number> --watch
```

Fill every section of `.github/PULL_REQUEST_TEMPLATE.md`:

- **Title and `## Task`**: the `TASK-nnn`. PR titles put it after the type, `docs: TASK-094 publish …`. Commit
  subjects put it at the end, `docs: publish … (TASK-094)`. GitHub's auto-title from a branch name
  (`Docs/task 095 …`) does not count.
- **`## Description`**: what changed and why, naming the record or ADR section it implements.
- **`## Test evidence`**: the template's sample lines (`dotnet test src/backend ->` and so on) and its
  "n/a — no code" are placeholders. Replace them with the §5.3 commands you actually ran and their summary lines.
  On a docs-only or infra-only PR, write `n/a — no backend or frontend code changed` for the dotnet and npm lines,
  and list the checks you did run.
- **`## Security checklist`**: each item is worded so that it is either satisfied or does not apply. Tick a box
  only after checking your diff for it. If one cannot honestly be ticked, leave it unticked and say why. Never tick
  a box to make the PR look complete. The template's comments say every box must be ticked; that is the goal for a
  mergeable PR, not permission to tick one that is untrue. The first box requires `secret-scan` to be green, which
  you only know after CI runs. Open the PR with that box unticked, then tick it with `gh pr edit` once the check
  passes.

No automated check reads the description. The template is enforced by the author and the reviewer
(`branching-strategy.md` §5.1), which is why the rules above are strict.

### 6.3 What has to pass

The ruleset on `dev`, `main` and `stage` requires these 11 checks, green on a branch that is current with `dev`:
`backend`, `backend-risk-governance`, `backend-endpoint-hardening`, `backend-platform-services`,
`api-documentation`, `coverage`, `frontend`, `repo-checks`, `terraform`, `secret-scan`, `dependency-vuln-gate`.
All of them run on every PR, docs-only ones included, and take about ten minutes; `backend` is the
longest. `e2e-journeys` also reports on a PR but is not required. Every review thread must be resolved, and there is no
force-push and no bypass. The required approval count is zero while only one account has write access
(`branching-strategy.md` §7 S-4). The repository owner merges. In practice that has been a merge commit
(`Merge pull request #121 …`), although `branching-strategy.md` §2 names squash as the default.

If `dev` moves while your PR is open, merge `dev` into your branch (or rebase) and push. A stale branch cannot
merge.

Known CI noise, worth one rerun (`gh run rerun <run-id> --failed`) before you investigate:

- The backend "Unit tests" step aborts with every test passed: the test host crashed on shutdown.
- A job fails in "Initialize containers" with `toomanyrequests`: a Docker Hub rate limit. New images in CI come from
  `mirror.gcr.io/library/…` for this reason.

**Check:** `gh pr checks <number>` lists all 11 required checks as `pass`.

## 7. Controlled source documents

### 7.1 What governs what

The platform is built to a set of controlled documents. When two disagree, the higher rank wins
(`docs/baseline/controlled-source-baseline.md` §1):

| Rank | Source | Governs |
| --- | --- | --- |
| 1 | Latest issued WF-01–WF-15 / FG-01–FG-06 functional specification | Its own domain |
| 2 | Step 14A Integration & Reconciliation Review (QA CLOSED) | Cross-domain decisions (ICD-nn) |
| 3 | Functional Blueprint v2.0 (QA CLOSED) | Platform-wide structure and anything rank 1–2 leaves open |
| 4 | Adjusted Project Scope | What is in or out of the engagement |

Rules:

- **Functional Blueprint v1.0 is superseded.** It is in the package for traceability only. Never cite it.
- **Do not infer a PTBC/TBC-gated value** (an approval limit, formula, threshold, retention period, RPO/RTO) from
  any document. It comes from the governance tracker `docs/governance/ptbc-tbc-tracker.md` once AHDA approves it.
  Until then the code keeps it configurable and the record says it is open.
- Cite the highest-ranked source that answers the question, by section, ICD or screen id (for example
  `WF-06 §8`, `ICD-04`, `SCR-061`).
- Where an ADR has ratified a source decision, the ADR is the citation (`docs/architecture/adrs/ADR-register.md`).

### 7.2 Where the files are

The documents are **not in the repository**, because the repository is public. You receive them from the PMO
Engagement Lead (§1.1). Keep them outside your checkout and **never commit them, or extracts of them**, to this
repository.

| File | What is in it |
| --- | --- |
| `AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx` (live copy: the Google Sheet of §1.1) | The task workbook. Implementation Plan (one row per TASK-nnn: description, acceptance criteria, Branch, Validation Checks, Gate Decision), Environment and Secrets, Architecture Decisions, Release Checklist, Open Questions, Decision Log 19 Sep 2026 |
| `Project Files.zip` → `P1 — …` to `P9 — …` folders | The 21 WF/FG functional specifications, grouped by phase. For example `P3 — Risk, Issue, and Challenge Control/…WF-06_Risk_Management_Functional_Specification_v1.0.docx`, whose §8 holds the screen and modal specs |
| `Project Files.zip` → `Functional Blueprint/` | Blueprint v2.0 QA CLOSED (includes §22, the cyber control overlay, and Appendix A, the indicative permission matrix); the Step 14A Integration Reconciliation Review v1.0 QA CLOSED; Blueprint v1.0 (superseded) |
| `Project Files.zip` → `Front End Package/`, `Back End Package/` | Step 16 frontend package and registers (screen ids, audiences, proposed routes, accessibility rules); Step 17 backend package and registers |
| `Project Files.zip` → `RTM/`, `UAT Catalogue/`, `Prioritization/` | Step 14B traceability matrix and assessment; Step 18 UAT catalogue; Step 15 build prioritisation. These are instruments, **not** controlled sources (baseline §2.4) |
| `Project Files.zip` → `Relevant Folders/` | Supporting material, including `PM_Platform_Development_and_Cybersecurity_Guide.docx` (Appendix A: requirements CS-001–CS-033) and the client's original project scope |

The `.docx` and `.xlsx` files open in any office suite. To read one inside the zip without extracting it, use
Python's `zipfile` module.

The baseline register is still **PROVISIONAL**. It records spec revisions and file locations as pending
(`controlled-source-baseline.md` §5). The package above is where the files are today. If you find a later
revision of a spec, it outranks the one in the package; record it in the baseline in its own PR.

### 7.3 What the repository holds instead

| Path | What |
| --- | --- |
| `docs/baseline/controlled-source-baseline.md` | The register and the authority order |
| `docs/governance/` | PTBC/TBC tracker (`ptbc-tbc-tracker.md`, `.xlsx`) and its crosswalks to tasks and sources |
| `docs/rtm/step-14b-rtm-findings-closure.md` | Closure of the Step 14B RTM findings |
| `docs/architecture/adrs/ADR-register.md` | Architecture decisions and their approval status |
| `docs/architecture/*.md` | One record per task: what was built, against which source sections, and what is still open |

## 8. Your first pull request

Your first PR is your dry run of this guide. It proves you have a working stack, and it fixes the guide for the
next person.

1. Do §1 to §3 on a clean machine. Time it from the start of `git clone` to the §3.3 `curl` answering `201`, and
   write down every step where you
   needed something this guide did not tell you.
2. Branch `docs/task-095-onboarding-dry-run-<your-github-login>` off `dev`.
3. Add one row to the dry-run log in §9.2. For each step that needed outside help, fix the section that should have
   told you, in the same PR.
4. Run the `repo-checks` scripts (§5.3) and open the PR as §6 describes. Test evidence: the `repo-checks`
   lines you ran, and `n/a — no backend or frontend code changed` for dotnet and npm.
5. **Check:** all 11 checks pass, and the owner merges it.

## 9. Validation

### 9.1 How this guide was checked

On 2026-10-24 the author ran §2–§5 from a fresh `git clone` of `dev` at `e3e5a64` on macOS (Darwin 27, arm64),
Docker Desktop (engine 29.8.0, Compose 5.5.1). To keep an existing local stack untouched, the run used its own
compose project, container names and ports, with fresh volumes; the commands were otherwise those above.

| Step | Result |
| --- | --- |
| `git clone` | 7s |
| `up --build --wait` on empty volumes, warm image cache | exit 0 in 38s; `/health` → 200 `Healthy`; frontend → 200 |
| Sign-in through the frontend proxy | `local.r02` → 201; `local.r01` → 503 (MFA required, no provider), as §3.3 states |
| `dotnet build src/backend -warnaserror` | 0 errors, 0 warnings, 42s |
| `dotnet test src/backend/PMPlatform.Tests.Unit --no-build` | 2017 passed, 0 failed, 11s |
| Frontend `lint`, `format:check`, `typecheck` in the container | all exit 0, 63s together |
| Frontend `npm test` | 1052 passed on the third run. The first two runs, with three other Vite/API containers on the machine, lost 10 and then 2 tests to 5s timeouts, a different set each time (§5.3) |
| The 19 `repo-checks` scripts | all exit 0 |

Found in the dry run and fixed in this guide: an existing database whose rows fail the integrity check stops `up`
at `seed` with exit 3 (§3.5); R01 cannot sign in (§3.3); the Vitest load sensitivity (§5.3).

That was a rehearsal by the author. The workbook's validation, a dry run by someone unfamiliar with the
codebase, is the first row of §9.2.

### 9.2 Dry-run log

One row per developer who follows the guide for the first time (§8). Time runs from the start of `git clone` to
the §3.3 `curl` answering `201`.

| Date | Developer | Machine / engine | Clone → signed in | Steps that needed outside help | Fixed in |
| --- | --- | --- | --- | --- | --- |
| 2026-10-24 | AI agent with no prior context of the codebase, following only this guide (a **stand-in**, not a person) | macOS Darwin 27 arm64, Docker Desktop 29.8.0 / Compose 5.5.1. Images built cold (`build --no-cache --pull`), empty volumes, own compose project beside an existing stack, PostgreSQL/LDAP moved to 15432/13389 | **240s** (clone 26s, cold build 138s, `up --wait` 53s); stop→up 17s; `down -v`→up 56s | 13, none blocking. §1.2: port variables need `export`, and the integration tests need the moved ports. §3.1: project name `ahda` and how to run beside it. §3.2: `/api-docs` answers 301. §3.3: password location ambiguous, password typed into shell history. §3.4: frontend `Exited (1)` after `stop`; reset's "then `up`" ambiguous. §4.1: T-6 "the API" ambiguous. §4.3: `solution-architecture.md` not tied to "ADR-003"; query service unnamed; how to check §8.2 unstated. §5.3: repo-checks are not all `*-check.py` and include `migration-forward-only.sh`. §6.2: template placeholders, the "tick every box" comment, and the secret-scan box before CI; PR-title form. §8: end point of the timing | this revision (§10) |
| 2026-10-24 | Second AI stand-in with no prior context and no sight of run 1, following the revised guide only (a **stand-in**, not a person) | macOS arm64, Docker Desktop (engine 29.8.x, Compose 5.5.1). Images built cold (`build --no-cache --pull`), empty volumes, **empty NuGet cache**, own compose project beside a running `ahda` stack, PostgreSQL/LDAP on 15432/13389 | **225s** (clone 9s, cold build 138s, `up --wait` 60s); stop→up 18s; `down -v`→up 26s; cold-NuGet backend build 136s | 3, none blocking. §3.1: no example override or full command for running a second copy. §5.3: the integration command hard-codes `Port=5432` without `AD_LDAP_URL` and does not point to §1.2; the §1.2 values themselves worked. §3.4: does `smoke-test.sh` honour `-p`? | this revision (§10) |

Results of run 2: `/health` → 200; `/api-docs/` → 301 → 200; frontend → 200; `local.r02` → 201, before and after
the reset. Backend: 0 warnings, 2017 of 2017 unit tests passed. Frontend: lint, format and typecheck clean, Vitest
1052 of 1052 on the first run. Integration slice `…Integration.Identity` on the moved ports: 122 of 122 passed,
against the run's own PostgreSQL (checked in `pg_stat_database`). repo-checks: exit 0. Both §4 questions
(where Risk's project-status read goes; whether a new cross-module call needs an architecture change) were answered
correctly from the guide alone. The PR body was drafted from the template following §6.2. The run was interrupted
once when the Docker engine stopped answering; that was the machine, and the step was redone after a Docker Desktop
restart.

Results of run 1: §1 checks all answered. `/health` → 200; frontend → 200; `local.r02` → 201;
`local.r01` → 503. Backend: 0 warnings, 2017 of 2017 unit tests passed. Frontend: lint, format and typecheck exit 0;
Vitest lost 7 tests to timeouts on the first run (the maintainer's stack was running), then passed 1052 of 1052
twice. The 19 repo-checks scripts and `migration-forward-only.sh` all exit 0. Integration tests and the push were
out of scope for the run.

### 9.3 Acceptance

| Criterion | Status |
| --- | --- |
| A new developer following only this guide reaches a running local environment and submits a passing first PR without verbal handover, validated by a dry run with one developer new to the codebase | **PARTIAL.** The stand-in reached a running environment from cold images in 240s, using this guide only. Every gap it logged is fixed, and it prepared its first PR up to `git push`. It is not a person, and it did not push a PR or watch CI. The criterion reads MET when a developer new to the codebase adds a §9.2 row by following §8, with no step needing outside help (or with every step they list fixed in the same PR), and that PR passes the 11 checks and merges |
| Validation: follow the guide from a clean machine, time it, log every step that needed outside help, fix the guide | **DONE with stand-ins, two iterations** (§9.2). Run 1: 240s, 13 steps fixed. Run 2, on the revised guide with a colder machine: 225s, 3 steps, all fixed, none blocking, and every check including an integration slice green. To repeat it with a person, follow §8 |

## 10. Change log

| Date | Change | Author |
| --- | --- | --- |
| 2026-10-24 | Second validation dry run on the revised guide (§9.2 row 2, 225s, cold NuGet as well as cold images). Its 3 outside-help steps are fixed: an example override file and full command for running a second copy (§3.1); `smoke-test.sh` always acts on the default `ahda` stack (§3.4); the integration command must take the moved ports, or it reaches another stack's database (§5.3). | Documentation (TASK-095) |
| 2026-10-24 | Validation dry run by a stand-in with no prior context, from cold images (§9.2 row 1, 240s). Its 13 outside-help steps are fixed: port export and integration-test ports (§1.2); compose project `ahda` and how to run beside it (§3.1); `/api-docs/` redirect (§3.2); password read from the file, not typed (§3.3); `Exited (1)` after stop and the reset command (§3.4); T-6 wording (§4.1); ADR-003 = `solution-architecture.md`, query interfaces, how to check §8.2, a worked example (§4.3); a repo-checks command that reads the job (§5.3); template placeholders, checkbox honesty, the secret-scan box, title form, merge-commit practice (§6); timing end point (§8). Acceptance recorded as PARTIAL until a person does the run (§9.3). | Documentation (TASK-095) |
| 2026-10-24 | Initial guide: access and prerequisites, local stack, module boundaries, branching and PRs into `dev`, controlled sources and where the files are, first-PR procedure, author's rehearsal (§9.1). Dry run by a new developer pending. | Documentation (TASK-095) |

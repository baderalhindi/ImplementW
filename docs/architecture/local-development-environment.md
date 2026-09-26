# Local Development Environment

| Field | Value |
| --- | --- |
| Task | TASK-014 — Build Local Development Environment (Docker Compose) (P2 - Project Setup & Foundation) |
| Depends on | TASK-013 — Environment Variable Templates (`docs/architecture/environment-templates.md`, BUILT) |
| Record date | 2026-09-22 |
| Status | **BUILT — PROVISIONAL ON THE TASK-014 GATE.** `docker compose up` brings up PostgreSQL + API + frontend with seeded users for R01–R08 and `/health` 200; the compose file's services are stack-specific and disposable if ADR-002 is not confirmed (§2) |
| Branch | `chore/task-014-local-dev-environment` |
| Deliverables | `infra/docker/docker-compose.yml`; `infra/docker/api.Dockerfile`; `infra/docker/frontend.Dockerfile`; `infra/docker/postgres/init/01-schema.sql`, `02-seed-roles.sql`, `03-seed-local-users.sql`; `infra/docker/postgres/verify-seed.sql`; `infra/docker/smoke-test.sh`; `.dockerignore`; `docs/architecture/local-stack-check.py`; `PostgreSqlHealthCheck.cs` and the DI/package changes (§3.4); `CONTRIBUTING.md` "Local development stack"; this record |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), sheets Implementation Plan (TASK-014 row; TASK-024, TASK-027, TASK-028, TASK-030, TASK-031 rows for what this stack must not pre-empt), Environment and Secrets, as read 2026-09-21 |
| Controlled sources and authority order | per TASK-001 |

## 1. What this record is

TASK-014 makes a new developer productive without cloud access: one command, three services, data to work against. The
shape is fixed by ADR-002 (PostgreSQL 17, ASP.NET Core on .NET 10, React 19 on Vite), so this record is about the
four things that are choices — how the stack is gated on an unapproved ADR (§2), what the health check has to prove
before it may return 200 (§3.4), where the seeded users come from when the schema they need does not exist yet
(§3.2), and how a seed written against an ERD stays honest as the ERD moves (§4). §6 lists what the exercise
surfaced.

## 2. The gate

TASK-014's Gate Decision cell: *"GATED on TASK-006. The local stack cannot be containerized before the stack is
approved."* ADR-002 is **PROPOSED — PENDING AHDA APPROVAL** at the record date (§11 Q1–Q6 unanswered).

The stack was built on the task branch under the same stated assumption TASK-011 §2 records — **AHDA confirms
ADR-002 as proposed** — and for the reason ADR-002 §9 gives explicitly: *"`infra/docker` (TASK-014) keeps its path
but not its content: the compose file's services are stack-specific."* The cost of building now is therefore the
compose file and three Dockerfiles, and no later task is made harder by it:

- if ADR-002 Q1 is answered "Yes — mandated different stack", `infra/docker/*` is discarded with `src/` and
  ADR-002 §9 executes;
- if Q4 selects AlloyDB over Cloud SQL, nothing here changes — the local image is PostgreSQL either way, which is
  the point of ADR-002 §4.2.3(a);
- if S-4 forces the PostgreSQL 16 fallback, one image tag changes in `docker-compose.yml`.

What the gate protects against is *committing the engagement* to a stack through infrastructure. A compose file that
takes ten minutes to rewrite does not do that.

## 3. What was built

### 3.1 The stack

| Service | Image | Port | Health |
| --- | --- | --- | --- |
| `postgres` | `postgres:17` — the major version of every environment (ADR-002 §4.1; fallback 16 fixed at TASK-020) | `5432` | `pg_isready` |
| `api` | built from `infra/docker/api.Dockerfile` — SDK 10.0 publish stage, `aspnet:10.0` runtime stage, non-root `app` user | `5080` → 8080 (5080 matches `launchSettings.json`) | `curl --fail /health` |
| `frontend` | built from `infra/docker/frontend.Dockerfile` — `node:24-slim`, Vite dev server with `src/frontend` bind-mounted | `5173` | `fetch('/')` |

The compose project is **`ahda`**, and the three containers are `AHDA-postgres`, `AHDA-api` and `AHDA-frontend`,
so the stack is identifiable as AHDA in `docker ps` and in any Docker UI. Compose rejects an uppercase project
name (`must consist only of lowercase alphanumeric characters...`), which is why the project itself is lowercase
while the container names — which carry no such restriction — are not. The project name also prefixes the
volumes (`ahda_postgres-data`, `ahda_frontend-node-modules`) and the network (`ahda_default`).

Each published port is overridable (`PMPLATFORM_API_PORT`, `PMPLATFORM_FRONTEND_PORT`, `PMPLATFORM_POSTGRES_PORT`)
for a developer who already has something on 5432. `api` starts only once `postgres` is healthy
(`depends_on: condition: service_healthy`), so `--wait` returns when the whole stack is actually usable, not when
the processes have started.

The frontend image is **local only**: it runs `npm run dev`, and the deployable frontend is static assets from
`npm run build` (ADR-002 §4.1). `node_modules` is installed into the image and kept in a named volume, so the
bind mount cannot drag a host `node_modules` — built on a different platform — into the container.

### 3.2 The seed, and the schema it needs

TASK-014's criterion is "seeded data includes at least one user per role R01-R08". Since TASK-025 the schema comes
from the EF Core migrations: compose's one-shot `migrate` service runs the API image's `migrate` command once
`postgres` is healthy, then the one-shot `seed` service runs, with `psql` in one transaction, TASK-027's platform
seed `db/seed/seed-master-data.sql`, the local users `infra/docker/postgres/seed/seed-local-users.sql` and TASK-027's
`db/seed/validate-data-integrity.sql`; only then does `api` start. Both one-shots exit 0 on every `up` (the migrations
are already applied; the seeds are idempotent). The table below is the original TASK-014 layout: `01-schema.sql` is
deleted (TASK-025), `02-seed-roles.sql` is replaced by `db/seed/seed-master-data.sql` (TASK-027), and
`03-seed-local-users.sql` is now `seed/seed-local-users.sql`, which no longer creates the SERVICE principal or the
external-entity type — the platform seed does.

| Script | What it does | Whose job it really is |
| --- | --- | --- |
| `01-schema.sql` | **Bridge schema.** Nine tables of the canonical ERD — `department`, `master_data_catalogue`, `master_data_item`, `role`, `user`, `external_entity`, `permission_profile`, `permission_profile_version`, `access_relationship` — transcribed from `erd.dbml` column for column, with the foreign keys between them. | TASK-025 (TASK-024 built the framework and the module schemas, not these tables). **This file is deleted when TASK-025 lands**, and compose runs the API image's `migrate` command in its place. §4 check 3 is what keeps it honest until then. |
| `02-seed-roles.sql` | The eight canonical roles R01–R08 (`is_system`, `is_external_eligible` on R04 and R08 per ADR-013), each with its shipped-default `permission_profile` and one PUBLISHED version. | TASK-027 (`db/seed`), whose description names "the 8 canonical roles (R01-R08)". Interim copy, idempotent in TASK-027's own upsert style. |
| `03-seed-local-users.sql` | One department, one external entity and its master-data type, a SERVICE principal, and **eight local users bound to the eight default profile versions** — R03 carrying the DEPT anchor, R08 the ENTITY anchor and its named AHDA sponsor (ADR-013). | Nobody: these are synthetic local accounts, never promoted to DEV/SIT/UAT/PROD. |

Three decisions inside that:

- **The users are data, not accounts.** They carry no credential of any kind. Sign-in is SSO (ADR-007) and a local
  sign-in path is TASK-028's test directory; until then these rows are what a developer's queries, fixtures and
  authorization tests point at. `pmplatform.local` addresses are unroutable by construction.
- **Assignments bind to a profile version, not to a role** (ADR-018). Binding the seed directly to `role.id` would
  have been shorter and would have modelled the thing ADR-018 replaced.
- **No permission grants.** `permission_profile_grant` needs the protected permission catalogue, which is
  TASK-030/TASK-110's. A seeded guess at Appendix A's matrix would be a decision taken in a SQL file.

Role **labels** (`name_ar`/`name_en`) are provisional — see F-1.

### 3.3 No secret is baked in

The compose file carries four local values: the database name, user and password (`pmplatform`), the connection
string built from them, and `JWT_SIGNING_KEY`, whose value is the literal
`local-development-only-signing-key-not-valid-outside-docker-compose`. These are the two variables the workbook's
TASK-014 row names, in the classification it names them: *"DB_CONNECTION_STRING (local default, non-secret),
JWT_SIGNING_KEY (local dev key only)"*. Nothing in the file is valid anywhere else, and no SIT/UAT/PROD value may
ever be written here — those are injected from the secret store (TASK-016, TASK-019).

This does not weaken TASK-013 CTL-18. `appsettings.Template.json` and `.env.example` still hold **names only**; the
`env-template-check.py` gate is unaffected and still green (§4 check 6). Compose is not a template — it is the
environment, and the environment is where a value belongs.

The variable names are the sheet's, verbatim (TASK-013 R-1), so `DB_CONNECTION_STRING` in the compose file is the
key `Program.cs` reads. The API is given the six DEV-scoped variables this stack can satisfy
(`DB_CONNECTION_STRING`, `JWT_SIGNING_KEY`, `CORS_ALLOWED_ORIGINS`, `APP_BASE_URL`, `LOG_LEVEL`,
`APM_ENVIRONMENT_TAG`) and the frontend the two `VITE_` rows that need no external system; the rest stay unset and
the API starts without them (TASK-013 §3.2).

### 3.4 `/health` had to be made worth checking

The acceptance criterion is "a health-check endpoint returns 200 within 2 minutes". Before this task `/health`
returned 200 from `AddHealthChecks()` with no registered check — it proved the process was listening and nothing
else. A criterion satisfied by a stack whose database is unreachable is not a criterion.

`PMPlatform.Infrastructure/Persistence/PostgreSqlHealthCheck.cs` opens a connection using
`configuration["DB_CONNECTION_STRING"]` and runs `SELECT 1`; it is registered as check `postgresql` in
`AddInfrastructure`. `/health` is therefore 200 only when the API has reached the database, and 503 when it has
not (§4 check 4). Consequences, all intended:

- The check lives under `Infrastructure/Persistence`, where architecture rules A-2 and A-5 require data access to
  be. `Npgsql` and `Microsoft.Extensions.Diagnostics.HealthChecks` are added to `Directory.Packages.props` and
  referenced by the Infrastructure project only.
- The connection string is read per check, not captured at startup, so a reloaded
  `appsettings.{Environment}.Local.json` takes effect without a restart — the behaviour TASK-013 §3.3 built.
- A missing `DB_CONNECTION_STRING` reports Unhealthy with that sentence rather than throwing.

This is the local stack's health check, not the production one: the readiness/liveness split, dependency checks for
the document store and SMTP, and the response contract belong to TASK-090/TASK-091.

## 4. Verification

Run on macOS 15.6 (Darwin 24.6.0, arm64) with Docker CLI 29.8.1 and Docker Compose 5.5.1, against two engines:
**Docker Desktop 4.92 (server 29.8.0)** and **podman 6.1.2** (`applehv` VM, reached through `DOCKER_HOST`). Not run
on Linux Docker Engine (F-6). Timings differ by engine and are given per engine; everything else behaved
identically on both.

| # | Check | Result |
| --- | --- | --- |
| 1 | **Cold machine, podman.** Every image removed, build cache and volumes pruned, then `infra/docker/smoke-test.sh` | `built in 70s` (base images pulled, `dotnet restore` and `npm ci` from empty caches), `all services healthy after 26s`, `GET /health -> 200 Healthy`, `GET / -> 200`, eight roles with an active local user. 96s total |
| 1b | **Cold machine, Docker Desktop.** Same script on a fresh Docker Desktop install with its own empty image store | `built in 108s`, `all services healthy after 52s`, `/health -> 200 Healthy`, `/ -> 200`, eight roles. **52s to healthy — the criterion is met**, but 160s in total, so a first-ever build on Docker Desktop exceeds two minutes if the criterion is read as including it (§5) |
| 2 | Warm repeat: `smoke-test.sh` with images and caches present | `built in 3s`, healthy in 12s — what a developer waits for on a normal start |
| 3 | `python3 docs/architecture/local-stack-check.py` — every column of the bridge schema against `erd.dbml`: same tables, columns, order, types, nullability, defaults | `OK: 9 bridged tables, 124 columns match erd.dbml.` Mutation-tested: a widened `varchar` and a deleted column were each reported individually, exit code 1; restored, check green |
| 4 | Negative: `docker compose stop postgres`, then `GET /health` | `503 Unhealthy`; after `start postgres`, `200 Healthy` again — the endpoint tracks the database, not the process |
| 5 | Idempotency: re-run `02-` and `03-` against the seeded database | row counts unchanged — 8 roles, 8 profiles, 8 versions, 9 users (8 + the SERVICE principal), 8 assignments — before and after |
| 6 | Seed contents: `infra/docker/postgres/verify-seed.sql` | One ACTIVE user per R01–R08, each through a shipped-default profile version; R03 has the DEPT anchor, R08 the ENTITY anchor and `user_type = EXTERNAL`. The script raises (psql exits non-zero) if any role has no active user |
| 7 | Frontend bind mount: edit `src/frontend/src/App.tsx` on the host | the change is in the container within 2s and Vite serves the edited module; reverting removes it |
| 8 | `dotnet build src/backend -warnaserror`; `dotnet test src/backend` | 0 warnings, 0 errors; 25 passed — the architecture suite (A-1…A-6) accepts the health check's placement |
| 9 | `python3 docs/architecture/env-template-check.py`; `python3 docs/architecture/erd-check.py` | both green — the templates and the ERD are unchanged by this task |
| 10 | `gitleaks detect --no-git` over `infra/docker`, `.dockerignore` and the Infrastructure project | `no leaks found` — the local password and the labelled dev key are not detected as secrets, and there is nothing else to find |

## 5. Acceptance criteria

| # | Criterion | Status |
| --- | --- | --- |
| 1 | `docker compose up` on a clean machine brings up DB + API + frontend and a health-check endpoint returns 200 within 2 minutes | **MET** — 26s (podman) and 52s (Docker Desktop) to all-healthy, on both engines from an empty image store; `/health` 200 and it means the database is reachable (§3.4). **One reading is not met:** if the two minutes is taken to include the first-ever image build, a cold Docker Desktop run is 160s (108s build + 52s). Every subsequent start — which is what a developer actually repeats — is 12s |
| 2 | Seeded data includes at least one user per role R01–R08 for local testing | **MET** — checks 5, 6: eight users, one per canonical role, each bound to that role's shipped-default profile version |
| — | Validation cell: run from a clean clone on a machine with no prior state; verify API health endpoint and frontend both respond; confirm no production secret is baked into the compose file | **MET** — check 1 (no images, no volumes, no caches), checks 1 and 10; §3.3 states what the four local values are and why they are not secrets |

## 6. Findings and open items

| ID | Finding | Owner / where it goes |
| --- | --- | --- |
| **F-1** | **Role labels are provisional.** The Blueprint's Appendix A role list is not in the repository, so `name_ar`/`name_en` for R01–R08 follow the dashboard inventory DSH-001–008 and the two roles the workbook names outright (R01 System Administrator, R04 Project Manager). The codes R01–R08 are canonical and nothing keys on a label, so the correction is a label update. | TASK-027, from the controlled source |
| **F-2** | **CLOSED by TASK-025 (2026-09-25):** `01-schema.sql` and `local-stack-check.py` deleted; compose migrates, then seeds (§3.2); see `core-platform-schema.md` §7. Original finding: **The bridge schema must be deleted, not migrated.** `01-schema.sql` exists only because no migration creates its nine tables yet. TASK-024 built the migration framework and the module schemas; the tables are TASK-025's. When TASK-025 lands, delete the file, run the API image's `migrate` command in compose (`database-migrations.md` §3), and keep `02-`/`03-` as DML. Leaving it would give the local stack a second schema definition — exactly the drift `local-stack-check.py` exists to detect in the meantime. | TASK-025 (handed over by TASK-024, 2026-09-25) |
| **F-3** | `JWT_SIGNING_KEY` is supplied to the API and read by nothing: no code issues a token yet. It is set now because the workbook's TASK-014 row names it and because the variable must be present the day TASK-028 starts. | TASK-028 |
| **F-4** | **The seeded users cannot sign in**, by design (§3.2). A developer testing an authenticated path before TASK-028 needs a local token-issuing stub; that stub is TASK-028/TASK-029 scope and must never ship outside `Development`. | TASK-028 |
| **F-5** | The shipped-default profile versions carry **no permission grants** — the catalogue is TASK-030/TASK-110's. An authorization test written against this seed today asserts on role identity only. | TASK-030, TASK-110 |
| **F-6** | **Docker Desktop verified; Linux Docker Engine not.** The stack was first built against podman, then re-run unchanged on Docker Desktop 4.92 for macOS (check 1b) — same result, no file changed. Windows (WSL2) and Linux Docker Engine are still unverified; the `src/frontend` bind mount and the `postgres` init-script mount are where a platform difference would show. Run `infra/docker/smoke-test.sh` once on each before the onboarding claim is made unconditionally. | DevOps/Platform Lead, before TASK-015 |
| **F-7** | `local-stack-check.py` is run by hand, like `erd-check.py`, `contract-check.py` and `env-template-check.py`. Wiring the four into CI is TASK-015's gate scope. | TASK-015 (see TASK-013 F-7) |
| **F-8** | The stack serves **plain HTTP**; the API logs `Failed to determine the https port for redirect` because `UseHttpsRedirection` has no HTTPS port in the container. Harmless locally and correct — TLS terminates at the environment's ingress (TASK-016) — but the middleware's behaviour in a container is worth settling when the deployment shape is. | TASK-016, TASK-078 |
| **F-9** | **The SPA does not call the API in this stack.** It cannot: no variable names the API origin for the frontend (TASK-013 F-2, still open). `CORS_ALLOWED_ORIGINS` is set to `http://localhost:5173` in anticipation and is consumed by nothing until TASK-078. When F-2 is decided, add the variable to the sheet, then to `.env.example`, then here. | TASK-016 (decision), TASK-078 |

## 7. Change log

| Date | Change | Author |
| --- | --- | --- |
| 2026-09-26 | TASK-027 replaces the interim roles script with the platform seed `db/seed/seed-master-data.sql`, renames the local-users script to `seed/seed-local-users.sql` (it now relies on the platform seed for the SERVICE principal and the external-entity type), and ends the `seed` service with `db/seed/validate-data-integrity.sql`, all in one transaction. Run twice on the existing local volume: exit 0 both times, no violation; `verify-seed.sql` still finds one active user per R01–R08 (`seed-data-and-integrity.md` §5). | Database (TASK-027) |
| 2026-09-25 | TASK-025 closes F-2. Bridge schema and `local-stack-check.py` (check 3, and its CI step) deleted; the migrated schema is compared with `erd.dbml` by `CoreSchemaTests` instead. Compose gains the one-shot `migrate` and `seed` services; the seed scripts move to `postgres/seed`. The API's `DB_CONNECTION_STRING` host goes back to `postgres`, the service name. `smoke-test.sh` from a reset volume: healthy in 14s, `/health` 200, one user per R01–R08. | Database (TASK-025) |
| 2026-09-25 | F-2 and the `01-schema.sql` row handed from TASK-023/TASK-024 to TASK-025. TASK-024 created the module schemas but none of the nine bridged tables, so the bridge stays until TASK-025 migrates them. | Database (TASK-024) |
| 2026-09-22 | Re-run unchanged on Docker Desktop 4.92 (check 1b): healthy in 52s, all checks green. F-6 narrowed to Windows and Linux Docker Engine. Cold-build reading of criterion 1 stated explicitly (§5). | Architecture (TASK-014) |
| 2026-09-22 | Initial record. Three-service compose stack (PostgreSQL 17, API on .NET 10, Vite dev server) with health-gated start-up; bridge schema transcribing nine ERD tables, canonical roles R01–R08 with shipped-default profile versions, and one local user per role (§3.2); `/health` backed by a real PostgreSQL probe (§3.4); `smoke-test.sh` and `local-stack-check.py` with mutation check. Verified from a cold machine at 70s build + 26s to healthy (§4). Built under the ADR-002 gate on the assumption the stack is confirmed as proposed (§2). Nine findings (F-1…F-9). | Architecture (TASK-014) |

# ADR-002 — Application Technology Stack

| Field | Value |
| --- | --- |
| Task | TASK-006 — Ratify Application Technology Stack ADR (P1 - Architecture Decisions) |
| Depends on | TASK-005 — Resolve Hosting & Data Localisation Architecture Decision Record (`docs/architecture/adrs/ADR-001-hosting-and-data-localisation.md`, APPROVED — CONDITIONAL) |
| Record date | 2026-09-20 |
| Decision status | **PROPOSED — PENDING AHDA APPROVAL.** Not answered at the AHDA decision gate of 19 Sep 2026. The TASK-006 validation check requires this label until AHDA confirms; every Canonical File Directory value in the workbook is provisional on that confirmation (§9) |
| Decision owner | Engagement Architect (proposer) → AHDA IT (confirming authority, §11) |
| Branch | `chore/task-006-task-006-stack-adr` |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), sheets Implementation Plan (112 task rows, TASK-001–TASK-112), Architecture Decisions (ADR-001–ADR-013), Environment and Secrets, Release Checklist, Open Questions, as exported 2026-09-20 |
| Controlled sources and authority order | per TASK-001 |

## 1. What this record is

The Architecture Decisions sheet already holds an ADR-002 row, and that row already names a stack. It names it as a *working proposal*, and its own Rationale cell says why it exists: Option A "is carried as the working baseline **solely to give this workbook concrete, navigable Canonical File Directory values**; it is not yet an AHDA-approved decision."

So the workbook's entire `src/` directory column rests on a decision nobody has taken. That is the position this record is written from. TASK-006 asks to "ratify (or replace)" the proposal, and this record ratifies it — with three points settled that the register row leaves as labels rather than decisions, and with the directory scheme that follows from it written down and audited.

This record therefore does three things the register row does not:

1. **Settles the three points Option A leaves open** — which .NET LTS version, which managed PostgreSQL product, and which frontend and test toolchain (§4.2). Each is currently an unresolved "or", an "(LTS)", or an "e.g." somewhere in the workbook, and each blocks a specific task.
2. **Writes down the directory rule and the module registry** that every Backend/Frontend/Database row's directory column derives from (§6). Until now that rule existed only as a pattern visible across 73 cells.
3. **Audits all 112 task rows against it** and names the 11 that do not conform (§7), which is what the TASK-006 acceptance criterion asks for.

Per the TASK-006 validation check, the status stays **Proposed — Pending AHDA Approval**. This record does not approve itself. §11 is what AHDA IT signs.

### 1.1 One correction to the gate note

The TASK-006 Gate Decision Applied cell says ADR-002 "is now the single critical-path item for starting code: TASK-011 and TASK-014 cannot begin until it is approved."

ADR-002 is **necessary but not sufficient.** Two other cells in the same workbook say so:

- TASK-011's own Gate Decision Applied cell: *"GATED on TASK-006 **and** TASK-007. Do not initialize the monorepo before ADR-002 **and ADR-003** are approved — the directory layout depends on them."*
- The Release Checklist gate "Architecture Decisions Approved": *"ADR-002 application technology stack **and ADR-003** solution architecture pattern remain PROPOSED and are the **only remaining blockers** to this gate — and to starting code."*

ADR-002 selects the backend platform; ADR-003 decides the modular-monolith pattern and the module list. The path `src/backend/PMPlatform.Application/Features/<Module>` needs both: this record fixes the prefix, ADR-003 fixes `<Module>`. Approving this record alone leaves TASK-011 blocked. TASK-007's gate note already prescribes the remedy — *"Pair it with TASK-006 in one AHDA IT session"* — and that remains the correct sequence. §7.3 lists six module names where the two currently disagree.

## 2. Scope — what this ADR decides and what it does not

| | Subject | Owner |
| --- | --- | --- |
| **Decides** | Frontend language, framework, build tool and test tooling | This record |
| **Decides** | Backend language, framework and **runtime version**, ORM and migration tool, test framework | This record |
| **Decides** | Database engine and major version; the managed product (closing ADR-001 R-7) | This record |
| **Decides** | The repository layout, the directory derivation rule, and the canonical directory value for every task row | This record |
| **Does not decide** | The modular-monolith pattern and the 21-module boundary set | **ADR-003 / TASK-007** |
| **Does not decide** | Where the platform runs, the region, the tenancy, data localisation | **ADR-001** (approved, conditional) |
| **Does not decide** | **Compute runtime — Cloud Run or GKE** (ADR-001 R-6) | See §4.3 |
| **Does not decide** | Identity provider, notification channels, export formats, report counts | ADR-004 to ADR-013 |

## 3. Options considered

### 3.1 As recorded in the register

| Option | As recorded in the ADR-002 row | Assessment |
| --- | --- | --- |
| **A** | "React + TypeScript SPA frontend / ASP.NET Core (LTS) modular-monolith backend / PostgreSQL (the brief's working proposal)" | **Selected.** The only option with any detail behind it. It is also the option the workbook has already been written against: 73 of 112 task rows carry a directory value that presupposes it, and TASK-011's description names the four .NET project names and Vite explicitly. Ratifying it costs nothing; replacing it costs the re-issue in §9. |
| **B** | "An AHDA-mandated stack, if AHDA IT standards require a specific platform not yet stated to the delivery team" | **Not an option — a question.** Option B has no content. It is the possibility that AHDA IT holds a platform standard the delivery team has not been told about. It cannot be evaluated against Option A because nothing in it is specified. It is resolved by asking, which is Q1 in §11. If the answer is yes, Option A falls and §9 executes. |

The register's option set does not name a single concrete alternative to Option A, so "alternatives considered" is not satisfied by the row as it stands. §3.2 records the alternatives.

### 3.2 Per-layer alternatives

These were **not formally evaluated at the 19 Sep 2026 gate** — the gate did not reach ADR-002 at all. They are recorded here so that ratification is an informed decision rather than a rubber stamp, with the reason each is not selected.

| Layer | Alternative | Why not selected |
| --- | --- | --- |
| Backend | Java + Spring Boot | Equivalent on capability and on the modular-monolith pattern. Not selected: no stated AHDA precedent, and no advantage over ASP.NET Core that would justify discarding the working proposal and the 73 directory values built on it. |
| Backend | Node.js + NestJS | Would unify the language across both tiers. Not selected: a 21-module monolith with heavy relational reporting is better served by a statically typed, mature ORM stack; and the workbook's backend layout (`.Api`/`.Application`/`.Domain`/`.Infrastructure`) is a .NET solution convention. |
| Frontend | Angular | Closer to the .NET enterprise idiom and opinionated by default. Not selected: no advantage for this screen inventory, and the workbook already names React and Vite. |
| Frontend | Next.js (SSR/SSG) | Not selected: the platform is an authenticated internal application behind SSO with no public, indexable surface. Server-side rendering adds a Node runtime to the deployment topology — a second runtime to host, patch and localise under ADR-001 — for no benefit here. The SPA calls the API (API-01); that is the architecture the Blueprint states. |
| Database | Microsoft SQL Server | Natural pairing with .NET. Not selected: ADR-001 places the platform on Google Cloud, where PostgreSQL is the first-class managed relational engine; licensing cost is avoidable; and every workbook cell that names a database names PostgreSQL. |
| Database | MySQL | Not selected: weaker support for the features this schema needs (rich constraint and index behaviour, JSON with indexing, window functions in reporting queries). |
| Database | Oracle | Not selected: cost and operational weight out of proportion to 150 named users; no stated requirement. |

## 4. Decision

### 4.1 The stack

**Option A, ratified, with versions and products named.**

| Layer | Selection | Pinned where |
| --- | --- | --- |
| **Frontend language** | TypeScript 5.x, `strict: true` | `tsconfig.json` (TASK-011); enforced by `tsc --noEmit` in CI (TASK-015) |
| **Frontend framework** | React 19 (SPA; no server-side rendering) | `package.json` (TASK-011) |
| **Frontend build tool** | Vite | `package.json` (TASK-011) — named in TASK-011's description |
| **Frontend lint/format** | ESLint + Prettier | TASK-011, gated in CI by TASK-015 |
| **Frontend unit tests** | Vitest, colocated with the feature | TASK-015 |
| **Frontend e2e** | **Playwright** (resolves TASK-085's "Playwright/Cypress") | TASK-085, `src/frontend/e2e` |
| **Node.js (build/test toolchain only)** | Active LTS line — Node.js 24 LTS at the record date | `.nvmrc` + `engines` (TASK-011). Not a production runtime: the SPA ships as static assets. |
| **Backend language** | C# | — |
| **Backend framework** | ASP.NET Core on **.NET 10 (LTS)** | `global.json` SDK pin (TASK-011) |
| **Backend analysis** | `.editorconfig` + Roslyn analyzers, warnings-as-errors | TASK-011; TASK-011's acceptance criterion is a zero-warning build |
| **Backend unit tests** | xUnit | TASK-015 |
| **ORM + migrations** | Entity Framework Core with the Npgsql provider, code-first, forward-only, mandatory down-scripts (resolves TASK-024's "e.g.") | TASK-024 |
| **Database engine** | **PostgreSQL 17** (fallback 16 — §4.2.3), identical major version in all four environments and in the local Docker image | TASK-020, TASK-014 |
| **Managed product** | **Cloud SQL for PostgreSQL** unless AHDA IT requires AlloyDB (§4.2.3) | TASK-020 |
| **API contract** | OpenAPI, generated from the code in CI, never hand-maintained | TASK-094 |
| **Repository** | Single monorepo: `src/backend`, `src/frontend`, `db/`, `infra/`, `docs/`, `.github/` | TASK-011, §6 |

Version numbers are the current stable or LTS lines at the record date. TASK-011 pins exact versions in `global.json`, `package-lock.json` and `.nvmrc`, and confirms at pin time that each is still supported.

### 4.2 The three points Option A left open

#### 4.2.1 "ASP.NET Core (LTS)" is not a version — selected: .NET 10 (LTS)

"(LTS)" names a support policy, not a release. TASK-011's acceptance criterion is `dotnet build` with **zero warnings on a clean checkout**, which is not reproducible without a pinned SDK, and TASK-015 makes that build a branch-protection gate. A version has to be chosen before the first commit.

**.NET 10** is the LTS line to build on:

- **.NET 8's LTS support ends in November 2026** — within weeks of the date TASK-011 would start. Starting a ~22-month operations engagement on a runtime that leaves support before UAT is not defensible, and the Blueprint's secure-SDLC requirement (Section 22.1, cited by the penetration-test release gate) would be breached on day one.
- **.NET 10's LTS support runs to November 2028**, which covers the build and the early operations period.
- A non-LTS (STS) line is excluded outright: an 18-month support window inside a 22-month operations period guarantees an unplanned upgrade.

**Consequence to plan for, not to discover:** .NET 10 support ends in November 2028. If go-live is 2027, the operations period outlives the runtime. One in-support LTS upgrade must be budgeted inside the operations period rather than treated as a defect when support lapses. This is registered as S-2 and belongs in the operational handover package (TASK-096/097).

#### 4.2.2 The frontend and test toolchain had unresolved "or"s — resolved

ADR-001 §10.4 made the point in general terms: an unresolved "or" that no one owns survives indefinitely and blocks the task that has to implement it. Two of them sit inside this ADR's scope:

- **TASK-085's "Playwright/Cypress"** — selected: **Playwright**. It covers Chromium, Firefox and WebKit from one runner, has first-class TypeScript typings, and its trace viewer and CI sharding match TASK-085's requirement for a nightly suite against SIT with visible pass/fail history. Cypress is a capable alternative; the point is that `src/frontend/e2e` cannot be authored against "or".
- **TASK-024's "e.g. EF Core Migrations"** — selected: **EF Core Migrations**, as the example named. This is not a free choice: TASK-024's own acceptance criterion is `dotnet ef database update`, which is EF Core's CLI. The "e.g." is removed so the deliverable matches the criterion. It also fixes where migrations live (§7.2, TASK-089).

#### 4.2.3 The managed PostgreSQL product — closing ADR-001 R-7

The workbook names the database three ways. ADR-001 §10.1 recorded this and assigned it here (R-7):

| Where | Names |
| --- | --- |
| ADR-002, Selected Option | **PostgreSQL**, unqualified |
| TASK-005 and TASK-006 descriptions; TASK-020 task name; TASK-023/TASK-024 dependency references; `DB_CONNECTION_STRING` in Environment and Secrets | **AlloyDB** for PostgreSQL |
| ADR-001, Options Considered (Option B) | **Cloud SQL** for PostgreSQL |

**Decision, in two parts.**

**(a) The engine is PostgreSQL 17, and the application is written to standard PostgreSQL only.** No product-specific SQL, extension or feature is used without a revision of this ADR. This is the load-bearing part: it makes the managed product an infrastructure choice at TASK-020 rather than a code dependency, it keeps TASK-014's local Docker PostgreSQL a faithful copy of production behaviour, and it means a reversal costs a Terraform change rather than a rewrite. If PostgreSQL 17 is not offered by the selected product in the region named under ADR-001 R-2, the fallback is 16, fixed at TASK-020 and applied identically to the local image.

**(b) The selected product is Cloud SQL for PostgreSQL**, unless AHDA IT requires AlloyDB:

- **Nothing in the workbook states a requirement AlloyDB answers.** TASK-020's own description asks for encryption at rest, TLS-enforced connections, automated backups and point-in-time recovery sized to RPO 1 hour. Every one of those is a Cloud SQL capability. AlloyDB appears in the task *title* and in four reference cells; it is never justified in any body text.
- **The stated capacity does not call for it.** The workbook's working baseline is 150 named users, 25 concurrent, 50 at peak (Release Checklist, "Performance/Load Test Within Target"; OQ-010). AlloyDB's minimum billable cluster is materially larger than the smallest viable Cloud SQL instance, and ADR-001 C-7 puts **four** environments in scope, not one.
- **Regional risk.** ADR-001 §7 requires every managed service to be confirmed available in the named region *before* the region is fixed — and the region is still unnamed (ADR-001 R-2). AlloyDB's regional footprint is narrower than Cloud SQL's. Choosing the product with the narrower footprint against an unknown region adds avoidable risk to the critical path.

This is the one point where this record departs from the workbook's prevailing label. Either way the workbook must be re-issued, because the register's own ADR-002 row says "PostgreSQL" and ADR-001's Option B says "Cloud SQL": the six cells do not agree today and cannot all be right. The edits are in §7.2 and are put to AHDA as Q4 in §11. If AHDA requires AlloyDB, rule (a) means the **code-level consequence is nil**; the consequences are cost and regional availability, and they are AHDA's to accept.

### 4.3 Explicitly not decided here: the compute runtime

ADR-001 R-6 records that "Cloud Run/GKE" survives as an unresolved pair in every cell that mentions it, with no owner, and ADR-001 §10.4 proposed either a new ADR row or extending ADR-002's scope to cover it.

**This record does not take it**, for a reason and not by omission:

- It is a **hosting** decision, not an application-technology one. It is constrained by the region, the tenancy and the organization policy set — all ADR-001's subject matter — not by the language or framework.
- **The stack is unaffected either way.** Both are container runtimes. ASP.NET Core on .NET 10 produces the same container image for both; TASK-014 containerises the stack locally regardless; `CONTAINER_REGISTRY_TOKEN` in Environment and Secrets already presumes a container artifact. Nothing in §4.1 or §6 changes with the answer.
- It is **already in flight**: the ADR-001 confirmation request puts it to AHDA IT as **Q4**, issued 2026-09-20 and awaiting response.

R-6 therefore stays with ADR-001 and its confirmation request. Whether it ends as an ADR-001 amendment or its own register row is for the Engagement Architect and AHDA IT (§11 Q6). It is recorded here so that "ADR-002 extends to cover it" is not assumed to have happened.

## 5. Rationale

1. **The RFP does not prescribe a stack.** The register states this as the premise of ADR-002. There is therefore no contractual constraint to reconcile — unlike ADR-001, which reconciles one. The only external constraint is any AHDA IT platform standard the delivery team has not been told about, which is Option B and is a question, not an option (§3.1, Q1).
2. **The stack must suit a modular monolith, not a distributed system.** ADR-003 selects one deployable with 21 bounded modules for 150 named users. ASP.NET Core with EF Core over a single PostgreSQL instance is a direct fit: one solution, one database, transactional consistency across modules without distributed transactions, and one artifact to deploy and roll back (TASK-098–TASK-102).
3. **The three-tier rule constrains the frontend shape.** API-01 requires that the frontend never writes to the database directly. A React SPA calling the API over HTTPS satisfies this structurally — there is no server-side data path in the frontend tier to misuse. This is also why SSR is excluded (§3.2).
4. **ADR-001 constrains the database and nothing else.** The platform is on Google Cloud, in-Kingdom, with all data, backups and replicas inside Saudi Arabia. That makes managed PostgreSQL the natural engine and makes regional product availability a live constraint (§4.2.3). It places no constraint on the application language or framework.
5. **Ratifying costs nothing; replacing costs the re-issue.** 73 of 112 task rows already carry a directory value derived from Option A. This is not an argument that Option A is right — a wrong stack cheaply adopted is still wrong — but it is the accurate statement of what a replacement costs, and AHDA is entitled to it before answering Q1. §9 states the mechanics.
6. **The unresolved sub-decisions were the actual blocker, not the stack.** "ASP.NET Core (LTS)" cannot be installed; "PostgreSQL / AlloyDB / Cloud SQL" cannot be provisioned; "Playwright/Cypress" cannot be imported. Ratifying Option A without settling §4.2 would have produced an approved ADR that still did not let TASK-011 start.

## 6. Consequences — the canonical directory scheme

This section is what TASK-006 means by *"use it as the canonical basis for every Canonical File Directory value in this workbook."* It is the authority for that column.

### 6.1 Repository layout

```
/                                   TASK-011 — repo root, .editorconfig, CONTRIBUTING.md
├── .github/                        TASK-012 — CODEOWNERS, PR template
│   └── workflows/                  TASK-015, TASK-018, TASK-022, TASK-080 — CI/CD
├── db/
│   └── seed/                       TASK-027 — SQL seed and data-integrity scripts
├── docs/                           stack-independent (see §9)
├── infra/
│   ├── docker/                     TASK-014 — docker-compose.yml, local stack
│   ├── environments/               TASK-016
│   ├── secrets/                    TASK-019
│   └── terraform/                  TASK-017, /network TASK-021, /database TASK-020
└── src/
    ├── backend/
    │   ├── PMPlatform.Api/                    controllers, middleware, health checks
    │   ├── PMPlatform.Application/
    │   │   ├── Common/                        cross-cutting concerns
    │   │   └── Features/<Module>/             one folder per ADR-003 module
    │   ├── PMPlatform.Domain/                 entities, value objects, domain rules
    │   ├── PMPlatform.Infrastructure/
    │   │   ├── Identity/                      AD/LDAP, SSO, Nafath adapters
    │   │   ├── Notifications/                 email, SMS channel adapters
    │   │   └── Persistence/Migrations/        EF Core migrations
    │   ├── PMPlatform.Tests.Unit/             xUnit unit tests
    │   └── PMPlatform.Tests.Integration/      xUnit integration tests
    └── frontend/
        ├── e2e/                               TASK-085 — Playwright suite
        └── src/
            ├── content/help/                  in-app help content
            └── features/<module>/             one folder per module, kebab-case
```

The four backend project names are TASK-011's own (`PMPlatform.Api`, `.Application`, `.Domain`, `.Infrastructure`); `.Tests.Unit` and `.Tests.Integration` are added by this record (§6.5).

### 6.2 Directory derivation rule, by kind of task

| Task kind | Canonical directory |
| --- | --- |
| Backend — domain feature | `src/backend/PMPlatform.Application/Features/<Module>` (§6.3) |
| Backend — cross-cutting concern | `src/backend/PMPlatform.Application/Common/<Concern>` |
| Backend — entity, value object, domain rule | `src/backend/PMPlatform.Domain/<Area>` |
| Backend — API surface, middleware, health checks | `src/backend/PMPlatform.Api/<Area>` |
| Backend — external adapter (identity, messaging, storage) | `src/backend/PMPlatform.Infrastructure/<Area>` |
| Backend — schema change | `src/backend/PMPlatform.Infrastructure/Persistence/Migrations` |
| Backend — unit tests | `src/backend/PMPlatform.Tests.Unit/<Area>` |
| Backend — integration tests | `src/backend/PMPlatform.Tests.Integration/<Area>` |
| Frontend — feature UI | `src/frontend/src/features/<module>` (§6.3) |
| Frontend — end-to-end tests | `src/frontend/e2e` |
| Full-stack task | **Both** paths, comma-separated — the form TASK-013 already uses |
| SQL seed / data scripts | `db/seed` |
| Infrastructure as code | `infra/terraform/...`, `infra/docker`, `infra/environments`, `infra/secrets` |
| CI/CD pipeline | `/.github/workflows` |
| Document deliverable | `docs/<area>` |

### 6.3 Module registry — the 21 modules and their directories

`<Module>` and `<module>` are not free text. They are this table, which is the canonical form of ADR-003's module set. A new module requires a revision of ADR-003 **and** of this table.

| # | Module (ADR-003) | Backend directory | Frontend directory | Domain |
| --- | --- | --- | --- | --- |
| 1 | Project | `Features/Project` | `features/projects` | WF-01 |
| 2 | Progress | `Features/Progress` | `features/progress` | WF-02 |
| 3 | Schedule | `Features/Schedule` | `features/schedule` | WF-03 |
| 4 | Task | `Features/Task` | `features/tasks` | WF-04 |
| 5 | Milestone | `Features/Milestone` | `features/milestones` | WF-05 |
| 6 | Risk | `Features/Risk` | `features/risks` | WF-06 |
| 7 | ManagementConcern | `Features/ManagementConcern` | `features/issues-challenges` | WF-07 |
| 8 | ChangeRequest | `Features/ChangeRequest` | `features/change-requests` | WF-08 |
| 9 | Suspension | `Features/Suspension` | `features/suspension-closure` | WF-09 |
| 10 | Closure | `Features/Closure` | `features/suspension-closure` | WF-10 |
| 11 | Approval | `Features/Approval` | `features/approvals` | WF-11 |
| 12 | DocumentManagement | `Features/DocumentManagement` | `features/documents` | WF-12 |
| 13 | ExternalParticipation | `Features/ExternalParticipation` | `features/external-participation` | WF-13 |
| 14 | FinancialKpi | `Features/FinancialKpi` | `features/financial-kpi` | WF-14 |
| 15 | Notifications | `Features/Notifications` | `features/notifications` | WF-15 |
| 16 | Dashboards | `Features/Dashboards` | `features/dashboards` | FG-01 |
| 17 | Reports | `Features/Reports` | `features/reports` | FG-02 |
| 18 | IdentityAccess | `Features/IdentityAccess` | `features/identity-access` | FG-03 |
| 19 | MasterDataConfig | `Features/MasterDataConfig` | — (no frontend task; §7.3) | FG-04 |
| 20 | IntegrationMonitoring | `Features/IntegrationMonitoring` | `features/integration-admin` | FG-05 |
| 21 | AuditActivity | `Features/AuditActivity` | `features/audit-activity` | FG-06 |

Backend paths are relative to `src/backend/PMPlatform.Application/`, frontend paths to `src/frontend/src/`.

Two mappings are deliberately not 1:1 and are stated so they are not read as errors:

- **Suspension and Closure share one frontend folder** (`suspension-closure`), because TASK-064 builds one UI over both. They remain two backend modules.
- **MasterDataConfig has no frontend folder**, because no FG-04 frontend task exists in the workbook. §7.3 records that as a gap for the PMO, not a directory error.

### 6.4 Naming conventions

| Tier | Convention | Why |
| --- | --- | --- |
| Backend directories | **PascalCase**, matching the C# namespace | .NET convention; folder path and namespace stay identical, which is what the Roslyn analyzer set and CODEOWNERS (TASK-012) both key on |
| Frontend directories | **kebab-case** | React/Vite convention; avoids case-sensitivity defects between developer macOS and Linux CI runners |
| Migration files | `<timestamp>_<TaskID>_<description>` | TASK-024's stated convention, unchanged |
| Branches | `type/task-id-short-description` | TASK-012; the workbook's Branch column stays authoritative |

The case difference between tiers is intentional and is the single most likely thing to be "corrected" by someone tidying the workbook. It should not be.

### 6.5 Two gaps this scheme closes

Both are cases where the workbook's directory column has no value for work the workbook requires:

- **`PMPlatform.Domain` is created by TASK-011 and then never targeted.** No task row in the workbook points at it, yet ADR-003's modular monolith puts entities, value objects and domain rules there. TASK-025 builds the core schema but its directory is `.../Persistence/Migrations`, which is where the *migration* belongs, not the entity. Added to §6.2 as a recognised destination.
- **There is no unit-test project.** TASK-015's CI gate runs "unit tests for both tiers" and fails the build on a failing test, but no row names a backend unit-test directory. `PMPlatform.Tests.Unit` is added in §6.1. Frontend unit tests are colocated with the feature under `src/frontend/src/features/<module>`, which is Vitest's convention and needs no separate directory value.

## 7. Directory consistency audit

The TASK-006 acceptance criterion: *"every subsequent Backend/Frontend/Database task's directory column is consistent with ADR-002."* All 112 task rows were checked against §6.

### 7.1 Result

| | Rows |
| --- | --- |
| Total task rows (TASK-001 – TASK-112) | 112 |
| **Conform to §6** | **101** |
| **Do not conform** | **11** |
| — of which added 19–20 Sep 2026 by amendment (TASK-103 – TASK-112) | 10 |
| — of which pre-existing (TASK-089) | 1 |

TASK-001 to TASK-102 are consistent with one exception. The 10 amendment rows are consistent with each other and with nothing else: they use a third scheme, `src/<domain>/<feature>`, which exists in neither the backend nor the frontend tree and which no task creates.

### 7.2 The 11 non-conforming rows, with corrected values

Per the workbook's own rule (TASK-001 §4), revisions are re-issued, not overwritten in place. These are **specified, not applied**; authorisation is Q5 in §11.

| Task | Name | Category | Current value | Corrected value | Basis |
| --- | --- | --- | --- | --- | --- |
| TASK-089 | Validate Data Migration, Rollback & Seed Integrity | Testing | `db/migrations` | `docs/testing` | Both deliverables are documents (validation report, migration plan). `db/migrations` contradicts TASK-024/025/026, which put migrations in the Infrastructure project under EF Core; under §4.2.2 `db/migrations` would be an empty directory. |
| TASK-103 | Integrate SMS Provider, Sender ID and Delivery Receipts | Integration | `src/notifications/channels/sms` | `src/backend/PMPlatform.Infrastructure/Notifications/Sms` | An external-provider adapter. The delivery-receipt callback endpoint lands in `PMPlatform.Api` with the other controllers. |
| TASK-104 | Build Legacy Project Intake and Declared Baseline | Backend | `src/projects/intake` | `src/backend/PMPlatform.Application/Features/Project` | WF-01 — module 1. |
| TASK-105 | Build Governance Profile Configuration | Backend | `src/configuration/governance-profiles` | `src/backend/PMPlatform.Application/Features/MasterDataConfig` | FG-04 configuration — module 19. |
| TASK-106 | Build Change Materiality Band Routing | Backend | `src/change-control/materiality` | `src/backend/PMPlatform.Application/Features/ChangeRequest` | WF-08 — module 8. |
| TASK-107 | Build Consolidated Periodic Update Flow | Frontend | `src/progress/periodic-update` | `src/frontend/src/features/progress` | WF-02 — module 2. |
| TASK-108 | Implement Financial Source Provenance | Backend | `src/financial/source` | `src/backend/PMPlatform.Application/Features/FinancialKpi` | WF-14 — module 14. |
| TASK-109 | Implement Narrative Language Tag | Backend | `src/core/narrative` | `src/backend/PMPlatform.Domain/Narrative` | A persisted attribute on narrative fields across modules, not one module's feature. First row to use `PMPlatform.Domain` (§6.5). |
| TASK-110 | Build Permission Profile Authoring and Versioning | Backend | `src/administration/permission-profiles` | `src/backend/PMPlatform.Application/Features/IdentityAccess` | FG-03 — module 18. |
| TASK-111 | Enable Dashboard Personalization for Permitted Roles | Frontend | `src/dashboards/personalization` | `src/frontend/src/features/dashboards` | FG-01 — module 16. |
| TASK-112 | Build Controlled Report Explorer and Shared Saved Views | Full-stack | `src/reports/explorer` | `src/backend/PMPlatform.Application/Features/Reports, src/frontend/src/features/reports` | FG-02 — module 17. The only Full-stack row in the workbook; it needs both paths, in the form TASK-013 already uses. |

Two further edits follow from §4.2:

| Where | Current | Required |
| --- | --- | --- |
| TASK-020, task name | "Provision Managed **AlloyDB** for PostgreSQL…" | "Provision Managed **PostgreSQL**…" — the product is named in ADR-002 §4.2.3, not in a task title. Same correction to TASK-005/TASK-006 descriptions, TASK-023/TASK-024 dependency references, and the `DB_CONNECTION_STRING` purpose text, and to ADR-001's Option B text ("Cloud SQL"). Six cells, one value. |
| TASK-085, deliverable | "**Playwright/Cypress** e2e suite…" | "**Playwright** e2e suite…" (§4.2.2) |
| TASK-024, description | "code-first migration tool (**e.g.** EF Core Migrations)" | "**EF Core Migrations**" (§4.2.2) |

### 7.3 Naming divergences that are not directory errors

These do not break the directory column today, but each will if left standing.

**(a) Six module names differ between TASK-007 and the directory column.** TASK-007's description lists the 21 modules; the workbook's directory cells name them differently in six cases. ADR-003 owns the module names; this record owns the directories; they must be the same string.

| TASK-007 names | Directory column names | This record adopts |
| --- | --- | --- |
| Concern | ManagementConcern | **ManagementConcern** |
| Change | ChangeRequest | **ChangeRequest** |
| Document | DocumentManagement | **DocumentManagement** |
| Notification | Notifications | **Notifications** |
| Dashboard | Dashboards | **Dashboards** |
| Report | Reports | **Reports** |

The directory-column form is adopted in §6.3 because 22 task rows already use it and TASK-007 has not started. TASK-007 should carry the same six names into ADR-003. If ADR-003 chooses otherwise, §6.3 is re-issued to match it — ADR-003 owns the name, this record owns only the path built from it.

**(b) FG-04 has no frontend task.** `MasterDataConfig` is the only module with a backend task (TASK-034) and no frontend counterpart, yet master data and configuration are the surface AHDA populates for the matrices that UGV-03, UGV-04 and UGV-05 are waiting on. This is a scope gap for the PMO, not a directory defect — recorded here because §6.3 is where it becomes visible.

**(c) TASK-045 and TASK-047 overlap.** TASK-045 "Progress & Schedule UI (WF-02/WF-03 Frontend)" sits in `features/progress`; TASK-047 "Schedule, Gantt & Baseline UI (WF-03 Frontend)" sits in `features/schedule`. Two rows deliver WF-03 UI into two folders. The split needs a line in TASK-007's module boundary work; the directories themselves are correct under §6.3.

### 7.4 Two structural observations

Neither is ADR-002's to fix; both affect anyone reading the directory column.

- **The amendment rows use a second phase-numbering scheme.** TASK-001–TASK-102 use P0–P18 (`P2 - Project Setup & Foundation`, `P7 - Project Establishment`, `P8 - Execution & Performance`). TASK-103–TASK-112 use different names against the same numbers (`P2 - Core Project Lifecycle`, `P7 - Management Intelligence`, `P8 - Platform Administration`). Two rows can now carry "P2" and mean different phases. This also breaks the wave mapping in `docs/planning/wave-to-phase-crossreference.md`, which covers TASK-001–TASK-102 only. For the PMO.
- **ADR-014 to ADR-019 are cited but have no register rows.** TASK-104 cites ADR-014, TASK-112 cites ADR-019; the Architecture Decisions sheet ends at ADR-013. Ten task rows are traced to decisions that are not in the register, so their directory values cannot be traced to a decision at all. For the Engagement Architect.

## 8. Residual items

| # | Item | Owner | Owed by | Consequence if unresolved |
| --- | --- | --- | --- | --- |
| S-1 | **Confirmation that no AHDA IT platform standard mandates a different stack** (Option B). This is the whole decision. | AHDA IT | Before TASK-011 | The workbook's 73 stack-dependent directory values, and any code written against them, are provisional. §9 is the cost if the answer is yes. |
| S-2 | **.NET 10 (LTS) confirmed against AHDA IT's supported runtime and patch policy**, and an LTS upgrade scheduled inside the operations period (§4.2.1). | AHDA IT | Before TASK-011 | TASK-011's zero-warning build is not reproducible without a pinned SDK; and the runtime leaves support during operations with nobody owning the upgrade. |
| S-3 | **Managed PostgreSQL product — Cloud SQL or AlloyDB** (§4.2.3). Closes ADR-001 R-7. Conditional on the region named under ADR-001 R-2. | AHDA IT + Engagement Architect | With ADR-001 R-2, before TASK-020 | TASK-020 cannot be provisioned; six workbook cells continue to name the database three ways. |
| S-4 | **PostgreSQL 17 availability** in the selected product in the named region; fallback 16. | DevOps/Platform Lead | Before TASK-020 | The local Docker image (TASK-014) and the provisioned instance (TASK-020) can diverge on major version, which is exactly the class of defect that only appears in SIT. |
| S-5 | **ADR-003 approved in the same session as this record** (§1.1). | AHDA IT + Engagement Architect | Before TASK-011 | TASK-011 stays blocked by its own gate cell. `Features/<Module>` has a prefix but no module names. |
| S-6 | **Authorisation for the workbook edits in §7.2** — 11 directory values plus the database-product, Playwright and EF Core corrections. | PMO Engagement Lead | Before TASK-011 | Ten of the twelve newest rows point at directories the repository will not contain, and the acceptance criterion is not met. |
| S-7 | **Compute runtime — Cloud Run or GKE** (ADR-001 R-6 / confirmation-request Q4). Not taken here (§4.3). | AHDA IT (via ADR-001) | Before TASK-017 | TASK-017 cannot codify compute. Unchanged by this record; recorded so the deferral is visible. |
| S-8 | **Node.js LTS line pinned and confirmed still supported** at TASK-011 (§4.1). | DevOps/Platform Lead | Before TASK-011 | CI and developer machines drift onto different toolchain majors; the `tsc --noEmit` gate (TASK-015) becomes environment-dependent. |

## 9. Supersession — what happens if AHDA selects a different stack

The TASK-006 acceptance criterion: *"if AHDA later selects a different stack, all directory values in this workbook are treated as superseded and re-issued in a new workbook revision."* This section is the mechanics of that, so the cost is known before Q1 is answered rather than after.

**Scope of a stack change, measured on the current 112 rows:**

| | Rows | Effect of a stack change |
| --- | --- | --- |
| Directory under `src/` or `db/` | **73** | **Superseded.** Every value is re-derived from the replacement stack's layout conventions. |
| Directory under `docs/`, `infra/terraform/`, `infra/environments`, `infra/secrets`, `/.github`, repo root | **39** | **Unaffected as paths.** These are stack-independent. |

Three qualifications to the second row:

- `infra/docker` (TASK-014) keeps its path but not its content: the compose file's services are stack-specific.
- `docs/api` (TASK-094) keeps its path; OpenAPI generation is framework-specific.
- Under ADR-001, `infra/terraform/database` (TASK-020) is constrained by the database engine, not by the application stack — a frontend or backend change leaves it untouched.

**Also superseded by a stack change:** the four backend project names in TASK-011's description; the `dotnet build` / `npm run build` acceptance criteria in TASK-011; `dotnet ef database update` in TASK-024; `appsettings.Template.json` in TASK-013; the `tsc --noEmit` gate in TASK-015; §6.1 to §6.4 of this record; and the Environment and Secrets sheet only where a variable name presumes a framework.

**Mechanics.** Per TASK-001 §4, the workbook is not edited in place: a new revision is issued, the prior revision is retained for traceability, and this ADR is superseded by an ADR-002 revision naming the replacement stack. A stack change **after** TASK-011 has run is not a workbook revision — it is a rebuild of everything written against the old stack. This is why S-1 is owed before TASK-011 and not before the first release.

## 10. Acceptance-criteria check

| # | Criterion | Result |
| --- | --- | --- |
| 1 | ADR-002 records a single selected stack with rationale and alternatives considered | **MET.** One stack, named to the version and product (§4.1), with the three previously unresolved points settled (§4.2). Rationale in §5. Alternatives: the register's two options in §3.1, and seven per-layer alternatives in §3.2 — recorded because the register's own option set names no concrete alternative to Option A. |
| 2 | Every subsequent Backend/Frontend/Database task's directory column is consistent with ADR-002 | **PARTIAL — 101 of 112 rows.** The rule and the module registry are written down (§6); all 112 rows are audited (§7.1); the 11 non-conforming rows have specified corrections (§7.2). It cannot read MET until the edits are authorised (S-6) — this record specifies workbook edits, it does not apply them, per TASK-001 §4. |
| 3 | If AHDA later selects a different stack, all directory values are treated as superseded and re-issued in a new workbook revision | **MET as a recorded rule.** §9 states the rule, its scope (73 of 112 rows), what survives it, and the mechanics. It is a contingency, so it can be recorded but not executed. |

Validation check from the TASK-006 definition:

| Check | Result |
| --- | --- |
| "Verify ADR-002 is explicitly labeled 'Proposed pending AHDA approval' until AHDA confirms; directory paths in this workbook are provisional on that approval" | **MET.** The header status reads **PROPOSED — PENDING AHDA APPROVAL**; the register's ADR-002 Status cell reads "Proposed — Pending AHDA Approval" and is unchanged by this record. §9 states that every directory value is provisional on the confirmation, and §11 is the instrument that would change it. |

## 11. Confirmation required from AHDA IT

The TASK-006 gate note is right that this is a confirmation and not a design exercise: *"Recommended option already drafted — AHDA IT confirms rather than designs."* Six questions. Q1 is the decision; Q2–Q6 are the points the register left as labels.

| # | Question | Answer |
| --- | --- | --- |
| **Q1** | Does any **AHDA IT platform standard** mandate a technology stack for this platform? (Register Option B; S-1) | ☐ No — Option A as recorded in §4.1 is confirmed  ☐ Yes — mandated stack: ____________ |
| **Q2** | **.NET 10 (LTS)** as the backend runtime, confirmed against AHDA IT's supported runtime list. .NET 8's LTS support ends November 2026; .NET 10's ends November 2028, which falls inside the operations period and needs a planned upgrade. (§4.2.1; S-2) | ☐ Confirmed  ☐ Different version required: ____________ |
| **Q3** | **ADR-003 in the same session.** Approving ADR-002 alone does not unblock TASK-011 — its own gate cell requires both. (§1.1; S-5) | ☐ Both approved together  ☐ ADR-003 deferred, TASK-011 stays blocked |
| **Q4** | **Managed PostgreSQL product: Cloud SQL or AlloyDB?** The workbook names the database three ways. §4.2.3 recommends Cloud SQL — TASK-020's stated requirements are all Cloud SQL capabilities, the stated capacity is 150 named users, and ADR-001 C-7 puts four environments in scope. Either answer works at code level, because the application is written to standard PostgreSQL only. (S-3) | ☐ Cloud SQL for PostgreSQL  ☐ AlloyDB (cost and regional availability accepted)  ☐ Defer to TASK-020 within the named region |
| **Q5** | **Authorisation for the workbook edits in §7.2** — 11 directory values (10 of them on rows added 19–20 Sep 2026), plus the database-product, Playwright and EF Core corrections. Without these, acceptance criterion 2 cannot read MET. (S-6) | ☐ Authorised  ☐ Declined  ☐ Partial: ____________ |
| **Q6** | **The compute runtime (Cloud Run or GKE)** is not taken by this ADR (§4.3) and stays with ADR-001 R-6 / confirmation-request Q4. Confirm that placement, or direct it into an ADR-002 revision. (S-7) | ☐ Stays with ADR-001  ☐ Move into ADR-002 revision |

On Q1 being answered "No — Option A confirmed" and Q2, Q3 and Q4 being answered, the header status becomes **APPROVED**, the register's ADR-002 Status cell is re-issued to match, and the Release Checklist gate "Architecture Decisions Approved" closes once ADR-003 does the same.

| Role | Decision | Name | Date |
| --- | --- | --- | --- |
| AHDA IT | Q1, Q2, Q3, Q4, Q6 | | |
| PMO Engagement Lead | Q5: workbook edits authorised / declined | | |
| Engagement Architect | §4 stack and §6 directory scheme proposed and, on confirmation, binding on TASK-011 onward | | |

## 12. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-20 | Initial record. Option A ratified as the delivery-team proposal, status held at Proposed — Pending AHDA Approval per the TASK-006 validation check. Three open points settled: .NET 10 (LTS), Playwright and EF Core Migrations, and PostgreSQL 17 on Cloud SQL — the last closing ADR-001 R-7. Canonical directory rule and 21-module registry recorded (§6). All 112 task rows audited: 101 conform, 11 specified for correction (§7). Eight residual items (S-1 to S-8). Supersession scope measured at 73 of 112 rows (§9). Compute runtime (ADR-001 R-6) explicitly not taken (§4.3). | Architecture (TASK-006) |

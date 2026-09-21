# Monorepo and Solution Structure

| Field | Value |
| --- | --- |
| Task | TASK-011 — Initialize Monorepo & Solution Structure (P2 - Project Setup & Foundation) |
| Depends on | TASK-006 — ADR-002 (`docs/architecture/adrs/ADR-002-technology-stack.md`, PROPOSED — PENDING AHDA APPROVAL); TASK-007 — ADR-003 (`docs/architecture/solution-architecture.md`, PROPOSED — PENDING AHDA APPROVAL) |
| Record date | 2026-09-21 |
| Status | **BUILT — PROVISIONAL ON THE TASK-011 GATE.** The skeleton exists on the task branch and passes its acceptance builds; it is not to be merged to `main` until ADR-002 and ADR-003 are approved (§2) |
| Branch | `chore/task-011-repo-bootstrap` |
| Deliverables | Repository skeleton (`src/backend`, `src/frontend`, `infra/`, `db/`, `.github/workflows/ci.yml`); `.editorconfig`; `CONTRIBUTING.md`; this record |
| Controlled sources and authority order | per TASK-001 |

## 1. What this record is

TASK-011 creates the repository every later Backend, Frontend, Database and CI task writes into. Its content is not a design choice: ADR-002 §6 fixes the layout and naming, ADR-003 §4.2–§4.4 fixes the project dependency directions and the 21 module names, and ADR-003 §10 places the architecture-test suite "at TASK-011 alongside the skeleton, before any module exists". This record says what was built, what was verified and how, where the build departs from the letter of a record and why, and what remains open.

## 2. The gate

TASK-011's Gate Decision cell: *"Do not initialize the monorepo before ADR-002 and ADR-003 are approved — the directory layout depends on them."* At the record date both are **PROPOSED — PENDING AHDA APPROVAL** (ADR-002 §11 Q1–Q6, ADR-003 §14 Q1–Q4 unanswered).

The skeleton was built anyway, on the task branch, under one stated assumption: **AHDA confirms both records as proposed** — Option A stack on .NET 10 LTS, the 21 modules of ADR-003 §4.3 including `ProjectTask` (§4.4b). The reason is the one ADR-002 §9 gives: the layout is derived, so building it costs nothing that approval would not also cost, while the answer to Q1 ("does an AHDA standard mandate a different stack?") makes the whole tree disposable either way. What the gate protects against is code *depending* on an unapproved layout, and that is handled by not merging:

- the branch is **not merged to `main`** until ADR-002 §11 and ADR-003 §14 are signed;
- if ADR-002 Q1 is answered "Yes — mandated stack", the branch is discarded and ADR-002 §9 executes;
- if ADR-003 Q3 declines `ProjectTask`, one folder and one registry row are renamed (§5).

## 3. What was built

### 3.1 Layout

Exactly ADR-002 §6.1, with the additions that record makes (`PMPlatform.Tests.Unit`, `PMPlatform.Tests.Integration`) and one name from ADR-003 §4.4b (`Features/ProjectTask`). `CONTRIBUTING.md` reproduces the tree and the module table and is the developer-facing statement of it. Folders that no task has populated yet (`infra/*`, `db/seed`, `Persistence/Migrations`, each module's `Contracts/`, each frontend feature folder) exist with a `.gitkeep` so CODEOWNERS (TASK-012) can be written against real paths.

### 3.2 Backend

| Item | Where | Note |
| --- | --- | --- |
| SDK pin | `global.json` | 10.0.401, `rollForward: latestPatch`. .NET 10 LTS per ADR-002 §4.2.1 |
| Build baseline | `src/backend/Directory.Build.props` | `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, deterministic build, nullable, implicit usings. Applies to all six projects |
| Package versions | `src/backend/Directory.Packages.props` | Central package management; a `.csproj` carries no version |
| Solution | `src/backend/PMPlatform.slnx` | The SDK's current solution format |
| Project references | six `.csproj` files | Exactly L-1 to L-4: Domain → nothing; Application → Domain; Infrastructure → Application, Domain; Api → Application, Infrastructure |
| Composition root | `PMPlatform.Api/Program.cs` | Calls `AddApplication()` and `AddInfrastructure(configuration)`; maps `/health` and controllers. The only Api code allowed to reference Infrastructure (A-4) |
| Shared value types | `PMPlatform.Domain/Common` | The types the ERD's conventions are written against — §3.4 |

### 3.3 Architecture tests — ADR-003 §10, A-1 to A-6

`PMPlatform.Tests.Unit/Architecture`, four files, run by `dotnet test` and therefore by the CI backend job.

| Rule | Test | Method |
| --- | --- | --- |
| A-1 | `ModulesReferenceOtherModulesOnlyThroughContracts` | Every type under `Features/<X>` is read with Mono.Cecil — signatures, attributes and **method bodies** — and every referenced type under `Features/<Y>`, Y ≠ X, must be under `Features/<Y>/Contracts` |
| A-2 | `RepositoriesAreReferencedOnlyByTheirOwningModule`, `DbContextAndDbSetAreConfinedToPersistence` | A `*Repository` type is referenced only from its module (or the Infrastructure DI root); `Microsoft.EntityFrameworkCore.DbContext`/`DbSet<>` are referenced only under `Infrastructure.Persistence` |
| A-3 | `DomainTypesHoldNoStateOfAnotherModule` | No field or property of a `Domain/<X>` type is a `Domain/<Y>` type. `Domain/Common` is neutral |
| A-4 | `LayeringTests` (three tests) | Domain's assembly references contain no `PMPlatform.*`; Application references no Infrastructure or Api type; Api references Infrastructure only from `Program` |
| A-5 | `PersistenceBoundaryTests` (two tests) | `Migration` subclasses only in `Persistence.Migrations`; no SQL write statement (`INSERT INTO`, `UPDATE … SET`, `DELETE FROM`, `TRUNCATE`, `ALTER`/`DROP`/`CREATE TABLE`) as a constant or `ldstr` literal outside `Persistence` |
| A-6 | `CrossModuleReferencesAreRegisteredEdges` | The set of (referrer, referenced) module pairs derived as in A-1 must be a subset of `ModuleRegistry.cs`, which transcribes ADR-003 §8.1–§8.2 |

Two points about A-6 that are decisions, not transcription:

- **Event edges are recorded in the reference direction.** ADR-003 writes an event edge producer → consumer; in code the consumer's handler references the producer's `Contracts/Events` (event-conventions EV-8). `ModuleRegistry.cs` therefore records §8.2 row 35 as Schedule, Progress, Milestone, FinancialKpi → Project, rows 31–33 as IntegrationMonitoring → IdentityAccess, Notifications, ExternalParticipation, and row 28 as already covered by rows 20–27. This adds one pair §8.2 does not state as a call — **Milestone → Project** — which exists only because Milestone consumes `ProjectIntakeRecorded`. Rows 10 (split-authority contract) and 19 (target set unconfirmed, S-5) are not registered; the edge is added when the implementing task revises §8.2.
- **The check is one-directional until the last module lands.** §10 says A-6 "fails on any difference". With zero modules implemented, equality fails on day one, which is not what the rule is for. The test fails on any *unregistered* reference now; the equality form (registered edges that are never implemented also fail) is switched on when the module set is complete — TASK-015's gate is the natural place to record that switch.

**Verification that the suite fires.** A skeleton with no modules passes every rule trivially, so the suite was mutation-checked: one violation per rule was injected into a scratch copy (a Risk type reaching `Project/Internal`; a `Reports → Risk.Contracts` reference; an `IProjectRepository` used from Schedule; a `DbContext` in `Application/Common`; a `Domain.Risk.Risk` holding a `Domain.Project.Project`; a controller referencing `PMPlatform.Infrastructure`; an `UPDATE … SET` constant in Application; a `Migration` subclass in Application). All eight tests went red, and only those eight.

### 3.4 Participation amendment — what the skeleton carries

The amendment on this task (*"schema carries financial provenance fields, a language attribute on narrative fields, and the Declared Baseline and intake marker"*) is a schema statement; the ERD (TASK-008 D-7, D-10, and `ProjectBaseline.baseline_type` / `Project.legacy_intake_date` / `ProjectIntake`) is where it is made physical. What the skeleton contributes is the shared types those columns map to, placed per M-10 in `Domain/Common` so no module invents its own:

| Amendment | Type | ERD rule | Column shape |
| --- | --- | --- | --- |
| Financial provenance (ADR-008 ext., TASK-108) | `FinancialProvenance` (`FinancialSourceType`, `SourceReference`, `AsOfDate`, `EnteredByUserId`) | D-10 | `source_type`, `source_reference`, `as_of_date`, `entered_by_user_id` |
| Narrative language attribute (ADR-012 ext., TASK-109) | `NarrativeText` (`Text`, `Language`) with `Language` = `Ar`/`En` | D-7 | `<field> text`, `<field>_lang char(2)` |
| SAR money (ADR-008) | `Money` — `CurrencyCode` is the constant `"SAR"`; two-decimal, `numeric(18,2)` range enforced | D-5 | one `*_sar numeric(18,2)`, no currency column |
| Bilingual labels (ADR-012) | `BilingualLabel` (`Ar`, `En`, both required) | D-6 | `<name>_ar`, `<name>_en` |

The **Declared Baseline and intake marker** (ADR-014, TASK-104) are not shared types: `baseline_type` is a Schedule fact and `legacy_intake_date` a Project fact, and a `BaselineType` in `Domain/Common` would break M-10. They land in `Domain/Schedule` and `Domain/Project` at TASK-046 and TASK-104; the folders they land in exist. Each shared type has unit tests in `PMPlatform.Tests.Unit/Domain/Common`.

### 3.5 Frontend

| Item | Where | Note |
| --- | --- | --- |
| Toolchain pin | `.nvmrc` (24), `package.json` `engines` | Node 24 LTS per ADR-002 §4.1 (S-8). Build toolchain only |
| Stack | `package.json` | React 19.3, TypeScript 5.9 (`strict`, `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes`), Vite 8. Exact versions, `package-lock.json` committed |
| Lint | `eslint.config.js` | ESLint 10 flat config: `typescript-eslint` strict + stylistic type-checked, `react-hooks`, `react-refresh`, `eslint-config-prettier` last |
| Format | `.prettierrc` | 100 columns, single quotes, trailing commas; values match `.editorconfig` |
| T-1 | `vite.config.ts` | `envPrefix: 'VITE_'` — no `DB_*` variable can reach the bundle |
| Not yet | Vitest, Playwright | ADR-002 §4.1 places them at TASK-015 and TASK-085 |

The Vite template of the record date ships `oxlint` and TypeScript 6.0; both were replaced to match ADR-002 §4.1 (ESLint + Prettier, TypeScript 5.x).

### 3.6 CI and branch protection

`.github/workflows/ci.yml` defines two jobs, `backend` (restore, build `-warnaserror`, test) and `frontend` (`npm ci`, lint, format check, build), on every pull request to `main` and every push to it. These are the two status checks branch protection must require. The protection rule itself is a repository setting, not a file; it could not be applied from this environment (no GitHub credential) and is S-3.

## 4. Verification

No .NET SDK or Node.js is installed on the authoring machine. Every result below was produced inside the official images on a clean checkout of the branch, which is also the acceptance criterion's own condition:

| Check | Command (inside image) | Result |
| --- | --- | --- |
| Backend build, zero warnings | `mcr.microsoft.com/dotnet/sdk:10.0` → `dotnet build -warnaserror` | Build succeeded, **0 Warning(s), 0 Error(s)** |
| Backend tests | `dotnet test --no-build` | **25 passed**, 0 failed (13 architecture, 12 Domain/Common) |
| Architecture suite fires | mutation check, §3.3 | 8 injected violations → 8 red tests |
| Frontend lint | `node:24-alpine` → `npm run lint` | 0 problems; a deliberately bad component produced 4 errors (`no-explicit-any`, `no-unused-vars`, `set-state-in-effect`, `no-unsafe-argument`) |
| Frontend format | `npm run format:check` | All files formatted |
| Frontend build | `npm run build` (`tsc -b && vite build`) | Built, 15 modules |

## 5. Departures from the letter of a record

| Where | Departure | Why |
| --- | --- | --- |
| ADR-002 §6.3 row 4 | `Features/ProjectTask`, not `Features/Task` | ADR-003 §4.4b: a `…Features.Task` namespace shadows `System.Threading.Tasks.Task` (CS0118) in every async signature, which a zero-warning build starts by fighting. Frontend `features/tasks` unchanged. If ADR-003 Q3 declines, rename one folder and one `ModuleRegistry` entry |
| ADR-003 §10 A-6 | Subset check, not equality, until all modules exist | §3.3 |
| ADR-003 §8.2 | `Milestone → Project` registered as a reference | Consequence of row 35 in the reference direction; §3.3. For the §8.2 re-issue |
| ADR-003 §10 A-5 | Cross-schema writes *inside* migration SQL are not detected | The schema a migration touches is not visible from compiled code; that check belongs to TASK-024's migration review, where the schema is known |
| TASK-009 §9 row 3 | `ApiControllerBase`, `[SensitiveWrite]`, `ProblemDetails` factory, correlation-id middleware, idempotency filter, `EventEnvelope<TData>`, outbox writer, A-7 to A-10 **not built** | These are workbook additions to TASK-011 that api-conventions §9 specifies but Q3 has not authorised (TASK-001 §4). Building them here would apply an unauthorised revision. They are S-4 and are a bounded follow-up on this skeleton |

## 6. Acceptance-criteria check

| # | Criterion | Result |
| --- | --- | --- |
| 1 | `dotnet build` succeeds with zero warnings on a clean checkout | **MET** — §4, in the official SDK image on the clean branch; `-warnaserror` makes a warning a failure, so the result cannot drift silently |
| 2 | `npm run build` succeeds on the frontend workspace | **MET** — §4; lint and format check also pass |
| 3 | A `CONTRIBUTING.md` documents the folder layout | **MET** — layout tree, module table, toolchain, gates, branch policy |
| 4 | Branch protection on `main` requires passing CI before merge | **PARTIAL** — the CI that protection requires exists and is named (`backend`, `frontend`); the protection rule is a GitHub setting that this environment cannot apply (S-3). Reads MET when the repository administrator enables it |

## 7. Residual items

| # | Item | Owner | Owed by | Consequence if unresolved |
| --- | --- | --- | --- | --- |
| S-1 | **ADR-002 and ADR-003 approved** (ADR-002 §11, ADR-003 §14). This branch is not merged until then (§2). | AHDA IT + Engagement Architect | Before merge | The skeleton stays on a branch; TASK-012 to TASK-015 have nothing on `main` to build on |
| S-2 | **`ProjectTask` confirmed** (ADR-003 Q3) and ADR-002 §6.3 row 4 re-issued to match. | Engagement Architect + PMO | With S-1 | One folder and one registry row rename; TASK-048/049 directory cells |
| S-3 | **Branch protection on `main`**: require a pull request; required status checks `backend` and `frontend`; no force-push; no direct push. | Repository administrator | Before the first PR after merge | Criterion 4 reads PARTIAL; a red CI does not block a merge |
| S-4 | **TASK-009 §9 row 3 additions** to this skeleton (`ApiControllerBase`, write-class attributes, `ProblemDetails` factory, correlation-id middleware, idempotency filter, `EventEnvelope<TData>`, outbox writer, A-7 to A-10), once api-conventions Q3 is authorised. | Backend lead | Before TASK-031 (first API surface) | The first controller sets the shape every later one copies; the wire conventions are then retrofitted rather than inherited |
| S-5 | **A-6 switched to the equality form** when the 21st module lands, so unimplemented registered edges also fail (§3.3). | TASK-015 owner | End of the last Backend wave | The diagram is checked for over-reach only, not for drift toward fewer edges than the record claims |
| S-6 | **ADR-003 §8.2 re-issue** to state event edges in the reference direction and add `Milestone → Project` (§3.3), and to take up event-conventions S-7 (Notifications → source query). | Engagement Architect | With S-1 | `ModuleRegistry.cs` and §8.2 disagree on one pair; the first reminder handler fails A-6 |
| S-7 | **Vitest and Playwright** (ADR-002 §4.1 places them at TASK-015 and TASK-085). | TASK-015, TASK-085 | Per task | — |

## 8. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-21 | Initial record. Skeleton built on the task branch under the assumption ADR-002 and ADR-003 are approved as proposed; not merged (§2). Six-project .NET 10 solution with analyzers and warnings-as-errors; 21 module folders; Domain/Common value types for the participation amendments (§3.4); architecture tests A-1 to A-6 with mutation check (§3.3); React 19 / TypeScript 5.9 / Vite 8 workspace with ESLint and Prettier; baseline CI. All builds verified inside the official SDK images (§4). Five departures recorded (§5); criterion 4 partial pending branch protection (§6); seven residual items (§7). | Architecture (TASK-011) |

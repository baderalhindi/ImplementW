# Platform Test Strategy and Coverage Policy

| Field | Value |
| --- | --- |
| Task | TASK-084 — Author Platform Test Strategy & Coverage Policy (P15 - Testing & QA, P0) |
| Depends on | TASK-015 — Configure Quality Gates: Lint, Format, Type-Check, Test in CI (`docs/architecture/ci-quality-gates.md`, BUILT) |
| Record date | 2026-10-17 |
| Status | **DRAFT — AWAITING APPROVAL BY THE PMO ENGAGEMENT LEAD** (§11). The coverage gate and the consistency check are built and run in CI; the policy they enforce is this draft's until it is approved |
| Branch | `test/task-084-test-strategy-and-coverage-policy` |
| Deliverables | this strategy; the coverage-threshold CI configuration: `docs/testing/coverage-thresholds.json`, `docs/testing/coverage-check.py`, `src/backend/coverage.runsettings`, the `coverage` job and coverage uploads in `.github/workflows/ci-quality-gates.yml`, the frontend thresholds in `src/frontend/vite.config.ts`; `docs/testing/test-strategy-check.py` in `repo-checks` |
| Environment variables / secrets | None (workbook column) |
| Sources | Workbook TASK-084 row (local `AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`); Step 18 UAT Catalogue v1.0 (docx and xlsx, 15 Sep 2026: `UAT_Scenarios`, `UAT_Journeys`, `Test_Data_Evidence`, `Readiness_Gates`), read from `Project Files.zip` |

## 1. What this strategy decides

Step 18 is an acceptance catalogue. It says itself that "unit, penetration, performance and detailed API tests belong to
their specialist catalogues and are not replaced by UAT". This document is that specialist side. It decides five things:

1. which test layers exist, what each one proves, where it runs and what fails it (§2–§3);
2. the minimum each layer must reach, as numbers where a number is AHDA's to give or already agreed, and as a named open
   item where it is not (§4);
3. how the coverage minimum is measured and enforced as a build-breaking CI gate (§5);
4. how the 4,079 Step 18 scenarios and the 15 cross-domain journeys map onto the automated layers, and which automated
   e2e suite owns each journey (§6–§7);
5. who changes a threshold and how (§5.4, §10).

It does not execute UAT (TASK-088), build the e2e suite (TASK-085), run load tests (TASK-086) or the accessibility audit
(TASK-087). It does not close any Step 18 gate (18-G-01 to 18-G-05) or Step 14B finding.

## 2. Test layers

| Layer | Proves | Where | Tooling | Runs | Fails the PR on |
| --- | --- | --- | --- | --- | --- |
| **Unit** | Pure rules with no I/O: the authorization engine, state machines, calculators, validators, the A-1 to A-6 architecture rules | `src/backend/PMPlatform.Tests.Unit`; `src/frontend/src/**/*.test.tsx` | xUnit; Vitest with Testing Library in jsdom | every PR (`backend`, `frontend`) | any failing test |
| **Integration** | A module's behaviour through its real API and real database: handlers, guards and triggers, transactions, outbox and audit, authorization by scope | `src/backend/PMPlatform.Tests.Integration/<Module>` | xUnit, `WebApplicationFactory`, PostgreSQL 17, the `AHDA-ldap` test directory, Redis | every PR, one CI step per namespace across `backend`, `backend-risk-governance`, `backend-endpoint-hardening`, `backend-platform-services` | any failing test; a namespace left out of every step (TS-1) |
| **Contract** | The API is what `docs/api/openapi.v1.json` promises and nothing else; events match `event-envelope.schema.json` | `*ContractTests` in the integration namespaces; `docs/architecture/contract-check.py` | snapshot comparison (api-conventions R-10); standard-library lint | every PR (backend jobs, `repo-checks`) | a breaking change, an undocumented operation, a convention violation |
| **End-to-end** | A Step 18 journey across modules through the browser, against the deployed stack | `src/frontend/e2e/journeys/uj-nnn-*.spec.ts` (§7), `src/frontend/e2e/participation/` | Playwright 1.63 (`e2e-suite.md`) | nightly (SIT, or the e2e stack until SIT exists); every promotion to UAT; PRs that change the suite | a failing journey blocks promotion to UAT (`e2e-sit`) |
| **Performance** | p95 latency of the register, dashboard and report endpoints and screens at 25 and 50 concurrent people, at the TASK-026 volume | `docs/testing/load-test.md`, `docs/testing/load-test/` (TASK-086) | k6 1.3 | before UAT (acceptance profile, on SIT); PRs that change the harness (smoke, `load-test.yml`) | a breach in a recorded run without a filed optimisation task (`load-test-report.py --check` in `repo-checks`) |
| **Accessibility** | No axe violation on a rendered screen; keyboard and screen-reader use in both locales | every feature's component tests call `accessibilityViolations()` (`src/test/renderApp.tsx`); the TASK-087 audit (`accessibility-audit.md`, `src/frontend/e2e/accessibility`) | axe-core 4.13 in jsdom (component tests) and in Chromium against the e2e stack (audit: contrast, a Tab walk, reflow at 320 px); review of the accessibility tree | every PR (component tests); weekly and on audit changes (`accessibility-audit.yml`); before UAT (audit) | any axe violation in a component test; a recorded audit run that is not clean, or a tracker row out of order (`accessibility-audit.py --check` in `repo-checks`); an Open finding at UAT entry (`--gate`) |
| **Security** | Secrets, vulnerable dependencies, OWASP baseline, threat-model controls, the external test | `secret-scan`, `dependency-vuln-gate`, `docs/architecture/zap/`, `docs/security/` | gitleaks, Trivy, ZAP, `findings-log-check.py`, `penetration-test-check.py` | every PR; weekly scans; UAT and release gates | a secret, a new fixable CRITICAL, an inconsistent threat model; `--gate` at UAT/release |
| **Data** | Migrations apply, roll back and re-apply; seed integrity; forward-only migrations | `Persistence` namespace; `db/`; `migration-forward-only.sh` | xUnit against throwaway databases | every PR (`backend`, `repo-checks`) | a migration that does not round-trip; an edited migration |

Mutation testing is a practice, not a gate: each module task since TASK-048 ran a harness that mutates its rules and
confirms the tests kill each mutant (`artifacts/task-0nn/mutations/run.py`). It is the check that coverage cannot give,
because coverage counts lines executed, not lines asserted.

## 3. Why most business logic is proved by integration tests

The module handlers in `PMPlatform.Application` compose EF Core queries, row-version checks, database guards and the
outbox in one transaction. The behaviour the specifications care about is mostly in that composition: two requests racing
for one active baseline, a published snapshot the database refuses to change, an audit event that commits with the write
or not at all. A unit test with a mocked `DbContext` asserts none of that. The module records since TASK-043 therefore
test handlers through the API against PostgreSQL, and keep unit tests for pure logic.

The numbers show the result (measured 2026-10-17, §5.3):

| Business-logic assemblies | Unit tests only | Unit + integration |
| --- | --- | --- |
| `PMPlatform.Domain` and `PMPlatform.Application`, line coverage | 18.4% | 93.7% |

**D-1. The coverage gate measures every test that runs in CI, not the unit project alone.** The workbook's acceptance
criterion reads "minimum unit-test coverage threshold (e.g. 80% of business-logic modules)". Read literally against
`PMPlatform.Tests.Unit`, an 80% gate would fail today by about 60 points, and closing that gap would mean writing mocked
tests of code that is already proved against the real database. This strategy gates the business-logic modules at 80% as
measured by the unit and integration runs together, and reports the unit-only figure without gating it. If AHDA wants the
unit-only reading, the gate is the same script fed only the unit report (`coverage-check.py <unit TestResults>`); §11
asks the approver to confirm D-1.

## 4. Minimum per layer

| Layer | Minimum | Enforced by | Today |
| --- | --- | --- | --- |
| Unit + integration, backend business logic | every module ≥ 80% line coverage; all modules together ≥ 85% (§5.1) | `coverage` job, build-breaking | lowest module 86.7% (MasterDataConfig), total 93.7% (§5.3) |
| Unit, frontend | ≥ 80% lines and ≥ 70% branches over `src/**` less test fixtures (§5.2) | `frontend` job, build-breaking | 88.4% lines, 78.9% branches |
| Integration | 100% of integration namespaces run in a CI step | TS-1 in `repo-checks` | 27 of 27 (24 before this task) |
| Contract | 100% of documented operations covered by a module's snapshot comparison; 0 breaking changes without a version | contract tests, `contract-check.py` | in place since TASK-009/043 |
| End-to-end | 15 of 15 Step 18 journeys automated and passing on SIT before UAT entry | `e2e-sit` before `deploy-uat`; TS-5, TS-7 | 15 of 15 automated; 14 of 15 pass on the e2e stack, UJ-002 is an expected failure on `e2e-suite.md` F-1 |
| Performance | p95 for register and dashboard endpoints under 50 concurrent users within the threshold PTBC-043 sets; every breach with a filed follow-up | TASK-086; `load-test-report.py --check` | Run on the e2e stack against a **proposed** budget (`load-test.md` §2, PTBC-043 still open): every register endpoint within it; the portfolio-scope PORTFOLIO dashboard and 10 screens or report requests over it, each filed (#101–#104). SIT owed |
| Accessibility | 0 axe violations in component tests; 0 Critical/Serious on the audited screens in both locales at the conformance level PTBC-040 sets (WCAG 2.1 AA as working baseline) | component tests; TASK-087 (`accessibility-audit.py --check`, `--gate`) | component tests pass; audit: 0 Critical/Serious on 25 screens and states in both locales, Tab reaches every control on the 10 top-traffic screens (`accessibility-audit.md` §4); 2 findings open (A11Y-F-003, A11Y-F-004); **open: PTBC-040** |
| Security | 0 secrets; 0 fixable CRITICAL added; 0 High/Critical findings open at UAT; external test passed before release | `secret-scan`, `dependency-vuln-gate`, `findings-log-check.py --gate`, `penetration-test-check.py --gate` | gates red as recorded in `docs/security/` |

Threshold register (TS-4 checks these values against the configuration that enforces them):

| Key | Value | Configured in |
| --- | --- | --- |
| `backend.minimumPerModule` | 80% | `docs/testing/coverage-thresholds.json` |
| `backend.minimumTotal` | 85% | `docs/testing/coverage-thresholds.json` |
| `frontend.lines` | 80% | `src/frontend/vite.config.ts` |
| `frontend.branches` | 70% | `src/frontend/vite.config.ts` |

80% is the workbook's example figure. 85% for the total sits nearly nine points under today's measurement, so it catches a broad
decline that no single module's 80% would. The frontend branch minimum is 70% because branch coverage of JSX conditional
rendering runs about ten points below line coverage here (78.9% against 88.4%), and a 70% floor keeps the same distance
from today's figure as the line minimum does.

## 5. The coverage gate

### 5.1 Backend

- **What is measured.** Line coverage of `PMPlatform.Domain` and `PMPlatform.Application`, the two assemblies that hold
  business logic (ADR-003). `PMPlatform.Api` and `PMPlatform.Infrastructure` are adapters, proved by the contract and
  integration layers, and are not instrumented. Source-generated code under `obj/` and `[GeneratedCode]` members are
  excluded; nothing else is, and `test-strategy-check.py` TS-3 fails a PR that adds `[ExcludeFromCodeCoverage]` to either
  assembly.
- **What a module is.** One per bounded module of ADR-003: `PMPlatform.Domain/<Module>/` together with
  `PMPlatform.Application/Features/<Module>/`. Everything else in Application (`Common/`, `DependencyInjection.cs`) and
  `PMPlatform.Domain/Common/` is the module `Common`. 22 modules today.
- **How it is collected.** Under `CI=true`, `src/backend/Directory.Build.props` sets `RunSettingsFilePath` to
  `src/backend/coverage.runsettings` for every test project, so every `dotnet test` step in CI writes a Cobertura report
  with `Microsoft.CodeCoverage`, which `Microsoft.NET.Test.Sdk` already brings. A new CI step is covered without
  remembering to be. Overhead: about one second per step locally; on a CI runner that built in 82 s, every step ran within
  its `dev` time.
- **How it is gated.** Each of the four backend jobs uploads its reports as `coverage-<job>`. The `coverage` job needs
  all four, downloads the reports and runs `python3 docs/testing/coverage-check.py coverage`, which merges them line by
  line (a line is covered when any run executed it) and fails on CV-1 (an assembly missing from the reports), CV-2 (a
  module under `minimumPerModule`) or CV-3 (the total under `minimumTotal`). The table goes to the job summary.
- **Locally.** `dotnet test src/backend/<test project> -s src/backend/coverage.runsettings --results-directory <dir>`
  for each project, then `python3 docs/testing/coverage-check.py <dir>`.

### 5.2 Frontend

`npm run test:coverage` runs Vitest with `@vitest/coverage-v8` (5.0.1, pinned to Vitest 5.0.1). `vite.config.ts` sets the
thresholds and the scope: `src/**/*.{ts,tsx}` without test files, the `src/test` fixtures and `main.tsx`. Vitest exits 1
when a threshold is missed; the `frontend` job's "Unit tests and coverage" step runs it in place of `npm test`. The SPA
holds no business rules by design (ADR-003: the backend is authoritative), so the frontend minimum is a global floor
rather than a per-feature one.

### 5.3 Baseline

Measured 2026-10-17 on this branch: 1,911 unit and 806 integration tests, `Persistence` excluded locally (its
throwaway-database tests time out on this workstation; CI runs them). Unit + integration:

| Module | Lines covered | Line coverage |
| --- | --- | --- |
| --- | --- | --- |
| Approval | 689 of 713 | 96.6% |
| AuditActivity | 900 of 909 | 99.0% |
| ChangeRequest | 766 of 785 | 97.6% |
| Closure | 932 of 976 | 95.5% |
| Common | 861 of 878 | 98.1% |
| Dashboards | 1168 of 1279 | 91.3% |
| DocumentManagement | 685 of 740 | 92.6% |
| ExternalParticipation | 1116 of 1175 | 95.0% |
| FinancialKpi | 1578 of 1793 | 88.0% |
| IdentityAccess | 1466 of 1545 | 94.9% |
| IntegrationMonitoring | 845 of 908 | 93.1% |
| ManagementConcern | 745 of 769 | 96.9% |
| MasterDataConfig | 1050 of 1211 | 86.7% |
| Milestone | 434 of 452 | 96.0% |
| Notifications | 805 of 880 | 91.5% |
| Progress | 630 of 648 | 97.2% |
| Project | 522 of 557 | 93.7% |
| ProjectTask | 643 of 670 | 96.0% |
| Reports | 1767 of 1970 | 89.7% |
| Risk | 869 of 892 | 97.4% |
| Schedule | 1302 of 1374 | 94.8% |
| Suspension | 500 of 522 | 95.8% |
| **All modules** | 20273 of 21646 | 93.7% |

Unit tests alone: 18.4% over the same lines (CV-2 fails in all 22 modules, CV-3 fails).

FG-04 MasterDataConfig was the closest module at 80.0% before this task. Its listing endpoints and the DRAFT edit of a KPI
definition ran in no test. `MasterDataConfig/AdministrationReadAndEditTests.cs` adds four tests for them, and they found
F-3 (§10).

### 5.4 Changing a threshold

- Raising a minimum is a normal PR.
- Lowering one, or adding an exclusion, needs a PR that edits `coverage-thresholds.json` or `vite.config.ts` and this
  record's §4 table together (TS-4 fails otherwise). `/docs/testing/` and `coverage.runsettings` are owned by
  `@baderalhindi/qa` (CODEOWNERS). Once the ruleset is applied, that makes QA's review mandatory. The PR must cite the PMO
  engagement lead's agreement.
- There are no per-module overrides. A module under the minimum is fixed by testing it, not by exempting it.

## 6. Step 18 scenarios and the automated layers

Step 18 holds 4,079 draft scenarios, one per controlled story, none executed. Each has an execution mode:
`BUSINESS / ADMIN USER ACCEPTANCE` (2,854) is run by AHDA testers; `QA-ASSISTED BUSINESS ACCEPTANCE` (1,225) needs QA to
supply technical evidence for an observable result ("screenshots alone cannot prove backend authorization, uniqueness or
immutable evidence", Step 18 EV-01).

How the layers serve the catalogue:

- **Automated tests are evidence, not a substitute.** No scenario is counted as covered because a test exists. Step 18
  records results per run (NOT RUN, PASS, FAIL, BLOCKED, NOT APPLICABLE) and only an executed UAT run sets them.
- **QA-assisted scenarios cite their integration tests.** For a QA-assisted scenario, the QA evidence of a run is the
  CI-run integration test that asserts the backend-only outcome (denial without mutation, exactly-once application, a
  pinned version), named by `Folder/ClassName.Method` and the CI run of the build under test. TASK-088 records the
  citation in the Execution_Log row.
- **TD-05 failure fixtures come from the integration suites.** Step 18's controlled failures (failed apply, uncertain
  timeout, duplicate request, recovery) are what the integration suites already construct: outbox redelivery, racing
  requests (`GovernanceApi.AtOnceAsync`), a stopped mail relay, a simulated downstream outage. QA reuses them on UAT data
  rather than inventing new ones.
- **The 15 journeys are automated end to end** (§7), because they check outcomes a single-module test can miss.

Domain map (TS-6 checks the counts against Step 18 and that every namespace and feature exists):

| Domain | Name | Scenarios | Business-run | QA-assisted | Integration namespaces (CI) | Frontend features |
| --- | --- | --- | --- | --- | --- | --- |
| WF-01 | Project creation and registration | 114 | 91 | 23 | `Project` | `projects` |
| WF-02 | Progress and Overall Health | 162 | 110 | 52 | `Progress`, `Execution` | `progress` |
| WF-03 | Schedule and baselines | 162 | 112 | 50 | `Schedule`, `Execution` | `schedule` |
| WF-04 | Tasks | 168 | 116 | 52 | `ProjectTask` | `tasks` |
| WF-05 | Milestone achievement | 162 | 112 | 50 | `Milestone`, `Execution` | `milestones` |
| WF-06 | Risk management | 176 | 122 | 54 | `Risk`, `RiskIssue` | `risks` |
| WF-07 | Issues and challenges | 184 | 122 | 62 | `ManagementConcern`, `RiskIssue` | `issues-challenges` |
| WF-08 | Change request and authorization | 198 | 136 | 62 | `ChangeRequest`, `Governance` | `change-requests` |
| WF-09 | Suspension and resumption | 188 | 121 | 67 | `Suspension`, `Governance` | `suspension-closure` |
| WF-10 | Completion and closure | 195 | 123 | 72 | `Closure`, `Governance` | `suspension-closure` |
| WF-11 | Shared approval framework | 200 | 138 | 62 | `Approval` | `approvals` |
| WF-12 | Document management | 210 | 144 | 66 | `DocumentManagement` | `documents` |
| WF-13 | External entity update and review | 210 | 154 | 56 | `ExternalParticipation` | `external-participation` |
| WF-14 | Financial progress and KPI performance | 210 | 180 | 30 | `FinancialKpi`, `Execution` | `financial-kpi` |
| WF-15 | Notification runtime | 200 | 145 | 55 | `Notifications` | `notifications` |
| FG-01 | Dashboards | 233 | 174 | 59 | `Dashboards` | `dashboards` |
| FG-02 | Reports | 243 | 183 | 60 | `Reports` | `reports` |
| FG-03 | Identity, roles and access | 212 | 150 | 62 | `Identity`, `EndpointHardening`, `InputValidation` | `identity-access` |
| FG-04 | Master data and configuration | 222 | 125 | 97 | `MasterDataConfig` | — (no FG-04 screens built) |
| FG-05 | Integration monitoring | 210 | 116 | 94 | `IntegrationMonitoring` | `integration-admin` |
| FG-06 | Audit and activity | 220 | 180 | 40 | `AuditActivity` | `audit-activity` |
| **Total** | | **4,079** | **2,854** | **1,225** | | |

`Persistence` (migrations, seed integrity, query plans) serves no single domain; it is the Data layer of §2.

## 7. Cross-domain journeys and their owning e2e suites

Each journey has one owning Playwright spec under `src/frontend/e2e/journeys/`, named after it. TASK-085 wrote every spec
(`e2e-suite.md`), so every row is **AUTOMATED**, and TS-5 requires the file to exist and to name its journey id. The evidence column lists the CI-run tests that already assert the
journey's acceptance focus at the API. They are what UAT's QA evidence cites before the browser suite exists (§6). They do
not replace the journey.

| Journey | Acceptance focus (Step 18) | Domains | Owning e2e suite | Status | API-level evidence today (CI-run) |
| --- | --- | --- | --- | --- | --- |
| UJ-001 | Registration and Planned→Active follow Project-master governance; no standalone activation workflow | WF-01 | `src/frontend/e2e/journeys/uj-001-registration-activation.spec.ts` | AUTOMATED | `Project/RegistrationLifecycleTests`, `Project/ProjectLifecycleEndToEndTests`, `Schedule/ActivationGateTests` |
| UJ-002 | WF-02 owns Overall Project Health; missing or stale inputs never become zero or Green | WF-02 | `src/frontend/e2e/journeys/uj-002-progress-overall-health.spec.ts` | AUTOMATED — expected failure on e2e-suite.md F-1 | `Execution/OverallHealthAuthorityTests`, `Progress/PublicationTests`, `Progress/ReportingCycleTests` |
| UJ-003 | Schedule and achievement keep split authority over one shared milestone | WF-03, WF-05 | `src/frontend/e2e/journeys/uj-003-milestone-authority.spec.ts` | AUTOMATED | `Execution/SharedMilestoneTests`, `Milestone/MilestoneGuardTests` |
| UJ-004 | Approval is distinct from source application; a failed application is visible and never shown as applied | WF-11, WF-08 | `src/frontend/e2e/journeys/uj-004-approval-versus-implementation.spec.ts` | AUTOMATED | `ChangeRequest/ChangeAuthorizationTests`, `Governance/ChangeAuthorizationIdempotencyTests`, `Approval/OutcomeCallbackTests`, `change-requests/changeRequests.test.tsx` — gap G-1 |
| UJ-005 | Accepting an external contribution does not mutate source facts; application is explicit and scoped | WF-13 | `src/frontend/e2e/journeys/uj-005-external-contribution-staging.spec.ts` | AUTOMATED | `ExternalParticipation/SourceApplicationTests`, `ExternalParticipation/ExternalParticipationGuardTests` |
| UJ-006 | Submitted evidence keeps its exact immutable CLEAN version | WF-12 | `src/frontend/e2e/journeys/uj-006-exact-evidence-version.spec.ts` | AUTOMATED | `DocumentManagement/EvidenceAvailabilityTests`, `Milestone/AchievementEvidenceTests` |
| UJ-007 | The source owns the business condition, WF-15 the delivery; delivery or retry never changes business state | WF-15 | `src/frontend/e2e/journeys/uj-007-reminder-ownership.spec.ts` | AUTOMATED | `ManagementConcern/ConcernEscalationTests`, `Notifications/CommitBeforeNotifyTests`, `Notifications/ReminderRevalidationTests` |
| UJ-008 | Read projections keep source ownership, scope and freshness; reporting is not a write path | FG-01, FG-02 | `src/frontend/e2e/journeys/uj-008-governed-read-projections.spec.ts` | AUTOMATED | `Dashboards/DashboardRuntimeTests`, `ExternalParticipation/EntityIsolationTests` — gap G-2 |
| UJ-009 | Current, Published and Historical stay distinguishable; history is retained | FG-01, FG-02 | `src/frontend/e2e/journeys/uj-009-current-published-historical.spec.ts` | AUTOMATED | `Execution/PublishedSnapshotTests`, `Progress/PublicationTests`, `Reports/ReportRuntimeTests` |
| UJ-010 | Missing and stale stay distinguishable; absence is never zero or Green | FG-01, FG-02 | `src/frontend/e2e/journeys/uj-010-missing-and-stale.spec.ts` | AUTOMATED | `FinancialKpi/UnknownValueTests`, `Dashboards/DashboardRuntimeTests`, `Reports/ReportRuntimeTests`, `dashboards/dashboards.test.tsx` |
| UJ-011 | After a context is revoked the operation, direct link, download and export are denied | FG-03 | `src/frontend/e2e/journeys/uj-011-contextual-access-revocation.spec.ts` | AUTOMATED | `Identity/AuthorizationEndpointTests`, `Identity/AdministrationEndpointTests`, `Reports/ReportAcceptanceTests`, `ExternalParticipation/EntityIsolationTests` — gap G-3 |
| UJ-012 | Published configuration resolves deterministically; drafts never govern; pinned context is kept | FG-04 | `src/frontend/e2e/journeys/uj-012-published-configuration.spec.ts` | AUTOMATED | `MasterDataConfig/ConfigurationResolutionTests`, `RiskIssue/RiskRatingPinningTests`, `RiskIssue/ConcernSeverityPinningTests`, `Identity/FieldClassificationTests` |
| UJ-013 | Technical recovery changes no business lifecycle and duplicates no application | FG-05 | `src/frontend/e2e/journeys/uj-013-technical-recovery.spec.ts` | AUTOMATED | `IntegrationMonitoring/IntegrationFailureSimulationTests`, `IntegrationMonitoring/InvocationEngineTests`, `IntegrationMonitoring/ReconciliationTests` |
| UJ-014 | Formal Audit, Activity and operational logs stay distinct, correlated and restricted | FG-06 | `src/frontend/e2e/journeys/uj-014-audit-versus-activity.spec.ts` | AUTOMATED | `AuditActivity/ActivityRebuildTests`, `AuditActivity/AuditAccessTests`, `AuditActivity/RedactionTests` |
| UJ-015 | Exports are PDF, XLSX and CSV with scope, filters and restricted-field exclusion kept | FG-02 | `src/frontend/e2e/journeys/uj-015-committed-report-exports.spec.ts` | AUTOMATED | `Reports/ReportRuntimeTests`, `Reports/ReportAcceptanceTests`, `reports/reports.test.tsx` |

Gaps TASK-085 had to close in the browser suite, because no API-level test asserted them. UJ-004, UJ-008 and UJ-011 now do,
except G-1's visible failure, which the product does not show (`e2e-suite.md` F-4):

- **G-1 (UJ-004).** Failed application is shown as failed and never as applied for a **WF-08** change authorisation.
  `Approval/OutcomeCallbackTests` proves a failed outcome handler leaves the source unapplied and its retry applies once,
  and WF-13's `SourceApplicationTests` shows APPLICATION_FAILED. No test drives a WF-08 application failure to the screen.
- **G-2 (UJ-008).** "Reporting does not become a source write path" is held structurally by the architecture tests
  (`ModuleBoundaryTests`, `PersistenceBoundaryTests.NoSqlWriteStatementExistsOutsidePersistence`), not by a behavioural
  test.
- **G-3 (UJ-011).** Revocation is tested per surface: the next request, the report download, the entity's list. No single
  test revokes once and then tries the operation, the direct link, the download and the export together, which is the
  journey's procedure.

UJ-001's "no standalone Project Activation workflow" is structural: activation is the `activate` command after a WF-11
registration approval (`RegistrationLifecycleTests`), and no `PROJECT_ACTIVATION` approval type exists.

## 8. Environments and when each layer runs

| Where | What runs | Gate |
| --- | --- | --- |
| Pull request to `dev`, `stage`, `main` | unit, integration, contract, data, coverage, axe component tests, security scans, `repo-checks` | required checks of `protected-branches.ruleset.json` |
| Push to `main` | the same, called by `ci-cd-pipeline.yml` before any deployment | the pipeline's first stage |
| SIT, nightly and on promotion (TASK-085) | the 15 journey specs and ADR-013's, against SIT — until SIT exists, nightly against the e2e stack (`e2e-suite.md` §3) | a failing journey blocks promotion to UAT (`e2e-sit`) |
| Before UAT entry | performance (TASK-086), accessibility audit (TASK-087), `findings-log-check.py --gate` | Step 18 18-G-01 and the Release Checklist rows |
| UAT (TASK-088) | Step 18 scenarios by AHDA testers with QA evidence (§6); prepared in `uat/uat-execution.md`, not yet executed | `uat-catalogue.py --gate`; 18-G-05: exit criteria AHDA approves (proposed in `uat/uat-execution.md` §7) |

SIT and UAT do not exist yet. Every environment apply is held on the hosting confirmation (penetration-test.md §2).

## 9. Test data

Step 18's fixture classes are met as follows; the UAT data set is TASK-088's (`uat/uat-execution.md` §5).

| Step 18 | Need | Automated suites today |
| --- | --- | --- |
| TD-01 | authorized, out-of-scope, revoked and external identities | `local.r01`…`local.r08` in the test directory and `IdentityDatabase`, scoped by department and entity |
| TD-02 | source-valid and ineligible lifecycle states | each module's test host and fixtures, e.g. `ChangeRequest/Fixtures/ChangeAuthorizationFixture` |
| TD-03 | CLEAN, later, non-CLEAN and inaccessible document versions | the test scanner and EICAR in `DocumentManagement` |
| TD-04 | current, published, historical, missing, stale, Unknown, N/A, genuine zero | `FinancialKpi`, `Dashboards`, `Reports` fixtures |
| TD-05 | failed apply, timeout, duplicate request, recovery | outbox redelivery, racing requests, the stopped mail relay, the simulated outage (§6) |

Tests create their own data in their own database or collection; none reads production-shaped data, and none uses a real
document or a real person's details.

## 10. Rules and findings

Rules every change follows:

- A new module's integration tests get a CI step in the same PR (TS-1 fails the PR otherwise). The filter ends in `.` so
  it runs that namespace alone.
- No retry-on-failure in CI. A known flake may be rerun once with `gh run rerun <id> --failed`; a second failure is
  investigated as a defect.
- A test that pins a repo-wide count (permission count, seeded labels, operations) moves with the change that alters it.

Findings of this task:

| # | Finding | Disposition |
| --- | --- | --- |
| F-1 | The strategy needs the PMO engagement lead's approval (workbook AC). | **Open** — §11 |
| F-2 | 27 integration test files in `Approval` (WF-11), `MasterDataConfig` (FG-04) and `Notifications` (WF-15) ran in no CI step, the same gap DocumentManagement had until TASK-081. | **Closed** — three steps in the new `backend-platform-services` job; TS-1 prevents a recurrence |
| F-3 | Every admin text search containing `_`, `%` or `\` found nothing: `ContainsPattern` escaped them with `\`, but Npgsql translates the two-argument `EF.Functions.ILike` with `ESCAPE ''`, so the backslash was matched literally. A search for a code such as `RISK_MATRIX` returned an empty page. Affected: users, departments, external entities, master-data items, KPI definitions, projects, documents. | **Closed** — `AdministrationPersistence.LikeEscape` passed to all 17 calls; `AdministrationReadAndEditTests` searches with `_` and fails without the fix |
| F-4 | The acceptance criterion's wording is "unit-test coverage"; this strategy gates unit + integration coverage (D-1, §3). | **Open** — approver to confirm D-1 |
| F-5 | The ruleset is not applied on GitHub (`apply.sh` needs an authenticated admin run; `dev` reports "Branch not protected"), so `coverage` — like every other check — turns a PR red but blocks no merge until it is. | **Open** — repository owner, as for TASK-015 F-1 |
| F-6 | No journey is automated end to end; three have API-level gaps (G-1 to G-3). | **Closed** — TASK-085 automated all 15 and closed G-2 and G-3; G-1's "shown as failed" half is a product gap (`e2e-suite.md` F-4) |
| F-7 | The performance threshold (PTBC-043) and the accessibility conformance level (PTBC-040) are AHDA's to set. | **Open** — AHDA. TASK-086 judged its run against a proposed budget, set before the run and re-judged by `load-test-report.py --judge` once AHDA confirms (`load-test.md` D-1). TASK-087 audited against WCAG 2.1 AA, the working baseline; a different level means re-running the audit with that level's rules (`accessibility-audit.md` D-1) |
| F-8 | `NotificationInboxEndpointTests.TheBadgeCounts…` failed the first time it ran in CI (F-2): marking a notification read answered the tracked entity's `readAt` at the Linux clock's 100 ns precision, while PostgreSQL stores microseconds, so a second read answered a different `readAt`. Invisible on macOS, whose clock ticks in microseconds. | **Closed** — `NotificationInbox.MarkReadAsync` stamps at microsecond precision |
| F-9 | On a slow runner (builds of 115–127 s against 82 s on `dev`) every step runs about 1.4× longer, and `backend-risk-governance` (10m17s) and `backend` (9m41s) reached their ten-minute cap on this PR's first runs. | **Closed** — a seventh job, `backend-platform-services`, takes seven steps (ci-quality-gates.md F-4: split rather than raise the cap) |

## 11. Approval

| Role | Name | Decision | Date | Reference |
| --- | --- | --- | --- | --- |
| PMO engagement lead | *to be named by AHDA* | *pending* — including D-1 (§3) | | |
| QA lead (owner of `docs/testing/`) | | *pending* | | |

Until both rows are filled, this record is a draft: the gate runs and fails red, but the policy it enforces may still
change in review.

## 12. Verification

Run on this branch, 2026-10-17. Backend on the local SDK with PostgreSQL 17, `AHDA-ldap` and Redis in Docker; frontend in
the `AHDA-frontend` container (Node 24, as `.nvmrc`).

| # | Check | Result |
| --- | --- | --- |
| 1 | `dotnet build src/backend -c Release -warnaserror` | 0 warnings, 0 errors |
| 2 | Unit tests with `-s src/backend/coverage.runsettings` | 1,911 passed; report holds only `PMPlatform.Domain` and `PMPlatform.Application`, nothing under `obj/` |
| 3 | Integration tests with the run settings, `Persistence` excluded (§5.3) | 806 passed, 0 failed |
| 4 | `coverage-check.py` over 2 and 3 | **exit 0**: 22 modules, lowest 86.7% (MasterDataConfig), total 93.7% |
| 5 | `coverage-check.py` over 2 alone (the unit-only reading of D-1) | **exit 1**: 18.4%, CV-2 in all 22 modules, CV-3 |
| 6 | `CI=true dotnet test PMPlatform.Tests.Unit --no-build` with no `-s` | a Cobertura report is written (Directory.Build.props); without `CI=true`, none |
| 7 | Coverage overhead, `Project.` integration subset | 15.1 s without, 16.1 s with |
| 8 | `AdministrationReadAndEditTests` with F-3's fix reverted | 2 of 4 fail; with the fix, 27 of 27 MasterDataConfig tests pass |
| 9 | `npm run test:coverage` | **exit 0**: 1,018 passed; 88.4% lines, 78.9% branches |
| 10 | `vitest run --coverage --coverage.thresholds.lines=95` | **exit 1**: "Coverage for lines (88.36%) does not meet global threshold (95%)" |
| 11 | `npm run lint`, `format:check`, `typecheck`, `build` | exit 0 each |
| 12 | `test-strategy-check.py` | 0 findings; 15 seeded violations (TS-1 ×1, TS-2 ×3, TS-3 ×3, TS-4 ×2, TS-5 ×4, TS-6 ×2) each reported and exit 1 |
| 13 | Every other `repo-checks` script and `migration-forward-only.sh` | exit 0 each |
| 14 | `actionlint` 1.7.7 over `.github/workflows/` | exit 0, no findings |
| 15 | `@vitest/coverage-v8` lockfile delta | 8 packages added, all dev, none changed; `npm audit`: the one High (`source-map-js`) is already on `dev` |
| 16 | CI on PR #96 (run 38969753960) | every check green; `coverage` merged 28 reports (one per test step of the four backend jobs, `Persistence` included): lowest module 86.7% (MasterDataConfig), total 93.7%. Jobs: `backend` 8m35s, `backend-risk-governance` 5m44s, `backend-endpoint-hardening` 5m45s, `backend-platform-services` 7m0s, `frontend` 2m35s |
| 17 | Validation drill, draft PR #97 (run 38969754827): this branch plus 130 untested lines in `Features/Milestone` | `coverage` **failed**: "CV-2 Milestone: 74.3% of 584 lines, below 80%"; every other check green. Closed with its branch deleted |
| 18 | First CI runs of #96 (before F-8, F-9) | `Notifications` failed on F-8; `backend-risk-governance` hit its cap (F-9); `RateLimitingTests.ASignInBurstBeyondTheLimitIsThrottledWith429AndRetryAfter` failed once on a missing log line and passed on every later run, as it has on `dev`. It is recorded here as seen, not explained |

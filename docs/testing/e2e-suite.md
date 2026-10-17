# Cross-Domain End-to-End Journey Suite

| Field | Value |
| --- | --- |
| Task | TASK-085 — Implement Cross-Domain End-to-End Test Suite (P15 - Testing & QA, P0) |
| Depends on | The contract and integration suites of the Project Lifecycle (`project-lifecycle-contract-tests.md`), Execution (`execution-domain-contract-tests.md`), Risk/Issue (`risk-issue-contract-tests.md`) and Governance (`governance-contract-tests.md`) domains; TASK-084's strategy (`test-strategy.md` §7) |
| Record date | 2026-10-17 |
| Status | **BUILT. 16 of 17 journeys pass; UJ-002 fails on product gap F-1 (§9), by decision.** SIT does not exist, so the suite runs nightly against the e2e stack that stands in for it until `vars.E2E_SIT_BASE_URL` is set (§3, F-1 below) |
| Branch | `test/task-085-cross-domain-e2e-suite` |
| Directory | `src/frontend/e2e` |
| Deliverables | the Playwright suite — the 15 Step 18 journeys (`journeys/uj-nnn-*.spec.ts`) and ADR-013's participation journeys (`participation/`); its full-stack environment (`stack/`); CI scheduling and the UAT gate (`.github/workflows/e2e-journeys.yml`, the `e2e-sit` job of `ci-cd-pipeline.yml`) |
| Environment variables / secrets | `E2E_TEST_USER_CREDENTIALS_SECRET_REF` (reference only; the credentials stay in the secret store, §4) |
| Participation amendment | ADR-013: denied cross-entity access tests and the entity Project Manager journey (§2) |
| Sources | Workbook TASK-085 row; Step 18 UAT Catalogue v1.0 `UAT_Journeys` (procedure, expected result, negative extension of each journey); ADR-013; `test-strategy.md` §6–§10 |

## 1. What the suite proves

Each Step 18 journey crosses modules, so a single-module test can pass while the journey fails. The suite drives each journey
through the browser against a deployed stack, as the people Step 18 names, and asserts its expected result and its negative
extension: "Denied scope and stale/replayed request; confirm source-appropriate failure without unauthorized mutation". Every
journey therefore also proves, with exact status and problem codes, that an out-of-scope person is refused, that a stale
(`If-Match`) or replayed command is refused, and that the record did not change.

The suite is evidence for UAT (`test-strategy.md` §6), not a substitute for it. It does not execute Step 18 scenarios or set
their results (TASK-088).

## 2. The journeys

| Journey | Spec | What it proves end to end |
| --- | --- | --- |
| UJ-001 | `journeys/uj-001-registration-activation.spec.ts` | A draft registered through the form (after a form sign-in), submitted, reviewed, approved in the inbox and activated by the command only after a WF-03 baseline (422 before); no activation workflow exists beside the registration review. Stale `If-Match` 412, replayed activation 409, another department 404 |
| UJ-002 | `journeys/uj-002-progress-overall-health.spec.ts` | Task progress, a schedule slip turning schedule health Red while Overall Project Health stays "No data", never zero or Green; then the progress update, its publication and the comparison. **Fails at the progress update: F-1** |
| UJ-003 | `journeys/uj-003-milestone-authority.spec.ts` | One milestone planned in WF-03, baselined, reforecast, claimed and accepted in WF-05: each view shows its own facts and both reconcile; the approver cannot correct the achievement, the planner cannot re-date it |
| UJ-004 | `journeys/uj-004-approval-versus-implementation.spec.ts` | A WF-08 change approved through WF-11 stays "Issued, not applied" until its rebaseline; a QA "failed apply" fixture refuses the application, the screens never show it applied, and recovery applies it exactly once (G-1, see F-4) |
| UJ-005 | `journeys/uj-005-external-contribution-staging.spec.ts` | An entity contributor's answer, accepted internally, leaves the task untouched until an explicit "Apply to source"; the replayed apply is idempotent |
| UJ-006 | `journeys/uj-006-exact-evidence-version.spec.ts` | Evidence pinned on its CLEAN version 1 stays version 1 through a later upload, acceptance and a correction; pending and quarantined versions are refused; a stale attach is refused (it was not: F-3, fixed) |
| UJ-007 | `journeys/uj-007-reminder-ownership.spec.ts` | A WF-07 escalation whose WF-15 e-mail fails against a refusing relay, retries, dead-letters and is redriven once by the operator; the issue's status, revision and ETag never move |
| UJ-008 | `journeys/uj-008-governed-read-projections.spec.ts` | Dashboards and reports match the source records with the same owner, scope and freshness; every consumer control sends only the projection's own writes and leaves source ETags unchanged (G-2) |
| UJ-009 | `journeys/uj-009-current-published-historical.spec.ts` | Current values change while Published and Historical values stay as published, side by side and labelled |
| UJ-010 | `journeys/uj-010-missing-and-stale.spec.ts` | Five projects — Green, missing, Unknown, N/A, stale — each shown with its own meaning; none of the absences as zero or Green |
| UJ-011 | `journeys/uj-011-contextual-access-revocation.spec.ts` | One context granted in the admin UI and ended there; the still-open pages' operation, download and export and the direct links are all denied at once (G-3) |
| UJ-012 | `journeys/uj-012-published-configuration.spec.ts` | A draft risk-matrix version never governs; once published it governs new assessments while an earlier assessment keeps its pinned version; three-person separation of duties |
| UJ-013 | `journeys/uj-013-technical-recovery.spec.ts` | A dead-lettered directory reconciliation replayed in the FG-05 screens: the attempt lineage completes, no record or lifecycle changes, a second replay is refused |
| UJ-014 | `journeys/uj-014-audit-versus-activity.spec.ts` | A task created in the browser appears once in Activity and once in formal audit, correlated by id; each tab reads only its own store; people without audit permission are refused (operational logs: F-5) |
| UJ-015 | `journeys/uj-015-committed-report-exports.spec.ts` | PDF, XLSX and CSV downloaded and read: same rows, parameters, classification and sources; restricted amounts excluded for a consumer without the classification; no Word export offered |
| ADR-013 | `participation/adr-013-entity-project-manager.spec.ts` | The entity's own Project Manager drafts (API, F-7), submits and then manages their project in the browser; every AHDA gate — review, approvals, baseline approval, activation — stays refused to them |
| ADR-013 | `participation/adr-013-cross-entity-access-denial.spec.ts` | Entity B's Project Manager reaches nothing of entity A's project through the list, direct links, records, filtered collections or writes; entity A's contributor makes no PM-only write |

## 3. Where it runs

| Target | When | What it is |
| --- | --- | --- |
| **SIT** | nightly once `vars.E2E_SIT_BASE_URL` is set; on every promotion (`e2e-sit`, §8) | the deployed release, AHDA's providers, the data set loaded by code (§5), credentials from the secret store (§4) |
| **e2e stack** (`src/frontend/e2e/stack/compose.yml`) | nightly until SIT exists; on pull requests that change the suite; locally | the release built from the commit under test: the API image migrating a fresh database, the production SPA build, a TLS edge, provisioned with the e2e data set |

The e2e stack differs from SIT only by what a deployed environment has and a laptop or runner does not:

| # | Stand-in | SIT has instead |
| --- | --- | --- |
| S-1 | The test directory plus the e2e people (`stack/ldap/e2e-people.ldif`) | AHDA's directory with the synthetic e2e accounts |
| S-2 | An MFA provider accepting one code (`stack/standins/standins.mjs`) | AHDA's MFA provider; the persona's `totpSecret` (§4) |
| S-3 | A mail relay that can be told to refuse every recipient | the Exchange relay; QA's controlled relay outage |
| S-4 | A malware scanner deciding by file name (`infected`, `unscannable`, `held`), writing its verdict to the database because the platform has no network scanner adapter to stand in for (`document-management.md` F-2) | the selected scanning provider; an EICAR file and a way to hold a version pending |
| S-5 | `vite preview` serving the production build with the deployed host's headers, behind Caddy with its internal CA | the static host behind the real certificate |
| S-6 | Named fixtures run as SQL by the stand-ins (`stack/standins/fixtures`, §5) | QA's fixture service at `E2E_FIXTURE_CONTROL_URL` |
| S-7 | Workers polling every 2–30 s instead of minutes | the environment's own intervals; the journeys wait with a timeout, never a sleep |

## 4. Configuration

| Variable | Meaning |
| --- | --- |
| `E2E_BASE_URL` | the SPA's origin; the API is its `/api/v1` |
| `E2E_TARGET` | `compose` or `sit`; on `compose` the browser accepts the stack's internal CA |
| `E2E_TEST_USER_CREDENTIALS` / `E2E_TEST_USER_CREDENTIALS_FILE` | the personas' credentials as JSON, inline or in a file |
| `E2E_FIXTURE_CONTROL_URL` | QA's named fixtures (§5) |

`E2E_TEST_USER_CREDENTIALS_SECRET_REF` names the secret-store entry holding that JSON. The workflow reads it with the
`E2E_SECRET_READER_KEY` service account, masks every password, code and TOTP secret, and hands the file to the suite; nothing
else ever sees it. The JSON maps each persona to `{ "username", "password" }`, plus `mfaCode` (the stand-in) or `totpSecret`
(a real enrolment; the suite computes the RFC 6238 code) for the administrator:

| Persona | Holds |
| --- | --- |
| `administrator` | R01 |
| `portfolioManager` | AHDA's reviewer, approver and activator (E2E-R02, ALL) |
| `departmentManager`, `otherDepartmentManager` | E2E-R03 on DEPT-E2E and on DEPT-E2E-OTHER |
| `projectManager` | an internal Project Manager (E2E-R04) |
| `executive`, `viewer` | shipped R07, R06 |
| `entityProjectManager`, `entityContributor` | entity A: shipped R04 + R08, and R08 alone |
| `otherEntityProjectManager` | entity B: shipped R04 + R08 |

## 5. Test data

Every journey creates its own projects and records through the product, with unique titles, and never depends on another
journey or on run order. What it cannot create, the environment provides (Step 18 TD-01 to TD-05):

- **The e2e data set** (`stack/provision/*.sql`, in file order): the departments, entities and people; the test profiles
  E2E-R02/R03/R04 beside the untouched shipped ones (F-2); published master data (`E2E_*` codes); the published configuration
  families the journeys need — governance profile, workflow policy, approval authority, materiality bands, notification
  routing with its templates, participation, risk matrix, KPI definition, report rules; the document permissions' clearance.
  Every reference is found by its code through the API, so the same journey runs where ids differ. On SIT the data set is an
  entry precondition, loaded once by the environment's DBA and replaced by AHDA's approved values as they arrive.
- **Named fixtures** — QA's controlled conditions (TD-05), by name, through the fixture service:

| Fixture | Used by | Stands in for |
| --- | --- | --- |
| `uj-004-failed-apply` | UJ-004 | a source application that fails (lock timeout) and later succeeds |
| `uj-007-retry-due` | UJ-007 | the passing of a failed delivery's back-off |
| `uj-009-published-progress-period` | UJ-009, UJ-010, UJ-015 | WF-02's publication of a period, until F-1 lets WF-02 publish |
| `uj-010-overdue-progress-period` | UJ-010 | an open period past its due date |
| `uj-013-reconciliation-dead-letter` | UJ-013 | a directory outage during a reconciliation, retried to dead letter |

## 6. Writing a journey

One `test('UJ-0nn …')` per journey, so a journey is one pass/fail line; `test.step`s that read as the Step 18 procedure; a
header quoting the procedure, expected result and negative extension. The browser carries what a person does and sees; the API
prepares what the journey needs before its own steps and sends the denied, stale and replayed requests. Text containing a
translated value is compared without its isolate marks (`expectText`). No sleeps (`eventually` polls), no retries
(`retries: 0`, `test-strategy.md` §10). Shared helpers are in `src/frontend/e2e/support`.

## 7. Running it

```sh
src/frontend/e2e/stack/run.sh                              # build the stack from this tree, fresh database, every journey
src/frontend/e2e/stack/run.sh journeys/uj-004              # one journey
E2E_REUSE_STACK=1 src/frontend/e2e/stack/run.sh …          # against the stack already up
src/frontend/e2e/stack/provision.sh                        # re-apply the data set to a running stack
docker compose -f src/frontend/e2e/stack/compose.yml down -v   # remove it (never --remove-orphans)
```

The stack joins the local `ahda` group on a network of its own, so it never touches the local development stack. Reports
land in `src/frontend/playwright-report` (HTML, traces and screenshots of failures) and `src/frontend/test-results` (JUnit,
JSON).

## 8. CI: schedule, history and the UAT gate

- **Nightly** (`e2e-journeys.yml`, 00:40 Riyadh): against SIT when `vars.E2E_SIT_BASE_URL` is set, otherwise against the e2e
  stack built from the default branch.
- **Pass/fail history.** Every run attaches the HTML report, traces and JUnit/JSON results for 90 days and writes a
  per-journey table to the run summary (`e2e-summary.py`, which counts a journey that did not run as not passed). Every nightly
  run comments that table on the issue "Nightly cross-domain journeys", assigned to the QA owner in
  `.github/testing/e2e-owner.yaml`; a red night reopens it (`e2e-history.sh`). The Actions list for `e2e-journeys` is the same
  history with the reports.
- **Promotion.** `ci-cd-pipeline.yml`'s `e2e-sit` runs the suite against SIT with the release just deployed there, and
  `deploy-uat` needs it: a failing journey blocks promotion to UAT before any reviewer is asked. That is the first
  precondition of the `uat` environment (`infra/environments/github/environments.json`).
- **Pull requests** that change the suite, its stack, the reporting scripts or `infra/docker` run it against their own stack.
- TS-7 in `test-strategy-check.py` (repo-checks) fails a PR that removes the schedule or the gate, lets Playwright retry, or
  adds a journey spec the strategy's table does not own.

To point the suite at SIT, set repository variables `E2E_SIT_BASE_URL` and `E2E_SIT_FIXTURE_CONTROL_URL` and repository
secrets `E2E_TEST_USER_CREDENTIALS_SECRET_REF` and `E2E_SECRET_READER_KEY` (a service account allowed to read that one secret).

## 9. Findings

| # | Finding | Disposition |
| --- | --- | --- |
| F-1 | **WF-02 cannot derive or publish progress anywhere.** The only `IProgressInputs` is `NoProgressInputs`, so every progress update answers 422 `PROGRESS_ROLLUP_UNAVAILABLE` and the screen says the project "has no work breakdown", which is untrue. No period is ever stored, so WF-14 cannot publish figures either (`FINANCIAL_NOTHING_TO_REPORT`). UJ-002 asserts what Step 18 requires and fails here; UJ-009, UJ-010 and UJ-015 use a fixture for WF-02's publication. SIT does not exist yet either. | **Open** — `progress-update.md` F-1, Engagement Architect; UJ-002 stays red by decision (2026-10-17) until it is built |
| F-2 | The shipped permission matrix grants nobody AHDA's review, approval or activation permissions, and gives `USER_VIEW` and `CONFIGURATION_MANAGE` to R01 alone. The data set adds test profiles E2E-R02/R03/R04 for the e2e people only (`20-authority.sql`). Several test values also stand in for AHDA's (approval routes, bands, matrix, routing, KPI definition, report rules, document clearance). | **Open** — AHDA's matrix and configuration replace them on SIT |
| F-3 | **Evidence `If-Match` ignored.** Attaching or withdrawing a milestone claim's evidence, or an Approved Budget version's referenced document, ignored a stale `If-Match` and linked or withdrew anyway (R-21; `milestone-achievement.md` §4). The link's own save reset the revision's expected version before the revision was saved. | **Closed** — the four commands refuse a stale `If-Match` before writing; `AchievementEvidenceTests` and `FinancialProgressTests` fail without the fix |
| F-4 | **G-1: a failed WF-08 application is not shown as failed.** While the outcome's delivery fails, every screen shows "not applied / not yet sent", the same as a pending one; the outbox's attempts and error are on no screen, and a dead-lettered outcome has no redrive. UJ-004 asserts that the failure is never shown as applied and that recovery applies it once. | **Open** — product gap, recorded by decision (2026-10-17); `integration-monitoring.md` F-5 (the outbox is not observed) |
| F-5 | **Operational logs cannot be correlated.** R-42 puts the correlation id on every log line; the API logs with the default console formatter without scopes, so the id of a request appears on none. The SPA also sends no `X-Correlation-Id` (R-41); the server's own id still correlates audit and Activity. UJ-014 cannot assert the log line, and neither the stack nor SIT gives the suite log access. | **Open** — JSON console logging with scopes, and a SIT log query for QA |
| F-6 | The nightly history issue and the scheduled runs are owned by the QA Lead; until AHDA names one the repository owner holds the role (`.github/testing/e2e-owner.yaml`). | **Open** — AHDA |
| F-7 | **Entity users cannot use the forms that need reference lists.** The shipped R04 and R08 hold neither `MASTER_DATA_VIEW` nor `ORGANIZATION_VIEW`, so the registration form tells the entity PM it cannot read its lists and keeps Create draft disabled, and Register risk and Raise issue keep Save disabled. ADR-013's entity draft is possible only through the API; the ADR-013 journey drafts there. | **Open** — matrix, AHDA PMO |
| F-8 | WF-15 deliveries have no operator screen; the redrive is API-only (`notification-center-ui.md`). | **Open** — a later admin UI task |
| F-9 | Smaller observations: the issues-challenges Escalate dialog has no accessible name; SCR-115 shows unresolved names ("User 00000000"); the approval inbox names a request by type and revision, not title; the Project Report's PDF truncates its 25 default columns; the export dialog warns "Sensitive" for a file it then classifies Standard; a narrative returned with `language: "EN"` is refused 400 when sent back; an R08 contributor's withdraw of an entity project is refused only by its state (409), not by permission. | **Open** — recorded for their owners; none fails a journey |

## 10. Verification

Run on this branch, 2026-10-17.

| # | Check | Result |
| --- | --- | --- |
| 1 | `src/frontend/e2e/stack/run.sh` on a fresh stack built from the branch | 16 passed, UJ-002 failed at the progress update (F-1); 4.7 min |
| 2 | Each journey run at least twice in a row while it was written | green, except UJ-002 (F-1) and UJ-006 before F-3's fix |
| 3 | `AchievementEvidenceTests`, `FinancialProgressTests` with F-3's checks disabled | 2 of 2 new tests fail; with them, Milestone 36/36 and FinancialKpi 16/16 pass |
| 4 | `test-strategy-check.py` with five seeded violations (no schedule, `deploy-uat` without `e2e-sit`, gate not on SIT, `retries: 1`, an unlisted spec) | each reported as TS-7, exit 1; clean tree 0 findings |
| 5 | `cicd-pipeline-check.py`, every other repo check, `actionlint` 1.7.7, `shellcheck -S warning` | exit 0 |
| 6 | `npx tsc -p tsconfig.e2e.json`, `eslint e2e`, `prettier --check e2e` | clean |
| 7 | Trivy over `package-lock.json` (Playwright 1.63.0 added) | no CRITICAL; the one HIGH (`source-map-js`) is already on `dev` |
| 8 | `e2e-summary.py` over run 1 | 16 passed, 1 not passed, exit 1 |
| 9 | Validation drill: activation gate removed in a draft PR | see the PR |

# CI Quality Gates

| Field | Value |
| --- | --- |
| Task | TASK-015 — Configure Quality Gates: Lint, Format, Type-Check, Test in CI (P2 - Project Setup & Foundation) |
| Depends on | TASK-011 — Initialize Monorepo & Solution Structure (`docs/architecture/monorepo-bootstrap.md`, BUILT) |
| Record date | 2026-09-22 |
| Status | **BUILT — PROVISIONAL ON APPLYING THE RULESET.** The three jobs are green on a clean checkout and red on each violation class (§4); making them required checks needs one authenticated `apply.sh` run that this environment cannot make (§6 F-1) |
| Branch | `infra/task-015-ci-quality-gates` |
| Deliverables | `.github/workflows/ci-quality-gates.yml` (renamed from `ci.yml`); `repo-checks` appended to `.github/branch-protection/protected-branches.ruleset.json`; `branch_types` corrected in `.github/scripts/pr-policy.sh` (§3.5); Vitest in `src/frontend` (`package.json`, `vite.config.ts`, `src/App.test.tsx`); `CONTRIBUTING.md` "Quality baselines"; `branching-strategy.md` §5/§7/§8; this record |
| Environment variables / secrets | None (workbook column) |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), Implementation Plan sheet, TASK-015 row, as read 2026-09-22 |
| Controlled sources and authority order | per TASK-001 |

## 1. What this record is

TASK-011 already wrote the checks: analyzers and `TreatWarningsAsErrors` in `Directory.Build.props`, ESLint and
Prettier configs, `strict` TypeScript, the A-1 to A-6 architecture tests. What did not exist was a barrier — a PR
could introduce a lint error and merge, because `ci.yml` ran only on PRs to `main` and no rule required it.

This task is therefore not "add linting". It is four decisions about the barrier: what belongs inside the gate and
what does not (§3.1), how the gate stays a required check while the workflow file is renamed (§3.3), what to do
with the four hand-run consistency scripts that three earlier records deferred here (§3.2), and what CTL-39 names
that still cannot be enforced today (§5, §6).

## 2. No gate decision

TASK-015's Gate Decision cell is empty, and its Environment Variables cell reads `None`. The workflow consumes no
secret and reaches no external system beyond the Actions runner's own package feeds, so nothing here is contingent
on ADR-001 (hosting) or ADR-002 (stack) being answered. If ADR-002 Q1 replaced the stack, the job *names* survive
and only their steps change — which is why the names, not the steps, are what branch protection is bound to (§3.3).

## 3. What was built

### 3.1 Three jobs, and the line around them

`.github/workflows/ci-quality-gates.yml` runs on every pull request to `main`, `dev` and `stage` and on every push
to them. No `paths-ignore`: a required check that is skipped is a required check that never reports, which blocks
the PR rather than passing it.

| Job | Steps | Fails on |
| --- | --- | --- |
| `backend` | `dotnet restore`; `dotnet build -warnaserror --configuration Release`; `dotnet test PMPlatform.Tests.Unit --no-build` | any analyzer or code-style diagnostic (`AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`), any compiler warning, any failing unit test — which includes the A-1 to A-6 architecture tests |
| `frontend` | `npm ci`; `npm run lint`; `npm run format:check`; `npm run typecheck`; `npm test`; `npm run build` | any ESLint error, any Prettier deviation, any type error (`tsc -b --noEmit`), any failing Vitest test, any Vite build failure |
| `repo-checks` | the four `python3 docs/architecture/*-check.py` scripts | contract, ERD, environment-template or local-stack drift (§3.2) |

Deliberately outside the line:

- **Integration tests.** `PMPlatform.Tests.Integration` needs the containerised stack, so `backend` names
  `PMPlatform.Tests.Unit` explicitly rather than the solution. The project holds no tests today, so nothing is lost
  now; when it holds tests they belong to a stage with services, not to a ten-minute gate. `dotnet build` still
  compiles it, so it cannot rot.
- **Coverage.** `branching-strategy.md` S-5 anticipated a coverage check here. There is no coverage policy to gate
  against — that is TASK-084 — and a threshold invented by the CI task is a number no one agreed to. §6 F-3.
- **Secret and dependency scanning.** CTL-42, TASK-080.

`timeout-minutes: 10` on each job is the acceptance criterion made mechanical: the gate cannot quietly grow past
its budget, it fails instead. NuGet packages are cached on the hash of `Directory.Packages.props`; npm packages on
`package-lock.json` via `setup-node`.

### 3.2 The four scripts had nowhere else to go

`erd-check.py` (TASK-008), `contract-check.py` (TASK-009), `env-template-check.py` (TASK-013) and
`local-stack-check.py` (TASK-014) each guard a document against drift, and each was run by hand. Three of those
records name TASK-015 as where they get wired in — `environment-templates.md` F-7, `local-development-environment.md`
F-7, and `contract-check.py`'s own docstring ("Standard library only, so it runs in the TASK-015 CI gate with no
extra toolchain").

They are in `repo-checks` rather than appended to `backend` or `frontend` because they belong to neither tier, and
because a job that needs only the runner's `python3` should not wait on an SDK it does not use. No `setup-python`
step: every script is standard library by construction, which was the reason that constraint was imposed.

### 3.3 The rename, and why `backend` and `frontend` kept their names

The workbook names `ci-quality-gates.yml`, and `ci.yml` was renamed to it (`git mv`, so the history follows).

GitHub binds a required status check to the **job name**, not to the file. `branching-strategy.md` §5 calls those
names load-bearing, and it is right: renaming a job does not fail the rule, it silently stops reporting it. So the
two existing jobs kept the names `backend` and `frontend` through the rename, and the ruleset gained exactly one
entry, `repo-checks`. The file rename is invisible to branch protection; a job rename would not have been.

`.github/branch-protection/apply.sh` is unchanged and idempotent — re-running it is what puts the fourth check in
effect (§6 F-1).

### 3.4 The frontend had no test runner

ADR-002 §4.1 places Vitest at TASK-015, and `monorepo-bootstrap.md` §5 S-7 carries it forward. "Unit tests for both
tiers" cannot be a gate on one tier, so Vitest is added here:

- `vitest`, `jsdom`, `@testing-library/react` and `@testing-library/dom`, pinned exactly like every other npm
  dependency; `npm test` = `vitest run`, `npm run test:watch` for development.
- Configured in `vite.config.ts` (imported from `vitest/config`) rather than a second config file, so the alias,
  `envPrefix` and plugin list cannot drift between the app and its tests. `include` is `src/**/*.test.{ts,tsx}`,
  which keeps `e2e/` for Playwright (TASK-085) and matches ADR-002 §6: tests are colocated with the feature.
- No `globals: true` and no `jest-dom`. Tests import `test`/`expect` from `vitest`, so the ESLint
  type-checked rules and `tsc` see them with no change to `tsconfig.app.json`'s `types`.
- `src/App.test.tsx` is one real test against the one component that exists. It is there so the runner is proven
  wired, not for coverage.

### 3.5 `pr-policy.sh` rejected this task's own branch

The first PR from this branch failed `pr-policy` on its name. The workbook's Branch column — which
`CONTRIBUTING.md` calls authoritative — gives TASK-015 `infra/task-015-ci-quality-gates`, and `branch_types` in
`.github/scripts/pr-policy.sh` (TASK-012) did not list `infra`. Nor `sec`, nor `release`. Across the 112 workbook
rows the column uses seven prefixes:

| Prefix | Rows | Was accepted |
| --- | --- | --- |
| `feat` | 57 | yes |
| `chore` | 14 | yes |
| `infra` | 11 | **no** — TASK-015 to TASK-018, 020 to 023, 090 to 092 |
| `sec` | 10 | **no** |
| `test` | 10 | yes |
| `docs` | 5 | yes |
| `release` | 5 | **no** |

26 of 112 tasks could not open a compliant PR. The check was wrong, not the branch, so the three prefixes were
added rather than the branch renamed — renaming would have put the repository at odds with the authoritative
column for every one of those tasks. The seven unused prefixes TASK-012 chose (`fix`, `refactor`, `ci`, `build`,
`perf`, `revert`, `hotfix`) are kept: they cover work that has no workbook row, which is exactly when a hotfix
branch is needed. `branching-strategy.md` §2 and `CONTRIBUTING.md` now carry the same list.

## 4. Verification

Run on the branch head, 2026-09-22. The backend ran on the local SDK (10.0.401, matching `global.json`); the
frontend ran in `node:24-alpine`, the image CI's `.nvmrc` resolves to; `actionlint` ran over all three workflow
files.

| # | Check | Result |
| --- | --- | --- |
| 1 | `actionlint` over `.github/workflows/` | exit 0, no findings |
| 2 | Backend clean: restore, `-warnaserror` build, unit tests | **0 warnings, 0 errors; 25 passed, 0 failed** |
| 3 | Frontend clean: `npm ci`, lint, format, typecheck, `npm test`, build | all exit 0; **1 test passed**; `npm ci` confirms `package-lock.json` is in step with `package.json` |
| 4 | `repo-checks` clean | contract 0 findings; ERD 121 tables; env templates 43/43; local stack 9 tables/124 columns — all exit 0 |
| 5 | **Lint error** — a file with `console.log` (banned by `no-console`) | `npm run lint` **exit 1** |
| 6 | **Type error** — `export const n: number = 'not a number'` | `npm run typecheck` **exit 2** |
| 7 | **Failing frontend test** — `expect(1).toBe(2)` | `npm test` **exit 1** |
| 8 | **Analyzer violation** — an unnecessary `using` in `PMPlatform.Domain` | build **exit 1**, `IDE0005` reported as an error |
| 9 | **Failing backend test** — `Assert.Equal(1, 2)` | `dotnet test` **exit 1** |
| 10 | Working tree after 5–9 | clean; every violation file removed |
| 11 | `pr-policy.sh` on this branch with a filled PR body (§3.5) | **exit 0** |
| 12 | `pr-policy.sh` still rejects `infrastructure/task-015-x`, and an unticked checklist box | **exit 1** each |
| 13 | `pr-policy.sh` accepts `sec/task-080-...` and `release/task-100-...` | exit 0 each |

Rows 5 to 9 are the workbook's validation note ("open a deliberately broken PR and confirm the pipeline fails red")
executed against the commands the workflow runs, one violation class at a time, which is stricter than one broken
PR. The round trip on GitHub itself is F-2.

**Run time.** The gate stage is far inside the ten-minute criterion and each job is capped at it. Locally, backend
restore+build+test is ~4 s; the full frontend chain from a clean `npm ci` in `node:24-alpine` is ~12 s. On a
cold runner the dominant costs are SDK setup, `npm ci` and the first restore, all of which are cached. §6 F-4 records what must be re-measured once the tree is real.

## 5. Acceptance criteria

| # | Criterion | Status |
| --- | --- | --- |
| 1 | CI fails a PR that introduces a lint error, a type error, or a failing unit test | **MET** — §4 rows 5–9, each violation class proven red against the workflow's own commands, both tiers |
| 2 | Passing the gate is a required branch-protection check on `main` | **MET ON FILE, PENDING APPLICATION** — the ruleset lists `backend`, `frontend` and `repo-checks` on `main`, `dev` and `stage` — `pr-policy` was removed from the required checks on 2026-09-22 (`branching-strategy.md` §5.1), which does not affect this task's three; it takes effect when an administrator runs `apply.sh` (F-1). Same shape as `monorepo-bootstrap.md` S-3 |
| 3 | Gate stage run time under 10 minutes on a representative PR | **MET, AND ENFORCED** — `timeout-minutes: 10` per job; measured times in §4 |

CTL-39 (cybersecurity control matrix) additionally names the contract check: `contract-check.py` runs in
`repo-checks` over the conventions sample and the event samples. It does **not** yet run over a document generated
from the built API — F-5.

## 6. Findings and open items

| # | Finding | Owner / trigger |
| --- | --- | --- |
| **F-1** | **The ruleset is not applied.** No GitHub credential exists in this environment (`gh auth status`: not logged in), so `repo-checks` is listed but not enforced, and neither are the three checks TASK-012 listed. One run of `.github/branch-protection/apply.sh` by a repository administrator closes this and `monorepo-bootstrap.md` S-3 together. | Repository administrator, before the next merge to `main` |
| **F-2** | **The broken-PR round trip has not been run on GitHub.** §4 proves the commands fail; it does not prove GitHub reports them as a failed check and blocks the merge button. Open one PR with a `console.log`, confirm red, remove it, confirm green. Needs F-1 first, or the check is reported but not required. | TASK-015 owner, immediately after F-1 |
| **F-3** | **No coverage gate.** Deliberate (§3.1): TASK-084 authors the coverage policy. Adding it is one `--collect:"XPlat Code Coverage"` flag plus a threshold, and the threshold is the part that needs authority. | TASK-084 |
| **F-4** | **Run time is measured on an empty tree.** Four backend projects, one React component. `npm ci` and the first `dotnet restore` dominate today and both are cached; the risk is the architecture tests and the Vite build as modules land. Re-measure at the end of W2; if a job approaches ten minutes, split `repo-checks` off the critical path rather than raising the cap. | DevOps/Platform Lead, end of W2 |
| **F-5** | **The contract check lints the sample document, not the generated one.** `api-conventions.md` §9 row 2 asks this gate to generate the OpenAPI document from the built API and lint that. Generation works (`Microsoft.Extensions.ApiDescription.Server` was trialled on this branch and emits `obj/PMPlatform.Api.json`), but the API has no controllers and no `ProblemDetails` schema, so `contract-check.py` C-6 fails the generated document on day one — the gate would block every merge over an absence the gate cannot fix. The trial was reverted rather than left as dead machinery. Wire it in with the first controller and the shared error envelope, in the same PR. | First Backend API task (W2), with `api-conventions.md` R-49 |
| **F-6** | **A-6 is still the one-directional form.** `monorepo-bootstrap.md` §3.3 and S-5 name TASK-015 as "the natural place to record that switch", not to make it: the equality form fails until the 21st module exists. Recorded here so the trigger is not lost — it is the last Backend wave, not this task. | TASK-015 owner ⇒ carried to the last Backend wave |

## 7. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-22 | Initial record. `ci.yml` renamed to `ci-quality-gates.yml` with `backend` and `frontend` extended and `repo-checks` added (§3.1, §3.2); `repo-checks` appended to the ruleset (§3.3); Vitest added to the frontend (§3.4); thirteen verification rows including five deliberate violations (§4); `branch_types` corrected in `pr-policy.sh` (§3.5); six findings (§6). | DevOps (TASK-015) |

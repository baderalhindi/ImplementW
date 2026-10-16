# Secret Scanning and Dependency Vulnerability Gate

| Field | Value |
| --- | --- |
| Task | TASK-080 — Add Secret Scanning & Dependency Vulnerability Gate to CI (P14 - Security Hardening & Compliance) |
| Depends on | TASK-019 (`secret-management.md`: `.gitleaks.toml`, the clean full-history scan, F-6) |
| Record date | 2026-10-16 |
| Status | **BUILT AND VERIFIED**: locally by the drill (§5.2, 15 of 15) and the guard's mutation run (§5.1, 27 of 27), and on GitHub by the workbook's check in a test PR (§5.3). **Enforced once the ruleset is applied** (F-2) |
| Branch | `sec/task-080-secret-and-dependency-scanning-gate` |
| Directory | `.github/workflows` |
| Deliverables | **`secret-scan.yml`** and **`dependency-vuln-gate.yml`**, each a per-change required check and a weekly scan reported to the named owner. Behind them: `.github/scripts/secret-scan.sh`, `dependency-vuln-gate.sh`, `report-to-owner.sh`, `verify-scanning-gates.sh` (the drill); `.github/security/scan-owner.yaml`; both checks in `.github/branch-protection/protected-branches.ruleset.json`; `docs/architecture/secret-and-dependency-scanning-check.py` in `repo-checks` |
| Environment variables / secrets | None (as the workbook row says). The weekly jobs use the workflow's own `GITHUB_TOKEN` with `issues: write` |
| Gate decision applied | None in the workbook row |
| Control | CTL-42; CTL-55 (the weekly scan during operations) (`cybersecurity-control-matrix.md`) |
| Sources read | The TASK-080 row as supplied on 2026-10-16, checked against the workbook (`AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`): description, acceptance criteria, deliverables, branch and the validation check. `secret-management.md` (§7, F-6), `artifact-build-and-scanning.md` (D-3, F-3, F-5), `cicd-pipeline.md`, `branching-strategy.md` (§3, S-1, S-4, S-5), `ci-quality-gates.md`, `environment-separation.md` G-2, CTL-42 and CTL-55 |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | Secrets in source: every commit of every pull request and push to `main`, `dev` and `stage`, and weekly every commit on every ref | This task |
| **In** | Dependency vulnerabilities a change adds: the backend's full NuGet graph and the frontend's npm graph, dev dependencies included, on every pull request and push | This task |
| **In** | A weekly dependency report on the default branch, and both weekly scans reported to a named owner | This task |
| **Out** | The release image, its OS packages and the SBOM gate at promotion: fixable HIGH and CRITICAL block a release whatever change brought them in | TASK-022 (`artifact-build-and-scanning.md`) |
| **Out** | Secrets in a running environment, rotation | TASK-019 (`secret-management.md`) |
| **Out** | Applying the ruleset and the second reviewer it needs | Repository administrator (`branching-strategy.md` S-1, S-4) |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| **D-1** | **gitleaks 8.30.1, the binary, checksum-verified.** `secret-scan.sh install` downloads the release archive and checks it against the release's published SHA-256 before it runs. No marketplace action | The scanner and version TASK-019 ran over the full history, so the gate starts from that clean baseline. `gitleaks-action` needs a paid licence for organisation repositories and is a third-party action holding the token; a checksum pins the binary harder than a tag pins an action (`artifact-build-and-scanning.md` F-3) |
| **D-2** | **A pull request is scanned commit by commit, not as a net diff.** The scope is `base..head`: every commit the change adds | A value added in one commit and deleted in the next is still served by GitHub from the pull request's commits. Clearing the check takes a rotation and a rewrite of the branch, and the summary says so |
| **D-3** | **A change cannot widen its own gate.** The pull-request scan reads `.gitleaks.toml` and `.gitleaksignore` from the base commit, ignores inline `gitleaks:allow`, and the dependency gate reads the base's `vulnerability-exceptions.yaml`. The route past a false positive or a finding that cannot be upgraded is a change of its own, reviewed by the Security Lead (CODEOWNERS), merged first | Otherwise the change that adds a secret can add its allowlist beside it and pass, and the review of a large diff is all that stands in the way. Cost: one extra small PR per false positive. History shows it: PR #87 added the ZAP reports (`b815ca7`) and their allowlist (`927c526`) in one PR; read with the base's rules, that range is blocked by 31 matches (§5.2) |
| **D-4** | **"New" is per vulnerability and package.** The gate scans base and head and blocks on a fixable CRITICAL whose (vulnerability, package) pair is in the head and not in the base. A change that touches no package manifest (`Directory.Packages.props`, `Directory.Build.props/.targets`, `*.csproj`, `nuget.config`, `packages.lock.json`, `package.json`, `package-lock.json`) cannot add a dependency and passes without a scan | The criterion is a *new* CRITICAL. A CVE published against a package already on `dev` would otherwise block every unrelated PR until someone fixes it; that finding goes to the weekly report and blocks the next release (TASK-022), where it belongs. Skipping manifest-free changes saves a Trivy database download on most PRs, the step most likely to fail for reasons that have nothing to do with the change |
| **D-5** | **Dev dependencies are scanned at the PR.** Trivy runs with `--include-dev-deps` on the frontend lock file; the backend graph includes the test projects' packages | Build and test tools run on the runner with the repository's token (`artifact-build-and-scanning.md` F-5). The release gate keeps scanning only what ships |
| **D-6** | **CRITICAL at the PR, HIGH and CRITICAL in the weekly report.** One scanner (Trivy v0.74.0, the release pipeline's) and one exceptions file | CRITICAL is the criterion. The weekly report uses the release gate's line (D-3 of TASK-022) over the same graphs as the PR gate, so it lists everything that will block the next promotion, plus fixable HIGH and CRITICAL in dev dependencies, which the release gate does not scan |
| **D-7** | **The weekly scans tell a named owner every run.** `report-to-owner.sh` keeps one issue per scan, assigned to the account in `.github/security/scan-owner.yaml`, and comments on it every run with the outcome, the redacted summary and a link to the run; a run with findings reopens it if it was closed. Mondays 03:17 UTC (secrets) and 03:47 UTC (dependencies), and on demand | "Reports to a named owner" holds only if a clean week is also reported; otherwise silence cannot be told apart from a scan that stopped running. The issue is also the weekly evidence CTL-55 asks for in the handover package. The owner is a reviewed file, not a repository variable, so a change of who is told about a leak has history and a reviewer |
| **D-8** | **Required checks are the job names `secret-scan` and `dependency-vuln-gate`; no path filters; the weekly jobs are not required.** | A required check a path filter skips never reports, and the PR waits forever; one a filter passes is a hole. The weekly jobs never run on a PR, so requiring them would block every merge |
| **D-9** | **Not a need of `deploy-dev`.** | Every commit on `main` arrives through a PR whose `secret-scan` is required, the push to `main` is scanned again, and dependencies are gated at promotion by TASK-022. `environment-separation.md` G-2's "dependency and secret scans clean" is met by those, not by a third copy |

## 3. As built

| Job | Runs on | Does | Fails on |
| --- | --- | --- | --- |
| `secret-scan` (required) | PR to and push on `main`, `dev`, `stage` | `secret-scan.sh range <base> <head>`: the commits the change adds; with no base (a new ref, or a force-push that removed the old head), every commit the head reaches | any finding (exit 1), or a scan that could not run (exit 2) |
| `secret-scan-history` | Mondays 03:17 UTC; on demand | `secret-scan.sh history`: every commit on every ref, with the default branch's `.gitleaks.toml`; then `report-to-owner.sh` | any finding |
| `dependency-vuln-gate` (required) | PR to and push on `main`, `dev`, `stage` | `dependency-vuln-gate.sh gate <base> <head>`: base and head (the merge commit) as worktrees, each restored with lock files and scanned | a fixable CRITICAL the head adds |
| `dependency-vuln-report` | Mondays 03:47 UTC; on demand | `dependency-vuln-gate.sh report` on the default branch (`dev`); then `report-to-owner.sh` | any fixable HIGH or CRITICAL |

Every job runs with `contents: read`; only the two weekly jobs add `issues: write`. Reports are attached to every run for 90 days; secret values are redacted in the log, the report, the summary and the owner's issue.

`secret-and-dependency-scanning-check.py` (in `repo-checks`) holds the shape in place:

| Check | Asserts |
| --- | --- |
| SD-1 | the workflows, scripts, owner file, scanner configuration and exceptions file exist; the scripts are executable |
| SD-2 | both workflows run on PRs to every branch the ruleset protects, with no `paths`/`paths-ignore`/`branches-ignore`; each has a schedule that runs at least weekly, and `workflow_dispatch` |
| SD-3 | each gate job reports under its own name, runs on PRs, is a required check, and clones the full history; no weekly job is required |
| SD-4 | the secret scan redacts, ignores inline allows, reads the base's configuration and ignore file, pins gitleaks by version and SHA-256; `.gitleaks.toml` extends the default rules and every allowlist names a rule, paths and a value shape under `condition = "AND"` |
| SD-5 | the dependency gate blocks on CRITICAL, ignores unfixed, includes dev dependencies, reads the base's exceptions from the release gate's file, restores lock files, and installs the release pipeline's Trivy version |
| SD-6 | the owner file names an account; each weekly job reports to it after a failed scan too, and holds `issues: write`; nothing else in either workflow can write |
| SD-7 | `.gitleaks.toml`, both workflows, the four scripts, the owner file, the exceptions file and the ruleset are owned by the Security Lead in CODEOWNERS |

## 4. Using the gates

- **`secret-scan` is red.** The value is on GitHub. Rotate it (`infra/secrets/secret-rotation-runbook.md`), rewrite the branch so no commit holds it, force-push. Deleting it in a new commit does not clear the check (D-2).
- **It is not a secret.** Open a separate PR adding a scoped `[[allowlists]]` entry to `.gitleaks.toml` (rule, paths, value shape, `condition = "AND"`); the Security Lead reviews it; once merged, re-run the first PR's check (D-3).
- **`dependency-vuln-gate` is red.** Take the fixed version in the summary. If that cannot be done now, a separate PR adds a dated exception to `.github/security/vulnerability-exceptions.yaml` (`cicd-pipeline-check.py` P-15 holds it to 90 days).
- **Run locally.** `.github/scripts/secret-scan.sh range origin/dev HEAD` (gitleaks 8.30.1 on PATH); `.github/scripts/dependency-vuln-gate.sh gate $(git merge-base origin/dev HEAD) HEAD` (dotnet, trivy, jq); `.github/scripts/verify-scanning-gates.sh` for the drill.

## 5. Verification

### 5.1 The guard, by mutation

`artifacts/task-080/mutations/run.py` (git-ignored) applies 27 edits, each the quiet way a gate is weakened — a branch dropped from the PR trigger, a `paths` filter, a monthly cron, a check removed from the ruleset, a renamed job, a shallow clone, `--redact` or `--ignore-gitleaks-allow` dropped, the change's own `.gitleaks.toml` or exceptions read, the checksum check removed, a global `[allowlist]`, an allowlist without `AND`, the default rules off, the gate on HIGH only, dev dependencies dropped, a Trivy version drift, the owner told only on success, an empty owner, `issues: write` removed, write granted to a gate job, `.gitleaks.toml` without a security owner — and runs the check after each. **27 of 27 fail it**; the unmutated tree passes before and after.

### 5.2 The drill, locally

`.github/scripts/verify-scanning-gates.sh` builds each case as commits on a scratch worktree and runs the gate commands the PR jobs run, on the same `base..head` ranges (Trivy v0.74.0 in its container, gitleaks 8.30.1). **15 of 15** (the 12 cases below, the two CVEs named, and the base's findings counted once each):

| Case | Expected | Result |
| --- | --- | --- |
| clean change | pass | exit 0 |
| + a fake API key (random, generated per run) | block | exit 1 |
| + the key marked `// gitleaks:allow` | block | exit 1 |
| + the key and an allowlist for it in one change | block | exit 1 |
| + the key, then a commit deleting it | block | exit 1 |
| the change rewritten without it | pass | exit 0 |
| + the key, its allowlist merged first on its own | pass | exit 0 |
| + log4net 2.0.9 and lodash 4.17.11 (dev dependency) | block, naming CVE-2018-1285 and CVE-2019-10744 | exit 1, both named |
| + both and an exception for each in one change | block | exit 1 |
| both already in the base, an unrelated manifest edit | pass | exit 0 |
| both removed by rewriting the change | pass | exit 0 |
| + both, their exceptions merged first on their own | pass | exit 0 |

Also run: the full history with the CI configuration — 286 commits on every ref, **no leaks found**; and `range` over `9ab19b3..7403ace` of real history (PRs #85 to #89, 22 commits), which **blocks with 31 `generic-api-key` matches** in the TASK-077 ZAP reports, because their allowlist came in the same range (D-3).

### 5.3 The workbook's check, on GitHub

Draft PR #91 (`sec/task-080-validation-fake-credential` → `dev`, closed unmerged), on top of this branch:

| Push | Head | Check | Result | Run |
| --- | --- | --- | --- | --- |
| 1. + a fake API key in `DrillCredential.cs` (random, labelled fake), log4net 2.0.9, lodash 4.17.11 as a dev dependency | `62402c5` | `secret-scan` | **fail**: gitleaks 8.30.1 installed after `gitleaks.tar.gz: OK`; 4 commits scanned; 1 finding, `generic-api-key` at `DrillCredential.cs:6`, value redacted | 38917491044 |
| | | `dependency-vuln-gate` | **fail**: 2 new fixable CRITICAL — CVE-2018-1285 (log4net 2.0.9 → 2.0.10), CVE-2019-10744 (lodash 4.17.11 → 4.17.12) | 38917491103 |
| 2. the branch rewritten without that commit, force-pushed | `c5f7b34` | `secret-scan` | **pass** | 38917968269 |
| | | `dependency-vuln-gate` | **pass** (no manifest changed against `dev`, D-4) | 38917968264 |
| 3. one commit letting the weekly jobs also run on `pull_request`, since a schedule runs only on the default branch | `0af8ff8` | `secret-scan-history` | **pass**, 206 commits on every ref, no secret; reported to the owner: issue **#92** created, assigned to `baderalhindi`, "Clean" | 38919427880 |
| | | `dependency-vuln-report` | **fail**, one real finding on `dev` (F-8); reported to the owner: issue **#93** created, assigned to `baderalhindi`, "Findings — action needed" | 38919427820 |

Push 1's run also showed a defect, fixed before push 2: the summary said the base "already carries 4" findings when it carried none, because a package is listed once per lock file that resolves it. The count is now of distinct findings, and the drill asserts it (§5.2, now 15 of 15).

On this PR (#90) at `c5f7b34`, every check is green: `secret-scan`, `dependency-vuln-gate`, `backend`, `backend-risk-governance`, `backend-endpoint-hardening`, `frontend`, `repo-checks` (with the new guard), `terraform`.

## 6. Acceptance criteria and validation check

| Criterion | Verdict |
| --- | --- |
| A PR introducing a hardcoded credential-looking string is blocked from merge by the secret scanner | **MET as a failing required check** (§5.3 push 1; §5.2 for the ways around it). The merge button is held only once the ruleset is applied (F-2) |
| A scheduled full-history scan runs at least weekly and reports to a named owner | **MET.** Mondays 03:17 UTC (SD-2); the report path ran on GitHub and created issue #92 assigned to the owner (§5.3 push 3). The first scheduled run is the Monday after merge, when the workflow reaches the default branch |
| The gate is a required branch-protection check | **MET in the ruleset file; ENFORCED once it is applied.** Both checks are in `required_status_checks` (SD-3 holds them there), but no ruleset is applied to the repository yet (F-2) |
| Validation: fake credential in a test PR blocked; removed, unblocked | **EXECUTED.** PR #91: blocked at `62402c5`, unblocked at `c5f7b34` (§5.3) |
| Deliverables: `secret-scan.yml`, `dependency-vuln-gate.yml` pipeline stages | **MET.** §3 |

## 7. Findings

| # | Finding | Owner |
| --- | --- | --- |
| **F-1** | **The named owner is the repository owner until AHDA names a Security Lead.** `.github/security/scan-owner.yaml` names `baderalhindi`. When the Security Lead is appointed, change the file (SD-6 requires an account; the account must have access to the repository) | Engagement Lead; AHDA Cybersecurity |
| **F-2** | **No ruleset is applied, so no check is enforced.** `protected-branches.ruleset.json` requires both checks, as it requires the six before them, but `apply.sh` has not been run (`branching-strategy.md` S-1), and with one maintainer the review rule in the same ruleset blocks every PR (S-4). Until then a red `secret-scan` is visible, not blocking | Repository administrator |
| **F-3** | **No bot opens dependency updates.** `branching-strategy.md` named this task as the owner of the scanner that opens `dependabot/**` branches. The gate detects; it does not propose the fix. Recommended: `.github/dependabot.yml` for `nuget`, `npm` and `github-actions`, weekly, which would also move the tag-pinned actions to SHAs (`artifact-build-and-scanning.md` F-3). Not enabled here: it opens PRs on its own, which is the repository owner's call | Repository owner; DevOps Lead |
| **F-4** | **A fixable HIGH can merge.** The PR gate blocks CRITICAL (the criterion); the release gate blocks HIGH too. A PR adding a fixable HIGH merges, the Monday report lists it, and the next promotion stops on it. Raising the PR line to HIGH is one word in `dependency-vuln-gate.sh` | Security Lead, noted |
| **F-5** | **The weekly dependency report covers the default branch (`dev`) only.** `main` lags `dev` (`branching-strategy.md` S-3) and is gated at every promotion. The secret scan covers every ref | Noted; closes with S-3 |
| **F-6** | **The owner's issue is public.** The repository is public, so the weekly issue is too. It holds rule names, paths, line numbers and short commit ids, never a value (redacted at the scanner). A leaked value is already public in the repository; the issue adds nothing an attacker could not read from the commit | Security Lead, noted |
| **F-7** | **GitHub's own secret scanning and push protection are on** (provider patterns, at push time). They complement this gate rather than replace it: they reject a known provider's token before it lands, and do not match generic keys, which is what gitleaks' `generic-api-key` catches | Noted |
| **F-8** | **`dev` carries a fixable HIGH in a dev dependency.** The first report (issue #93): `source-map-js` 1.2.1, CVE-2026-93749, fixed in 1.2.2, pulled in by `postcss` and `css-tree`. Not CRITICAL and not shipped, so neither gate blocks on it; a lock-file update to 1.2.2 closes it | Frontend lead |

## 8. Change log

| Date | Change |
| --- | --- |
| 2026-10-16 | Initial record. `secret-scan.yml` and `dependency-vuln-gate.yml`, both required checks in the ruleset file; the base's allowlists and exceptions; weekly scans reported every run to the named owner; the guard in `repo-checks` (27 of 27 mutants); the drill (15 of 15); the workbook's check on GitHub in PR #91 (§5.3), with the owner's issues #92 and #93. F-6 of `secret-management.md` and F-5 of `artifact-build-and-scanning.md` closed |

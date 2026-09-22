# Branching Strategy, Pull Request Policy and Code Ownership

| Field | Value |
| --- | --- |
| Task | TASK-012 — Define Branching Strategy, PR Template & Code Owners (P2 - Project Setup & Foundation) |
| Depends on | TASK-011 — Initialize Monorepo & Solution Structure (`docs/architecture/monorepo-bootstrap.md`, BUILT — PROVISIONAL) |
| Record date | 2026-09-21 |
| Status | **FILES BUILT — PROTECTION NOT APPLIED.** The template, CODEOWNERS, policy check and ruleset are on the task branch and verified locally; the ruleset is a repository setting that this environment cannot apply (§7 S-1), and the CODEOWNERS teams do not exist yet (§7 S-2) |
| Branch | `chore/task-012-branching-strategy` |
| Deliverables | `.github/PULL_REQUEST_TEMPLATE.md`; `.github/CODEOWNERS`; `.github/branch-protection/protected-branches.ruleset.json` and `apply.sh`; `.github/workflows/pr-policy.yml` with `.github/scripts/pr-policy.sh`; this record |
| Implements | CTL-38 (`cybersecurity-control-matrix.md` §4 CF-7); the branch-naming row of ADR-002 §6.4 |
| Controlled sources and authority order | per TASK-001 |

## 1. What this record is

TASK-012 fixes how change reaches `main`: the branch model, what a pull request must carry, who must review which directory, and the protection rules that make all of it mandatory rather than customary. The workbook's Branch column stays authoritative for branch names; this record says what the column's values must look like and what happens to a PR that does not conform.

## 2. Branch model

**Trunk-based development. `main` is the trunk.** Every change is a short-lived branch off `main`, merged back by pull request, and deleted on merge. Nothing is released from any other branch.

| Rule | Value |
| --- | --- |
| Branch name | `type/task-id-short-description` — `type` ∈ {`feat`, `fix`, `chore`, `infra`, `sec`, `docs`, `test`, `release`, `refactor`, `ci`, `build`, `perf`, `revert`, `hotfix`} — the first seven are the ones the workbook's Branch column uses; the rest cover work that has no workbook row; `task-id` is `task-nnn`; the description is lowercase kebab-case. Examples: `chore/task-011-repo-bootstrap`, `feat/task-046-schedule-baseline` |
| One task per branch | A branch carries one Task ID. A task that needs several PRs uses several branches with the same `task-nnn` and different descriptions |
| Lifetime | Days, not weeks. A branch that falls behind `main` is rebased or merged forward by its author; the ruleset's `strict_required_status_checks_policy` refuses to merge a branch whose checks ran against an older `main` |
| Merge | Squash by default, so that `main` carries one commit per PR with the Task ID in its subject. A merge commit is acceptable when the branch's individual commits are meaningful on their own |
| Direct push to a protected branch | Rejected, for every account (no bypass actors, §5) |
| Hotfix | `hotfix/task-nnn-…` off `main`, same PR rules. There is no release branch to backport to |
| Automated dependency branches | `dependabot/**` and `renovate/**` are exempt from the naming rule only; every other rule applies (TASK-080 owns the scanner that opens them) |

### 2.1 The existing `dev` and `stage` branches

At the record date the repository has three long-lived branches. `main` holds one commit; all twelve PRs to date were merged into `dev`, which is 25 commits ahead of `main`; `stage` equals `main`. None of the three has any protection, and `.github/workflows/ci.yml` as built by TASK-011 ran only on PRs to `main` — so no PR merged so far has run CI.

That is a two-branch environment ladder (`dev` → `stage` → `main`), which is not trunk-based development. Trunk-based development promotes a *built artifact* through environments (DEV → SIT → UAT → PROD), not a branch; TASK-018 builds that pipeline and CTL-40 governs it. The decision here is:

1. `main` is the trunk from the moment this record is merged; new task branches are cut from `main` and target `main`.
2. `dev` and `stage` are **not deleted by this task** — they carry the merged TASK-001 to TASK-011 work that `main` does not yet have (§7 S-3). Until they are retired they receive the **same protection as `main`** (the ruleset targets all three) and CI runs on PRs to them, so that nothing else lands unprotected.
3. TASK-018 retires them: once artifact promotion exists, `dev` and `stage` are fast-forwarded into `main` (or `main` reset to `dev`, which is a superset) and deleted. Their protection rule is then a no-op on absent refs.

## 3. Pull request policy

`.github/PULL_REQUEST_TEMPLATE.md` pre-fills every PR with four mandatory sections — **Task**, **Description**, **Test evidence**, **Security checklist** — and one optional (**Notes for reviewers**).

A template cannot enforce anything on its own, so `.github/workflows/pr-policy.yml` runs `.github/scripts/pr-policy.sh` on every PR open, edit, push, reopen and ready-for-review. It reports red, and **as of 2026-09-22 it does not block the merge** — `pr-policy` was removed from the required status checks (§5). The check reports a failure unless:

| # | Check | Why |
| --- | --- | --- |
| 1 | Head branch matches the naming rule of §2 (dependency bots exempt) | The Branch column is authoritative; a branch that cannot be traced to a task cannot be traced to a workbook row |
| 2 | Title or body names a `TASK-nnn` | Linked Task ID (acceptance criterion; CTL-40 traceability of every deployment to a Task ID starts here) |
| 3 | `## Test evidence` has at least one line that is not blank, not a code fence and not an unfilled `… ->` template line | Test evidence (acceptance criterion) |
| 4 | `## Security checklist` exists and has no unticked box | Security checklist (acceptance criterion) |

HTML comments are stripped before the checks run, so the template's own guidance never satisfies them. PR text reaches the script only through environment variables, never interpolated into shell. The Task ID match is case-insensitive (`task-012` in a title passes); GitHub's auto-generated title from a branch name (`Chore/task 012 …`) does not, and is not meant to.

**Bootstrap.** GitHub pre-fills a PR from `PULL_REQUEST_TEMPLATE.md` only once the file exists on the repository's default branch, while `pr-policy` runs from the PR's own head. A PR opened before the template has merged — including the PR that introduces it — therefore arrives with an empty description and fails with one error telling the author to paste the template in by hand. After the first merge the template is pre-filled.

**The security checklist.** Seven items, each derived from a control the matrix already assigns and each worded so that it is either satisfied or does not apply to the change, which is why every box must be ticked rather than "ticked where relevant":

| Item | Control |
| --- | --- |
| No secret, credential, connection string or `.env` committed | CTL-18, Section 22.1 F-3 |
| Server-side authorization and DTO validation on every new/changed endpoint | CTL-08, CTL-11 |
| No password, token, national ID or classified field in logs, error reports or fixtures | CTL-27 |
| New dependencies pinned and free of known CRITICAL vulnerabilities | CTL-41, CTL-42 (self-attestation until TASK-080's scanner is a required check) |
| Migrations under `Persistence/Migrations`, no forbidden column, cross-schema writes reviewed | A-5; ERD D-3, D-5, D-16; `monorepo-bootstrap.md` §5 row 4 |
| No cross-module reference outside `Contracts/`; ADR-003 §8.2 revised before a new edge | A-1, A-6 |
| Security Lead review requested on authentication, SSO, RBAC, data scope, upload, WF-13, Nafath | CTL-43 |

### 3.1 Verification of the check

`pr-policy.sh` was run locally against eleven cases built from the template itself:

| Case | Result |
| --- | --- |
| Template filled in: Task ID, one evidence line, all boxes ticked | ok |
| Template submitted untouched | 4 failures (no Task ID, empty evidence, checklist unticked — reported twice, as "no ticked item" and "unticked item present") |
| One box left unticked | 1 failure |
| Boxes ticked with uppercase `[X]` and indented | ok |
| Branch `feature/my-branch` | 1 failure (naming) |
| Branch `chore/task-005-task-005-hosting-adr` (existing style, doubled task id in the description) | ok — conforms, if untidy |
| Task ID absent from title and body | 1 failure |
| Branch `dependabot/npm_and_yarn/…` | ok (naming exempt) |
| Empty or whitespace-only body | 1 failure ("description is empty — paste the template"), plus the Task ID failure if the title lacks one; the section checks are skipped as redundant |
| Body with the checklist section deleted | 1 failure |
| Task ID only in the title, lowercase (`task-012`) | ok |

## 4. Code ownership

`.github/CODEOWNERS` maps every top-level directory — and every security- or data-relevant path below one — to a reviewer group. GitHub applies the *last* matching pattern, so the file lists a default, then each tier, then the paths within a tier that need a second group.

| Group | Role (as named in the records) | Owns |
| --- | --- | --- |
| `@baderalhindi/architecture` | Engagement Architect | default for unmatched paths; `docs/`; `CONTRIBUTING.md`, `README.md`; `Tests.Unit/Architecture/` (A-1 to A-6); `CODEOWNERS` and the PR template (with security) |
| `@baderalhindi/backend` | Backend lead | `src/backend/`; `.editorconfig` (with frontend); `global.json` (with devops) |
| `@baderalhindi/frontend` | Frontend lead | `src/frontend/`; `.nvmrc` (with devops) |
| `@baderalhindi/database` | Database lead | `db/`; `Persistence/Migrations/` (with backend); `infra/terraform/database/` (with devops) |
| `@baderalhindi/devops` | DevOps/Platform lead | `.github/`; `infra/`; `.vscode/`, `.gitignore`, `.gitattributes` |
| `@baderalhindi/security` | Security Lead | second owner on `.github/workflows/`, `.github/branch-protection/`, `infra/secrets/`, `Infrastructure/Identity/`, `cybersecurity-control-matrix.md`, `CODEOWNERS`, the PR template |
| `@baderalhindi/qa` | QA Lead | second owner on `Tests.Integration/` and `frontend/e2e/` |

The 21 module folders under `Application/Features/`, `Domain/`, `Infrastructure/Persistence/` and `Api/Controllers/` are owned by the tier group. Per-module ownership is added when a module has a named owner other than the tier lead; the pattern is one line per folder, placed after the tier line.

**The groups must exist.** CODEOWNERS resolves an owner only if it is a user with write access or a team that is visible and has write access. The repository is currently hosted under a personal account (`baderalhindi/ImplementW`), and teams exist only in organisations, so at the record date **no line in the file resolves**. Consequences and the fix are S-2 in §7. The one-approval rule of §5 does not depend on CODEOWNERS and holds regardless.

## 5. Branch protection

`.github/branch-protection/protected-branches.ruleset.json` is a GitHub **repository ruleset** (Settings → Rules → Rulesets → Import, or `apply.sh` with `gh`). A ruleset rather than a classic branch-protection rule because it is a single JSON document that can be versioned here, imported unchanged, and applied to several branches at once.

| Rule | Setting | Acceptance criterion / control |
| --- | --- | --- |
| Targets | `main`, `dev`, `stage` (§2.1) | "on main" |
| `deletion` | branch cannot be deleted | — |
| `non_fast_forward` | no force-push | `monorepo-bootstrap.md` S-3 |
| `pull_request` | required; **1 approving review**; stale reviews dismissed on push; code-owner review required; every review thread resolved | "at least 1 approving review" |
| `required_status_checks` | **`backend`, `frontend`, `repo-checks`**; strict (branch must be current with the target). `pr-policy` was removed on 2026-09-22 — it still runs and still reports, it no longer blocks (§5.1) | "passing CI"; CTL-39 |
| `bypass_actors` | none — administrators included | CTL-38 verification: direct push rejected |
| `enforcement` | `active` | — |

The check names are the `jobs.<id>.name` values in `ci-quality-gates.yml`. Renaming a job breaks the rule silently (the check is simply never reported), so the names are load-bearing. TASK-015 renamed `ci.yml` to `ci-quality-gates.yml`, kept `backend` and `frontend` so this rule did not move under it, and appended `repo-checks` (`docs/architecture/ci-quality-gates.md` §3.3).

**No bypass actors** means an emergency merge requires editing the ruleset, which is itself an audited repository event. That is the intended cost.

### 5.1 `pr-policy` is advisory

`pr-policy` was removed from `required_status_checks` on 2026-09-22 at the repository owner's instruction. The
workflow, the script and the PR template are unchanged: a PR missing its Task ID, its test evidence or a ticked
security-checklist box still goes red, and the failure is still visible on the PR — it just no longer prevents the
merge.

What that costs is precise. **CTL-38's "PR template enforcing linked Task ID, test evidence and a security
checklist" is now enforced by review rather than by the gate**, as is TASK-012 acceptance criterion 1. The
code-owner review requirement and the three CI checks are untouched, so an unreviewed or failing-CI change still
cannot merge. Restoring the gate is one line in this ruleset and one `apply.sh` run.

**Not set, deliberately:** `required_linear_history` (a merge commit remains acceptable, §2), `required_signatures` (no key-management decision exists; a candidate for TASK-022 with artifact signing, CTL-41), `required_deployments` (TASK-018).

### 5.1 Applying it

```sh
gh auth login            # as a repository administrator
.github/branch-protection/apply.sh            # current repo, or: apply.sh owner/repo
```

The script creates the ruleset or updates the one with the same name, then prints the rule types in effect on `main`. Expected: `deletion`, `non_fast_forward`, `pull_request`, `required_status_checks`. Verification per CTL-38: a direct `git push origin main` from any account is refused with `GH013`.

## 6. Acceptance-criteria check

| # | Criterion | Result |
| --- | --- | --- |
| 1 | PR template enforces linked Task ID, description, test evidence and security checklist | **MET** — the template carries all four sections; `pr-policy` fails a PR missing the Task ID, the evidence or a ticked checklist (§3, verified §3.1). "Description" is present in the template but not machine-checked: a check for prose that says something is not a check worth having |
| 2 | ≥1 approving review and passing CI are required branch-protection rules on `main` | **PARTIAL** — the rules are fully specified in the ruleset (§5) and the checks they require exist and run; the ruleset is a repository setting this environment cannot apply (no GitHub credential). Reads MET when S-1 is done |
| 3 | A CODEOWNERS file exists covering every top-level module directory | **MET on file, PARTIAL in effect** — every top-level directory (`.github`, `.vscode`, `db`, `docs`, `infra`, `src/backend`, `src/frontend`) and every root file has an owner, with a `*` default for anything added later; the teams do not yet exist (S-2), so GitHub cannot request them |

## 7. Residual items

| # | Item | Owner | Owed by | Consequence if unresolved |
| --- | --- | --- | --- | --- |
| S-1 | **Apply the ruleset** (§5.1). Supersedes `monorepo-bootstrap.md` S-3, which specified the same rules for `main` without the review requirement | Repository administrator | Before the next PR is merged | Criterion 2 reads PARTIAL; a red CI or an unreviewed PR still merges |
| S-2 | **Create the seven teams** of §4 in the organisation that hosts the repository, each with write access, and move the repository into that organisation if it stays under a personal account. Then replace the `baderalhindi/` namespace in `CODEOWNERS` if the organisation name differs | Repository administrator + Engagement Lead | With S-1 | No owner resolves; `require_code_owner_review` requests nobody, so the only reviewer requirement in force is "one approval from anyone with write access" |
| S-3 | **Bring `main` up to `dev`** (`main` is a strict ancestor; a fast-forward suffices) so that task branches can be cut from the trunk as §2 requires, and **retire `dev` and `stage`** once TASK-018's artifact promotion exists (§2.1) | Repository administrator (fast-forward); TASK-018 owner (retirement) | Fast-forward with S-1; retirement at TASK-018 | Until the fast-forward, a branch cut from `main` lacks the entire skeleton; until retirement, three protected branches carry three copies of the same rules |
| S-4 | **Single maintainer**: with one account holding write access, "1 approving review" cannot be satisfied — GitHub does not count the author's own approval. A second reviewer with write access is needed before S-1 is applied, or every PR blocks | Engagement Lead | With S-1 | Either the rule is applied and nothing merges, or it is not applied and criterion 2 stays PARTIAL |
| S-5 | **TASK-015 — DONE for the contract check and the frontend unit tests** (appended `repo-checks`; Vitest runs inside `frontend`). Coverage was **not** appended: no coverage policy exists to gate against until TASK-084 (`ci-quality-gates.md` §6 F-3). **TASK-080** appends the secret-scanning and dependency checks (CTL-42) and, once they are required, the dependency item of the security checklist changes from self-attestation to a pointer at the check | TASK-084, TASK-080 owners | Per task | The ruleset requires only the four listed checks; a later gate is advisory until listed |
| S-6 | **Per-module CODEOWNERS lines** once modules have named owners (§4) | Backend and Frontend leads | First module with an owner other than the tier lead | Tier lead reviews everything in the tier |

## 8. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-22 | `pr-policy` removed from `required_status_checks` at the repository owner's instruction (§5.1). The workflow and script are unchanged and still report; the check is advisory. CTL-38's PR-content enforcement falls back to code-owner review. | Repository owner |
| 2026-09-22 | TASK-015: `branch_types` in `pr-policy.sh` corrected to accept `infra`, `sec` and `release` (§2) — the workbook's authoritative Branch column uses all three, so 26 of 112 tasks could not open a compliant PR. `ci.yml` renamed to `ci-quality-gates.yml`; `repo-checks` appended to `required_status_checks` (§5); S-5 closed for the contract check and the frontend unit tests (§7). | DevOps (TASK-015) |
| 2026-09-21 | Initial record. Trunk-based model on `main` with `type/task-nnn-description` branches (§2); `dev`/`stage` kept, protected identically, and scheduled for retirement at TASK-018 (§2.1). PR template with Task, Description, Test evidence and a seven-item Security checklist derived from CTL-08/11/18/27/41/42/43 and A-1/A-5/A-6; `pr-policy` required check enforcing it, verified against eleven cases (§3). CODEOWNERS with seven reviewer groups covering every top-level directory (§4). Repository ruleset requiring 1 approval, code-owner review, thread resolution, `backend`/`frontend`/`pr-policy` checks, no force-push, no bypass, with `apply.sh` (§5). `ci.yml` triggers extended to `dev` and `stage`. Six residual items (§7). | Architecture (TASK-012) |

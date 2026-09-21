<!--
Branch: type/task-id-short-description (e.g. feat/task-046-schedule-baseline). The workbook's Branch column is authoritative.
The `pr-policy` check fails this PR until: the title or body names a TASK-nnn, "Test evidence" has content,
and every box under "Security checklist" is ticked. See docs/architecture/branching-strategy.md.
-->

## Task

TASK-

<!-- One task per PR. If the PR closes an open item of a record (e.g. "S-3 of monorepo-bootstrap.md"), name it here too. -->

## Description

<!-- What changed and why. Name the record or ADR section this implements. If an ADR was revised to permit it (e.g. a new ADR-003 §8.2 edge), link the revision. -->

## Test evidence

<!-- Commands run and their results. Paste the summary lines, not the full log. -->

```
dotnet build src/backend -warnaserror   ->
dotnet test src/backend                 ->
npm run lint && npm run typecheck       ->
```

<!-- For behaviour changes: the test that would have failed before this change. For docs-only PRs: "n/a — no code". -->

## Security checklist

<!-- Every box must be ticked. Each item is written so that it is either satisfied or does not apply to this change. -->

- [ ] No secret, credential, connection string, token or `.env` file is committed; `infra/secrets/` holds templates only (CTL-18, F-3).
- [ ] Every new or changed API endpoint has server-side authorization and server-side DTO validation, or this PR adds no endpoint (CTL-08, CTL-11).
- [ ] No password, token, national ID or classified field reaches a log, error report or test fixture, or this PR touches no logging path (CTL-27).
- [ ] Every new dependency (NuGet or npm) is pinned in `Directory.Packages.props` / `package-lock.json` and has no known CRITICAL vulnerability, or this PR adds no dependency (CTL-41, CTL-42).
- [ ] Every migration lives under `Infrastructure/Persistence/Migrations`, adds no `deleted_at`, `currency_code` or `row_version` column, and has been reviewed for cross-schema writes, or this PR adds no migration (A-5, ERD D-3/D-5/D-16).
- [ ] No cross-module reference outside `Contracts/` and no edge missing from ADR-003 §8.2; the architecture tests (A-1 to A-6) pass unchanged, or ADR-003 was revised first and the revision is linked above.
- [ ] Any change to authentication, SSO, RBAC, data scope, document upload, WF-13 or Nafath has a Security Lead review requested (CTL-43), or this PR touches none of these.

## Notes for reviewers

<!-- Optional: rollout steps, follow-ups owed, anything a reviewer should look at first. -->

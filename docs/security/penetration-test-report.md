# Penetration Test Report Register

| Field | Value |
| --- | --- |
| Task | TASK-082 |
| Purpose | The register of each report the independent provider issues, and the report's finding list as issued. The report itself is not here (`penetration-test.md` §4.4) |
| Who edits it | The Security Lead, in a PR, on receiving a report. Each finding is copied with the provider's severity unchanged (§5.2) |
| Check | `python3 docs/security/penetration-test-check.py [--gate [--verify-prs]]` |

## 1. Reports

One block per report (`PTR-n`). A draft report may be registered as `Draft` so the fixes can start, but the gate counts only a `Final` one. A `Final` block needs every field below filled in:

- `Tested commit`: 40 hex characters.
- `Report SHA-256`: 64 hex characters, the hash of the signed copy in the controlled store.
- `Areas covered`: names at least A, B, C and D of `penetration-test.md` §3.2.

### PTR-1

| Field | Value |
| --- | --- |
| Status | Not received |
| Provider | — |
| Independence | — |
| Environment | — |
| Tested commit | — |
| Image digest | — |
| Test window | — |
| Areas covered | — |
| Report reference | — |
| Report SHA-256 | — |
| Received | — |

## 2. Findings as issued

One row per finding of every registered report, numbered `PT-nn` in the order received; the provider's own id goes in `Provider ref`. Until a finding is closed by retest, its title names a category and an area only (§4.4).

| ID | Report | Provider ref | Severity | CVSS | Area | Title |
| --- | --- | --- | --- | --- | --- | --- |

## 3. Change log

| Date | Change |
| --- | --- |
| 2026-10-16 | Register created (TASK-082); no report received |

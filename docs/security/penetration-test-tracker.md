# Penetration Test Remediation Tracker and Retest Evidence

| Field | Value |
| --- | --- |
| Task | TASK-082 |
| Purpose | What happens to each finding in `penetration-test-report.md` §2 (the remediation action plan CS-024 asks for), and the provider's retest results that close them |
| Rules | `penetration-test.md` §5.3 (retest), §6 (handling), §7 (the gate) |
| Check | `python3 docs/security/penetration-test-check.py [--gate [--verify-prs]]`; run it for the current counts |

## 1. Remediation tracker

One row for each finding in the report register, with the same `ID` and `Severity`.

| Column | Value |
| --- | --- |
| Owner, Due | Set by the Security Lead (CS-023: by severity) |
| Disposition | `Open`; `Remediated` (a fix is merged); `Risk accepted` (High or lower only); `Withdrawn` (the provider withdrew it in a retest) |
| Fix | For `Remediated`, the merged PR(s) as `PR #n`; for `Risk accepted`, the `RA-nn` in `risk-acceptances.md`; otherwise `—` |
| Retest | The latest retest round that names the finding and its result, e.g. `RT-1 Closed`; `—` before any |
| Log | The `SR-nn` this finding adds to or reopens in `findings-log.md` (§6), or `—` |

| ID | Severity | Owner | Due | Disposition | Fix | Retest | Log |
| --- | --- | --- | --- | --- | --- | --- | --- |

## 2. Retest evidence

One block per retest round (`RT-n`), issued by the provider against a build deployed to UAT. The retest report stays in the controlled store, as the report does. A round lists every finding it retested with one of these results: `Closed`, `Not closed` or `Withdrawn`. A finding's latest result is the one in its highest-numbered round.

No retest has taken place.

<!--
### RT-1

| Field | Value |
| --- | --- |
| Report | PTR-1 |
| Provider | |
| Retest date | |
| Tested commit | |
| Retest reference | |
| Retest SHA-256 | |

| ID | Result | Provider's note |
| --- | --- | --- |
| PT-01 | Closed | |
-->

## 3. Change log

| Date | Change |
| --- | --- |
| 2026-10-16 | Tracker created (TASK-082); no findings |

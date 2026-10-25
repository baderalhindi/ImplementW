# Home and Portfolio

| Field | Value |
| --- | --- |
| Source of the change | The prototype review of 24 Oct 2026: high-priority gap H2 ("Home and Portfolio need distinct jobs"), finding 4 ("Home and Portfolio are the same"), M-1 (the administrator's dashboard shows business data), M-4 (specification codes shown to end users) and M-5 (widgets the user cannot see are still drawn). No workbook row names it: branch `feat/review-home-and-portfolio`, stacked on `feat/review-name-lookups` |
| Record date | 2026-10-25 |
| Status | **BUILT AND VERIFIED LOCALLY** |
| Supersedes | `dashboards-ui.md` D-1 ("no dashboard has a route; the Home hosts the dashboards") |

## 1. Decisions

| # | Decision | Basis |
| --- | --- | --- |
| D-1 | **The Home (`/`) is the person's own work**, each card offered to a holder of the permission its list reads under: approvals waiting for their decision with due dates (`APPROVAL_DECIDE`), their open tasks with what is overdue and due within a week (`TASK_UPDATE`), update requests waiting for their answer (`EXTERNAL_CONTRIBUTION_RESPOND`), their requests in approval (`APPROVAL_VIEW`), unread notifications (everyone), and what they may start or open (register a project, raise a change request, the register, the Portfolio Dashboard for its audience, the dashboards). A card whose read fails says so in a line and keeps the page | Review H2: "Home = my work, for every role. Approvals waiting for me (with SLA), my overdue updates, tasks and milestones due this week, requests I submitted, unread notifications, quick actions I'm allowed to take" |
| D-2 | **The dashboards are at `/dashboards`**, unchanged otherwise: the landing dashboard by role, the others one link away, DSH-009 still only in SCR-040. The Portfolio menu's Portfolio Dashboard is `/dashboards?dashboard=PORTFOLIO`; "Dashboards & Reports" lists Dashboards for an internal holder of `PROJECT_VIEW` | Review H2: "Portfolio = analysis, only for R02, R03 (department-locked), R06 and R07". ADR-006 fixes the dashboards at three, so the Home is not a fourth: it is a page of lists the person already reads elsewhere |
| D-3 | **The administrator's Home is the platform's health**: enabled integrations down, with stale data and open alerts (`INTEGRATION_VIEW`); failing and waiting audit messages and the Activity projection's lag (`AUDIT_OPERATIONS_MANAGE`); active internal and external people and disabled accounts (`USER_VIEW`); and the administration they may start. Shown to someone who holds an administration read and no business data | Review M-1. Configuration drafts awaiting publication, expiring access and the last data import wait for screens that read them (F-1) |
| D-4 | **A widget restricted outright is not drawn**: no data and the reason RESTRICTED. A widget showing part of its data with the rest masked stays | Review M-5: "Don't render widgets the user isn't allowed to see" |
| D-5 | **No specification code on the dashboards page**: its description reads "Shown by the {dashboard}", without the DSH code and the version | Review M-4 |

## 2. Tests

| Concern | Test |
| --- | --- |
| The Portfolio Manager's Home: decisions waiting, its requests, the Portfolio Dashboard offered; no task card; not a dashboard | `home.test.tsx` |
| The Project Manager's Home: its tasks and requests, no approvals waiting, no Portfolio | `home.test.tsx` |
| The System Administrator's Home: platform health, no business card | `home.test.tsx` |
| Accessibility | `home.test.tsx` (axe) |
| A widget restricted outright is not drawn | `dashboards.test.tsx` |
| No DSH code on the dashboards page | `dashboards.test.tsx` |
| The dashboards at `/dashboards`; DSH-009 still without a route | `dashboards.test.tsx` |

## 3. Validation

| # | Check | Result |
| --- | --- | --- |
| 1 | Frontend, in the e2e runner: `tsc -b --noEmit`, `eslint src` | Clean |
| 2 | `vitest run`, three full runs (1,086–1,090 tests) | 0, 14 and 9 failed, a different set each time: 5-second test timeouts and 1-second waits under load (another project's six containers were running on the machine). Every failed file was rerun: `--testTimeout=30000` over the nine files of the second run (407 of 408 passed; the one, `auditActivity.test.tsx`, passed alone twice, 24 of 24) and over the six of the third (392 of 392), `auditActivity.test.tsx` alone (33 of 33) |
| 3 | `vitest run src/features/home src/app/help src/shared/i18n` | 25 passed |

## 4. Findings

| # | Finding | Owner | Effect |
| --- | --- | --- | --- |
| F-1 | **Three platform-health measures wait for an API**: configuration drafts awaiting publication (the configuration screens of review H5), access ending soon (no end-date filter on assignments) and the last data import (no batch list) | Engineering | The administrator's Home shows integrations, audit and people only |
| F-2 | **Milestones due this week are not on the Home**: milestones are read one project at a time, like tasks, and the review lists them with tasks; the tasks card is the first step | Engineering | A Project Manager sees milestone dates on the project's Milestones tab |
| F-3 | **The Portfolio Dashboard is unchanged inside** (review M-2, M-3: headline cards, one health figure with an as-of chip, the same period as the KPI register) | Engineering (UX), with AHDA | The analysis page still reads as counts with bars |

## 5. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-10-25 | Created | Delivery team |

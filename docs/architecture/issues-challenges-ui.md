# Issue, Challenge & Escalation UI (WF-07 Frontend)

| Field | Value |
| --- | --- |
| Task | TASK-058 — Build Issue, Challenge & Escalation UI (WF-07 Frontend) (P9 - Risk & Issue Management (WF-06/07)) |
| Depends on | TASK-057 — the WF-07 API (`management-concern.md` §4, §5, §6 item 1): `/management-concerns` and `/concern-escalations`, 15 operations; severity computed by the server and pinned to the RISK_MATRIX version in force (D-4); one OPEN escalation per concern, replayed on a repeated `Idempotency-Key` (D-6). TASK-055/056 — the RISK_MATRIX scale issues share (ADR-011) and the risk screens this follows (`risk-management-ui.md`) |
| Record date | 2026-10-06 |
| Status | **BUILT AND VERIFIED LOCALLY**: 45 component and rule tests against a mocked API (§4 row 4), 10 mutation tests (§4.1), and **the workbook's two validation checks in Chromium against the real TASK-057 API** on a scratch database, signed in for real as the Project Manager and the Department Manager, nothing served by the browser (§4 rows 8, 9: 18 of 18 HTTP, 21 of 21 browser, 40 of 40 accessibility). **In a real environment** the shipped Department Manager — the role escalations are routed to — cannot list projects, so SCR-087 is empty for them and the notification's deep link does not open (F-1); and nobody but R01 reads role, person or catalogue names, so those show as shortened ids (F-2) |
| Branch | `feat/task-058-wf07-issues-challenges-frontend` |
| Deliverables | React components and routes for SCR-083 to SCR-088 and MOD-036 to MOD-039 in `src/frontend/src/features/issues-challenges` (`ProjectConcerns.tsx`, `ConcernRegisterPage.tsx`, `ConcernDetail.tsx`, `EscalationsPage.tsx`, `EscalationDetail.tsx`, `routes.tsx`, `paths.ts`, `dialogs/`, `components/`, `api/`, `access.ts`, `concernRules.ts`, `presentation.ts`, `problems.ts`, `useConcernData.ts`); i18n `features/issues-challenges/i18n/{ar,en}.json`. The workspace's Issues & challenges tab (`projects/workspace/IssuesChallengesTab.tsx`), the `/issues-challenges` routes and their navigation (D-1). A shared `ReadOnlyField` (D-3). 45 frontend tests. This record |
| Environment variables / secrets | None |
| Gate decision applied | None recorded for TASK-058 (the workbook's Gate Decision cell is empty) |
| Workbook read | The TASK-058 row of `AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`, read 2026-10-06: description, acceptance criteria, directory, deliverables, branch, and the validation checks "Confirm the Severity field is rendered as read-only in the Issue Detail form; verify the Escalations list default filter excludes resolved items and can be toggled to show them". The WF-07 Functional Specification §8 (SCR-083–088, §8.8 MOD-036–039, §8.9) and §9.1; the Step 16 Frontend Registers (audiences, proposed routes) — both in `Project Files.zip`. `management-concern.md`; `risk-management-ui.md` |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | SCR-083 Issue Register and SCR-085 Challenge Register: a project's (workspace tab) and across the projects a person may see | This task |
| **In** | SCR-084 Issue Detail and SCR-086 Challenge Detail: one screen for the one aggregate (ISS-GP-01) | This task |
| **In** | SCR-087 Escalations and SCR-088 Escalation Detail | This task |
| **In** | MOD-036 Create Issue, MOD-037 Edit Issue, MOD-038 Create Challenge, MOD-039 Escalate Item; the impact assessment, assignment, start, resolution submission, review and close; resolving and withdrawing an escalation | This task |
| **Out** | The specification's wider model: Draft/registration, duplicate, cancel, reopen, corrective actions and linked tasks, relations, change assessment, evidence, comments, acknowledgement, escalation levels | Not in the TASK-057 API (its F-1, F-14) |
| **Out** | Deciding a resolution's validation | WF-11's SCR-100 Approval Inbox (TASK-035/036) |
| **Out** | Risk escalations in SCR-087 | WF-06 has none (TASK-055 F-3) |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Where it lives.** `features/issues-challenges`. A project's registers are the workspace's **Issues & challenges** tab, after Risks, audience `reached` (CONCERN_VIEW ships to the Project Manager, the delivering entity and the department's manager, TASK-057 D-8): `/projects/:id/issues-challenges` is SCR-083 and `…/challenges` SCR-085, with an Issues \| Challenges sub-navigation (the schedule's pattern). A concern's detail is `/projects/:id/issues-challenges/:concernId` — **the address TASK-057's escalation notification links to** — and serves SCR-084 or SCR-086 by the concern's type. Across projects: `/issues-challenges` (SCR-083), `/issues-challenges/challenges` (SCR-085), `/issues-challenges/escalations` (SCR-087) and `/issues-challenges/escalations/:id` (SCR-088), behind `RequireSession`, with an "Issues and challenges" sidebar group and a home-page link. The Step 16 `/views/scr-0xx/…` routes are proposals only | ADR-002; `risk-management-ui.md` D-1; TASK-057 D-6's deep link |
| D-2 | **Acceptance criterion 1, on every screen: severity is shown, never an input.** The registers, SCR-084/086 and SCR-088 show the severity as a badge in the CONCERN_SEVERITY item's words with "Overall impact: level n", and SCR-084/086 say "Computed by the platform; not editable". An unassessed concern reads "Not assessed", never the lowest severity. No request the SPA builds has a severity, overall impact or version field (`toConcernRequest`, `toAssessCommand`; tests assert the exact bodies) | Acceptance criterion 1; TASK-057 D-4 |
| D-3 | **Validation check 1: MOD-037 renders Severity as a read-only field.** The edit form shows Severity as a labelled `<input readOnly>` (shared `ReadOnlyField`: announced read-only, reached in the form's order, dashed edge on the neutral ground) with the hint "Computed by the platform from the impact assessment; it cannot be edited. It changes only when the impact is reassessed." Priority is an ordinary required select with the hint that people set it and it is separate from severity. MOD-036/038 say severity is not entered. The severity changes only through **Assess impact** | Acceptance criterion 1; workbook check 1 |
| D-4 | **"Priority remains editable for authorized roles"** means MOD-037 is offered to whoever may manage the concern: the project's internal Project Manager (CONCERN_MANAGE ships to R04 at OWN, and the API refuses management to an external user, ADR-013), until the resolution goes to validation (409 CONCERN_NOT_EDITABLE after). Everyone else reads priority as text. `access.ts` is navigation only; the API decides | TASK-057 D-3, D-8 |
| D-5 | **The impact assessment** offers, for each impact dimension of the RISK_MATRIX version in force (the scale risks and issues share, ADR-011), that dimension's levels with their labels and "Not applicable" (VAL-ISS-006); at least one. It previews the overall impact (the highest) and says the platform computes the severity when saved — the SPA does not look the severity up, though the version carries the mapping. The matrix is read through WF-06's `useRiskMatrix`; without it (403/422) the dialog says why and offers nothing to send. A reassessment starts from the levels that still fit the version. The detail says whether the recorded severity was computed under the version in force or an earlier one | ADR-011; TASK-057 D-4 |
| D-6 | **Acceptance criterion 2: the Escalations list opens on open escalations only.** The Status filter's default is "Open only"; "Resolved", "Withdrawn" and "All (open and ended)" show the others. Open and ended escalations are told apart by the status in words (Open on a warning ground, Resolved positive, Withdrawn neutral), ended rows muted and saying when and by whom they ended, "Open for n day(s)" against "Was open for n day(s)", a count of open escalations, a status line ("Showing open escalations: n") and, in "All", open ones first. The empty open view says how to see the ended ones | Acceptance criterion 2; workbook check 2 |
| D-7 | **The default view reads no history.** Every concern carries its OPEN escalation, so "Open only" reads the visible projects' concerns and nothing else; the other views read each concern's `/concern-escalations` (it requires `managementConcernId`), six at a time, as SCR-080 reads risks. The cost of a history view grows with the number of concerns (F-3) | TASK-057 D-13 |
| D-8 | **MOD-039 sends a reason only and holds one `Idempotency-Key` per reason.** Pressing "Escalate" again after an answer was lost resends the same key, so the API replays the escalation and nothing is notified twice (TASK-057 D-6); a different reason is a different request with its own key, and if the first reached the API it is refused as already open (409, read again). The dialog says escalation is not approval, that the item's status does not change and that the platform routes it. A concern with an OPEN escalation, or resolved, is not offered another | WF-07 §8.8 MOD-039, ISS-GP-06; TASK-057 D-6 |
| D-9 | **SCR-088 is read from the escalation and its concern only.** The project is read for its title and a refusal shows "Not readable with your access", so the addressee needs no PROJECT_VIEW to open it (F-1). It shows the concern as it stands now: the API keeps no snapshot from the moment of escalation (F-4). The addressee resolves it with a management direction; its escalator withdraws it | WF-07 §8 SCR-088; TASK-057 D-8 |
| D-10 | **Who is offered resolving.** The session names its roles by code, an escalation its addressee by role id, and role names need ROLE_VIEW (R01 only). When the roles can be read, resolving is offered to holders of the addressed role; when not, to any internal person but the escalator, and the API decides (403 shown in the dialog). Withdrawing is offered to the escalator only | TASK-057 D-8 |
| D-11 | **The lifecycle as the API has it** (TASK-057 D-3): Assess/Reassess, Assign/Reassign and Edit while OPEN, ASSIGNED or IN_PROGRESS; Start work from ASSIGNED; Submit resolution from IN_PROGRESS (the validation is WF-11's, in its inbox; a returned resolution is prefilled for correction and the status reads "Returned by validation"); Record review until resolved; Close from RESOLVED once no escalation is open (the detail says so while one is). Commands without input are confirmed in words (WF-06's `ConfirmCommandDialog`, now given the WF-07 wording and stale codes); a 412, invalid transition, closed or frozen concern, or an escalation already open or ended reads the record again with "It changed while you were working on it" | TASK-057 §3 |
| D-12 | **The registers' order and views.** Most severe first by the overall impact the server derived the severity from (the severity items carry no rank), unassessed after, closed last; then the earliest target date. Views: Open (default), Assigned to me, Escalated, Overdue, Pending validation, Closed, All; severity and priority filters offer the values the concerns carry. Overdue (target passed, unresolved), Review due and Escalated are words with an icon; an overdue row also has a thick edge. Summary cards: open, not yet assigned, escalated, overdue, review due, pending validation | WF-07 §8 SCR-083 |
| D-13 | **Who raises.** MOD-036/038 are offered to the project's Project Manager and to an external user the project reaches (the delivering entity, ADR-013; CONCERN_RAISE ships to R04 OWN and R08 ENTITY) on an APPROVED_PLANNED, ACTIVE or SUSPENDED project; a raise lands on the new concern. A raise is never retried by the SPA (TASK-057 F-7). MOD-038 asks for the same fields as MOD-036 with challenge wording: the API has no constraint or dependency fields (F-5) | ADR-013; TASK-057 D-8, D-11 |

## 3. Screens and routes

| Screen | Route | Reads | Offers |
| --- | --- | --- | --- |
| SCR-083 (project) | `/projects/:id/issues-challenges` | `/management-concerns?projectId&concernType=ISSUE`; CONCERN_CATEGORY, PRIORITY, CONCERN_SEVERITY, IMPACT_DIMENSION | Summary cards, view/severity/priority filters, the table; MOD-036 |
| SCR-085 (project) | `/projects/:id/issues-challenges/challenges` | as above, `CHALLENGE` | as above; MOD-038 |
| SCR-083 / SCR-085 (across projects) | `/issues-challenges`, `/issues-challenges/challenges` | `/projects?status=…`, then each project's concerns | The same, each concern naming its project |
| SCR-084 / SCR-086 | `/projects/:id/issues-challenges/:concernId` | `/management-concerns/{id}` (ETag), `/concern-escalations?managementConcernId`, the matrix, `/roles` | The commands (D-11), MOD-037, MOD-039; resolve/withdraw on its escalations |
| SCR-087 | `/issues-challenges/escalations` | `/projects`, each project's concerns; with an ended view, each concern's escalations | Status (default Open only) and type filters |
| SCR-088 | `/issues-challenges/escalations/:id` | `/concern-escalations/{id}`, its concern, its project (optional) | Resolve with direction; withdraw |

## 4. Verification

Run 2026-10-06 on macOS with Docker Desktop: frontend in `AHDA-frontend` (Node 24); the API image built from TASK-057 (`ahda-api`), which is what `dev` serves — no backend file changed since.

| # | Command | Result |
| --- | --- | --- |
| 1 | `npm run lint` | 0 problems |
| 2 | `npm run format:check` | All matched files use Prettier code style |
| 3 | `npm run typecheck` | No errors |
| 4 | `npm test` | 41 files, 610 tests passed (39 files, 565 on `dev`). 45 new: 27 in `issues-challenges/concerns.test.tsx`, 18 in `issues-challenges/rules.test.ts`. The two tab assertions in `projects` include Issues & challenges. On the first full run `documents/detail/detail.test.tsx` failed on axe `document-title`, the known flake (TASK-056 record); it passed on the next run with no change |
| 5 | `npm run build` | Built. 1,524 KB JS (367 KB gzip); the >500 KB chunk warning was already on `dev` |
| 6 | `npm audit` | 1 high (`source-map-js`, GHSA-68fv-2mgg-jv7q), as on `dev` (`risk-management-ui.md` F-5); `package.json` and the lockfile are unchanged |
| 7 | — | No backend file changed; the `dotnet` gates do not apply |
| 8 | Live fixture (git-ignored `artifacts/task-058/live`; `reset.sh` rebuilds it): scratch database `task058_live`, migrated by `ahda-migrate` to `TASK-057_GuardManagementConcernHistory` and seeded by the compose seed; `setup.sql` is TASK-057's WF-07 configuration (CONCERN_CATEGORY, PRIORITY, CONCERN_SEVERITY items; GOVERNANCE_PROFILE; WORKFLOW_POLICY routing escalations to R03; APPROVAL_AUTHORITY; NOTIFICATION_ROUTING with a template; a RISK_MATRIX version mapping levels to severities) with project A managed by **local.r02** and **test grants**: R02 at ALL the four concern permissions R04 holds at OWN, PROJECT_VIEW, CONFIGURATION_VIEW and MASTER_DATA_VIEW (the shipped R04 reads no project, `ahda-dev-environment`); R03 at DEPT PROJECT_VIEW only (F-1). A second API container on 5088. `prepare.py`, over HTTP: local.r02 raises a challenge and two issues, assesses and escalates each; withdraws one escalation; local.r03 resolves another | **18 of 18** (`prepare-output.txt`): the live `ConcernDetail` and `ConcernEscalationDetail` carry exactly the fields of `api/types.ts`; a concern answers an ETag; escalations are addressed to R03 and resolved by local.r03; local.r03 is refused `/roles` (403) |
| 9 | **Browser check** (`check.mjs`): Chromium headless with puppeteer-core 24 inside `AHDA-frontend`, a second Vite on 5178 forwarding `/api` to the scratch API, signed in for real; nothing served by the browser. Phase *main*; phase *a11y* | **Main, 21 of 21** (`check-main-output.txt`). As local.r02: MOD-036 raised "Collapsed culvert" through the form, no severity in the POST; Assess impact at level 4 → the server's **Major**, shown as text. **Validation check 1**: MOD-037's Severity is an input with `readOnly` labelled "Severity", value "Major"; typing into it changed nothing; Priority was an enabled select, changed to Low and saved; the PUT carried the priority and no severity; the API then held priority Low and severity MAJOR at level 4. MOD-039 escalated it; the issue stayed OPEN. As local.r03: **Validation check 2**: SCR-087's Status filter defaulted to "Open only" and listed the two OPEN escalations, not the RESOLVED or WITHDRAWN ones; toggled to All, all four, open first, the ended ones muted reading Resolved and Withdrawn; Resolved listed the resolved one only; back to Open only. SCR-088 resolved the culvert's escalation, which then left the default list and appeared under Resolved. **A11y, 40 of 40** (`check-a11y-output.txt`): axe with colour contrast on SCR-083 (project and across), SCR-085, SCR-084, SCR-086, SCR-087 and SCR-088 in English and Arabic, and on MOD-036, MOD-037, MOD-038, MOD-039, the assessment and the assignment: no violation; no sideways page scroll at 390 px on every screen in both languages; every field of every modal inside its dialog. Screenshots `out/` |

The first browser run read 18 of 21: the harness read an escalation row's first badge, which is the severity, instead of its status. Fixed in the harness and run again from a fresh database. Component tests found one defect before the live run: SCR-088's sections were `h3` under the page's `h1` (axe `heading-order`); they are `h2`.

The tests, by acceptance criterion:

| Item | Tests |
| --- | --- |
| 1. Severity read-only, Priority editable for authorised roles | `concerns.test.tsx`: the register and detail show severity as text with no input and "Not assessed" for an unassessed issue; **validation check 1** — MOD-037's Severity input is read-only, typing changes nothing, no severity select exists, Priority is enabled and the PUT body is exactly the five fields with If-Match; a non-manager is offered no edit and reads priority as text; nothing is editable once the resolution is with validation; MOD-036 sends no severity; the assessment sends levels only. `rules.test.ts`: `toConcernRequest` keys, `toAssessCommand`, `impactValuesOf`, `canEditConcern` (an external Project Manager included). Live: row 9. M-1 to M-4 |
| 2. Escalations list: open vs. resolved, default open-only | `concerns.test.tsx`: **validation check 2** — the filter defaults to Open only, only the OPEN escalation is listed and no history is read; toggled to All, the ended ones appear muted after the open one with their status in words and who ended them; Resolved lists the resolved one; back to Open only; the empty open view says how to see the ended ones; Arabic. `rules.test.ts`: the default, `needsHistory`, `matchesEscalationView`, `byEscalation`, `escalationAgeDays`. Live: row 9. M-5 to M-8 |
| MOD-036 to MOD-039, commands | Exact bodies of raise (issue and challenge), edit, assess, submit-resolution, escalate and resolve; blank reason, resolution and direction refused before sending; MOD-039 resends one key on a retry and a new one for a new reason (M-9, M-10); a stale edit reads the issue again; close waits for the open escalation; resolve and withdraw offered to the addressee and the escalator only |

### 4.1 Mutation tests

Each mutation was applied, `vitest run src/features/issues-challenges` run in `AHDA-frontend`, and the source restored (`artifacts/task-058/mutate.py`, `mutation-results.txt`). All ten were caught; the restored baseline passed 45 of 45.

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | MOD-037's Severity field is not read-only | 1 |
| M-2 | The edit request carries the severity | 3 |
| M-3 | Priority is disabled in MOD-037 | 3 |
| M-4 | An external Project Manager is offered management (ADR-013) | 1 |
| M-5 | The Escalations list opens on all statuses | 3 |
| M-6 | The status filter is ignored | 2 |
| M-7 | Ended escalations are not muted | 2 |
| M-8 | The open view reads every escalation history | 3 |
| M-9 | MOD-039 takes a new Idempotency-Key on every attempt | 1 |
| M-10 | A blank reason or resolution is sent | 3 |

## 5. Acceptance criteria and deliverables

| Item | Status |
| --- | --- |
| Severity is displayed read-only (computed, not editable) while Priority remains an editable field for authorized roles | **Met** (D-2 to D-4; §4 rows 4, 9; M-1 to M-4) |
| The Escalations list clearly differentiates open vs. resolved escalations with a filter default of open-only | **Met** (D-6, D-7; §4 rows 4, 9; M-5 to M-8) |
| Validation: the Severity field is rendered read-only in the Issue Detail form | **Met** in jsdom and live (§4 rows 4, 9) |
| Validation: the Escalations list's default filter excludes resolved items and can be toggled to show them | **Met** in jsdom and live (§4 rows 4, 9) |
| React components/routes for SCR-083–088 and MOD-036–039 | **Delivered** (§3) |

## 6. Findings

| # | Finding | Owner | Effect until resolved |
| --- | --- | --- | --- |
| F-1 | **The Department Manager, to whom escalations are routed, cannot list or open projects.** R03 ships CONCERN_VIEW and CONCERN_ESCALATION_RESOLVE at DEPT but no PROJECT_VIEW, and the API lists concerns only per project (`projectId` required) and escalations only per concern. So SCR-087 has nothing to read for them, and the escalation notification's link (`/projects/{p}/issues-challenges/{c}`, TASK-057 D-6) lands in a workspace that cannot read the project. SCR-088 works without the project (D-9), but nothing leads them to it. Either Appendix A grants R03 PROJECT_VIEW at DEPT, or the API serves a caller's escalations without a concern id (e.g. `GET /concern-escalations?status=OPEN` scoped by CONCERN_VIEW) and the notification links to SCR-088 | PMO (Appendix A); Engineering Architect (API) | The shipped addressee cannot find their escalations in the UI |
| F-2 | **Names read as ids for everyone but R01.** Roles need ROLE_VIEW, people USER_VIEW, and category, priority and severity labels MASTER_DATA_VIEW — all R01-only. The UI falls back to "Role 00000000", "User 00000000", "Item 58580000" (seen live as local.r03). The severity is still read-only and coloured by level, but its word is lost; MOD-036/038 cannot be filled without the catalogues and say so. Same cause as `risk-management-ui.md` F-1 | PMO (Appendix A); Engineering Architect (expanded names in representations) | Severity, priority, category, addressee and people are not readable by name |
| F-3 | **No escalation list across concerns.** A history view of SCR-087 reads every visible concern's escalations, six at a time (D-7); the default view does not | Engineering Architect | Slow history views with many concerns |
| F-4 | **No snapshot or acknowledgement.** SCR-088 shows the concern as it is now; the specification's frozen source snapshot, trigger, level, routed recipients, SLA and acknowledgement are not in the API (TASK-057 F-14) | Engineering Architect; PMO | SCR-088 has no acknowledgement step or history of who was notified |
| F-5 | **The specification's wider screens are not built**: tabs for actions and tasks, relations and linked issues, evidence, change assessment, activity; duplicate, cancel and reopen; challenge constraint/dependency fields; age/aging. The API has none of them (TASK-057 F-1) | PMO / Business Analyst; later tasks | SCR-084/086 show the row's model only |
| F-6 | **A resolution's validation is decided elsewhere**: the concern shows it as pending, returned or validated; the decision is WF-11's inbox (SCR-100), and APPROVAL_DECIDE ships to no one (TASK-057 F-2) | PMO (Appendix A) | No concern reaches RESOLVED in an environment |
| F-7 | **No Security Lead review is requested (CTL-43)**: this change touches no authentication, RBAC, data scope or upload; commands are offered as navigation only and decided by the API | Maintainer | — |

## 7. Change log

| Date | Change |
| --- | --- |
| 2026-10-06 | Created (TASK-058) |

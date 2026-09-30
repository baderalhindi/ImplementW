# Approvals UI: Inbox, My Requests and History (WF-11 Frontend)

| Field | Value |
| --- | --- |
| Task | TASK-036 — Build Approvals UI: Inbox, My Requests & History (WF-11 Frontend) (P6 - Shared Platform Services) |
| Depends on | TASK-035 — the WF-11 API (`approval-framework.md` §3): every screen reads and writes through all eleven of its operations, and every refusal shown is one it returns. TASK-032 — the SPA foundation (`identity-access-administration-ui.md` D-1) |
| Record date | 2026-09-30 |
| Status | **BUILT AND VERIFIED LOCALLY** against a mocked API (component tests; axe, Lighthouse and screenshots in headless Chromium) and, for the server half of the two workbook checks, against the real API pipeline on `AHDA-postgres` and `AHDA-ldap` (§4). No role holds `APPROVAL_VIEW` or `APPROVAL_DECIDE` and no APPROVAL_AUTHORITY is published (TASK-035 F-1, F-2), so on the compose stack every screen answers "You do not have approval authority for this" (F-3) |
| Branch | `feat/task-036-wf11-approvals-frontend` |
| Deliverables | React components and routes for SCR-100, SCR-101, SCR-114, SCR-115 and MOD-040–045 in `src/frontend/src/features/approvals` (`inbox/`, `requests/`, `delegations/`, `history/`, `dialogs/`, `routes.tsx`, `api/`); i18n files `features/approvals/i18n/{ar,en}.json`. Small additions to the TASK-032 foundation (D-7). 43 component tests; 3 integration test cases in `ApprovalEndpointTests`. This record |
| Environment variables / secrets | None |
| Gate decision applied | None on the row |
| Participation amendment | **ADR-013**: MOD-043 searches internal users only and says why; the API refuses an external delegate on its own (TASK-035 D-6) |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), Implementation Plan row TASK-036, as read 2026-09-30; `approval-framework.md`; `identity-access-administration-ui.md`; `api-conventions.md` |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | SCR-100, SCR-101, SCR-114, SCR-115 and MOD-040–045, Arabic and English, responsive | This task |
| **In** | Server tests for the workbook's two checks that TASK-035 did not cover: an empty or blank reason, and a delegated task in the delegate's inbox (§4 rows 8, 9) | This task |
| **Out** | A human title and a link to the source record of each request | TASK-041, 046, 050, 052, 057, 060, 062, 063 (F-4) |
| **Out** | Who may approve (Appendix A) and the routes and policy values (OQ-005) | PMO, AHDA (F-3) |
| **Out** | Notifications of a new or escalated task | TASK-039 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **The inbox shows exactly what `GET /approval-tasks` returns.** It makes one call per page and never filters, merges or adds tasks. Which tasks an approver may decide, by their own role and scope or by a delegator's, is decided on the server at the moment of listing (TASK-035 D-4, D-5). | Acceptance criterion 1. A client-side filter could only hide tasks, never enforce scope. |
| D-2 | **Routes under `/approvals` need a session, not a role.** A new `RequireSession` guard (which `RequireRole` now uses) sends a signed-out person to sign in. No role is checked: the session carries role codes, not permissions, and the permissions are ungranted today (F-3). A 403 is shown as "You do not have approval authority for this", never as an empty list. | Any role may hold approval authority once Appendix A is settled. A refusal that looks like "nothing to approve" would mislead. |
| D-3 | **Four states, never mixed.** Each list shows exactly one of: loading ("Loading your approvals…", `role="status"`), error with retry (`role="alert"`), empty ("No pending approvals"), or the table. `useApiResource` clears the previous result while a new one loads. | Acceptance criterion 3. |
| D-4 | **MOD-040, 041, 042 and 045 are one dialog** (`DecisionDialog`), parameterised by action. Reject and return mark the reason required. A blank reason is refused before any request, with `aria-invalid`, a described error, a form alert and focus on the field. A server refusal of `reason` or `reason.text` lands on the same field. Approve and escalate send `{}` when no reason is given. | Acceptance criterion 2, client half. The API refuses the same cases (§4 row 8). |
| D-5 | **A reason is tagged with the interface language** (`{text, language: "ar"\|"en"}`), trimmed, at most 2000 characters (`NarrativeTextRequest.TextLength`). SCR-115 shows each reason with its `lang` and `dir="auto"`. | ADR-012 extension, ERD D-7: the entry language is recorded once, at entry (F-6). |
| D-6 | **When a task has moved on, the list is read again.** `TERMINAL_STATE`, `APPROVAL_CONCURRENT_DECISION` and `NOT_FOUND` after a decision close the dialog, show a warning notice and reload. A retried decision after a lost reply is one of these (TASK-035 F-10). Other refusals stay in the dialog. | The person should see the current list, not retry a decision that already happened. |
| D-7 | **Four additions to the foundation, all backward compatible.** `useSaveAction(describe?)` takes a feature's refusal messages and returns the error on failure. `ConfirmDialog` passes a describer through. `StatusBadge` moves from `identity-access/components` to `shared/ui` and gains `info` and `warning` tones. `FormFields` gains `TextAreaField`, and `useFocusFirstError` now reaches textareas. The shell shows the approvals navigation to anyone signed in and the administration navigation to R01. | DRY: the approvals screens reuse the TASK-032 forms, dialogs, user picker and name lookup instead of copying them. |
| D-8 | **Each state has its own tone and its own word.** Pending is `info` (blue), approved `positive` (green), rejected `negative` (red), returned `warning` (amber), withdrawn/cancelled/escalated `neutral`; "Overdue" is `negative`. New colours: `#1d4f91` on `#e6eef9` (6.96:1), `#7a4100` on `#fdf0dc` (7.23:1). | The workbook asks for clear pending/approved/rejected/returned indicators. The label carries the meaning; colour only repeats it (WCAG 1.4.1). |
| D-9 | **Names are read where the caller may read them.** The approval representations carry ids only. Roles come from `GET /roles`, people from `GET /users/{id}`; either may answer 403. Then the screen shows "User b2b2b2b2" / "Role d4d4d4d4", and the signed-in person is always "You". | There is no other source of names (F-1). |
| D-10 | **Decisions are taken from the inbox only.** SCR-115 offers the requester's two actions: withdraw a pending run, and escalate an overdue task of the current stage. Approvers decide from SCR-100, which lists only the tasks they can decide. | The history does not say which tasks the viewer may decide; offering buttons that then fail 403 would mislead. |

## 3. Screens and routes

All under `/approvals`, behind `RequireSession` (`features/approvals/routes.tsx`).

| Screen | Route | Component |
| --- | --- | --- |
| SCR-100 Approval Inbox | `/approvals/inbox?page=` | `inbox/ApprovalInboxPage.tsx` |
| SCR-101 My Requests | `/approvals/requests?status=&page=` | `requests/MyRequestsPage.tsx` |
| SCR-114 Delegated Approvals | `/approvals/delegations` | `delegations/DelegatedApprovalsPage.tsx` |
| SCR-115 Workflow/Approval History | `/approvals/instances/:instanceId` | `history/ApprovalHistoryPage.tsx` |
| MOD-040 Approve, MOD-041 Reject, MOD-042 Return | on SCR-100 | `dialogs/DecisionDialog.tsx` |
| MOD-043 Delegate | on SCR-114 | `dialogs/DelegateDialog.tsx` |
| MOD-044 Withdraw | on SCR-101 and SCR-115 | `dialogs/WithdrawDialog.tsx` |
| MOD-045 Escalate | on SCR-115, requester only, overdue task of the current stage | `dialogs/DecisionDialog.tsx` |

Revoking a delegation (SCR-114) uses the platform `ConfirmDialog`. `/approvals` redirects to the inbox.

## 4. Verification

Run 2026-09-30 on macOS with Docker Desktop: frontend in `AHDA-frontend` (Node 24.21.0), backend on the host (.NET SDK 10.0.401) against `AHDA-postgres` and `AHDA-ldap`. No other container was started.

| # | Command | Result |
| --- | --- | --- |
| 1 | `npm run lint` | 0 problems |
| 2 | `npm run format:check` | All matched files use Prettier code style |
| 3 | `npm run typecheck` | No errors |
| 4 | `npm test` | 14 files, 117 tests passed (74 existing, 43 new) |
| 5 | `npm run build` | Built. Bundle 721 KB JS (202 KB gzip), 11 KB CSS; the >500 KB chunk warning already appears on `dev` (672 KB) (F-7) |
| 6 | `npm audit` | 0 vulnerabilities. No dependency added |
| 7 | Headless Chromium 154 against the production build and a throwaway mock API of TASK-035's contract: axe-core 4.13.0 **with colour contrast** on the four screens and the reject dialog with its error shown, in both languages; Lighthouse 13 accessibility on SCR-100 | axe: no violation on any page. Lighthouse: **100** in English and Arabic, desktop and mobile. Reject with an empty reason: no POST sent, field error shown, in both languages. `<html dir>` `ltr`/`rtl` |
| 8 | `dotnet test … --filter RejectingOrReturningWithAnEmptyReason…` | Passed, reject and return. Through the real API pipeline, no reason, `{}`, `{reason: {text: ""}}` and `{reason: {text: "   "}}` are each 400 `VALIDATION_FAILED` (`reason REQUIRED` or `reason.text REQUIRED`), and the run and task stay PENDING with no reason recorded |
| 9 | `dotnet test … --filter ADelegatesInboxListsTheDelegatedTask…` | Passed. `local.r03` does not see an R02 task; after `local.r02` delegates the routing key to them, `GET /approval-tasks` lists it with `onBehalfOfUserId` = `local.r02`; after revocation it is gone |
| 10 | `dotnet build src/backend -warnaserror`, Debug and Release | 0 warnings, 0 errors |
| 11 | `dotnet test src/backend/PMPlatform.Tests.Unit`; `DB_CONNECTION_STRING=… dotnet test src/backend/PMPlatform.Tests.Integration` | 425 passed; 312 passed (309 before, 3 new) |
| 12 | Screenshots of the four screens at 1280 and 390 px, both languages | Arabic mirrored: navigation on the right, columns reversed, Latin codes left-to-right. At 390 px tables scroll inside their region, not the page. The first run showed the inbox's Return button clipped at 1280 px in English; the three decision buttons now wrap in their cell (D-8 styles), and rows 7 and 4 were run again |
| 13 | The eight `docs/architecture/*-check.py` gates (contract-check needs a running API) | All OK |

The browser tooling (Chromium, Noto fonts, puppeteer-core, Lighthouse, the mock API) was installed under `/tmp` and by apt inside `AHDA-frontend` for the run, then removed. None of it is in the repository.

The tests, by acceptance criterion and validation check:

| Criterion | Tests |
| --- | --- |
| 1. An approver sees only tasks assigned to them per FG-03 scope | Server: TASK-035's `AnApproverFindsTheTaskInTheInbox…`, `RequestsAreGatedValidatedAndAnsweredByR47` and the delegation-chain tests decide what `GET /approval-tasks` returns. Client (`inbox.test.tsx`): the screen renders exactly the returned rows, from one call to the inbox endpoint and none elsewhere (D-1); a 403 is an error, not an empty inbox |
| 2. Reject/return require a non-empty reason, client- and server-validated | Client (`decision.test.tsx`): empty and blank reasons refused for both actions with no request sent; the server's `reason.text REQUIRED` shown on the field; the reason is sent trimmed with its language. Server: row 8 |
| 3. A distinct "no pending approvals" empty state, apart from loading | `inbox.test.tsx`: loading is shown and the empty state is not; after the answer the empty state reads "No pending approvals" and the loading state is gone; row 7 in both languages |
| Status indicators | `requests.test.tsx`: pending, approved, rejected and returned have four different tones; `history.test.tsx` for tasks |
| Validation: reject with an empty reason; client and server both refuse | `decision.test.tsx`; row 7 (browser); row 8 (API) |
| Validation: an approver assigned via delegation sees the delegated item in their inbox | Row 9 (API); `inbox.test.tsx` shows "On behalf of Huda Delegator" |
| MOD-043–045, SCR-114/115 | `delegations.test.tsx` (lists, empty states, revoke, required fields, internal-only search, request body, `APPROVAL_DELEGATE_INVALID`); `history.test.tsx` (stage order, delegated decider, reason language, previous revision, escalate visibility and request, withdraw); `requests.test.tsx` (filter, withdraw, `TERMINAL_STATE`); `access.test.tsx` (sign-in redirect, navigation, home link) |

### 4.1 Mutation tests

Each mutation was applied, the tests run, and the source restored.

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | The dialog accepts an empty reason | 2 (reject, return) |
| M-2 | The empty state is shown while loading | 1 |
| M-3 | A delegated task is shown as the approver's own | 2 |
| M-4 | The inbox drops a returned task | 1 |
| M-5 | Returned has the rejected tone | 2 |
| S-1 | The API accepts reject/return without a reason | 2 (row 8) |
| S-2 | The API accepts a blank reason text | 2 (row 8) |
| S-3 | The inbox omits `onBehalfOfUserId` | 1 (row 9) |

## 5. Acceptance criteria, validation and amendment

| Item | Result |
| --- | --- |
| An approver sees only tasks assigned to them per FG-03 scope | **MET.** Enforced by the API (TASK-035 D-4); the screen adds nothing (D-1). Not exercised against the compose API with data (F-3) |
| Rejecting or returning requires a non-empty reason (client- and server-validated) | **MET** (D-4; §4 row 8) |
| A distinct "no pending approvals" empty state separate from loading | **MET** (D-3) |
| Validation: empty-reason reject refused by client and server | **MET** (§4 rows 7, 8) |
| Validation: an approver assigned via delegation sees the delegated item | **MET** (§4 row 9) |
| Deliverables: routes and components for SCR-100/101/114/115 and MOD-040–045 | **MET** (§3) |
| ADR-013 | **MET** in MOD-043 (internal-only search, `DelegateDialog`, tested in `delegations.test.tsx`); enforced by the API (TASK-035 D-6) |

## 6. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **The approval representations carry ids only**: the task's role, the requester, the delegator, and the deciding and authority users. Names come from `GET /roles` and `GET /users/{id}`, which need ROLE_VIEW and USER_VIEW. An approver without them sees "User b2b2b2b2" and "Role d4d4d4d4" (D-9). A role code and display names in `ApprovalInboxItem`, `ApprovalTaskDetail` and `ApprovalDelegationDetail` would fix it | Engineering Architect; TASK-035 module | Approvers cannot tell who requested or which role a stage is |
| F-2 | **MOD-043 needs USER_VIEW to choose a delegate**: the picker is ADM-002's user search. An approver without USER_VIEW sees the permission refusal in the picker and cannot delegate from the UI | Engineering Architect / Identity (a delegate lookup any approver may call); PMO (Appendix A) | Delegation works only for callers who may list users |
| F-3 | **No role holds `APPROVAL_VIEW` or `APPROVAL_DECIDE`, and no route or policy is published** (TASK-035 F-1, F-2). On the compose stack every screen answers 403, shown as "You do not have approval authority for this"; no screen has run against the live API with data | PMO (Appendix A); AHDA (OQ-005) | A contract drift between the mock and the API would first appear in SIT |
| F-4 | **A request is shown as its module, type, revision and routing key**, without a title or a link to the record. Approval holds no title (M-8) and no source module is built | TASK-041, 046, 050, 052, 057, 060, 062, 063 (a title, or a route per subject module) | Approvers must find the record elsewhere to judge it |
| F-5 | **Names are read one user at a time** (TASK-032 F-8): at most 25 rows a page, each with up to two people | Identity (`/users?ids=`) | Up to 50 small reads per inbox page |
| F-6 | **The reason's language is the interface language** (D-5). Someone typing Arabic in the English interface records `en` | Engineering Architect (ADR-012 extension, TASK-109) | The tag can be wrong for mixed-language users |
| F-7 | **The production bundle is 721 KB in one chunk** (672 KB on `dev`, +49 KB here). Vite warns above 500 KB. No route is code-split | Frontend lead (route-level `lazy()`; no workbook row owns it) | Initial load grows with every module |
| F-8 | **Delivery-team Arabic strings** have not had a bilingual business review (as TASK-032 F-9) | PMO / AHDA business reviewer | Terminology may differ from AHDA's usage |
| F-9 | **Security Lead review (CTL-43) cannot be requested**: the CODEOWNERS teams do not exist (TASK-031 F-13). This change adds a route guard and shows authority-dependent data | Maintainer | The PR's CTL-43 box stays unticked |

## 7. Change log

| Date | Change |
| --- | --- |
| 2026-09-30 | Created (TASK-036) |

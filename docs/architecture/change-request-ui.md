# Change Request UI (WF-08 Frontend)

| Field | Value |
| --- | --- |
| Task | TASK-061 — Build Change Request UI (WF-08 Frontend) (P10 - Governance & Change Control (WF-08/09/10)) |
| Depends on | TASK-060 — the WF-08 API (`change-request.md` §4, §5, §6 item 1): `/change-requests`, 12 operations used here; materiality previewed before review and recorded at it (D-4); authorisations issued on approval and applied by their modules (D-6, D-7). TASK-035/036 — WF-11 and its modals (`approval-framework.md`, `approvals-ui.md`) |
| Record date | 2026-10-07 |
| Status | **BUILT AND VERIFIED LOCALLY**: 51 component and rule tests against a mocked API (§4 row 4), 12 mutation tests (§4.1), and **both workbook validation checks in Chromium against the real TASK-060 API** on a scratch database, signed in for real as the Project Manager (local.r04, shipped grants) and AHDA's reviewer and approver (local.r02, test grants): 33 of 33 checks, and 48 of 48 accessibility and layout checks (§4 rows 8, 9). **In a real environment** the Project Manager cannot read their own project on the shipped grants, so the workspace tab and the cross-project list are out of their reach and SCR-106/107 are reached by address only (F-1); and no request can be submitted from SCR-106 until AHDA publishes MATERIALITY_BAND bands (F-3) |
| Branch | `feat/task-061-wf08-change-request-frontend` |
| Deliverables | React components and routes for SCR-105–107 in `src/frontend/src/features/change-requests` (`ChangeRequestRegisterPage.tsx`, `ProjectChangeRequests.tsx`, `ChangeRequestFormPage.tsx`, `ChangeRequestDetailPage.tsx`, `routes.tsx`, `paths.ts`, `components/`, `api/`, `access.ts`, `changeRequestRules.ts`, `presentation.ts`, `problems.ts`, `useChangeRequestData.ts`); i18n `features/change-requests/i18n/{ar,en}.json`. The workspace's Change requests tab, the `/change-requests` routes, sidebar group and home-page link. Shared: `approvals/sourceReview.ts` (D-5), `CheckboxField`, `readInBatches`. 51 frontend tests. This record |
| Environment variables / secrets | None |
| Gate decision applied | None recorded for TASK-061 (the workbook's Gate Decision cell is empty) |
| Workbook read | The TASK-061 row of `AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`, read 2026-10-07: description, acceptance criteria, directory, deliverables, branch, and the validation checks "Submit a change request and confirm the materiality classification is visible before submit is enabled; verify Approved and Implemented render as visually distinct statuses". The WF-08 Functional Specification §2 (CHG-GP-08), §4, §4.1, §6, §8, §9; the Step 16 Frontend Registers rows for SCR-105–107 (audiences, proposed routes, "No browser business calculation", "WF-11 decisions acknowledged separately from source application") — both in `Project Files.zip`. `change-request.md`; `issues-challenges-ui.md`; `schedule-ui.md` |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | SCR-105 Change Requests: a project's (workspace tab) and across the projects a person may see | This task |
| **In** | SCR-106 Create Change Request, also used to correct and resubmit a DRAFT or RETURNED request, with the materiality classification before submission | This task |
| **In** | SCR-107 Change Request Detail: approval and implementation as separate badges, the recorded or previewed classification, the review, the authorisations, the lifecycle commands, and WF-11's MOD-040–042 and MOD-044 | This task |
| **Out** | The specification's Change Items, impact assessments, implementation actions and verification, CR number, urgency | Not in the TASK-060 API (its F-1) |
| **Out** | An authorisation picker on the WF-03 and WF-14 screens | TASK-060 F-11; a WF-03/WF-14 UI task (F-7) |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Where it lives.** A project's list is the workspace's **Change requests** tab (`/projects/:id/change-requests`, after Issues & challenges, audience `reached`). The form and the detail are under `/change-requests` — `new?projectId=`, `:id`, `:id/edit` — and the cross-project list is `/change-requests`, with a sidebar group and a home-page link. The detail and the form work without the project: the reviewer (R03) holds no PROJECT_VIEW (TASK-058 F-1), and on the shipped grants neither does the Project Manager (F-1); a project answering 403 or 404 is shown as "not readable with your access" and what the person may do is the API's answer. WF-11's screens link a `ChangeRequest` subject to its detail (`SubjectSummary`), so an approver reads the change before deciding. The Step 16 `/views/scr-10x/…` routes are proposals only | ADR-002; `issues-challenges-ui.md` D-1, D-9 |
| D-2 | **Acceptance criterion 1: the classification comes before submission.** SCR-106 has three actions: *Save draft*, *Save and check materiality* (saves the draft — POST, or PUT with If-Match — then `POST …/preview-materiality`) and *Submit for review*. Submit is **disabled until the classification of the request as saved is on screen**; any edit after it withdraws the classification and disables submit again until it is checked again. The panel shows the resulting band ("Band 2 of 3"), the approval path that band enters, each dimension's band beside the impact stated and the cumulative position since the active baseline, and its basis (advisory preview, configuration version, baseline and budget). Beside the buttons the form says which path submitting enters ("Submitting sends revision 1 into the band 2 approval path"). | Acceptance criterion 1; workbook check 1; TASK-060 D-4 |
| D-3 | **The SPA computes no band and sends none** (WF-08 CHG-GP-08; Step 16 "No browser business calculation"). The band, its route and the cumulative figures are the server's; `toChangeRequestRequest` carries the request's own fields only, and tests assert the exact bodies. The approval path is worded per band (band 3 "the elevated route"), naming no approver: the route is AHDA's APPROVAL_AUTHORITY configuration, which a requester cannot read (F-8) | TASK-060 D-4, D-5 |
| D-4 | **Acceptance criterion 2: Approved and Implemented never look alike.** SCR-107 shows the request's state as two labelled dimensions (WF-08 §4.1): **Approval** (Not submitted, Awaiting review, In review, Returned, Approved, Rejected, Withdrawn) and **Implementation** (Not applicable, Not started, In progress with "n of m authorisations applied", Implemented), with the lifecycle status beside them and a sentence saying what they mean together ("Approved, not implemented. The approval issued 2 authorisation(s) and changed nothing itself…"). An approved request keeps *Approved* through its implementation. The two styles are reserved: Approved is outlined on a pale ground with a tick, Implemented solid with a double tick; they differ in fill, edge, text colour and icon as well as in words (WCAG 1.4.1; white on the positive colour is 8:1), and no other state uses either. The lists use the same two styles for the APPROVED and IMPLEMENTED statuses | Acceptance criterion 2; workbook check 2; TASK-060 D-8 |
| D-5 | **WF-11's modals are reused, not rebuilt.** Under review, the person's inbox task for the revision is found (`approvals/sourceReview.ts`: `findInboxTask`, `decidesSubject`, `unlessForbidden`, lifted from the schedule screen, which now uses them too) and Approve, Return and Reject open WF-11's own `DecisionDialog` (MOD-040–042), with its reason rules; the originator withdraws a review in progress with WF-11's `WithdrawDialog` (MOD-044), which withdraws the request (TASK-060 D-5). A decision is acknowledged as WF-11's ("Your approval is recorded. The request shows Approved once every stage of its route has approved and the outcome reaches it") and the request is read again: the outcome reaches WF-08 through the outbox. The runs link to their history (SCR-115) when the person may read them | Description "reusing the WF-11 approval modals"; Step 16 "WF-11 decisions acknowledged separately from source application" |
| D-6 | **Who is offered what** (`access.ts`, navigation only). Raising, editing, deleting a draft and withdrawing before review: the requester or the project's Project Manager (CHANGE_REQUEST_RAISE at OWN). Start review: an internal person who did not raise it and whose assignments reach the project (or anyone internal but the raiser when the project cannot be read) — R03 at DEPT ships it. Start implementation, mark implemented, close: the internal raiser or Project Manager (R04 IMPLEMENT at OWN; ADR-013 keeps it from external users). A decision: an internal person whose inbox holds the task, never the run's requester. Mark implemented is offered only once every authorisation is applied; before, the screen says what it waits for | TASK-060 D-11 |
| D-7 | **The lifecycle as the API has it** (TASK-060 §3). A DRAFT or RETURNED request is edited and submitted through SCR-106 only, so the classification is always seen before submission; a returned one says it comes back as the next revision. Commands without input are confirmed in words (WF-06's `ConfirmCommandDialog` with WF-08's wording and stale codes); a 412, invalid transition, final state, not-editable or already-evaluated refusal reads the request again with "It changed while you were working on it". Before the review the detail offers *Preview the classification*; after it, the recorded one is shown, pinned | TASK-060 D-3, D-4 |
| D-8 | **The authorisations** are listed with what each permits, the version it was approved against, Issued/Applied, and when, by whom and through which record it was applied, with a link to the screen that applies it (the schedule's baselines, the financials) | TASK-060 D-6, D-7 |
| D-9 | **Lists.** Most recently changed first (the API's order; across projects merged by `updatedAt`); a status filter offering the statuses present; the recorded band, or "Recorded at review"; the stated impacts in a line. Across projects, the visible projects (APPROVED_PLANNED to CLOSED) are read, then each one's requests, six at a time (`readInBatches`, F-5) | TASK-060 D-14 |

## 3. Screens and routes

| Screen | Route | Reads | Offers |
| --- | --- | --- | --- |
| SCR-105 (project) | `/projects/:id/change-requests` | `/change-requests?projectId` | Status filter, table; *Raise change request* to the Project Manager |
| SCR-105 (across) | `/change-requests` | `/projects?status=…`, then each project's requests | Status filter, table naming the project |
| SCR-106 | `/change-requests/new?projectId=`, `/change-requests/:id/edit` | `/projects/{id}` (optional), `/change-requests/{id}` (ETag), GOVERNANCE_PROFILE items | Save draft; Save and check materiality (`POST`/`PUT`, `preview-materiality`); Submit for review (`submit`, If-Match) |
| SCR-107 | `/change-requests/:id` | `/change-requests/{id}` (ETag), `/projects/{id}` (optional), `/approval-tasks`, `/approval-instances?subjectModule=ChangeRequest…` | The commands (D-6, D-7); MOD-040–042, MOD-044; preview |

## 4. Verification

Run 2026-10-07 on macOS with Docker Desktop: frontend in `AHDA-frontend` (Node 24); the API and migrate images rebuilt from this branch (backend identical to `dev` f7d6947).

| # | Command | Result |
| --- | --- | --- |
| 1 | `docker exec -w /repo/src/frontend AHDA-frontend npm run lint` | 0 problems |
| 2 | `npm run typecheck` | 0 errors |
| 3 | `npm run format:check` | All matched files use Prettier code style |
| 4 | `npx vitest run` | **661 passed, 43 files** (610 on `dev`; 51 new: `changeRequests.test.tsx` 20, `rules.test.ts` 31). Changed: the workspace's two tab-order assertions (`workspace.test.tsx`, `projects/rules.test.ts`) include the new tab. `workspace.test.tsx`'s SCR-043 documents test failed once in a partial parallel run and passed 3 of 3 alone and in every full run (the same flake seen in CI on PR #50) |
| 5 | `npm run build` | Built; the chunk-size warning is the one `dev` has |
| 6 | Criterion 1 in tests | `submit stays disabled until the server's classification of the saved request, with its approval path, is shown` (exact create body, no band; preview after save; submit with If-Match); `a change after classification disables submission until the request is classified again`; `a schedule change is not classified without its days, though its draft may be saved without them`; `when no classification can be computed, the reason is shown and submission stays disabled`; `a Project Manager who cannot read the project still raises and classifies it`; axe in Arabic |
| 7 | Criterion 2 in tests | `an approved request reads Approved for its approval and Not started for its implementation`; `an implemented request shows Approved and Implemented in two different styles and words` (classes, icons, axe); `during implementation it says how many authorisations are applied`; `in the list, Approved and Implemented requests read and look different`; rule tests: only APPROVED is drawn approved and only IMPLEMENTED implemented, for every status |
| 8 | Live, phase `main` (`artifacts/task-061/live`, git-ignored: `reset.sh` — TASK-060's fixture on a fresh `task061_live`, `AHDA-api-061` on 5091, PROJECT_VIEW added to local.r02's test grants; `prepare.py` — W baselined by local.r04 through WF-03, a cost change "Generator hire" approved through WF-11; `check.mjs` — a second Vite on 5179, Chromium) | **33 of 33.** local.r04, whose project read answers 404, opens SCR-106 by address; with a 12-day schedule change **Submit is disabled and no classification is shown**; *Save and check materiality* POSTs the draft (no band in the body) and then the preview; **"Band 2 of 3" and the band 2 path are shown and Submit is enabled**; changing to 9 days withdraws it and disables Submit, re-checking (PUT, preview) shows band 1; back to 12, band 2, submitted. local.r02 starts the review (band 2 recorded) and approves through the reused MOD-040; once the outbox delivers, **Approval: "Approved" (`badge--approved`, rgb(230,244,234) fill, 2px edge, tick) and Implementation: "Not started"**. local.r04 starts the implementation, applies the REBASELINE through WF-03 (200 ACTIVE) and marks it implemented: **Approved stays outlined on rgb(230,244,234), Implemented is solid rgb(21,91,47) with white text and a double tick**. SCR-105 (local.r02) shows "Rock at the intake" Implemented and "Generator hire" Approved in those two styles; the cross-project list names the project |
| 9 | Live, phase `a11y` | **48 of 48**: axe with colour contrast and no sideways scroll at 1366 and 390 px, English and Arabic, on both lists, SCR-107 approved and implemented, MOD-040 over SCR-107, and SCR-106 classified |

The first live run found two things: SCR-106 refused to open for local.r04 because it required the project read (fixed: D-1, and a test); and the materiality table's Band column was scrolled out of view in the form's width (fixed: Band is now the second column).

### 4.1 Mutation tests

Each mutation was applied, `src/features/change-requests` and `src/features/approvals` run, and the source restored with a fresh mtime (`artifacts/task-061/mutations/run.py`). 12 mutations, 12 killed.

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | Submit enabled before any classification | 4, among them the criterion 1 test |
| M-2 | An edit after classifying keeps submission enabled | `a change after classification disables submission…` |
| M-3 | The implementation badge's Implemented drawn in the approved style | 2 (rule; detail) |
| M-4 | The lifecycle IMPLEMENTED drawn like APPROVED | 3 (rule; detail; list) |
| M-5 | The approval dimension stops reading Approved once implemented | 2 |
| M-6 | The request body carries a band | 2 (exact bodies) |
| M-7 | The originator is offered a decision on their own change | `the originator is never offered a decision…` |
| M-8 | The raiser is offered Start review | `the raiser of a submitted request may withdraw it but not start its review` |
| M-9 | One icon for both badges | `an implemented request shows Approved and Implemented…` |
| M-10 | A schedule change is classified without its days | 2 |
| M-11 | Classifying does not save the edit first | `a change after classification…` |
| M-12 | The WF-11 decision offered without an inbox task | 4 |

## 5. Acceptance criteria, deliverables and checks

| Item | Result |
| --- | --- |
| The form surfaces the computed materiality classification before final submission so the requester understands the approval path they are entering | **MET.** D-2, D-3; §4 rows 6, 8; M-1, M-2, M-6, M-10, M-11 |
| The Detail screen clearly separates "Approved" from "Implemented" as distinct, non-interchangeable badges | **MET.** D-4; §4 rows 7, 8; M-3 to M-5, M-9 |
| Validation: submit a change request and confirm the classification is visible before submit is enabled | **MET** in Chromium against the real API (§4 row 8) |
| Validation: Approved and Implemented render as visually distinct statuses | **MET** in Chromium against the real API, computed styles compared (§4 row 8) |
| Description: SCR-105, SCR-106, SCR-107, reusing the WF-11 approval modals, classification shown before submission | **MET** (D-1, D-5); M-7, M-12 |
| Deliverables: React components/routes for SCR-105–107 | **MET** (header row "Deliverables") |

## 6. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **On the shipped grants a Project Manager cannot read their own project** (local.r04: `GET /projects/{W}` 404; PROJECT_VIEW is R04 at ENTITY, TASK-049). They raise and implement change requests through the API (CHANGE_REQUEST_* at OWN), so SCR-106 and SCR-107 work for them by address (D-1), but the workspace tab and the cross-project list never show them their project | PMO (Blueprint Appendix A); TASK-110 | A Project Manager finds their change requests only through a link |
| F-2 | **The reviewer cannot list projects either** (R03 ships no PROJECT_VIEW, TASK-058 F-1) and no notification tells them a request awaits review (TASK-060 F-10) | PMO; TASK-039 configuration | Start review is reached by address only |
| F-3 | **No request can be submitted from SCR-106 until AHDA publishes MATERIALITY_BAND bands** for the project's governance profile (TASK-060 F-3): the preview fails closed (422), and criterion 1 keeps Submit disabled until a classification is shown, so the form says why and keeps the draft. The API itself would accept the submission and fail at Start review | AHDA (OQ-013) | Drafts accumulate until the bands are published |
| F-4 | **The Project Manager cannot withdraw a request under review** (MOD-044 needs APPROVAL_VIEW, TASK-060 F-12); the button is shown only when the run can be read | PMO (Appendix A) | Withdrawal under review needs AHDA |
| F-5 | **The batched cross-project read is duplicated** in tasks, financial-kpi, milestones, risks and issues-challenges; this task added `readInBatches` to `shared/api/paging.ts` and uses it, without touching the others | Frontend | Five copies to keep in step |
| F-6 | **The specification's wider model is not shown** — Change Items, impact assessments, implementation actions, CR number, urgency (TASK-060 F-1) | PMO / BA | — |
| F-7 | **The WF-03 and WF-14 screens still have no authorisation picker** (TASK-060 F-11): SCR-107 links to them, but the authorisation id is still entered by hand there | A WF-03/WF-14 UI task | Rebaselines need the id copied from SCR-107 |
| F-8 | **The approval path names the band, not the approvers**: APPROVAL_AUTHORITY needs CONFIGURATION_VIEW, which a requester does not hold | AHDA | The requester learns who approves from the review once it starts |

## 7. Change log

| Date | Change |
| --- | --- |
| 2026-10-07 | Created (TASK-061) |

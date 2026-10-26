# Permission-Driven Navigation

| Field | Value |
| --- | --- |
| Source of the change | The prototype review of 24 Oct 2026: critical gap C3 ("the menus and buttons aren't driven by permissions"), finding 1 ("every role sees the same portals"), 1b (the Project Manager's Portfolio entry), M-9 (the "Administration hidden for this role" note), M-12 ("show only tabs the user may open") and L-3 (Retry on a permission error). No workbook row names it: branch `feat/review-permission-driven-navigation`, stacked on `fix/review-shipped-permission-matrix` |
| Record date | 2026-10-25 |
| Status | **BUILT AND VERIFIED LOCALLY.** The API reports what the caller holds; the shell offers every menu item, workspace tab and Create action on the permission it needs, and hides a menu left with none |
| Depends on | `shipped-permission-matrix.md`: the menus follow the grants, so they are only as right as the matrix |

## 1. Decisions

| # | Decision | Basis |
| --- | --- | --- |
| D-1 | **`GET /api/v1/sessions/current/permissions`** answers `{ permissions: [{ code, scopes[] }] }`: the caller's grants now, from the engine and after `GrantPolicy`, grouped by permission, the scopes in declared order. An inactive or unknown user holds nothing. Session-only (`[AllowAnyAuthenticatedUser]`): it reports the caller's own access and nobody else's | Review C3: "have the session (or a `/me` call) return the user's effective permissions with scope". Read on each sign-in rather than carried in the token, so a changed grant shows without a new token, as the engine's own checks do |
| D-2 | **`PermissionsProvider` and `useCan`.** The signed-in shell asks once per signed-in user; until the API answers nothing is held, so nothing is offered and then withdrawn | Build Pack Phase 0 item 9: "menu items appear only if `useCan`" (as the review quotes it) |
| D-3 | **Every navigation item declares the permissions it needs (any of them).** `menusFor` keeps an item only for a holder and drops a menu with no item left. The administration items follow their own permissions (`USER_VIEW`, `ROLE_VIEW`, `ROLE_ASSIGN`, `ORGANIZATION_VIEW`, the integration permissions, `DATA_IMPORT`) instead of the R01 role | Review: "give every menu item and action button the permission it requires; hide a menu when it has no visible items" |
| D-4 | **My Work is the doers'**: tasks need `TASK_UPDATE`, milestone claims `MILESTONE_SUBMIT`. The approval inbox and delegations need `APPROVAL_DECIDE`, the requests list `APPROVAL_VIEW`. Reports: the centre for an internal holder of `PROJECT_VIEW`, the explorer and saved views for `REPORT_COMPOSE`, the export history for `REPORT_EXPORT`; nothing for an external user (ADR-019) | The review's "What each role should see": R02, R03, R06 and R07 have no My Work; R06 no Approvals; R08 no Reports |
| D-5 | **The Portfolio menu is the portfolio audience's (R02, R03, R06, R07)**, the KPI register included; a Project Manager reads its KPIs in its projects | Review finding 1b: "the menu also gives the PM a Portfolio entry (the KPI register) they shouldn't have". A dashboard's audience is its roles (ADR-006), so this menu stays role-based |
| D-6 | **The audit trail sits under Administration** for a holder of an administrative audit category (administration, security, integration) **and under Projects** for one who reads business audit only | The matrix gives `AUDIT_BUSINESS_VIEW` to R02–R07 (review H4); under Administration it would bring back the Administration menu the review says R02 must not see |
| D-7 | **Workspace tabs need their read permission** as well as reach: Progress `PROGRESS_VIEW`, Schedule `SCHEDULE_VIEW`, Tasks `TASK_VIEW`, Milestones `MILESTONE_VIEW`, Financials `FINANCIAL_VIEW`, KPIs `KPI_VIEW`, Risks `RISK_VIEW`, Issues `CONCERN_VIEW`, Change requests `CHANGE_REQUEST_VIEW`, Suspension `SUSPENSION_VIEW`, Closeout `CLOSEOUT_VIEW`, External requests `EXTERNAL_REQUEST_VIEW`, Reviews `APPROVAL_VIEW`, Documents `DOCUMENT_VIEW` | Review M-12: "show only tabs the user may open". The grouping of the eighteen tabs into six is the review's next step and not done here (F-1) |
| D-8 | **Create follows `PROJECT_REGISTER` and `CHANGE_REQUEST_RAISE`**, item by item, and the button is gone for someone who may start neither; the register's "Create project" follows `PROJECT_REGISTER`; the import action follows `DATA_IMPORT` instead of the R01 role | Review C3: "the global '+ Create' button shows for Admin and R02, who hold neither" |
| D-9 | **No Retry on a refused permission.** `ErrorState` takes the failure; a 403 keeps its message and drops Retry, at all 123 call sites | Review L-3 |
| D-10 | **The "Administration hidden for this role" note is removed** (its key and style too) | Review M-9 |

## 2. Tests

| Concern | Test |
| --- | --- |
| The endpoint reports the grants held now, after `GrantPolicy`, and a changed grant on the next call | `AuthorizationEndpointTests.TheCurrentSessionsPermissionsAreTheGrantsHeldNow`; its session-only declaration in `EveryEndpointDeclaresItsServerSideProtection` |
| Each role is shown the menus its shipped permissions open, and no other (the review's role table) | `shell.test.tsx`: R01–R07 (`test.each`), the external entity user, the full-order case |
| Create offers only what the person may start | `shell.test.tsx` |
| The Project Manager has no Portfolio menu | `shell.test.tsx` |
| The approval screens follow the approval permissions | `approvals/access.test.tsx` |
| Report navigation follows compose and export | `reports.test.tsx` |
| A tab is offered only to a holder of its permission; the entity Project Manager and the entity contributor see their own sets | `projects/rules.test.ts`, `workspace.test.tsx` |

The test harness answers `GET /sessions/current/permissions` from the shipped matrix applied to the session's roles (`src/test/permissionFixtures.ts`, generated from `PermissionCatalogue`) unless a test registers its own answer, and keeps the read out of `requests` as it does the approval count. `entityManagerSession()` is ADR-013's entity Project Manager (R08 over the entity and R04 on the project), the schedule and milestone suites' actor.

## 3. Validation

| # | Check | Result |
| --- | --- | --- |
| 1 | `dotnet test PMPlatform.Tests.Integration --filter "…Integration.Identity.|…ContractTests|…OpenApi"`, with the OpenAPI snapshot regenerated (`UPDATE_OPENAPI_SNAPSHOT=1`) | 199 passed |
| 2 | `contract-check.py`, `api-documentation-check.py` | 0 findings; OK |
| 3 | Frontend, in the e2e runner: `tsc -b --noEmit`, `eslint src`, `vitest run` | Clean; 1,086 passed |

## 4. Findings

| # | Finding | Owner | Effect |
| --- | --- | --- | --- |
| F-1 | **The workspace still has up to eighteen tabs**, now only those the person may open. Grouping them into six (Overview, Plan, Performance, Control, Governance, Records), as review M-12 proposes, is a layout change of its own | Engineering (UX) | A manager still sees a long tab row |
| F-2 | **The administration routes keep their R01 guard** (TASK-032). A profile authored later that gives another role `USER_VIEW` (TASK-110) shows that role the menu item, and the route still refuses it | Engineering, with TASK-110 | None today: only R01 holds an administration permission |
| F-3 | **Buttons inside the feature screens still follow the API's refusals**, not `useCan`, apart from Create, the register's "Create project", the import action and the report composer's entries. The review names the global Create; a sweep of every in-page action is a follow-up | Engineering | A refused action is still answered by the API with its message |

## 5. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-10-25 | Created | Delivery team |

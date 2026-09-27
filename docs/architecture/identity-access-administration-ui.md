# Users, Roles and Permissions Administration (FG-03 Frontend)

| Field | Value |
| --- | --- |
| Task | TASK-032 — Build Users, Roles & Permissions Administration (FG-03 Frontend) (P5 - Identity & Access Management, FG-03) |
| Depends on | TASK-031 — the FG-03 API (`identity-access-administration.md`): every screen here reads and writes through its 31 operations, and every refusal shown is one the API returns |
| Record date | 2026-09-27 |
| Status | **BUILT AND VERIFIED LOCALLY** against a mocked API (component tests, Lighthouse). The live API was reached through the new dev proxy as R02 only: R01 cannot sign in locally because the compose stack has no MFA provider (TASK-029 F-5), so no R01 screen has been exercised against the running API (F-6) |
| Branch | `feat/task-032-fg03-identity-access-frontend` |
| Deliverables | React components and routes for ADM-002–013 and MOD-080–082 in `src/frontend/src/features/identity-access` (`users/`, `roles/`, `assignments/`, `organization/`, `routes.tsx`); i18n resource files `features/identity-access/i18n/{ar,en}.json`. The minimum application foundation the screens need and no task owns (D-1): `src/app` (shell, routes, styles), `src/shared` (HTTP client, i18n, UI primitives, `common` resource files) and `features/identity-access/session` (sign-in with MFA, step-up, R01 guard). 74 unit/component tests. This record |
| Environment variables / secrets | None in the Environment and Secrets sheet. `PMPLATFORM_API_ORIGIN` is a compose-only development variable read by Vite's Node process, never bundled (D-2) |
| Gate decision applied | None on the row |
| Participation amendments | **ADR-013**: the third user type (EXTERNAL) is created with its entity (ADM-004), and an external user's assignment is per project, within their own entity, with a named internal sponsor (MOD-081 → MOD-082); only external-eligible roles are offered (D-6) |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), Implementation Plan rows TASK-031, TASK-032 and every Frontend row, as read 2026-09-27; `identity-access-administration.md`; `api-conventions.md`; `mfa-privileged-access.md`; ADR-002, ADR-012, ADR-013 |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | ADM-002–013 and MOD-080–082, Arabic and English, responsive | This task |
| **In** | The application shell, sign-in with the second factor, step-up and the R01 route guard, at the minimum the screens need (D-1) | This task (F-1) |
| **Out** | Authoring and publishing permission profiles (ADM-008 "create") | TASK-110 |
| **Out** | A master-data picker for the entity type, a project picker | TASK-034, TASK-041 (F-2, F-3) |
| **Out** | The e2e suite and the platform-wide accessibility audit | TASK-085, TASK-087 (F-7) |
| **Out** | Content-Security-Policy and the other response headers | TASK-078 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **A minimal foundation is built here.** Before this task `src/frontend` rendered one `<main>`. No workbook row owns a router, i18n, an API client or the sign-in screens (SCR-001/002), and TASK-032 is the first Frontend row, so it adds them at the size the FG-03 screens need: `react-router` 8.4.0 (the one runtime dependency added), a typed i18n provider of about 100 lines instead of an i18n library, `fetch` with a small client, and native `<dialog>` modals. Dev-only: `@testing-library/user-event` 14.6.7, `axe-core` 4.13.0. All pinned exactly; `npm audit`: 0 vulnerabilities. | Without them no ADM screen is reachable. A hand-written i18n keeps the dependency surface small and makes a missing key a type error (`TranslationKey` is derived from `en.json`; `resources.test.ts` fails if Arabic lacks, adds or changes the placeholders of a key). |
| D-2 | **The SPA calls the API same-origin at `/api/v1`.** No `VITE_` variable names the API origin. In development Vite forwards `/api` to `PMPLATFORM_API_ORIGIN` (compose sets `http://api:8080`; the default `http://localhost:5080` serves a host-run Vite). | ADR-003 T-1 keeps configuration out of the bundle, and a same-origin API needs no CORS for the SPA. It settles the practical side of TASK-013 F-2 / TASK-014 F-9 without a sheet variable; deployed routing of `/api` is the load balancer's (F-5). |
| D-3 | **The session lives in `sessionStorage`**, tab-scoped and cleared when the tab closes; nothing is written to `localStorage` except the language. A 401 triggers one shared refresh; a failed refresh signs out. Sign-out is client-side: the API has no revoke endpoint. | Survives a reload without outliving the tab. Tokens readable by script are exposed to XSS; the mitigations are React's escaping, no `dangerouslySetInnerHTML`, and TASK-078's CSP. |
| D-4 | **The R01 guard is navigation, not protection.** `/admin/*` renders only for a session holding R01; without a session it redirects to sign-in, and for R02–R08 it shows "Access denied" and calls no API. Every call is still decided by the server (TASK-031 D-9). | The acceptance criterion names R01. The session exposes role codes, not permissions, so the guard reads the role (F-4). |
| D-5 | **Step-up is answered in place.** A 403 `STEP_UP_REQUIRED` (activate, disable, assign, end; TASK-031 D-11) opens a code dialog; after `POST /sessions/current/step-up` the request is sent again once, with the same `Idempotency-Key`. Cancelling shows the refusal. | The first attempt was refused before it ran, so the retry is the same logical request (R-36). |
| D-6 | **MOD-082 is the assignment's scope step (TASK-031 D-4, F-14).** The assignment dialog is MOD-081 (user, profile, PUBLISHED version, period) then MOD-082 (department, entity, project, sponsor). For an external user MOD-082 fixes the entity to their own, hides the department and requires a sponsor chosen among active internal users; MOD-081 lists only external-eligible roles. ADM-004 is where an external user's entity is set. A refusal naming a MOD-081 field returns the dialog to MOD-081. | An internal user's department is the directory's (ADR-007), and an assignment is never edited (TASK-031 D-5), so MOD-082 cannot be an edit dialog. |
| D-7 | **ADM-008 edits a role's bilingual name; ADM-009 is read-only.** The matrix is catalogue permissions × permission profiles, each cell the data scope of the profile's newest PUBLISHED version, or of a version chosen for one profile. | TASK-031 D-2: roles are canonical and profiles are authored by TASK-110. |
| D-8 | **Bilingual rendering.** Arabic until a language is chosen (the API's default `preferredLanguage`); the choice is kept per browser. `<html lang dir>` follows the language and the stylesheet uses logical properties only. Latin-script values (usernames, emails, codes, ids) are `dir="ltr"`; every interpolated value is bidi-isolated (FSI…PDI) and the Latin examples in Arabic hints are LRI…PDI. Dates are Gregorian with Latin digits in both languages. | A single stylesheet serves both directions. Without isolation a `+` or a UUID inside Arabic text reorders. The calendar and digits are a delivery-team choice for AHDA to confirm (F-9). |
| D-9 | **Forms validate the API's shapes first and show the API's field errors the same way.** Required, length, E.164, email, code and UUID checks mirror `RequestValidation.cs`; a 400/409/422 `errors[]` item lands on the input with the same field path. Each invalid input gets `aria-invalid` and `aria-describedby`, a form-level `role="alert"` counts the errors, focus moves to the first. The submit button reads "Saving…" and is disabled while a write runs. | Acceptance criterion 2. One path for client and server errors keeps them indistinguishable to the administrator. |

## 3. Screens and routes

All under `/admin`, behind the R01 guard (`features/identity-access/routes.tsx`).

| Screen | Route | Component |
| --- | --- | --- |
| ADM-002 User List | `/admin/users` (filters, sort and page in the query string) | `users/UserListPage.tsx` |
| ADM-003 User Detail, with the user's assignments | `/admin/users/:userId` | `users/UserDetailPage.tsx` |
| ADM-004 Create User | `/admin/users/new` | `users/UserFormPage.tsx` |
| ADM-005 Edit User (`If-Match`) | `/admin/users/:userId/edit` | `users/UserFormPage.tsx` |
| ADM-006 Roles | `/admin/roles` | `roles/RolePages.tsx` |
| ADM-007 Role Detail | `/admin/roles/:roleId` | `roles/RolePages.tsx` |
| ADM-008 Create-Edit Role (name) | `/admin/roles/:roleId/edit` | `roles/RolePages.tsx` |
| ADM-009 Permission Matrix | `/admin/permission-matrix?profileId=&versionId=` | `roles/PermissionMatrixPage.tsx` |
| ADM-010 Role Assignment | `/admin/role-assignments` | `assignments/RoleAssignmentPage.tsx` |
| ADM-011 Organization Structure | `/admin/organization` | `organization/OrganizationStructurePage.tsx` |
| ADM-012 Departments (create/edit dialog, activate/deactivate) | `/admin/organization/departments` | `organization/DepartmentListPage.tsx` |
| ADM-013 Entities (create/edit dialog, suspend/activate/retire) | `/admin/organization/entities` | `organization/ExternalEntityListPage.tsx` |
| MOD-080 Activate/Disable User | on ADM-003 | `users/UserStatusDialog.tsx` |
| MOD-081 Assign Role, MOD-082 Assign Department/Entity | on ADM-003 and ADM-010 | `assignments/AssignRoleDialog.tsx` |

Every list has a loading state, an error state with retry, and two empty states: none yet, and none matching the filters.

## 4. Verification

Run 2026-09-27 in `AHDA-frontend` (Node 24.21.0), macOS host, Docker Desktop.

| # | Command | Result |
| --- | --- | --- |
| 1 | `npm run lint` | 0 problems |
| 2 | `npm run format:check` | All matched files use Prettier code style |
| 3 | `npm run typecheck` | No errors |
| 4 | `npm test` | 8 files, 74 tests passed |
| 5 | `npm run build`; `NODE_ENV=production vite build` | Built; production bundle 425 KB JS (125 KB gzip), 10 KB CSS |
| 6 | `npm audit` | 0 vulnerabilities |
| 7 | Lighthouse 13.5.0, accessibility only, ADM-002 User List signed in as R01 against a throwaway mock API, headless Chromium 153 | **100** in English and in Arabic, desktop and mobile emulation; no failed audit |
| 8 | `docker compose up -d --wait api frontend`, then `POST /api/v1/sessions` and `GET /api/v1/users` through `localhost:5173` | `local.r02`: 201, then 403 from the API; `local.r01`: 503 (no MFA provider, TASK-029 F-5); `GET /admin/users` serves the SPA (200) |
| 9 | Screenshots in both languages at 1280 and 390 px wide; glyph positions measured in the Arabic mobile-number hint | Layout mirrored (navigation on the right, columns reversed, Latin values left-to-right); tables scroll inside their region, not the page; `+966501234567` reads left-to-right within the Arabic sentence |
| 10 | The `docs/architecture/*-check.py` gates of `ci-quality-gates.yml` | All OK |

The Lighthouse and screenshot tooling (Lighthouse, puppeteer-core, a headless Chromium, a Noto Arabic font and the mock API) was installed under `/tmp` in `AHDA-frontend` for the run and removed afterwards; none of it is in the repository.

The tests, by acceptance criterion and validation check:

| Criterion | Tests |
| --- | --- |
| 1. ADM-002–013 and MOD-080–082 implemented, reachable only for R01 | `screens.test.tsx`: each of the 12 ADM routes renders for R01. `access.test.tsx`: every administration route without a session goes to sign-in; R02–R08 are denied every route, shown no administration navigation and make no API call; sign-in with the second factor returns to the requested page. MOD-080 in `disableUser.test.tsx`, MOD-081/082 in `assignRole.test.tsx` |
| 2. Field-level validation errors, loading states while saving, empty states | `createUser.test.tsx`: required and malformed fields marked `aria-invalid`, described and focused, no request sent; a 409 `DUPLICATE` lands on the username; "Saving…" is shown and disabled while the write runs. `assignRole.test.tsx`: MOD-081 refuses to continue without a profile; a 422 on `startsAt` returns to MOD-081 and marks the field. `screens.test.tsx`: the four list empty states and the filtered one |
| 3. LTR (English) and RTL (Arabic) | `screens.test.tsx` renders every screen in both languages and checks `<html lang dir>`; `resources.test.ts`: identical keys and placeholders, isolation of interpolated values; row 9 above |
| 4. Lighthouse accessibility ≥ 90 on the User List | Row 7 above: 100 |
| Validation: axe-core on User List and Create User | axe-core 4.13.0 runs on every screen in both languages (`screens.test.tsx`), on Create User with errors shown (`createUser.test.tsx`) and on the open assignment dialog. Colour contrast is excluded in jsdom, which does not paint; Lighthouse covered it (row 7) |
| Validation: create-user, assign-role and disable-user flows | `createUser.test.tsx` (internal and ADR-013 external), `assignRole.test.tsx` (internal, external with sponsor and project, from ADM-003 and ADM-010, ending), `disableUser.test.tsx` (with `If-Match`, with step-up, self-administration refused) |
| ADR-013 | External user created with their entity; only profiles of external-eligible roles offered; entity fixed, no department, sponsor required and searched among internal users; request body checked |

### 4.1 Mutation tests: each protection fails its test when removed

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | The R01 guard lets any role in | 7 |
| M-2 | A text input loses `aria-invalid` | 4 |
| M-3 | Disable sent without `If-Match` | 1 |
| M-4 | `<html dir>` not set | 25 |
| M-5 | The step-up retry gets a new `Idempotency-Key` | 2 |
| M-6 | An external user is offered every role | 1 |
| M-7 | A text input loses its label | 25 |
| M-8 | The unfiltered empty user list shows the filtered message | 1 |
| M-9 | The saving state is never cleared | 1 |

Each source was restored and the full suite re-run clean (row 4).

## 5. Acceptance criteria, validation and amendment

| Item | Result |
| --- | --- |
| All ADM-002–013 screens and MOD-080–082 modals implemented and reachable only for R01 per RBAC | **MET** against the API contract (§3, §4). ADM-008 is a name edit and ADM-009 read-only, per TASK-031 D-2 (D-7). Not yet exercised as R01 against the running API (F-6) |
| Forms show field-level validation errors, loading states while saving, and empty states for zero-result lists | **MET** (D-9; §4 criterion 2) |
| Screens render correctly in LTR (English) and RTL (Arabic) | **MET** (D-8; §4 criterion 3, row 9). The Arabic strings need a bilingual reviewer's sign-off (F-9) |
| Lighthouse accessibility score ≥ 90 on the User List page | **MET**: 100, both languages, desktop and mobile (§4 row 7). Run manually, not in CI (F-7) |
| Validation: manually verify RTL with Arabic selected | **MET** (§4 row 9) |
| Validation: axe-core against User List and Create User | **MET** with no violation, colour contrast by Lighthouse instead (§4) |
| Validation: component/e2e tests for create-user, assign-role and disable-user | **MET** as component tests; no Playwright suite exists yet (TASK-085) |
| ADR-013 | **MET** (D-6) |

## 6. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **No task owns the application shell or the sign-in screens (SCR-001, SCR-002).** TASK-032 built the minimum (D-1): a header, a role-filtered navigation, directory sign-in with the second factor. SSO sign-in, the MFA enrolment QR code, session expiry warnings and the landing dashboards are not built | PMO Engagement Lead (assign a row); TASK-070 for dashboards | Every later frontend task either extends this shell without an owner or builds its own |
| F-2 | **The entity type is entered as a master-data id.** No FG-04 API lists `EXTERNAL_ENTITY_TYPE` items (TASK-034), so ADM-013 takes the item's UUID | TASK-034 | An administrator needs the id from the seed or a DBA |
| F-3 | **Projects are entered and shown as ids.** No Project API exists (TASK-041), so MOD-082 takes a project UUID and ADM-010 shows it | TASK-041, then this screen | An external project manager's grant needs the project id from elsewhere |
| F-4 | **The guard reads the R01 role code.** If Appendix A grants another role a view of users or structures (TASK-031 F-1), the UI needs the caller's effective permissions, which no endpoint returns | PMO (Appendix A); Identity (an endpoint) | A scoped administrator is sent to "Access denied" although the API would serve them |
| F-5 | **Deployed routing of `/api` is not configured.** Same-origin needs the load balancer to route `/api/*` to the API and the rest to the static SPA | TASK-016 (decision, closes TASK-013 F-2), TASK-018, TASK-021 | The deployed SPA cannot reach the API |
| F-6 | **No R01 screen has run against the live API.** Locally R01 cannot sign in without an MFA provider (TASK-029 F-5); the screens were verified against a mock that follows TASK-031's contract | DevOps (a local MFA provider, if wanted); SIT | A contract drift between mock and API would surface first in SIT |
| F-7 | **Lighthouse and the Playwright flows are not in CI.** axe runs in `npm test` at component level; Lighthouse was run by hand | TASK-085 (e2e), TASK-087 (audit) | An accessibility regression that needs a real browser is caught only at the next manual run |
| F-8 | **Names are looked up one user at a time.** Assignments and entities carry user ids; ADM-003, ADM-010 and ADM-013 read each distinct id (at most 25 a page, plus sponsors). A `/users?ids=` filter would make it one call | Identity (API) | Up to 50 small reads per page |
| F-9 | **Delivery-team Arabic and calendar choices.** The Arabic interface strings have not had a bilingual business review, and Gregorian dates with Latin digits (D-8) are not confirmed by AHDA | PMO / AHDA business reviewer | Terminology may differ from AHDA's usage |
| F-10 | **Security Lead review (CTL-43) cannot be requested** on this repository: the CODEOWNERS teams do not exist (TASK-031 F-13). This change adds sign-in, step-up and role gating in the SPA | Maintainer | The PR's CTL-43 box stays unticked until a Security Lead reviews it |

## 7. Change log

| Date | Change |
| --- | --- |
| 2026-09-27 | Created (TASK-032) |

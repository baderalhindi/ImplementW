# Name Lookups

| Field | Value |
| --- | --- |
| Source of the change | The prototype review of 24 Oct 2026, high-priority gap H1: "business users see raw IDs instead of names" — departments, Project Managers, risk categories and KPIs shown as "عنصر 86b92705", "المستخدم 00000000", because the lookups (`/users`, `/departments`, master data, `/kpi-definitions`) answered 403 to R02–R08. No workbook row names it: branch `feat/review-name-lookups`, stacked on `feat/review-permission-driven-navigation` |
| Record date | 2026-10-25 |
| Status | **BUILT AND VERIFIED LOCALLY** |
| Depends on | `shipped-permission-matrix.md`: every role now holds `MASTER_DATA_VIEW` (Appendix A, "Master data: V"), which names master data items and KPI definitions; this record covers the people and the organisation units, which stay the administrators' to manage |

## 1. Decisions

| # | Decision | Basis |
| --- | --- | --- |
| D-1 | **`GET /api/v1/display-names?userId=…`** (repeated, up to 100) answers `{ users: [{ id, displayName }] }` in the order asked, for any signed-in user: a name and nothing else of the person | Review H1: "add a read-only, names-only lookup endpoint for every signed-in user". Reports already name units server-side; the screens that list records by id need the same for people |
| D-2 | **`GET /api/v1/organization-units`** answers `{ departments, externalEntities }`, each `{ id, code, name, isActive }` by code: what screens name and forms offer. The project registration form's department and entity lists come from it, so R02, R03 and R04, who register projects since the matrix, are offered them | The administration lists need `ORGANIZATION_VIEW` (R01 only); without D-2 the form would offer nothing to those who may now register |
| D-3 | **An external user is told its own entity's names alone**: its own entity among the units and no department, and its own entity's people among the names — never AHDA's people or structure | ADR-013's isolation; AUD-CC-18 keeps AHDA's internal actors unnamed to an entity; the review's R08 walkthrough counts "users, departments and entities return 403" as isolation that holds |
| D-4 | **The SPA's lookups read these** — `useUserNames` (one call per hundred ids instead of one `/users/{id}` per person), `useProjectLookups`, `useDepartmentNames`, `useEntityNames` and the external request form — and keep their short-id fallback for an id the API does not name. The administration screens keep the administration lists | One change behind the hooks every screen already uses |

## 2. Tests

| Concern | Test |
| --- | --- |
| An internal user is told names and units; an inactive entity is flagged | `DisplayNamesTests.AnInternalUserIsToldTheNamesAScreenShowsAndTheUnitsAFormOffers` |
| An external user is told its own entity's names alone | `DisplayNamesTests.AnExternalUserIsToldItsOwnEntitysNamesAlone` |
| Over 100 ids, an empty id, no token | `DisplayNamesTests.MoreThanAHundredIdsOrAnEmptyIdIsRefusedAndNoTokenIsUnauthorized` |
| A screen names through the lookups, never the administration lists | `register.test.tsx` |

The test harness answers the two endpoints from the routes a test already mocks (`GET /users/{id}`, `GET /departments`, `GET /external-entities`), so every fixture that names a person or a unit names it on every screen.

## 3. Validation

| # | Check | Result |
| --- | --- | --- |
| 1 | `dotnet test PMPlatform.Tests.Integration --filter "…Integration.Identity.|…ContractTests|…OpenApi"`, the OpenAPI snapshot regenerated | 202 passed |
| 2 | `dotnet test PMPlatform.Tests.Unit` | 2,170 passed |
| 3 | `contract-check.py` | 0 findings |
| 4 | Frontend, in the e2e runner: `tsc -b --noEmit`, `eslint src`, `vitest run`; then `vitest run src/features/projects/register` after the register test was added | Clean; 1,086 passed; 13 passed |

## 4. Findings

| # | Finding | Owner | Effect |
| --- | --- | --- | --- |
| F-1 | **The project workspace's Overview still shows the governance profile, region and city as ids for a role whose catalogues fail to load** (review M-12). With `MASTER_DATA_VIEW` held by every role they resolve; a catalogue AHDA has not filled (regions, cities) still has nothing to name | AHDA (master data) | Until AHDA loads its catalogues, an unfilled one shows the short id |

## 5. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-10-25 | Created | Delivery team |

# Integration Administration UI (FG-05 Frontend)

| Field | Value |
| --- | --- |
| Task | TASK-076 — Build Integration Administration UI (FG-05 Frontend) (P13 - Technical Operations (FG-05/06)) |
| Depends on | TASK-075 — the FG-05 API (`integration-monitoring.md` §5): `/integrations`, `/integration-invocations`, `/integration-reconciliations`, `/sync-runs`, `/operational-alerts`. TASK-028/068 — ADM-041's `GET /identity-integration` and `POST /identity-integration/test` (`sso-directory-integration.md` D-10, `nafath-identity-verification.md` D-11) |
| Record date | 2026-10-14 |
| Status | **BUILT AND VERIFIED LOCALLY**: rule and component tests against a mocked API that enforces what the API enforces and plants secret-looking values (§4 rows 1–5), 12 mutation tests (§4.1), and **both workbook validation checks in Chromium against the real API** on a scratch database, local.r01 signed in through MFA (§4.2: 29 of 29 browser checks, 4 of 4 API checks). No backend change |
| Branch | `feat/task-076-fg05-integration-admin-frontend` |
| Deliverables | React routes and components in `src/frontend/src/features/integration-admin`: `CatalogueScreens` (ADM-040, ADM-044, ADM-045), `ProviderScreens` (ADM-041, ADM-042, ADM-043), `IntegrationDetailPage` (ADM-046), `IntegrationMonitoringPage` (ADM-047), `IntegrationLogsPage` (ADM-048 and the attempt detail); `components/TestConnectionDialog` (MOD-100), `ActivationDialog` (MOD-101), `HealthIndicators`, `IntegrationTable`, `AlertList`, `InvocationList`/`InvocationTable`, `SyncRunList`, `MetricsPanel`, `IdentityConfiguration`, `CataloguePage`, `Display`; `catalogue.ts`, `presentation.ts`, `problems.ts`, `useCatalogue.ts`, `api/`; the `integrationAdmin` i18n namespace and styles; `src/test/integrationAdminFixtures.ts`. This record |
| Environment variables / secrets | None |
| Gate decision applied | None in the workbook row |
| Sources read | The TASK-076 row as supplied on 2026-10-14, checked against the workbook (`AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`, Implementation Plan row 77: branch; validation: "Trigger Test Integration Connection and inspect network/console output to confirm no secret value is exposed; confirm ADM-047 renders Availability and Freshness as distinct indicators for a sample integration"). **FG-05 Integration Monitoring Functional Specification v1.0** §5, §8, §10–§13, §20, §28. **Blueprint v2.0** §17 and §17.1. **Step 16 Frontend Registers** rows ADM-040–048 and MOD-100/101 (routes, audiences, screen contracts). `integration-monitoring.md`, `sso-directory-integration.md`, `nafath-identity-verification.md`, `audit-activity-ui.md` |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | ADM-040 to ADM-048, MOD-100 Test Integration Connection, MOD-101 Enable / Disable Integration; the sidebar's *Integrations* group | This task |
| **Out** | A connection test for Exchange, SMS, MFA or an internal system: no API (F-1) | F-1 |
| **Out** | Contract versions on ADM-045, queues, dependency graph, maintenance windows, operational notes, reprocess and cancel, export: no API (TASK-075 F-11) | F-2 |
| **Out** | Editing endpoint or secret settings: they stay in the approved configuration and secret stores (FG-05 §8.4, TASK-028 D-10) | — |
| **Out** | ADM-057 Health / Operational Status | Not in the row |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Availability and Freshness are two indicators** (acceptance criterion 2). `AvailabilityIndicator` and `FreshnessIndicator` are separate components: each names its dimension, carries its own value word and has its own shape — a square chip with a signal icon for availability, a rounded dashed chip with a clock for freshness — with colour only repeating the word. Every table showing an instance (ADM-040–047) has an *Availability* and a *Freshness* column. ADM-047 opens with a legend stating what each means, and its summary counts *Availability down* and *Freshness stale* separately. No element combines them, and nothing is named "health" | Blueprint §17.1: availability is "distinct from freshness", "a reachable source may still be stale"; FG-05 §5.4 |
| D-2 | **MOD-100 never shows or logs a secret** (acceptance criterion 1). The dialog sends no body. It renders three fields of each result — outcome, time, failure code — never the response as a whole. A failure code is shown only if it is a plain upper-case code (`plainCode`); anything else reads "The connection failed". A refusal is translated from its code; its `title`/`detail` are never shown (the shared client never reads them). The feature writes nothing to the console (ESLint `no-console` forbids `log`; nothing here calls `warn` or `error`). ADM-041/043's configuration reference renders named fields only, with each secret as *Set* / *Not set* | The API already returns none (TASK-028 D-10); this keeps it that way if the API ever does |
| D-3 | **MOD-100 is the identity connection test.** `POST /identity-integration/test` tests the directory, SSO and Nafath together; the dialog is offered on ADM-041, ADM-043 and ADM-046 for `DIRECTORY`, `SSO`, `NAFATH`. No other integration has a test endpoint (F-1) | TASK-028 D-10, TASK-068 D-11 |
| D-4 | **Inside the R01 guard.** Step 16's audience for ADM-040–048 is "ADM"; R01 is the only role holding FG-05's five permissions and `IDENTITY_INTEGRATION_MANAGE` (TASK-075 D-16, TASK-030). The routes sit beside FG-03's in `/admin`'s R01 layout and the sidebar group shows to R01. The API checks each permission on every call: an R01 whose grant is withdrawn sees the permission message, never an empty list. A future non-R01 Integration Operator (TBC-INT-19) would need a permission listing such as FG-06's `/audit-access` (F-3) | FG-05 §12; Step 16 "screen audience is not authority" |
| D-5 | **Screen membership by code.** ADM-041: `DIRECTORY`, `SSO`, `MFA` (sign-in's identity runtime). ADM-042: `EXCHANGE_EMAIL` and `SMS` (ADR-004 monitors SMS beside Exchange), with ADR-004's measures. ADM-043: `NAFATH`, stating whether `Identity:Nafath:Enabled` is on ("if implemented"). ADM-044: every other registered integration, so an internal system registered later appears with no code change. ADM-045: the catalogue as contract references — direction, kind, instances, endpoint configuration key, activation | TASK-075 D-15; the registry has no internal/external attribute |
| D-6 | **Step 16's routes**, ADM-046 taking the integration id (`/admin/integration-detail-configuration/:integrationId`) and ADM-048 the attempt id (`/admin/integration-logs/:invocationId`), as ADM-051 takes its event id (TASK-074 D-3). MOD-100/101 are dialogs, not routes (Step 16: "UNRESOLVED — do not implement route behavior") | Step 16 02_Navigation_Routes |
| D-7 | **The backend decides every action.** MOD-101 is offered unless the integration is RETIRED, sends the detail's ETag as `If-Match` (412 is shown, nothing overwritten), and states that disabling stops alerts and FG-05's operations but not the integration, whose telemetry is still recorded. *Run reconciliation* appears only when the detail's `operations` lists `RECONCILE`; it states that nothing is changed and opens the attempt it started. *Retry now* appears only when the API answers `retryPermitted`; otherwise the page says why. An alert is acknowledged after confirmation; resolve and dismiss need a note (required in the form and by the API) | FG-05 §13.3, §20.1; TASK-075 D-7–D-9, D-15 |
| D-8 | **States are never invented.** UNKNOWN is neutral, never green. A measure over no sample reads *No sample*, never 0. A time absent reads *Never* or *Not evaluated yet*. A source that cannot be read is an error with *Try again*, never an empty list. The environment from the catalogue is shown on every screen | Blueprint "missing ≠ zero; Unknown ≠ Green"; FG-05 §13.1 |
| D-9 | **ADM-048 is allowlisted search.** Integration, state, operation code, correlation id, lineage key, business reference and a day range, in the URL; ids must be UUIDs and lengths within the API's limits before anything is sent; a set is sent comma-joined as the API parses it. An attempt shows its lineage oldest first and links to every attempt with the same correlation id | FG-05 §13.2; TASK-075 D-14 |
| D-10 | **Additions outside the feature**, backward compatible: `app/routes.tsx` mounts `integrationAdminRoutes` in the R01 layout; `AppLayout` adds the R01 *Integrations* group; `resources.ts` the `integrationAdmin` namespace; `styles.css` the indicator, legend, summary, environment and connection-result styles. Reused unchanged: `ConfirmDialog`, `Dialog`, `useSaveAction`, `Detail`, `ValueStateFlag` | — |

## 3. Screens

| Screen | Route | Component | Reads |
| --- | --- | --- | --- |
| ADM-040 Integrations Overview | `/admin/integrations-overview` | `IntegrationsOverviewPage` | `GET /integrations` |
| ADM-041 SSO / Directory | `/admin/sso-directory-integration` | `SsoDirectoryPage` + MOD-100 | `/integrations`, `/identity-integration` |
| ADM-042 Email / Exchange | `/admin/email-exchange-integration` | `EmailExchangePage` | `/integrations`, `/integrations/{id}` (measures) |
| ADM-043 Nafath | `/admin/nafath-integration` | `NafathPage` + MOD-100 | `/integrations`, `/identity-integration` |
| ADM-044 Internal-System Integrations | `/admin/internal-system-integrations` | `InternalSystemsPage` | `/integrations` |
| ADM-045 API Integration Catalogue | `/admin/api-integration-catalogue` | `ApiCataloguePage` | `/integrations` |
| ADM-046 Integration Detail / Configuration | `/admin/integration-detail-configuration/:integrationId` | `IntegrationDetailPage` + MOD-100, MOD-101 | `/integrations/{id}?from&to`, `/operational-alerts`, `/sync-runs` |
| ADM-047 Integration Monitoring | `/admin/integration-monitoring` | `IntegrationMonitoringPage` | `/integrations`, `/operational-alerts`, `/integration-invocations?status=DEAD_LETTER`, `/sync-runs` |
| ADM-048 Integration Logs | `/admin/integration-logs`, `…/:invocationId` | `IntegrationLogsPage`, `InvocationDetailPage` | `/integration-invocations`, `/integration-invocations/{id}` |

## 4. Verification

Run on 2026-10-14, macOS; frontend commands in the `AHDA-frontend` container (`/repo/src/frontend`).

| # | Command | Result |
| --- | --- | --- |
| 1 | `npm run typecheck` | 0 errors |
| 2 | `npm run lint` | 0 problems |
| 3 | `npm run format:check` | All matched files use Prettier code style |
| 4 | `npm test` | **1005 passed, 56 files** (956 on `dev`): +49 in `integration-admin` (`rules.test.ts` 7, `integrationAdmin.test.tsx` 42) |
| 5 | `npm run build` | Built (the existing chunk-size warning only) |

| Item | Tests (`integrationAdmin.test.tsx` unless named) |
| --- | --- |
| Criterion 1 | `MOD-100 sends no value, shows each outcome and code, and nothing of the secrets the response carries` (the mock adds a bind password, a client secret and a provider message to the response; DOM, markup and every console method checked), `a failure "code" that is a provider sentence is not shown…`, `a refused test is told by its code alone…` (the refusal's title and detail carry secrets), `ADM-041 and ADM-043 show each secret only as set or not set…`; `rules.test.ts` `plainCode` |
| Criterion 2 | `each integration has an availability and a freshness indicator, apart, named and in their own columns` (the directory UP and STALE at once; separate cells, headers, icons, classes; one of each per row; no `health` indicator), `the legend states what each means, and the summary counts each dimension on its own` |
| Screens | ADM-040–048 and the attempt detail render for R01 in English and Arabic with the environment shown and no axe violation (20 cases) |
| Access | R02 is denied every screen and no FG-05 endpoint is called; the sidebar group is R01's; a withdrawn grant (403) reads as such |
| Actions | MOD-101 (ETag in `If-Match`, Idempotency-Key, the detail read again; 412 shown; none for RETIRED), reconciliation (offered by `operations`, opens its attempt), measures window, alert acknowledge/resolve (note required, sent with its language), log filters (malformed id refused before any request; `status` and `correlationId` sent), retry (lineage, new attempt opened; none for an observed attempt) |

### 4.1 Mutation tests

`artifacts/task-076/mutations/run.py` (not committed) applied each mutation alone, ran the feature's tests judged by vitest's exit code, and restored and touched the file. **12 of 12 killed.**

| # | Mutation | Killed by |
| --- | --- | --- |
| M-1 | `plainCode` lets any text through | `keeps a fixed code and drops any other text…`, `a failure "code" that is a provider sentence is not shown…` |
| M-2 | The freshness indicator shows availability's value | `each integration has an availability and a freshness indicator…` |
| M-3 | One icon for both indicators | `each integration has an availability and a freshness indicator…` |
| M-4 | The test dialog renders the raw failure code | `a failure "code" that is a provider sentence is not shown…` |
| M-5 | The test logs its result to the console | `MOD-100 sends no value…`, `a failure "code" that is a provider sentence is not shown…` |
| M-6 | Activation sends no `If-Match` | `disabling says the integration keeps working, sends the ETag…` |
| M-7 | Sets sent as repeated parameters | `an open alert is acknowledged…`, `resolving needs a note…` |
| M-8 | A retired integration is offered activation | `a retired integration is offered no activation command` |
| M-9 | Retry offered whatever the API says | `an attempt another module made offers no retry, and says why` |
| M-10 | A resolution note is not required | `resolving needs a note…` |
| M-11 | The integrations group for every role | `the sidebar offers the integrations group to R01 only` |
| M-12 | A measure over no sample reads 0 | `a measure over no sample reads "No sample", never 0` |

### 4.2 Workbook validation checks on the local DEV stack

`artifacts/task-076/live` (not committed). A scratch database `task076_live` was migrated (through `TASK-075_GuardIntegrationMonitoringHistory`) and seeded by the compose seed. `AHDA-api-076` ran the `ahda-api` image with the compose environment, the MFA stand-in, a 60-second freshness window for `DIRECTORY` and a 5-second FG-05 pass. Its `/openapi/v1.json` equals `docs/api/openapi.v1.json` (309 paths, schemas equal). The image could not be rebuilt (F-4); no backend file has changed on `dev` since TASK-075's feature commit, from which it was built. local.r01 signed in through MFA in Chromium against a second Vite. The driver wrote the API container's values of `AD_BIND_DN`, `AD_BIND_PASSWORD`, `AD_LDAP_URL`, `JWT_SIGNING_KEY` and `MFA_PROVIDER_API_KEY` to a file the harness read and deleted after; none was printed.

| Check | Observed |
| --- | --- |
| **1 — Test Integration Connection** | On ADM-041, MOD-100 ran: directory *Connected* (a real bind to `AHDA-ldap`), SSO and Nafath *Not configured*. Network: one `POST /identity-integration/test` with no body; its response was outcomes, times and null failure codes. **None of the watched values appeared in any of 101 API responses, the page markup (dialog included) or 60 console lines over the whole session.** The API refuses both FG-05 and the test to local.r02 (403) |
| **2 — ADM-047's two indicators** | Sample integration `DIRECTORY` after its window lapsed: **Availability *Up* and Freshness *Stale* at once**, in separate cells under the *Availability* and *Freshness* headers, with different icons; every row has one of each (`MFA UP/UNKNOWN`, others `UNKNOWN/UNKNOWN`); the legend precedes the table; the FRESHNESS alert the pass raised was listed with its actions |
| Also | axe with colour contrast clean on ADM-040–048, MOD-100 with results, and ADM-047 in Arabic; MOD-101 disabled and re-enabled `SMS` through the API; ADM-047 right-to-left in Arabic and no sideways scroll at 390 px; local.r02 denied ADM-047 with no FG-05 request and no *Integrations* group |

SIT and UAT do not exist yet; this is the checks' procedure on DEV.

## 5. Acceptance criteria and validation checks

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | The Test Integration Connection action never displays or logs the underlying secret value in the UI or browser console | **MET** | D-2; §4 criterion 1 tests, M-1, M-4, M-5; §4.2 check 1 |
| 2 | ADM-047 clearly differentiates Availability from Freshness as two separate indicators per Blueprint §17.1, not a single combined health icon | **MET** | D-1; §4 criterion 2 tests, M-2, M-3; §4.2 check 2 |
| V | Trigger the test and inspect network/console output; confirm ADM-047's distinct indicators for a sample integration | **Done on the local DEV stack** | §4.2 |
| — | Deliverables: routes/components for ADM-040–048 and MOD-100–101 | **MET**, within the API that exists (F-1, F-2) | §3 |

## 6. Findings

| # | Finding | Owner | Action |
| --- | --- | --- | --- |
| F-1 | **Only the identity integrations have a connection test.** There is no test endpoint for Exchange, SMS, MFA or an internal system, so ADM-042, ADM-044 and ADM-046 offer MOD-100 only for `DIRECTORY`, `SSO`, `NAFATH`. The identity test also always tests all three together | FG-05 owner; TASK-092/TASK-103 | Add per-integration test operations (an FG-05 `CONNECTION_TEST` operation per definition); MOD-100 then takes the integration |
| F-2 | **ADM-045 and ADM-046 show what TASK-075 built.** Contract versions, queues, dependency impact, maintenance windows, operational notes, reprocess/cancel and export have no API (TASK-075 F-11); nothing is shown for them rather than a placeholder | FG-05 owner, AHDA | Specify with TASK-092 |
| F-3 | **FG-05 has no permission listing.** The screens follow R01 (D-4). If AHDA names a non-R01 Integration Operator (TBC-INT-19), the SPA needs a `GET /integration-access` like FG-06's `/audit-access` to open the screens and offer each action by permission | FG-05 owner | Add with the operator role |
| F-4 | **`dotnet restore` failed on a new advisory — fixed here.** From 2026-10-14 restore failed with NU1902/NU1903 (warnings as errors) on `System.Security.Cryptography.Pkcs` 10.0.0, which MimeKit 4.18.1 (MailKit) brings: CVE-2026-70162 and CVE-2026-84008 (GHSA-2fmv-rv2c-59x7, GHSA-846f-f764-76mx), .NET denial of service, versions 10.0.0–10.0.12. Every backend build and CI's two backend jobs failed. `Directory.Packages.props` now pins the patched 10.0.13 through central transitive pinning. Verified: restore, `dotnet build src/backend -warnaserror` 0 errors, unit tests 1885 passed, Notifications integration tests 38 passed. The advisories name the .NET runtime; the container base images take the patched runtime on their next rebuild | Backend / DevOps | Rebuild the images on the patched `aspnet:10.0` runtime |
| F-5 | **Alert detail is the API's English text.** `operational_alert.detail` is a single string (TASK-075 D-8), shown as written in both languages | FG-05 owner | A coded detail would let the UI translate it |

## 7. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-10-14 | F-4 fixed: `System.Security.Cryptography.Pkcs` pinned to 10.0.13, which unblocks restore locally and in CI. | Frontend (TASK-076) |
| 2026-10-14 | Initial record. ADM-040–048, MOD-100 and MOD-101 over TASK-075's API and ADM-041's identity-integration endpoints; two indicators; secret-safe connection test; 49 tests, 12 mutations, both workbook checks on DEV; 10 decisions, 5 findings. | Frontend (TASK-076) |

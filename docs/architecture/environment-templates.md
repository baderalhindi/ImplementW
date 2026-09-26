# Environment Variable Templates

| Field | Value |
| --- | --- |
| Task | TASK-013 — Author Environment Variable Templates per Environment (P2 - Project Setup & Foundation) |
| Depends on | TASK-011 — Initialize Monorepo & Solution Structure (`docs/architecture/monorepo-bootstrap.md`) |
| Record date | 2026-09-21 |
| Status | **BUILT.** Both templates match the Environment and Secrets sheet in both directions; no value in either; verified by `env-template-check.py` (§4) |
| Branch | `chore/task-013-env-templates` |
| Deliverables | `src/backend/PMPlatform.Api/appsettings.Template.json`; `src/frontend/.env.example`; `docs/architecture/environment-and-secrets.csv` (sheet snapshot); `docs/architecture/env-template-check.py`; `Program.cs` and `PMPlatform.Api.csproj` changes (§3.3); `CONTRIBUTING.md` "Local configuration"; this record |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), sheets Implementation Plan (TASK-013 row and every row's Environment Variables / Secrets Required cell), Environment and Secrets (43 rows), Architecture Decisions (ADR-004, ADR-007, ADR-010, ADR-013), as read 2026-09-21 |
| Controlled sources and authority order | per TASK-001 |

## 1. What this record is

TASK-013 names every environment variable and secret the platform needs across DEV, SIT, UAT and PROD, in two files a new developer copies and fills, without a single value being committed. The list is not a design choice — it is the Environment and Secrets sheet — so this record is about the three things that are choices: how the sheet's names map onto ASP.NET Core and Vite configuration (§3), how the copy is loaded so that "copy the template and fill local values" is literally sufficient (§3.3), and how drift between the sheet and the templates is caught (§4). §6 lists what the exercise surfaced about the sheet itself.

## 2. The gate

The Gate Decision cell says *"No SMS variables required (ADR-004, confirmed at the gate)"*; the Participation Amendment cell says *"ADR-004 CHANGED: SMS variables are now required … The earlier note that no SMS variables are needed is superseded."* The amendment is later and the Architecture Decisions sheet agrees (ADR-004: "Option A SUPERSEDED 19 Sep 2026 — In-App, Email AND SMS"), so the four `SMS_*` variables are in the template, sourced from TASK-103's row.

The other two gate notes are applied as written: the four `NAFATH_*` variables are listed and annotated as external-entity identity verification at onboarding only, never internal sign-in (ADR-007, ADR-013); the two `MFA_PROVIDER_*` variables are listed and annotated as possibly unnecessary if MFA comes from AHDA's identity provider, to be confirmed at environment setup (ADR-010's step-up trigger list is still outstanding). A template lists what *may* be required; removing a row is a sheet edit first, and the check in §4 then fails until the template follows.

## 3. What was built

### 3.1 Rules

| Rule | Decision | Why |
| --- | --- | --- |
| **R-1** Key = sheet name | `appsettings.Template.json` is flat: its 43 keys are the 43 sheet names, verbatim (`"DB_CONNECTION_STRING": ""`), in sheet order, grouped by comment. No `ConnectionStrings:Default`, no `Database:ConnectionString`. | ASP.NET Core's environment-variable provider maps `DB_CONNECTION_STRING` to configuration key `DB_CONNECTION_STRING` unchanged. One name in the sheet, the secret store, the container environment, the file and `configuration["DB_CONNECTION_STRING"]` — nothing to translate, nothing to get out of step, and the sheet-versus-template diff is a set comparison. |
| **R-2** Names only | Every value is `""` in the backend template and empty in the frontend one. Not even a local default (`localhost`, `5432`), not even a format hint that a scanner could read as a credential. | TASK-013 acceptance criterion 2 and CTL-18 ("environment templates list names only"). The check in §4 fails on any non-empty value. |
| **R-3** The backend template is the whole inventory | All 43 variables are in `appsettings.Template.json`, including the two the API never reads (`DEPLOY_SERVICE_ACCOUNT_KEY`, `CONTAINER_REGISTRY_TOKEN`, CI/CD platform secret store only) — their comment says so. | The criterion is "every env var listed in the sheet appears in the template files". One file that *is* the sheet is simpler to diff and to onboard from than an inventory split across the repo. |
| **R-4** The frontend template holds Public rows only, as `VITE_<NAME>` | Two variables: `VITE_APP_BASE_URL`, `VITE_APM_ENVIRONMENT_TAG`. The prefix marks the same sheet row; the check strips it. (Five until TASK-028 removed the three `VITE_SSO_OIDC_*` rows: §6 F-3.) | Everything in a Vite bundle is public once built (ADR-003 T-1); `vite.config.ts` already exposes only `VITE_`. A Secret-classified row in `.env.example` would be a design error, and the check treats it as one. |
| **R-5** Every line is annotated | Each variable carries `Classification | Environment scope | Owner — Purpose` from the sheet; each group names the task that consumes it (from the Implementation Plan's Environment Variables cells). | A developer copying the template needs to know which of the 43 to fill for DEV (14 of them) and who to ask for the rest. The annotations are comments, so they cannot leak into configuration. |
| **R-6** The sheet is snapshotted, with no values, beside the check | `environment-and-secrets.csv` is the sheet's eight columns for 43 rows. | The check must be reproducible without workbook access (same reason `ptbc-themes.csv` and `erd-check.py` exist). The sheet holds no values, so the snapshot holds none. When the sheet changes, the CSV is re-exported and the check reports what the templates now lack. |

### 3.2 Which variables a developer fills for DEV

Fourteen rows carry DEV in their scope: `DB_CONNECTION_STRING`, `JWT_SIGNING_KEY`, `SSO_OIDC_CLIENT_ID` (test tenant), `SSO_OIDC_CLIENT_SECRET`, `SSO_OIDC_AUTHORITY`, `SSO_OIDC_CALLBACK_URL`, `DOCUMENT_STORAGE_CONNECTION_STRING`, `SECRET_STORE_ENDPOINT`, `SECRET_STORE_AUTH_TOKEN`, `CORS_ALLOWED_ORIGINS`, `APP_BASE_URL`, `LOG_LEVEL`, `APM_ENVIRONMENT_TAG` and, for anyone running the E2E suite, `E2E_TEST_USER_CREDENTIALS_SECRET_REF` (DEV/SIT). `CONTAINER_REGISTRY_TOKEN` is "All (CI/CD)" — pipeline, not developer. TASK-014's docker-compose supplies the local `DB_CONNECTION_STRING` and a local-only `JWT_SIGNING_KEY`; the remaining twelve are empty until the corresponding task exists, and the API starts without them (verified, §4).

### 3.3 How the copy is loaded

| Change | File | What it does |
| --- | --- | --- |
| Local override source | `PMPlatform.Api/Program.cs` | Inserts `appsettings.{Environment}.Local.json` (optional, reload on change) into the configuration sources **directly before the environment-variable provider**, so the order is `appsettings.json` → `appsettings.{Env}.json` → `appsettings.{Env}.Local.json` → environment variables → command line. `AddJsonFile` after `CreateBuilder` would have appended it *after* environment variables, letting a developer's file silently beat an injected value; that was built first, caught by the run in §4, and replaced. |
| Publish exclusion | `PMPlatform.Api.csproj` | `appsettings.Template.json` and `appsettings.*.Local.json` are `CopyToPublishDirectory="Never"`. The template is documentation; a Local.json is a secret. Verified: `dotnet publish` output holds `appsettings.json` and `appsettings.Development.json` only. |
| Git ignore | `.gitignore` (TASK-011, unchanged) | `appsettings.*.Local.json`, `.env.*`, `*.local` are ignored; `.env.example` is un-ignored. Verified with `git check-ignore`. |
| Onboarding text | `CONTRIBUTING.md` | The two `cp` commands, the precedence rule and the check command. |

## 4. Verification

| # | Check | Result |
| --- | --- | --- |
| 1 | `python3 docs/architecture/env-template-check.py` — every sheet name in the backend template; every backend key in the sheet; every frontend key is `VITE_` + a Public sheet name; no value anywhere | `OK: 43 sheet variables; 43 in appsettings.Template.json; 5 public variables in .env.example; no values in either template.` |
| 2 | Mutation test of the check: a value in the backend, an unknown backend key, a deleted sheet variable, a Secret row in the frontend, a frontend value, an unprefixed frontend key | All six reported individually, exit code 1; templates restored, check green |
| 3 | `dotnet build src/backend -warnaserror`; `dotnet test src/backend` | 0 warnings, 0 errors; 25 passed (architecture tests A-1 to A-6 included) |
| 4 | Onboarding path: `cp appsettings.Template.json appsettings.Development.Local.json`, set `LOG_LEVEL` to `Debug`, run the API, read `configuration["LOG_LEVEL"]` through a temporary probe endpoint (removed afterwards) | `Debug` — the Local.json is read. With `LOG_LEVEL=Warning` in the environment: `Warning` — the environment variable wins. With no Local.json: `(unset)` and `/health` 200 — the file is optional |
| 5 | `dotnet publish -c Release` with a Local.json present | Output contains `appsettings.json`, `appsettings.Development.json`; no template, no Local.json |
| 6 | `gitleaks detect --no-git` over this task's six files | `no leaks found` |
| 7 | `gitleaks detect --no-git` over the whole working tree | 5 findings, all pre-existing and all the same false positive: example `idempotencyKey` UUIDs in TASK-009/TASK-010 sample files (`api-conventions-samples.openapi.json`, `event-samples/*.json`). Recorded for TASK-080's allowlist (§6 F-6) |

The frontend template does not participate in `npm run build` (Vite reads `.env`, `.env.local` and mode files, never `.env.example`), so the frontend CI job is unaffected; no Node runtime was available in this environment to re-run it, and no frontend source changed.

## 5. Acceptance criteria

| # | Criterion | Status |
| --- | --- | --- |
| 1 | Every env var listed in the Environment and Secrets sheet appears in the template files | **MET** — 43 of 43 in `appsettings.Template.json` (check 1), plus the 5 Public rows a bundle may hold in `.env.example` |
| 2 | The templates contain no real secret values (verified by secret-scan, P14) | **MET on this task's own scan** (checks 1, 6: no value of any kind, so no secret value); the CI gate itself is TASK-080 |
| 3 | Onboarding a new developer requires copying the template and filling local-only values | **MET** — two `cp` commands (CONTRIBUTING "Local configuration"); the copy is git-ignored, publish-excluded and loaded with the right precedence (check 4, 5) |
| — | Validation cell: diff the template files against the sheet programmatically; fail if any variable is missing from either side | **MET** — `env-template-check.py`, both directions, exit code 1 (checks 1, 2) |

## 6. Findings and open items

| ID | Finding | Owner / where it goes |
| --- | --- | --- |
| **F-1** | Three sheet variables are declared in no task row's Environment Variables cell: `DOCUMENT_STORAGE_CONNECTION_STRING`, `MALWARE_SCAN_API_KEY` (TASK-037's cell reads "None", although `cybersecurity-control-matrix.md` CTL-20 assigns both to TASK-037) and `LOG_LEVEL`. The template annotates them from the control matrix. | Workbook edit: TASK-037's cell should name the two document variables; `LOG_LEVEL` belongs to TASK-016 or TASK-090. PMO |
| **F-2** | The sheet has no variable telling the SPA where the API is. `CORS_ALLOWED_ORIGINS` ("frontend origins permitted to call the API") presumes the SPA and API can be on different origins, yet no Public row names the API origin for the frontend. Not invented here — the check would (correctly) reject a template key the sheet lacks. | Decide at TASK-016: either the SPA is served from the API origin (then say so, and CORS is a same-origin no-op) or add a Public row `API_BASE_URL` to the sheet; the template follows. Engagement Architect |
| **F-3** | `VITE_SSO_OIDC_AUTHORITY`, `VITE_SSO_OIDC_CLIENT_ID`, `VITE_SSO_OIDC_CALLBACK_URL` are in `.env.example` on the assumption the SPA may initiate the OIDC redirect. If TASK-028 makes the API initiate it (`JWT_SIGNING_KEY` — the platform issues its own session tokens — is compatible with either), the three are removed from the frontend template; the backend template is unaffected. **CLOSED by TASK-028:** the API initiates the redirect (`sso-directory-integration.md` D-4), and the three rows are removed. | TASK-028 |
| **F-4** | `MFA_PROVIDER_API_KEY`, `MFA_PROVIDER_ENDPOINT` stay listed under the gate note "may be unnecessary if MFA comes from AHDA's identity provider". If confirmed unnecessary, the sheet rows are removed first (TASK-001 §4: revisions are re-issued), then the template. | TASK-029; AHDA IT (Identity) |
| **F-5** | `VITE_` values are fixed at build time. The sheet's "distinct value per environment" for `APP_BASE_URL` therefore means one frontend build per environment, or a runtime configuration injection that TASK-016/TASK-018 would have to design. Until decided, `.env.example` documents build-time behaviour. | TASK-016, TASK-018 |
| **F-6** | Repository-wide `gitleaks` reports five pre-existing false positives (example `idempotencyKey` UUIDs in documentation samples). They are not secrets; they will fail TASK-080's gate unless allowlisted or the samples use an obviously synthetic pattern. | TASK-080 |
| **F-7** | `env-template-check.py` is run by hand. Wiring it into CI (alongside `erd-check.py` and `contract-check.py`) is TASK-015's CI-gate scope, not this task's. | TASK-015 |
| **F-8** | The sheet's `EXCHANGE_SMTP_PORT` is a number and `CORS_ALLOWED_ORIGINS` a list; the template types nothing (every value is `""`). Binding types are the consuming task's (`TASK-039`, `TASK-078`) — the flat string key is what the environment supplies either way. | TASK-039, TASK-078 |

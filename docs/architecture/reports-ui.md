# Reports Center UI (FG-02 Frontend)

| Field | Value |
| --- | --- |
| Task | TASK-072 — Build Reports Center UI (FG-02 Frontend) (P12 - Management Intelligence (FG-01/02)) |
| Depends on | TASK-071 — the FG-02 API (`reports.md` §5–§7): the role-aware catalogue of ten reports, each report's columns, parameters with authorised options, formats, `mayExport` and `maySaveView`; runs with every cell's freshness, as-of and unknown reason and each section's R-20(c) metadata and coverage; the SCR-138 explorer over the PUBLISHED `REPORT_RULES` allowlist; report jobs authorised again at download; private saved views. TASK-070 — the value-state flags, semantic-state badges and value vocabularies this reuses |
| Record date | 2026-10-11 |
| Status | **BUILT AND VERIFIED LOCALLY**: component and rule tests against a mocked API (§4 row 4), the backend's Reports integration suite (§4 row 7), 17 mutation tests (§4.1), and **both workbook validation checks in Chromium against this branch's API** on a scratch database, signed in for real as local.r02, local.r03 and local.r07 (§4 row 10: 41 of 41). **In a real environment** the explorer is closed until AHDA publishes a `REPORT_RULES` version, and AHDA's roles export nothing until Blueprint Appendix A (F-3, F-4) |
| Branch | `feat/task-072-fg02-reports-center-frontend` |
| Deliverables | React routes and components for SCR-130 to SCR-140 and MOD-060 to MOD-062 in `src/frontend/src/features/reports`: `ReportsCenterPage` (SCR-130), `ReportFamilyPage` (SCR-131–137), `ReportPage` (the report shell of SCR-131–137), `ExplorerPage` (SCR-138), `SavedViewsPage` (SCR-139), `ExportHistoryPage` (SCR-140); `dialogs/ExportDialog` (MOD-060), `dialogs/ColumnsDialog` (MOD-061), `dialogs/SaveViewDialog` (MOD-062); `components/` (result grid, cell, coverage banner, report list, saved-view notice, job download); `api/`, `composition.ts`, `presentation.ts`, `problems.ts`, `access.ts`, `useSavedView.ts`, `routes.tsx`; i18n `features/reports/i18n/{ar,en}.json`. Shared: `shared/api/saveFile.ts` (moved out of WF-12's `DownloadButton`). **Backend, by decision with the user (D-3)**: saved-view sharing — `SavedViewShare`, migration `TASK-072_ShareSavedViews`, `sharedWithRoleCodes` on `/saved-views`; a job's requester on `/report-jobs`. 45 frontend tests, 3 integration tests changed or added. This record |
| Environment variables / secrets | None |
| Gate decision applied | **"FIXED at 10 reports (ADR-006). Bilingual labels; free-text content renders in the language entered (ADR-012)."** — the UI knows the ten reports of the API and presents each in the experience its definition names; SCR-131–137 are ways in, never more reports (D-1, D-2); every word in Arabic and English, report and field labels read from the API in the interface language, and every free text — a saved view's name, a project's title — in the language and direction it was entered in (D-10) |
| Participation amendment | **ADR-013**: an entity's person sees only the entity report set the API offers, its financial amounts *Restricted*, and no builder or saved views (D-6). **ADR-006**: report parameters and modes are the API's parameters and options, shown as it maps them (D-4). **ADR-012**: narrative is shown in its entry language and marked when it differs from the interface language (D-10). **ADR-019**: MOD-061 columns and filters, MOD-062 save view, SCR-139 saved views for R02, R03 and R07. **Shared saved views enabled (TBC-RPT-20) — configuration transfers, authorisation does not** (D-3) |
| Sources read | The TASK-072 row as supplied on 2026-10-11, checked against the workbook's Implementation Plan row (branch, validation checks: "Confirm the Report Builder's field picker only shows allowlisted options by comparing it against the backend configuration; verify a saved view created by one user is not visible to another user unless explicitly shared"). **FG-02 Reports, Filters & Export Functional Specification v1.0** §5.2 (canonical inventory), §10.1, §10.2, §11.3, §13.1–§13.7, TBC-RPT-20, Appendix A (`Project Files.zip`). **Step 16 Frontend Registers** sheet 01 (SCR-130–140, MOD-060–062), the proposed routes sheet, sheet 03 (FE-CMP-002, -004, -008, -010, -012, -014). `reports.md` (TASK-071) whole; `dashboards-ui.md` D-3, D-4, D-11; `erd.md` §5.20 |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | SCR-130 Reports Center; SCR-131–137 as the seven experiences and the shared report shell; SCR-138 controlled explorer; SCR-139 saved views; SCR-140 export history; MOD-060 export, MOD-061 columns and filters, MOD-062 save view | This task |
| **In** | Saved views shared with roles (TBC-RPT-20 as the amendment enables it), backend and UI | This task (D-3) |
| **In** | The requester of each job on the API, for SCR-140 | This task (D-7) |
| **Out** | Scheduled reports, report snapshots retained in WF-12, an operational cross-user export view, ADM-037 report configuration screens | `reports.md` F-7; F-5 |
| **Out** | Which roles may export, and the REPORT_RULES allowlist AHDA publishes | Blueprint Appendix A; AHDA on FG-04 (F-3, F-4) |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Routes under `/reports`.** `/reports` SCR-130; `/reports/{executive,portfolio,department,project,financial,progress,risk-issue}` SCR-131–137; `/reports/{experience}/{REPORT_CODE}` the report shell; `/reports/explorer` SCR-138; `/reports/saved-views` SCR-139; `/reports/exports` SCR-140. Step 16's `/views/scr-13n/…` are marked PROPOSED and every earlier feature names its routes by what they show, so these do the same (F-6). MOD-060–062 are dialogs of their host screens with no route ("authorized host binding required"). A report opened under another experience's address is moved to its own; a name that is not one of the ten, or a report outside the person's audience, is *Report not found* (R-47) | Step 16 sheets 01 and the routes sheet; FG-02 §13 |
| D-2 | **SCR-131–137 are the experiences, not reports.** Each report's definition names the experience presenting it (`audienceFamily`); the experience page lists the person's catalogue entries of that family, and each opens the one report shell of FG-02 §13.2 — header, Parameter Panel, Active Filter Summary, Coverage/Freshness Banner, Results Grid, Column Selector, sort, Export, Save View. The catalogue the API returns is the only list of reports; the UI adds none (gate decision) | ADR-006; FG-02 §5.2, §13.2 |
| D-3 | **Shared saved views, built here.** The amendment enables TBC-RPT-20 and the workbook's check needs "unless explicitly shared", while TASK-071 left sharing to "TASK-112", a row the architecture maps back to TASK-071/072 (`solution-architecture.md` §11.5). Asked on 2026-10-11, the user chose to build it in this task. **Backend**: `reports.saved_view_share` (one row per view and role, migration `TASK-072_ShareSavedViews` with the view-parts guard); `sharedWithRoleCodes` optional on `POST`/`PUT /saved-views` (absent or empty keeps a view private; adding it is not a breaking change), validated to ADR-019's saved-view roles R02, R03, R07, each once (422 `REPORT_SAVED_VIEW_NOT_ALLOWED` otherwise); a view is listed, read and run by its owner **and** by the holders of a role it is shared with, never by an external entity's person; only its owner changes or deletes it (another's `PUT` 404, `DELETE` 204 with nothing removed); the owner alone reads whom it is shared with; every run is the reader's own (the existing run path already ran as the caller, so nothing of the owner's access is reused). Recipients are roles because a non-administrator can list neither users nor departments to pick from; user and department recipients are not built (F-2). The audit events carry the roles shared with. **UI**: MOD-062 offers the three roles as checkboxes, none ticked; SCR-139 lists own and shared views with owner, sharing and fit, offers *Change sharing* and *Delete* to the owner only; a shared view opens with "shared with you by …" and "It runs with your own access", and can be saved as the reader's own new view, never changed | Amendment; TBC-RPT-20; ADR-019; FG-02 §10.2 |
| D-4 | **The report shell is the API's report.** Parameters are selects of the options `GET /reports/{code}` authorised (departments by name, projects by ID and title, OPTION values with their labels); a required one must be chosen before anything runs, an optional one defaults to *All*. Columns and their order are chosen in MOD-061 among the report's own published columns (its default columns first), with one sort among those of a sortable type; a report has no free filters — its parameters are its filters. The Active Filter Summary repeats what was run. Export appears only when `mayExport`, saving only when `maySaveView` and the report allows views | FG-02 §6, §13.2; ADR-006 |
| D-5 | **SCR-138 is the allowlist and nothing else** (acceptance criterion 1). The field picker is built from `GET /report-allowlist-entries` and only from it; the header names the REPORT_RULES version and the count. Every field is a checkbox; a filter's field is a select of the filterable entries, its condition a select of that entry's operators, its value a select of the entry's closed values (checkboxes for *is one of*), or a typed number, date or text value for an open field — two inputs for *is between*, commas stripped so a value cannot become two. There is no text area, no query, join or formula box anywhere. Requests carry the two codes the API gave each field; a field, filter or sort that is not offered is dropped before sending (`composition.ts`). While no REPORT_RULES version is published the page says the builder is closed (422 `CONFIGURATION_MISSING`); without `REPORT_COMPOSE` it says whom it is for (403) | FG-02 §11.3, §13.3; BR-RPT-045/046 |
| D-6 | **Missing is never zero, stale never current** (FE-CMP-008). Each section of a result has a banner: which columns it fills, its semantic state (*Current · live*, *Published · official*, *Historical · snapshot*), *Stale* or its unknown reason, its as-of, source and projection version, and "2 of 3 projects shown · 1 left out · 1 with stale values". Each column header names its semantic state. A cell with no value is its reason as a flag — *No data*, *Not applicable*, *Restricted*, *Source unavailable* — never blank or 0; a masked cell is *Restricted* whatever it carries; a STALE value keeps its value with "Stale · as of …". "Run at …" says when the report ran, not when its sources were current. Codes read in their source's words through the dashboards' vocabularies, amounts as SAR grouped to two places, Latin digits in both languages | FG-02 §7, BR-RPT-019/020/030; `dashboards-ui.md` D-3–D-5 |
| D-7 | **SCR-140 says who generated each output and when** (acceptance criterion 2). The API's job detail and summary now carry `requestedByUserId` (and the summary the FAILED job's `failureCode`); the history shows *Generated by* — the signed-in person by name with "(you)", anyone else through the directory or by short id — *Requested*, *Generated* (the job's completion, or *Not generated*), status, format, classification, *Available until*, the failure in words, *Download* for a completed job and *Cancel* until it runs. The list is the person's own jobs, as the API returns them; it reads itself again every 5 s while a job is running | FG-02 §13.5; workbook acceptance criterion |
| D-8 | **MOD-060 is a job, followed to its file.** PDF, XLSX or CSV (the formats the API lists) in Arabic or English; a warning that the file will be classified Sensitive when a shown column is sensitive; the job's `Idempotency-Key` from the client. The dialog reads the job every 2 s until it ends, then offers *Download* (authorised again: a requester who may no longer see what it holds reads "You may no longer see some of what this file holds") or says why it failed, and links to SCR-140 | FG-02 §9, §13.6; FE-CMP-010; `reports.md` D-7, D-9 |
| D-9 | **Navigation is by role, protection is the API's.** The Reports group offers the Reports Center and Export history to everyone signed in, and the builder and saved views to internal holders of R02, R03 or R07 (`REPORT_COMPOSE`'s shipped holders); the session carries roles, not permissions | ADR-019; `dashboards-ui.md` F-6 |
| D-10 | **Bilingual, narrative as entered** (ADR-012). Every interface word is in `reports` `{ar,en}.json`; report names, descriptions, column, parameter and option labels and allowlist labels come from the API in both languages and are read in the interface language. A saved view's name is saved with the language its author chose and rendered with that `lang` and `dir="auto"`; SCR-139 marks a name entered in the other language ("Entered in Arabic"); free-text cells (titles) keep their own direction | ADR-012; FG-02 §13.7 |
| D-11 | **Additions to the foundation**, backward compatible: `shared/api/saveFile.ts` (WF-12's `DownloadButton` now imports it, unchanged in behaviour); the `reports` i18n namespace; the report styles; `src/test/reportFixtures.ts` | — |

## 3. Screens

| Screen | Route | Component |
| --- | --- | --- |
| SCR-130 Reports Center | `/reports` (search `?q=`, experience `?family=`) | `ReportsCenterPage` |
| SCR-131–137 experiences | `/reports/{executive…risk-issue}` | `ReportFamilyPage` |
| SCR-131–137 report shell | `/reports/{experience}/{REPORT_CODE}` (`?view=` opens a saved view) | `ReportPage` |
| SCR-138 Custom Report / Filter Builder | `/reports/explorer` (`?view=`) | `ExplorerPage` |
| SCR-139 Saved Reports / Views | `/reports/saved-views` | `SavedViewsPage` |
| SCR-140 Export History | `/reports/exports` | `ExportHistoryPage` |
| MOD-060 / MOD-061 / MOD-062 | dialogs of the report shell, the builder and SCR-139 | `ExportDialog`, `ColumnsDialog`, `SaveViewDialog` |

| State | When | What is shown |
| --- | --- | --- |
| No report | Empty catalogue (R01) | "No report is published for your roles." |
| Required parameter | A report about one project | "Choose the required parameters, then run the report." — nothing runs |
| Builder closed | 422 `CONFIGURATION_MISSING` | "The custom report builder is not available yet" and why |
| View not available | `?view=` of a view neither the person's nor shared with them (404) | "This report, view or export does not exist or is not available to you." |
| View no longer fits | `compatibility: INCOMPATIBLE` | What does not fit, in words; the view is not applied |
| Download refused | 403 at download | "You may no longer see some of what this file holds, so it cannot be downloaded." |

## 4. Verification

Run 2026-10-11 on macOS: frontend in the `AHDA-frontend` container (Node 24); backend with `NUGET_PACKAGES=/Volumes/SanDisk/Bader/Development/Caches/nuget` against `AHDA-postgres` and `AHDA-ldap`.

| # | Command | Result |
| --- | --- | --- |
| 1 | `docker exec -w /repo/src/frontend AHDA-frontend npm run lint` | 0 problems |
| 2 | `npm run typecheck` | 0 errors |
| 3 | `npx prettier --check .` | All matched files use Prettier code style |
| 4 | `npx vitest run` | **919 passed, 52 files** (874, 50 files on `dev` at 6b15f8c: 45 new — `reports.test.tsx` 34, `rules.test.ts` 11). A first full run under load (the live API and a second Vite running) failed 4 unrelated `findBy` waits in the schedule, closeout and create-project suites; alone they passed (72 of 72), and the rerun with the live services stopped passed whole |
| 5 | `npm run build` | Built; the chunk-size warning is the one on `dev` |
| 6 | `dotnet build src/backend/PMPlatform.slnx -warnaserror`, and `-c Release -warnaserror` | 0 warnings, 0 errors, both |
| 7 | `dotnet test src/backend/PMPlatform.Tests.Integration --filter FullyQualifiedName~PMPlatform.Tests.Integration.Reports` | 23 passed (21 on `dev`): `AViewSharedWithARoleIsReadAndRunByItsHoldersEachAsTheyMaySee` and `AViewIsSharedOnlyWithTheSavedViewRolesEachOnce` new; `AnOutputIsAuthorisedAgainAtDownloadTimeOnTheRulesOfItsData` now also asserts the requester on the job and in the history |
| 8 | `dotnet test src/backend/PMPlatform.Tests.Unit`; Persistence integration | 1653 passed (as on `dev`); Persistence 123 of 123, query plans included |
| 9 | `UPDATE_OPENAPI_SNAPSHOT=1 … --filter ReportContractTests`; `TheApiKeepsEveryPromiseOfTheSnapshot` against `dev`'s snapshot; `contract-check.py`; `migration-dry-run.sh` then `seed-dry-run.sh` on a scratch database after a Release build; `erd-check.py` and the other `docs/architecture/*-check.py` | No breaking change against `dev` (the share list is an optional request property); the snapshot gains `sharedWithRoleCodes` (request, detail, summary), `ownerUserId` and `compatibility` (summary), `requestedByUserId` (job detail and summary), `failureCode` (job summary); 1646 linter findings, as on `dev`; "3928 statement(s), applied twice, no destructive statement" (6 lines name TASK-072); "21 table(s) seeded, unchanged by a second run, no integrity violation"; ERD "123 tables, 123 register rows, 123 diagram entities"; all OK |
| 10 | Live (`artifacts/task-072/live`, git-ignored: `reset.sh` — `task072_live` migrated by this branch's `ahda-migrate` image and seeded by the compose seed, TASK-071's fixture plus a project C in a second department (`setup.sql`), `AHDA-api-072` from this branch's `ahda-api` image on 5102 with the job worker every 5 s; a second Vite on 5183; `expected072.json` — the PUBLISHED REPORT_RULES entries read from the database before the run; `check.mjs` in Chromium; output `check.out`, screenshots `out/`) | **41 of 41 checks.** **Check 1**: SCR-138 states "41 fields offered, from published report rules version 2"; the field picker holds exactly the 41 labels of the PUBLISHED version in the database, none missing, none extra; the live *Approved budget (current)* the allowlist leaves out is not offered; every choice is a checkbox and there is no text area on the page; a filter's field list is exactly the 41 filterable entries; a composition of three fields with a filter and a sort runs (200). **Check 2**: local.r02 saves the composition with no role ticked; local.r03 and local.r07 are not listed it and opening it by address is 404, "not available"; local.r02 shares it with R03 on SCR-139 (`PUT` 200, "Shared with Department Managers"); local.r03 is then listed it "Shared with you" with no *Change sharing* or *Delete*, opens it with "shared with you by …" and "It runs with your own access", and its run holds projects A and B but not project C of the other department, which local.r02's run of the same view holds — the configuration transferred, the authorisation did not; local.r07 is still not listed it; unshared, local.r03 is no longer listed it. **Criterion 2**: local.r02 exports Financial Performance as CSV (202), the dialog follows the job to *Download* (200); SCR-140's first row reads *Generated by* "Local R02 — Portfolio Manager (you)", requested and generated 11 Oct 2026, 16:15 (UTC in the browser), the same job, requester and minute as `reports.report_job` in the database; local.r03's history does not list it. **axe with colour contrast**: 0 violations on six screens in English and three in Arabic; Arabic right to left; **390 px**: no sideways page scroll |

The live run found one harness defect (a selector read every select of the dialog as the filter's field list) and, on review of the screenshots, one wording defect: the builder's filter summary showed the code `ACTIVE`; it now reads the value in its source's words. The component tests found that the explorer offers *Choose columns and filters* twice (header and empty state) by design, and the mutation tests found the OpenAPI snapshot stale after `failureCode` was added to the job summary; it was regenerated.

### 4.1 Mutation tests

`artifacts/task-072/mutations/run.py` (not committed) applies each mutation alone, runs the reports frontend suite (F-) or builds and runs the Reports integration suite (B-), restores the file with a fresh mtime and, after the backend ones, rebuilds. 17 mutations, 17 killed.

| # | Mutation | Killed by |
| --- | --- | --- |
| F-1 | The builder offers a field the allowlist does not hold | `its field picker is exactly the PUBLISHED allowlist …`, `there is no query box …` |
| F-2 | A filter with an operator the field does not take is sent | `a field, filter or sort that is not offered is dropped, never sent` |
| F-3 | A masked value is shown | `a masked value is Restricted even if the source sent one` |
| F-4 | A missing value is shown as 0 | `a value-less cell with no reason is No data, never blank or 0` |
| F-5 | A stale value is not flagged | `a missing value is never 0 and a stale one keeps its value …` |
| F-6 | The export history does not name who generated an output | `each job names its requester and when …` |
| F-7 | The export history shows the request time as the generation time | `each job names its requester and when …` |
| F-8 | Another person's shared view may be deleted or re-shared from the list | `the list holds the person's own views and those shared with them …` |
| F-9 | A view may be shared with a role outside ADR-019 | `MOD-062 shares only with ADR-019's roles …` |
| F-10 | Export is offered whatever the API says | `export is offered only when the API says the person may export` |
| F-11 | Changing sharing drops the roles chosen | `its owner changes who a view is shared with …` |
| B-1 | A view is readable and runnable by anyone | `AViewIsItsOwnersAloneAndRunsWithTheRunnersAuthorisation`, `AViewSharedWithARoleIsReadAndRunByItsHoldersEachAsTheyMaySee` |
| B-2 | The list holds views shared with any role | `AViewSharedWithARoleIsReadAndRunByItsHoldersEachAsTheyMaySee` |
| B-3 | A view may be shared with any role | `AViewIsSharedOnlyWithTheSavedViewRolesEachOnce` |
| B-4 | A person a view is shared with may change it | `AViewSharedWithARoleIsReadAndRunByItsHoldersEachAsTheyMaySee` |
| B-5 | A person a view is shared with is told its other recipients | `AViewSharedWithARoleIsReadAndRunByItsHoldersEachAsTheyMaySee` |
| B-6 | A job does not name its requester | `AnOutputIsAuthorisedAgainAtDownloadTimeOnTheRulesOfItsData` |

## 5. Acceptance criteria, deliverables and checks

| Item | Result |
| --- | --- |
| The Custom Report Builder only ever offers the allowlisted fields/joins from the backend configuration (never a free-text SQL box) | **MET.** D-5; tests `acceptance criterion 1`; §4 row 10 check 1; F-1, F-2 |
| The Export History list shows who generated each output and when | **MET.** D-7; tests `acceptance criterion 2`; §4 rows 7 and 10; F-6, F-7, B-6 |
| A user cannot see another user's non-shared saved views | **MET.** D-3; tests `acceptance criterion 3`; §4 rows 7 and 10 check 2; F-8, B-1, B-2, B-4 |
| Validation: compare the builder's field picker with the backend configuration | **MET** in Chromium against the database's PUBLISHED REPORT_RULES version (§4 row 10) |
| Validation: a saved view created by one user is not visible to another unless explicitly shared | **MET** in Chromium, both halves: private and invisible, then shared and visible to the role only, then private again (§4 row 10) |
| Deliverables: React components/routes for SCR-130–140 and MOD-060–062 | **MET** (§3; header row "Deliverables") |
| Gate decision: 10 reports; bilingual labels; free text in the language entered | **MET** (D-1, D-2, D-10) |
| Participation amendment: ADR-013, ADR-006, ADR-012, ADR-019, shared saved views (TBC-RPT-20) | **MET** (D-3 to D-6, D-10). Sharing recipients are roles only (F-2) |

## 6. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **People are named by short id for most readers.** A shared view's owner and another requester are read through `GET /users/{id}`, which needs `USER_VIEW` (R01's): others see "User 00000000". The signed-in person's own jobs and views name them in full | PMO (Appendix A) or a later task (a names read for reports) | "Shared with you by User 00000000" |
| F-2 | **TBC-RPT-20's sharing authority, as built**: the owner shares; recipients are ADR-019's saved-view roles R02, R03, R07, all holders of the role across every department. User and department recipients, and the ERD's one-of-three CHECK, are not built (`erd.md` change log 2026-10-11): a non-administrator can list neither users nor departments to choose from | Product/Governance (TBC-RPT-20) | A view cannot be shared with one person or one department |
| F-3 | **The builder is closed everywhere** until AHDA publishes a `REPORT_RULES` version on FG-04 (`reports.md` F-5); the page says so | AHDA (FG-04 publisher) | SCR-138 shows "not available yet" |
| F-4 | **AHDA's roles export nothing yet** (`reports.md` F-3): `mayExport` is false for them, so the shell offers no Export and says "Your access does not include exporting this report" | PMO (Blueprint Appendix A) | No export from the internal roles until the grants are decided |
| F-5 | **Not built**: scheduled reports on SCR-130, an operational cross-user export view on SCR-140, a "last used" time on SCR-139 (the API records no use; *Last changed* is shown), a sensitivity and semantic-state marker on SCR-130's catalogue entries (the catalogue entry carries neither; the report shell shows both), ADM-037 screens | PMO / Business Analyst (`reports.md` F-7) | Those parts of FG-02 §13 wait for their API |
| F-6 | **Routes differ from Step 16's PROPOSED `/views/scr-13n/…`** (D-1) | Frontend lead | None at runtime |
| F-7 | **A frontend task changed the backend** (D-3, D-7), by the user's decision of 2026-10-11 | — | — |
| F-8 | **CTL-43 review advisable**: sharing changes who may read a configuration (never its data: every run is authorised as its reader), and a Security Lead review cannot be requested — the CODEOWNERS teams do not exist (TASK-031 F-13) | Maintainer | The PR's CTL-43 box stays unticked |
| F-9 | **ADR-019 is not in `adrs/ADR-register.md`** (`reports.md` F-2) | Engineering Architect | Traceable to the task rows only |

## 7. Change log

| Date | Change |
| --- | --- |
| 2026-10-11 | Created (TASK-072) |

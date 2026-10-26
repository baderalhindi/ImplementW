# Project Register and Creation UI (WF-01 Frontend)

| Field | Value |
| --- | --- |
| Task | TASK-042 — Build Project Register & Creation UI (WF-01 Frontend) |
| Depends on | TASK-041 — the WF-01 API (`project-registration.md` §3–§5): every screen reads and writes through it, and every refusal shown is one it returns. TASK-026 — the register pattern (`indexing-strategy.md` P-2, I-01 to I-05). TASK-032, TASK-036, TASK-038, TASK-040 — the SPA foundation |
| Record date | 2026-10-02 |
| Status | **BUILT AND VERIFIED LOCALLY**: component tests against a mocked API, the request shapes against the compose API (§4 rows 9–10), mutation tests (§4.1), and Draft → Submitted end to end in Chromium against this branch's API with test grants (§4 row 13). **In a real environment nobody can create a project through this UI yet** (F-1), and no AHDA role sees a project (F-2): both are grants, not code |
| Branch | `feat/task-042-wf01-project-registration-frontend` |
| Deliverables | React components and routes for SCR-025, SCR-026, SCR-033, SCR-034, SCR-035, SCR-040–043 and MOD-001–003 in `src/frontend/src/features/projects` (`register/`, `form/`, `workspace/`, `dialogs/`, `components/`, `access.ts`, `registration.ts`, `governance.ts`, `useProjectLookups.ts`, `routes.tsx`, `api/`); i18n `features/projects/i18n/{ar,en}.json`. One API filter (D-9). Additions to the foundation (D-10). 55 frontend tests, 1 integration test. This record |
| Environment variables / secrets | None |
| Gate decision applied | **No project-creation route for external entity users** — superseded by the participation amendment below, as TASK-041 already applied it (D-1) |
| Participation amendment | **ADR-013 changed: the UI provides an entity project-draft creation route; approval remains AHDA's** — D-1. **ADR-015: registration assigns a governance profile by rule on budget and duration, AHDA-overridable; mandatory fields follow the profile** — D-6 (assignment by rule is TASK-105's, F-3) |
| Workbook read | The TASK-042 row as given in the task request, 2026-10-02; `project-registration.md`; `indexing-strategy.md` §3, §4; `approval-framework.md` D-10, F-8; `master-data-configuration.md` D-10, D-15; `document-library-ui.md` F-1; `step-14b-rtm-findings-closure.md` F-02 (DSH-009 inside SCR-040). Blueprint Appendix B (the screen inventory) is not in the repository (F-6) |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | SCR-025 Project Register, SCR-026 My Projects, SCR-033 Create, SCR-034 Edit Draft, SCR-035 Location, SCR-040–043 the workspace shell and its tabs, MOD-001 Delete Draft, MOD-002 Assign Project Manager, MOD-003 Change Status; Arabic and English, responsive | This task |
| **In** | `projectManagerUserId` on `GET /projects`, the one thing SCR-026 needed that the API lacked | This task (D-9) |
| **Out** | Governance profile assignment by rule and its override (ADR-015) | TASK-105 (F-3) |
| **Out** | The review decision itself (SCR-100, MOD-040–042) | TASK-036, already built; SCR-042 links to it |
| **Out** | A map | No approved map service (F-5) |
| **Out** | The legacy intake path (ADR-014); suspension and closure | TASK-104; TASK-062, TASK-063 |
| **Out** | The tabs later modules add (schedule, milestones, tasks, progress, financials, risks) and DSH-009 inside SCR-040 | TASK-044 onward; TASK-070 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **SCR-033 is open to external entity users.** The row's gate decision ("no project-creation route for external entity users") is superseded by its own participation amendment (ADR-013 changed), as TASK-041 D-10 already shipped it: R04 and R08 hold `PROJECT_REGISTER` at ENTITY. An external user's form starts with their entity fixed and the mode ENTITY_MANAGED; they never see "Start review" or "Activate" (D-7), which the API also refuses them. | ADR-013 as amended: entity drafts, AHDA approval. |
| D-2 | **The Register follows the indexing task's pattern.** Filters and page live in the URL (`shared/api/useListParams.ts`): `status`, `departmentId`, `externalEntityId` (internal users only; an external user sees one entity), `q` (title or Formal Project ID); each change returns to page 1. Paging is R-29's, 25 a page. The order is P-2's, most recently changed first, and the screen says so: `ListProjects` declares no `sort` (F-8), and sorting one page in the browser would misrepresent the rest. A department or entity filter needs ORGANIZATION_VIEW to name the choices and is not offered without it. | Acceptance criterion 2; `indexing-strategy.md` P-2, I-01–I-05; R-29, R-31. |
| D-3 | **Three distinct empty answers.** Unfiltered on SCR-025: "Your role does not give you access to any project yet", with how scope works. Filtered: "No project matches these filters" and Clear filters. SCR-026: "You are not the Project Manager of any project". A 403 on the list (a role without `PROJECT_VIEW`, every internal role today, F-2) is the same answer as a scope that reaches nothing, not an error with a retry. | Acceptance criterion 2: "a distinct empty state for a role with zero visible Projects". |
| D-4 | **Workspace tabs follow the session's scope.** The session carries role assignments with their anchors, not permissions (TASK-032). `access.ts` counts an assignment as reaching the project when each anchor it has (department, entity, project) matches the project's; an unanchored one reaches every project. Overview, Registration and Location show to anyone the API showed the project; Documents needs reach; Review History needs reach and an internal user, since a review's requester is AHDA (TASK-041 F-11) and a run carries no entity anchor (TASK-035 F-8). A tab opened by its address is refused to someone it is not offered to, and nothing is read. Edit, Submit and Change Status need reach; Delete needs the creator. The API decides every read and command again (F-7). | Acceptance criterion 3. |
| D-5 | **Create is enabled only when the form is valid.** `registration.ts` mirrors `ProjectRequest.Validate`: the fields a draft needs (title, classification, owning department, participation, governance profile, and the entity when ENTITY_MANAGED); title and description ≤ 2000; the budget as R-16 (up to 16 digits, up to 2 decimals, sent with exactly 2); end date not before start; latitude ±90, longitude ±180. The button is disabled until all pass, and a note tied to it (`aria-describedby`) lists what is still needed. A wrongly shaped value is flagged as it is typed. The API then checks the references; its field errors land on the input they name (`title.text` on the title), and its rule refusals show above the form. The button stays disabled while saving, so a double click does not make a second draft (TASK-041 F-16). | Acceptance criterion 1: "client- and server-side before submission is enabled". |
| D-6 | **ADR-015 in the form: the registrant names a profile; its mandatory fields are asked for at submission.** The profile is a required choice of the PUBLISHED GOVERNANCE_PROFILE items, with "AHDA confirms or changes it at review" (TASK-041 F-8). The profile's `mandatoryFieldCodes` come from the GOVERNANCE_PROFILE resolution (`GET /configuration-resolutions/GOVERNANCE_PROFILE`), each code read as the registration property it names (F-4). They are marked "Needed before submitting for review" with the budget and planned dates the API requires at submission, and MOD-002 stays disabled until all are filled. A draft can be saved without them, as the API allows. | ADR-015 as amended; TASK-041 D-8. |
| D-7 | **The three modals.** MOD-001 Delete Draft: confirmed, sent with the ETag, back to the Register with a notice; a draft another record names (409 `PROJECT_IN_USE`) stays. MOD-002 Assign Project Manager: submission names the manager (TASK-041 D-9); the default is the submitter, or the manager named before, or someone found with the user picker (USER_VIEW); it lists anything still missing and is disabled until nothing is. MOD-003 Change Status: one command per edge, each with what it does: withdraw (SUBMITTED, the registrant), start review (SUBMITTED, internal only), activate (APPROVED_PLANNED, internal only). There is no "set status". Every command sends the ETag the workspace read, so a change made meanwhile is 412, not overwritten. | R-4 (a command per transition); TASK-041 §3, D-7, D-10, D-11. |
| D-8 | **SCR-035 shows and edits the location without a map.** Region, city and coordinates, edited while the project is DRAFT or RETURNED as a PUT of the whole registration with only the location replaced. A city is offered within its region, and changing the region clears a city outside it (core-platform-schema N-1 (b)). | F-5. |
| D-9 | **`GET /projects?projectManagerUserId=` for SCR-026.** One predicate added inside the caller's `RecordScope`, so it narrows and never widens what they see; I-04 (`project_manager_user_id, updated_at, id`) already serves it. `MyProjectsAreTheOnesTheCallerManagesWithinTheirScope`: the managed project is listed, an unmanaged draft is not, another person's filter returns nothing of theirs, a malformed id is 400. | SCR-026 could not be built correctly without it: one page filtered in the browser would page wrongly. |
| D-10 | **Additions to the foundation, backward compatible.** `httpClient`: `DELETE`, which carries no Idempotency-Key (idempotent, R-40), and a 204 with no body. `shared/api/masterData.ts`: master data reads and `isPublished`, moved from documents, which now imports them (DRY). `DocumentBrowser` `embedded` (an h2 section in the workspace). `TextField` `type="date"` and `inputMode="decimal"`. `common.home.projects`. Styles: `.tabs`, `.form__section`, `.badge-group`, `.list-order`, `.workspace__*`, `.pre-line`. | — |

## 3. Screens and routes

All under `/projects`, behind `RequireSession` (`features/projects/routes.tsx`).

| Screen | Route | Component |
| --- | --- | --- |
| SCR-025 Project Register | `/projects?status=&departmentId=&externalEntityId=&q=&page=` | `register/ProjectListPages.tsx` (`ProjectRegisterPage`) over `register/ProjectList.tsx` |
| SCR-026 My Projects | `/projects/mine?status=&q=&page=` | `MyProjectsPage` |
| SCR-033 Create Project | `/projects/new` | `form/ProjectFormPages.tsx` (`CreateProjectPage`) over `form/ProjectForm.tsx` |
| SCR-034 Edit Project Draft | `/projects/:projectId/edit` | `EditProjectPage` |
| SCR-040 Project Workspace (shell, Overview) | `/projects/:projectId` | `workspace/ProjectWorkspace.tsx`, `workspace/OverviewTab.tsx` |
| SCR-041 Registration | `/projects/:projectId/registration` | `workspace/RegistrationTab.tsx` |
| SCR-042 Review History | `/projects/:projectId/reviews` | `workspace/ReviewsTab.tsx` |
| SCR-043 Documents | `/projects/:projectId/documents` | `workspace/DocumentsTab.tsx` (the WF-12 `DocumentBrowser`, embedded) |
| SCR-035 Project Location | `/projects/:projectId/location` | `workspace/LocationTab.tsx`, `components/LocationFields.tsx` |
| MOD-001 Delete Draft | on SCR-040 | `dialogs/DeleteDraftDialog.tsx` |
| MOD-002 Assign Project Manager | on SCR-040 | `dialogs/AssignManagerDialog.tsx` |
| MOD-003 Change Status | on SCR-040 | `dialogs/ChangeStatusDialog.tsx` |

## 4. Verification

Run 2026-10-02 on macOS with Docker Desktop: frontend in `AHDA-frontend` (Node 24); backend on the host (.NET 10.0.401) against `AHDA-postgres` and `AHDA-ldap`; the compose API rebuilt with this branch (`docker compose -f infra/docker/docker-compose.yml up -d --build --wait api`).

| # | Command | Result |
| --- | --- | --- |
| 1 | `npm run lint` | 0 problems |
| 2 | `npm run format:check` | All matched files use Prettier code style |
| 3 | `npm run typecheck` | No errors |
| 4 | `npm test` | 27 files, 247 tests passed (23 files and 192 tests before; 55 new: 54 in `features/projects`, 1 in `httpClient.test.ts`). The documents tests pass unchanged on the moved master data module |
| 5 | `npm run build` | Built. 897 KB JS (239 KB gzip), up from 823 KB; the >500 KB chunk warning was already on `dev` (TASK-038 F-5) |
| 6 | `npm audit` | 0 vulnerabilities. No dependency added |
| 7 | `dotnet build src/backend -warnaserror` | 0 warnings, 0 errors |
| 8 | `dotnet test src/backend/PMPlatform.Tests.Unit`; `DB_CONNECTION_STRING=… dotnet test src/backend/PMPlatform.Tests.Integration` | 686 passed; 459 passed (458 before, 1 new: `MyProjectsAreTheOnesTheCallerManagesWithinTheirScope`) |
| 9 | curl through the SPA's proxy (`localhost:5173/api/v1`) as `local.r08` (entity) and `local.r02` (AHDA), with the calls the screens make | r08: `GET /projects`, `?projectManagerUserId={own id}`, `?status=DRAFT&q=road` 200 empty pages; `?projectManagerUserId=me` 400; `GET /projects/{unknown}` 404; **403** for `/master-data-catalogues`, `/departments`, `/external-entities`, `/configuration-resolutions/GOVERNANCE_PROFILE` (F-1). r02: `GET /projects` **403**, `GET /approval-instances?subjectModule=Project…` 403 (F-2) |
| 10 | As `local.r08`, `POST /projects` with the body SCR-033 builds (ids read from the database) | 422 `PROJECT_REFERENCE_INVALID` `classificationItemId NOT_FOUND` (no published classification, TASK-041 F-10): the shape is accepted. With the budget `1500000.5`: 400 `registrationBudgetSar MALFORMED`, which the form never sends (it sends `1500000.50`, D-5). End before start: 400 `plannedEndDate DATE_BEFORE_START`. Each field path is one the form puts on its input |
| 11 | The nine `docs/architecture/*-check.py` gates | All OK |
| 12 | `DB_CONNECTION_STRING=… dotnet test src/backend/PMPlatform.Tests.Integration --filter …Project` | 87 passed, with `AProjectManagersProjectIsInTheirMyProjectsButNotInAnotherDepartmentsManagerView`: the entity Project Manager's draft is not in their My Projects until submitted naming them, then is; the other department's Department Manager (local.r03) neither lists, filters nor opens it (404), and does list a project of their own department. With the list's `RecordScope` predicate removed, it fails ("Item found in collection"). `ProjectTestHost` gained a second active department |
| 13 | Browser e2e, Draft → Submitted (harness, not committed; F-11): a throwaway database migrated and seeded like compose, with test grants; this branch's API image against it; the SPA (Vite) proxied to it; Chromium 154 headless driven by puppeteer-core 24 inside `AHDA-frontend` | **19 of 20 checks passed.** local.r04 (Project Manager): Create disabled when empty, without a department and with budget `1,500`, enabled once valid; draft created, Draft, no Formal Project ID; not in My Projects; MOD-002 names them by default and is enabled; Submitted; then in My Projects. local.r06 (R03 of another department): lists only their own control draft, the PM's project by address is "not found". local.r03 (R03 of the PM's department): lists it under Submitted. **The one failure was the script's expectation**: MOD-003 offered local.r04 "Start AHDA's review" beside "Withdraw" — an internal user reached by an assignment (D-4); the API refused it, 403 `PERMISSION_DENIED` (F-7). The database, API container, dev server, Chromium and tooling were removed afterwards |

Not run: axe with colour contrast and the Arabic layout at phone width in a browser (row 13 drove the English flow only). jsdom's axe pass (contrast off) is clean on SCR-025 in English and in Arabic right to left, SCR-033 and SCR-040. The new colours are existing pairs: the active tab is `--color-primary` on the background, as links are. No end-to-end create: no classification is published in any environment (TASK-041 F-10) and the entity registrant cannot read the lists the form offers (F-1).

The tests, by acceptance criterion and amendment:

| Item | Tests |
| --- | --- |
| 1. Create validates required fields client- and server-side before submission is enabled | `createProject.test.tsx`: disabled with the note naming each missing field, enabled once all are in, nothing sent before; ENTITY_MANAGED needs its entity; a malformed budget, end before start and latitude 95 keep it disabled; the exact request (R-16 money, no `formalProjectId`, Idempotency-Key); a 400 `title.text` lands on the title, a 422 is explained; 412 on edit. `rules.test.ts`: each shape check |
| 2. Register filter/sort/pagination, distinct empty state for a role with none | `register.test.tsx`: rows as returned, order stated; status, department and text in the request and URL, back to page 1; next page; the role empty state with no table and not the filtered one; the filtered empty state with Clear filters; a 403 is the role empty state; other refusals retry; SCR-026 asks for `projectManagerUserId` = the user and has its own empty state; axe in English and Arabic |
| 3. Workspace tabs from the user's RBAC scope | `workspace.test.tsx`: internal reaching user 5 tabs; external 4 (no reviews); internal not reaching 3 and no actions; `/reviews` by address refused with no read; actions by state and person (creator delete, external only withdraw, AHDA review and activate, external never activate). `rules.test.ts`: reach by anchor, tabs and commands per access |
| ADR-013 amendment | `createProject.test.tsx`: an entity user creates for their own entity, fixed. `workspace.test.tsx` and `rules.test.ts`: no AHDA gate is ever offered to an external user |
| ADR-015 amendment | `workspace.test.tsx`: MOD-002 lists and waits for a FULL profile's description and region; `rules.test.ts`: codes to fields, unknown codes ignored |
| MOD-001–003, SCR-035, SCR-042, SCR-043 | `workspace.test.tsx`: delete with If-Match and the deleted notice, 409 kept; submit names the submitter by default with If-Match, 422 manager explained; start review and activate as single commands with If-Match, 422 route missing explained; location PUT with only the location changed and the city cleared, read-only under review; reviews listed and linked; documents as an h2 section scoped to the project |

### 4.1 Mutation tests

Each mutation was applied, `npx vitest run src/features/projects` run, and the source restored (54 of 54 passed afterwards).

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | Create enabled with required fields missing | 4 |
| M-2 | The unfiltered empty Register shows the filtered message | 2 |
| M-3 | Tabs ignore the scope | 3 |
| M-4 | Start review offered to an external user | 2 |
| M-5 | My Projects not narrowed to the manager | 1 |
| M-6 | MOD-002 ignores the profile's mandatory fields | 1 |
| M-7 | Commands sent without If-Match | 2 |
| M-8 | A server field error not put on its field | 1 |

## 5. Acceptance criteria and deliverables

| Item | Result |
| --- | --- |
| Create Project validates required fields client- and server-side before submission is enabled | **MET** (D-5; §4) |
| The Register supports the indexing task's filter/sort/pagination pattern and shows a distinct empty state for a role with zero visible Projects | **MET** (D-2, D-3; §4). One sort order, the API's (F-8) |
| The workspace shell renders its tabs based on the user's RBAC scope | **MET** (D-4; §4), from assignment anchors; the API remains the authority (F-7) |
| Gate decision / ADR-013 amendment: an entity draft-creation route; approval AHDA's | **MET** (D-1, D-7) |
| ADR-015 amendment: profile by rule, AHDA-overridable; mandatory fields follow the profile | **PARTLY MET.** Mandatory fields follow the profile in the UI (D-6). Assignment by rule and the override are TASK-105's (F-3), and the API does not yet enforce the profile's fields |
| Deliverables: components and routes for SCR-025/026/033–035/040–043 and MOD-001–003 | **MET** (§3); SCR-041–043 named by this task (F-6) |

## 6. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **The registrant cannot read what the form offers.** Classifications, profiles, regions and cities need MASTER_DATA_VIEW; departments and entities ORGANIZATION_VIEW; the profile settings CONFIGURATION_VIEW; another Project Manager USER_VIEW. Only R01 holds them; R04 and R08, the only roles with `PROJECT_REGISTER`, get 403 for each (§4 row 9). The form says which lists it cannot read and cannot be saved. Same cause as TASK-038 F-1. **For an external user no grant can fix it** (found by §4 row 13): with ORGANIZATION_VIEW at ALL granted, local.r08 still gets 403 on `/departments` and `/external-entities` and 404 on its own department, because the engine never lets an external user reach a record of no entity (`authorization-engine.md` D-3 (2)), and a department has none. ADR-013's entity draft route therefore needs an API change: a lookup of active departments (and the caller's entity) open to a registrant, e.g. over `IOrganizationDirectory`, and the same for PUBLISHED master data items | Engineering Architect (API); PMO (Appendix A) | No project can be created through the UI in a real environment; an entity user cannot create one even with grants |
| F-2 | **No internal role holds `PROJECT_VIEW`, `PROJECT_REVIEW` or `PROJECT_ACTIVATE`** (TASK-041 F-1). AHDA users see the role empty state, and start review and activate are never reached | PMO (Appendix A); TASK-110 | AHDA cannot see, review or activate a project through the UI |
| F-3 | **ADR-015's assignment by rule and override are not built**, and the API does not check a profile's mandatory fields at submission: the UI is the only check (D-6) | TASK-105 | A client other than this UI can submit without the profile's fields |
| F-4 | **Profile field codes are read by a convention this task chose**: a code is the registration property in UPPER_SNAKE_CASE (`REGISTRATION_BUDGET_SAR`, `REGION_ITEM_ID`, …; `governance.ts`). FG-04 fixes no vocabulary for `mandatoryFieldCodes`; any other code asks nothing of the form | PMO (GOVERNANCE_PROFILE content); TASK-105 | A profile written with other codes makes no field mandatory |
| F-5 | **No map on SCR-035.** No map tile service is approved, and a public one would receive every project's coordinates; no map library is a dependency | Engineering Architect; AHDA Cybersecurity | Location is read as region, city and numbers |
| F-6 | **Screen inventory absent.** Blueprint Appendix B is not in the repository: SCR-041 Registration, SCR-042 Review History and SCR-043 Documents are this task's names, SCR-040 Overview follows the RTM's "DSH-009 inside SCR-040". TASK-041's controller comment calls create "MOD-001"; this task's row names MOD-001 Delete Draft, which is followed | PMO (Appendix B) | A screen may be named differently in AHDA's inventory |
| F-7 | **Tabs are offered from assignment anchors, not permissions**: the session carries no permissions, and an assignment's scope type (OWN, DEPT, …) is in its profile. A tab can be offered and the API still refuse it; SCR-042 then says so (`projects.reviews.forbidden`) | Engineering Architect (an effective-permission read for the session) | A person may see a tab whose content their role cannot read |
| F-8 | **`ListProjects` declares no `sort`**: the Register has one order, P-2's | Engineering Architect, when a sort is required (indexing-strategy A-1) | No sort by title, state or date |
| F-9 | **Delivery-team Arabic strings** have not had a bilingual business review (TASK-032 F-9) | PMO / AHDA business reviewer | Terminology may differ from AHDA's usage |
| F-10 | **Security Lead review (CTL-43) cannot be requested**: the CODEOWNERS teams do not exist (TASK-031 F-13). This change adds a filter to an RBAC-scoped list and renders tabs and commands by scope | Maintainer | The PR's CTL-43 box stays unticked |
| F-11 | **No committed browser e2e suite**: Playwright is TASK-085's (`ci-quality-gates.md`), so row 13 is a harness kept outside the repository (the script and screenshots are in the maintainer's evidence folder) | TASK-085 | Draft → Submitted is not re-run by CI in a browser; the component and integration tests are |

## 7. Change log

| Date | Change |
| --- | --- |
| 2026-10-02 | Created (TASK-042) |
| 2026-10-02 | Validation: integration test of My Projects vs another department's manager (§4 row 12); browser e2e Draft → Submitted (row 13); F-1 extended (external users), F-11 added |
| 2026-10-02 | SCR-040 gains the Progress tab (SCR-048, SCR-070) after Location, audience `reached`; only the overview tab link matches exactly (TASK-045, `progress-ui.md` D-1, D-11) |
| 2026-10-03 | SCR-040 gains the Schedule tab (SCR-060, SCR-045, SCR-061) after Progress, audience `reached` (TASK-047, `schedule-ui.md` D-1, D-13) |
| 2026-10-03 | SCR-040 gains the Tasks tab (SCR-047) after Schedule, audience `reached` (TASK-049, `task-boards-ui.md` D-1, D-11) |
| 2026-10-04 | SCR-040 gains the Milestones tab (SCR-046) after Tasks, audience `reached` (TASK-051, `milestone-ui.md` D-1, D-11) |
| 2026-10-05 | SCR-040 gains the Financials (SCR-049, SCR-071) and KPIs (SCR-050, SCR-073) tabs after Milestones, audience `reached` (TASK-053, `financial-kpi-ui.md` D-1, D-12) |

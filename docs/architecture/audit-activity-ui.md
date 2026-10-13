# Audit & Activity UI (FG-06 Frontend)

| Field | Value |
| --- | --- |
| Task | TASK-074 — Build Audit & Activity UI (FG-06 Frontend) (P13 - Technical Operations (FG-05/06)) |
| Depends on | TASK-073 — the FG-06 API (`audit-activity.md` §5): `GET /audit-access`, `/audit-events` and `/audit-events/{id}` read by category, scope and sensitivity; `/activity-entries` under `ACTIVITY_VIEW`; `/audit-ingestion-status`, `POST /audit-integrity-verifications` and `/activity-projection-checks` under `AUDIT_OPERATIONS_MANAGE` |
| Record date | 2026-10-13 |
| Status | **BUILT AND VERIFIED LOCALLY**: rule and component tests against a mocked API that enforces categories as the API does (§4 rows 1–4), 9 mutation tests (§4.1), and **both workbook validation checks in Chromium against the real API** on a scratch database, signed in for real as local.r01 (through MFA) and local.r02 (§4 row 6: 27 of 27 browser checks, 5 of 5 API checks). No backend change |
| Branch | `feat/task-074-fg06-audit-activity-frontend` |
| Deliverables | React routes and components in `src/frontend/src/features/audit-activity`: `workspace/ProjectActivityTab` (SCR-057), `workspace/ProjectAuditTab` (SCR-058, its in-project event view and its denial), `AuditTrailPage` (ADM-050, ADM-051 page, ADM-053), `SystemEventLogsPage` (ADM-052); `components/AuditEventList`, `AuditEventDetailView` (ADM-051), `AuditFilters`, `RequireAuditAccess`, `ActorName`; `access.ts`, `filters.ts`, `presentation.ts`, `useAuditAccess.ts`, `api/`; the `auditActivity` i18n namespace. This record |
| Environment variables / secrets | None |
| Gate decision applied | None in the workbook row |
| Participation amendment | **ADR-013**: an entity's people read their projects' Activity only (`ACTIVITY_VIEW` at ENTITY); an actor the API withholds from them is shown as "Actor not shown", never guessed (D-6) |
| Sources read | The TASK-074 row as supplied on 2026-10-13, checked against the workbook's Implementation Plan row (branch; validation: "Attempt to reach SCR-058 as an R01 user who lacks the specific Audit-viewing permission and confirm access is denied; visually confirm SCR-057 and SCR-058 are separate tabs with distinct labeling"). **FG-06 Audit & Activity Functional Specification v1.0** §6–§8, §11, §17, §24.4 and the AUD-API list. **Step 16 Frontend Registers** rows SCR-057/058 and ADM-050–053 (routes, audiences, authorisation UX). `audit-activity.md` (TASK-073) |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | SCR-057 Project Activity History and SCR-058 Project Audit History as two workspace tabs; ADM-050 Audit Trail; ADM-051 Audit Event Detail (on its own and inside the project); ADM-052 System Event Logs; ADM-053 Integration Audit Logs; the sidebar's audit group | This task |
| **Out** | ADM-052's searchable system-event list: AUD-API-055 has no API (TASK-073 F-4) | F-1 |
| **Out** | Archive retrieval, retention and holds, FG-02 audit export, causation lineage, retention class on ADM-051, authorisation-aware filter value lists (AUD-API-015–017): no API (TASK-073 F-4) | F-2 |
| **Out** | The source-record Activity timeline embedded in each record's detail (FG-06 §8.1) | F-3 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Two tabs, two presentations** (acceptance criterion 1). *Activity history* (`/projects/:id/activity-history`) and *Audit history (formal)* (`…/audit-history`) are separate `WORKSPACE_TABS` entries with their own audiences, routes and labels. Activity is a plain-language timeline on the page's surface; the formal audit is a bordered evidence panel holding a table of event codes, classes, outcomes and actors. Each reads only its own endpoint: Activity never calls `/audit-events`, the audit tab never `/activity-entries` | FG-06 §7.2: "Business Activity by default; … a richer formal Audit view"; BR-AUD-005 keeps them apart |
| D-2 | **SCR-058 needs a project audit category: BUSINESS, FINANCIAL or EXTERNAL** (acceptance criterion 2). The tab is offered, and its address opens, only when `GET /audit-access` lists one of them; the list asks for exactly the held ones (`category=…`) and the project. Administration, security and integration audit are platform evidence, read in ADM-050/053, where a project filter remains. R01 ships with ADMINISTRATION only (TASK-073 D-11), so R01 never reaches SCR-058; if the SPA's guard were bypassed, the API answers R01's `category=BUSINESS` with 403 (§4 row 6) | BR-AUD-023: audit access "is not implied by R01"; the workbook's check |
| D-3 | **FG-06's admin screens are not behind the R01 guard.** `/admin/audit-trail`, `/admin/audit-trail/:id`, `/admin/system-event-logs` and `/admin/integration-audit-logs` (Step 16's routes; ADM-051 takes the id) sit beside FG-03's R01-only routes, each behind `RequireAuditAccess` with the permission it needs: any category (ADM-050/051), INTEGRATION (ADM-053), `operationsPermitted` (ADM-052). Anyone else sees why the screen is closed and nothing of it is read | Step 16 audiences: "ADM limited; approved business users", "authorized auditor". An Audit Reviewer need not be R01 |
| D-4 | **Permissions come from `GET /audit-access`, asked again when a screen opens.** The session carries roles, audit is authorised by permission. The shell asks once for the sidebar group, the workspace once for its tabs, each admin screen once for its guard. A failed answer offers no audit tab (never assumed); on an admin screen it is an error with *Try again* | Step 16: "refetch only with current authority"; the API decides every read again |
| D-5 | **ADM-052 is labelled a narrow technical view** (acceptance criterion 3). A labelled aside — *Narrow technical view*: the audit subsystem's own system events, "not the formal audit trail, and … not an observability or SIEM console" — precedes the figures in reading order. It shows the pipeline state (§24.4: captures pending and failing, last error code, events stored, store head, Activity lag) and runs the two read-only, audited checks (integrity verification, projection check), each finding linked to its event in ADM-051. The projection **rebuild** is not offered: it changes the projection and stays an operator command and API call (TASK-073 D-9) | BR-AUD-043; FG-06 §17 ("correlation link to formal Audit where applicable"). The searchable event list has no API (F-1) |
| D-6 | **Activity reads as the API wrote it.** Each line is `summaryEn` or `summaryAr` in the interface language, its domain, its time, and the actor's name or "Actor not shown" when the API withholds it (ADR-013, TASK-073 D-12). Its source link carries the record's id only, to the screen that reads it under its own permission (AUD-CC-17); subjects with no screen of their own have no link | FG-06 §7.1, §7.4 |
| D-7 | **ADM-051 is evidence, with nothing to change.** It shows the event, actor, source and project, the correlation id (with *Show correlated events* into ADM-050), the captured attributes, the request-context attributes withheld from the reader (named, never shown), the hash chain position and digest, and SIEM forwarding, under a line saying the event cannot be edited or deleted. No control writes. From SCR-058 it opens inside the project's workspace (FG-06 §7.2) | §8.3; TASK-073 D-7 |
| D-8 | **Filters are server-side and sent only when valid.** Category (the held ones), class, outcome, a day range, event type, project, actor and correlation ids — those the API supports — kept in the URL. Ids are typed UUIDs, refused in the form when malformed; days are sent as the bounds of the person's local day; a set is sent comma-joined (`category=A,B`), the way `QueryParameters.EnumSet` parses it, not as the shared client's repeated parameter | §8.2; AUD-API-015–017 lists are not built, and listing every user or project would show what the reader may not see |
| D-9 | **Additions outside the feature**, backward compatible: `projects/access.ts` gains the `activity` and `projectAudit` audiences, and `projectAccess` an optional audit access; `WorkspaceTabGuard` waits for `/audit-access` on the history tabs and takes an optional `denied` message; `WorkspaceContext` carries the audit access; `/admin` became a pathless R01 layout plus the audit routes; the sidebar's *Audit & logs* group; the `auditActivity` namespace and styles; `src/test/auditActivityFixtures.ts`. Two tests that count every request now also leave out the shell's `/audit-access`, as they already left out the unread badge | — |

## 3. Screens

| Screen | Route | Component | Opens for |
| --- | --- | --- | --- |
| SCR-057 Project Activity History | `/projects/:projectId/activity-history` | `ProjectActivityTab` | `ACTIVITY_VIEW` (R04 OWN, R08 ENTITY as shipped) |
| SCR-058 Project Audit History | `/projects/:projectId/audit-history` | `ProjectAuditTab` / `ProjectAuditDenied` | BUSINESS, FINANCIAL or EXTERNAL audit |
| ADM-051 in the project | `/projects/:projectId/audit-history/:auditEventId` | `ProjectAuditEventTab` → `AuditEventDetailView` | as SCR-058 |
| ADM-050 Audit Trail | `/admin/audit-trail` | `AuditTrailPage` | any audit category |
| ADM-051 Audit Event Detail | `/admin/audit-trail/:auditEventId` | `AuditEventDetailPage` → `AuditEventDetailView` | any audit category; the event's own category decides (404 otherwise) |
| ADM-052 System Event Logs | `/admin/system-event-logs` | `SystemEventLogsPage` | `AUDIT_OPERATIONS_MANAGE` (R01 as shipped) |
| ADM-053 Integration Audit Logs | `/admin/integration-audit-logs` | `IntegrationAuditPage` | `AUDIT_INTEGRATION_VIEW` |

## 4. Verification

Run on 2026-10-13, macOS; frontend commands in the `AHDA-frontend` container (`/repo/src/frontend`).

| # | Command | Result |
| --- | --- | --- |
| 1 | `npm run typecheck` | 0 errors |
| 2 | `npm run lint` | 0 problems |
| 3 | `npm run format:check` | All matched files use Prettier code style |
| 4 | `npm test` | **956 passed, 54 files** (919 on `dev`): +33 in `audit-activity` (`rules.test.ts` 9, `auditActivity.test.tsx` 24), +4 from `closedWorkspace.test.tsx`, which sweeps every workspace tab for two sessions and now covers the two new tabs |
| 5 | `npm run build` | Built (Vite's existing chunk-size warning only) |
| 6 | **Validation check in Chromium against the real API** (`artifacts/task-074/live`, not committed: `reset.sh`, `setup-074.sql`, `check.py`, `check074.mjs`). A scratch database `task074_live` migrated and seeded as compose does, TASK-056's risk fixture, and `ahda-api` (dev's backend, rebuilt from this tree, fully cached) on 5104 with an MFA stand-in so local.r01 signs in through MFA. local.r02 (no R01) is granted business audit and Activity; local.r01 keeps its shipped audit grants and is given `PROJECT_VIEW`, so that a refusal of SCR-058 can come only from the audit permission. local.r02 registered, assessed and closed risks through the API first | **5 of 5 API checks, 27 of 27 browser checks.** API: R01 holds `[ADMINISTRATION]`; R02 `[BUSINESS]` with Activity; **R01's SCR-058 read (`category=BUSINESS`, project A) answers 403**; R01 reads project A (200). Browser, R01: no Activity or Audit tab on project A; **`/projects/A/audit-history` shows the denial with its reason and reads nothing of the audit store**; ADM-052 opens with *Narrow technical view* before any figure, verification *Intact*, projection *Matches*; ADM-050 lists administration events only; ADM-053 denied. R02: **two tabs, *Activity history* and *Audit history (formal)*, distinct addresses**; the Activity timeline shows the four derived lines and reads only `/activity-entries`; the audit tab lists `Risk.RiskRegistered`, `.RiskAssessed`, `.RiskClosed`, asks `category=BUSINESS&projectId=A` and never reads Activity; **computed styles differ** (Activity: no border, no background; Audit: 4px border, `#f4f6f8` panel); ADM-051 in the project has no edit or delete control; ADM-052 denied to R02. Arabic lines right to left; 390 px without sideways scroll; axe with colour contrast clean on 7 pages. Screenshots in `artifacts/task-074/live/out` |

### 4.1 Mutation tests

`artifacts/task-074/mutations/run.py` (not committed) applied each mutation alone, ran `src/features/audit-activity` and `src/features/projects/rules.test.ts`, and restored the file. **9 of 9 killed.**

| # | Mutation | Failed tests |
| --- | --- | --- |
| M-1 | SCR-058 opens on any audit category (R01 included) | 5 |
| M-2 | The Audit tab follows Activity (the two merged) | 2 |
| M-3 | The tab guard lets an unoffered history tab through | 4 |
| M-4 | SCR-058 asks for every held category, administration included | 1 |
| M-5 | Sets sent as repeated parameters | 3 |
| M-6 | ADM-052 loses its narrow-scope label | 1 |
| M-7 | The admin audit screens require the operations permission (R01's) | 4 |
| M-8 | A malformed id filter is sent | 1 |
| M-9 | ADM-053 reads every category | 1 |

## 5. Acceptance criteria, deliverables and checks

| Item | Result |
| --- | --- |
| SCR-057 (Activity) and SCR-058 (Audit) are visually and navigationally distinct, never merged into one tab | **MET.** D-1; tests `acceptance criterion 1`; M-2; §4 row 6 |
| SCR-058 is reachable only for users holding the explicit Audit-viewing permission, not for every R01 | **MET.** D-2, D-4; tests `acceptance criterion 2`; M-1, M-3, M-4; §4 row 6 (browser and API) |
| ADM-052 is clearly labelled as a narrow technical view, not a full SIEM/observability console | **MET.** D-5; tests `acceptance criterion 3`; M-6; §4 row 6. Its event list awaits an API (F-1) |
| Validation: reach SCR-058 as an R01 user who lacks the Audit-viewing permission; confirm access is denied | **MET** in Chromium and at the API (§4 row 6) |
| Validation: visually confirm SCR-057 and SCR-058 are separate tabs with distinct labelling | **MET** in Chromium, with screenshots and computed styles (§4 row 6) |
| Deliverables: React components/routes for SCR-057/058 and ADM-050–053 | **MET** (§3) |

## 6. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **ADM-052 has no system-event list.** AUD-API-055 (`/audit/system-events`) is not built (TASK-073 F-4), and the platform keeps no system-event store to read. ADM-052 shows what FG-06 exposes (§24.4 self-monitoring) and says so | Backend (a later FG-06/FG-05 task) | ADM-052 is status and checks only |
| F-2 | **ADM-051 lacks retention class, causation lineage and the acting-on-behalf-of context**; ADM-050 lacks Event ID, actor type, source domain, sensitivity, causation and environment filters, archive mode and export: the API has none of them (TASK-073 F-4) | Backend, AHDA (retention, PTBC-011) | Shown as the API serves them |
| F-3 | **The source-record Activity timeline** inside each record's detail (FG-06 §8.1) is not built; `/activity-entries?subjectType=&subjectId=` supports it | Each source module's UI owner | Activity is read per project only |
| F-4 | **Shipped grants make SCR-058 empty on a fresh install**: no role ships with business, financial or external audit (TASK-073 F-2), so only a permission profile opens it | AHDA (PTBC-011) | Nobody sees the Audit tab until AHDA names the reviewers |
| F-5 | **Resolved 2026-10-13 by TASK-075** (`integration-monitoring.md` D-12): IntegrationMonitoring emits INTEGRATION audit — alerts raised, acknowledged, resolved and dismissed, retries, reconciliations requested and completed — so ADM-053 lists them for an `AUDIT_INTEGRATION_VIEW` holder. Was: **No module emits INTEGRATION audit yet** | FG-05 owner | — |
| F-6 | **ADM-052 does not offer the projection rebuild** (D-5) | Maintainer | A rebuild remains `activity-projection-rebuild` or `POST /activity-projection-rebuilds` |
| F-7 | **CTL-43 review advisable**: the UI decides which audit screens to offer and moves FG-06's screens out of the R01 guard. The API enforces every read, but a Security Lead review cannot be requested: the CODEOWNERS teams do not exist (TASK-031 F-13) | Maintainer | Review before merge |

## 7. Change log

| Date | Change |
| --- | --- |
| 2026-10-13 | Initial record. SCR-057 and SCR-058 as two workspace tabs on their own permissions; ADM-050–053 under `/admin` behind audit permissions, not R01; 9 decisions, 7 findings; verified with 37 new tests, 9 mutations and the workbook check in Chromium against the API (Frontend, TASK-074) |

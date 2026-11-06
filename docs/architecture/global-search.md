# Header Search

| Field | Value |
| --- | --- |
| Request | Client request, 2026-11-06: a general search in the header, for all users, that shows results immediately |
| Branch | `feat/global-header-search` |
| Status | **Built and tested locally**, including in a browser on a clone of the demo data with 100,000 extra risks (F-1) |
| API | `GET /api/v1/search?q=` (`GlobalSearch_Search`), any signed-in person |
| Screen | `src/frontend/src/app/search/GlobalSearch.tsx`, in the top bar of every signed-in page |

## 1. What it finds

| Kind | Matched on | Permission | Opens |
| --- | --- | --- | --- |
| Screens (in the SPA, not the API) | Label, in the current language | Those the person's menus offer | The screen |
| Projects | Title, formal id, legacy reference | `PROJECT_VIEW` | `/projects/:id` |
| Tasks | Title | `TASK_VIEW` | The project's task board (a task has no page of its own) |
| Risks | Title | `RISK_VIEW` | `/projects/:projectId/risks/:id` |
| Issues and challenges | Title | `CONCERN_VIEW` | `/projects/:projectId/issues-challenges/:id` |
| Change requests | Title | `CHANGE_REQUEST_VIEW` | `/change-requests/:id` |
| Documents | Title | `DOCUMENT_VIEW` | `/documents/:id` |

At most five of each kind, most recently changed first. Not searched: people, master data, descriptions and document contents.

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Open to every signed-in person; each kind filters by that person's own permission and scope.** The endpoint is `[AllowAnyAuthenticatedUser]`, and someone holding no view permission gets an empty result, not a 403 | A search spans permissions, so no single permission can guard it. An empty result is not a refusal (R-3), so a search writes nothing to the audit trail |
| D-2 | **A person finds a record exactly when its own page would open for them.** Projects and documents go through their own list services. Tasks, risks, concerns and change requests have only per-project lists, so `GlobalSearchRepository` turns the caller's `RecordScope` for the same view permission into SQL, over the same anchors each module's `SubjectOf` gives the engine. `GlobalSearch*Tests` assert both directions against the detail endpoint | Filtering rows after fetching them would drop matches past the limit. Asking per project would be one engine call per project |
| D-3 | **An assigned person finds their assigned task or concern, though the concern's list would not show it to them.** The search follows the record's detail decision, which includes the assignee | D-2's guarantee is the record's page, and that page opens for them |
| D-4 | **Screens match on each keystroke in the browser; records from two characters, 150 ms after typing stops.** The previous request is aborted when the text changes | Screens need no network, so they are immediate. One character would match nearly every record |
| D-5 | **The focus stays in the input (WAI-ARIA combobox).** Arrow keys move the active option, Enter opens it or the first option, Escape closes and then clears, and Ctrl/Cmd+K moves to the box from anywhere | Typing continues while results change. Ctrl+K overrides the browser's own shortcut for its address bar search on this site |

## 3. Findings

| # | Finding | Consequence if left |
| --- | --- | --- |
| F-1 | **Unindexed `ILIKE '%text%'`.** Each kind scans its title column, as the existing project and document list searches do. Measured 2026-11-06 on a local database of 167 projects and 100,000 risks, one user at a time: 16 to 46 ms per search, including a term that matches nothing. Concurrent users and the other kinds at that size were not measured. If it slows, the fix is a `pg_trgm` GIN index on those columns | Results lag behind typing on a much larger register or under load |
| F-2 | **Record search does not normalise Arabic spelling.** Screens match however alef, teh marbuta and yeh are written; records need the spelling as stored, so `اداره` does not find `إدارة` | Some Arabic searches miss records unless the person types the exact letters |
| F-3 | **A task opens the project's board, not the task.** No task page exists to link to | The person finds the task on the board themselves |

## 4. Rate limiting

None of its own: `RateLimitingTests` covers anonymous actions and sensitive writes, and this is neither. It is a signed-in read like the list endpoints.

## 5. Module boundary

`GlobalSearch` is a new module beside ADR-003 §4.3's 21, with two new query edges in `ModuleRegistry`: to `Project` (row 53) and `DocumentManagement` (row 54). It reaches them only through their `Contracts`. `GlobalSearchRepository` reads the task, risk, concern and change request tables directly, as a read across schemas.

**ADR-003 itself is outside this repository and still lists 21 modules.** It needs this revision recorded by its owner, the Engagement Architect.

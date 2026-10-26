# Document Library and Upload UI (WF-12 Frontend)

| Field | Value |
| --- | --- |
| Task | TASK-038 — Build Document Library & Upload UI (WF-12 Frontend) (P6 - Shared Platform Services) |
| Depends on | TASK-037 — the WF-12 API (`document-management.md` §3): every screen reads and writes through its operations, and every refusal shown is one it returns. TASK-032 and TASK-036 — the SPA foundation (`identity-access-administration-ui.md` D-1, `approvals-ui.md` D-7) |
| Record date | 2026-09-30 |
| Status | **BUILT AND VERIFIED LOCALLY**: component tests against a mocked API; the wire format against the compose API (§4 rows 8, 9); and **both workbook validation checks in headless Chromium against the real WF-12 API pipeline** with TASK-037's test scanner (§4.2). The real scanning provider is still owed (TASK-037 F-2). On the compose stack nothing can be uploaded: no document type or classification is published (TASK-037 F-4), and the one role that may upload cannot read the catalogues (F-1) |
| Branch | `feat/task-038-wf12-document-library-frontend` |
| Deliverables | React components and routes for SCR-120–124 and MOD-050–054 in `src/frontend/src/features/documents` (`library/`, `detail/`, `versions/`, `dialogs/`, `upload/`, `components/`, `routes.tsx`, `api/`); i18n files `features/documents/i18n/{ar,en}.json`. Additions to the foundation (D-8). 42 component and client tests. This record |
| Environment variables / secrets | None (the workbook row agrees) |
| Gate decision applied | None on the row |
| Participation amendment | None of its own. ADR-013's amendment (R04 uploads, views and reads version history on their own project) is enforced by the API (TASK-037 D-7); the screens show what it returns |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), Implementation Plan row TASK-038, as read 2026-09-30; `document-management.md`; `approvals-ui.md`; `api-conventions.md` R-8, R-21 |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | SCR-120–124 and MOD-050–054, Arabic and English, responsive | This task |
| **In** | Upload progress and the four scan states as the person sees them; quarantined content flagged and its evidence-relevant actions disabled | This task |
| **Out** | The request that attaches a document to a record (MOD-054 submits through the owning module) | TASK-050/051, TASK-066 (TASK-037 F-8) |
| **Out** | A project's name, and a title for a linked record | TASK-041 and the owning modules (F-2) |
| **Out** | The malware-scanning provider, the document taxonomy and who holds document permissions | TASK-037 F-1, F-2, F-4 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **The lists show exactly what `GET /documents` returns.** SCR-120 (all), SCR-121 (`projectId`) and SCR-122 (first 10) make one call per page and never filter, merge or add rows. What a person may open is decided on the server (TASK-037 D-6). A 403 is an error ("You do not have permission to do this with documents"), never an empty library. | A client-side filter could only hide, never enforce. |
| D-2 | **Routes under `/documents` need a session, not a role**, as `/approvals` (TASK-036 D-2). The session carries role codes, not permissions, so edit, archive and rescan are offered and a 403 is shown where the API gives one. | Appendix A will settle the roles (TASK-037 F-1); a role check here would go stale. |
| D-3 | **Six upload states, each with its own word, tone and explanation.** Uploading (`info`, a `<progress>` bar and "n% sent"), then the scan's state: scanning (`neutral`), clean (`positive`), quarantined (`negative`, `role="alert"`, a thicker danger edge), scan failed (`warning`); a refusal (413, 415, 422, 503, network) returns to the form with the message on the file or field it concerns. The same `ScanStateBadge` and `ScanStateNotice` are used on every screen. | Acceptance criterion 1. The word carries the meaning; colour only repeats it (WCAG 1.4.1). |
| D-4 | **Evidence-relevant actions need a CLEAN version.** Downloading a version, downloading evidence, and choosing a document in MOD-054 are disabled for SCAN_PENDING, SCAN_FAILED and QUARANTINED, with the reason beside the control (`aria-describedby`). A quarantined document is flagged by an alert on SCR-123, and its rows in SCR-120–122 and SCR-124 by a danger edge and badge. Uploading a new version stays enabled: it is the remedy. | Acceptance criterion 2. The API refuses the same (TASK-037 D-4, D-12, 409 `DOCUMENT_NOT_AVAILABLE`); a refusal that still arrives is shown. |
| D-5 | **A pending version is read again every 5 s** (`useScanWatch`) on MOD-050, MOD-052 and SCR-123, and never again once decided. A failed read stops polling and says the result is unknown. The worker scans every 30 s (TASK-037 `DocumentManagement:Scan`). | The person sees the verdict without reloading; the screen never claims a verdict it did not receive. |
| D-6 | **Uploads go through `XMLHttpRequest`** (`apiUpload`), because `fetch` reports no upload progress. It shares one exchange with `apiRequest`: bearer token, Idempotency-Key reused on the retries, 401 refreshed once, STEP_UP_REQUIRED stepped up once. Closing the dialog while a file is sent aborts the request. Downloads (`apiDownload`) read the attachment name from `Content-Disposition`, preferring `filename*`. | R-8, R-35; the progress the workbook row asks for. |
| D-7 | **SCR-124 lists every version the API returns**, newest first, unpaged (TASK-037 F-11): number, file name, size, uploader, upload time, scan state with its decision time, checksum. The uploader is named where USER_VIEW allows, otherwise "User 5a5a5a5a"; the signed-in person is "You". | Acceptance criterion 3. |
| D-8 | **Additions to the foundation, all backward compatible.** `httpClient`: `apiUpload`, `apiDownload`, `attachmentFileName`, one shared `exchange`. `FormFields`: `FileField`. `PageNotice` moves from approvals to `shared/ui`; `paging.ts` to `shared/api`. `usePersonNames` (identity-access) is the "You / name / User 1a2b3c4d" rule, now used by approvals too; its two strings move to `common.people`. The test `mockApi` answers `XMLHttpRequest` from the same routes and serves binary replies. The shell shows the documents navigation to anyone signed in. | DRY: approvals and documents used the same three helpers. |
| D-9 | **MOD-054 Attach is a component, not a route.** It lists the project's ACTIVE documents, disables the ones not CLEAN, and hands the chosen document to an `onAttach` the owning module supplies. | WF-12 exposes no link API: a module links in process after authorizing its own record (TASK-037 D-8, F-8). |
| D-10 | **Texts are tagged with the interface language** (as TASK-036 D-5). MOD-051 keeps a text's recorded language when it is not changed. | ADR-012 extension, ERD D-7. |
| D-11 | **MOD-052 reloads SCR-123 when the dialog closes**, not when the file is accepted: reloading under the dialog would unmount it and lose the scan it is showing. | Found by the Replace Version test. |
| D-12 | **`[hidden]` always hides** (`styles.css` base rule, `display: none !important`). MOD-050 and MOD-052 keep their form mounted but `hidden` while the file is sent and scanned (so a refusal returns to the same choices); `.form`'s own `display` had overridden the attribute and left the form showing under the progress. | Found by the browser run (§4.2); jsdom applies no stylesheet, so no component test could see it. |

## 3. Screens and routes

All under `/documents`, behind `RequireSession` (`features/documents/routes.tsx`).

| Screen | Route | Component |
| --- | --- | --- |
| SCR-120 Document Library | `/documents?q=&status=&page=` | `library/DocumentLibraryPage.tsx` (`DocumentLibraryPage`) over `components/DocumentBrowser.tsx` |
| SCR-121 Project Documents | `/documents/projects/:projectId?q=&status=&page=` | `library/DocumentLibraryPage.tsx` (`ProjectDocumentsPage`) |
| SCR-122 Recent Documents | `/documents/recent` | `library/RecentDocumentsPage.tsx` |
| SCR-123 Document Detail | `/documents/:documentId` | `detail/DocumentDetailPage.tsx`, `detail/DocumentLinks.tsx` |
| SCR-124 Version History | `/documents/:documentId/versions` | `versions/VersionHistoryPage.tsx` |
| MOD-050 Upload | on SCR-120, SCR-121 | `dialogs/UploadDocumentDialog.tsx` |
| MOD-051 Edit Metadata | on SCR-123 | `dialogs/EditMetadataDialog.tsx` |
| MOD-052 Replace Version | on SCR-123 | `dialogs/ReplaceVersionDialog.tsx` |
| MOD-053 Archive | on SCR-123 | `dialogs/ArchiveDocumentDialog.tsx` (the platform `ConfirmDialog`) |
| MOD-054 Attach | mounted by the owning module | `dialogs/AttachDocumentDialog.tsx` |

## 4. Verification

Run 2026-09-30 on macOS with Docker Desktop: frontend in `AHDA-frontend` (Node 24.21.0); the compose API `AHDA-api` on `AHDA-postgres` and `AHDA-ldap`. No other container was started.

| # | Command | Result |
| --- | --- | --- |
| 1 | `npm run lint` | 0 problems |
| 2 | `npm run format:check` | All matched files use Prettier code style |
| 3 | `npm run typecheck` | No errors |
| 4 | `npm test` | 19 files, 159 tests passed (117 before, 42 new: 37 in `features/documents`, 5 in `httpClient.test.ts`) |
| 5 | `npm run build` | Built. 787 KB JS (215 KB gzip); the >500 KB chunk warning is on `dev` already (F-5) |
| 6 | `npm audit` | 0 vulnerabilities. No dependency added |
| 7 | Mutation tests (§4.1) | Each of 8 mutations fails at least one test |
| 8 | curl, as `local.r04` on the compose API: `GET /documents`, `GET /documents?pageSize=10`, `GET /master-data-catalogues`, `GET /users/{id}` | 200 empty page, 200 empty page, **403**, **403** (F-1) |
| 9 | curl, as `local.r04`: `POST /documents` multipart with the fields `documentsApi.create` sends; then without the title | 422 `DOCUMENT_REFERENCE_INVALID` with `documentTypeItemId NOT_FOUND` (the fields were read; no type is published, TASK-037 F-4); 400 with `title.text`/`title.language REQUIRED` |
| 10 | The eight `docs/architecture/*-check.py` gates (contract-check needs a live OpenAPI run and is unchanged here) | All OK |

Not run: Lighthouse. Colour contrast was measured by axe in Chromium (§4.2); every new colour pair is an existing token pair (`approvals-ui.md` D-8; `--color-danger` on `--color-danger-surface` is `.form-alert`'s).

The tests, by acceptance criterion and validation check:

| Item | Tests |
| --- | --- |
| 1. Distinct states for uploading, scanning, clean and quarantined | `upload.test.tsx`: uploading (progress bar at the share sent, "100% sent", `info`), scanning (`neutral`), clean after one poll (`positive`), the three tones distinct; quarantined as an alert (`negative`) and no further poll; scan failed (`warning`); 413 and 415 on the file field with the form restored; 422 on its field; client-side refusal with no request; catalogues unreadable → submit disabled with the reason |
| 2. A quarantined document is flagged and its evidence-relevant actions disabled | `detail.test.tsx`: alert on SCR-123, download disabled with the quarantine reason, no content request, new version still possible; a pending version polled until clean, then downloadable. `versions.test.tsx`: only the CLEAN row downloadable, the quarantined row flagged. `library.test.tsx`: the quarantined row flagged, four scan tones distinct. `attach.test.tsx`: quarantined and pending documents cannot be chosen, each with its reason |
| 3. Version history shows every version with uploader and timestamp | `versions.test.tsx`: four versions by three people (one "You"), in API order, each row's uploader and formatted upload time; an unreadable uploader as "User 6b6b6b6b" |
| Validation: a large file shows progress | **Browser, real API** (§4.2 check 1); client: `upload.test.tsx`, `httpClient.test.ts` |
| Validation: EICAR shows QUARANTINED and disables download/evidence | **Browser, real API with the test scanner** (§4.2 check 2); client: as criterion 2. Against the real provider: owed (TASK-037 F-2) |
| SCR-120–122, MOD-051–053 | `library.test.tsx` (one call, links, filters in the request and URL, loading/empty/filtered-empty, 403 as error, unreadable names, navigation; SCR-121 `projectId`; SCR-122 `pageSize=10` only); `detail.test.tsx` (download with `filename*`, 409 shown, links and evidence download, 404, If-Match on edit and archive, 412, retired current type, archived disables changes, new version) |

### 4.1 Mutation tests

Each mutation was applied, `npx vitest run src/features/documents` run, and the source restored.

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | Any scanned state counts as usable | 4 |
| M-2 | Quarantined has the scanning tone | 4 |
| M-3 | Uploading shown as the scan's state | 8 |
| M-4 | No uploader in the version history | 2 |
| M-5 | A pending version is never read again | 4 |
| M-6 | The quarantine notice is a status, not an alert | 2 |
| M-7 | MOD-054 lets any document be chosen | 1 |
| M-8 | The version history drops a version | 4 |

### 4.2 Workbook validation in a browser

Run 2026-09-30. **API**: TASK-037's `DocumentTestHost` (the real API pipeline, a throwaway database in `AHDA-postgres` dropped afterwards, `AHDA-ldap`, a local-directory store, `TestMalwareScanner`, which flags the EICAR signature) served by a temporary harness on Kestrel port 5099 with the **shipped** upload policy (100 MiB) and a scan pass every 3 s. The harness also granted MASTER_DATA_VIEW to the test R02 profile (test data only; F-1 blocks MOD-050 otherwise), and signed `local.r02` in, answering the second factor with the test authenticator. **UI**: the production build (`vite preview`) proxied to it. **Browser**: Chromium 154 headless with puppeteer-core 24 and axe-core 4.13.0 **with colour contrast**, installed by apt and npm inside `AHDA-frontend` for the run. The harness file, Chromium, the fonts and the tooling were removed afterwards; none of it is in the repository. The run was repeated after the D-12 fix; the results below are from the second run.

| Check | Steps | Result |
| --- | --- | --- |
| 1. Upload a large file and confirm progress indication | MOD-050 with a 95 MiB file (random bytes, `.pdf`), upload throttled to 15 MiB/s through the DevTools protocol so the loopback transfer lasts long enough to watch; the dialog's `<progress>` was sampled every 100 ms | **Passed.** 70 distinct readings rising from 0 to 100 % over 7.2 s ("Uploading · 40% sent" with the bar at 40 %, screenshot); the form hidden (`display: none`); then "Scanning" (`badge--neutral`), then "Clean" (`badge--positive`) after a scan pass |
| 2. Upload the EICAR test file and confirm the UI reflects QUARANTINED and disables download/evidence actions | MOD-050 with the 68-byte EICAR file (`text/plain`); then SCR-123, SCR-124 and SCR-120 | **Passed.** Dialog: "Scanning", then an alert "Malware found: this file is quarantined" (`badge--negative`). SCR-123: the alert above the details; **Download disabled**, described by "Quarantined: malware was found. It cannot be downloaded or used as evidence."; "Upload new version" enabled. SCR-124: the row flagged (`row--flagged`), Download disabled, uploader "You" and the upload time shown. SCR-120: the row flagged with "Quarantined". The API, called with the SPA's own token: version QUARANTINED, content **409 `DOCUMENT_NOT_AVAILABLE`** |
| axe with colour contrast | Upload dialog (clean, quarantined), SCR-123 quarantined in English and Arabic, SCR-124, SCR-120 | No violation on any |
| Arabic and phone width | SCR-123 quarantined with `ar` | `<html dir="rtl">`, layout mirrored (screenshot); at 390 px no horizontal page scroll |

Evidence actions: no document was linked, so SCR-123 showed no evidence row. MOD-054, the only screen that chooses a document as evidence, is mounted by owning modules that do not exist yet (D-9), so its disabled choice for a quarantined document is verified by `attach.test.tsx`, not in the browser. The API refuses such a pin on its own (TASK-037 D-12).

## 5. Acceptance criteria, validation and deliverables

| Item | Result |
| --- | --- |
| Upload UI shows distinct states for uploading, scanning, clean and quarantined | **MET** (D-3; §4) |
| A quarantined document is visibly flagged and its evidence-relevant actions are disabled in the UI | **MET** (D-4; §4). Backend enforcement is TASK-037's |
| Version history displays every version with uploader and timestamp | **MET** (D-7; §4). An uploader is named only where USER_VIEW allows (F-1) |
| Validation: large file progress; EICAR shows QUARANTINED with download/evidence disabled | **MET in a browser against the real API pipeline** (§4.2), with the test scanner deciding EICAR. **Owed against the real scanning provider** (F-3) |
| Deliverables: components and routes for SCR-120–124 and MOD-050–054 | **MET** (§3). MOD-054 has no route by design (D-9) |

## 6. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **The uploader cannot read what a document is described with.** The type, classification and evidence-type labels come from `GET /master-data-catalogues` and `/master-data-items` (MASTER_DATA_VIEW, R01 only), and names from `GET /users/{id}` (USER_VIEW). R04, the one role that may upload (TASK-037 D-7), gets 403 for both (§4 row 8). MOD-050 then says so and cannot be sent; lists show "Item af000000" and "User 5a5a5a5a". Fixes: a read of a catalogue's PUBLISHED items open to any signed-in caller, or labels in the document representations | Engineering Architect (API); PMO (Appendix A) | No one but R01 can upload through the UI, even once types are published |
| F-2 | **A project and a linked record are shown by id** ("Project 7c7c7c7c", "Milestone · MILESTONE · e0000000"): no Project contract or owning-module title exists | TASK-041; TASK-050/051, TASK-066 | People must recognise projects by id |
| F-3 | **EICAR through the real scanning provider is owed.** Both validation checks passed in a browser against the real API pipeline (§4.2), but the QUARANTINED verdict came from TASK-037's test scanner: no provider is selected (TASK-037 F-2). Also owed in SIT: the same upload through the load balancer and Cloud Storage (TASK-037 F-3) | Security Lead (scanner); DevOps/Platform Lead; QA | A provider that reports differently (e.g. SCAN_FAILED for EICAR) is first seen in SIT |
| F-4 | **The upload policy is not published to the client** (100 MiB, eleven types, TASK-037 F-5), so a file is checked only after it is sent: a too-large file is refused at the end of its upload (413). A `GET` of the policy would let MOD-050 check first | Engineering Architect | Wasted uploads of files the server will refuse |
| F-5 | **The production bundle is 787 KB in one chunk** (721 KB after TASK-036, +66 KB). No route is code-split (TASK-036 F-7) | Frontend lead | Initial load grows with every module |
| F-6 | **Edit, archive and rescan are offered to everyone signed in** (D-2); no role holds DOCUMENT_MANAGE (TASK-037 F-1), so today they answer 403, shown in the dialog | PMO (Appendix A); Identity (permissions in the session) | Buttons that fail for most people |
| F-7 | **Delivery-team Arabic strings** have not had a bilingual business review (TASK-032 F-9) | PMO / AHDA business reviewer | Terminology may differ from AHDA's usage |
| F-8 | **Security Lead review (CTL-43) cannot be requested**: the CODEOWNERS teams do not exist (TASK-031 F-13). This change shows classification-dependent data and handles malware-flagged content | Maintainer | The PR's CTL-43 box stays unticked |

## 7. Change log

| Date | Change |
| --- | --- |
| 2026-09-30 | Created (TASK-038) |
| 2026-10-04 | MOD-054 first mounted, by TASK-051's MOD-019: `evidenceTypes` (a required "Evidence of" choice), `describe`, and the type passed to `onAttach` as a second argument; MOD-050 opened from MOD-019 too (`milestone-ui.md` D-8) |

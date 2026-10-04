# Document & Evidence Management (WF-12 Backend)

| Field | Value |
| --- | --- |
| Task | TASK-037 — Build Document & Evidence Management (WF-12 Backend) (P6 - Shared Platform Services) |
| Depends on | TASK-034 — Build Master Data & Configuration Service (`master-data-configuration.md`): document types, classifications and evidence types are resolved through `IMasterDataResolver`. TASK-030 (`authorization-engine.md`): every document operation is decided by `IAuthorizationEngine`. TASK-033 (`auth-access-audit-logging.md`): every change is audited through `IAuditTrail` |
| Record date | 2026-09-30 |
| Status | **BUILT AND VERIFIED LOCALLY** against `AHDA-postgres` and `AHDA-ldap`, in process and through the real API pipeline (§6). In a fresh environment it is inert: no malware-scanning provider is selected (F-2), no document type, evidence type or classification is published and no document permission has a clearance (F-4), and only the entity Project Manager holds document permissions (F-1) |
| Branch | `feat/task-037-wf12-document-management-backend` |
| Deliverables | **WF-12 upload / versioning / scan-status API**: `DocumentsController` and `EvidenceReferencesController` (13 operations, §3) over `IDocumentService`; the scan pass `DocumentScanning` and its worker. **Evidence-pinning service**: `IDocumentLinks` (`DocumentLinkService`) — link, unlink, designate and withdraw evidence, and the evidence a record holds. **Storage integration**: `IDocumentStorage` with the Cloud Storage adapter (`gs://`), the local-directory adapter (`file://`), and the connection-string parser. Supporting: `Domain/DocumentManagement`, three migrations, `RecordScope` in the authorization engine, three permissions, tests (§6) |
| Environment variables / secrets | `DOCUMENT_STORAGE_CONNECTION_STRING` (Secret, DEV/SIT/UAT/PROD), read now and optional: without it uploads and downloads answer 503. `MALWARE_SCAN_API_KEY` is **not read yet**: no provider is selected, and `ApplicationSecrets` lists only keys code reads (F-2). Two configuration sections: `DocumentManagement:Upload` (`MaxFileSizeBytes`, `AllowedContentTypes`, values in `appsettings.json`, F-5) and `DocumentManagement:Scan` (`PollInterval` 30 s, `BatchSize` 20). The workbook cell reads "None" (F-13) |
| Gate decision applied | None on the workbook row |
| Participation amendment | **ADR-013: entity Project Managers may upload, view and access version history on their own project within classification rules** — D-7 |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), Implementation Plan rows TASK-037, TASK-038, TASK-051, as read 2026-09-30; ERD §5 `document_management` and `master_data_config.evidence_requirement_rule`; `cybersecurity-control-matrix.md` CTL-20; `api-conventions.md` R-3, R-8, R-35, §4.4; `solution-architecture.md` §4.3 row 12, §5 (foundation modules), §8.2 edges 16–17; `indexing-strategy.md` I-31, I-32; Environment and Secrets rows `DOCUMENT_STORAGE_CONNECTION_STRING`, `MALWARE_SCAN_API_KEY`; `infra/terraform/storage` |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | Document → immutable DocumentVersion → BusinessLink (attachment or reference) → EvidenceReference, as ERD §5 draws them | This task |
| **In** | Secure upload, malware scanning with SCAN_PENDING / CLEAN / QUARANTINED / SCAN_FAILED, content served only when CLEAN | This task |
| **In** | Versioning that never alters historical evidence; unlink that never deletes; an explicit authorization check on every document | This task |
| **In** | The storage integration: the port, the Cloud Storage adapter, a local adapter for DEV and tests | This task |
| **In** | ADR-013's amendment as a shipped grant and as tested rules | This task |
| **Out** | SCR-120–124 and MOD-050–054 | TASK-038 |
| **Out** | The modules that attach documents and require evidence (milestone achievement, external contributions) | TASK-050/051, TASK-066 |
| **Out** | The malware-scanning provider and its adapter | Security Lead (F-2) |
| **Out** | Which roles read and manage documents (Appendix A); the classification taxonomy and clearances (UGV-01) | PMO, AHDA Cybersecurity (F-1, F-4) |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Module shape.** `Features/DocumentManagement` with `Contracts` (what the API and other modules see) and internal services; entities in `Domain/DocumentManagement`; tables in `document_management` exactly as ERD §5 draws them (`DocumentSchemaTests`). DocumentManagement is a platform foundation module: it references only `Application/Common` and the IdentityAccess and MasterDataConfig contracts, and never a domain module (A-1 to A-6 pass). | ADR-003 §4.3 row 12, §5. |
| D-2 | **Secure upload.** `POST /documents` and `POST /documents/{id}/versions` take `multipart/form-data` with the file in part `file` (R-8). The action reads the form itself, after setting the request limit to the upload policy's size (`DisableFormValueModelBinding`), so MVC does not read the body first under the server's defaults. A larger file is 413 `PAYLOAD_TOO_LARGE`, a media type outside the policy 415 `UNSUPPORTED_MEDIA_TYPE`. The object key is the platform's (`documents/<document>/<version>`), never the uploader's; the SHA-256 and size are taken from the bytes as they are stored (`HashingReadStream`); the file name kept is its last path segment without control characters; the media type is recorded without parameters and never served back (downloads are `application/octet-stream`). The uploader becomes the owner. | CTL-20 "secure upload"; R-8; §4.4 413/415. |
| D-3 | **Four scan states, decided by a provider port.** A version is born SCAN_PENDING (the database refuses any other state on insert). The scan worker sends pending versions to `IMalwareScanner`: *clean* → CLEAN, *infected* → QUARANTINED with the matched signature as `scan_reference`, *unscannable* → SCAN_FAILED. A scanner that cannot be reached leaves the version pending and moves it to the back of the queue (its `updated_at`), so one failing file does not starve the rest. CLEAN and QUARANTINED are final in the database; SCAN_FAILED goes back to SCAN_PENDING only through `rescan` (`DOCUMENT_MANAGE`). With no scanner configured nothing is scanned, so nothing becomes CLEAN: the check fails closed (F-2). Quarantined bytes are kept, never served. | CTL-20; the user's choice on 2026-09-30: provider-neutral port, fail closed. |
| D-4 | **Only CLEAN content leaves the store.** Both download paths — a version's content, and the evidence path (the version an evidence reference pins) — answer 409 `DOCUMENT_NOT_AVAILABLE` for SCAN_PENDING, SCAN_FAILED and QUARANTINED. An attempt on QUARANTINED content is audited (`QuarantinedContentWithheld`). Content is an attachment with `X-Content-Type-Options: nosniff` and `Cache-Control: no-store`. | Acceptance criterion 1; R-8. |
| D-5 | **A new file is a new version; a version never changes.** Its number is the document's latest plus one; the document's own row is touched in the same save, so two versions added at once cannot both commit (409 `DOCUMENT_VERSION_CONFLICT`), and the unique key (document, number) backs it. The database refuses any change to a version's file, key, name, size, checksum, uploader or document — only the scan verdict is written — and any change to a document's project or owner. An ARCHIVED document takes no version and no change. | Workbook: "later versions never alter historical evidence". |
| D-6 | **Access is an explicit check on the document, never project membership.** Every operation asks the engine with the document's anchors: its project, that project's owning department and delivering entity, its owner and its classification. A project-bound assignment with no document grant opens nothing; a grant whose scope or clearance does not cover the document is 404, like an unknown id (R-47), and the refusal is audited. Collections are filtered in SQL by `IAuthorizationEngine.GetRecordScopeAsync`, added here: a user's grants of one permission as clauses on a record's anchors, built from the same rules as a single decision (`RecordScopeTests`), so a list never shows what a person cannot open, nor hides what they can. | Acceptance criterion 3; `authorization-engine.md` F-8. |
| D-7 | **ADR-013's amendment is R04 at ENTITY for `DOCUMENT_VIEW` and `DOCUMENT_UPLOAD`.** ENTITY reaches a document only when its project's delivering entity is the holder's, so an internal R04 holder gains nothing from these grants. An external holder's assignment is bound to one project (ADR-013), so the entity Project Manager uploads, views and reads version history on that project and no other project of their own entity. `DOCUMENT_MANAGE` (edit, archive, rescan) is not theirs. "Within classification rules" is the permissions' clearance (ADR-010): nothing classified above it is uploaded or read. | Participation amendment; the user's choice on 2026-09-30 (R04 at ENTITY only). |
| D-8 | **Unlinking never deletes.** Modules attach documents in process through `IDocumentLinks` (ADR-003 §8.2 edges 16 and 17): the module authorizes its own record, DocumentManagement the document (the actor must be able to read it). Linking the same document, target and role again returns the active link. Unlinking records when and by whom, and withdraws the link's VALID evidence; the link, the document, every version and every file remain. The database refuses a delete on any of the four tables, and any change to a link except its unlinking, once. | Acceptance criterion 2; ERD "unlink never deletes". |
| D-9 | **Three permissions, group `DOCUMENT_MANAGEMENT`.** `DOCUMENT_VIEW` (Read: find, read, version history, links, evidence, download CLEAN content), `DOCUMENT_UPLOAD` (Write: a document, a version), `DOCUMENT_MANAGE` (Write: metadata, archive, rescan). Shipped grants: R04 `DOCUMENT_VIEW` and `DOCUMENT_UPLOAD` at ENTITY (D-7). Nothing else, pending Appendix A (F-1). | TASK-030's rule: only source-backed grants ship. |
| D-10 | **Storage.** `IDocumentStorage` writes an object once and never replaces or deletes it. `DOCUMENT_STORAGE_CONNECTION_STRING` is `gs://<bucket>[/<prefix>]` — the environment's private, versioned bucket from `infra/terraform/storage`, reached with the runtime's service account through Application Default Credentials, so the value holds no key; objects are created with generation precondition 0 — or `file:///<absolute path>` for DEV and tests. A value in neither form stops the API at start-up without the value being logged; no value means 503 on upload and download. The bucket name is checked as written (`Uri` lower-cases hosts). | Environment and Secrets row; CTL-20; ADR-001 C-4. |
| D-11 | **Every change is audited** through `IAuditTrail`, in the saving transaction: `DocumentUploaded`, `VersionAdded`, `MetadataChanged`, `DocumentLinked`, `DocumentUnlinked`, `EvidenceDesignated`, `EvidenceWithdrawn` (DATA_CHANGE); `DocumentArchived`, `ScanCompleted`, `ScanRequeued` (LIFECYCLE_TRANSITION); `QuarantinedContentWithheld` (AUTHORIZATION_DENIAL, recorded on its own). Each carries the project, entity and classification anchors. Titles and descriptions are free text: a change records that they changed, not their values. | event-conventions row 14; CTL-25. |
| D-12 | **Evidence is pinned, and only CLEAN evidence satisfies.** `DesignateEvidenceAsync` pins a version of the link's own document, on an active link, as a PUBLISHED EVIDENCE_TYPE; a version that is not CLEAN is 409 `DOCUMENT_NOT_AVAILABLE`, and the database refuses such a pin too (`ck_evidence_reference_clean_version`). `GetSatisfiedEvidenceTypesAsync(target)` answers which evidence types the record holds: a VALID reference, on a link not ended, pinned to a CLEAN version — the set TASK-050 compares with its EVIDENCE_POLICY requirement. A later version is a new pin; the earlier one stays as it was. | Acceptance criterion 1; ERD `evidence_reference` note. |
| D-13 | **Three migrations, one schema each** (migrations README R-7): `TASK-037_CreateDocumentManagementTables`, `TASK-037_AddDocumentManagementCrossModuleForeignKeys`, `TASK-037_GuardDocumentHistory`. Three unique keys whose generated names exceed 63 characters are named. I-31 and I-32 are built; the scan queue (a partial index on pending versions) and the link-target lookup are this module's own queries. | TASK-024 conventions; `indexing-strategy.md` §2. |
| D-14 | **A service principal** `svc.document-scan` (`…00fa`, `DocumentServicePrincipal.Id`) authors every scan result, so the row has a `updated_by` (ERD D-2). | ERD D-2. |
| D-15 | **413 and 415 are refusal kinds** of the shared `AdministrationError`, mapped by the controller base, so a module decides them like any other refusal. | §4.4. |

## 3. Endpoints

All paths are under `/api/v1`, tag `DocumentManagement`, operation ids `DocumentManagement_*`. Every non-2xx answer is the R-23 envelope; every `POST`/`PUT` is a sensitive write (R-35) and also answers 400 `IDEMPOTENCY_KEY_REQUIRED`/`…_INVALID`. Linking and evidence are in process only (D-8).

| Operation | Method and path | Permission | Success | Refusals beyond 401/403 |
| --- | --- | --- | --- | --- |
| `ListDocuments` | `GET /documents?projectId=&status=&q=&page=&pageSize=` | `DOCUMENT_VIEW` | 200 `DocumentPage`, only documents the caller may open, most recently changed first | 400 |
| `GetDocument` | `GET /documents/{id}` | `DOCUMENT_VIEW` | 200 `DocumentDetail` with the latest version, `ETag` | 404 |
| `CreateDocument` | `POST /documents` multipart: `file`, `title.text`, `title.language`, `description.*`, `documentTypeItemId`, `dataClassificationItemId`, `projectId?` | `DOCUMENT_UPLOAD` | 201, `Location`, `ETag`; version 1 SCAN_PENDING | 400; 413; 415; 422 `DOCUMENT_REFERENCE_INVALID`; 503 |
| `UpdateDocument` | `PUT /documents/{id}` `{title, description?, documentTypeItemId, dataClassificationItemId}`, `If-Match` required | `DOCUMENT_MANAGE` | 200 | 404; 409 `TERMINAL_STATE`; 412; 422; 428 |
| `ArchiveDocument` | `POST /documents/{id}/archive` | `DOCUMENT_MANAGE` | 200 | 404; 409 `TERMINAL_STATE`; 412 |
| `ListDocumentVersions` | `GET /documents/{id}/versions` | `DOCUMENT_VIEW` | 200, newest first (SCR-124) | 404 |
| `AddDocumentVersion` | `POST /documents/{id}/versions` multipart: `file` | `DOCUMENT_UPLOAD` | 201, `Location` | 404; 409 `TERMINAL_STATE`, `DOCUMENT_VERSION_CONFLICT`; 413; 415; 503 |
| `GetDocumentVersion` | `GET /documents/{id}/versions/{versionId}` | `DOCUMENT_VIEW` | 200 | 404 |
| `DownloadDocumentVersion` | `GET /documents/{id}/versions/{versionId}/content` | `DOCUMENT_VIEW` | 200 `application/octet-stream`, attachment | 404; 409 `DOCUMENT_NOT_AVAILABLE`; 503 |
| `RescanDocumentVersion` | `POST /documents/{id}/versions/{versionId}/rescan` | `DOCUMENT_MANAGE` | 200, SCAN_PENDING | 404; 409 `INVALID_TRANSITION` |
| `ListDocumentLinks` | `GET /documents/{id}/links` | `DOCUMENT_VIEW` | 200, every link, ended ones included, with its evidence | 404 |
| `GetEvidenceReference` | `GET /evidence-references/{id}` | `DOCUMENT_VIEW` (on the document) | 200 `EvidenceReferenceDetail` with `satisfies` | 404 |
| `DownloadEvidence` | `GET /evidence-references/{id}/content` | `DOCUMENT_VIEW` (on the document) | 200, the pinned version's bytes | 404; 409 `DOCUMENT_NOT_AVAILABLE` |

## 4. Error codes

`DocumentErrorCodes` (R-27), and the platform codes this module is the first to return:

| Code | Status | When |
| --- | --- | --- |
| `DOCUMENT_NOT_AVAILABLE` | 409 | Content or evidence of a version that is not CLEAN (D-4, D-12) |
| `DOCUMENT_REFERENCE_INVALID` | 422 | A document type, classification, evidence type or project that is unknown or not PUBLISHED |
| `DOCUMENT_VERSION_CONFLICT` | 409 | Another version of the document was added at the same moment (D-5) |
| `DOCUMENT_VERSION_MISMATCH` | 422 | In process: the version is not of the link's document |
| `DOCUMENT_LINK_ENDED` | 422 | In process: evidence on, or a new link repeating, an ended link (F-7) |
| `DOCUMENT_EVIDENCE_WITHDRAWN` | 422 | In process: re-designating withdrawn evidence (F-7) |
| `PAYLOAD_TOO_LARGE` (platform) | 413 | Above `DocumentManagement:Upload:MaxFileSizeBytes` |
| `UNSUPPORTED_MEDIA_TYPE` (platform) | 415 | Not multipart, or a media type outside `AllowedContentTypes` |

## 5. How a module uses it

1. **Attach.** In the command that records the business change, call `IDocumentLinks.LinkAsync(actor, documentId, new BusinessTarget("<Module>", "<Type>", id), role)` after authorizing the actor on your own record. The link saves with your staged changes.
2. **Pin evidence.** `DesignateEvidenceAsync(actor, new EvidenceDesignation(linkId, versionId, evidenceTypeItemId))`. A version not CLEAN is refused: show the scan state and let the user wait.
3. **Check a requirement.** `GetSatisfiedEvidenceTypesAsync(target)` against the evidence types your EVIDENCE_POLICY row makes mandatory (TASK-050). Never count a link or a reference yourself: only this query applies the CLEAN rule.
4. **Detach.** `UnlinkAsync(actor, linkId)`. Nothing is deleted; the link's evidence is withdrawn.
5. **Record what was relied on.** When your record is accepted, keep the evidence reference ids it relied on (M-4: identity only). They and their pinned versions never change.

## 6. Verification

Run 2026-09-30 on macOS, Docker Desktop, PostgreSQL 17 (`AHDA-postgres`) and `AHDA-ldap`. No other container was started.

| # | Command | Result |
| --- | --- | --- |
| 1 | `dotnet build src/backend -warnaserror`, and `--configuration Release -warnaserror` | Build succeeded, 0 warnings, 0 errors, both |
| 2 | `dotnet test src/backend/PMPlatform.Tests.Unit` | 486 passed (425 on `dev`): 34 new in `RecordScopeTests` (4), `DocumentUploadTests` (14), `DocumentStorageTests` (16); 27 new `ShippedGrantTests` cases (the two R04 cells both ways, 22 withheld cells, `DocumentViewAndUploadGoToTheEntityProjectManagerOnly`). Architecture tests A-1 to A-6 pass |
| 3 | `DB_CONNECTION_STRING=… dotnet test src/backend/PMPlatform.Tests.Integration` | 334 passed (312 on `dev`): 22 new — `EvidenceAvailabilityTests` 3, `UnlinkTests` 3, `DocumentAuthorizationTests` 4, `ScanLifecycleTests` 7, `DocumentEndpointTests` 4, `DocumentSchemaTests` 1. Two count assertions moved with the catalogue (`SeedDataTests` 100 → 103 labels, `AdministrationContractTests` 16 → 19 permissions). `MigrationRollbackTests` runs the three new Downs |
| 4 | `.github/scripts/migration-dry-run.sh` and `seed-dry-run.sh` on a scratch database in `AHDA-postgres` (dropped after) | "1055 statement(s), applied twice, no destructive statement"; "12 table(s) seeded, unchanged by a second run, no integrity violation" |
| 5 | `docker compose -f infra/docker/docker-compose.yml up -d --build --wait api` | Migrates to `20260930164532_TASK-037_GuardDocumentHistory`; integrity check: no violation in 102 foreign keys, 70 check constraints, 176 indexes, 45 audited tables, 21 master data references |
| 6 | On the compose API, with curl: sign in as `local.r02`, `local.r04`, `local.r08`; `GET /documents` and a multipart `POST /documents` each | `local.r02`, `local.r08` (no document grant): 403 and 403. `local.r04` (internal R04, ENTITY): 200 with an empty page, and 422 `DOCUMENT_REFERENCE_INVALID` — no document type is published (F-4). No token: 401 |
| 7 | `python3 docs/architecture/contract-check.py <openapi v1 of the running API>` | 92 operations, 422 findings; 65 on the 13 document operations: C-4 13, C-5 13, C-7 7, C-8 1, C-12 15 (the R-52 classes the platform does not emit yet, as TASK-035 F-11), C-2 6, C-9 8, C-11 2 (F-11) |
| 8 | The nine `docs/architecture/*-check.py` gates | All OK |

The tests, by acceptance criterion, validation check and amendment:

| Item | Tests |
| --- | --- |
| 1. QUARANTINED or SCAN_PENDING cannot satisfy an evidence requirement or be downloaded through the evidence path | `AnEicarUploadIsQuarantinedAndIsNeverServedNorEvidence` — the workbook's check with the test scanner (F-2): pending, then QUARANTINED with the signature; 409 on content both times; the attempt audited; pinning refused in the application and in the database; the target satisfies nothing; the bytes kept. `ANewVersionNeverAltersEvidencePinnedToAnEarlierOne` (a pending version cannot be pinned; the earlier pin's rows and served bytes are identical after v2). `APinnedVersionsVerdictCanNeverBeChangedInTheDatabase`; `AnUndecidableFileIsScanFailedUntilItIsQueuedAgainAndDecided`; `WithoutAScannerNothingIsEverClean` |
| 2. Unlinking does not delete the file or version history; the version stays retrievable by an authorized query | `UnlinkingACleanDocumentDeletesNothingAndItStaysRetrievable` — the workbook's check: after unlink, every row and the file are still there; the version list, the content by both paths (same checksum) and the ended link with its withdrawn evidence are returned to an authorized caller. `AnEndedLinkStaysEnded`; `TheDatabaseRefusesToDeleteOrReopenDocumentHistory` (9 statements refused; a version born CLEAN refused) |
| 3. An explicit authorization check, not merely project membership | `ProjectMembershipAloneOpensNoDocument` (a project member with no grant: 403 on four calls; a DEPT holder who is a member of another department's project: 404, audited). `EachPersonsListIsExactlyTheDocumentsTheyMayOpen` (for three people, every document: listed ⇔ opens). `TheScopeMatchesExactlyTheRecordsTheEngineAllows` (unit, 8,000 comparisons) |
| ADR-013 amendment | `TheEntityProjectManagerUploadsViewsAndSeesHistoryOnTheirOwnProjectOnly` (`local.r08`: upload, second version, history, detail on their project; refused on another project of their entity, another entity's, another department's and the library; a document on another project of their entity is 404; managing is 403). `NoOneReachesADocumentAboveTheirClearance`. `ShippedGrantTests` ENTITY cases (unit) |
| Scan and store behaviour | `AScannerThatCannotBeReachedLeavesTheVersionPendingForTheNextPass`, `WithoutAStoreNothingIsUploadedAndTheApiStillStarts`, `AStoreInNeitherAcceptedFormStopsTheApi` (3 cases; the value is not in the message), `DocumentStorageTests` |
| API and schema | `DocumentEndpointTests` (idempotency key, 415 twice, 413, 400 field errors, 422 references, 201 with `Location` and `ETag`, a path-stripped file name, the stored checksum, no storage key in the body; attachment headers; `If-Match` 428/412, reclassification above clearance 403, archive and its 409s; unknown ids 404). `DocumentSchemaTests` |

### 6.1 Mutation tests

Each mutation was applied, the solution rebuilt, the authorization and document tests run, and the source restored and touched.

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | Content served whatever the scan state | `AnEicarUploadIsQuarantinedAndIsNeverServedNorEvidence`, `AnUndecidableFileIsScanFailedUntilItIsQueuedAgainAndDecided`, `WithoutAScannerNothingIsEverClean` |
| M-2 | Evidence pinned whatever the scan state (application check removed; the database's remains) | `ANewVersionNeverAltersEvidencePinnedToAnEarlierOne`, `AnEicarUploadIsQuarantinedAndIsNeverServedNorEvidence` |
| M-3 | A document's project anchor dropped | `EachPersonsListIsExactlyTheDocumentsTheyMayOpen`, `ProjectMembershipAloneOpensNoDocument`, `TheEntityProjectManagerUploadsViewsAndSeesHistoryOnTheirOwnProjectOnly` |
| M-4 | A record-scope clause ignores the per-project bound | `TheScopeMatchesExactlyTheRecordsTheEngineAllows`, `TheEntityProjectManagersClauseIsTheirProjectAndTheirEntity`, `EachPersonsListIsExactlyTheDocumentsTheyMayOpen`, `TheEntityProjectManagerUploadsViewsAndSeesHistoryOnTheirOwnProjectOnly` |
| M-5 | A malware verdict recorded as CLEAN | `AnEicarUploadIsQuarantinedAndIsNeverServedNorEvidence` |
| M-6 | The list ignores classification | `EachPersonsListIsExactlyTheDocumentsTheyMayOpen`, `NoOneReachesADocumentAboveTheirClearance` |
| M-7 | Unlinking leaves evidence VALID | `UnlinkingACleanDocumentDeletesNothingAndItStaysRetrievable`, `TheDatabaseRefusesToDeleteOrReopenDocumentHistory` |
| M-8 | The satisfied-evidence query counts ended links | **None.** Unlinking withdraws the link's evidence, so no VALID reference on an ended link exists for the query to count; the filter guards only a designation racing an unlink (F-18) |
| M-9 | Upload not authorized | `TheEntityProjectManagerUploadsViewsAndSeesHistoryOnTheirOwnProjectOnly`, `NoOneReachesADocumentAboveTheirClearance` |

## 7. Acceptance criteria, validation and amendment

| Item | Result |
| --- | --- |
| A document QUARANTINED or SCAN_PENDING cannot satisfy an evidence requirement or be downloaded through the normal evidence path | **MET.** D-3, D-4, D-12; §6 criterion 1; M-1, M-2, M-5 |
| Unlinking a document from a business context does not delete the underlying file or version history | **MET.** D-8; §6 criterion 2; M-7 |
| Access to a document requires an explicit authorization check, not merely project membership | **MET.** D-6; §6 criterion 3; M-3, M-4, M-6, M-9 |
| Validation: an EICAR upload lands QUARANTINED and cannot be retrieved via the evidence endpoint | **MET against the test scanner**, which proves what the platform does with a malware verdict. **Owed against the real provider**: none is selected (F-2) |
| Validation: unlinking a clean document leaves the DocumentVersion retrievable by an authorized audit query | **MET.** `UnlinkingACleanDocumentDeletesNothingAndItStaysRetrievable` |
| Deliverables: upload/versioning/scan-status API, evidence-pinning service, storage integration | **MET** (header row "Deliverables"). The Cloud Storage adapter is not exercised against a bucket (F-3) |
| Description: the Document → DocumentVersion → BusinessLink → EvidenceReference model, secure upload, four scan states, versioning, unlink-never-deletes | **MET** (D-2 to D-8, D-12), per the workbook, the ERD, CTL-20 and R-8: Blueprint Section 13 itself is not in the repository (F-12) |
| ADR-013: entity Project Managers upload, view and access version history on their own project within classification rules | **MET.** D-7; §6 amendment row |

## 8. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **Only R04 holds document permissions, at ENTITY** (the user's choice, 2026-09-30). No internal role reads, uploads or manages documents, and no one holds `DOCUMENT_MANAGE`: Blueprint Appendix A is not in the repository (`authorization-engine.md` F-1). The tests grant the permissions to test roles | PMO (Appendix A) | In a real environment no AHDA user can see a document (§6 row 6); only entity Project Managers can use WF-12 |
| F-2 | **No malware-scanning provider is selected.** Nothing is scanned, so nothing becomes CLEAN, is served or is evidence. The EICAR validation ran against a test double; the provider's run ("EICAR test file is correctly flagged QUARANTINED", the sheet's verification) is owed. `MALWARE_SCAN_API_KEY` is read by no code yet, and the sheet scopes it to SIT/UAT/PROD, so **DEV will never scan** unless DEV gets a scanner too. The rotation pattern is open (`secret-management.md` F-5) | Security Lead (provider); PMO (DEV scope) | WF-12 accepts uploads but serves nothing until a provider adapter replaces `UnconfiguredMalwareScanner` |
| F-3 | **The Cloud Storage adapter has not touched a bucket**: no environment exists (ADR-001 Q1–Q22). Owed: the sheet's verification, "upload/download round-trip succeeds via WF-12 API", in SIT | DevOps/Platform Lead | A fault in the adapter or the runtime's IAM surfaces at first use |
| F-4 | **Nothing to classify or type a document with.** No DOCUMENT_TYPE, EVIDENCE_TYPE or DATA_CLASSIFICATION item is published, and no permission has a clearance (UGV-01). Every document is classified (the column is NOT NULL) and a permission without clearance clears nothing (`authorization-engine.md` D-6), so no document can be uploaded (422) or read in any environment until all three exist | AHDA Cybersecurity (taxonomy, clearances); PMO (document and evidence types) | WF-12 is inert |
| F-5 | **The upload policy is the delivery team's**: 100 MiB and eleven media types (PDF, Word, Excel, PowerPoint in both formats, PNG, JPEG, plain text, CSV); archives are excluded because they hide content from scanners. No source states either | Security Lead | Larger files or other types are refused until configured |
| F-6 | **A cross-module read.** `DocumentRepository.FindProjectAnchorsAsync` and the list query read `project.project` (department, delivering entity) directly: DocumentManagement is a foundation module and may call no domain module, and Project publishes no contract yet. The same read as `identity-access-administration.md` F-6 | Engineering Architect (ADR-003); TASK-041 (a Project anchors contract) | The read moves when a contract exists |
| F-7 | **The ERD's unique keys include ended links and withdrawn evidence.** A document unlinked from a target cannot be linked to it again in the same role, and withdrawn evidence cannot be designated again for the same link, version and type (`DOCUMENT_LINK_ENDED`, `DOCUMENT_EVIDENCE_WITHDRAWN`). Partial unique keys (`WHERE unlinked_at IS NULL`, `WHERE status = 'VALID'`) would allow both | Engineering Architect (ERD) | A re-attachment needs the other role or a new document |
| F-8 | **No API links or pins.** MOD-054 Attach attaches through the owning module's API, which calls `IDocumentLinks`; Milestone and ExternalParticipation do not exist yet | TASK-038; TASK-050, TASK-066 | The attach modal has no endpoint until those modules do |
| F-9 | **Unreferenced and quarantined objects stay in the bucket.** If the save after a store write fails, the object is orphaned; no sweeper exists. Quarantined bytes are kept in the same private bucket (never served), not moved to a separate one | DevOps/Platform Lead; Security Lead | Storage grows by failed uploads; malware stays at rest, encrypted and private |
| F-10 | **Reads are not audited.** A CLEAN download or a document read is not an audit event: no audit class covers access, and CTL-25 names denials. Refusals and QUARANTINED attempts are | Security Lead; TASK-073 | No trail of who downloaded which evidence |
| F-11 | **The OpenAPI document misses R-49 to R-55** on the 13 operations (§6 row 7), as on every earlier module. Two lists are unpaged (C-9, C-11): versions and links, small per-document sets. C-2 flags R-8's own binary path `…/versions/{versionId}/content` and the `rescan` command as deeper than R-3 allows: the conventions contradict each other there | TASK-011, TASK-015, TASK-094; Engineering Architect (R-3 against R-8) | As TASK-031 F-3 |
| F-12 | **Blueprint Section 13 is not in the repository** (`solution-architecture.md` S-1). The runtime follows the workbook row, the ERD, CTL-20 and R-8 | PMO (document custody) | A Section 13 rule absent from those sources is missing |
| F-13 | **The workbook cell for environment variables reads "None"**, though this task reads `DOCUMENT_STORAGE_CONNECTION_STRING` and owns `MALWARE_SCAN_API_KEY` (`environment-templates.md` F-1) | PMO (workbook edit) | The workbook understates the task's secrets |
| F-14 | **A project's lifecycle is not consulted.** A document can be uploaded to a CLOSED project: the project's state is Project's, and no contract exposes it | TASK-041, TASK-063 | Closed projects accept documents until the Project contract carries the state |
| F-15 | **Idempotency keys are required, not replayed** (TASK-031 F-2): a retried upload creates a second document | Engineering Architect (`common.idempotency_record`) | Duplicate documents on a client retry |
| F-16 | **Security Lead review (CTL-43) cannot be requested**: the CODEOWNERS teams do not exist (TASK-031 F-13). This change touches RBAC and the engine | Maintainer | The PR's CTL-43 box stays unticked |
| F-17 | **`NarrativeText` is `{text, language}`**, not R-18's `{text, lang}`; the multipart fields follow it (`title.text`, `title.language`), as TASK-035 F-12 records | Engineering Architect | As TASK-035 F-12 |
| F-18 | **M-8 is not caught by a test**: the ended-link condition in the satisfied-evidence query guards only a designation committed while an unlink of the same link is in flight | — | None while the unlink withdraws evidence |

## 9. Change log

| Date | Change |
| --- | --- |
| 2026-09-30 | Created (TASK-037) |
| 2026-10-03 | TASK-050 (`milestone-achievement.md` D-8): Milestone is the first module to attach and pin evidence (§8.2 edge 16), on the achievement revision as the target (`Milestone`/`MilestoneAchievement`), and checks EVIDENCE_POLICY's mandatory types with `GetSatisfiedEvidenceTypesAsync` at submission. F-8 is met for Milestone: MOD-054 attaches through `POST /milestone-achievements/{id}/evidence`. F-7 shows there: withdrawn evidence is pinned again only as a new version (`milestone-achievement.md` F-9). |
| 2026-10-04 | TASK-052 (`financial-kpi.md` D-6): FinancialKpi links and pins documents to an Approved Budget version (§8.2 edge 38, target `FinancialKpi`/`FinancialCommitment`), and requires at least one satisfied evidence type before the version is submitted — the ADR-008 gate's referenced document. No EVIDENCE_POLICY applies: any CLEAN evidence of any type counts. |

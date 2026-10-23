# Migration Data Import (TASK-102)

| Field | Value |
| --- | --- |
| Task | TASK-102 — Confirm Data Migration Cutover & Legacy Decommission (P18 - Release & Go-Live): the mechanism the cutover loads legacy records through. The workbook has no task for the import itself; it is filed under the task that needs it |
| Depends on | TASK-089's legacy-data plan (`db/migrations/legacy-data-migration.md`, Blocked on OQ-008); the owning modules' create paths; TASK-027's integrity validator |
| Record date | 2026-10-23 |
| Status | **Built and verified locally** for the reference registers (§3), through the real API pipeline against `AHDA-postgres` and `AHDA-ldap`. Projects and the project registers follow (§6) |
| Deliverables | The engine `Application/Common/DataImport` (`DataImportService`, `IImportRegister`, the cell parser and the report); the workbook `Infrastructure/DataImport/XlsxImportWorkbook.cs`; the lineage tables `common.import_batch` and `common.imported_record` (migration `TASK-102_CreateImportLineage`); one register per importable register in its owning module; per register `GET …/import-template/content` and `POST …/imports`; permission `DATA_IMPORT`; the SPA's Import action on each register page and the Data migration page (`/admin/data-migration`) |
| Environment variables / secrets | None |

## 1. Why the platform defines the workbook

The legacy-data plan is Blocked on OQ-008: AHDA has not said what system holds its records, in what format, or how its fields map
(legacy-data-migration.md §2, Q-1 to Q-5). The platform cannot write a mapping for a source nobody has described, but it can say
exactly what it accepts. Each register publishes a template: the columns a row must fill, the type and length of each, the codes a
reference may name. AHDA's data owner fills it from whatever the source is, and the mapping becomes theirs to make and sign off
(Guide §9.5 step 2) against a fixed target. Q-3 ("database export, file export, or an API") is answered on the platform side: a file,
in this format.

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **All or nothing, every row reported.** A workbook loads in one transaction. Each row is written behind a savepoint; a refused row is rolled back to it and the run goes on, so the report lists every problem in the file, by row and column. Unless every row passed and the import was asked for, the transaction is rolled back. A validation is the same run with the commit withheld: it writes exactly what the import would and keeps none of it, so it finds what the database itself would refuse | Guide §9.5 steps 6–7: counts reconcile only if a file loads whole or not at all, and rejected records are documented. A dry run that only parsed would pass rows the module or a constraint refuses |
| D-2 | **One permission, R01 at ALL, held by no other role.** `DATA_IMPORT` (group `DATA_IMPORT`, privileged) on every import endpoint; the endpoints' module permissions are not asked. Each committed batch is a `PRIVILEGED_ACTION` audit event, `<Module>.RecordsImported`, with the register, the file's SHA-256 and the counts; each row is audited by its module as a create on screen is | The import loads records in their legacy state around approval workflows (D-7), so it is an administrator's operation, not a business role's. PRIVILEGED_ACTION puts the batch in the administration audit category R01 reads (FG-06 §16) |
| D-3 | **Lineage in `common`.** `common.import_batch` (who, when, which file by SHA-256, counts) and `common.imported_record` (register, legacy ID, the record it became, the row), both APPEND_ONLY. `(register_code, legacy_id)` is unique: a row whose legacy ID was loaded before is **skipped**, so loading a workbook again creates nothing (BE-027: "re-run import without duplicate identities"). A later row, or a later register, names a migrated record by its legacy ID through this table | Every register writes lineage, so it belongs to no module, like the outbox (ERD D-17). The entity id is an opaque value (D-14): the record may be in any module's table |
| D-4 | **Each register lives in the module that owns its rows and writes through that module's own path.** A register implements `IImportRegister` in `Features/<Module>` and writes each row through the module's service, the same call the form makes, so an imported row meets every rule, constraint and audit a typed one does. The engine knows nothing of any module. The endpoints are on the module's controller, under its tag (R-51): `GET <collection>/import-template/content` (R-8 binary download) and `POST <collection>/imports` (R-8 multipart, part `file`; `commit=true` imports, otherwise validates; `[SensitiveWrite]`) | ADR-003 M-1 and API-03: a module writes only its own facts. A generic importer writing tables directly would bypass the module's invariants and breach the module boundary on the first row |
| D-5 | **References by business key.** A department, entity or catalogue item is named by its code; a user by username; a record migrated in an earlier register by its legacy ID. Registers are loaded in an order where each names only what is above it (the Data migration page lists them so). A reference that names nothing is `REFERENCE_NOT_FOUND` on its column | A legacy system has no platform ids. Codes are what the template's Reference values sheet lists and what the data owner can check |
| D-6 | **Governed rows land DRAFT.** Master data items and KPI definitions follow the governed lifecycle (ERD D-12): the importer is their author, and two other administrators validate and publish them as for one entered on screen. No register may skip a separation of duties | Importing them PUBLISHED would let one administrator author, review and publish the platform's code lists alone, defeating TASK-110's control for exactly the data a migration loads in bulk |
| D-7 | **Other rows land in their legacy state** where the module has a path to it: a department inactive, an entity SUSPENDED or RETIRED, a user DISABLED is created and then moved there through the module's own command, each step audited. A row never gets a state its module has no command for | The gate decision for TASK-102: migrated records arrive as they are, not as new drafts re-entering every workflow |
| D-8 | **The workbook is untrusted input.** XLSX only, read directly (ADR-005, like the report export) with no spreadsheet library: no DTD is processed and nothing resolved (XXE), every part is capped at 64 MB decompressed whatever the archive declares (decompression bombs), reading stops past 5,000 rows, a file is at most 10 MB. Only element local names are matched, so transitional and strict workbooks read alike; a formula's cached value is read, never the formula. Every cell obeys `TextInput` (TASK-077), then its column's shape (`ImportCellParser`) | The upload is the one place a user hands the platform a structured document to parse. The limits keep one import within one transaction's reach |
| D-9 | **The template is the documentation.** Sheet *Data* has the headers (required ones highlighted), every column formatted as text so a code such as `007` or a date typed `2025-01-31` stays as typed, and drop-down lists on choice and TRUE/FALSE columns. Sheet *Columns* gives each column's required flag, type, length, allowed values and a description in Arabic and English. Sheet *Reference values* lists the codes a reference column accepts today (published items, active departments and entities). A header the register does not have is refused (`COLUMN_UNKNOWN`), so a typo cannot drop a column silently | The data owner fills the template without reading this record |

## 3. Registers

| Register | Code | Owning module | Template columns (required in bold) | Lands as |
| --- | --- | --- | --- | --- |
| Departments | `DEPARTMENT` | IdentityAccess | **legacy_id**, **code**, **name_ar**, **name_en**, parent_code, directory_reference, is_active | Active, or created then deactivated |
| Master data items | `MASTER_DATA_ITEM` | MasterDataConfig | **legacy_id**, **catalogue_code**, **code**, **label_ar**, **label_en**, description_ar, description_en, parent_code, sort_order | DRAFT (D-6) |
| External entities | `EXTERNAL_ENTITY` | IdentityAccess | **legacy_id**, **code**, **name_ar**, **name_en**, **entity_type_code**, sponsor_username, status | ACTIVE, or created then SUSPENDED / RETIRED |
| Users | `USER` | IdentityAccess | **legacy_id**, **user_type**, **username**, **display_name**, **email**, mobile_number, preferred_language, directory_subject_id (required if INTERNAL), job_title (EXTERNAL only), external_entity_code (required if EXTERNAL), status | ACTIVE, or created then DISABLED. No role assignment: an assignment binds a profile version (ADR-018) and is made on ADM-010 |
| KPI definitions | `KPI_DEFINITION` | MasterDataConfig | **legacy_id**, **code**, **name_ar**, **name_en**, description_ar, description_en, **unit_code**, **direction** | DRAFT (D-6) |

Issue codes in a report: a cell's shape uses the validation codes (`REQUIRED`, `MAX_LENGTH`, `MALFORMED`, `ENUM_VALUE`,
`INVALID_CHARACTER`); the import adds `COLUMN_MISSING`, `COLUMN_UNKNOWN`, `COLUMN_DUPLICATE`, `NO_ROWS`, `DUPLICATE_IN_FILE`,
`REFERENCE_NOT_FOUND`, `ALREADY_EXISTS`; a module's refusal keeps its own code (`NOT_FOUND`, `INACTIVE`, `DUPLICATE`, `NOT_ALLOWED`, or a
module code such as `IDENTITY_ACCESS_DEPARTMENT_CYCLE`), under the column the refused field comes from.

## 4. Verification

| Check | Evidence |
| --- | --- |
| The engine | `Tests.Unit/Application/Common/DataImport`: header mismatches refused before a row is written; every bad cell and refused row reported; a refused row undone; a validation keeps nothing; an import commits rows, lineage and one audited batch; an earlier legacy ID skipped; a later row resolves an earlier row's legacy ID; refusals before any row is read. `ImportCellParserTests`: every kind, dates as ISO text or serial, TextInput |
| The workbook | `Tests.Unit/Infrastructure/DataImport/XlsxImportWorkbookTests`: the template reads back as its header, with drop-downs, both languages and a formula-like reference value quote-prefixed; shared strings, inline strings, numbers, booleans, phonetic runs and an absolute relationship target read as a spreadsheet shows them; a non-workbook, a DTD and a part decompressing past the cap are refused; reading stops at the row limit. The template was also opened with openpyxl 3.1.5: three sheets, required headers filled, text-formatted columns, frozen header |
| Through the API | `Tests.Integration/DataImport` (CI step "DataImport integration tests"): each register's template, validation and import against PostgreSQL, the rows as the database holds them, lineage, the audit events, a second load skipping every row, a refused row leaving nothing, non-workbooks refused 415, other roles refused 403; every import endpoint declares `DATA_IMPORT` and every register has both endpoints |
| The SPA | `features/data-import/*.test.tsx`: template download, a check listing problems by row and column with no import offered, a clean check committing and reloading the register, a non-workbook explained; the Data migration page in load order in both languages with no axe violation, refused to every role but R01 |

## 5. Findings

| # | Finding | Owner | Consequence until resolved |
| --- | --- | --- | --- |
| F-1 | **OQ-008 is still open.** The templates fix the target, not the source: which records move, how much history, the legacy code lists and who signs the mapping are AHDA's (legacy-data-migration.md Q-1, Q-2, Q-4 to Q-12) | AHDA IT / Data Owner | The import can be rehearsed on synthetic or masked data (Guide §9.3) but no cutover plan can be signed |
| F-2 | **Governed rows need two more administrators** (D-6), one item at a time on ADM-020–029's API: there is no bulk validate or publish | PMO | A large legacy code list takes as many validate and publish calls as items |
| F-3 | **Role assignments are not imported.** A user arrives with no role; ADM-010 binds each to a profile version | PMO | Assignments are made by hand after the user load |

## 6. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-10-23 | Initial record: the engine, the lineage, `DATA_IMPORT`, the reference registers (departments, master data items, external entities, users, KPI definitions), the SPA's Import action and Data migration page | Backend / Frontend (TASK-102) |

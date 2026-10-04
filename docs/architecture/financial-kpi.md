# Financial Progress & KPI Performance (WF-14 Backend)

| Field | Value |
| --- | --- |
| Task | TASK-052 — Build Financial Progress & KPI Performance (WF-14 Backend) (P8 - Execution & Performance) |
| Depends on | TASK-044 — Build Progress Update & Overall Health (`progress-update.md`): WF-02's reporting periods, read through the new `IReportingCycleReader` (ADR-003 §8.2 edge 13). TASK-041 — Build Project Creation & Registration (`project-registration.md`): the project's anchors and lifecycle state through `IProjectFactsReader` (edge 5). TASK-035 (`approval-framework.md`): approval of commitment and target versions through WF-11. TASK-037 (`document-management.md`): the Approved Budget's referenced document. TASK-030, TASK-033 and TASK-034: authorization and masking, audit, configuration and the KPI catalogue |
| Record date | 2026-10-04 |
| Status | **BUILT AND VERIFIED LOCALLY** against `AHDA-postgres` and `AHDA-ldap`, in process through the real API pipeline, WF-11, DocumentManagement and the outbox (§7). In a real environment no one can enter, review or configure anything until Appendix A grants it (F-2); nothing can be published until AHDA sets the financial-status thresholds (F-4); no Approved Budget can be approved until AHDA publishes a COMMITMENT route, and no KPI target until a KPI_TARGET route (F-4) |
| Branch | `feat/task-052-wf14-financial-kpi-backend` |
| Directory | `src/backend/PMPlatform.Application/Features/FinancialKpi` |
| Deliverables | **WF-14 financial/KPI services, two distinct subdomains.** Financial Progress: `IFinancialSourceModeService`, `IFinancialCommitmentService`, `IFinancialProgressService`. KPI Performance: `IKpiAssignmentService`, `IKpiTargetVersionService`, `IKpiMeasurementService`. Seven controllers, 46 operations (§4); `EventHandlers/FinancialKpiApprovalOutcomeHandler` applies WF-11's outcome. **Snapshot publication**: `published_financial_snapshot`, written once at publication with every figure copied and the status rated under pinned thresholds, and never changed (migration `TASK-052_GuardFinancialKpiHistory`). **Aggregation compatibility checks**: `FinancialAggregation` (currency verified as SAR) and `KpiAggregation` (one KPI unit), each listing what it left out and marking the aggregate partial. **Tests**: 54 unit, 15 integration, 12 mutations (§7). Supporting: the `financial_kpi` schema (three migrations), `IReportingCycleReader` (Progress contract, edge 13), eight permissions with ADR-013's grants, the R-20 masked-field filter, a CI step |
| Environment variables / secrets | None |
| Gate decision applied | **ADR-008, SCOPED** — D-3, D-4, D-6, D-8 |
| Participation amendment | **ADR-013**: an entity sees budget, expenditure and KPI status for its own project, masked by audience per ADR-010 — D-10, D-12. **ADR-008 extended**: every financial record carries source, source reference, as-of date and entered-by; the cost breakdown aligns to Etimad categories from day one — D-4 |
| Sources read | The TASK-052 row as supplied on 2026-10-04 (description, acceptance criteria, directory, deliverables, gate decision, participation amendment); the workbook itself was not opened, and no WF-14 functional specification was available (F-5). ERD §5.17, §7 rows 17, 18, 28, F-059 to F-068, E-4, §11 (TASK-052 gate reconciliation); `erd.dbml` `financial_kpi.*`; `solution-architecture.md` §8.2 edges 5, 12, 13, 14, 26, 29, S-4; `api-conventions.md` R-2 to R-5, R-16, R-20, R-21, R-27 to R-29, R-40, R-47; `indexing-strategy.md` I-39, I-40; `authorization-engine.md` D-6, D-8, §3; `ptbc-themes.csv` PTBC-024, PTBC-025; `unassigned-gated-values.csv` UGV-01; `progress-update.md` F-1, F-13; `milestone-achievement.md` and `schedule-baseline.md` for the WF-11 and evidence patterns |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | Financial Progress: source mode per project and per field; Approved Budget versions approved through WF-11, each with a referenced document; periodic actuals and forecast per WF-02 period, reviewed and published by AHDA; immutable Published Financial Snapshots; the CURRENT/LIVE position | This task |
| **In** | KPI Performance: KPI assignments; target versions approved through WF-11 and immutable once ACTIVE; periodic measurements pinned to the target version in force when recorded | This task |
| **In** | Portfolio aggregation of financial totals and KPI values, only over compatible figures | This task |
| **Out** | The integrated source adapter (Etimad or other) | Not scheduled (F-7) |
| **Out** | The DECLARED_BUDGET and opening spend-to-date of a legacy intake (ADR-014) | TASK-104 (F-13) |
| **Out** | WF-08's change authorisation of a budget change | TASK-060 (F-3) |
| **Out** | SCR-049, SCR-071 and the KPI screens | TASK-053 |
| **Out** | Overall Health reading the financial status | WF-02, once §8.2 allows it (`progress-update.md` F-1) |
| **Out** | Contract tests across the execution domain | TASK-054 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Two subdomains, one module (Blueprint Section 8.1).** `Features/FinancialKpi` holds Financial Progress (`FinancialSourceModeService`, `FinancialCommitmentService`, `FinancialProgressService`, `FinancialStatusRule`, `FinancialAggregation`, `FinancialPolicy`) and KPI Performance (`KpiAssignmentService`, `KpiTargetVersionService`, `KpiMeasurementService`, `KpiRagRule`, `KpiAggregation`), each with its own contracts. They share only access, audit, mapping, the repository port and the WF-11 outcome handler. Entities in `Domain/FinancialKpi`; repository and configurations in `Infrastructure/Persistence`. The module references Project's, Progress's, Approval's and DocumentManagement's contracts (edges 5, 13, 26, 38) and the universal ones; A-1 to A-6 pass | ADR-003 §4.3 row 14 |
| D-2 | **Unknown is never 0 (acceptance criterion 2; R-20(a)).** Every figure — actual, forecast, measured value — is present exactly when its `valueStatus` is MEASURED; MISSING, STALE and NOT_APPLICABLE carry `null`, never `"0.00"`. The rule is checked in the application (422 `FINANCIAL_KPI_VALUE_STATUS_INVALID`) and in the database (`ck_financial_progress_update_value_status`, `ck_kpi_measurement_value_status`). A new update starts MISSING with every figure null: nothing is presumed. A financial status is UNKNOWN unless the actuals are MEASURED and a positive budget and a forecast are present; a RAG is UNKNOWN without a value or thresholds and NOT_APPLICABLE for an N/A period — `ck_published_financial_snapshot_financial_status` and `ck_kpi_measurement_rag_status` refuse a colour without its figures. On the wire the property is present and `null` | Acceptance criterion 2 |
| D-3 | **SAR only (ADR-008 gate).** Every amount is the platform's `Money`, whose currency is SAR by construction: no currency column, no conversion, no rate source; amounts cross the wire as R-16 decimal strings. Project total only: `financial_commitment_line` and `financial_progress_update_line` exist (the amendment: "aligns to Etimad categories from day one", ERD §11) and no operation writes them (F-8). OPEN_COMMITMENT is in the value sets and refused (422 `FINANCIAL_NOT_IN_USE`) | Gate decision |
| D-4 | **Provenance on every financial record (ADR-008 extended, ERD D-10).** `source_type`, `source_reference`, `as_of_date` and `entered_by_user_id` on every commitment, update and snapshot. A request writes MANUAL provenance with the caller as `entered_by` and an optional document or system reference; the as-of date is required and not after today (422 `FINANCIAL_KPI_AS_OF_DATE_INVALID`). A non-manual source must name its reference (`ck_*_source_reference`). The snapshot copies the update's provenance | Amendment |
| D-5 | **Approved versions through WF-11 (S-4, edge 26).** A commitment version and a KPI target version share one lifecycle (`ApprovedVersionStatus`, `ApprovedVersionWorkflow`, 7 edges, §3): opened DRAFT, submitted to WF-11 — routing keys `COMMITMENT` (with the amount, for materiality routing) and `KPI_TARGET`, the ERD's APPROVAL_AUTHORITY codes — and ended by the outcome: APPROVED → ACTIVE, superseding the previous ACTIVE version of the project and type, or of the assignment, which keeps everything but its status and successor; RETURNED → RETURNED, edited and resubmitted as revision + 1 under a new run (TASK-035 D-9); REJECTED and WITHDRAWN are final. One version is on its way at a time (409 `FINANCIAL_COMMITMENT_OPEN`, `KPI_TARGET_OPEN`). The requester is the person who entered it, so WF-11 keeps them from approving it. One `FinancialKpiApprovalOutcomeHandler` applies both subject types in the dispatch transaction, saving the superseded row first because the ACTIVE partial index is checked row by row; a stale outcome is audited as ignored (EV-5). An activation with no effective date takes the activation date | ERD E-4; S-4's consequence |
| D-6 | **A referenced document for any change to the Approved Budget (ADR-008 gate; edge 38).** The ERD names it an EvidenceReference, so the document is linked through DocumentManagement to the version and one of its CLEAN versions pinned (`POST /financial-commitments/{id}/documents`), as Milestone does (edge 16). Submission requires at least one satisfied evidence type (422 `FINANCIAL_BUDGET_DOCUMENT_REQUIRED`). Every APPROVED_BUDGET version needs one — the first sets the budget, each later one changes it. Documents change only while the version is DRAFT or RETURNED, and each change touches the version's row, so a submission that read the documents before fails 412. Periodic actuals and forecast need none (the gate). §8.2 gains edge 38 (F-1) | Gate decision; ERD `change_authorization_id` note |
| D-7 | **Periodic figures on WF-02's periods (edge 13).** `IReportingCycleReader`, new in Progress's contracts, returns a project's periods as stored; WF-14 generates none (F-11). An update is for the earliest begun period without published financial figures, one at a time (409 `FINANCIAL_UPDATE_EXISTS`, `FINANCIAL_NOTHING_TO_REPORT`), on an ACTIVE project. Its workflow is WF-02's: DRAFT → SUBMITTED → UNDER_REVIEW → RETURNED or PUBLISHED (`FinancialUpdateWorkflow`); a return opens revision + 1 with the returned figures. One revision of a period is open at a time, and one snapshot is published per period (two unique indexes beyond the ERD, F-10) | ERD `financial_progress_update.reporting_cycle_id` "period alignment with WF-02 (edge 13)" |
| D-8 | **Source mode per project and per field (ADR-008 gate).** A field with no row is MANUAL. `financial_source_mode` sets APPROVED_BUDGET, ACTUAL_EXPENDITURE or FORECAST_AT_COMPLETION to MANUAL, INTEGRATED or HYBRID. INTEGRATED and HYBRID are built — stored, enforced — and left unconnected: no adapter exists (F-7). Once a field is INTEGRATED, a figure for it is refused when entered by hand (422 `FINANCIAL_FIELD_INTEGRATED`), so during an outage — which, unconnected, is always — the field stays Unknown; and it never returns to MANUAL or HYBRID (409 `FINANCIAL_SOURCE_MODE_LOCKED`; `ck_financial_source_mode_integrated` for any writer), since that switch would be the manual substitution the gate forbids. HYBRID accepts manual figures | Gate decision |
| D-9 | **Two views, never merged (M-12; R-20(c)).** `published_financial_snapshot` is PUBLISHED/OFFICIAL: written at publication with the Approved Budget in force, the figures and provenance copied, and `financial_status` rated under the WORKFLOW_POLICY version pinned on it (ERD D-13); never updated or deleted (APPEND_ONLY, guarded). The CURRENT/LIVE position (`GET /financial-positions`) is computed on read and stored nowhere: the ACTIVE Approved Budget beside the latest revision submitted for review or published, rated under the thresholds in force now, or UNKNOWN when there are none. Each representation states its `semanticState`. The live view may therefore show figures AHDA has not yet published; the published view never does | Description; acceptance criterion 2 |
| D-10 | **Eight permissions; only the amendment's grants ship.** Group `FINANCIAL`: `FINANCIAL_VIEW` (Read), `FINANCIAL_SUBMIT`, `FINANCIAL_REVIEW`, `FINANCIAL_SOURCE_MANAGE` (Write). Group `KPI`: `KPI_VIEW` (Read), `KPI_MANAGE` (assignments and targets), `KPI_RECORD`, `KPI_REVIEW` (Write). R04 and R08 hold the two views at ENTITY, which reaches only a holder with an entity: ADR-013's "an entity sees budget, expenditure and KPI status for its own project" (the reading TASK-041 gave R04 and R08 for project visibility). Nothing else ships (F-2) | `authorization-engine.md` D-9 |
| D-11 | **Aggregation only over compatible figures (acceptance criterion 3).** `GET /financial-portfolio-aggregates?projectId=…` (1 to 200 projects) totals the latest snapshots, or the live positions with `semanticState=CURRENT_LIVE`. A project counts only when its figures are visible to the caller, verified as SAR (`FinancialFigures.CurrencyCode`: every stored figure is `Money`; a future integrated source states its own and is never converted), MEASURED and complete; any other is listed in `exclusions` with its reason (`NOT_AVAILABLE`, `CURRENCY_UNVERIFIED`, `VALUE_NOT_MEASURED`, `MASKED`, `NO_PUBLISHED_FIGURE`) and `isPartial` is true. `GET /kpi-portfolio-aggregates?kpiDefinitionId=…&projectId=…` takes the latest PUBLISHED measurement of each KPI on each project; `meanValue` is computed only when every KPI measures in one KPI_UNIT (`isUnitCompatible`), otherwise it is null and the aggregate partial; RAG counts are unitless and always given. With nothing counted every total is null and `coverage` NONE | Acceptance criterion 3 |
| D-12 | **Masked by audience (ADR-010; R-20(b); CTL-19).** Each representation is built with the engine's `GetFieldMaskAsync` under the read permission, for entity codes `FinancialCommitment`, `FinancialProgressUpdate`, `PublishedFinancialSnapshot` and `KpiMeasurement`, field codes being the JSON property names. A withheld field is null in the application's record and named in `maskedFields`; `[OmitMaskedFields]` then omits it from the JSON, so the SPA can tell *restricted* from *Unknown*. An aggregate leaves a masked project out (`MASKED`). FIELD_CLASSIFICATION classifies nothing yet (UGV-01), so in a real environment nothing is masked (F-6) | Amendment |
| D-13 | **Pinned measurements (acceptance criterion 1).** Recording a measurement pins the assignment's ACTIVE target version (422 `KPI_TARGET_NOT_APPROVED` without one) and stores the RAG rated against it (`KpiRagRule`, by the KPI's direction; ERD §7 row 17). Editing a DRAFT re-rates it against the version it is pinned to, never the one in force now. A target version is immutable from ACTIVE: a new target is a new version, and superseding one leaves it as approved. The database refuses any change of `kpi_target_version_id`, period or assignment, any change of value or RAG once submitted, and an insert not pinned to the ACTIVE version of its own assignment (`ck_kpi_measurement_pinned_target`) | Acceptance criterion 1 |
| D-14 | **RAG thresholds per target version.** GREEN and AMBER, both or neither, in the KPI's unit: values for HIGHER_IS_BETTER and LOWER_IS_BETTER (GREEN at or past the GREEN value), distances from the target for TARGET_BAND. Incoherent ordering is 422 `KPI_THRESHOLDS_INVALID`. Without thresholds every rating is UNKNOWN. KPI_POLICY's thresholds and `calculationExpression` are not used (F-5) | ERD `kpi_target_version.green_threshold` "per-target RAG thresholds (OQ-006)" |
| D-15 | **Review is AHDA's gate.** Starting the review, returning and publishing a financial update (`FINANCIAL_REVIEW`), and publishing a measurement (`KPI_REVIEW`), are refused to an external user whatever they hold, and to the person whose figures they are (the submitter; the recorder): 403, audited `ReviewRefused` with the reason `EXTERNAL_USER` or `SUBMITTER`. Neither is a WF-11 run: edge 26 covers only versions | ADR-013: entities see, AHDA governs |
| D-16 | **Everything is audited** in the saving transaction (event-conventions §4 row 21): data changes with their figures (narratives withheld), lifecycle transitions with the state change, WF-11's run and decision, the version superseded, the snapshot with its status and pinned thresholds | event-conventions EV-9 |

## 3. Workflows

Approved versions — `financial_commitment` (APPROVED_BUDGET) and `kpi_target_version`:

| From | To | Cause | Who |
| --- | --- | --- | --- |
| — | DRAFT | `POST` | `FINANCIAL_SUBMIT` / `KPI_MANAGE` on the project |
| DRAFT, RETURNED | SUBMITTED | `submit` (a commitment needs its referenced document; RETURNED → revision + 1) | as above |
| SUBMITTED | ACTIVE | WF-11 APPROVED; the previous ACTIVE becomes SUPERSEDED | the final approver |
| SUBMITTED | RETURNED, REJECTED, WITHDRAWN | WF-11's decision | the decider, or the requester who withdrew |

A period's financial update: DRAFT → SUBMITTED (`FINANCIAL_SUBMIT`) → UNDER_REVIEW → RETURNED (revision + 1 opened) or PUBLISHED (snapshot written), the last three by `FINANCIAL_REVIEW`, internal, not the submitter. A measurement: DRAFT → SUBMITTED (`KPI_RECORD`) → PUBLISHED (`KPI_REVIEW`, internal, not the recorder). An assignment: ACTIVE ↔ SUSPENDED, either → RETIRED.

## 4. Endpoints

All under `/api/v1`, tag `FinancialKpi`, operation ids `FinancialKpi_*`. Every non-2xx answer is the R-23 envelope; every `POST` and `PUT` also answers 400 `IDEMPOTENCY_KEY_REQUIRED`/`…_INVALID`; `PUT` requires `If-Match` (428, 412), commands honour it. A collection of a project the caller may not see is an empty page; a record is 404, or 403 when visible but not theirs to change (R-47).

| Resource | Operations |
| --- | --- |
| `/financial-source-modes` | `GET ?projectId=` (every field, MANUAL where unset), `GET /{id}`, `POST {projectId, fieldCode, sourceMode}`, `PUT /{id} {sourceMode}` — 409 `FINANCIAL_SOURCE_MODE_LOCKED`; 422 `FINANCIAL_NOT_IN_USE` |
| `/financial-commitments` | `GET ?projectId=`, `GET /{id}`, `POST {projectId, amountSar, effectiveFrom, sourceReference, asOfDate}`, `PUT /{id}`, `DELETE /{id}` (DRAFT; 204 also when absent, R-40), `POST /{id}/submit`, `GET /{id}/documents`, `POST /{id}/documents {documentId, documentVersionId, evidenceTypeItemId}`, `POST /{id}/documents/{evidenceReferenceId}/withdraw` — 409 `FINANCIAL_COMMITMENT_OPEN`, `FINANCIAL_KPI_VERSION_NOT_EDITABLE`, `INVALID_TRANSITION`, `DOCUMENT_NOT_AVAILABLE`; 422 `FINANCIAL_KPI_PROJECT_NOT_ELIGIBLE`, `FINANCIAL_FIELD_INTEGRATED`, `FINANCIAL_BUDGET_DOCUMENT_REQUIRED`, `FINANCIAL_KPI_AS_OF_DATE_INVALID`, `CONFIGURATION_MISSING` |
| `/financial-progress-updates` | `GET ?projectId=`, `GET /{id}`, `POST {projectId}`, `PUT /{id} {actualExpenditureToDateSar, forecastAtCompletionSar, valueStatus, narrative, sourceReference, asOfDate}`, `DELETE /{id}`, `POST /{id}/submit`, `/start-review`, `/return {reason}`, `/publish` — 409 `FINANCIAL_NOTHING_TO_REPORT`, `FINANCIAL_UPDATE_EXISTS`, `FINANCIAL_KPI_NOT_EDITABLE`, `INVALID_TRANSITION`; 422 `FINANCIAL_KPI_PROJECT_NOT_ELIGIBLE`, `FINANCIAL_KPI_VALUE_STATUS_INVALID`, `FINANCIAL_FIELD_INTEGRATED`, `CONFIGURATION_MISSING` (publish) |
| `/published-financial-snapshots`, `/financial-positions` | `GET ?projectId=` — PUBLISHED/OFFICIAL latest first (I-40); CURRENT/LIVE, one item |
| `/financial-portfolio-aggregates` | `GET ?projectId=…&semanticState=PUBLISHED_OFFICIAL\|CURRENT_LIVE` |
| `/kpi-assignments` | `GET ?projectId=`, `GET /{id}`, `POST {projectId, kpiDefinitionId, ownerUserId, measurementFrequencyItemId}`, `PUT /{id}`, `POST /{id}/suspend`, `/reactivate`, `/retire` — 409 `KPI_ALREADY_ASSIGNED`, `TERMINAL_STATE`; 422 `KPI_REFERENCE_INVALID` |
| `/kpi-target-versions` | `GET ?kpiAssignmentId=`, `GET /{id}`, `POST {kpiAssignmentId, targetValue, greenThreshold, amberThreshold, effectiveFrom}`, `PUT /{id}`, `DELETE /{id}`, `POST /{id}/submit` — 409 `KPI_TARGET_OPEN`; 422 `KPI_ASSIGNMENT_NOT_ACTIVE`, `KPI_THRESHOLDS_INVALID` |
| `/kpi-measurements` | `GET ?kpiAssignmentId=`, `GET /{id}`, `POST {kpiAssignmentId, periodStart, periodEnd, measuredValue, valueStatus, asOfDate, narrative}`, `PUT /{id}`, `DELETE /{id}`, `POST /{id}/submit`, `/publish` — 409 `KPI_MEASUREMENT_EXISTS`; 422 `KPI_TARGET_NOT_APPROVED`, `KPI_PERIOD_INVALID`, `FINANCIAL_KPI_VALUE_STATUS_INVALID` |
| `/kpi-portfolio-aggregates` | `GET ?kpiDefinitionId=…&projectId=…` |

The review history of a version is WF-11's `/approval-instances?subjectModule=FinancialKpi&subjectType=FinancialCommitment|KpiTargetVersion&subjectId=`.

## 5. Error codes

`FinancialKpiErrorCodes` (R-27), beside the platform's `INVALID_TRANSITION`, `TERMINAL_STATE` and `CONFIGURATION_MISSING`: `FINANCIAL_KPI_PROJECT_NOT_ELIGIBLE` (422: budget, assignments and targets on an APPROVED_PLANNED or ACTIVE project, periodic figures on an ACTIVE one), `FINANCIAL_FIELD_INTEGRATED` (422), `FINANCIAL_SOURCE_MODE_LOCKED` (409), `FINANCIAL_NOT_IN_USE` (422), `FINANCIAL_COMMITMENT_OPEN` (409), `FINANCIAL_KPI_VERSION_NOT_EDITABLE` (409), `FINANCIAL_BUDGET_DOCUMENT_REQUIRED` (422), `FINANCIAL_NOTHING_TO_REPORT` (409), `FINANCIAL_UPDATE_EXISTS` (409), `FINANCIAL_KPI_NOT_EDITABLE` (409), `FINANCIAL_KPI_VALUE_STATUS_INVALID` (422), `FINANCIAL_KPI_AS_OF_DATE_INVALID` (422), `KPI_REFERENCE_INVALID` (422), `KPI_ALREADY_ASSIGNED` (409), `KPI_ASSIGNMENT_NOT_ACTIVE` (422), `KPI_TARGET_OPEN` (409), `KPI_THRESHOLDS_INVALID` (422), `KPI_TARGET_NOT_APPROVED` (422), `KPI_MEASUREMENT_EXISTS` (409), `KPI_PERIOD_INVALID` (422).

## 6. How other modules build on it

1. **TASK-053** (WF-14 UI): render `null` with its `valueStatus` as Unknown/N/A, never 0; a property absent and named in `maskedFields` as restricted; `semanticState` to label the official and the live views; `isPartial`, `coverage` and `exclusions` on every aggregate.
2. **TASK-063** (Closure, edge 14): read the latest snapshot and the live position; a query contract is added to `FinancialKpi.Contracts` then.
3. **TASK-069/071** (edge 29): a read projection over `published_financial_snapshot` and the live position; never recalculate the status.
4. **WF-02**: once §8.2 gives Progress an edge to FinancialKpi, `IProgressInputs` reads `financial_status` from the latest snapshot (`progress-update.md` F-1).
5. **TASK-060**: `IFinancialCommitmentService` gains the change authorisation of a budget change (edge 12) and `fk_financial_commitment_…_change_authorization_id` (F-3).
6. **TASK-104**: the DECLARED_BUDGET (born ACTIVE, with `project_intake_id`) and the opening spend-to-date update (F-13).
7. **AHDA**: COMMITMENT and KPI_TARGET routes in APPROVAL_AUTHORITY; `FINANCIAL_STATUS_AMBER_OVERRUN_PERCENT` and `…_RED_…` in WORKFLOW_POLICY (PTBC-025); KPI definitions, units and MEASUREMENT_FREQUENCY items; targets (PTBC-024); FIELD_CLASSIFICATION (UGV-01); the Appendix A grants.

## 7. Verification

Run 2026-10-04 on macOS, Docker Desktop, PostgreSQL 17 (`AHDA-postgres`) and `AHDA-ldap`, with `NUGET_PACKAGES=/Volumes/SanDisk/Bader/Development/Caches/nuget`. Counts on `dev` (4d1744a): 1035 unit; 576 integration (590 less the 14 tests this task added to that run).

| # | Command | Result |
| --- | --- | --- |
| 1 | `dotnet build src/backend/PMPlatform.slnx -warnaserror`, and `--configuration Release -warnaserror` | 0 warnings, 0 errors, both |
| 2 | `dotnet test src/backend/PMPlatform.Tests.Unit` | 1089 passed, 54 new: `KpiRagRuleTests` 22, `FinancialKpiWorkflowTests` 13, `FinancialStatusRuleTests` 9, `PortfolioAggregationTests` 8, and `ShippedGrantTests` 2 (`FinancialsAndKpisAreSeenByTheEntityOnly`, `AnEntitySeesTheFinancialsOfItsOwnProjectsOnly`); the shipped-grant theories cover the eight new permissions. A-1 to A-6 pass with edge 38 registered |
| 3 | `DB_CONNECTION_STRING=… dotnet test src/backend/PMPlatform.Tests.Integration` | 591 passed, 15 new: `KpiPerformanceTests` 3, `UnknownValueTests` 2, `PortfolioAggregateTests` 2, `FinancialProgressTests` 6, `FinancialKpiGuardTests` 2. Counts moved with the catalogue: `SeedDataTests` 122 → 130 labels, `AdministrationContractTests` 38 → 46 permissions. `MigrationRollbackTests` runs the three new Downs |
| 4 | The CI step's filter, `--filter FullyQualifiedName~PMPlatform.Tests.Integration.FinancialKpi` | 15 passed |
| 5 | `.github/scripts/migration-dry-run.sh`, then `seed-dry-run.sh`, on one scratch database in `AHDA-postgres` (dropped after) | "1925 statement(s), applied twice, no destructive statement"; "12 table(s) seeded, unchanged by a second run, no integrity violation" |
| 6 | `UPDATE_OPENAPI_SNAPSHOT=1 … --filter ProjectContractTests`, then a JSON diff of `docs/api/openapi.v1.json` against `dev` | 4 passed. 29 paths (46 operations), 48 schemas added; no existing path or schema changed |
| 7 | `python3 docs/architecture/contract-check.py docs/api/openapi.v1.json` | 227 operations, 991 findings (795 before). The 196 new ones are all on the FinancialKpi surface and in classes every module has: C-4 46, C-5 46, C-7 37, C-12 53 (the R-52 extensions no module emits), C-8 6 (every `PUT`); plus C-2 2 on `…/documents`, as Milestone's `…/evidence`, and C-9 6 on `…/documents` and the two aggregates, which are single resources, not collections (F-18) |
| 8 | `contract-check.py --self-test`, and the eight other `docs/architecture/*-check.py` gates (`cicd-pipeline-check.py` with the new CI step) | 29 mutations, 0 missed; all OK |

The tests, by criterion:

| Criterion | Tests |
| --- | --- |
| 1. A target-version change never rewrites an earlier measurement's recorded target reference | `ATargetVersionChangeNeverRewritesAnEarlierMeasurement`: a published and a DRAFT measurement under version 1 (92 → AMBER); version 2 approved (92 would be GREEN): the published row is byte-identical (`to_jsonb`), still pinned to version 1 and AMBER; the DRAFT, edited, is re-rated against version 1 and stays AMBER; version 1 is SUPERSEDED with every other column as approved; a later measurement pins version 2. The database refuses re-pinning either and editing either target. `AMeasurementIsBornPinnedToItsAssignmentsActiveTarget` (another assignment's target, a DRAFT target, a REJECTED one: refused). `AMeasurementIsPinnedOnlyToAnApprovedTarget` |
| 2. A missing financial or KPI value renders/serializes as explicit Unknown/N/A, never 0 | `AMissingFinancialValueIsUnknownNeverZero`: a new update is MISSING with null figures; `"0.00"` with MISSING and MEASURED without a figure are 422; STALE serializes `null` in the update, the live position and the snapshot, all UNKNOWN, the column NULL; the CHECKs refuse a 0 or a colour with the guards off. `AMissingKpiValueIsUnknownAndANotApplicableOneIsNotApplicable`. `FinancialStatusRuleTests`, `KpiRagRuleTests` |
| 3. Portfolio aggregation only when currency/unit compatibility is verified, else explicitly partial | `FinancialTotalsAddOnlyVerifiedMeasuredSarFiguresAndSayWhenPartial` (a MISSING project left out and listed, totals from the counted one, another entity's project NOT_AVAILABLE, nothing counted → null totals); `KpiValuesAreCombinedOnlyWithinOneUnit` (two percent KPIs combined; percent with days: `isUnitCompatible` false, `meanValue` null, partial, RAG still counted); `PortfolioAggregationTests` (an unverified currency left out, CURRENCY_UNVERIFIED) |
| Two subdomains, current/live and published distinct | `PublishingAPeriodNeverAltersAnEarlierSnapshotAndTheLiveViewStaysApart` (a later budget and period leave the first snapshot byte-identical; the live position shows the newest figures; the database refuses to update, delete or truncate a snapshot); `AReturnedUpdateContinuesAsTheNextRevision` |
| ADR-008 gate | `AnApprovedBudgetChangeNeedsAReferencedDocumentAndSupersedesThePreviousVersion` (no document 422; one open version; returned and resubmitted as revision 2; approval supersedes, the old row unchanged; provenance recorded); `AnIntegratedFieldTakesNoManualFigureAndIsNeverSwitchedBack` |
| ADR-013 | `AnEntitySeesItsOwnProjectsFinancialsMaskedByAudience` (own project visible, another entity's not; a classified field omitted and named in `maskedFields`; the aggregate leaves it out); `AMeasurementIsPublishedByAhdaNeverByWhoRecordedIt`; `NoOneReviewsFiguresTheySubmitted`; `FinancialsAndKpisAreSeenByTheEntityOnly`, `AnEntitySeesTheFinancialsOfItsOwnProjectsOnly` (unit) |
| Guards | `TheDatabaseHoldsTheVersionsUpdatesAndMeasurements` (15 refusals, nothing changed) |

### 7.1 Mutation tests

Each mutation was applied, the affected project rebuilt, the named tests run, and the source restored (script and output under `artifacts/task-052/mutations`, outside the repository's tracked files).

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | A DRAFT measurement is re-rated against the ACTIVE target, not its pinned one | `ATargetVersionChangeNeverRewritesAnEarlierMeasurement` |
| M-2 | The guard lets a measurement be re-pinned | `ATargetVersionChangeNeverRewritesAnEarlierMeasurement` |
| M-3 | A position that is not MEASURED is rated | `AFigureThatIsNotMeasuredRatesUnknown` (3 cases) |
| M-4 | A MISSING KPI value is rated GREEN | `AMissingKpiValueIsUnknownAndANotApplicableOneIsNotApplicable` |
| M-5 | Financial aggregation skips the currency check | `AFigureWhoseCurrencyIsNotVerifiedIsLeftOutAndTheAggregateIsPartial` |
| M-6 | KPI aggregation combines values across units | `KpiValuesAreCombinedOnlyWithinOneUnit` |
| M-7 | The snapshot guard allows an UPDATE | `PublishingAPeriodNeverAltersAnEarlierSnapshotAndTheLiveViewStaysApart` |
| M-8 | An Approved Budget version is submitted without a document | `AnApprovedBudgetChangeNeedsAReferencedDocumentAndSupersedesThePreviousVersion` |
| M-9 | The review gate admits the submitter | `NoOneReviewsFiguresTheySubmitted` (it survived the first run, whose only submitter was external; the test was added) |
| M-10 | An INTEGRATED field may return to MANUAL | `AnIntegratedFieldTakesNoManualFigureAndIsNeverSwitchedBack` |
| M-11 | A masked field is sent as null instead of omitted | `AnEntitySeesItsOwnProjectsFinancialsMaskedByAudience` |
| M-12 | A new update presumes a zero actual | `AMissingFinancialValueIsUnknownNeverZero` |

### 7.2 Live validation check

Run 2026-10-04 against the API image built from this branch (`docker compose … build api migrate`), as a second container on a
scratch database `task052_live` in `AHDA-postgres`, migrated by the image's `migrate` command to `TASK-052_GuardFinancialKpiHistory`
and seeded with the compose seed (the data-integrity check: no violation). A fixture added what no environment has yet: three
ACTIVE projects managed by local.r04 — A delivered by ENT-LOCAL, B and C AHDA's — WF-02 periods for A and B, three PUBLISHED KPI
definitions in two units, the financial-status thresholds, COMMITMENT and KPI_TARGET routes decided by R02, and the test grants
of F-2. Everything else went through HTTP as local.r04 (enters), local.r02 (reviews, decides in `/approval-tasks`) and local.r08
(R08 on ENT-LOCAL); WF-11's outcomes reached WF-14 through the API's own outbox worker. Two SQL steps stand in for what the stack
lacks: the malware scanner's CLEAN verdict (no provider is configured locally, so an upload is never scanned), and read-only
row probes. The database refusals were attempted as raw `UPDATE`s. **19 of 19 checks passed**:

| Check | Observed |
| --- | --- |
| Criterion 1: an earlier measurement keeps its target version and rating after a new target is approved | measurement under v1 (target 95): v1, AMBER, after v2 (target 80) was approved; v1 SUPERSEDED by v2; the row's `to_jsonb` identical before and after |
| Criterion 1: a measurement recorded now pins the new version | v2, GREEN for the same 92 |
| Criterion 1: the database refuses to re-pin | `its assignment, period and pinned target version never change` |
| Criterion 2: a new update is MISSING with null figures; `"0.00"` for Unknown is refused | `"actualExpenditureToDateSar":null` in the 201 body; 422 `FINANCIAL_KPI_VALUE_STATUS_INVALID` |
| Criterion 2: the published snapshot and the live position carry null and UNKNOWN | snapshot STALE, `null`, UNKNOWN, `PUBLISHED_OFFICIAL`; position `null`, UNKNOWN, `CURRENT_LIVE`; the column is NULL |
| Criterion 2: KPI | N/A → NOT_APPLICABLE, MISSING → UNKNOWN, value `null` |
| Criterion 3: financial | A (Unknown) and B (measured): PARTIAL, 1 of 2 counted, totals B's only, A listed VALUE_NOT_MEASURED; B alone: COMPLETE, SAR |
| Criterion 3: KPI | percent with days: `isUnitCompatible` false, `meanValue` null, partial, RAG counted; one unit: mean 4.0, COMPLETE |
| ADR-008 gate: referenced document | no document → 422; a scan-pending one → 409 `DOCUMENT_NOT_AVAILABLE`, nothing linked; with a CLEAN one approved → ACTIVE, MANUAL provenance, entered by local.r04 |
| ADR-008 gate: INTEGRATED | manual budget → 422 `FINANCIAL_FIELD_INTEGRATED`; INTEGRATED → MANUAL → 409 `FINANCIAL_SOURCE_MODE_LOCKED` |
| Published snapshot immutable, live view apart | a later budget version (B: 1,000,000 → 1,200,000): snapshot unchanged, live position 1,200,000, the first version SUPERSEDED; raw `UPDATE` refused (APPEND_ONLY) |
| ADR-013 | local.r08 sees A's position and KPIs, nothing of B, and is refused an entry (403) |

The fixture granted local.r04 its reads through a second assignment (R03 at DEPT): R04's shipped views are ENTITY, which reach
nothing for an internal Project Manager, and a profile holds one grant per permission — the shape F-2 already leaves to Appendix A.

## 8. Acceptance criteria, deliverables and amendments

| Item | Result |
| --- | --- |
| A target-version change never rewrites an earlier measurement's recorded target reference | **MET.** D-13; §7 criterion 1; M-1, M-2 |
| A missing financial or KPI value renders/serializes as an explicit Unknown/N/A, never 0 | **MET** for the API and the database. D-2; §7 criterion 2; M-3, M-4, M-12. Rendering is TASK-053's |
| Portfolio aggregation only when currency/unit compatibility is verified, else explicitly partial | **MET.** D-11; §7 criterion 3; M-5, M-6 |
| Description: two subdomains, current/live and Published views distinct, nothing coerced to 0 or Green | **MET.** D-1, D-2, D-9 |
| Deliverables: WF-14 services, snapshot publication, aggregation compatibility checks | **MET** (header row "Deliverables") |
| Gate decision ADR-008 | **MET** for manual mode, SAR only, project total, the referenced document and the INTEGRATED rule; integrated and hybrid are built and unconnected (F-7). Open commitments refused. D-3, D-6, D-8 |
| ADR-013 | **MET** for visibility (D-10) and the masking mechanism (D-12); nothing is classified yet (F-6) |
| ADR-008 extended | **MET.** Provenance on every financial record (D-4); Etimad line tables exist, empty at launch (D-3, F-8) |

## 9. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **§8.2 revised by this task**: edge 38 (FinancialKpi → DocumentManagement) added for the referenced document; edges 5, 13 and 26 implemented. S-4 decided for WF-14 by the delivery team: versions route through WF-11 | Engineering Architect | If WF-14 must not use WF-11, the version states shrink and the handler goes |
| F-2 | **Only the views ship** (D-10). No role enters, reviews or configures until Appendix A; the tests grant them | PMO (Appendix A); TASK-110 | Nothing can be entered in a real environment |
| F-3 | **No WF-08 authorisation of a budget change.** ChangeRequest is not built (TASK-060): `change_authorization_id` is never set and has no foreign key; edge 12 is unused. The gate asks only for a referenced document, which is enforced | TASK-060 | A budget change is approved without a linked change request |
| F-4 | **No values.** The financial-status formula (forecast overrun in percent of the Approved Budget) is this task's; its thresholds (PTBC-025) and the KPI targets (PTBC-024) are AHDA's. Publication answers 422 `CONFIGURATION_MISSING` until the thresholds are published; approval needs COMMITMENT and KPI_TARGET routes | AHDA Finance / PMO | Nothing is published or approved |
| F-5 | **KPI_POLICY is not used** (OQ-006): ratings use each target version's thresholds; values are entered, not calculated; `calculationExpression` is not evaluated. No WF-14 specification was available | AHDA PMO; Engineering Architect | A KPI's rule cannot change without a new target version |
| F-6 | **Masking audience is the read permission's clearance** (TASK-030 D-8): every holder of `FINANCIAL_VIEW` sees the same fields, so AHDA and an entity cannot see different fields under the same permission. FIELD_CLASSIFICATION classifies nothing yet | AHDA Cybersecurity (UGV-01); Engineering Architect | No field is masked; if entities must see less than AHDA, the engine needs a per-audience clearance or a separate read permission |
| F-7 | **The integrated source is not connected.** No adapter exists; an INTEGRATED field stays Unknown. Leaving INTEGRATED is refused; how a source is decommissioned is AHDA's decision | AHDA Finance; IntegrationMonitoring owner | An INTEGRATED field never shows a figure |
| F-8 | **Etimad category lines are not written** (launch: project total only); the tables exist | AHDA Finance | No category breakdown until a later task builds it |
| F-9 | **UNDER_REVIEW is unused for versions**: WF-11 gives no signal between start and outcome (as `schedule-baseline.md` F-8) | Engineering Architect | — |
| F-10 | **Beyond the ERD**: submitter, reviewer and return reason on `financial_progress_update`; `submitted_at`, `published_*` on `kpi_measurement`; partial unique indexes (one ACTIVE and one open version, one open revision per period); one snapshot per period; CHECKs on value status, ratings and provenance. `erd.dbml` is not changed | Engineering Architect (TASK-008) | The ERD understates the constraints |
| F-11 | **Periods come from WF-02.** A period exists only once WF-02 has generated it, when progress is first reported for it; until then a financial update is 409 `FINANCIAL_NOTHING_TO_REPORT` | Engineering Architect | Financial figures wait for progress reporting |
| F-12 | **A published measurement cannot be corrected**: the ERD's states have no return, and a period has one measurement | PMO | A wrong published value stands |
| F-13 | **ADR-014's declared budget and opening spend-to-date are not written** (TASK-104's consumer); the guard admits a DECLARED_BUDGET born ACTIVE | TASK-104 | Intake projects have no budget of record until one is approved |
| F-14 | **WF-02 does not read the financial status** (`progress-update.md` F-1) | Engineering Architect | Overall Health's financial dimension stays UNKNOWN |
| F-15 | **No notifications** of submission, approval or publication: NOTIFICATION_ROUTING has no WF-14 family | PMO; TASK-039 configuration | Reviewers learn of work from their screens |
| F-16 | **Security Lead review (CTL-43) cannot be requested**: the CODEOWNERS teams do not exist. This change touches RBAC | Maintainer | The PR's CTL-43 box stays unticked |
| F-17 | **Idempotency keys are required, not replayed** (TASK-031 F-2): a retried create answers 409 | Engineering Architect | The SPA treats that 409 after a retry as success |
| F-18 | **Contract-check C-9 on the aggregates**: the checker reads a `GET` on a plural path as a collection; an aggregate is one resource | Engineering Architect | Recorded, as Milestone's C-2 |
| F-19 | **The KPI aggregate is an arithmetic mean** of the latest published values; OQ-006 may weight it | AHDA PMO | — |

## 10. Change log

| Date | Change |
| --- | --- |
| 2026-10-04 | Created (TASK-052) |
| 2026-10-04 | §7.2: the live validation check against the API image built from the branch, 19 of 19 checks passed |

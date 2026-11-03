# RPMO review, round 3 (2 Nov 2026)

What the platform does about the client's four requests in *RPMO Platform — Review Round 3*, and where it departs from that document. The
migrations are named `TASK-111`, a placeholder until the workbook gives this round a task ID.

## 1. One filter bar (request 1)

`shared/ui/FilterBar.tsx` is the one filter bar of the Portfolio and Governance dashboards and the project register. Sector, entity,
department, region and city are in its first row; status, priority, strategic portfolio and initiative are under **More filters**, which
counts the ones set. The label sits inside each control ("Sector: All"), the active filters show as chips with ×, and **Clear all** removes
them. A filter applies as it is chosen. The filters are kept in the URL, and on a dashboard they are also remembered per person, per
dashboard, in the browser (`pmplatform.dashboardFilters.<user>.<dashboard>`). The bar stays in view as the page scrolls; on a phone it
folds into a **Filters (n)** button.

`GET /dashboards/{code}` takes `classificationItemId` (the sector), `status`, `priorityItemId`, `strategicPortfolioItemId` and
`strategicInitiativeItemId` beside the four filters it had. `filters` lists each filter's values with the count of projects in the caller's
scope (DSH-CC-17: a filter narrows the population and can never widen it). A region narrows the cities and a portfolio the initiatives.

The **Period** filter (`period`, a year) finds the projects whose planned period touches that year: a project planned from 2025 to 2027 is in
each of the three; one with a single planned date is in that date's year. The dashboard map from round 2 is unchanged.

Department and sector are told apart everywhere: the department (الإدارة) sets R03's scope, and the sector (القطاع) is the project
classification, aligned to RPMO's 15 sectors by loading them as PROJECT_CLASSIFICATION items. The administration screens called
departments "sectors" since the 24 Oct 2026 review; they now say "departments". Seeded report column labels still say "Sector" for the
department (`db/seed`, report definitions) and are left for a governed report change.

## 2. Strategy classification and RPMO's fields (requests 2 and 3)

| RPMO column | Platform |
| --- | --- |
| المحفظة / المبادرة | `strategic_portfolio_item_id`, `strategic_initiative_item_id`: catalogues STRATEGIC_PORTFOLIO and STRATEGIC_INITIATIVE. An initiative's parent is a portfolio (`master_data_catalogue.parent_catalogue_id`, which also links CITY to REGION). A view, never a scope |
| رقم المشروع | `legacy_reference`, unique where set, searchable; a re-import matches on it |
| مسؤول المحفظة | `follow_up_owner_user_id`, an active internal user |
| هل التمويل من ميزانية الاستراتيجية | `funding_source`: STRATEGY_BUDGET_FULL, STRATEGY_BUDGET_PARTIAL, NOT_STRATEGY_BUDGET |
| الأولوية | `priority_item_id` (PRIORITY) |
| نسبة الإنجاز / الصرف المخططة والفعلية | `declared_*_percent` with `declared_as_of` and `declared_source` (IMPORT, MANUAL). A blank is "Not available", never 0 |
| المعوق الحالي | `current_obstacle` |
| الإجراء التالي / مسؤول الإجراء / الموعد المستهدف | `next_action`, `next_action_owner`, `next_action_due_date` |
| هل الجهة ملتزمة بالتحديث الدوري | `entity_update_compliant`, as RPMO records it |
| ملاحظات | `project.project_follow_up_note`, dated and attributed |

The registration form asks for the portfolio, then offers only its initiatives. Portfolio and initiative are optional in the API; making
the portfolio required at registration waits on AHDA's decision 2.

The follow-up fields are changed by `POST /projects/{id}/update-follow-up` (PROJECT_FOLLOW_UP: R02 ALL, R03 DEPT; internal users only) from
APPROVED_PLANNED on, never by registration. Every project read carries RPMO's calculated columns (`indicators`): the gap (actual − planned,
in points), the on-plan indicator (on track above −5, late to just above −10, stalled at −10 or worse; WORKFLOW_POLICY
`ON_PLAN_LATE_GAP_POINTS` and `ON_PLAN_STALLED_GAP_POINTS` override RPMO's defaults), days remaining, the next action's due status and the
follow-up reasons. The gap is not calculated for a project not started or closed, or when either percentage is missing.

Entity update compliance is calculated (`indicators.entityUpdates`) from the update requests WF-13 issued to the entity: a request counts
once its due date has come or it is answered, and is on time when its first contribution was submitted by its due date. The entity is
compliant from 80% on time (WORKFLOW_POLICY `ENTITY_UPDATE_COMPLIANCE_PERCENT`). The value RPMO records overrides the calculation. The
Project module declares `IProjectUpdateComplianceSource` and ExternalParticipation implements it, so Project depends on no module that
depends on it.

The indicator reads declared progress; a project with a WF-02 schedule still shows its declared values until they are cleared.

## 3. Follow-up list (build item 5)

`GET /project-follow-ups` and **Projects → Follow-up list** (قائمة المتابعة): every project the caller may see with a follow-up reason,
ordered stalled, suspended, late, overdue action, open obstacle, then by the earliest action due.

## 4. Project import (request 4)

| Permission | Shipped to | Does |
| --- | --- | --- |
| `PROJECT_IMPORT` | R02 ALL, R03 DEPT | Template, validation, submission. A row outside the importer's scope is an error |
| `PROJECT_IMPORT_APPROVE` | R02 ALL | Approve (applies the batch) or return with a reason; roll back. Never one's own batch |

An administrator grants either to another profile in the profile editor, through the usual prepare, verify and publish route.

The flow: **Projects → Import**. The template (`GET /project-import-batches/import-template/content`) has RPMO's sheet, Arabic headers with
`*` on the required ones, the row of field keys, drop-downs over a القوائم sheet drawn from live master data, and `template_version=1` in a
hidden column. `POST /project-import-batches/validations` reads the workbook by its field keys. Each row gets errors, warnings and info, and
the action it would take: create, update (matched by legacy reference, then Formal Project ID), or skip. An update with no change is a skip,
so the same file uploaded again creates nothing. Names the platform does not know are listed once, with the values they may be mapped to;
mappings are remembered (`project.project_import_name_mapping`). `POST /project-import-batches` submits the file, the mappings, the skipped
rows and a note. The approver sees `GET /project-import-batch-previews/{id}`, the plan validated again now, and approves or returns it. An
approval validates once more and applies the batch in one transaction.

Imported projects enter in their mapped state as in-flight intake (ADR-014), with a Formal Project ID and an activation stamp (none for
APPROVED_PLANNED). No registration approval runs again: the batch's approval covers them. They carry `project_import_batch_id`, the
Imported label. Status mapping: قيد التنفيذ and نشط → ACTIVE; مستقبلي → APPROVED_PLANNED; متوقف → SUSPENDED; منجز → COMPLETED; ملغي → **CLOSED**
(the platform has no Cancelled state); أخرى or blank must be mapped.

The import's events are recorded per batch, with no project scope, so a batch can be rolled back. Rollback is allowed while the batch
updated no existing project and none of its projects has changed since it was applied. The project guard deletes a non-DRAFT project only
when its batch is ROLLED_BACK, and any record another module holds on a project refuses the rollback.

**Deviations from the review.** The project manager must hold R04 over the row's department, as the review's rule says: the import does not
grant R04. An unmatched **follow-up owner** is a warning that leaves the field empty rather than an error, because the column is optional
and RPMO's owners may have no account. Governed fields of an existing project — name, department, entity, sector, value, dates, place,
status, governance level — are not updated by an import: a difference is a warning, and the change goes through a change request.

**Background validation.** A workbook of more than 200 rows answers 202 with a queued `project.project_import_validation`; the
`ProjectImport:Validation` worker validates it as its importer may import, keeps the preview (`GET /project-import-validations/{id}`, to
the importer only) and clears the file. The importer is notified (`Project.ImportValidated`, family PROJECT_IMPORT_VALIDATED); the import
page polls meanwhile, and the notification's link reopens the result. 1,500 rows is still the limit.

**Asking for a missing value.** For each unknown name the import page offers "Ask an administrator to add it":
`POST /project-import-value-requests` records the request (one OPEN request per name) and notifies the administrators
(`Project.ImportValueRequested`, family PROJECT_IMPORT_VALUE_REQUESTED). A holder of MASTER_DATA_MANAGE or ORGANIZATION_MANAGE adds the value
where it belongs and marks the request added or declined on the Master data screen; the importer is notified
(`Project.ImportValueResolved`, family PROJECT_IMPORT_VALUE_RESOLVED).

**Notifications addressed to one person.** WF-15 routes by role (ADR-004). An intent may name a `recipientUserId`: of the people the
family's role matrix names over the intent's scope, only that user receives it. The three families need NOTIFICATION_ROUTING entries and
templates, which AHDA publishes.

## 5. RPMO's draft file

`RPMO_Projects_Import_v1.xlsx`, run through the import on the test stack on 3 Nov 2026 with its القوائم lists loaded as master data, its four
departments and 41 entities, and an internal R04 holder standing in for `local.r04`:

| | |
| --- | --- |
| Rows | 167, none with an error; submittable |
| Warnings | Every row has one: the four follow-up owners have no account (167). Also active with a past end date (36), completed below 100% (7), completed with a future end date (5), a value of SAR 10 billion or more (1), not started with progress (1) |
| Kept blank | Spend (167 rows), planned progress (69) |
| Approved | 167 projects: 109 active, 20 completed, 19 approved – planning, 18 suspended, 1 closed; declared actual progress on 137 |
| Rolled back | All 167 removed |

The review counted 40 rows with warnings in the file's own column; the platform's rules count more because they read the dates the draft
filled in, against 3 Nov 2026.

## 6. Decisions still AHDA's

The eight in the review's §5 stand. Until they are made, the draft file's assumed departments, governance levels, places, dates and project
manager are what the import loads.

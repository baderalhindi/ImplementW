# Shipped Permission Matrix

| Field | Value |
| --- | --- |
| Source of the change | The prototype review of 24 Oct 2026 ("RPMO Platform — Prototype Review", @Mo): findings 1a, 1b, 3; critical gaps C1, C2 and C4's safety rules; high-priority H3 and H4; medium M-6. No workbook row names it: the work is on branch `fix/review-shipped-permission-matrix` |
| Record date | 2026-10-25 |
| Status | **BUILT AND VERIFIED LOCALLY.** The eight shipped profiles hold Blueprint v2.0 Appendix A as the review applies it (§3). Approval, review and activation authority ship for the first time (C1). The safety rules hold in code (D-2) |
| Controlled sources | Blueprint v2.0 (QA CLOSED) Appendix A "Detailed Permission / Authorization Model" and A.1 "Permission implementation rules", read from `AHDA/Project Files.zip` on 2026-10-25. The review cites "Build Pack 03 §4" and "Build Pack 06 §A", which are not in the repository, the zip or Drive; where the review states a cell the build pack fixes, that cell is taken from the review (§4) |
| Supersedes | The "waits for Appendix A" rows of `authorization-engine.md` F-1, `seed-data-and-integrity.md` F-1 and the module records' F-1/F-2 rows listed in §7; ADR-013's entity report set (`reports.md` D-6) by ADR-019 (D-7) |

## 1. What the review found, and what changed

| Review item | Cause in the build | Change |
| --- | --- | --- |
| C1 — no role can approve | `APPROVAL_VIEW`/`APPROVAL_DECIDE` and every review permission shipped to no role | R02 (ALL), R03 (DEPT) and R07 (ALL) decide; R04 sees its own submissions (D-3). Review, activation and publication go to R02 and R03 |
| C2 — scope not enforced | The reviewed site ran on the showcase database, whose fixture granted the view permissions at ALL to R01, R02, R03, R05, R06 and R07; the shipped profiles held almost nothing | Each role's grants at its agreed scope (§3); R05 at ASSIGNED (D-4 of `authorization-engine.md`, as amended); the amounts behind `FINANCIAL_VIEW_SENSITIVE` (D-4) |
| 1a — the administrator sees business data | The showcase fixture's grants | R01 ships no business permission, and `GrantPolicy` drops any a profile holds (D-2) |
| 1b — the PM sees the whole portfolio | Test data: one PM named on all 13 internal projects | R04 at OWN; the demo data is the next phase's (review M-7) |
| 3, H3 — export to the wrong roles | `REPORT_EXPORT` shipped to R04 and R08 only | R02, R03, R04, R05 and R07 export at their scope; R06 waits for AHDA (F-3); R08 runs no report at all (D-7) |
| H4 — core abilities missing | Appendix A's cells never shipped | R02/R03 register, view documents, issues and governance requests; R05 issues and monitors external update requests; R07 sees issues; `AUDIT_BUSINESS_VIEW` ships to R02–R07 |
| C4 — safety rules | None in code | `GrantPolicy` (D-2) |
| M-6 — contradictory assignments | No anchor rule for internal roles | `IDENTITY_ACCESS_SCOPE_ANCHOR_INVALID` (D-6) |

## 2. Decisions

| # | Decision | Basis |
| --- | --- | --- |
| D-1 | **One scope per role.** R02 and R07 ALL, R03 and R06 DEPT, R04 OWN, R05 ASSIGNED, R08 ENTITY (R08's task and answering grants ASSIGNED, as before). Master data and configuration views are ALL (platform-wide, no record to scope); `LAYOUT_PERSONALIZE` and `REPORT_COMPOSE` stay OWN (ADR-019) | The review's "Agreed" column; Appendix A's per-role qualifiers ("portfolio", "dept", "own", "assigned", "entity") |
| D-2 | **Safety rules in code, not in a profile** (`GrantPolicy`). The engine drops a grant they forbid as if the profile did not hold it: R01 holds nothing of the business groups (approval, project, progress, schedule, tasks, milestones, risks, issues, change, suspension, closeout, external participation, financial, KPI, documents, reports, Activity, business/financial/external audit); an external user, and any holder of R08, holds no approval, report or integration-monitoring permission and no `FINANCIAL_VIEW_SENSITIVE`. Nobody deciding their own submission stays WF-11's person check (`ApprovalAuthority`, refusal `REQUESTER`) | Review C4; Appendix A.1 ("System Administrator business-record access … does not create business authority"); ADR-013; ADR-019; BR-INT-022 |
| D-3 | **R04 views approvals at OWN and decides none.** WF-11 decides on the run's project and department anchors only, so an OWN grant of `APPROVAL_DECIDE` would reach no run; OWN on `APPROVAL_VIEW` is the runs the holder requested — the Reviews tab of the PM's project | Appendix A "Approval inbox — Project Mgr: V own submissions"; the review's "via task" is the WF-11 routing, which names roles, not owners |
| D-4 | **`FINANCIAL_VIEW_SENSITIVE`, a separate read permission for the SAR amounts.** A holder of `FINANCIAL_VIEW` without it sees the financial condition, dates and value status, and every amount is withheld and named in `maskedFields` (`FinancialKpiAccess.MaskAsync` adds to the FIELD_CLASSIFICATION mask, never relaxes it). Dashboards, reports and exports read figures through the same service, so they withhold them the same way (CTL-19). The sensitive grant goes with the view grant at the same scope, so it is decided once per audience; a unit test holds the shipped profiles to that | Review C2 (R06 "masked amounts", R08 "amounts absent", Build Pack 06 item 6) and C4 ("FIN_VIEW_SENSITIVE"); `financial-kpi.md` F-6 named exactly this gap ("a per-audience clearance or a separate read permission") |
| D-5 | **A project with no Project Manager is owned by its registrant.** A DRAFT has no PM (ERD), so OWN registration needs an owner: `ProjectAccess.OwnerOf` is the PM, else `created_by`, in the single-record check and in the register's SQL alike; creation is checked with the caller as owner | Review: `PROJECT_REGISTER` R04 OWN |
| D-6 | **An internal assignment carries its role's anchor:** R03 a department, R05 a project, R06 a department or a project, and no internal assignment an external entity. 422 `IDENTITY_ACCESS_SCOPE_ANCHOR_INVALID`, every rule broken listed | Review M-6 |
| D-7 | **No reports for external users (ADR-019, OD-S13).** `ReportCatalogue` offers R01 and R08 no report and an external caller runs none; `[ExternalUsersNotFound]` on the report, report-job, definition, explorer and allowlist controllers answers an external caller 404 on every action, deciding on the engine's user type: as an action filter where the caller passes the gate, and as the gate's own refusal (`BearerChallenge.OnForbidden`) where it does not. The seeded audiences: Portfolio Summary to R02, R03, R06, R07 (the portfolio audience, review H2); Governance and Change (EXECUTIVE) to R02, R03, R07; the other eight to R02–R07 | Review H3; Build Pack 06 item 6 ("`/api/v1/reports/*` return 404 for external users"), superseding ADR-013's entity report set |
| D-8 | **The seed owns the shipped versions' grants.** No shared environment has run the seed (seed record F-12), so the matrix goes into version 1; a re-run upserts each row's scope and deletes a version-1 grant outside the matrix, so a local database re-seeded after a matrix change holds the matrix. Integration test hosts add their grants with `ON CONFLICT … DO UPDATE`, keeping the scope each was written for | `seed-data-and-integrity.md` F-12 ("whoever adds Appendix A") |

## 3. The matrix

"—" is not granted. 91 permissions; 234 grants. Eight permissions ship to no role: `SUSPENSION_ACTIVATE` and `CLOSEOUT_ACTIVATE` (the workflows' own services), and six waiting on AHDA (F-1, F-2). `ShippedGrantTests.TheShippedGrantsAreTheAgreedMatrix` states this table a second time, independently of the catalogue.

| Permission | Group | Mode | R01 | R02 | R03 | R04 | R05 | R06 | R07 | R08 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `IDENTITY_INTEGRATION_MANAGE` | IDENTITY_ACCESS | Write | ALL | — | — | — | — | — | — | — |
| `USER_VIEW` | IDENTITY_ACCESS | Read | ALL | — | — | — | — | — | — | — |
| `USER_MANAGE` | IDENTITY_ACCESS | Write | ALL | — | — | — | — | — | — | — |
| `ROLE_VIEW` | IDENTITY_ACCESS | Read | ALL | — | — | — | — | — | — | — |
| `ROLE_MANAGE` | IDENTITY_ACCESS | Write | ALL | — | — | — | — | — | — | — |
| `ROLE_ASSIGN` | IDENTITY_ACCESS | Write | ALL | — | — | — | — | — | — | — |
| `ORGANIZATION_VIEW` | IDENTITY_ACCESS | Read | ALL | — | — | — | — | — | — | — |
| `ORGANIZATION_MANAGE` | IDENTITY_ACCESS | Write | ALL | — | — | — | — | — | — | — |
| `MASTER_DATA_VIEW` | MASTER_DATA_CONFIG | Read | ALL | ALL | ALL | ALL | ALL | ALL | ALL | ALL |
| `MASTER_DATA_MANAGE` | MASTER_DATA_CONFIG | Write | ALL | — | — | — | — | — | — | — |
| `CONFIGURATION_VIEW` | MASTER_DATA_CONFIG | Read | ALL | ALL | ALL | — | — | — | ALL | — |
| `CONFIGURATION_MANAGE` | MASTER_DATA_CONFIG | Write | ALL | — | — | — | — | — | — | — |
| `APPROVAL_VIEW` | APPROVAL | Read | — | ALL | DEPT | OWN | — | — | ALL | — |
| `APPROVAL_DECIDE` | APPROVAL | Write | — | ALL | DEPT | — | — | — | ALL | — |
| `DOCUMENT_VIEW` | DOCUMENT_MANAGEMENT | Read | — | ALL | DEPT | OWN | ASSIGNED | DEPT | ALL | — |
| `DOCUMENT_UPLOAD` | DOCUMENT_MANAGEMENT | Write | — | ALL | DEPT | OWN | ASSIGNED | — | — | — |
| `DOCUMENT_MANAGE` | DOCUMENT_MANAGEMENT | Write | — | ALL | DEPT | — | — | — | — | — |
| `PROJECT_VIEW` | PROJECT | Read | — | ALL | DEPT | OWN | ASSIGNED | DEPT | ALL | ENTITY |
| `PROJECT_REGISTER` | PROJECT | Write | — | ALL | DEPT | OWN | — | — | — | ENTITY |
| `PROJECT_REVIEW` | PROJECT | Write | — | ALL | DEPT | — | — | — | — | — |
| `PROJECT_ACTIVATE` | PROJECT | Write | — | ALL | DEPT | — | — | — | — | — |
| `PROGRESS_VIEW` | PROGRESS | Read | — | ALL | DEPT | OWN | ASSIGNED | DEPT | ALL | ENTITY |
| `PROGRESS_SUBMIT` | PROGRESS | Write | — | — | — | OWN | — | — | — | — |
| `PROGRESS_REVIEW` | PROGRESS | Write | — | ALL | DEPT | — | — | — | — | — |
| `SCHEDULE_VIEW` | SCHEDULE | Read | — | ALL | DEPT | OWN | ASSIGNED | DEPT | ALL | — |
| `SCHEDULE_EDIT` | SCHEDULE | Write | — | — | — | OWN | — | — | — | — |
| `TASK_VIEW` | PROJECT_TASK | Read | — | ALL | DEPT | OWN | ASSIGNED | DEPT | ALL | ASSIGNED |
| `TASK_UPDATE` | PROJECT_TASK | Write | — | — | — | OWN | — | — | — | ASSIGNED |
| `TASK_MANAGE` | PROJECT_TASK | Write | — | — | — | OWN | — | — | — | — |
| `TASK_REOPEN` | PROJECT_TASK | Write | — | — | — | OWN | — | — | — | — |
| `MILESTONE_VIEW` | MILESTONE | Read | — | ALL | DEPT | OWN | ASSIGNED | DEPT | ALL | — |
| `MILESTONE_SUBMIT` | MILESTONE | Write | — | — | — | OWN | — | — | — | — |
| `RISK_VIEW` | RISK | Read | — | ALL | DEPT | OWN | ASSIGNED | DEPT | ALL | ENTITY |
| `RISK_MANAGE` | RISK | Write | — | ALL | DEPT | OWN | — | — | — | — |
| `RISK_ASSESS` | RISK | Write | — | ALL | DEPT | — | — | — | — | — |
| `RISK_ACCEPT` | RISK | Write | — | ALL | DEPT | — | — | — | — | — |
| `RISK_REOPEN` | RISK | Write | — | ALL | DEPT | — | — | — | — | — |
| `CONCERN_VIEW` | MANAGEMENT_CONCERN | Read | — | ALL | DEPT | OWN | ASSIGNED | DEPT | ALL | ENTITY |
| `CONCERN_RAISE` | MANAGEMENT_CONCERN | Write | — | ALL | DEPT | OWN | — | — | — | ENTITY |
| `CONCERN_MANAGE` | MANAGEMENT_CONCERN | Write | — | — | — | OWN | — | — | — | — |
| `CONCERN_ESCALATE` | MANAGEMENT_CONCERN | Write | — | — | — | OWN | — | — | — | — |
| `CONCERN_ESCALATION_RESOLVE` | MANAGEMENT_CONCERN | Write | — | ALL | DEPT | — | — | — | ALL | — |
| `CHANGE_REQUEST_VIEW` | CHANGE_REQUEST | Read | — | ALL | DEPT | OWN | — | DEPT | ALL | — |
| `CHANGE_REQUEST_RAISE` | CHANGE_REQUEST | Write | — | ALL | DEPT | OWN | — | — | — | — |
| `CHANGE_REQUEST_REVIEW` | CHANGE_REQUEST | Write | — | ALL | DEPT | — | — | — | — | — |
| `CHANGE_REQUEST_IMPLEMENT` | CHANGE_REQUEST | Write | — | — | — | OWN | — | — | — | — |
| `SUSPENSION_VIEW` | SUSPENSION | Read | — | ALL | DEPT | OWN | ASSIGNED | DEPT | ALL | — |
| `SUSPENSION_RAISE` | SUSPENSION | Write | — | ALL | DEPT | OWN | — | — | — | — |
| `SUSPENSION_REVIEW` | SUSPENSION | Write | — | ALL | DEPT | — | — | — | — | — |
| `SUSPENSION_ACTIVATE` | SUSPENSION | Write | — | — | — | — | — | — | — | — |
| `CLOSEOUT_VIEW` | CLOSEOUT | Read | — | ALL | DEPT | OWN | ASSIGNED | DEPT | ALL | — |
| `CLOSEOUT_RAISE` | CLOSEOUT | Write | — | ALL | DEPT | OWN | — | — | — | — |
| `CLOSEOUT_REVIEW` | CLOSEOUT | Write | — | ALL | DEPT | — | — | — | — | — |
| `CLOSEOUT_WAIVE` | CLOSEOUT | Write | — | ALL | DEPT | — | — | — | — | — |
| `CLOSEOUT_ACTIVATE` | CLOSEOUT | Write | — | — | — | — | — | — | — | — |
| `EXTERNAL_REQUEST_VIEW` | EXTERNAL_PARTICIPATION | Read | — | ALL | DEPT | OWN | ASSIGNED | — | — | ENTITY |
| `EXTERNAL_REQUEST_MANAGE` | EXTERNAL_PARTICIPATION | Write | — | ALL | DEPT | OWN | ASSIGNED | — | — | — |
| `EXTERNAL_CONTRIBUTION_RESPOND` | EXTERNAL_PARTICIPATION | Write | — | — | — | — | — | — | — | ASSIGNED |
| `EXTERNAL_CONTRIBUTION_REVIEW` | EXTERNAL_PARTICIPATION | Write | — | ALL | DEPT | OWN | ASSIGNED | — | — | — |
| `EXTERNAL_CONTRIBUTION_APPLY` | EXTERNAL_PARTICIPATION | Write | — | — | — | OWN | — | — | — | — |
| `FINANCIAL_VIEW` | FINANCIAL | Read | — | ALL | DEPT | OWN | — | DEPT | ALL | ENTITY |
| `FINANCIAL_VIEW_SENSITIVE` | FINANCIAL | Read | — | ALL | DEPT | OWN | — | — | ALL | — |
| `FINANCIAL_SUBMIT` | FINANCIAL | Write | — | — | — | OWN | — | — | — | — |
| `FINANCIAL_REVIEW` | FINANCIAL | Write | — | ALL | DEPT | — | — | — | — | — |
| `FINANCIAL_SOURCE_MANAGE` | FINANCIAL | Write | — | — | — | — | — | — | — | — |
| `KPI_VIEW` | KPI | Read | — | ALL | DEPT | OWN | ASSIGNED | DEPT | ALL | ENTITY |
| `KPI_MANAGE` | KPI | Write | — | ALL | DEPT | OWN | — | — | — | — |
| `KPI_RECORD` | KPI | Write | — | — | — | OWN | — | — | — | — |
| `KPI_REVIEW` | KPI | Write | — | ALL | DEPT | — | — | — | — | — |
| `NOTIFICATION_TEMPLATE_VIEW` | NOTIFICATIONS | Read | ALL | — | — | — | — | — | — | — |
| `NOTIFICATION_TEMPLATE_MANAGE` | NOTIFICATIONS | Write | ALL | — | — | — | — | — | — | — |
| `NOTIFICATION_DELIVERY_VIEW` | NOTIFICATIONS | Read | ALL | — | — | — | — | — | — | — |
| `NOTIFICATION_DELIVERY_MANAGE` | NOTIFICATIONS | Write | ALL | — | — | — | — | — | — | — |
| `LAYOUT_PERSONALIZE` | DASHBOARDS | Write | — | OWN | OWN | — | — | — | OWN | — |
| `REPORT_COMPOSE` | REPORTS | Write | — | OWN | OWN | — | — | — | OWN | — |
| `REPORT_EXPORT` | REPORTS | Read | — | ALL | DEPT | OWN | ASSIGNED | — | ALL | — |
| `ACTIVITY_VIEW` | ACTIVITY | Read | — | ALL | DEPT | OWN | ASSIGNED | — | ALL | ENTITY |
| `AUDIT_SECURITY_VIEW` | AUDIT_SECURITY | Read | — | — | — | — | — | — | — | — |
| `AUDIT_ADMINISTRATION_VIEW` | AUDIT_ADMINISTRATION | Read | ALL | — | — | — | — | — | — | — |
| `AUDIT_INTEGRATION_VIEW` | AUDIT_INTEGRATION | Read | — | — | — | — | — | — | — | — |
| `AUDIT_FINANCIAL_VIEW` | AUDIT_FINANCIAL | Read | — | — | — | — | — | — | — | — |
| `AUDIT_EXTERNAL_VIEW` | AUDIT_EXTERNAL | Read | — | — | — | — | — | — | — | — |
| `AUDIT_BUSINESS_VIEW` | AUDIT_BUSINESS | Read | — | ALL | DEPT | OWN | ASSIGNED | DEPT | ALL | — |
| `AUDIT_CONTEXT_VIEW` | AUDIT_CONTEXT | Read | — | — | — | — | — | — | — | — |
| `AUDIT_OPERATIONS_MANAGE` | AUDIT_OPERATIONS | Write | ALL | — | — | — | — | — | — | — |
| `INTEGRATION_VIEW` | INTEGRATION_MONITORING | Read | ALL | — | — | — | — | — | — | — |
| `INTEGRATION_LOG_VIEW` | INTEGRATION_MONITORING | Read | ALL | — | — | — | — | — | — | — |
| `INTEGRATION_OPERATE` | INTEGRATION_MONITORING | Write | ALL | — | — | — | — | — | — | — |
| `INTEGRATION_ALERT_MANAGE` | INTEGRATION_MONITORING | Write | ALL | — | — | — | — | — | — | — |
| `INTEGRATION_CONFIGURE` | INTEGRATION_MONITORING | Write | ALL | — | — | — | — | — | — | — |
| `DATA_IMPORT` | DATA_IMPORT | Write | ALL | — | — | — | — | — | — | — |

## 4. Where the matrix departs from Appendix A, and why

| Cell | Appendix A | Shipped | Why |
| --- | --- | --- | --- |
| R01 business records | "Support view only" | Nothing | Review 1a and C4: R01 sees no business data; A.1 limits support access to legitimate purposes and an access path for that is not specified |
| R04 documents | "V/C/E/D* own" | View and upload, no `DOCUMENT_MANAGE` | ADR-013's amendment to TASK-037 gives the entity Project Manager upload, view and history only; managing (edit, archive, rescan) stays with R02 and R03 |
| R04 approval inbox | "V own submissions" | `APPROVAL_VIEW` OWN | Same reading; no `APPROVAL_DECIDE` (D-3) |
| R05 approvals | "V own" | Nothing | Review: R05 "must not see Approvals" |
| R06 business audit | "—" | `AUDIT_BUSINESS_VIEW` DEPT | Review H4 lists R06 among the roles that read the business audit trail in scope |
| R06 export | "V scoped/export*" | No `REPORT_EXPORT` | Review: "R06 (if AHDA confirms)" — F-3 |
| R07 risks | "V critical/summary + A*" | `RISK_VIEW` ALL | No criticality filter exists in the engine; Executive dashboards and reports show the critical items |
| R08 documents | "V/C/E/D* entity" | Nothing | `external-participation.md` F-1: `DOCUMENT_VIEW` at ENTITY would let an entity browse every document of its projects, which WF-13 §10.1 forbids |
| R08 reports | "V entity limited" | Nothing | ADR-019 (D-7) |
| R08 amounts | entity financials | Condition only | D-4 |
| Users / roles for R02, R03 | "V limited*" | Nothing | The name lookups of review H1 are the next phase's; a USER_VIEW grant opens the administration register |

## 5. Build Pack 06 §A, automated

| Item | Test |
| --- | --- |
| 1. No admin approvals | `MatrixAcceptanceTests.TheAdministratorHoldsNoApprovalAndNoBusinessDataWhateverAProfileGrants` |
| 2. No self-approval | `ApprovalAuthorityTests.NoOneApprovesTheirOwnRequestNotEvenForADelegator` (WF-11's `REQUESTER` refusal, which no grant overrides) |
| 3. Viewer read-only and department-limited | `MatrixAcceptanceTests.TheViewerIsReadOnlyAndLimitedToItsDepartment` |
| 4. Department isolation | `MatrixAcceptanceTests.ADepartmentManagerReachesItsOwnDepartmentOnly` |
| 5. External isolation | `MatrixAcceptanceTests.AnExternalUserIsIsolatedToItsEntityAndHoldsNoApprovalReportOrAmount`, `TheExternalEntityUserRoleHoldsNoApprovalReportOrAmountEvenForAnInternalHolder` |
| 6. No reports for externals | `ReportAcceptanceTests.AnEntityUserIsGivenNoReport` (catalogue, report, run, export, jobs, explorer, allowlist and definitions all 404) |

Also: `ReportAcceptanceTests.TheViewerSeesTheFinancialConditionOfItsDepartmentWithTheAmountsWithheld` (D-4), `ALiaisonReachesTheProjectsItIsAssignedToAndNoOther`, `ApprovalAuthorityShipsToTheManagersAndTheExecutive`, `TheSensitiveAmountsGoWithTheFinancialViewAtItsScope`, `AccessRelationshipServiceTests.AnInternalAssignmentWithoutItsScopeAnchorIsRefusedWithEveryRuleItBreaks`.

## 6. Validation

| # | Check | Result |
| --- | --- | --- |
| 1 | `dotnet test PMPlatform.Tests.Unit` | 2,170 passed, 0 failed |
| 2 | `dotnet test PMPlatform.Tests.Integration --filter "FullyQualifiedName!~RegisterQueryPlanTests"` against `AHDA-postgres` and `AHDA-ldap` (the query-plan tests time out on this machine whatever the branch, `ahda-dev-environment`; CI runs them) | 922 passed; 3 failed, then each resolved: `ReportDefinitionTests.AnAbandonedDraftIsRetiredAndConfigurationIsForItsManagersOnly` (the test's non-manager held a test grant; corrected, passes), `IntegrationFailureSimulationTests` (a business-state digest moved by two notification deliveries under full-suite load; passes alone and passed in the two earlier full runs), `SharedRateLimitStoreTests` (needs `RATE_LIMIT_TEST_REDIS`) |
| 3 | The EndpointHardening namespace with `RATE_LIMIT_TEST_REDIS=localhost:6379` (`AHDA-redis-078`) | 70 passed |
| 4 | Repository checks: `contract-check.py`, `erd-check.py`, `findings-log-check.py`, `test-strategy-check.py`, `uat-catalogue.py --check`, `database-check.py`, `api-documentation-check.py`, `handover-package-check.py`, `accessibility-audit.py --check`, `load-test-report.py --check`, `penetration-test-check.py` | All exit 0 |
| 5 | Frontend, in the e2e runner: `vitest run src/shared/i18n src/features/identity-access`, `tsc -b --noEmit`, `eslint src/features/identity-access` | 73 passed; typecheck and lint clean |

## 7. Records this changes

`authorization-engine.md` (D-4 ASSIGNED; F-1), `seed-data-and-integrity.md` (F-1), `reports.md` (D-6, F-3), `financial-kpi.md` (F-2, F-6), `project-registration.md` (F-1), `approval-framework.md` (F-1), `external-participation.md` (F-2), `identity-access-administration.md` (the new error code). Their change logs point here.

## 8. Findings

| # | Finding | Owner | Effect |
| --- | --- | --- | --- |
| F-1 | **Five audit categories ship to no role:** security, integration, financial and external audit, and request context. They are Audit and Security Reviewer assignments AHDA has not named (TBC-AUD-05 to -07, PTBC-011) | AHDA (PMO, Cybersecurity) | Those categories are read by no one until a profile grants them (TASK-110's editor) |
| F-2 | **`FINANCIAL_SOURCE_MANAGE` ships to no role:** who sets a project's MANUAL/INTEGRATED/HYBRID source mode (ADR-008) is not stated | AHDA (PMO, Finance) | Every source mode stays the default |
| F-3 | **R06 export** waits for AHDA's confirmation (review H3) | AHDA (PMO) | Viewers run reports and export nothing |
| F-4 | **Documents need AHDA's classification taxonomy (UGV-01)** to be readable at all: a permission without clearance clears nothing (`authorization-engine.md` D-6). The grants are in place; the taxonomy is configuration | AHDA Cybersecurity | Documents stay unreadable in a fresh environment until DATA_CLASSIFICATION is seeded |
| F-5 | **No index serves "registrant of a project with no PM"** (D-5). Drafts are few, so the register's OWN query stays fast; a partial index on `created_by WHERE project_manager_user_id IS NULL` is the remedy if it is not | Engineering | None measured |
| F-6 | **The e2e data set and the showcase fixture** grant on top of the shipped profiles; both are rebuilt in the demo-data phase (review M-7) | Engineering | Until then the showcase still shows the review's symptoms |
| F-7 | **An entity Project Manager enters figures it cannot read back.** The review requires amounts to be absent for every external user (D-4), and R04 holds `FINANCIAL_SUBMIT` at OWN, so an entity Project Manager can submit a financial update whose amounts it then sees withheld. ADR-013's amendment to TASK-052 gives entities the view only; whether an entity Project Manager submits financials at all is AHDA's to confirm | AHDA (PMO) | If not, `FINANCIAL_SUBMIT` joins `GrantPolicy`'s internal-only permissions |

## 9. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-10-25 | Created: the matrix, `GrantPolicy`, `FINANCIAL_VIEW_SENSITIVE`, ASSIGNED for a project-bound internal assignment, registrant ownership, internal anchor rules, no reports for external users | Delivery team |

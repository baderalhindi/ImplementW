# Server-Side RBAC and Data-Scope Authorization Engine

| Field | Value |
| --- | --- |
| Task | TASK-030 — Implement Server-Side RBAC & Data-Scope Authorization Engine (P5 - Identity & Access Management, FG-03) |
| Depends on | TASK-028 — Integrate AD/LDAP and SSO Authentication (`sso-directory-integration.md`): the session token whose `sub` the engine authorises; TASK-029 (`mfa-privileged-access.md`): the MFA policy the gate re-checks |
| Record date | 2026-09-26 |
| Status | **ENGINE BUILT AND VERIFIED LOCALLY; MATRIX PARTIAL.** Blueprint Appendix A is not in the repository or Drive (`controlled-source-baseline.md` §5.1; seed record F-1), and the user decided on 2026-09-26 to build without it. Only the permission rows a controlled source fixes for all eight roles are shipped (D-9, F-1) |
| Branch | `feat/task-030-rbac-data-scope-engine` |
| Deliverables | Authorization engine: `PMPlatform.Application/Common/Authorization` (`AuthorizationEngine`, `FieldMask`, the request, subject and decision types, the `IAuthorizationRepository` port) and `PMPlatform.Infrastructure/Persistence/Authorization/AuthorizationRepository.cs`. Policy definitions: `PermissionCatalogue` and its seed rows in `db/seed/seed-master-data.sql`; the API gate `PMPlatform.Api/Authorization/RequirePermissionAttribute.cs`, `PermissionAuthorizationHandler.cs`, `EndpointAuthorization.cs`. Test suite: 152 unit tests under `PMPlatform.Tests.Unit/Application/Authorization`, 17 integration tests under `PMPlatform.Tests.Integration` (§5). This record |
| Environment variables / secrets | None. The engine reads the `sub` of the platform's own access token (TASK-028), which rests on the `SSO_OIDC_*` and `AD_*` configuration already in the sheet |
| Gate decision applied | **ADR-010 (EXTENDED).** Record-level scoping plus field-level masking by audience, one decision per audience and entity for every projection type (D-8). The taxonomy and field list are outstanding (UGV-01), so no field is classified yet |
| Participation amendments | **ADR-013**: authorization resolves from scope, assignment and gates, not employer; a per-project assignment covers only its project; cross-entity isolation is absolute (D-3). **ADR-018**: grants are read from the profile version each assignment is bound to (D-2). **ADR-019**: Personalize Layout and Compose Report to R02, R03, R07; withheld from R04, R05, R06, R08 (D-9) |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), Implementation Plan row TASK-030, as read 2026-09-26; controls CTL-08 and CTL-19 (`cybersecurity-control-matrix.md`); `api-conventions.md` R-20, R-47 |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | The Section 10.1 formula: permission + data scope + project/business relationship + assignment/ownership + record/lifecycle state + workflow authority + sensitivity restriction | This task |
| **In** | Scopes ALL, DEPT, OWN, ASSIGNED, ENTITY, READ-ONLY for any role, any profile | This task |
| **In** | Field-level masking by audience (ADR-010) | This task |
| **In** | A server-side gate on every endpoint, enforced at start-up | This task |
| **In** | The permission rows a controlled source fixes (D-9) | This task |
| **Out** | The rest of the Appendix A matrix (F-1) | PMO supplies Appendix A; then added here |
| **Out** | The classification taxonomy, the clearance of each permission and the classified field list (F-2) | AHDA Cybersecurity (UGV-01) |
| **Out** | Record-level checks and collection filtering on business endpoints: none exists yet (F-8) | Each module's first endpoint (TASK-041 onward) |
| **Out** | Authorization denials as Formal Audit events (CTL-25) | TASK-033 |
| **Out** | Authoring, versioning and migrating profiles | TASK-110, TASK-031 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **The engine is in `Application/Common/Authorization` and never reads a domain module.** A module passes the record's facts as an `AuthorizationSubject`: project, department, entity, owner, assignees, whether its state admits change, its workflow actors, its classification. The engine returns a decision. | Solution-architecture M-7 and E-U1; the workbook's directory. Otherwise IdentityAccess would need a read edge into all 20 modules. |
| D-2 | **Grants are read from the database on every request, never from the token.** They come from the grants of the profile version each assignment in force is bound to, and only a PUBLISHED version grants. `AuthorizationRepository` joins assignment → version → profile → role → grant → permission. The engine is scoped, so this happens once per request. | ADR-018: "effective-permission calculation now resolves against the assigned profile version". TASK-031: a change applies "on next authorization check without requiring a re-login". A DRAFT version is unreviewed, so it grants nothing. |
| D-3 | **Grants combine by union, and ADR-013 bounds every one.** A request is allowed if any single grant allows it. Whatever the scope, three rules apply. (1) An assignment with `project_id` covers only that project. (2) An external user never reaches a record of another entity, or of no entity. (3) An external user's assignment counts only for R04 or R08, as it already did at sign-in. | ADR-013: an entity Project Manager holds R04 "scoped to their own project only; cross-entity isolation absolute". The boundary is scope, not employer, so the same R04 grant gives an internal holder no such limit. |
| D-4 | **Scope semantics.** ALL: every record. DEPT: the record's department equals the assignment's `department_id`, or the user's directory department if the assignment has none. OWN: the record's owner is the user. ASSIGNED: the user is among the record's assignees. ENTITY: the record's entity equals the assignment's `external_entity_id`, or the user's entity. READ-ONLY: every record the D-3 bounds allow, for permissions that only read (D-9). | The ERD scope anchors on `access_relationship`. DEPT is the exact department, not its sub-departments (F-3). READ-ONLY's extent is not stated anywhere (F-7). |
| D-5 | **Lifecycle state and workflow authority apply last.** A record whose state admits no change refuses every write permission. A record with a workflow step refuses a caller who is not one of its actors. Both answer 403, because the caller can already see the record. | Blueprint Section 10.1's fifth and sixth terms. Which states lock a record, and who acts on a step, belong to the owning module, so the caller supplies them. A transition the lifecycle forbids is still the module's 409 (R-24). |
| D-6 | **Sensitivity: a permission's clearance against a record's classification.** `permission.data_classification_item_id` is the highest classification it may expose. Rank is the DATA_CLASSIFICATION item's `sort_order`, higher meaning more sensitive. An unclassified record is visible to all. An unknown classification, or a permission without clearance, clears nothing, so the check fails closed. | ADR-010. The ERD holds the ceiling on the permission and the classification as a master data item; the ordering convention is this task's (F-2). |
| D-7 | **Three outcomes: Allowed, Forbidden (403), NotFound (404).** If the permission fails on a record, the engine asks whether any permission of the same `permission_group` lets the caller see that record: 403 if so, otherwise 404, the same answer as a nonexistent id. The reason (`AuthorizationDenial`) is logged and never returned. | api-conventions R-47, least disclosure. A 403 on a record the caller cannot see would confirm it exists across entities (TASK-066). |
| D-8 | **Field masking: one `FieldMask` per audience and entity.** It is built from the FIELD_CLASSIFICATION version in force (latest PUBLISHED and effective now) and the clearance of the permission the entity is read under. A field above the clearance gets its configured rule (WITHHOLD or MASK); a REVEAL rule, or a cleared audience, sees it. The mask takes no argument naming the projection. A restricted field is omitted and listed in `maskedFields`. | ADR-010: "applied consistently to screens, dashboards, reports, exports and API projections". Leaving the projection out of the inputs is what makes the result identical for all five (CTL-19). R-20 fixes the representation. |
| D-9 | **The catalogue lives in code and is seeded; only source-backed rows ship.** `PermissionCatalogue` defines each permission (code, group, read or write). The seed writes each definition and the grants of the shipped-default versions 1, and `ThePermissionCatalogueAndShippedGrantsAreTheCodes` fails if the two differ. Shipped: `IDENTITY_INTEGRATION_MANAGE` to R01 at ALL, since TASK-028 built ADM-041 for R01 only; `LAYOUT_PERSONALIZE` and `REPORT_COMPOSE` to R02, R03, R07 at OWN (ADR-019). | Appendix A is absent (F-1). A row that no source fixes for all eight roles would be a guess at security policy. ADR-019 names no scope; OWN holds because each permission acts on the holder's own layout or report definition, and the report's data is authorised separately for each viewer (CTL-15; F-4). |
| D-10 | **Every endpoint declares its protection, and start-up refuses one that does not.** The declaration is one of: `[RequirePermission(code)]` (the engine's gate), `[AllowAnyAuthenticatedUser]` (acts on the caller's own session only), or `[AllowAnonymous]`. `RequireEndpointAuthorization` stops the API if an endpoint declares none, or names a permission outside the catalogue. The TASK-028 stop-gap policy `SystemAdministrator` (role claim R01) is removed. | The acceptance criterion "every protected API endpoint calls the authorization engine server-side", as a rule that is checked. The fallback policy (authenticated) remains, so a missed declaration fails closed as well as loudly. |
| D-11 | **The gate re-checks MFA against the roles held now.** If the user's current grants come from a role on `Identity:Mfa:RequiredRoles` and the token did not pass MFA, the permission is not granted. | D-2 moves roles from the token to the database. Without this, an assignment to R01 made mid-session would reach a token that never passed MFA until the token expired (CTL-07). Refresh already refuses this (TASK-029 D-4). |
| D-12 | **Denials are logged, not audited.** Each is logged at Information with the user id, the permission code, the outcome and the reason; no record data is logged. | CTL-25's audit event for "every authorization denial" is TASK-033's (F-6). CTL-27: nothing sensitive in logs. |

## 3. How a module uses the engine

| Step | What | Answer |
| --- | --- | --- |
| 1 | Put `[RequirePermission(PermissionCatalogue.X)]` on the controller or action | 401 without a valid token; 403 `PERMISSION_DENIED` unless the caller holds X at some scope |
| 2 | Load the record, build its `AuthorizationSubject`, call `IAuthorizationEngine.AuthorizeAsync(userId, new(X, subject))` | `NotFound` → 404 `NOT_FOUND`; `Forbidden` → 403 `PERMISSION_DENIED`; `Allowed` → continue |
| 3 | Build the representation with `GetFieldMaskAsync(userId, readPermission, entityCode)` | Omit each field the mask does not reveal; list them in `maskedFields` |

A new permission is added to `PermissionCatalogue` and to the seed together; the seed test fails otherwise.

## 4. Configuration

Nothing new. The engine reads `identity_access` (grants) and `master_data_config` (DATA_CLASSIFICATION items, FIELD_CLASSIFICATION versions and rules). The MFA re-check (D-11) uses TASK-029's `Identity:Mfa:RequiredRoles`.

## 5. Verification

Run 2026-09-26 on macOS, Docker Desktop, PostgreSQL 17 (`AHDA-postgres`) and `AHDA-ldap`.

| # | Command | Result |
| --- | --- | --- |
| 1 | `dotnet build src/backend -warnaserror` | Build succeeded, 0 warnings |
| 2 | `dotnet test src/backend/PMPlatform.Tests.Unit` | 193 passed: 152 new under `Application/Authorization`, 41 existing (architecture tests A-1 to A-6 unchanged and green) |
| 3 | `DB_CONNECTION_STRING=… dotnet test src/backend/PMPlatform.Tests.Integration` | 188 passed, twice in a row: 17 new (9 in `AuthorizationEndpointTests` as 15 cases, 1 in `FieldClassificationTests`, 1 in `SeedDataTests`), 171 existing (`EverySeededLabelIsBilingual` now counts 87 labels, including permissions) |
| 4 | `docker compose -f infra/docker/docker-compose.yml up -d --build --wait api` | The stack migrates, seeds (7 grants, as in §2 D-9), validates data integrity with no violation, and starts, passing `RequireEndpointAuthorization` |
| 5 | Sign in as `local.r02`, then `GET /api/v1/identity-integration` and `GET /api/v1/sessions/current` with the token, called directly with curl, bypassing the SPA | 403 and 200 |
| 6 | Workbook validation check on the local stack: `local.r01` to `local.r08` (one active user per role) each sign in, then `GET /api/v1/identity-integration` and `POST /api/v1/identity-integration/test` directly with curl | R02–R08, all marked "—" for ADM-041: sign-in 201, both calls **403 `PERMISSION_DENIED`**. R01: sign-in 503 `UNAVAILABLE`, because R01 requires MFA and the compose stack has no MFA provider (TASK-029 D-8), so the allowed case is shown by row 7 |
| 7 | `dotnet test … --filter AuthorizationEndpointTests\|IdentityIntegrationEndpointTests`: the real API pipeline over `AHDA-postgres` and `AHDA-ldap`, with the test MFA provider | 19 passed. The allowed case: `R01IsAllowedIdentityIntegration`, `TheStatusShowsWhatIsConfigured…` and `TheConnectionTestReaches…` all 200 for R01 after MFA. The denied cases: `EveryRoleButR01IsRefusedIdentityIntegration` gives 403 for R02–R08 on both calls |

The tests, by acceptance criterion and validation check:

| Criterion | Tests |
| --- | --- |
| 1. Every protected endpoint calls the engine server-side | `EveryEndpointDeclaresItsServerSideProtection`; start-up check D-10 (mutation M-11) |
| 2. Hiding a UI control is never the only protection, shown by direct API calls | `EveryRoleButR01IsRefusedIdentityIntegration` (R02–R08, GET and POST), `R01IsAllowedIdentityIntegration`, `AnExternalUserHoldingR01IsRefused`, `R01GainedMidSessionGrantsNothingWithoutMfa`, `AnEndedAssignmentTakesEffectOnTheNextRequest`, `GrantsComeFromTheBoundPublishedProfileVersion`, `APermissionGrantedToAnotherProfileAppliesOnTheNextRequest`, `TheEngineHoldsNoInternalOnlyRoleOfAnExternalUser` |
| 3. One allow and one deny per role/scope combination | `ScopeCoverageTests`: each of R01–R08 at each of the six scopes, one allow and one deny (96 cases, R08 as an external user). `ShippedGrantTests`: each shipped cell allowed and denied outside its scope, and each withheld cell of the 8 × 3 matrix denied (403 at the gate, 404 on a record). Against the full Appendix A matrix: **not possible until it is supplied** (F-1) |
| Formula terms beyond scope | `EffectiveAuthorizationTests`: entity Project Manager held to their project; cross-entity isolation against ALL; locked state; workflow actors; clearance against classification (6 cases); 403 against 404 (R-47); union of assignments; DEPT falling back to the user's department; disabled or unknown user; permission outside the catalogue; principal read once per request |
| ADR-010 masking | `FieldMaskTests` (4), `FieldClassificationTests` (against the database: PUBLISHED version in force, a later DRAFT ignored, clearance by rank) |
| Workbook validation: 403 for a role marked "—", success for an allowed role | Rows 2 and 5 above; `EveryRoleButR01IsRefusedIdentityIntegration` and `R01IsAllowedIdentityIntegration` |

### 5.1 Mutation tests: each protection fails its test when removed

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | The gate grants whatever the engine decides | 11 of 15 in `AuthorizationEndpointTests` |
| M-2 | Cross-entity isolation removed | `NoScopeTakesAnExternalUserOutsideTheirEntity` |
| M-3 | A per-project assignment covers every project | `AnEntityProjectManagerReachesOnlyTheirOwnProject` |
| M-4 | Every denial answered 403 (R-47 removed) | 63 unit tests |
| M-5 | Lifecycle state ignored | `ALockedRecordCanBeReadButNotChanged` |
| M-6 | Any clearance clears any classification | 2 cases of `ARecordIsVisibleOnlyUpToThePermissionsClearance` |
| M-7 | The gate's MFA re-check removed | `R01GainedMidSessionGrantsNothingWithoutMfa` |
| M-8 | A DRAFT profile version grants | `GrantsComeFromTheBoundPublishedProfileVersion` |
| M-9 | An external user's R01 assignment honoured | `TheEngineHoldsNoInternalOnlyRoleOfAnExternalUser`. The API test `AnExternalUserHoldingR01IsRefused` still **passed**, because D-11 refuses the non-MFA token too: two independent layers |
| M-10 | A DRAFT FIELD_CLASSIFICATION version in force | `AFieldAboveThePermissionsClearanceIsMaskedAndADraftClassificationIsNot` (it first **passed**: the draft had no effective date. The test now dates it) |
| M-11 | `[AllowAnyAuthenticatedUser]` removed from `GetCurrentSession` | All 15: the API does not start |
| M-12 | DEPT covers every department | 10 unit tests |

## 6. Acceptance criteria, validation and gate decision

| Item | Result |
| --- | --- |
| Every protected endpoint calls the engine server-side | **MET** for the endpoints that exist (the 2 on ADM-041; the rest anonymous or session-only), and enforced at start-up for every endpoint added later (D-10) |
| Hiding a UI control is never the only protection, shown by direct API calls | **MET**: §5 rows 3 and 5 |
| One allow and one deny per role/scope combination in Appendix A | **MET FOR THE ENGINE, PARTIAL FOR THE MATRIX.** Every role × scope combination the engine can hold is tested both ways, and so is every cell of the shipped rows. The Appendix A cells beyond them cannot be tested because Appendix A is absent (F-1) |
| Validation check: 403 for a "—" role, success for an allowed role | **MET** for ADM-041, the one protected endpoint: 403 for all seven "—" roles live (§5 row 6), 200 for R01 (§5 row 7). Other Appendix A cells wait for F-1 |
| ADR-010: field-level masking by audience, consistent across projections | **MET** as a mechanism (D-8). No field is classified until UGV-01 (F-2) |
| ADR-013, ADR-018, ADR-019 | **MET** (D-2, D-3, D-9) |

## 7. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **Blueprint Appendix A is not available.** The catalogue ships 3 permissions and 7 grants. R04, R05, R06 and R08 hold no permission, and every business permission (projects, risks, approvals, …) is missing. Add each Appendix A row to `PermissionCatalogue` and the seed together, then add its cells to `ShippedGrantTests`. Correct the role labels while doing so (seed F-1) | PMO (document custody); the next backend task that needs a business permission | No role but R01 can call a business endpoint once one exists; the Appendix A acceptance cells stay unverified |
| F-2 | **The classification taxonomy is outstanding (UGV-01).** No DATA_CLASSIFICATION item, no permission clearance and no FIELD_CLASSIFICATION rule exists, so nothing is masked yet. Ranking by `sort_order` is this task's convention (D-6); if AHDA's taxonomy is not a single order, D-6 changes | AHDA Cybersecurity | ADR-010 masking has no effect in any environment until the taxonomy is seeded |
| F-3 | **DEPT is the exact department.** Whether a Department Manager's DEPT scope includes sub-departments (`department.parent_department_id`) is not stated in any source in the repository | PMO (Appendix A / FG-03) | A manager of a parent department sees nothing of its sub-departments |
| F-4 | **ADR-019 names no scope;** OWN is inferred (D-9) | PMO | If Appendix A gives another scope, change the two seed rows (and see seed F-12) |
| F-5 | **Shipped grants were added to PUBLISHED versions 1** (seed record F-12) | TASK-110 | None now; once an environment exists, a change needs a new version |
| F-6 | **Denials are logged, not yet audit events** (CTL-25) | TASK-033 | The audit trail has no authorization denials until TASK-033 |
| F-7 | **READ-ONLY's extent is an interpretation** (D-4): read-only, over everything the ADR-013 bounds allow. The ERD holds READ_ONLY as a scope, not as a mode on another scope | PMO (Appendix A) | If Appendix A means "read-only within DEPT" or similar, a READ-ONLY holder sees more than intended |
| F-8 | **No endpoint yet acts on a business record**, so record-level checks (§3 step 2) and masked projections (step 3) have no caller in the API. Collection endpoints will also need the grants turned into a query filter (R-3: "filters the collection server-side"); no such helper is built, since nothing would use it yet | TASK-041 and each module's first endpoint | A module that skips step 2 protects only by permission, not by scope; code review and each module's negative tests are the control |
| F-9 | **A RETIRED profile version grants nothing** (D-2 honours PUBLISHED only). TASK-110 must migrate assignments before retiring a version | TASK-110 | Users bound to a retired version silently lose every permission |
| F-10 | **Security Lead review (CTL-43) cannot be requested:** the CODEOWNERS teams do not exist on this personal-account repository | Maintainer | The PR's CTL-43 box stays unticked until a Security Lead reviews it |

## 8. Change log

| Date | Change |
| --- | --- |
| 2026-09-26 | Created (TASK-030) |

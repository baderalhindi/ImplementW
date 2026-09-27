# Users, Roles and Permissions Administration (FG-03 Backend)

| Field | Value |
| --- | --- |
| Task | TASK-031 — Build Users, Roles & Permissions Administration (FG-03 Backend) (P5 - Identity & Access Management, FG-03) |
| Depends on | TASK-030 — Implement Server-Side RBAC & Data-Scope Authorization Engine (`authorization-engine.md`): every endpoint here is gated and every record decided by that engine, which reads grants from the database on each request |
| Record date | 2026-09-27 |
| Status | **BUILT AND VERIFIED LOCALLY** against `AHDA-postgres` and `AHDA-ldap`, through the real API pipeline with the test MFA provider. Mobile-number verification has no SMS provider to run against (TASK-103, OQ-012), so it answers 503 in every environment until one exists (F-4) |
| Branch | `feat/task-031-fg03-identity-access-backend` |
| Deliverables | FG-03 API endpoints: 31 operations on seven controllers in `PMPlatform.Api/Controllers` (`Users`, `AccessRelationships`, `Roles`, `Permissions`, `PermissionProfiles`, `Departments`, `ExternalEntities`), their request models in `Models/IdentityAccess`, `SensitiveWriteAttribute` (R-36). Service layer: `PMPlatform.Application/Features/IdentityAccess/Administration` (six services, the repository ports, `IMobileNumberVerifier`) and its contracts in `Features/IdentityAccess/Contracts/Administration`; repositories in `PMPlatform.Infrastructure/Persistence/IdentityAccess`. Unit and integration tests: 49 unit tests under `PMPlatform.Tests.Unit/Application/IdentityAccess`, 33 integration tests in `AdministrationEndpointTests` and `AdministrationContractTests` (§5). This record |
| Environment variables / secrets | None. The SMS provider's variables are TASK-103's |
| Gate decision applied | None on the row |
| Participation amendments | **ADR-013**: the third user type is an EXTERNAL user holding a per-project grant with a named AHDA sponsor; access ends on project closure or role change (D-5, D-6). **ADR-004**: E.164 mobile number with a verification step; only a verified number is handed to the SMS channel (D-8). **ADR-018**: administration assigns profile versions, never ad hoc permissions; no per-user grant exists (BR-IAM-023) (D-5) |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), Implementation Plan rows TASK-030, TASK-031, TASK-032, TASK-033, TASK-068, TASK-103, TASK-110, as read 2026-09-27; `api-conventions.md`; `authorization-engine.md`; ERD §5.2; controls CTL-09 and CTL-25 (`cybersecurity-control-matrix.md`) |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | ADM-002–005 and MOD-080: user list, detail, create, edit, activate, disable | This task |
| **In** | ADM-006–009: roles, the permission catalogue, permission profiles and each version's grants (the matrix) | This task (read; role name edit) |
| **In** | ADM-010 and MOD-081: assigning a profile version to a user, and ending an assignment | This task |
| **In** | ADM-011–013 and MOD-082: organization structure, departments, external entities | This task |
| **In** | ADR-004 mobile number verification step; ADR-013 per-project grants, sponsor, role change and project closure | This task |
| **Out** | Authoring, validating, publishing and migrating profile versions; ADM-008 "create role" as a new profile | TASK-110 |
| **Out** | The SMS provider that sends and checks verification codes | TASK-103 (OQ-012) |
| **Out** | Audit events for every administration change (CTL-25) | TASK-033 |
| **Out** | Calling project-closure access ending from the project lifecycle | TASK-063 |
| **Out** | The administration UI | TASK-032 |
| **Out** | The rest of the Appendix A matrix | PMO (engine record F-1) |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Seven catalogue permissions, shipped to R01 at ALL.** `USER_VIEW` (ADM-002/003), `USER_MANAGE` (ADM-004/005, MOD-080), `ROLE_VIEW` (ADM-006/007/009), `ROLE_MANAGE` (ADM-008), `ROLE_ASSIGN` (ADM-010, MOD-081), `ORGANIZATION_VIEW` and `ORGANIZATION_MANAGE` (ADM-011–013, MOD-082), all in group `IDENTITY_ACCESS`. The four that change data are `is_privileged`. Added to `PermissionCatalogue` and the seed together; `ShippedGrantTests` and `ThePermissionCatalogueAndShippedGrantsAreTheCodes` keep the two in step. | TASK-032's acceptance criterion: every ADM-002–013 screen is "reachable only for R01 per RBAC". That fixes the row for all eight roles, which is the TASK-030 D-9 bar for shipping a grant. ALL because an administrator administers every user and structure. |
| D-2 | **Roles are canonical; only their name is edited.** No create or delete: R01–R08 are shipped and undeletable (ERD F-080, CTL-09), and what a role may do is a permission profile (ADR-018). `PUT /roles/{id}` edits the bilingual name, which the seed inserts once and leaves to AHDA. The permission catalogue is read-only here: it is defined in code and seeded (engine D-9). Profiles and their versions are read-only here: authoring them is TASK-110's. | ERD F-080, F-081; ADR-018; TASK-110 owns "create a profile … version it, publish it". ADM-008's "Create-Edit" becomes TASK-110's profile authoring once built. |
| D-3 | **A user is never deleted, and disabling changes the status and nothing else.** `disable` sets `status = DISABLED` and `disabled_at`; it ends no assignment and touches no other row. Access stops anyway: sign-in refuses a disabled user (TASK-028) and the engine treats them as inactive from the next request (TASK-030). `activate` restores the status and clears `disabled_at`; the assignments are as they were. | Appendix A.1: "deactivation does not rewrite historical ownership, assignments or decisions". Ending the assignments would rewrite them; leaving them makes reactivation exact. ERD delete class RETAIN, so no `DELETE` (R-5). |
| D-4 | **Directory attributes stay the directory's (ADR-007).** An internal user's department, manager and job title are copied from the directory at each sign-in, so the API does not edit them: a changed job title on an internal user is 422 `IDENTITY_ACCESS_DIRECTORY_AUTHORITATIVE`. An internal user needs a `directorySubjectId`, the key sign-in matches. User type and external entity are fixed at creation. MOD-082 ("Assign Department/Entity") is therefore the DEPT and ENTITY scope anchors of an assignment (ADM-010), and the entity of an external user at creation; a wrong internal department is fixed by the department's directory reference (ADM-012). | ADR-007: "the directory is authoritative for department, manager and job title". An edit here would be overwritten at the next sign-in. |
| D-5 | **Assignments bind a PUBLISHED profile version and are never edited or deleted.** There is no endpoint that gives a user a permission; the only grant path is a profile version (BR-IAM-023). A draft, validated or retired version is refused. An assignment starts now or later, never in the past, and ends after it starts. It ends (`end`, reason MANUAL) and stays as a row with its reason. A user holds at most one active assignment per project: a new one on the same project ends the old as ROLE_CHANGE. An identical active assignment is 409 `IDENTITY_ACCESS_ALREADY_ASSIGNED`. | ADR-018: "user administration assigns profiles, not ad hoc permissions"; ADR-013: access ends "on project closure or role change". A draft grants nothing (engine D-2), so assigning one would be a silent no-op. A backdated start would record access the user never had, the same rewrite of history Appendix A.1 forbids. |
| D-6 | **ADR-013 for external users, every rule reported at once.** The role must be external-eligible (R04, R08). The entity anchor is the user's own entity (defaulted; another is `OUTSIDE_ENTITY`). No department anchor. A named sponsor is required and must be an active internal user. R04 is per project, on a project the user's entity delivers. A closed project takes no new assignment. `IProjectAccessLifecycle.EndAccessForClosedProjectAsync` ends every active assignment on a closing project as PROJECT_CLOSED. | ADR-013: "a person employed by the delivering entity may hold the Project Manager role on their own entity's project, scoped to that project"; the amendment's "named AHDA sponsor". The engine already isolates entities absolutely (engine D-3); refusing the grant keeps the data from saying something the engine would never honour. |
| D-7 | **No administrator administers themselves.** Disabling oneself, assigning oneself, and ending one's own assignment are 422 `IDENTITY_ACCESS_SELF_ADMINISTRATION`. A SERVICE principal is not administered through the API (`IDENTITY_ACCESS_SERVICE_PRINCIPAL`). | Self-assignment is self-escalation; disabling oneself or ending one's own R01 can lock administration out. CTL-09's "R01 cannot self-approve" is the same principle for profiles. **Delivery-team decision**, not stated by a source (F-8). |
| D-8 | **ADR-004: a number is verified only by its holder.** E.164 is checked by the API (400) and by the existing database check. Any change of number clears `mobile_verified_at`. The user verifies their own number: `POST /users/current/mobile-verification-challenge`, then `…/mobile-verification` with the code. The code, its expiry and its attempt limit are the provider's (`IMobileNumberVerifier`); a challenge is bound to the user and the number it was sent to, so a code sent to a previous number verifies nothing. Until TASK-103 supplies a provider, `UnconfiguredMobileNumberVerifier` answers 503 and nothing can be verified. `IUserContactDirectory` hands another module the number only if it is verified and the user is active. | ADR-004: "an unverified number would send AHDA project information to a stranger". Keeping codes in the provider avoids a code table the ERD does not have and puts brute-force limits where the codes are. |
| D-9 | **Every endpoint is gated, and every record is decided by the engine.** The gate is `[RequirePermission]` (engine D-10). Each service then asks the engine about the record with its anchors (a user's department, entity and self; an assignment's anchors, project and user): 404 if the caller may not see it, 403 if they may see it but not do this (R-47). A collection is served only to a grant that covers an unanchored record, which only ALL and READ-ONLY do; a narrower scope is refused with 403 rather than shown everything, because no scope filter for collections exists yet (engine F-8). Roles, permissions and profiles are platform reference data with no record scope: the gate is their whole check. | The acceptance criterion of TASK-030 carried forward. Failing closed on collections means a future DEPT-scoped `USER_VIEW` (Appendix A) sees nothing rather than everyone. |
| D-10 | **The API conventions, as far as the platform implements them.** No `PATCH` and no `DELETE` (R-5). `PUT` replaces the editable representation and requires `If-Match` (428 absent, 412 stale); commands on users, departments and entities honour `If-Match` when sent (R-21). The ETag is PostgreSQL `xmin` (ERD D-16), mapped as a shadow row version on `user`, `department`, `external_entity` and `role`; the migration `TASK-031_MapRowVersionConcurrencyTokens` records the mapping and generates no DDL in either direction. Every `POST` and `PUT` here is a sensitive write (§5 of the conventions: identity, access, configuration) and requires a uuid `Idempotency-Key` (R-36). Offset paging (R-29), one query parameter per filter and comma-separated sets (R-31), `sort` on the user list (R-32). Module error codes are `IDENTITY_ACCESS_*` (R-27, §4). | `api-conventions.md` R-5, R-21, R-27 to R-32, R-36, R-47. |
| D-11 | **Step-up on the four operations that change who may do what.** `IdentityAccess_ActivateUser`, `IdentityAccess_DisableUser`, `IdentityAccess_CreateAccessRelationship` and `IdentityAccess_EndAccessRelationship` are added to `Identity:StepUp:Operations`. | ADR-010: step-up applies to privileged actions; the list is configuration (TASK-029 D-5). |
| D-12 | **Changes are logged with ids only; audit events are TASK-033's.** Each change logs the actor, the change and the record id; no name, email or number. | CTL-27; CTL-25 is TASK-033's (as engine D-12). |

## 3. Endpoints

All paths are under `/api/v1`, tag `IdentityAccess`. Every non-2xx answer is the R-23 envelope. "Record" means the engine decides the record: 404 out of scope, 403 visible but not permitted.

| Operation | Method and path | Permission | Success | Refusals beyond 401/403 |
| --- | --- | --- | --- | --- |
| `ListUsers` | `GET /users?status=&userType=&departmentId=&externalEntityId=&q=&sort=&page=&pageSize=` | `USER_VIEW` | 200 `UserPage` | 400 |
| `GetUser` | `GET /users/{id}` | `USER_VIEW`, record | 200 `UserDetail` + ETag | 404 |
| `CreateUser` | `POST /users` | `USER_MANAGE` | 201 `UserDetail`, Location, ETag | 400; 409 `…_DUPLICATE_KEY`; 422 `…_SERVICE_PRINCIPAL`, `…_DIRECTORY_AUTHORITATIVE`, `…_REFERENCE_INVALID` |
| `UpdateUser` | `PUT /users/{id}` + `If-Match` | `USER_MANAGE`, record | 200 `UserDetail` + ETag | 400; 404; 409; 412; 422; 428 |
| `ActivateUser` / `DisableUser` | `POST /users/{id}/activate` \| `/disable` | `USER_MANAGE`, record; step-up | 200 `UserDetail` | 404; 409 `INVALID_TRANSITION`; 412; 422 `…_SELF_ADMINISTRATION`, `…_SERVICE_PRINCIPAL` |
| `CreateMobileVerificationChallenge` | `POST /users/current/mobile-verification-challenge` | the caller's own record | 200 `MobileVerificationChallenge` | 409 (already verified); 422 `…_MOBILE_NUMBER_MISSING`; 503 |
| `VerifyMobileNumber` | `POST /users/current/mobile-verification` | the caller's own record | 200 `MobileVerificationResult` | 400; 409; 422 `…_MOBILE_VERIFICATION_FAILED`; 503 |
| `ListAccessRelationships` | `GET /access-relationships?userId=&projectId=&status=&page=&pageSize=` | `USER_VIEW` | 200 `AccessRelationshipPage` | 400 |
| `GetAccessRelationship` | `GET /access-relationships/{id}` | `USER_VIEW`, record | 200 `AccessRelationshipDetail` | 404 |
| `CreateAccessRelationship` | `POST /access-relationships` | `ROLE_ASSIGN`; step-up | 201 `AccessRelationshipDetail`, Location | 400; 409 `…_ALREADY_ASSIGNED`; 422 `…_REFERENCE_INVALID`, `…_EXTERNAL_GRANT_INVALID`, `…_PROFILE_VERSION_NOT_PUBLISHED`, `…_PROJECT_CLOSED`, `…_INVALID_PERIOD`, `…_SELF_ADMINISTRATION`, `…_SERVICE_PRINCIPAL` |
| `EndAccessRelationship` | `POST /access-relationships/{id}/end` | `ROLE_ASSIGN`, record; step-up | 200 `AccessRelationshipDetail` | 404; 409 `TERMINAL_STATE`; 422 `…_SELF_ADMINISTRATION` |
| `ListRoles` / `GetRole` | `GET /roles` (unpaged, 8 rows) \| `/roles/{id}` | `ROLE_VIEW` | 200 `RoleSummary[]` \| `RoleDetail` + ETag | 404 |
| `UpdateRole` | `PUT /roles/{id}` + `If-Match` | `ROLE_MANAGE` | 200 `RoleDetail` + ETag | 400; 404; 412; 428 |
| `ListPermissions` | `GET /permissions` (unpaged) | `ROLE_VIEW` | 200 `PermissionSummary[]` | — |
| `ListPermissionProfiles` / `GetPermissionProfile` | `GET /permission-profiles?baseRoleId=&page=&pageSize=` \| `/permission-profiles/{id}` | `ROLE_VIEW` | 200 `PermissionProfilePage` \| `PermissionProfileDetail` (versions with grants) | 400; 404 |
| `ListDepartments` / `GetDepartment` | `GET /departments?isActive=&parentDepartmentId=&q=&page=&pageSize=` \| `/departments/{id}` | `ORGANIZATION_VIEW` (record on get) | 200 `DepartmentPage` \| `DepartmentDetail` + ETag | 400; 404 |
| `CreateDepartment` / `UpdateDepartment` | `POST /departments` \| `PUT /departments/{id}` + `If-Match` | `ORGANIZATION_MANAGE` | 201 \| 200 `DepartmentDetail` | 400; 404; 409 `…_DUPLICATE_KEY` (code, directory reference); 412; 422 `…_REFERENCE_INVALID`, `…_DEPARTMENT_CYCLE`; 428 |
| `ActivateDepartment` / `DeactivateDepartment` | `POST /departments/{id}/activate` \| `/deactivate` | `ORGANIZATION_MANAGE`, record | 200 `DepartmentDetail` | 404; 409; 412 |
| `ListExternalEntities` / `GetExternalEntity` | `GET /external-entities?status=&entityTypeItemId=&q=&page=&pageSize=` \| `/external-entities/{id}` | `ORGANIZATION_VIEW` (record on get) | 200 `ExternalEntityPage` \| `ExternalEntityDetail` + ETag | 400; 404 |
| `CreateExternalEntity` / `UpdateExternalEntity` | `POST /external-entities` \| `PUT /external-entities/{id}` + `If-Match` | `ORGANIZATION_MANAGE` | 201 \| 200 `ExternalEntityDetail` | 400; 404; 409 `…_DUPLICATE_KEY`, `TERMINAL_STATE`; 412; 422 `…_REFERENCE_INVALID`; 428 |
| `SuspendExternalEntity` / `ActivateExternalEntity` / `RetireExternalEntity` | `POST /external-entities/{id}/suspend` \| `/activate` \| `/retire` | `ORGANIZATION_MANAGE`, record | 200 `ExternalEntityDetail` | 404; 409 `INVALID_TRANSITION`, `TERMINAL_STATE`; 412 |

Every `POST` and `PUT` above also answers 400 `IDEMPOTENCY_KEY_REQUIRED` or `IDEMPOTENCY_KEY_INVALID`. Operation ids are prefixed `IdentityAccess_` (R-50).

## 4. Error codes

Module codes (`IdentityAccessErrorCodes`, R-27). `errors[]` names each field with a code (`REQUIRED`, `NOT_FOUND`, `INACTIVE`, `DUPLICATE`, `NOT_ALLOWED`, `OUTSIDE_ENTITY`, `NOT_EXTERNAL_ELIGIBLE`, `DATE_BEFORE_START`) and never echoes a value (R-25).

| Code | Status | When |
| --- | --- | --- |
| `IDENTITY_ACCESS_DUPLICATE_KEY` | 409 | Username, email, directory subject or code taken; a directory reference held by another active department. `errors[]` names the field. PostgreSQL reports the first unique index a row breaks, so one field is named at a time (F-11) |
| `IDENTITY_ACCESS_ALREADY_ASSIGNED` | 409 | The same active assignment exists |
| `IDENTITY_ACCESS_REFERENCE_INVALID` | 422 | A referenced user, sponsor, department, entity, entity type, project or profile version does not exist or may not be used |
| `IDENTITY_ACCESS_EXTERNAL_GRANT_INVALID` | 422 | An ADR-013 rule (D-6); `errors[]` lists every rule broken |
| `IDENTITY_ACCESS_PROFILE_VERSION_NOT_PUBLISHED` | 422 | The version is not PUBLISHED |
| `IDENTITY_ACCESS_PROJECT_CLOSED` | 422 | A new assignment on a CLOSED project |
| `IDENTITY_ACCESS_SELF_ADMINISTRATION` | 422 | D-7 |
| `IDENTITY_ACCESS_SERVICE_PRINCIPAL` | 422 | A SERVICE principal created, changed or assigned |
| `IDENTITY_ACCESS_DIRECTORY_AUTHORITATIVE` | 422 | An internal user's job title entered (ADR-007) |
| `IDENTITY_ACCESS_INVALID_PERIOD` | 422 | An assignment starting in the past, or ending at or before its start |
| `IDENTITY_ACCESS_DEPARTMENT_CYCLE` | 422 | A department moved under itself or a descendant |
| `IDENTITY_ACCESS_MOBILE_NUMBER_MISSING` | 422 | Verification without a number |
| `IDENTITY_ACCESS_MOBILE_VERIFICATION_FAILED` | 422 | The code was not accepted for the number stored now |

Platform codes added to `ErrorCodes`: `NOT_FOUND`, `INVALID_TRANSITION`, `TERMINAL_STATE`, `PRECONDITION_FAILED`, `PRECONDITION_REQUIRED`, `IDEMPOTENCY_KEY_REQUIRED`, `IDEMPOTENCY_KEY_INVALID`. Field codes added to `FieldError`: `ENUM_VALUE`, `OUT_OF_RANGE`, `NOT_ALLOWED`.

## 5. Verification

Run 2026-09-27 on macOS, Docker Desktop, PostgreSQL 17 (`AHDA-postgres`) and `AHDA-ldap`.

| # | Command | Result |
| --- | --- | --- |
| 1 | `dotnet build src/backend -warnaserror` | Build succeeded, 0 warnings, 0 errors |
| 2 | `dotnet test src/backend/PMPlatform.Tests.Unit` | 305 passed: 49 new under `Application/IdentityAccess`, 63 new cases of `ShippedGrantTests` (the 7 granted cells allowed and denied, the 49 new withheld cells denied), 193 existing. Architecture tests A-1 to A-6 unchanged and green |
| 3 | `DB_CONNECTION_STRING=… dotnet test src/backend/PMPlatform.Tests.Integration` | 221 passed, twice in a row: 33 new (12 in `AdministrationEndpointTests`, 21 in `AdministrationContractTests`), 188 existing. `EverySeededLabelIsBilingual` now counts 94 labels |
| 4 | `docker compose -f infra/docker/docker-compose.yml up -d --build --wait api` | Migrates to `20260927191019_TASK-031_MapRowVersionConcurrencyTokens`, seeds 10 permissions and 14 grants, validates data integrity with no violation, and starts, passing `RequireEndpointAuthorization` and `RequireKnownStepUpOperations` |
| 5 | Sign in as `local.r02` … `local.r08`, then `GET` each of `/users`, `/roles`, `/departments`, `/external-entities`, `/access-relationships`, `/permissions`, `/permission-profiles` and `POST /users`, directly with curl | R02–R08: all eight calls **403** for every role. No token: 401. `local.r01`: sign-in 503, because R01 requires MFA and the compose stack has no MFA provider (TASK-029 D-8), so R01's allowed calls are shown by row 3 |
| 6 | `python3 docs/architecture/contract-check.py <openapi.v1.json from the running API>` | 186 findings over 42 operations; 134 on the 31 FG-03 operations, each an R-52 extension the platform does not emit yet (F-3). The committed sample still lints with 0 findings |
| 7 | `migration-forward-only.sh`, `erd-check.py`, and the other `docs/architecture/*-check.py` gates of `ci-quality-gates.yml` | All OK |

The tests, by acceptance criterion and validation check:

| Criterion | Tests |
| --- | --- |
| 1. Deactivating a user does not delete or null out records they owned or acted on | `DisablingAProjectManagerKeepsEveryRecordThatNamesThem` (API, database): the project still names user 8 as manager, their three assignments keep status and reason, they are still readable by id, and they can no longer sign in; reactivation changes none of it. `DisablingAUserChangesOnlyTheirStatus` (unit): every attribute of the user but status, disabled time and update stamp is unchanged |
| 2. Role and permission changes apply on the next authorization check without a new sign-in | `AnAssignmentMadeOrEndedTakesEffectOnTheUsersNextRequest`: one token, 403 → 200 after an assignment through the API → 403 after it is ended. `ADisabledUsersTokenGrantsNothingFromTheNextRequest`: 200 → 403 after disabling. The engine's own (TASK-030): `AnEndedAssignmentTakesEffectOnTheNextRequest`, `APermissionGrantedToAnotherProfileAppliesOnTheNextRequest` |
| 3. Full ADM-002–013 API with request validation and consistent errors | `AdministrationContractTests` (21): each resource round-trips; `AMalformedRequestNamesEveryFieldAndNoValue`; `AMalformedQueryIsRefused` (4); `ASensitiveWriteRequiresAnIdempotencyKey` (2); `ATakenKeyAndARepeatedTransitionAreConflicts`; `AnUnknownIdIsNotFound` (6); ETag and `If-Match` 428/412 in `AUserIsCreatedReadListedAndEditedUnderTheETag`. Each refusal is checked for status, `code`, `type`, `instance`, `correlationId`, `timestamp`, `idempotencyKey` and `application/problem+json` |
| ADM-002–013 for R01 only | `EveryAdministrationEndpointNamesItsPermission`; `EveryRoleButR01IsRefusedAdministration` (R02–R08, seven calls each); `ACallerWithoutTheManagePermissionIsRefused`, `ADepartmentScopedViewerSeesTheirDepartmentAndNoList` (unit, R-47 and D-9) |
| ADR-013 | `AnExternalUsersGrantsFollowTheParticipationModel` (API); `AnExternalGrantBreakingADR013IsRefusedWithEveryRuleItBreaks` (7 cases), `AnEntityProjectManagerIsGrantedR04OnTheirOwnProject`, `TheSponsorIsAnActiveInternalUser`, `ANewRoleOnAProjectEndsThePreviousOneAsARoleChange`, `AClosedProjectTakesNoNewAccess`, `ClosingAProjectEndsItsAccessAndOnlyItsAccess` (unit); `ClosingAProjectEndsTheAccessItGave` (database) |
| ADR-004 | `TheHolderOfTheNumberVerifiesItAndAChangedNumberIsUnverified`, `WithoutAnSmsProviderAMobileNumberCannotBeVerified` (API); `MobileNumberVerificationServiceTests` (6); `ChangingTheMobileNumberUnverifiesItAndKeepingItDoesNot` |
| ADR-018 | `OnlyAPublishedProfileVersionIsAssigned`; no endpoint accepts a permission code |

### 5.1 Mutation tests: each protection fails its test when removed

Each mutation was applied to the source, the suite run, and the source restored. The first pass of M-10 to M-12 ran on a stale build (the restored files kept an older timestamp than the mutated binaries); those three were re-run after rebuilding, with the results below, and the full suite was re-run clean afterwards (§5 rows 2 and 3).

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | Disabling also clears the user's department | `DisablingAUserChangesOnlyTheirStatus` |
| M-2 | ADR-013 external-grant rules skipped | 7 cases of `AnExternalGrantBreakingADR013IsRefusedWithEveryRuleItBreaks` |
| M-3 | No role change on a project | `ANewRoleOnAProjectEndsThePreviousOneAsARoleChange` |
| M-4 | A DRAFT profile version is assignable | `OnlyAPublishedProfileVersionIsAssigned` |
| M-5 | A changed mobile number stays verified | `ChangingTheMobileNumberUnverifiesItAndKeepingItDoesNot` |
| M-6 | Every record-level decision allowed | `ACallerWithoutTheManagePermissionIsRefused`, `ADepartmentScopedViewerSeesTheirDepartmentAndNoList` |
| M-7 | An administrator may disable themselves | `AnAdministratorCannotDisableThemselves` |
| M-8 | A collection served to any scope | `ADepartmentScopedViewerSeesTheirDepartmentAndNoList` |
| M-9 | Any user may be a sponsor | `TheSponsorIsAnActiveInternalUser`, `TheEntityTypeAndSponsorMustBeUsable` |
| M-10 | `Idempotency-Key` not required | `ASensitiveWriteRequiresAnIdempotencyKey` (key absent) |
| M-11 | `If-Match` not required on `PUT` | `AUserIsCreatedReadListedAndEditedUnderTheETag` |
| M-12 | Disabling a user ends their assignments | `DisablingAProjectManagerKeepsEveryRecordThatNamesThem`, `AnAssignmentMadeOrEndedTakesEffectOnTheUsersNextRequest` |
| M-13 | Disabling a user clears them as project manager | `DisablingAProjectManagerKeepsEveryRecordThatNamesThem` |
| M-14 | The engine's grants cached across requests (the "next sign-in" design) | `AnAssignmentMadeOrEndedTakesEffectOnTheUsersNextRequest`, `ADisabledUsersTokenGrantsNothingFromTheNextRequest` |
| M-15 | An assignment may start in the past | `AnAssignmentStartsNoEarlierThanNowAndEndsAfterItStarts` |

A `[Produces("application/json")]` tried on the controllers turned every problem response into `application/json`; the envelope assertion caught it (`AnUnknownIdIsNotFound`, 6 cases) and it was removed.

## 6. Acceptance criteria, validation and amendments

| Item | Result |
| --- | --- |
| Deactivating a user does not delete or null out records they previously owned or acted on | **MET.** D-3; §5 criterion 1; M-1, M-12, M-13 |
| Role/permission changes take effect on next authorization check without re-login | **MET.** Engine D-2 (grants read per request) exercised through the new endpoints; §5 criterion 2; M-14 |
| Full CRUD API for ADM-002–013 with request validation and consistent error responses per the API conventions | **MET for the runtime contract**: every resource has create, read, list, full-representation update and its lifecycle commands, shape validation (400 with `errors[]`), module codes (409/422) and the R-23 envelope on every refusal. "Delete" is a terminal command, not `DELETE`, because every FG-03 table is RETAIN (R-5); roles have no create (D-2); the permission catalogue and profile versions are read-only here (D-2, TASK-110). **PARTIAL for the generated OpenAPI document**: the R-52 extensions are not emitted (F-3) |
| Validation: deactivate a test user who owns a Project and confirm the Project's ownership history still shows that user | **MET on the data that exists.** `DisablingAProjectManagerKeepsEveryRecordThatNamesThem`: `project.project_manager_user_id` still names the disabled user, and so do their assignments. There is no ownership-history table yet (Project module TASK-041, audit TASK-033), so re-run the check when one exists (F-12) |
| Validation: run the FG-03 endpoint contract tests | **MET.** `AdministrationContractTests`, 21 passed (§5 row 3) |
| ADR-013 | **MET** (D-5, D-6). The closure call from the project lifecycle is TASK-063's (F-7) |
| ADR-004 | **MET as a mechanism** (D-8); no number can be verified until TASK-103 supplies a provider (F-4) |
| ADR-018 / BR-IAM-023 | **MET** (D-5) |

## 7. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **Appendix A is still absent** (engine F-1). ADM-002–013 go to R01 only, from TASK-032's acceptance criterion. If Appendix A gives another role a narrower scope over users or structures (e.g. R03 `USER_VIEW` at DEPT), that role gets single records but no lists until a collection scope filter exists (D-9, engine F-8) | PMO (Appendix A); first task needing a scoped list | A scoped administrator sees no list, which is safe but not usable |
| F-2 | **Idempotency keys are required, not replayed.** `common.idempotency_record` is not in the ERD (api-conventions S-3, core-platform-schema G-3). A retried create answers 409 (`…_DUPLICATE_KEY` or `…_ALREADY_ASSIGNED`) and a retried command 409 `INVALID_TRANSITION`, instead of R-37's replayed response | Engagement Architect (ERD re-issue), then the idempotency filter | The SPA must treat those 409s on a retry as "already done" |
| F-3 | **The generated OpenAPI document does not meet R-49 to R-55.** On the 31 FG-03 operations `contract-check.py` reports 134 findings: `x-module` (C-4, 31), `default` response and a `ProblemDetails` schema (C-5, 31), `x-write-class` (C-7, 26), `X-Correlation-Id` response header (C-12, 35), `If-Match` as a declared header (C-8, 4), `x-unpaged` on roles and permissions (C-9, C-11, 6), `x-sortable` (C-10, 1). The 11 operations that already existed have the same classes of finding. Response schemas are declared and named per R-54. CI lints only the sample document | TASK-011 (attributes and transformers, api-conventions §9 row 3), TASK-015 (lint the generated document, §9 row 2), TASK-094 | The document cannot be the contract-test input TASK-043 expects until then |
| F-4 | **No SMS provider**: verification answers 503 everywhere and no number is verified (D-8). The provider must bind a challenge to user and number, expire it and limit attempts | TASK-103 (OQ-012) | No SMS reaches anyone, which ADR-004 prefers to reaching a stranger |
| F-5 | **Administration changes are logged, not audited** (CTL-25) | TASK-033 | No audit trail of user, assignment or structure changes until TASK-033 |
| F-6 | **A cross-module read.** `AccessRelationshipRepository.FindProjectAsync` reads `project.project` (delivering entity, closed) directly, because no Project contract exists yet. `access_relationship.project_id` already couples the tables (TASK-025). IdentityAccess → Project is not an ADR-003 §8.2 edge | Engineering Architect (ADR-003 edge), TASK-041 (Project contract) | The read moves to a Project contract query when one exists |
| F-7 | **Project closure does not yet end access.** `IProjectAccessLifecycle.EndAccessForClosedProjectAsync` exists and is tested; nothing calls it until project closure is built. The Closure → IdentityAccess command edge needs ADR-003 §8.2 | TASK-063; Engineering Architect | Until then, closure leaves per-project grants active; the engine still limits them to their project |
| F-8 | **The self-administration rule (D-7) is a delivery-team decision.** It also means the first R01 of an environment cannot be assigned through the API: it is a provisioning step (the seed creates roles and profiles, not people) | PMO / AHDA IT; DevOps for the bootstrap step | If the PMO rejects D-7, remove the checks; the bootstrap step must be written into the environment runbook either way |
| F-9 | **A disabled sponsor's grants continue.** ADR-013 requires a named AHDA sponsor when a grant is made; whether the sponsor's deactivation should end or transfer the grants they sponsor is not stated | PMO | Grants can outlive their sponsor's employment |
| F-10 | **Directory references are unique among active departments by application check only.** A concurrent create could leave two, and sign-in's department lookup expects at most one. A partial unique index (`directory_reference WHERE is_active`) would enforce it | TASK-026 index register (a new migration) | A rare race breaks sign-in's department refresh for that reference |
| F-11 | **One duplicate field at a time.** A request repeating username and email is told only about the first index PostgreSQL checks | — | The administrator fixes one field, then the next |
| F-12 | **No ownership history to show.** The validation check's "ownership history" is today the project's manager column; the Project module and audit add history later | TASK-041, TASK-033 | Re-run the check against the history when it exists |
| F-13 | **Security Lead review (CTL-43) cannot be requested** on this repository: the CODEOWNERS teams do not exist (engine F-10) | Maintainer | The PR's CTL-43 box stays unticked until a Security Lead reviews it |
| F-14 | **MOD-082 is the assignment's scope anchors** (D-4), not an edit of an internal user's department | TASK-032 | The UI should present MOD-082 on ADM-010, and an external user's entity at creation |

## 8. Change log

| Date | Change |
| --- | --- |
| 2026-09-27 | Created (TASK-031) |
| 2026-09-27 | TASK-032 builds the administration UI on this API (`identity-access-administration-ui.md`). F-14 applied: MOD-082 is the scope step of the assignment dialog, and an external user's entity is set at creation (UI D-6) |

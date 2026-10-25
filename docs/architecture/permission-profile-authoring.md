# Permission profile authoring

| Field | Value |
| --- | --- |
| Source of the change | TASK-110 (ADM-008, the governed permission profile), brought forward by the prototype review of 24 Oct 2026: critical gap C4 ("permission profiles can't be edited … AHDA can't adjust access without a code release") and step 6 of its fix order ("clone, edit, compare, two-person publish"). Branch `feat/task-110-permission-profiles`, stacked on `feat/review-home-and-portfolio` |
| Record date | 2026-10-25 |
| Status | **BUILT AND VERIFIED LOCALLY** |
| Builds on | `shipped-permission-matrix.md` (the eight shipped profiles and `GrantPolicy`), `identity-access-administration.md` (ADM-006–010), ERD D-12 (the governed lifecycle) |

## 1. Decisions

| # | Decision | Basis |
| --- | --- | --- |
| D-1 | **A profile is created on a canonical role, its version 1 a DRAFT copying the version chosen** (any profile's, the shipped defaults' included), or empty. A later version is a DRAFT copying the version named, or the profile's newest PUBLISHED one; one open DRAFT or VALIDATED version per profile at a time (409 `IDENTITY_ACCESS_PROFILE_DRAFT_OPEN`). R01–R08 and their shipped profiles are never created or deleted here | ADR-018 (a role's rights are its profile); the review's "clone" |
| D-2 | **The governed lifecycle, three people**: the author edits the grid while DRAFT; a second person validates; a third publishes (`GovernedLifecycle`, 422 `IDENTITY_ACCESS_SEPARATION_OF_DUTIES`). A PUBLISHED version never changes; a change is a new version | ERD D-12; the review's "two-person publish" is met and exceeded, as configuration publishing is (`master-data-configuration.md`) |
| D-3 | **No one approves access they would receive**: neither the reviewer nor the publisher may hold an active assignment of any version of the profile (422 `IDENTITY_ACCESS_SELF_APPROVAL`) | FG-03 separation of duties; `threat-model-authorization.md` RBAC-T09 |
| D-4 | **The grid is held to the catalogue and to `GrantPolicy` when edited, validated and published**: a permission outside the catalogue (`NOT_FOUND`), named twice (`DUPLICATE`), or one the base role may never hold (`NOT_ALLOWED`: business data to R01; approval, reports, integration monitoring or the SAR amounts to R08) is refused with 422 `IDENTITY_ACCESS_PROFILE_GRANT_INVALID`, one field error per grant (`grants[i].permissionCode`). The engine also drops such a grant at evaluation (`shipped-permission-matrix.md` D-2), so a profile cannot widen it either way | The review's C4 safety rules; the protected catalogue (ERD F-081) |
| D-5 | **Assignments stay on their version until explicitly migrated**: `POST /permission-profile-versions/{id}/assignment-migrations` moves every active assignment of the profile's older versions onto it, only if it is the profile's newest PUBLISHED version (422 `IDENTITY_ACCESS_MIGRATION_TARGET_INVALID`), with one audit event per assignment. A PUBLISHED version held by anyone, or a shipped default's only PUBLISHED version, is not retired (422 `IDENTITY_ACCESS_PROFILE_VERSION_IN_USE`); a DRAFT or VALIDATED one is abandoned by retiring it | ADR-018: a published version is what an assignment was granted; no one's access changes without a step that says so |
| D-6 | **Permissions and step-up**: authoring, validating, publishing and retiring need `ROLE_MANAGE` (ADM-008), migrating assignments `ROLE_ASSIGN` (ADM-010), reading a version `ROLE_VIEW`. Publish, retire and migrate are in `Identity:StepUp:Operations` | ADR-010; the four FG-03 operations that change who may do what are already step-up |
| D-7 | **Every step is audited, class PERMISSION_CHANGE**: `PermissionProfileCreated`, `.VersionDrafted` (source version, grant count), `.GrantsChanged` (one `grant_<permission code>` attribute per changed grant, old scope to new, plus the change summary), `.VersionValidated`, `.VersionPublished`, `.VersionRetired` (lifecycle state old to new), `.AssignmentsMigrated` (per assignment, old version to new) | The diff TASK-110 requires (ERD `AuditEventAttribute`) |
| D-8 | **R-21 concurrency**: the version carries an ETag (its `xmin`, mapped by the migration `TASK-110_MapPermissionProfileVersionRowVersion`, which adds no DDL); `PUT …/grants` requires `If-Match`, the commands honour it when sent | api-conventions R-21 |
| D-9 | **The editor (ADM-008) is a version page, the matrix (ADM-009) its way in**. The matrix offers "New permission profile" and opens the selected version. The version page shows the grid beside the base role's shipped default, marks each permission that differs and can show only those, names who prepared, validated and published it, and offers the steps its state and the caller allow: Save (the author, while DRAFT), Validate, Publish, Withdraw or Abandon, Start a new version, Move holders to this version, Copy into a new profile. The server holds every rule; the page says why a refusal came | The review's "clone, edit, compare" |

## 2. API

| Operation | Endpoint | Permission | Success | Refusals |
| --- | --- | --- | --- | --- |
| `CreatePermissionProfile` | `POST /permission-profiles` | `ROLE_MANAGE` | 201 version + ETag | 400; 409 duplicate code; 422 reference invalid |
| `CreatePermissionProfileVersion` | `POST /permission-profiles/{id}/versions` | `ROLE_MANAGE` | 201 version + ETag | 404; 409 draft open; 422 reference invalid |
| `GetPermissionProfileVersion` | `GET /permission-profile-versions/{id}` | `ROLE_VIEW` | 200 + ETag | 404 |
| `UpdatePermissionProfileGrants` | `PUT /permission-profile-versions/{id}/grants` + `If-Match` | `ROLE_MANAGE` | 200 + ETag | 400; 404; 409 not DRAFT; 412; 422 not the author, grant invalid; 428 |
| `ValidatePermissionProfileVersion` | `POST …/{id}/validate` | `ROLE_MANAGE` | 200 + ETag | 404; 409; 412; 422 author, self-approval, grant invalid |
| `PublishPermissionProfileVersion` | `POST …/{id}/publish` (step-up) | `ROLE_MANAGE` | 200 + ETag | as validate |
| `RetirePermissionProfileVersion` | `POST …/{id}/retire` (step-up) | `ROLE_MANAGE` | 200 + ETag | 404; 409 already retired; 412; 422 in use |
| `MigratePermissionProfileAssignments` | `POST …/{id}/assignment-migrations` (step-up) | `ROLE_ASSIGN` | 200 `{permissionProfileVersionId, migratedAssignments}` | 404; 422 not the newest PUBLISHED |

## 3. Tests

| Concern | Test |
| --- | --- |
| Clone, edit with the diff audited, three-person publish, a PUBLISHED version frozen, If-Match required | `PermissionProfileAuthoringTests.AProfileIsClonedEditedWithItsDiffAuditedAndPublishedByThreePeople` |
| Catalogue, duplicate and `GrantPolicy` refusals, one field error each; an unknown scope is 400 | `.AGridIsHeldToTheCatalogueAndToWhatItsBaseRoleMayHold` |
| No self-approval; one open draft per profile | `.NoOneApprovesAProfileTheyHold` |
| Assignments stay until migrated; a held or last shipped version is not retired | `.AssignmentsStayOnTheirVersionUntilMigratedAndAHeldOrLastShippedVersionIsNotRetired` |
| Only `ROLE_MANAGE` authors | `.OnlyAHolderOfRoleManageAuthorsAProfile`; `AdministrationEndpointTests.EveryAdministrationEndpointNamesItsPermission` |
| The editor: the author's grid against the shipped default and the whole grid saved with If-Match; another's DRAFT read only; validate, publish, migrate offered by state; a refusal explained; a new profile copying a version; axe | `permissionProfiles.test.tsx` |

## 4. Validation

| # | Check | Result |
| --- | --- | --- |
| 1 | `dotnet build PMPlatform.slnx` | 0 warnings, 0 errors |
| 2 | Unit tests | 2,170 passed |
| 3 | Integration: `PermissionProfileAuthoringTests` | 5 passed |
| 4 | Integration: the Identity and MasterDataConfig namespaces, `ClosedProjectWriteTests`, the OpenAPI and contract tests | 245 passed |
| 5 | `docs/api/openapi.v1.json` regenerated (`UPDATE_OPENAPI_SNAPSHOT=1`) | Eight operations added |
| 6 | Frontend, in the e2e runner: `tsc -b --noEmit`, `eslint src` | Clean |
| 7 | `vitest run src/features/identity-access src/shared/i18n src/app`, then `src/app src/content` after the guide changed | 123 and 50 passed |

## 5. Findings

| # | Finding | Owner | Effect |
| --- | --- | --- | --- |
| F-1 | **The R01 profile cannot be changed by the people who hold it.** Every administrator holds R01's shipped profile, so D-3 refuses each of them as its reviewer or publisher. A change to R01 needs two administrators who hold another profile on R01 (an "access reviewer" profile, created and published here, then assigned) | AHDA (IT governance) | Intended: no administrator approves their own access. AHDA decides who reviews R01 changes |
| F-2 | **No reason is recorded on a migration** (FG-03 §10.1 asks for one on privileged assignment, `threat-model-authorization.md` RBAC-T08) | Engineering | The audit names who moved whom and when, not why |
| F-3 | **A profile is not renamed or deactivated here**: only its versions are governed. A profile no one should hold is emptied by retiring its versions | Engineering | Its name stays as created |
| F-4 | **The editor shows one version at a time against the shipped default**; comparing two arbitrary versions side by side is the matrix's (choose the profile, then the version) | Engineering (UX) | Two steps rather than one |

## 6. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-10-25 | Created | Delivery team |

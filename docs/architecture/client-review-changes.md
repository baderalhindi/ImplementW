# Client review changes (TASK-112, branch `feat/task-112-client-review-changes`)

| Field | Value |
| --- | --- |
| Source | The client's numbered change list, received in parts from 7 Nov 2026 |
| Status | **In progress**: items 1, 2, 4 and 5 built; item 3 not received |
| Migration | `TASK-112_DeleteProjectsAndUsersUserPermissions` |

## 1. Done

| Item | Change |
| --- | --- |
| 1 | A whole SAR amount reads without ".00" on screens, in edit forms, and in PDF and Excel reports. A fraction stays exact. The API and CSV keep two places (R-16). |
| 2 | The governance profile's info button is removed; the client wanted an explanation, not a platform change. |
| 4 | R02 deletes a project in any state (§2). |
| 5 | Delete and disable users from the list and the user's page; per-user permissions (§3). |

## 2. Deleting a project (item 4, decided by the client)

`PROJECT_DELETE`, shipped to R02 at ALL. The API sets `project.deleted_at` and `deleted_by`; nothing else changes. A holder without it keeps the old rule: their own DRAFT, removed outright.

`PMPlatformDbContext.HideDeletedProjects` puts one query filter on `project.project` and on every entity with a `ProjectId` or `ScopeProjectId`, so registers, dashboards, reports, search, approvals, notifications and background jobs all lose the project and its records together. A new table with either property is covered without a change.

- **Not filtered:** `audit_activity`. The audit trail keeps the project, its name and the deletion.
- **Raw SQL is not filtered**, but what the repositories send through `FromSql` is composed with the filter. `DemoDataReset` and other plain SQL act on all rows, deleted ones included.
- **A child table with neither property** (a schedule activity, for one) is reached through its parent, which is filtered.
- **Register number:** `ix_project_legacy_reference` no longer counts a deleted project, so importing its RPMO number again makes a new project. The Formal Project ID is never reused.
- **Restoring** is a database change: clear `deleted_at` and `deleted_by`. If a new project took the register number, that row's `legacy_reference` must change first.
- **No step-up:** the endpoint is also a Project Manager's delete of their own draft. The dialog confirms by name, and the audit event records it.

## 3. Users (item 5, decided by the client)

**Disable** already existed (MOD-080) and blocks the next request. It was on the user's page only, so the client did not find it; it is now in the list's selection bar too.

**Delete** is `POST /users/{id}/delete` (`USER_MANAGE`, step-up), a command because the row stays. Status `DELETED`: no sign-in, no session, out of every list and picker, 404 from administration, no way back through the API. Active assignments end with `USER_DELETED`. History keeps the name and the username stays taken. An administrator cannot delete themselves. A record still naming the person as its Project Manager keeps naming them, as with a disabled user; moving it is a separate action.

**Custom permissions** are `identity_access.user_permission_override`, one row per user and permission: GRANT with a scope, or REMOVE, with a required reason.

| Operation | Permission | Step-up |
| --- | --- | --- |
| `GET /users/{id}/permissions` | `ROLE_VIEW` | No |
| `PUT /users/{id}/permissions/{code}` | `ROLE_ASSIGN` | Yes |
| `POST /users/{id}/permissions/{code}/clear` | `ROLE_ASSIGN` | Yes |

`AuthorizationRepository.FindPrincipalAsync` applies them, so sessions, the engine and the SPA's permission list see one answer on the next request. A REMOVE wins over every role. A GRANT joins each role assignment in force, with its anchors, so a per-project assignment still reaches only its project.

**Limits:**

- A user with no role cannot be granted anything (`IDENTITY_ACCESS_USER_HAS_NO_ROLE`), because a grant joins a role. Ending their last assignment leaves any GRANT stored and without effect.
- `GrantPolicy` is checked when granting and again in the engine: no business data for R01, no approval, reports or SAR amounts for an external user or R08.
- **Approval stages name roles.** `APPROVAL_DECIDE` granted to a person does not put them on a stage that names another role, and `RoleHolderDirectory` still routes tasks by role. A REMOVE stops the person deciding, but a task can still be addressed to them.
- This does not pass the three people a permission profile needs. That is the client's decision: threat RBAC-T30.

## 4. Not done

- Item 3 has not been received.
- The showcase is not deployed: it needs migrate and seed.
- A Microsoft or directory account deleted here stays in the directory; it just cannot sign in to the platform. The directory sync reads active users only, so it leaves a deleted one alone.

# Client review changes (TASK-112, branch `feat/task-112-client-review-changes`)

| Field | Value |
| --- | --- |
| Source | The client's numbered change list, received in parts from 7 Nov 2026 |
| Status | **In progress**: items 1, 2 and 4–8 built; item 3 not received; item 8 covers the project registers, not yet the admin lists |
| Migrations | `TASK-112_DeleteProjectsAndUsersUserPermissions`, `TASK-112_UserNationalIdAndDeletedDepartments`, `TASK-112_DeleteRegisterRecords`, `TASK-112_AddPasskeysAndSmsCodes` |

## 1. Done

| Item | Change |
| --- | --- |
| 1 | A whole SAR amount reads without ".00" on screens, in edit forms, and in PDF and Excel reports. A fraction stays exact. The API and CSV keep two places (R-16). |
| 2 | The governance profile's info button is removed; the client wanted an explanation, not a platform change. |
| 4 | R02 deletes a project in any state (§2). |
| 5 | Delete and disable users from the list and the user's page; per-user permissions (§3). |
| 6 | Delete a department (§5). |
| 8 | Edit and Delete in the rows of the project registers, for every role that manages them; the Viewer (R06) and the Executive (R07) see neither (§7). |
| 7 | National ID or Iqama number on the user, masked, with Nafath bound to it; a Microsoft sign-in only with the profile's e-mail (§6). |
| Follow-up, 13 Nov 2026 | Each language's field in its own language: an Arabic name (or description) has an Arabic letter and may still name a product in Latin letters; an English one has an English letter and no Arabic one. One check (`TextShape.IsInScript`) serves the API's `RequestValidation.Label` (every bilingual name and description: departments, entities, roles, profiles, master data, KPIs, stages, dashboards, reports) and the imports' text columns (`ImportColumn.Script`: departments, entities, master data, KPIs, project names), answering `NOT_ARABIC` or `NOT_ENGLISH`; the administration forms check the same while typing. Data already stored is not changed: the 52 RPMO list values whose English label is their Arabic text must be corrected before they can be saved again. |
| Follow-up, 13 Nov 2026 | Sign-in without a password: Face ID / Touch ID passkeys kept by the platform, and a code by SMS to a verified mobile number (§8). |
| Follow-up, 10 Nov 2026 | No verification code after sign-in: `Identity:StepUp:MaxAge` is the 8-hour session, so the code an administrator passes at sign-in covers the step-up operations. The list and the mechanism stay; AHDA Cybersecurity's acceptance is open (SR-14, `mfa-privileged-access.md`). |
| Follow-up, 10 Nov 2026 | The client removed ADM-011 Organization Structure: it repeated the Departments and External entities pages. Its address redirects to Departments, whose Parent department column keeps the hierarchy; the tab, page and help mention are gone. The spec's screen catalogue still names ADM-011, so a check of delivered screens against it should note this decision. |

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

## 5. Deleting a department (item 6, decided by the client)

`POST /departments/{id}/delete` (`ORGANIZATION_MANAGE`, step-up) sets `department.deleted_at` and deactivates it. It leaves the list, the
structure, every picker and the code list; its detail is 404. Records that name it keep the name.

It is refused with 409 `IDENTITY_ACCESS_DEPARTMENT_IN_USE`, one field issue `IN_USE` each, while it has any of:

- users not deleted (`users`), disabled ones included;
- sub-departments not deleted (`subDepartments`);
- projects not closed (`projects`);
- assignments in force (`assignments`).

Moving them is a separate step. The list's selection bar and each row offer Delete; the confirmation says a department still in use is refused.

## 6. National ID and the sign-in checks (item 7, decided by the client)

**Storage.** `user.national_id_sealed` holds the number sealed with AES-256-GCM. `national_id_fingerprint` is an HMAC-SHA256, unique where set. `national_id_last_digits` holds four digits for the mask. Both keys come by HKDF from the new secret `PERSONAL_DATA_KEY` (sheet, manifest and runbook F-5). Without the key the API answers 503 when a number is set or revealed, and refuses every Nafath verification. The API checks the number: 10 digits, starting with 1 (National ID) or 2 (Iqama), Luhn check digit.

**Who sees it.** Everyone sees `nationalIdMasked` (`******6789`) on the user record. `GET /users/{id}/national-id` returns the full number:

- only to the new `USER_NATIONAL_ID_VIEW`, shipped to R01 and grantable per user (§3);
- with `Cache-Control: no-store`;
- with every reveal audited as `IdentityAccess.UserNationalIdRevealed`.

The screen shows it only when asked and hides it again. `PUT /users/{id}/national-id` (`USER_MANAGE`, step-up) sets, changes or removes it, audited without the value.

**Nafath.** A verification is refused (`IDENTITY_MISMATCH`) unless the identity Nafath asserts has the user's fingerprint, or when the user has none. Setting a new number undoes an earlier verification. Accounts verified before this change stay verified. The claim read is `Identity:Nafath:NationalIdClaim`, `sub` until AHDA IT confirms it with Nafath. This closes SR-53 in code; it stays open until then.

**Microsoft sign-in.** The platform asks for the `profile` and `email` scopes on top of the configured ones. It refuses (`EMAIL_MISMATCH`) a sign-in whose `email` claim, or failing that `preferred_username`, is missing or is not the profile's e-mail, ignoring case. Microsoft keeps its own verification code; the platform sends none.

**Not built:**

- Sign-in through Nafath, and its biometric approval: they need AHDA's Nafath production contract.
- A job that re-seals every number under a new key.

## 7. Edit and Delete in the registers (item 8, decided by the client)

The client asked that every role but the Viewer has Edit and Delete in the tables. The client decided three things:

- R07 (Executive) stays read-only.
- A delete works as for projects: the record is hidden and kept.
- The project registers come first.

**Registers covered:** tasks, milestones, risks, issues and challenges, KPIs, change requests and documents. Each is covered in its project tab and in its cross-project register.

**Who sees the actions.** The `useRowActions` helper adds an Actions column only for a holder of the register's manage permission:

| Register | Permission |
| --- | --- |
| Tasks | `TASK_MANAGE` |
| Milestones | `SCHEDULE_EDIT` |
| Risks | `RISK_MANAGE` |
| Issues and challenges | `CONCERN_MANAGE` |
| Change requests | `CHANGE_REQUEST_RAISE` |
| Documents | `DOCUMENT_MANAGE` |
| KPIs | `KPI_MANAGE` |

Rows of a CLOSED project have no buttons.

**Edit** opens the record's existing form:

- Tasks and milestones: in the row's dialog. The cross-project registers send `?edit=<id>` to the project tab.
- Risks, issues and documents: the record's page opens its edit form when given `?edit=1`.
- Change requests: the edit page, for a DRAFT or RETURNED request only.
- KPIs: no edit form exists, so a KPI row has Delete only.

**Delete**, one rule everywhere (`RecordDeletion.Stamp`, `IDeletable`):

- The API sets `deleted_at` and `deleted_by` and audits `<Module>.<Type>Deleted`.
- Three named EF query filters take the record out of every read:
  - `Deleted` on the record;
  - `DeletedProject` on everything anchored on a deleted project;
  - `DeletedParent:<column>` on every entity with a foreign key to a deletable record, read from the model.
- A task's subtasks are stamped with it, and its dependencies leave with it.
- The children hidden with their parent:
  - risk: assessments, acceptances and actions;
  - issue: escalations and impacts;
  - KPI: targets and measurements;
  - change request: materiality evaluations and authorizations;
  - document: versions and links, so a deleted document stops counting as evidence;
  - milestone: achievement claims and baseline milestones.
- An issue raised from a risk is deletable itself, so it stays when the risk is deleted.
- Each module's history guard (a database trigger) lets exactly that stamp through, whatever the record's state. A deleted row changes no more.

**Refused (409 `RECORD_APPROVAL_PENDING`)** while an approval run would be left with nothing to decide:

- a change request UNDER_REVIEW;
- an issue PENDING_VALIDATION;
- a milestone with a SUBMITTED achievement claim, asked of the Milestone module through `IMilestoneClaimsInProgress`;
- a KPI with a target SUBMITTED or UNDER_REVIEW.

The approval's outcome handlers would otherwise fail on a missing record, and the run would block project closure.

**Known effects:**

- A deleted milestone also leaves the baselines that froze it.
- A notification already sent about a deleted record links to a page that answers "not found".
- Change requests now keep a deleted draft as a hidden row, where they used to remove it.

**Not done:** the admin and other lists, such as roles, import batches, external requests and the follow-up list.

## 8. Sign-in without a password (13 Nov 2026, decided by the client)

The client first asked for Face ID / Touch ID through Microsoft (`login_hint` and `prompt=login` on the Microsoft sign-in), then replaced it: the platform keeps its own passkeys, and offers a code by SMS as the second way. The Microsoft version is reverted (`778f3ab`). The client chose:

- **Sign-in with biometrics starts from the National ID** (follow-up the same day). The panel keeps the mockup's National ID / Iqama number field and "Sign in with biometrics" button. The person then chooses Face ID / Touch ID (their passkey) or a code by SMS to the mobile number verified on their account; no mobile number is typed. The choice looks the same for any number, nobody's included, so the page says nothing about who has an account. Only the passkey of the National ID's owner verifies (the sealed ceremony carries the expected user). It needs `PERSONAL_DATA_KEY`, as National IDs do.
- **SMS is a first factor.** A role that needs MFA (R01) still gives its authenticator code after it.
- **A passkey counts as both factors.** It is the device in hand plus its biometric or PIN, and it is phishing-resistant, so no authenticator code follows.
- **Until AHDA selects an SMS provider (OQ-012)**, a stand-in sends each SMS as an e-mail to a test mailbox (`Notifications:Sms:StandIn:Enabled`, mailpit locally). Real phones receive nothing.

**Passkeys.** WebAuthn, verified by Fido2 4.3.3 (passwordless-lib, MIT): discoverable, user-verified passkeys on the device's own authenticator (Face ID, Touch ID, Windows Hello, Android fingerprint), attestation "none". `identity_access.user_passkey` keeps the credential id, the COSE public key, the counter and the device name; the private key and the biometric never leave the device. The relying party id and origins are `Identity:Passkeys` (`RelyingPartyId`, `Origins`), or by default the host and origin of `APP_BASE_URL`. A passkey belongs to that domain, so a passkey made on the tunnel's domain or on localhost does not work elsewhere, and moving the platform to AHDA's domain means registering again. A ceremony's options travel sealed with the client (AES-GCM under a key of its own, 5 minutes), so nothing is stored between its two calls.

| Endpoint | What |
| --- | --- |
| `POST /sessions/passkey-challenge` `{ nationalId }`, `POST /sessions/passkey` | Anonymous passkey sign-in, under the `SignIn` rate limit |
| `POST /sessions/sms-code-challenge` `{ nationalId }`, `POST /sessions/sms` | Anonymous SMS sign-in, under the `SignIn` rate limit |
| `GET /users/current/passkeys`, `GET …/registration-options`, `POST /users/current/passkeys`, `POST …/{id}/delete` | The person's own passkeys. Adding one needs a sign-in within 15 minutes (`IDENTITY_ACCESS_RECENT_SIGN_IN_REQUIRED`) |
| `GET /users/current/mobile-number` | The person's own number, whether it is verified, and whether a code can be sent |

**SMS codes.** Six digits drawn by the platform, kept only as a hash in `identity_access.sms_code`, five tries, five minutes, used once. A person gets one code per purpose a minute (`Identity:SmsCodes`). The same codes now back ADR-004's mobile verification, which until now had no sender (`UnconfiguredMobileNumberVerifier` is gone). The text is in the person's language.

**Screens.** The sign-in page's Nafath panel: National ID, Sign in with biometrics, then the choice. The device button is named for the device (Face ID on iPhone and iPad, Touch ID on a Mac, Windows Hello, fingerprint on Android) and stays disabled where the browser has no device unlock. The account menu gains **Sign-in methods** (`/account/sign-in-methods`): add a passkey for this device, list and remove passkeys, and verify the mobile number. Change password now treats a passkey or SMS session like a password one.

**Security.** AUTH-T35 to AUTH-T37 and SR-87 to SR-89 in `docs/security`: SR-87 (SMS as a sign-in factor) needs AHDA's acceptance, and SR-89 (no guard against the stand-in in UAT or PROD) is open.

**Verified.** 17 integration tests drive real WebAuthn messages from a software authenticator and the SMS flows. 10 SPA tests cover the National ID step, both ways and the account page. In Chromium with a virtual platform authenticator, on localhost:5173: a passkey added from Sign-in methods; National ID then Touch ID signs in; the same passkey with another person's National ID is refused; National ID then the SMS code from mailpit signs in. Not tried on a physical iPhone or Mac.

## 9. Not done

- Item 3 has not been received.
- The showcase is not deployed: it needs migrate and seed.
- A Microsoft or directory account deleted here stays in the directory; it just cannot sign in to the platform. The directory sync reads active users only, so it leaves a deleted one alone.

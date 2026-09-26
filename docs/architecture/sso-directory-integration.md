# SSO and Directory Integration

| Field | Value |
| --- | --- |
| Task | TASK-028 — Integrate AD/LDAP and SSO Authentication (P5 - Identity & Access Management, FG-03) |
| Depends on | TASK-025 — Implement Core Platform Schema (`core-platform-schema.md`): `identity_access.user`, `access_relationship`, `permission_profile_version` |
| Record date | 2026-09-26 |
| Status | **BUILT AND VERIFIED LOCALLY** against a real LDAP directory (`AHDA-ldap`, OpenLDAP) and an OpenID Connect provider hosted in the test process. Wired into CI. **Not yet run against AHDA's directory or identity provider**: no environment exists, and the product is confirmed with AHDA IT at environment setup (F-9) |
| Branch | `feat/task-028-sso-ad-ldap-integration` |
| Deliverables | SSO/AD integration module: `PMPlatform.Infrastructure/Identity` (LDAP directory, OIDC client, session tokens), `PMPlatform.Application/Features/IdentityAccess` (sign-in service, ports, contracts), `PMPlatform.Infrastructure/Persistence/IdentityAccess/UserAccessRepository.cs`. ADM-041 backend: `IdentityIntegrationController`. API: `SessionsController`, bearer authentication, deny-by-default authorization. Integration test suite: `PMPlatform.Tests.Integration/Identity` (35 tests). Test directory: `infra/docker/ldap`, Compose service `AHDA-ldap`, CI step. This record |
| Environment variables / secrets | `AD_LDAP_URL`, `AD_BIND_DN`, `AD_BIND_PASSWORD`, `SSO_OIDC_CLIENT_SECRET` (secret store, optional per environment, D-9); `SSO_OIDC_CLIENT_ID`, `SSO_OIDC_AUTHORITY`, `SSO_OIDC_CALLBACK_URL` (configuration store); `JWT_SIGNING_KEY` (secret store, required). Nothing is added to the sheet |
| Gate decision applied | **ADR-007 (SCOPED).** AHDA's existing directory with SSO; the directory is authoritative for department, manager and job title; platform roles are assigned in the platform and not inherited from directory groups. Resolves PTBC-036 |
| Participation amendments | **ADR-013**: the third user type — an external identity holding an internal-grade project role — signs in and receives R04 scoped to their project (D-2). **ADR-018**: an assignment binds to a permission profile version; the session's role is that version's base role (D-2) |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), Implementation Plan rows TASK-028 to TASK-032, as read 2026-09-26; Environment and Secrets snapshot `environment-and-secrets.csv` |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | Sign-in with directory credentials (LDAP bind) and with SSO (OIDC authorization code + PKCE); one platform session model for both; expiry and refresh | This task |
| **In** | Mapping an authenticated person to their platform user and platform roles; the third user type (ADR-013) | This task |
| **In** | Copying the three directory-authoritative attributes at sign-in (ADR-007) | This task |
| **In** | ADM-041 backend: the integration's configuration status and a connection test | This task |
| **In** | Generic failure responses (no user enumeration); no credential in logs | This task |
| **Out** | MFA and step-up (the session carries `amr` for it) | TASK-029 |
| **Out** | Permission checks per endpoint and data scope; ADM-041's R01 policy is a stop-gap (D-11) | TASK-030 |
| **Out** | User, role and assignment administration (ADM-002–013) | TASK-031, TASK-032 |
| **Out** | Directory sync of users who do not sign in, sync runs and mismatch alerts | TASK-075 (FG-05 `sync_run`) |
| **Out** | Rate limiting of the sign-in endpoints; CORS; security headers | TASK-078 |
| **Out** | Nafath identity verification at external onboarding | TASK-068 |
| **Out** | The sign-in screen and the SSO callback route in the SPA | Frontend (TASK-032 or a sign-in task) |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Two ways in, one way through.** A directory bind and an SSO sign-in both end in a *directory subject*. The platform finds the user whose `directory_subject_id` equals it and issues the session. Nothing else from the directory or the identity provider decides access: no group is read, and no group or role scope is requested (`scope=openid`). | ADR-007: roles are assigned in the platform. This is also the answer to the workbook description's "group/claim mapping to platform roles R01–R08" (PTBC-036): the gate decision replaced it. The only mapping is the subject, plus the three attributes of D-6. |
| D-2 | **Session roles are the base roles of the profile versions the user's active assignments bind to.** An assignment counts when `status = ACTIVE`, `starts_at <= now` and `ends_at` is null or later. For an EXTERNAL user only roles with `is_external_eligible` (R04, R08) count; any other is ignored. Each role is returned with its profile version and scope anchors (department, entity, project). The entity Project Manager is an EXTERNAL user with an R04 assignment anchored to a project. | ADR-018 (bind to a version), ADR-013 (the grant, not the person, makes an entity PM; an external user can never hold R01). Fail closed: a bad assignment row from elsewhere does not become an external admin session. |
| D-3 | **The platform issues its own tokens**, carried as `Authorization: Bearer` (api-conventions R-46), signed HS256 with `JWT_SIGNING_KEY`. Access token 15 min; refresh token 30 min (idle timeout); session 8 h absolute, never extended by a refresh. **These values are provisional** (control matrix G-2). Tokens are **stateless**: a refresh re-reads the user, their entity and their assignments, so disabling a user or ending an assignment takes effect at the next refresh, at most 15 minutes later. | The ERD holds no session table ("session state is held by the identity provider", erd.md). Revocation before expiry needs server state; F-3. |
| D-4 | **The API starts the SSO redirect**, as a confidential client with PKCE (S256), nonce and state. The state, nonce and verifier travel in a *transaction* sealed with AES-256-GCM, under a key derived from `JWT_SIGNING_KEY` with HKDF under its own label. The SPA holds it (session storage) and posts it back with the code. No server-side store, so any replica completes any sign-in. The verifier never appears in a URL. | Closes TASK-013 F-3: the three `VITE_SSO_OIDC_*` rows are removed from `src/frontend/.env.example`; the client secret stays server-side. |
| D-5 | **One failure, one answer.** Unknown user, wrong password, directory person with no platform user, disabled user, external user with a suspended entity, invalid or expired token: all `401 AUTHENTICATION_REQUIRED`, byte-identical apart from `correlationId` and `timestamp`. A rejected password sign-in answers no sooner than **1 second** after it began. A directory or identity provider that cannot be reached, or a sign-in method the environment has not configured, is `503 UNAVAILABLE`: the platform's failure, not the person's. | TASK-028 acceptance criterion 2 and api-conventions §4.4. The paths differ in length (search, bind, database), so without the floor the response time would reveal which case applied. |
| D-6 | **The directory's three attributes are copied at every sign-in**, for INTERNAL users: job title as-is; department by matching the directory value to `Department.directory_reference` (active departments only); manager by reading the manager DN's subject and matching it to a user. An absent attribute clears the field. A value the platform cannot resolve leaves the stored one and is logged, never guessed. Changes are attributed to the new SERVICE principal `svc.directory-sync` (`…00fe`, seeded by `db/seed`). After SSO the directory is read by subject; if it is unreachable the sign-in still succeeds and the stored values stand. | ADR-007. TASK-075's rule: "mismatches never auto-repair authoritative data". Attribution: ERD D-2, a directory change is not the signing-in person's change. |
| D-7 | **Encrypted transport only.** The directory is used over `ldaps://` unless `Identity:Directory:AllowUnencryptedConnection` is set, which only the local test directory does; the connection test reports `TRANSPORT_SECURITY_REQUIRED`. OIDC discovery and keys are HTTPS-only unless `Identity:Sso:RequireHttpsMetadata` is cleared (tests only). An empty password never reaches the directory: it is an anonymous bind, which most directories accept. | A plain LDAP bind sends the password in clear. RFC 4513 §5.1.2 (unauthenticated bind). |
| D-8 | **`AD_LDAP_URL` is an RFC 4516 LDAP URL whose DN is the user search base**, e.g. `ldaps://dc01.ahda.example:636/OU=Staff,DC=ahda,DC=example`. | The search base is needed and the sheet has no variable for it; the URL form carries it without adding one. |
| D-9 | **`JWT_SIGNING_KEY` is a required secret; the `AD_*` and `SSO_OIDC_CLIENT_SECRET` secrets are optional.** `ApplicationSecrets.OptionalKeys` loads them when the store holds a value and leaves them unset otherwise; the method then reports "not configured". `secret-management-check.py` K-1 now checks the optional list against the sheet scope. | The sheet scopes `AD_*` to SIT/UAT/PROD, and the SSO client exists only once AHDA IT registers it. Making them required would stop DEV from starting. |
| D-10 | **ADM-041 shows and tests; it does not edit.** `GET /api/v1/identity-integration` returns which settings are set (secrets reduced to a flag), the attribute and claim mapping, and whether transport is encrypted. `POST /api/v1/identity-integration/test` binds with the service account and reads the search base, and resolves the OIDC discovery document and checks its issuer against `SSO_OIDC_AUTHORITY`. No migration: the values live in the secret store and the configuration store (CTL-18), as the sheet routes them. | The sheet's verification methods for `AD_*` ("successful bind/test connection from ADM-041") and `SSO_OIDC_AUTHORITY` ("token issuer matches configured value"). Editing secrets from a screen would move them out of the approved store. |
| D-11 | **Deny by default.** A fallback authorization policy requires an authenticated user on every endpoint; the sign-in endpoints, `/health` and the development OpenAPI document opt out explicitly. ADM-041 requires role R01 (`AuthorizationPolicies.SystemAdministrator`), a stop-gap TASK-030 replaces with a permission check. | A new endpoint is protected unless someone decides otherwise. Checklist item CTL-08. |
| D-12 | **The test directory is a real LDAP server in the AHDA stack; the test identity provider runs in the test process.** `AHDA-ldap` (OpenLDAP on Alpine) holds one synthetic person per role, linked to the local users by entryUUID. The OIDC provider (`TestIdentityProvider`) serves discovery, keys, an authorization endpoint and a token endpoint over HTTP on a loopback port, and checks client secret, redirect URI, single use of the code and the PKCE verifier. | TASK-028's validation cell asks for "a test AD/SSO instance". Directory behaviour (binds, filters, referrals, DNs) is what an LDAP fake gets wrong, so it is a real server. An OIDC provider is plain HTTP, and the API validates its tokens with the same library code either way. |

## 3. API surface

| Operation | Endpoint | Auth | Success | Failure |
| --- | --- | --- | --- | --- |
| `IdentityAccess_CreateSession` | `POST /api/v1/sessions` `{ username, password }` | anonymous | 201, `Location: /api/v1/sessions/current`, `SessionDetail` | 400 `VALIDATION_FAILED`; 401 `AUTHENTICATION_REQUIRED`; 503 `UNAVAILABLE` |
| `IdentityAccess_GetSsoAuthorization` | `GET /api/v1/sessions/sso-authorization` | anonymous | 200 `{ authorizationUrl, transaction }` | 503 |
| `IdentityAccess_CreateSsoSession` | `POST /api/v1/sessions/sso` `{ code, state, transaction }` | anonymous | 201 `SessionDetail` | 400; 401; 503 |
| `IdentityAccess_GetCurrentSession` | `GET /api/v1/sessions/current` | bearer | 200 `{ userId, sessionId, userType, authenticationMethod, roles, expiresAt }` | 401 |
| `IdentityAccess_RefreshSession` | `POST /api/v1/sessions/current/refresh` `{ refreshToken }` | anonymous (the refresh token is the credential) | 200 `SessionDetail` | 400; 401 |
| `IdentityAccess_GetIdentityIntegration` | `GET /api/v1/identity-integration` | bearer, R01 | 200 status | 401; 403 `PERMISSION_DENIED` |
| `IdentityAccess_TestIdentityIntegration` | `POST /api/v1/identity-integration/test` | bearer, R01 | 200 `{ directory, singleSignOn }` each `{ outcome, testedAt, failureCode }` | 401; 403 |

`SessionDetail` is `{ tokenType: "Bearer", accessToken, accessTokenExpiresAt, refreshToken, refreshTokenExpiresAt, sessionExpiresAt, user: { id, userType, username, displayName, preferredLanguage, authenticationMethod, roleAssignments: [{ roleCode, permissionProfileVersionId, departmentId, externalEntityId, projectId }] } }`. Every response carrying a token is `Cache-Control: no-store` (RFC 6749 §5.1). Every error is the R-23 envelope with `code`, `correlationId`, `idempotencyKey`, `timestamp`; `X-Correlation-Id` is echoed on every response (R-41). Sign-in and the connection test take no `Idempotency-Key`: they change no business state (R-35).

Access token claims: `iss=pmplatform`, `aud=pmplatform-api`, `sub` (user id), `sid`, `user_type`, `role` (codes), `amr` (`pwd` or `sso`), `iat`, `nbf`, `exp`. The refresh token has `aud=pmplatform-session-refresh`, `sub`, `sid`, `amr`, `session_exp`: the API rejects it as an access token and the refresh path rejects an access token. The claim names are the module's contract, `Features/IdentityAccess/Contracts/SessionTokenClaims.cs`.

## 4. Configuration

| Key | Store | Default | Meaning |
| --- | --- | --- | --- |
| `AD_LDAP_URL` | secret store | — | LDAP URL; path = search base (D-8) |
| `AD_BIND_DN`, `AD_BIND_PASSWORD` | secret store | — | Service account; read per use, so a rotation applies without a restart |
| `SSO_OIDC_AUTHORITY`, `SSO_OIDC_CLIENT_ID`, `SSO_OIDC_CALLBACK_URL` | configuration store | — | OIDC client; the issuer must equal the authority |
| `SSO_OIDC_CLIENT_SECRET` | secret store | — | `client_secret_basic` at the token endpoint |
| `JWT_SIGNING_KEY` | secret store | — | ≥ 32 bytes; read per token, so a rotated key invalidates old tokens at once |
| `Identity:Directory:UsernameAttribute` | configuration | `sAMAccountName` | Attribute a person signs in with |
| `Identity:Directory:SubjectAttribute` | configuration | `objectGUID` | Immutable id stored as `directory_subject_id`; `objectGUID` is stored as its GUID string |
| `Identity:Directory:JobTitleAttribute` / `DepartmentAttribute` / `ManagerAttribute` | configuration | `title` / `department` / `manager` | ADR-007's three attributes |
| `Identity:Directory:AllowUnencryptedConnection` | configuration | `false` | Local test directory only (D-7) |
| `Identity:Directory:Timeout` | configuration | 10 s | Per connection and operation |
| `Identity:Sso:SubjectClaim` | configuration | `sub` | ID token claim holding the directory subject; must equal the directory's subject attribute value (F-2) |
| `Identity:Sso:Scopes` | configuration | `["openid"]` | No group or role scope (D-1) |
| `Identity:Sso:RequireHttpsMetadata`, `TransactionLifetime`, `Timeout` | configuration | `true`, 10 min, 10 s | |
| `Identity:Session:AccessTokenLifetime`, `RefreshTokenLifetime`, `SessionLifetime` | configuration | 15 min, 30 min, 8 h | **Provisional** (G-2, F-1) |

The defaults are Active Directory's attribute names. The local stack and the tests set OpenLDAP's (`uid`, `entryUUID`, `departmentNumber`); the exact directory product is confirmed with AHDA IT (ADR-007 Impact).

## 5. Layering

`PMPlatform.Api` → `Features/IdentityAccess/Contracts` (`IAuthenticationService`, `IIdentityIntegrationService`) → `AuthenticationService` in `Features/IdentityAccess/Authentication`, which depends only on four ports declared beside it: `IDirectoryService`, `ISingleSignOnProvider`, `ISessionTokenService`, `IUserAccessRepository`. Infrastructure implements them: `LdapDirectoryService` (Novell.Directory.Ldap.NETStandard 4.0.0, pure managed, so the runtime image needs no native `libldap`), `OpenIdConnectProvider` (Microsoft.IdentityModel 8.23.0), `JwtSessionTokenService`, and `UserAccessRepository` under `Persistence/IdentityAccess`, since A-2 confines `DbContext` to Persistence. The API's composition root alone references Infrastructure, for `SessionTokenValidation.AccessToken` (A-4). The architecture tests A-1 to A-6 pass unchanged.

## 6. The test directory

`infra/docker/ldap`: `Dockerfile` (Alpine 3.22, `openldap`, `openldap-back-mdb`), `slapd.conf` (MDB, no rootdn, only the service account may search, passwords usable only to bind as their owner), `directory.ldif` (loaded with `slapadd` at build time, so every container starts identical). People: `local.r01` … `local.r07` under `ou=People`, `local.r08` under `ou=External`, `local.unregistered` (in the directory, not in the platform). Each entryUUID is `00000000-0012-4000-8000-00000000000n`, which `infra/docker/postgres/seed/seed-local-users.sql` stores as `directory_subject_id`. Passwords are labelled local defaults. `docker compose -f infra/docker/docker-compose.yml up -d --wait ldap` starts it alone (the tests and CI); a full `up` also points the API at it.

## 7. Verification

Run 2026-09-26 on macOS, Docker Desktop, PostgreSQL 17 (`AHDA-postgres`) and `AHDA-ldap`.

| # | Command | Result |
| --- | --- | --- |
| 1 | `dotnet build src/backend -warnaserror` | Build succeeded, 0 warnings |
| 2 | `dotnet test src/backend/PMPlatform.Tests.Unit` | 41 passed (39 before; +2 optional-key tests); architecture tests A-1 to A-6 unchanged and green |
| 3 | `DB_CONNECTION_STRING=… dotnet test src/backend/PMPlatform.Tests.Integration` | 145 passed: 35 new under `Identity`, 110 existing under `Persistence` (the seed tests include the new service principal) |
| 4 | `docker compose -f infra/docker/docker-compose.yml up -d --build --wait`, then `infra/docker/smoke-test.sh` | PASS: healthy in 14 s; one active local user per R01–R08 |
| 5 | `curl -X POST localhost:5080/api/v1/sessions` as `local.r01`, then ADM-041 with the token | 201 with R01; `local.r08` gets `EXTERNAL` with R08 on its entity; wrong password 401 `application/problem+json`; ADM-041 test: directory `SUCCEEDED`, SSO `NOT_CONFIGURED` (no identity provider locally) |
| 6 | All nine `docs/architecture/*-check.py` | PASS; `env-template-check`: 2 public variables in `.env.example` |
| 7 | `actionlint .github/workflows/ci-quality-gates.yml` | clean |
| 8 | `gitleaks dir . --config .gitleaks.toml` | no leaks found |
| 9 | `dotnet list src/backend package --vulnerable --include-transitive` | no vulnerable package in any project |

The 35 identity tests, by acceptance criterion:

| Criterion | Tests |
| --- | --- |
| Session with correctly mapped roles | `ADirectorySignInIssuesASessionWithThePlatformRolesOfTheUsersAssignments`, `AnSsoSignInIssuesASessionWithThePlatformRolesOfTheUsersAssignments`, `AnEntityProjectManagerSignsInAsAnExternalUserHoldingR04OnTheirProject`, `OnlyAssignmentsInForceBecomeSessionRoles`, `ASignInCopiesTheDirectoryAuthoritativeAttributes`, `AnSsoSignInCopiesTheDirectoryAuthoritativeAttributes`, `TheAuthorizationRequestUsesPkceAndAsksForNoRoleOrGroupClaims` |
| Generic failure, no enumeration | `EveryRejectedSignInIsTheSameGeneric401` (7 cases incl. two filter-injection attempts, identical body, ≥ 1 s each), `AnEmptyPasswordIsAShapeErrorAndNeverReachesTheDirectory`, `APersonThePlatformDoesNotAdmitIsRejected` (3), `ATamperedTransactionIsRejected`, `AStateThatIsNotTheTransactionsIsRejected`, `ACodeCanBeRedeemedOnlyOnce`, `AnIdTokenForAnotherSignInIsRejected`, `AnExpiredTransactionIsRejected`, `AProtectedEndpointWithoutATokenIsTheSameGeneric401` |
| Expiry and refresh | `AnAccessTokenIsRefusedOnceItExpires`, `ARefreshIssuesAWorkingTokenPairWithoutExtendingTheSession`, `ASessionNotRefreshedWithinTheIdleTimeoutEnds`, `NoRefreshOutlivesTheAbsoluteSessionLifetime`, `ARefreshIsRefusedOnceTheUserIsDisabled`, `AnAccessTokenAndARefreshTokenCannotStandInForEachOther`, `ATokenIsRejectedOnceTheSigningKeyIsRotated` |
| Directory unavailable | `AnUnreachableDirectoryIsA503`, `AnUnreachableDirectoryFailsTheConnectionTest`, `AnUnreachableIdentityProviderIsA503`, `AnUnconfiguredSignInMethodIsA503`, `AnUnencryptedDirectoryIsRefusedUnlessAllowed` |
| No credential in logs | `NoCredentialOrTokenAppearsInTheLogs`: user and service passwords, a mistyped password, the client secret, the signing key and four issued tokens are searched for in every captured log line, scope and exception |
| ADM-041 | `TheStatusShowsWhatIsConfiguredAndNoSecretValue`, `TheConnectionTestReachesTheDirectoryAndTheIdentityProvider`, `TheConnectionTestReportsARefusedServiceAccount`, `OnlyASystemAdministratorMayUseIt` |

### 7.1 Mutation tests: each protection fails its test when removed

| # | Mutation | Test | Result |
| --- | --- | --- | --- |
| M-1 | External users keep non-eligible roles | `AnEntityProjectManagerSignsIn…` | FAILED: the session held `R01, R04, R08` |
| M-2 | No rejection floor | `EveryRejectedSignInIsTheSameGeneric401` | FAILED |
| M-3 | Username not escaped in the LDAP filter | `EveryRejectedSignInIsTheSameGeneric401` | FAILED |
| M-4 | ID token nonce not compared | `AnIdTokenForAnotherSignInIsRejected` | FAILED |
| M-5 | Refresh does not re-check the user's status | `ARefreshIsRefusedOnceTheUserIsDisabled` | FAILED |
| M-6 | Refresh not capped at the session's absolute expiry | `NoRefreshOutlivesTheAbsoluteSessionLifetime` | FAILED |

Every mutation was reverted and the suite re-run green.

## 8. Acceptance criteria, validation and gate decision

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | A user authenticated via AD/SSO receives a session with correctly mapped platform role(s) | **MET** for both methods. The roles come from the platform's assignments, not the directory's groups, as ADR-007 requires | §7 row "Session with correctly mapped roles"; D-1, D-2 |
| 2 | Authentication failure returns a generic error (no user enumeration) | **MET.** One 401 body for every cause, with a response-time floor | D-5; M-2, M-3 |
| 3 | Session/token expiry and refresh behaviour is implemented and covered by an integration test against a test directory | **MET** against `AHDA-ldap`. The lifetimes are provisional (F-1) | §7 row "Expiry and refresh"; M-5, M-6 |
| — | Validation: integration tests against a test AD/SSO instance covering successful login, invalid credentials and directory-unavailable | **MET** against a test LDAP directory and a test OIDC provider. **Not run against AHDA's** (F-9) | §7 |
| — | Validation: no credential in application logs | **MET** | `NoCredentialOrTokenAppearsInTheLogs` |
| — | Gate ADR-007: directory authoritative for department, manager, job title; roles not inherited from groups | **MET** | D-1, D-6 |
| — | Amendment ADR-013: the third user type | **MET** | `AnEntityProjectManagerSignsIn…`; M-1 |
| — | Amendment ADR-018: assignment binds to a profile version | **MET.** Each session role carries its `permissionProfileVersionId` | D-2 |
| — | Sheet verification, `JWT_SIGNING_KEY`: issued and validated round-trip; rejected once rotated | **MET** | `ATokenIsRejectedOnceTheSigningKeyIsRotated` |
| — | Sheet verification, `AD_*`: successful bind/test connection from ADM-041 | **MET locally** | `TheConnectionTestReaches…`, `…RefusedServiceAccount` |
| — | Sheet verification, `SSO_OIDC_*`: discovery resolves and the token issuer matches; login completes end to end | **MET against the test provider** | `TheConnectionTestReaches…`, `AnSsoSignInIssuesASession…` |

## 9. Findings

| # | Finding | Owner | Consequence if left |
| --- | --- | --- | --- |
| F-1 | **Session lifetimes are provisional**: 15 min access, 30 min idle, 8 h absolute. They are AHDA's values (control matrix G-2; ADR-007 "session-invalidation behaviour confirmed with AHDA IT at environment setup") and are configuration, so confirming them changes no code | AHDA IT (Identity) + Security Lead; register as a UGV (TASK-004 §6) | Sessions live longer or shorter than AHDA's policy |
| F-2 | **The SSO subject and the directory subject must be the same value**, and which claim carries it depends on the product: AD `objectGUID` matches an AD FS claim only if AD FS is configured to issue it; Entra ID's `oid` is not the on-premises `objectGUID`. `Identity:Sso:SubjectClaim` and `Identity:Directory:SubjectAttribute` are configuration, but the pair must be chosen with AHDA IT | AHDA IT (Identity), at environment setup | SSO users match no platform user and every SSO sign-in is a 401 |
| F-3 | **No revocation before expiry.** Tokens are stateless (D-3), so sign-out is the client discarding its tokens, and a stolen refresh token works until its idle timeout (≤ 30 min, never beyond 8 h). Disabling a user stops their refresh at once. If AHDA requires immediate revocation, the ERD needs a session or revocation table, against its current "session state is held by the identity provider" | Engagement Architect, with F-1 | A compromised session cannot be ended early |
| F-4 | **The sign-in endpoints are not rate-limited.** R-35 treats them as non-sensitive *because* TASK-078 rate-limits them; the 1 s floor slows a single client but not a parallel one | TASK-078 | Password guessing is bounded only by the directory's lockout policy |
| F-5 | **The external users' sign-in method is AHDA IT's to confirm** (ADR-007 note, 19 Sep 2026). This module signs in any person AHDA's directory or identity provider authenticates, with the user type and roles the platform holds, so an external identity in either works unchanged. If AHDA chooses a separate identity provider for external users, it is a second OIDC client | AHDA IT (Identity) | External Project Managers have no working sign-in in an environment |
| F-6 | **Directory attributes refresh only at sign-in.** A person who never signs in keeps stale values, and a department move shows only at their next sign-in. A scheduled directory sync is FG-05's `sync_run` (`DIRECTORY_SYNC`) | TASK-075 | Reports by department reflect the last sign-in, not the directory |
| F-7 | **ADM-041 authorizes by role (R01), not by permission** (D-11) | TASK-030 | None beyond the stop-gap; a different ADM-041 audience needs TASK-030 |
| F-8 | **ADR-018 is not in the repository's Architecture Decision Register** (`adrs/ADR-register.md` covers ADR-001 to ADR-013). This task applied it from the workbook's Participation Amendment cell and from `access_relationship.permission_profile_version_id` | PMO: refresh the register from the workbook | A reviewer cannot read ADR-018 in the repository |
| F-9 | **Not run against AHDA's directory or identity provider.** No environment exists; provisioning waits on the ADR-001 confirmation request. The first environment's ADM-041 connection test and one sign-in per method are this task's live check | DevOps/Platform Lead + AHDA IT, at first environment | — |
| F-10 | **Security Lead review (CTL-43) could not be requested**: the repository belongs to a personal account, so the CODEOWNERS teams do not exist. This change is authentication and SSO throughout | Maintainer: request the Security Lead's review by name | The checklist item stays unticked |
| F-11 | **The correlation-id middleware and the problem-details helper are minimal.** api-conventions A-9 puts them behind an `ApiControllerBase` and `Application/Common/Errors`, which TASK-011 did not build. They live in `PMPlatform.Api/Correlation` and `PMPlatform.Api/Errors` until that base exists | Whoever builds `ApiControllerBase` | Two places to change when the base lands |
| F-12 | **The SPA has no sign-in screen or `/auth/callback` route yet.** The API contract is fixed (§3); the SPA keeps the transaction in session storage between the redirect out and the callback | Frontend task for sign-in | Local sign-in is through the API only |

## 10. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-26 | Initial record. Directory (LDAP bind) and SSO (OIDC code + PKCE) sign-in to one stateless session model, with roles from active profile-version assignments; directory-authoritative attributes copied at sign-in; ADM-041 status and connection test; deny-by-default bearer authentication; optional secret keys; test directory `AHDA-ldap` in Compose and CI; 35 integration tests, six mutations. TASK-013 F-3 and TASK-014 F-3/F-4 closed. Twelve findings | Identity (TASK-028) |

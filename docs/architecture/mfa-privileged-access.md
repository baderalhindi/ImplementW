# MFA and Privileged Access Controls

| Field | Value |
| --- | --- |
| Task | TASK-029 — Implement MFA & Privileged Access Controls (P5 - Identity & Access Management, FG-03) |
| Depends on | TASK-028 — Integrate AD/LDAP and SSO Authentication (`sso-directory-integration.md`): the sign-in paths, the session tokens and their `amr` claim |
| Record date | 2026-09-26 |
| Status | **BUILT AND VERIFIED LOCALLY** against an MFA provider hosted in the test process, over the directory `AHDA-ldap` and the test identity provider. **Not run against a real MFA provider**: none is selected (PTBC-027), and no environment exists (F-11) |
| Branch | `sec/task-029-mfa-privileged-access` |
| Deliverables | MFA enrolment/verification flow: `PMPlatform.Infrastructure/Identity/HttpMultiFactorProvider.cs` (provider adapter), `AuthenticationService` (the gate every sign-in path passes), four endpoints on `SessionsController`. Privileged-session re-auth middleware: `PMPlatform.Api/Authorization/StepUpAuthentication.cs`, with `MultiFactorTokenValidation.cs` in the bearer handler. Test MFA provider and 26 integration tests under `PMPlatform.Tests.Integration/Identity`. This record |
| Environment variables / secrets | `MFA_PROVIDER_API_KEY` (secret store, optional per environment, D-8); `MFA_PROVIDER_ENDPOINT` (configuration store). Both already in the sheet; nothing added |
| Gate decision applied | **ADR-010 (SCOPED).** Step-up applies to privileged and sensitive actions; the trigger list follows the classification taxonomy still outstanding with AHDA Cybersecurity (UGV-01). The mechanism is built and the triggers are configuration (D-5) |
| Participation amendments | **ADR-013**: external users sign in through the same paths and the same MFA gate; a role that requires MFA requires it of an external user too (D-2). **ADR-018**: step-up rules are not part of a permission profile, so authoring a profile cannot switch step-up off (D-5) |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), Implementation Plan row TASK-029, as read 2026-09-26; Environment and Secrets snapshot `environment-and-secrets.csv`; control CTL-07 (`cybersecurity-control-matrix.md`) |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | A second factor before any session is issued to a person who requires MFA: R01 at least, other roles by configuration (SCR-002) | This task |
| **In** | Enrolment of the factor and its verification, through the MFA provider; the platform records only that a user is enrolled (`user.mfa_enrolled_at`) | This task |
| **In** | Step-up: a configured operation requires a second factor no older than a configured threshold | This task |
| **In** | Accepting a second factor that AHDA's identity provider already applied, when configured (the sheet's gate note for `MFA_PROVIDER_*`) | This task |
| **Out** | Which operations step up: follows the classification taxonomy (UGV-01, ADR-010). One trigger ships (D-5) | AHDA Cybersecurity; TASK-030/031 add operations |
| **Out** | Role assignment, the criterion's example of a privileged action: its endpoint is TASK-031's, which adds its operation id to the trigger list (F-3) | TASK-031 |
| **Out** | Administrator-issued enrolment or factor reset (F-2) | TASK-031 |
| **Out** | Rate limiting of the MFA endpoints (F-4) | TASK-078 |
| **Out** | The SPA's MFA and step-up screens | Frontend sign-in task |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **One gate, before the session.** Directory and SSO sign-in both end in `AuthenticationService.StartSessionAsync`. A person who requires MFA gets **200 with an MFA token** instead of 201 with a session. The session is issued only by `POST /sessions/mfa`, after the MFA provider accepts the code. The MFA token has its own audience (`pmplatform-session-mfa`), lives 5 minutes, and is refused as an access token, as a refresh token and by step-up. | CTL-07: "MFA enforced before a session is granted". A token that only admits its holder to the second factor holds no role, so it cannot be used for anything else. |
| D-2 | **Who requires MFA:** anyone holding a role on `Identity:Mfa:RequiredRoles` (default `R01`), and anyone already enrolled, whatever their roles. R01 cannot be configured away: the configuration binder keeps the default and adds to it, and start-up refuses a list without R01. External users are treated like internal users: a role on the list requires MFA of whoever holds it (ADR-013). | CTL-07's minimum viable scope is R01. SCR-002 for standard users means listing their roles. "Once enrolled, always challenged" means an enrolment can never be skipped later. |
| D-3 | **The session remembers how and when.** Access and refresh tokens carry `amr` as an RFC 8176 array (`["pwd"]`, `["sso"]`, `["pwd","mfa"]`, …) and `auth_time`, the time of the last interactive authentication. A refresh carries both forward unchanged; a step-up renews `auth_time` and keeps the session id and absolute expiry. | Step-up freshness has to survive refreshes without being renewed by them. A refresh that renewed `auth_time` would make a stale session look fresh (M-6). |
| D-4 | **A token that skipped MFA is no token.** The bearer handler's `OnTokenValidated` fails an access token that does not state `amr` and `auth_time`, or that holds a role requiring MFA without `mfa` in `amr`: 401 on every protected endpoint, an anonymous caller elsewhere. A refresh is refused for the same reason, including when a role requiring MFA was assigned after a session began without it. | Sign-in cannot mint such a token, but a token minted before the policy grew, or by anything else holding the key, would otherwise pass. This is the "no bypass via any documented API path" criterion enforced at the one place every request passes. |
| D-5 | **Step-up is configuration.** `Identity:StepUp:Operations` lists endpoint names (OpenAPI operation ids); `Identity:StepUp:MaxAge` (default 5 min, provisional) is the threshold. The middleware runs after authorization, so an unauthorized caller still gets `PERMISSION_DENIED`. A listed operation called by a session whose last second factor is older than the threshold, or that never passed one, answers **403 `STEP_UP_REQUIRED`** with `WWW-Authenticate: Bearer error="insufficient_user_authentication", max_age=300`. Start-up fails if an operation names no endpoint. The shipped list is the ADM-041 connection test (`IdentityAccess_TestIdentityIntegration`). | ADR-010 fixes that step-up applies to privileged and sensitive actions; which ones follows UGV-01, so the list cannot be code. A misspelt trigger would switch step-up off silently, so it stops the API instead. The status is the error catalogue's (api-conventions §4.4); the header parameters are RFC 9470's, which uses 401 (F-8). Step-up rules are not held in a permission profile, so no profile can bypass them (ADR-018). |
| D-6 | **Step-up is a refresh that also passes a second factor.** `POST /sessions/current/step-up-challenge` and `POST /sessions/current/step-up` take the session's refresh token, as the refresh does, and answer with the session's next token pair. A user with no factor yet enrols one here, if enrolment is allowed. | The refresh token already identifies the session and carries its expiry and authentication context; no new credential and no server state are needed (TASK-028 D-3). |
| D-7 | **The platform decides enrolment or verification.** The purpose comes from `user.mfa_enrolled_at`, never from the client, so an enrolled person cannot be steered into enrolling a second, attacker-held factor. A passed enrolment sets `mfa_enrolled_at`, recorded as the user's own change. `Identity:Mfa:AllowEnrolmentAtSignIn` (default true) decides whether a person with no factor may enrol one after the first factor alone (F-2). | ERD: "MFA enrolment recorded; factors held by the MFA provider". No migration: the column exists since TASK-025. |
| D-8 | **Fail closed, and no development bypass.** No MFA provider configured means no session for anyone who requires MFA: 503 `UNAVAILABLE`, with an error log naming the user id. `MFA_PROVIDER_API_KEY` is an optional secret-store key (like the `AD_*` keys, TASK-028 D-9), so an environment without it starts. The endpoint must be HTTPS unless `Identity:Mfa:Provider:RequireHttps` is cleared, which only the tests do. Locally, R01 cannot sign in (F-5). | The sheet scopes both variables to SIT/UAT/PROD and notes they may be unnecessary if AHDA's identity provider applies MFA. A setting that turns MFA off would be the bypass the criterion forbids. |
| D-9 | **Same failure answers as TASK-028.** A wrong code, a spent or expired challenge, another person's challenge, an expired MFA token or a user disabled between the factors are all the generic 401 `AUTHENTICATION_REQUIRED`, no sooner than 1 s after the request began. A provider that refuses the platform's key or cannot be reached is 503. | TASK-028 D-5. The floor also slows code guessing from a single client (F-4). |
| D-10 | **The identity provider's own second factor counts only when configured.** `Identity:Mfa:IdentityProviderMethods` (default empty) lists ID token `amr` values that show AHDA's identity provider applied MFA. A match with an `auth_time` present gives an MFA session at once, and its freshness is measured from the provider's `auth_time`, not the sign-in. | If AHDA's identity provider does MFA, `MFA_PROVIDER_*` may be unnecessary (sheet gate note). Trusting it is AHDA's choice, so it is off unless set. |
| D-11 | **The MFA provider learns the user id and the code, nothing else.** No name, username or email is sent. The adapter speaks the platform's own four-call contract (§4); the selected product is fitted to it (F-1). | Data minimisation. The provider is not selected (PTBC-027), so any vendor's API chosen now would be a guess. |

## 3. API surface

New and changed operations. Every response carrying a token or a challenge is `Cache-Control: no-store`.

| Operation | Endpoint | Auth | Success | Failure |
| --- | --- | --- | --- | --- |
| `IdentityAccess_CreateSession` (changed) | `POST /api/v1/sessions` | anonymous | 201 `SessionDetail`, **or 200 `MfaPendingDetail`** | 400; 401; 503 (also: MFA required, no provider) |
| `IdentityAccess_CreateSsoSession` (changed) | `POST /api/v1/sessions/sso` | anonymous | as above | as above |
| `IdentityAccess_CreateMfaChallenge` | `POST /api/v1/sessions/mfa-challenge` `{ mfaToken }` | anonymous (the MFA token is the credential) | 200 `MfaChallengeDetail` | 400; 401; 503 |
| `IdentityAccess_CreateMfaSession` | `POST /api/v1/sessions/mfa` `{ mfaToken, challengeId, code }` | anonymous | 201 `SessionDetail` | 400; 401; 503 |
| `IdentityAccess_RefreshSession` (changed) | `POST /api/v1/sessions/current/refresh` | anonymous | 200 | 401 also when the user now requires MFA the session never passed |
| `IdentityAccess_CreateStepUpChallenge` | `POST /api/v1/sessions/current/step-up-challenge` `{ refreshToken }` | anonymous (the refresh token is the credential) | 200 `MfaChallengeDetail` | 400; 401; 503 |
| `IdentityAccess_StepUpSession` | `POST /api/v1/sessions/current/step-up` `{ refreshToken, challengeId, code }` | anonymous | 200 `SessionDetail` (same session, `auth_time` now) | 400; 401; 503 |
| Any operation on `Identity:StepUp:Operations` | — | bearer | — | 403 `STEP_UP_REQUIRED` with the RFC 9470 header |

`MfaPendingDetail` is `{ mfaToken, mfaTokenExpiresAt, enrolmentRequired }`. `MfaChallengeDetail` is `{ challengeId, expiresAt, provisioningUri }`; `provisioningUri` is set for an enrolment only. `SessionDetail.user` and `GET /sessions/current` gain `multiFactorAuthenticated` and `authenticatedAt`.

Token claims (the module's contract, `SessionTokenClaims.cs`): access and refresh tokens now carry `amr` as an array and `auth_time`; the MFA token has `aud=pmplatform-session-mfa`, `sub`, `amr` (the first factor), `iat`, `nbf`, `exp`, and nothing else.

## 4. The MFA provider contract

The adapter `HttpMultiFactorProvider` sends every call as `POST` with JSON and `Authorization: Bearer MFA_PROVIDER_API_KEY`, relative to `MFA_PROVIDER_ENDPOINT`:

| Call | Body | Success | Read as the person's failure (401) | Read as the provider's failure (503) |
| --- | --- | --- | --- | --- |
| `enrolments` | `{ subject }` | 2xx `{ challengeId, expiresAt, provisioningUri }` | — | any non-2xx; missing `challengeId` or `provisioningUri` |
| `challenges` | `{ subject }` | 2xx `{ challengeId, expiresAt }` | — | any non-2xx; missing `challengeId` |
| `enrolments/{challengeId}/verify` | `{ subject, code }` | 2xx `{ verified: true }` | 400, 404, 409, 410, 422, 429; 2xx without `verified: true` | 401, 403, 5xx, network |
| `challenges/{challengeId}/verify` | `{ subject, code }` | as above | as above | as above |

`subject` is the platform user id. `challengeId` is escaped as one path segment. The provider must bind a challenge to its subject, expire it, and limit attempts; the test provider allows one verification per challenge.

## 5. Configuration

| Key | Store | Default | Meaning |
| --- | --- | --- | --- |
| `MFA_PROVIDER_ENDPOINT` | configuration store | — | Base URL of the MFA provider; HTTPS required |
| `MFA_PROVIDER_API_KEY` | secret store (optional key) | — | Read per call, so a rotation applies without a restart |
| `Identity:Mfa:RequiredRoles` | configuration | `["R01"]` | Roles that require MFA; R01 always included (D-2) |
| `Identity:Mfa:AllowEnrolmentAtSignIn` | configuration | `true` | Whether a person with no factor may enrol after the first factor (D-7, F-2) |
| `Identity:Mfa:IdentityProviderMethods` | configuration | `[]` | ID token `amr` values accepted as the identity provider's own MFA (D-10) |
| `Identity:Mfa:Provider:RequireHttps`, `Timeout` | configuration | `true`, 10 s | Transport to the provider |
| `Identity:Session:MultiFactorTokenLifetime` | configuration | 5 min | Time between the first and the second factor. **Provisional** (F-6) |
| `Identity:StepUp:MaxAge` | configuration | 5 min | Step-up threshold. **Provisional** (F-6) |
| `Identity:StepUp:Operations` | configuration (`appsettings.json`) | `["IdentityAccess_TestIdentityIntegration"]` | Operations that require step-up (D-5) |

## 6. Verification

Run 2026-09-26 on macOS, Docker Desktop, PostgreSQL 17 (`AHDA-postgres`) and `AHDA-ldap`.

| # | Command | Result |
| --- | --- | --- |
| 1 | `dotnet build src/backend -warnaserror` | Build succeeded, 0 warnings |
| 2 | `dotnet test src/backend/PMPlatform.Tests.Unit` | 41 passed; architecture tests A-1 to A-6 unchanged and green |
| 3 | `DB_CONNECTION_STRING=… dotnet test src/backend/PMPlatform.Tests.Integration` | 171 passed: 61 under `Identity` (26 new, 35 from TASK-028 adapted to the second factor), 110 under `Persistence`. Ten consecutive full runs green after the fix in §6.2; 8 of 10 before it |
| 4 | `docker compose -f infra/docker/docker-compose.yml up -d --build --wait api`, then `curl -X POST localhost:5080/api/v1/sessions` | The API starts with the shipped trigger list. `local.r01`: 503 `UNAVAILABLE`, and the log names the user and "no MFA provider is configured" (D-8, F-5). `local.r02`: 201, `multiFactorAuthenticated: false` |
| 5 | All nine `docs/architecture/*-check.py` | PASS |

The 26 new tests, by acceptance criterion:

| Criterion | Tests |
| --- | --- |
| 1. MFA before a session, at least R01 | `ASystemAdministratorGetsNoSessionUntilTheSecondFactorIsVerified`, `AnSsoSignInOfASystemAdministratorAlsoWaitsForTheSecondFactor`, `AUserWhoseRolesDoNotRequireMfaGetsASessionAtOnce`, `TheFirstSecondFactorEnrolsAndEveryLaterOneVerifies`, `RequiredRolesAreConfigurationAndR01CannotBeRemoved`, `AnEnrolledUserIsChallengedWhateverTheirRoles`, `WithoutEnrolmentAtSignInAnUnenrolledAdministratorIsRefused`, `WithoutAnMfaProviderNoSessionIsIssuedToAUserWhoRequiresMfa`, `AnMfaProviderThatRefusesThePlatformIsA503`, `AnIdentityProviderSecondFactorCountsOnlyWhenTheConfigurationTrustsIt`, `TheMfaProviderIsSentOnlyTheUserIdAndTheCode` |
| 2. Privileged action needs a fresh context, threshold configurable (validation check 1) | `APrivilegedActionWithAStaleSessionRequiresReauthentication`, `ARefreshDoesNotMakeAStaleSessionFresh`, `TheThresholdIsConfiguration`, `TheTriggerListIsConfiguration`, `AnUnknownTriggerStopsStartUp`, `AStepUpOperationNeedsASecondFactorFromEveryUser`, `AStepUpWithAWrongCodeIsTheGeneric401AndChangesNothing` |
| 3. No MFA bypass via any documented API path (validation check 2) | `NoEndpointIssuesASessionOrServesAPersonWhoHasNotPassedTheSecondFactor` (every mapped endpoint, called with the password, the MFA token as bearer and in every token field, and a wrong code), `AnAccessTokenThatSkippedMfaIsRefusedByEveryProtectedEndpoint` (tokens forged with the platform key), `ARefreshTokenThatSkippedMfaIsNotRefreshed`, `ARoleThatRequiresMfaIsNotHandedOverByARefresh`, `AWrongCodeIsTheGeneric401AndSpendsTheChallenge`, `AnotherPersonsChallengeDoesNotCompleteASignIn`, `AnMfaTokenExpiresAfterFiveMinutes`, `AUserDisabledBetweenTheFactorsGetsNoSession` |
| No credential in logs | `NoCredentialOrTokenAppearsInTheLogs` (TASK-028) now also searches for the MFA provider key, the MFA token, a right and a wrong code, and the step-up's tokens |

### 6.1 Mutation tests: each protection fails its test when removed

| # | Mutation | Test | Result |
| --- | --- | --- | --- |
| M-1 | Sign-in issues a session without the second factor | `ASystemAdministratorGetsNoSession…` | FAILED |
| M-2 | Refresh does not check MFA against the user's current roles | `ARoleThatRequiresMfa…`, `ARefreshTokenThatSkippedMfa…` | FAILED (2) |
| M-3 | Bearer validation accepts a token that skipped MFA | `AnAccessTokenThatSkippedMfa…` | FAILED |
| M-4 | Step-up middleware not in the pipeline | `APrivilegedActionWithAStaleSession…` | FAILED |
| M-5 | Step-up ignores the threshold | `APrivilegedActionWithAStaleSession…`, `TheThresholdIsConfiguration` | FAILED (2) |
| M-6 | A refresh renews `auth_time` | `ARefreshDoesNotMakeAStaleSessionFresh` | FAILED |
| M-7 | An enrolled user is sent to enrolment again | `TheFirstSecondFactorEnrolsAndEveryLaterOneVerifies` | FAILED |
| M-8 | Unknown step-up operation not checked at start-up | `AnUnknownTriggerStopsStartUp` | FAILED |
| M-9 | No response-time floor on the second factor | `AWrongCodeIsTheGeneric401…` | FAILED |
| M-10 | The identity provider's `amr` trusted without configuration | `AnIdentityProviderSecondFactorCounts…` | FAILED |
| M-11 | The second factor does not re-check whether the user may sign in | `AUserDisabledBetweenTheFactorsGetsNoSession` | FAILED |
| M-12 | The access-token audience also admits the MFA token | `NoEndpointIssuesASession…` | **PASSED**: D-4 still refuses the MFA token, which carries no `auth_time`. Two independent layers; M-13 removes both |
| M-13 | M-12 and M-3 together | `NoEndpointIssuesASession…` | FAILED |

Every mutation was reverted and the suite re-run green.

### 6.2 A defect the repeated runs found

`AWrongCodeIsTheGeneric401AndSpendsTheChallenge` failed in 3 of the 16 recorded full runs before the fix, each time with "answered after 00:00:00.9997442": the rejected answer came back 0.3 ms **before** the 1 s floor. `Task.Delay` wakes on a millisecond-resolution timer, which can fire a fraction of a millisecond before the high-resolution clock the floor is measured on reaches it. The floor, shared since TASK-028 with the password path, now re-checks the elapsed time after each wait and waits out any remainder (`AuthenticationService.WithRejectionFloorAsync`). Ten consecutive full runs after the fix were green, and M-9 still fails the test. The bypass tests also obtain their MFA token through `MfaTokenOrFailAsync`, which asserts the sign-in's 200, so any other cause would name itself.

## 7. Acceptance criteria, validation and gate decision

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | MFA is enforced before a session is granted for at least the R01 role | **MET** on both sign-in paths, fail-closed without a provider; R01 cannot be configured away | D-1, D-2, D-8; §6 row 1; M-1 |
| 2 | A privileged action requires a fresh authentication context no older than a configurable threshold | **MET** for the configured operations; the threshold and the list are configuration. The criterion's example, role assignment, has no endpoint yet (F-3) | D-3, D-5; §6 row 2; M-4, M-5, M-6 |
| 3 | MFA bypass is not possible via any documented API path | **MET**: every mapped endpoint is exercised, so an endpoint added later is covered too | D-1, D-4; §6 row 3; M-2, M-3, M-13 |
| — | Validation: a privileged action with a stale session requires re-authentication | **MET** | `APrivilegedActionWithAStaleSessionRequiresReauthentication` |
| — | Validation: a direct call to the privileged API without completing MFA is rejected | **MET** | `NoEndpointIssuesASession…`, `AnAccessTokenThatSkippedMfa…` |
| — | Sheet verification, `MFA_PROVIDER_*`: a test MFA challenge/verify round-trip succeeds | **MET against the test provider**, not a real one (F-1, F-11) | `ASystemAdministratorGetsNoSession…`, `TheFirstSecondFactorEnrols…` |
| — | Gate ADR-010: mechanism built, triggers configurable | **MET** | D-5 |
| — | Amendment ADR-013: external sign-in path; step-up per ADR-010 | **MET**: one gate for internal and external users | D-2 |
| — | Amendment ADR-018: step-up unaffected by profile authoring | **MET**: step-up rules live in configuration, not in profiles | D-5 |

## 8. Findings

| # | Finding | Owner | Consequence if left |
| --- | --- | --- | --- |
| F-1 | **No MFA provider is selected** (PTBC-027). The adapter speaks the platform's contract (§4). The selected product needs either a thin adapter to that contract, or the identity provider's MFA via `IdentityProviderMethods`, in which case the sheet rows for `MFA_PROVIDER_*` are removed first (environment-templates F-4) | AHDA Cybersecurity + Security Lead | No environment can issue an R01 session |
| F-2 | **Enrolment at sign-in rests on the first factor alone.** Someone holding the password of an R01 who has not enrolled yet could enrol their own factor. Options: set `AllowEnrolmentAtSignIn=false` and enrol out of band (an administrator-issued enrolment, TASK-031), or enrol at the identity provider. This is AHDA's PAM policy to set | AHDA Cybersecurity (PTBC-027); TASK-031 for out-of-band enrolment | The first sign-in of each R01 is the weakest point |
| F-3 | **The trigger list holds one operation.** Role assignment (the criterion's example) is TASK-031's endpoint; TASK-031 must add its operation id to `Identity:StepUp:Operations`. The ERD's `permission.is_privileged` is the intended long-term trigger source once TASK-030's engine declares a permission per endpoint | TASK-030, TASK-031; AHDA Cybersecurity for the list (UGV-01) | Privileged actions built later do not step up unless listed |
| F-4 | **Code guessing is bounded by the provider and the 1 s floor only.** An MFA token can start new challenges for 5 minutes; the platform keeps no attempt count (stateless, TASK-028 D-3). The provider must limit attempts per subject, and TASK-078 rate-limits the endpoints | Provider selection (F-1); TASK-078 | A provider without attempt limits allows parallel guessing within the MFA token's lifetime |
| F-5 | **Locally, R01 cannot sign in.** The compose stack has no MFA provider, so `local.r01` gets 503 (D-8). There is deliberately no switch to turn MFA off. R01 flows run in the integration tests. A local provider would be a new AHDA container | DevOps, if wanted | Local manual testing of R01 screens needs the test provider or a real one |
| F-6 | **Thresholds are provisional**: step-up 5 min, MFA token 5 min. They are configuration, so confirming them changes no code | AHDA Cybersecurity (PTBC-027), with TASK-028 F-1 | Step-up is demanded more or less often than AHDA's policy |
| F-7 | **A session whose MFA came from the identity provider steps up through the platform's MFA provider.** If AHDA uses identity-provider MFA only, step-up needs an OIDC re-authentication (`prompt=login` or `max_age`, then check `auth_time`), which is not built | Identity, once F-1 is decided | Step-up operations are unusable where only the identity provider has MFA |
| F-8 | **403, not RFC 9470's 401.** The error catalogue fixes 403 `STEP_UP_REQUIRED`; the `WWW-Authenticate` parameters are RFC 9470's, so an RFC 9470 client reads the header but may expect 401 | Engagement Architect, if the catalogue changes | None for the platform's own SPA |
| F-9 | **Security Lead review (CTL-43) could not be requested**: the repository belongs to a personal account, so the CODEOWNERS teams do not exist. This change is authentication throughout | Maintainer: request the Security Lead's review by name | The checklist item stays unticked |
| F-10 | **CTL-07 and PTBC-027 rows are unchanged.** The control matrix and the PTBC tracker record ratification status, which this task does not change; PTBC-027 stays Partially Resolved until F-1, F-2 and F-6 are answered | PMO | — |
| F-11 | **Not run against a real MFA provider or in an environment.** The first environment's live check is one R01 sign-in with each method and one step-up | DevOps/Platform Lead + Security Lead, at first environment | — |

## 9. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-26 | Initial record. MFA gate before every session for R01 and configured roles, enrolment and verification through an MFA provider adapter, identity-provider MFA when configured; `amr` array and `auth_time` in the session tokens; step-up middleware with configurable operations and threshold; bearer validation that refuses tokens that skipped MFA; four endpoints; test MFA provider; 26 integration tests, thirteen mutations. The rejection floor re-checks after each wait, so it never answers early (§6.2). Eleven findings | Security (TASK-029) |

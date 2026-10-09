# Nafath Identity Verification

| Field | Value |
| --- | --- |
| Task | TASK-068 — Integrate Nafath Identity Verification (P11 - Collaboration & External Participation (WF-13)). The gate decision removes the "(If Confirmed In Scope)" qualifier: Nafath is confirmed in scope by ADR-007 |
| Depends on | TASK-066 — WF-13 backend (`external-participation.md`), whose §6 hands the external sign-in to this task. Built on TASK-028's sign-in and OIDC client (`sso-directory-integration.md`) and TASK-029's admission gate and pending-token pattern (`mfa-privileged-access.md`) |
| Record date | 2026-10-09 |
| Status | **BUILT AND VERIFIED LOCALLY** against a Nafath played in the test process (an OpenID Connect provider under Nafath's own client registration), over `AHDA-postgres` and `AHDA-ldap`. **Off by default** (`Identity:Nafath:Enabled`, D-7). **Not run against Nafath**: the platform is not registered with it, and its integration contract is unconfirmed (F-2). **Not usable from the SPA yet**: the sign-in screen has no verification step (F-4) |
| Branch | `feat/task-068-nafath-identity-verification` |
| Deliverables | **Nafath integration module**: `PMPlatform.Infrastructure/Identity/Nafath` (`NafathIdentityVerifier`, `NafathClient`, `NafathOptions`) behind the port `IIdentityVerificationProvider`; the admission gate in `AuthenticationService` (`AdmitAsync`, `BeginIdentityVerificationAsync`, `CompleteIdentityVerificationAsync`); `IdentityVerificationPolicy` (the feature flag); two endpoints on `SessionsController`; the shared `OpenIdConnectRelyingParty` extracted from TASK-028's `OpenIdConnectProvider` (D-5). **Data-minimisation decision record**: `nafath-data-minimisation.md`. Tests: `NafathVerificationTests` (8), `NafathDataMinimisationTests` (2), two ADM-041 tests. This record |
| Environment variables / secrets | `NAFATH_CLIENT_ID`, `NAFATH_CLIENT_SECRET` (secret store, optional: `ApplicationSecrets.OptionalKeys`); `NAFATH_CALLBACK_URL`, `NAFATH_APP_ID` (configuration store). All four were already in the sheet, the templates and `environments.json`; nothing is added to the sheet. Nafath's issuer URL has no sheet variable and is the configuration key `Identity:Nafath:Authority` (D-10, F-2) |
| Gate decision applied | **"CONFIRMED IN SCOPE by ADR-007 — … External entity identity verification only; never internal sign-in. Data-minimization boundary still open (OQ-007)."** — D-1, D-7, `nafath-data-minimisation.md` |
| Participation amendment | **ADR-013 (note of 19 Sep 2026 on ADR-007)**: "Nafath verifies identity at onboarding; a persistent sign-in session follows. Verification alone is insufficient." Nafath is a gate on an external user's first session, after their sign-in, and never a sign-in itself (D-1, D-2) |
| Sources read | The TASK-068 row as supplied on 2026-10-09, checked against the workbook (`AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`): Implementation Plan row (branch, validation check, gate decision), Environment and Secrets rows 18–21, Open Questions OQ-007 (resolution and impact), Architecture Decisions ADR-007. Every Nafath passage in `Project Files.zip`: Blueprint v2.0 (ADM-043, PTBC-031, PTBC-032), the Integration Reconciliation Review, FG-05 (§ Nafath monitoring, TBC-INT-03), WF-01, WF-13 (TBC-EXT-004), the Development and Cybersecurity Guide. `adrs/ADR-register.md` ADR-007 and ADR-013; `ptbc-tbc-tracker.md` PTBC-031 and PTBC-003; `cybersecurity-control-matrix.md` CTL-10, CTL-21, CTL-43, CTL-50; ERD F-086 |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | Verifying an external user's identity with Nafath once, at onboarding, before their first session; never for an internal user, never as a sign-in | This task |
| **In** | The fallback when Nafath is unavailable: an explicit, retryable state that neither grants nor denies | This task |
| **In** | Keeping only a reference and a time, and the written decision for it | This task (`nafath-data-minimisation.md`) |
| **In** | Nafath's settings and connection test on ADM-041's backend, until ADM-043 has its own (D-11) | This task |
| **Out** | The SPA's verification step and its callback route at `NAFATH_CALLBACK_URL` (F-4) | A frontend task |
| **Out** | ADM-043, Nafath's administration screen | TASK-076 |
| **Out** | Nafath invocation telemetry to FG-05 (ADR-003 edge 33) (F-6) | TASK-075 |
| **Out** | Resetting a user's verification, or verifying an account again (F-7) | FG-03 follow-up, after OQ-007 |
| **Out** | Rate limiting of the verification endpoints | TASK-078 |
| **Out** | Who may onboard an external user and on what criteria (PTBC-003): FG-03 creates the account; this task verifies the person who signs in to it | TASK-031 (built); PMO for per-entity criteria |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **One use case, enforced in one place.** Verification is required when `Identity:Nafath:Enabled` is on, the user is `EXTERNAL`, and `user.nafath_verified_at` is null (`IdentityVerificationPolicy.Requires`). Nafath is called from two methods only, `BeginIdentityVerificationAsync` and `CompleteIdentityVerificationAsync`. Both re-read the user from the token's subject and refuse, before any call to Nafath, a user who is disabled, whose entity is not active, or who needs no verification (internal, verified already, or the feature off since the token was issued). | Acceptance criterion 1: "called only for the confirmed use case(s) in PTBC-031's resolution", which is ADR-007's "external entity users, identity verification only, never internal sign-in" and ADR-013's "at onboarding". A verified user is not verified again, so Nafath hears nothing of a returning user's sign-ins. |
| D-2 | **The last gate before a session.** Directory and SSO sign-in, with MFA where it applies, all end in `AuthenticationService.AdmitAsync`. A person who needs verification gets **200 with an identity verification token** instead of 201 with a session. The session is issued only by `POST /sessions/identity-verification`, after Nafath verified them. It carries the authentication context the person earned (method, MFA, `auth_time`) unchanged. | Verification comes after every factor, so Nafath is never reached by someone who has not proved they hold the account. "Verification alone is insufficient" (ADR-013): Nafath does not replace the sign-in. |
| D-3 | **The identity verification token is a fourth audience** (`pmplatform-session-identity-verification`) beside the access, refresh and MFA tokens. It holds `sub`, `amr` and `auth_time`, no role, and lives 20 minutes (`Identity:Session:IdentityVerificationTokenLifetime`, provisional like the other lifetimes). Every other endpoint refuses it, and the verification endpoints refuse every other token. | The MFA token's pattern (`mfa-privileged-access.md` D-1). Twenty minutes outlasts a Nafath transaction (10 min), so a verification Nafath could not complete can be started again without signing in again (D-6). |
| D-4 | **The adapter speaks OpenID Connect**: the authorization code flow with PKCE (S256), nonce and state, the platform as a confidential client (`client_secret_basic`), scope `openid`. The person authenticates on Nafath's page and is redirected to `NAFATH_CALLBACK_URL`. **The integration contract is unconfirmed** (F-2). This reading follows the sheet: a client id and secret, a "registered callback URL for the Nafath verification flow", and "callback completes without a redirect-mismatch error" all describe a redirect flow with a registered redirect URI. | The specifications leave the provider contract open (TBC-INT-03, TBC-EXT-004, PTBC-032). The port `IIdentityVerificationProvider` keeps the protocol in one class, so a different Nafath product replaces `NafathIdentityVerifier` and nothing else. |
| D-5 | **One OIDC relying party for SSO and Nafath.** TASK-028's code exchange, ID-token validation and metadata cache moved unchanged from `OpenIdConnectProvider` into `OpenIdConnectRelyingParty`, which each provider holds its own instance of. Each flow seals its transaction under its own HKDF label (`AuthorizationTransactionProtector`), so an SSO transaction cannot be opened as a Nafath one or the other way round. A Nafath transaction is **bound to the platform user** it was started for, and completing it under another user's token fails. | DRY: the flow is the same protocol. Binding stops a code obtained in one person's verification from completing another's (and stops a planted callback, the login-CSRF variant). SSO's label is unchanged, so an SSO sign-in under way survives the deployment. The 110 identity tests that predate this task pass unchanged. |
| D-6 | **Three answers, none of them a silent grant or denial.** The verification token not accepted (forged, expired, another kind of token, user now disabled or needing no verification) is the generic **401 `AUTHENTICATION_REQUIRED`**: sign in again. Nafath answered and did not verify (code spent or expired, state, transaction, nonce or binding not this verification's, no subject) is **422 `IDENTITY_VERIFICATION_FAILED`**: start the verification again with the same token. Nafath unreachable, refusing the platform's client, failing, or not configured is **503 `IDENTITY_VERIFICATION_UNAVAILABLE` with `Retry-After`** (`Identity:Nafath:RetryAfter`, 30 s): no session, nothing recorded against the person, the same token still good. A sign-in that needs verification never calls Nafath, so an outage cannot fail the sign-in itself. | Acceptance criterion 3 and the validation check: "neither silently grants nor silently denies access, but surfaces an explicit retry/error state". The person is signed in at this point, so the no-enumeration rule (TASK-028 D-5) has nothing to protect, and a precise code serves them better. Every outcome is audited (D-9). |
| D-7 | **Off by default.** `Identity:Nafath:Enabled` is false unless an environment sets it. Off, the platform behaves as before TASK-068 for everyone. | OQ-007's impact note: "the module can be built behind a feature flag but should not go live until confirmed". The minimisation boundary is still open, and the SPA cannot drive the flow yet (F-4). |
| D-8 | **A session that skipped a verification now required is not continued.** A refresh or step-up of an external user's session that never passed verification is refused (401, audited as `IDENTITY_VERIFICATION_MISSING`). The person signs in again and is verified. | As MFA (`mfa-privileged-access.md` D-4): turning the feature on must not leave earlier sessions running indefinitely. An access token already issued lives out its 15 minutes (F-8). |
| D-9 | **Data minimisation: a reference and a time** (`nafath-data-minimisation.md` MD-1 to MD-8). Scope `openid` only; the ID token is validated and dropped; the platform keeps a UUID of its own (`nafath_verification_reference`) and `nafath_verified_at`; audit events and logs carry the user id and the reference, never an identity attribute. The first verification stands; a concurrent second one changes nothing. | Acceptance criterion 2. ERD F-086 ("minimum attributes only") and `User.NafathVerificationReference` ("only the verification reference is stored") already said so; OQ-007 remains AHDA Cybersecurity's to confirm or widen. |
| D-10 | **Secrets and settings.** `NAFATH_CLIENT_ID` and `NAFATH_CLIENT_SECRET` are optional secret-store keys (`ApplicationSecrets.OptionalKeys`), like the `AD_*` and SSO secrets (TASK-028 D-9). The sheet classifies the client id as Secret, so it is stored, and reported on the status, as one. `NAFATH_CALLBACK_URL` and `NAFATH_APP_ID` come from the configuration store. The issuer is `Identity:Nafath:Authority`. Discovery and keys must be HTTPS unless `Identity:Nafath:RequireHttpsMetadata` is cleared (tests only). | The sheet scopes all four to SIT/UAT/PROD, so DEV must start without them. `secret-management-check.py` K-1 confirms every environment that scopes them has a container. |
| D-11 | **ADM-041 carries Nafath for now.** `GET /api/v1/identity-integration` gains `identityVerification`: the flag, whether the client is complete, the authority, the callback URL and application id, the scopes, and the two secrets reduced to flags. `POST /api/v1/identity-integration/test` resolves Nafath's discovery document and checks its issuer, and the audited test event records the outcome. | The sheet's verification methods need an operator-side check, and ADM-043 is TASK-076's. Additive: the existing members are unchanged. |

## 3. API surface

| Operation | Endpoint | Auth | Success | Failure |
| --- | --- | --- | --- | --- |
| `IdentityAccess_CreateSession`, `_CreateSsoSession`, `_CreateMfaSession` (changed) | as before | anonymous | 201 `SessionDetail`, 200 `MfaPendingDetail`, **or 200 `IdentityVerificationPendingDetail`** | as before |
| `IdentityAccess_CreateIdentityVerificationAuthorization` | `POST /api/v1/sessions/identity-verification-authorization` `{ identityVerificationToken }` | anonymous (the token is the credential) | 200 `{ authorizationUrl, transaction }` | 400; 401; 503 `IDENTITY_VERIFICATION_UNAVAILABLE` + `Retry-After` |
| `IdentityAccess_CreateIdentityVerificationSession` | `POST /api/v1/sessions/identity-verification` `{ identityVerificationToken, code, state, transaction }` | anonymous | 201 `SessionDetail` | 400; 401; 422 `IDENTITY_VERIFICATION_FAILED`; 503 `IDENTITY_VERIFICATION_UNAVAILABLE` + `Retry-After` |
| `IdentityAccess_RefreshSession`, `_StepUpSession` (changed) | as before | anonymous | as before | 401 also when the user now needs verification the session never passed (D-8) |
| `IdentityAccess_GetIdentityIntegration`, `_TestIdentityIntegration` (changed) | as before | bearer, `IDENTITY_INTEGRATION_MANAGE` | gains `identityVerification` (D-11) | as before |

`IdentityVerificationPendingDetail` is `{ identityVerificationToken, identityVerificationTokenExpiresAt }`. Every response carrying a token, an authorization URL or a transaction is `Cache-Control: no-store`. The client keeps the transaction in session storage between the redirect out and the callback, as for SSO. When Nafath's redirect carries an `error` instead of a code (the person cancelled at Nafath), the client starts again with `identity-verification-authorization`; the platform has nothing to redeem. The OpenAPI snapshot gains the two paths and three schemas, and `IdentityIntegrationStatus` and `IdentityIntegrationTestResult` gain one member each; nothing else changes.

New audit events (class `AUTHENTICATION`, `event-conventions.md` §4 row 8): `IdentityAccess.IdentityVerificationRequired` (the sign-in passed and waits on Nafath), `IdentityAccess.IdentityVerified` (with `verification_reference`, committed with the user row), `IdentityAccess.IdentityVerificationFailed` (with `failure_reason`: `TOKEN_INVALID`, `ACCOUNT_INACTIVE`, `IDENTITY_VERIFICATION_NOT_REQUIRED`, `IDENTITY_NOT_VERIFIED`, `PROVIDER_UNAVAILABLE`, `NOT_CONFIGURED`). A verified sign-in then records `IdentityAccess.SignInSucceeded` as any other.

## 4. Configuration

| Key | Store | Default | Meaning |
| --- | --- | --- | --- |
| `Identity:Nafath:Enabled` | configuration | `false` | The feature flag (D-7) |
| `Identity:Nafath:Authority` | configuration | — | Nafath's OIDC issuer for the environment (sandbox or production) (F-2) |
| `NAFATH_CLIENT_ID`, `NAFATH_CLIENT_SECRET` | secret store | — | The platform's client at Nafath; read per use, so a rotation applies without a restart |
| `NAFATH_CALLBACK_URL` | configuration store | — | The registered redirect URI; the SPA's verification callback route |
| `NAFATH_APP_ID` | configuration store | — | Reported on the status; not sent (F-3) |
| `Identity:Nafath:Scopes` | configuration | `["openid"]` | Widening it is an OQ-007 decision (`nafath-data-minimisation.md` §4) |
| `Identity:Nafath:RequireHttpsMetadata`, `TransactionLifetime`, `Timeout`, `RetryAfter` | configuration | `true`, 10 min, 10 s, 30 s | |
| `Identity:Session:IdentityVerificationTokenLifetime` | configuration | 20 min | Provisional (TASK-028 F-1) |

The client is complete only when the authority, both secrets and the callback URL are set; with the flag on and the client incomplete, a person who needs verification is told it is unavailable (503), never let through.

## 5. Layering

`SessionsController` → `IAuthenticationService` (`AuthenticationService`, Features/IdentityAccess/Authentication) → the port `IIdentityVerificationProvider`, declared beside the other sign-in ports and implemented by `Infrastructure/Identity/Nafath/NafathIdentityVerifier`, the workbook's canonical directory. The verifier and `OpenIdConnectProvider` each hold an `OpenIdConnectRelyingParty`. The repository write is `IUserAccessRepository.RecordIdentityVerificationAsync`. No migration: the two columns exist since TASK-025. The architecture tests pass unchanged.

## 6. Verification

Run 2026-10-09 on macOS, Docker Desktop, PostgreSQL 17 (`AHDA-postgres`) and `AHDA-ldap`, with `NUGET_PACKAGES=/Volumes/SanDisk/Bader/Development/Caches/nuget`.

| # | Command | Result |
| --- | --- | --- |
| 1 | `dotnet build src/backend/PMPlatform.slnx -warnaserror` | Build succeeded, 0 warnings |
| 2 | `dotnet test src/backend/PMPlatform.Tests.Integration --filter FullyQualifiedName~PMPlatform.Tests.Integration.Identity`, before the Nafath tests were written | 110 passed: TASK-028/029/031/033's identity suite unchanged on the extracted relying party (D-5) |
| 3 | The same, with this task's tests | 122 passed: 110 before, 10 Nafath tests and 2 ADM-041 tests |
| 4 | `dotnet test src/backend/PMPlatform.Tests.Unit` | 1599 passed |
| 5 | `dotnet test … --filter FullyQualifiedName~PMPlatform.Tests.Integration.AuditActivity` | 32 passed |
| 6 | `UPDATE_OPENAPI_SNAPSHOT=1 … TheSnapshotIsTheApiAsBuilt`, then a JSON diff of `docs/api/openapi.v1.json` against `dev` | +2 paths, +3 schemas; `IdentityIntegrationStatus` and `IdentityIntegrationTestResult` gain `identityVerification`; nothing removed or otherwise changed |
| 7 | `python3 docs/architecture/contract-check.py docs/api/openapi.v1.json` | 343 operations, 1497 findings (1489 on `dev`). The 8 new ones are on the two new operations, in the platform-wide open classes (C-4, C-5, C-7, C-12; `OpenApiDocument.OpenPlatformFindings`) |
| 8 | The nine `docs/architecture/*-check.py` gates | All OK. `secret-management-check.py` K-1 accepts the two new optional keys; `env-template-check.py`: 43 sheet variables, 43 in the template |
| 10 | Live validation (`artifacts/task-068/live`, not committed): `reset.sh` migrates and seeds a fresh `task068_live` with this branch's `ahda-migrate` image and runs this branch's `ahda-api` as `AHDA-api-068` (`ASPNETCORE_ENVIRONMENT=Development`, non-PROD) with Nafath on, pointed at a Nafath stand-in on the host (an OIDC provider with an outage switch whose ID tokens carry a synthetic national id, two names and a birth date). `check.py` drives it over HTTP | **25/25 passed** (§6.2) |
| 9 | `gitleaks dir . --config .gitleaks.toml` | No finding in a tracked file or in the branch's commits; the 102 findings of a whole-tree scan are all in git-ignored `artifacts/` logs of earlier tasks |

The tests, by criterion:

| Criterion | Tests |
| --- | --- |
| 1 — called only for the confirmed use case | `AnExternalUserGetsNoSessionUntilNafathVerifiesThemAndSignsInWithoutNafathAfterwards` (200 with a verification token and no session; `scope=openid`, PKCE; 201 after Nafath; the next sign-in and its refresh send Nafath nothing), `NafathIsNeverCalledForAnInternalUser` (R01 with MFA, R02, R03, R06 by password, R02 by SSO, and a forged verification token for an internal user: Nafath's request count unchanged), `WithTheFeatureOffNafathIsNeverCalled`, `ANafathVerificationIsNeverASignIn` (a genuine Nafath callback refused with an MFA, refresh or access token or none, and as an SSO sign-in), `ThePersonPassesMfaFirstAndTheSessionAfterTheVerificationKeepsIt` |
| 2 — minimum attributes stored, decision documented | `NafathDataMinimisationTests` (both), `nafath-data-minimisation.md` §5 |
| 3 — tested fallback that neither grants nor denies | `ANafathOutageIsAnExplicitRetryStateThatNeitherGrantsNorDeniesAccess` (Nafath unreachable at the start, down at the code's redemption, and not configured: 503 `IDENTITY_VERIFICATION_UNAVAILABLE` with `Retry-After: 30` each time, no session, the account active and unverified, and the same token verifying once Nafath is back), `AVerificationNafathDidNotGrantIsAnExplicitFailureThatCanBeStartedAgain` (ID token for another authorization, a spent code, wrong state, a tampered transaction, another person's transaction: 422 each, then success with the same token) |
| D-8 | `ASessionThatSkippedTheVerificationIsNotRefreshed` |
| D-11 | `TheStatusShowsNafathsSettingsAndNeitherOfItsSecrets`, `TheConnectionTestReachesNafathOrSaysWhyNot` |
| No token bypass | `MultiFactorBypassTests.NoEndpointIssuesASessionOrServesAPersonWhoHasNotPassedTheSecondFactor` now also puts the MFA token in `identityVerificationToken` |

### 6.1 Mutation tests: each protection fails its tests when removed

`artifacts/task-068/mutations/run.py` applies each mutation, builds, runs the unit tests and the Identity suite, and restores the file.

| # | Mutation | Result | Tests that failed |
| --- | --- | --- | --- |
| M-1 | No admission gate: every external user gets a session without Nafath | KILLED | OnlyAReferenceAndATimeAreKeptOfAVerification, TheUserRecordShowsWhenTheIdentityWasVerifiedAndNotTheReference, ANafathOutageIsAnExplicitRetryStateThatNeitherGrantsNorDeniesAccess, ANafathVerificationIsNeverASignIn, ASessionThatSkippedTheVerificationIsNotRefreshed, AVerificationNafathDidNotGrantIsAnExplicitFailureThatCanBeStartedAgain, AnExternalUserGetsNoSessionUntilNafathVerifiesThemAndSignsInWithoutNafathAfterwards, ThePersonPassesMfaFirstAndTheSessionAfterTheVerificationKeepsIt |
| M-2 | Use case widened: internal users are verified too | KILLED | TheUserRecordShowsWhenTheIdentityWasVerifiedAndNotTheReference, ANafathVerificationIsNeverASignIn, NafathIsNeverCalledForAnInternalUser |
| M-3 | Not once: a verified user is verified again at every sign-in | KILLED | AnExternalUserGetsNoSessionUntilNafathVerifiesThemAndSignsInWithoutNafathAfterwards |
| M-4 | No feature flag: Nafath applies whatever the configuration | KILLED | AnExternalUserHoldingR01IsRefused, AnEntityProjectManagerSignsInAsAnExternalUserHoldingR04OnTheirProject, ASessionThatSkippedTheVerificationIsNotRefreshed, WithTheFeatureOffNafathIsNeverCalled |
| M-5 | A token for an internal or verified user still reaches Nafath | KILLED | NafathIsNeverCalledForAnInternalUser, WithTheFeatureOffNafathIsNeverCalled |
| M-6 | A transaction is not bound to the person it was started for | KILLED | AVerificationNafathDidNotGrantIsAnExplicitFailureThatCanBeStartedAgain |
| M-7 | An outage at the start is answered as the person's failure (401) | KILLED | ANafathOutageIsAnExplicitRetryStateThatNeitherGrantsNorDeniesAccess |
| M-8 | An outage at the code's redemption is answered as "not verified" (422) | KILLED | ANafathOutageIsAnExplicitRetryStateThatNeitherGrantsNorDeniesAccess |
| M-9 | The API answers an outage with the generic 503, no retry state | KILLED | ANafathOutageIsAnExplicitRetryStateThatNeitherGrantsNorDeniesAccess |
| M-10 | The national id Nafath asserts is kept as the reference | KILLED | OnlyAReferenceAndATimeAreKeptOfAVerification, AnExternalUserGetsNoSessionUntilNafathVerifiesThemAndSignsInWithoutNafathAfterwards |
| M-11 | A refresh continues a session that skipped the verification | KILLED | ASessionThatSkippedTheVerificationIsNotRefreshed |

Every mutation was reverted, the solution rebuilt, and the suites re-run green (§6 rows 3–5). No unit test failed under any mutation: the protections are exercised end to end.

### 6.2 Live validation: the workbook's checks against a running API

Run 2026-10-09 against `AHDA-api-068` on `task068_live`, the stand-in switched down and up by the check:

| Check | Result |
| --- | --- |
| Nafath down from the outset | local.r08's sign-in still answers 200 with a verification token and no session. Starting the verification answers 503 `IDENTITY_VERIFICATION_UNAVAILABLE`, `Retry-After: 30`, with no token in the body. The verification token is refused (401) by `GET /sessions/current`. The account stays `ACTIVE` and unverified, and a new sign-in is again shown the verification |
| Nafath down after the person approved | Completing answers 503 `IDENTITY_VERIFICATION_UNAVAILABLE`, `Retry-After: 30`, no session; nothing recorded |
| Nafath back | The **same** token and code complete: 201, a working EXTERNAL session |
| Afterwards | local.r08's next sign-in is 201 at once; internal local.r02, r03, r04 and r06 sign in with 201. The stand-in received no request for any of these |
| Stored fields vs `nafath-data-minimisation.md` | The user row changed in `nafath_verification_reference` (a UUID), `nafath_verified_at`, `updated_at` and `updated_by` only. None of the four identity values the stand-in sent appears in `pg_dump --data-only` of the database (130 KB) or in the API container's log (70 KB). Nor do the client secret, code, transaction or token. Audit: two `IdentityVerificationFailed` with `failure_reason=PROVIDER_UNAVAILABLE`, then `IdentityVerified` with the reference only |

## 7. Acceptance criteria, validation and gate

| # | Criterion | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Nafath verification is called only for the confirmed use case(s) in PTBC-031's resolution | **MET.** External users, at onboarding, once; never internal users; never as a sign-in | D-1, D-2; §6 criterion 1; M-1 to M-5 |
| 2 | Only the minimum required identity attributes are stored, and this minimisation decision is documented | **MET at the narrowest boundary; the boundary itself awaits AHDA Cybersecurity (OQ-007, F-1)** | `nafath-data-minimisation.md`; M-10 |
| 3 | A tested fallback/error path when Nafath is unavailable that does not silently grant or deny access | **MET** | D-6; §6 criterion 3; M-7 to M-9 |
| — | Validation: simulate a Nafath outage in a non-PROD environment; the fallback surfaces an explicit retry/error state | **MET on the local DEV stack** against a running API built from this branch, the outage simulated at a Nafath stand-in both before the verification starts and after the person approved (§6.2), and in the test suite. Not run in an AHDA SIT/UAT environment against Nafath's sandbox: none exists (F-5) | §6.2; `ANafathOutageIs…` |
| — | Validation: review stored fields against the documented minimisation decision | **MET**, live and in the suite | §6.2; `NafathDataMinimisationTests.OnlyAReferenceAndATimeAreKeptOfAVerification` |
| — | Gate ADR-007: external entity identity verification only; never internal sign-in | **MET** | D-1; `NafathIsNeverCalledForAnInternalUser`; `ANafathVerificationIsNeverASignIn` |
| — | Amendment ADR-013: verification at onboarding; a persistent session follows; verification alone is insufficient | **MET** | D-2; the second sign-in in `AnExternalUserGetsNoSession…` |
| — | Sheet verification, `NAFATH_*`: a sandbox round-trip; callback without a redirect-mismatch error; request accepted by the sandbox | **NOT RUN against Nafath** (F-5). The round trip runs against the in-process Nafath, which checks client id, secret, redirect URI, PKCE and single use of the code | `AnExternalUserGetsNoSession…`; `TheConnectionTestReachesNafath…` |

## 8. Findings

| # | Finding | Owner | Consequence if left |
| --- | --- | --- | --- |
| F-1 | **OQ-007 is open.** The platform keeps a reference and a time (option A of `nafath-data-minimisation.md` §4). That record lists what this cannot answer (which person verified an account; one person on two accounts; an account handed on) and what options B and C would cost | AHDA Cybersecurity | The boundary stays the delivery team's proposal, not AHDA's decision |
| F-2 | **Nafath's integration contract is unconfirmed.** The specifications leave the provider contract open (TBC-INT-03, TBC-EXT-004, PTBC-032). This task reads the sheet's variables as an OIDC redirect flow (D-4). If AHDA's registration is SAML, or Nafath's app-based push verification instead of a redirect, only `NafathIdentityVerifier` changes. The issuer URL has no sheet variable (`Identity:Nafath:Authority`), and whether Nafath can trace a verification from the platform's client id and time is part of the same registration | AHDA IT (Identity), with Nafath, at registration | Nafath cannot be switched on |
| F-3 | **`NAFATH_APP_ID` is reported, not sent.** An OIDC exchange has no place for it; the status shows it so AHDA IT can match the registration. If the registration names a parameter for it, the adapter sends it | AHDA IT (Identity) | — |
| F-4 | **The SPA has no verification step.** `SignInPage` treats any 200 from a sign-in without an `mfaToken` as a session, so with the flag on an external user cannot sign in through the browser. The SPA needs the step after sign-in and MFA, and a callback route at `NAFATH_CALLBACK_URL` that keeps the transaction in session storage, as for SSO (TASK-028 F-12). The flag must stay off in any environment until then | Frontend task (sign-in) | Turning the flag on locks external users out of the SPA |
| F-5 | **Not run against Nafath.** No environment and no registration exist. The sheet's three verification methods are the first environment's live check: ADM-041's connection test, one verification end to end, and one forced outage | DevOps/Platform Lead + AHDA IT, at first environment | — |
| F-6 | **No FG-05 telemetry yet.** ADR-003 edge 33 and event catalogue row 5 expect Nafath invocations as FG-05 invocations; FG-05 is unbuilt. Every outcome is audited and logged with the correlation id. Edge 33 named ExternalParticipation as the source; it now names IdentityAccess, where the adapter is | TASK-075 | Nafath's availability is not on ADM-047 until TASK-075 |
| F-7 | **A verification cannot be reset.** An account reassigned to a different person keeps the first person's verification, and nothing re-verifies it. Resetting the two columns is an FG-03 administrative action. Whether it is needed depends on F-1 (option B detects a different person) | FG-03 owner, after OQ-007 | A reassigned external account is never re-verified |
| F-8 | **Access tokens already issued live out their lifetime.** Turning the flag on stops refresh and step-up of unverified external sessions (D-8), but an access token issued before keeps working for at most 15 minutes. Tokens are stateless (TASK-028 D-3, F-3) | — | At most 15 minutes |
| F-9 | **Security Lead review (CTL-43) cannot be requested**: the CODEOWNERS teams do not exist (TASK-031 F-13). This change is authentication and Nafath throughout | Maintainer | The PR's CTL-43 box stays unticked |
| F-10 | **CTL-50: Nafath's operator and processing location are not named in a record.** They belong in the integration inventory with the registration | AHDA Cybersecurity, with F-2 | CTL-50 open for Nafath |

## 9. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-10-09 | Initial record. Nafath identity verification for external users at onboarding, behind a feature flag: the admission gate after every factor, the identity verification token, two endpoints, OIDC with PKCE and a user-bound transaction, explicit 401/422/503 outcomes, refresh and step-up refusal, a reference and a time kept. TASK-028's OIDC flow extracted into a shared relying party. ADM-041 status and test gain Nafath. Ten findings | Identity (TASK-068) |

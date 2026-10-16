# Threat Model — Nafath Identity Verification

| Field | Value |
| --- | --- |
| Feature | Verification of an external entity user's identity through Nafath at onboarding (ADR-007; CTL-10, CTL-21), behind `Identity:Nafath:Enabled` |
| Built by | TASK-068 (PR #78), on the OIDC relying party of TASK-028 (PR #32) |
| Reviewed | `dev` at `6f19809`, 2026-10-16 |
| Feature records | `nafath-identity-verification.md`, `nafath-data-minimisation.md` |
| Spec | ADR-007; WF-13 §9.2 ("identity proofing … per approved policy"), TBC-EXT-004; PTBC-031, PTBC-032; OQ-007 |

Paths are under `src/backend/` unless stated.

## 1. In scope

The workbook row says "Nafath integration if in scope". It is: the code ships and every path is reachable.

| Question | Answer |
| --- | --- |
| Shipped? | Yes. `NafathIdentityVerifier` is registered unconditionally (`Infrastructure/Identity/IdentityServiceCollectionExtensions.cs:26-28`) |
| Endpoints reachable? | Always. `POST /sessions/identity-verification-authorization` and `POST /sessions/identity-verification` are anonymous and rate-limited (`SessionsController.cs:117-158`) |
| Flag | `false` in `appsettings.json` and by default. No environment sets it, the authority or `NAFATH_CALLBACK_URL`, so no environment can complete the client today |
| Flag off | No verification token is issued. A token presented anyway is refused before Nafath is called (`AuthenticationService.cs:308-312`). `NafathVerificationTests.WithTheFeatureOffNafathIsNeverCalled` |
| Flag on | Every sign-in path ends in `AdmitAsync` (`AuthenticationService.cs:282-293`). An unverified external user gets a 20-minute verification token and no session. An outage or missing configuration returns 503 and grants nothing |
| Usable in a browser? | No. The SPA has no verification step (`nafath-identity-verification.md` F-4), and with the flag on its shell breaks (NAF-T21) |

## 2. Data flow and trust boundaries

```mermaid
sequenceDiagram
  participant B as External user's browser
  participant A as API (AuthenticationService)
  participant N as Nafath (OIDC)
  participant D as identity_access.user
  B->>A: sign in (password or SSO, MFA if the role needs it)
  A-->>B: verification token (20 min), no session
  B->>A: begin verification (token)
  A-->>B: authorization URL + sealed transaction (state, nonce, PKCE verifier, user binding, 10 min)
  B->>N: approve in the Nafath app
  N-->>B: redirect to NAFATH_CALLBACK_URL ?code&state
  B->>A: complete (token, code, state, transaction)
  A->>N: redeem code (client secret, PKCE verifier)
  N-->>A: ID token (iss, aud, signature, exp, nonce checked; sub present, then dropped)
  A->>D: random reference + time
  A-->>B: session
```

TB-1 is the browser: the code and state travel in its URL. TB-2 is the anonymous API. TB-3 is Nafath's back channel, trusted for signed assertions only. TB-4 is the database; its two Nafath columns have no guard trigger.

What the platform knows after verification: *some* person with a Nafath account approved a login for the platform's client. It does not know which person, and does not compare that person with anyone it expected (NAF-T01).

## 3. Threats

| ID | STRIDE | Threat | L | I | Inherent | Control and evidence | Residual | Status | Finding |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| NAF-T01 | S | Someone with an unverified external user's password completes the onboarding verification with **their own** Nafath identity, gets the session, and leaves the account marked verified for good. External R04 and R08 users need no MFA, and Nafath's `sub` is checked for presence and dropped, never compared with an identity expected for the account | 2 | 3 | High | Verification comes after every platform factor (`AuthenticationService.cs:282-293`), and the transaction is bound to the user (`OpenIdConnectRelyingParty.cs:81`). Nothing binds the Nafath identity to the account (`Infrastructure/Identity/Nafath/NafathIdentityVerifier.cs:78-87`), by the minimisation decision (`nafath-data-minimisation.md` §3). Option B of that record (a hash of `sub` kept afterwards) detects reuse but does not stop a first use: that needs an expected identity captured at onboarding and compared with `sub` | High | DEPENDS | SR-53 |
| NAF-T02 | S | One Nafath identity verifies many platform accounts, e.g. one person at an entity "verifies" colleagues' accounts | 2 | 2 | Medium | None: no subject or hash is kept to detect reuse (OQ-007) | Medium | DEPENDS | SR-57 |
| NAF-T03 | S | Login CSRF or callback injection: a victim's browser completes the victim's verification with the attacker's code and state, or the attacker uses a code from someone else's transaction | 3 | 2 | High | Constant-time `state` check against the sealed transaction, bound to the user id in the verification token, with expiry (`OpenIdConnectRelyingParty.cs:78-86`); completion needs the victim's token in a JSON body. `NafathVerificationTests.AVerificationNafathDidNotGrantIsAnExplicitFailureThatCanBeStartedAgain` (wrong state, tampered transaction, another person's transaction) | Low | MITIGATED | SR-51 |
| NAF-T04 | S | A leaked authorization code (browser history, Referer, proxy logs) is redeemed by someone else | 2 | 3 | High | PKCE S256 with the verifier only in the sealed transaction; a confidential client (`client_secret_basic`); the code alone completes nothing without the transaction and the verification token. `NafathVerificationTests.AnExternalUserGetsNoSessionUntilNafathVerifiesThemAndSignsInWithoutNafathAfterwards` asserts S256 | Low | MITIGATED | SR-51 |
| NAF-T05 | S | A forged or substituted ID token is accepted: wrong issuer or audience, foreign key, expired, algorithm confusion | 1 | 3 | Medium | Issuer, audience, signature, lifetime and nonce validated (`OpenIdConnectRelyingParty.cs:160-166`, `:233-248`, `:115-119`); the token arrives only over the authenticated HTTPS back channel. Not present: a `ValidAlgorithms` pin and negative tests other than the nonce (shared with SSO, AUTH-T08) | Low | PARTIAL | SR-08 |
| NAF-T06 | S | A callback or an ID token is replayed to verify again or mint more sessions | 2 | 2 | Medium | Nonce; the transaction expires after 10 min; once verified, further completions are refused (`AuthenticationService.cs:310`). `SingleSignOnTests.ACodeCanBeRedeemedOnlyOnce`, `.AnExpiredTransactionIsRejected` | Low | MITIGATED | — |
| NAF-T07 | E | The verification token is used as an access or refresh token, or another token is presented at the verification endpoints to obtain a session | 3 | 3 | Critical | A separate audience per token kind, HS256 only (`SessionTokenValidation.cs:19-35`); the bearer accepts the access audience only; the verification endpoints accept the verification audience only (`JwtSessionTokenService.cs:126-136`). `NafathVerificationTests.ANafathVerificationIsNeverASignIn`; `MultiFactorBypassTests.NoEndpointIssuesASessionOrServesAPersonWhoHasNotPassedTheSecondFactor` | Low | MITIGATED | SR-51 |
| NAF-T08 | E | An unverified external user holds or gets a session once the flag is on: another sign-in path, a refresh of an earlier session, a step-up, or an access token still alive | 2 | 3 | High | `AdmitAsync` is the one gate on the password, SSO and MFA paths; refresh refused (`AuthenticationService.cs:104-108`); step-up completion refused (`:160-162`). `NafathVerificationTests.ASessionThatSkippedTheVerificationIsNotRefreshed`. Remaining: an access token already issued lives out its 15 min (`nafath-identity-verification.md` F-8); `BeginStepUpAsync` starts a challenge that completion will refuse (`:139-145`); the step-up refusal is untested | Low | PARTIAL | SR-54 |
| NAF-T09 | E | The Nafath-unavailable path fails open (CTL-10): an outage, a misconfiguration, an exception or a slow provider ends in a session | 2 | 3 | High | Unavailable or not configured is 503 with `Retry-After` and no session (`SessionsController.cs:275-291`); exceptions map to unavailable; a 10 s HTTP timeout. `NafathVerificationTests.ANafathOutageIsAnExplicitRetryStateThatNeitherGrantsNorDeniesAccess` | Low | MITIGATED | SR-52 |
| NAF-T10 | I | The national id or other identity attributes are requested, stored, logged, audited or returned beyond the minimisation decision (CTL-21) | 2 | 3 | High | `scope=openid` only (`Nafath/NafathOptions.cs:23`); `sub` checked and dropped, the reference a fresh GUID; logs carry the user id and the reference; telemetry carries neither. `NafathDataMinimisationTests.OnlyAReferenceAndATimeAreKeptOfAVerification` (every table, the logs and the responses searched). Widening `Identity:Nafath:Scopes` is an unguarded configuration change; the boundary itself is OQ-007's | Low | MITIGATED | SR-52 |
| NAF-T11 | I | The Nafath client secret leaks through the ADM-041 status, the connection test, the logs or a `ToString` | 2 | 3 | High | `Describe` reduces both settings to booleans (`NafathIdentityVerifier.cs:90-98`); `OpenIdConnectClient.ToString` omits the secret; the connection test returns an outcome and a code; secrets read from the store per use; the endpoint needs `IDENTITY_INTEGRATION_MANAGE` and step-up. `IdentityIntegrationEndpointTests.TheStatusShowsNafathsSettingsAndNeitherOfItsSecrets`, `.TheConnectionTestReachesNafathOrSaysWhyNot` | Low | MITIGATED | SR-52 |
| NAF-T12 | S | A hostile endpoint through the configured authority or through endpoints its discovery document supplies: the browser sent to a phishing page, or the client secret posted elsewhere | 1 | 3 | Medium | The authority comes from configuration only; discovery, JWKS and the token endpoint must be HTTPS; the issuer must equal the authority. Not present: `authorization_endpoint` is not checked for HTTPS or the authority's origin (`OpenIdConnectRelyingParty.cs:63-64`); nothing stops `RequireHttpsMetadata=false` or an `http` callback outside Development | Low | PARTIAL | SR-58 |
| NAF-T13 | D | The anonymous Nafath endpoints are flooded: token guessing, Nafath's quota exhausted, the audit and telemetry outboxes filled | 3 | 1 | Medium | `[RateLimited(SignIn)]` on both (`SessionsController.cs:119`, `:139`); every refusal happens before Nafath is called. `RateLimitingTests.EveryAnonymousActionAndEverySensitiveWriteIsRateLimited` | Low | MITIGATED | — |
| NAF-T14 | R | A verified user denies having verified, or an incident needs to know who verified an account, and the platform cannot say; two concurrent completions can lose the second `IdentityVerified` event or answer 500 | 2 | 2 | Medium | `IdentityVerificationRequired`, `IdentityVerified` (with the reference) and `IdentityVerificationFailed` audit events; the correlation id is sent to Nafath. Whether Nafath can trace a verification from client id, time and correlation id is unconfirmed (`nafath-identity-verification.md` F-2); the race is in `AuthenticationService.cs:211-213` | Medium | DEPENDS | SR-57 |
| NAF-T15 | S | An external account is handed to another person (username, e-mail or `directory_subject_id` changed by an administrator), and the new holder inherits the first person's verification | 2 | 2 | Medium | None: `UserAdministrationService.UpdateAsync` resets the mobile verification when the number changes (`:126-129`) but never the Nafath columns (`nafath-identity-verification.md` F-7) | Medium | GAP | SR-59 |
| NAF-T16 | T | An insider or a stolen database credential sets `nafath_verified_at` directly, bypassing Nafath | 1 | 3 | Medium | Database access control only; no guard trigger on the two columns; no reconciliation of references against `IdentityVerified` events | Medium | GAP | SR-60 |
| NAF-T17 | T | A client forges or alters the sealed transaction to choose its nonce, verifier, expiry or binding, or replays an SSO transaction as a Nafath one | 3 | 3 | Critical | AES-256-GCM with a random nonce under a per-flow HKDF key (`AuthorizationTransactionProtector.cs:18-61`); an altered transaction is rejected. `AVerificationNafathDidNotGrant…` (tampered transaction); `ANafathVerificationIsNeverASignIn` (a Nafath callback refused at the SSO endpoint); `SingleSignOnTests.ATamperedTransactionIsRejected` | Low | MITIGATED | SR-51 |
| NAF-T18 | S | The verification token is stolen within its 20 minutes (XSS, a malicious extension, a shared device) and used to complete the verification with the thief's Nafath identity, yielding the victim's session | 2 | 3 | High | `no-store` on the responses; the verification audience opens only the verification; the user is re-read for disabled or suspended state (`AuthenticationService.cs:308-312`); the SPA's CSP. The token is not bound to the sender, and how the SPA will hold it is undesigned (F-4) | Medium | PARTIAL | SR-55 |
| NAF-T19 | E | With the flag off, the still-mapped endpoints are abused: a forged or old verification token reaches Nafath or reveals account state | 3 | 1 | Medium | Refused before any Nafath call (`AuthenticationService.cs:300-319`); a forged token needs `JWT_SIGNING_KEY`; every refusal is one 401. `WithTheFeatureOffNafathIsNeverCalled`; `NafathIsNeverCalledForAnInternalUser` | Low | MITIGATED | — |
| NAF-T20 | S | The user is sent to an attacker's URL, or the code is delivered to an attacker's `redirect_uri` | 2 | 2 | Medium | `redirect_uri` fixed from `NAFATH_CALLBACK_URL`, never from the request (`OpenIdConnectRelyingParty.cs:55`, `:192`); the authorization URL built server-side. Exact-match registration is Nafath's | Low | MITIGATED | — |
| NAF-T21 | D | With the flag on, an unverified external user's browser sign-in stores the verification response as a session; the shell reads `session.user`, which is undefined, and crashes, locking external users out of the SPA | 2 | 2 | Medium | Fails closed (no access token is sent). No control: `src/frontend/src/features/identity-access/session/sessionApi.ts:48-52` treats any 200 without `mfaToken` as a session; `app/AppLayout.tsx:102` | Medium | GAP | SR-56 |
| NAF-T22 | T | Completions refused locally, which never reach Nafath, are recorded as failed Nafath `TOKEN_REDEEM` calls, skewing Nafath's failure rate in FG-05; anyone with a verification token can push it | 2 | 1 | Low | None (`IdentityIntegrationTelemetry` via `ObservedIdentityProviders.cs:60-61`); rate-limited | Low | GAP | SR-61 |

Counts by inherent rating: Critical 2, High 8, Medium 11, Low 1.

## 4. Spec requirements against evidence

| Requirement | Evidence | Gap |
| --- | --- | --- |
| ADR-007, CTL-10: Nafath for external users at onboarding only, never internal sign-in | `NafathIsNeverCalledForAnInternalUser`, `ANafathVerificationIsNeverASignIn` | — |
| CTL-10: the unavailable path neither grants nor silently denies | NAF-T09 | Not run against Nafath's sandbox (F-5) |
| CTL-21, OQ-007: minimum attributes stored, decision written | NAF-T10 | The boundary awaits AHDA Cybersecurity |
| WF-13 §9.2, TBC-EXT-004: identity proofing per approved policy | — | NAF-T01: the proofing does not bind the person to the account (SR-53) |
| ID-token validation: iss, aud, signature, exp, nonce | Nonce tested | SR-08 |
| `nafath-identity-verification.md` D-8: refresh and step-up of an unverified session refused | Refresh tested | Step-up untested, challenge not gated (SR-54) |
| CTL-50: Nafath's operator and processing location named | — | `nafath-identity-verification.md` F-10 |

## 5. Record claims found stale

| Record | Claim | On `dev` |
| --- | --- | --- |
| `nafath-identity-verification.md` §1 | Rate limiting of the endpoints is out of scope (TASK-078) | Shipped by TASK-078 (`SessionsController.cs:119`, `:139`) |
| `nafath-identity-verification.md` header | All four `NAFATH_*` variables are in `environments.json` | Only `NAFATH_CLIENT_ID` and `NAFATH_CLIENT_SECRET`; `NAFATH_CALLBACK_URL`, `NAFATH_APP_ID` and the authority are absent |
| `nafath-identity-verification.md` D-8 | Step-up of an unverified session is refused | Completion is; the challenge is not (NAF-T08) |
| `nafath-identity-verification.md` D-9 | A concurrent second verification changes nothing | It can drop its audit event or answer 500 (NAF-T14) |
| `integration-monitoring.md` D-4 | Only the provider's own refusals are Nafath business failures | Local refusals are recorded too (NAF-T22) |
| `nafath-identity-verification.md` D-5, `nafath-data-minimisation.md` §1 | The binding stops a planted callback; verification proves the account holder is a person Nafath identifies | True, but neither says that a stolen password plus the attacker's own Nafath account passes (NAF-T01) |

## 6. Change log

| Date | Change |
| --- | --- |
| 2026-10-16 | First review (TASK-081) |

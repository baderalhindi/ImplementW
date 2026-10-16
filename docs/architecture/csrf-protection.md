# CSRF Protection for Session-Based Flows

| Field | Value |
| --- | --- |
| Task | TASK-079 — Implement CSRF Protection for Session-Based Flows (P14 - Security Hardening & Compliance) |
| Depends on | TASK-028 (`sso-directory-integration.md` D-3: the platform's own tokens, carried as `Authorization: Bearer`) |
| Record date | 2026-10-16 |
| Status | **BUILT AND VERIFIED LOCALLY**: in process through the real API pipeline (§5.1), by mutation (§5.2), and in Chromium on the deployed shape — one HTTPS origin behind an edge, with a hostile page on another origin of the same site (§5.3) |
| Branch | `sec/task-079-csrf-protection` |
| Directory | `src/backend/PMPlatform.Api/Middleware` |
| Deliverables | **CSRF middleware / token issuance:** `CsrfProtection` — every state-changing request must carry the session's token or a body a browser preflights; the token is the access token `POST /sessions` already issues (D-1, D-2). **Developer guideline note:** §7 of this record, the eighth item of the PR security checklist, and R-46 in `api-conventions.md` |
| Environment variables / secrets | None (as the workbook row says) |
| Gate decision applied | None in the workbook row |
| Control | CTL-13 (`cybersecurity-control-matrix.md`); OWASP ASVS 4.0 V4.2.2 |
| Sources read | The TASK-079 row as supplied on 2026-10-16, checked against the workbook (`AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`): description, acceptance criteria, deliverables, branch, variables and the validation check. `sso-directory-integration.md` D-3 and §4 (the session model, the anonymous session endpoints); `api-conventions.md` R-23, R-40, R-46; `endpoint-hardening.md` D-9, D-12 (the origin allowlist, the CORS policy without credentials); `nafath-identity-verification.md` D-5 (login CSRF on the OIDC redirect); `src/frontend/src/shared/api/httpClient.ts` and `sessionStore.ts` (what the SPA sends and where it keeps the session) |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | CSRF protection on every state-changing API endpoint a browser session can reach — 252 method/route pairs today, and every one added later, without the developer opting in | This task |
| **In** | The strategy written down so a new endpoint follows it by default (§7) | This task |
| **Out** | CSRF on the OIDC redirect (SSO and Nafath): already covered by `state` and the sealed, user-bound transaction | TASK-028, TASK-068 (`nafath-identity-verification.md` D-5) |
| **Out** | Token theft by script injection (XSS). A header-borne token is out of a forger's reach, not out of a script's; that is the CSP's and output encoding's job | TASK-077, TASK-078 |
| **Out** | Revoking a session before it expires | `sso-directory-integration.md` F-3 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **The CSRF token is the session's access token**, issued by `POST /api/v1/sessions` (and the MFA, SSO, Nafath, refresh and step-up exchanges) and sent by the SPA as `Authorization: Bearer` on every authenticated call. No separate anti-forgery token, no double-submit cookie, no `SameSite` cookie | The workbook asks for a strategy "consistent with the chosen session/token model". That model (TASK-028 D-3, R-46) has no cookie: the SPA holds the token in `sessionStorage` and attaches it itself. CSRF works by making the browser attach a credential on its own — a cookie, cached `Basic` or `Negotiate` credentials, a client certificate — and this session is none of those. A double-submit or `SameSite` scheme protects a cookie session; adding one here would add a cookie in order to protect it. This is OWASP's "token-based" pattern (the session token in a custom header), the strongest of the three |
| D-2 | **One middleware enforces it for every request, not an attribute per endpoint.** `CsrfProtection` refuses a request with an unsafe method — anything but GET, HEAD, OPTIONS, TRACE — unless it carries `Authorization: Bearer …`, or a `Content-Type` that is not one of the three a page may send cross-origin without a preflight (`application/x-www-form-urlencoded`, `multipart/form-data`, `text/plain`). The refusal is 403 `CROSS_SITE_REQUEST_REFUSED`, before authentication | "So future endpoints follow it by default rather than each developer re-deciding." An endpoint cannot be forgotten, and there is no exemption to reach for. A new method (PATCH, WebDAV) is checked because the list is of safe methods, not unsafe ones |
| D-3 | **The rule is "could a page on another origin have sent this without the API's consent?"** A request with the bearer header or a JSON body cannot: the browser preflights it, and `OriginAllowlist` refuses every origin but the platform's (TASK-078 D-9). A request with neither — a form post, a no-cors `fetch`, a bodyless POST — can, so it is refused even though the token check after it would also refuse it | The token alone already stops a forged request on an authenticated endpoint (no token, 401). The middleware adds what the token cannot: it holds when a developer adds an anonymous write, binds a form, or reads a credential from anywhere a browser fills in by itself. It also does not depend on the browser sending `Origin` |
| D-4 | **Only `Bearer` counts; `Basic`, `Negotiate` and any other scheme do not** | Browsers attach cached `Basic` credentials and, on an intranet, Kerberos `Negotiate` tokens without the page asking. A bearer token is never attached by a browser |
| D-5 | **The anonymous session endpoints need no exemption.** Sign-in, MFA, SSO, Nafath, refresh and step-up take their credential in a JSON body; a JSON body is preflighted; the preflight from a foreign origin is refused | The ten anonymous endpoints already bind JSON records. A forged sign-in could not plant a session anyway: the token comes back in the body to the page that asked, and a hostile page cannot read it (TASK-078 D-12: no credentials in the CORS policy) |
| D-6 | **A `Content-Type` that does not parse is forgeable** | If a browser had thought it safelisted it would not have preflighted; if not, it would have. Refusing it costs a legitimate client nothing |
| D-7 | **`SameSite` and Fetch Metadata are not relied on.** The deployed SPA and API share one origin, and any sibling host of AHDA's domain is the same *site*: in the live check the hostile page on `evil.ahda.test` was sent with `Sec-Fetch-Site: same-site` to `pmplatform.ahda.test` (§5.3). A same-site defence would have let it through | The origin allowlist compares the full origin; the token is not attached by any browser. Neither depends on where the site boundary falls |
| D-8 | **The session stays out of cookies, and a test says so.** The API registers the bearer scheme only and sets no cookie at sign-in, at the second factor or at refresh; `TheSessionIsNeverACookie` fails otherwise | D-1 holds only while this does. Moving the refresh token into an HttpOnly cookie — a common hardening proposal — would make the refresh endpoint forgeable by form (it would then need its own token) and must go through §7 rule 5 first |

## 3. As built

| Piece | Where |
| --- | --- |
| `CsrfProtection` (middleware and `IsForgeable`) | `src/backend/PMPlatform.Api/Middleware/CsrfProtection.cs` |
| `CROSS_SITE_REQUEST_REFUSED` | `src/backend/PMPlatform.Api/Errors/ErrorCodes.cs` |
| Pipeline order: forwarded headers → security headers → correlation id → exception handler → HTTPS redirection → origin allowlist → CORS → **CSRF protection** → authentication → rate limiting → authorization → step-up | `src/backend/PMPlatform.Api/Program.cs` |
| Tests | `src/backend/PMPlatform.Tests.Integration/EndpointHardening/CsrfProtectionTests.cs`, run by CI's `backend-endpoint-hardening` job (its filter is the namespace) |

After the origin allowlist, so a forged request from a foreign origin is still answered `ORIGIN_NOT_ALLOWED`; before authentication, so a forged request never reaches the token check, the audit of a rejected token, the rate limiter or an action. Nothing in the SPA changed: it already sends the bearer token on every authenticated call and `Content-Type: application/json` with every body (`httpClient.ts`), and its uploads send the bearer token with the multipart body.

## 4. API

| Status | `code` | When |
| --- | --- | --- |
| 403 | `CROSS_SITE_REQUEST_REFUSED` | **New.** A request with an unsafe method carries neither `Authorization: Bearer` nor a body a browser would preflight; nothing ran |

In the R-23 envelope and in `api-conventions.md` §4.4. A new error code is non-breaking (R-10); no operation's request or response schema changed, so `docs/api/openapi.v1.json` is unchanged. A client that is not a browser is affected only if it sent a write with no bearer token and no JSON body, which no endpoint accepted: such a request was a 401 or a 400/415 before, and is a 403 now.

## 5. Verification

### 5.1 Tests

| Suite | What it proves | Result |
| --- | --- | --- |
| `CsrfProtectionTests.AReplayedStateChangingRequestFromAnotherOriginWithoutTheTokenIsRejected` | **The acceptance criterion.** An administrator's "disable this user" is replayed as a hostile page could send it: a form from the hostile origin (403 `ORIGIN_NOT_ALLOWED`), the same with `Origin` withheld (403 `CROSS_SITE_REQUEST_REFUSED`), a text/plain body (403 `CROSS_SITE_REQUEST_REFUSED`), with `Basic` credentials a browser attaches itself (403 `CROSS_SITE_REQUEST_REFUSED`), the preflight of a JSON fetch (403 `ORIGIN_NOT_ALLOWED`), and with a made-up bearer token (401). The user is still ACTIVE. The same request from the SPA's origin with the session's token: 200, user DISABLED | passed |
| `CsrfProtectionTests.EveryStateChangingEndpointRefusesAForgedRequest` | Each of the API's 252 state-changing method/route pairs refuses a forged form post with 403 `CROSS_SITE_REQUEST_REFUSED` | passed |
| `CsrfProtectionTests.ARequestIsForgeableUnlessItCarriesTheTokenOrAPreflightedBody` (22 cases) | The rule, shape by shape: unsafe methods including PATCH and an unknown one; no body, form, multipart, text/plain in any case, an unparseable type; `Basic`, `Negotiate`, a bare `Bearer`; JSON, `application/problem+json`, a bearer token in either case, a bearer upload; GET, HEAD, OPTIONS | 22 passed |
| `CsrfProtectionTests.TheSessionIsNeverACookie` | The only authentication scheme is `Bearer`; sign-in, the second factor and refresh set no cookie | passed |
| `EndpointHardening` namespace, with `RATE_LIMIT_TEST_REDIS` | TASK-078's 42 unchanged beside the 25 above | 67 passed |
| Full backend unit suite | No regression | 1,911 passed |
| Full integration suite, Persistence excluded | No existing test sent a forgeable write and expected it to run | 801 of 802 passed; the one failure was `SharedRateLimitStoreTests`, which needs `RATE_LIMIT_TEST_REDIS` and passed in the row above with it. Persistence was left out: it starts no API, and on this machine it times out on parallel throwaway databases whatever the branch (recorded since TASK-063) |

### 5.2 Mutation tests

Each mutant was compiled into the API and `CsrfProtectionTests` run against it; a mutant is killed when a test fails. The unmutated tree passed first (`artifacts/task-079/mutations/run.py`, not committed).

**8 of 8 killed.** The harness records only killed or survived per mutant; the right-hand column names the tests that assert the mutated behaviour, not a per-test log.

| # | Mutant | Asserted by |
| --- | --- | --- |
| M1 | The middleware not registered | `AReplayedStateChanging…` (the Origin-less replays answer 401, not 403), `EveryStateChangingEndpoint…` |
| M2 | It runs before the origin allowlist | `AReplayedStateChanging…` (a hostile form answers `CROSS_SITE_REQUEST_REFUSED`, not `ORIGIN_NOT_ALLOWED`) |
| M3 | Any `Authorization` scheme counts as the token | `ARequestIsForgeable…` (`Basic`, `Negotiate`), `AReplayedStateChanging…` (the `Basic` replay) |
| M4 | A bare `Bearer` with no token counts | `ARequestIsForgeable…(POST, "Bearer")` |
| M5 | `text/plain` not safelisted | `ARequestIsForgeable…` (three text/plain cases), `AReplayedStateChanging…` (the no-cors replay) |
| M6 | No `Content-Type` counts as preflighted | `ARequestIsForgeable…` (bodyless cases), `AReplayedStateChanging…` (the `Basic` replay) |
| M7 | DELETE treated as a safe method | `ARequestIsForgeable…(DELETE, …)`, `EveryStateChangingEndpoint…` |
| M8 | The media type compared case-sensitively | `ARequestIsForgeable…(POST, "TEXT/PLAIN")` |

### 5.3 The workbook's validation check, in Chromium on the deployed shape

"Replay a captured POST request from a cross-origin test harness without the CSRF token and confirm rejection; confirm a legitimate same-origin request with the token succeeds."

The stack: the API image built from this branch on a fresh, migrated and seeded database (`task079_live`), behind a Caddy edge serving one HTTPS origin, `https://pmplatform.ahda.test` — `/api` to the API, everything else to the SPA's production build — with `CORS_ALLOWED_ORIGINS` set to that origin, as Terraform sets it. The same edge served the hostile page at `https://evil.ahda.test`. An MFA stand-in let local.r01 sign in. A dev server was not used: Vite answers CORS preflights from any localhost origin itself, before its proxy, so the API would not have seen them (`artifacts/task-079/live`: reset.sh, Caddyfile, check.py, check079.mjs; not committed). Run on 2026-10-16: **19 of 19 checks passed.**

| # | Step | Result |
| --- | --- | --- |
| 1 | **The legitimate same-origin request with the token.** local.r01 signs in to the SPA (password, then the second factor), opens ADM-003 for local.r08 and confirms "Disable user". The request is captured | `POST https://pmplatform.ahda.test/api/v1/users/{r08}/disable` with `Authorization: Bearer`, `Idempotency-Key`, `If-Match`, `Origin: https://pmplatform.ahda.test`: **200**, local.r08 DISABLED. local.r08 is then made ACTIVE again by SQL |
| 2 | **Replayed from the cross-origin harness** — the page on `https://evil.ahda.test`, in the same browser, while local.r01's session is open in the SPA | HTML forms, url-encoded, multipart and text/plain: each **403 `ORIGIN_NOT_ALLOWED`**, sent with `Origin: https://evil.ahda.test` and `Sec-Fetch-Site: same-site`. A no-cors `fetch` with credentials: **403** (the page sees only a network error; the API's CORP header withholds even the opaque response). A CORS `fetch` with the captured headers minus the token, and with a made-up token: the **preflight answered 403**, so Chromium never sent the POST |
| 3 | **The same request over HTTP through the edge**, in shapes a current browser does not produce (no `Origin`) and with credentials a browser attaches itself | Form, text/plain, and `Basic` with no body: each **403 `CROSS_SITE_REQUEST_REFUSED`**. The captured headers minus the token: **403 `CROSS_SITE_REQUEST_REFUSED`**. With a made-up bearer token: **401 `AUTHENTICATION_REQUIRED`** |
| 4 | Effect | local.r08 **ACTIVE** after every replay: nothing a replay sent was executed |

Row 2 is refused by the origin allowlist, because a browser always sends `Origin` on a cross-origin write; row 3 is what `CsrfProtection` adds, for a client that does not.

## 6. Acceptance criteria and validation check

| Criterion / check | Evidence | Verdict |
| --- | --- | --- |
| An automated test replaying a captured state-changing request from a different origin without a valid CSRF token is rejected | `AReplayedStateChangingRequestFromAnotherOriginWithoutTheTokenIsRejected` (CI, every PR); `EveryStateChangingEndpointRefusesAForgedRequest`; §5.3 | **Met** |
| The chosen strategy is documented so future endpoints follow it by default rather than each developer re-deciding | D-1–D-8; §7; the middleware applies to every endpoint with no opt-in and no exemption (D-2); `api-conventions.md` R-46 and §4.4; the PR security checklist's eighth item | **Met** |
| Validation check: replay a captured POST from a cross-origin harness without the CSRF token and confirm rejection; confirm a legitimate same-origin request with the token succeeds | §5.3, rows 1–4 | **Met** (locally, on the deployed shape) |
| CTL-13 verification: "Replayed cross-origin POST without the token is rejected; same-origin request with the token succeeds" | Same | **Met** |

## 7. Rules for new endpoints — the developer guideline

The platform's CSRF defence is that **no browser ever sends the session on its own**. The session is the access token, the SPA puts it in `Authorization: Bearer` itself, and `CsrfProtection` refuses every write that arrives without either that header or a JSON body. You get this for free on every endpoint; the rules below are the ways to lose it.

1. **Never change state on GET or HEAD.** Safe methods are not checked, and a page can make a browser send them with an `<img>` tag. Reads only (R-40).
2. **Never read a credential from a cookie or a query string, or authenticate any other way than the bearer scheme.** No `AddCookie`, no `JwtBearerEvents.OnMessageReceived` reading a cookie or `?access_token=`, no `Basic`/`Negotiate`/client-certificate scheme. `TheSessionIsNeverACookie` fails if a second scheme is registered or a cookie is set at sign-in (R-46).
3. **Take a body as JSON.** A `[FromForm]` or text endpoint without the bearer token is refused, by design. A file upload is multipart *with* the bearer token, as `DocumentsController` is.
4. **Do not exempt an endpoint from `CsrfProtection`, and do not move it after authentication or before the origin allowlist.** There is no exemption attribute. A webhook or server-to-server caller authenticates with a bearer token too.
5. **Changing the session model reopens this record.** A cookie of any kind — an HttpOnly refresh token, a "remember me", a BFF session — makes the browser an ambient credential carrier. Before it lands: a per-session anti-forgery token the SPA sends in a header and the API compares (synchronizer or signed double-submit), `SameSite=Strict`, the CORS policy still without credentials, and §5's tests extended to the cookie's endpoints. `SameSite` alone is not enough: a sibling host of AHDA's domain is the same site (D-7).
6. **Keep `CORS_ALLOWED_ORIGINS` to the platform's own origin.** Every allowed origin can send the SPA's preflighted requests; the allowlist is half of this defence (TASK-078 D-10).

A reviewer ticks the PR checklist's CSRF item only when the diff keeps 1–4 (`.github/PULL_REQUEST_TEMPLATE.md`).

## 8. Findings

| # | Finding | Owner | Status |
| --- | --- | --- | --- |
| F-1 | **The workbook names a double-submit or `SameSite` cookie strategy; neither is used**, because the session has no cookie (D-1). The row also says "consistent with the chosen session/token model", which is what the bearer-token strategy is. The Security Lead should accept D-1 as the CTL-13 strategy (CTL-43 asks for their review of authentication changes), or say which cookie the platform should have | Security Lead | Open |
| F-2 | **The token sits in `sessionStorage`, readable by any script on the SPA's origin.** That is the trade the bearer model makes: no CSRF, at the price of XSS reaching the token. It is held by the SPA's CSP (`script-src 'self'`, TASK-078 §3.2) and TASK-077's sink lint and encoding; it is recorded here so a future change to either is weighed against it | Frontend lead, Security Lead | Open (accepted risk, monitored through TASK-077/078) |
| F-3 | **The validation check ran locally**, on the deployed shape (§5.3); no SIT exists yet (`endpoint-hardening.md` F-1). On SIT the replay's row 2 is the same request from any other origin to `APP_BASE_URL`; nothing environment-specific is configured for CSRF | DevOps/Platform Lead (TASK-018) | Open |

## 9. Change log

| Date | Change |
| --- | --- |
| 2026-10-16 | Built and verified (this record) |

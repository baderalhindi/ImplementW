# Rate Limiting, CORS & Secure HTTP Headers

| Field | Value |
| --- | --- |
| Task | TASK-078 — Implement Rate Limiting, CORS & Secure HTTP Headers (P14 - Security Hardening & Compliance) |
| Depends on | TASK-009 (`api-conventions.md`: R-23's envelope, R-48's 429 contract, R-35's sensitive writes) |
| Record date | 2026-10-14 |
| Status | **BUILT AND VERIFIED LOCALLY**: in process through the real API pipeline (one API, and two sharing a Redis), on the DEV compose stack built from this branch, by the MDN HTTP Observatory's own scanner, and in Chromium (§5). The workbook's checks name SIT, which does not exist yet (F-1) |
| Branch | `sec/task-078-rate-limiting-cors-secure-headers` |
| Directory | `src/backend/PMPlatform.Api/Middleware` |
| Deliverables | **Rate-limiting middleware:** `RateLimiting` with `RateLimitPolicy`/`[RateLimited]`, over `IRateLimitStore` (Redis shared by every instance, or in memory). **CORS policy configuration:** `OriginAllowlist` — the allowlist from `CORS_ALLOWED_ORIGINS`, the CORS policy, and the refusal of every other origin. **Secure-headers middleware:** `SecurityHeaders`. Also the forwarded client address, the SPA's own header set, `CORS_ALLOWED_ORIGINS` in Terraform, the SPA's throttling message, and the CI job that runs it all |
| Environment variables / secrets | `RATE_LIMIT_STORE_CONNECTION_STRING` (Secret, SIT/UAT/PROD), `CORS_ALLOWED_ORIGINS` (Public, DEV/SIT/UAT/PROD) |
| Gate decision applied | None in the workbook row |
| Control | CTL-12 (`cybersecurity-control-matrix.md`); OWASP ASVS 4.0 V2.2.1, V14.4, V14.5 |
| Sources read | The TASK-078 row as supplied on 2026-10-14, checked against the workbook (`AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`): description, acceptance criteria, deliverables, branch, variables and validation checks; the Environment and Secrets rows of both variables, including their verification methods. `api-conventions.md` R-10, R-23, R-35, R-48; `environment-separation.md` D-1; `network-security.md` §3.3, §3.4, N-F1, N-F4; `auth-access-audit-logging.md` F-5; `sso-directory-integration.md` F-4; `mfa-privileged-access.md` F-4; `input-validation.md` F-1; `secret-rotation-runbook.md` (class B) |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | Per-client rate limiting on authentication and the other sensitive endpoints, with the counters shared across API instances | This task |
| **In** | CORS restricted to the approved AHDA origins | This task |
| **In** | The security response headers: the workbook's five (Content-Security-Policy, X-Content-Type-Options, X-Frame-Options, Strict-Transport-Security, Referrer-Policy) and the four TASK-077's ZAP scan reported missing | This task |
| **In** | The client address behind the load balancer, without which "per client" means "everyone" (§2 D-4) | This task (closes `auth-access-audit-logging.md` F-5) |
| **Out** | Provisioning the Redis instance the shared counters live in | DevOps/Platform Lead (F-2) |
| **Out** | Serving the SPA in a deployed environment — and so applying the SPA's header set there | TASK-018 / TASK-021 (F-3) |
| **Out** | Rate limiting at the edge (Cloud Armor) | `network-security.md` N-F4, revisit after go-live |
| **Out** | CSRF | TASK-079 (CTL-13) |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **The middleware is the platform's own**, not ASP.NET Core's `RateLimiter`. One middleware reads the endpoint's policy, counts the request in `IRateLimitStore`, and answers 429 past the limit | The built-in limiters count in process only. A distributed one needs a third-party package or a `RateLimiter` subclass whose synchronous `AttemptAcquire` cannot reach Redis. The middleware is 60 lines; the deliverable names a middleware |
| D-2 | **Three policies, each its own counter**: `SignIn` — every endpoint that verifies, or starts verifying, a credential or second factor (password, SSO, MFA challenge and code, Nafath, step-up); `SessionRefresh` — the refresh-token exchange; `SensitiveWrite` — every `[SensitiveWrite]` action (R-35, 234 operations). An action is under a policy by `[RateLimited(…)]`, or by being a sensitive write. A test fails if any anonymous action is under no policy | "Authentication and other sensitive endpoints". The refresh is counted apart because every open SPA tab calls it once per access-token lifetime: in one counter with sign-in, a busy office would use up its own sign-in attempts. A sensitive write is the API's own definition of an operation that changes business, financial, identity or access state |
| D-3 | **The client is the signed-in user, or for an anonymous caller the client address.** The counter key is `<policy>:user:<id>` or `<policy>:address:<ip>` | Sign-in steps are anonymous, so the address is all there is. A sensitive write has a user, and counting by user keeps one user's script from throttling colleagues behind the same corporate egress address (N-F4) |
| D-4 | **The client address is the one the load balancer forwarded.** `ClientAddress:TrustedProxyHops` is 2 (appsettings.json): the regional load balancer appends `<client>, <load balancer>` to X-Forwarded-For, and the API takes the address before those two; anything a client wrote further left is ignored. Every proxy is trusted for those hops because Cloud Run accepts traffic from that load balancer only (NS-8). Development is 0: the TCP peer | Behind the load balancer the TCP peer is Google's front end, so without this every caller would share one counter and the first burst would throttle everyone. The hop count is Google's documented header format; it is confirmed on SIT (F-1). The audit trail's `client_address` reads the same address (closes `auth-access-audit-logging.md` F-5) |
| D-5 | **A fixed window that opens at the client's first request**: the counter expires `Window` after it; the request past `PermitLimit` gets 429 `RATE_LIMITED`, `Retry-After` set to the seconds until it expires, rounded up, at least 1 (R-48). Throttled requests are still counted. A refused request never reaches the action — a throttled sign-in does not touch the directory | Clock-aligned windows let a burst straddling the boundary through twice. A window anchored at the first request is one Redis `INCR`+`PEXPIRE`, atomic in one script |
| D-6 | **Shipped limits: `SignIn` 30, `SessionRefresh` 120, `SensitiveWrite` 120, each per minute.** In appsettings.json, overridable per environment like any setting; a missing or non-positive limit stops the API at start-up | Thresholds are delivery-team engineering values (control matrix CTL-12, TASK-004 §7). A sign-in with MFA is three requests, so 30 is ten complete sign-ins a minute from one address: generous for an office, slow for guessing (each failure also takes the 1 s floor, `sso-directory-integration.md`). 120 refreshes is 1,800 users a minute behind one address at the 15-minute token lifetime. 120 writes is two a second for one person, which no screen sends. Revisit with PROD logs (F-4) |
| D-7 | **The counters live in Redis when `RATE_LIMIT_STORE_CONNECTION_STRING` is set**, and in each instance's memory when it is not (DEV, local). The store is read once at start-up | The sheet scopes the variable to SIT/UAT/PROD and verifies it with "throttling behaves consistently across multiple API instances". Without it, N instances let a client through N times the limit |
| D-8 | **A store outage lets requests through, logged as a warning naming the exception type only.** Commands fail at once instead of queueing (`BacklogPolicy.FailFast`) | The rotation runbook's class B: "throttling degrades rather than fails". Failing closed would turn a Redis outage into a sign-in outage. The type only, because the client's message can name the store's endpoint (CTL-27) |
| D-9 | **A request whose `Origin` is not on the allowlist is refused with 403 `ORIGIN_NOT_ALLOWED`** before anything runs, preflight or not; a request without `Origin` is not affected | CORS alone only hides the response from the page — a form-encoded sign-in posted by a hostile page would still run, letting it guess passwords through its visitors' browsers. Requests without `Origin` (curl, servers, same-origin reads) are not cross-origin browser requests. A new error code is non-breaking (R-10) |
| D-10 | **The allowlist is the environment's one origin.** Terraform sets `CORS_ALLOWED_ORIGINS` to `APP_BASE_URL` (`infra/terraform/platform/main.tf`); compose sets `http://localhost:5173` | Under TASK-016 D-1 the SPA and API share one origin, and browsers send `Origin` on same-origin writes too, so that origin must be listed |
| D-11 | **Each entry must be `scheme://host[:port]` and nothing more**, compared as browsers serialise `Origin` (lower case, default port dropped); `https` only outside Development. A wildcard, path, query, user info or a bare host stops the API at start-up | A malformed entry must not quietly widen or empty the policy |
| D-12 | **The CORS policy itself**: the allowed origins; GET, POST, PUT, PATCH, DELETE; request headers `Authorization`, `Content-Type`, `If-Match`, `Idempotency-Key`, `X-Correlation-Id`; exposed `ETag`, `Location`, `Retry-After`, `Content-Disposition`, `X-Correlation-Id`; preflight cached 10 minutes; **no credentials** | Exactly what the SPA sends (`httpClient.ts`) and reads. The API authenticates by bearer token, never by cookie |
| D-13 | **Every API response carries the headers**, set as the response starts — after anything that cleared the response, so the exception handler's 500, the framework's 404 and the hardening refusals carry them too. Kestrel's `Server` header is off | A header set only on success would leave the error pages, which scanners request first, unprotected |
| D-14 | **The API's CSP allows nothing**: `default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'`. **The SPA has its own** (§3.2) | The API serves JSON and files, never a page |
| D-15 | **HSTS is `max-age=31536000; includeSubDomains`, sent whatever the scheme the API sees; no `preload`** | Browsers ignore it over plain HTTP, and behind the load balancer the API sees HTTP. `preload` is a commitment for AHDA's domain, which is not yet named (`network-security.md` N-F1) |
| D-16 | **The SPA shows its own message for `RATE_LIMITED`** ("…Wait a minute, then try again", English and Arabic) | Without it a throttled person read "Something went wrong" |

## 3. As built

### 3.1 Backend

| Piece | Where |
| --- | --- |
| `RateLimiting` (middleware), `RateLimitPolicy`, `[RateLimited]`, `RateLimitingOptions` | `PMPlatform.Api/Middleware/RateLimiting.cs`, `RateLimitPolicy.cs` |
| `OriginAllowlist` (middleware, `Parse`, `Policy`), `AllowedOrigins` | `PMPlatform.Api/Middleware/OriginAllowlist.cs` |
| `SecurityHeaders` (middleware and the header list) | `PMPlatform.Api/Middleware/SecurityHeaders.cs` |
| `IRateLimitStore`, `RateLimitCount` | `PMPlatform.Application/Common/RateLimiting/IRateLimitStore.cs` |
| `RedisRateLimitStore` (StackExchange.Redis 3.3.1), `InMemoryRateLimitStore` | `PMPlatform.Infrastructure/RateLimiting/` |
| Pipeline order: forwarded headers → security headers → correlation id → exception handler → HTTPS redirection → origin allowlist → CORS → authentication → **rate limiting** → authorization → step-up | `PMPlatform.Api/Program.cs` |
| `[RateLimited]` on the ten anonymous session endpoints | `SessionsController.cs` |
| `RATE_LIMIT_STORE_CONNECTION_STRING` an optional secret | `ApplicationSecrets.OptionalKeys` |

The rate limiter runs after authentication, so it knows the user, and before authorization, so refused requests count too.

### 3.2 The SPA's header set

The SPA's production build is served with the same nine headers, but its CSP allows its own origin: `default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'`. The bundle has no inline script or style tag (React sets `style` through the DOM, which CSP allows). The set is in `src/frontend/vite.config.ts`, applied to `vite preview`, which served the build for the scans. The deployed host must serve the same set (F-3). The dev server sets none, because Vite's hot reload injects inline scripts.

### 3.3 Configuration

| Setting | Shipped | Development | Tests |
| --- | --- | --- | --- |
| `RateLimiting:Policies:{SignIn,SessionRefresh,SensitiveWrite,ClientErrorReport}` | 30 / 120 / 120 / 30 per `00:01:00` (`ClientErrorReport` since TASK-090: the SPA's anonymous error report, `apm-error-tracking.md` D-7) | same | 1,000,000 (`IdentityApiFactory.UnreachableRateLimits`), so no suite throttles itself; the rate-limiting tests set their own |
| `ClientAddress:TrustedProxyHops` | 2 | 0 | 2 in the hardening tests |
| `CORS_ALLOWED_ORIGINS` | `APP_BASE_URL` (Terraform) | `http://localhost:5173` (compose) | empty, or the test's origin |
| `RATE_LIMIT_STORE_CONNECTION_STRING` | secret store, SIT/UAT/PROD | empty → in memory | `RATE_LIMIT_TEST_REDIS` for the shared-store tests only |

## 4. API

| Status | `code` | When |
| --- | --- | --- |
| 429 | `RATE_LIMITED` | The client is past its policy's limit in the current window; `Retry-After` in whole seconds (R-48, unchanged) |
| 403 | `ORIGIN_NOT_ALLOWED` | **New.** The request carries an `Origin` not on `CORS_ALLOWED_ORIGINS`; nothing ran |

Both are in the R-23 envelope and in `api-conventions.md` §4.4. No operation's request or response schema changed, so `docs/api/openapi.v1.json` is unchanged.

## 5. Verification

### 5.1 Tests

| Suite | What it proves | Result |
| --- | --- | --- |
| `EndpointHardening.RateLimitingTests` (14) | A sign-in burst gets 401 five times, then 429 even with the right password, in the R-23 envelope with `Retry-After` 1–60 s, while another address is unaffected and the API logs it once; a spoofed X-Forwarded-For does not escape; every sign-in step shares one counter; the window reopens after it closes; the refresh is counted apart; sensitive writes are counted per user across addresses, not per address; every anonymous action and every sensitive write is under its policy (10 + 234); an incomplete policy stops the API; Retry-After rounding | 14 passed |
| `EndpointHardening.SharedRateLimitStoreTests` (2) | **The sheet's verification method**: two API instances sharing one Redis throttle an alternating burst at the limit (4 × 401, 4 × 429), not at twice it; an unreachable store lets requests through and logs neither the store's host nor its password | 2 passed (Redis 7) |
| `EndpointHardening.OriginAllowlistTests` (24) | Preflights from five non-listed origins (another host, a suffix trick, `http`, another port, `null`) are refused with no `Access-Control-*`; a simple request from one is not executed; the listed origin's preflight gets exactly the policy and its request runs, with the exposed headers; a request without `Origin` is unaffected; parsing and its refusals; an invalid allowlist stops the API | 24 passed |
| `EndpointHardening.SecurityHeadersTests` (2) | 200, 401, 404, 403 `ORIGIN_NOT_ALLOWED` and 429 each carry all nine headers with their exact values and no `Server` header; the workbook's five have passing values | 2 passed |
| `InMemoryRateLimitStoreTests` (4, unit) | Count and remaining time; separate keys; the window closes exactly at `Window`; 500 concurrent increments counted once each | 4 passed |
| `access.test.tsx` (frontend) | A 429 at sign-in shows the throttling message | passed |
| Full backend unit suite | No regression | 1,911 passed |
| Full integration suite (local) | No regression with the middleware in every test API | 914 of 915 passed (before the last allowlist test was added); the one failure was `RegisterQueryPlanTests` (SCR-081), which passed 75/75 on its own — the planner flake recorded since TASK-057 |

CI runs the 42 integration tests in a new job, `backend-endpoint-hardening`, with a Redis service container. `backend` and `backend-risk-governance` each ran within 90 s of their 10-minute cap on `dev` (`ci-quality-gates.md` F-4).

### 5.2 Mutation tests

Each mutant was compiled into the API, and the EndpointHardening integration tests and `InMemoryRateLimitStoreTests` were run against it; a mutant is killed when a test fails (`artifacts/task-078/mutations/run.py`, not committed). **11 of 11 killed.** The harness records only killed or survived per mutant; the right-hand column names the tests that assert the mutated behaviour, not a per-test log.

| # | Mutant | Asserted by |
| --- | --- | --- |
| M1 | The limit is off by one (`<= PermitLimit + 1`) | The burst, spoofing, sign-in step, window, refresh, per-user, shared-store and header tests |
| M2 | The limit is ignored | Every throttling test |
| M3 | `Retry-After` may be 0 (floor, minimum 0) | `RetryAfterIsTheWindowsRemainderInWholeSeconds` |
| M4 | Signed-in users counted by address | `SensitiveWritesAreCountedPerUser` |
| M5 | Sensitive writes under no policy | `EveryAnonymousActionAndEverySensitiveWrite…`, `SensitiveWritesAreCountedPerUser` |
| M6 | A foreign `Origin` let through | `APreflightFromAnOriginNotListed…` (5 cases), `ARequestFromAnOriginNotListed…`, `EveryKindOfResponse…` |
| M7 | `http` origins allowed outside Development | `AnEntryThatIsNotAnHttpsOrigin…(http://…)` |
| M8 | HSTS dropped from the header list | `EveryKindOfResponse…`, `TheWorkbooksFiveHeaders…` |
| M9 | A store outage fails the request | `AnUnreachableStoreLetsRequestsThrough…` |
| M10 | The window lasts one tick longer | `TheNextRequestAfterTheWindowClosesOpensANewOne` |
| M11 | One more proxy hop trusted | `ASpoofedForwardedForDoesNotEscapeTheLimit`, the per-address tests |

### 5.3 Checks on the running stack

All against the API image built from this branch, on a scratch database (`task078_live`), with the shipped limits (`artifacts/task-078/live`, not committed).

| # | Check | Result |
| --- | --- | --- |
| 1 | **Criterion 1 — a scripted login burst** (`check.py`): 35 wrong-password sign-ins from one client | **Attempts 1–30: 401; 31–35: 429** `RATE_LIMITED`, `application/problem+json`, `Retry-After: 30`. The right password from the same client: 429. The burst took 30.5 s (the 1 s failure floor). Repeated with the counters in Redis (`RATE_LIMIT_STORE_CONNECTION_STRING=redis078:6379`): same result, key `pmplatform:rate-limit:SignIn:address:::ffff:192.168.65.1`, count 37, expiring at the window's end |
| 2 | **Criterion 2 — a non-allowlisted Origin** | Preflight from `https://evil.example`: **403 `ORIGIN_NOT_ALLOWED`**, no `Access-Control-Allow-Origin`; a simple POST from it: 403, not executed. Preflight from the allowlisted `http://localhost:5173`: 204 with the policy |
| 3 | **Criterion 3 — header scan, "Mozilla Observatory equivalent"**: the MDN HTTP Observatory's own scanner (`@mdn/mdn-http-observatory` 1.7.1, the engine behind developer.mozilla.org/observatory), run against the deployed shape — one HTTPS origin, `pmplatform.ahda.test`, behind a Caddy edge that routes `/api` to the API and the rest to the SPA's production build, port 443 only — and against the API alone (`pmplatform-api.ahda.test`) | **Both: 11 of 12 tests pass, no header missing.** CSP (no unsafe), X-Content-Type-Options, X-Frame-Options (via CSP), Referrer-Policy, COOP, COEP, CORP, cookies (none), CORS (no `*`), redirection (no HTTP listener), SRI. The 12th, `strict-transport-security`, reports `hsts-invalid-cert`: the header is present (`max-age=31536000; includeSubDomains`), but the scanner rejects the edge's locally issued certificate. Its −20 is the whole gap to 100: with a publicly trusted certificate, as a deployed environment's Google-managed one is, the scan has no failed test. Grade here: B+ (80) |
| 4 | Every kind of response (`check.py`): 200, 401, 401 for an unknown route, 429, 403 origin | All nine headers on each; no `Server` header |
| 5 | **The SPA under its CSP, in Chromium** (`check078.mjs`) through the edge | local.r02 signs in through the edge (its `Origin` allowlisted); Home, notifications and the project register render with **no CSP violation and no page error**; another origin framing the SPA is blocked by `frame-ancestors 'none'`; after a 31-request burst from the page (29 × 401, then 429), the sign-in form shows the throttling message. 7 of 7 |

### 5.4 The SIT check, scripted and rehearsed

`infra/environments/verify-endpoint-hardening.sh <env>` runs the workbook's two validation checks against an environment's `APP_BASE_URL`. It prints one PASS, FAIL or SKIP line per check and exits 0 only when all pass:
- **E-1:** a burst of `SIGN_IN_LIMIT` + 1 sign-ins, as a username no directory holds, so it cannot lock out a real account;
- **E-2:** a foreign Origin, preflight and simple request;
- **E-3:** the nine headers on the SPA's `/` and on the API's 401;
- **E-4:** the MDN Observatory scanner (Node 24+).

On 2026-10-14 it was run with `BASE_URL=https://pmplatform.ahda.test` from a container on the stack's network. The stack was the deployed shape: the API with Redis counters, trusting one forwarded hop, behind the HTTPS edge of §5.3 row 3.

| Check | Result |
| --- | --- |
| E-1 | PASS: attempts 1–30 got 401; attempt 31 got 429 `RATE_LIMITED` with `Retry-After: 30`. The Redis key was the script container's address (`…address:172.18.0.8`), not the edge's (172.18.0.7): the forwarded client was counted, which is the mechanism SIT relies on |
| E-2 | PASS: the preflight and the request from `https://evil.example` both got 403 `ORIGIN_NOT_ALLOWED` |
| E-3 | PASS: all nine headers on the frontend's `/` (200) and on the API's `/api/v1/sessions/current` (401) |
| E-4 | FAIL, as expected locally: grade B+, 11 of 12 tests. The one failure is `strict-transport-security=hsts-invalid-cert`, caused by the edge's local certificate |

Against SIT, with its publicly trusted certificate, all four are expected to pass.

## 6. Acceptance criteria and validation checks

| Criterion / check | Evidence | Verdict |
| --- | --- | --- |
| A scripted burst of login attempts beyond the configured threshold is throttled with a standard 429 response | §5.3 row 1; `ASignInBurstBeyondTheLimit…`, `ASpoofedForwardedFor…`, `EverySignInStep…` | **Met** |
| A request from a non-allowlisted Origin is rejected by CORS | §5.3 row 2; `OriginAllowlistTests` | **Met** |
| A security-header scan (e.g. Mozilla Observatory equivalent) reports no missing mandatory header | §5.3 row 3; `SecurityHeadersTests` | **Met** — no header missing; the one failed test is the scan's local certificate, not a header |
| Validation check: "Script a login-attempt burst against SIT and confirm throttling kicks in at the configured threshold" | **Scripted:** `infra/environments/verify-endpoint-hardening.sh sit` (E-1). Rehearsed on 2026-10-14 against the deployed shape locally (§5.4): PASS. SIT has no address (`APP_BASE_URL` empty) and no deployment, and no credential for its project was available, so it has not run there | **Pending SIT** (F-1) |
| Validation check: "run a header-scan tool against the deployed frontend and API" | **Scripted:** the same script's E-3 (the nine headers on the SPA's `/` and the API's 401) and E-4 (the MDN Observatory scanner). Rehearsal: E-3 PASS on both; E-4 11/12, the 12th being the local certificate | **Pending SIT** (F-1, F-3) |
| `RATE_LIMIT_STORE_CONNECTION_STRING`: "throttling behaves consistently across multiple API instances in a load test" | `TwoApiInstancesSharingTheStore…` in process; the image with Redis on the stack | **Met in test**; load test on SIT pending (F-1, F-2) |
| `CORS_ALLOWED_ORIGINS`: "request from a non-listed origin is rejected by CORS in a test" | `OriginAllowlistTests` | **Met** |

## 7. Findings

| # | Finding | Owner | Status |
| --- | --- | --- | --- |
| F-1 | **The workbook's checks name SIT, which does not exist.** Run `infra/environments/verify-endpoint-hardening.sh sit` once it does (§5.4); exit 0 is the check passed. Run it from an address no one else is using and at least a minute after any other sign-in from that address, or E-1 also counts their requests. Confirm there that `ClientAddress:TrustedProxyHops` = 2 yields the client's address: one request with a known source address, then that address in `audit_event.client_address`. A wrong value shows either as everyone sharing one counter (too few hops) or as a spoofable address (too many) | DevOps/Platform Lead (TASK-018) | Open |
| F-2 | **No Redis instance is provisioned.** `RATE_LIMIT_STORE_CONNECTION_STRING`'s secret containers exist (`environments.json`), but Terraform has no Memorystore instance. Until one does, each Cloud Run instance counts alone, so a client gets up to max-instances × the limit. The client takes a connection string and works with Memorystore for Redis or Valkey. Rotating the secret takes a restart (D-7), like the database's | DevOps/Platform Lead | Open |
| F-3 | **The deployed SPA's headers depend on how it is served**, which is not decided (`identity-access-administration-ui.md` F-5). Whatever serves the bundle must send §3.2's set — a Cloud Storage backend bucket's `custom_response_headers`, or the API serving the bundle (its middleware then covers it, with the SPA's CSP). Until then ZAP's TASK-077 header alerts on the SPA are closed only under `vite preview` | TASK-018 / TASK-021 | Open |
| F-4 | **The limits are engineering values, not measured ones.** `SignIn` is per address, and AHDA's users most likely share a few corporate egress addresses: 30 a minute is ten complete sign-ins a minute per address. Read PROD's load balancer logs after go-live and adjust; the values are configuration, no release needed | Delivery team, with AHDA IT | Open |
| F-5 | **Closed here:** `auth-access-audit-logging.md` F-5 (`client_address` was the balancer), `sso-directory-integration.md` F-4 and `mfa-privileged-access.md` F-4 (sign-in and MFA endpoints not rate-limited), `input-validation.md` F-1 (missing response headers), `network-security.md` N-F1's HSTS dependency | This task | Closed |
| F-6 | **Throttling is logged, not audited.** The first refusal per client and window is a warning log line with the policy and client; no audit event is written. Whether a throttled sign-in burst is an AUTHENTICATION-class audit event is the audit catalogue's question | Audit catalogue owner (PTBC-029) | Open |

## 8. Change log

| Date | Change |
| --- | --- |
| 2026-10-14 | Built and verified (this record) |
| 2026-10-20 | TASK-090 adds a fourth policy, `ClientErrorReport` (30 a minute per client address), for `POST /client-error-reports`, the eleventh anonymous action; `EveryAnonymousActionAndEverySensitiveWriteIsRateLimited` covers it (`apm-error-tracking.md` D-7) |

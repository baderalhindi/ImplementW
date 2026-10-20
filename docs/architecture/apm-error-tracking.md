# Application Performance Monitoring & Error Tracking

| Field | Value |
| --- | --- |
| Task | TASK-090 — Integrate Application Performance Monitoring & Error Tracking (P16 - Observability) |
| Depends on | TASK-018 (CI/CD and controlled promotion: the environments that set `APM_DSN`); TASK-009 (`api-conventions.md` R-41, R-42: the correlation id and where it goes) |
| Record date | 2026-10-20 |
| Status | **BUILT AND VERIFIED LOCALLY**: in process through the real API pipeline against a stub ingestion endpoint (§5.1), by mutation (§5.2), and with the release image built from this branch sending to a real APM — GlitchTip 6.2.6, a Sentry-protocol server — where the workbook's validation check passed (§5.3). No SIT exists yet (F-3), and no APM has been chosen or provisioned for the environments (F-2) |
| Branch | `infra/task-090-apm-error-tracking-integration` |
| Directory | `src/backend/PMPlatform.Application/Common/Observability` (the redaction registry and redactor); the SDK integration is in `src/backend/PMPlatform.Api/Observability` (the composition root's side of it, ADR-003 L-4) and `src/frontend/src/shared/observability` |
| Deliverables | **APM/error-tracking SDK integration:** the Sentry .NET SDK (`Sentry.AspNetCore` 6.12.0) in the API — unhandled exceptions, logged errors, failed outbound calls and a trace per request, each tagged with its correlation id (`ApmRegistration`, `ApmEventEnrichment`); the SPA's unhandled errors reported through the API (`errorReporting.ts`, `RouteErrorPage`, `POST /api/v1/client-error-reports`). **Redaction cross-check:** the registry (`RedactionRegistry`), applied to every byte sent (`ApmPayloadRedaction`, `ApmEnvelope`, `TelemetryRedactor`), and `ApmRedactionTests.NoRegisteredFieldReachesTheApm`, which plants every registered field by every path and proves none arrives |
| Environment variables / secrets | `APM_DSN` (Secret, SIT/UAT/PROD — already mapped to `pmplatform-{env}-apm-dsn` in `infra/environments/environments.json`), `APM_ENVIRONMENT_TAG` (Public, DEV/SIT/UAT/PROD), and its build-time twin `VITE_APM_ENVIRONMENT_TAG` (TASK-013 R-4). New settings: `Apm:TracesSampleRate` (1.0), `Apm:DiagnosticExceptionEndpoint` (false), `RateLimiting:Policies:ClientErrorReport` (30 a minute) |
| Gate decision applied | None in the workbook row |
| Control | CTL-30 (this task), the APM part of CTL-27 (redaction) and CTL-29 (traceability); CTL-28 / ADR-001 C-6 constrain where the APM may run (F-2) |
| Sources read | The TASK-090 row, read from the workbook (`AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`) on 2026-10-20: description, acceptance criteria, deliverables, branch, variables and the validation check. The row as supplied for this task carried TASK-089's acceptance criteria; the workbook's were used. The Environment and Secrets rows `APM_DSN` and `APM_ENVIRONMENT_TAG`; `cybersecurity-control-matrix.md` CTL-27 to CTL-30; ADR-001 C-6 and its confirmation request Q9; `api-conventions.md` R-23, R-26, R-35, R-41 to R-43, R-51; `endpoint-hardening.md` D-2, D-6; `csrf-protection.md` §7; `auth-access-audit-logging.md` D-8; the TASK-083 row (not built) |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | Latency, throughput, error rate and unhandled exceptions of the API, and the SPA's unhandled errors, in one APM, each attributed to its correlation id | This task |
| **In** | Redaction of every APM payload against an explicit field registry, and the test that cross-checks the two | This task |
| **In** | Queue and integration failures as APM events: a worker pass or integration that logs an error, and an outbound call answered 5xx | This task |
| **Out** | Choosing, hosting and provisioning the APM for SIT/UAT/PROD, and creating the `APM_DSN` secret values | DevOps/Platform Lead (F-2) |
| **Out** | Redaction of the application's own logs (Cloud Logging) — the platform-wide log policy | TASK-083 (F-1) |
| **Out** | Dashboards and alert thresholds built on these signals | TASK-092 |
| **Out** | Uptime checks | TASK-091 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **The Sentry .NET SDK, speaking the Sentry envelope protocol to whatever `APM_DSN` names.** Any Sentry-protocol server ingests it: a self-hosted Sentry or GlitchTip in the named region, or a Sentry SaaS region. `Sentry.AspNetCore` 6.12.0, pinned in `Directory.Packages.props`, the latest release at least two weeks old | The sheet's variable is a *DSN*, Sentry's name for the ingestion address and key. One SDK gives exceptions, logged errors, failed outbound calls and request traces, and the server can be self-hosted, which CTL-28 needs: telemetry stays in the region unless AHDA Cybersecurity accepts otherwise in writing (ADR-001 C-6, Q9). OpenTelemetry was the alternative; it would add a collector and a separate error tracker to reach the same place |
| D-2 | **Off unless `APM_DSN` is set.** The decision is taken before the host is built, and without a DSN nothing is registered — the SDK's process-wide hub is never initialised, not even disabled. With a DSN, `APM_ENVIRONMENT_TAG` is required, or the API does not start | DEV and CI have no APM. The sheet's verification for the tag is "events in APM dashboard are correctly filtered by environment tag", which an untagged event defeats. A disabled initialisation would still replace a live hub elsewhere in the process |
| D-3 | **Redaction happens on the wire.** The SDK's HTTP transport sends through `ApmPayloadRedaction`, which reads the envelope (`ApmEnvelope`), redacts every JSON item with the registry (`TelemetryRedactor`), recomputes item lengths, and drops any item that is not JSON. Compression is off so the body can be read; a compressed body is refused, not sent | One choke point that nothing can go around. A scrubber on the SDK's object model would have to know every field the SDK, an integration or a future SDK version fills — span descriptions, contexts, breadcrumbs, exception data — and would fail open on the one it missed. This one fails closed |
| D-4 | **The registry is explicit: `RedactionRegistry`, 13 fields.** Credentials (password, secret and API key, token and any JWT, `Authorization`, cookie, connection string, credentials in a URL and the DSN, second-factor code), personal identifiers (national ID or Iqama number, a person's name), contact details (e-mail, mobile number) and the client IP address. Each has the names it travels under — a name matches with case and separators ignored, or as a suffix (`newPassword`, `AD_BIND_PASSWORD`) — and, where its value has a shape, a pattern that finds it in free text. A registered name's value is replaced whole; in text, `name=value` and `name: value` lose the value | The workbook asks for a cross-check "against the redaction registry", and CTL-27 asks that "the redacted-field registry is explicit". TASK-083, which owns the platform-wide policy, is not built, so this task creates the registry and TASK-083 extends it (F-1). The fields are those CTL-27 names and those earlier records withhold (`auth-access-audit-logging.md` D-8, the SMS threat model R-7); the classification taxonomy that will add more is outstanding (UGV-01) |
| D-5 | **Data minimisation before redaction.** The SDK sends no user, client address, cookies or request body (`SendDefaultPii` off, `MaxRequestBodySize` none); `ApmEventEnrichment` drops the query string and every request header but `Accept`, `Content-Length`, `Content-Type`, `User-Agent` and `X-Correlation-Id`. Logged warnings become breadcrumbs, information lines do not. No trace header is added to calls to third parties. The SDK writes nothing to disk. The only user field is the SDK's own installation hash | What is not collected cannot leak, and redaction is a reduction, not a guarantee (CTL-28). EF Core logs its SQL at Information |
| D-6 | **Every event and trace carries the tag `correlation_id`**: the request's (`CorrelationId.Of`, the value of the response's `X-Correlation-Id` and of the 500 envelope's `correlationId`), or for a background pass the lineage's (`CorrelationScope.Current`). A report from the SPA carries the id of the failed call it names | "Correctly attributed to the originating request/correlation ID" (acceptance criterion 1); R-42: "the APM span as an attribute". A user who quotes the reference on an error page leads support to the APM event, its trace, its log lines and its audit events |
| D-7 | **The SPA reports through the API, with no APM SDK in the browser.** `installErrorReporting` (script errors, unhandled rejections) and the route error boundary `RouteErrorPage` (a screen that throws) send `POST /api/v1/client-error-reports`: type, message, stack, the route's path without query or fragment, and the correlation id of a failed API call. The API turns it into an APM event (`origin=spa`) and answers 204; nothing is stored. Anonymous — the sign-in screen can fail too — so rate-limited per client address (`ClientErrorReport`, 30 a minute), and a JSON body, so `CsrfProtection` passes it. The SPA reports only in a build with `VITE_APM_ENVIRONMENT_TAG`, each distinct error once, at most 10 a page | `APM_DSN` is a Secret, and anything in the bundle is public (ADR-003 T-1); the SPA's CSP allows only its own origin; the browser's reports go through the same redaction as the server's. No npm dependency is added |
| D-8 | **What is captured.** Errors: an exception that reaches the exception handler (R-26), an unobserved task exception, an unhandled exception on any thread, every log line at Error or above — which is how a worker pass, a queue or an integration reports a failure — and an outbound `HttpClient` call answered 5xx. Performance: a trace per request named by its route (`GET /api/v1/projects/{id}`), with duration, status and its database and HTTP spans, which give latency, throughput and error rate per route. `Apm:TracesSampleRate` is 1.0 | Blueprint Section 22: "Monitoring: availability/errors/latency/queue/integration failures" (F-5 of the control matrix). Availability is TASK-091's uptime check. The sample rate is revisited with production volume (F-5) |
| D-9 | **The validation check's deliberate exception is a switch, off by default.** With `Apm:DiagnosticExceptionEndpoint=true`, `POST /api/v1/apm-diagnostic-exceptions` (any signed-in user, body `{"note": "…"}`) throws `ApmDiagnosticException` with the note in its message. It is never in the OpenAPI document, and the API refuses to start with it on when the environment tag is `prod` or missing | The workbook's check is "throw a test exception in a non-PROD environment". Production cannot be made to throw on request; the note lets the check plant a sensitive value in an exception message, which is where real ones end up |
| D-10 | **The report endpoint is documented under FG-05's tag, `IntegrationMonitoring`** | R-51 allows only the 21 module tags, and FG-05 is the platform's monitoring module. Nothing in FG-05 is referenced; `IntegrationMonitoringContractTests` lists the operation beside FG-05's own |

## 3. As built

| Piece | Where |
| --- | --- |
| `RedactionRegistry`, `RedactedField`, `TelemetryRedactor` | `src/backend/PMPlatform.Application/Common/Observability/` |
| `ApmRegistration` (SDK options, the diagnostic endpoint), `ApmOptions`, `ApmEventEnrichment`, `ApmPayloadRedaction`, `ApmEnvelope` | `src/backend/PMPlatform.Api/Observability/` |
| `ClientErrorReportsController`, `ClientErrorReportCreateRequest` | `src/backend/PMPlatform.Api/Controllers/`, `Models/Observability/` |
| `builder.AddApm()` after CORS; `app.MapApmDiagnostics()` after the controllers | `src/backend/PMPlatform.Api/Program.cs` |
| `ClientErrorReport` rate-limit policy, `Apm` settings | `Middleware/RateLimitPolicy.cs`, `appsettings.json` |
| SPA reporter, route error boundary, the screen-error text in Arabic and English | `src/frontend/src/shared/observability/`, `src/main.tsx`, `src/app/routes.tsx`, `src/shared/i18n/locales/` |
| Tests | `PMPlatform.Tests.Integration/Observability/` (CI step "Observability integration tests", job `backend-endpoint-hardening`); `PMPlatform.Tests.Unit/Application/Common/TelemetryRedactorTests.cs`; `src/frontend/src/shared/observability/errorReporting.test.tsx` |

## 4. API

| Operation | Notes |
| --- | --- |
| `POST /api/v1/client-error-reports` (`IntegrationMonitoring_CreateClientErrorReport`) | **New.** Anonymous, `ClientErrorReport` rate limit, not a sensitive write (R-35: stores nothing). Body: `source` (`WINDOW_ERROR`, `UNHANDLED_REJECTION`, `RENDER_ERROR`), `errorType` (≤200), `message` (≤2,000), `path` (≤2,000, starting `/`, no `?` or `#`), optional `stack` (≤8,000) and `correlationId`. 204; 400 `VALIDATION_FAILED` with the field; 429 `RATE_LIMITED` |
| `POST /api/v1/apm-diagnostic-exceptions` | Only where an environment enables it (D-9); not in the contract |

`docs/api/openapi.v1.json` is regenerated with the new operation; no existing operation changed and no error code was added.

## 5. Verification

### 5.1 Tests

| Suite | What it proves | Result |
| --- | --- | --- |
| `ApmErrorTrackingTests.ADeliberateUnhandledExceptionReachesTheApmAttributedToItsRequest` | **Acceptance criterion 1 in process.** A signed-in user's request to the diagnostic endpoint answers 500 `INTERNAL_ERROR`; the event arrives at the stub ingestion endpoint, over HTTP, with `correlation_id` equal to the 500's `correlationId` and `X-Correlation-Id`, the exception type, environment `test`, level error, unhandled, the route as its transaction; no query string, cookies, body or unlisted header; no user but the SDK's installation hash | passed (arrives in under a second) |
| `ApmRedactionTests.NoRegisteredFieldReachesTheApm` | **Acceptance criterion 2, the cross-check.** Every name of every registry entry (41) with a unique marker value, and a value in each of the six registered shapes, through the query string, request headers and the exception message of a request that throws; a logged error whose properties carry the markers under their registered names; and the SPA's report with all of them in its message, stack and path. All three events arrive, each with a control value planted beside the markers; not one marker, nor the session's access or refresh token, is anywhere in what the APM received. A registry entry with a value pattern and no planted value fails the test | passed |
| `ApmErrorTrackingTests.EveryRequestIsTracedWithItsCorrelationId` | A request's trace arrives named by its route, with its duration and status, tagged with the response's correlation id, and without the bearer token | passed |
| `ApmErrorTrackingTests.TheSpasErrorReportLandsOnTheRequestThatCausedIt` | A report naming a failed call is tagged with that call's id; one naming none, with its own request's id; `origin=spa`, the source, the path as the transaction | passed |
| `ApmErrorTrackingTests.AnIncompleteReportIsRefused` (5 cases), `WithoutADsnNothingIsRegistered`, `AnUnsafeApmConfigurationStopsTheApi` (4 cases), `TheDiagnosticExceptionIsNeverDocumented` | Validation; no SDK and no diagnostic endpoint without configuration; `prod` or no tag stops the API; the endpoint is never documented | 11 passed |
| `ApmEnvelopeTests` (3) | The envelope header passes unchanged; JSON items are redacted; a length-prefixed item is read by its length and given its new one; an item that is not JSON is dropped | 3 passed |
| `TelemetryRedactorTests` (38, unit) | Names matched whatever their case and separators, ordinary names not; text cases (connection strings, JSON, `national_id:`, Bearer, JWT, an e-mail in a PostgreSQL error, Saudi mobiles, an Iqama, URL credentials, `apiKey`); ordinary text unchanged (a uuid, longer digit runs, versions); JSON redacted in place; every registry entry well-formed | 38 passed |
| `errorReporting.test.tsx` (7, frontend) | The report's body (path only, no token, the failed call's correlation id); long text cut; once per error, at most 10 a page; nothing without the environment tag; a refused report raises nothing; window errors and rejections until removed; a screen that throws is reported and replaced by the explanation, never the error | 7 passed |
| `RateLimitingTests.EveryAnonymousActionAndEverySensitiveWriteIsRateLimited`, `IntegrationMonitoringContractTests` | Updated for the eleventh anonymous action and FG-05's sixteenth operation | passed |
| Full backend unit suite; integration suite without Persistence (with `RATE_LIMIT_TEST_REDIS`); frontend lint, typecheck and suite with coverage | No regression | see the PR's test evidence |

### 5.2 Mutation tests

Each mutant was compiled into the solution and judged by the Observability integration tests and `TelemetryRedactorTests`; a mutant is killed when either fails. The unmutated tree passed first (`artifacts/task-090/mutations/run.py`, not committed).

**14 of 14 killed.** The harness records killed or survived per mutant; the right-hand column names the tests that assert the mutated behaviour.

| # | Mutant | Asserted by |
| --- | --- | --- |
| M1 | The egress filter not installed | `NoRegisteredFieldReachesTheApm` |
| M2 | A registered property name does not mask its value | `NoRegisteredFieldReachesTheApm`, `TelemetryRedactorTests`, `ApmEnvelopeTests` |
| M3 | Value patterns not applied | same |
| M4 | `name=value` in text not recognised | same |
| M5 | No correlation id tag | every `ApmErrorTrackingTests` arrival, `NoRegisteredFieldReachesTheApm` |
| M6 | The request's correlation id overwrites the one a report names | `TheSpasErrorReportLandsOnTheRequestThatCausedIt`, `NoRegisteredFieldReachesTheApm` |
| M7 | A logged property's value left in the formatted message | `NoRegisteredFieldReachesTheApm` |
| M8 | Every request header kept | `ADeliberateUnhandledException…` (header allowlist) |
| M9 | The query string kept | `ADeliberateUnhandledException…` |
| M10 | The diagnostic endpoint allowed in production | `AnUnsafeApmConfigurationStopsTheApi` |
| M11 | A DSN accepted without an environment tag | `AnUnsafeApmConfigurationStopsTheApi` |
| M12 | The SDK's own personal data on (`SendDefaultPii`) | `ADeliberateUnhandledException…` (user and cookies) |
| M13 | A payload that is not JSON passed on | `AnItemThatIsNotJsonIsDropped` |
| M14 | A report path with a query string accepted | `AnIncompleteReportIsRefused` |

M8 and M9 would not have leaked a registered value — the egress redaction still masks it — so they are asserted directly as minimisation (D-5).

### 5.3 The workbook's validation check, against a real APM

"Throw a test exception in a non-PROD environment and confirm it appears in the APM tool attributed to the correct correlation ID; verify a sensitive test field does not appear in the captured payload."

The stack: GlitchTip 6.2.6 (all-in-one, its own PostgreSQL) on the compose network as the APM; the API image built from this branch on a fresh, migrated and seeded database (`task090_live`) with `APM_DSN` naming the GlitchTip project, `APM_ENVIRONMENT_TAG=dev` and the diagnostic endpoint on. local.r02 signs in; every request goes through the API's real pipeline and the SDK's real transport; GlitchTip's own API — the one its dashboard reads — is then polled (`artifacts/task-090/live`: reset.sh, setup_glitchtip.py, check.py; not committed). Run on 2026-10-20: **10 of 10 checks passed.**

| # | Step | Result |
| --- | --- | --- |
| 1 | The diagnostic exception, with a marker for each of the registry's 41 names in the query string, as `X-<name>` headers and in the note, and a value in each of the six registered shapes in the note | 500 `INTERNAL_ERROR`, `correlationId` `47bc9a6c-…`, equal to `X-Correlation-Id` |
| 2 | **In the APM within 5 minutes, attributed to that request** | Listed by GlitchTip **2.2 s** after the request, as `PMPlatform.Api.Observability.ApmDiagnosticException`, tag `correlation_id` `47bc9a6c-…`, environment `dev`, the control value present |
| 3 | The SPA's report, naming a failed call's correlation id, with the same markers | 204; in GlitchTip 2.1 s later, `origin=spa`, on the named correlation id |
| 4 | **No sensitive field in the captured payload** | No marker and not the access token in any stored event of the project. The stored message reads `… password=[REDACTED]; passwd=[REDACTED]; secret=[REDACTED]; …`; the stored request has three headers (`Content-Length`, `Content-Type`, `User-Agent`) and no query string, cookies or body |
| 5 | Latency and throughput | GlitchTip's transaction groups list each route with its count and mean duration (`POST /api/v1/sessions` n=2, 156 ms) |

## 6. Acceptance criteria and validation check

| Criterion / check | Evidence | Verdict |
| --- | --- | --- |
| A deliberately thrown unhandled exception in a non-PROD environment appears in the APM dashboard within 5 minutes, correctly attributed to the originating request/correlation ID | `ADeliberateUnhandledExceptionReachesTheApmAttributedToItsRequest` (CI, every PR); §5.3 rows 1–2: 2.2 s in GlitchTip, on the request's correlation id | **Met** locally; on SIT once the APM exists (F-2, F-3) |
| No sensitive field appears in captured error payloads (cross-checked against the redaction registry) | `NoRegisteredFieldReachesTheApm` (CI, every PR) plants every registry entry by every path; §5.3 row 4 | **Met** for the registry as it stands; the registry itself is to be extended by TASK-083 and the UGV-01 taxonomy (F-1, F-4) |
| Validation check: throw a test exception in a non-PROD environment, confirm it appears attributed to the correct correlation ID, verify a sensitive test field does not appear | §5.3 | **Met** locally, against a real APM |
| CTL-30: latency, throughput, error rates, queue and integration failures, unhandled exceptions, backend and frontend, with PII redaction | D-6 to D-8; §5.1 | **Met** in the build; frontend latency is server-side only (F-6) |

## 7. Operating it

| Environment | Settings | Notes |
| --- | --- | --- |
| DEV (compose), CI | `APM_ENVIRONMENT_TAG=dev` / none; no `APM_DSN` | No APM: nothing registered (D-2). The SPA has `VITE_APM_ENVIRONMENT_TAG=dev`, so it reports, and the API drops the reports |
| SIT, UAT | `APM_DSN` from `pmplatform-{sit,uat}-apm-dsn`; `APM_ENVIRONMENT_TAG=sit`/`uat`; the SPA built with the same tag | To run the validation check: set `Apm__DiagnosticExceptionEndpoint=true`, sign in, `POST /api/v1/apm-diagnostic-exceptions` with `{"note": "password=<marker> <an e-mail address>"}`, find the event by the 500's `correlationId`, confirm the marker is `[REDACTED]`; then switch it off |
| PROD | `APM_DSN` from `pmplatform-prod-apm-dsn`; `APM_ENVIRONMENT_TAG=prod` | The diagnostic endpoint cannot be enabled (D-9). Rotating the DSN: `secret-rotation-runbook.md` (`APM_DSN`: "verify a test exception arrives before retiring the old DSN") — in PROD by an SPA report or a logged error, since the endpoint is off |

Adding a field to the redaction: one line in `RedactionRegistry` — its names, and its value pattern if it has a shape, with a matching planted value in `ApmRedactionTests.ShapedMarker`, which the test demands.

## 8. Findings

| # | Finding | Owner | Status |
| --- | --- | --- | --- |
| F-1 | **TASK-083 is not built, so this task created the redaction registry.** It is applied to APM payloads only. The application's own log lines (stdout, Cloud Logging) are not redacted by it: a value logged at Information still reaches the logs, though not the APM (D-5). TASK-083 should adopt `RedactionRegistry` as its field registry — moving it to `Common/Logging` if it prefers — so logs and APM share one list | TASK-083 | Open |
| F-2 | **No APM is chosen or provisioned for SIT/UAT/PROD, and the `APM_DSN` secrets hold no value.** The SDK needs any Sentry-protocol server. In the named region (me-central2, provisional) that is a self-hosted Sentry or GlitchTip — GlitchTip needs only PostgreSQL and one container, as §5.3 ran it. A Sentry SaaS region is outside the Kingdom and needs AHDA Cybersecurity's written acceptance first (ADR-001 C-6, confirmation request Q9; CTL-50 asks for the processor to be named) | DevOps/Platform Lead; AHDA Cybersecurity (Q9) | Open |
| F-3 | **The validation check ran locally**, with the release image and a real APM (§5.3). No SIT exists (`endpoint-hardening.md` F-1); §7 is the procedure there | DevOps/Platform Lead (TASK-018) | Open |
| F-4 | **Free-text redaction is a reduction.** A personal value with no registered name before it and no registered shape — a person's name inside an exception message, or typed text quoted in an SPA error — is not recognised. The classification taxonomy (UGV-01) decides which further fields are sensitive; each becomes a registry entry. Code that builds exception messages from user input remains the root cause to avoid | AHDA Cybersecurity (UGV-01); developers | Open |
| F-5 | **Every request is traced (`Apm:TracesSampleRate` 1.0).** Correct for throughput and cheap in the SDK, but the APM's ingest grows with traffic. Set the rate per environment once PROD volume is known; the APM extrapolates throughput from the rate | DevOps/Platform Lead (with TASK-092) | Open |
| F-6 | **The SPA reports errors, not its own timings.** Latency is measured at the API per route; time in the browser (rendering, Web Vitals) is not captured. If TASK-092's dashboards need it, the report endpoint is the place to add it | Frontend lead (TASK-092) | Open |
| F-7 | **SPA stack traces are of the minified bundle.** Source maps are built (`vite.config.ts`) but not given to the APM, so a frame reads `index-<hash>.js:1:2345`. Uploading them is a pipeline step with the APM's own API token | DevOps/Platform Lead | Open |

## 9. Change log

| Date | Change |
| --- | --- |
| 2026-10-20 | Built and verified (this record) |

# Platform-Wide Input Validation & Output Encoding

| Field | Value |
| --- | --- |
| Task | TASK-077 — Implement Platform-Wide Input Validation & Output Encoding (P14 - Security Hardening & Compliance) |
| Depends on | TASK-009 (`api-conventions.md`: the R-23 envelope, R-24's 400/422 split, R-25) and TASK-041 (`project-registration.md`: the first module's request models, whose shape the rest follow) |
| Record date | 2026-10-14 |
| Status | **BUILT AND VERIFIED LOCALLY**: in process through the real API pipeline, in the SPA's test suite, in Chromium against the API built from this branch, and by OWASP ZAP against the DEV compose stack (§5) |
| Branch | `sec/task-077-platform-input-validation-output-encoding` |
| Directory | `src/backend/PMPlatform.Application/Common/Validation` |
| Deliverables | **Shared validation middleware/library:** `TextInput` and `ValidationCode` (the library, in the task's directory); `RequestTextValidation`, the MVC filter that applies `TextInput` to every argument of every action; the same rules on the multipart upload form. **Output-encoding review checklist:** `output-encoding-checklist.md`. **ZAP baseline report:** `zap-baseline-report.md`, with ZAP's own reports beside it in `zap/`. Also the SPA's ESLint rules against HTML and script sinks, `isAppPath`, and the stored-XSS sweep over ten screens |
| Environment variables / secrets | None |
| Gate decision applied | None in the workbook row |
| Control | CTL-11 (`cybersecurity-control-matrix.md`); OWASP ASVS 4.0 V5 |
| Sources read | The TASK-077 row as supplied on 2026-10-14, checked against the workbook (`AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`): description, acceptance criteria, deliverables, branch and validation checks; the TASK-078 row, to keep response headers out (§1). `api-conventions.md` R-10, R-23 to R-26, T-3 and §8 row 8; `cybersecurity-control-matrix.md` CTL-11, CTL-12, §7; ADR-012 (text stored as entered) |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | One server-side layer that holds every string an endpoint receives — body, query, route, upload form — to the same rules, before and in addition to each request model's own validation and to anything the SPA checks | This task |
| **In** | Context-aware output encoding on every render path: the SPA's screens and links, and the backend's SQL, exports, messages and logs — reviewed, gaps fixed, and guarded so they stay closed | This task |
| **In** | Evidence: OWASP ZAP against a representative set of endpoints; the stored-XSS check in a browser | This task |
| **Out** | Response headers — CSP, `X-Content-Type-Options` on every response, `X-Frame-Options`, HSTS, `Referrer-Policy` — rate limiting and CORS | TASK-078 (CTL-12) |
| **Out** | CSRF | TASK-079 (CTL-13) |
| **Out** | What a field *means* — a date order, a catalogue item that must be PUBLISHED, a lifecycle state. That stays in each module (R-24: 422) | Each module |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Validate on the way in, encode on the way out; never escape on the way in.** Text is stored and returned exactly as entered, markup included | ADR-012 stores narrative as entered. Escaping at input would corrupt the data for every context but one (a CSV cell, a PDF, an e-mail, the audit trail) and give a false sense of safety. Each output context encodes for itself (`output-encoding-checklist.md` §1) |
| D-2 | **The platform's text rules are one library**, `TextInput` in `PMPlatform.Application/Common/Validation`: no control character except tab, line feed and carriage return (U+0000–U+001F, U+007F, U+0080–U+009F); no surrogate without its pair; at most 10,000 characters. Its codes are `ValidationCode`, which `FieldError`'s constants now point at, so there is one list | These are the rules no field may break, whatever it means. U+0000 was a 500 (PostgreSQL refuses it in `text`, §5.1); the other controls are invisible and carry log, header and terminal injection; a lone surrogate cannot be stored as UTF-8. In Application, so a module that receives text outside HTTP can apply the same rules |
| D-3 | **`INVALID_CHARACTER` is a new validation code** | Neither `MALFORMED` (a value that does not parse) nor any existing code says what is wrong. A new error code is non-breaking (R-10) |
| D-4 | **The ceiling is 10,000 characters**, the longest limit any field of the API already declared | A ceiling below an existing field's limit would tighten that field, an R-10 break. Each field keeps its own, stricter, limit in its `Validate` |
| D-5 | **Applied by an MVC action filter registered for every controller**, `RequestTextValidation`. It walks each bound argument: strings, collections, dictionaries (keys and values) and the public properties of the platform's own types, naming each field as the request names it (`title.text`, `fields[2].value`, `status`, R-23). It runs after authentication and authorization, and before the action, so before the model's own `Validate` | A filter cannot be forgotten by a new endpoint, unlike a call in each controller. After authorization, so an anonymous or unauthorised caller learns nothing from a 400. MVC has already bound and named the arguments, and the walk reads them as bound |
| D-6 | **A guard test fails for any action argument the walk would not see into** (`RequestTextCoverageTests`): a `JsonElement`, an `object`, a third-party type | Without it, a new endpoint taking such a type would carry text past the filter unnoticed |
| D-7 | **Forms MVC does not bind apply `TextInput` where they are read.** The only one is the document upload (`DocumentUploadForm`): every form field and the file's name | `[DisableFormValueModelBinding]` keeps the form away from MVC, so the filter never sees it |
| D-8 | **Unknown JSON members are still ignored**, not refused | Refusing them is a tightening (R-10) and the SPA ships with the API (R-11), so it buys nothing against injection: an unknown member never reaches a handler |
| D-9 | **The SPA's sinks are closed by lint, not by review alone.** ESLint refuses `dangerouslySetInnerHTML`, `srcDoc`, `innerHTML`/`outerHTML` assignment, `insertAdjacentHTML`, `createContextualFragment`, `parseFromString`, `document.write`, `eval`, `new Function`, `javascript:` URLs, and an `href`/`src`/`action`/`formAction` bound to a value on a DOM element. No plugin is added; the rules are core ESLint (`no-restricted-syntax`, `no-eval`, `no-new-func`, `no-script-url`) | The tree has none of these today (§3.2); lint keeps it so, in CI, and the reviewer's checklist covers what lint cannot |
| D-10 | **One `isAppPath` for every path taken from data**: the notification deep link and the sign-in redirect. One leading slash; no backslash, scheme or control character | Both accepted `/\evil.example`, which a browser treats as `//evil.example` (F-2) |
| D-11 | **The stored-XSS sweep is a test over real screens** (`outputEncoding.test.tsx`): each screen that shows free text renders a payload in it, and the test fails if any element, script, `javascript:` link or `on*` attribute the payload describes appears | The acceptance criterion asks that every list/detail screen that renders user-supplied text is *verified*; a test keeps verifying it. A new screen is added by the checklist's rule |
| D-12 | **ZAP: a baseline of the SPA and an active scan of the API**, both against the stack built from this branch (§5.4) | A baseline is passive and sends no payload, so on its own it cannot find an injection. The active API scan sends ZAP's SQL, NoSQL, LDAP, XPath, XSS, command and template injection payloads into every parameter of 34 operations |

## 3. As built

### 3.1 Backend

| Piece | Where | What it does |
| --- | --- | --- |
| `TextInput` | `PMPlatform.Application/Common/Validation/TextInput.cs` | `Violation(value)` returns the code a string breaks, or null (D-2) |
| `ValidationCode` | `…/Common/Validation/ValidationCode.cs` | The validation codes of `errors[].code`, including `INVALID_CHARACTER` |
| `RequestTextValidation` | `PMPlatform.Api/Validation/RequestTextValidation.cs`, registered in `Program.cs` | D-5. A refusal is 400 `VALIDATION_FAILED` with `errors[]` of `{field, code}`, never the value (R-25) |
| Upload form | `PMPlatform.Api/Models/DocumentManagement/DocumentUploadForm.cs` | D-7 |
| `FieldError` | `PMPlatform.Api/Errors/FieldError.cs` | Its constants are `ValidationCode`'s |

### 3.2 Frontend

| Piece | Where |
| --- | --- |
| Sink rules | `src/frontend/eslint.config.js` (D-9) |
| `isAppPath` | `src/frontend/src/shared/navigation/appPath.ts`, used by `notifications/presentation.ts` (`isAppRoute`) and `SignInPage.tsx` (`redirectTarget`) |
| Stored-XSS sweep | `src/frontend/src/app/outputEncoding.test.tsx` |

### 3.3 What the review found already in place

Recorded in full in `output-encoding-checklist.md` §3. In short: no SQL text is built from a value (every raw call is parameterised; the SCR-138 explorer resolves names through its allowlist and filters in memory); LDAP filters are RFC 4515-escaped; CSV and XLSX cells are neutralised against formulas; PDF text is written as glyph ids; e-mail is plain text with single-pass placeholders; downloads are attachments with `nosniff`; logs carry no end-user free text; the SPA has no HTML sink and no link bound to data.

## 4. API

No endpoint, schema or status code changes; `docs/api/openapi.v1.json` is unchanged. A request that breaks the text rules is answered:

```http
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json

{ "type": "urn:pmplatform:problem:validation-failed", "title": "Validation failed.", "status": 400, "instance": "/api/v1/risks",
  "code": "VALIDATION_FAILED", "correlationId": "…", "idempotencyKey": "…", "timestamp": "…",
  "errors": [ { "field": "title.text", "code": "INVALID_CHARACTER" } ] }
```

A request that a model's own `Validate` would also refuse gets the text rules' answer first, and only that (the filter returns before the action).

## 5. Verification

### 5.1 The defect the layer closes

Before the filter, on this branch's base (`dev` at beeaaab), through the risk API in process:

| Title sent | Answer |
| --- | --- |
| `nul \u0000 here` | **500 `INTERNAL_ERROR`**: PostgreSQL refuses U+0000 in `text` |
| `bell \u0007 here` | 201, stored and returned with the BEL |

After: both are 400 `title.text INVALID_CHARACTER` and nothing is stored. The same on the DEV stack (§5.4).

### 5.2 Tests

| Test | Proves |
| --- | --- |
| `TextInputTests` (unit, 16) | Text people write is accepted — Arabic, emoji, tabs and line breaks, markup, SQL, bidirectional marks; every refused control character and every unpaired surrogate is refused; the ceiling is inclusive |
| `RequestTextValidationTests` (integration, 5) | Through the real pipeline: control characters in a body are refused field by field and nothing is written; a lone-surrogate escape is refused while binding (`body MALFORMED`); a query parameter is checked and named by itself, the ceiling included; obeying text — markup, SQL, Arabic, emoji — is stored and returned exactly as entered, as `application/json`; an unauthenticated request is 401, its body never looked at |
| `UploadFormTextValidationTests` (integration, 1) | An upload form field and a file name holding a control character are refused |
| `RequestTextWalkTests` (3) | The walk reaches nested models, list items, dictionary keys and values and renamed properties, and names each by its path; a parameter by itself and a query model by its properties |
| `RequestTextCoverageTests` (1) | Every argument of every action of the API is a type the walk sees into (D-6) |
| `LdapFilterTests` (unit, 6) | A username is one escaped assertion value in the directory search filter (OE-12) |
| `appPath.test.ts` (2) | D-10, including `/\evil.example` and `javascript:` |
| `outputEncoding.test.tsx` (10) | D-11 on SCR-080, SCR-082, the issue register and detail, change request detail, the task board, progress (Arabic entry), document detail, SCR-150 and ADM-051 |

The new integration tests run in CI as their own step, "Input validation integration tests", in the `backend` job (`PMPlatform.Tests.Integration.InputValidation.`).

Full suites on this branch (Release, as CI builds): the build with 0 warnings; unit 1907/1907; integration 873/873; SPA lint, format, type-check and build clean, and vitest 1017/1017 in 58 files. An earlier full integration run failed 2 `RegisterQueryPlanTests` cases, which passed 75/75 alone and in this run: a known planner-choice flake under the parallel run.

### 5.3 Mutation tests

Each mutant was compiled into the API, and the input-validation integration tests and `TextInputTests` were run against it; a mutant is killed when a test fails (`artifacts/task-077/mutations/run.py`, not committed). **14 of 14 killed.** The SPA's two guards were mutated by hand: rendering the risk title through `dangerouslySetInnerHTML` failed the sweep (SCR-080) and lint; dropping the backslash rule from `isAppPath` failed its test on `/\evil.example`.

| # | Mutant | Killed by |
| --- | --- | --- |
| M1 | The filter not registered | `ControlCharactersInABody…`, `AQueryParameterIsChecked…` |
| M2 | Control characters allowed | 10 tests in four classes: the body, query and upload tests, both walk tests, and five `TextInputTests` cases |
| M3 | C1 controls (U+0080–U+009F) allowed | `ControlCharacters…(c1 \x9f)` |
| M4 | No ceiling | `TextUpToTheCeiling…`, both query tests |
| M5 | Lone surrogates allowed | `ASurrogateWithoutItsPairIsRefused` |
| M6 | Collections not walked | `EveryStringInABodyIsReached…`, `AParameterIsNamedByItself…` |
| M7 | Dictionaries not walked | `EveryStringInABodyIsReached…` |
| M8 | Nested models not walked | `ControlCharactersInABody…`, `EveryStringInABodyIsReached…` |
| M9 | Parameters named from the root | Both query tests |
| M10 | `[JsonPropertyName]` ignored | `EveryStringInABodyIsReached…` |
| M11 | A refusal not returned | Every refusal test |
| M12 | Upload form fields unchecked | `AnUploadFormFieldOrFileName…` |
| M13 | Upload file name unchecked | `AnUploadFormFieldOrFileName…` |
| M14 | Only `PMPlatform.Api` types walked | `EveryStringInABodyIsReached…`, `AParameterIsNamedByItself…` |

### 5.4 Validation checks on the DEV stack

The stack: the compose images built from this branch (`docker compose … up -d --build`), a scratch database `task077_live` migrated and seeded by them with TASK-056's project fixture, and every permission granted to local.r02 at ALL so a scan reaches each handler rather than a 403 (F-6 says why not the compose database). There is no SIT deployment yet; the workbook's SIT run is F-5.

**Check 1 — "Run OWASP ZAP baseline scan … and confirm zero High/Critical findings."** **Passed on DEV: zero High, zero Critical, no injection finding.** The ZAP 2.17.0 baseline of the SPA's production build ran 58 passive rules: 2 Medium, CSP and anti-clickjacking headers, and 6 Low, all headers but one bundle timestamp. The active API scan, signed in as local.r02, covered 34 operations on 19 paths: 117 rules passed, including ZAP's SQL, XSS, CRLF, command, XPath, XSLT, template and code injection rules, with 2 Low header alerts. The API logged no unhandled exception. Every header alert is TASK-078's (F-1). The scan's limits, chiefly that ZAP's placeholder values stop most requests at validation, are in `zap-baseline-report.md` §4.

**Check 2 — "Manually attempt a stored-XSS payload in a comment field and confirm it renders as inert text, not executable markup."** local.r02 registered a risk through the API whose description is `<img src=x onerror=alert(document.domain)><script>alert(1)</script><svg onload=alert(2)>` (201, stored as entered), then signed in to the SPA in Chromium and opened SCR-080 and SCR-082. The description shows as that literal text; the page holds no `<script>`, `<img>` or `<svg onload>` and no `on*` attribute; no alert, confirm or prompt opened; no page error. 5/5 checks passed. In the same session a title holding U+0000 was answered 400 `title.text INVALID_CHARACTER`. The check was repeated on a comment-type field: no field is named "comment", and the nearest is a risk's closure rationale, the note the person closing a risk writes, shown under *Closure* on SCR-082. local.r02 closed a risk with the rationale `<img src=x onerror=alert(document.cookie)><script>alert("comment")</script><a href="javascript:alert(3)">click</a><iframe srcdoc="<script>alert(4)</script>"></iframe>` (200, stored as entered). The rationale shows as that literal text; the page holds no `<script>`, `<img>` or `<iframe>`, no `javascript:` link and no `on*` attribute; the payload's `<a>` is not a link; no dialog opened; no page error. 5/5 checks passed.

## 6. Acceptance criteria and validation checks

| Criterion / check | Result | Evidence |
| --- | --- | --- |
| An automated scan (e.g. OWASP ZAP baseline) against a representative set of endpoints finds zero High/Critical injection findings | **Met on DEV**: zero High/Critical and no injection finding of any severity, baseline and active API scan | §5.4, `zap-baseline-report.md` |
| Every list/detail screen that renders user-supplied text (e.g. risk description, comment) is verified to encode it, not interpolate raw HTML | **Met.** The tree has no HTML sink (lint over the whole SPA, D-9); the sweep renders a payload on ten screens and finds it inert; the checklist adds every new free-text screen to the sweep | `outputEncoding.test.tsx`, `output-encoding-checklist.md` §3.1 |
| Validation check 1: ZAP baseline against SIT, zero High/Critical | **Passed on DEV**, not yet on SIT | §5.4; F-5 |
| Validation check 2: stored-XSS payload renders as inert text | **Passed** in Chromium | §5.4 |

## 7. Findings

| # | Finding | Owner | Status |
| --- | --- | --- | --- |
| F-1 | ZAP reports the missing response headers — CSP and anti-clickjacking (Medium), `X-Content-Type-Options`, `Permissions-Policy`, the cross-origin policies (Low) — on the SPA and the API. They are CTL-12's | TASK-078 | Open there |
| F-2 | **Fixed here.** The SPA followed `/\evil.example` as an app path (notification deep link, sign-in redirect); a browser opens it as `//evil.example`. The API's intake already refused it, so no stored deep link carried it | This task | Closed |
| F-3 | **Fixed here.** A U+0000 in any text field was a 500 (§5.1) | This task | Closed |
| F-4 | Two values from integrated systems are logged as received: the identity provider's `error` string (`OpenIdConnectRelyingParty`) and Nafath's verification reference. Neither comes from an end user; a hostile or broken provider could still put a line break into a plain-text log line | TASK-083 (redaction) | Open |
| F-5 | The workbook's check names a SIT deployment; none exists yet. The scans ran against the DEV compose stack built from this branch, with the SPA's production build under `vite preview`, which adds no headers of its own. Rerun `zap/` against SIT when it exists; F-1's header alerts depend on how SIT serves the SPA | Delivery team (TASK-018) | Open |
| F-6 | The local compose database fails the seed's integrity check (30 `audit_activity.audit_event` rows reference projects removed by earlier live checks), so `docker compose up` stops at `seed`. Local data only; the live checks here used a scratch database | Local environment | Open |
| F-7 | DocumentManagement's integration tests run in no CI step (`ci-quality-gates.yml` has no `…Integration.DocumentManagement` filter). This task's upload test runs in its own step; the module's own suite does not | Owner of `ci-quality-gates.yml` | Open |

## 8. Change log

| Date | Change |
| --- | --- |
| 2026-10-14 | Built and verified (this record) |

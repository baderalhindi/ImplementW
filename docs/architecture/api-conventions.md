# API Conventions — REST and OpenAPI Style Guide

| Field | Value |
| --- | --- |
| Task | TASK-009 — Define Platform API & Event Contract Conventions (P1 - Architecture Decisions) |
| Depends on | TASK-007 — Define Three-Tier Solution Architecture & Module Boundaries (`docs/architecture/solution-architecture.md`, ADR-003, PROPOSED — PENDING AHDA APPROVAL) |
| Record date | 2026-09-20 |
| Status | **RATIFIED (delivery team) — PROVISIONAL on ADR-002 and ADR-003 approval.** TASK-009 has no Architecture Decisions register row and no gate note; the conventions are the Engagement Architect's to ratify (§12). Every path segment derives from ADR-003's module and aggregate names, and two mechanisms (ASP.NET Core `ProblemDetails`, the Npgsql `xmin` ETag) from ADR-002; if either record is replaced, §4 is re-issued |
| Decision owner | Engagement Architect |
| Branch | `chore/task-009-task-009-api-conventions` |
| Deliverables | This record (the OpenAPI style guide); `event-conventions.md` (the event-schema style guide); `api-conventions-samples.openapi.json` (three sample endpoint definitions, §7); `contract-check.py` (the lint, §8); `event-envelope.schema.json` and `event-samples/` (three sample events, with `event-conventions.md`) |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), sheets Implementation Plan (112 rows, TASK-001–TASK-112), Architecture Decisions (ADR-001–ADR-013), Release Checklist, Open Questions, as exported 2026-09-20 |
| Controlled sources and authority order | per TASK-001 |

## 1. What this record is

TASK-007 fixed *which* calls exist between modules and *which* mechanism each uses; it deliberately did not fix what any of them look like on the wire (solution architecture §2: "TASK-009 fixes what they look like on the wire"). TASK-008 fixed the physical shape of every fact, including the hooks the wire has to respect: uuid identities (D-1), money with no currency column (D-5), bilingual pairs (D-6), narrative with a language tag (D-7), `revision_no` on every approvable aggregate (D-15), `xmin` concurrency (D-16), and the single transactional outbox (D-17).

This record is the wire. It has three parts:

1. **The REST conventions** (§4) — 55 rules, R-1 to R-55, covering the seven concerns the TASK-009 row enumerates: resource naming, versioning, the error envelope, pagination and filtering, idempotency keys for sensitive writes, correlation-id propagation, and the OpenAPI document that carries all of it. The typed-event contract is the eighth concern and has its own record, `event-conventions.md`, because it is read by a different audience (module authors writing handlers, not endpoint authors).
2. **The three sample endpoint definitions** the TASK-009 validation check asks for (§7), written as a real OpenAPI 3.1 document and linted rather than reviewed by eye.
3. **The lint** (§8) — `contract-check.py`, thirteen checks C-1 to C-13 over any OpenAPI 3.x JSON document, plus four architecture tests A-7 to A-10 that extend the TASK-007 suite. Together they are the "lint rule or checklist" of the acceptance criterion, and the lint is what makes the checklist re-checkable on every commit.

The record is ratified at delivery-team level and held provisional on ADR-002/ADR-003, for the reason in the header. Nothing here needs an AHDA decision: the API has one first-party client (the SPA) and no AHDA system consumes it at launch (§4.2).

## 2. Scope

| | Subject | Owner |
| --- | --- | --- |
| **Decides** | URL structure, resource naming, HTTP method semantics, command endpoints | This record §4.1 |
| **Decides** | Versioning and the definition of a breaking change | §4.2 |
| **Decides** | JSON representation: identifiers, dates, money, bilingual labels, narrative, enums, null/masked/unknown, concurrency tokens | §4.3 |
| **Decides** | The error envelope and the status-code catalogue | §4.4 |
| **Decides** | Pagination, filtering and sorting | §4.5 |
| **Decides** | Idempotency-key handling and the definition of a sensitive write | §4.6 |
| **Decides** | Correlation-id propagation | §4.7 |
| **Decides** | The OpenAPI document's structure and the lint that enforces it | §4.9, §8 |
| **Decides** | The cross-module event contract | **`event-conventions.md`** |
| **Does not decide** | Which endpoints exist in each module | Each Backend task row (TASK-031 to TASK-112) — they apply these rules |
| **Does not decide** | Authorization decisions, scopes, field-level masking taxonomy | TASK-030, ADR-010 (UGV-01). §4.3 R-20 fixes only *how* a masked field appears |
| **Does not decide** | Per-field validation rules, output encoding | TASK-077 — it uses the §4.4 envelope for its results |
| **Does not decide** | Rate limiting, CORS, security response headers | TASK-078 — §4.8 fixes only the 429 envelope |
| **Does not decide** | OpenAPI generation tooling, hosting and the docs UI | TASK-094 (ADR-002 §4.1: generated from code, never hand-maintained). §9 pulls the *generation* step forward into the CI gate |
| **Does not decide** | Index design | TASK-026 — §4.5 R-33 hands it the obligation list |
| **Does not decide** | Authentication protocol, token lifetime, MFA | TASK-028, TASK-029 |

## 3. Sources — what was read and what could not be

The TASK-009 description names **Blueprint Section 19** as the source of API-01 through API-10 and **Step 14A** as the source of the INT-001-030 and EVT-001-024 families. Neither document is obtainable: the Blueprint and the Step 14A review are held neither in the repository nor in the connected Google Drive (TASK-001 §5.1, re-verified by TASK-007 S-1 and TASK-008 §3 on 2026-09-20, and again for this record).

What the workbook does state of Section 19:

| Rule | Text as the workbook restates it | Where it is implemented |
| --- | --- | --- |
| **API-01** | The frontend never writes to the database directly; all access goes through the business-logic API (TASK-007 §4.1, ADR-002 §5 point 3) | Structural — TASK-007 T-1 to T-6. This record adds nothing to it; every rule below presupposes it |
| **API-03** | No direct cross-domain database writes; a cross-module write is a typed application-service call (TASK-007 M-3, "This is API-03/P-08") | Structural — TASK-007 M-3, A-2, A-5. On the wire, R-4 (one command endpoint per transition) and `event-conventions.md` are its consequences |
| API-02, API-04 to API-10 | **Not obtainable.** No workbook cell quotes or paraphrases them | The seven concerns the TASK-009 row enumerates are treated as the content of the remaining eight rules and are covered in §4.1–§4.9. Reconciling the numbering is S-1 |

The conventions are therefore built from the next-best sources under the TASK-001 order: the TASK-009 row's own enumeration, the 34 Backend/Testing/Integration/Full-stack rows that state wire-visible requirements (idempotent callbacks, terminal-state errors, explicit Unknown/N/A, semantic-state metadata, masked exports, 429 throttling, correlation-traceable retries), ADR-003's edge register, and the ERD's column-level conventions. Each rule cites the row or convention it comes from. Nothing below is quoted from Section 19.

## 4. The rules

Each rule states what it is and, where it is not obvious, why. §8 says which rules the lint checks (C-1 to C-13) and which the architecture tests check (A-7 to A-10); a rule with no check is enforced by code review against the §8.3 checklist.

### 4.1 URL structure and resource naming

| # | Rule |
| --- | --- |
| **R-1** | **Base path `/api/v1/`.** Every module's endpoints share it. There is no module segment in the path: the module is an internal boundary (ADR-003), not a client concern, and the SPA composes several modules on one screen (TASK-007 T-6). The module is carried by the OpenAPI tag (R-51). |
| **R-2** | **A collection is the kebab-case plural of the ERD entity name** (D-4: entity = PascalCase of the table): `Risk` → `/risks`, `ManagementConcern` → `/management-concerns`, `ChangeRequest` → `/change-requests`, `ProjectBaseline` → `/project-baselines`. An item is `/{collection}/{id}` with the uuid primary key (D-1). Business identifiers — the Formal Project ID, catalogue codes — are never path segments; they are filters (R-31). The 121 ERD table names are unique across all schemas, so no collection name collides. |
| **R-3** | **Nesting is one level deep and only under an aggregate root**: `/risks/{riskId}/assessment-versions`, `/documents/{documentId}/versions`. Never deeper. The project is *not* a path prefix: `/projects/{projectId}/risks` would put every project-scoped aggregate two levels down and duplicate the `projectId` filter every collection already takes (R-31). Data-scope authorization does not depend on the path — the engine receives the scope anchor as a value (TASK-007 M-7) and filters the collection server-side (TASK-030). |
| **R-4** | **Lifecycle transitions are command endpoints**: `POST /{collection}/{id}/{command}`, where `{command}` is an imperative kebab-case verb — `submit`, `withdraw`, `activate`, `suspend`, `resume`, `apply-change-authorization`. It is the only non-noun path segment permitted, and one command exists per transition, because the workbook requires transitions to be "only reachable through the documented command, never implicitly" (TASK-041) and separately auditable (TASK-062). A command returns 200 with the updated representation, or 202 for asynchronous work (R-7). A command is always a sensitive write (R-35). |
| **R-5** | **Methods.** `GET` reads (safe, no side effects). `POST` creates (201 + `Location`) or executes a command (R-4). `PUT` replaces the editable representation in full and requires `If-Match` (R-21). `DELETE` exists only for ERD delete classes `HARD_DRAFT`, `HARD_OWNER` and `HARD_WORKING` (ERD §8) — a `RETAIN` or `APPEND_ONLY` aggregate has no `DELETE`; it has a terminal command (`withdraw`, `retire`, `close`). **`PATCH` is not used.** JSON Merge Patch's null-means-remove semantics conflict with the paired columns of D-6/D-7 and with the rule that an absent value is an explicit null, never a default (R-20, TASK-052); a partial update is a command or a `PUT`. |
| **R-6** | **Casing.** Path segments kebab-case; query parameters and JSON properties camelCase; headers as named in §4.6–§4.7. |
| **R-7** | **Asynchronous work** — report jobs, exports, projection rebuilds — returns `202 Accepted` with `Location: /api/v1/report-jobs/{id}`. The job is a resource whose `status` is the ERD lifecycle (`REQUESTED · VALIDATING · QUEUED · RUNNING · COMPLETED · FAILED · CANCELLED · EXPIRED`, TASK-071). Its output is a separate `GET` that is authorized at download time with the same rules as the underlying data (TASK-071). |
| **R-8** | **Binary endpoints** are the only non-JSON surface: upload is `POST` `multipart/form-data` (WF-12, TASK-037); download is `GET .../content` returning `application/octet-stream` with `Content-Disposition`. Both are marked `x-binary` in OpenAPI (R-52). A document in `SCAN_PENDING` or `QUARANTINED` returns 409 `DOCUMENT_NOT_AVAILABLE` on the evidence path (TASK-037). |

### 4.2 Versioning

| # | Rule |
| --- | --- |
| **R-9** | **The major version is in the path** (`/api/v1/`) and **one major version is live at a time.** No header or query-string versioning, no per-resource versions. |
| **R-10** | **A breaking change is any of:** removing an endpoint, a response property or an enum value; changing a property's type, format or nullability; adding a required request property; changing a status code an operation returns; changing the meaning of a property without renaming it; tightening validation on an existing property. **Non-breaking:** adding an endpoint, an optional request property, a response property, a new error `code`, or an enum value — the last is allowed only because the SPA and the API ship as one release (R-11); it is still listed in the release note. This is the list the contract tests of TASK-043, TASK-054, TASK-059 and TASK-065 fail on. |
| **R-11** | **Within the engagement, v1 may change breakingly only when the SPA and the API ship in the same release and no external consumer is registered for the affected endpoint.** The platform has one first-party client; the only other callers are provider callbacks (SMS delivery receipts, TASK-103) and the "internal/future systems" TASK-075 anticipates. An external consumer is *registered* when an `IntegrationDefinition` with `direction` INBOUND or BIDIRECTIONAL (ERD, FG-05) names the endpoint. From that moment the endpoint is frozen: a breaking change requires `/api/v2/` for the whole surface and a migration window. The contract test snapshot (`docs/api/openapi.v1.json`, §9 row 5) is updated deliberately in the same PR as the break, which is what makes a break visible rather than accidental. |
| **R-12** | **Deprecation** is marked `deprecated: true` on the operation and answered with a `Deprecation: true` response header. A deprecated operation is removed no earlier than the release after every registered consumer has migrated. |

### 4.3 Representation

| # | Rule |
| --- | --- |
| **R-13** | **JSON, UTF-8, camelCase.** Requests and 2xx responses are `application/json`; every non-2xx response is `application/problem+json` (R-23). No vendor media types, no XML, no envelope around a single resource — the resource *is* the body. |
| **R-14** | **Identifiers and references.** Every resource has `id` (uuid, D-1). A reference to another aggregate is `<entity>Id` — an identifier, never an embedded object (TASK-007 M-4). One exception, for display: a response may embed a *summary* of a referenced entity — `projectType: { id, code, name }`, `owner: { id, displayName }` — named without the `Id` suffix and using a shared `<Entity>Ref` schema. It is never the full representation and never accepted on input. |
| **R-15** | **Timestamps** are RFC 3339 in UTC with the `Z` suffix (`2026-09-20T09:12:44Z`), OpenAPI `format: date-time`. **Dates** are `YYYY-MM-DD`, `format: date`. Local time never crosses the wire; the SPA converts for display. |
| **R-16** | **Money (ADR-008, ERD D-5).** A decimal **string** with exactly two fraction digits, in a property whose name ends in `Sar`: `"approvedBudgetSar": "12500000.00"`. There is **no currency property** anywhere — the currency is the type's constant, exactly as it is in the schema. Why a string: `numeric(18,2)` holds values above 2^53, which a JSON number loses in every JavaScript client. The shared schema is `MoneySar` (`^-?[0-9]{1,16}\.[0-9]{2}$`). |
| **R-17** | **Bilingual labels (ADR-012, ERD D-6)** are an object under the base name: `"name": { "ar": "…", "en": "…" }`, shared schema `BilingualLabel`, both properties required wherever the columns are `not null`. Never two flat properties `nameAr`/`nameEn` — the pair is one value. |
| **R-18** | **Narrative (ERD D-7, TASK-109)** is an object `"description": { "text": "…", "lang": "ar" }`, shared schema `Narrative`, `lang` ∈ `ar` \| `en`. The client sends `lang`; the server never infers it and never translates. |
| **R-19** | **Enumerations** are UPPER_SNAKE_CASE strings. A lifecycle property is named `status` (or `lifecycleState` for the D-12 governed artefacts, mirroring the column) and its values are **exactly** the ERD §6 state names — the API must not introduce a second vocabulary for a state. Enum schemas are named `<Entity>Status` and shared between request filters and responses. |
| **R-20** | **Absent, masked and projected values are three different things.** (a) A value that does not exist is `null` — never `0`, `""`, `false` or a default (TASK-052: "an explicit Unknown/N/A, never 0"). (b) A field withheld from this caller by ADR-010 classification is **omitted** from the representation and its name is listed in `maskedFields: [...]`, so the SPA renders *restricted* rather than *empty*; every representation that can carry a classified field has `maskedFields`. (c) A projection consumed by FG-01/FG-02 carries `projection: { semanticState, freshness, asOf, coverage }` with `semanticState` ∈ `CURRENT_LIVE` \| `PUBLISHED_OFFICIAL` \| `HISTORICAL_SNAPSHOT`, `freshness` ∈ `FRESH` \| `STALE` \| `UNKNOWN`, `asOf` date-time or null, `coverage` ∈ `COMPLETE` \| `PARTIAL` \| `NONE` (TASK-069: "explicitly Unknown/Stale in the payload, never coerced"). This object is the "projection metadata contract" TASK-069 delivers; it is defined once in `Application/Common` (M-10). |
| **R-21** | **Revision and concurrency.** Every approvable aggregate exposes `revisionNo` (D-15). Every `GET` of a single mutable resource returns an `ETag` derived from PostgreSQL `xmin` (D-16) — opaque to the client. `PUT` **requires** `If-Match`: absent → 428 `PRECONDITION_REQUIRED`; stale → 412 `PRECONDITION_FAILED`. Commands accept `If-Match` and honour it when present. Invariants that must hold under concurrency — one Active baseline (TASK-046), one ActiveSuspension (TASK-062) — are database constraints, not ETags; the ETag protects the *user's* edit, the constraint protects the *fact*. |
| **R-22** | **Collections return `<Entity>Summary`; items return `<Entity>Detail`.** They are distinct named schemas even when they overlap, so a register screen never pays for a detail payload and a detail change never breaks a register. No anonymous or inline object schemas in any response (R-54). |

### 4.4 Error envelope

| # | Rule |
| --- | --- |
| **R-23** | **Every non-2xx response is RFC 9457 Problem Details** (`application/problem+json`) with the platform's extensions, using **one schema, `ProblemDetails`, on every endpoint.** ASP.NET Core produces it natively (ADR-002), which is why the standard is adopted rather than a bespoke envelope. Fields are in the table below. |
| **R-24** | **Status codes** come from the catalogue below and nowhere else. The split between 400 and 422 follows TASK-007 T-3: *shape* is validated in `PMPlatform.Api` (400), *meaning* in the module (422). 409 is reserved for state: a transition the lifecycle does not allow, a terminal state, a violated invariant, or an idempotency key still in flight. |
| **R-25** | **`title` and `detail` are developer-facing English and are never shown to a user verbatim.** The contract is `code`: the SPA maps it to bilingual text (ADR-012 keeps the interface bilingual; the API stays out of localisation). `detail` and `errors[].message` never echo a user-supplied value — that is both a redaction rule (TASK-083) and an injection rule (TASK-077). |
| **R-26** | **A 500 carries `code`, `correlationId`, `timestamp` and nothing else.** No exception type, message or stack trace leaves the server in any environment; the correlation id is how the operator finds it in APM (TASK-090). |
| **R-27** | **Error codes are a registry.** Platform codes (the catalogue) live in `Application/Common/Errors`; module codes are prefixed with the module in UPPER_SNAKE — `SCHEDULE_SINGLE_ACTIVE_BASELINE`, `SUSPENSION_ALREADY_ACTIVE`, `CLOSURE_PROJECT_CLOSED` — and are declared in the module's `Contracts` and on the operation as `x-error-codes` (R-52). A code, once shipped, is never reused with a different meaning. |

**`ProblemDetails` fields.** The TASK-009 validation check asks that the correlation id and the idempotency key be present in the envelope schema; both are required properties, and C-6 fails the build if either is dropped.

| Property | Type | Required | Content |
| --- | --- | --- | --- |
| `type` | string | yes | `urn:pmplatform:problem:<code-in-kebab-case>` — a stable identifier, not a resolvable URL |
| `title` | string | yes | Short English summary of the `code` |
| `status` | integer | yes | The HTTP status, repeated |
| `detail` | string | no | Developer-facing, redacted (R-25); absent on 500 |
| `instance` | string | yes | Request path without query string |
| `code` | string | yes | UPPER_SNAKE machine code — the contract (R-25) |
| `correlationId` | uuid | yes | R-41 |
| `idempotencyKey` | uuid or null | yes | Echo of the request `Idempotency-Key`; null when the request carried none |
| `timestamp` | date-time | yes | Server time, UTC |
| `errors[]` | `{ field, code, message? }` | on 400/422 | `field` is the dotted path into the body, or the parameter name; `code` is a validation code (`REQUIRED`, `MAX_LENGTH`, `ENUM_VALUE`, `DATE_BEFORE_START`, …) |

**Status-code catalogue.**

| Status | `code` | When | Source |
| --- | --- | --- | --- |
| 400 | `VALIDATION_FAILED` | Body, query or header fails shape validation; `errors[]` lists each field | TASK-077, T-3 |
| 400 | `IDEMPOTENCY_KEY_REQUIRED` / `IDEMPOTENCY_KEY_INVALID` | Sensitive write without a key, or key not a uuid | R-36 |
| 401 | `AUTHENTICATION_REQUIRED` | No or invalid token. Generic — no user enumeration | TASK-028 |
| 403 | `PERMISSION_DENIED` | The caller may see the resource but lacks the permission for this operation | TASK-030 |
| 403 | `STEP_UP_REQUIRED` | The operation needs a fresh authentication context | TASK-029, ADR-010 |
| 404 | `NOT_FOUND` | The resource does not exist **or is outside the caller's data scope** (R-47) | TASK-066 |
| 409 | `INVALID_TRANSITION` | The command is not allowed from the current lifecycle state | TASK-041, TASK-060 |
| 409 | `TERMINAL_STATE` | Any write against a CLOSED project or other terminal aggregate ("an explicit terminal-state error") | TASK-063 |
| 409 | `INVARIANT_VIOLATED` or a module code | A single-instance rule: second Active baseline, second ActiveSuspension, second current accepted achievement | TASK-046, TASK-050, TASK-062 |
| 409 | `IDEMPOTENCY_KEY_IN_FLIGHT` | Same key, first request still executing | R-37 |
| 409 | `DOCUMENT_NOT_AVAILABLE` | Evidence path on a `SCAN_PENDING`/`QUARANTINED` document | TASK-037 |
| 412 | `PRECONDITION_FAILED` | `If-Match` does not match the current ETag | R-21 |
| 413 / 415 | `PAYLOAD_TOO_LARGE` / `UNSUPPORTED_MEDIA_TYPE` | Uploads | TASK-037 |
| 422 | `BUSINESS_RULE_VIOLATED` or a module code | Well-formed request rejected by a domain rule: end date before start, severity supplied by the client (TASK-057), out-of-allowlist report field (TASK-071) | T-3 |
| 422 | `CONFIGURATION_MISSING` | Required configuration absent — the fail-closed rule; the operation does not default | TASK-034, Blueprint Section 12 |
| 422 | `IDEMPOTENCY_KEY_REUSED` | Same key, different request fingerprint | R-37 |
| 428 | `PRECONDITION_REQUIRED` | `PUT` without `If-Match` | R-21 |
| 429 | `RATE_LIMITED` | Throttled; `Retry-After` header present | TASK-078 |
| 500 | `INTERNAL_ERROR` | Unhandled; minimal body (R-26) | TASK-090 |
| 503 | `UNAVAILABLE` | Readiness failing (database unreachable) | TASK-091 |

### 4.5 Pagination, filtering and sorting

| # | Rule |
| --- | --- |
| **R-28** | **Every collection `GET` is paged.** An unpaged collection is permitted only for a small closed set (roles, a catalogue's items) and is marked `x-unpaged` with the reason; C-9 flags everything else. |
| **R-29** | **Offset paging** is the default and serves every register screen (SCR-025, SCR-080, SCR-083, …; TASK-026): `page` (1-based, default 1) and `pageSize` (default 25, max 200). Envelope: `{ "items": [...], "page": 1, "pageSize": 25, "totalCount": 132 }`. Schema name `<Entity>Page` (R-54). |
| **R-30** | **Cursor paging** serves append-only streams where a total is meaningless and an offset drifts: audit events, business activity, notification history. `cursor` (opaque, base64) and `pageSize`. Envelope: `{ "items": [...], "nextCursor": "…" \| null }`. |
| **R-31** | **Filtering is one query parameter per filter, named as the property it filters.** Equality `status=ACTIVE`; a set is comma-separated `status=ACTIVE,SUSPENDED` (OpenAPI `style: form, explode: false`); a range is `<property>From` / `<property>To`, inclusive; free text is `q` over the fields the operation documents; booleans are `true`/`false`. **There is no filter expression language** — no `filter=` DSL, no operators in values — for the same reason SCR-138 is an allowlisted explorer and not a query builder (TASK-071, TASK-112). |
| **R-32** | **Sorting** is `sort=<property>:asc\|desc[,<property>:asc\|desc]`, restricted to the properties the operation declares in `x-sortable`; every collection declares a default sort; the server appends `id` as the tiebreak so pages are stable. |
| **R-33** | **Every `x-filterable` and `x-sortable` property is an index obligation.** The OpenAPI document is therefore the input to TASK-026's indexing strategy: its acceptance criterion ("every register/list screen's default filter and sort columns are covered by a composite index") is checked against these declarations, not against screen mock-ups. |

### 4.6 Idempotency

| # | Rule |
| --- | --- |
| **R-34** | **Header `Idempotency-Key`**, a client-generated UUID v4, one per logical operation. A retry — network failure, timeout, user double-click — reuses the same key; a new operation gets a new key. The SPA's HTTP client generates it for every `POST` and `PUT`. |
| **R-35** | **Every `POST` and `PUT` is a sensitive write unless declared otherwise.** The declaration is `x-write-class: sensitive \| non-sensitive` on the operation (from the `[SensitiveWrite]` / `[NonSensitiveWrite("reason")]` action attributes, A-7); `non-sensitive` requires `x-write-class-reason`. A command endpoint (R-4) is always sensitive. A **sensitive write** is any operation that creates or transitions an aggregate with a lifecycle (ERD §6), writes a financial fact (a `*_sar` column), issues, applies or decides an approval, authorization or contribution, changes an identity, access or published-configuration artefact, or emits a mandatory audit class (PTBC-011) or a `NotificationIntent`. The enumerated **non-sensitive** writes are: draft autosave `PUT`s (periodic-update session items, TASK-107), user preferences and dashboard personalisation (TASK-111), saved views (TASK-112), marking a notification read, and provider callbacks whose headers the platform does not control (SMS delivery receipts, TASK-103 — deduplicated on the provider's message id instead). Authentication endpoints are non-sensitive here because they are rate-limited (TASK-078) and have no business effect to replay. |
| **R-36** | A sensitive write **without** the header is rejected with 400 `IDEMPOTENCY_KEY_REQUIRED`; a malformed key with 400 `IDEMPOTENCY_KEY_INVALID`. It is never silently accepted. |
| **R-37** | **Scope and replay.** A key is scoped to (authenticated principal, request path). The server stores the key, a SHA-256 fingerprint of the body, and the response (status, headers, body) for **24 hours**. Same key, same fingerprint → the stored response is returned with `Idempotent-Replayed: true` and no side effect. Same key, different fingerprint → 422 `IDEMPOTENCY_KEY_REUSED`. Same key while the first request is still executing → 409 `IDEMPOTENCY_KEY_IN_FLIGHT`. |
| **R-38** | **The store is a table, written in the same transaction as the write** — `common.idempotency_record` (specified in §9 row 4 for the ERD; it is not an in-memory cache because Cloud Run/GKE run more than one replica and a restart must not forget a key). A rolled-back write stores nothing, so the retry executes — which is the correct outcome. Records are purged at `expires_at` by a background job; none of the six D-3 delete classes describes an expiring infrastructure row, so the ERD re-issue assigns one (S-3). |
| **R-39** | **Two idempotency layers, not one.** The HTTP key protects against *client* retry of an endpoint. The per-aggregate keys the ERD already carries — `ChangeAuthorization.idempotency_key` (TASK-060), `ApprovalInstance.outcome_idempotency_key` (TASK-035), `NotificationIntent (source_event_type, source_reference)` (TASK-057), `SourceApplication.idempotency_key` (TASK-066) — protect against *internal* retry of a cross-module call or event, and are governed by `event-conventions.md` EV-4. The HTTP key is **never** propagated into an event or a cross-module call; a module derives its internal key from its own aggregate identity. |
| **R-40** | `GET` and `DELETE` ignore the header. `DELETE` is idempotent by definition: a second delete of the same id returns 204. |

### 4.7 Correlation

| # | Rule |
| --- | --- |
| **R-41** | **Header `X-Correlation-Id`**, a uuid. The SPA generates one per user action and sends it; the server accepts it if it is a valid uuid, otherwise generates one; **every response echoes it**, 2xx and error alike (C-12). |
| **R-42** | **Propagation.** The id is bound to the logging scope of the request (every log line, TASK-083 redaction applies to the line, not the id), the `ProblemDetails` body, `AuditEvent.correlation_id` (ERD), the event envelope (`event-conventions.md` EV-2), `Invocation.correlation_id` and `SourceApplication.correlation_id` (ERD), every outbound integration call that accepts a header (TASK-075: "retries are idempotent and correlation-ID traceable end-to-end"), and the APM span as an attribute (TASK-090: an exception is "correctly attributed to the originating request/correlation ID"). |
| **R-43** | **W3C `traceparent` is not the correlation id.** ASP.NET Core and OpenTelemetry propagate `traceparent` for distributed tracing; it is a tracing concern, is not stored on any business row, and is not exposed to the SPA as a contract. The correlation id is attached to the trace as an attribute so the two can be joined in APM. |
| **R-44** | **Background work continues the correlation of what caused it.** An outbox dispatch (event-conventions EV-6) runs under the producer's correlation id; a scheduled job (reminders, projection rebuilds) starts a new one and every event it produces carries the job's id as `causationId`. |
| **R-45** | **The correlation id and the idempotency key are never derived from each other** and never equal. One identifies a *trace*, the other an *operation*; a retried operation has one key and several correlation ids. |

### 4.8 Authorization surface

| # | Rule |
| --- | --- |
| **R-46** | Credentials travel only in `Authorization: Bearer <token>`. No token, key or credential in a query string or a body; no cookie-based session for the API (the SPA holds the token). |
| **R-47** | **Least disclosure on 403 versus 404.** A resource outside the caller's data scope — another department under DEPT scope, another entity under ENTITY scope (R08), a project the caller is not assigned to — returns **404 `NOT_FOUND`, identical to a nonexistent id.** 403 is returned only when the caller may see the resource and lacks the permission for this operation. This is what makes TASK-066's criterion — an R08 user "can never query or infer data outside their explicit ENTITY/Project grant" — hold for the single-item endpoints, where a 403 would confirm existence. The threat-model review confirms the rule (S-4). |
| **R-48** | Rate limiting, CORS and security response headers are TASK-078's. The only contract fixed here is that a throttled request answers 429 with the R-23 envelope and a `Retry-After` header. |

### 4.9 The OpenAPI document

| # | Rule |
| --- | --- |
| **R-49** | **Generated from code, OpenAPI 3.1**, `info.version` = the release version. It is produced in the CI quality gate on every PR (TASK-015, §9 row 2) and linted there; TASK-094 hosts the artifact from `main`. It is never edited by hand; a fix is a code change. |
| **R-50** | **`operationId` is `<Module>_<Action>`**, PascalCase both sides, unique: `Project_CreateProject`, `Risk_ListRisks`, `ChangeRequest_Submit`. The generated TypeScript client (TASK-011 frontend workspace) is named from it. |
| **R-51** | **Exactly one tag per operation, the module name** from TASK-007 §4.3 (`ProjectTask`, not `Task` — TASK-007 §4.4b), mirrored in `x-module`. The tag list is the 21 modules; the docs UI groups by it. |
| **R-52** | **Extensions.** `x-module`; `x-write-class` and `x-write-class-reason` (R-35); `x-error-codes` (R-27); `x-sortable` and `x-filterable` (R-32, R-33); `x-unpaged` (R-28); `x-binary` (R-8). Each is emitted from a C# attribute so the document and the runtime cannot disagree. |
| **R-53** | **Responses.** Every operation documents its 2xx responses with their headers (`X-Correlation-Id` always; `Location` on 201; `ETag` where R-21 applies; `Idempotent-Replayed` on sensitive writes), every specific 4xx it returns, and a `default` response — all non-2xx referencing `ProblemDetails`. `default` is mandatory so that no operation is documented as unable to fail. |
| **R-54** | **Schema names** are PascalCase and follow: `<Entity>Summary`, `<Entity>Detail`, `<Entity>Ref`, `<Entity>Status`, `<Entity>Page`, `<Entity>CreateRequest`, `<Entity>UpdateRequest`, `<Entity><Command>Command` (`ChangeRequestSubmitCommand`); shared value types `BilingualLabel`, `Narrative`, `MoneySar`, `ProjectionMeta`, `ProblemDetails`, `ValidationError`. No inline object schema in a response; every request body is a named schema. |
| **R-55** | **Examples.** Every request schema and every 2xx response carries an example; `ProblemDetails` carries one per status class. Examples are what the docs UI (TASK-094) and the contract tests (TASK-043) render and replay. |

## 5. The sensitive-write register

R-35 is a default, not a list, because the endpoint set does not exist yet. What can be fixed now is the *class* each kind of endpoint falls into, so that a module author does not decide it endpoint by endpoint:

| Endpoint kind | Class | Reason |
| --- | --- | --- |
| Create an aggregate root (`POST /{collection}`) | sensitive | Creates a lifecycle row (ERD §6); audited |
| Command (`POST /{collection}/{id}/{command}`) | sensitive, always | Lifecycle transition; the audit and notification trigger (R-4) |
| Replace an editable representation (`PUT /{collection}/{id}`) on a submitted or approved aggregate | sensitive | Creates a revision (D-15) |
| Replace a DRAFT (`PUT`) | sensitive | Still audited (HARD_DRAFT rows are audited on delete, so on edit too) — except the autosave PUTs below |
| Financial write of any kind | sensitive | ADR-008 provenance; `*_sar` column |
| Approval decision, delegation; ChangeAuthorization issue/apply; contribution accept/return/reject/apply | sensitive | The workbook's own idempotency criteria (TASK-035, TASK-060, TASK-066) |
| Identity, role, profile, master data, configuration publication | sensitive | Mandatory audit classes (TASK-033, TASK-110) |
| Document upload, version, link, unlink | sensitive | Evidence (TASK-037) |
| Periodic-update session item autosave (TASK-107) | non-sensitive | Working state; the submit command is the sensitive step |
| Dashboard personalisation (TASK-111), notification preferences, mark-read | non-sensitive | `HARD_OWNER` convenience rows |
| Saved view create/update/share (TASK-112) | non-sensitive | Configuration only; every viewer is authorised at execution |
| Authentication, token refresh, MFA challenge | non-sensitive | Rate-limited (TASK-078); no business effect to replay |
| Provider callbacks (SMS delivery receipt, TASK-103) | non-sensitive | Header not under platform control; deduplicated on provider message id |

## 6. Mapping to Blueprint Section 19

| Concern named in the TASK-009 row | Rules | Section 19 rule |
| --- | --- | --- |
| Three-tier access (API-01) | TASK-007 T-1 to T-6; presupposed here | **API-01** (workbook) |
| No direct cross-domain writes (API-03) | TASK-007 M-3; R-4; event-conventions | **API-03** (workbook) |
| REST resource naming | R-1 to R-8 | not obtainable (S-1) |
| Versioning scheme | R-9 to R-12 | not obtainable (S-1) |
| Standard error-response envelope | R-23 to R-27 | not obtainable (S-1) |
| Pagination and filtering conventions | R-28 to R-33 | not obtainable (S-1) |
| Idempotency-key handling for sensitive writes | R-34 to R-40, §5 | not obtainable (S-1) |
| Correlation-ID propagation | R-41 to R-45 | not obtainable (S-1) |
| Typed-event contract format | `event-conventions.md` EV-1 to EV-12 | not obtainable (S-1); INT/EVT families S-2 |

The mapping is complete over the row's enumeration and incomplete over the numbering. When Section 19 is located, the check is: for each of API-02 and API-04 to API-10, find the rule or rules that implement it and fill the third column; a rule with no implementing convention is added to §4, never inferred.

## 7. Sample endpoint definitions — the validation check

The TASK-009 validation check: *"Review 3 sample endpoint definitions against the style guide for compliance before it is adopted platform-wide; confirm correlation-ID and idempotency-key fields are present in the error envelope schema."*

The three definitions are in `api-conventions-samples.openapi.json`, one per kind of endpoint the rules distinguish, and drawn from the three domains TASK-043, TASK-059 and TASK-065 write contract tests for:

| # | Operation | Kind | Rules exercised |
| --- | --- | --- | --- |
| 1 | `POST /api/v1/projects` — `Project_CreateProject` | Create (sensitive) | R-5 (201 + `Location`), R-14 (`projectType` ref), R-16 (`declaredBudgetSar`), R-18 (`title` narrative), R-20 (`formalProjectId` null until approval, `maskedFields`), R-34/R-35 (`Idempotency-Key` required), R-53 |
| 2 | `GET /api/v1/risks` — `Risk_ListRisks` | Collection | R-22 (`RiskSummary`), R-29 (`RiskPage`), R-31 (set filter `status=…,…`, range `nextReviewDateFrom/To`, `q`), R-32 (`sort`, `x-sortable`), R-33 (`x-filterable`), R-19 (`RiskStatus` = ERD states), R-20 (`currentRating` null until assessed) |
| 3 | `POST /api/v1/change-requests/{changeRequestId}/submit` — `ChangeRequest_Submit` | Command (sensitive) | R-4, R-21 (`If-Match` optional on commands, 412), R-24 (409 `INVALID_TRANSITION`, `TERMINAL_STATE`), R-27 (`x-error-codes`), R-37 (`Idempotent-Replayed`) |

The document also carries the shared `ProblemDetails` schema with `correlationId` and `idempotencyKey` as required properties, and three `ProblemDetails` examples (400 with `errors[]`, 409, 500).

Result of the review, run as a lint rather than by eye:

```
$ python3 docs/architecture/contract-check.py
api-conventions-samples.openapi.json: 3 operations, 3 event samples, 0 findings

$ python3 docs/architecture/contract-check.py --self-test
self-test: 29 mutations, 0 missed
```

The self-test mutates the sample document 23 ways (drop the `Idempotency-Key` parameter, add a `PATCH`, remove `correlationId` from the envelope's required list, replace a 400 with a plain JSON body, unpage a collection, add a `currency` property, …) and 6 ways for events, and asserts that each mutation produces the finding for its rule. That is the TASK-043-style proof that the lint fails when it should, done once here so it does not have to be believed.

## 8. Enforcement

### 8.1 The lint — `contract-check.py`

Standard-library Python, no toolchain beyond what `erd-check.py` already needs, so it drops into the TASK-015 gate without a Node dependency on the backend build. It reads any OpenAPI 3.x JSON document — the sample file by default, the generated `openapi.json` in CI — and exits 1 on any finding.

| # | Checks | Rules |
| --- | --- | --- |
| **C-1** | Document is OpenAPI 3.x; every path starts with `/api/v1/` | R-1, R-9 |
| **C-2** | Static segments kebab-case; parameters `{camelCase}`; depth ≤ 4; a segment after `{id}` is a child collection or a POST-only command | R-2, R-3, R-4, R-6 |
| **C-3** | No `PATCH` operation | R-5 |
| **C-4** | `operationId` matches `<Module>_<Action>` and is unique; exactly one tag, from the 21 modules; `x-module` equals it | R-50, R-51 |
| **C-5** | Every operation has `responses.default`; every non-2xx response is `application/problem+json` → `ProblemDetails` | R-23, R-53 |
| **C-6** | `ProblemDetails` exists; `type`, `title`, `status`, `instance`, `code`, `correlationId`, `idempotencyKey`, `timestamp` are required properties; `errors` exists | R-23, validation check |
| **C-7** | Every `POST`/`PUT` declares `x-write-class`; `sensitive` → required `Idempotency-Key` header parameter; `non-sensitive` → `x-write-class-reason`; a command path is `sensitive` | R-4, R-35 — **the acceptance criterion's idempotency rule** |
| **C-8** | Every `PUT` has a required `If-Match` header parameter | R-21 |
| **C-9** | Every collection `GET` not marked `x-unpaged` takes `page`+`pageSize` or `cursor`+`pageSize`, and its 200 schema is a page envelope | R-28 to R-30 |
| **C-10** | `sort` parameter ⇒ `x-sortable` declared; non-JSON 2xx content ⇒ `x-binary` | R-8, R-13, R-32 |
| **C-11** | Schema names PascalCase; properties camelCase; no inline response schema | R-6, R-22, R-54 |
| **C-12** | Every 2xx response declares `X-Correlation-Id`; every 201 declares `Location` | R-5, R-41 |
| **C-13** | A `*Sar` property is a `MoneySar` decimal string; no `currency` property exists | R-16 |

Alternatives not taken: Spectral (the usual OpenAPI linter) expresses C-1 to C-13 in a ruleset, but it needs Node in the backend gate and could not be executed in this environment to prove the rules fire; a checker that runs today and self-tests was preferred. If TASK-015 later adopts Spectral for the frontend workspace, the ruleset is a transcription of this table and C-1 to C-13 stay the normative list.

### 8.2 Architecture tests — extending the TASK-007 suite

TASK-007 §10 defines A-1 to A-6 in `PMPlatform.Tests.Unit`, authored at TASK-011 and gated by TASK-015. Four more rules join that suite so the lint cannot be satisfied by an attribute the runtime ignores:

| # | Rule asserted | Fails when |
| --- | --- | --- |
| **A-7** | Every `[HttpPost]` or `[HttpPut]` action carries exactly one of `[SensitiveWrite]` or `[NonSensitiveWrite(reason)]`; `[SensitiveWrite]` actions are served by the idempotency filter and emit `x-write-class: sensitive` into OpenAPI | An endpoint is documented as sensitive but does not enforce the key at runtime, or vice versa (R-35) |
| **A-8** | No `[HttpPatch]` action exists | R-5 |
| **A-9** | Every controller derives from `ApiControllerBase`, the one base that registers the `ProblemDetails` exception mapping, the correlation-id middleware contract and the idempotency filter; no `ProblemDetails` is constructed outside `Application/Common/Errors` | A controller answers an error in a shape the envelope does not cover (R-23, R-27) |
| **A-10** | Every public type under `Features/<Module>/Contracts/Events` derives from `EventEnvelope<TData>` and its `eventType` appears in the event catalogue (`event-conventions.md` §4) | An event exists that the catalogue does not list — the event-side counterpart of A-6 |

### 8.3 Review checklist — the PR template (TASK-012)

For anything the lint cannot see. One line each, ticked by the author, checked by the module owner (CODEOWNERS):

- [ ] Resource names are the ERD entity names; no new vocabulary for a state (R-2, R-19)
- [ ] Every lifecycle transition is a command endpoint, one per transition (R-4)
- [ ] `DELETE` only on `HARD_*` aggregates (R-5)
- [ ] Absent → `null`; classified → omitted + `maskedFields`; projections carry `projection` (R-20)
- [ ] New error codes are in the module's `Contracts`, prefixed with the module, and listed in `x-error-codes` (R-27)
- [ ] No `filter=` DSL; every filterable/sortable property declared — and its index requested from TASK-026 (R-31, R-33)
- [ ] Sensitive-write classification of each new `POST`/`PUT` matches §5 (R-35)
- [ ] The HTTP idempotency key is not passed into any event or cross-module call (R-39)
- [ ] A breaking change (R-10) updates `docs/api/openapi.v1.json` in the same PR and is called out in the description (R-11)
- [ ] New events follow `event-conventions.md` EV-1 to EV-12 and are added to its catalogue

## 9. Consequences for the workbook

Specified, not applied — per TASK-001 §4, revisions are re-issued rather than overwritten. Authorisation is Q3 (§12).

| # | Row(s) | Change | Basis |
| --- | --- | --- | --- |
| 1 | **The 34 rows that build or test an API surface:** Backend TASK-031, 034, 035, 037, 039, 041, 044, 046, 048, 050, 052, 055, 057, 060, 062, 063, 066, 068, 069, 071, 073, 075, 104, 105, 106, 108, 109, 110; Integration TASK-103; Full-stack TASK-112; Testing TASK-043, 054, 059, 065 | Add to Acceptance Criteria: *"API surface conforms to TASK-009 `api-conventions.md` and events to `event-conventions.md`; `contract-check.py` passes on the generated OpenAPI document."* Today **2 of the 34** reference the conventions at all — TASK-031 ("consistent error responses per the API conventions") and TASK-043 ("OpenAPI conventions (TASK for API conventions)") — and TASK-043's is a placeholder, not a task id. The acceptance criterion of this task ("every later Backend/API Contracts task … references these conventions") cannot read MET until the other 32 do | Acceptance criterion 2 |
| 2 | **TASK-015** Configure Quality Gates | Add to the gate: generate the OpenAPI document from the built API (`dotnet` tool, one step) and run `contract-check.py` over it; fail on any finding. Generation moves *forward* from TASK-094 (W6) to the gate (W1) — otherwise the lint has nothing to read until the last wave. TASK-094 keeps hosting and the docs UI | R-49, §8.1 |
| 3 | **TASK-011** Initialize Monorepo | Add to deliverables: `ApiControllerBase`, the `[SensitiveWrite]`/`[NonSensitiveWrite]` attributes, the `ProblemDetails` factory in `Application/Common/Errors`, the correlation-id middleware, the idempotency filter, `EventEnvelope<TData>` and the outbox writer in `Application/Common`, and architecture tests A-7 to A-10 alongside A-1 to A-6 (TASK-007 S-8 makes the same request for A-1 to A-6) | §8.2 |
| 4 | **TASK-008** ERD (re-issue) | Add `common.idempotency_record`: `id uuid pk`, `principal_id uuid not null`, `request_path varchar(500) not null`, `idempotency_key uuid not null`, `request_fingerprint char(64) not null`, `response_status smallint`, `response_headers text`, `response_body text`, `started_at timestamptz not null`, `completed_at timestamptz`, `expires_at timestamptz not null`, audit columns; unique `(principal_id, request_path, idempotency_key)`; purged at `expires_at` (delete class to be assigned — no D-3 class covers an expiring infrastructure row). Second infrastructure table beside `outbox_message`; D-17's "only other table outside a module schema" wording changes accordingly | R-38 |
| 5 | **TASK-043, TASK-054, TASK-059, TASK-065** contract tests | Define "contract test" as: (a) `contract-check.py` over the generated document, (b) a schema diff of the generated document against the committed snapshot `docs/api/openapi.v1.json` that fails on any R-10 breaking change, (c) example replay — every `example` in the document is sent to the DEV API and the response validated against its schema. TASK-043's "TASK for API conventions" becomes "TASK-009" | R-10, R-11, R-55 |
| 6 | **TASK-026** Indexing Strategy | Input is the `x-filterable`/`x-sortable` declarations of the generated document, not the screen inventory alone | R-33 |
| 7 | **TASK-012** PR template | Add the §8.3 checklist | §8.3 |
| 8 | **TASK-077** Input Validation | Validation results are returned as `ProblemDetails.errors[]` with the validation codes of R-24; no second error shape | R-23, R-25 |
| 9 | **TASK-078** Rate Limiting | The 429 response uses the envelope with `Retry-After` | R-48 |
| 10 | **TASK-069** Dashboard Projection Service | The "projection metadata contract" deliverable is `ProjectionMeta` as defined in R-20, placed in `Application/Common` | R-20 |
| 11 | **TASK-090** APM | The correlation id is the `X-Correlation-Id` value, attached to the trace as an attribute; `traceparent` is not the correlation id | R-42, R-43 |
| 12 | **TASK-094** API Documentation | Hosts the document the gate already generates and lints; its "manual audit … 5 sampled endpoints" is the example replay of row 5 run against DEV | R-49 |

## 10. Residual items

| # | Item | Owner | Owed by | Consequence if unresolved |
| --- | --- | --- | --- | --- |
| S-1 | **Blueprint Section 19 is not obtainable** (§3). API-01 and API-03 are implemented from the workbook's restatement; API-02 and API-04 to API-10 cannot be quoted, so §6 maps the TASK-009 row's enumeration and not the rule numbers. | PMO Engagement Lead (TASK-007 Q6 / TASK-008 Q6) | Before TASK-011 | The acceptance criterion "implementing API-01 through API-10" is met over the row's enumeration and unverified against the numbered rules. A Section 19 rule outside the enumeration has no convention. |
| S-2 | **Step 14A INT-001-030 and EVT-001-024 are not obtainable.** The event catalogue in `event-conventions.md` §4 is built from ADR-003's edge register and the ERD; its INT/EVT columns are blank. | PMO Engagement Lead | Before TASK-035 | An integration or event the families define and the workbook does not restate has no contract; discovered at build. |
| S-3 | **`common.idempotency_record` is not in the ERD** (§9 row 4). | Engagement Architect | Into the TASK-008 re-issue, before TASK-025 | TASK-025's migration has no table for R-37, and idempotency falls back to memory — wrong under more than one replica. |
| S-4 | **R-47 (out-of-scope → 404) needs the threat-model review's confirmation.** It is the least-disclosure reading of TASK-066; the alternative (403 everywhere) leaks existence across entities. | Security Lead, in the TASK-079-class security review | Before TASK-066 | Either the rule or the TASK-066 authorization-boundary test has to change; changing the rule after WF-13 is built changes every single-item endpoint. |
| S-5 | **Enum values are the ERD's inferred state names** (R-19; TASK-008 E-2: 25 of 53 sets inferred). A rename after code exists is a breaking change under R-10. | Engagement Architect, with TASK-008 E-1 | Before TASK-041 | Renaming a state that a shipped enum carries costs a v1 break under R-11 in every module that exposes it. |
| S-6 | **Spectral not adopted** (§8.1). If the frontend gate adopts it, C-1 to C-13 must be transcribed once and the two kept identical. | DevOps/Platform Lead | At TASK-015 | Two linters with two rule sets. Not a defect today; recorded so the choice is visible. |
| S-7 | **Reminder revalidation needs an edge ADR-003 does not list** (`event-conventions.md` §4, EV-10). TASK-039 requires a scheduled reminder to "revalidate the current source condition before sending"; that is a Notifications → source-module *query*, absent from ADR-003 §8.2, and A-6 fails on the first reminder handler until it is added. | Engagement Architect (ADR-003 re-issue) | Before TASK-039 | Either the edge is added or reminders send on stale conditions — the exact failure TASK-039's validation check tests for. |
| S-8 | **`audit_activity.audit_event` has no dedupe column** (`event-conventions.md` EV-4, §8 row 2). At-least-once dispatch can insert the same audit event twice into an APPEND_ONLY, hash-chained table. | Engagement Architect | Into the TASK-008 re-issue, before TASK-073 | A retried dispatch duplicates an audit row and the hash chain records the duplicate as genuine. |

## 11. Acceptance-criteria check

| # | Criterion | Result |
| --- | --- | --- |
| 1 | A published OpenAPI style guide and an event-schema style guide exist | **MET.** This record (R-1 to R-55, §4; the OpenAPI document rules in §4.9) and `event-conventions.md` (EV-1 to EV-12, the envelope schema `event-envelope.schema.json`, the catalogue). Both are in `docs/architecture` per the task's directory and deliverables cells. |
| 2 | Every later Backend/API Contracts task in this workbook references these conventions instead of inventing its own | **NOT MET — 2 of 34 rows reference them, one by placeholder.** The 34 rows are enumerated and the sentence to add is specified (§9 row 1); this record specifies workbook edits and does not apply them (TASK-001 §4). Reads MET when Q3 is authorised and the re-issue lands. |
| 3 | A lint rule or checklist enforces the standard error envelope and idempotency-key header on all POST/PUT/PATCH endpoints performing sensitive writes | **MET.** `contract-check.py` C-5/C-6 (envelope on every non-2xx response, with `correlationId` and `idempotencyKey` required) and C-7 (every `POST`/`PUT` classified; sensitive ⇒ required `Idempotency-Key`; `PATCH` excluded outright by C-3) run over any OpenAPI document and self-test 29 mutations (§7). A-7 ties the document to the runtime. The §8.3 checklist covers what a document cannot show. Wiring it into CI is TASK-015's (§9 row 2). |

Validation check from the TASK-009 definition:

| Check | Result |
| --- | --- |
| "Review 3 sample endpoint definitions against the style guide for compliance before it is adopted platform-wide" | **DONE (§7).** Three definitions — create, collection, command — in `api-conventions-samples.openapi.json`; 0 findings; 29-mutation self-test 0 missed. |
| "Confirm correlation-ID and idempotency-key fields are present in the error envelope schema" | **CONFIRMED.** `ProblemDetails.required` includes `correlationId` and `idempotencyKey` (R-23 table); C-6 fails the lint if either is removed, and the self-test proves it does. |

## 12. Confirmation required

| # | Question | Answer |
| --- | --- | --- |
| **Q1** | **R-1 to R-55 and the event conventions EV-1 to EV-12** as the binding wire conventions for TASK-011 onward, provisional on ADR-002/ADR-003. | ☐ Ratified  ☐ Amended: ____________ |
| **Q2** | **Three deliberate exclusions:** no `PATCH` (R-5); no filter expression language (R-31); no currency property and money as a decimal string (R-16). Each is cheaper to reverse now than after the first module ships. | ☐ Confirmed  ☐ Reinstate: ____________ |
| **Q3** | **Authorisation for the workbook edits in §9** — the 34-row reference sentence, the TASK-015 generation-and-lint step, the TASK-011 additions, the ERD table, and the contract-test definition. | ☐ Authorised  ☐ Declined  ☐ Partial: ____________ |
| **Q4** | **R-47** — out-of-scope resources answer 404, not 403 — confirmed as the least-disclosure rule the threat model will test against (S-4). | ☐ Confirmed  ☐ 403 everywhere, TASK-066 criterion re-read |

On Q1 the header status becomes **RATIFIED**, still provisional on ADR-002/ADR-003; on those two records' approval the provisional qualifier is removed.

| Role | Decision | Name | Date |
| --- | --- | --- | --- |
| Engagement Architect | Q1, Q2; §4–§8 proposed and, on ratification, binding on TASK-011 onward | | |
| Security Lead | Q4 | | |
| PMO Engagement Lead | Q3: workbook edits authorised / declined | | |

## 13. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-20 | Initial record. 55 REST and OpenAPI rules (R-1 to R-55) over the seven concerns of the TASK-009 row; API-01/API-03 mapped from the workbook, the rest recorded as unobtainable (§3, §6). Sensitive-write register (§5). Three sample endpoint definitions linted with 0 findings and a 29-mutation self-test (§7). Lint C-1 to C-13 in `contract-check.py`, architecture tests A-7 to A-10, PR checklist (§8). Twelve workbook consequences including `common.idempotency_record` (§9). Eight residual items (S-1 to S-8), two of them raised by the companion event record. | Architecture (TASK-009) |

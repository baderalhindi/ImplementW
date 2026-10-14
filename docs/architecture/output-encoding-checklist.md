# Output-Encoding Review Checklist

| Field | Value |
| --- | --- |
| Task | TASK-077 — Implement Platform-Wide Input Validation & Output Encoding (P14 - Security Hardening & Compliance), deliverable "output-encoding review checklist" |
| Control | CTL-11 (`cybersecurity-control-matrix.md`); OWASP ASVS 4.0 V5.3 (output encoding and injection prevention) |
| Record date | 2026-10-14 |
| Use | A reviewer runs §2 against every pull request that adds or changes a screen, an export, a message or a query. §3 is the review of the whole tree on 2026-10-14, the baseline the checks in §2 keep |

Text is stored **as entered** (ADR-012). Nothing escapes it on the way in, because the right encoding depends on where the text ends up: HTML text, an HTML attribute, a URL, a CSV cell, a PDF string, an e-mail header or a SQL statement each need their own. Each place that writes text into one of these contexts is responsible for encoding it. This checklist lists those places. The input side is in `input-validation.md`.

## 1. Rules

| # | Context | Rule | Enforced by |
| --- | --- | --- | --- |
| OE-1 | HTML text and attributes (SPA) | User text reaches the page only as a React text child or an attribute value, which React encodes. Nothing parses a string as HTML: no `dangerouslySetInnerHTML`, `srcDoc`, `innerHTML`/`outerHTML` assignment, `insertAdjacentHTML`, `createContextualFragment`, `DOMParser.parseFromString` or `document.write` | ESLint `no-restricted-syntax` (`src/frontend/eslint.config.js`), CI `frontend` → Lint |
| OE-2 | Code (SPA) | No string is run as code: no `eval`, `new Function`, string timers, `javascript:` URLs | ESLint `no-eval`, `no-new-func`, `no-script-url`, `@typescript-eslint/no-implied-eval` |
| OE-3 | URLs (SPA) | No `href`, `src`, `action` or `formAction` on a DOM element is bound to a value; links are react-router `<Link to>` built from ids. A path from data (a notification's deep link, the sign-in redirect) is followed only if `isAppPath` accepts it: one leading slash, no backslash, no scheme, no control character | ESLint `no-restricted-syntax`; `src/shared/navigation/appPath.ts` and its test |
| OE-4 | Multi-line text (SPA) | Line breaks are shown with CSS `white-space: pre-line`, never by injecting `<br>` | Review |
| OE-5 | JSON (API) | Every response body is serialized by System.Text.Json as `application/json` or `application/problem+json`; no handler writes text into a body by hand. `detail` and `errors[].message` never echo a submitted value (R-25) | `ApiProblem`; `RequestTextValidationTests.TextThatObeysTheRulesIsStoredAndReturnedExactlyAsEntered` |
| OE-6 | SQL | Values reach SQL only as parameters: LINQ, `FromSql`/`SqlQuery`/`ExecuteSql` with an interpolated `FormattableString`, or `DbCommand` parameters. `…Raw` methods take constants or server configuration only. A column, sort or filter name a client sends is resolved through an allowlist, never written into SQL | Review; §3.2 |
| OE-7 | CSV and XLSX exports | A cell whose first visible character is `=`, `+`, `-`, `@` (or the full-width form), or which starts with a tab or carriage return, is prefixed with `'` (XLSX also sets `quotePrefix`). Control characters other than line feed are removed. XLSX text is XML-escaped and written as an inline string, never as a formula | `SpreadsheetSafety`, `CsvReportRenderer`, `XlsxReportRenderer` and their unit tests |
| OE-8 | PDF exports | Text is written as hex glyph ids, never as a literal string, so it cannot close the string or add PDF operators | `PdfReportRenderer` |
| OE-9 | E-mail and SMS | Bodies are plain text (`TextFormat.Plain`). Template placeholders are filled in one pass, so a substituted value is never scanned again. Headers are set through MimeKit, which encodes them | `SmtpEmailSender`, `TemplateText` and their tests |
| OE-10 | Downloads | A stored file is sent as `application/octet-stream`, as an attachment, with `X-Content-Type-Options: nosniff`, so a browser never renders an upload as a page | `AdministrationControllerBase`; `DocumentEndpointTests` |
| OE-11 | Logs | Logging uses `[LoggerMessage]` templates with values as structured fields. No end-user free text (titles, descriptions, search text, file names) is logged; exceptions are logged by type, not message | Review |
| OE-12 | LDAP search filters | A value written into a directory filter is an RFC 4515 assertion value: `(`, `)`, `*`, `\`, NUL, control and non-ASCII bytes escaped as `\hh`, so it can neither widen the search nor add a clause | `LdapFilter`; `LdapFilterTests` |

## 2. Pull request checklist

Tick each line, or write why it does not apply.

- [ ] **Screens.** A new or changed screen shows user text only as JSX children or attribute values (OE-1). `npm run lint` passes with no new `eslint-disable` of `no-restricted-syntax`, `no-eval`, `no-new-func` or `no-script-url`.
- [ ] **Links.** A new link is a `<Link to>` built from ids. A path taken from data is checked with `isAppPath` (OE-3).
- [ ] **Free text on a new list or detail screen.** The screen is added to `src/frontend/src/app/outputEncoding.test.tsx`, so the stored-XSS payload is shown as inert text on it.
- [ ] **New endpoint.** Its arguments are strings, scalars, collections or the platform's own request models, so `RequestTextValidation` checks them; `RequestTextCoverageTests` passes. If it reads a form or a body by hand, it calls `TextInput.Violation` on every field it reads, as `DocumentUploadForm` does.
- [ ] **New field.** Its `Validate` sets its own maximum length, at most `TextInput.MaxLength`, and anything stricter its meaning needs (a code pattern, an enumeration).
- [ ] **Queries.** No value is concatenated or interpolated into SQL text outside a `FormattableString` method; a client-chosen column, sort or filter goes through an allowlist (OE-6).
- [ ] **Exports and messages.** New export cells go through `SpreadsheetSafety` or the PDF glyph writer. New messages stay plain text, or, if a channel ever renders HTML, every substituted value is HTML-encoded at substitution (OE-7 to OE-9).
- [ ] **Directory queries.** A value written into an LDAP filter goes through `LdapFilter` (OE-12).
- [ ] **Errors and logs.** No error `detail`, `errors[].message` or log line contains a submitted value (OE-5, OE-11).

## 3. Review of the tree, 2026-10-14

### 3.1 Frontend render paths (`src/frontend/src`)

| Check | Result | Evidence |
| --- | --- | --- |
| HTML sinks (OE-1) | **None in application code.** The only `innerHTML` uses are reads in `integrationAdmin.test.tsx`. No HTML or Markdown renderer is a dependency: the runtime dependencies are `react`, `react-dom` and `react-router` only | `npm run lint` passes over the whole tree with the OE-1 rules on |
| Code sinks (OE-2) | None | Lint |
| Links bound to data (OE-3) | **None.** The one raw `<a href>` is the constant skip link (`AppLayout.tsx`). `saveFile.ts` sets a blob URL it creates. URL-like API fields (`callbackUrl`, `provisioningUri`, `sourceReference`) are shown as text, never as links | Lint |
| Deep link and sign-in redirect (OE-3) | **Fixed.** `isAppRoute` and `redirectTarget` accepted `/\evil.example`, which a browser opens as `//evil.example` (another host) when the link is middle-clicked or opened in a new tab. The API already refused it at intake (`NotificationIntentIntake`); the SPA now checks the same way through `isAppPath` | `appPath.test.ts` |
| Styles from data | Numbers and enumerations only (`--widget-span`, Gantt percentages) | Review |
| `lang`/`dir` from data | Attribute values, encoded by React | Review |
| Stored-XSS sweep | **Inert on all 10 screens**: SCR-080 Risk Register, SCR-082 Risk Detail, issue register and detail, change request detail, task board, progress (Arabic entry), document detail, SCR-150 notifications, ADM-051 audit event values. The payload `<img … onerror …><script>…</script><a href="javascript:…">` is read back as text, and no element it describes, no `<script>`, no `javascript:` link and no `on*` attribute exists | `outputEncoding.test.tsx`; mutation in `input-validation.md` §5.3 |

### 3.2 Backend output paths (`src/backend`)

| Check | Result | Evidence |
| --- | --- | --- |
| Raw SQL (OE-6) | **All parameterised.** `FromSql`/`SqlQuery`/`ExecuteSql` with interpolated values in `IntegrationMonitoringRepository`, `ActivityProjectionRepository`, `OutboxDispatcher`, `AuditIngestor`, `ProjectTaskRepository`, `ScheduleRepository` and `NotificationRepository`. `AuditIntegrityRepository` builds `CommandText` from constants and binds `@after`/`@count`. The one `ExecuteSqlRawAsync` (`AuditIngestor.LockTimeoutStatement`) contains an integer from server configuration. `DatabaseScripts` runs embedded seed files from a fixed map, from the CLI only | Review |
| Client-chosen columns, sorts, filters (OE-6) | **Allowlisted.** The SCR-138 explorer resolves every column, filter and sort against the PUBLISHED REPORT_RULES allowlist and the in-code field registry (`ReportQueryBuilder.Resolve`). Operators and directions are enumerations. Filtering and sorting run in memory over projection rows (`ReportExecutor`), never in SQL. User-list sort maps to the `UserSort` enumeration. Text search uses `EF.Functions.ILike` with `\`, `%` and `_` escaped | Review; CTL-15 tests of TASK-071 |
| CSV/XLSX/PDF (OE-7, OE-8) | Neutralised as stated in §1 | `SpreadsheetSafety`, renderer tests |
| E-mail/SMS (OE-9) | Plain text; single-pass substitution; no SMS renderer yet (`UnconfiguredSmsGateway`) | Review |
| Responses (OE-5) | JSON only; no `ContentResult`, Razor or `text/html` anywhere | Review |
| LDAP (OE-12) | **Escaped.** The sign-in username and a subject id reach the directory only through `LdapFilter.Equality`; a binary subject id is written byte by byte. No test pinned it; `LdapFilterTests` now does | `LdapFilterTests` |
| Logs (OE-11) | No end-user free text logged. An identity provider's `error` string (`OpenIdConnectRelyingParty`) and Nafath's verification reference are logged as received: values from integrated systems, not users (`input-validation.md` F-4) | Review |

### 3.3 Out of this checklist

Response headers (`Content-Security-Policy`, `X-Content-Type-Options` on every response, `X-Frame-Options`, `Strict-Transport-Security`, `Referrer-Policy`) are CTL-12, TASK-078. A CSP would be a second barrier behind OE-1 to OE-3, not a replacement for them. `input-validation.md` F-1 carries the ZAP alerts that belong there.

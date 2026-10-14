# OWASP ZAP Baseline Report

| Field | Value |
| --- | --- |
| Task | TASK-077 — Implement Platform-Wide Input Validation & Output Encoding, deliverable "ZAP baseline report" |
| Control | CTL-11 (`cybersecurity-control-matrix.md`) |
| Scan date | 2026-10-14 |
| Tool | OWASP ZAP 2.17.0 (`ghcr.io/zaproxy/zaproxy:stable`): `zap-baseline.py` and `zap-api-scan.py` |
| Target | The DEV compose stack built from `sec/task-077-platform-input-validation-output-encoding`, on a scratch database (`input-validation.md` §5.4). Not SIT: none exists yet (`input-validation.md` F-5) |
| Result | **Zero High and zero Critical findings in either scan. No injection finding of any severity** |
| ZAP's own reports | `zap/baseline-spa.html` and `.json`, `zap/api-scan.html` and `.json`; the scanned operations in `zap/api-scan-scope.txt` |

## 1. What was scanned

| Scan | Target | How | Signed in |
| --- | --- | --- | --- |
| **Baseline** (the workbook's scan) | The SPA's production build (`npm run build`, served by `vite preview`, which forwards `/api` to the API) | Traditional spider plus the AJAX spider for 3 minutes, then ZAP's passive rules on every response | No: the baseline sees what an anonymous visitor sees |
| **Active API scan** | 34 operations on 19 paths of `docs/api/openapi.v1.json` (`zap/api-scan-scope.txt`): sign-in, projects, risks (including a command), issues, change requests, progress, documents, users, departments, audit events, notifications, notification templates, a report run and the SCR-138 explorer — list endpoints with free-text search and filters, and create/update bodies with free text | ZAP imports the OpenAPI document and sends its active rules' payloads into every path, query, header and body parameter | Yes: local.r02 holding every permission at ALL, so requests reach each handler rather than a 403 |

A baseline is passive: it sends no payload and so cannot find an injection on its own. The active API scan is the one that tries injection. Both are reported.

## 2. Results

| Risk | Baseline (SPA) | Active scan (API) |
| --- | --- | --- |
| **Critical** | 0 | 0 |
| **High** | 0 | 0 |
| Medium | 2 — Content Security Policy header not set (10038); missing anti-clickjacking header (10020) | 0 |
| Low | 6 — X-Content-Type-Options missing (10021); Permissions-Policy not set (10063); Cross-Origin-Embedder/Opener/Resource-Policy missing (90004 ×3); Unix timestamp in the JS bundle (10096) | 2 — X-Content-Type-Options missing (10021); Cross-Origin-Resource-Policy missing (90004) |
| Informational | 6 (localStorage use, comments in the bundle, a modern web application, cacheability) | 4 (client-error responses, the sign-in request identified, a query parameter named like a credential, cacheability) |
| Rules passed | 58 | 117 |

**Injection rules the active scan ran and passed:** SQL Injection (40018) and its time-based PostgreSQL, MySQL, Oracle, MsSQL and Hypersonic variants (40019–40022, 40027); Cross Site Scripting reflected, persistent, persistent-prime, persistent-spider and DOM-based (40012, 40014, 40016, 40017, 40026); user-controllable HTML attribute and JavaScript event (10031, 10043); CRLF Injection (40003); Server Side Code Injection (90019); Remote OS Command Injection and its time-based variant (90020, 90037); XPath Injection (90021); XSLT Injection (90017); SOAP XML Injection (90029); Server Side Template Injection and its blind variant (90035, 90036); XML External Entity (90023); Path Traversal (6); Remote File Inclusion (7); Log4Shell (40043). The API logged no unhandled exception during the scan, and Application Error Disclosure (90022) passed.

## 3. Disposition

| Alert | Disposition |
| --- | --- |
| All header alerts (10038, 10020, 10021, 10063, 90004) | CTL-12, **TASK-078** (`input-validation.md` F-1). They depend on how SIT and PROD serve the SPA and the API; `vite preview` adds no headers |
| Timestamp disclosure (10096) | A number in the minified bundle that looks like a Unix time; nothing secret |
| Sensitive information in URL (10024) | Query parameters ZAP filled with its own values on the scanned list endpoints; the platform sends no credential in a URL (R-41, TASK-028) |
| Client error responses (100000) | Expected: ZAP's payloads are refused with 400, 404 and 422 — the validation layer answering |

## 4. Limits of this scan

- **The target is DEV, not SIT.** Rerun both scans against SIT when it exists (`input-validation.md` F-5); `zap/api-scan-scope.txt` and §1 give the scope.
- **ZAP fills parameters from the schema with its own placeholder values**: the literal `"string"` for text, a parameter's name for a query value (`status=status`), arbitrary ids. Many requests were therefore refused at validation, as 400, 404 or 422, before any data was read or written. The scan wrote no row to the scratch database. ZAP's report records a 2xx only for the notifications list, which shows the scan was signed in and reached a handler. Its payloads reached binding and the validation layer of every operation. Beyond that, a payload reaching SQL is shown only where a request passed validation. That a stored value goes through a real write and read back is covered by `RequestTextValidationTests.TextThatObeysTheRulesIsStoredAndReturnedExactlyAsEntered` and the browser check (`input-validation.md` §5.4); that no SQL text is built from a value is covered by the review (`output-encoding-checklist.md` §3.2). A scan seeded with valid example bodies would go deeper; it belongs with the SIT rerun (F-5).
- **The baseline is anonymous.** Pages behind sign-in are covered by the stored-XSS sweep (`outputEncoding.test.tsx`) and the browser check, not by ZAP.

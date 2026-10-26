# Notification Center UI (WF-15 Frontend)

| Field | Value |
| --- | --- |
| Task | TASK-040 — Build Notification Center UI (WF-15 Frontend) (P6 - Shared Platform Services) |
| Depends on | TASK-039 — the WF-15 API (`notification-runtime.md` §3): every screen reads and writes through its inbox and preference operations, and every refusal shown is one it returns. TASK-032, TASK-036 and TASK-038 — the SPA foundation (`identity-access-administration-ui.md` D-1, `approvals-ui.md` D-7, `document-library-ui.md` D-8) |
| Record date | 2026-10-02 |
| Status | **BUILT AND VERIFIED LOCALLY**: component tests against a mocked API, and the wire format against the compose API (§4 row 8). Not run in a browser (§4). In a fresh environment the screens are empty: no module publishes an intent and no NOTIFICATION_ROUTING version is published (TASK-039 F-4), so SCR-154 says preferences are not available yet and SCR-151/152 say no family of their kind is configured |
| Branch | `feat/task-040-wf15-notification-center-frontend` |
| Deliverables | React components and routes for SCR-150–154 and MOD-070–071 in `src/frontend/src/features/notifications` (`inbox/`, `history/`, `preferences/`, `dialogs/`, `components/NotificationBell.tsx`, `unreadCount.ts`, `routes.tsx`, `api/`); i18n files `features/notifications/i18n/{ar,en}.json`. Additions to the foundation (D-9). 33 component tests. This record |
| Environment variables / secrets | None |
| Gate decision applied | None on the row |
| Participation amendment | **ADR-004: the preference UI lets a recipient disable non-mandatory SMS families; in-app remains always on** — D-6 |
| Workbook read | The TASK-040 row as given in the task request, 2026-10-02 (the Google Sheet cited by earlier records was not reachable from this session); `notification-runtime.md` §2–§4, F-14, F-17; `api-conventions.md` R-35 |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | SCR-150–154 and MOD-070–071, Arabic and English, responsive | This task |
| **In** | The unread badge in the shell and its near-real-time refresh | This task |
| **Out** | Template authoring and the operations views (intents, deliveries, redrive) | A later admin UI task (TASK-039 D-14: R01 only) |
| **Out** | Push delivery (WebSocket/SSE) | No API offers it (TASK-039 F-17) |
| **Out** | Publishing intents, NOTIFICATION_ROUTING content, templates | Each source task; PMO (TASK-039 F-4) |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **The badge shows the API's count, never one computed in the browser.** `unreadCount.ts` is one store outside React: `GET /notifications/unread-count` when the shell mounts for a session, every 30 s after that (`UNREAD_POLL_INTERVAL_MS`), when the tab becomes visible again, and right after this person reads something (MOD-070 closed, MOD-071 done). A poll is skipped while the tab is hidden. A newer read abandons an older one still in flight, so the latest answer wins; a failed read keeps the last count until the next poll. The number is shown exactly as returned (no "99+"). | Acceptance criterion 1: the badge matches the API within one refresh cycle. The worker routes every 15 s (TASK-039 F-17), so a new notification shows within about 45 s. |
| D-2 | **An open inbox follows the badge.** The store keeps a `revision` that moves whenever a later answer differs from the one before; SCR-150–152 read their page again when it moves (`useReloadOnUnreadChange`). The first answer after sign-in does not move it, so opening the inbox costs one list read, not two. | Near-real-time on the list as well as the badge, without a second poll. |
| D-3 | **Mark-all-read cannot be undone except by new notifications.** MOD-071 is confirmed first and says that read notifications cannot be made unread. It calls `POST /notifications/read-all`, which marks what was sent until then (TASK-039 D-12); the screen then shows the API's `markedCount`, re-reads the page and the count. No screen offers "mark unread", and no API operation exists for it. The button is disabled while the badge reads 0 and is offered on SCR-150 only, because it marks every family, not just the ones SCR-151/152 show. | Acceptance criterion 2. |
| D-4 | **Opening a notification reads it.** MOD-070 reads `GET /notifications/{id}`; when it is SENT it then calls `POST …/read` and shows the answer. A READ notification is only shown. The text is plain (React escapes it) with its line breaks kept, tagged with the language it was rendered in (`lang`, `dir="auto"`). The deep link becomes a router `Link` only when it is an application route (`/…`, not `//…`). The API refuses anything else at intake (TASK-039 D-3), and the screen checks again. | R-35 "marking a notification read"; EV-3. |
| D-5 | **SCR-151 Alerts and SCR-152 Escalations are chosen by the family code's suffix.** FG-04 families carry only a code, a label and a mandatory flag (TASK-039 F-14). SCR-151 offers the families whose code ends in `_ALERT` (or is `ALERT`), SCR-152 those ending in `_ESCALATION`, and each lists one family at a time (`eventFamilyCode` takes one value), the first by default. The families and their labels come from `GET /notification-preferences`, the only family list a non-administrator can read. With no routing in force (422 `CONFIGURATION_MISSING`) or no family of the kind, the screen says so and asks for no notifications. SCR-150 then shows codes and offers no family filter. | The API offers no other field; paging stays the API's because each page is one call. F-1 records the convention. |
| D-6 | **Preferences: in-app always on, mandatory families fixed, the rest the recipient's.** SCR-154 shows one row per family of the routing in force and one column per channel. A control is enabled only for e-mail or SMS of a non-mandatory family whose `isConfigurable` is true. In-app is locked even if the API were to say otherwise, which is the amendment's own rule. A locked control says why ("Always on", "Required for this family"). A channel the family is not routed to has no control. Only the changed cells are sent (`PUT /notification-preferences`, all or none). The answer replaces what the screen shows, and a new visit reads the stored set back. After 422 `NOTIFICATION_PREFERENCE_*` or 412, the refusal stays on screen and the stored set is read again. | ADR-004 as amended; acceptance criterion 3. WF-15 reads the stored choice at routing and at every send attempt (TASK-039 D-11), so the next send honours it. |
| D-7 | **SCR-153 shows what became of each delivery, not its content.** Channel, family, the status as a word with a tone (pending, sent, delivered, read, failed and retrying, not delivered, not sent), and why: each of TASK-039's six delivery suppression reasons in plain words, an unknown one by its code, or when it was read or delivered. Only the subject is shown. | TASK-039 D-12; the history API carries no body. |
| D-8 | **Routes under `/notifications` need a session, not a role**, as `/approvals` and `/documents`. Every operation is the caller's own (TASK-039 D-12, D-14). The shell shows the badge and the notifications navigation to anyone signed in. | — |
| D-9 | **Additions to the foundation, backward compatible.** `shared/api/useListParams.ts`: a list screen's filters and page in the URL, used by SCR-150–153. Documents still has its own copy. `common.home.notifications`. Styles: `.bell`, `.bell__count` (white on the primary colour, the active navigation link's pair), `.row--unread`, `.notification__*`, `.preference*`, `.form--wide`. | DRY across the four list screens. |

## 3. Screens and routes

All under `/notifications`, behind `RequireSession` (`features/notifications/routes.tsx`).

| Screen | Route | Component |
| --- | --- | --- |
| SCR-150 All Notifications | `/notifications?state=&family=&page=` | `inbox/NotificationInboxPages.tsx` (`AllNotificationsPage`) over `inbox/NotificationInbox.tsx` |
| SCR-151 Alerts | `/notifications/alerts?state=&family=&page=` | `AlertsPage` |
| SCR-152 Escalation Notifications | `/notifications/escalations?state=&family=&page=` | `EscalationsPage` |
| SCR-153 Notification History | `/notifications/history?channel=&status=&page=` | `history/NotificationHistoryPage.tsx` |
| SCR-154 Notification Preferences | `/notifications/preferences` | `preferences/NotificationPreferencesPage.tsx` |
| MOD-070 Notification Detail | on SCR-150–152 | `dialogs/NotificationDetailDialog.tsx` |
| MOD-071 Mark All Read | on SCR-150 | `dialogs/MarkAllReadDialog.tsx` (the platform `ConfirmDialog`) |
| Unread badge | the shell header, every page | `components/NotificationBell.tsx`, `unreadCount.ts` |

## 4. Verification

Run 2026-10-02 on macOS with Docker Desktop: frontend in `AHDA-frontend` (Node 24.21.0); the compose API `AHDA-api` on `AHDA-postgres` and `AHDA-ldap`, built and started with `docker compose -f infra/docker/docker-compose.yml up -d --build --wait` (migrate and seed exited 0, all four services healthy).

| # | Command | Result |
| --- | --- | --- |
| 1 | `npm run lint` | 0 problems |
| 2 | `npm run format:check` | All matched files use Prettier code style |
| 3 | `npm run typecheck` | No errors |
| 4 | `npm test` | 23 files, 192 tests passed (19 files and 159 tests before, 33 new in `features/notifications`). `access.test.tsx` now excludes the shell's own `unread-count` read from "no request on a denied administration screen"; without that, its 7 cases failed, because the badge polls on every signed-in page |
| 5 | `npm run build` | Built. 823 KB JS (223 KB gzip), up from 787 KB; the >500 KB chunk warning was already on `dev` (TASK-038 F-5) |
| 6 | `npm audit` | 0 vulnerabilities. No dependency added |
| 7 | Mutation tests (§4.1) | Each of 6 mutations fails at least one test |
| 8 | curl through the SPA's proxy (`localhost:5173/api/v1`) as `local.r04`, with the calls the screens make: `GET /notifications/unread-count`; `GET /notifications?unread=true&eventFamilyCode=SECURITY_ALERT&page=1&pageSize=25`; `GET /notifications/history?channel=SMS&status=SUPPRESSED&page=1&pageSize=25`; `GET` and `PUT /notification-preferences`; `POST /notifications/read-all` with an `Idempotency-Key`; `GET /notifications/{unknown id}`; `GET /notifications?unread=maybe` | 200 `{"unreadCount":0}`; 200 empty page; 200 empty page; **422 `CONFIGURATION_MISSING`** for both (no routing published, TASK-039 F-4); 200 `{"markedCount":0}` (the key is ignored, as R-35 allows); 404 `NOT_FOUND`; 400 `VALIDATION_FAILED`. Every shape matches `api/types.ts`, and every code is one the screens handle (SCR-154 and SCR-151/152 show "not configured" for the 422) |
| 9 | The eight `docs/architecture/*-check.py` gates (contract-check needs a live OpenAPI run and is unchanged here) | All OK |

Not run: a browser pass (axe with colour contrast, Arabic layout, phone width); no Chromium is installed in `AHDA-frontend`. jsdom's axe pass (contrast off) is clean on SCR-150, SCR-153, SCR-154 and MOD-070. The new colour pair, `.bell__count`, is white on `--color-primary`, the pair the active navigation link already uses. No end-to-end send: nothing on the compose stack publishes an intent or a routing version (TASK-039 F-4), so the "next send honours it" half of criterion 3 rests on TASK-039's tests (§5).

The tests, by acceptance criterion and amendment:

| Item | Tests |
| --- | --- |
| 1. Badge matches the API within one refresh cycle | `badge.test.tsx`: 3, then 41 one interval later (not one millisecond before), then no badge at 0, three reads in all; a failed poll keeps 2 and the next shows 5; read again when the tab becomes visible, not while hidden; no badge and no poll without a session. `inbox.test.tsx`: the badge clears after MOD-070 reads the only unread notification |
| 2. Mark-all-read reversible only by new notifications | `badge.test.tsx`: two unread → confirm → one `read-all`, "2 notifications marked as read", badge gone, no unread row, no "mark unread" control, button disabled; then a new notification arrives, the next poll shows 1 and the open page shows it unread while the old ones stay read; a refusal stays in the dialog and changes nothing |
| 3. Preferences persist and apply on the next send | `preferences.test.tsx`: turning SMS off sends exactly `[{APPROVAL_TASK, SMS, false}]` (a cell toggled and toggled back is not sent); a new visit reads it back off; nothing changed sends nothing; undo sends nothing; a 422 is explained and the stored set read again; 422 `CONFIGURATION_MISSING` says preferences are not available yet |
| ADR-004 amendment | `preferences.test.tsx`: in-app checked and locked on every family ("Always on"), and locked even if the API offered it; SMS and e-mail of a non-mandatory family enabled; mandatory families locked ("Required for this family"); an unrouted channel has no control; Arabic labels right-to-left |
| SCR-150–153, MOD-070 | `inbox.test.tsx` (rows as returned, unread marked, family labels or codes, filters in the request and URL, empty/filtered-empty/error, no family filter without routing, navigation; SCR-151/152 families by kind, the first by default, no request without a family; MOD-070 text, language, link, read once, read notification only shown, non-route link not offered, 404). `history.test.tsx` (each outcome and reason, unknown reason by code, filters, empty states) |

### 4.1 Mutation tests

Each mutation was applied, `npx vitest run src/features/notifications` run, and the source restored (33 of 33 passed afterwards).

| # | Mutation | Tests failed |
| --- | --- | --- |
| M-1 | The badge is never polled again | 3 |
| M-2 | The badge counts in the browser (one less after any read) | 4 |
| M-3 | An open inbox ignores a moved count | 1 |
| M-4 | In-app can be turned off | 2 |
| M-5 | Every cell is sent, not only the changed ones | 2 |
| M-6 | Opening an unread notification does not read it | 1 |

## 5. Acceptance criteria and deliverables

| Item | Result |
| --- | --- |
| Unread badge count matches the count returned by the API within one refresh cycle | **MET** (D-1, D-2; §4) |
| Marking all as read is reversible only by new notifications, not by re-marking old ones | **MET** (D-3; §4) |
| Preference changes persist and are honoured by the WF-15 backend's channel routing on the next send | **MET in the UI** (D-6; §4). The backend half is TASK-039's `PreferencesListEveryFamilyRefuseWhatCannotBeChangedAndTheNextSendHonoursThem` and `ARecipientWhoTurnedSmsOffGetsNoneForThatFamilyButStillGetsMandatoryOnes` |
| Amendment: ADR-004, non-mandatory SMS can be disabled, in-app always on | **MET** (D-6; §4) |
| Deliverables: components and routes for SCR-150–154 and MOD-070–071 | **MET** (§3) |

## 6. Findings

| # | Finding | Owner | Consequence if unresolved |
| --- | --- | --- | --- |
| F-1 | **SCR-151/152 tell alert and escalation families apart by the code's suffix** (D-5), a convention this task chose because FG-04 families have no kind (TASK-039 F-14). A family named otherwise appears on SCR-150 only | PMO (FG-04 family kinds, or a naming rule for NOTIFICATION_ROUTING) | An alert family not ending in `_ALERT` is missing from SCR-151 |
| F-2 | **SCR-151/152 list one family at a time**: `GET /notifications` takes one `eventFamilyCode`. With several alert families, the person switches between them | Engineering Architect (API: a set-valued `eventFamilyCode`) | No single "all alerts" view |
| F-3 | **Near-real-time is a 30 s poll** on top of the worker's 15 s pass (TASK-039 F-17). A push channel would remove both delays | Engineering Architect | Up to about 45 s before a badge moves; one count read per signed-in tab every 30 s (I-47, 0.037 ms, `indexing-strategy.md`) |
| F-4 | **Families are read from the preference set**, the one family list open to every signed-in person. With no routing in force, the inbox shows codes and SCR-151/152 show nothing (D-5) | — | None while routing is published |
| F-5 | **Delivery-team Arabic strings** have not had a bilingual business review (TASK-032 F-9) | PMO / AHDA business reviewer | Terminology may differ from AHDA's usage |
| F-6 | **Security Lead review (CTL-43) cannot be requested**: the CODEOWNERS teams do not exist (TASK-031 F-13). This change narrows an assertion of the RBAC test `access.test.tsx` (a denied administration screen may now make the shell's `unread-count` read, nothing else), renders server-provided text and follows server-provided links | Maintainer | The PR's CTL-43 box stays unticked |

## 7. Change log

| Date | Change |
| --- | --- |
| 2026-10-02 | Created (TASK-040) |

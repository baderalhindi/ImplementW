# RPMO review, round 4 (5 Nov 2026)

What the platform does about the six requests in *RPMO Platform — Review Round 4*, and where it departs from that document. The migrations are
named `TASK-112`, a placeholder until the workbook gives this round a task ID.

## 1. Filters behind a button (request 1)

`shared/ui/FilterBar.tsx` is the filter component of every filtered page. A **Filters** button, closed by default at every screen size, counts the
active filters; the panel opens below it with the same rows as before; the active filters stay as chips below it while it is closed, and **Clear all** sits beside the button.
Open or closed is remembered per person and per page in the browser (`pmplatform.filterPanel.<user>.<page>`). Filter values stay in the URL where
they were. Esc closes the panel and returns focus to the button. On a dashboard, "Updated…", Refresh and Customize layout share the button's row.

The project register's search box stays visible above the button. A report's required parameters hold the panel open until the report has run, so
they cannot be missed. The master data catalogue is a required choice, not a filter, so it stays outside the panel.

## 2. Monthly updates (request 2)

A `update_request_batch` issues one `MONTHLY_UPDATE` request per project (`POST /update-request-batches`, all or nothing). The permission is
`UPDATE_REQUEST_SEND`: R02 ALL, R03 DEPT. The Liaison is left out until AHDA answers the review's open point; granting it is a profile change.

The window at sign-in and the pinned bell item read `GET /my-update-requests`: the open requests naming the signed-in person as responder, whoever
they are. A draft pre-fills itself, otherwise last month's values from the project. Save, submit and review are WF-13's. Accepting writes the
values to the project with `declared_source = ENTITY_UPDATE`, in the same transaction, and records the application attempt the schema's invariant
requires.

The responder defaults to the project's `entity_contact_user_id`. A project with none must be given one before sending, and the one given becomes
its contact. An internal user holding `EXTERNAL_CONTRIBUTION_RESPOND` (R04 ASSIGNED) may answer this schema; every other schema stays external-only,
in the service and in the database guards.

The `ExternalParticipation:MonthlyUpdate` worker reminds responders 3 days before the due date, on the day, then daily, one a day per request. On
the first late day it also tells the reviewer. On its day it sends a repeating batch's next sending to the same projects, to their contact as it
stands then, due the same number of days after sending. The batch page shows each project as sent, opened, draft, submitted, accepted, returned,
rejected or withdrawn, and whether it is overdue, with **remind the late ones** and **withdraw**.

**Departures from the review.**

- **The optional evidence file is not built.** R08 holds no WF-12 grant, and a document needs AHDA's published document types and a scanner, which
  fail closed today (`external-participation.md` F-1). The window shows no dead control. This needs AHDA's decision on what an entity may upload.
- **"The existing escalation rules apply" has nothing to apply.** WF-13 has no escalation rules. The first-late-day notice to the reviewer stands
  in for one.
- **Reminder delivery needs AHDA's publication.** The `MONTHLY_UPDATE_REMINDER` family needs a NOTIFICATION_ROUTING entry and a template, as every
  family does. Without them the intents are recorded and nothing is delivered. The window and the bell item do not depend on them.
- **A project with no delivering entity cannot be requested.** A request is addressed to an entity (a required column), so such a project answers
  `PROJECT_HAS_NO_ENTITY`. All 167 RPMO projects have an entity.

## 3. Dashboard additions (request 3)

`GET /portfolio-insights` takes the filter bar's filters and answers each section only to a holder of its permission, at that permission's scope:

| Permission | Sections | Shipped to |
| --- | --- | --- |
| `PORTFOLIO_SUMMARY_VIEW` | Headline cards, table by portfolio | R02 ALL, R07 ALL, R03 DEPT, R06 DEPT |
| `PORTFOLIO_BREAKDOWN_VIEW` | Tables by initiative and sector, printable group cards | R02 ALL, R07 ALL, R03 DEPT |
| `PORTFOLIO_STATUS_VIEW` | Projects by stage, on-plan indicator | R02, R07 ALL, R03, R06 DEPT, R04 OWN, R05 ASSIGNED |
| `PORTFOLIO_FOLLOW_UP_VIEW` | Why projects need follow-up, top 10 | R02, R07 ALL, R03 DEPT, R05 ASSIGNED |

Amounts answer only to a holder of `FINANCIAL_VIEW_SENSITIVE`. Averages are simple averages of the measured projects' percentages; a total row is
calculated from the projects. A project without both percentages is unmeasured, never zero.

Run over `RPMO_Projects_Import_v1.xlsx` on 5 Nov 2026: 167 projects, 97 measured, planned 64.92%, actual 53.75%, gap −11.16 (stalled), 56 on track,
12 late, 29 stalled, 70 not measured; 62 need follow-up (29 stalled, 12 suspended, 12 late, 0 overdue action, 9 open obstacle); PF-07 155 projects,
−11.33; PF-03 8, −10.4; PF-02 2, −5.5; first on the top 10 AHP-000005. Total SAR 64,553,236,526.17, the Excel's plus the file's 491 million.

**Departure from the review.** Projects by stage reads the platform's status, not the file's old status, which is not stored (the review's own §4.2).
The Excel's "in progress 104, other 3, not set 2" therefore reads as active 109: the file maps those five rows to active.

**Projects by location is a map** (asked for after the review). `master_data_item.latitude` and `longitude` place a city, and the dashboard's `places` carry them. The Portfolio dashboard draws one circle per city, sized by its project count, under the filters in force; pressing a circle, or the city in the list beside it, opens the place's figures and first projects, each as the person's permissions allow. The base map is OpenFreeMap's vector map drawn by MapLibre GL JS (2026-11-07; it replaced Leaflet over OpenStreetMap's image tiles, which blurred on high-density screens): free for commercial use, with no key, account or request limit. Labels follow the person's language from OpenStreetMap's Arabic and English names, falling back to the local name. Its data is OpenStreetMap's, so places and names are no richer than before. The public instance has no uptime guarantee: if it fails, the circles still draw on a plain background. Production can self-host OpenFreeMap by changing `STYLE_URL` and the CSP. CSP: `connect-src` allows `https://tiles.openfreemap.org`, `img-src` gains `blob:` as MapLibre requires, and `worker-src 'self'` runs its worker from the SPA's origin; `script-src` stays `'self'`. Google's Map Tiles API was built and set aside on `feat/task-112-google-map-tiles` because it needs a billing account. `load-rpmo-lists` sets the 13 cities' coordinates: approximate town centres, for AHDA to correct. There is no screen to edit them yet. Decision 5 in §7.

## 4. Project import (request 4)

The import was already granted to R02 and R03 in `db/seed`. The client could not see it because **the showcase database was migrated but never
seeded again**, so its permission rows did not exist. The showcase recipe needs `seed` after `migrate`. The grants stay on version 1 of the shipped
profiles, as the seed writes them, so the test users' assignments need no move and no new version.

`name_en` and `is_strategic` are now stored. `entity_type`, `legacy_status`, `project_type`, `assumed_fields` and `validation_warnings` are skipped
with an info note. A file with no version stamp is not called outdated. A closed project whose row is unchanged is a skip, so the same file can be
uploaded again.

`load-rpmo-lists <file>` loads the file's lists: 4 departments, 7 portfolios, 11 initiatives, 15 sectors, 3 areas, 13 cities, 3 priorities, and the 41
entities in its data; the 3 governance levels are seeded and take RPMO's words. It retires unused items of those catalogues that the file does not
list. It also assigns R04 over each row's department to each existing user named as project manager, which the import requires of a manager: this
is what lets `local.r04` manage all 167. It reads the file at run time, so the public repository holds no client data.

Result on a showcase clone: 167 rows, 0 errors, 167 with warnings. Every row has `OWNER_UNMATCHED` because the four follow-up owners have no account, so
the field stays empty as the review allows. The importer cannot approve their own batch; a second R02 user approved it. AHP-000001 shows the row's
title, English title, dates, value, status and progress. R03 gets `OUTSIDE_SCOPE`.

## 5. Remove the demo projects (request 5)

`reset-demo-data [--dry-run] [--include-unmarked]` removes projects whose `seed_source` is DEMO. The migration marks the rows the seed actor made,
which is every showcase project but those testers entered by hand. `--include-unmarked` removes those too, and is refused in Production.

History tables refuse a DELETE by design, so the run sets `session_replication_role = replica` for its one transaction. Its role needs that right, or
it fails and changes nothing. That setting also turns foreign keys off, so the run follows them itself, removing rows that lost their parent until
none is left. Saved views and report runs carry no project key, so they go only when the register ends up empty.

Kept: users, roles, profiles, settings and the audit trail. `audit_event.scope_project_id` loses its foreign key, because the append-only trail
outlives a removed project. An internal person's last assignment, anchored on a removed project, keeps the role without the anchor; any other project-anchored assignment goes with its project, because clearing an external user's anchor would widen it to their whole entity. One entry,
`Administration.DemoDataRemoved`, records the counts per table. Uploaded document files stay in storage; only their rows go.

On a showcase clone: 16 projects removed, `validate-data-integrity` clean, the audit chain intact, the activity projection matching.

## 6. Accounts and roles (request 6)

**Local accounts.** `account_source = LOCAL`: a password hashed with PBKDF2-SHA256, shown once when set, in the response that set it. A temporary
password issues no session: sign-in answers a `passwordChangeToken` and `POST /sessions/password-change` continues the sign-in, second factor
included. At least 12 characters mixing letters and digits, not the username, none of the last 5; locked for 15 minutes after 5 failures.
`POST /users/{id}/reset-password`, `/unlock` and `/set-end-date` are USER_MANAGE. `Identity:LocalAccounts:Enabled` turns local accounts off.

This departs from Cybersecurity Guide CS-011, "no unmanaged local accounts". The client asked for it; the finding is SR-81, for AHDA to accept with its
end date. Lockout returns the same 401 as a wrong password, so a locked user is not told why.

**Changing and resetting a password yourself (added 6 Nov 2026).** Every signed-in person finds **Change password** in the account menu.

| Account | Change while signed in | Forgotten password |
| --- | --- | --- |
| Local | `POST /sessions/current/password-change`, with the current password. Wrong guesses count towards the lockout | `POST /sessions/password-reset-requests` e-mails a link to `/reset-password`; `POST /sessions/password-reset` sets the new password. An administrator's reset stays available |
| Directory or Microsoft | Not the platform's to change (ADR-007): the page links to `VITE_PASSWORD_RESET_URL`, or names the service desk | The same |

The password rules are the ones above. The reset link works once, lasts 30 minutes (`Identity:LocalAccounts:PasswordResetLinkLifetime`) and issues no session, so the second factor still applies. At most one message goes per account in 5 minutes (`PasswordResetCooldown`). A reset request answers the same 204 in the same time whether or not the account exists.

Any password change, including an administrator's reset, ends the account's sessions at their next refresh, up to 15 minutes later; the person who changed it is signed out at once. `GET /sessions/sign-in-options` offers the reset only where `EXCHANGE_SMTP_HOST` and `APP_BASE_URL` are set and local accounts are on; elsewhere the sign-in page names the service desk. The message does not go through WF-15, whose history stores every rendered body, so its text is fixed in `PasswordResetMailer` in both languages rather than a governed template. The finding is SR-83.

**Roles.** `POST /roles` creates a role and a draft profile, empty or copied from another role's published one. A role carries its own allowed scopes
and landing dashboard, which replace the R03, R05 and R06 rules in code and the R01–R08 landing. `Identity:ProfileApproval:RequiredAdministrators` is 3 by
default and 1 on a test environment; at 1 one person prepares, verifies and publishes, and the rule that a holder may not approve their own profile
is off. A created role with no assignment ever may be deleted; otherwise it is deactivated, which stops new assignments and leaves those made
running until ended.

## 7. Decisions for AHDA

1. May the Liaison (R05) send monthly requests? (The review's open point.)
2. What may an entity upload as evidence? (§2)
3. Accepting local accounts against CS-011, and until when (SR-81), and password reset by e-mail (SR-83).
4. Publish NOTIFICATION_ROUTING and templates for `MONTHLY_UPDATE_REMINDER`.
5. The map's coordinates, and whether production uses OpenFreeMap's public instance or hosts its own (§3).

# Platform Accessibility Audit

| Field | Value |
| --- | --- |
| Task | TASK-087 — Execute Platform-Wide Accessibility Audit (P15 - Testing & QA, P1) |
| Depends on | TASK-032 — Users, Roles & Permissions Administration (FG-03 Frontend); TASK-042 — Project Register & Creation UI (WF-01 Frontend) |
| Record date | 2026-10-18 |
| Status | **RUN, REMEDIATED AND RE-RUN on the e2e stack. Zero Critical or Serious violations on 25 screens and states in Arabic and English; Tab reaches every control on all of them, including the 10 highest-traffic screens. Two findings closed, two open for AHDA (A11Y-F-003, A11Y-F-004), so `--gate` stays red until they are closed or deferred.** Audited against WCAG 2.1 Level AA, the delivery-team baseline, until AHDA confirms PTBC-040 (OQ-010) |
| Branch | `test/task-087-platform-accessibility-audit` |
| Directory | `docs/testing` |
| Deliverables | **Accessibility audit report (both locales):** this record, with the recorded runs in `accessibility-audit/results/` (before and after remediation). **Remediation tracker:** `accessibility-audit/remediation-tracker.csv` (§7). The harness: `src/frontend/e2e/accessibility/` (screen set, audit, negative control, runner), `accessibility-audit/accessibility-audit.py` (`--record`, `--check` in `repo-checks`, `--gate` before UAT), `.github/workflows/accessibility-audit.yml` |
| Environment variables / secrets | None (workbook). The audit reads the e2e suite's `E2E_BASE_URL`, `E2E_TARGET` and credentials (`e2e-suite.md` §4); on the e2e stack these are local defaults |
| Sources | Workbook TASK-087 row (description, acceptance criteria, validation checks, gate decision); `ptbc-themes.csv` PTBC-040 and Open Question OQ-010; Step 16 Frontend Registers v1.0 `09_Accessibility_Localization` (FE-AX-001 to FE-AX-007) and `02_Navigation_Routes` (screen ids); `load-test.md` §4.2 (the ten highest-traffic screens); `test-strategy.md` §2, §4; `e2e-suite.md` §3–§5; WCAG 2.1 |

## 1. What was asked, and what this answers

| Asked | Answered in |
| --- | --- |
| Automated (axe-core) audit of the highest-traffic screens, Arabic (RTL) and English (LTR) | §3.1, §4: axe-core 4.13.0 in Chromium against the production build, 25 screens and states × 2 locales |
| Manual keyboard audit | §3.2, §4.2: real Tab presses through every screen until focus comes back round; visible focus measured at each stop; modals checked for focus staying in and returning on close. Scripted, not a person at a keyboard (§3.2, last paragraph) |
| Manual screen-reader audit | §3.6, §5.4: every screen's accessibility tree (what a screen reader is given) reviewed in both locales. **No session with NVDA, JAWS or VoiceOver output heard by a person was run: A11Y-F-004, open** |
| Conformance level from PTBC-040, an Open Question until confirmed; WCAG 2.1 AA as working baseline | §2, D-1 |
| Zero Critical/Serious violations on the audited set, both locales | §4.1: zero in the run after remediation, and zero before it |
| Full keyboard reach on at least the 10 highest-traffic screens | §4.2: every control reached on all 25, including the 10 (§2.1) |
| Findings tracked to closure or explicit AHDA-accepted deferral | §7: six rows; two findings closed and verified by a re-run, two findings open for AHDA, two advisories |
| Validation: re-run after remediation, zero Critical/Serious open | §4: run `2026-10-18-after-remediation`; `accessibility-audit.py --check` holds every later run to it |
| Validation: tab through the 10 screens, every control reachable with a visible focus state | §4.2 |

## 2. Baseline and severities

**D-1. WCAG 2.1 Level AA, the delivery-team position, pending AHDA (PTBC-040, OQ-010).** The workbook's gate decision fixes it
as the working baseline; `ptbc-themes.csv` records PTBC-040 as "Open (baseline proposed)", owned by the AHDA Business Sponsor,
to be fixed before UAT because the audit is scored against it. Every verdict here is against WCAG 2.0 and 2.1, Levels A and AA
(axe tags `wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa`). If AHDA sets another level, add its tags to `BASELINE_TAGS` in
`src/frontend/e2e/accessibility/audit.ts` and re-run (§8). For WCAG 2.2 AA that adds, among others, 2.4.11 Focus Not
Obscured, 2.5.8 Target Size (Minimum) and 3.3.8 Accessible Authentication, which this run did not test.

**D-2. "Critical" and "Serious" are axe-core's impact levels.** The acceptance criterion's two severities are axe's top two of
four (critical, serious, moderate, minor). A finding from manual review is given the level axe would give a defect of its
kind.

**D-3. Findings and advisories.** A *finding* fails a success criterion of the baseline, or is a gap in the audit itself; the
acceptance criterion's "tracked to closure or AHDA-accepted deferral" applies to findings, and `--gate` fails while one is
open. An *advisory* is a recommendation that fails no criterion (axe's best-practice rules are the same kind); it is tracked
and needs no acceptance.

### 2.1 The audited screen set

The ten highest-traffic screens are the ten TASK-086 measured as the screens the SPA's navigation leads to most
(`load-test.md` §4.2), ranked by Σ(people active at peak × visits per 100) over its persona groups, from that record's
workload model (its assumptions A-1, A-2: no production traffic exists yet). Each is opened as the group contributing most to
its index, with records on it. Around them: sign-in, which every session crosses; the screens of the two tasks TASK-087
depends on (FG-03 administration, WF-01 registration); and the states FE-AX-001/002 are about — a second step, a modal, form
errors.

| Id | Screen | Step 16 | Basis | Audited as |
| --- | --- | --- | --- | --- |
| A11Y-S01 | Sign in | SCR-001 | every session starts here | not signed in |
| A11Y-S02 | Home dashboard | DSH-002 on Home | traffic rank 1 (index 1,360) | Portfolio Manager |
| A11Y-S04 | Project workspace | SCR-040, DSH-009 | traffic rank 2 (935) | Project Manager |
| A11Y-S03 | Project register | SCR-025 | traffic rank 3 (730, with My projects) | Portfolio Manager |
| A11Y-S13 | My projects | SCR-026 | rank 3, as Project Managers open it | Project Manager |
| A11Y-S05 | Risk register | SCR-080 | traffic rank 4 (390) | Project Manager |
| A11Y-S09 | Notifications | SCR-150 | traffic rank 5 (345) | Project Manager |
| A11Y-S10 | Report run (Department Project Register) | SCR-133 | traffic rank 6 (340) | Department Manager |
| A11Y-S12 | Reports centre | SCR-130 | entry to rank 6 | Executive |
| A11Y-S06 | Issue register | SCR-083 | traffic rank 7= (305) | Project Manager |
| A11Y-S08 | Document library | SCR-120 | traffic rank 7= (305) | Department Manager ¹ |
| A11Y-S07 | Approval inbox | SCR-100 | traffic rank 9 (150) | Portfolio Manager |
| A11Y-S11 | Change requests | SCR-105 | traffic rank 10 (140) | Project Manager |
| A11Y-S14 | Register project (form) | SCR-033 | WF-01 dependency | Department Manager |
| A11Y-S15 | Users | ADM-002 | FG-03 dependency | System Administrator |
| A11Y-S16 | User detail | ADM-003 | FG-03 dependency | System Administrator |
| A11Y-S17 | Create user (form) | ADM-004 | FG-03 dependency | System Administrator |
| A11Y-S18 | Roles | ADM-006 | FG-03 dependency | System Administrator |
| A11Y-S19 | Role detail | ADM-007 | FG-03 dependency | System Administrator |
| A11Y-S20 | Permission matrix | ADM-009 | FG-03 dependency | System Administrator |
| A11Y-S21 | Role assignments | ADM-010 | FG-03 dependency | System Administrator |
| A11Y-S22 | Sign in — verification step | SCR-002 | second step of every privileged sign-in | administrator's credentials |
| A11Y-S23 | Approval inbox — decision dialog | SCR-100, MOD-040 | modal state of rank 9 | Portfolio Manager |
| A11Y-S24 | Register project — validation errors | SCR-033 | error state of the WF-01 form | Department Manager |
| A11Y-S25 | Create user — validation errors | ADM-004 | error state of the FG-03 form | System Administrator |

¹ Project Managers open the library more (160 against 75), but the e2e data set's Project Manager profile reads no documents
and would see only the empty state, so the next group is used to audit it with rows.

The data (`audit-data.setup.ts`, run first): an ACTIVE project through WF-01 and WF-03, with a risk, an issue, a change request
and a scanned document, and a second project waiting in AHDA's review, so the registers, the workspace and the approval inbox
are audited with rows and actions, not as empty states.

## 3. Method

Everything below runs in `src/frontend/e2e/accessibility/platform-accessibility-audit.spec.ts`, one test per screen and
language, in Chromium (Playwright 1.63, Desktop Chrome, 1280 × 720) against the e2e stack: the production SPA build under the
deployed host's headers (CSP included), behind the TLS edge, on the API built from the same commit (`e2e-suite.md` §3). The
language is set as a person sets it (the SPA's stored preference), and the screen counts as rendered when `<html lang dir>`
match the language, its `h1` shows, no loading state remains and the network is idle.

### 3.1 axe-core

axe-core 4.13.0 (the version the component tests use) is evaluated in the page through the DevTools protocol: the production
CSP refuses an injected script tag, and the audit does not weaken the headers it audits. All rules run. A violation tagged with
a baseline criterion counts against the acceptance criterion at critical or serious impact (D-2); best-practice rules are
reported apart. Unlike the component tests, which run in jsdom and must switch colour contrast off, this run measures
contrast. Where axe cannot decide a node's contrast — it gives up when a cell is clipped by a scrolling container — the
harness measures it itself: the text's colour against the first opaque background up its ancestors, translucent layers
composited, against 4.5:1 (3:1 for large text); a ratio below fails the screen.

### 3.2 Keyboard

The Tab walk starts at the top of the page and presses Tab until focus comes back to where it started, recording each stop.
It then compares the stops with every visible, enabled control on the screen (links, buttons, fields, summaries, anything
with a role a person operates or a tabindex): a control counts as reached when Tab stopped on it, or on the single tab stop of
the composite widget or radio group it belongs to. At each stop it compares the control's focused style with its unfocused
style: an outline, ring, border, background, underline or colour change is a visible indicator, none is a failure (2.4.7). A
keyboard trap is focus that stays on one element for six presses (a native date input legitimately takes three, one per
segment). For a modal state the walk is confined to the open dialog, any stop behind it fails (focus escaping), and Escape must
close it and give focus back to the control that opened it.

This is the workbook's "manually tab through … confirming every control is reachable and has a visible focus state", done
with real key presses in a real browser and checked against the full list of controls, which a person tabbing by eye cannot
do reliably on a screen with 85 controls. It cannot judge whether the order is the one a person expects; the recorded orders
(§4.2) were read for that (§5.1).

### 3.3 The negative control

An audit that finds nothing is only evidence if it would have found something. `harness-control.spec.ts` runs in every audit:
on the real sign-in page, under the production headers, it plants an image without a text alternative, an unlabelled field,
text at about 1.3:1, a control Tab cannot reach, a button whose focus ring is suppressed, a field that swallows Tab, and an
element 600 px wide outside any scroll container — and requires the harness to report each one. During the task the focus
check was also mutated to always answer "visible"; the control failed on the suppressed ring, as it must.

### 3.4 Reflow

At 320 CSS px wide (1280 px at 400% zoom, WCAG 1.4.10) the page must not scroll sideways. Content that needs two dimensions —
the permission matrix, data tables — may scroll inside its own container, so only an element reaching past the viewport
outside every scroll container fails.

### 3.5 Language of parts

WCAG 3.1.2 requires a passage in another language to say so, or a screen reader reads an English project title in an Arabic
voice. axe checks that `lang` values are valid, not that they are present. The harness lists, on each Arabic screen, every run
of two or more Latin words whose effective `lang` is still Arabic (and on English screens, any Arabic), for review by hand:
people's names are exempt and look the same (§5.3).

### 3.6 What a screen reader is given

Each screen's accessibility tree (Playwright's ARIA snapshot: roles, accessible names, states, landmarks, headings, the
values a screen reader announces) is kept beside its record and was reviewed in both locales (§5.4). It is the browser's own
computation of what assistive technology receives. It is not a screen reader: it does not show how NVDA, JAWS or VoiceOver
phrase, order or skip that content, or how they handle Arabic. That session is A11Y-F-004.

## 4. Results

Two recorded runs, the same harness, data model and axe 4.13.0 on the e2e stack:

| Run | What ran | Screens × locales | Critical/Serious | Any axe violation | Controls (all screens) | Unreached | No visible focus | Traps | Reflow failures |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `2026-10-18-before-remediation` | SPA built from `dev` at e14d5ca, before any fix, on a database holding earlier audit runs' records | 25 × 2 | 0 | 0 | 2,888 | 0 | 0 | 0 | 0 |
| `2026-10-18-after-remediation` | SPA and API built fresh from this branch at bcab00f, new database | 25 × 2 | 0 | 0 | 2,482 | 0 | 0 | 0 | 0 |

The automated criterion held before any remediation: every feature's component tests run axe on every pull request, and the
shared colours were chosen for AA contrast when the shell was built (`styles.css`, TASK-032). What the run added is what those tests cannot see; the two
findings it led to (§7) are of that kind.

### 4.1 axe-core, per screen (run `2026-10-18-after-remediation`)

| Screen | Step 16 | Locale | axe passes | Violations (C/S/M/m) | Controls | Reached by Tab | Visible focus | Reflow 320 px |
| --- | --- | --- | ---: | --- | ---: | --- | --- | --- |
| A11Y-S01 Sign in | SCR-001 | ar | 30 | 0/0/0/0 | 6 | all | yes | yes |
| A11Y-S01 Sign in | SCR-001 | en | 30 | 0/0/0/0 | 6 | all | yes | yes |
| A11Y-S02 Home dashboard | DSH-002 (Home) | ar | 44 | 0/0/0/0 | 48 | all | yes | yes |
| A11Y-S02 Home dashboard | DSH-002 (Home) | en | 44 | 0/0/0/0 | 48 | all | yes | yes |
| A11Y-S03 Project register | SCR-025 | ar | 46 | 0/0/0/0 | 50 | all | yes | yes |
| A11Y-S03 Project register | SCR-025 | en | 46 | 0/0/0/0 | 50 | all | yes | yes |
| A11Y-S04 Project workspace | SCR-040, DSH-009 | ar | 41 | 0/0/0/0 | 61 | all | yes | yes |
| A11Y-S04 Project workspace | SCR-040, DSH-009 | en | 41 | 0/0/0/0 | 61 | all | yes | yes |
| A11Y-S05 Risk register | SCR-080 | ar | 46 | 0/0/0/0 | 43 | all | yes | yes |
| A11Y-S05 Risk register | SCR-080 | en | 46 | 0/0/0/0 | 43 | all | yes | yes |
| A11Y-S06 Issue register | SCR-083 | ar | 46 | 0/0/0/0 | 44 | all | yes | yes |
| A11Y-S06 Issue register | SCR-083 | en | 46 | 0/0/0/0 | 44 | all | yes | yes |
| A11Y-S07 Approval inbox | SCR-100 | ar | 41 | 0/0/0/0 | 46 | all | yes | yes |
| A11Y-S07 Approval inbox | SCR-100 | en | 41 | 0/0/0/0 | 46 | all | yes | yes |
| A11Y-S08 Document library | SCR-120 | ar | 46 | 0/0/0/0 | 47 | all | yes | yes |
| A11Y-S08 Document library | SCR-120 | en | 46 | 0/0/0/0 | 47 | all | yes | yes |
| A11Y-S09 Notifications | SCR-150 | ar | 39 | 0/0/0/0 | 40 | all | yes | yes |
| A11Y-S09 Notifications | SCR-150 | en | 39 | 0/0/0/0 | 40 | all | yes | yes |
| A11Y-S10 Report run | SCR-133 | ar | 45 | 0/0/0/0 | 48 | all | yes | yes |
| A11Y-S10 Report run | SCR-133 | en | 45 | 0/0/0/0 | 48 | all | yes | yes |
| A11Y-S11 Change requests | SCR-105 | ar | 44 | 0/0/0/0 | 41 | all | yes | yes |
| A11Y-S11 Change requests | SCR-105 | en | 44 | 0/0/0/0 | 41 | all | yes | yes |
| A11Y-S12 Reports centre | SCR-130 | ar | 41 | 0/0/0/0 | 62 | all | yes | yes |
| A11Y-S12 Reports centre | SCR-130 | en | 41 | 0/0/0/0 | 62 | all | yes | yes |
| A11Y-S13 My projects | SCR-026 | ar | 46 | 0/0/0/0 | 45 | all | yes | yes |
| A11Y-S13 My projects | SCR-026 | en | 46 | 0/0/0/0 | 45 | all | yes | yes |
| A11Y-S14 Register project (form) | SCR-033 | ar | 37 | 0/0/0/0 | 56 | all | yes | yes |
| A11Y-S14 Register project (form) | SCR-033 | en | 37 | 0/0/0/0 | 56 | all | yes | yes |
| A11Y-S15 Users | ADM-002 | ar | 46 | 0/0/0/0 | 87 | all | yes | yes |
| A11Y-S15 Users | ADM-002 | en | 46 | 0/0/0/0 | 87 | all | yes | yes |
| A11Y-S16 User detail | ADM-003 | ar | 43 | 0/0/0/0 | 60 | all | yes | yes |
| A11Y-S16 User detail | ADM-003 | en | 43 | 0/0/0/0 | 60 | all | yes | yes |
| A11Y-S17 Create user (form) | ADM-004 | ar | 38 | 0/0/0/0 | 65 | all | yes | yes |
| A11Y-S17 Create user (form) | ADM-004 | en | 38 | 0/0/0/0 | 65 | all | yes | yes |
| A11Y-S18 Roles | ADM-006 | ar | 41 | 0/0/0/0 | 65 | all | yes | yes |
| A11Y-S18 Roles | ADM-006 | en | 41 | 0/0/0/0 | 65 | all | yes | yes |
| A11Y-S19 Role detail | ADM-007 | ar | 34 | 0/0/0/0 | 57 | all | yes | yes |
| A11Y-S19 Role detail | ADM-007 | en | 34 | 0/0/0/0 | 57 | all | yes | yes |
| A11Y-S20 Permission matrix | ADM-009 | ar | 45 | 0/0/0/0 | 57 | all | yes | yes |
| A11Y-S20 Permission matrix | ADM-009 | en | 45 | 0/0/0/0 | 57 | all | yes | yes |
| A11Y-S21 Role assignments | ADM-010 | ar | 46 | 0/0/0/0 | 84 | all | yes | yes |
| A11Y-S21 Role assignments | ADM-010 | en | 46 | 0/0/0/0 | 84 | all | yes | yes |
| A11Y-S22 Sign in — verification step | SCR-002 | ar | 30 | 0/0/0/0 | 5 | all | yes | yes |
| A11Y-S22 Sign in — verification step | SCR-002 | en | 30 | 0/0/0/0 | 5 | all | yes | yes |
| A11Y-S23 Approval inbox — decision dialog | SCR-100, MOD-040 | ar | 20 | 0/0/0/0 | 3 | all | yes | yes |
| A11Y-S23 Approval inbox — decision dialog | SCR-100, MOD-040 | en | 20 | 0/0/0/0 | 3 | all | yes | yes |
| A11Y-S24 Register project — validation errors | SCR-033 | ar | 37 | 0/0/0/0 | 56 | all | yes | yes |
| A11Y-S24 Register project — validation errors | SCR-033 | en | 37 | 0/0/0/0 | 56 | all | yes | yes |
| A11Y-S25 Create user — validation errors | ADM-004 | ar | 42 | 0/0/0/0 | 65 | all | yes | yes |
| A11Y-S25 Create user — validation errors | ADM-004 | en | 42 | 0/0/0/0 | 65 | all | yes | yes |

axe's "needs review" results, all on the permission matrix (A11Y-S20), were decided by hand (§5.2): colour contrast on the
cells the matrix's scroll container clips (20 nodes measured by the harness, the lowest at 16.58:1 against
4.5:1), and `th-has-data-cells`, an axe heuristic on full-width group headers.

### 4.2 Keyboard

Every screen and state, in both locales: Tab reached every control and came back round, every stop showed a visible focus
indicator, nothing trapped focus. The ten top-traffic screens hold 936 controls across both locales. In the decision
dialog (A11Y-S23) focus stayed in the dialog through all its controls and returned to the Approve button that opened it when
Escape closed it, in both locales. Focus order is the DOM order in both directions: skip link, brand, language, notifications,
sign-out, the sidebar sections, then the page; in Arabic the layout mirrors and the order follows it right to left, as
expected.

### 4.3 Reflow, contrast, language

No screen scrolls sideways at 320 px in either locale; wide tables scroll in their own containers. Every contrast axe left
undecided measured at least 16.58:1. Language of parts: the risk, issue and change-request registers and the workspace
heading showed English titles untagged in the Arabic interface (1, 16, 16 and 16 items before; 0 after, A11Y-F-002); the
Department Project Register report still does (25 items before on the larger data set, 2 after on a fresh database: A11Y-F-003), and a user's job title (A11Y-F-006); the rest are
people's display names and service principals' names, exempt.

## 5. Manual review

### 5.1 Step 16 accessibility controls

| Control | What it asks | Outcome |
| --- | --- | --- |
| FE-AX-001 Keyboard / focus | tab order follows the reading order; visible focus; dialogs trap and restore focus; no dead ends | Met on every audited screen (§4.2). Dialogs are the native `<dialog>` with `showModal()`, which gives the trap, the inert background and Escape |
| FE-AX-002 Labels / errors / status | programmatic labels; error summary and inline errors; async status announced without technical detail | Met: every field named (axe `label`, the trees), errors marked `aria-invalid` and described (A11Y-S24, S25), loading and notices in `role="status"`. Announcement as heard: A11Y-F-004 |
| FE-AX-003 Status / colour / tables | non-colour indicators; table headers; chart alternatives | Met: every status is a word (badges, "Green", "No data — it is not zero"); tables have captions and scoped headers; dashboard figures are tables with captions, not images. The permission matrix's group headers were wrong: A11Y-F-001, closed |
| FE-AX-004 Arabic / English / RTL | language switches labels and direction; mixed-language values isolated; ids unchanged | Met for direction and isolation (`<html lang dir>` flips; translated parameters are isolated with U+2068/U+2069; codes `dir="ltr"`). Stored text in the other language must say so: A11Y-F-002 closed, A11Y-F-003 open |
| FE-AX-005 to 007 | dates and numbers, responsive layout, historical labels | Not accessibility conformance items of this baseline; reflow at 320 px (§3.4) covers the part of FE-AX-006 WCAG requires |

### 5.2 axe "needs review" decided

- **Colour contrast, A11Y-S20.** axe could not determine the background of 89 cells in English and 17 in Arabic because the
  matrix's scroll container clips them (fewer in Arabic, where the container clips a different part of the table). Measured by the harness:
  dark text `rgb(27, 31, 35)` on white, 16.58:1. Conforms.
- **`th-has-data-cells`, A11Y-S20.** axe 4.13 decides a `th` whose `scope` is neither `col` nor `row` by its row: a row of only
  header cells makes it a column header (`_getScope`). The matrix's group header — one `th` spanning the row,
  `scope="rowgroup"` since A11Y-F-001 — is therefore taken as the column header of all twelve columns, and `traverseForHeaders`
  caches the headers it finds on that one node, so a later column inherits an earlier column's headers and axe finds no data
  cell for the R08 column. Every row has twelve cells and column 12 holds `Entity`, `Assigned` or `— Not granted` (checked in
  the page). The markup follows the HTML table model; the result is the heuristic's, and it does not change with the markup's
  scope value. Conforms.

### 5.3 Screen-reader session still to run (A11Y-F-004)

For a person with a current NVDA + Firefox (Windows) and VoiceOver + Safari (macOS), in Arabic and in English, on A11Y-S02 to
A11Y-S11, A11Y-S20 and A11Y-S23, with the data of §2.1:

1. Move by landmarks and by headings: one main, one `h1` naming the screen, the page title announced on arrival.
2. Read the first two table rows by cell: each cell announced with its column (and in the matrix, its group and permission).
3. Hear a project or risk title in the other language read in that language's voice.
4. Open the decision dialog: its name announced, focus inside, Escape returning to Approve.
5. On A11Y-S24/S25, submit with errors: each error announced with its field.
6. Trigger a notice (e.g. a saved filter) and hear the status without moving focus.

Record each defect as a tracker row, with the screen, locale, reader and browser.

### 5.4 Accessibility-tree review

All 50 trees (25 screens and states × 2 locales): exactly one `h1`, no skipped heading level, no control without an accessible
name, landmarks banner / navigation / main / search where present, tables with captions, regions named by their headings,
dialog named by its title. The signed-in shell gives 14 navigation landmarks, one per sidebar section (A11Y-F-005, advisory).

## 6. Remediation

| Finding | Change | Test that fails without it |
| --- | --- | --- |
| A11Y-F-001 | `PermissionMatrixPage.tsx`: one `<tbody>` per permission group, headed by `<th scope="rowgroup">`, replacing one body with `scope="colgroup"` headers and no column groups | `identity-access/screens.test.tsx` "heads each permission group as a row group" |
| A11Y-F-002 | `lang` from the record's own language beside the existing `dir="auto"`: `PageHeader` (new `titleLanguage`, used by `ProjectWorkspace`), `RiskTable`, `ConcernTable`, `EscalationTable`, `ChangeRequestTable` | assertions in `risks.test.tsx`, `concerns.test.tsx`, `changeRequests.test.tsx`, `workspace.test.tsx` |

Each test was run with its fix reverted and failed (5 of 5), then passed with it. The re-run after remediation is
`2026-10-18-after-remediation`.

## 7. Remediation tracker

`accessibility-audit/remediation-tracker.csv` is the tracker of record; `accessibility-audit.py --check` (every PR) holds it
to the recorded runs and `--gate` (UAT entry, Release Checklist "Accessibility Audit Findings Closed") fails while a finding is
open. A deferral is valid only with AHDA's acceptance in `ahda_acceptance`; a deferred axe rule is also listed, per screen, in
`src/frontend/e2e/accessibility/accepted-deferrals.json`, so the audit stops failing on it and on nothing else. None is
deferred today.

| Id | Kind | Criterion | Severity | Screens | Status |
| --- | --- | --- | --- | --- | --- |
| A11Y-F-001 | Finding | 1.3.1 Info and Relationships (A) | Moderate | Permission matrix | **Closed** — verified in `2026-10-18-after-remediation` |
| A11Y-F-002 | Finding | 3.1.2 Language of Parts (AA) | Moderate | Workspace, risk, issue and change-request registers | **Closed** — verified in `2026-10-18-after-remediation` |
| A11Y-F-003 | Finding | 3.1.2 Language of Parts (AA) | Moderate | Report run | **Open** — the API's `ReportCell` carries no language; an FG-02 contract change, or AHDA accepts a deferral |
| A11Y-F-004 | Finding | audit method | Moderate | top-traffic screens | **Open** — the screen-reader session of §5.3, or AHDA accepts the tree review in its place |
| A11Y-F-005 | Advisory | landmark practice | Minor | signed-in shell | Open — a design decision for the UX owner |
| A11Y-F-006 | Advisory | 3.1.2 (role label) | Minor | User detail | Open — the directory holds no language for a job title |

## 8. Running it

```sh
src/frontend/e2e/accessibility/run.sh                     # build the e2e stack from this tree, fresh database, audit every screen
src/frontend/e2e/accessibility/run.sh -g A11Y-S05         # the screens whose title matches
E2E_REUSE_STACK=1 src/frontend/e2e/accessibility/run.sh   # against the stack already up
python3 docs/testing/accessibility-audit/accessibility-audit.py --record <yyyy-mm-dd-name> --note "…"   # keep the run's figures
python3 docs/testing/accessibility-audit/accessibility-audit.py --check   # what repo-checks runs
python3 docs/testing/accessibility-audit/accessibility-audit.py --gate    # the UAT check
```

Each run writes one JSON record, ARIA snapshot and screenshot per screen and language to
`src/frontend/test-results/accessibility` (cleared at the start of a run) and the HTML report to
`src/frontend/playwright-report/accessibility`. `accessibility-audit.yml` runs the audit weekly against the default branch,
on pull requests that change it, and on demand, and attaches the records; CI runs are evidence for comparison, while this
record's verdicts come from the runs kept in `results/`. A new screen is a row in `screens.ts`; `--check` then requires it in
the latest recorded run in both locales.

## 9. Findings of this record

| # | Finding | Disposition |
| --- | --- | --- |
| F-1 | The conformance level is AHDA's (PTBC-040, OQ-010); this audit used WCAG 2.1 AA (D-1). | **Open** — AHDA Business Sponsor, before UAT. A different level means re-running with its rules |
| F-2 | Run on the e2e stack standing in for SIT (`e2e-suite.md` F-1), with the e2e data set's test profiles and English test data (F-2 there). Real data will be mostly Arabic, which turns A11Y-F-002/003's direction round (Arabic titles in the English interface), not their substance. | **Open** — re-run on SIT with AHDA's data before UAT sign-off |
| F-3 | Chromium only, desktop width only (plus 320 px reflow). The supported browsers, devices and breakpoints are unconfirmed (Step 16 16-F-001). | **Open** — re-run in the browsers AHDA confirms; the harness takes any Playwright browser |
| F-4 | What no automated check decides is left to the session of §5.3: whether names and instructions are meaningful, whether the order makes sense to a person, how Arabic is spoken. | **Open** — A11Y-F-004 |

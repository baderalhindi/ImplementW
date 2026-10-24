# End-User Guide and FAQ (Arabic + English)

| Field | Value |
| --- | --- |
| Task | TASK-096 — Author End-User Guide & FAQ (Arabic + English) (P17 - Documentation) |
| Depends on | TASK-042 — Project Register & Creation UI (`project-registration-ui.md`); TASK-070 — Dashboards UI (`dashboards-ui.md`) |
| Record date | 2026-10-24 |
| Status | **BUILT; BILINGUAL REVIEW PENDING.** The User Guide, FAQ and Knowledge Base are written in both languages and served in the app under Help and Knowledge. Every role's §20.2 landing page has an article in both languages, enforced by a test. The five sampled articles passed an independent bilingual pre-review, and its 27 findings are applied (§6.1). The second acceptance criterion needs a bilingual business reviewer's sign-off, which has not happened yet (§6.2) |
| Branch | `docs/task-096-end-user-guide-faq` |
| Deliverables | **Content (ar/en):** `src/frontend/src/content/help/{en,ar}/guide.json` (9 articles), `faq.json` (21 questions in 5 groups), `knowledge-base.json` (19 terms); `help.ts` (article metadata, the §20.2 landing map, the `[[key]]` label references). **Screens:** `src/frontend/src/app/help/` — SCR-011 User Guide, SCR-012/SCR-126 FAQ, SCR-125 Knowledge Base, and Blueprint §20.1 menu 11 Help and Knowledge (`menus.ts`). **Checks:** `content.test.ts`, `help.test.tsx`. This record |
| Gate decision applied | **"Bilingual per ADR-012: interface and controlled content in Arabic and English."** Every article, question and term exists in both languages, each language written in its own file (D-5) |
| Participation amendment | **ADR-012 EXTENDED: narrative carries its entry language; mixed-language reporting is accepted.** The FAQ says so to users: text that people typed stays in the language it was written in (`faq.notifications.language`) |
| Controlled sources | Blueprint v2.0 §20, §20.1 (menu 11: "User guide; FAQ; support"), §20.2 (role-based landing pages), Appendix B (SCR-010–012, SCR-125–126); Step 16 Frontend Registers (01_Canonical_Inventory, 02_Navigation_Routes, 04_Screen_Contracts, 16_Source_UI_Contracts FE-SRC-0426/0427/0441/0442); WF-12 §13 table 27 rows 8–9 and Appendix A |

## 1. Scope

The task populates the in-app help for the core workflows, aligned to the screens as built. The screens it names did
not exist: the shell's comment read "Help and Knowledge has no screen yet, so it is not listed". The repository owner
chose to build read-only help screens with the content (D-1), so the content is reachable in the app rather than
sitting unused in a folder.

Out of scope: SCR-010 Help Center, and §20.1's "support" entry. No support channel, contact list or ticketing
integration is defined anywhere in the sources, and inventing one would be a decision this task cannot take (F-6).

## 2. Decisions

| # | Decision | Basis |
| --- | --- | --- |
| D-1 | **Read-only help screens are built with the content.** Step 16 marks SCR-011/SCR-012 "INHERITED GAP — NO BEHAVIOR CONTRACT" and "do not implement route behavior from inventory alone". The pages built here hold no action, no field, no API call and no data. They render bundled text, so there is no behaviour for a contract to govern. Building them was the repository owner's choice for this task (2026-10-24) | Step 16 04_Screen_Contracts; WF-12 FE-SRC-0426/0427 ("handled as shared/help content") |
| D-2 | **Routes `/help/guide`, `/help/guide/:articleId`, `/help/faq`, `/help/knowledge-base`**, under one Help and Knowledge menu, the eleventh of §20.1, after Administration. Step 16 proposes `/views/scr-125/knowledge-base` and `/views/scr-126/faq`, but no built screen uses the `/views/scr-nnn/` pattern; every module mounts under its own path. The proposed routes are marked PROPOSED in the register | Blueprint §20.1; Step 16 02_Navigation_Routes |
| D-3 | **One FAQ page serves SCR-012 and SCR-126; one Knowledge Base page serves SCR-125.** The inventory lists FAQ and Knowledge Base twice, under Common (SCR-012 "FAQ / Knowledge Base") and under Documents (SCR-125, SCR-126). WF-12 classifies both as "Help/shared content rather than governed Project Documents". Two copies of the same answers would drift, so each page carries its screen ids in `app/help/routes.tsx`. §20.1 menu 7 lists "knowledge base" under Documents; the Documents menu is unchanged, and the knowledge base sits under Help and Knowledge only | Blueprint Appendix B; WF-12 §13 |
| D-4 | **The guide offers each person the articles for their roles first; internal users can also read the rest; external users see only what is written for them.** Article audiences are role codes in `help.ts`. The page shows "For your role" and, for internal users, "Other guides". An external user opening an internal article directly gets "Page not found". This is presentation, not protection: the text is public. It keeps an entity user's help to its own channel, in line with ADR-013's least-disclosure posture | ADR-013; Step 16 08_Authorization_UX |
| D-5 | **Each language is written, not translated.** The Arabic is written in a formal administrative register for an Arabic reader, not sentence by sentence from the English. The two files must carry the same articles, sections, steps, notes, questions and terms, so a reviewer can compare them side by side and a missing translation fails a test. Paragraph count and wording are free | ADR-012; task acceptance criterion 2 |
| D-6 | **Interface labels are referenced, never retyped.** Inside any help text, `[[projects.nav.create]]` is replaced by the label in the reader's language, in bold. The guide therefore names every control exactly as the screen does: renaming a label renames it in the help, and deleting a key fails `content.test.ts`. 390 references across the six files | `shared/i18n` (TASK-032) |
| D-7 | **Statements are limited to what the code, the seed or a record shows.** Each article was written from the built screens' i18n files, routes, access rules and the seeded dashboard catalogue (`db/seed/seed-master-data.sql`), not from the specs alone. Where the platform's exact rule was not visible (the health formula, the session lifetime), the text says what the user sees and stops there | — |

## 3. Coverage: Blueprint §20.2 landing pages

`LANDING_ARTICLE` in `help.ts` maps every role to its article; `content.test.ts` fails if a role has none, if the
article is missing in either language, or if its audience leaves the role out.

| Role | §20.2 default landing page | As built (`dashboards-ui.md` D-2) | Guide article |
| --- | --- | --- | --- |
| R01 System Administrator | Administration Dashboard | GOVERNANCE, headed "Administration Dashboard" | `administrator-home` |
| R02 Portfolio Manager | Portfolio Dashboard | PORTFOLIO | `portfolio-dashboard` |
| R03 Department Manager (seeded "Sector Manager") | Department Dashboard | PORTFOLIO in the sector scope, headed "Sector Dashboard" | `portfolio-dashboard` |
| R04 Project Manager | My Work / Project Manager Dashboard | GOVERNANCE | `project-manager-home` |
| R05 Liaison Officer | My Actions Dashboard | GOVERNANCE | `liaison-home` |
| R06 Viewer | Read-Only Dashboard | PORTFOLIO | `portfolio-dashboard` |
| R07 Executive Management | Executive Dashboard | PORTFOLIO | `portfolio-dashboard` |
| R08 External Entity User | External Contributor Dashboard | "Your projects", then PROJECT in the project's Overview | `external-contributor-home` |

The core workflows beyond the landing pages: `getting-started` (everyone), `register-a-project` (R04, R08: the
`PROJECT_REGISTER` holders), `submit-a-progress-update` (R04), `decide-an-approval` (internal roles: approval
follows approval authority, not a role grant).

## 4. Verification

| # | Check | Result |
| --- | --- | --- |
| 1 | `content.test.ts`: same articles, sections, steps, notes, FAQ groups, questions and terms in both languages; no empty text; Arabic text is Arabic; every role has a landing article in both languages; every `[[key]]` and related-screen label exists in both languages | 6 passed |
| 2 | `help.test.tsx`: the Help and Knowledge menu and its three screens; each of R01–R08 offered its landing article; an external user offered only its own articles, and an internal article opened directly reads not found; an article names controls by their on-screen label in Arabic and in English, with no raw `[[` left; FAQ and Knowledge Base render every question and term; axe finds no violation on the article, FAQ and Knowledge Base pages in either language | 18 passed |
| 3 | `shell.test.tsx` with Help and Knowledge as the eleventh menu | 8 passed |
| 4 | Full frontend gate in `AHDA-frontend`: `npm run lint`, `format:check`, `typecheck`, `npx vitest run`, `npm run build` | all exit 0; 64 files, 1076 tests passed (1052 before) |
| 5 | Real browser (Chromium, puppeteer), signed in as a Project Manager: guide index, an article, the FAQ and the Knowledge Base in Arabic, an article in English | `dir` is `rtl` for Arabic and `ltr` for English; no raw reference in the page; labels shown in the reader's language. The check found that a section's paragraphs render before its steps, so three paragraphs written as follow-ups were rewritten as introductions in both languages. Screenshots in git-ignored `artifacts/task-096/` |

## 5. Acceptance criteria

| # | Criterion | Status |
| --- | --- | --- |
| 1 | Every role's default landing workflow (Section 20.2) has at least one corresponding guide article in both languages | **MET** — §3; enforced by `content.test.ts` on every build |
| 2 | A business reviewer confirms the Arabic content is not a machine-only translation of the English without review | **NOT YET MET** — the Arabic was written for Arabic readers (D-5), and an independent pre-review of the sample found no literal translation after its calques were fixed (§6.1), but no bilingual business reviewer has signed off. §6.2 is the sign-off sheet |
| — | Validation: sample 5 guide articles per language and confirm a bilingual reviewer has signed off on accuracy and tone, not just literal translation | **PARTIAL** — the 5 articles were sampled and pre-reviewed in both languages, and every finding is fixed (§6.1). The bilingual reviewer's sign-off is still owed (§6.2) |

## 6. Bilingual review and sign-off

### 6.1 Independent pre-review (2026-10-24) — not the sign-off

The sample of five articles below was reviewed in both languages by a reviewer independent of the author: an AI
reviewer given only the content files and the code, with no part in writing them. It checked three things. Accuracy:
each statement against the built screens, access rules and the seed. Tone: formal register for government staff.
Literalness: whether the Arabic was written for Arabic readers. **This is not the bilingual business reviewer's
sign-off the acceptance criterion requires (§6.2); it was done so that the reviewer signs corrected text.**

| Article | EN accuracy | EN tone | AR accuracy | AR tone | AR not literal |
| --- | --- | --- | --- | --- | --- |
| `getting-started` | issues → fixed | issues → fixed | issues → fixed | issues → fixed | minor calques → fixed |
| `portfolio-dashboard` | issues → fixed | issues → fixed | issues → fixed | issues → fixed | calques → fixed |
| `project-manager-home` | issues → fixed | pass | issues → fixed | issues → fixed | calques → fixed |
| `external-contributor-home` | issues → fixed | pass | issues → fixed | issues → fixed | minor calques → fixed |
| `register-a-project` | issues → fixed | pass | issues → fixed | issues → fixed | minor calques → fixed |

The reviewer judged the Arabic "mostly written for Arabic readers, not word for word", and listed its calques
sentence by sentence. 27 findings, all applied (`artifacts/task-096/review-fixes.py`, git-ignored):

- **Accuracy (both languages):**
  - The menus show every area to everyone, except Administration; access is decided by the platform. The same
    error in the FAQ answer `missing-menu` was fixed too.
  - The counts beside Requests and Approvals and beside Notifications were explained.
  - Sign-in: multi-factor authentication can be required for roles other than R01, and the first sign-in shows an
    enrolment key.
  - Closing the tab ends the session.
  - The Sector list offers the sectors of the projects you may see.
  - Not every widget has Open.
  - A widget's source is shown as a workflow code.
  - The dashboard tabs have no visible "Your dashboards" label.
  - Reporting completeness counts projects; it does not list them.
  - Blocking a task asks for a reason, and open subtasks block completion.
  - The external user's Project Dashboard also shows current figures, and only the named responder sees the form.
  - Drafts are listed among an entity's responses.
  - The person named as Project Manager must hold that role over the project.
  - **Widgets read Restricted until the role is granted the view (F-9).** The same note was added to the unsampled
    administrator and liaison articles, whose roles are affected in the same way.
- **Arabic grammar and meaning:**
  - «الأرقام اللاتينية» read as Roman numerals.
  - «بنوعيها المنشورة والحالية» lacked agreement.
  - «الإدارة التنفيذية» named a unit, not the R07 role «تنفيذي».
  - «ما تستحقه مشاريعك» meant "what your projects deserve".
  - «سمِّ … أنت» was in the wrong case.
  - «لم يصدر بعد» had the project as its subject instead of its number.
- **Arabic calques:** «تفتح الرئيسية على», «تبلغها أدواره», «لا يملك قيمة», «رفع التقدم», «قيمك السابقة», «تنقل
  مراجعة الهيئة», «بوصفه مراجعة تالية».

After the fixes, the reviewer's verdict is that English and Arabic are both ready for human sign-off.

### 6.2 Bilingual business reviewer's sign-off

The reviewer reads each sampled article in both languages, in the app (Help and Knowledge → User guide; the language
switch is in the top bar), and records for each language: **accuracy** (it matches the screen), **tone** (natural,
formal register for AHDA staff), and **not a literal translation**. Findings go in the table; fixes are made in the
JSON files and re-reviewed.

| Article (sample of 5) | English: accuracy / tone | Arabic: accuracy / tone / not literal | Reviewer | Date |
| --- | --- | --- | --- | --- |
| `getting-started` | | | | |
| `portfolio-dashboard` | | | | |
| `project-manager-home` | | | | |
| `external-contributor-home` | | | | |
| `register-a-project` | | | | |

| Reviewer name and role | Signature | Date | Outcome (accepted / accepted with changes / rejected) |
| --- | --- | --- | --- |
| | | | |

## 7. Findings

| ID | Finding | Owner / where it goes |
| --- | --- | --- |
| F-1 | **Arabic "التنبيهات" names two things.** The shell's Notifications menu (`common.menu.notifications`) is "التنبيهات", and so is the Alerts screen inside it (`notifications.nav.alerts`), while the notification screens call themselves "الإشعارات". The help uses the labels as they are (D-6), so it inherits the clash. Suggested: menu "الإشعارات". | Frontend (WF-15 UI), with the bilingual reviewer |
| F-2 | **R03's name differs between sources.** Blueprint §20.2 says Department Manager and Department Dashboard; the seed and the UI say "Sector Manager", "Sector Dashboard" and "Sector" (القطاع). The help follows the UI. | TASK-027 / AHDA terminology owner |
| F-3 | **"Access denied" always says "available to system administrators only"** (`common.forbidden.description`), including on pages guarded by other roles. The FAQ explains the title only. | Frontend (shell) |
| F-4 | **No dedicated message for a wrong password or a locked account** at sign-in: these fall to generic problems. The help does not describe them, because their wording is not fixed. | FG-03 UI |
| F-5 | **The content must follow the screens.** D-6 catches a renamed or deleted label, but not a changed behaviour: a change to a documented workflow (statuses, required fields, who may act) needs its article updated in the same PR. | Every UI task's PR (reviewers) |
| F-6 | **SCR-010 Help Center and §20.1's "support" are not built**: no support channel is defined in the sources. | AHDA service owner, then a follow-up task |
| F-7 | **Arabic count agreement on dashboards.** `dashboards.coverage.counted` prints «احتُسب 10 من 10 مشروعًا»; Arabic needs the plural «مشاريع» for 3–10 (found by the pre-review). | Frontend (FG-01 UI) |
| F-8 | **"Revision" is labelled «المراجعة» in Arabic** (`projects` `fields.revision`, and two `revision` strings), the same word as "review". The help says «نسخة» for revision to avoid the clash. | Frontend (WF-01 UI), with the bilingual reviewer |
| F-9 | **The landing dashboards read Restricted for most roles as shipped.** R02, R06 and R07 hold no view grant on any dashboard source; R03's grants do not include them either; R05 holds none; R01 holds only CONFIGURATION_VIEW (`db/seed/seed-master-data.sql`; `dashboards.md` F-3). The help states this, so it stays true either way, but the §20.2 landing pages show little until the Blueprint Appendix A view grants are shipped. | PMO / TASK-110 (permission profiles) |

## 8. Change log

| Date | Change | Author |
| --- | --- | --- |
| 2026-10-24 | Independent bilingual pre-review of the five sampled articles (§6.1); its 27 findings applied in both languages, plus the FAQ `missing-menu` answer and the Restricted note in the administrator and liaison articles. Findings F-7 to F-9. Human sign-off still open (§6.2). | Documentation (TASK-096) |
| 2026-10-24 | Initial build: 9 guide articles, 21 FAQ entries, 19 knowledge-base terms in English and Arabic; SCR-011, SCR-012/SCR-126 and SCR-125 as read-only pages under the new Help and Knowledge menu; landing-page coverage and language-parity tests; bilingual review sheet pending. | Documentation (TASK-096) |

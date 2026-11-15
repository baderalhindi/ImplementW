# RPMO review, round 8

Branch `feat/task-112-review-round8` (TASK-112), stacked on `feat/task-112-review-round7`. The client's Review Round 8 (revised, 15 Nov 2026) has 16 points, one page each, built in the order the client suggests. Users, roles and permissions wait for the RACI matrix; phone layouts are out of scope.

## Point 00: the index

The index lists the points and what the 15 Nov build showed. It asks for no change of its own. Its two open items from round 7 belong to point 02:
- no project has a stage plan yet, so the stage chart puts all 167 projects under "لم تُرفع مرحلة بعد";
- the Overview still shows the declared progress from the import.

## Point 01: every dashboard element opens the records behind it

A count opens a list of the same records, so the list's total equals the number clicked. The link carries the dashboard's filters, and Back returns to the dashboard with them, because they are in its URL.

### Where each element goes

| Element | Opens |
|---|---|
| Portfolio, initiative or sector name, or its bar | `/projects?registered=true&<its filter>`; the report card stays as a second link, "بطاقة التقرير" |
| "المشاريع المسجلة" | `/projects?registered=true` |
| "قيد التنفيذ" | `/projects?registered=true&status=ACTIVE` |
| "إجمالي القيمة" | `/projects?registered=true&sort=value:desc` |
| "متوسط المخطط", "متوسط الفعلي", "الفارق" | `/projects?registered=true&measured=true`, which adds the planned and actual columns |
| "تحتاج متابعة" and a table's follow-up count | `/projects/follow-up` with the same filters |
| A status bar or row | `/projects?registered=true&status=<state>` |
| A stage bar or row | `/projects?registered=true&stage=<code>`; "لم تُرفع مرحلة بعد" is `stage=NONE` |
| An on-plan state | `/projects?registered=true&onPlan=<ON_TRACK, LATE, STALLED or NOT_MEASURED>` |
| A follow-up reason | `/projects/follow-up?mainReason=<reason>` |
| A count in a breakdown row | The register or the Follow-up list filtered to that row |
| A sector's split by portfolio | The register filtered to that sector and portfolio |
| The lifecycle widget's state (Portfolio, Governance) | `/projects?status=<state>` with no `registered`, because that widget counts every state |
| The risk widget's rating | `/risks?rating=<code>`, or the project's own risks on the Project Dashboard |
| Home: projects, active projects, open risks | `/projects`, `/projects?status=ACTIVE`, `/risks` |
| A map city | `/projects?cityItemId=<id>` (already built) |

### Why the counts match

- **`registered=true`**: the portfolio figures leave out projects still being registered (Draft, Submitted, Under review, Returned). `ProjectLifecycleStates.Registering` now holds that list for both.
- **`stage=NONE`**: the chart counts a project under "no stage yet" when it has no current stage, or one outside the stage version in force. The register uses the same rule.
- **`onPlan` and `measured`**: these are calculated per project, so the register reads every match, applies the dashboard's rule, then pages.
- **`mainReason`**: the dashboard counts each project once, under its first reason. `reason` still keeps every project that has the reason at all.
- **The Follow-up list**: it now narrows by the dashboard's filter bar through the same `PortfolioInsightFilters.Matches`.

**Checked on localhost (15 Nov):**
- As local.r02, r07 and r03, 154 counts were compared with the totals of the lists they open, and none differed (`artifacts/round8/counts.py`). `EachDashboardCountEqualsTheListItOpens` checks the same in the integration suite.
- The lifecycle widget's states equal the register's totals.
- "سياحة ترفيهية" opens 8 projects with the chips "المحفظة الاستراتيجية: سياحة ترفيهية" and "التسجيل: المشاريع المسجلة فقط".

### Lists that take filters in the URL

- **Project register:** it already took its filters in the URL. It now adds `registered`, `stage`, `onPlan`, `measured` and the sort `value:desc`. Each one shows as a removable chip.
- **Follow-up list:** `mainReason` plus the dashboard's filter bar, each one a chip.
- **Risk register:** `view` and `rating` moved from page state into the URL. Across projects, it also takes the dashboard's project filters, and reads only the projects they keep.
- **Task lists:** the status filter moved into the URL.

### Clickable and keyboard

- **Bars and columns:** each one is a link, named by its words and count, with a tooltip such as "عرض المشاريع (8)". Tab reaches it and Enter opens it.
- **Donut segments:** they open on click, and their legend or table carries the same links for the keyboard.
- **Headline cards:** the whole card opens. A link inside a card stays a separate link.
- **Feedback:** hovering a linked element tints it, and the pointer and focus ring show it is clickable.

### Not linked, and why

- **Health, KPI condition, reporting completeness and the money figures:** no list filters by them yet.
- **"Unclassified" rows** (a project with no portfolio, initiative or sector): the register has no filter for "none".
- **Donut bands of the entity completion chart:** point 03 builds that list.

### Open

- **Scope.** The dashboard counts with the portfolio permissions' scope (PORTFOLIO_SUMMARY_VIEW and the others). The register and the Follow-up list read with PROJECT_VIEW's scope. For R02, R03 and R07 on localhost they reach the same projects. A role whose two scopes differ would see different totals. That is a permissions question, held back for the RACI matrix.
- **Not walked for every role.** The browser walk was run as local.r02 only. The other dashboards (Sector, Project Manager, Liaison, Viewer, Executive and the external user's) were checked by API counts and tests, not clicked through.

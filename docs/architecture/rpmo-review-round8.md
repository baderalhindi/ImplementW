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

## Point 02: each project's overall progress from its schedule or its stages

### The method

`project.progress_method` is `STAGES`, `SCHEDULE` or null. The migration is `TASK-112_AddProjectProgressMethod`, and it sets `STAGES` on projects that already had a stage plan.

| How the method is set | What happens |
|---|---|
| A new registration | Starts on `STAGES`. The form's stage-plan section offers the choice, and choosing the schedule hides the stage plan. |
| Saving a stage plan | Sets `STAGES` on a project that has no method. |
| `POST /projects/{id}/set-progress-method` (`progressMethod`, optional `reason`, If-Match) | Allowed for the stage-plan writers: the registration's writer, the Project Manager (PROGRESS_SUBMIT), and R02/R03 (PROJECT_REGISTER). Not allowed while the registration is under review, or once the project is closed. |

**Changing the method:**
- **Before an approved update:** the change is free.
- **After one:** an update is approved once one is published or an entity update is accepted. From then on a change needs a reason (`PROJECT_PROGRESS_METHOD_REASON_REQUIRED`).
- **History:** every change is audited as `Project.ProgressMethodChanged`, with from, to and the reason. It shows in the project's history.
- **Updates:** a project on the schedule method reports without stages, and its registration needs no stage plan.

### The one figure

The figure is `ProjectOverallProgress`, and it lives in the project's indicators. The Overview, the register, the follow-up list, the portfolio dashboard's averages, gap and on-plan counts, and the report cards all read it. The gap and the on-plan label are calculated from it, not from the declared values.

| Method | Actual | Planned |
|---|---|---|
| Stages | Σ weight × % complete over the stages that apply | The same sum over each stage's planned % today, from the plan's dates |
| Schedule | The work breakdown's weighted progress (WF-02's roll-up) while there is an active baseline | The baseline's planned % today |
| None | The declared figure, labelled "رقم معلن — لم تُحدد طريقة الاحتساب" with its source | The declared figure |

**Where the stage method's % complete comes from,** newest first:
1. The latest published stage-based update, with the weights it recorded.
2. An accepted entity update: its current stage and its %.
3. The plan's starting %. Saving it also sets the project's current stage, so the stage chart and the register filter count the project where its figure puts it.
4. The imported current stage.

With a current stage and its %, the stages before it count as complete and those after it as not started.

**Arithmetic:** `ProjectOverallProgressTests` checks the client's worked example. With weights 10/15/10/50/10/5 and completions 100/100/100/40/0/0, the figure is 55%.

**The weights in force are 5/15/10/55/10/5,** as published in round 7. With those weights the same completions give 52%. Localhost shows 52% for that reason.

**Without a PROJECT_STAGE version in force,** the stage method shows the declared figure, because there is nothing to weigh.

### Screens

- **Overview.** "الإنجاز الكلي" comes first, showing:
  - actual, with its as-of date;
  - planned to date;
  - the gap;
  - the method and the current stage;
  - on the stage method, a bar per stage with its weight, planned % and % complete.

  Someone who may set the method sees "حدد طريقة احتساب الإنجاز" while there is no method, or no plan on the stage method. A schedule-method project without an approved baseline says so. The follow-up panel no longer repeats the declared progress.
- **Progress tab.** Shows the method.

### Test data on localhost

`artifacts/round8/seed_methods.py`, as local.r04, set up these projects:
- **Stage method:** projects 1, 2 and 3 got stage plans with starting %. They show 52.0%, 26.0% and 9.5%, and the stage chart counts them under Execution, Tendering and Design.
- **Schedule method:** project 4 shows no figure, because no baseline could be approved. The pmplatform_demo database has no published GOVERNANCE_PROFILE version (`CONFIGURATION_MISSING` on submit).

The script left a schedule with two activities on project 4. It deleted the draft baseline it had created.

### Not done

- **FG-02 report exports.** They read the published snapshots through the governed REPORT_RULES configuration, the same blocker as round 7's stage columns.
- **Schedule figure on localhost.** It can't be shown until GOVERNANCE_PROFILE is published there.

### Noticed, not changed

Four external update requests in pmplatform_demo name the contribution schema `PROGRESS_V1`, which the platform does not define. Listing them answers 500 (`ContributionSchemas.Of`).

### Tests

- Unit tests: 2,378 pass.
- `StageProgressTests` adds the method rules, the shared figure and the reason.
- Project, Progress, stage, ExternalParticipation, Dashboards and Reports integration suites: 232 of 234 pass when run together. `ReportSavedViewTests` and `MasterDataConfig.ProjectStageTests` failed only when run with the others, and pass alone.
- Frontend: 1,416 tests pass.

## Point 03: entity update donut, average, top five, "see more"

The card is on the Portfolio dashboard, for AHDA's people who hold EXTERNAL_REQUEST_VIEW. It shares its period with the entity table under it.

- **Title.** The title carries the period: "نسبة إنجاز التحديثات حسب الجهة · نوفمبر 2026". The last three months read "سبتمبر – نوفمبر 2026", and this year reads "2026". These match the API's period: the month start, two months back.
- **Centre.**
  - Shows "متوسط الجهات": the API's `averagePercent`, the simple average of each entity's rate over the entities that had requests in the period.
  - Each entity counts once. Rounding up accepted over issued is no longer used.
  - A tooltip gives the definition.
  - It reads "—" when no entity had requests.
- **Ring.**
  - Only the entities with requests, in four bands: 90% or more, 70–89%, 50–69%, below 50%.
  - The "no requests" slice is gone. Two lines under the ring say "من N جهة لديها طلبات في الفترة" and "N جهة بلا طلبات في الفترة". The second counts active entities only.
- **Beside the ring.**
  - Five entities, the lowest first. A toggle switches between "الأدنى" and "الأعلى".
  - Each entity shows its name, linked to its request monitor (`/external-requests/monitor?externalEntityId=`), a % bar, and "accepted من issued".
- **Links.**
  - A band (segment or legend) opens `?completionBand=<band>#entity-completion` on the same dashboard, keeping its filters. "عرض المزيد" opens the same address without a band.
  - The page scrolls to the table.
  - With a band in the address, the table shows that band's entities and a "عرض كل الجهات" link. Its columns and its order (lowest % first) are unchanged.
  - This also completes point 01's "donut band → entity list".

**Localhost data, as of 15 Nov.** No entity had requests this month. Over the last three months, one entity (هيئة التراث) had 4 requests and none accepted. Those are the same 4 requests whose undefined `PROGRESS_V1` schema breaks their list.

## Point 04: group the project page's 19 tabs

- **Headings.** The project page shows six headings on one row: نظرة عامة, التخطيط, الأداء, المخاطر, الطلبات, السجلات. Under them, a second row shows only the open heading's tabs, as smaller pills. Overview has no second row.

  | Heading | Tabs |
  |---|---|
  | نظرة عامة | Overview |
  | التخطيط | Schedule, tasks, milestones |
  | الأداء | Progress, financials, KPIs |
  | المخاطر | Risks, issues and challenges |
  | الطلبات | Change requests, suspension, closeout, external update requests |
  | السجلات | Registration, location, documents, review history, activity history, audit history |

  Groups are defined in `workspace/tabGroups.ts`.
- **What each person sees.** A heading shows only when the person is offered at least one of its tabs, by the same rules as before (`visibleTabs`).
- **Addresses unchanged.** Every tab keeps its address, so `/projects/{id}/progress` opens Progress under الأداء, and old links keep working.
- **Last tab, remembered.**
  - The last tab a person opens on a project is kept in their browser, per person and per project.
  - Opening the project at its plain address returns to that tab, when it is still offered.
  - Choosing Overview on the page keeps Overview.
  - A heading opens the remembered tab when it is under that heading, otherwise its first tab.
- **Checked in a browser at 1280 px, as local.r02.**
  - The six headings stay on one row.
  - Every tab opens through its heading.
  - After Financials, leaving and reopening the project returns to Financials.
  - `/projects/{id}/progress` opens Progress.
- **Tests not yet updated.** The workspace tests that list every tab in one row describe the old single row. They are updated in the CI step.

## Administration tabs on one row (15 Nov request, beside point 04)

The client asked for this outside the PDFs. Administration's 19 screens wrapped onto two rows. They now sit under six headings on one row, and the open heading's screens show on a second row, as on the project page:

| Heading | Screens |
|---|---|
| المستخدمون والصلاحيات | Users, Roles, Permission matrix, Role assignments |
| الهيكل التنظيمي | Departments, External entities |
| البيانات المرجعية | Master data, Project stages |
| التكاملات | Integrations overview, Integration monitoring, Integration logs, API integration catalogue |
| الأنظمة المرتبطة | SSO and directory, E-mail and Exchange, Nafath, Internal systems |
| البيانات والتدقيق | Data migration, Audit trail, Integration audit logs, System event logs |

- **Where it lives.** `ADMINISTRATION_GROUPS` in `app/menus.ts`. A screen not named there joins the last heading.
- **What each person sees.** Each screen keeps its permission and its address. A heading shows only when the person is offered one of its screens, and it opens the first of those.
- **Other menus.** They keep their single row.
- **Checked in a browser at 1280 px, as local.r01.** The six headings fit on one row, and each heading's screens fit on one row.
- **Point 15.** This does not cover the admin side menu.

# RPMO review, round 7

Branch `feat/task-112-review-round7` (TASK-112). This round has five items from the client's Review Round 7 document. Decisions the client made when asked:

| Question | Answer |
|---|---|
| What R02 may edit on an approved project | Every registration field except the budget, the planned dates and the governance profile, which still change through a change request. |
| Stage weights | The standard weights only. A project cannot override them. |
| Which projects have a stage plan | Every governance profile. |
| Which stages | The suggested six, in one list. |

## Item 1: stage-weighted progress

**Stages.** The seed publishes the six stages as a PROJECT_STAGE version:

| Code | Arabic | Weight |
|---|---|---|
| `BUSINESS_CASE` | دراسة الجدوى وحالة العمل | 5 |
| `DESIGN` | التصميم | 15 |
| `TENDERING` | الطرح والترسية | 10 |
| `EXECUTION` | التنفيذ | 55 |
| `TESTING` | الاختبار والتشغيل التجريبي | 10 |
| `HANDOVER` | التسليم والإغلاق | 5 |

The seed adds this version only when the family has no published version. AHDA changes a weight by publishing a later version at Administration › Project stages.

**Stage plan.** Each project has one row per stage in `project.project_stage_plan`, with:
- whether the stage applies;
- its planned start and end;
- for a project already under way, its starting %.

The plan is read and written at `GET/PUT /api/v1/project-stage-plans/{projectId}`, under the project's ETag. `GET /api/v1/project-stages` lists the stages a new registration starts from.

| When | What the plan allows |
|---|---|
| Draft or Returned | Free edits. A stage's dates may wait until submission. |
| Submitting the registration | The plan must be complete (`PROJECT_INCOMPLETE`, field `stagePlan`). |
| Under review | Locked (`PROJECT_STAGE_PLAN_UNDER_REVIEW`). |
| Approved, plan already set | A change must claim a `STAGE_PLAN_CHANGE` authorisation of the plan's current revision. |
| Approved, no plan yet | Set once without a change request. This covers imported projects and projects approved before stages existed. |

An approved Schedule change request (one with a schedule impact) issues the `STAGE_PLAN_CHANGE` authorisation. The plan editor offers the issued authorisations by their change request's title.

**Module boundary.** Project does not reference ChangeRequest, because that would make a cycle with edge 6. Instead, Project declares the port `IStagePlanChangeAuthorization`, and ChangeRequest implements it.

**Progress.** A period started while stages are in force is stage-based:
- **Weights.** Its stages (`progress.progress_stage_entry`) copy the weights of the version in force that day. Its later revisions keep them, so a weight changed later never moves a recorded period. The next period uses the new version, and the update shows the version number it used.
- **Overall figure.** Σ weight × % complete ÷ Σ weight, over the stages that apply. 100/100/100/40/0/0 gives 52.0%, and 43.5% without Design.
- **Planned %.** Taken on the period's date from the plan's dates: 0 before the start, 100 from the end, linear between. The planned overall uses the same weights.
- **Current stage.** The first applicable stage below 100%.
- **Execution.** Comes from the schedule's roll-up, read-only, while an active baseline gives a figure. Before that it is entered. The Progress tab says which.
- **Entry rules.** A started stage needs its actual start, a complete stage its finish, and a % below last period's needs a reason.
- **No schedule needed.** A stage-based period needs no work breakdown to be submitted.
- **No plan yet.** Starting an update refuses with `PROGRESS_STAGE_PLAN_REQUIRED`, and the screen opens the plan first.
- **Publishing.** Writes the actual, the planned figure and the current stage to the project, with source PROGRESS_UPDATE. These feed the gap, the on-plan indicator and the Follow-up list, with the thresholds unchanged.

**Screens.**
- **Registration form.** A Stage plan section after Budget and schedule.
- **Overview.** A stage bar: each stage's planned % today beside its % in the latest published update, with the current stage marked; overall planned, actual and gap; and the stage plan dialog.
- **Progress update.** A Stage progress step before the narrative. The overall figure and the current stage update as the PM types.
- **Progress history.** The stage table of every published period.
- **Portfolio dashboard.** The status chart is now titled المشاريع حسب الحالة. A separate المشاريع حسب المرحلة section shows the project count and value per current stage.
- **Import.** The template (version 2) has Current stage and % complete of the current stage. An import never overrides the stage of a project that has published a stage-based update.
- **Entity monthly update.** For a project with a plan, the update asks for the current stage, each stage shown with its planned %, and that stage's %. Accepting it weighs the overall figure from the plan.

**Not done: the FG-02 report exports.** The Project report and Progress history reports have no stage columns yet. Their columns come from the governed REPORT_RULES configuration, so adding them needs a stage projection and a new published REPORT_RULES version. The in-app progress history shows the stage tables.

**Tests.** The seed's stage version is withdrawn in the shared identity test database, so suites written before stages keep testing the paths without them. The stage suites (`ProjectStagePlanTests`, `StageProgressTests`, `StageVersionChangeTests`, `MonthlyStageUpdateTests`) publish stages in a database of their own.

## Item 2: assign or change the Project Manager

The registration form names the Project Manager. This is kept as `proposed_project_manager_user_id`, which grants nothing: setting the manager on a draft would have changed who owns it under OWN scope. The submit step proposes that person.

**Assign / change Project Manager.** This action on the Overview (`POST /projects/{id}/change-manager`):
- **When.** Works at any state but Closed, with a reason.
- **Who.** Needs the new permission `PROJECT_MANAGER_CHANGE`: R02 over every project, R03 over its own department.
- **New manager.** Must hold R04 over the project (`PROJECT_MANAGER_INVALID`). Naming the current manager answers `PROJECT_MANAGER_UNCHANGED`.
- **Handover.** The outgoing manager's open tasks and update requests move in the same unit of work, through `IProjectManagerHandover`, implemented by ProjectTask and ExternalParticipation. The entity contact role moves with them when the outgoing manager held it.
- **Notification.** Each manager gets a `Project.ManagerChanged` notification intent (family `PROJECT_MANAGER_CHANGED`, addressed by `recipientUserId`). As with every other family, delivery needs the family in NOTIFICATION_ROUTING and its templates published.
- **Audit.** `Project.ManagerChanged`: from, to, the reason, and how many records moved.

## Item 3: master data values

Administration › Master data now:
- shows each value's display order;
- edits its Arabic and English names and its order (the code stays fixed): a draft by its author, a published value as an audited correction;
- deletes a value nothing references, drafts included.

A value that records reference is retired instead, and the answer says how many records use it. The count reads every foreign key to `master_data_item` from the database's own constraints.

Validating and publishing take `Identity:ProfileApproval:RequiredAdministrators` people, as permission profiles and project stages already do. Set it to 1 on an environment with one administrator; the default is 3.

## Item 4: R02 edits an approved registration

`PROJECT_REGISTRATION_EDIT` (R02, every project) opens the registration form on a project past review, at any state but Closed, with a required reason:
- **Locked fields.** The budget, the planned dates and the governance profile are shown read-only. The API refuses a change to them with `PROJECT_CHANGE_REQUEST_REQUIRED`.
- **Drafts.** A draft or returned registration is edited as before (`PROJECT_EDIT_AS_DRAFT`).
- **Audit.** `Project.RegistrationEdited`, each field old and new, with the reason.

## Item 5: entity update completion

A donut on the Portfolio dashboard shows the entities' update completion, banded, with the overall rate. It shares the period with the entity table.

## Data

- **Migrations.** `TASK-112_AddStagePlansAndStageProgress` and `TASK-112_AddProposedProjectManager`, both forward-only. Three enum CHECKs are widened with the reviewed DROP CONSTRAINT marker.
- **Seed.** After migrating, run the seed: it brings the two new permissions and the stage version.
- **The 167 imported projects.** They have no stage plan. Each one sets its plan once from the Overview or when its next update starts.

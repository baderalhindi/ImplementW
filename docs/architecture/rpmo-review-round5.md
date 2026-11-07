# RPMO review, round 5 (7 Nov 2026)

What the platform does about *RPMO Platform — Review Round 5*, where it departs from that document, and what is still open. The migrations are named `TASK-112`, as round 4's are. Run on a clone of the showcase unless a row says otherwise.

## Status

| # | Request | Status |
| --- | --- | --- |
| 1 | Review of R04 submissions | **Done.** §1 |
| 2 | Select, edit, delete, sort | **Part done.** Project register and Users, Departments; the other tables are not done. §2 |
| 3 | Select all eligible projects | Done |
| 4 | Add beside Import | Done. KPIs get Add KPI; KPI definitions stay AHDA's catalogue (§3) |
| 5 | User search | Done. §4 |
| 6 | Thousands separators | Done on the named fields. There is no screen for amounts in admin settings to apply it to |
| 7 | Stages and weights | **Stages editable and versioned; no figure reads the weights yet.** §5 |
| 8 | Password change | Already built in round 4; verified. §6 |
| 9, 10 | Completion rate | Done |
| 11 | Map providers | Answered: `map-provider-comparison.md`. Nothing changed in the code |
| 12 | Viewer | Starting proposal applied; list for confirmation in §7 |
| 13 | Governance profile | Tooltip added; answer in §8 |
| 14 | Top bar | Done. Create is a role setting, off for R02 and R03 |
| 15 | Risk matrix for readers | Done |
| 16–18, 20 | Wording | Done. Also: the approvals inbox named requests by code and roles by a short ID |
| 19 | AHP-000040 | Canceled can now be recorded; the showcase row still needs correcting (§9) |

## 1. Why R04's submissions could not be reviewed

There were two separate causes, and neither was the rule that no one reviews their own submission being applied to a different person by mistake in the rule itself.

**Registration.** Starting a review created the approval run with the *reviewer* as its requester. The separation rule then, correctly, kept the requester from deciding it, so the R02 who pressed Start review found an empty inbox; only a second R02 could decide. The tests and the e2e journeys always used a different person to start and to decide, which hid it. A registration now records who submitted it (`project.submitted_by_user_id`), and that person is the requester. No error is raised or logged in this case: the task simply did not appear, so there is no correlation ID to send for it.

**Progress updates.** WF-02 had never been connected to its inputs. Its only source was a placeholder, so every project answered `422 PROGRESS_ROLLUP_UNAVAILABLE` when an update was started: reproduced on the clone with correlation ID `14bfaabd-718a-4b72-af22-bf953694c4f5`. It now reads the work breakdown, the active baseline, Schedule Health and the financial status (ADR-003 §8.2 edges 55 and 56).

**Departure the client should confirm.** None of RPMO's 167 imported projects has a work breakdown, so "derived, never typed" (ADR-009) would still block all of them. Such a project now reports the figure declared on it. With none, the Project Manager enters one with where it comes from. The published figure replaces the declared one, so the register and dashboards show it. A project with tasks is unaffected.

Financial updates failed for a related reason, `FINANCIAL_NOTHING_TO_REPORT`: periods were generated only by a progress update. WF-14 now generates a due period itself.

### Walked with R04 submitting and R02 deciding

| R04 submits | R02 | Result on the clone |
| --- | --- | --- |
| Registration, naming the Project Manager | Start review, return, then approve, then activate | Passed by API, and in the browser up to approval |
| Progress update | Start review, return, publish | Passed. R04 reviewing their own is refused |
| Financial update | Start review, return, publish | Passed |
| Schedule baseline (STANDARD project) | Return, approve; reject on another | Passed. A LIGHT project's baseline takes effect with no approver, by its profile |
| Milestone claim | Return; a new claim; accept | Passed |
| Change request | Start review, approve from the inbox | Passed |
| Suspension request | Start review, approve from the inbox | Passed. It takes effect by the worker on its date |
| Completion request | — | Submitting is refused with `CLOSURE_BLOCKER_EXISTS` until progress has been published. **Not walked past that point** |

The Project Manager is named at submission, as the client asked to check: the draft has none, and the submit dialog offers "Me" or someone else.

A run already pending when this ships keeps its old requester, so a second R02 must decide it. The showcase has none.

## 2. Table actions

Shared: `shared/ui/rowSelection.tsx` and `useRowSelection.ts` (checkbox per row and in the header, the count, the actions), and a sort option in the shared filter bar.

| Table | Select, count | Edit | Delete or equivalent | Sort |
| --- | --- | --- | --- | --- |
| Project register, My projects | Yes | Link, on DRAFT and RETURNED | Drafts only, confirmed by name; a submitted project is withdrawn or closed | Yes, on the server |
| Users | Yes | On the user's page | None by design: a user is disabled, because the audit trail names them | Existing |
| Departments | Yes | Existing | Deactivate, confirmed by name; never deleted | **No** |

**Not done:** about 70 other table screens, including Entities, Master data, Roles, Tasks, Milestones, Risks, Issues, Change requests and Documents. Only Users had server sorting, so each list needs its own API sort, and each needs its own delete rules. The acceptance check "every table" is **not met**.

## 3. KPIs: who adds them

The client asked to confirm. On the shipped matrix **KPI_MANAGE is held by R02 (ALL), R03 (DEPT) and R04 (OWN)**, and AHDA approves the target. The tab's text said only AHDA assigns KPIs, which was wrong, and now says so. Add KPI offers a published KPI from AHDA's catalogue, its frequency and its target, which goes to AHDA for approval. Name, unit and direction belong to the catalogue entry, which only an administrator creates, so they are shown rather than typed. This departs from the review's "Add KPI (name, unit, frequency, direction, target)"; a free-form KPI would need the client's decision. "Record value" already existed on each KPI.

## 4. User search

The picker searched `GET /users`, which needs USER_VIEW, an administrator's permission, so others were asked for a typed ID. `GET /person-lookups` answers anyone holding a write permission, so not the Viewer, with people who are eligible for the field: holders of a role over the project for an owner, an external entity's own people for an entity caller. It gives name, username, roles and unit, and no contact details. Results appear as the person types. An entity, contribution type or task the person cannot read now says so and asks no ID.

## 5. Stages and weights

**The platform had no stages or weights.** "المشاريع حسب المرحلة" on the dashboard counts lifecycle statuses. PROJECT_STAGE is now a configuration family on the risk matrix's engine, edited at Administration › Project stages: add, rename in both languages, reorder, deactivate, weights validated to total 100, a new version with its effective date, an audit entry for each step. A stage once published cannot be removed from a later version. Resolving the family as of a past date gives the weights in force then. On an environment with one administrator (`Identity:ProfileApproval:RequiredAdministrators`, as for permission profiles) one person can validate and publish; every other family still needs three people.

**Open, and needed before the acceptance check can be fully met:** no figure uses the weights, because no source says which stage a project is in or how a figure is calculated from the weights. The statuses' display names are not editable yet; the client's note is a point to confirm rather than a specified change.

## 6. Password change

Built in round 4 and unchanged. On a clone, a LOCAL account changed its password from the account menu: the old password was refused, the new one worked, `IdentityAccess.PasswordChanged` was audited. **The client saw the service desk text because `local.r01` to `local.r08` are directory accounts**, for which that text is correct. `local.r02b` is a local account. Other sessions end at their next refresh, up to 15 minutes later, not at once. A link for directory accounts needs AHDA's address in `VITE_PASSWORD_RESET_URL`.

## 7. Viewer (R06): current list

As `GET /sessions/current/permissions` reports it after this change; scope in brackets. Removed: PROJECT_IMPORT and AUDIT_BUSINESS_VIEW. **PROJECT_IMPORT reverses the 6 Nov request that every role may import**, because the review now says the Viewer has no imports.

`CHANGE_REQUEST_VIEW`, `CLOSEOUT_VIEW`, `CONCERN_VIEW`, `DOCUMENT_VIEW`, `FINANCIAL_VIEW`, `KPI_VIEW`, `MILESTONE_VIEW`, `PORTFOLIO_STATUS_VIEW`, `PORTFOLIO_SUMMARY_VIEW`, `PROGRESS_VIEW`, `PROJECT_VIEW`, `RISK_VIEW`, `SCHEDULE_VIEW`, `SUSPENSION_VIEW`, `TASK_VIEW` (all DEPT); `MASTER_DATA_VIEW` (ALL).

Signed in as R06, every menu page and project tab opened with no create, submit, approve or export action. **For the client to confirm:** the proposal's Keep list does not name the risk, issue, change, suspension and closure registers, KPIs or schedule, which this list keeps read-only as "project pages". `local.r06` has no project on the showcase, because its department has none, so it sees empty registers there.

## 8. Governance profile

It sets how much control the platform applies to a project. Values below are the showcase's, which are **test values, not AHDA's policy**. Standard and Full behave the same today.

| What | Light (مبسط) | Standard (قياسي) | Full (كامل) | State |
| --- | --- | --- | --- | --- |
| Schedule baseline needs approval | No | Yes | Yes | Works |
| Risk register | Not available | Available | Available | Works |
| Reporting period | 7 days | 7 days | 7 days | Works. The monthly update batch is set up separately |
| Extra required fields | None | None | None | Possible on screens, none configured |
| Other approvals (registration, milestone, change, commitment, KPI target, issue resolution, suspension, resumption, completion, closure) | Yes | Yes | Yes | Same at every level |
| Approval routes | R02; a band 3 change also R03 | Same | Same | Can vary by level; none does |
| Change bands | Band 2: 5% of budget or 10 days. Band 3: SAR 200,000, 50% of duration, or any scope change | Same | Same | Can vary by level; none does |
| Reports and dashboards | No difference | No difference | No difference | Nothing reads the level |

Stored but with no effect: "band needs approval", the number of bands, "included in reporting", document control level, and the minimum budget and duration. An approved change of level does not change the project today.

**Our proposal, not a sourced rule:** Full for SAR 100 million and over, or strategic at any value; Standard from SAR 10 million to under 100 million; Light under SAR 10 million. It matches the 168 projects as loaded, whose levels Round 3 notes as assumed, so it describes the data rather than proving the rule. Choosing a level is by hand today.

**For AHDA:** the thresholds; what should separate Standard from Full; the real approval matrix and bands; the period per level; whether reporting should differ.

## 9. For the showcase

The branch is not deployed. To show it: migrate, seed, and for finding 19, as RPMO's data stays out of this public repository:

```sql
BEGIN; SET LOCAL session_replication_role = replica;
UPDATE project.project SET is_canceled = true WHERE legacy_reference = 'AHP-000040' AND lifecycle_state = 'CLOSED';
COMMIT;
```

The platform's lifecycle has one end state, so Canceled is recorded on a CLOSED project and shown in its place. The import sets it from the file's status word.

## 10. Decisions for the client

1. Declared figures for projects with no work breakdown (§1).
2. Which table comes next (§2).
3. Free-form KPIs, or the catalogue only (§3).
4. How a stage attaches to a project and what the weights calculate, and whether statuses' display names are to be editable (§5).
5. The Viewer's final list (§7).
6. The governance values (§8).
7. Which map option (`map-provider-comparison.md`).

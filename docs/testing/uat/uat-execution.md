# UAT Catalogue Preparation and Execution

| Field | Value |
| --- | --- |
| Task | TASK-088 — Prepare & Execute UAT Catalogue Scenarios (P15 - Testing & QA, P0) |
| Depends on | TASK-085 — Cross-Domain End-to-End Test Suite (`../e2e-suite.md`); TASK-084 — Platform Test Strategy & Coverage Policy (`../test-strategy.md`) |
| Record date | 2026-10-19 |
| Status | **PREPARED, NOT EXECUTED.** The cases in scope, the UAT data set, the execution log, the defect tracker and the sign-off record exist and are checked on every PR. UAT itself has not started: there is no UAT environment, no named AHDA tester, and nine Critical readiness defects stop it (§8). The Release Checklist gate stays **Not Started**, and `uat-catalogue.py --gate` fails |
| Branch | `test/task-088-uat-catalogue-execution` |
| Directory | `docs/testing/uat` |
| Deliverables | UAT execution log (`execution-log.csv`); defect tracker (`defect-tracker.csv`); UAT sign-off record (`uat-sign-off.md`). With them: the cases in scope (`scenario-register.csv`, `adr-013-cases.csv`), the check and gate (`uat-catalogue.py`), and the UAT data set (`src/frontend/e2e/uat/`) |
| Environment variables / secrets | None (workbook column). Staging on UAT reuses the e2e suite's `E2E_*` variables (`../e2e-suite.md` §4) |
| Participation amendment | ADR-013: UAT covers the entity Project Manager journey end to end, not only the contributor journey (§3) |
| Sources | Workbook TASK-088 row and Release Checklist sheet (local `AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`); Step 18 UAT Catalogue v1.0 (`UAT_Scenarios`, `UAT_Journeys`, `Test_Data_Evidence`, `Readiness_Gates`, `Execution_Log`, `Validation`), read from `Project Files.zip`; ADR-013 (`../../architecture/adrs/ADR-register.md`) |

## 1. What this task does, and what it cannot do alone

The workbook asks for three things: stage the Step 18 scenarios in a UAT environment with realistic data, coordinate AHDA's
business users while they execute them, and track pass, fail and defects to closure before production sign-off.

This record does the parts the delivery team can do and makes the rest checkable:

- **Scope, fixed and traceable.** The acceptance criterion's minimum subset is computed, scenario by scenario, from Step 18
  and the workbook (§2). The 16 ADR-013 cases are added to it (§3).
- **Staging.** A UAT data set is staged through the product's own API by the people who own each step (§5). It was staged
  and rehearsed on the e2e stack, which stands in for UAT until UAT exists.
- **Coordination.** Roles, cycles, run logging, defect triage and retest are defined (§4, §6), and the severity policy and
  exit criteria are proposed for AHDA's approval (§7).
- **Tracking.** The log, the tracker and the sign-off record are files that `uat-catalogue.py --check` holds consistent on
  every PR, and `--gate` judges against the Release Checklist (§9).

What it cannot do is execute UAT. Step 18 defines UAT as AHDA's business users accepting the product: "No execution or
approval claimed", and "named testers, dates, environment and exit policy require approval" (Step 18 Validation 18-DOD-07).
No AHDA tester was available to this task, and no UAT environment exists, because every environment apply is held on the
hosting confirmation (`../../security/penetration-test.md` §2). The delivery team's own runs are recorded as rehearsals and
never count as UAT outcomes (UL-1, G-1).

## 2. Cases in scope

The acceptance criterion: "at minimum, all scenarios tied to the 15 cross-domain journeys and every P0-priority task in this
workbook".

- **Tied to a journey.** Step 18 names the packages each journey exercises (`UAT_Journeys`), and gives every scenario one
  package. A scenario is tied to a journey when its package is one of the journey's. That ties **2,958** scenarios. The
  journeys name 16 packages, and BPK-008 has no scenario of its own. These are cycle 1 (§6).
- **Tied to a P0 task.** The workbook's P0 tasks include a build task for every one of the 21 domains: WF-01 to WF-15 (for
  example TASK-041/042 for WF-01, TASK-062 for WF-09) and FG-01 to FG-06 (TASK-028 to 033 for FG-03, TASK-034 for FG-04). A
  scenario is tied to the P0 tasks of its domain, so **every one of the 4,079 scenarios** is tied to one. The other 1,121 —
  WF-04, WF-06, WF-07, WF-09, WF-10 and WF-14, whose packages no journey exercises — are cycle 2.

The minimum the criterion defines is therefore the whole catalogue. The criterion's "subset" would only be smaller than
4,079 if some domain had no P0 task, and none lacks one.

`scenario-register.csv` holds one row per scenario: Step 18's identifiers, domain, package, wave, actor, execution mode and
capability, plus the journeys, P0 tasks and cycle this task derives. It is generated from the controlled files:

```sh
python3 docs/testing/uat/uat-catalogue.py --register "<path>/Project Files.zip" "<path>/AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx"
```

The script checks its two constants against those files as it writes the register. `JOURNEY_PACKAGES` must equal
`UAT_Journeys`. Every task in `P0_TASKS` must be P0 and name its domain, and no P0 task of a WF/FG phase may be left
unmapped. In CI, UR-1 checks each row against the same constants. Step 18's procedures, oracles and negative variants are
not copied: a tester reads them in Step 18 by scenario id, the controlled text.

| Journey | Packages | Scenarios tied |
| --- | --- | --- |
| UJ-001 | BPK-014 | 114 |
| UJ-002 | BPK-022 | 162 |
| UJ-003 | BPK-015, BPK-017 | 324 |
| UJ-004 | BPK-011, BPK-020 | 398 |
| UJ-005 | BPK-023 | 210 |
| UJ-006 | BPK-010 | 210 |
| UJ-007 | BPK-012 | 200 |
| UJ-008, UJ-009, UJ-010 | BPK-026, BPK-027 | 476 |
| UJ-011 | BPK-006 | 212 |
| UJ-012 | BPK-007 | 222 |
| UJ-013 | BPK-009 | 210 |
| UJ-014 | BPK-008, BPK-028 | 220 |
| UJ-015 | BPK-027 | 243 (also in UJ-008 to UJ-010) |
| **Distinct** | | **2,958** |

## 3. ADR-013: the entity Project Manager journey

Step 18 predates ADR-013. Its WF-13 scenarios cast the entity as a request-driven contributor ("External Entity User /
Project Contributor"), and no scenario follows an entity's own Project Manager from draft to an active, managed project.
ADR-013 makes R04 employer-neutral: the entity's Project Manager drafts and manages their own project, and AHDA keeps every
approval and lifecycle gate. The participation amendment requires UAT to cover that journey end to end.

`adr-013-cases.csv` adds 16 cases (UAT-A13-01 to 16), in the order the journey runs. Each has a procedure, an expected
outcome, a negative variant and the automated evidence that already exists:

| Cases | Journey stage | Domains |
| --- | --- | --- |
| A13-01 | Landing: only the entity's own projects, DSH-008 | FG-01, FG-03 |
| A13-02, A13-03 | Draft through the form; submit naming themselves | WF-01 |
| A13-04, A13-05 | AHDA decides registration, baseline and activation; the entity never can | WF-01, WF-03, WF-11 |
| A13-06 to A13-10 | Managing the active project: schedule, tasks, risks and issues, milestone claims with evidence, progress, change requests | WF-02 to WF-08, WF-12 |
| A13-11, A13-12 | Reading the dashboard and the entity report set, restricted figures masked | FG-01, FG-02, WF-14 |
| A13-13 | AHDA's update requests to the entity; contributors answer, AHDA applies | WF-13 |
| A13-14 | Suspension and closure decisions stay AHDA's | WF-09, WF-10 |
| A13-15, A13-16 | Another entity reaches nothing; an ended assignment ends access | FG-03, WF-13 |

The cases run in cycle 1 with the journeys. They use the personas `entityProjectManager`, `entityContributor` and
`otherEntityProjectManager` and the fixtures UAT-FX-07 to UAT-FX-09 (§5). Six of them are already expected to be blocked, by five defects
(UAT-D-001, -004, -006, -007, -008). All sixteen are blocked by UAT-D-009 until external users can sign in.

## 4. Roles and coordination

| Role | Who | Does |
| --- | --- | --- |
| UAT lead | QA Lead (the repository owner holds the role until AHDA names one, as for the e2e history: `../e2e-suite.md` F-6) | Plans cycles, assigns scenarios, runs daily triage, keeps the log and tracker, prepares the sign-off |
| Domain owners | AHDA, one per WF/FG domain | Confirm each scenario's oracle, actor and steps before it runs (18-G-02, 18-G-03); agree NOT APPLICABLE |
| Business testers | AHDA staff in the source actor's role, each with a named UAT account | Execute scenarios and record outcomes with evidence (EV-01) |
| QA support | Delivery QA | Stages data (§5), arranges TD-05 failures, supplies QA evidence for QA-assisted scenarios (`../test-strategy.md` §6), retests fixes before handing them back |
| Triage board | UAT lead, Engagement Architect, the domain owner concerned | Daily during a cycle: confirms each new defect's severity under §7, assigns it, decides retests |
| Acceptance | AHDA Business Sponsor and QA Lead | Approve §7 before execution (18-G-05); sign `uat-sign-off.md` at exit |

Assignment is by domain. Each domain owner names the testers for their scenarios, and the register's `Source actor` says
which role runs each one. A QA-assisted scenario (1,225) is run by the business tester. QA attaches the technical evidence:
the CI-run integration test that asserts the backend-only outcome, named by `Folder/Class.Method` and the CI run of the build
under test (`../test-strategy.md` §6).

## 5. Environment and the UAT data set

**Environment.** UAT is the `uat` environment of `infra/environments/github/environments.json`. A build reaches it only after
the 15 journeys pass on SIT (`e2e-sit`) and the QA and Security Leads approve. Neither SIT nor UAT exists yet (18-G-01).
Until they do, the e2e stack (`src/frontend/e2e/stack`) stands in for rehearsal. It runs the release built from the commit
under test on a fresh database, with the stand-ins `../e2e-suite.md` §3 lists (S-1 to S-7).

**Identities (TD-01).** The e2e personas map to Step 18's identity fixtures: an authorised actor
(`departmentManager`, `projectManager`, `portfolioManager`), the same role outside the assigned project
(`otherDepartmentManager`), a different external entity (`otherEntityProjectManager`), and a revoked context (UJ-011's
procedure). On UAT each persona is a named AHDA test account in the credentials secret (`../e2e-suite.md` §4). Several
roles are stand-in test profiles because of UAT-D-002.

**Records (TD-02, TD-03).** `src/frontend/e2e/uat/data-set.json` lists eleven projects, staged by
`src/frontend/e2e/uat/stage-uat-data.spec.ts`:

| Fixture | Title | State | For |
| --- | --- | --- | --- |
| UAT-FX-01 | إعادة تأهيل شبكة تصريف مياه الأمطار — المرحلة الثانية | DRAFT | WF-01 edit, submit, withdraw |
| UAT-FX-02 | Northern region school maintenance programme 2027 | SUBMITTED | WF-01 start of review |
| UAT-FX-03 | تطوير الواجهة البحرية — الحزمة الأولى | UNDER_REVIEW | WF-01/WF-11 registration decision |
| UAT-FX-04 | Southern city ring road — section B | APPROVED_PLANNED, schedule without baseline | WF-03 baseline |
| UAT-FX-05 | Public library renovation and digital services | ACTIVE, baselined; 3 tasks (2 started), 2 risks, 1 issue, 1 draft change request, documents CLEAN, QUARANTINED and held pending | WF-02 to WF-14 |
| UAT-FX-06 | مشروع إنارة الطرق الريفية | ACTIVE, other department | TD-01 out of scope |
| UAT-FX-07 | Industrial zone access road — entity delivered | ACTIVE, entity A's Project Manager; their 3 tasks (2 started), 2 risks and 1 issue | ADR-013 |
| UAT-FX-08 | مركز خدمات المستثمرين — مبادرة الجهة | DRAFT by entity A's Project Manager | ADR-013 A13-03 onward |
| UAT-FX-09 | تطوير مرافق الحديقة العامة | ACTIVE, entity B | TD-01, ADR-013 A13-15 |
| UAT-FX-10 | Heritage district pedestrianisation | ACTIVE | WF-09, reserved |
| UAT-FX-11 | مشروع تشجير المحاور الرئيسية | ACTIVE | WF-10, reserved |

Every fixture is created through the API by the people who own each step. The department or entity drafts; AHDA starts the
review and approves through WF-11; the Project Manager plans and submits the baseline; AHDA approves it and activates.
Every record is therefore in a state the platform itself produced. Each test asserts its fixture's state, and the run
writes the ids to `src/frontend/test-results/uat/fixtures/<id>.json`, which testers cite in a run's `Fixture IDs`.

**Projections and recovery (TD-04, TD-05).** These are time-dependent, so they are arranged per run, not pre-staged. QA runs
the named fixtures of `../e2e-suite.md` §5 (`uj-009-published-progress-period`, `uj-010-overdue-progress-period`,
`uj-004-failed-apply`, `uj-007-retry-due`, `uj-013-reconciliation-dead-letter`) or the integration suites' equivalents
(`../test-strategy.md` §9).

```sh
src/frontend/e2e/uat/run.sh                          # fresh e2e stack, stage every fixture, print the manifest
E2E_REUSE_STACK=1 src/frontend/e2e/stack/run.sh      # the rehearsal: every journey and ADR-013 spec on the staged stack
```

On UAT, the same spec stages the same set: run it with the e2e runner and the `E2E_*` variables pointing at UAT
(`--config e2e/uat.config.ts`). The data set's synthetic `E2E_*` reference codes are replaced by AHDA's approved master data
when UAT-D-003 closes. Stage once per environment: titles are not unique keys.

## 6. Executing and recording

**Cycles.**

| Cycle | Cases | Purpose |
| --- | --- | --- |
| 0 — rehearsal | the 15 journeys and both ADR-013 specs, by delivery QA, on the staged environment | Proves the environment and data before testers arrive (18-G-01 evidence). Logged as REHEARSAL; never a UAT outcome |
| 1 | 2,958 journey-tied scenarios and the 16 ADR-013 cases, wave by wave (W1 → W6) | The cross-domain acceptance the Release Checklist names |
| 2 | 1,121 remaining scenarios (WF-04, WF-06, WF-07, WF-09, WF-10, WF-14) | Completes the P0 domains |
| Retest | every case whose defect is fixed, plus the journey that owns its package | Closes defects; reruns the journey spec to catch regressions |

**One run, one row.** A tester's run of one case is one row of `execution-log.csv`, with Step 18's Execution_Log columns plus
`Run type`. Run ids are `UAT-nnnn` for UAT and `RH-nnnn` for rehearsals, issued in order by the UAT lead. A row records:

- `Case revision`: `v1.0` for Step 18; the date of the domain owner's confirmation once 18-G-02 revises a case;
- `Build / environment`: commit, digest and environment;
- `Tester`: name and role;
- `Fixture IDs`: the UAT-FX ids and record ids used;
- `Actual outcome`: what was seen, in words;
- `Result`: PASS, FAIL, BLOCKED or NOT APPLICABLE;
- `Defect / retest reference`: the UAT-D ids raised or met, and "retest of UAT-D-nnn" for a retest;
- `Evidence link`: the EV-01 bundle in AHDA's records system — screenshots, correlation ids, version ids, redacted (Step 18
  EV-01);
- `Acceptance reviewer`: who agreed a NOT APPLICABLE.

NOT RUN is never logged: a case with no row is not run. The latest UAT row of a case is its outcome. No row is edited after
the fact; a correction is a new run.

**Defects.** A FAIL or BLOCKED names a defect. The tester raises it with the UAT lead, who adds a row to `defect-tracker.csv`
(`Found in` = the run). The triage board sets the severity under §7. A fix arrives as a PR on `dev` and is marked
`Fixed - awaiting retest` with `PR #n`. It is closed only by a UAT run that passes the retest and references the defect, on a
build containing the fix (UD-1). Critical and High are never deferred. Medium and Low may be, with AHDA's acceptance reference.

**Every PR.** `python3 docs/testing/uat/uat-catalogue.py --check` (repo-checks) holds the files together (rules UR-1 to UT-1
in the script's docstring), so a malformed row, a dangling defect reference or a premature signature fails CI.

## 7. Severity policy and exit criteria (proposed, for AHDA's approval: 18-G-05)

Step 18 leaves "defect severity policy, exit thresholds, waiver authority and sign-off responsibilities" to AHDA's acceptance
authority, "no values invented" (18-G-05). The following is the delivery team's proposal. It governs this record's readiness
defects until AHDA approves it or replaces it.

| Severity | Definition |
| --- | --- |
| S-1 Critical | (a) unauthorised disclosure or change of data, data loss or corruption, or a governed gate (approval, lifecycle, separation of duties) bypassed; or (b) a cross-domain journey or the ADR-013 entity Project Manager journey cannot be completed, with no workaround a business user may use |
| S-2 High | a scenario's expected outcome is not met, or a capability is unavailable to its source actor, with no acceptable workaround |
| S-3 Medium | the outcome is met with a workaround, or degraded: slower than the agreed budget, hard to read, misleading wording that does not change a decision |
| S-4 Low | cosmetic or text, no effect on the outcome |

Exit criteria (`--gate`, G-1 to G-4):

1. Every case in scope has a recorded UAT outcome: PASS, FAIL or NOT APPLICABLE (not BLOCKED).
2. A FAIL rests only on Medium or Low defects AHDA deferred. A NOT APPLICABLE names the acceptance reviewer who agreed it.
3. No Critical or High defect remains Open or Fixed - awaiting retest. Each is closed by a passed retest, or rejected with a
   reason.
4. The AHDA Business Sponsor and the QA Lead have signed `uat-sign-off.md`, with name, date and the signed copy's reference.

Waivers: only AHDA's Business Sponsor defers a Medium or Low defect or accepts a NOT APPLICABLE. Nobody waives a Critical or
High defect: it is fixed, or the case is withdrawn from scope by a dated decision recorded here.

## 8. Readiness review: what stops UAT today

Before any tester runs a case, QA reviewed every open finding of the module, UI, testing and security records for what a
business tester would meet. Eighteen became UAT defects. Each names its source finding and the cases it is expected to
block.

| Severity | Defects | What they mean for UAT |
| --- | --- | --- |
| Critical | UAT-D-002, UAT-D-003 | **Entry blockers.** The shipped permission matrix gives AHDA's people none of their business permissions, and AHDA's configuration and master data are unpublished: no draft can even be created. Every case is stopped until both close |
| Critical | UAT-D-001, -004, -005, -006, -007, -008, -009 | WF-02 cannot publish progress. Entity forms are unusable. There is no FG-04 administration UI and no malware scanner. Restricted figures are unmasked for entities. There is no MFA provider (no administrator session) and no external sign-in |
| High | UAT-D-010, -011, -012 | Failed WF-08 application looks pending; WF-15 is silent for every module but WF-07 and Risk; no delivery operator screen |
| Medium, Low | UAT-D-013 to UAT-D-018 | Logs uncorrelated, ids shown instead of names, registers over the proposed latency budget, report cells without language, UTC "today", smaller observations |

Six Critical defects (UAT-D-002, -003, -005, -006, -008, -009) are AHDA's decisions or supplies, not code. The matrix rows
are Appendix A's. The configuration values are OQ-005, OQ-006, OQ-013 and OQ-014. The scanner, the MFA provider and the
external sign-in method are provider choices (PTBC-027, SR-41, ADR-007). The FG-04 UI has no task. The release plan cannot
reach UAT until those owners act. Security findings (`../../security/findings-log.md`) are not copied here: their own gate
covers them. SR-26 is the exception, as UAT-D-007, because a tester would see its effect.

## 9. Status

The status table is the output of `uat-catalogue.py --status`, kept here by UT-1. "At risk" counts the cases an open,
domain-specific defect is expected to block. Entry blockers stop every case and are listed below the table.

<!-- uat-status:begin -->
| Domain | In scope | Cycle 1 | Executed | PASS | FAIL | BLOCKED | N/A | Not run | At risk |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| WF-01 | 114 | 114 | 0 | 0 | 0 | 0 | 0 | 114 | 20 |
| WF-02 | 162 | 162 | 0 | 0 | 0 | 0 | 0 | 162 | 162 |
| WF-03 | 162 | 162 | 0 | 0 | 0 | 0 | 0 | 162 | 27 |
| WF-04 | 168 | 0 | 0 | 0 | 0 | 0 | 0 | 168 | 25 |
| WF-05 | 162 | 162 | 0 | 0 | 0 | 0 | 0 | 162 | 38 |
| WF-06 | 176 | 0 | 0 | 0 | 0 | 0 | 0 | 176 | 26 |
| WF-07 | 184 | 0 | 0 | 0 | 0 | 0 | 0 | 184 | 28 |
| WF-08 | 198 | 198 | 0 | 0 | 0 | 0 | 0 | 198 | 48 |
| WF-09 | 188 | 0 | 0 | 0 | 0 | 0 | 0 | 188 | 20 |
| WF-10 | 195 | 0 | 0 | 0 | 0 | 0 | 0 | 195 | 20 |
| WF-11 | 200 | 200 | 0 | 0 | 0 | 0 | 0 | 200 | 24 |
| WF-12 | 210 | 210 | 0 | 0 | 0 | 0 | 0 | 210 | 210 |
| WF-13 | 210 | 210 | 0 | 0 | 0 | 0 | 0 | 210 | 50 |
| WF-14 | 210 | 0 | 0 | 0 | 0 | 0 | 0 | 210 | 35 |
| WF-15 | 200 | 200 | 0 | 0 | 0 | 0 | 0 | 200 | 85 |
| FG-01 | 233 | 233 | 0 | 0 | 0 | 0 | 0 | 233 | 54 |
| FG-02 | 243 | 243 | 0 | 0 | 0 | 0 | 0 | 243 | 58 |
| FG-03 | 212 | 212 | 0 | 0 | 0 | 0 | 0 | 212 | 56 |
| FG-04 | 222 | 222 | 0 | 0 | 0 | 0 | 0 | 222 | 222 |
| FG-05 | 210 | 210 | 0 | 0 | 0 | 0 | 0 | 210 | 30 |
| FG-06 | 220 | 220 | 0 | 0 | 0 | 0 | 0 | 220 | 34 |
| ADR-013 | 16 | 16 | 0 | 0 | 0 | 0 | 0 | 16 | 16 |
| **Total** | **4,095** | **2,974** | **0** | **0** | **0** | **0** | **0** | **4,095** | **1,288** |

| Open defects | Critical | High | Medium | Low |
| --- | --- | --- | --- | --- |
| Open or awaiting retest | 9 | 3 | 5 | 1 |

Entry blockers (stop every case; not counted as at risk): UAT-D-002, UAT-D-003.
<!-- uat-status:end -->

## 10. Rehearsal (cycle 0)

Run 2026-10-19 by delivery QA on the e2e stack built from `dev` at `72c9798`, after staging the data set on a fresh database.
It was run twice. The second run was on a fresh stack after the staging spec's final lint and format changes, and both runs
gave the same result. Logged as RH-0001 to RH-0020 in `execution-log.csv`. These are rehearsals: they prove the
environment, the data set and the automated evidence, and set no UAT outcome.

| Run | What ran | Result |
| --- | --- | --- |
| Staging | `uat/run.sh`: the 11 fixtures of §5 | 11 of 11 staged in their stated state (FX-04 to FX-11 as PRJ-000001 to PRJ-000007); FX-05's documents CLEAN, QUARANTINED and SCAN_PENDING as named; 1.1 min |
| RH-0001 to RH-0015 | the 15 journey specs, on the staged stack | 14 PASS. UJ-002 FAIL at the progress update (422 `PROGRESS_ROLLUP_UNAVAILABLE`), as its `test.fail` mark expects: UAT-D-001 |
| RH-0016 | `adr-013-cross-entity-access-denial.spec.ts` → UAT-A13-15 | PASS |
| RH-0017 to RH-0020 | `adr-013-entity-project-manager.spec.ts` → UAT-A13-03 to 06, the stages it drives in the browser | PASS. Not logged against A13-02 or A13-07: the spec makes the draft, the risk and the issue through the API (UAT-D-004) |

The rehearsal shows the staged environment carries every journey the delivery team can automate, alongside the UAT data.
Nothing in it relieves an entry blocker. The stack's test profiles and stand-ins hide UAT-D-002, -003, -006, -008 and -009,
and that is exactly why the rehearsal is not UAT.

## 11. Findings

| # | Finding | Disposition |
| --- | --- | --- |
| F-1 | **UAT was not executed.** There is no UAT environment (hosting confirmation, 18-G-01), no named AHDA tester (18-G-02) and no approved exit policy (18-G-05). Nine Critical defects stop it (§8). The acceptance criterion — every case in scope with a recorded outcome, Critical/High resolved and retested — is not met | **Open** — AHDA Business Sponsor (testers, exit policy); hosting owner (environment); the owners of §8 |
| F-2 | The criterion's "minimum subset" is the whole catalogue: every domain has a P0 build task (§2). Executing 4,079 scenarios by business users is a programme, not a task, and the plan does not size it | **Open** — PMO: confirm the scope, or narrow it by a recorded decision. The register, cycles and gate take any subset by editing `P0_TASKS` or the cycle rule |
| F-3 | `notification-runtime.md` F-4 says no module publishes a notification intent. WF-07's escalation (`ConcernEscalationService`) and Risk's review condition (`RiskConditionSource`) now do | **Open** — WF-15 owner: update the finding; UAT-D-011 records the gap that remains |
| F-4 | `authorization-engine.md` F-1 still says Appendix A is unavailable. `findings-log.md` P-2 found it in `Project Files.zip` | **Open** — as P-2; UAT-D-002 notes it |
| F-5 | The Release Checklist row "UAT Catalogue Executed & Signed Off" is **Not Started**, and stays so: the log holds no UAT run, the tracker holds nine open Critical and three High, the sign-off is unsigned | Recorded; `--gate` is the cross-check the workbook's validation note asks for |

## 12. Verification

Run on this branch, 2026-10-19.

| # | Check | Result |
| --- | --- | --- |
| 1 | `uat-catalogue.py --register` from `Project Files.zip` and the workbook | 4,079 scenarios written; `JOURNEY_PACKAGES` equals `UAT_Journeys`; every `P0_TASKS` entry is P0 and names its domain; no P0 domain task unmapped; 0 findings |
| 2 | Register counts | 2,958 cycle 1, 1,121 cycle 2 (WF-04 168, WF-06 176, WF-07 184, WF-09 188, WF-10 195, WF-14 210); 2,854 business-run, 1,225 QA-assisted, matching `../test-strategy.md` §6 |
| 3 | `uat-catalogue.py --check` | 0 findings |
| 4 | `uat-catalogue.py --gate` | exit 1: G-1 (4,095 of 4,095 cases without a UAT outcome), G-3 (9 Critical and 3 High open), G-4 (no signature) |
| 5 | `artifacts/task-088/check-scenarios.py`: each rule seeded into a copy of this directory | 16 of 16 as expected. 12 seeded violations each reported by their rule (UR-1 ×2, UR-2, UL-1 ×4, UD-1 ×3, US-1, UT-1). The committed state fails the gate on G-1. A fully accepted world (every case PASS, every defect closed by a fix and a passed retest, both signatures) passes it. Reopening a High fails G-3; an undeferred Medium FAIL fails G-2 |
| 6 | `src/frontend/e2e/uat/run.sh` on a fresh stack, twice | 11 passed each time |
| 7 | Rehearsal, `E2E_REUSE_STACK=1 src/frontend/e2e/stack/run.sh` on the staged stack, twice | 17 passed (16 PASS, UJ-002 expected failure), 4.5 and 4.8 min; `e2e-summary.py` exit 0 |
| 8 | Scan states of FX-05's documents, read in the stack's database | `site-survey-report.pdf` CLEAN, `contractor-submission-infected.pdf` QUARANTINED, `design-drawings-held.pdf` SCAN_PENDING |
| 9 | `tsc -p tsconfig.e2e.json`, `eslint e2e`, `prettier --check e2e` in the e2e runner (Node 24) | clean |
| 10 | Every other `repo-checks` script | exit 0 each |
| 11 | `actionlint` 1.7.7 on `ci-quality-gates.yml`; `shellcheck -S warning` on `uat/run.sh`; `gitleaks dir` on the new directories | clean |
| 12 | Every finding a defect cites, read in its record | present and open as cited; `notification-runtime.md` F-4 checked against the code (F-3) |

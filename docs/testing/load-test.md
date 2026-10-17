# Performance and Load Test

| Field | Value |
| --- | --- |
| Task | TASK-086 — Execute Performance & Load Testing Against Capacity Targets (P15 - Testing & QA, P1) |
| Depends on | TASK-026 — Define Indexing Strategy (`docs/architecture/indexing-strategy.md`, `register-query-baseline.csv`) |
| Record date | 2026-10-17 |
| Status | **RUN AND REPORTED on the e2e stack standing in for SIT. The register and dashboard endpoints meet the working budget at 50 concurrent users except the portfolio-scope PORTFOLIO dashboard; four register screens, the Home dashboard and the report screens do not. Every breach has a filed follow-up (§8).** SIT does not exist yet (F-5); the budget is a delivery-team proposal until AHDA confirms PTBC-043 (§2) |
| Branch | `test/task-086-performance-load-testing` |
| Directory | `docs/testing` |
| Deliverables | **Load-test report:** this record, with the recorded run in `load-test/results/2026-10-17-e2e-stack-acceptance.json`. **Optimisation backlog:** §8, issues #101–#106. The harness: `load-test/` (k6 script, latency budget, representative volume, stack overlay, runner, report and check), `.github/workflows/load-test.yml`, and the `load-test-report.py --check` step in `repo-checks` |
| Environment variables / secrets | None (workbook). The harness reads `LOAD_BASE_URL`, `LOAD_PASSWORD`, `LOAD_PROFILE` (§7); on the e2e stack these are local defaults |
| Sources | Workbook TASK-086 and TASK-026 rows, Release Checklist "Performance/Load Test Within Target", Open Question OQ-010; `ptbc-tbc-tracker.md` and `ptbc-themes.csv` (PTBC-043); the FG-01, FG-02, WF-13, WF-14, WF-15 and FG-06 specifications (each leaves response times "Technical TBC"); `test-strategy.md` §2, §4, §8; `e2e-suite.md` §3; `endpoint-hardening.md` D-6; `dashboards.md` F-8; `reports.md` F-9; `infrastructure-as-code.md` F-7; `database-provisioning.md` D-4 |

## 1. What was asked, and what this answers

The workbook asks for a load test against the brief's capacity assumption — 150 named users, 10–25 normally active, 50 at
peak — over the highest-traffic register, dashboard and report endpoints; a report of p95 response time for the register and
dashboard endpoints under 50 concurrent users; a filed optimisation task for every endpoint over an agreed threshold before
UAT; and a comparison with the TASK-026 indexing baseline. Its validation cell adds: run against SIT at the target
concurrency.

| Asked | Answered in |
| --- | --- |
| Capacity: 150 named, 25 normal, 50 peak | §4.1: 150 people in the directory and the database; 25 then 50 signed-in people at once, each as themselves |
| Highest-traffic register, dashboard and report endpoints | §4.2: the ten screens the SPA's navigation leads to most, each sending exactly the requests the SPA sends for it |
| p95 under 50 concurrent users | §5: per endpoint, per data scope, at 25 and at 50 people; also per whole screen |
| Thresholds: PTBC-043, else the workbook's baseline | §2: PTBC-043 fixes the concurrency only; no source gives a latency, so a proposed budget, set before the first run |
| A follow-up task for every breach, before UAT | §8 and issues #101–#106; `load-test-report.py --check` fails `repo-checks` if a breach has no backlog row |
| Compared with the indexing baseline | §6 |
| Run against SIT | Not possible: SIT does not exist (F-5). Run on the e2e stack, the SIT stand-in TASK-085 uses, at the TASK-026 volume (§3) |

## 2. Thresholds

**PTBC-043 fixes the capacity, not a latency.** The tracker records it as a delivery-team working baseline ("150 named
users, 25 concurrent, 50 at peak … awaiting AHDA confirmation", OQ-010), not an approval. The workbook's Release Checklist
asks for "p95 latency within agreed thresholds" without a number, and every specification that mentions one says exact
response times are technical TBC (FG-01 §27, FG-02 §24, WF-13 §24, WF-14 §25, WF-15 §23, FG-06 TBC-AUD-27). There is
therefore no agreed threshold to test against.

**D-1. The run is judged against a proposed budget, written before the first measured run** (`load-test/budget.json`), so no
number was fitted to a result. It has the same status as PTBC-043's concurrency: a delivery-team proposal for AHDA to
confirm or replace. Every verdict in this record follows from that file; when AHDA sets its thresholds, replace the values
and run `load-test-report.py --judge load-test/results/<run>.json`, and `--check` names any new breach without a
backlog row.

| Group | p95 | Applies to | Basis |
| --- | ---: | --- | --- |
| shell | 500 ms | requests every page sends beside its content (unread badge, audit access) | must never be what the person waits for |
| register | 1,000 ms | a register page, filter, next page, record or lookup; **and a whole register screen** | 1 s keeps a person's flow of thought (Nielsen's response-time limits) |
| dashboard | 2,000 ms | a dashboard; the Home and project workspace screens | several widgets over the caller's scope, page frame already shown |
| report-run | 3,000 ms | an interactive report run; the Report run screen | first page of a report, inside the 10 s limit of attention |
| report-export | 1,000 ms | requesting an export (it queues a job) | answers like any command |
| report-job | 30,000 ms | queued to downloadable file | asynchronous, progress shown |
| errors | ≤ 1% | every endpoint, excluding the permission matrix's 403s (§4.3) | |

The acceptance criterion names endpoints. This record also judges **whole screens** — first request to last, as the person
waits — because three registers answer each request quickly and still take tens of seconds to show (F-1).

## 3. Where it ran

| | |
| --- | --- |
| Environment | The e2e stack (`src/frontend/e2e/stack/compose.yml`) with the load overlay (`load-test/compose.yml`): the API image built from this branch, migrating a fresh PostgreSQL 17; the platform seed, the e2e data set and the representative volume (§4.4) loaded before the API starts; the API's own workers running. The k6 runner calls the API directly on the stack's network |
| Machine | Apple M4, Docker Desktop VM with 10 CPUs and 8 GB. The API and the database had no CPU limit. Another project's containers (five `studafy-*`) and the idle local AHDA stack ran beside it throughout |
| Run | 2026-10-17 13:55–14:11 UTC, run `acceptance-20261017T135540Z`, k6 1.3.0 |

How it differs from SIT, each difference a stand-in like TASK-085's (`e2e-suite.md` §3):

| # | Here | SIT |
| --- | --- | --- |
| S-1 | One API container using up to 4.4 cores (median 0.4) | Cloud Run, 1 vCPU and 2 GiB per instance, 1 to 4 instances (`environments/sit/terraform.tfvars`) |
| S-2 | PostgreSQL 17 in a container, default configuration (`shared_buffers` 128 MB, `max_connections` 100) | Cloud SQL `db-custom-2-7680` |
| S-3 | No network between runner, API and database | the load balancer, the VPC connector and the private IP |
| S-4 | The workers poll every 2–30 s (S-7 of TASK-085), adding background queries | the environment's own intervals |
| S-5 | The 140 load people in the test directory (`ldap.Dockerfile`, `LOAD_PEOPLE=1`) | synthetic load accounts in AHDA's directory, credentials from the secret store |

The figures are therefore evidence of **where the platform's time goes at the target volume and concurrency**, not SIT's
service levels. A request that is slow here because it reads too much (P-1 to P-3, P-5) is slow on SIT for the same reason;
the absolute numbers will differ.

## 4. The workload

### 4.1 People

150 named people: the 10 of the e2e data set and 140 load people, `load.u001`–`load.u140` (`volume/10-organisation.sql`;
the directory image generates the same numbering). Each virtual user signs in as one person, holds their session for the run
(refreshing it as the SPA does) and never shares it.

| Group | Named | Active at peak | Of the first 25 | Profile and scope | Reaches |
| --- | ---: | ---: | ---: | --- | --- |
| Portfolio Manager | 10 | 5 | 3 | E2E-R02, ALL | 3,000 projects |
| Department Manager | 26 | 10 | 5 | E2E-R03, DEPT | 230–231 projects |
| Project Manager (internal) | 75 | 20 | 10 | E2E-R04, OWN | 20 projects each |
| Entity Project Manager | 20 | 7 | 3 | shipped R04 + R08, ENTITY | 75 projects of their entity |
| Executive | 5 | 4 | 2 | shipped R07 | dashboards, reports |
| Viewer | 4 | 4 | 2 | shipped R06 | dashboards |

The profiles are the e2e data set's (`e2e-suite.md` F-2): the shipped matrix grants nobody AHDA's review rights and gives
`PROJECT_VIEW` to R04 and R08 only. **A-1:** the role mix of the named and the active people is an assumption; the brief
gives only the totals.

### 4.2 Screens

Each screen sends the requests the SPA sends for it, read from the SPA's code (`src/frontend/src`), side by side where the
SPA sends them side by side, at most six at a time as a browser does:

| Screen | Requests |
| --- | --- |
| Home dashboard | `GET /dashboards` (catalogue), then the default landing: `GET /dashboards/{code}` (PORTFOLIO, GOVERNANCE) or, for entity users, the project list; `GET /departments` |
| Project Register / My Projects | `GET /projects` (page 1 or 2, 25 rows; a status filter one time in four) or `?projectManagerUserId=<me>`; master data (4 catalogues), departments, external entities, the governance profile; one `GET /users/{id}` per manager shown |
| Project workspace | `GET /projects/{id}`, `GET /audit-access`, the same lookups, `GET /dashboards/PROJECT?projectId=` (DSH-009), the manager's name |
| Risk Register, Issue Register, Change Requests | **every open project the caller reaches** (`GET /projects?status=…&pageSize=200`, page after page), **then one request per project** (`/risks?projectId=`, `/management-concerns?projectId=&concernType=ISSUE`, `/change-requests?projectId=`), then their lookups |
| Approval Inbox | `GET /approval-tasks` (25), `GET /roles`, requesters' names |
| Notifications | `GET /notification-preferences`, `GET /notifications` (25) |
| Document Library | `GET /documents` (25), 3 master-data catalogues, owners' names |
| Report run | `GET /reports`, `GET /report-jobs` (5), `GET /reports/{code}` for a report of the catalogue, `POST /reports/{code}/run` (25 rows, default columns, a required parameter chosen from its options). One run in four is followed by an export: `POST …/export`, `GET /report-jobs/{id}` every 2 s, `GET …/content` |
| Shell | `GET /audit-access` once per visit; `GET /notifications/unread-count` at once and every 30 s, during pauses too |

| Group | Home | Projects | Workspace | Risks | Issues | Changes | Approvals | Documents | Notifications | Reports |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Portfolio, Department | 25 | 15 | 15 | 8 | 5 | 4 | 10 | 5 | 5 | 8 |
| Project Manager | 15 | 20 (mine) | 25 | 10 | 8 | 4 | — | 8 | 5 | 5 |
| Entity Project Manager | 15 | 15 | 30 | 10 | 10 | — | — | 10 | 10 | — |
| Executive | 60 | — | — | — | — | — | — | — | 10 | 30 |
| Viewer | 85 | — | — | — | — | — | — | — | 15 | — |

**A-2:** how often each group opens each screen (out of 100) is an assumption. Executives and viewers open only what the
shipped matrix lets them read (F-4). **A-3:** one report run in four is exported. **A-4:** a person reads a screen for 5 to 15
seconds before the next.

### 4.3 Load profiles

| Profile | People | Pause | Duration | Judged |
| --- | --- | --- | --- | --- |
| `acceptance` | 25 for 4 min, then 50 for 8 min (30 s ramps, not judged) | 5–15 s | 13.75 min | yes: phases `normal` (25) and `peak` (50) |
| `saturation` | 50 | none | 5 min | no: headroom only (§5.4) |
| `smoke` | one per group, every screen once | — | ~1 min | no: every request must answer 2xx (or 403 where §4.2's lookups allow it) |

Latency is computed over answered (2xx) requests. A 403 from the permission matrix on a lookup the SPA survives (names,
roles, master data, departments, entities, configuration) is counted as **denied**, not as an error and not as a latency;
any other non-2xx is an error.

### 4.4 Data volume

The volume TASK-026 reviewed its query plans at (`indexing-strategy.md` §5.2), loaded into the real module tables
(`load-test/volume/*.sql`, 35 s; the database is 874 MB): 3,000 projects in 13 departments, half delivered by 20 of 40
entities; 45,000 risks with 90,000 assessments; 24,000 concerns and 2,400 escalations; 30,000 approvals with 60,000 tasks
(652 pending); 9,000 change requests and 600 suspension requests; 60,000 documents; 150,000 tasks and 24,000 milestones;
monthly progress, financial and KPI history for two years for the 2,718 activated projects (64,748 published snapshots of
each kind, 163,080 KPI measurements); 400,000 notification deliveries. Each file's header records its distributions and its
deviations from §5.2: only activated projects report (so 64,748 rather than 72,000 monthly rows), 652 rather than 600
pending tasks, and external participation, audit, delegation and report-history tables are not loaded. `ANALYZE` runs last.

## 5. Results

The recorded run, `acceptance-20261017T135540Z` (`load-test/results/2026-10-17-e2e-stack-acceptance.json`): 26,384
requests at 25 people and 122,624 at 50, **0 errors**; 374 and 2,099 denied by the permission matrix (F-4). Each cell below
is p95 in ms, **normal (25) / peak (50)**; bold is over budget. `--judge` prints p50, p99, maximum and counts.

### 5.1 Register and dashboard endpoints — the acceptance criterion

| Endpoint | Budget p95 ms | ALL | DEPT | OWN | ENTITY |
| --- | ---: | ---: | ---: | ---: | ---: |
| `GET /projects` | 1,000 | 7.5 / 15 | 16 / 94 | 4.6 / 2.7 | 28 / 77 |
| `GET /projects?projectManagerUserId` | 1,000 | — | — | 12 / 101 | — |
| `GET /projects?status` | 1,000 | 12 / 40 | 20 / 157 | 14 / 96 | 25 / 231 |
| `GET /projects/{id}` | 1,000 | 4.9 / 22 | 9.1 / 90 | 9.6 / 127 | 15 / 162 |
| `GET /risks?projectId` | 1,000 | — / 93 | 14 / 101 | 14 / 168 | 14 / 213 |
| `GET /management-concerns?projectId` | 1,000 | 4.6 / 58 | 47 / 76 | 12 / 109 | 10 / 129 |
| `GET /change-requests?projectId` | 1,000 | 4.9 / 88 | 40 / 103 | 9.7 / 84 | — |
| `GET /approval-tasks` | 1,000 | 146 / 95 | 41 / 566 | — | — |
| `GET /documents` | 1,000 | 14 / 163 | 42 / 346 | 64 / 339 | 27 / 346 |
| `GET /notifications` | 1,000 | 46 / 272 | 59 / 772 | 41 / 597 | 38 / 234 |
| `GET /notifications/unread-count` | 500 | 31 / 200 | 40 / 278 | 32 / 165 | 32 / 240 |
| `GET /master-data-items?catalogueId` | 1,000 | 5.3 / 31 | 6.0 / 75 | 6.9 / 80 | — |
| `GET /configuration-resolutions/{family}` | 1,000 | 6.2 / 82 | 9.6 / 256 | 12 / 281 | — / — |
| `GET /dashboards` | 1,000 | 9.3 / 101 | 11 / 71 | 12 / 130 | 128 / 130 |
| `GET /dashboards/{code}` | 2,000 | **2,204** / **3,678** | 188 / 1,473 | 43 / 689 | — |
| `GET /dashboards/{code}?projectId` | 2,000 | 28 / 423 | 15 / 288 | 32 / 906 | 77 / 935 |

**Every register endpoint is within budget at 50 people, in every scope; the largest p95 is 772 ms.** One dashboard call is
not: the PORTFOLIO dashboard for a portfolio-scope person, p95 3,678 ms at the peak (p99 39,004 ms) and 2,204 ms already at
25 (P-2). The same dashboard for a department is 1,473 ms, for a project manager 689 ms; the project dashboard is under
1 s in every scope.

### 5.2 Report endpoints

| Endpoint | Budget p95 ms | ALL | DEPT | OWN | ENTITY |
| --- | ---: | ---: | ---: | ---: | ---: |
| `GET /reports` | 1,000 | 30 / 434 | 32 / 563 | 171 / 762 | — |
| `GET /reports/{code}` | 1,000 | 10 / 629 | 133 / **1,014** | 136 / 579 | — |
| `POST /reports/{code}/run` | 3,000 | 7.8 / **3,526** | 127 / 1,181 | 361 / 1,158 | — |
| `POST /reports/{code}/export` | 1,000 | — / 18 | 22 / 571 | 47 / 447 | — |
| `GET /report-jobs/{id}` | 1,000 | — / 17 | 49 / 42 | 22 / 43 | — |
| `GET /report-jobs/{id}/content` | 1,000 | — / **3,403** | 573 / 637 | 224 / 401 | — |

Queued to file (31 exports): p95 6,321 ms at 25 people, 13,489 ms at 50 — within 30 s. With one portfolio person and no
other load, `GET /reports/PROGRESS_HISTORY` takes 2.9 s and returns 451 KB (all 2,718 projects inlined as parameter
options), and the KPI_PERFORMANCE, FINANCIAL_PERFORMANCE and SCHEDULE_DELIVERY runs take 1.4–2.3 s for 25 rows (P-3).

### 5.3 Whole screens

| Screen | Budget p95 ms | ALL | DEPT | OWN | ENTITY |
| --- | ---: | ---: | ---: | ---: | ---: |
| Home dashboard | 2,000 | **2,214** / **3,636** | 205 / 1,768 | 64 / 955 | 263 / 519 |
| Project Register | 1,000 | 120 / 846 | 54 / **1,193** | — | 186 / 823 |
| My Projects | 1,000 | — | — | 42 / 524 | — |
| Project workspace | 2,000 | 54 / 970 | 46 / 919 | 71 / 1,570 | 285 / 1,918 |
| Risk Register | 1,000 | — / **51,094** | 851 / **6,269** | 86 / **1,253** | 324 / **4,388** |
| Issue Register | 1,000 | **1,980** / **46,731** | **1,411** / **3,012** | 57 / 740 | 185 / **3,174** |
| Change Requests | 1,000 | **2,130** / **40,603** | **1,291** / **6,348** | 38 / 602 | — |
| Approval Inbox | 1,000 | 198 / 213 | 197 / 992 | — | — |
| Document Library | 1,000 | 74 / 680 | 66 / **1,077** | 80 / 607 | 37 / 789 |
| Notifications | 1,000 | 48 / 294 | 61 / 775 | 42 / 657 | 40 / 263 |
| Report run | 3,000 | 46 / **3,943** | 241 / **3,140** | 681 / **3,038** | — |

The three cross-project registers send one request per project (F-1): about 2,860 requests for a portfolio person, 220 for
a department, 73 for an entity, 20 for a project manager. Each request is fast; the screen is not, and its bursts slow
everything sharing the API and the database at that moment, which is where the Document Library and Project Register tails
come from (P-4). With one portfolio person and no other load the Risk Register already takes 2.6 s.

The first acceptance run (13:35 UTC, `acceptance-20261017T133550Z`, not recorded because two report requests answered 422 on
a harness fault since fixed, F-8) breached the same endpoints and screens in the same way, with lower fan-out peaks (Risk
Register ALL p95 19,440 ms): how often several portfolio people open a cross-project register at the same moment varies from
run to run, and that coincidence sets those screens' tails.

### 5.4 Saturation (headroom, not judged)

50 people with no pause (`saturation-20261017T141212Z`, 5 min): about 450 requests a second; the API used a median 3.3 cores
and PostgreSQL 3.7. **The API exhausted the database's 100 connections** ("sorry, too many clients already"): ten dashboard
requests timed out at 60 s, and after the run the pool still held the connections, so even `psql` could not connect until
they were released (F-6, #106). Every screen breached its budget; that is expected without pauses, and none of it is
acceptance evidence.

## 6. Against the TASK-026 baseline

TASK-026 measured each register screen's default query in PostgreSQL alone — median of three local runs on the same machine,
same volume, warm cache (`register-query-baseline.csv`). Its record calls those times "the regression baseline, not a
service-level figure". Here the same screens are measured through the API at 50 people. "SQL floor" is the page plus its count
(R-29) as TASK-026 measured them:

| Request | Scope | Baseline queries | SQL floor ms | API p50 ms | API p95 ms |
| --- | --- | --- | ---: | ---: | ---: |
| `GET /projects` | ALL | SCR-025 ALL page + count | 0.245 | 8.2 | 15 |
| `GET /projects` | DEPT | SCR-025 DEPT page + count | 0.071 | 6.4 | 94 |
| `GET /projects` | ENTITY | SCR-025 ENTITY page + count | 0.070 | 5.7 | 77 |
| `GET /projects?projectManagerUserId` | OWN | SCR-026 OWN page + count | 0.082 | 7.6 | 101 |
| `GET /risks?projectId` | ALL | SCR-080 project tab page | 0.106 | 9.5 | 93 |
| `GET /change-requests?projectId` | ALL | SCR-105 project page | 0.038 | 5.5 | 88 |
| `GET /approval-tasks` | DEPT | SCR-100 personal page + count + role queue | 1.253 | 27 | 566 |
| `GET /documents` | ALL | SCR-120 ALL page + count | 4.470 | 16 | 163 |
| `GET /notifications` | DEPT | SCR-150 first page | 0.105 | 53 | 772 |
| `GET /notifications/unread-count` | DEPT | SCR-150 unread badge | 0.037 | 19 | 278 |

`load-test-report.py` prints every mapped row. What the comparison shows:

1. **Where the API runs the baseline's query, the database is not the cost.** The register reads stay on their indexes: in
   `pg_stat_statements` the project page and count statements average 0.02–2.3 ms, the per-project risk, concern and change
   request pages 0.05–0.20 ms and their counts 0.02–0.04 ms. A register request's median, 5–10 ms, is mostly the API's own
   work around the query: authentication, the caller's grants (153,557 reads of `access_relationship`, one per request, mean
   0.19 ms), the scope checks and serialisation. Its p95 at the peak is set by contention with the fan-out bursts (P-1) and the
   whole-portfolio reads (P-2, P-3), not by its own query.
2. **Three queries the baseline measured are not the queries the API runs**, and each is among the costliest of the run:
   - the **unread badge** counts by `status = 'SENT'`, which I-47 `(recipient_user_id, channel, read_at)` cannot narrow:
     1,905 heap blocks and 29 ms per call cold, against the baseline's index-only `read_at IS NULL` at 0.037 ms (P-5);
   - the **latest published financial snapshot and KPI measurement** for FG-01 and FG-02 are found by loading every published
     row of every project in scope and keeping the latest in memory (`FinancialKpiRepository.ListLatestSnapshotsAsync` and
     its neighbours): KPI measurements mean 41 ms (max 1,705 ms); the baseline measured one index probe per project at
     0.026–0.029 ms (P-2);
   - the **cross-project registers** never send the scoped, paged register query the baseline measured (SCR-080 ALL page
     0.042 ms, DEPT page 2.243 ms); the SPA sends one project-tab query per project instead (F-1).
3. **One register count reads its table whole, in a scope the baseline did not measure.** The Document Library's count under
   DEPT and ENTITY scope (`document LEFT JOIN project … WHERE p.department_id = $1 AND d.data_classification_item_id = ANY
   ($2)`) is planned through the classification index; every document carries one of the classifications the caller may see,
   so it visits all 60,000 documents and hash-joins them to the scope's projects (13.9 ms for a department, 33.5 ms cold for an
   entity) instead of starting from the 231 or 75 projects through I-31. TASK-026 measured SCR-120's ALL count (4.3 ms) and
   SCR-121's project page, not this one. It grows with every document and adds to P-4's Document Library tail (#104). Every
   query TASK-026 did measure is still planned as recorded: `RegisterQueryPlanTests` holds them in CI.

## 7. Running it

```sh
docs/testing/load-test/run.sh                           # build from this tree, fresh database with the volume, acceptance, report
LOAD_PROFILE=smoke docs/testing/load-test/run.sh        # or saturation
LOAD_REUSE_STACK=1 docs/testing/load-test/run.sh        # against the load stack already up
python3 docs/testing/load-test/load-test-report.py --judge docs/testing/load-test/results/<run>.json
docker compose -f src/frontend/e2e/stack/compose.yml -f docs/testing/load-test/compose.yml down -v   # never --remove-orphans
```

A run writes to `artifacts/load-test/<profile>-<time>/`: every request (`requests.csv.gz`), k6's summary, the statements
PostgreSQL executed (`statements.csv`), the containers' CPU and memory every 10 s, `stats.json` and `report.md`. To record a
run, copy its `stats.json` into `load-test/results/`; `repo-checks` then holds every breach in it to §8.

CI (`load-test.yml`): a pull request that changes the harness or the e2e stack runs the smoke profile on a GitHub runner;
`workflow_dispatch` runs any profile there and attaches the results. A runner is not SIT either, so its figures are kept for
comparison and not recorded.

**On SIT**, once it exists (F-5): the DBA loads `volume/*.sql` after the e2e data set; the directory holds the 140 load
accounts; then from a host that reaches SIT:

```sh
docker run --rm -v "$PWD/docs/testing/load-test:/load:ro" -v "$PWD/artifacts/load-test/sit:/results" -w /load \
  -e LOAD_BASE_URL=https://<SIT host> -e LOAD_PASSWORD -e LOAD_PROFILE=acceptance \
  mirror.gcr.io/grafana/k6:1.3.0 run --out csv=/results/requests.csv.gz load-test.js
python3 docs/testing/load-test/load-test-report.py artifacts/load-test/sit
```

`LOAD_PASSWORD` comes from the secret store. Sign-in is throttled at 30 a minute per client address (`endpoint-hardening.md`
D-6), so the 50 sign-ins take about two minutes before the load starts; behind SIT's load balancer they count against the
runner's address. Enable `pg_stat_statements` on the instance for the run, as TASK-026 F-6 asks.

## 8. Optimisation backlog

Every endpoint or screen of the recorded run over its budget, with the follow-up filed for it. `load-test-report.py --check`
reads this table: a breach of a recorded run whose endpoint has no row with an issue and a status fails `repo-checks`, and so
does a row for an endpoint no run measured. P-5 is within budget and recorded for its drift from the baseline (§6).

| # | Endpoint or screen | Breach (recorded run, worst scope) | Issue | Status |
| --- | --- | --- | --- | --- |
| P-1 | `screen Risk Register` | peak: ALL 51,094 ms, DEPT 6,269, ENTITY 4,388, OWN 1,253 | #101 | Open — before UAT |
| P-1 | `screen Issue Register` | normal ALL 1,980 ms, DEPT 1,411; peak ALL 46,731, ENTITY 3,174, DEPT 3,012 | #101 | Open — before UAT |
| P-1 | `screen Change Requests` | normal ALL 2,130 ms, DEPT 1,291; peak ALL 40,603, DEPT 6,348 | #101 | Open — before UAT |
| P-2 | `GET /dashboards/{code}` | ALL: normal 2,204 ms, peak 3,678 (p99 39,004) | #102 | Open — before UAT |
| P-2 | `screen Home dashboard` | ALL: normal 2,214 ms, peak 3,636 | #102 | Open — before UAT |
| P-3 | `POST /reports/{code}/run` | peak ALL 3,526 ms (p99 11,846) | #103 | Open — before UAT |
| P-3 | `GET /reports/{code}` | peak DEPT 1,014 ms (p99 5,568) | #103 | Open — before UAT |
| P-3 | `GET /report-jobs/{id}/content` | peak ALL 3,403 ms | #103 | Open — before UAT |
| P-3 | `screen Report run` | peak ALL 3,943 ms, DEPT 3,140, OWN 3,038 | #103 | Open — before UAT |
| P-4 | `screen Document Library` | peak DEPT 1,077 ms; its requests ≤ 346 ms; its scoped count reads every document (§6) | #104 | Open — fix the count, re-measure after P-1 to P-3 |
| P-4 | `screen Project Register` | peak DEPT 1,193 ms; its requests ≤ 256 ms | #104 | Open — re-measure after P-1 to P-3 |
| P-5 | `GET /notifications/unread-count` | within (peak p95 278 ms); query bypasses I-47 | #105 | Open — not a breach |

A row closes when a recorded acceptance run judges its endpoint within budget, or when AHDA's confirmed threshold does.

## 9. Findings

| # | Finding | Disposition |
| --- | --- | --- |
| F-1 | **The Risk, Issue/Challenge and Change Request registers fan out one request per project.** The SPA reads every open project in scope and then one register request per project, six at a time, filtering and paging in the browser. Each request passes its budget; the screens take up to 51 s for a portfolio person at the peak and 2.6 s with nobody else on the platform. The API already declares the scoped, paged form (`Risk_ListRisks`), and its indexes exist (I-11 to I-13, I-16 to I-18, I-26 to I-28). | **Open** — P-1, #101 |
| F-2 | **FG-01 and FG-02 read whole histories to find the latest row.** Financial snapshots, financial updates and KPI measurements are loaded for every project in scope and reduced in memory; WF-14 visibility is still decided project by project (`dashboards.md` F-8, `reports.md` F-9). This is the PORTFOLIO dashboard's and the portfolio reports' time. | **Open** — P-2 #102, P-3 #103 |
| F-3 | Report definitions inline every option of a project parameter: 451 KB and 2.9 s for PROGRESS_HISTORY at 2,718 projects. | **Open** — P-3, #103 |
| F-4 | **The shipped matrix limits what executives and viewers can open.** R07 and R06 read no project (only R04 and R08 hold `PROJECT_VIEW`) and R06 no document, so their load is dashboards, reports and notifications only; R02 (E2E-R02) and the entity users are refused the user, role and some lookup reads the SPA sends (2,473 denials in the run, all on §4.2's lookups). Same root as `e2e-suite.md` F-2 and F-7. With AHDA's matrix these people will open the registers too, adding load this run does not have. | **Open** — matrix, AHDA PMO; re-run when it lands |
| F-5 | SIT does not exist; the run is on the e2e stack (§3). The workbook's validation ("run against SIT at the target concurrency") is owed there. | **Open** — DevOps, after SIT is provisioned (§7) |
| F-6 | **One API instance can exhaust the database's connections.** At saturation the default Npgsql pool (100) filled PostgreSQL's 100 connections. SIT and UAT allow 4 instances, PROD 10. This is `infrastructure-as-code.md` F-7 / `database-provisioning.md` D-4, now with evidence. The acceptance run also executed `DISCARD ALL` 1,803,598 times for about 149,000 requests: about 12 pool checkouts per request. | **Open** — #106; Backend Lead and TASK-019/020 owners |
| F-7 | Two API queries miss the index TASK-026 gave their screen: the unread badge's count bypasses I-47 (P-5), and the Document Library's DEPT/ENTITY count reads every document instead of starting from the scope's projects (§6, P-4). | **Open** — #105, #104 |
| F-8 | Harness faults found and fixed while building it, each before the recorded run: a report's required project was taken from any project the person had seen, so PROGRESS_HISTORY was run for a draft and answered 422 (the SPA offers only the parameter's options); the export's wait was timed inside the Report run screen; a tag containing `&` was split by k6's CSV output; the smoke threshold passed with zero checks. | **Closed** |
| F-9 | The figures vary between runs where they depend on coincidence: the fan-out screens' tails depend on how many portfolio people open them at once (§5.3). The verdicts did not change between the two acceptance runs; the magnitudes did. | Recorded |

## 10. Acceptance criteria and validation

| # | Criterion | Result |
| --- | --- | --- |
| 1 | A load-test report exists showing p95 response time for register/dashboard endpoints under 50 concurrent users | **MET** on the e2e stack (§5.1): every register endpoint within the proposed budget in every scope; one dashboard endpoint (PORTFOLIO, ALL scope) over it. SIT owed (F-5) |
| 2 | Any endpoint breaching an agreed threshold has a follow-up optimisation task filed before UAT | **MET** against the proposed budget: 11 endpoints and screens in breach, each in §8 with issue #101–#104; `--check` keeps it so in `repo-checks`. No threshold is agreed yet (§2): when AHDA confirms PTBC-043, re-judge (D-1) |
| 3 | Results are compared against the indexing-strategy baseline | **MET** (§6): the measured register queries are still on their indexes; four API queries differ from what the baseline measured (P-2, P-4's document count, P-5, F-1) |
| — | Validation: run against SIT at the target concurrency; p95 recorded and compared with the baseline; a tracked follow-up for every endpoint over threshold | **Partly**: run at the target concurrency and volume on the SIT stand-in; recorded and compared; follow-ups filed. SIT itself owed (F-5) |

## 11. Verification

Run on this branch, 2026-10-17.

| # | Check | Result |
| --- | --- | --- |
| 1 | Volume chain on a scratch database: migrate, seed, e2e data set, `volume/*.sql`, `validate-data-integrity.sql` | applies in 47 s; "no violation in 313 foreign keys, 418 check constraints, 525 indexes, 122 audited tables, 37 master data references"; a second application of `volume/` changes nothing |
| 2 | The same through the load stack's seed (one transaction) | the stack answers in 48–51 s from `up` |
| 3 | `ldap.Dockerfile` with `LOAD_PEOPLE=1` | 140 `load.u*` entries, subjects matching `10-organisation.sql`; with the default the image is the e2e one |
| 4 | Smoke profile | every group opens every screen; 9,784 checks pass, k6 exit 0. Seeded faults turn it red: `GET /approval-tasks` sent to a path answering 404, k6 exit 99 (the `checks` threshold); a wrong password, setup fails at the first sign-in, exit 107 |
| 5 | Acceptance profile, twice on a fresh stack | §5; the second is recorded |
| 6 | Saturation profile | §5.4 |
| 7 | `load-test-report.py --check`; `--judge` of the recorded run | 0 findings, exit 0. With the `GET /dashboards/{code}` row removed: 2 LT-1 findings (normal and peak), exit 1. With a row for an endpoint no run measured: LT-2, exit 1. `--judge` exit 0 |
| 8 | Report probe for each of the 10 reports as a portfolio person, one at a time | §5.2's single-user figures |
| 9 | `EXPLAIN (ANALYZE, BUFFERS)` of the unread count, as the API sends it and with `read_at IS NULL` | 1,905 heap blocks 29.1 ms; index-only 4 buffers 0.037 ms |
| 10 | `EXPLAIN (ANALYZE, BUFFERS)` of the Document Library's DEPT and ENTITY counts | 60,000 documents read through `ix_document_data_classification_item_id`, hash-joined to 231 / 75 projects: 13.9 ms / 33.5 ms |

# Operational Dashboards & Alerting Thresholds (FG-05 Alerts)

| Field | Value |
| --- | --- |
| Task | TASK-092 — Build Operational Dashboards & Alerting Thresholds (FG-05 Alerts) (P16 - Observability) |
| Depends on | TASK-075 (`integration-monitoring.md`): the FG-05 registry, invocations, retries, dead letters and Operational Alerts this task measures and routes; its F-1 (alerts reach no one) and F-3 (no threshold set) are this task's. TASK-090 (`apm-error-tracking.md`): per-route latency, throughput and errors in the APM. Also TASK-091 (`health-checks-uptime-monitoring.md`): the on-call channel that holds `ALERT_CHANNEL_WEBHOOK_URL` and the monitoring module this task extends. TASK-039 (`notification-runtime.md`): WF-15, which delivers the alerts. TASK-076 (`integration-admin-ui.md`): ADM-047 |
| Record date | 2026-10-21 |
| Status | **BUILT AND VERIFIED LOCALLY.** The backend, ADM-047 and the static check are tested in CI; the forced breach was run end to end on the local DEV stack with the release image, a real browser and a real SMTP relay (§6.3). The Cloud Monitoring dashboard, log-based metric and on-call policy are codified and validated, but no environment exists to apply them to (F-1), and every threshold value is AHDA's and open (F-2) |
| Branch | `infra/task-092-operational-dashboards-alerting` |
| Directory | `docs/operations` (this record, `alert-thresholds.json`, `dashboards/`, `operational-dashboards-check.py`) |
| Deliverables | **Operational dashboard definitions:** OD-1, ADM-047 Integration Monitoring as the dashboard of every registered integration — availability, freshness, queue depth, retries and dead letters, live (§4.1); OD-2, the platform service dashboard in each environment's Cloud Monitoring — API traffic, latency, error rate, readiness, database, operational alerts raised (`dashboards/platform-service.json.tftpl`, §4.2). **Alert threshold configuration:** the register `alert-thresholds.json` (AT-1 to AT-7), the settings the API reads (`IntegrationMonitoring:Policy`), each environment's `integration_alert_thresholds` in Terraform, and the route of each alert to its configured recipients (WF-15) and to the on-call channel (§5) |
| Environment variables / secrets | `ALERT_CHANNEL_WEBHOOK_URL` (Secret, SIT/UAT/PROD): unchanged, read only by `route-alerts.sh` (TASK-091). This task sends a second policy to the channel it holds (D-8); nothing new reads the secret. New settings: `IntegrationMonitoring:Policy:AvailabilityAlertDelays`, `:QueueBacklogThresholds`, `:ReconciliationMismatchTolerances` (none set by default). New Terraform input: `integration_alert_thresholds` (per environment, committed, empty) |
| Gate decision applied | None in the workbook row. **PTBC-037 is open**: every threshold value is documented as an Open Question and none is set (D-3, F-2). PTBC-046's subject is not recoverable (`ptbc-tbc-tracker.md` 9.5); no threshold is attributed to it |
| Control | CTL-32 (this task); CTL-52's verification, "every registered integration visible with live health on the operational dashboard" |
| Sources read | The TASK-092 row, read from the workbook (`AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`) on 2026-10-21: description, acceptance criteria, directory, deliverables, branch, variables and the validation check. The **FG-05 Integration Monitoring Functional Specification v1.0** (`Project Files.zip`): §5 (health dimensions), §6.6 (queue snapshots), §9.2–9.3 (deduplication, recovery), §10 (alert families, lifecycle, the WF-15 boundary), §13 (ADM-047), the event catalogue EVT-INT-026–043 and TBC-INT-08 to -17. `docs/governance/ptbc-themes.csv` rows PTBC-037, PTBC-046; `cybersecurity-control-matrix.md` CTL-32, CTL-52; `integration-monitoring.md`; `integration-admin-ui.md`; `apm-error-tracking.md`; `health-checks-uptime-monitoring.md`; `notification-runtime.md`; `event-conventions.md` §3, EV-10, R-45 |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | Dashboards over FG-05 integration health, queue depth, retry and dead-letter counts, and API latency and error rate | This task |
| **In** | Alert thresholds for availability, freshness, queue backlog and reconciliation mismatch, configurable per integration and environment, with no value invented | This task |
| **In** | Routing each Operational Alert to its configured recipients, and to the on-call channel | This task |
| **Out** | The threshold values themselves | AHDA IT / DevOps Lead (PTBC-037, F-2) |
| **Out** | Who receives the alerts and the escalation route beyond the on-call channel (TBC-INT-17), on-call rotation and response targets | AHDA; TASK-093 |
| **Out** | Choosing and provisioning the APM, and its own dashboards | DevOps/Platform Lead (TASK-090 F-2, F-8 here) |
| **Out** | Queue state of the outbox and of WF-15's deliveries not yet attempted | F-3 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Two dashboards, each where its data is.** OD-1 is ADM-047 Integration Monitoring in the application: the FG-05 registry, its health, its queue and its dead letters live in `integration_monitoring`, which only the application reads, behind FG-05's permissions. OD-2 is a Cloud Monitoring dashboard in each environment's project: the API's request rate, latency and error rate are Cloud Run's built-in metrics, measured at the platform with no application change, beside readiness (TASK-091), the database and the alerts raised. Per-route traces and exceptions stay in the APM (TASK-090) | FG-05 §13 names ADM-047 "the central health, failure, freshness, queue, dead-letter and reconciliation workspace", and "every registered integration" is the registry, so it cannot be a dashboard drawn up front in Terraform. Copying FG-05's state into a metrics service would add a pipeline, a second copy of operational data and, for a hosted one, a processor (CTL-50) |
| D-2 | **Queue depth is FG-05's own record of unfinished work.** Per instance: attempts IN_PROGRESS (in flight) plus operations whose latest attempt is RETRY_SCHEDULED (awaiting a retry), when the oldest of them was first attempted, and, apart, operations whose latest attempt is DEAD_LETTER. It covers every registered integration, the operations FG-05 runs and the ones other modules report. Read live by one query on a partial index, `ix_invocation_queue_state` (TASK-075 D-7 keeps those three states to live rows, so the index stays small as attempts accumulate) | FG-05 §6.6 asks for "depth, oldest item age, processing rate, retry count and dead-letter count without turning FG-05 into a raw broker administration console". A dead letter has left the queue and waits for an operator, so it is not counted as depth. Work a module has not attempted yet is not in FG-05's records (F-3) |
| D-3 | **A threshold for each condition, and no value.** Availability (AT-1, a delay), freshness (AT-2, built by TASK-075), queue backlog (AT-3 depth, AT-4 oldest age), reconciliation mismatch (AT-5, a tolerance), delivery rate (AT-6, TASK-075), dead letter (AT-7, none: every dead letter is an alert). Without a value FG-05 behaves as TASK-075 built it: down is alerted at once, any mismatch is alerted, freshness stays UNKNOWN, no backlog or delivery-rate alert. A policy that names a threshold without a usable bound stops the API at start-up | The workbook: thresholds "once PTBC-037/046 values are confirmed by AHDA (documented as Open Question thresholds until then)". FG-05 §5: "exact thresholds are Configuration/TBC and are never invented". The defaults fail loud, never quiet |
| D-4 | **One register, one place for values.** `alert-thresholds.json` names each threshold, its setting, unit, meaning, behaviour without a value and the open question it waits on. Values are written only in each environment's `terraform.tfvars` (`integration_alert_thresholds`), which the platform passes to the API as `IntegrationMonitoring__Policy__<setting>`; Terraform refuses a key that is no registered threshold, and `operational-dashboards-check.py` O-3 refuses a value in PROD for a threshold not CONFIRMED | A value written twice drifts. A reviewer diffs one file per environment, as with every other sizing value |
| D-5 | **An availability delay counts from when the integration went down, and alerts once per outage.** `integration_instance.availability_changed_at` (new column, migration `TASK-092_AddIntegrationQueueStateAndAlertThresholds`, back-filled from the outcome that set the current availability) dates every change. With a delay the attempt that takes the integration down raises nothing; the pass raises AVAILABILITY once it has been down that long, unless an AVAILABILITY alert was already raised since it went down, even one an operator has closed. Without a delay, the recorder raises at once as before | A delay that counted from the last failure would never be reached while calls keep failing; one that ignored closed alerts would raise a new alert every 30 s after an operator closed one during a long outage |
| D-6 | **QUEUE_BACKLOG** is a new alert type (FG-05 §10.1 "queue backlog/oldest-message age exceeds configured policy", EVT-INT-028), WARNING like every condition but availability (TBC-INT-17). Raised by the pass when the depth is greater than AT-3 or the oldest queued operation has waited longer than AT-4; one live per instance, as every alert type (TASK-075 D-9) | A backlog lasts; one alert per condition, not per pass. Strictly greater, so a depth threshold of 0 means "anything queued" |
| D-7 | **A reconciliation mismatch within its tolerance raises no alert, and is still recorded.** The run keeps every mismatch it found; AT-5 decides only the alert | TBC-INT-15 "tolerances by integration". The record is never thinned by a threshold |
| D-8 | **Each alert is routed through WF-15 to the configured recipients.** Raising an alert stages, in the same transaction, the NotificationIntent `IntegrationMonitoring.OperationalAlertRaised` (event catalogue row 7, sample `operational-alert-raised-intent.json`), keyed by the alert's id, family `INTEGRATION_OPERATIONAL_ALERT`, platform-wide scope (no project, department or entity, so WF-15 reaches holders of the family's roles whose assignment carries no anchor), deep link to ADM-046 of the integration, and codes only — integration, type, severity — never the detail. WF-15 resolves the recipients, channels and templates from the NOTIFICATION_ROUTING version in force: that family's configuration *is* the configured recipient. Its correlation id is the flow's, or a new one for the pass, never the key (R-45). This closes TASK-075 F-1 | FG-05 §10.3: "FG-05 emits typed events … WF-15 resolves recipients, templates, channels and delivery/retry. FG-05 does not implement a second notification engine." Staged with the alert, the intent exists exactly when the alert does |
| D-9 | **And to the on-call channel.** The API logs one line per alert raised, in a fixed form (`Operational alert raised: <TYPE> <SEVERITY> on <INTEGRATION> (alert <id>).`); a log-based metric in each environment counts it with its type, severity and integration, and the policy `<prefix> operational alert raised` sends each new one to the on-call channel, which holds `ALERT_CHANNEL_WEBHOOK_URL` (TASK-091 D-8). SIT, UAT and PROD, as TASK-091's uptime alert | The on-call channel is the platform's only route that does not depend on the platform: if WF-15 or the outbox is what failed, the channel is still told. The API's logs already reach Cloud Logging; no metric exporter, collector or processor is added. FG-05 deduplicates per integration and type, so the channel gets one notification per condition |
| D-10 | **The configured window is 5 minutes**, from a threshold being crossed to the alert reaching its recipient — the threshold TASK-091 set for the uptime alert, until PTBC-037 or TASK-093 sets one. The configured route is FG-05's pass (30 s), the outbox (5 s), WF-15's routing (15 s, in-app) and sending (15 s, e-mail): at most 65 s; the on-call route adds at most 60 s of alignment. `operational-dashboards-check.py` O-4 reads the defaults from the code and keeps the sum inside the window | The window must be checkable, so it is a number in the register with its basis, not a phrase |
| D-11 | **ADM-047 stays live**: the catalogue and the alert, dead-letter and run lists are read again every 30 seconds — the FG-05 pass's interval, more often would show nothing new — each keeping what it shows until the next read lands. Its queue table lists every instance, an integration with nothing queued as zeros | Acceptance criterion 1: "live". The first drill run refreshed only the catalogue: the summary counted three live alerts while the list beneath still said none, so every part now refreshes (§6.3) |
| D-12 | **OD-2's definition is in this directory**, `dashboards/platform-service.json.tftpl`, and Terraform renders it into the environment's dashboard (`infra/terraform/monitoring/operations.tf`). Each panel is PD-n, defined in §4.2; O-7 keeps the two lists equal | The workbook puts the deliverable in `docs/operations`; one definition, so the document cannot describe a dashboard that is not deployed |

## 3. As built

| Piece | Where |
| --- | --- |
| Queue state per instance (`IntegrationQueueState`: depth, in flight, awaiting retry, oldest queued, dead letters) and `availabilityChangedAt` on `GET /integrations` and `GET /integrations/{id}` | `src/backend/PMPlatform.Application/Features/IntegrationMonitoring/Contracts/IntegrationViews.cs`; `IntegrationMonitoringRepository.MeasureQueuesAsync` |
| Thresholds: `AvailabilityAlertDelays`, `QueueBacklogThresholds` (`Depth`, `OldestAge`), `ReconciliationMismatchTolerances` beside TASK-075's `FreshnessWindows`, `MinimumDeliveryRates` | `IntegrationMonitoringPolicy.cs`; evaluated in `IntegrationHealthEvaluation` (the pass), `IntegrationHealthRecorder`, `IntegrationInvocationEngine` |
| `OperationalAlertType.QueueBacklog`; `integration_instance.availability_changed_at`; `ix_invocation_queue_state` | Domain; migration `TASK-092_AddIntegrationQueueStateAndAlertThresholds`; `erd.dbml`, `erd.md` |
| The intent `OperationalAlertRaised` and the on-call log line | `Contracts/Events/OperationalAlertRaised.cs`; `OperationalAlerting.cs` |
| OD-1: ADM-047's queue table, summary and refresh | `src/frontend/src/features/integration-admin/components/QueueTable.tsx`, `IntegrationMonitoringPage.tsx`, `useRefresh.ts` (used by `CataloguePage`, `AlertList`, `InvocationList`, `SyncRunList`); the `integrationAdmin` strings in Arabic and English |
| OD-2, the log-based metric and the on-call policy | `docs/operations/dashboards/platform-service.json.tftpl`; `infra/terraform/monitoring/operations.tf` |
| Threshold values per environment | `integration_alert_thresholds` in `infra/terraform/environments/*/terraform.tfvars` (empty), passed by `platform/main.tf` |
| Every key in one file | The root `.env.example`: the 43 sheet variables, names only (`env-template-check.py`), and every threshold setting for each seeded integration, commented out (O-2). An empty duration is ignored, but an empty `QueueBacklogThresholds` bound declares a threshold with no bound and stops the API, so a threshold line is uncommented only with a value |
| Register and static check | `docs/operations/alert-thresholds.json`; `docs/operations/operational-dashboards-check.py` (repo-checks, O-1 to O-7) |
| Tests | `PMPlatform.Tests.Unit/Application/IntegrationMonitoring/AlertThresholdTests.cs`; `PMPlatform.Tests.Integration/IntegrationMonitoring/OperationalAlertThresholdTests.cs` (CI step "IntegrationMonitoring integration tests"); `integrationAdmin.test.tsx` |

`docs/api/openapi.v1.json` is regenerated: `IntegrationInstanceView` gains `availabilityChangedAt` and `queue`, `IntegrationQueueState` is new, `OperationalAlertType` gains `QUEUE_BACKLOG`. No operation, request or error code changed.

## 4. Dashboard definitions

### 4.1 OD-1 — ADM-047 Integration Monitoring (every registered integration)

`/admin/integration-monitoring`, R01 (FG-05's permissions, TASK-075 D-16), every panel read again every 30 s. All figures are the API's; the screen only adds them up.

| Panel | Shows | Source |
| --- | --- | --- |
| Summary | Integrations whose availability is down; whose freshness is stale; live alerts; operations queued; dead letters — each counted on its own, never combined into one health figure | `GET /integrations` |
| Availability and freshness | Per instance: activation, availability and freshness as two indicators, last success and failure, when evaluated, live alerts (TASK-076 D-1) | `GET /integrations` |
| Queues and dead letters | Per instance: depth, in flight, awaiting retry, oldest queued since (or "Nothing queued"), dead letters | `GET /integrations`, `queue` |
| Operational alerts | Live by default, any status on choice; acknowledge, resolve, dismiss (TASK-076 D-7) | `GET /operational-alerts` |
| Dead letters | The dead-lettered attempts, with retry where FG-05 runs the operation | `GET /integration-invocations?status=DEAD_LETTER` |
| Runs | Sync and reconciliation runs with their counts | `GET /sync-runs` |

### 4.2 OD-2 — platform service (Cloud Monitoring, SIT/UAT/PROD)

`PMPlatform <env> platform service`, in the environment's project. Rendered from `dashboards/platform-service.json.tftpl`; `<service>` is the Cloud Run service `ahda-pmplatform-<env>-api`.

| Panel | Metric | Aggregation |
| --- | --- | --- |
| PD-0 | About this dashboard: what is here, what is on ADM-047, what is in the APM | — |
| PD-1 | API requests per second by response class — `run.googleapis.com/request_count` | rate, summed by `response_code_class` |
| PD-2 | API error rate — 5xx requests over all requests | ratio of two rates |
| PD-3 | API latency, 50th, 95th and 99th percentile — `run.googleapis.com/request_latencies` (ms) | delta, percentile across instances |
| PD-4 | API instances by state — `run.googleapis.com/container/instance_count` | max, summed by `state` |
| PD-5 | `/health/ready` from outside, share of checks passed by region — TASK-091's uptime check | fraction true, 5 min |
| PD-6 | FG-05 operational alerts raised, by type — the log-based metric `<prefix>-operational-alerts-raised` | sum, 5 min |
| PD-7 | Database connections — `cloudsql.googleapis.com/database/postgresql/num_backends`; the load test found 100 the ceiling (`docs/testing/load-test.md`) | mean |
| PD-8 | Database CPU utilisation — `cloudsql.googleapis.com/database/cpu/utilization` | mean, max across |

No threshold line is drawn on PD-2 or PD-3: the workbook asks for alert thresholds on availability, freshness, backlog and mismatch, not on latency or error rate, and no value exists for either (TBC-INT-09, -26).

## 5. Alert threshold configuration

The register is `alert-thresholds.json`; this is its summary. Every threshold is **OPEN** on PTBC-037 but AT-7, which has none.

| # | Alert | Setting (per integration code) | Unit | Without a value | Waits on |
| --- | --- | --- | --- | --- | --- |
| AT-1 | AVAILABILITY (CRITICAL) | `AvailabilityAlertDelays:<CODE>` | duration | Alerted at once | PTBC-037, TBC-INT-08 |
| AT-2 | FRESHNESS | `FreshnessWindows:<CODE>` | duration | Freshness UNKNOWN, no alert | PTBC-037, TBC-INT-12 |
| AT-3 | QUEUE_BACKLOG | `QueueBacklogThresholds:<CODE>:Depth` | operations | No alert on depth | PTBC-037, TBC-INT-11 |
| AT-4 | QUEUE_BACKLOG | `QueueBacklogThresholds:<CODE>:OldestAge` | duration | No alert on age | PTBC-037, TBC-INT-11 |
| AT-5 | RECONCILIATION_MISMATCH | `ReconciliationMismatchTolerances:<CODE>` | records | Any mismatch alerted | PTBC-037, TBC-INT-15 |
| AT-6 | DELIVERY_RATE | `MinimumDeliveryRates:<CODE>` | rate | No alert | TBC-INT-10, ADR-004 |
| AT-7 | DEAD_LETTER | — | — | Always alerted | TBC-INT-14 |

**Setting a value.** In the environment's `terraform.tfvars`, the setting without its prefix, `:` written `__`:

```hcl
integration_alert_thresholds = {
  "QueueBacklogThresholds__EXCHANGE_EMAIL__Depth" = "50"
  "AvailabilityAlertDelays__DIRECTORY"            = "00:05:00"
}
```

The next deployment passes them to the API as `IntegrationMonitoring__Policy__…`. A key Terraform does not recognise fails the plan; a value the API cannot use (a zero delay, a negative depth) stops the API at start-up. In PROD, mark the threshold CONFIRMED with its reference in the register first (O-3).

**The routing configuration each environment authors** (FG-04 and WF-15 are configuration, not seed: `notification-runtime.md` F-4). Without it an alert's intent is FAILED `CONFIGURATION_MISSING` and only the on-call channel is told.

1. A NOTIFICATION_ROUTING version that adds the family `INTEGRATION_OPERATIONAL_ALERT` — proposed: mandatory, IN_APP and EMAIL, to R01 — **and keeps every family of the version it replaces**: the newest PUBLISHED version is the one in force.
2. Two templates for the event type `IntegrationMonitoring.OperationalAlertRaised`, IN_APP and EMAIL, in both languages, using only `{{integrationCode}}`, `{{alertType}}`, `{{severity}}` and `{{deepLink}}`; authored, validated and published by three people (ADR-012). The text used in §6.3:

| | English | Arabic |
| --- | --- | --- |
| Subject | Operational alert {{alertType}} ({{severity}}): {{integrationCode}} | تنبيه تشغيلي {{alertType}} ({{severity}}): {{integrationCode}} |
| Body (e-mail adds the link) | An operational alert of type {{alertType}} and severity {{severity}} was raised on integration {{integrationCode}}. No business record was changed; handle it on the integration's screen. Open the integration: {{deepLink}} | رُفع تنبيه تشغيلي من نوع {{alertType}} بخطورة {{severity}} على التكامل {{integrationCode}}. لم تتغير أي بيانات أعمال. افتح التكامل: {{deepLink}} |

## 6. Verification

### 6.1 Tests

| Suite | What it proves | Result |
| --- | --- | --- |
| `OperationalAlertThresholdTests.ABacklogBeyondItsThresholdRaisesOneAlertWhichWf15DeliversToTheConfiguredRecipient` | **Acceptance criterion 2 in process.** An integration registered for the test, its provider down, a backlog threshold of 1: one operation queued raises nothing; two raise one QUEUE_BACKLOG alert whose detail gives the counts, and a second pass no second one. One outbox dispatch and one WF-15 pass later the family's recipient, local.r01 (R01), has it in-app (`WARNING QUEUE_BACKLOG: <code>`, deep link to ADM-046) and an e-mail at the in-process SMTP relay, its rendered body linking to `APP_BASE_URL` + the deep link, without the detail; nobody else receives it | passed |
| `.EveryRegisteredIntegrationShowsItsQueueDepthRetriesAndDeadLettersAsTheyChange` | **Acceptance criterion 1 in process.** Every instance of `GET /integrations` has its queue; two operations against a provider that is down are depth 2, both awaiting a retry, dated by the first attempt of the older; still so after one retry each; then 2 dead letters and depth 0 | passed |
| `.WithoutABacklogThresholdNoBacklogAlertIsInvented` | Five operations queued and no threshold: no QUEUE_BACKLOG alert | passed |
| `.AnAvailabilityAlertWaitsForItsDelayAndIsRaisedOncePerOutage` | With a 5-minute delay: nothing at the failure or the next pass; raised once the delay has passed; resolved by an operator during the outage, not raised again; recovery and a second outage raise a second one after the delay | passed |
| `.AReconciliationMismatchWithinItsToleranceRaisesNoAlertAndOneBeyondItDoes` | Tolerance 2: 2 mismatches raise nothing, 3 raise RECONCILIATION_MISMATCH; both runs record their counts | passed |
| `IntegrationFailureSimulationTests` (TASK-075, updated) | The outage simulation now also asserts that WF-15 received exactly one `IntegrationMonitoring.OperationalAlertRaised` intent per alert raised, and still that no business table changed (WF-15's received intents left out of the digest as communication history) | passed |
| `IntegrationAccessTests` (TASK-075, updated) | The seeded catalogue, ignoring the integrations this suite's tests register | passed |
| `AlertThresholdTests` (13, unit) | Depth and age bounds strict, either bound sufficient, dead letters not backlog; a change of availability dated by the attempt that made it, a refusal not; a delay counted from going down; QUEUE_BACKLOG a warning; no threshold by default; a threshold without a usable bound, a zero delay or a negative tolerance refused | 13 passed |
| `integrationAdmin.test.tsx` (3 new) | ADM-047 lists the queue of every integration, one registered after the seed included, with each part, the oldest queued time and "Nothing queued"; the summary counts queued work and dead letters; axe clean. The catalogue and the three lists are read again after 30 s and a forced backlog appears without reopening the screen, not before. QUEUE_BACKLOG is named | 3 passed |
| `operational-dashboards-check.py` | O-1 to O-7 pass; 12 seeded violations each fail it (§6.2) | pass |
| `terraform fmt -check`, `terraform validate` (four roots, provider 8.x, read-only locks); a provider-free plan rendering the dashboard template | Valid; the template renders to 9 panels, Cloud Monitoring's own `${metric.label.*}` legends intact | pass |
| `terraform-check.py`, `uptime-monitoring-check.py`, `secret-management-check.py`, `environment-separation-check.py`, `cicd-pipeline-check.py`, `env-template-check.py`, `network-security-check.py`, `database-check.py`, `erd-check.py`, `contract-check.py`, `findings-log-check.py`, `test-strategy-check.py` | Unchanged rules still pass with this change; `contract-check.py` validates the new event sample (it caught a correlation id equal to the idempotency key, R-45, fixed in the code before commit) | pass |

| Regression, run on 2026-10-21 | `dotnet build src/backend -warnaserror` Debug and Release: 0 warnings, 0 errors. Unit: **1970 passed** (a first run aborted with every test passed, the known test-host crash; the rerun was clean). Integration, the whole suite with `AHDA-postgres`, `AHDA-ldap` and Redis: **977 passed**, Persistence included. Frontend in the e2e runner: typecheck, lint, Prettier clean; **1029 passed, 59 files**; production build. `migration-dry-run.sh`: 4116 statements applied twice, no destructive statement (the check constraint's drop is marked: it is widened and re-added, as TASK-063 did); `seed-dry-run.sh`: 23 tables, unchanged by a second run, no integrity violation | pass |

### 6.2 Mutation tests and seeded violations

Each mutant was applied alone, built, judged and restored with a fresh modification time; the unmutated tree passed first (`artifacts/task-092/mutations`, not committed). Backend mutants are judged by the IntegrationMonitoring unit tests and the whole FG-05 integration namespace; frontend mutants by the `integration-admin` suite's exit code; seeded violations by `operational-dashboards-check.py` failing with the expected rule.

**Backend: 13 of 13 killed.**

| # | Mutant | Killed by |
| --- | --- | --- |
| M1 | A backlog is exceeded at its depth, not beyond it | `ABacklogIsExceededBeyondItsDepthNotAtIt`, `EitherBoundAloneExceedsABacklogThatHasBoth`, `DeadLettersAreNotPartOfTheBacklog` |
| M2 | The oldest-age bound is ignored | `ABacklogIsExceededWhenItsOldestOperationHasWaitedBeyondItsAge`, `EitherBoundAlone…` |
| M3 | The pass never checks the backlog | `ABacklogBeyondItsThresholdRaisesOneAlertWhichWf15DeliversToTheConfiguredRecipient`, `ADownstreamOutageChanges…` |
| M4 | A raised alert publishes no intent | `ABacklogBeyondItsThreshold…`, `ADownstreamOutageChanges…` |
| M5 | The intent is keyed by a fresh id, not the alert's | same |
| M6 | The intent's correlation id is its key (R-45) | same |
| M7 | With a delay, the failure that takes the integration down still alerts at once | `AnAvailabilityAlertWaitsForItsDelayAndIsRaisedOncePerOutage` |
| M8 | An outage whose alert an operator closed is alerted again on every pass | `AnAvailabilityAlertWaits…` |
| M9 | A change of availability is not dated | `AChangeOfAvailabilityIsDatedByTheAttemptThatMadeIt`, `AnAvailabilityAlertWaits…` |
| M10 | The mismatch tolerance is ignored | `AReconciliationMismatchWithinItsToleranceRaisesNoAlertAndOneBeyondItDoes` |
| M11 | Dead letters counted as queued work | `DeadLettersAreNotPartOfTheBacklog`, `EveryRegisteredIntegrationShowsItsQueueDepth…` |
| M12 | The oldest queued operation dated by its latest attempt | `EveryRegisteredIntegrationShowsItsQueueDepth…` (still dated by the first attempt after a retry) |
| M13 | A backlog threshold without a bound accepted | `ABacklogThresholdWithoutAUsableBoundIsRefused` |

**Frontend: 5 of 5 killed** — ADM-047 not refreshed (F1), the depth column showing only the retries (F2), the summary's dead letters counting live alerts (F3), an integration with nothing queued left out (F4), the queue read every 3 minutes (F5); each by the TASK-092 tests of `integrationAdmin.test.tsx`.

**Static check: 12 of 12 seeded violations caught**, the tree restored and passing after: an alert type not registered (O-1); a setting the API does not read, Terraform admitting any key (O-2); PROD setting an unconfirmed threshold, an environment setting an unregistered key (O-3); WF-15 polling every 5 minutes, a 635 s route (O-4); the log line reworded, the integration extractor no longer matching (O-5); no intent staged, the intent's family not the register's (O-6); a panel the record does not define, ADM-047 without its dead-letter column (O-7).

### 6.3 The workbook's validation check, on the local DEV stack

`artifacts/task-092/live` (not committed): `reset.sh` builds `task092_live` — migrated by this branch's `ahda-migrate`, seeded by the compose seed, with the §5 routing configuration (`setup.sql`: the family to R01, in-app and e-mail, and the two templates, authored by local.r01, validated by local.r02, published by local.r03) — mailpit as the Exchange relay, and two containers of this branch's `ahda-api` image on the compose environment: **AHDA-api-092**, healthy, for sign-in and the SPA, FG-05's pass parked; **AHDA-api-092-outage**, whose directory is unreachable (port 1), with `IntegrationMonitoring__Policy__QueueBacklogThresholds__DIRECTORY__Depth=2`, the only one running FG-05's pass, at its default 30 s. Both run the outbox (5 s) and WF-15 (15 s) at their defaults. The image's `/openapi/v1.json` equals `docs/api/openapi.v1.json` but for its `servers` entry. `check.py` hosts the MFA stand-in, opens ADM-047 in Chromium (`check092.mjs`, a second Vite on 5186) **before** forcing anything, then forces the breach: three reconciliations of the directory as local.r01. Run on 2026-10-21:

| Step | Observed |
| --- | --- |
| Before | ADM-047 lists the queue of all six registered integrations; DIRECTORY 0 queued, 0 dead letters, "Nothing queued" |
| The forced breach, 16:47:45Z | Three reconciliations answered 201, each attempt 1 RETRY_SCHEDULED; `GET /integrations`: DIRECTORY depth 3, 3 awaiting a retry, oldest queued 16:47:44.69 — past the threshold of 2 |
| **The alert**, +20.2 s | One QUEUE_BACKLOG alert: "DIRECTORY has 3 operations queued (0 in flight, 3 awaiting a retry), the oldest since …, beyond its backlog threshold (depth 2, oldest age none)". An AVAILABILITY alert (CRITICAL, no delay configured) was raised at the first failure, as built |
| **Routed to the configured recipient**, +35.2 s in-app, +35.3 s e-mail | WF-15's intent: family INTEGRATION_OPERATIONAL_ALERT, deep link `/admin/integration-detail-configuration/<DIRECTORY>`, COMPLETED. Deliveries to local.r01 and nobody else: IN_APP SENT; EMAIL SENT and received by mailpit at `local.r01@pmplatform.local`, subject "Operational alert QUEUE_BACKLOG (WARNING): DIRECTORY". **35.3 s against the 300 s window.** local.r01's notification centre lists it |
| **The dashboard reflects it**, no reload | The open page showed DIRECTORY 3 queued, 3 awaiting a retry, the oldest dated, summary "Queued 3", 30 s after the page was ready; axe clean. 210 s after, the same page showed 0 queued and 3 dead letters as the retries ran out, and the alert list beside them named the backlog alert. Seven catalogue reads, every 30 s |
| Arabic | Right-to-left, the queue section titled «قوائم الانتظار والرسائل المتعذرة»; axe clean |
| **The on-call route's input** | The API logged one line per alert raised — `Operational alert raised: AVAILABILITY CRITICAL on DIRECTORY (alert …)`, `… QUEUE_BACKLOG WARNING on DIRECTORY …`, `… DEAD_LETTER WARNING on DIRECTORY …` — 3 lines for 3 alerts; the log-based metric's three extractors, taken from `operations.tf`, read each line's type, severity and integration back correctly. Cloud Logging and the policy themselves need an environment (F-1) |

| **The dead-letter condition** (re-run, below) | After their last retry the three operations were dead letters; one DEAD_LETTER alert was raised as the first was dead-lettered and reached local.r01 in-app 14.8 s and by e-mail 14.8 s later |

**11 of 11 driver checks and 10 of 10 browser checks passed.** **Re-run on the PR head** (`b35829a`, images rebuilt, a fresh `task092_live`, the image's OpenAPI equal to the snapshot), with the dead-letter routing asserted too: **15 of 15 driver checks and 10 of 10 browser checks passed** — QUEUE_BACKLOG raised 19.3 s after the breach and received in-app 34.2 s and by e-mail 34.4 s after it; the three operations dead-lettered and DEAD_LETTER received 14.8 s after the first; ADM-047 showed 3 queued, then 3 dead letters and the alerts, without a reload. Two earlier runs are kept beside it (`check-run1.out`, `check-run2.out`): the first failed only in the harness — Puppeteer's 180 s protocol timeout ended a 7-minute wait while the page already showed the dead letters; the second found the alert list not refreshed (D-11), fixed before this run. Screenshots: `artifacts/task-092/live/out`.

## 7. Operating it

### 7.1 Setting up an environment (SIT, UAT, PROD)

1. TASK-091's §7.1 first: the on-call channel exists and its name reaches the deployment. Its policy and OD-2 are applied with the uptime check.
2. Author the routing configuration of §5 (the family, kept with every existing family, and the two templates).
3. Set the thresholds AHDA has confirmed in the environment's `terraform.tfvars` (§5), and deploy.

### 7.2 When an operational alert arrives

The notification names the integration, the alert type and its severity, and links to the integration's ADM-046; ADM-047 shows it among the live alerts with its detail. What each type means and what to look at:

| Type | Meaning | First look |
| --- | --- | --- |
| AVAILABILITY | The latest technical outcome is a failure (for the delay, if one is set) | ADM-048 attempts of the integration: the failure code (`UNREACHABLE`, `SocketException`); the provider's own status |
| FRESHNESS | No success within the integration's window | Whether anything called it: a source nobody calls goes stale without failing |
| QUEUE_BACKLOG | More work queued than the threshold, or queued for too long | ADM-047's queue table: awaiting retry (the provider is failing) or in flight (it is slow) |
| DEAD_LETTER | An operation failed every permitted attempt | ADM-047's dead letters; retry once the cause is fixed, where FG-05 runs the operation |
| RECONCILIATION_MISMATCH | A reconciliation found more differences than tolerated; nothing was repaired | The run's detail names people and attributes, never values; the source's owner decides |
| DELIVERY_RATE | The provider delivered less than its minimum share over the window | ADM-042's measures |

Raising an alert changed no business record; acknowledging it does not claim recovery, and recovery does not close it (TASK-075 D-9). The incident's tier comes from [`incident-severity-classification.md`](incident-severity-classification.md) §2 by integration and alert type, not from the alert's own severity; the response is [`on-call-runbook.md`](on-call-runbook.md) PB-4 to PB-6.

### 7.3 The drill (SIT or UAT)

The workbook's check, as §6.3 ran it: force a backlog and watch it arrive.

1. In the environment's `terraform.tfvars` set a test threshold the drill will cross, `"QueueBacklogThresholds__DIRECTORY__Depth" = "2"`, and deploy. Record that the value is a drill value.
2. Make the directory unreachable to FG-05's operations only: the drill needs the reconciliation to fail technically while sign-in still works. In SIT this is a second revision of the service with `AD_LDAP_URL` pointing at a closed port, taking no traffic and running FG-05's pass, as §6.3's outage API does; or, simpler, a scheduled window in which the directory's firewall refuses the platform. Agree which with the directory's owner first.
3. As an R01 holder, open ADM-047, then start three reconciliations of the directory (ADM-041's *Run reconciliation*, three times). Note the time.
4. Confirm without reloading that ADM-047 shows the directory with 3 queued, then 3 dead letters; that the on-call channel and the family's recipients (in-app and e-mail) receive the QUEUE_BACKLOG alert within 5 minutes of step 3; that OD-2's PD-6 counts it.
5. Restore the directory and remove the drill threshold. Resolve the drill's alerts with a note naming the drill.

## 8. Acceptance criteria and validation check

| Criterion / check | Evidence | Verdict |
| --- | --- | --- |
| Operational dashboard shows live integration health, queue depth and dead-letter count for every registered integration | OD-1 (D-1, D-2, D-11): `EveryRegisteredIntegrationShowsItsQueueDepthRetriesAndDeadLettersAsTheyChange` and the three ADM-047 tests (CI, every PR); §6.3 in Chromium against the release image | **Met** locally (§6.3: the backlog and its dead letters appeared on the open page within one refresh); in SIT once it exists (F-1) |
| A manually forced threshold breach in a non-PROD environment produces an Operational Alert routed to the configured recipient within the configured window | D-5 to D-10: `ABacklogBeyondItsThresholdRaisesOneAlertWhichWf15DeliversToTheConfiguredRecipient` (CI, every PR); §6.3 on the local DEV stack | **Met** locally: QUEUE_BACKLOG raised 20.2 s after the breach and delivered to R01 in-app and by e-mail 35.3 s after it, against a 300 s window. The on-call half is codified and its input verified; Cloud Monitoring itself is owed on SIT (F-1) |
| Validation check: force a dead-letter/backlog condition in a non-PROD environment and confirm the operational dashboard reflects it and the alert fires as configured | §6.3 | **Done on the local DEV stack** with the release image; owed on SIT with §7.3 (F-1) |
| Deliverables: operational dashboard definitions, alert threshold configuration | §4 (OD-1, OD-2 and its definition), §5 and `alert-thresholds.json` | **Met** |
| CTL-32 | D-1 to D-12 | **Built**; thresholds open (F-2), OD-2 not applied (F-1) |

## 9. Findings

| # | Finding | Owner | Status |
| --- | --- | --- | --- |
| F-1 | **No environment exists, so OD-2, the log-based metric and the on-call policy have not been applied**, and the drill (§7.3) has not run in SIT. Every apply is held on ADR-001 §9, as for TASK-091 F-1. §6.3 ran everything else end to end locally and read the API's real log lines back with the metric's extractors | DevOps/Platform Lead (TASK-018) | Open |
| F-2 | **Every threshold value is open (PTBC-037).** AT-1 to AT-6 have their settings, units and meaning; AHDA IT / DevOps Lead gives the values per integration and environment. PTBC-046's subject is not recoverable from the sources (`ptbc-tbc-tracker.md` 9.5): it is cited jointly with PTBC-037 and nothing here is attributed to it | AHDA IT / DevOps Lead | Open |
| F-3 | **Queue depth is FG-05's record of unfinished work, not every queue.** Deliveries WF-15 has not attempted yet, and messages waiting in the outbox, are not observed (TASK-075 F-5): a stalled WF-15 pass shows no backlog until its first failed attempt. FG-05 §6.6's queue snapshots would need WF-15 and the outbox to publish their depth as telemetry — an ADR-003 edge for the outbox, which belongs to no module | Engineering Architect (ADR-003) | Open |
| F-4 | **The on-call metric reads the console log's text.** Cloud Run sends each line of the default console formatter as `textPayload`; O-5 keeps the C# message and the metric's regular expressions together, but a change of formatter — JSON logging under TASK-083, for instance — would move the text to `jsonPayload` and the metric would count nothing. TASK-083 must update the filter with the formatter | TASK-083 | Open |
| F-5 | **The routing configuration is not seeded.** Each environment authors the family and the two templates (§5); until then an alert's intent is FAILED `CONFIGURATION_MISSING` (`GET /notification-intents`) and only the on-call channel is told. The proposed recipient, R01, is a proposal (TBC-INT-17) | PMO / FG-04 configuration owner | Open |
| F-6 | **Recovery is not notified.** FG-05's IntegrationRecovered, Queue Recovered and alert resolution events (EVT-INT-027, -030) publish nothing; the on-call incident closes itself 30 minutes after its last alert | FG-05 owner | Open |
| F-7 | **No APM dashboard is defined as code.** Latency and errors per route are in the APM's own views (TASK-090 §5.3); which APM SIT/UAT/PROD use is open (TASK-090 F-2), and GlitchTip has no dashboard API. OD-2 gives the service-level latency and error rate from Cloud Run | DevOps/Platform Lead | Open |
| F-8 | **Rolling back the migration fails once a QUEUE_BACKLOG alert exists**: the narrower check constraint cannot be restored over it and alerts cannot be deleted (TASK-075 guard). Down is for an environment that has not raised one | — | Accepted |

## 10. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-10-21 | Initial record. OD-1 and OD-2, the queue state, three new thresholds and QUEUE_BACKLOG, the route of each alert through WF-15 and to the on-call channel, the register and its check; 12 decisions, 8 findings. | Observability (TASK-092) |
| 2026-10-21 | §6.3 re-run on the PR head with the dead-letter route asserted: 15 of 15 and 10 of 10. The root `.env.example` added (every key). | Observability (TASK-092) |
| 2026-10-23 | §7.2 points to the incident tiers and the on-call playbooks (TASK-093). | Observability (TASK-093) |

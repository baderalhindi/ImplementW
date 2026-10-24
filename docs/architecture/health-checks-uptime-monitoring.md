# Health Checks & Uptime Monitoring

| Field | Value |
| --- | --- |
| Task | TASK-091 — Implement Health Checks & Uptime Monitoring (P16 - Observability) |
| Depends on | TASK-016 (DEV / SIT / UAT / PROD environment separation: `infra/environments/environments.json`, whose `APP_BASE_URL` and `ALERT_CHANNEL_WEBHOOK_URL` this task reads); TASK-017 (the Terraform it extends); TASK-018 (the deploy action that applies it) |
| Record date | 2026-10-20 |
| Status | **BUILT; VERIFIED LOCALLY END TO END, WITH A STAND-IN FOR CLOUD MONITORING.** The health endpoints are tested in process against a database cut mid-run (§5.1), by mutation (§5.2), and with the release image against a database container that was stopped and paused, using the drill script SIT will run (§5.3). The alert path was run with a real open-source uptime monitor set to the Terraform's timings and a webhook receiver as the on-call channel: the alert arrived 1 min 42 s after the database stopped (§5.4). The Cloud Monitoring check, alert and channel themselves are codified and validated, but no environment exists to apply them to (F-1) |
| Branch | `infra/task-091-health-checks-uptime-monitoring` |
| Directory | `src/backend/PMPlatform.Api/HealthChecks` (the endpoints); the checks are Infrastructure's (`Persistence/PostgreSqlHealthCheck.cs`, `Hosting/WorkerLivenessHealthCheck.cs`), because only the composition root may reference Infrastructure (ADR-003 L-4) |
| Deliverables | **Health-check endpoints:** `/health/live`, `/health/ready`, and `/health` kept for its callers. **Uptime-monitor configuration:** `infra/terraform/monitoring` — a Cloud Monitoring uptime check on each environment's `/health/ready` and an alert policy on it; Cloud Run's probes in `infra/terraform/compute`. **Alert routing:** the on-call channel, kept by `infra/environments/route-alerts.sh` from `ALERT_CHANNEL_WEBHOOK_URL` and handed to the alert by the deploy action. **Validation drill:** `infra/environments/drill-database-outage.sh` |
| Environment variables / secrets | `ALERT_CHANNEL_WEBHOOK_URL` (Secret, SIT/UAT/PROD — `pmplatform-{env}-alert-channel-webhook-url`, already in the manifest): read only by `route-alerts.sh`, run by its owner. `UPTIME_MONITOR_API_KEY` (Secret, SIT/UAT/PROD): **not read by anything** — Cloud Monitoring authenticates with the deploying identity, not a key (F-2). New setting: `HealthChecks:WorkerPassTimeout` (2 hours). New Terraform inputs: `uptime_monitoring` (per environment, committed) and `alert_notification_channel` (supplied at apply time, never committed) |
| Gate decision applied | None in the workbook row |
| Control | CTL-31 (this task); CTL-50 (the uptime service named with its processing location, D-6, F-5); CTL-18 (the webhook URL never in the repository, the state or a process argument, D-8) |
| Sources read | The TASK-091 row, read from the workbook (`AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx`) on 2026-10-20: description, acceptance criteria, deliverables, branch, variables and the validation check. The Environment and Secrets rows `UPTIME_MONITOR_API_KEY` and `ALERT_CHANNEL_WEBHOOK_URL`; `cybersecurity-control-matrix.md` CTL-31 and CTL-50; `secret-management.md` §3; `infrastructure-as-code.md` §3 and T-9; `apm-error-tracking.md` (TASK-090: availability left to this task); `local-development-environment.md` §3.4 (`/health` as TASK-014 built it) |

## 1. Scope

| | Subject | Owner |
| --- | --- | --- |
| **In** | Liveness and readiness of the API and of the background workers it hosts, as HTTP endpoints | This task |
| **In** | Cloud Run's start-up and liveness probes on those endpoints | This task |
| **In** | An outside-in uptime check of each environment, an alert on it, and its route to the on-call channel | This task |
| **Out** | Creating the on-call channel itself and the `ALERT_CHANNEL_WEBHOOK_URL` values; applying any environment | DevOps/Platform Lead (F-1, F-4) |
| **Out** | Who is on call, severity tiers, response targets | TASK-093 |
| **Out** | FG-05 integration health, queue depth, latency dashboards and their thresholds | TASK-092 |

## 2. Decisions

| # | Decision | Why |
| --- | --- | --- |
| D-1 | **Three endpoints, plain text, nothing but the status.** `/health/live` (liveness), `/health/ready` (readiness) and `/health` (every check — the path TASK-014 introduced, which the compose stack, the smoke tests, the restore drill and the rotation rehearsal read). Each answers 200 `Healthy` or 503 `Unhealthy`, is anonymous and is never cached (`Cache-Control: no-store, no-cache`). Which check failed and why goes to the log (`Health check postgresql with status Unhealthy … 'PostgreSQL unreachable.'`), never to the caller | Probes and the uptime monitor carry no credential. The body stays the two words `rehearse-rotation.sh` compares; anything more would tell an anonymous caller about the database |
| D-2 | **Liveness never touches the database.** It is the process answering and no worker being stalled (D-3). Readiness is the database answering | A liveness failure restarts the instance. Were the database in it, an outage would restart every instance at once, repeatedly, and a restart does not bring a database back. That was the case until now: both Cloud Run probes read `/health`, which checks the database (D-5) |
| D-3 | **A worker is stalled when one pass has run longer than `HealthChecks:WorkerPassTimeout`, 2 hours by default.** `PollingWorker` records when each pass begins and clears it when the pass ends, however it ends (`WorkerActivity`); `WorkerLivenessHealthCheck` names any worker past the limit. A failed pass is not a stall — it is logged and retried — so a database outage leaves the workers live. The workers' readiness is the API's: they run in the same process and need the same database, and no traffic is routed to them | A pass that never returns is the one failure only a restart fixes. Busy is not stalled: the slowest pass found is a notification batch of 50 against an SMTP server that accepts and never answers, 50 × the 30-second `Notifications:Email:Timeout`, about 25 minutes; the outbound HTTP clients time out at 10 seconds. A server that answers slowly stretches each operation further, so the default leaves a wide margin (F-6) |
| D-4 | **The database probe is shared, bounded and cannot be cancelled by a caller.** One per instance at a time, at most one started every 2 seconds, the checks in between sharing its result; its own connection pool, with Npgsql's connect and command timeouts at 3 seconds; no caller's cancellation token reaches it | The endpoints are anonymous: without sharing, every request would open a database connection (AUTH-T28). Npgsql does not honour a cancellation token while it waits for the server's handshake — a database that accepted and never answered held the probe for **15.0 s**, its default timeout, in the first test run — so the bound is the driver's own. A caller that disconnects cannot fail the result others are waiting for, so readiness cannot be made to flap |
| D-5 | **Cloud Run: start-up reads `/health/ready` (timeout 4 s), liveness reads `/health/live`.** `cpu_idle = false` is written down | A revision that cannot reach its database never takes traffic, and the deployment fails on the old revision. The 4-second timeout sits above the 3-second database probe; the default 1 second would fail every slow start-up as a timeout. The workers run between requests, so CPU must stay allocated; the provider already does so whenever `resources` is set, and the line keeps it from being lost |
| D-6 | **The uptime monitor is Cloud Monitoring, in each environment's own project.** An uptime check every 60 s from three checker regions (`EUROPE`, `ASIA_PACIFIC`, `USA_VIRGINIA` — Cloud Monitoring requires three, and has none in the Kingdom) requests `https://<APP_BASE_URL host>/health/ready` over TLS through the load balancer, expecting 200 within 10 s | It checks what a user depends on, end to end: DNS, the certificate, the load balancer and its WAF, Cloud Run, and the database. It is in the platform's own tenancy, IAM and Terraform, so no other processor is added and no API key is needed. What crosses the border is one anonymous GET and the word `Healthy` or `Unhealthy` (CTL-50, F-5). A third-party monitor would bring a new processor needing AHDA Cybersecurity's acceptance; a self-hosted one would need something to watch it |
| D-7 | **The alert fires when 2 of the 3 regions see a failure that lasts 60 s; the configured threshold is 5 minutes.** `check_passed`, `ALIGN_NEXT_OLDER`, `REDUCE_COUNT_FALSE`, threshold `> 1`, duration 60 s; severity CRITICAL, closed automatically 30 minutes after recovery, with the meaning of a 503 and a pointer to §7 in its text. Configuration lets an outage go unflagged for at most 60 + 10 + 60 = **130 s**; the remainder of the 5 minutes is Cloud Monitoring's own ingestion, evaluation and delivery, which is not measured yet (F-3) | One region alone is a checker's own network fault as often as an outage; one failed check alone is a lost packet. `uptime-monitoring-check.py` H-5 keeps the configured part at or below 180 s, so at least two minutes of the threshold are left for the service |
| D-8 | **The alert's route: the channel is kept outside Terraform, and Terraform is given its name.** The channel is a Cloud Monitoring `webhook_tokenauth` channel, `pmplatform-{env}-on-call`, whose URL is `ALERT_CHANNEL_WEBHOOK_URL`. Its owner runs `route-alerts.sh <env>`, which reads the URL from the secret store into a variable and passes it to gcloud on stdin, then creates the channel or updates it in place. The deploy action reads the channel's resource name with `route-alerts.sh <env> --name` (no secret read) and passes it as `TF_VAR_alert_notification_channel`, as it passes the image digest. A plan with monitoring and no channel is refused (`platform/guards.tf`) | Nothing in `infra/terraform` reads a secret (`secret-management.md` §3): a channel resource, or a data source looking one up, would copy the URL into the state file. Attaching the channel from outside instead would need `ignore_changes`, which T-9 refuses for good reason. Rotation keeps the channel's name, so a rotated URL needs no Terraform change |
| D-9 | **DEV has no uptime check** (`uptime_monitoring = false`) | The Environment and Secrets sheet gives DEV no `ALERT_CHANNEL_WEBHOOK_URL`, so an alert there would reach no one. Its probes and endpoints are the same as everywhere |

## 3. As built

| Piece | Where |
| --- | --- |
| `HealthEndpoints` (`/health`, `/health/live`, `/health/ready`), mapped by `app.MapHealthEndpoints()` | `src/backend/PMPlatform.Api/HealthChecks/`, `Program.cs` |
| `HealthCheckTags` (`live`, `ready`) | `src/backend/PMPlatform.Application/Common/Observability/` |
| `PostgreSqlHealthCheck` (readiness, D-4) | `src/backend/PMPlatform.Infrastructure/Persistence/` |
| `WorkerActivity`, `WorkerLivenessHealthCheck`, `WorkerLivenessOptions`; `PollingWorker` records each pass | `src/backend/PMPlatform.Infrastructure/Hosting/`; the twelve workers pass `WorkerActivity` to the base class |
| Registration with tags, `HealthChecks` options | `PMPlatform.Infrastructure/DependencyInjection.cs`; `appsettings.json` |
| Cloud Run probes | `infra/terraform/compute/main.tf` |
| Uptime check, alert policy | `infra/terraform/monitoring/` (sixth module), composed in `infra/terraform/platform/main.tf` where `uptime_monitoring` is true; the channel precondition in `platform/guards.tf` |
| The on-call channel | `infra/environments/route-alerts.sh`; the deploy action's step "Find the on-call channel" |
| The validation drill | `infra/environments/drill-database-outage.sh` |
| Static check | `docs/architecture/uptime-monitoring-check.py` (repo-checks, H-1 to H-7) |
| Tests | `PMPlatform.Tests.Integration/Observability/HealthCheckTests.cs` and `DatabaseRelay.cs` (CI step "Observability integration tests"); `PMPlatform.Tests.Unit/Infrastructure/Persistence/PostgreSqlHealthCheckTests.cs`, `…/Hosting/WorkerLivenessTests.cs` |

No API operation is added to `docs/api/openapi.v1.json`: health endpoints are outside `/api/v1` and not part of the contract.

## 4. Endpoints

| Path | Checks | Healthy | Unhealthy | Read by |
| --- | --- | --- | --- | --- |
| `/health/live` | `background-workers` | 200 `Healthy` | 503 `Unhealthy` — a worker in one pass for longer than `WorkerPassTimeout` | Cloud Run's liveness probe (every 30 s, three failures restart the instance) |
| `/health/ready` | `postgresql` | 200 `Healthy` | 503 `Unhealthy` — the database refused, failed, or did not answer within 3 s | Cloud Run's start-up probe; each environment's uptime check |
| `/health` | both | 200 `Healthy` | 503 `Unhealthy` | The compose health check, `smoke-test.sh`, the deploy action's smoke test, the restore drill, the rotation rehearsal — unchanged |

## 5. Verification

### 5.1 Tests

| Suite | What it proves | Result |
| --- | --- | --- |
| `HealthCheckTests.ReadinessIsUnhealthyWhileTheDatabaseIsDownAndLivenessIsNot` | **Acceptance criterion 1 in process.** The API's database sits behind a TCP relay (`DatabaseRelay`) that the test cuts while the API runs: every relayed connection reset, new ones refused. `/health/ready` and `/health` turn 503 within the reuse window plus the probe's timeout, `/health/live` stays 200; with the relay restored, both are 200 again | passed |
| `HealthCheckTests.ADatabaseThatNeverAnswersIsReportedUnhealthyWithinTheProbeTimeout` | The relay accepts connections and never answers. Every `/health/ready` request returns within 3 s + 1.5 s and the answer turns 503; liveness stays 200 | passed |
| `HealthCheckTests.AWorkerStuckInOnePassFailsLiveness` | With `HealthChecks:WorkerPassTimeout` at 1 minute and a pass recorded 2 minutes ago, `/health/live` and `/health` are 503 and `/health/ready` 200; once the pass ends, live is 200 | passed |
| `HealthCheckTests.EachHealthEndpointIsAnonymousAndNeverCached` (3 cases) | No credential; `Healthy`; `Cache-Control: no-store` | 3 passed |
| `PostgreSqlHealthCheckTests` (4, unit) | Twenty concurrent checks share one probe; a database that never answers is unhealthy within the timeout, not after Npgsql's 15 s; a caller that gives up does not end the shared probe; a result is shared until the next probe is due; no connection string is unhealthy | 4 passed |
| `WorkerLivenessTests` (4, unit) | Between passes, live; a pass up to the timeout is busy, past it stalled and named; `PollingWorker` records a pass while it runs and clears it when it fails | 4 passed |
| `uptime-monitoring-check.py` | H-1 to H-7 pass; seven seeded violations (liveness probe on `/health`, start-up timeout dropped, DEV monitored, uptime check on `/health`, a 300 s failure duration, a channel as a data source, no channel passed by the deploy action) each fail it | 7 of 7 caught |
| `terraform fmt`, `terraform validate` on the four roots (provider 8.x, read-only locks), `terraform-check.py`, `secret-management-check.py`, `findings-log-check.py` | The module is valid against the locked provider; T-9 passes without an exception; no secret reaches Terraform | pass |
| `route-alerts.sh` against a gcloud stub that logs every argument | DEV: nothing to route, exit 0. SIT `--name` before the channel exists: refused, naming the owner's command. Create, then update in place, the same name printed; the URL arrives on stdin only — not once in any logged argument. A failed listing and a non-`https` value are refused with a message | pass |
| `docs/security/threat-model-authentication.md` | The health endpoints listed among the anonymous endpoints, with AUTH-T28 (flooding them: Medium inherent, Low residual, MITIGATED) | `findings-log-check.py` passes |

### 5.2 Mutation tests

Each mutant was compiled into the solution and judged by `HealthCheckTests`, `PostgreSqlHealthCheckTests` and `WorkerLivenessTests`; the unmutated tree passed first (`artifacts/task-091/mutations/run.py`, not committed).

**9 of 9 killed.**

| # | Mutant | Killed by |
| --- | --- | --- |
| M1 | Liveness also reads the database | `ReadinessIsUnhealthyWhileTheDatabaseIsDownAndLivenessIsNot`, `ADatabaseThatNeverAnswers…` (liveness 503 during the outage) |
| M2 | A failed pass stays recorded (no `finally`) | `APassIsRecordedWhileItRunsWhetherItSucceedsOrFails` |
| M3 | Passes are not recorded | `APassIsRecordedWhileItRunsWhetherItSucceedsOrFails` |
| M4 | A pass exactly at the timeout counts as stalled | `APassUpToTheTimeoutIsBusyNotStalled` |
| M5 | Every check runs its own probe | `ACallerThatGivesUpDoesNotCancelTheProbe…`, `AResultIsSharedUntilTheNextProbeIsDue`, `ConcurrentChecksShareOneProbe…` |
| M6 | The probe has no timeout of its own (Npgsql's 15 s default) | `ConcurrentChecksShareOneProbe…WithinTheTimeout`, `ADatabaseThatNeverAnswersIsReportedUnhealthyWithinTheProbeTimeout` |
| M7 | Readiness runs every check | `AWorkerStuckInOnePassFailsLiveness` (readiness 503 for a stalled worker) |
| M8 | Readiness is not anonymous | `EachHealthEndpointIsAnonymousAndNeverCached`, `ReadinessIsUnhealthy…` |
| M9 | `HealthChecks:WorkerPassTimeout` is not read | `AWorkerStuckInOnePassFailsLiveness` |

The first run exposed a defect in the tests rather than the code: under M3, `APassIsRecorded…` failed its assertion and then hung in `StopAsync` on a pass nobody released. The test now releases the pass and bounds the stop, so a failure fails, and M3 fails in 0.24 s.

### 5.3 The workbook's validation check, locally

"Simulate a database outage in a non-PROD environment and confirm the readiness endpoint reports unhealthy; confirm the configured alert fires and reaches the on-call channel."

The stack: the API image built from this branch (`ahda-api:latest`) as `AHDA-api-091`, with its own PostgreSQL 17 container so the outage touches nothing else, migrated by `ahda-migrate:latest` and seeded by the compose seed. The drill was the committed `drill-database-outage.sh sit`, with SIT's `APP_BASE_URL` pointed at the container and `gcloud sql instances patch --activation-policy=NEVER|ALWAYS` played by `docker stop|start` of the database (`artifacts/task-091/live`: reset.sh, check.sh; not committed). Run on 2026-10-20:

| Step | Result |
| --- | --- |
| D-1 before | `/health`, `/health/live`, `/health/ready` 200 `Healthy`, each `Cache-Control: no-store, no-cache` |
| D-2 the database stopped | 16:52:02Z |
| D-3 **readiness unhealthy** | `/health/ready` **503 after 5 s** (the drill polls every 5 s); `/health/live` 200 |
| D-4 the outage held 90 s | `/health/ready` still 503 at the end; `/health/live` 200 throughout; the container's restart count 0 before and after. The workers kept running their passes and logging each failure (`Outbox dispatch pass failed …`, 19 times) |
| D-5 the database started | `/health/ready` 200 after 5 s |
| A database that accepts connections and never answers (`docker pause`), one request a second for 40 s | Every `/health/ready` **503 in 3.06–3.07 s**; `/health/live` 200; 200 again 1 s after `docker unpause` |
| The body of a 503 | `Unhealthy`, nothing else; the platform's security headers present |
| **The alert reaching the on-call channel** | Run with a stand-in for Cloud Monitoring: §5.4 |

### 5.4 The alert half, with a stand-in for Cloud Monitoring

Cloud Monitoring cannot run outside a GCP project, and none exists (F-1). To run the alert half of the check anyway, the uptime monitor was played by **Gatus 5.37.0** (`mirror.gcr.io/twinproduction/gatus:v5.37.0`, an open-source uptime monitor configured as code), set to the Terraform module's defaults on one prober: `/health/ready` every 60 s, 10 s timeout, 200 expected, the alert sent only when the failure is still there one period later (`failure-threshold: 2`, the equivalent of `failure_duration_seconds = 60`), and a resolved notice on recovery. The on-call channel was a webhook receiver on the compose network that logs each POST with the time it arrived. The outage was the drill of §5.3 again, the committed `drill-database-outage.sh sit` with its default 7-minute hold (`artifacts/task-091/alert`: Gatus config, receiver, drill.log; not committed). Run on 2026-10-20:

| Time (UTC) | Event | After D-2 |
| --- | --- | --- |
| 18:29:03, 18:30:03 | Two checks pass; nothing sent to the channel | — |
| 18:30:21 | **D-2: the database stopped** | 0 |
| 18:30:27 | D-3: `/health/ready` 503 (the drill's own poll); `/health/live` 200 | 6 s |
| 18:31:03 | The monitor's first failed check: `[STATUS] (503) == 200` | 42 s |
| 18:32:03 | Second failed check, one period later: **the alert is sent and arrives at the on-call channel** — `TRIGGERED`, `pmplatform-sit-readiness`, `/health/ready failing` | **1 min 42 s** |
| 18:33:03 – 18:37:03 | Five more failed checks; no repeat notification | — |
| 18:37:32 | D-5: the database started; `/health/ready` 200 five seconds later | — |
| 18:38:03 | The next check passes; `RESOLVED` arrives at the channel | 31 s after recovery |

**The alert reached the channel 1 min 42 s after the outage began, inside the 5-minute threshold and inside the 130 s the configuration allows (D-7).** The API container's restart count stayed 0 throughout. What this does not prove is Cloud Monitoring's part: its two-of-three-regions rule, its ingestion and notification delay (F-3), and the channel type AHDA's on-call tool accepts (F-4). Those are what the SIT run of the same drill adds.


## 6. Acceptance criteria and validation check

| Criterion / check | Evidence | Verdict |
| --- | --- | --- |
| A readiness check correctly reports unhealthy when the database is unreachable (verified by a simulated DB outage in a non-PROD environment) | §5.1 rows 1–2 (CI, every PR); §5.3 D-1 to D-5 and the paused database, with the release image | **Met** locally; on SIT with the same drill once it exists (F-1) |
| An uptime-monitor alert fires within the configured threshold when an environment's health endpoint fails | Locally with a stand-in monitor at the module's timings: the alert arrived **1 min 42 s** after the database stopped, against a 5-minute threshold (§5.4). The Cloud Monitoring check, alert (D-7: at most 130 s of configuration inside the threshold) and route (D-8) are codified, validated and statically checked (§5.1) | **Met with a stand-in monitor**; with Cloud Monitoring itself owed on SIT, where the same drill runs (F-1, F-3) |
| Validation check: simulate a DB outage, confirm readiness unhealthy; confirm the alert fires and reaches the on-call channel | §5.3 (readiness, release image, the database stopped and paused); §5.4 (the alert, sent and received 1 min 42 s after the stop, resolved on recovery) | **Met** locally, with a stand-in for Cloud Monitoring and for the on-call channel; the run on SIT is owed (F-1) |
| CTL-31: liveness and readiness for the API and background workers; readiness unhealthy when the database is unreachable; external uptime monitoring alerting an on-call channel per environment | D-1 to D-9 | **Built**; per-environment alerting for SIT, UAT and PROD, not DEV (D-9) |

## 7. Operating it

### 7.1 Setting up an environment (SIT, UAT, PROD)

1. The owner of `ALERT_CHANNEL_WEBHOOK_URL` writes the on-call channel's webhook URL as the secret's first version (`infra/secrets/set-secret-version.sh`).
2. The same owner runs `infra/environments/route-alerts.sh <env>`. It creates the channel `pmplatform-<env>-on-call` and prints its name.
3. The next deployment reads that name and applies the uptime check and the alert. Until step 2 is done, the plan for that environment refuses with the command to run.

### 7.2 When the alert fires

`/health/ready` is 503 from at least two checker regions: the API cannot reach its database within 3 seconds. No answer at all means the load balancer, the certificate or the service itself. The API's log names the failing check (`Health check postgresql`). Instances are not restarted for a database outage (D-2), so they serve again as soon as the database answers; the alert closes itself 30 minutes after recovery. In PROD this is P1-1 ([`incident-severity-classification.md`](../operations/incident-severity-classification.md)); who is paged and what they do is [`on-call-runbook.md`](../operations/on-call-runbook.md) PB-1.

### 7.3 The drill (SIT or UAT)

`ADR001_CONFIRMATION_REF=… infra/environments/drill-database-outage.sh sit` stops the environment's Cloud SQL instance, waits for `/health/ready` to answer 503, holds the outage for 7 minutes (`HOLD_SECONDS`), and starts the instance again — whatever happens after the stop. The person running it records, in the evidence log, when the alert reached the on-call channel, against the stop time printed at D-2: it must arrive within 5 minutes. PROD and DEV are refused. Rotating the webhook: `secret-rotation-runbook.md` §7 (`ALERT_CHANNEL_WEBHOOK_URL`).

## 8. Findings

| # | Finding | Owner | Status |
| --- | --- | --- | --- |
| F-1 | **No environment exists, so Cloud Monitoring's alert has not fired.** Every apply is held on ADR-001 §9 (the written confirmation, the tenancy UGV-07 and the DNS zone `environment-separation.md` F-1). The alert path was run locally with a stand-in monitor at the same timings (§5.4); the run with Cloud Monitoring and AHDA's on-call channel is owed on SIT, with `drill-database-outage.sh` | DevOps/Platform Lead (TASK-018) | Open |
| F-2 | **`UPTIME_MONITOR_API_KEY` is read by nothing.** Cloud Monitoring authenticates with the deploying identity. The secret holds no value; if AHDA prefers a third-party uptime service, this is its key and the service needs CTL-50 acceptance first; otherwise the Environment and Secrets row can be retired | DevOps/Platform Lead; AHDA Cybersecurity | Open |
| F-3 | **Cloud Monitoring's own delay is not measured.** Configuration accounts for at most 130 s of the 5-minute threshold (D-7); the remaining 170 s is a budget for ingestion, evaluation and delivery, not a figure Google publishes for this path. The SIT drill measures it; if it does not fit, the threshold or the failure duration changes | DevOps/Platform Lead | Open |
| F-4 | **No on-call channel is named.** A `webhook_tokenauth` channel posts Cloud Monitoring's own incident JSON. Microsoft Teams and Slack incoming webhooks expect their own formats and will not show it as a message; PagerDuty, Slack (OAuth) and Google Chat have native Cloud Monitoring channel types instead. Which tool AHDA's on-call uses decides the channel type `route-alerts.sh` creates; the URL secret fits only a webhook that accepts the incident JSON | DevOps/Platform Lead (with TASK-093) | Open |
| F-5 | **The uptime checkers are outside the Kingdom.** Cloud Monitoring is the hosting provider's own service in the platform's project, and its checks carry no personal data — one anonymous GET, one word back — but CTL-50 asks for each external processor to be named with its processing location. Recorded here for AHDA Cybersecurity's list | AHDA Cybersecurity | Open |
| F-6 | **The 2-hour stall threshold is set by the slowest pass, not by measurement.** Today that is notification delivery over SMTP (D-3). The SMS gateway and the malware scanner are not built (`UnconfiguredSmsGateway`, `UnconfiguredMalwareScanner`); when they are, their per-call timeout × their batch size must stay well below `HealthChecks:WorkerPassTimeout`, or a slow provider restarts instances that are working | Developers (the SMS and scanner integrations) | Open |

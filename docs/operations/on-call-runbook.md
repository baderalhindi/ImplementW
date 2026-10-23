# On-call runbook

| Field | Value |
| --- | --- |
| Task | TASK-093 — Document Operational SLAs & On-Call Runbook (P16 - Observability) |
| Depends on | TASK-091 — health checks, uptime alert and on-call channel ([`health-checks-uptime-monitoring.md`](../architecture/health-checks-uptime-monitoring.md)); TASK-023 — backup, restore and DR ([`backup-restore-dr-runbook.md`](backup-restore-dr-runbook.md)). Also uses: TASK-092 FG-05 alerts and dashboards ([`operational-dashboards.md`](operational-dashboards.md)); TASK-019 secret rotation ([`secret-rotation-runbook.md`](../../infra/secrets/secret-rotation-runbook.md)); TASK-039 WF-15 notifications ([`notification-runtime.md`](../architecture/notification-runtime.md)) |
| Companion | [`incident-severity-classification.md`](incident-severity-classification.md) — the P1–P4 rules this runbook applies |
| Record date | 2026-10-23 |
| Status | **WRITTEN — NOT YET STAFFED OR EXERCISED LIVE.** The roles, procedure, playbooks and escalation path are complete; the sample P1 incident was walked through it at the desk (§8). No one is on the rota yet (F-2), the on-call tool is not named (F-3), and no environment exists to page from (`health-checks-uptime-monitoring.md` F-1) |
| Support period | **About 22 months of operations, maintenance and technical support**, after implementation and launch, within the 24-month contract (*Client Original Project Scope* §13; Master Plan Phase 4 "Operate, Support & Improve", months 3–24) |
| Fixed inputs | **RPO 1 hour, RTO 4 to 6 hours** (ADR-001; TASK-093 Gate Decision Applied) |
| Open | **The contractual SLA table — every response, update and resolution time, hours of cover, service credits: TBC (OQ-009 / UGV-06)**, owner AHDA Contracts / PMO Engagement Lead, gate Before Production |
| Participation amendment | **ADR-004: the on-call runbook covers SMS provider failure and the fallback to e-mail and in-app** — PB-5 |
| Control | CTL-34; Release Checklist row "Operational Runbooks & On-Call Ready" (§9); touches CTL-51 (remote vendor access, F-8) and G-6 (security incident response, PB-3) |

## 1. The support model

The contract runs 24 months: implementation and launch first, then about 22 months in which the
delivery team operates, maintains and supports the platform. *Client Original Project Scope* §11 lists
what that period includes: corrective and preventive maintenance; handling faults and technical
problems; software and security updates (patches, bug fixes, hot fixes, service packs); periodic checks
of the platform's health; periodic reports on technical performance; **monthly periodic visits**;
on-site support when needed; changes to the platform and its dashboards; cyber-security updates; and
support for integrations and future development. It also states that the RFP booklet sets SLAs for
response and fault-resolution times by priority. That table has not reached the delivery team (OQ-009),
so this runbook gives the structure — who, in what order, doing what — and leaves every time target
TBC.

This runbook covers **unplanned** incidents. Planned changes go through change control
(`cicd-pipeline.md`; the PROD approval gate); maintenance windows are not yet agreed
(`incident-severity-classification.md` F-6).

## 2. Roles

### 2.1 Who does what

| Role | Side | Does |
| --- | --- | --- |
| **Primary on-call engineer** | Delivery team | Receives every page. Acknowledges, classifies (§2 of the classification), opens the incident record, works the playbook. **Incident commander for P3 and P4**, and for P1/P2 until the commander below takes over |
| **Secondary on-call engineer** | Delivery team | Takes the page when the primary does not acknowledge (§5.3); second pair of hands on P1/P2 |
| **DevOps/Platform Lead** | Delivery team | **Incident commander for P1 and P2.** On-call owner of the DR runbook: declares a recovery and starts the RTO clock (`backup-restore-dr-runbook.md` §3, §4). Decides rollbacks and redeploys |
| **Backend Lead** | Delivery team | Application and data diagnosis; confirms a restore point and signs the restored data (`backup-restore-dr-runbook.md` §3). Owns the fix of a backend defect |
| **Frontend Lead** | Delivery team | Owns the fix of an SPA defect |
| **Security Lead** | Delivery team | **Leads every security incident (P1-4, P2-6)** with AHDA Cybersecurity; the only role that lowers a security incident's tier (classification O-3); audit custodian on a restore |
| **PMO Engagement Lead** | Delivery team | **The one voice to AHDA** for P1 and P2: sends the notices of §6, keeps AHDA's stakeholders informed, holds the contract view (SLA, credits) |
| **AHDA IT service desk** | AHDA | Users' first contact. Logs a user's report and passes it to the primary on-call engineer (§4) |
| **AHDA IT** | AHDA | The GCP tenancy and region, the network, the directory, SSO/MFA, the Exchange relay; project undelete; every region-loss decision (`backup-restore-dr-runbook.md` §3, S-3, S-7) |
| **AHDA Cybersecurity** | AHDA | Leads the response to a security incident under AHDA's cyber-security incident and threat management policy (CS-026-2025); the SIEM; any notification to an authority (F-7) |
| **AHDA Business Sponsor** | AHDA | Accepts the business impact of a P1/P2 and of a decision that trades data for time (an older restore point); approves what is said to users |
| **SMS provider support** | Third party | The provider's own status and support desk, once TASK-103 selects one (UGV-10) |

Roles are named, not people. **The people and their contact routes go in the on-call roster, which is
kept in AHDA's on-call tool or ticketing system, never in this repository**: the repository is public,
and a phone number or a personal address in it would be published (F-2).

### 2.2 Who is engaged at each tier

| | P1 — Critical | P2 — High | P3 — Medium | P4 — Low |
| --- | --- | --- | --- | --- |
| **Paged** | Primary on-call; at once also the DevOps/Platform Lead | Primary on-call; at once also the DevOps/Platform Lead | Primary on-call | Nobody — logged for the next working day |
| **Incident commander** | DevOps/Platform Lead | DevOps/Platform Lead | Primary on-call | Primary on-call |
| **Engaged by the commander** | Backend Lead (always); Frontend Lead if the SPA; Secondary on-call | Backend or Frontend Lead for the area; Secondary on-call if needed | The lead for the area, in working hours | The lead for the area, when planned |
| **Security incident** (P1-4, P2-6, by rule) | Security Lead leads, at once; AHDA Cybersecurity told at once | Security Lead assesses, at once; AHDA Cybersecurity told | — (a security incident is never P3/P4 until the Security Lead lowers it) | — |
| **Tells AHDA** | PMO Engagement Lead → AHDA IT service desk, AHDA IT, AHDA Business Sponsor | PMO Engagement Lead → AHDA IT service desk, AHDA IT | Primary on-call → AHDA IT service desk, through the ticket | Primary on-call → the ticket |
| **AHDA engaged** | AHDA IT (always: tenancy, region, network); AHDA Cybersecurity if security | AHDA IT if the cause is on AHDA's side (directory, Exchange, network) | AHDA IT if the cause is on AHDA's side | — |
| **Post-incident review** | Required (§7) | Required (§7) | When the cause is not yet known or will recur | No |
| **Targets** | TBC — OQ-009 (`incident-severity-classification.md` §4); restore within RTO 4–6 h, RPO 1 h | TBC — OQ-009 | TBC — OQ-009 | TBC — OQ-009 |

## 3. The rotation

| Item | Value |
| --- | --- |
| Shape | **One primary and one secondary on-call engineer at all times within the hours of cover**, from the delivery team's engineers who hold the access in §3.1. The DevOps/Platform Lead and the Security Lead are on call for escalation over the same hours, with a named deputy for leave |
| Hours of cover | **TBC — OQ-009.** 24×7 or AHDA working hours (Sunday to Thursday) is the SLA table's to say. Until it is known, the rotation is planned for AHDA working hours, with the readiness alert also reaching the DevOps/Platform Lead out of hours on a best-effort basis that is not offered to AHDA as a commitment |
| Rotation length | One week, handed over at the start of the first working day (proposed; the DevOps/Platform Lead sets it) |
| Roster | In AHDA's on-call tool or ticketing system (F-3, F-4), with each person's name, role, phone and deputy. Updated before each rotation starts |

**Handover**, at each change of primary:

1. Open incidents: tier, commander, next action, next update due.
2. Alerts that fired since the last handover and were closed as noise, with why.
3. Planned changes in the coming week, and their windows.
4. The incoming primary confirms they can open every item in §3.1 *before* the outgoing primary
   leaves the rota.

### 3.1 What an on-call engineer must be able to open

| Access | For | Granted by |
| --- | --- | --- |
| The on-call channel `pmplatform-<env>-on-call` and the roster | Receiving pages | DevOps/Platform Lead (F-3) |
| GCP IAM in `ahda-pmplatform-prod` (and SIT, UAT) to read Cloud Run, Cloud SQL, Cloud Logging, Cloud Monitoring and run the DR runbook's commands | PB-1, PB-2, PB-6, PB-7 | AHDA IT; DevOps/Platform Lead |
| A path to the database's private IP | `backup-restore-dr-runbook.md` §6 verification — **no such path exists yet** (BR-1) | TASK-021; AHDA Cybersecurity |
| A PROD account holding R01 with MFA | ADM-047 (Integration Monitoring), the notification dead-letter list and redrive (`NOTIFICATION_DELIVERY_MANAGE`) | AHDA IT (directory account); FG-03 assignment |
| The APM | Exceptions and traces by correlation id — **no APM is chosen for PROD yet** (`apm-error-tracking.md` F-2) | DevOps/Platform Lead |
| AHDA's ticketing system | The incident record | AHDA IT (F-4) |

Access from outside the Kingdom is governed by CTL-51: until AHDA Cybersecurity has accepted the
access model for vendor operations staff in writing, on-call engineers work only from where that
model allows (F-8).

## 4. Where incidents come from

| Source | Arrives as | Lands at | Usually |
| --- | --- | --- | --- |
| **Uptime check** on `/health/ready`, every 60 s from three regions | `ahda-pmplatform-<env> readiness failing` (CRITICAL) | On-call channel | P1-1 in PROD; P3-6/P4 elsewhere |
| **FG-05 operational alert** | `ahda-pmplatform-<env> operational alert raised` (WARNING), with type, severity and integration; and an in-app/e-mail notification to the `INTEGRATION_OPERATIONAL_ALERT` family's recipients | On-call channel; ADM-047 | §2.3 of the classification by integration — P1-2, P2-2, P2-3, P3-3, P3-4 |
| **A user** | A call or ticket to the AHDA IT service desk | The service desk passes it to the primary on-call: through the ticket for P3/P4, and **by phone** when the user's description matches a P1 or P2 rule | Any |
| **AHDA Cybersecurity** | A SIEM finding or a report | The Security Lead, by phone | P1-4, P2-6 |
| **The delivery team itself** | A failed deployment, a drill finding, a dependency advisory, a penetration-test finding | The primary on-call | P2-6, P3, P4 |
| **The SMS provider** | A status notice | The primary on-call | P3-3 (PB-5) |

Not yet alerting, so found only by looking (each is a finding): a failed backup or a stale recovery
point (F-5), an error spike in the APM (F-6).

## 5. The procedure

Every incident goes through the same six steps. The commander is the one who makes sure each happens;
for P3/P4 that is the primary on-call engineer throughout.

### 5.1 Steps

| Step | Who | Do | Done when |
| --- | --- | --- | --- |
| **1. Detect and acknowledge** | Primary on-call | Acknowledge the page in the on-call tool, so the escalation timer (§5.3) stops. For a user report, call the reporter back or reply in the ticket to confirm it is being looked at | Page acknowledged; the time recorded |
| **2. Classify** | Primary on-call | Run `incident-severity-classification.md` §2 top to bottom; the first matching rule gives the tier. Unsure between two: the higher (O-2). Anything that could be a breach: P2-6 at least, and call the Security Lead now (O-3) | A tier and the rule that gave it |
| **3. Open the incident record** | Primary on-call | In AHDA's ticketing system (F-4): tier, rule, time detected, time acknowledged, environment, symptom, who is commander. **No secret, token, personal data or record content in it** — correlation ids, error codes and integration codes only | Record number exists |
| **4. Escalate** | Primary on-call | Page whoever §2.2 names for the tier, now, not after a first look. For P1/P2 hand the commander role to the DevOps/Platform Lead when they join, by saying so in the record | Every role §2.2 lists for the tier is engaged or has been paged |
| **5. Communicate** | P1/P2: PMO Engagement Lead. P3/P4: Primary on-call | Send the initial notice (§6.1) to the recipients §2.2 lists; then updates (§6.2) at the interval the SLA table sets (**TBC — OQ-009**; until then at every change of tier, cause or expected restore time); then the resolution notice (§6.3). Use a route that does not depend on the platform: the platform's own e-mail may be what is down | Notice sent; next update time written in the record |
| **6. Mitigate, resolve, close** | Commander | Work the playbook (§5.2). **Restore service first, find the cause second**: a rollback, a redeploy or a restore that brings the service back comes before the root cause. Classify again when the facts change (O-5). Close when the symptom has gone and the rule that matched no longer does; record the time restored and the time closed. Then §7 | Resolution notice sent; record closed with times |

### 5.2 Playbooks

| # | When | Go to |
| --- | --- | --- |
| PB-1 | Platform unavailable — readiness alert (P1-1) | §5.2.1 |
| PB-2 | Data lost or wrong; a store unusable (P1-3), recovery point stale (P2-4) | §5.2.2 |
| PB-3 | Security incident (P1-4, P2-6) | §5.2.3 |
| PB-4 | E-mail not leaving the platform (P2-3) | §5.2.4 |
| PB-5 | **SMS provider failure — fallback to e-mail and in-app** (P3-3; P2-3 if e-mail fails too) | §5.2.5 |
| PB-6 | Any other FG-05 operational alert (P1-2, P2-2, P3-4) | `operational-dashboards.md` §7.2, by alert type; sign-in failures go to AHDA IT for `DIRECTORY`/`SSO`/`MFA` and to the Nafath integration's owner for `NAFATH` |
| PB-7 | A worker stalled — liveness failing (P2-5) | §5.2.6 |
| PB-8 | A release caused it | Roll back by re-promoting the previous release through the pipeline (`cicd-pipeline.md` D-4) — with the PROD approval gate, which is not bypassed in an incident. A release that wrote bad data is PB-2 as well |

#### 5.2.1 PB-1 — the platform is unavailable

1. Ask the three endpoints, from outside and in this order (`health-checks-uptime-monitoring.md` §4):

   ```sh
   for p in /health/live /health/ready /health; do
     printf '%s ' "$p"; curl -s -o /dev/null -w '%{http_code}\n' --max-time 10 "https://<PROD host>$p"
   done
   ```

   | Answer | Means | Next |
   | --- | --- | --- |
   | live 200, ready 503 | The API runs but cannot reach its database within 3 s | Step 2 |
   | live 503 | A worker has stalled; instances are being restarted | PB-7 |
   | No answer, TLS error, 502/504 from the load balancer | The edge, the certificate, DNS or the service itself | Step 3 |

2. **Database.** Read why readiness fails, then the instance's state:

   ```sh
   gcloud logging read 'resource.type="cloud_run_revision" AND textPayload:"Health check postgresql"' \
     --project=ahda-pmplatform-prod --freshness=30m --limit=5
   gcloud sql instances describe pmplatform-prod-db --project=ahda-pmplatform-prod \
     --format='value(state,settings.activationPolicy,gceZone)'
   gcloud sql operations list --instance=pmplatform-prod-db --project=ahda-pmplatform-prod --limit=5
   ```

   `RUNNABLE` and an operation in progress (maintenance, failover): wait for it while updating AHDA, and
   watch readiness return — instances are not restarted for a database outage, so they serve as soon as
   it answers (`health-checks-uptime-monitoring.md` D-2). **Not `RUNNABLE`, deleted, or not recovering:
   this is now P1-3 — go to PB-2 and declare a recovery at once; do not keep debugging an instance
   while the RTO is not running.**

3. **Edge or service.** `gcloud run services describe ahda-pmplatform-prod-api --project=ahda-pmplatform-prod --region=<region>`
   for the serving revision and its conditions; the load balancer and certificate are AHDA IT's to
   check with the DevOps/Platform Lead. A revision deployed in the last hours is PB-8.

4. Readiness 200 from outside again: the alert closes itself 30 minutes after recovery. Close the
   incident on the symptom, not on the alert.

#### 5.2.2 PB-2 — data lost or wrong, store unusable, recovery point stale

- **P1-3:** the DevOps/Platform Lead declares a recovery under `backup-restore-dr-runbook.md` §4 — T0,
  the RTO clock starts — and follows its scenario table S-1 to S-7. RTO 4 to 6 hours and RPO 1 hour are
  the targets. The decision to restore to an older point, losing data, is the Business Sponsor's to
  accept (§2.1). S-7 (region lost) cannot meet the RTO (BR-5): tell AHDA IT at once.
- **P2-4 (recovery point older than 1 hour, nothing lost):**

  ```sh
  gcloud sql instances describe pmplatform-prod-db --project=ahda-pmplatform-prod \
    --format='value(settings.backupConfiguration.enabled,settings.backupConfiguration.pointInTimeRecoveryEnabled)'
  gcloud sql backups list --instance=pmplatform-prod-db --project=ahda-pmplatform-prod --limit=3
  ```

  Turn back on what is off through the environment's Terraform and the deploy action, not by hand, so
  the next apply does not undo it; meanwhile take an on-demand backup
  (`gcloud sql backups create --instance=pmplatform-prod-db --project=ahda-pmplatform-prod`) so a
  recovery point exists while it is fixed. Record the window in which the RPO was not met.

#### 5.2.3 PB-3 — security incident

The Security Lead leads, with AHDA Cybersecurity, under AHDA's policy CS-026-2025 (the policy is a
scanned Arabic document; its steps are not transcribed here and prevail over this list — F-7).

1. **Preserve before you change.** Do not redeploy, restore, delete or rotate anything that is evidence
   until the Security Lead says what to keep: a Cloud SQL on-demand backup or clone, the relevant Cloud
   Logging entries exported, the audit store untouched (it is append-only, CTL-24). Containment that
   cannot wait — cutting traffic with `--ingress=internal` (`backup-restore-dr-runbook.md` §4 step 3),
   disabling an account — is done and recorded with its time.
2. **An exposed secret:** `secret-rotation-runbook.md` §6, emergency rotation — rotate, revoke at the
   source, disable every older version. A secret owned by AHDA IT is rotated by AHDA IT.
3. **A user reaching records outside their scope:** disable the access path — the assignment, the
   account, or the release (PB-8) — then establish from the audit store what was read or changed.
4. **Reporting outward** — to AHDA's leadership, to an authority, to affected people — is AHDA
   Cybersecurity's decision, not the delivery team's (G-6; F-7). The incident record and the review
   hold codes and references; the evidence itself stays where the Security Lead stores it, never in
   this repository or its issues.

#### 5.2.4 PB-4 — e-mail not leaving the platform

1. ADM-047: the `EXCHANGE_EMAIL` instance's latest attempts (ADM-048) and their failure codes
   (`SocketException`, TLS, authentication).
2. In-app notifications still arrive: say so in the notices — no notification is lost, they are in
   every recipient's inbox.
3. The relay is AHDA IT's (Messaging): give them the failure code and the time it began. A credential
   problem is `EXCHANGE_SMTP_PASSWORD`, rotated under `secret-rotation-runbook.md` §7.
4. After recovery, e-mail deliveries still FAILED are retried by the worker; those already DEAD_LETTER
   are redriven by an R01 holder from the dead-letter list (`notification-runtime.md` D-13). Redrive
   the mandatory families (security, critical escalation) first.

#### 5.2.5 PB-5 — SMS provider failure, and the fallback to e-mail and in-app

**How the fallback works today.** WF-15 creates one delivery per recipient *and per channel the
family is routed to*, and sends each on its own (`notification-runtime.md` D-6, D-9). An SMS that
fails does not stop or delay that notification's in-app and e-mail deliveries: they were created
alongside it. So the fallback is **not a re-route at failure time; it is that every family routed to
SMS is also routed to in-app and e-mail**. That holds only if the NOTIFICATION_ROUTING version in force
says so: nothing in the code requires it (F-9). A recipient cannot turn in-app off, and a mandatory
family's e-mail keeps the routing's setting whatever the recipient chose (D-11), so for a family routed
to all three the fallback reaches the person.

**What a failure looks like.** Each SMS attempt that fails leaves its delivery FAILED and retried at 1,
2, 4 and 8 minutes; the fifth failure dead-letters it, about 15 minutes after the first
(`notification-runtime.md` D-13, F-5). Every attempt is reported to FG-05 as `SMS`
(`IntegrationCodes.Sms`), which raises AVAILABILITY (CRITICAL) on the first failure — or after the
delay `AvailabilityAlertDelays:SMS`, once AHDA sets one (AT-1) — and DEAD_LETTER, which every dead letter
raises (AT-7). Both reach the on-call channel.

1. **Classify.** E-mail delivering (no AVAILABILITY or DEAD_LETTER on `EXCHANGE_EMAIL`): **P3-3**. E-mail
   failing too: **P2-3**, and work PB-4 first — e-mail is the channel that reaches people outside the
   platform.
2. **Confirm the fallback reached people.** For a dead-lettered SMS, open its notification's other
   deliveries in the delivery list (`NOTIFICATION_DELIVERY_VIEW`): in-app SENT or READ, e-mail SENT. A
   notification whose family has **no** in-app or e-mail delivery is a routing configuration defect
   (F-9): raise it with the FG-04 configuration owner as P3, and list which families it affects.
3. **Ask the provider.** Their status page and support desk (contact in the roster, once TASK-103
   selects one). Record their reference in the incident.
4. **Tell AHDA** through the ticket (P3): SMS is not being delivered; in-app and e-mail carry every
   notification; no action is needed from users.
5. **Prolonged outage.** Nothing needs switching off: in-app and e-mail keep delivering, SMS keeps
   failing into dead letters, and FG-05 raises one AVAILABILITY alert per outage. If the provider
   reports an outage that will last days, the routing can be republished without SMS — a governed
   change, authored, validated and published by three people (`notification-runtime.md` D-8) — and
   republished with it afterwards.
6. **After recovery.** The next SMS attempt that succeeds sets `SMS` available again. **Do not redrive
   the outage's SMS dead letters by default**: the notification already reached the person in-app and
   by e-mail, and an SMS hours late adds nothing. Redrive one only where that recipient's e-mail for the
   same notification also failed.

Until TASK-103 selects a provider, every SMS delivery is SUPPRESSED `CHANNEL_NOT_CONFIGURED` and never
attempted (`notification-runtime.md` F-2): no SMS alert can fire, and this playbook has not been
exercised (F-10).

#### 5.2.6 PB-7 — a worker stalled

`/health/live` 503 names the worker in the log (`Health check background-workers`): one pass has run
longer than `HealthChecks:WorkerPassTimeout` (2 h). Cloud Run restarts the instance after three failed
probes, which ends the pass. Repeated restarts mean a pass that never completes — usually an outbound
call that never answers (an SMTP relay that accepts and never replies is the known case,
`health-checks-uptime-monitoring.md` D-3). Find the dependency in the APM or the log, and work that
integration's playbook.

### 5.3 Escalation path and timers

```
page ──► Primary on-call ──(no acknowledgement)──► Secondary on-call ──(no acknowledgement)──► DevOps/Platform Lead
              │
              └─ P1/P2 ──► DevOps/Platform Lead (commander) ──► Backend / Frontend Lead
                         │                                  └─► AHDA IT (tenancy, network, directory, relay)
                         ├─ security ──► Security Lead ──► AHDA Cybersecurity
                         └─► PMO Engagement Lead ──► AHDA IT service desk, AHDA Business Sponsor
```

The acknowledgement and response targets are the SLA table's (**TBC — OQ-009**). So that a page is
never left unanswered meanwhile, these **interim internal timers** apply. They are the delivery team's
working values for moving a page on to the next person; they are not response targets offered to AHDA,
and they are replaced by the SLA table's figures when it arrives (`incident-severity-classification.md`
§6).

| # | Page not acknowledged within | P1 | P2 | P3 | Then |
| --- | --- | --- | --- | --- | --- |
| IT-1 | by the primary | 15 min | 30 min | next working day | Page the secondary |
| IT-2 | by the secondary | 15 min | 30 min | — | Call the DevOps/Platform Lead |

The DevOps/Platform Lead escalates to AHDA IT whenever the cause, or the next action, is in AHDA's
tenancy, network, directory or relay — at the moment that is known, not after a timer.

## 6. Notices

Plain text, sent by e-mail from the sender's own mailbox (not through the platform) and, for P1, also
by phone. No secret, personal data or record content; correlation ids and codes only.

### 6.1 Initial notice

```
Subject: [P<n>] PM Platform <PROD|UAT|SIT> — <symptom in five words> — incident <record no.>

Detected:   <time, AST>       Classified: P<n> under rule <P1-1 …>
Impact:     <who cannot do what — e.g. "no user can open the platform">
Started:    <time, AST, or "unknown">
Commander:  <role> — contact through the on-call roster
Doing now:  <one line>
Next update: <time, AST>
```

### 6.2 Update

```
Subject: [P<n>] PM Platform — update <k> — incident <record no.>

Status:     <investigating | cause found | mitigating | restored, monitoring>
Since last: <what changed>
Tier:       P<n> <unchanged | changed from P<m> under rule …>
Expected restore: <time, AST, or "not yet known">
Next update: <time, AST>
```

### 6.3 Resolution

```
Subject: [P<n>] PM Platform — resolved — incident <record no.>

Restored:   <time, AST>       Duration: <h:mm>
Cause:      <one line, or "under review">
Data:       <"no data lost" | "restored to <time>; changes after it must be re-entered">
Follow-up:  <post-incident review due — §7>
```

## 7. After the incident

**A post-incident review is written for every P1 and P2**, and for a P3 whose cause is unknown or
will recur. It is blameless and holds: the timeline from the incident record (detected, acknowledged,
classified, escalated, restored, closed); the tier and every reclassification with its rule; the cause;
what restored service; what the response did well and where it waited; the follow-up actions, each with
an owner and a task ID.

It is presented to AHDA at the **next monthly periodic visit** (*Client Original Project Scope* §11),
and its outcome is included in the periodic technical performance report. The deadline for writing it
is the SLA table's if it sets one (**TBC — OQ-009**).

Where it goes: AHDA's ticketing system with the incident record. A follow-up that changes code is a
GitHub issue or task that describes the fix, never the incident's evidence — the repository is public.
A gap found in this runbook or in the classification is fixed in the document, with a change-log
entry.

## 8. The sample P1 incident, walked through

The workbook's validation check: "Walk a sample P1 incident scenario through the runbook and confirm
every step (detect, classify, escalate, communicate) is unambiguous and actionable without further
clarification." Walked at the desk on 2026-10-23, against this document and the documents it links. No
environment exists, so no command was run against PROD.

**Scenario.** Sunday, 09:12 AST. A Cloud SQL maintenance operation leaves the PROD instance
`pmplatform-prod-db` failing to start. `/health/ready` answers 503; users get the platform's error page;
the service desk begins receiving calls.

| # | Step | What the runbook says to do | Who | Actionable without asking? |
| --- | --- | --- | --- | --- |
| 1 | Detect | The uptime check fails from two regions; after 60 s the readiness alert `ahda-pmplatform-prod readiness failing` posts to `pmplatform-prod-on-call` (§4) — about 09:14 by TASK-091's local measurement | — | Yes. **Depends on F-3**: the channel must be one the on-call engineer's tool shows as a page |
| 2 | Acknowledge | Acknowledge in the on-call tool; IT-1 (15 min for P1) stops (§5.1 step 1, §5.3) | Primary on-call | Yes, once a roster exists (**F-2**) |
| 3 | Classify | §2.3 top to bottom: P1-1 "the PROD readiness alert is open" matches → **P1** | Primary on-call | Yes. Matches sample S-01 of the classification |
| 4 | Record | Open the incident in AHDA's ticketing system: P1, P1-1, 09:14 detected, 09:16 acknowledged, PROD, "readiness 503", commander: primary until the DevOps/Platform Lead joins (§5.1 step 3) | Primary on-call | Yes, once the system is named (**F-4**) |
| 5 | Escalate | §2.2 P1 column: page the DevOps/Platform Lead (commander) now; the Lead engages the Backend Lead and the secondary; no security indicator, so not the Security Lead (§5.1 step 4) | Primary on-call → DevOps/Platform Lead | Yes |
| 6 | Communicate | PMO Engagement Lead sends §6.1 to the AHDA IT service desk, AHDA IT and the Business Sponsor, by e-mail from their own mailbox and by phone; next update at each change of status (interval TBC — OQ-009) (§5.1 step 5) | PMO Engagement Lead | Yes. The update interval is TBC, and the interim rule (at each change of tier, cause or restore time) is stated |
| 7 | Diagnose | PB-1 step 1: live 200, ready 503 → database. Step 2: the log says `PostgreSQL unreachable`; the instance is not `RUNNABLE` and its last operation failed | Primary on-call, Backend Lead | Yes. **Depends on §3.1 access** being granted before the rota starts |
| 8 | Decide | PB-1 step 2: not recovering → P1-3, PB-2. The DevOps/Platform Lead declares a recovery: T0 = the declaration, RTO clock running (`backup-restore-dr-runbook.md` §4, S-2: instance unusable, still listed → §5.2 from its newest backup, or §5.1 PITR) | DevOps/Platform Lead | Yes. **F-11**: whether AHDA's RTO counts from the declaration or from 09:12 is unconfirmed; the walk records both times |
| 9 | Mitigate | Freeze writes, restore into a new instance by PITR (RPO 1 h), verify by §6.1 of the DR runbook, cut over (§5.7), reopen writes, stop the clock | DevOps/Platform Lead, Backend Lead, Security Lead (audit store) | Yes. **BR-1**: the §6.1 verification needs a path to the private IP that does not exist yet |
| 10 | Resolve | Readiness 200 from outside; §6.3 sent with "no data lost" or the restore time; record closed with times | DevOps/Platform Lead, PMO Engagement Lead | Yes |
| 11 | Review | §7: written for P1; presented at the next monthly visit | DevOps/Platform Lead | Yes |

**Result.** Every step names who acts, what they do and when it is done; no step needed a question
answered to proceed. The walk found five things the runbook depends on that do not exist yet — a
staffed roster (F-2), a named on-call tool (F-3), a ticketing system (F-4), the on-call access of §3.1
including the database path (BR-1), and AHDA's reading of when the RTO starts (F-11). They are
preconditions of the Release Checklist row (§9), not ambiguities in the steps.

The walk is owed again **live** on SIT, combined with the database-outage drill
(`infra/environments/drill-database-outage.sh`, `health-checks-uptime-monitoring.md` §7.3), once SIT
exists and the roster is staffed: page to acknowledgement to notice, timed.

## 9. Readiness — Release Checklist "Operational Runbooks & On-Call Ready"

The gate's evidence: "On-call runbook, incident-severity classification and operational dashboards are
published and the on-call rotation is staffed"; its note: "SLA figures marked TBC until AHDA confirms
the contractual SLA table".

| # | Item | State |
| --- | --- | --- |
| RC-1 | On-call runbook published | **Done** — this document |
| RC-2 | Incident-severity classification published | **Done** — `incident-severity-classification.md` |
| RC-3 | Operational dashboards published | **Built** — OD-1 and OD-2 (TASK-092); not applied to an environment (`operational-dashboards.md` F-1) |
| RC-4 | SLA figures marked TBC | **Done** — `incident-severity-classification.md` §4; §5.3 here |
| RC-5 | On-call rotation staffed: roster with names, deputies and contacts in AHDA's tool | **Open** — F-2 |
| RC-6 | On-call channel created and reaching the roster's tool | **Open** — F-3; `health-checks-uptime-monitoring.md` F-4 |
| RC-7 | Every rostered engineer holds §3.1's access | **Open** — after RC-5; BR-1 |
| RC-8 | Live P1 walk on SIT (§8) | **Open** — needs SIT and RC-5 to RC-7 |

The gate is **not met** until RC-5 to RC-8 are.

## 10. Findings

| # | Finding | Owner | Status |
| --- | --- | --- | --- |
| F-1 | **The contractual SLA table is not available (OQ-009 / UGV-06)**: response, update and resolution times, hours of cover, service credits, post-incident review deadline. Marked TBC wherever they would appear; `incident-severity-classification.md` §6 is the procedure for when it arrives | AHDA Contracts / PMO Engagement Lead | Open — gate Before Production |
| F-2 | **Nobody is on the rota.** Roles are named; the people, their deputies and their contact routes are not, and belong in AHDA's on-call tool, not in this public repository. Same as `backup-restore-dr-runbook.md` BR-6 | DevOps/Platform Lead; PMO | Open — gate Before Production |
| F-3 | **The on-call tool is not named**, so the channel type is not decided (`health-checks-uptime-monitoring.md` F-4: a `webhook_tokenauth` channel posts Cloud Monitoring's incident JSON, which Teams or Slack incoming webhooks do not render; PagerDuty and Google Chat have native channel types). A page that arrives as unreadable JSON is a page nobody acts on | DevOps/Platform Lead, with AHDA IT | Open |
| F-4 | **No ticketing system is named for the incident record.** FG-05 leaves ITSM integration out of scope. Until AHDA names one, the record is kept in a system AHDA and the delivery team can both read and that is not public — not this repository, not GitHub issues | AHDA IT | Open |
| F-5 | **No alert watches backups or the recovery point.** A disabled PITR or a failed backup run is found only by looking (P2-4). Recommended: a Cloud Monitoring alert policy in `infra/terraform/monitoring` on Cloud SQL's backup-run failures, routed to the on-call channel | DevOps/Platform Lead | Open |
| F-6 | **No alert rule on the APM**, and no APM chosen for PROD (`apm-error-tracking.md` F-2). An error spike that does not take readiness down is seen only by a user's report | DevOps/Platform Lead | Open |
| F-7 | **The security incident procedure rests on AHDA's CS-026-2025**, which is a scanned Arabic PDF in *Project Files* that could not be transcribed for this record; PB-3 states the delivery team's part and defers to it. Whether and to whom a breach is notified is AHDA's (G-6; ADR-001 R-4) | AHDA Cybersecurity; Security Lead | Open |
| F-8 | **On-call from outside the Kingdom is not yet accepted** (CTL-51; ADR-001 C-9). Until AHDA Cybersecurity accepts the vendor access model in writing, the rota must be staffed by engineers who can work from where the model allows | Security Lead; AHDA Cybersecurity | Open |
| F-9 | **The SMS fallback is a configuration property, not a code guarantee.** A NOTIFICATION_ROUTING version may route a family to SMS without in-app or e-mail, and nothing refuses it. Rule for the routing's authors: every family routed to SMS is routed to IN_APP and EMAIL too. A validation in FG-04's routing content would make it a guarantee | FG-04 configuration owner; Engineering (FG-04 validation) | Open |
| F-10 | **PB-5 is unexercised.** No SMS provider is selected (TASK-103; OQ-012 / UGV-10): every SMS is SUPPRESSED `CHANNEL_NOT_CONFIGURED`, so no SMS alert can fire. When TASK-103 lands, run PB-5 on SIT with the provider's sandbox made unreachable, and add the provider's support contact to the roster | TASK-103 | Open |
| F-11 | **When the RTO starts is unconfirmed.** The DR runbook starts the clock at the declaration of a recovery (§2 there); AHDA IT's business continuity plan may count from the start of the disruption. The incident record keeps both times so either reading can be reported | AHDA IT | Open |

## 11. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-10-23 | Initial record: support model, 12 roles and who is engaged at each tier, the rotation and its access, sources of incidents, the six-step procedure, eight playbooks including SMS provider failure and its fallback (ADR-004), the escalation path with interim internal timers, three notices, post-incident review, the sample P1 walk-through, the release-gate readiness list, 11 findings. | Observability (TASK-093) |

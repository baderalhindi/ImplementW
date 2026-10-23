# Incident severity classification

| Field | Value |
| --- | --- |
| Task | TASK-093 — Document Operational SLAs & On-Call Runbook (P16 - Observability) |
| Depends on | TASK-091 — health checks and uptime monitoring ([`health-checks-uptime-monitoring.md`](../architecture/health-checks-uptime-monitoring.md)); TASK-023 — backup, restore and DR ([`backup-restore-dr-runbook.md`](backup-restore-dr-runbook.md)). Also read: TASK-092 FG-05 alerts ([`operational-dashboards.md`](operational-dashboards.md)) |
| Companion | [`on-call-runbook.md`](on-call-runbook.md) — who is paged for each tier, and what they do |
| Record date | 2026-10-23 |
| Status | **WRITTEN — WORKING DEFINITIONS, NOT YET THE CONTRACT'S.** OQ-009 says the contractual SLA table covers "response times, severity definitions, service credits", and the delivery team has not received it. These definitions are the delivery team's until it arrives; then the contract's prevail and §6 records how these map to them |
| Fixed inputs | **RPO 1 hour, RTO 4 to 6 hours** (ADR-001; PTBC-048; AHDA gate 19 Sep 2026; TASK-093 Gate Decision Applied) |
| Open | **Every response, update and resolution time, the hours of cover and the service credits: TBC — OQ-009 / UGV-06**, owner AHDA Contracts / PMO Engagement Lead, gate Before Production |
| Control | CTL-34 (incident management); G-6 (security incident response, `cybersecurity-control-matrix.md` §9) |
| Sources | The TASK-093 row of `AHDA_RPMO_Platform_Implementation_Plan_v2.xlsx` (description, acceptance criteria, validation check, Gate Decision Applied), its Participation Amendment (ADR-004), Open Questions OQ-003 and OQ-009, Release Checklist row "Operational Runbooks & On-Call Ready"; *Client Original Project Scope* §11 and §13; the Development and Cybersecurity Guide §14 and §15 ("Operations"); `unassigned-gated-values.csv` UGV-06 and UGV-10 |

## 1. What this is for

A person who has just been told something is wrong uses §2 to give it one severity, P1 to P4. The
severity decides who is paged and who leads ([`on-call-runbook.md`](on-call-runbook.md) §2) and which
response targets apply (§4).

The rules are written so that two people given the same facts reach the same tier: each names
something the on-call engineer can observe — an alert by its name, a health endpoint's answer, a
scenario of the DR runbook, a check they can repeat with their own account — not a judgement such as
"major" or "significant". §5 is a set of sample incidents with the answer each must give.

`P1`–`P4` here are incident severities. They are not the workbook's task priorities (`P0`, `P1` in the
Implementation Plan's Priority column), and not the severities Cloud Monitoring or FG-05 put on an alert
(`CRITICAL`, `WARNING`): those are inputs to §2, never the answer (rule O-6).

## 2. How to classify

Work down the table. **The first rule that matches gives the severity**; do not read further.

### 2.1 Terms

| Term | Meaning |
| --- | --- |
| PROD | The production environment, `ahda-pmplatform-prod`. SIT, UAT and DEV are non-PROD |
| Core function | One of C-1 to C-8 (§2.2) |
| Fails for everyone | The function fails at every attempt over **CP-1** (§3) for the on-call engineer's own account **and** for at least one other user in a different role (a second report, or a second account the engineer holds), no attempt in that time succeeds, and no other route reaches the same result. For a group the engineer cannot join (external entity users), two users of that group in place of the engineer's account |
| Readiness alert | Cloud Monitoring's `ahda-pmplatform-<env> readiness failing` (CRITICAL): `/health/ready` failing from 2 of 3 checker regions for 60 s (`health-checks-uptime-monitoring.md` D-7) |
| Operational alert | An FG-05 alert — AVAILABILITY, FRESHNESS, QUEUE_BACKLOG, DEAD_LETTER, RECONCILIATION_MISMATCH, DELIVERY_RATE — on an integration code (`DIRECTORY`, `SSO`, `MFA`, `NAFATH`, `EXCHANGE_EMAIL`, `SMS`), shown on ADM-047 and sent to the on-call channel as `ahda-pmplatform-<env> operational alert raised` (`operational-dashboards.md` D-8, D-9) |
| Recovery point | The newest point the database can be restored to: the end of the point-in-time-recovery log, or the newest automated backup (`backup-restore-dr-runbook.md` §1) |
| Security evidence | An audit record, log line, alert or finding that shows access, change or disclosure actually happened, or a reproduction of it on the release PROD runs, in any environment — as opposed to a report that it might have |

### 2.2 Core functions

The functions whose loss stops AHDA's project control. Everything else is non-core. **Proposed by the
delivery team; AHDA confirms or amends the list with the SLA table (F-2).**

| # | Core function | Where |
| --- | --- | --- |
| C-1 | Sign in (internal users through the directory; external entity users through Nafath) | `POST /api/v1/sessions`; FG-03 |
| C-2 | Find and open a project record | Projects register; WF-01 |
| C-3 | Submit a progress update | WF-02 |
| C-4 | Decide an approval task | WF-11 |
| C-5 | Upload and download a document | WF-12 |
| C-6 | Open a dashboard or run a report | FG-01, FG-02 |
| C-7 | Receive notifications in-app and by e-mail | WF-15 |
| C-8 | An external entity submits its update | WF-13 |

### 2.3 The rules

**P1 — Critical.** PROD, and any of:

| Rule | Condition | How it is observed |
| --- | --- | --- |
| P1-1 | **The platform is unavailable** | The PROD readiness alert is open; or the platform's address gives no page, or 5xx for every request, over CP-1 |
| P1-2 | **No internal user can sign in** | C-1 fails for everyone signing in through the directory — usually with an open AVAILABILITY alert on `DIRECTORY`, `SSO` or `MFA` |
| P1-3 | **Data is lost or wrong, or a store is unusable** | One of the DR runbook's scenarios S-1 to S-7 applies, whether it is then fixed by a restore or by a corrective change (`backup-restore-dr-runbook.md` §4 step 2): bad data written by a release or a bulk mistake, an instance unusable or deleted, documents lost, a zone or region down |
| P1-4 | **A security breach with evidence** | Security evidence that someone read, changed or took PROD data they are not entitled to; that a user can open or change a record outside their scope; that a PROD secret is outside the secret store; that the audit chain fails verification; or that a file served by the document store is malicious |

**P2 — High.** PROD, no P1 rule matches, and any of:

| Rule | Condition | How it is observed |
| --- | --- | --- |
| P2-1 | **A core function fails for everyone**, while sign-in works | One of C-2 to C-8 fails for everyone |
| P2-2 | **One group of users cannot sign in** | C-1 fails for everyone in one group — external entity users (`NAFATH` AVAILABILITY), or every user of one department or entity — while others sign in |
| P2-3 | **No notification leaves the platform by e-mail, or none is created in-app** | An AVAILABILITY alert open, or a DEAD_LETTER raised, on `EXCHANGE_EMAIL`; or notification intents stay RECEIVED and nothing new reaches an inbox over CP-1 (`GET /notification-intents`). E-mail is the only channel that reaches a person outside the platform while no SMS provider exists (UGV-10) |
| P2-4 | **The RPO is breached** | The recovery point is older than **1 hour** (RPO), and nothing has been lost yet; once data is lost it is P1-3 |
| P2-5 | **A background worker has stalled** | `/health/live` answers 503 naming a worker, or Cloud Run restarts instances on liveness two or more times within CP-1 (`health-checks-uptime-monitoring.md` D-3) |
| P2-6 | **A security report without evidence yet** | A report that PROD data may have been accessed, changed or exposed, before the Security Lead has assessed it (rule O-3); or an exploitable weakness found in what PROD runs — a Critical penetration-test finding, a dependency advisory with a known exploit |

**P3 — Medium.** No P1 or P2 rule matches, and any of:

| Rule | Condition | How it is observed |
| --- | --- | --- |
| P3-1 | **A core function fails for some users, some records, or with a workaround** | It fails for some users only (one role, one project, one browser) while others succeed; or it fails and another route reaches the same result |
| P3-2 | **A non-core function fails for everyone** in PROD | Administration, configuration, imports, audit views, exports |
| P3-3 | **The SMS provider fails while e-mail and in-app deliver** | An AVAILABILITY alert or a DEAD_LETTER on `SMS`, with no P2-3 condition. The same notification reached the recipient in-app and by e-mail ([`on-call-runbook.md`](on-call-runbook.md) PB-5) |
| P3-4 | **An operational alert in PROD that no rule above names** | FRESHNESS, QUEUE_BACKLOG, RECONCILIATION_MISMATCH, DELIVERY_RATE or DEAD_LETTER on any integration; AVAILABILITY on an integration whose loss matches no P1 or P2 rule |
| P3-5 | **PROD is slow but working** | Functions complete; OD-2's latency panel is above the threshold PTBC-043 will set (**TBC**). Until it is set: users report slowness and the on-call engineer sees it with their own account |
| P3-6 | **A non-PROD environment blocks scheduled work** | SIT or UAT is down or a core function fails there during a scheduled UAT session, a drill, or a release being promoted |

**P4 — Low.** Anything else: a defect in text, layout or translation; one user's problem with a
workaround; a question; a documentation error; a non-PROD problem that blocks nothing scheduled.

### 2.4 Override rules

| # | Rule |
| --- | --- |
| O-1 | **First match wins**, in the order P1-1 … P3-6. A condition that matches two rules takes the higher |
| O-2 | **When unsure between two tiers, take the higher.** Lower it only by running §2 again with the missing fact found, and record that fact and the time in the incident record. A security incident is lowered only under O-3 |
| O-3 | **A security report is P2 (P2-6) until the Security Lead assesses it.** With security evidence it becomes P1 (P1-4); ruled out, it is closed or classified again on what remains. Nobody but the Security Lead lowers it |
| O-4 | **Non-PROD is never above P3**, except a security incident that involves PROD data, a PROD credential, or a weakness in the release PROD runs: that is classified as PROD wherever it was found (a PROD secret pasted into a SIT ticket is P1-4) |
| O-5 | **Classify again whenever the facts change**, and record the new tier, the rule and the time. An incident's tier is the highest it has held for reporting; its current tier drives who is engaged |
| O-6 | **An alert's own severity is not the incident's.** Cloud Monitoring's CRITICAL/WARNING and FG-05's severities are facts used in the rules, not answers |
| O-7 | **Planned work is not an incident** while it runs inside an agreed maintenance window (windows: **TBC**, F-6). The same symptom outside the window, or past its end, is classified by §2 |
| O-8 | **A failure that cleared by itself within CP-1** and is not open (no alert open, the function works again) is P4: recorded, and reviewed with the deployment or change that caused it, if any |

## 3. Classification parameters

These are part of the definitions, not service levels: they decide *which* tier an incident is in, not
how fast anyone must act. They are the delivery team's, set by the DevOps/Platform Lead, and change
only with an entry in §8.

| # | Parameter | Value | Why |
| --- | --- | --- | --- |
| CP-1 | How long a failure must persist to count as "fails for everyone" | **10 minutes** | Longer than one retry cycle of the notification worker (1, 2, 4 min back-off) and than a Cloud Run revision start, so a deployment's few seconds of errors do not page anyone; short enough to sit well inside the 4-hour lower bound of the RTO |
| CP-2 | RPO used by P2-4 | **1 hour** | ADR-001 (fixed input) |

## 4. Response targets per tier

Every figure an SLA would hold is **TBC** until AHDA confirms the contractual SLA table (OQ-009 /
UGV-06). The *Client Original Project Scope* §11 states that the RFP booklet "defines service level
agreements for response and fault-resolution times by priority"; that table has not reached the
delivery team, so none of its values is written here and none is guessed.

| Target | P1 | P2 | P3 | P4 | Source |
| --- | --- | --- | --- | --- | --- |
| Hours of cover (24×7, or AHDA working hours) | **TBC** | **TBC** | **TBC** | **TBC** | OQ-009 |
| Acknowledge (on-call engineer accepts the page) | **TBC** | **TBC** | **TBC** | **TBC** | OQ-009 |
| Respond (engineer working on it; AHDA told) | **TBC** | **TBC** | **TBC** | **TBC** | OQ-009 |
| Status update to AHDA, every | **TBC** | **TBC** | **TBC** | **TBC** | OQ-009 |
| Workaround or service restored | **TBC** | **TBC** | **TBC** | **TBC** | OQ-009 |
| Permanent fix | **TBC** | **TBC** | **TBC** | **TBC** | OQ-009 |
| Service credit if missed | **TBC** | **TBC** | **TBC** | **TBC** | OQ-009 |
| Remediation of a security finding during operations | **TBC** | **TBC** | **TBC** | **TBC** | OQ-009; G-5 |
| **Restore of a lost or corrupted store (P1-3)** | **RTO 4 to 6 hours** | — | — | — | ADR-001, fixed |
| **Maximum data loss on restore (P1-3)** | **RPO 1 hour** | — | — | — | ADR-001, fixed |

What is known, and is not a service level:

| Fact | Value | Source |
| --- | --- | --- |
| Configured detection of a PROD outage (P1-1): failure to alert reaching the on-call channel | ≤ 5 minutes (configuration accounts for at most 130 s; Cloud Monitoring's own delay is unmeasured) | `health-checks-uptime-monitoring.md` D-7, F-3 |
| Configured detection of an operational alert: threshold crossed to on-call channel and recipients | ≤ 5 minutes | `operational-dashboards.md` D-10 |
| Drill pass mark for a restore | ≤ 4 h passes; 4–6 h is a finding; > 6 h fails | `backup-restore-dr-runbook.md` §2 |

The on-call runbook's interim escalation timers (`on-call-runbook.md` §5.3) are the delivery team's
internal working values for paging the next person. They are not offered to AHDA as response targets
and are replaced by whatever the SLA table sets.

## 5. Sample incidents — the test of these definitions

Each row is an incident description and the answer §2 must give. Give the description alone to someone
who has read §2 and compare. A different answer means a rule is ambiguous, and the rule is fixed — not
the row.

| # | Description | Tier | Deciding rule | Why not higher |
| --- | --- | --- | --- | --- |
| S-01 | 09:12 Sunday. The readiness alert for PROD fires. The service desk reports users see an error page | **P1** | P1-1 | — |
| S-02 | PROD readiness alert is not open, but sign-in returns `500` for every internal user for 15 minutes; `DIRECTORY` AVAILABILITY alert is open | **P1** | P1-2 | — |
| S-03 | A migration in last night's release overwrote the planned end date of every milestone in PROD with the actual date | **P1** | P1-3 (S-1, bad data) | — |
| S-04 | An entity Project Manager reports seeing another entity's project in their register; the on-call engineer reproduces it with the user's role in UAT on the same release | **P1** | P1-4 (a user can open a record outside their scope) | — |
| S-05 | A user e-mails the service desk: "I think someone logged in as me yesterday" | **P2** | P2-6 by O-3 | No security evidence yet; the Security Lead assesses |
| S-06 | Document download returns `503` for every user and every document for 20 minutes; everything else works | **P2** | P2-1 (C-5) | Platform is available, no data lost |
| S-07 | `NAFATH` AVAILABILITY alert open; no external entity user can sign in; internal users can | **P2** | P2-2 | P1-2 is internal users only |
| S-08 | `EXCHANGE_EMAIL` AVAILABILITY alert open in PROD; in-app notifications arrive | **P2** | P2-3 | Platform works; notifications are degraded, not lost (in-app holds them) |
| S-09 | Cloud SQL shows the newest automated backup is 26 hours old and point-in-time recovery is off | **P2** | P2-4 | Nothing lost yet; restore not needed |
| S-10 | `SMS` AVAILABILITY alert open and SMS deliveries are dead-lettering; e-mail and in-app deliveries for the same notifications are SENT | **P3** | P3-3 | E-mail works, so P2-3 does not match |
| S-11 | `SMS` and `EXCHANGE_EMAIL` AVAILABILITY alerts both open | **P2** | P2-3 | — (P2-3 is checked before P3-3) |
| S-12 | Progress update submission fails for one project only with `409`; other projects submit | **P3** | P3-1 | Not everyone |
| S-13 | The legacy data import page fails for everyone in PROD | **P3** | P3-2 | Import is not a core function |
| S-14 | A QUEUE_BACKLOG alert on `DIRECTORY` in PROD; sign-in works | **P3** | P3-4 | Sign-in works, so no P1-2/P2-2 |
| S-15 | UAT is down at 10:00 during a scheduled UAT session with AHDA testers | **P3** | P3-6 | Non-PROD: never above P3 (O-4) |
| S-16 | SIT is down on a Thursday evening; nothing is scheduled on it | **P4** | — | Non-PROD, blocks nothing scheduled |
| S-17 | A PROD service-account key is found pasted in a SIT defect ticket | **P1** | P1-4 by O-4 | — |
| S-18 | An Arabic label on the KPI register is truncated on mobile | **P4** | — | Cosmetic |
| S-19 | During an agreed maintenance window, PROD is unavailable as planned | **No incident** | O-7 | — |
| S-20 | The same maintenance overruns its window by 30 minutes and PROD is still unavailable | **P1** | P1-1 (O-7: past the window) | — |
| S-21 | Five minutes after a PROD deployment, sign-in failed for everyone for about 40 seconds, then recovered by itself; no alert is open | **P4** | O-8 | Shorter than CP-1, so not "fails for everyone" |

## 6. When AHDA's SLA table arrives

1. Copy its severity definitions and targets into §4, replacing each **TBC** with the value and the
   contract reference, and record the change in §8.
2. Map each of its tiers to P1–P4 here. Where its definitions differ, **the contract's prevail**: change
   the rules in §2.3 to say what the contract says, keeping each rule observable, then re-run §5 and
   correct any row the change moves.
3. If the contract's core functions differ from §2.2, replace §2.2.
4. Update `on-call-runbook.md` §3 (hours of cover) and §5.3 (replace the interim timers).
5. Close F-1 here and UGV-06 in `docs/governance/unassigned-gated-values.csv`.

## 7. Findings

| # | Finding | Owner | Status |
| --- | --- | --- | --- |
| F-1 | **The contractual SLA table is not available (OQ-009 / UGV-06).** Every response, update and resolution time, the hours of cover and the service credits are TBC; the severity definitions here are working definitions until §6 is done | AHDA Contracts / PMO Engagement Lead | Open — gate Before Production |
| F-2 | **The core-function list (§2.2) is the delivery team's proposal.** It decides the line between P2 and P3 | AHDA Business Sponsor, with the SLA table | Open |
| F-3 | **P3-5 has no latency threshold.** PTBC-043 (performance NFRs) is open; the load-test baseline (150 named users, 25 concurrent, 50 at peak) is the delivery team's position, not AHDA's | AHDA IT (PTBC-043) | Open |
| F-4 | **P2-4 is detected only by looking.** No alert policy watches the recovery point or a failed backup; the RPO can be breached unnoticed until someone checks Cloud SQL (`on-call-runbook.md` F-5) | DevOps/Platform Lead | Open |
| F-5 | **What counts as a reportable security breach, and to whom, is AHDA's** (G-6; ADR-001 R-4). P1-4 and P2-6 classify; they do not decide notification to any authority | AHDA Cybersecurity | Open |
| F-6 | **No maintenance windows are agreed** (Development and Cybersecurity Guide §15, "Operations"). Until they are, any planned PROD outage needs AHDA's change approval with its own window, and O-7 applies only to that window | AHDA IT; DevOps/Platform Lead | Open |

## 8. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-10-23 | Initial record: four tiers, 16 rules (P1-1 to P3-6) and the P4 remainder, 7 overrides, 2 classification parameters, response targets TBC on OQ-009 with RPO/RTO fixed, 20 sample incidents. |
| 2026-10-23 | Blind test: a reader given §2 to §4 without §5 classified the 20 samples and 8 new incidents; all 28 tiers matched. Wording it found open to two readings tightened: "fails for everyone" excludes a workaround and covers groups the engineer cannot join; security evidence includes a reproduction on the PROD release; P1-3 covers a store fixed without a restore; P2-4 and P2-5 given cut-offs; O-2 defers to O-3; O-4 covers a weakness in the PROD release; O-8 added for a failure that cleared itself within CP-1, with sample S-21. | Observability (TASK-093) | Observability (TASK-093) |

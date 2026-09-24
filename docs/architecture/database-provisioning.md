# Managed PostgreSQL — encryption, backup and point-in-time recovery

| Field | Value |
| --- | --- |
| Task | TASK-020 — Provision Managed PostgreSQL with Backup & Encryption (P3 - Infrastructure & DevOps). The workbook titles it "AlloyDB"; ADR-002 §4.2.3 settled the product as Cloud SQL for PostgreSQL and listed the title as one of six cells to correct (ADR-002 §10, ADR-001 §10.1) |
| Depends on | TASK-017 — Infrastructure as Code (`docs/architecture/infrastructure-as-code.md`, BUILT — NOT APPLIED) |
| Record date | 2026-09-24 |
| Status | **BUILT — NOT APPLIED.** The module, its guards and both verification paths are written, formatted, schema-validated against the provider, planned end to end against a placeholder manifest and scanned with zero HIGH/CRITICAL findings. Nothing is provisioned: the region (ADR-001 R-2), the tenancy (R-3) and the written confirmation (R-1) are outstanding, and the plan refuses while they are. The restore drill is TASK-023's and has not run |
| Branch | `infra/task-020-database-provisioning` |
| Deliverables | `infra/terraform/database/` — the provisioning module, its encryption and backup configuration (`main.tf`), the off-instance export (`backup.tf`) and `verify-database-controls.sh`; two PROD guards in `infra/terraform/platform/guards.tf`; the wiring through `platform/` and the four environment roots; `docs/architecture/database-check.py` wired into `repo-checks`; this record |
| Environment variables / secrets | `DB_CONNECTION_STRING` (per environment) and `DB_BACKUP_STORAGE_CONNECTION_STRING` (SIT/UAT/PROD). Neither is read, written or held here: this task creates the instance and the backup target the two values address, and TASK-019's owner writes the values (`secret-management.md` §3). The module issues no password at all — the application authenticates as its runtime service account |
| Implements | CTL-17 (encryption at rest and in transit), CTL-35 (automated backups and PITR sized to RPO 1 hour); ADR-001 §7 C-1, C-3, C-4; ADR-002 §4.2.3; PTBC-048 |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), sheets Implementation Plan (TASK-020 and the P3 rows around it), Environment and Secrets, Open Questions, Architecture Decisions, as read for TASK-017 on 2026-09-22 and unchanged since |
| Controlled sources and authority order | per TASK-001 |

## 1. What this record is

TASK-017 authored a Cloud SQL module as one of its five deliverables and left the encryption,
backup and recovery configuration inside it to this task. So TASK-020 did not start from an empty
directory; it started from a module that already had TLS-only ingress, private-IP-only networking,
backups and PITR switched on, and IAM authentication. What this task owns is the difference between
*switched on* and *demonstrably meeting the objective the gate approved*.

Three things follow, and they are the whole of §3:

- **RPO 1 hour is met by point-in-time recovery, not by the backup.** Cloud SQL's automated backup
  runs once a day. A configuration with backups on and PITR off has an RPO of 24 hours and would
  pass a casual reading of "automated backups are enabled". §3.2 states the relationship and adds
  the invariant that keeps it true.
- **"Encryption at rest is verifiable via the provider's console/CLI" needs care.** With
  Google-managed keys there is no field that reads *encrypted: true*, because encryption is a
  property of the platform that cannot be turned off. §3.3 says what the CLI can actually be asked,
  and what the module adds so that a customer-managed key costs a variable rather than a rebuild.
- **An automated backup is deleted with the instance it belongs to.** That is enough for the
  failures the RPO describes and no answer at all to the instance or the project being deleted. The
  Environment and Secrets sheet already names a target for the copy that survives —
  `DB_BACKUP_STORAGE_CONNECTION_STRING` — and until this task nothing wrote to it. §3.4.

The retention period is not decided here. OQ-003 / PTBC-048 is open with AHDA Cybersecurity and
Records, and TASK-020's own gate cell says to build the mechanism and leave the period
configurable. Two variables carry it, neither has a default, and PROD cannot be applied while
either is unset.

## 2. The gate

TASK-020's Gate Decision cell:

> UNBLOCKED by ADR-001. Backup frequency and point-in-time recovery sized to RPO 1 hour. Retention
> period is still TBC (OQ-003) — build the mechanism, leave the period configurable.

| Item | State | Effect here |
| --- | --- | --- |
| ADR-001 (GCP, in-Kingdom; RPO 1 h, RTO 4–6 h) | Approved at the AHDA gate, 19 Sep 2026 | The substrate, and the objective the backup configuration is sized to. |
| ADR-002 §4.2.3 — managed product | **Settled**: Cloud SQL for PostgreSQL 17, fallback 16, unless AHDA IT requires AlloyDB | The module is Cloud SQL. Rule (a) of that section — standard PostgreSQL only — means an AlloyDB answer costs this module's resource type and nothing above it. |
| PTBC-048 — RPO/RTO | **Approved**, from AHDA IT's business continuity plan | §3.2: PITR is on in every environment and the backup chain is asserted to cover its window. |
| OQ-003 / PTBC-048 — retention period | **Open**, AHDA Cybersecurity + Records, gate Before Production | `retained_backups` and `noncurrent_version_retention_days` have no defaults; `database-check.py` DB-4 fails the pull request if either acquires one; PROD refuses to plan without the first. |
| ADR-001 R-4 — data classification | **Open**, no answer | §3.3: encryption at rest is Google-managed, and `encryption_key_name` is the hook that makes a different answer a variable. |
| ADR-001 R-1, R-2, R-3 | Outstanding | No apply is possible, in this task or any other. TASK-017's preflight guards fail the plan first and say who owes what. |

## 3. What was built

### 3.1 The four properties, and what carries each

| Property | Where | Control |
| --- | --- | --- |
| No non-TLS connection, and no public address for one to arrive on | `ip_configuration`: `ssl_mode = "ENCRYPTED_ONLY"`, `ipv4_enabled = false`, private network only | CTL-17, CTL-03 |
| Encrypted at rest, with a customer-managed key when AHDA names one | `encryption_key_name`, plus the KMS grant for the Cloud SQL service agent and a precondition that the key is in the named region | CTL-17, ADR-001 C-1 |
| Automated backups and PITR in the named region, sized to RPO 1 hour, retention configurable | `backup_configuration` and the two preconditions in §3.2 | CTL-35, PTBC-048, ADR-001 C-3 |
| A copy that outlives the instance | `backup.tf` — a scheduled export into the environment's single-region backup bucket | CTL-35, ADR-001 C-4 |

Two further properties were already true and are restated because they are what make the rest
coherent: the instance issues **no password** (the application is a Cloud SQL IAM service-account
user, so this module creates, holds and hands over no secret material, CTL-18), and deletion
protection is now set at **both** levels — Terraform's, which stops `terraform destroy`, and the
API's `deletion_protection_enabled`, which stops the console and the API.

### 3.2 How RPO 1 hour is actually met

Cloud SQL's automated backup is daily. On its own that is an RPO of 24 hours, and no start time
makes it 1 hour. Point-in-time recovery is what meets PTBC-048: the write-ahead log is archived
continuously, and a restore can name any second inside the log window, so **inside that window the
RPO is seconds and outside it the RPO is the age of the newest backup.** PITR is therefore not an
enhancement in this module; it is the mechanism, and the module treats it as one.

That leaves one way for the window to become quietly unusable. Recovery replays the log *forward
from a backup*, so a day of log with no surviving backup behind it cannot be recovered to. Backups
are daily, so the chain holds exactly while the retained backup count covers the log window. A
precondition asserts it:

```
retained_backups (3) is fewer than transaction_log_retention_days (7). Automated backups are daily,
and point-in-time recovery replays the log forward from one of them, so the oldest 4 day(s) of log
would have no backup to start from. Raise the retained backup count or shorten the log window
(PTBC-048, CTL-35).
```

This matters most on the day OQ-003 closes. A retention period is a business answer, and the
obvious way to apply it is to set `retained_backups` to whatever number AHDA gives. If that number
is smaller than the log window, the PITR window silently shortens to it — an RPO regression
introduced by a compliance decision, which is not a class of defect anyone goes looking for. The
plan now refuses instead.

`transaction_log_retention_days` defaults to 7, which is the provider's maximum for the Cloud SQL
Enterprise edition and is validated as such. It is not an AHDA value and is not presented as one:
it is the longest window the selected edition offers, and how far back a restore can reach is
exercised by TASK-023's drill.

### 3.3 Encryption at rest, and what "verifiable" means

The acceptance criterion is that encryption at rest is "enabled and verifiable via the hosting
provider's console/CLI". Two facts have to sit next to each other for that to be answered honestly:

- Cloud SQL encrypts every instance, its automated backups and its exports at rest with
  Google-managed keys. It cannot be turned off, and there is no field to read that says so.
- What the CLI *can* be asked is which key: `diskEncryptionConfiguration.kmsKeyName` is present
  when a customer-managed key is in use and absent when it is not.

So the verification script (§4) reports the key when there is one and records the Google-managed
default when there is not, and it does not invent an assertion the provider does not make. What it
does assert, when a key is named, is that the key is **in the named in-Kingdom region** — a key
held elsewhere is the one thing that makes in-Kingdom data readable outside it (ADR-001 C-1). The
same assertion is a plan-time precondition, so a key in the wrong region fails before anything is
created.

Whether AHDA requires its own key follows the data classification, which is ADR-001 R-4 and
unanswered — `infrastructure-as-code.md` F-6. The module therefore does not choose: `encryption_key_name`
defaults to null, and when it is set the Cloud SQL service agent is granted use of that key in the
same apply. **No key ring, key or rotation period is created here**, because each needs an owner
and none has one. If R-4 comes back requiring CMEK, the cost is a variable and a key AHDA
administers, not a rebuild.

### 3.4 The copy that outlives the instance

Automated backups and the PITR log live with the instance and are deleted with it. The Environment
and Secrets sheet names `DB_BACKUP_STORAGE_CONNECTION_STRING`, scoped to SIT, UAT and PROD, for the
copy that does not, and TASK-017 already created the single-region `db-backups` bucket it addresses
(ADR-001 C-4). Nothing wrote to it, which made the row name a target nothing used.

`backup.tf` closes that:

| Piece | Why it is shaped this way |
| --- | --- |
| The instance's own service agent gets `objectCreator` and `objectViewer` on the bucket | `objectCreator` can add an object and cannot delete or overwrite one. An export that could remove an earlier export would be a backup the backup mechanism can destroy. |
| A dedicated service account, not the runtime one | The application must not be able to export the database it serves (CTL-02, least privilege). |
| A custom role with `cloudsql.instances.export` and `cloudsql.instances.get` | `roles/cloudsql.editor` would do this and also restart, patch and restore the instance. |
| A Cloud Scheduler job calling the Cloud SQL Admin API, `offload: true` | The export runs on a temporary instance, so a nightly export cannot slow the service that is serving traffic. |
| One fixed object name, in a versioned bucket | Each run lands as a new version of one object, so the chain's length is the bucket's own non-current-version lifecycle rule — which is null while OQ-003 is open. The frequency is the schedule, the retention is that rule, and neither carries a value nobody approved. |

DEV has no backup bucket, because the sheet scopes the row to SIT, UAT and PROD, so DEV has no
export; a schedule without a bucket fails the plan rather than creating a job that writes nowhere.
`database-check.py` DB-7 reads that scoping out of the manifest rather than listing three
environment names, so the sheet stays the source of truth: add the row to DEV and the check asks
for DEV's export rather than contradicting it.

### 3.5 What differs between environments

Nothing structural. Every environment gets the same TLS posture, the same encryption, the same PITR
and the same backup window. What the four `terraform.tfvars` files differ on, for this task:

| | DEV | SIT | UAT | PROD |
| --- | --- | --- | --- | --- |
| `backup_export_schedule` | none — no backup bucket | `0 3 * * *` | `0 3 * * *` | `0 3 * * *` |
| `retained_backups` | unset (OQ-003) | unset | unset | unset — **and PROD cannot be applied** |
| `database_encryption_key_name` | unset (ADR-001 R-4) | unset | unset | unset |

03:00 UTC clears both the 22:00 UTC backup window and the Saturday 01:00 UTC maintenance window.

### 3.6 Two more guards

TASK-017's preflight already refused a PROD apply with no `retained_backups`. This task adds the
second half of the same argument:

| Guard (`platform/guards.tf`) | Owed by | Why it is a guard and not a default |
| --- | --- | --- |
| PROD has a `retained_backups` value | AHDA Cybersecurity and Records — OQ-003 / PTBC-048 | "How long a production backup is kept" is not a value to discover at the first audit. |
| PROD has a `backup_export_schedule` | — | Not an AHDA value at all. It is the difference between a backup and a snapshot: without it, PROD's only copies are on the instance, and deleting the instance deletes them. |

## 4. Verification

Everything below was executed on this branch with Terraform 1.16.3 — the version
`infra/terraform/.terraform-version` pins and CI runs.

| | Check | Result |
| --- | --- | --- |
| 1 | `terraform fmt -check -recursive infra/terraform` | clean |
| 2 | `terraform validate` on all four roots | 4/4 success |
| 3 | `terraform plan` end to end, placeholder manifest | DEV **40 to add**; SIT and UAT **49**; PROD **49** once a retention value is supplied. Of the nine extra resources in SIT/UAT/PROD, seven are this task's export — two bucket grants, a service account, a custom role and its binding, the scheduler token grant and the job — and two are TASK-017's backup buckets, which DEV does not get |
| 4 | Two consecutive SIT plans compared through `compare-plans.py` | **identical** — 50 resource changes, actions `create` and `read`, same `resource_changes`, `planned_values`, `configuration`, `variables` and `output_changes` |
| 5 | Trivy config scan, `infra/terraform` | **zero HIGH, zero CRITICAL.** Four LOW, all pre-existing and both already recorded by TASK-017: GCP-0075 (false positive on the proxy-only subnet) and GCP-0066 (CMEK on buckets — F-6) |
| 6 | Every guard and precondition fired against a plan | 5/5 — PROD without retention, PROD without an export schedule, `retained_backups` below the log window, a key outside the region, a schedule with no bucket |
| 7 | `database-check.py`, mutation test | **9 mutations, 9 caught** (§4.1) |
| 8 | `verify-database-controls.sh <env> --dry-run` | prints the five read-only calls per environment; DEV correctly skips the export checks |
| 9 | `verify-database-controls.sh`, exercised end to end against a stubbed provider and a real TLS-only PostgreSQL 17 | **15 cases, 15 correct** (§4.3). Two defects in the script were found this way and fixed |
| 10 | All eight other `repo-checks` scripts | unchanged, all pass |

The plan in row 3 needs no credentials and reads nothing from the provider: the one data source —
the project number, which every Google service agent is named after — is deferred behind the
preflight guards, so a plan still refuses at the guard rather than at an authentication error.

### 4.1 Mutation test

Each mutation was applied to a copy of the tree, `database-check.py` run, and the mutation reverted.

| | Mutation | Caught by |
| --- | --- | --- |
| M1 | `ssl_mode` relaxed to `ALLOW_UNENCRYPTED_AND_ENCRYPTED` | DB-2 |
| M2 | point-in-time recovery turned off | DB-3 |
| M3 | backup location changed to another region | DB-3 |
| M4 | `retained_backups` given a default of 7 | DB-4 |
| M5 | a retention period set in `uat/terraform.tfvars` | DB-4 |
| M6 | the PROD export guard removed | DB-5 |
| M7 | a KMS key committed to `prod/terraform.tfvars` | DB-6 |
| M8 | SIT's export schedule deleted while the sheet still scopes it a backup target | DB-7 |
| M9 | a control documented in the verification script's header but not implemented | DB-8 |

### 4.2 What could not be executed, and what that leaves owed

Two of the acceptance criteria are assertions about a running instance: *attempt a non-TLS
connection and confirm rejection*, and *confirm the backup job history shows successful recent
runs*. **Neither was executed against a provisioned instance, because there is none.** No project
exists, no Terraform state exists anywhere in the repository, and `gcloud` is not installed on the
authoring machine. The two checks are D-2 (`--connect`) and D-4, and both **fail rather than skip**
when they cannot run, so nothing in this repository records them as passed.

| Owed | Why it cannot run today | Who closes it |
| --- | --- | --- |
| A real `terraform apply` | ADR-001 R-1, R-2, R-3. The plan refuses at the preflight guards. | AHDA IT — UGV-07 |
| **The non-TLS rejection, observed** | There is no public endpoint, so the attempt has to come from a host with a route to the private IP. What was proved instead is that the check is correct: it was run against a real PostgreSQL 17 configured to accept TLS only, against one that accepts plaintext, and against nothing at all, and it reported refused / ACCEPTED / nothing-proved respectively (§4.3). | `verify-database-controls.sh <env> --connect`, from inside the VPC, at the first apply |
| **Backup history showing successful recent runs** | Needs an instance that has been running for a day. D-4 asserts a successful automated backup inside the last 26 hours, stored in the named region — 26 rather than 24 so a late backup is not read as a missing one. | `verify-database-controls.sh <env>`, the day after the first apply |
| "A restore has been test-executed at least once" | **TASK-023's drill.** The criterion names it, and this task supplies what the drill restores from: the PITR window, the export object, the instance's service agent and the `database_backup_configuration` output. | TASK-023 |

On the day the first environment exists, the two commands are:

```sh
infra/terraform/database/verify-database-controls.sh sit            # D-4: backup history, and the rest
infra/terraform/database/verify-database-controls.sh sit --connect  # D-2: attempt the non-TLS connection
```

### 4.3 The verification script, exercised

A check that has never run is a claim, not a control — and the acceptance criteria rest on this one.
It was therefore driven end to end before any environment exists: a stub standing in for the
provider's five read-only calls, and, for the connection attempt, a real PostgreSQL 17 container
configured exactly as the instance is — TLS on, and a host-based authentication file with no
plaintext entry. That container refuses an unencrypted connection with the same message a Cloud SQL
instance under `ssl_mode = ENCRYPTED_ONLY` returns:

```
FATAL:  no pg_hba.conf entry for host "…", user "postgres", database "pmplatform", no encryption
```

Fifteen cases, each breaking one control, all reported correctly:

| | Case | Caught by |
| --- | --- | --- |
| R1 | `sslMode` relaxed to `ALLOW_UNENCRYPTED_AND_ENCRYPTED` | D-2 |
| R2 | a public address enabled | D-2 |
| R3 | point-in-time recovery off | D-3 |
| R4 | backups stored in another region | D-3 |
| R5 | retention below the log window | D-3 |
| R6 | PROD left on the provider's default retention | D-3 |
| R7 | no successful backup exists | D-4 |
| R8 | the newest successful backup is 40 hours old | D-4 |
| R9 | an encryption key outside the Kingdom | D-1 |
| R10 | the export job paused | D-5 |
| R11 | a multi-region backup bucket | D-5 |
| R12 | bucket versioning off | D-5 |
| R13 | no exported object present | D-6 |
| R14 | an instance that **accepts** an unencrypted connection | D-2 |
| R15 | nothing listening — the attempt never reached a server | D-2, as *nothing proved* |

Two defects in the script were found this way, and both would have mattered on the day it first
runs for real:

- **A failed `psql` was read as a refusal.** An unreachable host, a timeout and a name that does not
  resolve all fail too, so the check would have reported the control as proved on a day nothing was
  tested — which is exactly what R15 now catches. The attempt is now classified three ways, and only
  a refusal *by the server, for an encryption reason* passes.
- **`jq`'s `//` operator treats `false` as absent.** `ipv4Enabled` being false is the single most
  important thing this script reads, and `// ""` turned it into "unset" — so a correctly configured
  private instance was reported as having a public address. Every boolean read went through that
  helper.

## 5. Acceptance criteria

| # | Criterion | Status |
| --- | --- | --- |
| 1 | Database instances reject non-TLS connections | **BUILT, NOT OBSERVED — no instance exists to attempt it against.** `ssl_mode = "ENCRYPTED_ONLY"` rejects an unencrypted connection outright, and there is no public address for one to arrive on; DB-2 fails any pull request that relaxes either. The attempt itself is D-2 (`--connect`), which was proved correct against a real TLS-only PostgreSQL 17 and against an instance that accepts plaintext (§4.3), and which fails rather than skips when it cannot reach a server. Run it from inside the VPC at the first apply. |
| 2 | Encryption at rest enabled and verifiable via the provider's console/CLI | **MET in configuration, with the qualification in §3.3.** Encryption at rest is on and cannot be turned off; what the CLI can be asked is which key, and the script reports it. A customer-managed key is a variable away and is asserted to be in the named region, at plan time and at run time. Whether AHDA requires one is ADR-001 R-4. |
| 3 | Automated backup job runs on the documented schedule | **BUILT, NOT OBSERVED.** Daily backups at 22:00 UTC into the named region, PITR on, plus a daily export at 03:00 UTC into the single-region backup bucket in SIT/UAT/PROD. The schedule is documented by being an output (`database_backup_configuration`) rather than a paragraph that can drift from it. That it *ran* — successful backups in the provider's own history, inside the last 26 hours, in the named region — is D-4, which needs a provisioned environment and fails rather than skips. **Not observed: no environment exists.** |
| 4 | A restore has been test-executed at least once (TASK-023 drill) | **NOT MET — TASK-023's, and blocked with it.** What this task owed the drill is in place: a PITR window of up to 7 days, an export object that survives the instance, and the invariant that the backup chain covers the log window. |
| — | Gate cell: backup frequency and PITR sized to RPO 1 hour | **MET.** §3.2, including the precondition that keeps it true when OQ-003 closes. |
| — | Gate cell: retention period configurable, no value invented | **MET.** `retained_backups` and `noncurrent_version_retention_days` have no defaults, are set in no `terraform.tfvars`, and DB-4 fails the pull request if either acquires one. PROD refuses to plan without the first. |
| — | `DB_CONNECTION_STRING`, `DB_BACKUP_STORAGE_CONNECTION_STRING` | **TARGETS CREATED, VALUES NOT WRITTEN.** This task creates the instance and the backup target the two rows address and outputs the connection name and the export URI. The values are written by the owner the sheet names, under TASK-019. Nothing here reads or holds a secret. |

## 6. Findings and open items

| ID | Finding | Owner / where it goes |
| --- | --- | --- |
| **D-1** | **The APIs these resources need are not enabled anywhere.** `provision-environment.sh` enables seven services and says "every other task enables its own"; no Terraform module enables any. This module needs `sqladmin.googleapis.com`, `cloudscheduler.googleapis.com` and, if a key is ever named, `cloudkms.googleapis.com` — and TASK-017's other modules have the same gap (`servicenetworking`, `run`, `compute`, `certificatemanager`). It surfaces at the first apply as an API error, not as a design problem, but it surfaces on every environment. **Recommended: one `gcloud services enable` step in `provision-environment.sh` covering the platform's full set, rather than four tasks each adding their own.** Until then: `gcloud services enable sqladmin.googleapis.com cloudscheduler.googleapis.com --project=<project>` before the first apply. | TASK-016 (the script), TASK-017 (the full list) |
| **D-2** | **Cloud Scheduler's availability in the named region is unconfirmed.** ADR-001 §7's closing note and CTL-54 require each managed product to be confirmed available in the region *before* the region is fixed, and the confirmation request lists the load balancer, the database and the compute runtime. Cloud Scheduler is now on that list too: the export job is a regional resource in the named region. If it is unavailable there, the export moves to a scheduled job on the existing pipeline and the bucket, grants and object layout are unchanged. | AHDA IT — with UGV-07; add to the CTL-54 confirmation list |
| **D-3** | **The retention period, when it arrives, lands in two places.** `retained_backups` governs the automated backups on the instance; `noncurrent_version_retention_days` governs how many exported copies the bucket keeps. OQ-003 will most likely give one number, and it applies to both. They are separate variables because they are separate mechanisms, and DB-4 keeps both of them empty until the answer exists — but a reviewer applying the answer must set both. | AHDA Cybersecurity + Records — OQ-003 / PTBC-048; then whoever applies it |
| **D-4** | **F-7 from TASK-017 is still open, and its infrastructure half is this task's.** `max_instances × connection pool size` is not bounded against the instance's connection limit: PROD scales to 10 Cloud Run instances, and a default Npgsql pool of 100 would ask for 1000 connections. Nothing was invented here — Cloud SQL sets `max_connections` from the tier, and overriding it needs a number nobody has derived. **Recommended: read the effective `max_connections` at the first apply, set the pool ceiling on the connection string TASK-019 writes, and review the two numbers together whenever either changes.** | TASK-019 (connection string), Backend Lead (pool size), this module (the flag, if one is needed) |
| **D-5** | **The workbook still calls this task AlloyDB.** ADR-002 §4.2.3 settled the product as Cloud SQL and listed six cells to correct, of which this task's title is one; ADR-001 §10.1 says the same. The module is Cloud SQL, the application is written to standard PostgreSQL only, and an AlloyDB answer costs this module's resource type. Recorded here so the divergence is visible where the work is, not only in the two ADRs. | Workbook edit — PMO; product — ADR-002 S-3 |

## 7. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-24 | Verification script exercised end to end against a stubbed provider and a real TLS-only PostgreSQL 17: 15 cases, 15 correct (§4.3). Two defects found and fixed — a failed `psql` read as a refusal, and `jq`'s `//` turning `ipv4Enabled: false` into "unset". §4.2 restated: the non-TLS attempt and the backup history are **not observed**, because no environment exists. | Infrastructure (TASK-020) |
| 2026-09-24 | Initial record. Encryption, backup and recovery configuration taken over from the TASK-017 module: CMEK hook with an in-region precondition, API-level deletion protection, the backup-chain invariant behind RPO 1 hour, and the off-instance export (`backup.tf`) that makes `DB_BACKUP_STORAGE_CONNECTION_STRING` address something. Two PROD guards added. `verify-database-controls.sh` (six runtime checks) and `database-check.py` (eight static checks, wired into `repo-checks`) written; nine mutations tested, nine caught. Five findings registered. | Infrastructure (TASK-020) |

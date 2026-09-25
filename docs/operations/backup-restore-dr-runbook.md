# Backup, restore and disaster recovery runbook

| Field | Value |
| --- | --- |
| Task | TASK-023 — Define Backup, Restore & Disaster Recovery Runbook (P3 - Infrastructure & DevOps) |
| Depends on | TASK-020 — managed PostgreSQL with backup and encryption ([`database-provisioning.md`](../architecture/database-provisioning.md), BUILT — NOT APPLIED). The workbook titles it "AlloyDB"; the product is Cloud SQL for PostgreSQL 17 (ADR-002 §4.2.3) |
| Covers | The three stores CTL-36 names — the **database**, the **document store** and the **audit store** — in SIT, UAT and PROD. DEV is rebuilt, not restored (§2) |
| Recovery objectives | **RPO 1 hour, RTO 4 to 6 hours.** Approved: PTBC-048, AHDA gate of 19 Sep 2026, from AHDA IT's business continuity plan; TASK-023 gate cell "THRESHOLDS SET" |
| Retention period | **TBC** — OQ-003 / PTBC-048, owner AHDA Cybersecurity + Records, gate Before Production. Not invented anywhere in this runbook or in the Terraform |
| On-call owner | **DevOps/Platform Lead** — the owner the Environment and Secrets sheet names for both backup targets. Named individual and contact route: §3 |
| Implements | CTL-36; exercises CTL-35 (TASK-020) and CTL-24's chain (TASK-073); ADR-001 §7 C-3, C-4, C-11 |
| Status | **WRITTEN — DRILLED LOCALLY, NOT IN A NON-PROD ENVIRONMENT.** The §8 drill was executed end to end on Docker (`run-restore-drill.sh`, evidence log E-3): restore into an isolated environment, every table's row count and checksum equal to the source's, the application healthy on the restored database, time-to-restore recorded. The verification was also shown to catch nine kinds of fault (`rehearse-restore.sh`, E-1). The drill the acceptance criterion requires — from a Cloud SQL automated backup into a non-PROD instance — has not run, because no environment exists (ADR-001 R-1 to R-3, UGV-07); its blank entry is E-4 |
| Evidence | [`restore-drill-evidence-log.md`](restore-drill-evidence-log.md) |
| Tools | [`verify-restore.sh`](verify-restore.sh) and [`database-fingerprint.sql`](database-fingerprint.sql) — the verification; [`run-restore-drill.sh`](run-restore-drill.sh) — §8 executed locally; [`rehearse-restore.sh`](rehearse-restore.sh) — the verification's fault tests; [`drill-fixture.sql`](drill-fixture.sql) — the data both local scripts add |

Every time in this runbook is UTC, like every schedule in the Terraform.

## 1. What protects each store

| Store | Where it lives | Protected by | Data loss if restored from it | Restore path |
| --- | --- | --- | --- | --- |
| **Database** | Cloud SQL instance `pmplatform-<env>-db`, database `pmplatform` | Point-in-time recovery: the write-ahead log, archived continuously, 7-day window | Seconds, anywhere inside the window | §5.1 |
| | | Automated backup, daily at 22:00, in the named region, on the instance | Up to 24 h, for a restore to the backup's time | §5.2 |
| | | Scheduled export, daily at 03:00, to `gs://<project>-db-backups/pmplatform-<env>-db/pmplatform.sql.gz`, one version per run. **The only copy that survives the instance being deleted** | Up to 24 h | §5.3 |
| **Document store** | Bucket `<project>-documents` (WF-12) | Object versioning and a 7-day soft-delete window, in the bucket itself | None, for an overwritten or deleted object | §5.4 |
| | | Hourly Storage Transfer job to `<project>-document-backups` — never deletes and never overwrites in the backup (`infra/terraform/storage/backup.tf`) | Up to 1 h | §5.5 |
| **Audit store** | Schema `audit_activity` inside the application database (`audit_event`, hash-chained, append-only — CTL-24) | Everything that protects the database, because it *is* the database | As the database | §5.6 |
| | | The forwarded subset, in AHDA's SIEM (CTL-26). AHDA's copy, not a restore source; used to reconcile (§5.6) | — | — |

Two things follow from this table and are easy to get wrong under pressure:

- **RPO 1 hour is met by PITR and by the hourly document transfer, not by the daily backups.** Restoring
  the 22:00 backup when PITR could have restored to five minutes ago loses up to a day for nothing.
  Reach for §5.1 first whenever the instance still exists.
- **The instance's automated backups and its PITR log are deleted with the instance.** Once the instance
  is gone, the export in the `db-backups` bucket is the newest copy there is, and it is up to 24 hours
  old. Deletion protection is on at both the Terraform and the API level in PROD so that this case
  needs two deliberate acts to reach.

Both backup buckets are in the **same project** as the data they protect (finding BR-3).

## 2. Recovery objectives

| Field | Value | Source |
| --- | --- | --- |
| RPO | **1 hour** | PTBC-048, approved 19 Sep 2026 |
| RTO | **4 to 6 hours** | PTBC-048, approved 19 Sep 2026 |
| RTO clock starts | When the on-call owner declares a recovery (§4 step 1) | This runbook |
| RTO clock stops | When the restored service passes §6.1 and writes are reopened | This runbook |
| Pass mark for a drill | Time-to-restore ≤ **4 h**. Between 4 h and 6 h is inside the approved range but is recorded as a finding, because the margin a real incident needs is gone. Over 6 h fails | This runbook, from the approved range |
| Backup retention period | **TBC** — OQ-003 / PTBC-048 | Open with AHDA Cybersecurity + Records |
| Hot standby | **Not assumed.** RTO 4–6 h does not by itself require one (TASK-023 gate cell; ADR-001 C-11). PROD is `REGIONAL` today; see BR-4 | — |
| DEV | No RPO or RTO applies. DEV holds no backup bucket (the sheet scopes both backup rows to SIT, UAT and PROD) and is rebuilt from Terraform and the seed | Environment and Secrets sheet |

The acceptance criterion asked for RPO/RTO to be marked TBC *until AHDA approves them*. AHDA has approved
them, so they are stated. The retention period has not been approved and is TBC everywhere.

## 3. Who is on call

| Role | Who | Does |
| --- | --- | --- |
| **On-call owner** | **DevOps/Platform Lead** — named individual: **TBC at environment handover**, recorded here and in AHDA's on-call rota | Declares the recovery, starts the RTO clock, runs §5, owns the decision to reopen writes |
| Database approver | Backend Lead | Confirms the restore point for §5.1 and signs §6.1 for application data |
| Audit custodian | Security Lead, with AHDA Cybersecurity | Signs §5.6: the restored chain, and any gap against the SIEM |
| Tenancy and region | AHDA IT | Project undelete (§4, S-3), anything the delivery team's IAM cannot do, and every region-loss decision (S-7) |
| Change authority | AHDA change process | A PROD restore is a production change; in an outage the record is written after the fact |

The role is named because the sheet names it; the person is not, because no individual has been
designated and inventing one would put a name on a rota nobody agreed to. Filling that cell is a
precondition of the first PROD apply (BR-6).

## 4. Declare, decide, freeze

1. **Declare.** Record the time: this is T0, and the RTO clock starts. Open an incident record.
2. **Decide what failed**, and go to the row:

   | | What happened | Go to |
   | --- | --- | --- |
   | S-1 | Bad data: a faulty migration or release, a bulk mistake, rows deleted — the instance itself is fine | §5.1 (PITR, to just before the event) |
   | S-2 | The instance is unusable or deleted | Instance still listed: §5.2 from its newest backup, or §5.1. Deleted: §5.3 |
   | S-3 | The project was deleted | AHDA IT: `gcloud projects undelete <project>` — possible for 30 days, and everything in it comes back. Then re-assess from S-1 |
   | S-4 | Documents overwritten or deleted, bucket intact | §5.4 |
   | S-5 | The documents bucket, or its objects wholesale, lost | §5.5 |
   | S-6 | A zone is down | PROD (`REGIONAL`) fails over by itself; confirm and monitor. SIT/UAT (`ZONAL`): wait, or §5.2 into another zone of the region if the outage threatens the RTO |
   | S-7 | The region is down | **No in-Kingdom target exists** (ADR-001 C-11, BR-5). Escalate to AHDA IT at once; this runbook cannot meet the RTO for this case |

3. **Freeze writes** for anything that touches the database (S-1, S-2): stop traffic reaching the API, so
   nothing is written to an instance you are about to replace or read from.

   ```sh
   gcloud run services update ahda-pmplatform-<env>-api --project=ahda-pmplatform-<env> \
     --region=<region> --ingress=internal
   ```

   `internal` refuses the load balancer's traffic. The service's normal setting is
   `internal-and-cloud-load-balancing` (`infra/terraform/compute`); §7 puts it back, and so will the next
   `terraform apply`.

4. **Do not restore over the only copy.** Every procedure below restores into a *new* instance or
   bucket and leaves the original as it is. The original is what §7 rolls back to. The single exception
   is §5.2's in-place restore, which needs the on-call owner and the database approver to agree, in the
   incident record, and an on-demand backup of the current state first.

## 5. Procedures

Common to all of them: `gcloud` authenticated as an operator in that environment's project only
(CTL-02), `jq`, and for any verification a host **inside the environment's VPC** with the Cloud SQL
Auth Proxy and `psql` 17 — the instance has no public address (BR-1). Names below are from
`infra/environments/environments.json`; `<env>` is `sit`, `uat` or `prod`.

```sh
env=sit
project=ahda-pmplatform-$env
region=$(jq -r .platform.region infra/environments/environments.json)
instance=pmplatform-$env-db
target=pmplatform-$env-db-r$(date -u +%Y%m%d%H%M)   # a new name: Cloud SQL does not reuse a deleted name for about a week
```

### 5.1 Point-in-time recovery into a new instance (S-1, S-2 while the instance exists)

1. Establish the restore point with the database approver: the last good second before the event, from
   the release log, the audit trail or the application logs. Check it is recoverable:

   ```sh
   gcloud sql instances describe $instance --project=$project --format='value(settings.backupConfiguration)'
   gcloud sql instances get-latest-recovery-time $instance --project=$project
   ```

   The point must be inside the 7-day log window and no later than the latest recovery time.

2. Clone to that point. The clone is a new instance with the source's settings, on the same private
   range:

   ```sh
   gcloud sql instances clone $instance $target --project=$project \
     --point-in-time='2026-10-01T09:14:00Z' \
     --allocated-ip-range-name=<private_services_range_name from the network module>
   ```

3. Verify: §6.1. Then cut over (§5.7) or, for a narrow mistake, copy the affected rows back from the
   clone to the live instance — a decision for the database approver, and the only case where the live
   instance is written to.

### 5.2 Restore an automated backup (S-2)

1. Find the newest successful backup:

   ```sh
   gcloud sql backups list --instance=$instance --project=$project \
     --filter='status=SUCCESSFUL' --sort-by=~endTime --limit=5 \
     --format='table(id,endTime,type,location)'
   ```

2. Create the target: same version, same or larger tier, private network only, TLS only.

   ```sh
   gcloud sql instances create $target --project=$project --region=$region \
     --database-version=POSTGRES_17 --tier=<tier from the environment's terraform.tfvars> \
     --network=projects/$project/global/networks/<network from the manifest> --no-assign-ip \
     --allocated-ip-range-name=<private_services_range_name> --ssl-mode=ENCRYPTED_ONLY \
     --database-flags=cloudsql.iam_authentication=on
   ```

3. Restore into it. The restore replaces everything on the target, including its users:

   ```sh
   gcloud sql backups restore <backup-id> --restore-instance=$target \
     --backup-instance=$instance --project=$project
   ```

4. Verify: §6.1. Cut over: §5.7.

**In place** — `--restore-instance=$instance` — overwrites the live instance and its data since the
backup. Only under §4 step 4's exception, and only after:

```sh
gcloud sql backups create --instance=$instance --project=$project --description="before in-place restore, incident <ref>"
```

### 5.3 Import the off-instance export (S-2 with the instance deleted)

1. Choose the export version. Each daily run is a version of one object:

   ```sh
   gcloud storage ls --all-versions --long "gs://$project-db-backups/$instance/pmplatform.sql.gz"
   ```

2. Copy the chosen generation to its own name, so the import reads exactly that version:

   ```sh
   gcloud storage cp "gs://$project-db-backups/$instance/pmplatform.sql.gz#<generation>" \
     "gs://$project-db-backups/restore/<incident-ref>.sql.gz"
   ```

3. Create the target as in §5.2 step 2, then its database and the application's IAM user, and let the
   new instance's service agent read the bucket:

   ```sh
   gcloud sql databases create pmplatform --instance=$target --project=$project
   gcloud sql users create app-$env@$project.iam --instance=$target --project=$project --type=cloud_iam_service_account
   agent=$(gcloud sql instances describe $target --project=$project --format='value(serviceAccountEmailAddress)')
   gcloud storage buckets add-iam-policy-binding "gs://$project-db-backups" \
     --member="serviceAccount:$agent" --role=roles/storage.objectViewer
   ```

4. Import:

   ```sh
   gcloud sql import sql $target "gs://$project-db-backups/restore/<incident-ref>.sql.gz" \
     --database=pmplatform --project=$project
   ```

5. Verify: §6.1. Cut over: §5.7.

### 5.4 Recover documents inside the bucket (S-4)

An overwritten object's earlier content is a non-current version; a deleted object is non-current or,
inside 7 days, soft-deleted. Neither needs the backup bucket.

```sh
gcloud storage ls --all-versions --long "gs://$project-documents/<path>"          # find the generation
gcloud storage cp "gs://$project-documents/<path>#<generation>" "gs://$project-documents/<path>"
gcloud storage ls --soft-deleted --long "gs://$project-documents/<path>"          # deleted and gone from the list
gcloud storage restore "gs://$project-documents/<path>#<generation>"
```

The copy makes the old content the live version again; the version it replaces stays as a non-current
version, so this step is itself reversible.

### 5.5 Restore documents from the backup bucket (S-5)

1. If the bucket itself is gone, recreate it and its grants from code:
   `terraform -chdir=infra/terraform/environments/<env> apply`. Bucket names are reusable.
2. Copy back. `rsync` without `--delete-unmatched-destination-objects` only adds: nothing already in
   the documents bucket is removed or replaced.

   ```sh
   gcloud storage rsync --recursive "gs://$project-document-backups" "gs://$project-documents"
   ```

3. Verify: §6.1, documents row.

Objects uploaded in the hour before the loss may not have reached the backup yet. That is the RPO; list
them from the WF-12 tables (`document_management.document_version` rows newer than the last transfer run) so their
owners can be asked to upload again.

```sh
gcloud transfer operations list --job-names=<document_backup_job_name output> --limit=3
```

### 5.6 The audit store (with every database restore)

The audit store comes back with the database. Three things are specific to it:

1. **The chain must be intact** on the restored instance. `verify-restore.sh` checks it on every
   compare (§6.1); a restore that breaks the chain has not restored the audit store, whatever else
   it restored.
2. **A restore to an earlier point removes the audit events after it.** They are gone from the
   authoritative store, and CTL-24 says committed audit events cannot be deleted — so the loss is itself
   an audit-relevant event. The audit custodian records the window (restore point → T0) in the incident
   record and informs AHDA Cybersecurity.
3. **Reconcile with the SIEM.** Events in that window that belonged to the forwarded subset are still in
   AHDA's SIEM, which is the only surviving record of them. Events restored with
   `audit_forwarding_record.status = 'PENDING'` that the SIEM already holds will be forwarded again.
   Whether AHDA's SIEM deduplicates them is not known (PTBC-029 is open); if it does not, the custodian
   identifies the duplicates by the envelope's `eventId` and tells AHDA Cybersecurity which to disregard.

### 5.7 Cut over to the restored instance

Only after §6.1 has passed.

1. Point the application at the restored instance: write the new `DB_CONNECTION_STRING` for this
   environment through the TASK-019 procedure, which keeps the previous version for §7.

   ```sh
   printf %s "$new_connection_string" | infra/secrets/rotate-secret.sh $env DB_CONNECTION_STRING --wait 0
   gcloud run deploy ahda-pmplatform-$env-api --project=$project --region=$region --image=<current digest>
   ```

   The redeploy of the same digest makes every instance re-read the secret now instead of within five
   minutes.

2. Check `/health` is `Healthy` through the internal path, then **reopen writes** — this is the point
   after which rolling back loses data (§7):

   ```sh
   gcloud run services update ahda-pmplatform-$env-api --project=$project --region=$region \
     --ingress=internal-and-cloud-load-balancing
   ```

3. Stop the RTO clock and record the time.

## 6. Data-integrity verification

### 6.1 After any restore, before cutover

| # | Check | How | Pass |
| --- | --- | --- | --- |
| V-1 | Every table, sequence and schema object came back | `verify-restore.sh fingerprint "<restored>" > restored.fp`, then read it: every schema the release's migrations create is present | No schema missing; row counts plausible against the last fingerprint on record |
| V-2 | Row counts and checksums | **When a fingerprint of the source at the restore point exists** (the drill, or a fingerprint taken before a risky change): `verify-restore.sh compare source.fp "<restored>"` | Exit 0. Exit 1 names the table; exit 2 means nothing was compared |
| V-3 | The audit chain is intact | Part of every `compare` and `fingerprint` (`audit-chain|` line) | `genesis=1 broken=0 forks=0`, or `absent` before TASK-073 |
| V-4 | Schema matches the deployed release | The EF Core migration history table on the restored instance lists exactly the migrations of the image digest in service (TASK-024) | Identical list. A restore to before a migration means redeploying the matching earlier release, not running the migration forward blind |
| V-5 | Sequences are ahead of their data | In the fingerprint; for each sequence, its `last_value` ≥ the maximum of the column it numbers | No sequence behind |
| V-6 | The restore point is what was asked for | The newest `recorded_at` in `audit_activity.audit_event` (or the newest `updated_at` in a busy table) is just before the chosen point | Within the minute |
| V-7 | Documents | `verify-restore.sh documents gs://<reference> gs://<restored>` — the reference is the backup bucket for §5.5, the source for the drill | Exit 0 |
| V-8 | The application works against it | `/health` Healthy; the smoke test (`infra/docker/smoke-test.sh` pattern, against the internal path); one read of a known project and one document download | All pass |

V-2 is the strongest check and needs a fingerprint *taken before* the failure. For an outage there
usually is none, so V-1, V-3 to V-8 carry the decision. Taking a fingerprint before any risky PROD
change — a migration, a bulk data correction — costs one command and turns V-2 on for that change.

### 6.2 In the drill

The drill can use V-2 at full strength because it controls the timing: it fingerprints the source
before and after the backup and only accepts the pair when they are identical (§8, DS-2 and DS-3). A
restored database must then reproduce that fingerprint byte for byte.

## 7. Rollback

Each procedure leaves the original instance or bucket untouched, so rolling back means pointing back
at it. What a rollback costs depends on how far the recovery got:

| Reached | Rollback | Loses |
| --- | --- | --- |
| Target created or restored, not cut over | `gcloud sql instances delete $target --project=$project`, or keep it for analysis | Nothing |
| Cut over, **writes still frozen** | `infra/secrets/rotate-secret.sh $env DB_CONNECTION_STRING --rollback`, redeploy the same digest, confirm `/health` against the original, reopen writes | Nothing |
| Cut over, **writes reopened** | As above, and the writes made on the restored instance since reopening are not on the original. Export them first (`gcloud sql export sql $target …`) and decide with the database approver whether to replay them | The writes since reopening, unless replayed |
| In-place restore (§5.2 exception) | Restore the on-demand backup taken before it, in place | Anything written after that backup |
| Documents copied back (§5.4, §5.5) | Nothing to undo: both only add versions. For §5.4, copy the replaced generation back the same way | Nothing |
| Manifest change merged (§9) | Revert the pull request; `terraform plan` must show no destroy | Nothing |

**Reopening writes is the point of no return.** Everything before it is free to undo, which is why
§6.1 runs before cutover and writes stay frozen until it passes.

## 8. The restore drill

Required by the acceptance criterion and by the Release Checklist ("Backup & DR Restore Drill Passed").
Run it in **SIT** — non-PROD and scoped a backup bucket by the sheet. Run it before the first PROD
release, after any change to the database or storage modules' backup configuration, and after any
restore procedure in this runbook changes. A standing cadence is not set here: it belongs with the
retention answer (OQ-003).

Locally, `run-restore-drill.sh <evidence-directory>` executes the same steps on Docker (evidence log
E-3). It is how a change to this section is tested before the next real drill.

**Pass criteria** — all of them:

| | Criterion | Threshold |
| --- | --- | --- |
| P-1 | Restored database equals the source at the backup | `verify-restore.sh compare` exit 0, from an **automated** backup (§5.2) |
| P-2 | The export restores equally | `verify-restore.sh compare` exit 0, from the latest export (§5.3) |
| P-3 | Audit chain intact on the restored database | `genesis=1 broken=0 forks=0`, or `absent` |
| P-4 | Documents restored equal the source | `verify-restore.sh documents` exit 0 |
| P-5 | Time-to-restore, DS-4 start to V-8 pass | ≤ 4 h (≤ 6 h with a finding; > 6 h fails) |
| P-6 | Recoverable point is recent | `now − latestRecoveryTime` ≤ 1 h (RPO) |

**Steps** (DS-n, so they are not confused with `verify-database-controls.sh`'s D-n checks). Record every time and every output in the evidence log as you go.

| | Step | Command |
| --- | --- | --- |
| DS-1 | Preconditions: SIT applied and a day old; `verify-database-controls.sh sit` passes (its D-4 shows last night's backup); a drill host in the SIT VPC (BR-1); synthetic test documents in the SIT documents bucket, if WF-12 has none yet — `verify-restore.sh documents` refuses an empty source | `infra/terraform/database/verify-database-controls.sh sit` |
| DS-2 | 21:45, before the 22:00 backup: fingerprint the source | `verify-restore.sh fingerprint "$sit" > before.fp` |
| DS-3 | After 03:00's export **and** the next hourly document transfer have finished: fingerprint again. `before.fp` and `after.fp` must be identical — SIT was quiet across the backup and the export, so both must reproduce it. If they differ, the night was not quiet: repeat, do not proceed | `verify-restore.sh fingerprint "$sit" > after.fp && cmp before.fp after.fp` |
| DS-4 | **Start the clock.** Restore the 22:00 automated backup into `pmplatform-sit-db-drill-<date>` | §5.2 steps 1–3 |
| DS-5 | Compare (P-1, P-3) | `verify-restore.sh compare after.fp "$drill"` |
| DS-6 | Import the 03:00 export into a second database on the drill instance and compare (P-2) | §5.3 steps 1–4 with `--database=pmplatform_export`, then `compare` |
| DS-7 | Restore documents into `<project>-document-drill-<date>` from the backup bucket, and compare with the source (P-4) | §5.5 step 2 with the drill bucket as destination; `verify-restore.sh documents gs://$project-documents gs://<drill bucket>` |
| DS-8 | **Stop the clock** after V-8 against the drill instance (point a scratch API revision at it, or run the smoke test from the drill host) (P-5) | §6.1 V-8 |
| DS-9 | RPO evidence (P-6) | `gcloud sql instances get-latest-recovery-time pmplatform-sit-db --project=ahda-pmplatform-sit` |
| DS-10 | Rollback rehearsal: delete the drill instance and drill bucket — §7 row 1. Nothing in SIT changed | `gcloud sql instances delete …`; `gcloud storage rm --recursive gs://<drill bucket>` |
| DS-11 | Record the result and sign it: on-call owner, database approver, audit custodian | Evidence log |

## 9. After recovery

1. **Bring the restored instance under Terraform.** A restored instance has a new name and Terraform's
   state still points at the old one. Change `database.instance` in `infra/environments/environments.json`
   through a reviewed pull request, move the state (`terraform state rm` the old instance,
   `terraform import` the new), and confirm `terraform plan` proposes no replacement. The scheduled
   export follows the instance name, so this is also what restarts the off-instance backup.
2. **Confirm backups run on the new instance** the next day: `verify-database-controls.sh <env>`, D-4.
3. **Keep the original instance, stopped, not deleted** (`--activation-policy=NEVER`) until the incident
   is closed. It holds the only copy of whatever was on it; how long it is kept after that is the
   retention period, which is TBC.
4. **Write the incident up** and link it from the evidence log. Any step in this runbook that was wrong
   is corrected in the same change.

## 10. The two backup variables

| Variable | Addresses | Used by this runbook |
| --- | --- | --- |
| `DB_BACKUP_STORAGE_CONNECTION_STRING` | `gs://<project>-db-backups`, the export target (TASK-020 `backup_export_uri`) | No. The runbook works through `gcloud` with the operator's own IAM identity and reads no secret |
| `DOCUMENT_STORAGE_BACKUP_CONNECTION_STRING` | `gs://<project>-document-backups`, the transfer target (`infra/terraform/storage/backup.tf`) | No, as above |

Both are Secret, scoped SIT/UAT/PROD, owner DevOps/Platform Lead, stored in Secret Manager (TASK-019).
Their values are written by that owner; this task created the second one's target writer and wrote
neither value. The sheet's verification method for both — "backup job completes and is visible in the
provider console" — is `verify-database-controls.sh` D-4 for the first and the transfer job's operation
history (§5.5) for the second.

## 11. Findings

| ID | Finding | Owner |
| --- | --- | --- |
| **BR-1** | **No operator path into a VPC exists.** The instance has only a private IP (CTL-03), and nothing in `infra/terraform` provides a bastion, an IAP tunnel or any other host an operator can run `psql` from. Every verification in §6 needs one, as does TASK-020's `--connect`. **Recommended: an IAP-reachable drill host per environment, created only for the drill or the incident and destroyed after**, so no standing path exists. | TASK-021 (network), AHDA Cybersecurity (approval of the access path) |
| **BR-2** | **Storage Transfer Service's data residency is unconfirmed.** The document transfer is a new managed service for ADR-001 C-1. Source and sink are both single-region buckets in the named region, but the service is global. Add it to the CTL-54 product confirmation list next to Cloud Scheduler (TASK-020 D-2). If it cannot be confirmed, the replacement is a scheduled `gcloud storage rsync` on the pipeline, with the same buckets and grants. | AHDA IT — with UGV-07 |
| **BR-3** | **The backups share a project with what they protect.** A compromised project owner, or a deleted project past its 30-day undelete window, takes the backups too. A separate backup project (or bucket-lock retention once OQ-003 names a period) closes it. This is a tenancy and cost decision, not one to make here. | AHDA IT, AHDA Cybersecurity — with ADR-001 R-3 |
| **BR-4** | **PROD's `REGIONAL` availability is a cost the RTO does not require.** The gate cell asks for any regional HA cost to be re-tested against RTO 4–6 h before it is committed. A restore from backup into a new instance is minutes to create plus the restore itself; the drill's P-5 measures it. If P-5 is comfortably under 4 h, `ZONAL` in PROD meets the RTO and roughly halves the instance cost — at the price of a restore instead of an automatic failover for a zone loss. `terraform.tfvars` is unchanged; the decision needs the drill's number. | Product owner and AHDA IT, after the drill |
| **BR-5** | **No DR target exists for loss of the region.** `me-central2` is the only region on the allowlist and ADR-001 C-11 forbids any out-of-Kingdom one. S-7 cannot meet the RTO. AHDA IT is asked (ADR-001 §7 closing note) whether a second in-Kingdom location exists; until then this is an accepted gap, and it is stated rather than implied away. | AHDA IT |
| **BR-6** | **The on-call individual is not named.** §3 names the role; the person and their contact route are required before the first PROD apply. | PMO — with AHDA's on-call rota |
| **BR-7** | **Recomputing the audit hash is not yet possible.** The chain check verifies linkage only. When TASK-073 defines `event_hash`, add the recomputation to `database-fingerprint.sql` so a restored event whose content changed but whose link did not is also caught. | TASK-073 |
| **BR-8** | **Retention lands in three places.** OQ-003's answer sets `retained_backups` (instance) and `noncurrent_version_retention_days` (buckets — db-backups *and* document-backups share it), as TASK-020 D-3 records. It also decides how long §9 step 3 keeps a replaced instance. | AHDA Cybersecurity + Records — OQ-003 |

## 12. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-25 | Validation cell executed locally: `run-restore-drill.sh` runs §8 against an isolated Docker environment with the application. PASS — 11 of 11 tables equal on rows and checksum, audit chain intact, application healthy on the restored database, time-to-restore 4 s against 4 h (evidence log E-3, raw evidence committed). Fixture moved to `drill-fixture.sql`; `verify-restore.sh report` added. The SIT drill is now E-4. | DevOps (TASK-023) |
| 2026-09-25 | Initial runbook. Three stores, seven failure cases, six restore procedures with rollback, eight integrity checks, the drill with pass criteria from RPO 1 h / RTO 4–6 h. Document-store backup built (`infra/terraform/storage/backup.tf`: hourly transfer to `document-backups`). Verification tooling written and rehearsed locally: two restores matched, one control and nine faults reported as expected (evidence log E-1, E-2). Non-PROD drill owed. Eight findings. | DevOps (TASK-023) |

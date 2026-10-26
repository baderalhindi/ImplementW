# Restore drill evidence log

| Field | Value |
| --- | --- |
| Task | TASK-023 — Define Backup, Restore & Disaster Recovery Runbook |
| Runbook | [`backup-restore-dr-runbook.md`](backup-restore-dr-runbook.md) — §8 is the drill, §6 the verification |
| Thresholds | RPO 1 h, RTO 4–6 h (PTBC-048, approved 19 Sep 2026). Retention: **TBC** (OQ-003) |
| Acceptance criterion | "A restore has been executed against a non-PROD environment from an automated backup and validated by row-count/checksum comparison" |
| Validation cell | "Execute a full restore-from-backup drill into an isolated environment; compare row counts and checksums of key tables against the source; record actual time-to-restore against the (TBC) RTO target" — the RTO is no longer TBC: 4–6 h |
| Status | **Validation cell executed locally (E-3): PASS. Acceptance criterion NOT MET.** E-3 ran the whole drill — backup, restore into an isolated environment, per-table row counts and checksums, the application brought up on the restored database, time-to-restore recorded — on Docker, from `pg_basebackup`. The criterion requires a non-PROD environment and a Cloud SQL automated backup; that drill (E-4) has not run because no environment exists (ADR-001 R-1 to R-3, UGV-07) |

Entries are appended, never edited. A correction is a new entry that names the one it corrects.

## Summary

| Entry | Date | Where | From | Result | Satisfies the acceptance criterion |
| --- | --- | --- | --- | --- | --- |
| E-1 | 2026-09-25 | Local, PostgreSQL 17 containers | Physical base backup; SQL export | **PASS** — both restores matched; control and 9 faults reported as expected | **No** — not a non-PROD environment, not an automated backup |
| E-2 | 2026-09-25 | Local, stubbed `gcloud` | Two bucket listings | **PASS** — 5 of 5 cases as expected, after one defect fix | No — the document comparison's logic only |
| E-3 | 2026-09-25 | Local: an isolated Docker environment, with the application | Physical base backup; SQL export | **PASS** — 11 of 11 tables match on rows and checksum; time-to-restore 4 s against 4 h | **No** — same two gaps as E-1. It is the validation cell, executed as far as anything can be without an environment |
| E-4 | — | SIT | Cloud SQL automated backup, export, document backup bucket | **NOT RUN** — blocked on UGV-07 and BR-1 | This is the entry that will |

## E-1 — Local rehearsal of the database restore and its verification

| Field | Value |
| --- | --- |
| Date | 2026-09-25, 12:19:35–12:19:47 UTC (final run) |
| Operator | DevOps (TASK-023) |
| Command | `docs/operations/rehearse-restore.sh` |
| Tooling | Docker 29.8.0, `postgres:17` image, psql 17.11 |
| Source | TASK-014 schema and seed (`infra/docker/postgres/init`), plus a fixture: 1,000 hash-chained `audit_activity.audit_event` rows and a 200,000-row table with a sequence, an index and Arabic text. 11 tables, 201,045 rows |
| Backups | Physical `pg_basebackup` (12 MB) — the local counterpart of a Cloud SQL automated backup restored to a new instance; plain SQL export, gzipped (5.1 MB) — the format of the TASK-020 scheduled export |
| Isolation | Each restore on its own server, in its own container, on a network created for the run and removed after it. A row was written to the source *after* the backups, so a restore that read the live source instead of the backup would have failed the comparison |

**Restores.**

| Restore | Time | Row counts and checksums | Audit chain |
| --- | --- | --- | --- |
| Physical backup onto a new server | 1 s | **MATCH** — 11 tables, 201,045 rows, 1 sequence | events=1000, genesis=1, broken=0, forks=0 |
| SQL export into an empty database | < 1 s | **MATCH** — 11 tables, 201,045 rows, 1 sequence | events=1000, genesis=1, broken=0, forks=0 |

The times are for a 12 MB database on one machine and say nothing about the RTO. P-5 is measured in E-4.

**The comparison is not vacuous.** Each fault was applied to a fresh copy of the restored database and
compared against the fingerprint taken at the backup:

| | Fault | Expected exit | Actual exit | |
| --- | --- | --- | --- | --- |
| F0 | none — an unchanged copy, the control: every fault below is judged against it | 0 | 0 | expected |
| F1 | one row missing out of 200,000 | 1 | 1 | expected |
| F2 | one character changed in one row, row count unchanged | 1 | 1 | expected |
| F3 | a whole table missing | 1 | 1 | expected |
| F4 | a sequence behind the data it numbers | 1 | 1 | expected |
| F5 | an index missing | 1 | 1 | expected |
| F6 | one audit event's link rewritten | 1 | 1 | expected |
| F7 | the import stopped part-way | 1 | 1 | expected |
| F8 | nothing listening — the comparison never reached a server | 2 | 2 | expected |
| F9 | a broken chain restored faithfully — identical fingerprints | 1 | 1 | expected |

What the diff showed for four of them — each names the fault and nothing else:

```
F2  -table|identity_access.role|8|873c920d7776314afc61ad3d0d2f014d
    +table|identity_access.role|8|620cf279d20df7671aed8383d5634381
F4  -sequence|drill_fixture.load_id_seq|200000|true
    +sequence|drill_fixture.load_id_seq|1|true
F6  -table|audit_activity.audit_event|1000|23156acd650037d6b7041fc32f927fa8
    +table|audit_activity.audit_event|1000|81917b1e2bd5462328470bc8038df7e1
    -audit-chain|events=1000|genesis=1|broken=0|forks=0
    +audit-chain|events=1000|genesis=1|broken=1|forks=0
    AUDIT CHAIN BROKEN: events=1000|genesis=1|broken=1|forks=0
F9  AUDIT CHAIN BROKEN: events=1000|genesis=1|broken=1|forks=0
```

F8 is the case that matters most on the day: a comparison that cannot connect reports *nothing proved*
(exit 2), never a match. F9 is why the chain check exists separately from the comparison — a backup
taken after the chain broke restores the break faithfully, and equality alone would pass it.

**Defects found by the rehearsal, fixed before the final run.**

| | Defect | Effect had it shipped |
| --- | --- | --- |
| 1 | `pg_basebackup` without `-c fast` waited for a spread checkpoint (4 min 31 s of a 4 min 37 s run) | Rehearsal only; a drill script with a silent five-minute stall is one nobody runs |
| 2 | The first fault selected a row with `min(uuid)`, which PostgreSQL does not define | Rehearsal only |

**Verdict.** The restore procedure's verification — row counts, checksums, sequences, schema objects and
the audit chain — works and is not vacuous. **This entry does not satisfy the acceptance criterion**: it
ran on containers, not in a non-PROD environment, and from `pg_basebackup`, not from a Cloud SQL
automated backup.

## E-2 — Document comparison, stubbed provider

| Field | Value |
| --- | --- |
| Date | 2026-09-25 |
| Command | `verify-restore.sh documents gs://<a> gs://<b>`, with a stub `gcloud` on `PATH` returning fixed listings (name, size, CRC32C; one name in Arabic) |
| What it proves | The comparison and its exit statuses. It does not prove the real `gcloud storage objects list` call, which needs a bucket |

| | Case | Expected exit | Actual exit |
| --- | --- | --- | --- |
| DC-0 | identical copy | 0 | 0 |
| DC-1 | one object missing | 1 | 1 |
| DC-2 | one object's CRC32C differs | 1 | 1 |
| DC-3 | restored bucket does not exist | 2 | 2 |
| DC-4 | source bucket lists nothing | 2 | 2 — **after a fix**: the first run returned 1, because an empty listing was written as one blank line and compared as a mismatch rather than refused as nothing to compare |

## E-3 — Full restore drill into an isolated environment, local

The validation cell, executed with `run-restore-drill.sh`, which follows runbook §8 step for step.
Raw evidence — the fingerprints, the log, the table report and the seed check — is committed in
[`evidence/2026-09-25-local-drill/`](evidence/2026-09-25-local-drill/).

| Field | Value |
| --- | --- |
| Date | 2026-09-25, 12:38 UTC |
| Operator | DevOps (TASK-023) |
| Command | `docs/operations/run-restore-drill.sh docs/operations/evidence/2026-09-25-local-drill` |
| Source environment | Its own Docker network: PostgreSQL 17 with the TASK-014 schema and seed plus `drill-fixture.sql` (11 tables, 201,045 rows, 1,000 chained audit events), and `PMPlatform.Api` built from this tree serving from it (`/health` 200) |
| Isolated environment | A Docker `--internal` network created at DS-4: a new PostgreSQL 17 server, the same API image, and an operations host from which every check ran. Nothing is published to the host |
| Isolation, tested | From the isolated operations host the source database was **unreachable**, and so was the internet. The same probe from the source's own network **reached** the source database — the control, without which "unreachable" would prove nothing |
| DS-3 | The source was fingerprinted before and after the backup; the two were identical, so `source.fingerprint` is the source as backed up. A row was then written to the source, so a restore that read the live source would have failed |

| Field | Value |
| --- | --- |
| Release artifact | `pmplatform-api:task-023-drill` `sha256:1546475b290c8db0b97c7c56ac9c86722dae110c521d3222079724d149adc4cf` |
| Backup taken | 2026-09-25T12:38:16Z |
| DS-4 clock start | 2026-09-25T12:38:16Z |
| Backup restored, server accepting connections | 2026-09-25T12:38:17Z |
| DS-5 comparison finished | 2026-09-25T12:38:19Z |
| DS-8 service verified, clock stop | 2026-09-25T12:38:20Z |
| **Time-to-restore** | **4 s** |
| P-5 against RTO 4–6 h | PASS (<= 4 h) |
| P-1 database, from the backup | MATCH |
| P-2 database, from the export | MATCH |
| P-3 audit chain | events=1000, genesis=1, broken=0, forks=0 |

**Row counts and checksums, every table** (`verify-restore.sh report`). The key tables — the
identity and access tables the application authorises with, the audit store, and the volume table —
are all of them:

| Table | Source rows | Restored rows | Source checksum (md5) | Restored checksum (md5) | |
| --- | ---: | ---: | --- | --- | --- |
| `audit_activity.audit_event` | 1000 | 1000 | `23156acd650037d6b7041fc32f927fa8` | `23156acd650037d6b7041fc32f927fa8` | match |
| `drill_fixture.load` | 200000 | 200000 | `45807b9cfbc1efdd706c36e6ade0e5ad` | `45807b9cfbc1efdd706c36e6ade0e5ad` | match |
| `identity_access.access_relationship` | 8 | 8 | `83d1aa373a754def2d6bb90c67fdb8e9` | `83d1aa373a754def2d6bb90c67fdb8e9` | match |
| `identity_access.department` | 1 | 1 | `d9e643519ebf62a72a99498a106cbe4a` | `d9e643519ebf62a72a99498a106cbe4a` | match |
| `identity_access.external_entity` | 1 | 1 | `2a1b00d9497433e7e5944c381674f3a3` | `2a1b00d9497433e7e5944c381674f3a3` | match |
| `identity_access.permission_profile_version` | 8 | 8 | `54f4b088326000693ec08146c9d11dfb` | `54f4b088326000693ec08146c9d11dfb` | match |
| `identity_access.permission_profile` | 8 | 8 | `999d4ab832252b643c5df190af276671` | `999d4ab832252b643c5df190af276671` | match |
| `identity_access.role` | 8 | 8 | `1b654a23bab94eb13c009f32645e54e5` | `1b654a23bab94eb13c009f32645e54e5` | match |
| `identity_access.user` | 9 | 9 | `f70cd32818118cb1b546ad3d636bb96d` | `f70cd32818118cb1b546ad3d636bb96d` | match |
| `master_data_config.master_data_catalogue` | 1 | 1 | `5e55740efe9dedc3e771188b9d2abf5b` | `5e55740efe9dedc3e771188b9d2abf5b` | match |
| `master_data_config.master_data_item` | 1 | 1 | `d2edc85bbc857938d11aba0b39687b47` | `d2edc85bbc857938d11aba0b39687b47` | match |

Sequence `drill_fixture.load_id_seq` 200000 on both sides; schema object counts identical (full
fingerprints in the evidence directory). The application check (DS-8): `/health` returned 200 from the
API on the restored database, and `verify-seed.sql` found one active local user for each of R01–R08.

**What the time means.** 4 seconds is a 12 MB database restored on one machine. It shows that the
clock is started and stopped where the runbook says and that the steps between them are complete. It
says nothing about how long a Cloud SQL restore takes: creating an instance and restoring a backup into
it is a provider operation whose duration depends on the instance and the data. That number is E-4's
P-5, and BR-4 waits on it.

**Not executed here:** DS-7 documents (no object store locally; the comparison's logic is E-2) and
DS-9 RPO (Cloud SQL's latest recovery time has no local counterpart).

**Verdict.** The validation cell's three demands are met on a local environment: a full
restore-from-backup into an isolated environment, row counts and checksums of every table equal to the
source's, and time-to-restore recorded against the 4-hour target. **The acceptance criterion is not
met**: the backup was `pg_basebackup`, not a Cloud SQL automated backup, and the environment was Docker,
not SIT.

## E-4 — SIT drill from a Cloud SQL automated backup

**NOT RUN.** Blocked on:

- a SIT environment — ADR-001 R-1 to R-3, UGV-07 (`docs/architecture/adrs/ADR-001-confirmation-request.md`);
- a host inside the SIT VPC to verify from — runbook BR-1;
- confirmation that Storage Transfer Service may be used — runbook BR-2 (for P-4 only).

Filled in by the operator as the drill runs (runbook §8).

| Field | Value |
| --- | --- |
| Date | |
| Operator (on-call owner) | |
| Database approver | |
| Audit custodian | |
| Drill host | |
| DS-1 `verify-database-controls.sh sit` | |
| DS-2 `before.fp` taken at / sha256 | |
| DS-3 `after.fp` taken at / sha256 / identical to `before.fp` | |
| Automated backup id / endTime / location | |
| Export generation / time | |
| Document transfer operation / time | |
| DS-4 clock start | |
| DS-4 drill instance / restore operation id / finished | |
| DS-5 P-1 compare — exit / tables / rows | |
| DS-5 P-3 audit chain line | |
| DS-6 P-2 export compare — exit | |
| DS-7 P-4 documents compare — exit / objects | |
| DS-8 clock stop / **time-to-restore** / P-5 verdict against 4 h (6 h) | |
| DS-9 latestRecoveryTime / now / **P-6** against 1 h | |
| DS-10 drill instance and bucket deleted at | |
| Deviations from the runbook, and the change that corrects each | |
| Result — PASS only if P-1 to P-6 all pass | |
| Signed | |

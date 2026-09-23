# Secret rotation runbook

| Field | Value |
| --- | --- |
| Task | TASK-019 — Integrate Approved Secret Management Store (P3 - Infrastructure & DevOps) |
| Applies to | The 23 variables the Environment and Secrets sheet stores in the approved secret-management platform, across DEV, SIT, UAT and PROD (69 secret containers) |
| Store | Google Secret Manager, per ADR-001 and the TASK-019 gate cell. Replication is user-managed and pinned to the in-Kingdom region (ADR-001 C-5) |
| Implements | CTL-18 ("rotation procedure documented and rehearsed"), CTL-51 |
| Status | **WRITTEN — REHEARSED IN CODE ONLY.** No environment exists yet (ADR-001 R-1 to R-3, UGV-07), so §4 has been rehearsed against the application, not against a provisioned store. See §9 |
| Record | [`docs/architecture/secret-management.md`](../../docs/architecture/secret-management.md) |

## 1. What this runbook is for

Replacing the value of one secret, in one environment, without an outage and without a deployment —
and putting the old value back when the new one turns out to be wrong.

It is not a schedule. **No rotation cadence is set anywhere**, and none is invented here: the cadence
is an AHDA value, recorded as G-3 in the cybersecurity control matrix and proposed for the
unassigned-gated-values register as **UGV-13** (§8). What this runbook does carry is the list of
events that require a rotation regardless of any schedule (§8).

## 2. Before you start

| Precondition | Why |
| --- | --- |
| You are the owner the Environment and Secrets sheet names for this variable, or acting with them | The sheet's Owner column is the authority for each secret. §7 lists them |
| You have `secretmanager.versions.add` on the secret, in that environment's project only | Access is per environment: no principal holds a grant in PROD and in a non-PROD project (CTL-02) |
| You have `gcloud` authenticated and `jq` on PATH | Every step below is one of the scripts in this directory |
| For PROD: the change is raised and approved as AHDA's change process requires | A PROD credential change is a production change |
| You know which pattern the variable follows (§3) | It decides whether there is a window in which the old value stops working |

The value never reaches a command line, a file or a shell history. Every script reads it from stdin,
and `printf %s` is used rather than `echo` so no trailing newline is stored:

```sh
printf %s "$new_value" | infra/secrets/rotate-secret.sh sit AD_BIND_PASSWORD
```

Prefer a pipe from the tool that generated the value, or a shell with history disabled
(`set +o history` in bash, `unsetopt HIST_SAVE` in zsh) for the command that reads it.

## 3. The two patterns

The application re-reads every secret it uses on a five-minute interval
(`SecretStoreOptions.RefreshInterval`), so a new value reaches running instances by itself. What
differs between secrets is what happens at the **source** of the credential.

**Pattern A — two live credentials (no window).** The source system can hold the old and the new
credential valid at the same time: a second API key, a second client secret, a second service
account, a second webhook. Order: create the new credential at the source → write it to the store →
soak → verify → revoke the old credential at the source → disable the superseded secret version.
Nothing is ever without a working credential.

**Pattern B — one live credential (bounded window).** The source holds one credential and changing
it invalidates the old one immediately — a password change is the usual case. Order: change at the
source and write to the store in the same step → the window lasts until running instances re-read
the store. In PROD, close the window immediately instead of waiting for it: redeploy the **same
image digest**, which restarts the instances and forces a fresh read.

```sh
gcloud run deploy "$service" --project="$project" --region="$region" --image="$digest"
```

**Which pattern applies is a property of the source system, not of this platform.** Where the
provider is not yet chosen — SMS (UGV-10), MFA (ADR-010), the malware scanner — §7 records the
pattern as *confirm at provider selection*, and confirming it is part of selecting the provider.

## 4. The procedure

`rotate-secret.sh` runs steps 2 to 5. Steps 1 and 6 are at the source system and are yours.

1. **Create or change the credential at the source.** Pattern A: create the second credential, leave
   the first one working. Pattern B: change it, and expect the old one to stop working now.

2. **Write the new value.** From stdin, as a new version. The previous version stays enabled.

   ```sh
   printf %s "$new_value" | infra/secrets/rotate-secret.sh <env> <VARIABLE_NAME>
   ```

   `latest` resolves to the most recent **enabled** version, so this one call switches the
   application over.

3. **Soak.** The script waits five minutes by default — at least one refresh interval, so every
   running instance has re-read the store. `--wait <seconds>` shortens it after a forced redeploy.

4. **Verify, before anything is retired.** The script prints the Verification Method the Environment
   and Secrets sheet states for this variable and waits for you to confirm it. Verify it for real:
   this is the step that makes the rest of the procedure safe, because the old value is still enabled
   and one command puts it back. Do not confirm on the absence of alerts.

   If it does not verify, answer `n`. Nothing is retired, and the script tells you to run §5.

5. **Retire the superseded version.** On confirmation the script disables the previous version. It
   **disables, never destroys**: a disabled version can be re-enabled, and how long a superseded
   secret is kept before destruction is governed by a retention period nobody has set (OQ-003).

6. **Revoke at the source (Pattern A only).** Delete or disable the credential you replaced, at the
   provider. Until you do, the old credential still works even though the platform no longer uses it.

7. **Record it.** §9.

## 5. Rollback

The new value does not work. The previous version is disabled or still enabled, depending on how far
§4 got; either way:

```sh
infra/secrets/rotate-secret.sh <env> <VARIABLE_NAME> --rollback
```

It enables the previous version first and disables the current one second, so at no point does the
secret have no enabled version. Running instances pick the restored value up within the refresh
interval; force it with the redeploy in §3 if the window matters.

Then, if you rotated under Pattern A, make sure the old credential at the source is still valid — if
step 6 already revoked it, rolling back the store is not enough and you are rotating forward to a
third credential, not backward.

## 6. Emergency rotation — a secret is believed to be exposed

1. Rotate now, by §4, with `--wait 0` and a forced redeploy. Do not wait for a change window; a PROD
   change record is written after the fact.
2. Revoke the exposed credential at the source immediately. The platform having stopped using it is
   not revocation.
3. Disable **every** version of that secret older than the new one, not only the one in use.
4. Treat the exposure itself as a security incident under TASK-093: how it was exposed decides
   whether other secrets are affected, and a secret exposed in a log, a screenshot or a ticket is
   usually not the only one.
5. If the exposure is a committed value, rotating is the fix; removing the commit is not. Run
   `infra/secrets/scan-history.sh` to find every place it appears, and rotate all of them.
6. Notify AHDA Cybersecurity. `SIEM_API_TOKEN`, `AD_BIND_PASSWORD` and anything under an AHDA IT
   owner in §7 are AHDA's credentials, not the delivery team's.

## 7. The secrets, their owners and their patterns

Owner and environment scope are the sheet's; the pattern is this runbook's.

| Variable | Owner (sheet) | Environments | Pattern | Notes |
| --- | --- | --- | --- | --- |
| `DB_CONNECTION_STRING` | DevOps/Platform Lead | DEV/SIT/UAT/PROD | B | Change the database user's password, then write the whole connection string. Pattern A is available at the cost of a second database user; TASK-020 decides. The connection string carries the pool ceiling: keep `Maximum Pool Size` × the environment's `max_instances` under the instance's connection limit (infrastructure-as-code.md F-7) — a rotation is where that value is most easily lost |
| `DB_BACKUP_STORAGE_CONNECTION_STRING` | DevOps/Platform Lead | SIT/UAT/PROD | A | Storage credentials can be issued in pairs; verify a backup job completes before revoking the old one (TASK-020) |
| `JWT_SIGNING_KEY` | Security Lead | DEV/SIT/UAT/PROD | B — see note | Rotating it invalidates every issued token, which is what the sheet's "rejected once rotated" describes. Until TASK-028 supports an overlapping previous key, rotate in a maintenance window and expect every session to end. F-3 |
| `AD_LDAP_URL` | AHDA IT (Directory Services) | SIT/UAT/PROD | A | An endpoint, not a credential: point at the new one, verify a bind, then retire the old |
| `AD_BIND_DN` | AHDA IT (Directory Services) | SIT/UAT/PROD | A | Rotate with `AD_BIND_PASSWORD` when moving to a new service account: write the DN first, then the password, then verify once |
| `AD_BIND_PASSWORD` | AHDA IT (Directory Services) | SIT/UAT/PROD | B, or A with a second service account | The sheet requires "rotation tested without downtime", which is Pattern A: ask AHDA IT for a second bind account |
| `SSO_OIDC_CLIENT_SECRET` | AHDA IT (Identity) | DEV/SIT/UAT/PROD | A where the IdP supports secret rollover | Confirm rollover support with AHDA IT (Identity); if it does not, this is Pattern B and every login in flight fails |
| `MFA_PROVIDER_API_KEY` | Security Lead | SIT/UAT/PROD | Confirm at provider selection | May not exist at all: if MFA comes from AHDA's IdP the variable is unnecessary (ADR-010) |
| `EXCHANGE_SMTP_USER` | AHDA IT (Messaging) | SIT/UAT/PROD | A | Rotate with the password when moving to a new relay account |
| `EXCHANGE_SMTP_PASSWORD` | AHDA IT (Messaging) | SIT/UAT/PROD | B | App-password style credentials are usually single; queue outbound mail through the window |
| `NAFATH_CLIENT_ID` | AHDA IT (Identity) / Security Lead | SIT/UAT/PROD | A | Classified Secret by the sheet even though it is an identifier; treat it as one |
| `NAFATH_CLIENT_SECRET` | AHDA IT (Identity) / Security Lead | SIT/UAT/PROD | Confirm with the Nafath platform | Verify in the sandbox first — the sheet's verification method is a sandbox round-trip |
| `DOCUMENT_STORAGE_CONNECTION_STRING` | DevOps/Platform Lead | DEV/SIT/UAT/PROD | A | Object-storage keys are issued in pairs; verify an upload **and** a download before revoking |
| `DOCUMENT_STORAGE_BACKUP_CONNECTION_STRING` | DevOps/Platform Lead | SIT/UAT/PROD | A | As above, against the backup target |
| `MALWARE_SCAN_API_KEY` | Security Lead | SIT/UAT/PROD | Confirm at provider selection | Verify with an EICAR file: a scanner that silently fails open passes every other check |
| `SIEM_API_TOKEN` | AHDA Cybersecurity | SIT/UAT/PROD | A | AHDA Cybersecurity issues it. Audit forwarding stops silently if it is wrong — verify an event lands |
| `RATE_LIMIT_STORE_CONNECTION_STRING` | DevOps/Platform Lead | SIT/UAT/PROD | B | Throttling degrades rather than fails; verify under load, not on one request |
| `APM_DSN` | DevOps/Platform Lead | SIT/UAT/PROD | A | Verify a test exception arrives before retiring the old DSN |
| `UPTIME_MONITOR_API_KEY` | DevOps/Platform Lead | SIT/UAT/PROD | A | Verify with a simulated outage, not by the absence of alerts |
| `ALERT_CHANNEL_WEBHOOK_URL` | DevOps/Platform Lead | SIT/UAT/PROD | A | Create the second webhook, verify a test alert arrives, then delete the first |
| `E2E_TEST_USER_CREDENTIALS_SECRET_REF` | QA Lead | DEV/SIT | A | A reference to synthetic accounts, never real AHDA credentials. Rotating the accounts themselves is a QA action (TASK-085) |
| `SMS_PROVIDER_API_KEY` | DevOps/Platform Lead | SIT/UAT/PROD | Confirm at provider selection | Provider is UGV-10; sender-ID registration is separate and is not rotated |
| `LEGACY_SYSTEM_CONNECTION_STRING` | AHDA IT / DevOps Lead | cutover window only | Not rotated — revoked | Created when the cutover window opens and revoked when it closes (TASK-102). §7.1 |

### 7.1 The cutover credential

`LEGACY_SYSTEM_CONNECTION_STRING` is the one secret with no standing environment, so
`inventory.py` does not put it in the manifest and no container exists for it. When TASK-102 opens
the cutover window, add the row's scope to the sheet, run `inventory.py --write`, provision the
container, write the value — and, at the close of the window, disable every version and confirm with
the sheet's own verification method that the access is revoked. Leaving a read-only legacy credential
enabled after cutover is the failure this row exists to prevent.

## 8. When to rotate

No periodic cadence is set. **Proposed UGV-13** — secret and key rotation cadence, owner AHDA
Cybersecurity, gate Before Production (control matrix G-3). Until it is set, these triggers stand on
their own and none of them waits for a schedule:

| Trigger | Scope |
| --- | --- |
| A value is believed to be exposed — in a log, a screenshot, a ticket, a commit, a shared file | That secret, in every environment it exists in (§6) |
| Someone with access to a secret leaves the engagement or changes role | Every secret they could read, in the environments they could reach |
| A provider requires it, or a credential is approaching expiry | That secret |
| After a security incident, on AHDA Cybersecurity's instruction | As instructed |
| The cutover window closes | `LEGACY_SYSTEM_CONNECTION_STRING`, revoked (§7.1) |
| Before the production handover | Every secret whose value was ever set by the delivery team and is meant to be AHDA's (TASK-097) |

## 9. Evidence

A rotation is a change to a production system and leaves a record. For each one, record: the
variable, the environment, who ran it, when, which version number superseded which, the verification
that was performed, and — for PROD — the change reference. The secret store keeps the version
numbers and their timestamps; the rest belongs in AHDA's change record.

**What has and has not been rehearsed.** The procedure in §4 has been rehearsed against the
application: `SecretStoreTests.ARotatedSecretReachesARunningApplication` changes a value in the store
and asserts the running configuration follows it, with no restart and no code change, and the test
fails when the refresh is disabled. It has **not** been rehearsed against a provisioned Secret
Manager namespace, because no environment exists (ADR-001 R-1 to R-3, UGV-07). The first rehearsal
against DEV is a release-checklist item, and `infra/secrets/verify-secret-integration.sh dev` is the
check to run before it.

## 10. Findings

| # | Finding | Owner |
| --- | --- | --- |
| **F-1** | **No rotation cadence exists.** Proposed as UGV-13 (§8). Until it is registered, rotation is trigger-driven only, and "rotation procedure documented and rehearsed" (CTL-18) is met while "rotated every N days" is not claimed | PMO (register); AHDA Cybersecurity (value) |
| **F-2** | **Four variables have no confirmed rotation pattern** — `MFA_PROVIDER_API_KEY`, `MALWARE_SCAN_API_KEY`, `SMS_PROVIDER_API_KEY`, `NAFATH_CLIENT_SECRET` — because the provider is not selected or its capability is unknown. Confirming the pattern is part of selecting the provider | DevOps/Platform Lead; Security Lead |
| **F-3** | **Rotating `JWT_SIGNING_KEY` ends every session.** An overlapping previous-key window is a TASK-028 design decision, not a runbook step. Until it exists, the rotation is a maintenance-window operation | Security Lead; TASK-028 |
| **F-4** | **The access model for who may read and write PROD secrets is not accepted in writing.** CTL-51 requires it, including for delivery-vendor staff outside the Kingdom over the operations period (ADR-001 C-9). The scripts enforce per-environment scope; who holds that scope is AHDA's decision | AHDA Cybersecurity |

# Secret management — the approved store, and how the application reads it

| Field | Value |
| --- | --- |
| Task | TASK-019 — Integrate Approved Secret Management Store (P3 - Infrastructure & DevOps) |
| Depends on | TASK-017 — Infrastructure as Code (`docs/architecture/infrastructure-as-code.md`, BUILT — NOT APPLIED); TASK-016 — Environment separation |
| Record date | 2026-09-23 |
| Status | **BUILT — NOT APPLIED.** The inventory, the application integration, the write and rotation path, the verification script and the runbook are written, and the rotation is rehearsed in test. Nothing is provisioned: no environment exists while ADR-001 R-1 to R-3 (UGV-07) are open, so no container has been created and no value has been written |
| Branch | `sec/task-019-secret-management-integration` |
| Deliverables | `infra/secrets/` — `inventory.py`, `lib.sh`, `set-secret-version.sh`, `rotate-secret.sh`, `verify-secret-integration.sh`, `scan-history.sh`, `README.md`, `secret-rotation-runbook.md`; `src/backend/PMPlatform.Infrastructure/Secrets/` — the integration module; `docs/architecture/secret-management-check.py` wired into `repo-checks`; `.gitleaks.toml`; this record |
| Environment variables / secrets | `SECRET_STORE_ENDPOINT` (Public, per environment, derived by Terraform from the manifest) and `SECRET_STORE_AUTH_TOKEN` (the bootstrap credential — unset on Cloud Run, where the runtime service account's own token is used). No other secret is configured anywhere but the store |
| Implements | CTL-18; and the parts of CTL-02, CTL-48, CTL-51 and CTL-52 that concern secrets. ADR-001 §7 C-5 |
| Gate decision applied | "UNBLOCKED by ADR-001. Use the approved GCP secret-management service unless AHDA IT nominates an alternative." Google Secret Manager it is; §6 is what changes if AHDA IT nominates another |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), sheets Implementation Plan (TASK-019 and its neighbours), Environment and Secrets (all 43 rows), Architecture Decisions, as read 2026-09-23 |
| Controlled sources and authority order | per TASK-001 |

## 1. What this record is

TASK-019's acceptance criteria are three: a repository-wide secret scan finds zero committed
secrets; application configuration reads every secret at runtime from the secret store, never from a
checked-in file; and the rotation procedure is documented. §8 answers each one. This record explains
the three decisions the criteria do not make for you — where the inventory of secrets comes from
(§2), how the application reaches the store (§3), and how a value gets in and out of it (§4) — and
records what changed in TASK-017's work as a result (§5).

## 2. The inventory is derived, not written

The Environment and Secrets sheet has 43 rows. They do not all belong in the secret store, and which
ones do is decided by the **Storage Location** column, not by the Secret/Public classification:

| Route | Rows | Where they live |
| --- | --- | --- |
| `secret-store` | 23 | The approved secret-management platform. 69 containers, because scope differs per row |
| `cicd` | 2 | `DEPLOY_SERVICE_ACCOUNT_KEY`, `CONTAINER_REGISTRY_TOKEN` — the CI/CD platform's own store (CTL-48). The pipeline needs them before any environment exists, so they cannot live in a thing the pipeline creates |
| `bootstrap` | 2 | `SECRET_STORE_ENDPOINT` and `SECRET_STORE_AUTH_TOKEN`. The credential that opens the store cannot be kept inside it |
| `config` | 16 | Public rows — the environment's configuration store |

`infra/secrets/inventory.py` applies that routing to the sheet snapshot and writes the resulting
per-environment entries into `infra/environments/environments.json`, building each id as
`{prefix}{lower-kebab name}` from the namespace prefix TASK-016 recorded. The manifest stays the
single source of truth for identifiers; what changed is that its secret entries are now **derived**
from the sheet instead of being the three TASK-016 seeded it with.

Three properties follow, and each is enforced rather than asserted:

- **A row added to the sheet cannot be forgotten.** `inventory.py` refuses any Storage Location it
  does not recognise and any Environment Scope that names no environment and is not one of the two
  recognised non-standing scopes. A new row with new wording fails the build; it does not fall
  through into "not a secret".
- **A sheet that contradicts itself is raised, not resolved.** A row classified Secret whose storage
  is the configuration store — or the reverse — stops the check. Which column is wrong is a question
  for the sheet's owner.
- **The manifest cannot drift.** `environment-separation-check.py` S-6 and S-7 compare it to the
  sheet on every pull request, through the same module that wrote it.

`LEGACY_SYSTEM_CONNECTION_STRING` is the one secret-store row with no standing environment — its
scope is "cutover window only" — so no container is declared for it. The runbook §7.1 has the
procedure for creating and revoking it around the cutover window.

## 3. How the application reads a secret

**The application reads the store itself.** It is not handed secret values by the deployment
platform. That is what the TASK-019 row's Environment Variables cell describes — the application
needs `SECRET_STORE_ENDPOINT` and `SECRET_STORE_AUTH_TOKEN` — and what the sheet's verification
method for both says: "Application successfully retrieves a test secret at startup".

```
SECRET_STORE_ENDPOINT = https://secretmanager.googleapis.com/v1/projects/ahda-pmplatform-dev/secrets/pmplatform-dev-
                        └─────────────── the store ───────────────┘└─ the project ─┘        └ the namespace ┘
```

The endpoint is the base address of the environment's namespace, **including the id prefix**, and a
variable's address is that string with its lower-kebab name appended. One value therefore carries the
store, the project and the namespace, and the application needs no variable the sheet does not name —
the rule TASK-017 set for itself and this task inherits.

`SecretStoreConfigurationProvider` is an ordinary .NET configuration provider, registered **last** so
a secret resolves to the store's value wherever else it is configured. It:

- loads every key in `ApplicationSecrets.Keys` at start-up, and **fails the application by name** if
  one has no value — the sheet's stated verification method for `DB_CONNECTION_STRING`;
- re-reads them every five minutes, so a rotated value reaches a running instance without a
  deployment and without a code change (§8, criterion 2);
- keeps the values it has when a refresh fails, and tries again on the next tick, so a store that is
  briefly unreachable does not empty a running instance's configuration;
- holds values in memory only. They are never logged, never written to disk, and never put into an
  exception message — `AFailedReadNamesTheVariableAndNotItsValue` asserts the last of those.

Authentication is the runtime service account's own identity, fetched from the compute platform's
metadata server: on Cloud Run `SECRET_STORE_AUTH_TOKEN` has no value at all, which is exactly what
the sheet asks for ("injected via deployment platform identity/role, never a static file"). The
bootstrap token is used only where a runtime has no platform identity.

`ApplicationSecrets.Keys` holds one key today — `DB_CONNECTION_STRING`, the only secret any code
reads. A key is added by the task that first reads it, because the provider *requires* every key on
that list at start-up and listing a variable early would refuse to start an environment over a value
nothing needs. The containers for the other 22 variables exist and are read the same way from the
moment their feature lands; `secret-management-check.py` K-1 fails the build if a key is added to
that list without a container in all four environments.

### 3.1 Why not Cloud Run's own secret injection

Cloud Run can mount a Secret Manager version into an environment variable, and TASK-017 wired it that
way. Two reasons it is not what ships:

1. **Rotation would need a deployment.** The value is resolved when the instance starts, so a rotated
   secret reaches the application only on a new revision. The TASK-019 validation cell asks for the
   opposite: "confirm the running application picks up the new value without a code change".
2. **The store would not be replaceable.** The gate cell allows AHDA IT to nominate an alternative
   platform. With injection, that nomination changes the deployment of every environment; with an
   `ISecretStore` behind a configuration provider, it is one adapter (§6).

The cost is that the application holds a credential-fetching path of its own. It is bounded: one
interface, one HTTP call per secret per refresh, and an identity that is the environment's own
service account rather than a credential anyone stores.

## 4. How a value gets in, and out

No value is ever a command argument, because a process's arguments are readable by anyone who can
list processes and because `common.sh`'s `run()` echoes every argument it is given. Values reach
`gcloud` on stdin.

| Step | Command | What it does |
| --- | --- | --- |
| Write | `printf %s "$v" \| infra/secrets/set-secret-version.sh <env> <VAR>` | Adds a version. Refuses a variable the inventory does not hold in that environment, and an empty value |
| Rotate | `printf %s "$v" \| infra/secrets/rotate-secret.sh <env> <VAR>` | New version → soak → the sheet's own verification method → retire the previous version |
| Roll back | `infra/secrets/rotate-secret.sh <env> <VAR> --rollback` | Enables the previous version, then disables the current one |
| Verify | `infra/secrets/verify-secret-integration.sh <env>` | Read-only: containers, in-Kingdom replication, enabled versions, IAM, and what the deployed service was given |

`latest` resolves to the most recent **enabled** version, which is what makes both directions one
call and neither of them destructive. Superseded versions are disabled, never destroyed: how long one
is kept is a retention period nobody has set (OQ-003), so nothing here destroys anything.

The rotation runbook — `infra/secrets/secret-rotation-runbook.md` — carries the rest: the two
patterns a rotation can follow depending on whether the *source* system can hold two credentials at
once, the per-secret owners and patterns, emergency rotation, and what evidence a rotation leaves.

## 5. What this changed in TASK-017's work

| Change | Why |
| --- | --- |
| `infra/terraform/compute` no longer takes `secret_environment`, and the Cloud Run template has no `value_source` block | §3.1. The runtime is given the store's address; it reads the values itself |
| `infra/terraform/platform` derives `SECRET_STORE_ENDPOINT` from the manifest and passes it in `plain_environment` | One address per environment, built from the project and namespace the manifest already holds |
| `infra/environments/environments.json` carries 69 secret entries instead of 6 | §2. `provision-environment.sh` already looped over them, so it now creates every container and grants the runtime service account read access on each — its code did not change |
| `environment-separation-check.py` S-6 and S-7 validate the inventory against the sheet instead of a fixed list of three | The fixed list was TASK-016's seed, not a rule |

`secret-management-check.py` K-2 fails the build if a `secret_key_ref`, a `value_source` or a
`google_secret_manager_secret_version` reappears anywhere in `infra/terraform`: the first is injection
coming back, the last is Terraform writing a secret value into state.

## 6. If AHDA IT nominates a different store

The gate cell's escape clause is real, and the seam for it is `ISecretStore` — one method, taking a
Variable Name. A different platform is a second implementation of it plus the endpoint format in
`SECRET_STORE_ENDPOINT`; `SecretStoreConfigurationProvider`, the application, the inventory, the
manifest and the runbook's procedure are unchanged. What does change is `inventory.py`'s id rule if
the alternative namespaces secrets differently, and the four scripts, which are `gcloud`-specific.

## 7. Verification

| What | How | Result |
| --- | --- | --- |
| Full-history secret scan | `infra/secrets/scan-history.sh` — gitleaks 8.30.1 over `--all --full-history` (34 commits, 1.37 MB) and over the working tree (4.34 MB) | **Zero.** Five findings on first run were `idempotencyKey` values in the API and event documentation samples — row identifiers by `event-conventions.md` EV-4, not credentials — and are allowlisted by rule, path and line shape in `.gitleaks.toml` |
| The allowlist is not a blanket | A genuine AWS access key and a GitHub token planted in one of the allowlisted files | Both found; only the `idempotencyKey` finding suppressed |
| A rotated secret reaches a running application | `SecretStoreTests.ARotatedSecretReachesARunningApplication` | Passes. Mutation-tested: disabling the refresh timer fails it and `AFailedRefreshKeepsTheValueAlreadyInUse` |
| The id the application asks for is the id the manifest holds | `SecretStoreTests.EverySecretIdInTheManifestIsTheOneTheApplicationAsksFor`, over all 69 entries | Passes |
| An application whose secret is unset does not start | `SecretStoreTests.AnApplicationWhoseSecretIsUnsetDoesNotStart` | Passes |
| A failed read does not carry the value | `SecretStoreTests.AFailedReadNamesTheVariableAndNotItsValue` | Passes |
| The repository's own invariants | `python3 docs/architecture/secret-management-check.py`, plus the five existing `repo-checks` | All pass |
| Build and unit tests | `dotnet build -warnaserror`, `dotnet test` | 33 tests, 0 failures |

What could not be verified, and why: everything that needs a provisioned environment — that a
container exists, that its replication names only `me-central2`, that only the runtime service
account can read it, that the deployed service carries the right endpoint, and a rotation against the
real store. No environment exists while UGV-07 is open. `verify-secret-integration.sh` is written for
exactly those checks and is the first thing to run against DEV.

## 8. Acceptance criteria

| Criterion | Status |
| --- | --- |
| A repository-wide secret scan finds zero committed secrets | **Met.** §7, row 1. TASK-080 owns the standing gate (CTL-42); this is the scan, run |
| Application configuration reads every secret at runtime from the secret store, never from a checked-in file | **Met for every secret the application reads** — one today, `DB_CONNECTION_STRING`, and the mechanism is the same for the other 22 as their features land (§3). The deployment injects no value; no checked-in file holds one; local development is the only case that may run without the store, on a git-ignored file that holds no AHDA secret |
| Secret rotation procedure is documented | **Met.** `infra/secrets/secret-rotation-runbook.md`, rehearsed in test (§7) and not yet against a provisioned store (§9 F-2) |
| Validation: rotate one test secret end to end and confirm the running application picks up the new value without a code change | **Met in test, not in an environment.** §7, row 3 |
| Validation: run a secret scanner against the full git history | **Met.** §7, row 1 |

## 9. Findings and open items

| # | Finding | Owner |
| --- | --- | --- |
| **F-1** | **The control plane is a global API; only the payload is pinned in Kingdom.** Secrets are created with user-managed replication to `me-central2`, so the secret material is stored only in Saudi Arabia (ADR-001 C-5). The Secret Manager *API* that serves a read is Google's global endpoint. If AHDA Cybersecurity requires the control plane to be in-Kingdom too, the containers become regional secrets — a change to `provision-environment.sh`, to `SECRET_STORE_ENDPOINT`'s host, and a re-check of whether every consumer supports them | AHDA Cybersecurity (requirement); DevOps/Platform Lead (change) |
| **F-2** | **The rotation has not been rehearsed against a provisioned store.** CTL-18 asks for "documented and rehearsed". The procedure is documented and rehearsed against the application; the rehearsal against DEV is blocked by UGV-07 and is a release-checklist item | DevOps/Platform Lead, once DEV exists |
| **F-3** | **No rotation cadence exists.** Control matrix G-3; proposed as **UGV-13**, owner AHDA Cybersecurity, gate Before Production. Rotation is trigger-driven until it is registered (runbook §8) | PMO (register); AHDA Cybersecurity (value) |
| **F-4** | **Who may read and write PROD secrets is not accepted in writing.** CTL-51, including delivery-vendor access from outside the Kingdom (ADR-001 C-9). The scripts and the IAM model enforce per-environment scope; who holds that scope is AHDA's decision | AHDA Cybersecurity |
| **F-5** | **Four secrets have no confirmed rotation pattern**, because their provider is not selected or its capability is unknown: `MFA_PROVIDER_API_KEY` (ADR-010), `MALWARE_SCAN_API_KEY`, `SMS_PROVIDER_API_KEY` (UGV-10), `NAFATH_CLIENT_SECRET`. Runbook §7, F-2 | DevOps/Platform Lead; Security Lead |
| **F-6** | **`.gitleaks.toml` is here but the gate is not.** TASK-080 / CTL-42 owns scanning per pull request, on a schedule, and as a required status check, and owns this file from the point it lands. Until then nothing prevents a secret being committed between scans | TASK-080 |
| **F-7** | **Rotating `JWT_SIGNING_KEY` ends every session.** An overlapping previous-key window is a TASK-028 decision. Runbook §7, F-3 | Security Lead; TASK-028 |

## 10. Change log

| Date | Change |
| --- | --- |
| 2026-09-23 | Record created with the module, the runbook, the check and the scan (TASK-019) |

# Secret-store integration (TASK-019)

Every secret the platform uses is held in the approved secret-management platform — Google Secret
Manager, per ADR-001 and the TASK-019 gate cell — and is read from it at runtime. No secret value is
in this repository, in a configuration file, in a container image, in Terraform state or in a log.

| File | What it is |
| --- | --- |
| `inventory.py` | Derives the per-environment secret inventory from the Environment and Secrets sheet and writes it into `../environments/environments.json`. `--check` fails when the two disagree. |
| `lib.sh` | Shared loader for the scripts below. Sourced, never executed. |
| `set-secret-version.sh` | Writes one value into the store, from stdin. The owner named in the sheet runs it. |
| `rotate-secret.sh` | Rotation and rollback, end to end: new version, soak, verify, retire the old one. |
| `verify-secret-integration.sh` | Read-only check of a provisioned environment: containers, replication, versions, IAM, and what the deployed service was given. |
| `rehearse-rotation.sh` | Rehearses the rotation end to end against the running API, with `stub-store.py` standing in for the store. Takes about six minutes: it waits out a real refresh interval. |
| `stub-store.py` | The stand-in, for that rehearsal only. Loopback, plain file, no versions, no IAM — not a secret store. |
| `scan-history.sh` | Scans the full git history and the working tree for committed secrets. Evidence, not a gate — the gate is TASK-080. |
| `secret-rotation-runbook.md` | The rotation runbook: patterns, procedure, rollback, emergency rotation, per-secret owners. |

The application side is `src/backend/PMPlatform.Infrastructure/Secrets/`, and the record that explains
the whole design is [`docs/architecture/secret-management.md`](../../docs/architecture/secret-management.md).

## The one thing to know

`SECRET_STORE_ENDPOINT` is the base address of an environment's secret namespace, **including the id
prefix**:

```
https://secretmanager.googleapis.com/v1/projects/ahda-pmplatform-dev/secrets/pmplatform-dev-
```

A variable's address is that string with its lower-kebab name appended. `DB_CONNECTION_STRING` in DEV
is `…/secrets/pmplatform-dev-db-connection-string`. One value therefore carries the store, the
project and the namespace, and the application needs no variable the sheet does not name.

The id rule lives in exactly two places — `inventory.py` writes the ids, `GoogleSecretManagerStore`
asks for them — and `SecretStoreTests.EverySecretIdInTheManifestIsTheOneTheApplicationAsksFor`
asserts they agree on all 69 entries.

## Adding a secret

1. Add the row to the Environment and Secrets sheet, and refresh the snapshot at
   `docs/architecture/environment-and-secrets.csv`.
2. `python3 infra/secrets/inventory.py --write` — the manifest gains one entry per environment the
   sheet scopes it to.
3. `infra/environments/provision-environment.sh <env>` creates the container and grants the
   environment's runtime service account read access. It creates no value.
4. The owner the sheet names writes the value:
   `printf %s "$value" | infra/secrets/set-secret-version.sh <env> <VARIABLE_NAME>`.
5. Add the key to `ApplicationSecrets.Keys` in the task that first reads it.

`python3 docs/architecture/secret-management-check.py` fails the pull request if step 2 or step 5 is
missing.

## What is deliberately not here

- **Secret values.** `.gitignore` excludes everything under `infra/secrets/` except this README, the
  code and the runbook. Nothing writes a value to disk.
- **`DEPLOY_SERVICE_ACCOUNT_KEY` and `CONTAINER_REGISTRY_TOKEN`.** They live in the CI/CD platform's
  own secret store (CTL-48, TASK-018): the pipeline needs them before any environment exists.
- **`SECRET_STORE_AUTH_TOKEN`.** The credential that opens the store cannot be kept inside it. On
  Cloud Run it does not exist as a value at all — the runtime service account's token comes from the
  metadata server. It is set only where a runtime has no platform identity.
- **The secret-scanning gate.** TASK-080 / CTL-42 owns scanning per pull request and on a schedule,
  and owns `.gitleaks.toml` from the point it lands.

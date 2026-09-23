# Environment separation (TASK-016)

Four isolated environments — DEV, SIT, UAT, PROD — one GCP project each, inside AHDA's own GCP
organisation, all in the named in-Kingdom region. Nothing here creates an application resource;
this directory creates the **boundary** each environment lives inside, and the tasks that create
resources take their names from `environments.json`.

Full record, topology diagram and promotion path: [`docs/architecture/environment-separation.md`](../../docs/architecture/environment-separation.md).

## Files

| File | What it is |
| --- | --- |
| `environments.json` | The source of truth. Four environments, their projects, networks, database instances, secret-store namespaces, state buckets, service accounts and the variables each environment holds. The secret entries are derived from the Environment and Secrets sheet by `infra/secrets/inventory.py` (TASK-019). Holds **no value of any kind** for a secret. |
| `apply-org-policy.sh` | Creates the two folders and binds `constraints/gcp.resourceLocations` and the default log-bucket location to them. Run **first**: both are fixed for every project created afterwards. |
| `provision-environment.sh` | Creates one environment: project, state bucket, secret containers, service accounts, per-project IAM. |
| `verify-separation.sh` | The separation drills: a DEV credential against the SIT database, PROD secrets from non-PROD principals, cross-environment IAM, and resource locality. |
| `github/environments.json`, `github/apply.sh` | The four GitHub deployment environments and their approval gates. TASK-018's pipeline deploys through them. |
| `lib/common.sh` | Manifest loader and the guards every script shares. |

The static invariants of `environments.json` are checked in CI by
`docs/architecture/environment-separation-check.py` (`repo-checks` job).

## Order of operations

```sh
export ADR001_CONFIRMATION_REF="…"            # the written confirmation of ADR-001 R-1/R-2/R-3

infra/environments/apply-org-policy.sh --dry-run          # then without --dry-run
for env in dev sit uat prod; do
  infra/environments/provision-environment.sh "$env" --dry-run
done
infra/environments/github/apply.sh
infra/environments/verify-separation.sh
```

Every script accepts `--dry-run` except `verify-separation.sh`, which only reads. A dry run prints
the exact `gcloud` calls and makes none of them, and needs nothing but `jq`.

## Three rules the scripts enforce rather than document

1. **No region but the named in-Kingdom one.** `platform.region` is empty until AHDA IT names it
   (UGV-07), and every script refuses to run while it is. A region that is not on
   `platform.region_allowlist` is refused — `me-central1` is Doha, Qatar, not Dammam.
2. **No apply before the written confirmation.** `ADR001_CONFIRMATION_REF` must name the document
   that closes ADR-001 R-1, R-2 and R-3 (ADR-001 §9), and it is echoed into the run log.
3. **No secret value passes through this directory.** The scripts create empty secret containers;
   versions are added by the owner the Environment and Secrets sheet names, under TASK-019.

## What other tasks own

| Resource | Task |
| --- | --- |
| VPC, subnets, firewall rules, load balancer, WAF | TASK-021 |
| Database instance, backups, encryption | TASK-020 |
| Terraform modules and the runtime (Cloud Run or GKE, unresolved) | TASK-017 |
| Secret **values**, rotation, application integration | TASK-019 |
| The pipeline that deploys through the gates | TASK-018 |

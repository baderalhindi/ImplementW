# Infrastructure as code (TASK-017)

Every environment's network, compute, managed database, object storage and WAF/load balancer,
codified. Nothing here is created by hand: `terraform apply` from a clean state produces the whole
environment, and a console change shows up as a diff on the next plan.

Full record, the compute decision and the open items: [`docs/architecture/infrastructure-as-code.md`](../../docs/architecture/infrastructure-as-code.md).
The boundary these resources are created inside — the projects, folders, state buckets, secret
containers and service accounts — is TASK-016's:
[`infra/environments/`](../environments/README.md).

## Layout

```
infra/terraform/
├── network/         VPC, subnets, private services access, firewall   (TASK-021 owns the rules)
├── compute/         Cloud Run service, Direct VPC egress               (ADR-001 R-6, provisional)
├── database/        Cloud SQL for PostgreSQL 17, backups, PITR        (TASK-020 owns the drills)
├── storage/         document and backup buckets                        (TASK-037, TASK-023)
├── load-balancer/   regional external ALB, Cloud Armor, TLS 1.2+      (TASK-021 owns the WAF baseline)
├── platform/        the composition: guards, and the five wired together
├── environments/    one root per environment — dev, sit, uat, prod
├── verify-idempotency.sh   the validation cell: validate, scan, plan, plan again
└── compare-plans.py        what V-4 compares two plans with
```

A root is a state backend, a provider and one call to `platform`. Everything structural is in
`platform/` and is identical in all four environments; everything that differs is in that root's
`terraform.tfvars`, so two environments diff in one screen.

## Where values come from

`infra/environments/environments.json` (TASK-016) is the single source of truth for the region,
the organisation, and every environment's project, network, database instance, secret ids and
service accounts. `platform/` reads it. **No hosting value is written into the Terraform**, and
`docs/architecture/terraform-check.py` fails the pull request if one appears.

The two exceptions are both structural:

| Value | Why it is not read from the manifest |
| --- | --- |
| The state bucket in each root's `backend.tf` | A `backend` block takes no variables. The check compares all four against the manifest. |
| The image in `container_image` | It is a property of the release, not of the environment. One artifact is built once and promoted unchanged (TASK-018), so the pipeline supplies `TF_VAR_container_image` at apply time and no digest is committed. |

## Running it

```sh
export TF_VAR_container_image="<region>-docker.pkg.dev/<project>/pmplatform/api@sha256:<digest>"

terraform -chdir=infra/terraform/environments/dev init
terraform -chdir=infra/terraform/environments/dev plan
terraform -chdir=infra/terraform/environments/dev apply
```

Order: `infra/environments/apply-org-policy.sh`, then `provision-environment.sh <env>`, then this.
The locality policy and the log-bucket location are fixed at project creation and cannot be
corrected afterwards, and this configuration has nowhere to put its state until that project and
its state bucket exist.

**Nothing has been applied.** The plan refuses before it reaches the provider, and says why:

| Guard (`platform/guards.tf`) | Owed by |
| --- | --- |
| `platform.region` is empty | AHDA IT — UGV-07, ADR-001 R-2 |
| the region is not on `platform.region_allowlist` | — `me-central1` is Doha, Qatar |
| `platform.organization_id` is empty | AHDA IT — UGV-07, ADR-001 R-3, CTL-47 |
| `APP_BASE_URL` is empty | AHDA IT — environment-separation.md F-1, proposed UGV-12 |
| PROD has no `retained_backups` | AHDA Cybersecurity and Records — OQ-003 / PTBC-048 |

## Checks

| Check | Where |
| --- | --- |
| `terraform fmt -check -recursive`, `init -lockfile=readonly`, `validate` on all four roots | `terraform` job, `.github/workflows/ci-quality-gates.yml` |
| Trivy config scan, zero HIGH/CRITICAL | same job |
| Manifest agreement, no duplicated hosting value, no committed credential, pinned toolchain, multi-platform lock files, no shared address range, nothing that would make a plan non-deterministic | `docs/architecture/terraform-check.py`, `repo-checks` job |
| The workbook's validation cell, end to end | `./verify-idempotency.sh <env>` — below |

### The validation cell

```sh
export TF_VAR_container_image="…"
infra/terraform/verify-idempotency.sh sit
```

| | Check | Runs today |
| --- | --- | --- |
| V-1 | `terraform validate` | yes |
| V-2 | static security scan, zero HIGH/CRITICAL | yes |
| V-3 | `terraform plan` on the current state reports no changes | **no — needs an applied environment** |
| V-4 | two consecutive plans describe the same changes | **no — needs an applied environment** |

V-3 and V-4 **exit non-zero when they cannot run**. An unexecuted check is not a passed check, and
the acceptance criterion is not met by a script that would have proved it.

V-4 compares `terraform show -json`, not the text, through `compare-plans.py`: Terraform stamps
every plan with a `timestamp` and emits `relevant_attributes` in map-iteration order, so a raw
`diff` reports a difference on every run and proves nothing. The comparator ignores those two and
compares what a plan asserts — `resource_changes`, `planned_values`, `prior_state`, `configuration`,
`variables`, `output_changes`.

**On tfsec.** The workbook names it as an example. It was archived in 2023, and its
`google-sql-encrypt-in-transit-data` rule requires `require_ssl` — an argument the provider has
since removed, so setting it fails `terraform validate`. The module uses the stricter successor
`ssl_mode = "ENCRYPTED_ONLY"`. Trivy carries the same rule set forward and reports zero; it is what
CI and V-2 run. Detail: [infrastructure-as-code.md §5.4](../../docs/architecture/infrastructure-as-code.md).

`.terraform.lock.hcl` is committed in every root and covers `linux_amd64`, `darwin_arm64` and
`darwin_amd64`. Regenerate it with `terraform providers lock -platform=…` — not with a bare
`init`, which records only the machine that ran it.

## Credentials

There are none in this directory and there never will be. The provider takes its credentials from
the environment (`GOOGLE_OAUTH_ACCESS_TOKEN`, or Workload Identity Federation in CI), and
`DEPLOY_SERVICE_ACCOUNT_KEY` lives in the CI/CD platform's own secret store. The check rejects a
`credentials =` argument, a key file or a literal password anywhere under `infra/terraform`.

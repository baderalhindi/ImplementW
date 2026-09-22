# Infrastructure as Code — the AHDA-approved hosting target

| Field | Value |
| --- | --- |
| Task | TASK-017 — Author Infrastructure as Code for Approved Hosting Target (P3 - Infrastructure & DevOps) |
| Depends on | TASK-016 — Environment separation (`docs/architecture/environment-separation.md`, BUILT — NOT APPLIED) |
| Record date | 2026-09-22 |
| Status | **BUILT — NOT APPLIED.** The five modules, the composition and the four environment roots are written, formatted, schema-validated against the provider, planned end to end against a placeholder manifest, and scanned with zero HIGH/CRITICAL findings. Nothing is provisioned: the region (ADR-001 R-2), the tenancy (R-3), the written confirmation (R-1) and the DNS zone (environment-separation.md F-1) are outstanding, and the plan refuses while they are (§3.4, §5) |
| Branch | `infra/task-017-infrastructure-as-code` |
| Deliverables | `infra/terraform/{network,compute,database,storage,load-balancer}/` — the five modules; `infra/terraform/platform/` — the composition and the preflight guards; `infra/terraform/environments/{dev,sit,uat,prod}/` — four roots with committed lock files; `infra/terraform/verify-idempotency.sh` and `compare-plans.py` — the validation cell, executed; `infra/terraform/README.md`; `docs/architecture/terraform-check.py` wired into `repo-checks`; the `terraform` CI job and its branch-protection entry; this record |
| Environment variables / secrets | None held here. The provider takes its credentials from the environment; `DEPLOY_SERVICE_ACCOUNT_KEY` stays in the CI/CD platform's own secret store (CTL-48). `TF_VAR_container_image` is supplied at apply time by TASK-018 |
| Implements | CTL-05, CTL-48; and the codified form of CTL-01, CTL-03, CTL-04, CTL-17, CTL-20, CTL-28, CTL-35 (`cybersecurity-control-matrix.md`); ADR-001 §7 C-1, C-2, C-3, C-4, C-6; ADR-002 §4.2.3 |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), sheets Implementation Plan (TASK-017 and the P3 rows around it), Environment and Secrets, Architecture Decisions, Open Questions, as read 2026-09-22 |
| Controlled sources and authority order | per TASK-001 |

## 1. What this record is

TASK-017's acceptance criterion is that 100% of environment infrastructure is created by
`terraform apply` from a clean state with zero manual console changes, that a plan on the current
state produces no unexpected diff, and that the IaC is peer reviewed like application code.

The task sits between two others that already own most of what it touches. TASK-016 created the
*boundary* — the projects, folders, state buckets, secret containers and service accounts — and
declared the names everything inside an environment must use. TASK-020 and TASK-021 depend on this
task and own the database's encryption and backup drills and the network's WAF baseline and
segmentation evidence respectively. What TASK-017 owns is the **codification**: the modules those
two tasks configure, the composition that wires them, and the machinery that makes "created by
`terraform apply`, reviewed like code" enforceable rather than asserted.

Two things shape the result:

- **The hosting parameters are still not known.** The region and the tenancy are UGV-07, the DNS
  zone has no owner yet, and ADR-001 §9 holds every apply — DEV included — until the written
  confirmation closes. As in TASK-016, none of that is invented. Five preconditions fail the plan
  before it reaches the provider, each naming the item that owes the value (§3.4).
- **One decision could not be deferred.** ADR-001 R-6 — Cloud Run or GKE — is open, has no owner,
  and ADR-002 §4.3 declined to take it, recording as S-7 that "TASK-017 cannot codify compute".
  A compute module is a named deliverable of this task. §4 says what was done about that.

## 2. The gate

TASK-017's Gate Decision cell:

> UNBLOCKED by ADR-001. Target is GCP, in-Kingdom. Region name to be supplied by AHDA IT before the
> first apply.

| Item | State | Effect here |
| --- | --- | --- |
| ADR-001 decision (GCP, in-Kingdom) | Approved at the AHDA gate, 19 Sep 2026 | The substrate is GCP. The provider is `hashicorp/google`, pinned. |
| ADR-002 §4.2.3 — managed PostgreSQL product | **Settled**: Cloud SQL for PostgreSQL 17, fallback 16, unless AHDA IT requires AlloyDB. This closed ADR-001 R-7 | `database/` is a Cloud SQL module. Rule (a) of that section — standard PostgreSQL only — means an AlloyDB answer costs this module's resource type and nothing above it. |
| ADR-001 R-6 — compute runtime | **Open**, no owner (ADR-002 S-7) | §4. |
| R-1 — written confirmation | Outstanding | Not a Terraform guard: the hold lives in `infra/environments/lib/common.sh`, which runs first and creates the project this configuration needs. |
| R-2 — the in-Kingdom region name (UGV-07) | Outstanding | `platform.region` is empty; two preconditions fail the plan (§3.4). |
| R-3 — tenancy (UGV-07) | Outstanding | `platform.organization_id` is empty; a precondition fails the plan. |
| F-1 — the DNS zone | Outstanding | `APP_BASE_URL` is empty, so no certificate can be issued; a precondition fails the plan. |
| OQ-003 / PTBC-048 — backup retention | Outstanding | The mechanism is built and the period left configurable, as TASK-020's gate cell requires. PROD may not be applied while it is unset. |

## 3. What was built

### 3.1 Shape

```
infra/terraform/
├── network/         VPC, two subnets, private services access, firewall
├── compute/         Cloud Run service, Direct VPC egress               (§4)
├── database/        Cloud SQL for PostgreSQL 17, backups, PITR, IAM auth
├── storage/         document and backup buckets
├── load-balancer/   regional external ALB, Cloud Armor, TLS 1.2+
├── platform/        preflight guards and the five modules wired together
└── environments/    dev · sit · uat · prod — a backend, a provider, one call
```

The five module names are the five deliverables the workbook names, in the two directories it
names (`infra/terraform/network` for TASK-021, `infra/terraform/database` for TASK-020) plus three
beside them.

**A root is thin on purpose**: a state backend, `provider "google" {}` and one call to `platform`,
with everything variable in a `terraform.tfvars` beside it. The alternative — four roots each
composing the five modules — puts the same seventy lines in four places, and the first time DEV and
UAT drift, nobody finds out from a diff. With the composition shared, `diff environments/sit
environments/prod` is five lines of sizing and one bucket name, which is exactly the review
question: *is PROD the same thing as SIT, larger?*

The provider block is empty because every resource sets its own `project` and `region`, both read
from the manifest. There is no provider-level default for a module to inherit silently, and no
second place the region is written down.

### 3.2 One source of truth

`infra/environments/environments.json` is TASK-016's, and this task reads it rather than restating
it. Project ids, network names, database instance and user names, secret ids, service accounts and
the region all come from there, and `environment-separation-check.py` already proves no identifier
is shared between two environments. `terraform-check.py` T-3 fails the pull request if any of those
values appears as a literal in the Terraform, and T-3 also rejects any region literal at all,
including the correct one: a second copy of a value is a second truth, and it drifts.

Two values cannot be read from the manifest, and both are structural rather than accidental:

| Value | Why | How it is held honest |
| --- | --- | --- |
| The state bucket in each root's `backend.tf` | A `backend` block takes no variables — Terraform resolves it before evaluating anything | T-2 compares all four against the manifest; T-4 fails if two roots share one bucket, which would put PROD state inside a non-PROD boundary |
| `container_image` | It is a property of the release, not of the environment. One artifact is built once and promoted unchanged through all four environments (TASK-016 §4), so its digest is not an environment's to hold | Supplied as `TF_VAR_container_image` at apply time by TASK-018. Nothing is committed, and there is no `ignore_changes` on the image — Terraform *is* the deploy mechanism, so a deploy leaves no drift for the next plan to find |

### 3.3 The five modules, and what each one is for

| Module | What it creates | The property that is the point of it |
| --- | --- | --- |
| **network** | Custom-mode VPC; application subnet with Private Google Access and flow logs; `REGIONAL_MANAGED_PROXY` subnet; private services access range and peering; three firewall rules | Custom mode because auto mode creates a subnet in every GCP region and ADR-001 C-1 admits one. The proxy-only subnet is what lets the load balancer be regional (C-2). The firewall rules are the tier-to-tier half of CTL-03: the application tag may open 5432 to the private services range and nothing else on the VPC path, with the deny logged so TASK-021 tunes from evidence rather than from guesswork. |
| **database** | Cloud SQL for PostgreSQL 17; the application database; an IAM service-account user; two project IAM grants | No public IP exists to be firewalled (CTL-03). `ssl_mode = ENCRYPTED_ONLY` is the rejection TASK-020's validation check attempts (CTL-17). Backups and PITR are on, located in the named region, with the retention period left null (§3.5). Six database flags, five of them CIS benchmark items, put connection-level audit logging where CTL-26 can forward it. |
| **storage** | `<project>-documents`, and `-db-backups` / `-document-backups` outside DEV | Single-region in the named region — not multi-region, not dual-region (ADR-001 C-4). Uniform access, public access prevention enforced, versioned, soft delete on. DEV has no backup buckets because the Environment and Secrets sheet scopes both backup rows to SIT, UAT and PROD. The runtime service account can write documents and has no grant on either backup bucket: a backup the application can overwrite is not a backup. |
| **compute** | Cloud Run service with Direct VPC egress; an invoker binding | `INGRESS_TRAFFIC_INTERNAL_LOAD_BALANCER`, so the only path in is the one where TLS, the WAF and the in-Kingdom termination are enforced. Secrets are read from the environment's own containers at start-up, so no value passes through this repository or Terraform state. §4 for the runtime itself. |
| **load-balancer** | Regional external ALB, regional Cloud Armor policy, Certificate Manager DNS authorisation and managed certificate, TLS policy, two forwarding rules | Every resource is **regional**, which is the whole module: a global load balancer terminates TLS at the Google edge nearest the client, which is not necessarily inside the Kingdom, and ADR-001 C-2 forbids that. Port 80 exists only to redirect to 443 (CTL-04). The certificate is Google-managed against a DNS authorisation, so no private key is held by anyone. |

### 3.4 Guards: the plan refuses before it reaches the provider

`platform/guards.tf` is a `terraform_data` resource carrying five preconditions. It uses no provider
and needs no credentials, so the refusal is the first thing a plan prints rather than an
authentication error some way in. The messages name the residual item, not the field.

| Precondition | Owed by | Observed |
| --- | --- | --- |
| `platform.region` is not empty | AHDA IT — UGV-07, ADR-001 R-2 | fires today |
| the region is on `platform.region_allowlist` | — `me-central1` is Doha, Qatar | fires today |
| `platform.organization_id` is not empty | AHDA IT — UGV-07, ADR-001 R-3, CTL-47 | fires today |
| `APP_BASE_URL` is not empty | AHDA IT — environment-separation.md F-1, proposed UGV-12 | fires today |
| PROD has a `retained_backups` value | AHDA Cybersecurity and Records — OQ-003 / PTBC-048 | fires for PROD only |

These are the Terraform counterpart of the shell guards in `infra/environments/lib/common.sh`. The
same line is held in two places because the two run at different times: the scripts create the
project, this creates what is in it, and a mistake in either is a rebuild rather than an edit.

The fifth is the one worth stating plainly. TASK-020's gate cell says to build the backup mechanism
and leave the period configurable because OQ-003 is open. Leaving a variable with no default does
that, but it also means a PROD apply would silently take the provider's default — a retention period
nobody approved, discovered at the first audit. The precondition makes PROD refuse instead. DEV, SIT
and UAT may run on the default while the question is open.

### 3.5 What differs between environments, and what does not

Everything structural is identical in all four. What a root's `terraform.tfvars` sets:

| | DEV | SIT | UAT | PROD |
| --- | --- | --- | --- | --- |
| Address plan (`10.x`) | 10.10 | 10.20 | 10.30 | 10.40 |
| Database tier | `db-custom-1-3840` | `db-custom-2-7680` | `db-custom-2-7680` | `db-custom-4-15360` |
| Availability | ZONAL | ZONAL | ZONAL | **REGIONAL** |
| Disk, autoresize ceiling | 20 / 100 GB | 50 / 200 GB | 50 / 200 GB | 100 / 500 GB |
| Cloud Run cpu / memory | 1 / 1Gi | 1 / 2Gi | 2 / 2Gi | 2 / 4Gi |
| Instances, min–max | 0–2 | 1–4 | 1–4 | 2–10 |
| Backup buckets | none | yes | yes | yes |
| Deletion protection | off | off | **on** | **on** |
| WAF | preview | preview | preview | preview |

The four VPCs are not peered and carry no route to one another, so the address ranges need not
differ. They do, so that connecting one of them to an AHDA network later is a route, not a
renumbering — and T-8 fails the pull request if two environments are given the same range.

PROD's `REGIONAL` availability is the one sizing value that is a cost decision rather than a
capacity one. ADR-001 C-11 is explicit that an RTO of 4 to 6 hours does not by itself require a hot
standby and that any regional high-availability cost must be re-tested against that target before it
is committed. It is set here because a production database losing a zone is a different event from a
production database losing a region, and it is one line to reverse if AHDA declines the cost.

The WAF is in **preview** in all four environments: the rules log rather than block. Cloud Armor's
preconfigured OWASP rule sets turned straight to enforcing are how a WAF takes a working application
off the air on its first day, and the control matrix's G-7 records that no rule baseline is stated
and that TASK-021 records it. What this task owns is that the policy exists, is regional, is
attached to the backend, and has somewhere for TASK-021 to put the answer.

### 3.6 Reviewed like application code

The third acceptance criterion is a process, not a resource, so it is built out of the same three
mechanisms the application code uses:

| Mechanism | Where |
| --- | --- |
| A CODEOWNERS entry that pulls the Security Lead into every change under `infra/terraform/`, and the Database Lead into the database module | `.github/CODEOWNERS` |
| A required status check named `terraform` on the protected branches, alongside `backend`, `frontend` and `repo-checks` | `.github/branch-protection/protected-branches.ruleset.json` |
| A CI job running `fmt -check`, `init -lockfile=readonly` and `validate` on all four roots, and a Trivy config scan gated at HIGH/CRITICAL | `.github/workflows/ci-quality-gates.yml` |
| A static check of what a plan cannot see — manifest agreement, duplicated hosting values, committed credentials, toolchain pins, lock-file coverage, address overlaps, and anything that would make a plan non-deterministic (T-9) | `docs/architecture/terraform-check.py`, in `repo-checks` |

`init -lockfile=readonly` is the load-bearing flag. The committed `.terraform.lock.hcl` covers
`linux_amd64`, `darwin_arm64` and `darwin_amd64`, so CI uses the same provider build a reviewer saw;
without the flag, CI would quietly resolve its own.

tfsec, which the workbook's validation cell names as an example, was retired into Trivy, and its
rules are what Trivy's config scanner runs. The scan is the same check under the current name.

## 4. The compute runtime

ADR-001 R-6 records "Cloud Run/GKE" as an unresolved pair with no owner. ADR-002 §4.3 declined to
take it, for stated reasons, and registered S-7: *"TASK-017 cannot codify compute."* The ADR-001
confirmation request puts it to AHDA IT as Q4, issued 2026-09-20 and unanswered.

A compute module is a deliverable of this task. Three courses were available: deliver nothing and
leave the deliverable open; implement both behind a selector; or implement one and say so. The
third was taken.

**`compute/` implements Cloud Run, as the delivery team's selection, provisionally, pending Q4.**
This record does not close R-6 and has no authority to: the reasoning below is offered to the
Engagement Architect and AHDA IT as the recommendation Q4 asks for.

Why Cloud Run:

- **ADR-003 deploys one container.** A modular monolith, one artifact, one database, for 150 named
  users. GKE's value is orchestrating many services with independent scaling and deployment; there
  is one service here, and the workbook names Kubernetes nowhere outside the unresolved pair.
- **The operations period is ~22 months and the operator is AHDA.** A GKE cluster is a system with
  its own upgrades, node pools, patching and failure modes, and TASK-096/097 hand it over. Cloud Run
  has no nodes to patch. CTL-55's dependency-currency obligation runs through the whole period.
- **Nothing in the module set changes.** Cloud Run attaches to the same VPC with Direct VPC egress,
  takes the same firewall tag, reads the same secrets, and is fronted by the same regional load
  balancer through a serverless network endpoint group. Network, database and storage are untouched
  by the answer.
- **The RTO is 4 to 6 hours** (PTBC-048). It does not call for the availability machinery a cluster
  brings, and ADR-001 C-11 asks for any high-availability cost to be re-tested against that target.

What a GKE answer would cost, stated so the decision is priced rather than guessed:

| Changes | Does not change |
| --- | --- |
| `compute/` is replaced: a private regional cluster, node pool, workload identity, Deployment and Service | `database/`, `storage/`, the application, the container image, the promotion model |
| `network/` gains secondary ranges for pods and services, and a Cloud NAT for node egress | The VPC, the proxy-only subnet, the private services access range |
| `load-balancer/` takes a zonal NEG from a Gateway or Ingress instead of a serverless NEG | The Cloud Armor policy, the TLS policy, the certificate, the forwarding rules, the redirect |
| The pipeline deploys with a rollout rather than a revision | The artifact, the gates, the approvals |

The substitution is a module body and two edges, not a rewrite — which is the reason the composition
in `platform/` passes explicit inputs to a named module rather than assuming a runtime anywhere else.

**This is F-1 below, and it is the first thing a reviewer of this task should agree or reject.**

## 5. Verification

What was run in this environment, against the committed files. Terraform 1.16.3, google provider
8.4.0, Trivy 0.74.0.

| # | Check | Result |
| --- | --- | --- |
| 1 | `terraform fmt -check -recursive infra/terraform` | Clean |
| 2 | `terraform init -backend=false -lockfile=readonly` then `validate`, all four roots | `Success! The configuration is valid.` ×4, with the committed lock files authoritative |
| 3 | `terraform plan`, all four roots, against the committed manifest | Refused, before the provider: **4 precondition failures in DEV, SIT and UAT, 5 in PROD** — region, allowlist, tenancy, `APP_BASE_URL`, and for PROD `retained_backups` — each naming its residual item (§3.4) |
| 4 | `terraform plan`, all four roots, against a manifest carrying placeholder values (`me-central2`, a placeholder organisation, `https://<env>.pmplatform.example.invalid`, PROD retention set) | **DEV 40 to add, SIT 42, UAT 42, PROD 42** — no errors, no warnings. This resolved the whole graph against the real provider and its validators, not just the schema |
| 5 | Two consecutive plans of the same root, compared as `terraform show -json` by `compare-plans.py` | **Identical: 40 resource changes, all `create`.** `resource_changes`, `planned_values`, `prior_state`, `configuration`, `variables` and `output_changes` all byte-identical; `sha256(resource_changes)` equal. Only `timestamp` and the *order* of `relevant_attributes` differ, and neither describes a change (§5.3) |
| 6 | `compare-plans.py` against three deliberately altered plans | A dropped resource is reported by address; a changed planned value (`min_tls_version` → `TLS_1_0`) is reported; a plan differing only in `timestamp` is correctly reported identical |
| 7 | `trivy config --severity HIGH,CRITICAL --exit-code 1 infra/terraform` | **Zero HIGH, zero CRITICAL.** Exit 0 |
| 8 | `tfsec --minimum-severity HIGH`, all four roots, with each root's tfvars | **One HIGH each: `google-sql-encrypt-in-transit-data`.** Investigated and shown to be unsatisfiable — see §5.4 and F-11 |
| 9 | `trivy config` at every severity, with each root's tfvars | Two LOW findings remain, both explained in §5.2. Five MEDIUM database findings were real and were fixed |
| 10 | `python3 docs/architecture/terraform-check.py` | `OK: 5 modules, 4 environment roots, region UNNAMED (UGV-07); no hosting value duplicated outside infra/environments/environments.json, no credential committed.` |
| 11 | Mutation test of the check: eighteen edits that break an invariant, one at a time, plus an unmutated control | Each caught, with the finding naming the invariant; the control exits 0 (§5.1) |
| 12 | `python3 docs/architecture/environment-separation-check.py` | Unchanged and green — this task added no identifier the manifest does not hold |
| 13 | `gitleaks detect --no-git` over `infra/terraform/` and the check script | `no leaks found` |
| 14 | `sh -n infra/terraform/verify-idempotency.sh`; workflow YAML parsed; jobs `backend`, `frontend`, `repo-checks`, `terraform` | Clean; valid |

Rows 3 to 5 were run from a copy of `infra/` with each root's `backend.tf` removed: a `gcs`
backend is initialised before anything else is evaluated, and initialising it needs the state bucket
and a credential, neither of which exists. Nothing else was changed, and the placeholder values in
row 4 were written into the copy's manifest, never the committed one. Row 4's `me-central2` is
Google's Saudi Arabia region and the only candidate on the allowlist; it is a placeholder here, not
a confirmation — R-2 is still owed.

What is **owed**, and cannot be run until an environment exists:

| Check | From | Blocked by |
| --- | --- | --- |
| `terraform apply` from a clean state | TASK-017 acceptance criterion 1 | R-1, R-2, R-3, F-1 (this record's F-2) |
| V-3, V-4 of `verify-idempotency.sh` — plan against the applied state, then twice in a row | TASK-017 acceptance criterion 2 and validation cell | The same. Row 5 proves the configuration is deterministic and T-9 proves nothing in it can become non-deterministic; only an apply can prove the provider agrees |
| A non-TLS connection rejected; backup history in the console | TASK-020 | The instance |
| External port scan showing only 443; database unreachable from outside the application tier | TASK-021 | The environment |

### 5.1 Mutation test

Each invariant was broken once, in a copy of the repository, and the check run. All eighteen exited 1; an unmutated control copy exited 0.

| # | Edit | Reported |
| --- | --- | --- |
| M1 | `load-balancer/` deleted | T-1 — module missing |
| M2 | `network/outputs.tf` removed | T-1 — module has no outputs.tf |
| M3 | `environments/sit` renamed to `staging` | T-2 — root set disagrees with the manifest; plus T-7 and T-8 for the environment that lost its lock file and tfvars |
| M4 | DEV's state bucket pointed at PROD's | T-2 — disagrees with the manifest; T-4 — two roots, one bucket |
| M5 | DEV's root declared `environment = "sit"` | T-2 — directory and declaration disagree |
| M6 | `me-central2` written into the network module | T-3 — the region is read from the manifest, a second copy is a second truth |
| M7 | `me-central1` written into the compute module | T-3, naming it as not on the allowlist |
| M8 | PROD's project id hardcoded in the composition | T-3 — the manifest already holds it as `prod.project_id` |
| M9 | `credentials = file("key.json")` added to a provider | T-5 — deploy credentials live in the CI/CD secret store |
| M10 | Storage module pinned to a different provider version | T-6 — one configuration, one provider version |
| M11 | `.terraform-version` bumped without CI | T-6 — CI would run a different version from the one the locks were produced with |
| M12 | UAT's lock file deleted | T-7 |
| M13 | PROD's lock reduced to one platform | T-7 — with the `terraform providers lock -platform=…` command to fix it |
| M14 | SIT given DEV's application subnet range | T-8 — both given 10.10.0.0/20 |
| M15 | A literal password appended to a tfvars file | T-5 |
| M16 | `timestamp()` used in a label | T-9 — its value changes between runs, so every plan would report a change against an unchanged state |
| M17 | `ignore_changes` added to the Cloud Run image | T-9 — it hides drift rather than removing it, against acceptance criterion 1 |
| M18 | `uuid()` used in a bucket name | T-9 |

### 5.2 The two remaining scanner findings

Both are LOW, neither blocks, and both are recorded rather than suppressed.

- **GCP-0075, "Private Google Access disabled"** on `network/main.tf`. A false positive: the
  application subnet has it enabled, and the finding is against the `REGIONAL_MANAGED_PROXY`
  subnet, where the setting does not exist. The scanner cannot tell the two apart.
- **GCP-0066, "buckets should be encrypted with a customer-managed key"** on `storage/main.tf`.
  Deliberate. Google-managed encryption is always on and satisfies CTL-17 and CTL-20 as written.
  Whether a customer-managed key is *required* follows from AHDA's data classification, which is
  ADR-001 R-4 and unanswered. Adding CMEK now would mean choosing a key ring, a rotation period and
  a key administrator, none of which anyone has decided. This is F-6.

### 5.3 What "zero diff" was measured on

The validation cell asks for two consecutive plans with no manual change between them and zero
diff. Two things in Terraform's own JSON output differ between any two runs and describe no change
at all: `timestamp`, the moment the plan was produced, and the *order* of `relevant_attributes`,
which Terraform emits in map-iteration order. A raw file comparison therefore reports a difference
every time and proves nothing, which is why `compare-plans.py` exists rather than a `diff`.

It compares what a plan asserts — `resource_changes`, `planned_values`, `prior_state`,
`configuration`, `variables`, `output_changes`, `checks` — and `relevant_attributes` as a set. On
the two plans in row 5 every one of those is identical, including the SHA-256 of `resource_changes`.
Row 6 confirms the comparator fails when it should: a dropped resource is named by address, an
altered planned value is caught, and a plan differing only in `timestamp` is correctly reported
identical.

This proves the *configuration* is deterministic. It does not prove idempotency, which is a property
of the configuration **and** the provider **and** the API together and needs an applied environment.
`verify-idempotency.sh` V-3 and V-4 run that the day one exists, and exit non-zero rather than
skipping when they cannot — an unexecuted check is not a passed check. What can be enforced today is
enforced: T-9 fails the pull request on `timestamp()`, `plantimestamp()`, `uuid()`, `bcrypt()` or a
`lifecycle.ignore_changes`, which are the two configuration-level ways to lose a zero diff.

### 5.4 tfsec's one HIGH finding, and why Trivy is what CI runs

The validation cell names tfsec as an example. Run at `--minimum-severity HIGH`, tfsec 1.28.14 —
its final release — reports one HIGH finding against every environment:
**`google-sql-encrypt-in-transit-data`, "Database instance does not require TLS for all connections."**

It is wrong, and provably so. The rule looks for `settings.ip_configuration.require_ssl = true`.
That argument was deprecated and then **removed** from the `google` provider in favour of
`ssl_mode`; adding it to this module fails at `terraform validate`:

```
Error: Unsupported argument
  on ../../database/main.tf line 38, in resource "google_sql_database_instance" "main":
  38:       require_ssl     = true
An argument named "require_ssl" is not expected here.
```

The module sets `ssl_mode = "ENCRYPTED_ONLY"`, which is the successor and is stricter: `require_ssl`
required TLS, while `ENCRYPTED_ONLY` rejects any unencrypted connection outright — which is exactly
what TASK-020's validation check attempts and CTL-17 requires. Trivy, which carries tfsec's rule set
forward and is maintained, reports **zero** findings against the same module.

So the finding cannot be cleared by changing the configuration; it can only be cleared by changing
the configuration to something the provider no longer accepts. tfsec was archived in 2023 and is
frozen against a provider generation that no longer exists. **CI runs Trivy**, which is the same
check under its current name, and `verify-idempotency.sh` V-2 does the same. This is F-11.

## 6. Acceptance criteria

| # | Criterion | Status |
| --- | --- | --- |
| 1 | 100% of environment infrastructure is created by `terraform apply` from a clean state, zero manual console changes | **BUILT, NOT APPLIED.** All five resource groups the task names — network, compute, managed database, load balancer/WAF, object storage — are codified and plan cleanly end to end (§5 row 4: 40 to 42 resources per environment). Nothing is applied: R-1, R-2, R-3 and F-1 are outstanding and the plan refuses while they are. No console change is possible against an environment that does not exist, and the guards make the first one `terraform apply` by construction. |
| 2 | A `terraform plan` on the current state produces no unexpected diff | **NOT MET — and cannot be, today.** There is no state. What is proved: the configuration is deterministic (two consecutive plans assert identical changes, §5 rows 5 and 6), and nothing in it is excluded from management — no `ignore_changes`, no resource left to the console, and the container image is an input rather than a drift source (§3.2). What is enforced from here on: T-9 fails any pull request that introduces a non-deterministic function or an `ignore_changes` (§5.1 M16–M18). What is owed: one run of `verify-idempotency.sh` per environment after the first apply. |
| 3 | IaC is peer reviewed like application code | **MET.** CODEOWNERS routes every change under `infra/terraform/` to the DevOps and Security Leads; `terraform` is a required status check on the protected branches; the job runs format, schema and a security scan on every pull request; and `terraform-check.py` in `repo-checks` catches what a plan cannot see (§3.6, §5.1). |
| — | Validation cell: `terraform plan` twice with no manual change between, zero diff | **SCRIPTED AND HALF-EXECUTED.** The comparison is `infra/terraform/verify-idempotency.sh` V-4, using `compare-plans.py` so that Terraform's own `timestamp` and attribute ordering are not mistaken for a change (§5.3). Run against the configuration today, two consecutive plans assert identical changes — identical `resource_changes`, `planned_values`, `configuration`, `variables` and `output_changes`, equal SHA-256. Against an applied state it cannot run, and V-3/V-4 exit non-zero rather than skip. |
| — | Validation cell: `terraform validate` and a static security scan with zero HIGH/CRITICAL | **MET.** `validate` green on all four roots (§5 row 2). Trivy reports **zero HIGH and zero CRITICAL** (row 7). tfsec, which the cell names as the example, reports one HIGH that is unsatisfiable: its rule requires a provider argument that no longer exists, and the module already uses the stricter successor (§5.4, F-11). |
| — | Gate cell: GCP, in-Kingdom, region supplied before the first apply | **ENFORCED.** Every resource is regional and takes its region from the manifest; no region literal may exist in the Terraform (T-3); the plan refuses while the region is unnamed or off the allowlist; and the folder-level `gcp.resourceLocations` policy TASK-016 binds before any project exists makes a non-compliant apply fail at the API (ADR-001 C-1). |
| — | Environment cell: provider credentials in the CI/CD secret store, never committed | **MET.** No credential of any kind is in this directory; the provider reads the environment; T-5 rejects a credentials argument, a key file or a literal password. See F-3 on the shape that credential should take. |

## 7. Findings and open items

| ID | Finding | Owner / where it goes |
| --- | --- | --- |
| **F-1** | **The compute runtime is codified on Cloud Run, provisionally** (§4). ADR-001 R-6 is open and has no owner; ADR-002 §4.3 declined it and recorded S-7 ("TASK-017 cannot codify compute"); the confirmation request's Q4 is unanswered. This task could not deliver a compute module without taking a position, so it took one and priced the alternative. **Recommended: amend ADR-001 with the Cloud Run selection, or return Q4 with GKE and accept the §4 substitution cost.** Until then the module is the delivery team's recommendation, not a decision. | Engagement Architect + AHDA IT — ADR-001 R-6 / confirmation request Q4 |
| **F-2** | **No AHDA-owned DNS zone is named**, so no environment has an `APP_BASE_URL` and no certificate can be issued. This is environment-separation.md F-1, still unregistered; it now blocks an apply rather than only a configuration value, because the load balancer's managed certificate is issued against a DNS authorisation AHDA must publish. The `dns_authorization_record` output gives AHDA the exact CNAME to publish once the zone is named. **Recommended: register as UGV-12, owner AHDA IT, gate Before Build/Integration Build.** | PMO (register); AHDA IT (value); `docs/governance/unassigned-gated-values.csv` |
| **F-3** | **`DEPLOY_SERVICE_ACCOUNT_KEY` presumes a downloadable key**, which TASK-016 F-2 already flagged as the one credential that can leave the environment it belongs to. It now also applies to this task: Terraform holds the broadest permissions in the pipeline, and a downloadable key for it is the highest-value credential in the engagement. **Recommended, unchanged: Workload Identity Federation from GitHub Actions to each environment's deploy service account, after which `constraints/iam.disableServiceAccountKeyCreation` can be bound to both folders and the sheet row becomes a provider reference (Public) rather than a Secret.** | TASK-018, TASK-019; sheet edit — DevOps/Platform Lead |
| **F-4** | **The Cloud Run invoker binding names `allUsers`.** It is safe because ingress is restricted to the internal load balancer, so traffic that did not arrive through the ALB is rejected before the binding is consulted — but an organisation enforcing `constraints/iam.allowedPolicyMemberDomains` (domain-restricted sharing) will refuse the binding outright, and the apply fails. If AHDA's organisation enforces it, the backend becomes authenticated with the load balancer's service agent as the invoker: one resource changed, no architectural consequence. **This needs an answer with the tenancy answer (R-3), not after the first apply.** | AHDA IT — with UGV-07 |
| **F-5** | **The application needs configuration the Environment and Secrets sheet does not name**: the document bucket and the Cloud SQL connection name. Nothing was invented here — only `APP_BASE_URL` is set on the service, and the bucket and instance names are outputs. Both values are carried inside `DOCUMENT_STORAGE_CONNECTION_STRING` and `DB_CONNECTION_STRING`, which TASK-019 writes. `DOCUMENT_STORAGE_CONNECTION_STRING` also has no container: TASK-016 created only its own two, so TASK-019 creates it and adds it to the manifest, after which the compute module picks it up with no change. | TASK-019; manifest edit |
| **F-6** | **Customer-managed encryption keys are not used** (§5.2). Google-managed encryption satisfies CTL-17 and CTL-20 as written. Whether CMEK is *required* follows from AHDA's data classification — ADR-001 R-4, unanswered — and adding it means choosing a key ring, a rotation period and a key administrator, none of which have owners. If R-4 comes back requiring CMEK, it touches the database instance and the three buckets and is additive. | AHDA Cybersecurity — ADR-001 R-4; then TASK-020 |
| **F-7** | **`max_instances × connection pool size` is not bounded against the database.** PROD scales to 10 Cloud Run instances; a default Npgsql pool of 100 would ask for 1000 connections from an instance that does not have them. The infrastructure cannot fix this — the pool size is application configuration — so the ceiling must be set on the connection string TASK-019 writes, and the two numbers reviewed together whenever either changes. | TASK-020 (instance limits), TASK-019 (connection string), Backend Lead (pool size) |
| **F-8** | **Egress from the application is not routed through a fixed address.** Cloud Run uses `PRIVATE_RANGES_ONLY`, so calls to external services leave by Google's own addresses rather than a Cloud NAT with a stable IP. Nothing today requires otherwise, and the simpler configuration is the right default — but Nafath (TASK-068), Etimad (ADR-008) and the SMS provider (UGV-10) are the kind of counterparty that allowlists source addresses. Changing it later is a Cloud NAT, a router and one line of egress setting; discovering it during integration testing is a schedule event. | TASK-021 (the change), TASK-068 / TASK-039 (whether it is needed) |
| **F-9** | **The container registry has no home.** One artifact promoted unchanged through four projects needs one Artifact Registry repository those four projects can all pull from, which crosses the project boundary TASK-016 built deliberately. Nothing here creates it, because where it lives is a decision (a shared project in the same folder structure is the usual answer) and `CONTAINER_REGISTRY_TOKEN` is scoped "All (CI/CD)" in the sheet, implying exactly one. | TASK-018, TASK-022 — with AHDA IT on the hosting project |
| **F-11** | **tfsec is named in the validation cell and is no longer usable as written.** Archived in 2023 and frozen at its final release, its `google-sql-encrypt-in-transit-data` rule requires `require_ssl`, an argument the `google` provider has since removed — setting it fails `terraform validate` (§5.4). The module uses the stricter successor `ssl_mode = "ENCRYPTED_ONLY"`, and Trivy, which carries tfsec's rules forward, reports zero. **Recommended workbook edit: the TASK-017 validation cell reads "a static security scan (e.g. tfsec)"; make it name Trivy, or leave the "e.g." and record here that Trivy is the tool.** Nothing about the infrastructure changes either way; what changes is which scan an auditor is handed. | Workbook edit — PMO; scan choice — DevOps/Platform Lead |
| **F-10** | **The serverless backend service is planned with `balancing_mode = "UTILIZATION"`**, which is the provider's default for the field rather than a choice made here; a serverless network endpoint group has no balancing mode. It may be accepted and ignored by the API, or rejected at apply. It is recorded because it is the kind of thing only a first apply settles, and the fix if it is rejected is one line. | TASK-017 — at the first apply |

## 8. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-22 | Validation cell executed, and the part that needs a live environment scripted: `verify-idempotency.sh` (V-1 validate, V-2 scan, V-3 plan against the current state, V-4 plan twice) with `compare-plans.py`, both exiting non-zero rather than skipping. Two consecutive plans shown to assert identical changes on the machine-readable plan, not merely identical text (§5.3). tfsec run alongside Trivy; its one HIGH finding shown to be unsatisfiable against the current provider (§5.4, F-11). T-9 added to the check — non-deterministic functions and `ignore_changes` — with three more mutations (M16–M18). | Infrastructure (TASK-017) |
| 2026-09-22 | Initial record. Five Terraform modules, a shared composition and four environment roots authored against the TASK-016 manifest as the single source of truth; five preflight preconditions that refuse the plan while the region, tenancy, DNS zone or PROD retention period are unset; peer review made enforceable through CODEOWNERS, a required `terraform` status check, a CI job (fmt, validate with read-only locks, Trivy) and `terraform-check.py` in `repo-checks`. Verified: fmt clean, validate green on four roots, full plans of 40–42 resources per environment against a placeholder manifest, two consecutive plans byte-identical, zero HIGH/CRITICAL, fifteen mutations caught, gitleaks clean. The compute runtime is codified on Cloud Run provisionally, with the GKE substitution cost priced (§4, F-1). Ten findings raised. | Infrastructure (TASK-017) |

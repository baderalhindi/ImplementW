# ADR-001 — Confirmation Request, Addendum 1: values needed to provision

| Field | Value |
| --- | --- |
| Supplements | `docs/architecture/adrs/ADR-001-confirmation-request.md` (issued 2026-09-20, Q1–Q12) |
| Request date | 2026-09-25 |
| Status | **DRAFT — to be issued by PMO** |
| Addressed to | AHDA IT (§2), PMO Engagement Lead (§3) |
| Raised by | Infrastructure (TASK-017, TASK-020, TASK-021) |
| Response needed by | ______________ (set by PMO). Every question here is needed **before the first `terraform apply`**, alongside Q1, Q2 and Q5 |

## 1. Why an addendum

The original request asks AHDA to confirm *whose* GCP organisation and *which* region. Building the
environments since then (TASK-016 to TASK-021) has shown that a confirmed answer to those questions
is still not enough to provision anything: the scripts and the Terraform also need the identifiers
behind the answers, a DNS zone, and an account allowed to act in AHDA's organisation. None of these
is a delivery-team value, and the repository refuses to proceed while each is empty rather than
guess one — a project created in the wrong organisation or region is rebuilt, not reconfigured.

This addendum asks for them, so that one reply closes everything that stands between the approved
decision and the first environment. The original Q1–Q12 are unchanged and still open.

## 2. For AHDA IT

| # | Question | Answer |
| --- | --- | --- |
| **Q13** | The **GCP organisation ID** (numeric) of the organisation named in Q1. It is where the two environment folders and their policies are created. (UGV-07, R-3) | Organisation ID: ______________ |
| **Q14** | The **billing account ID** the four environment projects are linked to. (UGV-07) | Billing account: ______-______-______ |
| **Q15** | The **DNS zone** the platform's addresses live in, and one **hostname per environment**. The SPA and the API share one origin (TASK-016 D-1), so each environment needs exactly one name, e.g. `pmplatform-dev.<zone>` … `pmplatform.<zone>` for PROD. (environment-separation.md F-1, proposed UGV-12) | Zone: ______________<br>DEV: ______________<br>SIT: ______________<br>UAT: ______________<br>PROD: ______________ |
| **Q16** | **Who publishes DNS records** in that zone, and with what lead time? For each environment, two records are needed after its first apply: an **A record** for the hostname pointing at the load balancer's address, and a **CNAME** that lets Google issue and renew the TLS certificate (no private key is held by anyone). Both values are output by Terraform. | Owner: ______________<br>Lead time: ______________ |
| **Q17** | **Provisioning access.** Who runs the first provisioning, and will that account be granted, for the duration of the build: `roles/resourcemanager.folderCreator`, `roles/orgpolicy.policyAdmin`, `roles/resourcemanager.projectCreator` and `roles/logging.admin` on the organisation (or on a parent folder AHDA designates), and `roles/billing.user` on the billing account in Q14? | ☐ Granted to: ______________  ☐ AHDA IT runs it, with the delivery team  ☐ Other: ______________ |
| **Q18** | **Organisation policies already in force** that the build must respect. Two are known to matter: `iam.allowedPolicyMemberDomains` (if enforced, the API's invoker binding changes shape — infrastructure-as-code.md F-4) and `compute.trustedImageProjects` (the network validation creates two temporary VMs from `debian-cloud` — network-security.md N-F5). And: may the delivery team bind four constraints to the two environment folders — resource locations, no default network, no VM public IPs, no Cloud SQL public IPs? | Enforced policies: ______________<br>Folder constraints: ☐ Approved  ☐ AHDA IT applies them  ☐ Other: ______________ |
| **Q19** | **Product availability in the region from Q2** — confirm each, since the region cannot be changed later (CTL-54): the regional external Application Load Balancer; regional Cloud Armor security policies; regional SSL policies; Certificate Manager regional certificates; Cloud SQL for PostgreSQL 17; Cloud Run with Direct VPC egress; Cloud Scheduler. | ☐ All available  ☐ Unavailable: ______________ |
| **Q20** | Where the **container registry** lives. One image is built once and promoted unchanged through all four environments, so it needs one Artifact Registry repository they can all pull from. Proposed: a project `ahda-pmplatform-artifacts` in the same organisation, in the Q2 region. (infrastructure-as-code.md F-9) | ☐ Approved as proposed  ☐ Instead: ______________ |
| **Q21** | **HTTPS only, no port 80.** The platform's addresses listen on 443 alone; a browser typing `http://` is refused rather than redirected, until HSTS (TASK-078) makes browsers upgrade automatically. This follows TASK-021's validation check ("only 443 is reachable"). Acceptable? (network-security.md N-F1) | ☐ Accepted  ☐ Redirect from 80 required |

## 3. For the PMO Engagement Lead

| # | Question | Answer |
| --- | --- | --- |
| **Q22** | Issue this addendum with the original request, and record whether Q15/Q16 should be registered as **UGV-12** (DNS zone and record ownership) in the unassigned-gated-values register, as environment-separation.md F-1 proposed. | ☐ Issued  ☐ UGV-12 registered |

## 4. What each answer unblocks

| Question | Unblocks | If it stays open |
| --- | --- | --- |
| Q13, Q14 | `apply-org-policy.sh` and `provision-environment.sh` — the folders, policies and projects | Both scripts refuse to run. No environment can exist |
| Q15 | The TLS certificate, and every `terraform plan` — the plan refuses while `APP_BASE_URL` is empty | No environment can be planned, even once Q13 and Q14 are answered |
| Q16 | The certificate being issued after the first apply | The load balancer exists but cannot serve HTTPS; TASK-021's scan cannot run |
| Q17 | Anyone being able to run Q13/Q14's scripts | The values exist and nobody can use them |
| Q18 | A first apply that does not fail on an unknown policy | Failures surface one at a time during the apply |
| Q19 | Fixing the region | A missing product found after the region is fixed is a redesign — for the WAF, ADR-001 C-2 rules out the global alternative |
| Q20 | TASK-018's promotion pipeline and the first deploy | There is no image for any environment to run |
| Q21 | TASK-021 acceptance as built | Port 80 is reopened for a redirect, and the validation check's wording changes first |

## 5. Return and sign-off

Return this document completed, or reply in writing citing the question numbers. On receipt the
values are written into `infra/environments/environments.json` in a reviewed pull request — the one
place every script and the Terraform read them from — and each script is dry-run for AHDA IT to
review before anything is created.

| Role | Name | Signature | Date | Questions answered |
| --- | --- | --- | --- | --- |
| AHDA IT | | | | Q13–Q21 |
| PMO Engagement Lead | | | | Q22 |

## 6. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-25 | Drafted. Ten questions (Q13–Q22) for the identifiers, DNS, access, policies, product availability and registry needed before the first apply, which the original request does not ask for. | Infrastructure (TASK-021) |

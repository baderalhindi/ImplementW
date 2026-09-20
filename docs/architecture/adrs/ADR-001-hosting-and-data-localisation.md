# ADR-001 — Hosting and Data-Localisation Architecture

| Field | Value |
| --- | --- |
| Task | TASK-005 — Resolve Hosting & Data Localisation Architecture Decision Record (P1 - Architecture Decisions) |
| Depends on | TASK-001 — Controlled Source Baseline (`docs/baseline/controlled-source-baseline.md`, PROVISIONAL) |
| Record date | 2026-09-20 |
| Decision status | **APPROVED — CONDITIONAL.** AHDA decision gate, 19 Sep 2026. Written confirmation, the in-Kingdom region name and the tenancy owner are outstanding (§8, R-1 to R-3) |
| Decision owner | AHDA IT / Cybersecurity + Engagement Architect |
| Branch | `chore/task-005-task-005-hosting-adr` |
| Workbook read | Google Sheet "Implementation work" (ID `1JQdbw-S9wAS247cP3PpSCme_D2NAPVnWzhXhvvxrtl0`), sheets Implementation Plan, Architecture Decisions, Open Questions, Environment and Secrets, Release Checklist, as exported 2026-09-20 |
| Controlled sources and authority order | per TASK-001 |

## 1. What this record is

The Architecture Decisions sheet already holds an ADR-001 row, and it already carries a decision taken at the AHDA gate on 19 Sep 2026. **This record does not make that decision again.** It is the repository-side statement of ADR-001: it restates the decision as the register holds it, states the requirement it reconciles, sets out what the decision commits the build to, and names what is still outstanding before an environment can be provisioned against it.

The TASK-005 "Gate Decision Applied" cell says the same thing in the workbook's own words:

> RESOLVED by ADR-001. This task now records the decision rather than making it: GCP, in-Kingdom, RPO 1 hour, RTO 4 to 6 hours. Remaining work: obtain written confirmation, name the specific in-Kingdom region, and confirm whether the tenancy is AHDA's GCP organization or the vendor's.

Three things follow from that and shape this document. First, the decision is **conditional**: it rests on a reading of the requirement (§5) that only holds if the tenancy answer comes back one particular way (§6). Second, a hosting decision is not finished when the provider is named — the localisation requirement binds a specific list of resources, services and processes, and that list is written down here (§7) so each P3 task inherits it rather than re-deriving it. Third, the residual items are not administrative tidying; two of them block the first `terraform apply` (§8, §9).

## 2. The requirement this ADR reconciles

The requirement, as the workbook states it in three places:

| Where | Wording |
| --- | --- |
| ADR-001, Rationale column | "The RFP **explicitly requires internal hosting and data localisation**, but the only detailed architecture proposal on file (the brief's 'proposed architecture') is Google Cloud. This is a direct, unresolved conflict; it must be reconciled before any environment is provisioned." |
| OQ-001, Missing Information | "Hosting target: internal/KSA on-premise vs. the proposed Google Cloud architecture — **the RFP requires internal hosting and data localisation**; only a cloud architecture has been detailed to date." |
| TASK-005, Validation Checks | "Confirm ADR-001 references the exact RFP scope line (**'internal hosting, data localisation'**)." |

**The scope line reconciled is the RFP's requirement for internal hosting and data localisation.**

The RFP clause identifier — section, paragraph or page — **cannot be cited from this repository.** The RFP is not a controlled source in its own right: under TASK-001 §2.4 it is a contractual input that feeds the Adjusted Project Scope (rank 4) and is "not cited directly for functional definitions", and under TASK-001 §5.1 it is one of the documents held neither in the repository nor in the connected Google Drive. The three cells above are the full extent of the requirement's wording available to this task. Recording the clause identifier is R-5 in §8, and it is the one part of the TASK-005 acceptance criteria that this record cannot close (§11).

This matters beyond citation hygiene. "Internal hosting" is being *interpreted* in §5, and an interpretation of a contractual clause is only as sound as the clause text it is read against. The full clause may qualify "internal" in a way the one-line scope entry does not show.

## 3. Context

- The RFP requires internal hosting and data localisation. No RFP-side architecture was supplied with it.
- The only detailed architecture on file is the brief's proposed Google Cloud design: **managed PostgreSQL, Cloud Run/GKE, HTTPS load balancer + Cloud Armor, single primary region.**
- AHDA already runs on Google Cloud and holds **no on-premises storage** (stated in the ADR-001 Selected Option cell).
- The conflict blocked Phase P3 in its entirety. OQ-001's impact statement: "Blocks all environment provisioning, IaC, network security and hosting-dependent secret-management tasks (Phase P3 entirely)."
- The Release Checklist gate "Architecture Decisions Approved" carries the hard rule: **"no PROD environment may be provisioned before ADR-001 is Approved."**

The conflict is therefore not a preference between two viable designs. One side of it is a contractual obligation, and the other is the only design that exists.

## 4. Options considered

Verbatim from the Options Considered column, with the assessment that follows from §3.

| Option | As recorded | Assessment |
| --- | --- | --- |
| **A** | "Internal/on-premise AHDA-hosted infrastructure with KSA data residency, matching the RFP's stated requirement literally" | Satisfies the clause on its most literal reading and removes all interpretation risk. Defeated on fact, not on preference: AHDA holds no on-premises storage, so Option A is not a use of existing AHDA infrastructure but the construction of a new facility capability — outside the engagement's scope, schedule and BoQ, and with no controlled source describing it. |
| **B** | "Google Cloud Platform as currently proposed (Cloud SQL for PostgreSQL, Cloud Run/GKE, HTTPS load balancer + Cloud Armor, single primary region)" | **Selected.** The only option with a detailed design on file and with existing AHDA operational precedent. Carries the interpretation risk in §5–§6 and the control obligations in §7. |
| **C** | "KSA-region-approved sovereign/local cloud offering, if one exists and is AHDA-approved" | Conditional on its own premise. No such offering is named anywhere in the workbook and none was put forward at the gate. Not evaluated on merits; it was never populated. If R-4 (§8) returns a data classification that bars public cloud, this is the option the decision falls back to, and it will then need the evaluation it has not had. |

The options as recorded name **Cloud SQL** for Option B, while five other workbook cells name **AlloyDB** and ADR-002 names plain **PostgreSQL**. That divergence is real and is recorded as a required edit in §10.1. It does not change this decision: ADR-001 decides *where* the platform runs, not *which* managed PostgreSQL product it uses.

## 5. Decision

Verbatim from the Selected Option column:

> **Option B (cloud) — Google Cloud Platform, in-Kingdom.** AHDA already runs on GCP with no on-premises storage, so the RFP's 'internal hosting' wording is read as AHDA's own controlled cloud tenancy rather than a physical facility. **All data, backups and replicas remain inside Saudi Arabia.** Recovery objectives: **RPO 1 hour, RTO 4 to 6 hours** (AHDA IT business continuity plan).

The decision has three parts, and they carry different weights:

1. **Provider: Google Cloud Platform.** An AHDA decision, taken at the gate.
2. **Locality: in-Kingdom, with all data, backups and replicas inside Saudi Arabia.** This is the part that discharges "data localisation", and it is an absolute — §7 turns it into per-resource constraints.
3. **The reading of "internal hosting" as AHDA's own controlled cloud tenancy rather than a physical facility.** This is an *interpretation of a contractual clause*, not a technical selection. It is the load-bearing element of the whole decision, and §6 states the condition it depends on.

The recovery objectives (RPO 1 hour, RTO 4 to 6 hours) are recorded here because the ADR-001 row records them, but they are governed as a PTBC value: **PTBC-048**, approved at the 19 Sep 2026 gate from AHDA IT's business continuity plan (`docs/governance/ptbc-tbc-tracker.md` §4.3; OQ-003). The **backup retention period is not part of that approval and has no value** — it remains open with AHDA Cybersecurity and Records, gate Before Production. No retention value is set or implied by this ADR.

## 6. The condition the decision rests on

The reading in §5.3 is that "internal" means **AHDA's own controlled tenancy**. The ADR-001 Impact column then records, as an outstanding item:

> AHDA IT must name the specific in-Kingdom region and **confirm whether the platform sits in AHDA's own GCP organization or the vendor's.**

These two outstanding items are not of the same kind. The region name is a parameter — needed before the first apply, and cheap to supply. **The tenancy answer decides whether the decision is valid at all:**

- If the platform sits in **AHDA's own GCP organization**, the reading holds. The tenancy is AHDA's, AHDA's IAM governs it, AHDA's billing and organization policies bind it, and the delivery vendor operates inside a boundary AHDA owns. "Internal hosting" is then a defensible reading of the clause.
- If the platform sits in **the vendor's GCP organization**, the reading does not hold. Whatever the data residency, the hosting is then external to AHDA by any ordinary meaning of the word — a third party owns the tenancy, the identities, the organization policies and the ability to change them. Data localisation would be satisfied; internal hosting would not. The decision would have to be re-taken, and at that point Option A and Option C return.

This is therefore the first question to put in the written confirmation (R-1), not the last. It is recorded in the tracker as **UGV-07** — "GCP in-Kingdom region name and tenancy ownership", owner AHDA IT, proposed gate **Before Build/Integration Build**, with the basis "the region cannot be changed after provisioning without rebuilding every environment" (`docs/governance/unassigned-gated-values.csv`).

A second question belongs in the same confirmation. The workbook nowhere names the **regulatory instrument** behind the RFP's localisation requirement, nor AHDA's **data classification** for this platform's content. Residency and classification are different tests: a classification that bars government data of a given class from public cloud is not satisfied by keeping that data in an in-Kingdom public-cloud region. Option B survives the residency test on its face; whether it survives the classification test is unknown to this record and cannot be inferred. That is R-4.

## 7. Consequences — what this decision binds the build to

The decision's locality clause ("all data, backups and replicas remain inside Saudi Arabia") is an absolute, and an absolute has to be enforced per resource. The constraints below are the enforceable form of it. Each names the task that implements it and the check that proves it. They are constraints derived from the decision, not new decisions.

| # | Constraint | Binds | Verification |
| --- | --- | --- | --- |
| C-1 | **Every resource is created in the named in-Kingdom region.** No resource in any other region, in any environment, including DEV. Enforce with the `constraints/gcp.resourceLocations` organization policy at the organization or folder level, so a non-compliant apply *fails* rather than relying on review. | TASK-016, TASK-017 | A `terraform apply` that names any other region is rejected by policy, not by a reviewer. |
| C-2 | **Ingress and WAF are regional, in the named region.** A *global* external load balancer terminates TLS at the Google edge nearest the client, which is not necessarily inside the Kingdom. TASK-021's gate note already says "In-Kingdom edge and load balancing"; this is what that requires. Confirm the WAF capability needed is available on the regional load-balancer product in that region before committing to it. | TASK-021 | Load balancer and security policy are regional resources in the named region; TLS terminates in-region. |
| C-3 | **Database instance, automated backups and any read replica are in the named region.** Point-in-time recovery sized to RPO 1 hour (TASK-020 gate note). Retention period configurable, no default committed as a business decision (PTBC-048 open). | TASK-020, TASK-023 | Backup and replica locations read from the provider console/CLI equal the named region. |
| C-4 | **Object storage is single-region in the named region** — not multi-region or dual-region — for document/evidence storage (WF-12), database backup storage and audit storage. | TASK-020, TASK-023, TASK-037 | Bucket location type is `region`, value = the named region. |
| C-5 | **Secret Manager uses user-managed replication pinned to the named region.** The default automatic replication policy replicates across regions and would place secret material outside the Kingdom. | TASK-019 | Each secret's replication policy names only the in-Kingdom region. |
| C-6 | **Log, monitoring and APM data stays in the named region.** Log bucket location is fixed when the project is created and cannot be changed afterwards, so it is a project-creation decision, not a later one. A third-party SaaS APM would export telemetry out of the Kingdom; logs carry personal data until TASK-083's redaction is in place, and redaction is a reduction, not a guarantee. | TASK-016 (project creation), TASK-033, TASK-083, TASK-090, TASK-092 | Log bucket locations equal the named region; any external telemetry destination is named and accepted in writing by AHDA Cybersecurity. |
| C-7 | **Non-production environments are in scope.** DEV, SIT and UAT hold seeded and migration-rehearsal data (TASK-027, TASK-089). If localisation attaches to the data class rather than to the environment label, a UAT copy outside the Kingdom breaches it exactly as a PROD copy would. Treat all four environments identically until AHDA Cybersecurity says otherwise (R-4). | TASK-016, TASK-027, TASK-089 | All four environments provision into the named region under C-1. |
| C-8 | **CI/CD execution location is a decision, not a default.** The pipeline is GitHub Actions (`/.github/workflows`); GitHub-hosted runners execute outside the Kingdom, and migration and seed steps can put real data through them. Either confirm that source, build artefacts and test data are out of the localisation scope, or use self-hosted runners in the named region for any job that touches data or PROD credentials. | TASK-018, TASK-022 | A named decision on runner location, with any data-touching job pinned accordingly. |
| C-9 | **Administrative and support access from outside the Kingdom is addressed explicitly.** Remote access is not residency, but several localisation regimes govern it, and the engagement includes a ~22-month operations period. | TASK-019, TASK-093, TASK-097 | Access model for vendor operations staff recorded and accepted by AHDA Cybersecurity. |
| C-10 | **Third-party services process in-Kingdom.** ADR-004 already requires a CST-licensed SMS provider with in-Kingdom processing. Apply the same test to the email path and to any other outbound service. Nafath (TASK-068) and Etimad (ADR-008, unconnected at launch) are in-Kingdom by nature. | TASK-039, TASK-068, TASK-103 | Each external processor named, with its processing location. |
| C-11 | **Disaster recovery is in-Kingdom only.** Combined with a single primary region, cross-region DR is available only if a second in-Kingdom location exists. RTO 4 to 6 hours "does not by itself require a hot standby" (ADR-001 Impact; TASK-023 gate note) — restore-from-backup within the region meets it, and any regional high-availability cost must be re-tested against that target before it is committed. | TASK-023 | Restore drill meets RPO 1 h / RTO 4–6 h with no out-of-Kingdom dependency. |

Two further consequences are structural rather than per-resource:

- **The region choice is irreversible in practice.** UGV-07's gate basis states it: "the region cannot be changed after provisioning without rebuilding every environment." This is why R-2 gates the first apply rather than the first PROD deploy.
- **Product availability constrains, and is constrained by, the region.** The managed PostgreSQL product (§10.1), the regional load-balancer/WAF pairing (C-2) and any other managed service must each be confirmed available in the named region *before* the region is fixed. Google's Saudi Arabia region is `me-central2` (Dammam); AHDA IT confirms the actual region name and whether any second in-Kingdom location is available for C-11.

## 8. Residual items

R-1 to R-3 are the workbook's own outstanding items. R-4 to R-7 are raised by this record.

| # | Item | Owner | Owed by | Consequence if unresolved |
| --- | --- | --- | --- | --- |
| R-1 | **Written confirmation of the 19 Sep 2026 gate decision** by AHDA IT/Cybersecurity. The decision is on record in the register; the signature is not. | AHDA IT / Cybersecurity | Before the first `terraform apply` | The TASK-005 validation check ("signed off by AHDA IT/Cybersecurity, not assumed by the delivery team") is not satisfied, and a contractual clause is being satisfied by an unsigned interpretation. |
| R-2 | **The specific in-Kingdom region name** (UGV-07). | AHDA IT | Before the first `terraform apply` | No apply is possible; and once applied, changing it rebuilds every environment. |
| R-3 | **Tenancy ownership — AHDA's GCP organization or the vendor's** (UGV-07). | AHDA IT | Before the first `terraform apply`, and before R-1 is signed | §6: the "internal hosting" reading, and therefore the decision, does not hold if the answer is the vendor's organization. |
| R-4 | **The governing regulatory instrument and AHDA's data classification for this platform.** The workbook names neither. Candidates for AHDA Cybersecurity to confirm or replace: the PDPL and its implementing regulations, NDMO data management and personal-data-protection standards, and the CST Cloud Computing Regulatory Framework's treatment of government data. | AHDA Cybersecurity | Before Build/Integration Build | Residency is confirmed but sufficiency is not (§6). Also sets the scope of C-7, C-8 and C-9, each of which is currently scoped by assumption. |
| R-5 | **The RFP clause identifier** for the internal-hosting / data-localisation requirement, and a copy of the clause text, recorded in the baseline register per TASK-001 §5.1. | PMO Engagement Lead | Before AHDA sign-off of this ADR | §2: the interpretation in §5.3 is made against a one-line scope entry, not against the clause. |
| R-6 | **Compute runtime: Cloud Run or GKE.** The pair appears only in TASK-005's description and in ADR-001's Option B, always as an unresolved "or". No ADR, task or Open Question decides it, and no one owns it. | Engagement Architect + AHDA IT | Before TASK-017 codifies compute | TASK-017 must codify compute in IaC and cannot; the two runtimes differ in networking, scaling, cost model and operational burden. |
| R-7 | **Managed PostgreSQL product: AlloyDB or Cloud SQL** (§10.1). Belongs to ADR-002 (technology stack, still Proposed), constrained by ADR-001 through availability in the named region. | Engagement Architect → ADR-002 / TASK-006 | With ADR-002, before TASK-020 | TASK-020's title, four further workbook cells and ADR-001's own option text disagree; the two products differ in cost, HA model and regional availability. |

## 9. Provisioning control

The acceptance criterion is that **no infrastructure task in Phase P3 provisions a production or SIT environment until ADR-001 status = Approved.** Status is Approved, so the criterion permits provisioning. Three separate mechanisms already carry the rule:

| Mechanism | Wording |
| --- | --- |
| Release Checklist, "Architecture Decisions Approved" | "Blocking gate — no PROD environment may be provisioned before ADR-001 is Approved" |
| TASK-016, Detailed Description | "Exact hosting substrate follows ADR-001's approved outcome; this task is Blocked until ADR-001 = Approved" |
| TASK-016/017/019/020/021/023, Gate Decision Applied | "UNBLOCKED by ADR-001" |

State of P3 at this record's date: no P3 task has started, `infra/` does not exist in the repository, and no IaC has been authored. Nothing has been provisioned, so the criterion holds trivially today.

What still prevents a first apply is **not** ADR-001's status. It is R-2 (no region to apply into) and, behind it, R-3 (the tenancy the decision depends on). TASK-017's own gate note already says so: "Region name to be supplied by AHDA IT before the first apply."

**Proposed control, for the PMO Engagement Lead and AHDA IT to accept or replace (it is proposed here, not imposed):**

> No `terraform apply` against any environment — including DEV — until R-1, R-2 and R-3 are closed in writing. DEV is included because C-1's organization policy and C-6's log-bucket location are fixed at project creation and cannot be corrected later without rebuilding, and because a DEV project created in the wrong tenancy is the same mistake as a PROD one, discovered later.

This is stricter than the acceptance criterion, which names only PROD and SIT. The criterion is met either way; the difference is whether the first environment is built before the tenancy question is answered.

## 10. Inconsistencies found in the workbook

Per the workbook's own rule — revisions are re-issued, not overwritten in place (TASK-001 §4) — these are specified, not applied.

### 10.1 The managed PostgreSQL product is named three ways

| Where | Names |
| --- | --- |
| ADR-001, Options Considered (Option B) | **Cloud SQL** for PostgreSQL |
| TASK-005 Detailed Description; TASK-006 Detailed Description; TASK-020 task name; TASK-023 and TASK-024 dependency references; Environment and Secrets, `DB_CONNECTION_STRING` ("PostgreSQL-compatible connection string (AlloyDB for PostgreSQL)") | **AlloyDB** for PostgreSQL |
| ADR-002, Selected Option | **PostgreSQL**, unqualified |

The two products are not interchangeable: they differ in cost, high-availability model and regional availability, and regional availability interacts directly with R-2 under §7's closing note. The decision belongs to **ADR-002**, not here (R-7). Required edit: settle it in ADR-002 and make all six cells agree, with ADR-001's Option B text corrected to whichever product ADR-002 selects — or to "managed PostgreSQL", since ADR-001 does not decide it.

### 10.2 OQ-001's blocking-task list does not match the phase it names

OQ-001 reads "TASK-005, TASK-014 through TASK-020 (all Phase P3)". Phase P3 is **TASK-016 to TASK-023**; TASK-014 and TASK-015 are P2. The hosting-dependent set, per ADR-001's own Impact column, is TASK-016, 017, 019, 020, 021 and 023. Required edit: correct the list.

### 10.3 ADR-001's status line omits the tenancy question

The Status cell reads "written confirmation and region naming outstanding". The Impact cell also requires AHDA IT to "confirm whether the platform sits in AHDA's own GCP organization or the vendor's" — the item that §6 shows the decision depends on. Required edit: add tenancy ownership to the status line so the condition is visible where the status is read.

### 10.4 The compute runtime has no owner

R-6. "Cloud Run/GKE" survives as an unresolved pair in both cells that mention it. Required edit: add an ADR row for the compute runtime, or extend ADR-002's scope to cover it, and give it an owner.

### 10.5 Pointer to this record

Required edit: add `docs/architecture/adrs/ADR-001-hosting-and-data-localisation.md` to the ADR-001 row, so the register points at the full record.

## 11. Acceptance-criteria check

| # | Criterion | Result |
| --- | --- | --- |
| 1 | ADR-001 exists in the Architecture Decisions sheet with status no worse than "Pending AHDA Approval" | **MET.** The row exists and reads "Approved (AHDA gate, 19 Sep 2026) — written confirmation and region naming outstanding", which is above the floor the criterion sets. The approval is conditional (§6) and not yet in writing (R-1). |
| 2 | No infrastructure task in Phase P3 provisions a production or SIT environment until ADR-001 status = Approved | **MET.** Status is Approved; the rule is additionally carried by the Release Checklist gate and TASK-016's own description. No P3 task has started and no environment exists (§9). The remaining barrier to a first apply is UGV-07 (R-2, R-3), not ADR-001's status; §9 proposes the corresponding hold. |
| 3 | The ADR explicitly states the RFP clause it reconciles | **PARTIAL.** The requirement is stated verbatim as the workbook records it in all three places — internal hosting and data localisation (§2). The clause identifier (section/paragraph/page) cannot be cited: the RFP is not in the repository or the connected Drive, and is a rank-4 contractual input rather than a controlled source (TASK-001 §2.4, §5.1). R-5. |

Validation checks from the TASK-005 definition:

| Check | Result |
| --- | --- |
| "Confirm ADR-001 references the exact RFP scope line ('internal hosting, data localisation')" | **MET in substance, PARTIAL in form.** §2 quotes the scope line as the workbook records it and states that the clause reference itself is not available. |
| "and is signed off by AHDA IT/Cybersecurity, not assumed by the delivery team" | **NOT MET.** The decision was taken at the AHDA gate on 19 Sep 2026 with AHDA IT / Cybersecurity as the named owner, so it is not a delivery-team assumption. Written confirmation is outstanding (R-1) and the sign-off block below is unsigned. |

## 12. Sign-off

| Role | Decision | Name | Date |
| --- | --- | --- | --- |
| AHDA IT | R-2: the named in-Kingdom region is ______________. R-3: tenancy is AHDA's GCP organization / the vendor's (state which). R-6: compute runtime Cloud Run / GKE | | |
| AHDA Cybersecurity | R-1: the 19 Sep 2026 decision (GCP, in-Kingdom, all data/backups/replicas in Saudi Arabia) is confirmed in writing. R-4: governing instrument and data classification stated; §7 C-7, C-8, C-9 scope confirmed | | |
| PMO Engagement Lead | R-5: RFP clause identifier recorded in the baseline register. §9 provisioning control: Accepted / Replaced (state which). §10 workbook edits: authorised / declined | | |
| Engagement Architect | §7 constraints C-1 to C-11 accepted as binding on TASK-016 to TASK-023 | | |

On R-1, R-2 and R-3 being signed, the status in the header becomes **APPROVED** without qualification, and the ADR-001 row's status line is re-issued to match.

## 13. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-20 | Initial record. Decision restated from the Architecture Decisions register as approved at the AHDA gate of 19 Sep 2026 (Option B — GCP, in-Kingdom). Reconciled requirement stated; clause identifier recorded as unavailable. 11 binding localisation constraints derived (C-1 to C-11); 7 residual items registered (R-1 to R-7), of which 3 gate the first apply; 5 workbook inconsistencies specified. | Architecture (TASK-005) |

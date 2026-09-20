# ADR-001 — Confirmation Request

| Field | Value |
| --- | --- |
| Task | TASK-005 — Resolve Hosting & Data Localisation Architecture Decision Record (P1 - Architecture Decisions) |
| Relates to | `docs/architecture/adrs/ADR-001-hosting-and-data-localisation.md` (residual items R-1 to R-7) |
| Request date | 2026-09-20 |
| Status | **ISSUED — awaiting response** |
| Addressed to | AHDA IT (§3), AHDA Cybersecurity (§4), PMO Engagement Lead (§5) |
| Raised by | Engagement Architect |
| Response needed by | ______________ (set by PMO). Q1, Q2 and Q5 are needed **before the first `terraform apply`**; see §6. |
| Branch | `chore/task-005-task-005-hosting-adr` |

## 1. What this is

The hosting decision was taken at the AHDA gate on 19 Sep 2026 and is on record in the Architecture Decisions sheet as ADR-001. It is not in dispute and this request does not re-open it.

Three things about it are still open, and they are the reason Phase P3 cannot start: the decision has not been confirmed in writing, the in-Kingdom region has not been named, and it has not been stated whether the platform sits in AHDA's GCP organization or the delivery vendor's. This document asks the ten questions that close those items, plus four more that the record surfaced. Each question names who owns it, what it unblocks, and what happens if it is left open.

**Q1 is the one to answer first.** The decision reads the RFP's "internal hosting" requirement as AHDA's own controlled cloud tenancy rather than a physical facility. That reading is what makes Option B compliant. If the tenancy turns out to be the vendor's, the reading does not hold and the decision has to be re-taken — so confirming the decision in writing (Q5) before Q1 is answered would be confirming something that may not stand.

## 2. The decision being confirmed

Verbatim from the ADR-001 Selected Option cell. Signing §7 confirms this text and nothing wider:

> **Option B (cloud) — Google Cloud Platform, in-Kingdom.** AHDA already runs on GCP with no on-premises storage, so the RFP's 'internal hosting' wording is read as AHDA's own controlled cloud tenancy rather than a physical facility. **All data, backups and replicas remain inside Saudi Arabia.** Recovery objectives: **RPO 1 hour, RTO 4 to 6 hours** (AHDA IT business continuity plan).

Backup **retention period** is not part of this decision and no value is implied by it. It remains open with AHDA Cybersecurity and Records, gate Before Production.

## 3. For AHDA IT

| # | Question | Answer |
| --- | --- | --- |
| **Q1** | Does the platform sit in **AHDA's own GCP organization** or in **the delivery vendor's**? (R-3, UGV-07) | ☐ AHDA's own GCP organization  ☐ Vendor's organization  ☐ Other: ____________ |
| **Q2** | What is the **specific in-Kingdom region**? Google's Saudi Arabia region is `me-central2` (Dammam); please confirm or replace. (R-2, UGV-07) | Region: ______________ |
| **Q3** | Is a **second in-Kingdom location** available, and is it approved for use? This decides whether cross-region DR is possible at all within the Kingdom. | ☐ Yes: ______________  ☐ No  ☐ Unknown |
| **Q4** | **Cloud Run or GKE** for the application runtime? The pair has survived as an unresolved "or" in every cell that mentions it and has no owner. (R-6) | ☐ Cloud Run  ☐ GKE  ☐ Defer to Engagement Architect |

Notes on Q2 and Q3: the region cannot be changed after provisioning without rebuilding every environment, and the managed PostgreSQL product and the regional load-balancer/WAF pairing must each be confirmed available in the region before it is fixed.

## 4. For AHDA Cybersecurity

| # | Question | Answer |
| --- | --- | --- |
| **Q5** | Do you **confirm in writing** the 19 Sep 2026 decision as quoted in §2? (R-1) | ☐ Confirmed  ☐ Confirmed subject to: ____________  ☐ Not confirmed |
| **Q6** | Which **regulatory instrument** governs the localisation requirement, and what is AHDA's **data classification** for this platform's content? The workbook names neither. Candidates to confirm or replace: PDPL and its implementing regulations, NDMO data-management and personal-data-protection standards, CST Cloud Computing Regulatory Framework. (R-4) | Instrument(s): ______________<br>Classification: ______________ |
| **Q7** | Does that classification **permit this data in an in-Kingdom public-cloud region**? Residency and classification are different tests; keeping data in-Kingdom does not by itself satisfy a classification rule that bars a data class from public cloud. | ☐ Yes  ☐ No — see Q6 instrument  ☐ Conditional: ____________ |
| **Q8** | Scope confirmation on three points the record currently scopes by assumption: | |
| Q8a | **Non-production environments.** DEV, SIT and UAT hold seeded and migration-rehearsal data. Treat all four environments as in scope for localisation? (C-7) | ☐ Yes, all four  ☐ PROD/SIT only  ☐ Other: ____________ |
| Q8b | **CI/CD runners.** GitHub-hosted runners execute outside the Kingdom and migration/seed jobs can put real data through them. Are source, build artefacts and test data out of localisation scope, or must data-touching jobs run on self-hosted runners in the named region? (C-8) | ☐ Out of scope  ☐ Self-hosted required  ☐ Other: ____________ |
| Q8c | **Administrative access from outside the Kingdom** by vendor operations staff during the ~22-month operations period. Permitted, and under what controls? (C-9) | ☐ Permitted, controls: ____________  ☐ Not permitted  ☐ Other: ____________ |
| **Q9** | Is any **telemetry destination outside the Kingdom** acceptable (third-party SaaS APM)? Logs carry personal data until TASK-083's redaction is in place, and redaction reduces exposure rather than removing it. (C-6) | ☐ No external telemetry  ☐ Permitted: ____________ |

## 5. For the PMO Engagement Lead

| # | Question | Answer |
| --- | --- | --- |
| **Q10** | The **RFP clause identifier** (section/paragraph/page) for the internal-hosting and data-localisation requirement, and a copy of the clause text, for the baseline register per TASK-001 §5.1. The RFP is held neither in the repository nor in the connected Drive. (R-5) | Clause: ______________<br>Text attached: ☐ Yes ☐ No |
| **Q11** | The provisioning control proposed in ADR-001 §9 — **no `terraform apply` against any environment, including DEV, until Q1, Q2 and Q5 are closed in writing**. This is stricter than the acceptance criterion, which names only PROD and SIT. | ☐ Accepted  ☐ Replaced with: ____________ |
| **Q12** | The **workbook edits** specified in ADR-001 §10 (managed PostgreSQL product named three ways; OQ-001's blocking-task list wrong; ADR-001 status line omits the tenancy question; compute runtime has no owner; ADR row does not point at the full record). | ☐ Authorised  ☐ Declined  ☐ Partial: ____________ |

## 6. What each answer unblocks

| Question | Unblocks | If it stays open |
| --- | --- | --- |
| Q1 | The validity of ADR-001 itself | The "internal hosting" reading is unverified. If the answer is the vendor's organization, the decision is re-taken and Options A and C return. |
| Q2 | TASK-016, TASK-017 — the first `terraform apply` | No environment can be provisioned. Once applied, changing the region rebuilds every environment. |
| Q3 | TASK-023 — DR design | Cross-region DR cannot be designed; restore-from-backup within the region is assumed. |
| Q4 | TASK-017 — compute codified in IaC | TASK-017 cannot be written. The two runtimes differ in networking, scaling, cost model and operational burden. |
| Q5 | The TASK-005 validation check requiring AHDA IT/Cybersecurity sign-off | A contractual clause is being satisfied by an unsigned interpretation held by the delivery team. |
| Q6, Q7 | The sufficiency test behind the whole decision | Residency is confirmed; sufficiency is not. A classification that bars this data from public cloud would invalidate Option B. |
| Q8a–c, Q9 | Scope of constraints C-6 to C-9 across TASK-016, 018, 019, 022, 027, 033, 083, 089, 090, 092, 093, 097 | Those constraints stay scoped by assumption, and an assumption applied across twelve tasks is expensive to reverse. |
| Q10 | AHDA sign-off of ADR-001 | The interpretation stands against a one-line scope entry rather than the clause. The clause may qualify "internal" in a way that entry does not show. |
| Q11 | The hold on DEV provisioning | DEV is created before the tenancy question is answered; C-1's organization policy and C-6's log-bucket location are fixed at project creation and cannot be corrected later without rebuilding. |
| Q12 | Register accuracy | The workbook continues to name the database product three ways and the ADR row does not point at the full record. |

## 7. Return and sign-off

Return this document completed, or reply in writing citing the question numbers. On receipt it is filed beside ADR-001 and ADR-001's residual table is updated.

On Q1, Q2 and Q5 being answered, ADR-001's header status becomes **APPROVED** without qualification and the ADR-001 row's status line in the Architecture Decisions sheet is re-issued to match. Until then it remains **APPROVED — CONDITIONAL**.

| Role | Name | Signature | Date | Questions answered |
| --- | --- | --- | --- | --- |
| AHDA IT | | | | Q1–Q4 |
| AHDA Cybersecurity | | | | Q5–Q9 |
| PMO Engagement Lead | | | | Q10–Q12 |

## 8. Change log

| Date | Change | By |
| --- | --- | --- |
| 2026-09-20 | Issued. Twelve questions raised against ADR-001 residual items R-1 to R-7, grouped by owning role, with unblocking consequence per question. | Architecture (TASK-005) |

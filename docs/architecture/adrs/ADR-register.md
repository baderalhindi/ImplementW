# Architecture Decision Register

Reference copy of the workbook's Architecture Decisions sheet (ADR-001 to ADR-013), as supplied on 2026-09-25. Cell text is reproduced verbatim; notes appended to a cell in the sheet (separated by `||`) start on a new line after "Note:". The workbook remains the controlled source — if the two disagree, the workbook wins and this file is updated.

## Summary

| ID | Decision | Owner | Status |
| --- | --- | --- | --- |
| [ADR-001](#adr-001) | Hosting and data-localisation architecture | AHDA IT / Cybersecurity + Engagement Architect | Approved (AHDA gate, 19 Sep 2026) — written confirmation and region naming outstanding |
| [ADR-002](#adr-002) | Application technology stack | Engagement Architect | Proposed — Pending AHDA Approval |
| [ADR-003](#adr-003) | Solution architecture pattern | Engagement Architect | Proposed — Pending AHDA Approval |
| [ADR-004](#adr-004) | Notification channel scope for MVP | Engagement Architect | Approved (delivery team, 19 Sep 2026) — supersedes the Blueprint MVP channel scope; pending AHDA |
| [ADR-005](#adr-005) | Report export baseline | Engagement Architect | Approved (per Blueprint v2.0 ICD-17) |
| [ADR-006](#adr-006) | Dashboard and report count baseline for FG-01/FG-02 | PMO Engagement Lead + AHDA Business Sponsor | Approved (delivery team, 19 Sep 2026) — mapping tables pending AHDA |
| [ADR-007](#adr-007) | Identity provider and external identity verification | AHDA IT (Identity) + Engagement Architect | Approved (AHDA gate, 19 Sep 2026) |
| [ADR-008](#adr-008) | Financial source mode and reporting currency | AHDA Finance / IT + Engagement Architect | Approved (delivery team, 19 Sep 2026) — extends gate decision 5, pending AHDA |
| [ADR-009](#adr-009) | Physical progress derivation and weighting | AHDA PMO + Engagement Architect | Approved (AHDA gate, 19 Sep 2026) |
| [ADR-010](#adr-010) | Data sensitivity and field-level restriction | AHDA Cybersecurity + PMO | Approved (AHDA gate, 19 Sep 2026) — taxonomy outstanding |
| [ADR-011](#adr-011) | Risk and issue impact model | AHDA PMO / Risk and Portfolio Office | Approved (AHDA gate, 19 Sep 2026) — boundary values outstanding |
| [ADR-012](#adr-012) | Bilingual content policy | AHDA PMO + Engagement Architect | Approved (delivery team, 19 Sep 2026) — extends gate decision 8, pending AHDA |
| [ADR-013](#adr-013) | External participation model and project role eligibility | AHDA PMO + Business Sponsor + Cybersecurity | APPROVED (AHDA PMO, 20 Sep 2026) — the entity fills in the information and AHDA approves. Confirms the participation model. |

## Detail

### ADR-001

| Field | Value |
| --- | --- |
| Decision | Hosting and data-localisation architecture |
| Options considered | (A) Internal/on-premise AHDA-hosted infrastructure with KSA data residency, matching the RFP's stated requirement literally; (B) Google Cloud Platform as currently proposed (Cloud SQL for PostgreSQL, Cloud Run/GKE, HTTPS load balancer + Cloud Armor, single primary region); (C) KSA-region-approved sovereign/local cloud offering, if one exists and is AHDA-approved. |
| Selected option | Option B (cloud) — Google Cloud Platform, in-Kingdom. AHDA already runs on GCP with no on-premises storage, so the RFP's 'internal hosting' wording is read as AHDA's own controlled cloud tenancy rather than a physical facility. All data, backups and replicas remain inside Saudi Arabia. Recovery objectives: RPO 1 hour, RTO 4 to 6 hours (AHDA IT business continuity plan). |
| Rationale | The RFP explicitly requires internal hosting and data localisation, but the only detailed architecture proposal on file (the brief's "proposed architecture") is Google Cloud. This is a direct, unresolved conflict; it must be reconciled before any environment is provisioned (blocks TASK for environment separation and everything downstream in P3). |
| Impact | UNBLOCKED. Phase P3 Infrastructure/DevOps may proceed: TASK-016, TASK-017, TASK-019, TASK-020, TASK-021, TASK-023. RPO 1 hour drives backup frequency and point-in-time recovery; RTO 4 to 6 hours does not by itself require a hot standby, so re-test any regional high-availability cost against that target. OUTSTANDING: AHDA IT must name the specific in-Kingdom region and confirm whether the platform sits in AHDA's own GCP organization or the vendor's. |
| Owner | AHDA IT / Cybersecurity + Engagement Architect |
| Status | Approved (AHDA gate, 19 Sep 2026) — written confirmation and region naming outstanding |

### ADR-002

| Field | Value |
| --- | --- |
| Decision | Application technology stack |
| Options considered | (A) React + TypeScript SPA frontend / ASP.NET Core (LTS) modular-monolith backend / PostgreSQL (the brief's working proposal); (B) An AHDA-mandated stack, if AHDA IT standards require a specific platform not yet stated to the delivery team. |
| Selected option | Option A — React + TypeScript / ASP.NET Core / PostgreSQL (proposed, not yet AHDA-confirmed) |
| Rationale | The RFP does not prescribe a stack (per the brief's stated RFP ambiguity). Option A is carried as the working baseline solely to give this workbook concrete, navigable Canonical File Directory values; it is not yet an AHDA-approved decision. |
| Impact | Determines every Canonical File Directory value in this workbook; a change of stack requires reissuing directory values platform-wide.<br><br>Note: 19 Sep 2026: NOT answered by the AHDA decision gate. This is now the single remaining blocker to starting code — TASK-011 monorepo and TASK-014 local development cannot begin until it is approved. |
| Owner | Engagement Architect |
| Status | Proposed — Pending AHDA Approval |

### ADR-003

| Field | Value |
| --- | --- |
| Decision | Solution architecture pattern |
| Options considered | (A) Modular monolith with one bounded module per WF/FG domain; (B) Microservices per domain; (C) Monolith with no internal module boundaries. |
| Selected option | Option A — Modular monolith, one module per WF/FG domain |
| Rationale | Matches the platform's scale (150 named users, 21 WF/FG domains with explicit single-owner-per-fact rules in Blueprint Appendix D) without the operational overhead of microservices; module boundaries still enforce API-01/API-03 (no direct cross-domain DB writes) so the design can be split into services later if AHDA's scale changes. |
| Impact | Sets the internal project/module layout inside the backend solution for every domain task.<br><br>Note: 19 Sep 2026: NOT answered by the AHDA decision gate. This is now the single remaining blocker to starting code — TASK-011 monorepo and TASK-014 local development cannot begin until it is approved. |
| Owner | Engagement Architect |
| Status | Proposed — Pending AHDA Approval |

### ADR-004

| Field | Value |
| --- | --- |
| Decision | Notification channel scope for MVP |
| Options considered | (A) In-App and Email only (per Blueprint Section 15's stated MVP first-class channels); (B) In-App, Email and SMS from day one. |
| Selected option | Option A SUPERSEDED 19 Sep 2026 — In-App, Email AND SMS. SMS is enabled at launch as a third channel, applied to a narrow, configurable set of event families rather than to all notifications. The event-to-channel matrix, the recipient-role matrix and the Mandatory / User-configurable classification are FG-04 configuration that AHDA populates. |
| Rationale | Blueprint v2.0 Section 15 scoped MVP to in-app and email. Reversed because external entity adoption is the mechanism by which AHDA obtains live regional project data, and email to a municipality inbox is unreliably read. All messages are service messages, permitted 24/7 under CST rules; no marketing is sent, so no prior consent is required. |
| Impact | Adds an SMS provider adapter to the existing WF-15 channel abstraction, a verified mobile number on the user record, short SMS template variants, a delivery-receipt endpoint and four new environment variables. SMS carries no sensitive content — event plus deep link only. Arabic uses UCS-2 at 70 characters per segment, so cost is budgeted per segment. On by default with recipient opt-out; security and critical escalation events are mandatory. |
| Owner | Engagement Architect |
| Status | Approved (delivery team, 19 Sep 2026) — supersedes the Blueprint MVP channel scope; pending AHDA |

### ADR-005

| Field | Value |
| --- | --- |
| Decision | Report export baseline |
| Options considered | (A) PDF, XLSX, CSV only (per ICD-17); (B) Add DOCX export as a committed baseline capability. |
| Selected option | Option A — PDF, XLSX, CSV only |
| Rationale | ICD-17 (Step 14A final integration decision, carried into Blueprint v2.0) states DOCX export is not a committed baseline capability unless AHDA later approves it — this ADR ratifies the controlled source. |
| Impact | Scopes the FG-02 backend export-generator task; DOCX generation is excluded from MVP unless AHDA reopens ICD-17. |
| Owner | Engagement Architect |
| Status | Approved (per Blueprint v2.0 ICD-17) |

### ADR-006

| Field | Value |
| --- | --- |
| Decision | Dashboard and report count baseline for FG-01/FG-02 |
| Options considered | (A) "3 dashboards + 10 reports" as literally stated in the RFP; (B) "24 outputs" as stated in the BoQ; (C) The role-based catalogue actually specified in Blueprint Appendix B (12 DSH- dashboards, 11 SCR-13x report screens). |
| Selected option | Option A CONFIRMED and MAPPED 19 Sep 2026 — 3 dashboards and 10 reports. FG-01's twelve DSH definitions map to three delivered dashboards (Portfolio, Project, Governance); FG-02's twenty-six catalogue entries map to ten delivered reports. Counting basis: one dashboard or report is one named definition; role, permission, filter, parameter and mode variants are not separate deliverables. |
| Rationale | The contracted count stands and nothing is discarded — each definition is absorbed as a variant, served by a module screen, or left Conditional as the specification already has it. FG-02 5.1 states the catalogue is a design-ready baseline with final inclusion Configuration/TBC under ADM-037, so this supplies a decision the specification defers rather than overriding it. |
| Impact | Three dashboards are dense and heavily filter-driven; design and test effort exceeds three average screens and the estimate must say so. DSH-008 is a permission rendering of the Project Dashboard, not a fourth dashboard. Detailed risk, financial and KPI listings move from FG-01 to FG-02 reports per BR-DSH-045. |
| Owner | PMO Engagement Lead + AHDA Business Sponsor |
| Status | Approved (delivery team, 19 Sep 2026) — mapping tables pending AHDA |

### ADR-007

| Field | Value |
| --- | --- |
| Decision | Identity provider and external identity verification |
| Options considered | (A) AHDA enterprise directory with SSO, roles assigned in the platform; (B) directory groups mapped automatically to platform roles; (C) platform-local accounts |
| Selected option | Option A — AHDA's existing directory with single sign-on. The directory is authoritative for department, manager and job title. Platform roles are assigned in the platform, not inherited from directory groups. Nafath is in scope for external entity users, for identity verification only, never for internal sign-in. |
| Rationale | AHDA decision gate, 19 Sep 2026. Keeps role governance inside the platform where FG-03 can audit it, while trusting the directory for organizational attributes. |
| Impact | Scopes TASK-028 AD/LDAP and SSO, TASK-031/032 FG-03, and TASK-068 Nafath. Confirms the AD_\*, SSO_OIDC_\* and NAFATH_\* variables in Environment and Secrets. Exact directory product, topology and session-invalidation behavior confirmed with AHDA IT at environment setup.<br><br>Note: EXTENDED 19 Sep 2026 under ADR-013: external entity users require a working sign-in path, not one-time identity verification. Nafath verifies identity at onboarding; a persistent authenticated session follows. AHDA IT confirms the method. |
| Owner | AHDA IT (Identity) + Engagement Architect |
| Status | Approved (AHDA gate, 19 Sep 2026) |

### ADR-008

| Field | Value |
| --- | --- |
| Decision | Financial source mode and reporting currency |
| Options considered | (A) Manual entry only; (B) integrated from a finance source system at launch; (C) dual mode — manual at launch with integrated and hybrid modes built and left unconnected |
| Selected option | Option A EXTENDED 19 Sep 2026 — manual entry at launch, integration-ready, WITH PROVENANCE. Every financial record carries source (Manual, Etimad, Other), source reference, as-of date and entered-by. |
| Rationale | Manual figures without provenance lose leadership trust the first time they disagree with finance, and that trust does not return. Aligning the cost breakdown to Etimad categories now avoids a data remapping exercise later. Etimad access is a government approval process with its own lead time. |
| Impact | Four fields on the financial model; source and as-of shown on every financial figure in dashboards and reports; a FinancialSource interface with a Manual implementation so the Etimad adapter is an addition rather than a rewrite; cost breakdown aligned to Etimad categories from day one. |
| Owner | AHDA Finance / IT + Engagement Architect |
| Status | Approved (delivery team, 19 Sep 2026) — extends gate decision 5, pending AHDA |

### ADR-009

| Field | Value |
| --- | --- |
| Decision | Physical progress derivation and weighting |
| Options considered | (A) Entered by the project manager at project level; (B) derived from tasks, unweighted; (C) derived from tasks, weighted by duration; (D) derived, weighted by cost |
| Selected option | Option C — actual progress is captured at task level and rolled up through the work breakdown, weighted by planned duration. Planned % complete is calculated from the approved baseline and is never editable. Actual and planned are shown side by side at task, summary and project level. |
| Rationale | AHDA decision gate, 19 Sep 2026. Cost weighting is unavailable because financial data is manual and held at project total level only (ADR-008), so no reliable task-level cost exists. |
| Impact | Scopes TASK-044 WF-02, TASK-046 WF-03, TASK-048 WF-04 and TASK-025 core schema, which must carry activity duration weighting. A task's actual percentage is maintained by the task owner and editable by the Project Manager; the project roll-up is calculated and may be overridden only with a recorded reason, with the calculated value retained and the figure flagged as overridden. Requires an approved active baseline before a project goes Active. Override tolerance remains TBC. |
| Owner | AHDA PMO + Engagement Architect |
| Status | Approved (AHDA gate, 19 Sep 2026) |

### ADR-010

| Field | Value |
| --- | --- |
| Decision | Data sensitivity and field-level restriction |
| Options considered | (A) Record-level access control only; (B) record-level plus field-level masking by audience |
| Selected option | Option B — field-level restriction is required. Financial values and any field AHDA classifies as sensitive are masked or withheld by audience across screens, dashboards, reports, exports and API projections. Step-up authentication applies to privileged and sensitive actions. |
| Rationale | AHDA decision gate, 19 Sep 2026. Deciding this late would force a revisit of every projection in the platform, so it is fixed before the first projection is built. |
| Impact | Scopes TASK-030 RBAC and data-scope engine, TASK-029 MFA, TASK-069/070 FG-01, TASK-071/072 FG-02, TASK-083 logging redaction. OUTSTANDING: the classification taxonomy and the specific field list, to be supplied by AHDA Cybersecurity; the step-up trigger list follows from it. |
| Owner | AHDA Cybersecurity + PMO |
| Status | Approved (AHDA gate, 19 Sep 2026) — taxonomy outstanding |

### ADR-011

| Field | Value |
| --- | --- |
| Decision | Risk and issue impact model |
| Options considered | (A) Single overall impact scale; (B) several impact dimensions scored separately, each on five levels |
| Selected option | Option B — impact is assessed across several dimensions: cost, schedule, reputation and at least one operational dimension, each on five levels. The same dimension set serves both risks and issues. |
| Rationale | AHDA decision gate, 19 Sep 2026. The dimension count changes the risk and issue record structure, so it is fixed before Phase P9 build. |
| Impact | Scopes TASK-055 WF-06, TASK-057 WF-07, TASK-034 FG-04 configuration and TASK-025 core schema. OUTSTANDING: level descriptions and quantitative boundaries per dimension, plus the 5x5 matrix cell-to-rating mapping and rating labels, all to be supplied by AHDA. The engine is built generically and seeded once the values arrive. |
| Owner | AHDA PMO / Risk and Portfolio Office |
| Status | Approved (AHDA gate, 19 Sep 2026) — boundary values outstanding |

### ADR-012

| Field | Value |
| --- | --- |
| Decision | Bilingual content policy |
| Options considered | (A) Interface bilingual, all content mandatory in both languages; (B) interface bilingual, controlled master data bilingual, free text in the language entered; (C) interface bilingual only |
| Selected option | Bilingual policy CONFIRMED and EXTENDED 19 Sep 2026. Interface and master data bilingual; narrative stored as entered per gate decision 8. ADDED: each narrative field stores its entry language. Mixed-language reporting accepted. |
| Rationale | Structured data stays bilingual, so dashboards, filters and labels work fully in either language — only free text is mixed, a far smaller problem. The language tag cannot be backfilled once narrative exists. |
| Impact | A language attribute on every narrative field; language-aware grouping in reports. Leadership summaries are written in Arabic at the AHDA end with source narratives beneath as evidence. No translation is stored as the record. |
| Owner | AHDA PMO + Engagement Architect |
| Status | Approved (delivery team, 19 Sep 2026) — extends gate decision 8, pending AHDA |

### ADR-013

| Field | Value |
| --- | --- |
| Decision | External participation model and project role eligibility |
| Options considered | (A) Project Manager restricted to AHDA employees, entity staff as reviewed contributors; (B) employer-neutral project role with AHDA holding the approval and lifecycle gates; (C) hybrid by project classification |
| Selected option | Option B — R04 Project Manager is employer-neutral. A person employed by the delivering entity may hold the Project Manager role on their own entity's project, scoped to that project. Contributors who do not manage the project remain request-driven and reviewed. Entities may create a project draft that AHDA approves. Entities see progress, schedule status and health on their own projects. AHDA retains every approval and lifecycle gate. |
| Rationale | Regional projects are delivered by the entities that own them. Adoption is the mechanism by which AHDA gets live regional project data at all: if the platform is a one-way reporting obligation, entities do the minimum and the data goes stale. The boundary that protects AHDA is authority and scope, not employer. |
| Impact | Scopes TASK-028 to TASK-032 identity, TASK-041/042 WF-01, TASK-066/067/068 WF-13 and Nafath, TASK-069 to TASK-072 FG-01 and FG-02. DSH-008 moves to launch scope. Thirty-five clause amendments across twenty controlled specifications, recorded in the Participation Amendment Pack v1.0. Closes finding DEP-F-002. Re-opens ADR-006.<br><br>Note: CONFIRMED 20 Sep 2026. Terminology clarified: 'external entity' means any organisation delivering a regional project — government entities, other public authorities and private companies — not municipalities specifically. |
| Owner | AHDA PMO + Business Sponsor + Cybersecurity |
| Status | APPROVED (AHDA PMO, 20 Sep 2026) — the entity fills in the information and AHDA approves. Confirms the participation model. |

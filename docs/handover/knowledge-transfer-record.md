# Training and Knowledge-Transfer Record

| Field | Value |
| --- | --- |
| Task | TASK-097 — Compile Operational Handover Package (P17 - Documentation) |
| Part of | [`README.md`](README.md) — the handover package; its index IDs (H-nn) name the materials here |
| Record date | 2026-10-24 |
| Status | **PLANNED — NO SESSION HELD.** The five areas of Client Scope §12 are mapped to their audiences, materials and a test of completion (§2), and the sessions are planned (§3). None has been held: the hands-on sessions need an environment to train on, and none exists (README §7 O-5). The log (§4) is empty |
| Source | *Client Original Project Scope* §12; *AHDA Master Plan* activities 3.9 ("Training, admin handover and user documentation"), 3.10 ("Related-entity workshops, training and onboarding") and 4.9 ("Documentation, knowledge transfer and service reporting"); Guide §13, go-live row "Users" |

## 1. What the contract asks

*Client Original Project Scope* §12, «التدريب ونقل المعرفة»:

> يشمل المشروع تدريب ونقل المعرفة لموظفي الهيئة والجهات ذات العلاقة في المجالات التالية: استخدام المنصة. إدخال وتحديث
> بيانات المشاريع. إدارة وتشغيل المنصة. تقديم الدعم الفني ومعالجة المشكلات. تمكين موظفي الهيئة من نقل المعرفة وتدريب
> مستخدمين آخرين. وتتطلب الكراسة تنفيذ تدريب عملي وورش عمل خلال مرحلة التنفيذ، مع استمرار نقل المعرفة خلال مدة العقد.

In English (the delivery team's translation): the project includes training and knowledge transfer for the staff of
AHDA and of the related entities in five areas — using the platform; entering and updating project data; administering
and operating the platform; providing technical support and resolving problems; and enabling AHDA's staff to transfer
the knowledge and train other users. The RFP booklet requires hands-on training and workshops during implementation,
with knowledge transfer continuing for the length of the contract.

## 2. The five areas

Each area is complete when its test passes for the audience named, and the session that showed it is in the log (§4).

| Area | Client Scope §12 | Audience | Materials | Done when |
| --- | --- | --- | --- | --- |
| KT-1 | Using the platform («استخدام المنصة») | AHDA users of every role, R01 to R07; external entity users, R08 | H-08, H-09, H-10, H-11 | A trainee of each role signs in, reaches their landing page (Blueprint §20.2) and finds their way through Help and Knowledge without help |
| KT-2 | Entering and updating project data («إدخال وتحديث بيانات المشاريع») | Project managers (R04), liaison officers (R05), external entity users (R08) | H-09 and H-10 articles `register-a-project`, `submit-a-progress-update`, `decide-an-approval`; H-75 for a bulk load | A trainee registers a project, submits a progress update and sees it through its approval, in UAT |
| KT-3 | Administering and operating the platform («إدارة وتشغيل المنصة») | AHDA platform administrators (R01); AHDA IT operations; a developer joining for maintenance | README §5.1; H-70 to H-76; H-20, H-22, H-50 to H-58; H-36, H-40, H-43; H-07 | An AHDA administrator does each task of README §5.1 that has a screen, in UAT; AHDA IT can say for each environment change which record governs it |
| KT-4 | Providing technical support and resolving problems («تقديم الدعم الفني ومعالجة المشكلات») | AHDA IT service desk; AHDA IT; AHDA Cybersecurity | H-01, H-02, H-30 to H-35, H-40, H-61 to H-66, H-74 | The service desk classifies the 24 sample incidents of H-02 §5 to the tier given; AHDA IT has observed one live drill on SIT (T-08) |
| KT-5 | Enabling AHDA staff to train others («تمكين موظفي الهيئة من نقل المعرفة وتدريب مستخدمين آخرين») | Trainers AHDA nominates | KT-1's and KT-2's materials (H-08 to H-11), and a trainer pack that does not exist yet (README F-4) | An AHDA trainer delivers T-01 to a group, observed by the delivery team's business analyst |

## 3. Sessions planned

Hands-on sessions run on UAT with the UAT data set (`docs/testing/uat/uat-execution.md`), never on PROD data. External
entity users train with restricted permissions only, never demonstration rights (Master Plan 3.10).

| # | Session | Areas | Audience | Delivered by | Needs first | Materials |
| --- | --- | --- | --- | --- | --- | --- |
| T-01 | Platform orientation, one per role: sign in, landing page, menus, Help and Knowledge | KT-1 | AHDA users, by role | Business Analyst | A stable UAT build (Master Plan 3.9) | H-08 to H-11 |
| T-02 | Related-entity workshop: sign in, assigned projects, first data entry | KT-1, KT-2 | External entity users (R08) | Business Analyst | UAT; external pilot accounts | H-09, H-10 |
| T-03 | Entering and updating project data: registration, progress updates, approvals | KT-2 | R04, R05, R08 | Business Analyst | UAT | H-09, H-10 |
| T-04 | Loading legacy data with the import templates | KT-2, KT-3 | R01; AHDA data owners | Backend Lead | UAT; the legacy-data plan unblocked (OQ-008) | H-75, H-45 |
| T-05 | Platform administration: users, roles, assignments, organisation, integrations, audit trail | KT-3 | R01 | Backend Lead | UAT | README §5.1; H-70 to H-74, H-76 |
| T-06 | Operating the platform: architecture, environments, Terraform, the promotion pipeline, secrets, backups | KT-3 | AHDA IT | DevOps/Platform Lead | SIT | H-20, H-22, H-50 to H-58, H-36, H-40 |
| T-07 | Support: classifying an incident, the six-step procedure, the playbooks; a table-top walk of the on-call runbook's P1 scenarios | KT-4 | AHDA IT service desk; AHDA IT | DevOps/Platform Lead | None — a desk exercise | H-01, H-02 |
| T-08 | Live drill on SIT, observed: the database-outage drill and the restore drill | KT-4 | AHDA IT | DevOps/Platform Lead | SIT; an operator path into the VPC (README §7 O-6) | H-30, H-40, H-41 |
| T-09 | Security operations: the security incident playbook (PB-3), the findings log, the weekly scans, the audit trail | KT-4 | AHDA Cybersecurity | Security Lead | None for the walk-through; UAT for the audit trail | H-01, H-61 to H-66, H-74 |
| T-10 | Train the trainer | KT-5 | AHDA's nominated trainers | Business Analyst | The trainer pack (README F-4); T-01 delivered once | H-08 to H-11 |
| T-11 | Maintenance developer onboarding: the guide followed from a clean machine | KT-3 | A developer new to the codebase | Any delivery-team engineer, observing only | None | H-07 |

T-07 and T-09 need no environment and can be held first. T-11 is also the human dry run TASK-095 still owes (README §7
O-19).

## 4. Session log

One row per session held. Attendance is recorded by role and count only: the attendance sheet with names is kept in
AHDA's records, not in this public repository. The evidence column names where the sheet and any assessment results
are held.

| # | Date | Areas | Session | Audience (roles, count) | Delivered by (role) | Evidence held at |
| --- | --- | --- | --- | --- | --- | --- |

No session has been held.

## 5. Knowledge transfer during the operations period

Client Scope §12 continues the transfer for the length of the contract. It happens through:

| Channel | When | What is transferred |
| --- | --- | --- |
| The monthly periodic visit (Client Scope §11) | Monthly | Post-incident reviews of every P1 and P2 (H-01 §7); changes released since the last visit |
| The record of each change | Every PR | A change to a documented behaviour updates its record and its help article in the same PR (README §6 C-12) |
| The handover package itself | At each review | This index is re-issued with the records TASK-098 to TASK-102 add (README §5) |
| A new rota member's handover | Each rotation change | H-01 §3: open incidents, alerts closed as noise, planned changes, access confirmed |

## 6. Change log

| Date | Change |
| --- | --- |
| 2026-10-24 | Written: the five areas with audiences, materials and tests; eleven sessions planned; empty log |

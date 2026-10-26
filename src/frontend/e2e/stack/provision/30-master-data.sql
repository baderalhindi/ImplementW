-- 30-master-data.sql — the published master data the journeys pick from (TASK-085). db/seed publishes only the governance
-- profiles, impact dimensions and external entity types; every other catalogue is empty until AHDA's values are loaded
-- (FG-04, TASK-027 §4). These items are synthetic, coded E2E_*, and labelled in both languages.
--
-- Synthetic kind e2e9. Idempotent: safe to re-run.

INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, parent_item_id, sort_order, lifecycle_state, published_at,
                                                 created_at, created_by, updated_at, updated_by)
SELECT i.id::uuid, c.id, i.code, i.label_ar, i.label_en, i.parent::uuid, i.sort_order, 'PUBLISHED', now(),
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('00000000-e2e9-4000-8000-000000000001', 'PROJECT_CLASSIFICATION', 'E2E_INFRASTRUCTURE', 'بنية تحتية', 'Infrastructure', NULL, 1),
    ('00000000-e2e9-4000-8000-000000000002', 'PROJECT_CLASSIFICATION', 'E2E_DEVELOPMENT', 'تنمية', 'Development', NULL, 2),
    ('00000000-e2e9-4000-8000-000000000011', 'REGION', 'E2E_NORTH', 'المنطقة الشمالية', 'Northern region', NULL, 1),
    ('00000000-e2e9-4000-8000-000000000012', 'REGION', 'E2E_SOUTH', 'المنطقة الجنوبية', 'Southern region', NULL, 2),
    ('00000000-e2e9-4000-8000-000000000021', 'CITY', 'E2E_NORTH_CITY', 'مدينة الشمال', 'North city', '00000000-e2e9-4000-8000-000000000011', 1),
    ('00000000-e2e9-4000-8000-000000000022', 'CITY', 'E2E_SOUTH_CITY', 'مدينة الجنوب', 'South city', '00000000-e2e9-4000-8000-000000000012', 2),
    ('00000000-e2e9-4000-8000-000000000031', 'DOCUMENT_CONTROL_LEVEL', 'E2E_CONTROLLED', 'مضبوط', 'Controlled', NULL, 1),
    ('00000000-e2e9-4000-8000-000000000041', 'RISK_CATEGORY', 'E2E_WEATHER', 'الطقس', 'Weather', NULL, 1),
    ('00000000-e2e9-4000-8000-000000000042', 'RISK_CATEGORY', 'E2E_COMMERCIAL', 'تجاري', 'Commercial', NULL, 2),
    ('00000000-e2e9-4000-8000-000000000051', 'CONCERN_CATEGORY', 'E2E_SITE_ACCESS', 'الوصول إلى الموقع', 'Site access', NULL, 1),
    ('00000000-e2e9-4000-8000-000000000061', 'PRIORITY', 'E2E_HIGH', 'عالية', 'High', NULL, 1),
    ('00000000-e2e9-4000-8000-000000000062', 'PRIORITY', 'E2E_LOW', 'منخفضة', 'Low', NULL, 2),
    ('00000000-e2e9-4000-8000-000000000071', 'CONCERN_SEVERITY', 'MINOR', 'طفيفة', 'Minor', NULL, 1),
    ('00000000-e2e9-4000-8000-000000000072', 'CONCERN_SEVERITY', 'MAJOR', 'كبيرة', 'Major', NULL, 2),
    ('00000000-e2e9-4000-8000-000000000073', 'CONCERN_SEVERITY', 'CRITICAL', 'حرجة', 'Critical', NULL, 3),
    ('00000000-e2e9-4000-8000-000000000081', 'MILESTONE_CATEGORY', 'E2E_DELIVERY', 'تسليم', 'Delivery', NULL, 1),
    ('00000000-e2e9-4000-8000-000000000091', 'EVIDENCE_TYPE', 'E2E_COMPLETION_CERTIFICATE', 'شهادة إنجاز', 'Completion certificate', NULL, 1),
    ('00000000-e2e9-4000-8000-000000000092', 'EVIDENCE_TYPE', 'E2E_BUDGET_APPROVAL', 'اعتماد الميزانية', 'Budget approval', NULL, 2),
    ('00000000-e2e9-4000-8000-0000000000a1', 'DOCUMENT_TYPE', 'E2E_REPORT', 'تقرير', 'Report', NULL, 1),
    ('00000000-e2e9-4000-8000-0000000000a2', 'DOCUMENT_TYPE', 'E2E_LETTER', 'خطاب', 'Letter', NULL, 2),
    ('00000000-e2e9-4000-8000-0000000000b1', 'DATA_CLASSIFICATION', 'E2E_INTERNAL', 'داخلي', 'Internal', NULL, 10),
    ('00000000-e2e9-4000-8000-0000000000b2', 'DATA_CLASSIFICATION', 'E2E_RESTRICTED', 'مقيد', 'Restricted', NULL, 20),
    ('00000000-e2e9-4000-8000-0000000000c1', 'KPI_UNIT', 'E2E_PERCENT', 'نسبة مئوية', 'Percent', NULL, 1),
    ('00000000-e2e9-4000-8000-0000000000d1', 'MEASUREMENT_FREQUENCY', 'E2E_MONTHLY', 'شهري', 'Monthly', NULL, 1),
    ('00000000-e2e9-4000-8000-0000000000e1', 'CONTRIBUTION_TYPE', 'E2E_PROGRESS_UPDATE', 'تحديث التقدم', 'Progress update', NULL, 1))
    AS i (id, catalogue, code, label_ar, label_en, parent, sort_order)
JOIN master_data_config.master_data_catalogue c ON c.code = i.catalogue
ON CONFLICT (id) DO NOTHING;

-- UJ-005 and the ADR-013 participation specs (TASK-085): WF-13 issues a request only for a CONTRIBUTION_TYPE whose code names one
-- of the platform's typed schemas (external-participation.md D-8): TASK_PROGRESS, applied to a WF-04 task, and
-- PROJECT_INFORMATION, reference-only. E2E_PROGRESS_UPDATE above has no schema (422 EXTERNAL_REQUEST_SCHEMA_INVALID).
INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, parent_item_id, sort_order, lifecycle_state, published_at,
                                                 created_at, created_by, updated_at, updated_by)
SELECT i.id::uuid, c.id, i.code, i.label_ar, i.label_en, NULL, i.sort_order, 'PUBLISHED', now(),
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('00000000-e2e9-4000-8000-000000000301', 'TASK_PROGRESS', 'تقدم المهمة', 'Task progress', 11),
    ('00000000-e2e9-4000-8000-000000000302', 'PROJECT_INFORMATION', 'معلومات المشروع', 'Project information', 12))
    AS i (id, code, label_ar, label_en, sort_order)
JOIN master_data_config.master_data_catalogue c ON c.code = 'CONTRIBUTION_TYPE'
ON CONFLICT (id) DO NOTHING;

-- UJ-011 (TASK-085), and any journey that uploads or reads a document: a permission's clearance is the highest
-- DATA_CLASSIFICATION it may expose, and a permission without one clears nothing (authorization-engine.md D-6), so with the
-- shipped catalogue no document can be uploaded or read (403 on POST /documents). The clearance taxonomy is AHDA
-- Cybersecurity's (UGV-01); this is the test value the document integration host uses (DocumentTestHost): the document
-- permissions are cleared for E2E_INTERNAL, so an E2E_RESTRICTED document stays out of reach.
UPDATE identity_access.permission SET data_classification_item_id = '00000000-e2e9-4000-8000-0000000000b1'
WHERE code IN ('DOCUMENT_VIEW', 'DOCUMENT_UPLOAD', 'DOCUMENT_MANAGE');

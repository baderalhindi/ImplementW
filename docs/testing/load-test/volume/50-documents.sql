-- 50-documents.sql — WF-12 documents at indexing-strategy.md §5.2's volume (TASK-086): SCR-120 Document Library, SCR-121
-- Project Documents and SCR-122 Recent Documents read these rows.
--
-- document_management.document          60,000  20 per project (d = 1..20, n = (p-1)*20 + d, id 10b1-n), owned by the project's
--                                                Project Manager; E2E_REPORT or E2E_LETTER by hash; classified E2E_INTERNAL,
--                                                which 30-master-data.sql clears the document permissions for, except one in
--                                                twenty E2E_RESTRICTED (out of every load user's reach, as a real library has);
--                                                one in twenty-five ARCHIVED; updated_at a fixed permutation over two years
-- document_management.document_version  60,000  one per document (10b2-n), CLEAN, so every document lists, opens and shows
--                                                its latest version. §5.2 does not size versions; one per document is the floor.
--                                                No object is stored: a download is not part of the load profile, and would
--                                                answer that the object is missing
--
-- No business_link or evidence_reference: the registers list by project_id, and evidence is not on the load profile.
-- Guards off (session_replication_role = replica), as the repository's fixtures write stored facts; CHECKs hold, and
-- validate-data-integrity.sql checks the foreign keys afterwards. Dates relative to the load, as in 20-projects.sql.

SELECT NOT EXISTS (SELECT 1 FROM document_management.document WHERE id = '00000000-10b1-4000-8000-000000000001') AS load_documents_fresh \gset
\if :load_documents_fresh

SET LOCAL session_replication_role = replica;

INSERT INTO document_management.document (id, document_type_item_id, data_classification_item_id, project_id, owner_user_id, status, description_lang, description,
                                          title_lang, title, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10b1-4000-8000-' || lpad(x.n::text, 12, '0'))::uuid,
       CASE WHEN (hashint4(x.n * 31 + 1) & 1048575) % 3 = 0 THEN '00000000-e2e9-4000-8000-0000000000a2'::uuid ELSE '00000000-e2e9-4000-8000-0000000000a1'::uuid END,
       CASE WHEN x.n % 20 = 0 THEN '00000000-e2e9-4000-8000-0000000000b2'::uuid ELSE '00000000-e2e9-4000-8000-0000000000b1'::uuid END,
       pr.id, pr.project_manager_user_id,
       CASE WHEN (hashint4(x.n * 31 + 2) & 1048575) % 25 = 0 THEN 'ARCHIVED' ELSE 'ACTIVE' END,
       NULL, NULL,
       pr.title_lang, CASE WHEN pr.title_lang = 'ar' THEN 'مستند الحمل ' || x.n ELSE 'Load document ' || x.n END,
       t.updated_at - ((hashint4(x.n * 31 + 3) & 1048575) % 30) * interval '1 day' - interval '1 hour', pr.project_manager_user_id,
       t.updated_at, pr.project_manager_user_id
FROM generate_series(1, 3000) AS p
JOIN project.project pr ON pr.id = ('00000000-10a6-4000-8000-' || lpad(p::text, 12, '0'))::uuid
CROSS JOIN LATERAL (SELECT (p - 1) * 20 + d AS n FROM generate_series(1, 20) AS d) AS x
CROSS JOIN LATERAL (SELECT now() - ((x.n::bigint * 7919) % 60000) * interval '1051 seconds' AS updated_at) AS t;

INSERT INTO document_management.document_version (id, document_id, version_no, storage_object_key, file_name, content_type, size_bytes, checksum_sha256,
                                                  uploaded_by_user_id, uploaded_at, scan_state, scan_completed_at, scan_reference,
                                                  created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10b2-4000-8000-' || right(d.id::text, 12))::uuid, d.id, 1, 'load/documents/' || d.id || '/v1', 'load-document-' || right(d.id::text, 12)::integer || '.pdf',
       'application/pdf', 20000 + (hashint4(right(d.id::text, 12)::integer * 31 + 4) & 1048575) % 5000000,
       md5('load-document-a:' || d.id) || md5('load-document-b:' || d.id),
       d.owner_user_id, d.created_at, 'CLEAN', d.created_at + interval '2 minutes', 'load-scan-' || right(d.id::text, 12),
       d.created_at, d.owner_user_id, d.created_at + interval '2 minutes', '00000000-0000-4000-8000-0000000000ff'
FROM document_management.document d
WHERE d.id::text LIKE '00000000-10b1-%';

\endif

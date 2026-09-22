-- verify-seed.sql — one row per canonical role with its local user (TASK-014 acceptance: at least one user per R01–R08).
-- Usage (smoke-test.sh runs it; psql exits non-zero when a role has no active local user):
--   docker compose -f infra/docker/docker-compose.yml exec -T postgres psql -U pmplatform -d pmplatform < infra/docker/postgres/verify-seed.sql
\set ON_ERROR_STOP on
SELECT r.code AS role, u.username, u.user_type, ar.status,
       ar.department_id IS NOT NULL AS dept_anchor,
       ar.external_entity_id IS NOT NULL AS entity_anchor
FROM identity_access.role r
LEFT JOIN identity_access.permission_profile p ON p.base_role_id = r.id AND p.is_shipped_default
LEFT JOIN identity_access.permission_profile_version v ON v.permission_profile_id = p.id AND v.lifecycle_state = 'PUBLISHED'
LEFT JOIN identity_access.access_relationship ar ON ar.permission_profile_version_id = v.id AND ar.status = 'ACTIVE'
LEFT JOIN identity_access."user" u ON u.id = ar.user_id
ORDER BY r.code;

DO $$
DECLARE missing text;
BEGIN
    SELECT string_agg(r.code, ', ' ORDER BY r.code) INTO missing
    FROM identity_access.role r
    WHERE r.code IN ('R01', 'R02', 'R03', 'R04', 'R05', 'R06', 'R07', 'R08')
      AND NOT EXISTS (
        SELECT 1
        FROM identity_access.permission_profile p
        JOIN identity_access.permission_profile_version v ON v.permission_profile_id = p.id
        JOIN identity_access.access_relationship ar ON ar.permission_profile_version_id = v.id AND ar.status = 'ACTIVE'
        WHERE p.base_role_id = r.id AND p.is_shipped_default);
    IF missing IS NOT NULL THEN
        RAISE EXCEPTION 'No active local user for role(s): %', missing;
    END IF;
END $$;

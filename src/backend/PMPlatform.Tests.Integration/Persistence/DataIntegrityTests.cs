using Npgsql;
using PMPlatform.Infrastructure.Persistence;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// TASK-027 acceptance for <c>db/seed/validate-data-integrity.sql</c>: a migrated and seeded database passes; each
/// kind of violation, injected alone, fails the script with SQLSTATE 23000 and is the only violation it reports; and
/// the release image's <c>validate-data-integrity</c> command fails on a committed orphan, so the deployment step does.
/// </summary>
public sealed class DataIntegrityTests(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    private const string Actor = "'00000000-0000-4000-8000-0000000000ff'";

    /// <summary>A department whose parent does not exist. The foreign key would refuse it, so each case below gets it past the key its own way.</summary>
    private const string OrphanedDepartment = $"""
        INSERT INTO identity_access.department (id, code, name_ar, name_en, parent_department_id, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), 'DEPT-ORPHAN', 'إدارة يتيمة', 'Orphaned department', gen_random_uuid(), now(), {Actor}, now(), {Actor});
        """;

    public static TheoryData<string, string, string> Violations => new()
    {
        {
            // The workbook's validation cell: an orphaned row in a non-PROD copy. A bulk load with constraint
            // triggers off (pg_restore --disable-triggers) is how one gets past the foreign key.
            "orphan loaded with triggers off",
            "SET LOCAL session_replication_role = replica;\n" + OrphanedDepartment,
            "ORPHANED_FOREIGN_KEY identity_access.department fk_department_department_parent_department_id: 1 row(s) reference no identity_access.department row"
        },
        {
            "orphan behind a NOT VALID foreign key",
            "ALTER TABLE identity_access.department DROP CONSTRAINT fk_department_department_parent_department_id;\n" + OrphanedDepartment +
            "ALTER TABLE identity_access.department ADD CONSTRAINT fk_department_department_parent_department_id FOREIGN KEY (parent_department_id) REFERENCES identity_access.department (id) NOT VALID;",
            "ORPHANED_FOREIGN_KEY identity_access.department fk_department_department_parent_department_id: 1 row(s)"
        },
        {
            "row behind a NOT VALID check",
            """
            ALTER TABLE master_data_config.probability_level_definition DROP CONSTRAINT ck_probability_level_definition_level;
            UPDATE master_data_config.probability_level_definition SET level = 6 WHERE level = 5;
            ALTER TABLE master_data_config.probability_level_definition ADD CONSTRAINT ck_probability_level_definition_level CHECK (level BETWEEN 1 AND 5) NOT VALID;
            """,
            "CHECK_VIOLATION master_data_config.probability_level_definition ck_probability_level_definition_level: 1 row(s)"
        },
        {
            "invalid unique index",
            "UPDATE pg_index SET indisvalid = false WHERE indexrelid = 'identity_access.ix_role_code'::regclass;",
            "INVALID_INDEX identity_access.ix_role_code on identity_access.role: invalid; its unique key is not enforced"
        },
        {
            "audit column naming no user",
            "UPDATE master_data_config.configuration_family SET updated_by = gen_random_uuid() WHERE code = 'KPI_POLICY';",
            "ORPHANED_AUDIT_ACTOR master_data_config.configuration_family: 1 row(s) created or updated by no user"
        },
        {
            "reference to another catalogue's item",
            $"""
            INSERT INTO identity_access.external_entity (id, code, name_ar, name_en, entity_type_item_id, status, created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), 'ENT-WRONG', 'جهة', 'Entity', i.id, 'ACTIVE', now(), {Actor}, now(), {Actor}
            FROM master_data_config.master_data_item i WHERE i.code = 'LIGHT';
            """,
            "WRONG_CATALOGUE identity_access.external_entity.entity_type_item_id: 1 row(s) reference an item outside EXTERNAL_ENTITY_TYPE"
        },
        {
            "master data reference with no catalogue",
            "ALTER TABLE project.project ADD COLUMN sector_item_id uuid REFERENCES master_data_config.master_data_item (id);",
            "UNMAPPED_MASTER_DATA_REFERENCE project.project.sector_item_id: no catalogue in validate-data-integrity.sql"
        },
    };

    [Fact]
    public async Task ASeededDatabaseHasNoViolation()
    {
        await database.RunCommandAsync(DatabaseScripts.ValidateDataIntegrityCommand);
    }

    [Theory]
    [MemberData(nameof(Violations))]
    public async Task EachViolationIsReportedAlone(string violation, string injection, string expected)
    {
        PostgresException failure = await Assert.ThrowsAsync<PostgresException>(
            () => database.ExecuteRolledBackAsync(injection + "\n" + DatabaseScripts.Read("validate-data-integrity.sql")));

        Assert.True(failure.SqlState == PostgresErrorCodes.IntegrityConstraintViolation, $"{violation}: {failure.SqlState} {failure.MessageText}");
        string[] lines = failure.MessageText.Split('\n');
        Assert.Equal("data integrity: 1 violation(s)", lines[0]);
        Assert.StartsWith(expected, lines[1], StringComparison.Ordinal);
    }

    /// <summary>What the deployment step runs: the command, against committed data, must fail rather than report.</summary>
    [Fact]
    public async Task TheValidateCommandFailsOnACommittedOrphan()
    {
        SeededDatabase scratch = new();
        await scratch.InitializeAsync();
        try
        {
            await scratch.ExecuteAsync("SET session_replication_role = replica;\n" + OrphanedDepartment);

            PostgresException failure = await Assert.ThrowsAsync<PostgresException>(
                () => scratch.RunCommandAsync(DatabaseScripts.ValidateDataIntegrityCommand));
            Assert.Equal(PostgresErrorCodes.IntegrityConstraintViolation, failure.SqlState);
        }
        finally
        {
            await scratch.DisposeAsync();
        }
    }
}

using System.Text.RegularExpressions;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Infrastructure.Persistence;

namespace PMPlatform.Tests.Integration.Persistence;

/// <summary>
/// TASK-027 acceptance for <c>db/seed/seed-master-data.sql</c>, loaded through the release image's <c>seed</c> command:
/// a repeated run adds and changes nothing, the eight canonical roles are there, every label is bilingual (ADR-012),
/// the three governance profiles are seeded (ADR-015), the risk and issue scale is generic and unpublished (OQ-006),
/// no materiality band is invented (OQ-013), and AHDA's wording survives a re-seed. TASK-030: the permission catalogue
/// and the shipped-default grants are PermissionCatalogue's.
/// </summary>
public sealed partial class SeedDataTests(SeededDatabase database) : IClassFixture<SeededDatabase>
{
    private static readonly string[] CanonicalRoles = ["R01", "R02", "R03", "R04", "R05", "R06", "R07", "R08"];

    /// <summary>The workbook's validation cell: run the seed twice and the row counts are unchanged. The fingerprint also proves no row was rewritten.</summary>
    [Fact]
    public async Task RunningTheSeedAgainChangesNoRow()
    {
        IReadOnlyList<string> before = await database.QueryAsync(SeededDatabase.TableFingerprints);

        await database.RunCommandAsync(DatabaseScripts.SeedCommand);

        IReadOnlyList<string> after = await database.QueryAsync(SeededDatabase.TableFingerprints);
        Assert.Contains(before, line => line.StartsWith("master_data_config.master_data_catalogue|21|", StringComparison.Ordinal));
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task EveryCanonicalRoleHasAPublishedShippedDefaultProfile()
    {
        IReadOnlyList<string> roles = await database.QueryAsync("""
            SELECT r.code
            FROM identity_access.role r
            JOIN identity_access.permission_profile p ON p.base_role_id = r.id AND p.is_shipped_default AND p.code = r.code || '-DEFAULT'
            JOIN identity_access.permission_profile_version v ON v.permission_profile_id = p.id AND v.version_no = 1 AND v.lifecycle_state = 'PUBLISHED'
            WHERE r.is_system
            ORDER BY r.code
            """);

        Assert.Equal(CanonicalRoles, roles);
    }

    /// <summary>TASK-030: the seed writes PermissionCatalogue, row for row, to the catalogue and the shipped-default versions 1.</summary>
    [Fact]
    public async Task ThePermissionCatalogueAndShippedGrantsAreTheCodes()
    {
        IReadOnlyList<string> permissions = await database.QueryAsync(
            "SELECT code || ' ' || permission_group FROM identity_access.permission ORDER BY code");
        IReadOnlyList<string> grants = await database.QueryAsync("""
            SELECT r.code || ' ' || p.code || ' ' || g.data_scope
            FROM identity_access.permission_profile_grant g
            JOIN identity_access.permission p ON p.id = g.permission_id
            JOIN identity_access.permission_profile_version v ON v.id = g.permission_profile_version_id AND v.version_no = 1
            JOIN identity_access.permission_profile pp ON pp.id = v.permission_profile_id AND pp.is_shipped_default
            JOIN identity_access.role r ON r.id = pp.base_role_id
            ORDER BY 1
            """);

        Assert.Equal(
            PermissionCatalogue.Platform.Definitions.Select(d => $"{d.Code} {d.Group}").Order(StringComparer.Ordinal),
            permissions);
        Assert.Equal(
            PermissionCatalogue.ShippedDefaultGrants.Select(g => $"{g.RoleCode} {g.PermissionCode} {ScopeValue(g.Scope)}").Order(StringComparer.Ordinal),
            grants);
    }

    /// <summary>ADR-012: controlled master data is bilingual. Each Arabic label holds Arabic script and each English label none.</summary>
    [Fact]
    public async Task EverySeededLabelIsBilingual()
    {
        const string labels = """
            SELECT 'role ' || code AS row_name, name_ar AS ar, name_en AS en FROM identity_access.role
            UNION ALL SELECT 'permission_profile ' || code, name_ar, name_en FROM identity_access.permission_profile
            UNION ALL SELECT 'permission ' || code, name_ar, name_en FROM identity_access.permission
            UNION ALL SELECT 'master_data_catalogue ' || code, name_ar, name_en FROM master_data_config.master_data_catalogue
            UNION ALL SELECT 'master_data_item ' || code, label_ar, label_en FROM master_data_config.master_data_item
            UNION ALL SELECT 'configuration_family ' || code, name_ar, name_en FROM master_data_config.configuration_family
            UNION ALL SELECT 'probability_level_definition ' || level, label_ar, label_en FROM master_data_config.probability_level_definition
            UNION ALL SELECT 'impact_level_definition ' || level, label_ar, label_en FROM master_data_config.impact_level_definition
            """;

        IReadOnlyList<string> count = await database.QueryAsync($"SELECT count(*)::text FROM ({labels}) l");
        IReadOnlyList<string> notBilingual = await database.QueryAsync($"""
            SELECT row_name FROM ({labels}) l
            WHERE ar !~ '[؀-ۿ]' OR en ~ '[؀-ۿ]' OR btrim(en) = ''
            """);

        Assert.Equal("87", count[0]);
        Assert.Empty(notBilingual);
    }

    [Fact]
    public async Task TheThreeGovernanceProfilesArePublished()
    {
        IReadOnlyList<string> profiles = await database.QueryAsync("""
            SELECT i.code FROM master_data_config.master_data_item i
            JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
            WHERE c.code = 'GOVERNANCE_PROFILE' AND i.lifecycle_state = 'PUBLISHED' AND i.is_system
            ORDER BY i.sort_order
            """);

        Assert.Equal(["LIGHT", "STANDARD", "FULL"], profiles);
    }

    /// <summary>
    /// ADR-011 with OQ-006 outstanding: five probability levels and five levels for each of the four dimensions, with
    /// no description or boundary, in a version that stays DRAFT. Nothing is seeded whose value AHDA has not given:
    /// no rating label, no matrix cell, no materiality band (OQ-013), no governance profile setting (OQ-014).
    /// </summary>
    [Fact]
    public async Task TheRiskAndIssueScaleIsGenericAndUnpublished()
    {
        IReadOnlyList<string> facts = await database.QueryAsync("""
            SELECT 'version ' || v.version_no || ' ' || v.lifecycle_state
            FROM master_data_config.configuration_version v
            JOIN master_data_config.configuration_family f ON f.id = v.configuration_family_id
            WHERE f.code = 'RISK_MATRIX'
            UNION ALL
            SELECT 'probability levels ' || string_agg(level::text, ',' ORDER BY level)
                   || ' with values ' || count(*) FILTER (WHERE lower_pct IS NOT NULL OR upper_pct IS NOT NULL)
            FROM master_data_config.probability_level_definition
            UNION ALL
            SELECT 'impact ' || d.code || ' ' || string_agg(l.level::text, ',' ORDER BY l.level)
                   || ' with values ' || count(*) FILTER (WHERE l.lower_bound IS NOT NULL OR l.upper_bound IS NOT NULL OR l.description_en IS NOT NULL)
            FROM master_data_config.impact_level_definition l
            JOIN master_data_config.master_data_item d ON d.id = l.impact_dimension_item_id
            GROUP BY d.code, d.sort_order
            UNION ALL
            SELECT 'unseeded ' || (SELECT count(*) FROM master_data_config.risk_rating_definition)
                   || ',' || (SELECT count(*) FROM master_data_config.risk_matrix_cell)
                   || ',' || (SELECT count(*) FROM master_data_config.materiality_band)
                   || ',' || (SELECT count(*) FROM master_data_config.governance_profile_setting)
            """);

        Assert.Equal(
            [
                "impact COST 1,2,3,4,5 with values 0",
                "impact OPERATIONAL 1,2,3,4,5 with values 0",
                "impact REPUTATION 1,2,3,4,5 with values 0",
                "impact SCHEDULE 1,2,3,4,5 with values 0",
                "probability levels 1,2,3,4,5 with values 0",
                "unseeded 0,0,0,0",
                "version 1 DRAFT",
            ],
            facts.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The seed owns structure and puts it back; AHDA owns wording and keeps it. An administrator's label edits
    /// survive a re-seed, a drifted flag the platform depends on does not.
    /// </summary>
    [Fact]
    public async Task ReseedingKeepsAhdaWordingAndRestoresStructure()
    {
        IReadOnlyList<string> state = await database.QueryRolledBackAsync(
            """
            UPDATE identity_access.role SET name_en = 'Edited by AHDA', is_external_eligible = false WHERE code = 'R04';
            UPDATE master_data_config.master_data_item SET label_ar = 'معدّل', is_system = false WHERE code = 'LIGHT';
            """ + DatabaseScripts.Read("seed-master-data.sql"),
            """
            SELECT name_en || ' ' || is_external_eligible FROM identity_access.role WHERE code = 'R04'
            UNION ALL
            SELECT label_ar || ' ' || is_system FROM master_data_config.master_data_item WHERE code = 'LIGHT'
            """);

        Assert.Equal(["Edited by AHDA true", "معدّل true"], state);
    }

    /// <summary>
    /// Every master data reference in erd.dbml is mapped to a catalogue in validate-data-integrity.sql, and the seed
    /// creates every catalogue the map names, so no reference can point at a catalogue that does not exist.
    /// </summary>
    [Fact]
    public async Task EveryErdMasterDataReferenceHasASeededCatalogue()
    {
        string[] erdReferences = [.. ErdItemReferences(File.ReadAllText(RepositoryFile.Path("docs/architecture/erd.dbml")))];
        Dictionary<string, string> map = ValidatorMapLine().Matches(DatabaseScripts.Read("validate-data-integrity.sql"))
            .ToDictionary(m => $"{m.Groups["schema"].Value}.{m.Groups["table"].Value}.{m.Groups["column"].Value}", m => m.Groups["catalogue"].Value);
        IReadOnlyList<string> seeded = await database.QueryAsync("SELECT code FROM master_data_config.master_data_catalogue WHERE is_system");

        Assert.Equal(37, erdReferences.Length);
        Assert.Equal(erdReferences.Order(StringComparer.Ordinal), map.Keys.Order(StringComparer.Ordinal));
        Assert.Empty(map.Values.Distinct().Except(seeded));
        Assert.Empty(seeded.Except(map.Values));
    }

    private static IEnumerable<string> ErdItemReferences(string dbml)
    {
        string? table = null;
        foreach (string line in dbml.Split('\n'))
        {
            if (ErdTable().Match(line) is { Success: true } header)
            {
                table = header.Groups["table"].Value;
            }
            else if (ErdItemColumn().Match(line) is { Success: true } column && column.Groups["column"].Value != "parent_item_id")
            {
                yield return $"{table}.{column.Groups["column"].Value}";
            }
        }
    }

    [GeneratedRegex(@"^Table (?<table>\w+\.\w+) \{")]
    private static partial Regex ErdTable();

    [GeneratedRegex(@"^\s+(?<column>\w+_item_id) uuid .*ref: > master_data_config\.master_data_item\.id")]
    private static partial Regex ErdItemColumn();

    [GeneratedRegex(@"^\s+\('(?<schema>\w+)',\s+'(?<table>\w+)',\s+'(?<column>\w+)',\s+'(?<catalogue>[A-Z_]+)'\)", RegexOptions.Multiline)]
    private static partial Regex ValidatorMapLine();

    /// <summary>A data scope as <c>ck_permission_profile_grant_data_scope</c> stores it.</summary>
    private static string ScopeValue(DataScope scope) => scope == DataScope.ReadOnly ? "READ_ONLY" : scope.ToString().ToUpperInvariant();
}

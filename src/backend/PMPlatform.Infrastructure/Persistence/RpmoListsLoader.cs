using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using PMPlatform.Application.Common.DataImport;
using PMPlatform.Application.Features.Project.Import;
using PMPlatform.Infrastructure.DataImport;

namespace PMPlatform.Infrastructure.Persistence;

/// <summary>
/// <c>load-rpmo-lists &lt;workbook.xlsx&gt;</c> (the 5 Nov 2026 review §4.2): loads the lists of RPMO's import file, so its names match without mapping. From
/// the القوائم sheet: departments, sectors, portfolios, initiatives under their portfolio, priorities, areas and cities under their area. From the data
/// sheet: the entities it names, and for each existing user named as project manager, the R04 assignment over each department of their rows, which
/// the import requires of a manager. Items no project uses and the file does not list are retired, so the lists read as RPMO's. Run it again and it
/// changes nothing further. The file itself stays out of the repository: the public repository holds no client data.
/// </summary>
public static partial class RpmoListsLoader
{
    public const string Command = "load-rpmo-lists";

    private const string ListsSheet = "القوائم";

    /// <summary>Where RPMO's places are on the dashboard's map: approximate town centres, which AHDA may correct. The file holds none.</summary>
    private static readonly (string City, decimal Latitude, decimal Longitude)[] CityCoordinates =
    [
        ("الهفوف", 25.3647m, 49.5870m),
        ("المبرز", 25.4077m, 49.5903m),
        ("العيون", 25.6050m, 49.5700m),
        ("العمران", 25.4333m, 49.7167m),
        ("الجفر", 25.3750m, 49.7200m),
        ("الطرف", 25.3625m, 49.7236m),
        ("القارة", 25.4150m, 49.6700m),
        ("الجشة", 25.3920m, 49.7050m),
        ("المطيرفي", 25.4780m, 49.5580m),
        ("البطالية", 25.4330m, 49.6330m),
        ("العقير", 25.6430m, 50.2130m),
        ("سلوى", 24.7440m, 50.7630m),
        ("يبرين", 23.2600m, 48.9700m),
    ];
    private const string SeedActor = "00000000-0000-4000-8000-0000000000ff";
    private const int FirstRow = 4;
    private const int LastRow = 19;

    // The columns of the القوائم sheet.
    private const int DepartmentColumn = 0;
    private const int SectorColumn = 2;
    private const int PortfolioColumn = 3;
    private const int InitiativeColumn = 4;
    private const int InitiativePortfolioColumn = 5;
    private const int PriorityColumn = 9;
    private const int AreaColumn = 11;
    private const int CityColumn = 12;
    private const int CityAreaColumn = 13;

    public static bool IsCommand(string[] args) => args is [Command, ..];

    /// <summary>Runs the command and returns the process exit code.</summary>
    public static async Task<int> RunAsync(this IServiceProvider services, string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(args);
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RpmoListsLoader));
        if (args is not [_, string path])
        {
            LogUsage(logger, Command);
            return 1;
        }

        byte[] file = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        ImportSheet lists = ReadSheet(file, ListsSheet);
        ImportSheet data = ReadSheet(file, ProjectImportLayout.DataSheet);
        Dictionary<string, int> keys = KeyColumns(data);

        PMPlatformDbContext context = scope.ServiceProvider.GetRequiredService<PMPlatformDbContext>();
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        Batch batch = new(connection, (NpgsqlTransaction)transaction, cancellationToken);
        await batch.ExecuteAsync("SET LOCAL session_replication_role = replica").ConfigureAwait(false);

        await LoadItemsAsync(batch, "PROJECT_CLASSIFICATION", "SEC", Column(lists, SectorColumn)).ConfigureAwait(false);
        await LoadCodedItemsAsync(batch, "STRATEGIC_PORTFOLIO", Column(lists, PortfolioColumn).Select(p => (p, (string?)null)), null).ConfigureAwait(false);
        await LoadCodedItemsAsync(batch, "STRATEGIC_INITIATIVE", Pairs(lists, InitiativeColumn, InitiativePortfolioColumn), "STRATEGIC_PORTFOLIO").ConfigureAwait(false);
        await LoadItemsAsync(batch, "PRIORITY", "PRI", Column(lists, PriorityColumn)).ConfigureAwait(false);
        await LoadItemsAsync(batch, "REGION", "AREA", Column(lists, AreaColumn)).ConfigureAwait(false);
        await LoadItemsAsync(batch, "CITY", "CITY", Column(lists, CityColumn), Pairs(lists, CityColumn, CityAreaColumn).ToDictionary(p => p.Item1, p => p.Item2!, StringComparer.Ordinal), "REGION").ConfigureAwait(false);

        foreach ((string city, decimal latitude, decimal longitude) in CityCoordinates)
        {
            await batch.ExecuteAsync(
                """
                UPDATE master_data_config.master_data_item i SET latitude = @latitude, longitude = @longitude
                FROM master_data_config.master_data_catalogue c
                WHERE c.code = 'CITY' AND i.catalogue_id = c.id AND i.label_ar = @city
                """,
                ("city", city), ("latitude", latitude), ("longitude", longitude)).ConfigureAwait(false);
        }

        // RPMO's words for the seeded governance profiles, so a project shows the level as its row states it.
        await batch.ExecuteAsync("""
            UPDATE master_data_config.master_data_item i SET label_ar = v.label
            FROM (VALUES ('FULL', 'كامل'), ('LIGHT', 'مبسط')) AS v (code, label), master_data_config.master_data_catalogue c
            WHERE c.code = 'GOVERNANCE_PROFILE' AND i.catalogue_id = c.id AND i.code = v.code
            """).ConfigureAwait(false);

        foreach ((string department, int index) in Column(lists, DepartmentColumn).Select((d, i) => (d, i)))
        {
            await batch.ExecuteAsync(
                """
                INSERT INTO identity_access.department (id, code, name_ar, name_en, created_at, created_by, updated_at, updated_by)
                SELECT gen_random_uuid(), @code, @name, @name, now(), @actor, now(), @actor
                WHERE NOT EXISTS (SELECT 1 FROM identity_access.department d WHERE d.name_ar = @name)
                """,
                ("code", $"RPMO-DEPT-{index + 1}"), ("name", department), ("actor", Guid.Parse(SeedActor))).ConfigureAwait(false);
        }

        List<(string Entity, string Type)> entities = [.. Rows(data, keys)
            .Select(r => (Entity: Cell(r, keys, ProjectImportLayout.Entity), Type: Cell(r, keys, "entity_type")))
            .Where(e => e.Entity is not null)
            .GroupBy(e => e.Entity!, StringComparer.Ordinal)
            .Select(g => (g.Key, g.First().Type ?? "جهة حكومية"))
            .OrderBy(e => e.Key, StringComparer.Ordinal)];
        await LoadItemsAsync(batch, "EXTERNAL_ENTITY_TYPE", "RPMO-TYPE", [.. entities.Select(e => e.Type).Distinct(StringComparer.Ordinal)], retireOthers: false).ConfigureAwait(false);
        foreach (((string entity, string type), int index) in entities.Select((e, i) => (e, i)))
        {
            await batch.ExecuteAsync(
                """
                INSERT INTO identity_access.external_entity (id, code, name_ar, name_en, entity_type_item_id, status, created_at, created_by, updated_at, updated_by)
                SELECT gen_random_uuid(), @code, @name, @name, i.id, 'ACTIVE', now(), @actor, now(), @actor
                FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
                WHERE c.code = 'EXTERNAL_ENTITY_TYPE' AND i.label_ar = @type
                AND NOT EXISTS (SELECT 1 FROM identity_access.external_entity e WHERE e.name_ar = @name)
                """,
                ("code", $"RPMO-ENT-{index + 1:D2}"), ("name", entity), ("type", type), ("actor", Guid.Parse(SeedActor))).ConfigureAwait(false);
        }

        int assignments = 0;
        foreach (IGrouping<string, ImportSheetRow> manager in Rows(data, keys).Where(r => Cell(r, keys, ProjectImportLayout.ProjectManager) is not null).GroupBy(r => Cell(r, keys, ProjectImportLayout.ProjectManager)!, StringComparer.Ordinal))
        {
            foreach (string department in manager.Select(r => Cell(r, keys, ProjectImportLayout.Department)).OfType<string>().Distinct(StringComparer.Ordinal))
            {
                assignments += await batch.ExecuteAsync(
                    """
                    INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, department_id, starts_at, status, created_at, created_by, updated_at, updated_by)
                    SELECT gen_random_uuid(), u.id, v.id, d.id, now(), 'ACTIVE', now(), @actor, now(), @actor
                    FROM identity_access."user" u, identity_access.department d, identity_access.permission_profile p
                    JOIN identity_access.permission_profile_version v ON v.permission_profile_id = p.id AND v.lifecycle_state = 'PUBLISHED'
                    WHERE u.username = @username AND d.name_ar = @department AND p.code = 'R04-DEFAULT'
                    AND NOT EXISTS (SELECT 1 FROM identity_access.access_relationship a WHERE a.user_id = u.id AND a.department_id = d.id AND a.permission_profile_version_id = v.id AND a.status = 'ACTIVE')
                    """,
                    ("username", manager.Key), ("department", department), ("actor", Guid.Parse(SeedActor))).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        int departments = Column(lists, DepartmentColumn).Count;
        int sectors = Column(lists, SectorColumn).Count;
        int portfolios = Column(lists, PortfolioColumn).Count;
        int initiatives = Column(lists, InitiativeColumn).Count;
        LogLoaded(logger, departments, sectors, portfolios, initiatives, entities.Count, assignments);
        return 0;
    }

    private static ImportSheet ReadSheet(byte[] file, string name)
    {
        using MemoryStream stream = new(file, writable: false);
        return XlsxImportWorkbook.Read(stream, ProjectImportLayout.MaxRows + 10, name) ?? throw new InvalidOperationException($"The sheet {name} cannot be read.");
    }

    private static Dictionary<string, int> KeyColumns(ImportSheet data)
    {
        ImportSheetRow row = data.Rows.FirstOrDefault(r => r.Cells.Values.Contains(ProjectImportLayout.LegacyRef, StringComparer.Ordinal))
            ?? throw new InvalidOperationException("The data sheet has no row of field keys.");
        return row.Cells.GroupBy(c => c.Value.Trim(), StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Key, StringComparer.Ordinal);
    }

    private static IEnumerable<ImportSheetRow> Rows(ImportSheet data, Dictionary<string, int> keys) =>
        data.Rows.SkipWhile(r => Cell(r, keys, ProjectImportLayout.LegacyRef) != ProjectImportLayout.LegacyRef).Skip(1)
            .Where(r => Cell(r, keys, ProjectImportLayout.LegacyRef) is not null);

    private static string? Cell(ImportSheetRow row, Dictionary<string, int> keys, string key) =>
        keys.TryGetValue(key, out int column) && row.Cells.TryGetValue(column, out string? value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static List<string> Column(ImportSheet lists, int column) =>
        [.. lists.Rows.Where(r => r.RowNumber is >= FirstRow and <= LastRow && r.Cells.ContainsKey(column) && !string.IsNullOrWhiteSpace(r.Cells[column])).Select(r => r.Cells[column].Trim())];

    private static IEnumerable<(string, string?)> Pairs(ImportSheet lists, int column, int parentColumn) =>
        lists.Rows.Where(r => r.RowNumber is >= FirstRow and <= LastRow && r.Cells.ContainsKey(column) && !string.IsNullOrWhiteSpace(r.Cells[column]))
            .Select(r => (r.Cells[column].Trim(), r.Cells.GetValueOrDefault(parentColumn)?.Trim()));

    private static Task LoadItemsAsync(Batch batch, string catalogue, string prefix, IReadOnlyList<string> labels, IReadOnlyDictionary<string, string>? parents = null, string? parentCatalogue = null, bool retireOthers = true) =>
        LoadAsync(batch, catalogue, [.. labels.Select((label, i) => ($"{prefix}-{i + 1:D2}", label, parents?.GetValueOrDefault(label)))], parentCatalogue, retireOthers);

    /// <summary>A portfolio or initiative as RPMO writes it, "PF-02 — وجهة ثقافية": its code and its label.</summary>
    private static Task LoadCodedItemsAsync(Batch batch, string catalogue, IEnumerable<(string, string?)> items, string? parentCatalogue) =>
        LoadAsync(
            batch,
            catalogue,
            [.. items.Select(i => (SplitCoded(i.Item1), i.Item2)).Select(i => (i.Item1.Code, i.Item1.Label, i.Item2 is null ? null : SplitCoded(i.Item2).Label))],
            parentCatalogue,
            retireOthers: true);

    private static (string Code, string Label) SplitCoded(string text)
    {
        string[] parts = text.Split(" — ", 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (text, text);
    }

    private static async Task LoadAsync(Batch batch, string catalogue, IReadOnlyList<(string Code, string Label, string? Parent)> items, string? parentCatalogue, bool retireOthers)
    {
        int order = 0;
        foreach ((string code, string label, string? parent) in items)
        {
            order++;
            await batch.ExecuteAsync(
                """
                INSERT INTO master_data_config.master_data_item AS i (id, catalogue_id, code, label_ar, label_en, parent_item_id, sort_order, lifecycle_state, published_at, created_at, created_by, updated_at, updated_by)
                SELECT gen_random_uuid(), c.id, @code, @label, @label,
                       (SELECT p.id FROM master_data_config.master_data_item p JOIN master_data_config.master_data_catalogue pc ON pc.id = p.catalogue_id
                        WHERE pc.code = @parentCatalogue AND p.label_ar = @parent AND p.lifecycle_state = 'PUBLISHED' LIMIT 1),
                       @order, 'PUBLISHED', now(), now(), @actor, now(), @actor
                FROM master_data_config.master_data_catalogue c
                WHERE c.code = @catalogue AND NOT EXISTS (
                    SELECT 1 FROM master_data_config.master_data_item e WHERE e.catalogue_id = c.id AND e.label_ar = @label AND e.lifecycle_state = 'PUBLISHED')
                ON CONFLICT (catalogue_id, code) DO UPDATE SET label_ar = EXCLUDED.label_ar, label_en = EXCLUDED.label_en, parent_item_id = EXCLUDED.parent_item_id,
                    lifecycle_state = 'PUBLISHED', updated_at = now()
                """,
                ("code", code), ("label", label), ("order", order), ("catalogue", catalogue), ("actor", Guid.Parse(SeedActor)),
                ("parent", (object?)parent ?? DBNull.Value), ("parentCatalogue", (object?)parentCatalogue ?? DBNull.Value)).ConfigureAwait(false);
        }

        if (retireOthers)
        {
            await batch.ExecuteAsync(
                """
                UPDATE master_data_config.master_data_item i SET lifecycle_state = 'RETIRED', updated_at = now()
                FROM master_data_config.master_data_catalogue c
                WHERE i.catalogue_id = c.id AND c.code = @catalogue AND i.lifecycle_state = 'PUBLISHED' AND NOT i.is_system
                AND NOT (i.label_ar = ANY(@labels))
                AND NOT EXISTS (SELECT 1 FROM project.project p WHERE i.id IN (p.classification_item_id, p.strategic_portfolio_item_id, p.strategic_initiative_item_id, p.priority_item_id, p.region_item_id, p.city_item_id))
                """,
                ("catalogue", catalogue), ("labels", items.Select(i => i.Label).ToArray())).ConfigureAwait(false);
        }
    }

    /// <summary>One transaction's statements, with their parameters.</summary>
    private sealed class Batch(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        public async Task<int> ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
        {
            await using NpgsqlCommand command = new(sql, connection, transaction);
            foreach ((string name, object value) in parameters)
            {
                // A parameter that may be null names its type, since a DBNull alone gives Postgres none to infer.
                command.Parameters.Add(value is DBNull
                    ? new NpgsqlParameter(name, NpgsqlTypes.NpgsqlDbType.Text) { Value = value }
                    : new NpgsqlParameter(name, value));
            }

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Usage: {Command} <workbook.xlsx>")]
    private static partial void LogUsage(ILogger logger, string command);

    [LoggerMessage(Level = LogLevel.Information, Message = "RPMO lists loaded: {Departments} departments, {Sectors} sectors, {Portfolios} portfolios, {Initiatives} initiatives, {Entities} entities; {Assignments} R04 assignments added")]
    private static partial void LogLoaded(ILogger logger, int departments, int sectors, int portfolios, int initiatives, int entities, int assignments);
}

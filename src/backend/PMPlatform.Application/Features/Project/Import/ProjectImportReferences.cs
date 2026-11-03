using System.Globalization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Import;

/// <summary>
/// The lists a workbook's names are matched against (R3-4), each read once per validation: active entities and departments, the PUBLISHED
/// items of the master data catalogues, active users, and the lifecycle states an import may set. A name resolves, in this order, through the
/// importer's own mapping, a mapping remembered from an earlier batch, then the list itself — by code, or by either label as
/// <see cref="ImportNames"/> compares them.
/// </summary>
internal sealed class ProjectImportReferences
{
    /// <summary>RPMO's status words, and the platform's own, as the lifecycle state an imported project enters (review §4, Status mapping).</summary>
    private static readonly Dictionary<string, ProjectLifecycleState> Statuses = new(StringComparer.Ordinal)
    {
        [ImportNames.Key("نشط")] = ProjectLifecycleState.Active,
        [ImportNames.Key("قيد التنفيذ")] = ProjectLifecycleState.Active,
        [ImportNames.Key("معتمد – تخطيط")] = ProjectLifecycleState.ApprovedPlanned,
        [ImportNames.Key("معتمد - تخطيط")] = ProjectLifecycleState.ApprovedPlanned,
        [ImportNames.Key("مستقبلي")] = ProjectLifecycleState.ApprovedPlanned,
        [ImportNames.Key("موقوف")] = ProjectLifecycleState.Suspended,
        [ImportNames.Key("متوقف")] = ProjectLifecycleState.Suspended,
        [ImportNames.Key("مكتمل")] = ProjectLifecycleState.Completed,
        [ImportNames.Key("منجز")] = ProjectLifecycleState.Completed,
        // The platform has no Cancelled state: a cancelled project is closed, terminal and read-only.
        [ImportNames.Key("ملغي")] = ProjectLifecycleState.Closed,
        [ImportNames.Key("مغلق")] = ProjectLifecycleState.Closed,
        ["active"] = ProjectLifecycleState.Active,
        ["approved_planned"] = ProjectLifecycleState.ApprovedPlanned,
        ["suspended"] = ProjectLifecycleState.Suspended,
        ["completed"] = ProjectLifecycleState.Completed,
        ["closed"] = ProjectLifecycleState.Closed,
    };

    /// <summary>The states an import may set: a project already registered and approved.</summary>
    public static IReadOnlyList<ProjectLifecycleState> ImportableStates { get; } =
    [
        ProjectLifecycleState.ApprovedPlanned, ProjectLifecycleState.Active, ProjectLifecycleState.Suspended, ProjectLifecycleState.Completed,
        ProjectLifecycleState.Closed,
    ];

    /// <summary>RPMO's governance levels, as the GOVERNANCE_PROFILE codes they mean (ADR-015's three profiles).</summary>
    private static readonly Dictionary<string, string> GovernanceSynonyms = new(StringComparer.Ordinal)
    {
        [ImportNames.Key("كامل")] = "FULL",
        [ImportNames.Key("شامل")] = "FULL",
        [ImportNames.Key("قياسي")] = "STANDARD",
        [ImportNames.Key("مبسط")] = "LIGHT",
    };

    private readonly Dictionary<ProjectImportDimension, IReadOnlyList<Candidate>> _lists;
    private readonly Dictionary<(ProjectImportDimension, string), string> _remembered;
    private readonly Dictionary<(ProjectImportDimension, string), string> _requested;

    private ProjectImportReferences(
        Dictionary<ProjectImportDimension, IReadOnlyList<Candidate>> lists,
        Dictionary<(ProjectImportDimension, string), string> remembered,
        Dictionary<(ProjectImportDimension, string), string> requested,
        IReadOnlyList<UserIdentity> users,
        IReadOnlyDictionary<Guid, MasterDataItemReference> items)
    {
        _lists = lists;
        _remembered = remembered;
        _requested = requested;
        Users = users;
        Items = items;
    }

    public IReadOnlyList<UserIdentity> Users { get; }

    /// <summary>Every PUBLISHED item read, by id: an initiative's portfolio and a city's region are read from it.</summary>
    public IReadOnlyDictionary<Guid, MasterDataItemReference> Items { get; }

    public static async Task<ProjectImportReferences> LoadAsync(
        IMasterDataResolver masterData,
        IOrganizationDirectory organizations,
        IUserLookup userLookup,
        IReadOnlyList<ProjectImportNameMapping> remembered,
        ProjectImportPlan plan,
        CancellationToken cancellationToken)
    {
        Dictionary<ProjectImportDimension, IReadOnlyList<Candidate>> lists = [];
        Dictionary<Guid, MasterDataItemReference> items = [];
        foreach ((ProjectImportDimension dimension, string catalogue) in new[]
        {
            (ProjectImportDimension.Sector, MasterDataCatalogueCodes.ProjectClassification),
            (ProjectImportDimension.Portfolio, MasterDataCatalogueCodes.StrategicPortfolio),
            (ProjectImportDimension.Initiative, MasterDataCatalogueCodes.StrategicInitiative),
            (ProjectImportDimension.Region, MasterDataCatalogueCodes.Region),
            (ProjectImportDimension.City, MasterDataCatalogueCodes.City),
            (ProjectImportDimension.Priority, MasterDataCatalogueCodes.Priority),
            (ProjectImportDimension.Governance, MasterDataCatalogueCodes.GovernanceProfile),
        })
        {
            IReadOnlyList<MasterDataItemReference> published = await PublishedAsync(masterData, catalogue, cancellationToken).ConfigureAwait(false);
            foreach (MasterDataItemReference item in published)
            {
                items[item.Id] = item;
            }

            lists[dimension] = [.. published.Select(i => new Candidate(i.Id.ToString("D"), i.Code, i.Label))];
        }

        lists[ProjectImportDimension.Department] = await OrganizationAsync(
            await organizations.ListActiveDepartmentCodesAsync(cancellationToken).ConfigureAwait(false), organizations.FindDepartmentIdAsync, cancellationToken)
            .ConfigureAwait(false);
        lists[ProjectImportDimension.Entity] = await OrganizationAsync(
            await organizations.ListActiveExternalEntityCodesAsync(cancellationToken).ConfigureAwait(false), organizations.FindExternalEntityIdAsync, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<UserIdentity> users = await userLookup.ListActiveAsync(cancellationToken).ConfigureAwait(false);
        lists[ProjectImportDimension.ProjectManager] = [.. users.Select(u => new Candidate(u.Id.ToString("D"), u.Username, new BilingualLabel(u.DisplayName, u.DisplayName), u.Email))];
        lists[ProjectImportDimension.FollowUpOwner] = [.. users.Where(u => u.UserType == UserType.Internal)
            .Select(u => new Candidate(u.Id.ToString("D"), u.Username, new BilingualLabel(u.DisplayName, u.DisplayName), u.Email))];
        lists[ProjectImportDimension.Status] = [.. ImportableStates.Select(s => new Candidate(StateCode(s), StateCode(s), StateLabel(s)))];

        Dictionary<(ProjectImportDimension, string), string> rememberedMappings = [];
        foreach (ProjectImportNameMapping mapping in remembered)
        {
            if (Enum.TryParse(mapping.Dimension, ignoreCase: true, out ProjectImportDimension dimension))
            {
                rememberedMappings[(dimension, mapping.SourceKey)] = mapping.TargetValue;
            }
        }

        Dictionary<(ProjectImportDimension, string), string> requested = [];
        foreach (ProjectImportMapping mapping in plan.Mappings)
        {
            requested[(mapping.Dimension, ImportNames.Key(mapping.SourceText))] = mapping.TargetValue;
        }

        return new ProjectImportReferences(lists, rememberedMappings, requested, users, items);
    }

    /// <summary>The platform value <paramref name="text"/> means, or null when it matches nothing.</summary>
    public string? Resolve(ProjectImportDimension dimension, string text)
    {
        string key = ImportNames.Key(text);
        IReadOnlyList<Candidate> list = _lists[dimension];
        if (_requested.TryGetValue((dimension, key), out string? requested) && list.Any(c => c.Value == requested))
        {
            return requested;
        }

        if (_remembered.TryGetValue((dimension, key), out string? remembered) && list.Any(c => c.Value == remembered))
        {
            return remembered;
        }

        if (dimension == ProjectImportDimension.Status)
        {
            return Statuses.TryGetValue(key, out ProjectLifecycleState state) ? StateCode(state) : null;
        }

        if (dimension == ProjectImportDimension.Governance && GovernanceSynonyms.TryGetValue(key, out string? code)
            && list.FirstOrDefault(c => c.Code == code) is { } profile)
        {
            return profile.Value;
        }

        (string? prefix, string label) = ImportNames.Split(text);
        string labelKey = ImportNames.Key(label);
        return (list.FirstOrDefault(c => string.Equals(c.Code, text.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? (prefix is null ? null : list.FirstOrDefault(c => string.Equals(c.Code, prefix, StringComparison.OrdinalIgnoreCase)))
                ?? list.FirstOrDefault(c => ImportNames.Key(c.Label.Ar) == key || ImportNames.Key(c.Label.En) == key)
                ?? list.FirstOrDefault(c => ImportNames.Key(c.Label.Ar) == labelKey || ImportNames.Key(c.Label.En) == labelKey)
                ?? list.FirstOrDefault(c => c.Email is not null && string.Equals(c.Email, text.Trim(), StringComparison.OrdinalIgnoreCase)))?.Value;
    }

    /// <summary>What a name of <paramref name="dimension"/> may be mapped to; at most 300, so a list of people stays a list.</summary>
    public IReadOnlyList<ProjectImportOption> Options(ProjectImportDimension dimension) =>
        [.. _lists[dimension].Take(300).Select(c => new ProjectImportOption(c.Value, c.Label))];

    /// <summary>The label of a resolved value, for a field's before-and-after.</summary>
    public string? LabelOf(ProjectImportDimension dimension, string? value) =>
        value is null ? null : _lists[dimension].FirstOrDefault(c => c.Value == value)?.Label.Ar ?? value;

    public static string StateCode(ProjectLifecycleState state) =>
        System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(state.ToString());

    public static ProjectLifecycleState? StateOf(string code) =>
        ImportableStates.Cast<ProjectLifecycleState?>().FirstOrDefault(s => StateCode(s!.Value) == code);

    private static BilingualLabel StateLabel(ProjectLifecycleState state) => state switch
    {
        ProjectLifecycleState.ApprovedPlanned => new BilingualLabel("معتمد – تخطيط", "Approved – planning"),
        ProjectLifecycleState.Active => new BilingualLabel("نشط", "Active"),
        ProjectLifecycleState.Suspended => new BilingualLabel("موقوف", "Suspended"),
        ProjectLifecycleState.Completed => new BilingualLabel("مكتمل", "Completed"),
        ProjectLifecycleState.Closed => new BilingualLabel("مغلق (ملغي)", "Closed (cancelled)"),
        ProjectLifecycleState.Draft or ProjectLifecycleState.Submitted or ProjectLifecycleState.UnderReview or ProjectLifecycleState.Returned
            => throw new ArgumentOutOfRangeException(nameof(state), state, "An import does not set a registration state."),
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown lifecycle state."),
    };

    private static async Task<IReadOnlyList<MasterDataItemReference>> PublishedAsync(IMasterDataResolver masterData, string catalogue, CancellationToken cancellationToken)
    {
        try
        {
            return await masterData.ListPublishedItemsAsync(catalogue, cancellationToken).ConfigureAwait(false);
        }
        catch (ConfigurationMissingException)
        {
            return [];
        }
    }

    private static async Task<IReadOnlyList<Candidate>> OrganizationAsync(
        IReadOnlyList<OrganizationCode> codes, Func<string, CancellationToken, Task<Guid?>> find, CancellationToken cancellationToken)
    {
        List<Candidate> candidates = [];
        foreach (OrganizationCode code in codes)
        {
            if (await find(code.Code, cancellationToken).ConfigureAwait(false) is { } id)
            {
                candidates.Add(new Candidate(id.ToString("D"), code.Code, code.Name));
            }
        }

        return candidates;
    }

    /// <summary>A value a name may resolve to: its stored value, its code, its labels and, for a person, their e-mail.</summary>
    private sealed record Candidate(string Value, string Code, BilingualLabel Label, string? Email = null);

    /// <summary>For logging and keys: a dimension as the API writes it.</summary>
    public static string DimensionCode(ProjectImportDimension dimension) =>
        System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(dimension.ToString()).ToString(CultureInfo.InvariantCulture);
}

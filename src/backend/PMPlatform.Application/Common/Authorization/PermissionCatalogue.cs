using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Common.Authorization;

/// <summary>
/// The protected permission catalogue (ERD F-081) and the grants of the R01–R08 shipped-default profiles. db/seed
/// writes both to <c>identity_access</c>, and a seed test fails if the two disagree.
/// </summary>
/// <remarks>
/// Blueprint Appendix A, the full matrix, is not in the repository (controlled-source-baseline.md §5.1, seed record
/// F-1). Only rows a controlled source fixes for all eight roles are here; the rest are added from Appendix A.
/// </remarks>
public sealed class PermissionCatalogue
{
    /// <summary>ADM-041 SSO/Directory Integration: R01 only (TASK-028).</summary>
    public const string IdentityIntegrationManage = "IDENTITY_INTEGRATION_MANAGE";

    /// <summary>ADR-019: personalise one's own dashboard layout (TASK-111).</summary>
    public const string LayoutPersonalize = "LAYOUT_PERSONALIZE";

    /// <summary>ADR-019: compose a report in the controlled report explorer (TASK-112).</summary>
    public const string ReportCompose = "REPORT_COMPOSE";

    public static PermissionCatalogue Platform { get; } = new(
        [
            new(IdentityIntegrationManage, "IDENTITY_ACCESS", AccessMode.Write),
            new(LayoutPersonalize, "DASHBOARDS", AccessMode.Write),
            new(ReportCompose, "REPORTS", AccessMode.Write),
        ]);

    /// <summary>
    /// ADR-019 grants Personalize Layout and Compose Report to R02, R03 and R07 and withholds them from R04, R05, R06 and
    /// R08; each acts on the holder's own layout or report definition, hence OWN (the ADR names no scope). The report's
    /// data is authorised independently for every viewer (CTL-15).
    /// </summary>
    public static IReadOnlyList<ShippedGrant> ShippedDefaultGrants { get; } =
    [
        new("R01", IdentityIntegrationManage, DataScope.All),
        new("R02", LayoutPersonalize, DataScope.Own),
        new("R02", ReportCompose, DataScope.Own),
        new("R03", LayoutPersonalize, DataScope.Own),
        new("R03", ReportCompose, DataScope.Own),
        new("R07", LayoutPersonalize, DataScope.Own),
        new("R07", ReportCompose, DataScope.Own),
    ];

    private readonly Dictionary<string, PermissionDefinition> _definitions;

    public PermissionCatalogue(IEnumerable<PermissionDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(d => d.Code, StringComparer.Ordinal);
    }

    public IEnumerable<PermissionDefinition> Definitions => _definitions.Values;

    public bool Contains(string code) => _definitions.ContainsKey(code);

    /// <summary>The definition of <paramref name="code"/>. A code outside the catalogue is a programming error, not a denial.</summary>
    public PermissionDefinition Get(string code) =>
        _definitions.TryGetValue(code, out PermissionDefinition? definition)
            ? definition
            : throw new ArgumentException($"'{code}' is not in the permission catalogue.", nameof(code));
}

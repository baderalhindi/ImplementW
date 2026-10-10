using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Domain.Dashboards;

namespace PMPlatform.Application.Features.Dashboards;

/// <summary>
/// The three delivered dashboards (ADR-006) and what is fixed about each by decision rather than configuration: the context it is read
/// in, whether it may be personalised (ADR-019), and who it may be offered to (ADR-019: entity users get the Project Dashboard only).
/// </summary>
internal static class DashboardCatalogue
{
    /// <summary>R08 External Entity User.</summary>
    public const string ExternalEntityRole = "R08";

    public static IReadOnlyList<DashboardCode> Codes { get; } = Enum.GetValues<DashboardCode>();

    /// <summary>The Project Dashboard is read for one project (DSH-009 in SCR-040, DSH-008); the others over a population.</summary>
    public static DashboardContextKind ContextOf(DashboardCode code) =>
        code == DashboardCode.Project ? DashboardContextKind.Project : DashboardContextKind.Portfolio;

    public static bool MayPersonalize(DashboardCode code) => code == DashboardCode.Portfolio;

    public static bool MayBeOfferedTo(DashboardCode code, string roleCode) =>
        code == DashboardCode.Project || !string.Equals(roleCode, ExternalEntityRole, StringComparison.Ordinal);

    /// <summary>ADR-019: an external entity's person — R08, or ADR-013's entity Project Manager in R04 — opens the Project Dashboard and nothing further.</summary>
    public static bool MayOpen(DashboardCode code, bool isExternal) => code == DashboardCode.Project || !isExternal;
}

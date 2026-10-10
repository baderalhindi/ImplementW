using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports;

/// <summary>
/// The ten delivered reports (ADR-006) and what is fixed about them by decision rather than configuration: the FG-02 catalogue entries each
/// absorbs (ADR-006 MAPPED: twenty-six entries, ten definitions), the entity report set (ADR-013), who a report may be offered to, which must be
/// run for one project, and the export baseline (ADR-005).
/// </summary>
internal static class ReportCatalogue
{
    /// <summary>R01 System Administrator: administration grants no business report (BR-RPT-011, DC-RPT-18).</summary>
    public const string AdministratorRole = "R01";

    /// <summary>R08 External Entity User.</summary>
    public const string ExternalEntityRole = "R08";

    public static IReadOnlyList<ReportCode> Codes { get; } = Enum.GetValues<ReportCode>();

    /// <summary>ADR-005: PDF, XLSX and CSV, for every report; a format never makes another report.</summary>
    public static IReadOnlyList<ReportExportFormat> ExportFormats { get; } = Enum.GetValues<ReportExportFormat>();

    /// <summary>
    /// The FG-02 §5.1 catalogue entries each report absorbs, as its parameters, options and audiences (ADR-006 MAPPED). Each of the twenty-three
    /// delivered entries is absorbed exactly once.
    /// </summary>
    public static IReadOnlyDictionary<ReportCode, IReadOnlyList<string>> CatalogueEntries { get; } = new Dictionary<ReportCode, IReadOnlyList<string>>
    {
        [ReportCode.PortfolioSummary] = ["RPT-PRJ-001", "RPT-EXE-001", "RPT-PRJ-004"],
        [ReportCode.ProjectRegister] = ["RPT-PRJ-002", "RPT-PRJ-003", "RPT-SUS-001", "RPT-CLO-001"],
        [ReportCode.ProjectReport] = ["RPT-PRJ-005"],
        [ReportCode.ProgressReporting] = ["RPT-PRG-001"],
        [ReportCode.ProgressHistory] = ["RPT-PRG-002"],
        [ReportCode.ScheduleDelivery] = ["RPT-SCH-001", "RPT-TSK-001", "RPT-MIL-001"],
        [ReportCode.RiskIssue] = ["RPT-RSK-001", "RPT-RSK-002", "RPT-ISS-001"],
        [ReportCode.FinancialPerformance] = ["RPT-FIN-001"],
        [ReportCode.KpiPerformance] = ["RPT-KPI-001", "RPT-CMP-001"],
        [ReportCode.GovernanceChange] = ["RPT-CHG-001", "RPT-APR-001", "RPT-CLO-002", "RPT-EXT-001"],
    };

    /// <summary>
    /// The entries no report absorbs (the participation amendment): RPT-DOC-001 is served by WF-12's document register screen; RPT-EXC-001 and
    /// RPT-AUD-001 remain Conditional.
    /// </summary>
    public static IReadOnlyList<string> NotDelivered { get; } = ["RPT-DOC-001", "RPT-EXC-001", "RPT-AUD-001"];

    /// <summary>ADR-013's amendment to TASK-071: the defined report set an entity may run and export on its own projects.</summary>
    public static IReadOnlySet<ReportCode> EntityReports { get; } = new HashSet<ReportCode>
    {
        ReportCode.ProjectRegister,
        ReportCode.ProjectReport,
        ReportCode.ProgressReporting,
        ReportCode.ProgressHistory,
        ReportCode.FinancialPerformance,
        ReportCode.KpiPerformance,
    };

    /// <summary>A report about one project: a formal Project report, a project's published history. Its PROJECT parameter is required.</summary>
    public static bool RequiresProject(ReportCode code) => code is ReportCode.ProjectReport or ReportCode.ProgressHistory;

    /// <summary>R01 is offered no report; R08 only the entity report set.</summary>
    public static bool MayBeOfferedTo(ReportCode code, string roleCode) =>
        !string.Equals(roleCode, AdministratorRole, StringComparison.Ordinal)
        && (EntityReports.Contains(code) || !string.Equals(roleCode, ExternalEntityRole, StringComparison.Ordinal));

    /// <summary>
    /// ADR-013: an external entity's person — R08, or the entity Project Manager in R04 — runs the entity report set and nothing further, whatever
    /// an audience lists.
    /// </summary>
    public static bool MayRun(ReportCode code, bool isExternal) => !isExternal || EntityReports.Contains(code);
}

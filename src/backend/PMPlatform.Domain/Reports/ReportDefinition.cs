using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Reports;

/// <summary>
/// One version of a governed, parameterised report (FG-02 §4.2 Report Definition and Report Version, ERD <c>reports.report_definition</c>):
/// its audience, its parameters and the registered projection fields it presents. Under the governed lifecycle (DRAFT → VALIDATED → PUBLISHED →
/// RETIRED; author, reviewer and publisher differ, ADM-037). A PUBLISHED version is immutable; a change is a new version, and publishing it retires
/// the one it replaces. No column holds a business value or an expression: a report names projections and fields of FG-01's register (BR-RPT-046).
/// Delete policy: RETAIN.
/// </summary>
public sealed class ReportDefinition : GovernedEntity
{
    public ReportCode Code { get; set; }

    /// <summary>1 for a code's first version, one more for each after it.</summary>
    public int VersionNo { get; set; }

    public required BilingualLabel Name { get; set; }

    public BilingualLabel? Description { get; set; }

    public ReportAudienceFamily AudienceFamily { get; set; }

    /// <summary>
    /// The registered projection whose permission and eligibility decide the report's rows (FG-02 REP-006 Binding purpose PRIMARY): a row is a
    /// project the caller reaches under it, or one of its snapshots.
    /// </summary>
    public required string PrimaryProjectionCode { get; set; }

    /// <summary>Whether a person may save a private set of the report's parameters (REP-010; ADR-019).</summary>
    public bool AllowsSavedViews { get; set; }
}

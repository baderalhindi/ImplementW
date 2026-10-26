namespace PMPlatform.Domain.Reports;

/// <summary>What a report job generates: a published report, or an SCR-138 composition of allowlisted fields.</summary>
public enum ReportJobKind
{
    Report = 1,
    Explorer = 2,
}

namespace PMPlatform.Domain.Reports;

/// <summary>What a saved view holds (ERD <c>view_type</c>).</summary>
public enum SavedViewType
{
    /// <summary>A private set of one report's parameter values (SCR-139, MOD-062).</summary>
    ReportParameters = 1,

    /// <summary>A private SCR-138 composition: allowlisted columns, sorts and filters (ADR-019).</summary>
    ExplorerComposition = 2,
}

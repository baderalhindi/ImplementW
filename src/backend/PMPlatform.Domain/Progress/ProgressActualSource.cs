namespace PMPlatform.Domain.Progress;

/// <summary>Where a submission's calculated figure comes from. A project with no work breakdown reports its declared figure until it has one.</summary>
public enum ProgressActualSource
{
    /// <summary>The duration-weighted roll-up of the project's tasks (ADR-009).</summary>
    WorkBreakdown = 1,

    /// <summary>The figure declared on the project, by import, follow-up or an earlier update.</summary>
    Declared = 2,

    /// <summary>Neither exists: the calculated figure is a placeholder, so the person submitting must enter one with a reason.</summary>
    NotAvailable = 3,
}

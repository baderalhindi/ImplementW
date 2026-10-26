namespace PMPlatform.Domain.Reports;

/// <summary>What a report parameter takes (FG-02 §6.1). Each narrows the authorised rows and never widens them (BR-RPT-007).</summary>
public enum ReportParameterDataType
{
    /// <summary>One project the caller reaches: a formal Project report, a project's history.</summary>
    Project = 1,

    /// <summary>One department among those of the projects the caller reaches (Hierarchical Scope).</summary>
    Department = 2,

    /// <summary>One of the parameter's fixed options: a value of the field it is bound to (Status/Condition; a mode or scope ADR-006 absorbs).</summary>
    Option = 3,
}

using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Reports;

/// <summary>
/// A parameter of a report version (FG-02 REP ReportParameterDefinition). An OPTION parameter is bound to a field of the report and filters its
/// rows to the option chosen; its options are the fixed values the version publishes. Delete policy: CASCADE, while the version is a DRAFT.
/// </summary>
public sealed class ReportParameter : AuditedEntity
{
    public Guid ReportDefinitionId { get; set; }

    public required string Code { get; set; }

    public required BilingualLabel Label { get; set; }

    public ReportParameterDataType DataType { get; set; }

    public bool IsRequired { get; set; }

    /// <summary>The parameter's place in the parameter panel, from 1.</summary>
    public short SortOrder { get; set; }

    /// <summary>For an OPTION parameter, the field it filters: its source entity.</summary>
    public string? SourceEntityCode { get; set; }

    /// <summary>For an OPTION parameter, the field it filters.</summary>
    public string? FieldCode { get; set; }
}

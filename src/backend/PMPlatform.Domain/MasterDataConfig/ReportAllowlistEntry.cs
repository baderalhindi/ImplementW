using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>A field the SCR-138 controlled explorer may expose (TASK-071, ADR-019). Delete policy: CASCADE.</summary>
public sealed class ReportAllowlistEntry : AuditedEntity
{
    public Guid ConfigurationVersionId { get; set; }

    public required string SourceEntityCode { get; set; }

    public required string FieldCode { get; set; }

    public required BilingualLabel Label { get; set; }

    public bool IsFilterable { get; set; }

    public bool IsSortable { get; set; }

    /// <summary>Masking applies at execution (ADR-010).</summary>
    public Guid? DataClassificationItemId { get; set; }
}

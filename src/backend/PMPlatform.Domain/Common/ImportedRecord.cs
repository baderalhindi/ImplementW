namespace PMPlatform.Domain.Common;

/// <summary>
/// The lineage of one imported row (Step 17 BE-027: "map legacy IDs … and preserve import lineage"): the legacy system's ID, the
/// record it became and the batch that loaded it. Unique per register and legacy ID, so a re-run creates no second identity.
/// Delete policy: APPEND_ONLY.
/// </summary>
public sealed class ImportedRecord : AuditedEntity
{
    public Guid ImportBatchId { get; set; }

    public required string RegisterCode { get; set; }

    /// <summary>The record's identifier in the legacy system, as the workbook gave it.</summary>
    public required string LegacyId { get; set; }

    /// <summary>The platform record the row became; a value, since it may be in any module's table.</summary>
    public Guid EntityId { get; set; }

    /// <summary>The worksheet row it came from.</summary>
    public int RowNumber { get; set; }
}

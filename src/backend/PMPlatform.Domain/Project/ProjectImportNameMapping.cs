namespace PMPlatform.Domain.Project;

/// <summary>
/// A name in an import workbook mapped to the platform value it means — an entity, department, sector, portfolio, initiative, region, city,
/// priority, governance level, status or person (R3-4). Remembered from a submitted batch, so the next import maps the same name the same way.
/// Delete policy: RETAIN; a later batch that maps the name otherwise replaces <see cref="TargetValue"/>.
/// </summary>
public sealed class ProjectImportNameMapping : Common.AuditedEntity
{
    /// <summary>What the name is of: ENTITY, DEPARTMENT, SECTOR, … (ProjectImportDimension, as text).</summary>
    public required string Dimension { get; set; }

    /// <summary>The name as normalized for matching: trimmed, single-spaced, without diacritics or tatweel, alef and yaa forms unified.</summary>
    public required string SourceKey { get; set; }

    /// <summary>The platform value: an id, or a lifecycle state's code for a STATUS mapping.</summary>
    public required string TargetValue { get; set; }
}

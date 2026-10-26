using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Reports;

/// <summary>
/// A role that may run a report version. An audience selects a report; it never grants data — every cell is authorised on its projection's own
/// permission (FG-02 §8.1). Delete policy: CASCADE, while the version is a DRAFT.
/// </summary>
public sealed class ReportAudienceRole : AuditedEntity
{
    public Guid ReportDefinitionId { get; set; }

    public Guid RoleId { get; set; }
}

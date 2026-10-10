using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Dashboards;

/// <summary>
/// A role that may open a dashboard version, and whether it is that role's default landing (Blueprint §20.2). An audience selects
/// a dashboard; it never grants data — every widget is authorised on its own source permission (DSH-CC-02, BR-DSH-008). Delete
/// policy: CASCADE, while the version is a DRAFT.
/// </summary>
public sealed class DashboardAudienceRole : AuditedEntity
{
    public Guid DashboardDefinitionId { get; set; }

    public Guid RoleId { get; set; }

    public bool IsDefaultLanding { get; set; }
}

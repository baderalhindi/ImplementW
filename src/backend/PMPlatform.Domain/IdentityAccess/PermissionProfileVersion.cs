using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IdentityAccess;

/// <summary>A version of a permission profile, immutable once published; assignments bind to a version (ADR-018). Delete policy: RETAIN.</summary>
public sealed class PermissionProfileVersion : GovernedEntity
{
    public Guid PermissionProfileId { get; set; }

    public int VersionNo { get; set; }

    public NarrativeText? ChangeSummary { get; set; }
}

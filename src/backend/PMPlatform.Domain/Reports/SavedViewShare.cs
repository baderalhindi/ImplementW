using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Reports;

/// <summary>
/// A role a saved view is shared with (TBC-RPT-20, enabled by TASK-072's amendment). A share carries the view's configuration to the role's holders
/// and nothing else: each runs it as they may see then, and none of its owner's access goes with it (ERD §5.20). The owner alone writes it, with the
/// view. Delete policy: CASCADE.
/// </summary>
public sealed class SavedViewShare : AuditedEntity
{
    public Guid SavedViewId { get; set; }

    public Guid SharedWithRoleId { get; set; }
}

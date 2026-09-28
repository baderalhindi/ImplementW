using PMPlatform.Domain.Approval;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Approval;

/// <summary>New and changed approval rows with their audit columns (ERD D-2).</summary>
internal static class ApprovalRows
{
    public static ApprovalTask NewTask(Guid instanceId, short sequenceNo, Guid roleId, DateTimeOffset? dueAt, Guid createdBy, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        ApprovalInstanceId = instanceId,
        SequenceNo = sequenceNo,
        AssignedRoleId = roleId,
        Status = ApprovalTaskStatus.Pending,
        DueAt = dueAt,
        CreatedAt = now,
        CreatedBy = createdBy,
        UpdatedAt = now,
        UpdatedBy = createdBy,
    };

    public static void Touch(AuditedEntity row, Guid by, DateTimeOffset now)
    {
        row.UpdatedAt = now;
        row.UpdatedBy = by;
    }
}

namespace PMPlatform.Application.Features.Approval;

/// <summary>The SERVICE principal (db/seed <c>svc.approval-workflow</c>) that escalates overdue tasks, expires delegations and records outcome delivery.</summary>
public static class ApprovalServicePrincipal
{
    public static readonly Guid Id = new("00000000-0000-4000-8000-0000000000fc");
}

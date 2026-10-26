namespace PMPlatform.Infrastructure.Approval;

/// <summary>Configuration section <c>Approval:Maintenance</c>: how often overdue approval tasks are escalated and lapsed delegations expired.</summary>
internal sealed class ApprovalMaintenanceOptions
{
    public const string Section = "Approval:Maintenance";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(5);

    public int BatchSize { get; set; } = 100;
}

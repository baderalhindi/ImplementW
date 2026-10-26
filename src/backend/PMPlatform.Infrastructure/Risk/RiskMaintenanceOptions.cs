namespace PMPlatform.Infrastructure.Risk;

/// <summary>Configuration section <c>Risk:Maintenance</c>: how often lapsed risk acceptances are expired.</summary>
internal sealed class RiskMaintenanceOptions
{
    public const string Section = "Risk:Maintenance";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(15);

    public int BatchSize { get; set; } = 100;
}

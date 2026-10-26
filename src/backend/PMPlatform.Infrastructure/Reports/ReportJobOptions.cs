namespace PMPlatform.Infrastructure.Reports;

/// <summary>
/// Configuration section <c>Reports:Jobs</c>: how often report jobs are validated, generated and expired, and the limits FG-02 leaves to AHDA —
/// how long an output stays downloadable (TBC-RPT-12), how many rows an export may hold (TBC-RPT-10), and how long a job may sit VALIDATING or
/// RUNNING before its worker is taken to have stopped. The defaults are the delivery team's until AHDA sets them (reports.md F-6).
/// </summary>
internal sealed class ReportJobOptions
{
    public const string Section = "Reports:Jobs";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(15);

    public int BatchSize { get; set; } = 10;

    public TimeSpan OutputLifetime { get; set; } = TimeSpan.FromDays(1);

    public int MaxExportRows { get; set; } = 10_000;

    public TimeSpan AbandonAfter { get; set; } = TimeSpan.FromMinutes(15);
}

namespace PMPlatform.Infrastructure.Suspension;

/// <summary>Configuration section <c>Suspension:Activation</c>: how often approved requests whose effective date has come are activated.</summary>
internal sealed class SuspensionActivationOptions
{
    public const string Section = "Suspension:Activation";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(5);

    public int BatchSize { get; set; } = 100;
}

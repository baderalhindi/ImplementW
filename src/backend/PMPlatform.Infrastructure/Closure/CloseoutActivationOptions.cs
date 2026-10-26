namespace PMPlatform.Infrastructure.Closure;

/// <summary>Configuration section <c>Closure:Activation</c>: how often approved completion and closure cases are activated.</summary>
internal sealed class CloseoutActivationOptions
{
    public const string Section = "Closure:Activation";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(1);

    public int BatchSize { get; set; } = 100;
}

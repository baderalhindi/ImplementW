using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IntegrationMonitoring;

/// <summary>
/// A named external integration, independent of any environment (Blueprint §17.1, FG-05 §3): the directory, single sign-on, MFA,
/// Nafath, the Exchange relay, the SMS provider. Its retry policy governs the operations FG-05 runs itself. Delete policy: RETAIN.
/// </summary>
public sealed class IntegrationDefinition : AuditedEntity
{
    /// <summary>Stable and language-neutral, e.g. <c>DIRECTORY</c>; producers name the integration by it.</summary>
    public required string Code { get; set; }

    public required BilingualLabel Name { get; set; }

    public IntegrationDirection Direction { get; set; }

    public IntegrationKind IntegrationKind { get; set; }

    /// <summary>The role accountable for the integration; null until AHDA names one (TBC-INT-19).</summary>
    public Guid? OwnerRoleId { get; set; }

    /// <summary>Attempts of an FG-05 operation before it is dead-lettered, the first included.</summary>
    public short MaxAttempts { get; set; } = 3;

    /// <summary>The wait after a first failed attempt; each later failure doubles it.</summary>
    public int RetryBackoffSeconds { get; set; } = 60;

    public IntegrationDefinitionStatus Status { get; set; }
}

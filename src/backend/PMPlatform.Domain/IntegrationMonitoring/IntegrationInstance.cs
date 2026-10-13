using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IntegrationMonitoring;

/// <summary>
/// A definition as this environment deploys it (Blueprint §17.1). Each environment has its own database, so every instance in it
/// is that environment's. Availability and freshness are two columns because they are two indicators (ADM-047, BR-INT-013); both
/// are an operational projection the runtime rewrites, never a business state (ERD derived register row 21). The endpoint is
/// named by its configuration key, never by a secret. Delete policy: RETAIN.
/// </summary>
public sealed class IntegrationInstance : AuditedEntity
{
    public Guid IntegrationDefinitionId { get; set; }

    /// <summary>Unique within its definition, e.g. <c>PRIMARY</c>.</summary>
    public required string Code { get; set; }

    /// <summary>The configuration or secret entry that holds the endpoint, e.g. <c>AD_LDAP_URL</c>: its name, never its value.</summary>
    public required string EndpointConfigurationKey { get; set; }

    public bool IsEnabled { get; set; } = true;

    public IntegrationAvailability Availability { get; set; } = IntegrationAvailability.Unknown;

    public IntegrationFreshness Freshness { get; set; } = IntegrationFreshness.Unknown;

    /// <summary>The end of the latest attempt that succeeded technically.</summary>
    public DateTimeOffset? LastSuccessAt { get; set; }

    /// <summary>The end of the latest attempt that failed technically.</summary>
    public DateTimeOffset? LastFailureAt { get; set; }

    /// <summary>When availability and freshness were last evaluated: by an attempt recorded, or by the health pass.</summary>
    public DateTimeOffset? EvaluatedAt { get; set; }
}

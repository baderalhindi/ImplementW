namespace PMPlatform.Domain.IntegrationMonitoring;

/// <summary>
/// Why an attempt failed (FG-05 §6.3, DC-INT-04). Only a technical failure says anything about the provider's
/// availability; a business or security refusal is the provider working as intended.
/// </summary>
public enum InvocationFailureCategory
{
    /// <summary>Network, provider, timeout or transport fault: counts against availability and may be retried.</summary>
    Technical = 1,

    /// <summary>The provider answered and refused the content: a wrong password, an unverified identity. Never retried by FG-05.</summary>
    BusinessValidation = 2,

    /// <summary>A signature, token or authorisation check failed. Never retried.</summary>
    Security = 3,
}

namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>One connection test. <see cref="FailureCode"/> is a fixed code, never the provider's own message.</summary>
public sealed record ConnectionTestResult(ConnectionTestOutcome Outcome, DateTimeOffset TestedAt, string? FailureCode)
{
    /// <summary>The directory or provider refused the service account's bind (<c>AD_BIND_DN</c>, <c>AD_BIND_PASSWORD</c>).</summary>
    public const string BindRejected = "BIND_REJECTED";

    /// <summary>No connection, a TLS failure or a timeout.</summary>
    public const string Unreachable = "UNREACHABLE";

    /// <summary>The discovery document's issuer is not <c>SSO_OIDC_AUTHORITY</c>.</summary>
    public const string IssuerMismatch = "ISSUER_MISMATCH";
}

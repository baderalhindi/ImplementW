namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>The number the user confirmed, and when.</summary>
public sealed record MobileVerificationResult(string MobileNumber, DateTimeOffset MobileVerifiedAt);

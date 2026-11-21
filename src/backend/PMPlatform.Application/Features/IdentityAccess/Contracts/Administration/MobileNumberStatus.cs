namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// The signed-in person's own mobile number as their account page shows it: the number an administrator recorded, when its
/// holder verified it (ADR-004), and whether this environment can send the code to verify it.
/// </summary>
public sealed record MobileNumberStatus(string? MobileNumber, DateTimeOffset? VerifiedAt, bool VerificationAvailable);

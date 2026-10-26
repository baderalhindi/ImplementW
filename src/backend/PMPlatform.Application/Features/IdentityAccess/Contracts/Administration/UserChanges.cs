using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// ADM-005 Edit User: the editable representation in full (R-5). User type and external entity are fixed at creation.
/// A changed mobile number is unverified until its holder confirms it (ADR-004).
/// </summary>
public sealed record UserChanges(
    string Username,
    string DisplayName,
    string Email,
    string? MobileNumber,
    Language PreferredLanguage,
    string? DirectorySubjectId,
    string? JobTitle);

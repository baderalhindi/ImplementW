using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// ADM-004 Create User. An internal user's department, manager and job title come from the directory at sign-in
/// (ADR-007), so only an external user's job title is entered. <see cref="ExternalEntityId"/> is required for an
/// external user and absent for an internal one.
/// </summary>
public sealed record UserDraft(
    UserType UserType,
    string Username,
    string DisplayName,
    string Email,
    string? MobileNumber,
    Language PreferredLanguage,
    string? DirectorySubjectId,
    string? JobTitle,
    Guid? ExternalEntityId);

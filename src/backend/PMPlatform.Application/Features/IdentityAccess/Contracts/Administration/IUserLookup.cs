using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// A user named the way a person names one (E-U1): a migration workbook gives a project manager, an owner or a sponsor by username
/// (TASK-102). Whether that user may hold the role is still the owning module's rule.
/// </summary>
public interface IUserLookup
{
    /// <summary>The user with this username, whatever their state; null when none has it.</summary>
    public Task<Guid?> FindIdByUsernameAsync(string username, CancellationToken cancellationToken);

    /// <summary>
    /// Every ACTIVE user, with what a person is known by in a spreadsheet — username, display name and e-mail — so an import can match a name
    /// to an account (the 2 Nov 2026 review, R3-4).
    /// </summary>
    public Task<IReadOnlyList<UserIdentity>> ListActiveAsync(CancellationToken cancellationToken);
}

public sealed record UserIdentity(Guid Id, string Username, string DisplayName, string Email, UserType UserType);

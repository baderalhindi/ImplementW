namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// A user named the way a person names one (E-U1): a migration workbook gives a project manager, an owner or a sponsor by username
/// (TASK-102). Whether that user may hold the role is still the owning module's rule.
/// </summary>
public interface IUserLookup
{
    /// <summary>The user with this username, whatever their state; null when none has it.</summary>
    public Task<Guid?> FindIdByUsernameAsync(string username, CancellationToken cancellationToken);
}

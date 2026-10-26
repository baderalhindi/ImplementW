namespace PMPlatform.Application.Common.Authorization;

/// <summary>What the engine reads: the principal, the classification order and the field classifications in force.</summary>
public interface IAuthorizationRepository
{
    /// <summary>The user with the grants of their assignments in force now; null if there is no such user.</summary>
    public Task<AuthorizationPrincipal?> FindPrincipalAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The rank of every DATA_CLASSIFICATION item: its sort order, higher being more sensitive. A clearance covers every
    /// classification of equal or lower rank.
    /// </summary>
    public Task<IReadOnlyDictionary<Guid, int>> GetClassificationRanksAsync(CancellationToken cancellationToken);

    /// <summary>The classified fields of <paramref name="entityCode"/> in the PUBLISHED FIELD_CLASSIFICATION version in force now.</summary>
    public Task<IReadOnlyList<FieldClassification>> GetFieldClassificationsAsync(string entityCode, CancellationToken cancellationToken);
}

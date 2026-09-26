using PMPlatform.Application.Common.Authorization;

namespace PMPlatform.Tests.Unit.Application.Authorization;

/// <summary>The engine's reads, held in memory.</summary>
internal sealed class FakeAuthorizationRepository : IAuthorizationRepository
{
    public Dictionary<Guid, AuthorizationPrincipal> Principals { get; } = [];

    public Dictionary<Guid, int> ClassificationRanks { get; } = [];

    public Dictionary<string, List<FieldClassification>> FieldClassifications { get; } = new(StringComparer.Ordinal);

    public int PrincipalReads { get; private set; }

    public Task<AuthorizationPrincipal?> FindPrincipalAsync(Guid userId, CancellationToken cancellationToken)
    {
        PrincipalReads++;
        return Task.FromResult(Principals.GetValueOrDefault(userId));
    }

    public Task<IReadOnlyDictionary<Guid, int>> GetClassificationRanksAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(ClassificationRanks);

    public Task<IReadOnlyList<FieldClassification>> GetFieldClassificationsAsync(string entityCode, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FieldClassification>>(FieldClassifications.GetValueOrDefault(entityCode) ?? []);
}

using PMPlatform.Application.Features.Risk.Contracts;

namespace PMPlatform.Application.Features.Risk;

/// <summary>A project's risks neither closed nor accepted as residual, for WF-10's readiness (edge 42).</summary>
internal sealed class RiskCloseoutReader(IRiskRepository repository) : IRiskCloseoutReader
{
    public Task<int> CountOpenAsync(Guid projectId, CancellationToken cancellationToken) => repository.CountOpenUnacceptedAsync(projectId, cancellationToken);
}

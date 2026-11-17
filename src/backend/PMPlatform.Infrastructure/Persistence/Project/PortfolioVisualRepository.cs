using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.Project.Insights;
using PMPlatform.Domain.Project;

namespace PMPlatform.Infrastructure.Persistence.Project;

/// <summary>The Portfolio dashboard's design (17 Nov 2026): saved and read as a whole, in reading order.</summary>
internal sealed class PortfolioVisualRepository(PMPlatformDbContext context) : IPortfolioVisualRepository
{
    public async Task<IReadOnlyList<PortfolioVisual>> ListAsync(CancellationToken cancellationToken) =>
        await context.Set<PortfolioVisual>().AsNoTracking().OrderBy(v => v.SortOrder).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task ReplaceAsync(IReadOnlyList<PortfolioVisual> visuals, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await context.Set<PortfolioVisual>().ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        context.AddRange(visuals);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}

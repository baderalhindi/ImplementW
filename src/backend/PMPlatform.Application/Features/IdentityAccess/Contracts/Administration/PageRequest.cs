namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>Offset paging (api-conventions R-29): <see cref="Page"/> is 1-based.</summary>
public sealed record PageRequest(int Page, int PageSize)
{
    public int Skip => (Page - 1) * PageSize;
}

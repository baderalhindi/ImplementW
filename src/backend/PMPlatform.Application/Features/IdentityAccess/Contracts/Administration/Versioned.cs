namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>A representation with the concurrency version of its row (PostgreSQL <c>xmin</c>, ERD D-16): the ETag of R-21.</summary>
public sealed record Versioned<T>(T Value, uint Version)
    where T : class;

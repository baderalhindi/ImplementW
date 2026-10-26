namespace PMPlatform.Application.Common.Auditing;

/// <summary>The request an audit event was produced in. Outside a request there is no client and no signed-in user.</summary>
public interface IAuditRequestContext
{
    /// <summary>The request's <c>X-Correlation-Id</c> (api-conventions R-41), or an id for the unit of work outside a request.</summary>
    public Guid CorrelationId { get; }

    /// <summary>The client's network address as the API sees it; null outside a request.</summary>
    public string? ClientAddress { get; }

    /// <summary>The <c>sub</c> of the request's validated access token; null if the caller has none.</summary>
    public Guid? UserId { get; }
}

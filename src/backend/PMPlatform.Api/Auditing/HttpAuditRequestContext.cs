using System.Security.Claims;
using RequestCorrelation = PMPlatform.Api.Correlation.CorrelationId;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Api.Auditing;

/// <summary>
/// The HTTP request an audit event is produced in (TASK-033). Outside a request, as in the SIEM forwarder, there is no
/// client or user, and the unit of work gets a correlation id of its own.
/// </summary>
internal sealed class HttpAuditRequestContext(IHttpContextAccessor accessor) : IAuditRequestContext
{
    public Guid CorrelationId =>
        accessor.HttpContext is { } context && RequestCorrelation.Of(context) is var id && id != Guid.Empty ? id : UnitOfWorkId;

    /// <summary>The peer address. Behind AHDA's load balancer this is the balancer until forwarded headers are configured (record F-5).</summary>
    public string? ClientAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    private Guid UnitOfWorkId { get; } = Guid.NewGuid();

    public Guid? UserId =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user && Guid.TryParse(user.FindFirstValue(SessionTokenClaims.Subject), out Guid id)
            ? id
            : null;
}

using System.Security.Claims;
using RequestCorrelation = PMPlatform.Api.Correlation.CorrelationId;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Correlation;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Api.Auditing;

/// <summary>
/// The HTTP request an audit event is produced in (TASK-033). Outside a request, as in the SIEM forwarder, there is no
/// client or user, and the unit of work gets a correlation id of its own — unless it continues a lineage that set a
/// <see cref="CorrelationScope"/>, as an integration retry does (TASK-075).
/// </summary>
internal sealed class HttpAuditRequestContext(IHttpContextAccessor accessor) : IAuditRequestContext
{
    public Guid CorrelationId =>
        CorrelationScope.Current
        ?? (accessor.HttpContext is { } context && RequestCorrelation.Of(context) is var id && id != Guid.Empty ? id : UnitOfWorkId);

    /// <summary>The client address: behind AHDA's load balancer the forwarded one, as rate limiting reads it (TASK-078, <c>endpoint-hardening.md</c> D-4).</summary>
    public string? ClientAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    private Guid UnitOfWorkId { get; } = Guid.NewGuid();

    public Guid? UserId =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user && Guid.TryParse(user.FindFirstValue(SessionTokenClaims.Subject), out Guid id)
            ? id
            : null;
}

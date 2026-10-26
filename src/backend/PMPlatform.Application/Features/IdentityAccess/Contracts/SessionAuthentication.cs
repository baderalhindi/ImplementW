namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// How and when the session's user last proved who they are. <see cref="MultiFactor"/> is true once a second factor has
/// been passed in this session; <see cref="AuthenticatedAt"/> is the time step-up freshness is measured from.
/// </summary>
public sealed record SessionAuthentication(AuthenticationMethod Method, bool MultiFactor, DateTimeOffset AuthenticatedAt);

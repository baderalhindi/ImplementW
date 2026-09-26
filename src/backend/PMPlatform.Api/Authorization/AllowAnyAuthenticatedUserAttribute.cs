namespace PMPlatform.Api.Authorization;

/// <summary>
/// Declares that an endpoint acts only on the caller's own session, so a valid access token is its whole protection and
/// it asks the engine for no permission. Every endpoint declares this, <c>[AllowAnonymous]</c> or
/// <see cref="RequirePermissionAttribute"/>; start-up fails on one that declares none (<see cref="EndpointAuthorization"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class AllowAnyAuthenticatedUserAttribute : Attribute;

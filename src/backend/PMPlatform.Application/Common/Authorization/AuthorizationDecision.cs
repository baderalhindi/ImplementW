namespace PMPlatform.Application.Common.Authorization;

public sealed record AuthorizationDecision(AuthorizationOutcome Outcome, AuthorizationDenial Denial)
{
    public static AuthorizationDecision Allowed { get; } = new(AuthorizationOutcome.Allowed, AuthorizationDenial.None);

    public bool IsAllowed => Outcome == AuthorizationOutcome.Allowed;

    public static AuthorizationDecision Forbidden(AuthorizationDenial denial) => new(AuthorizationOutcome.Forbidden, denial);

    public static AuthorizationDecision NotFound(AuthorizationDenial denial) => new(AuthorizationOutcome.NotFound, denial);
}

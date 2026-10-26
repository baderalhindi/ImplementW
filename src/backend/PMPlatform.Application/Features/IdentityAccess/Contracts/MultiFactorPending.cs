namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// The first factor passed, but the person must pass a second before a session is issued. <see cref="MfaToken"/> is
/// accepted only by the MFA challenge and MFA sign-in endpoints: it is not an access token and not a refresh token.
/// <see cref="EnrolmentRequired"/> is true when the person has no enrolled factor and enrols one on this sign-in.
/// </summary>
public sealed record MultiFactorPending(string MfaToken, DateTimeOffset MfaTokenExpiresAt, bool EnrolmentRequired);

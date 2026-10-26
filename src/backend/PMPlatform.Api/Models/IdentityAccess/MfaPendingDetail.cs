namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>
/// The first factor passed; no session yet (TASK-029). The client starts the second factor with <c>mfaToken</c>
/// (<c>POST /api/v1/sessions/mfa-challenge</c>) and completes it (<c>POST /api/v1/sessions/mfa</c>). The token opens
/// nothing else: every other endpoint refuses it. <c>enrolmentRequired</c> is true when the person has no factor yet
/// and enrols one on this sign-in.
/// </summary>
public sealed record MfaPendingDetail(string MfaToken, DateTimeOffset MfaTokenExpiresAt, bool EnrolmentRequired);

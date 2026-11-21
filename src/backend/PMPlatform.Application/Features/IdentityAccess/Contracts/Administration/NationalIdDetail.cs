namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>A user's full National ID or Iqama number, for a holder of USER_NATIONAL_ID_VIEW; the reveal is audited.</summary>
public sealed record NationalIdDetail(string NationalId);

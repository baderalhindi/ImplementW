namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>The sort orders ADM-002 offers (R-32); each ends on the user id so pages are stable.</summary>
public enum UserSort
{
    DisplayNameAscending = 1,
    DisplayNameDescending = 2,
    UsernameAscending = 3,
    UsernameDescending = 4,
}

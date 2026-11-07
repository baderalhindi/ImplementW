namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// A person as the directory holds them: the immutable subject that links them to their platform user, and the three
/// attributes ADR-007 makes directory-authoritative. <see cref="DepartmentReference"/> matches
/// <c>Department.DirectoryReference</c>; <see cref="ManagerSubjectId"/> is the manager's own subject. <see cref="Email"/> is where a
/// forgotten password's link goes, because the platform's copy of the address can be edited on the platform.
/// </summary>
public sealed record DirectoryEntry(string SubjectId, string? JobTitle, string? DepartmentReference, string? ManagerSubjectId, string? Email = null);

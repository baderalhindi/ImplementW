namespace PMPlatform.Application.Common.Authorization;

/// <summary>
/// A permission, and the record it is exercised on. Without a <see cref="Subject"/> the request asks only whether the
/// caller holds the permission at some scope: the gate on an endpoint, before the record is known.
/// </summary>
public sealed record AuthorizationRequest(string PermissionCode, AuthorizationSubject? Subject = null);

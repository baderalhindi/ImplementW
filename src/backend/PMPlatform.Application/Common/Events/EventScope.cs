namespace PMPlatform.Application.Common.Events;

/// <summary>Authorization and routing anchors supplied by the producer (M-7).</summary>
public sealed record EventScope(Guid? ProjectId, Guid? DepartmentId, Guid? ExternalEntityId);

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// What submitting adds to the draft: its Project Manager, an R04 holder over the project's anchors, internal or of the
/// delivering entity (ADR-013: the role is employer-neutral, so the manager is not resolved from the AHDA directory alone).
/// </summary>
public sealed record ProjectSubmission(Guid ProjectManagerUserId);

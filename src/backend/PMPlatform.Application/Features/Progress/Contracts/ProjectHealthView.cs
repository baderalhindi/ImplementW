namespace PMPlatform.Application.Features.Progress.Contracts;

/// <summary>
/// Overall Project Health in its two semantic states, never merged: <see cref="Published"/> is the latest
/// PUBLISHED/OFFICIAL snapshot, <see cref="Current"/> the CURRENT/LIVE value. Either is null until it first exists.
/// </summary>
public sealed record ProjectHealthView(PublishedProgressSnapshotDetail? Published, ProjectHealthStatusDetail? Current);

using PMPlatform.Domain.Common;
using PMPlatform.Domain.Suspension;

namespace PMPlatform.Application.Features.Suspension.Contracts;

/// <summary>A new suspension or resumption request of the project, DRAFT. A draft may lack its dates; submission checks them.</summary>
public sealed record SuspensionRequestDraft(Guid ProjectId, SuspensionRequestType RequestType, SuspensionRequestChanges Fields);

/// <summary>A request's own fields, as a whole (R-5). Its type, project and status change only through the commands.</summary>
public sealed record SuspensionRequestChanges(NarrativeText Reason, DateOnly? RequestedEffectiveDate, DateOnly? PlannedResumptionDate);

/// <summary>A project's requests, filtered by any of <see cref="RequestTypes"/> and any of <see cref="Statuses"/>.</summary>
public sealed record SuspensionRequestQuery(Guid ProjectId, IReadOnlyCollection<SuspensionRequestType> RequestTypes, IReadOnlyCollection<SuspensionRequestStatus> Statuses);

/// <summary>A project's suspension periods; with <see cref="Open"/>, only the open one (true) or only ended ones (false).</summary>
public sealed record ActiveSuspensionQuery(Guid ProjectId, bool? Open);

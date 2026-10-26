using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress.Contracts;

public sealed record ReportingCycleSummary(Guid Id, Guid ProjectId, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly DueDate, ReportingCycleStatus Status);

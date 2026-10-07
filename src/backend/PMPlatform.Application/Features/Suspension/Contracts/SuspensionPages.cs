namespace PMPlatform.Application.Features.Suspension.Contracts;

/// <summary>A project's suspension and resumption requests, most recently changed first (R-29; indexing-strategy P-2, I-29).</summary>
public sealed record SuspensionRequestPage(IReadOnlyList<SuspensionRequestDetail> Items, int Page, int PageSize, int TotalCount);

/// <summary>A project's suspension periods, most recently started first: its suspension history across cycles (BR-SUS-039).</summary>
public sealed record ActiveSuspensionPage(IReadOnlyList<ActiveSuspensionDetail> Items, int Page, int PageSize, int TotalCount);

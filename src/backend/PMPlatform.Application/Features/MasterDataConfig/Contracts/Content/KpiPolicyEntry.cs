namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

/// <summary>The calculation rule and RAG thresholds of a PUBLISHED KPI definition (OQ-006).</summary>
public sealed record KpiPolicyEntry(Guid KpiDefinitionId, string? CalculationExpression, decimal? GreenThreshold, decimal? AmberThreshold);

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

/// <summary>A matrix cell, naming its rating by code within the same version.</summary>
public sealed record RiskMatrixCellEntry(short ProbabilityLevel, short ImpactLevel, string RatingCode);

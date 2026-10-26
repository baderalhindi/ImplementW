namespace PMPlatform.Domain.MasterDataConfig;

/// <summary>What an audience without clearance sees of a classified field (ADR-010).</summary>
public enum MaskingRule
{
    Withhold = 1,
    Mask = 2,
    Reveal = 3,
}
